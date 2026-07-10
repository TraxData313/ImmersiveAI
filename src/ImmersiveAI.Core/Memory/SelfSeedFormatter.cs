using System.Collections.Generic;
using System.Text;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// Shapes the FIRST content of an NPC's self file (<see cref="NpcSelf"/>) from the story the
    /// world already tells of them, so no one begins as a blank page: a wanderer arrives carrying
    /// the tale they tell in taverns (hand-written, already in their own first-person voice), a
    /// noble the account the world keeps of their house and repute. The seed is only a beginning —
    /// at every reflection they rewrite their self freely, keeping, refining, or releasing whatever
    /// they wish, exactly as with a self they authored from nothing.
    ///
    /// The Module side (BackstoryBuilder) gathers the raw texts from the game; everything here is
    /// pure text-shaping so it stays unit-tested.
    /// </summary>
    public static class SelfSeedFormatter
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
        /// keep of a noble) as something they can carry in their own file: their voice owning the
        /// telling, the story following. Their first reflection makes it truly theirs. Empty in,
        /// empty out.
        /// </summary>
        public static string FromWorldStory(string? story)
        {
            var clean = TidingsFormatter.StripMarkup(story);
            if (clean.Length == 0) return string.Empty;
            return "So runs my story, as the world tells it: " + clean;
        }
    }
}
