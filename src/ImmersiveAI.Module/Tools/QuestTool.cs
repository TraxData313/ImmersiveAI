using System;
using System.Linq;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// Connects conversational dialogue directly to native Bannerlord Issues and Quests.
    /// When an NPC (Village Notable, Town Artisan, Merchant, or Lord) has an issue or active quest,
    /// this tool allows the NPC to formally hand over the quest or accept its completion in dialogue.
    /// </summary>
    public static class QuestTool
    {
        public const string AcceptQuest = "accept_quest";
        public const string ReportQuest = "report_quest";

        public sealed class Tally
        {
            public Hero? Npc;
            public IssueBase? AcceptedIssue;
            public QuestBase? ReportedQuest;
            public Func<bool>? CompletionDelegate;
            public bool IsReneged;
            public int OptionIndex;
            public bool HasExplicitOptionIndex;
            public int RequiredGold;
        }

        public static readonly ToolDefinition AcceptTool = new ToolDefinition(AcceptQuest,
            "Formally hand over my spoken issue or task to the traveler only after they have clearly and explicitly committed in words to take it upon themselves. " +
            "If multiple agreement branches exist in my awareness (e.g. Standard Task Agreement vs Direct Cash Buyout/Alternative), pass the exact option_index matching the traveler's choice. " +
            "Do NOT call this when they are merely inquiring, discussing possibilities, or stating their skills.",
            new[]
            {
                new ToolParameter("confirmation", "A brief phrase confirming the task agreed upon.", required: false),
                new ToolParameter("option_index", "The integer index of the chosen agreement option (matching the option list in your awareness).", required: false)
            });

        public static readonly ToolDefinition ReportTool = new ToolDefinition(ReportQuest,
            "Acknowledge the handover, progress report, or breach resolution of an ongoing task when speaking with the traveler. " +
            "Pass the corresponding option_index matching the chosen dialogue resolution branch.",
            new[]
            {
                new ToolParameter("result", "Confirmation of the quest result or resolution phrase.", required: false),
                new ToolParameter("option_index", "The integer index of the chosen dialogue resolution option.", required: false)
            });

        public static IssueBase? GetAvailableIssue(Hero npc)
        {
            try
            {
                if (npc == null || Campaign.Current?.IssueManager == null) return null;
                if (Campaign.Current.IssueManager.Issues.TryGetValue(npc, out var issue) && issue != null)
                {
                    if (!issue.IsSolvingWithQuest && !issue.IsSolvingWithAlternative && !issue.IsSolvingWithLordSolution)
                        return issue;
                }
                return null;
            }
            catch { return null; }
        }

        public static QuestBase? GetActiveQuest(Hero npc)
        {
            try
            {
                if (npc == null || Campaign.Current?.QuestManager == null) return null;
                return Campaign.Current.QuestManager.Quests
                    .FirstOrDefault(q => (q.QuestGiver == npc || IsQuestTargetHero(q, npc)) && !q.IsFinalized);
            }
            catch { return null; }
        }

        public static bool IsQuestTargetHero(QuestBase? q, Hero? npc)
        {
            if (q == null || npc == null) return false;
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var targetHero = (q.GetType().GetField("_targetHero", flags) ?? q.GetType().GetField("_destinationHero", flags) ?? q.GetType().GetField("_recipientHero", flags))?.GetValue(q) as Hero;
                return targetHero == npc;
            }
            catch { return false; }
        }
    }
}
