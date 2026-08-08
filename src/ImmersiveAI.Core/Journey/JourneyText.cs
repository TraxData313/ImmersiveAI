using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Journey
{
    /// <summary>
    /// The road journal in words — the block a soul riding with the player carries in their
    /// situation: the last few stops (the freshest in detail, the older ones one line each), the
    /// tasks the company carries, and the lately settled ones with their honest outcomes. First
    /// person, a witness's own account; numbers plain; sections that hold nothing are absent.
    /// </summary>
    public static class JourneyText
    {
        /// <summary>How many stops the telling shows (the freshest of them in detail).</summary>
        public const int MaxVisitsTold = 5;

        /// <summary>How many settled tasks the telling shows.</summary>
        public const int MaxResolvedTold = 3;

        /// <summary>The whole block: the road behind us, then the tasks. Empty when the journal
        /// holds nothing worth a word.</summary>
        public static string SituationBlock(JourneyLog log, double nowDay)
        {
            if (log == null) return string.Empty;

            var sb = new StringBuilder();
            var road = RoadBlock(log);
            if (road.Length > 0) sb.AppendLine(road);

            var tasks = TasksBlock(log, nowDay);
            if (tasks.Length > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine(tasks);
            }
            return sb.ToString().TrimEnd();
        }

        // ------------------------------ the stops ------------------------------

        private static string RoadBlock(JourneyLog log)
        {
            // Only stops worth a word: a named place, or doings on the road. A pass-through with
            // nothing done still shows — "we called at X" is a real memory — but empty road
            // buckets are noise.
            var told = log.Visits
                .Where(v => v != null && (v.Kind != JourneyVisit.Kinds.Road || v.HasAnyDoings))
                .ToList();
            if (told.Count == 0) return string.Empty;
            if (told.Count > MaxVisitsTold) told = told.Skip(told.Count - MaxVisitsTold).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("The road we have ridden of late, as I saw it myself:");
            for (int i = 0; i < told.Count - 1; i++)
                sb.AppendLine("- " + OneLine(told[i]));
            sb.AppendLine(Detailed(told[told.Count - 1]));
            return sb.ToString().TrimEnd();
        }

        /// <summary>An older stop in one breath: "In the town of Ortysia (Summer 12, half a day):
        /// sold for 900 denars, bought for 250; hired 2; left 3 on the walls; sold 5 captives."</summary>
        public static string OneLine(JourneyVisit v)
        {
            var sb = new StringBuilder();
            var at = AtPlace(v);
            sb.Append($"{char.ToUpperInvariant(at[0])}{at.Substring(1)} ({When(v)}{Stay(v, ", we stayed ")})");

            var doings = new List<string>();
            if (v.SoldValue > 0) doings.Add($"sold for {Denars(v.SoldValue)}");
            if (v.BoughtValue > 0) doings.Add($"bought for {Denars(v.BoughtValue)}");
            if (v.Recruited.Count > 0) doings.Add($"hired {CountOf(v.Recruited)}");
            if (v.LeftInGarrison.Count > 0) doings.Add($"left {CountOf(v.LeftInGarrison)} on the walls");
            if (v.PrisonersSold > 0) doings.Add($"sold {v.PrisonersSold} captives");
            if (v.PrisonersDonated > 0) doings.Add($"gave {v.PrisonersDonated} captives to the dungeon");
            sb.Append(doings.Count == 0 ? ": we only passed through." : ": " + string.Join(", ", doings) + ".");
            return sb.ToString();
        }

        /// <summary>The freshest stop told whole, one short paragraph.</summary>
        public static string Detailed(JourneyVisit v)
        {
            var sb = new StringBuilder();
            bool there = v.LeaveDay < 0;
            sb.Append($"Our latest stop — {AtPlace(v)}, {When(v)}");
            sb.Append(there ? ", where we are still" : Stay(v, ", where we stayed "));
            sb.AppendLine(".");

            if (v.SoldValue > 0)
                sb.AppendLine($"We sold goods worth {Denars(v.SoldValue)}{Chief(v.SoldNotable)}.");
            if (v.BoughtValue > 0)
                sb.AppendLine($"We bought for {Denars(v.BoughtValue)}{Chief(v.BoughtNotable)}.");
            if (v.Recruited.Count > 0)
                sb.AppendLine($"We took on {string.Join(", ", v.Recruited)}.");
            if (v.LeftInGarrison.Count > 0)
                sb.AppendLine($"We left {string.Join(", ", v.LeftInGarrison)} to hold the walls.");
            if (v.PrisonersSold > 0)
                sb.AppendLine($"{v.PrisonersSold} {(v.PrisonersSold == 1 ? "captive was" : "captives were")} sold to the ransom broker.");
            if (v.PrisonersDonated > 0)
                sb.AppendLine($"{v.PrisonersDonated} {(v.PrisonersDonated == 1 ? "captive was" : "captives were")} given over to the dungeon.");
            if (!v.HasAnyDoings && !there)
                sb.AppendLine("We only rested and rode on.");
            return sb.ToString().TrimEnd();
        }

        private static string AtPlace(JourneyVisit v)
        {
            if (v.Kind == JourneyVisit.Kinds.Road || string.IsNullOrWhiteSpace(v.Place))
                return "upon the road";
            var kind = v.Kind == JourneyVisit.Kinds.Castle ? "castle"
                : v.Kind == JourneyVisit.Kinds.Village ? "village" : "town";
            return $"in the {kind} of {v.Place.Trim()}";
        }

        private static string When(JourneyVisit v) =>
            string.IsNullOrWhiteSpace(v.ArriveText) ? "lately" : v.ArriveText.Trim();

        private static string Stay(JourneyVisit v, string lead)
        {
            if (v.LeaveDay < 0) return string.Empty;
            double days = Math.Max(0, v.LeaveDay - v.ArriveDay);
            if (days < 0.2) return lead + "an hour or two";
            if (days < 0.7) return lead + "half a day";
            if (days < 1.6) return lead + "a day";
            return lead + $"{(int)Math.Round(days)} days";
        }

        // "hired 2" — the men counted across the merged "Name ×N" lines.
        private static int CountOf(List<string> counted)
        {
            int total = 0;
            foreach (var line in counted)
            {
                int cut = line.LastIndexOf(" ×", StringComparison.Ordinal);
                if (cut > 0 && int.TryParse(line.Substring(cut + 2), out int n)) total += n;
                else total += 1;
            }
            return total;
        }

        private static string Chief(List<string> notable) =>
            notable == null || notable.Count == 0
                ? string.Empty
                : $" ({string.Join(", ", notable)} the chief of it)";

        private static string Denars(int value) =>
            value.ToString("#,0", CultureInfo.InvariantCulture) + " denars";

        // ------------------------------ the tasks ------------------------------

        private static string TasksBlock(JourneyLog log, double nowDay)
        {
            var sb = new StringBuilder();

            var open = log.OpenQuests;
            if (open.Count > 0)
            {
                sb.AppendLine("Tasks we carry:");
                foreach (var q in open.Skip(Math.Max(0, open.Count - 8)))
                    sb.AppendLine("- " + OpenLine(q, nowDay));
            }

            var resolved = log.ResolvedQuests;
            if (resolved.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("Lately settled:");
                foreach (var q in resolved.Skip(Math.Max(0, resolved.Count - MaxResolvedTold)))
                    sb.AppendLine($"- '{q.Title}'{ForGiver(q)} — {q.Outcome}.");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>"'An Escort for the Caravan' for Lucon (taken Summer 12; 14 days given, about
        /// 9 remain)".</summary>
        public static string OpenLine(JourneyQuest q, double nowDay)
        {
            var sb = new StringBuilder();
            sb.Append($"'{q.Title}'{ForGiver(q)}");
            var when = string.IsNullOrWhiteSpace(q.TakenText) ? null : "taken " + q.TakenText.Trim();
            string? time = null;
            if (q.DaysGiven > 0)
            {
                int remain = (int)Math.Floor(q.TakenDay + q.DaysGiven - nowDay);
                time = remain > 0
                    ? $"{q.DaysGiven} days given, about {remain} remain"
                    : $"{q.DaysGiven} days given, and the time is all but run out";
            }
            if (when != null || time != null)
                sb.Append($" ({string.Join("; ", new[] { when, time }.Where(s => s != null))})");
            return sb.ToString();
        }

        private static string ForGiver(JourneyQuest q) =>
            string.IsNullOrWhiteSpace(q.Giver) ? string.Empty : $" for {q.Giver.Trim()}";

        // ------------------------------ outcome words ------------------------------

        /// <summary>The settled-outcome words, reason folded in — fed by the game layer from the
        /// game's own completion detail, so "why" is never invented.</summary>
        public static class Outcomes
        {
            public const string Success = "succeeded";
            public const string Fail = "failed";
            public const string Timeout = "failed — the time ran out on us";
            public const string Betrayal = "failed — and by our own broken word at that";
            public const string Canceled = "set aside — the matter ended on its own";
        }
    }
}
