using System.Collections.Generic;
using System.Text;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// Shapes the FIRST page of an NPC's deep memory (<see cref="NpcMemory.Summary"/>) from the
    /// story the world already tells of them, so no one begins as a blank page: a wanderer arrives
    /// carrying the tale they tell in taverns (hand-written, already in their own first-person
    /// voice), a noble the account the world keeps of their house and repute. Because it is MEMORY
    /// (not a fixed sheet line), it lives under the rolling summary's own rules from the start —
    /// rewritten whole at every compression, theirs to keep, reshape, or let fade. (Until
    /// 2026.08.08 this seeded self.txt instead, where it stood at a constant place and weight.)
    ///
    /// The Module side (BackstoryBuilder) gathers the raw texts from the game; everything here is
    /// pure text-shaping so it stays unit-tested.
    /// </summary>
    public static class StorySeedFormatter
    {
        /// <summary>
        /// Weaves a story told in the NPC's OWN voice — a wanderer's tavern tale, told in parts —
        /// into one piece of prose. Each part is a beat of the telling and keeps its own paragraph.
        /// Blank parts are skipped and markup is stripped; empty when there is no story to tell.
        /// </summary>
        public static string FromOwnStory(IEnumerable<string?>? parts)
        {
            if (parts == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                var clean = TidingsFormatter.StripMarkup(part);
                if (clean.Length == 0) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(clean);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Frames a story the world tells ABOUT the NPC (third person — the account the chroniclers
        /// keep of a noble) as something they can carry in their own memory: their voice owning the
        /// telling, the story following. Their first settling of memory makes it truly theirs.
        /// Empty in, empty out.
        /// </summary>
        public static string FromWorldStory(string? story)
        {
            var clean = TidingsFormatter.StripMarkup(story);
            if (clean.Length == 0) return string.Empty;
            return "So runs my story, as the world tells it: " + clean;
        }

        /// <summary>
        /// What the NPC had already heard of the player before they ever spoke — renown turned into
        /// remembered hearsay, closing the seed so a famed name enters the story already carried.
        /// Tiers follow the situation's own thresholds (word begins to travel at 150; 300 carries it
        /// far; 900 matches "famed across all Calradia" in the clan recall) — below 150 the world has
        /// told them nothing and this is empty, agreeing with the beholder's "No word of their deeds
        /// has ever reached me." Like the rest of the seed it is MEMORY: theirs to keep or let fade,
        /// while the live situation line keeps telling how far the name travels TODAY.
        /// </summary>
        public static string FromPlayerFame(string? playerName, float renown)
        {
            var name = (playerName ?? string.Empty).Trim();
            if (name.Length == 0) return string.Empty;
            if (renown >= 900f)
                return $"Of {name}, before ever we spoke, the world had long been telling: a name famed across all Calradia, whose deeds are recounted at many fires.";
            if (renown >= 300f)
                return $"Of {name}, before ever we spoke, the world had already told me much: a name carried far across the land, with no small tale of deeds behind it.";
            if (renown >= 150f)
                return $"Of {name}, before ever we spoke, some word had already reached me: a name with deeds beginning to travel behind it.";
            return string.Empty;
        }
    }
}
