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
            _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model;
            _maxTokens = maxTokens > 0 ? maxTokens : 400;
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

            var payload = new JObject
            {
                ["model"] = _model,
                ["max_tokens"] = _maxTokens,
                ["messages"] = BuildTurns(messages),
            };

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(tools);
                // Definitions always ride along (a history holding tool calls needs them to validate);
                // "none" is how a final, spoken-answer-only round is enforced.
                if (!allowToolUse) payload["tool_choice"] = "none";
            }

            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("Authorization", "Bearer " + _apiKey);
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"OpenAI request failed ({(int)response.StatusCode}): {Truncate(body, 400)}");

                    var json = JObject.Parse(body);
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
