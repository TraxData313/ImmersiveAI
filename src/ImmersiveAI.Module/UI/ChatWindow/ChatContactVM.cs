using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.ChatWindow
{
    /// <summary>
    /// One soul in the chat window's "those near you" list: their live portrait, name, and whether
    /// unread words of theirs are waiting. Selection is handled by the window VM (one thread on
    /// stage at a time).
    /// </summary>
    public class ChatContactVM : ViewModel
    {
        private readonly Action<ChatContactVM> _onSelect;

        private ImageIdentifierVM? _visual;
        private string _name = string.Empty;
        private string _detail = string.Empty;
        private bool _hasUnread;
        private bool _isSelected;

        public Hero Hero { get; }

        /// <summary>Whether a remembered story with this one already exists on disk — used for ordering
        /// (friends before strangers), never shown as a cold label.</summary>
        public bool HasHistory { get; }

        /// <summary>The last game day this one spoke with the player (-1 when never) — newest bonds first.</summary>
        public double LastSpokenGameDay { get; }

        public ChatContactVM(Hero hero, bool hasHistory, double lastSpokenGameDay, string detail, Action<ChatContactVM> onSelect)
        {
            Hero = hero;
            HasHistory = hasHistory;
            LastSpokenGameDay = lastSpokenGameDay;
            _onSelect = onSelect;
            _name = hero?.Name?.ToString() ?? "Unknown";
            _detail = detail ?? string.Empty;

            try
            {
                if (hero?.CharacterObject != null)
                    _visual = new CharacterImageIdentifierVM(Portraits.DarkCode(hero.CharacterObject));
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

        /// <summary>A soft second line under the name (e.g. "rides with you" / "here in Sargot").</summary>
        [DataSourceProperty]
        public string Detail
        {
            get => _detail;
            set { if (value != _detail) { _detail = value; OnPropertyChangedWithValue(value, "Detail"); } }
        }

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
