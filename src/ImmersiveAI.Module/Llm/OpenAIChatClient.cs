using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Llm
{
    /// <summary>OpenAI chat-completions client (raw HTTP, .NET Framework compatible). Supports
    /// native function calling, which carries the NPCs' "recall the world" ability. The endpoint
    /// is configurable (<see cref="ModConfig.OpenAIBaseUrl"/>), so the same client speaks to any
    /// OpenAI-compatible service — OpenRouter, NanoGPT, a local server.</summary>
    public sealed class OpenAIChatClient : IToolChatClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly string _endpoint;
        private readonly string _label;

        static OpenAIChatClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public OpenAIChatClient(string apiKey, string model, int maxTokens, string? endpoint = null, string? providerLabel = null)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-5.4-mini" : model;
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
            _endpoint = string.IsNullOrWhiteSpace(endpoint) ? ModConfig.DefaultOpenAIEndpoint : endpoint;
            // Errors and log lines name the true provider ("OpenRouter request failed…"), so a
            // router problem never sends the player checking their OpenAI account.
            _label = string.IsNullOrWhiteSpace(providerLabel) ? "OpenAI" : providerLabel;
        }

        private bool IsOpenRouter =>
            _endpoint.IndexOf("openrouter.ai", StringComparison.OrdinalIgnoreCase) >= 0;

        // A provider-prefixed id ("openai/gpt-5.4-mini") is the OpenRouter/NanoGPT convention: the
        // request rides through a router that translates parameters per provider. Those get the
        // universal shape (classic max_tokens), never the OpenAI-only fields below.
        private bool IsRoutedModel => _model.IndexOf('/') >= 0;

        // The gpt-5.x family and the o-series reject the classic max_tokens in favor of
        // max_completion_tokens, and carry the reasoning_effort dial; older models keep the
        // classic shape. Getting this wrong is a hard 400, so it keys off the model id.
        private bool IsReasoningFamily =>
            _model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var result = await SendAsync(messages, null, allowToolUse: false, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.Text))
                throw new InvalidOperationException("OpenAI returned an empty response.");
            return result.Text;
        }

        public Task<ChatResult> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool allowToolUse = true,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(messages, tools, allowToolUse, cancellationToken);
        }

        private async Task<ChatResult> SendAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            bool allowToolUse,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException(_label + " API key is not set. Add it to " + ModConfig.ConfigFilePath);
            if (!UsageLedger.CanCall(out var capReason))
                throw new InvalidOperationException(capReason);

            // Reasoning is OFF for good (2026.07.13, Anton's call): silent thinking spends billed
            // tokens against the reply's budget and slows every answer — an NPC that "thinks" too
            // long answers with silence. "none" is sent explicitly because omitting the dial lets
            // the API default to reasoning. (It also sidesteps gpt-5.6's refusal to combine
            // function tools with reasoning on chat completions, hit live 2026.07.12.)
            var reasoningFamily = !IsRoutedModel && IsReasoningFamily;
            var payload = new JObject
            {
                ["model"] = _model,
                [reasoningFamily ? "max_completion_tokens" : "max_tokens"] = _maxTokens,
                ["messages"] = BuildTurns(messages),
            };
            if (reasoningFamily)
                payload["reasoning_effort"] = "none";
            else if (IsRoutedModel)
                // The routers' own unified reasoning switch (documented as ignored by models that
                // cannot reason) — without it, a routed gpt-5.x thinks at its default effort and
                // spends the spoken budget on silence, the very "..." bug of 2026.07.13.
                payload["reasoning"] = new JObject { ["enabled"] = false };

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(tools);
                // Definitions always ride along (a history holding tool calls needs them to validate);
                // "none" is how a final, spoken-answer-only round is enforced.
                if (!allowToolUse) payload["tool_choice"] = "none";
            }

            var payloadText = payload.ToString(Formatting.None);

            var (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);

            // Some routed models cannot have their thinking turned off — fable, grok-4.5 and
            // gemini-3.5 all answer 400 "Reasoning is mandatory for this endpoint" (verified live
            // 2026.07.16). Drop the reasoning field and let such a model think as it must: better
            // a thoughtful reply than a refused one, and no per-model list to keep.
            if (status == 400 && payload["reasoning"] != null
                && body.IndexOf("Reasoning is mandatory", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                payload.Remove("reasoning");
                payloadText = payload.ToString(Formatting.None);
                ModLog.Warn($"{_label}: '{_model}' cannot run with reasoning disabled — retrying with its own thinking allowed.");
                (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);
            }

            // OpenAI's "insufficient permissions" 401 shows up INTERMITTENTLY for a while after
            // the account's model access is changed (their ACL propagating — observed live
            // 2026.07.12: the same request shape succeeding and failing seconds apart, and
            // one 1.5s retry still losing whole replies HOURS after the grant). Three retries
            // with growing pauses ride out a stale server; a truly bad key fails them all and
            // is reported as before.
            for (int attempt = 1; attempt <= 3 && status == 401
                 && body.IndexOf("insufficient permissions", StringComparison.OrdinalIgnoreCase) >= 0; attempt++)
            {
                ModLog.Warn($"{_label} answered 401 'insufficient permissions' — retry {attempt} of 3 (fresh access changes propagate slowly on their side).");
                await Task.Delay(1500 * attempt, cancellationToken).ConfigureAwait(false);
                (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);
            }

            if (status < 200 || status >= 300)
            {
                LlmGate.ReportFailure(status, _label, body);
                throw new InvalidOperationException($"{_label} request failed ({status}): {Truncate(body, 400)}");
            }

            var json = JObject.Parse(body);

            // The API measures its own tokens — hand them to the ledger, and tell the
            // gate the road is open again.
            UsageLedger.RecordCall(_model,
                (int?)json.SelectToken("usage.prompt_tokens") ?? 0,
                (int?)json.SelectToken("usage.completion_tokens") ?? 0);
            LlmGate.ReportSuccess();

            var message = json.SelectToken("choices[0].message");

            var text = ((string?)message?["content"] ?? string.Empty).Trim();

            var calls = (message?["tool_calls"] as JArray ?? new JArray())
                .Where(c => (string?)c["type"] == "function")
                .Select(c => new ToolCall(
                    (string?)c["id"] ?? "",
                    (string?)c.SelectToken("function.name") ?? "",
                    (string?)c.SelectToken("function.arguments") ?? "{}"))
                .ToList();

            return new ChatResult(text, calls);
        }

        /// <summary>One POST to the chat endpoint: status + body, never throwing on an API error
        /// status (the caller decides about retries). A failed CONNECTION still throws, after
        /// telling the gate.</summary>
        private async Task<(int Status, string Body)> PostOnceAsync(string payloadText, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                request.Headers.Add("Authorization", "Bearer " + _apiKey);
                if (IsOpenRouter)
                {
                    // OpenRouter's optional app attribution — shows the mod by name in the user's
                    // own activity view instead of an anonymous key.
                    request.Headers.Add("HTTP-Referer", "https://www.nexusmods.com/mountandblade2bannerlord/mods/12119");
                    request.Headers.Add("X-Title", "Immersive AI (Bannerlord)");
                }
                request.Content = new StringContent(payloadText, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The connection itself failed (or timed out) — quiet the autonomous flows.
                    LlmGate.ReportFailure(0, _label, ex.Message);
                    throw;
                }
                using (response)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ((int)response.StatusCode, body);
                }
            }
        }

        private static JArray BuildTurns(IReadOnlyList<ChatMessage> messages)
        {
            var turns = new JArray();
            foreach (var m in messages)
            {
                if (m.Role == ChatRole.Tool)
                {
                    turns.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = m.ToolCallId ?? "",
                        ["content"] = m.Content,
                    });
                    continue;
                }

                if (m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0)
                {
                    var toolCalls = new JArray();
                    foreach (var call in m.ToolCalls)
                    {
                        toolCalls.Add(new JObject
                        {
                            ["id"] = call.Id,
                            ["type"] = "function",
                            ["function"] = new JObject
                            {
                                ["name"] = call.Name,
                                ["arguments"] = call.ArgumentsJson,
                            },
                        });
                    }

                    var turn = new JObject { ["role"] = "assistant", ["tool_calls"] = toolCalls };
                    // OpenAI wants content null (not "") when the turn is purely a tool reach.
                    turn["content"] = string.IsNullOrWhiteSpace(m.Content) ? JValue.CreateNull() : (JToken)m.Content;
                    turns.Add(turn);
                    continue;
                }

                turns.Add(new JObject
                {
                    ["role"] = m.Role == ChatRole.System ? "system" : m.Role == ChatRole.User ? "user" : "assistant",
                    ["content"] = m.Content,
                });
            }
            return turns;
        }

        private static JArray BuildTools(IReadOnlyList<ToolDefinition> tools)
        {
            var arr = new JArray();
            foreach (var tool in tools)
            {
                var properties = new JObject();
                var required = new JArray();
                foreach (var p in tool.Parameters)
                {
                    properties[p.Name] = new JObject { ["type"] = "string", ["description"] = p.Description };
                    if (p.Required) required.Add(p.Name);
                }

                arr.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = properties,
                            ["required"] = required,
                        },
                    },
                });
            }
            return arr;
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
