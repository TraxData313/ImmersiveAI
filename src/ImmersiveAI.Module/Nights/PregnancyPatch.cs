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

        /// <summary>Who fathered the child of a woman whose pregnancy has lost its father. Set by
        /// the behavior; answers null whenever it cannot say honestly, and null means we stand
        /// aside. See <see cref="BeforeDeliverOffSpring"/>.</summary>
        public static Func<Hero, Hero?>? FatherForLostChild;

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

            TryApplyLostFatherRescue();
        }

        /// <summary>Whether a fatherless pregnancy can be rescued at the delivery.</summary>
        public static bool RescueApplied { get; private set; }

        /// <summary>
        /// THE THIRD HARMONY TOUCH (2026.08.30), and it exists to un-brick saves rather than to add
        /// anything: a rescue for a pregnancy whose father is null.
        ///
        /// Vanilla holds the father from the instant of conception — <c>Pregnancy(mother,
        /// mother.Spouse, dueDate)</c> — and reads him again thirty-six days later at the delivery,
        /// where <c>HeroCreator.DeliverOffSpring</c> touches <c>father.CharacterObject.Race</c> on
        /// its very first line and the offspring's body properties read <c>hero.Father.BodyProperties</c>.
        /// A null there is a hard crash on the due date, and because the due date does not move it
        /// repeats on every load: the campaign simply stops. Ours made one (a woman player's
        /// unmarried lover left her spouse slot empty — fixed at the source in the nights), and any
        /// romance mod calling <c>MakePregnantAction</c> on an unwed woman makes the same one.
        ///
        /// So: a prefix on the public static delivery, acting ONLY when the father is already null,
        /// asking the nights who it should have been, and standing aside when there is no honest
        /// answer — a wrong father would be permanent lineage, and a pregnancy that is none of our
        /// business stays none of our business.
        /// </summary>
        private static void TryApplyLostFatherRescue()
        {
            try
            {
                var target = AccessTools.Method(typeof(HeroCreator), "DeliverOffSpring");
                if (target == null)
                {
                    ModLog.Warn("The nights: the game's delivery could not be found, so a fatherless pregnancy cannot be rescued.");
                    return;
                }

                new Harmony("mod.immersiveai.nights.lostfather").Patch(target, prefix: new HarmonyMethod(
                    typeof(PregnancyPatch).GetMethod(nameof(BeforeDeliverOffSpring),
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
                RescueApplied = true;
            }
            catch (Exception ex)
            {
                RescueApplied = false;
                ModLog.Error("patching the delivery against a lost father", ex);
            }
        }

        // Runs inside the game's own daily tick. Everything is wrapped, and the untouched case — a
        // father who is simply there — costs one null check.
        private static void BeforeDeliverOffSpring(Hero mother, ref Hero father)
        {
            try
            {
                if (father != null || mother == null) return;

                var found = FatherForLostChild?.Invoke(mother);
                if (found == null || found == mother || found.IsFemale) return;

                father = found;
                ModLog.Warn($"the nights: {mother.Name} carried a child with no father recorded; "
                          + $"{found.Name} is named, so the birth can happen instead of crashing.");
            }
            catch { /* a rescue that throws would be worse than the crash it came for */ }
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
