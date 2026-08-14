using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.UI.ChatWindow;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// THE TALK SCREEN (2026.08.14) — the chat window and the letter window grown into one place,
    /// and lifted out of a little box in the middle of the map into something that looks like the
    /// game's own Talk: everyone you know down the left, the chosen one standing before you in the
    /// middle, and the whole of what has passed between you down the right.
    ///
    /// The merge's one idea: a friend is a friend whether they are in the room or a kingdom away.
    /// The list makes no distinction; only the button does — Send while they can hear you, Seal
    /// while only a courier can reach them — and letters take their place in the same thread as
    /// spoken words, wearing a ✉ so you can see which words travelled.
    ///
    /// Everything here is a VIEW. Closing the screen loses nothing: the words are recorded turns in
    /// their memory, the letters are in the bag and in letters.txt, and unsent drafts wait in the
    /// manager's store.
    /// </summary>
    public class TalkScreenVM : ViewModel
    {
        // Header tints: the player's words in warm parchment-gold, the NPC's in the same soft
        // sea-glass as the activity notices — read at a glance without bubbles.
        private static readonly Color PlayerHeaderColor = new Color(0.85f, 0.75f, 0.55f, 1f);
        private static readonly Color NpcHeaderColor = new Color(0.74f, 0.90f, 0.86f, 1f);
        // The "since you were last alone" footnote: the nights' own dusk-violet, so it reads as a
        // margin note about the two of you rather than as anyone's spoken words.
        private static readonly Color LineHeaderColor = new Color(0.72f, 0.70f, 0.92f, 1f);
        // The wedding accounts wear the courtship road's own rose; a child's day a warmer, older gold.
        private static readonly Color WeddingHeaderColor = new Color(0.93f, 0.62f, 0.72f, 1f);
        private static readonly Color CradleHeaderColor = new Color(0.96f, 0.84f, 0.48f, 1f);

        // The scrollback above the conversation — what the chosen one is about to be given. Cooler
        // and dimmer than anything anyone actually said, so the eye knows at once it has scrolled
        // out of the talk and into the mind behind it.
        private static readonly Color PromptFrameColor = new Color(0.58f, 0.68f, 0.80f, 1f);
        private static readonly Color PromptSheetColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        private static readonly Color PromptToolColor = new Color(0.56f, 0.72f, 0.70f, 1f);

        private readonly ModConfig _config;

        // The line sent but not yet answered, per soul — shown in the thread while the reply is on
        // its way, and restored into the writing box should the sending fail.
        private readonly Dictionary<string, string> _pendingLines = new Dictionary<string, string>(StringComparer.Ordinal);

        // Every soul the screen knows of, unfiltered — Contacts is the searched VIEW over this.
        private readonly List<TalkContactVM> _allContacts = new List<TalkContactVM>();

        private MBBindingList<TalkContactVM> _contacts = new MBBindingList<TalkContactVM>();
        private MBBindingList<ChatMessageVM> _messages = new MBBindingList<ChatMessageVM>();
        private TalkContactVM? _selected;

        /// <summary>Whoever's thread is on stage — the manager stamps their talk as ended when the
        /// screen closes, so the "since we were last alone" line moves only once you have walked
        /// away (see the behavior's Nights partial).</summary>
        internal Hero? SelectedHero => _selected?.Hero;

        /// <summary>The chosen one's folder — the identity drafts are kept under, because it is the
        /// one thing that survives both a rename and a death.</summary>
        internal string SelectedFolder => _selected?.Folder ?? string.Empty;

        private string _inputText = string.Empty;
        private string _searchText = string.Empty;
        private string _selectedName = string.Empty;
        private string _relationText = string.Empty;
        private string _bondStatsText = string.Empty;
        private string _memoryLoadText = string.Empty;
        private string _reachText = string.Empty;
        private Color _relationColor = Colors.White;
        private ImmersiveChatBehavior.TalkReach _reach = ImmersiveChatBehavior.TalkReach.Closed;

        private bool _isInfoShown;
        private bool _isPromptEditShown;
        private string _promptEditTitle = string.Empty;
        private string _promptEditText = string.Empty;
        private bool _isWaiting;
        private bool _isMisgivingsShown;
        private bool _pageIsWedding;
        private ImmersiveChatBehavior.RoadPage? _roadPage;
        private string _misgivingsButtonText = string.Empty;
        private string _misgivingsTitleText = string.Empty;
        private string _misgivingsBodyText = string.Empty;
        private bool _isDevShown;

        // "Let me think…" — the player's own next line (see the behavior's Thoughts partial).
        // _isWish marks that what stands in the writing box is an INTENDED MEANING rather than words:
        // it is only ever a WARNING (the mirror turns violet and says so), never a change of what the
        // keys do — Enter sends and Shift+Enter thinks, always, whatever stands in the box.
        private bool _isWish;
        private string _wishText = string.Empty;
        private bool _isThinking;
        private bool _isPresetsShown;
        private bool _isPresetEditShown;
        private string _presetEditName = string.Empty;
        private string _presetEditText = string.Empty;
        private List<Core.Prompts.ConversationPreset> _presetList = new List<Core.Prompts.ConversationPreset>();
        private MBBindingList<ConversationPresetVM> _presets = new MBBindingList<ConversationPresetVM>();
        private MBBindingList<ConversationPresetVM> _presetRows = new MBBindingList<ConversationPresetVM>();

        public TalkScreenVM(ModConfig config)
        {
            _config = config;
            RefreshContacts();

            // Somebody is always on stage from the first frame. Partly grace — an empty middle is a
            // poor way to open — and partly a rail: the face's widget dislikes being built with
            // nothing to draw and never ticked, and choosing here happens BEFORE the movie loads.
            if (_selected == null && _allContacts.Count > 0) SelectContact(_allContacts[0]);
        }

        // ------------------------------ the circle on the left ------------------------------

        /// <summary>Rebuilds the list — everyone near, everyone known and away, everyone whose
        /// letters remain — keeping the current selection when that soul is still listed.</summary>
        public void RefreshContacts()
        {
            var keepFolder = _selected?.Folder;
            _allContacts.Clear();

            foreach (var info in ImmersiveChatBehavior.ContactsForTalk()
                         .OrderByDescending(i => i.IsHere)
                         .ThenByDescending(i => !i.IsGone)
                         .ThenByDescending(i => i.HasHistory || i.HasLetters)
                         .ThenByDescending(i => i.LastSpokenGameDay)
                         .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            {
                var vm = new TalkContactVM(info, OnContactSelected);
                if (info.Hero != null) vm.HasUnread = TalkScreenManager.HasUnread(info.Hero.StringId);
                _allContacts.Add(vm);
            }

            ApplyContactFilter();

            var again = keepFolder == null
                ? null
                : _allContacts.FirstOrDefault(c => string.Equals(c.Folder, keepFolder, StringComparison.OrdinalIgnoreCase));
            if (again != null) SelectContact(again);
            else if (_selected != null)
            {
                // Whoever was on stage has left the circle entirely. Clear the WHOLE stage, not just
                // the name: leaving the thread and the face standing under a blank header showed one
                // soul's story beneath another's absence.
                _selected = null;
                RefreshSelectionState();
                RefreshThread();
                TalkScreenManager.ShowCharacter(null);
                OnPropertyChanged("TableauData");
                OnPropertyChanged("HasFace");
                OnPropertyChanged("EmptyHint");
            }
        }

        // The search line above the list: a plain name-or-detail contains, so "scout", "Sargot", or
        // half a name all find their soul. The selection is a thing apart from the view — a
        // filtered-out selected thread stays on stage, only its row steps out of the list.
        private void ApplyContactFilter()
        {
            var q = (_searchText ?? string.Empty).Trim();
            var list = new MBBindingList<TalkContactVM>();
            foreach (var c in _allContacts)
                if (q.Length == 0 || MatchesSearch(c.Name, q) || MatchesSearch(c.Detail, q))
                    list.Add(c);
            Contacts = list;
        }

        private static bool MatchesSearch(string? text, string q) =>
            text != null && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnContactSelected(TalkContactVM contact) => SelectContact(contact);

        /// <summary>Puts one soul on stage: their face in the middle, their story on the right, and
        /// whatever was half-written to them back in the writing box.</summary>
        public void SelectContact(TalkContactVM contact)
        {
            if (contact == null) return;

            foreach (var c in _allContacts) c.IsSelected = c == contact;
            _selected = contact;

            contact.HasUnread = false;
            if (contact.Hero != null)
            {
                TalkScreenManager.ClearUnread(contact.Hero.StringId);
                ImmersiveChatBehavior.OnChatThreadViewed(contact.Hero);
            }

            // The state FIRST, the thread second, and the order is load-bearing: the thread's
            // scrollback asks how this one can be reached (a spoken sheet or a letter's), and asking
            // before the new selection has been weighed answers with the LAST soul's shape.
            RefreshSelectionState();
            RefreshThread();

            TalkScreenManager.ShowCharacter(contact.Hero);
            OnPropertyChanged("TableauData");
            OnPropertyChanged("HasFace");
            OnPropertyChanged("EmptyHint");

            InputText = TalkScreenManager.GetDraft(contact.Folder);
        }

        /// <summary>Selects a given soul's thread if they are listed (used when the screen is opened
        /// by a knock — a map notice, a toast, or a letter's arrival).</summary>
        public void TrySelect(Hero hero)
        {
            if (hero == null) return;
            var contact = _allContacts.FirstOrDefault(c => c.Hero == hero);
            if (contact == null) return;
            if (!Contacts.Contains(contact)) SearchText = string.Empty; // the knock outranks a stale filter
            SelectContact(contact);
        }

        /// <summary>A thread changed underneath the screen (a reply arrived, a letter came, an NPC's
        /// first word landed): refresh what is on stage, or mark the knock unread in the list.</summary>
        public void OnThreadChanged(string heroStringId)
        {
            if (_selected?.Hero != null && _selected.Hero.StringId == heroStringId)
            {
                RefreshSelectionState();   // the reach may have changed under us (a courier set out)
                RefreshThread();
            }
            else
            {
                var contact = _allContacts.FirstOrDefault(c => c.Hero != null && c.Hero.StringId == heroStringId);
                if (contact != null) contact.HasUnread = true;
                else RefreshContacts(); // someone new stepped into the circle with their first word
            }
        }

        /// <summary>The sending failed (the words were never recorded): put them back into the
        /// writing box so nothing the player wrote is lost.</summary>
        public void OnSendFailed(string heroStringId, string text)
        {
            _pendingLines.Remove(heroStringId);
            if (_selected?.Hero != null && _selected.Hero.StringId == heroStringId && string.IsNullOrEmpty(_inputText))
                InputText = text ?? string.Empty;
            OnThreadChanged(heroStringId);
        }

        // ------------------------------ the thread on stage ------------------------------

        private void RefreshThread()
        {
            var messages = new MBBindingList<ChatMessageVM>();
            var contact = _selected;
            if (contact == null) { Messages = messages; MemoryLoadText = string.Empty; return; }

            var npc = contact.Hero;
            var npcName = contact.Name;
            var playerName = Hero.MainHero?.Name?.ToString() ?? "You";

            // A soul gone from the world keeps no living memory to read — but their letters remain,
            // and that correspondence IS their thread. (The letter window's own view, kept.)
            if (npc == null)
            {
                MemoryLoadText = string.Empty;
                AddCorrespondence(messages, contact, npcName, playerName);
                Messages = messages;
                TalkScreenManager.RequestScrollToBottom();
                return;
            }

            // ABOVE the oldest word: everything they are about to be given. Scroll up far enough and
            // the thread simply keeps going, out of the conversation and into the mind behind it.
            AddPromptScrollback(messages, npcName);

            var memory = ImmersiveChatBehavior.PeekMemoryFor(npc);

            // How heavy their verbatim memory of you has grown against the thresholds that fold it
            // into the summary — built here, where the memory is already in hand (no second read).
            MemoryLoadText = memory == null
                ? string.Empty
                : MemoryTokenProfile.MemoryLoadLabel(_config, memory, CampaignTime.Now.ToDays);

            var voice = string.IsNullOrWhiteSpace(_config.SystemVoiceName) ? "Angel" : _config.SystemVoiceName.Trim();

            // THE LINE stands where it belongs — at the moment, not at the end (Anton's screenshot,
            // 2026.08.10): immediately after the last exchange the two of them actually had alone,
            // with everything that came after it below. -1 means there is no such moment.
            var lineBlock = ImmersiveChatBehavior.TogetherBlock(npc);
            double lineDay = string.IsNullOrWhiteSpace(lineBlock)
                ? double.MaxValue : ImmersiveChatBehavior.LastAloneDayOf(npc);
            bool lineDrawn = string.IsNullOrWhiteSpace(lineBlock);

            void DrawLineBefore(double turnDay)
            {
                if (lineDrawn || turnDay <= lineDay) return;
                lineDrawn = true;
                messages.Add(new ChatMessageVM("— not yet discussed between you —",
                    lineBlock.Trim(), isNarration: true, LineHeaderColor));
            }

            if (memory != null)
            {
                foreach (var turn in memory.RecentTurns)
                {
                    var stamp = Stamp(turn);
                    DrawLineBefore(turn.GameDay);
                    if (turn.IsFromAngel || turn.IsInnerThought)
                    {
                        // Letters wear their letters openly, in their place in the one thread — the
                        // whole point of the merge (Anton, 2026.08.14). Recognized across both eras:
                        // the retired Angel's recorded beats and the first-person inner ones.
                        if (Core.Prompts.PromptBuilder.IsComposeLetterBeat(turn.PlayerLine))
                        {
                            // A letter still on the road stays sealed: she remembers writing it, but
                            // its words are not the player's until the courier arrives.
                            if (ImmersiveChatBehavior.IsLetterOnRoadToPlayer(npc.StringId, turn.NpcLine))
                            {
                                messages.Add(new ChatMessageVM(string.Empty,
                                    WithStamp(stamp, $"✉ {npcName} has written you a letter — it is sealed, and rides toward you still."),
                                    isNarration: true, Colors.White));
                                continue;
                            }
                            messages.Add(new ChatMessageVM(string.Empty,
                                WithStamp(stamp, $"✉ {npcName} takes up the quill and writes to you:"),
                                isNarration: true, Colors.White));
                            if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                                messages.Add(new ChatMessageVM($"{npcName} ✉ by letter",
                                    turn.NpcLine, isNarration: false, NpcHeaderColor));
                            continue;
                        }
                        if (Core.Prompts.PromptBuilder.TryExtractReceivedLetter(turn.PlayerLine, out var letterBody))
                        {
                            messages.Add(new ChatMessageVM(string.Empty,
                                WithStamp(stamp, $"✉ Your letter reaches {npcName}:"),
                                isNarration: true, Colors.White));
                            messages.Add(new ChatMessageVM($"{playerName} ✉ by letter",
                                letterBody, isNarration: false, PlayerHeaderColor));
                            if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                                messages.Add(new ChatMessageVM(string.Empty,
                                    $"({npcName}, on whether to answer: {turn.NpcLine})",
                                    isNarration: true, Colors.White));
                            continue;
                        }

                        // A night of the marriage wears its own card. The beat holds only the NAME —
                        // the account itself lives in the night ledger — so the card is filled from
                        // there, and only for the freshest few: older nights keep their name and
                        // nothing more, which is how memory works anyway.
                        if (Core.Nights.NightText.IsNightBeat(turn.PlayerLine))
                        {
                            var nightName = Core.Nights.NightText.ExtractNightName(turn.PlayerLine);
                            var nightStory = ImmersiveChatBehavior.NightStoryInThread(npc, turn.GameDay);
                            messages.Add(new ChatMessageVM(string.Empty,
                                WithStamp(stamp, "☾ " + turn.PlayerLine),
                                isNarration: true, Colors.White));
                            if (!string.IsNullOrWhiteSpace(nightStory))
                                messages.Add(new ChatMessageVM(
                                    string.IsNullOrWhiteSpace(nightName) ? $"☾ {npcName} — that night" : "☾ " + nightName,
                                    nightStory, isNarration: false, LineHeaderColor));
                            continue;
                        }

                        // The wedding day, and the night that followed it: the written account is a
                        // thing to be READ, not a stage direction — so the framing line stays soft
                        // narration and the account itself is drawn as its own block.
                        if (Core.Weddings.WeddingText.TrySplitBeat(turn.PlayerLine, out var weddingFrame, out var weddingAccount))
                        {
                            bool isNight = Core.Weddings.WeddingText.IsNightBeat(turn.PlayerLine);
                            if (!string.IsNullOrWhiteSpace(weddingFrame))
                                messages.Add(new ChatMessageVM(string.Empty, WithStamp(stamp, "❦ " + weddingFrame),
                                    isNarration: true, Colors.White));
                            else
                                messages.Add(new ChatMessageVM(string.Empty,
                                    WithStamp(stamp, $"❦ {npcName} remembers the night that followed — theirs alone."),
                                    isNarration: true, Colors.White));
                            if (!string.IsNullOrWhiteSpace(weddingAccount))
                                messages.Add(new ChatMessageVM(
                                    isNight ? $"❦ {npcName} — that night" : "❦ The wedding day",
                                    weddingAccount, isNarration: false, WeddingHeaderColor));
                            continue;
                        }

                        // A child's day wears the cradle's own card — the hour in the mother's voice,
                        // the feast the hall carries. A father's plain mark and a grief mark carry no
                        // account and stay soft narration, which is exactly what they are.
                        if (Core.Births.BirthText.IsBirthBeat(turn.PlayerLine))
                        {
                            if (Core.Births.BirthText.TrySplitBeat(turn.PlayerLine, out var birthFrame, out var birthAccount))
                            {
                                bool isHour = Core.Births.BirthText.IsHourBeat(turn.PlayerLine);
                                if (!string.IsNullOrWhiteSpace(birthFrame))
                                    messages.Add(new ChatMessageVM(string.Empty, WithStamp(stamp, "❧ " + birthFrame),
                                        isNarration: true, Colors.White));
                                if (!string.IsNullOrWhiteSpace(birthAccount))
                                    messages.Add(new ChatMessageVM(
                                        isHour ? $"❧ {npcName} — that hour" : "❧ The feast for the child",
                                        birthAccount, isNarration: false, CradleHeaderColor));
                            }
                            else
                            {
                                messages.Add(new ChatMessageVM(string.Empty,
                                    WithStamp(stamp, "❧ " + turn.PlayerLine), isNarration: true, Colors.White));
                            }
                            continue;
                        }

                        if (turn.IsFromAngel)
                        {
                            // A retired narrator's recorded beats (older saves) — still shown softly,
                            // exactly as her own prompt replays them, never hidden.
                            messages.Add(new ChatMessageVM(string.Empty,
                                WithStamp(stamp, $"{voice}, softly into {npcName}'s mind: {turn.PlayerLine}"),
                                isNarration: true, Colors.White));
                        }
                        else if (Core.Prompts.PromptBuilder.IsPonderBeat(turn.PlayerLine))
                        {
                            // A ponder is wholly inner — reckoning and resolution fold into one soft
                            // line, nothing was spoken aloud.
                            messages.Add(new ChatMessageVM(string.Empty,
                                WithStamp(stamp, $"({npcName}, within: {turn.PlayerLine} {turn.NpcLine})".TrimEnd()),
                                isNarration: true, Colors.White));
                            continue;
                        }
                        else
                        {
                            // Her own mind at work — an arrival met, an approach made, a bargain
                            // lived; the words she actually spoke stand as a spoken card below.
                            messages.Add(new ChatMessageVM(string.Empty,
                                WithStamp(stamp, $"({npcName}, within: {turn.PlayerLine})"),
                                isNarration: true, Colors.White));
                        }
                    }
                    else
                    {
                        AddSpoken(messages, WithStamp(stamp, playerName), turn.PlayerLine, PlayerHeaderColor);
                    }

                    if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                        AddSpoken(messages, npcName, turn.NpcLine, NpcHeaderColor);
                }
            }

            // A line already sent but not yet answered is not a recorded turn yet — show it, and her
            // considering, so the wait is never a blank. Shown even when the sent line itself is
            // unknown (screen reopened mid-reply: the draft dict dies with the old VM, hers does not).
            var busy = ImmersiveChatBehavior.IsQuickChatBusy(npc);
            if (busy)
            {
                if (_pendingLines.TryGetValue(npc.StringId, out var pendingLine))
                    AddSpoken(messages, playerName, pendingLine, PlayerHeaderColor);
                messages.Add(new ChatMessageVM(string.Empty, $"({npcName} considers your words…)", isNarration: true, Colors.White));
            }
            else
            {
                _pendingLines.Remove(npc.StringId);
            }

            // A courier of the player's own on the road is not in her memory at all — she has not
            // read it yet. It belongs at the foot of the thread all the same: a letter is a promise
            // already underway, and the screen should never look as though nothing was sent.
            var riding = ImmersiveChatBehavior.Current?.CourierStatusFor(npc.StringId) ?? string.Empty;
            if (riding.Length > 0)
                messages.Add(new ChatMessageVM(string.Empty, $"✉ ({riding})", isNarration: true, Colors.White));

            if (messages.Count == 0)
                messages.Add(new ChatMessageVM(string.Empty,
                    $"(No words have yet passed between you and {npcName} — yours would be the first.)",
                    isNarration: true, Colors.White));

            // If everything after the moment has already aged out of the verbatim turns, the line
            // never found a beat to stand before — it still belongs in the thread, at the foot.
            if (!lineDrawn)
                messages.Add(new ChatMessageVM("— not yet discussed between you —",
                    lineBlock.Trim(), isNarration: true, LineHeaderColor));

            Messages = messages;
            TalkScreenManager.RequestScrollToBottom();
        }

        // ------------------------------ what they are about to be given ------------------------------
        //
        // THE SCROLLBACK (Anton, 2026.08.14). No button, no popup, no developer's mode: the thread
        // keeps going upward past its oldest word into everything the chosen one carries into your
        // next message — their own mind in their own first person, then the hands they may reach for
        // while they answer — in the very order they receive it. Then the conversation itself.
        //
        // The sheet is cut into cards at its own paragraph breaks rather than shown as one slab:
        // Gauntlet lays out a single enormous text badly, and a reader scrolling wants landmarks.
        private void AddPromptScrollback(MBBindingList<ChatMessageVM> messages, string npcName)
        {
            var preview = _selected?.Hero == null
                ? null
                : ImmersiveChatBehavior.PromptPreviewFor(_selected.Hero, IsAway);
            if (preview == null) return;

            messages.Add(new ChatMessageVM(
                IsAway ? $"▲ What {npcName} will read your letter with" : $"▲ What {npcName} will hear you with",
                "Everything below is given to them the moment you write — in this order, and in their "
                + "own voice. Nothing here is spoken aloud; it is simply who they are when your words "
                + "arrive. Keep scrolling down for the conversation itself.",
                isNarration: true, PromptFrameColor));

            foreach (var block in SplitIntoBlocks(preview.Sheet))
                messages.Add(new ChatMessageVM(string.Empty, block, isNarration: true, PromptSheetColor));

            if (preview.Tools.Count > 0)
            {
                messages.Add(new ChatMessageVM("✦ What they may reach for while answering",
                    "They are not told to use these. They are simply able to — to look something up, "
                    + "to weigh how they feel about you — and each one is described to them exactly "
                    + "as it reads here.",
                    isNarration: true, PromptFrameColor));

                foreach (var tool in preview.Tools)
                {
                    var body = new StringBuilder();
                    body.Append(tool.Description);
                    foreach (var p in tool.Parameters)
                        body.Append("\n   · ").Append(p);
                    messages.Add(new ChatMessageVM("✦ " + tool.Name, body.ToString(),
                        isNarration: true, PromptToolColor));
                }
            }

            messages.Add(new ChatMessageVM("▼ And then everything between you",
                "Every word below is remembered, and rides along with the rest.",
                isNarration: true, PromptFrameColor));
        }

        // Paragraphs, kept whole. The sheet is written in blocks with blank lines between them, so
        // its own shape is the right one to read by.
        private static List<string> SplitIntoBlocks(string sheet)
        {
            var blocks = new List<string>();
            if (string.IsNullOrWhiteSpace(sheet)) return blocks;

            var current = new StringBuilder();
            foreach (var rawLine in sheet.Replace("\r\n", "\n").Split('\n'))
            {
                if (rawLine.Trim().Length == 0)
                {
                    if (current.Length > 0) { blocks.Add(current.ToString().TrimEnd()); current.Clear(); }
                    continue;
                }
                if (current.Length > 0) current.Append('\n');
                current.Append(rawLine);
            }
            if (current.Length > 0) blocks.Add(current.ToString().TrimEnd());
            return blocks;
        }

        // The correspondence of a soul who is gone: their letters are all that is left, and they
        // stay readable forever. Parsed from letters.txt exactly as the letter window did.
        private static void AddCorrespondence(
            MBBindingList<ChatMessageVM> messages, TalkContactVM contact, string npcName, string playerName)
        {
            foreach (var entry in ImmersiveChatBehavior.CorrespondenceEntriesFor(contact.Folder))
            {
                if (entry.IsNote)
                {
                    messages.Add(new ChatMessageVM(string.Empty,
                        WithStamp(entry.Stamp, $"({entry.Body})"), isNarration: true, Colors.White));
                    continue;
                }

                bool fromThem = string.Equals(entry.FromName, npcName, StringComparison.Ordinal)
                                || !string.Equals(entry.FromName, playerName, StringComparison.Ordinal)
                                   && !string.Equals(entry.ToName, npcName, StringComparison.Ordinal);
                var provenance = string.IsNullOrEmpty(entry.Detail) ? string.Empty : $"  ({entry.Detail})";
                messages.Add(new ChatMessageVM(
                    WithStamp(entry.Stamp, $"✉ {entry.FromName}{provenance}"),
                    entry.Body, isNarration: false,
                    fromThem ? NpcHeaderColor : PlayerHeaderColor));
            }

            if (messages.Count == 0)
                messages.Add(new ChatMessageVM(string.Empty,
                    $"(Nothing of {npcName} remains here to read.)", isNarration: true, Colors.White));
        }

        private static string Stamp(ConversationTurn turn)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(turn.Place)) parts.Add(turn.Place.Trim());
            if (!string.IsNullOrWhiteSpace(turn.CalradiaTime)) parts.Add(turn.CalradiaTime.Trim());
            return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
        }

        private static string WithStamp(string stamp, string text) =>
            string.IsNullOrEmpty(stamp) ? text : $"[{stamp}]  {text}";

        // A spoken message may carry small acted gestures between *asterisks* (the acting-out
        // grammar — EmoteText): the words draw as the spoken card, each gesture as a soft narration
        // line in its place. The header rides the first segment whatever it is — a reply that is all
        // gesture still says whose act it was.
        private static void AddSpoken(
            MBBindingList<ChatMessageVM> messages, string header, string body, Color headerColor)
        {
            var segments = Core.Prompts.EmoteText.Split(body);
            if (segments.Count == 0)
            {
                messages.Add(new ChatMessageVM(header, body, isNarration: false, headerColor));
                return;
            }
            bool first = true;
            foreach (var seg in segments)
            {
                var head = first ? header : string.Empty;
                if (seg.IsGesture)
                    messages.Add(new ChatMessageVM(head, $"*{seg.Text}*", isNarration: true, headerColor));
                else
                    messages.Add(new ChatMessageVM(head, seg.Text, isNarration: false, headerColor));
                first = false;
            }
        }

        private void RefreshSelectionState()
        {
            SelectedName = _selected?.Name ?? string.Empty;

            // How this one can be reached at all — the one question that decides the button, the
            // key bindings, and what the grey line under the name says.
            _reach = _selected == null
                ? ImmersiveChatBehavior.TalkReach.Closed
                : ImmersiveChatBehavior.ReachOf(_selected.Hero, out var why);
            ReachText = _selected == null ? string.Empty : ReasonFor(_selected, _reach);

            OnPropertyChanged("HasSelection");
            OnPropertyChanged("DevTitleText");
            OnPropertyChanged("IsAway");
            OnPropertyChanged("IsGone");
            OnPropertyChanged("SendText");
            OnPropertyChanged("CanSend");
            OnPropertyChanged("CanEdit");
            OnPropertyChanged("MessagesBottomMargin");
            OnPropertyChanged("HasDraft");

            IsWaiting = _selected?.Hero != null && ImmersiveChatBehavior.IsQuickChatBusy(_selected.Hero);
            RefreshThinkState();

            _relationText = _selected?.Hero == null ? string.Empty : ImmersiveChatBehavior.RelationLabel(_selected.Hero);
            _relationColor = RelationTint(_selected?.Hero == null ? 0 : ImmersiveChatBehavior.RelationValue(_selected.Hero));
            OnPropertyChanged("RelationText");
            OnPropertyChanged("HasRelation");
            OnPropertyChanged("RelationColor");

            BondStatsText = _selected?.Hero == null ? string.Empty : ImmersiveChatBehavior.BondStatsLabel(_selected.Hero);

            // One little button under the name, carrying whatever this bond's own page is right now:
            // her misgivings while the road is walked, her kin's blessing to be sought, the days of
            // preparation counting down, then the wedding day itself, kept forever.
            var page = _selected?.Hero != null ? ImmersiveChatBehavior.RoadPageFor(_selected.Hero) : null;
            _roadPage = page;
            _pageIsWedding = page != null && page.Kind == ImmersiveChatBehavior.RoadPageKind.WeddingDay;
            if (page != null)
            {
                MisgivingsButtonText = page.Label;
                MisgivingsTitleText = page.Title;
                MisgivingsBodyText = page.Body;
            }
            else
            {
                MisgivingsButtonText = string.Empty;
                IsMisgivingsShown = false;
            }
            OnPropertyChanged("HasMisgivings");
            OnPropertyChanged("MisgivingsHintText");
        }

        // The grey line under the name: how they can be reached, and — when they cannot — why. A
        // silent gray button is indistinguishable from a broken one (the bargain lesson).
        private static string ReasonFor(TalkContactVM contact, ImmersiveChatBehavior.TalkReach reach)
        {
            ImmersiveChatBehavior.ReachOf(contact.Hero, out var reason);
            switch (reach)
            {
                case ImmersiveChatBehavior.TalkReach.Spoken:
                    return $"{contact.Name} stands with you — speak, and they will answer.";
                case ImmersiveChatBehavior.TalkReach.Letter:
                    return "Too far for words — the road is open, and a courier stands ready.";
                default:
                    return reason;
            }
        }

        // Warm green when they hold you dear, cool red when they do not, plain parchment at neutral.
        private static Color RelationTint(int relation)
        {
            if (relation > 0) return new Color(0.55f, 0.82f, 0.55f, 1f);
            if (relation < 0) return new Color(0.86f, 0.53f, 0.49f, 1f);
            return new Color(0.78f, 0.75f, 0.68f, 1f);
        }

        // ------------------------------ speaking, and sealing ------------------------------

        /// <summary>The one door out of the writing box: a spoken line when they stand with you, a
        /// sealed letter when only a courier can reach them. Which of the two it is was decided when
        /// they were chosen, so nothing here has to guess.</summary>
        public void ExecuteSend()
        {
            var contact = _selected;
            var npc = contact?.Hero;
            var text = (_inputText ?? string.Empty).Trim();
            if (contact == null || npc == null || text.Length == 0) return;

            // The graying is a courtesy; THIS is the rail (Anton, 2026.08.10 — a wish went out at a
            // greyed-out button's click). Every road into the sending runs through here, and a
            // refusal says why.
            if (_isWish)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    _reach == ImmersiveChatBehavior.TalkReach.Letter
                        ? "Those are my own thoughts, not the letter — Shift+Enter turns them into words."
                        : "Those are my own thoughts, not words to say — Shift+Enter turns them into words.",
                    PlayerHeaderColor));
                return;
            }

            if (_reach == ImmersiveChatBehavior.TalkReach.Spoken)
            {
                if (IsWaiting) return;
                if (!ImmersiveChatBehavior.SendQuickChat(npc, text)) return;

                _pendingLines[npc.StringId] = text;
                InputText = string.Empty;
                // Your words reach them, and they shift their weight a little — the one movement
                // there is, at the one moment worth having it.
                TalkScreenManager.ShiftStance(npc);
                RefreshSelectionState();
                RefreshThread();
                return;
            }

            if (_reach == ImmersiveChatBehavior.TalkReach.Letter)
            {
                if (!ImmersiveChatBehavior.SendLetterFromWindow(npc, text)) { RefreshSelectionState(); return; }

                InputText = string.Empty;
                RefreshSelectionState();// the road is now taken until word comes back
                RefreshThread();        // and the courier rides at the foot of the thread
                RefreshContacts();      // their row's detail line says so too
                return;
            }

            // Closed: say why rather than doing nothing at all.
            if (!string.IsNullOrEmpty(_reachText))
                InformationManager.DisplayMessage(new InformationMessage(_reachText, PlayerHeaderColor));
        }

        // ------------------------------ "Let me think…" ------------------------------
        // The player's own next line, worked out on the very sheet the chosen one would answer on.
        // Whatever stands in the writing box rides along as the wish, and what comes back replaces
        // it — theirs to keep, change, or throw away. Nothing is recorded.

        public void ExecuteThink()
        {
            var npc = _selected?.Hero;
            if (npc == null || !CanThink) return;
            if (!ImmersiveChatBehavior.BeginPlayerThought(npc, asLetter: IsAway, _inputText)) return;

            IsPresetsShown = false;
            RefreshThinkState();
        }

        /// <summary>The words came back: they take the writing box, and the wish they grew from is
        /// spent. A thread that has since moved on only refreshes the button (the words are safely
        /// in the manager's draft store either way).</summary>
        public void OnThoughtReady(string heroStringId, string words)
        {
            if (_selected?.Hero != null && _selected.Hero.StringId == heroStringId)
            {
                InputText = words ?? string.Empty;
                IsWish = false;
            }
            RefreshThinkState();
        }

        public void OnThoughtFailed(string heroStringId) => RefreshThinkState();

        private void RefreshThinkState()
        {
            IsThinking = _selected?.Hero != null
                         && ImmersiveChatBehavior.IsThinkingFor(_selected.Hero, asLetter: IsAway);
            OnPropertyChanged("CanThink");
            OnPropertyChanged("ThinkText");
        }

        // ------------------------------ the presets menu ------------------------------

        public void ExecuteTogglePresets()
        {
            if (!_isPresetsShown) LoadPresets();
            IsPresetsShown = !_isPresetsShown;
        }

        /// <summary>Opens the editing page (from inside the little menu, so the menu folds away).</summary>
        public void ExecuteOpenPresetEditor()
        {
            LoadPresets();
            PresetEditName = string.Empty;
            PresetEditText = string.Empty;
            IsPresetsShown = false;
            IsPresetEditShown = true;
        }

        /// <summary>Adds the preset being written, or rewrites the one already wearing its name.</summary>
        public void ExecuteSavePreset()
        {
            if (string.IsNullOrWhiteSpace(_presetEditText)) return;
            _presetList = Core.Prompts.ConversationPresets.Upsert(_presetList, _presetEditName, _presetEditText);
            ImmersiveChatBehavior.SaveConversationPresets(_presetList);
            PresetEditName = string.Empty;
            PresetEditText = string.Empty;
            BuildPresetRows();
        }

        private void LoadPresets()
        {
            _presetList = ImmersiveChatBehavior.ConversationPresetsForMenu();
            BuildPresetRows();
        }

        private void BuildPresetRows()
        {
            var menu = new MBBindingList<ConversationPresetVM>();
            var rows = new MBBindingList<ConversationPresetVM>();
            foreach (var preset in _presetList)
            {
                menu.Add(new ConversationPresetVM(preset, ChoosePreset));
                rows.Add(new ConversationPresetVM(preset, ChoosePreset, EditPreset, RemovePreset));
            }
            Presets = menu;
            PresetRows = rows;
            OnPropertyChanged("HasPresets");
            OnPropertyChanged("NoPresetsText");
        }

        // Choosing one drops its words into the writing box AS A WISH — never sent, only thought on.
        private void ChoosePreset(ConversationPresetVM row)
        {
            if (row == null) return;
            InputText = row.Wish;
            _wishText = row.Wish;
            IsWish = true;
            IsPresetsShown = false;
            IsPresetEditShown = false;
        }

        private void EditPreset(ConversationPresetVM row)
        {
            if (row == null) return;
            PresetEditName = row.Name;
            PresetEditText = row.Wish;
        }

        private void RemovePreset(ConversationPresetVM row)
        {
            if (row == null) return;
            _presetList = Core.Prompts.ConversationPresets.Remove(_presetList, row.Name);
            ImmersiveChatBehavior.SaveConversationPresets(_presetList);
            BuildPresetRows();
        }

        /// <summary>Back to the three given at the start — asked about first, since it throws every
        /// preset of the player's own away.</summary>
        public void ExecuteRestorePresets() => ImmersiveChatBehavior.RestoreConversationPresets(LoadPresets);

        public void ExecuteClose() => TalkScreenManager.Close();

        public void ExecuteToggleInfo() => IsInfoShown = !IsInfoShown;

        /// <summary>The way back out of whichever page is up — the same order Escape folds them in.
        /// Every overlay wears it as a button, because "X" closes the whole screen and nothing else
        /// said how to step back.</summary>
        public void ExecuteBack()
        {
            if (IsPromptEditShown) ExecutePromptCancel();
            else if (IsPresetEditShown) IsPresetEditShown = false;
            else if (IsPresetsShown) IsPresetsShown = false;
            else if (IsDevShown) IsDevShown = false;
            else if (IsMisgivingsShown) IsMisgivingsShown = false;
            else if (IsInfoShown) IsInfoShown = false;
        }

        // ------------------------------ the in-game prompt editor ------------------------------
        // The editing doors, IN the game (Anton's ask — no Notepad, no alt-tab): an overlay with the
        // letters-composer shape — a tall wrapped mirror of the whole text, a single writing line
        // beneath (Gauntlet inputs cannot hold newlines), Save and Discard. Prompts are re-read on
        // every reply, so a saved edit speaks from the very next answer.

        private Hero? _promptEditNpc;   // null while editing the world's global prompt

        public void ExecuteEditNpcPrompt()
        {
            var npc = _selected?.Hero;
            if (npc == null) return;
            _promptEditNpc = npc;
            PromptEditTitle = $"Their prompt — {npc.Name} — write it in their own voice: \"I ...\"";
            PromptEditText = PromptFiles.LoadNpcPromptForEdit(
                NpcPaths.CustomInstructionsFile(npc), npc.Name?.ToString() ?? "Unknown");
            IsPromptEditShown = true;
        }

        public void ExecuteEditGlobalPrompt()
        {
            _promptEditNpc = null;
            PromptEditTitle = "World prompt — what every soul knows of this world";
            PromptEditText = PromptFiles.LoadGlobalPromptForEdit();
            IsPromptEditShown = true;
        }

        public void ExecutePromptSave()
        {
            try
            {
                if (_promptEditNpc != null)
                    PromptFiles.SaveNpcPromptFromGame(
                        NpcPaths.CustomInstructionsFile(_promptEditNpc),
                        _promptEditNpc.Name?.ToString() ?? "Unknown", PromptEditText);
                else
                    PromptFiles.SaveGlobalPromptFromGame(PromptEditText);
                InformationManager.DisplayMessage(new InformationMessage(_promptEditNpc != null
                    ? $"{_promptEditNpc.Name}'s prompt is set — it speaks from their next reply."
                    : "The world prompt is set — it speaks from the next reply."));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
            IsPromptEditShown = false;
        }

        public void ExecutePromptCancel() => IsPromptEditShown = false;

        // ------------------------------ the road page ------------------------------

        /// <summary>Recomputes just the road button — called when a game day turns under an open
        /// screen, so a countdown counts down where the player is looking.</summary>
        public void RefreshRoadPage()
        {
            try { if (_selected != null) RefreshSelectionState(); }
            catch { }
        }

        public void ExecuteToggleMisgivings()
        {
            var npc = _selected?.Hero;
            var kind = _roadPage?.Kind ?? ImmersiveChatBehavior.RoadPageKind.Misgivings;

            // The wedding day plays itself again before it is read (Anton, 2026.08.09).
            if (kind == ImmersiveChatBehavior.RoadPageKind.WeddingDay)
            {
                if (npc != null) ImmersiveChatBehavior.ShowWeddingViewFor(npc);
                return;
            }

            // The wedding stage is a DOOR, not a page: it opens the choice of the day itself.
            if (kind == ImmersiveChatBehavior.RoadPageKind.Wedding && npc != null)
            {
                IsMisgivingsShown = false;
                ImmersiveChatBehavior.OpenWeddingDoorFor(npc);
                return;
            }

            IsMisgivingsShown = !IsMisgivingsShown;
        }

        // ------------------------------ the dev panel ------------------------------

        public void ExecuteToggleDev() => IsDevShown = !IsDevShown;

        private void RunDev(Action<Hero> lever)
        {
            var npc = _selected?.Hero;
            if (npc == null) return;
            IsDevShown = false;
            lever(npc);
        }

        public void ExecuteDevRevealMind() => RunDev(ImmersiveChatBehavior.DevRevealMind);
        public void ExecuteDevRevealRoad() => RunDev(ImmersiveChatBehavior.DevRevealCourtship);
        public void ExecuteDevClearMisgivings() => RunDev(npc =>
        {
            ImmersiveChatBehavior.DevClearMisgivings(npc);
            RefreshSelectionState();
        });
        public void ExecuteDevAdvanceRoad() => RunDev(npc =>
        {
            ImmersiveChatBehavior.DevAdvanceCourtship(npc);
            RefreshSelectionState();
        });
        public void ExecuteDevChild() => RunDev(ImmersiveChatBehavior.DevHastenConception);

        // Voice-over milestone 1 — does the game's own audio engine play a WAV of ours? Temporary.
        public void ExecuteDevTestSound() => RunDev(ImmersiveChatBehavior.DevTestSound);

        // ------------------------------ bound properties ------------------------------

        [DataSourceProperty]
        public MBBindingList<TalkContactVM> Contacts
        {
            get => _contacts;
            set { if (value != _contacts) { _contacts = value; OnPropertyChangedWithValue(value, "Contacts"); } }
        }

        [DataSourceProperty]
        public MBBindingList<ChatMessageVM> Messages
        {
            get => _messages;
            set { if (value != _messages) { _messages = value; OnPropertyChangedWithValue(value, "Messages"); } }
        }

        [DataSourceProperty]
        public string TitleText => "Those you know";

        /// <summary>What the middle says when there is no face in it — and there are three different
        /// reasons for that, so it says which. A black rectangle explains nothing.</summary>
        [DataSourceProperty]
        public string EmptyHint
        {
            get
            {
                if (!HasSelection) return "Choose someone, and simply speak.";
                if (HasFace) return string.Empty;
                if (IsGone) return $"{SelectedName} is gone from this world.\nWhat they wrote to you remains.";
                return $"{SelectedName}\n(their likeness cannot be raised just now — every word still reaches them)";
            }
        }

        /// <summary>The chosen one, drawn standing in their own place. Null — and the middle of the
        /// screen falls back to its hint — when nobody is chosen, or when this game build cannot
        /// raise the conversation visuals at all (a grace, never the point).</summary>
        [DataSourceProperty]
        public object? TableauData => ConversationTableauController.TableauData;

        /// <summary>Whether there is a face on stage to look at.</summary>
        [DataSourceProperty]
        public bool HasFace => HasSelection && ConversationTableauController.TableauData != null;

        /// <summary>The one button, named for what it will actually do to these words. Keyed to
        /// DISTANCE, not to whether the road happens to be open: someone across the map is written
        /// to, and the button must go on saying so while the seal is shut — a button that renames
        /// itself "Send  (Enter)" the moment a courier sets out, then refuses Enter, teaches the
        /// player something untrue about their own keyboard.</summary>
        [DataSourceProperty]
        public string SendText => IsAway ? "Seal and send" : "Send  (Enter)";

        [DataSourceProperty]
        public bool HasSelection => _selected != null;

        [DataSourceProperty]
        public string RelationText
        {
            get => _relationText;
            set { if (value != _relationText) { _relationText = value; OnPropertyChangedWithValue(value, "RelationText"); } }
        }

        [DataSourceProperty]
        public bool HasRelation => !string.IsNullOrEmpty(_relationText);

        /// <summary>How the chosen one can be reached, and why not when they cannot.</summary>
        [DataSourceProperty]
        public string ReachText
        {
            get => _reachText;
            set
            {
                if (value != _reachText)
                {
                    _reachText = value;
                    OnPropertyChangedWithValue(value, "ReachText");
                    OnPropertyChanged("HasReach");
                    OnGreyBlockChanged();
                }
            }
        }

        [DataSourceProperty]
        public bool HasReach => !string.IsNullOrEmpty(_reachText);

        /// <summary>The bond's own mechanics under the name — shared story, freshness, and the hourly
        /// chance they are moved to come or to write.</summary>
        [DataSourceProperty]
        public string BondStatsText
        {
            get => _bondStatsText;
            set
            {
                if (value != _bondStatsText)
                {
                    _bondStatsText = value;
                    OnPropertyChangedWithValue(value, "BondStatsText");
                    OnPropertyChanged("HasBondStats");
                    OnGreyBlockChanged();
                }
            }
        }

        [DataSourceProperty]
        public bool HasBondStats => !string.IsNullOrEmpty(_bondStatsText);

        /// <summary>The weight of what they keep verbatim of you, against the thresholds where it is
        /// folded into the rolling memory: the share of the model's context window, the tokens, the
        /// turns, the age of the oldest one — every number a live compression trigger.</summary>
        [DataSourceProperty]
        public string MemoryLoadText
        {
            get => _memoryLoadText;
            set
            {
                if (value != _memoryLoadText)
                {
                    _memoryLoadText = value;
                    OnPropertyChangedWithValue(value, "MemoryLoadText");
                    OnPropertyChanged("HasMemoryLoad");
                    OnGreyBlockChanged();
                }
            }
        }

        [DataSourceProperty]
        public bool HasMemoryLoad => !string.IsNullOrEmpty(_memoryLoadText);

        /// <summary>The search line above the list — typing refilters the names at once.</summary>
        [DataSourceProperty]
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value != _searchText)
                {
                    _searchText = value ?? string.Empty;
                    OnPropertyChangedWithValue(value, "SearchText");
                    OnPropertyChanged("IsSearchEmpty");
                    ApplyContactFilter();
                }
            }
        }

        [DataSourceProperty]
        public bool IsSearchEmpty => string.IsNullOrEmpty(_searchText);

        [DataSourceProperty]
        public string SearchHintText => "Search…";

        [DataSourceProperty]
        public Color RelationColor
        {
            get => _relationColor;
            set { if (value != _relationColor) { _relationColor = value; OnPropertyChangedWithValue(value, "RelationColor"); } }
        }

        [DataSourceProperty]
        public string SelectedName
        {
            get => _selectedName;
            set { if (value != _selectedName) { _selectedName = value; OnPropertyChangedWithValue(value, "SelectedName"); } }
        }

        /// <summary>The way out of every overlay, worn as a button on each of them.</summary>
        [DataSourceProperty]
        public string BackText => "← Back  (Esc)";

        /// <summary>The same corner button on the prompt editors — named honestly, because stepping
        /// back from a half-written prompt throws the edit away (Save is the door that keeps it).</summary>
        [DataSourceProperty]
        public string BackDiscardText => "← Back  (discards)";

        // The grey lines under the name stack in a ListPanel, so they can never land on each other —
        // but everything BELOW them is placed by margin, and Gauntlet will not tell us how tall a
        // wrapped line came out. So we estimate it from the strings and always round UP: a little air
        // costs nothing, an overlap is the bug (Anton's screenshot, 2026.08.08).
        private const float GreyBlockTop = 118f;
        private const float GreyLineHeight = 20f;
        private const int GreyLineChars = 120;

        private static int WrappedLines(string text)
            => string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (text.Length + GreyLineChars - 1) / GreyLineChars);

        /// <summary>Where the thread begins vertically — right under the header's grey lines, their
        /// wrapping counted.</summary>
        [DataSourceProperty]
        public float MessagesTopMargin =>
            GreyBlockTop + GreyLineHeight * (WrappedLines(_reachText) + WrappedLines(_bondStatsText)
                                             + WrappedLines(_memoryLoadText)) + 12f;

        private void OnGreyBlockChanged()
        {
            OnPropertyChanged("MessagesTopMargin");
            TalkScreenManager.RequestScrollToBottom();
        }

        [DataSourceProperty]
        public string InputText
        {
            get => _inputText;
            set
            {
                if (value != _inputText)
                {
                    // The draft mirror appearing (or folding away) shrinks the thread from the bottom;
                    // re-pin to the newest line so their last words stay in view above the box.
                    bool draftBefore = !string.IsNullOrWhiteSpace(_inputText);
                    _inputText = value ?? string.Empty;
                    bool draftAfter = !string.IsNullOrWhiteSpace(_inputText);

                    OnPropertyChangedWithValue(value, "InputText");
                    OnPropertyChanged("CanSend");
                    OnPropertyChanged("HasDraft");
                    OnPropertyChanged("MessagesBottomMargin");

                    // A chosen preset holds the SENDING shut (see CanSend) until the player makes the
                    // words their own — so one keystroke away from the preset it is theirs again and
                    // Send unlocks, exactly as an emptied box does (Anton, 2026.08.10).
                    if (_isWish && !string.Equals(_inputText, _wishText, StringComparison.Ordinal))
                        IsWish = false;

                    if (draftBefore != draftAfter)
                        TalkScreenManager.RequestScrollToBottom();

                    if (_selected != null)
                        TalkScreenManager.SetDraft(_selected.Folder, _inputText);
                }
            }
        }

        /// <summary>Whether something is being composed — shows the wrapped draft mirror above the
        /// writing line (the engine's editable text is single-line; the mirror is where a long
        /// message stays readable while it is written).</summary>
        [DataSourceProperty]
        public bool HasDraft => CanEdit && !string.IsNullOrWhiteSpace(_inputText);

        /// <summary>Where the thread ends vertically: just above the writing line, or above the draft
        /// mirror while one is being written — taller for a letter, which runs long.</summary>
        [DataSourceProperty]
        public float MessagesBottomMargin => !CanEdit ? 24f
            : HasDraft ? (IsAway ? 280f : 210f) : 122f;

        /// <summary>Whether there is any point in a writing box at all — a soul gone from the world
        /// can be read, never written to.</summary>
        [DataSourceProperty]
        public bool CanEdit => HasSelection && _selected?.Hero != null;

        [DataSourceProperty]
        public bool CanSend => CanEdit && !_isWish && !string.IsNullOrWhiteSpace(_inputText)
                               && (_reach == ImmersiveChatBehavior.TalkReach.Spoken
                                   ? !_isWaiting
                                   : _reach == ImmersiveChatBehavior.TalkReach.Letter);

        /// <summary>Whether the chosen one is across the map — the button becomes a seal, Enter stops
        /// sending, and the thinking writes a letter instead of a line.</summary>
        [DataSourceProperty]
        public bool IsAway => _reach == ImmersiveChatBehavior.TalkReach.Letter
                              || (HasSelection && !IsGone && !(_selected?.IsHere ?? false));

        /// <summary>Whether they are gone from the world — their letters remain, nothing more.</summary>
        [DataSourceProperty]
        public bool IsGone => _selected?.IsGone ?? false;

        [DataSourceProperty]
        public bool IsWaiting
        {
            get => _isWaiting;
            set { if (value != _isWaiting) { _isWaiting = value; OnPropertyChangedWithValue(value, "IsWaiting"); OnPropertyChanged("CanSend"); OnPropertyChanged("CanThink"); } }
        }

        // ------------------------------ "Let me think…" ------------------------------

        /// <summary>Whether the whole thing is offered at all (config) — the two little buttons and
        /// the menu hide together when it is not.</summary>
        [DataSourceProperty]
        public bool ShowThink => _config.EnableThinkForMe;

        /// <summary>The button, in the player's own voice — never a machine's name (Anton,
        /// 2026.08.10: "Think (AI) ще е имържън брейкър"). It says what it is doing while it does it.</summary>
        [DataSourceProperty]
        public string ThinkText => _isThinking ? "…thinking" : "Think  (Shift+Enter)";

        [DataSourceProperty]
        public string PresetText => "Conversation preset";

        /// <summary>An EMPTY writing box is a perfectly good ask (Anton, 2026.08.10): "think of
        /// something to say to them" — the moment, the memory and the situation are cause enough.</summary>
        [DataSourceProperty]
        public bool CanThink => ShowThink && CanEdit && !_isWaiting && !_isThinking
                                && _reach != ImmersiveChatBehavior.TalkReach.Closed;

        [DataSourceProperty]
        public bool IsThinking
        {
            get => _isThinking;
            set
            {
                if (value == _isThinking) return;
                _isThinking = value;
                OnPropertyChangedWithValue(value, "IsThinking");
                OnPropertyChanged("CanThink");
                OnPropertyChanged("ThinkText");
            }
        }

        /// <summary>Whether the writing box holds an INTENDED MEANING rather than words to send — the
        /// draft mirror turns violet and says so, which is the whole of what this flag does. The keys
        /// stay fixed either way: Enter sends, Shift+Enter thinks.</summary>
        [DataSourceProperty]
        public bool IsWish
        {
            get => _isWish;
            set
            {
                if (value == _isWish) return;
                _isWish = value;
                OnPropertyChangedWithValue(value, "IsWish");
                OnPropertyChanged("DraftColor");
                OnPropertyChanged("WishHintText");
                OnPropertyChanged("CanSend");
            }
        }

        [DataSourceProperty]
        public string WishHintText => IsAway
            ? "what I mean to get across, not the letter itself — so the seal is held shut. Shift+Enter and I will think what to write; change a word of it and it is yours to send."
            : "what I mean to get across, not words to send — so Send is held shut. Shift+Enter and I will think what to say; change a word of it and it is yours to send.";

        /// <summary>The draft mirror's ink: warm parchment for words that will be spoken or sealed,
        /// the quiet violet of a private note for a wish that will only be thought on.</summary>
        [DataSourceProperty]
        public Color DraftColor => _isWish ? LineHeaderColor : PlayerHeaderColor;

        /// <summary>The little menu's rows — a click chooses one.</summary>
        [DataSourceProperty]
        public MBBindingList<ConversationPresetVM> Presets
        {
            get => _presets;
            set { if (value != _presets) { _presets = value; OnPropertyChangedWithValue(value, "Presets"); } }
        }

        /// <summary>The same presets on the editing page, wearing a pen and a cross.</summary>
        [DataSourceProperty]
        public MBBindingList<ConversationPresetVM> PresetRows
        {
            get => _presetRows;
            set { if (value != _presetRows) { _presetRows = value; OnPropertyChangedWithValue(value, "PresetRows"); } }
        }

        [DataSourceProperty]
        public bool HasPresets => _presetList.Count > 0;

        [DataSourceProperty]
        public string NoPresetsText => _presetList.Count > 0
            ? string.Empty
            : "No presets kept yet — write one below, or simply press \"Think\" with the box empty.";

        /// <summary>The little menu above the buttons (five rows, then it scrolls).</summary>
        [DataSourceProperty]
        public bool IsPresetsShown
        {
            get => _isPresetsShown;
            set { if (value != _isPresetsShown) { _isPresetsShown = value; OnPropertyChangedWithValue(value, "IsPresetsShown"); } }
        }

        [DataSourceProperty]
        public string PresetsTitleText => "What shall I turn my mind to?";

        [DataSourceProperty]
        public string PresetEditorText => "Edit…";

        /// <summary>The editing page for the presets themselves.</summary>
        [DataSourceProperty]
        public bool IsPresetEditShown
        {
            get => _isPresetEditShown;
            set { if (value != _isPresetEditShown) { _isPresetEditShown = value; OnPropertyChangedWithValue(value, "IsPresetEditShown"); } }
        }

        [DataSourceProperty]
        public string PresetEditTitleText => "My own presets";

        [DataSourceProperty]
        public string PresetEditHintText =>
            "Standing wishes for your own thinking — never sent to anyone. Click one to use it now, the pen to bring it down for reworking, the cross to strike it out. Write below and Save to add one (a name already kept is rewritten). They live in conversation_presets.txt.";

        [DataSourceProperty]
        public string PresetNameHintText => "name";

        [DataSourceProperty]
        public string PresetTextHintText => "what I mean to get across…";

        [DataSourceProperty]
        public string PresetSaveText => "Save";

        [DataSourceProperty]
        public string PresetRestoreText => "Restore the first three";

        [DataSourceProperty]
        public string PresetEditName
        {
            get => _presetEditName;
            set
            {
                if (value == _presetEditName) return;
                _presetEditName = value ?? string.Empty;
                OnPropertyChangedWithValue(value, "PresetEditName");
                OnPropertyChanged("IsPresetNameEmpty");
            }
        }

        [DataSourceProperty]
        public bool IsPresetNameEmpty => string.IsNullOrEmpty(_presetEditName);

        [DataSourceProperty]
        public string PresetEditText
        {
            get => _presetEditText;
            set
            {
                if (value == _presetEditText) return;
                _presetEditText = value ?? string.Empty;
                OnPropertyChangedWithValue(value, "PresetEditText");
                OnPropertyChanged("IsPresetTextEmpty");
                OnPropertyChanged("CanSavePreset");
            }
        }

        [DataSourceProperty]
        public bool IsPresetTextEmpty => string.IsNullOrEmpty(_presetEditText);

        [DataSourceProperty]
        public bool CanSavePreset => !string.IsNullOrWhiteSpace(_presetEditText);

        // ------------------------------ the misgivings / road overlay ------------------------------

        /// <summary>Whether the chosen one has a road page to show at all — keys the little button.</summary>
        [DataSourceProperty]
        public bool HasMisgivings => HasSelection && !string.IsNullOrEmpty(_misgivingsButtonText);

        [DataSourceProperty]
        public string MisgivingsButtonText
        {
            get => _misgivingsButtonText;
            set { if (value != _misgivingsButtonText) { _misgivingsButtonText = value; OnPropertyChangedWithValue(value, "MisgivingsButtonText"); } }
        }

        [DataSourceProperty]
        public string MisgivingsTitleText
        {
            get => _misgivingsTitleText;
            set { if (value != _misgivingsTitleText) { _misgivingsTitleText = value; OnPropertyChangedWithValue(value, "MisgivingsTitleText"); } }
        }

        [DataSourceProperty]
        public string MisgivingsBodyText
        {
            get => _misgivingsBodyText;
            set { if (value != _misgivingsBodyText) { _misgivingsBodyText = value; OnPropertyChangedWithValue(value, "MisgivingsBodyText"); } }
        }

        [DataSourceProperty]
        public bool IsMisgivingsShown
        {
            get => _isMisgivingsShown;
            set { if (value != _isMisgivingsShown) { _isMisgivingsShown = value; OnPropertyChangedWithValue(value, "IsMisgivingsShown"); } }
        }

        /// <summary>The hover text — and at every stage but the first it is INSTRUCTIONS: what the
        /// road now asks of the player, and where to go for it.</summary>
        [DataSourceProperty]
        public string MisgivingsHintText => _roadPage?.Hint ?? string.Empty;

        // ------------------------------ the dev panel ------------------------------

        /// <summary>Whether the Dev button shows at all — DevMode players only.</summary>
        [DataSourceProperty]
        public bool IsDevMode => _config.DevMode;

        [DataSourceProperty]
        public bool IsDevShown
        {
            get => _isDevShown;
            set { if (value != _isDevShown) { _isDevShown = value; OnPropertyChangedWithValue(value, "IsDevShown"); } }
        }

        [DataSourceProperty]
        public string DevTitleText => string.IsNullOrEmpty(SelectedName)
            ? "Immersive AI — developer levers (choose someone first for the per-soul ones)"
            : $"Immersive AI — developer levers, acting on {SelectedName}";

        [DataSourceProperty]
        public string DevHintText =>
            "The same test levers as the face-to-face menu, without the walk over. Popups open above this screen; levers that start something async (reach-out, letter, spark) show their result as it lands.";

        // ------------------------------ the prompt editor overlay ------------------------------

        [DataSourceProperty]
        public bool IsPromptEditShown
        {
            get => _isPromptEditShown;
            set { if (value != _isPromptEditShown) { _isPromptEditShown = value; OnPropertyChangedWithValue(value, "IsPromptEditShown"); } }
        }

        [DataSourceProperty]
        public string PromptEditTitle
        {
            get => _promptEditTitle;
            set { if (value != _promptEditTitle) { _promptEditTitle = value; OnPropertyChangedWithValue(value, "PromptEditTitle"); } }
        }

        [DataSourceProperty]
        public string PromptEditText
        {
            get => _promptEditText;
            set { if (value != _promptEditText) { _promptEditText = value; OnPropertyChangedWithValue(value, "PromptEditText"); } }
        }

        [DataSourceProperty]
        public string PromptEditHint =>
            "One flowing text — write below; the whole of it stays readable here. # comment lines stay in the file, out of sight. Save speaks from the very next reply; Discard (or Escape) changes nothing.";

        [DataSourceProperty]
        public string InfoButtonText => "?";

        /// <summary>The "?" overlay: what this screen is, how it works, what to try.</summary>
        [DataSourceProperty]
        public bool IsInfoShown
        {
            get => _isInfoShown;
            set { if (value != _isInfoShown) { _isInfoShown = value; OnPropertyChangedWithValue(value, "IsInfoShown"); } }
        }

        [DataSourceProperty]
        public string InfoTitleText => "Words with those you know — how it works";

        [DataSourceProperty]
        public string InfoText =>
            "Everyone you know stands in one place here — those in the room with you and those a kingdom away — and what has passed between you reads as one story, whether it was spoken or carried by a courier.\n" +
            "\n" +
            "WHO IS LISTED\n" +
            "• (here) — they can hear you now: write, press Enter, and they answer.\n" +
            "• (away) — too far for words. The same box writes them a letter instead, and the button becomes \"Seal and send\".\n" +
            "• (gone) — they have died. Their letters stay readable; nothing more can be sent.\n" +
            "• A gold dot ● means their words are waiting for you. The line above the list searches it.\n" +
            "\n" +
            "HOW IT WORKS\n" +
            "• Enter sends a spoken line. It does NOT seal a letter — a letter deserves a deliberate press.\n" +
            "• One letter at a time between the two of you, in either direction: while a courier rides, the seal waits for word. It is a correspondence, not a shouting match.\n" +
            "• Letters appear in the thread wearing a ✉, in their place among the spoken words — one story, in the order it happened.\n" +
            "• Scroll UP past the oldest words to read exactly what they will be given when you write: everything they know, remember, and can reach for, in the order they receive it.\n" +
            "• \"Think\" (Shift+Enter) has your own character find the next line — or draft the letter — reading everything the one before you reads. \"Preset…\" keeps standing wishes to steer it.\n" +
            "• An unsent draft is kept when you close: nothing is lost.\n" +
            "• Every exchange is a real, remembered moment, and it can move their heart toward or away from you — that is the number beside their name.\n" +
            "\n" +
            "WHAT TO TRY\n" +
            "• Ask your surgeon how the wounded fare, or your scout whether you could outrun that war party.\n" +
            "• Write to a far-off companion and ask how their errand goes — one in your service will send a real field report.\n" +
            "• Tell someone what you saw on the road today, or simply ask how they slept. They remember kindness.\n" +
            "\n" +
            "An answer takes a few breaths to arrive — it is being truly thought, not picked from a list.";
    }
}
