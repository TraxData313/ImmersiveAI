using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
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
            // Chains the next sentence of a spoken reply when the one before it finishes. A no-op
            // whenever nothing is speaking, which is almost always.
            Voice.VoicePlayback.Tick();
        }
    }
}
