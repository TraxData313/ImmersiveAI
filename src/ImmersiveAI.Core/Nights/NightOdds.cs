using System;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Nights
{
    /// <summary>
    /// Whether a night quickens — the whole arithmetic of it, kept pure so it can be tested
    /// (2026.08.09, Anton's design: conception stops being a daily coin the world flips behind the
    /// player's back and becomes a thing the player DOES, on a night he chose, with a woman whose
    /// body has its own season).
    ///
    /// THE ONE RULE THE NUMBERS SERVE: a player who takes every night of a woman's fertile window
    /// should father a child about as readily as the world's own reckoning would have given him one
    /// anyway. So we do not invent a balance — we take the game's own daily chance
    /// (<c>PregnancyModel.GetDailyChanceOfPregnancyForHero</c>, which already weighs her age, how
    /// many children she has borne, her house's standing and the Virile perk), spread a whole
    /// cycle's worth of it across the fertile nights by <see cref="MoodTides.Fertility"/>, and hand
    /// the player the moment. Miss the window and you have missed the month; take it and you have
    /// what the world would have given you.
    ///
    /// What that feels like, for a young wife with no children (the game's own daily chance ≈ 0.11,
    /// a 28-day cycle): about 66% on the crest night, 29% four nights before it, a hundredth of
    /// that in the quiet days, and nothing at all while the custom of women is upon her. Older, or
    /// with children already borne, the same curve stands but the whole of it is far lower — the
    /// game's own arithmetic doing the work, not ours.
    /// </summary>
    public static class NightOdds
    {
        /// <summary>No night is ever a certainty, whatever the gift and whatever the day.</summary>
        public const double MaxNightlyChance = 0.85;

        /// <summary>
        /// The chance that this night quickens.
        /// </summary>
        /// <param name="vanillaDailyChance">The game's own daily chance for this woman — her age,
        /// her children, her house and her perks already folded in. Zero (too young, too old,
        /// already carrying) honestly yields zero, and the night still happens.</param>
        /// <param name="fertility">Where her body stands, 0..1 (<see cref="MoodTides.Fertility"/>).</param>
        /// <param name="cycleLength">Her own cycle's length in days.</param>
        /// <param name="giftMultiplier">What was laid out for the night (<see cref="NightGifts"/>).</param>
        /// <param name="playerMultiplier">The player's own dial on the whole feature; 1 by default.</param>
        public static double NightlyChance(
            double vanillaDailyChance,
            double fertility,
            int cycleLength,
            double giftMultiplier = 1.0,
            double playerMultiplier = 1.0)
        {
            if (vanillaDailyChance <= 0.0 || fertility <= 0.0) return 0.0;
            if (cycleLength <= 0) return 0.0;

            // A whole cycle's worth of the world's own hazard, spread across the fertile window.
            double perWindowPoint = vanillaDailyChance * cycleLength / MoodTides.FertileWindowSum;
            double chance = fertility * perWindowPoint
                          * Math.Max(0.0, giftMultiplier)
                          * Math.Max(0.0, playerMultiplier);

            return Math.Min(MaxNightlyChance, Math.Max(0.0, chance));
        }

        /// <summary>The same reckoning straight from her id and the day — what every caller in the
        /// game actually wants. Her door being closed answers an honest zero.</summary>
        public static double NightlyChanceFor(
            string npcId, int campaignDay, double vanillaDailyChance,
            double giftMultiplier = 1.0, double playerMultiplier = 1.0)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return 0.0;
            return NightlyChance(
                vanillaDailyChance,
                MoodTides.Fertility(npcId, campaignDay),
                MoodTides.CycleLength(npcId),
                giftMultiplier,
                playerMultiplier);
        }

        /// <summary>How far off her crest this day stands, in nights (negative before, positive
        /// after) — what the auto-choice sorts by when it looks for the woman nearest her season.
        /// Days of the custom answer <see cref="int.MaxValue"/>: her door is closed, so she is not
        /// near anything.</summary>
        public static int NightsFromCrest(string npcId, int campaignDay)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return int.MaxValue;
            if (MoodTides.PhaseOf(npcId, campaignDay, out int dayOfCycle) == MoodTides.CyclePhase.Menses)
                return int.MaxValue;
            return dayOfCycle - MoodTides.CycleLength(npcId) / 2;
        }

        /// <summary>
        /// The odds in words, for the player's own eyes when the reckoning is kept hidden — never a
        /// number, always a judgment a husband could honestly have made. The thresholds are read off
        /// the fertility curve, not off the final chance, so an old wife and a young one are told
        /// the same thing about the same night of her season — which is the truth of it; what
        /// differs between them is not the night.
        /// </summary>
        public static string LikelihoodWord(double fertility, bool doorClosed)
        {
            if (doorClosed) return "no child can come of it now";
            if (fertility >= 0.85) return "her season is at its height — a child is likeliest now";
            if (fertility >= 0.45) return "her season is rising — a child may well come of it";
            if (fertility >= 0.20) return "her season is near, but not yet at its height";
            return "her season is low — a child is unlikely, though never impossible";
        }
    }
}
