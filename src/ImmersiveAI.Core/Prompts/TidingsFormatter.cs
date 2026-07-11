using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// Pure text-shaping for the "tidings" block — the world's recent happenings and the talk of the
    /// town folded into an NPC's situation. The Module side gathers the raw events from the game's
    /// log history (see TidingsBuilder there); everything here is game-free so it stays unit-tested.
    ///
    /// Like the rest of the prompt, the block is written as gentle narration addressed to the NPC —
    /// never a clinical "EVENTS:" sheet. Tidings are facts that reached their ears; rumors are what
    /// they overheard the common folk say, kept in the folk's own words.
    /// </summary>
    public static class TidingsFormatter
    {
        // Game text can carry encyclopedia link markup (<a ...>Name</a> and kin); an NPC should see
        // only the words. Tags are removed and the whitespace they leave behind is smoothed over.
        public static string StripMarkup(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var bare = Regex.Replace(text, "<[^>]*>", string.Empty);
            return Regex.Replace(bare, @"\s+", " ").Trim();
        }

        /// <summary>How long ago something happened, in spoken words: "earlier today", "yesterday",
        /// "some 4 days past".</summary>
        public static string AgoPhrase(double daysAgo)
        {
            if (daysAgo < 1) return "earlier today";
            if (daysAgo < 2) return "yesterday";
            return $"some {(int)daysAgo} days past";
        }

        /// <summary>One tiding as a list line: the fact (markup stripped, trailing period folded away)
        /// with when it happened breathed after it. Empty facts yield an empty line (skipped).</summary>
        public static string TidingLine(string fact, double daysAgo)
        {
            var clean = StripMarkup(fact).TrimEnd('.', ' ');
            if (clean.Length == 0) return string.Empty;
            return $"- {clean} — {AgoPhrase(daysAgo)}.";
        }

        /// <summary>One overheard rumor as a list line, kept in the speaker's own words and quoted
        /// (unless the text already carries its own quotes).</summary>
        public static string RumorLine(string overheard)
        {
            var clean = StripMarkup(overheard);
            if (clean.Length == 0) return string.Empty;
            bool quoted = clean.StartsWith("\"") || clean.StartsWith("“");
            return quoted ? "- " + clean : $"- “{clean}”";
        }

        /// <summary>
        /// Weaves the prepared lines into the narration block. Either list may be empty; when both
        /// are, the block is empty and nothing is added to the situation. <paramref name="placeName"/>
        /// names where the rumors were overheard (null/blank when the road has no streets to listen in).
        /// </summary>
        public static string Compose(
            IReadOnlyList<string> tidingLines,
            IReadOnlyList<string> rumorLines,
            string? placeName = null)
        {
            bool hasTidings = tidingLines != null && tidingLines.Count > 0;
            bool hasRumors = rumorLines != null && rumorLines.Count > 0;
            if (!hasTidings && !hasRumors) return string.Empty;

            var sb = new StringBuilder();
            if (hasTidings)
            {
                sb.AppendLine("Tidings of the world's late doings have reached my ears:");
                foreach (var line in tidingLines!)
                    if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
            }
            if (hasRumors)
            {
                if (hasTidings) sb.AppendLine();
                sb.AppendLine(string.IsNullOrWhiteSpace(placeName)
                    ? "And I have overheard the common folk say:"
                    : $"And in the streets of {placeName}, I have overheard folk say:");
                foreach (var line in rumorLines!)
                    if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
