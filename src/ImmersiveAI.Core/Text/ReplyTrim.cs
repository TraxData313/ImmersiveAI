using System;
using System.Collections.Generic;
using System.Text;
using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Text
{
    /// <summary>
    /// THE LENGTH GUARD (2026.11.11, the handmaid's prompt step). A model handed a meek, few-worded
    /// soul still drifts to long, advising replies; her sheet asks, and this makes sure. A reply
    /// within the word budget comes back untouched; a longer one is cut back to the last COMPLETE
    /// sentence that fits, never mid-sentence.
    /// <para>Rules: an acted gesture between single asterisks is one unbreakable unit (its own
    /// stops never split it); a new line also ends a unit; sentence ends are read for any script
    /// (. ! ? … and the Arabic, CJK, Devanagari and Greek/Armenian stops), never by capitals; if
    /// even the first spoken sentence is over budget it is kept whole; and the
    /// <c>[voice: …]</c> key is lifted off first, never counted, and put back as the last line.</para>
    /// </summary>
    public static class ReplyTrim
    {
        /// <summary>The reply cut to fit <paramref name="budget"/> words; unchanged when it fits or
        /// when the budget is 0 or less (off).</summary>
        public static string ToWordBudget(string? reply, int budget)
        {
            var text = reply ?? string.Empty;
            if (budget <= 0 || text.Length == 0) return text;

            var body = VoiceLine.Split(text, out var direction);
            if (CountWords(body) <= budget) return text;

            var units = Units(body);
            var kept = new StringBuilder();
            int words = 0;
            bool spokeOne = false;
            foreach (var unit in units)
            {
                int w = CountWords(unit.Text);
                if (words + w <= budget)
                {
                    kept.Append(unit.Text);
                    words += w;
                    if (!unit.IsGesture && w > 0) spokeOne = true;
                    continue;
                }
                if (spokeOne) break;
                // Nothing spoken yet: an act too big to fit is let go, and the first spoken
                // sentence is kept whole, however long it runs.
                if (unit.IsGesture) continue;
                kept.Append(unit.Text);
                break;
            }

            var result = kept.ToString().TrimEnd();
            if (result.Length == 0) return text; // nothing spoken at all: never answer with silence
            if (direction.Length > 0) result += "\n[voice: " + direction + "]";
            return result;
        }

        /// <summary>Words as a listener hears them: whitespace-separated tokens, plus every
        /// ideograph or kana counted as one (scripts written without spaces). Asterisks alone and
        /// bare punctuation are not words.</summary>
        public static int CountWords(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            bool inToken = false;
            bool tokenHasLetter = false;
            foreach (var c in text!)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (inToken && tokenHasLetter) count++;
                    inToken = false;
                    tokenHasLetter = false;
                    continue;
                }
                if (IsSpacelessLetter(c))
                {
                    if (inToken && tokenHasLetter) count++;
                    count++;
                    inToken = false;
                    tokenHasLetter = false;
                    continue;
                }
                inToken = true;
                if (char.IsLetterOrDigit(c)) tokenHasLetter = true;
            }
            if (inToken && tokenHasLetter) count++;
            return count;
        }

        private static bool IsSpacelessLetter(char c) =>
            (c >= '぀' && c <= 'ヿ')    // hiragana, katakana
            || (c >= '㐀' && c <= '䶿') // CJK extension A
            || (c >= '一' && c <= '鿿') // CJK unified
            || (c >= '豈' && c <= '﫿'); // CJK compatibility

        private static bool IsStop(char c) =>
            c == '.' || c == '!' || c == '?' || c == '…'
            || c == '。' || c == '！' || c == '？' || c == '｡'
            || c == '؟' || c == '۔' || c == '।' || c == '॥'
            || c == ';' // the Greek question mark (not the semicolon it looks like)
            || c == '։';      // the Armenian full stop

        private static bool IsCloser(char c) =>
            c == '"' || c == '\'' || c == '”' || c == '’' || c == '»' || c == '›' || c == ')' || c == ']'
            || c == '」' || c == '』' || c == '）';

        private readonly struct Unit
        {
            public Unit(string text, bool isGesture) { Text = text; IsGesture = isGesture; }
            public string Text { get; }
            public bool IsGesture { get; }
        }

        // Cuts the body into units, each carrying its own trailing whitespace so the kept ones
        // concatenate back to exactly the original text.
        private static List<Unit> Units(string s)
        {
            var units = new List<Unit>();
            var cur = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];

                // A gesture: from a single asterisk to its closing one, whole. An unclosed one is
                // read as plain words.
                if (c == '*' && !(i + 1 < s.Length && s[i + 1] == '*'))
                {
                    int close = s.IndexOf('*', i + 1);
                    if (close > i)
                    {
                        Flush(units, cur, false);
                        int end = close + 1;
                        while (end < s.Length && char.IsWhiteSpace(s[end])) end++;
                        units.Add(new Unit(s.Substring(i, end - i), true));
                        i = end;
                        continue;
                    }
                }

                cur.Append(c);
                i++;

                if (c == '\n')
                {
                    while (i < s.Length && char.IsWhiteSpace(s[i])) cur.Append(s[i++]);
                    Flush(units, cur, false);
                    continue;
                }

                if (IsStop(c))
                {
                    // A run of stops ("?!", "...") and closing quotes belongs to this sentence.
                    while (i < s.Length && (IsStop(s[i]) || IsCloser(s[i]))) cur.Append(s[i++]);
                    // CJK stops need no space after them; every other stop ends a sentence only
                    // before whitespace or the end (so "3.5" and "e.g." mid-word stay whole).
                    bool spaceless = c == '。' || c == '！' || c == '？' || c == '｡';
                    if (i >= s.Length || char.IsWhiteSpace(s[i]) || spaceless)
                    {
                        while (i < s.Length && char.IsWhiteSpace(s[i]) && s[i] != '\n') cur.Append(s[i++]);
                        if (i < s.Length && s[i] == '\n')
                            while (i < s.Length && char.IsWhiteSpace(s[i])) cur.Append(s[i++]);
                        Flush(units, cur, false);
                    }
                }
            }
            Flush(units, cur, false);
            return units;
        }

        private static void Flush(List<Unit> units, StringBuilder cur, bool gesture)
        {
            if (cur.Length == 0) return;
            units.Add(new Unit(cur.ToString(), gesture));
            cur.Clear();
        }
    }
}
