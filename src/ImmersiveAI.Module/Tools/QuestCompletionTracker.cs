using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// Tracks recently completed native quests by QuestGiver Hero StringId.
    /// Used by TroubleBuilder to inform NPCs of deeds completed on the map
    /// since their last face-to-face conversation.
    /// </summary>
    public static class QuestCompletionTracker
    {
        private static readonly Dictionary<string, (string Title, double CompletedDay)> _recentlyCompleted =
            new Dictionary<string, (string, double)>();

        public static void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
        {
            try
            {
                if (quest?.QuestGiver != null && detail == QuestBase.QuestCompleteDetails.Success)
                {
                    var giverId = quest.QuestGiver.StringId;
                    var title = quest.Title?.ToString() ?? "Quest";
                    double completedDay = CampaignTime.Now.ToDays;
                    _recentlyCompleted[giverId] = (title, completedDay);
                    ModLog.Info($"[QuestCompletionTracker] Recorded completed quest '{title}' for giver {giverId} on day {completedDay:F2}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestCompletionTracker] Error recording completed quest: {ex.Message}");
            }
        }

        public static bool TryGetRecentDeed(string heroId, double lastTalkDay, out string deedTitle)
        {
            deedTitle = string.Empty;
            if (heroId != null && _recentlyCompleted.TryGetValue(heroId, out var record))
            {
                // Only consider it a new unacknowledged deed if completed AFTER their last conversation
                if (record.CompletedDay > lastTalkDay)
                {
                    deedTitle = record.Title;
                    return true;
                }
            }
            return false;
        }
    }
}
