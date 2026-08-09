using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI
{
    /// <summary>
    /// Loads user-editable prompt files from the Bannerlord Configs folder, the same
    /// idea as ChatAI's global_prompt.txt: lines starting with # or // are ignored so
    /// the file can carry instructions to the user alongside the actual prompt text.
    ///
    /// Location (freely editable, no admin rights needed):
    ///   Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\global_prompt.txt
    ///   Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\NPCs\&lt;id&gt;_&lt;FirstName&gt;\custom_instructions.txt
    ///
    /// Per-NPC prompt paths are owned by <see cref="NpcPaths"/>; this class only reads/creates the
    /// file at a path it's handed and strips comment lines.
    /// </summary>
    public static class PromptFiles
    {
        public static string RootDirectory => ModConfig.ConfigDirectory;
        public static string GlobalPromptPath => Path.Combine(RootDirectory, "global_prompt.txt");

        private const string GlobalTemplate =
@"# Immersive AI - Global Prompt: how to shape your whole world
#
# Whatever you write here (except lines starting with # or //, which are ignored)
# enters EVERY character's mind under the words ""Of this world, this I know:"" -
# so write it as knowledge of the world, the way its people would hold it.
# Keep it short - a few plain sentences carry further than a page of rules, and
# too many rules make every soul answer the same.
#
# Things people do with this file (remove the leading '# ' to use one):
#
#   The world is harsh and medieval. People speak plainly and fear their lords.
#
#   This year is one of famine; food and coin weigh on every mind.
#
#   People speak in short sentences, rarely more than two or three at a time.
#
#   People answer in the language spoken to them, whatever it may be.
#
# Each character ALSO has their own file - custom_instructions.txt inside their
# folder under NPCs\ - for things only they should carry. Write THAT file in the
# character's own first person (""I secretly resent the traveler"", ""I stutter
# when I am nervous"") - it enters their mind as their own private truth. This
# file is the world; that file is the person. Changes take effect the next time
# you speak with someone - no restart needed.
";

        /// <summary>Reads the global prompt, creating a commented template on first run. Returns the text with comment lines stripped.</summary>
        public static string LoadGlobalPrompt()
        {
            try
            {
                Directory.CreateDirectory(RootDirectory);
                if (!File.Exists(GlobalPromptPath))
                    File.WriteAllText(GlobalPromptPath, GlobalTemplate);
                return StripComments(File.ReadAllText(GlobalPromptPath));
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Reads the per-NPC prompt file at the given path (owned by <see cref="NpcPaths"/>),
        /// creating a commented template on first run. Returns the text with comment lines stripped.</summary>
        public static string LoadNpcPrompt(string path, string npcName)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(path))
                {
                    var template =
$@"# Immersive AI - Custom instructions for {npcName}
# This text is added only for this character, entering their mind under the
# words ""Of myself, this I hold true:"" - so write it in THEIR own first person,
# as truths they privately carry. Lines starting with # or // are ignored.
#
# Example:
#   I secretly resent the traveler. I never forget an insult.
";
                    File.WriteAllText(path, template);
                }
                return StripComments(File.ReadAllText(path));
            }
            catch
            {
                return string.Empty;
            }
        }

        // ── The in-game prompt editor's read & write ────────────────────────────────
        // The windows' "Their prompt" / "World prompt" buttons edit IN PLACE (Anton's ask,
        // 2026.08.07 — the first cut opened Notepad and he wanted to stay in the game). The editor
        // shows the EFFECTIVE prompt as one flowing line (the Gauntlet input line cannot hold
        // newlines — same constraint the letter composer lives with); the file's # comment lines
        // are kept aside on save, so hand-written notes and the template survive. No restart is
        /// ever needed: prompts are re-read every time a context is built, never cached.

        /// <summary>The NPC's effective prompt as one flowing line for the in-game editor (file and
        /// template created first if missing).</summary>
        public static string LoadNpcPromptForEdit(string path, string npcName)
            => Flatten(LoadNpcPrompt(path, npcName));

        /// <summary>The global prompt as one flowing line for the in-game editor.</summary>
        public static string LoadGlobalPromptForEdit() => Flatten(LoadGlobalPrompt());

        /// <summary>Writes an in-game edit of this NPC's prompt back to their file, keeping its
        /// comment lines at the top. Takes hold on the very next reply.</summary>
        public static void SaveNpcPromptFromGame(string path, string npcName, string text)
        {
            LoadNpcPrompt(path, npcName); // ensures the file and its template exist
            WriteKeepingComments(path, text);
        }

        /// <summary>Writes an in-game edit of the global prompt back, keeping its comment lines.</summary>
        public static void SaveGlobalPromptFromGame(string text)
        {
            LoadGlobalPrompt();
            WriteKeepingComments(GlobalPromptPath, text);
        }

        // ── The player's own presets ("Let me think…") ─────────────────────────────────
        // Not a prompt anyone reads: the standing wishes the think-menu offers ("romantic",
        // "ender"), which only ever steer the PLAYER's own next line. Same #-comment convention as
        // every other file here, so the template can explain itself and survive editing in game.

        public static string ConversationPresetsPath => Path.Combine(RootDirectory, "conversation_presets.txt");

        // The name this file wore for the few hours between being built and being renamed
        // (2026.08.10). Kept only so an early file is carried over rather than left orphaned.
        private static string LegacyPresetsPath => Path.Combine(RootDirectory, "think_intents.txt");

        private const string PresetsTemplate =
@"# Immersive AI - your conversation presets
#
# ""Think"" (Shift+Enter) in the chat and letter windows has your own character work
# out what to say next. It reads everything the one before you reads - who you both
# are, all that has passed between you, where you stand, the whole conversation - and
# writes their next line into your writing box, for you to keep, change, or throw away.
#
# The lines below are the standing presets its little menu offers. Choosing one drops
# its words into your writing box: they are NEVER sent to anyone, they only steer your
# own thinking. Then press Shift+Enter and the words come back.
#
# One per line, in your own words:
#
#     name = what I mean to get across
#
# Edit them here, or from the menu itself without leaving the game.

";

        /// <summary>The player's standing presets, creating the commented template on first run.</summary>
        public static List<ConversationPreset> LoadConversationPresets()
        {
            try
            {
                Directory.CreateDirectory(RootDirectory);
                if (!File.Exists(ConversationPresetsPath) && File.Exists(LegacyPresetsPath))
                    File.Move(LegacyPresetsPath, ConversationPresetsPath);
                if (!File.Exists(ConversationPresetsPath))
                    File.WriteAllText(ConversationPresetsPath,
                        PresetsTemplate + ConversationPresets.Format(ConversationPresets.Defaults));
                // An emptied file stays empty on purpose: the defaults are a starting gift, not a
                // floor — a player who strikes them all out meant to.
                return ConversationPresets.Parse(File.ReadAllText(ConversationPresetsPath));
            }
            catch { return ConversationPresets.Defaults.ToList(); }
        }

        /// <summary>Writes the presets back (the in-game menu's add / rewrite / strike-out), keeping
        /// the file's own comment lines at the top.</summary>
        public static void SaveConversationPresets(IEnumerable<ConversationPreset> presets)
        {
            try
            {
                LoadConversationPresets();   // ensures the file and its template exist
                WriteKeepingComments(ConversationPresetsPath, ConversationPresets.Format(presets));
            }
            catch { /* the menu still holds them for this session */ }
        }

        // ── The director's spark (the generated starting persona) ──────────────────────
        // The spark lands in the SAME per-NPC file the player edits; a "# spark:" comment line
        // marks that the director has already passed (or was declined), so a soul is sparked at
        // most once. Deleting the whole file — or the DevMode reroll lever — invites him back.

        public const string SparkStampPrefix = "# spark:";

        /// <summary>True when this soul still awaits the director: the file is missing, or it
        /// carries no authored content AND no spark stamp. Any hand-written prompt counts as the
        /// player having already shaped them. Unreadable files answer false — never generate
        /// against a file that cannot be checked.</summary>
        public static bool NeedsPersonaSpark(string path)
        {
            try
            {
                if (!File.Exists(path)) return true;
                var raw = File.ReadAllText(path);
                if (StripComments(raw).Length > 0) return false;
                return !HasSparkStamp(raw);
            }
            catch { return false; }
        }

        private static bool HasSparkStamp(string raw) =>
            (raw ?? string.Empty).Split('\n')
                .Any(l => l.TrimStart().StartsWith(SparkStampPrefix, StringComparison.OrdinalIgnoreCase));

        /// <summary>Writes the director's spark into the NPC's prompt file: the template's comments
        /// stay at the top, a stamp comment records the moment, and the spark itself becomes the
        /// file's body (NeedsPersonaSpark guaranteed it was empty). Takes hold on the next reply.</summary>
        public static void WritePersonaSpark(string path, string npcName, string spark, string when)
        {
            LoadNpcPrompt(path, npcName); // ensures the file and its template exist
            AppendStampedBody(path,
                $"{SparkStampPrefix} written once by the unseen director at first meeting ({when}). Yours to edit or erase.",
                spark);
        }

        /// <summary>Records that the player declined the director for this soul, so they are never
        /// asked again (the stamp is a comment; the file stays otherwise empty and editable).</summary>
        public static void MarkPersonaSparkDeclined(string path, string npcName)
        {
            LoadNpcPrompt(path, npcName);
            AppendStampedBody(path,
                $"{SparkStampPrefix} declined by your hand — the director will not ask again. Delete this line to be asked anew.",
                null);
        }

        /// <summary>The DevMode reroll: strips every spark stamp AND the file's body (the template
        /// comments survive), so the very next interaction invites the director afresh.</summary>
        public static void ClearPersonaSpark(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var kept = File.ReadAllLines(path)
                    .Where(l =>
                    {
                        var t = l.TrimStart();
                        return (t.StartsWith("#") || t.StartsWith("//"))
                            && !t.StartsWith(SparkStampPrefix, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(l => l.TrimEnd('\r'));
                File.WriteAllText(path, string.Join(Environment.NewLine, kept) + Environment.NewLine);
            }
            catch { /* the reroll is best-effort */ }
        }

        // Rewrites the file as: its existing comment lines, the new stamp comment, then the body.
        private static void AppendStampedBody(string path, string stampComment, string? body)
        {
            var comments = new List<string>();
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var t = line.TrimStart();
                    if (t.StartsWith("#") || t.StartsWith("//")) comments.Add(line.TrimEnd('\r'));
                }
            }
            catch { /* a fresh file simply has no comments to keep */ }
            comments.Add(stampComment);

            var sb = new System.Text.StringBuilder();
            foreach (var c in comments) sb.AppendLine(c);
            var text = (body ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(text);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string Flatten(string text)
            => string.Join(" ", (text ?? string.Empty)
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0));

        // The file keeps its own voice: every #/'//' line it already carries (template or
        // hand-written) gathers at the top, then the edited prompt below. Interleaved comments
        // lose their exact position — a fair price for never losing them at all.
        private static void WriteKeepingComments(string path, string text)
        {
            var comments = new List<string>();
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var t = line.TrimStart();
                    if (t.StartsWith("#") || t.StartsWith("//")) comments.Add(line.TrimEnd('\r'));
                }
            }
            catch { /* a fresh file simply has no comments to keep */ }

            var sb = new System.Text.StringBuilder();
            foreach (var c in comments) sb.AppendLine(c);
            if (comments.Count > 0) sb.AppendLine();
            var body = (text ?? string.Empty).Trim();
            if (body.Length > 0) sb.AppendLine(body);
            File.WriteAllText(path, sb.ToString());
        }

        private static string StripComments(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var kept = raw
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l =>
                {
                    var t = l.TrimStart();
                    return !t.StartsWith("#") && !t.StartsWith("//");
                });
            return string.Join("\n", kept).Trim();
        }
    }
}
