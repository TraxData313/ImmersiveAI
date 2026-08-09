using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
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

        public ImmersiveNightNotificationItemVM(ImmersiveNightMapNotification data)
            : base(data)
        {
            base.NotificationIdentifier = "quest";

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
