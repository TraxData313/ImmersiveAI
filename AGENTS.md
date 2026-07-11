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
   multi-turn conversation, a rolling summary of older exchanges, durable "known facts",
   and a distinct speech style.
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
- **Memory is three layers** (`NpcMemory`): `RecentTurns` (verbatim, sent as real
  user/assistant messages), `Summary` (rolling, LLM-compressed when turns exceed
  `MaxRecentTurns`), and `KnownFacts` (distilled one-liners). This is the anti-repetition core.
- **Every NPC gets a distinct voice.** `PersonaBuilder` deterministically assigns a speech
  style from `Hero.StringId` so it's stable across sessions, plus personality from real
  traits. Distinct voices + relevant-only context are the levers against repetition.
- **Anthropic is the default backend**, model `Codex-opus-4-8`. Clients use raw `HttpClient`
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
- `config.json` — API keys, `Backend` ("Anthropic"/"OpenAI"), model, `MaxTokens`, memory limits,
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
  + `EnableLetterWindow` + `LetterWindowHotkey` (the letter window, hotkey "U" — correspondence as
  letter cards from letters.txt via Core `CorrespondenceLog`, composer on the courier-menu road;
  letter beats also render as ✉ cards in the chat window's thread),
  `EnableChatWindow` + `ChatWindowHotkey` + `SendInitiationsToChatWindow` (the map chat window:
  write first to anyone co-located, no greeting ceremony; NPC reach-outs land there as waiting
  messages instead of accept/decline popups),
  `SeedSelfFromWorldStory` (first self.txt page seeded from the story the world tells of them),
  `EnableNpcGoals` + `MaxNpcGoals` (personal aims in goals.txt: the `tend_goals` tool shapes them
  one at a time mid-conversation, reflection reworks them wholesale via a `GOALS:` replace-all section),
  `MaxKnownFacts` (lasting-truths budget; the NPC rewrites the whole list at each reflection —
  replace, not append) + `MaxMemoryWriteTokens` (separate output budget for memory-writing calls),
  `NotifyOnMemoryRefactor` (soft notice when an NPC's compression reworks her deep memory),
  `ModelContextWindows` (user-editable model → context-window dict the memory-percent settings
  scale against; longest key contained in the model id wins), `DevMode` (default false: hides the
  test levers, the raw-prompt inspector, and the chat window's deep-memory overview).
- `global_prompt.txt` — world-wide instructions added to every NPC (lines starting with
  `#` or `//` are ignored, matching ChatAi's convention).
- `NPCs\campaign_<id>\` — one folder per **campaign** (playthrough). Hero stringIds repeat across
  campaigns, so memories are scoped by a campaign id minted once by `ImmersiveChatBehavior` and
  persisted inside the save via `SyncData` (`Campaign.UniqueGameId` changes on every save, so it
  can't be used). New campaigns get `campaign_<8hex>_<PlayerFirstName>`; pre-scoping saves all
  resolve to the fixed `campaign_legacy` and their flat NPC folders are adopted into it on first
  load. A `_campaign.txt` label (character, clan, last played) is rewritten each session.
- `NPCs\campaign_<id>\<stringId>_<FirstName>\` — one folder per NPC (e.g. `lord_7_13_1_Gunjadrid\`).
  The folder name embeds the first name for readability; identity is still the stringId. Holds:
  - `memories.json` — persisted NpcMemory for that NPC.
  - `custom_instructions.txt` — per-NPC prompt (comment lines `#`/`//` ignored).
  - `current_situation_info.txt` — environmental facts (when/where/who) snapshot plus recent
    world tidings & local rumors (`TidingsBuilder`), rewritten every time the player opens a
    chat; built by `SituationBuilder` relative to the party the NPC speaks with, written as a
    gentle second-person narration and folded into her prompt.
  - `self.txt` — the NPC's OWN evolving sense of self (`NpcSelf`), written by them in first
    person during reflection (not by the player). Kept separate from `memories.json` because
    the self is general to the NPC while memory is branching toward per-person files. Folded
    into the prompt as "Who you have become". Updated by `MemoryCompressor.ReflectAsync`.
    First seeded from the story the world tells of them: a wanderer's tavern tale or a noble's
    encyclopedia account (`BackstoryBuilder` Module + `SelfSeedFormatter` Core, hooked in
    `LoadOrSeedSelf`); deleting the file re-seeds. Toggle: `SeedSelfFromWorldStory`.
  - `goals.txt` — the NPC's OWN personal aims (`NpcGoals`), one per line, what they strive for of
    their own will. General to them like the self; authored by them via the `tend_goals` tool
    (mid-conversation) and the reflection `GOALS:` section (wholesale, replace-all like FACTS).
    Folded into the prompt as "What you strive for". Comment lines `#`/`//` ignored; deleting it
    clears their aims. Toggle: `EnableNpcGoals`.
  - `letters.txt` — human-readable log of all letters carried between the player and this NPC.
  - future per-NPC files go here too.
- `NPCs\campaign_<id>\_letters.json` — letters currently on the road (Core `LetterBag`); they
  travel real in-game days by distance and must survive save/load.
- `NPCs\_README.txt` — auto-written blurb explaining the layout to the user.

The folder layout, path resolution, and the one-time migration from the old flat
`memory\<id>.json` / `npcs\<id>.txt` files are owned by `src\ImmersiveAI.Module\NpcPaths.cs`.
**If you change the layout or file names, update `NpcPaths` (including its `RuntimeReadmeText`
and the migration in `EnsureMigrated`) and these runtime-files sections in README.md /
CLAUDE.md / AGENTS.md together.**

## In-game feature (current)

Talking to any hero shows a **"Speak freely with me. [Immersive AI]"** dialog option →
"Say something..." → a text popup → the reply appears in the conversation panel and loops.
Errors surface as a top-left "Immersive AI: ..." message.

NPCs also act on their own: co-located ones may reach out for a face-to-face talk (bond-scaled
hourly rolls); distant ones may WRITE — letters travel real in-game days by map distance,
persist in `_letters.json`, and the player can send letters from town/castle/village menus
("Send a letter by courier"), with the NPC answering at most once per letter. Mid-reply, NPCs
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
