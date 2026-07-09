using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// The view-model behind the seeking-you-out map notice. Carries the NPC's live portrait
    /// (<see cref="CharacterImage"/>, rendered by the extra widget our MapNotificationItem prefab
    /// override adds — vanilla notices simply leave that widget empty) over a quest-scroll base
    /// icon as the fallback should the portrait ever fail to draw. Instantiated reflectively by
    /// MapNotificationVM via the type registration in <see cref="MapNoticePatch"/>.
    /// </summary>
    public class ImmersiveChatNotificationItemVM : MapNotificationItemBaseVM
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

        public ImmersiveChatNotificationItemVM(ImmersiveChatMapNotification data)
            : base(data)
        {
            base.NotificationIdentifier = "quest";

            try
            {
                if (data.Npc?.CharacterObject != null)
                {
                    CharacterImage = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(data.Npc.CharacterObject));
                    HasCharacterImage = true;
                }
            }
            catch { /* the quest-scroll base icon stands in */ }

            _onInspect = () =>
            {
                _inspected = true;
                ImmersiveChatBehavior.OnMapNoticeInspected(data.Npc);
                ExecuteRemove();
            };
        }

        // The notice outlives its cause (the NPC died, the offer expired, a save was reloaded) —
        // fold it away quietly on the map's periodic refresh.
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
                ImmersiveChatBehavior.OnMapNoticeDismissed((Data as ImmersiveChatMapNotification)?.Npc);
        }
    }
}
