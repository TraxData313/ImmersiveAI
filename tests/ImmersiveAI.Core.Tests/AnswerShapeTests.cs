using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Prompts;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The heart's measure as a REQUIRED field of every spoken answer (2026.10.01, Anton's ask): one
/// call when nothing is looked up, a number always present, never inside the words. Covers both
/// shapes — the flattened roads' JSON envelope and the native roads' speak hand — and the loop.
/// </summary>
public class AnswerShapeTests
{
    // Mirrors the Module's HeartTool.Tool (an answer field named "heart" over the move_heart hand).
    private static readonly ToolDefinition Heart = new("move_heart",
        "how what just passed moved my regard — 0 when my heart held.",
        new[] { new ToolParameter("shift", "A whole number from -100 to 100.", jsonType: "integer") },
        answerField: "heart");

    private static readonly ToolDefinition Recall = new("recall_person", "Call a person to mind.",
        new[] { new ToolParameter("name", "Their name.") });

    private static readonly ToolDefinition[] HeartAndRecall = { Recall, Heart };

    private static int? ShiftOf(ToolCall call) =>
        FeelingParser.ParseShift(JObject.Parse(call.ArgumentsJson)["shift"]?.ToString() ?? "");

    // ---------------------------------------------------------------- the definition

    [Fact]
    public void AnAnswerField_IsSilent_AndCarriesOneParameter()
    {
        Assert.True(Heart.IsAnswerField);
        Assert.True(Heart.Silent);
        Assert.Equal("integer", Heart.Parameters[0].JsonType);
        Assert.Throws<ArgumentException>(() => new ToolDefinition("two", "x",
            new[] { new ToolParameter("a", "a"), new ToolParameter("b", "b") }, answerField: "f"));
        Assert.Equal("string", Recall.Parameters[0].JsonType);
    }

    // ---------------------------------------------------------------- the flattened roads (envelope)

    [Fact]
    public void Schema_MakesTheHeartARequiredIntegerBesideTheReply_NotAHand()
    {
        var schema = JObject.Parse(ClaudeCliShape.BuildSchema(HeartAndRecall, allowToolUse: true));

        Assert.Equal("integer", (string?)schema.SelectToken("properties.heart.type"));
        Assert.Contains("heart", schema["required"]!.Values<string>());
        var handNames = schema.SelectTokens("properties.tool_calls.items.anyOf[*].properties.name.enum[0]")
            .Select(t => (string?)t).ToList();
        Assert.Equal(new[] { "recall_person" }, handNames);
    }

    [Fact]
    public void Schema_KeepsTheHeartOnTheForcedLastRound()
    {
        var schema = JObject.Parse(ClaudeCliShape.BuildSchema(HeartAndRecall, allowToolUse: false));

        Assert.Contains("heart", schema["required"]!.Values<string>());
        Assert.Equal(0, (int?)schema.SelectToken("properties.tool_calls.maxItems"));
    }

    [Fact]
    public void StrictSchema_KeepsTheHeartRequired_AndNotNullable()
    {
        var schema = JObject.Parse(CodexAppServerShape.BuildStrictSchema(HeartAndRecall, allowToolUse: true));

        Assert.Equal("integer", (string?)schema.SelectToken("properties.heart.type"));
        Assert.Contains("heart", schema["required"]!.Values<string>());
        Assert.False((bool)schema["additionalProperties"]!);
    }

    [Fact]
    public void Sheet_NamesTheHeartOnce_AsPartOfTheAnswer_NeverAsAHand()
    {
        var system = ClaudeCliShape.BuildSystem(new[] { ChatMessage.System("I am Ira.") }, HeartAndRecall, true);

        Assert.Contains("- recall_person(", system);
        Assert.DoesNotContain("- move_heart(", system);
        Assert.Contains("Beside every reply I also set down \"heart\": " + Heart.Description, system);
        Assert.Equal(1, CountOf(system, Heart.Description));
    }

    [Fact]
    public void Sheet_WithOnlyTheHeart_StillSaysWhereTheWordsGo()
    {
        var system = ClaudeCliShape.BuildSystem(new[] { ChatMessage.System("I am Ira.") }, new[] { Heart }, true);

        Assert.Contains("How I answer: my spoken words go in \"reply\".", system);
        Assert.DoesNotContain("These hands are mine", system);
        Assert.Contains("\"heart\"", system);
    }

    [Fact]
    public void Envelope_WithTheHeart_GivesTheWordsAndOneAnswerCall()
    {
        var result = ClaudeCliShape.ParseToolResult(
            "{\"reply\":\"You came back to me.\",\"tool_calls\":[],\"heart\":2}", HeartAndRecall);

        Assert.Equal("You came back to me.", result.Text);
        Assert.False(result.WantsTools);
        var call = Assert.Single(result.AnswerCalls);
        Assert.Equal("move_heart", call.Name);
        Assert.Equal(2, ShiftOf(call));
    }

    [Fact]
    public void Envelope_ZeroIsAFullAnswer()
    {
        var result = CodexAppServerShape.ParseToolResult(
            "{\"reply\":\"Aye.\",\"tool_calls\":[],\"heart\":0}", HeartAndRecall);

        Assert.Equal(0, ShiftOf(Assert.Single(result.AnswerCalls)));
    }

    [Fact]
    public void Envelope_MissingHeart_LeavesItUnweighed()
    {
        var result = ClaudeCliShape.ParseToolResult("{\"reply\":\"Aye.\",\"tool_calls\":[]}", HeartAndRecall);

        Assert.Equal("Aye.", result.Text);
        Assert.Empty(result.AnswerCalls);
    }

    [Theory]
    [InlineData("\"-3\"", -3)]
    [InlineData("250", 100)]
    [InlineData("-999", -100)]
    [InlineData("\"+4 — a true kindness\"", 4)]
    public void Envelope_LenientNumbers_AreReadAndClamped(string heart, int expected)
    {
        var result = ClaudeCliShape.ParseToolResult(
            "{\"reply\":\"Aye.\",\"tool_calls\":[],\"heart\":" + heart + "}", HeartAndRecall);

        Assert.Equal(expected, ShiftOf(Assert.Single(result.AnswerCalls)));
    }

    [Fact]
    public void Envelope_GarbledHeart_IsUnreadable_NotAGuess()
    {
        var result = ClaudeCliShape.ParseToolResult(
            "{\"reply\":\"Aye.\",\"tool_calls\":[],\"heart\":\"warm\"}", HeartAndRecall);

        Assert.Null(ShiftOf(Assert.Single(result.AnswerCalls)));
    }

    [Fact]
    public void Envelope_NullHeart_IsMissing()
    {
        var result = ClaudeCliShape.ParseToolResult(
            "{\"reply\":\"Aye.\",\"tool_calls\":[],\"heart\":null}", HeartAndRecall);

        Assert.Empty(result.AnswerCalls);
    }

    [Fact]
    public void Envelope_ANumberLeakedIntoTheWords_IsLiftedOut()
    {
        var result = ClaudeCliShape.ParseToolResult(
            "{\"reply\":\"I missed you.\\nheart: +2\\n[voice: soft, glad]\",\"tool_calls\":[],\"heart\":2}",
            HeartAndRecall);

        Assert.Equal("I missed you.\n[voice: soft, glad]", result.Text);
    }

    [Fact]
    public void Prose_WithoutAnEnvelope_NeverYieldsAMeasure()
    {
        var result = ClaudeCliShape.ParseToolResult("I missed you. (heart: +2)", HeartAndRecall);

        Assert.Equal("I missed you.", result.Text);
        Assert.Empty(result.AnswerCalls);
    }

    // ---------------------------------------------------------------- leaks

    [Theory]
    [InlineData("You came back.\nheart: 2", "You came back.")]
    [InlineData("You came back.\n[heart: -1]\n[voice: low]", "You came back.\n[voice: low]")]
    [InlineData("You came back. [heart +3]", "You came back.")]
    [InlineData("You came back.\n{\"heart\": 0}", "You came back.")]
    [InlineData("You came back.\n**Heart:** −2", "You came back.")]
    [InlineData("You came back.\nmove_heart(3)", "You came back.")]
    [InlineData("You came back.\nshift: +1", "You came back.")]
    public void LeakedMeasures_AreStripped(string words, string expected)
    {
        Assert.Equal(expected, AnswerShape.StripLeakedMeasure(words, new[] { Heart }));
    }

    [Theory]
    [InlineData("My heart is yours, all 3 of my sons know it.")]
    [InlineData("Heart of the matter: I waited 2 days.")]
    [InlineData("*I press my hand to my heart*\nStay.")]
    public void SpeechAboutTheHeart_IsNeverTouched(string words)
    {
        Assert.Equal(words, AnswerShape.StripLeakedMeasure(words, new[] { Heart }));
    }

    // ---------------------------------------------------------------- the native roads (speak hand)

    [Fact]
    public void NativeOffer_FoldsTheHeartIntoOneSpeakHand()
    {
        var offer = AnswerShape.NativeOffer(HeartAndRecall);

        Assert.Equal(new[] { "recall_person", "speak" }, offer.Select(t => t.Name));
        var speak = offer[1];
        Assert.Equal(new[] { "words", "heart" }, speak.Parameters.Select(p => p.Name));
        Assert.All(speak.Parameters, p => Assert.True(p.Required));
        Assert.Equal("integer", speak.Parameters[1].JsonType);
        Assert.Equal(Heart.Description, speak.Parameters[1].Description);
        Assert.False(speak.IsAnswerField);
    }

    [Fact]
    public void NativeOffer_AsksForTheVoiceDirection_OnlyWhenTheSheetDoes()
    {
        var spoken = new[] { ChatMessage.System("- My voice is heard. Every reply of mine ENDS with one line more: [voice: …]"),
            ChatMessage.User("Hello.") };
        var quotedInHistory = new[] { ChatMessage.System("I am Ira."), ChatMessage.Assistant("Hi.\n[voice: soft]"),
            ChatMessage.User("Hello.") };

        var withVoice = AnswerShape.NativeOffer(HeartAndRecall, spoken).Single(t => t.Name == "speak");
        var without = AnswerShape.NativeOffer(HeartAndRecall, quotedInHistory).Single(t => t.Name == "speak");

        Assert.Equal(new[] { "words", "voice", "heart" }, withVoice.Parameters.Select(p => p.Name));
        Assert.All(withVoice.Parameters, p => Assert.True(p.Required));
        Assert.Equal(new[] { "words", "heart" }, without.Parameters.Select(p => p.Name));
    }

    [Theory]
    [InlineData("{\"words\":\"Stay.\",\"voice\":\"low and warm\",\"heart\":1}", "Stay.\n[voice: low and warm]")]
    [InlineData("{\"words\":\"Stay.\",\"voice\":\"[voice: low and warm]\",\"heart\":1}", "Stay.\n[voice: low and warm]")]
    [InlineData("{\"words\":\"Stay.\\n[voice: her own]\",\"voice\":\"low\",\"heart\":1}", "Stay.\n[voice: her own]")]
    [InlineData("{\"words\":\"Stay.\",\"voice\":null,\"heart\":1}", "Stay.")]
    [InlineData("{\"words\":\"Stay.\",\"heart\":1}", "Stay.")]
    public void FromNative_TheVoiceDirection_ComesBackAsTheLastLine(string args, string expected)
    {
        var result = AnswerShape.FromNative("", new[] { new ToolCall("c1", "speak", args) }, HeartAndRecall);

        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void NativeOffer_WithoutFields_IsUntouched()
    {
        var tools = new[] { Recall };
        Assert.Same(tools, AnswerShape.NativeOffer(tools));
    }

    [Fact]
    public void FromNative_SpeakBecomesWordsAndTheHeart()
    {
        var result = AnswerShape.FromNative("", new[]
        {
            new ToolCall("c1", "speak", "{\"words\":\"Welcome home, love.\",\"heart\":3}"),
        }, HeartAndRecall);

        Assert.Equal("Welcome home, love.", result.Text);
        Assert.False(result.WantsTools);
        var heart = Assert.Single(result.AnswerCalls);
        Assert.Equal("move_heart", heart.Name);
        Assert.Equal(3, ShiftOf(heart));
    }

    [Fact]
    public void FromNative_SpeakBesideARecall_KeepsTheRecallAReach()
    {
        var result = AnswerShape.FromNative("", new[]
        {
            new ToolCall("r1", "recall_person", "{\"name\":\"Rhagaea\"}"),
            new ToolCall("c1", "speak", "{\"words\":\"Let me think.\",\"heart\":0}"),
        }, HeartAndRecall);

        Assert.Equal("recall_person", Assert.Single(result.ToolCalls).Name);
        Assert.Equal("Let me think.", result.Text);
        Assert.Single(result.AnswerCalls);
    }

    [Fact]
    public void FromNative_PlainTalkWithoutSpeak_StandsUnmeasured()
    {
        var result = AnswerShape.FromNative("Just words.", Array.Empty<ToolCall>(), HeartAndRecall);

        Assert.Equal("Just words.", result.Text);
        Assert.Empty(result.AnswerCalls);
    }

    [Fact]
    public void FromNative_BrokenJson_IsSalvaged()
    {
        var result = AnswerShape.FromNative("", new[]
        {
            new ToolCall("c1", "speak", "{\"words\":\"I said \\\"stay\\\".\nAnd you did.\",\"heart\":-2"),
        }, HeartAndRecall);

        Assert.StartsWith("I said \"stay\".", result.Text);
        Assert.Equal(-2, ShiftOf(Assert.Single(result.AnswerCalls)));
    }

    [Fact]
    public void FromNative_TheSpeakSignature_MovesToTheFirstReach()
    {
        var result = AnswerShape.FromNative("", new[]
        {
            new ToolCall("c1", "speak", "{\"words\":\"Hm.\",\"heart\":0}", "sig-1"),
            new ToolCall("r1", "recall_person", "{\"name\":\"Ira\"}"),
        }, HeartAndRecall);

        Assert.Equal("sig-1", Assert.Single(result.ToolCalls).ProviderSignature);
    }

    [Fact]
    public void FromNative_WithoutFields_IsAPassThrough()
    {
        var calls = new[] { new ToolCall("r1", "recall_person", "{}") };
        var result = AnswerShape.FromNative("x", calls, new[] { Recall });

        Assert.Equal("x", result.Text);
        Assert.Same(calls, result.ToolCalls);
    }

    // ---------------------------------------------------------------- the loop

    private sealed class Scripted : IToolChatClient
    {
        public readonly Queue<ChatResult> Script = new();
        public readonly List<IReadOnlyList<ChatMessage>> Requests = new();
        public readonly List<bool> Allow = new();

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult("plain");

        public Task<ChatResult> CompleteWithToolsAsync(IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools, bool allowToolUse = true, CancellationToken cancellationToken = default)
        {
            Requests.Add(new List<ChatMessage>(messages));
            Allow.Add(allowToolUse);
            return Task.FromResult(Script.Count > 0 ? Script.Dequeue() : new ChatResult("out of script"));
        }
    }

    private static ToolCall HeartAnswer(int shift) => new("answer_heart", "move_heart", "{\"shift\":" + shift + "}");

    private static List<ChatMessage> Seed() => new()
    {
        ChatMessage.System("I am Ira."),
        ChatMessage.User("You made me smile all day."),
    };

    [Fact]
    public async Task Loop_TheHeartArrivesWithTheWords_InOneCall()
    {
        var client = new Scripted();
        client.Script.Enqueue(new ChatResult("Then I did my work well.", null, new[] { HeartAnswer(2) }));
        var resolved = new List<ToolCall>();

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall,
            c => { resolved.Add(c); return Task.FromResult("ok"); });

        Assert.Equal("Then I did my work well.", text);
        Assert.Single(client.Requests);
        Assert.Equal(2, ShiftOf(Assert.Single(resolved)));
    }

    [Fact]
    public async Task Loop_ARecallRound_ThenTheSpeakingRoundCarriesTheOneHeart()
    {
        var client = new Scripted();
        // The reach round fills the required field too — it measures nothing yet and is let go.
        client.Script.Enqueue(new ChatResult("", new[] { new ToolCall("r1", "recall_person", "{\"name\":\"Rhagaea\"}") },
            new[] { HeartAnswer(5) }));
        client.Script.Enqueue(new ChatResult("The empress? She rides north.", null, new[] { HeartAnswer(1) }));
        var resolved = new List<ToolCall>();

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall,
            c => { resolved.Add(c); return Task.FromResult(c.Name == "recall_person" ? "She rides north." : "ok"); });

        Assert.Equal("The empress? She rides north.", text);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(new[] { "recall_person", "move_heart" }, resolved.Select(c => c.Name));
        Assert.Equal(1, ShiftOf(resolved[1]));
        // The answer field is never fed back as a reach.
        var reach = client.Requests[1].Single(m => m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0);
        Assert.Equal(new[] { "recall_person" }, reach.ToolCalls.Select(c => c.Name));
    }

    [Fact]
    public async Task Loop_TheForcedLastRound_StillBringsTheHeart()
    {
        var client = new Scripted();
        client.Script.Enqueue(new ChatResult("", new[] { new ToolCall("r1", "recall_person", "{}") }));
        client.Script.Enqueue(new ChatResult("I cannot recall her.", null, new[] { HeartAnswer(0) }));
        var resolved = new List<ToolCall>();

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall,
            c => { resolved.Add(c); return Task.FromResult("x"); }, maxToolRounds: 1);

        Assert.Equal(new[] { true, false }, client.Allow);
        Assert.Equal("I cannot recall her.", text);
        Assert.Equal(0, ShiftOf(resolved.Last(c => c.Name == "move_heart")));
    }

    [Fact]
    public async Task Loop_ADraftThatStands_KeepsItsOwnHeart()
    {
        var client = new Scripted();
        client.Script.Enqueue(new ChatResult("Wait — let me think who that is.",
            new[] { new ToolCall("r1", "recall_person", "{}") }, new[] { HeartAnswer(-1) }));
        client.Script.Enqueue(new ChatResult("", null));
        var resolved = new List<ToolCall>();

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall,
            c => { resolved.Add(c); return Task.FromResult("x"); });

        Assert.Equal("Wait — let me think who that is.", text);
        Assert.Equal(-1, ShiftOf(resolved.Single(c => c.Name == "move_heart")));
    }

    [Fact]
    public async Task Loop_WordsWithoutAHeart_ResolveNoMeasure()
    {
        var client = new Scripted();
        client.Script.Enqueue(new ChatResult("Aye.", null));
        var resolved = new List<ToolCall>();

        await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall,
            c => { resolved.Add(c); return Task.FromResult("x"); });

        Assert.Empty(resolved);
    }

    [Fact]
    public async Task Loop_AFailingResolver_NeverCostsTheWords()
    {
        var client = new Scripted();
        client.Script.Enqueue(new ChatResult("Aye.", null, new[] { HeartAnswer(1) }));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall,
            _ => throw new InvalidOperationException("boom"));

        Assert.Equal("Aye.", text);
    }

    [Fact]
    public async Task Loop_ALeakInTheFinalWords_IsLiftedOut()
    {
        var client = new Scripted();
        client.Script.Enqueue(new ChatResult("Aye, love.\n[heart: +1]", null, new[] { HeartAnswer(1) }));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("x"));

        Assert.Equal("Aye, love.", text);
    }

    private static int CountOf(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
