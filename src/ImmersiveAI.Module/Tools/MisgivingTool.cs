using System.Collections.Generic;
using System.Linq;
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

        // TIGHT ON PURPOSE (2026.08.27 token audit) — the rails all stand: weigh when marriage
        // truly enters, "none" counts and must be set down, the list lives (set down / settle with
        // a word / release / revise / reopen), settle only what deeds truly answered, hand waits
        // while any stands and no doubt bars when none does, never invent and never pretend away.
        /// <summary>The hand's own words, shared by the plain <see cref="Tool"/> and by the
        /// per-soul <see cref="For"/> that carries her list as the schema's choices.</summary>
        public const string Description =
            "What weighs on my own heart about a life wed to the one I speak with — my open worries " +
            "before marriage, written and kept by my own hand. When marriage or courtship truly " +
            "enters the talk and I have not yet weighed my heart, I pause and do it honestly: each " +
            "misgiving its own short line, a few at the very most — or, if my heart is honestly " +
            "clear, the single word \"none\", for a clear heart must be set down too. AFTERWARDS I " +
            "WORK THROUGH WHAT I WROTE RATHER THAN ADDING TO IT: while any doubt of mine still " +
            "stands I set down no new one — if what troubles me now is that same worry in other " +
            "words, it is already written, and my part is to lay THAT one to rest when it is truly " +
            "answered, or strike it out if it proved empty. I lay one to rest with a word on what " +
            "answered it, only when talks or deeds have truly answered it — never for one warm " +
            "promise; I reword one that has changed, and take a settled one up again if it returns. " +
            "I speak of one plainly when the talk itself comes near it, and then let it lie — I do " +
            "not circle back to the same doubt again and again, nor make it the toll on every warm " +
            "word. While any stands my hand waits; when none stands, no doubt of mine bars the " +
            "road. I never invent one to test, and never pretend one away.";

        public static readonly ToolDefinition Tool = new ToolDefinition(WeighMisgivings, Description,
            new[]
            {
                new ToolParameter("action",
                    "One of these five words exactly: \"" + ActSetDown + "\" — write down my " +
                    "misgivings (first weighing, or a new one); \"" + ActSettle + "\" — lay one to " +
                    "rest, truly answered; \"" + ActRelease + "\" — strike one out that proved " +
                    "empty; \"" + ActRevise + "\" — reword one that has changed; \"" + ActReopen +
                    "\" — a settled one has returned to me.",
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

        /// <summary>
        /// THE HAND, CARRYING HER OWN LINES AS THE SCHEMA'S OWN CHOICES (2026.08.31, Anton: "I told
        /// her stuff to drop the 1/4 left and she didnt drop it but made a duplicate again").
        ///
        /// <para>THE FAULT, and why no matcher could have fixed it. Naming a misgiving back in free
        /// text means paraphrasing it, and a paraphrase either lands (fine), or misses — and a miss
        /// reads to the model as "that one is not written down yet", so the very next breath sets it
        /// down AGAIN, in the new words. The list grows by exactly the doubts the player is trying
        /// to answer. The two Rhia carried are semantic twins with barely a word in common ("I am in
        /// his pay — I would know the difference between being cherished and being kept" beside "I
        /// fear I may be cherished as my captain's paid healer rather than chosen freely as his
        /// equal"): no containment rule, at any threshold, will ever fold those, and one loose
        /// enough to try would swallow real doubts wholesale.</para>
        ///
        /// <para>So the targeting stops being free text. Her own standing lines (and, for
        /// <see cref="ActReopen"/>, the ones she has laid to rest) ride as the <c>which</c>
        /// parameter's <see cref="ToolParameter.AllowedValues"/> — the schema's own enum, which
        /// every client emits and no model can paraphrase past. This is the 2026.08.09 law applied
        /// where it was always needed most: a closed set explained in prose comes back as a synonym;
        /// a closed set in the SCHEMA comes back exactly.</para>
        ///
        /// <para>Returns the plain <see cref="Tool"/> when she holds nothing yet — there is nothing
        /// to choose from, and an empty enum would refuse her first weighing.</para>
        /// </summary>
        public static ToolDefinition For(IReadOnlyList<string>? standing, IReadOnlyList<string>? settled)
        {
            var open = (standing ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim()).Distinct().ToList();
            var rest = (settled ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim()).Distinct().Where(t => !open.Contains(t)).ToList();
            var all = open.Concat(rest).ToList();
            if (all.Count == 0) return Tool;

            var parameters = new List<ToolParameter>
            {
                new ToolParameter("action",
                    "One of these five words exactly: \"" + ActSetDown + "\" — write down a new " +
                    "misgiving (or my first weighing); \"" + ActSettle + "\" — lay one to rest, " +
                    "truly answered; \"" + ActRelease + "\" — strike one out that proved empty; \"" +
                    ActRevise + "\" — reword one that has changed; \"" + ActReopen + "\" — a " +
                    "settled one has returned to me.",
                    allowedValues: new[] { ActSetDown, ActSettle, ActRelease, ActRevise, ActReopen }),

                // The whole fix in one parameter: she PICKS her own line rather than retyping it.
                new ToolParameter("which",
                    "WHICH of my own misgivings I mean, for " + ActSettle + ", " + ActRelease +
                    ", " + ActRevise + " and " + ActReopen + " — chosen exactly as it stands in my " +
                    "list, never reworded. Left out only when I am setting a new one down.",
                    required: false, allowedValues: all),

                new ToolParameter("misgiving",
                    "For " + ActSetDown + " ONLY: the new misgiving I am writing down, in my own " +
                    "words — or the single word \"none\" if my heart is clear. Never used to name " +
                    "one I already hold; that is what \"which\" is for.",
                    required: false),

                new ToolParameter("note",
                    "My own short word ABOUT that misgiving — never the misgiving itself. For " +
                    ActSettle + ": the one honest sentence on what answered it, kept beside it " +
                    "forever. For " + ActRevise + ": its new wording. Left out otherwise.",
                    required: false),
            };

            return new ToolDefinition(WeighMisgivings, Description, parameters);
        }

        /// <summary>Which of her own lines she picked, when the hand carried them. Empty when she
        /// is setting a new one down, or when an older shape of the tool was in play.</summary>
        public static string ParseWhich(ToolCall call)
        {
            try
            {
                return (JObject.Parse(call.ArgumentsJson)["which"]?.ToString() ?? string.Empty).Trim();
            }
            catch { return string.Empty; }
        }

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
