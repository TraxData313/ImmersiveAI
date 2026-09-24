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

        /// <summary>
        /// The line as a voice that can ACT would say it: a gesture that is a sound becomes the sound
        /// itself, written the way the voice app wants it — <c>*laughs softly*</c> becomes
        /// <c>(laugh)</c>, heard as a laugh rather than read as a word — and a whispered gesture makes
        /// the whole line a whisper.
        /// <para>
        /// Only what the engine says it can do is asked of it. <paramref name="sounds"/> is the voice
        /// app's own list for the engine speaking now (Breeze: laugh, sigh, cough, clears throat; the
        /// others: none), and <paramref name="takesMood"/> whether it follows a mood at all. With
        /// neither, this is exactly <see cref="SpokenWithGestures"/> or <see cref="SpokenOnly"/>, so an
        /// engine that cannot act loses nothing it had.
        /// </para>
        /// <para>
        /// A short gesture that IS the sound — <c>*chuckles*</c>, <c>*sighs deeply*</c> — is replaced by
        /// it whole; read aloud after the laugh it would be the same moment twice. A longer one keeps
        /// its narration after the sound when acted parts are read, because <c>*laughs and pours the
        /// wine*</c> is two things and only the first of them is a sound.
        /// </para>
        /// </summary>
        public static PerformedLine Performed(string? body, bool speakActed, ICollection<string>? sounds, bool takesMood)
        {
            var line = new PerformedLine();
            var segments = EmoteText.Split(body);
            if (segments.Count == 0) return line;

            var parts = new List<string>(segments.Count);
            foreach (var segment in segments)
            {
                if (!segment.IsGesture) { parts.Add(segment.Text); continue; }

                var gesture = segment.Text;
                var whisper = takesMood && IsWhisper(gesture);
                if (whisper) line.Mood = "whisper";

                var sound = SoundIn(gesture, sounds);
                var shortGesture = WordCount(gesture) <= ShortGestureWords;
                if (sound != null) parts.Add("(" + sound + ")");

                // The narration, when it is wanted and the gesture said more than a sound or a
                // whisper already carries.
                var carried = sound != null || whisper;
                if (speakActed && !(carried && shortGesture)) parts.Add(Closed(gesture));
            }

            var text = string.Join(" ", parts);

            // A mood the soul wrote herself — "(tender) I am here" — is taken out of the words whether
            // or not this engine follows one, so it is never read aloud; where it does, it wins over a
            // whispered gesture, being the more deliberate of the two.
            var mood = TakeMood(ref text);
            if (takesMood && mood.Length > 0) line.Mood = mood;

            line.Text = Collapse(Normalize(text));
            return line;
        }

        /// <summary>The moods claude-voice knows by name (its docs/api.md, "Moods, beside the words").
        /// A soul is offered exactly these, and only these are lifted out of her words.</summary>
        public static readonly string[] Moods =
        {
            "happy", "excited", "playful", "calm", "tender", "sad", "tired", "serious", "whisper",
            "surprised", "angry",
        };

        /// <summary>The words a soul reaches for instead of a mood's own name — claude-voice's own
        /// table, and the ones Crushfinger taught us on the first evening ("(startled)"). Each is taken
        /// as the mood it means.</summary>
        public static readonly Dictionary<string, string> MoodAliases = new Dictionary<string, string>
        {
            ["whispering"] = "whisper", ["whispered"] = "whisper", ["whispers"] = "whisper",
            ["hushed"] = "whisper", ["quietly"] = "whisper", ["softly"] = "tender", ["soft"] = "tender",
            ["tenderly"] = "tender", ["warm"] = "tender", ["warmly"] = "tender", ["fond"] = "tender",
            ["fondly"] = "tender", ["loving"] = "tender", ["lovingly"] = "tender",
            ["sadly"] = "sad", ["sorrowful"] = "sad", ["mournful"] = "sad",
            ["joyful"] = "happy", ["cheerful"] = "happy", ["happily"] = "happy", ["bright"] = "happy",
            ["excitedly"] = "excited", ["eager"] = "excited", ["eagerly"] = "excited",
            ["gentle"] = "calm", ["gently"] = "calm", ["calmly"] = "calm",
            ["weary"] = "tired", ["wearily"] = "tired", ["sleepy"] = "tired",
            ["mad"] = "angry", ["angrily"] = "angry", ["furious"] = "angry",
            ["shocked"] = "surprised", ["startled"] = "surprised", ["astonished"] = "surprised",
            ["teasing"] = "playful", ["teasingly"] = "playful", ["playfully"] = "playful", ["amused"] = "playful",
            ["grim"] = "serious", ["grave"] = "serious", ["gravely"] = "serious", ["stern"] = "serious",
        };

        /// <summary>The mood a word means — its own name or one of <see cref="MoodAliases"/> — or empty.</summary>
        public static string MoodOf(string? word)
        {
            var w = (word ?? string.Empty).Trim().ToLowerInvariant();
            if (Array.IndexOf(Moods, w) >= 0) return w;
            return MoodAliases.TryGetValue(w, out var m) ? m : string.Empty;
        }

        private static readonly System.Text.RegularExpressions.Regex MoodTag =
            new System.Text.RegularExpressions.Regex(@"\(\s*([A-Za-z]+)\s*\)\s*",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static readonly System.Text.RegularExpressions.Regex LeadingTag =
            new System.Text.RegularExpressions.Regex(@"^\s*\(\s*([A-Za-z]+)\s*\)\s*",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>Removes every one-word bracket naming a mood (by name or alias) from
        /// <paramref name="text"/> and returns the first such mood, or empty. A one-word bracket that
        /// OPENS the line is her stage direction whatever the word — "(startled)", "(wary)" — and is
        /// never read aloud either. Sounds — (laugh) — and any other bracket stay.</summary>
        public static string TakeMood(ref string text)
        {
            var found = string.Empty;
            var t = text ?? string.Empty;
            var lead = LeadingTag.Match(t);
            if (lead.Success && !IsSoundWord(lead.Groups[1].Value))
            {
                found = MoodOf(lead.Groups[1].Value);
                t = t.Substring(lead.Length);
            }
            text = MoodTag.Replace(t, m =>
            {
                var mood = MoodOf(m.Groups[1].Value);
                if (mood.Length == 0) return m.Value;
                if (found.Length == 0) found = mood;
                return string.Empty;
            });
            return found;
        }

        private static bool IsSoundWord(string word)
        {
            foreach (var pair in Sounds)
                if (pair.Value.IsMatch(word)) return true;
            return false;
        }

        /// <summary>
        /// What the voice will DO with a gesture, written the way the thread shows it: "(laugh)" for
        /// *laughs softly*, "(whisper)" for *whispers*, both, or empty. The same judgement
        /// <see cref="Performed"/> makes, so what the player sees beside a gesture is exactly what is
        /// heard (Anton, 2026.09.24: the app showed "(laugh)" where the game showed only the gesture).
        /// </summary>
        public static string CuesOf(string? gesture, ICollection<string>? sounds, bool takesMood)
        {
            if (string.IsNullOrWhiteSpace(gesture)) return string.Empty;
            var cues = new List<string>(2);
            if (takesMood && IsWhisper(gesture!)) cues.Add("(whisper)");
            var sound = SoundIn(gesture!, sounds);
            if (sound != null) cues.Add("(" + sound + ")");
            return string.Join(" ", cues);
        }

        /// <summary>A gesture this short that holds a sound is taken to BE the sound.</summary>
        private const int ShortGestureWords = 4;

        /// <summary>The sounds, by the words a writer reaches for. Cyrillic too: the mod is played in
        /// Bulgarian, and a laugh is a laugh in any alphabet even where the words around it cannot be
        /// read by the engine that would make it.</summary>
        private static readonly KeyValuePair<string, System.Text.RegularExpressions.Regex>[] Sounds =
        {
            Sound("laugh", @"\b(laugh\w*|chuckl\w*|giggl\w*|snicker\w*|chortl\w*|cackl\w*)\b|смя|смее|кикот|засмя"),
            Sound("sigh", @"\bsigh\w*\b|въздиш|въздъхн"),
            Sound("cough", @"\bcough\w*\b|кашл|изкашл"),
            Sound("clears throat", @"\bclear\w*\s+(?:\w+\s+)?throat\b|прочиств\w*\s+гърло"),
        };

        private static readonly System.Text.RegularExpressions.Regex Whisper =
            new System.Text.RegularExpressions.Regex(@"\bwhisper\w*\b|шепн|шепот|прошеп",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static KeyValuePair<string, System.Text.RegularExpressions.Regex> Sound(string name, string pattern)
            => new KeyValuePair<string, System.Text.RegularExpressions.Regex>(name,
                new System.Text.RegularExpressions.Regex(pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant));

        private static string? SoundIn(string gesture, ICollection<string>? sounds)
        {
            if (sounds == null || sounds.Count == 0) return null;
            foreach (var pair in Sounds)
                if (sounds.Contains(pair.Key) && pair.Value.IsMatch(gesture)) return pair.Key;
            return null;
        }

        private static bool IsWhisper(string gesture) => Whisper.IsMatch(gesture);

        private static int WordCount(string text)
            => text.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

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

namespace ImmersiveAI.Core.Voices
{
    /// <summary>What <see cref="SpeakableText.Performed"/> hands back: the words to send, and a mood
    /// for the whole line ("whisper") or empty for none.</summary>
    public sealed class PerformedLine
    {
        public string Text { get; set; } = string.Empty;
        public string Mood { get; set; } = string.Empty;
    }
}
