namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// Everything that makes one NPC sound different from another.
    /// Built by the game layer from Hero data; consumed by PromptBuilder.
    /// </summary>
    public sealed class NpcPersona
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>The opening identity/atmosphere line, e.g. "You are Aurelia, a living soul in the world
        /// of Calradia in feudal times." Player-configurable (name already substituted by the game layer);
        /// when empty <see cref="PromptBuilder"/> falls back to its own default. Lets the storyteller set the
        /// whole atmosphere from the config file.</summary>
        public string AtmosphereLine { get; set; } = string.Empty;

        /// <summary>Optional, player-authored guidance on tone and spirit — how the world feels, an invitation
        /// to roleplay and enjoy it — offered gently as freedom, never a command. Folded into the closing
        /// "whisper of guidance". Empty by default (the game layer fills it from config).</summary>
        public string RoleplayGuidance { get; set; } = string.Empty;

        /// <summary>The NPC's kin and house, in their own second-person recollection (parents, spouse,
        /// children with ages, clan and its people). Durable identity, folded in on every chat so they feel
        /// part of a family in this world. Built by the game layer from live Hero data.</summary>
        public string FamilyKnowledge { get; set; } = string.Empty;

        /// <summary>Role and standing, e.g. "Vlandian lord, ruler of Sargot, at war with the Empire".</summary>
        public string RoleDescription { get; set; } = string.Empty;

        /// <summary>Prose rendering of game personality traits (honor, valor, mercy, ...).</summary>
        public string PersonalityDescription { get; set; } = string.Empty;

        /// <summary>
        /// A distinct voice assigned to this NPC (vocabulary, sentence rhythm, verbal tics).
        /// Giving every NPC its own speech style is a primary anti-repetition lever.
        /// </summary>
        public string SpeechStyle { get; set; } = string.Empty;

        /// <summary>The NPC's own evolving sense of who they are, authored by them (not the player)
        /// during reflection. Distinct from the game-given traits above and from the user-authored
        /// instructions below; it is the self they have grown into. Kept in its own file. See
        /// <see cref="ImmersiveAI.Core.Memory.NpcSelf"/>.</summary>
        public string SelfConcept { get; set; } = string.Empty;

        /// <summary>Optional world-wide, user-authored instructions shared by every NPC
        /// (the global prompt file). Shown near the top as "About Calradia:".</summary>
        public string WorldInstructions { get; set; } = string.Empty;

        /// <summary>Optional user-authored extra instructions for THIS NPC (per-NPC prompt file).
        /// Shown near the top as "About you:".</summary>
        public string CustomInstructions { get; set; } = string.Empty;

        /// <summary>True when this NPC can reach into the world's memory mid-thought (the recall
        /// tools are on offer — see the game layer's WorldRecall). Adds a whisper telling them to
        /// trust what surfaces over invention.</summary>
        public bool CanRecallWorld { get; set; }
    }
}
