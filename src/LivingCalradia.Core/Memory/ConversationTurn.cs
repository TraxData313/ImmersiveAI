using System;

namespace LivingCalradia.Core.Memory
{
    /// <summary>One player line and the NPC's reply, stamped with in-game time.</summary>
    public sealed class ConversationTurn
    {
        public string PlayerLine { get; set; } = string.Empty;
        public string NpcLine { get; set; } = string.Empty;

        /// <summary>Campaign day the exchange happened on (game time, not real time).</summary>
        public double GameDay { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
