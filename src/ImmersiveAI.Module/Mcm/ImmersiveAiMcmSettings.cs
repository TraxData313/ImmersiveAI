using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace ImmersiveAI.Mcm
{
    /// <summary>
    /// The in-game Mod Configuration Menu (MCM) face of <see cref="ModConfig"/>. MCM auto-discovers
    /// this class when the Mod Configuration Menu module is installed; when it is NOT, nothing ever
    /// touches this type, so the mod runs on <c>config.json</c> alone (see <see cref="McmBridge"/>).
    ///
    /// This is a live editor over the exposed subset only — <c>config.json</c> remains the full,
    /// authoritative store. <see cref="McmBridge"/> pushes the current config values into this menu on
    /// startup and writes every menu change straight back into the shared <see cref="ModConfig"/> and
    /// its file, so config.json is always the single source of truth and no menu default can ever wipe
    /// a value the player set by hand.
    /// </summary>
    public sealed class ImmersiveAiMcmSettings : AttributeGlobalSettings<ImmersiveAiMcmSettings>
    {
        // Bumping the id (…_v2) would orphan the old MCM file; since config.json is our real store,
        // that is harmless — the menu just re-seeds from config.json. Keep it stable regardless.
        public override string Id => "ImmersiveAI_v1";
        public override string DisplayName => "Immersive AI";
        public override string FolderName => "ImmersiveAI";
        public override string FormatType => "json2";

        // ── Connection ──────────────────────────────────────────────────────────────
        // Which service answers, the keys, and the models. These are read once when the chat client
        // is built at game start, so changing them asks for a restart to take effect.

        [SettingPropertyDropdown("Backend", Order = 0, RequireRestart = true,
            HintText = "Which AI service the NPCs think with. Anthropic (Claude) is the default; OpenAI (GPT) also works; OpenRouter reaches both with one key from openrouter.ai; Local speaks to a model on YOUR machine (LM Studio/Ollama) — free and private, no key, but weaker with the NPCs' tools.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> Backend { get; set; } = new Dropdown<string>(new[] { "Anthropic", "OpenAI", "OpenRouter", "Local" }, 0);

        [SettingPropertyText("Anthropic API key", Order = 1, RequireRestart = true,
            HintText = "Your Claude API key (starts with sk-ant-...). Get one at console.anthropic.com. Required when the backend is Anthropic. Kept only in your local config file.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string AnthropicApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("Anthropic model", Order = 2, RequireRestart = true,
            HintText = "Haiku 4.5 ($1/$5 per MTok) is the fast, cheap default. Sonnet 5 ($3/$15) is the step-up, Opus 4.8 ($5/$25) stronger still, Fable 5 ($10/$50) the flagship. A model set by hand in config.json also appears here.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> AnthropicModel { get; set; } = new Dropdown<string>(
            new[] { "claude-haiku-4-5", "claude-sonnet-5", "claude-opus-4-8", "claude-fable-5" }, 0);

        [SettingPropertyText("OpenAI API key", Order = 3, RequireRestart = true,
            HintText = "Your OpenAI API key (starts with sk-...). Required only when the backend is OpenAI. Kept only in your local config file.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenAIApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("OpenAI model", Order = 4, RequireRestart = true,
            HintText = "gpt-5.4-mini ($0.75/$4.50 per MTok) is the proven default. luna ($1/$6) is newer, terra ($2.50/$15) stronger, sol and 5.5 ($5/$30) the flagships, 5.4-nano ($0.20/$1.25) the cheapest. A model set by hand in config.json also appears here.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> OpenAIModel { get; set; } = new Dropdown<string>(
            new[] { "gpt-5.4-mini", "gpt-5.6-luna", "gpt-5.6-terra", "gpt-5.6-sol", "gpt-5.5", "gpt-5.4", "gpt-5.4-nano" }, 0);

        [SettingPropertyText("OpenRouter API key", Order = 5, RequireRestart = true,
            HintText = "Your OpenRouter key (starts with sk-or-...). Get one at openrouter.ai — one key and a little credit reaches both Claude and GPT models at the providers' own prices. Required only when the backend is OpenRouter.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenRouterApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("OpenRouter model", Order = 6, RequireRestart = true,
            HintText = "Which model when the backend is OpenRouter — GPT, Claude, Gemini, Grok, DeepSeek and Mistral all verified with the NPCs' tools, same prices as going direct. gpt-5.4-mini and claude-haiku-4.5 are the proven picks; deepseek-v4-flash the cheapest. Any other id from openrouter.ai/models set in config.json also appears here.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> OpenRouterModel { get; set; } = new Dropdown<string>(new[]
        {
            "openai/gpt-5.4-mini",
            "anthropic/claude-haiku-4.5",
            "deepseek/deepseek-v4-flash",
            "google/gemini-2.5-flash",
            "google/gemini-3.5-flash",
            "mistralai/mistral-large-2512",
            "anthropic/claude-sonnet-5",
            "openai/gpt-5.6-luna",
            "openai/gpt-5.6-terra",
            "x-ai/grok-4.5",
            "anthropic/claude-opus-4.8",
            "anthropic/claude-fable-5",
            "openai/gpt-5.4-nano",
        }, 0);

        [SettingPropertyText("Local server URL", Order = 7, RequireRestart = true,
            HintText = "Where your local AI server listens, when the backend is Local. LM Studio: http://localhost:1234/v1 (start its server on the Developer tab). Ollama: http://localhost:11434/v1. Blank resets to the LM Studio default. A key, if your server wants one, goes in LocalApiKey in config.json.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string LocalEndpoint { get; set; } = string.Empty;

        [SettingPropertyText("Local model id", Order = 8, RequireRestart = true,
            HintText = "The EXACT id your local server serves — LM Studio shows it on its server page (e.g. qwen/qwen3-30b-a3b), Ollama uses the name you pulled (e.g. qwen3:30b). Prefer an instruct model that carries native tool calling; small models go shy of the NPCs' tools.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string LocalModel { get; set; } = string.Empty;

        [SettingPropertyInteger("Local context length", 2048, 131072, "0", Order = 9, RequireRestart = true,
            HintText = "The context window your server ACTUALLY loads the model with (LM Studio's context-length setting; Ollama's num_ctx). The NPCs' memory budget scales against this — claiming more than is truly loaded means silent truncation and strange amnesia. Match your server.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public int LocalContextWindow { get; set; } = 16384;

        [SettingPropertyText("Custom endpoint (advanced)", Order = 10, RequireRestart = true,
            HintText = "Empty = the real OpenAI. For another OpenAI-compatible CLOUD service (NanoGPT…), paste its base URL ending in /v1, put its key in the OpenAI key field, and set its model id in config.json. For OpenRouter or a local server, pick those backends above instead.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenAIBaseUrl { get; set; } = string.Empty;

        [SettingPropertyInteger("Reply length (max tokens)", 100, 2000, "0", Order = 11, RequireRestart = true,
            HintText = "Roughly how long an NPC's spoken reply may run. Higher means longer answers but slower, pricier calls. 400 is a good balance.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public int MaxTokens { get; set; } = 400;

        // ── Windows & Hotkeys ───────────────────────────────────────────────────────

        [SettingPropertyBool("Chat window", Order = 0, RequireRestart = false,
            HintText = "The on-map chat window for speaking with those near you without ceremony. Opens with its hotkey or a settlement menu option.")]
        [SettingPropertyGroup("Windows & Hotkeys", GroupOrder = 1)]
        public bool EnableChatWindow { get; set; } = true;

        [SettingPropertyDropdown("Chat window key", Order = 1, RequireRestart = true,
            HintText = "The key that opens and closes the chat window on the map. Pick one that does not clash with your other map keys.")]
        [SettingPropertyGroup("Windows & Hotkeys", GroupOrder = 1)]
        public Dropdown<string> ChatWindowHotkey { get; set; } = HotkeyChoices("O");

        [SettingPropertyDropdown("Letter window key", Order = 2, RequireRestart = true,
            HintText = "The key that opens and closes the letter window on the map.")]
        [SettingPropertyGroup("Windows & Hotkeys", GroupOrder = 1)]
        public Dropdown<string> LetterWindowHotkey { get; set; } = HotkeyChoices("Y");

        [SettingPropertyBool("Notify when a reply is ready", Order = 3, RequireRestart = false,
            HintText = "Shows a short notice the moment an NPC's answer arrives, so you need not click 'wait' and guess whether it has come.")]
        [SettingPropertyGroup("Windows & Hotkeys", GroupOrder = 1)]
        public bool NotifyWhenReplyReady { get; set; } = true;

        // ── Life of the NPCs ────────────────────────────────────────────────────────

        [SettingPropertyBool("They reach out to you", Order = 0, RequireRestart = false,
            HintText = "NPCs near you may come and speak to you of their own accord, scaled by how close the bond is. Set the pace with the Socialness slider below (and the on-map control).")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool EnableNpcInitiatedChats { get; set; } = true;

        [SettingPropertyFloatingInteger("Socialness (visits per day)", 0f, 24f, "0.0", Order = 1, RequireRestart = false,
            HintText = "How open you are to company: the expected number of reach-outs per day in TOTAL across everyone when bonds are full (not per person). 0.3 is quiet; 1.5 is lively. Also adjustable live from the on-map control.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public float Socialness { get; set; } = 0.3f;

        [SettingPropertyBool("Letters", Order = 2, RequireRestart = false,
            HintText = "Distant NPCs may write letters that travel real in-game days, and you can send letters by courier from any settlement.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool EnableLetters { get; set; } = true;

        [SettingPropertyBool("Recall the world's truth", Order = 3, RequireRestart = false,
            HintText = "NPCs may look up live campaign facts mid-reply — family, who holds a town, who is at war — instead of misremembering. Recommended.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool EnableWorldRecall { get; set; } = true;

        [SettingPropertyBool("All they have read and heard (web search)", Order = 4, RequireRestart = false,
            HintText = "NPCs may quietly search the internet mid-reply when asked how something in the world is done — framed to them as their own reading and hearsay. Uses DuckDuckGo, no key needed; the search queries leave your machine, so turn this off if you prefer everything to stay between you and your AI provider.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool EnableWebSearch { get; set; } = true;

        [SettingPropertyInteger("Lasting truths per NPC", 1, 30, "0", Order = 5, RequireRestart = false,
            HintText = "How many distilled 'known facts' about you an NPC may carry at once. Higher means a longer memory but a larger, pricier prompt.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public int MaxKnownFacts { get; set; } = 10;

        [SettingPropertyInteger("Personal aims per NPC", 1, 20, "0", Order = 6, RequireRestart = false,
            HintText = "How many personal goals an NPC may hold at once — what they strive for of their own will. Kept small so their striving stays focused.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public int MaxNpcGoals { get; set; } = 6;

        [SettingPropertyBool("Memories rewind with your saves", Order = 7, RequireRestart = false,
            HintText = "When on, loading a save also rewinds the NPCs' memories to that moment — so reloading to before an NPC's angry turn truly un-remembers it (the game already rewinds the relation number itself). Off: a reload leaves them remembering what, on that timeline, never happened.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool RevertMemoriesWithSaves { get; set; } = true;

        // ── Costs ───────────────────────────────────────────────────────────────────

        [SettingPropertyBool("Show cost notices", Order = 0, RequireRestart = false,
            HintText = "After each exchange, a soft gray line shows what it took: tokens in/out, number of calls, and the price when the model's rates are known. The same lines also go to log.txt, and daily totals persist in usage.json.")]
        [SettingPropertyGroup("Costs", GroupOrder = 3)]
        public bool ShowCostNotices { get; set; } = true;

        [SettingPropertyInteger("Daily request cap (0 = none)", 0, 2000, "0", Order = 1, RequireRestart = false,
            HintText = "A safety valve: at most this many AI requests per real day, across all sessions. When reached, the world goes quiet until the day turns or the cap is raised. 0 means no cap.")]
        [SettingPropertyGroup("Costs", GroupOrder = 3)]
        public int MaxDailyRequests { get; set; } = 0;

        // ── Advanced ────────────────────────────────────────────────────────────────

        [SettingPropertyBool("Developer mode", Order = 0, RequireRestart = false,
            HintText = "Shows the mod's test levers and the 'reveal the whole of your mind' prompt inspector. Leave OFF for normal play.")]
        [SettingPropertyGroup("Advanced", GroupOrder = 4)]
        public bool DevMode { get; set; } = false;

        /// <summary>A curated set of map-safe keys for the window hotkeys, with <paramref name="preferred"/>
        /// selected. Kept modest so a player picks rather than types an InputKey name by hand.</summary>
        private static Dropdown<string> HotkeyChoices(string preferred)
        {
            var keys = new[] { "O", "P", "K", "J", "U", "N", "B", "L", "Y", "H", "G", "V", "F9", "F10", "F11", "F12" };
            var index = 0;
            for (var i = 0; i < keys.Length; i++)
                if (keys[i] == preferred) { index = i; break; }
            return new Dropdown<string>(keys, index);
        }
    }
}
