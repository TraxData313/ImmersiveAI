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
                sb.Append($"A {culture} character");

            if (npc.Clan != null)
                sb.Append($" of clan {npc.Clan.Name}");
            if (npc.MapFaction != null && npc.Clan?.Kingdom != null)
                sb.Append($", sworn to {npc.Clan.Kingdom.Name}");
            sb.Append('.');

            // The standing toward the player deliberately does NOT ride here: the situation block
            // (SituationBuilder.DescribeOther) speaks it once, beside the person it belongs to, so the
            // sheet never tells her the same heart twice in two places.
            return sb.ToString();
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
