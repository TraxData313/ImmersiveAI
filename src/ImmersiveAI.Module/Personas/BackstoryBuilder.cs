using System;
using System.Collections.Generic;
using ImmersiveAI.Core.Memory;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// The story the world already tells of an NPC, gathered as the first page of their self file
    /// (see SelfSeedFormatter in Core for the shaping; the seeding hook lives in the behavior's
    /// LoadOrSeedSelf). A wanderer has a real, hand-written tale — the one they tell in taverns
    /// when first met, already in their own first-person voice. Anyone else gets the account the
    /// world keeps of them: a hand-authored biography when one exists (story characters, mods set
    /// Hero.EncyclopediaText), else the same reputational story the encyclopedia page composes —
    /// title, house, repute. Best-effort throughout: any failure means they simply begin unwritten,
    /// as everyone did before this existed.
    /// </summary>
    public static class BackstoryBuilder
    {
        public static string BuildInitialSelf(Hero npc)
        {
            try
            {
                if (npc == null) return string.Empty;

                var spoken = WandererTale(npc);
                if (!string.IsNullOrWhiteSpace(spoken)) return spoken;

                // A hand-authored biography rides first (vanilla lords have none; story NPCs and
                // mod-added heroes may). It is third person, so it is framed as the world's telling.
                var bio = npc.EncyclopediaText?.ToString();
                if (!string.IsNullOrWhiteSpace(bio)) return SelfSeedFormatter.FromWorldStory(bio);

                // The generated encyclopedia account needs a house and a banner to speak of.
                if (npc.Clan != null && npc.MapFaction != null)
                    return SelfSeedFormatter.FromWorldStory(Hero.SetHeroEncyclopediaTextAndLinks(npc)?.ToString());

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // The tavern tale is keyed by the wanderer's character template: parts a through d are the
        // telling itself (the prebackstory line is the "shall I tell you?" beat, not the story, so
        // it is left out), with a single generic fallback for templates that tell it in one breath.
        private static string WandererTale(Hero npc)
        {
            if (!npc.IsWanderer) return string.Empty;

            var templateId = TemplateId(npc);
            if (string.IsNullOrWhiteSpace(templateId)) return string.Empty;

            var parts = new List<string?>
            {
                FindLine("backstory_a", templateId),
                FindLine("backstory_b", templateId),
                FindLine("backstory_c", templateId),
                FindLine("backstory_d", templateId),
            };
            var tale = SelfSeedFormatter.FromOwnStory(parts);
            if (tale.Length > 0) return tale;

            return SelfSeedFormatter.FromOwnStory(new[] { FindLine("generic_backstory", templateId) });
        }

        // A living wanderer usually IS their template character; a spawned copy points back to it
        // through OriginalCharacter (surfaced as Hero.Template).
        private static string TemplateId(Hero npc)
        {
            var own = npc.CharacterObject?.StringId;
            if (own != null && own.StartsWith("spc_wanderer_", StringComparison.OrdinalIgnoreCase)) return own;
            return npc.Template?.StringId ?? own ?? string.Empty;
        }

        // Backstory strings load as GameText id + template-id variation ("backstory_a.spc_wanderer_…"
        // in the XML). A missing variation comes back as an "ERROR: Text with id …" placeholder,
        // which counts as no line at all.
        private static string? FindLine(string id, string templateId)
        {
            try
            {
                var text = GameTexts.FindText(id, templateId)?.ToString();
                if (text == null || text.Trim().Length == 0) return null;
                if (text.StartsWith("ERROR: Text with id", StringComparison.OrdinalIgnoreCase)) return null;
                return text;
            }
            catch
            {
                return null;
            }
        }
    }
}
