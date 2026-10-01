using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Probe
{
    /// <summary>The Anthropic Messages road shaped as src/ImmersiveAI.Module/Llm/AnthropicChatClient.cs
    /// shapes it (2026.10.01): thinking disabled, answer fields folded into a forced speak hand
    /// ("any", the last round names speak). The key is read from config.json and never written out.</summary>
    public sealed class AnthropicProbeClient : IToolChatClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        private readonly string _key, _model;
        private readonly int _maxTokens;
        public readonly List<CallRecord> Calls = new List<CallRecord>();

        public AnthropicProbeClient(string key, string model, int maxTokens)
        {
            _key = key; _model = model; _maxTokens = maxTokens;
        }

        public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default) =>
            (await CompleteWithToolsAsync(messages, Array.Empty<ToolDefinition>(), false, ct)).Text;

        public async Task<ChatResult> CompleteWithToolsAsync(IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools, bool allowToolUse = true, CancellationToken ct = default)
        {
            var payload = new JObject
            {
                ["model"] = _model,
                ["max_tokens"] = _maxTokens,
                ["system"] = string.Join("\n\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content)),
                ["messages"] = BuildTurns(messages),
                ["thinking"] = new JObject { ["type"] = "disabled" },
            };
            bool speaking = AnswerShape.HasFields(tools);
            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(AnswerShape.NativeOffer(tools, messages));
                if (speaking)
                    payload["tool_choice"] = allowToolUse
                        ? new JObject { ["type"] = "any" }
                        : new JObject { ["type"] = "tool", ["name"] = AnswerShape.SpeakTool };
                else if (!allowToolUse) payload["tool_choice"] = new JObject { ["type"] = "none" };
            }
            var rec = new CallRecord { Round = Calls.Count + 1, Model = _model, AllowToolUse = allowToolUse };
            Calls.Add(rec);
            var sw = Stopwatch.StartNew();
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json"),
            };
            req.Headers.TryAddWithoutValidation("x-api-key", _key);
            req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            using var resp = await Http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            rec.TotalMs = Math.Round(sw.Elapsed.TotalMilliseconds);
            if (!resp.IsSuccessStatusCode)
            {
                rec.Error = ((int)resp.StatusCode) + " " + (body.Length > 400 ? body.Substring(0, 400) : body);
                throw new InvalidOperationException(rec.Error);
            }
            var json = JObject.Parse(body);
            rec.TokensIn = (int?)json.SelectToken("usage.input_tokens") ?? 0;
            rec.TokensOut = (int?)json.SelectToken("usage.output_tokens") ?? 0;
            var blocks = json["content"] as JArray ?? new JArray();
            var text = string.Concat(blocks.Where(b => (string?)b["type"] == "text").Select(b => (string?)b["text"] ?? ""));
            var calls = blocks.Where(b => (string?)b["type"] == "tool_use")
                .Select(b => new ToolCall((string?)b["id"] ?? "", (string?)b["name"] ?? "", b["input"]?.ToString(Formatting.None) ?? "{}"))
                .ToList();
            var parsed = AnswerShape.FromNative(text.Trim(), calls, tools);
            rec.RawResult = text + " " + string.Join(" ", calls.Select(c => c.Name + c.ArgumentsJson));
            rec.Reply = parsed.Text;
            rec.ToolCalls = parsed.ToolCalls.Select(c => c.Name + c.ArgumentsJson)
                .Concat(parsed.AnswerCalls.Select(c => "[answer]" + c.Name + c.ArgumentsJson)).ToList();
            return parsed;
        }

        private static JArray BuildTurns(IReadOnlyList<ChatMessage> messages)
        {
            var turns = new JArray();
            JArray? pending = null;
            foreach (var m in messages)
            {
                if (m.Role == ChatRole.System) continue;
                if (m.Role == ChatRole.Tool)
                {
                    if (pending == null) { pending = new JArray(); turns.Add(new JObject { ["role"] = "user", ["content"] = pending }); }
                    pending.Add(new JObject { ["type"] = "tool_result", ["tool_use_id"] = m.ToolCallId ?? "", ["content"] = m.Content });
                    continue;
                }
                pending = null;
                if (m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0)
                {
                    var content = new JArray();
                    if (!string.IsNullOrWhiteSpace(m.Content)) content.Add(new JObject { ["type"] = "text", ["text"] = m.Content });
                    foreach (var c in m.ToolCalls)
                        content.Add(new JObject { ["type"] = "tool_use", ["id"] = c.Id, ["name"] = c.Name, ["input"] = JObject.Parse(c.ArgumentsJson) });
                    turns.Add(new JObject { ["role"] = "assistant", ["content"] = content });
                    continue;
                }
                turns.Add(new JObject { ["role"] = m.Role == ChatRole.User ? "user" : "assistant", ["content"] = m.Content });
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
                    ["name"] = tool.Name, ["description"] = tool.Description,
                    ["input_schema"] = new JObject { ["type"] = "object", ["properties"] = props, ["required"] = req },
                });
            }
            return arr;
        }
    }
}
