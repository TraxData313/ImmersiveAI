using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// Reads the NPC's own answer to the Angel's private question "how did that exchange move your
    /// heart?" — a single signed number. Kept deliberately lenient: weaker models sometimes wrap the
    /// number in a word or two ("about +5"), so we take the first integer we find and clamp it to the
    /// game's -100..100 standing rail.
    ///
    /// This replaced an earlier approach that asked the NPC to smuggle a ♥ mark into the tail of their
    /// spoken reply. Chatty models (e.g. gpt-4o) would simply narrate a number in prose and never emit
    /// the mark, so nothing moved. Asking the question on its own — see
    /// <see cref="PromptBuilder.BuildFeelingQuery"/> — is far more reliable across backends.
    /// </summary>
    public static class FeelingParser
    {
        // First signed 1-3 digit integer anywhere in the answer. We ask for the shift first and alone,
        // so the first number is the delta even if the model tacks on "(now 37)" or similar.
        private static readonly Regex NumberPattern =
            new Regex(@"[+-]?\d{1,3}", RegexOptions.Compiled);

        /// <summary>The felt shift the NPC named, clamped to -100..100, or null if they named no number.</summary>
        public static int? ParseShift(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return null;

            var m = NumberPattern.Match(response);
            if (!m.Success || !int.TryParse(m.Value, out int shift)) return null;

            if (shift < -100) shift = -100;
            if (shift > 100) shift = 100;
            return shift;
        }
    }
}
