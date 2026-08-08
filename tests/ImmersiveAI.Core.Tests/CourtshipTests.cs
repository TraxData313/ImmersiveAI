using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class CourtshipTests
{
    // ------------------------- the road's rails -------------------------

    private static CourtshipRoad.StepFacts Facts(
        CourtshipStage stage,
        int relation = 100,
        double daysSinceStep = double.PositiveInfinity,
        int playerTier = 6,
        int herTier = 1,
        int slack = 2,
        bool weighed = true,
        int openMisgivings = 0,
        double daysBetrothed = -1,
        int minBetrothalDays = 3) => new()
        {
            Stage = stage,
            Relation = relation,
            DaysSinceForwardStep = daysSinceStep,
            PlayerClanTier = playerTier,
            HerStationTier = herTier,
            CharmSlack = slack,
            MisgivingsWeighed = weighed,
            OpenMisgivings = openMisgivings,
            DaysBetrothed = daysBetrothed,
            MinBetrothalDays = minBetrothalDays,
        };

    [Fact]
    public void TheHeartIsFree_ButOnlyWhereItTrulyStands()
    {
        // Warmth needs only "not an enemy"; deeper steps need a real bond.
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.None, relation: 0)));
        Assert.Equal(CourtshipRoad.StepVerdict.HeartNotThere, CourtshipRoad.JudgeForward(Facts(CourtshipStage.None, relation: -1)));
        Assert.Equal(CourtshipRoad.StepVerdict.HeartNotThere, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Warmth, relation: 19)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Warmth, relation: 20)));
        Assert.Equal(CourtshipRoad.StepVerdict.HeartNotThere, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, relation: 39)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, relation: 40)));
    }

    [Fact]
    public void NoOneSprintsTheRoadInOneEvening()
    {
        // A forward step taken this very day blocks the next until a day has passed —
        // but the first step (None → Warmth) is free.
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.None, daysSinceStep: 0)));
        Assert.Equal(CourtshipRoad.StepVerdict.TooSoon, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Warmth, daysSinceStep: 0.4)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Warmth, daysSinceStep: 1.0)));
        Assert.Equal(CourtshipRoad.StepVerdict.TooSoon, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, daysSinceStep: 0.9)));
    }

    [Fact]
    public void TheStationGateBindsTheHand_NotTheHeart()
    {
        // An emperor's daughter (tier 6): a tier-0 player may win Warmth and even Devotion —
        // but Ready is walled until tier 4 (6 − slack 2). The exact walkthrough of the brief.
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.None, playerTier: 0, herTier: 6)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Warmth, playerTier: 0, herTier: 6)));
        Assert.Equal(CourtshipRoad.StepVerdict.StationTooFar, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, playerTier: 3, herTier: 6)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, playerTier: 4, herTier: 6)));
        // And the gate is re-run at both seals.
        Assert.Equal(CourtshipRoad.StepVerdict.StationTooFar, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Ready, playerTier: 3, herTier: 6)));
        Assert.Equal(CourtshipRoad.StepVerdict.StationTooFar, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, playerTier: 3, herTier: 6, daysBetrothed: 10)));
    }

    [Fact]
    public void AWandererBride_HasNoStationWall()
    {
        // Sibylla's walkthrough: station tier 1, slack 2 → required tier 0. A fresh player may wed her.
        Assert.Equal(1, CourtshipRoad.StationTier(clanless: true, rulingClan: false, clanTier: 0));
        Assert.Equal(0, CourtshipRoad.RequiredTier(1, 2));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, playerTier: 0, herTier: 1)));
    }

    [Fact]
    public void StationTiers_MapAsTheBriefAsks()
    {
        Assert.Equal(6, CourtshipRoad.StationTier(false, rulingClan: true, clanTier: 4));
        Assert.Equal(5, CourtshipRoad.StationTier(false, false, 6)); // great house, walled to 5
        Assert.Equal(5, CourtshipRoad.StationTier(false, false, 5));
        Assert.Equal(3, CourtshipRoad.StationTier(false, false, 3));
        Assert.Equal(2, CourtshipRoad.StationTier(false, false, 1)); // minor noble floors at 2
    }

    [Fact]
    public void HerMisgivings_GateReadinessAndTheBetrothal_ButNotTheWedding()
    {
        // A heart that never weighed itself cannot say "no questions remain"…
        Assert.Equal(CourtshipRoad.StepVerdict.MisgivingsUnweighed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, weighed: false)));
        Assert.Equal(CourtshipRoad.StepVerdict.MisgivingsUnweighed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Ready, weighed: false)));
        // …and one that set something down waits until SHE lays it to rest.
        Assert.Equal(CourtshipRoad.StepVerdict.MisgivingsRemain, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, openMisgivings: 2)));
        Assert.Equal(CourtshipRoad.StepVerdict.MisgivingsRemain, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Ready, openMisgivings: 1)));
        // A weighed-and-clear heart walks on.
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion)));
        // The promise was proven when it was given — the wedding lay re-checks neither.
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, weighed: false, openMisgivings: 3, daysBetrothed: 10)));
    }

    [Fact]
    public void TheTrothMustSeason_BeforeTheWeddingLay()
    {
        Assert.Equal(CourtshipRoad.StepVerdict.TrothTooFresh, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, daysBetrothed: 1, minBetrothalDays: 3)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, daysBetrothed: 3, minBetrothalDays: 3)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, daysBetrothed: 0, minBetrothalDays: 0)));
        Assert.Equal(CourtshipRoad.StepVerdict.NoRoadFurther, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Wed)));
    }

    // ------------------------- her misgivings: her own hand's operations -------------------------

    [Fact]
    public void SetDown_TakesLines_ShedsBullets_DedupesAndCaps()
    {
        var list = new List<CourtshipMisgiving>();
        int added = CourtshipMisgivings.SetDown(list,
            "- I fear the road would own him more than I would.\n" +
            "2) His purse is lighter than his promises.\n" +
            "I fear the road would own him more than I would.\n" +   // duplicate
            "“What of my Da, left alone?”");
        Assert.Equal(3, added);
        Assert.Equal(3, CourtshipMisgivings.TotalCount(list));
        Assert.Equal("I fear the road would own him more than I would.", list[0].Text);
        Assert.Equal("What of my Da, left alone?", list[2].Text);

        // The cap binds what stands OPEN at once — five, never more.
        CourtshipMisgivings.SetDown(list, "A fourth worry; A fifth worry; A sixth that must not land");
        Assert.Equal(CourtshipMisgivings.MaxMisgivings, CourtshipMisgivings.OpenCount(list));
    }

    [Fact]
    public void TheListLives_SettledOnesNeverBlockANewDoubt()
    {
        // Five open = full…
        var list = new List<CourtshipMisgiving>();
        CourtshipMisgivings.SetDown(list, "one; two; three; four; five");
        Assert.Equal(0, CourtshipMisgivings.SetDown(list, "a sixth, refused"));

        // …but a settled one frees the room: a new true doubt may be born mid-talk.
        CourtshipMisgivings.Settle(list, "three", "answered");
        Assert.Equal(1, CourtshipMisgivings.SetDown(list, "a sixth, now welcome"));
        Assert.Equal(5, CourtshipMisgivings.OpenCount(list));
        Assert.Equal(6, CourtshipMisgivings.TotalCount(list));
    }

    [Fact]
    public void Release_StrikesOneOutEntirely_StandingFirst()
    {
        var list = new List<CourtshipMisgiving>();
        CourtshipMisgivings.SetDown(list, "His purse is lighter than his promises.\nWhat of my Da, left alone?");
        CourtshipMisgivings.Settle(list, "his purse is lighter than his promises", "his word held");

        // A standing one struck out is simply gone — not settled, not remembered.
        var released = CourtshipMisgivings.Release(list, "what of my Da left alone");
        Assert.NotNull(released);
        Assert.Equal(1, CourtshipMisgivings.TotalCount(list));
        Assert.Equal(0, CourtshipMisgivings.OpenCount(list));

        // A settled one can be struck out too; a stranger's words strike nothing.
        Assert.Null(CourtshipMisgivings.Release(list, "the weather over the mountains"));
        Assert.NotNull(CourtshipMisgivings.Release(list, "his purse is lighter than his promises"));
        Assert.Empty(list);
    }

    [Fact]
    public void History_NeverOutgrowsItsRoom_TheOldestSettledFade()
    {
        // Eight settled already carried, two standing…
        var list = new List<CourtshipMisgiving>();
        for (int i = 0; i < 8; i++)
            list.Add(new CourtshipMisgiving { Text = $"an old settled worry number {i}", Settled = true });
        CourtshipMisgivings.SetDown(list, "a standing worry; another standing worry");
        Assert.Equal(10, CourtshipMisgivings.TotalCount(list));

        // …a new doubt still lands, and the OLDEST settled one fades to make room.
        Assert.Equal(1, CourtshipMisgivings.SetDown(list, "a fresh doubt, mid-talk"));
        Assert.Equal(CourtshipMisgivings.MaxCarried, CourtshipMisgivings.TotalCount(list));
        Assert.DoesNotContain(list, m => m.Text == "an old settled worry number 0");
        Assert.Contains(list, m => m.Text == "a fresh doubt, mid-talk");
        Assert.Equal(3, CourtshipMisgivings.OpenCount(list)); // nothing standing ever fades
    }

    [Fact]
    public void None_IsAnHonestAnswer_NotAMisgiving()
    {
        Assert.True(CourtshipMisgivings.IsNone(null));
        Assert.True(CourtshipMisgivings.IsNone("none"));
        Assert.True(CourtshipMisgivings.IsNone(" None. "));
        Assert.True(CourtshipMisgivings.IsNone("my heart is clear"));
        Assert.False(CourtshipMisgivings.IsNone("I fear nothing but the sea"));

        var list = new List<CourtshipMisgiving>();
        Assert.Equal(0, CourtshipMisgivings.SetDown(list, "none"));
        Assert.Empty(list);
    }

    [Fact]
    public void Settle_MatchesLoosely_AndKeepsHerLightWord()
    {
        var list = new List<CourtshipMisgiving>();
        CourtshipMisgivings.SetDown(list, "His purse is lighter than his promises.\nWhat of my Da, left alone?");

        var settled = CourtshipMisgivings.Settle(list, "the worry about his purse being lighter than his promises", "He has shown me his ledgers, and his word held.");
        Assert.NotNull(settled);
        Assert.True(settled!.Settled);
        Assert.Equal("He has shown me his ledgers, and his word held.", settled.SettledNote);
        Assert.Equal(1, CourtshipMisgivings.OpenCount(list));

        // A settled one is not settled twice; an unrelated restatement matches nothing.
        Assert.Null(CourtshipMisgivings.Settle(list, "his purse and his promises", "again"));
        Assert.Null(CourtshipMisgivings.Settle(list, "the weather over the mountains", ""));
    }

    [Fact]
    public void Revise_Rewords_AndReopen_TakesASettledOneUpAgain()
    {
        var list = new List<CourtshipMisgiving>();
        CourtshipMisgivings.SetDown(list, "What of my Da, left alone?");

        var revised = CourtshipMisgivings.Revise(list, "my Da left alone", "What of my Da — who will buy him his farm?");
        Assert.NotNull(revised);
        Assert.Equal("What of my Da — who will buy him his farm?", list[0].Text);
        Assert.Null(CourtshipMisgivings.Revise(list, "my Da", "")); // a revise never blanks

        CourtshipMisgivings.Settle(list, "who will buy him his farm", "The farm is bought.");
        Assert.Equal(0, CourtshipMisgivings.OpenCount(list));

        var reopened = CourtshipMisgivings.Reopen(list, "who will buy my Da his farm");
        Assert.NotNull(reopened);
        Assert.False(reopened!.Settled);
        Assert.Equal(string.Empty, reopened.SettledNote);
        Assert.Equal(1, CourtshipMisgivings.OpenCount(list));
    }

    // ------------------------- the seeding of an already-lived road -------------------------

    [Fact]
    public void Seed_ReadsTheStageAndTheWhy()
    {
        Assert.True(CourtshipSeed.TryParseSeed("STAGE: devotion\nWHY: Love has been spoken between us.", out var stage, out var why));
        Assert.Equal(CourtshipStage.Devotion, stage);
        Assert.Equal("Love has been spoken between us.", why);

        Assert.True(CourtshipSeed.TryParseSeed("stage: betrothed", out stage, out _));
        Assert.Equal(CourtshipStage.Betrothed, stage);

        Assert.True(CourtshipSeed.TryParseSeed("ready", out stage, out _));
        Assert.Equal(CourtshipStage.Ready, stage);

        Assert.True(CourtshipSeed.TryParseSeed("STAGE: none\nWHY: We have spoken only of trade.", out stage, out _));
        Assert.Equal(CourtshipStage.None, stage);
    }

    [Fact]
    public void Seed_RefusesGarbage_SoTheSeedingRetriesAnotherDay()
    {
        Assert.False(CourtshipSeed.TryParseSeed(null, out _, out _));
        Assert.False(CourtshipSeed.TryParseSeed("She seems fond of him, hard to say.", out _, out _));
    }

    [Fact]
    public void Seed_PromptHonorsBothLoveSpoken_AndPromiseWithheld()
    {
        var prompt = CourtshipSeed.BuildPrompt("Sibylla", "woman", "Mizam",
            "He taught me of the Word.", "I love him in secret.", "…");
        Assert.Contains("betrothed — we have already promised ourselves", prompt);
        Assert.Contains("where a promise was deliberately not yet given, honor that too", prompt);
        Assert.Contains("STAGE:", prompt);
    }

    // ------------------------- the words she reads -------------------------

    [Fact]
    public void RoadSection_SpeaksTheStage_AndHerMisgivingsOpenly()
    {
        var misgivings = new[]
        {
            new CourtshipText.MisgivingView { Text = "I fear the road would own him more than I would" },
            new CourtshipText.MisgivingView { Text = "His purse is lighter than his promises", Settled = true, Note = "His word held" },
        };
        var section = CourtshipText.RoadSection("Mizam", CourtshipStage.Devotion, misgivings, true, false, false, "");

        Assert.Contains("my heart is truly given", section);
        Assert.Contains("set down by my own hand", section);
        Assert.Contains("speak of these openly", section);
        Assert.Contains("this still stands in me", section);
        Assert.Contains("laid to rest: His word held", section);
        // The list is a living thing, and she knows what settling it opens.
        Assert.Contains("This list lives with me", section);
        Assert.Contains("no doubt of mine bars the road", section);
        // The old ledger's voice is gone for good.
        Assert.DoesNotContain("never recite them as a list", section);
    }

    [Fact]
    public void RoadSection_InvitesTheWeighing_UntilSheHasDoneIt()
    {
        var unweighed = CourtshipText.RoadSection("Mizam", CourtshipStage.Warmth, null, false, false, false, "");
        Assert.Contains("I have not yet sat with myself", unweighed);
        Assert.Contains("five at the very most", unweighed);
        Assert.Contains("set down none", unweighed);

        var clear = CourtshipText.RoadSection("Mizam", CourtshipStage.Ready, null, true, false, false, "");
        Assert.Contains("found no misgiving standing", clear);

        // Betrothed already: the promise is given; no invitation to re-open the weighing.
        var betrothed = CourtshipText.RoadSection("Mizam", CourtshipStage.Betrothed, null, false, false, false, "");
        Assert.DoesNotContain("I have not yet sat with myself", betrothed);
    }

    [Fact]
    public void RoadSection_IsSilentAtNoneAndAtWed()
    {
        Assert.Equal(string.Empty, CourtshipText.RoadSection("Mizam", CourtshipStage.None, null, false, false, false, ""));
        Assert.Equal(string.Empty, CourtshipText.RoadSection("Mizam", CourtshipStage.Wed, null, true, false, false, ""));
    }

    [Fact]
    public void RoadSection_Betrothed_NamesTheMissingBlessing()
    {
        var barred = CourtshipText.RoadSection("Mizam", CourtshipStage.Betrothed, null, true, true, false, "Lucon");
        Assert.Contains("the blessing of Lucon", barred);
        Assert.Contains("cannot be given without it", barred);

        var blessed = CourtshipText.RoadSection("Mizam", CourtshipStage.Betrothed, null, true, true, true, "Lucon");
        Assert.Contains("my kin have blessed the match", blessed);
    }

    [Fact]
    public void Refusals_NeverQuoteARailNumber()
    {
        foreach (CourtshipRoad.StepVerdict verdict in System.Enum.GetValues(typeof(CourtshipRoad.StepVerdict)))
        {
            var text = CourtshipText.ForwardRefusal(verdict, "Mizam");
            Assert.DoesNotMatch(@"\d", text); // the Sibuga floor lesson, held by test forever
        }
    }

    [Fact]
    public void SuitorTerms_KeepTheBoundsPrivate_AndNeverSellTheFloorFirst()
    {
        var terms = CourtshipText.SuitorTerms("Mizam", "Ira", "my daughter", 1500, 1050, 1950, true);
        Assert.Contains("1500", terms);
        Assert.Contains("never below 1050", terms);
        Assert.Contains("I do not speak these numbers aloud", terms);
        Assert.Contains("never volunteer my lowest", terms);

        var fixedPrice = CourtshipText.SuitorTerms("Mizam", "Ira", "my daughter", 1500, 1500, 1500, false);
        Assert.Contains("no more and no less", fixedPrice);
    }

    // ------------------------- persistence: old files stay whole -------------------------

    [Fact]
    public void OldMemories_LoadWithNoRoadWalked_AndRoundTripTheNewFields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "immersiveai-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var store = new JsonMemoryStore(dir);
        var path = Path.Combine(dir, "memories.json");
        try
        {
            // A file written before the courtship road existed — and one from the short era of the
            // matchmaker's CourtshipAsks (2026.08.07–08): the retired field is simply ignored, so
            // such a soul begins with an unweighed heart and writes her own misgivings in the talk.
            File.WriteAllText(path,
                "{\"NpcId\":\"npc_1\",\"NpcName\":\"Sibylla\",\"Summary\":\"old story\"," +
                "\"CourtshipAsks\":[{\"Text\":\"He must be true\",\"Check\":\"trait Honor >= 1\"}]}");
            var old = store.LoadFrom(path, "npc_1");
            Assert.Equal(CourtshipStage.None, old.CourtshipStage);
            Assert.False(old.CourtshipSeeded);
            Assert.Empty(old.CourtshipMisgivings);
            Assert.False(old.MisgivingsWeighed);
            Assert.Equal(-1, old.BetrothedGameDay);
            Assert.Equal(-1, old.FamilyBlessingDay);

            old.CourtshipStage = CourtshipStage.Betrothed;
            old.CourtshipSeeded = true;
            old.BetrothedGameDay = 91100.5;
            old.MisgivingsWeighed = true;
            old.CourtshipMisgivings.Add(new CourtshipMisgiving
            {
                Text = "What of my Da, left alone?",
                Settled = true,
                SettledNote = "The farm is bought.",
            });
            store.SaveTo(path, old);

            var reloaded = store.LoadFrom(path, "npc_1");
            Assert.Equal(CourtshipStage.Betrothed, reloaded.CourtshipStage);
            Assert.True(reloaded.CourtshipSeeded);
            Assert.Equal(91100.5, reloaded.BetrothedGameDay);
            Assert.True(reloaded.MisgivingsWeighed);
            Assert.Single(reloaded.CourtshipMisgivings);
            Assert.True(reloaded.CourtshipMisgivings[0].Settled);
            Assert.Equal("The farm is bought.", reloaded.CourtshipMisgivings[0].SettledNote);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    // ------------------------- the sheet carries the road -------------------------

    private static NpcPersona Persona() => new()
    {
        Name = "Sibylla",
        RoleDescription = "A Sturgian wanderer.",
        PersonalityDescription = "Honest, daring.",
    };

    [Fact]
    public void Sheet_OffersTheTrothAndMisgivingsWhispers_OnlyWhenTheHandRidesAlong()
    {
        var withTool = Persona();
        withTool.CanTendTroth = true;
        var on = new PromptBuilder().Build(withTool, new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.Contains("My troth is my own to tend", on);
        Assert.Contains("the seal is wholly theirs", on);
        Assert.Contains("I never speak of steps, stages, or rules", on);
        Assert.Contains("My misgivings about a life together are my own", on);
        Assert.Contains("never pretend one away", on);

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.DoesNotContain("My troth is my own to tend", off);
        Assert.DoesNotContain("My misgivings about a life together", off);
    }

    [Fact]
    public void Sheet_OffersTheBlessingWhisper_OnlyToTheHeadOfTheHouse()
    {
        var head = Persona();
        head.CanBlessTroth = true;
        var on = new PromptBuilder().Build(head, new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.Contains("the blessing of that match is mine to give or withhold", on);
        Assert.Contains("never volunteer my lowest", on);

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.DoesNotContain("blessing of that match", off);
    }

    [Fact]
    public void Sheet_PlacesTheRoad_BesideTheDeepMemoryOfThePlayer()
    {
        var persona = Persona();
        persona.CourtshipTerms = CourtshipText.RoadSection("Mizam", CourtshipStage.Devotion, null, true, false, false, "");
        var memory = new NpcMemory { Summary = "He taught me of the Word." };

        var system = new PromptBuilder().Build(persona, memory, "In the tavern." + PromptBuilder.MeetingSeparator + "And now Mizam comes to me.", "Mizam", "Hello")[0].Content;

        Assert.Contains("Where my heart stands with Mizam", system);
        // The road follows what he IS to her, and the arrival still lands last.
        Assert.True(system.IndexOf("What Mizam is to me") < system.IndexOf("Where my heart stands with Mizam"));
        Assert.True(system.IndexOf("Where my heart stands with Mizam") < system.IndexOf("And now Mizam comes to me."));
    }
}
