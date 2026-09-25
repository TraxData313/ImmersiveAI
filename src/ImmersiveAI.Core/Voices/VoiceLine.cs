using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>
    /// A soul's own direction for how her words sound — the last line of a reply, <c>[voice: low and
    /// teasing, a smile in it]</c> — while the engine speaking can follow one (Qwen, Breeze).
    /// <para>
    /// Anton, 2026.09.25: his Abby fills a "sound" field on every reply and the NPCs, merely offered a
    /// mood word, left it out. So it is a KEY now, the same shape every time, required by the sheet
    /// (<see cref="Prompts.PromptBuilder.VoiceGuidance"/>) and kept in the recorded words, so each of
    /// her own past replies reminds her of the form. It is lifted off before anything is spoken — the
    /// voice app gets it as its <c>instruction</c> — and drawn small and orange under the reply, never
    /// inside it. Every bracketed key in the text is taken, wherever it slipped to; the last one wins.
    /// </para>
    /// </summary>
    public static class VoiceLine
    {
        /// <summary>claude-voice takes at most this many characters of instruction.</summary>
        public const int MaxLength = 200;

        private static readonly Regex Key = new Regex(
            @"[ \t]*[\[\(]\s*voice\s*[:：\-–—]\s*([^\]\)\r\n]{1,300}?)\s*[\]\)][ \t]*(\r?\n)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The reply without its voice key, and the direction it held (empty when none).</summary>
        public static string Split(string? reply, out string direction)
        {
            var found = string.Empty;
            var body = Key.Replace(reply ?? string.Empty, m =>
            {
                found = m.Groups[1].Value.Trim();
                return string.Empty;
            });
            if (found.Length > MaxLength) found = found.Substring(0, MaxLength).TrimEnd();
            direction = found;
            return found.Length == 0 && body.Length == (reply ?? string.Empty).Length ? reply ?? string.Empty : body.TrimEnd();
        }

        /// <summary>Just the reply, for everywhere the direction must not show: speech, notices, the
        /// face-to-face panel, anything quoted back.</summary>
        public static string Strip(string? reply) => Split(reply, out _);
    }
}
