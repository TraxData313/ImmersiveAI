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
        private string _inputText = string.Empty;
        private string _searchText = string.Empty;
        private string _selectedName = string.Empty;
        private string _relationText = string.Empty;
        private string _bondStatsText = string.Empty;
        private Color _relationColor = Colors.White;
        private string _overviewText = string.Empty;
        private bool _isOverviewShown = true;
        private bool _isInfoShown;
        private bool _isWaiting;

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
            if (npc == null) { Messages = messages; OverviewText = string.Empty; return; }

            var memory = ImmersiveChatBehavior.PeekMemoryFor(npc);
            var npcName = npc.Name?.ToString() ?? "They";
            var playerName = Hero.MainHero?.Name?.ToString() ?? "You";
            var voice = string.IsNullOrWhiteSpace(_config.SystemVoiceName) ? "Angel" : _config.SystemVoiceName.Trim();

            // Her deep memory laid bare is a developer's view (DevMode); players meet what she
            // remembers the way people do — through what she says. No overview text means the
            // whole block and its toggle stay off stage (HasOverview keys off this).
            OverviewText = _config.DevMode ? BuildOverview(memory, npcName) : string.Empty;

            if (memory != null)
            {
                foreach (var turn in memory.RecentTurns)
                {
                    var stamp = Stamp(turn);
                    if (turn.IsFromAngel)
                    {
                        // Letter beats wear their letters openly (Anton's ask, 2026.07.10): the
                        // moment she wrote to the player, or the player's letter reaching her
                        // hands, shows as a letter card in its place in the thread — instead of
                        // the Angel's raw quill-instruction narration. Everything else the Angel
                        // spoke stays as soft stage directions, never hidden.
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

                        // The Angel's beats are the story's stage directions — shown softly, never
                        // hidden: the same recorded stream her own prompt replays.
                        messages.Add(new ChatMessageVM(string.Empty,
                            WithStamp(stamp, $"{voice}, softly into {npcName}'s mind: {turn.PlayerLine}"),
                            isNarration: true, Colors.White));
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
        // rolling summary and the truths she chose to hold — so a long story is readable at a glance.
        private static string BuildOverview(NpcMemory? memory, string npcName)
        {
            if (memory == null ||
                (string.IsNullOrWhiteSpace(memory.Summary) && memory.KnownFacts.Count == 0))
                return string.Empty;

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                var asOf = string.IsNullOrWhiteSpace(memory.SummaryAsOf) ? string.Empty : $" (as of {memory.SummaryAsOf.Trim()})";
                sb.AppendLine($"What lingers in {npcName}'s memory of you{asOf}:");
                sb.AppendLine(memory.Summary.Trim());
            }
            if (memory.KnownFacts.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("The truths they hold about you:");
                foreach (var fact in memory.KnownFacts)
                    sb.AppendLine("• " + fact);
            }
            return sb.ToString().TrimEnd();
        }

        private void RefreshSelectionState()
        {
            SelectedName = _selected?.Hero?.Name?.ToString() ?? string.Empty;
            OnPropertyChanged("HasSelection");
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
                }
            }
        }

        [DataSourceProperty]
        public bool HasBondStats => !string.IsNullOrEmpty(_bondStatsText);

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

        /// <summary>Whether the overview block occupies its place right now (it exists AND is unfolded).</summary>
        [DataSourceProperty]
        public bool ShowOverviewBlock => HasOverview && _isOverviewShown;

        /// <summary>Where the thread begins vertically: under the header and its bond line, or under
        /// the unfolded overview block. Bound as the thread's top margin so the layout reflows.</summary>
        [DataSourceProperty]
        public float MessagesTopMargin => ShowOverviewBlock ? 256f : 68f;

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
