using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// The road already walked: a soul with a REAL shared story must not start at None just
    /// because the courtship machinery arrived after the love did (Anton's ask, 2026.08.07 — read
    /// the Sibylla history and let her own stage answer it). A one-time plain utility call reads
    /// her remembered story and answers where her heart ALREADY stands — up to Betrothed at most;
    /// a wedding exists only through the real seal. The lived story is taken as truth; the rails
    /// apply from there forward.
    /// </summary>
    public static class CourtshipSeed
    {
        /// <summary>Builds the seeding prompt — a plain meta utility call, judged from her own
        /// remembered words alone, deliberately told not to be hopeful on her behalf.</summary>
        public static string BuildPrompt(
            string npcName,
            string genderWord,
            string partnerName,
            string summary,
            string knownFacts,
            string selfText,
            string recentExcerpt)
        {
            var she = string.IsNullOrWhiteSpace(genderWord) ? "they" : (genderWord == "woman" ? "she" : "he");
            var her = she == "she" ? "her" : she == "he" ? "his" : "their";

            var sb = new StringBuilder();
            sb.AppendLine($"You are a quiet reader of one soul's heart in a living medieval world. {npcName.Trim()} already shares a story with {partnerName.Trim()}; judge from {her} own remembered words alone where {her} heart TRULY stands on the road toward marrying them. Read only what the words themselves carry — be neither hopeful nor stingy on {her} behalf; where love was spoken, honor it, and where a promise was deliberately not yet given, honor that too.");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(summary))
                sb.AppendLine($"What {she} remembers: \"{summary.Trim()}\"");
            if (!string.IsNullOrWhiteSpace(knownFacts))
                sb.AppendLine($"Truths {she} holds: \"{knownFacts.Trim()}\"");
            if (!string.IsNullOrWhiteSpace(selfText))
                sb.AppendLine($"What {she} holds true of {her} own self: \"{selfText.Trim()}\"");
            if (!string.IsNullOrWhiteSpace(recentExcerpt))
            {
                sb.AppendLine($"{Cap(her)} latest remembered exchanges with {partnerName.Trim()}:");
                sb.AppendLine(recentExcerpt.Trim());
            }
            sb.AppendLine();
            sb.AppendLine("The road's places, in the soul's own words:");
            sb.AppendLine("  none — no more than ordinary acquaintance; nothing of the heart has been spoken.");
            sb.AppendLine("  warmth — I like them; their company draws me.");
            sb.AppendLine("  devotion — my heart is truly given; love has been spoken or plainly lives between us.");
            sb.AppendLine("  ready — no questions remain; were marriage asked between us, I would say yes.");
            sb.AppendLine("  betrothed — we have already promised ourselves to one another in so many words.");
            sb.AppendLine();
            sb.AppendLine("Answer in exactly two lines:");
            sb.AppendLine("STAGE: <none|warmth|devotion|ready|betrothed>");
            sb.AppendLine($"WHY: <one short sentence in {her} own first-person voice>");
            return sb.ToString().TrimEnd();
        }

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static readonly Regex StageLine = new Regex(
            @"STAGE\s*:\s*(?<stage>none|warmth|devotion|ready|betrothed)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex WhyLine = new Regex(
            @"WHY\s*:\s*(?<why>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        /// <summary>
        /// Reads the seeding answer. True only when a stage was truly named (a bare stage word
        /// alone also counts); false means the answer was unusable and the seeding should simply be
        /// tried again another day — never silently seeded to None by a garbled reply.
        /// </summary>
        public static bool TryParseSeed(string? raw, out CourtshipStage stage, out string why)
        {
            stage = CourtshipStage.None;
            why = string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var text = raw!.Trim();
            var m = StageLine.Match(text);
            string? word = m.Success ? m.Groups["stage"].Value : null;

            if (word == null)
            {
                // A model that answered with the bare word alone is still honored.
                var bare = text.Trim('.', '"', '\'', '*', ' ').ToLowerInvariant();
                if (bare == "none" || bare == "warmth" || bare == "devotion" || bare == "ready" || bare == "betrothed")
                    word = bare;
            }
            if (word == null) return false;

            switch (word.ToLowerInvariant())
            {
                case "warmth": stage = CourtshipStage.Warmth; break;
                case "devotion": stage = CourtshipStage.Devotion; break;
                case "ready": stage = CourtshipStage.Ready; break;
                case "betrothed": stage = CourtshipStage.Betrothed; break;
                default: stage = CourtshipStage.None; break;
            }

            var w = WhyLine.Match(text);
            if (w.Success) why = w.Groups["why"].Value.Trim().Trim('"', '“', '”');
            return true;
        }
    }
}
