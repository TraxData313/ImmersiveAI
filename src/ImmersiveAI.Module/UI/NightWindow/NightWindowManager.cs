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
                return KeyMayAct();
            }
            catch { return false; }
        }

        /// <summary>Whether the hearth key means what it says right now, or is somebody's keystroke:
        /// the part of <see cref="CanOpenNow"/> that is about the MOMENT rather than about which of
        /// our own windows is up. Split out 2026.08.16 so the talk-screen road cannot skip it — the
        /// old shape returned before ever reaching these, which is how an "h" typed into the writing
        /// box came to turn the whole screen over.</summary>
        private static bool KeyMayAct()
        {
            try
            {
                if (Campaign.Current == null) return false;
                if (Mission.Current != null) return false;
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return false;
                if (mapState.MapConversationActive) return false;
                if (Hero.OneToOneConversationHero != null) return false;
                if (InformationManager.IsAnyInquiryActive()) return false;
                if (MapOverlays.IsEncyclopediaOpen) return false;   // typing in its search box is not a hotkey
                if (MapOverlays.IsTypingSomewhere) return false;    // any focused text field holds the keys
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

            // THE HEARTH IS A MODE OF THE TALK SCREEN NOW (2026.08.16). The key is unchanged and so
            // is everything the player knows; what it raises is the one screen, turned to the
            // hearth — the women listed left, the chosen one ALIVE in the middle in her own place.
            // The old window is still whole behind UseClassicChatWindow and behind the screen's own
            // session fallback, so a game patch that breaks the tableau costs the hearth its stage
            // and not its existence.
            if (UI.TalkUI.UsesTalkScreen)
            {
                // ONCE THE SCREEN IS UP, THIS KEY IS A LETTER (Anton's playtest, 2026.08.16: open the
                // screen after a load, start typing, and the chat vanishes — an "h" in the middle of a
                // word turned the screen over to the hearth). The branch above was written to jump
                // CanOpenNow because that refuses while the talk screen is open — and it jumped the
                // typing, encyclopedia and inquiry guards with it, leaving this the only key in the
                // whole mod that acts while the player is writing.
                //
                // So: nothing at all while the screen stands. Turning it over is what the "Between us"
                // and "Talk" buttons in its own bar are for, they are always in reach, and a keyboard
                // shortcut that shares a key with a letter inside a screen built for writing can only
                // ever fire mid-word. Every other guard still applies to RAISING it.
                if (TalkScreen.TalkScreenManager.IsOpen) return;
                if (!KeyMayAct()) return;
                TalkScreen.TalkScreenManager.Open(hearth: true);
                return;
            }

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
