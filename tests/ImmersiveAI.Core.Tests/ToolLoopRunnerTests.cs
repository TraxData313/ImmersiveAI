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

    private sealed class ScriptedToolClient : IToolChatClient
    {
        public readonly Queue<ChatResult> Script = new();
        public readonly List<IReadOnlyList<ChatMessage>> Requests = new();
        public readonly List<bool> AllowFlags = new();

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
            return Task.FromResult(Script.Count > 0 ? Script.Dequeue() : new ChatResult("out of script"));
        }
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
}
