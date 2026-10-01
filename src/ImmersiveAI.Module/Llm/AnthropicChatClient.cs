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
    /// <summary>
    /// Anthropic Messages API client (raw HTTP — the official SDK requires modern .NET,
    /// and Bannerlord runs mods on .NET Framework 4.7.2). Supports native tool use, which
    /// carries the NPCs' "recall the world" ability (see WorldRecall / ToolLoopRunner).
    /// </summary>
    public sealed class AnthropicChatClient : IToolChatClient
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";

        private static readonly HttpClient Http = CreateHttpClient();

        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;

        // Set the first time a forced tool choice is refused (a 400 naming tool_choice — Anthropic
        // will not force a tool while the model thinks). From then on the speak hand is only offered.
        private bool _forcedChoiceRefused;

        // Fable/Mythos always think, and thinking cannot be combined with a forced tool choice.
        private bool ThinksAlways =>
            _model.IndexOf("fable", StringComparison.OrdinalIgnoreCase) >= 0
            || _model.IndexOf("mythos", StringComparison.OrdinalIgnoreCase) >= 0;

        static AnthropicChatClient()
        {
            // .NET Framework needs an explicit opt-in to TLS 1.2 on some systems
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            return client;
        }

        public AnthropicChatClient(string apiKey, string model, int maxTokens)
        {
            _apiKey = apiKey ?? "";
            _model = string.IsNullOrWhiteSpace(model) ? "claude-haiku-4-5" : model;
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var result = await SendAsync(messages, null, allowToolUse: false, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.Text))
                throw new InvalidOperationException("Anthropic returned an empty response.");
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
                throw new InvalidOperationException("Anthropic API key is not set. Add it to " + ModConfig.ConfigFilePath);
            if (!UsageLedger.CanCall(out var capReason))
                throw new InvalidOperationException(capReason);

            var system = string.Join("\n\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));

            var payload = new JObject
            {
                ["model"] = _model,
                ["max_tokens"] = _maxTokens,
                ["messages"] = BuildTurns(messages),
            };
            if (system.Length > 0) payload["system"] = system;

            // Thinking is explicitly OFF (2026.07.13, Anton's call): silent reasoning spends the
            // spoken reply's small token budget and slows the answer — the NPC "thinks" and the
            // player gets "...". Explicit, not omitted, because sonnet-5 runs ADAPTIVE thinking by
            // default when the field is absent. Fable/Mythos are the one exception: thinking is
            // always on there and an explicit "disabled" is a hard 400, so they keep the omission.
            if (_model.IndexOf("fable", StringComparison.OrdinalIgnoreCase) < 0
                && _model.IndexOf("mythos", StringComparison.OrdinalIgnoreCase) < 0)
                payload["thinking"] = new JObject { ["type"] = "disabled" };

            // THE ANSWER FIELDS (2026.10.01 — the heart beside every reply): the reply is given
            // through one hand, speak(words, heart), and the choice is FORCED ("any" — a reach or the
            // answer; the last round names speak itself), so the measure comes with the words in one
            // call. See AnswerShape.
            bool speaking = AnswerShape.HasFields(tools);
            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(AnswerShape.NativeOffer(tools, messages));
                // The definitions must always ride along (a history holding tool_use blocks is rejected
                // without them); "none" is how a final, spoken-answer-only round is enforced.
                if (speaking && !_forcedChoiceRefused && !ThinksAlways)
                    payload["tool_choice"] = allowToolUse
                        ? new JObject { ["type"] = "any" }
                        : new JObject { ["type"] = "tool", ["name"] = AnswerShape.SpeakTool };
                else if (!allowToolUse && !speaking) payload["tool_choice"] = new JObject { ["type"] = "none" };
            }

            var (status, body) = await PostOnceAsync(payload, cancellationToken).ConfigureAwait(false);
            if (status == 400 && speaking && payload["tool_choice"] != null
                && body.IndexOf("tool_choice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                payload.Remove("tool_choice");
                _forcedChoiceRefused = true;
                ModLog.Warn($"Anthropic: '{_model}' refused a forced tool choice — the reply is offered the speak hand without forcing it.");
                (status, body) = await PostOnceAsync(payload, cancellationToken).ConfigureAwait(false);
            }

            if (status < 200 || status >= 300)
            {
                LlmGate.ReportFailure(status, "Anthropic", body);
                throw new InvalidOperationException($"Anthropic request failed ({status}): {Truncate(body, 400)}");
            }

            var json = JObject.Parse(body);

            // The API measures its own tokens — hand them to the ledger, and tell the
            // gate the road is open again.
            UsageLedger.RecordCall(_model,
                (int?)json.SelectToken("usage.input_tokens") ?? 0,
                (int?)json.SelectToken("usage.output_tokens") ?? 0);
            LlmGate.ReportSuccess();

            var stopReason = (string?)json["stop_reason"];
            if (stopReason == "refusal")
                throw new InvalidOperationException("The model declined to answer this request.");

            var blocks = json["content"] as JArray ?? new JArray();

            var text = string.Concat(blocks
                .Where(b => (string?)b["type"] == "text")
                .Select(b => (string?)b["text"] ?? ""));

            var calls = blocks
                .Where(b => (string?)b["type"] == "tool_use")
                .Select(b => new ToolCall(
                    (string?)b["id"] ?? "",
                    (string?)b["name"] ?? "",
                    b["input"]?.ToString(Formatting.None) ?? "{}"))
                .ToList();

            return AnswerShape.FromNative(text.Trim(), calls, tools);
        }

        /// <summary>One POST: status + body, never throwing on an API error status (the caller
        /// decides about the one retry). A failed CONNECTION still throws, after telling the gate.</summary>
        private async Task<(int Status, string Body)> PostOnceAsync(JObject payload, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The connection itself failed (or timed out) — quiet the autonomous flows.
                    LlmGate.ReportFailure(0, "Anthropic", ex.Message);
                    throw;
                }
                using (response)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ((int)response.StatusCode, body);
                }
            }
        }

        // The message list, in Anthropic's shape: plain strings for ordinary turns; content-block
        // arrays for assistant turns that reached for tools; and tool results as user-side
        // tool_result blocks — consecutive results merged into ONE user message, both because they
        // answer one assistant turn and because roles must alternate.
        private static JArray BuildTurns(IReadOnlyList<ChatMessage> messages)
        {
            var turns = new JArray();
            JArray? pendingToolResults = null;

            foreach (var m in messages)
            {
                if (m.Role == ChatRole.System) continue;

                if (m.Role == ChatRole.Tool)
                {
                    if (pendingToolResults == null)
                    {
                        pendingToolResults = new JArray();
                        turns.Add(new JObject { ["role"] = "user", ["content"] = pendingToolResults });
                    }
                    pendingToolResults.Add(new JObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = m.ToolCallId ?? "",
                        ["content"] = m.Content,
                    });
                    continue;
                }

                pendingToolResults = null;

                if (m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0)
                {
                    var content = new JArray();
                    if (!string.IsNullOrWhiteSpace(m.Content))
                        content.Add(new JObject { ["type"] = "text", ["text"] = m.Content });
                    foreach (var call in m.ToolCalls)
                    {
                        content.Add(new JObject
                        {
                            ["type"] = "tool_use",
                            ["id"] = call.Id,
                            ["name"] = call.Name,
                            ["input"] = SafeParseObject(call.ArgumentsJson),
                        });
                    }
                    turns.Add(new JObject { ["role"] = "assistant", ["content"] = content });
                    continue;
                }

                turns.Add(new JObject
                {
                    ["role"] = m.Role == ChatRole.User ? "user" : "assistant",
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
                    var schema = new JObject { ["type"] = p.JsonType, ["description"] = p.Description };
                    // A closed vocabulary belongs in the schema, not only in the prose (see
                    // ToolParameter.AllowedValues — the silent-synonym bug of 2026.08.09).
                    if (p.AllowedValues != null)
                        schema["enum"] = new JArray(p.AllowedValues.Cast<object>().ToArray());
                    properties[p.Name] = schema;
                    if (p.Required) required.Add(p.Name);
                }

                arr.Add(new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = required,
                    },
                });
            }
            return arr;
        }

        private static JObject SafeParseObject(string json)
        {
            try { return JObject.Parse(json); }
            catch { return new JObject(); }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
