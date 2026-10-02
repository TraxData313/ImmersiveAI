using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Probe
{
    /// <summary>The control: the plain OpenAI chat-completions road, request shaped as the mod's
    /// OpenAIChatClient shapes it (real roles, native function tools, reasoning_effort "none",
    /// max_completion_tokens). The key is read from config.json at run time and never written out.</summary>
    public sealed class OpenAIProbeClient : IToolChatClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly string _key, _model, _endpoint;
        private readonly int _maxTokens;
        public readonly List<CallRecord> Calls = new List<CallRecord>();

        public OpenAIProbeClient(string key, string model, string endpoint, int maxTokens)
        {
            _key = key; _model = model; _endpoint = endpoint; _maxTokens = maxTokens;
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default) =>
            (await CompleteWithToolsAsync(messages, Array.Empty<ToolDefinition>(), false, ct)).Text;

        private bool _noneRefused;

        private async Task<(HttpResponseMessage, string)> PostAsync(JObject payload, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json"),
            };
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _key);
            var resp = await Http.SendAsync(req, ct);
            return (resp, await resp.Content.ReadAsStringAsync(ct));
        }

        public async Task<ChatResult> CompleteWithToolsAsync(IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools, bool allowToolUse = true, CancellationToken ct = default)
        {
            var payload = new JObject
            {
                ["model"] = _model,
                ["messages"] = BuildTurns(messages),
                ["max_completion_tokens"] = _maxTokens,
                ["reasoning_effort"] = _noneRefused ? "low" : "none",
            };
            // Mirrors OpenAIChatClient (2026.10.01): answer fields fold into one forced speak hand.
            bool speaking = AnswerShape.HasFields(tools);
            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(AnswerShape.NativeOffer(tools, messages));
                if (speaking)
                    payload["tool_choice"] = allowToolUse
                        ? (JToken)"required"
                        : new JObject { ["type"] = "function", ["function"] = new JObject { ["name"] = AnswerShape.SpeakTool } };
                else if (!allowToolUse) payload["tool_choice"] = "none";
            }
            var rec = new CallRecord { Round = Calls.Count + 1, Model = _model, AllowToolUse = allowToolUse };
            Calls.Add(rec);
            var sw = Stopwatch.StartNew();
            var (resp, body) = await PostAsync(payload, ct);
            // Mirrors OpenAIChatClient (2026.10.02): a model refusing "none" is asked for "low", its floor.
            if ((int)resp.StatusCode == 400 && !_noneRefused && body.Contains("reasoning_effort"))
            {
                _noneRefused = true;
                payload["reasoning_effort"] = "low";
                (resp, body) = await PostAsync(payload, ct);
            }
            rec.TotalMs = Math.Round(sw.Elapsed.TotalMilliseconds);
            if (!resp.IsSuccessStatusCode)
            {
                rec.Error = ((int)resp.StatusCode) + " " + (body.Length > 400 ? body.Substring(0, 400) : body);
                throw new InvalidOperationException(rec.Error);
            }
            var json = JObject.Parse(body);
            rec.Usage = json["usage"] as JObject;
            rec.TokensIn = (int?)json.SelectToken("usage.prompt_tokens") ?? 0;
            rec.TokensCached = (int?)json.SelectToken("usage.prompt_tokens_details.cached_tokens") ?? 0;
            rec.TokensOut = (int?)json.SelectToken("usage.completion_tokens") ?? 0;
            rec.TokensReasoning = (int?)json.SelectToken("usage.completion_tokens_details.reasoning_tokens") ?? 0;
            var msg = json.SelectToken("choices[0].message") as JObject;
            var text = (string?)msg?["content"] ?? "";
            var calls = (msg?["tool_calls"] as JArray ?? new JArray())
                .Where(c => (string?)c["type"] == "function")
                .Select(c => new ToolCall((string?)c["id"] ?? "", (string?)c.SelectToken("function.name") ?? "",
                    (string?)c.SelectToken("function.arguments") ?? "{}"))
                .ToList();
            var parsed = AnswerShape.FromNative(text, calls, tools);
            rec.RawResult = text + (calls.Count > 0 ? " " + string.Join(" ", calls.Select(c => c.Name + c.ArgumentsJson)) : "");
            rec.Reply = parsed.Text;
            rec.ToolCalls = parsed.ToolCalls.Select(c => c.Name + c.ArgumentsJson)
                .Concat(parsed.AnswerCalls.Select(c => "[answer]" + c.Name + c.ArgumentsJson)).ToList();
            return parsed;
        }

        private static JArray BuildTurns(IReadOnlyList<ChatMessage> messages)
        {
            var turns = new JArray();
            foreach (var m in messages)
            {
                if (m.Role == ChatRole.Tool)
                {
                    turns.Add(new JObject { ["role"] = "tool", ["tool_call_id"] = m.ToolCallId ?? "", ["content"] = m.Content });
                    continue;
                }
                if (m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0)
                {
                    var tc = new JArray(m.ToolCalls.Select(c => new JObject
                    {
                        ["id"] = c.Id, ["type"] = "function",
                        ["function"] = new JObject { ["name"] = c.Name, ["arguments"] = c.ArgumentsJson },
                    }));
                    turns.Add(new JObject
                    {
                        ["role"] = "assistant", ["tool_calls"] = tc,
                        ["content"] = string.IsNullOrWhiteSpace(m.Content) ? JValue.CreateNull() : (JToken)m.Content,
                    });
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
                var props = new JObject();
                var req = new JArray();
                foreach (var p in tool.Parameters)
                {
                    var s = new JObject { ["type"] = p.JsonType, ["description"] = p.Description };
                    if (p.AllowedValues != null) s["enum"] = new JArray(p.AllowedValues.Cast<object>().ToArray());
                    props[p.Name] = s;
                    if (p.Required) req.Add(p.Name);
                }
                arr.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tool.Name, ["description"] = tool.Description,
                        ["parameters"] = new JObject { ["type"] = "object", ["properties"] = props, ["required"] = req },
                    },
                });
            }
            return arr;
        }
    }
}
