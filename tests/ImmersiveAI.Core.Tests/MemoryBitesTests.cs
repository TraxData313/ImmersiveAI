using System.Linq;
using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// THE BITES (2026.08.27, Anton's design) — the deep memory's plain facts as small keyed notes she
/// edits one at a time, with one key reserved for prose.
/// </summary>
public class MemoryBitesTests
{
    [Fact]
    public void OneSubjectNeverBecomesFourNotes()
    {
        // Keys arrive from a model and WILL vary in case, spacing and punctuation. If they were
        // filed as written, "Ahil" and "ahil " would be two notes each holding half the truth.
        Assert.Equal("ahil", MemoryBites.CanonicalKey("Ahil"));
        Assert.Equal("ahil", MemoryBites.CanonicalKey("  ahil  "));
        Assert.Equal("ahil", MemoryBites.CanonicalKey("\"Ahil\":"));
        Assert.Equal("captain", MemoryBites.CanonicalKey("The Captain"));
        Assert.Equal("my wage", MemoryBites.CanonicalKey("my   wage"));
        Assert.Equal(string.Empty, MemoryBites.CanonicalKey(null));

        var bites = new Dictionary<string, string>();
        MemoryBites.Set(bites, "Ahil", "He hired me at Dunglanys.");
        MemoryBites.Set(bites, " ahil ", "He equips me well and listens to my counsel.");
        Assert.Single(bites);
        Assert.Equal("He equips me well and listens to my counsel.", bites["ahil"]);
    }

    [Fact]
    public void ANoteIsALine_NotAParagraph()
    {
        var bites = new Dictionary<string, string>();
        var longNote = new string('x', 200) + ". " + new string('y', 400) + ".";
        MemoryBites.Set(bites, "the war", longNote);   // filed under "war" — the article is stripped

        Assert.True(bites["war"].Length <= MemoryBites.MaxBiteChars);
        // Cut back to a finished sentence rather than mid-word.
        Assert.EndsWith(".", bites["war"]);

        // Newlines never survive: a note is one line, and the section parser reads by lines.
        MemoryBites.Set(bites, "kin", "Her mother\nlives\r\nstill.");
        Assert.DoesNotContain("\n", bites["kin"]);
    }

    [Fact]
    public void AnEmptyNoteDropsTheKey_BecauseThatIsARealEdit()
    {
        var bites = new Dictionary<string, string> { ["ahil"] = "He hired me." };
        MemoryBites.Set(bites, "ahil", "   ");
        Assert.Empty(bites);
    }

    [Fact]
    public void TheShelfHasAnEnd_AndSaysSoRatherThanSilentlyRefusing()
    {
        var bites = new Dictionary<string, string>();
        for (int i = 0; i < MemoryBites.MaxBites; i++)
            MemoryBites.Set(bites, "thing " + i, "a fact.");

        Assert.True(MemoryBites.IsFull(bites));
        Assert.Equal(string.Empty, MemoryBites.Set(bites, "one more", "a fact."));
        Assert.Equal(MemoryBites.MaxBites, bites.Count);

        // But rewriting a word she already keeps is always allowed — that is not growth.
        Assert.NotEqual(string.Empty, MemoryBites.Set(bites, "thing 0", "a changed fact."));
        Assert.Equal("a changed fact.", bites["thing 0"]);
    }

    [Fact]
    public void TheSectionIsADelta_SoUnnamedNotesSurviveUntouched()
    {
        // The whole point: what she does not mention keeps standing, word for word. That is what
        // the rewritten-whole page could never promise.
        var bites = new Dictionary<string, string>
        {
            ["ahil"] = "He hired me at Dunglanys.",
            ["my wage"] = "34 denars a day.",
            ["my bow"] = "A Woodland Yew Bow, the best I have carried.",
        };

        int changed = MemoryBites.ApplySection(bites,
            "ahil: He equips me well and listens to my counsel.\n-my bow\nseordas: We took the village; I kept to the wounded.");

        Assert.Equal(3, changed);
        Assert.Equal("He equips me well and listens to my counsel.", bites["ahil"]);
        Assert.Equal("34 denars a day.", bites["my wage"]);      // never named, never touched
        Assert.False(bites.ContainsKey("my bow"));                 // struck out
        Assert.Contains("kept to the wounded", bites["seordas"]);  // newly written
    }

    [Fact]
    public void TheSectionIsReadLeniently_BecauseAModelDressesADictionary()
    {
        var bites = new Dictionary<string, string>();
        MemoryBites.ApplySection(bites,
            "{\n  \"ahil\": \"He hired me at Dunglanys.\",\n  \"my wage\": \"34 denars a day.\"\n}");

        Assert.Equal(2, bites.Count);
        Assert.Equal("He hired me at Dunglanys.", bites["ahil"]);
        Assert.Equal("34 denars a day.", bites["my wage"]);

        // Bullets and stray lines: the bullet is stripped, the line with no colon is skipped
        // rather than filed under a nonsense key.
        var more = new Dictionary<string, string>();
        MemoryBites.ApplySection(more, "- kin: Her mother lives still.\nI think that is all for now\n");
        Assert.Single(more);
        Assert.True(more.ContainsKey("kin"));
    }

    [Fact]
    public void TheFeelingIsANoteToo_AndFitsWholeUnderTheCap()
    {
        // The reserved prose key lasted one afternoon (2026.08.27 evening, Anton: "keep only the
        // short key-value pair memory, that is the new variant"). How things stand between them is
        // now a note like any other — so it must FILE rather than be skipped, and the cap must be
        // roomy enough that her actual voice survives instead of being clipped to a ledger line.
        var bites = new Dictionary<string, string>();
        var felt = "There is a tenderness growing between us, tempered by danger and the road; "
                 + "I will not name it too quickly.";
        MemoryBites.ApplySection(bites, "how things stand between us: " + felt);

        Assert.Single(bites);
        Assert.Equal(felt, bites["how things stand between us"]);
        Assert.Equal(felt, MemoryBites.Tidy(felt));      // whole, not clipped
    }

    [Fact]
    public void ApplyCompression_TakesTheNotesAsEdits_AndLetsTheOldPageGo()
    {
        var memory = new NpcMemory { Summary = "The long page I used to carry." };
        memory.Bites["ahil"] = "He hired me.";
        memory.Bites["my wage"] = "34 denars a day.";
        memory.AddTurn(new ConversationTurn { PlayerLine = "Здравей.", NpcLine = "И на теб." });

        memory.ApplyCompression(null, 1, "ahil: He equips me well.");

        // The named note changed, the unnamed one stands word for word, and the page is gone.
        Assert.Equal("He equips me well.", memory.Bites["ahil"]);
        Assert.Equal("34 denars a day.", memory.Bites["my wage"]);
        Assert.Equal(string.Empty, memory.Summary);
        Assert.Empty(memory.RecentTurns);
    }

    [Fact]
    public void ApplyCompression_KeepsThePageWhenSheWroteNothingAtAll()
    {
        // A stumbling backend must never be able to erase a soul's whole memory and leave nothing
        // in its place: clearing the page is keyed on her having notes to stand on.
        var memory = new NpcMemory { Summary = "Everything I hold of him." };
        memory.ApplyCompression(null, 0, null);
        Assert.Equal("Everything I hold of him.", memory.Summary);

        // And an old-shape reply that still carries a page is taken as one rather than thrown away.
        var legacy = new NpcMemory { Summary = "old" };
        legacy.ApplyCompression("a page written the old way", 0, null);
        Assert.Equal("a page written the old way", legacy.Summary);
    }

    [Fact]
    public void DeepMemoryText_PrefersTheNotes_AndFallsBackToThePage()
    {
        var page = new NpcMemory { Summary = "The old page." };
        Assert.Equal("The old page.", page.DeepMemoryText());
        Assert.True(page.HasDeepMemory);

        var noted = new NpcMemory { Summary = "The old page.", Bites = { ["ahil"] = "He hired me." } };
        Assert.Contains("ahil: He hired me.", noted.DeepMemoryText());
        Assert.DoesNotContain("The old page.", noted.DeepMemoryText());

        Assert.False(new NpcMemory().HasDeepMemory);
    }

    [Fact]
    public void AnOldSaveNeedsNoMigration_ItsPageSimplyBecomesTheProse()
    {
        // The whole reason the prose lives in Summary: every memories.json ever written already
        // holds one, and it is exactly the half we still want written as prose.
        var old = new NpcMemory { Summary = "I am Rhia the Healer, Battanian by birth and by temper." };
        Assert.True(MemoryBites.NeedsSeeding(old));
        Assert.Empty(old.Bites);

        var started = new NpcMemory { Summary = "…", Bites = { ["ahil"] = "He hired me." } };
        Assert.False(MemoryBites.NeedsSeeding(started));
    }

    [Fact]
    public void AnOldRichMemoryIsInvitedToLiftItsFactsOut_Once()
    {
        // Anton, 2026.08.27: the folk wives must not lose their memories in the turning. Nothing is
        // migrated by machine — only she knows what deserves a word — so the first reflection after
        // the change invites her to lift the plain facts out. Her page keeps all the rest.
        var old = new NpcMemory
        {
            NpcName = "Rhia the Healer",
            Summary = "I am Rhia the Healer. Ahil hired me at Dunglanys for 400 denars; my wage is 34 a day.",
        };
        old.AddTurn(new ConversationTurn { PlayerLine = "Здравей.", NpcLine = "И на теб." });

        var prompt = string.Join("\n", MemoryCompressor
            .BuildCompressionRequest(old, old.RecentTurns).Select(m => m.Content));
        Assert.Contains("set the whole of it down as notes", prompt);
        Assert.Contains("let the page go", prompt);
        // The feeling must cross over with the facts, or the turning costs her her voice.
        Assert.Contains("how things truly stand between us", prompt);

        // Once she keeps notes the invitation is gone and her shelf is simply shown back to her.
        old.Bites["ahil"] = "He hired me at Dunglanys.";
        var second = string.Join("\n", MemoryCompressor
            .BuildCompressionRequest(old, old.RecentTurns).Select(m => m.Content));
        Assert.DoesNotContain("set the whole of it down as notes", second);
        Assert.Contains("these STAND unless I say otherwise", second);
        Assert.Contains("ahil: He hired me at Dunglanys.", second);
    }

    [Fact]
    public void TheBitesSectionIsParsedBesideTheProse()
    {
        var parsed = MemoryCompressor.ParseResponse(
            "BITES:\nahil: He equips me well.\n-my bow\n\nSUMMARY:\nHe is my captain, and I trust him.");

        Assert.Equal("He is my captain, and I trust him.", parsed.Summary);
        Assert.Contains("ahil: He equips me well.", parsed.Bites);
        Assert.DoesNotContain("He is my captain", parsed.Bites);   // the sections bound each other

        // No BITES section at all is the common, correct case: nothing changed.
        var quiet = MemoryCompressor.ParseResponse("SUMMARY:\nHe is my captain.");
        Assert.Null(quiet.Bites);
        Assert.Equal("He is my captain.", quiet.Summary);
    }

    [Fact]
    public void RenderIsStableAndSkipsEmptyNotes()
    {
        var bites = new Dictionary<string, string>
        {
            ["my wage"] = "34 denars a day.",
            ["ahil"] = "He hired me.",
            ["nothing"] = "   ",
        };
        var rendered = MemoryBites.Render(bites);

        Assert.Equal("- ahil: He hired me.\n- my wage: 34 denars a day.", rendered.Replace("\r\n", "\n"));
        Assert.DoesNotContain("nothing", rendered);
        Assert.Equal(string.Empty, MemoryBites.Render(new Dictionary<string, string>()));
    }
}
