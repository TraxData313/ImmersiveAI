using System;

namespace ImmersiveAI.Core.Nights
{
    /// <summary>
    /// The night's clock, kept by the SUN rather than by a count of hours (Anton, 2026.08.15).
    /// <para>
    /// The old rule was a flat cooldown — so many hours since the last night — and its fault was
    /// DRIFT. A night at half past eleven put the next one out of reach until half past eleven the
    /// following day, which is after the evening's question has been and gone; the evening was then
    /// saved only by the window of hours around it, and a house that went to bed a little later each
    /// night walked its own clock right out of the day. Nobody meant that, and it is not how anyone
    /// thinks about their own evenings.
    /// </para>
    /// <para>
    /// So a night belongs to a CYCLE: the stretch from one late afternoon to the next. Availability
    /// resets at that turn — the same hour every day, whatever time was kept the night before — and
    /// the question is put later, in the evening proper. The two are deliberately separate hours:
    /// the reset says WHEN THE HOUSE IS READY AGAIN, the evening says WHEN IT IS ASKED. The whole
    /// afternoon between belongs to the player, to go of his own accord if he wants to, which is the
    /// thing the automatic night was always careful to leave him.
    /// </para>
    /// <para>
    /// A cycle is exactly 24 hours long, so "one cycle ago" and "a day ago" are the same span — that
    /// is what lets the settling of an unanswered evening go on measuring in plain days.
    /// </para>
    /// </summary>
    public static class NightClock
    {
        /// <summary>Late afternoon: the house is ready again, and the evening's question is still
        /// hours away.</summary>
        public const int DefaultResetHour = 16;

        /// <summary>Any hour, folded into 0..23 — so a hand-edited config can never throw.</summary>
        public static int NormalizeHour(int resetHour) => ((resetHour % 24) + 24) % 24;

        /// <summary>Which cycle a moment falls in. Cycle <c>N</c> runs from the reset hour on day
        /// <c>N</c> to the reset hour on day <c>N+1</c> — so an hour after midnight belongs to the
        /// evening it grew out of, not to the morning it landed in.</summary>
        public static int CycleOf(double gameDay, int resetHour)
        {
            var reset = NormalizeHour(resetHour);
            var day = Math.Floor(gameDay);
            var hour = (gameDay - day) * 24.0;
            return (int)(hour >= reset ? day : day - 1);
        }

        /// <summary>True when both moments belong to the same cycle — the plain question
        /// "has a night already been spent this evening?".</summary>
        public static bool SameCycle(double a, double b, int resetHour)
            => CycleOf(a, resetHour) == CycleOf(b, resetHour);

        /// <summary>Hours from this moment to the next reset: what an honest "not yet — N hours"
        /// counts down to. Always more than zero and never more than a full day.</summary>
        public static double HoursUntilReset(double gameDay, int resetHour)
        {
            var reset = NormalizeHour(resetHour);
            var day = Math.Floor(gameDay);
            var hour = (gameDay - day) * 24.0;
            var left = hour >= reset ? 24.0 - hour + reset : reset - hour;
            if (left <= 0) left = 24.0;
            return left > 24.0 ? 24.0 : left;
        }
    }
}
