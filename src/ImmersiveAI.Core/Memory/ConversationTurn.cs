using System;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>One line spoken TO the NPC and the NPC's reply, stamped with in-game time. Usually the
    /// incoming line is the player's, but it can also be the Angel's (see <see cref="Speaker"/>) — the NPC's
    /// own exchanges with the meta-voice live in the same remembered stream, never hidden from them.</summary>
    public sealed class ConversationTurn
    {
        /// <summary>The line spoken to the NPC. From the player, unless <see cref="Speaker"/> says otherwise.</summary>
        public string PlayerLine { get; set; } = string.Empty;
        public string NpcLine { get; set; } = string.Empty;

        /// <summary>Who spoke the incoming line: null/empty means the player (the default, and what every
        /// turn saved before this field existed loads as); <see cref="AngelSpeaker"/> means the Angel — the
        /// gentle meta-voice asking or narrating (e.g. the reaching-out beats). Kept so the line is framed
        /// and shown correctly to the NPC and to the player, rather than mistaken for the player speaking.</summary>
        public string Speaker { get; set; } = string.Empty;

        /// <summary>The <see cref="Speaker"/> value marking the Angel (the meta-voice) as the one who spoke.</summary>
        public const string AngelSpeaker = "Angel";

        /// <summary>Convenience: true when the incoming line was the Angel's, not the player's.</summary>
        public bool IsFromAngel => string.Equals(Speaker, AngelSpeaker, StringComparison.OrdinalIgnoreCase);

        /// <summary>Campaign day the exchange happened on (game time, not real time). Drives compression.</summary>
        public double GameDay { get; set; }

        /// <summary>Human-readable Calradia date/time of the exchange (e.g. "1084.02.15 14.30").
        /// Filled by the game layer; empty for older turns saved before this was tracked.</summary>
        public string CalradiaTime { get; set; } = string.Empty;

        /// <summary>Where the exchange happened (settlement name, or a short field note). Filled by the
        /// game layer; empty for older turns. Lets the NPC recall when and where each thing was said.</summary>
        public string Place { get; set; } = string.Empty;

        /// <summary>How the NPC's own regard for the player moved on this exchange, as they themselves set
        /// it (the private ♥ mark, folded into the real game standing). 0 when they left it unchanged or
        /// relationship shifts are off. Kept for provenance — a trail of how a bond grew, turn by turn.</summary>
        public int FeltShift { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
