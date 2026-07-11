using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.LetterWindow
{
    /// <summary>
    /// One correspondent in the letter window's list: portrait, name, and a soft line about the
    /// road between you (a courier riding, "here with you", or where last word places them).
    /// The hero may be null — a writer who has died keeps their place; the letters remain.
    /// </summary>
    public class LetterContactVM : ViewModel
    {
        private readonly Action<LetterContactVM> _onSelect;

        private ImageIdentifierVM? _visual;
        private string _name = string.Empty;
        private string _detail = string.Empty;
        private bool _isSelected;

        public Hero? Hero { get; }

        /// <summary>The bond's folder on disk — where letters.txt lives; the stable key even when
        /// the hero is gone.</summary>
        public string Folder { get; }

        public bool HasLetters { get; }
        public double LastSpokenGameDay { get; }

        internal LetterContactVM(ImmersiveChatBehavior.LetterContactInfo info, Action<LetterContactVM> onSelect)
        {
            Hero = info.Hero;
            Folder = info.Folder;
            HasLetters = info.HasLetters;
            LastSpokenGameDay = info.LastSpokenGameDay;
            _onSelect = onSelect;
            _name = info.Name;
            _detail = info.Detail;

            try
            {
                if (info.Hero?.CharacterObject != null)
                    _visual = new CharacterImageIdentifierVM(Portraits.DarkCode(info.Hero.CharacterObject));
            }
            catch { /* the entry stands without a face rather than not at all */ }
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

        [DataSourceProperty]
        public string Detail
        {
            get => _detail;
            set { if (value != _detail) { _detail = value; OnPropertyChangedWithValue(value, "Detail"); } }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } }
        }
    }
}
