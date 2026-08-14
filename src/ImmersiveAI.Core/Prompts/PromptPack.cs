using System;
using System.Collections.Generic;
using System.Text;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// THE PROMPT PACK (2026.08.14, Anton's ask) — every word an NPC is ever given, in one file the
    /// player owns: "so that I don't have to ask you for change a single word."
    ///
    /// The shape is deliberately the dullest thing that could work, because it is edited by hand at
    /// two in the morning:
    ///
    /// <code>
    /// # what a soul is told about speaking plainly
    /// sheet.guidance.plain_speech = "I speak as people speak, not as books are written."
    ///
    /// # the memory contract - {name} is filled in with their own name
    /// memory.contract.summary = """
    /// What I remember of {name}:
    /// ...
    /// """
    /// </code>
    ///
    /// Four laws, each of them learned from something that has already bitten this mod:
    ///
    /// • A MISSING OR BROKEN KEY IS NEVER FATAL. The compiled-in default stands, the game speaks,
    ///   and the trouble is written to the log. A prompt file is the last place that should be able
    ///   to brick someone's campaign.
    /// • EDITS SURVIVE UPDATES. Reading a file never rewrites what the player wrote; new keys are
    ///   only ever APPENDED, and a key the player has touched is left exactly as they left it.
    /// • NOT EVERY STRING BELONGS HERE. The marks — the fragments matched against memories recorded
    ///   years of play ago, the labels parsed back out of answers — stay in code, and the file says
    ///   so where they would otherwise be missed. Editing those would not change a prompt; it would
    ///   quietly orphan an old save.
    /// • ORDER IS SOMETIMES IDENTITY. A few lists are addressed by INDEX (speech styles, the humors,
    ///   the night images): rewording an entry is free, moving one re-voices somebody. Those keys
    ///   carry the warning in their own comment.
    /// </summary>
    public sealed class PromptPack
    {
        private readonly Dictionary<string, string> _values;

        /// <summary>Keys that were in the file but are not known to this build — kept whole so that
        /// loading an old file under a new version, or a new file under an old one, never silently
        /// throws the player's writing away.</summary>
        public IReadOnlyCollection<string> UnknownKeys { get; }

        /// <summary>Lines that could not be read at all. Reported once to the log, never thrown:
        /// one fat-fingered line must not cost the other five hundred.</summary>
        public IReadOnlyList<string> Complaints { get; }

        private PromptPack(Dictionary<string, string> values, List<string> unknown, List<string> complaints)
        {
            _values = values;
            UnknownKeys = unknown;
            Complaints = complaints;
        }

        /// <summary>An empty pack — every key answers with its compiled-in default.</summary>
        public static PromptPack Empty =>
            new PromptPack(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                           new List<string>(), new List<string>());

        /// <summary>The text for a key: what the player wrote if they wrote anything, otherwise the
        /// default handed in. Never null — an unknown key answers with the fallback, and a key the
        /// player has deliberately emptied answers empty, which is a legitimate way to switch a
        /// piece of guidance off.</summary>
        public string Get(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
            return _values.TryGetValue(key, out var text) ? text : (fallback ?? string.Empty);
        }

        /// <summary>Whether the player has written this key at all (as opposed to inheriting it).</summary>
        public bool Has(string key) => !string.IsNullOrEmpty(key) && _values.ContainsKey(key);

        /// <summary>Reads a pack from the file's text. <paramref name="known"/> is the set of keys
        /// this build understands; anything else is kept aside in <see cref="UnknownKeys"/> rather
        /// than dropped.</summary>
        public static PromptPack Parse(string? text, ICollection<string>? known = null)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var unknown = new List<string>();
            var complaints = new List<string>();
            if (string.IsNullOrEmpty(text))
                return new PromptPack(values, unknown, complaints);

            var lines = text!.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                // Blank, and the two comment conventions this mod has always used.
                if (trimmed.Length == 0) continue;
                if (trimmed[0] == '#' || trimmed.StartsWith("//", StringComparison.Ordinal)) continue;

                int eq = trimmed.IndexOf('=');
                if (eq <= 0)
                {
                    complaints.Add($"line {i + 1}: expected 'name = value'");
                    continue;
                }

                var key = trimmed.Substring(0, eq).Trim();
                var raw = trimmed.Substring(eq + 1).Trim();
                if (key.Length == 0)
                {
                    complaints.Add($"line {i + 1}: a value with no name");
                    continue;
                }

                string value;
                if (raw.StartsWith("\"\"\"", StringComparison.Ordinal))
                {
                    // A block: everything up to a line that is just the closing fence. An unclosed
                    // block takes the rest of the file rather than failing — the player is midway
                    // through typing, and losing the tail would be the worse answer.
                    var body = new StringBuilder();
                    var firstLineRest = raw.Substring(3);
                    bool closedOnSameLine = firstLineRest.EndsWith("\"\"\"", StringComparison.Ordinal)
                                            && firstLineRest.Length >= 3;
                    if (closedOnSameLine)
                    {
                        value = firstLineRest.Substring(0, firstLineRest.Length - 3);
                    }
                    else
                    {
                        if (firstLineRest.Trim().Length > 0) body.Append(firstLineRest.TrimStart());
                        int j = i + 1;
                        bool closed = false;
                        for (; j < lines.Length; j++)
                        {
                            if (lines[j].Trim() == "\"\"\"") { closed = true; break; }
                            if (body.Length > 0) body.Append('\n');
                            body.Append(lines[j]);
                        }
                        if (!closed) complaints.Add($"line {i + 1}: '{key}' was never closed with \"\"\"");
                        i = Math.Min(j, lines.Length - 1);
                        value = body.ToString();
                    }
                    value = TrimBlock(value);
                }
                else
                {
                    value = Unquote(raw);
                }

                if (known != null && !known.Contains(key)) unknown.Add(key);
                values[key] = value;   // a repeated key: the last one written wins
            }

            return new PromptPack(values, unknown, complaints);
        }

        // A one-line value. Quotes are the convention but not a requirement: someone who types the
        // words bare means the words.
        private static string Unquote(string raw)
        {
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                raw = raw.Substring(1, raw.Length - 2);
            return raw
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"");
        }

        // A block keeps its inner shape but not the blank lines the fences leave behind.
        private static string TrimBlock(string value)
        {
            value = value.Replace("\r\n", "\n");
            while (value.StartsWith("\n", StringComparison.Ordinal)) value = value.Substring(1);
            while (value.EndsWith("\n", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 1);
            return value;
        }

        /// <summary>Writes one entry the way the file wants it: its explanation as comments, then
        /// the value — a block whenever the text runs to more than one line, because a wall of
        /// backslash-n is not something anyone should be asked to edit.</summary>
        public static string RenderEntry(string key, string text, string? comment = null,
                                         IEnumerable<string>? slots = null, string? warning = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(comment))
                foreach (var line in comment!.Replace("\r\n", "\n").Split('\n'))
                    sb.Append("# ").Append(line).Append('\n');

            if (slots != null)
            {
                var list = new List<string>(slots);
                if (list.Count > 0)
                    sb.Append("# fills in: ").Append(string.Join(", ", list.ConvertAll(s => "{" + s + "}"))).Append('\n');
            }

            if (!string.IsNullOrWhiteSpace(warning))
                sb.Append("# ! ").Append(warning).Append('\n');

            var body = (text ?? string.Empty).Replace("\r\n", "\n");
            if (body.IndexOf('\n') >= 0)
                sb.Append(key).Append(" = \"\"\"\n").Append(body).Append("\n\"\"\"\n");
            else
                sb.Append(key).Append(" = \"").Append(body.Replace("\"", "\\\"")).Append("\"\n");

            return sb.ToString();
        }
    }
}
