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

        private readonly string _letterHotkey;
        private readonly string _chatHotkey;

        // Every correspondent, unfiltered — Contacts is the searched VIEW over this.
        private readonly System.Collections.Generic.List<LetterContactVM> _allContacts =
            new System.Collections.Generic.List<LetterContactVM>();

        private MBBindingList<LetterContactVM> _contacts = new MBBindingList<LetterContactVM>();
        private MBBindingList<ChatMessageVM> _entries = new MBBindingList<ChatMessageVM>();
        private LetterContactVM? _selected;
        private string _inputText = string.Empty;
        private string _searchText = string.Empty;
        private string _selectedName = string.Empty;
        private string _relationText = string.Empty;
        private string _bondStatsText = string.Empty;
        private Color _relationColor = Colors.White;
        private string _statusText = string.Empty;
        private bool _canWrite;
        private bool _isInfoShown;

        public LetterWindowVM(ModConfig config)
        {
            _letterHotkey = string.IsNullOrWhiteSpace(config.LetterWindowHotkey) ? "Y" : config.LetterWindowHotkey.Trim();
            _chatHotkey = string.IsNullOrWhiteSpace(config.ChatWindowHotkey) ? "O" : config.ChatWindowHotkey.Trim();
            RefreshContacts();
        }

        // ------------------------------ correspondents ------------------------------

        public void RefreshContacts()
        {
            var keepFolder = _selected?.Folder;
            _allContacts.Clear();

            foreach (var info in ImmersiveChatBehavior.CorrespondentsForLetters()
                         .OrderByDescending(i => i.HasLetters)
                         .ThenByDescending(i => i.LastSpokenGameDay)
                         .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                _allContacts.Add(new LetterContactVM(info, OnContactSelected));

            ApplyContactFilter();

            var again = keepFolder == null ? null : _allContacts.FirstOrDefault(c => c.Folder == keepFolder);
            if (again != null) SelectContact(again);
            else if (_selected != null) { _selected = null; RefreshSelectionState(); }
        }

        // The search line above the list: name-or-detail contains, so half a name or "caravan"
        // both find their soul. A filtered-out selected correspondence stays on stage.
        private void ApplyContactFilter()
        {
            var q = (_searchText ?? string.Empty).Trim();
            var list = new MBBindingList<LetterContactVM>();
            foreach (var c in _allContacts)
                if (q.Length == 0 || MatchesSearch(c.Name, q) || MatchesSearch(c.Detail, q))
                    list.Add(c);
            Contacts = list;
        }

        private static bool MatchesSearch(string? text, string q) =>
            text != null && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnContactSelected(LetterContactVM contact) => SelectContact(contact);

        public void SelectContact(LetterContactVM contact)
        {
            if (contact == null) return;

            foreach (var c in _allContacts) c.IsSelected = c == contact;
            _selected = contact;

            RefreshCorrespondence();
            RefreshSelectionState();

            // Bring back any half-written letter to this one from before the window was last closed.
            InputText = LetterWindowManager.GetDraft(contact.Folder);
        }

        /// <summary>Puts a given hero's correspondence on stage (used by "Write back" on an arrival).</summary>
        public void TrySelect(Hero hero)
        {
            if (hero == null) return;
            var contact = _allContacts.FirstOrDefault(c => c.Hero == hero);
            if (contact == null) return;
            if (!Contacts.Contains(contact)) SearchText = string.Empty; // "Write back" outranks a stale filter
            SelectContact(contact);
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

            _relationText = _selected?.Hero == null ? string.Empty : ImmersiveChatBehavior.RelationLabel(_selected.Hero);
            int rel = _selected?.Hero == null ? 0 : ImmersiveChatBehavior.RelationValue(_selected.Hero);
            _relationColor = rel > 0 ? new Color(0.55f, 0.82f, 0.55f, 1f)
                : rel < 0 ? new Color(0.86f, 0.53f, 0.49f, 1f)
                : new Color(0.78f, 0.75f, 0.68f, 1f);
            OnPropertyChanged("RelationText");
            OnPropertyChanged("HasRelation");
            OnPropertyChanged("RelationColor");

            BondStatsText = _selected == null ? string.Empty : ImmersiveChatBehavior.BondStatsLabel(_selected.Hero);

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

        public void ExecuteToggleInfo() => IsInfoShown = !IsInfoShown;

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
        public string RelationText
        {
            get => _relationText;
            set { if (value != _relationText) { _relationText = value; OnPropertyChangedWithValue(value, "RelationText"); } }
        }

        [DataSourceProperty]
        public bool HasRelation => !string.IsNullOrEmpty(_relationText);

        /// <summary>The bond's own mechanics under the name — shared story, freshness, and the hourly
        /// chance they are moved to write (the odds view's numbers for this one soul).</summary>
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

                    if (_selected != null)
                        LetterWindowManager.SetDraft(_selected.Folder, _inputText);
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

        // ------------------------------ the info overlay ------------------------------

        /// <summary>The "?" overlay: what this window is, how letters travel, what to try. Escape
        /// folds it away before closing the window (the manager checks this flag first).</summary>
        [DataSourceProperty]
        public bool IsInfoShown
        {
            get => _isInfoShown;
            set { if (value != _isInfoShown) { _isInfoShown = value; OnPropertyChangedWithValue(value, "IsInfoShown"); } }
        }

        [DataSourceProperty]
        public string InfoButtonText => "?";

        [DataSourceProperty]
        public string InfoTitleText => "Letters — how they work";

        [DataSourceProperty]
        public string InfoText =>
            "Letters cross the distances spoken words cannot: anyone you hold a story with can be written to, however far the roads run — and the letters you have exchanged stay readable here, even when the writer is gone.\n" +
            $"Open this window anywhere on the map with [{_letterHotkey}], with \"Send a letter by courier\" in a town, castle, or village — or with \"Write back\" when a letter reaches you.\n" +
            "\n" +
            "HOW LETTERS TRAVEL\n" +
            "• A letter rides with a courier for real days — the farther away they are, the longer the road. A courier underway is noted at the end of the page.\n" +
            "• One courier per correspondent at a time: while yours rides, that road is taken until it arrives.\n" +
            "• Someone standing beside you needs no courier — the line under their name will point you to go and speak instead (press [" + _chatHotkey + "]).\n" +
            "• The line above the list searches it — type part of a name. Under a chosen name: how much story you share, and the hour's chance they are moved to write to you.\n" +
            "• They may write to you first, and they may answer your letter — once — or let it lie unanswered. Both are remembered, and both are set down on this page.\n" +
            "• A sealed letter is a promise: it survives saving and loading, and arrives even if the world turns meanwhile.\n" +
            "\n" +
            "WRITING\n" +
            "• The writing line below holds a single line; the tall mirror above it shows the whole letter as it grows.\n" +
            "• Enter does NOT send here — a letter deserves a deliberate seal. Press \"Seal and send\" when it is ready.\n" +
            "• An unfinished letter is kept when the window closes; come back and it waits in the writing line.\n" +
            "\n" +
            "WHAT TO TRY\n" +
            "• Ask a far-off companion how their errand fares — a caravan master in your service will write you back a field report.\n" +
            "• Write to a lord you fought beside, or to kin you have not seen in a season.\n" +
            "• The words you send become part of how they remember you — a letter can move a heart across the whole map.";
    }
}
