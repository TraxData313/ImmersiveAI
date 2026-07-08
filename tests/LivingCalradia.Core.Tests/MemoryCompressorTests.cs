using LivingCalradia.Core.Llm;
using LivingCalradia.Core.Memory;

namespace LivingCalradia.Core.Tests;

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
    public async Task CompressAsync_RequestContainsPriorSummaryAndTurns()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);
        memory.Summary = "Old friends from the north.";

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        var prompt = client.LastRequest![0].Content;
        Assert.Contains("Old friends from the north.", prompt);
        Assert.Contains("p0", prompt);
        Assert.Contains("p3", prompt);
        Assert.DoesNotContain("p4", prompt); // kept verbatim, not compressed
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
