using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ImmersiveAI.Nights
{
    /// <summary>
    /// The mod's second Harmony patch (2026.08.09): the one that takes the player's own marriages
    /// out of the world's nightly coin-flip, so that a child is begun on a night the player CHOSE.
    ///
    /// THE TOUCH IS AS NARROW AS IT CAN BE MADE. Vanilla's <c>PregnancyCampaignBehavior</c> ticks
    /// every woman daily and, for anyone with a living spouse who is nearby, rolls
    /// <c>PregnancyModel.GetDailyChanceOfPregnancyForHero</c> inside the private
    /// <c>RefreshSpouseVisit</c>. We prefix THAT method and answer "skip" for exactly one kind of
    /// woman: one wed to the player. Every other marriage in Calradia keeps its own nights, and
    /// deliveries — which run in the same daily tick through a different branch — are never touched,
    /// so a pregnancy already begun always comes to term.
    ///
    /// WHY NOT REPLACE THE MODEL: because a PregnancyModel replacement fights every other mod that
    /// wants the same seat, and would silence the whole world rather than one household.
    ///
    /// IT FAILS OPEN, AND THE MOD IS TOLD. If Harmony or the method is gone (a game patch renaming
    /// a private), <see cref="Applied"/> stays false; the nights still happen and are still written,
    /// but conception is left to the world exactly as it was, because two systems each rolling for
    /// the same child is worse than either alone.
    /// </summary>
    internal static class PregnancyPatch
    {
        /// <summary>Whether the nights truly own conception now. False means the world still does.</summary>
        public static bool Applied { get; private set; }

        /// <summary>Answers whether this woman's nights belong to the player's own hearth. Set by
        /// the behavior at registration; a null hook means "no one's", so the patch stands aside and
        /// vanilla runs untouched — the safe answer whenever the mod is not fully awake.</summary>
        public static Func<Hero, bool>? IsOursToDecide;

        public static void TryApply()
        {
            try
            {
                var target = AccessTools.Method(typeof(PregnancyCampaignBehavior), "RefreshSpouseVisit");
                if (target == null)
                {
                    ModLog.Warn("The nights: vanilla's spouse-visit roll could not be found, so conception stays the world's own.");
                    return;
                }

                new Harmony("mod.immersiveai.nights").Patch(target, prefix: new HarmonyMethod(
                    typeof(PregnancyPatch).GetMethod(nameof(BeforeSpouseVisit),
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
                Applied = true;
                ModLog.Info("The nights: conception for the player's own marriages now waits on the player's own nights.");
            }
            catch (Exception ex)
            {
                Applied = false;
                ModLog.Error("patching the world's nightly roll", ex);
            }
        }

        // False skips vanilla's roll for this woman on this day. Everything is wrapped: a throw here
        // would run inside the game's own daily tick, and no feature of ours is worth that.
        private static bool BeforeSpouseVisit(Hero hero)
        {
            try
            {
                var hook = IsOursToDecide;
                return hook == null || hero == null || !hook(hero);
            }
            catch { return true; }
        }
    }
}
