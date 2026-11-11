using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Handmaids;
using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Core.Tests;

/// <summary>The handmaid's courtship (2026.11.11): her ten small doubts in the schema, her own
/// road wording in place of the general one, and the station gate that never binds her.</summary>
public class HandmaidCourtshipTests
{
    // ── the ten small doubts ────────────────────────────────────────────────────────────

    [Fact]
    public void TenDistinctLinesAreWritten()
    {
        Assert.Equal(10, HandmaidDoubts.Lines.Count);
        Assert.Equal(10, HandmaidDoubts.Lines.Distinct().Count());
        Assert.Equal("Why have I found grace in my lord's eyes, a stranger with no house?", HandmaidDoubts.Lines[0]);
        Assert.Equal("I do not know if I am allowed to be this happy.", HandmaidDoubts.Lines[9]);
        Assert.Equal(3, HandmaidDoubts.Pick);
    }

    [Fact]
    public void BeforeSheWeighsTheTenRideAsTheSchemasChoices()
    {
        var hand = HandmaidDoubts.Hand(new List<CourtshipMisgiving>(), weighed: false)!;
        Assert.Equal("weigh_misgivings", hand.Name);

        var action = hand.Parameters.Single(p => p.Name == "action");
        Assert.Equal(new[] { CourtshipMisgivings.ActSetDown }, action.AllowedValues);

        var picks = hand.Parameters.Where(p => p.Name != "action").ToList();
        Assert.Equal(HandmaidDoubts.PickParameters, picks.Select(p => p.Name));
        Assert.All(picks, p => Assert.Equal(HandmaidDoubts.Lines, p.AllowedValues));
        Assert.True(picks[0].Required);
        Assert.False(picks[1].Required);
        Assert.False(picks[2].Required);
        // No free-text field at all: nothing of her own words can be written.
        Assert.DoesNotContain(hand.Parameters, p => p.AllowedValues == null && p.Name != "note");
    }

    [Fact]
    public void OnceChosenTheOtherSevenAreGoneAndNothingNewCanBeSetDown()
    {
        var list = new List<CourtshipMisgiving>();
        bool weighed = false;
        var pick = new[] { HandmaidDoubts.Lines[1], HandmaidDoubts.Lines[2], HandmaidDoubts.Lines[6] };
        Assert.Equal(HandmaidDoubts.Verdict.Chosen, HandmaidDoubts.Choose(list, ref weighed, pick, out _));

        var hand = HandmaidDoubts.Hand(list, weighed)!;
        var action = hand.Parameters.Single(p => p.Name == "action");
        Assert.DoesNotContain(CourtshipMisgivings.ActSetDown, action.AllowedValues!);
        Assert.DoesNotContain(CourtshipMisgivings.ActRevise, action.AllowedValues!);
        Assert.Contains(CourtshipMisgivings.ActSettle, action.AllowedValues!);
        Assert.Contains(CourtshipMisgivings.ActRelease, action.AllowedValues!);
        Assert.Contains(CourtshipMisgivings.ActReopen, action.AllowedValues!);
        Assert.Equal(pick, hand.Parameters.Single(p => p.Name == "which").AllowedValues);

        // The unchosen lines cost nothing anywhere in the hand.
        var everything = hand.Description + string.Join(" ", hand.Parameters.Select(p =>
            p.Description + string.Join(" ", p.AllowedValues ?? Array.Empty<string>())));
        foreach (var line in HandmaidDoubts.Lines.Except(pick))
            Assert.DoesNotContain(line, everything);
    }

    [Fact]
    public void ThreePicksAreSetDownWholeEvenWithASemicolon()
    {
        var list = new List<CourtshipMisgiving>();
        bool weighed = false;
        var pick = new[] { HandmaidDoubts.Lines[2], HandmaidDoubts.Lines[3], HandmaidDoubts.Lines[7] };
        var verdict = HandmaidDoubts.Choose(list, ref weighed, pick, out var chosen);

        Assert.Equal(HandmaidDoubts.Verdict.Chosen, verdict);
        Assert.True(weighed);
        Assert.Equal(pick, list.Select(m => m.Text));
        Assert.Equal(pick, chosen);
        Assert.Contains(list, m => m.Text.Contains(';'));
        Assert.Equal(3, CourtshipMisgivings.OpenCount(list));
    }

    [Fact]
    public void OneOrTwoAreAcceptedAndRepeatsFold()
    {
        var list = new List<CourtshipMisgiving>();
        bool weighed = false;
        var verdict = HandmaidDoubts.Choose(list, ref weighed,
            new[] { HandmaidDoubts.Lines[4], HandmaidDoubts.Lines[4].ToUpperInvariant(), null, "" }, out var chosen);
        Assert.Equal(HandmaidDoubts.Verdict.Chosen, verdict);
        Assert.Single(list);
        Assert.Equal(HandmaidDoubts.Lines[4], list[0].Text);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("None.")]
    [InlineData("my heart is clear")]
    public void NoneIsRefusedForHer(string none)
    {
        var list = new List<CourtshipMisgiving>();
        bool weighed = false;
        var verdict = HandmaidDoubts.Choose(list, ref weighed, new[] { none }, out _);
        Assert.Equal(HandmaidDoubts.Verdict.NoneRefused, verdict);
        Assert.Empty(list);
        Assert.False(weighed);
        Assert.Contains("one to three", HandmaidDoubts.Answer(verdict, 0));
        Assert.Contains("\"none\"", HandmaidDoubts.Answer(verdict, 0));
    }

    [Fact]
    public void FreeTextIsRefusedWholeAndNamesWhatIsWanted()
    {
        var list = new List<CourtshipMisgiving>();
        bool weighed = false;
        var verdict = HandmaidDoubts.Choose(list, ref weighed,
            new[] { HandmaidDoubts.Lines[0], "I fear his mother will not like me." }, out var chosen);
        Assert.Equal(HandmaidDoubts.Verdict.NotHers, verdict);
        Assert.Empty(list);
        Assert.Empty(chosen);
        Assert.False(weighed);
        var answer = HandmaidDoubts.Answer(verdict, 0);
        Assert.Contains("exactly as it stands", answer);
        Assert.Contains("never in words of my own", answer);

        Assert.Equal(HandmaidDoubts.Verdict.NotHers, HandmaidDoubts.Choose(list, ref weighed, new string?[] { null }, out _));
    }

    [Fact]
    public void AfterTheFirstWeighingSheCanNeverSetDownAgain()
    {
        var list = new List<CourtshipMisgiving>();
        bool weighed = false;
        HandmaidDoubts.Choose(list, ref weighed, new[] { HandmaidDoubts.Lines[0] }, out _);
        CourtshipMisgivings.Settle(list, HandmaidDoubts.Lines[0], "He said I am his choice.");
        Assert.Equal(0, CourtshipMisgivings.OpenCount(list));

        var verdict = HandmaidDoubts.Choose(list, ref weighed, new[] { HandmaidDoubts.Lines[5] }, out _);
        Assert.Equal(HandmaidDoubts.Verdict.AlreadyChosen, verdict);
        Assert.Single(list);
        // And the weighed-but-empty corner of an old save still refuses a new weighing.
        bool w2 = true;
        Assert.Equal(HandmaidDoubts.Verdict.AlreadyChosen,
            HandmaidDoubts.Choose(new List<CourtshipMisgiving>(), ref w2, new[] { HandmaidDoubts.Lines[5] }, out _));
        Assert.Null(HandmaidDoubts.Hand(new List<CourtshipMisgiving>(), weighed: true));
    }

    [Fact]
    public void SettleReleaseAndReopenWorkOnHerLinesAsForAnyone()
    {
        var list = new List<CourtshipMisgiving>();
        bool weighed = false;
        HandmaidDoubts.Choose(list, ref weighed, new[] { HandmaidDoubts.Lines[2], HandmaidDoubts.Lines[3] }, out _);
        Assert.NotNull(CourtshipMisgivings.Settle(list, HandmaidDoubts.Lines[2], "He calls me fair."));
        Assert.NotNull(CourtshipMisgivings.Release(list, HandmaidDoubts.Lines[3]));
        Assert.Equal(0, CourtshipMisgivings.OpenCount(list));
        Assert.NotNull(CourtshipMisgivings.Reopen(list, HandmaidDoubts.Lines[2]));
        Assert.Equal(1, CourtshipMisgivings.OpenCount(list));
    }

    [Theory]
    [InlineData("  \"He took me in from pity. Is it pity still?\" ", 3)]
    [InlineData("i bring no dowry. i have nothing to give him but myself.", 1)]
    public void CanonicalForgivesCaseAndQuotesOnly(string raw, int index)
    {
        Assert.Equal(HandmaidDoubts.Lines[index], HandmaidDoubts.Canonical(raw));
        Assert.Null(HandmaidDoubts.Canonical("He took me in from pity."));
    }

    // ── the station gate ────────────────────────────────────────────────────────────────

    private static CourtshipRoad.StepFacts Facts(CourtshipStage stage, bool ofHisHouse) => new()
    {
        Stage = stage,
        Relation = 60,
        PlayerClanTier = 0,
        HerStationTier = 6, // his own clan, if it rules a realm
        CharmSlack = 0,
        MisgivingsWeighed = true,
        OpenMisgivings = 0,
        OfHisHouse = ofHisHouse,
    };

    [Theory]
    [InlineData(CourtshipStage.Devotion)]   // → Ready
    [InlineData(CourtshipStage.Ready)]      // → the betrothal
    [InlineData(CourtshipStage.Betrothed)]  // → the wedding
    public void TheStationGateNeverBindsHer(CourtshipStage stage)
    {
        Assert.Equal(CourtshipRoad.StepVerdict.StationTooFar, CourtshipRoad.JudgeForward(Facts(stage, false)));
        Assert.Equal(CourtshipRoad.StepVerdict.Allowed, CourtshipRoad.JudgeForward(Facts(stage, true)));
    }

    [Fact]
    public void EveryOtherRailStillBindsHer()
    {
        var low = Facts(CourtshipStage.Devotion, true); low.Relation = 39;
        Assert.Equal(CourtshipRoad.StepVerdict.HeartNotThere, CourtshipRoad.JudgeForward(low));

        var unweighed = Facts(CourtshipStage.Devotion, true); unweighed.MisgivingsWeighed = false;
        Assert.Equal(CourtshipRoad.StepVerdict.MisgivingsUnweighed, CourtshipRoad.JudgeForward(unweighed));

        var open = Facts(CourtshipStage.Ready, true); open.OpenMisgivings = 1;
        Assert.Equal(CourtshipRoad.StepVerdict.MisgivingsRemain, CourtshipRoad.JudgeForward(open));

        var fresh = Facts(CourtshipStage.Betrothed, true); fresh.DaysBetrothed = 1; fresh.MinBetrothalDays = 3;
        Assert.Equal(CourtshipRoad.StepVerdict.TrothTooFresh, CourtshipRoad.JudgeForward(fresh));

        var warmth = Facts(CourtshipStage.None, true); warmth.Relation = -5;
        Assert.Equal(CourtshipRoad.StepVerdict.HeartNotThere, CourtshipRoad.JudgeForward(warmth));
    }

    // ── her road section ────────────────────────────────────────────────────────────────

    private static List<CourtshipText.MisgivingView> Views(params (string text, bool settled)[] items) =>
        items.Select(i => new CourtshipText.MisgivingView { Text = i.text, Settled = i.settled }).ToList();

    [Fact]
    public void HerSectionReadsHerWordingInPlaceOfTheGeneral()
    {
        var mine = Views((HandmaidDoubts.Lines[3], false), (HandmaidDoubts.Lines[7], false));
        var hers = CourtshipText.RoadSection("Toni", CourtshipStage.Devotion, mine, true,
            blessingRequired: true, blessingGiven: false, headOfHouse: "Toni", handmaid: true);
        var plain = CourtshipText.RoadSection("Toni", CourtshipStage.Devotion, mine, true,
            blessingRequired: false, blessingGiven: false, headOfHouse: "");

        Assert.Contains(HandmaidText.RoadDoubtsWay, hers);
        Assert.Contains(HandmaidText.RoadStepsWay, hers);
        Assert.Contains(HandmaidText.RoadHandWaits, hers);
        Assert.Contains("- He took me in from pity. Is it pity still? (this still stands in me)", hers);
        Assert.Contains(CourtshipText.WordsDoNotWed, hers);
        // The general treatment is replaced, not repeated beside hers.
        Assert.DoesNotContain("never for a warm word alone", hers);
        Assert.DoesNotContain("circle back", hers);
        Assert.DoesNotContain("blessing", hers);
        Assert.Contains("never for a warm word alone", plain);
        Assert.DoesNotContain(HandmaidText.RoadDoubtsWay, plain);
        Assert.DoesNotContain(HandmaidText.RoadStepsWay, plain);
    }

    [Fact]
    public void HerHourToChooseReplacesTheWeighingInvitation()
    {
        var hers = CourtshipText.RoadSection("Toni", CourtshipStage.Devotion, null, false, false, false, "", handmaid: true);
        Assert.Contains(HandmaidText.RoadChooseNow, hers);
        Assert.DoesNotContain("a few at the very most", hers);
        // The ten never ride the sheet: they live in the schema.
        foreach (var line in HandmaidDoubts.Lines) Assert.DoesNotContain(line, hers);

        var warmth = CourtshipText.RoadSection("Toni", CourtshipStage.Warmth, null, false, false, false, "", handmaid: true);
        Assert.DoesNotContain(HandmaidText.RoadChooseNow, warmth);
        Assert.Contains(HandmaidText.RoadStepsWay, warmth);
    }

    [Fact]
    public void ReadyWithNothingStandingWaitsWithoutTheOrdinaryRestraint()
    {
        var mine = Views((HandmaidDoubts.Lines[1], true));
        var hers = CourtshipText.RoadSection("Toni", CourtshipStage.Ready, mine, true, false, false, "", handmaid: true);
        Assert.Contains("So I wait now to be asked.", hers);
        Assert.Contains(HandmaidText.RoadAllAnswered, hers);
        Assert.DoesNotContain("I do not press", hers);
        Assert.DoesNotContain("go looking for no new ones", hers);
        Assert.Empty(CourtshipText.RoadSection("Toni", CourtshipStage.Wed, mine, true, false, false, "", handmaid: true));
    }

    [Fact]
    public void TheOrdinarySectionIsUnchangedByTheNewParameter()
    {
        var mine = Views(("I fear the road.", false), ("Will he stay?", true));
        foreach (var stage in new[] { CourtshipStage.Warmth, CourtshipStage.Devotion, CourtshipStage.Ready, CourtshipStage.Betrothed })
            Assert.Equal(
                CourtshipText.RoadSection("Toni", stage, mine, true, true, false, "Ealric"),
                CourtshipText.RoadSection("Toni", stage, mine, true, true, false, "Ealric", handmaid: false));
    }

    // ── her troth hand ──────────────────────────────────────────────────────────────────

    private const string OrdinaryTendDescription =
        "My troth is mine to tend, and marriage is a road of steps: liking, love, readiness, the " +
        "betrothal, the wedding day. The first arrivals — liking, then love — are no proposal " +
        "and speak of no marriage: they are my heart's own record, and I set them down freely " +
        "the day they are true, without waiting to be sure of more. I move closer only when real " +
        "talks have truly earned the next depth — one step in a talk at most, never for one warm " +
        "word alone, never to test — " +
        "and I set each arrival down in the very breath I feel it, above all the day I find I " +
        "would give my hand were the word spoken. ONLY when marriage has been openly spoken and " +
        "welcomed between us do I lay our betrothal (or, betrothed already, our wedding day) " +
        "before them; what I lay settles nothing — the seal is wholly theirs, and if they let it " +
        "lie I do not press. No words of ours make a marriage: not vows, not a temple, not a " +
        "ceremony we tell each other about — until I lay the day by this hand and they seal it " +
        "by theirs, we are not wed, and I neither say we are nor let the talk drift as though we " +
        "were. I move apart when a wound or a true doubt sets me back — always mine to do; and " +
        "if I am already theirs without any vow, moving apart ends THAT, whole and at once. I " +
        "never speak of steps, stages, or rules: my heart simply knows where it stands.";

    [Fact]
    public void TheOrdinaryTrothHandIsWordForWordWhatItWas()
    {
        Assert.Equal("tend_courtship", CourtshipHands.Tend.Name);
        Assert.Equal(OrdinaryTendDescription, CourtshipHands.Tend.Description);
        Assert.Equal(new[] { "closer", "apart" }, CourtshipHands.Tend.Parameters[0].AllowedValues);
    }

    [Fact]
    public void HerTrothHandReadsHerPaceInPlaceOfTheOrdinary()
    {
        var hers = CourtshipHands.TendForHandmaid.Description;
        Assert.Contains(HandmaidText.RoadStepsWay, hers);
        Assert.DoesNotContain("never for one warm word alone", hers);
        Assert.Contains("ONLY when marriage has been openly spoken", hers);
        Assert.Contains("No words of ours make a marriage", hers);
        Assert.Equal(CourtshipHands.Tend.Name, CourtshipHands.TendForHandmaid.Name);
        Assert.Equal(CourtshipHands.Tend.Parameters.Select(p => p.Name),
            CourtshipHands.TendForHandmaid.Parameters.Select(p => p.Name));
    }

    // ── refusals, the guide, the role line ──────────────────────────────────────────────

    [Fact]
    public void HerRefusalsTeachHerOwnWay()
    {
        var plain = CourtshipText.ForwardRefusal(CourtshipRoad.StepVerdict.MisgivingsRemain, "Toni");
        var hers = CourtshipText.ForwardRefusal(CourtshipRoad.StepVerdict.MisgivingsRemain, "Toni", handmaid: true);
        Assert.Contains("when life truly answers it", plain);
        Assert.Contains("kind word is answer enough", hers);
        Assert.Equal(CourtshipText.ForwardRefusal(CourtshipRoad.StepVerdict.HeartNotThere, "Toni"),
            CourtshipText.ForwardRefusal(CourtshipRoad.StepVerdict.HeartNotThere, "Toni", handmaid: true));
    }

    [Fact]
    public void TheGuideTellsThePlayerAKindWordAnswersHer()
    {
        var f = new CourtshipText.NextStepFacts
        {
            Stage = CourtshipStage.Devotion, Verdict = CourtshipRoad.StepVerdict.MisgivingsRemain,
            NpcName = "Neya", OpenMisgivings = 2,
        };
        Assert.Contains("Only life answers a doubt", CourtshipText.WhatNow(f));
        f.Handmaid = true;
        Assert.Equal("2 small doubts of hers still stand — read them below. A plain kind word from you answers each.",
            CourtshipText.WhatNow(f));

        f.WorldBlock = HandmaidText.MarriedWallForPlayer;
        Assert.Equal("Nothing can be sealed while you already have a wife; without a mod such as Marry Anyone the world allows only one.",
            CourtshipText.WhatNow(f));
    }

    [Fact]
    public void HerRoleLineIsHerHouseholdNotANobleClan()
    {
        Assert.Equal("A Sturgian woman of Toni's household, his handmaid", HandmaidText.RoleLine("Sturgian", "Toni", wed: false));
        Assert.Equal("A Sturgian woman of Toni's household, his wife", HandmaidText.RoleLine("Sturgian", "Toni", wed: true));
        Assert.Equal("A woman of my lord's household, his handmaid", HandmaidText.RoleLine(null, " ", wed: false));
    }
}
