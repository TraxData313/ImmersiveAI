using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// One standing PRESET — a short wish the player keeps on hand for their own thinking:
    /// "romantic", "ender", "starter". Choosing one drops its words into the writing line, where
    /// they steer the player's own next line (see <see cref="PlayerThought"/>) and are never sent
    /// to anyone. The name is only the menu's label; the text is what the thinking actually reads.
    /// </summary>
    public sealed class ConversationPreset
    {
        public ConversationPreset(string? name, string? text)
        {
            Text = (text ?? string.Empty).Trim();
            var label = (name ?? string.Empty).Trim();
            if (label.Length == 0) label = ConversationPresets.DeriveName(Text);
            Name = label.Length > ConversationPresets.MaxNameLength
                ? label.Substring(0, ConversationPresets.MaxNameLength).TrimEnd()
                : label;
        }

        /// <summary>The menu's label ("romantic") — short, the player's own word.</summary>
        public string Name { get; }

        /// <summary>What they mean to get across, in their own words — the wish itself.</summary>
        public string Text { get; }
    }

    /// <summary>
    /// The player's own presets file (conversation_presets.txt): plain "name = the wish" lines, with
    /// #/// comments kept for the template's explanation. Parsing is deliberately lenient — a line
    /// with no "=" is taken whole as a wish and names itself from its first words — because this is
    /// a file the player edits by hand as often as through the menu.
    /// </summary>
    public static class ConversationPresets
    {
        /// <summary>Long enough for a real word or two, short enough for the menu's button.</summary>
        public const int MaxNameLength = 24;

        /// <summary>A backstop against a runaway file filling the menu; a silent cut, never an error.</summary>
        public const int MaxPresets = 40;

        /// <summary>What a fresh install starts with (Anton's three, 2026.08.10): the opening, the
        /// warm word, and the graceful way out — the three moments a player most often wants help
        /// finding words for.</summary>
        public static IReadOnlyList<ConversationPreset> Defaults { get; } = new List<ConversationPreset>
        {
            new ConversationPreset("starter",
                "I want to open a talk with them — something worth speaking of between us just now, not empty courtesy."),
            new ConversationPreset("romantic",
                "I want to say something romantic to them, something that draws us closer."),
            new ConversationPreset("ender",
                "I want to find a courteous way to say that we should bring this talk to a close."),
        };

        /// <summary>Reads the presets file. Comment lines (#, //) and blanks are skipped; every other
        /// line is "name = wish", or bare wish text that names itself.</summary>
        public static List<ConversationPreset> Parse(string? raw)
        {
            var result = new List<ConversationPreset>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (var line in raw!.Split('\n'))
            {
                if (result.Count >= MaxPresets) break;

                var trimmed = line.TrimEnd('\r').Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;

                string name, text;
                int split = trimmed.IndexOf('=');
                if (split > 0)
                {
                    name = trimmed.Substring(0, split).Trim();
                    text = trimmed.Substring(split + 1).Trim();
                }
                else
                {
                    name = string.Empty;
                    text = trimmed;
                }

                if (text.Length == 0) continue;
                result.Add(new ConversationPreset(name, text));
            }
            return result;
        }

        /// <summary>The file's body as the parser expects it back — one "name = wish" per line.</summary>
        public static string Format(IEnumerable<ConversationPreset>? presets)
        {
            if (presets == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var preset in presets)
            {
                if (preset == null || string.IsNullOrWhiteSpace(preset.Text)) continue;
                sb.AppendLine($"{preset.Name} = {Flatten(preset.Text)}");
            }
            return sb.ToString();
        }

        /// <summary>Adds a new preset, or rewrites the one already wearing that name (the menu's
        /// Save does both with one button). Returns the new list, never mutating the old.</summary>
        public static List<ConversationPreset> Upsert(IEnumerable<ConversationPreset>? presets, string? name, string? text)
        {
            var next = (presets ?? Enumerable.Empty<ConversationPreset>()).ToList();
            var fresh = new ConversationPreset(name, text);
            if (fresh.Text.Length == 0) return next;

            int at = next.FindIndex(i => i != null && Same(i.Name, fresh.Name));
            if (at >= 0) next[at] = fresh;
            else if (next.Count < MaxPresets) next.Add(fresh);
            return next;
        }

        /// <summary>Strikes one out by name. Returns the new list, never mutating the old.</summary>
        public static List<ConversationPreset> Remove(IEnumerable<ConversationPreset>? presets, string? name)
        {
            var next = (presets ?? Enumerable.Empty<ConversationPreset>()).ToList();
            next.RemoveAll(i => i != null && Same(i.Name, (name ?? string.Empty).Trim()));
            return next;
        }

        private static bool Same(string? a, string? b) =>
            string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>A label for a wish written without one: its first few words, so a hand-typed
        /// line still shows up in the menu wearing something readable.</summary>
        public static string DeriveName(string? text)
        {
            var words = (text ?? string.Empty)
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(3)
                .ToList();
            if (words.Count == 0) return "preset";
            var label = string.Join(" ", words).Trim(' ', ',', '.', ';', ':', '—', '-');
            return label.Length > MaxNameLength ? label.Substring(0, MaxNameLength).TrimEnd() : label;
        }

        // The file is line-based, so a wish carrying newlines would split into several on the next
        // read; the in-game editor cannot type one anyway, but a hand-edited file might.
        private static string Flatten(string text) =>
            string.Join(" ", (text ?? string.Empty)
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0));
    }
}
