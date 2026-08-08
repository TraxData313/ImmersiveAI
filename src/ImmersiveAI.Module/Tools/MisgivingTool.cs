using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The misgivings' hand (2026.08.08, Anton's ask — "no robotic bargains"): the NPC's own hand
    /// upon what weighs on her heart about a life wed to the player. This REPLACED the matchmaker's
    /// checkable quiet asks: nothing is generated for her and nothing is auto-judged against gold or
    /// skills — when marriage truly enters the talk she sets her misgivings down in her own words
    /// (five at the very most, or honestly "none"), speaks of them openly as the talk turns that
    /// way, and lays each to rest by her own judgment with a light word on what answered it. The
    /// road's rails (CourtshipRoad) only count them: Ready and the betrothal wait until she has
    /// weighed her heart and nothing she set down still stands. Rides beside tend_courtship on the
    /// same tally; the list ops live in Core (CourtshipMisgivings, unit-tested).
    /// </summary>
    public static class MisgivingTool
    {
        public const string WeighMisgivings = "weigh_misgivings";

        public const string ActSetDown = "set_down";
        public const string ActSettle = "settle";
        public const string ActRevise = "revise";
        public const string ActReopen = "reopen";
        public const string ActRelease = "release";

        public static readonly ToolDefinition Tool = new ToolDefinition(WeighMisgivings,
            "Tend what weighs on my own heart about a life wed to the one I speak with — my open " +
            "questions and worries before marriage, written and kept by my own hand. This is the " +
            "one life I am given and I would spend it beside them, so I use this freely: it is no " +
            "shame but plain wisdom to weigh a soul honestly before my hand is given. When marriage " +
            "or courtship truly enters the talk and I have not yet weighed my heart, I pause and " +
            "set my misgivings down in my own words — each its own short line, a few at the very " +
            "most, or the single word \"none\" if my heart is honestly clear. THE LIST LIVES WITH " +
            "ME afterward: a new true doubt born in any later talk I set down when it arises; I " +
            "lay one to rest only when the living talks or deeds have truly answered it (never for " +
            "one warm word); one that proved empty or no longer truly mine I strike out; I reword " +
            "one that has changed, and I take a settled one up again if it returns. I may speak of " +
            "them openly — and I know that while any of them still stands, I will not give my " +
            "hand; when none stands, no doubt of mine bars the road. I never invent one to test " +
            "or to bargain, and I never pretend one away.",
            new[]
            {
                new ToolParameter("action",
                    "\"" + ActSetDown + "\" — write down my misgivings (first weighing, or a new " +
                    "one that truly arose); \"" + ActSettle + "\" — lay one to rest, truly " +
                    "answered; \"" + ActRelease + "\" — strike one out that proved empty or is no " +
                    "longer truly mine; \"" + ActRevise + "\" — reword one that has changed; \"" +
                    ActReopen + "\" — a settled one has returned to me."),
                new ToolParameter("text",
                    "For " + ActSetDown + ": my misgivings, each on its own line — or the single " +
                    "word \"none\" if my heart is clear. For the rest: the misgiving I mean, as " +
                    "near to how I set it down as I can."),
                new ToolParameter("note",
                    "For " + ActSettle + ": one short honest sentence on what answered it, kept " +
                    "beside it. For " + ActRevise + ": the misgiving as it now stands, reworded.",
                    required: false),
            });

        /// <summary>The action word, lowered and trimmed; empty when unreadable.</summary>
        public static string ParseAction(ToolCall call)
        {
            try { return (JObject.Parse(call.ArgumentsJson)["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant(); }
            catch { return string.Empty; }
        }

        public static string ParseText(ToolCall call)
        {
            try { return (JObject.Parse(call.ArgumentsJson)["text"]?.ToString() ?? string.Empty).Trim(); }
            catch { return string.Empty; }
        }

        public static string ParseNote(ToolCall call)
        {
            try
            {
                var note = (JObject.Parse(call.ArgumentsJson)["note"]?.ToString() ?? string.Empty).Trim();
                return note.Length > 300 ? note.Substring(0, 300) : note;
            }
            catch { return string.Empty; }
        }
    }
}
