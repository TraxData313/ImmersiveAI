using System;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>One player line and the NPC's reply, stamped with in-game time.</summary>
    public sealed class ConversationTurn
    {
        public string PlayerLine { get; set; } = string.Empty;
        public string NpcLine { get; set; } = string.Empty;

        /// <summary>Campaign day the exchange happened on (game time, not real time). Drives compression.</summary>
        public double GameDay { get; set; }

        /// <summary>Human-readable Calradia date/time of the exchange (e.g. "1084.02.15 14.30").
        /// Filled by the game layer; empty for older turns saved before this was tracked.</summary>
        public string CalradiaTime { get; set; } = string.Empty;

        /// <summary>Where the exchange happened (settlement name, or a short field note). Filled by the
        /// game layer; empty for older turns. Lets the NPC recall when and where each thing was said.</summary>
        public string Place { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
