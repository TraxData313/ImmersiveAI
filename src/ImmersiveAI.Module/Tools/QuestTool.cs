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
            public System.Reflection.MethodInfo? CompletionMethod;
            public int RequiredGold;
        }

        public static readonly ToolDefinition AcceptTool = new ToolDefinition(AcceptQuest,
            "I am burdened with trouble, and the task is mine to give: this formally hands my spoken errand over to the traveler once they have plainly agreed, in their own words, to take it upon themselves. " +
            "I call it ONLY when they have committed to aid me — never when they merely ask, discuss possibilities, or weigh their skill. Nothing is settled in the world until the hand is given.",
            new[]
            {
                new ToolParameter("confirmation", "A brief phrase confirming the task agreed upon.", required: false)
            });

        public static readonly ToolDefinition ReportTool = new ToolDefinition(ReportQuest,
            "Acknowledge the fulfillment of an errand brought to me: this receives delivered goods, herds, or messages and seals the quest's completion when the traveler returns having done what was asked. " +
            "Combat deeds upon the field conclude on their own; I call this when hands meet to deliver what was promised.",
            new[]
            {
                new ToolParameter("result", "Confirmation of the quest result.", required: false)
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
