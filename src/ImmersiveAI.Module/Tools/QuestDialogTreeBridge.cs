using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ImmersiveAI.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// Universal Dialogue Tree Bridge Engine (Single Source of Truth):
    /// Dynamically inspects TaleWorlds DialogFlow trees (Offer & Discuss) across vanilla and mods,
    /// tracking state-machine token graphs and option branches, evaluating conditions and clickable constraints
    /// with dynamic explanations and universal balance guards, formatting prompts for LLM with universal opening etiquette,
    /// providing live inspection verification on Tool Calls, and safely executing the chosen native Consequence delegates on the game thread.
    /// </summary>
    public static class QuestDialogTreeBridge
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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

        private static readonly PropertyInfo? IssueQuestCanBeDuplicatedProperty =
            typeof(IssueBase).GetProperty("IssueQuestCanBeDuplicated", Flags);

        private static readonly MethodInfo? CanPlayerTakeQuestConditionsMethod =
            typeof(IssueBase).GetMethod("CanPlayerTakeQuestConditions", Flags);

        public sealed class DialogOptionNode
        {
            public int Index { get; set; }
            public string Text { get; set; } = string.Empty;
            public string InputToken { get; set; } = string.Empty;
            public string OutputToken { get; set; } = string.Empty;
            public Delegate? ConsequenceDelegate { get; set; }
            public Delegate? ConditionDelegate { get; set; }
            public Delegate? ClickableConditionDelegate { get; set; }
            public string ConsequenceMethodName { get; set; } = string.Empty;
            public bool IsAvailable { get; set; } = true;
            public string UnavailableReason { get; set; } = string.Empty;
            public bool IsStandardAccept { get; set; }
            public bool IsInstantResolve { get; set; }
            public bool IsSuccess { get; set; }
            public bool IsFail { get; set; }
            public bool IsRefusal { get; set; }
            public bool IsCloseOnly { get; set; }
        }

        /// <summary>
        /// Extracts all valid player option branches from a DialogFlow instance.
        /// Evaluates ConditionDelegate to prune structurally unreachable branches for the current worldline,
        /// performs semantic classification based on state machine roles (Success vs CloseOnly vs Refusal),
        /// and evaluates ClickableConditionDelegate & universal balance guards to determine availability.
        /// </summary>
        public static List<DialogOptionNode> ExtractOptions(DialogFlow? flow, bool evaluateConditions = true, QuestBase? quest = null)
        {
            var results = new List<DialogOptionNode>();
            if (flow == null) return results;

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
                        InputToken = inToken,
                        OutputToken = outToken,
                        ConsequenceDelegate = consDel,
                        ConditionDelegate = condDel,
                        ClickableConditionDelegate = clickDel
                    });
                }

                // Traverse all valid Root-to-Leaf paths in the DialogFlow DAG
                var paths = TraverseAllPathsDFS(rawLines, evaluateConditions);

                bool isReportingTree = (quest != null);
                var finalResults = new List<DialogOptionNode>();
                int idx = 0;

                foreach (var node in paths)
                {
                    bool isAvailable = true;
                    string unavailableReason = string.Empty;

                    if (evaluateConditions)
                    {
                        // 1. Structural Visibility Filter (ConditionDelegate):
                        // If ConditionDelegate returns false, this branch does not exist in the current worldline. Prune it!
                        if (node.ConditionDelegate != null)
                        {
                            try
                            {
                                bool passed = (bool)node.ConditionDelegate.DynamicInvoke();
                                if (!passed) continue; // PRUNED: Invisible in current state!
                            }
                            catch { continue; }
                        }

                        // 2. Resource & Clickable availability check
                        isAvailable = VerifyOptionAvailability(node, quest, out unavailableReason);
                    }

                    var invokedMethods = GetInvokedMethodNamesFromDelegate(node.ConsequenceDelegate);

                    bool isRefusal = invokedMethods.Any(m => m.IndexOf("CompleteQuestWithFail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("QuestFail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("BrokeAgreement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("PlayerBroke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Refuse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Decline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0);

                    // CloseOnly: No consequence, or explicitly only CloseDialog without clickable requirement / item transfer
                    bool isCloseOnly = (node.ConsequenceDelegate == null || invokedMethods.Any(m => m.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0))
                                       && node.ClickableConditionDelegate == null && !isRefusal;

                    bool isInstant = invokedMethods.Any(m => m.IndexOf("Bought", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Instant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("PayDirectly", StringComparison.OrdinalIgnoreCase) >= 0);

                    bool isSuccess;
                    bool isStandard;

                    if (isReportingTree)
                    {
                        // In Discuss/Reporting mode:
                        // Any non-refusal, non-close-only consequence (whether named or anonymous lambda) IS a task conclusion/handover!
                        isSuccess = !isRefusal && !isCloseOnly &&
                                    (node.ConsequenceDelegate != null ||
                                     node.ClickableConditionDelegate != null ||
                                     invokedMethods.Any(m => m.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Deliver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Sold", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Paid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Finish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             m.IndexOf("Complete", StringComparison.OrdinalIgnoreCase) >= 0));
                        isStandard = false;
                    }
                    else
                    {
                        // In Offer/Acceptance mode:
                        isSuccess = isInstant;
                        isStandard = !isRefusal && !isInstant && !isCloseOnly &&
                                     (node.ConsequenceDelegate != null ||
                                      invokedMethods.Any(m => m.IndexOf("Accept", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                              m.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                              m.IndexOf("Take", StringComparison.OrdinalIgnoreCase) >= 0));
                    }

                    string primaryMethodName = invokedMethods.FirstOrDefault(m => m.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                                   m.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                                   m.IndexOf("Fail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                                   m.IndexOf("Broke", StringComparison.OrdinalIgnoreCase) >= 0)
                                               ?? node.ConsequenceDelegate?.Method?.Name ?? string.Empty;

                    node.Index = idx++;
                    node.ConsequenceMethodName = primaryMethodName;
                    node.IsAvailable = isAvailable;
                    node.UnavailableReason = unavailableReason;
                    node.IsStandardAccept = isStandard;
                    node.IsInstantResolve = isInstant;
                    node.IsRefusal = isRefusal;
                    node.IsSuccess = isSuccess;
                    node.IsFail = isRefusal;
                    node.IsCloseOnly = isCloseOnly;
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

        private sealed class RawDialogLine
        {
            public bool ByPlayer { get; set; }
            public string Text { get; set; } = string.Empty;
            public string InputToken { get; set; } = string.Empty;
            public string OutputToken { get; set; } = string.Empty;
            public Delegate? ConsequenceDelegate { get; set; }
            public Delegate? ConditionDelegate { get; set; }
            public Delegate? ClickableConditionDelegate { get; set; }
        }

        private static List<DialogOptionNode> TraverseAllPathsDFS(List<RawDialogLine> rawLines, bool evaluateConditions)
        {
            var results = new List<DialogOptionNode>();
            if (rawLines == null || rawLines.Count == 0) return results;

            var playerLines = rawLines.Where(r => r.ByPlayer).ToList();
            if (playerLines.Count == 0)
            {
                foreach (var raw in rawLines.Where(r => r.ConsequenceDelegate != null))
                {
                    results.Add(new DialogOptionNode
                    {
                        Index = results.Count,
                        Text = raw.Text,
                        InputToken = raw.InputToken,
                        OutputToken = raw.OutputToken,
                        ConditionDelegate = raw.ConditionDelegate,
                        ClickableConditionDelegate = raw.ClickableConditionDelegate,
                        ConsequenceDelegate = raw.ConsequenceDelegate
                    });
                }
                return results;
            }

            var allPlayerOutputTokens = new HashSet<string>(playerLines.Select(p => p.OutputToken).Where(t => !string.IsNullOrEmpty(t)), StringComparer.OrdinalIgnoreCase);

            var rootPlayerLines = playerLines.Where(p =>
                string.Equals(p.InputToken, "quest_discuss", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.InputToken, "issue_classic_quest_start", StringComparison.OrdinalIgnoreCase) ||
                !allPlayerOutputTokens.Contains(p.InputToken)
            ).ToList();

            if (rootPlayerLines.Count == 0)
            {
                rootPlayerLines = playerLines;
            }

            foreach (var rootLine in rootPlayerLines)
            {
                var visitedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectPathsFromNode(rootLine, rootLine.Text, rootLine.ConditionDelegate, rootLine.ClickableConditionDelegate, rootLine.ConsequenceDelegate, rawLines, visitedTokens, results, evaluateConditions);
            }

            return results;
        }

        private static void CollectPathsFromNode(
            RawDialogLine currentLine,
            string currentText,
            Delegate? accumulatedCondition,
            Delegate? accumulatedClickable,
            Delegate? accumulatedConsequence,
            List<RawDialogLine> allLines,
            HashSet<string> visitedTokens,
            List<DialogOptionNode> results,
            bool evaluateConditions)
        {
            var cond = accumulatedCondition ?? currentLine.ConditionDelegate;
            var click = accumulatedClickable ?? currentLine.ClickableConditionDelegate;
            var cons = accumulatedConsequence ?? currentLine.ConsequenceDelegate;

            // Structural prune: If node has ConditionDelegate and returns false in current worldline, drop entire path
            if (evaluateConditions && currentLine.ConditionDelegate != null)
            {
                try
                {
                    bool passed = (bool)currentLine.ConditionDelegate.DynamicInvoke();
                    if (!passed) return;
                }
                catch { return; }
            }

            if (cons != null || string.IsNullOrEmpty(currentLine.OutputToken) || !visitedTokens.Add(currentLine.OutputToken) || visitedTokens.Count > 16)
            {
                results.Add(new DialogOptionNode
                {
                    Index = results.Count,
                    Text = currentText,
                    InputToken = currentLine.InputToken,
                    OutputToken = currentLine.OutputToken,
                    ConditionDelegate = cond,
                    ClickableConditionDelegate = click,
                    ConsequenceDelegate = cons
                });
                return;
            }

            var childLines = allLines.Where(l => string.Equals(l.InputToken, currentLine.OutputToken, StringComparison.OrdinalIgnoreCase)).ToList();

            if (childLines.Count == 0)
            {
                results.Add(new DialogOptionNode
                {
                    Index = results.Count,
                    Text = currentText,
                    InputToken = currentLine.InputToken,
                    OutputToken = currentLine.OutputToken,
                    ConditionDelegate = cond,
                    ClickableConditionDelegate = click,
                    ConsequenceDelegate = cons
                });
                return;
            }

            foreach (var child in childLines)
            {
                if (child.ByPlayer)
                {
                    string combinedText = string.IsNullOrWhiteSpace(currentText)
                        ? child.Text
                        : $"{currentText} ({child.Text})";

                    var childVisited = new HashSet<string>(visitedTokens, StringComparer.OrdinalIgnoreCase);
                    CollectPathsFromNode(child, combinedText, cond, click, cons, allLines, childVisited, results, evaluateConditions);
                }
                else
                {
                    var childVisited = new HashSet<string>(visitedTokens, StringComparer.OrdinalIgnoreCase);
                    CollectPathsFromNode(child, currentText, cond, click, cons, allLines, childVisited, results, evaluateConditions);
                }
            }
        }

        /// <summary>
        /// Single Pure Verification Engine:
        /// Verifies whether an active visible option branch is currently available by evaluating
        /// universal balance/inventory checks FIRST (for rich, exact numeric reasoning) and clickable conditions.
        /// </summary>
        public static bool VerifyOptionAvailability(DialogOptionNode node, QuestBase? quest, out string unavailableReason)
        {
            unavailableReason = string.Empty;
            if (node == null) return false;

            // 1. Universal Balance & Inventory Guard FIRST (provides exact amounts & deficits)
            if (!CheckUniversalOptionRequirements(node, quest, out string reqReason))
            {
                unavailableReason = reqReason;
                return false;
            }

            // 2. Clickable condition delegate
            if (node.ClickableConditionDelegate != null)
            {
                try
                {
                    var expText = new TextObject(string.Empty);
                    object[] args = new object[] { expText };
                    bool clickable = (bool)node.ClickableConditionDelegate.DynamicInvoke(args);
                    if (!clickable)
                    {
                        unavailableReason = args[0] is TextObject t && !string.IsNullOrWhiteSpace(t.ToString())
                            ? t.ToString()
                            : "Requirements not met";
                        return false;
                    }
                }
                catch { }
            }

            return true;
        }

        /// <summary>
        /// Universal Requirements Scanner (0 Hardcoding):
        /// Scans option text and dynamic quest fields for required gold or delivery item counts,
        /// calculating exact current possession and shortfall deficits.
        /// </summary>
        public static bool CheckUniversalOptionRequirements(DialogOptionNode node, QuestBase? quest, out string reason)
        {
            reason = string.Empty;
            if (node == null || Hero.MainHero == null) return true;

            // 1. Universal Gold Requirement Scanner: matches amounts like "275 denars", "275<img...", "275 gold", "275 coins"
            var match = Regex.Match(node.Text, @"(\d+)\s*(?:<img|denar|gold|coin)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int reqGold) && reqGold > 0)
            {
                int currentGold = Hero.MainHero.Gold;
                if (currentGold < reqGold)
                {
                    reason = $"Requires {reqGold} denars; traveler currently holds {currentGold} denars";
                    return false;
                }
            }

            // 2. Quest-level delivery item / gold checks
            if (quest != null)
            {
                try
                {
                    var qType = quest.GetType();
                    var invoked = GetInvokedMethodNamesFromDelegate(node.ConsequenceDelegate);
                    bool isPayAction = node.IsSuccess ||
                                       invoked.Any(m => m.IndexOf("Paid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                        m.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                        m.IndexOf("Sold", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (isPayAction)
                    {
                        var targetGoldField = qType.GetField("_targetDenarsToAchieve", Flags) ?? qType.GetField("_targetDenars", Flags);
                        if (targetGoldField != null)
                        {
                            int targetGold = Convert.ToInt32(targetGoldField.GetValue(quest));
                            if (targetGold > 0 && Hero.MainHero.Gold < targetGold)
                            {
                                reason = $"Requires {targetGold} denars; traveler currently holds {Hero.MainHero.Gold} denars";
                                return false;
                            }
                        }
                    }
                }
                catch { }
            }

            return true;
        }

        /// <summary>
        /// Deep IL Bytecode Call Inspector:
        /// Traverses method body bytecode instructions (OpCodes.Call, Callvirt, Newobj, Ldftn, Ldvirtftn)
        /// to resolve all underlying method names invoked inside compiler-generated lambdas and delegates.
        /// </summary>
        public static HashSet<string> GetInvokedMethodNamesFromDelegate(Delegate? del)
        {
            var methodNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (del == null) return methodNames;

            try
            {
                var method = del.Method;
                methodNames.Add(method.Name);

                InspectMethodBodyIL(method, methodNames, depth: 0);
            }
            catch { }

            return methodNames;
        }

        private static void InspectMethodBodyIL(System.Reflection.MethodInfo? method, HashSet<string> methodNames, int depth)
        {
            if (method == null || depth > 2) return;

            try
            {
                var body = method.GetMethodBody();
                if (body == null) return;

                var il = body.GetILAsByteArray();
                if (il == null || il.Length == 0) return;

                var module = method.Module;
                for (int i = 0; i < il.Length - 4; i++)
                {
                    byte op = il[i];
                    int token = 0;

                    // 0x28 = OpCodes.Call, 0x6F = OpCodes.Callvirt, 0x73 = OpCodes.Newobj
                    if (op == 0x28 || op == 0x6F || op == 0x73)
                    {
                        token = BitConverter.ToInt32(il, i + 1);
                    }
                    // 0xFE prefix for two-byte opcodes: 0xFE 0x06 = Ldftn, 0xFE 0x07 = Ldvirtftn
                    else if (op == 0xFE && i < il.Length - 5)
                    {
                        byte subOp = il[i + 1];
                        if (subOp == 0x06 || subOp == 0x07)
                        {
                            token = BitConverter.ToInt32(il, i + 2);
                        }
                    }

                    if (token != 0)
                    {
                        try
                        {
                            var resolvedMethod = module.ResolveMethod(token) as System.Reflection.MethodInfo;
                            if (resolvedMethod != null)
                            {
                                if (methodNames.Add(resolvedMethod.Name))
                                {
                                    // If resolved method is another compiler-generated closure method (e.g. <SetDialogs>b__), recurse
                                    if (resolvedMethod.Name.IndexOf("b__", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        resolvedMethod.Name.IndexOf("<", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        InspectMethodBodyIL(resolvedMethod, methodNames, depth + 1);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Universal Conversation & Action Guidance (Single Source of Truth):
        /// Pure event-driven direction: 0 behavioral interference, 0 emotion forcing, clear branch ownership.
        /// </summary>
        public static string GetUniversalConversationGuidance(bool isReporting)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Conversation & Action Rules:");
            sb.AppendLine("1. Dialogue Branch Ownership:");
            sb.AppendLine("   The dialogue branches listed above represent the TRAVELER'S potential choices or responses, NOT your own lines. Do NOT speak, quote, or assume the traveler's option text as your own words.");
            sb.AppendLine("2. Physical World Gate & Action Execution:");
            sb.AppendLine("   Spoken words alone CANNOT modify the state of the world or transfer goods/gold.");
            if (isReporting)
            {
                sb.AppendLine("   - Merely reporting that a deed was done (such as goods being sold or enemies encountered) is a status report, NOT a handover.");
                sb.AppendLine("   - Each available branch above represents a distinct in-game decision or physical action. When the traveler's spoken words or physical actions align with the decision of an available branch, you MUST invoke report_quest with that branch's option_index to execute the action in the world.");
                sb.AppendLine("   - If the traveler has not taken or committed to any of the available branches (such as casual chatting, joking, or undecided discussion), do NOT invoke tools; respond naturally in dialogue.");
            }
            else
            {
                sb.AppendLine("   - When the traveler's spoken words or physical actions align with accepting a task or choosing an agreement branch, you MUST invoke accept_quest with that branch's option_index.");
                sb.AppendLine("   - If the traveler is merely inquiring, hesitating, or discussing possibilities, do NOT invoke tools; respond naturally in dialogue.");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Strict Deterministic Option Index Resolver:
        /// - If explicitIndex is passed and valid (and not CloseOnly), returns explicitIndex.
        /// - If no explicitIndex, checks if there is EXACTLY ONE available non-close-only option in the tree. If so, returns that single option.
        /// - If multiple available options exist or none exist, returns -1 (Refuses ambiguous execution, 0 blind guessing).
        /// </summary>
        public static int ResolveOptionIndex(List<DialogOptionNode>? options, int explicitIndex, bool hasExplicitIndex, bool isReporting, bool isReneged = false)
        {
            if (options == null || options.Count == 0) return -1;

            if (hasExplicitIndex && explicitIndex >= 0 && explicitIndex < options.Count)
            {
                var explicitNode = options[explicitIndex];
                if (explicitNode.IsCloseOnly) return -1; // CloseOnly options cannot conclude or accept quests!
                return explicitIndex;
            }

            // Single Available Option Direct Pass:
            // When there is only 1 available active actionable option in the entire tree (e.g. standard accept for 90% of quests),
            // safely execute it without ambiguity.
            var availableActionable = options.FindAll(o => o.IsAvailable && !o.IsCloseOnly);
            if (availableActionable.Count == 1)
            {
                return availableActionable[0].Index;
            }

            // Ambiguous multiple options or 0 options: strictly return -1 (0 guessing)
            return -1;
        }

        /// <summary>
        /// Formats extracted options into structured guidance for the LLM with clear availability status and reasons.
        /// </summary>
        public static string FormatOptionsPrompt(List<DialogOptionNode> options, string header = "Available dialogue resolution branches:")
        {
            if (options == null || options.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var opt in options)
            {
                string desc = !string.IsNullOrWhiteSpace(opt.Text) ? opt.Text : opt.ConsequenceMethodName;
                string status = opt.IsAvailable ? "[AVAILABLE]" : $"[UNAVAILABLE: {opt.UnavailableReason}]";
                string type = opt.IsStandardAccept ? "Standard Task Agreement"
                    : (opt.IsInstantResolve ? "Direct Cash Buyout / Alternative"
                    : (opt.IsSuccess ? "Success Handover / Completion"
                    : (opt.IsRefusal ? "Refusal / Breach"
                    : (opt.IsCloseOnly ? "Checking in / Inquire only" : "Dialogue Branch"))));

                sb.AppendLine($"- option {opt.Index}: {status} ({type}) \"{desc}\"");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Single Unified Quest Tool Call Resolver:
        /// Automatically handles both accept_quest and report_quest with live inspection verification,
        /// returning synchronous, roleplay-ready tool responses to the LLM and binding game-thread executions.
        /// </summary>
        public static string ResolveToolCall(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? tally)
        {
            if (tally == null || npc == null) return "No quest context available.";

            bool isReporting = string.Equals(call.Name, QuestTool.ReportQuest, StringComparison.OrdinalIgnoreCase);

            int optionIndex = 0;
            bool hasExplicitIndex = false;
            bool isReneged = false;

            if (!string.IsNullOrWhiteSpace(call.ArgumentsJson))
            {
                try
                {
                    var jObj = Newtonsoft.Json.Linq.JObject.Parse(call.ArgumentsJson);
                    if (jObj.TryGetValue("option_index", out var optToken) && optToken.Type != Newtonsoft.Json.Linq.JTokenType.Null)
                    {
                        optionIndex = Convert.ToInt32(optToken);
                        hasExplicitIndex = true;
                    }
                }
                catch { }

                var json = call.ArgumentsJson;
                if (json.IndexOf("renege", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    json.IndexOf("refuse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    json.IndexOf("broke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    json.IndexOf("betray", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isReneged = true;
                }
            }

            if (isReporting)
            {
                var activeQuest = QuestTool.GetActiveQuest(npc);
                var questTitle = activeQuest?.Title?.ToString() ?? "Unknown";
                ModLog.Info($"[QuestBridge] LLM called report_quest for {npc.Name} (Active quest: '{questTitle}')");

                if (activeQuest == null)
                {
                    ModLog.Warn($"[QuestBridge] report_quest called for {npc.Name}, but no active quest was found.");
                    return "No ongoing task was found.";
                }

                var flowField = typeof(QuestBase).GetField("DiscussDialogFlow", Flags);
                var discussFlow = flowField?.GetValue(activeQuest) as DialogFlow;
                // Evaluate conditions: prunes unreachable worldlines, leaving only active options
                var options = ExtractOptions(discussFlow, evaluateConditions: true, quest: activeQuest);

                if (options.Count == 0)
                {
                    return "No dialogue resolution branches available.";
                }

                int chosenIdx = ResolveOptionIndex(options, optionIndex, hasExplicitIndex, isReporting: true, isReneged: isReneged);
                if (chosenIdx < 0 || chosenIdx >= options.Count)
                {
                    tally.ReportedQuest = null;
                    tally.CompletionDelegate = null;
                    ModLog.Warn($"[QuestBridge] No valid completion branch matched for {npc.Name} (report_quest).");
                    return "TASK RESOLUTION AMBIGUOUS / FAILED: Multiple resolution branches are available. You MUST pass the specific option_index corresponding to the traveler's stated commitment. If the traveler made no final decision, respond in dialogue without calling tools.";
                }

                var chosenNode = options[chosenIdx];

                // Live inspection verification
                bool isAvailable = VerifyOptionAvailability(chosenNode, activeQuest, out string failReason);
                if (!isAvailable)
                {
                    tally.ReportedQuest = null;
                    tally.CompletionDelegate = null;
                    ModLog.Warn($"[QuestBridge] Live inspection blocked report_quest for {npc.Name} on option {chosenIdx} ('{chosenNode.Text}'): {failReason}");
                    return $"TASK CONCLUDE FAILED: Requirements not met ({failReason}). The required conditions or deliverables are not met in the world. React naturally in your own authentic voice and persona.";
                }

                tally.Npc = npc;
                tally.ReportedQuest = activeQuest;
                tally.OptionIndex = chosenIdx;
                tally.HasExplicitOptionIndex = hasExplicitIndex;
                tally.IsReneged = isReneged || chosenNode.IsFail || chosenNode.IsRefusal;
                tally.CompletionDelegate = () => ExecuteOption(options, chosenIdx, true, out _, activeQuest);

                ModLog.Info($"[QuestBridge] Live inspection passed for report option {chosenIdx} ('{chosenNode.Text}'). Binding execution.");
                return tally.IsReneged
                    ? "I acknowledge their refusal/breach of agreement with anger or outrage, and hold them to account. I speak on in my authentic words."
                    : "TASK CONCLUDED SUCCESSFULLY: The goods, payment, or deeds have been verified and received in full. Settle the account and speak naturally in your own authentic voice and persona.";
            }
            else
            {
                var issue = QuestTool.GetAvailableIssue(npc);
                var issueTitle = issue?.Title?.ToString() ?? "Unknown";
                ModLog.Info($"[QuestBridge] LLM called accept_quest for {npc.Name} (Available issue: '{issueTitle}')");

                if (issue == null)
                {
                    ModLog.Warn($"[QuestBridge] accept_quest called for {npc.Name}, but no available issue was found.");
                    return "No pending task/issue is available from this person.";
                }

                // Check TaleWorlds Issue Preconditions
                int flagsInt = 0;
                try
                {
                    if (CanPlayerTakeQuestConditionsMethod != null)
                    {
                        var expText = new TextObject(string.Empty);
                        object[] args = new object[] { Hero.MainHero, null, expText };
                        bool canTake = (bool)CanPlayerTakeQuestConditionsMethod.Invoke(issue, args);
                        if (!canTake && args[1] != null)
                        {
                            flagsInt = Convert.ToInt32(args[1]);
                        }
                    }
                }
                catch { }

                if ((flagsInt & PreconditionFlagAtWar) != 0)
                    return "We are at war. I cannot give you this task.";
                if ((flagsInt & PreconditionFlagWounded) != 0)
                    return "You are too severely wounded to undertake this task.";
                if ((flagsInt & PreconditionFlagRelation) != 0)
                    return "You and I do not have a good history. I do not trust you with this business.";

                // Extract Offer branches
                QuestBase? previewQ = issue.IssueQuest;
                if (previewQ == null)
                {
                    var genMethod = issue.GetType().GetMethod("GenerateIssueQuest", Flags);
                    if (genMethod != null)
                    {
                        try { previewQ = genMethod.Invoke(issue, new object[] { (issue.StringId ?? "issue") + "_bridge_preview" }) as QuestBase; }
                        catch { }
                    }
                }

                var offerField = typeof(QuestBase).GetField("OfferDialogFlow", Flags);
                var offerFlow = offerField?.GetValue(previewQ) as DialogFlow;
                var options = ExtractOptions(offerFlow, evaluateConditions: true, quest: previewQ);

                int chosenIdx = ResolveOptionIndex(options, optionIndex, hasExplicitIndex, isReporting: false);
                if (chosenIdx >= 0 && chosenIdx < options.Count)
                {
                    var chosenNode = options[chosenIdx];
                    bool isAvailable = VerifyOptionAvailability(chosenNode, previewQ, out string failReason);
                    if (!isAvailable)
                    {
                        tally.AcceptedIssue = null;
                        ModLog.Warn($"[QuestBridge] Live inspection blocked accept_quest for {npc.Name}: {failReason}");
                        return $"TASK ACCEPTANCE BLOCKED: ({failReason}). React naturally in your own authentic voice and persona.";
                    }
                }

                tally.Npc = npc;
                tally.AcceptedIssue = issue;
                tally.OptionIndex = chosenIdx >= 0 ? chosenIdx : 0;
                tally.HasExplicitOptionIndex = hasExplicitIndex;

                bool isSoloOrSmallParty = (flagsInt & PreconditionFlagNotEnoughTroops) != 0;
                if (isSoloOrSmallParty)
                {
                    ModLog.Info($"[QuestBridge] Player taking quest with small/solo party ({npc.Name}). Granting solo player freedom with dialogue guidance.");
                    return "The agreement is struck. The task is officially given into their hands. Note: since the player rides with few or no troops, the speaker may briefly remark with caution or admiration at their daring ('You ride with only a handful of men... be cautious'), offering parting advice and blessing their journey.";
                }

                return "The agreement is struck. The task is officially given into their hands. I speak on in my own words, thanking them, giving parting advice, or noting their courage.";
            }
        }

        /// <summary>
        /// Executes the chosen option consequence delegate safely on the game thread with runtime safety checks.
        /// </summary>
        public static bool ExecuteOption(List<DialogOptionNode> options, int optionIndex, bool isReporting, out string executedMethod, QuestBase? quest = null)
        {
            executedMethod = string.Empty;
            if (options == null || options.Count == 0) return false;

            if (optionIndex < 0 || optionIndex >= options.Count)
            {
                optionIndex = ResolveOptionIndex(options, 0, false, isReporting);
                if (optionIndex < 0 || optionIndex >= options.Count) return false;
            }

            var node = options[optionIndex];

            // Re-verify on game thread before executing
            if (!VerifyOptionAvailability(node, quest, out string reqReason))
            {
                ModLog.Warn($"[QuestBridge] Blocked execution of option {optionIndex}: {reqReason}.");
                return false;
            }

            if (node.ConsequenceDelegate != null)
            {
                try
                {
                    var beforeEndOneShot = GetConversationEndOneShot();
                    node.ConsequenceDelegate.DynamicInvoke();
                    executedMethod = node.ConsequenceMethodName;
                    ModLog.Info($"[QuestBridge] Successfully executed option {optionIndex} consequence: {executedMethod}");

                    var afterEndOneShot = GetConversationEndOneShot();
                    if (afterEndOneShot != null && afterEndOneShot != beforeEndOneShot)
                    {
                        ClearConversationEndOneShot();
                        ModLog.Info($"[QuestBridge] Executing registered ConversationEndOneShot action for option {optionIndex}...");
                        afterEndOneShot.Invoke();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[QuestBridge] Error executing option {optionIndex} ({node.ConsequenceMethodName})", ex);
                }
            }

            return false;
        }

        private static Action? GetConversationEndOneShot()
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

        private static void ClearConversationEndOneShot()
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

        /// <summary>
        /// Dispatches and commits quest acceptance and reporting outcomes on the game thread.
        /// </summary>
        public static void DispatchOutcomes(QuestTool.Tally? quest, string? spokenReply = null)
        {
            if (quest == null) return;

            if (quest.AcceptedIssue != null)
            {
                var issue = quest.AcceptedIssue;
                var npc = quest.Npc ?? issue.IssueOwner;
                int chosenOptIndex = quest.OptionIndex;
                bool hasExplicit = quest.HasExplicitOptionIndex;

                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var title = issue.Title?.ToString() ?? "Issue";
                        ModLog.Info($"[QuestBridge] Formally starting quest for issue '{title}' ({npc?.Name}) via native StartIssueQuest()...");

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
                            var offerFlowField = typeof(QuestBase).GetField("OfferDialogFlow", Flags);
                            var offerFlow = offerFlowField?.GetValue(realQuest) as DialogFlow;
                            var options = ExtractOptions(offerFlow, evaluateConditions: false, quest: realQuest);
                            if (options.Count > 0)
                            {
                                int finalIdx = ResolveOptionIndex(options, chosenOptIndex, hasExplicit, isReporting: false);
                                if (finalIdx >= 0)
                                {
                                    ExecuteOption(options, finalIdx, false, out var mName, realQuest);
                                    ModLog.Info($"[QuestBridge] Executed OfferDialogFlow option {finalIdx} consequence: {mName}");
                                }
                            }
                        }

                        ModLog.Info($"[QuestBridge] Successfully started quest: '{title}' for {npc?.Name}");
                        InformationManager.DisplayMessage(
                            new InformationMessage($"Quest Started: {title}", new Color(0.35f, 0.85f, 0.45f, 1f)));

                        if (!string.IsNullOrWhiteSpace(spokenReply))
                        {
                            MBTextManager.SetTextVariable("IMMERSIVEAI_RESPONSE", spokenReply, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("starting quest via native dialogue pipeline", ex);
                        InformationManager.DisplayMessage(
                            new InformationMessage($"Quest Error: {ex.Message}", new Color(0.9f, 0.3f, 0.3f, 1f)));
                    }
                });
            }

            if (quest.ReportedQuest != null)
            {
                var questToReport = quest.ReportedQuest;
                var npc = quest.Npc ?? questToReport.QuestGiver;
                var delToInvoke = quest.CompletionDelegate;
                bool isReneged = quest.IsReneged;

                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var title = questToReport.Title?.ToString() ?? "Quest";
                        bool executedOk = false;

                        if (delToInvoke != null)
                        {
                            ModLog.Info($"[QuestBridge] Executing DiscussDialogFlow consequence delegate for '{title}' ({npc?.Name})");
                            executedOk = delToInvoke.Invoke();
                        }

                        if (!executedOk)
                        {
                            ModLog.Warn($"[QuestBridge] Quest conclusion for '{title}' ({npc?.Name}) was blocked by requirement guards. Quest remains active/ongoing.");
                            return;
                        }

                        if (questToReport.IsFinalized)
                        {
                            if (isReneged)
                            {
                                InformationManager.DisplayMessage(
                                    new InformationMessage($"Quest Failed (Breach of Agreement): {title}", new Color(0.95f, 0.4f, 0.35f, 1f)));
                            }
                            else
                            {
                                InformationManager.DisplayMessage(
                                    new InformationMessage($"Quest Completed: {title}", new Color(0.95f, 0.85f, 0.35f, 1f)));
                            }
                        }
                        else
                        {
                            ModLog.Info($"[QuestBridge] DiscussDialogFlow option executed, quest '{title}' remains active/ongoing.");
                        }

                        if (!string.IsNullOrWhiteSpace(spokenReply))
                        {
                            MBTextManager.SetTextVariable("IMMERSIVEAI_RESPONSE", spokenReply, false);
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
