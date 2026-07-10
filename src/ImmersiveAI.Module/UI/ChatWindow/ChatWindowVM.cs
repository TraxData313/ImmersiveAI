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

        // The line sent but not yet answered, per NPC — shown in the thread while the reply is on
        // its way (the turn is only recorded once the answer is in), and restored into the input
        // box should the sending fail.
        private readonly Dictionary<string, string> _pendingLines = new Dictionary<string, string>(StringComparer.Ordinal);

        private MBBindingList<ChatContactVM> _contacts = new MBBindingList<ChatContactVM>();
        private MBBindingList<ChatMessageVM> _messages = new MBBindingList<ChatMessageVM>();
        private ChatContactVM? _selected;
        private string _inputText = string.Empty;
        private string _selectedName = string.Empty;
        private string _overviewText = string.Empty;
        private bool _isOverviewShown = true;
        private bool _isWaiting;

        public ChatWindowVM(ModConfig config)
        {
            _config = config;
            RefreshContacts();
        }

        // ------------------------------ contacts (those near you) ------------------------------

        /// <summary>Rebuilds the left-hand list from whoever is co-located right now, keeping the
        /// current selection when that soul is still present.</summary>
        public void RefreshContacts()
        {
            var keepId = _selected?.Hero?.StringId;
            var list = new MBBindingList<ChatContactVM>();

            foreach (var info in ImmersiveChatBehavior.NearbyHeroesForChat()
                         .OrderByDescending(i => i.HasHistory)
                         .ThenByDescending(i => i.LastSpokenGameDay)
                         .ThenBy(i => i.Hero.Name?.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                var vm = new ChatContactVM(info.Hero, info.HasHistory, info.LastSpokenGameDay, info.Detail, OnContactSelected);
                vm.HasUnread = ChatWindowManager.HasUnread(info.Hero.StringId);
                list.Add(vm);
            }

            Contacts = list;

            var again = keepId == null ? null : list.FirstOrDefault(c => c.Hero.StringId == keepId);
            if (again != null) SelectContact(again);
            else if (_selected != null) { _selected = null; RefreshSelectionState(); }
        }

        private void OnContactSelected(ChatContactVM contact) => SelectContact(contact);

        /// <summary>Puts one thread on stage: loads the remembered story, builds the overview and the
        /// message list, and lets the world know the knock (if any) has been answered by a look.</summary>
        public void SelectContact(ChatContactVM contact)
        {
            if (contact == null) return;

            foreach (var c in Contacts) c.IsSelected = c == contact;
            _selected = contact;

            contact.HasUnread = false;
            ChatWindowManager.ClearUnread(contact.Hero.StringId);
            ImmersiveChatBehavior.OnChatThreadViewed(contact.Hero);

            RefreshThread();
            RefreshSelectionState();
        }

        /// <summary>Selects the thread of a given hero if they are in the list (used when the window
        /// is opened by a knock — the map notice or a toast).</summary>
        public void TrySelect(Hero hero)
        {
            if (hero == null) return;
            var contact = Contacts.FirstOrDefault(c => c.Hero == hero);
            if (contact != null) SelectContact(contact);
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
                var contact = Contacts.FirstOrDefault(c => c.Hero.StringId == heroStringId);
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

            OverviewText = BuildOverview(memory, npcName);

            if (memory != null)
            {
                foreach (var turn in memory.RecentTurns)
                {
                    var stamp = Stamp(turn);
                    if (turn.IsFromAngel)
                    {
                        // The Angel's beats are the story's stage directions — shown softly, never
                        // hidden: the same recorded stream her own prompt replays.
                        messages.Add(new ChatMessageVM(string.Empty,
                            WithStamp(stamp, $"{voice}, softly into {npcName}'s mind: {turn.PlayerLine}"),
                            isNarration: true, Colors.White));
                    }
                    else
                    {
                        messages.Add(new ChatMessageVM(WithStamp(stamp, playerName),
                            turn.PlayerLine, isNarration: false, PlayerHeaderColor));
                    }

                    if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                        messages.Add(new ChatMessageVM(npcName, turn.NpcLine, isNarration: false, NpcHeaderColor));
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
                    messages.Add(new ChatMessageVM(playerName, pendingLine, isNarration: false, PlayerHeaderColor));
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

        /// <summary>Where the thread begins vertically: right under the header, or under the unfolded
        /// overview block. Bound as the thread's top margin so the layout reflows with the toggle.</summary>
        [DataSourceProperty]
        public float MessagesTopMargin => ShowOverviewBlock ? 238f : 50f;

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
                    _inputText = value ?? string.Empty;
                    OnPropertyChangedWithValue(value, "InputText");
                    OnPropertyChanged("CanSend");
                    OnPropertyChanged("HasDraft");
                    OnPropertyChanged("MessagesBottomMargin");
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
        public bool CanSend => HasSelection && !_isWaiting && !string.IsNullOrWhiteSpace(_inputText);

        [DataSourceProperty]
        public bool IsWaiting
        {
            get => _isWaiting;
            set { if (value != _isWaiting) { _isWaiting = value; OnPropertyChangedWithValue(value, "IsWaiting"); OnPropertyChanged("CanSend"); } }
        }
    }
}
