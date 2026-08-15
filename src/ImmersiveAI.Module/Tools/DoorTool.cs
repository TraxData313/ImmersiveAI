using System;
using System.Text.RegularExpressions;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// HER HAND ON THE DOOR (2026.08.15) — the misgivings' own hand, turned on the night instead of
    /// on a marriage.
    ///
    /// TWO LESSONS FROM weigh_misgivings ARE BUILT INTO THIS SCHEMA and both were paid for in a live
    /// probe (2026.08.09): a closed set of words explained only in prose comes back as the model's
    /// own synonym, so the deeds are an <see cref="ToolParameter.AllowedValues"/> enum that both
    /// clients emit as a real JSON schema `enum`; and a parameter called anything like "text" gets
    /// whatever the model has most to say — which is its ANSWER to the player — so every parameter
    /// here is named for exactly what it holds and its description says what it never holds.
    ///
    /// There is no forgiveness verb for the player anywhere in the mod. Settling is HERS, and this
    /// is the hand she settles with.
    /// </summary>
    public static class DoorTool
    {
        public const string WeighWhatStands = "weigh_what_stands";

        /// <summary>What one turn did to the door, filled by the resolver — at most one deed a turn,
        /// the same rule the heart and the troth both keep.</summary>
        public sealed class Tally
        {
            public bool Acted;
            public bool Shut;      // something new stands
            public bool Eased;     // something was laid to rest or struck out
            public string Word = string.Empty;
            /// <summary>True while a LETTER is being written: the door still moves (a woman may
            /// forgive in writing, and may certainly take offence in writing), but no notice fires
            /// at composing time — the courier's seal law.</summary>
            public bool ByLetter;
        }

        public static readonly ToolDefinition Tool = new ToolDefinition(WeighWhatStands,
            "Set down, or lay to rest, what stands between me and the one I share a bed with — and " +
            "it is what stands here that decides whether my door is open to them at all. This list " +
            "is mine alone: no one else writes on it, no coin settles any of it, and no warm hour " +
            "opens my door while something of mine still stands unanswered on it. Nor do I invent a " +
            "grievance to keep the door shut: when nothing of mine stands, nothing of mine bars it. " +
            "I set a thing down when a real hurt is given — something done, something said, " +
            "something I learned of — never for a passing mood and never to punish. I lay a thing " +
            "to rest only when life has truly answered it: not because it was apologised for " +
            "prettily, not because time passed, but because something changed or was said that " +
            "genuinely reached me. I may speak of any of it openly, and I may equally say nothing " +
            "at all — the silence is mine too. I never speak of lists, rules or the shape of this; " +
            "I speak as a person does about what is wrong between us.",
            new[]
            {
                new ToolParameter("deed",
                    "What I do, as one of these words exactly: 'set_down' — a new thing now stands " +
                    "between us; 'settle' — this one is truly answered and I lay it to rest; " +
                    "'release' — I strike it out, it was never really the thing; 'revise' — it has " +
                    "changed shape and I reword it; 'reopen' — I take one back up, because it was " +
                    "not answered after all.",
                    allowedValues: new[] { "set_down", "settle", "release", "revise", "reopen" }),
                new ToolParameter("matter",
                    "The matter itself, in my own voice, one sentence. For 'set_down', the new " +
                    "thing that now stands. For every other deed, enough of the one I mean that it " +
                    "can be told apart from the rest. This is NEVER my answer to them, never my " +
                    "reply in the conversation, and never how I feel about it — only the matter."),
                // ITS OWN FIELD, and it must stay one (2026.08.16). Revising takes TWO strings —
                // which one I mean, and what it now says — and the resolver was handing `matter`
                // for both, so a revise could only ever rewrite a thing to itself and then report
                // that it had reworded it. Named for what it holds, per the standing law, rather
                // than folded into `note` the way the misgivings' older tool folds it.
                new ToolParameter("reworded",
                    "Only for 'revise': what the matter NOW says, in my own voice, one sentence — " +
                    "its new wording, never the old one and never my reply to them. The 'matter' " +
                    "field still names which one I mean; this is what it becomes.",
                    required: false),
                new ToolParameter("opens",
                    "Only for 'set_down' and 'revise': what would truly answer this, in my own " +
                    "words. Leave it out entirely if I do not know — 'I do not know what would fix " +
                    "this' is an honest answer and I am not obliged to invent a way back.",
                    required: false),
                new ToolParameter("note",
                    "Only for 'settle': my own short word on what actually answered it. Not my " +
                    "reply to them — the reason it is at rest.",
                    required: false),
            });

        /// <summary>The deed, canonicalised. The SECOND line of defence behind the schema enum:
        /// honest synonyms are honoured (a model that says "resolve" plainly means settle), and
        /// anything unreadable comes back null so the resolver can say which words it wanted rather
        /// than doing nothing in silence.</summary>
        /// <summary>The deed, canonicalised in Core so the table is unit-testable and lives beside
        /// the ops it names. Null when nothing close came, which the resolver must answer out loud
        /// rather than by doing nothing.</summary>
        public static string? ParseDeed(ToolCall call)
        {
            try
            {
                var raw = JObject.Parse(call.ArgumentsJson)["deed"]?.ToString();
                var deed = Core.Doors.DoorReasons.CanonicalDeed(raw);
                return deed.Length == 0 ? null : deed;
            }
            catch { return null; }
        }

        public static string ParseMatter(ToolCall call) => Field(call, "matter");
        public static string ParseOpens(ToolCall call) => Field(call, "opens");
        public static string ParseNote(ToolCall call) => Field(call, "note");

        /// <summary>The matter's new wording, for a revise. Falls back to `note`, which is where
        /// the misgivings' own tool has always kept it — a model that has learned that shape (and
        /// they ride the same souls) costs nothing to honour, and `note` means nothing on a revise
        /// otherwise.</summary>
        public static string ParseReworded(ToolCall call)
        {
            var said = Field(call, "reworded");
            return said.Length > 0 ? said : Field(call, "note");
        }

        private static string Field(ToolCall call, string name)
        {
            try
            {
                var value = (JObject.Parse(call.ArgumentsJson)[name]?.ToString() ?? string.Empty).Trim();
                return value.Length > 400 ? value.Substring(0, 400) : value;
            }
            catch { return string.Empty; }
        }
    }
}
