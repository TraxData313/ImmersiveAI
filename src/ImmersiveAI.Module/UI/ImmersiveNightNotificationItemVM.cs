using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// The view-model behind the evening's notice (2026.08.10). It behaves like its two siblings,
    /// with one thing of its own: THE THREE ENDINGS ARE TOLD APART.
    ///
    /// • Clicked — the evening's choice opens, and the notice goes.
    /// • X'd by the player — that is an answer: not tonight, and not for a week
    ///   (Anton's ask; a man who waves the question away is not asking to be waved at again).
    /// • Lapsed at first light, untouched — nothing is snoozed. Missing one notice must not
    ///   silence the next seven, so the lapse path marks itself before removing.
    ///
    /// Without that middle case the only honest reading of a vanished notice would be "ignored",
    /// and a player who simply rode past dawn would be punished for it.
    /// </summary>
    public class ImmersiveNightNotificationItemVM : MapNotificationItemBaseVM
    {
        private bool _inspected;
        private bool _lapsed;
        private ImageIdentifierVM? _characterImage;
        private bool _hasCharacterImage;

        /// <summary>Her live portrait, drawn inside the notice's circle by the marked block in our
        /// MapNotificationItem override — the same widget the reach-out and letter notices have
        /// used since they shipped. Without it the circle drew bare (2026.08.15, Anton's
        /// screenshot: a flat magenta disc where a face belongs).</summary>
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

        public ImmersiveNightNotificationItemVM(ImmersiveNightMapNotification data)
            : base(data)
        {
            base.NotificationIdentifier = "quest";

            try
            {
                if (data.Woman?.CharacterObject != null)
                {
                    CharacterImage = new CharacterImageIdentifierVM(Portraits.DarkCode(data.Woman.CharacterObject));
                    HasCharacterImage = true;
                }
            }
            catch { /* the quest-scroll base icon stands in, as it does for its siblings */ }

            _onInspect = () =>
            {
                _inspected = true;
                ImmersiveChatBehavior.OnNightNoticeInspected();
                ExecuteRemove();
            };
        }

        // The notice outlives its cause (the night was settled another way, dawn came, a save was
        // reloaded) — fold it away quietly, and mark that it was time and not the player.
        public override void ManualRefreshRelevantStatus()
        {
            base.ManualRefreshRelevantStatus();
            if (!Data.IsValid())
            {
                _lapsed = true;
                ExecuteRemove();
            }
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            if (!_inspected && !_lapsed)
                ImmersiveChatBehavior.OnNightNoticeDismissed();
        }
    }
}
