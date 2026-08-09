using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.UI.ChatWindow;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.NightWindow
{
    /// <summary>
    /// The window of the hearth (2026.08.09) — the chat window's quietest cousin. Your wives on the
    /// left with where each of them stands and how her season runs, her own fortnight of nights on
    /// the right, and one decision at the bottom.
    ///
    /// It is a pure VIEW: everything comes from the behavior's night ledger, and going to someone
    /// from here walks the very same road the evening's own question walks, gift inquiry and all.
    /// No number is ever shown of a woman's body unless the player asked for the odds to be shown;
    /// the default is words, because words are what a husband would actually have.
    /// </summary>
    public class NightWindowVM : ViewModel
    {
        private readonly ModConfig _config;

        private MBBindingList<NightContactVM> _contacts = new MBBindingList<NightContactVM>();
        private MBBindingList<ChatMessageVM> _entries = new MBBindingList<ChatMessageVM>();
        private NightContactVM? _selected;

        private string _selectedName = string.Empty;
        private string _statusText = string.Empty;
        private string _seasonText = string.Empty;
        private string _seasonColor = "#8E8A80FF";
        private string _oddsText = string.Empty;
        private string _relationText = string.Empty;
        private string _relationColor = "#8E8A80FF";
        private string _goText = "Go to her tonight";
        private string _goHintText = string.Empty;
        private bool _canGo;
        private bool _isInfoShown;

        public NightWindowVM(ModConfig config)
        {
            _config = config;
            RefreshContacts();
        }

        // ------------------------------ the list ------------------------------

        internal void RefreshContacts()
        {
            var list = new MBBindingList<NightContactVM>();
            try
            {
                foreach (var info in ImmersiveChatBehavior.NightContacts())
                    list.Add(new NightContactVM(info.Hero, info.Detail, info.SeasonText, info.SeasonColor,
                        info.SeasonRank, info.Block, OnSelect));
            }
            catch { /* an empty list is an honest one */ }

            Contacts = list;

            var keep = _selected?.Hero;
            var again = keep == null ? null : list.FirstOrDefault(c => c.Hero == keep);
            OnSelect(again ?? list.FirstOrDefault());
        }

        internal bool TrySelect(Hero hero)
        {
            var match = _contacts.FirstOrDefault(c => c.Hero == hero);
            if (match == null) return false;
            OnSelect(match);
            return true;
        }

        private void OnSelect(NightContactVM? contact)
        {
            foreach (var c in _contacts) c.IsSelected = c == contact;
            _selected = contact;
            OnPropertyChanged("HasSelection");
            RefreshSelected();
        }

        // ------------------------------ the chosen one ------------------------------

        internal void RefreshSelected()
        {
            var entries = new MBBindingList<ChatMessageVM>();
            var hero = _selected?.Hero;
            if (hero == null)
            {
                Entries = entries;
                SelectedName = string.Empty;
                StatusText = SeasonText = OddsText = RelationText = GoHintText = string.Empty;
                CanGo = false;
                return;
            }

            SelectedName = hero.Name?.ToString() ?? string.Empty;
            SeasonText = _selected!.SeasonText;
            SeasonColor = _selected.SeasonColor;

            try
            {
                var view = ImmersiveChatBehavior.NightViewFor(hero);
                StatusText = view.Status;
                OddsText = view.Odds;
                RelationText = view.Relation;
                RelationColor = view.RelationColor;
                GoText = view.GoLabel;
                GoHintText = view.GoHint;
                CanGo = view.CanGo;

                foreach (var line in view.Entries)
                    entries.Add(new ChatMessageVM(line.Header, line.Body, line.IsNarration, ColorOf(line.HeaderColor)));
            }
            catch (Exception ex)
            {
                ModLog.Error("drawing the window of the hearth", ex);
            }

            Entries = entries;
            OnPropertyChanged("HasOdds");
            OnPropertyChanged("HasRelation");
            OnPropertyChanged("EntriesTopMargin");
        }

        private static Color ColorOf(string hex)
        {
            try { return Color.ConvertStringToColor(string.IsNullOrWhiteSpace(hex) ? "#C9BFA8FF" : hex); }
            catch { return Colors.White; }
        }

        // ------------------------------ the commands ------------------------------

        public void ExecuteGo()
        {
            var hero = _selected?.Hero;
            if (hero == null || !_canGo) return;
            NightWindowManager.Close();
            ImmersiveChatBehavior.GoToHerFromWindow(hero);
        }

        public void ExecuteCycleMode()
        {
            try
            {
                ImmersiveChatBehavior.CycleNightMode();
                OnPropertyChanged("ModeText");
                OnPropertyChanged("ModeButtonText");
                RefreshContacts();
            }
            catch (Exception ex) { ModLog.Error("changing how the evenings are lived", ex); }
        }

        public void ExecuteToggleInfo() => IsInfoShown = !IsInfoShown;

        public void ExecuteClose() => NightWindowManager.Close();

        // ------------------------------ the bound properties ------------------------------

        [DataSourceProperty]
        public MBBindingList<NightContactVM> Contacts
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
        public bool HasSelection => _selected?.Hero != null;

        [DataSourceProperty]
        public string TitleText => "Your own hearth";

        [DataSourceProperty]
        public string EmptyHint =>
            _contacts.Count == 0
                ? "You have no wife. This window keeps the nights of a marriage — it will fill itself when there is one."
                : "Choose one of them.";

        [DataSourceProperty]
        public string ModeText => ImmersiveChatBehavior.NightModeDescription();

        [DataSourceProperty]
        public string ModeButtonText => "Change how the evenings go";

        [DataSourceProperty]
        public string SelectedName
        {
            get => _selectedName;
            set { if (value != _selectedName) { _selectedName = value; OnPropertyChangedWithValue(value, "SelectedName"); } }
        }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set { if (value != _statusText) { _statusText = value; OnPropertyChangedWithValue(value, "StatusText"); } }
        }

        [DataSourceProperty]
        public string SeasonText
        {
            get => _seasonText;
            set { if (value != _seasonText) { _seasonText = value; OnPropertyChangedWithValue(value, "SeasonText"); } }
        }

        [DataSourceProperty]
        public string SeasonColor
        {
            get => _seasonColor;
            set { if (value != _seasonColor) { _seasonColor = value; OnPropertyChangedWithValue(value, "SeasonColor"); } }
        }

        [DataSourceProperty]
        public string OddsText
        {
            get => _oddsText;
            set { if (value != _oddsText) { _oddsText = value; OnPropertyChangedWithValue(value, "OddsText"); OnPropertyChanged("HasOdds"); } }
        }

        [DataSourceProperty]
        public bool HasOdds => !string.IsNullOrWhiteSpace(_oddsText);

        [DataSourceProperty]
        public string RelationText
        {
            get => _relationText;
            set { if (value != _relationText) { _relationText = value; OnPropertyChangedWithValue(value, "RelationText"); OnPropertyChanged("HasRelation"); } }
        }

        [DataSourceProperty]
        public bool HasRelation => !string.IsNullOrWhiteSpace(_relationText);

        [DataSourceProperty]
        public string RelationColor
        {
            get => _relationColor;
            set { if (value != _relationColor) { _relationColor = value; OnPropertyChangedWithValue(value, "RelationColor"); } }
        }

        [DataSourceProperty]
        public string GoText
        {
            get => _goText;
            set { if (value != _goText) { _goText = value; OnPropertyChangedWithValue(value, "GoText"); } }
        }

        [DataSourceProperty]
        public string GoHintText
        {
            get => _goHintText;
            set { if (value != _goHintText) { _goHintText = value; OnPropertyChangedWithValue(value, "GoHintText"); } }
        }

        [DataSourceProperty]
        public bool CanGo
        {
            get => _canGo;
            set { if (value != _canGo) { _canGo = value; OnPropertyChangedWithValue(value, "CanGo"); } }
        }

        // The nights start below the header block, which is three stacked lines that may each wrap.
        [DataSourceProperty]
        public int EntriesTopMargin => HasOdds ? 156 : 132;

        [DataSourceProperty]
        public bool IsInfoShown
        {
            get => _isInfoShown;
            set { if (value != _isInfoShown) { _isInfoShown = value; OnPropertyChangedWithValue(value, "IsInfoShown"); } }
        }

        [DataSourceProperty]
        public string InfoButtonText => "?";

        [DataSourceProperty]
        public string BackText => "Back";

        [DataSourceProperty]
        public string InfoTitleText => "The nights of a marriage";

        [DataSourceProperty]
        public string InfoText
        {
            get
            {
                var key = string.IsNullOrWhiteSpace(_config?.NightWindowHotkey) ? "H" : _config!.NightWindowHotkey;
                return
                    "This is your own hearth. Every wife you have is here, with where her season stands and "
                    + "when the next night is yours to spend.\n\n"
                    + "WHAT THE NIGHTS ARE FOR. A child is no longer begun by a coin the world flips behind "
                    + "your back — it is begun on a night you chose. Each evening you are asked where you will "
                    + "sleep (or you can leave that to itself, with the button on the left). A woman's body keeps "
                    + "its own month, and the nights near the crest of it are the ones that may quicken; through "
                    + "the days of the custom her door is closed and no one is asked anything.\n\n"
                    + "WHAT COIN BUYS. Nothing, and it is still a true night. Ten denars buys wine; a thousand "
                    + "buys a jewel. What the coin actually buys is three things: better odds, a WRITTEN account "
                    + "of the night with a name she will keep it by — and talk. The grander the night, the more "
                    + "likely your other wives hear of it, and they hear its name too. That is the trade.\n\n"
                    + "AND THE MORNING. A night you paid for costs you the morning: the company breaks camp slow "
                    + "and disorganized after it. She is told of the lingering, since it was for her.\n\n"
                    + "WHAT SHE SEES. Each of them keeps her own fortnight — the nights you came, the nights her "
                    + "door was closed, and whatever she came to learn of the rest. Nothing tells her how to feel "
                    + "about any of it. That has always been her own.\n\n"
                    + $"Open and close this window with \"{key}\". Escape closes it too.";
            }
        }
    }
}
