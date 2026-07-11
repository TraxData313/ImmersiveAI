using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Weighs a hero's REAL skills into honest first-person craft-words, so every soul knows what
    /// their own hands and wits are good at — a wanderer asked "what would you be good at?" answers
    /// from truth, a green scout owns that his eyes are yet green, and a captain judging his people
    /// judges them truly. One short line for the persona sheet; single-skill words for the duty
    /// sentences (SituationBuilder) and the recall of a person (WorldRecall).
    ///
    /// Everything is best-effort: a hero whose skills cannot be read simply has no crafts line.
    /// </summary>
    public static class CraftsBuilder
    {
        /// <summary>The persona-sheet line: "What my hands and wits are good at: masterly in
        /// Medicine; able in Scouting and Riding; middling with the rest I have tried." Empty when
        /// nothing rises above the common run.</summary>
        public static string Build(Hero h)
        {
            if (h == null) return string.Empty;
            try
            {
                int ValueOf(SkillObject s) { try { return h.GetSkillValue(s); } catch { return 0; } }
                var ranked = TaleWorlds.CampaignSystem.Extensions.Skills.All?
                    .Where(s => s != null)
                    .Select(s => new { Name = s.Name?.ToString() ?? string.Empty, Value = ValueOf(s) })
                    .Where(x => x.Name.Length > 0 && x.Value >= 40)
                    .OrderByDescending(x => x.Value)
                    .Take(5)
                    .ToList();
                if (ranked == null || ranked.Count == 0) return string.Empty;

                var parts = ranked
                    .GroupBy(x => Word(x.Value))
                    .Select(g => $"{g.Key} in {JoinAnd(g.Select(x => x.Name).ToList())}");
                return "What my hands and wits are honestly good at: " + string.Join("; ", parts) + ".";
            }
            catch { return string.Empty; }
        }

        /// <summary>One skill weighed into its craft-word — "green", "middling", "able", "fine",
        /// "masterly", "among the finest in Calradia" — for a duty sentence.</summary>
        public static string WordFor(Hero h, SkillObject skill)
        {
            try { return Word(h?.GetSkillValue(skill) ?? 0); }
            catch { return "middling"; }
        }

        /// <summary>A raw skill value weighed into its craft-word — for callers that already hold
        /// the number (the recall of a person lists another's crafts this way).</summary>
        public static string WordForValue(int value) => Word(value);

        /// <summary>The raw value, safely — for the game layer to coarsen numbers by (a green scout
        /// counts banners less surely than a master).</summary>
        public static int ValueFor(Hero h, SkillObject skill)
        {
            try { return h?.GetSkillValue(skill) ?? 0; }
            catch { return 0; }
        }

        // The scale of honest words. Kept coarse on purpose: the NPC should carry a self-judgment,
        // not a character sheet.
        private static string Word(int value)
        {
            if (value >= 225) return "among the finest in Calradia";
            if (value >= 175) return "masterly";
            if (value >= 125) return "fine";
            if (value >= 75) return "able";
            if (value >= 40) return "middling";
            return "green";
        }

        private static string JoinAnd(System.Collections.Generic.List<string> items)
        {
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return items[0] + " and " + items[1];
            return string.Join(", ", items.Take(items.Count - 1)) + ", and " + items[items.Count - 1];
        }
    }
}
