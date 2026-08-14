using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImmersiveAI.Core.Voices;
using Newtonsoft.Json;

namespace ImmersiveAI.Voice
{
    /// <summary>What one utterance's folder holds, written last so its presence means "finished".</summary>
    internal sealed class VoiceCacheManifest
    {
        public string Key { get; set; } = string.Empty;
        public string VoiceId { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int LanguageId { get; set; } = -1;

        /// <summary>How many chunk files there should be. A folder holding fewer is unfinished.</summary>
        public int Chunks { get; set; }

        public long Bytes { get; set; }

        /// <summary>The words, so a cache folder can be understood by a human looking at it. The
        /// same words already sit in memories.json and letters.txt; nothing new is disclosed.</summary>
        public string Text { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// The spoken lines, kept on disk so a line is made once and heard forever after.
    /// <para>
    /// <c>Voices\_cache\&lt;voiceId&gt;\&lt;key&gt;\000.wav</c>, and the key is
    /// <see cref="VoiceCacheKey"/>'s, which folds in the voice, the model and the language — so a
    /// recast or a model swap invalidates nothing by hand: the key simply changes and the old audio
    /// ages out on its own.
    /// </para>
    /// <para>
    /// TWO RULES carry everything here. First, the manifest is written LAST and a hit requires it
    /// plus every chunk it names — so a synthesis cut short by a crash, an alt-F4 or a dying host is
    /// never served as a whole reply with a hole in the middle. Second, a folder is pruned WHOLE and
    /// never while its audio is playing: chunk 3 of 5 vanishing mid-sentence would be a bug the
    /// player could hear but never reproduce.
    /// </para>
    /// </summary>
    internal static class VoiceCache
    {
        private const string ManifestName = "manifest.json";

        /// <summary>Prune no oftener than this, however many lines are spoken.</summary>
        private static readonly TimeSpan PruneRest = TimeSpan.FromMinutes(2);

        /// <summary>Rewriting a manifest on every hit would be a disk write per spoken line for a
        /// timestamp nobody reads until the cache is full. An hour's resolution is plenty for
        /// deciding which lines are old.</summary>
        private static readonly TimeSpan TouchRest = TimeSpan.FromHours(1);

        /// <summary>Prune down to this share of the budget, so it is not re-triggered by the next line.</summary>
        private const double PruneDownTo = 0.85;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> InUse = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _lastPruneUtc = DateTime.MinValue;
        private static bool _pruning;

        /// <summary>The player's shelf of voices — where the casting sheet and the voice folders live.</summary>
        public static string VoicesRoot => Path.Combine(ModConfig.ConfigDirectory, "Voices");

        /// <summary>Where the spoken audio is kept. Under the shelf, and beginning with an underscore
        /// like every other bookkeeping folder this mod writes, so it never reads as a voice.</summary>
        public static string CacheRoot => Path.Combine(VoicesRoot, "_cache");

        /// <summary>One utterance's folder. Never creates anything.</summary>
        public static string FolderFor(string voiceId, string key)
            => Path.Combine(CacheRoot, SafeName(voiceId), SafeName(key));

        public static string ChunkPath(string folder, int index)
            => Path.Combine(folder, VoiceCacheKey.ChunkFileName(index));

        // ------------------------------------------------------------------

        /// <summary>
        /// The audio for this line, in order, or null when it is not (wholly) there. A folder whose
        /// manifest is missing or whose chunks do not all exist is a miss, not a partial hit.
        /// </summary>
        public static IReadOnlyList<string>? TryHit(string voiceId, string key)
        {
            try
            {
                var folder = FolderFor(voiceId, key);
                var manifest = ReadManifest(folder);
                if (manifest == null || manifest.Chunks <= 0) return null;

                var files = new List<string>(manifest.Chunks);
                for (var i = 0; i < manifest.Chunks; i++)
                {
                    var path = ChunkPath(folder, i);
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length <= 44) return null;   // 44 bytes is a bare WAV header
                    files.Add(path);
                }

                Touch(folder, manifest);
                return files;
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: reading a cached line", ex);
                return null;
            }
        }

        /// <summary>Makes an utterance's folder ready to be written into, clearing any half-finished
        /// attempt that was left in it. Returns empty when the folder cannot be made.</summary>
        public static string PrepareFolder(string voiceId, string key)
        {
            try
            {
                var folder = FolderFor(voiceId, key);
                Directory.CreateDirectory(folder);

                // An unsealed folder is the wreckage of an interrupted synthesis. Its chunks may be
                // truncated, so they are cleared rather than reused.
                if (ReadManifest(folder) == null)
                    foreach (var stale in SafeFiles(folder, "*.wav"))
                        try { File.Delete(stale); } catch { }

                return folder;
            }
            catch (Exception ex)
            {
                ModLog.Error("voice: preparing a cache folder", ex);
                return string.Empty;
            }
        }

        /// <summary>Seals a finished utterance: the manifest goes down last, and only then is the
        /// folder a hit for anyone else.</summary>
        public static void Seal(string folder, VoiceCacheManifest manifest)
        {
            if (string.IsNullOrEmpty(folder) || manifest == null) return;
            try
            {
                manifest.Bytes = SafeFiles(folder, "*.wav").Sum(SizeOf);
                manifest.LastUsedUtc = DateTime.UtcNow;

                var path = Path.Combine(folder, ManifestName);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(manifest, Formatting.Indented));
                if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
                else File.Move(temp, path);
            }
            catch (Exception ex)
            {
                // An unsealed folder is simply a miss next time — the line is made again. Nothing is
                // broken by a cache that failed to remember.
                ModLog.Error("voice: sealing a cached line", ex);
            }
        }

        // ------------------------------------------------------------------
        // What must not be swept away while it is being heard
        // ------------------------------------------------------------------

        public static void MarkInUse(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            lock (Gate)
            {
                InUse.TryGetValue(folder, out var count);
                InUse[folder] = count + 1;
            }
        }

        public static void ReleaseInUse(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            lock (Gate)
            {
                if (!InUse.TryGetValue(folder, out var count)) return;
                if (count <= 1) InUse.Remove(folder);
                else InUse[folder] = count - 1;
            }
        }

        private static bool IsInUse(string folder)
        {
            lock (Gate) return InUse.ContainsKey(folder);
        }

        // ------------------------------------------------------------------
        // Pruning
        // ------------------------------------------------------------------

        /// <summary>
        /// Trims the cache to its budget, off the game thread and at most once every few minutes.
        /// Whole folders, oldest-used first, and never one whose audio is in the air. A budget of 0
        /// means "keep everything", which is a legitimate choice on a big disk.
        /// </summary>
        public static void PruneSoon(long budgetBytes)
        {
            if (budgetBytes <= 0) return;

            lock (Gate)
            {
                if (_pruning) return;
                if (DateTime.UtcNow - _lastPruneUtc < PruneRest) return;
                _pruning = true;
                _lastPruneUtc = DateTime.UtcNow;
            }

            Task.Run(() =>
            {
                try { Prune(budgetBytes); }
                catch (Exception ex) { ModLog.Error("voice: pruning the spoken lines", ex); }
                finally { lock (Gate) _pruning = false; }
            });
        }

        private static void Prune(long budgetBytes)
        {
            var root = CacheRoot;
            if (!Directory.Exists(root)) return;

            var entries = new List<(string Folder, long Bytes, DateTime Used, bool Junk)>();
            long total = 0;

            foreach (var voiceFolder in SafeDirectories(root))
            foreach (var folder in SafeDirectories(voiceFolder))
            {
                var bytes = SafeFiles(folder, "*.*").Sum(SizeOf);
                var manifest = ReadManifest(folder);
                var junk = manifest == null;                    // never sealed: wreckage, sweep it first
                var used = manifest?.LastUsedUtc ?? DateTime.MinValue;

                // An unsealed folder from the last few minutes may be a synthesis in progress right
                // now. Its chunks are being written as we look at them.
                if (junk && DirectoryWrittenWithin(folder, TimeSpan.FromMinutes(10))) continue;

                entries.Add((folder, bytes, used, junk));
                total += bytes;
            }

            if (total <= budgetBytes) return;

            var target = (long)(budgetBytes * PruneDownTo);
            var order = entries.OrderByDescending(e => e.Junk).ThenBy(e => e.Used).ToList();

            var freed = 0L;
            var swept = 0;
            foreach (var entry in order)
            {
                if (total - freed <= target) break;
                if (IsInUse(entry.Folder)) continue;            // it is being heard this very moment

                try
                {
                    Directory.Delete(entry.Folder, recursive: true);
                    freed += entry.Bytes;
                    swept++;
                }
                catch { /* locked or already gone; the next pass will find it */ }
            }

            if (swept > 0)
                ModLog.Info($"voice: cache trimmed — {swept} line(s), {freed / (1024 * 1024)} MB freed of {total / (1024 * 1024)} MB.");
        }

        // ------------------------------------------------------------------

        private static VoiceCacheManifest? ReadManifest(string folder)
        {
            try
            {
                var path = Path.Combine(folder, ManifestName);
                if (!File.Exists(path)) return null;
                return JsonConvert.DeserializeObject<VoiceCacheManifest>(File.ReadAllText(path));
            }
            catch { return null; }
        }

        private static void Touch(string folder, VoiceCacheManifest manifest)
        {
            try
            {
                if (DateTime.UtcNow - manifest.LastUsedUtc < TouchRest) return;
                manifest.LastUsedUtc = DateTime.UtcNow;

                var path = Path.Combine(folder, ManifestName);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(manifest, Formatting.Indented));
                if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
                else File.Move(temp, path);
            }
            catch { /* the stamp is a hint for pruning, never worth an error */ }
        }

        private static bool DirectoryWrittenWithin(string folder, TimeSpan window)
        {
            try { return DateTime.UtcNow - Directory.GetLastWriteTimeUtc(folder) < window; }
            catch { return false; }
        }

        private static long SizeOf(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static IEnumerable<string> SafeDirectories(string root)
        {
            try { return Directory.GetDirectories(root); }
            catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeFiles(string folder, string pattern)
        {
            try { return Directory.GetFiles(folder, pattern); }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>A folder name that cannot escape the cache, whatever a voice id or a key holds.
        /// Both are already tame — a slug and a hex hash — so this is a rail, not a transformation.</summary>
        private static string SafeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "_";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = raw.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            var name = new string(chars).Trim('.', ' ');
            if (name.Length == 0) return "_";
            return name.Length > 64 ? name.Substring(0, 64) : name;
        }
    }
}
