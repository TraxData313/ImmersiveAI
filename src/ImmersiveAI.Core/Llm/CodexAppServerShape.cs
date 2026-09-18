using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// The pure wire shape for the Codex subscription road. Codex app-server still receives one
    /// fresh user turn per mod call, so it shares Claude Code's proven system/transcript/tool
    /// envelope; this class adds OpenAI-strict schema sealing and folds app-server's streamed
    /// notifications into one answer. Process and credential mechanics stay in the Module.
    /// </summary>
    public static class CodexAppServerShape
    {
        public static string BuildSystem(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            bool allowToolUse,
            int maxTokens = 0) =>
            ClaudeCliShape.BuildSystem(messages, tools, allowToolUse, maxTokens);

        public static string BuildTranscript(IReadOnlyList<ChatMessage> messages) =>
            ClaudeCliShape.BuildTranscript(messages);

        public static ChatResult ParseToolResult(string resultText) =>
            ClaudeCliShape.ParseToolResult(resultText);

        /// <summary>Codex's outputSchema uses OpenAI strict structured output: every object is
        /// sealed and every property is required. A genuinely optional tool argument therefore
        /// becomes required-but-nullable; the tool resolver already reads null as absent.</summary>
        public static string BuildStrictSchema(IReadOnlyList<ToolDefinition>? tools, bool allowToolUse)
        {
            var schema = JObject.Parse(ClaudeCliShape.BuildSchema(tools, allowToolUse));
            // OpenAI validates array schemas structurally even when maxItems is zero. Claude's
            // schema needs no item shape in that case; Codex still requires one.
            if (schema.SelectToken("properties.tool_calls") is JObject calls && calls["items"] == null)
            {
                calls["items"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["required"] = new JArray(),
                    ["additionalProperties"] = false,
                };
            }
            SealObject(schema);
            return schema.ToString(Formatting.None);
        }

        private static void SealObject(JToken token)
        {
            if (token is JObject obj)
            {
                if (obj["properties"] is JObject properties)
                {
                    var wasRequired = new HashSet<string>(StringComparer.Ordinal);
                    if (obj["required"] is JArray required)
                        foreach (var name in required.Values<string>())
                            if (name != null) wasRequired.Add(name);

                    var all = new JArray();
                    foreach (var property in properties.Properties())
                    {
                        SealObject(property.Value);
                        all.Add(property.Name);
                        if (!wasRequired.Contains(property.Name) && property.Value is JObject optional)
                            MakeNullable(optional);
                    }
                    obj["required"] = all;
                    obj["additionalProperties"] = false;
                }

                foreach (var property in obj.Properties())
                    if (property.Name != "properties") SealObject(property.Value);
            }
            else if (token is JArray array)
            {
                foreach (var child in array) SealObject(child);
            }
        }

        private static void MakeNullable(JObject schema)
        {
            var type = schema["type"];
            if (type == null) return;
            if (type.Type == JTokenType.String)
            {
                if (!string.Equals((string?)type, "null", StringComparison.Ordinal))
                    schema["type"] = new JArray(type.Value<string>() ?? "string", "null");
            }
            else if (type is JArray types && !types.Values<string>().Contains("null"))
            {
                types.Add("null");
            }

            // Type-nullable is not enough when the string also has a closed vocabulary: JSON
            // Schema applies both constraints, so null must belong to the enum as well.
            if (schema["enum"] is JArray values && !values.Any(v => v.Type == JTokenType.Null))
                values.Add(JValue.CreateNull());
        }

        /// <summary>One app-server turn as its JSONL notifications arrive.</summary>
        public sealed class TurnEnvelope
        {
            public bool Finished;
            public bool Ok;
            public string ResultText = string.Empty;
            public string ErrorText = string.Empty;
            public int TokensIn;
            public int TokensOut;
            public int ThinkingTokens;
            public int ContextWindow;
        }

        /// <summary>Fold one app-server notification. Returns false only when it belongs to another
        /// thread. Completed final-answer items replace deltas, so any commentary that arrived
        /// earlier can never leak into the structured result.</summary>
        public static bool FoldTurnEvent(TurnEnvelope envelope, JObject message, string threadId)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            if (message == null) throw new ArgumentNullException(nameof(message));

            var parameters = message["params"] as JObject;
            var eventThread = (string?)parameters?["threadId"];
            if (!string.IsNullOrEmpty(eventThread)
                && !string.Equals(eventThread, threadId, StringComparison.Ordinal))
                return false;

            var method = (string?)message["method"] ?? string.Empty;
            switch (method)
            {
                case "item/agentMessage/delta":
                    envelope.ResultText += (string?)parameters?["delta"] ?? string.Empty;
                    break;

                case "item/completed":
                    if (parameters?["item"] is JObject item
                        && string.Equals((string?)item["type"], "agentMessage", StringComparison.Ordinal)
                        && !string.Equals((string?)item["phase"], "commentary", StringComparison.OrdinalIgnoreCase))
                        envelope.ResultText = (string?)item["text"] ?? envelope.ResultText;
                    break;

                case "thread/tokenUsage/updated":
                    var usage = parameters?["tokenUsage"] as JObject;
                    var total = usage?["total"] as JObject;
                    envelope.TokensIn = ToInt(total?["inputTokens"]);
                    envelope.TokensOut = ToInt(total?["outputTokens"]);
                    envelope.ThinkingTokens = ToInt(total?["reasoningOutputTokens"]);
                    envelope.ContextWindow = ToInt(usage?["modelContextWindow"]);
                    break;

                case "error":
                    envelope.ErrorText = (string?)parameters?.SelectToken("error.message")
                        ?? (string?)parameters?["message"] ?? envelope.ErrorText;
                    break;

                case "turn/completed":
                    var turn = parameters?["turn"] as JObject;
                    var status = (string?)turn?["status"] ?? string.Empty;
                    envelope.Finished = true;
                    envelope.Ok = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
                    if (!envelope.Ok)
                        envelope.ErrorText = (string?)turn?.SelectToken("error.message")
                            ?? (string?)turn?["error"] ?? (status.Length == 0 ? "Codex did not finish." : status);
                    break;
            }
            return true;
        }

        /// <summary>"5h at 58%, weekly at 9%" from account/rateLimits/read, or null when the
        /// installed server names no usable Codex subscription window.</summary>
        public static string? ComposeRateLimitLabel(JObject result)
        {
            if (result == null) return null;
            var snapshot = result.SelectToken("rateLimitsByLimitId.codex") as JObject
                ?? result["rateLimits"] as JObject;
            if (snapshot == null) return null;

            string? primary = WindowLabel(snapshot["primary"] as JObject);
            string? secondary = WindowLabel(snapshot["secondary"] as JObject);
            if (primary == null) return secondary;
            return secondary == null ? primary : primary + ", " + secondary;
        }

        private static string? WindowLabel(JObject? window)
        {
            if (window?["usedPercent"] == null) return null;
            var percent = Math.Round((double)window["usedPercent"]!).ToString(CultureInfo.InvariantCulture) + "%";
            switch ((int?)window["windowDurationMins"])
            {
                case 300: return "5h at " + percent;
                case 10080: return "weekly at " + percent;
                default: return "plan at " + percent;
            }
        }

        private static int ToInt(JToken? token)
        {
            var value = (long?)token ?? 0;
            if (value <= 0) return 0;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
