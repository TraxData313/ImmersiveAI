using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>The Responses API wire format for reasoning models with native NPC tools.</summary>
    public static class OpenAiResponsesShape
    {
        public static JObject BuildRequest(string model, int maxTokens, IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools, bool allowToolUse)
        {
            var input = new JArray();
            foreach (var message in messages)
            {
                if (message.Role == ChatRole.Tool)
                {
                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = message.ToolCallId,
                        ["output"] = message.Content,
                    });
                    continue;
                }

                if (message.ToolCalls.Count == 0 || !string.IsNullOrWhiteSpace(message.Content))
                {
                    input.Add(new JObject
                    {
                        ["role"] = message.Role == ChatRole.System ? "developer"
                            : message.Role == ChatRole.User ? "user" : "assistant",
                        ["content"] = message.Content,
                    });
                }

                foreach (var call in message.ToolCalls)
                {
                    if (call.ProviderSignature != null)
                        foreach (var item in JArray.Parse(call.ProviderSignature))
                            input.Add(item);

                    input.Add(new JObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = call.Id,
                        ["name"] = call.Name,
                        ["arguments"] = call.ArgumentsJson,
                    });
                }
            }

            var request = new JObject
            {
                ["model"] = model,
                ["max_output_tokens"] = Math.Max(16, maxTokens),
                ["reasoning"] = new JObject { ["effort"] = "low" },
                ["store"] = false,
                ["include"] = new JArray("reasoning.encrypted_content"),
                ["input"] = input,
            };

            if (tools == null || tools.Count == 0) return request;

            request["tools"] = new JArray(AnswerShape.NativeOffer(tools, messages).Select(BuildFunction));
            // Keep a recall and the terminal speak call in separate rounds.
            request["parallel_tool_calls"] = false;
            if (AnswerShape.HasFields(tools))
                request["tool_choice"] = allowToolUse
                    ? (JToken)"required"
                    : new JObject { ["type"] = "function", ["name"] = AnswerShape.SpeakTool };
            else if (!allowToolUse)
                request["tool_choice"] = "none";

            return request;
        }

        private static JObject BuildFunction(ToolDefinition tool)
        {
            var properties = new JObject();
            var required = new JArray();
            foreach (var parameter in tool.Parameters)
            {
                var schema = new JObject { ["type"] = parameter.JsonType, ["description"] = parameter.Description };
                if (parameter.AllowedValues != null)
                    schema["enum"] = new JArray(parameter.AllowedValues);
                properties[parameter.Name] = schema;
                if (parameter.Required) required.Add(parameter.Name);
            }

            return new JObject
            {
                ["type"] = "function",
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["strict"] = false,
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required,
                },
            };
        }

        public static ChatResult ParseResult(JObject response, IReadOnlyList<ToolDefinition>? tools)
        {
            var output = response["output"] as JArray ?? new JArray();
            var text = string.Concat(output.Where(item => (string?)item["type"] == "message")
                .SelectMany(item => (JArray?)item["content"] ?? new JArray())
                .Where(part => (string?)part["type"] == "output_text")
                .Select(part => (string?)part["text"])).Trim();
            var calls = new List<ToolCall>();
            var reasoning = new JArray();
            foreach (var item in output)
            {
                if ((string?)item["type"] == "reasoning")
                    reasoning.Add(item.DeepClone());
                else if ((string?)item["type"] == "function_call" && (string?)item["status"] != "incomplete")
                {
                    // Stateless follow-ups replay the reasoning items before the call they led to.
                    calls.Add(new ToolCall((string?)item["call_id"] ?? "", (string?)item["name"] ?? "",
                        (string?)item["arguments"] ?? "{}", reasoning.Count == 0 ? null : reasoning.ToString(Formatting.None)));
                    reasoning.Clear();
                }
            }
            return AnswerShape.FromNative(text, calls, tools);
        }
    }
}
