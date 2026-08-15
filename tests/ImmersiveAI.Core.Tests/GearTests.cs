using ImmersiveAI.Core.Gear;

namespace ImmersiveAI.Core.Tests;

public class GearTests
{
    private static GearChangeSet Set(params GearChange[] changes) => new GearChangeSet(changes);

    [Fact]
    public void NothingChanged_IsNoBeatAtAll()
    {
        Assert.Equal(string.Empty, GearText.Beat(new GearChangeSet(), "Mizam"));
        Assert.Equal(string.Empty, GearText.Beat(null, "Mizam"));
    }

    [Fact]
    public void AGivenPieceCarriesItsWorth()
    {
        var beat = GearText.Beat(Set(new GearChange(GearSlot.Body, "Lordly Mail Hauberk", 4800)), "Mizam");

        Assert.True(GearText.IsGearBeat(beat));
        Assert.Contains("Mizam gave me Lordly Mail Hauberk (4,800 denars)", beat);
        Assert.Contains("my back", beat);
    }

    [Fact]
    public void ATakenPieceSaysWhatLeftHer()
    {
        var beat = GearText.Beat(Set(new GearChange(GearSlot.Hands, takenName: "Leather Gloves", takenValue: 78)), "Mizam");
        Assert.Contains("took my Leather Gloves (78 denars) from me", beat);
    }

    [Fact]
    public void ASwapNamesBothSides()
    {
        var beat = GearText.Beat(
            Set(new GearChange(GearSlot.Hands, "Mail Mittens", 340, "Leather Gloves", 78)), "Mizam");

        Assert.Contains("Mail Mittens (340 denars)", beat);
        Assert.Contains("my Leather Gloves (78 denars)", beat);
    }

    [Fact]
    public void ArmsAreNamedByTheItemAndNeverBySlot()
    {
        // The game's four weapon slots admit anything, so naming a slot would say something it does
        // not guarantee. The item's own name already carries what it is.
        var beat = GearText.Beat(
            Set(new GearChange(GearSlot.Arms, "Noble Long Bow", 1150, "Nomad Bow", 240)), "Mizam");

        Assert.Contains("for arms", beat, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Noble Long Bow", beat);
        Assert.DoesNotContain("slot", beat, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Weapon", beat, System.StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralArmsGatherIntoOneClause()
    {
        var beat = GearText.Beat(Set(
            new GearChange(GearSlot.Arms, "Menavlion", 900),
            new GearChange(GearSlot.Arms, "Heavy Kite Shield", 400)), "Mizam");

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(
            beat, "for arms", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Assert.Contains("Menavlion (900 denars) and Heavy Kite Shield (400 denars)", beat);
    }

    [Fact]
    public void TakingHerHorseLeavesHerAfoot()
    {
        var beat = GearText.Beat(
            Set(new GearChange(GearSlot.Mount, takenName: "Fine Steppe Horse", takenValue: 1100)), "Mizam");
        Assert.Contains("I go afoot now.", beat);
    }

    [Fact]
    public void SwappingOneHorseForAnotherLeavesNobodyAfoot()
    {
        var beat = GearText.Beat(
            Set(new GearChange(GearSlot.Mount, "War Horse", 1400, "Fine Steppe Horse", 1100)), "Mizam");
        Assert.DoesNotContain("afoot", beat);
    }

    [Fact]
    public void TheTallyCountsBothWays()
    {
        var richer = GearText.Beat(Set(
            new GearChange(GearSlot.Body, "Lordly Mail Hauberk", 4800, "Leather Coat", 190),
            new GearChange(GearSlot.Arms, "Noble Long Bow", 1150, "Nomad Bow", 240)), "Mizam");
        Assert.Contains("richer by 5,520 denars", richer);

        var poorer = GearText.Beat(Set(
            new GearChange(GearSlot.Body, takenName: "Lordly Mail Hauberk", takenValue: 4800),
            new GearChange(GearSlot.Hands, takenName: "Mail Mittens", takenValue: 340)), "Mizam");
        Assert.Contains("poorer by 5,140 denars", poorer);
    }

    [Fact]
    public void AnEvenTradeSaysSoRatherThanCountingZero()
    {
        var beat = GearText.Beat(Set(
            new GearChange(GearSlot.Hands, "Mail Mittens", 340, "Other Mittens", 340),
            new GearChange(GearSlot.Cape, "Cloak", 100, "Other Cloak", 100)), "Mizam");
        Assert.Contains("no richer and no poorer", beat);
    }

    [Fact]
    public void OneSmallPieceNeedsNoReckoning()
    {
        // "He gave me gloves worth 78 denars. All told I am richer by 78 denars." is the mod
        // talking to hear itself.
        var beat = GearText.Beat(Set(new GearChange(GearSlot.Hands, "Leather Gloves", 78)), "Mizam");
        Assert.DoesNotContain("All told", beat);
    }

    [Fact]
    public void HerOwnWageIsTheOnlyYardstickAndOnlyWhenItSaysSomething()
    {
        var changes = Set(
            new GearChange(GearSlot.Body, "Lordly Mail Hauberk", 4800),
            new GearChange(GearSlot.Hands, "Mail Mittens", 340));

        var withWage = GearText.Beat(changes, "Mizam", wagePerDay: 40);   // 128 days
        Assert.Contains("months of my wage", withWage);

        var withoutWage = GearText.Beat(changes, "Mizam", wagePerDay: 0);
        Assert.DoesNotContain("wage", withoutWage);

        // A rich soul's wage makes the comparison meaningless, so it is not made.
        var richlyPaid = GearText.Beat(changes, "Mizam", wagePerDay: 5000);
        Assert.DoesNotContain("wage", richlyPaid);
    }

    [Fact]
    public void NothingInTheBeatJudgesThePlayer()
    {
        // The founding law: she is handed the facts and feels about them herself.
        var beat = GearText.Beat(Set(
            new GearChange(GearSlot.Body, "Lordly Mail Hauberk", 4800),
            new GearChange(GearSlot.Arms, "Noble Long Bow", 1150)), "Mizam", wagePerDay: 40);

        foreach (var word in new[] { "generous", "kind", "princely", "cruel", "mean", "grateful", "insult" })
            Assert.DoesNotContain(word, beat, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheMarkIsStableAndRecognised()
    {
        var beat = GearText.Beat(Set(new GearChange(GearSlot.Head, "Nomad Helmet", 220)), "Mizam");
        Assert.StartsWith(GearText.GearBeatMark, beat);
        Assert.True(GearText.IsGearBeat(beat));
        Assert.False(GearText.IsGearBeat("Some other thing entirely"));
        Assert.False(GearText.IsGearBeat(null));
    }
}
