namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// An NPC's evolving sense of who they are — their spirit, longings, and what they hold dear —
    /// written by the NPC themselves when they reflect, in their own first-person voice, never by
    /// the player.
    ///
    /// Deliberately kept apart from <see cref="NpcMemory"/>: memory is what they hold *of another*
    /// and is branching toward per-person files (this player, later other NPCs), whereas the self is
    /// *general* to the NPC — one identity carried into every relationship. It lives in its own file
    /// so it can grow independently. Today it is a single piece of prose; it may gain more over time.
    /// </summary>
    public sealed class NpcSelf
    {
        public string Text { get; set; } = string.Empty;
    }
}
