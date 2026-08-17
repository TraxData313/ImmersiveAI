using System;
using System.Collections.Generic;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Tools;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Narrates the trouble the speaker themselves carries — the issue the game has laid on them
    /// (the very matter a player is sent to resolve) and any quest they have already given — so a
    /// villager asked "what ails you?" truly knows his own problem instead of inventing one.
    /// Rendered in the speaker's own first person like the rest of the situation, using the issue's
    /// own words (the brief and the asked-for remedy are written first person by the giver, so
    /// they quote naturally as "this is how I tell it").
    ///
    /// Everything is best-effort: a missing or throwing game datum costs only its own sentence,
    /// and a hero with no trouble simply contributes nothing.
    /// </summary>
    public static class TroubleBuilder
    {
        private static readonly System.Reflection.MethodInfo? CanPlayerTakeQuestConditionsMethod =
            typeof(IssueBase).GetMethod("CanPlayerTakeQuestConditions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        /// <summary>The speaker's own trouble and given quests as a flowing paragraph, or empty
        /// when nothing weighs on them. <paramref name="partner"/> only shapes the phrasing (the
        /// taker of a quest is always the player, named outright even when speaking to another).</summary>
        public static string Build(Hero speaker, Hero partner)
        {
            try { return BuildInner(speaker, partner); }
            catch { return string.Empty; }
        }

        private static string BuildInner(Hero speaker, Hero partner)
        {
            if (speaker == null || Campaign.Current == null) return string.Empty;

            var sentences = new List<string>();

            // Module 1: Recent unacknowledged deeds completed on the map since last conversation
            Try(() => AppendRecentDeedsFact(speaker, partner, sentences));

            // Module 2: Active trouble or current quest progress
            IssueBase issue = null;
            Try(() =>
            {
                var issues = Campaign.Current.IssueManager?.Issues;
                if (issues != null) issues.TryGetValue(speaker, out issue);
            });

            if (issue != null)
                DescribeOwnIssue(issue, sentences, speaker, partner);
            else
                Try(() =>
                {
                    if (speaker.IsNotable)
                        sentences.Add("CRITICAL FACT: I currently have NO troubles, tasks, deliveries, or business opportunities to offer anyone. If the traveler asks for work, errands, or trade opportunities, I MUST clearly and plainly tell them that I have no tasks, shipments, or work for them at this time, and recommend they check with other notables or the tavern. I MUST NEVER invent, promise, or fabricate fake errands, cargo shipments, or tasks.");
                });

            // Quests they gave that ride on without an issue behind them (a lord's charge, a story
            // quest) — the issue's own quest is already told above, so it is not repeated here.
            Try(() => DescribeGivenQuests(speaker, issue, sentences));

            // Module 3: Quests where the speaker is the target recipient contact (e.g. delivery of goods or livestock)
            Try(() => DescribeIncomingDeliveries(speaker, sentences));

            return sentences.Count == 0 ? string.Empty : string.Join(" ", sentences);
        }

        // Appends recent victory/settlement deeds completed since our last conversation.
        private static void AppendRecentDeedsFact(Hero speaker, Hero partner, List<string> sentences)
        {
            if (speaker == null) return;
            double lastTalkDay = 0;
            Try(() =>
            {
                var chatBehavior = Campaign.Current?.GetCampaignBehavior<ImmersiveChatBehavior>();
                var mem = chatBehavior?.LoadMemory(speaker);
                if (mem != null) lastTalkDay = mem.LastConversationGameDay;
            });

            if (Tools.QuestCompletionTracker.TryGetRecentDeed(speaker.StringId, lastTalkDay, out string deedTitle))
            {
                var player = partner?.Name?.ToString() ?? Hero.MainHero?.Name?.ToString() ?? "someone";
                sentences.Add($"Recent deed since we last spoke: The matter of “{deedTitle}” was successfully resolved on the map by {player}. The threat is gone and our settlement enjoys peace thanks to their aid.");
            }
        }

        // The trouble itself, in the giver's own words, and where its resolving presently stands.
        // We supply both the high-level objective (issue.Description), background context (issue.IssueBriefByIssueGiver),
        // and the detailed scope/destination (issue.IssueQuestSolutionExplanationByIssueGiver) as unquoted objective facts
        // rather than literal dialogue quotes, directing the LLM to paraphrase in its own persona across all languages.
        private static void DescribeOwnIssue(IssueBase issue, List<string> sentences, Hero speaker, Hero partner)
        {
            string title = null, desc = null, brief = null, questAsk = null, altAsk = null, goodName = null, targetSettlementName = null, targetDir = null;
            int goodCount = 0;
            Try(() => title = TidingsFormatter.StripMarkup(issue.Title?.ToString()));
            Try(() => desc = TidingsFormatter.StripMarkup(issue.Description?.ToString()));
            Try(() => brief = TidingsFormatter.StripMarkup(issue.IssueBriefByIssueGiver?.ToString()));
            Try(() => questAsk = TidingsFormatter.StripMarkup(issue.IssueQuestSolutionExplanationByIssueGiver?.ToString()));
            Try(() => altAsk = TidingsFormatter.StripMarkup(issue.IssueAlternativeSolutionExplanationByIssueGiver?.ToString()));

            // Extract target trade good / supply scope if present
            Try(() =>
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var issueType = issue.GetType();
                var itemField = issueType.GetField("_issueItem", flags) ?? issueType.GetField("_requestedItem", flags);
                if (itemField != null)
                {
                    var item = itemField.GetValue(issue) as ItemObject;
                    if (item != null) goodName = item.Name?.ToString();
                }

                var countField = issueType.GetField("_issueItemCount", flags) ?? issueType.GetField("_requestedItemCount", flags);
                if (countField != null)
                {
                    goodCount = Convert.ToInt32(countField.GetValue(issue));
                }
            });

            // Extract target destination settlement and spatial direction
            Try(() =>
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var issueType = issue.GetType();
                var targetField = issueType.GetField("_targetSettlement", flags) ?? issueType.GetField("_destinationSettlement", flags);
                if (targetField != null)
                {
                    var settlement = targetField.GetValue(issue) as Settlement;
                    if (settlement != null)
                    {
                        targetSettlementName = settlement.Name?.ToString();
                        var speakerSettlement = speaker?.CurrentSettlement ?? speaker?.PartyBelongedTo?.CurrentSettlement;
                        if (speakerSettlement != null)
                        {
                            var diff = settlement.Position.ToVec2() - speakerSettlement.Position.ToVec2();
                            targetDir = GetCompassDirection(diff.X, diff.Y);
                        }
                    }
                }
            });

            if (!string.IsNullOrWhiteSpace(title))
                sentences.Add($"A trouble weighs on me in these days — the matter of “{title}”.");

            if (!string.IsNullOrWhiteSpace(desc))
                sentences.Add($"The core objective of the matter: {desc}");

            var player = Hero.MainHero?.Name?.ToString() ?? "someone";

            if (issue.IsSolvingWithQuest)
            {
                sentences.Add($"{player} has taken this burden up at my asking.");
                Try(() => DescribeQuestProgress(issue.IssueQuest, sentences));
            }
            else if (issue.IsSolvingWithAlternative)
            {
                sentences.Add($"{player} has sent trusted people with a company of men to see it done for me; I await word of how they fare.");
            }
            else if (issue.IsSolvingWithLordSolution)
            {
                sentences.Add("The matter has been laid in a lord's hands to resolve, and I await their justice.");
            }
            else
            {
                sentences.Add("No one has yet taken this burden from me.");

                if (!string.IsNullOrWhiteSpace(brief) && !string.Equals(brief, desc, StringComparison.OrdinalIgnoreCase))
                    sentences.Add($"Background of the trouble: {brief}");

                if (!string.IsNullOrWhiteSpace(questAsk) && !string.Equals(questAsk, desc, StringComparison.OrdinalIgnoreCase) && !string.Equals(questAsk, brief, StringComparison.OrdinalIgnoreCase))
                    sentences.Add($"Specific request scope: {questAsk}");

                if (!string.IsNullOrWhiteSpace(altAsk) && !string.Equals(altAsk, desc, StringComparison.OrdinalIgnoreCase) && !string.Equals(altAsk, brief, StringComparison.OrdinalIgnoreCase) && !string.Equals(altAsk, questAsk, StringComparison.OrdinalIgnoreCase))
                    sentences.Add($"Alternative delegation or scope details: {altAsk}");

                if (!string.IsNullOrWhiteSpace(goodName))
                    sentences.Add(goodCount > 0 ? $"Specific trade goods to deliver: {goodCount} {goodName}" : $"Specific trade goods to deliver: {goodName}");

                if (!string.IsNullOrWhiteSpace(targetSettlementName) && !string.IsNullOrWhiteSpace(targetDir))
                    sentences.Add($"Target destination location: {targetSettlementName} (lies to the {targetDir} of where we stand).");

                // Soft condition awareness in the discovery phase (solo traveler / small party)
                int flagsInt = 0;
                Try(() =>
                {
                    if (CanPlayerTakeQuestConditionsMethod != null && Hero.MainHero != null)
                    {
                        object[] args = new object[] { Hero.MainHero, null!, null!, null!, 0 };
                        CanPlayerTakeQuestConditionsMethod.Invoke(issue, args);
                        if (args[1] != null) flagsInt = Convert.ToInt32(args[1]);
                    }
                });

                if ((flagsInt & 256) != 0) // PreconditionFlagNotEnoughTroops
                {
                    sentences.Add("Note on who stands before me: they ride with very few men or travel alone for a dangerous task. If they merely inquire about general local troubles or ask after the settlement, express realistic hesitation about their company size before formally proposing the charge.");
                }
            }
        }

        // How the taken-up quest fares: the last words of its journal, and the time it has left.
        private static void DescribeQuestProgress(QuestBase quest, List<string> sentences)
        {
            if (quest == null || !quest.IsOngoing) return;

            int current = 0;
            int target = 0;
            string progressText = string.Empty;
            Try(() =>
            {
                progressText = LatestJournalLine(quest, out current, out target);
            });

            if (!string.IsNullOrWhiteSpace(progressText))
            {
                sentences.Add($"Task progress: {progressText}");
            }

            Try(() =>
            {
                if (quest.IsRemainingTimeHidden) return;
                var remaining = quest.QuestDueTime - CampaignTime.Now;
                double days = remaining.ToDays;
                if (days <= 0 || days > 500) return; // lapsed, or so distant it does not press
                sentences.Add(days < 1.5
                    ? "The time for it is nearly spent."
                    : $"Some {(int)Math.Round(days)} days remain before the chance is lost.");
            });
        }

        // Quests this hero gave that are not the issue's own — each named with its latest word.
        private static void DescribeGivenQuests(Hero speaker, IssueBase ownIssue, List<string> sentences)
        {
            var quests = Campaign.Current.QuestManager?.Quests;
            if (quests == null) return;

            QuestBase ownQuest = ownIssue?.IssueQuest;
            foreach (var q in quests)
            {
                if (q == null || q == ownQuest || q.QuestGiver != speaker || !q.IsOngoing) continue;
                Try(() =>
                {
                    string title = TidingsFormatter.StripMarkup(q.Title?.ToString());
                    int current = 0, target = 0;
                    string progress = LatestJournalLine(q, out current, out target);
                    if (string.IsNullOrWhiteSpace(progress))
                        sentences.Add($"I have laid the charge of “{title}” on {Hero.MainHero?.Name?.ToString() ?? "someone"} to resolve.");
                    else
                        sentences.Add($"I have charged {Hero.MainHero?.Name?.ToString() ?? "someone"} with “{title}”, and where it stands is: {progress}");
                });
            }
        }

        // Quests given by other lords where this speaker is the designated contact or recipient.
        private static void DescribeIncomingDeliveries(Hero speaker, List<string> sentences)
        {
            var quests = Campaign.Current.QuestManager?.Quests;
            if (quests == null) return;

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;

            foreach (var q in quests)
            {
                if (q == null || !q.IsOngoing) continue;
                Try(() =>
                {
                    var qType = q.GetType();
                    var targetHeroField = qType.GetField("_targetHero", flags) ??
                                          qType.GetField("_buyerHero", flags) ??
                                          qType.GetField("_recipientHero", flags);

                    if (targetHeroField != null)
                    {
                        var targetHero = targetHeroField.GetValue(q) as Hero;
                        if (targetHero == speaker)
                        {
                            var giverName = q.QuestGiver?.Name?.ToString() ?? "someone";
                            var questTitle = TidingsFormatter.StripMarkup(q.Title?.ToString() ?? "a delivery task");
                            sentences.Add($"I am awaiting a delivery under the charge of “{questTitle}” arranged by {giverName}. If the traveler brings the cargo or livestock, I should acknowledge the delivery.");
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Assembles dynamic resolution branches and tool guidance strictly for active reply turns (not greetings).
        /// </summary>
        public static string BuildQuestBranches(Hero speaker, Hero partner)
        {
            if (speaker == null || Campaign.Current == null) return string.Empty;
            var sentences = new List<string>();

            IssueBase issue = null;
            var availableIssue = QuestTool.GetAvailableIssue(speaker);
            if (availableIssue != null)
            {
                QuestBase previewQ = null;
                var genMethod = availableIssue.GetType().GetMethod("GenerateIssueQuest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (genMethod != null)
                {
                    try { previewQ = genMethod.Invoke(availableIssue, new object[] { (availableIssue.StringId ?? "issue") + "_preview" }) as QuestBase; }
                    catch { }
                }
                var offerFlowField = typeof(QuestBase).GetField("OfferDialogFlow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var offerFlow = offerFlowField?.GetValue(previewQ) as DialogFlow;
                var options = QuestDialogTreeBridge.ExtractOptions(offerFlow, evaluateConditions: true, quest: previewQ);
                if (options.Count > 0)
                {
                    string prompt = QuestDialogTreeBridge.FormatOptionsPrompt(options, "Available dialogue acceptance branches:");
                    if (!string.IsNullOrWhiteSpace(prompt))
                    {
                        sentences.Add(prompt);
                        sentences.Add(QuestDialogTreeBridge.GetUniversalConversationGuidance(isReporting: false));
                    }
                }
            }
            else
            {
                var quests = Campaign.Current.QuestManager?.Quests;
                if (quests != null)
                {
                    foreach (var q in quests)
                    {
                        if (q != null && !q.IsFinalized && (q.QuestGiver == speaker || QuestTool.IsQuestTargetHero(q, speaker)))
                        {
                            var flowField = typeof(QuestBase).GetField("DiscussDialogFlow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                            var discussFlow = flowField?.GetValue(q) as DialogFlow;
                            var options = QuestDialogTreeBridge.ExtractOptions(discussFlow, evaluateConditions: true, quest: q);
                            if (options.Count > 0)
                            {
                                string prompt = QuestDialogTreeBridge.FormatOptionsPrompt(options, "Available dialogue resolution branches:");
                                if (!string.IsNullOrWhiteSpace(prompt))
                                {
                                    sentences.Add(prompt);
                                    sentences.Add(QuestDialogTreeBridge.GetUniversalConversationGuidance(isReporting: true));
                                }
                            }
                            break;
                        }
                    }
                }
            }

            return sentences.Count == 0 ? string.Empty : string.Join("\n", sentences);
        }

        private static string GetCompassDirection(float dx, float dy)
        {
            double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
            if (angle < 0) angle += 360.0;

            if (angle >= 337.5 || angle < 22.5) return "east";
            if (angle >= 22.5 && angle < 67.5) return "north-east";
            if (angle >= 67.5 && angle < 112.5) return "north";
            if (angle >= 112.5 && angle < 157.5) return "north-west";
            if (angle >= 157.5 && angle < 202.5) return "west";
            if (angle >= 202.5 && angle < 247.5) return "south-west";
            if (angle >= 247.5 && angle < 292.5) return "south";
            return "south-east";
        }

        private static string LatestJournalLine(QuestBase quest, out int current, out int target)
        {
            current = 0;
            target = 0;
            if (quest == null) return string.Empty;

            try
            {
                var entries = quest.JournalEntries;
                if (entries != null && entries.Count > 0)
                {
                    var last = entries[entries.Count - 1];
                    current = last.CurrentProgress;
                    target = last.Range;
                    var text = last.LogText?.ToString();
                    if (!string.IsNullOrWhiteSpace(text)) return TidingsFormatter.StripMarkup(text);
                }
            }
            catch { }

            return string.Empty;
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch { }
        }
    }
}
