using System;
using System.Text;
using ImmersiveAI.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Builds a lean persona from live game data. Deliberately small: the goal is a
    /// distinct character voice plus only the facts that matter to THIS conversation,
    /// not a dump of everything the game knows.
    /// </summary>
    public static class PersonaBuilder
    {
        // Deterministically assigned per NPC so every character keeps a distinct,
        // stable voice across sessions â€” a primary anti-repetition lever.
        private static readonly string[] SpeechStyles =
        {
            "Terse and blunt; short sentences, dry wit, no flattery.",
            "Warm and talkative; fond of small anecdotes and proverbs.",
            "Formal and courtly; precise words, never vulgar, subtle irony.",
            "Rough soldier's speech; earthy metaphors, occasional dark humor.",
            "Soft-spoken and thoughtful; pauses to weigh words, asks questions back.",
            "Boastful and loud; exaggerates own deeds, quick to laugh.",
            "Suspicious and guarded; answers narrowly, probes for motives.",
            "Cheerful merchant's patter; quick, practical, always angling for advantage.",
            "Old and weary; speaks slowly, references the past, gives unasked-for advice.",
            "Pious and solemn; invokes the heavens, moralizes gently.",
            "Sharp and impatient; interrupts pleasantries, wants the point.",
            "Playful and teasing; answers with jokes first, substance second.",
        };

        public static NpcPersona Build(Hero npc)
        {
            var persona = new NpcPersona
            {
                Name = npc.Name?.ToString() ?? "Unknown",
                RoleDescription = BuildRole(npc),
                PersonalityDescription = BuildPersonality(npc),
                SpeechStyle = PickSpeechStyle(npc),
            };
            return persona;
        }

        private static string PickSpeechStyle(Hero npc)
        {
            var id = npc.StringId ?? npc.Name?.ToString() ?? "";
            int hash = 17;
            foreach (var c in id) hash = unchecked(hash * 31 + c);
            return SpeechStyles[Math.Abs(hash) % SpeechStyles.Length];
        }

        private static string BuildRole(Hero npc)
        {
            var sb = new StringBuilder();
            var culture = npc.Culture?.Name?.ToString();
            if (npc.IsLord)
                sb.Append($"A {culture} noble");
            else if (npc.IsWanderer)
                sb.Append($"A {culture} wanderer and sellsword");
            else if (npc.IsMerchant)
                sb.Append($"A {culture} merchant");
            else
                sb.Append(OccupationHead(npc, culture));

            if (npc.Clan != null)
                sb.Append($" of clan {npc.Clan.Name}");
            if (npc.MapFaction != null && npc.Clan?.Kingdom != null)
                sb.Append($", sworn to {npc.Clan.Kingdom.Name}");

            // Gender and years belong to the opening thought of who I am (moved up from the situation
            // block, 2026.07.11 — Anton's ask), so she never meets herself mid-page.
            try
            {
                var gender = npc.IsFemale ? "a woman" : "a man";
                int age = (int)npc.Age;
                sb.Append(age > 0 ? $" — {gender} of some {age} years" : $" — {gender}");
            }
            catch { /* the role stands without it */ }
            sb.Append('.');

            // What their trade means they truly KNOW — one sentence, so an artisan can counsel on
            // workshops and a tavern-keeper on hirelings without a rulebook stapled to their head:
            // the basics live here, the rest they reason out or go and look up (seek_wisdom).
            var trade = TradeKnowledge(npc);
            if (trade.Length > 0) sb.Append(' ').Append(trade);

            // The standing toward the player deliberately does NOT ride here: the situation block
            // (SituationBuilder.DescribeOther) speaks it once, beside the person it belongs to, so the
            // sheet never tells her the same heart twice in two places.
            return sb.ToString();
        }

        // A warmer station word for the folk the head cases above don't cover, from the live occupation.
        private static string OccupationHead(Hero npc, string culture)
        {
            try
            {
                switch (npc.Occupation)
                {
                    case Occupation.Tavernkeeper: return $"A {culture} tavern-keeper";
                    case Occupation.RansomBroker: return $"A {culture} ransom broker";
                    case Occupation.Artisan: return $"A {culture} artisan";
                    case Occupation.GangLeader: return $"A {culture} leader among the streets";
                    case Occupation.Preacher: return $"A {culture} preacher";
                    case Occupation.Headman: return $"A {culture} village headman";
                    case Occupation.RuralNotable: return $"A {culture} elder of the countryside";
                    case Occupation.ArenaMaster: return $"A {culture} master of the arena";
                    case Occupation.Blacksmith:
                    case Occupation.Weaponsmith:
                    case Occupation.Armorer: return $"A {culture} smith";
                    case Occupation.GoodsTrader:
                    case Occupation.HorseTrader: return $"A {culture} trader";
                    case Occupation.Musician: return $"A {culture} musician";
                    case Occupation.Mercenary: return $"A {culture} sellsword";
                }
            }
            catch { /* the plain word serves */ }
            return $"A {culture} character";
        }

        // The working knowledge a station carries — short, an anchor to reason from, never a lecture.
        private static string TradeKnowledge(Hero npc)
        {
            try
            {
                bool caravan = false;
                try { caravan = npc.PartyBelongedTo?.IsCaravan == true; } catch { }
                if (caravan && npc.PartyBelongedTo?.LeaderHero == npc)
                    return "The roads are my ledger: what sells where, what the passage costs in days and dangers, " +
                           "and what a caravan must carry to come home richer than it left.";

                switch (npc.Occupation)
                {
                    case Occupation.Tavernkeeper:
                        return "All the town's talk passes my counter: who is hiring, who is for hire and what " +
                               "they are good for — a keen-eyed scout, a learned healer, a steady hand with stores — " +
                               "and where work and trouble are both to be found.";
                    case Occupation.RansomBroker:
                        return "My trade is captives and their prices: ransoms brokered between foes, prisoners " +
                               "bought and sold, and the worth of a man in chains reckoned to the denar.";
                    case Occupation.Artisan:
                        return "My trade is the workshop: the making and selling of goods, what a workshop costs " +
                               "to begin and what it returns, and which wares a town truly hungers for.";
                    case Occupation.Merchant:
                        return "Trade is my blood: caravans and workshops, the roads and their risks, what such " +
                               "ventures cost to mount and what they return when they are run well.";
                    case Occupation.ArenaMaster:
                        return "The arena is mine: the fighters, the wagers, the tourneys and their prizes.";
                    case Occupation.Blacksmith:
                    case Occupation.Weaponsmith:
                    case Occupation.Armorer:
                        return "Steel is my trade: the forging and mending of arms and harness, and the worth of " +
                               "a blade at a glance.";
                    case Occupation.Headman:
                    case Occupation.RuralNotable:
                        return "The village's needs are mine to carry: its livelihood, its levies, and its " +
                               "standing with the lords who hold the land." + VillageLivelihood(npc);
                }
            }
            catch { /* no trade line */ }
            return string.Empty;
        }

        // What the village TRULY lives by, read from its real production — an iron-digging village's
        // headman must never be handed "fields and herds" (the Cadugan playtest find, 2026.07.12:
        // asked what Beglomuar specializes in, he answered grain because the old line said fields).
        private static string VillageLivelihood(Hero npc)
        {
            try
            {
                var village = (npc.CurrentSettlement ?? npc.HomeSettlement)?.Village;
                var type = village?.VillageType;
                var primary = type?.PrimaryProduction?.Name?.ToString();
                if (string.IsNullOrWhiteSpace(primary)) return string.Empty;

                var others = new System.Collections.Generic.List<string>();
                try
                {
                    foreach (var (item, _) in type.Productions)
                    {
                        var n = item?.Name?.ToString();
                        if (string.IsNullOrWhiteSpace(n) || n == primary) continue;
                        if (!others.Contains(n)) others.Add(n);
                        if (others.Count == 2) break;
                    }
                }
                catch { /* the primary alone serves */ }

                var line = $" Our life and bread is the {primary.ToLowerInvariant()} we send to market";
                if (others.Count == 1) line += $", beside some {others[0].ToLowerInvariant()}";
                else if (others.Count == 2) line += $", beside some {others[0].ToLowerInvariant()} and {others[1].ToLowerInvariant()}";
                return line + ".";
            }
            catch { return string.Empty; }
        }

        public static string DescribeRelation(int relation)
        {
            if (relation >= 60) return "devoted friend";
            if (relation >= 30) return "good friend";
            if (relation >= 10) return "friendly";
            if (relation > -10) return "neutral acquaintance";
            if (relation > -30) return "unfriendly";
            if (relation > -60) return "hostile";
            return "bitter enemy";
        }

        private static string BuildPersonality(Hero npc)
        {
            var sb = new StringBuilder();
            AppendTrait(sb, npc.GetTraitLevel(DefaultTraits.Honor), "honorable", "deceitful");
            AppendTrait(sb, npc.GetTraitLevel(DefaultTraits.Valor), "daring", "cautious");
            AppendTrait(sb, npc.GetTraitLevel(DefaultTraits.Mercy), "compassionate", "cruel");
            AppendTrait(sb, npc.GetTraitLevel(DefaultTraits.Generosity), "generous", "closefisted");
            AppendTrait(sb, npc.GetTraitLevel(DefaultTraits.Calculating), "calculating", "impulsive");
            return sb.Length == 0 ? "Unremarkable temperament." : sb.ToString().TrimEnd(',', ' ') + ".";
        }

        private static void AppendTrait(StringBuilder sb, int level, string high, string low)
        {
            if (level > 0) sb.Append(high + ", ");
            else if (level < 0) sb.Append(low + ", ");
        }
    }
}
