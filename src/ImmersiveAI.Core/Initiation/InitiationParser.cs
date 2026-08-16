using System.Linq;

namespace ImmersiveAI.Core.Initiation
{
    /// <summary>
    /// Reads the yes/no answers the NPC still gives about their own correspondence
    /// (<see cref="WantsToReachOut"/> — do I write back to this letter?), plus a safety check on words
    /// meant as an opening (<see cref="IsDecline"/>). Nothing here gates a reaching-out any more: the
    /// spoken reach-out and the spontaneous letter both lost their asking step on 2026.08.16 — the roll
    /// picks, and the picked soul speaks or writes (see PromptBuilder's retirement note). Silence is
    /// still theirs; it is simply no longer solicited.
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

        // WantsToGo — the reader for the ponder's "NO / YES: the something" — went out with the ponder
        // itself on 2026.08.16. It is not a compat rail: nothing persisted was ever written in its
        // shape, so there is nothing left to read. Old ponder ANSWERS live on as plain recorded text.

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
