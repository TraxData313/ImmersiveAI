using System.Collections.Generic;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class ClaudeCliShapeTests
    {
        private static ToolDefinition Heart() => new ToolDefinition(
            "move_heart", "How this exchange moved my regard.",
            new[]
            {
                new ToolParameter("shift", "A whole number from -20 to 20."),
                new ToolParameter("why", "One short reason.", required: false),
            });

        private static ToolDefinition Misgivings() => new ToolDefinition(
            "weigh_misgivings", "My own written doubts.",
            new[] { new ToolParameter("act", "What I do with one.", allowedValues: new[] { "set_down", "settle", "release" }) });

        // ── the system prompt ───────────────────────────────────────────────────

        [Fact]
        public void SystemJoinsSheetAndAddsReplyContractOnlyWithTools()
        {
            var messages = new List<ChatMessage> { ChatMessage.System("I am Rhia."), ChatMessage.User("hello") };

            var plain = ClaudeCliShape.BuildSystem(messages, null, allowToolUse: true);
            Assert.Equal("I am Rhia.", plain);

            var armed = ClaudeCliShape.BuildSystem(messages, new[] { Heart(), Misgivings() }, allowToolUse: true);
            Assert.StartsWith("I am Rhia.", armed);
            Assert.Contains("tool_calls", armed);
            // The hands are NAMED in the sheet — schema descriptions alone left them unused
            // (probed live 2026.08.28); the optional mark and the vocabulary ride the sketch.
            Assert.Contains("move_heart(shift, why?)", armed);
            Assert.Contains("weigh_misgivings(act: set_down|settle|release)", armed);
            Assert.Contains("How this exchange moved my regard.", armed);

            var wordsOnly = ClaudeCliShape.BuildSystem(messages, new[] { Heart() }, allowToolUse: false);
            Assert.Contains("reaching for nothing more", wordsOnly);
        }

        // ── the transcript ──────────────────────────────────────────────────────

        [Fact]
        public void SingleUserMessagePassesThroughVerbatim()
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System("I am Rhia."),
                ChatMessage.User("Within my own mind — I, Rhia: what has this year made of me?"),
            };
            Assert.Equal(messages[1].Content, ClaudeCliShape.BuildTranscript(messages));
        }

        [Fact]
        public void HistoryBecomesFirstPersonScriptEndingOnMyTurn()
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System("I am Rhia."),
                ChatMessage.User("How fare the men?"),
                ChatMessage.AssistantToolCalls("", new[] { new ToolCall("cli_1", "recall_company", "{}") }),
                ChatMessage.ToolResult("cli_1", "Forty-two ride with us."),
                ChatMessage.User("And truly?"),
            };
            var script = ClaudeCliShape.BuildTranscript(messages);
            Assert.Contains("[Said to me:]\nHow fare the men?", script);
            Assert.Contains("[I reached for: recall_company({})]", script);
            Assert.Contains("[The world answered recall_company:]\nForty-two ride with us.", script);
            Assert.EndsWith("[Now I answer.]", script);
            // The reach's answer is named by the reach, in order.
            Assert.True(script.IndexOf("[I reached for") < script.IndexOf("[The world answered"));
        }

        [Fact]
        public void PlainAssistantTurnsAreMarkedAsMyOwnAnswers()
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.User("Who are you?"),
                ChatMessage.Assistant("Rhia. A healer."),
                ChatMessage.User("Say it again."),
            };
            var script = ClaudeCliShape.BuildTranscript(messages);
            Assert.Contains("[I answered:]\nRhia. A healer.", script);
        }

        // ── the schema ──────────────────────────────────────────────────────────

        [Fact]
        public void SchemaCarriesTypedToolsAndEnums()
        {
            var schema = JObject.Parse(ClaudeCliShape.BuildSchema(new[] { Heart(), Misgivings() }, allowToolUse: true));
            Assert.Equal("object", (string)schema["type"]);

            var shapes = (JArray)schema["properties"]["tool_calls"]["items"]["anyOf"];
            Assert.Equal(2, shapes.Count);

            var heart = (JObject)shapes[0];
            Assert.Equal("move_heart", (string)heart["properties"]["name"]["enum"][0]);
            var args = (JObject)heart["properties"]["arguments"];
            Assert.NotNull(args["properties"]["shift"]);
            Assert.Single((JArray)args["required"]);              // why is optional

            // The closed vocabulary is a real enum, never prose (the 2026.08.09 law).
            var mis = (JObject)shapes[1];
            var actEnum = (JArray)mis["properties"]["arguments"]["properties"]["act"]["enum"];
            Assert.Contains("settle", actEnum.ToObject<string[]>());
        }

        [Fact]
        public void FinalRoundPinsToolCallsEmpty()
        {
            var schema = JObject.Parse(ClaudeCliShape.BuildSchema(new[] { Heart() }, allowToolUse: false));
            Assert.Equal(0, (int)schema["properties"]["tool_calls"]["maxItems"]);
        }

        // ── parsing the answer ──────────────────────────────────────────────────

        [Fact]
        public void ParsesReplyAndCallsWithSynthesizedIds()
        {
            var result = ClaudeCliShape.ParseToolResult(
                "{\"reply\":\"\",\"tool_calls\":[{\"name\":\"recall_person\",\"arguments\":{\"name\":\"Yngvald\"}},{\"name\":\"recall_company\",\"arguments\":{}}]}");
            Assert.True(result.WantsTools);
            Assert.Equal(2, result.ToolCalls.Count);
            Assert.Equal("cli_1", result.ToolCalls[0].Id);
            Assert.Equal("recall_person", result.ToolCalls[0].Name);
            Assert.Contains("Yngvald", result.ToolCalls[0].ArgumentsJson);
        }

        [Fact]
        public void MalformedAnswerIsStillAnAnswer()
        {
            var result = ClaudeCliShape.ParseToolResult("Rhia only smiles.");
            Assert.False(result.WantsTools);
            Assert.Equal("Rhia only smiles.", result.Text);
        }

        // The live find of 2026.08.28: fable's answer arrived wrapped in the CLI's own tags, and
        // the raw envelope walked into Rhia's thread. These fixtures are her actual recorded turns.
        [Fact]
        public void StructuredOutputWrapperIsUndressed()
        {
            var wrapped = "<StructuredOutput>\n{\"reply\": \"Tonight I will only be glad.\", \"tool_calls\": []}\n</StructuredOutput>";
            var result = ClaudeCliShape.ParseToolResult(wrapped);
            Assert.False(result.WantsTools);
            Assert.Equal("Tonight I will only be glad.", result.Text);
        }

        [Fact]
        public void WrappedEnvelopeKeepsItsToolCalls()
        {
            var wrapped = "<StructuredOutput>\n{\"reply\": \"I touch the back of his hand.\", " +
                "\"tool_calls\": [{\"name\": \"move_heart\", \"arguments\": {\"shift\": \"3\"}}]}\n</StructuredOutput>";
            var result = ClaudeCliShape.ParseToolResult(wrapped);
            Assert.Single(result.ToolCalls);
            Assert.Equal("move_heart", result.ToolCalls[0].Name);
            Assert.Contains("3", result.ToolCalls[0].ArgumentsJson);
        }

        [Fact]
        public void FencedAndTrailingChatterEnvelopesStillParse()
        {
            var fenced = "```json\n{\"reply\": \"Well met.\", \"tool_calls\": []}\n```";
            Assert.Equal("Well met.", ClaudeCliShape.ParseToolResult(fenced).Text);

            var chattered = "{\"reply\": \"Well met.\", \"tool_calls\": []}\nThat is my answer.";
            Assert.Equal("Well met.", ClaudeCliShape.ParseToolResult(chattered).Text);
        }

        [Fact]
        public void ForeignJsonIsSpokenNotMistakenForTheEnvelope()
        {
            // Valid JSON without a "reply" is not ours — an NPC reading a ledger aloud, say.
            var result = ClaudeCliShape.ParseToolResult("{\"grain\": 12}");
            Assert.Equal("{\"grain\": 12}", result.Text);
        }

        // ── unwrapping recorded lines (the memory heal) ─────────────────────────

        [Fact]
        public void RecordedEnvelopeLineUnwrapsToItsSpeechAlone()
        {
            var line = "<StructuredOutput>\n{\"reply\": \"*I close my eyes.*\\n\\nTonight I will only be glad.\", " +
                "\"tool_calls\": [{\"name\": \"move_heart\", \"arguments\": {\"shift\": \"3\"}}]}\n</StructuredOutput>";
            Assert.True(ClaudeCliShape.TryUnwrapSpokenEnvelope(line, out var reply));
            Assert.Equal("*I close my eyes.*\n\nTonight I will only be glad.", reply);
        }

        [Theory]
        [InlineData("Honest words about {braces} mid-sentence.")]
        [InlineData("{\"grain\": 12}")]
        [InlineData("")]
        public void HonestLinesAreNeverUnwrapped(string line)
        {
            Assert.False(ClaudeCliShape.TryUnwrapSpokenEnvelope(line, out _));
        }

        // ── parsing the stream ──────────────────────────────────────────────────

        [Fact]
        public void StreamYieldsResultCostTokensModelAndLimits()
        {
            var stdout =
                "{\"type\":\"system\",\"subtype\":\"init\"}\n" +
                "{\"type\":\"rate_limit_event\",\"rate_limit_info\":{\"status\":\"allowed\",\"resetsAt\":1787912400,\"rateLimitType\":\"five_hour\",\"isUsingOverage\":false}}\n" +
                "not json at all\n" +
                "{\"type\":\"result\",\"is_error\":false,\"result\":\"{\\\"reply\\\":\\\"Well met.\\\",\\\"tool_calls\\\":[]}\",\"total_cost_usd\":0.0123," +
                "\"usage\":{\"input_tokens\":100,\"cache_read_input_tokens\":900,\"cache_creation_input_tokens\":50,\"output_tokens\":40}," +
                "\"modelUsage\":{\"claude-haiku-4-5-20251001\":{\"outputTokens\":40,\"canonicalModel\":\"claude-haiku-4-5\"}}}";
            var env = ClaudeCliShape.ParseStream(stdout);
            Assert.True(env.Ok);
            Assert.Contains("Well met", env.ResultText);
            Assert.Equal(0.0123, env.CostUsd.Value, 6);
            Assert.Equal(1050, env.TokensIn);
            Assert.Equal(40, env.TokensOut);
            Assert.Equal("claude-haiku-4-5", env.Model);
            Assert.Single(env.Limits);
            Assert.Equal("five_hour", env.Limits[0].Kind);
        }

        [Fact]
        public void StreamWithoutResultSaysSoPlainly()
        {
            var env = ClaudeCliShape.ParseStream("{\"type\":\"system\"}\n");
            Assert.False(env.Ok);
            Assert.Contains("no result", env.ErrorText);
        }

        [Fact]
        public void ErrorResultCarriesItsOwnWords()
        {
            var env = ClaudeCliShape.ParseStream(
                "{\"type\":\"result\",\"is_error\":true,\"result\":\"Rate limit reached.\",\"subtype\":\"error_during_execution\"}");
            Assert.False(env.Ok);
            Assert.Contains("Rate limit", env.ErrorText);
        }

        // ── the argument escaper ────────────────────────────────────────────────

        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("has space", "\"has space\"")]
        [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [InlineData("tail\\", "tail\\")]              // no space, no quote: bare is already right
        [InlineData("tail \\", "\"tail \\\\\"")]      // quoted, so the trailing backslash doubles
        [InlineData("a\\\"b", "\"a\\\\\\\"b\"")]
        public void EscapesTheWayCreateProcessUnquotes(string arg, string expected)
        {
            Assert.Equal(expected, ClaudeCliShape.EscapeWindowsArg(arg));
        }

        [Fact]
        public void SchemaSurvivesItsOwnEscaping()
        {
            // The one JSON that rides the command line whole: quote it, unquote it by the msvcrt
            // rules, and the same schema must come back.
            var schema = ClaudeCliShape.BuildSchema(new[] { Misgivings() }, allowToolUse: true);
            var escaped = ClaudeCliShape.EscapeWindowsArg(schema);
            Assert.Equal(schema, Unquote(escaped));
        }

        private static string Unquote(string quoted)
        {
            // CreateProcess's own reading: outside quotes spaces split (not exercised here);
            // 2n backslashes before a quote → n backslashes, quote toggles; 2n+1 → n + literal quote.
            var sb = new System.Text.StringBuilder();
            int i = 0;
            bool inQuotes = false;
            while (i < quoted.Length)
            {
                int slashes = 0;
                while (i < quoted.Length && quoted[i] == '\\') { slashes++; i++; }
                if (i < quoted.Length && quoted[i] == '"')
                {
                    sb.Append('\\', slashes / 2);
                    if (slashes % 2 == 1) sb.Append('"');
                    else inQuotes = !inQuotes;
                    i++;
                }
                else
                {
                    sb.Append('\\', slashes);
                    if (i < quoted.Length) { sb.Append(quoted[i]); i++; }
                }
            }
            return sb.ToString();
        }
    }
}
