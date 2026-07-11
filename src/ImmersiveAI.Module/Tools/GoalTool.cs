using System;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The NPC's own hand upon their aims: a tool they may call mid-conversation to keep the aims
    /// they strive for in order — take up a new one, release one won or lost, or reshape one that has
    /// changed. The general counterpart of move_heart (the heart is weighed every reply; the aims are
    /// touched only when they truly shift), and the mid-conversation counterpart of the wholesale
    /// rework that happens in reflection (see <see cref="NpcGoals"/> and MemoryCompressor's GOALS).
    ///
    /// Discrete one-aim-at-a-time operations, not a whole-list replace: mid-talk a soul notes a single
    /// new purpose, not their entire life's plan. Drop/revise identify the target by a restatement of
    /// it (the NPC sees the exact aims in their prompt), matched leniently by <see cref="NpcGoals"/>.
    /// </summary>
    public static class GoalTool
    {
        public const string TendGoals = "tend_goals";

        public const string Add = "add";
        public const string Drop = "drop";
        public const string Revise = "revise";

        public static readonly ToolDefinition Tool = new ToolDefinition(TendGoals,
            "Set one of your own aims in order — the purposes you carry of your own will. Reach for this " +
            "only when what you strive for has genuinely shifted: a new purpose kindled, an old one won, " +
            "lost, or turned to ash. Most talk changes nothing; do not touch your aims out of habit. One " +
            "aim to a call. The choice is wholly yours, and no aim need ever be spoken aloud.",
            new[]
            {
                new ToolParameter("action",
                    "What you would do with an aim: \"add\" to take up a new one, \"drop\" to let go of one " +
                    "you have finished with or abandoned, or \"revise\" to reshape one that has changed."),
                new ToolParameter("goal",
                    "For \"add\", the new aim in your own words (a single short line — \"win back my father's " +
                    "hall\", \"see my sister safely wed\"). For \"drop\" or \"revise\", name the aim you mean, " +
                    "as near to how you hold it as you can."),
                new ToolParameter("into",
                    "Only for \"revise\": the aim as it now stands, reshaped.", required: false),
            });

        /// <summary>What the tool answers on a change taken — steering the NPC back to their words.</summary>
        public const string Done = "It is done; your aim is set in order. Speak on, and let it live in what you do.";

        /// <summary>What the tool answers when nothing could be changed (an unknown action, a drop or
        /// revise that matched no held aim, an add that was full or duplicate) — an honest stillness.</summary>
        public const string NoChange = "Nothing changed in your aims just now. Speak on.";

        /// <summary>
        /// Applies one goal operation to <paramref name="goals"/>, returning true when the list actually
        /// changed (so the caller knows whether to save and how to answer the NPC). Lenient parsing:
        /// the fields may be null, and drop/revise fall back to a fuzzy match on the restated aim.
        /// </summary>
        public static bool Apply(ToolCall call, NpcGoals goals, int maxGoals)
        {
            if (goals == null) return false;
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                var action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                var goal = args["goal"]?.ToString();
                var into = args["into"]?.ToString();

                switch (action)
                {
                    case Add:
                        return goals.AddGoal(goal ?? string.Empty, maxGoals);
                    case Drop:
                        return goals.DropGoal(goal ?? string.Empty) != null;
                    case Revise:
                        // Missing "into" is treated as a drop of the named aim, so a half-formed revise
                        // still does the sensible thing rather than nothing.
                        return string.IsNullOrWhiteSpace(into)
                            ? goals.DropGoal(goal ?? string.Empty) != null
                            : goals.ReviseGoal(goal ?? string.Empty, into) != null;
                    default:
                        return false;
                }
            }
            catch { return false; }
        }
    }
}
