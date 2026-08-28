using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// The pure shape of one exchange with the Claude Code CLI — the road that speaks with the
    /// player's own Claude subscription instead of an API key. The CLI is a one-shot process, not
    /// a chat API: it takes ONE system prompt and ONE user prompt, so the multi-turn message list
    /// every other backend replays as real turns is FLATTENED here into a first-person script
    /// (probed live 2026.08.28 — the model stays in character and answers the last line; the same
    /// move living-abby's room makes). Native tool calling is carried by the CLI's structured
    /// output instead: a JSON schema with "reply" + "tool_calls", each tool a typed entry so
    /// AllowedValues stay schema-enforced enums, never prose (the 2026.08.09 law).
    /// Everything here is string work on purpose — the process mechanics live in the Module.
    /// </summary>
    public static class ClaudeCliShape
    {
        /// <summary>The system prompt: the sheet as every backend sends it, plus — when tools ride
        /// along — how the answer must be shaped. The reply contract lives HERE rather than in the
        /// transcript so it reads as how I speak, not as something said to me.</summary>
        public static string BuildSystem(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            bool allowToolUse)
        {
            var system = string.Join("\n\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));
            if (tools == null || tools.Count == 0) return system;

            var sb = new StringBuilder(system);
            if (sb.Length > 0) sb.Append("\n\n");
            if (allowToolUse)
            {
                // The hands live in the SHEET, not only in the schema — probed 2026.08.28: with
                // the descriptions carried by the schema alone the model answered around its hands
                // instead of reaching; named in the sheet it reached on the first try. The schema
                // keeps enforcing the shapes and vocabularies; this is where they become known.
                sb.Append("These hands are mine to reach with, named in \"tool_calls\":\n");
                foreach (var tool in tools)
                {
                    sb.Append("- ").Append(tool.Name).Append('(');
                    sb.Append(string.Join(", ", tool.Parameters.Select(ParameterSketch)));
                    sb.Append(')');
                    if (!string.IsNullOrWhiteSpace(tool.Description))
                        sb.Append(" — ").Append(tool.Description);
                    sb.Append('\n');
                }
                sb.Append("\nHow I answer: my spoken words go in \"reply\". When I need a hand first, "
                    + "I name it in \"tool_calls\" and leave \"reply\" empty — the world answers, and "
                    + "then I speak. I may reach for more than one at once. When I have nothing to "
                    + "reach for, \"tool_calls\" stays empty.");
            }
            else
            {
                sb.Append("How I answer now: in my own spoken words, in \"reply\", reaching for nothing more.");
            }
            return sb.ToString();
        }

        private static string ParameterSketch(ToolParameter p)
        {
            var name = p.Required ? p.Name : p.Name + "?";
            return p.AllowedValues != null ? name + ": " + string.Join("|", p.AllowedValues) : name;
        }

        /// <summary>
        /// The non-system messages as one prompt. A single user message passes through VERBATIM —
        /// memory writing, feeling calls and every other plain ask keep their exact wording. A real
        /// history is rendered as a bracketed first-person script: what was said to me, what I
        /// answered, what I reached for and what the world handed back — ending on whose turn it is.
        /// </summary>
        public static string BuildTranscript(IReadOnlyList<ChatMessage> messages)
        {
            var turns = messages.Where(m => m.Role != ChatRole.System).ToList();
            if (turns.Count == 1 && turns[0].Role == ChatRole.User)
                return turns[0].Content;

            // Tool results carry only the call id; the name lives on the assistant turn that
            // reached. Walk once so every answer can say WHICH hand the world was answering.
            var nameOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var call in turns.Where(t => t.Role == ChatRole.Assistant).SelectMany(t => t.ToolCalls))
                if (!string.IsNullOrEmpty(call.Id) && !nameOf.ContainsKey(call.Id))
                    nameOf[call.Id] = call.Name;

            var sb = new StringBuilder();
            foreach (var m in turns)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                switch (m.Role)
                {
                    case ChatRole.User:
                        sb.Append("[Said to me:]\n").Append(m.Content);
                        break;
                    case ChatRole.Assistant when m.ToolCalls.Count > 0:
                        if (!string.IsNullOrWhiteSpace(m.Content))
                            sb.Append("[I answered:]\n").Append(m.Content).Append("\n\n");
                        sb.Append("[I reached for: ")
                          .Append(string.Join(", ", m.ToolCalls.Select(c => c.Name + "(" + c.ArgumentsJson + ")")))
                          .Append("]");
                        break;
                    case ChatRole.Assistant:
                        sb.Append("[I answered:]\n").Append(m.Content);
                        break;
                    case ChatRole.Tool:
                        var hand = nameOf.TryGetValue(m.ToolCallId ?? "", out var n) ? n : "my reach";
                        sb.Append("[The world answered ").Append(hand).Append(":]\n").Append(m.Content);
                        break;
                }
            }
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("[Now I answer.]");
            return sb.ToString();
        }

        /// <summary>
        /// The structured-output schema: "reply" plus a typed "tool_calls" entry per tool, so a
        /// closed vocabulary rides as a real schema enum. With <paramref name="allowToolUse"/>
        /// false the array is pinned empty — the final round must end in words, the same law
        /// tool_choice "none" enforces on the API backends.
        /// </summary>
        public static string BuildSchema(IReadOnlyList<ToolDefinition>? tools, bool allowToolUse)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["reply"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "My spoken words. Empty only while a reach in tool_calls is still unanswered.",
                    },
                    ["tool_calls"] = ToolCallsSchema(tools, allowToolUse),
                },
                ["required"] = new JArray("reply", "tool_calls"),
                ["additionalProperties"] = false,
            };
            return schema.ToString(Formatting.None);
        }

        private static JObject ToolCallsSchema(IReadOnlyList<ToolDefinition>? tools, bool allowToolUse)
        {
            if (tools == null || tools.Count == 0 || !allowToolUse)
                return new JObject { ["type"] = "array", ["maxItems"] = 0 };

            var shapes = new JArray();
            foreach (var tool in tools)
            {
                var props = new JObject();
                var required = new JArray();
                foreach (var p in tool.Parameters)
                {
                    var prop = new JObject { ["type"] = "string" };
                    if (!string.IsNullOrWhiteSpace(p.Description)) prop["description"] = p.Description;
                    if (p.AllowedValues != null) prop["enum"] = new JArray(p.AllowedValues.ToArray());
                    props[p.Name] = prop;
                    if (p.Required) required.Add(p.Name);
                }
                var shape = new JObject
                {
                    ["type"] = "object",
                    ["description"] = tool.Description,
                    ["properties"] = new JObject
                    {
                        // A one-value enum, not "const": same meaning, older validators all know it.
                        ["name"] = new JObject { ["enum"] = new JArray(tool.Name) },
                        ["arguments"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = props,
                            ["required"] = required,
                            ["additionalProperties"] = false,
                        },
                    },
                    ["required"] = new JArray("name", "arguments"),
                    ["additionalProperties"] = false,
                };
                shapes.Add(shape);
            }
            return new JObject { ["type"] = "array", ["items"] = new JObject { ["anyOf"] = shapes } };
        }

        /// <summary>
        /// The schema'd answer back into the mod's own shape. Ids are synthesized ("cli_1"…) —
        /// the CLI issues none, and the loop only needs them to match results to reaches.
        /// The envelope does not always arrive naked (live find, 2026.08.28, fable through the
        /// CLI): a thinking model's structured output can come wrapped in the CLI's own
        /// &lt;StructuredOutput&gt; tags — which strict parsing read as prose, so the raw JSON
        /// walked into the thread and into memory wearing its braces. So: strip known wrappers,
        /// then parse; failing that, parse the first object the text carries; only then is a
        /// result treated as spoken words, whole — an answer that arrived malformed is still an
        /// answer.
        /// </summary>
        public static ChatResult ParseToolResult(string resultText)
        {
            var cleaned = StripWrappers(resultText ?? string.Empty);

            var parsed = TryParseEnvelope(cleaned);
            if (parsed == null)
            {
                // Trailing chatter after the object (or a partial wrapper) — take the outermost
                // braces the text carries and try once more.
                int open = cleaned.IndexOf('{'), close = cleaned.LastIndexOf('}');
                if (open >= 0 && close > open)
                    parsed = TryParseEnvelope(cleaned.Substring(open, close - open + 1));
            }
            return parsed ?? new ChatResult(cleaned.Trim(), null);
        }

        private static ChatResult? TryParseEnvelope(string text)
        {
            try
            {
                var obj = JObject.Parse(text);
                if (obj["reply"] == null) return null;   // some other JSON is not our envelope
                var reply = (string?)obj["reply"] ?? string.Empty;
                var calls = new List<ToolCall>();
                if (obj["tool_calls"] is JArray arr)
                {
                    int i = 0;
                    foreach (var item in arr.OfType<JObject>())
                    {
                        var name = (string?)item["name"];
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        i++;
                        calls.Add(new ToolCall("cli_" + i, name!,
                            item["arguments"]?.ToString(Formatting.None) ?? "{}"));
                    }
                }
                return new ChatResult(reply.Trim(), calls);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>The dressings a structured answer has been seen to arrive in: the CLI's own
        /// &lt;StructuredOutput&gt; tags, and markdown code fences. Removed only when they wrap the
        /// WHOLE text — braces inside honest speech are left alone.</summary>
        private static string StripWrappers(string text)
        {
            var s = text.Trim();

            const string openTag = "<StructuredOutput>";
            const string closeTag = "</StructuredOutput>";
            if (s.StartsWith(openTag, StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(openTag.Length);
                var end = s.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
                if (end >= 0) s = s.Substring(0, end);
                s = s.Trim();
            }

            if (s.StartsWith("```", StringComparison.Ordinal))
            {
                var firstBreak = s.IndexOf('\n');
                var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
                if (firstBreak >= 0 && lastFence > firstBreak)
                    s = s.Substring(firstBreak + 1, lastFence - firstBreak - 1).Trim();
            }
            return s;
        }

        /// <summary>
        /// Whether a RECORDED spoken line is, in truth, one of these envelopes — and if so, the
        /// words it was carrying. For healing memories that took the raw shape in before the
        /// parser learned to undress it: tool calls inside are dead history (the live loop
        /// resolved or lost them at the time) and are deliberately dropped, only the speech
        /// comes back. False for anything that merely mentions braces mid-sentence.
        /// </summary>
        public static bool TryUnwrapSpokenEnvelope(string line, out string reply)
        {
            reply = string.Empty;
            if (string.IsNullOrWhiteSpace(line)) return false;
            var cleaned = StripWrappers(line);
            if (!cleaned.StartsWith("{", StringComparison.Ordinal)) return false;
            var parsed = TryParseEnvelope(cleaned);
            if (parsed == null || parsed.Text.Length == 0) return false;
            reply = parsed.Text;
            return true;
        }

        /// <summary>One window of the plan gauge, as the CLI's stream names it.</summary>
        public sealed class RateWindow
        {
            public string Kind = "";        // "five_hour", "seven_day", …
            public string Status = "";      // "allowed", "allowed_warning", "rejected"
            public long ResetsAtUnix;
            public bool UsingOverage;
        }

        /// <summary>Everything one CLI run hands back that the mod cares about.</summary>
        public sealed class Envelope
        {
            public bool Ok;
            public string ResultText = "";
            public string ErrorText = "";
            public double? CostUsd;
            public int TokensIn;
            public int TokensOut;
            public string Model = "";
            public List<RateWindow> Limits = new List<RateWindow>();
        }

        /// <summary>
        /// The stream-json output, one event per line, folded to the one answer. The result event
        /// carries the words, the measured tokens and the CLI's own cost figure; rate_limit events
        /// ride beside it and are kept for the plan gauge. Unparseable lines are skipped — the
        /// stream carries progress chatter we never asked for.
        /// </summary>
        public static Envelope ParseStream(string stdout)
        {
            var env = new Envelope();
            JObject? result = null;
            foreach (var raw in (stdout ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] != '{') continue;
                JObject ev;
                try { ev = JObject.Parse(line); }
                catch (JsonException) { continue; }

                if ((string?)ev["type"] == "result") result = ev;

                if (ev["rate_limit_info"] is JObject info && !string.IsNullOrEmpty((string?)info["rateLimitType"]))
                {
                    var window = new RateWindow
                    {
                        Kind = (string?)info["rateLimitType"] ?? "",
                        Status = (string?)info["status"] ?? "",
                        ResetsAtUnix = (long?)info["resetsAt"] ?? 0,
                        UsingOverage = (bool?)info["isUsingOverage"] ?? false,
                    };
                    env.Limits.RemoveAll(w => w.Kind == window.Kind);
                    env.Limits.Add(window);
                }
            }

            if (result == null)
            {
                env.ErrorText = "no result arrived from Claude Code";
                return env;
            }

            env.Ok = (bool?)result["is_error"] != true;
            env.ResultText = (string?)result["result"] ?? "";
            if (!env.Ok && string.IsNullOrWhiteSpace(env.ResultText))
                env.ErrorText = (string?)result["subtype"] ?? "Claude Code reported an error";
            else if (!env.Ok)
                env.ErrorText = env.ResultText;
            env.CostUsd = (double?)result["total_cost_usd"];

            var usage = result["usage"] as JObject;
            if (usage != null)
            {
                env.TokensIn = ((int?)usage["input_tokens"] ?? 0)
                    + ((int?)usage["cache_read_input_tokens"] ?? 0)
                    + ((int?)usage["cache_creation_input_tokens"] ?? 0);
                env.TokensOut = (int?)usage["output_tokens"] ?? 0;
            }

            // Several models can appear in one run; the one that wrote the answer is the one
            // that produced the output tokens (living-abby's lesson, kept).
            if (result["modelUsage"] is JObject mu)
            {
                var best = mu.Properties()
                    .OrderByDescending(p => (int?)p.Value?["outputTokens"] ?? 0)
                    .FirstOrDefault();
                if (best != null)
                    env.Model = (string?)best.Value?["canonicalModel"] ?? best.Name;
            }
            return env;
        }

        /// <summary>
        /// One argument, quoted the way CreateProcess actually unquotes (the msvcrt rules:
        /// backslashes double only before a quote, quotes become backslash-quote). The schema is
        /// the one JSON that must ride the command line whole — a quoting slip there is a 400 in
        /// a wig, so this is Core and tested rather than inline and hoped.
        /// </summary>
        public static string EscapeWindowsArg(string arg)
        {
            if (!string.IsNullOrEmpty(arg) && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
                return arg;

            var sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (var c in arg ?? "")
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1).Append('"');
                    backslashes = 0;
                    continue;
                }
                sb.Append('\\', backslashes).Append(c);
                backslashes = 0;
            }
            sb.Append('\\', backslashes * 2).Append('"');
            return sb.ToString();
        }
    }
}
