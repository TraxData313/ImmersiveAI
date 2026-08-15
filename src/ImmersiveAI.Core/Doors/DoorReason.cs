using System;

namespace ImmersiveAI.Core.Doors
{
    /// <summary>
    /// ONE REASON A DOOR IS CLOSED, in her own words (2026.08.15).
    ///
    /// This is the misgivings model applied to the night, and it is deliberately the SAME model
    /// rather than a second shape: her list, her words, written by her own hand, and laid to rest
    /// by her own judgment and nobody else's. What it adds is the second half, which is the half
    /// that makes the thing playable — beside WHY, she writes WHAT WOULD OPEN IT AGAIN. Without
    /// that a closed door is a wall with no door in it, and the player is left guessing at a
    /// combination.
    ///
    /// There is no forgiveness function anywhere in this mod. There never was one for the
    /// misgivings either, and that turned out to be the discovery of the whole design: we do not
    /// program forgiveness, we hand her something to forgive with.
    /// </summary>
    public sealed class DoorReason
    {
        /// <summary>Her own words for what stands between them.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Her own words for what would answer it. May be empty — a woman is not obliged
        /// to know the way back, and "I do not know what would fix this" is a real answer that the
        /// player has to live with.</summary>
        public string Opens { get; set; } = string.Empty;

        /// <summary>Whether she has laid it to rest. Only her own hand ever sets this.</summary>
        public bool Settled { get; set; }

        /// <summary>Her light word on what answered it, kept so the player can read what reached
        /// her — which is the only feedback this system ever gives, and enough.</summary>
        public string SettledNote { get; set; } = string.Empty;

        public double SetDownDay { get; set; } = -1;
        public double SettledDay { get; set; } = -1;

        /// <summary>
        /// True when this was laid down by the world rather than by her hand — a duty night, and
        /// whatever else the mechanics later decide must cost something whether or not she happens
        /// to be in a talk that day.
        ///
        /// It matters for exactly one reason and it is not bookkeeping: the SPIRAL has to bite. A
        /// closure that only ever deepens when she is standing in front of the player, with a tool
        /// slot free and a model willing to reach for it, is a closure a player can farm by simply
        /// not talking to her. So the world lays these down itself, in fixed words, and she may
        /// still settle them by her own judgment like any other.
        /// </summary>
        public bool LaidByTheWorld { get; set; }

        public bool Stands => !Settled && !string.IsNullOrWhiteSpace(Text);
    }
}
