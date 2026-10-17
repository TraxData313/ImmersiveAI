using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Text;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class EscapedBreaksTests
    {
        private static readonly ToolDefinition Heart = new("move_heart", "how what just passed moved my regard.",
            new[] { new ToolParameter("shift", "A whole number.", jsonType: "integer") }, answerField: "heart");

        // 2026.10.15, Safiya on the Claude Code road: the model escaped its breaks twice inside the
        // JSON string, so the parse handed back a literal backslash-n and the thread showed "\n\n".
        private const string Doubled =
            "{\"reply\": \"*I stare after him a moment.*\\\\n\\\\nCheeky Sturgian.\\\\n\\\\n*I nudge my horse.*\", \"tool_calls\": []}";

        [Fact]
        public void TheEnvelope_HandsBackRealBreaks_WhenTheModelEscapedThemTwice()
        {
            var result = ClaudeCliShape.ParseToolResult(Doubled);
            Assert.Equal("*I stare after him a moment.*\n\nCheeky Sturgian.\n\n*I nudge my horse.*", result.Text);
        }

        [Fact]
        public void TheSpeakHand_HandsBackRealBreaks_Too()
        {
            var result = AnswerShape.FromNative("",
                new[] { new ToolCall("c1", "speak", "{\"words\": \"Fine.\\\\nKeep your winks.\", \"heart\": 1}") },
                new[] { Heart });
            Assert.Equal("Fine.\nKeep your winks.", result.Text);
        }

        [Theory]
        [InlineData("plain words", "plain words")]
        [InlineData("a\\nb", "a\nb")]
        [InlineData("a\\r\\nb", "a\nb")]
        [InlineData("she said \\\"aye\\\"", "she said \"aye\"")]
        [InlineData("a doubled \\\\n stays", "a doubled \\\\n stays")]
        [InlineData("real\nbreak", "real\nbreak")]
        public void Heal_PutsBackOnlyWhatWasEscaped(string input, string expected) =>
            Assert.Equal(expected, EscapedBreaks.Heal(input));

        [Fact]
        public void Heal_LeavesAStringWithNoBackslash_AsTheSameInstance()
        {
            var s = "Twenty-seven years.\n\nOld enough.";
            Assert.Same(s, EscapedBreaks.Heal(s));
        }

        [Fact]
        public void RecordedTurns_AreHealedAtLoad()
        {
            var safiya = new NpcMemory { NpcId = "CharacterObject_1854" };
            safiya.RecentTurns.Add(new ConversationTurn
            {
                PlayerLine = "*winks*",
                NpcLine = "*I huff.*\\n\\nCheeky Sturgian.",
            });

            safiya.HealEnvelopeLines();
            Assert.Equal("*I huff.*\n\nCheeky Sturgian.", safiya.RecentTurns[0].NpcLine);

            safiya.HealEnvelopeLines();   // idempotent
            Assert.Equal("*I huff.*\n\nCheeky Sturgian.", safiya.RecentTurns[0].NpcLine);
        }
    }
}
