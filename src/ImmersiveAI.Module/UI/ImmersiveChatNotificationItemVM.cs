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
                    CharacterImage = new CharacterImageIdentifierVM(DarkPortraitCode(data.Npc.CharacterObject));
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

        // The tableau tints the portrait backdrop with the code's Color1/Color2 (multiplayer uses
        // team colors there); the plain CreateFrom leaves them white, which renders as the raw
        // purple gradient. A muted near-black keeps the face lit and the circle quiet. Falls back
        // to the plain code if any piece of the character cannot be read.
        private static TaleWorlds.Core.CharacterCode DarkPortraitCode(TaleWorlds.CampaignSystem.CharacterObject character)
        {
            try
            {
                const uint backdrop = 0xFF17120DU; // ARGB: near-black with a warm cast
                return CharacterCode.CreateFrom(
                    character.Equipment?.CalculateEquipmentCode(),
                    character.GetBodyProperties(character.Equipment),
                    character.IsFemale,
                    character.IsHero,
                    backdrop,
                    backdrop,
                    character.DefaultFormationClass,
                    character.Race);
            }
            catch
            {
                return CharacterCode.CreateFrom(character);
            }
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
