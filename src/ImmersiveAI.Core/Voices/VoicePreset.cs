namespace ImmersiveAI.Core.Voices
{
    /// <summary>A hint used only to choose a soul's FIRST voice, before anyone has assigned one.
    /// Never a claim about the voice itself — a player may put any voice on anybody.</summary>
    public enum VoiceGender
    {
        Unknown = 0,
        Female = 1,
        Male = 2,
    }

    /// <summary>
    /// One voice the player's voice app can speak in, as the mod sees it: a name to show and the two
    /// hints casting needs.
    /// <para>
    /// THE FILES ARE NOT OURS ANY MORE (2026.09.24). Until then the mod carried its own speech engine
    /// and this record pointed at the embedding and the reference clip on disk. The speaking now
    /// happens in claude-voice, the separate app that owns the engines, the models and the folders —
    /// so a voice here is only what that app's catalogue says about it, and the id is the one it
    /// answers to. Which engine speaks it is the app's business, never this record's.
    /// </para>
    /// </summary>
    public sealed class VoicePreset
    {
        /// <summary>The id the voice app answers to — its folder name, lowercase and stable.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Display name ("Gwen").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Only ever a hint for first assignment — see <see cref="VoiceGender"/>.</summary>
        public VoiceGender Gender { get; set; } = VoiceGender.Unknown;

        /// <summary>
        /// The people this voice belongs to, as the game's own culture id (<c>battania</c>,
        /// <c>empire</c>…), or empty when it belongs to nobody in particular.
        /// <para>
        /// Like <see cref="Gender"/> this is ONLY a hint for choosing a soul's first voice, never a
        /// rule: a player may put a Khuzait voice on a Vlandian and it is simply honoured.
        /// </para>
        /// </summary>
        public string Culture { get; set; } = string.Empty;

        /// <summary>A few words about the voice's manner, when the app has any ("the original").</summary>
        public string Style { get; set; } = string.Empty;

        /// <summary>True when there is anything to ask the app for at all.</summary>
        public bool IsSpeakable => !string.IsNullOrWhiteSpace(Id);
    }
}
