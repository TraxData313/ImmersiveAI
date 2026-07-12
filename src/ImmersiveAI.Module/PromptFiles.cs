using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
# is added to EVERY character's mind, woven into how they see their world and speak.
# Keep it short - a few plain sentences carry further than a page of rules, and
# too many rules make every soul answer the same.
#
# Things people do with this file (remove the leading '# ' to use one):
#
#   The world is harsh and medieval. People speak plainly and fear their lords.
#
#   Everyone is aware the year is one of famine; food and coin weigh on every mind.
#
#   People speak in short sentences, rarely more than two or three at a time.
#
#   Answer in the language the traveler speaks to you, whatever it may be.
#
# Each character ALSO has their own file - custom_instructions.txt inside their
# folder under NPCs\ - for things only they should carry (""You secretly resent
# the player"", ""You stutter when nervous""). This file is the world; that file
# is the person. Changes take effect the next time you speak with someone -
# no restart needed.
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
# This text is added only for this character. Lines starting with # or // are ignored.
#
# Example:
#   You secretly resent the player. You never forget an insult.
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
