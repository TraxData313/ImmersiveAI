using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Text
{
    /// <summary>
    /// LINE BREAKS ESCAPED TWICE (2026.10.15, Safiya on the Claude Code road): now and then a model
    /// writing its answer as a JSON string escapes the escape — <c>"…cream.\\n\\n*I nudge…"</c> —
    /// so the honest parse hands back a literal backslash-n, and the thread showed "\n\n" between
    /// her gestures and her words. Measured: 3 of 30 replies in one session, the rest clean.
    /// <para>A backslash before n/r/t (or a quote) in spoken words is never speech in Calradia, so
    /// the sequence is put back to what it meant. A doubled backslash ("\\\\") is left alone. A
    /// string with no backslash comes back as the very same instance.</para>
    /// </summary>
    public static class EscapedBreaks
    {
        private static readonly Regex Escape = new Regex(@"(?<!\\)\\(r\\n|n|r|t|"")", RegexOptions.Compiled);

        /// <summary>The text with literal \n, \r\n, \r, \t and \" turned back into what they stood for.</summary>
        public static string Heal(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (text!.IndexOf('\\') < 0) return text;
            return Escape.Replace(text, m =>
            {
                switch (m.Groups[1].Value)
                {
                    case "t": return "\t";
                    case "\"": return "\"";
                    default: return "\n";
                }
            });
        }
    }
}
