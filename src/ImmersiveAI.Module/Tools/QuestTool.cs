using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Llm;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// Connects conversational dialogue directly to native Bannerlord Issues and Quests.
    /// Follows the hard law: the tool only LAYS the terms on the table (tally.Laid = true).
    /// No quest starts and no goods/gold move until the player confirms or chooses the branch
    /// in the native confirmation popup.
    /// </summary>
    public static class QuestTool
    {
        public const string OfferQuest = "offer_quest";
        public const string AcceptQuest = OfferQuest;
        public const string ReportQuest = "report_quest";

        public sealed class Tally
        {
            public bool Laid;
            public bool IsReport;
            public Hero? Npc;
            public IssueBase? AcceptedIssue;
            public QuestBase? ReportedQuest;
            public List<QuestDialogTreeBridge.DialogOptionNode> Branches = new List<QuestDialogTreeBridge.DialogOptionNode>();
            public string Confirmation = string.Empty;
        }

        public static readonly ToolDefinition OfferTool = new ToolDefinition(OfferQuest,
            "I am burdened with trouble, and the task is mine to lay before the traveler: this lays my proposed terms, task, or trouble formally before them, for them to seal or let lie. " +
            "I call this in the very same turn I describe, introduce, or propose an unundertaken task/trouble to the traveler, to lay the available choices (personal endeavor, companion dispatch, or lord's decree) on the table. " +
            "Nothing is settled by this tool call alone: the choice and sealing of it remain wholly theirs. Until they seal it by their own hand in the world, nothing is begun — I neither say it is settled, nor let the talk drift as though the task were already undertaken.",
            new[]
            {
                new ToolParameter("confirmation", "A brief phrase summarizing the task laid before them.", required: false)
            });

        public static readonly ToolDefinition AcceptTool = OfferTool;

        public static readonly ToolDefinition ReportTool = new ToolDefinition(ReportQuest,
            "Lays the verification, inspection, or delivery proposition of an ongoing task formally before the traveler, for them to seal or let lie. " +
            "I call this in the turn the traveler reports completion or proposes delivering upon the ongoing task. " +
            "In this turn, I speak only of inspecting, verifying, or examining what is brought (e.g. checking condition, counting goods, or acknowledging their offer to settle); until the physical transfer is sealed by their own hand in the world, nothing has changed hands and no settlement has occurred. I do not declare the matter settled or hand over payment/rewards in this turn.",
            new[]
            {
                new ToolParameter("result", "Confirmation of the quest result or resolution phrase.", required: false)
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
                    .FirstOrDefault(q => (q.QuestGiver == npc || IsQuestTargetHero(q, npc)) && !q.IsFinalized && QuestDialogTreeBridge.HasActionableReportBranches(q, npc));
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

