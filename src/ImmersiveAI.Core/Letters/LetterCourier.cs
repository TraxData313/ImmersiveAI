using System;

namespace ImmersiveAI.Core.Letters
{
    /// <summary>
    /// How long a letter takes to cross the map. A courier rides harder than an army marches
    /// (parties make roughly 100–140 map units a day; the courier is a single rider with fresh
    /// horses), so nearby correspondence lands within a day while a letter across Calradia takes
    /// most of a week — long enough that writing feels like a real act, short enough to answer.
    /// </summary>
    public static class LetterCourier
    {
        /// <summary>Map units a courier covers in one in-game day.</summary>
        public const double UnitsPerDay = 150.0;

        /// <summary>Even across the street a letter waits for hands to carry it.</summary>
        public const double MinDays = 0.25;

        /// <summary>No road in Calradia is longer than this; also the rail against a bad distance.</summary>
        public const double MaxDays = 10.0;

        /// <summary>How much rarer writing is than crossing a room: a distant NPC's daily chance to
        /// write is their reaching-out chance (see Initiation.InitiationScorer) scaled by this.</summary>
        public const double WriteRateFactor = 0.5;

        /// <summary>Shared exchanges at which the story is deep enough to fully fund a correspondence.
        /// Below it the write-chance scales down linearly (see <see cref="StoryDepthFactor"/>).</summary>
        public const int WriteStoryFullAt = 12;

        /// <summary>Sitting down to write asks for more shared story than crossing a room: someone
        /// spoken with once has little to say that was not already said, and saying it again in new
        /// words is exactly the repetition the first players reported (2026.07.26). Scales the
        /// write-chance by richness / <see cref="WriteStoryFullAt"/>, capped at 1 — a single
        /// conversation funds half-weight letters at best; a real history writes freely.</summary>
        public static double StoryDepthFactor(int storyRichness)
        {
            if (storyRichness <= 0) return 0;
            double f = storyRichness / (double)WriteStoryFullAt;
            return f > 1 ? 1 : f;
        }

        /// <summary>In-game days for a letter to travel <paramref name="distance"/> map units.
        /// A negative or NaN distance (an unknown position) is treated as a far road, not a near one.</summary>
        public static double TravelDays(double distance)
        {
            if (double.IsNaN(distance) || distance < 0) return MaxDays / 2;
            double days = MinDays + distance / UnitsPerDay;
            if (days < MinDays) return MinDays;
            if (days > MaxDays) return MaxDays;
            return days;
        }
    }
}
