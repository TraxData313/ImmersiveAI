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

        /// <summary>How much of the shorter line the longer must already say before the shorter
        /// says nothing new. Not a likeness score — a containment: nine tenths of what this line
        /// says stands in the held one already.</summary>
        public const double RestatementContainment = 0.9;

        /// <summary>Under this many words a line is a scrap, and a scrap is never swallowed by a
        /// long held line however neatly it sits inside it.</summary>
        public const int RestatementFloorWords = 4;

        /// <summary>
        /// The held line that a line being set down merely says again — null when it says something
        /// new. Exact, then the plain containment of one inside the other, then WORD-LEVEL
        /// containment: nine tenths of the shorter line's stems already stand in the longer.
        ///
        /// That third rule was paid for by Rhia the Healer (2026.08.30), who set both of these down
        /// twice — "I do not know if he will let himself be loved." beside "I do not know if he will
        /// truly let himself be loved.", and "I am no lord's daughter, and I fear he will one day
        /// want a wife who is." beside "I fear he will one day want a wife who is noble-born." One
        /// word inserted and one word traded, and character containment sees two strangers. She then
        /// laid the SECOND of each pair to rest, and the first stood on forever — unanswerable,
        /// because she no longer had the words she first wrote it in. A road walled shut by a doubt
        /// she had already answered.
        ///
        /// That is why the old razor moved. It leaned the other way before, judging a swallowed new
        /// doubt the worse mistake; the live evidence says otherwise. A swallowed doubt is soft —
        /// she is told WHICH held line hers landed on and may set it down again saying plainly how
        /// it differs — while an orphaned twin is a wall, because a clear heart in this system is
        /// earned and never declared.
        ///
        /// Still deliberately NOT the lenient overlap of <see cref="Best"/>: "I do not know if he
        /// will let me go" keeps four fifths of that first line and is a wholly different fear.
        ///
        /// HOW MUCH ROOM IS LEFT, for whoever reaches for this constant next: the nearest thing on
        /// the far side of it is the doors' cap test, whose five filler grievances differ by one
        /// ordinal alone and so sit at 8 shared stems of 9 — 0.889. It passes, and it is the canary.
        /// Raise the containment and real twins come back; lower it and that test goes red, which is
        /// the reading you want before touching this number.
        /// </summary>
        public static string? Restated(IEnumerable<string>? held, string? piece)
        {
            var p = Normalize(piece);
            if (p.Length == 0) return null;
            var pStems = Stems(p);
            foreach (var line in held ?? Enumerable.Empty<string>())
            {
                var t = Normalize(line);
                if (t.Length == 0) continue;
                if (t == p) return line;
                if (Math.Min(t.Length, p.Length) >= 12 && (t.Contains(p) || p.Contains(t))) return line;
                if (SaysNothingNew(pStems, Stems(t))) return line;
            }
            return null;
        }

        /// <summary>Whether a line being set down merely says again something already held. A line
        /// that normalizes away to nothing counts as said already — there is nothing in it to add.
        /// </summary>
        public static bool Restates(IEnumerable<string>? held, string? piece) =>
            Normalize(piece).Length == 0 || Restated(held, piece) != null;

        /// <summary>Whether the shorter of two stem sets is <see cref="RestatementContainment"/>
        /// held inside the longer — measured on the SHORTER, so a short line fully carried by a long
        /// one is a restatement of it and not the other way about.</summary>
        private static bool SaysNothingNew(HashSet<string> a, HashSet<string> b)
        {
            if (a == null || b == null) return false;
            var fewer = a.Count <= b.Count ? a : b;
            var more = ReferenceEquals(fewer, a) ? b : a;
            if (fewer.Count < RestatementFloorWords) return false;
            return fewer.Count(more.Contains) >= fewer.Count * RestatementContainment;
        }
    }
}
