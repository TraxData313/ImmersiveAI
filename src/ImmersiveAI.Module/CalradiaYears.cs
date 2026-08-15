using System;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// How long ago a recorded day was, in the world's own years (2026.08.15). Core keeps the
    /// chronicles and none of the calendar — <see cref="CampaignTime.DaysInYear"/> is the game's,
    /// and a mod may change it — so anything that needs "how many years since" asks here and hands
    /// Core a plain number.
    /// </summary>
    public static class CalradiaYears
    {
        /// <summary>Years between <paramref name="gameDay"/> and now; -1 when the day is unknown or
        /// the calendar cannot be read. Fractional, so a caller may floor it however it likes.</summary>
        public static double Since(double gameDay)
        {
            try
            {
                if (gameDay <= 0) return -1;
                int daysInYear = CampaignTime.DaysInYear;
                if (daysInYear <= 0) return -1;
                double years = (CampaignTime.Now.ToDays - gameDay) / daysInYear;
                return years < 0 ? -1 : years;
            }
            catch { return -1; }
        }
    }
}
