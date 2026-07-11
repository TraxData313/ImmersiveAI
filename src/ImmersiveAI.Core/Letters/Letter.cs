using System;

namespace ImmersiveAI.Core.Letters
{
    /// <summary>
    /// One letter on the road — the mod's first asynchronous act: an NPC (or the player) writing
    /// across the map instead of speaking face to face. Letters travel with distance and survive
    /// save/load (they live in the campaign's own letters file, like memories do), so a word sent
    /// is a word that will arrive, whatever else happens in between.
    /// </summary>
    public sealed class Letter
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>The NPC on the far end — writer when <see cref="ToPlayer"/>, recipient otherwise.</summary>
        public string NpcId { get; set; } = string.Empty;

        /// <summary>Their name at writing time, so the letter still reads truly if they die en route.</summary>
        public string NpcName { get; set; } = string.Empty;

        /// <summary>True: NPC → player. False: player → NPC.</summary>
        public bool ToPlayer { get; set; }

        public string Body { get; set; } = string.Empty;

        public double SentGameDay { get; set; }
        public double ArriveGameDay { get; set; }

        /// <summary>True when this letter answers one received (an NPC replies at most once per
        /// letter, so correspondence is a chain of choices, not an echo).</summary>
        public bool IsReply { get; set; }

        /// <summary>Where it was written (a settlement name or a field note) — the letterhead.</summary>
        public string SentFrom { get; set; } = string.Empty;

        /// <summary>True once this letter stands in the human-readable correspondence log. The
        /// player's own letters are logged the moment they set out (the player knows what they
        /// wrote); an NPC's letter to the player is logged only on ARRIVAL — a letter still on the
        /// road must not be readable through the letter window. Defaults true so letters persisted
        /// before this flag existed (already logged at send) are never logged twice.</summary>
        public bool Logged { get; set; } = true;
    }
}
