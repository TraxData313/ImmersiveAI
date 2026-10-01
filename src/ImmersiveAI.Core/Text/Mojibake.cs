using System.Collections.Generic;

namespace ImmersiveAI.Core.Text
{
    /// <summary>
    /// UTF-8 READ AS WINDOWS-1252 AND WRITTEN BACK AS UTF-8 (2026.10.01, found in Anton's own
    /// config.json): every em dash of his RoleplayGuidance had become "â€”". Beyond reading badly
    /// and costing 1.6x a token for each of the three junk characters, it broke an EQUALITY test —
    /// the 2026.08.14 migration that retires the old two-bullet default compares the text exactly,
    /// so the mangled copy never matched and he was still sending the bullet he had asked to cut.
    /// <para>Only the punctuation this mod's own defaults and players' hand-typed text actually
    /// carry is repaired, by a fixed table — no guessing at encodings, and a string with nothing
    /// mangled in it comes back as the very same instance.</para>
    /// </summary>
    public static class Mojibake
    {
        // Each key is the UTF-8 bytes of the value, read as windows-1252.
        private static readonly KeyValuePair<string, string>[] Pairs =
        {
            new KeyValuePair<string, string>("â€”", "—"), // — em dash
            new KeyValuePair<string, string>("â€“", "–"), // – en dash
            new KeyValuePair<string, string>("â€™", "’"), // ’
            new KeyValuePair<string, string>("â€˜", "‘"), // ‘
            new KeyValuePair<string, string>("â€œ", "“"), // “
            new KeyValuePair<string, string>("â€\u009d", "”"), // ” (0x9D has no 1252 glyph; read as itself)
            new KeyValuePair<string, string>("â€¦", "…"), // …
            new KeyValuePair<string, string>("Â·", "·"),       // ·
            new KeyValuePair<string, string>("Ã—", "×"),       // ×
        };

        /// <summary>The text with any mangled punctuation from the table above put back.</summary>
        public static string Repair(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (text!.IndexOf('â') < 0 && text.IndexOf('Â') < 0 && text.IndexOf('Ã') < 0) return text;
            var fixedText = text;
            foreach (var p in Pairs)
                fixedText = fixedText.Replace(p.Key, p.Value);
            return fixedText;
        }
    }
}
