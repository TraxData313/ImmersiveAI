using LivingCalradia.Core.Memory;

namespace LivingCalradia.Core.Tests;

public class NpcMemoryTests
{
    private static ConversationTurn Turn(string player, string npc, double day = 1) =>
        new() { PlayerLine = player, NpcLine = npc, GameDay = day };

    [Fact]
    public void AddTurn_AppendsAndTracksLastConversationDay()
    {
        var memory = new NpcMemory { NpcId = "lord_1" };

        memory.AddTurn(Turn("Hello", "Well met", day: 12));
        memory.AddTurn(Turn("Farewell", "Safe travels", day: 15));

        Assert.Equal(2, memory.RecentTurns.Count);
        Assert.Equal(15, memory.LastConversationGameDay);
    }

    [Fact]
    public void NeedsCompression_OnlyWhenOverThreshold()
    {
        var memory = new NpcMemory();
        for (int i = 0; i < 10; i++) memory.AddTurn(Turn($"p{i}", $"n{i}"));

        Assert.False(memory.NeedsCompression(maxRecentTurns: 10));
        Assert.True(memory.NeedsCompression(maxRecentTurns: 9));
    }

    [Fact]
    public void GetTurnsToCompress_ReturnsOldestKeepingNewest()
    {
        var memory = new NpcMemory();
        for (int i = 0; i < 5; i++) memory.AddTurn(Turn($"p{i}", $"n{i}"));

        var toCompress = memory.GetTurnsToCompress(keepMostRecent: 2);

        Assert.Equal(3, toCompress.Count);
        Assert.Equal("p0", toCompress[0].PlayerLine);
        Assert.Equal("p2", toCompress[2].PlayerLine);
    }

    [Fact]
    public void GetTurnsToCompress_WhenKeepingMoreThanExists_ReturnsEmpty()
    {
        var memory = new NpcMemory();
        memory.AddTurn(Turn("p0", "n0"));

        Assert.Empty(memory.GetTurnsToCompress(keepMostRecent: 5));
    }

    [Fact]
    public void ApplyCompression_ReplacesSummaryRemovesConsumedTurnsAndMergesFacts()
    {
        var memory = new NpcMemory { Summary = "old summary" };
        memory.KnownFacts.Add("The player is a Vlandian vassal");
        for (int i = 0; i < 5; i++) memory.AddTurn(Turn($"p{i}", $"n{i}"));

        memory.ApplyCompression(
            "new summary",
            consumedTurnCount: 3,
            newFacts: new[] { "The player saved my caravan", "the player is a vlandian vassal", "  ", "The player saved my caravan" });

        Assert.Equal("new summary", memory.Summary);
        Assert.Equal(2, memory.RecentTurns.Count);
        Assert.Equal("p3", memory.RecentTurns[0].PlayerLine);
        // duplicate (case-insensitive) and blank facts are not added
        Assert.Equal(2, memory.KnownFacts.Count);
        Assert.Contains("The player saved my caravan", memory.KnownFacts);
    }

    [Fact]
    public void ApplyCompression_InvalidConsumedCount_Throws()
    {
        var memory = new NpcMemory();
        memory.AddTurn(Turn("p", "n"));

        Assert.Throws<ArgumentOutOfRangeException>(() => memory.ApplyCompression("s", consumedTurnCount: 2));
    }
}
