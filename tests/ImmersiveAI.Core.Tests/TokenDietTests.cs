using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Nights;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Core.Text;

namespace ImmersiveAI.Core.Tests;

/// <summary>Token diet round 2 (2026.10.01): what the prompt stops repeating, and what it keeps.</summary>
public class TokenDietTests
{
    private static NpcPersona Persona(bool voiceTakesMood = false, bool onPaper = false) => new()
    {
        Name = "Gafnir",
        RoleDescription = "A Nord warrior.",
        SpeechStyle = "Terse and blunt.",
        VoiceTakesMood = voiceTakesMood,
        OnPaper = onPaper,
    };

    private static ConversationTurn Spoken(string place, string time, string player, string npc) => new()
    {
        Place = place, CalradiaTime = time, PlayerLine = player, NpcLine = npc,
    };

    private static string History(IReadOnlyList<ChatMessage> messages) =>
        string.Join("\n---\n", messages.Skip(1).Select(m => m.Content));

    // ------------------------------ the stamps ------------------------------

    [Fact]
    public void AStampIsNotRepeated_ForTurnsOfTheSameMoment_ButEveryRealGapStillShows()
    {
        var memory = new NpcMemory();
        memory.AddTurn(Spoken("Varcheg", "1085.03.10 13.22 (Autumn 10, Year 1085)", "Hail.", "Hail."));
        memory.AddTurn(Spoken("Varcheg", "1085.03.10 13.22 (Autumn 10, Year 1085)", "Mead?", "Aye."));
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.InnerSpeaker, Place = "Varcheg",
            CalradiaTime = "1085.03.10 13.22 (Autumn 10, Year 1085)", PlayerLine = "My gear is changed.",
        });
        memory.AddTurn(Spoken("Varcheg", "1085.03.10 13.22 (Autumn 10, Year 1085)", "Another?", "Two."));
        memory.AddTurn(Spoken("Varcheg", "1085.03.10 19.41 (Autumn 10, Year 1085)", "Evening.", "Evening."));
        memory.AddTurn(Spoken("the road", "1085.03.10 19.41 (Autumn 10, Year 1085)", "Ride.", "Riding."));

        var messages = new PromptBuilder().Build(Persona(), memory, "In the tavern.", "Vulgrim", "Now?");
        var history = History(messages);

        Assert.Equal(1, Count(history, "[Varcheg, 1085.03.10 13.22 (Autumn 10, Year 1085)]"));
        Assert.Contains("[Varcheg, 1085.03.10 19.41 (Autumn 10, Year 1085)] Evening.", history);   // a new minute
        Assert.Contains("[the road, 1085.03.10 19.41 (Autumn 10, Year 1085)] Ride.", history);     // a new place
        Assert.Contains("Mead?", history);
        Assert.Contains("(Within my own mind: My gear is changed.)", history);
        // The record keeps every stamp.
        Assert.All(memory.RecentTurns, t => Assert.False(string.IsNullOrWhiteSpace(t.CalradiaTime)));
    }

    private static int Count(string text, string part)
    {
        int n = 0, at = 0;
        while ((at = text.IndexOf(part, at, StringComparison.Ordinal)) >= 0) { n++; at += part.Length; }
        return n;
    }

    // ------------------------------ the voice keys ------------------------------

    private static NpcMemory FiveVoicedReplies()
    {
        var memory = new NpcMemory();
        for (int i = 0; i < 5; i++)
            memory.AddTurn(Spoken("Varcheg", "1085.03.10 13." + (10 + i) + " (Autumn 10, Year 1085)",
                "Line " + i, "Reply " + i + "\n[voice: tone " + i + "]"));
        return memory;
    }

    [Fact]
    public void OnlyTheNewestRepliesKeepTheirVoiceKey_WhileTheVoiceAsksForOne()
    {
        var memory = FiveVoicedReplies();
        var history = History(new PromptBuilder().Build(Persona(voiceTakesMood: true), memory, "", "Vulgrim", "Now?"));

        Assert.DoesNotContain("[voice: tone 0]", history);
        Assert.DoesNotContain("[voice: tone 1]", history);
        Assert.Contains("[voice: tone 2]", history);
        Assert.Contains("[voice: tone 3]", history);
        Assert.Contains("[voice: tone 4]", history);
        Assert.Equal(PromptBuilder.VoiceKeysTaught, Count(history, "[voice:"));
        Assert.Contains("Reply 0", history);                                    // the words themselves stay
        Assert.Contains("[voice: tone 0]", memory.RecentTurns[0].NpcLine);     // and the record is untouched
    }

    [Fact]
    public void NoVoiceKeyRides_OnPaper_OrWhenNoVoiceFollowsAMood()
    {
        var memory = FiveVoicedReplies();
        var onPaper = new PromptBuilder().BuildInnerPrompt(Persona(voiceTakesMood: true, onPaper: true), memory, "",
            "Vulgrim", PromptBuilder.ComposeLetterLine("Vulgrim"));
        var silent = new PromptBuilder().Build(Persona(voiceTakesMood: false), memory, "", "Vulgrim", "Now?");

        Assert.DoesNotContain("[voice:", History(onPaper));
        Assert.DoesNotContain("[voice:", History(silent));
        Assert.Contains("Reply 4", History(onPaper));
    }

    // ------------------------------ the guidance on working words ------------------------------

    [Fact]
    public void TheLengthDriftLine_StillLeadsWithWhy_AndBreaksTheMirror()
    {
        Assert.Contains("SPOKEN", PromptBuilder.NoLengthDriftGuidance);
        Assert.Contains("waiting to answer", PromptBuilder.NoLengthDriftGuidance);
        Assert.Contains("how long I spoke last is no measure of how long I speak now", PromptBuilder.NoLengthDriftGuidance);
        Assert.Contains("short again straight after", PromptBuilder.NoLengthDriftGuidance);
    }

    // ------------------------------ the reckoning ------------------------------

    private static NightRecord Night(double day, NightKind kind, string id) => new()
    {
        Id = id, GameDay = day, WifeId = "wife", WifeName = "Sibylla", Kind = kind, PlaceName = "the town of Onira",
    };

    [Fact]
    public void TheReckoning_IsLeftOut_WhileEveryNightOfTheMonthStandsInTheRoll()
    {
        var nights = new[] { Night(100, NightKind.Together, "a"), Night(101, NightKind.Together, "b") };
        var marks = nights.Select(NightMark.From).ToList();

        var roll = NightText.BuildRoll(nights, today: 102, marks: marks);
        Assert.DoesNotContain("Reckoning the last", roll);
        Assert.True(NightText.NothingBeyondTheRoll(marks, 100, 102));

        // A night older than the roll: now the sum knows something the lines do not.
        marks.Insert(0, new NightMark { GameDay = 85, Kind = NightKind.Together, WifeId = "wife" });
        Assert.Contains("Reckoning the last", NightText.BuildRoll(nights, today: 102, marks: marks));

        // And the talk of his other nights is always the reckoning's own.
        var elsewhere = nights.Select(NightMark.From).ToList();
        elsewhere.Add(new NightMark { GameDay = 101, Kind = NightKind.Elsewhere, OtherName = "Ira", WifeId = "wife" });
        Assert.False(NightText.NothingBeyondTheRoll(elsewhere, 100, 102));
    }

    // ------------------------------ mangled punctuation ------------------------------

    [Fact]
    public void Mojibake_PutsTheDashesAndQuotesBack_AndLeavesCleanTextAlone()
    {
        var mangled = "- My words carry the feel of these old feudal days â€” a light colour " +
                      "â€“ itâ€™s â€œsoâ€\u009dâ€¦";
        Assert.Equal("- My words carry the feel of these old feudal days — a light colour – it’s “so”…",
            Mojibake.Repair(mangled));

        const string clean = "Already fine — Сибила’s “words”.";
        Assert.Same(clean, Mojibake.Repair(clean));
        Assert.Equal(string.Empty, Mojibake.Repair(null));
    }

    // ------------------------------ the CLI wall ------------------------------

    [Fact]
    public void TheLengthWall_IsOneShortLine()
    {
        var system = ClaudeCliShape.BuildSystem(new[] { ChatMessage.System("I am Gafnir.") }, null, true, 1200);
        Assert.Contains("about 720 words — a WALL, never a target.", system);
        Assert.DoesNotContain("settled above", system);
    }
}
