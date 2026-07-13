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

        static OpenAIChatClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public OpenAIChatClient(string apiKey, string model, int maxTokens)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-5.4-mini" : model;
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
        }

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
                throw new InvalidOperationException("OpenAI API key is not set. Add it to " + ModConfig.ConfigFilePath);
            if (!UsageLedger.CanCall(out var capReason))
                throw new InvalidOperationException(capReason);

            // Reasoning is OFF for good (2026.07.13, Anton's call): silent thinking spends billed
            // tokens against the reply's budget and slows every answer — an NPC that "thinks" too
            // long answers with silence. "none" is sent explicitly because omitting the dial lets
            // the API default to reasoning. (It also sidesteps gpt-5.6's refusal to combine
            // function tools with reasoning on chat completions, hit live 2026.07.12.)
            var payload = new JObject
            {
                ["model"] = _model,
                [IsReasoningFamily ? "max_completion_tokens" : "max_tokens"] = _maxTokens,
                ["messages"] = BuildTurns(messages),
            };
            if (IsReasoningFamily)
                payload["reasoning_effort"] = "none";

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
            // 2026.07.12: the same request shape succeeding and failing seconds apart, and
            // one 1.5s retry still losing whole replies HOURS after the grant). Three retries
            // with growing pauses ride out a stale server; a truly bad key fails them all and
            // is reported as before.
            for (int attempt = 1; attempt <= 3 && status == 401
                 && body.IndexOf("insufficient permissions", StringComparison.OrdinalIgnoreCase) >= 0; attempt++)
            {
                ModLog.Warn($"OpenAI answered 401 'insufficient permissions' — retry {attempt} of 3 (fresh access changes propagate slowly on their side).");
                await Task.Delay(1500 * attempt, cancellationToken).ConfigureAwait(false);
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
