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
