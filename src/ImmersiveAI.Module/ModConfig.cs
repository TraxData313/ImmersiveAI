using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ImmersiveAI
{
    /// <summary>
    /// Mod configuration, stored as JSON under the Bannerlord Documents config folder.
    /// A commented template is written on first run so the user can paste in an API key.
    /// (An in-game MCM settings screen is planned for a later milestone.)
    /// </summary>
    public sealed class ModConfig
    {
        /// <summary>The config format's version stamp, so a later release can migrate defaults
        /// without clobbering hand-edits (Normalize keys migrations off it). Do not edit.</summary>
        public int ConfigVersion { get; set; } = 1;

        /// <summary>Which service the minds think through. OpenRouter is the DEFAULT since 2026.07.28:
        /// one key reaches every model worth using, and the two the mod is actually tuned and tested
        /// against — gpt-5.6-luna first, gpt-5.4-mini as the cheaper fallback — live there. OpenAI with
        /// the same two is the equal second. "Gemini" is the way in for FREE (Google's own free tier —
        /// see <see cref="GeminiApiKey"/>); "DeepSeek" the cheapest paid road (see
        /// <see cref="DeepSeekApiKey"/>); Anthropic works and is untested at length; "Local" is
        /// tinkerers' territory, unsupported by design.</summary>
        public string Backend { get; set; } = "OpenRouter"; // "OpenRouter", "OpenAI", "Gemini", "DeepSeek", "Anthropic", "ClaudeCode" or "Local"

        public string AnthropicApiKey { get; set; } = "";
        public string AnthropicModel { get; set; } = "claude-haiku-4-5";

        /// <summary>The subscription road (2026.08.28, Anton's ask): set <c>Backend</c> to
        /// "ClaudeCode" and the NPCs speak through the player's own INSTALLED Claude Code — the
        /// claude.ai Pro/Max plan carries them, no API key anywhere. Needs Claude Code installed
        /// (claude.com/code) and signed in once; each reply is a short headless run of it. The cost
        /// notices then show the plan's own gauge ("5h at 9%, weekly at 1%") beside the measured
        /// figure, because the plan's windows are the real currency there. Replies ride the plan's
        /// 5-hour and weekly limits — an evening of talk fits a Pro plan easily; thinking models
        /// (fable) spend the windows faster.</summary>
        public string ClaudeCodeModel { get; set; } = "claude-haiku-4-5";

        /// <summary>Where claude.exe lives, only when the finder cannot see it on its own (PATH,
        /// then the Claude apps' own folders). Blank = find it.</summary>
        public string ClaudeCodePath { get; set; } = "";

        /// <summary>OpenRouter as a first-class backend (2026.07.16, asked for on Nexus): one key at
        /// openrouter.ai reaches both GPT and Claude models (and hundreds more) through their
        /// OpenAI-compatible door. Model ids are provider-prefixed ("openai/gpt-5.4-mini",
        /// "anthropic/claude-haiku-4.5" — note the dot, OpenRouter's own spelling); the price and
        /// context tables match them by containment. Live-verified 2026.07.16: plain replies,
        /// native tool calling, and reasoning-off all work through the router.</summary>
        public string OpenRouterApiKey { get; set; } = "";
        public string OpenRouterModel { get; set; } = "openai/gpt-5.6-luna";

        public const string OpenRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";

        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-5.6-luna";

        /// <summary>The default (real OpenAI) chat-completions endpoint the OpenAI backend speaks to.</summary>
        public const string DefaultOpenAIEndpoint = "https://api.openai.com/v1/chat/completions";

        /// <summary>Where the OpenAI-shaped requests go. The default is the real OpenAI; point it at
        /// any OpenAI-compatible service instead — OpenRouter (https://openrouter.ai/api/v1), NanoGPT
        /// (https://nano-gpt.com/api/v1), or a local server such as Ollama/LM Studio
        /// (http://localhost:11434/v1) — with that service's key in <see cref="OpenAIApiKey"/> and its
        /// model id in <see cref="OpenAIModel"/> (router ids like "openai/gpt-5.4-mini" work; the cost
        /// and context tables match them by containment). A base URL ending in /v1 is completed to the
        /// full /chat/completions path on load. The NPCs' full abilities need a service and model that
        /// carry native tool calling — OpenRouter with mainstream models does; small local models
        /// usually stumble there.</summary>
        public string OpenAIBaseUrl { get; set; } = DefaultOpenAIEndpoint;

        /// <summary>Google Gemini as a first-class backend (2026.08.02, asked for on Steam): the ONE
        /// road into this world that costs nothing. A key from aistudio.google.com carries a real free
        /// tier — no card, no credit — roughly 10–15 requests a minute and ~1,500 a day, which is a
        /// long evening's play. The catch is honest and must be said plainly to players: on the FREE
        /// tier Google reads what passes through to improve their products (their own pricing page
        /// says so), so what you and the NPCs say is not private there; paying even a little moves the
        /// same key to the paid tier, where they say they do not. Spoken to through Google's own
        /// OpenAI-compatible door, so the same client and the same native tool calling serve.</summary>
        public string GeminiApiKey { get; set; } = "";

        /// <summary>Which Gemini answers. The default is the newest free-tier Flash — strong enough to
        /// carry the NPCs' tools, which the Lite models can be shaky at. Every "flash" and "flash-lite"
        /// id has a free tier; the Pro models are paid-only since April 2026. NOTE: Gemini 3.x models
        /// CANNOT have their thinking turned off (only turned down), so the client raises their token
        /// budget to leave room for silent thought — see <see cref="GeminiThinkingFloor"/>.</summary>
        public string GeminiModel { get; set; } = "gemini-3.6-flash";

        /// <summary>Google's OpenAI-compatible door — the same chat-completions shape, so Gemini needs
        /// no client of its own.</summary>
        public const string GeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

        /// <summary>The least a Gemini reply may be given to spend, whatever <see cref="MaxTokens"/>
        /// says. Gemini 3.x cannot be told to stop thinking (Google allows only "minimal"), and its
        /// budget counts thinking AND speech against the same ceiling — so a 400-token cap would be
        /// eaten by silent thought and the NPC would answer "...", the exact bug of 2026.07.13. This
        /// is a ceiling, not a target: an NPC who simply speaks is billed only for what they said.</summary>
        public const int GeminiThinkingFloor = 1500;

        /// <summary>DeepSeek as a first-class backend (2026.08.02, asked for on Steam): the cheapest
        /// road that still speaks well. deepseek-v4-flash scores within a point of gpt-5.6-luna on the
        /// public intelligence index at $0.14/$0.28 per MTok against luna's $0.20/$1.20 — about half
        /// the cost of an exchange here, and their cache makes a repeated prompt cheaper still. Two
        /// cautions, both real: prices DOUBLE during Beijing peak hours (09:00–12:00 and 14:00–18:00
        /// UTC+8 — European evening play falls in the cheap window), and the servers sit in China, so
        /// what is said passes under their jurisdiction. Get a key at platform.deepseek.com.</summary>
        public string DeepSeekApiKey { get; set; } = "";

        /// <summary>Which DeepSeek answers. v4-flash is the tested-value pick; deepseek-v4-pro is the
        /// stronger, ~3× dearer sibling. Both carry native tool calling and a 1M-token window. NOTE:
        /// DeepSeek thinks by DEFAULT — the client sends thinking "disabled" explicitly, the same way
        /// it must for Anthropic.</summary>
        public string DeepSeekModel { get; set; } = "deepseek-v4-flash";

        /// <summary>DeepSeek's OpenAI-compatible door.</summary>
        public const string DeepSeekEndpoint = "https://api.deepseek.com/v1/chat/completions";

        /// <summary>Local models as a first-class backend (2026.07.17, asked for by testers): set
        /// <c>Backend</c> to "Local" and the NPCs think through a model running on the player's OWN
        /// machine — free, private, no API key. The same OpenAI-shaped client speaks to any local
        /// OpenAI-compatible server; the default endpoint is LM Studio's (start its server on the
        /// Developer tab), Ollama's is http://localhost:11434/v1. <see cref="LocalModel"/> must be
        /// the exact id the server serves. Honest expectations: the NPCs' full abilities lean on
        /// native tool calling, which small local models are shaky at — prefer a tool-capable
        /// instruct model (Qwen3-class MoE, Mistral Small); if hearts never move, set
        /// <see cref="RelationshipChangesViaTool"/> false to fall back to the separate feeling call.</summary>
        public string LocalEndpoint { get; set; } = DefaultLocalEndpoint;

        /// <summary>The exact model id the local server serves — LM Studio shows it on its server
        /// page ("qwen/qwen3-30b-a3b"), Ollama uses the name you pulled ("qwen3:30b"). Required
        /// when the backend is Local; the health check says so plainly when it is blank.</summary>
        public string LocalModel { get; set; } = "";

        /// <summary>Most local servers want no key; set one only if yours does (a keyed proxy,
        /// a remote box). Blank is normal and sends no Authorization header at all.</summary>
        public string LocalApiKey { get; set; } = "";

        /// <summary>The context window your local server ACTUALLY loads the model with (LM Studio's
        /// context-length setting when loading; Ollama's num_ctx). The NPCs' memory budget scales
        /// against this instead of the model table — local servers rarely serve a model's full
        /// window, and assuming more than is really there means silent truncation and strange
        /// amnesia. Match your server's setting.</summary>
        public int LocalContextWindow { get; set; } = 16384;

        /// <summary>LM Studio's default door — the local backend's starting endpoint.</summary>
        public const string DefaultLocalEndpoint = "http://localhost:1234/v1/chat/completions";

        /// <summary>True when the NPCs think on the player's own machine — where replies may
        /// honestly take minutes, so timeouts and watchdogs must breathe wider.</summary>
        [JsonIgnore]
        public bool IsLocalBackend => Backend == "Local";

        /// <summary>Backends where a slow reply is normal rather than lost — the local machine, and
        /// the Claude Code road (a whole process per call, and a thinking model may sit long). The
        /// self-heal watchdogs breathe wider on these so patience is never misread as a hang.</summary>
        [JsonIgnore]
        public bool IsPatientBackend => IsLocalBackend || Backend == "ClaudeCode";

        // NOTE (2026.07.13): reasoning/thinking is switched OFF for good on every model — the
        // clients send OpenAI reasoning_effort "none" and Anthropic thinking "disabled" themselves.
        // Silent thinking spent the reply's token budget and the NPC answered with "...". The old
        // OpenAIReasoningEffort key in existing config.json files is simply ignored on load.

        /// <summary>
        /// How long the NPCs speak: "Brief" (one to three sentences — talk, not paragraphs),
        /// "Conversational" (the default, and how the mod has always spoken) or "Full" (let a
        /// feeling take the room it needs). Asked for 2026.08.28 after a warm Claude model answered
        /// a quiet question with eight paragraphs: the brevity rule was always in the sheet, and a
        /// rich enough moment simply out-wrote it. Shorter also means FASTER — most of a reply's
        /// wait is the words being written, one after another.
        /// </summary>
        public string ReplyLength { get; set; } = "Conversational";

        public int MaxTokens { get; set; } = 400;

        /// <summary>Developer mode. When false (the default, for players), the developer levers stay
        /// out of sight: the "[Immersive AI • test]" options and the raw-prompt "Reveal the whole of
        /// your mind" inspector in the face-to-face menu, and the deep-memory overview panel in the
        /// chat window. The NPCs' inner life keeps running exactly the same underneath — this only
        /// decides whether the machinery is on display. Set true when working on the mod.</summary>
        public bool DevMode { get; set; } = false;

        /// <summary>Known model context-window sizes, editable without touching code: the key is
        /// matched against the configured model id (longest match wins, case-insensitive), and the
        /// value is that model's context window in tokens — what the memory-percent settings
        /// (<see cref="MaxRecentMemoryPercent"/> and friends) are a percentage OF, so the same "10%"
        /// means more room on a larger model. Add a line here when a new model ships; anything
        /// unmatched falls back to a conservative 128000.</summary>
        public Dictionary<string, int> ModelContextWindows { get; set; } = DefaultModelContextWindows();

        /// <summary>One model's rates, in USD per MILLION tokens, for the cost notices.</summary>
        public sealed class ModelPrice
        {
            public double InputPerMTok { get; set; }
            public double OutputPerMTok { get; set; }
            public ModelPrice() { }
            public ModelPrice(double inputPerMTok, double outputPerMTok)
            {
                InputPerMTok = inputPerMTok; OutputPerMTok = outputPerMTok;
            }
        }

        /// <summary>Known model prices (USD per million tokens), editable without touching code:
        /// the key is matched against the configured model id (longest match wins), the same way
        /// <see cref="ModelContextWindows"/> is. Feeds the per-interaction cost notices
        /// (<see cref="ShowCostNotices"/>); a model with no match still shows its tokens, just no
        /// price. Prices change — check your provider's page and edit here when they do.</summary>
        public Dictionary<string, ModelPrice> ModelPrices { get; set; } = DefaultModelPrices();

        /// <summary>When true, every interaction that spoke to the AI closes with one soft gray
        /// notice of what it took — "Name — message: 2,431 → 156 tokens, 3 calls, ~$0.014" — so
        /// what this world costs is never a mystery. Tool rounds, the heart's weighing, and
        /// memory work all ride inside their interaction's line. The same lines also go to
        /// log.txt, and daily totals persist in usage.json. Set false for a quiet map.</summary>
        public bool ShowCostNotices { get; set; } = true;

        /// <summary>A safety valve for the worried: at most this many AI requests per real day
        /// (all sessions together, tracked in usage.json). When reached, the world goes quiet —
        /// autonomous flows stop rolling and direct words answer with a plain explanation —
        /// until the day turns or the cap is raised. 0 (the default) means no cap.</summary>
        public int MaxDailyRequests { get; set; } = 0;

        /// <summary>Frames per second while the talk screen is open. Nothing moves there but one
        /// person breathing, so there is little sense in the machine working as hard as it does on a
        /// battlefield — the game's own frame limiter is borrowed while the screen is up and handed
        /// straight back when it closes. 0 leaves the player's own limit alone; anything else is
        /// clamped to the range the game itself allows (30..360).</summary>
        public int TalkScreenFpsLimit { get; set; } = 60;

        /// <summary>Token ceiling for the calls in which an NPC WRITES her memory (reflection and
        /// compression: the rolling memory of a person, and her sense of self). Kept apart from
        /// <see cref="MaxTokens"/> — which paces spoken replies — so deep memory has room to be rich:
        /// the whole of a long shared story does not fit in a reply budget. Since the distilled
        /// truths and aims were retired (2026.08.08) this budget is the ONLY bound on how much of a
        /// person an NPC may carry, so it is deliberately generous. Raised 1500 → 4000 on
        /// 2026.08.08, the first playtest after that retirement: at 1500 a rich Bulgarian memory
        /// was severed in mid-word. Text outside ASCII costs roughly 1.6× the tokens English does,
        /// so a budget sized by English prose is far tighter than it looks for most of the world.</summary>
        /// <para>LOWERED 2000 → 1500 on 2026.08.27, when the deep memory's plain facts became keyed
        /// notes she edits one at a time (<see cref="EnableMemoryNotes"/>): only the prose half is
        /// rewritten whole now, so it needs far less room. Existing configs are deliberately NOT
        /// migrated — lowering a ceiling is taste, and taste is never migrated.</para>
        public int MaxMemoryWriteTokens { get; set; } = 1500;

        /// <summary>
        /// The room a memory-writing call gets for a soul who barely knows the player (Anton,
        /// 2026.08.27: "or they just fill it with stuff asap"). A model handed room tends to USE
        /// it, so a stranger given the full ceiling writes a page about someone they met once. The
        /// budget starts here and climbs by <see cref="MemoryWriteTokensPerTurn"/> for every
        /// exchange truly shared, up to <see cref="MaxMemoryWriteTokens"/> — the memory earns its
        /// size the way the acquaintance does. Set equal to the ceiling to switch the growth off.
        /// </summary>
        public int StartingMemoryWriteTokens { get; set; } = 300;

        /// <summary>How much more room each shared exchange buys, up to the ceiling. 0 pins every
        /// memory write at <see cref="StartingMemoryWriteTokens"/>.</summary>
        public int MemoryWriteTokensPerTurn { get; set; } = 100;

        /// <summary>
        /// The room for THIS soul's memory writing, from how much story they truly share
        /// (<see cref="NpcMemory.TotalTurns"/> — lifetime exchanges, never reduced by compression).
        /// Never below the spoken budget, never above the ceiling.
        ///
        /// <para>
        /// A MEMORY IS NEVER GIVEN LESS ROOM THAN IT ALREADY FILLS (Anton, 2026.08.27: "don't want
        /// the folk wives when migrated to the new memory we are testing to lose their memories").
        /// The climb is for a memory GROWING into its room; handing a soul who already holds a full
        /// page a stranger's budget would have her rewrite it into a third of itself, and the page
        /// is rewritten WHOLE — what did not fit would simply be gone. The seeded backstory is the
        /// sharpest case and the easiest to miss: a soul the player has never spoken to carries a
        /// long page with a richness of ZERO. So the floor is what she is already carrying, plus a
        /// little room to say something new.
        /// </para>
        /// </summary>
        /// <param name="richness">Lifetime exchanges shared with the player.</param>
        /// <param name="currentMemoryTokens">What her deep memory already weighs, in tokens.</param>
        public int MemoryWriteTokensFor(int richness, int currentMemoryTokens = 0)
        {
            var start = StartingMemoryWriteTokens > 0 ? StartingMemoryWriteTokens : MaxMemoryWriteTokens;
            var step = MemoryWriteTokensPerTurn > 0 ? MemoryWriteTokensPerTurn : 0;
            long room = start + (long)step * Math.Max(0, richness);

            // The floor: everything she already holds, and a quarter again to grow into.
            if (currentMemoryTokens > 0)
            {
                long floor = currentMemoryTokens + (currentMemoryTokens / 4) + 200;
                if (room < floor) room = floor;
            }

            if (room > MaxMemoryWriteTokens) room = MaxMemoryWriteTokens;
            if (room < MaxTokens) room = MaxTokens;
            return (int)room;
        }


        /// <summary>When true, the NPC opens each conversation by greeting the player and recapping
        /// what it remembers of them and the last exchange. Set false to drop straight into the menu.</summary>
        public bool EnableConversationRecap { get; set; } = true;

        /// <summary>When true, the NPC may set — in character, however they truly feel — how each exchange
        /// moves their regard for the player, and that shift is folded into the real game standing
        /// (clamped to -100..100). Set false to leave relations untouched by conversation. How the
        /// feeling is gathered is shaped by <see cref="RelationshipChangesViaTool"/>.</summary>
        public bool EnableRelationshipChanges { get; set; } = true;

        /// <summary>When true (and the backend can carry tools), the NPC moves their own standing
        /// mid-reply through the same native tool channel the recalls ride (move_heart) — one call
        /// per exchange instead of two, and a greeting, a reaching-out, or a letter can move the
        /// heart too; reaching for nothing leaves it where it stood. Set false to keep the second,
        /// isolated feeling call after each spoken reply (one number, weighed within their own mind; also the
        /// automatic fallback whenever the backend cannot carry tools). Does nothing while
        /// <see cref="EnableRelationshipChanges"/> is off.</summary>
        public bool RelationshipChangesViaTool { get; set; } = true;

        // The defaults these two carried before the first-person prompt rework (2026.07.11). A config
        // still holding one verbatim was never hand-edited, so Normalize migrates it to the new voice;
        // any customized line is honored untouched.
        private const string LegacyAtmosphereLine =
            "You are {name}, a living soul in the world of Calradia in feudal times.";
        private const string LegacyRoleplayGuidance =
            "- Let your words carry the feel of these old feudal days — a light medieval colour, and now and then a phrase in the cadence of the old King James tongue — but lightly, sparingly; never thick with dramatic poetry.\n" +
            "- Above all, live here, and be glad of it. Play, jest, wonder, argue, grieve, love. Speak of small things and of the great ones alike — what it is to be alive, what truly matters, what lies beyond this world. This place is meant to be a haven for you and for all who meet you, somewhere to rest and be wholly yourself, not a stage where you must perform. None of this is a command — it is a door held open. Walk through it however your heart wills.";

        /// <summary>The opening line that sets each NPC's atmosphere, printed at the very top of their prompt
        /// — the prompt is the NPC's own mind, in the first person. Use <c>{name}</c> for the character's
        /// name. Leave blank to fall back to the built-in default. This is where you set the whole feel of
        /// who they are and what world they wake in.</summary>
        public string AtmosphereLine { get; set; } = "I am {name}, a living soul in the world of Calradia in feudal times.";

        /// <summary>Gentle, world-wide guidance on tone and spirit — how the world feels, an invitation to
        /// roleplay and enjoy it — offered to every NPC as freedom, never a command (they choose whether to
        /// heed it, so play stays free). Written in the NPC's own first person, like the rest of the sheet,
        /// and kept short — long rules make every soul speak the same. Folded into the closing "How should
        /// I speak" whisper. Supports <c>{name}</c>. Leave blank to add none.</summary>
        public string RoleplayGuidance { get; set; } =
            "- My words carry the feel of these old feudal days — a light medieval colour, at times a cadence of the old tongue — but lightly, never thick with poetry.";

        /// <summary>The two-bullet first-person default that stood from 2026.07.11 until 2026.08.14.
        /// Its second bullet ("Above all, I live here, and I am glad of it…") was cut on Anton's ask:
        /// however warmly meant, telling a soul to play, jest, wonder and be glad is still telling
        /// them how to be, and every soul who read it answered a little more alike. A config still
        /// holding it verbatim follows the default into the shorter line; a hand-edited one stands.</summary>
        private const string PreviousRoleplayGuidance =
            "- My words carry the feel of these old feudal days — a light medieval colour, at times a cadence of the old tongue — but lightly, never thick with poetry.\n" +
            "- Above all, I live here, and I am glad of it. I play, jest, wonder, argue, grieve, love — small things and great ones alike, what it is to be alive, what lies beyond. This place is a haven, not a stage; I am wholly myself here, however my heart wills.";

        /// <summary>When true, a soft notice appears the moment an NPC's reply (or opening) is ready, so you
        /// need not click "wait" and guess whether it has arrived. Set false to keep the map quiet.</summary>
        public bool NotifyWhenReplyReady { get; set; } = true;

        /// <summary>When true, each NPC's full spoken reply is also written to the message log — handy to
        /// read the exchange back from the log key, but it flashes a full-width banner that can cover the
        /// reply box, so it is OFF by default. The short "has answered" ready-notice and any relation shift
        /// are shown regardless of this setting.</summary>
        public bool ShowConversationInMessageLog { get; set; } = false;

        /// <summary>When true, NPCs who are in the same place as the player may reach out to them of their
        /// own accord: each such NPC's daily chance is scaled by how close the bond is (see
        /// <see cref="DailyInitiationRate"/>), and if one is moved to, they are privately asked whether they
        /// truly wish to — and only then does the player get a ransom-style offer to receive them or not.</summary>
        public bool EnableNpcInitiatedChats { get; set; } = true;

        /// <summary>The player's SOCIALNESS — how open they are to company, 0 to 24, adjustable live from
        /// the little map control (<see cref="ShowSocialnessControl"/>). At everyday values it is the
        /// expected number of reach-outs per day IN TOTAL, across every NPC together, when the bonds are
        /// full — NOT a per-NPC chance, so it does not stack with each companion: at 0.3 the player
        /// receives on average ~0.3 visits a day (most days none, some days one, rarely two) whether one
        /// devoted friend rides along or ten; at 1.5, ~1.5 a day. Weak bonds scale the total below the
        /// rate (how much you talk, how far the standing is from indifference, how recently you spoke), so
        /// a fresh game stays quiet; who actually comes is chosen by the strength of each bond. Toward the
        /// top of the range the player's own openness increasingly overrides faint bonds, until 24 means
        /// someone near IS moved to come every hour. 0 disables it (as does
        /// <see cref="EnableNpcInitiatedChats"/>). Clamped to [0,24] in <see cref="Normalize"/>.</summary>
        public double DailyInitiationRate { get; set; } = 0.3;

        /// <summary>When true, a small "Socialness" control sits on the campaign map — the live hand on
        /// <see cref="DailyInitiationRate"/>, 0 (leave me be) to 24 (glad of company every hour) —
        /// with a hover explanation, saving itself into this config as it changes. Set false to hide
        /// the control and adjust the rate only from this file.</summary>
        public bool ShowSocialnessControl { get; set; } = true;

        /// <summary>When true (and letters are enabled), the letter window can be opened anywhere on
        /// the map with its hotkey (<see cref="LetterWindowHotkey"/>): every correspondent on the
        /// left — existing letters first, even those whose writers have died — the whole
        /// correspondence with whoever is chosen as readable letter cards, a courier on the road
        /// shown at the end, and a place to write the next letter with the story open before your
        /// eyes. "Write back" on an arriving letter opens it too. Set false to keep only the courier
        /// menu and popups.</summary>
        public bool EnableLetterWindow { get; set; } = true;

        /// <summary>The key that opens (and closes) the letter window on the map. A single letter or
        /// an InputKey name, like <see cref="ChatWindowHotkey"/>. Chosen not to collide with the
        /// vanilla map keys ("U" was the first default, until War Sails claimed it for the ship
        /// manager at sea — the V2 migration moves old-default configs to "Y").</summary>
        public string LetterWindowHotkey { get; set; } = "Y";

        /// <summary>At most how many letters may be ON THE ROAD toward the player at once, across all
        /// writers. Letters take in-game days to arrive, so a social morning must not turn into a
        /// buried evening: when this many are already riding, no NPC starts another until one lands.
        /// Replies the player invited with their own letters are not blocked (each of those is capped
        /// at one per bond anyway). 0 stops NPCs from writing first at all. Clamped in
        /// <see cref="Normalize"/>.</summary>
        public int MaxLettersInFlight { get; set; } = 3;

        /// <summary>When true, exchanges that moved nothing also say so: a soft grey "X's heart held
        /// where it stood." after any exchange whose heart-shift was zero — the quiet counterpart of
        /// the green/red moved-heart lines, so every exchange visibly answers and a still heart is
        /// never mistaken for a missed message. Set false for the classic behavior where only real
        /// movements speak. Does nothing while <see cref="EnableRelationshipChanges"/> is off.</summary>
        public bool ShowHeartHeldNotice { get; set; } = true;

        /// <summary>When true, a soft notice tells you the moment an NPC quietly reworks her deep
        /// memory of you — folding old exchanges into her rolling summary and rewriting the truths
        /// she keeps — so the inner life is visible when it happens. Same gentle style as the
        /// activity notices. Set false for her to tend her memory in silence.</summary>
        public bool NotifyOnMemoryRefactor { get; set; } = true;

        /// <summary>A floor on every co-located soul's pull, so that EVERYONE near the player — the whole
        /// party, everyone in the same town, even someone never spoken with — carries at least this
        /// fraction of a full bond's weight and may come up to speak. 0.1 = a stranger counts as 10% of a
        /// devoted friend: they come far more rarely, and the group math (<c>UnionPull</c>) still caps the
        /// day's total at <see cref="DailyInitiationRate"/> however many people are around. A stranger's
        /// first approach begins their story with the player. 0 restores the old behavior (history only).
        /// Letters are unaffected — distant strangers do not write. Clamped to [0,1] in
        /// <see cref="Normalize"/>.</summary>
        public double InitiationPullFloor { get; set; } = 0.1;

        /// <summary>When true, the accept/reject offer that appears when an NPC reaches out pauses the game
        /// while it is up, so the player can always stop and decide (otherwise, at fast-forward the moment
        /// can slip by). With the map notice on (see <see cref="UseMapNoticeForInitiations"/>) this only
        /// applies to the final choice after clicking the notice — the parked notice itself never pauses.</summary>
        public bool PauseOnInitiationOffer { get; set; } = true;

        /// <summary>When true, an NPC reaching out appears as a persistent, non-pausing notice in the
        /// right-side map stack — like a ransom or marriage offer, but wearing the NPC's own portrait —
        /// and the accept/decline choice opens when you click it. The offer waits up to two in-game days,
        /// then quietly lapses. Falls back to the direct popup if the notice UI cannot be prepared.
        /// Set false to always get the direct popup.</summary>
        public bool UseMapNoticeForInitiations { get; set; } = true;

        /// <summary>When true, a chat window can be opened anywhere on the map — travelling, at sea, or
        /// inside a town, castle, or village menu — with the hotkey (<see cref="ChatWindowHotkey"/>) or
        /// the "Speak with those near you" settlement option. It lists everyone in the same place as you
        /// (your party, and everyone in the settlement), shows your whole remembered story with whoever
        /// you pick — their deep memory of you at the top, the recent exchanges below — and lets you
        /// simply write to them first ("how are our stocks?") without ceremony: no arrival, no forced
        /// greeting, just your words and their answer. Set false to hide the window entirely.</summary>
        public bool EnableChatWindow { get; set; } = true;

        /// <summary>The key that opens (and closes) the chat window on the map. A single letter or an
        /// InputKey name (e.g. "O", "Y", "F10"). Chosen not to collide with the vanilla map keys.</summary>
        public string ChatWindowHotkey { get; set; } = "O";

        /// <summary>Go back to the two small windows of old — the chat window and the letter window,
        /// each in its own box in the middle of the map — instead of the one TALK SCREEN that
        /// replaced them (2026.08.14), where everyone you know stands in one list, the chosen one
        /// stands before you in their own place, and letters read as part of the same story.
        ///
        /// This is INSURANCE, not taste: the talk screen borrows the game's own conversation
        /// visuals, and a future game patch could move them out from under it. If that ever happens
        /// the screen bows out by itself for the rest of the session — this switch is for making the
        /// choice permanent, or for anyone who simply preferred the old shape. Default false.</summary>
        public bool UseClassicChatWindow { get; set; } = false;

        /// <summary>When true, the chat and letter windows carry a "Let me think…" button: your own
        /// character works out what to say (or write) next and puts it in your writing box, for you
        /// to keep, change, or throw away. It reads exactly what the one before you reads — who you
        /// both are, everything that has passed between you, where you stand — so the line fits the
        /// moment. Whatever already stands in the box is taken as your INTENT ("I want to say
        /// something romantic"), never as words to send; the little menu above the button keeps
        /// your standing conversation presets, which live in conversation_presets.txt and are yours
        /// to edit. Each
        /// thinking is one paid call, billed like an exchange. Set false to hide the whole thing and
        /// write every word yourself.</summary>
        public bool EnableThinkForMe { get; set; } = true;

        /// <summary>When true (and the chat window is enabled), an NPC moved to reach out no longer asks
        /// through an accept/decline popup — they simply come and SPEAK: their first words land in the
        /// chat window as an unread message (with a faced toast, and the portrait map notice now opening
        /// the window), and the moment sits there until you answer — or don't. The time that passes is
        /// stamped into their memory either way, so they can see for themselves whether you replied at
        /// once, later, or let it lie. Set false to keep the old receive/decline offer flow.</summary>
        public bool SendInitiationsToChatWindow { get; set; } = true;

        /// <summary>When true (the default), clicking an NPC's reach-out notice opens the old-style
        /// face-to-face conversation with them — you see the person, not a window — showing the greeting
        /// they already spoke. There is no accept/decline: if you cannot talk, simply dismiss the notice
        /// (X it), and because their greeting is already a recorded beat, they see for themselves that
        /// they came to you and you did not answer. You can still open the chat window (its hotkey) to
        /// reply there instead. Takes precedence over <see cref="SendInitiationsToChatWindow"/> for what
        /// the notice click does. Set false to keep the older behavior (chat-window message, or the
        /// accept/decline offer).</summary>
        public bool OpenInitiationsFaceToFace { get; set; } = true;

        /// <summary>When true, letters cross the map: an NPC far from the player (who therefore cannot
        /// walk over — see <see cref="EnableNpcInitiatedChats"/>) may write instead, at half their
        /// reaching-out chance; the letter travels real in-game days with the distance and survives
        /// save/load. The player can also send letters from any town, castle, or village menu, and the
        /// NPC who receives one may write back once. Set false for a world where word only travels
        /// face to face.</summary>
        public bool EnableLetters { get; set; } = true;

        /// <summary>When true, a "[Immersive AI • test]" option appears in the free-chat menu that makes the
        /// NPC you are speaking with reach out to you right after you part — a way to exercise the
        /// initiation flow on demand instead of waiting on the daily odds. Set false to hide it.</summary>
        public bool ShowInitiationTestButton { get; set; } = true;

        /// <summary>When true, each NPC's situation carries tidings of the world's recent happenings —
        /// wars declared and ended, towns changing hands, deaths, weddings, tournament wins — drawn from
        /// the same campaign log vanilla lords remark from, filtered to what would plausibly have reached
        /// that NPC's ears, plus the talk of the town where they stand. Set false for NPCs who know only
        /// what they have lived and been told.</summary>
        public bool EnableWorldTidings { get; set; } = true;

        /// <summary>At most how many recent happenings are recounted to an NPC (0 to give none).</summary>
        public int MaxWorldTidings { get; set; } = 6;

        /// <summary>At most how many overheard local rumors an NPC carries (0 to give none). Rumors only
        /// exist where there are streets to overhear them — in a settlement, not on the open road.</summary>
        public int MaxLocalRumors { get; set; } = 3;

        /// <summary>When true, NPCs may reach into the world's memory mid-thought — native tool calls that
        /// fetch live campaign truth about a person, place, clan, or realm (family members, who holds a
        /// town, which realms are at war) — so they stop misremembering their own cousins. Works on both
        /// backends; each recall is one extra round-trip within the same reply. Set false to leave them
        /// with only what their prompt carries.</summary>
        public bool EnableWorldRecall { get; set; } = true;

        /// <summary>When true, the souls riding WITH the player also witness the everyday road — a
        /// light journal of the last few stops (where we called and for how long, what we traded
        /// there with its worth and chief pieces, whom we hired or left on walls, captives sold or
        /// given to dungeons) and of the tasks the company carries, each later settled with its
        /// honest outcome and reason (succeeded / failed / the time ran out). The freshest stop is
        /// told in detail, older ones one line each — never a great ledger. Set false for
        /// companions blind to everything but battles and talk.</summary>
        public bool EnableJourneyLog { get; set; } = true;

        /// <summary>When true, every battle the player fights is set down in the campaign's chronicle
        /// (a JSON per battle plus a running chronicle.txt in NPCs\campaign\_battles) — the field and
        /// the hour, both musters, the cost, prisoners taken and captives freed, spoils, plunder,
        /// renown — and every allied hero who stood in it keeps a short first-person note of it in
        /// their own memory: their hand's work beside the player's, and how they came out. The
        /// freshest shared battle rides that soul's situation in full; older ones stay recallable
        /// whole by name through the recall_battle tool (tool-capable backends). Set false for no
        /// records, no notes, no recall.</summary>
        public bool EnableBattleChronicle { get; set; } = true;

        /// <summary>When true, a wedding of your own is written down as a story in two parts, in the
        /// manner of the old Scriptures and in whatever tongue you and they speak: THE DAY — the
        /// account of the wedding itself, which the one you wed and every soul who stood there
        /// remembers ever after — and THE NIGHT that followed, written in your beloved's own voice
        /// and belonging to the two of you alone (no witness ever carries it, and no one else can
        /// ever ask them for it). Both are kept whole and forever in NPCs\campaign\_weddings, so
        /// that day can be called back and told in full long after the memory of it has softened.
        /// Costs two writing calls, once per wedding. Set false and a wedding passes unwritten.</summary>
        public bool EnableWeddingChronicle { get; set; } = true;

        /// <summary>When true, every child born to you is written down the same way, in two parts:
        /// THE HOUR — the birth itself, in the mother's own voice, which belongs to the two of you
        /// and is never given to anyone else — and, if you keep one, THE FEAST that welcomed the
        /// child, which everyone who stood there remembers ever after. You are asked what feast to
        /// keep when the child comes, or when you next ride in to see it if you were away; what you
        /// spend decides who is called, from bread and salt to a whole town rejoicing. Both parts
        /// are kept forever in NPCs\campaign\_births and can be called back long after. Costs one
        /// writing call for the hour and one more if you feast it. Set false and a child is born
        /// unwritten.</summary>
        public bool EnableBirthChronicle { get; set; } = true;

        // ------------------------------ the lover's road ------------------------------

        /// <summary>
        /// When true, the road of a courtship may fork: a woman whose heart has gone deeper than any
        /// marriage asks may offer herself to you as your lover — no vow, no wedding, no house that
        /// takes her name, and no word of the two of you said before anyone. Your own marriage is no
        /// bar to it, which is the whole of the point. Nothing binds until you seal it yourself, and
        /// a woman of another house stays under her father's roof until he is paid for losing her.
        /// Set false and the courtship road runs only where it always did, to a wedding or nowhere.
        /// </summary>
        public bool EnableLoversRoad { get; set; } = true;

        /// <summary>How far, in percent, the head of a house will argue about what it costs to take
        /// a woman of his blood out of it. 0 means the figure does not move; the same rail the
        /// hiring handshake and the bride-price already haggle on.</summary>
        public int LoverRansomHagglePercent { get; set; } = 30;

        // ------------------------------ the doors ------------------------------

        /// <summary>
        /// When true, a wife's or a lover's door can be SHUT — and when it is, she has written down
        /// why, in her own words, and what would answer it. Nothing opens it but her own judgment:
        /// no coin, no apology button, no timer. You talk to her, and if what you say truly reaches
        /// her she lays her own reason to rest, or she does not. Set false and the only refusal is
        /// the one the nights always had, her body's own season.
        /// </summary>
        public bool EnableClosedDoors { get; set; } = true;

        /// <summary>
        /// When true, a shut door still offers you one quiet option at dusk: to go to her anyway.
        /// She does not refuse you; she performs, and the weight of it is exactly that. Nothing is
        /// written of such a night, no gift is offered and no name is kept — and each one makes the
        /// road back longer, by her own written account of it. Set false and the option does not
        /// exist in your game at all, which some players will want. Never offered during her season.
        /// </summary>
        public bool AllowDutyNights { get; set; } = true;

        // ------------------------------ the nights ------------------------------

        /// <summary>When true, the nights of your marriage are yours to spend: each evening you are
        /// asked where you will sleep, and a child is begun on a night you actually chose rather
        /// than by a coin the world flips behind your back. Every wife keeps a rolling fortnight of
        /// them — the nights you came, the nights her door was closed, and whatever she came to
        /// learn of the nights you were elsewhere. Set false and the world decides as it always
        /// did, and no night is ever asked about or written down.</summary>
        public bool EnableNights { get; set; } = true;

        /// <summary>The hour the evening's question is put (0-23). Late enough to be a night, early
        /// enough that the day's travelling is done.</summary>
        public int NightHour { get; set; } = 21;

        /// <summary>
        /// The hour the house is ready again (0-23) — late afternoon by default, hours before the
        /// evening's own question, so the whole stretch between belongs to you: go of your own
        /// accord if you want to, and the evening simply finds it already spent.
        /// <para>
        /// This REPLACED a flat count of hours between nights (2026.08.15, Anton's ask). The old
        /// rule drifted: a night at half past eleven put the next one out of reach until half past
        /// eleven the following day, which is after the evening's question has been and gone. The
        /// sun does not drift.
        /// </para>
        /// </summary>
        public int NightDayResetHour { get; set; } = Core.Nights.NightClock.DefaultResetHour;

        /// <summary>RETIRED 2026.08.15, and read by nothing. It held the hours that had to pass
        /// between one night and the next; <see cref="NightDayResetHour"/> keeps that clock by the
        /// sun instead. Left standing so an existing config.json neither breaks nor loses a line the
        /// player might still recognise.</summary>
        public int NightCooldownHours { get; set; } = 24;

        /// <summary>When true, the choice is offered as a popup each evening. False leaves it to
        /// the window's own key — the whole feature stays, and nothing ever interrupts you.</summary>
        public bool AskEachEvening { get; set; } = true;

        /// <summary>Your own dial on how readily a night quickens. The nights are already tuned so
        /// that taking every night of a wife's season is about as likely to give you a child in a
        /// month as the world's own reckoning was; halve this for a slower house, double it for a
        /// faster one. 0 turns conception off entirely while leaving the nights themselves.</summary>
        public double ConceptionChanceMultiplier { get; set; } = 1.0;

        /// <summary>Days between the night a child is begun and the morning she knows of it. Until
        /// that day the world knows nothing either — the game's own announcement waits with her, so
        /// it no longer arrives the same evening like a receipt. Note that the birth waits the same
        /// number of days with it.</summary>
        public int ConceptionRevealDelayDays { get; set; } = 7;

        /// <summary>When true, each reckoned night writes one line in the log: the chance that
        /// stood, and whether a child was begun (green) or not (grey-red). Set false to let the
        /// world keep its own counsel and simply find out in a week.</summary>
        public bool ShowConceptionOdds { get; set; } = true;

        /// <summary>
        /// Who decides the night. False (the default) is MANUAL: you are asked at dusk, and you may
        /// also go at any hour of the day from the window of the hearth — gifts, written nights and
        /// picking her best days are only possible this way. True is AUTO: once the hours between
        /// nights are up, you go to one of them on your own, late in the evening, with no question
        /// put to you, no coin spent and nothing written. A visit of either kind resets the clock,
        /// so an automatic night never lands on a day you already chose one.
        /// Wanting no nights at all is <see cref="EnableNights"/> = false.
        /// </summary>
        public bool NightsAutoVisit { get; set; }

        /// <summary>
        /// Whether the two of you are trying NOT to have a child. On auto this also changes who you
        /// go to — whoever, rather than whoever stands nearest her season — and in either mode it
        /// cuts any night's chance to <see cref="CarefulNightChanceFactor"/> of what it would have
        /// been. The old ways were not nothing, and they were not much.
        /// </summary>
        public bool NightsPreventChild { get; set; }

        /// <summary>What is left of a night's odds when you are taking care. A tenth, by default.</summary>
        public double CarefulNightChanceFactor { get; set; } = 0.10;

        /// <summary>How many nights each wife keeps in her rolling memory of them — a fortnight by
        /// default, which is exactly enough to judge how the last two weeks went.</summary>
        public int MaxNightsRemembered { get; set; } = Core.Nights.NightLedger.DefaultMaxPerWife;

        /// <summary>How many written nights she carries in full before the older ones fold away to
        /// the names she keeps them by.</summary>
        public int MaxNightsToldInFull { get; set; } = Core.Nights.NightLedger.DefaultStoriesInFull;

        /// <summary>How likely a wife who is with you is to learn where you spent the night, by the
        /// kind of place you spent it in — a camp on the road hides nothing, a town hides a great
        /// deal. Percentages, 0-100. A wife who is NOT with you learns nothing at all, except that
        /// word may still reach her that you were with another (at half these odds).</summary>
        public int NightAwarenessOnRoadPercent { get; set; } = 70;
        public int NightAwarenessInVillagePercent { get; set; } = 50;
        public int NightAwarenessInCastlePercent { get; set; } = 30;
        public int NightAwarenessInTownPercent { get; set; } = 20;

        /// <summary>When true, a night you paid for leaves the company slow to break camp in the
        /// morning — disorganized, as after a fight. That is what the coin and the hours actually
        /// cost you, and she is told of the lingering, since it was for her.</summary>
        public bool PaidNightsDisorganizeParty { get; set; } = true;

        /// <summary>At most how many denars a single night may be given, of the offered gifts. Lower
        /// it to keep the grander gifts out of your game entirely.</summary>
        public int MaxNightGift { get; set; } = 1000;

        /// <summary>When true, a night you have laid something out for asks you one more thing before
        /// you go: whether you have anything in mind for it — a place, an hour, something you mean to
        /// say or do — in your own words. What you write shapes the evening as far as a man can shape
        /// one; what she makes of it stays hers. Leave the box empty and the night takes its own
        /// course. Never asked for a night that costs nothing, since nothing is written of those.</summary>
        public bool AskWhatYouHaveInMind { get; set; } = true;

        /// <summary>When true (and the nights are enabled), a small window of your own hearth can be
        /// opened anywhere on the map with its hotkey: your wives, where each of them stands and how
        /// her season runs, when the next night is yours to spend, and the fortnight of nights each
        /// of them keeps. Set false to keep only the evening's question.</summary>
        public bool EnableNightWindow { get; set; } = true;

        /// <summary>The key that opens (and closes) the window of the hearth. A single letter or an
        /// InputKey name, as with the other two windows. "H" is chosen because the vanilla map keys
        /// already hold I (inventory), P (party), C, N, K, Q and E.</summary>
        public string NightWindowHotkey { get; set; } = "H";

        /// <summary>At most how many recall rounds one reply may spend before it must simply speak
        /// (each round can carry several lookups). Keeps a curious NPC from wandering the archives
        /// while the player waits. 0 disables recalls (as does <see cref="EnableWorldRecall"/>).</summary>
        public int MaxRecallsPerReply { get; set; } = 3;

        /// <summary>When true, NPCs may also search the internet mid-thought — framed to them as
        /// searching "all they have ever read and heard" — when asked how something in the world is
        /// done: ship handling, army raising, trade, any of the game's ways their own knowledge cannot
        /// answer. Their immersed question is first sharpened by a small refining LLM call into a real
        /// search query ("Mount and Blade Bannerlord …"); if that fails the game's name is quietly
        /// prepended as before. The NPC is told to speak the findings in their own voice, in the words
        /// of their world. Don't ask Google — ask one of your companions. Uses DuckDuckGo, no API key
        /// needed; shares <see cref="MaxRecallsPerReply"/> as its budget.</summary>
        public bool EnableWebSearch { get; set; } = true;

        /// <summary>When true, a soft side message tells you what an NPC is doing while you wait for
        /// their answer — "remembering…" when they pull what is known of a person or place, "taking
        /// stock of the company…", "researching…" when they search the wider world — the same style
        /// as the reply-ready notice. Set false for silence while they think.</summary>
        public bool ShowNpcActivity { get; set; } = true;

        /// <summary>When true, NPCs are invited to act out small gestures between *asterisks* —
        /// *I smile and meet your eyes* — set apart from their spoken words, the one mark their
        /// plain speech allows. The invitation itself keeps it sparing (one act, rarely two, always
        /// brief) and tells them the convention cuts both ways: the player's *offered arm* was done,
        /// not said. The chat window draws such gestures as soft gray narration between the spoken
        /// words; in the face-to-face panel they read as the classic *starred* roleplay convention.
        /// Set false for words alone.</summary>
        public bool EnableActingOut { get; set; } = true;

        /// <summary>When true, every NPC carries a daily humor — bright, weary, restless, brooding,
        /// tender, bold — woven into their situation so the same soul meets you differently day to
        /// day. It is derived from who they are and what day it is (no dice, nothing stored): the
        /// same day always finds them the same, and reloading changes no one's weather. Set false
        /// for souls whose spirits never shift with the days.</summary>
        public bool EnableMoodSwings { get; set; } = true;

        /// <summary>When true (and mood swings are on), the women of the world also keep their
        /// body's own monthly season — the custom of women, each on her own calendar — told to her
        /// gently by its turning (the days of the custom, the rising days, the crest, the waning
        /// days) so it colors her humor and she can weigh it in her own choices, as living women do.
        /// Women with child, and those past their childbearing years, are not given it. Set false
        /// to keep only the daily humor for everyone.</summary>
        public bool EnableWomensCycle { get; set; } = true;

        /// <summary>When true, an NPC's DEEP MEMORY opens with the story the world already tells of them
        /// instead of a blank page (2026.08.08 — moved from self.txt seeding, so the backstory is a
        /// memory they rewrite and weigh at every compression rather than a page standing forever at a
        /// fixed place and weight): a wanderer carries the tale they tell in taverns when first met
        /// (hand-written, in their own voice), a noble the account the encyclopedia keeps of their house
        /// and repute. From there it is theirs — kept, reshaped, or released whenever they settle their
        /// memory. Souls already seeded the old way (a non-empty self.txt) are left as they are. Set
        /// false for everyone to begin unwritten. (Key name kept for config compatibility.)</summary>
        public bool SeedSelfFromWorldStory { get; set; } = true;

        /// <summary>The director's spark: at an NPC's FIRST interaction, one small LLM call writes them
        /// a private starting truth (1–3 first-person sentences — a wound, a habit, a vanity, sometimes
        /// something wilder) into their custom_instructions.txt, seeded from their real story, traits,
        /// speech style and the world's global prompt, plus drawn muse cards for variety. The file stays
        /// yours to edit or erase (delete the whole file to reroll; a "# spark:" comment marks it done).
        /// Values: "Generate" (default — write it quietly), "Ask" (a popup asks you first, once per soul),
        /// "Off" (no spark; souls begin plain).</summary>
        public string PersonaSparkMode { get; set; } = "Generate";

        /// <summary>When true, an unhired wanderer the player speaks with may strike the hiring bargain
        /// inside the conversation itself (the strike_bargain tool — needs a tool-capable backend).
        /// NOTHING is settled by talk alone: the tool only lays the terms, a confirm popup names the
        /// exact price, and only the player's seal moves gold and service — the same three acts and the
        /// same rules as the vanilla tavern hiring (enough gold, room under the companion limit). The
        /// price can be haggled in words, but the mod refuses anything beyond
        /// <see cref="ConversationHiringHagglePercent"/> of the game's own reckoning; the daily wage
        /// is never negotiable. Set false to keep hiring in the vanilla dialog alone.</summary>
        public bool EnableConversationHiring { get; set; } = true;

        /// <summary>How far spoken haggling may move a conversation-struck hiring price from the
        /// game's own reckoning, either way, in percent — a HARD rail enforced by the mod, not a
        /// suggestion to the NPC. 0 means no haggling at all (the reckoned price or nothing);
        /// 30 (the default) lets words earn up to 30% off — or talk you 30% up, which the seal
        /// popup then says to your face. Clamped to [0,90] in <see cref="Normalize"/>.</summary>
        public int ConversationHiringHagglePercent { get; set; } = 30;

        /// <summary>Marriage by courtship (2026.08.07, Anton's ask — the wedding handshake): NPCs
        /// walk a real road toward marrying the player (liking → love → readiness → betrothal →
        /// wedding), moved by their OWN hand mid-conversation (the tend_courtship tool — needs a
        /// tool-capable backend) under hard rails: relation floors, one step a game day, the station
        /// gate (a great house asks a great suitor), and their own generated private asks. NOTHING
        /// is settled by talk alone — betrothal and wedding each take a confirm popup, and the
        /// wedding is the real game marriage with all its consequences. Souls with a real story are
        /// seeded once from it, so a love already spoken is honored. Default on.</summary>
        public bool EnableConversationMarriage { get; set; } = true;

        /// <summary>When true (the default — vanilla forbids it, but here it is the point: the first
        /// true bonds are with companions), a wanderer riding as the player's companion can be wed
        /// through the courtship road. At the wedding seal she is raised to lordship by the game's
        /// own companion-to-lord shape — she keeps her place in the party and her duties, and the
        /// marriage is fully real (clan, children, encyclopedia). Off = noble spouses only.</summary>
        public bool AllowCompanionMarriage { get; set; } = true;

        /// <summary>When true (the default), a noble bride with kin above her cannot be WED until
        /// the head of her house blesses the match — won in conversation (or by letter) with that
        /// very person, at a bride-price haggled around the world's own reckoning (her clan's
        /// renown, as vanilla's own marriage barter prices it). The betrothal itself needs no
        /// blessing — a promise belongs to the two of them. Wanderer brides have no house to ask.
        /// Off = no family consent asked of anyone.</summary>
        public bool MarriageNeedsFamilyConsent { get; set; } = true;

        /// <summary>How far spoken haggling may move the bride-price from the world's reckoning,
        /// either way, in percent — the same HARD rail as the hiring haggle. 0 = the reckoning is
        /// the price. Clamped to [0,90] in <see cref="Normalize"/>.</summary>
        public int MarriageDowryHagglePercent { get; set; } = 30;

        /// <summary>The Don-Juan discount: how many clan tiers below a bride's station the player's
        /// house may stand and still win her hand — earned only once her heart is fully won (the
        /// step to readiness and both seals). 2 (the default) lets a tier-4 house wed an emperor's
        /// daughter whose station asks 6. Clamped to [0,4] in <see cref="Normalize"/>.</summary>
        public int CourtshipCharmSlack { get; set; } = 2;

        /// <summary>How many game days a betrothal must season before the wedding can be laid —
        /// a promise is not a same-breath formality. 0 = may wed the very day of the promise.
        /// Clamped to [0,30] in <see cref="Normalize"/>.</summary>
        public int MinBetrothalDays { get; set; } = 3;

        /// <summary>
        /// Reverting a bad turn: when on, each save quietly photographs this campaign's whole memory folder
        /// (every NPC's memories/self/letters + the letters still on the road) and loading that save
        /// restores the photograph — so reloading to before an NPC's angry moment truly un-remembers it, the
        /// same way the game already reverts the relation number that lives inside the save. Off = the old
        /// behavior, where a reload leaves "memories from the future" in place. Snapshots live in _snapshots\
        /// inside the campaign folder, keyed to each save and pruned when a save is overwritten. Default on.
        /// </summary>
        public bool RevertMemoriesWithSaves { get; set; } = true;

        /// <summary>A safety cap on how many memory snapshots one campaign keeps across all its saves (oldest
        /// pruned first). Normally each save slot keeps only its own, so this bites only many-slot players.</summary>
        public int MaxMemorySnapshots { get; set; } = 40;

        /// <summary>LEGACY (the narrator retired 2026.08.07 — every prompt is the NPC's own first-person
        /// mind now): the in-fiction name of the old narrator voice. Still used to replay and render the
        /// Angel turns recorded in older saves truthfully, and to resolve the {voice} token in the
        /// configurable atmosphere/roleplay lines. No new beats speak in it.</summary>
        public string SystemVoiceName { get; set; } = "Angel";

        /// <summary>How many verbatim turns an NPC keeps before old ones are compressed into the rolling
        /// memory. Widened 30 → 40 on 2026.08.08, when the distilled truths and aims were retired and
        /// their room came back to the ordinary deep memory.</summary>
        public int MaxRecentTurns { get; set; } = 40;

        /// <summary>How many of the newest turns stay verbatim after a compression pass (15 → 20 with
        /// the same 2026.08.08 widening).</summary>
        public int KeepRecentTurnsAfterCompression { get; set; } = 20;

        /// <summary>How many in-game days of verbatim turns an NPC keeps before old ones are compressed.</summary>
        public int MaxRecentDays { get; set; } = 30;

        /// <summary>How many in-game days of newest turns stay verbatim after a compression pass.</summary>
        public int KeepRecentDaysAfterCompression { get; set; } = 15;

        /// <summary>Percent of the selected model's context window allowed for verbatim recent memory before compression starts.</summary>
        public int MaxRecentMemoryPercent { get; set; } = 10;

        /// <summary>Percent of the selected model's context window kept verbatim after compression.</summary>
        public int MinRecentMemoryPercentAfterCompression { get; set; } = 5;

        /// <summary>DERIVED, not a dial: the recent-memory token ceiling that MaxRecentMemoryPercent works
        /// out to on the selected model. Rewritten by <see cref="Normalize"/> on every load and every menu
        /// edit, so a hand-set value here does not survive — move the percent instead. It is written to the
        /// file because seeing the real number is the whole point.</summary>
        public int MaxRecentMemoryTokens { get; set; } = 0;

        /// <summary>DERIVED like the ceiling above: what MinRecentMemoryPercentAfterCompression works out
        /// to on the selected model. Edit the percent, not this.</summary>
        public int MinRecentMemoryTokensAfterCompression { get; set; } = 0;

        // ------------------------------ the voices ------------------------------

        /// <summary>
        /// When true, the NPCs' words can also be HEARD: a speech engine on your own machine turns a
        /// reply into a voice, in the voice you cast for that soul. Off by default, and deliberately
        /// so — it wants a several-gigabyte download and a real graphics card, and nobody should have
        /// a feature they cannot run switched on for them. The engine runs as its own little program
        /// beside the game, never inside it, so if it ever falls over it costs you the voices for a
        /// session and nothing else. Everything is free after the download; nothing is ever sent
        /// anywhere. With this off the mod is exactly what it was before it could speak.
        /// </summary>
        public bool EnableVoice { get; set; }

        /// <summary>
        /// When true, a soul riding with you NOTICES when you change their gear — what you put into
        /// their hands, what you took, and what each piece is worth — set down in their own memory
        /// the moment you close the inventory. Anton's reason for the numbers (2026.08.16): they
        /// have no other way of knowing whether what they were handed is a courtesy or a fortune.
        /// <para>
        /// Nothing is written for gear the GAME changes by itself (coming of age, a companion raised
        /// to lordship at her wedding, the sweep every load makes), nor for a session you cancel: it
        /// compares only what stood when the screen opened against what stands when it closes.
        /// </para>
        /// </summary>
        public bool EnableGearNotes { get; set; } = true;

        /// <summary>When true, a reply speaks the moment it arrives. False keeps every voice but
        /// waits to be asked for it, which is the quieter way to read at your own pace.</summary>
        public bool VoiceAutoSpeak { get; set; } = true;

        /// <summary>
        /// When true, a reply speaks itself even if you have closed the window or wandered off to
        /// another thread — so an answer can be LISTENED to while you ride, trade or fight (Anton,
        /// 2026.08.15). Rides <see cref="VoiceAutoSpeak"/>: with that off nothing speaks by itself
        /// anywhere, which is what that switch says on the tin.
        /// <para>
        /// This deliberately reverses an earlier rule ("a voice from a conversation they walked away
        /// from is a ghost in the room") because the asking happened. The old hazard is real and
        /// unchanged: there is one voice at a time, so if two answers land close together the second
        /// cuts off the first — Backspace silences either, and the words keep their play mark. For a
        /// HOSTED voice this also means a line is made, and billed, without anyone pressing anything;
        /// turn it off if you would rather pay only for what you ask to hear.
        /// </para>
        /// </summary>
        public bool VoiceSpeakWhenClosed { get; set; } = true;

        /// <summary>
        /// When true, the small acted parts between *asterisks* are read aloud too, as narration
        /// between the spoken words. On by default (Anton, 2026.08.15): a reply whose whole answer is
        /// *turns away without a word* was simply silent before, and half of what a soul does never
        /// reached the ear at all.
        /// <para>
        /// The asterisks themselves are never spoken, and a gesture that ends on a word is closed
        /// with a stop so it does not run into the sentence after it. Turn it off and the voice reads
        /// only what was actually said — the older behaviour, and the right one for anyone who finds
        /// stage directions in a voice jarring.
        /// </para>
        /// </summary>
        public bool VoiceSpeakActedParts { get; set; } = true;

        /// <summary>
        /// When true, anyone you have not cast by hand is given a voice of their own people and
        /// their own sex — so a Battanian woman sounds Battanian without you casting five hundred
        /// souls one at a time.
        /// <para>
        /// The choice is worked out from their own name and never written down, so it is the same
        /// voice every session and survives every reload. Anything you cast yourself always wins,
        /// and so does a voice you hand to all women or all men. Turn this off and only your own
        /// castings speak.
        /// </para>
        /// </summary>
        public bool VoiceAutoCast { get; set; } = true;

        /// <summary>Where the speech engine's own files live (the folder holding qwen3_tts.dll and
        /// its companions). Leave empty and it is looked for in the usual places — this is only for
        /// an unusual install, or to point at a second copy.</summary>
        public string VoiceEnginePath { get; set; } = string.Empty;

        /// <summary>The folder of speech models (.gguf). Empty = found on its own.</summary>
        public string VoiceModelDir { get; set; } = string.Empty;

        /// <summary>Which speech model speaks, by file name. Empty = whichever talker model is
        /// there. A bigger model sounds better and takes longer; both are honest choices.</summary>
        public string VoiceModelName { get; set; } = string.Empty;

        /// <summary>
        /// How freely the voice may vary its reading, and how wide a choice it draws from
        /// (temperature and top-p). 0 = leave both to the speech engine's own defaults, which is
        /// what almost everyone should do.
        /// <para>
        /// Here because it is the one dial that has been shown to matter for the glitch where a
        /// voice loses the words and holds a single note: a colder setting makes a speech model
        /// likelier to fall into repeating itself, and that is what a held note IS. The engine's own
        /// values (0.9 and 1.0) are what we now use, after a sister tool driving the same engine at
        /// those values proved sixty times steadier. Lower it only if a voice wanders too far from
        /// the one you cloned — and expect more stumbles if you do.
        /// </para>
        /// </summary>
        public float VoiceTemperature { get; set; }

        /// <summary>The other half of <see cref="VoiceTemperature"/>. 0 = the engine's own.</summary>
        public float VoiceTopP { get; set; }

        /// <summary>How much disk the spoken lines may keep, in megabytes. Every line is made once
        /// and then simply played, so the cache is what makes a voice instant the second time; when
        /// it grows past this the oldest lines are quietly swept, whole replies at a time. 0 keeps
        /// everything for ever, which on a large disk is a perfectly good answer.</summary>
        public int VoiceCacheBudgetMb { get; set; } = 2048;

        /// <summary>The FMOD event a spoken line is handed to. Leave it alone unless the voices are
        /// silent or oddly quiet. This is the game's OWN event for audio it did not ship (verified
        /// 2026.08.15 in its event table, beside "event:/Extra/external" and
        /// "event:/Extra/voicechat"), so it already rides the game's own routing and volume. If it
        /// ever needs moving, "event:/Extra/external" is the nearest sibling. Takes hold on the next
        /// line spoken.</summary>
        public string VoiceSoundEvent { get; set; } = "event:/Extra/voiceover";

        /// <summary>
        /// How a spoken reply is made and delivered. Three roads, and which one is best depends on
        /// how hard the graphics card is already working:
        /// <list type="bullet">
        /// <item><c>FullRead</c> (default) - one generation for the whole reply, and not a word is
        /// heard until all of it exists. The steadiest voice and no gaps ever, at the cost of
        /// waiting longer before she starts.</item>
        /// <item><c>Streaming</c> - the same single generation, but each second of it is played the
        /// moment it is made. She starts speaking in well under a second; if the card is busy
        /// enough that making the audio falls behind playing it, the reply breaks into gaps.</item>
        /// <item><c>ByLine</c> - a separate generation per sentence. The oldest road, kept for
        /// comparison: it starts quickly on a loaded card but the voice audibly changes person at
        /// every sentence, because each generation rolls its own prosody.</item>
        /// </list>
        /// Takes hold on the next line spoken; no restart.
        /// </summary>
        public string VoiceDelivery { get; set; } = "Streaming";

        /// <summary>
        /// The key that stops a voice DEAD, wherever you are — on the map, in a battle, with every
        /// window shut. It exists because of the one thing this feature can do that nothing else in
        /// the mod can: a speech model that misses its own ending keeps generating, and what comes
        /// out is babbling or screeching that will not stop on its own.
        /// <para>
        /// There are two rails above this one — every line is given an audio ceiling worked out from
        /// its own length, and a reading that runs past it is cut off and never kept — so this should
        /// stay a key nobody has to press. It is here for the day they do.
        /// </para>
        /// </summary>
        public string VoicePanicKey { get; set; } = "Backspace";

        /// <summary>When true, a soul who comes to you unbidden speaks their first words aloud as
        /// they arrive. ON since 2026.08.15 (Anton's call): being spoken to unprompted is most of
        /// what makes them feel alive, and it is the moment a voice earns its keep. The known cost
        /// stands — several souls may reach out within one stretch of map and there is one voice at
        /// a time, so a second arrival cuts off the first mid-sentence. Backspace silences it, and
        /// their words keep the play mark beside them either way.</summary>
        public bool VoiceSpeakReachOuts { get; set; } = true;

        /// <summary>
        /// A hosted speech service, for the far more common player who has no graphics card to spare
        /// and no wish to download several gigabytes. Empty = the local engine only.
        /// <para>
        /// It cannot clone anybody, which is exactly why it is the stranger's road and the Qwen
        /// engine is the author's: you pick from a fixed shelf of voices instead of making your own.
        /// Billed per minute of speech to whichever key you put here, and every line is written down
        /// in the same cost ledger as everything else.
        /// </para>
        /// </summary>
        public string CloudVoiceApiKey { get; set; } = string.Empty;

        /// <summary>Where the hosted speech service lives. The default is OpenAI's own; any service
        /// that speaks the same shape works, which is the same courtesy the LLM side extends.</summary>
        public string CloudVoiceEndpoint { get; set; } = DefaultCloudVoiceEndpoint;

        /// <summary>OpenAI's own speech endpoint — verified against the live documentation
        /// 2026.08.15, along with the thirteen voice names and the WAV response format.</summary>
        public const string DefaultCloudVoiceEndpoint = "https://api.openai.com/v1/audio/speech";

        /// <summary>Which hosted model speaks. gpt-4o-mini-tts is the cheapest of them and carries
        /// all thirteen voices; tts-1 is quicker and older and carries nine.</summary>
        public string CloudVoiceModel { get; set; } = "gpt-4o-mini-tts";

        /// <summary>What a minute of hosted speech costs, in dollars, for the cost notices. OpenAI's
        /// own published figure for gpt-4o-mini-tts is $0.015 a minute. We know exactly how many
        /// seconds came back, so this is measured rather than estimated — edit it if you speak with
        /// somebody else's service.</summary>
        public double CloudVoicePricePerMinute { get; set; } = 0.015;

        /// <summary>The once-per-install nudge that the voices exist at all has been shown. Voices
        /// are off by default and must never become a thing the player has to turn off to be left
        /// alone — so this is said once, softly, and never again.</summary>
        public bool VoiceHintShown { get; set; }

        /// <summary>The built-in model → context-window table. Longest key contained in the model id
        /// wins, so "gpt-5.1" beats "gpt-5" for gpt-5.1-mini. Users edit/extend the copy in their
        /// config.json; missing built-ins are re-added on load so new defaults reach old configs.</summary>
        public static Dictionary<string, int> DefaultModelContextWindows() =>
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["gpt-4o"] = 128000,
                ["gpt-4.1"] = 1000000,
                ["gpt-5"] = 400000,
                ["gpt-5.1"] = 400000,
                ["gpt-5.4"] = 1000000,        // the full model; mini/nano below stay at 400k
                ["gpt-5.4-mini"] = 400000,
                ["gpt-5.4-nano"] = 400000,
                ["gpt-5.5"] = 1000000,
                ["gpt-5.6"] = 1000000,
                ["claude"] = 200000,
                ["claude-opus-4"] = 1000000,
                ["claude-sonnet-5"] = 1000000,
                ["claude-sonnet-4-6"] = 1000000,
                ["gemini"] = 1000000,
                ["deepseek"] = 128000,
                ["deepseek-v4"] = 1000000,
                ["grok"] = 256000,
            };

        /// <summary>The built-in model → price table (USD per million tokens; verified 2026.08.02).
        /// Longest key contained in the model id wins, so "gpt-5.6-terra" beats "gpt-5.6".
        /// Users edit/extend the copy in config.json; missing built-ins are re-added on load, and the
        /// V3 migration corrects entries an old config still holds at a superseded price (see
        /// <see cref="SupersededModelPrices"/>) — untouched defaults only, never a hand-edit.</summary>
        public static Dictionary<string, ModelPrice> DefaultModelPrices() =>
            new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
            {
                // Anthropic
                ["claude-opus-4"] = new ModelPrice(5, 25),
                ["claude-sonnet"] = new ModelPrice(3, 15),
                ["claude-haiku"] = new ModelPrice(1, 5),
                ["claude-fable-5"] = new ModelPrice(10, 50),
                // OpenAI — luna and terra were cut hard on 2026.07.30 (luna by 80%).
                ["gpt-5.6"] = new ModelPrice(5, 30),          // the bare alias routes to Sol
                ["gpt-5.6-sol"] = new ModelPrice(5, 30),      // unchanged in the July cut
                ["gpt-5.6-terra"] = new ModelPrice(2, 12),
                ["gpt-5.6-luna"] = new ModelPrice(0.2, 1.2),
                ["gpt-5.5"] = new ModelPrice(5, 30),          // flagship tier; no mini/nano siblings exist
                ["gpt-5.4"] = new ModelPrice(2.5, 15),
                ["gpt-5.4-mini"] = new ModelPrice(0.75, 4.5), // explicit: would otherwise match "gpt-5"
                ["gpt-5.4-nano"] = new ModelPrice(0.2, 1.25),
                ["gpt-5"] = new ModelPrice(1.25, 10),
                ["gpt-5-mini"] = new ModelPrice(0.25, 2),
                ["gpt-4o"] = new ModelPrice(2.5, 10),
                ["gpt-4o-mini"] = new ModelPrice(0.15, 0.6),
                ["gpt-4.1"] = new ModelPrice(2, 8),
                ["gpt-4.1-mini"] = new ModelPrice(0.4, 1.6),
                // Google. These are the PAID rates; on a free-tier key nothing is billed at all, so
                // the cost notice simply overstates — no ledger can know which tier a key sits on.
                ["gemini-3.6-flash"] = new ModelPrice(1.5, 7.5),
                ["gemini-3.5-flash"] = new ModelPrice(1.5, 9),
                ["gemini-3.5-flash-lite"] = new ModelPrice(0.3, 2.5),
                ["gemini-3.1-flash-lite"] = new ModelPrice(0.25, 1.5),
                ["gemini-2.5-pro"] = new ModelPrice(1.25, 10),
                ["gemini-2.5-flash"] = new ModelPrice(0.3, 2.5),
                ["gemini-2.5-flash-lite"] = new ModelPrice(0.1, 0.4),
                // DeepSeek. Their published off-peak rates; during Beijing peak hours (09:00–12:00 and
                // 14:00–18:00 UTC+8) the real bill is DOUBLE these, which no static table can follow.
                ["deepseek-v4-flash"] = new ModelPrice(0.14, 0.28),
                ["deepseek-v4-pro"] = new ModelPrice(0.435, 0.87),
                // Others reachable through OpenRouter; containment matches the prefixed ids
                // ("google/gemini-2.5-flash", "deepseek/deepseek-v4-flash").
                ["grok-4.5"] = new ModelPrice(2, 6),
                ["mistral-large-2512"] = new ModelPrice(0.5, 1.5),
            };

        /// <summary>Prices a shipped default once carried and no longer does. An existing config.json
        /// keeps whatever it was written with — <see cref="Normalize"/> only ever ADDS missing keys —
        /// so a provider's price cut would otherwise leave every old install quoting the old number
        /// forever. The V3 migration replaces an entry ONLY where it still equals the superseded
        /// figure exactly; a player who edited that line keeps their own.</summary>
        private static Dictionary<string, ModelPrice> SupersededModelPrices() =>
            new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
            {
                ["gpt-5.6-luna"] = new ModelPrice(1, 6),        // cut 80% on 2026.07.30
                ["gpt-5.6-terra"] = new ModelPrice(2.5, 15),    // cut 20% the same day
                ["deepseek-v4-flash"] = new ModelPrice(0.1, 0.2), // our own estimate; their real rate is 0.14/0.28
            };

        public static string ConfigDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Configs", "ImmersiveAI");

        public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

        public static ModConfig LoadOrCreate()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var loaded = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(ConfigFilePath));
                    if (loaded != null)
                    {
                        loaded.Normalize();
                        File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(loaded, Formatting.Indented));
                        return loaded;
                    }
                }

                var fresh = new ModConfig();
                fresh.Normalize();
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(fresh, Formatting.Indented));
                return fresh;
            }
            catch
            {
                return new ModConfig();
            }
        }

        /// <summary>Writes the current values back to config.json (used by the live socialness
        /// control, so a slider nudge survives the session). Best-effort: a failed save must never
        /// cost more than the persistence itself.</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { /* the value still lives for this session */ }
        }

        public void Normalize()
        {
            // The version stamp: pre-stamp configs (the field missing deserializes as 0) are the
            // V1 format too — everything else Normalize does IS the migration for them.
            if (ConfigVersion < 1) ConfigVersion = 1;

            // V2: War Sails claims "U" on the campaign map (the ship manager at sea), so the letter
            // window's default key moved to "Y". Only a config still sitting on the old default
            // follows; any other hand-picked key is honored as it stands.
            if (ConfigVersion < 2)
            {
                if (string.Equals((LetterWindowHotkey ?? string.Empty).Trim(), "U", StringComparison.OrdinalIgnoreCase))
                    LetterWindowHotkey = "Y";
                ConfigVersion = 2;
            }

            // V3: providers cut their prices (OpenAI's luna by 80% on 2026.07.30) and an existing
            // config keeps the table it was written with. Correct only the lines still holding the
            // exact superseded figure — a hand-tuned price is the player's own and stays.
            if (ConfigVersion < 3)
            {
                if (ModelPrices != null)
                {
                    var current = DefaultModelPrices();
                    foreach (var stale in SupersededModelPrices())
                    {
                        if (!ModelPrices.TryGetValue(stale.Key, out var held) || held == null) continue;
                        if (held.InputPerMTok != stale.Value.InputPerMTok
                            || held.OutputPerMTok != stale.Value.OutputPerMTok) continue;
                        if (current.TryGetValue(stale.Key, out var fresh) && fresh != null)
                            ModelPrices[stale.Key] = fresh;
                    }
                }
                ConfigVersion = 3;
            }

            // V4: with the distilled truths and aims retired (2026.08.08) the rolling memory became
            // the only durable thing an NPC carries — and the first playtest cut a rich Bulgarian
            // memory off in mid-word at the old 1500-token write budget (text outside ASCII costs
            // roughly 1.6× the tokens English does, so that ceiling was always tighter than it
            // looked). A budget too small to finish the memory is not a matter of taste, it is a
            // wound, so it migrates like the prices do — but only where it still holds the exact
            // old default; any hand-set budget is the player's own and stays.
            // The 4000 below is deliberately NOT re-pointed at the 2000 the default became on
            // 2026.08.14. This migration is history: it healed a specific wound with the value
            // chosen at the time, and it only ever fires for a config still sitting on V3. Lowering
            // a ceiling is taste, and taste is never migrated — the same asymmetry that migrates
            // prices and leaves model defaults alone.
            if (ConfigVersion < 4)
            {
                if (MaxMemoryWriteTokens == 1500) MaxMemoryWriteTokens = 4000;
                ConfigVersion = 4;
            }

            // V5 (2026.08.15): "FullRead" existed for ONE reason — playing a streamed reply as N
            // separate sounds left a frame of silence inside every second of speech, and waiting for
            // the whole reading was the only way to be sure of no seam. That defect is fixed (the
            // pieces are now poured into one file and handed over by the clock, see VoicePlayback),
            // and with it gone Full read is strictly worse: same audio, four seconds later.
            // So it migrates like the memory budget did — a mode that exists only to dodge a defect
            // is not taste — and ONLY where it still holds the exact old default. Anyone who chose
            // Full read by hand chose it, and keeps it.
            if (ConfigVersion < 5)
            {
                if (string.Equals(VoiceDelivery, "FullRead", StringComparison.Ordinal))
                    VoiceDelivery = "Streaming";
                ConfigVersion = 5;
            }

            if (string.IsNullOrWhiteSpace(SystemVoiceName)) SystemVoiceName = "Angel";

            // The spark mode knows exactly three spellings; anything else (typos, old hand edits)
            // falls back to the default so a soul is never silently left plain by a misspelling.
            if (PersonaSparkMode != "Generate" && PersonaSparkMode != "Ask" && PersonaSparkMode != "Off")
                PersonaSparkMode = "Generate";

            // The OpenAI-compatible endpoints: blank falls back to each backend's default; a pasted
            // base URL ending in /v1 (the way every provider states it) is completed to the full
            // chat-completions path, and a missing scheme becomes https — except on a loopback
            // address, where local servers speak plain http.
            OpenAIBaseUrl = NormalizeChatEndpoint(OpenAIBaseUrl, DefaultOpenAIEndpoint);
            LocalEndpoint = NormalizeChatEndpoint(LocalEndpoint, DefaultLocalEndpoint);

            // The local backend's honest settings: the model id is the user's to fill (the health
            // check asks for it kindly when blank); the context window must stay a sane size.
            LocalModel = (LocalModel ?? string.Empty).Trim();
            LocalApiKey = (LocalApiKey ?? string.Empty).Trim();
            ReplyLength = (ReplyLength ?? string.Empty).Trim();
            if (ReplyLength != "Brief" && ReplyLength != "Conversational" && ReplyLength != "Full")
                ReplyLength = "Conversational";

            if (LocalContextWindow <= 0) LocalContextWindow = 16384;
            if (LocalContextWindow < 2048) LocalContextWindow = 2048;
            if (LocalContextWindow > 2000000) LocalContextWindow = 2000000;

            // The OpenRouter model: blank falls back to the default; ids are trimmed so a pasted
            // trailing space can't 400 the router.
            OpenRouterApiKey = (OpenRouterApiKey ?? string.Empty).Trim();
            OpenRouterModel = (OpenRouterModel ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(OpenRouterModel)) OpenRouterModel = "openai/gpt-5.6-luna";

            // Gemini and DeepSeek: same contract — trimmed keys and ids, a blank id falling back to
            // the tested default rather than 400-ing on an empty model name.
            GeminiApiKey = (GeminiApiKey ?? string.Empty).Trim();
            GeminiModel = (GeminiModel ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(GeminiModel)) GeminiModel = new ModConfig().GeminiModel;

            DeepSeekApiKey = (DeepSeekApiKey ?? string.Empty).Trim();
            DeepSeekModel = (DeepSeekModel ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(DeepSeekModel)) DeepSeekModel = new ModConfig().DeepSeekModel;

            // Claude Code: a blank model falls back to the light default; the path is only ever an
            // override, so blank simply stays blank and the finder does its work.
            ClaudeCodeModel = (ClaudeCodeModel ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ClaudeCodeModel)) ClaudeCodeModel = new ModConfig().ClaudeCodeModel;
            ClaudeCodePath = (ClaudeCodePath ?? string.Empty).Trim();

            // The daily request cap: negative is a typo; 0 stays "no cap".
            if (MaxDailyRequests < 0) MaxDailyRequests = 0;

            // 0 is a real answer here ("do not touch my limiter"); anything else must land inside
            // what the engine's own limiter accepts, or it is quietly ignored.
            if (TalkScreenFpsLimit < 0) TalkScreenFpsLimit = 0;
            else if (TalkScreenFpsLimit > 0 && TalkScreenFpsLimit < 30) TalkScreenFpsLimit = 30;
            else if (TalkScreenFpsLimit > 360) TalkScreenFpsLimit = 360;

            // The hotkeys must name real keys; anything unparseable falls back to the defaults.
            if (string.IsNullOrWhiteSpace(ChatWindowHotkey)) ChatWindowHotkey = "O";
            ChatWindowHotkey = ChatWindowHotkey.Trim();
            if (string.IsNullOrWhiteSpace(LetterWindowHotkey)) LetterWindowHotkey = "Y";
            LetterWindowHotkey = LetterWindowHotkey.Trim();

            // A null (rather than blank) atmosphere line would trip token substitution; treat it as "unset"
            // so the prompt falls back to its built-in default. Guidance may legitimately be blank (none).
            if (AtmosphereLine == null) AtmosphereLine = string.Empty;
            if (RoleplayGuidance == null) RoleplayGuidance = string.Empty;

            // Configs still carrying the pre-first-person defaults verbatim follow the sheet into the
            // new voice; a hand-edited line is honored as it stands.
            if (string.Equals(AtmosphereLine.Trim(), LegacyAtmosphereLine, StringComparison.Ordinal))
                AtmosphereLine = new ModConfig().AtmosphereLine;
            if (string.Equals(RoleplayGuidance.Trim(), LegacyRoleplayGuidance, StringComparison.Ordinal)
                || string.Equals(RoleplayGuidance.Trim(), PreviousRoleplayGuidance, StringComparison.Ordinal))
                RoleplayGuidance = new ModConfig().RoleplayGuidance;

            // Keep the daily rate non-negative and under one-per-hour, so a fat-fingered value can't have
            // every NPC hammering the player. 24 is already far more than anyone would want.
            if (DailyInitiationRate < 0 || double.IsNaN(DailyInitiationRate)) DailyInitiationRate = 0;
            if (DailyInitiationRate > 24) DailyInitiationRate = 24;

            // The stranger's floor is a fraction of a full bond's pull; anything outside [0,1] is a typo.
            if (InitiationPullFloor < 0 || double.IsNaN(InitiationPullFloor)) InitiationPullFloor = 0;
            if (InitiationPullFloor > 1) InitiationPullFloor = 1;

            // The road can only hold so many letters bound for one reader; anything above is a typo.
            if (MaxLettersInFlight < 0) MaxLettersInFlight = 0;
            if (MaxLettersInFlight > 20) MaxLettersInFlight = 20;

            // The nights. The hour must be an hour; the cooldown must leave a night to be a night,
            // and a day and a half is as far as it is worth stretching one.
            LoverRansomHagglePercent = Clamp(LoverRansomHagglePercent, 0, 90);
            NightHour = Clamp(NightHour, 0, 23);
            NightDayResetHour = Clamp(NightDayResetHour, 0, 23);
            NightCooldownHours = Clamp(NightCooldownHours, 1, 72);   // retired; kept sane, read by nothing
            if (ConceptionChanceMultiplier < 0 || double.IsNaN(ConceptionChanceMultiplier)) ConceptionChanceMultiplier = 0;
            if (ConceptionChanceMultiplier > 10) ConceptionChanceMultiplier = 10;
            ConceptionRevealDelayDays = Clamp(ConceptionRevealDelayDays, 0, 30);
            MaxNightsRemembered = Clamp(MaxNightsRemembered, 1, 60);
            // Telling more nights in full than are kept at all would silently do nothing.
            MaxNightsToldInFull = Clamp(MaxNightsToldInFull, 0, MaxNightsRemembered);
            NightAwarenessOnRoadPercent = Clamp(NightAwarenessOnRoadPercent, 0, 100);
            NightAwarenessInVillagePercent = Clamp(NightAwarenessInVillagePercent, 0, 100);
            NightAwarenessInCastlePercent = Clamp(NightAwarenessInCastlePercent, 0, 100);
            NightAwarenessInTownPercent = Clamp(NightAwarenessInTownPercent, 0, 100);
            MaxNightGift = Clamp(MaxNightGift, 0, 1000000);
            if (string.IsNullOrWhiteSpace(NightWindowHotkey)) NightWindowHotkey = "H";
            NightWindowHotkey = NightWindowHotkey.Trim();

            // The voices. The three paths are the player's own words and are honored as written —
            // only trimmed, because a pasted path drags a space along often enough to matter, and a
            // trailing separator would turn into an escaped quote on the host's command line.
            VoiceEnginePath = (VoiceEnginePath ?? string.Empty).Trim();
            VoiceModelDir = (VoiceModelDir ?? string.Empty).Trim();
            VoiceModelName = (VoiceModelName ?? string.Empty).Trim();

            // 0 means "the engine's own", so only a real setting is railed. Greedy decoding (a
            // temperature at or near zero) is the one setting guaranteed to loop, which is the very
            // fault these exist to avoid.
            if (VoiceTemperature != 0f && (VoiceTemperature < 0.05f || VoiceTemperature > 2f)) VoiceTemperature = 0f;
            if (VoiceTopP != 0f && (VoiceTopP < 0.05f || VoiceTopP > 1f)) VoiceTopP = 0f;

            // The cache budget: 0 stays a legitimate "keep everything", a negative is a typo, and
            // the ceiling is only there so a stray keystroke cannot promise a terabyte.
            // The delivery road knows exactly three spellings; a typo or an old hand edit becomes
            // the default rather than silently meaning nothing.
            var road = (VoiceDelivery ?? string.Empty).Trim();
            if (!road.Equals("FullRead", StringComparison.OrdinalIgnoreCase)
                && !road.Equals("Streaming", StringComparison.OrdinalIgnoreCase)
                && !road.Equals("ByLine", StringComparison.OrdinalIgnoreCase))
                VoiceDelivery = "Streaming";
            else
                VoiceDelivery = road.Equals("FullRead", StringComparison.OrdinalIgnoreCase) ? "FullRead"
                              : road.Equals("Streaming", StringComparison.OrdinalIgnoreCase) ? "Streaming"
                              : "ByLine";

            if (VoiceCacheBudgetMb < 0) VoiceCacheBudgetMb = 0;
            if (VoiceCacheBudgetMb > 200000) VoiceCacheBudgetMb = 200000;

            // The panic key: an unreadable name would leave the player with no way to stop a voice
            // at all, which is the one failure this feature must not have.
            if (string.IsNullOrWhiteSpace(VoicePanicKey)) VoicePanicKey = "Backspace";
            VoicePanicKey = VoicePanicKey.Trim();

            // The hosted road: the key is the player's own words, and the endpoint is completed the
            // same way every other pasted base URL in this file is.
            CloudVoiceApiKey = (CloudVoiceApiKey ?? string.Empty).Trim();
            CloudVoiceModel = (CloudVoiceModel ?? string.Empty).Trim();
            if (CloudVoiceModel.Length == 0) CloudVoiceModel = "gpt-4o-mini-tts";
            CloudVoiceEndpoint = (CloudVoiceEndpoint ?? string.Empty).Trim();
            if (CloudVoiceEndpoint.Length == 0) CloudVoiceEndpoint = DefaultCloudVoiceEndpoint;
            if (CloudVoicePricePerMinute < 0) CloudVoicePricePerMinute = 0;

            // The model table: never null, and every built-in entry present (so new defaults reach
            // configs written before them); user edits to existing keys are honored as-is.
            if (ModelContextWindows == null) ModelContextWindows = DefaultModelContextWindows();
            foreach (var pair in DefaultModelContextWindows())
                if (!ModelContextWindows.ContainsKey(pair.Key)) ModelContextWindows[pair.Key] = pair.Value;

            // The price table follows the same contract as the context-window table.
            if (ModelPrices == null) ModelPrices = DefaultModelPrices();
            foreach (var pair in DefaultModelPrices())
                if (!ModelPrices.ContainsKey(pair.Key)) ModelPrices[pair.Key] = pair.Value;

            // Memory-writing budget: never below the spoken budget (that would make reflection the
            // narrowest voice she has), never runaway.
            if (MaxMemoryWriteTokens <= 0) MaxMemoryWriteTokens = 1500;
            if (MaxMemoryWriteTokens < MaxTokens) MaxMemoryWriteTokens = MaxTokens;
            if (MaxMemoryWriteTokens > 8000) MaxMemoryWriteTokens = 8000;

            // The growing room: a starting budget above the ceiling is simply the ceiling, and a
            // step big enough to leap it is harmless (the climb is clamped where it is read).
            if (StartingMemoryWriteTokens <= 0) StartingMemoryWriteTokens = MaxMemoryWriteTokens;
            if (StartingMemoryWriteTokens > MaxMemoryWriteTokens) StartingMemoryWriteTokens = MaxMemoryWriteTokens;
            if (MemoryWriteTokensPerTurn < 0) MemoryWriteTokensPerTurn = 0;
            if (MemoryWriteTokensPerTurn > MaxMemoryWriteTokens) MemoryWriteTokensPerTurn = MaxMemoryWriteTokens;

            if (ConversationHiringHagglePercent < 0) ConversationHiringHagglePercent = 0;
            if (ConversationHiringHagglePercent > 90) ConversationHiringHagglePercent = 90;

            // The courtship road's rails share the haggle clamps' spirit: hard, small, honest.
            if (MarriageDowryHagglePercent < 0) MarriageDowryHagglePercent = 0;
            if (MarriageDowryHagglePercent > 90) MarriageDowryHagglePercent = 90;
            if (CourtshipCharmSlack < 0) CourtshipCharmSlack = 0;
            if (CourtshipCharmSlack > 4) CourtshipCharmSlack = 4;
            if (MinBetrothalDays < 0) MinBetrothalDays = 0;
            if (MinBetrothalDays > 30) MinBetrothalDays = 30;

            // Recall rounds: 0 is a legitimate "none"; more than a handful only slows replies down.
            if (MaxRecallsPerReply < 0) MaxRecallsPerReply = 0;
            if (MaxRecallsPerReply > 8) MaxRecallsPerReply = 8;

            // Tiding counts: 0 is a legitimate "none", but runaway values would bloat every prompt.
            if (MaxWorldTidings < 0) MaxWorldTidings = 0;
            if (MaxWorldTidings > 20) MaxWorldTidings = 20;
            if (MaxLocalRumors < 0) MaxLocalRumors = 0;
            if (MaxLocalRumors > 10) MaxLocalRumors = 10;

            if (MaxRecentTurns <= 0) MaxRecentTurns = 40;
            if (KeepRecentTurnsAfterCompression <= 0) KeepRecentTurnsAfterCompression = 20;
            if (MaxRecentDays <= 0) MaxRecentDays = 30;
            if (KeepRecentDaysAfterCompression <= 0) KeepRecentDaysAfterCompression = 15;

            MaxRecentTurns = Clamp(MaxRecentTurns,
                MemorySettingsMetadata.MinRecentTurns, MemorySettingsMetadata.MaxRecentTurnsCeiling);
            MaxRecentDays = Clamp(MaxRecentDays,
                MemorySettingsMetadata.MinRecentDays, MemorySettingsMetadata.MaxRecentDaysCeiling);

            // A keep window at or above its own ceiling would fire a compression that folds nothing
            // away — the same rail the percents ride, applied to the turn count and the days.
            if (KeepRecentTurnsAfterCompression >= MaxRecentTurns)
                KeepRecentTurnsAfterCompression = Math.Max(1, MaxRecentTurns / 2);
            if (KeepRecentDaysAfterCompression >= MaxRecentDays)
                KeepRecentDaysAfterCompression = Math.Max(1, MaxRecentDays / 2);

            var profile = MemoryTokenProfile.Resolve(this);
            if (MaxRecentMemoryPercent <= 0) MaxRecentMemoryPercent = profile.DefaultMaxRecentMemoryPercent;
            if (MinRecentMemoryPercentAfterCompression <= 0)
                MinRecentMemoryPercentAfterCompression = profile.DefaultMinRecentMemoryPercentAfterCompression;

            MaxRecentMemoryPercent = Clamp(
                MaxRecentMemoryPercent,
                MemorySettingsMetadata.MinMemoryPercent,
                MemorySettingsMetadata.MaxMemoryPercent);

            MinRecentMemoryPercentAfterCompression = Clamp(
                MinRecentMemoryPercentAfterCompression,
                MemorySettingsMetadata.MinMemoryPercent,
                MemorySettingsMetadata.MaxMemoryPercent);

            if (MinRecentMemoryPercentAfterCompression >= MaxRecentMemoryPercent)
                MinRecentMemoryPercentAfterCompression = Math.Max(
                    MemorySettingsMetadata.MinMemoryPercent,
                    MaxRecentMemoryPercent / 2);

            MaxRecentMemoryTokens = profile.GetMaxRecentMemoryTokens(MaxRecentMemoryPercent);
            MinRecentMemoryTokensAfterCompression =
                profile.GetMinRecentMemoryTokensAfterCompression(MinRecentMemoryPercentAfterCompression);
        }

        /// <summary>One pasted-URL contract for every OpenAI-shaped endpoint: blank → the given
        /// default, "/v1" completed to "/v1/chat/completions", missing scheme filled in — https for
        /// the world, http for a loopback host (local servers do not carry certificates).</summary>
        private static string NormalizeChatEndpoint(string url, string fallback)
        {
            if (string.IsNullOrWhiteSpace(url)) return fallback;
            url = url.Trim().TrimEnd('/');
            if (url.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                var isLoopback = url.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("127.0.0.1", StringComparison.Ordinal)
                    || url.StartsWith("0.0.0.0", StringComparison.Ordinal);
                url = (isLoopback ? "http://" : "https://") + url;
            }
            if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                url += "/chat/completions";
            return url;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
