using System.Linq;

namespace ImmersiveAI.Core.Initiation
{
    /// <summary>
    /// Reads the NPC's answers in the two-step reaching-out flow: first whether they wish to go to the
    /// player at all (<see cref="WantsToReachOut"/>, a yes/no), then — only if they do — a safety check on
    /// the greeting they give (<see cref="IsDecline"/>). The reaching-out is always theirs to choose: the
    /// mod never forces a silent NPC to speak, so a negative or empty answer simply lets the moment pass.
    /// </summary>
    public static class InitiationParser
    {
        /// <summary>True when the NPC's yes/no answer says they DO wish to seek the player out. Read
        /// leniently around a one-word answer, but a clear "no"/"nay"/"not" wins, and a blank or unreadable
        /// answer is treated as "no" so the player is never troubled on a mere ambiguity.</summary>
        public static bool WantsToReachOut(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return false;

            var t = reply.Trim().ToLowerInvariant();
            if (t.StartsWith("no") || t.StartsWith("nay") || t.StartsWith("not")) return false;
            if (t.StartsWith("yes") || t.StartsWith("aye") || t.StartsWith("sure")
                || t.StartsWith("gladly") || t.StartsWith("of course")) return true;

            // Otherwise take a standalone "yes" anywhere as assent; anything else is left as "no".
            return System.Text.RegularExpressions.Regex.IsMatch(t, "\\byes\\b");
        }

        /// <summary>Reads the NO / "YES: what I want to discuss" resolution of a reach-out ponder (the
        /// first-person weighing that replaced the Angel's question on 2026.07.26; YES/NO rather than
        /// STAY/GO so the words never smell of physically leaving — Anton, 2026.07.27). Lenient around
        /// dressing — markdown, quotes, "I go"/"I stay" phrasings, the old STAY/GO shape — but an
        /// unreadable answer falls back to the plain yes/no reading, and anything still unclear is a NO,
        /// so the player is never troubled on a mumble. The reason (what they resolved to discuss) is
        /// handed back trimmed and single-line; empty when they gave none.</summary>
        public static bool WantsToGo(string? reply, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(reply)) return false;

            // Strip the dressing an eager model may wrap the verdict in.
            var t = reply!.Trim().TrimStart('*', '-', '>', '#', '(', '[', '"', '“', '\'', ' ');
            var lower = t.ToLowerInvariant();

            if (StartsWithWord(lower, "no") || StartsWithWord(lower, "nay")
                || StartsWithWord(lower, "stay") || StartsWithWord(lower, "i stay")
                || StartsWithWord(lower, "i remain") || StartsWithWord(lower, "i keep")) return false;

            if (StartsWithWord(lower, "yes") || StartsWithWord(lower, "aye"))
            {
                reason = CleanReason(t.Substring(3));
                return true;
            }
            if (StartsWithWord(lower, "go"))
            {
                reason = CleanReason(t.Substring(2));
                return true;
            }
            if (StartsWithWord(lower, "i go") || StartsWithWord(lower, "i will go") || StartsWithWord(lower, "i cross"))
            {
                var idx = t.IndexOfAny(new[] { ':', '—' });
                if (idx >= 0 && idx + 1 < t.Length) reason = CleanReason(t.Substring(idx + 1));
                return true;
            }

            // Not the asked-for shape — fall back to the plain yes/no reading (no reason to hand back).
            return WantsToReachOut(reply);
        }

        // True when the text begins with the given word(s) followed by a non-letter or the end — so
        // "go" never matches "good day" and "stay" never matches "staying power"... which it would.
        private static bool StartsWithWord(string text, string word)
        {
            if (!text.StartsWith(word, System.StringComparison.Ordinal)) return false;
            return text.Length == word.Length || !char.IsLetter(text[word.Length]);
        }

        // One line, unquoted, unpadded — the reason travels into prompts and recorded notes.
        private static string CleanReason(string raw)
        {
            var r = (raw ?? string.Empty).Trim().TrimStart(':', '—', '-', ',', '.', ' ', '"', '“', '*')
                .TrimEnd('"', '”', ')', ']', '*', ' ');
            r = System.Text.RegularExpressions.Regex.Replace(r, "\\s+", " ").Trim();
            return r.Length > 240 ? r.Substring(0, 240).TrimEnd() + "…" : r;
        }

        /// <summary>True when the NPC chose not to reach out (an empty or single-word declining answer).</summary>
        public static bool IsDecline(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return true;

            // Reduce to bare letters so wrapping punctuation or brackets ("(none)", "None.", "«no»")
            // can't hide a declining answer. A real opening line has far more than one word to it.
            var letters = new string(reply.Where(char.IsLetter).ToArray()).ToLowerInvariant();
            if (letters.Length == 0) return true;

            // Only treat as declining when the WHOLE answer is one of these words — a real greeting that
            // merely happens to contain "no" ("No wonder you've come...") must still count as reaching out.
            var words = reply.Split(new[] { ' ', '\t', '\r', '\n' },
                System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 2) return false;

            return letters == "none" || letters == "pass" || letters == "no"
                || letters == "notnow" || letters == "nothing";
        }
    }
}
