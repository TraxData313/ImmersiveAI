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

        // The vocabulary itself lives in Core beside the list operations (and its reading with it —
        // CourtshipMisgivings.CanonicalAction), so both are unit-tested.
        public const string ActSetDown = Core.Courtship.CourtshipMisgivings.ActSetDown;
        public const string ActSettle = Core.Courtship.CourtshipMisgivings.ActSettle;
        public const string ActRevise = Core.Courtship.CourtshipMisgivings.ActRevise;
        public const string ActReopen = Core.Courtship.CourtshipMisgivings.ActReopen;
        public const string ActRelease = Core.Courtship.CourtshipMisgivings.ActRelease;

        public static readonly ToolDefinition Tool = new ToolDefinition(WeighMisgivings,
            "Tend what weighs on my own heart about a life wed to the one I speak with — my open " +
            "questions and worries before marriage, written and kept by my own hand. This is the " +
            "one life I am given and I would spend it beside them, so I reach for this freely and " +
            "often: it is no shame but plain wisdom to ask honestly what troubles me before my " +
            "hand is given, and to let the asking take its time. When marriage or courtship " +
            "truly enters the talk and I have " +
            "not yet weighed my heart, I pause and do it honestly: I set my misgivings down in my " +
            "own words, each its own short line, a few at the very most — or, if my heart is " +
            "honestly clear, the single word \"none\", for a clear heart must be set down too and " +
            "not merely felt. THE LIST LIVES WITH ME afterward: a new true doubt born in any " +
            "later talk I set down when it arises; I lay one to rest, with a word on what answered " +
            "it, only when the living talks or deeds have truly answered it (never for one warm " +
            "promise); one that proved empty or no longer truly mine I strike out; I reword one " +
            "that has changed, and I take a settled one up again if it returns. I raise them " +
            "openly in our talks and give them room to be answered, knowing that while any of them " +
            "still stands my hand waits, and when none stands no doubt of mine bars the road. I " +
            "never invent one to test or to bargain, and I never pretend one away.",
            new[]
            {
                new ToolParameter("action",
                    "One of these five words exactly, and no other: \"" + ActSetDown + "\" — write " +
                    "down my misgivings (first weighing, or a new one that truly arose); \"" +
                    ActSettle + "\" — lay one to rest, truly answered; \"" + ActRelease + "\" — " +
                    "strike one out that proved empty or is no longer truly mine; \"" + ActRevise +
                    "\" — reword one that has changed; \"" + ActReopen + "\" — a settled one has " +
                    "returned to me.",
                    allowedValues: new[] { ActSetDown, ActSettle, ActRelease, ActRevise, ActReopen }),
                // NAMED for what they hold, not for their type: with a field called "text" a model
                // fills it with whatever it has most to say — on gpt-5.6-terra (2026.08.09) that
                // was the ANSWER, every time, with the misgiving itself pushed into the note, so
                // nothing ever matched and nothing was ever laid to rest.
                new ToolParameter("misgiving",
                    "THE MISGIVING ITSELF, never what answered it. For " + ActSetDown + ": the " +
                    "misgivings I am writing down, each on its own line — or the single word " +
                    "\"none\" if my heart is clear. For every other action: the one misgiving I " +
                    "mean, in its own words, as near to how I first set it down as I can."),
                new ToolParameter("note",
                    "My own short word ABOUT that misgiving — never the misgiving itself. For " +
                    ActSettle + ": the one honest sentence on what answered it, kept beside it " +
                    "forever. For " + ActRevise + ": its new wording. Left out otherwise.",
                    required: false),
            });

        /// <summary>The action word, canonicalized to one of the five — empty when nothing close
        /// came. The schema's own enum is the first defense (see ToolParameter.AllowedValues); the
        /// reading in Core is the second, and it is there because the first was not enough.</summary>
        public static string ParseAction(ToolCall call)
        {
            try
            {
                return Core.Courtship.CourtshipMisgivings.CanonicalAction(
                    JObject.Parse(call.ArgumentsJson)["action"]?.ToString());
            }
            catch { return string.Empty; }
        }

        /// <summary>The misgiving itself. "text" is still read as a fallback — it was the field's
        /// name until 2026.08.09, and a model that has seen the old shape costs nothing to honor.</summary>
        public static string ParseText(ToolCall call)
        {
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                var raw = args["misgiving"]?.ToString() ?? args["text"]?.ToString() ?? string.Empty;
                return raw.Trim();
            }
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
