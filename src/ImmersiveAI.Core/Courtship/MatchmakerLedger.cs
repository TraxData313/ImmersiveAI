using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// The matchmaker's ledger — the persona-spark's sibling: a one-time plain utility LLM call
    /// that writes a soul's 2–4 PRIVATE requirements for the one they would wed, each tied where
    /// possible to a predicate the game can truly answer (gold, renown, a skill, a trait, her own
    /// regard) and at least one left to the heart alone. Grounded in her story, nature, spark, and
    /// the keeper's world text — exactly the "real woman" ask: she KNOWS them, she never recites
    /// them; they surface only as talk turns that way. The game layer owns when to call it and
    /// where the asks land (NpcMemory.CourtshipAsks).
    /// </summary>
    public static class MatchmakerLedger
    {
        public const int MaxAsks = 4;
        public const int MinAsks = 2;

        /// <summary>Everything the matchmaker is told. All strings optional except the names —
        /// empty ones leave their line out of the prompt.</summary>
        public sealed class Facts
        {
            public string Name = string.Empty;
            /// <summary>"woman" / "man".</summary>
            public string GenderWord = string.Empty;
            public int Age;
            /// <summary>Her station in the world, e.g. "a Sturgian wanderer riding as a companion".</summary>
            public string Station = string.Empty;
            /// <summary>Her station tier rung (see <see cref="CourtshipRoad.StationTier"/>) — scales the wealth guidance.</summary>
            public int StationTier = 1;
            public string Traits = string.Empty;
            /// <summary>The story the world tells of them (backstory / encyclopedia).</summary>
            public string Backstory = string.Empty;
            /// <summary>What they hold true of themselves (custom instructions — the spark included).</summary>
            public string SelfText = string.Empty;
            /// <summary>The player's name — the one being courted toward.</summary>
            public string PartnerName = string.Empty;
            /// <summary>The story already shared with the player, as she remembers it (summary + held truths).</summary>
            public string SharedStory = string.Empty;
            /// <summary>The keeper's world text (+ roleplay guidance), so the world's flavor colours the asks.</summary>
            public string WorldText = string.Empty;
        }

        /// <summary>Wealth-guidance band for a station rung — a hint to the matchmaker, never a rail.</summary>
        public static (int Low, int High) GoldBand(int stationTier)
        {
            switch (stationTier <= 1 ? 1 : stationTier >= 6 ? 6 : stationTier)
            {
                case 1: return (500, 5000);
                case 2: return (2000, 20000);
                case 3: return (10000, 50000);
                case 4: return (25000, 100000);
                case 5: return (50000, 200000);
                default: return (100000, 500000);
            }
        }

        /// <summary>
        /// Builds the matchmaker's prompt — a plain meta utility call (like the casting director),
        /// NOT the NPC's own mind; the OUTPUT is in her first-person voice and lands in her sheet's
        /// private ledger forever.
        /// </summary>
        public static string BuildPrompt(Facts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var she = string.IsNullOrWhiteSpace(facts.GenderWord) ? "they" : (facts.GenderWord == "woman" ? "she" : "he");
            var her = she == "she" ? "her" : she == "he" ? "his" : "their";
            var herself = she == "she" ? "herself" : she == "he" ? "himself" : "themselves";
            var band = GoldBand(facts.StationTier);

            var sb = new StringBuilder();
            sb.AppendLine("You are the quiet matchmaker of a living medieval world — the one who knows what each soul would truly ask of a marriage, even when they have never said it aloud. A soul of this world has begun to walk the road of courtship, and you set down the few private requirements " + her + " heart holds for the one " + she + " would wed.");
            sb.AppendLine();
            sb.AppendLine("The soul:");

            var head = new StringBuilder();
            head.Append("- ").Append(facts.Name.Trim());
            if (!string.IsNullOrWhiteSpace(facts.GenderWord)) head.Append(" — ").Append(facts.GenderWord.Trim());
            if (facts.Age > 0) head.Append(", about ").Append(facts.Age);
            if (!string.IsNullOrWhiteSpace(facts.Station)) head.Append(", ").Append(facts.Station.Trim());
            head.Append('.');
            sb.AppendLine(head.ToString());

            if (!string.IsNullOrWhiteSpace(facts.Traits))
                sb.AppendLine($"- {Cap(her)} cast of mind, from the world's reckoning: {facts.Traits.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.Backstory))
                sb.AppendLine($"- {Cap(her)} story, as the world tells it: \"{facts.Backstory.Trim()}\"");
            if (!string.IsNullOrWhiteSpace(facts.SelfText))
                sb.AppendLine($"- What {she} holds true of {herself}: \"{facts.SelfText.Trim()}\"");
            if (!string.IsNullOrWhiteSpace(facts.SharedStory) && !string.IsNullOrWhiteSpace(facts.PartnerName))
                sb.AppendLine($"- The story {she} already shares with {facts.PartnerName.Trim()}, as {she} remembers it: \"{facts.SharedStory.Trim()}\"");

            if (!string.IsNullOrWhiteSpace(facts.WorldText))
            {
                sb.AppendLine();
                sb.AppendLine($"The world {she} lives in, as its keeper wrote it: \"{facts.WorldText.Trim()}\"");
            }

            sb.AppendLine();
            sb.AppendLine($"Write {MinAsks} to {MaxAsks} lines, each one requirement, EXACTLY in this shape:");
            sb.AppendLine("ASK: <the requirement in " + her + " own first-person voice, one sentence> | CHECK: <check>");
            sb.AppendLine("where <check> is exactly one of:");
            sb.AppendLine("  gold >= N          (wealth the suitor must hold, in denars)");
            sb.AppendLine("  renown >= N        (how far the suitor's name must have traveled; 50 known nearby, 200 known in the realm, 800 famed everywhere)");
            sb.AppendLine("  skill <Name> >= N  (a craft of the world: One-Handed, Two-Handed, Polearm, Bow, Crossbow, Throwing, Riding, Athletics, Smithing, Scouting, Tactics, Roguery, Charm, Leadership, Trade, Steward, Medicine, Engineering; 50 able, 100 strong, 150 masterly)");
            sb.AppendLine("  trait <Honor|Valor|Mercy|Generosity|Calculating> >= N   (N from -2 to 2; 1 = plainly so, 2 = famed for it)");
            sb.AppendLine("  relation >= N      (how warmly " + she + " " + herself + " must have come to regard the suitor, 0..100; 40 true warmth, 60 deep trust)");
            sb.AppendLine("  heart              (what no ledger can weigh — only " + her + " own heart can judge it in the living talks)");
            sb.AppendLine();
            sb.AppendLine($"Rules: at least one line must be CHECK: heart. Numbers must fit {her} station (for one of {her} standing, wealth asks land between {band.Low} and {band.High} denars — most souls ask nearer the low end). The asks must grow out of HER OWN story, nature, and world above — concrete and hers alone, never a generic list; no two asks weigh the same thing. Output only the lines themselves, nothing else.");

            return sb.ToString().TrimEnd();
        }

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static readonly Regex AskLine = new Regex(
            @"^\s*(?:[-*\d\.\)]+\s*)?ASK\s*:\s*(?<ask>.+?)\s*\|\s*CHECK\s*:\s*(?<check>.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Reads the ledger's answer into asks. Lenient per line (list bullets and numbering are
        /// shed; a line without the ASK/CHECK shape is skipped), capped at <see cref="MaxAsks"/>.
        /// Returns an empty list when nothing usable came back — the caller then simply asks the
        /// matchmaker again another day (the spark's retry semantics).
        /// </summary>
        public static List<CourtshipAsk> ParseAsks(string? raw)
        {
            var asks = new List<CourtshipAsk>();
            if (string.IsNullOrWhiteSpace(raw)) return asks;

            foreach (var line in raw!.Split('\n'))
            {
                var m = AskLine.Match(line.TrimEnd('\r'));
                if (!m.Success) continue;
                var text = m.Groups["ask"].Value.Trim().Trim('"', '“', '”');
                var check = m.Groups["check"].Value.Trim().Trim('.', '"');
                if (text.Length == 0) continue;
                asks.Add(new CourtshipAsk { Text = text, Check = check });
                if (asks.Count >= MaxAsks) break;
            }
            return asks;
        }
    }
}
