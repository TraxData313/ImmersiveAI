using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// One personality trait in the dev panel, as a stepper: "Mercy  [-]  -1 · cruel  [+]". The words
    /// are the sheet's own (PersonaBuilder.PersonalityWords), so what the row says is what she reads.
    /// Steps act at once and leave the panel open; the label is the only feedback, because the talk
    /// screen covers the map's message log.
    /// </summary>
    public class DevTraitRowVM : ViewModel
    {
        private readonly Hero _hero;
        private readonly TraitObject _trait;
        private readonly string _high, _low;

        public DevTraitRowVM(Hero hero, TraitObject trait, string high, string low)
        {
            _hero = hero;
            _trait = trait;
            _high = high;
            _low = low;
            NameText = trait.Name?.ToString() ?? trait.StringId;
        }

        private int Level => _hero.GetTraitLevel(_trait);

        [DataSourceProperty] public string NameText { get; }

        [DataSourceProperty]
        public string LevelText
        {
            get
            {
                int level = Level;
                if (level == 0) return "0 · neither";
                string word = level > 0 ? _high : _low;
                string sign = level > 0 ? "+" : string.Empty;
                bool strong = level >= 2 || level <= -2;
                return sign + level + " · " + word + (strong ? " (strongly)" : string.Empty);
            }
        }

        [DataSourceProperty] public bool CanDecrease => Level > _trait.MinValue;
        [DataSourceProperty] public bool CanIncrease => Level < _trait.MaxValue;

        public void ExecuteDecrease() => Step(-1);
        public void ExecuteIncrease() => Step(+1);

        private void Step(int delta)
        {
            ImmersiveChatBehavior.DevStepTrait(_hero, _trait, delta);
            OnPropertyChanged(nameof(LevelText));
            OnPropertyChanged(nameof(CanDecrease));
            OnPropertyChanged(nameof(CanIncrease));
        }
    }
}
