using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Initiation
{
    /// <summary>
    /// Choosing WHICH NPC reaches out when more than one is moved to in the same moment. The "how likely"
    /// per NPC lives in <see cref="InitiationScorer"/>; this only breaks ties, weighted by their pull, so
    /// the decision stays unit-tested and independent of the game clock.
    /// </summary>
    public static class InitiationPlanner
    {
        /// <summary>
        /// Picks one index from a list of non-negative weights, proportional to weight (so an NPC with 70
        /// shared exchanges is chosen over one with 30 roughly 70:30). <paramref name="roll"/> is a single
        /// value in [0,1). Returns -1 if the list is empty or every weight is zero.
        /// </summary>
        public static int PickWeightedIndex(IReadOnlyList<double> weights, double roll)
        {
            if (weights == null || weights.Count == 0) return -1;

            double total = 0;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0) total += weights[i];

            if (total <= 0) return -1;

            // Guard the roll into [0,1) so a stray 1.0 can't fall past the final bucket.
            if (roll < 0) roll = 0;
            if (roll >= 1) roll = 0.9999999;

            double target = roll * total;
            double running = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                running += weights[i];
                if (target < running) return i;
            }

            // Floating-point drift only; the last positive weight is the intended fallback.
            for (int i = weights.Count - 1; i >= 0; i--)
                if (weights[i] > 0) return i;
            return -1;
        }
    }
}
