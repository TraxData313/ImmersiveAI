using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// Owns the on-disk layout of per-NPC runtime files. Memories are scoped per CAMPAIGN
    /// (one playthrough = one world), because Hero string ids repeat across campaigns —
    /// lord_7_13_1 is "the same" Gunjadrid in every new game, and without scoping she would
    /// greet a fresh playthrough with memories of a world that never happened there:
    ///
    ///   Configs\ImmersiveAI\NPCs\campaign_&lt;campaignId&gt;\&lt;stringId&gt;_&lt;FirstName&gt;\
    ///       memories.json           - the persisted NpcMemory (was memory\&lt;stringId&gt;.json)
    ///       custom_instructions.txt - per-NPC prompt (was npcs\&lt;stringId&gt;.txt)
    ///       &lt;future per-NPC files go here too&gt;
    ///
    /// The campaign id is minted by ImmersiveChatBehavior and persisted INSIDE the save via
    /// SyncData, so every save of one campaign reopens the same folder. (The game's own
    /// Campaign.UniqueGameId is useless here — it changes on every save.) Saves from before
    /// this scoping carry no id and all resolve to the fixed <see cref="LegacyCampaignId"/>,
    /// which is exactly the behavior they always had (one shared pool) and guarantees the
    /// adoption move can never orphan memories, even if the player loads but never saves.
    ///
    /// The NPC folder name embeds the first name for readability, but identity is the stringId:
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
        public const string SituationFileName = "current_situation_info.txt";
        public const string SelfFileName = "self.txt";

        public const string CampaignFolderPrefix = "campaign_";
        public const string CampaignLabelFileName = "_campaign.txt";

        /// <summary>The fixed id every pre-scoping save resolves to (they always shared one pool).</summary>
        public const string LegacyCampaignId = "legacy";

        /// <summary>The campaign whose world is on stage. Set by ImmersiveChatBehavior from the id
        /// persisted in the save (or minted for it) before any NPC file is touched. Empty only
        /// outside a campaign; then paths fall back to the unscoped root, which never happens
        /// during actual play.</summary>
        public static string ActiveCampaignId { get; set; } = string.Empty;

        /// <summary>Umbrella root that holds one campaign_&lt;id&gt; subfolder per playthrough.</summary>
        public static string NpcsRoot => Path.Combine(ModConfig.ConfigDirectory, "NPCs");

        /// <summary>Root for the active campaign's NPC folders.</summary>
        public static string CampaignRoot => string.IsNullOrEmpty(ActiveCampaignId)
            ? NpcsRoot
            : Path.Combine(NpcsRoot, CampaignFolderPrefix + ActiveCampaignId);

        /// <summary>A fresh campaign id: short random token + the player's first name for
        /// human-readable folder names (identity is the whole string, name included).</summary>
        public static string MintCampaignId(string playerFirstName)
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            var fn = Sanitize(playerFirstName);
            return (fn.Length > 0 && fn != "_") ? id + "_" + fn : id;
        }

        // Legacy flat layout (pre-restructure), kept only so it can be migrated forward.
        private static string LegacyMemoryDir => Path.Combine(ModConfig.ConfigDirectory, "memory");
        private static string LegacyNpcPromptDir => Path.Combine(ModConfig.ConfigDirectory, "npcs");

        public static string NpcFolder(Hero npc) => NpcFolder(npc.StringId, FirstNameOf(npc));

        public static string NpcFolder(string npcId, string firstName)
        {
            var folderName = Sanitize(npcId);
            var fn = Sanitize(firstName);
            if (fn.Length > 0 && fn != "_") folderName += "_" + fn;
            return Path.Combine(CampaignRoot, folderName);
        }

        public static string MemoryFile(Hero npc) => Path.Combine(NpcFolder(npc), MemoryFileName);

        public static string CustomInstructionsFile(Hero npc) => Path.Combine(NpcFolder(npc), CustomInstructionsFileName);

        public static string SituationFile(Hero npc) => Path.Combine(NpcFolder(npc), SituationFileName);

        public static string SelfFile(Hero npc) => Path.Combine(NpcFolder(npc), SelfFileName);

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
        /// Moves any pre-campaign-scoping NPC folders (directly under NPCs\, no campaign_ prefix)
        /// into the active campaign's folder. Only ever called when the active campaign is the
        /// legacy one — those folders were the shared pool of every pre-scoping save, which is
        /// exactly what campaign_legacy is. Per-folder best-effort and idempotent: a folder that
        /// fails to move (open handle, etc.) is left in place for the next load to retry.
        /// </summary>
        public static void AdoptLegacyIntoActiveCampaign()
        {
            try
            {
                if (string.IsNullOrEmpty(ActiveCampaignId) || !Directory.Exists(NpcsRoot)) return;

                foreach (var dir in Directory.GetDirectories(NpcsRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name)
                        || name.StartsWith(CampaignFolderPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        Directory.CreateDirectory(CampaignRoot);
                        var dest = Path.Combine(CampaignRoot, name);
                        if (!Directory.Exists(dest)) Directory.Move(dir, dest);
                    }
                    catch { /* leave it; the next load retries and lazy EnsureMigrated still finds it */ }
                }
            }
            catch { /* best-effort housekeeping */ }
        }

        /// <summary>Writes a small human-readable label into the active campaign's folder so the
        /// user browsing NPCs\ can tell which playthrough is which. Rewritten every session, so
        /// "last played" stays fresh.</summary>
        public static void WriteCampaignLabel(string characterName, string clanName, string gameDate)
        {
            try
            {
                if (string.IsNullOrEmpty(ActiveCampaignId)) return;
                Directory.CreateDirectory(CampaignRoot);

                var text =
                    "This folder holds the NPC memories of ONE campaign (one playthrough)." + Environment.NewLine +
                    "Character:   " + characterName + Environment.NewLine +
                    "Clan:        " + clanName + Environment.NewLine +
                    "Last played: " + DateTime.Now.ToString("yyyy.MM.dd HH:mm") +
                    " (in-game: " + gameDate + ")" + Environment.NewLine;
                File.WriteAllText(Path.Combine(CampaignRoot, CampaignLabelFileName), text);
            }
            catch { /* best-effort */ }
        }

        /// <summary>
        /// Eagerly migrates ALL existing flat-layout files into per-NPC folders, using the NpcName
        /// stored inside each memories JSON to derive the folder's first-name suffix (no live Hero
        /// needed). Called once on save-load (after the campaign id is resolved, so folders land
        /// under the right campaign) so the Configs folder reorganizes immediately instead of only
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

        /// <summary>Drops a short human-readable README in the NPCs root, so the user (who edits
        /// these files by hand) understands the layout. Rewritten whenever the text here changes,
        /// since the file is auto-authored, not the user's.</summary>
        public static void EnsureRuntimeReadme()
        {
            try
            {
                Directory.CreateDirectory(NpcsRoot);
                var readmePath = Path.Combine(NpcsRoot, "_README.txt");
                if (!File.Exists(readmePath) || File.ReadAllText(readmePath) != RuntimeReadmeText)
                    File.WriteAllText(readmePath, RuntimeReadmeText);
            }
            catch { /* best-effort */ }
        }

        private const string RuntimeReadmeText =
@"Immersive AI - per-NPC files
============================

Each CAMPAIGN (one playthrough) has one folder here, named campaign_<id> — the id is
stored inside that campaign's save files, so the same world always reopens the same
folder and two playthroughs never share memories (the 'same' lord in a new game is a
stranger again). campaign_legacy holds everything from before this scoping; saves made
back then all open it. A _campaign.txt inside names the character it belongs to.

Within a campaign folder, each NPC has one folder, named <stringId>_<FirstName>
(e.g. lord_7_13_1_Gunjadrid). Inside each NPC folder:

  memories.json           - everything the NPC remembers of you (recent turns, rolling
                            summary, known facts). Safe to read; edit only if you know the
                            JSON shape. Delete it to make that NPC forget you completely.
  custom_instructions.txt - private instructions for THIS NPC. Lines starting with # or //
                            are ignored. (World-wide instructions go in ..\global_prompt.txt.)
  current_situation_info.txt - the environmental facts (when/where/who) captured the last time
                            you opened a chat with this NPC. Regenerated on every chat; read-only
                            snapshot, edits are overwritten. This is exactly what the NPC 'sees'
                            as the Current situation in her prompt.
  self.txt                - the NPC's OWN evolving sense of who they are, written by them (in their
                            own first-person voice) when they reflect - not by you. It grows over
                            time and is folded into their prompt as 'Who you have become'. Safe to
                            read; you may edit it, but the next reflection may rewrite it.

You can delete an NPC's whole folder to reset that character, or delete a whole
campaign_<id> folder to reset every memory of a playthrough you no longer keep.
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
