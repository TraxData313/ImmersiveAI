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
