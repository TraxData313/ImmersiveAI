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

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// Owns the talk screen's life on stage (2026.08.14): one full-screen Gauntlet layer over the
    /// map, opened by either old hotkey (both now lead here), by a settlement menu, by a knock, or
    /// by a letter's arrival. It replaces the chat window AND the letter window; the two old windows
    /// remain reachable behind <see cref="ModConfig.UseClassicChatWindow"/> as insurance until this
    /// has lived through a few game patches.
    ///
    /// Everything is best-effort: a failure to raise the screen — a missing prefab, a tableau that
    /// will not build after a game update — closes it and falls back to the old windows for the rest
    /// of the session, never touching the game.
    /// </summary>
    internal static class TalkScreenManager
    {
        private static ModConfig? _config;
        private static InputKey _hotkey = InputKey.O;
        private static InputKey _letterHotkey = InputKey.Y;

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ScreenBase? _host;
        private static TalkScreenVM? _vm;

        /// <summary>Set when raising the screen threw: the session falls back to the old windows
        /// rather than trying (and failing) at every keypress.</summary>
        private static bool _disabledForSession;

        // Whose words await reading (stringId → true). Session-scoped by design: the words themselves
        // live in the memory files; only the little dot is transient.
        private static readonly HashSet<string> _unread = new HashSet<string>(StringComparer.Ordinal);

        // Unsent drafts, keyed by MEMORY FOLDER rather than by hero id — the one identity that
        // survives both a rename and a death, and the same key a letter is filed under. Kept when the
        // screen closes so stepping away and coming back loses nothing.
        private static readonly Dictionary<string, string> _drafts =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Frames left before the message list is nudged to its newest line (layout must run first).
        private static int _scrollCountdown;

        // The last campaign day the road button was recomputed on — see the tick.
        private static int _lastSeenDay = -1;

        internal static bool IsOpen => _layer != null;

        /// <summary>Whether the talk screen is the one that should open at all — off when the player
        /// has asked for the old windows, or when raising it has already failed this session.</summary>
        internal static bool IsPreferred =>
            _config != null && _config.EnableChatWindow && !_config.UseClassicChatWindow && !_disabledForSession;

        internal static void Configure(ModConfig config)
        {
            _config = config;
            _hotkey = ParseKey(config.ChatWindowHotkey, InputKey.O);
            _letterHotkey = ParseKey(config.LetterWindowHotkey, InputKey.Y);
        }

        private static InputKey ParseKey(string name, InputKey fallback)
        {
            return Enum.TryParse<InputKey>((name ?? string.Empty).Trim(), ignoreCase: true, out var key)
                ? key
                : fallback;
        }

        // ------------------------------ unread marks ------------------------------

        internal static bool HasUnread(string heroStringId) => _unread.Contains(heroStringId);

        internal static void MarkUnread(string heroStringId)
        {
            if (!string.IsNullOrEmpty(heroStringId)) _unread.Add(heroStringId);
        }

        internal static void ClearUnread(string heroStringId) => _unread.Remove(heroStringId);

        // ------------------------------ unsent drafts ------------------------------

        internal static string GetDraft(string folder) =>
            !string.IsNullOrEmpty(folder) && _drafts.TryGetValue(folder, out var d) ? d : string.Empty;

        internal static void SetDraft(string folder, string text)
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (string.IsNullOrWhiteSpace(text)) _drafts.Remove(folder);
            else _drafts[folder] = text;
        }

        internal static void ClearDraft(string folder) => _drafts.Remove(folder);

        private static string FolderFor(Hero npc)
        {
            try { return npc == null ? string.Empty : NpcPaths.NpcFolder(npc); }
            catch { return string.Empty; }
        }

        /// <summary>Whether the player is looking at this very thread right now (so a "has answered"
        /// toast would only state the obvious).</summary>
        internal static bool IsViewing(Hero npc)
        {
            try { return IsOpen && _vm != null && npc != null && _vm.SelectedHero == npc; }
            catch { return false; }
        }

        /// <summary>A thread changed underneath the screen (a reply landed, a letter came, or an NPC
        /// spoke first). Refreshes what is on stage; otherwise the unread mark waits for the next
        /// opening. Must run on the game thread.</summary>
        internal static void OnThreadChanged(Hero npc, bool markUnread)
        {
            if (npc == null) return;
            try
            {
                if (markUnread && !IsViewing(npc)) MarkUnread(npc.StringId);
                _vm?.OnThreadChanged(npc.StringId);
            }
            catch { /* the screen is a view; the words are already safe in memory */ }
        }

        /// <summary>A quick message could not be sent after all — give the words back to the player.</summary>
        internal static void OnSendFailed(Hero npc, string text)
        {
            try { if (npc != null) _vm?.OnSendFailed(npc.StringId, text); }
            catch { /* best-effort */ }
        }

        /// <summary>The player's own thought came back ("Let me think…"). The words go into the draft
        /// store FIRST, so a thought asked for and then walked away from still waits in the writing
        /// box on the way back; the open screen then picks them up. Must run on the game thread.</summary>
        internal static void OnThoughtReady(Hero npc, string words)
        {
            if (npc == null) return;
            try
            {
                SetDraft(FolderFor(npc), words);
                _vm?.OnThoughtReady(npc.StringId, words);
            }
            catch { /* the words are in the draft store either way */ }
        }

        internal static void OnThoughtFailed(Hero npc)
        {
            try { if (npc != null) _vm?.OnThoughtFailed(npc.StringId); }
            catch { /* best-effort */ }
        }

        // ------------------------------ the one on stage ------------------------------

        /// <summary>Puts the chosen one before the player — their own face, in their own place. A
        /// failure here costs the portrait, never the conversation.</summary>
        internal static void ShowCharacter(Hero? hero)
        {
            try { ConversationTableauController.Show(hero); }
            catch { /* the words are the point; the face is the grace */ }
        }

        /// <summary>A small shift of weight as your words reach them — the ONE moment anybody moves
        /// (see the note in ConversationSceneBuilder: this was once three, and three was too many).
        /// Must run on the game thread.</summary>
        internal static void ShiftStance(Hero? hero)
        {
            try { ConversationTableauController.ShiftStance(hero); }
            catch { /* best-effort, always */ }
        }

        // ------------------------------ open / close ------------------------------

        /// <summary>Opens the screen (optionally with someone already on stage). Safe to call at any
        /// moment: does nothing when the map is not the place for it.</summary>
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
                _vm = new TalkScreenVM(_config!);
                _layer = new GauntletLayer("ImmersiveTalkScreen", 4500);
                _movie = _layer.LoadMovie("ImmersiveTalkScreen", _vm);
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
                TearDown(talkHappened: false);   // nothing rose, so nobody was talked to
                // One failure is enough: fall back to the old windows for the rest of the session so
                // a game patch that breaks the new screen cannot take the whole feature with it.
                _disabledForSession = true;
                ModLog.Error("raising the talk screen", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    "Immersive AI: the talk screen could not open — falling back to the older windows. " + ex.Message));
            }
        }

        // A soul waiting for the map to be clear enough to show them (an inquiry closing, a
        // conversation tearing down). Counted down in the TICK, one try per frame.
        private static Hero? _pendingOpen;
        private static Action<Hero>? _pendingFallback;
        private static int _pendingTicks;

        /// <summary>Opens the screen the moment the map is clear of popups — used when something
        /// closing (an inquiry, a notice, a conversation) would otherwise race the opening.
        ///
        /// The waiting is parked and counted down ONE TRY PER TICK. It must not be done by
        /// re-queueing onto the dispatcher: that queue is drained in a `while` loop that also picks
        /// up whatever the drain itself adds, so a self-requeueing retry spends its whole budget
        /// inside a single frame and is simply a next-tick attempt wearing a disguise. An inquiry is
        /// still reliably "active" one tick after its button is clicked — the very bug the letter
        /// window hit and fixed the same way (2026.07.15).</summary>
        internal static void OpenWhenClear(Hero npc, Action<Hero>? fallback = null)
        {
            if (npc == null) return;
            if (CanOpenNow()) { Open(npc); return; }
            _pendingOpen = npc;
            _pendingFallback = fallback;
            _pendingTicks = 120;   // a couple of seconds of patience, then the caller's own way out
        }

        private static void ClearPending()
        {
            _pendingOpen = null;
            _pendingFallback = null;
            _pendingTicks = 0;
        }

        private static void TickPendingOpen()
        {
            var waiting = _pendingOpen;
            if (waiting == null) return;

            if (IsOpen)
            {
                ClearPending();
                _vm?.TrySelect(waiting);
                return;
            }
            if (CanOpenNow())
            {
                ClearPending();
                Open(waiting);
                return;
            }
            if (--_pendingTicks > 0) return;

            var fallback = _pendingFallback;
            ClearPending();
            try { fallback?.Invoke(waiting); }
            catch { /* the caller's fallback is a courtesy, not a contract */ }
        }

        internal static void Close()
        {
            if (!IsOpen) return;
            TearDown();
        }

        private static void TearDown(bool talkHappened = true)
        {
            // Closing the screen silences whoever was speaking, for the same reason walking out of a
            // conversation does: the words are gone from the page, so the voice should go with them.
            Voice.VoiceService.Stop();

            // Walking away from the screen ends that talk, the way closing a conversation does — and
            // only then does what was said in it count as said (the line, 2026.08.09). A screen that
            // never managed to RISE is not a talk, though: stamping it would move "since we were
            // last alone" for someone the player never even saw.
            if (talkHappened)
            {
                try
                {
                    var talkedWith = _vm?.SelectedHero;
                    if (talkedWith != null) ImmersiveChatBehavior.NoteTalkEndedWith(talkedWith);
                }
                catch { /* the grace fallback still moves the line by itself */ }
            }

            // The LAYER goes first, then the stage. The tableau widget reads the scene while it
            // finalizes, and pulling the scene out from under it is a known crash — so the widget is
            // let go of first, and only then is the stage struck (see the controller's Clear).
            try
            {
                if (_layer != null)
                {
                    _layer.IsFocusLayer = false;
                    _layer.InputRestrictions.ResetInputRestrictions();
                    // Released by name, not left to the layer's removal: the tableau widget must be
                    // finalized so it hands the SHARED conversation scene back. Left holding, its
                    // figures turn up standing in the game's own next Talk.
                    if (_movie != null) _layer.ReleaseMovie(_movie);
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

            // Never leave the shared conversation scene held: the game's own Talk needs it back.
            try { ConversationTableauController.Clear(); }
            catch { /* best-effort */ }
        }

        internal static bool CanOpenNow()
        {
            try
            {
                if (_config == null || !IsPreferred) return false;
                if (ChatWindow.ChatWindowManager.IsOpen) return false;      // the classics, if in use
                if (LetterWindow.LetterWindowManager.IsOpen) return false;
                if (NightWindow.NightWindowManager.IsOpen) return false;    // the hearth is its own place
                if (Campaign.Current == null) return false;
                if (Mission.Current != null) return false;
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return false;
                if (mapState.MapConversationActive) return false;   // the game's own talk holds the scene
                if (Hero.OneToOneConversationHero != null) return false;
                if (InformationManager.IsAnyInquiryActive()) return false;
                if (MapOverlays.IsEncyclopediaOpen) return false;   // typing in its search box is not a hotkey
                if (MapOverlays.IsTypingSomewhere) return false;    // any focused text field holds the keys
                return Hero.MainHero != null && Hero.MainHero.IsAlive;
            }
            catch { return false; }
        }

        // ------------------------------ the tick ------------------------------

        /// <summary>Called every application tick (from SubModule). Cheap when nothing is happening:
        /// two key polls on the map, nothing anywhere else.</summary>
        internal static void Tick()
        {
            try
            {
                TickPendingOpen();
                if (IsOpen) TickOpen();
                else TickClosed();
            }
            catch { /* never let the screen's plumbing touch the frame */ }
        }

        private static void TickClosed()
        {
            if (!IsPreferred) return;
            if (Campaign.Current == null) return;
            // Both old hotkeys lead here now — one place, two doors, so nobody's muscle memory breaks.
            if (!Input.IsKeyReleased(_hotkey) && !Input.IsKeyReleased(_letterHotkey)) return;
            if (!CanOpenNow()) return;
            Open();
        }

        private static void TickOpen()
        {
            // The world moved somewhere the screen cannot follow — fold it away quietly. The game's
            // own map conversation counts: it wants the one shared tableau scene back, and two
            // tableaus in it is not an error the engine reports, only one it draws wrongly.
            var state = Game.Current?.GameStateManager?.ActiveState as MapState;
            if (Campaign.Current == null || Mission.Current != null || state == null
                || state.MapConversationActive)
            {
                Close();
                return;
            }

            var input = _layer?.Input;
            if (input != null)
            {
                if (input.IsKeyReleased(InputKey.Escape))
                {
                    // Escape folds the prompt editor first (discarding), then the presets' two pages,
                    // then the dev panel, the road page, and the info overlay — the same order every
                    // overlay's own "Back" button walks; only a bare press closes the screen.
                    if (_vm != null && _vm.IsPromptEditShown) _vm.ExecutePromptCancel();
                    else if (_vm != null && _vm.IsPresetEditShown) _vm.IsPresetEditShown = false;
                    else if (_vm != null && _vm.IsPresetsShown) _vm.IsPresetsShown = false;
                    else if (_vm != null && _vm.IsDevShown) _vm.IsDevShown = false;
                    else if (_vm != null && _vm.IsMisgivingsShown) _vm.IsMisgivingsShown = false;
                    else if (_vm != null && _vm.IsInfoShown) _vm.IsInfoShown = false;
                    else Close();
                    return;
                }
                if ((input.IsKeyReleased(InputKey.Enter) || input.IsKeyReleased(InputKey.NumpadEnter))
                    && _vm?.IsInfoShown != true && _vm?.IsPromptEditShown != true
                    && _vm?.IsDevShown != true && _vm?.IsMisgivingsShown != true
                    && _vm?.IsPresetsShown != true && _vm?.IsPresetEditShown != true)
                {
                    // Two fixed keys, and nothing clever (Anton, 2026.08.10): ENTER sends, SHIFT+ENTER
                    // thinks. The one exception is a LETTER, which Enter never seals — a letter rides
                    // for days and deserves a deliberate press of its own button.
                    if (ShiftHeld()) _vm?.ExecuteThink();
                    else if (_vm?.IsAway != true) _vm?.ExecuteSend();
                }
            }

            if (_scrollCountdown > 0 && --_scrollCountdown == 0)
                ScrollMessagesToBottom();

            // The map keeps running under this screen, so a road that counts DAYS ("Preparations 1/3")
            // would sit stale until the player clicked away and back — and a button that does not move
            // is indistinguishable from a broken one. One cheap check per tick, one refresh per day.
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                if (today != _lastSeenDay)
                {
                    _lastSeenDay = today;
                    _vm?.RefreshRoadPage();
                }
            }
            catch { /* the label is a nicety; never let it break the screen */ }
        }

        /// <summary>Either shift, read from the engine's own keyboard state rather than the layer: a
        /// modifier is not a layer's business, and the layer's input reports only the keys it was
        /// given.</summary>
        internal static bool ShiftHeld()
        {
            try { return Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift); }
            catch { return false; }
        }

        // ------------------------------ scroll to the newest word ------------------------------

        /// <summary>Asks for the message list to be scrolled to its end once layout has caught up (a
        /// couple of frames — sizes are not known the moment the list is rebuilt).</summary>
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
