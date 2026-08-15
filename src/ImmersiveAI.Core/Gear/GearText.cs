using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Gear
{
    /// <summary>
    /// What a soul sets down in their own mind when the player changes their gear (Anton,
    /// 2026.08.16: "tell them if I take off an item, if I add item… add the item values, cause they
    /// might not know it and its giving info on how valuable it is").
    /// <para>
    /// THE NUMBER IS THE POINT, not decoration. A companion handed a lordly hauberk has no way of
    /// knowing whether she has been given a courtesy or a fortune, and the mod's job is to hand her
    /// the figure and let her feel about it herself — never to tell her it was generous. That is the
    /// same law the nights and the leaks keep: she sees what she would see, and nothing scripts the
    /// feeling.
    /// </para>
    /// <para>
    /// The one yardstick offered beside the number is her OWN DAILY WAGE, and only when the sum is
    /// large enough that the comparison says something. It is a fact she already owns, not a
    /// judgment we are lending her.
    /// </para>
    /// </summary>
    public static class GearText
    {
        /// <summary>The permanent mark that opens every one of these beats. NEVER REWORD IT — a
        /// recorded memory keeps the phrasing it was born with forever, and this string is how the
        /// beat is recognised for the rest of a campaign's life.</summary>
        public const string GearBeatMark = "My gear is changed, and I set it down in my mind:";

        /// <summary>How many days of their own wage a sum must be worth before it is worth
        /// measuring in wages at all. Below this the number speaks for itself.</summary>
        public const int WageClauseFromDays = 30;

        public static bool IsGearBeat(string? line) =>
            !string.IsNullOrWhiteSpace(line) &&
            line!.TrimStart().StartsWith(GearBeatMark, StringComparison.Ordinal);

        /// <summary>
        /// The whole beat, or empty when nothing truly changed.
        /// </summary>
        /// <param name="set">What the visit to the inventory did.</param>
        /// <param name="playerName">Named rather than "he" — the player may be a woman.</param>
        /// <param name="wagePerDay">Their own daily wage, for the one yardstick. 0 to leave it out.</param>
        public static string Beat(GearChangeSet? set, string playerName, int wagePerDay = 0)
        {
            if (set == null || !set.Any) return string.Empty;
            var who = string.IsNullOrWhiteSpace(playerName) ? "They" : playerName.Trim();

            var clauses = new List<string>();

            // The arms go together under one heading, whatever slots they sat in.
            var arms = set.Changes.Where(c => c.Slot == GearSlot.Arms).ToList();
            if (arms.Count > 0) clauses.Add(ArmsClause(who, arms));

            foreach (var change in set.Changes.Where(c => c.Slot != GearSlot.Arms))
                clauses.Add(PieceClause(who, change));

            if (clauses.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append(GearBeatMark).Append(' ');
            sb.Append(Sentence(clauses));

            var tally = Tally(set, wagePerDay);
            if (tally.Length > 0) sb.Append(' ').Append(tally);

            // Left afoot is worth its own breath: it is the one change that alters how she travels.
            if (set.Changes.Any(c => c.Slot == GearSlot.Mount && c.WasTaken && !c.WasGiven))
                sb.Append(" I go afoot now.");

            return sb.ToString();
        }

        // ------------------------------------------------------------------

        private static string PieceClause(string who, GearChange c)
        {
            var mine = MineWord(c.Slot);
            if (c.IsSwap)
                return $"{who} put {Named(c.GivenName, c.GivenValue)} on me where {Worn(c.TakenName, c.TakenValue)} was";
            if (c.WasGiven)
                return $"{who} gave me {Named(c.GivenName, c.GivenValue)} for {mine}";
            return $"{who} took {Worn(c.TakenName, c.TakenValue)} from me";
        }

        private static string ArmsClause(string who, List<GearChange> arms)
        {
            var given = arms.Where(a => a.WasGiven).Select(a => Named(a.GivenName, a.GivenValue)).ToList();
            var taken = arms.Where(a => a.WasTaken).Select(a => Worn(a.TakenName, a.TakenValue)).ToList();

            if (given.Count > 0 && taken.Count > 0)
                return $"for arms {who} took {Listed(taken)} and gave me {Listed(given)}";
            if (given.Count > 0)
                return $"for arms {who} gave me {Listed(given)}";
            return $"for arms {who} took {Listed(taken)} from me";
        }

        /// <summary>What a person calls the place a piece sits. Weapons never reach this — see
        /// <see cref="GearSlot"/> for why the game cannot promise what a weapon slot holds.</summary>
        private static string MineWord(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Head: return "my head";
                case GearSlot.Body: return "my back";
                case GearSlot.Legs: return "my feet";
                case GearSlot.Hands: return "my hands";
                case GearSlot.Cape: return "my shoulders";
                case GearSlot.Mount: return "the road";
                case GearSlot.Harness: return "my horse";
                case GearSlot.Banner: return "my standard";
                default: return "myself";
            }
        }

        private static string Named(string name, int value) =>
            value > 0 ? $"{name} ({Denars(value)})" : name;

        private static string Worn(string name, int value) =>
            value > 0 ? $"my {name} ({Denars(value)})" : "my " + name;

        private static string Denars(int value) => $"{value:N0} denars";

        /// <summary>The reckoning of it all — but only when there is more than one piece, or when
        /// what changed hands is worth measuring. One glove for another says its own arithmetic.</summary>
        private static string Tally(GearChangeSet set, int wagePerDay)
        {
            var net = set.NetWorth;
            if (set.Changes.Count < 2 && Math.Abs(net) < 500) return string.Empty;
            if (net == 0) return "All told I am no richer and no poorer than I was.";

            var word = net > 0 ? "richer" : "poorer";
            var sum = Denars(Math.Abs(net));
            var line = $"All told I am {word} by {sum} than I was this morning";

            var wage = WageClause(Math.Abs(net), wagePerDay);
            return line + (wage.Length > 0 ? $" — {wage}." : ".");
        }

        /// <summary>The one yardstick, and it is hers: her own wage. A fact, never a judgment — the
        /// mod does not tell her she has been treated well.</summary>
        private static string WageClause(int worth, int wagePerDay)
        {
            if (wagePerDay <= 0 || worth <= 0) return string.Empty;
            var days = worth / (double)wagePerDay;
            if (days < WageClauseFromDays) return string.Empty;

            if (days >= 365) return $"near {Round(days / 365.0)} years of my wage";
            if (days >= 60) return $"near {Round(days / 30.0)} months of my wage";
            return $"near {Round(days)} days of my wage";
        }

        private static string Round(double n)
        {
            var whole = (int)Math.Round(n);
            if (whole < 1) whole = 1;
            return whole.ToString();
        }

        private static string Listed(List<string> items)
        {
            if (items.Count == 0) return string.Empty;
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.Take(items.Count - 1)) + " and " + items[items.Count - 1];
        }

        /// <summary>Joins the clauses into one sentence, capitalised, closed with a stop.</summary>
        private static string Sentence(List<string> clauses)
        {
            var body = clauses.Count == 1
                ? clauses[0]
                : string.Join("; ", clauses.Take(clauses.Count - 1)) + "; and " + clauses[clauses.Count - 1];
            body = body.Trim();
            if (body.Length == 0) return string.Empty;
            return char.ToUpperInvariant(body[0]) + body.Substring(1) + ".";
        }
    }
}
