using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace ImmersiveAI.UI.NightWindow
{
    /// <summary>
    /// Owns the hearth window's life on screen — the letter window's twin, same layer plumbing, same
    /// calm-map rules, and the same yielding: only one of the three windows stands at a time.
    /// Escape folds the info overlay first, then closes. Everything is best-effort: a UI failure
    /// closes the window, never the game.
    /// </summary>
    internal static class NightWindowManager
    {
        private static ModConfig? _config;
        private static InputKey _hotkey = InputKey.H;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ScreenBase? _host;
        private static NightWindowVM? _vm;

        internal static bool IsOpen => _layer != null;

        internal static void Configure(ModConfig config)
        {
            _config = config;
            _hotkey = ParseKey(config.NightWindowHotkey);
        }

        private static InputKey ParseKey(string name) =>
            Enum.TryParse<InputKey>((name ?? string.Empty).Trim(), ignoreCase: true, out var key) ? key : InputKey.H;

        // ------------------------------ open / close ------------------------------

        internal static bool Open(Hero? preselect = null)
        {
            if (IsOpen)
            {
                if (preselect != null) _vm?.TrySelect(preselect);
                return true;
            }
            if (!CanOpenNow()) return false;

            try
            {
                _vm = new NightWindowVM(_config!);
                _layer = new GauntletLayer("ImmersiveNightWindow", 4500);
                _movie = _layer.LoadMovie("ImmersiveNightWindow", _vm);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;

                _host = ScreenManager.TopScreen;
                _host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);

                if (preselect != null) _vm.TrySelect(preselect);
                return true;
            }
            catch (Exception ex)
            {
                TearDown();
                InformationManager.DisplayMessage(new InformationMessage(
                    "Immersive AI: the window of the hearth could not open — " + ex.Message));
                return false;
            }
        }

        internal static void Close()
        {
            if (!IsOpen) return;
            TearDown();
        }

        /// <summary>Redraws the open window after a night is spent or the mode changes.</summary>
        internal static void Refresh()
        {
            try { if (IsOpen) _vm?.RefreshContacts(); }
            catch { /* a stale panel is not worth a throw */ }
        }

        // The evening's own question can hand the whole decision to this window instead of a chain
        // of popups (Anton, 2026.08.09). Parked and retried across ticks like the letter window's
        // "Write back", because the notice that sent us here may still be closing.
        private static int _pendingTicks;
        private static Action? _pendingFallback;

        internal static void OpenWhenClear(Action? fallback)
        {
            _pendingTicks = 120;
            _pendingFallback = fallback;
        }

        private static void TickPendingOpen()
        {
            if (_pendingTicks <= 0) return;
            if (Campaign.Current == null) { ClearPendingOpen(); return; }
            if (IsOpen) { ClearPendingOpen(); return; }
            if (CanOpenNow())
            {
                var fallback = _pendingFallback;
                bool opened = Open();
                ClearPendingOpen();
                if (!opened) fallback?.Invoke();
                return;
            }
            if (--_pendingTicks > 0) return;
            var fb = _pendingFallback;
            ClearPendingOpen();
            fb?.Invoke();
        }

        private static void ClearPendingOpen()
        {
            _pendingTicks = 0;
            _pendingFallback = null;
        }

        private static void TearDown()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.IsFocusLayer = false;
                    _layer.InputRestrictions.ResetInputRestrictions();
                    _host?.RemoveLayer(_layer);
                }
            }
            catch { /* the screen may already be gone */ }
            finally
            {
                _layer = null;
                _movie = null;
                _host = null;
                _vm = null;
            }
        }

        private static bool CanOpenNow()
        {
            try
            {
                if (_config == null || !_config.EnableNights || !_config.EnableNightWindow) return false;
                if (TalkScreen.TalkScreenManager.IsOpen) return false;       // one place at a time
                if (ChatWindow.ChatWindowManager.IsOpen) return false;      // one window at a time
                if (LetterWindow.LetterWindowManager.IsOpen) return false;
                if (Campaign.Current == null) return false;
                if (Mission.Current != null) return false;
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return false;
                if (mapState.MapConversationActive) return false;
                if (Hero.OneToOneConversationHero != null) return false;
                if (InformationManager.IsAnyInquiryActive()) return false;
                if (MapOverlays.IsEncyclopediaOpen) return false;   // typing in its search box is not a hotkey
                if (MapOverlays.IsTypingSomewhere) return false;
                return Hero.MainHero != null && Hero.MainHero.IsAlive;
            }
            catch { return false; }
        }

        // ------------------------------ the tick ------------------------------

        internal static void Tick()
        {
            try
            {
                if (_pendingTicks > 0) TickPendingOpen();
                if (IsOpen) TickOpen();
                else TickClosed();
            }
            catch { /* never let the window's plumbing touch the frame */ }
        }

        private static void TickClosed()
        {
            if (_config == null || !_config.EnableNights || !_config.EnableNightWindow) return;
            if (Campaign.Current == null) return;
            if (!Input.IsKeyReleased(_hotkey)) return;
            if (!CanOpenNow()) return;
            Open();
        }

        private static void TickOpen()
        {
            if (Campaign.Current == null || Mission.Current != null
                || !(Game.Current?.GameStateManager?.ActiveState is MapState))
            {
                Close();
                return;
            }

            var input = _layer?.Input;
            if (input != null && input.IsKeyReleased(InputKey.Escape))
            {
                if (_vm != null && _vm.IsInfoShown) _vm.IsInfoShown = false;
                else Close();
            }
        }
    }
}
