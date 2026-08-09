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

        public void ExecuteToggleVisitMode()
        {
            try
            {
                ImmersiveChatBehavior.ToggleNightAutoVisit();
                RefreshModeText();
            }
            catch (Exception ex) { ModLog.Error("changing how the women are visited", ex); }
        }

        public void ExecuteTogglePrevent()
        {
            try
            {
                ImmersiveChatBehavior.ToggleNightPreventChild();
                RefreshModeText();
            }
            catch (Exception ex) { ModLog.Error("changing whether a child is prevented", ex); }
        }

        private void RefreshModeText()
        {
            OnPropertyChanged("VisitModeText");
            OnPropertyChanged("VisitModeButtonText");
            OnPropertyChanged("PreventText");
            OnPropertyChanged("PreventButtonText");
            OnPropertyChanged("ControlsHeight");
            RefreshContacts();
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

        // The two switches, each with its own plain sentence under it. No lyric here on purpose:
        // a control has to say what it does (Anton, 2026.08.10).
        [DataSourceProperty]
        public string VisitModeText => ImmersiveChatBehavior.NightVisitModeLine();

        [DataSourceProperty]
        public string VisitModeButtonText => ImmersiveChatBehavior.NightVisitButtonText();

        [DataSourceProperty]
        public string PreventText => ImmersiveChatBehavior.NightPreventLine();

        [DataSourceProperty]
        public string PreventButtonText => ImmersiveChatBehavior.NightPreventButtonText();

        /// <summary>Where the wives' list starts, below the two switches. Their sentences wrap, and
        /// how far they wrap depends on which way each switch stands, so this is measured from the
        /// text rather than nailed to a number — the fixed margin is exactly what clipped the words
        /// in Anton's screenshot.</summary>
        [DataSourceProperty]
        public int ControlsHeight
        {
            get
            {
                // ~26px a wrapped line at font 14 in this column's width, two buttons and the gaps.
                int lines = EstimateLines(VisitModeText) + EstimateLines(PreventText);
                return 44 + 36 + 36 + 24 + lines * 20;
            }
        }

        // The column is ~330px wide at font 14 — about 52 characters to a line.
        private static int EstimateLines(string text) =>
            string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(1, (text.Trim().Length + 51) / 52);

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
                // PLAIN AND SCANNABLE, on purpose (Anton, 2026.08.10 — "не бих чел, по-скоро бих
                // експериментирал направо"). This is the one page in the mod written for the player
                // as an OPERATOR rather than as a reader: short headed blocks, one fact to a line,
                // the real numbers stated instead of described. The immersive voice belongs to the
                // souls, not to a help page nobody finishes. Every figure below is read from the
                // live config, so a page that says 24 hours is telling the truth about YOUR game.
                var key = string.IsNullOrWhiteSpace(_config?.NightWindowHotkey) ? "H" : _config!.NightWindowHotkey.Trim();
                int hours = _config?.NightCooldownHours ?? 24;
                int reveal = _config?.ConceptionRevealDelayDays ?? 7;
                int kept = _config?.MaxNightsRemembered ?? 14;
                int tenth = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, _config?.CarefulNightChanceFactor ?? 0.1)) * 100);

                return
                    "WHAT THIS IS\n"
                    + "Your marriage, night by night. A child is begun on a night you choose — not by a hidden daily roll.\n\n"

                    + "THE TWO SWITCHES (left)\n"
                    + "Visiting = MANUAL — you are asked at dusk, and you can come here any hour and go.\n"
                    + "Visiting = AUTO — goes on its own once the hours are up, late evening. Nothing asked, nothing bought, nothing written.\n"
                    + "Prevent a child = OFF — on Auto, goes to whoever is nearest her season.\n"
                    + $"Prevent a child = ON — on Auto, goes to whoever; any night's chance drops to {tenth}% of normal.\n\n"

                    + "HER MONTH\n"
                    + "Shown in words beside her name. \"At the crest\" = her best nights.\n"
                    + "Days of the custom — her door is closed. No visit, and you are not asked.\n"
                    + "Take her whole fertile stretch and a child is about as likely over a month as the game's own rate. Miss it and you missed the month.\n\n"

                    + "WHAT A GIFT BUYS (10–1000 denars)\n"
                    + "1. Odds — ×1.1 for wine, up to ×2 for a jewel.\n"
                    + "2. A written account of that night, with a name she keeps it by.\n"
                    + "3. Talk — your OTHER wives are likelier to hear of it (×0.5 with no gift, ×2 with the jewel), and they hear its name too.\n"
                    + "Cost: the company breaks camp disorganized next morning. A free night costs nothing and is not written.\n\n"

                    + "TIMING\n"
                    + $"One night per {hours} hours. Any visit resets the clock.\n"
                    + "Auto waits for the evening, so the whole day before it is yours to use.\n\n"

                    + "A CHILD\n"
                    + $"Not announced at once — she learns about {reveal} days later, and may then come or write to tell you.\n"
                    + "Optional line in the log after each night: the chance that stood, and whether it took.\n\n"

                    + "WHAT SHE SEES\n"
                    + $"Her last {kept} nights: when you came, when her door was closed, when she learned you were elsewhere, when she saw nothing.\n"
                    + "A wife who is not with you learns only that you were with another — and only sometimes.\n"
                    + "Nothing tells her how to feel about any of it.\n\n"

                    + "KEYS\n"
                    + $"{key} opens and closes this window. Escape closes it.\n"
                    + "More dials in the mod options, under \"Life of the NPCs\".";
            }
        }
    }
}
