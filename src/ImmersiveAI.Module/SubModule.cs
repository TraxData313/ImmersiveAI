using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI
{
    public class SubModule : MBSubModuleBase
    {
        private bool _announced;

        // One config for the whole process: loaded once, shared by the behavior, the on-map controls,
        // and the MCM menu, so a change made in any of them is seen by all the others.
        private static ModConfig? _config;
        internal static ModConfig Config => _config ??= ModConfig.LoadOrCreate();

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            // The one Harmony patch: registers our portrait map-notice type with the map's
            // notification VM (a public game API; nothing vanilla is altered). If it fails,
            // MapNoticePatch.Applied stays false and offers fall back to the direct popup.
            UI.MapNoticePatch.TryApply();

            // The second: takes the player's own marriages out of the world's nightly conception
            // roll, so a child is begun on a night the player chose. Everything else in Calradia
            // keeps its own nights, and a failure here only means the world keeps deciding.
            Nights.PregnancyPatch.TryApply();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter starter)
            {
                var config = Config;
                // If MCM was not yet ready when the main menu came up, bind it now (guarded, no-op once bound).
                Mcm.McmBridge.TryBind(config);
                // Which campaign's memory folder is on stage isn't known until the save's id is
                // read (or minted) in the behavior's load/session hooks; clear any id left over
                // from a previous session so nothing can write into the wrong campaign meanwhile.
                // Migration of old flat-layout files also runs there, once the id is resolved.
                NpcPaths.ActiveCampaignId = string.Empty;
                var behavior = new ImmersiveChatBehavior(config);
                starter.AddBehavior(behavior);
                behavior.AddDialogs(starter);
                // The cost ledger needs the config (prices, caps) before the first call is made.
                UsageLedger.Configure(config);
                // The voices. Configure only reads settings — the speech engine is not started here
                // and is never started at all unless a line actually wants speaking, because bringing
                // it up costs seconds and gigabytes of video memory that a silent campaign should
                // never pay. Absent engine, absent models, absent runtime: it stays quiet and says so
                // once, and no reply is ever delayed by it.
                Voice.VoiceService.Configure(config);
                // A quiet "are you there?" to the LLM the moment a game is entered, so a missing key,
                // a wrong key, or a dead connection surfaces as plain guidance now — not as mute NPCs
                // discovered mid-conversation. Runs once per process, off-thread. On a keyless fresh
                // install it also raises the one-time first-run guide popup.
                LlmHealthCheck.RunOnce(config);
            }
        }

        // The chronicle's tally of who felled whom is kept on the field itself, by the game's own
        // agent-removal event: it fires once per soul who falls and names the hand. (The campaign's
        // hit event double-counts heavy blows — see BattleDownsMissionBehavior.) Harmless anywhere
        // else: it only marks anything while a real battle of the player's is under way.
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            try
            {
                if (Campaign.Current != null && Config.EnableBattleChronicle)
                    mission.AddMissionBehavior(new Battles.BattleDownsMissionBehavior());
            }
            catch { /* a missing tally must never cost a mission */ }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (!_announced)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI loaded."));
                _announced = true;
            }
            // Bind the MCM menu to our config as early as the main menu, so settings edited before a
            // campaign is even loaded take hold. A soft dependency: does nothing if MCM isn't installed.
            Mcm.McmBridge.TryBind(Config);
        }

        public override void OnGameEnd(Game game)
        {
            // Leaving a campaign lets the speech engine go. The sidecar's own watchdog would catch a
            // crash, but quitting to the main menu is not a crash and there is no reason to sit at it
            // holding several gigabytes of video memory. It comes back up on the next line that wants
            // speaking, in about a second and a half.
            try { Voice.VoiceService.Shutdown(); }
            catch (Exception ex) { ModLog.Error("voice: letting the engine go", ex); }
            base.OnGameEnd(game);
        }

        // Whether the inventory screen was up on the LAST frame. The baseline is taken on the
        // rising edge alone: taking it every frame would mean diffing against the half-finished
        // state the player is in the middle of arranging.
        private static bool _inventoryWasOpen;

        private static void TickInventoryWatch()
        {
            try
            {
                bool open = Game.Current?.GameStateManager?.ActiveState
                            is TaleWorlds.CampaignSystem.GameState.InventoryState;
                if (open && !_inventoryWasOpen) ImmersiveChatBehavior.NoteInventoryOpened();
                _inventoryWasOpen = open;
            }
            catch { /* a lost note must never touch the frame */ }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            // Keep offering the MCM bind (self-throttled, no-op once bound): MCM may register our
            // settings after both hooks above, and until the bind lands, menu edits never reach
            // config.json — the "set Backend at the main menu, get reverted at campaign start" bug.
            Mcm.McmBridge.TryBind(Config);
            // Drain UI updates queued by background LLM calls.
            MainThreadDispatcher.Drain();
            // The talk screen's little life — hotkey on the map, Enter/Escape while open — and, in
            // the same breath, the two older windows it replaced (idle unless the player kept them).
            UI.TalkUI.Tick();
            // And the window of the hearth, the third of the three (only one stands at a time).
            UI.NightWindow.NightWindowManager.Tick();
            // The socialness control: appears with the map, folds away everywhere else.
            UI.Socialness.SocialnessManager.Tick();
            // Hands the next piece of a spoken reply over the instant the one before it ends. A
            // no-op whenever nothing is speaking, which is almost always — and called EVERY FRAME on
            // purpose: the handover is timed by the clock, and a frame is the precision it gets.
            Voice.VoicePlayback.Tick();
            // The inventory screen coming up: the moment a soul's gear is about to change, and the
            // only place to stand to find out what it was before.
            TickInventoryWatch();
            // And the one key that must work no matter what is on the screen.
            TickPanicKey();
        }

        /// <summary>
        /// THE PANIC STOP (2026.08.15). One key, live everywhere — on the map, inside a battle, at a
        /// menu, with every window of ours shut — that silences a voice at once.
        /// <para>
        /// It exists because of the one thing speech can do that no other part of this mod can: an
        /// autoregressive model that misses its own ending keeps generating, and what comes out is
        /// babbling or screeching. Two rails above this one make that rare (every line carries an
        /// audio ceiling worked out from its own length, and a reading that runs past it is cut off
        /// and never kept) — but a stop button you have to open a window to reach is not a stop
        /// button, so this one is bound to a key and read from the raw keyboard.
        /// </para>
        /// <para>
        /// Deliberately NOT gated on the map, a campaign, or anything else: the whole value is that
        /// it works at the moment everything else has gone wrong. It is only read while something is
        /// actually speaking, so it costs nothing and steals nobody's Backspace the rest of the time.
        /// </para>
        /// </summary>
        private static void TickPanicKey()
        {
            try
            {
                if (!Voice.VoicePlayback.IsSpeaking) return;

                var name = Config?.VoicePanicKey;
                if (string.IsNullOrWhiteSpace(name)) return;

                if (!_panicKeyParsed || !string.Equals(name, _panicKeyName, StringComparison.OrdinalIgnoreCase))
                {
                    _panicKeyName = name!;
                    _panicKeyParsed = Enum.TryParse<InputKey>(name!.Trim(), ignoreCase: true, out _panicKey);
                    if (!_panicKeyParsed)
                        ModLog.Warn($"voice: \"{name}\" is not a key I know — the panic stop is unbound.");
                }
                if (!_panicKeyParsed) return;

                // While a text field holds the keyboard, this key is part of a word being written —
                // the default is Backspace, which is exactly how a typo is deleted. Reading is when
                // the player wants the cord; writing is when they want their character back.
                if (UI.MapOverlays.IsTypingSomewhere) return;

                if (!Input.IsKeyPressed(_panicKey)) return;

                Voice.VoiceService.Stop();
                InformationManager.DisplayMessage(new InformationMessage("The voice is stopped."));
            }
            catch { /* a stop key that throws would be its own small disaster */ }
        }

        private static InputKey _panicKey;
        private static string _panicKeyName = string.Empty;
        private static bool _panicKeyParsed;
    }
}
