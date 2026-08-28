namespace ImmersiveAI.Mcm
{
    /// <summary>
    /// The dropdown choice lists the MCM menu offers, kept apart from <see cref="ImmersiveAiMcmSettings"/>
    /// because they are needed in two worlds: the menu itself (MCM present) and the store-file rescue in
    /// <see cref="McmBridge"/>, which maps MCM's persisted dropdown INDICES back to values on machines
    /// where the menu never connected — and so must never touch an MCM type. Plain strings only here.
    /// MCM's store file persists selections as indices into these arrays, so append new entries at the
    /// END; reordering silently changes what old store files mean.
    /// </summary>
    internal static class McmChoiceLists
    {
        // Gemini and DeepSeek joined on 2026.08.02, ClaudeCode (the subscription road) on
        // 2026.08.28 — all APPENDED, not slotted in beside the other cloud services: an old store
        // file's "3" must keep meaning Local.
        public static readonly string[] Backends = { "Anthropic", "OpenAI", "OpenRouter", "Local", "Gemini", "DeepSeek", "ClaudeCode" };

        /// <summary>The models the Claude Code road offers by name. Whatever the player's plan
        /// carries works — these are the ones worth listing; anything else goes in the custom
        /// field. Fable thinks always and spends the plan's windows fastest.</summary>
        public static readonly string[] ClaudeCodeModels =
            { "claude-haiku-4-5", "claude-sonnet-5", "claude-opus-5", "claude-fable-5" };

        /// <summary>Every free-tier Gemini, newest first. The Pro models are paid-only since April
        /// 2026 and deliberately absent: the whole point of this backend is the free road.
        /// gemini-2.5-flash (appended 2026.08.02) is the odd one out — older, but the last capable
        /// model whose thinking can be truly switched OFF (the client sends "none" for the 2.5s),
        /// so it is the escape from the 3.x thinking tax; Google retires it 2026.10.16.</summary>
        public static readonly string[] GeminiModels =
            { "gemini-3.6-flash", "gemini-3.5-flash", "gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-2.5-flash-lite", "gemini-2.5-flash" };

        public static readonly string[] DeepSeekModels = { "deepseek-v4-flash", "deepseek-v4-pro" };

        public static readonly string[] AnthropicModels =
            { "claude-haiku-4-5", "claude-sonnet-5", "claude-opus-4-8", "claude-fable-5" };

        // 2026.08.02, the ONE sanctioned deviation from append-only: luna SWAPPED to the top of both
        // cloud lists (Anton's call once luna proved itself in live play — and since the July price
        // cut it is cheaper than 5.4-mini too). A swap, not a shift, so only the two exchanged slots
        // change meaning for old store files; the bridge's value-based push self-heals stored
        // indices at every successful bind, and the once-per-process rescue can at worst adopt the
        // swapped sibling — both fine models. Everything else remains append-at-the-END.
        public static readonly string[] OpenAIModels =
            { "gpt-5.6-luna", "gpt-5.4-mini", "gpt-5.6-terra", "gpt-5.6-sol", "gpt-5.5", "gpt-5.4", "gpt-5.4-nano" };

        public static readonly string[] OpenRouterModels =
        {
            "openai/gpt-5.6-luna",
            "anthropic/claude-haiku-4.5",
            "deepseek/deepseek-v4-flash",
            "google/gemini-2.5-flash",
            "google/gemini-3.5-flash",
            "mistralai/mistral-large-2512",
            "anthropic/claude-sonnet-5",
            "openai/gpt-5.4-mini",
            "openai/gpt-5.6-terra",
            "x-ai/grok-4.5",
            "anthropic/claude-opus-4.8",
            "anthropic/claude-fable-5",
            "openai/gpt-5.4-nano",
            "google/gemini-3.6-flash",
            "deepseek/deepseek-v4-pro",
        };

        public static readonly string[] HotkeyKeys =
            { "O", "P", "K", "J", "U", "N", "B", "L", "Y", "H", "G", "V", "F9", "F10", "F11", "F12" };

        /// <summary>The director's-spark modes as the MENU spells them; config.json spells them
        /// "Generate"/"Ask"/"Off" (see McmBridge.SparkModeValue for the mapping). Append-only.</summary>
        public static readonly string[] SparkModes = { "Generate", "Ask first", "Off" };

        /// <summary>How a spoken reply is made and delivered, as the MENU spells it; config.json
        /// spells them "FullRead"/"Streaming"/"ByLine" (see McmBridge.VoiceDeliveryValue).
        /// APPEND-ONLY: these are persisted by MCM as indices into this array, so a reorder silently
        /// changes what an existing store file means. Index 0 is the default.</summary>
        public static readonly string[] VoiceDeliveryModes =
            { "Full read (steadiest)", "Streaming (starts soonest)", "By line (oldest)" };

        /// <summary>
        /// What a voice dropdown holds before the shelf has been read: the "nobody" entry, which is
        /// also a real choice (no voice for this kind of soul at all).
        /// <para>
        /// The voice lists are the ONE place this file's append-only rule cannot hold, because they
        /// are not a fixed vocabulary at all — they are whatever the player has made. So they are
        /// rebuilt from the shelf at every bind and re-selected BY VOICE ID, and the index MCM
        /// persisted last session is treated as disposable. See McmBridge.RebuildVoiceLists.
        /// </para>
        /// </summary>
        public static readonly string[] NoVoice = { NoVoiceLabel };

        public const string NoVoiceLabel = "(none)";

        /// <summary>Keys offered for the panic stop. Backspace first, and it is the default: nothing
        /// in the game wants it during play, and it can be hit blind while something is screeching.
        /// Append-only, like every other list here.</summary>
        /// <summary>How long the NPCs speak. Index 1 is the default. Append-only, like the rest.</summary>
        public static readonly string[] ReplyLengths = { "Brief", "Conversational", "Full" };

        public static readonly string[] PanicKeys =
            { "Backspace", "Delete", "End", "Home", "Insert", "F8", "F9", "F10", "F11", "F12" };

        /// <summary>The value an MCM-persisted dropdown index means, or null when the index falls
        /// outside the list (older/newer store files, or lists grown at runtime by SelectOrAdd).</summary>
        public static string? AtIndex(string[] choices, int? index) =>
            index.HasValue && index.Value >= 0 && index.Value < choices.Length ? choices[index.Value] : null;
    }
}
