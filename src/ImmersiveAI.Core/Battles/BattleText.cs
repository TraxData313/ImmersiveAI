using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Battles
{
    /// <summary>
    /// Every word the chronicle speaks: the forged battle-names ("The Grand Victory near Ortysia,
    /// over Thrice Our Number"), the short shared tale, the first-person beat each ally sets down in
    /// their own memory, and the full account the recall tool and the chronicle file carry. All pure
    /// prose over <see cref="BattleRecord"/> data — no game, no clock, fully testable. The voice
    /// follows the house rule: the NPC's own mind in the first person, numbers honest, no fourth wall.
    /// </summary>
    public static class BattleText
    {
        // ------------------------------ the recorded beat's marker ------------------------------
        // The word-for-word opening of every battle beat set down in an ally's memory. Views and
        // dedupe checks recognize a battle beat by this prefix — recorded memories carry the phrasing
        // they were born with forever, so NEVER reword it; add a second marker beside it instead.
        public const string BeatMark = "Battle is behind us, and I set it down in my mind:";

        /// <summary>True when a recorded inner line is a battle beat (see <see cref="BeatMark"/>).</summary>
        public static bool IsBattleBeat(string? innerLine) =>
            (innerLine ?? string.Empty).TrimStart().StartsWith(BeatMark, StringComparison.Ordinal);

        // ------------------------------ naming the battle ------------------------------

        /// <summary>The place woven into prose by the arena it was fought in: "near Ortysia",
        /// "at the walls of Varcheg", "on the waters off Ostican".</summary>
        public static string PlacePhrase(string kind, string placeName)
        {
            bool named = !string.IsNullOrWhiteSpace(placeName);
            var place = named ? placeName.Trim() : string.Empty;
            switch (kind ?? string.Empty)
            {
                case BattleRecord.Kinds.Siege: return named ? $"at the walls of {place}" : "at the walls";
                case BattleRecord.Kinds.Sally: return named ? $"before the gates of {place}" : "before the gates";
                case BattleRecord.Kinds.Sea: return named ? $"on the waters off {place}" : "on the open water";
                case BattleRecord.Kinds.Hideout: return named ? $"at the den near {place}" : "at a brigands' den";
                case BattleRecord.Kinds.Raid: return named ? $"among the fields of {place}" : "among burning fields";
                case BattleRecord.Kinds.Village: return named ? $"at {place}" : "at a village";
                default: return named ? $"near {place}" : "in the open field";
            }
        }

        /// <summary>
        /// Forges the name a battle is remembered by. The name carries what made the day worth naming:
        /// the arena (storming, holding, sally, sea-fight, den), the odds when they were long, the cost
        /// when the winning was dear, and honest words for defeat. Small clashes stay skirmishes; a
        /// great victory over long odds earns "Grand".
        /// </summary>
        public static string ForgeTitle(BattleRecord r)
        {
            bool won = r.Outcome == BattleRecord.Outcomes.Victory;
            bool lost = r.Outcome == BattleRecord.Outcomes.Defeat;
            var place = r.PlaceName?.Trim() ?? string.Empty;
            bool named = place.Length > 0;
            int ours = Math.Max(1, r.Ours?.Total ?? 1);
            int theirs = r.Theirs?.Total ?? 0;
            double odds = theirs / (double)ours;
            int scale = (r.Ours?.Total ?? 0) + theirs;
            bool costly = won && r.Ours != null && r.Ours.Total > 0
                && (r.Ours.Fallen + r.Ours.Wounded) >= (int)(0.4 * r.Ours.Total);
            var oddsTag = OddsWords(odds);

            switch (r.Kind)
            {
                case BattleRecord.Kinds.Siege:
                    if (won) return r.PlayerAttacked
                        ? (named ? $"The Storming of {place}" : "The Storming of the Walls")
                        : (named ? $"The Holding of {place}" : "The Holding of the Walls")
                          + (oddsTag != null ? $", against {oddsTag}" : string.Empty);
                    if (lost) return r.PlayerAttacked
                        ? (named ? $"The Repulse at the Walls of {place}" : "The Repulse at the Walls")
                        : (named ? $"The Fall of {place}" : "The Fall of the Walls");
                    return named ? $"The Broken Assault at {place}" : "The Broken Assault";

                case BattleRecord.Kinds.Sally:
                    if (won) return r.PlayerAttacked
                        ? (named ? $"The Great Sally from {place}" : "The Great Sally")
                        : (named ? $"The Sally Repelled at {place}" : "The Sally Repelled");
                    if (lost) return r.PlayerAttacked
                        ? (named ? $"The Sally Broken at {place}" : "The Sally Broken")
                        : (named ? $"The Sally that Burst from {place}" : "The Sally Endured");
                    return named ? $"The Sally-Fight at {place}" : "The Sally-Fight";

                case BattleRecord.Kinds.Sea:
                    if (won) return (odds >= 2 && scale >= 60 ? "The Grand Sea-Victory" : "The Sea-Victory")
                        + (named ? $" off {place}" : " on the Open Water")
                        + (oddsTag != null ? $", over {oddsTag}" : string.Empty);
                    if (lost) return named ? $"The Loss on the Waters off {place}" : "The Loss on the Open Water";
                    return named ? $"The Sea-Fight off {place}" : "The Sea-Fight on the Open Water";

                case BattleRecord.Kinds.Hideout:
                    if (won) return named ? $"The Rooting-Out of the Den near {place}" : "The Rooting-Out of the Den";
                    if (lost) return named ? $"The Repulse at the Den near {place}" : "The Repulse at the Den";
                    return named ? $"The Fight at the Den near {place}" : "The Fight at the Den";

                case BattleRecord.Kinds.Raid:
                    if (won) return r.PlayerAttacked
                        ? (named ? $"The Raid on {place}" : "The Raid")
                        : (named ? $"The Rescue of {place}" : "The Rescue of the Village");
                    if (lost) return r.PlayerAttacked
                        ? (named ? $"The Raid Broken at {place}" : "The Raid Broken")
                        : (named ? $"The Burning of {place}" : "The Burning of the Village");
                    return named ? $"The Fight among the Fields of {place}" : "The Fight among the Fields";

                case BattleRecord.Kinds.Village:
                    if (won) return r.PlayerAttacked
                        ? (named ? $"The Pressing of {place}" : "The Pressing of the Village")
                        : (named ? $"The Defense of {place}" : "The Defense of the Village");
                    if (lost) return named ? $"The Bitter Day at {place}" : "The Bitter Day at the Village";
                    return named ? $"The Fight at {place}" : "The Fight at the Village";
            }

            // The open field — where the odds and the cost do the naming.
            var at = named ? $" near {place}" : " in the Open Field";
            if (won)
            {
                if (odds >= 2.5 && scale >= 80) return $"The Grand Victory{at}, over {oddsTag}";
                if (oddsTag != null) return $"The Victory{at}, over {oddsTag}";
                if (costly) return $"The Dear-Bought Victory{at}";
                if (scale < 25) return $"The Skirmish Won{at}";
                return $"The Victory{at}";
            }
            if (lost)
            {
                if (scale < 25) return $"The Skirmish Lost{at}";
                return oddsTag != null ? $"The Bitter Day{at}, against {oddsTag}" : $"The Defeat{at}";
            }
            return scale >= 100 ? $"The Bloodied Parting{at}" : $"The Skirmish{at}";
        }

        /// <summary>Long odds put into telling words, or null when the numbers were near even.</summary>
        public static string? OddsWords(double theirsToOurs)
        {
            if (theirsToOurs >= 4.0) return "Four Times Our Number";
            if (theirsToOurs >= 3.0) return "Thrice Our Number";
            if (theirsToOurs >= 2.0) return "Twice Our Number";
            if (theirsToOurs >= 1.5) return "Half Again Our Number";
            return null;
        }

        // ------------------------------ the sides in words ------------------------------

        /// <summary>One side's muster in one honest line: "62 — 30 foot, 14 bows, 12 horse,
        /// 6 horse-archers; seasoned hands (about 2.7 of 6)".</summary>
        public static string SideLine(BattleSideStats s)
        {
            if (s == null || s.Total <= 0) return "none that were counted";
            var parts = new List<string>();
            if (s.Foot > 0) parts.Add($"{s.Foot} foot");
            if (s.Archers > 0) parts.Add($"{s.Archers} bows");
            if (s.Horse > 0) parts.Add($"{s.Horse} horse");
            if (s.HorseArchers > 0) parts.Add($"{s.HorseArchers} horse-archers");

            var sb = new StringBuilder();
            sb.Append(s.Total.ToString("#,0", CultureInfo.InvariantCulture));
            if (parts.Count > 0) sb.Append(" — ").Append(string.Join(", ", parts));
            if (s.TierAverage > 0)
                sb.Append($"; {SeasoningWord(s.TierAverage)} (about {s.TierAverage.ToString("0.#", CultureInfo.InvariantCulture)} of 6)");
            if (s.Ships > 0) sb.Append($"; {s.Ships} {(s.Ships == 1 ? "ship" : "ships")}");
            return sb.ToString();
        }

        /// <summary>The seasoning of a muster, from its average tier.</summary>
        public static string SeasoningWord(double tierAverage)
        {
            if (tierAverage < 1.5) return "green levies";
            if (tierAverage < 3.0) return "seasoned hands";
            if (tierAverage < 4.5) return "hardened fighters";
            return "an elite muster";
        }

        /// <summary>One side's cost in one line: "8 fell, 10 were wounded, 12 broke and ran".</summary>
        public static string CostClause(BattleSideStats s)
        {
            if (s == null) return "no count was kept";
            var parts = new List<string>();
            if (s.Fallen > 0) parts.Add($"{s.Fallen} fell");
            if (s.Wounded > 0) parts.Add($"{s.Wounded} were wounded");
            if (s.Routed > 0) parts.Add($"{s.Routed} broke and ran");
            if (s.ShipsLost > 0) parts.Add($"{s.ShipsLost} {(s.ShipsLost == 1 ? "ship was" : "ships were")} lost");
            return parts.Count == 0 ? "not one was lost" : string.Join(", ", parts);
        }

        // The verb of the meeting, by arena and stance — shared by the tales and the beats.
        private static string MeetingVerb(BattleRecord r)
        {
            switch (r.Kind)
            {
                case BattleRecord.Kinds.Siege:
                    return r.PlayerAttacked ? "we stormed the walls held by" : "we held the walls against";
                case BattleRecord.Kinds.Sally:
                    return r.PlayerAttacked ? "we burst from the gates upon" : "we met the sally of";
                case BattleRecord.Kinds.Sea: return "we met, on the water,";
                case BattleRecord.Kinds.Hideout: return "we fell upon the den of";
                case BattleRecord.Kinds.Raid:
                    return r.PlayerAttacked ? "we put the torch to the fields against" : "we came to the rescue against";
                default: return r.PlayerAttacked ? "we fell upon" : "we stood against";
            }
        }

        private static string OutcomeClause(BattleRecord r)
        {
            switch (r.Outcome)
            {
                case BattleRecord.Outcomes.Victory: return "and broke them";
                case BattleRecord.Outcomes.Defeat: return "and were broken";
                default: return "and parted, both sides bloodied, nothing settled";
            }
        }

        // ------------------------------ the short shared tale ------------------------------

        /// <summary>One or two sentences in the shared "we" voice — what the roll of battles shows
        /// once newer days have pushed this one down the list.</summary>
        public static string ShortTale(BattleRecord r)
        {
            var sb = new StringBuilder();
            sb.Append($"With {Math.Max(0, r.Ours?.Total ?? 0)} {MeetingVerb(r)} {FoeOrUnknown(r)}");
            if ((r.Theirs?.Total ?? 0) > 0) sb.Append($" — {r.Theirs!.Total} strong —");
            sb.Append($" {PlacePhrase(r.Kind, r.PlaceName)}, {OutcomeClause(r)}.");

            int ourLosses = (r.Ours?.Fallen ?? 0) + (r.Ours?.Wounded ?? 0);
            var second = new List<string>();
            if (ourLosses > 0) second.Add($"{ourLosses} of ours fell or bled");
            if (r.PrisonersTaken > 0) second.Add($"{r.PrisonersTaken} of theirs were led away in chains");
            if (r.CaptivesFreed > 0) second.Add($"{r.CaptivesFreed} souls they held were set free");
            if (second.Count > 0)
                sb.Append(" " + Capitalize(string.Join(", and ", second)) + ".");
            return sb.ToString();
        }

        private static string FoeOrUnknown(BattleRecord r) =>
            string.IsNullOrWhiteSpace(r.EnemyName) ? "our foes" : r.EnemyName.Trim();

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ------------------------------ the recorded beat (first person) ------------------------------

        /// <summary>
        /// The line one ally sets down in their own memory the moment the field falls quiet — short,
        /// first person, permanent: the meeting, the odds, their own hand's work beside the player's,
        /// how they came out of it, and the name the chronicle keeps. Recorded as a SILENT inner turn
        /// (like a meeting note): nothing was spoken, so nothing is fabricated.
        /// </summary>
        public static string BeatLine(BattleRecord r, BattleParticipant me, string playerName)
        {
            var sb = new StringBuilder();
            sb.Append(BeatMark);
            sb.Append($" {PlacePhrase(r.Kind, r.PlaceName)}, {(string.IsNullOrWhiteSpace(r.TimeOfDay) ? "this day" : r.TimeOfDay)}, ");
            sb.Append($"{MeetingVerb(r)} {FoeOrUnknown(r)} — ");
            sb.Append((r.Theirs?.Total ?? 0) > 0 && (r.Ours?.Total ?? 0) > 0
                ? $"{r.Theirs!.Total} of them against our {r.Ours!.Total} — "
                : string.Empty);
            sb.Append(OutcomeClause(r)).Append('.');

            // My own hand, honestly — and the player's beside it, spoken only when it did something,
            // so silence never shames anyone.
            if (me != null)
            {
                if (me.Downs > 0) sb.Append($" By my own hand I struck down {me.Downs}.");
                else if (me.Downs == 0) sb.Append(" None fell to my own hand that day — my part lay elsewhere.");
                else sb.Append(" Of single hands no tally was kept in that press.");
            }
            var player = r.Player;
            if (player != null && (me == null || !me.IsPlayer) && player.Downs > 0)
                sb.Append($" {playerName} felled {player.Downs}.");

            if (me != null && !me.IsPlayer)
            {
                switch (me.State)
                {
                    case BattleParticipant.States.Wounded:
                        sb.Append(" I came out of it wounded — my body still mends."); break;
                    case BattleParticipant.States.Captured:
                        sb.Append(" The day ended with me in their hands, a captive."); break;
                    case BattleParticipant.States.Fell:
                        sb.Append(" I was carried from the field, and my part in such days is ended."); break;
                    default:
                        sb.Append(" I came through unhurt."); break;
                }
            }

            // The player's own fate is part of what an ally remembers of the day.
            if (player != null && (me == null || !me.IsPlayer))
            {
                if (player.State == BattleParticipant.States.Wounded)
                    sb.Append($" {playerName} was borne from the field wounded.");
                else if (player.State == BattleParticipant.States.Captured)
                    sb.Append($" {playerName} was taken captive as the day closed.");
            }

            sb.Append($" The chronicle keeps it as '{r.Title}'.");
            return sb.ToString();
        }

        // ------------------------------ the full account ------------------------------

        /// <summary>
        /// The whole battle told compactly — the account the recall tool answers with, the chronicle
        /// file keeps, and the situation carries for the freshest shared battle. Shared "we" voice;
        /// every number the game's own. Sections that hold nothing are simply absent.
        /// </summary>
        public static string FullAccount(BattleRecord r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"'{r.Title}' — {r.DateText}{(string.IsNullOrWhiteSpace(r.TimeOfDay) ? "" : ", " + r.TimeOfDay)}.");
            sb.AppendLine($"{Capitalize(MeetingVerb(r))} {FoeOrUnknown(r)} {PlacePhrase(r.Kind, r.PlaceName)}; the attack was {(r.PlayerAttacked ? "ours" : "theirs")}.");
            sb.AppendLine($"Ours: {SideLine(r.Ours)}.");
            sb.AppendLine($"Theirs: {SideLine(r.Theirs)}.");

            switch (r.Outcome)
            {
                case BattleRecord.Outcomes.Victory: sb.AppendLine("The day was ours."); break;
                case BattleRecord.Outcomes.Defeat: sb.AppendLine("The day was theirs."); break;
                default: sb.AppendLine("Neither side could claim the day."); break;
            }

            sb.AppendLine($"The cost — ours: {CostClause(r.Ours)}. Theirs: {CostClause(r.Theirs)}.");

            var chains = new List<string>();
            if (r.PrisonersTaken > 0) chains.Add($"we led away {r.PrisonersTaken} prisoners");
            if (r.CaptivesFreed > 0) chains.Add($"we struck the chains from {r.CaptivesFreed} souls they had held");
            if (chains.Count > 0) sb.AppendLine(Capitalize(string.Join(", and ", chains)) + ".");

            var deeds = DeedsLine(r);
            if (deeds.Length > 0) sb.AppendLine(deeds);

            var spoils = SpoilsLine(r.Loot);
            if (spoils.Length > 0) sb.AppendLine(spoils);

            var purse = new List<string>();
            if (r.GoldGained > 0) purse.Add($"{r.GoldGained.ToString("#,0", CultureInfo.InvariantCulture)} denars of plunder");
            if (r.RenownGained > 0) purse.Add($"renown some {r.RenownGained}");
            if (r.InfluenceGained > 0) purse.Add($"influence {r.InfluenceGained}");
            if (purse.Count > 0) sb.AppendLine("The purse and the name: " + string.Join(", ", purse) + ".");

            return sb.ToString().TrimEnd();
        }

        // Named hands and fates: the players of the day, by their deeds.
        private static string DeedsLine(BattleRecord r)
        {
            if (r.Participants == null || r.Participants.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            var strikers = r.Participants
                .Where(p => p != null && p.Downs > 0)
                .OrderByDescending(p => p.Downs)
                .Take(5)
                .ToList();
            if (strikers.Count > 0)
                sb.Append("Deeds of note: " + string.Join("; ", strikers.Select(p => $"{p.Name} struck down {p.Downs}")) + ".");

            var wounded = r.Participants.Where(p => p != null && p.State == BattleParticipant.States.Wounded).Select(p => p.Name).ToList();
            if (wounded.Count > 0)
                sb.Append((sb.Length > 0 ? " " : "") + JoinNames(wounded) + $" came out of it wounded.");

            var fell = r.Participants.Where(p => p != null && p.State == BattleParticipant.States.Fell).Select(p => p.Name).ToList();
            if (fell.Count > 0)
                sb.Append((sb.Length > 0 ? " " : "") + JoinNames(fell) + $" fell that day.");

            var captured = r.Participants.Where(p => p != null && p.State == BattleParticipant.States.Captured).Select(p => p.Name).ToList();
            if (captured.Count > 0)
                sb.Append((sb.Length > 0 ? " " : "") + JoinNames(captured) + $" {(captured.Count == 1 ? "was" : "were")} taken captive.");

            return sb.ToString();
        }

        private static string JoinNames(List<string> names)
        {
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return names[0] + " and " + names[1];
            return string.Join(", ", names.Take(names.Count - 1)) + ", and " + names[names.Count - 1];
        }

        /// <summary>The spoils in one breath: total worth, the kinds, the richest and the most
        /// numerous pieces. Empty string when the day yielded nothing.</summary>
        public static string SpoilsLine(BattleLoot loot)
        {
            if (loot == null || loot.IsEmpty) return string.Empty;

            var sb = new StringBuilder();
            sb.Append($"Spoils worth some {loot.TotalValue.ToString("#,0", CultureInfo.InvariantCulture)} denars in all");
            if (loot.Categories.Count > 0)
                sb.Append(" — " + string.Join(", ", loot.Categories.Select(c => $"{c.Count} {c.Category}")));
            sb.Append('.');

            if (loot.TopValued.Count > 0)
                sb.Append(" Richest: " + string.Join(", ", loot.TopValued.Select(
                    l => $"{l.Name} ({l.UnitValue.ToString("#,0", CultureInfo.InvariantCulture)}{(l.Count > 1 ? " each, ×" + l.Count : "")})")) + ".");
            if (loot.TopNumerous.Count > 0)
                sb.Append(" Most numerous: " + string.Join(", ", loot.TopNumerous.Select(
                    l => $"{l.Name} (×{l.Count})")) + ".");
            return sb.ToString();
        }

        // ------------------------------ the roll and the situation block ------------------------------

        /// <summary>One line of the roll of battles: the name, the day, the short tale.</summary>
        public static string RollEntry(BattleRecord r) =>
            $"'{r.Title}' — {r.DateText}: {(string.IsNullOrWhiteSpace(r.ShortTale) ? ShortTale(r) : r.ShortTale)}";

        /// <summary>
        /// The chronicle as one soul's situation carries it: the roll of battles lived at the player's
        /// side (titles and short tales, newest last, the oldest folded into a count), and the full
        /// account of the freshest one — so the last battle is discussable in detail unprompted, and
        /// the older ones by name. First person, hers.
        /// </summary>
        public static string SituationBlock(IReadOnlyList<BattleRecord> shared, string playerName, bool mentionRecall, int maxListed = 6)
        {
            if (shared == null || shared.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"Battles I have lived through at {playerName}'s side, as the chronicle keeps them:");

            int skipped = Math.Max(0, shared.Count - Math.Max(1, maxListed));
            if (skipped > 0)
                sb.AppendLine($"- …and before these, {skipped} earlier {(skipped == 1 ? "battle" : "battles")} besides.");
            for (int i = skipped; i < shared.Count - 1; i++)
                sb.AppendLine("- " + RollEntry(shared[i]));

            var latest = shared[shared.Count - 1];
            sb.AppendLine($"Of the last of them, the full tale still fresh in my mind:");
            sb.AppendLine(FullAccount(latest));
            if (mentionRecall && shared.Count > 1)
                sb.AppendLine("Any of the older ones I may call back whole, by its name, when talk turns to it.");
            return sb.ToString().TrimEnd();
        }
    }
}
