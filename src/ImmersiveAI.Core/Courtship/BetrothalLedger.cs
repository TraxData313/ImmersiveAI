using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// The campaign's book of betrothals — the <see cref="Weddings.WeddingLedger"/> mold on the
    /// promise that comes before the wedding: one JSON per betrothal in the campaign's
    /// _betrothals folder, plus a human-readable betrothals.txt. Living under the campaign folder
    /// means the save-scoped snapshots photograph it, so loading an older save un-asks the
    /// question along with the memories of it.
    ///
    /// A campaign usually holds one. Several only where the world allows several (a polygamy mod,
    /// a troth broken and a new one given) — hence a ledger and not a single file.
    /// </summary>
    public sealed class BetrothalLedger
    {
        /// <summary>The folder-name of the book inside a campaign's folder.</summary>
        public const string FolderName = "_betrothals";

        /// <summary>The readable running log beside the JSONs.</summary>
        public const string ChronicleFileName = "betrothals.txt";

        private readonly List<BetrothalRecord> _records = new List<BetrothalRecord>();

        public string Folder { get; }

        public BetrothalLedger(string folder)
        {
            Folder = folder ?? string.Empty;
        }

        /// <summary>All records, oldest first.</summary>
        public IReadOnlyList<BetrothalRecord> Records => _records;

        /// <summary>Loads a campaign's whole book; a missing folder is an empty book, never an error.</summary>
        public static BetrothalLedger LoadFrom(string folder)
        {
            var ledger = new BetrothalLedger(folder);
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return ledger;
                foreach (var path in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        var record = JsonConvert.DeserializeObject<BetrothalRecord>(File.ReadAllText(path));
                        if (record != null && !string.IsNullOrEmpty(record.Id)) ledger._records.Add(record);
                    }
                    catch { /* one unreadable day must not close the book */ }
                }
                ledger._records.Sort((a, b) => a.GameDay.CompareTo(b.GameDay));
            }
            catch { /* an unreadable folder is an empty book */ }
            return ledger;
        }

        /// <summary>A fresh id for a betrothal on this day.</summary>
        public string NextId(double gameDay)
        {
            int day = (int)Math.Floor(gameDay);
            int seq = 1 + _records.Count(r => (int)Math.Floor(r.GameDay) == day);
            return seq == 1 ? $"d{day:0000}" : $"d{day:0000}-{seq}";
        }

        /// <summary>Adds (or, by id, replaces) a record and writes its file — once at the seal,
        /// once more when the chronicler's account arrives.</summary>
        public void Save(BetrothalRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.Id)) return;

            int at = _records.FindIndex(r => string.Equals(r.Id, record.Id, StringComparison.Ordinal));
            if (at >= 0) _records[at] = record;
            else
            {
                _records.Add(record);
                _records.Sort((a, b) => a.GameDay.CompareTo(b.GameDay));
            }

            if (string.IsNullOrWhiteSpace(Folder)) return;
            Directory.CreateDirectory(Folder);

            var path = FileFor(record);
            var json = JsonConvert.SerializeObject(record, Formatting.Indented);
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path)) File.Replace(tempPath, path, destinationBackupFileName: null);
            else File.Move(tempPath, path);
        }

        /// <summary>Appends one written day to the readable betrothals.txt. Append-only.</summary>
        public void AppendToChronicle(string entry)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Folder) || string.IsNullOrWhiteSpace(entry)) return;
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, ChronicleFileName),
                    entry.TrimEnd() + Environment.NewLine + Environment.NewLine);
            }
            catch { /* the log is a nicety; the JSONs are the record */ }
        }

        private string FileFor(BetrothalRecord record)
        {
            var existing = FindFileById(record.Id);
            if (existing != null) return existing;
            var slug = Weddings.WeddingLedger.Slug(record.SpouseName);
            var name = slug.Length > 0 ? record.Id + "_" + slug + ".json" : record.Id + ".json";
            return Path.Combine(Folder, name);
        }

        private string? FindFileById(string id)
        {
            try
            {
                if (!Directory.Exists(Folder)) return null;
                return Directory.GetFiles(Folder, id + "*.json")
                    .FirstOrDefault(p =>
                    {
                        var name = Path.GetFileNameWithoutExtension(p);
                        return string.Equals(name, id, StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase);
                    });
            }
            catch { return null; }
        }

        /// <summary>This soul's own betrothal to the player (the newest, where a troth was ever
        /// broken and given again), or null.</summary>
        public BetrothalRecord? OwnBetrothalOf(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;
            for (int i = _records.Count - 1; i >= 0; i--)
                if (_records[i].IsOfTheTwo(heroId)) return _records[i];
            return null;
        }
    }
}
