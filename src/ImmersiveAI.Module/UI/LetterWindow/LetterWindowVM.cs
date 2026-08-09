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
        private bool _isPromptEditShown;
        private string _promptEditTitle = string.Empty;
        private string _promptEditText = string.Empty;

        // "Let me think…", the chat window's twin — here it writes the LETTER, not a spoken line.
        private readonly ModConfig _config;
        private bool _isWish;
        private string _wishText = string.Empty;
        private bool _isThinking;
        private bool _isPresetsShown;
        private bool _isPresetEditShown;
        private string _presetEditName = string.Empty;
        private string _presetEditText = string.Empty;
        private System.Collections.Generic.List<Core.Prompts.ConversationPreset> _presetList =
            new System.Collections.Generic.List<Core.Prompts.ConversationPreset>();
        private MBBindingList<ConversationPresetVM> _presets = new MBBindingList<ConversationPresetVM>();
        private MBBindingList<ConversationPresetVM> _presetRows = new MBBindingList<ConversationPresetVM>();

        public LetterWindowVM(ModConfig config)
        {
            _config = config;
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
            RefreshThinkState();
        }

        // ------------------------------ writing ------------------------------

        public void ExecuteSend()
        {
            var npc = _selected?.Hero;
            var text = (_inputText ?? string.Empty).Trim();
            if (npc == null || text.Length == 0 || !_canWrite) return;
            // The graying is a courtesy; this is the rail — a wish is never sealed into a letter,
            // and the refusal says why rather than doing nothing at all.
            if (_isWish)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Those are my own thoughts, not the letter — Shift+Enter turns them into words.",
                    new Color(0.85f, 0.75f, 0.55f, 1f)));
                return;
            }

            if (!ImmersiveChatBehavior.SendLetterFromWindow(npc, text)) { RefreshSelectionState(); return; }

            InputText = string.Empty;
            RefreshCorrespondence();   // the new letter is already in the log, the courier at its end
            RefreshSelectionState();   // the road is now taken until it arrives
        }

        public void ExecuteClose() => LetterWindowManager.Close();

        public void ExecuteToggleInfo() => IsInfoShown = !IsInfoShown;

        /// <summary>The way back out of whichever page is up — the same order Escape folds them in.
        /// Worn as a button by every overlay, because "X" closes the whole window (Anton, 2026.08.08).</summary>
        public void ExecuteBack()
        {
            if (IsPromptEditShown) ExecutePromptCancel();
            else if (IsPresetEditShown) IsPresetEditShown = false;
            else if (IsPresetsShown) IsPresetsShown = false;
            else if (IsInfoShown) IsInfoShown = false;
        }

        // ------------------------------ "Let me think…" ------------------------------
        // The chat window's twin, writing the LETTER instead of a spoken line — the same presets
        // file, the same rail that a wish is never sent. Enter still seals nothing here (a letter
        // deserves a deliberate seal), but it does set the mind to work when the box holds a wish.

        public void ExecuteThink()
        {
            var npc = _selected?.Hero;
            if (npc == null || !CanThink) return;
            if (!ImmersiveChatBehavior.BeginPlayerThought(npc, asLetter: true, _inputText)) return;

            IsPresetsShown = false;
            RefreshThinkState();
        }

        public void OnThoughtReady(string folder, string words)
        {
            // Folders reach the list two ways (a live hero's path, or one read off a letters file),
            // so the match is case-blind — a mismatch here would strand the words in the draft store.
            if (_selected != null && string.Equals(_selected.Folder, folder, StringComparison.OrdinalIgnoreCase))
            {
                InputText = words ?? string.Empty;
                IsWish = false;
            }
            RefreshThinkState();
        }

        public void OnThoughtFailed(string folder) => RefreshThinkState();

        private void RefreshThinkState()
        {
            IsThinking = _selected?.Hero != null
                         && ImmersiveChatBehavior.IsThinkingFor(_selected.Hero, asLetter: true);
            OnPropertyChanged("CanThink");
            OnPropertyChanged("ThinkText");
        }

        public void ExecuteTogglePresets()
        {
            if (!_isPresetsShown) LoadPresets();
            IsPresetsShown = !_isPresetsShown;
        }

        public void ExecuteOpenPresetEditor()
        {
            LoadPresets();
            PresetEditName = string.Empty;
            PresetEditText = string.Empty;
            IsPresetsShown = false;
            IsPresetEditShown = true;
        }

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

        // The editing doors, same in-game overlay as the chat window's: a wrapped mirror + a
        // writing line, Save/Discard. Prompts are re-read on every reply, so no restart.
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
                    OnPropertyChanged("EntriesTopMargin");
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
            set
            {
                if (value != _statusText)
                {
                    _statusText = value;
                    OnPropertyChangedWithValue(value, "StatusText");
                    OnPropertyChanged("EntriesTopMargin");
                }
            }
        }

        // The two grey lines under the name stack in a list panel, so they can never land on each
        // other — but the correspondence below is placed by margin, and Gauntlet will not tell us how
        // tall a wrapped line came out. Estimated from the strings and always rounded UP: a little air
        // costs nothing, an overlap is the bug (the chat window's twin, 2026.08.08).
        private const float GreyBlockTop = 70f;
        private const float GreyLineHeight = 21f;
        private const int StatusLineChars = 95;    // the 15pt status line, its own right margin counted
        private const int BondLineChars = 130;     // the 14pt bond line, running the full pane

        private static int WrappedLines(string text, int perLine)
            => string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (text.Length + perLine - 1) / perLine);

        /// <summary>Where the correspondence begins vertically — under the header's grey lines,
        /// wrapping counted.</summary>
        [DataSourceProperty]
        public float EntriesTopMargin =>
            GreyBlockTop + GreyLineHeight * (WrappedLines(_statusText, StatusLineChars)
                                             + WrappedLines(_bondStatsText, BondLineChars)) + 8f;

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

                    // A chosen preset holds the SEAL shut (see CanSend) until the player makes the
                    // words their own — one keystroke away from the preset and it is theirs again.
                    if (_isWish && !string.Equals(_inputText, _wishText, StringComparison.Ordinal))
                        IsWish = false;

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
        public float EntriesBottomMargin => HasDraft ? 280f : 122f;

        [DataSourceProperty]
        public bool CanSend => HasSelection && _canWrite && !_isWish && !string.IsNullOrWhiteSpace(_inputText);

        // ------------------------------ "Let me think…" ------------------------------

        [DataSourceProperty]
        public bool ShowThink => _config.EnableThinkForMe;

        [DataSourceProperty]
        public string ThinkText => _isThinking ? "…thinking" : "Think  (Shift+Enter)";

        [DataSourceProperty]
        public string PresetText => "Conversation preset";

        /// <summary>An EMPTY box is a perfectly good ask: "think what I should write to them".</summary>
        [DataSourceProperty]
        public bool CanThink => ShowThink && HasSelection && _canWrite && !_isThinking;

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

        /// <summary>Whether the writing box holds an PRESET rather than the letter itself. Enter
        /// seals nothing here either way, but it does set the mind to work.</summary>
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
                OnPropertyChanged("CanSend");
            }
        }

        [DataSourceProperty]
        public string WishHintText =>
            "what I mean to get across, not the letter itself — so the seal is held shut. Shift+Enter and I "
            + "will think what to write; change a word of it and it is yours to send.";

        [DataSourceProperty]
        public Color DraftColor => _isWish
            ? new Color(0.72f, 0.70f, 0.92f, 1f)
            : new Color(0.85f, 0.75f, 0.55f, 1f);

        [DataSourceProperty]
        public MBBindingList<ConversationPresetVM> Presets
        {
            get => _presets;
            set { if (value != _presets) { _presets = value; OnPropertyChangedWithValue(value, "Presets"); } }
        }

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
            : "No presets kept yet — write one below, or simply press \"Let me think…\" with the box empty.";

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

        /// <summary>Back to the three given at the start — asked about first, since it throws every
        /// preset of the player's own away.</summary>
        public void ExecuteRestorePresets() => ImmersiveChatBehavior.RestoreConversationPresets(LoadPresets);

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

        // ------------------------------ the info overlay ------------------------------

        /// <summary>The "?" overlay: what this window is, how letters travel, what to try. Escape
        /// folds it away before closing the window (the manager checks this flag first).</summary>
        [DataSourceProperty]
        public bool IsInfoShown
        {
            get => _isInfoShown;
            set { if (value != _isInfoShown) { _isInfoShown = value; OnPropertyChangedWithValue(value, "IsInfoShown"); } }
        }

        /// <summary>The in-game prompt editor overlay — Escape discards it before the info overlay
        /// and before closing (the manager checks this flag first of all).</summary>
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

        /// <summary>The way out of every overlay, worn as a button on each of them.</summary>
        [DataSourceProperty]
        public string BackText => "← Back  (Esc)";

        /// <summary>The same corner button on the prompt editors — named honestly, because stepping
        /// back from a half-written prompt throws the edit away (Save is the door that keeps it).</summary>
        [DataSourceProperty]
        public string BackDiscardText => "← Back  (discards)";

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
            "• \"Let me think…\" (Shift+Enter) has your own character draft the letter for you, reading everything the one you are writing to would read; \"Preset…\" keeps standing wishes to steer it (\"ask after her health\"). A chosen preset fills the box with the WISH, never with the letter — Shift+Enter turns it into one.\n" +
            "• An unfinished letter is kept when the window closes; come back and it waits in the writing line.\n" +
            "\n" +
            "WHAT TO TRY\n" +
            "• Ask a far-off companion how their errand fares — a caravan master in your service will write you back a field report.\n" +
            "• Write to a lord you fought beside, or to kin you have not seen in a season.\n" +
            "• The words you send become part of how they remember you — a letter can move a heart across the whole map.";
    }
}
