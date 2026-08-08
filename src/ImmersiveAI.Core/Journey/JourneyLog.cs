using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Journey
{
    /// <summary>
    /// The company's road journal — the light log of the player's everyday campaign life, kept so
    /// the souls riding WITH the player can witness it: where we stopped and for how long, what we
    /// traded there, whom we hired or left in a garrison, captives sold or given over, and the
    /// tasks we carry (taken → succeeded / failed and why). Deliberately LIGHT (Anton's word for
    /// it): a handful of visits kept, the freshest told in detail, the older ones one line each —
    /// never a great ledger. Persisted as _journey.json in the campaign folder, so the save-scoped
    /// snapshots rewind the road with the memories. All prose lives in <see cref="JourneyText"/>.
    /// </summary>
    public sealed class JourneyLog
    {
        /// <summary>How many visits the file keeps; views show fewer (see JourneyText).</summary>
        public const int MaxVisitsKept = 12;

        /// <summary>How many settled tasks the file keeps behind the open ones.</summary>
        public const int MaxResolvedQuestsKept = 6;

        /// <summary>The place-name a between-stops bucket wears (trade with a caravan on the move).</summary>
        public const string RoadPlace = "the road";

        public List<JourneyVisit> Visits { get; set; } = new List<JourneyVisit>();
        public List<JourneyQuest> Quests { get; set; } = new List<JourneyQuest>();

        /// <summary>The stop we are presently at (or the open road-bucket), if any.</summary>
        public JourneyVisit? OpenVisit =>
            Visits.LastOrDefault(v => v != null && v.LeaveDay < 0);

        /// <summary>Begins a stop at a settlement, closing whatever was open (a dangling stop is
        /// closed at the new one's day — leaving fires before arriving, but never trust order).</summary>
        public JourneyVisit BeginVisit(string place, string kind, double day, string dateText)
        {
            CloseOpenVisit(day);
            var visit = new JourneyVisit
            {
                Place = place ?? string.Empty,
                Kind = kind ?? string.Empty,
                ArriveDay = day,
                ArriveText = dateText ?? string.Empty,
            };
            Visits.Add(visit);
            Prune();
            return visit;
        }

        /// <summary>Closes the open stop, stamping how long we stayed.</summary>
        public void CloseOpenVisit(double day)
        {
            var open = OpenVisit;
            if (open != null) open.LeaveDay = Math.Max(day, open.ArriveDay);
        }

        /// <summary>The stop to write a happening into: the open one, or a fresh "upon the road"
        /// bucket when it happens between stops (a caravan met on the move).</summary>
        public JourneyVisit CurrentOrRoad(double day, string dateText)
        {
            return OpenVisit ?? BeginVisit(RoadPlace, JourneyVisit.Kinds.Road, day, dateText);
        }

        // ------------------------------ tasks ------------------------------

        public void NoteQuestTaken(string title, string giver, double day, string dateText, int daysGiven)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            // The same task told twice (a re-fired start event) stays one entry.
            if (Quests.Any(q => q != null && q.Outcome.Length == 0
                && string.Equals(q.Title, title.Trim(), StringComparison.Ordinal))) return;

            Quests.Add(new JourneyQuest
            {
                Title = title.Trim(),
                Giver = (giver ?? string.Empty).Trim(),
                TakenDay = day,
                TakenText = dateText ?? string.Empty,
                DaysGiven = Math.Max(0, daysGiven),
            });
            Prune();
        }

        /// <summary>Settles the newest open entry bearing this title. Unknown titles are added
        /// settled (a quest taken before the journal existed still gets its closing line).</summary>
        public void NoteQuestResolved(string title, string giver, string outcome, double day)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(outcome)) return;
            var open = Quests.LastOrDefault(q => q != null && q.Outcome.Length == 0
                && string.Equals(q.Title, title.Trim(), StringComparison.Ordinal));
            if (open == null)
            {
                open = new JourneyQuest { Title = title.Trim(), Giver = (giver ?? string.Empty).Trim(), TakenDay = day };
                Quests.Add(open);
            }
            open.Outcome = outcome.Trim();
            open.ResolvedDay = day;
            Prune();
        }

        public IReadOnlyList<JourneyQuest> OpenQuests =>
            Quests.Where(q => q != null && q.Outcome.Length == 0).ToList();

        public IReadOnlyList<JourneyQuest> ResolvedQuests =>
            Quests.Where(q => q != null && q.Outcome.Length > 0).ToList();

        private void Prune()
        {
            if (Visits.Count > MaxVisitsKept)
                Visits.RemoveRange(0, Visits.Count - MaxVisitsKept);

            var resolved = ResolvedQuests;
            int drop = resolved.Count - MaxResolvedQuestsKept;
            for (int i = 0; i < resolved.Count && drop > 0; i++, drop--)
                Quests.Remove(resolved[i]);
        }

        // ------------------------------ persistence ------------------------------

        /// <summary>Loads a journal; a missing or unreadable file is an empty journal, never an error.</summary>
        public static JourneyLog LoadFrom(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return new JourneyLog();
                var log = JsonConvert.DeserializeObject<JourneyLog>(File.ReadAllText(filePath));
                if (log == null) return new JourneyLog();
                log.Visits = log.Visits?.Where(v => v != null).ToList() ?? new List<JourneyVisit>();
                log.Quests = log.Quests?.Where(q => q != null).ToList() ?? new List<JourneyQuest>();
                return log;
            }
            catch { return new JourneyLog(); }
        }

        /// <summary>Saves atomically (temp + replace), the pattern every campaign file shares.</summary>
        public void SaveTo(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path required.", nameof(filePath));
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(filePath)) File.Replace(tempPath, filePath, destinationBackupFileName: null);
            else File.Move(tempPath, filePath);
        }
    }

    /// <summary>One stop on the road, and everything the company did there.</summary>
    public sealed class JourneyVisit
    {
        public string Place { get; set; } = string.Empty;

        /// <summary>"town" / "castle" / "village" — or "road" for the between-stops bucket.</summary>
        public string Kind { get; set; } = Kinds.Town;

        public double ArriveDay { get; set; }
        public string ArriveText { get; set; } = string.Empty;

        /// <summary>-1 while we are still there.</summary>
        public double LeaveDay { get; set; } = -1;

        /// <summary>Denars paid out for what we bought here.</summary>
        public int BoughtValue { get; set; }

        /// <summary>Denars taken in for what we sold here.</summary>
        public int SoldValue { get; set; }

        /// <summary>The chief pieces bought, "Grain ×20" — a few, never the whole manifest.</summary>
        public List<string> BoughtNotable { get; set; } = new List<string>();
        public List<string> SoldNotable { get; set; } = new List<string>();

        /// <summary>Men taken on here, "2 Vlandian Recruits".</summary>
        public List<string> Recruited { get; set; } = new List<string>();

        /// <summary>Men left to hold the walls here, "3 Sturgian Warriors".</summary>
        public List<string> LeftInGarrison { get; set; } = new List<string>();

        /// <summary>Captives sold to the ransom broker here.</summary>
        public int PrisonersSold { get; set; }

        /// <summary>Captives given over to the settlement's dungeon.</summary>
        public int PrisonersDonated { get; set; }

        public bool HasAnyDoings =>
            BoughtValue > 0 || SoldValue > 0 || Recruited.Count > 0
            || LeftInGarrison.Count > 0 || PrisonersSold > 0 || PrisonersDonated > 0;

        /// <summary>Merges "2 × Vlandian Recruit" into an existing line for the same kind of man.</summary>
        public static void AddCounted(List<string> lines, string name, int count)
        {
            if (string.IsNullOrWhiteSpace(name) || count <= 0) return;
            var clean = name.Trim();
            for (int i = 0; i < lines.Count; i++)
            {
                int cut = lines[i].LastIndexOf(" ×", StringComparison.Ordinal);
                var existingName = cut > 0 ? lines[i].Substring(0, cut) : lines[i];
                if (!string.Equals(existingName, clean, StringComparison.Ordinal)) continue;
                int existingCount = 1;
                if (cut > 0) int.TryParse(lines[i].Substring(cut + 2), out existingCount);
                lines[i] = existingCount + count > 1 ? $"{clean} ×{existingCount + count}" : clean;
                return;
            }
            lines.Add(count > 1 ? $"{clean} ×{count}" : clean);
        }

        public static class Kinds
        {
            public const string Town = "town";
            public const string Castle = "castle";
            public const string Village = "village";
            public const string Road = "road";
        }
    }

    /// <summary>One task carried by the company: taken, and later settled with its outcome.</summary>
    public sealed class JourneyQuest
    {
        public string Title { get; set; } = string.Empty;
        public string Giver { get; set; } = string.Empty;
        public double TakenDay { get; set; }
        public string TakenText { get; set; } = string.Empty;

        /// <summary>Days allowed when taken; 0 = no told limit.</summary>
        public int DaysGiven { get; set; }

        /// <summary>Empty while the task is carried; else the settled outcome, reason included
        /// ("succeeded", "failed — the time ran out", "set aside").</summary>
        public string Outcome { get; set; } = string.Empty;

        public double ResolvedDay { get; set; } = -1;
    }
}
