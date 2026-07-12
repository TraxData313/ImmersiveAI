using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ImmersiveAI.UI
{
    /// <summary>
    /// The right-side map notice for an NPC seeking the player out — the same persistent,
    /// non-pausing stack the ransom and marriage offers live in, but wearing the NPC's own
    /// portrait (see the MapNotificationItem prefab override + <see cref="ImmersiveChatNotificationItemVM"/>).
    /// Clicking it opens the accept/decline inquiry the player already knows.
    ///
    /// InformationData subclasses are persisted inside the save while the notice is up, so this
    /// type MUST stay registered with the save system (<see cref="ImmersiveAISaveDefiner"/>) —
    /// with the mod installed, an unregistered live notice would fail at save-WRITE time.
    /// Loading such a save with the mod fully removed is safe (verified against the v1.4.7
    /// save-load code, 2026.07.12): every step of LoadContext tolerates the unknown type id,
    /// and CampaignInformationManager.OnGameLoaded scrubs the resulting null notice itself.
    /// After a load the behavior's pending-offer state is gone, so <see cref="IsValid"/> turns
    /// false and the stale notice cleans itself up.
    /// </summary>
    public class ImmersiveChatMapNotification : InformationData
    {
        [SaveableProperty(1)]
        public Hero Npc { get; private set; }

        public override TextObject TitleText => new TextObject("{=ImmersiveAI_NoticeTitle}Someone seeks you");

        public override string SoundEventPath => "event:/ui/notification/ransom_offer";

        public ImmersiveChatMapNotification(Hero npc, TextObject descriptionText)
            : base(descriptionText)
        {
            Npc = npc;
        }

        public override bool IsValid()
        {
            return Npc != null && Npc.IsAlive
                && ImmersiveChatBehavior.IsNoticeStillAlive(Npc);
        }
    }

    /// <summary>Registers the mod's saveable types. The base id is a large, arbitrary number to
    /// keep clear of other mods; never change it once saves exist with it.</summary>
    public class ImmersiveAISaveDefiner : SaveableTypeDefiner
    {
        public ImmersiveAISaveDefiner() : base(726_401_000) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(ImmersiveChatMapNotification), 1);
        }
    }
}
