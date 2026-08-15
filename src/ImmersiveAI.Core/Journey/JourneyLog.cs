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

        /// <summary>
        /// The stop we are presently at (or the open road-bucket), if any.
        /// <para>
        /// NEVER PERSISTED, and that attribute is a BUG FIX rather than tidiness (the item flood,
        /// Anton 2026.08.16 — a town stop listing "Silver Ore x6, Cow x4, Salt x19…" four times
        /// over). This property ALIASES a live element of <see cref="Visits"/>. Serialized, the file
        /// carries that visit twice; and on the way back in, Newtonsoft's default
        /// <c>ObjectCreationHandling.Auto</c> does not skip a readable-but-not-writable member
        /// holding a non-null object — it POPULATES the instance it already has, which is the very
        /// same visit, appending its piece-lists to themselves. Every save-then-load with a stop
        /// open doubled them again.
        /// </para>
        /// <para>
        /// THE GENERAL LESSON, worth more than this one field: on a persisted type, a computed view
        /// over persisted state must be <see cref="JsonIgnoreAttribute"/>d. It is not merely dead
        /// weight in the file — if it hands back a live reference, the round trip mutates the thing
        /// it was only supposed to describe.
        /// </para>
        /// </summary>
        [JsonIgnore]
        public JourneyVisit? OpenVisit =>
            Visits.LastOrDefault(v => v != null && v.LeaveDay < 0);

        /// <summary>A re-entry into the same place within this many days is the SAME stay resumed,
        /// not a new stop — a save loaded inside a town re-fires the entered event, and a player
        /// stepping out to the map and back would otherwise mint an empty "latest stop" that
        /// demotes the real one's telling to a single line (the Onira bug, 2026.08.08).</summary>
        public const double ContinuedStayGapDays = 0.5;

        /// <summary>Begins a stop at a settlement, closing whatever was open (a dangling stop is
        /// closed at the new one's day — leaving fires before arriving, but never trust order).
        /// Re-entering the place just left resumes that same stay instead.</summary>
        public JourneyVisit BeginVisit(string place, string kind, double day, string dateText)
        {
            CloseOpenVisit(day);

            var last = Visits.LastOrDefault(v => v != null);
            if (last != null && last.LeaveDay >= 0
                && day - last.LeaveDay < ContinuedStayGapDays
                && string.Equals(last.Place, (place ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(last.Kind, kind, StringComparison.Ordinal))
            {
                last.LeaveDay = -1; // the stay goes on
                return last;
            }

            var visit = new JourneyVisit
            {
                Place = (place ?? string.Empty).Trim(),
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

        /// <summary>Returns the entry added, or null when the same open task was already told
        /// (a re-fired start event stays one entry — and one beat).</summary>
        public JourneyQuest? NoteQuestTaken(string title, string giver, double day, string dateText, int daysGiven)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            if (Quests.Any(q => q != null && q.Outcome.Length == 0
                && string.Equals(q.Title, title.Trim(), StringComparison.Ordinal))) return null;

            var quest = new JourneyQuest
            {
                Title = title.Trim(),
                Giver = (giver ?? string.Empty).Trim(),
                TakenDay = day,
                TakenText = dateText ?? string.Empty,
                DaysGiven = Math.Max(0, daysGiven),
            };
            Quests.Add(quest);
            Prune();
            return quest;
        }

        /// <summary>Settles the newest open entry bearing this title, returning it. Unknown titles
        /// are added settled (a quest taken before the journal existed still gets its closing line).</summary>
        public JourneyQuest? NoteQuestResolved(string title, string giver, string outcome, double day)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(outcome)) return null;
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
            return open;
        }

        // Computed views, never persisted — see OpenVisit above for why that matters here.
        [JsonIgnore]
        public IReadOnlyList<JourneyQuest> OpenQuests =>
            Quests.Where(q => q != null && q.Outcome.Length == 0).ToList();

        [JsonIgnore]
        public IReadOnlyList<JourneyQuest> ResolvedQuests =>
            Quests.Where(q => q != null && q.Outcome.Length > 0).ToList();

        /// <summary>Heals a journal whose stays were split by re-entries before the resume rule
        /// existed (or by a snapshot restore): adjacent same-place stops with barely a breath
        /// between them fold into one, values summed, lists merged, the longer stay kept.</summary>
        public void MergeContinuedStays()
        {
            for (int i = Visits.Count - 1; i >= 1; i--)
            {
                var prev = Visits[i - 1];
                var next = Visits[i];
                if (prev == null || next == null) continue;
                if (prev.LeaveDay < 0) prev.LeaveDay = next.ArriveDay; // never trust order
                if (next.ArriveDay - prev.LeaveDay >= ContinuedStayGapDays) continue;
                if (!string.Equals(prev.Place, next.Place, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(prev.Kind, next.Kind, StringComparison.Ordinal)) continue;

                prev.LeaveDay = next.LeaveDay < 0 ? -1 : Math.Max(prev.LeaveDay, next.LeaveDay);
                prev.BeatDone = prev.BeatDone || next.BeatDone;
                prev.BoughtValue += next.BoughtValue;
                prev.SoldValue += next.SoldValue;
                prev.PrisonersSold += next.PrisonersSold;
                prev.PrisonersDonated += next.PrisonersDonated;
                foreach (var line in next.BoughtNotable) JourneyVisit.AddCountedLine(prev.BoughtNotable, line);
                foreach (var line in next.SoldNotable) JourneyVisit.AddCountedLine(prev.SoldNotable, line);
                foreach (var line in next.Recruited) JourneyVisit.AddCountedLine(prev.Recruited, line);
                foreach (var line in next.LeftInGarrison) JourneyVisit.AddCountedLine(prev.LeftInGarrison, line);
                Visits.RemoveAt(i);
            }
        }

        private void Prune()
        {
            if (Visits.Count > MaxVisitsKept)
                Visits.RemoveRange(0, Visits.Count - MaxVisitsKept);

            var resolved = ResolvedQuests;
            int drop = resolved.Count - MaxResolvedQuestsKept;
            for (int i = 0; i < resolved.Count && drop > 0; i++, drop--)
                Quests.Remove(resolved[i]);
        }

        /// <summary>
        /// Heals piece-lists that the old <c>OpenVisit</c> round trip doubled (the item flood,
        /// 2026.08.16 — see that property for the cause). A journal on disk may already carry a
        /// stop's goods four or eight times over, and no amount of fixing the writer mends what is
        /// already written.
        /// <para>
        /// It rides one invariant, and only works because of it: EVERY honest write goes through
        /// <see cref="JourneyVisit.AddCounted"/>, which merges by name and returns — so an honest
        /// list can never hold the same name twice, and a repeated name is therefore proof of a
        /// replayed copy. The FIRST of them carries the true count.
        /// </para>
        /// <para>
        /// Repeats are DROPPED, never summed. Summing would turn "Silver Ore x6" seen four times
        /// into "Silver Ore x24" — inventing goods that never changed hands, and doing it in a form
        /// nobody could later tell from the truth.
        /// </para>
        /// </summary>
        public void DropReplayedLines()
        {
            foreach (var visit in Visits)
            {
                if (visit == null) continue;
                DropRepeatedNames(visit.BoughtNotable);
                DropRepeatedNames(visit.SoldNotable);
                DropRepeatedNames(visit.Recruited);
                DropRepeatedNames(visit.LeftInGarrison);
            }
        }

        // "Wool x24" and "Wool x3" are the same GOOD twice over: compare the name, not the line.
        private static void DropRepeatedNames(List<string> lines)
        {
            if (lines == null || lines.Count < 2) return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i] ?? string.Empty;
                var cut = line.LastIndexOf(" ×", StringComparison.Ordinal);
                var name = cut > 0 ? line.Substring(0, cut) : line;
                if (seen.Add(name)) continue;
                lines.RemoveAt(i);
                i--;
            }
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
                // ORDER IS LOAD-BEARING: heal the replayed lines FIRST. MergeContinuedStays folds
                // same-place stays through AddCountedLine, which SUMS by name — so merging two
                // doubled stays would turn the flood into inflated counts, which is worse than the
                // flood because it looks plausible.
                log.DropReplayedLines();
                log.MergeContinuedStays();
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

        /// <summary>True once this stop's one recorded beat has been set down in the witnesses'
        /// memories — a resumed stay closed twice must not be remembered twice.</summary>
        public bool BeatDone { get; set; }

        [JsonIgnore]
        public bool HasAnyDoings =>
            BoughtValue > 0 || SoldValue > 0 || Recruited.Count > 0
            || LeftInGarrison.Count > 0 || PrisonersSold > 0 || PrisonersDonated > 0;

        /// <summary>Folds an already-counted line ("Wool ×24") into a list, merging by name.</summary>
        public static void AddCountedLine(List<string> lines, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            int cut = line.LastIndexOf(" ×", StringComparison.Ordinal);
            if (cut > 0 && int.TryParse(line.Substring(cut + 2), out int n))
                AddCounted(lines, line.Substring(0, cut), n);
            else
                AddCounted(lines, line, 1);
        }

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
