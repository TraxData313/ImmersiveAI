using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Text
{
    /// <summary>
    /// FINDING THE LINE A SOUL MEANT when she names one of her own earlier sentences back at us.
    ///
    /// Extracted from the misgivings on 2026.08.15, unchanged in behaviour, because the doors now
    /// need exactly the same thing and a second, weaker copy would have been the real bug. Every
    /// rule in here was paid for by a live probe against a real backend, and they are worth stating
    /// because none of them is obvious:
    ///
    /// • A model asked to name back its own written doubt returns a PARAPHRASE of it, never the
    ///   words. Exact matching therefore makes the settle verb silently do nothing — and a woman who
    ///   forgave and was not recorded as forgiving is the worst failure either of these systems can
    ///   have.
    /// • THE STEMS ARE WHY THIS WORKS AT ALL IN BULGARIAN. Anton plays in Bulgarian, where
    ///   "избере" and "избереш" are one word wearing two endings, and word-for-word comparison
    ///   called them strangers. Cutting every word to four letters folds the ending without naming
    ///   any language or favouring one.
    /// • Jaccard alone punishes the honest shape of a restatement: a short paraphrase of a long line
    ///   shares nearly all of ITS words and still scores low. So containment of the SHORTER line
    ///   counts too — guarded by a floor of three shared words, so a two-word scrap can never sweep
    ///   the whole list.
    /// </summary>
    public static class LooseMatch
    {
        /// <summary>The score a word-overlap must beat to count as the same line. Below a real
        /// majority it is a coincidence of register, not a match.</summary>
        public const double DefaultFloor = 0.34;

        /// <summary>Lower-cased, punctuation stripped, runs of space collapsed.</summary>
        public static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var sb = new StringBuilder(s!.Length);
            foreach (var c in s.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (char.IsWhiteSpace(c) && (sb.Length == 0 || sb[sb.Length - 1] != ' ')) sb.Append(' ');
            }
            return sb.ToString().Trim();
        }

        /// <summary>The words of a normalized line, each cut back to its first four letters — crude
        /// on purpose, and the reason an inflected tongue matches itself. See the class remarks.</summary>
        public static HashSet<string> Stems(string normalized)
        {
            var set = new HashSet<string>();
            foreach (var w in (normalized ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                set.Add(w.Length <= 4 ? w : w.Substring(0, 4));
            return set;
        }

        /// <summary>How near two stem sets are. See the class remarks for why it is not plain Jaccard.</summary>
        public static double Overlap(HashSet<string> a, HashSet<string> b)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0) return 0;
            int inter = a.Count(b.Contains);
            if (inter == 0) return 0;
            int union = a.Count + b.Count - inter;
            double jaccard = union == 0 ? 0 : (double)inter / union;
            if (inter < 3) return jaccard;
            double contained = (double)inter / Math.Min(a.Count, b.Count);
            return contained >= 0.5 && contained > jaccard ? contained : jaccard;
        }

        /// <summary>
        /// The held line that best matches a restatement of it: exact, then plain containment either
        /// way, then stem-overlap above <paramref name="floor"/>. Null when nothing is close enough,
        /// which the caller must always treat as "say which words you wanted" and never as silence.
        /// </summary>
        public static T? Best<T>(IEnumerable<T>? pool, Func<T, string> textOf, string? query,
            double floor = DefaultFloor) where T : class
        {
            if (pool == null || textOf == null) return null;
            var kept = pool.Where(x => x != null && !string.IsNullOrWhiteSpace(textOf(x))).ToList();
            if (kept.Count == 0) return null;

            var q = Normalize(query);
            if (q.Length == 0) return null;

            foreach (var one in kept)
                if (Normalize(textOf(one)) == q) return one;

            foreach (var one in kept)
            {
                var t = Normalize(textOf(one));
                if (t.Length > 0 && (t.Contains(q) || q.Contains(t))) return one;
            }

            var qStems = Stems(q);
            T? best = null;
            double bestScore = floor;
            foreach (var one in kept)
            {
                var score = Overlap(qStems, Stems(Normalize(textOf(one))));
                if (score > bestScore) { bestScore = score; best = one; }
            }
            return best;
        }

        /// <summary>
        /// Whether a line being set down merely says again something already held — exactly, or as
        /// the plain containment of one inside the other. Deliberately NOT the lenient overlap
        /// above: two truly different things in the same register share plenty of words, and
        /// swallowing a real new one is the worse mistake. The length floor keeps a stray word from
        /// vanishing into a long held line.
        /// </summary>
        public static bool Restates(IEnumerable<string>? held, string? piece)
        {
            var p = Normalize(piece);
            if (p.Length == 0) return true;
            foreach (var line in held ?? Enumerable.Empty<string>())
            {
                var t = Normalize(line);
                if (t.Length == 0) continue;
                if (t == p) return true;
                if (Math.Min(t.Length, p.Length) >= 12 && (t.Contains(p) || p.Contains(t))) return true;
            }
            return false;
        }
    }
}
