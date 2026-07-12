using System.Collections.Generic;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>One piece of a spoken message: either words said aloud, or a small acted
    /// gesture that rode between *asterisks* (the acting-out grammar — see
    /// <see cref="PromptBuilder.ActingOutGuidance"/>).</summary>
    public readonly struct EmoteSegment
    {
        public EmoteSegment(string text, bool isGesture)
        {
            Text = text;
            IsGesture = isGesture;
        }

        public string Text { get; }
        public bool IsGesture { get; }
    }

    /// <summary>
    /// Splits a spoken message into speech and *gesture* segments so the chat window can draw
    /// actions as soft narration between the spoken words. The grammar is deliberately strict —
    /// a gesture is a SINGLE-asterisk span on one line, hugging its content (*smiles*, never
    /// * smiles or **bold**) — so a stray asterisk or markdown leftover stays literal text
    /// instead of swallowing half a message.
    /// </summary>
    public static class EmoteText
    {
        /// <summary>Splits <paramref name="body"/> into speech and gesture segments, in order.
        /// Speech segments are trimmed and blank ones dropped; a body with no valid gesture
        /// spans comes back as one speech segment. Null/blank input yields an empty list.</summary>
        public static IReadOnlyList<EmoteSegment> Split(string? body)
        {
            var segments = new List<EmoteSegment>();
            if (string.IsNullOrWhiteSpace(body)) return segments;
            var text = body!;

            int cursor = 0;   // start of the pending speech run
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] != '*') { i++; continue; }

                // A doubled asterisk is markdown residue, not a gesture — leave both literal.
                if (i + 1 < text.Length && text[i + 1] == '*') { i += 2; continue; }

                int close = FindClose(text, i);
                if (close < 0) { i++; continue; }

                AddSpeech(segments, text, cursor, i);
                var gesture = text.Substring(i + 1, close - i - 1).Trim();
                if (gesture.Length > 0)
                    segments.Add(new EmoteSegment(gesture, isGesture: true));
                cursor = close + 1;
                i = close + 1;
            }

            AddSpeech(segments, text, cursor, text.Length);
            return segments;
        }

        /// <summary>True when the body carries at least one valid *gesture* span.</summary>
        public static bool HasGesture(string? body)
        {
            var segments = Split(body);
            foreach (var seg in segments)
                if (seg.IsGesture) return true;
            return false;
        }

        // The closing asterisk of a span opened at `open`: content must start and end on
        // non-whitespace, contain no asterisk or line break, and be non-empty.
        private static int FindClose(string text, int open)
        {
            if (open + 1 >= text.Length) return -1;
            if (char.IsWhiteSpace(text[open + 1])) return -1;
            for (int j = open + 1; j < text.Length; j++)
            {
                char c = text[j];
                if (c == '\n' || c == '\r') return -1;
                if (c != '*') continue;
                if (j == open + 1) return -1;                    // empty span
                if (char.IsWhiteSpace(text[j - 1])) return -1;   // "* smiles *" stays literal
                return j;
            }
            return -1;
        }

        private static void AddSpeech(List<EmoteSegment> segments, string text, int start, int end)
        {
            if (end <= start) return;
            var speech = text.Substring(start, end - start).Trim();
            if (speech.Length > 0)
                segments.Add(new EmoteSegment(speech, isGesture: false));
        }
    }
}
