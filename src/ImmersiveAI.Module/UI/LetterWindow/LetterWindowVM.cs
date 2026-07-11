using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using ImmersiveAI.UI.ChatWindow;

namespace ImmersiveAI.UI.LetterWindow
{
    /// <summary>
    /// The letter window: every correspondent on the left (existing letters first, then the
    /// freshest bonds), the whole correspondence with whoever is chosen on the right — each letter
    /// a card with its writing time and provenance, the asides ("read and let lie unanswered") as
    /// soft narration, a courier on the road shown at the end — and a place to write the next
    /// letter with the story open before your eyes. A pure VIEW over letters.txt and the letter
    /// bag: closing it loses nothing. The writing itself takes the same road as the courier menu.
    /// </summary>
    public class LetterWindowVM : ViewModel
    {
        // The same tints as the chat window: the player's words parchment-gold, theirs sea-glass.
        private static readonly Color PlayerHeaderColor = new Color(0.85f, 0.75f, 0.55f, 1f);
        private static readonly Color NpcHeaderColor = new Color(0.74f, 0.90f, 0.86f, 1f);

        private MBBindingList<LetterContactVM> _contacts = new MBBindingList<LetterContactVM>();
        private MBBindingList<ChatMessageVM> _entries = new MBBindingList<ChatMessageVM>();
        private LetterContactVM? _selected;
        private string _inputText = string.Empty;
        private string _selectedName = string.Empty;
        private string _statusText = string.Empty;
        private bool _canWrite;

        public LetterWindowVM()
        {
            RefreshContacts();
        }

        // ------------------------------ correspondents ------------------------------

        public void RefreshContacts()
        {
            var keepFolder = _selected?.Folder;
            var list = new MBBindingList<LetterContactVM>();

            foreach (var info in ImmersiveChatBehavior.CorrespondentsForLetters()
                         .OrderByDescending(i => i.HasLetters)
                         .ThenByDescending(i => i.LastSpokenGameDay)
                         .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                list.Add(new LetterContactVM(info, OnContactSelected));

            Contacts = list;

            var again = keepFolder == null ? null : list.FirstOrDefault(c => c.Folder == keepFolder);
            if (again != null) SelectContact(again);
            else if (_selected != null) { _selected = null; RefreshSelectionState(); }
        }

        private void OnContactSelected(LetterContactVM contact) => SelectContact(contact);

        public void SelectContact(LetterContactVM contact)
        {
            if (contact == null) return;

            foreach (var c in Contacts) c.IsSelected = c == contact;
            _selected = contact;

            RefreshCorrespondence();
            RefreshSelectionState();
        }

        /// <summary>Puts a given hero's correspondence on stage (used by "Write back" on an arrival).</summary>
        public void TrySelect(Hero hero)
        {
            if (hero == null) return;
            var contact = Contacts.FirstOrDefault(c => c.Hero == hero);
            if (contact != null) SelectContact(contact);
        }

        // ------------------------------ the correspondence on stage ------------------------------

        private void RefreshCorrespondence()
        {
            var entries = new MBBindingList<ChatMessageVM>();
            var contact = _selected;
            if (contact == null) { Entries = entries; return; }

            var npcName = contact.Name;
            var playerName = Hero.MainHero?.Name?.ToString() ?? "You";

            foreach (var entry in ImmersiveChatBehavior.CorrespondenceEntriesFor(contact.Folder))
            {
                if (entry.IsNote)
                {
                    entries.Add(new ChatMessageVM(string.Empty,
                        WithStamp(entry.Stamp, $"({entry.Body})"), isNarration: true, Colors.White));
                    continue;
                }

                bool fromThem = string.Equals(entry.FromName, npcName, StringComparison.Ordinal)
                                || !string.Equals(entry.FromName, playerName, StringComparison.Ordinal)
                                   && !string.Equals(entry.ToName, npcName, StringComparison.Ordinal);
                var provenance = string.IsNullOrEmpty(entry.Detail) ? string.Empty : $"  ({entry.Detail})";
                entries.Add(new ChatMessageVM(
                    WithStamp(entry.Stamp, $"✉ {entry.FromName}{provenance}"),
                    entry.Body,
                    isNarration: false,
                    fromThem ? NpcHeaderColor : PlayerHeaderColor));
            }

            // A courier still on the road closes the page — the letter is a promise underway.
            var riding = contact.Hero == null
                ? string.Empty
                : ImmersiveChatBehavior.Current?.CourierStatusFor(contact.Hero.StringId) ?? string.Empty;
            if (!string.IsNullOrEmpty(riding))
                entries.Add(new ChatMessageVM(string.Empty, $"({riding})", isNarration: true, Colors.White));

            if (entries.Count == 0)
                entries.Add(new ChatMessageVM(string.Empty,
                    $"(No letters have yet passed between you and {npcName} — yours would be the first.)",
                    isNarration: true, Colors.White));

            Entries = entries;
            LetterWindowManager.RequestScrollToBottom();
        }

        private static string WithStamp(string stamp, string text) =>
            string.IsNullOrEmpty(stamp) ? text : $"[{stamp}]  {text}";

        private void RefreshSelectionState()
        {
            SelectedName = _selected?.Name ?? string.Empty;

            if (_selected == null)
            {
                _canWrite = false;
                StatusText = string.Empty;
            }
            else
            {
                _canWrite = ImmersiveChatBehavior.CanWriteTo(_selected.Hero, out var reason);
                StatusText = _canWrite
                    ? "The road is open — a courier stands ready."
                    : reason;
            }

            OnPropertyChanged("HasSelection");
            OnPropertyChanged("CanWrite");
            OnPropertyChanged("CanSend");
        }

        // ------------------------------ writing ------------------------------

        public void ExecuteSend()
        {
            var npc = _selected?.Hero;
            var text = (_inputText ?? string.Empty).Trim();
            if (npc == null || text.Length == 0 || !_canWrite) return;

            if (!ImmersiveChatBehavior.SendLetterFromWindow(npc, text)) { RefreshSelectionState(); return; }

            InputText = string.Empty;
            RefreshCorrespondence();   // the new letter is already in the log, the courier at its end
            RefreshSelectionState();   // the road is now taken until it arrives
        }

        public void ExecuteClose() => LetterWindowManager.Close();

        // ------------------------------ bound properties ------------------------------

        [DataSourceProperty]
        public MBBindingList<LetterContactVM> Contacts
        {
            get => _contacts;
            set { if (value != _contacts) { _contacts = value; OnPropertyChangedWithValue(value, "Contacts"); } }
        }

        [DataSourceProperty]
        public MBBindingList<ChatMessageVM> Entries
        {
            get => _entries;
            set { if (value != _entries) { _entries = value; OnPropertyChangedWithValue(value, "Entries"); } }
        }

        [DataSourceProperty]
        public string TitleText => "Letters";

        [DataSourceProperty]
        public string EmptyHint => "Choose someone, and read what the roads have carried.";

        [DataSourceProperty]
        public string SendText => "Seal and send";

        [DataSourceProperty]
        public bool HasSelection => _selected != null;

        [DataSourceProperty]
        public string SelectedName
        {
            get => _selectedName;
            set { if (value != _selectedName) { _selectedName = value; OnPropertyChangedWithValue(value, "SelectedName"); } }
        }

        /// <summary>The road's state under the name: open, a courier riding, "go and speak", or the
        /// quiet fact that the writer is gone.</summary>
        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set { if (value != _statusText) { _statusText = value; OnPropertyChangedWithValue(value, "StatusText"); } }
        }

        [DataSourceProperty]
        public bool CanWrite => _canWrite;

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
                    OnPropertyChanged("EntriesBottomMargin");
                }
            }
        }

        /// <summary>The wrapped draft mirror above the input line — the engine's editable text is
        /// single-line, so this is where a long letter stays readable while it is written.</summary>
        [DataSourceProperty]
        public bool HasDraft => _canWrite && !string.IsNullOrWhiteSpace(_inputText);

        /// <summary>Where the correspondence ends vertically: above the input line, or above the
        /// draft mirror while a letter is being written (letters run long — the mirror is tall).</summary>
        [DataSourceProperty]
        public float EntriesBottomMargin => HasDraft ? 240f : 82f;

        [DataSourceProperty]
        public bool CanSend => HasSelection && _canWrite && !string.IsNullOrWhiteSpace(_inputText);
    }
}
