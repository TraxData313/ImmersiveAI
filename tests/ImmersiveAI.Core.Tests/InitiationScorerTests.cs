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
}
