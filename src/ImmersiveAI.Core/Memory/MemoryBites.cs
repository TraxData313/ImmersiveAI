using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// THE BITES (2026.08.27, Anton's design) — the deep memory as small keyed notes she edits one
    /// at a time, instead of one long page rewritten whole at every compression.
    ///
    /// <para>
    /// WHY, and it is not only the tokens. Rewriting the whole page each time is expensive AND
    /// quietly erosive: the contract has to warn her "what I do not set down here fades from me",
    /// so every pass risks losing a particular she simply did not think to re-copy. A bite is
    /// touched only when it changes. What she does not mention keeps standing, word for word.
    /// She can also write the moment something is said — the <c>keep_note</c> hand — rather than
    /// waiting for a compression that may be twenty turns away.
    /// </para>
    ///
    /// <para>
    /// THIS IS NOT THE RETIRED <c>hold_truth</c> (2026.08.08), and the difference is the whole
    /// reason it may exist. That was a THIRD layer standing beside the rolling summary, holding the
    /// same material and reading it back to her twice. The bites REPLACE the page's facts; there is
    /// nothing left to duplicate. What survives of the old objection is its cost — one more tool in
    /// every reply — and the incremental writes are what pay for it.
    /// </para>
    ///
    /// <para>
    /// ONE KEY IS RESERVED FOR PROSE (<see cref="ProseKey"/>, Anton's call when asked): how things
    /// stand between the two of them stays a few sentences of real writing, because that is where
    /// a soul's voice lives — "there is affection there, or the beginning of it; I know better than
    /// to name it more firmly before the road has shown what it is" does not survive being chopped
    /// into "gave me gear, I trust him". Everything else is tight. It is stored in
    /// <see cref="NpcMemory.Summary"/>, which is also what makes every old save migrate for free:
    /// an existing page simply becomes the prose bite, and the facts flake off it as she goes.
    /// </para>
    /// </summary>
    public static class MemoryBites
    {
        /// <summary>The one key whose value is allowed to be prose rather than a note. Its text
        /// lives in <see cref="NpcMemory.Summary"/>, never in the dictionary.</summary>
        public const string ProseKey = "how things stand between us";

        /// <summary>The most notes a soul keeps. Past this the weakest must go before a new one is
        /// written — a memory that only ever grows is a page again, wearing a different hat.</summary>
        public const int MaxBites = 24;

        /// <summary>How long one note may be. A bite is a note, not a paragraph; past this it is
        /// cut back to its last finished sentence (falling back to a hard cut).</summary>
        public const int MaxBiteChars = 320;

        /// <summary>How long a key may be. A key is a subject — a name, a thing, a matter.</summary>
        public const int MaxKeyChars = 48;

        // ------------------------------ keys ------------------------------

        /// <summary>
        /// The key as it is FILED. Keys arrive from a model and will otherwise fragment — "Ahil",
        /// "ahil", "Ahil ", "the captain" — leaving four notes where one was meant, each of them
        /// half the truth. Lowercased, whitespace collapsed, surrounding punctuation and a leading
        /// article dropped. The same lesson the misgivings' fuzzy matching learned the hard way.
        /// </summary>
        public static string CanonicalKey(string? key)
        {
            var text = (key ?? string.Empty).Trim().ToLowerInvariant();
            if (text.Length == 0) return string.Empty;

            var sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (var ch in text)
            {
                if (char.IsWhiteSpace(ch)) { space = sb.Length > 0; continue; }
                if (char.IsLetterOrDigit(ch) || ch == '\'' || ch == '-')
                {
                    if (space) { sb.Append(' '); space = false; }
                    sb.Append(ch);
                }
                // everything else (colons, quotes, brackets) is filing noise and is dropped
            }

            var canon = sb.ToString();
            if (canon.StartsWith("the ", StringComparison.Ordinal)) canon = canon.Substring(4);
            if (canon.Length > MaxKeyChars) canon = canon.Substring(0, MaxKeyChars).TrimEnd();
            return canon;
        }

        /// <summary>True when this key means the reserved prose bite (however it was spelled).</summary>
        public static bool IsProseKey(string? key)
        {
            var canon = CanonicalKey(key);
            return canon == CanonicalKey(ProseKey) || canon == "how things stand" || canon == "us";
        }

        // ------------------------------ the ops ------------------------------

        /// <summary>Writes or rewrites one note. An empty note DROPS the key — a soul saying a thing
        /// is now nothing to her is a real edit, not a malformed one. Returns what happened, in her
        /// own plain words, for the tool to answer with.</summary>
        public static string Set(IDictionary<string, string> bites, string? key, string? note)
        {
            if (bites == null) throw new ArgumentNullException(nameof(bites));
            var canon = CanonicalKey(key);
            if (canon.Length == 0) return string.Empty;

            var text = Tidy(note);
            if (text.Length == 0) return Drop(bites, key);

            bool had = bites.ContainsKey(canon);
            if (!had && bites.Count >= MaxBites) return string.Empty;   // caller says which to let go of

            bites[canon] = text;
            return had ? canon : canon;
        }

        /// <summary>Strikes one note out. Empty when there was nothing under that key.</summary>
        public static string Drop(IDictionary<string, string> bites, string? key)
        {
            if (bites == null) throw new ArgumentNullException(nameof(bites));
            var canon = CanonicalKey(key);
            if (canon.Length == 0 || !bites.ContainsKey(canon)) return string.Empty;
            bites.Remove(canon);
            return canon;
        }

        /// <summary>True when the shelf is full and something must go before anything new is
        /// written.</summary>
        public static bool IsFull(IDictionary<string, string> bites) =>
            bites != null && bites.Count >= MaxBites;

        /// <summary>One note, trimmed to a note. Cut back to the last finished sentence rather than
        /// mid-word — the same courtesy the memory's own trimmer pays a severed summary.</summary>
        public static string Tidy(string? note)
        {
            var text = (note ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            if (text.Length <= MaxBiteChars) return text;

            var cut = text.Substring(0, MaxBiteChars);
            int stop = cut.LastIndexOfAny(new[] { '.', '!', '?', '…' });
            return stop > MaxBiteChars / 2 ? cut.Substring(0, stop + 1).Trim() : cut.TrimEnd() + "…";
        }

        // ------------------------------ the reading ------------------------------

        /// <summary>The keyed notes as her own sheet carries them. Empty when she holds none.
        /// The prose bite is NOT here — it is rendered in its own place by the prompt builder.</summary>
        public static string Render(IDictionary<string, string>? bites)
        {
            if (bites == null || bites.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var pair in bites.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(pair.Value)) continue;
                sb.Append("- ").Append(pair.Key).Append(": ").AppendLine(pair.Value.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Reads a BITES: section of a memory-writing reply and applies it. The shape asked for is
        /// one edit per line — <c>key: the note</c> to write, <c>-key</c> to strike out — and it is
        /// read leniently, because a model handed a dictionary will sometimes give back JSON braces,
        /// quotes or a leading bullet. Anything with no colon and no minus is skipped rather than
        /// filed under a nonsense key.
        /// </summary>
        /// <returns>How many notes were written or struck out.</returns>
        public static int ApplySection(IDictionary<string, string> bites, string? section)
        {
            if (bites == null) throw new ArgumentNullException(nameof(bites));
            if (string.IsNullOrWhiteSpace(section)) return 0;

            int changed = 0;
            foreach (var raw in section!.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                // JSON furniture and list bullets are stripped rather than refused.
                line = line.Trim('{', '}', ',', '·', '•').Trim();
                if (line.StartsWith("- ", StringComparison.Ordinal)) line = line.Substring(2).Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("-", StringComparison.Ordinal))
                {
                    if (Drop(bites, Unquote(line.Substring(1))).Length > 0) changed++;
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var key = Unquote(line.Substring(0, colon));
                var note = Unquote(line.Substring(colon + 1));
                if (IsProseKey(key)) continue;                       // the prose rides in SUMMARY:
                if (Set(bites, key, note).Length > 0) changed++;
            }
            return changed;
        }

        private static string Unquote(string text) =>
            text.Trim().Trim('"', '“', '”', '\'').Trim();

        /// <summary>
        /// The one-time turning of an old rolling page into the new shape: the whole page becomes
        /// the prose bite and nothing is chopped up by machine. Facts flake off it into notes as she
        /// next writes — which is right, because only she knows which of them are worth a key.
        /// Deliberately does nothing when notes already stand.
        /// </summary>
        public static bool NeedsSeeding(NpcMemory memory) =>
            memory != null && memory.Bites.Count == 0 && !string.IsNullOrWhiteSpace(memory.Summary);

        /// <summary>What a soul is told about her own shelf when it is full — the invitation to let
        /// the weakest go rather than simply refusing her.</summary>
        public static string ShelfFullNote(int max = MaxBites) =>
            $"I hold {max} notes already, which is as many as I keep. To set a new one down I first " +
            "let go of the one that matters least.";

        /// <summary>How the count reads for the player's own eye ("14 of 24 notes").</summary>
        public static string CountLabel(IDictionary<string, string>? bites) =>
            string.Format(CultureInfo.InvariantCulture, "{0} of {1} notes", bites?.Count ?? 0, MaxBites);
    }
}
