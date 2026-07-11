using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Initiation
{
    /// <summary>
    /// How strongly the NPCs are moved to reach out to the player. The reaching-out should feel
    /// earned, not random: someone who loves (or hates) the player and speaks with them often will want to
    /// reach out; a near-stranger at a neutral standing almost never will.
    ///
    /// Each NPC has a <see cref="Pull"/> in [0,1] — how moved they are toward the player —
    ///
    ///     pull = frequency × closeness × recency
    ///
    /// where each factor in [0,1] pulls it down toward silence:
    ///   - frequency: how much has ever been shared, saturating at <see cref="FrequencyFullAt"/> exchanges.
    ///   - closeness: how far the standing is from indifference, |relation| / 100 (love OR enmity both pull).
    ///   - recency: gently decays if they have not spoken lately, so a long-quiet bond grows quiet.
    ///
    /// The config's <c>DailyInitiationRate</c> is the expected number of reach-outs per day IN TOTAL,
    /// across every NPC together, when the bonds justify it — NOT a per-NPC chance that stacks with each
    /// companion. The group's pulls are combined into <see cref="UnionPull"/> (the chance that at least one
    /// soul is moved), the day's expectation is rate × unionPull ≤ rate, and who actually comes is chosen
    /// by pull. So at 0.3 the player receives on average ~0.3 visits a day — some days none, some days one,
    /// rarely two — whether one devoted friend rides along or ten; a fresh game (everyone near relation 0,
    /// little shared) stays calm because every pull is tiny.
    /// </summary>
    public static class InitiationScorer
    {
        /// <summary>Lifetime exchanges at which "how much we speak" counts for its full weight.</summary>
        public const int FrequencyFullAt = 20;

        /// <summary>Days of silence at which the pull halves. Long, so bonds fade slowly, not overnight.</summary>
        public const double RecencyHalfLifeDays = 14.0;

        /// <summary>A small floor so even a long-lost friend can, once in a great while, still reach out.</summary>
        public const double RecencyFloor = 0.05;

        /// <summary>Hour the world wakes — before this the reaching-out is fully night-damped.</summary>
        public const double DawnHour = 6.0;

        /// <summary>Hour the world settles for the night — after this the damping begins.</summary>
        public const double DuskHour = 22.0;

        /// <summary>How much the reaching-out is divided at the very bottom of the night (~02:00): /8.
        /// Shallow night passes through /2 on the way there, matching "sleep deepens through the night".</summary>
        public const double DeepestNightDivisor = 8.0;

        /// <summary>A small floor on closeness so that someone the player speaks with often, but holds at a
        /// neutral standing, is not utterly silent — they may still, rarely, reach out. Standing still
        /// dominates (a maxed bond is far more likely), and frequency gates it (a near-stranger stays quiet
        /// regardless), so this only lifts the reaching-out off exactly zero for the bonds that are actually
        /// close in time spent. Keeps the feature observable rather than near-impossible to ever see.</summary>
        public const double ClosenessFloor = 0.15;

        /// <summary>Recency floor for someone in the player's own service (their clan: companions
        /// leading parties and caravans, kin, governors). Bonds of AFFECTION fade with silence; a
        /// bond of DUTY does not — a caravan away for forty days doing the player's bidding is
        /// exactly who should be writing home, so distance-in-time must not silence them.</summary>
        public const double DutyRecencyFloor = 0.6;

        /// <summary>Closeness floor for someone in the player's own service: duty stands in for
        /// affection, so even a near-neutral standing keeps the field reports coming.</summary>
        public const double DutyClosenessFloor = 0.5;

        /// <summary>
        /// How moved this NPC is toward the player, in [0,1]: frequency × closeness × recency.
        /// 1 is a full-blown bond (rich shared story, maxed standing, spoken today); zero means nothing
        /// has ever been shared. This is the NPC's weight when the day's reach-outs are shared among the
        /// group, and what scales the group's total below the configured rate when bonds are weak.
        /// </summary>
        public static double Pull(int storyRichness, int relation, double daysSinceLastTalk)
            => Pull(storyRichness, relation, daysSinceLastTalk, inPlayersService: false);

        /// <summary>Same pull, but for someone in the PLAYER'S OWN SERVICE the fading factors are
        /// floored (<see cref="DutyRecencyFloor"/>, <see cref="DutyClosenessFloor"/>): the letter
        /// flow passes true for the player's clan, so a party or caravan long on the road still
        /// writes its casual report home. Frequency still gates — someone never truly spoken with
        /// stays quiet — and the face-to-face flow keeps the plain pull (when they are near, the
        /// bond itself should do the moving).</summary>
        public static double Pull(int storyRichness, int relation, double daysSinceLastTalk, bool inPlayersService)
        {
            if (storyRichness <= 0) return 0;

            double frequency = Math.Min(1.0, storyRichness / (double)FrequencyFullAt);
            double standing = Math.Min(1.0, Math.Abs(relation) / 100.0);
            double closeness = ClosenessFloor + (1.0 - ClosenessFloor) * standing;
            double recency = RecencyFactor(daysSinceLastTalk);

            if (inPlayersService)
            {
                closeness = Math.Max(closeness, DutyClosenessFloor);
                recency = Math.Max(recency, DutyRecencyFloor);
            }

            double pull = frequency * closeness * recency;
            if (pull < 0) pull = 0;
            if (pull > 1) pull = 1;
            return pull;
        }

        /// <summary>
        /// Combines the group's pulls into one factor in [0,1]: the chance that at least one soul is moved,
        /// 1 − Π(1 − pull). A single NPC contributes exactly their own pull; several medium bonds together
        /// pull harder than any one of them alone, but the whole can never exceed 1 — so the configured
        /// rate stays the ceiling on the day's total no matter how many companions ride along.
        /// </summary>
        public static double UnionPull(IReadOnlyList<double> pulls)
        {
            if (pulls == null || pulls.Count == 0) return 0;

            double silent = 1.0;
            for (int i = 0; i < pulls.Count; i++)
            {
                double p = pulls[i];
                if (p <= 0) continue;
                silent *= p >= 1 ? 0 : 1.0 - p;
            }
            return 1.0 - silent;
        }

        /// <summary>
        /// The probability that anyone at all reaches out during ONE HOUR. The rate doubles as the
        /// player's SOCIALNESS (0–24, the map slider): at everyday rates it is the day's expectation
        /// (dailyRate × unionPull) spread over 24 hourly rolls — bonds fully in charge, exactly
        /// dailyRate/24 per hour when the bonds are full. As the rate climbs toward 24 the player's
        /// own openness increasingly overrides how faint the bonds are (the s² blend below — felt
        /// only at deliberately social settings, vanishing at everyday rates), until at 24 someone
        /// IS moved every hour, however slight the pulls: "I am here and glad of company" is the
        /// player's word, not the bonds'. Rolled once per hour for the whole group; the winner is
        /// then chosen by pull. Zero eligible souls (unionPull 0) stays silent at any rate — an
        /// empty room cannot knock.
        /// </summary>
        public static double GroupHourlyChance(double dailyRate, double unionPull)
        {
            if (dailyRate <= 0 || double.IsNaN(dailyRate) || unionPull <= 0) return 0;

            double s = Math.Min(1.0, dailyRate / 24.0);      // socialness in [0,1]
            double up = Math.Min(1.0, unionPull);
            double t = s * s;                                 // how much the player's openness overrides the bonds
            double hourly = s * ((1.0 - t) * up + t);
            return hourly > 1 ? 1 : hourly;
        }

        /// <summary>
        /// The expected reach-outs per day if this NPC were ALONE with the player: dailyRate × pull,
        /// capped at 1. Kept for the odds inspection view — the live schedule shares the day among the
        /// whole group via <see cref="UnionPull"/> and <see cref="GroupHourlyChance"/> instead of
        /// rolling this per NPC (which would stack: five devoted companions ≠ five times the visits).
        /// </summary>
        public static double DailyChance(double dailyRate, int storyRichness, int relation, double daysSinceLastTalk)
        {
            if (dailyRate <= 0 || double.IsNaN(dailyRate)) return 0;

            // Same math as the live schedule (including the socialness override at high rates),
            // just for one soul standing alone, summed back to a day and read as a chance.
            double chance = 24.0 * GroupHourlyChance(dailyRate, Pull(storyRichness, relation, daysSinceLastTalk));
            if (chance < 0) chance = 0;
            if (chance > 1) chance = 1;
            return chance;
        }

        /// <summary>
        /// How much the world being asleep dampens a face-to-face reach-out at this hour of day (0–24),
        /// a multiplier in (0,1]. People do not cross a dark camp at three in the morning: the daytime
        /// (<see cref="DawnHour"/>–<see cref="DuskHour"/>) is undamped (1.0), then through the night the
        /// chance is divided by a factor rising smoothly from 1 at dusk/dawn to <see cref="DeepestNightDivisor"/>
        /// at the night's middle (~02:00) — passing through /2 in the shallow night and reaching /8 at the
        /// deepest. A raised-cosine trough keeps it continuous at the day's edges (no sudden drop at 22:00),
        /// so evening chats fade gently rather than snapping shut. Letters are unaffected — a distant hand's
        /// writing hour is never seen, only the arrival days later.
        /// </summary>
        public static double NightFactor(double hourOfDay)
        {
            // Fold any stray value into a single day so callers need not normalize.
            hourOfDay %= 24.0;
            if (hourOfDay < 0) hourOfDay += 24.0;

            if (hourOfDay >= DawnHour && hourOfDay < DuskHour) return 1.0; // broad daylight, fully social

            double nightLength = 24.0 - DuskHour + DawnHour;               // hours of night (dusk → dawn)
            double sinceDusk = hourOfDay >= DuskHour ? hourOfDay - DuskHour : hourOfDay + (24.0 - DuskHour);
            double progress = sinceDusk / nightLength;                     // 0 at dusk, 1 at dawn, 0.5 deepest

            // Raised cosine: 0 at both night edges (flat, so it eases in from full day), 1 at the middle.
            double nightness = (1.0 - Math.Cos(2.0 * Math.PI * progress)) / 2.0;
            double divisor = 1.0 + (DeepestNightDivisor - 1.0) * nightness;
            return 1.0 / divisor;
        }

        /// <summary>The recency multiplier: 1 when they spoke today, halving every
        /// <see cref="RecencyHalfLifeDays"/>, never below <see cref="RecencyFloor"/>.</summary>
        public static double RecencyFactor(double daysSinceLastTalk)
        {
            if (daysSinceLastTalk <= 0) return 1.0;
            double f = Math.Pow(0.5, daysSinceLastTalk / RecencyHalfLifeDays);
            return f < RecencyFloor ? RecencyFloor : f;
        }
    }
}
