using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Memory;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.ChatWindow
{
    /// <summary>
    /// The chat window: everyone in the same place as the player on the left, the shared story with
    /// whoever is chosen on the right — their deep memory of the player as an overview up top (so a
    /// long history needs no endless scrolling), the recent exchanges as a readable thread, and a
    /// place to simply write to them first. No ceremony stands between the player and a companion:
    /// "how are our stocks?" is one hotkey, a line, and Send. Replies land in the thread when they
    /// come; closing the window loses nothing — every word is a recorded turn in her memory.
    /// </summary>
    public class ChatWindowVM : ViewModel
    {
        // Header tints: the player's words in warm parchment-gold, the NPC's in the same soft
        // sea-glass as the activity notices — read at a glance without bubbles.
        private static readonly Color PlayerHeaderColor = new Color(0.85f, 0.75f, 0.55f, 1f);
        private static readonly Color NpcHeaderColor = new Color(0.74f, 0.90f, 0.86f, 1f);
        // The "since you were last alone" footnote: the nights' own dusk-violet, so it reads as a
        // margin note about the two of you rather than as anyone's spoken words.
        private static readonly Color LineHeaderColor = new Color(0.72f, 0.70f, 0.92f, 1f);
        // The wedding accounts wear the courtship road's own rose, so the day reads as the end of
        // that story rather than as one more exchange.
        private static readonly Color WeddingHeaderColor = new Color(0.93f, 0.62f, 0.72f, 1f);

        private readonly ModConfig _config;
        private readonly string _letterHotkey;
        private readonly string _chatHotkey;

        // The line sent but not yet answered, per NPC — shown in the thread while the reply is on
        // its way (the turn is only recorded once the answer is in), and restored into the input
        // box should the sending fail.
        private readonly Dictionary<string, string> _pendingLines = new Dictionary<string, string>(StringComparer.Ordinal);

        // Every soul the window knows of, unfiltered — Contacts is the searched VIEW over this.
        private readonly List<ChatContactVM> _allContacts = new List<ChatContactVM>();

        private MBBindingList<ChatContactVM> _contacts = new MBBindingList<ChatContactVM>();
        private MBBindingList<ChatMessageVM> _messages = new MBBindingList<ChatMessageVM>();
        private ChatContactVM? _selected;

        /// <summary>Whoever's thread is on stage — the manager stamps their talk as ended when the
        /// window closes, so the "since we were last alone" line moves only once you have walked
        /// away (see the behavior's Nights partial).</summary>
        internal Hero? SelectedHero => _selected?.Hero;
        private string _inputText = string.Empty;
        private string _searchText = string.Empty;
        private string _selectedName = string.Empty;
        private string _relationText = string.Empty;
        private string _bondStatsText = string.Empty;
        private string _memoryLoadText = string.Empty;
        private Color _relationColor = Colors.White;
        private string _overviewText = string.Empty;
        private string _overviewTitle = string.Empty;
        // Folded until asked for (Anton's call, 2026.08.08): the deep-memory block only exists in
        // DevMode, and unfolded by default it ate the top of the thread on every open. Deliberately
        // NOT remembered between openings — every window starts folded.
        private bool _isOverviewShown;
        private bool _isInfoShown;
        private bool _isPromptEditShown;
        private string _promptEditTitle = string.Empty;
        private string _promptEditText = string.Empty;
        private bool _isWaiting;
        private bool _isMisgivingsShown;
        // Whether the little page under the name is the WEDDING (wed) or the misgivings (courting).
        private bool _pageIsWedding;
        // The whole road stage this button currently stands at — it decides both the hover text
        // and what a click actually does.
        private ImmersiveChatBehavior.RoadPage? _roadPage;
        private string _misgivingsButtonText = string.Empty;
        private string _misgivingsTitleText = string.Empty;
        private string _misgivingsBodyText = string.Empty;
        private bool _isDevShown;

        public ChatWindowVM(ModConfig config)
        {
            _config = config;
            _letterHotkey = string.IsNullOrWhiteSpace(config.LetterWindowHotkey) ? "Y" : config.LetterWindowHotkey.Trim();
            _chatHotkey = string.IsNullOrWhiteSpace(config.ChatWindowHotkey) ? "O" : config.ChatWindowHotkey.Trim();
            RefreshContacts();
        }

        // ------------------------------ contacts (those near you) ------------------------------

        /// <summary>Rebuilds the left-hand list from whoever is co-located right now, keeping the
        /// current selection when that soul is still present.</summary>
        public void RefreshContacts()
        {
            var keepId = _selected?.Hero?.StringId;
            _allContacts.Clear();

            foreach (var info in ImmersiveChatBehavior.NearbyHeroesForChat()
                         .OrderByDescending(i => i.IsHere)
                         .ThenByDescending(i => i.HasHistory)
                         .ThenByDescending(i => i.LastSpokenGameDay)
                         .ThenBy(i => i.Hero.Name?.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                var vm = new ChatContactVM(info.Hero, info.HasHistory, info.LastSpokenGameDay, info.Detail, info.IsHere, OnContactSelected);
                vm.HasUnread = ChatWindowManager.HasUnread(info.Hero.StringId);
                _allContacts.Add(vm);
            }

            ApplyContactFilter();

            var again = keepId == null ? null : _allContacts.FirstOrDefault(c => c.Hero.StringId == keepId);
            if (again != null) SelectContact(again);
            else if (_selected != null) { _selected = null; RefreshSelectionState(); }
        }

        // The search line above the list: a plain name-or-detail contains, so "scout", "Sargot",
        // or half a name all find their soul. The selection is a thing apart from the view — a
        // filtered-out selected thread stays on stage, only its row steps out of the list.
        private void ApplyContactFilter()
        {
            var q = (_searchText ?? string.Empty).Trim();
            var list = new MBBindingList<ChatContactVM>();
            foreach (var c in _allContacts)
                if (q.Length == 0 || MatchesSearch(c.Name, q) || MatchesSearch(c.Detail, q))
                    list.Add(c);
            Contacts = list;
        }

        private static bool MatchesSearch(string? text, string q) =>
            text != null && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnContactSelected(ChatContactVM contact) => SelectContact(contact);

        /// <summary>Puts one thread on stage: loads the remembered story, builds the overview and the
        /// message list, and lets the world know the knock (if any) has been answered by a look.</summary>
        public void SelectContact(ChatContactVM contact)
        {
            if (contact == null) return;

            foreach (var c in _allContacts) c.IsSelected = c == contact;
            _selected = contact;

            contact.HasUnread = false;
            ChatWindowManager.ClearUnread(contact.Hero.StringId);
            ImmersiveChatBehavior.OnChatThreadViewed(contact.Hero);

            RefreshThread();
            RefreshSelectionState();

            // Bring back whatever was being composed to this one before the window was last closed.
            InputText = ChatWindowManager.GetDraft(contact.Hero.StringId);
        }

        /// <summary>Selects the thread of a given hero if they are in the list (used when the window
        /// is opened by a knock — the map notice or a toast).</summary>
        public void TrySelect(Hero hero)
        {
            if (hero == null) return;
            var contact = _allContacts.FirstOrDefault(c => c.Hero == hero);
            if (contact == null) return;
            if (!Contacts.Contains(contact)) SearchText = string.Empty; // the knock outranks a stale filter
            SelectContact(contact);
        }

        /// <summary>Called when a thread changed underneath the window (a reply arrived, or an NPC's
        /// first word landed): refresh what is on stage, or mark the knock unread in the list.</summary>
        public void OnThreadChanged(string heroStringId)
        {
            if (_selected != null && _selected.Hero.StringId == heroStringId)
            {
                RefreshThread();
                RefreshSelectionState();
            }
            else
            {
                var contact = _allContacts.FirstOrDefault(c => c.Hero.StringId == heroStringId);
                if (contact != null) contact.HasUnread = true;
                else RefreshContacts(); // someone new stepped into range with their first word
            }
        }

        /// <summary>The sending failed (the words were never recorded): put them back into the input
        /// box so nothing the player wrote is lost.</summary>
        public void OnSendFailed(string heroStringId, string text)
        {
            _pendingLines.Remove(heroStringId);
            if (_selected != null && _selected.Hero.StringId == heroStringId && string.IsNullOrEmpty(_inputText))
                InputText = text ?? string.Empty;
            OnThreadChanged(heroStringId);
        }

        // ------------------------------ the thread on stage ------------------------------

        private void RefreshThread()
        {
            var messages = new MBBindingList<ChatMessageVM>();
            var npc = _selected?.Hero;
            if (npc == null) { Messages = messages; OverviewText = string.Empty; MemoryLoadText = string.Empty; return; }

            var memory = ImmersiveChatBehavior.PeekMemoryFor(npc);
            var npcName = npc.Name?.ToString() ?? "They";

            // How heavy their verbatim memory of you has grown against the thresholds that fold it
            // into the summary — built here, where the memory is already in hand (no second read).
            MemoryLoadText = memory == null
                ? string.Empty
                : MemoryTokenProfile.MemoryLoadLabel(_config, memory, CampaignTime.Now.ToDays);

            var playerName = Hero.MainHero?.Name?.ToString() ?? "You";
            var voice = string.IsNullOrWhiteSpace(_config.SystemVoiceName) ? "Angel" : _config.SystemVoiceName.Trim();

            // Her deep memory laid bare is a developer's view (DevMode); players meet what she
            // remembers the way people do — through what she says. No overview text means the
            // whole page and its toggle stay off stage (HasOverview keys off this). The heading
            // rides apart from the body: the overlay wears it as its own title.
            OverviewTitleText = _config.DevMode ? OverviewHeading(memory, npcName) : string.Empty;
            OverviewText = _config.DevMode ? BuildOverview(memory) : string.Empty;

            // THE LINE stands where it belongs — at the moment, not at the end (Anton's screenshot,
            // 2026.08.10). "From this moment until now" is meaningless nailed to the foot of the
            // thread: it goes in immediately after the last exchange the two of them actually had
            // alone, with everything that came after it below. -1 means there is no such moment,
            // and then it simply never appears.
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
                        // Letter beats wear their letters openly (Anton's ask, 2026.07.10): the
                        // moment she wrote to the player, or the player's letter reaching her
                        // hands, shows as a letter card in its place in the thread — instead of
                        // the raw quill narration. Recognized across both eras: the retired Angel's
                        // recorded beats (pre-2026.08.07 saves) and the first-person inner ones.
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

                        // A night of the marriage wears its own card too (2026.08.10). The beat in
                        // her memory holds only the NAME — the account itself lives in the night
                        // ledger — so the card is filled from there, and only for the freshest few:
                        // older nights keep their name and nothing more, which is how memory works
                        // anyway. Same shape as the wedding's cards, in the nights' own violet.
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

                        // The wedding day, and the night that followed it, wear their own cards
                        // (2026.08.09): the written account is a thing to be READ, not a stage
                        // direction — so the framing line stays soft narration and the account
                        // itself is drawn as its own block. The night's card says whose it is.
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
                        AddSpoken(messages, WithStamp(stamp, playerName),
                            turn.PlayerLine, PlayerHeaderColor);
                    }

                    if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                        AddSpoken(messages, npcName, turn.NpcLine, NpcHeaderColor);
                }
            }

            // A line already sent but not yet answered is not a recorded turn yet — show it, and her
            // considering, so the wait is never a blank. The considering note lives ONLY here in the
            // thread (a second copy under the input box showed the same words twice — QA'd out,
            // 2026.07.10). Shown even when the sent line itself is unknown (window reopened mid-reply:
            // the draft dict dies with the old VM, her considering does not).
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
            ChatWindowManager.RequestScrollToBottom();
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
        // grammar — EmoteText): the words draw as the spoken card, each gesture as a soft
        // narration line in its place, so actions look like actions and words like words. The
        // header rides the first segment whatever it is — a reply that is all gesture still
        // says whose act it was.
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

        // The deep-memory overview: what she carries of the player beyond the verbatim thread — the
        // rolling memory she rewrites whole at every reflection — so a long story is readable at a
        // glance. (A second block of distilled "truths" stood here until 2026.08.08.)
        private static string BuildOverview(NpcMemory? memory)
            => memory == null || string.IsNullOrWhiteSpace(memory.Summary) ? string.Empty : memory.Summary.Trim();

        private static string OverviewHeading(NpcMemory? memory, string npcName)
        {
            if (memory == null || string.IsNullOrWhiteSpace(memory.Summary)) return string.Empty;
            // A memory holding only the seeded story of their own road is not yet a memory OF the
            // player — head it as the story they carry, not as what lingers of you.
            if (memory.SeededFromStory && memory.StoryRichness == 0)
                return $"The story {npcName} carries of their own road";
            var asOf = string.IsNullOrWhiteSpace(memory.SummaryAsOf) ? string.Empty : $" (as of {memory.SummaryAsOf.Trim()})";
            return $"What lingers in {npcName}'s memory of you{asOf}";
        }

        private void RefreshSelectionState()
        {
            SelectedName = _selected?.Hero?.Name?.ToString() ?? string.Empty;
            OnPropertyChanged("HasSelection");
            OnPropertyChanged("DevTitleText");
            OnPropertyChanged("HasOverview");
            OnOverviewLayoutChanged();
            IsWaiting = _selected != null && ImmersiveChatBehavior.IsQuickChatBusy(_selected.Hero);
            OnPropertyChanged("CanSend");
            OnPropertyChanged("IsAway");
            OnPropertyChanged("AwayNotice");

            _relationText = _selected == null ? string.Empty : ImmersiveChatBehavior.RelationLabel(_selected.Hero);
            _relationColor = RelationTint(_selected == null ? 0 : ImmersiveChatBehavior.RelationValue(_selected.Hero));
            OnPropertyChanged("RelationText");
            OnPropertyChanged("HasRelation");
            OnPropertyChanged("RelationColor");

            BondStatsText = _selected == null ? string.Empty : ImmersiveChatBehavior.BondStatsLabel(_selected.Hero);

            // One little button under the name, carrying whatever this bond's own page is right now
            // (Anton, 2026.08.08 and 2026.08.09): the misgivings while the road is walked — and once
            // you are wed, the WEDDING DAY takes its place, because the doubts are answered and the
            // day is what remains. Wed souls open theirs forever; it never goes away.
            // …and since 2026.08.09 it walks the WHOLE road, not just its two ends: her misgivings,
            // then her kin's blessing to be sought, then the days of preparation counting down,
            // then the wedding itself — each stage naming what to do next in its hover text.
            var page = _selected != null ? ImmersiveChatBehavior.RoadPageFor(_selected.Hero) : null;
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

        // Warm green when they hold you dear, cool red when they do not, plain parchment at neutral.
        private static Color RelationTint(int relation)
        {
            if (relation > 0) return new Color(0.55f, 0.82f, 0.55f, 1f);
            if (relation < 0) return new Color(0.86f, 0.53f, 0.49f, 1f);
            return new Color(0.78f, 0.75f, 0.68f, 1f);
        }

        // ------------------------------ speaking ------------------------------

        public void ExecuteSend()
        {
            var npc = _selected?.Hero;
            var text = (_inputText ?? string.Empty).Trim();
            if (npc == null || text.Length == 0 || IsWaiting) return;

            if (!ImmersiveChatBehavior.SendQuickChat(npc, text)) return;

            _pendingLines[npc.StringId] = text;
            InputText = string.Empty;
            RefreshThread();
            RefreshSelectionState();
        }

        public void ExecuteClose() => ChatWindowManager.Close();

        public void ExecuteToggleOverview() => IsOverviewShown = !IsOverviewShown;

        public void ExecuteToggleInfo() => IsInfoShown = !IsInfoShown;

        /// <summary>The way back out of whichever page is up — the same order Escape folds them in.
        /// Every overlay wears it as a button, because "X" closes the whole window and nothing else
        /// said how to step back (Anton, 2026.08.08).</summary>
        public void ExecuteBack()
        {
            if (IsPromptEditShown) ExecutePromptCancel();
            else if (IsDevShown) IsDevShown = false;
            else if (IsMisgivingsShown) IsMisgivingsShown = false;
            else if (IsOverviewShown) IsOverviewShown = false;
            else if (IsInfoShown) IsInfoShown = false;
        }

        // ------------------------------ the in-game prompt editor ------------------------------
        // The editing doors, IN the game now (Anton's ask — no Notepad, no alt-tab): an overlay
        // with the letters-composer shape — a tall wrapped mirror of the whole text, a single
        // writing line beneath (Gauntlet inputs cannot hold newlines), Save and Discard. Prompts
        // are re-read on every reply, so a saved edit speaks from the very next answer.

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

        // ------------------------------ the misgivings overlay ------------------------------

        // The misgivings read fine inside the window's own overlay; the wedding is long and meant to
        // be READ, so it opens in the same paused popup the day itself came in — the player asked
        // to be able to sit with it (2026.08.09).
        /// <summary>Recomputes just the road button — called when a game day turns under an open
        /// window, so a countdown counts down where the player is looking.</summary>
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
        // The DevMode levers, launched from where Anton already stands (his ask, 2026.08.08):
        // each closes the panel first so the popup it opens is not buried under the overlay.

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

        // The panel is deliberately SHORT (Anton, 2026.08.10): the eight levers he never reaches
        // for — reroll a spark, force a reach-out, force a letter, forge a battle, spend a night,
        // rewrite the wedding, rename, the odds view — are gone from it. Every one of them still
        // lives on the face-to-face devmode menu; this is the panel he actually works in, and a
        // panel of thirteen buttons is a panel you stop reading.

        // ------------------------------ bound properties ------------------------------

        [DataSourceProperty]
        public MBBindingList<ChatContactVM> Contacts
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
        public string TitleText => "Those near you";

        [DataSourceProperty]
        public string EmptyHint => "Choose someone near you, and simply speak.";

        [DataSourceProperty]
        public string SendText => "Send";

        [DataSourceProperty]
        public string OverviewToggleText => "Deep memory";

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

        /// <summary>The bond's own mechanics under the name — shared story, freshness, and the hourly
        /// chance they are moved to come (the odds view's numbers for this one soul).</summary>
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
                    OnOverviewLayoutChanged();
                }
            }
        }

        [DataSourceProperty]
        public bool HasBondStats => !string.IsNullOrEmpty(_bondStatsText);

        /// <summary>The weight of what they keep verbatim of you, against the thresholds where it is
        /// folded into the rolling summary: the share of the model's context window, the tokens, the
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
                    OnOverviewLayoutChanged();
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

        [DataSourceProperty]
        public string OverviewText
        {
            get => _overviewText;
            set
            {
                if (value != _overviewText)
                {
                    _overviewText = value;
                    OnPropertyChangedWithValue(value, "OverviewText");
                    OnPropertyChanged("HasOverview");
                    OnOverviewLayoutChanged();
                }
            }
        }

        [DataSourceProperty]
        public string OverviewTitleText
        {
            get => _overviewTitle;
            set { if (value != _overviewTitle) { _overviewTitle = value; OnPropertyChangedWithValue(value, "OverviewTitleText"); } }
        }

        [DataSourceProperty]
        public string OverviewHintText =>
            "The one rolling memory they carry of you — rewritten whole whenever they gather their thoughts, and read back to them at every exchange. A developer's view; the words themselves are theirs.";

        /// <summary>The way out of every overlay, worn as a button on each of them.</summary>
        [DataSourceProperty]
        public string BackText => "← Back  (Esc)";

        /// <summary>The same corner button on the prompt editors — named honestly, because stepping
        /// back from a half-written prompt throws the edit away (Save is the door that keeps it).</summary>
        [DataSourceProperty]
        public string BackDiscardText => "← Back  (discards)";

        [DataSourceProperty]
        public bool HasOverview => HasSelection && !string.IsNullOrWhiteSpace(_overviewText);

        [DataSourceProperty]
        public bool IsOverviewShown
        {
            get => _isOverviewShown;
            set
            {
                if (value != _isOverviewShown)
                {
                    _isOverviewShown = value;
                    OnPropertyChangedWithValue(value, "IsOverviewShown");
                    OnOverviewLayoutChanged();
                }
            }
        }

        /// <summary>Whether the deep-memory page is up right now (it exists AND was asked for).</summary>
        [DataSourceProperty]
        public bool ShowOverviewBlock => HasOverview && _isOverviewShown;

        // The two grey lines under the name (the bond's mechanics, the memory's weight) stack in a
        // ListPanel, so they can never land on each other — but everything BELOW them is placed by
        // margin, and Gauntlet will not tell us how tall a wrapped line came out. So we estimate it
        // from the strings themselves and always round UP: a little air costs nothing, an overlap is
        // the bug (Anton's screenshot, 2026.08.08 — a wrapped bond line sat on the memory line).
        private const float GreyBlockTop = 118f;   // where the grey block starts: buttons row, then name
        private const float GreyLineHeight = 20f;  // one rendered line of the 14pt grey text
        private const int GreyLineChars = 135;     // roughly what fits across the pane at that size

        private static int WrappedLines(string text)
            => string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (text.Length + GreyLineChars - 1) / GreyLineChars);

        /// <summary>Where the thread begins vertically — right under the header's grey lines, their
        /// wrapping counted. (The deep memory is its own page now, so nothing else pushes it down.)</summary>
        [DataSourceProperty]
        public float MessagesTopMargin =>
            GreyBlockTop + GreyLineHeight * (WrappedLines(_bondStatsText) + WrappedLines(_memoryLoadText)) + 12f;

        private void OnOverviewLayoutChanged()
        {
            OnPropertyChanged("ShowOverviewBlock");
            OnPropertyChanged("MessagesTopMargin");
            ChatWindowManager.RequestScrollToBottom();
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
                    // re-pin to the newest line so the NPC's last words stay in view above the box
                    // (Anton's ask, 2026.07.11 — the mirror used to cover them until you scrolled).
                    bool draftBefore = !string.IsNullOrWhiteSpace(_inputText);
                    _inputText = value ?? string.Empty;
                    bool draftAfter = !string.IsNullOrWhiteSpace(_inputText);

                    OnPropertyChangedWithValue(value, "InputText");
                    OnPropertyChanged("CanSend");
                    OnPropertyChanged("HasDraft");
                    OnPropertyChanged("MessagesBottomMargin");

                    if (draftBefore != draftAfter)
                        ChatWindowManager.RequestScrollToBottom();

                    if (_selected != null)
                        ChatWindowManager.SetDraft(_selected.Hero.StringId, _inputText);
                }
            }
        }

        /// <summary>Whether something is being composed — shows the wrapped draft mirror above the
        /// input line (the engine's editable text is single-line; the mirror is where a long message
        /// stays readable while it is written).</summary>
        [DataSourceProperty]
        public bool HasDraft => !string.IsNullOrWhiteSpace(_inputText);

        /// <summary>Where the thread ends vertically: just above the input line, or above the draft
        /// mirror while one is being written. Bound as the thread's bottom margin.</summary>
        [DataSourceProperty]
        public float MessagesBottomMargin => HasDraft ? 170f : 82f;

        [DataSourceProperty]
        public bool CanSend => HasSelection && (_selected?.IsHere ?? false) && !_isWaiting && !string.IsNullOrWhiteSpace(_inputText);

        /// <summary>Whether the chosen one is away across the map — spoken words cannot reach them, so
        /// the send is grayed and a gentle note points to a letter instead.</summary>
        [DataSourceProperty]
        public bool IsAway => HasSelection && !(_selected?.IsHere ?? false);

        [DataSourceProperty]
        public string AwayNotice =>
            IsAway ? $"{SelectedName} is far from you now — send a letter (press {_letterHotkey}) to reach them." : string.Empty;

        [DataSourceProperty]
        public bool IsWaiting
        {
            get => _isWaiting;
            set { if (value != _isWaiting) { _isWaiting = value; OnPropertyChangedWithValue(value, "IsWaiting"); OnPropertyChanged("CanSend"); } }
        }

        // ------------------------------ the info overlay ------------------------------

        /// <summary>The "?" overlay: what this window is, how it works, what to try. Escape folds
        /// it away before closing the window (the manager checks this flag first).</summary>
        [DataSourceProperty]
        public bool IsInfoShown
        {
            get => _isInfoShown;
            set { if (value != _isInfoShown) { _isInfoShown = value; OnPropertyChangedWithValue(value, "IsInfoShown"); } }
        }

        // ------------------------------ the misgivings overlay ------------------------------

        /// <summary>Whether the chosen one has a misgivings view to show at all (a courtship road
        /// is walked, or something was once written) — keys the little button's visibility.</summary>
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

        /// <summary>The misgivings overlay — folded by Escape before the info overlay (manager).</summary>
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

        /// <summary>The dev panel overlay — folded by Escape first of the informational overlays.</summary>
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
            "The same test levers as the face-to-face menu, without the walk over. Popups open above this window; levers that start something async (reach-out, letter, spark) show their result as it lands.";

        // ------------------------------ the prompt editor overlay ------------------------------

        /// <summary>The in-game prompt editor (Their prompt / World prompt). Escape discards it
        /// before the info overlay and before closing (the manager checks this flag first of all);
        /// Enter never sends the chat line while it is up.</summary>
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

        [DataSourceProperty]
        public string InfoTitleText => "Words with those near you — how it works";

        [DataSourceProperty]
        public string InfoText =>
            "This window is for quick words with those who share your road — no ceremony, no scene: choose a face, write, and send.\n" +
            $"Open it anywhere on the map with [{_chatHotkey}], with \"Speak with those near you\" in a town, castle, or village — or by answering someone's knock.\n" +
            "\n" +
            "WHO IS LISTED\n" +
            "• Everyone in the same place as you — your own party, and the folk of the settlement you stand in — plus everyone you already hold a story with, wherever they are.\n" +
            "• (here) — they can hear you now. (away) — they are far across the map; spoken words cannot reach them, so the Send stays gray. A letter can: press [" + _letterHotkey + "].\n" +
            "• A gold dot ● means their words are waiting for you.\n" +
            "• The line above the list searches it — type part of a name (or of the note under one).\n" +
            "• Under a chosen name: how much story you share, how fresh it is, and the hour's chance they are moved to come to you (or, away, to write).\n" +
            "\n" +
            "HOW IT WORKS\n" +
            "• Enter sends; Escape closes. An unsent draft is kept — closing the window loses nothing.\n" +
            "• Every exchange is a real, remembered moment: they will carry it, and it can move their heart toward or away from you — that is the number beside their name.\n" +
            "• The soft gray lines are the story's stage directions; nothing they remember of you is hidden.\n" +
            "• Letters between you appear as ✉ cards in their place in the thread; a letter still on the road stays sealed until it arrives.\n" +
            "• With the SOCIALNESS dial above zero (lower-right of the map), people may seek you out on their own — their greeting waits here unread.\n" +
            "\n" +
            "WHAT TO TRY\n" +
            "• Ask your surgeon how the wounded fare, or your scout whether you could outrun that war party to the east.\n" +
            "• Ask a merchant what grain fetches in this town, or a lord what he makes of the war.\n" +
            "• Tell someone what you saw on the road today — or simply ask how they slept. They remember kindness.\n" +
            "\n" +
            "An answer takes a few breaths to arrive — it is being truly thought, not picked from a list.";
    }
}
