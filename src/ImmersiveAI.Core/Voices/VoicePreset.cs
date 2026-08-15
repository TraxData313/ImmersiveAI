using System;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>Which engine speaks a voice. The mod carries the same two-road shape the LLM side
    /// does: a local engine that costs nothing but wants a real machine, and a hosted one that
    /// wants only the key the player already pasted in.</summary>
    public enum VoiceBackend
    {
        /// <summary>Qwen-TTS running on the player's own machine — cloned voices, free, needs the
        /// models and (in practice) a GPU.</summary>
        QwenLocal = 0,

        /// <summary>A hosted OpenAI-compatible speech endpoint (OpenRouter and friends) — no
        /// download, a fixed catalogue of voices, billed per character.</summary>
        Remote = 1,
    }

    /// <summary>A hint used only to choose a soul's FIRST voice, before anyone has assigned one.
    /// Never a claim about the voice itself — a player may put any voice on anybody.</summary>
    public enum VoiceGender
    {
        Unknown = 0,
        Female = 1,
        Male = 2,
    }

    /// <summary>
    /// One voice the NPCs can speak with — the mod's own portable record of it.
    /// <para>
    /// A voice lives as a FOLDER, not a row in a table: its metadata, its speaker embedding and
    /// (optionally) the few seconds of reference audio it was cloned from all sit together under
    /// one name. That is the whole shareability story — a voice is something you can zip, hand to
    /// a friend, and drop into their Voices folder, with nothing else to edit and no absolute path
    /// to break. Qwen-TTS Studio keeps the same material in one flat TSV plus two side folders of
    /// id-named files; <see cref="VoiceLibrary.ImportFromStudio"/> gathers those into this shape.
    /// </para>
    /// </summary>
    public sealed class VoicePreset
    {
        /// <summary>Stable identity, and the folder name — lowercase, filename-safe.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>What the player sees and picks from a list.</summary>
        public string Name { get; set; } = string.Empty;

        public VoiceBackend Backend { get; set; } = VoiceBackend.QwenLocal;

        /// <summary>For <see cref="VoiceBackend.Remote"/>: the hosted voice's own id (a Kokoro
        /// speaker, say). Empty for a local clone, which is identified by its embedding file.</summary>
        public string RemoteVoiceId { get; set; } = string.Empty;

        /// <summary>
        /// For one of the speech model's OWN built-in speakers: its name, as the model knows it.
        /// Empty for everything else.
        /// <para>
        /// These exist only on the <c>customvoice</c> model — the <c>base</c> model that does the
        /// cloning reports none at all — which is the wrinkle worth knowing about: a player who
        /// fetched the cloning model and has made no voice of their own has nothing to speak with
        /// until they make one. Nothing is stored on disk for these; they are offered when the
        /// loaded model is one that carries them.
        /// </para>
        /// </summary>
        public string SpeakerName { get; set; } = string.Empty;

        /// <summary>Speaker-embedding width the engine was asked for — Studio writes both 1024 and
        /// 2048 and the model picks by its own size. We keep the one that was actually chosen.</summary>
        public int Dimension { get; set; } = 2048;

        /// <summary>What the reference clip actually says (or how the player described the voice).
        /// The ICL road hands this to the engine alongside the audio; it is also simply the most
        /// useful thing to show a player deciding between five voices named "Sibylla 3".</summary>
        public string ReferenceText { get; set; } = string.Empty;

        /// <summary>Only ever a hint for first assignment — see <see cref="VoiceGender"/>.</summary>
        public VoiceGender Gender { get; set; } = VoiceGender.Unknown;

        /// <summary>
        /// Which people this voice belongs to, as the game's own culture id — <c>battania</c>,
        /// <c>aserai</c>, <c>empire</c>. Empty means it belongs to nobody in particular, which is
        /// the honest state of a voice cloned off a friend rather than cast for a nation.
        /// <para>
        /// Like <see cref="Gender"/> this is ONLY a hint for choosing a soul's first voice, never a
        /// rule about the voice itself: the player may put a Battanian voice on an Imperial lady and
        /// nothing here will argue. It is filled in from the folder a shipped voice arrives in
        /// (<c>Voices\female\battania\</c>), and it is what lets a fresh install give five hundred
        /// souls their own voice without anyone casting a single one.
        /// </para>
        /// </summary>
        public string Culture { get; set; } = string.Empty;

        /// <summary>The player's own note. Never shown to an NPC, never sent anywhere.</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>Where it came from, for the player's own bookkeeping ("Qwen-TTS Studio",
        /// "cloned in game", "shared by …"). Free text on purpose.</summary>
        public string Source { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // ---- resolved at load time from the folder's contents; never persisted ----

        /// <summary>The folder this voice was loaded from. Empty for a voice not yet saved.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>Full path of the speaker-embedding json, when the folder holds one.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public string EmbeddingPath { get; set; } = string.Empty;

        /// <summary>Full path of the ICL-prompt json, when the folder holds one.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public string IclPromptPath { get; set; } = string.Empty;

        /// <summary>Full path of the kept reference clip, when the folder holds one. Optional —
        /// a voice speaks from its embedding alone; the clip is kept so it can be re-extracted at
        /// another width, or re-cloned by whoever it is shared with.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public string ReferenceWavPath { get; set; } = string.Empty;

        /// <summary>True when this voice has everything it needs to actually speak.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsSpeakable =>
            Backend == VoiceBackend.Remote
                ? !string.IsNullOrWhiteSpace(RemoteVoiceId)
                : !string.IsNullOrWhiteSpace(EmbeddingPath)
                  || !string.IsNullOrWhiteSpace(IclPromptPath)
                  || !string.IsNullOrWhiteSpace(SpeakerName);
    }
}
