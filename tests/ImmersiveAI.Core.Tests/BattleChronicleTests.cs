using ImmersiveAI.Core.Battles;

namespace ImmersiveAI.Core.Tests;

public class BattleChronicleTests
{
    private static BattleRecord Ortysia() => new BattleRecord
    {
        Id = "d0123",
        DateText = "Summer 12, Year 1084",
        TimeOfDay = "early afternoon",
        GameDay = 123.6,
        PlaceName = "Ortysia",
        Kind = BattleRecord.Kinds.Field,
        PlayerAttacked = true,
        Outcome = BattleRecord.Outcomes.Victory,
        EnemyName = "the war-band of Akrum of the Khuzaits",
        Ours = new BattleSideStats { Total = 62, Foot = 30, Archers = 14, Horse = 12, HorseArchers = 6, TierAverage = 2.7, Fallen = 8, Wounded = 10 },
        Theirs = new BattleSideStats { Total = 190, Foot = 120, Archers = 50, Horse = 20, TierAverage = 1.9, Fallen = 60, Wounded = 45, Routed = 30 },
        PrisonersTaken = 40,
        CaptivesFreed = 5,
        GoldGained = 350,
        RenownGained = 12,
        InfluenceGained = 6,
        Participants =
        {
            new BattleParticipant { HeroId = "player", Name = "Vulgrim", IsPlayer = true, Downs = 11, State = BattleParticipant.States.Hale },
            new BattleParticipant { HeroId = "comp_thyrsif", Name = "Thyrsif", Downs = 4, State = BattleParticipant.States.Wounded },
            new BattleParticipant { HeroId = "lord_9_2", Name = "Yngvald", Downs = 3, State = BattleParticipant.States.Hale },
        },
    };

    [Fact]
    public void ForgeTitle_GrandVictoryOverLongOdds()
    {
        var r = Ortysia();
        var title = BattleText.ForgeTitle(r);
        Assert.Equal("The Grand Victory near Ortysia, over Thrice Our Number", title);
    }

    [Fact]
    public void ForgeTitle_KnowsTheArenas()
    {
        var r = Ortysia();

        r.Kind = BattleRecord.Kinds.Siege; r.PlaceName = "Varcheg"; r.PlayerAttacked = true;
        Assert.Equal("The Storming of Varcheg", BattleText.ForgeTitle(r));

        r.PlayerAttacked = false;
        Assert.StartsWith("The Holding of Varcheg", BattleText.ForgeTitle(r));

        r.Outcome = BattleRecord.Outcomes.Defeat;
        Assert.Equal("The Fall of Varcheg", BattleText.ForgeTitle(r));

        r.Kind = BattleRecord.Kinds.Sea; r.Outcome = BattleRecord.Outcomes.Victory; r.PlaceName = "Ostican";
        var sea = BattleText.ForgeTitle(r);
        Assert.Contains("Sea-Victory off Ostican", sea);

        r.Kind = BattleRecord.Kinds.Hideout; r.PlaceName = "Ortysia";
        Assert.Equal("The Rooting-Out of the Den near Ortysia", BattleText.ForgeTitle(r));
    }

    [Fact]
    public void ForgeTitle_SmallAndBitterDaysStayHonest()
    {
        var r = Ortysia();
        r.Ours = new BattleSideStats { Total = 8, Foot = 8, TierAverage = 1.2 };
        r.Theirs = new BattleSideStats { Total = 10, Foot = 10, TierAverage = 1.0 };
        Assert.Equal("The Skirmish Won near Ortysia", BattleText.ForgeTitle(r));

        r.Outcome = BattleRecord.Outcomes.Defeat;
        Assert.Equal("The Skirmish Lost near Ortysia", BattleText.ForgeTitle(r));

        r.Ours.Total = 100; r.Theirs.Total = 210;
        Assert.Equal("The Bitter Day near Ortysia, against Twice Our Number", BattleText.ForgeTitle(r));
    }

    [Fact]
    public void ShortTale_CarriesNumbersFoeAndChains()
    {
        var r = Ortysia();
        var tale = BattleText.ShortTale(r);
        Assert.Contains("With 62 we fell upon the war-band of Akrum", tale);
        Assert.Contains("190 strong", tale);
        Assert.Contains("near Ortysia", tale);
        Assert.Contains("broke them", tale);
        Assert.Contains("18 of ours fell or bled", tale);
        Assert.Contains("40 of theirs were led away in chains", tale);
        Assert.Contains("5 souls they held were set free", tale);
    }

    [Fact]
    public void BeatLine_SpeaksHerOwnHandAndThePlayers()
    {
        var r = Ortysia();
        r.Title = BattleText.ForgeTitle(r);
        var me = r.ParticipantById("comp_thyrsif")!;
        var beat = BattleText.BeatLine(r, me, "Vulgrim");

        Assert.True(BattleText.IsBattleBeat(beat));
        Assert.Contains("By my own hand I struck down 4", beat);
        Assert.Contains("Vulgrim felled 11", beat);
        Assert.Contains("wounded", beat);
        Assert.Contains("The chronicle keeps it as 'The Grand Victory near Ortysia", beat);
    }

    [Fact]
    public void BeatLine_HonestWhenNoTallyWasKept_AndWhenThePlayerFellCaptive()
    {
        var r = Ortysia();
        r.Title = BattleText.ForgeTitle(r);
        r.Outcome = BattleRecord.Outcomes.Defeat;
        var player = r.Player!;
        player.Downs = -1;
        player.State = BattleParticipant.States.Captured;
        var me = r.ParticipantById("lord_9_2")!;
        me.Downs = -1;

        var beat = BattleText.BeatLine(r, me, "Vulgrim");
        Assert.Contains("no tally was kept", beat);
        Assert.DoesNotContain("felled", beat);
        Assert.Contains("Vulgrim was taken captive", beat);
        Assert.Contains("were broken", beat);
    }

    [Fact]
    public void FullAccount_TellsTheWholeDayCompactly()
    {
        var r = Ortysia();
        r.Title = BattleText.ForgeTitle(r);
        r.Loot = BattleLoot.Summarize(new[]
        {
            new BattleLootItem("Fine Steel Sword", 2, 1200, "weapons"),
            new BattleLootItem("Hardwood", 18, 15, "goods"),
            new BattleLootItem("Northern Round Shield", 8, 90, "shields"),
            new BattleLootItem("Mail Hauberk", 3, 800, "armor"),
            new BattleLootItem("Steppe Horse", 6, 350, "horses & beasts"),
            new BattleLootItem("Arrows", 12, 40, "bows & bolts"),
        });

        var account = BattleText.FullAccount(r);
        Assert.Contains("'The Grand Victory near Ortysia", account);
        Assert.Contains("Summer 12, Year 1084", account);
        Assert.Contains("the attack was ours", account);
        Assert.Contains("Ours: 62 — 30 foot, 14 bows, 12 horse, 6 horse-archers; seasoned hands (about 2.7 of 6)", account);
        Assert.Contains("The day was ours.", account);
        Assert.Contains("8 fell, 10 were wounded", account);
        Assert.Contains("led away 40 prisoners", account);
        Assert.Contains("struck the chains from 5 souls", account);
        Assert.Contains("Vulgrim struck down 11; Thyrsif struck down 4; Yngvald struck down 3", account);
        Assert.Contains("Thyrsif came out of it wounded", account);
        Assert.Contains("Spoils worth some", account);
        Assert.Contains("Fine Steel Sword (1,200 each, ×2)", account);
        Assert.Contains("Hardwood (×18)", account);
        Assert.Contains("350 denars of plunder", account);
        Assert.Contains("renown some 12", account);
        Assert.Contains("influence 6", account);
        // Compact: a telling, not a ledger sprawl.
        Assert.True(account.Split('\n').Length <= 14);
    }

    [Fact]
    public void Loot_Summarize_TotalsGroupsAndTops()
    {
        var loot = BattleLoot.Summarize(new[]
        {
            new BattleLootItem("Sword", 2, 100, "weapons"),
            new BattleLootItem("Axe", 1, 50, "weapons"),
            new BattleLootItem("Grain", 30, 5, "goods"),
            new BattleLootItem("  ", 5, 5, "goods"),          // nameless lines are dropped
            new BattleLootItem("Crown", 1, 5000, "goods"),
        }, topCount: 2);

        Assert.Equal(2 * 100 + 50 + 30 * 5 + 5000, loot.TotalValue);
        Assert.Equal(2, loot.Categories.Count);
        Assert.Equal("weapons", loot.Categories[0].Category);
        Assert.Equal(3, loot.Categories[0].Count);
        Assert.Equal(31, loot.Categories[1].Count);
        Assert.Equal("Crown", loot.TopValued[0].Name);
        Assert.Equal(2, loot.TopValued.Count);
        Assert.Equal("Grain", loot.TopNumerous[0].Name);
    }

    [Fact]
    public void SituationBlock_ListsOldTitlesAndTellsTheLatestWhole()
    {
        var older = Ortysia(); older.Id = "d0100"; older.GameDay = 100; older.Title = "The Skirmish Won near Odrimir";
        older.ShortTale = BattleText.ShortTale(older);
        var latest = Ortysia(); latest.Title = BattleText.ForgeTitle(latest);

        var block = BattleText.SituationBlock(new[] { older, latest }, "Vulgrim", mentionRecall: true);
        Assert.Contains("Battles I have lived through at Vulgrim's side", block);
        Assert.Contains("The Skirmish Won near Odrimir", block);
        Assert.Contains("full tale still fresh", block);
        Assert.Contains("The day was ours.", block);
        Assert.Contains("call back whole, by its name", block);
    }

    [Fact]
    public void SituationBlock_FoldsTheDeepPastIntoACount()
    {
        var records = new List<BattleRecord>();
        for (int i = 0; i < 10; i++)
        {
            var r = Ortysia();
            r.Id = $"d{100 + i:0000}";
            r.GameDay = 100 + i;
            r.Title = $"The Victory near Ortysia {i}";
            records.Add(r);
        }
        var block = BattleText.SituationBlock(records, "Vulgrim", mentionRecall: false, maxListed: 4);
        Assert.Contains("6 earlier battles besides", block);
        Assert.DoesNotContain("call back whole", block);
    }

    [Fact]
    public void Ledger_SavesLoadsAndMintsDayScopedIds()
    {
        var folder = Path.Combine(Path.GetTempPath(), "iai_battles_" + Guid.NewGuid().ToString("N"));
        try
        {
            var ledger = new BattleLedger(folder);
            Assert.Equal("d0123", ledger.NextId(123.6));

            var r = Ortysia();
            r.Title = BattleText.ForgeTitle(r);
            r.ShortTale = BattleText.ShortTale(r);
            ledger.Save(r);
            Assert.Equal("d0123-2", ledger.NextId(123.9));

            // Enrich and rewrite: same id, same file, no duplicate.
            r.GoldGained = 999;
            ledger.Save(r);

            var loaded = BattleLedger.LoadFrom(folder);
            Assert.Single(loaded.Records);
            Assert.Equal(999, loaded.Records[0].GoldGained);
            Assert.Equal("The Grand Victory near Ortysia, over Thrice Our Number", loaded.Records[0].Title);
            Assert.Single(Directory.GetFiles(folder, "*.json"));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Ledger_FindsByLooseName_ScopedToWhatTheAskerLived()
    {
        var ledger = new BattleLedger(string.Empty);
        var a = Ortysia(); a.Id = "d0100"; a.GameDay = 100; a.Kind = BattleRecord.Kinds.Siege; a.PlaceName = "Varcheg";
        a.Title = BattleText.ForgeTitle(a); // The Storming of Varcheg
        var b = Ortysia(); b.Id = "d0123"; b.Title = BattleText.ForgeTitle(b);
        var c = Ortysia(); c.Id = "d0150"; c.GameDay = 150; c.PlaceName = "Ostican"; c.Kind = BattleRecord.Kinds.Sea;
        c.Title = BattleText.ForgeTitle(c);
        c.Participants.RemoveAt(1); // Thyrsif was not aboard for the sea-fight
        ledger.Save(a); ledger.Save(b); ledger.Save(c);

        Assert.Equal("d0100", ledger.Find("the storming of Varcheg")?.Id);
        Assert.Equal("d0123", ledger.Find("ortysia", "comp_thyrsif")?.Id);
        Assert.Equal("d0150", ledger.Find("the sea fight")?.Id);
        Assert.Equal("d0150", ledger.Find("our last battle")?.Id);
        Assert.Equal("d0123", ledger.Find("last", "comp_thyrsif")?.Id);   // her last is not the sea-fight
        Assert.Null(ledger.Find("ostican", "comp_thyrsif"));              // she was not there
        Assert.Null(ledger.Find("gibberish nowhere"));

        Assert.Equal(2, ledger.SharedWith("comp_thyrsif").Count);
        Assert.Equal("d0123", ledger.LatestSharedWith("comp_thyrsif")?.Id);
    }

    [Fact]
    public void Titles_MakeCleanFileSlugs()
    {
        var slug = BattleLedger.Slug("The Grand Victory near Ortysia, over Thrice Our Number");
        Assert.StartsWith("the-grand-victory-near-ortysia-over", slug);
        Assert.True(slug.Length <= 40);
        Assert.DoesNotContain("--", slug);
        Assert.Equal(string.Empty, BattleLedger.Slug("   "));
    }
}
