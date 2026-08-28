using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Tests;

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
    public void TotalTurns_CountsEveryExchangeAndSurvivesCompression()
    {
        var memory = new NpcMemory();
        for (int i = 0; i < 5; i++) memory.AddTurn(Turn($"p{i}", $"n{i}"));

        Assert.Equal(5, memory.TotalTurns);

        // Folding the three oldest turns into the summary trims RecentTurns but not the lifetime count.
        memory.ApplyCompression("summary", consumedTurnCount: 3);

        Assert.Equal(2, memory.RecentTurns.Count);
        Assert.Equal(5, memory.TotalTurns);
        Assert.Equal(5, memory.StoryRichness);
    }

    [Fact]
    public void StoryRichness_FallsBackToSurvivingTurns_ForMemoriesSavedBeforeTotalTurnsExisted()
    {
        // An old save loads TotalTurns as 0; richness must still reflect the turns it kept verbatim.
        var memory = new NpcMemory { TotalTurns = 0 };
        memory.RecentTurns.Add(Turn("a", "b"));
        memory.RecentTurns.Add(Turn("c", "d"));

        Assert.Equal(2, memory.StoryRichness);
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
    public void NeedsCompression_WhenOldestTurnExceedsRecentDayWindow()
    {
        var memory = new NpcMemory();
        memory.AddTurn(Turn("old", "old reply", day: 9));
        memory.AddTurn(Turn("new", "new reply", day: 20));

        Assert.False(memory.NeedsCompression(maxRecentTurns: 30, currentGameDay: 20, maxRecentDays: 11, maxRecentMemoryTokens: 1000));
        Assert.True(memory.NeedsCompression(maxRecentTurns: 30, currentGameDay: 20, maxRecentDays: 10, maxRecentMemoryTokens: 1000));
    }

    [Fact]
    public void NeedsCompression_WhenRecentTurnEstimateExceedsTokenLimit()
    {
        var memory = new NpcMemory();
        memory.AddTurn(Turn(new string('p', 100), new string('n', 100)));

        Assert.False(memory.NeedsCompression(maxRecentTurns: 30, currentGameDay: 1, maxRecentDays: 30, maxRecentMemoryTokens: 1000));
        Assert.True(memory.NeedsCompression(maxRecentTurns: 30, currentGameDay: 1, maxRecentDays: 30, maxRecentMemoryTokens: 10));
    }

    [Fact]
    public void GetKeepMostRecentForCompression_AppliesTurnDayAndTokenTargets()
    {
        var memory = new NpcMemory();
        memory.AddTurn(Turn("old", "old reply", day: 1));
        memory.AddTurn(Turn("middle", "middle reply", day: 10));
        memory.AddTurn(Turn("new", "new reply", day: 20));
        memory.AddTurn(Turn("newer", "newer reply", day: 21));

        Assert.Equal(2, memory.GetKeepMostRecentForCompression(
            keepRecentTurns: 3,
            currentGameDay: 21,
            keepRecentDays: 5,
            minRecentMemoryTokensAfterCompression: 1000));

        Assert.Equal(0, memory.GetKeepMostRecentForCompression(
            keepRecentTurns: 3,
            currentGameDay: 21,
            keepRecentDays: 5,
            minRecentMemoryTokensAfterCompression: 1));
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
    public void ApplyCompression_ReplacesSummaryAndRemovesConsumedTurns()
    {
        var memory = new NpcMemory { Summary = "old summary" };
        for (int i = 0; i < 5; i++) memory.AddTurn(Turn($"p{i}", $"n{i}"));

        memory.ApplyCompression("new summary", consumedTurnCount: 3);

        Assert.Equal("new summary", memory.Summary);
        Assert.Equal(2, memory.RecentTurns.Count);
        Assert.Equal("p3", memory.RecentTurns[0].PlayerLine);
    }

    [Fact]
    public void ApplyCompression_LeavesTheRetiredTruthsUntouched()
    {
        // The distilled truths were retired 2026.08.08. Whatever an older save still carries in the
        // field stays exactly where it lies — nothing reads it, and nothing may quietly destroy it.
        var memory = new NpcMemory();
        memory.KnownFacts.Add("a truth she held under the old shape");

        memory.ApplyCompression("the whole of what I now remember", consumedTurnCount: 0);

        Assert.Equal(new[] { "a truth she held under the old shape" }, memory.KnownFacts);
    }

    [Fact]
    public void ApplyCompression_InvalidConsumedCount_Throws()
    {
        var memory = new NpcMemory();
        memory.AddTurn(Turn("p", "n"));

        Assert.Throws<ArgumentOutOfRangeException>(() => memory.ApplyCompression("s", consumedTurnCount: 2));
    }

    [Fact]
    public void Outreach_CountsAndRests_UntilThePlayerEngages()
    {
        var memory = new NpcMemory();
        Assert.Equal(-1, memory.LastOutreachGameDay, 5);
        Assert.Equal(0, memory.UnansweredOutreachCount);

        // Two outreaches into silence stack; a mere weighing rests them without wounding pride.
        memory.NoteOutreach(10);
        memory.NoteOutreach(12);
        Assert.Equal(12, memory.LastOutreachGameDay, 5);
        Assert.Equal(2, memory.UnansweredOutreachCount);
        memory.NoteOutreachConsidered(13);
        Assert.Equal(13, memory.LastOutreachGameDay, 5);
        Assert.Equal(2, memory.UnansweredOutreachCount);

        // The player engaging outside the turn stream clears the silence; the rest-day stamp remains.
        memory.NotePlayerEngaged();
        Assert.Equal(0, memory.UnansweredOutreachCount);
        Assert.Equal(13, memory.LastOutreachGameDay, 5);
    }

    [Fact]
    public void AddTurn_PlayerTurnAnswersTheSilence_AngelTurnDoesNot()
    {
        var memory = new NpcMemory();
        memory.NoteOutreach(10);

        // An Angel beat (their own reaching-out being narrated) is not the player answering.
        memory.AddTurn(new ConversationTurn { Speaker = ConversationTurn.AngelSpeaker, PlayerLine = "…", GameDay = 10 });
        Assert.Equal(1, memory.UnansweredOutreachCount);

        // Neither is one of their OWN inner reckonings (the reach-out beats since 2026.07.26).
        memory.AddTurn(new ConversationTurn { Speaker = ConversationTurn.InnerSpeaker, PlayerLine = "…", NpcLine = "STAY", GameDay = 10 });
        Assert.Equal(1, memory.UnansweredOutreachCount);

        // A turn whose incoming line is the player's IS the answer.
        memory.AddTurn(new ConversationTurn { PlayerLine = "Well met.", NpcLine = "And you.", GameDay = 11 });
        Assert.Equal(0, memory.UnansweredOutreachCount);
    }

    // ------------------------- the envelope that walked in whole -------------------------

    [Fact]
    public void HealEnvelopeLines_UndressesARecordedRawAnswer_AndLeavesHonestLinesAlone()
    {
        // 2026.08.28, Rhia again: a wrapped Claude Code answer failed strict parsing and the raw
        // {"reply": ...} envelope was recorded as her spoken line. The heal keeps only the speech —
        // the tool calls inside are dead history and must never re-fire.
        var rhia = new NpcMemory { NpcId = "CharacterObject_1884" };
        rhia.RecentTurns.Add(new ConversationTurn
        {
            PlayerLine = "Go up, and soak.",
            NpcLine = "<StructuredOutput>\n{\"reply\": \"Tonight I will only be glad.\", " +
                "\"tool_calls\": [{\"name\": \"move_heart\", \"arguments\": {\"shift\": \"3\"}}]}\n</StructuredOutput>",
        });
        rhia.RecentTurns.Add(new ConversationTurn
        {
            PlayerLine = "And the ledger?",
            NpcLine = "It reads {\"grain\": 12} — the clerk writes strangely.",
        });

        rhia.HealEnvelopeLines();

        Assert.Equal("Tonight I will only be glad.", rhia.RecentTurns[0].NpcLine);
        Assert.Contains("the clerk writes strangely", rhia.RecentTurns[1].NpcLine);

        // Idempotent: a healed line no longer matches.
        rhia.HealEnvelopeLines();
        Assert.Equal("Tonight I will only be glad.", rhia.RecentTurns[0].NpcLine);
    }

    // ------------------------- the one-day notes experiment -------------------------

    [Fact]
    public void HealLegacyNotes_FoldsAOneDayNoteShelfBackIntoThePage()
    {
        // 2026.08.27: the deep memory was keyed notes for a day, then reverted. A soul who
        // compressed during that day has her WHOLE memory in Bites with an empty page — so the
        // revert had to fold it back, or it would not have restored her memory, it would have
        // deleted her. Anton: "if there are current NPCs like Rhia that switched to key: value
        // just leave it as a text."
        var rhia = new NpcMemory
        {
            NpcId = "CharacterObject_1884",
            Bites =
            {
                ["ahil"] = "My captain, who hired me at Dunglanys and has kept me close.",
                ["wage"] = "Ahil owes me 34 denars each day.",
            },
        };

        rhia.HealLegacyNotes();

        Assert.Empty(rhia.Bites);
        Assert.Contains("ahil: My captain, who hired me at Dunglanys and has kept me close.", rhia.Summary);
        Assert.Contains("wage: Ahil owes me 34 denars each day.", rhia.Summary);
        Assert.True(rhia.HasDeepMemory);
    }

    [Fact]
    public void HealLegacyNotes_KeepsAnyPageThatStands_AndIsIdempotent()
    {
        // A soul mid-experiment may hold BOTH — the page she had not yet converted and a note or
        // two written live. Neither may be thrown away, and the page comes first because it is
        // the older, fuller telling.
        var both = new NpcMemory
        {
            Summary = "He found me at Dunglanys and gave me a place in his company.",
            Bites = { ["wage"] = "34 denars a day." },
        };

        both.HealLegacyNotes();
        Assert.Contains("He found me at Dunglanys", both.Summary);
        Assert.Contains("wage: 34 denars a day.", both.Summary);
        Assert.True(both.Summary.IndexOf("Dunglanys", StringComparison.Ordinal)
                    < both.Summary.IndexOf("wage:", StringComparison.Ordinal));

        // Running again changes nothing — it is a load-time heal and loads happen constantly.
        var once = both.Summary;
        both.HealLegacyNotes();
        Assert.Equal(once, both.Summary);

        // And a memory that never saw the experiment is untouched.
        var plain = new NpcMemory { Summary = "Nothing to heal." };
        plain.HealLegacyNotes();
        Assert.Equal("Nothing to heal.", plain.Summary);
    }

    // A talk with no words in it is not a talk (2026.08.28). This is the test both the line's
    // anchor and the parted-stamp ask, so what counts as "we spoke" is settled in one place.
    [Fact]
    public void IsSpokenExchange_CountsOnlyWordsThatTrulyPassed()
    {
        Assert.True(Turn("Well?", "Well enough.").IsSpokenExchange);

        // A silent beat their own mind set down: a meeting note, a letter, a night's title.
        Assert.False(new ConversationTurn
        {
            Speaker = ConversationTurn.InnerSpeaker,
            PlayerLine = "We met and spoke face to face",
        }.IsSpokenExchange);

        // The retired narrator's turns are still in old saves, and were never a talk either.
        Assert.False(new ConversationTurn
        {
            Speaker = ConversationTurn.AngelSpeaker,
            PlayerLine = "He comes to you again",
            NpcLine = "Then I will hear him.",
        }.IsSpokenExchange);

        // Nothing said at all — opening a thread and closing it again.
        Assert.False(new ConversationTurn { GameDay = 3 }.IsSpokenExchange);

        // One side alone is still words that passed: a line sent whose answer never came.
        Assert.True(new ConversationTurn { PlayerLine = "Are you there?" }.IsSpokenExchange);
    }
}
