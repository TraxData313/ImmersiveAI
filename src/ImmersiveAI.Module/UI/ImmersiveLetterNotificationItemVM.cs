using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// The view-model behind the letter-arrival map notice — the letter twin of
    /// <see cref="ImmersiveChatNotificationItemVM"/>: the writer's live portrait over the
    /// quest-scroll fallback, and a click that opens the letter window on their thread instead
    /// of the old read-it-now inquiry. Instantiated reflectively by MapNotificationVM via the
    /// type registration in <see cref="MapNoticePatch"/>.
    /// </summary>
    public class ImmersiveLetterNotificationItemVM : MapNotificationItemBaseVM
    {
        private bool _inspected;
        private ImageIdentifierVM? _characterImage;
        private bool _hasCharacterImage;

        [DataSourceProperty]
        public ImageIdentifierVM? CharacterImage
        {
            get => _characterImage;
            set
            {
                if (value != _characterImage)
                {
                    _characterImage = value;
                    OnPropertyChangedWithValue(value, "CharacterImage");
                }
            }
        }

        [DataSourceProperty]
        public bool HasCharacterImage
        {
            get => _hasCharacterImage;
            set
            {
                if (value != _hasCharacterImage)
                {
                    _hasCharacterImage = value;
                    OnPropertyChangedWithValue(value, "HasCharacterImage");
                }
            }
        }

        public ImmersiveLetterNotificationItemVM(ImmersiveLetterMapNotification data)
            : base(data)
        {
            base.NotificationIdentifier = "quest";

            try
            {
                if (data.Npc?.CharacterObject != null)
                {
                    CharacterImage = new CharacterImageIdentifierVM(Portraits.DarkCode(data.Npc.CharacterObject));
                    HasCharacterImage = true;
                }
            }
            catch { /* the quest-scroll base icon stands in */ }

            _onInspect = () =>
            {
                _inspected = true;
                ImmersiveChatBehavior.OnLetterNoticeInspected(data.Npc);
                ExecuteRemove();
            };
        }

        public override void ManualRefreshRelevantStatus()
        {
            base.ManualRefreshRelevantStatus();
            if (!Data.IsValid())
                ExecuteRemove();
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            if (!_inspected)
                ImmersiveChatBehavior.OnLetterNoticeDismissed((Data as ImmersiveLetterMapNotification)?.Npc);
        }
    }
}
