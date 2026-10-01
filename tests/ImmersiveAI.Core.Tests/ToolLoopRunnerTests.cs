using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Core.Tests;

public class ToolLoopRunnerTests
{
    private static readonly ToolDefinition[] RecallTools =
    {
        new ToolDefinition("recall_person", "Call a person to mind.",
            new[] { new ToolParameter("name", "Their name.") }),
    };

    private sealed class PlainFakeClient : IChatClient
    {
        public string Response = "plain";
        public bool Called;

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(Response);
        }
    }

    private static readonly ToolDefinition Heart = new("move_heart", "Weigh the heart.",
        new[] { new ToolParameter("shift", "The measure.") }, silent: true);

    private static readonly ToolDefinition[] HeartAndRecall = { RecallTools[0], Heart };

    private static ToolCall HeartCall(string id = "h1") => new(id, "move_heart", "{\"shift\":\"1\"}");
    private static ToolCall RecallCall(string id = "r1") => new(id, "recall_person", "{\"name\":\"Rhagaea\"}");

    private class ScriptedToolClient : IToolChatClient
    {
        public readonly Queue<ChatResult> Script = new();
        public readonly List<IReadOnlyList<ChatMessage>> Requests = new();
        public readonly List<bool> AllowFlags = new();
        public readonly List<string[]> Offers = new();

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult("plain-fallback");

        public Task<ChatResult> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool allowToolUse = true,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(new List<ChatMessage>(messages));
            AllowFlags.Add(allowToolUse);
            Offers.Add(tools.Select(t => t.Name).ToArray());
            return Task.FromResult(Script.Count > 0 ? Script.Dequeue() : new ChatResult("out of script"));
        }
    }

    /// <summary>A flattened road (Codex, Claude Code): the offer may narrow between rounds.</summary>
    private sealed class NarrowingClient : ScriptedToolClient, IToolOfferPolicy
    {
        public bool OfferMayNarrowMidTurn => true;
    }

    private static List<ChatMessage> Seed() => new()
    {
        ChatMessage.System("You are Gafnir."),
        ChatMessage.User("Who is Rhagaea?"),
    };

    [Fact]
    public async Task PlainClient_FallsBackToPlainCompletion()
    {
        var client = new PlainFakeClient();

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("unused"));

        Assert.True(client.Called);
        Assert.Equal("plain", text);
    }

    [Fact]
    public async Task NoTools_FallsBackToPlainCompletion()
    {
        var client = new ScriptedToolClient();

        var text = await ToolLoopRunner.RunAsync(client, Seed(), Array.Empty<ToolDefinition>(), _ => Task.FromResult("unused"));

        Assert.Equal("plain-fallback", text);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task OneRecall_ResolvesAndSpeaks()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[] { new ToolCall("call_1", "recall_person", "{\"name\":\"Rhagaea\"}") }));
        client.Script.Enqueue(new ChatResult("She is the empress."));

        ToolCall? resolved = null;
        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, call =>
        {
            resolved = call;
            return Task.FromResult("Rhagaea rules the Southern Empire.");
        });

        Assert.Equal("She is the empress.", text);
        Assert.NotNull(resolved);
        Assert.Equal("recall_person", resolved!.Name);
        Assert.Contains("Rhagaea", resolved.ArgumentsJson);

        // The second request must carry the whole exchange: the reach and the world's answer.
        var second = client.Requests[1];
        var assistant = second.First(m => m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0);
        Assert.Equal("call_1", assistant.ToolCalls[0].Id);
        var result = second.First(m => m.Role == ChatRole.Tool);
        Assert.Equal("call_1", result.ToolCallId);
        Assert.Contains("Southern Empire", result.Content);
    }

    [Fact]
    public async Task ProviderSignature_RidesBackWithTheReplayedCall()
    {
        // Gemini signs each function call and 400s unless the replay carries the signature
        // untouched — so the loop's history must preserve it through the round-trip.
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[]
        {
            new ToolCall("call_1", "recall_person", "{\"name\":\"Rhagaea\"}", "sig-abc123"),
        }));
        client.Script.Enqueue(new ChatResult("She is the empress."));

        await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("answer"));

        var replayed = client.Requests[1].First(m => m.Role == ChatRole.Assistant && m.ToolCalls.Count > 0);
        Assert.Equal("sig-abc123", replayed.ToolCalls[0].ProviderSignature);
    }

    [Fact]
    public void ProviderSignature_BlankMeansNone()
    {
        Assert.Null(new ToolCall("id", "recall_person", "{}").ProviderSignature);
        Assert.Null(new ToolCall("id", "recall_person", "{}", "  ").ProviderSignature);
        Assert.Equal("sig", new ToolCall("id", "recall_person", "{}", "sig").ProviderSignature);
    }

    [Fact]
    public async Task SpentBudget_ForcesASpokenAnswer()
    {
        var client = new ScriptedToolClient();
        // The model keeps reaching; after maxToolRounds the runner must forbid tools and take the words.
        for (int i = 0; i < 2; i++)
            client.Script.Enqueue(new ChatResult("", new[] { new ToolCall($"c{i}", "recall_person", "{\"name\":\"x\"}") }));
        client.Script.Enqueue(new ChatResult("Fine, I shall speak."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools,
            _ => Task.FromResult("an answer"), maxToolRounds: 2);

        Assert.Equal("Fine, I shall speak.", text);
        Assert.Equal(new[] { true, true, false }, client.AllowFlags);
    }

    [Fact]
    public async Task FailedOrEmptyRecall_BecomesAnHonestBlank()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[]
        {
            new ToolCall("c1", "recall_person", "{\"name\":\"a\"}"),
            new ToolCall("c2", "recall_person", "{\"name\":\"b\"}"),
        }));
        client.Script.Enqueue(new ChatResult("So be it."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, call =>
            call.Id == "c1"
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(""));

        Assert.Equal("So be it.", text);
        var results = client.Requests[1].Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(ToolLoopRunner.NothingSurfaces, r.Content));
    }

    [Fact]
    public async Task WordsSpokenBesideTheToolCall_SurviveASilentFinalRound()
    {
        // Haiku's habit: the greeting rides IN the same round as move_heart, and the forced
        // final round has nothing left to say. The spoken words must not be dropped for "".
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("Well met, battle brother.",
            new[] { new ToolCall("c1", "recall_person", "{\"name\":\"Vulgrim\"}") }));
        client.Script.Enqueue(new ChatResult("   "));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("r"));

        Assert.Equal("Well met, battle brother.", text);
    }

    [Fact]
    public async Task AFinalRoundThatSpeaks_StillWinsOverEarlierWords()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("Hmm, let me think on her.",
            new[] { new ToolCall("c1", "recall_person", "{\"name\":\"Rhagaea\"}") }));
        client.Script.Enqueue(new ChatResult("She is the empress."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("r"));

        Assert.Equal("She is the empress.", text);
    }

    [Fact]
    public async Task SilenceInEveryRound_StaysAnHonestEmpty()
    {
        // If no round ever spoke, the caller's own "..." fallback should tell the truth.
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[] { new ToolCall("c1", "recall_person", "{}") }));
        client.Script.Enqueue(new ChatResult(""));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("r"));

        Assert.Equal("", text);
    }

    [Fact]
    public async Task CallerMessagesAreNotMutated()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[] { new ToolCall("c1", "recall_person", "{}") }));
        client.Script.Enqueue(new ChatResult("done"));

        var seed = Seed();
        await ToolLoopRunner.RunAsync(client, seed, RecallTools, _ => Task.FromResult("r"));

        Assert.Equal(2, seed.Count);
    }

    [Fact]
    public async Task VoiceKeyAlone_AtTheEnd_IsSilence_TheEarlierWordsStandWithIt()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("Aye, gladly. Point me at them.",
            new[] { new ToolCall("call_1", "recall_person", "{\"name\":\"Rhagaea\"}") }));
        client.Script.Enqueue(new ChatResult("[voice: brightening, with a firm edge]"));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("found"));

        Assert.Equal("Aye, gladly. Point me at them.\n[voice: brightening, with a firm edge]", text);
    }

    [Fact]
    public async Task VoiceKeyAlone_AtTheEnd_KeepsTheEarlierWordsOwnKey()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("Aye.\n[voice: warm]",
            new[] { new ToolCall("call_1", "recall_person", "{\"name\":\"Rhagaea\"}") }));
        client.Script.Enqueue(new ChatResult("[voice: cold]"));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("found"));

        Assert.Equal("Aye.\n[voice: warm]", text);
    }

    [Fact]
    public async Task VoiceKeyAlone_InAToolRound_IsNotRememberedAsWords()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("[voice: warm]",
            new[] { new ToolCall("call_1", "recall_person", "{\"name\":\"Rhagaea\"}") }));
        client.Script.Enqueue(new ChatResult("She is the empress."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), RecallTools, _ => Task.FromResult("found"));

        Assert.Equal("She is the empress.", text);
    }

    // ------------------------- silent hands (the coda bug, 2026.10.01) -------------------------

    [Fact]
    public async Task WordsBesideOnlyASilentHand_AreTheReply_InOneCall()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("You caught me. It was your face by the lamp.\n[voice: warm]",
            new[] { HeartCall() }));
        client.Script.Enqueue(new ChatResult("*I settle closer.* Good night, my love."));   // the coda

        var resolved = new List<string>();
        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, call =>
        {
            resolved.Add(call.Name);
            return Task.FromResult("It is felt.");
        });

        Assert.Equal("You caught me. It was your face by the lamp.\n[voice: warm]", text);
        Assert.Single(client.Requests);                   // no second round, no coda
        Assert.Equal(new[] { "move_heart" }, resolved);    // the heart was still weighed
    }

    [Fact]
    public async Task ASilentHandWithoutWords_StillGetsARoundForTheWords()
    {
        // The API roads: the model sends no words beside a tool call.
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[] { HeartCall() }));
        client.Script.Enqueue(new ChatResult("Oh! I nearly fell asleep on my own answer."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("It is felt."));

        Assert.Equal("Oh! I nearly fell asleep on my own answer.", text);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(new[] { true, true }, client.AllowFlags);
    }

    [Fact]
    public async Task AVoiceKeyBesideASilentHand_IsNotWords()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("[voice: warm]", new[] { HeartCall() }));
        client.Script.Enqueue(new ChatResult("Good evening."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("felt"));

        Assert.Equal("Good evening.", text);
    }

    [Fact]
    public async Task WordsBesideARecall_AreADraft_NeverFedBackAsAnAnswer()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("Rhagaea? Let me think.", new[] { RecallCall(), HeartCall() }));
        client.Script.Enqueue(new ChatResult("She is the empress, and my mother."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("found"));

        Assert.Equal("She is the empress, and my mother.", text);
        var reach = client.Requests[1].Single(m => m.Role == ChatRole.Assistant);
        Assert.Equal("", reach.Content);                          // the draft is not "[I answered:]"
        Assert.Equal(2, reach.ToolCalls.Count);
        Assert.Equal(2, client.Requests[1].Count(m => m.Role == ChatRole.Tool));
    }

    [Fact]
    public async Task ADraft_StillStands_WhenEveryLaterRoundIsSilence()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("Well met.", new[] { RecallCall() }));
        client.Script.Enqueue(new ChatResult(""));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("r"));

        Assert.Equal("Well met.", text);
    }

    [Fact]
    public async Task AfterARecall_WordsBesideTheHeart_EndTheTurn()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[] { RecallCall() }));
        client.Script.Enqueue(new ChatResult("She is the empress.", new[] { HeartCall("h2") }));
        client.Script.Enqueue(new ChatResult("a coda that must never be asked for"));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("r"));

        Assert.Equal("She is the empress.", text);
        Assert.Equal(2, client.Requests.Count);
    }

    [Fact]
    public async Task OnAFlattenedRoad_TheWeighedHeart_LeavesTheOffer()
    {
        var client = new NarrowingClient();
        client.Script.Enqueue(new ChatResult("", new[] { HeartCall(), RecallCall() }));
        client.Script.Enqueue(new ChatResult("She is the empress."));

        await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("r"));

        Assert.Equal(new[] { "recall_person", "move_heart" }, client.Offers[0]);
        Assert.Equal(new[] { "recall_person" }, client.Offers[1]);
    }

    [Fact]
    public async Task OnAFlattenedRoad_NothingLeftToOffer_MeansTheNextRoundIsWords()
    {
        var client = new NarrowingClient();
        client.Script.Enqueue(new ChatResult("", new[] { HeartCall() }));
        client.Script.Enqueue(new ChatResult("Good evening."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), new[] { Heart }, _ => Task.FromResult("felt"));

        Assert.Equal("Good evening.", text);
        Assert.Equal(new[] { true, false }, client.AllowFlags);
    }

    [Fact]
    public async Task OnAnApiRoad_TheOfferNeverNarrows()
    {
        // A history holding tool_use blocks wants every definition to keep riding.
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[] { HeartCall() }));
        client.Script.Enqueue(new ChatResult("Good evening."));

        await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("felt"));

        Assert.All(client.Offers, offer => Assert.Equal(new[] { "recall_person", "move_heart" }, offer));
    }

    [Fact]
    public async Task ReachingAgainOnlyForASpentSilentHand_ForcesWords()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("", new[] { HeartCall("h1") }));
        client.Script.Enqueue(new ChatResult("", new[] { HeartCall("h2") }));
        client.Script.Enqueue(new ChatResult("Good evening."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall,
            _ => Task.FromResult("felt"), maxToolRounds: 3);

        Assert.Equal("Good evening.", text);
        Assert.Equal(new[] { true, true, false }, client.AllowFlags);
    }

    [Fact]
    public async Task AnUnknownHand_IsNeverSilent()
    {
        var client = new ScriptedToolClient();
        client.Script.Enqueue(new ChatResult("Words.", new[] { new ToolCall("x", "not_offered", "{}") }));
        client.Script.Enqueue(new ChatResult("The reply."));

        var text = await ToolLoopRunner.RunAsync(client, Seed(), HeartAndRecall, _ => Task.FromResult("r"));

        Assert.Equal("The reply.", text);
    }

    [Fact]
    public void ToolsAreNotSilent_UnlessSaidSo()
    {
        Assert.False(RecallTools[0].Silent);
        Assert.True(Heart.Silent);
    }
}
