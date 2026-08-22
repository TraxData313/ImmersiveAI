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
        public sealed class DeedRecord
        {
            public string Title { get; set; } = string.Empty;
            public double CompletedDay { get; set; }
            public QuestBase.QuestCompleteDetails Detail { get; set; }
            public string LogSummary { get; set; } = string.Empty;
            public bool IsAcknowledged { get; set; } = false;
        }

        private static readonly Dictionary<string, DeedRecord> _recentlyCompleted =
            new Dictionary<string, DeedRecord>(StringComparer.OrdinalIgnoreCase);

        public static void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
        {
            try
            {
                if (quest?.QuestGiver != null)
                {
                    var giverId = quest.QuestGiver.StringId;
                    var title = quest.Title?.ToString() ?? "Quest";
                    double completedDay = CampaignTime.Now.ToDays;
                    string logSummary = string.Empty;

                    try
                    {
                        var entries = quest.JournalEntries;
                        if (entries != null && entries.Count > 0)
                        {
                            var lastEntry = entries[entries.Count - 1];
                            logSummary = lastEntry?.LogText?.ToString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(logSummary))
                            {
                                logSummary = Core.Prompts.TidingsFormatter.StripMarkup(logSummary);
                            }
                        }
                    }
                    catch { }

                    _recentlyCompleted[giverId] = new DeedRecord
                    {
                        Title = title,
                        CompletedDay = completedDay,
                        Detail = detail,
                        LogSummary = logSummary,
                        IsAcknowledged = false
                    };

                    ModLog.Info($"[QuestCompletionTracker] Recorded quest '{title}' ({detail}) for giver {giverId} on day {completedDay:F2}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestCompletionTracker] Error recording completed quest: {ex.Message}");
            }
        }

        public static bool TryGetRecentDeed(string heroId, double lastTalkDay, out DeedRecord? record)
        {
            record = null;
            if (heroId != null && _recentlyCompleted.TryGetValue(heroId, out var rec))
            {
                // Only consider it a recent deed if completed recently (on or after last recorded conversation day)
                if (rec.CompletedDay >= lastTalkDay)
                {
                    record = rec;
                    return true;
                }
            }
            return false;
        }

        public static void MarkAcknowledged(string? heroId)
        {
            if (heroId != null && _recentlyCompleted.TryGetValue(heroId, out var rec))
            {
                rec.IsAcknowledged = true;
            }
        }
    }
}
