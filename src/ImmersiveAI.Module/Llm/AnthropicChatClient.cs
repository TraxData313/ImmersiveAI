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
            _model = string.IsNullOrWhiteSpace(model) ? "claude-opus-4-8" : model;
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

            var system = string.Join("\n\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));

            var payload = new JObject
            {
                ["model"] = _model,
                ["max_tokens"] = _maxTokens,
                ["messages"] = BuildTurns(messages),
            };
            if (system.Length > 0) payload["system"] = system;

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(tools);
                // The definitions must always ride along (a history holding tool_use blocks is rejected
                // without them); "none" is how a final, spoken-answer-only round is enforced.
                if (!allowToolUse) payload["tool_choice"] = new JObject { ["type"] = "none" };
            }

            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Anthropic request failed ({(int)response.StatusCode}): {Truncate(body, 400)}");

                    var json = JObject.Parse(body);
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

                    return new ChatResult(text.Trim(), calls);
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
                    properties[p.Name] = new JObject { ["type"] = "string", ["description"] = p.Description };
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
