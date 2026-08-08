using System;
using System.Collections.Generic;
using System.IO;
using ImmersiveAI.Core.Memory;

namespace ImmersiveAI
{
    /// <summary>
    /// A read cache over the campaign's memory files for the hot loops that only need a few
    /// facts per NPC (richness, last-talk day, name): the hourly letter roll and the per-hour
    /// co-located pulls used to re-parse every memories.json each time — fine at 30 NPCs, real
    /// I/O at 300. Entries are keyed by file path and self-invalidate on the file's write stamp,
    /// so saves, snapshot restores, and hand-edits all take effect with no wiring. THREAD-SAFE
    /// since 2026.08.08 (the courtship resolvers and the letter-answer flow read it from LLM
    /// worker threads while the hourly rolls walk it on the game thread): the cache dictionary is
    /// guarded by a lock, file IO deliberately outside it — a rare duplicate parse is cheaper than
    /// the game thread waiting on a worker's disk read. Best-effort: a parse failure just means
    /// "no entry".
    /// </summary>
    internal static class MemoryIndex
    {
        internal sealed class Entry
        {
            public string NpcId = "";
            public string NpcName = "";
            public int Richness;
            public double LastTalkGameDay = -1;
            public double LastOutreachGameDay = -1;
            public int UnansweredOutreach;
            /// <summary>The courtship road's stage as a raw int (see Core CourtshipStage) — read by
            /// the bond-stats line, the odds view, and the one-troth-at-a-time rule without loading
            /// the whole memory.</summary>
            public int CourtshipStage;
            /// <summary>Her own written marriage misgivings, counted for the bond-stats line:
            /// how many still stand, how many she set down in all, and whether she has weighed
            /// her heart at least once (even to find it clear).</summary>
            public int MisgivingsOpen;
            public int MisgivingsTotal;
            public bool MisgivingsWeighed;
            public DateTime StampUtc;
            public long Length;
        }

        private static readonly Dictionary<string, Entry> Cache =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new object();

        /// <summary>The cached facts for one memory file, re-parsed only when it changed on disk.</summary>
        internal static Entry? Get(string memFile, JsonMemoryStore store)
        {
            try
            {
                var info = new FileInfo(memFile);
                if (!info.Exists)
                {
                    lock (CacheLock) Cache.Remove(memFile);
                    return null;
                }

                lock (CacheLock)
                {
                    if (Cache.TryGetValue(memFile, out var hit)
                        && hit.StampUtc == info.LastWriteTimeUtc && hit.Length == info.Length)
                        return hit;
                }

                var memory = store.LoadFrom(memFile, string.Empty);
                var entry = new Entry
                {
                    NpcId = memory.NpcId ?? "",
                    NpcName = memory.NpcName ?? "",
                    Richness = memory.StoryRichness,
                    LastTalkGameDay = memory.LastConversationGameDay,
                    LastOutreachGameDay = memory.LastOutreachGameDay,
                    UnansweredOutreach = memory.UnansweredOutreachCount,
                    CourtshipStage = (int)memory.CourtshipStage,
                    MisgivingsOpen = Core.Courtship.CourtshipMisgivings.OpenCount(memory.CourtshipMisgivings),
                    MisgivingsTotal = Core.Courtship.CourtshipMisgivings.TotalCount(memory.CourtshipMisgivings),
                    MisgivingsWeighed = memory.MisgivingsWeighed,
                    StampUtc = info.LastWriteTimeUtc,
                    Length = info.Length,
                };
                lock (CacheLock) Cache[memFile] = entry;
                return entry;
            }
            catch { return null; }
        }

        /// <summary>Every NPC with a memory file under the campaign root (the same folders the
        /// old scans walked; _snapshots has no memories.json at its top level, so it never matches).</summary>
        internal static IEnumerable<Entry> All(string campaignRoot, string memoryFileName, JsonMemoryStore store)
        {
            if (string.IsNullOrEmpty(campaignRoot) || !Directory.Exists(campaignRoot)) yield break;

            string[] folders;
            try { folders = Directory.GetDirectories(campaignRoot); }
            catch { yield break; }

            foreach (var folder in folders)
            {
                var entry = Get(Path.Combine(folder, memoryFileName), store);
                if (entry != null && !string.IsNullOrWhiteSpace(entry.NpcId))
                    yield return entry;
            }
        }
    }
}
