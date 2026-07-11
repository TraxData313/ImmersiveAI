using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI
{
    /// <summary>
    /// Save-scoped memory: photographs a campaign's whole memory folder each time the game is saved,
    /// and restores that photograph when the save is loaded — so reloading to before an NPC's angry
    /// moment truly un-remembers it, the same way the game already reverts the relation number that
    /// lives inside the save file. This closes the "memories from the future" divergence (the mod's
    /// files live OUTSIDE the .sav, so without this they never rewind with a reload).
    ///
    /// How the save and the photograph are tied: a fresh random token is minted into each save via
    /// SyncData; <see cref="Snapshot"/> copies the live folder into _snapshots\&lt;token&gt;\ right after
    /// the save completes (when the save NAME is finally known, used only to prune a slot's previous
    /// photograph so overwriting a save replaces it), and on load <see cref="Restore"/> is handed the
    /// token read back out of the save. No load-time save name is needed, which the engine does not
    /// hand us cleanly anyway.
    ///
    /// Everything is best-effort and fail-safe: a missing or empty snapshot restores NOTHING (it never
    /// wipes live memory on a bad token), and any IO failure is swallowed so a save or load is never
    /// broken by housekeeping. Snapshots live under _snapshots\ inside the campaign folder; the leading
    /// underscore keeps them clear of the &lt;stringId&gt;_&lt;name&gt; NPC folders (and every NPC-folder
    /// scan gates on a memories.json / letters.txt directly inside, which _snapshots\ never has).
    /// </summary>
    public static class MemorySnapshotStore
    {
        public const string SnapshotsFolderName = "_snapshots";
        private const string IndexFileName = "_index.json";

        /// <summary>Photographs the campaign folder into _snapshots\&lt;token&gt;\, replacing any prior
        /// photograph taken for the same save slot (so an overwrite reuses the disk) and pruning down to
        /// <paramref name="maxSnapshots"/> oldest-first. Best-effort; never throws.</summary>
        public static void Snapshot(string campaignRoot, string token, string saveName, int maxSnapshots)
        {
            try
            {
                if (string.IsNullOrEmpty(campaignRoot) || string.IsNullOrEmpty(token)) return;
                if (!Directory.Exists(campaignRoot)) return;

                var snapshotsRoot = Path.Combine(campaignRoot, SnapshotsFolderName);
                var dest = Path.Combine(snapshotsRoot, token);

                Directory.CreateDirectory(snapshotsRoot);
                // A fresh token folder each save, so a half-written copy can never corrupt a good one.
                if (Directory.Exists(dest)) TryDeleteDir(dest);
                Directory.CreateDirectory(dest);

                CopyFolderContents(campaignRoot, dest, excludeTopLevelName: SnapshotsFolderName);

                UpdateIndex(snapshotsRoot, saveName, token, Math.Max(1, maxSnapshots));
            }
            catch { /* a photograph is a nicety; a save must never fail for it */ }
        }

        /// <summary>Restores _snapshots\&lt;token&gt;\ over the live campaign folder, replacing every NPC
        /// folder and the letters files but leaving _snapshots\ itself untouched. Returns false and
        /// changes nothing when the snapshot is missing or empty (so a stale or absent token is safe).</summary>
        public static bool Restore(string campaignRoot, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(campaignRoot) || string.IsNullOrEmpty(token)) return false;

                var snapshotsRoot = Path.Combine(campaignRoot, SnapshotsFolderName);
                var src = Path.Combine(snapshotsRoot, token);
                if (!Directory.Exists(src)) return false;

                // Fail safe: an empty snapshot must never blank a live campaign.
                bool hasContent = Directory.EnumerateFileSystemEntries(src).Any();
                if (!hasContent) return false;

                ClearFolderContents(campaignRoot, excludeTopLevelName: SnapshotsFolderName);
                CopyFolderContents(src, campaignRoot, excludeTopLevelName: null);
                return true;
            }
            catch { return false; }
        }

        // Maps saveName -> token so overwriting a slot frees its old photograph, and caps total count.
        private static void UpdateIndex(string snapshotsRoot, string saveName, string token, int maxSnapshots)
        {
            var indexPath = Path.Combine(snapshotsRoot, IndexFileName);
            JObject index;
            try { index = File.Exists(indexPath) ? JObject.Parse(File.ReadAllText(indexPath)) : new JObject(); }
            catch { index = new JObject(); }

            var key = string.IsNullOrWhiteSpace(saveName) ? token : saveName;

            // Overwriting this slot? Drop the photograph it used to point at.
            if (index[key] is JObject prior && (string?)prior["token"] is string oldToken
                && !string.IsNullOrEmpty(oldToken) && oldToken != token)
                TryDeleteDir(Path.Combine(snapshotsRoot, oldToken));

            index[key] = new JObject { ["token"] = token, ["utc"] = DateTime.UtcNow.ToString("o") };

            PruneToCap(snapshotsRoot, index, maxSnapshots);
            PruneOrphanFolders(snapshotsRoot, index);

            try { File.WriteAllText(indexPath, index.ToString()); } catch { /* best-effort */ }
        }

        // Enforces the disk cap by dropping the oldest slots (by their recorded utc) and their folders.
        private static void PruneToCap(string snapshotsRoot, JObject index, int maxSnapshots)
        {
            var entries = index.Properties()
                .Select(p => new { p.Name, Utc = (string?)p.Value["utc"] ?? string.Empty, Token = (string?)p.Value["token"] ?? string.Empty })
                .OrderBy(e => e.Utc, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < entries.Count - maxSnapshots; i++)
            {
                index.Remove(entries[i].Name);
                if (!string.IsNullOrEmpty(entries[i].Token))
                    TryDeleteDir(Path.Combine(snapshotsRoot, entries[i].Token));
            }
        }

        // Deletes token folders on disk that no live index entry points at (crash leftovers), keeping the
        // index folder and file themselves.
        private static void PruneOrphanFolders(string snapshotsRoot, JObject index)
        {
            try
            {
                var live = new HashSet<string>(
                    index.Properties().Select(p => (string?)p.Value["token"] ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var dir in Directory.GetDirectories(snapshotsRoot))
                    if (!live.Contains(Path.GetFileName(dir)))
                        TryDeleteDir(dir);
            }
            catch { /* best-effort */ }
        }

        // Recursively copies every child of src into dest, optionally skipping one top-level entry by name.
        private static void CopyFolderContents(string src, string dest, string? excludeTopLevelName)
        {
            Directory.CreateDirectory(dest);

            foreach (var dir in Directory.GetDirectories(src))
            {
                var name = Path.GetFileName(dir);
                if (excludeTopLevelName != null && string.Equals(name, excludeTopLevelName, StringComparison.OrdinalIgnoreCase))
                    continue;
                CopyDirRecursive(dir, Path.Combine(dest, name));
            }

            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }

        private static void CopyDirRecursive(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirRecursive(dir, Path.Combine(dest, Path.GetFileName(dir)));
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }

        // Deletes every child of folder, optionally sparing one top-level entry by name.
        private static void ClearFolderContents(string folder, string? excludeTopLevelName)
        {
            foreach (var dir in Directory.GetDirectories(folder))
            {
                var name = Path.GetFileName(dir);
                if (excludeTopLevelName != null && string.Equals(name, excludeTopLevelName, StringComparison.OrdinalIgnoreCase))
                    continue;
                TryDeleteDir(dir);
            }

            foreach (var file in Directory.GetFiles(folder))
                try { File.Delete(file); } catch { /* best-effort */ }
        }

        private static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* leave it; a later prune retries */ }
        }
    }
}
