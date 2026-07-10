using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace ImmersiveAI.UI.Socialness
{
    /// <summary>
    /// Keeps the socialness control alive on the campaign map: a small mouse-only Gauntlet layer
    /// (no focus, no hotkey claims — the map underneath keeps working) that appears whenever the
    /// map is on stage and folds away in missions and other screens. Everything is best-effort in
    /// the chat window's manner: if the prefab or layer ever fails, the control stays away for the
    /// session and the game plays on untouched.
    /// </summary>
    internal static class SocialnessManager
    {
        private static ModConfig? _config;

        private static GauntletLayer? _layer;
        private static ScreenBase? _host;
        private static SocialnessVM? _vm;

        // One failure retires the control for the session — a broken nicety must not retry every tick.
        private static bool _broken;

        internal static void Configure(ModConfig config) => _config = config;

        /// <summary>Called every application tick (from SubModule). Cheap: a couple of state checks.</summary>
        internal static void Tick()
        {
            try
            {
                bool shown = _layer != null;

                // The screen it was born on is gone (encyclopedia, menus, loading) — let go first.
                if (shown && _host != ScreenManager.TopScreen)
                {
                    TearDown();
                    shown = false;
                }

                bool wanted = ShouldShowNow();
                if (wanted && !shown) Create();
                else if (!wanted && shown) TearDown();
            }
            catch { /* never let the little control touch the frame */ }
        }

        private static bool ShouldShowNow()
        {
            if (_broken || _config == null || !_config.ShowSocialnessControl) return false;
            // The number it edits drives both the walking-over and the letters; with both worlds
            // of reaching-out off, the control would steer nothing.
            if (!_config.EnableNpcInitiatedChats && !_config.EnableLetters) return false;
            if (Campaign.Current == null || Mission.Current != null) return false;
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState)) return false;
            return Hero.MainHero != null && Hero.MainHero.IsAlive;
        }

        private static void Create()
        {
            try
            {
                _vm = new SocialnessVM(_config!);
                _layer = new GauntletLayer("ImmersiveSocialness", 250);
                _layer.LoadMovie("ImmersiveSocialness", _vm);
                // Mouse only, never focus: the buttons click, the map's own keys and camera stay whole.
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);
                _host = ScreenManager.TopScreen;
                _host.AddLayer(_layer);
            }
            catch (Exception ex)
            {
                TearDown();
                _broken = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    "Immersive AI: the socialness control could not appear — " + ex.Message));
            }
        }

        private static void TearDown()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    _host?.RemoveLayer(_layer);
                }
            }
            catch { /* the screen may already be gone */ }
            finally
            {
                _layer = null;
                _host = null;
                _vm = null;
            }
        }
    }
}
