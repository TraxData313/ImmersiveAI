using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// The few plain facts the PLAYER's own thinking needs — and nothing else (Anton, 2026.08.10,
    /// after the thinking twice answered in the NPC's voice: "някакви лесни неща, които описват нея
    /// и мене, но ако можеш без неща от нейно лице").
    ///
    /// WHY THIS EXISTS AT ALL, and why it is not the persona sheet: the sheet is eleven thousand
    /// tokens of "I am Sibylla" in her own first person. Hung in front of a model asked for the
    /// player's line, it simply wins — no system message, no aside, no arrow outweighs that much
    /// first person. So the thinking is handed HER IN THE THIRD PERSON and ME in the first, and
    /// her own voice appears in exactly one place where it belongs: the transcript of what we have
    /// actually said to each other.
    ///
    /// It is also the honester shape. The player does not have her private mind, her moods or her
    /// misgivings to consult — only who she is, how she stands toward him, where they are, and
    /// everything the two of them have said. That is precisely what this block carries.
    /// </summary>
    public static class ThoughtFacts
    {
        /// <summary>A short factual block: who I am, who they are, what they are good at, how they
        /// stand toward me, and where we both are. Best-effort line by line — a fact that cannot be
        /// read is simply left out, never guessed at.</summary>
        public static string Build(Hero npc, Hero player, bool apart = false)
        {
            var sb = new StringBuilder();
            try
            {
                var me = player?.Name?.ToString() ?? "I";
                var them = npc?.Name?.ToString() ?? "they";

                sb.AppendLine("Who I am: " + Me(player, me));
                sb.AppendLine($"Who they are: {them} — {Bond(npc, player)}");

                var crafts = Crafts(npc);
                if (crafts.Length > 0) sb.AppendLine($"What {them} is good at: {crafts}");

                var standing = Standing(npc, player, them);
                if (standing.Length > 0) sb.AppendLine(standing);

                sb.AppendLine(Where(npc, apart, them));
            }
            catch { /* a fact that will not be read is simply not told */ }
            return sb.ToString().TrimEnd();
        }

        // ------------------------------ me ------------------------------

        private static string Me(Hero player, string me)
        {
            var parts = new List<string> { me };
            try
            {
                var clan = player?.Clan;
                if (clan != null && !string.IsNullOrWhiteSpace(clan.Name?.ToString()))
                    parts.Add(clan.Leader == player
                        ? $"head of the {clan.Name} clan"
                        : $"of the {clan.Name} clan");

                var party = MobileParty.MainParty;
                int men = party?.MemberRoster?.TotalManCount ?? 0;
                if (men > 0) parts.Add($"at the head of a company of {men}");
            }
            catch { /* the name alone will do */ }
            return string.Join(", ", parts) + ".";
        }

        // ------------------------------ them ------------------------------

        // What they are TO ME first (that is what shapes how I would speak to them), then what they
        // are in the world. Deliberately plain — a station, a duty, a house; never a character sheet.
        private static string Bond(Hero npc, Hero player)
        {
            var parts = new List<string>();
            try
            {
                if (npc == null) return "someone I know.";

                if (player != null && FamilyBuilder.AreWed(npc, player))
                    parts.Add(npc.IsFemale ? "my wife" : "my husband");

                if (npc.PartyBelongedTo != null && npc.PartyBelongedTo == MobileParty.MainParty)
                {
                    var duty = SituationBuilder.PartyDuty(npc, MobileParty.MainParty);
                    parts.Add(duty == null ? "riding in my own company" : $"the {duty} of my own company");
                }
                else if (npc.Clan != null && Clan.PlayerClan != null && npc.Clan == Clan.PlayerClan)
                    parts.Add("of my own clan");

                if (parts.Count == 0)
                {
                    if (npc.IsWanderer) parts.Add("a wanderer, free to be hired");
                    else if (npc.Clan != null && !string.IsNullOrWhiteSpace(npc.Clan.Name?.ToString()))
                        parts.Add((npc.IsFemale ? "a lady" : "a lord") + $" of the {npc.Clan.Name} clan"
                                  + (npc.MapFaction != null && npc.MapFaction != npc.Clan
                                      ? $", under the banner of {npc.MapFaction.Name}" : string.Empty));
                    else
                    {
                        var station = StationWord(npc);
                        var seat = npc.CurrentSettlement?.Name?.ToString() ?? npc.HomeSettlement?.Name?.ToString();
                        parts.Add(station + (string.IsNullOrWhiteSpace(seat) ? string.Empty : $" of {seat}"));
                    }
                }
                else if (npc.Clan != null && Clan.PlayerClan != null && npc.Clan != Clan.PlayerClan
                         && !string.IsNullOrWhiteSpace(npc.Clan.Name?.ToString()))
                    parts.Add($"of the {npc.Clan.Name} clan");
            }
            catch { /* whatever was gathered stands */ }

            return parts.Count == 0 ? "someone I know." : string.Join(", ", parts) + ".";
        }

        private static string StationWord(Hero npc)
        {
            try
            {
                switch (npc.CharacterObject?.Occupation)
                {
                    case Occupation.Merchant: return "a merchant";
                    case Occupation.Artisan: return "an artisan";
                    case Occupation.GangLeader: return "a gang leader";
                    case Occupation.RuralNotable: return "a village headman";
                    case Occupation.Preacher: return "a preacher";
                    case Occupation.Tavernkeeper: return "a tavern-keeper";
                    case Occupation.Mercenary: return "a sellsword";
                    default: return "one of the folk";
                }
            }
            catch { return "one of the folk"; }
        }

        // Their strongest hands, in the third person and briefly — three at most. The full weighing
        // is CraftsBuilder's; this is only what I would know of them from having watched them work.
        private static string Crafts(Hero npc)
        {
            try
            {
                if (npc == null) return string.Empty;
                var ranked = TaleWorlds.CampaignSystem.Extensions.Skills.All?
                    .Where(s => s != null)
                    .Select(s => new { Name = s.Name?.ToString() ?? string.Empty, Value = CraftsBuilder.ValueFor(npc, s) })
                    .Where(x => x.Name.Length > 0 && x.Value >= 75)
                    .OrderByDescending(x => x.Value)
                    .Take(3)
                    .ToList();
                if (ranked == null || ranked.Count == 0) return string.Empty;
                return string.Join("; ", ranked.Select(x => $"{CraftsBuilder.WordForValue(x.Value)} in {x.Name}")) + ".";
            }
            catch { return string.Empty; }
        }

        private static string Standing(Hero npc, Hero player, string them)
        {
            try
            {
                if (npc == null || player == null) return string.Empty;
                int r = npc.GetRelation(player);
                var word = PersonaBuilder.DescribeRelation(r);
                return $"How {them} stands toward me: {word} ({(r > 0 ? "+" : string.Empty)}{r}).";
            }
            catch { return string.Empty; }
        }

        private static string Where(Hero npc, bool apart, string them)
        {
            try
            {
                var now = SituationBuilder.Timestamp();
                if (!apart)
                    return $"Where we both are: {SituationBuilder.Place(npc)}. The hour: {now}.";

                var theirs = SituationBuilder.Place(npc);
                var mine = Hero.MainHero != null ? SituationBuilder.Place(Hero.MainHero) : string.Empty;
                return $"Where we are: I am {mine}; last word places {them} {theirs}. The hour: {now}.";
            }
            catch { return string.Empty; }
        }
    }
}
