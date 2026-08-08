using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Battles
{
    /// <summary>
    /// The campaign's book of battles: every <see cref="BattleRecord"/> of one playthrough, one JSON
    /// file per battle in the campaign's _battles folder (same atomic-write pattern as
    /// <see cref="Letters.LetterBag"/>). Living under the campaign folder means the save-scoped memory
    /// snapshots photograph it too — load an older save and the battles that never happened are
    /// un-written along with the memories of them. Loading is forgiving (an unreadable file is skipped,
    /// never an error); finding is by name, loosely, because an NPC will ask for "the storming of
    /// Varcheg" or just "Ortysia", not for a file name.
    /// </summary>
    public sealed class BattleLedger
    {
        /// <summary>The folder-name of the book inside a campaign's folder.</summary>
        public const string FolderName = "_battles";

        /// <summary>The human-readable running log kept beside the JSONs — every full account,
        /// appended as it is written, so the player can read the whole war from one file.</summary>
        public const string ChronicleFileName = "chronicle.txt";

        private readonly List<BattleRecord> _records = new List<BattleRecord>();

        public string Folder { get; }

        public BattleLedger(string folder)
        {
            Folder = folder ?? string.Empty;
        }

        /// <summary>All records, oldest first.</summary>
        public IReadOnlyList<BattleRecord> Records => _records;

        /// <summary>Loads a campaign's whole book; a missing folder is an empty book, never an error.</summary>
        public static BattleLedger LoadFrom(string folder)
        {
            var ledger = new BattleLedger(folder);
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return ledger;
                foreach (var path in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        var record = JsonConvert.DeserializeObject<BattleRecord>(File.ReadAllText(path));
                        if (record != null && !string.IsNullOrEmpty(record.Id)) ledger._records.Add(record);
                    }
                    catch { /* one unreadable battle must not close the book */ }
                }
                ledger._records.Sort((a, b) => a.GameDay.CompareTo(b.GameDay));
            }
            catch { /* an unreadable folder is an empty book */ }
            return ledger;
        }

        /// <summary>A fresh id for a battle fought on this day — "d0123-2" when one was already
        /// recorded that day.</summary>
        public string NextId(double gameDay)
        {
            int day = (int)Math.Floor(gameDay);
            int seq = 1 + _records.Count(r => (int)Math.Floor(r.GameDay) == day);
            return seq == 1 ? $"d{day:0000}" : $"d{day:0000}-{seq}";
        }

        /// <summary>Adds (or, by id, replaces) a record in the book and writes its file — loot and the
        /// purse arrive a breath after the fighting, so a battle is written once at the field's edge
        /// and once more when the spoils are told.</summary>
        public void Save(BattleRecord record)
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

        /// <summary>Appends one full account to the human-readable chronicle.txt. Append-only — the
        /// running log keeps the first telling even when the JSON is later enriched with spoils.</summary>
        public void AppendToChronicle(string fullAccount)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Folder) || string.IsNullOrWhiteSpace(fullAccount)) return;
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, ChronicleFileName),
                    fullAccount.TrimEnd() + Environment.NewLine + Environment.NewLine);
            }
            catch { /* the log is a nicety; the JSONs are the record */ }
        }

        // The file a record lives in: id first so a rewrite finds it even if the slug rules ever
        // shift, a slug of the title after so the folder reads like a table of contents.
        private string FileFor(BattleRecord record)
        {
            var existing = FindFileById(record.Id);
            if (existing != null) return existing;
            var slug = Slug(record.Title);
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

        /// <summary>A title flattened into a file-name tag ("the-grand-victory-near-ortysia…").</summary>
        public static string Slug(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in title.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            var slug = sb.ToString().Trim('-');
            return slug.Length > 40 ? slug.Substring(0, 40).TrimEnd('-') : slug;
        }

        // ------------------------------ finding and sharing ------------------------------

        /// <summary>The battles this hero stood in beside the player, oldest first.</summary>
        public IReadOnlyList<BattleRecord> SharedWith(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return Array.Empty<BattleRecord>();
            return _records.Where(r => r.ParticipantById(heroId) != null).ToList();
        }

        /// <summary>The latest battle this hero and the player lived through together, or null.</summary>
        public BattleRecord? LatestSharedWith(string heroId)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
                if (_records[i].ParticipantById(heroId) != null) return _records[i];
            return null;
        }

        /// <summary>
        /// Finds a battle by the loose way people name them — "the storming of Varcheg", "Ortysia",
        /// "that sea-fight", "the last battle". Scored by how many of the asker's words the record's
        /// title, place, foe, date, or kind carry; ties go to the most recent. Scoped to one hero's
        /// shared battles when <paramref name="heroId"/> is given (an NPC recalls what THEY lived).
        /// Returns null when nothing matches at all.
        /// </summary>
        public BattleRecord? Find(string query, string? heroId = null)
        {
            var pool = string.IsNullOrEmpty(heroId) ? (IReadOnlyList<BattleRecord>)_records : SharedWith(heroId!);
            if (pool.Count == 0) return null;

            var tokens = Tokenize(query);
            if (tokens.Count == 0) return pool[pool.Count - 1];

            // "the last battle" / "our latest fight" reach straight for the newest shared record.
            if (tokens.Contains("last") || tokens.Contains("latest") || tokens.Contains("newest"))
                return pool[pool.Count - 1];
            if (tokens.Contains("first"))
                return pool[0];

            BattleRecord? best = null;
            int bestScore = 0;
            foreach (var record in pool) // oldest → newest, so a tie lands on the most recent
            {
                int score = Score(record, tokens);
                if (score >= bestScore && score > 0)
                {
                    best = record;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int Score(BattleRecord record, List<string> tokens)
        {
            var haystack = Normalize(string.Join(" ",
                record.Title, record.PlaceName, record.EnemyName, record.DateText, record.Kind));
            int score = 0;
            foreach (var token in tokens)
                if (haystack.Contains(token)) score++;
            return score;
        }

        // Words that name nothing on their own — never let "the battle of" match every record equally
        // ("battle"/"fight" are in most titles, so they stay meaningful and are NOT stopped).
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        { "the", "a", "an", "of", "at", "in", "on", "by", "near", "off", "our", "that", "with", "over" };

        private static List<string> Tokenize(string query)
        {
            var tokens = new List<string>();
            foreach (var raw in Normalize(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (raw.Length < 2 || StopWords.Contains(raw)) continue;
                if (!tokens.Contains(raw)) tokens.Add(raw);
            }
            return tokens;
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (var c in text.ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            return sb.ToString();
        }
    }
}
