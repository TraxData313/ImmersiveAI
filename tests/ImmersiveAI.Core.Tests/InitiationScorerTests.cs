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
    public void Pull_InPlayersService_DoesNotFadeWithTheWeeksAway()
    {
        // The moving-writers fix (2026.07.12): a caravan or party of the player's own clan, away for
        // forty days doing the player's bidding, is exactly who should be writing home — duty floors
        // the recency and closeness that would otherwise silence them.
        double faded = InitiationScorer.Pull(storyRichness: 40, relation: 5, daysSinceLastTalk: 40);
        double dutiful = InitiationScorer.Pull(storyRichness: 40, relation: 5, daysSinceLastTalk: 40, inPlayersService: true);
        Assert.True(dutiful > faded * 3, $"duty should lift a long-away servant well above the faded pull ({dutiful} vs {faded})");
        Assert.Equal(InitiationScorer.DutyClosenessFloor * InitiationScorer.DutyRecencyFloor, dutiful, 5);

        // Duty never lowers a bond that is already warm and fresh...
        double warm = InitiationScorer.Pull(60, 90, 0);
        Assert.True(InitiationScorer.Pull(60, 90, 0, inPlayersService: true) >= warm);

        // ...and frequency still gates: a servant never truly spoken with stays quiet.
        Assert.Equal(0.0, InitiationScorer.Pull(0, 0, 0, inPlayersService: true), 5);
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
    public void NightFactor_IsFullByDay_AndDampedThroughTheNight()
    {
        // Broad daylight is undamped, all the way to the day's edges.
        Assert.Equal(1.0, InitiationScorer.NightFactor(12.0), 5);
        Assert.Equal(1.0, InitiationScorer.NightFactor(InitiationScorer.DawnHour), 5);   // 06:00 wakes
        Assert.Equal(1.0, InitiationScorer.NightFactor(21.99), 5);                       // still evening-social

        // The deepest night (~02:00) is the /8 floor.
        double deepest = (InitiationScorer.DuskHour + (InitiationScorer.DawnHour + 24.0)) / 2.0 - 24.0; // 02:00
        Assert.Equal(1.0 / InitiationScorer.DeepestNightDivisor, InitiationScorer.NightFactor(deepest), 3);

        // Shallow night passes through roughly /2 (a few hours off the bottom).
        Assert.Equal(0.5, InitiationScorer.NightFactor(23.0), 1);
        Assert.Equal(0.5, InitiationScorer.NightFactor(5.0), 1);

        // Continuous at the dusk edge — no cliff at 22:00 that snaps evening chats shut.
        Assert.Equal(InitiationScorer.NightFactor(21.99), InitiationScorer.NightFactor(22.01), 2);

        // Every hour stays a genuine multiplier in (0,1], and wrapping is handled.
        for (double h = -5; h < 30; h += 0.25)
        {
            double f = InitiationScorer.NightFactor(h);
            Assert.True(f > 0 && f <= 1.0);
        }
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

    [Fact]
    public void OutreachDamping_NeverReachedOut_IsUntouched()
    {
        Assert.Equal(1.0, InitiationScorer.OutreachDamping(-1, 0), 5);
        Assert.Equal(1.0, InitiationScorer.OutreachDamping(-1, 3), 5); // stale count without a day stamp changes nothing
    }

    [Fact]
    public void OutreachDamping_RestsAfterAnyOutreach_ThenRecovers()
    {
        // The moment they reach out (answered or not) the pull is at zero — no knocking twice in an
        // afternoon — and with the player having engaged (0 unanswered) it is whole again after the
        // cooldown day.
        Assert.Equal(0.0, InitiationScorer.OutreachDamping(0, 0), 5);
        double halfway = InitiationScorer.OutreachDamping(InitiationScorer.OutreachCooldownDays / 2, 0);
        Assert.True(halfway > 0 && halfway < 1);
        Assert.Equal(1.0, InitiationScorer.OutreachDamping(InitiationScorer.OutreachCooldownDays, 0), 5);
    }

    [Fact]
    public void OutreachDamping_SilenceStretchesPatience_AndLowersTheCeiling()
    {
        // One letter into silence: even fully recovered, the pull tops out at the pride factor —
        // and the recovery itself takes days, not hours.
        double patience1 = InitiationScorer.OutreachCooldownDays + InitiationScorer.UnansweredPatienceDays;
        Assert.Equal(InitiationScorer.UnansweredPrideFactor,
            InitiationScorer.OutreachDamping(patience1, 1), 5);
        Assert.True(InitiationScorer.OutreachDamping(1.0, 1) < 0.1); // a day later, still nearly silent

        // Each further unanswered outreach shrinks the ceiling geometrically: the Emperor does not
        // send letter after letter to someone who never writes back (the 2026.07.26 report).
        double two = InitiationScorer.OutreachDamping(1000, 2);
        double three = InitiationScorer.OutreachDamping(1000, 3);
        Assert.Equal(Math.Pow(InitiationScorer.UnansweredPrideFactor, 2), two, 5);
        Assert.True(three < two);
    }

    [Fact]
    public void HearthFactors_TheWeddedOneIsThriceACompanion()
    {
        // Anton's own numbers, 2026.08.15: the one the player is wed to is three times as likely to
        // come to them as a companion of the very same shared story, and a companion is only a nudge
        // above the world ("not as dramatically... maybe touch them a bit"). Pinned because these are
        // a stated requirement rather than a tuning knob free to drift.
        Assert.Equal(3.0 * InitiationScorer.CompanionHearthFactor, InitiationScorer.SpouseHearthFactor, 5);
        Assert.True(InitiationScorer.CompanionHearthFactor > 1.0);
        Assert.True(InitiationScorer.CompanionHearthFactor < 2.0);
    }
}
