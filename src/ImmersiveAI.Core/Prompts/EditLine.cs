using System;
using System.Linq;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// The in-game editor's one line, and the paragraphs it has to carry. A Gauntlet input cannot
    /// hold a newline (the letter composer and the prompt editor both live with that), which was
    /// harmless for a prompt of a few sentences and is not for a deep memory: several paragraphs,
    /// rewritten by the NPC in paragraphs, and flattened into one slab by a player who only meant
    /// to fix a name. So a paragraph break travels through the line as a visible mark, and comes
    /// back out as a break on save. A single line break inside a paragraph becomes a space.
    /// </summary>
    public static class EditLine
    {
        /// <summary>Where one paragraph ends and the next begins, while the text is one line.</summary>
        public const string ParagraphMark = "¶";

        private static readonly string[] BlankLine = { "\n\n" };

        /// <summary>Text as the editor shows it: each paragraph on one line, " ¶ " between them.</summary>
        public static string ToLine(string? text)
        {
            var paragraphs = Normalize(text)
                .Split(BlankLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => string.Join(" ", p.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)))
                .Where(p => p.Length > 0);
            return string.Join(" " + ParagraphMark + " ", paragraphs);
        }

        /// <summary>The editor's line back as text: a paragraph at every mark, blank pieces dropped.</summary>
        public static string FromLine(string? line)
        {
            var paragraphs = (line ?? string.Empty)
                .Replace("\r", " ").Replace("\n", " ")
                .Split(new[] { ParagraphMark }, StringSplitOptions.None)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);
            return string.Join("\n\n", paragraphs);
        }

        // Line endings unified, and any run of blank lines (or whitespace-only lines) read as one break.
        private static string Normalize(string? text)
        {
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
                .Select(l => l.Trim().Length == 0 ? string.Empty : l);
            var joined = string.Join("\n", lines);
            while (joined.Contains("\n\n\n")) joined = joined.Replace("\n\n\n", "\n\n");
            return joined.Trim();
        }
    }
}
