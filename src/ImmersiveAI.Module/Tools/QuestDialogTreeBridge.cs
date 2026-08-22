using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ImmersiveAI.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// Universal Dialogue Tree Bridge Engine:
    /// Dynamically inspects TaleWorlds DialogFlow trees (Offer & Discuss) across vanilla and mods
    /// using pure DFS graph traversal over DialogFlow.Lines and dynamic condition pruning.
    /// Emits reachable player dialogue branches for native UI confirmation popups without fragile IL bytecode inspection.
    /// </summary>
    public static class QuestDialogTreeBridge
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly MethodInfo? CheckPreconditionsMethod =
            typeof(IssueBase).GetMethod("CheckPreconditions", Flags);

        private static readonly MethodInfo? CanPlayerTakeQuestConditionsMethod =
            typeof(IssueBase).GetMethod("CanPlayerTakeQuestConditions", Flags);

        public enum SolutionKind
        {
            DirectQuest,
            CompanionDispatch,
            LordSolution,
            Decline
        }

        public sealed class DialogOptionNode
        {
            public int Index { get; set; }
            public string Text { get; set; } = string.Empty;
            public string InputToken { get; set; } = string.Empty;
            public string OutputToken { get; set; } = string.Empty;
            public Delegate? ConsequenceDelegate { get; set; }
            public Delegate? ConditionDelegate { get; set; }
            public Delegate? ClickableConditionDelegate { get; set; }
            public bool IsAvailable { get; set; } = true;
            public string UnavailableReason { get; set; } = string.Empty;
            public SolutionKind Kind { get; set; } = SolutionKind.DirectQuest;
        }

        public static QuestBase? GetOrCreatePreviewQuest(IssueBase issue)
        {
            if (issue == null) return null;
            if (issue.IssueQuest != null)
            {
                EnsureQuestDialogs(issue.IssueQuest);
                return issue.IssueQuest;
            }
            return null;
        }

        private static QuestBase? CreateTemporaryPreviewQuest(IssueBase issue)
        {
            if (issue == null) return null;
            try
            {
                var prevIssueQuest = issue.IssueQuest;
                var genMethod = issue.GetType().GetMethod("GenerateIssueQuest", Flags);
                if (genMethod != null)
                {
                    var q = genMethod.Invoke(issue, new object[] { (issue.StringId ?? "issue") + "_preview" }) as QuestBase;
                    if (issue.IssueQuest != prevIssueQuest)
                    {
                        try
                        {
                            var setQuestProp = typeof(IssueBase).GetProperty("IssueQuest", Flags);
                            setQuestProp?.SetValue(issue, prevIssueQuest);
                        }
                        catch
                        {
                            var setQuestField = typeof(IssueBase).GetField("IssueQuest", Flags) ?? typeof(IssueBase).GetField("_issueQuest", Flags);
                            setQuestField?.SetValue(issue, prevIssueQuest);
                        }
                    }
                    return q;
                }
            }
            catch { }
            return null;
        }

        private static void DestroyTemporaryPreviewQuest(IssueBase issue, QuestBase? q)
        {
            if (q == null) return;

            try
            {
                // 1. Remove all dialogue lines injected into global ConversationManager (especially for quests like RuralNotableInnAndOut)
                Campaign.Current?.ConversationManager?.RemoveRelatedLines(q);
            }
            catch { }

            try
            {
                // 2. Clear all internal tracked objects and related fields
                var clearMethod = typeof(QuestBase).GetMethod("ClearRelatedFields", Flags);
                clearMethod?.Invoke(q, null);
            }
            catch { }

            try
            {
                // 3. Remove from QuestManager if registered
                Campaign.Current?.QuestManager?.OnQuestFinalized(q);
            }
            catch { }

            try
            {
                // 4. Unregister from MBObjectManager so it never survives as a ghost in the campaign
                TaleWorlds.ObjectSystem.MBObjectManager.Instance?.UnregisterObject(q);
            }
            catch { }

            try
            {
                if (issue != null && issue.IssueQuest == q)
                {
                    var setQuestProp = typeof(IssueBase).GetProperty("IssueQuest", Flags);
                    setQuestProp?.SetValue(issue, null);
                }
            }
            catch { }
        }

        public static void PurgeLingeringPreviewQuests()
        {
            try
            {
                var quests = Campaign.Current?.QuestManager?.Quests;
                if (quests != null)
                {
                    var ghosts = quests.Where(q => q != null && (q.StringId?.EndsWith("_preview", StringComparison.OrdinalIgnoreCase) == true || q.StringId?.Contains("_preview") == true)).ToList();
                    var clearMethod = typeof(QuestBase).GetMethod("ClearRelatedFields", Flags);
                    foreach (var ghost in ghosts)
                    {
                        ModLog.Info($"[QuestDialogTreeBridge] Purging lingering preview ghost quest: {ghost.StringId}");
                        try { Campaign.Current?.ConversationManager?.RemoveRelatedLines(ghost); } catch { }
                        try { clearMethod?.Invoke(ghost, null); } catch { }
                        try { Campaign.Current?.QuestManager?.OnQuestFinalized(ghost); } catch { }
                        try { TaleWorlds.ObjectSystem.MBObjectManager.Instance?.UnregisterObject(ghost); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestDialogTreeBridge] Error during PurgeLingeringPreviewQuests: {ex.Message}");
            }
        }

        public static void EnsureQuestDialogs(QuestBase q)
        {
            if (q == null) return;
            try
            {
                var setDialogsMethod = q.GetType().GetMethod("SetDialogs", Flags);
                setDialogsMethod?.Invoke(q, null);
            }
            catch { }
        }

        public static Action? GetConversationEndOneShot()
        {
            try
            {
                var convMgr = Campaign.Current?.ConversationManager;
                if (convMgr == null) return null;
                var field = typeof(ConversationManager).GetField("ConversationEndOneShot", Flags);
                return field?.GetValue(convMgr) as Action;
            }
            catch { return null; }
        }

        public static void ClearConversationEndOneShot()
        {
            try
            {
                var convMgr = Campaign.Current?.ConversationManager;
                if (convMgr == null) return;
                var field = typeof(ConversationManager).GetField("ConversationEndOneShot", Flags);
                field?.SetValue(convMgr, null);
            }
            catch { }
        }

        private sealed class ConversationContextScope : IDisposable
        {
            private readonly ConversationManager? _convMgr;
            private readonly List<object>? _prevAgents;
            private readonly object? _prevRepeatLines;
            private readonly int _prevRepeatIndex;

            public ConversationContextScope(Hero? targetHero)
            {
                _convMgr = Campaign.Current?.ConversationManager;
                if (_convMgr != null && targetHero != null)
                {
                    try
                    {
                        var agentsField = typeof(ConversationManager).GetField("_conversationAgents", Flags);
                        if (agentsField?.GetValue(_convMgr) is IList agentsList)
                        {
                            _prevAgents = new List<object>();
                            foreach (var a in agentsList)
                            {
                                if (a != null) _prevAgents.Add(a);
                            }

                            agentsList.Clear();
                            var agent = new TaleWorlds.CampaignSystem.Conversation.MapConversationAgent(targetHero.CharacterObject);
                            agentsList.Add(agent);
                        }

                        var repeatLinesField = typeof(ConversationManager).GetField("_dialogRepeatLines", Flags);
                        var repeatIndexField = typeof(ConversationManager).GetField("_currentRepeatIndex", Flags);

                        if (repeatLinesField != null)
                        {
                            _prevRepeatLines = repeatLinesField.GetValue(_convMgr);
                            var newLines = new List<TextObject> { new TextObject(string.Empty) };
                            repeatLinesField.SetValue(_convMgr, newLines);
                        }

                        if (repeatIndexField != null)
                        {
                            _prevRepeatIndex = Convert.ToInt32(repeatIndexField.GetValue(_convMgr));
                            repeatIndexField.SetValue(_convMgr, 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Warn($"[QuestDialogTreeBridge] Error setting conversation context scope: {ex.Message}");
                    }
                }
            }

            public void Dispose()
            {
                if (_convMgr != null)
                {
                    try
                    {
                        if (_prevAgents != null)
                        {
                            var agentsField = typeof(ConversationManager).GetField("_conversationAgents", Flags);
                            if (agentsField?.GetValue(_convMgr) is IList agentsList)
                            {
                                agentsList.Clear();
                                foreach (var a in _prevAgents)
                                {
                                    agentsList.Add(a);
                                }
                            }
                        }

                        var repeatLinesField = typeof(ConversationManager).GetField("_dialogRepeatLines", Flags);
                        var repeatIndexField = typeof(ConversationManager).GetField("_currentRepeatIndex", Flags);

                        repeatLinesField?.SetValue(_convMgr, _prevRepeatLines);
                        repeatIndexField?.SetValue(_convMgr, _prevRepeatIndex);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Extracts all valid, reachable player option branches from a DialogFlow instance.
        /// Evaluates ConditionDelegate to prune structurally unreachable branches for the current worldline,
        /// and evaluates ClickableConditionDelegate to determine availability.
        /// </summary>
        public static List<DialogOptionNode> ExtractOptions(DialogFlow? flow, bool evaluateConditions = true, QuestBase? quest = null, Hero? npc = null)
        {
            var results = new List<DialogOptionNode>();
            if (flow == null) return results;

            var targetHero = npc ?? quest?.QuestGiver;

            using (new ConversationContextScope(targetHero))
            {
                try
                {
                    var linesField = typeof(DialogFlow).GetField("Lines", Flags) ?? typeof(DialogFlow).GetField("_lines", Flags);
                    var lines = linesField?.GetValue(flow) as IEnumerable;
                    if (lines == null) return results;

                    var rawLines = new List<RawDialogLine>();

                    foreach (var lineObj in lines)
                    {
                        if (lineObj == null) continue;
                        var lineType = lineObj.GetType();

                        bool byPlayer = false;
                        var byPlayerField = lineType.GetField("ByPlayer", Flags) ?? lineType.GetField("_byPlayer", Flags);
                        if (byPlayerField != null) byPlayer = Convert.ToBoolean(byPlayerField.GetValue(lineObj));

                        var inToken = (lineType.GetField("InputToken", Flags) ?? lineType.GetField("_inputToken", Flags))?.GetValue(lineObj) as string ?? string.Empty;
                        var outToken = (lineType.GetField("OutputToken", Flags) ?? lineType.GetField("_outputToken", Flags))?.GetValue(lineObj) as string ?? string.Empty;
                        var consDel = (lineType.GetField("ConsequenceDelegate", Flags) ?? lineType.GetField("_consequenceDelegate", Flags))?.GetValue(lineObj) as Delegate;
                        var condDel = (lineType.GetField("ConditionDelegate", Flags) ?? lineType.GetField("_conditionDelegate", Flags))?.GetValue(lineObj) as Delegate;
                        var clickDel = (lineType.GetField("ClickableConditionDelegate", Flags) ?? lineType.GetField("_clickableConditionDelegate", Flags))?.GetValue(lineObj) as Delegate;
                        var textField = lineType.GetField("Text", Flags) ?? lineType.GetField("_text", Flags);
                        var textObj = textField?.GetValue(lineObj) as TextObject;
                        string lineText = textObj?.ToString() ?? string.Empty;

                        rawLines.Add(new RawDialogLine
                        {
                            ByPlayer = byPlayer,
                            Text = lineText,
                            TextObject = textObj,
                            InputToken = inToken,
                            OutputToken = outToken,
                            ConsequenceDelegate = consDel,
                            ConditionDelegate = condDel,
                            ClickableConditionDelegate = clickDel
                        });
                    }

                    var paths = TraverseAllPathsDFS(rawLines, evaluateConditions);
                    var finalResults = new List<DialogOptionNode>();
                    int idx = 0;

                    foreach (var node in paths)
                    {
                        bool isAvailable = true;
                        string unavailableReason = string.Empty;

                        if (evaluateConditions && node.ClickableConditionDelegate != null)
                        {
                            try
                            {
                                var parameters = node.ClickableConditionDelegate.Method.GetParameters();
                                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(TextObject).MakeByRefType())
                                {
                                    object[] invokeArgs = new object[] { new TextObject(string.Empty) };
                                    isAvailable = (bool)node.ClickableConditionDelegate.DynamicInvoke(invokeArgs);
                                    if (!isAvailable && invokeArgs[0] is TextObject reasonTextObj)
                                    {
                                        unavailableReason = reasonTextObj.ToString();
                                    }
                                }
                                else if (parameters.Length == 0)
                                {
                                    isAvailable = (bool)node.ClickableConditionDelegate.DynamicInvoke();
                                }
                            }
                            catch { }
                        }

                        node.Index = idx++;
                        node.IsAvailable = isAvailable;
                        node.UnavailableReason = unavailableReason;
                        finalResults.Add(node);
                    }

                    return finalResults;
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"[QuestDialogTreeBridge] Error extracting options from DialogFlow: {ex.Message}");
                    return results;
                }
            }
        }

        private sealed class RawDialogLine
        {
            public bool ByPlayer { get; set; }
            public string Text { get; set; } = string.Empty;
            public TextObject? TextObject { get; set; }
            public string InputToken { get; set; } = string.Empty;
            public string OutputToken { get; set; } = string.Empty;
            public Delegate? ConsequenceDelegate { get; set; }
            public Delegate? ConditionDelegate { get; set; }
            public Delegate? ClickableConditionDelegate { get; set; }
        }

        private static Delegate? CombineDelegates(List<Delegate> delegates)
        {
            if (delegates == null || delegates.Count == 0) return null;
            var nonNull = delegates.Where(d => d != null).ToList();
            if (nonNull.Count == 0) return null;
            if (nonNull.Count == 1) return nonNull[0];

            return new Action(() =>
            {
                foreach (var d in nonNull)
                {
                    try
                    {
                        d.DynamicInvoke();
                    }
                    catch (Exception ex)
                    {
                        ModLog.Warn($"[QuestDialogTreeBridge] Error invoking chained consequence ({d.Method?.Name}): {ex.Message}");
                    }
                }
            });
        }

        private static List<DialogOptionNode> TraverseAllPathsDFS(List<RawDialogLine> rawLines, bool evaluateConditions)
        {
            var results = new List<DialogOptionNode>();
            if (rawLines == null || rawLines.Count == 0) return results;

            var linesByInToken = new Dictionary<string, List<RawDialogLine>>(StringComparer.OrdinalIgnoreCase);
            var outputTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in rawLines)
            {
                if (string.IsNullOrWhiteSpace(line.InputToken)) continue;
                if (!linesByInToken.TryGetValue(line.InputToken, out var list))
                {
                    list = new List<RawDialogLine>();
                    linesByInToken[line.InputToken] = list;
                }
                list.Add(line);

                if (!string.IsNullOrWhiteSpace(line.OutputToken))
                {
                    outputTokens.Add(line.OutputToken);
                }
            }

            var rootTokens = linesByInToken.Keys.Where(tok => !outputTokens.Contains(tok)).ToList();
            if (rootTokens.Count == 0 && rawLines.Count > 0 && !string.IsNullOrWhiteSpace(rawLines[0].InputToken))
            {
                rootTokens.Add(rawLines[0].InputToken);
            }

            var visitedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rootToken in rootTokens)
            {
                TraverseGraphDFS(rootToken, linesByInToken, visitedTokens, new List<Delegate>(), results, evaluateConditions);
            }

            return results;
        }

        private static void TraverseGraphDFS(
            string currentToken,
            Dictionary<string, List<RawDialogLine>> linesByInToken,
            HashSet<string> visitedTokens,
            List<Delegate> accumulatedConsequences,
            List<DialogOptionNode> results,
            bool evaluateConditions)
        {
            if (string.IsNullOrWhiteSpace(currentToken) || visitedTokens.Contains(currentToken))
                return;

            visitedTokens.Add(currentToken);

            if (!linesByInToken.TryGetValue(currentToken, out var lines) || lines.Count == 0)
                return;

            foreach (var line in lines)
            {
                string resolvedText = line.Text;

                if (evaluateConditions && line.ConditionDelegate != null)
                {
                    try
                    {
                        var repeatLinesField = typeof(ConversationManager).GetField("_dialogRepeatLines", Flags);
                        var convMgr = Campaign.Current?.ConversationManager;
                        if (convMgr != null && repeatLinesField?.GetValue(convMgr) is IList repList)
                        {
                            var initialTextObj = line.TextObject ?? new TextObject(line.Text);
                            if (repList.Count == 0) repList.Add(initialTextObj);
                            else repList[0] = initialTextObj;
                        }

                        bool passed = (bool)line.ConditionDelegate.DynamicInvoke();
                        if (!passed) continue;

                        if (line.TextObject != null)
                        {
                            var refreshed = line.TextObject.ToString();
                            if (!string.IsNullOrWhiteSpace(refreshed))
                            {
                                resolvedText = refreshed;
                            }
                        }

                        if (convMgr != null && repeatLinesField?.GetValue(convMgr) is IList repListAfter && repListAfter.Count > 0)
                        {
                            var dynText = repListAfter[0]?.ToString();
                            if (!string.IsNullOrWhiteSpace(dynText))
                            {
                                resolvedText = dynText;
                            }
                        }
                    }
                    catch { continue; }
                }
                else if (line.TextObject != null)
                {
                    var fresh = line.TextObject.ToString();
                    if (!string.IsNullOrWhiteSpace(fresh))
                    {
                        resolvedText = fresh;
                    }
                }

                if (line.ByPlayer)
                {
                    var pathConsequences = new List<Delegate>(accumulatedConsequences);
                    if (line.ConsequenceDelegate != null)
                    {
                        pathConsequences.Add(line.ConsequenceDelegate);
                    }

                    var childNodes = new List<DialogOptionNode>();
                    if (!string.IsNullOrWhiteSpace(line.OutputToken) && linesByInToken.ContainsKey(line.OutputToken))
                    {
                        TraverseDownstreamDFS(
                            line.OutputToken,
                            linesByInToken,
                            new HashSet<string>(visitedTokens, StringComparer.OrdinalIgnoreCase),
                            pathConsequences,
                            childNodes,
                            evaluateConditions);
                    }

                    if (childNodes.Count > 0)
                    {
                        // Sub-branches exist downstream (e.g. multi-step player choices)
                        results.AddRange(childNodes);
                    }
                    else if (!string.IsNullOrWhiteSpace(resolvedText))
                    {
                        // Leaf player option node
                        results.Add(new DialogOptionNode
                        {
                            Text = resolvedText,
                            InputToken = line.InputToken,
                            OutputToken = line.OutputToken,
                            ConsequenceDelegate = CombineDelegates(pathConsequences),
                            ConditionDelegate = line.ConditionDelegate,
                            ClickableConditionDelegate = line.ClickableConditionDelegate
                        });
                    }
                }
                else
                {
                    var nextConsequences = new List<Delegate>(accumulatedConsequences);
                    if (line.ConsequenceDelegate != null)
                    {
                        nextConsequences.Add(line.ConsequenceDelegate);
                    }

                    if (!string.IsNullOrWhiteSpace(line.OutputToken) && linesByInToken.ContainsKey(line.OutputToken))
                    {
                        TraverseGraphDFS(
                            line.OutputToken,
                            linesByInToken,
                            new HashSet<string>(visitedTokens, StringComparer.OrdinalIgnoreCase),
                            nextConsequences,
                            results,
                            evaluateConditions);
                    }
                }
            }
        }

        private static void TraverseDownstreamDFS(
            string currentToken,
            Dictionary<string, List<RawDialogLine>> linesByInToken,
            HashSet<string> visitedTokens,
            List<Delegate> accumulatedConsequences,
            List<DialogOptionNode> results,
            bool evaluateConditions)
        {
            if (string.IsNullOrWhiteSpace(currentToken) || visitedTokens.Contains(currentToken))
                return;

            visitedTokens.Add(currentToken);

            if (!linesByInToken.TryGetValue(currentToken, out var lines) || lines.Count == 0)
                return;

            // Check if there are multiple player options at this token (branch point)
            bool isBranchPoint = lines.Count(l => l.ByPlayer && !string.IsNullOrWhiteSpace(l.Text)) > 1;

            if (isBranchPoint)
            {
                TraverseGraphDFS(
                    currentToken,
                    linesByInToken,
                    visitedTokens,
                    accumulatedConsequences,
                    results,
                    evaluateConditions);
                return;
            }

            foreach (var line in lines)
            {
                if (evaluateConditions && line.ConditionDelegate != null)
                {
                    try
                    {
                        bool passed = (bool)line.ConditionDelegate.DynamicInvoke();
                        if (!passed) continue;
                    }
                    catch { continue; }
                }

                if (line.ConsequenceDelegate != null)
                {
                    accumulatedConsequences.Add(line.ConsequenceDelegate);
                }

                if (!string.IsNullOrWhiteSpace(line.OutputToken) && linesByInToken.ContainsKey(line.OutputToken))
                {
                    TraverseDownstreamDFS(
                        line.OutputToken,
                        linesByInToken,
                        new HashSet<string>(visitedTokens, StringComparer.OrdinalIgnoreCase),
                        accumulatedConsequences,
                        results,
                        evaluateConditions);
                }
            }
        }

        private static Delegate? FindFirstDownstreamConsequence(
            string currentToken,
            Dictionary<string, List<RawDialogLine>> linesByInToken,
            HashSet<string> visited,
            bool evaluateConditions)
        {
            if (string.IsNullOrWhiteSpace(currentToken) || visited.Contains(currentToken))
                return null;

            visited.Add(currentToken);

            if (!linesByInToken.TryGetValue(currentToken, out var lines))
                return null;

            foreach (var line in lines)
            {
                if (evaluateConditions && line.ConditionDelegate != null)
                {
                    try
                    {
                        bool passed = (bool)line.ConditionDelegate.DynamicInvoke();
                        if (!passed) continue;
                    }
                    catch { continue; }
                }

                if (line.ConsequenceDelegate != null)
                    return line.ConsequenceDelegate;

                if (!string.IsNullOrWhiteSpace(line.OutputToken))
                {
                    var found = FindFirstDownstreamConsequence(line.OutputToken, linesByInToken, visited, evaluateConditions);
                    if (found != null) return found;
                }
            }

            return null;
        }

        public static Delegate? FindFirstDownstreamConsequence(DialogFlow? flow, bool evaluateConditions = false)
        {
            if (flow == null) return null;
            try
            {
                var linesField = typeof(DialogFlow).GetField("Lines", Flags) ?? typeof(DialogFlow).GetField("_lines", Flags);
                var lines = linesField?.GetValue(flow) as IEnumerable;
                if (lines == null) return null;

                var linesByInToken = new Dictionary<string, List<RawDialogLine>>(StringComparer.OrdinalIgnoreCase);
                var outputTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? firstToken = null;

                foreach (var lineObj in lines)
                {
                    if (lineObj == null) continue;
                    var lineType = lineObj.GetType();

                    var inToken = (lineType.GetField("InputToken", Flags) ?? lineType.GetField("_inputToken", Flags))?.GetValue(lineObj) as string ?? string.Empty;
                    var outToken = (lineType.GetField("OutputToken", Flags) ?? lineType.GetField("_outputToken", Flags))?.GetValue(lineObj) as string ?? string.Empty;
                    var consDel = (lineType.GetField("ConsequenceDelegate", Flags) ?? lineType.GetField("_consequenceDelegate", Flags))?.GetValue(lineObj) as Delegate;
                    var condDel = (lineType.GetField("ConditionDelegate", Flags) ?? lineType.GetField("_conditionDelegate", Flags))?.GetValue(lineObj) as Delegate;

                    if (firstToken == null && !string.IsNullOrWhiteSpace(inToken))
                    {
                        firstToken = inToken;
                    }

                    var raw = new RawDialogLine
                    {
                        InputToken = inToken,
                        OutputToken = outToken,
                        ConsequenceDelegate = consDel,
                        ConditionDelegate = condDel
                    };

                    if (!string.IsNullOrWhiteSpace(inToken))
                    {
                        if (!linesByInToken.TryGetValue(inToken, out var list))
                        {
                            list = new List<RawDialogLine>();
                            linesByInToken[inToken] = list;
                        }
                        list.Add(raw);
                    }
                    if (!string.IsNullOrWhiteSpace(outToken))
                    {
                        outputTokens.Add(outToken);
                    }
                }

                var rootTokens = linesByInToken.Keys.Where(tok => !outputTokens.Contains(tok)).ToList();
                if (rootTokens.Count == 0 && firstToken != null)
                {
                    rootTokens.Add(firstToken);
                }

                foreach (var root in rootTokens)
                {
                    var found = FindFirstDownstreamConsequence(root, linesByInToken, new HashSet<string>(StringComparer.OrdinalIgnoreCase), evaluateConditions);
                    if (found != null) return found;
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestDialogTreeBridge] Error finding downstream consequence from DialogFlow: {ex.Message}");
            }
            return null;
        }

        public static void ExecuteQuestAcceptance(QuestBase? realQuest, DialogOptionNode? branch)
        {
            if (realQuest == null) return;

            EnsureQuestDialogs(realQuest);
            bool consequenceExecuted = false;

            var beforeEndOneShot = GetConversationEndOneShot();

            try
            {
                var offerFlowField = typeof(QuestBase).GetField("OfferDialogFlow", Flags) ?? typeof(QuestBase).GetField("_offerDialogFlow", Flags);
                var offerFlow = offerFlowField?.GetValue(realQuest) as DialogFlow;
                if (offerFlow != null)
                {
                    Delegate? targetConsDel = null;

                    // 1. Check if realQuest has player options that match branch index (for multi-branch acceptance flows)
                    var realOptions = ExtractOptions(offerFlow, evaluateConditions: false, quest: realQuest);
                    if (branch != null && realOptions.Count > branch.Index && branch.Index >= 0)
                    {
                        targetConsDel = realOptions[branch.Index].ConsequenceDelegate;
                    }

                    // 2. For standard single-path quests (~80% vanilla quests), find downstream ConsequenceDelegate on the live realQuest
                    if (targetConsDel == null)
                    {
                        targetConsDel = FindFirstDownstreamConsequence(offerFlow, evaluateConditions: false);
                    }

                    if (targetConsDel != null)
                    {
                        targetConsDel.DynamicInvoke();
                        consequenceExecuted = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[QuestDialogTreeBridge] Error executing OfferDialogFlow consequence for quest {realQuest.StringId}: {ex.Message}", ex);
            }

            // 3. Fallback: reflection on QuestAcceptedConsequences method
            if (!consequenceExecuted)
            {
                try
                {
                    var acceptMethod = realQuest.GetType().GetMethod("QuestAcceptedConsequences", Flags);
                    if (acceptMethod != null)
                    {
                        acceptMethod.Invoke(realQuest, null);
                        consequenceExecuted = true;
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[QuestDialogTreeBridge] Error invoking QuestAcceptedConsequences method directly on {realQuest.StringId}: {ex.Message}", ex);
                }
            }

            // 4. Drain ConversationEndOneShot
            try
            {
                var afterEndOneShot = GetConversationEndOneShot();
                if (afterEndOneShot != null && afterEndOneShot != beforeEndOneShot)
                {
                    ClearConversationEndOneShot();
                    afterEndOneShot.Invoke();
                }
            }
            catch { }

            // 5. Verification
            if (!realQuest.IsOngoing)
            {
                ModLog.Warn($"[QuestDialogTreeBridge] Quest {realQuest.StringId} did not transition to Ongoing after acceptance consequences. Consequence executed: {consequenceExecuted}");
            }
        }

        /// <summary>
        /// Dedicated extraction method for Quest Discuss/Report options.
        /// Extracts reachable player option branches from DiscussDialogFlow that have actionable consequences,
        /// and captures terminal NPC consequence paths (for quests like Vlandian Gang Leader with pure NPC completion lines).
        /// </summary>
        public static List<DialogOptionNode> ExtractReportOptions(QuestBase? quest, Hero? npc = null)
        {
            var options = new List<DialogOptionNode>();
            if (quest == null) return options;

            EnsureQuestDialogs(quest);
            var discussFlowField = typeof(QuestBase).GetField("DiscussDialogFlow", Flags) ?? typeof(QuestBase).GetField("_discussDialogFlow", Flags);
            var discussFlow = discussFlowField?.GetValue(quest) as DialogFlow;
            if (discussFlow == null) return options;

            var rawOptions = ExtractOptions(discussFlow, evaluateConditions: true, quest: quest, npc: npc);
            foreach (var opt in rawOptions)
            {
                // Only include actionable branches that execute state-changing consequences in the world
                if (opt != null && opt.ConsequenceDelegate != null)
                {
                    options.Add(opt);
                }
            }

            // If DFS found 0 player branches (e.g. terminal NPC consequence like GangLeaderNeedsRecruits),
            // capture the downstream ConsequenceDelegate on discussFlow.
            if (options.Count == 0)
            {
                var downstreamCons = FindFirstDownstreamConsequence(discussFlow, evaluateConditions: true);
                if (downstreamCons != null)
                {
                    options.Add(new DialogOptionNode
                    {
                        Index = 0,
                        Text = new TextObject("{=ImmersiveAI_ReportCompletion}Report completion and settle the matter.").ToString(),
                        ConsequenceDelegate = downstreamCons,
                        IsAvailable = true,
                        Kind = SolutionKind.DirectQuest
                    });
                }
            }

            return options;
        }

        public static bool HasActionableReportBranches(QuestBase? quest, Hero? npc = null)
        {
            if (quest == null || quest.IsFinalized) return false;
            var options = ExtractReportOptions(quest, npc);
            return options.Any(o => o != null && o.IsAvailable && o.ConsequenceDelegate != null);
        }

        /// <summary>
        /// Unified extraction method for Issue Offer options (Single Source of Truth).
        /// Combines precondition checking, DFS DialogFlow extraction, DirectQuest fallback,
        /// Companion Dispatch (Alternative Solution), and Lord Solution.
        /// </summary>
        public static List<DialogOptionNode> ExtractOfferOptions(IssueBase issue, Hero? npc = null)
        {
            var options = new List<DialogOptionNode>();
            if (issue == null) return options;

            var targetHero = npc ?? issue.IssueOwner;

            // Precondition check for player taking quest personally
            bool canTakePersonally = true;
            string takePersonallyUnavailableReason = string.Empty;
            try
            {
                if (targetHero != null)
                {
                    if (CheckPreconditionsMethod != null)
                    {
                        object[] args = new object[] { targetHero, new TextObject(string.Empty) };
                        canTakePersonally = (bool)CheckPreconditionsMethod.Invoke(issue, args);
                        if (!canTakePersonally && args[1] is TextObject explanation && !string.IsNullOrWhiteSpace(explanation.ToString()))
                        {
                            takePersonallyUnavailableReason = explanation.ToString();
                        }
                    }
                    else if (CanPlayerTakeQuestConditionsMethod != null)
                    {
                        object[] args = new object[] { targetHero, null!, null!, null!, 0 };
                        canTakePersonally = (bool)CanPlayerTakeQuestConditionsMethod.Invoke(issue, args);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestDialogTreeBridge] Error evaluating CheckPreconditions: {ex.Message}");
            }

            if (!canTakePersonally && string.IsNullOrWhiteSpace(takePersonallyUnavailableReason))
            {
                takePersonallyUnavailableReason = "Requirements not met to undertake personally.";
            }

            QuestBase? previewQuest = issue.IssueQuest;
            bool isTemporaryPreview = false;
            if (previewQuest == null)
            {
                previewQuest = CreateTemporaryPreviewQuest(issue);
                isTemporaryPreview = true;
            }

            DialogFlow? offerFlow = null;
            if (previewQuest != null)
            {
                EnsureQuestDialogs(previewQuest);
                var offerFlowField = typeof(QuestBase).GetField("OfferDialogFlow", Flags) ?? typeof(QuestBase).GetField("_offerDialogFlow", Flags);
                offerFlow = offerFlowField?.GetValue(previewQuest) as DialogFlow;
            }

            try
            {
                options = ExtractOptions(offerFlow, evaluateConditions: true, quest: previewQuest, npc: targetHero);
            }
            finally
            {
                if (isTemporaryPreview && previewQuest != null)
                {
                    DestroyTemporaryPreviewQuest(issue, previewQuest);
                }
            }

            foreach (var opt in options)
            {
                opt.Kind = SolutionKind.DirectQuest;
                if (!canTakePersonally)
                {
                    opt.IsAvailable = false;
                    opt.UnavailableReason = takePersonallyUnavailableReason;
                }
            }

            // If DFS didn't find any specific direct quest branches (common for standard issues),
            // add the standard direct quest option from IssueBase using authentic native dialogue.
            if (!options.Any(o => o.Kind == SolutionKind.DirectQuest))
            {
                var acceptText = issue.IssueQuestSolutionAcceptByPlayer?.ToString();
                if (string.IsNullOrWhiteSpace(acceptText))
                {
                    acceptText = new TextObject("{=ImmersiveAI_TakeQuestPersonally}I will take care of this matter myself.").ToString();
                }

                options.Insert(0, new DialogOptionNode
                {
                    Index = 0,
                    Text = acceptText,
                    IsAvailable = canTakePersonally,
                    UnavailableReason = canTakePersonally ? string.Empty : takePersonallyUnavailableReason,
                    Kind = SolutionKind.DirectQuest
                });
            }

            // Extract Companion Dispatch (Alternative Solution) if supported
            if (issue.IsThereAlternativeSolution)
            {
                TextObject explanation;
                bool available = true;
                try
                {
                    available = issue.AlternativeSolutionCondition(out explanation);
                }
                catch { available = true; explanation = new TextObject(string.Empty); }

                // Check if player actually has an available companion hero in the main party
                bool hasAvailableCompanion = Clan.PlayerClan?.Companions != null &&
                    Clan.PlayerClan.Companions.Any(c => c.IsAlive && c.PartyBelongedTo == MobileParty.MainParty && !c.IsPrisoner && c.CanHaveCampaignIssues());

                if (!hasAvailableCompanion)
                {
                    available = false;
                    explanation = new TextObject("{=ImmersiveAI_NoCompanion}You have no available companions in your party.");
                }

                string altText = issue.IssueAlternativeSolutionAcceptByPlayer?.ToString();
                if (string.IsNullOrWhiteSpace(altText))
                {
                    int menCount = 0;
                    int durationDays = 0;
                    try
                    {
                        menCount = issue.GetTotalAlternativeSolutionNeededMenCount();
                        durationDays = issue.GetTotalAlternativeSolutionDurationInDays();
                    }
                    catch { }

                    altText = durationDays > 0 && menCount > 0
                        ? new TextObject("{=ImmersiveAI_SendCompanion}Assign a companion and {MEN_COUNT} troops to resolve this ({DURATION} days).")
                            .SetTextVariable("MEN_COUNT", menCount)
                            .SetTextVariable("DURATION", durationDays)
                            .ToString()
                        : "Assign a companion and troops to resolve this task.";
                }

                options.Add(new DialogOptionNode
                {
                    Index = options.Count,
                    Text = altText,
                    IsAvailable = available,
                    UnavailableReason = explanation?.ToString() ?? string.Empty,
                    Kind = SolutionKind.CompanionDispatch
                });
            }

            // Extract Lord Solution if supported
            if (issue.IsThereLordSolution)
            {
                TextObject lordExplanation;
                bool lordAvailable = false;
                try
                {
                    lordAvailable = issue.LordSolutionCondition(out lordExplanation);
                }
                catch { lordExplanation = new TextObject(string.Empty); }

                string lordText = issue.IssueLordSolutionAcceptByPlayer?.ToString();
                if (string.IsNullOrWhiteSpace(lordText))
                {
                    int influence = 0;
                    try
                    {
                        influence = issue.NeededInfluenceForLordSolution;
                    }
                    catch { }

                    lordText = influence > 0
                        ? new TextObject("{=ImmersiveAI_LordSolution}Issue a ruler's decree to resolve the matter ({INFLUENCE} influence).")
                            .SetTextVariable("INFLUENCE", influence)
                            .ToString()
                        : "Issue a ruler's decree to resolve the matter.";
                }

                options.Add(new DialogOptionNode
                {
                    Index = options.Count,
                    Text = lordText,
                    IsAvailable = lordAvailable,
                    UnavailableReason = lordExplanation?.ToString() ?? string.Empty,
                    Kind = SolutionKind.LordSolution
                });
            }

            return options;
        }

        /// <summary>
        /// Handles tool calls from the LLM.
        /// Strictly adheres to the confirmation blocker law: validates preconditions and populates the tally,
        /// laying the offer or handover before the player without modifying game state directly.
        /// </summary>
        public static string ResolveToolCall(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? tally)
        {
            if (call == null || npc == null) return "The moment does not allow it; I let the matter rest.";

            try
            {
                if (call.Name == QuestTool.OfferQuest || call.Name == QuestTool.AcceptQuest)
                {
                    if (tally == null)
                        return "This is not the moment for taking on tasks — I let the talk carry on.";
                    if (tally.Laid)
                        return "The offer already lies before them, laid this very breath — theirs to choose or let lie.";

                    var issue = QuestTool.GetAvailableIssue(npc);
                    if (issue == null)
                        return "I have no open task or trouble to lay before them just now; I speak of other matters.";

                    var options = ExtractOfferOptions(issue, npc);

                    tally.Laid = true;
                    tally.IsReport = false;
                    tally.Npc = npc;
                    tally.AcceptedIssue = issue;
                    tally.ReportedQuest = null;
                    tally.Branches = options;

                    return "The task and its terms lie before the traveler. Describe the situation, state what is required, and invite their choice. Nothing is begun until they seal their choice in the world.";
                }

                if (call.Name == QuestTool.ReportQuest)
                {
                    if (tally == null)
                        return "This is not the moment for quest reports — I let the talk carry on.";
                    if (tally.Laid)
                        return "The handover already lies before them to confirm or choose; I need not lay it twice.";

                    var quest = QuestTool.GetActiveQuest(npc);
                    if (quest == null)
                        return "There is no ongoing task between us to report upon; I speak of other matters.";

                    var options = ExtractReportOptions(quest, npc);

                    tally.Laid = true;
                    tally.IsReport = true;
                    tally.Npc = npc;
                    tally.ReportedQuest = quest;
                    tally.Branches = options;

                    return "TOOL LAYS TERMS. Inspection/verification terms lie before the traveler. Until their choice is confirmed in the world, nothing has changed hands. Speak of inspecting or examining what is brought (e.g. 'Let me take a look'); do not declare the transaction completed or hand over payment in this breath.";
                }

                return "I let the matter rest.";
            }
            catch (Exception ex)
            {
                ModLog.Error($"[QuestDialogTreeBridge] Error resolving tool call {call.Name}: {ex.Message}", ex);
                return "The moment does not allow it; I let the matter rest.";
            }
        }

        public static string FormatOptionsPrompt(List<DialogOptionNode> options, string header = "Available dialogue branches:")
        {
            if (options == null || options.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var opt in options)
            {
                if (string.IsNullOrWhiteSpace(opt.Text)) continue;
                string status = opt.IsAvailable ? "[AVAILABLE]" : $"[UNAVAILABLE: {opt.UnavailableReason}]";
                sb.AppendLine($"- \"{opt.Text}\" {status}");
            }
            return sb.ToString().TrimEnd();
        }

        public static string GetUniversalConversationGuidance(bool isReporting)
        {
            var sb = new StringBuilder();
            sb.AppendLine("QUEST INTERACTION RULES (LAY VS. SEAL PROTOCOL):");
            sb.AppendLine("1. The dialogue branches above represent the TRAVELER'S available choices.");
            if (isReporting)
            {
                sb.AppendLine("2. When the traveler reports progress or proposes delivering upon the ongoing task in this turn, call report_quest to lay the formal resolution choices before them.");
                sb.AppendLine("3. Calling report_quest enters the INSPECTION / PROPOSAL phase (Uncommitted State) — speak only of inspecting, checking, or examining the matter (e.g. 'Let me inspect what you brought'); strictly do NOT narrate receiving the items or hand over payment in the same breath you call report_quest.");
                sb.AppendLine("4. Once the traveler confirms the resolution via popup, the physical transfer occurs in the world, and you will receive the confirmed outcome in the follow-up turn to deliver your genuine gratitude and closing settlement.");
            }
            else
            {
                sb.AppendLine("2. When explaining, introducing, or proposing an unundertaken task to the traveler, call offer_quest in that turn to lay the formal choices before them.");
                sb.AppendLine("3. Calling offer_quest only LAYS the task on the table (Uncommitted State) — nothing is begun until the traveler confirms via popup. Do not treat the agreement as sealed until their confirmed choice is received in the following turn.");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
