using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Weddings
{
    /// <summary>
    /// The campaign's book of weddings — the <see cref="Battles.BattleLedger"/> mold applied to the
    /// gentlest record a campaign keeps: one JSON per wedding in the campaign's _weddings folder,
    /// plus a human-readable weddings.txt. Living under the campaign folder means the save-scoped
    /// memory snapshots photograph it too, so loading an older save un-writes a wedding along with
    /// the memories of it. Loading is forgiving; finding is loose, because a soul asks for "our
    /// wedding" or "that day in Onira", never for a file name.
    ///
    /// A campaign usually holds one record. It holds several only where the world allows several
    /// (a polygamy mod, a widowed player who weds again) — hence a ledger and not a single file.
    /// </summary>
    public sealed class WeddingLedger
    {
        /// <summary>The folder-name of the book inside a campaign's folder.</summary>
        public const string FolderName = "_weddings";

        /// <summary>The readable running log beside the JSONs — the full accounts as they are
        /// written, so the player can read the day itself from one file.</summary>
        public const string ChronicleFileName = "weddings.txt";

        private readonly List<WeddingRecord> _records = new List<WeddingRecord>();

        public string Folder { get; }

        public WeddingLedger(string folder)
        {
            Folder = folder ?? string.Empty;
        }

        /// <summary>All records, oldest first.</summary>
        public IReadOnlyList<WeddingRecord> Records => _records;

        /// <summary>Loads a campaign's whole book; a missing folder is an empty book, never an error.</summary>
        public static WeddingLedger LoadFrom(string folder)
        {
            var ledger = new WeddingLedger(folder);
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return ledger;
                foreach (var path in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        var record = JsonConvert.DeserializeObject<WeddingRecord>(File.ReadAllText(path));
                        if (record != null && !string.IsNullOrEmpty(record.Id)) ledger._records.Add(record);
                    }
                    catch { /* one unreadable day must not close the book */ }
                }
                ledger._records.Sort((a, b) => a.GameDay.CompareTo(b.GameDay));
            }
            catch { /* an unreadable folder is an empty book */ }
            return ledger;
        }

        /// <summary>A fresh id for a wedding on this day — "d0123-2" only if the world ever allows
        /// two in one day.</summary>
        public string NextId(double gameDay)
        {
            int day = (int)Math.Floor(gameDay);
            int seq = 1 + _records.Count(r => (int)Math.Floor(r.GameDay) == day);
            return seq == 1 ? $"d{day:0000}" : $"d{day:0000}-{seq}";
        }

        /// <summary>Adds (or, by id, replaces) a record and writes its file — a wedding is written
        /// once at the seal and once more when the chronicler's two accounts arrive.</summary>
        public void Save(WeddingRecord record)
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

        /// <summary>Appends one written day to the readable weddings.txt. Append-only.</summary>
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

        // Id first so a rewrite finds the file whatever the slug rules become; the spouse's name
        // after, so the folder reads like a family album.
        private string FileFor(WeddingRecord record)
        {
            var existing = FindFileById(record.Id);
            if (existing != null) return existing;
            var slug = Slug(record.SpouseName);
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

        /// <summary>A name flattened into a file-name tag.</summary>
        public static string Slug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            var slug = sb.ToString().Trim('-');
            return slug.Length > 40 ? slug.Substring(0, 40).TrimEnd('-') : slug;
        }

        // ------------------------------ finding and sharing ------------------------------

        /// <summary>The weddings this soul stood at (as the spouse, or as a witness), oldest first.</summary>
        public IReadOnlyList<WeddingRecord> SharedWith(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return Array.Empty<WeddingRecord>();
            return _records.Where(r => r.WasThere(heroId)).ToList();
        }

        /// <summary>This soul's own wedding to the player, or null.</summary>
        public WeddingRecord? OwnWeddingOf(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;
            for (int i = _records.Count - 1; i >= 0; i--)
                if (_records[i].IsSpouse(heroId)) return _records[i];
            return null;
        }

        /// <summary>
        /// Finds a wedding by the loose way a soul names it — "our wedding", "the day in Onira",
        /// "when you wed Sibylla". Their OWN wedding always wins when nothing else scores higher,
        /// because "our wedding day" is what they will ask for nine times in ten. Scoped to what the
        /// asker truly lived when <paramref name="heroId"/> is given.
        /// </summary>
        public WeddingRecord? Find(string query, string? heroId = null)
        {
            var pool = string.IsNullOrEmpty(heroId) ? (IReadOnlyList<WeddingRecord>)_records : SharedWith(heroId!);
            if (pool.Count == 0) return null;

            var own = string.IsNullOrEmpty(heroId) ? null : OwnWeddingOf(heroId!);
            var tokens = Tokenize(query);
            if (tokens.Count == 0) return own ?? pool[pool.Count - 1];

            if (tokens.Contains("last") || tokens.Contains("latest") || tokens.Contains("newest"))
                return pool[pool.Count - 1];
            if (tokens.Contains("first")) return pool[0];

            WeddingRecord? best = null;
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
            // "our own wedding", "my wedding day" name nothing the record carries — fall to theirs.
            return best ?? own ?? pool[pool.Count - 1];
        }

        private static int Score(WeddingRecord record, List<string> tokens)
        {
            var haystack = Normalize(string.Join(" ",
                record.SpouseName, record.PlayerName, record.PlaceName, record.DateText, record.CultureName));
            int score = 0;
            foreach (var token in tokens)
                if (haystack.Contains(token)) score++;
            return score;
        }

        // Words that name nothing on their own; "wedding"/"night"/"day" are deliberately NOT stopped
        // for scoring — they simply match nothing, and the own-wedding fallback catches them.
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        { "the", "a", "an", "of", "at", "in", "on", "by", "near", "our", "my", "that", "with", "when", "us" };

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
