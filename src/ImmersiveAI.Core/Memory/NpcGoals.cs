using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// The personal aims an NPC carries — what they strive for, of their own will: to win back a
    /// lost holding, to see a daughter wed well, to grow rich, to be free of a lord's leash. Held
    /// as a short list of one-line intents, in the same spirit as <see cref="NpcMemory.KnownFacts"/>
    /// are held as lasting truths.
    ///
    /// Kept apart from <see cref="NpcMemory"/> for the same reason as <see cref="NpcSelf"/>: a goal
    /// is *general* to the NPC — one set of longings carried into every relationship — not something
    /// held *of another*. It lives in its own file (goals.txt) so it can grow independently.
    ///
    /// The NPC shapes this list two ways: mid-conversation, one aim at a time, through the game
    /// layer's tend_goals tool (add / drop / revise — see the mutation helpers here), and wholesale
    /// when they gather their thoughts in reflection (replace-all, mirroring the FACTS contract —
    /// see <see cref="SetAll"/> and <see cref="MemoryCompressor"/>).
    /// </summary>
    public sealed class NpcGoals
    {
        /// <summary>A sensible default ceiling on how many aims one soul carries at once.</summary>
        public const int DefaultMaxGoals = 6;

        public List<string> Goals { get; set; } = new List<string>();

        /// <summary>Adds a new aim, unless it duplicates one already held or the list is full.
        /// Returns true when it was actually taken up.</summary>
        public bool AddGoal(string goal, int max = DefaultMaxGoals)
        {
            if (max < 1) max = 1;
            if (string.IsNullOrWhiteSpace(goal)) return false;
            var trimmed = goal.Trim();
            if (Goals.Count >= max) return false;
            if (Goals.Any(g => Normalize(g) == Normalize(trimmed))) return false;
            Goals.Add(trimmed);
            return true;
        }

        /// <summary>Releases the aim best matching <paramref name="match"/> (the NPC restates it in
        /// their own words, so the match is fuzzy). Returns the released text, or null if none matched.</summary>
        public string? DropGoal(string match)
        {
            int idx = FindBestMatch(match);
            if (idx < 0) return null;
            var removed = Goals[idx];
            Goals.RemoveAt(idx);
            return removed;
        }

        /// <summary>Reshapes the aim best matching <paramref name="match"/> into <paramref name="revised"/>.
        /// Returns the previous text, or null if none matched (nothing is added on a miss — a revise is
        /// meant to reshape an existing aim, not invent one).</summary>
        public string? ReviseGoal(string match, string revised)
        {
            if (string.IsNullOrWhiteSpace(revised)) return null;
            int idx = FindBestMatch(match);
            if (idx < 0) return null;
            var old = Goals[idx];
            Goals[idx] = revised.Trim();
            return old;
        }

        /// <summary>Replaces the whole list with what the NPC chose to keep carrying (reflection's
        /// replace-all, like the FACTS contract): blanks and duplicates dropped, trimmed to the budget.</summary>
        public void SetAll(IEnumerable<string> goals, int max = DefaultMaxGoals)
        {
            if (max < 1) max = 1;
            Goals.Clear();
            if (goals == null) return;
            foreach (var goal in goals)
            {
                if (string.IsNullOrWhiteSpace(goal)) continue;
                var trimmed = goal.Trim();
                if (Goals.Any(g => Normalize(g) == Normalize(trimmed))) continue;
                Goals.Add(trimmed);
                if (Goals.Count >= max) break;
            }
        }

        /// <summary>The index of the held aim that best matches a restatement of it, or -1 when none is
        /// close enough. Exact (normalized) match wins; then one wholly containing the other; then the
        /// greatest word overlap above a modest threshold, so a loose paraphrase still lands on the aim
        /// it means without ever matching an unrelated one.</summary>
        public int FindBestMatch(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || Goals.Count == 0) return -1;
            var q = Normalize(query);
            if (q.Length == 0) return -1;

            // Exact normalized equality.
            for (int i = 0; i < Goals.Count; i++)
                if (Normalize(Goals[i]) == q) return i;

            // Containment either way (a short restatement of a longer aim, or vice versa).
            for (int i = 0; i < Goals.Count; i++)
            {
                var g = Normalize(Goals[i]);
                if (g.Length > 0 && (g.Contains(q) || q.Contains(g))) return i;
            }

            // Best word-overlap (Jaccard) above a threshold — a paraphrase that shares most of its words.
            var qWords = Words(q);
            int best = -1;
            double bestScore = 0.34; // needs a real majority of shared words, not one incidental "the"
            for (int i = 0; i < Goals.Count; i++)
            {
                var score = Jaccard(qWords, Words(Normalize(Goals[i])));
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var c in s.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (char.IsWhiteSpace(c) && (sb.Length == 0 || sb[sb.Length - 1] != ' ')) sb.Append(' ');
            }
            return sb.ToString().Trim();
        }

        private static HashSet<string> Words(string normalized) =>
            new HashSet<string>(normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        private static double Jaccard(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0;
            int inter = a.Count(b.Contains);
            int union = a.Count + b.Count - inter;
            return union == 0 ? 0 : (double)inter / union;
        }
    }
}
