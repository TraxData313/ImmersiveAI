using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>
    /// Turns a written message into what is actually SAID, and cuts it into bites a speech engine
    /// can start on before the rest exists.
    /// <para>
    /// Two jobs, both load-bearing. First, gestures are acted, not spoken: the acting-out grammar
    /// (<see cref="PromptBuilder.ActingOutGuidance"/>) means a reply can carry *sets down her cup*
    /// beside its words, and a voice reading that aloud is instantly a robot reading stage
    /// directions. Second, a whole reply handed to an engine in one piece means silence until the
    /// last word of it is generated; cut at sentence ends and the first sentence can be in the
    /// air while the third is still being made. That is the entire difference between "it speaks"
    /// and "it speaks quickly".
    /// </para>
    /// </summary>
    public static class SpeakableText
    {
        /// <summary>Below this a bite is not worth its own synthesis call — it is folded into its
        /// neighbour. "Yes." on its own arrives as a bark with an audible seam after it.</summary>
        public const int DefaultMinChars = 40;

        /// <summary>Above this a bite is split at the last comfortable pause, so one runaway
        /// sentence cannot hold the whole reply silent while it generates.</summary>
        public const int DefaultMaxChars = 260;

        /// <summary>
        /// Symbols that are worth SAYING rather than dropping, and what they become.
        /// <para>
        /// The dashes turn into commas and the ellipsis into a full stop on purpose: they are pauses
        /// in the writing, so a pause is what they should be in the reading. Borrowed whole from the
        /// sister project's <c>voice_lib._SPOKEN</c>, where it has ridden a thousand generations.
        /// </para>
        /// </summary>
        private static readonly Dictionary<char, string> Spoken = new Dictionary<char, string>
        {
            // The trailing space matters: "soon—and" must become "soon, and" and never "soon,and",
            // which is one strange blob to a tokenizer where the whole point was to remove one.
            // A space that lands in FRONT of the comma is swept up again by Tidy.
            ['—'] = ", ", ['–'] = ", ", ['―'] = ", ",
            ['‘'] = "'", ['’'] = "'", ['“'] = "\"", ['”'] = "\"", ['„'] = "\"", ['«'] = "\"", ['»'] = "\"",
            ['…'] = ".", [' '] = " ", ['​'] = "",
            ['×'] = " times ", ['°'] = " degrees ", ['±'] = " plus or minus ",
            ['→'] = " to ", ['←'] = " from ", ['⇒'] = " gives ",
            ['≤'] = " at most ", ['≥'] = " at least ", ['≠'] = " not equal to ",
            ['•'] = " ", ['·'] = " ", ['✓'] = " ", ['✗'] = " ", ['❦'] = " ", ['☾'] = " ", ['✒'] = " ",
        };

        /// <summary>
        /// Every character that reaches the speech engine, and nothing else.
        /// <para>
        /// THE REASON THIS EXISTS (2026.08.17, Anton's own catch — "maybe in bannerlord they say some
        /// strange symbols?"). The sister project speaks through the same DLL on the same card and
        /// derailed ONCE in about a thousand generations; this mod derailed TWELVE times in 196. The
        /// engine is therefore not the problem, and one of the two things it is handed differently
        /// is the text: that project passes every character through this whitelist and we passed
        /// none. An em dash, a curly quote, a zero-width space or a bullet is a rare token to a
        /// speech tokenizer, and a rare token is exactly the sort of thing an autoregressive model
        /// wanders off after — it never emits its end-of-speech and generates until it hits the rail.
        /// </para>
        /// <para>
        /// The rule is a whitelist rather than a blacklist because the failure is silent and the
        /// space of strange characters is unbounded: a model writing in-world prose reaches for
        /// typographic dashes and quotes constantly, and nothing downstream would ever complain.
        /// LETTERS AND DIGITS OF EVERY SCRIPT PASS — Anton plays in Bulgarian, and asking for
        /// <c>[A-Za-z0-9]</c> would silently throw away every Cyrillic word in the mod.
        /// </para>
        /// </summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text!.Length);
            foreach (var c in text!)
            {
                string mapped;
                if (Spoken.TryGetValue(c, out mapped)) { sb.Append(mapped); continue; }

                // Plain ASCII is always safe, except the control codes, which are not speech.
                if (c < 128)
                {
                    sb.Append(c < 32 && c != '\n' && c != '\t' ? ' ' : c);
                    continue;
                }

                // A letter or a digit in ANY script — Cyrillic, Greek, anything.
                if (char.IsLetterOrDigit(c)) { sb.Append(c); continue; }

                sb.Append(' ');
            }
            return Tidy(sb.ToString());
        }

        /// <summary>
        /// Clears up after the sweep: a space in front of punctuation (left by a dropped symbol, or
        /// by a dash that became a comma), and a run of the same mark where two of them met.
        /// Without it "a — b" reads out as "a , b" and a swept bullet leaves "· ." behind.
        /// </summary>
        private static string Tidy(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (IsTightPunct(c))
                {
                    // Drop the whitespace we just wrote in front of it...
                    while (sb.Length > 0 && (sb[sb.Length - 1] == ' ' || sb[sb.Length - 1] == '\t'))
                        sb.Length--;
                    // ...and never write the same mark twice running.
                    if (sb.Length > 0 && sb[sb.Length - 1] == c) continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool IsTightPunct(char c)
            => c == ',' || c == '.' || c == ';' || c == ':' || c == '!' || c == '?';

        /// <summary>The words of a message, with the gestures taken out. Returns empty when there
        /// is nothing to say (a reply that was ALL gesture, or nothing at all).</summary>
        public static string SpokenOnly(string? body)
        {
            var segments = EmoteText.Split(body);
            if (segments.Count == 0) return string.Empty;

            var spoken = segments.Where(s => !s.IsGesture).Select(s => s.Text);
            return Collapse(Normalize(string.Join(" ", spoken)));
        }

        /// <summary>
        /// The whole message including its gestures, for a player who would rather hear them read as
        /// narration than lose them (Anton, 2026.08.15 — a toggle, on by default).
        /// <para>
        /// The asterisks themselves never reach the engine: <see cref="EmoteText"/> hands back the
        /// gesture's CONTENT, so what is read aloud is "I pour the wine", never "asterisk I pour the
        /// wine asterisk". And a segment that ends on a word is closed with a full stop, because
        /// otherwise a gesture runs straight into the sentence after it — "I pour the wine It was a
        /// hard day" — which reads as one breathless line and gives <see cref="Chunk"/> nowhere to
        /// breathe. Punctuation already there is left exactly as it stands.
        /// </para>
        /// </summary>
        public static string SpokenWithGestures(string? body)
        {
            var segments = EmoteText.Split(body);
            if (segments.Count == 0) return string.Empty;
            return Collapse(Normalize(string.Join(" ", segments.Select(s => Closed(s.Text)))));
        }

        /// <summary>Gives a segment an ending when it has none, so the next one does not run into it.
        /// Only a segment finishing on a letter or a digit is touched — a comma, a dash or a question
        /// mark is the writer's own choice and is left alone.</summary>
        private static string Closed(string text)
        {
            var t = (text ?? string.Empty).TrimEnd();
            if (t.Length == 0) return t;
            var last = t[t.Length - 1];
            return char.IsLetterOrDigit(last) ? t + "." : t;
        }

        /// <summary>True when this body has words worth sending to an engine at all.</summary>
        public static bool IsWorthSpeaking(string? body) => SpokenOnly(body).Length > 0;

        /// <summary>
        /// Cuts speakable text into bites at sentence ends. Runs of terminators stay whole ("…",
        /// "?!"), a full stop followed by a lower-case letter is treated as an abbreviation rather
        /// than an end, bites under <paramref name="minChars"/> keep growing, and anything still
        /// over <paramref name="maxChars"/> is split at its last pause. A trailing scrap is folded
        /// back into the bite before it, so a reply never ends on a two-word stub.
        /// </summary>
        public static IReadOnlyList<string> Chunk(
            string? text,
            int minChars = DefaultMinChars,
            int maxChars = DefaultMaxChars)
        {
            var bites = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return bites;
            if (minChars < 1) minChars = 1;
            if (maxChars < minChars) maxChars = minChars;

            var s = text!;
            var start = 0;
            var i = 0;

            while (i < s.Length)
            {
                var c = s[i];

                if (c == '\n')
                {
                    // A hard line break is always an honest place to breathe.
                    Flush(bites, s.Substring(start, i - start), minChars);
                    i++;
                    start = i;
                    continue;
                }

                if (!IsTerminator(c)) { i++; continue; }

                // Swallow the whole run: "..." and "?!" are one ending, not three.
                var end = i;
                while (end + 1 < s.Length && IsTerminator(s[end + 1])) end++;

                // Closing quotes and brackets belong to the sentence they close.
                var after = end + 1;
                while (after < s.Length && IsClosingMark(s[after])) after++;

                if (IsContinuation(s, i, end, after))
                {
                    i = after;
                    continue;
                }

                var candidate = s.Substring(start, after - start);
                if (candidate.Trim().Length < minChars)
                {
                    // Too short to stand alone — keep gathering rather than bark one word.
                    i = after;
                    continue;
                }

                Flush(bites, candidate, minChars);
                start = after;
                i = after;
            }

            if (start < s.Length)
                Flush(bites, s.Substring(start), minChars);

            // Fold a final scrap backwards; better a slightly long last bite than a stub.
            if (bites.Count > 1 && bites[bites.Count - 1].Length < minChars)
            {
                bites[bites.Count - 2] = bites[bites.Count - 2] + " " + bites[bites.Count - 1];
                bites.RemoveAt(bites.Count - 1);
            }

            return bites.SelectMany(b => SplitLong(b, maxChars)).ToList();
        }

        /// <summary>The usual road: a written message in, the bites to speak out.</summary>
        public static IReadOnlyList<string> BitesFor(
            string? body,
            bool includeGestures = false,
            int minChars = DefaultMinChars,
            int maxChars = DefaultMaxChars)
        {
            var text = includeGestures ? SpokenWithGestures(body) : SpokenOnly(body);
            return Chunk(text, minChars, maxChars);
        }

        // ------------------------------------------------------------------

        private static void Flush(List<string> into, string piece, int minChars)
        {
            var trimmed = piece.Trim();
            if (trimmed.Length == 0) return;

            // Growing the previous bite is better than emitting a runt.
            if (into.Count > 0 && trimmed.Length < minChars)
            {
                into[into.Count - 1] = into[into.Count - 1] + " " + trimmed;
                return;
            }
            into.Add(trimmed);
        }

        private static IEnumerable<string> SplitLong(string bite, int maxChars)
        {
            while (bite.Length > maxChars)
            {
                var cut = LastPauseBefore(bite, maxChars);
                if (cut <= 0)
                {
                    // No pause and no space to be found: hand it over whole rather than
                    // slice a word in half. A long unbroken run is the engine's problem.
                    break;
                }
                yield return bite.Substring(0, cut).Trim();
                bite = bite.Substring(cut).Trim();
            }
            if (bite.Length > 0) yield return bite;
        }

        private static int LastPauseBefore(string s, int limit)
        {
            var ceiling = Math.Min(limit, s.Length - 1);
            for (var i = ceiling; i > 0; i--)
                if (s[i] == ',' || s[i] == ';' || s[i] == ':' || s[i] == '—' || s[i] == '–')
                    return i + 1;
            for (var i = ceiling; i > 0; i--)
                if (char.IsWhiteSpace(s[i]))
                    return i + 1;
            return -1;
        }

        private static bool IsTerminator(char c)
            => c == '.' || c == '!' || c == '?' || c == '…';

        private static bool IsClosingMark(char c)
            => c == '"' || c == '\'' || c == '»' || c == '”' || c == '’'
               || c == ')' || c == ']' || c == '„' || c == '“';

        /// <summary>
        /// True when a run of stops is NOT the end of a thought, and so is no place to cut.
        /// Two cases, both settled by what follows being lower-case:
        /// a lone '.' is then an abbreviation ("e.g.", "св. Иван"), and an ellipsis is then a
        /// trailing-off that the same breath picks up again ("I had not thought to see you
        /// again... not after everything"). Cutting either one mid-thought puts an audible seam
        /// exactly where the voice should have hesitated instead. '!' and '?' always end.
        /// </summary>
        private static bool IsContinuation(string s, int runStart, int runEnd, int after)
        {
            var loneStop = runEnd == runStart && s[runStart] == '.';
            var ellipsis = s[runStart] == '…'
                           || (runEnd > runStart && AllDots(s, runStart, runEnd));
            if (!loneStop && !ellipsis) return false;

            var j = after;
            while (j < s.Length && s[j] == ' ') j++;
            if (j >= s.Length) return false;
            return char.IsLower(s[j]);
        }

        private static bool AllDots(string s, int from, int to)
        {
            for (var i = from; i <= to; i++)
                if (s[i] != '.') return false;
            return true;
        }

        private static string Collapse(string s)
        {
            var sb = new StringBuilder(s.Length);
            var lastWasSpace = false;
            foreach (var c in s)
            {
                var isSpace = c == ' ' || c == '\t';
                if (isSpace)
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                    continue;
                }
                sb.Append(c);
                lastWasSpace = false;
            }
            return sb.ToString().Trim();
        }
    }
}
