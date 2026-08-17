using System;
using System.Text;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// THE TONGUE RULE — the one guard that keeps a generated-once text in the language the player
    /// actually plays in (Steam, Moetel, 2026.08.17: a soul first met under one model kept an English
    /// spark forever, and a deep memory could drift to English mid-summary).
    ///
    /// It SHOWS rather than TELLS: real words that truly passed are quoted (or pointed at) as
    /// EVIDENCE, and the rule says "match those". That is why it holds on weak models where naming a
    /// language does not — the model has the tongue in front of it instead of a claim about it. It
    /// rides LAST in every prompt that carries it, because the model reads the end most closely.
    ///
    /// The shape was proven three times over in the chronicles (WeddingText / BirthText / NightText,
    /// each carrying its own tuned <c>AppendTongueRule</c> with clauses those accounts need — a TITLE,
    /// the spelling of names). Those three are deliberately left where they are: their wording is
    /// tuned and test-guarded. This is the canonical form for every NEW call site, and anything else
    /// that starts generating durable text should reach for it here rather than write a fourth copy.
    /// </summary>
    public static class TongueRule
    {
        /// <summary>How much quoted evidence is enough to fix a tongue without paying for a novel.</summary>
        public const int DefaultEvidenceChars = 1200;

        /// <summary>
        /// The rule with its evidence quoted inline — for prompts whose only sample of the player's
        /// tongue is text we must carry in ourselves (the persona spark, whose soul has not yet
        /// spoken a word). An empty <paramref name="evidence"/> falls back to <see cref="AppendFallback"/>,
        /// so a caller never has to branch.
        /// </summary>
        /// <param name="evidenceIntro">What these words ARE, in the prompt's own voice — the model is
        /// told to take the tongue from them and nothing else, so it must know whose they are.</param>
        /// <param name="subject">What is being written, e.g. "your answer" or "my memory".</param>
        /// <param name="ownMind">True when the prompt is the NPC's own first-person mind (the memory
        /// work), false when it instructs an outside writer (the casting director). The mod never
        /// speaks to a soul in the imperative; see CLAUDE.md's voice rules.</param>
        public static void AppendQuoted(StringBuilder sb, string? evidence, string evidenceIntro,
            string subject, bool ownMind = false, int maxChars = DefaultEvidenceChars)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));

            if (string.IsNullOrWhiteSpace(evidence))
            {
                AppendFallback(sb, subject, ownMind);
                return;
            }

            sb.AppendLine();
            sb.AppendLine("THE TONGUE. " + (evidenceIntro ?? string.Empty).Trim());
            sb.AppendLine("\"\"\"");
            sb.AppendLine(Squeeze(evidence!, maxChars));
            sb.AppendLine("\"\"\"");
            sb.AppendLine(ownMind
                ? $"I set down {subject} in the SAME TONGUE as those words — whatever tongue it is, I match it exactly, and I do not translate it into another. I take from them only the tongue, nothing else."
                : $"Write {subject} in the SAME TONGUE as those words — whatever tongue it is, match it exactly, and do not translate it into another. Take from them only the tongue, nothing else.");
        }

        /// <summary>
        /// The rule pointing at evidence that already stands in the same prompt — for the memory
        /// prompts, where the folded transcript IS the sample and quoting it a second time would
        /// double the largest prompt this mod builds (up to forty turns, and non-ASCII play costs
        /// ~1.6x the tokens on top of that).
        /// </summary>
        /// <param name="pointer">What to look at, e.g. "the words that passed between us above".</param>
        public static void AppendFromAbove(StringBuilder sb, string pointer, string subject, bool ownMind = true)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));

            sb.AppendLine();
            sb.AppendLine(ownMind
                ? $"THE TONGUE. I write {subject} in the SAME TONGUE as {pointer} — whatever tongue it is, I match it exactly, and I do not translate it into another. If nothing there tells me otherwise, I write in English."
                : $"THE TONGUE. Write {subject} in the SAME TONGUE as {pointer} — whatever tongue it is, match it exactly, and do not translate it into another. If nothing there tells you otherwise, write in English.");
        }

        /// <summary>The rule with nothing to show for it — a plain default, never a silent omission.</summary>
        public static void AppendFallback(StringBuilder sb, string subject, bool ownMind = false)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));

            sb.AppendLine();
            sb.AppendLine(ownMind
                ? $"THE TONGUE: I set down {subject} in the tongue this person and I speak between ourselves. If nothing tells me otherwise, I write in English."
                : $"THE TONGUE: write {subject} in the tongue these people speak between themselves. If nothing tells you otherwise, write in English.");
        }

        // Cut at a whitespace boundary so the evidence never ends mid-word — a severed sample still
        // fixes a tongue, but a ragged one reads as damage and invites the model to "fix" it.
        private static string Squeeze(string text, int maxChars)
        {
            var trimmed = text.Trim();
            if (maxChars <= 0 || trimmed.Length <= maxChars) return trimmed;

            var cut = trimmed.Substring(0, maxChars);
            int lastSpace = cut.LastIndexOf(' ');
            if (lastSpace > maxChars / 2) cut = cut.Substring(0, lastSpace);
            return cut.TrimEnd() + "…";
        }
    }
}
