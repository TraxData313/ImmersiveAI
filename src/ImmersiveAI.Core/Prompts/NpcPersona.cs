namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// Everything that makes one NPC sound different from another.
    /// Built by the game layer from Hero data; consumed by PromptBuilder.
    /// </summary>
    public sealed class NpcPersona
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Role and standing, e.g. "Vlandian lord, ruler of Sargot, at war with the Empire".</summary>
        public string RoleDescription { get; set; } = string.Empty;

        /// <summary>Prose rendering of game personality traits (honor, valor, mercy, ...).</summary>
        public string PersonalityDescription { get; set; } = string.Empty;

        /// <summary>
        /// A distinct voice assigned to this NPC (vocabulary, sentence rhythm, verbal tics).
        /// Giving every NPC its own speech style is a primary anti-repetition lever.
        /// </summary>
        public string SpeechStyle { get; set; } = string.Empty;

        /// <summary>Optional user-authored extra instructions (per-NPC prompt file).</summary>
        public string CustomInstructions { get; set; } = string.Empty;
    }
}
