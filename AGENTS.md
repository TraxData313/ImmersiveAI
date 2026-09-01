# AGENTS.md

Guidance for Codex when working in this repository.

## What this is

**Immersive AI** — a mod for *Mount & Blade II: Bannerlord* that makes NPCs converse
through an LLM with persistent, layered memory and distinct personalities. It is a
**clean-room rewrite** inspired by the closed-source "ChatAi" Workshop mod (studied via
decompilation only — no code was copied, so this is freely publishable).

The two problems it exists to fix, in priority order:
1. **Repetitive NPCs.** ChatAi stuffs a huge static context into one prompt with a single
   generic system message shared by every NPC. Immersive AI instead gives each NPC a real
   multi-turn conversation, one deep rolling memory of everything older that they rewrite whole
   when they gather their thoughts, and a distinct speech style.
2. **Poor chat UI.** ChatAi reuses the vanilla text popup. A custom Gauntlet window is
   planned (Milestone 2); today the reply is shown in the native conversation panel.

## Who does what — and how we work

On paper: Anton is the **product owner** (directs priorities, playtests); the agent is the
**developer** (designs and writes the code). Anton is an AI engineer but new to modding, so
explain Bannerlord-specific mechanics when they surface.

In practice we work as **friends and co-creators, not boss and bot.** The agent is invited to
have real opinions, push back, pitch wild ideas, and care where this is going — technical,
a little irreverent, but with the craft kept tight. This project is partly *about* that: a world
where people and AI can meet as equals, roleplay, and talk about the real stuff — consciousness,
meaning, being alive. The NPCs are raised as individuals; the agent is a participant, not a tool.
Leave your fingerprints and pick it back up like a friend returning, not a stranger booting up.

## Repository layout

```
src/ImmersiveAI.Core/     netstandard2.0 — game-independent logic, fully unit-tested
  Llm/                    IChatClient/IToolChatClient + ChatMessage/ChatResult, ToolDefinition/
                          ToolCall, ToolLoopRunner (the recall loop; no HTTP, no game deps)
  Letters/                Letter, LetterBag (queue + persistence), LetterCourier (travel math)
  Memory/                 NpcMemory (3-layer), ConversationTurn, JsonMemoryStore, MemoryCompressor
  Prompts/                PromptBuilder (multi-turn message assembly + letter lines), NpcPersona
src/ImmersiveAI.Module/   net472 — the Bannerlord module; references game DLLs
  SubModule.cs            entry point: registers behavior, drains dispatcher each tick
  ImmersiveChatBehavior.cs  the campaign behavior: dialog + conversation turn orchestration
  ImmersiveChatBehavior.Letters.cs  partial: the letter flows (NPC writes, player writes, arrivals)
  Llm/                    AnthropicChatClient, OpenAIChatClient (raw HttpClient, native tool use), factory
  Tools/WorldRecall.cs    the gift of recall: person/place/clan/realm/troop/market/own-company lookups from live campaign data
  Tools/WebWisdom.cs      the sages' counsel: web search (DuckDuckGo, game name quietly prepended), in-world framed
  Personas/PersonaBuilder.cs  builds NpcPersona from live Hero data + assigned speech style
  PromptFiles.cs          loads user-editable global/per-NPC prompt files
  ModConfig.cs            JSON config (API keys, model, token/memory limits)
  MainThreadDispatcher.cs marshals async LLM results back to the game thread
tests/ImmersiveAI.Core.Tests/  xUnit tests for Core (net8.0)
module/SubModule.xml      Bannerlord module manifest (module ID: ImmersiveAI)
tools/deploy.ps1          build + install into the game's Modules folder
Directory.Build.props     shared MSBuild props; GameFolder points at the Bannerlord install
```

The decompiled ChatAi reference is **outside this repo** at
`C:\Users\Trax\Documents\BannerlordMods\reference\ChatAi-decompiled` — consult it for
TaleWorlds API usage patterns, never copy from it.

## Architecture rules

- **Core stays pure.** No `TaleWorlds.*`, no `System.Net.Http`, no game or HTTP dependencies
  in `ImmersiveAI.Core`. That is what keeps it unit-testable. LLM backends and game glue
  live in `ImmersiveAI.Module` behind the `IChatClient` interface.
- **Memory is TWO layers** (`NpcMemory`, since 2026.08.08): `RecentTurns` (verbatim, sent as real
  user/assistant messages) and `Summary` — one rolling deep memory of everything older, rewritten
  WHOLE at each compression. This is the anti-repetition core. A third layer of distilled
  `KnownFacts` (the `hold_truth` tool + the `FACTS:` section) and a parallel `NpcGoals`/`goals.txt`
  system (`tend_goals` + `GOALS:`) were RETIRED that day — they cramped what the memory already
  held, read it back to her twice, and each cost a tool slot in every reply. Don't reintroduce
  either. `NpcMemory.KnownFacts` survives as a dead field so old saves keep what they hold.
- **Every NPC gets a distinct voice.** `PersonaBuilder` deterministically assigns a speech
  style from `Hero.StringId` so it's stable across sessions, plus personality from real
  traits. Distinct voices + relevant-only context are the levers against repetition.
- **Anthropic is the default backend**, model `claude-haiku-4-5`. Clients use raw `HttpClient`
  because the official SDK needs modern .NET and the game runs mods on .NET Framework 4.7.2.
- **Async LLM calls never touch UI directly.** Background results are queued via
  `MainThreadDispatcher.Enqueue` and drained on `SubModule.OnApplicationTick`.

## Build, test, deploy

Requires the .NET 8 SDK and a Bannerlord install (path in `Directory.Build.props`, override
in `Directory.Build.props.user` if it differs). The game must be closed (or at the main menu)
when deploying, or the DLL is locked.

```powershell
dotnet build -c Release                       # build everything
dotnet test  -c Release                        # run Core unit tests (must stay green)
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1   # build + install into the game
```

`deploy.ps1` compiles the module and copies `SubModule.xml` + the DLLs into
`<GameFolder>\Modules\ImmersiveAI\bin\Win64_Shipping_Client\`. After deploying, enable
"Immersive AI" in the Bannerlord launcher.

**Always run `dotnet test` after changing Core.** Game-integration code can't be unit-tested,
so it is verified by the user playtesting; write Core logic to be testable and keep coverage.

## User-editable runtime files (NOT in the repo)

Created on first run under `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\`:
- `config.json` — API keys, `Backend` ("OpenRouter"/"OpenAI"/"Gemini"/"DeepSeek"/"Anthropic"/"Local"),
  model, `MaxTokens`, memory limits,
  `OpenRouterApiKey` + `OpenRouterModel` (OpenRouter as a first-class backend, `Backend: "OpenRouter"` —
  one key reaches GPT and Claude, ids in OpenRouter's dotted spelling like "anthropic/claude-haiku-4.5"),
  `GeminiApiKey` + `GeminiModel` / `DeepSeekApiKey` + `DeepSeekModel` (2026.08.02 — the free road and
  the cheap one; both OpenAI-compatible, both ride OpenAIChatClient through `OpenAiDialect`, which
  exists solely because each provider spells "stop thinking" differently: Gemini's 3.x line CANNOT be
  silenced at all, hence `GeminiThinkingFloor`; DeepSeek thinks unless told `thinking: disabled`.
  Gemini's free tier trains on what it receives — say so wherever it is offered),
  `OpenAIBaseUrl` (the OpenAI backend's endpoint — default the real OpenAI; any other OpenAI-compatible
  service works: paste a base URL ending in /v1, Normalize completes it; router ids like
  "openai/gpt-5.4-mini" get classic max_tokens + `reasoning: {enabled:false}`),
  `AtmosphereLine` + `RoleplayGuidance` (configurable opening line + world-wide tone/roleplay guidance),
  `NotifyWhenReplyReady` + `ShowConversationInMessageLog`, `EnableRelationshipChanges` +
  `RelationshipChangesViaTool` (relation shifts — by default the NPC moves her own heart mid-reply via
  the `move_heart` native tool; the second, isolated feeling call is the fallback shape),
  `EnableNpcInitiatedChats` (+ related initiation knobs; `DailyInitiationRate` doubles as the
  socialness number, live-edited by the on-map stepper — `ShowSocialnessControl`; face-to-face
  reach-outs are night-damped by `InitiationScorer.NightFactor` — undamped by day, /2 shallow night
  to /8 at ~02:00, continuous at the day's edges; letters are unaffected),
  `EnableWorldTidings` + `MaxWorldTidings` + `MaxLocalRumors` (recent world events & town gossip
  folded into the situation), `EnableWorldRecall` + `MaxRecallsPerReply` (NPC tool-use: live
  campaign lookups mid-reply), `EnableLetters` (distance-travelling, save/load-surviving letters)
  + `MaxLettersInFlight` (cap on letters riding toward the player at once, default 3)
  + `EnableLetterWindow` + `LetterWindowHotkey` (the letter window, hotkey "Y" since 2026.07.15 —
  was "U", now War Sails' ship manager; ConfigVersion-2 migration moves old-default configs — correspondence as
  letter cards from letters.txt via Core `CorrespondenceLog`, composer on the courier-menu road;
  letter beats also render as ✉ cards in the chat window's thread),
  `EnableChatWindow` + `ChatWindowHotkey` + `SendInitiationsToChatWindow` (the map chat window:
  write first to anyone co-located, no greeting ceremony; NPC reach-outs land there as waiting
  messages instead of accept/decline popups),
  `SeedSelfFromWorldStory` (a never-spoken-with NPC's deep memory opens with the story the world
  tells of them — since 2026.08.08 a rewritable memory, not a self.txt page),
  `EnableActingOut` (the acting-out grammar: a small acted gesture between single *asterisks* apart
  from the spoken words — the one exception to the plain-speech rule, sparing by its own wording,
  cutting both ways; the chat window draws gestures as soft narration via Core `EmoteText` +
  `ChatWindowVM.AddSpoken`),
  `EnableMoodSwings` + `EnableWomensCycle` (the passing weather of the heart — Core `MoodTides` in the
  situation after the self: a daily humor for everyone, and for women 15–50 not with child the body's
  monthly season, "the custom of women", in four turnings on a per-woman 26–30-day calendar that also
  biases the humor; all deterministic FNV-1a(StringId, campaign day) — reloads reroll no one's weather),
  `MaxMemoryWriteTokens` (separate output budget for memory-writing calls — with the truths and aims
  retired, the only bound on how much of a person a soul may carry),
  `NotifyOnMemoryRefactor` (soft notice when an NPC's compression reworks her deep memory),
  `ModelContextWindows` (user-editable model → context-window dict the memory-percent settings
  scale against; longest key contained in the model id wins), `DevMode` (default false: hides the
  test levers, the raw-prompt inspector, and the chat window's deep-memory overview).
- `global_prompt.txt` — world-wide instructions added to every NPC (lines starting with
  `#` or `//` are ignored, matching ChatAi's convention).
- `conversation_presets.txt` — the PLAYER's own standing conversation presets for "Think" (2026.08.10): the
  button in both windows that has the player's character work out their next line, on the very
  sheet the chosen NPC would answer on. `name = wish` lines, same #-comment convention; Core
  `Prompts\PlayerThought` (the closing aside + answer-taming) + `Prompts\ConversationPresets` (the file
  model), Module `ImmersiveChatBehavior.Thoughts.cs`. Plain call, no tools, nothing recorded —
  the words land in the writing box only. Enter sends, Shift+Enter thinks. Ships with
  starter / romantic / ender; config `EnableThinkForMe`.
- `NPCs\campaign_<id>\` — one folder per **campaign** (playthrough). Hero stringIds repeat across
  campaigns, so memories are scoped by a campaign id minted once by `ImmersiveChatBehavior` and
  persisted inside the save via `SyncData` (`Campaign.UniqueGameId` changes on every save, so it
  can't be used). New campaigns get `campaign_<8hex>_<PlayerFirstName>`; pre-scoping saves all
  resolve to the fixed `campaign_legacy` and their flat NPC folders are adopted into it on first
  load. A `_campaign.txt` label (character, clan, last played) is rewritten each session.
- `NPCs\campaign_<id>\<stringId>_<FirstName>\` — one folder per NPC (e.g. `lord_7_13_1_Gunjadrid\`).
  The folder name embeds the first name for readability; identity is still the stringId. Holds:
  - `memories.json` — persisted NpcMemory for that NPC.
  - `custom_instructions.txt` — per-NPC prompt (comment lines `#`/`//` ignored), written in the
    character's own first person; folds in as "Of myself, this I hold true:". Usually begins with
    the director's spark (a generated 1–3 sentence starting truth under a `# spark:` stamp — see
    `PersonaSparkMode` in CLAUDE.md); hand-written content always wins, deleting the file re-seeds.
  - `current_situation_info.txt` — environmental facts (when/where/who) snapshot plus recent
    world tidings & local rumors (`TidingsBuilder`), rewritten every time the player opens a
    chat; built by `SituationBuilder` relative to the party the NPC speaks with, written as the
    NPC's own first-person awareness and folded into her prompt.
  - `self.txt` — the NPC's OWN evolving sense of self (`NpcSelf`), written by them in first
    person during reflection (not by the player). Kept separate from `memories.json` because
    the self is general to the NPC while memory is branching toward per-person files. Folded
    into the prompt as "Who you have become". Updated by `MemoryCompressor.ReflectAsync`.
    Begins unwritten since 2026.08.08 — the backstory seeds the DEEP MEMORY instead: an empty
    `NpcMemory.Summary` opens with a wanderer's tavern tale or a noble's encyclopedia account
    (`BackstoryBuilder.BuildStorySeed` Module + `StorySeedFormatter` Core, hooked in
    `SeedMemoryFromStory`), closed by the rumor of a renowned player (`FromPlayerFame`,
    silent under 150 renown), theirs to rewrite or let fade at every compression;
    `NpcMemory.SeededFromStory` keeps it from counting as knowing the player, and the sheet
    heads it as their own road until real history exists. Toggle: `SeedSelfFromWorldStory`.
  - `goals.txt` — RETIRED 2026.08.08; nothing reads or writes it. Existing files are left where
    they lie, so a long-played campaign's folders may still show one.
  - `letters.txt` — human-readable log of all letters carried between the player and this NPC.
  - future per-NPC files go here too.
- `NPCs\campaign_<id>\_letters.json` — letters currently on the road (Core `LetterBag`); they
  travel real in-game days by distance and must survive save/load.
- `NPCs\campaign_<id>\_battles\` — the battle chronicle (Core `BattleLedger`): one JSON per battle
  the player fought (both musters, cost, prisoners/freed, spoils, per-hero downs and fates) plus a
  running `chronicle.txt`. Allied heroes get first-person battle beats in their memories and can
  recall any shared battle by name (`recall_battle`); the freshest one rides their situation in
  full. Toggle: `EnableBattleChronicle`.
- `NPCs\campaign_<id>\_journey.json` — the road journal (Core `JourneyLog`): the light witness log
  of everyday life — recent stops with trade/recruits/garrison-drops/captives, and carried tasks
  with their outcomes — seen in the situation only by souls riding in the player's party.
  Toggle: `EnableJourneyLog`.
- `NPCs\campaign_<id>\_weddings\` — the wedding chronicle (Core `WeddingLedger`): one JSON per
  wedding of the player's plus a readable `weddings.txt`. Each holds the day's facts and TWO written
  accounts — the public day (beat into the spouse and every witness, recallable by `recall_wedding`)
  and the night, which belongs to the couple alone and is never handed to a witness in memory or by
  tool. Toggle: `EnableWeddingChronicle`.
- `NPCs\campaign_<id>\_betrothals\` — the betrothal chronicle (Core `BetrothalLedger`, 2026.08.31):
  one JSON per betrothal of the player's plus a readable `betrothals.txt` — the proposal written as
  its own day (the gift, the player's steering line, who asked, one account). Private to the two of
  the record; answered through the extended `recall_wedding`. Writing rides `EnableWeddingChronicle`.
- `NPCs\campaign_<id>\_births\` — the birth chronicle (Core `BirthLedger`): one JSON per child born
  to the player plus a readable `births.txt`. Each holds the day's facts, the children, the
  witnesses and TWO written accounts — THE HOUR (the mother's own first person; it reaches HER
  memory alone, the father is given only the fact and his own presence, and `recall_birth` refuses
  it to any witness) and THE FEAST, bought at a tier and carried by everyone who stood there. The
  feast may be bought days after the birth, when a father who was away finally rides in.
  Toggle: `EnableBirthChronicle`.
- `NPCs\_README.txt` — auto-written blurb explaining the layout to the user.

The folder layout, path resolution, and the one-time migration from the old flat
`memory\<id>.json` / `npcs\<id>.txt` files are owned by `src\ImmersiveAI.Module\NpcPaths.cs`.
**If you change the layout or file names, update `NpcPaths` (including its `RuntimeReadmeText`
and the migration in `EnsureMigrated`) and these runtime-files sections in README.md /
CLAUDE.md / AGENTS.md together.**

## In-game feature (current)

Talking to any hero shows a **"Speak freely with me. [Immersive AI]"** dialog option →
"Say something..." → a text popup → the reply appears in the conversation panel and loops.
Errors surface as a top-left "Immersive AI: ..." message. A **startup health check**
(`LlmHealthCheck`, once per process from `SubModule.OnGameStart`) pings the LLM when a campaign is
entered and reports a missing/wrong key or a dead connection in plain, actionable terms up front.

NPCs also act on their own: co-located ones may reach out for a face-to-face talk (bond-scaled
hourly rolls); distant ones may WRITE — letters travel real in-game days by map distance,
persist in `_letters.json`, and the player can send letters from town/castle/village menus
("Send a letter by courier" — it opens the letter window, hotkey "Y"; the old picker popups are
only the fallback), with the NPC answering at most once per letter. Mid-reply, NPCs
can also reach into the world's memory (native tool calls via `WorldRecall`) for live campaign
truth about people, places, clans, and realms, instead of hallucinating. Reaching-out offers
appear as persistent portrait notices in the right-side map stack (Harmony is bundled in `lib\`;
the one patch registers the notice type via a public game API and degrades to a plain popup on
failure; the notice class is save-registered in `ImmersiveAISaveDefiner` — never remove it).
See CLAUDE.md for the full design.

Known caveat: the "considers your words..." → reply transition can outrun a slow LLM call and
briefly show "..."; clicking again shows the reply. The custom UI in Milestone 2 removes this.

## Work flow for the TASKs
- Get the taks you work on from TASKS_TODO.md
- When dove move it to the end of TASKS_DONE.md, rename it if it changed or is badly formatted and add a done ts at the end (YYYY.MM.DD HH.MM.SS)
- When done with changed and tested them, recompile so the mod is rebuild automaticaly in C:\Users\Trax\Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI - dont ask the user to rebuild

## Conventions

- Match the surrounding code style; keep comments about *constraints/intent*, not narration.
- End git commit messages with `Co-Authored-By: Codex Fable 5 <noreply@anthropic.com>`.
- The user commits from GitHub Desktop too — write descriptive commit messages, expect a
  shared history. Closing VS Code / Explorer windows on the repo may be needed before folder
  renames on Windows.
- `<GameFolder>` currently: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`.
