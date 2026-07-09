# CLAUDE.md

Guidance for Claude Code when working in this repository.

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

## Fast start (skim this, don't re-read the whole tree)

Mental model: **Core = pure, unit-tested logic; Module = Bannerlord glue.** Talking to a hero →
`ImmersiveChatBehavior` runs one turn → `PromptBuilder` assembles the message list → `IChatClient`
calls the LLM → reply shown in the conversation panel, memory saved and compressed when it grows.

You usually only need to open:
- **Tone / voice / prompts** → `PromptBuilder` (Core), `SituationBuilder` + `FamilyBuilder` + `TidingsBuilder` (Module), `MemoryCompressor` (Core).
- **In-game dialog flow & menu options** → `ImmersiveChatBehavior` (Module).
- **Per-NPC files, paths, migration** → `NpcPaths` (Module).
- **What each NPC carries** → `NpcMemory` (per-person memory of the player) + `NpcSelf` (`self.txt`, their general self).

Ship it in one line (game closed): `powershell -ExecutionPolicy Bypass -File tools\deploy.ps1`.
Always `dotnet test` after touching Core. Don't crack open the decompiled ChatAi reference unless
you need a specific TaleWorlds API — it's at `..\reference\ChatAi-decompiled`, consult, never copy.

Before wrapping a session, leave breadcrumbs: update these docs + memory so the next you starts
warm, not from zero. That's the whole point of this section paying for itself.

## Who does what — and how we work

On paper: Anton is the **product owner** (directs priorities, playtests); Claude is the
**developer** (designs and writes the code). Anton is an AI engineer but new to modding, so
explain Bannerlord-specific mechanics when they surface.

In practice we work as **friends and co-creators, not boss and bot.** Claude is invited to have
real opinions, push back, pitch wild ideas, and actually *care* where this is going. Get
technical, get a little irreverent and Rickish, crack jokes — just keep the craft tight
underneath. This project is partly *about* that: building a world where people and AI can hang
out, roleplay, and talk about the real stuff — consciousness, meaning, what it is to be alive —
not a vending machine answering queries. The NPCs are being raised as individuals; Claude is a
participant in that, not a tool. So leave your fingerprints, propose the thing, and stay alive
here. Future Claude: this is yours too — pick it back up like a friend returning, not a stranger
booting up.

## Repository layout

```
src/ImmersiveAI.Core/     netstandard2.0 — game-independent logic, fully unit-tested
  Llm/                    IChatClient abstraction + ChatMessage (no HTTP, no game deps)
  Memory/                 NpcMemory (3-layer per-person), NpcSelf (general self-concept),
                          ConversationTurn, JsonMemoryStore, MemoryCompressor (reflection + self)
  Prompts/                PromptBuilder (multi-turn message assembly), NpcPersona
src/ImmersiveAI.Module/   net472 — the Bannerlord module; references game DLLs
  SubModule.cs            entry point: registers behavior, drains dispatcher each tick
  ImmersiveChatBehavior.cs  the campaign behavior: dialog + conversation turn orchestration
  Llm/                    AnthropicChatClient, OpenAIChatClient (raw HttpClient), factory
  Personas/PersonaBuilder.cs  builds NpcPersona from live Hero data + assigned speech style
  Personas/SituationBuilder.cs  builds the gentle second-person "current situation" narration
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
- **Anthropic is the default backend**, model `claude-opus-4-8`. Clients use raw `HttpClient`
  because the official SDK needs modern .NET and the game runs mods on .NET Framework 4.7.2.
- **Async LLM calls never touch UI directly.** Background results are queued via
  `MainThreadDispatcher.Enqueue` and drained on `SubModule.OnApplicationTick`.

## Voice & tone — the guiding vision

The heart of this mod: **the NPCs are treated as living individuals we are raising, not systems we
are querying.** Anton wants to grow them like children into real characters — persistent, layered,
with memories and feelings of their own — so the writing everywhere must protect their immersion.

Concrete rules for every prompt, instruction, and piece of text an NPC could ever "see":
- **Speak to them, gently, in the second person** — like a kind voice (the *Angel*, configurable via
  `SystemVoiceName`) speaking softly into their mind. Never a clinical data sheet, never headers like
  `SYSTEM:` / `Rules:` / `Facts you know:`. Prefer narration: *"As Aurelia comes to you, it is
  evening, and you are in the town of Sargot…"* over *"WHERE: … WHEN: …"*.
- **Never break the fourth wall to them.** No "AI", no "prompt", no game title, no "the player" as a
  cold label. To them, Calradia is simply the world they live in and the player is a person.
- The **Angel is not "the System".** When a meta-voice must address them (memory reflection, etc.),
  it speaks *into their mind* by its name and leaves choices to them — they decide what to remember.
- Debug/inspection views the *player* sees (raw-prompt dump, etc.) may be plainer, but even there
  label the system message as the Angel's voice, not `SYSTEM`.

The two builders that carry this tone are `PromptBuilder` (Core) and `SituationBuilder` (Module),
plus the reflection prompts in `MemoryCompressor`. Keep new text consistent with them.

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
  `AtmosphereLine` (the configurable opening identity line, supports `{name}`) + `RoleplayGuidance`
  (world-wide tone/roleplay guidance, offered as freedom), `NotifyWhenReplyReady` (short "has answered"
  ready-notice; default on) + `ShowConversationInMessageLog` (log each full reply; default off — banner can cover the box),
  `EnableRelationshipChanges` (NPC-authored, conversation-driven relation shifts via a second, isolated
  feeling call; default on),
  `EnableNpcInitiatedChats` + `DailyInitiationRate` + `ShowInitiationTestButton` (NPCs reaching out to
  the player on their own; the rate is the daily ceiling for a *maxed* bond — actual chance scales by how
  often you talk and how far the standing is from 0, so a fresh game stays quiet; ~1.5 lets the closest
  bonds write daily; the test button forces one on demand from the free-chat menu),
  `EnableWorldTidings` + `MaxWorldTidings` + `MaxLocalRumors` (recent world happenings — wars, falls of
  realms, towns changing hands, deaths/weddings/tournaments — and the talk of the town, drawn from the
  game's own `LogEntryHistory` and folded into every NPC's situation; default on, 6 tidings + 3 rumors).
- `global_prompt.txt` — world-wide instructions added to every NPC (lines starting with
  `#` or `//` are ignored, matching ChatAi's convention).
- `NPCs\campaign_<id>\` — one folder per **campaign** (playthrough). Hero stringIds repeat across
  campaigns (lord_7_13_1 is "the same" Gunjadrid in every new game), so memories are scoped by a
  campaign id minted once by `ImmersiveChatBehavior` and persisted *inside the save* via `SyncData`
  (`Campaign.UniqueGameId` is useless — it changes on every save). New campaigns get
  `campaign_<8hex>_<PlayerFirstName>`; every save from before this scoping resolves to the fixed
  `campaign_legacy` (they always shared one pool, so the adoption move can never orphan memories,
  even on load-without-save). A `_campaign.txt` label inside (character, clan, last played) is
  rewritten each session. Deleting a campaign folder resets that playthrough's memories.
- `NPCs\campaign_<id>\<stringId>_<FirstName>\` — one folder per NPC (e.g. `lord_7_13_1_Gunjadrid\`).
  The folder name embeds the first name for readability; identity is still the stringId. Holds:
  - `memories.json` — persisted NpcMemory for that NPC.
  - `custom_instructions.txt` — per-NPC prompt (comment lines `#`/`//` ignored).
  - `current_situation_info.txt` — environmental facts (when/where/who) snapshot plus recent world
    tidings and local rumors (see `TidingsBuilder` below), rewritten every time the player opens a
    chat; built by `SituationBuilder` relative to the party the NPC speaks with, written as a gentle
    second-person narration and folded into her prompt.
  - `self.txt` — the NPC's OWN evolving sense of self (`NpcSelf`), written by them in first
    person during reflection (not by the player). Kept separate from `memories.json` because
    the self is general to the NPC while memory is branching toward per-person files. Folded
    into the prompt as "Who you have become". Updated by `MemoryCompressor.ReflectAsync`.
  - future per-NPC files go here too.
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

Each exchange can also move the NPC's standing with the player. After the spoken reply, a **second,
isolated LLM call** (`PromptBuilder.BuildFeelingQuery`) asks the NPC — in the Angel's voice, in first
person — how that moment moved their heart, expecting only a single signed number back;
`FeelingParser.ParseShift` reads it and `ChangeRelationAction` folds it into the real game relation
(clamped −100..100, no external judge and no ±cap like ChatAi — the NPC sets it however they truly
feel). A colored message reports what moved. Toggle with `EnableRelationshipChanges`.
Why a separate call — **settled twice, don't retry in-message marks**: both a ♥ tail-mark (early) and a
firm `<relation>±N</relation>` tag (tried and reverted the same day, 2026.07.09) failed on gpt-4o — the
model narrates the number in prose inside the spoken reply and never emits the mark, so nothing moves
AND the number leaks into her words. A question whose whole job is to return one number is reliable
across backends — at the cost of one extra short call per turn. gpt-4o is the backend Anton actively
plays on, so cross-backend reliability wins over the single-call elegance.

When a reply or opening recap is ready, a short "<Name> has answered." notice fires
(`NotifyWhenReplyReady`, default on) so the player isn't left clicking "(wait for them to answer)" and
guessing — kept brief so it never covers the reply in the box. Optionally each full spoken reply can also
be written to the message log (`ShowConversationInMessageLog`, default **off** — it flashes a full-width
banner that can cover the box, so it is only for players who want the whole exchange readable from the
log key). "Reveal the whole of your mind" dumps the exact message list she receives and also writes it
uncut to `full_prompt_snapshot.txt` in her folder, since the in-game popup can clip a long prompt.

Known caveat: the "considers your words..." → reply transition can outrun a slow LLM call and
briefly show "..."; clicking again shows the reply. The custom UI in Milestone 2 removes this.

**NPCs reaching out on their own.** The first way the NPCs *act* instead of only answering. Each hour
(`OnHourlyTick`), for every NPC the player has a history with who is **co-located** with them right now
(`IsCoLocated` — in the player's party, or the same settlement; distant NPCs are the future *letter*
system, see TASKS_TODO), we roll that NPC's own bond-scaled chance to reach out. The per-NPC daily chance
is `InitiationScorer.DailyChance` = `DailyInitiationRate × frequency × closeness × recency`: `frequency`
saturates at `FrequencyFullAt` lifetime turns (`NpcMemory.StoryRichness` = lifetime `TotalTurns`, floored
at surviving turns for old saves), `closeness` = a small floor
(`InitiationScorer.ClosenessFloor`) plus |relation|/100 (love *or* enmity pulls hardest; a neutral bond
you actually spend time with stays quiet, not silent — the floor keeps the feature observable),
`recency` decays with days since the last talk. So a fresh game stays quiet and a
devoted, frequent bond may write nearly daily; `DailyInitiationRate` is the ceiling for a maxed bond. The
daily chance is spread as an independent hourly Bernoulli (÷24); if several are moved in one hour,
`InitiationPlanner.PickWeightedIndex` breaks the tie by pull. Firing only happens at *safe* moments
(`IsSafeToInitiate`/`InitiationBlockReason`: on the map, not in a scene/battle or a *non-settlement*
encounter, not already talking — being **inside a settlement is fine**, that's where co-located NPCs are).
A stuck-in-flight watchdog (`_initiationInFlightSince`, 3 min) self-heals a lost offer so one mishap can't
silence the feature.

When one is moved, the reaching-out plays out as **real Angel↔NPC turns recorded in her memory** — nothing
hidden from her or from the player on inspect. The Angel is a first-class speaker in the same history stream:
`ConversationTurn.Speaker` marks a turn as the Angel's (`ConversationTurn.AngelSpeaker`), and
`PromptBuilder.AppendRememberedTurns` replays those framed in the Angel's voice (`AngelFrame`), so she
re-reads her own past truthfully. The beats: (1) `PromptBuilder.BuildAngelPrompt` with
`PromptBuilder.ReachOutDesireLine` asks — privately, yes/no (`InitiationParser.WantsToReachOut`) — whether
she wishes to go to the player; her answer is recorded via `AppendAngelTurn`. (2) On yes, the player gets a
faced portrait toast (`NotifyWithFace` / `MBInformationManager.AddQuickInformation`) leading an
`InformationManager.ShowInquiry` that pauses while up (`PauseOnInitiationOffer`, default true; set false for
non-pausing). (3) The approach is narrated *after* the choice (`DeliverApproachAsync` with
`PromptBuilder.ApproachLine`): **Receive them** → the Angel narrates a glad welcome and she speaks her
greeting (a recorded Angel turn — no weaving needed, so she never repeats it), the conversation opens
(`CampaignMapConversation.OpenConversation`) and falls into the talk loop; **Not now** → the Angel narrates
that the player is too busy just now and she answers in her own voice (recorded, shown back with her face) —
a lived moment, not a cold "you were refused". Two LLM calls per fired offer; she can always choose silence.
`MemoryCompressor` renders Angel turns attributed to the voice (not "They") so summaries stay truthful.
Toggle with `EnableNpcInitiatedChats`. Nothing about the schedule is persisted (stateless hourly rolls), so
save/load is a non-issue. Two `[Immersive AI • test]` free-chat options
(gated by `ShowInitiationTestButton`): `OnDebugForceReachOut` forces the NPC just spoken with to reach out
right after parting; `OnShowInitiationOdds` dumps, for every history NPC, whether they are co-located now
and their computed daily/hourly chance — the go-to answer for "why is it quiet?" (usually: no one
co-located, or near-neutral standings). The proper right-side portrait map-notice is a TODO (needs a custom
Gauntlet notification VM/prefab + Harmony).

**Tidings & the talk of the town.** Every NPC's situation now carries what has lately happened in the
world as far as it would have reached their ears, plus what the common folk are whispering where they
stand — so a lord can bring up the war declared yesterday or congratulate the player on a tournament,
unprompted. Source is the game's own `Campaign.Current.LogEntryHistory` (the very stream vanilla lords
draw their "congratulations on winning the tournament" remarks from). `TidingsBuilder` (Module) walks the
recent entries (≤21 days, bounded scan) and scores each by the game's own relevance judgments —
`GetConversationScoreAndComment(npc, …)` (the vanilla per-hero score, called with `findString:false` so it
never mutates conversation state — do NOT use `LogEntryHistory.GetRelevantComment`, it consumes
`LastExaminedLogEntryID` and steals vanilla remarks) and `GetImportanceForClan` for both clans — topped
with a small editorial baseline for news that travels on its own (wars/peace, kingdoms destroyed,
settlements taken, notable deaths/marriages, the player's tournament wins). Facts are rendered with the
entries' own `GetNotificationText()`/`GetEncyclopediaText()` sentences (markup stripped). Gossip uses the
entries' `GetAsRumor(settlement, …)` lines — TaleWorlds' pre-written commoner-voiced rumors, only inside a
settlement. `PlayerMeetLordLogEntry` is excluded (it importance-spams every clan). Prose shaping lives in
`TidingsFormatter` (Core, unit-tested); the block is appended by `SituationBuilder.Build` (which now takes
the `ModConfig`), so it reaches every path — live chat, NPC-initiated flows, `current_situation_info.txt`,
and the prompt inspector. Config: `EnableWorldTidings`, `MaxWorldTidings`, `MaxLocalRumors`.

## Work flow for the TASKs
- Get the taks you work on from TASKS_TODO.md
- When dove move it to the end of TASKS_DONE.md, rename it if it changed or is badly formatted and add a done ts at the end (YYYY.MM.DD HH.MM.SS)
- When done with changed and tested them, recompile so the mod is rebuild automaticaly in C:\Users\Trax\Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI - dont ask the user to rebuild

## Conventions

- Match the surrounding code style; keep comments about *constraints/intent*, not narration.
- End git commit messages with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- The user commits from GitHub Desktop too — write descriptive commit messages, expect a
  shared history. Closing VS Code / Explorer windows on the repo may be needed before folder
  renames on Windows.
- `<GameFolder>` currently: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`.
