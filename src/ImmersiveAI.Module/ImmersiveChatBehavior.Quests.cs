using System;
using ImmersiveAI.Core;
using ImmersiveAI.Tools;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI
{
    public partial class ImmersiveChatBehavior
    {
        private bool CanBridgeQuests(Hero npc)
        {
            if (!_config.EnableQuestDialogueBridge) return false;
            if (npc == null || !npc.IsAlive) return false;
            return QuestTool.GetAvailableIssue(npc) != null || QuestTool.GetActiveQuest(npc) != null;
        }

        private void DispatchQuestOutcomes(QuestTool.Tally? quest, string? spokenReply = null)
        {
            if (quest == null) return;
            QuestDialogTreeBridge.DispatchOutcomes(quest, spokenReply);
        }
    }
}
