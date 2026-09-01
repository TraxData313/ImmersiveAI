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
    public void TheRoadKeepsNoCalendar()
    {
        // 2026.08.30: forward steps used to come one per game day. Anton asked, she said "aye, with
        // all my heart" — and the log answered that her heart needed a night to settle. The whole
        // road can now be walked in one evening if her heart truly stands there; what refuses is
        // only ever hers.
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.None)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Warmth)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Ready)));

        // …and the heart still rules: the same instant, one step short of the regard it asks.
        Assert.Equal(CourtshipRoad.StepVerdict.HeartNotThere, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, relation: 39)));
        Assert.Equal(CourtshipRoad.StepVerdict.MisgivingsRemain, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, openMisgivings: 1)));
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

    [Fact]
    public void MayWriteNew_LetsTheFirstWeighingThrough_ThenRefusesWhileOneStands()
    {
        var list = new List<CourtshipMisgiving>();

        // The first weighing writes the whole list at once and is never refused.
        Assert.True(CourtshipMisgivings.MayWriteNew(list, alreadyWeighed: false));
        CourtshipMisgivings.SetDown(list,
            "I am no lord's daughter, and I fear he will one day want a wife who is\n"
            + "He is my captain and I am in his pay");
        Assert.Equal(2, CourtshipMisgivings.OpenCount(list));

        // Afterwards nothing new is written on top of what already stands — the pile that made
        // his courtship a bug is stopped at the source, not chased with a fuzzier matcher.
        Assert.False(CourtshipMisgivings.MayWriteNew(list, alreadyWeighed: true));

        // A semantic twin of a standing doubt shares barely a word with it, which is exactly why
        // no matching rule could ever have caught this one — and why the rail above is the fix.
        Assert.Null(CourtshipMisgivings.FindRestated(list,
            "I fear I may be cherished as my captain's paid healer rather than chosen freely as his equal"));

        // Answer them and the list lives again.
        foreach (var m in list) { m.Settled = true; m.SettledNote = "life answered it"; }
        Assert.True(CourtshipMisgivings.MayWriteNew(list, alreadyWeighed: true));
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
        // WHILE ONE STANDS, NOTHING NEW IS WRITTEN (2026.08.31): the rail the resolver enforces is
        // said in her own sheet too, or she goes on arguing with a rule she cannot read.
        Assert.Contains("set down nothing new", section);
        Assert.Contains("no doubt of mine bars the road", section);
        // The old ledger's voice is gone for good.
        Assert.DoesNotContain("never recite them as a list", section);
    }

    [Fact]
    public void RoadSection_InvitesTheWeighing_AtLove_AndNotBefore()
    {
        // THE DOUBTS BELONG TO LOVE AND AFTER (2026.08.31, Anton's model). At plain warmth nobody
        // has spoken of a marriage, and inviting her to weigh one builds a wall across a road that
        // has not begun — which is exactly what his save carried.
        var warmth = CourtshipText.RoadSection("Mizam", CourtshipStage.Warmth, null, false, false, false, "");
        Assert.DoesNotContain("sat with myself", warmth);

        var unweighed = CourtshipText.RoadSection("Mizam", CourtshipStage.Devotion, null, false, false, false, "");
        Assert.Contains("I have not yet sat with myself", unweighed);
        Assert.Contains("Now is the hour for it", unweighed);
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
    public void Sheet_CarriesNoTrothOrBlessingProse_ItLivesInTheToolsNow()
    {
        // 2026.08.14: the troth, misgivings and blessing paragraphs moved into tend_courtship,
        // weigh_misgivings and bless_marriage themselves. The Can* flags still decide which hands
        // are offered at all - only the words moved - so the sheet must stay silent either way.
        var everyHand = Persona();
        everyHand.CanTendTroth = true;
        everyHand.CanBlessTroth = true;

        var on = new PromptBuilder().Build(everyHand, new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.DoesNotContain("My troth is my own to tend", on);
        Assert.DoesNotContain("My misgivings about a life together", on);
        Assert.DoesNotContain("blessing of that match", on);

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.DoesNotContain("My troth is my own to tend", off);
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

    // ------------------------- the action word, as models actually say it -------------------------
    //
    // Every string below came off the wire on gpt-5.6-terra (2026.08.09), with the real sheet, the
    // real tool list and Sibylla's own three misgivings: she reached for the hand at exactly the
    // right moments, quoted her own misgivings word for word, wrote honest notes — and named the
    // deed "resolve" and "review". Both fell through the resolver and nothing moved. The words are
    // kept here verbatim so the reading can never quietly narrow again.

    [Theory]
    [InlineData("settle")]
    [InlineData("resolve")]      // live, gpt-5.6-terra
    [InlineData("resolved")]
    [InlineData("Settle")]
    [InlineData(" lay to rest ")]
    [InlineData("answered")]
    public void CanonicalAction_ReadsLayingToRest_HoweverItIsNamed(string said) =>
        Assert.Equal(CourtshipMisgivings.ActSettle, CourtshipMisgivings.CanonicalAction(said));

    [Theory]
    [InlineData("set_down")]
    [InlineData("review")]       // live, gpt-5.6-terra
    [InlineData("set-down")]
    [InlineData("write_down")]
    [InlineData("weigh")]
    [InlineData("none")]
    public void CanonicalAction_ReadsTheWriting_HoweverItIsNamed(string said) =>
        Assert.Equal(CourtshipMisgivings.ActSetDown, CourtshipMisgivings.CanonicalAction(said));

    [Theory]
    [InlineData("release", CourtshipMisgivings.ActRelease)]
    [InlineData("strike out", CourtshipMisgivings.ActRelease)]
    [InlineData("revise", CourtshipMisgivings.ActRevise)]
    [InlineData("reword", CourtshipMisgivings.ActRevise)]
    [InlineData("reopen", CourtshipMisgivings.ActReopen)]
    [InlineData("unsettle", CourtshipMisgivings.ActReopen)]
    public void CanonicalAction_ReadsTheRest(string said, string expected) =>
        Assert.Equal(expected, CourtshipMisgivings.CanonicalAction(said));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("marry")]
    [InlineData("tend_courtship")]
    public void CanonicalAction_AnswersEmptyForWhatItCannotRead(string said) =>
        Assert.Equal(string.Empty, CourtshipMisgivings.CanonicalAction(said));

    [Fact]
    public void Settle_LandsOnHerOwnWords_InHerOwnTongue()
    {
        // Her three, verbatim from memories.json, and the texts the model sent back with them.
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving { Text = "Страх ме е, че разликата между свободен мъж и слугиня може да донесе тежест и опасност и на двама ни." },
            new CourtshipMisgiving { Text = "Страх ме е, че под властта и славата ти може да избереш благородна жена, а аз да остана настрана." },
            new CourtshipMisgiving { Text = "Трябва да знам, че в брака ще има място и за моята съвест, разум и честна дума, не само за мълчание." },
        };

        Assert.NotNull(CourtshipMisgivings.Settle(list,
            "Страх ме е, че разликата между свободен мъж и слугиня може да донесе тежест и опасност и на двама ни.",
            "Публичното ми освобождаване пред клана отговори на този страх."));
        // A loose restatement, not a quotation — the lenient matching must still find its mark.
        Assert.NotNull(CourtshipMisgivings.Settle(list,
            "Страх, че ще избере благородна жена вместо мен", "Той се закле пред Бога."));
        Assert.NotNull(CourtshipMisgivings.Settle(list,
            "място за моята съвест и честна дума в брака", "Обеща, че гласът ми ще се чува."));

        Assert.Equal(0, CourtshipMisgivings.OpenCount(list));
        Assert.Equal(3, CourtshipMisgivings.TotalCount(list));
    }

    [Fact]
    public void RoadSection_AtReadyWithNothingStanding_SaysSheWaitsToBeAsked()
    {
        var settled = new List<CourtshipText.MisgivingView>
        {
            new CourtshipText.MisgivingView { Text = "His purse is lighter than his promises.", Settled = true, Note = "his word held" },
        };

        var ready = CourtshipText.RoadSection("Mizam", CourtshipStage.Ready, settled, true, false, false, "");
        Assert.Contains("I wait now to be asked", ready);
        Assert.Contains("lay my promise before them by my own hand", ready);

        // Not before readiness — a heart still on its way is not waiting for the word.
        Assert.DoesNotContain("I wait now to be asked",
            CourtshipText.RoadSection("Mizam", CourtshipStage.Devotion, settled, true, false, false, ""));
        // Not while something of hers still stands, even at Ready (a new doubt may be written
        // down after the rung was reached).
        var oneOpen = new List<CourtshipText.MisgivingView>(settled)
        {
            new CourtshipText.MisgivingView { Text = "What of my Da, left alone?" },
        };
        Assert.DoesNotContain("I wait now to be asked",
            CourtshipText.RoadSection("Mizam", CourtshipStage.Ready, oneOpen, true, false, false, ""));
        // And never once the promise is given — the posture then is the troth, not the waiting.
        Assert.DoesNotContain("I wait now to be asked",
            CourtshipText.RoadSection("Mizam", CourtshipStage.Betrothed, settled, true, false, false, ""));
    }

    [Fact]
    public void RoadSection_AlwaysCarriesTheRailAgainstAWeddingMadeOfWords()
    {
        // The 2026.08.15 report: two open misgivings, so the road refused every step and every lay —
        // and the pair went to a temple and said their vows anyway, in words, while nothing at all
        // happened in the world. The rails held; the TALK simply walked around them. So every stage
        // that is on the road carries the rail, wherever her heart stands.
        foreach (var stage in new[] { CourtshipStage.Warmth, CourtshipStage.Devotion,
                                      CourtshipStage.Ready, CourtshipStage.Betrothed })
        {
            var section = CourtshipText.RoadSection("Mizam", stage, null, true, false, false, "");
            Assert.Contains("no words of ours make a marriage", section);
            Assert.Contains("not a temple", section);
        }

        // And nowhere else: silence at both ends of the road stays silence.
        Assert.Equal(string.Empty, CourtshipText.RoadSection("Mizam", CourtshipStage.None, null, true, false, false, ""));
        Assert.Equal(string.Empty, CourtshipText.RoadSection("Mizam", CourtshipStage.Wed, null, true, false, false, ""));
    }

    [Fact]
    public void ForwardRefusalForPlayer_SaysPlainlyWhatHerOwnWordsMayNot()
    {
        // Her side is numberless and vague on purpose; the PLAYER's side must name the cause, or a
        // refused reach is indistinguishable from a broken mod.
        Assert.Contains("misgiving", CourtshipText.ForwardRefusalForPlayer(CourtshipRoad.StepVerdict.MisgivingsRemain));
        Assert.Contains("station", CourtshipText.ForwardRefusalForPlayer(CourtshipRoad.StepVerdict.StationTooFar));
        Assert.Contains("seasoned", CourtshipText.ForwardRefusalForPlayer(CourtshipRoad.StepVerdict.TrothTooFresh));

        foreach (CourtshipRoad.StepVerdict verdict in System.Enum.GetValues(typeof(CourtshipRoad.StepVerdict)))
            Assert.False(string.IsNullOrWhiteSpace(CourtshipText.ForwardRefusalForPlayer(verdict)));
    }

    [Fact]
    public void HandsCameSwapped_CatchesTheMisgivingWrittenIntoItsOwnAnswer()
    {
        // Verbatim off the wire, gpt-5.6-terra, 2026.08.09: every settle it made came crossed —
        // the answer where the misgiving belongs, the misgiving in the note.
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving { Text = "Страх ме е, че разликата между свободен мъж и слугиня може да донесе тежест и опасност и на двама ни." },
            new CourtshipMisgiving { Text = "Трябва да знам, че в брака ще има място и за моята съвест, разум и честна дума, не само за мълчание." },
        };
        var came = "Мизам ме обяви пред клана за свободна жена и господарка в дома си.";
        var note = "Страх ме е, че разликата между свободен мъж и слугиня може да донесе тежест и опасност и на двама ни.";

        Assert.True(CourtshipMisgivings.HandsCameSwapped(list, came, note));

        // Untangled, it lands — and her light word is the answer, kept beside it.
        var settled = CourtshipMisgivings.Settle(list, note, came);
        Assert.NotNull(settled);
        Assert.Equal(came, settled!.SettledNote);

        // Hands the right way round are never "swapped"; nor are two lines that both match nothing.
        Assert.False(CourtshipMisgivings.HandsCameSwapped(list,
            "място за моята съвест и честна дума", "Обеща, че гласът ми ще се чува."));
        Assert.False(CourtshipMisgivings.HandsCameSwapped(list,
            "времето над планините", "конете са напоени"));
    }

    [Fact]
    public void SetDown_DoesNotBreedCopiesWhenSheRecitesTheListBack()
    {
        // Asked "what stops you?", she reaches for the hand and reads her whole list back through
        // it (live, gpt-5.6-terra). A recital must add nothing — least of all a second copy of a
        // doubt she then could not tell apart from its twin.
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving { Text = "Страх ме е, че под властта и славата ти може да избереш благородна жена, а аз да остана настрана." },
        };

        Assert.Equal(0, CourtshipMisgivings.SetDown(list,
            "Страх ме е, че под властта и славата ти може да избереш благородна жена, а аз да остана настрана."));
        Assert.Equal(0, CourtshipMisgivings.SetDown(list,
            "Страх ме е, че под властта и славата ти може да избереш благородна жена"));
        Assert.Single(list);

        // A truly new doubt in the very same register still lands.
        Assert.Equal(1, CourtshipMisgivings.SetDown(list,
            "Страх ме е, че децата ни ще носят срама на майка си."));
        Assert.Equal(2, CourtshipMisgivings.OpenCount(list));
    }

    [Fact]
    public void SetDown_CatchesTheTwinThatOnlyTradedAWord()
    {
        // Both pairs are Rhia the Healer's, verbatim from her memories.json (2026.08.30). One word
        // inserted, one word traded, and the old character-containment test called them strangers.
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving { Text = "I do not know if he will let himself be loved." },
            new CourtshipMisgiving { Text = "I am no lord's daughter, and I fear he will one day want a wife who is." },
        };

        Assert.Equal(0, CourtshipMisgivings.SetDown(list, "I do not know if he will truly let himself be loved."));
        Assert.Equal(0, CourtshipMisgivings.SetDown(list, "I fear he will one day want a wife who is noble-born."));
        Assert.Equal(2, CourtshipMisgivings.TotalCount(list));

        // The leniency stops well short of a different fear wearing the same opening.
        Assert.Equal(1, CourtshipMisgivings.SetDown(list, "I do not know if he will let me go."));
        Assert.Equal(3, CourtshipMisgivings.OpenCount(list));
    }

    [Fact]
    public void ATwinLaidToRestNoLongerOrphansTheOneSheFirstWrote()
    {
        // THE BUG, whole (Rhia, 2026.08.30): the twin was born, she answered the twin, and her first
        // wording stood open forever — a road walled shut by a doubt she had already answered, and
        // no way out, because "none" is refused while anything stands. Now there is no twin to
        // answer: her words land on the one line she truly holds.
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving { Text = "I do not know if he will let himself be loved." },
        };
        CourtshipMisgivings.SetDown(list, "I do not know if he will truly let himself be loved.");

        Assert.NotNull(CourtshipMisgivings.Settle(list,
            "I do not know if he will truly let himself be loved.",
            "He has begun to speak of opening his heart in happiness, not only in fear."));
        Assert.Equal(0, CourtshipMisgivings.OpenCount(list));
    }

    [Fact]
    public void ARefusedTwinCanBeNamedBackToHer()
    {
        // The other half of a lenient test: she must be able to see WHICH of her lines swallowed
        // hers, or a doubt truly new is lost in silence.
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving { Text = "I do not know if he will let himself be loved." },
        };

        var already = CourtshipMisgivings.FindRestated(list, "I do not know if he will truly let himself be loved.");
        Assert.NotNull(already);
        Assert.Equal("I do not know if he will let himself be loved.", already!.Text);

        Assert.Null(CourtshipMisgivings.FindRestated(list, "I do not know if he will let me go."));
        Assert.Null(CourtshipMisgivings.FindRestated(list, "none"));
    }

    [Fact]
    public void HealTwins_FoldsATwinAlreadyWrittenIntoTheLineSheFirstWrote()
    {
        // Rhia the Healer's own list, as her save carried it on 2026.08.31 — both pairs born before
        // the razor learned word-level containment, each pair half-answered, so two doubts she had
        // already put to rest stood open forever and her road was walled shut with nothing in her
        // own hand able to reach them.
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving
            {
                Text = "I am no lord's daughter, and I fear he will one day want a wife who is.",
                Settled = true,
                SettledNote = "He has plainly said he would choose me over rank or any noble daughter.",
            },
            new CourtshipMisgiving { Text = "I do not know if he will let himself be loved." },
            new CourtshipMisgiving { Text = "I fear he will one day want a wife who is noble-born." },
            new CourtshipMisgiving
            {
                Text = "I do not know if he will truly let himself be loved.",
                Settled = true,
                SettledNote = "He has begun to speak of opening his heart in happiness, not only in fear.",
            },
        };

        Assert.Equal(2, CourtshipMisgivings.HealTwins(list));
        Assert.Equal(2, CourtshipMisgivings.TotalCount(list));
        Assert.Equal(0, CourtshipMisgivings.OpenCount(list));

        // The EARLIER wording survives — that is the line she has lived with — and it comes to rest
        // under the answer whichever copy carried it.
        var loved = list.Single(m => m.Text == "I do not know if he will let himself be loved.");
        Assert.True(loved.Settled);
        Assert.Contains("opening his heart", loved.SettledNote);
        Assert.Contains("no lord's daughter", list.Single(m => m.Settled && m != loved).Text);
    }

    [Fact]
    public void HealTwins_LeavesADifferentDoubtAndRunsOnlyOnce()
    {
        var list = new List<CourtshipMisgiving>
        {
            new CourtshipMisgiving { Text = "I do not know if he will let himself be loved." },
            new CourtshipMisgiving { Text = "I do not know if he will let me go." },
        };

        // A fear that shares four fifths of its words is still a wholly different fear.
        Assert.Equal(0, CourtshipMisgivings.HealTwins(list));
        Assert.Equal(2, CourtshipMisgivings.OpenCount(list));

        list.Add(new CourtshipMisgiving { Text = "I do not know if he will truly let himself be loved." });
        Assert.Equal(1, CourtshipMisgivings.HealTwins(list));
        // Idempotent: it runs at every load, and a healed list must fold nothing on the next pass.
        Assert.Equal(0, CourtshipMisgivings.HealTwins(list));
        Assert.Equal(2, CourtshipMisgivings.TotalCount(list));
    }

    [Fact]
    public void RoadSection_KeepsHerFreeToSpeak_ButNeverTellsHerToRaiseThem()
    {
        // 2026.08.31, Anton: she circled the same two doubts every exchange for days. The old
        // closing line told her to — "so I bring them into our talks myself, and give each its
        // honest hearing". Her freedom to speak of them stands; the instruction to is gone.
        var misgivings = new[]
        {
            new CourtshipText.MisgivingView { Text = "I fear the road would own him more than I would" },
            new CourtshipText.MisgivingView { Text = "His purse is lighter than his promises", Settled = true, Note = "His word held" },
        };
        var section = CourtshipText.RoadSection("Mizam", CourtshipStage.Devotion, misgivings, true, false, false, "");

        Assert.Contains("speak of these openly", section);
        Assert.Contains("I do not circle back to the same doubt", section);
        Assert.DoesNotContain("I bring them into our talks myself", section);
        // The answered ones are marked closed where she reads them, not merely listed beside the
        // living ones — half of the circling was over doubts she had already put down.
        Assert.Contains("I do not take them up again", section);
    }

    // ------------------------- the road, shown to the player -------------------------

    [Fact]
    public void Rail_DrawsTheWholePathWithExactlyOneRungLit()
    {
        var rail = CourtshipRail.Build(CourtshipStage.Devotion);

        Assert.Equal(new[] { "Warmth", "Love", "Ready", "Betrothed", "Married" },
            rail.Select(n => n.Name).ToArray());
        Assert.Single(rail.Where(n => n.Current));
        Assert.Equal("Love", rail.Single(n => n.Current).Name);
        // Behind us and ahead of us, both readable at a glance — that is the whole point of a path.
        Assert.True(rail.Single(n => n.Name == "Warmth").Done);
        Assert.False(rail.Single(n => n.Name == "Ready").Done);
    }

    [Fact]
    public void Rail_DrawsTheWorldsOwnRungsOnlyWhereTheyApply()
    {
        // A wanderer has no house to ask, and MinBetrothalDays at 0 means no days are owed. Drawing
        // either as a greyed future step would invent an obstacle — which is exactly the mistake a
        // player makes unaided: Anton believed he had days to wait, and had none.
        var plain = CourtshipRail.Build(CourtshipStage.Betrothed);
        Assert.DoesNotContain(plain, n => n.Name == CourtshipRail.KinsWord);
        Assert.DoesNotContain(plain, n => n.Name == CourtshipRail.TheDays);
        Assert.Equal("Betrothed", plain.Single(n => n.Current).Name);

        // And where they DO apply, the rung that actually stands in the way takes the light — the
        // path must never say "you are here: betrothed" while the thing to do is elsewhere.
        var withKin = CourtshipRail.Build(CourtshipStage.Betrothed, kinsWordRung: true, kinsWordGiven: false,
            seasonRung: true, seasonDone: false);
        Assert.Equal(CourtshipRail.KinsWord, withKin.Single(n => n.Current).Name);

        var kinGiven = CourtshipRail.Build(CourtshipStage.Betrothed, kinsWordRung: true, kinsWordGiven: true,
            seasonRung: true, seasonDone: false);
        Assert.Equal(CourtshipRail.TheDays, kinGiven.Single(n => n.Current).Name);
        Assert.True(kinGiven.Single(n => n.Name == CourtshipRail.KinsWord).Done);
    }

    [Fact]
    public void Rail_AtEveryStage_LightsExactlyOneRung()
    {
        foreach (var stage in new[] { CourtshipStage.None, CourtshipStage.Warmth, CourtshipStage.Devotion,
                                      CourtshipStage.Ready, CourtshipStage.Betrothed, CourtshipStage.Wed })
        {
            var rail = CourtshipRail.Build(stage, kinsWordRung: true, seasonRung: true);
            Assert.Single(rail.Where(n => n.Current));
            Assert.Contains("[", CourtshipRail.OneLine(rail));
        }
        // Nothing is behind you before the road has begun.
        Assert.DoesNotContain(CourtshipRail.Build(CourtshipStage.None), n => n.Done);
    }

    [Fact]
    public void WhatNow_AlwaysNamesAVerb_AndPointsAtTheAskingDoor()
    {
        // THE ASKING IS THE PLAYER'S NOW (2026.08.31): the moment nothing would refuse — at love
        // as at readiness, since the question itself carries her last step — the line points at
        // the one visible door, and phrases an absent soul honestly.
        foreach (var stage in new[] { CourtshipStage.Ready, CourtshipStage.Devotion })
        {
            var open = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
            {
                Stage = stage, NpcName = "Rhia",
                Verdict = CourtshipRoad.StepVerdict.Allowed,
            });
            Assert.Contains("ask for Rhia's hand", open);
            Assert.Contains("Between us", open);

            var away = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
            {
                Stage = stage, NpcName = "Rhia",
                Verdict = CourtshipRoad.StepVerdict.Allowed,
                Together = false,
            });
            Assert.Contains("when you stand together", away);
        }

        // And before any road at all, the line every new player was owed and never had.
        var unbegun = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.None, NpcName = "Rhia",
            Verdict = CourtshipRoad.StepVerdict.Allowed,
        });
        Assert.Contains("No road has begun", unbegun);
    }

    [Fact]
    public void WhatNow_NamesTheRealBlocker_AndMayQuoteNumbersBecauseThePlayerReadsIt()
    {
        var doubts = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.Devotion, NpcName = "Rhia", OpenMisgivings = 2,
            Verdict = CourtshipRoad.StepVerdict.MisgivingsRemain,
        });
        Assert.Contains("2 doubts", doubts);

        var heart = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.Warmth, NpcName = "Rhia", Relation = 12,
            Verdict = CourtshipRoad.StepVerdict.HeartNotThere,
        });
        // Numbers are refused to HER and owed to HIM — he already sees her regard on the same panel.
        Assert.Contains("12", heart);
        Assert.Contains(CourtshipRoad.DevotionRelationFloor.ToString(), heart);

        // The rungs of the world after the promise, each with its own plain instruction.
        var kin = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.Betrothed, NpcName = "Rhia",
            KinsWordAwaited = true, HeadName = "Lucon",
        });
        Assert.Contains("Lucon", kin);

        var days = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.Betrothed, NpcName = "Rhia", DaysLeft = 2.4,
        });
        Assert.Contains("3 more days", days);

        var wed = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.Betrothed, NpcName = "Rhia",
        });
        Assert.Contains("choose the wedding day", wed);

        // The station gate names both tiers when it has them — a wall you can measure is a wall
        // you can climb; without the numbers it stays plainly named.
        var station = CourtshipText.WhatNow(new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.Devotion, NpcName = "Rhia",
            Verdict = CourtshipRoad.StepVerdict.StationTooFar,
            PlayerClanTier = 2, RequiredTier = 4,
        });
        Assert.Contains("tier 4", station);
        Assert.Contains("yours is 2", station);
    }

    [Fact]
    public void IsRoadBeat_KnowsEveryOneOfItsOwnTemplates()
    {
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.StepBeat("Mizam", CourtshipStage.Ready, "at last")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.StepBackBeat("Mizam", CourtshipStage.Betrothed, CourtshipStage.Ready, "")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.StepBackBeat("Mizam", CourtshipStage.Ready, CourtshipStage.Devotion, "")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.BetrothalSealedBeat("Mizam")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.ProposalSealedBeat("Mizam", "a ring of gold")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.ProposalSealedBeat("Mizam", null)));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.BetrothalDeclinedBeat("Mizam")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.WeddingSealedBeat("Mizam")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.WeddingDeclinedBeat("Mizam")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.SeededBeat("Mizam", CourtshipStage.Warmth, "")));
        Assert.True(CourtshipText.IsRoadBeat(CourtshipText.BlessingNewsBeat("Mizam", "Lucon")));

        Assert.False(CourtshipText.IsRoadBeat("We met and spoke face to face for the first time."));
        Assert.False(CourtshipText.IsRoadBeat(null));
    }

    [Fact]
    public void RoadNotes_KeepTheLastOfThem_AndRecordNoBlanks()
    {
        var notes = new List<RoadNote>();
        for (int i = 0; i < RoadNotes.MaxKept + 5; i++)
            RoadNotes.Add(notes, RoadNotes.KindMoved, "movement " + i, i, i);

        Assert.Equal(RoadNotes.MaxKept, notes.Count);
        Assert.Equal("movement " + (RoadNotes.MaxKept + 4), notes.Last().Text);

        RoadNotes.Add(notes, RoadNotes.KindMoved, "   ", 1, 1);
        RoadNotes.Add(notes, RoadNotes.KindMoved, null, 1, 1);
        Assert.Equal(RoadNotes.MaxKept, notes.Count);
    }
}
