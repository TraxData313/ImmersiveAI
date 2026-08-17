using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Options;
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

        // ------------------------------ holding the world still ------------------------------
        //
        // While this screen is up the clock stops, and the game draws no more frames than it needs
        // to — because behind our picture there is nothing moving that anyone can see.
        //
        // WHAT WAS TRIED AND MUST NOT BE TRIED AGAIN LIGHTLY (2026.08.14, it crashed Anton's game on
        // every close). Vanilla's own Talk stops drawing the campaign map — MapScreen computes
        // `isSceneViewEnabled = !isConversationActive && (TopScreen == this)` — and that, not the
        // pause, is why it runs at twice our framerate. Doing the same by hand
        // (`MapScreen.Instance.SceneLayer.SceneView` → `SetEnable(false)`) works and is fast, but
        // turning it back on is NOT one call: MapScreen follows it with `MapScene.CheckResources`
        // and, if the scene ever had one, a full re-creation of the water-wake renderer. Skip those
        // and the map comes back to life holding freed resources — a hard native crash, at sea most
        // of all, which is where Anton plays. Worse, MapScreen keeps its own private cache of the
        // flag, so it cannot be told what we did and will not repair it for us.
        //
        // If this is ever revisited: mirror the whole enable sequence, and TEST IT ON WATER. Until
        // then the frame limiter below buys most of the same quiet for none of the risk.

        private static bool _worldHeld;
        private static CampaignTimeControlMode _timeBefore;

        private static void HoldTheWorld()
        {
            if (_worldHeld) return;
            _worldHeld = true;

            try
            {
                _timeBefore = Campaign.Current.TimeControlMode;
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            }
            catch { /* a world that will not hold still is not worth a crash */ }

            CapTheFrames();
        }

        private static void ReleaseTheWorld()
        {
            if (!_worldHeld) return;
            _worldHeld = false;

            ReleaseTheFrames();

            try
            {
                if (Campaign.Current != null) Campaign.Current.TimeControlMode = _timeBefore;
            }
            catch { /* the player's own pause key is one press away either way */ }
        }

        // ------------------------------ the frame limiter ------------------------------
        //
        // A still picture of one person does not need two hundred frames a second (Anton, 2026.08.14).
        // The game has its own limiter — the "Frame limiter" video option, in real frames per second,
        // 30..360 — so we borrow it while the screen is up and hand it back on the way out. Nothing
        // here is written to the player's own options file, so even a crash costs at most a restart.

        private static float _frameLimitBefore;
        private static bool _framesCapped;

        private static void CapTheFrames()
        {
            var wanted = _config?.TalkScreenFpsLimit ?? 0;
            if (wanted <= 0) return;    // 0 means "leave my own limit alone"

            try
            {
                _frameLimitBefore = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.FrameLimiter);
                NativeOptions.SetConfig(NativeOptions.NativeOptionsType.FrameLimiter, wanted);
                NativeOptions.ApplyConfigChanges(false);
                _framesCapped = true;
            }
            catch { /* a limiter that will not budge costs nothing but heat */ }
        }

        private static void ReleaseTheFrames()
        {
            if (!_framesCapped) return;
            _framesCapped = false;

            try
            {
                NativeOptions.SetConfig(NativeOptions.NativeOptionsType.FrameLimiter, _frameLimitBefore);
                NativeOptions.ApplyConfigChanges(false);
            }
            catch { /* the player's own video options are one restart away */ }
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
        internal static void Open(Hero? preselect = null, bool hearth = false)
        {
            if (IsOpen)
            {
                // Already up: the hotkey for the other side simply turns this one over, which is
                // what makes the hearth a MODE rather than a second screen the player must close.
                if (_vm != null && _vm.IsHearth != hearth) _vm.IsHearth = hearth;
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

                // Set BEFORE the first selection, so the contact list is already the right circle
                // and the soul chosen for the player is one of the women rather than whoever heads
                // the world's list.
                if (hearth) _vm.IsHearth = true;
                if (preselect != null) _vm.TrySelect(preselect);
                RequestScrollToBottom();
                HoldTheWorld();
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
            // FIRST, and outside every other try: the map must start being drawn again and the clock
            // must run, whatever else goes wrong on the way out. A screen that fails to close is a
            // bug; a world left frozen and black is a broken game.
            ReleaseTheWorld();

            // DELIBERATELY NOT stopping the voice here (2026.08.14, Anton's ask AND a crash).
            //
            // Two reasons, pulling the same way. He wants a line to finish even if he steps out of
            // the screen — "let her continue speaking" — which is also the kinder behaviour: her
            // words are already said, and cutting them off mid-sentence because a window closed is
            // the mod deciding she did not mean it. And the game CRASHED twice on exactly this path
            // (Escape, and the X while she was mid-sentence), while the speech host exited cleanly
            // both times — so the fault is on this side, in stopping a playing sound during
            // teardown, not in the engine. Leaving the sound alone removes the call that crashed and
            // gives him the behaviour he wanted, at once.
            //
            // Barge-in still happens where it belongs: sending the NEXT line cuts off the last one.

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
                    else if (_vm != null && _vm.IsVoiceShown) _vm.IsVoiceShown = false;
                    else if (_vm != null && _vm.IsMisgivingsShown) _vm.IsMisgivingsShown = false;
                    else if (_vm != null && _vm.IsInfoShown) _vm.IsInfoShown = false;
                    else Close();
                    return;
                }
                if ((input.IsKeyReleased(InputKey.Enter) || input.IsKeyReleased(InputKey.NumpadEnter))
                    && _vm?.IsInfoShown != true && _vm?.IsPromptEditShown != true
                    && _vm?.IsDevShown != true && _vm?.IsMisgivingsShown != true
                    && _vm?.IsVoiceShown != true
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

            // The stop button appears only while something is actually speaking — never a dead
            // control, and its appearing IS the hint that it is there. One bool per frame, and the
            // VM only raises a change when it truly changed.
            try
            {
                if (_vm != null) _vm.IsVoicePlaying = Voice.VoicePlayback.IsSpeaking;
            }
            catch { /* the button is a courtesy */ }

            // And the download's own line, while the voices page is open. Only then: it is a
            // twenty-minute errand and nobody watches it, but the one who does needs to see it move.
            try
            {
                if (_vm != null && _vm.IsVoiceShown) _vm.RefreshVoiceFetchLine();
            }
            catch { /* likewise */ }

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

        private static void ScrollToEnd(Widget root, string id)
        {
            var panels = root.GetAllChildrenOfTypeRecursive<ScrollablePanel>(p => p.Id == id);
            if (panels.Count == 0) return;
            var bar = panels[0].VerticalScrollbar;
            if (bar != null) bar.ValueFloat = bar.MaxValue;
        }

        private static void ScrollMessagesToBottom()
        {
            try
            {
                var root = _movie?.Movie?.RootWidget;
                if (root == null) return;
                // BOTH ROLLS, and for the same reason (Anton, 2026.08.16): the newest is what you
                // came to read. The thread opens on the last thing said; the hearth opens on the
                // last night, and earlier ones are scrolled UP to, the way a memory is walked back.
                ScrollToEnd(root, "MessagesScroller");
                ScrollToEnd(root, "HearthScroller");
            }
            catch { /* a missed scroll is a shrug, not a failure */ }
        }
    }
}
