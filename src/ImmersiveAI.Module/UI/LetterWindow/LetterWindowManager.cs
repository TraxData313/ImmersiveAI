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

namespace ImmersiveAI.UI.LetterWindow
{
    /// <summary>
    /// Owns the letter window's life on screen — the chat window's twin (same layer plumbing, same
    /// calm-map rules), opened by its own hotkey or by "Write back" on an arrived letter. Escape
    /// closes; Enter deliberately does NOT send — a letter deserves a deliberate seal, not a slip
    /// of the finger. Everything is best-effort: a UI failure closes the window, never the game.
    /// </summary>
    internal static class LetterWindowManager
    {
        private static ModConfig? _config;
        private static InputKey _hotkey = InputKey.U;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ScreenBase? _host;
        private static LetterWindowVM? _vm;

        private static int _scrollCountdown;

        // Unsent letter drafts, per correspondent folder — kept when the window closes so stepping
        // away and coming back does not lose a half-written letter (Anton's ask, 2026.07.11).
        private static readonly System.Collections.Generic.Dictionary<string, string> _drafts =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        internal static bool IsOpen => _layer != null;

        // ------------------------------ unsent drafts ------------------------------

        internal static string GetDraft(string folder) =>
            !string.IsNullOrEmpty(folder) && _drafts.TryGetValue(folder, out var d) ? d : string.Empty;

        internal static void SetDraft(string folder, string text)
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (string.IsNullOrWhiteSpace(text)) _drafts.Remove(folder);
            else _drafts[folder] = text;
        }

        internal static void Configure(ModConfig config)
        {
            _config = config;
            _hotkey = ParseKey(config.LetterWindowHotkey);
        }

        private static InputKey ParseKey(string name)
        {
            return Enum.TryParse<InputKey>((name ?? string.Empty).Trim(), ignoreCase: true, out var key)
                ? key
                : InputKey.U;
        }

        // ------------------------------ open / close ------------------------------

        /// <summary>Opens the window (optionally with someone's correspondence on stage). True when
        /// it is open afterwards — "Write back" falls back to the old composer popups on false.</summary>
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
                _vm = new LetterWindowVM(_config!);
                _layer = new GauntletLayer("ImmersiveLetterWindow", 4500);
                _movie = _layer.LoadMovie("ImmersiveLetterWindow", _vm);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;

                _host = ScreenManager.TopScreen;
                _host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);

                if (preselect != null) _vm.TrySelect(preselect);
                RequestScrollToBottom();
                return true;
            }
            catch (Exception ex)
            {
                TearDown();
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: the letter window could not open — " + ex.Message));
                return false;
            }
        }

        internal static void Close()
        {
            if (!IsOpen) return;
            TearDown();
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
            catch { /* the screen may already be gone; nothing to restore */ }
            finally
            {
                _layer = null;
                _movie = null;
                _host = null;
                _vm = null;
                _scrollCountdown = 0;
            }
        }

        private static bool CanOpenNow()
        {
            try
            {
                if (_config == null || !_config.EnableLetters || !_config.EnableLetterWindow) return false;
                if (ChatWindow.ChatWindowManager.IsOpen) return false;   // one window at a time
                if (Campaign.Current == null) return false;
                if (Mission.Current != null) return false;
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return false;
                if (mapState.MapConversationActive) return false;
                if (Hero.OneToOneConversationHero != null) return false;
                if (InformationManager.IsAnyInquiryActive()) return false;
                return Hero.MainHero != null && Hero.MainHero.IsAlive;
            }
            catch { return false; }
        }

        // ------------------------------ the tick ------------------------------

        /// <summary>Called every application tick (from SubModule). One key poll on the map when
        /// closed; Escape and the scroll nudge when open.</summary>
        internal static void Tick()
        {
            try
            {
                if (IsOpen) TickOpen();
                else TickClosed();
            }
            catch { /* never let the window's plumbing touch the frame */ }
        }

        private static void TickClosed()
        {
            if (_config == null || !_config.EnableLetters || !_config.EnableLetterWindow) return;
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
                // Escape folds the info overlay first; only a second press closes the window.
                if (_vm != null && _vm.IsInfoShown) _vm.IsInfoShown = false;
                else Close();
                return;
            }

            if (_scrollCountdown > 0 && --_scrollCountdown == 0)
                ScrollEntriesToBottom();
        }

        // ------------------------------ scroll to the newest letter ------------------------------

        internal static void RequestScrollToBottom() => _scrollCountdown = IsOpen || _scrollCountdown > 0 ? 3 : _scrollCountdown;

        private static void ScrollEntriesToBottom()
        {
            try
            {
                var root = _movie?.Movie?.RootWidget;
                if (root == null) return;
                var panels = root.GetAllChildrenOfTypeRecursive<ScrollablePanel>(p => p.Id == "LettersScroller");
                if (panels.Count == 0) return;
                var bar = panels[0].VerticalScrollbar;
                if (bar != null) bar.ValueFloat = bar.MaxValue;
            }
            catch { /* a missed scroll is a shrug, not a failure */ }
        }
    }
}
