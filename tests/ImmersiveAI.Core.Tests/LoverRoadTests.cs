using System;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using Xunit;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The lover's fork (2026.08.15) — the bands it is measured in, the rails it is judged by, and the
/// words that must never quote either.
/// </summary>
public class LoverRoadTests
{
    // ------------------------------ the named bands ------------------------------

    [Fact]
    public void TheBandsClimbWithTheHeart_AndAgreeWithTheRoadsOwnGates()
    {
        Assert.Equal(HeartBand.Neutral, HeartBands.OfRelation(-40));
        Assert.Equal(HeartBand.Neutral, HeartBands.OfRelation(-1));
        Assert.Equal(HeartBand.Intrigued, HeartBands.OfRelation(0));
        Assert.Equal(HeartBand.Like, HeartBands.OfRelation(20));
        Assert.Equal(HeartBand.Love, HeartBands.OfRelation(40));
        Assert.Equal(HeartBand.DeepLove, HeartBands.OfRelation(70));

        // The two ladders must not drift apart: each band's floor IS the gate the marriage road
        // asks for at the matching depth, so "she is at love" and "she could be ready" agree.
        Assert.Equal(CourtshipRoad.WarmthRelationFloor, HeartBands.IntriguedFloor);
        Assert.Equal(CourtshipRoad.DevotionRelationFloor, HeartBands.LikeFloor);
        Assert.Equal(CourtshipRoad.ReadyRelationFloor, HeartBands.LoveFloor);

        // And deep love stands above everything the whole marriage road ever asks — which is what
        // makes it available as the lover's own, higher price.
        Assert.True(HeartBands.DeepLoveFloor > CourtshipRoad.ReadyRelationFloor);
    }

    [Fact]
    public void ADeclaredStageCushionsTheBand_ButOnlyWhileTheRoadIsStillBeingWalked()
    {
        // She said "my heart is given" and then the number dipped: the declaration is worth one
        // rung of cushion against a bad week.
        Assert.Equal(HeartBand.Like, HeartBands.Of(5, CourtshipStage.Devotion));
        Assert.Equal(HeartBand.Love, HeartBands.Of(25, CourtshipStage.Ready));

        // It never manufactures more than was declared.
        Assert.Equal(HeartBand.Love, HeartBands.Of(45, CourtshipStage.Ready));
        Assert.Equal(HeartBand.DeepLove, HeartBands.Of(80, CourtshipStage.Warmth));

        // PAST THE SEAL THERE IS NO CUSHION, and this is the fall's whole mechanism: a wife whose
        // heart has gone must read as gone, or the closed door arrives out of a clear sky.
        Assert.Equal(HeartBand.Neutral, HeartBands.Of(-30, CourtshipStage.Wed));
        Assert.Equal(HeartBand.Intrigued, HeartBands.Of(5, CourtshipStage.Betrothed));
    }

    [Fact]
    public void AWifesBedOutlastsTheFeeling_AndALoversDoesNot()
    {
        // The asymmetry, exactly as designed: the vow carries a wife well past the feeling, and a
        // lover has nothing carrying her at all.
        Assert.True(HeartBands.OpensTo(HeartBand.Intrigued, BondKind.Wed));
        Assert.False(HeartBands.OpensTo(HeartBand.Neutral, BondKind.Wed));

        Assert.False(HeartBands.OpensTo(HeartBand.Intrigued, BondKind.Lover));
        Assert.True(HeartBands.OpensTo(HeartBand.Like, BondKind.Lover));

        // No bond is no door, whatever the heart.
        Assert.False(HeartBands.OpensTo(HeartBand.DeepLove, BondKind.None));

        // And a lover asks MORE to begin than a wife ever asks to stay.
        Assert.True(HeartBands.LoverAsks > HeartBand.Love);
    }

    [Fact]
    public void TheBandsNeverSpeakANumber()
    {
        foreach (HeartBand band in Enum.GetValues(typeof(HeartBand)))
        {
            var word = HeartBands.Word(band);
            Assert.False(string.IsNullOrWhiteSpace(word));
            Assert.DoesNotContain(word, char.IsDigit(word[0]) ? word : "0123456789");
        }
    }

    // ------------------------------ the fork's rails ------------------------------

    private static LoverRoad.LoverFacts Deep() => new LoverRoad.LoverFacts
    {
        Stage = CourtshipStage.Devotion,
        Bond = LoverBond.None,
        Relation = 75,
    };

    [Fact]
    public void TheForkOpensFromDevotion_AndOnlyFromADeepLove()
    {
        Assert.Equal(LoverRoad.LoverVerdict.Allowed, LoverRoad.JudgeTake(Deep()));

        // The trunk must be walked first — this is not a step, it is the whole of her.
        var early = Deep(); early.Stage = CourtshipStage.Warmth;
        Assert.Equal(LoverRoad.LoverVerdict.RoadNotWalked, LoverRoad.JudgeTake(early));

        // And it asks more than any marriage gate does. 45 would carry her to Ready; not here.
        var fond = Deep(); fond.Relation = 45;
        Assert.Equal(LoverRoad.LoverVerdict.HeartNotDeepEnough, LoverRoad.JudgeTake(fond));

        // Ready is still the shared trunk, so the fork is open from there too.
        var ready = Deep(); ready.Stage = CourtshipStage.Ready;
        Assert.Equal(LoverRoad.LoverVerdict.Allowed, LoverRoad.JudgeTake(ready));
    }

    [Fact]
    public void APromiseOrAMarriageClosesTheFork()
    {
        var promised = Deep(); promised.Stage = CourtshipStage.Betrothed;
        Assert.Equal(LoverRoad.LoverVerdict.PromiseStands, LoverRoad.JudgeTake(promised));

        var wife = Deep(); wife.WedToPlayer = true;
        Assert.Equal(LoverRoad.LoverVerdict.AlreadyWed, LoverRoad.JudgeTake(wife));

        var already = Deep(); already.Bond = LoverBond.Lover;
        Assert.Equal(LoverRoad.LoverVerdict.AlreadyHis, LoverRoad.JudgeTake(already));

        // Scope, and it is a decision rather than an oversight: unattached women only.
        var bound = Deep(); bound.BoundElsewhere = true;
        Assert.Equal(LoverRoad.LoverVerdict.SheIsBound, LoverRoad.JudgeTake(bound));

        var refused = Deep(); refused.WorldRefusesThePair = true;
        Assert.Equal(LoverRoad.LoverVerdict.WorldRefuses, LoverRoad.JudgeTake(refused));
    }

    [Fact]
    public void ThePlayersOwnMarriageIsNeverABar()
    {
        // The entire point of the feature. There is deliberately no field for it, and this test is
        // here so that adding one later has to argue with something.
        var facts = Deep();
        Assert.Equal(LoverRoad.LoverVerdict.Allowed, LoverRoad.JudgeTake(facts));
        Assert.DoesNotContain(typeof(LoverRoad.LoverFacts).GetFields(),
            f => f.Name.IndexOf("PlayerWed", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void AFormerLoverCanBeTakenAgain_AndTheBondIsNeverErased()
    {
        var former = Deep(); former.Bond = LoverBond.Former;
        Assert.Equal(LoverRoad.LoverVerdict.Allowed, LoverRoad.JudgeTake(former));

        Assert.True(LoverRoad.CanPart(LoverBond.Lover));
        Assert.False(LoverRoad.CanPart(LoverBond.Former));
        Assert.False(LoverRoad.CanPart(LoverBond.None));
    }

    [Fact]
    public void SheLeavesWhenTheFeelingThins_AndNotBefore()
    {
        Assert.False(LoverRoad.HeartHasLeft(40, LoverBond.Lover));
        Assert.False(LoverRoad.HeartHasLeft(20, LoverBond.Lover));   // fondness still holds her
        Assert.True(LoverRoad.HeartHasLeft(19, LoverBond.Lover));
        Assert.True(LoverRoad.HeartHasLeft(-10, LoverBond.Lover));

        // Nothing to leave.
        Assert.False(LoverRoad.HeartHasLeft(-50, LoverBond.None));
        Assert.False(LoverRoad.HeartHasLeft(-50, LoverBond.Former));
    }

    // ------------------------------ what her house is paid ------------------------------

    [Fact]
    public void HerPriceIsHerGear_WithAFloorSoStrippingHerIsNoExploit()
    {
        // The gear is the anchor when it is worth anything.
        Assert.Equal(40000, LoverRoad.RansomReckoning(40000, 5));

        // And the floor bites when it is not — a lord's daughter in a plain dress is cheap to look
        // at, never cheap to lose.
        Assert.Equal(5 * LoverRoad.RansomFloorPerStationRung, LoverRoad.RansomReckoning(300, 5));
        Assert.True(LoverRoad.RansomReckoning(0, 6) > LoverRoad.RansomReckoning(0, 2));

        // Nonsense in, sane out.
        Assert.Equal(0, LoverRoad.RansomReckoning(-500, 0));
    }

    [Fact]
    public void TheRansomHagglesOnTheSameRailAsEveryOtherBargain()
    {
        LoverRoad.RansomRails(10000, 30, out int min, out int max);
        Assert.Equal(7000, min);
        Assert.Equal(13000, max);

        // Zero percent is a legitimate way to play: the figure simply does not move.
        LoverRoad.RansomRails(10000, 0, out min, out max);
        Assert.Equal(10000, min);
        Assert.Equal(10000, max);

        // And a wild config cannot invert the rail.
        LoverRoad.RansomRails(1000, 500, out min, out max);
        Assert.True(min >= 0 && max >= min);
    }

    // ------------------------------ the words ------------------------------

    [Fact]
    public void EveryRefusalSheReadsIsNumberless()
    {
        foreach (LoverRoad.LoverVerdict verdict in Enum.GetValues(typeof(LoverRoad.LoverVerdict)))
        {
            var words = LoverText.TakeRefusal(verdict, "Mizam");
            Assert.False(string.IsNullOrWhiteSpace(words));
            // A heart reciting its own thresholds is the least human thing this mod could do.
            Assert.DoesNotContain(words, c => char.IsDigit(c));
            Assert.DoesNotContain("stage", words, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("relation", words, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ThePlayerIsToldPlainlyWhatSheMayNotSay()
    {
        // The 2026.08.15 lesson: a rail nobody can see is indistinguishable from a broken mod.
        foreach (LoverRoad.LoverVerdict verdict in Enum.GetValues(typeof(LoverRoad.LoverVerdict)))
            Assert.False(string.IsNullOrWhiteSpace(LoverText.TakeRefusalForPlayer(verdict)));
    }

    [Fact]
    public void TheAntiPretenceRailSaysTheOneThingItMustSay()
    {
        // She will otherwise narrate the bond into being the moment she is told "not yet".
        Assert.Contains("no words of ours make me his", LoverText.WordsDoNotBind);
        Assert.Contains("my own hand", LoverText.WordsDoNotBind);
        Assert.Contains("it has not happened", LoverText.WordsDoNotBind);
    }

    [Fact]
    public void HerSheetStatesTheWorldsPosition_AndNeverHerFeelingAboutIt()
    {
        var sheet = LoverText.BondSection("Mizam", LoverBond.Lover,
            ridesWithHim: true, boughtFromHerHouse: true, hisOtherWomen: 1);

        // The facts of her place.
        Assert.Contains("I am not his wife", sheet);
        Assert.Contains("no vow anywhere says so", sheet);

        // The era's norm, in the era's own grammar: what is HELD, by everyone — never what she feels.
        Assert.Contains("Such is the order of the world", sheet);
        Assert.Contains("is counted shame", sheet);
        Assert.DoesNotContain("I feel ashamed", sheet);

        // And the explicit permission NOT to have a settled stance, which is the one line standing
        // between sixty distinct women and sixty identical ones.
        Assert.Contains("What I myself make of it is mine alone", sheet);
        Assert.Contains("not obliged to have an answer ready", sheet);

        // Her asking is a door, never a duty — a lover who MUST push is a quest-giver.
        Assert.Contains("There are things I could ask of him, if I wanted them", sheet);
        Assert.Contains("is mine to decide", sheet);

        Assert.Equal(string.Empty, LoverText.BondSection("Mizam", LoverBond.None, false, false));
    }

    [Fact]
    public void AFormerBondIsRememberedAndNeverErased()
    {
        var sheet = LoverText.BondSection("Mizam", LoverBond.Former,
            ridesWithHim: false, boughtFromHerHouse: true);

        Assert.Contains("his, once", sheet);
        Assert.Contains("neither of us can unsay it", sheet);
        // The gold does not come back and she did not go home. Both of them carry that.
        Assert.Contains("never given back", sheet);
    }

    [Fact]
    public void TheHeadOfHerHouseIsPaid_AndStaysUnappeased()
    {
        var terms = LoverText.RansomTerms("Mizam", "Sibylla", "a daughter of our house",
            12000, 8400, 15600, haggle: true);

        Assert.Contains("has not asked for her hand", terms);
        Assert.Contains("12000 denars", terms);
        Assert.Contains("8400", terms);
        Assert.Contains("15600", terms);
        // The whole point of the transaction, in one line.
        Assert.Contains("It settles nothing else", terms);

        var fixedPrice = LoverText.RansomTerms("Mizam", "Sibylla", "a daughter of our house",
            12000, 12000, 12000, haggle: false);
        Assert.Contains("does not move", fixedPrice);
    }

    // ------------------------------ where it sits in the sheet ------------------------------

    private static NpcPersona Persona() => new NpcPersona
    {
        Name = "Sibylla",
        RoleDescription = "a Sturgian wanderer",
        PersonalityDescription = "warm, stubborn",
    };

    [Fact]
    public void TheBondSitsBesideWhatHeIsToHer_AndBeforeTheMoment()
    {
        var persona = Persona();
        persona.LoverTerms = LoverText.BondSection("Mizam", LoverBond.Lover, true, false);
        var memory = new NpcMemory { Summary = "He taught me of the Word." };

        var system = new PromptBuilder().Build(persona, memory,
            "In the tavern." + PromptBuilder.MeetingSeparator + "And now Mizam comes to me.",
            "Mizam", "Hello")[0].Content;

        Assert.Contains("What I am to Mizam", system);
        // Beside her deep memory of him — that is where what he IS to her belongs — and the
        // arrival still lands last, as it has since the sheet was reordered.
        Assert.True(system.IndexOf("What Mizam is to me", StringComparison.Ordinal)
                  < system.IndexOf("What I am to Mizam", StringComparison.Ordinal));
        Assert.True(system.IndexOf("What I am to Mizam", StringComparison.Ordinal)
                  < system.IndexOf("And now Mizam comes to me.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheAntiPretenceRailHangsOffTheHand_NotOffTheBond()
    {
        // It is needed MOST before the bond exists — which is exactly when there is no section for
        // it to hang from. Getting this backwards would arm the rail only once it was too late.
        var reaching = Persona();
        reaching.CanOfferSelf = true;
        var withHand = new PromptBuilder().Build(reaching, new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.Contains("no words of ours make me his", withHand);

        var quiet = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Mizam", "Hello")[0].Content;
        Assert.DoesNotContain("no words of ours make me his", quiet);
    }

    // ------------------------------ the rungs of the hearth ------------------------------

    [Fact]
    public void ALoverSitsBetweenACompanionAndTheWife_AndNearerTheCompanion()
    {
        // Anton's 4.5 was for the one he is WED to. Handing a lover the same would make the fall
        // louder than the marriage, which inverts the point of the whole batch.
        Assert.True(Initiation.InitiationScorer.LoverHearthFactor > Initiation.InitiationScorer.CompanionHearthFactor);
        Assert.True(Initiation.InitiationScorer.LoverHearthFactor < Initiation.InitiationScorer.SpouseHearthFactor);
        Assert.True(Initiation.InitiationScorer.SpouseHearthFactor
                  > Initiation.InitiationScorer.LoverHearthFactor * 1.5);
    }

    // ------------------------------ where it is kept ------------------------------

    [Fact]
    public void TheBondRidesHerMemory_SoASaveRewindsIt()
    {
        var memory = new NpcMemory();
        Assert.Equal(LoverBond.None, memory.LoverBond);
        Assert.False(memory.HasLoverHistory);
        Assert.Equal(-1, memory.LoverSinceDay);
        Assert.Equal(0, memory.LoverRansomPaid);

        memory.LoverBond = LoverBond.Lover;
        memory.LoverSinceDay = 91.0;
        memory.LoverRansomPaid = 12000;
        Assert.True(memory.HasLoverHistory);

        // Ending it never erases what was paid, or that it happened at all.
        memory.LoverBond = LoverBond.Former;
        memory.LoverEndedDay = 140.0;
        Assert.True(memory.HasLoverHistory);
        Assert.Equal(12000, memory.LoverRansomPaid);
        Assert.Equal(91.0, memory.LoverSinceDay);
    }
}
