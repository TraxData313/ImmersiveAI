using System;
using ImmersiveAI.Core;
using ImmersiveAI.Tools;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

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

        // Precondition flags defined by TaleWorlds.CampaignSystem.Issues.IssueBase.PreconditionFlags
        private const int PreconditionFlagNone = 0;
        private const int PreconditionFlagRelation = 1;
        private const int PreconditionFlagSkill = 2;
        private const int PreconditionFlagMoney = 4;
        private const int PreconditionFlagRenown = 8;
        private const int PreconditionFlagInfluence = 16;
        private const int PreconditionFlagWounded = 32;
        private const int PreconditionFlagAtWar = 64;
        private const int PreconditionFlagClanTier = 128;
        private const int PreconditionFlagNotEnoughTroops = 256;
        private const int PreconditionFlagNotInSameFaction = 512;
        private const int PreconditionFlagPartySizeLimit = 1024;
        private const int PreconditionFlagClanIsMercenary = 2048;

        private static readonly System.Reflection.PropertyInfo? IssueQuestCanBeDuplicatedProperty =
            typeof(IssueBase).GetProperty("IssueQuestCanBeDuplicated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        private static readonly System.Reflection.MethodInfo? CanPlayerTakeQuestConditionsMethod =
            typeof(IssueBase).GetMethod("CanPlayerTakeQuestConditions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        private string ResolveAcceptQuest(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? quest)
        {
            var issue = QuestTool.GetAvailableIssue(npc);
            var issueTitle = issue?.Title?.ToString() ?? "Unknown";
            ModLog.Info($"[QuestBridge] LLM called accept_quest for {npc?.Name} (Available issue: '{issueTitle}')");

            if (issue == null || quest == null)
            {
                ModLog.Warn($"[QuestBridge] accept_quest called for {npc?.Name}, but no available issue was found.");
                return "No troubled matter is presently available to give.";
            }

            // 1. Native Duplicate Quest Check (TaleWorlds native IssueQuestCanBeDuplicated mechanism)
            try
            {
                bool canDuplicate = IssueQuestCanBeDuplicatedProperty != null && (bool)(IssueQuestCanBeDuplicatedProperty.GetValue(issue, null) ?? false);
                if (!canDuplicate && Campaign.Current?.IssueManager?.Issues != null)
                {
                    bool hasSameTypeActive = false;
                    foreach (var activeIssue in Campaign.Current.IssueManager.Issues.Values)
                    {
                        if (activeIssue != null && activeIssue.IsSolvingWithQuest && activeIssue.GetType() == issue.GetType())
                        {
                            hasSameTypeActive = true;
                            break;
                        }
                    }
                    if (hasSameTypeActive)
                    {
                        ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Player already has an active quest of identical type ({issue.GetType().Name}).");
                        return "I cannot entrust this task to you right now: you already have a similar commitment underway elsewhere in Calradia. Settle your current task first, then speak to me again.";
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestBridge] Error evaluating IssueQuestCanBeDuplicated: {ex.Message}");
            }

            // Three-tier Precondition Check
            int flagsInt = 0;
            int reqGold = 0;
            try
            {
                if (CanPlayerTakeQuestConditionsMethod != null && Hero.MainHero != null)
                {
                    object[] args = new object[] { Hero.MainHero, null!, null!, null!, 0 };
                    bool canTake = (bool)CanPlayerTakeQuestConditionsMethod.Invoke(issue, args);
                    if (args[1] != null) flagsInt = Convert.ToInt32(args[1]);
                    if (args[4] != null) reqGold = Convert.ToInt32(args[4]);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestBridge] Could not evaluate CanPlayerTakeQuestConditions reflectively: {ex.Message}");
            }

            // 2. Hard Blocks (physical/economic impossibility)
            if ((flagsInt & PreconditionFlagMoney) != 0 || (reqGold > 0 && Hero.MainHero?.Gold < reqGold))
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Not enough gold (Requires {reqGold}, Player has {Hero.MainHero?.Gold})");
                return $"I cannot entrust this task to you: you lack the required {reqGold} gold to cover the cost or deposit. Speak to me again when you have the funds.";
            }
            if (Hero.MainHero != null && Hero.MainHero.IsPrisoner)
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Player is prisoner.");
                return "You are currently a captive and cannot undertake tasks.";
            }
            if ((flagsInt & PreconditionFlagAtWar) != 0)
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Faction at war.");
                return "Our realms are at open war. I will not conspire with an enemy.";
            }
            if ((flagsInt & PreconditionFlagPartySizeLimit) != 0)
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Party limit exceeded.");
                return "Your party is at its absolute limit and cannot take on more men or supplies for this task.";
            }
            if ((flagsInt & PreconditionFlagRelation) != 0)
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Relation too low.");
                return "You and I do not have a good history. I do not trust you with this business.";
            }

            // 3. Physical Distance Boundary (Remote Letter / Distant Chat requiring physical goods or upfront coin)
            bool isDistant = (Hero.MainHero?.CurrentSettlement == null || npc.CurrentSettlement == null || Hero.MainHero.CurrentSettlement != npc.CurrentSettlement);
            if (isDistant && reqGold > 0)
            {
                ModLog.Info($"[QuestBridge] Refusing remote accept_quest for {npc.Name}: Physical goods/deposit requires in-person transaction.");
                return "This matter involves large sums of coin and heavy goods that cannot be entrusted to a courier. Please come speak with me in person at my settlement so we may settle terms face to face.";
            }

            // 4. Soft Conditions (Alone / Small Troop Count / Injured / Low Renown) -> Allow for roleplay freedom & solo players!
            quest.Npc = npc;
            quest.AcceptedIssue = issue;
            quest.RequiredGold = reqGold;

            bool isSoloOrSmallParty = (flagsInt & PreconditionFlagNotEnoughTroops) != 0;
            if (isSoloOrSmallParty)
            {
                ModLog.Info($"[QuestBridge] Player taking quest with small/solo party ({npc.Name}). Granting solo player freedom with dialogue guidance.");
                return "The agreement is struck. The task is officially given into their hands. Note: since the player rides with few or no troops, the speaker may briefly remark with caution or admiration at their daring ('You ride with only a handful of men... be cautious'), offering parting advice and blessing their journey.";
            }

            return "The agreement is struck. The task is officially given into their hands. I speak on in my own words, thanking them, giving parting advice, or noting their courage.";
        }

        private string ResolveReportQuest(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? quest)
        {
            var activeQuest = QuestTool.GetActiveQuest(npc);
            var questTitle = activeQuest?.Title?.ToString() ?? "Unknown";
            ModLog.Info($"[QuestBridge] LLM called report_quest for {npc?.Name} (Active quest: '{questTitle}')");

            if (activeQuest == null || quest == null)
            {
                ModLog.Warn($"[QuestBridge] report_quest called for {npc?.Name}, but no active quest was found.");
                return "No ongoing task was found.";
            }

            var questType = activeQuest.GetType();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            // 1. Check if the quest has a specific condition check (e.g. PlayerHasTools, PlayerHasRequestedGoods, etc.)
            var conditionMethod = questType.GetMethod("PlayerHasTools", flags)
                ?? questType.GetMethod("PlayerHasRequestedGoods", flags)
                ?? questType.GetMethod("IsGoalReached", flags)
                ?? questType.GetMethod("CanPlayerCompleteQuest", flags);

            if (conditionMethod != null)
            {
                try
                {
                    bool canComplete = (bool)conditionMethod.Invoke(activeQuest, null);
                    if (!canComplete)
                    {
                        ModLog.Info($"[QuestBridge] Refusing report_quest for {npc.Name}: Condition {conditionMethod.Name} returned false (player lacks required items/progress).");
                        return "The task deed or delivery is not yet accomplished: the required items or progress have not been verified in inventory. The task remains ongoing.";
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"[QuestBridge] Error evaluating quest condition {conditionMethod.Name}: {ex.Message}");
                }
            }

            // 1b. Check delivery item/herd inventory count if applicable
            var countItemMethod = questType.GetMethod("GetAvailableRequestedItemCountOnPlayer", flags);
            var herdTypeField = questType.GetField("_herdTypeToDeliver", flags);
            var animalCountField = questType.GetField("_animalCountToDeliver", flags);
            if (countItemMethod != null && herdTypeField != null && animalCountField != null)
            {
                try
                {
                    var item = herdTypeField.GetValue(activeQuest) as ItemObject;
                    int required = Convert.ToInt32(animalCountField.GetValue(activeQuest));
                    int available = Convert.ToInt32(countItemMethod.Invoke(activeQuest, new object[] { item }));
                    if (available < required)
                    {
                        ModLog.Info($"[QuestBridge] Refusing report_quest for {npc.Name}: Player only has {available}/{required} of {item?.Name}.");
                        return $"You do not have all of the required {required} {item?.Name} in your possession (only {available} available). I cannot accept an incomplete delivery.";
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"[QuestBridge] Error checking delivery item count: {ex.Message}");
                }
            }

            // 2. Check if the quest is an on-map combat quest with undefeated party targets
            var destroyedCountField = questType.GetField("_destroyedPartyCount", flags);
            var totalCountField = questType.GetField("_totalPartyCount", flags);
            if (destroyedCountField != null && totalCountField != null)
            {
                try
                {
                    int destroyed = Convert.ToInt32(destroyedCountField.GetValue(activeQuest));
                    int total = Convert.ToInt32(totalCountField.GetValue(activeQuest));
                    if (destroyed < total)
                    {
                        ModLog.Info($"[QuestBridge] Refusing report_quest for {npc.Name}: Outlaws not yet destroyed ({destroyed}/{total}).");
                        return "The deed is not yet accomplished: the outlaws are still reported roaming the lands. The matter remains ongoing until they are dealt with on the map.";
                    }
                }
                catch { }
            }

            // 3. Find genuine native turn-in consequence/finish method
            var finishMethod = questType.GetMethod("DeliverHerdOnConsequence", flags)
                ?? questType.GetMethod("DeliverProductsOnConsequence", flags)
                ?? questType.GetMethod("FinishQuestSuccess", flags)
                ?? questType.GetMethod("FinishQuestSuccess1", flags)
                ?? questType.GetMethod("SuccessConsequences", flags)
                ?? questType.GetMethod("QuestSuccessConsequences", flags);

            if (finishMethod == null)
            {
                ModLog.Info($"[QuestBridge] Refusing report_quest for {npc.Name}: No native dialogue finish method found for {questType.Name}. Quest must be resolved via map events.");
                return "The deed is not something resolved by mere spoken words: its outcome will be decided by actions on the map. Continue your duty until finished.";
            }

            quest.Npc = npc;
            quest.ReportedQuest = activeQuest;
            quest.CompletionMethod = finishMethod;

            return "I acknowledge the completion of the deed and receive the delivered goods with gratitude. I speak on in my own authentic words.";
        }

        private void DispatchQuestOutcomes(QuestTool.Tally? quest, string? spokenReply = null)
        {
            if (quest == null) return;

            if (quest.AcceptedIssue != null)
            {
                var issueToStart = quest.AcceptedIssue;
                var npc = quest.Npc ?? issueToStart.IssueOwner;
                int reqGold = quest.RequiredGold;

                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var title = issueToStart.Title?.ToString() ?? "Quest";
                        ModLog.Info($"[QuestBridge] Formally starting quest '{title}' for {npc?.Name} via native dialogue pipeline...");

                        bool ok = false;

                        // 1. Generate the quest instance via native issue mechanism
                        if (issueToStart.IssueQuest == null)
                        {
                            issueToStart.StartIssueWithQuest();
                        }

                        var activeQuest = issueToStart.IssueQuest ?? (npc != null ? QuestTool.GetActiveQuest(npc) : null);
                        if (activeQuest == null && Campaign.Current?.IssueManager != null && npc != null)
                        {
                            Campaign.Current.IssueManager.Issues.TryGetValue(npc, out var refreshedIssue);
                            if (refreshedIssue != null && refreshedIssue.IssueQuest == null)
                            {
                                refreshedIssue.StartIssueWithQuest();
                                activeQuest = refreshedIssue.IssueQuest;
                            }
                        }

                        if (activeQuest != null)
                        {
                            // 2. Invoke the quest's native initialization / acceptance consequence method
                            // (Grants quest items, deducts upfront costs, sets up map tracking, spots hideouts, registers dialog flows)
                            try
                            {
                                var questType = activeQuest.GetType();
                                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                                var acceptMethod = questType.GetMethod("QuestAcceptedConsequences", flags)
                                    ?? questType.GetMethod("OnQuestAccepted", flags)
                                    ?? questType.GetMethod("QuestAcceptedByPlayerConsequences", flags);

                                if (acceptMethod != null)
                                {
                                    acceptMethod.Invoke(activeQuest, null);
                                    ModLog.Info($"[QuestBridge] Executed native quest acceptance consequence: {questType.Name}.{acceptMethod.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                ModLog.Warn($"[QuestBridge] Error invoking quest acceptance consequence: {ex.Message}");
                            }

                            // 3. Fallback: If consequence didn't start or register the quest into QuestManager, ensure it is started
                            if (Campaign.Current?.QuestManager != null && !Campaign.Current.QuestManager.Quests.Contains(activeQuest))
                            {
                                activeQuest.StartQuest();
                            }

                            try
                            {
                                var setDialogsMethod = typeof(QuestBase).GetMethod("SetDialogs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                setDialogsMethod?.Invoke(activeQuest, null);
                            }
                            catch { }

                            if (activeQuest.JournalEntries == null || activeQuest.JournalEntries.Count == 0)
                            {
                                var desc = issueToStart.Description ?? issueToStart.IssueQuestSolutionExplanationByIssueGiver ?? issueToStart.Title;
                                if (desc != null)
                                {
                                    activeQuest.AddLog(desc);
                                }
                            }

                            ok = true;
                        }

                        if (ok)
                        {
                            ModLog.Info($"[QuestBridge] Successfully started quest: '{title}' for {npc?.Name}");
                            InformationManager.DisplayMessage(
                                new InformationMessage($"Quest Started: {title}", new Color(0.4f, 0.9f, 0.4f, 1f)));
                        }
                        else
                        {
                            ModLog.Warn($"[QuestBridge] Failed to start quest '{title}' for {npc?.Name}");
                            InformationManager.DisplayMessage(
                                new InformationMessage($"Could not start quest: {title}", new Color(0.9f, 0.4f, 0.4f, 1f)));
                        }

                        // Re-affirm the LLM's spoken response on ResponseVar so native engine defaults do not clobber it
                        if (!string.IsNullOrWhiteSpace(spokenReply))
                        {
                            MBTextManager.SetTextVariable(ResponseVar, spokenReply, false);
                            _lastNpcLine = spokenReply;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("starting quest via dialogue", ex);
                        InformationManager.DisplayMessage(
                            new InformationMessage($"Quest Error: {ex.Message}", new Color(0.9f, 0.3f, 0.3f, 1f)));
                    }
                });
            }

            if (quest.ReportedQuest != null)
            {
                var questToReport = quest.ReportedQuest;
                var npc = quest.Npc ?? questToReport.QuestGiver;
                var methodToInvoke = quest.CompletionMethod;

                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        if (questToReport != null && !questToReport.IsFinalized && methodToInvoke != null)
                        {
                            var title = questToReport.Title?.ToString() ?? "Quest";
                            ModLog.Info($"[QuestBridge] Executing native quest completion method '{methodToInvoke.Name}' for '{title}' ({npc?.Name})");
                            methodToInvoke.Invoke(questToReport, null);
                            InformationManager.DisplayMessage(
                                new InformationMessage($"Quest Completed: {title}", new Color(0.95f, 0.85f, 0.35f, 1f)));

                            // Re-affirm the LLM's spoken response on ResponseVar so native engine defaults do not clobber it
                            if (!string.IsNullOrWhiteSpace(spokenReply))
                            {
                                MBTextManager.SetTextVariable(ResponseVar, spokenReply, false);
                                _lastNpcLine = spokenReply;
                            }
                        }
                        else
                        {
                            ModLog.Warn($"[QuestBridge] Cannot complete quest for {npc?.Name}: method is null or quest already finalized.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("completing quest via dialogue", ex);
                        InformationManager.DisplayMessage(
                            new InformationMessage($"Quest Error: {ex.Message}", new Color(0.9f, 0.3f, 0.3f, 1f)));
                    }
                });
            }
        }
    }
}
