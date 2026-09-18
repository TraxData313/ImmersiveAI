using System.Collections.Generic;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class CodexAppServerShapeTests
    {
        [Fact]
        public void StrictSchemaSealsOptionalToolArgumentsAsNullableAndRequired()
        {
            var tool = new ToolDefinition("recall_market", "Recall the market.", new[]
            {
                new ToolParameter("place", "The town."),
                new ToolParameter("item", "Optional good.", required: false,
                    allowedValues: new[] { "grain", "wine" }),
            });

            var schema = JObject.Parse(CodexAppServerShape.BuildStrictSchema(new[] { tool }, true));
            var args = (JObject)schema.SelectToken("properties.tool_calls.items.anyOf[0].properties.arguments")!;
            Assert.False((bool)args["additionalProperties"]!);
            Assert.Equal(new[] { "place", "item" }, args["required"]!.Values<string>());
            Assert.Equal(new[] { "string", "null" },
                args.SelectToken("properties.item.type")!.Values<string>());
            Assert.Equal(new string?[] { "grain", "wine", null },
                args.SelectToken("properties.item.enum")!.Values<string?>());
        }

        [Fact]
        public void FinalRoundEmptyToolArrayStillHasAValidItemShape()
        {
            var schema = JObject.Parse(CodexAppServerShape.BuildStrictSchema(null, false));
            Assert.Equal(0, (int)schema.SelectToken("properties.tool_calls.maxItems")!);
            Assert.Equal("object", (string?)schema.SelectToken("properties.tool_calls.items.type"));
            Assert.False((bool)schema.SelectToken("properties.tool_calls.items.additionalProperties")!);
        }

        [Fact]
        public void TurnEventsKeepFinalAnswerAndMeasuredUsage()
        {
            var env = new CodexAppServerShape.TurnEnvelope();
            Assert.False(CodexAppServerShape.FoldTurnEvent(env,
                JObject.Parse("{\"method\":\"item/agentMessage/delta\",\"params\":{\"threadId\":\"other\",\"delta\":\"wrong\"}}"), "ours"));

            var events = new[]
            {
                "{\"method\":\"item/agentMessage/delta\",\"params\":{\"threadId\":\"ours\",\"delta\":\"{\\\"reply\\\":\\\"hel\"}}",
                "{\"method\":\"item/completed\",\"params\":{\"threadId\":\"ours\",\"item\":{\"type\":\"agentMessage\",\"phase\":\"final_answer\",\"text\":\"{\\\"reply\\\":\\\"hello\\\",\\\"tool_calls\\\":[]}\"}}}",
                "{\"method\":\"thread/tokenUsage/updated\",\"params\":{\"threadId\":\"ours\",\"tokenUsage\":{\"total\":{\"inputTokens\":120,\"outputTokens\":14,\"reasoningOutputTokens\":3},\"modelContextWindow\":1050000}}}",
                "{\"method\":\"turn/completed\",\"params\":{\"threadId\":\"ours\",\"turn\":{\"status\":\"completed\"}}}",
            };
            foreach (var line in events)
                Assert.True(CodexAppServerShape.FoldTurnEvent(env, JObject.Parse(line), "ours"));

            Assert.True(env.Finished);
            Assert.True(env.Ok);
            Assert.Equal("{\"reply\":\"hello\",\"tool_calls\":[]}", env.ResultText);
            Assert.Equal(120, env.TokensIn);
            Assert.Equal(14, env.TokensOut);
            Assert.Equal(3, env.ThinkingTokens);
            Assert.Equal(1050000, env.ContextWindow);
        }

        [Fact]
        public void FailedTurnKeepsServerReason()
        {
            var env = new CodexAppServerShape.TurnEnvelope();
            CodexAppServerShape.FoldTurnEvent(env, JObject.Parse(
                "{\"method\":\"turn/completed\",\"params\":{\"turn\":{\"status\":\"failed\",\"error\":{\"message\":\"usage limit reached\"}}}}"), "ours");
            Assert.True(env.Finished);
            Assert.False(env.Ok);
            Assert.Equal("usage limit reached", env.ErrorText);
        }

        [Fact]
        public void SubscriptionWindowsUseReportedDurations()
        {
            var result = JObject.Parse("{\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"usedPercent\":58.2,\"windowDurationMins\":300},\"secondary\":{\"usedPercent\":9,\"windowDurationMins\":10080}}}}");
            Assert.Equal("5h at 58%, weekly at 9%", CodexAppServerShape.ComposeRateLimitLabel(result));
        }

        [Fact]
        public void PromptShapePreservesTheExistingNpcToolContract()
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System("I am Rhia."),
                ChatMessage.User("Who are you?"),
                ChatMessage.Assistant("Rhia."),
                ChatMessage.User("And now?"),
            };
            var system = CodexAppServerShape.BuildSystem(messages,
                new[] { new ToolDefinition("recall_person", "Recall someone.") }, true, 400);
            Assert.Contains("recall_person", system);
            Assert.Contains("WALL", system);
            Assert.Contains("[I answered:]\nRhia.", CodexAppServerShape.BuildTranscript(messages));
        }
    }
}
