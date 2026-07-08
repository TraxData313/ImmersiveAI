using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI
{
    public class SubModule : MBSubModuleBase
    {
        private bool _announced;

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter starter)
            {
                var config = ModConfig.LoadOrCreate();
                // Reorganize any old flat-layout files into per-NPC folders up front, so the
                // Configs folder is migrated on load rather than piecemeal as NPCs are talked to.
                NpcPaths.MigrateAll();
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
        }
    }
}
