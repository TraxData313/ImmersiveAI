using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// The mod's one Harmony patch (so far): a postfix on MapNotificationVM's constructor that
    /// registers our notification type through the game's own PUBLIC
    /// <see cref="MapNotificationVM.RegisterMapNotificationType"/> — the lightest possible touch;
    /// nothing vanilla is altered. Registration cannot be done without the patch only because the
    /// VM instance is created deep inside the Gauntlet map screen with no hook to reach it.
    ///
    /// Everything degrades gracefully: if Harmony or the patch fails, <see cref="Applied"/> stays
    /// false and the behavior falls back to the plain inquiry offer it has always shown.
    /// </summary>
    internal static class MapNoticePatch
    {
        public static bool Applied { get; private set; }

        public static void TryApply()
        {
            try
            {
                var harmony = new Harmony("mod.immersiveai");
                var ctor = typeof(MapNotificationVM).GetConstructors().FirstOrDefault();
                if (ctor == null) return;

                harmony.Patch(ctor, postfix: new HarmonyMethod(
                    typeof(MapNoticePatch).GetMethod(nameof(AfterVmConstructed),
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
                Applied = true;
            }
            catch (Exception)
            {
                Applied = false;
            }
        }

        private static void AfterVmConstructed(MapNotificationVM __instance)
        {
            try
            {
                __instance.RegisterMapNotificationType(
                    typeof(ImmersiveChatMapNotification), typeof(ImmersiveChatNotificationItemVM));
                __instance.RegisterMapNotificationType(
                    typeof(ImmersiveLetterMapNotification), typeof(ImmersiveLetterNotificationItemVM));
            }
            catch { /* a failed registration only means the fallback inquiry path is used */ }
        }
    }
}
