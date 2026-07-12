using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MCM.Common;

namespace ImmersiveAI.Mcm
{
    /// <summary>
    /// Binds the in-game MCM menu to the live <see cref="ModConfig"/> as a SOFT dependency: if the Mod
    /// Configuration Menu module is not installed, nothing here ever touches an MCM type, so the mod
    /// runs on <c>config.json</c> alone. The whole MCM-facing surface lives in <see cref="Bind"/> (and
    /// its helpers), which are called only after we confirm MCM's assembly is loaded — so the CLR never
    /// tries to resolve MCM types when the module is absent.
    ///
    /// Design: <c>config.json</c> is the single source of truth. On startup we push its current values
    /// into the menu; on every menu change we write straight back into the same shared config and its
    /// file. So the menu is a live editor, never a competing store, and no menu default can wipe a value
    /// the player set by hand (e.g. an API key from a previous version).
    /// </summary>
    internal static class McmBridge
    {
        private static bool _bound;

        /// <summary>Binds the menu to <paramref name="live"/> if MCM is present. Safe to call when it is
        /// not — returns quietly. Best-effort: any failure only costs the menu, never the mod.</summary>
        public static void TryBind(ModConfig live)
        {
            if (_bound || live == null) return;
            // Note: do NOT reference any MCM type in this method's body — the check below must not force
            // the CLR to load MCMv5 when the module is absent. All MCM contact is quarantined in Bind().
            var mcmLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, "MCMv5", StringComparison.OrdinalIgnoreCase));
            if (!mcmLoaded) return;

            try
            {
                // Bind returns false if MCM is up but our settings aren't registered yet; leave _bound
                // false so a later call (e.g. from OnGameStart) retries. A real exception, by contrast,
                // won't heal itself — give up so we don't spam the log.
                if (Bind(live)) _bound = true;
            }
            catch (Exception ex)
            {
                _bound = true;
                // The menu is a convenience; config.json still works. Note it once and move on.
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage("Immersive AI: mod menu unavailable (" + ex.Message + ")."));
            }
        }

        // Kept out-of-line so TryBind can be JIT-compiled without resolving MCM types when MCM is absent.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Bind(ModConfig live)
        {
            var s = ImmersiveAiMcmSettings.Instance;
            if (s == null) return false; // MCM present but our settings not registered yet — retry later.

            PushConfigToMenu(s, live);
            s.PropertyChanged += (_, __) =>
            {
                try
                {
                    PullMenuToConfig(s, live);
                    live.Normalize();
                    live.Save();
                }
                catch { /* a bad edit must not crash the menu */ }
            };
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushConfigToMenu(ImmersiveAiMcmSettings s, ModConfig c)
        {
            Select(s.Backend, c.Backend);
            s.AnthropicApiKey = c.AnthropicApiKey ?? string.Empty;
            SelectOrAdd(s.AnthropicModel, c.AnthropicModel);
            s.OpenAIApiKey = c.OpenAIApiKey ?? string.Empty;
            SelectOrAdd(s.OpenAIModel, c.OpenAIModel);
            SelectOrAdd(s.OpenAIReasoningEffort, c.OpenAIReasoningEffort);
            s.MaxTokens = Clamp(c.MaxTokens, 100, 2000);

            s.EnableChatWindow = c.EnableChatWindow;
            SelectOrAdd(s.ChatWindowHotkey, c.ChatWindowHotkey);
            SelectOrAdd(s.LetterWindowHotkey, c.LetterWindowHotkey);
            s.NotifyWhenReplyReady = c.NotifyWhenReplyReady;

            s.EnableNpcInitiatedChats = c.EnableNpcInitiatedChats;
            s.Socialness = (float)Math.Max(0.0, Math.Min(24.0, c.DailyInitiationRate));
            s.EnableLetters = c.EnableLetters;
            s.EnableWorldRecall = c.EnableWorldRecall;
            s.EnableWebSearch = c.EnableWebSearch;
            s.MaxKnownFacts = Clamp(c.MaxKnownFacts, 1, 30);
            s.MaxNpcGoals = Clamp(c.MaxNpcGoals, 1, 20);
            s.RevertMemoriesWithSaves = c.RevertMemoriesWithSaves;

            s.ShowCostNotices = c.ShowCostNotices;
            s.MaxDailyRequests = Clamp(c.MaxDailyRequests, 0, 2000);

            s.DevMode = c.DevMode;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PullMenuToConfig(ImmersiveAiMcmSettings s, ModConfig c)
        {
            c.Backend = s.Backend.SelectedValue ?? c.Backend;
            c.AnthropicApiKey = s.AnthropicApiKey ?? string.Empty;
            c.AnthropicModel = s.AnthropicModel.SelectedValue ?? c.AnthropicModel;
            c.OpenAIApiKey = s.OpenAIApiKey ?? string.Empty;
            c.OpenAIModel = s.OpenAIModel.SelectedValue ?? c.OpenAIModel;
            c.OpenAIReasoningEffort = s.OpenAIReasoningEffort.SelectedValue ?? c.OpenAIReasoningEffort;
            c.MaxTokens = s.MaxTokens;

            c.EnableChatWindow = s.EnableChatWindow;
            c.ChatWindowHotkey = s.ChatWindowHotkey.SelectedValue ?? c.ChatWindowHotkey;
            c.LetterWindowHotkey = s.LetterWindowHotkey.SelectedValue ?? c.LetterWindowHotkey;
            c.NotifyWhenReplyReady = s.NotifyWhenReplyReady;

            c.EnableNpcInitiatedChats = s.EnableNpcInitiatedChats;
            c.DailyInitiationRate = s.Socialness;
            c.EnableLetters = s.EnableLetters;
            c.EnableWorldRecall = s.EnableWorldRecall;
            c.EnableWebSearch = s.EnableWebSearch;
            c.MaxKnownFacts = s.MaxKnownFacts;
            c.MaxNpcGoals = s.MaxNpcGoals;
            c.RevertMemoriesWithSaves = s.RevertMemoriesWithSaves;

            c.ShowCostNotices = s.ShowCostNotices;
            c.MaxDailyRequests = s.MaxDailyRequests;

            c.DevMode = s.DevMode;
        }

        /// <summary>Selects an existing dropdown value; leaves the current selection if the value is not
        /// present (used for a fixed set like the backend, where an unknown value is a config error).</summary>
        private static void Select(Dropdown<string> dropdown, string value)
        {
            if (dropdown == null || string.IsNullOrWhiteSpace(value)) return;
            var index = dropdown.IndexOf(value);
            if (index >= 0) dropdown.SelectedIndex = index;
        }

        /// <summary>Selects a dropdown value, adding it first if the list does not already carry it — so a
        /// model or key the player set by hand shows up in the menu instead of being silently dropped.</summary>
        private static void SelectOrAdd(Dropdown<string> dropdown, string value)
        {
            if (dropdown == null || string.IsNullOrWhiteSpace(value)) return;
            var index = dropdown.IndexOf(value);
            if (index < 0)
            {
                dropdown.Add(value);
                index = dropdown.Count - 1;
            }
            dropdown.SelectedIndex = index;
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
