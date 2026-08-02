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
        // Which service answers, the keys, and the models. The chat client is a live shell
        // (LiveSwapChatClient) that rebuilds itself when any of these change, so every edit here
        // takes hold on the very next reply — no restart.

        [SettingPropertyDropdown("Backend", Order = 0, RequireRestart = false,
            HintText = "Which AI service the NPCs think with. OpenRouter is the default (one key, every model); OpenAI the equal second. Gemini is the FREE road - Google's own free tier, no card. DeepSeek is the cheapest paid one. Anthropic works but is less tested. Local runs on YOUR machine - tinkerers only, unsupported.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> Backend { get; set; } = new Dropdown<string>(McmChoiceLists.Backends, 2);

        [SettingPropertyText("Anthropic API key", Order = 1, RequireRestart = false,
            HintText = "Your Claude API key (starts with sk-ant-...). Get one at console.anthropic.com. Required when the backend is Anthropic. Kept only in your local config file.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string AnthropicApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("Anthropic model", Order = 2, RequireRestart = false,
            HintText = "Claude works, but is the least tested of the three cloud backends. Haiku 4.5 ($1/$5 per MTok) is the fast, cheap pick; Sonnet 5 ($3/$15) the step-up, Opus 4.8 ($5/$25) stronger still, Fable 5 ($10/$50) the flagship. Any other model goes in the custom field below, at your own risk.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> AnthropicModel { get; set; } = new Dropdown<string>(McmChoiceLists.AnthropicModels, 0);

        [SettingPropertyText("Anthropic model (type any id)", Order = 3, RequireRestart = false,
            HintText = "While this holds text it OVERRIDES the dropdown: the exact Anthropic model id to use, as the API names it. Empty = the dropdown chooses. Unlisted models still work; the ~$ cost estimate just may not know their prices.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string AnthropicModelCustom { get; set; } = string.Empty;

        [SettingPropertyText("OpenAI API key", Order = 4, RequireRestart = false,
            HintText = "Your OpenAI API key (starts with sk-...). Required only when the backend is OpenAI. Kept only in your local config file.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenAIApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("OpenAI model", Order = 5, RequireRestart = false,
            HintText = "gpt-5.6-luna ($0.20/$1.20 per MTok since the July 2026 price cut) is the default and tested pick - now also nearly the cheapest. gpt-5.4-mini ($0.75/$4.50) is the proven fallback; terra ($2/$12) stronger, sol and 5.5 ($5/$30) the flagships, 5.4-nano ($0.20/$1.25). Any other model goes in the custom field below, at your own risk.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> OpenAIModel { get; set; } = new Dropdown<string>(McmChoiceLists.OpenAIModels, 0);

        [SettingPropertyText("OpenAI model (type any id)", Order = 6, RequireRestart = false,
            HintText = "While this holds text it OVERRIDES the dropdown: the exact OpenAI model id to use (e.g. gpt-4.1) — also the place for a custom endpoint's model id. Empty = the dropdown chooses. Unlisted models still work; the ~$ estimate just may not know their prices.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenAIModelCustom { get; set; } = string.Empty;

        [SettingPropertyText("OpenRouter API key", Order = 7, RequireRestart = false,
            HintText = "Your OpenRouter key (starts with sk-or-...). Get one at openrouter.ai — one key and a little credit reaches both Claude and GPT models at the providers' own prices. Required only when the backend is OpenRouter.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenRouterApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("OpenRouter model", Order = 8, RequireRestart = false,
            HintText = "openai/gpt-5.6-luna is the default and tested pick (cheap since the July 2026 price cut), openai/gpt-5.4-mini the proven fallback — same prices as going direct. Claude, Gemini, Grok, DeepSeek and Mistral are verified to carry the NPCs' tools but are not what the mod is tuned to. Any other id goes in the custom field below, at your own risk.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> OpenRouterModel { get; set; } = new Dropdown<string>(McmChoiceLists.OpenRouterModels, 0);

        [SettingPropertyText("OpenRouter model (type any id)", Order = 9, RequireRestart = false,
            HintText = "While this holds text it OVERRIDES the dropdown: any id from openrouter.ai/models, pasted exactly in OpenRouter's own spelling (e.g. qwen/qwen3-235b-a22b). Empty = the dropdown chooses. A mistyped id answers as a clear error naming the model.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenRouterModelCustom { get; set; } = string.Empty;

        [SettingPropertyText("Gemini API key", Order = 10, RequireRestart = false,
            HintText = "FREE: a key from aistudio.google.com, no card — ~1,500 replies a day. Two catches: Google trains on free-tier traffic (nothing said here is private; paying stops that), and replies are SLOW — Gemini's thinking cannot be switched off, so prefer the chat window over face-to-face.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string GeminiApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("Gemini model", Order = 11, RequireRestart = false,
            HintText = "All of these are free-tier (the Pro models are paid-only since April 2026). gemini-3.6-flash is the pick — strongest of them, and the tools the NPCs need are steadier on it than on the Lite models. Drop to a flash-lite if you keep hitting the per-minute limit.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> GeminiModel { get; set; } = new Dropdown<string>(McmChoiceLists.GeminiModels, 0);

        [SettingPropertyText("Gemini model (type any id)", Order = 12, RequireRestart = false,
            HintText = "While this holds text it OVERRIDES the dropdown: any Gemini id as Google names it. Empty = the dropdown chooses. Note that Gemini 3.x models cannot be told to stop thinking, so their replies always cost some silent thought — the mod widens their token ceiling to leave room for it.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string GeminiModelCustom { get; set; } = string.Empty;

        [SettingPropertyText("DeepSeek API key", Order = 13, RequireRestart = false,
            HintText = "The cheapest paid road: a key from platform.deepseek.com, about half the cost of gpt-5.6-luna per exchange for near-equal answers. Two things to know: prices DOUBLE during Beijing peak hours (09:00-12:00 and 14:00-18:00 UTC+8 - European evenings fall in the cheap window), and their servers are in China.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string DeepSeekApiKey { get; set; } = string.Empty;

        [SettingPropertyDropdown("DeepSeek model", Order = 14, RequireRestart = false,
            HintText = "deepseek-v4-flash ($0.14/$0.28 per MTok) is the value pick and scores within a point of gpt-5.6-luna. deepseek-v4-pro ($0.44/$0.87) is the stronger sibling, still cheaper than most. Both carry the NPCs' tools.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public Dropdown<string> DeepSeekModel { get; set; } = new Dropdown<string>(McmChoiceLists.DeepSeekModels, 0);

        [SettingPropertyText("Local server URL", Order = 15, RequireRestart = false,
            HintText = "Where your local AI server listens, when the backend is Local. LM Studio: http://localhost:1234/v1 (start its server on the Developer tab). Ollama: http://localhost:11434/v1. Blank resets to the LM Studio default. A key, if your server wants one, goes in LocalApiKey in config.json.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string LocalEndpoint { get; set; } = string.Empty;

        [SettingPropertyText("Local model id", Order = 16, RequireRestart = false,
            HintText = "UNSUPPORTED, at your own risk. The EXACT id your server serves (LM Studio: qwen/qwen3-30b-a3b; Ollama: qwen3:30b). It must be an instruct model with native tool calling AND with thinking/reasoning turned OFF in your server — a thinking model spends its whole budget in silence and answers '...'.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string LocalModel { get; set; } = string.Empty;

        [SettingPropertyInteger("Local context length", 2048, 131072, "0", Order = 17, RequireRestart = false,
            HintText = "The context window your server ACTUALLY loads the model with (LM Studio's context-length setting; Ollama's num_ctx). The NPCs' memory budget scales against this — claiming more than is truly loaded means silent truncation and strange amnesia. Match your server.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public int LocalContextWindow { get; set; } = 16384;

        [SettingPropertyText("Custom endpoint (advanced)", Order = 18, RequireRestart = false,
            HintText = "Empty = the real OpenAI. For another OpenAI-compatible CLOUD service (NanoGPT…), paste its base URL ending in /v1, put its key in the OpenAI key field, and type its model id in the custom OpenAI model field above. For OpenRouter or a local server, pick those backends instead.")]
        [SettingPropertyGroup("Connection", GroupOrder = 0)]
        public string OpenAIBaseUrl { get; set; } = string.Empty;

        [SettingPropertyInteger("Reply length (max tokens)", 100, 2000, "0", Order = 19, RequireRestart = false,
            HintText = "Roughly how long an NPC's spoken reply may run. Higher means longer answers but slower, pricier calls. 400 is a good balance. On Gemini this is raised to at least 1500 behind the scenes, because its thinking is charged against the same ceiling and would otherwise leave nothing to say.")]
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

        [SettingPropertyBool("Show socialness control on the map", Order = 2, RequireRestart = false,
            HintText = "The small stepper in the map's lower right that adjusts socialness live. Turn it off to clear the map — the slider above still sets the pace; it hides or returns at once, no restart.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool ShowSocialnessControl { get; set; } = true;

        [SettingPropertyBool("Letters", Order = 3, RequireRestart = false,
            HintText = "Distant NPCs may write letters that travel real in-game days, and you can send letters by courier from any settlement.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool EnableLetters { get; set; } = true;

        [SettingPropertyBool("Recall the world's truth", Order = 4, RequireRestart = false,
            HintText = "NPCs may look up live campaign facts mid-reply — family, who holds a town, who is at war — instead of misremembering. Recommended.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool EnableWorldRecall { get; set; } = true;

        [SettingPropertyBool("All they have read and heard (web search)", Order = 5, RequireRestart = false,
            HintText = "NPCs may quietly search the internet mid-reply when asked how something in the world is done — framed to them as their own reading and hearsay. Uses DuckDuckGo, no key needed; the search queries leave your machine, so turn this off if you prefer everything to stay between you and your AI provider.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public bool EnableWebSearch { get; set; } = true;

        [SettingPropertyInteger("Lasting truths per NPC", 1, 30, "0", Order = 6, RequireRestart = false,
            HintText = "How many distilled 'known facts' about you an NPC may carry at once. Higher means a longer memory but a larger, pricier prompt.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public int MaxKnownFacts { get; set; } = 10;

        [SettingPropertyInteger("Personal aims per NPC", 1, 20, "0", Order = 7, RequireRestart = false,
            HintText = "How many personal goals an NPC may hold at once — what they strive for of their own will. Kept small so their striving stays focused.")]
        [SettingPropertyGroup("Life of the NPCs", GroupOrder = 2)]
        public int MaxNpcGoals { get; set; } = 6;

        [SettingPropertyBool("Memories rewind with your saves", Order = 8, RequireRestart = false,
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
            var keys = McmChoiceLists.HotkeyKeys;
            var index = 0;
            for (var i = 0; i < keys.Length; i++)
                if (keys[i] == preferred) { index = i; break; }
            return new Dropdown<string>(keys, index);
        }
    }
}
