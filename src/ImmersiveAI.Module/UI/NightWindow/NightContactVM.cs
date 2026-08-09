using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.NightWindow
{
    /// <summary>
    /// One wife in the window's list: her face, her name, where her season stands in plain words,
    /// and where she is (with you, or a long way off). Never a number and never a percentage — what
    /// a husband would honestly know, and no more.
    /// </summary>
    public class NightContactVM : ViewModel
    {
        private readonly Action<NightContactVM> _onSelect;

        private ImageIdentifierVM? _visual;
        private string _name = string.Empty;
        private string _detail = string.Empty;
        private string _seasonText = string.Empty;
        private string _seasonColor = "#8E8A80FF";
        private bool _isSelected;

        public Hero Hero { get; }

        /// <summary>Nearness to her season, for sorting; smaller is nearer.</summary>
        public int SeasonRank { get; }

        /// <summary>Empty when she can be gone to tonight; otherwise why not.</summary>
        public string Block { get; }

        internal NightContactVM(Hero hero, string detail, string seasonText, string seasonColor,
            int seasonRank, string block, Action<NightContactVM> onSelect)
        {
            Hero = hero;
            _detail = detail ?? string.Empty;
            _seasonText = seasonText ?? string.Empty;
            _seasonColor = string.IsNullOrWhiteSpace(seasonColor) ? "#8E8A80FF" : seasonColor;
            SeasonRank = seasonRank;
            Block = block ?? string.Empty;
            _onSelect = onSelect;
            _name = hero?.Name?.ToString() ?? "she";

            try
            {
                if (hero?.CharacterObject != null)
                    _visual = new CharacterImageIdentifierVM(Portraits.DarkCode(hero.CharacterObject));
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
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } }
        }
    }
}
