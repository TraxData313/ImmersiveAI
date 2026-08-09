using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Births
{
    /// <summary>
    /// The campaign's book of children — the <see cref="Weddings.WeddingLedger"/> mold, and
    /// deliberately not one line cleverer than it. One JSON per birth in the campaign's _births
    /// folder plus a readable births.txt; living under the campaign folder means the save-scoped
    /// memory snapshots photograph it too, so loading an older save un-writes a child along with
    /// the memories of one. Loading is forgiving; finding is loose, because a soul asks for "the
    /// day our son was born" or "Ira", never for a file name.
    ///
    /// Unlike the wedding book, this one fills up: a long marriage is many records, and both of a
    /// birth's two parts may be owed a chronicler at once, days apart.
    /// </summary>
    public sealed class BirthLedger
    {
        public const string FolderName = "_births";
        public const string ChronicleFileName = "births.txt";

        /// <summary>How many times each part of a birth is asked for before we let it be. The
        /// wedding's number, for the wedding's reason: a passing 429 gets three honest chances and
        /// a dying key is not hammered forever.</summary>
        public const int MaxAttempts = 3;

        /// <summary>How long a father has to come home and hold the feast before the moment is
        /// simply past. A month is generous and finite; a cradle-side feast held half a year late
        /// would be a stranger's idea of a christening.</summary>
        public const double FeastOfferDays = 30;

        private readonly List<BirthRecord> _records = new List<BirthRecord>();

        public string Folder { get; }

        public BirthLedger(string folder) { Folder = folder ?? string.Empty; }

        /// <summary>All records, oldest first.</summary>
        public IReadOnlyList<BirthRecord> Records => _records;

        /// <summary>Loads a campaign's whole book; a missing folder is an empty book, never an error.</summary>
        public static BirthLedger LoadFrom(string folder)
        {
            var ledger = new BirthLedger(folder);
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return ledger;
                foreach (var path in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        var record = JsonConvert.DeserializeObject<BirthRecord>(File.ReadAllText(path));
                        if (record != null && !string.IsNullOrEmpty(record.Id)) ledger._records.Add(record);
                    }
                    catch { /* one unreadable day must not close the book */ }
                }
                ledger._records.Sort((a, b) => a.GameDay.CompareTo(b.GameDay));
            }
            catch { /* an unreadable folder is an empty book */ }
            return ledger;
        }

        /// <summary>A fresh id for a birth on this day — two in one day is rare but possible with
        /// more than one wife, so the sequence is there.</summary>
        public string NextId(double gameDay)
        {
            int day = (int)Math.Floor(gameDay);
            int seq = 1 + _records.Count(r => (int)Math.Floor(r.GameDay) == day);
            return seq == 1 ? $"d{day:0000}" : $"d{day:0000}-{seq}";
        }

        /// <summary>Adds (or, by id, replaces) a record and writes its file. A birth is written at
        /// the hour, again when the hour's account arrives, again if a feast is bought, and once
        /// more when the feast's account arrives.</summary>
        public void Save(BirthRecord record)
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

        /// <summary>Appends one written day to the readable births.txt. Append-only.</summary>
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

        private string FileFor(BirthRecord record)
        {
            var existing = FindFileById(record.Id);
            if (existing != null) return existing;
            var slug = Weddings.WeddingLedger.Slug(record.Children?.FirstOrDefault()?.Name ?? string.Empty);
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

        // ------------------------------ finding and sharing ------------------------------

        /// <summary>The births this soul lived (as a parent, or standing at the feast), oldest first.</summary>
        public IReadOnlyList<BirthRecord> SharedWith(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return Array.Empty<BirthRecord>();
            return _records.Where(r => r.WasThere(heroId)).ToList();
        }

        /// <summary>This soul's own children by the player, oldest first.</summary>
        public IReadOnlyList<BirthRecord> ChildrenOf(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return Array.Empty<BirthRecord>();
            return _records.Where(r => r.IsParent(heroId)).ToList();
        }

        /// <summary>The record of this very child, by the newborn's own id.</summary>
        public BirthRecord? ForChild(string childId)
        {
            if (string.IsNullOrEmpty(childId)) return null;
            return _records.LastOrDefault(r => r.Children != null
                && r.Children.Any(c => c != null && string.Equals(c.HeroId, childId, StringComparison.Ordinal)));
        }

        // ------------------------------ what still owes us something ------------------------------

        /// <summary>Births whose hour was never written, freshest first — the hourly retry's list.
        /// Guarded on CONTENT, never on existence: a record saved at the hour with no account yet
        /// is precisely what the retry is for.</summary>
        public IReadOnlyList<BirthRecord> AwaitingHour(int maxAttempts = MaxAttempts) =>
            _records.Where(r => r != null && r.AnyLived
                        && string.IsNullOrWhiteSpace(r.BirthAccount)
                        && r.BirthAttempts < maxAttempts)
                .OrderByDescending(r => r.GameDay).ToList();

        /// <summary>Feasts that were bought and never written, freshest first.</summary>
        public IReadOnlyList<BirthRecord> AwaitingFeast(int maxAttempts = MaxAttempts) =>
            _records.Where(r => r != null && r.Scale != BirthScale.Unfeasted
                        && string.IsNullOrWhiteSpace(r.FeastAccount)
                        && r.FeastAttempts < maxAttempts)
                .OrderByDescending(r => r.GameDay).ToList();

        /// <summary>Children whose father has not yet been asked whether he will keep a feast, and
        /// for whom it is not too late. Oldest first — the child who has waited longest is asked
        /// about first.</summary>
        public IReadOnlyList<BirthRecord> AwaitingFeastOffer(double today, double withinDays = FeastOfferDays) =>
            _records.Where(r => r != null && r.AnyLived && !r.FeastOffered
                        && r.Scale == BirthScale.Unfeasted
                        && today - r.GameDay <= withinDays)
                .OrderBy(r => r.GameDay).ToList();

        /// <summary>
        /// Finds a birth by the loose way a soul names it — "when our son was born", "Ira", "that
        /// day in Akkalat", "the last one". A parent's own most recent child wins when nothing else
        /// scores, because that is what they will mean nine times in ten.
        /// </summary>
        public BirthRecord? Find(string query, string? heroId = null)
        {
            var pool = string.IsNullOrEmpty(heroId) ? (IReadOnlyList<BirthRecord>)_records : SharedWith(heroId!);
            if (pool.Count == 0) return null;

            var own = string.IsNullOrEmpty(heroId) ? null : ChildrenOf(heroId!).LastOrDefault();
            var tokens = Tokenize(query);
            if (tokens.Count == 0) return own ?? pool[pool.Count - 1];

            if (tokens.Contains("last") || tokens.Contains("latest") || tokens.Contains("newest")
                || tokens.Contains("youngest"))
                return pool[pool.Count - 1];
            if (tokens.Contains("first") || tokens.Contains("eldest") || tokens.Contains("oldest"))
                return pool[0];

            BirthRecord? best = null;
            int bestScore = 0;
            foreach (var record in pool)  // oldest → newest, so a tie lands on the most recent
            {
                int score = Score(record, tokens);
                if (score >= bestScore && score > 0)
                {
                    best = record;
                    bestScore = score;
                }
            }
            return best ?? own ?? pool[pool.Count - 1];
        }

        private static int Score(BirthRecord record, List<string> tokens)
        {
            var haystack = Normalize(string.Join(" ",
                record.ChildNames(), record.MotherName, record.FatherName,
                record.PlaceName, record.DateText, record.CultureName));
            int score = 0;
            foreach (var token in tokens)
                if (haystack.Contains(token)) score++;
            return score;
        }

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        { "the", "a", "an", "of", "at", "in", "on", "by", "near", "our", "my", "that", "with", "when", "us", "was", "born" };

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
