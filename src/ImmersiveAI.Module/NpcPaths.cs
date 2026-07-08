using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// Owns the on-disk layout of per-NPC runtime files. Everything an NPC accumulates lives in
    /// its own folder so it is easy to find and inspect by hand:
    ///
    ///   Configs\ImmersiveAI\NPCs\&lt;stringId&gt;_&lt;FirstName&gt;\
    ///       memories.json           - the persisted NpcMemory (was memory\&lt;stringId&gt;.json)
    ///       custom_instructions.txt - per-NPC prompt (was npcs\&lt;stringId&gt;.txt)
    ///       &lt;future per-NPC files go here too&gt;
    ///
    /// The folder name embeds the NPC's first name for readability, but identity is the stringId:
    /// the folder path is derived deterministically from (stringId, firstName), and both are always
    /// available at every call site (we hold the live Hero). Old flat files are migrated in place the
    /// first time an NPC is touched, so existing memories are never wiped.
    ///
    /// HOUSEKEEPING (for future changes): if you rename a file or the layout here, also update
    /// <see cref="RuntimeReadmeText"/> below, the "User-editable runtime files" sections in
    /// README.md / CLAUDE.md / AGENTS.md, and the migration in <see cref="EnsureMigrated"/>.
    /// </summary>
    public static class NpcPaths
    {
        public const string MemoryFileName = "memories.json";
        public const string CustomInstructionsFileName = "custom_instructions.txt";

        /// <summary>Root that holds one subfolder per NPC.</summary>
        public static string NpcsRoot => Path.Combine(ModConfig.ConfigDirectory, "NPCs");

        // Legacy flat layout (pre-restructure), kept only so it can be migrated forward.
        private static string LegacyMemoryDir => Path.Combine(ModConfig.ConfigDirectory, "memory");
        private static string LegacyNpcPromptDir => Path.Combine(ModConfig.ConfigDirectory, "npcs");

        public static string NpcFolder(Hero npc) => NpcFolder(npc.StringId, FirstNameOf(npc));

        public static string NpcFolder(string npcId, string firstName)
        {
            var folderName = Sanitize(npcId);
            var fn = Sanitize(firstName);
            if (fn.Length > 0 && fn != "_") folderName += "_" + fn;
            return Path.Combine(NpcsRoot, folderName);
        }

        public static string MemoryFile(Hero npc) => Path.Combine(NpcFolder(npc), MemoryFileName);

        public static string CustomInstructionsFile(Hero npc) => Path.Combine(NpcFolder(npc), CustomInstructionsFileName);

        /// <summary>The NPC's first name only (second names excluded), for the folder label. Falls back
        /// to the first token of the full name, then to the raw id.</summary>
        public static string FirstNameOf(Hero npc)
        {
            var first = npc.FirstName?.ToString() ?? string.Empty;
            if (first.Trim().Length > 0) return first.Trim();

            var full = npc.Name?.ToString() ?? string.Empty;
            var token = full.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
        }

        /// <summary>
        /// Ensures this NPC's folder holds its files, migrating the old flat-layout files forward once.
        /// Idempotent and cheap after the first run (skips as soon as the new files exist). Best-effort:
        /// any IO failure is swallowed so a chat is never blocked by housekeeping.
        /// </summary>
        /// <summary>
        /// Eagerly migrates ALL existing flat-layout files into per-NPC folders, using the NpcName
        /// stored inside each memories JSON to derive the folder's first-name suffix (no live Hero
        /// needed). Called once on save-load so the folder reorganizes immediately instead of only
        /// when the player happens to talk to each NPC. Best-effort and idempotent.
        /// </summary>
        public static void MigrateAll()
        {
            try
            {
                NormalizeNpcsRootCasing();

                if (Directory.Exists(LegacyMemoryDir))
                {
                    foreach (var jsonPath in Directory.GetFiles(LegacyMemoryDir, "*.json"))
                    {
                        var id = Path.GetFileNameWithoutExtension(jsonPath);
                        var firstName = FirstNameFromMemoryJson(jsonPath);
                        var folder = NpcFolder(id, firstName);

                        MigrateFile(jsonPath, Path.Combine(folder, MemoryFileName), folder);
                        MigrateFile(Path.Combine(LegacyNpcPromptDir, id + ".txt"),
                                    Path.Combine(folder, CustomInstructionsFileName), folder);
                    }
                }

                EnsureRuntimeReadme();
            }
            catch { /* best-effort; per-NPC lazy migration in EnsureMigrated remains as a fallback */ }
        }

        // Reads only the NpcName out of a memories JSON and returns its first token for the folder label.
        private static string FirstNameFromMemoryJson(string jsonPath)
        {
            try
            {
                var name = (string?)JObject.Parse(File.ReadAllText(jsonPath))["NpcName"] ?? string.Empty;
                var token = name.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
            }
            catch { return string.Empty; }
        }

        public static void EnsureMigrated(Hero npc)
        {
            try
            {
                NormalizeNpcsRootCasing();

                var folder = NpcFolder(npc);
                var id = Sanitize(npc.StringId);

                MigrateFile(Path.Combine(LegacyMemoryDir, id + ".json"), Path.Combine(folder, MemoryFileName), folder);
                MigrateFile(Path.Combine(LegacyNpcPromptDir, id + ".txt"), Path.Combine(folder, CustomInstructionsFileName), folder);

                EnsureRuntimeReadme();
            }
            catch { /* housekeeping is best-effort; never block a conversation */ }
        }

        // On Windows the filesystem is case-insensitive, so the old lowercase "npcs" prompt folder is
        // the same physical directory as our "NPCs" root — CreateDirectory("NPCs") won't fix its casing.
        // Rename it to the desired casing via a temp step (a direct case-only Move is a no-op on Windows).
        private static void NormalizeNpcsRootCasing()
        {
            try
            {
                var parent = ModConfig.ConfigDirectory;
                if (!Directory.Exists(parent)) return;

                var existingName = Directory.GetDirectories(parent)
                    .Select(Path.GetFileName)
                    .FirstOrDefault(n => string.Equals(n, "NPCs", StringComparison.OrdinalIgnoreCase));

                if (existingName == null || existingName == "NPCs") return;

                var from = Path.Combine(parent, existingName);
                var temp = Path.Combine(parent, "NPCs__casing_" + Guid.NewGuid().ToString("N"));
                Directory.Move(from, temp);
                Directory.Move(temp, Path.Combine(parent, "NPCs"));
            }
            catch { /* best-effort; layout still works under either casing */ }
        }

        // Moves an old file to its new home only if the new one doesn't already exist. Copy-then-delete
        // so a failure can't lose the source; the copy is authoritative once written.
        private static void MigrateFile(string oldPath, string newPath, string folder)
        {
            if (File.Exists(newPath) || !File.Exists(oldPath)) return;
            Directory.CreateDirectory(folder);
            File.Copy(oldPath, newPath, overwrite: false);
            try { File.Delete(oldPath); } catch { /* leave orphan; new file is authoritative */ }
        }

        /// <summary>Drops a short human-readable README in the NPCs root the first time, so the user
        /// (who edits these files by hand) understands the layout.</summary>
        public static void EnsureRuntimeReadme()
        {
            try
            {
                Directory.CreateDirectory(NpcsRoot);
                var readmePath = Path.Combine(NpcsRoot, "_README.txt");
                if (!File.Exists(readmePath))
                    File.WriteAllText(readmePath, RuntimeReadmeText);
            }
            catch { /* best-effort */ }
        }

        private const string RuntimeReadmeText =
@"Immersive AI - per-NPC files
============================

Each NPC has one folder here, named <stringId>_<FirstName> (e.g. lord_7_13_1_Gunjadrid).
Inside each folder:

  memories.json           - everything the NPC remembers of you (recent turns, rolling
                            summary, known facts). Safe to read; edit only if you know the
                            JSON shape. Delete it to make that NPC forget you completely.
  custom_instructions.txt - private instructions for THIS NPC. Lines starting with # or //
                            are ignored. (World-wide instructions go in ..\global_prompt.txt.)

You can delete an NPC's whole folder to reset that character.
";

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "_";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var c in name) sb.Append(invalid.Contains(c) ? '_' : c);
            var cleaned = sb.ToString().Trim();
            return cleaned.Length == 0 ? "_" : cleaned;
        }
    }
}
