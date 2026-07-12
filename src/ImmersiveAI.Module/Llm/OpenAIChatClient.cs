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
    /// native function calling, which carries the NPCs' "recall the world" ability.</summary>
    public sealed class OpenAIChatClient : IToolChatClient
    {
        private const string Endpoint = "https://api.openai.com/v1/chat/completions";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly string _reasoningEffort;

        static OpenAIChatClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public OpenAIChatClient(string apiKey, string model, int maxTokens, string? reasoningEffort = null)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-5.6-luna" : model;
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
            _reasoningEffort = (reasoningEffort ?? string.Empty).Trim();
        }

        // The gpt-5.x family and the o-series reject the classic max_tokens in favor of
        // max_completion_tokens, and carry the reasoning_effort dial; older models keep the
        // classic shape. Getting this wrong is a hard 400, so it keys off the model id.
        private bool IsReasoningFamily =>
            _model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            || _model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);

        // Reasoning models spend their THINKING against max_completion_tokens too, so the
        // configured MaxTokens (meant as the spoken reply's budget) gets headroom on top —
        // otherwise a small budget dies mid-thought with "Could not finish the message"
        // (2026.07.12, found by the 16-token health-check ping killing gpt-5.6-terra).
        private int EffectiveMaxTokens(string effort)
        {
            if (!IsReasoningFamily) return _maxTokens;
            switch (effort)
            {
                case "none": return _maxTokens;
                case "minimal":
                case "low": return _maxTokens + 512;
                case "medium":
                case "": return _maxTokens + 1024;  // "" lets the API pick; give it medium's room
                default: return _maxTokens + 2048;  // high / xhigh / max think long
            }
        }

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
                throw new InvalidOperationException("OpenAI API key is not set. Add it to " + ModConfig.ConfigFilePath);
            if (!UsageLedger.CanCall(out var capReason))
                throw new InvalidOperationException(capReason);

            // gpt-5.6 on chat completions refuses function tools + reasoning together (live error,
            // 2026.07.12: "Function tools with reasoning_effort are not supported… use /v1/responses
            // or set reasoning_effort to 'none'"). Migrating to /v1/responses is a post-V1 task;
            // until then every tool-carrying call rides at 'none' and plain calls (the feeling
            // number, yes/no desires, search refining) keep the configured effort.
            var effort = _reasoningEffort;
            if (tools != null && tools.Count > 0) effort = "none";

            var payload = new JObject
            {
                ["model"] = _model,
                [IsReasoningFamily ? "max_completion_tokens" : "max_tokens"] = EffectiveMaxTokens(effort),
                ["messages"] = BuildTurns(messages),
            };
            if (IsReasoningFamily && effort.Length > 0)
                payload["reasoning_effort"] = effort;

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(tools);
                // Definitions always ride along (a history holding tool calls needs them to validate);
                // "none" is how a final, spoken-answer-only round is enforced.
                if (!allowToolUse) payload["tool_choice"] = "none";
            }

            var payloadText = payload.ToString(Formatting.None);

            var (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);

            // OpenAI's "insufficient permissions" 401 shows up INTERMITTENTLY for a while after
            // the account's model access is changed (their ACL propagating — observed live
            // 2026.07.12: the same request shape succeeding and failing seconds apart on
            // gpt-5.6-terra). One short retry rides out the stale server; a truly bad key
            // fails twice and is reported as before.
            if (status == 401 && body.IndexOf("insufficient permissions", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ModLog.Warn("OpenAI answered 401 'insufficient permissions' — retrying once (fresh access changes propagate slowly on their side).");
                await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
                (status, body) = await PostOnceAsync(payloadText, cancellationToken).ConfigureAwait(false);
            }

            if (status < 200 || status >= 300)
            {
                LlmGate.ReportFailure(status, "OpenAI", body);
                throw new InvalidOperationException($"OpenAI request failed ({status}): {Truncate(body, 400)}");
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
            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("Authorization", "Bearer " + _apiKey);
                request.Content = new StringContent(payloadText, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The connection itself failed (or timed out) — quiet the autonomous flows.
                    LlmGate.ReportFailure(0, "OpenAI", ex.Message);
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
