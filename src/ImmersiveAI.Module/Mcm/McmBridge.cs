using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MCM.Common;
using Newtonsoft.Json.Linq;

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
    ///
    /// Hardened 2026.07.26 (the second Nexus report — Backend set to OpenRouter in the menu, mod still
    /// speaking as Anthropic, on v1.3.2): MCM's menu renders through MCM.UI's own version-independent
    /// pipeline, but our bind needs MCM's OWN container to register the settings instance — and on some
    /// setups (older/Nexus MCM builds, dependency version soup) that registration never happens, so the
    /// menu WORKS while our bind silently never lands and every edit strands in MCM's store file. Three
    /// nets now: (1) a once-per-process RESCUE that reads MCM's store file directly — no MCM types — and
    /// adopts stranded values into config.json under strict never-clobber rules; (2) after a successful
    /// bind, a ~1s POLL that syncs menu↔config by snapshot comparison, so nothing depends on MCM raising
    /// its save event; (3) diagnostics — log lines for bind success/failure, and a plain player notice
    /// when MCM is present but our settings never register.
    /// </summary>
    internal static class McmBridge
    {
        private static bool _bound;
        private static int _failures;
        private static DateTime _nextAttemptUtc = DateTime.MinValue;
        private static bool _rescueDone;
        private static DateTime _firstSeenMcmUtc = DateTime.MinValue;
        private static bool _unboundWarned;
        private static string _lastMenuSig = string.Empty;
        private static string _lastCfgSig = string.Empty;

        /// <summary>Offers the bridge a heartbeat: rescue once, then bind if MCM is present, then keep
        /// the menu and config in step. Safe to call when MCM is absent — returns quietly. Best-effort:
        /// any failure only costs the menu, never the mod. Called every application tick (self-throttled
        /// to ~1s): MCM registers our settings on its own schedule, and until the bind lands, anything
        /// the player edits in the menu never reaches config.json.</summary>
        public static void TryBind(ModConfig live)
        {
            if (live == null) return;
            var now = DateTime.UtcNow;
            if (now < _nextAttemptUtc) return;
            _nextAttemptUtc = now.AddSeconds(1.0);

            // Works with or without MCM installed (plain file IO, no MCM types): values the player set
            // in the menu while the bridge was NOT connected live on in MCM's own store file — adopt
            // what config.json is plainly missing, so a stranded key or backend choice is not lost.
            TryRescueOnce(live);

            // Note: do NOT reference any MCM type in this method's body — the check below must not force
            // the CLR to load MCMv5 when the module is absent. All MCM contact is quarantined in the
            // NoInlining helpers.
            var mcmLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, "MCMv5", StringComparison.OrdinalIgnoreCase));
            if (!mcmLoaded) return;
            if (_firstSeenMcmUtc == DateTime.MinValue) _firstSeenMcmUtc = now;

            if (_bound)
            {
                try { SyncTick(live); } catch { /* one bad tick must not kill the bridge */ }
                return;
            }

            try
            {
                // Bind returns false if MCM is up but our settings aren't registered yet; leave _bound
                // false so the next tick retries.
                if (Bind(live))
                {
                    _bound = true;
                    ModLog.Info("MCM menu bound — settings sync is live" +
                                (_failures > 0 ? " (after " + _failures + " failed attempts)." : "."));
                }
                else
                {
                    WarnIfNeverRegistered(now);
                }
            }
            catch (Exception ex)
            {
                // NEVER give up: on some machines MCM (or a dependency it leans on) is still warming up
                // when the first attempts fire, and on others it heals only after a screen change — the
                // third Nexus round taught us that 3 strikes in 5 seconds is far too impatient. Back off
                // to ~15s and keep knocking. First failures log the FULL stack (the exact throw site
                // inside MCM is the diagnosis — type+message alone told us nothing), later ones a short
                // line every 20th so the log stays readable.
                _failures++;
                _nextAttemptUtc = now.AddSeconds(15.0);
                if (_failures <= 3)
                    ModLog.Error("MCM bind attempt " + _failures + " — " + ex);
                else if (_failures % 20 == 0)
                    ModLog.Error("MCM bind still failing (attempt " + _failures + ")", ex);
                if (_failures == 3)
                {
                    // The menu is a convenience; config.json still works. Note it once and keep trying.
                    TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                        "Immersive AI: the mod options menu is not connecting (" + ex.GetType().Name + ") — " +
                        "changes there may not take hold. config.json always works; details in log.txt."));
                }
            }
        }

        /// <summary>MCM is loaded but never registered our settings (its own init chain can fail on
        /// some setups while the menu still renders) — after a generous grace, tell the player plainly
        /// that menu edits will not take hold, instead of leaving a silent mystery.</summary>
        private static void WarnIfNeverRegistered(DateTime now)
        {
            if (_unboundWarned || (now - _firstSeenMcmUtc).TotalSeconds < 90) return;
            if (TaleWorlds.CampaignSystem.Campaign.Current == null) return; // notices need a game up
            _unboundWarned = true;
            ModLog.Warn("MCM is installed but never registered the Immersive AI settings — menu edits " +
                        "cannot reach config.json. Edit " + ModConfig.ConfigFilePath + " directly.");
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                "Immersive AI: the mod options menu could not connect — changes made there will not take hold. " +
                "Edit config.json instead (Documents\\Mount and Blade II Bannerlord\\Configs\\ImmersiveAI)."));
        }

        // Kept out-of-line so TryBind can be JIT-compiled without resolving MCM types when MCM is absent.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Bind(ModConfig live)
        {
            var s = ImmersiveAiMcmSettings.Instance;
            if (s == null) return false; // MCM present but our settings not registered yet — retry later.

            RepairMenuInstance(s);
            PushConfigToMenu(s, live);
            RecordSnapshots(s, live);
            // The fast path: MCM raises PropertyChanged ("SAVE_TRIGGERED") when the menu is saved. The
            // poll in SyncTick covers MCM builds where this event never comes — same code either way.
            s.PropertyChanged += (_, __) =>
            {
                try { PullAndRecord(s, live); }
                catch { /* a bad edit must not crash the menu */ }
            };
            return true;
        }

        /// <summary>The event-independent sync: every ~1s, if the menu changed since the last sync the
        /// menu wins (it is the player's hand); otherwise, if the config changed (the socialness stepper,
        /// a completed endpoint), the menu is refreshed to match. Snapshots make it ping-pong-free.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SyncTick(ModConfig live)
        {
            var s = ImmersiveAiMcmSettings.Instance;
            if (s == null) return;
            if (!string.Equals(MenuSignature(s), _lastMenuSig, StringComparison.Ordinal))
            {
                PullAndRecord(s, live);
                return;
            }
            if (!string.Equals(CfgSignature(live), _lastCfgSig, StringComparison.Ordinal))
            {
                PushConfigToMenu(s, live);
                RecordSnapshots(s, live);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PullAndRecord(ImmersiveAiMcmSettings s, ModConfig live)
        {
            var before = CfgSignature(live);
            PullMenuToConfig(s, live);
            live.Normalize();
            // Only the memory numbers are mirrored back after Normalize — they are the fields it can
            // silently CORRECT (a keep window halved under its ceiling), and a slider cannot be
            // mangled mid-edit the way a text field being typed into would be by a full push.
            PushMemoryToMenu(s, live);
            if (!string.Equals(CfgSignature(live), before, StringComparison.Ordinal))
            {
                live.Save();
                ModLog.Info("Mod menu edits synced to config.json (backend " + live.Backend + ").");
            }
            RecordSnapshots(s, live);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RecordSnapshots(ImmersiveAiMcmSettings s, ModConfig live)
        {
            _lastMenuSig = MenuSignature(s);
            _lastCfgSig = CfgSignature(live);
        }

        /// <summary>Some MCM builds materialize the settings without running our constructors (their
        /// ctor-delegate machinery can fail under mismatched dependencies, falling back to an
        /// UNINITIALIZED object) — every dropdown null, every string null, and both MCM's own load/save
        /// and our bind then die on it (the third Nexus round: ArgumentOutOfRange/NullReference inside
        /// bind). Rebuild what is missing so the menu and the sync work anyway; on a healthy MCM the
        /// initializers ran and this touches nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RepairMenuInstance(ImmersiveAiMcmSettings s)
        {
            var repaired = false;
            if (s.Backend == null) { s.Backend = new Dropdown<string>(McmChoiceLists.Backends, 0); repaired = true; }
            if (s.AnthropicModel == null) { s.AnthropicModel = new Dropdown<string>(McmChoiceLists.AnthropicModels, 0); repaired = true; }
            if (s.OpenAIModel == null) { s.OpenAIModel = new Dropdown<string>(McmChoiceLists.OpenAIModels, 0); repaired = true; }
            if (s.OpenRouterModel == null) { s.OpenRouterModel = new Dropdown<string>(McmChoiceLists.OpenRouterModels, 0); repaired = true; }
            if (s.GeminiModel == null) { s.GeminiModel = new Dropdown<string>(McmChoiceLists.GeminiModels, 0); repaired = true; }
            if (s.DeepSeekModel == null) { s.DeepSeekModel = new Dropdown<string>(McmChoiceLists.DeepSeekModels, 0); repaired = true; }
            if (s.ChatWindowHotkey == null) { s.ChatWindowHotkey = new Dropdown<string>(McmChoiceLists.HotkeyKeys, 0); repaired = true; }
            if (s.LetterWindowHotkey == null) { s.LetterWindowHotkey = new Dropdown<string>(McmChoiceLists.HotkeyKeys, 8); repaired = true; }
            if (s.PersonaSparkMode == null) { s.PersonaSparkMode = new Dropdown<string>(McmChoiceLists.SparkModes, 0); repaired = true; }
            if (s.VoiceDelivery == null) { s.VoiceDelivery = new Dropdown<string>(McmChoiceLists.VoiceDeliveryModes, 1); repaired = true; }
            if (s.VoiceForMe == null) { s.VoiceForMe = new Dropdown<string>(McmChoiceLists.NoVoice, 0); repaired = true; }
            if (s.VoicePanicKey == null) { s.VoicePanicKey = new Dropdown<string>(McmChoiceLists.PanicKeys, 0); repaired = true; }
            if (s.NightWindowHotkey == null) { s.NightWindowHotkey = new Dropdown<string>(McmChoiceLists.HotkeyKeys, 9); repaired = true; }
            if (repaired)
                ModLog.Warn("MCM served an uninitialized settings instance — dropdowns rebuilt by hand " +
                            "(an MCM/dependency version mismatch; the menu should still work now).");
        }

        /// <summary>A dropdown's selected value, or null when the dropdown is missing or its innards
        /// throw (unhealthy MCM builds) — never an exception.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string? SelectedOf(Dropdown<string>? dropdown)
        {
            try { return dropdown?.SelectedValue; }
            catch { return null; }
        }

        /// <summary>Everything the menu can edit, as one comparison key. Dropdown reads go through
        /// <see cref="SelectedOf"/> so a broken dropdown degrades to a blank instead of killing the
        /// whole sync tick.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string MenuSignature(ImmersiveAiMcmSettings s)
        {
            return string.Join("",
                SelectedOf(s.Backend), s.AnthropicApiKey, ChosenModel(s.AnthropicModel, s.AnthropicModelCustom),
                s.OpenAIApiKey, ChosenModel(s.OpenAIModel, s.OpenAIModelCustom),
                s.OpenRouterApiKey, ChosenModel(s.OpenRouterModel, s.OpenRouterModelCustom),
                s.GeminiApiKey, ChosenModel(s.GeminiModel, s.GeminiModelCustom),
                s.DeepSeekApiKey, SelectedOf(s.DeepSeekModel),
                s.OpenAIBaseUrl, s.LocalEndpoint, s.LocalModel, s.LocalContextWindow, s.MaxTokens,
                s.EnableChatWindow, SelectedOf(s.ChatWindowHotkey), SelectedOf(s.LetterWindowHotkey),
                s.NotifyWhenReplyReady, s.EnableNpcInitiatedChats, s.Socialness, s.ShowSocialnessControl,
                s.EnableLetters, s.EnableWorldRecall, s.EnableWebSearch,
                s.EnableConversationHiring, s.ConversationHiringHagglePercent,
                s.EnableConversationMarriage, s.AllowCompanionMarriage, s.MarriageNeedsFamilyConsent,
                s.MarriageDowryHagglePercent, s.CourtshipCharmSlack, s.MinBetrothalDays,
                s.EnableLoversRoad, s.LoverRansomHagglePercent, s.EnableClosedDoors, s.AllowDutyNights,
                SelectedOf(s.PersonaSparkMode),
                s.EnableVoice, s.VoiceAutoSpeak, s.VoiceSpeakWhenClosed, s.VoiceSpeakActedParts, s.VoiceAutoCast,
                SelectedOf(s.VoiceDelivery),
                SelectedOf(s.VoiceForMe),
                s.VoiceSpeakReachOuts, SelectedOf(s.VoicePanicKey), s.CloudVoiceApiKey,
                s.EnableNights, s.NightsAutoVisit, s.NightsPreventChild, s.NightCooldownHours,
                s.ConceptionRevealDelayDays, s.ShowConceptionOdds, s.PaidNightsDisorganizeParty,
                s.AskWhatYouHaveInMind,
                s.EnableNightWindow, SelectedOf(s.NightWindowHotkey),
                s.RevertMemoriesWithSaves,
                s.MaxRecentMemoryPercent, s.MinRecentMemoryPercentAfterCompression,
                s.MaxRecentTurns, s.KeepRecentTurnsAfterCompression,
                s.MaxRecentDays, s.KeepRecentDaysAfterCompression,
                s.MaxMemoryWriteTokens, s.NotifyOnMemoryRefactor,
                s.ShowCostNotices, s.MaxDailyRequests, s.TalkScreenFpsLimit, s.DevMode);
        }

        /// <summary>The config side of the same fields — raw values; any change means "push to menu".</summary>
        private static string CfgSignature(ModConfig c)
        {
            return string.Join("",
                c.Backend, c.AnthropicApiKey, c.AnthropicModel,
                c.OpenAIApiKey, c.OpenAIModel,
                c.OpenRouterApiKey, c.OpenRouterModel,
                c.GeminiApiKey, c.GeminiModel,
                c.DeepSeekApiKey, c.DeepSeekModel,
                c.OpenAIBaseUrl, c.LocalEndpoint, c.LocalModel, c.LocalContextWindow, c.MaxTokens,
                c.EnableChatWindow, c.ChatWindowHotkey, c.LetterWindowHotkey,
                c.NotifyWhenReplyReady, c.EnableNpcInitiatedChats, c.DailyInitiationRate, c.ShowSocialnessControl,
                c.EnableLetters, c.EnableWorldRecall, c.EnableWebSearch,
                c.EnableConversationHiring, c.ConversationHiringHagglePercent,
                c.EnableConversationMarriage, c.AllowCompanionMarriage, c.MarriageNeedsFamilyConsent,
                c.MarriageDowryHagglePercent, c.CourtshipCharmSlack, c.MinBetrothalDays,
                c.EnableLoversRoad, c.LoverRansomHagglePercent, c.EnableClosedDoors, c.AllowDutyNights,
                c.PersonaSparkMode,
                c.EnableVoice, c.VoiceAutoSpeak, c.VoiceSpeakWhenClosed, c.VoiceSpeakActedParts, c.VoiceAutoCast, c.VoiceDelivery,
                // The castings live in the voices' own sheet, not in config.json — so the signature
                // asks the service for them, and a voice given in the talk screen's panel shows up
                // in the menu on the next poll without either side owning the truth twice.
                Voice.VoiceService.PlayerVoiceId,
                c.VoiceSpeakReachOuts, c.VoicePanicKey, c.CloudVoiceApiKey,
                c.EnableNights, c.NightsAutoVisit, c.NightsPreventChild, c.NightCooldownHours,
                c.ConceptionRevealDelayDays, c.ShowConceptionOdds, c.PaidNightsDisorganizeParty,
                c.AskWhatYouHaveInMind,
                c.EnableNightWindow, c.NightWindowHotkey,
                c.RevertMemoriesWithSaves,
                c.MaxRecentMemoryPercent, c.MinRecentMemoryPercentAfterCompression,
                c.MaxRecentTurns, c.KeepRecentTurnsAfterCompression,
                c.MaxRecentDays, c.KeepRecentDaysAfterCompression,
                c.MaxMemoryWriteTokens, c.NotifyOnMemoryRefactor,
                c.ShowCostNotices, c.MaxDailyRequests, c.TalkScreenFpsLimit, c.DevMode);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushConfigToMenu(ImmersiveAiMcmSettings s, ModConfig c)
        {
            Select(s.Backend, c.Backend);
            s.AnthropicApiKey = c.AnthropicApiKey ?? string.Empty;
            s.AnthropicModelCustom = SelectOrCustom(s.AnthropicModel, c.AnthropicModel);
            s.OpenAIApiKey = c.OpenAIApiKey ?? string.Empty;
            s.OpenAIModelCustom = SelectOrCustom(s.OpenAIModel, c.OpenAIModel);
            s.OpenRouterApiKey = c.OpenRouterApiKey ?? string.Empty;
            s.OpenRouterModelCustom = SelectOrCustom(s.OpenRouterModel, c.OpenRouterModel);
            s.GeminiApiKey = c.GeminiApiKey ?? string.Empty;
            s.GeminiModelCustom = SelectOrCustom(s.GeminiModel, c.GeminiModel);
            s.DeepSeekApiKey = c.DeepSeekApiKey ?? string.Empty;
            // DeepSeek has no custom-id field (their catalog is two models); an id the list does not
            // carry simply leaves the dropdown where it stands rather than being wedged in.
            Select(s.DeepSeekModel, c.DeepSeekModel);
            // The endpoint shows blank while it is the real OpenAI — the field is for the exception.
            s.OpenAIBaseUrl = string.Equals(c.OpenAIBaseUrl, ModConfig.DefaultOpenAIEndpoint, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : (c.OpenAIBaseUrl ?? string.Empty);
            // The local endpoint shows its real value — where the server listens IS the information.
            s.LocalEndpoint = c.LocalEndpoint ?? string.Empty;
            s.LocalModel = c.LocalModel ?? string.Empty;
            s.LocalContextWindow = Clamp(c.LocalContextWindow, 2048, 131072);
            s.MaxTokens = Clamp(c.MaxTokens, 100, 2000);

            s.EnableChatWindow = c.EnableChatWindow;
            SelectOrAdd(s.ChatWindowHotkey, c.ChatWindowHotkey);
            SelectOrAdd(s.LetterWindowHotkey, c.LetterWindowHotkey);
            s.NotifyWhenReplyReady = c.NotifyWhenReplyReady;

            s.EnableNpcInitiatedChats = c.EnableNpcInitiatedChats;
            s.Socialness = (float)Math.Max(0.0, Math.Min(24.0, c.DailyInitiationRate));
            s.ShowSocialnessControl = c.ShowSocialnessControl;
            s.EnableLetters = c.EnableLetters;
            s.EnableWorldRecall = c.EnableWorldRecall;
            s.EnableWebSearch = c.EnableWebSearch;
            s.EnableConversationHiring = c.EnableConversationHiring;
            s.ConversationHiringHagglePercent = Clamp(c.ConversationHiringHagglePercent, 0, 90);
            s.EnableConversationMarriage = c.EnableConversationMarriage;
            s.AllowCompanionMarriage = c.AllowCompanionMarriage;
            s.MarriageNeedsFamilyConsent = c.MarriageNeedsFamilyConsent;
            s.MarriageDowryHagglePercent = Clamp(c.MarriageDowryHagglePercent, 0, 90);
            s.CourtshipCharmSlack = Clamp(c.CourtshipCharmSlack, 0, 4);
            s.MinBetrothalDays = Clamp(c.MinBetrothalDays, 0, 30);
            s.EnableLoversRoad = c.EnableLoversRoad;
            s.LoverRansomHagglePercent = Clamp(c.LoverRansomHagglePercent, 0, 90);
            s.EnableClosedDoors = c.EnableClosedDoors;
            s.AllowDutyNights = c.AllowDutyNights;
            Select(s.PersonaSparkMode, SparkModeLabel(c.PersonaSparkMode));
            s.EnableVoice = c.EnableVoice;
            s.VoiceAutoSpeak = c.VoiceAutoSpeak;
            s.VoiceSpeakWhenClosed = c.VoiceSpeakWhenClosed;
            s.VoiceSpeakActedParts = c.VoiceSpeakActedParts;
            s.VoiceAutoCast = c.VoiceAutoCast;
            Select(s.VoiceDelivery, VoiceDeliveryLabel(c.VoiceDelivery));
            s.VoiceSpeakReachOuts = c.VoiceSpeakReachOuts;
            SelectOrAdd(s.VoicePanicKey, c.VoicePanicKey);
            s.CloudVoiceApiKey = c.CloudVoiceApiKey ?? string.Empty;
            PushVoiceCastings(s);
            s.EnableNights = c.EnableNights;
            s.NightsAutoVisit = c.NightsAutoVisit;
            s.NightsPreventChild = c.NightsPreventChild;
            s.NightCooldownHours = Clamp(c.NightCooldownHours, 1, 72);
            s.ConceptionRevealDelayDays = Clamp(c.ConceptionRevealDelayDays, 0, 30);
            s.ShowConceptionOdds = c.ShowConceptionOdds;
            s.PaidNightsDisorganizeParty = c.PaidNightsDisorganizeParty;
            s.AskWhatYouHaveInMind = c.AskWhatYouHaveInMind;
            s.EnableNightWindow = c.EnableNightWindow;
            Select(s.NightWindowHotkey, c.NightWindowHotkey);
            s.RevertMemoriesWithSaves = c.RevertMemoriesWithSaves;

            PushMemoryToMenu(s, c);

            s.ShowCostNotices = c.ShowCostNotices;
            s.MaxDailyRequests = Clamp(c.MaxDailyRequests, 0, 2000);
            s.TalkScreenFpsLimit = Clamp(c.TalkScreenFpsLimit, 0, 360);

            s.DevMode = c.DevMode;
        }

        /// <summary>The consolidation dials, config → menu. Split out of the full push so it can also
        /// run right after <see cref="ModConfig.Normalize"/> on a pull: these are the values Normalize
        /// may correct, and a menu still showing the uncorrected number would be a quiet lie.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushMemoryToMenu(ImmersiveAiMcmSettings s, ModConfig c)
        {
            s.MaxRecentMemoryPercent = Clamp(c.MaxRecentMemoryPercent,
                MemorySettingsMetadata.MinMemoryPercent, MemorySettingsMetadata.MaxMemoryPercent);
            s.MinRecentMemoryPercentAfterCompression = Clamp(c.MinRecentMemoryPercentAfterCompression,
                MemorySettingsMetadata.MinMemoryPercent, MemorySettingsMetadata.MaxMemoryPercent);
            s.MaxRecentTurns = Clamp(c.MaxRecentTurns,
                MemorySettingsMetadata.MinRecentTurns, MemorySettingsMetadata.MaxRecentTurnsCeiling);
            s.KeepRecentTurnsAfterCompression = Clamp(c.KeepRecentTurnsAfterCompression,
                1, MemorySettingsMetadata.MaxRecentTurnsCeiling);
            s.MaxRecentDays = Clamp(c.MaxRecentDays,
                MemorySettingsMetadata.MinRecentDays, MemorySettingsMetadata.MaxRecentDaysCeiling);
            s.KeepRecentDaysAfterCompression = Clamp(c.KeepRecentDaysAfterCompression,
                1, MemorySettingsMetadata.MaxRecentDaysCeiling);
            s.MaxMemoryWriteTokens = Clamp(c.MaxMemoryWriteTokens,
                MemorySettingsMetadata.MinMemoryWriteTokens, MemorySettingsMetadata.MaxMemoryWriteTokensCeiling);
            s.NotifyOnMemoryRefactor = c.NotifyOnMemoryRefactor;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PullMenuToConfig(ImmersiveAiMcmSettings s, ModConfig c)
        {
            c.Backend = SelectedOf(s.Backend) ?? c.Backend;
            c.AnthropicApiKey = s.AnthropicApiKey ?? string.Empty;
            c.AnthropicModel = ChosenModel(s.AnthropicModel, s.AnthropicModelCustom) ?? c.AnthropicModel;
            c.OpenAIApiKey = s.OpenAIApiKey ?? string.Empty;
            c.OpenAIModel = ChosenModel(s.OpenAIModel, s.OpenAIModelCustom) ?? c.OpenAIModel;
            c.OpenRouterApiKey = s.OpenRouterApiKey ?? string.Empty;
            c.OpenRouterModel = ChosenModel(s.OpenRouterModel, s.OpenRouterModelCustom) ?? c.OpenRouterModel;
            c.GeminiApiKey = s.GeminiApiKey ?? string.Empty;
            c.GeminiModel = ChosenModel(s.GeminiModel, s.GeminiModelCustom) ?? c.GeminiModel;
            c.DeepSeekApiKey = s.DeepSeekApiKey ?? string.Empty;
            c.DeepSeekModel = SelectedOf(s.DeepSeekModel) ?? c.DeepSeekModel;
            // Blank means the real OpenAI; Normalize (run by the caller) completes a pasted /v1 base.
            c.OpenAIBaseUrl = string.IsNullOrWhiteSpace(s.OpenAIBaseUrl)
                ? ModConfig.DefaultOpenAIEndpoint
                : s.OpenAIBaseUrl.Trim();
            // Blank means LM Studio's default door; Normalize completes a pasted /v1 base here too.
            c.LocalEndpoint = string.IsNullOrWhiteSpace(s.LocalEndpoint)
                ? ModConfig.DefaultLocalEndpoint
                : s.LocalEndpoint.Trim();
            c.LocalModel = (s.LocalModel ?? string.Empty).Trim();
            // The menu's slider is narrower than the config's honest range (131k vs 2M) — adopt its
            // value only when the player truly moved it, so a hand-set wide window survives a sync.
            if (s.LocalContextWindow != Clamp(c.LocalContextWindow, 2048, 131072))
                c.LocalContextWindow = s.LocalContextWindow;
            if (s.MaxTokens != Clamp(c.MaxTokens, 100, 2000))
                c.MaxTokens = s.MaxTokens;

            c.EnableChatWindow = s.EnableChatWindow;
            c.ChatWindowHotkey = SelectedOf(s.ChatWindowHotkey) ?? c.ChatWindowHotkey;
            c.LetterWindowHotkey = SelectedOf(s.LetterWindowHotkey) ?? c.LetterWindowHotkey;
            c.NotifyWhenReplyReady = s.NotifyWhenReplyReady;

            c.EnableNpcInitiatedChats = s.EnableNpcInitiatedChats;
            c.DailyInitiationRate = s.Socialness;
            // SocialnessManager reads this flag every tick, so the stepper folds away or returns
            // the moment the box is toggled — the live hide promised on the Steam page comments.
            c.ShowSocialnessControl = s.ShowSocialnessControl;
            c.EnableLetters = s.EnableLetters;
            c.EnableWorldRecall = s.EnableWorldRecall;
            c.EnableWebSearch = s.EnableWebSearch;
            c.EnableConversationHiring = s.EnableConversationHiring;
            c.ConversationHiringHagglePercent = s.ConversationHiringHagglePercent;
            c.EnableConversationMarriage = s.EnableConversationMarriage;
            c.AllowCompanionMarriage = s.AllowCompanionMarriage;
            c.MarriageNeedsFamilyConsent = s.MarriageNeedsFamilyConsent;
            c.MarriageDowryHagglePercent = s.MarriageDowryHagglePercent;
            c.CourtshipCharmSlack = s.CourtshipCharmSlack;
            c.MinBetrothalDays = s.MinBetrothalDays;
            c.EnableLoversRoad = s.EnableLoversRoad;
            c.LoverRansomHagglePercent = s.LoverRansomHagglePercent;
            c.EnableClosedDoors = s.EnableClosedDoors;
            c.AllowDutyNights = s.AllowDutyNights;
            c.PersonaSparkMode = SparkModeValue(SelectedOf(s.PersonaSparkMode)) ?? c.PersonaSparkMode;
            c.EnableVoice = s.EnableVoice;
            c.VoiceAutoSpeak = s.VoiceAutoSpeak;
            c.VoiceSpeakWhenClosed = s.VoiceSpeakWhenClosed;
            c.VoiceSpeakActedParts = s.VoiceSpeakActedParts;
            c.VoiceAutoCast = s.VoiceAutoCast;
            c.VoiceDelivery = VoiceDeliveryValue(SelectedOf(s.VoiceDelivery)) ?? c.VoiceDelivery;
            c.VoiceSpeakReachOuts = s.VoiceSpeakReachOuts;
            c.VoicePanicKey = SelectedOf(s.VoicePanicKey) ?? c.VoicePanicKey;
            c.CloudVoiceApiKey = s.CloudVoiceApiKey ?? string.Empty;
            PullVoiceCastings(s);
            c.EnableNights = s.EnableNights;
            c.NightsAutoVisit = s.NightsAutoVisit;
            c.NightsPreventChild = s.NightsPreventChild;
            c.NightCooldownHours = s.NightCooldownHours;
            c.ConceptionRevealDelayDays = s.ConceptionRevealDelayDays;
            c.ShowConceptionOdds = s.ShowConceptionOdds;
            c.PaidNightsDisorganizeParty = s.PaidNightsDisorganizeParty;
            c.AskWhatYouHaveInMind = s.AskWhatYouHaveInMind;
            c.EnableNightWindow = s.EnableNightWindow;
            c.NightWindowHotkey = SelectedOf(s.NightWindowHotkey) ?? c.NightWindowHotkey;
            c.RevertMemoriesWithSaves = s.RevertMemoriesWithSaves;

            // The consolidation dials: the menu's ranges are the config's own rails, so these ride
            // straight across. Normalize (run by the caller) then enforces the pairs' order — a keep
            // window at or above its ceiling is halved — and PushMemoryToMenu mirrors that correction
            // back, so the menu never shows a number the mod is not actually using.
            c.MaxRecentMemoryPercent = s.MaxRecentMemoryPercent;
            c.MinRecentMemoryPercentAfterCompression = s.MinRecentMemoryPercentAfterCompression;
            c.MaxRecentTurns = s.MaxRecentTurns;
            c.KeepRecentTurnsAfterCompression = s.KeepRecentTurnsAfterCompression;
            c.MaxRecentDays = s.MaxRecentDays;
            c.KeepRecentDaysAfterCompression = s.KeepRecentDaysAfterCompression;
            c.MaxMemoryWriteTokens = s.MaxMemoryWriteTokens;
            c.NotifyOnMemoryRefactor = s.NotifyOnMemoryRefactor;

            c.ShowCostNotices = s.ShowCostNotices;
            if (s.MaxDailyRequests != Clamp(c.MaxDailyRequests, 0, 2000))
                c.MaxDailyRequests = s.MaxDailyRequests;
            if (s.TalkScreenFpsLimit != Clamp(c.TalkScreenFpsLimit, 0, 360))
                c.TalkScreenFpsLimit = s.TalkScreenFpsLimit;

            c.DevMode = s.DevMode;
        }

        // ── The voice castings ──────────────────────────────────────────────────────
        //
        // The one pair of dropdowns in this menu whose CHOICES are not a fixed vocabulary: they are
        // whatever voices the player has made or been given, which can change between one session and
        // the next — and MCM persists a dropdown as an INDEX into its list. An index into a list that
        // has grown a voice at the top is a different voice; silently recasting everybody because a
        // folder was added is exactly the sort of bug nobody would think to report.
        //
        // So: the lists are rebuilt from the live shelf here, the selection is made BY VOICE ID, and
        // whatever index MCM restored is thrown away. The truth lives in the voices' own sheet
        // (assignments.json), never in config.json — the panel in the talk screen writes the same
        // sheet, so the two doors agree without either owning it.
        //
        // The names shown are the voices' own; two voices may share one (nothing stops a player
        // naming two folders "Sibylla"), so the mapping back is by POSITION in the list we just
        // built, which is exact for as long as it is on screen.

        /// <summary>The shelf as the menu last showed it, in order, so a chosen row maps back to the
        /// voice it named rather than to a name that might be shared.</summary>
        private static List<string> _voiceIdsInMenuOrder = new List<string>();
        private static string _voiceShelfShape = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushVoiceCastings(ImmersiveAiMcmSettings s)
        {
            try
            {
                var shelf = Voice.VoiceService.Shelf();

                var labels = new List<string> { McmChoiceLists.NoVoiceLabel };
                var ids = new List<string> { string.Empty };
                foreach (var voice in shelf)
                {
                    labels.Add(voice.Name);
                    ids.Add(voice.Id);
                }

                // Only build NEW dropdowns when the shelf itself has changed. A push happens
                // whenever any watched setting moves, and handing MCM three fresh Dropdown objects
                // every time would throw away a selection the player is in the middle of making —
                // and, on some MCM builds, not refresh the visible list anyway. Selecting inside the
                // dropdown we already gave it is both cheaper and better behaved.
                var shape = string.Join("|", ids);
                var rebuild = shape != _voiceShelfShape;
                _voiceShelfShape = shape;
                _voiceIdsInMenuOrder = ids;

                if (rebuild)
                {
                    s.VoiceForMe = Rebuilt(labels, ids, Voice.VoiceService.PlayerVoiceId);
                    return;
                }

                SelectById(s.VoiceForMe, labels, ids, Voice.VoiceService.PlayerVoiceId);
            }
            catch (Exception ex)
            {
                ModLog.Warn("MCM: the voice lists could not be built — " + ex.Message);
            }
        }

        /// <summary>Moves an existing dropdown onto the voice with this id — BY ID, never by the
        /// index MCM happens to have stored.</summary>
        private static void SelectById(Dropdown<string>? dropdown, List<string> labels, List<string> ids, string chosenId)
        {
            try
            {
                if (dropdown == null) return;
                for (var i = 0; i < ids.Count; i++)
                {
                    var match = string.IsNullOrEmpty(chosenId)
                        ? string.IsNullOrEmpty(ids[i])
                        : string.Equals(ids[i], chosenId, StringComparison.OrdinalIgnoreCase);
                    if (!match) continue;
                    if (dropdown.SelectedIndex != i) dropdown.SelectedIndex = i;
                    return;
                }
            }
            catch { /* an unhealthy dropdown is never worth a failed sync */ }
        }

        private static Dropdown<string> Rebuilt(List<string> labels, List<string> ids, string chosenId)
        {
            var at = 0;
            for (var i = 0; i < ids.Count; i++)
                if (!string.IsNullOrEmpty(ids[i]) && string.Equals(ids[i], chosenId, StringComparison.OrdinalIgnoreCase))
                { at = i; break; }
            return new Dropdown<string>(labels, at);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PullVoiceCastings(ImmersiveAiMcmSettings s)
        {
            try
            {
                // Before the lists have ever been built, the menu holds only the placeholder — and
                // reading a casting out of THAT would clear all three. Nothing to pull yet.
                if (_voiceIdsInMenuOrder.Count <= 1) return;

                var mine = IdOfSelected(s.VoiceForMe);

                if (mine != null && mine != Voice.VoiceService.PlayerVoiceId) Voice.VoiceService.SetPlayerVoice(mine);
            }
            catch (Exception ex)
            {
                ModLog.Warn("MCM: a voice casting could not be read — " + ex.Message);
            }
        }

        /// <summary>The voice id a dropdown's selection means. Null when it cannot be read at all —
        /// which must never be treated as "no voice", or an unhealthy dropdown would silence
        /// everybody. Empty string IS a real answer: the player chose "(none)".</summary>
        private static string? IdOfSelected(Dropdown<string>? dropdown)
        {
            try
            {
                if (dropdown == null) return null;
                var at = dropdown.SelectedIndex;
                if (at < 0 || at >= _voiceIdsInMenuOrder.Count) return null;
                return _voiceIdsInMenuOrder[at];
            }
            catch { return null; }
        }

        // ── The store-file rescue ───────────────────────────────────────────────────
        // Menu edits made while the bridge was not connected (older mod versions, or MCM builds whose
        // own container never registers our settings even though the menu renders) land ONLY in MCM's
        // store file. Read that file directly — plain IO and JSON, valid even when MCM is absent — and
        // adopt what config.json is plainly missing. Strict never-clobber rules: keys fill only EMPTY
        // config keys; models/endpoints apply only over untouched defaults; the backend flips only when
        // the configured one cannot possibly speak (its key is missing) and the menu-chosen one can.

        private static void TryRescueOnce(ModConfig live)
        {
            if (_rescueDone) return;
            _rescueDone = true;
            try
            {
                var configsRoot = Path.GetDirectoryName(Path.GetDirectoryName(ModConfig.ConfigFilePath));
                if (string.IsNullOrEmpty(configsRoot)) return;
                var storePath = Path.Combine(configsRoot, "ModSettings", "Global", "ImmersiveAI", "ImmersiveAI_v1.json");
                if (!File.Exists(storePath)) return;
                var store = JObject.Parse(File.ReadAllText(storePath));
                var adopted = new List<string>();

                AdoptKey(store, "AnthropicApiKey", live.AnthropicApiKey, v => live.AnthropicApiKey = v, "Anthropic key", adopted);
                AdoptKey(store, "OpenAIApiKey", live.OpenAIApiKey, v => live.OpenAIApiKey = v, "OpenAI key", adopted);
                AdoptKey(store, "OpenRouterApiKey", live.OpenRouterApiKey, v => live.OpenRouterApiKey = v, "OpenRouter key", adopted);
                AdoptKey(store, "GeminiApiKey", live.GeminiApiKey, v => live.GeminiApiKey = v, "Gemini key", adopted);
                AdoptKey(store, "DeepSeekApiKey", live.DeepSeekApiKey, v => live.DeepSeekApiKey = v, "DeepSeek key", adopted);
                AdoptKey(store, "LocalModel", live.LocalModel, v => live.LocalModel = v, "local model", adopted);

                var defaults = new ModConfig();
                AdoptModel(store, "AnthropicModel", "AnthropicModelCustom", McmChoiceLists.AnthropicModels,
                    live.AnthropicModel, defaults.AnthropicModel, v => live.AnthropicModel = v, "Anthropic model", adopted);
                AdoptModel(store, "OpenAIModel", "OpenAIModelCustom", McmChoiceLists.OpenAIModels,
                    live.OpenAIModel, defaults.OpenAIModel, v => live.OpenAIModel = v, "OpenAI model", adopted);
                AdoptModel(store, "OpenRouterModel", "OpenRouterModelCustom", McmChoiceLists.OpenRouterModels,
                    live.OpenRouterModel, defaults.OpenRouterModel, v => live.OpenRouterModel = v, "OpenRouter model", adopted);
                AdoptModel(store, "GeminiModel", "GeminiModelCustom", McmChoiceLists.GeminiModels,
                    live.GeminiModel, defaults.GeminiModel, v => live.GeminiModel = v, "Gemini model", adopted);
                // No custom-id field exists for DeepSeek; naming one the store never holds simply
                // reads as "absent" and falls through to the dropdown index.
                AdoptModel(store, "DeepSeekModel", "DeepSeekModelCustom", McmChoiceLists.DeepSeekModels,
                    live.DeepSeekModel, defaults.DeepSeekModel, v => live.DeepSeekModel = v, "DeepSeek model", adopted);

                var storeEndpoint = TextOf(store, "OpenAIBaseUrl");
                if (!string.IsNullOrWhiteSpace(storeEndpoint) &&
                    string.Equals(live.OpenAIBaseUrl, ModConfig.DefaultOpenAIEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    live.OpenAIBaseUrl = storeEndpoint.Trim();
                    adopted.Add("custom endpoint");
                }
                var storeLocal = TextOf(store, "LocalEndpoint");
                if (!string.IsNullOrWhiteSpace(storeLocal) &&
                    string.Equals(live.LocalEndpoint, ModConfig.DefaultLocalEndpoint, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(storeLocal.Trim(), ModConfig.DefaultLocalEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    live.LocalEndpoint = storeLocal.Trim();
                    adopted.Add("local endpoint");
                }

                // The spark mode: adopted only over the untouched default, like the models.
                var storeSpark = SparkModeValue(McmChoiceLists.AtIndex(McmChoiceLists.SparkModes, IntOf(store, "PersonaSparkMode")));
                if (storeSpark != null && live.PersonaSparkMode == "Generate" && storeSpark != "Generate")
                {
                    live.PersonaSparkMode = storeSpark;
                    adopted.Add("starting personality " + storeSpark);
                }

                // The backend, last — so a key adopted above already counts toward "can speak".
                var storeBackend = McmChoiceLists.AtIndex(McmChoiceLists.Backends, IntOf(store, "Backend"));
                if (storeBackend != null &&
                    !string.Equals(storeBackend, live.Backend, StringComparison.OrdinalIgnoreCase) &&
                    !CanSpeak(live, live.Backend) && CanSpeak(live, storeBackend))
                {
                    live.Backend = storeBackend;
                    adopted.Add("Backend → " + storeBackend);
                }

                if (adopted.Count == 0) return;
                live.Normalize();
                live.Save();
                var what = string.Join(", ", adopted);
                ModLog.Info("Recovered settings stranded in the mod-menu store: " + what + ".");
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                    "Immersive AI: recovered mod-menu settings that had not been applied — " + what + "."));
            }
            catch (Exception ex)
            {
                ModLog.Error("MCM store rescue", ex);
            }
        }

        /// <summary>A backend the config as it stands could actually speak with: cloud backends need
        /// their key; the local backend is keyless by design and always counts.</summary>
        private static bool CanSpeak(ModConfig c, string backend)
        {
            switch (backend)
            {
                case "OpenAI": return !string.IsNullOrWhiteSpace(c.OpenAIApiKey);
                case "OpenRouter": return !string.IsNullOrWhiteSpace(c.OpenRouterApiKey);
                case "Gemini": return !string.IsNullOrWhiteSpace(c.GeminiApiKey);
                case "DeepSeek": return !string.IsNullOrWhiteSpace(c.DeepSeekApiKey);
                case "Local": return true;
                default: return !string.IsNullOrWhiteSpace(c.AnthropicApiKey);
            }
        }

        private static void AdoptKey(JObject store, string field, string current, Action<string> set,
            string label, List<string> adopted)
        {
            if (!string.IsNullOrWhiteSpace(current)) return; // never clobber a value the player has
            var stored = TextOf(store, field);
            if (string.IsNullOrWhiteSpace(stored)) return;
            set(stored.Trim());
            adopted.Add(label);
        }

        /// <summary>A model choice is adopted only over an untouched default: the custom field's text
        /// wins (it overrides the dropdown in the menu, same rule here), else a valid non-default
        /// dropdown index. A config model that differs from the default was chosen on purpose — kept.</summary>
        private static void AdoptModel(JObject store, string indexField, string customField, string[] choices,
            string current, string defaultModel, Action<string> set, string label, List<string> adopted)
        {
            if (!string.Equals(current, defaultModel, StringComparison.OrdinalIgnoreCase)) return;
            var custom = TextOf(store, customField);
            var chosen = !string.IsNullOrWhiteSpace(custom)
                ? custom.Trim()
                : McmChoiceLists.AtIndex(choices, IntOf(store, indexField));
            if (string.IsNullOrWhiteSpace(chosen) ||
                string.Equals(chosen, defaultModel, StringComparison.OrdinalIgnoreCase)) return;
            set(chosen);
            adopted.Add(label + " " + chosen);
        }

        private static string? TextOf(JObject store, string field)
        {
            var token = store[field];
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        private static int? IntOf(JObject store, string field)
        {
            var token = store[field];
            if (token == null) return null;
            try { return token.Value<int>(); } catch { return null; }
        }

        /// <summary>Selects an existing dropdown value; leaves the current selection if the value is not
        /// present (used for a fixed set like the backend, where an unknown value is a config error).
        /// Best-effort: a dropdown whose innards throw (unhealthy MCM builds) is left alone rather than
        /// killing the whole push.</summary>
        private static void Select(Dropdown<string> dropdown, string value)
        {
            if (dropdown == null || string.IsNullOrWhiteSpace(value)) return;
            try
            {
                var index = dropdown.IndexOf(value);
                if (index >= 0) dropdown.SelectedIndex = index;
            }
            catch { /* this field stays as it was; the rest of the push proceeds */ }
        }

        /// <summary>Selects the value in the dropdown when the curated list carries it (the custom field
        /// stays blank); otherwise leaves the dropdown alone and returns the value for the custom field —
        /// so a hand-set or exotic model id shows where it can be edited and cleared, not wedged into the
        /// curated menu. Clearing that field falls back to whatever the dropdown shows.</summary>
        private static string SelectOrCustom(Dropdown<string> dropdown, string value)
        {
            if (dropdown == null || string.IsNullOrWhiteSpace(value)) return string.Empty;
            try
            {
                var index = dropdown.IndexOf(value);
                if (index < 0) return value;
                dropdown.SelectedIndex = index;
                return string.Empty;
            }
            catch { return value; } // broken dropdown: the custom field carries the model instead
        }

        /// <summary>The model the menu currently means: the custom field while it holds text (it
        /// overrides the dropdown, as its label promises), the dropdown pick otherwise.</summary>
        private static string? ChosenModel(Dropdown<string>? dropdown, string custom)
        {
            if (!string.IsNullOrWhiteSpace(custom)) return custom.Trim();
            return SelectedOf(dropdown);
        }

        /// <summary>Selects a dropdown value, adding it first if the list does not already carry it — so a
        /// hotkey the player set by hand shows up in the menu instead of being silently dropped.</summary>
        private static void SelectOrAdd(Dropdown<string> dropdown, string value)
        {
            if (dropdown == null || string.IsNullOrWhiteSpace(value)) return;
            try
            {
                var index = dropdown.IndexOf(value);
                if (index < 0)
                {
                    dropdown.Add(value);
                    index = dropdown.Count - 1;
                }
                dropdown.SelectedIndex = index;
            }
            catch { /* this field stays as it was; the rest of the push proceeds */ }
        }

        // The spark mode's two spellings: the menu says "Ask first", config.json says "Ask".
        private static string SparkModeLabel(string configValue) =>
            configValue == "Ask" ? "Ask first" : configValue;

        private static string? SparkModeValue(string? menuLabel) =>
            menuLabel == null ? null : (menuLabel == "Ask first" ? "Ask" : menuLabel);

        // The menu spells the roads for a reader; config.json spells them for a parser. Matched on
        // the leading word so the parenthetical hints can be reworded without breaking stored files.
        private static string VoiceDeliveryLabel(string configValue)
        {
            if (string.Equals(configValue, "Streaming", StringComparison.OrdinalIgnoreCase))
                return McmChoiceLists.VoiceDeliveryModes[1];
            if (string.Equals(configValue, "ByLine", StringComparison.OrdinalIgnoreCase))
                return McmChoiceLists.VoiceDeliveryModes[2];
            return McmChoiceLists.VoiceDeliveryModes[0];
        }

        private static string? VoiceDeliveryValue(string? menuLabel)
        {
            if (menuLabel == null) return null;
            if (menuLabel.StartsWith("Streaming", StringComparison.OrdinalIgnoreCase)) return "Streaming";
            if (menuLabel.StartsWith("By line", StringComparison.OrdinalIgnoreCase)) return "ByLine";
            return "FullRead";
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
