using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI
{
    public class SubModule : MBSubModuleBase
    {
        private bool _announced;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            // The one Harmony patch: registers our portrait map-notice type with the map's
            // notification VM (a public game API; nothing vanilla is altered). If it fails,
            // MapNoticePatch.Applied stays false and offers fall back to the direct popup.
            UI.MapNoticePatch.TryApply();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter starter)
            {
                var config = ModConfig.LoadOrCreate();
                // Which campaign's memory folder is on stage isn't known until the save's id is
                // read (or minted) in the behavior's load/session hooks; clear any id left over
                // from a previous session so nothing can write into the wrong campaign meanwhile.
                // Migration of old flat-layout files also runs there, once the id is resolved.
                NpcPaths.ActiveCampaignId = string.Empty;
                var behavior = new ImmersiveChatBehavior(config);
                starter.AddBehavior(behavior);
                behavior.AddDialogs(starter);
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (!_announced)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI loaded."));
                _announced = true;
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            // Drain UI updates queued by background LLM calls.
            MainThreadDispatcher.Drain();
            // The chat window's little life: hotkey on the map, Enter/Escape while open.
            UI.ChatWindow.ChatWindowManager.Tick();
            // The letter window's, likewise (its own hotkey; Escape closes, Enter never sends).
            UI.LetterWindow.LetterWindowManager.Tick();
            // The socialness control: appears with the map, folds away everywhere else.
            UI.Socialness.SocialnessManager.Tick();
        }
    }
}
