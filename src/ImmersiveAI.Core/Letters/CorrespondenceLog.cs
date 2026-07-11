using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Letters
{
    /// <summary>
    /// One entry of a letters.txt correspondence log, parsed back into shape for the letter
    /// window. The log is append-only prose written by the Module (see AppendCorrespondenceLog):
    /// letters begin with a "[timestamp] From writes to To (from Place, ~N days on the road):"
    /// header followed by the body, notes are a single bracketed line ("(X read the letter, and
    /// let it lie unanswered.)"). The parser is deliberately forgiving — a line it cannot place
    /// becomes part of the current body rather than an error, so a hand-edited log still reads.
    /// </summary>
    public sealed class CorrespondenceEntry
    {
        public string Stamp { get; set; } = string.Empty;

        /// <summary>Writer and reader as the log names them; empty on notes.</summary>
        public string FromName { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;

        /// <summary>The parenthesised provenance, e.g. "from Sargot, ~2.5 days on the road".</summary>
        public string Detail { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        /// <summary>True for the single-line asides between letters ("read and let lie unanswered").</summary>
        public bool IsNote { get; set; }
    }

    public static class CorrespondenceLog
    {
        /// <summary>Parses a whole letters.txt into entries, oldest first. Null or blank → empty.</summary>
        public static List<CorrespondenceEntry> Parse(string? text)
        {
            var entries = new List<CorrespondenceEntry>();
            if (string.IsNullOrWhiteSpace(text)) return entries;

            CorrespondenceEntry? current = null;
            var body = new System.Text.StringBuilder();

            void CloseCurrent()
            {
                if (current == null) return;
                current.Body = body.ToString().Trim();
                entries.Add(current);
                current = null;
                body.Clear();
            }

            foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine;
                if (TryParseHeader(line, out var entry))
                {
                    CloseCurrent();
                    if (entry.IsNote) entries.Add(entry);   // notes carry no body lines
                    else current = entry;
                    continue;
                }

                if (current != null) body.AppendLine(line);
                // Lines before any header (or after a note) have no home; letting them fall away
                // keeps a truncated or hand-trimmed log readable instead of glued to a wrong entry.
            }
            CloseCurrent();

            return entries;
        }

        // "[timestamp] From writes to To (detail):"  |  "[timestamp] (a note between letters)"
        private static bool TryParseHeader(string line, out CorrespondenceEntry entry)
        {
            entry = new CorrespondenceEntry();
            if (line.Length < 3 || line[0] != '[') return false;

            int close = line.IndexOf("] ", StringComparison.Ordinal);
            if (close <= 0) return false;

            entry.Stamp = line.Substring(1, close - 1).Trim();
            var rest = line.Substring(close + 2).Trim();
            if (rest.Length == 0) return false;

            int writesAt = rest.IndexOf(" writes to ", StringComparison.Ordinal);
            if (writesAt > 0 && rest.EndsWith(":", StringComparison.Ordinal))
            {
                entry.FromName = rest.Substring(0, writesAt).Trim();
                var tail = rest.Substring(writesAt + " writes to ".Length, rest.Length - writesAt - " writes to ".Length - 1).Trim();

                int parenAt = tail.IndexOf(" (", StringComparison.Ordinal);
                if (parenAt > 0 && tail.EndsWith(")", StringComparison.Ordinal))
                {
                    entry.ToName = tail.Substring(0, parenAt).Trim();
                    entry.Detail = tail.Substring(parenAt + 2, tail.Length - parenAt - 3).Trim();
                }
                else
                {
                    entry.ToName = tail;
                }
                return entry.FromName.Length > 0 && entry.ToName.Length > 0;
            }

            // Anything else bracketed on its own line is an aside.
            entry.IsNote = true;
            entry.Body = rest.Trim('(', ')').Trim();
            return entry.Body.Length > 0;
        }
    }
}
