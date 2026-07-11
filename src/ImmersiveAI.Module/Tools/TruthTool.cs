using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The NPC's own hand upon their lasting truths: a tool they may call mid-conversation to set
    /// down (or release) one of the durable one-liners they hold about the player — the mid-talk
    /// counterpart of the wholesale FACTS rewrite that happens in reflection, exactly as tend_goals
    /// is to the reflection's GOALS. One truth to a call; used when something genuinely worth
    /// keeping is revealed, not for passing small talk.
    ///
    /// The resolver applies it to the LIVE NpcMemory instance the turn is speaking from (and saves),
    /// so the turn's own end-of-exchange save can never clobber what was set down mid-flight.
    /// </summary>
    public static class TruthTool
    {
        public const string HoldTruth = "hold_truth";

        public static readonly ToolDefinition Tool = new ToolDefinition(HoldTruth,
            "Set down one lasting truth about the one you speak with — something from this exchange " +
            "that should stay with you long after: a name, a bond, a promise, a deed, a debt. Reach " +
            "for it when something truly worth keeping is revealed; not for passing small talk. One " +
            "truth to a call, short, in your own words. You may instead release a truth that has " +
            "proven false or spent.",
            new[]
            {
                new ToolParameter("truth",
                    "The truth as one short line in your own words (\"They saved my caravan at Omor\", " +
                    "\"They are hunting the man who burned their village\"). To release one, restate " +
                    "the held truth you mean, as near to how you hold it as you can."),
                new ToolParameter("release",
                    "Leave empty to set the truth down and keep it. Write 'yes' to instead release " +
                    "the named truth from what you hold.", required: false),
            });

        /// <summary>What the tool answers when the truth was kept — steering them back to words.</summary>
        public const string Kept =
            "It is set down among the truths you hold; it will stay with you. Speak on.";

        /// <summary>What the tool answers when a held truth was released.</summary>
        public const string Released =
            "It is released; you hold it no longer. Speak on.";

        /// <summary>When the mind already carries all it can — reflection is where the list resettles.</summary>
        public const string Full =
            "You hold as many truths as your mind will carry; release one first, or let them settle " +
            "when next you gather your thoughts. Speak on.";

        /// <summary>When nothing changed (a duplicate, a release that matched nothing, an empty line).</summary>
        public const string NoChange =
            "Nothing changed among the truths you hold just now. Speak on.";

        /// <summary>
        /// Applies one truth operation to the live <paramref name="memory"/> and returns the in-world
        /// answer the NPC hears. Returns via <paramref name="changed"/> whether anything was actually
        /// written, so the caller knows whether to save and notice. Lenient parsing throughout.
        /// </summary>
        public static string Apply(ToolCall call, NpcMemory memory, int maxFacts, out bool changed)
        {
            changed = false;
            if (memory == null) return NoChange;
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                var truth = (args["truth"]?.ToString() ?? string.Empty).Trim();
                var releaseRaw = (args["release"]?.ToString() ?? string.Empty).Trim();
                bool release = releaseRaw.StartsWith("y", System.StringComparison.OrdinalIgnoreCase)
                    || releaseRaw.StartsWith("t", System.StringComparison.OrdinalIgnoreCase);
                if (truth.Length == 0) return NoChange;

                if (release)
                {
                    changed = memory.DropKnownFact(truth) != null;
                    return changed ? Released : NoChange;
                }

                if (memory.AddKnownFact(truth, maxFacts)) { changed = true; return Kept; }
                return maxFacts > 0 && memory.KnownFacts.Count >= maxFacts ? Full : NoChange;
            }
            catch { return NoChange; }
        }
    }
}
