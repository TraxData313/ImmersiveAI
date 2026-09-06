using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using ImmersiveAI.Core;
using ImmersiveAI.Tools;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImmersiveAI
{
    public partial class ImmersiveChatBehavior
    {
        private static Hero? _followUpTurnNpc;

        private bool CanBridgeQuests(Hero npc)
        {
            if (!_config.EnableQuestDialogueBridge) return false;
            if (npc == null || !npc.IsAlive) return false;
            if (_followUpTurnNpc == npc) return false;
            return QuestTool.GetAvailableIssue(npc) != null || QuestTool.GetActiveQuest(npc) != null;
        }

        // Presents a quest acceptance or handover laid this turn, after the reply is rendered
        // keeping the hard law that LLM words alone never modify game state.
        private void PresentQuestIfAny(Hero npc, TurnOutcome outcome)
        {
            try
            {
                if (outcome.Quest == null || !outcome.Quest.Laid) return;

                var tally = outcome.Quest;
                if (tally.IsReport && tally.ReportedQuest != null)
                {
                    ShowQuestReportInquiry(npc, tally);
                }
                else if (!tally.IsReport && tally.AcceptedIssue != null)
                {
                    ShowQuestAcceptInquiry(npc, tally);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("presenting quest inquiry for " + (npc?.Name?.ToString() ?? "?"), ex);
            }
        }

        private void ShowQuestAcceptInquiry(Hero npc, QuestTool.Tally tally)
        {
            try
            {
                var issue = tally.AcceptedIssue;
                if (issue == null) return;

                var name = npc?.Name?.ToString() ?? "Someone";
                var title = issue.Title?.ToString() ?? "A task is laid before you";
                var desc = issue.Description?.ToString() ?? string.Empty;

                var actionableBranches = tally.Branches?.Where(b => b != null).ToList() ?? new List<QuestDialogTreeBridge.DialogOptionNode>();

                if (actionableBranches.Count > 1)
                {
                    // Multi-branch acceptance (e.g. Art of the Trade: direct buyout vs standard consignment)
                    var elements = new List<InquiryElement>();
                    foreach (var branch in actionableBranches)
                    {
                        string optText = !string.IsNullOrWhiteSpace(branch.Text) ? branch.Text : "Agree to the task";
                        elements.Add(new InquiryElement(branch, optText, null, branch.IsAvailable, branch.UnavailableReason));
                    }

                    var declineBranch = new QuestDialogTreeBridge.DialogOptionNode
                    {
                        Text = new TextObject("{=ImmersiveAI_DeclineQuest}Let the matter rest for now.").ToString(),
                        IsAvailable = true,
                        Kind = QuestDialogTreeBridge.SolutionKind.Decline
                    };
                    elements.Add(new InquiryElement(declineBranch, declineBranch.Text, null, true, string.Empty));

                    var multiData = new MultiSelectionInquiryData(
                        title,
                        $"{name} lays the matter before you:\n{desc}\n\nChoose how you wish to answer:",
                        elements,
                        isExitShown: true,
                        minSelectableOptionCount: 1,
                        maxSelectableOptionCount: 1,
                        affirmativeText: "Confirm",
                        negativeText: "Let it lie",
                        affirmativeAction: selected =>
                        {
                            var chosen = selected?.FirstOrDefault()?.Identifier as QuestDialogTreeBridge.DialogOptionNode;
                            OnQuestAccepted(npc, issue, chosen);
                        },
                        negativeAction: _ => OnQuestDeclined(npc, issue),
                        soundEventPath: "",
                        isSeachAvailable: false);

                    MBInformationManager.ShowMultiSelectionInquiry(multiData, pauseGameActiveState: true, prioritize: true);
                }
                else
                {
                    // Single standard acceptance
                    var branch = actionableBranches.FirstOrDefault();
                    string branchNote = branch != null && !string.IsNullOrWhiteSpace(branch.Text) ? $"\n\n\"{branch.Text}\"" : string.Empty;
                    var body = $"{name} offers you the burden of this task:\n\n{desc}{branchNote}\n\nTake up the task?";
                    var acceptText = "Accept Task";
                    var declineText = "Let it lie";

                    var data = new InquiryData(
                        title, body, true, true, acceptText, declineText,
                        () => OnQuestAccepted(npc, issue, branch),
                        () => OnQuestDeclined(npc, issue),
                        "", 0f, null, null, null);

                    InformationManager.ShowInquiry(data, pauseGameActiveState: true);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("showing quest accept inquiry for " + (npc?.Name?.ToString() ?? "?"), ex);
            }
        }

        private void OnQuestAccepted(Hero npc, IssueBase issue, QuestDialogTreeBridge.DialogOptionNode? branch)
        {
            try
            {
                if (branch?.Kind == QuestDialogTreeBridge.SolutionKind.Decline)
                {
                    OnQuestDeclined(npc, issue);
                    return;
                }

                if (branch?.Kind == QuestDialogTreeBridge.SolutionKind.CompanionDispatch)
                {
                    OpenAlternativeSolutionPartyScreen(npc, issue);
                    return;
                }

                if (branch?.Kind == QuestDialogTreeBridge.SolutionKind.LordSolution)
                {
                    ExecuteLordSolution(npc, issue);
                    return;
                }

                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var title = issue.Title?.ToString() ?? "Task";

                if (Campaign.Current?.IssueManager != null && npc != null)
                {
                    Campaign.Current.IssueManager.StartIssueQuest(npc);
                }
                else
                {
                    issue.StartIssueWithQuest();
                }

                var realQuest = issue.IssueQuest;
                if (realQuest != null)
                {
                    QuestDialogTreeBridge.ExecuteQuestAcceptance(realQuest, branch);
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Task accepted: {title}",
                    new Color(0.45f, 0.85f, 0.45f, 1f)));

                string chosenText = !string.IsNullOrWhiteSpace(branch?.Text)
                    ? branch.Text
                    : $"I accept the task of \"{title}\".";

                string followupSystemNote = $"{playerName} accepted the burden of \"{title}\" by their own hand. The task has begun.";
                string outcomePromptNote = $"[QUEST ACCEPTANCE: {playerName} has accepted the task of “{title}” and is setting out. The traveler stated: \"{chosenText}\". Give your spoken encouragement or parting guidance for their journey. Do NOT call offer_quest or report_quest.]";

                TriggerFollowUpTurn(npc, chosenText, followupSystemNote, outcomePromptNote);
            }
            catch (Exception ex)
            {
                ModLog.Error("sealing quest acceptance for " + (npc?.Name?.ToString() ?? "?"), ex);
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        private void OpenAlternativeSolutionPartyScreen(Hero npc, IssueBase issue)
        {
            try
            {
                int neededMen = issue.GetTotalAlternativeSolutionNeededMenCount();
                int durationDays = issue.GetTotalAlternativeSolutionDurationInDays();
                var leftRoster = TroopRoster.CreateDummyTroopRoster();

                PartyScreenHelper.OpenScreenAsQuest(
                    leftRoster,
                    new TextObject("{=FbLOFO88}Select troops for mission"),
                    neededMen,
                    durationDays,
                    (leftMember, leftPrison, rightMember, rightPrison, leftLimit, rightLimit) =>
                    {
                        TextObject explanation;
                        bool satisfy = issue.DoTroopsSatisfyAlternativeSolution(leftMember, out explanation);
                        return new Tuple<bool, TextObject>(satisfy, explanation ?? new TextObject(string.Empty));
                    },
                    (leftOwner, leftMember, leftPrison, rightOwner, rightMember, rightPrison, fromCancel) =>
                    {
                        if (fromCancel)
                        {
                            if (leftMember != null && MobileParty.MainParty?.MemberRoster != null)
                            {
                                MobileParty.MainParty.MemberRoster.Add(leftMember);
                            }
                            OnQuestDeclined(npc, issue);
                            return;
                        }

                        try
                        {
                            Hero? assignedHero = null;
                            if (leftMember != null)
                            {
                                foreach (var elem in leftMember.GetTroopRoster())
                                {
                                    if (elem.Character?.IsHero == true && elem.Character.HeroObject != null && elem.Character.HeroObject != Hero.MainHero)
                                     {
                                        assignedHero = elem.Character.HeroObject;
                                        break;
                                    }
                                }
                            }

                            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                            var sentTroopsField = typeof(IssueBase).GetField("AlternativeSolutionSentTroops", flags)
                                ?? typeof(IssueBase).GetField("_alternativeSolutionSentTroops", flags);
                            sentTroopsField?.SetValue(issue, leftMember);

                            issue.AlternativeSolutionStartConsequence();
                            issue.StartIssueWithAlternativeSolution();

                            var title = issue.Title?.ToString() ?? "Task";
                            var compName = assignedHero?.Name?.ToString() ?? "a companion";
                            var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";

                            InformationManager.DisplayMessage(new InformationMessage(
                                $"Assigned {compName} to resolve: {title}",
                                new Color(0.45f, 0.85f, 0.45f, 1f)));

                            string followupText = issue.IssueAlternativeSolutionAcceptByPlayer?.ToString()
                                ?? $"I have assigned {compName} with troops to see to \"{title}\". They will carry out the task.";
                            string followupSystemNote = $"I assigned {compName} with troops to resolve \"{title}\". The assignment is underway.";
                            string outcomePromptNote = $"[COMPANION DISPATCHED: {playerName} assigned {compName} with troops to resolve “{title}”. Acknowledge this arrangement. Do NOT call offer_quest or report_quest.]";

                            TriggerFollowUpTurn(npc, followupText, followupSystemNote, outcomePromptNote);
                        }
                        catch (Exception ex)
                        {
                            ModLog.Error($"Error starting alternative solution for {npc?.Name}: {ex.Message}", ex);
                            InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
                        }
                    },
                    (character, type, side, leftOwner) =>
                    {
                        if (character == null) return false;
                        if (character.IsHero && character.HeroObject == Hero.MainHero) return false;
                        return true;
                    },
                    () => true
                );
            }
            catch (Exception ex)
            {
                ModLog.Error($"Error opening quest party screen for {npc?.Name}: {ex.Message}", ex);
            }
        }

        private void ExecuteLordSolution(Hero npc, IssueBase issue)
        {
            try
            {
                var title = issue.Title?.ToString() ?? "Task";
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                issue.StartIssueWithLordSolution();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Ruler's decree issued: {title}",
                    new Color(0.45f, 0.85f, 0.45f, 1f)));

                string followupText = issue.IssueLordSolutionAcceptByPlayer?.ToString()
                    ?? $"As the ruler of this realm, I have issued a formal decree to resolve \"{title}\".";
                string followupSystemNote = $"As ruler, I resolved \"{title}\" by decree.";
                string outcomePromptNote = $"[RULER'S DECREE: {playerName} resolved “{title}” by ruler's decree. Acknowledge this resolution. Do NOT call offer_quest or report_quest.]";

                TriggerFollowUpTurn(npc, followupText, followupSystemNote, outcomePromptNote);
            }
            catch (Exception ex)
            {
                ModLog.Error($"Error executing lord solution for {npc?.Name}: {ex.Message}", ex);
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        private void OnQuestDeclined(Hero npc, IssueBase issue)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var title = issue.Title?.ToString() ?? "the task";

                InformationManager.DisplayMessage(new InformationMessage(
                    "You let the task lie.", ConversationLogColor));

                string declineText = $"I must decline this task of \"{title}\" for now. Let the matter rest.";
                string fallbackSystemNote = $"I laid the task of \"{title}\" before {playerName}, but they let the offer lie unsealed. No commitment was made.";
                string outcomePromptNote = $"[QUEST DECLINED: {playerName} has declined to undertake “{title}\". The matter is left to rest without commitment. Do NOT call offer_quest or report_quest.]";

                TriggerFollowUpTurn(npc, declineText, fallbackSystemNote, outcomePromptNote);
            }
            catch { }
        }

        private void ShowQuestReportInquiry(Hero npc, QuestTool.Tally tally)
        {
            try
            {
                var quest = tally.ReportedQuest;
                if (quest == null) return;

                var name = npc?.Name?.ToString() ?? "Someone";
                var title = quest.Title?.ToString() ?? "Task Resolution";

                var actionableBranches = tally.Branches?.Where(b => b != null).ToList() ?? new List<QuestDialogTreeBridge.DialogOptionNode>();

                if (actionableBranches.Count > 1)
                {
                    var elements = new List<InquiryElement>();
                    foreach (var branch in actionableBranches)
                    {
                        string optText = !string.IsNullOrWhiteSpace(branch.Text) ? branch.Text : "Report progress";
                        elements.Add(new InquiryElement(branch, optText, null, branch.IsAvailable, branch.UnavailableReason));
                    }

                    var postponeBranch = new QuestDialogTreeBridge.DialogOptionNode
                    {
                        Text = new TextObject("{=ImmersiveAI_PostponeReport}Let us postpone settling this for now.").ToString(),
                        IsAvailable = true,
                        Kind = QuestDialogTreeBridge.SolutionKind.Decline
                    };
                    elements.Add(new InquiryElement(postponeBranch, postponeBranch.Text, null, true, string.Empty));

                    var multiData = new MultiSelectionInquiryData(
                        title,
                        $"Speaking with {name} regarding \"{title}\":\nChoose your response:",
                        elements,
                        isExitShown: true,
                        minSelectableOptionCount: 1,
                        maxSelectableOptionCount: 1,
                        affirmativeText: "Confirm",
                        negativeText: "Postpone",
                        affirmativeAction: selected =>
                        {
                            var chosen = selected?.FirstOrDefault()?.Identifier as QuestDialogTreeBridge.DialogOptionNode;
                            OnQuestReportConfirmed(npc, quest, chosen);
                        },
                        negativeAction: _ => OnQuestReportPostponed(npc, quest),
                        soundEventPath: "",
                        isSeachAvailable: false);

                    MBInformationManager.ShowMultiSelectionInquiry(multiData, pauseGameActiveState: true, prioritize: true);
                }
                else
                {
                    var branch = actionableBranches.FirstOrDefault();
                    string branchText = branch != null && !string.IsNullOrWhiteSpace(branch.Text) ? $"\n\n\"{branch.Text}\"" : string.Empty;
                    var body = $"Report and resolve the task of \"{title}\" with {name}?{branchText}";

                    var data = new InquiryData(
                        title, body, true, true, "Confirm Resolution", "Postpone",
                        () => OnQuestReportConfirmed(npc, quest, branch),
                        () => OnQuestReportPostponed(npc, quest),
                        "", 0f, null, null, null);

                    InformationManager.ShowInquiry(data, pauseGameActiveState: true);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("showing quest report inquiry for " + (npc?.Name?.ToString() ?? "?"), ex);
            }
        }

        private static QuestBase? _pendingHandoverQuest;
        private static Hero? _pendingHandoverNpc;

        internal static void NotePartyScreenClosed()
        {
            try { Current?.OnHandoverScreenClosed(); }
            catch (Exception ex) { ModLog.Error("evaluating handover completion", ex); }
        }

        internal void OnHandoverScreenClosed()
        {
            if (_pendingHandoverQuest == null || _pendingHandoverQuest.IsFinalized)
            {
                _pendingHandoverQuest = null;
                _pendingHandoverNpc = null;
                return;
            }

            var quest = _pendingHandoverQuest;
            var npc = _pendingHandoverNpc;
            _pendingHandoverQuest = null;
            _pendingHandoverNpc = null;

            if (npc != null && quest != null)
            {
                TryProgressQuestDiscussDialogFlow(npc, quest);
            }
        }

        internal static void TryProgressQuestDiscussDialogFlow(Hero npc, QuestBase quest)
        {
            if (npc == null || quest == null || quest.IsFinalized) return;

            try
            {
                QuestDialogTreeBridge.EnsureQuestDialogs(quest);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var flowField = typeof(QuestBase).GetField("DiscussDialogFlow", flags)
                    ?? typeof(QuestBase).GetField("_discussDialogFlow", flags);
                var discussFlow = flowField?.GetValue(quest) as DialogFlow;
                if (discussFlow == null) return;

                var options = QuestDialogTreeBridge.ExtractReportOptions(quest, npc);
                foreach (var opt in options)
                {
                    if (opt?.ConsequenceDelegate != null)
                    {
                        var beforeEndOneShot = QuestDialogTreeBridge.GetConversationEndOneShot();
                        opt.ConsequenceDelegate.DynamicInvoke();

                        var afterEndOneShot = QuestDialogTreeBridge.GetConversationEndOneShot();
                        if (afterEndOneShot != null && afterEndOneShot != beforeEndOneShot)
                        {
                            QuestDialogTreeBridge.ClearConversationEndOneShot();
                            afterEndOneShot.Invoke();
                        }

                        if (quest.IsFinalized)
                        {
                            var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                            var title = quest.Title?.ToString() ?? "Task";
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"Task resolution confirmed: {title}",
                                new Color(0.45f, 0.85f, 0.45f, 1f)));

                            string latestLog = string.Empty;
                            try
                            {
                                var entries = quest.JournalEntries;
                                if (entries != null && entries.Count > 0)
                                {
                                    var lastEntry = entries[entries.Count - 1];
                                    latestLog = lastEntry?.LogText?.ToString() ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(latestLog))
                                    {
                                        latestLog = Core.Prompts.TidingsFormatter.StripMarkup(latestLog);
                                    }
                                }
                            }
                            catch { }

                            string fallbackSystemNote = !string.IsNullOrWhiteSpace(latestLog)
                                ? $"{playerName} settled \"{title}\": {latestLog}"
                                : $"{playerName} reported and delivered upon \"{title}\". The matter is settled.";

                            string outcomePromptNote = !string.IsNullOrWhiteSpace(latestLog)
                                ? $"[QUEST RESOLUTION COMPLETED: The traveler has confirmed resolution of “{title}” in the world. Native resolution record: {latestLog}. Physical transfer and settlement are complete. Express your genuine reaction, gratitude, and conclude the agreement in character. Do NOT call offer_quest or report_quest.]"
                                : $"[QUEST RESOLUTION COMPLETED: The traveler has confirmed resolution of “{title}” in the world. Physical transfer and settlement are complete. Express your genuine reaction, gratitude, and conclude the agreement in character. Do NOT call offer_quest or report_quest.]";

                            Current?.TriggerFollowUpTurn(npc, opt.Text ?? $"I have completed \"{title}\".", fallbackSystemNote, outcomePromptNote);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[Quests] Error progressing discuss dialog flow for {quest?.Title}: {ex.Message}", ex);
            }
        }

        private void OnQuestReportConfirmed(Hero npc, QuestBase quest, QuestDialogTreeBridge.DialogOptionNode? branch)
        {
            try
            {
                if (branch?.Kind == QuestDialogTreeBridge.SolutionKind.Decline)
                {
                    OnQuestReportPostponed(npc, quest);
                    return;
                }

                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";

                if (branch?.ConsequenceDelegate != null)
                {
                    _pendingHandoverQuest = quest;
                    _pendingHandoverNpc = npc;

                    var beforeEndOneShot = QuestDialogTreeBridge.GetConversationEndOneShot();
                    branch.ConsequenceDelegate.DynamicInvoke();

                    var afterEndOneShot = QuestDialogTreeBridge.GetConversationEndOneShot();
                    if (afterEndOneShot != null && afterEndOneShot != beforeEndOneShot)
                    {
                        QuestDialogTreeBridge.ClearConversationEndOneShot();
                        afterEndOneShot.Invoke();
                    }
                }

                var title = quest.Title?.ToString() ?? "Task";
                string chosenText = !string.IsNullOrWhiteSpace(branch?.Text)
                    ? branch.Text
                    : $"I deliver and report upon the task of \"{title}\".";

                if (quest.IsFinalized)
                {
                    _pendingHandoverQuest = null;
                    _pendingHandoverNpc = null;

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Task resolution confirmed: {title}",
                        new Color(0.45f, 0.85f, 0.45f, 1f)));

                    string latestLog = string.Empty;
                    try
                    {
                        var entries = quest.JournalEntries;
                        if (entries != null && entries.Count > 0)
                        {
                            var lastEntry = entries[entries.Count - 1];
                            latestLog = lastEntry?.LogText?.ToString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(latestLog))
                            {
                                latestLog = Core.Prompts.TidingsFormatter.StripMarkup(latestLog);
                            }
                        }
                    }
                    catch { }

                    string fallbackSystemNote = !string.IsNullOrWhiteSpace(latestLog)
                        ? $"{playerName} settled \"{title}\": {latestLog}"
                        : $"{playerName} reported and delivered upon \"{title}\". The matter is settled.";

                    string outcomePromptNote = !string.IsNullOrWhiteSpace(latestLog)
                        ? $"[QUEST RESOLUTION COMPLETED: The traveler has confirmed resolution of “{title}” in the world (action: \"{chosenText}\"). Native resolution record: {latestLog}. Physical transfer and settlement are complete. Express your genuine reaction, gratitude, and conclude the agreement in character. Do NOT call offer_quest or report_quest.]"
                        : $"[QUEST RESOLUTION COMPLETED: The traveler has confirmed resolution of “{title}” in the world (action: \"{chosenText}\"). Physical transfer and settlement are complete. Express your genuine reaction, gratitude, and conclude the agreement in character. Do NOT call offer_quest or report_quest.]";

                    TriggerFollowUpTurn(npc, chosenText, fallbackSystemNote, outcomePromptNote);
                }
                // When quest is not finalized yet (e.g. PartyScreen opened for troop handover),
                // we do not force an artificial follow-up turn. This lets the native screen
                // operate smoothly without focus interruption or recursive report_quest loops.
            }
            catch (Exception ex)
            {
                ModLog.Error("confirming quest report for " + (npc?.Name?.ToString() ?? "?"), ex);
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        private void OnQuestReportPostponed(Hero npc, QuestBase quest)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var title = quest.Title?.ToString() ?? "the task";

                InformationManager.DisplayMessage(new InformationMessage(
                    "You postponed the matter.", ConversationLogColor));

                string postponeText = $"Let us postpone settling \"{title}\" for now.";
                string fallbackSystemNote = $"We spoke on \"{title}\", but {playerName} postponed the resolution for now.";
                string outcomePromptNote = $"[QUEST POSTPONED: Settlement of “{title}” was postponed by {playerName}. The task remains active in the world. Do NOT call offer_quest or report_quest.]";

                TriggerFollowUpTurn(npc, postponeText, fallbackSystemNote, outcomePromptNote);
            }
            catch { }
        }

        private void TriggerFollowUpTurn(Hero npc, string followupPlayerLine, string fallbackSystemNote, string? outcomePromptNote = null)
        {
            try
            {
                var situation = SafeBuildSituation(npc);
                if (!string.IsNullOrWhiteSpace(outcomePromptNote))
                {
                    situation = string.IsNullOrWhiteSpace(situation)
                        ? outcomePromptNote
                        : situation + "\n\n" + outcomePromptNote;
                }

                _followUpTurnNpc = npc;
                try
                {
                    if (Campaign.Current?.ConversationManager != null &&
                        Campaign.Current.ConversationManager.IsConversationInProgress &&
                        (Hero.OneToOneConversationHero == npc || _currentNpc == npc))
                    {
                        _ = RespondAsync(npc, followupPlayerLine, situation);
                    }
                    else if (UI.TalkUI.IsViewing(npc))
                    {
                        _ = QuickChatRespondAsync(npc, followupPlayerLine, situation);
                    }
                    else
                    {
                        AppendRecordedTurn(npc, fallbackSystemNote, string.Empty, OutreachMark.PlayerEngaged);
                        UI.TalkUI.OnThreadChanged(npc, markUnread: false);
                    }
                }
                finally
                {
                    _followUpTurnNpc = null;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[QuestFollowUp] Failed to trigger follow up turn for {npc?.Name?.ToString() ?? "?"}: {ex.Message}", ex);
            }
        }
    }
}
