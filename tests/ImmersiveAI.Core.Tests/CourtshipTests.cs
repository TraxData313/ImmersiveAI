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
        bool asksMet = true,
        double daysBetrothed = -1,
        int minBetrothalDays = 3) => new()
        {
            Stage = stage,
            Relation = relation,
            DaysSinceForwardStep = daysSinceStep,
            PlayerClanTier = playerTier,
            HerStationTier = herTier,
            CharmSlack = slack,
            AllCheckableAsksMet = asksMet,
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
    public void HerQuietAsks_GateReadinessAndTheBetrothal_ButNotTheWedding()
    {
        Assert.Equal(CourtshipRoad.StepVerdict.AsksUnmet, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Devotion, asksMet: false)));
        Assert.Equal(CourtshipRoad.StepVerdict.AsksUnmet, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Ready, asksMet: false)));
        // The promise was proven when it was given — gold spent on the wedding feast unmakes nothing.
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, asksMet: false, daysBetrothed: 10)));
    }

    [Fact]
    public void TheTrothMustSeason_BeforeTheWeddingLay()
    {
        Assert.Equal(CourtshipRoad.StepVerdict.TrothTooFresh, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, daysBetrothed: 1, minBetrothalDays: 3)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, daysBetrothed: 3, minBetrothalDays: 3)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Betrothed, daysBetrothed: 0, minBetrothalDays: 0)));
        Assert.Equal(CourtshipRoad.StepVerdict.NoRoadFurther, CourtshipRoad.JudgeForward(Facts(CourtshipStage.Wed)));
    }

    // ------------------------- her quiet asks: the check language -------------------------

    [Fact]
    public void Checks_ParseTheClosedLanguage_AndDegradeToHeart()
    {
        Assert.Equal(AskCheckKind.Gold, CourtshipChecks.Parse("gold >= 20000").Kind);
        Assert.Equal(20000, CourtshipChecks.Parse("gold >= 20,000").Threshold);
        Assert.Equal(AskCheckKind.Renown, CourtshipChecks.Parse("renown > 150").Kind);
        Assert.Equal(AskCheckKind.Relation, CourtshipChecks.Parse("relation >= 40").Kind);
        Assert.Equal(AskCheckKind.ClanTier, CourtshipChecks.Parse("tier >= 4").Kind);

        var skill = CourtshipChecks.Parse("skill Bow >= 100");
        Assert.Equal(AskCheckKind.Skill, skill.Kind);
        Assert.Equal("Bow", skill.Name);
        Assert.Equal(100, skill.Threshold);

        var trait = CourtshipChecks.Parse("trait Honor >= 1");
        Assert.Equal(AskCheckKind.Trait, trait.Kind);
        Assert.Equal("Honor", trait.Name);

        Assert.Equal(AskCheckKind.Heart, CourtshipChecks.Parse("heart").Kind);
        Assert.Equal(AskCheckKind.Heart, CourtshipChecks.Parse("he must love the road as I do").Kind);
        Assert.Equal(AskCheckKind.Heart, CourtshipChecks.Parse("").Kind);
        Assert.Equal(AskCheckKind.Heart, CourtshipChecks.Parse("skill >= 100").Kind); // nameless skill → heart
    }

    [Fact]
    public void Checks_JudgeFromLiveFacts_AndLeaveTheUnknownToTheHeart()
    {
        var facts = new SuitorFacts
        {
            Gold = 5000,
            Renown = 100,
            Relation = 45,
            ClanTier = 2,
            SkillOf = name => name == "Bow" ? 120 : (int?)null,
            TraitOf = name => name == "Honor" ? 1 : (int?)null,
        };

        Assert.True(CourtshipChecks.IsMet(new CourtshipAsk { Check = "gold >= 4000" }, facts));
        Assert.False(CourtshipChecks.IsMet(new CourtshipAsk { Check = "gold >= 6000" }, facts));
        Assert.True(CourtshipChecks.IsMet(new CourtshipAsk { Check = "skill Bow >= 100" }, facts));
        Assert.Null(CourtshipChecks.IsMet(new CourtshipAsk { Check = "skill Harpistry >= 10" }, facts)); // unknown craft → the heart's
        Assert.True(CourtshipChecks.IsMet(new CourtshipAsk { Check = "trait Honor >= 1" }, facts));
        Assert.Null(CourtshipChecks.IsMet(new CourtshipAsk { Check = "heart" }, facts));
    }

    // ------------------------- the matchmaker's ledger -------------------------

    [Fact]
    public void Ledger_ParsesWellFormedLines_ShedsBulletsAndCapsAtFour()
    {
        var raw =
            "- ASK: He must be no stranger to the sword. | CHECK: skill Two-Handed >= 80\n" +
            "2. ASK: He must hold wealth enough to keep a family. | CHECK: gold >= 3000\n" +
            "ASK: He must not cage me; I ride free. | CHECK: heart\n" +
            "ASK: His word must be iron. | CHECK: trait Honor >= 1\n" +
            "ASK: One too many. | CHECK: heart";
        var asks = MatchmakerLedger.ParseAsks(raw);
        Assert.Equal(4, asks.Count);
        Assert.Equal("He must be no stranger to the sword.", asks[0].Text);
        Assert.Equal("skill Two-Handed >= 80", asks[0].Check);
        Assert.Equal("heart", asks[2].Check);
    }

    [Fact]
    public void Ledger_AnswersNothingForGarbage_SoTheCallSimplyRetries()
    {
        Assert.Empty(MatchmakerLedger.ParseAsks(null));
        Assert.Empty(MatchmakerLedger.ParseAsks("I cannot help with that."));
        Assert.Empty(MatchmakerLedger.ParseAsks("ASK without the shape"));
    }

    [Fact]
    public void Ledger_PromptCarriesTheSoulAndTheRules()
    {
        var prompt = MatchmakerLedger.BuildPrompt(new MatchmakerLedger.Facts
        {
            Name = "Sibylla",
            GenderWord = "woman",
            Age = 20,
            Station = "a Sturgian wanderer riding as a companion",
            StationTier = 1,
            Traits = "honest, daring",
            SelfText = "I love him in secret.",
            PartnerName = "Mizam",
            SharedStory = "He taught me of the Word.",
            WorldText = "A biblical, feudal world.",
        });

        Assert.Contains("Sibylla", prompt);
        Assert.Contains("CHECK: heart", prompt);
        Assert.Contains("ASK:", prompt);
        Assert.Contains("Mizam", prompt);
        Assert.Contains("between 500 and 5000 denars", prompt);
        Assert.DoesNotContain("switch_unused", prompt);
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
            "He taught me of the Word.", "I love him.", "I love him in secret.", "…");
        Assert.Contains("betrothed — we have already promised ourselves", prompt);
        Assert.Contains("where a promise was deliberately not yet given, honor that too", prompt);
        Assert.Contains("STAGE:", prompt);
    }

    // ------------------------- the words she reads -------------------------

    [Fact]
    public void RoadSection_SpeaksTheStage_AndMarksTheAsks()
    {
        var asks = new[]
        {
            new CourtshipText.AskView { Text = "He must be no stranger to the sword", Met = true },
            new CourtshipText.AskView { Text = "He must hold wealth enough", Met = false },
            new CourtshipText.AskView { Text = "He must not cage me", Met = null },
        };
        var section = CourtshipText.RoadSection("Mizam", CourtshipStage.Devotion, asks, false, false, "");

        Assert.Contains("my heart is truly given", section);
        Assert.Contains("never recite them as a list", section);
        Assert.Contains("and this, I sense, they have", section);
        Assert.Contains("my heart is not yet sure", section);
        Assert.Contains("only my own heart can judge", section);
    }

    [Fact]
    public void RoadSection_IsSilentAtNoneAndAtWed()
    {
        Assert.Equal(string.Empty, CourtshipText.RoadSection("Mizam", CourtshipStage.None, null, false, false, ""));
        Assert.Equal(string.Empty, CourtshipText.RoadSection("Mizam", CourtshipStage.Wed, null, false, false, ""));
    }

    [Fact]
    public void RoadSection_Betrothed_NamesTheMissingBlessing()
    {
        var barred = CourtshipText.RoadSection("Mizam", CourtshipStage.Betrothed, null, true, false, "Lucon");
        Assert.Contains("the blessing of Lucon", barred);
        Assert.Contains("cannot be given without it", barred);

        var blessed = CourtshipText.RoadSection("Mizam", CourtshipStage.Betrothed, null, true, true, "Lucon");
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
            // A file written before the courtship road existed: no courtship fields at all.
            File.WriteAllText(path, "{\"NpcId\":\"npc_1\",\"NpcName\":\"Sibylla\",\"Summary\":\"old story\"}");
            var old = store.LoadFrom(path, "npc_1");
            Assert.Equal(CourtshipStage.None, old.CourtshipStage);
            Assert.False(old.CourtshipSeeded);
            Assert.Empty(old.CourtshipAsks);
            Assert.Equal(-1, old.BetrothedGameDay);
            Assert.Equal(-1, old.FamilyBlessingDay);

            old.CourtshipStage = CourtshipStage.Betrothed;
            old.CourtshipSeeded = true;
            old.BetrothedGameDay = 91100.5;
            old.CourtshipAsks.Add(new CourtshipAsk { Text = "He must be true", Check = "trait Honor >= 1" });
            store.SaveTo(path, old);

            var reloaded = store.LoadFrom(path, "npc_1");
            Assert.Equal(CourtshipStage.Betrothed, reloaded.CourtshipStage);
            Assert.True(reloaded.CourtshipSeeded);
            Assert.Equal(91100.5, reloaded.BetrothedGameDay);
            Assert.Single(reloaded.CourtshipAsks);
            Assert.Equal("trait Honor >= 1", reloaded.CourtshipAsks[0].Check);
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
    public void Sheet_OffersTheTrothWhisper_OnlyWhenTheTrothsHandRidesAlong()
    {
        var withTool = Persona();
        withTool.CanTendTroth = true;
        var on = new PromptBuilder().Build(withTool, new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.Contains("My troth is my own to tend", on);
        Assert.Contains("the seal is wholly theirs", on);
        Assert.Contains("I never speak of steps, stages, or rules", on);

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.DoesNotContain("My troth is my own to tend", off);
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
        persona.CourtshipTerms = CourtshipText.RoadSection("Mizam", CourtshipStage.Devotion, null, false, false, "");
        var memory = new NpcMemory { Summary = "He taught me of the Word." };

        var system = new PromptBuilder().Build(persona, memory, "In the tavern." + PromptBuilder.MeetingSeparator + "And now Mizam comes to me.", "Mizam", "Hello")[0].Content;

        Assert.Contains("Where my heart stands with Mizam", system);
        // The road follows what he IS to her, and the arrival still lands last.
        Assert.True(system.IndexOf("What Mizam is to me") < system.IndexOf("Where my heart stands with Mizam"));
        Assert.True(system.IndexOf("Where my heart stands with Mizam") < system.IndexOf("And now Mizam comes to me."));
    }
}
