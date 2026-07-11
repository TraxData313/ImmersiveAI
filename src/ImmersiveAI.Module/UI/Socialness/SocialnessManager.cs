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
    /// Keeps the socialness control alive on the campaign map: a small Gauntlet layer that claims
    /// the mouse ONLY while hovered (no focus, no hotkey claims, no resting grip on the cursor —
    /// the map underneath keeps its clicks AND its right-drag camera) and appears whenever the
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

        // Whether the layer currently claims the mouse (see the hover gate in Tick).
        private static bool _mouseClaimed;

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

                // The hover gate: claim the mouse only while the cursor is actually over the control.
                // A layer that holds the mouse permanently breaks the map's own right-drag camera
                // rotation (the map hides the cursor to turn; a mouse-claiming layer forces it back,
                // so the view snaps home after an inch — the 2026.07.12 camera bug). HitTest() (not
                // IsHitThisFrame, which the engine only sets for layers already holding a mouse mask)
                // hits only the buttons: everything else in the prefab is DoNotAcceptEvents.
                if (_layer != null)
                {
                    bool hovered = _layer.HitTest();
                    if (hovered && !_mouseClaimed)
                    {
                        _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);
                        _mouseClaimed = true;
                    }
                    else if (!hovered && _mouseClaimed)
                    {
                        _layer.InputRestrictions.ResetInputRestrictions();
                        _mouseClaimed = false;
                    }
                }
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
                // No input restrictions here: the mouse is claimed per-tick, only while hovered
                // (the hover gate above), so the map's camera and clicks stay whole everywhere else.
                _mouseClaimed = false;
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
                _mouseClaimed = false;
            }
        }
    }
}
