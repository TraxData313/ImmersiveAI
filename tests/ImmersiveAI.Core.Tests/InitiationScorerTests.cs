using ImmersiveAI.Core.Initiation;

namespace ImmersiveAI.Core.Tests;

public class InitiationScorerTests
{
    [Fact]
    public void DailyChance_IsZero_OnlyWhenThereIsNoStoryOrNoRate()
    {
        // No shared story or a disabled rate silences it outright...
        Assert.Equal(0, InitiationScorer.DailyChance(0.3, storyRichness: 0, relation: 80, daysSinceLastTalk: 0));
        Assert.Equal(0, InitiationScorer.DailyChance(0, storyRichness: 40, relation: 80, daysSinceLastTalk: 0));

        // ...but a neutral standing with real shared time is only quiet, not silent (the closeness floor),
        // so the feature stays observable rather than near-impossible to ever witness.
        Assert.True(InitiationScorer.DailyChance(0.3, storyRichness: 40, relation: 0, daysSinceLastTalk: 0) > 0);
    }

    [Fact]
    public void DailyChance_RisesWithClosenessOfStanding()
    {
        // Love and enmity both pull; a warmer bond reaches out more than a lukewarm one, which in turn
        // beats a neutral one sitting on the floor.
        double neutral = InitiationScorer.DailyChance(1.0, 40, relation: 0, daysSinceLastTalk: 0);
        double lukewarm = InitiationScorer.DailyChance(1.0, 40, relation: 20, daysSinceLastTalk: 0);
        double devoted = InitiationScorer.DailyChance(1.0, 40, relation: 90, daysSinceLastTalk: 0);
        Assert.True(devoted > lukewarm);
        Assert.True(lukewarm > neutral);

        // Enmity is symmetric with love: a bitter rival is as moved to reach out as a dear friend.
        double hated = InitiationScorer.DailyChance(1.0, 40, relation: -90, daysSinceLastTalk: 0);
        Assert.Equal(devoted, hated, 5);
    }

    [Fact]
    public void DailyChance_DevotedFrequentBond_CanApproachDaily()
    {
        // Rich story, high standing, spoken today, generous rate -> nearly every day.
        double chance = InitiationScorer.DailyChance(1.5, storyRichness: 60, relation: 100, daysSinceLastTalk: 0);
        Assert.Equal(1.0, chance, 5); // capped: at most about once a day
    }

    [Fact]
    public void DailyChance_FadesWithSilence()
    {
        double fresh = InitiationScorer.DailyChance(1.0, 40, 80, daysSinceLastTalk: 0);
        double stale = InitiationScorer.DailyChance(1.0, 40, 80, daysSinceLastTalk: 28);
        Assert.True(stale < fresh);
    }

    [Fact]
    public void RecencyFactor_HalvesEachHalfLife_WithAFloor()
    {
        Assert.Equal(1.0, InitiationScorer.RecencyFactor(0), 5);
        Assert.Equal(0.5, InitiationScorer.RecencyFactor(InitiationScorer.RecencyHalfLifeDays), 5);
        Assert.Equal(InitiationScorer.RecencyFloor, InitiationScorer.RecencyFactor(10000), 5);
    }

    [Fact]
    public void Pull_IsOneForAFullBond_AndZeroWithNoStory()
    {
        Assert.Equal(1.0, InitiationScorer.Pull(storyRichness: 60, relation: 100, daysSinceLastTalk: 0), 5);
        Assert.Equal(0.0, InitiationScorer.Pull(storyRichness: 0, relation: 100, daysSinceLastTalk: 0), 5);
    }

    [Fact]
    public void UnionPull_IsTheChanceAtLeastOneIsMoved()
    {
        // Empty group: no one to be moved.
        Assert.Equal(0.0, InitiationScorer.UnionPull(new double[0]), 5);

        // Alone, an NPC contributes exactly their own pull.
        Assert.Equal(0.4, InitiationScorer.UnionPull(new[] { 0.4 }), 5);

        // Two medium bonds pull harder together than either alone, but not additively.
        Assert.Equal(0.75, InitiationScorer.UnionPull(new[] { 0.5, 0.5 }), 5);

        // The whole can never exceed 1, however devoted the crowd.
        Assert.Equal(1.0, InitiationScorer.UnionPull(new[] { 1.0, 1.0, 1.0, 1.0, 1.0 }), 5);
    }

    [Fact]
    public void GroupHourlyChance_TotalsToTheRatePerDay_NotPerNpc()
    {
        // THE regression this model exists for: at rate 0.777 with five full bonds present, the old
        // per-NPC rolls averaged ~3.9 reach-outs/day; the group roll must average the rate itself.
        double union = InitiationScorer.UnionPull(new[] { 1.0, 1.0, 1.0, 1.0, 1.0 });
        double hourly = InitiationScorer.GroupHourlyChance(0.777, union);
        Assert.Equal(0.777, hourly * 24.0, 5); // expected reach-outs per day = rate, shared by all

        // Weak bonds pull the day's total below the rate — a fresh game stays quiet.
        double freshUnion = InitiationScorer.UnionPull(new[]
        {
            InitiationScorer.Pull(storyRichness: 2, relation: 0, daysSinceLastTalk: 0),
            InitiationScorer.Pull(storyRichness: 1, relation: 5, daysSinceLastTalk: 3),
        });
        Assert.True(InitiationScorer.GroupHourlyChance(0.777, freshUnion) * 24.0 < 0.03);

        // Disabled or empty: silent.
        Assert.Equal(0.0, InitiationScorer.GroupHourlyChance(0, 1.0), 5);
        Assert.Equal(0.0, InitiationScorer.GroupHourlyChance(0.777, 0), 5);
    }

    [Fact]
    public void GroupHourlyChance_FullSocialness_GuaranteesTheHour()
    {
        // The top of the socialness slider is the player's own word, not the bonds': at 24, someone
        // near IS moved every hour, however faint the pull (a lone stranger at the 0.1 floor).
        Assert.Equal(1.0, InitiationScorer.GroupHourlyChance(24.0, 0.1), 5);
        Assert.Equal(1.0, InitiationScorer.GroupHourlyChance(24.0, 1.0), 5);

        // But an empty room cannot knock, whatever the mood.
        Assert.Equal(0.0, InitiationScorer.GroupHourlyChance(24.0, 0.0), 5);
    }

    [Fact]
    public void GroupHourlyChance_SocialnessOverride_VanishesAtEverydayRates()
    {
        // At everyday rates the bonds stay fully in charge: the s² blend adds only rate³/24³ per
        // hour, imperceptible at 1.5/day — the day's total stays ≈ rate × unionPull.
        double up = 0.4;
        double everyday = InitiationScorer.GroupHourlyChance(1.5, up) * 24.0;
        Assert.Equal(1.5 * up, everyday, 1);

        // Toward the top the player's openness increasingly carries the day: at 12 the same weak
        // bonds are visited far more than the bonds alone would justify.
        double social = InitiationScorer.GroupHourlyChance(12.0, up);
        Assert.True(social > 12.0 * up / 24.0);

        // And the whole stays monotonic in the rate — more social is never fewer visits.
        double prev = 0;
        for (double rate = 0; rate <= 24.0; rate += 0.5)
        {
            double hourly = InitiationScorer.GroupHourlyChance(rate, up);
            Assert.True(hourly >= prev);
            prev = hourly;
        }
    }
}
