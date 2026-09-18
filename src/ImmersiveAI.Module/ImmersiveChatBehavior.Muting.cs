using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI
{
    public partial class ImmersiveChatBehavior
    {
        // Player preference, kept in the campaign save rather than in an NPC's rewritable memory.
        // Read and changed only on the game thread, including the final delivery of async outreach.
        private List<string> _mutedNpcIds = new List<string>();

        internal static bool IsNpcMuted(Hero? npc) =>
            npc != null && Current?._mutedNpcIds.Contains(npc.StringId) == true;

        internal static void ToggleNpcMute(Hero npc)
        {
            var self = Current;
            if (self == null || npc == null) return;
            if (!self._mutedNpcIds.Remove(npc.StringId))
            {
                self._mutedNpcIds.Add(npc.StringId);
                // Invalidates existing portrait notices without treating mute as an in-world rejection.
                self._pendingNotices.Remove(npc.StringId);
            }
        }
    }
}
