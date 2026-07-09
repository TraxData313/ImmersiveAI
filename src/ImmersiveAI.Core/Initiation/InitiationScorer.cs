using System;

namespace ImmersiveAI.Core.Initiation
{
    /// <summary>
    /// How likely a single NPC is to reach out to the player on a given day. The reaching-out should feel
    /// earned, not random: someone who loves (or hates) the player and speaks with them often will want to
    /// reach out; a near-stranger at a neutral standing almost never will. So the daily chance is
    ///
    ///     dailyRate × frequency × closeness × recency
    ///
    /// where the config's <c>DailyInitiationRate</c> is the ceiling for a full-blown bond, and each factor
    /// in [0,1] pulls it down toward silence:
    ///   - frequency: how much has ever been shared, saturating at <see cref="FrequencyFullAt"/> exchanges.
    ///   - closeness: how far the standing is from indifference, |relation| / 100 (love OR enmity both pull).
    ///   - recency: gently decays if they have not spoken lately, so a long-quiet bond grows quiet.
    ///
    /// This is why a fresh game stays calm (everyone near relation 0, little shared) while a devoted wife
    /// you speak with daily may write nearly every day.
    /// </summary>
    public static class InitiationScorer
    {
        /// <summary>Lifetime exchanges at which "how much we speak" counts for its full weight.</summary>
        public const int FrequencyFullAt = 20;

        /// <summary>Days of silence at which the pull halves. Long, so bonds fade slowly, not overnight.</summary>
        public const double RecencyHalfLifeDays = 14.0;

        /// <summary>A small floor so even a long-lost friend can, once in a great while, still reach out.</summary>
        public const double RecencyFloor = 0.05;

        /// <summary>A small floor on closeness so that someone the player speaks with often, but holds at a
        /// neutral standing, is not utterly silent — they may still, rarely, reach out. Standing still
        /// dominates (a maxed bond is far more likely), and frequency gates it (a near-stranger stays quiet
        /// regardless), so this only lifts the reaching-out off exactly zero for the bonds that are actually
        /// close in time spent. Keeps the feature observable rather than near-impossible to ever see.</summary>
        public const double ClosenessFloor = 0.15;

        /// <summary>
        /// The probability, in [0,1], that this NPC reaches out to the player over one day. Zero whenever
        /// there is nothing to move them — no shared story or a disabled rate. Capped at 1, since a soul
        /// reaches out at most about once a day however deep the bond.
        /// </summary>
        public static double DailyChance(double dailyRate, int storyRichness, int relation, double daysSinceLastTalk)
        {
            if (dailyRate <= 0 || double.IsNaN(dailyRate) || storyRichness <= 0) return 0;

            double frequency = Math.Min(1.0, storyRichness / (double)FrequencyFullAt);
            double standing = Math.Min(1.0, Math.Abs(relation) / 100.0);
            double closeness = ClosenessFloor + (1.0 - ClosenessFloor) * standing;
            double recency = RecencyFactor(daysSinceLastTalk);

            double chance = dailyRate * frequency * closeness * recency;
            if (chance < 0) chance = 0;
            if (chance > 1) chance = 1;
            return chance;
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
