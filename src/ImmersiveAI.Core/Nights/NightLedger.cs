using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ImmersiveAI.Core.Nights
{
    /// <summary>
    /// The book of nights (2026.08.09): every wife's own rolling fortnight of them — who came to
    /// her, whose door was closed, what she learned of the nights he was elsewhere, and the nights
    /// she never learned anything at all.
    ///
    /// Deliberately SHORT-MEMORIED, and that is the design: a fortnight back is exactly enough for
    /// a woman to judge how the last two weeks of her marriage went, and no more than the situation
    /// block can carry without swelling. What outlives the fortnight is what memory keeps anyway —
    /// the NAME of a night that was written down, set as a beat the evening it happened.
    ///
    /// Lives as <c>_nights.json</c> in the campaign folder (so the save-scoped snapshots rewind the
    /// nights with the memories) beside an append-only <c>nights.txt</c> of every account in full,
    /// which is the player's own keepsake and is never pruned.
    /// </summary>
    public sealed class NightLedger
    {
        /// <summary>How many nights each wife's roll keeps. The player's own dial rides over this
        /// (<c>MaxNightsRemembered</c>); this is the default the file is pruned to.</summary>
        public const int DefaultMaxPerWife = 14;

        /// <summary>How many written nights are shown in full before the rest fold to their names.</summary>
        public const int DefaultStoriesInFull = 5;

        /// <summary>How many times a night's account is asked for before we let it be.</summary>
        public const int MaxStoryAttempts = 3;

        public List<NightRecord> Nights { get; set; } = new List<NightRecord>();

        /// <summary>How many DAYS of thin marks are kept beside the records. Generous on purpose —
        /// they cost almost nothing and the reckoning wants a month of them (see NightMark).</summary>
        public const int DefaultMarkDays = 90;

        /// <summary>The thin trace of every evening, outliving the records it was made beside.
        /// A file written before this existed simply has none, and is seeded from what it does
        /// have the first time it is loaded.</summary>
        public List<NightMark> Marks { get; set; } = new List<NightMark>();

        /// <summary>The last evening that was actually settled one way or the other — chosen, spent,
        /// or honestly slept through alone. It is a NIGHT-level mark and not a per-wife one on
        /// purpose: a night where nobody happened to notice anything writes no records at all, and
        /// without this the sweep would keep re-closing it forever. -1 before the first evening.</summary>
        public double LastSettledNight { get; set; } = -1;

        /// <summary>Whether the evening of <paramref name="gameDay"/> has already been settled.</summary>
        public bool IsNightSettled(double gameDay) =>
            LastSettledNight >= 0 && Math.Floor(LastSettledNight) >= Math.Floor(gameDay);

        /// <summary>Marks an evening settled. Never walks backwards.</summary>
        public void SettleNight(double gameDay)
        {
            if (gameDay > LastSettledNight) LastSettledNight = gameDay;
        }

        // ------------------------------ writing ------------------------------

        /// <summary>Sets a night down and prunes that wife's roll back to <paramref name="maxPerWife"/>.
        /// A night already recorded for this wife on this day is REPLACED, not doubled — the evening
        /// tick can fire twice across a save, and a fortnight of duplicates would be a lie.</summary>
        public NightRecord Add(NightRecord record, int maxPerWife = DefaultMaxPerWife)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(record.Id)) record.Id = MakeId(record);

            var existing = Nights.FirstOrDefault(n => n != null
                && string.Equals(n.WifeId, record.WifeId, StringComparison.Ordinal)
                && SameNight(n.GameDay, record.GameDay));
            if (existing != null) Nights.Remove(existing);

            Nights.Add(record);
            Mark(record);
            PruneFor(record.WifeId, maxPerWife);
            return record;
        }

        /// <summary>
        /// Sets down a SECOND record for a wife on a night that already has one — her door being
        /// closed AND what she then saw of where he slept are two true things about one evening.
        /// It goes through the same door as any other so it gets an id and obeys the cap; adding
        /// straight to the list (as this once did) left it id-less, which quietly broke the
        /// "which written nights are told in full" grouping, and unpruned, so a woman in her
        /// custom days every month grew past her fortnight forever.
        /// </summary>
        public NightRecord AddBeside(NightRecord record, int maxPerWife = DefaultMaxPerWife)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(record.Id)) record.Id = MakeId(record) + "_b";
            Nights.Add(record);
            Mark(record);
            PruneFor(record.WifeId, maxPerWife);
            return record;
        }

        /// <summary>
        /// Forgets everything set down about one evening. The evening can be settled twice — the
        /// dusk question passes and every wife notes a night alone, and then the player opens the
        /// window at midnight and actually goes to one of them. Without this the others would still
        /// be remembering that he slept alone on the very night he did not (2026.08.10).
        /// </summary>
        public void ClearNight(double gameDay)
        {
            Nights.RemoveAll(n => n != null && SameNight(n.GameDay, gameDay));
            Marks.RemoveAll(m => m != null && SameNight(m.GameDay, gameDay));
        }

        /// <summary>Leaves the thin trace of an evening, replacing the one already there for that
        /// wife and that kind of night — an evening settled twice must not be counted twice.</summary>
        private void Mark(NightRecord record)
        {
            if (record == null) return;
            Marks.RemoveAll(m => m != null && m.IsSameEveningAs(record));
            Marks.Add(NightMark.From(record));
            PruneMarks(record.GameDay);
        }

        /// <summary>Lets go of marks older than <see cref="DefaultMarkDays"/>. Pruned by DAYS and
        /// never by count, because the reckoning asks "these last thirty days" and a busy month
        /// must not push a quiet one out of reach.</summary>
        public void PruneMarks(double today, int keepDays = DefaultMarkDays)
        {
            if (keepDays <= 0) return;
            double cutoff = today - keepDays;
            Marks.RemoveAll(m => m == null || m.GameDay < cutoff);
        }

        /// <summary>Re-leaves the trace of an evening whose record has since learned something —
        /// the name and the price of the other night reach us MINUTES after the mark was made, when
        /// the chronicler finally answers, so without this the reckoning would never hear of them.</summary>
        public void RefreshMark(NightRecord record)
        {
            if (record == null) return;
            Marks.RemoveAll(m => m != null && m.IsSameEveningAs(record));
            Marks.Add(NightMark.From(record));
        }

        /// <summary>Her thin marks, oldest first — what the reckoning counts.</summary>
        public IReadOnlyList<NightMark> MarksFor(string wifeId) =>
            Marks.Where(m => m != null && string.Equals(m.WifeId, wifeId, StringComparison.Ordinal))
                .OrderBy(m => m.GameDay).ToList();

        /// <summary>Whether this wife already has a night set down for this evening.</summary>
        public bool HasNightOn(string wifeId, double gameDay) =>
            Nights.Any(n => n != null
                && string.Equals(n.WifeId, wifeId, StringComparison.Ordinal)
                && SameNight(n.GameDay, gameDay));

        private static bool SameNight(double a, double b) => Math.Floor(a) == Math.Floor(b);

        private static string MakeId(NightRecord record)
        {
            var who = string.IsNullOrWhiteSpace(record.WifeId) ? "unknown" : record.WifeId.Trim();
            return $"d{(int)record.GameDay}_{who}";
        }

        private void PruneFor(string wifeId, int maxPerWife)
        {
            if (maxPerWife <= 0) maxPerWife = DefaultMaxPerWife;
            var hers = Nights.Where(n => n != null && string.Equals(n.WifeId, wifeId, StringComparison.Ordinal))
                .OrderBy(n => n.GameDay).ToList();
            for (int i = 0; i < hers.Count - maxPerWife; i++) Nights.Remove(hers[i]);
        }

        // ------------------------------ reading ------------------------------

        /// <summary>Her roll, oldest first — what the situation block and the window both draw on.</summary>
        public IReadOnlyList<NightRecord> For(string wifeId) =>
            Nights.Where(n => n != null && string.Equals(n.WifeId, wifeId, StringComparison.Ordinal))
                .OrderBy(n => n.GameDay).ToList();

        /// <summary>The last night he actually came to her; null when he never has.</summary>
        public NightRecord? LastTogether(string wifeId) =>
            For(wifeId).LastOrDefault(n => n.Kind == NightKind.Together);

        /// <summary>The last night the player spent with ANYONE — what the cooldown is counted from,
        /// since a man cannot be in two beds in one evening.</summary>
        public NightRecord? LastTogetherWithAnyone() =>
            Nights.Where(n => n != null && n.Kind == NightKind.Together)
                .OrderBy(n => n.GameDay).LastOrDefault();

        /// <summary>Nights still owed an account, freshest first — the hourly retry's work list.</summary>
        public IReadOnlyList<NightRecord> AwaitingStories(int maxAttempts = MaxStoryAttempts) =>
            Nights.Where(n => n != null && n.NeedsStory(maxAttempts))
                .OrderByDescending(n => n.GameDay).ToList();

        /// <summary>Conceptions whose day of knowing has come.</summary>
        public IReadOnlyList<NightRecord> DueForReveal(double today) =>
            Nights.Where(n => n != null && n.AwaitsReveal(today))
                .OrderBy(n => n.GameDay).ToList();

        /// <summary>Whether a conception of this wife's is already waiting to be known — so a
        /// second one is never rolled on top of the first while the world still knows nothing.</summary>
        public bool HasPendingConception(string wifeId) =>
            Nights.Any(n => n != null && n.Conceived && !n.Revealed
                && string.Equals(n.WifeId, wifeId, StringComparison.Ordinal));

        /// <summary>Nights owed a beat in her memory, oldest first.</summary>
        public IReadOnlyList<NightRecord> AwaitingBeats() =>
            Nights.Where(n => n != null && !n.BeatDone && n.Kind == NightKind.Together)
                .OrderBy(n => n.GameDay).ToList();

        // ------------------------------ persistence ------------------------------

        /// <summary>Loads the book; a missing or unreadable file is an empty one, never an error.</summary>
        public static NightLedger LoadFrom(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return new NightLedger();
                var ledger = JsonConvert.DeserializeObject<NightLedger>(File.ReadAllText(filePath));
                if (ledger == null) return new NightLedger();
                ledger.Nights = ledger.Nights?.Where(n => n != null).ToList() ?? new List<NightRecord>();
                ledger.Marks = ledger.Marks?.Where(m => m != null).ToList() ?? new List<NightMark>();

                // A book written before the marks existed still has its records: seed the trace
                // from them once, so a marriage already under way does not start its reckoning at
                // zero. What was already pruned is simply gone, and honestly so.
                if (ledger.Marks.Count == 0 && ledger.Nights.Count > 0)
                    foreach (var night in ledger.Nights.OrderBy(n => n.GameDay))
                        ledger.Marks.Add(NightMark.From(night));

                return ledger;
            }
            catch { return new NightLedger(); }
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

        /// <summary>Appends a written night to the readable keepsake. Only nights with an account
        /// are kept there — the plain ones live in the roll and nowhere else.</summary>
        public static void AppendReadable(string filePath, NightRecord record)
        {
            if (string.IsNullOrWhiteSpace(filePath) || record == null || !record.IsStoried) return;
            try
            {
                var folder = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.AppendAllText(filePath, NightText.KeepsakeEntry(record) + Environment.NewLine + Environment.NewLine);
            }
            catch { /* a keepsake that cannot be written must never cost the night itself */ }
        }
    }
}
