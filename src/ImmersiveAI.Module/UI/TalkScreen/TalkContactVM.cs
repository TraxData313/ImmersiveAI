using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// One soul in the talk screen's left-hand list — the chat window's contact and the letter
    /// window's correspondent grown together (2026.08.14): everyone near you, everyone you hold a
    /// story with wherever they are, and everyone whose letters remain though they are gone. Which
    /// of the three they are decides only how they are reached (a word, a letter, or nothing at
    /// all); the list itself makes no distinction between friends near and far.
    /// </summary>
    public class TalkContactVM : ViewModel
    {
        private readonly Action<TalkContactVM> _onSelect;

        private ImageIdentifierVM? _visual;
        private string _name = string.Empty;
        private string _detail = string.Empty;
        private bool _hasUnread;
        private bool _isSelected;

        /// <summary>Null only for a correspondent who has died — their letters stay readable.</summary>
        public Hero? Hero { get; }

        /// <summary>Their memory folder — the identity that survives death, and the key the letter
        /// drafts and the correspondence log are kept under.</summary>
        public string Folder { get; }

        /// <summary>Whether a remembered story already exists on disk — used for ordering (friends
        /// before strangers), never shown as a cold label.</summary>
        public bool HasHistory { get; }

        /// <summary>Whether letters have already passed between you — orders correspondents up.</summary>
        public bool HasLetters { get; }

        /// <summary>The last game day this one spoke with the player (-1 when never).</summary>
        public double LastSpokenGameDay { get; }

        /// <summary>They stand with the player right now: words reach them, and Send sends.</summary>
        public bool IsHere { get; }

        /// <summary>They are gone from the world: nothing can be sent, the letters remain to read.</summary>
        public bool IsGone { get; }

        // Internal because the row it is built from is: the class itself must stay public for
        // Gauntlet's binding, but nothing outside the module ever constructs one.
        internal TalkContactVM(ImmersiveChatBehavior.TalkContactInfo info, Action<TalkContactVM> onSelect)
        {
            Hero = info.Hero;
            Folder = info.Folder ?? string.Empty;
            HasHistory = info.HasHistory;
            HasLetters = info.HasLetters;
            LastSpokenGameDay = info.LastSpokenGameDay;
            IsHere = info.IsHere;
            IsGone = info.IsGone;
            _onSelect = onSelect;
            _name = info.Name ?? "Unknown";
            _detail = info.Detail ?? string.Empty;

            try
            {
                if (Hero?.CharacterObject != null)
                    _visual = new CharacterImageIdentifierVM(Portraits.DarkCode(Hero.CharacterObject));
            }
            catch { /* the list entry stands without a face rather than not at all */ }
        }

        public void ExecuteSelect() => _onSelect?.Invoke(this);

        [DataSourceProperty]
        public ImageIdentifierVM? Visual
        {
            get => _visual;
            set { if (value != _visual) { _visual = value; OnPropertyChangedWithValue(value, "Visual"); } }
        }

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set { if (value != _name) { _name = value; OnPropertyChangedWithValue(value, "Name"); } }
        }

        /// <summary>A soft second line under the name ("rides with you — your scout", "here in
        /// Sargot", "a courier rides toward you").</summary>
        [DataSourceProperty]
        public string Detail
        {
            get => _detail;
            set { if (value != _detail) { _detail = value; OnPropertyChangedWithValue(value, "Detail"); } }
        }

        /// <summary>The soft tag beside the name — how this one can be reached at all.</summary>
        [DataSourceProperty]
        public string PresenceText => IsGone ? "(gone)" : IsHere ? "(here)" : "(away)";

        /// <summary>Sea-glass for those who can hear you, cool grey for those across the map, and
        /// a dimmer grey still for those who cannot be reached at all.</summary>
        [DataSourceProperty]
        public Color PresenceColor => IsGone
            ? new Color(0.45f, 0.45f, 0.48f, 1f)
            : IsHere ? new Color(0.74f, 0.90f, 0.86f, 1f) : new Color(0.56f, 0.60f, 0.66f, 1f);

        [DataSourceProperty]
        public bool HasUnread
        {
            get => _hasUnread;
            set { if (value != _hasUnread) { _hasUnread = value; OnPropertyChangedWithValue(value, "HasUnread"); } }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } }
        }
    }
}
