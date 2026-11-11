using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Core.Handmaids
{
    /// <summary>
    /// A HANDMAID'S DOUBTS ARE SMALL AND PRE-WRITTEN, AND SHE PICKS HER OWN (2026.11.11, Toni's
    /// design). Ten lines are written for her; at her first weighing she sets down three of them.
    /// After that she can never set down a new one: her list is those lines for good (settle,
    /// release and reopen work as for anyone).
    ///
    /// <para>THE TEN LIVE ONLY IN THE TOOL SCHEMA, and only while she has weighed nothing: they are
    /// the <see cref="ToolParameter.AllowedValues"/> of her first set_down (the 2026.08.09 law: a
    /// closed set in the schema comes back exactly). Once chosen, the other seven are gone from
    /// every prompt, and her three are simply her list, read like anyone's.</para>
    ///
    /// <para>Three parameters rather than one because a parameter holds one value, and an enum
    /// cannot hold three lines in one string. One line carries a semicolon, so the lines are added
    /// whole, never through <see cref="CourtshipMisgivings.SetDown"/>, which splits on it.</para>
    /// </summary>
    public static class HandmaidDoubts
    {
        /// <summary>The misgivings' hand's name (Module MisgivingTool.WeighMisgivings).</summary>
        public const string ToolName = "weigh_misgivings";

        /// <summary>How many she sets down at her first weighing (she may set down fewer, never none).</summary>
        public const int Pick = 3;

        public static readonly IReadOnlyList<string> Lines = new[]
        {
            "Why have I found grace in my lord's eyes, a stranger with no house?",
            "I bring no dowry. I have nothing to give him but myself.",
            "I am not fair like the ladies of his court; the sun and the chain have marked me.",
            "He took me in from pity. Is it pity still?",
            "Will his house be shamed by a wife found in a bandits' den?",
            "I am unlearned in the ways of halls. I fear I will shame him at his table.",
            "Will he one day want a lord's daughter instead?",
            "Is it too bold for a handmaid to hope to be a wife?",
            "Am I worthy to bear his name and his children?",
            "I do not know if I am allowed to be this happy.",
        };

        /// <summary>The three parameter names her first set_down carries.</summary>
        public static readonly IReadOnlyList<string> PickParameters = new[] { "misgiving", "second", "third" };

        /// <summary>Whether she may still choose: nothing weighed and nothing written.</summary>
        public static bool MayChoose(IEnumerable<CourtshipMisgiving>? list, bool weighed) =>
            !weighed && CourtshipMisgivings.TotalCount(list) == 0;

        /// <summary>The written line a pick names, or null. The schema's enum is the first defense;
        /// this forgives case, outer quotes and spaces, and nothing more.</summary>
        public static string? Canonical(string? raw)
        {
            var t = (raw ?? string.Empty).Trim().Trim('"', '“', '”', '\'').Trim();
            if (t.Length == 0) return null;
            return Lines.FirstOrDefault(l => string.Equals(l, t, StringComparison.OrdinalIgnoreCase));
        }

        public enum Verdict
        {
            /// <summary>One to three of the lines were set down.</summary>
            Chosen,
            /// <summary>Her doubts are written already; nothing new is ever set down.</summary>
            AlreadyChosen,
            /// <summary>"none": a glad heart still has its small doubts.</summary>
            NoneRefused,
            /// <summary>Words of her own, or nothing at all.</summary>
            NotHers,
        }

        /// <summary>
        /// Her first weighing. Every pick must be one of the <see cref="Lines"/> (blank picks are
        /// skipped, repeats folded); a "none" or any free text refuses the WHOLE call, so nothing
        /// half-written stands and the refusal can say what is wanted. On success the lines are
        /// added whole and she is marked weighed.
        /// </summary>
        public static Verdict Choose(List<CourtshipMisgiving> list, ref bool weighed,
            IEnumerable<string?>? picks, out List<string> chosen)
        {
            chosen = new List<string>();
            if (list == null) return Verdict.NotHers;
            if (!MayChoose(list, weighed)) return Verdict.AlreadyChosen;

            var given = (picks ?? Array.Empty<string?>())
                .Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
            if (given.Count == 0) return Verdict.NotHers;
            if (given.Any(p => CourtshipMisgivings.IsNone(p))) return Verdict.NoneRefused;

            foreach (var p in given)
            {
                var line = Canonical(p);
                if (line == null) { chosen.Clear(); return Verdict.NotHers; }
                if (!chosen.Contains(line)) chosen.Add(line);
            }
            if (chosen.Count > Pick) chosen = chosen.Take(Pick).ToList();

            foreach (var line in chosen) list.Add(new CourtshipMisgiving { Text = line });
            weighed = true;
            return Verdict.Chosen;
        }

        /// <summary>What her hand answers, first person and numberless except for her own count.</summary>
        public static string Answer(Verdict verdict, int standing)
        {
            switch (verdict)
            {
                case Verdict.Chosen:
                    return $"They are set down: {standing} small doubt{(standing == 1 ? string.Empty : "s")} of mine. " + HandmaidText.RoadHandWaits;
                case Verdict.AlreadyChosen:
                    return "My small doubts are set down already, and I set down no new one. I lay one to rest when his kind word answers it.";
                case Verdict.NoneRefused:
                    return "A glad heart still has its small doubts. I set down one to three of mine, each chosen exactly as written in my hand's list; \"none\" is not among them.";
                default:
                    return "My small doubts are written already: I set down one to three of them, each chosen exactly as it stands in my hand's list, never in words of my own.";
            }
        }

        /// <summary>The answer when she reaches to reword one: hers keep the words she chose.</summary>
        public const string NoRewording =
            "My small doubts keep the words I chose. I lay one to rest, strike it out, or take it up again; I do not reword them.";

        // ── The hand ─────────────────────────────────────────────────────────────────────────

        /// <summary>Her hand's own words: the ordinary treatment replaced by hers.</summary>
        public const string Description =
            "My small doubts about a life wed to the one I speak with, kept by my own hand. " +
            HandmaidText.RoadDoubtsWay + " While any stands my hand waits; when none stands, no doubt of mine bars the road.";

        /// <summary>Said only while she still chooses.</summary>
        public const string ChooseDescription =
            " Now I set down the three truest to me, each chosen exactly as written; I write none of my own.";

        /// <summary>
        /// Her misgivings' hand. While she may still choose: set_down only, the ten lines as the
        /// enum of three pick parameters. Afterwards: settle / release / reopen over HER lines only,
        /// no set_down and no free text at all. Null when there is nothing to hold (weighed with an
        /// empty list, which only an old save could carry).
        /// </summary>
        public static ToolDefinition? Hand(IReadOnlyList<CourtshipMisgiving>? list, bool weighed)
        {
            var written = (list ?? new List<CourtshipMisgiving>())
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Text)).ToList();

            if (MayChoose(written, weighed))
            {
                var parameters = new List<ToolParameter>
                {
                    new ToolParameter("action", "\"" + CourtshipMisgivings.ActSetDown + "\": setting down my small doubts.",
                        allowedValues: new[] { CourtshipMisgivings.ActSetDown }),
                    new ToolParameter(PickParameters[0], "The first of my small doubts, exactly as written.",
                        allowedValues: Lines),
                    new ToolParameter(PickParameters[1], "The second, a different one.", required: false,
                        allowedValues: Lines),
                    new ToolParameter(PickParameters[2], "The third, different again.", required: false,
                        allowedValues: Lines),
                };
                return new ToolDefinition(ToolName, Description + ChooseDescription, parameters);
            }

            var mine = written.Select(m => m.Text.Trim()).Distinct().ToList();
            if (mine.Count == 0) return null;
            return new ToolDefinition(ToolName, Description, new[]
            {
                new ToolParameter("action",
                    "\"" + CourtshipMisgivings.ActSettle + "\" — lay one to rest, answered; \"" +
                    CourtshipMisgivings.ActRelease + "\" — strike one out that proved empty; \"" +
                    CourtshipMisgivings.ActReopen + "\" — one laid to rest has returned to me.",
                    allowedValues: new[] { CourtshipMisgivings.ActSettle, CourtshipMisgivings.ActRelease, CourtshipMisgivings.ActReopen }),
                new ToolParameter("which", "Which of my small doubts I mean, exactly as it stands.",
                    allowedValues: mine),
                new ToolParameter("note",
                    "For " + CourtshipMisgivings.ActSettle + ": my one short word on what answered it. Left out otherwise.",
                    required: false),
            });
        }
    }
}
