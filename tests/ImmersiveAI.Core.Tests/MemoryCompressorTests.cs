using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Tests;

public class MemoryCompressorTests
{
    private sealed class FakeChatClient : IChatClient
    {
        public string Response = "";
        public IReadOnlyList<ChatMessage>? LastRequest;

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            LastRequest = messages;
            return Task.FromResult(Response);
        }
    }

    private static NpcMemory MemoryWithTurns(int count)
    {
        var memory = new NpcMemory { NpcId = "lord_1", NpcName = "Gafnir" };
        for (int i = 0; i < count; i++)
            memory.AddTurn(new ConversationTurn { PlayerLine = $"p{i}", NpcLine = $"n{i}", GameDay = i });
        return memory;
    }

    [Fact]
    public async Task CompressAsync_FoldsOldTurnsIntoSummaryAndFacts()
    {
        var client = new FakeChatClient
        {
            Response = "SUMMARY:\nWe fought together at Omor and grew to trust one another.\nFACTS:\n- Vulgrim saved my life at Omor\n- none of importance"
        };
        var memory = MemoryWithTurns(10);

        var compressed = await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 4);

        Assert.True(compressed);
        Assert.Equal(4, memory.RecentTurns.Count);
        Assert.Equal("p6", memory.RecentTurns[0].PlayerLine);
        Assert.Contains("Omor", memory.Summary);
        Assert.Contains("Vulgrim saved my life at Omor", memory.KnownFacts);
    }

    [Fact]
    public async Task CompressAsync_NothingToCompress_ReturnsFalse()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nirrelevant" };
        var memory = MemoryWithTurns(3);

        var compressed = await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 5);

        Assert.False(compressed);
        Assert.Null(client.LastRequest);
        Assert.Equal(3, memory.RecentTurns.Count);
    }

    [Fact]
    public async Task CompressAsync_ShowsWholeDeepMemoryPlusFadingAndFreshTurns()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);
        memory.Summary = "Old friends from the north.";
        memory.KnownFacts.Add("Vulgrim rules Sargot");

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        var prompt = client.LastRequest![0].Content;
        // Whole deep memory is visible so they can update it with full context.
        Assert.Contains("Old friends from the north.", prompt);
        Assert.Contains("Vulgrim rules Sargot", prompt);
        // Oldest turns are the ones being folded in.
        Assert.Contains("The moments now fading", prompt);
        Assert.Contains("p0", prompt);
        Assert.Contains("p3", prompt);
        // The kept turns are shown as still-fresh context, not folded in.
        Assert.Contains("Still fresh in your mind", prompt);
        Assert.Contains("p4", prompt);
        Assert.Contains("p5", prompt);
    }

    [Fact]
    public async Task CompressAsync_AddressesNpcAsIndividualViaNamedSystemVoice()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: "Muse");

        var prompt = client.LastRequest![0].Content;
        Assert.Contains("Muse (System) addresses you, Gafnir:", prompt);
        Assert.Contains("what to carry forward and what to let go", prompt);
        Assert.Contains("Answer Muse", prompt);
        // Kept the machine-readable contract the parser depends on.
        Assert.Contains("SUMMARY:", prompt);
        Assert.Contains("FACTS:", prompt);
    }

    [Fact]
    public void BuildCompressionRequest_DefaultsSystemVoiceToAngel()
    {
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildCompressionRequest(memory, memory.RecentTurns)[0].Content;

        Assert.Contains("Angel (System) addresses you, Gafnir:", prompt);
    }

    [Fact]
    public void ParseResponse_HandlesMissingFactsSection()
    {
        var result = MemoryCompressor.ParseResponse("SUMMARY:\nJust a summary.");

        Assert.Equal("Just a summary.", result.Summary);
        Assert.Empty(result.Facts);
    }

    [Fact]
    public void ParseResponse_NoHeaders_TreatsWholeTextAsSummary()
    {
        var result = MemoryCompressor.ParseResponse("The model just wrote prose.");

        Assert.Equal("The model just wrote prose.", result.Summary);
    }

    [Fact]
    public void ParseResponse_FactsNone_YieldsNoFacts()
    {
        var result = MemoryCompressor.ParseResponse("SUMMARY:\ns\nFACTS:\n- none");

        Assert.Empty(result.Facts);
    }
}
