using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Handmaids;
using ImmersiveAI.Core.Nights;
using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// MORE THAN ONE WIFE (2026.11.11): the gates as a function of the setting, the words the player
/// and the women read, and the nights' leak naming what the other woman is — never how to feel.
/// </summary>
public class SecondWifeTests
{
    // ------------------------------ the law ------------------------------

    [Theory]
    // married, setting OFF — today's rule exactly: the hand is barred; the trunk only when the
    // lover's road is closed.
    [InlineData(true, false, true, true, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, false, true)]
    // married, setting ON — his marriage bars nothing.
    [InlineData(true, true, true, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, false, false, false)]
    // unmarried — never barred by a marriage he does not have.
    [InlineData(false, false, true, true, false)]
    [InlineData(false, true, true, false, false)]
    public void HisOwnMarriageBarsTheRoad_OnlyWhileTheSettingIsOff(
        bool married, bool setting, bool forHand, bool loversOpen, bool barred)
    {
        Assert.Equal(barred, SecondWifeLaw.PlayerMarriageBars(married, setting, forHand, loversOpen));
    }

    [Fact]
    public void ANewWedding_IsRefusedOnlyToAMarriedManWithTheSettingOff()
    {
        Assert.True(SecondWifeLaw.MayWedNow(playerMarried: false, moreThanOneWife: false));
        Assert.True(SecondWifeLaw.MayWedNow(playerMarried: true, moreThanOneWife: true));
        Assert.False(SecondWifeLaw.MayWedNow(playerMarried: true, moreThanOneWife: false));
    }

    [Fact]
    public void ALoversRoadToAWedding_IsShownOnlyWithTheSettingOn()
    {
        Assert.False(SecondWifeLaw.RoadShownForLover(isLover: true, moreThanOneWife: false));
        Assert.True(SecondWifeLaw.RoadShownForLover(isLover: true, moreThanOneWife: true));
        Assert.True(SecondWifeLaw.RoadShownForLover(isLover: false, moreThanOneWife: false));
    }

    [Fact]
    public void ALoverWhoIsWed_IsALoverNoMore()
    {
        Assert.Equal(LoverBond.None, SecondWifeLaw.BondAfterWedding(LoverBond.Lover));
        Assert.Equal(LoverBond.None, SecondWifeLaw.BondAfterWedding(LoverBond.Former));
        Assert.Equal(LoverBond.None, SecondWifeLaw.BondAfterWedding(LoverBond.None));
    }

    // ------------------------------ the player's words ------------------------------

    [Fact]
    public void TheWall_NamesOurSetting_NeverMarryAnyone()
    {
        var his = SecondWifeText.PlayerWedWall(playerIsFemale: false);
        Assert.Equal("you already have a wife; turn on \"More than one wife\" in the mod's settings to wed again", his);
        Assert.StartsWith("you already have a husband;", SecondWifeText.PlayerWedWall(playerIsFemale: true));
        Assert.DoesNotContain("Marry Anyone", his);
        Assert.DoesNotContain("Marry Anyone", HandmaidText.MarriedWallForPlayer);
        Assert.Contains("\"More than one wife\"", HandmaidText.MarriedWallForPlayer);
    }

    [Fact]
    public void TheMarryAnyoneNotice_IsOneLineAndNamesOurSetting()
    {
        Assert.DoesNotContain("\n", SecondWifeText.MarryAnyoneNotice);
        Assert.Contains("\"More than one wife\"", SecondWifeText.MarryAnyoneNotice);
    }

    // ------------------------------ the household's words ------------------------------

    [Fact]
    public void CoWives_AreNamedForWhatTheyAre_NotKinswomen()
    {
        Assert.Equal("my husband's other wife", SecondWifeText.CoSpouseWord(otherIsFemale: true));
        Assert.Equal("my wife's other husband", SecondWifeText.CoSpouseWord(otherIsFemale: false));
    }

    [Fact]
    public void TheHouseholdFact_IsEmptyForASingleWife_AndAPlainLineOtherwise()
    {
        Assert.Equal(string.Empty, SecondWifeText.OtherSpousesLine("Vulgrim", new List<string>(), speakerIsFemale: true));
        Assert.Equal("Vulgrim has another lawful wife besides me: Yasmin.",
            SecondWifeText.OtherSpousesLine("Vulgrim", new[] { "Yasmin" }, speakerIsFemale: true));
        Assert.Equal("Vulgrim has other lawful wives besides me: Yasmin and Ira.",
            SecondWifeText.OtherSpousesLine("Vulgrim", new[] { "Yasmin", "Ira" }, speakerIsFemale: true));
        Assert.Equal("Sibylla has another lawful husband besides me: Tor.",
            SecondWifeText.OtherSpousesLine("Sibylla", new[] { "Tor" }, speakerIsFemale: false));
    }

    [Fact]
    public void TheWeddingBeat_IsAFactInHerVoice_AndNeverTellsHerHowToFeel()
    {
        var beat = SecondWifeText.FurtherWeddingBeat("Vulgrim", "Yasmin", newSpouseIsFemale: true);
        Assert.Equal("Word of the household: Vulgrim has wed Yasmin this day; Yasmin is his lawful wife now.", beat);
        foreach (var feeling in new[] { "jealous", "hurt", "wrong", "betray", "should", "must", "feel" })
            Assert.DoesNotContain(feeling, beat, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheChronicler_IsToldOfTheMarriageAlreadyStanding_OnlyAtAFurtherWedding()
    {
        var facts = new WeddingText.Facts { SpouseName = "Yasmin", PlayerName = "Vulgrim" };
        var first = WeddingText.BuildFeastPrompt(facts);
        Assert.DoesNotContain("already wed", first);

        facts.AlreadyWedTo = new List<string> { "Sibylla" };
        var further = WeddingText.BuildFeastPrompt(facts);
        Assert.Contains("- Vulgrim is already wed to Sibylla; this is a further marriage, lawful in their house, and that marriage stands.", further);
        Assert.Contains("already wed to Sibylla", WeddingText.BuildNightPrompt(facts, string.Empty));
    }

    // ------------------------------ the nights' leak ------------------------------

    [Theory]
    [InlineData(true, true, OtherWomanIs.HisOtherWife)]
    [InlineData(true, false, OtherWomanIs.HisWife)]
    [InlineData(false, true, OtherWomanIs.HisLover)]
    [InlineData(false, false, OtherWomanIs.HisOtherLover)]
    public void WhatTheOtherWomanIs_ComesFromTwoPlainFacts(bool otherIsWife, bool listenerIsWife, OtherWomanIs expected)
    {
        Assert.Equal(expected, NightText.OtherWomanIsFor(otherIsWife, listenerIsWife));
    }

    private static NightRecord Elsewhere(OtherWomanIs otherIs, bool hearsay = false) => new NightRecord
    {
        GameDay = 99, WifeId = "w1", WifeName = "Sibylla", Kind = NightKind.Elsewhere,
        OtherName = "Yasmin", OtherIs = otherIs, ByHearsay = hearsay,
    };

    [Fact]
    public void AWife_LearnsHeWasWithHisOtherLawfulWife_PlainlyAndNamed()
    {
        Assert.Equal("Last night he went to Yasmin, his other lawful wife.",
            NightText.LineFor(Elsewhere(OtherWomanIs.HisOtherWife), today: 100, inFull: false));
        Assert.Equal("Last night word reached me that he spent the night with Yasmin, his other lawful wife.",
            NightText.LineFor(Elsewhere(OtherWomanIs.HisOtherWife, hearsay: true), today: 100, inFull: false));
    }

    [Fact]
    public void AWife_LearnsTheOtherWasHisLover_NotHisWife()
    {
        Assert.Equal("Last night he went to Yasmin, who is his lover, not his wife.",
            NightText.LineFor(Elsewhere(OtherWomanIs.HisLover), today: 100, inFull: false));
        Assert.Equal("Last night he went to Yasmin, his lawful wife.",
            NightText.LineFor(Elsewhere(OtherWomanIs.HisWife), today: 100, inFull: false));
    }

    [Fact]
    public void ANightRecordedBeforeThis_ReadsExactlyAsItAlwaysDid()
    {
        Assert.Equal("Last night he went to Yasmin, and not to me.",
            NightText.LineFor(Elsewhere(OtherWomanIs.Unknown), today: 100, inFull: false));
        Assert.Equal("Last night word reached me that he spent the night with Yasmin.",
            NightText.LineFor(Elsewhere(OtherWomanIs.Unknown, hearsay: true), today: 100, inFull: false));
    }

    [Fact]
    public void TheLeak_NeverTellsHerHowToFeel()
    {
        foreach (OtherWomanIs kind in Enum.GetValues(typeof(OtherWomanIs)))
        {
            var line = NightText.LineFor(Elsewhere(kind), today: 100, inFull: false);
            foreach (var feeling in new[] { "jealous", "hurt", "wrong", "betray", "should", "must", "shame", "feel" })
                Assert.DoesNotContain(feeling, line, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ARunAndTheReckoning_CarryWhatSheIsToo()
    {
        var run = new List<NightRecord>();
        for (int d = 95; d <= 99; d++)
        {
            var r = Elsewhere(OtherWomanIs.HisOtherWife);
            r.GameDay = d;
            run.Add(r);
        }
        Assert.Contains("he was with Yasmin, his other lawful wife, nearly every night.",
            NightText.RunLine(NightKind.Elsewhere, run, today: 100));

        var marks = run.Select(NightMark.From).ToList();
        Assert.All(marks, m => Assert.Equal(OtherWomanIs.HisOtherWife, m.OtherIs));
        Assert.Contains("I heard he was with Yasmin, his other lawful wife, five times.",
            NightText.BuildReckoning(marks, today: 100));

        var legacy = run.Select(r => { var m = NightMark.From(r); m.OtherIs = OtherWomanIs.Unknown; return m; }).ToList();
        Assert.Contains("I heard he was with Yasmin five times.", NightText.BuildReckoning(legacy, today: 100));
    }
}
