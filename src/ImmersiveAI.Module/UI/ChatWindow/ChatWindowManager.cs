using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace ImmersiveAI.UI.ChatWindow
{
    /// <summary>
    /// Owns the chat window's life on screen: a Gauntlet layer over the map (and over the town,
    /// castle, and village menus — they live on the map screen too), opened by hotkey, by the
    /// settlement menu option, or by clicking an NPC's map notice. Ticks from the application tick:
    /// polls the open-hotkey at calm map moments, Enter-to-send and Escape-to-close while open, and
    /// folds the window away if the world moves somewhere it cannot follow (a battle, a scene).
    /// Also keeps the session's unread marks — whose words are waiting — which are deliberately
    /// not persisted: the words themselves live in memory files; only the little dot is transient.
    /// Everything is best-effort: a UI failure closes the window, never the game.
    /// </summary>
    internal static class ChatWindowManager
    {
        private static ModConfig? _config;
        private static InputKey _hotkey = InputKey.O;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ScreenBase? _host;
        private static ChatWindowVM? _vm;

        // Whose words await reading (stringId → true). Session-scoped by design.
        private static readonly HashSet<string> _unread = new HashSet<string>(StringComparer.Ordinal);

        // Frames left before the message list is nudged to its newest line (layout must run first).
        private static int _scrollCountdown;

        internal static bool IsOpen => _layer != null;

        internal static void Configure(ModConfig config)
        {
            _config = config;
            _hotkey = ParseKey(config.ChatWindowHotkey);
        }

        private static InputKey ParseKey(string name)
        {
            return Enum.TryParse<InputKey>((name ?? string.Empty).Trim(), ignoreCase: true, out var key)
                ? key
                : InputKey.O;
        }

        // ------------------------------ unread marks ------------------------------

        internal static bool HasUnread(string heroStringId) => _unread.Contains(heroStringId);

        internal static void MarkUnread(string heroStringId)
        {
            if (!string.IsNullOrEmpty(heroStringId)) _unread.Add(heroStringId);
        }

        internal static void ClearUnread(string heroStringId) => _unread.Remove(heroStringId);

        /// <summary>Whether the player is looking at this very thread right now (so a "has answered"
        /// toast would only state the obvious).</summary>
        internal static bool IsViewing(Hero npc)
        {
            try
            {
                if (!IsOpen || _vm == null || npc == null) return false;
                foreach (var c in _vm.Contacts)
                    if (c.IsSelected && c.Hero == npc) return true;
                return false;
            }
            catch { return false; }
        }

        /// <summary>A thread changed underneath the window (a reply landed, or an NPC spoke first).
        /// Refreshes the open window; otherwise the unread mark waits for the next opening.
        /// Must run on the game thread.</summary>
        internal static void OnThreadChanged(Hero npc, bool markUnread)
        {
            if (npc == null) return;
            try
            {
                if (markUnread && !IsViewing(npc)) MarkUnread(npc.StringId);
                _vm?.OnThreadChanged(npc.StringId);
            }
            catch { /* the window is a view; the words are already safe in memory */ }
        }

        /// <summary>A quick message could not be sent after all — give the words back to the player.</summary>
        internal static void OnSendFailed(Hero npc, string text)
        {
            try { if (npc != null) _vm?.OnSendFailed(npc.StringId, text); }
            catch { /* best-effort */ }
        }

        // ------------------------------ open / close ------------------------------

        /// <summary>Opens the window (optionally with someone's thread already on stage). Safe to call
        /// at any moment: does nothing when the map is not the place for it.</summary>
        internal static void Open(Hero? preselect = null)
        {
            if (IsOpen)
            {
                if (preselect != null) _vm?.TrySelect(preselect);
                return;
            }
            if (!CanOpenNow()) return;

            try
            {
                _vm = new ChatWindowVM(_config!);
                _layer = new GauntletLayer("ImmersiveChatWindow", 4500);
                _movie = _layer.LoadMovie("ImmersiveChatWindow", _vm);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;

                _host = ScreenManager.TopScreen;
                _host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);

                if (preselect != null) _vm.TrySelect(preselect);
                RequestScrollToBottom();
            }
            catch (Exception ex)
            {
                TearDown();
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: the chat window could not open — " + ex.Message));
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
                if (_config == null || !_config.EnableChatWindow) return false;
                if (LetterWindow.LetterWindowManager.IsOpen) return false;   // one window at a time
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

        /// <summary>Called every application tick (from SubModule). Cheap when nothing is happening:
        /// one key poll on the map, nothing anywhere else.</summary>
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
            if (_config == null || !_config.EnableChatWindow) return;
            if (Campaign.Current == null) return;
            if (!Input.IsKeyReleased(_hotkey)) return;
            if (!CanOpenNow()) return;
            Open();
        }

        private static void TickOpen()
        {
            // The world moved somewhere the window cannot follow — fold it away quietly.
            if (Campaign.Current == null || Mission.Current != null
                || !(Game.Current?.GameStateManager?.ActiveState is MapState))
            {
                Close();
                return;
            }

            var input = _layer?.Input;
            if (input != null)
            {
                if (input.IsKeyReleased(InputKey.Escape)) { Close(); return; }
                if (input.IsKeyReleased(InputKey.Enter) || input.IsKeyReleased(InputKey.NumpadEnter))
                    _vm?.ExecuteSend();
            }

            if (_scrollCountdown > 0 && --_scrollCountdown == 0)
                ScrollMessagesToBottom();
        }

        // ------------------------------ scroll to the newest word ------------------------------

        /// <summary>Asks for the message list to be scrolled to its end once layout has caught up
        /// (a couple of frames — sizes are not known the moment the list is rebuilt).</summary>
        internal static void RequestScrollToBottom() => _scrollCountdown = IsOpen || _scrollCountdown > 0 ? 3 : _scrollCountdown;

        private static void ScrollMessagesToBottom()
        {
            try
            {
                var root = _movie?.Movie?.RootWidget;
                if (root == null) return;
                var panels = root.GetAllChildrenOfTypeRecursive<ScrollablePanel>(p => p.Id == "MessagesScroller");
                if (panels.Count == 0) return;
                var bar = panels[0].VerticalScrollbar;
                if (bar != null) bar.ValueFloat = bar.MaxValue;
            }
            catch { /* a missed scroll is a shrug, not a failure */ }
        }
    }
}
