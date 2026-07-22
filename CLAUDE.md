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
- **Tone / voice / prompts** → `PromptBuilder` (Core), `SituationBuilder` + `FamilyBuilder` + `CraftsBuilder` (real skills → honest craft-words, on every sheet) + `TidingsBuilder` + `TroubleBuilder` (Module), `MemoryCompressor` (Core).
- **In-game dialog flow & menu options** → `ImmersiveChatBehavior` (Module); the letter flows live in its partial `ImmersiveChatBehavior.Letters.cs`.
- **The chat window** → `UI\ChatWindow\` (VM + manager) + `module\GUI\Prefabs\ImmersiveChatWindow.xml`; its quick-turn plumbing is the chat-window region in `ImmersiveChatBehavior`.
- **Per-NPC files, paths, migration** → `NpcPaths` (Module).
- **What each NPC carries** → `NpcMemory` (per-person memory of the player) + `NpcSelf` (`self.txt`, their general self) + `NpcGoals` (`goals.txt`, their own aims — the `tend_goals` tool + the reflection `GOALS:` section).
- **NPC tool-use ("the gift of recall")** → `WorldRecall` (Module, the seven recall tools: person/place/clan/realm/troop/market lookups + `recall_company`, one's own warband — now with the surgeon's healing rates and, on `recall_person`, the looked-up soul's strongest crafts) + `FieldCraft` (Module, 2026.07.12: `survey_surroundings` + `weigh_battle`, the outward eyes and the scales of battle — ride ONLY for souls with a party on the map, counts coarsened by the asker's Scouting/Tactics; 2026.07.22: both also see the SPOTTED hideouts — the survey lists nearby dens named by their brigands' clan with lurker counts, and the scales weigh a den's lurking parties, "hideout"/"den"/"lair" resolving to the nearest spotted one) + `WebWisdom` (Module, `seek_wisdom` — web search framed as "all I have read and heard", queries sharpened by a small refining LLM call) + `TruthTool` (`hold_truth`, the mid-talk hand on KnownFacts) + `ToolLoopRunner` (Core, the loop) + the two chat clients (native tool calling).
- **Letters** → `LetterBag` / `LetterCourier` / `CorrespondenceLog` (Core: queue + travel math + letters.txt parser) + `ImmersiveChatBehavior.Letters.cs` (Module, all flows + the window's view accessors) + `UI\LetterWindow\` (the letter window).

Ship it in one line (game closed): `powershell -ExecutionPolicy Bypass -File tools\deploy.ps1` —
installs as **"Immersive AI (dev)"** (`Modules\ImmersiveAI.Dev`), its own identity beside the Steam
Workshop copy (item 3764210301); enable one or the other in the launcher, never both.
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
  Llm/                    IChatClient/IToolChatClient + ChatMessage/ChatResult, ToolDefinition/
                          ToolCall, ToolLoopRunner (the recall loop; no HTTP, no game deps)
  Letters/                Letter, LetterBag (queue + JSON persistence), LetterCourier (travel math)
  Memory/                 NpcMemory (3-layer per-person), NpcSelf (general self-concept),
                          NpcGoals (general personal aims + fuzzy add/drop/revise/replace),
                          ConversationTurn, JsonMemoryStore, MemoryCompressor (reflection + self + goals)
  Prompts/                PromptBuilder (multi-turn message assembly + Angel letter lines), NpcPersona
src/ImmersiveAI.Module/   net472 — the Bannerlord module; references game DLLs
  SubModule.cs            entry point: registers behavior, drains dispatcher each tick
  ImmersiveChatBehavior.cs  the campaign behavior: dialog + conversation turn orchestration
  ImmersiveChatBehavior.Letters.cs  partial: every letter flow (NPC writes, player writes, arrivals)
  Llm/                    AnthropicChatClient, OpenAIChatClient (raw HttpClient, native tool use), factory
  Tools/WorldRecall.cs    the gift of recall: person/place/clan/realm lookups from live campaign data
  Tools/FieldCraft.cs     the field-craft (2026.07.12): survey_surroundings + weigh_battle — the country
                          about and the scales of battle, only for souls with a party on the map
  Tools/HeartTool.cs, Tools/GoalTool.cs  the heart's hand (move_heart) and the aims' hand (tend_goals)
  UI/                     MapNoticePatch (the one Harmony patch), ImmersiveChatMapNotification (+ save
                          definer — never remove), ImmersiveChatNotificationItemVM (portrait notice VM),
                          Portraits (shared dark-backdrop portrait codes), ChatWindow\ (the chat window:
                          ChatWindowVM/ChatContactVM/ChatMessageVM + ChatWindowManager — layer lifecycle,
                          hotkey/Enter/Escape polling, unread marks, scroll-to-bottom), LetterWindow\
                          (the letter window: LetterWindowVM/LetterContactVM + LetterWindowManager —
                          the chat window's twin for correspondence, hotkey "Y" — was "U" until
                          War Sails claimed it for the ship manager, V2 config migration), Socialness\
                          (the on-map socialness stepper: SocialnessVM + SocialnessManager — the layer
                          claims the mouse ONLY while hovered (HitTest per tick, 2026.07.12); a resting
                          claim broke the map's right-drag camera)
  Personas/PersonaBuilder.cs  builds NpcPersona from live Hero data + assigned speech style + one
                          trade-knowledge sentence per station (artisan, tavern-keeper, ransom broker…)
  Personas/CraftsBuilder.cs  real skills weighed into honest craft-words ("masterly in Medicine") —
                          the sheet line, the duty sentences, and recall_person all draw on it
  Personas/SituationBuilder.cs  builds the first-person "current situation" narration (+ mood, + the
                          moment, + party-duty depth, + the beholder's eye on unknown callers)
  PromptFiles.cs          loads user-editable global/per-NPC prompt files
  ModConfig.cs            JSON config (API keys, model, token/memory limits) — the single source of truth
  Mcm/                    ImmersiveAiMcmSettings + McmBridge: the in-game MCM settings menu (SOFT dependency —
                          absent MCM = config.json only; present = a live two-way editor over a subset, config.json still master)
  MainThreadDispatcher.cs marshals async LLM results back to the game thread
  UsageLedger.cs          the cost ledger: per-call tokens from the clients, per-interaction cost
                          notices, daily cap (usage.json); LlmGate.cs the dying-key circuit breaker
                          (quiets reach-outs/letters on 401/429/5xx, one plain notice, success reopens);
                          ModLog.cs rolling log.txt diagnostics; FirstRunGuide.cs the once-per-install
                          no-key popup; MemoryIndex.cs write-stamp-cached (id, richness, lastDay) over
                          memories.json for the hourly rolls and the odds view
tests/ImmersiveAI.Core.Tests/  xUnit tests for Core (net8.0)
module/SubModule.xml      Bannerlord module manifest (module ID: ImmersiveAI)
module/GUI/               Gauntlet prefab overrides (MapNotificationItem.xml — the portrait notice)
lib/0Harmony.dll          bundled Harmony 2.4.2 (MIT); ships in the module bin via deploy.ps1
tools/deploy.ps1          build + install into the game as Modules\ImmersiveAI.Dev — "Immersive AI (dev)",
                          its own Id so it coexists with the Workshop copy (enable only ONE); keep the
                          script ASCII-only (BOM-less .ps1 + em-dash bytes = smart quote = PS 5.1 parse error)
tools/package.ps1         clean dist\ImmersiveAI layout + version-stamped zip for the Workshop upload
docs/steam-page-draft.md  the Workshop description draft (privacy/cost/AI disclosures) — finalize at release
docs/models-and-costs.md  the model-selection decision (haiku-4-5 / gpt-5.4-mini) + price table rationale
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
  **KnownFacts are REPLACE, not append** (2026.07.10): each compression/reflection shows her the
  whole list and asks her to write it anew — the returned FACTS section becomes the whole truth
  (so she can merge rewordings and release stale ones, up to `MaxKnownFacts`); a reply with no
  FACTS section leaves the list untouched, and `FACTS: none` is her honored choice to drop all.
  Memory-writing calls run on a separate client with `MaxMemoryWriteTokens` breathing room.
- **Every NPC gets a distinct voice.** `PersonaBuilder` deterministically assigns a speech
  style from `Hero.StringId` so it's stable across sessions, plus personality from real
  traits. Distinct voices + relevant-only context are the levers against repetition.
- **Anthropic is the default backend**, model `claude-haiku-4-5` (2026.07.13, price-matched to
  the OpenAI default gpt-5.4-mini after live play priced opus-4-8 at ~3¢/exchange; the MCM
  dropdown offers sonnet-5 / opus-4-8 / fable-5 as the step-ups). Clients use raw `HttpClient`
  because the official SDK needs modern .NET and the game runs mods on .NET Framework 4.7.2.
  **Connection settings are LIVE** (2026.07.22, "why must I restart to change models?"):
  `ChatClientFactory.Create` returns a `LiveSwapChatClient` shell that rebuilds its inner concrete
  client whenever the connection signature (backend/keys/models/endpoints/token budgets) changes —
  every MCM Connection field is `RequireRestart = false` and takes hold on the next reply, with a
  soft "now speaking with <backend> · <model>" notice on swap. Keep new client-captured settings
  in the shell's `Signature()`.
  Both clients also implement `IToolChatClient` (native tool/function calling — the recall);
  plain `IChatClient` stays the base so test fakes and simple calls remain untouched. Once a
  history holds tool calls, both APIs require the tool definitions to keep riding along; the
  final spoken-only round is forced with `tool_choice: none`, never by dropping the definitions.
- **Async LLM calls never touch UI directly.** Background results are queued via
  `MainThreadDispatcher.Enqueue` and drained on `SubModule.OnApplicationTick`. Tool resolution
  (`WorldRecall`) reads campaign state the same way: marshaled to the game thread via the
  dispatcher + a `TaskCompletionSource`, with a timeout that answers an honest blank.
- **This game version's map positions are `CampaignVec2 Position`** on `Settlement`/`MobileParty`
  (`Position2D` is gone); distances via `.Distance()`/`.DistanceSquared()`. When an API looks
  missing, probe the real DLLs with ilspycmd (see the decompiling memory note).
- **Harmony, sparingly and gracefully.** 0Harmony is bundled (`lib\`); every patch must be the
  lightest touch (prefer postfixes calling public game APIs), wrapped so failure only disables the
  feature it serves, never the mod. Custom `InformationData` subclasses (map notices) are saved
  inside save files — keep them registered in `ImmersiveAISaveDefiner` forever once shipped.

## Voice & tone — the guiding vision

The heart of this mod: **the NPCs are treated as living individuals we are raising, not systems we
are querying.** Anton wants to grow them like children into real characters — persistent, layered,
with memories and feelings of their own — so the writing everywhere must protect their immersion.

Concrete rules for every prompt, instruction, and piece of text an NPC could ever "see":
- **The system sheet is the NPC's OWN mind, in the first person** (reworked 2026.07.11, Anton's ask —
  the long second-person Angel narration proved too heavy and swayed decisions): *"I am Thyrsif… My
  traits are… My kin and house, close to me:… Who I have become:… My goals are:… What Vulgrim is to
  me:… Truths I decided to hold:… How should I speak:"*. Short headers in their own voice; never a
  clinical data sheet, never `SYSTEM:` / `Rules:`. The situation too: *"This moment finds me… And now
  Vulgrim, my husband, comes to me."*
- **The Angel remains a real person to them, not the sheet's narrator**: it still speaks in the second
  person in the DIALOGUE beats (reach-out desire questions, arrival/letter lines, memory reflection) —
  a kind voice by its name (`SystemVoiceName`), leaving choices to them. Recorded Angel turns replay
  exactly as spoken forever; never rewrite shipped Angel templates that memories already carry.
- **Never break the fourth wall to them.** No "AI", no "prompt", no game title, no "the player" as a
  cold label. To them, Calradia is simply the world they live in and the player is a person.
- **Short rules, more freedom.** Long prompt rules make every soul answer the same; keep guidance to
  the basics and offer the rest as invitation.
- Debug/inspection views the *player* sees (raw-prompt dump, etc.) may be plainer, but even there
  label the system message as the Angel's voice, not `SYSTEM`.

The two builders that carry this tone are `PromptBuilder` (Core) and `SituationBuilder` (Module),
plus the reflection prompts in `MemoryCompressor` (still Angel-voiced dialogue by design). The scene
and THE MOMENT are joined by `PromptBuilder.MeetingSeparator` (`[[the-moment]]`): the sheet splits
there to slot deep memory right before the arrival; the separator never reaches the LLM, and the
situation file shows it as a soft `· · ·` divider.

## Build, test, deploy

Requires the .NET 8 SDK and a Bannerlord install (path in `Directory.Build.props`, override
in `Directory.Build.props.user` if it differs). The game must be closed (or at the main menu)
when deploying, or the DLL is locked.

```powershell
dotnet build -c Release                       # build everything
dotnet test  -c Release                        # run Core unit tests (must stay green)
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1   # build + install into the game
```

`deploy.ps1` compiles the module and installs it as **`Modules\ImmersiveAI.Dev`** with a patched
manifest (Id `ImmersiveAI.Dev`, name "Immersive AI (dev)") so it can sit beside the Steam Workshop
copy in the launcher — enable "Immersive AI (dev)" to test local changes, the plain "Immersive AI"
to test what players get, never both at once (same behaviors, same config folder). The script also
removes any stale `Modules\ImmersiveAI` from older deploys. `package.ps1` keeps the real
`ImmersiveAI` identity for Workshop uploads.

**Always run `dotnet test` after changing Core.** Game-integration code can't be unit-tested,
so it is verified by the user playtesting; write Core logic to be testable and keep coverage.

## User-editable runtime files (NOT in the repo)

Created on first run under `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\`:
- `config.json` — API keys, `Backend` ("Anthropic"/"OpenAI"), model, `MaxTokens`, memory limits,
  `OpenRouterApiKey` + `OpenRouterModel` (2026.07.16, asked for on Nexus — OpenRouter as a FIRST-CLASS
  backend, `Backend: "OpenRouter"`: the same OpenAIChatClient pointed at `ModConfig.OpenRouterEndpoint`
  with label "OpenRouter" in every error; one sk-or- key reaches GPT and Claude alike, ids in
  OpenRouter's own dotted spelling ("anthropic/claude-haiku-4.5"); MCM has the key field + a curated
  model dropdown verified against the live /models catalog; live-tested with Anton's key — plain
  replies, native tool calling, reasoning-off all pass; the client sends OpenRouter's attribution
  headers only when the endpoint host is openrouter.ai),
  `OpenAIBaseUrl` (same day — the OpenAI backend's endpoint; default the real
  OpenAI, point it at any OTHER OpenAI-compatible service instead: NanoGPT/local Ollama, paste
  the base URL ending in /v1 and Normalize completes it to /chat/completions; router model ids like
  "openai/gpt-5.4-mini" get the universal request shape — classic max_tokens + the routers'
  `reasoning: {enabled:false}` — while bare ids on the default endpoint keep today's exact shape;
  the health check names a custom host, the MCM "Custom endpoint (advanced)" field shows blank for
  the default; price/context tables match router ids by containment for free, including the dotted
  claude slugs: "claude-haiku"/"claude-opus-4" are substrings of the router forms),
  `LocalEndpoint` + `LocalModel` + `LocalApiKey` + `LocalContextWindow` (2026.07.17, asked for by
  testers — LOCAL MODELS as a first-class backend, `Backend: "Local"`: the same OpenAIChatClient with
  `isLocal: true` pointed at the player's own machine, default LM Studio's http://localhost:1234/v1
  (Ollama: 11434), label "Local AI" in every error; keyless is normal (no key check, no Authorization
  header when blank — LocalApiKey only for keyed proxies), the routers' `reasoning` field is never
  sent locally (strict servers 400 on unknowns), Normalize's shared NormalizeChatEndpoint gives
  loopback hosts http:// not https://; `LocalContextWindow` (default 16384, clamp 2048..2M) feeds
  MemoryTokenProfile directly — the window is whatever the server LOADS, never a model-table lookup;
  the health check asks for a blank LocalModel by name and diagnoses a dead connection as
  "is your local server running?"; MCM has the Local backend + URL/model/context fields;
  LOCAL TIME RUNS SLOWER (2026.07.17, Anton's Qwen-35B dying at the flat 90s): the OpenAI client's
  shared HttpClient is timeout-infinite and each request carries its own — 90s cloud, 5 MIN local —
  the health check gives a local first-ping 180s (model may still be loading), and the reach-out/
  letter watchdogs breathe 12 min instead of 3 on `config.IsLocalBackend` so a slow reply is never
  mistaken for a lost one; LIVE-TESTED 2026.07.17: Qwen3.5-9B speaks and calls recall_company
  correctly, big Qwen needs the wide timeouts),
  `AtmosphereLine` (the configurable opening identity line, supports `{name}`) + `RoleplayGuidance`
  (world-wide tone/roleplay guidance, offered as freedom), `NotifyWhenReplyReady` (short "has answered"
  ready-notice; default on) + `ShowConversationInMessageLog` (log each full reply; default off — banner can cover the box),
  `EnableRelationshipChanges` + `RelationshipChangesViaTool` (NPC-authored, conversation-driven relation
  shifts; with the tool shape on — default — the NPC weighs her heart via the `move_heart` native tool
  EVERY reply, 0 being a full answer — the always-weigh ritual, Anton's design 2026.07.11 after gpt-4o
  proved too shy to reach for an optional tool; off, or on a backend without tools, a second isolated
  feeling call asks after the reply; both default on) + `ShowHeartHeldNotice` (the grey "heart held
  where it stood" line after zero-shift exchanges, the quiet counterpart of the green/red moved lines
  so every exchange visibly answers; player-visible, default on),
  `EnableNpcInitiatedChats` + `DailyInitiationRate` + `InitiationPullFloor` + `ShowInitiationTestButton`
  (NPCs reaching out to the player on their own; the rate is the expected visits per day **in total across
  everyone** when the bonds are full — it does NOT stack per companion — scaled down by how often you talk
  and how far the standing is from 0, so a fresh game stays quiet; 0.3 ≈ one visit every ~3 days, 1.5 ≈ one
  or two a day; the pull floor (default 0.1) gives EVERY co-located soul — the whole party, everyone in
  town, even someone never spoken with — at least that fraction of a full bond's pull, so strangers can
  approach and begin a story, still capped by the same group total; the test button forces one on demand
  from the free-chat menu — it also needs `DevMode` on now),
  `ShowSocialnessControl` (the SOCIALNESS stepper on the map — `UI\Socialness\` + its prefab — the live
  hand on `DailyInitiationRate`, 0–24 in quarter steps, lower-right above the army/time controls,
  saving config.json as it changes; CLICK the label to unfold the explanation (hover proved shy on a
  no-focus map layer — 2026.07.10 playtest — so hover is only the bonus path); the Core blend in `InitiationScorer.GroupHourlyChance` makes 24 mean "someone near IS
  moved every hour" — the player's openness overriding faint bonds via an s² term that vanishes at
  everyday rates, so old behavior is untouched below ~2/day; default on, and since 2026.07.22 also a
  live MCM checkbox — SocialnessManager reads the flag per tick, so toggling hides/returns the
  stepper at once, the hide promised to Charley Prince on Steam),
  `EnableWorldTidings` + `MaxWorldTidings` + `MaxLocalRumors` (recent world happenings — wars, falls of
  realms, towns changing hands, deaths/weddings/tournaments — and the talk of the town, drawn from the
  game's own `LogEntryHistory` and folded into every NPC's situation; default on, 6 tidings + 3 rumors),
  `EnableWorldRecall` + `MaxRecallsPerReply` (the gift of recall — NPCs fetching live campaign truth
  about people/places/clans/realms/troops/own company mid-reply via native tool calls; default on, 3 rounds),
  `EnableWebSearch` (NPCs searching the internet mid-reply, framed as "all I have read and heard" —
  DuckDuckGo; the immersed question is first sharpened into a real query by a small refining LLM call
  (`RefineSearchQueryAsync` in the behavior, seeing the last incoming words for intent — the fix for
  immersed-but-useless searches, 2026.07.11), falling back to game-name-prepended raw question; the
  in-world optional `beyond` flag still reaches PAST the world's rim when the visitor speaks openly of
  such, answered in their own voice; default on)
  + `ShowNpcActivity` (soft side notices of what an NPC is doing mid-thought — "remembering…",
  "researching…"; default on),
  `EnableLetters` (distant NPCs writing letters that travel with distance, and the player's courier
  menu in settlements; default on) + `MaxLettersInFlight` (at most this many letters ON THE ROAD toward
  the player at once — letters lag the socialness mood by days, so the cap, not the moment's mood,
  protects the later busier self; spontaneous writes only, player-invited replies ride free; default 3)
  + `EnableLetterWindow` + `LetterWindowHotkey` (the letter window — the chat window's twin, hotkey
  default "Y" since 2026.07.15 — was "U", which War Sails binds to the ship manager at sea; the
  ConfigVersion-2 migration moves configs still on the old default, hand-picked keys untouched —
  one of the two windows open at a time: every correspondent listed even when the writer
  has died, the whole correspondence as letter cards parsed from letters.txt by Core `CorrespondenceLog`,
  a courier on the road noted at the end, and a composer with a tall draft mirror, "Seal and send",
  same QueueLetter road and rules as the courier menu; "Write back" on an arrival opens it, popup
  fallback when it cannot; letter beats ALSO render as "✉ by letter" cards inside the chat window's
  thread via `PromptBuilder.IsComposeLetterBeat`/`TryExtractReceivedLetter` — markers that must stay
  word-for-word fragments of the Angel letter templates; BOTH windows carry a "?" info overlay beside
  the X (2026.07.12: what the window is, how it works, what to try — texts in the VMs' `InfoText` with
  hotkey names read live from config; Escape folds the overlay first, a second press closes; Enter-to-
  send suppressed while it is open); both default on),
  `EnableChatWindow` + `ChatWindowHotkey` + `SendInitiationsToChatWindow` (the chat window — see its
  section below: a Gauntlet window over the map, hotkey default "O", listing everyone co-located AND
  every remembered bond who is away (tagged "(here)"/"(away)", the away ones' send grayed with a
  point to a letter); the player writes first with no greeting ceremony; unsent drafts survive
  closing the window; the NPC's relation points show beside their name and move with each exchange;
  and NPC reach-outs land there as waiting spoken messages instead of accept/decline popups; all default on),
  `OpenInitiationsFaceToFace` (default on, takes precedence over `SendInitiationsToChatWindow` for what
  a reach-out notice CLICK does: opens the OLD-STYLE face-to-face conversation showing the greeting the
  NPC already spoke — no accept/decline; X'ing the notice just leaves that recorded greeting unanswered,
  the stamps telling the silence; the chat window is still reachable by hotkey to reply there instead),
  `UseMapNoticeForInitiations` (NPC offers as persistent portrait notices in the right-side map stack
  instead of an immediate popup; default on, falls back to the popup if the notice UI is unavailable;
  the click opens the face-to-face conversation, the chat window, or the accept/decline offer per the
  two toggles above),
  `SeedSelfFromWorldStory` (a never-written self.txt begins with the story the world tells of them —
  a wanderer's tavern tale, a noble's encyclopedia repute — instead of a blank page; default on),
  `EnableActingOut` (2026.07.12: the acting-out grammar — NPCs invited to set a small acted gesture
  between single *asterisks*, apart from their spoken words, as the ONE exception to the plain-speech
  rule (`PromptBuilder.ActingOutGuidance`, right after `PlainSpeechGuidance` because it IS that rule's
  exception); sparing by its own wording — one act, rarely two, always brief — the convention cuts both
  ways (the player's *offered arm* was done, not said), and a gesture weighs what the heart has earned;
  the chat window splits spoken bodies on the strict single-asterisk grammar (Core `EmoteText` — no
  newline/`**`/space-padded spans, so markdown residue and stray math stay literal) and draws gestures
  as soft gray narration between the spoken cards via `ChatWindowVM.AddSpoken`, the header riding the
  first segment so an all-gesture reply still says whose act it was; face-to-face panel shows the
  classic literal *starred* convention; default on),
  `EnableMoodSwings` + `EnableWomensCycle` (the passing weather of the heart — Core `MoodTides`, folded
  into the situation right after the self by `SituationBuilder.BuildMood`: every soul carries a daily
  humor from a 16-phrase palette, and women in their childbearing years (15–50, not with child) also
  keep their body's own monthly season — "the custom of women", the old scriptural phrasing — four
  turnings (custom days 1–5 / rising / crest of 3 days mid-cycle / waning) on a per-woman 26–30-day
  calendar, narrated gently so she can weigh it in her own choices; two days of three the season biases
  the humor pick toward its cluster. ALL of it deterministic — FNV-1a over (StringId, campaign day), no
  state, no persistence — so a reload rerolls no one's weather; both default on),
  `EnableNpcGoals` + `MaxNpcGoals` (personal goals — each NPC carries their own aims, what they strive
  for of their own will, in a `goals.txt` beside `self.txt`; shaped one aim at a time mid-conversation
  via the `tend_goals` native tool — `Tools\GoalTool`, add/drop/revise, tool-capable backends only, used
  sparingly unlike the every-reply heart — and reworked wholesale in reflection (a `GOALS:` section with
  the same replace/none contract as `FACTS:`, works on any backend); folded into the prompt as "What you
  strive for" right after "Who you have become"; default on, cap 6, clamp 1..20),
  `MaxKnownFacts` (how many lasting truths an NPC may carry; default 10, clamp 1..30) +
  `RevertMemoriesWithSaves` (save-scoped memory — each save photographs the whole campaign memory folder
  and loading it restores the photo, so a reload truly un-remembers a bad turn, the way the game already
  rewinds the relation inside the save; the fix for the reload-divergence "memories from the future" bug;
  default on) + `MaxMemorySnapshots` (disk cap on snapshots per campaign, oldest pruned first; default 40) +
  `MaxMemoryWriteTokens` (output budget for the memory-WRITING calls — reflection/compression run on
  their own client so the summary+truths+self never get squeezed by the spoken `MaxTokens` cap;
  default 1500, never below `MaxTokens`),
  `NotifyOnMemoryRefactor` (a soft activity-style notice the moment an NPC's automatic compression
  reworks her deep memory — "…turns over old memories of you, and settles them deeper"; default on),
  `ModelContextWindows` (user-editable model → context-window dict — gpt-4o 128k, gpt-4.1 1M,
  gpt-5.x 400k, gpt-5.6 1M, claude 200k/1M — that the `MaxRecentMemoryPercent` family scales against;
  longest key contained in the model id wins, unknown models fall back to 128k, missing built-ins are
  re-added on load; `MemoryTokenProfile.Resolve` reads it, so a new model is one config line, no redeploy),
  `ModelPrices` + `ShowCostNotices` + `MaxDailyRequests` (the cost ledger, 2026.07.12 — `UsageLedger`:
  both clients report the API-measured tokens of every call; an AsyncLocal interaction scope folds a
  whole flow into ONE soft "✒ Name — message: in → out tokens, calls, ~$" notice (spoiler flows —
  reach-out desire, letter compose/reply — bill quiet to log+totals only, never breaking a sealed
  letter); prices per MTok from `ModelPrices` (longest-key match, editable), daily counter persists in
  usage.json so `MaxDailyRequests` (0 = off) survives restarts — at the cap clients throw plainly and
  autonomous rolls skip; the odds view ends with the session/day summary),
  **reasoning is OFF everywhere, hardcoded** (2026.07.13, Anton's call after Opus NPCs answered "..."
  — silent thinking ate the spoken budget/time: OpenAI clients send `reasoning_effort: "none"`,
  Anthropic sends `thinking: {"type":"disabled"}` EXPLICITLY — sonnet-5 thinks by default when the
  field is omitted — except fable/mythos where explicit disabled is a hard 400 and the field stays
  omitted; the old `OpenAIReasoningEffort` config key is gone and ignored on load, no MCM dial;
  gpt-5.x/o-series still get `max_completion_tokens` instead of `max_tokens` — REQUIRED or gpt-5.6
  400s; OpenAI default model is now
  gpt-5.4-mini for fresh configs (2026.07.12, settled by live play after terra → luna both stumbled on
  access-propagation 401s; the MCM dropdown offers 5.4-mini/luna/terra/sol/5.5/5.4/5.4-nano — NO 5.5
  mini/nano exist; older models live on as config.json hand edits), existing configs deliberately
  unmigrated — see docs/models-and-costs.md),
  `ConfigVersion` (format stamp, 2 — migrations key off it; V2 = the letter hotkey's U→Y move),
  `DevMode` (default **false**, for players: hides the `[Immersive AI • test]` levers and the
  "Reveal the whole of your mind" inspector in the face-to-face menu, and the deep-memory overview
  panel in the chat window; set true when working on the mod — Anton keeps it true).
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
    **First seeded from the story the world tells of them** (2026.07.10): a never-written self
    begins as a wanderer's hand-written tavern tale (first person, from the game's
    `backstory_a..d`/`generic_backstory` strings keyed by character template) or a noble's
    encyclopedia account (`Hero.EncyclopediaText` if hand-authored, else the generated
    `Hero.SetHeroEncyclopediaTextAndLinks` paragraph, framed "So runs my story, as the world
    tells it:") — gathered by `BackstoryBuilder` (Module), shaped by `SelfSeedFormatter` (Core),
    seeded via `LoadOrSeedSelf`. Deleting `self.txt` re-seeds. Toggle: `SeedSelfFromWorldStory`.
  - `goals.txt` — the NPC's OWN personal aims (`NpcGoals`), one per line — what they strive for of
    their own will (win back a lost hall, see a child wed, be free of a lord). General to the NPC (like
    the self), authored by them: one aim at a time mid-conversation via the `tend_goals` tool
    (`GoalTool`), and reworked wholesale in reflection (the `GOALS:` section, replace-all like `FACTS:`).
    Folded into the prompt as "What you strive for". Comment lines `#`/`//` ignored; deleting it clears
    their aims. Toggle: `EnableNpcGoals` (cap `MaxNpcGoals`).
  - `letters.txt` — human-readable log of every letter carried between the player and this NPC,
    both directions, including "(read and let lie unanswered)" notes. Append-only record.
  - future per-NPC files go here too.
- `NPCs\campaign_<id>\_letters.json` — the letters currently ON THE ROAD for that campaign
  (Core `LetterBag`; letters travel real in-game days and must survive save/load). Delivered
  letters leave this file — they live on in NPC memory and `letters.txt`.
- `NPCs\campaign_<id>\_snapshots\<token>\` — save-scoped memory photographs (Module `MemorySnapshotStore`):
  a copy of the whole campaign folder taken at each save, so loading that save rewinds the NPCs' memories
  with it. Tied to the save by a GUID token minted into the save via `SyncData` (`OnSaveOverEvent` writes the
  photo, `OnGameLoaded` restores it); the save name (from `OnSaveOverEvent`) only prunes an overwritten
  slot's old photo. `_index.json` maps save→token. Managed automatically; restore is fail-safe (empty/missing
  photo restores nothing). Toggle `RevertMemoriesWithSaves`, cap `MaxMemorySnapshots`.
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

**Startup health check** (`LlmHealthCheck`, fired once per process from `SubModule.OnGameStart`): a tiny
off-thread "reply OK" ping when a campaign is entered, so a missing key / wrong key / dead connection is
told plainly ("add your key to <config> and restart", "check your internet connection", 401/429/404/5xx
classified) instead of surfacing as mute NPCs mid-conversation. Success shows a soft "connected to
<backend · model>." The remedy for any failure is fix-config-and-restart, which re-runs the check.

Each exchange can also move the NPC's standing with the player. **The heart moves by her own hand now
(2026.07.10, Anton's ask): a `move_heart` native tool** (`Tools\HeartTool`) rides every spoken path
beside the recalls — mid-reply the NPC may shift her regard herself, the resolver applies it at once via
`ApplyRelationShift` and tallies it into the turn's `FeltShift` (`TurnOutcome.FeltShiftApplied` keeps
callers from applying twice), and a calibration lives in the tool description + a "My heart is my own"
whisper (`NpcPersona.CanMoveHeart`). This lets greetings, reach-outs, and letters move the heart, which
the after-the-reply question never covered. **Hybrid since 2026.07.12** (gpt-4o went shy of volunteering
the call again once eleven tools rode along — a whole warm playtest landed 0s): a turn only counts as
weighed when a `move_heart` call actually CAME with a readable number (`HeartTool.Tally.Weighed`; an
honest mid-reply 0 is respected and asks nothing twice) — when the tool never came,
`ExecutePlayerTurnAsync` falls back to the **second, isolated feeling call**
(`PromptBuilder.BuildFeelingQuery`, Angel-voiced, one signed number via `FeelingParser.ParseShift`,
deliberately NOT told where the standing rests), the same path used when the tool shape is off or the
backend cannot carry tools. `ChangeRelationAction` folds shifts into the real game relation
(clamped −100..100, no external judge and no ±cap like ChatAi — the NPC sets it however they truly
feel); the colored message always shows the FELT shift even when the relation is already pinned at ±100
(the impact is the story; the rail just has nowhere left to move — 2026.07.09, Anton's ask,
ChatAi-style). Toggles: `EnableRelationshipChanges` (master), `RelationshipChangesViaTool` (default on).
Why tool-or-separate-call and never in-message marks — **settled twice, don't retry**: both a ♥
tail-mark (early) and a firm `<relation>±N</relation>` tag (tried and reverted the same day,
2026.07.09) failed on gpt-4o — the model narrates the number in prose inside the spoken reply and never
emits the mark, so nothing moves AND the number leaks into her words. Native tool calling is a
different, first-class API channel (the one the recalls ride reliably on both backends) — that is why
`move_heart` is worth the third try where inline marks were not; if a backend proves shy of reaching
for it, `RelationshipChangesViaTool: false` restores the separate question without a redeploy.

**Every visit is a recorded beat** (2026.07.10, Anton's ask): the opening recap greeting is no longer
ephemeral — the Angel narrates the arrival (`PromptBuilder.ArrivalLine`, first-meeting vs "comes to you
again") and her greeting is stored as a real Angel turn, exactly like reaching-out and letter beats, so
her memory shows WHEN the player came to her; the old `_lastGreeting` weaving hack is gone (the history
carries the greeting), and Angel turns replay with their `[place, time]` stamp. With recap disabled no
beat is recorded (nothing is fabricated). **The prompt sheet reads like a mind waking toward the moment**
(same day): identity → kin → self → About Calradia/About you → deep memory of the player (summary +
truths) → the situation LAST — itself ordered setting → who you are → tidings/rumors → "And now X comes
to you" + where the heart stands — so the arrival is the final breath before the live transcript. The
standing line lives only in the situation now (removed from `PersonaBuilder.BuildRole` — never tell her
the same heart twice).

**Even meetings without free chat are remembered** (2026.07.10): when any hero conversation ends
(`CampaignEvents.ConversationEnded`) that never became recorded beats (no free chat — `PrepareChat`
marks it via `_conversationBeatNpcId`; no accepted reach-out — `DeliverApproachAsync` marks it too), a
**silent Angel note** lands in their memory: "You and X met and spoke face to face for the first time —
a stranger no longer, though the words of it are not set down here" (`PromptBuilder.MeetingLine`,
first-meeting vs familiar), stamped `[place, time]`, no LLM call, one per NPC per game day
(`IsMeetingLine` dedupe). Silent beats (empty `NpcLine`) are a Core capability: both backends demand
user/assistant alternation, so `AppendRememberedTurns` folds a silent turn's incoming line into the
NEXT user message (or carries it into the live input), and `MemoryCompressor` renders them without
inventing an answer. So a quest talk or a bargain never ends in "hello, stranger".

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
(`OnHourlyTick`), **every hero co-located** with the player right now (`IsCoLocated` — in the player's
party, or the same settlement AND not behind the keep's closed doors: `IsBehindClosedDoors` (2026.07.12)
asks the game's own `SettlementAccessModel.CanMainHeroEnterLordsHall` (+ `Settlement.BribePaid` vs the
bribe price, vanilla's own paid-bribe rule) for anyone the `LocationComplex` places in "lordshall"/
"prison" — no leave to enter the keep means its souls are out of chat's and reach-outs' range, though a
known one still shows "(away)" in the window pointing to a letter, which DOES find them; fail-open so a
model hiccup never silences a keep; distant NPCs write letters instead) joins ONE bond-scaled group roll to
reach out — including people never spoken with: everyone carries at least `InitiationPullFloor` (default
0.1) of a full bond's pull, so a stranger may cross the room and begin their story (the Angel's desire
question tells them honestly it would be a first acquaintance — `ReachOutDesireLine(stranger: true)` —
and their first beat creates their memory). A real history raises the pull from there: each NPC's *pull*
in [0,1] is `InitiationScorer.Pull` = `frequency × closeness × recency`: `frequency`
saturates at `FrequencyFullAt` lifetime turns (`NpcMemory.StoryRichness` = lifetime `TotalTurns`, floored
at surviving turns for old saves), `closeness` = a small floor
(`InitiationScorer.ClosenessFloor`) plus |relation|/100 (love *or* enmity pulls hardest; a neutral bond
you actually spend time with stays quiet, not silent — the floor keeps the feature observable),
`recency` decays with days since the last talk. The pulls combine as `InitiationScorer.UnionPull`
(= 1 − Π(1 − pull), the chance at least one soul is moved) and the hour rolls once at
`InitiationScorer.GroupHourlyChance` = `DailyInitiationRate × unionPull ÷ 24`, so the **day's expected
total across everyone is ≈ rate × unionPull ≤ rate** — five devoted companions share the visits instead of
each bringing their own (the old per-NPC independent rolls summed: rate 0.777 with five close bonds gave
~3.9/day; settled 2026.07.10). Who comes is `InitiationPlanner.PickWeightedIndex` by pull. So a fresh game
stays quiet and `DailyInitiationRate` is the day's total for a full bond. Firing only happens at *safe* moments
(`IsSafeToInitiate`/`InitiationBlockReason`: on the map, not in a scene/battle or a *non-settlement*
encounter, not already talking — being **inside a settlement is fine**, that's where co-located NPCs are).
**The world sleeps at night** (2026.07.11, Anton's ask): the group hourly chance is multiplied by
`InitiationScorer.NightFactor(CampaignTime.Now.CurrentHourInDay)` — undamped through the day
(`DawnHour` 06:00–`DuskHour` 22:00), then divided by a factor rising on a raised-cosine trough from 1 at
dusk/dawn to `DeepestNightDivisor` (8) at the night's middle (~02:00), passing /2 in the shallow night —
so no one crosses a dark camp at three in the morning. Continuous at the day's edges (no cliff at 22:00).
FACE-TO-FACE only: letters are unaffected, since a distant hand's writing hour is never seen, only the
arrival days later. A stuck-in-flight watchdog (`_initiationInFlightSince`, 3 min) self-heals a lost offer
so one mishap can't silence the feature.

When one is moved, the reaching-out plays out as **real Angel↔NPC turns recorded in her memory** — nothing
hidden from her or from the player on inspect. The Angel is a first-class speaker in the same history stream:
`ConversationTurn.Speaker` marks a turn as the Angel's (`ConversationTurn.AngelSpeaker`), and
`PromptBuilder.AppendRememberedTurns` replays those framed in the Angel's voice (`AngelFrame`), so she
re-reads her own past truthfully. The beats: (1) `PromptBuilder.BuildAngelPrompt` with
`PromptBuilder.ReachOutDesireLine` asks — privately, yes/no (`InitiationParser.WantsToReachOut`) — whether
she wishes to go to the player; her answer is recorded via `AppendAngelTurn`. (2) On yes, the player gets a
faced portrait toast and — with `UseMapNoticeForInitiations` on (default) — a **persistent, non-pausing
right-side map notice wearing her live portrait** (see the Harmony section below); clicking it opens the
accept/decline inquiry (which pauses per `PauseOnInitiationOffer`). The notice waits up to 2 in-game days,
then quietly lapses (she is not told of a door the player never reached); several NPCs can be knocking at
once. Without the notice UI the inquiry shows directly, as before. (3) The approach is narrated *after* the choice (`DeliverApproachAsync` with
`PromptBuilder.ApproachLine`): **Receive them** → the Angel narrates a glad welcome and she speaks her
greeting (a recorded Angel turn — no weaving needed, so she never repeats it), the conversation opens
(`CampaignMapConversation.OpenConversation`) and falls into the talk loop; **Not now** → the Angel narrates
that the player is too busy just now and she answers in her own voice (recorded, shown back with her face) —
a lived moment, not a cold "you were refused". Two LLM calls per fired offer; she can always choose silence.
`MemoryCompressor` renders Angel turns attributed to the voice (not "They") so summaries stay truthful.
Toggle with `EnableNpcInitiatedChats`. Nothing about the schedule is persisted (stateless hourly rolls), so
save/load is a non-issue. Two `[Immersive AI • test]` free-chat options
(gated by `DevMode` + `ShowInitiationTestButton`): `OnDebugForceReachOut` forces the NPC just spoken with to reach out
right after parting; `OnShowInitiationOdds` dumps, for every history NPC, whether they are co-located now
and their computed daily/hourly chance — the go-to answer for "why is it quiet?" (usually: no one
co-located, or near-neutral standings).

**Harmony & the portrait map notice.** Harmony (0Harmony 2.4.2, MIT) is bundled in `lib\` and ships in the
module's bin — Anton green-lit it on 2026.07.09; use it sparingly, one intentional patch at a time. The one
patch so far (`UI\MapNoticePatch`, applied in `SubModule.OnSubModuleLoad`) is a ctor postfix on
`MapNotificationVM` that calls the game's own public `RegisterMapNotificationType` to register
`ImmersiveChatMapNotification` (an `InformationData`) → `ImmersiveChatNotificationItemVM` (carries a
`CharacterImageIdentifierVM` portrait over a "quest" fallback icon) — and, since 2026.07.22, its
letter twin `ImmersiveLetterMapNotification` → `ImmersiveLetterNotificationItemVM` (saveable id 2). The portrait is drawn by a marked
block in our override of `MapNotificationItem.xml` (`module\GUI\Prefabs\Map\` — same-name prefabs shadow
SandBox's; vanilla items bind nothing there and are unaffected; re-copy + re-mark after game patches).
**Save safety:** `InformationData` lives inside saves while a notice is up, so `ImmersiveAISaveDefiner`
(base id 726401000) must keep the class registered — never remove or renumber without migrating (the risk
is at save-WRITE time; loading a notice-carrying save with the mod fully REMOVED is verified safe,
2026.07.12 — the engine null-scrubs unknown saved types on load). Everything
degrades gracefully: patch fails → `Applied` false → direct-inquiry fallback. Parked offers live in
`_pendingNotices` (not persisted; a reload lets the moment pass via `IsValid`). Config:
`UseMapNoticeForInitiations`.

**The chat window — quick words, no ceremony (Milestone 2's first stone, 2026.07.10).** A custom
Gauntlet window over the map screen: hotkey (`ChatWindowHotkey`, default "O", parsed to `InputKey`),
a "Speak with those near you" option in every town/castle/village menu, or an NPC's knock. Works
anywhere the map is on stage — travelling, at sea, inside settlement menus — never in missions
(`ChatWindowManager.CanOpenNow`: MapState, no conversation, no inquiry up, and no encyclopedia —
that overlay never changes the GameState, so typing "o"/"y" in its SEARCH BOX would open the windows;
`UI\MapOverlays.IsEncyclopediaOpen` reads MapScreen's flag by cached soft reflection — resolved by
scanning loaded assemblies, NOT `Type.GetType("…, SandBox.View")`, which answers null for module-folder
DLLs and silently disarmed the guard once; beside it `MapOverlays.IsTypingSomewhere`
(`ScreenManager.FocusedLayer.IsFocusedOnInput()` — the engine's own any-text-field-focused signal)
blocks the hotkeys whenever ANY overlay's text box holds the keys; both windows' gates check both —
2026.07.12). Left side lists everyone
co-located (same `IsCoLocated` as reach-outs; friends first by last-spoken, portraits via the shared
`UI\Portraits.DarkCode`; a **search line above the list** (2026.07.12, both windows) refilters by
name/detail as you type — the full list lives in the VM's `_allContacts`, `Contacts` is the searched
view, and a knock/"Write back" clears a stale filter); the right side shows a grey **bond-stats line**
under the chosen name (`ImmersiveChatBehavior.BondStatsLabel` — richness, days since last spoke, and
the odds view's per-soul hourly reach-out/letter chance, night factor included; both windows show it)
plus the chosen one's **deep-memory overview up top**
(summary + held truths, collapsible — so a long story needs no scrolling) and the **recorded turns as
a thread** (Angel beats rendered as soft gray narration — nothing she remembers is hidden), with an
input line below. The player **writes first, with no arrival beat and no forced greeting** — the line
goes straight through `ExecutePlayerTurnAsync`, the shared trunk factored out of `RespondAsync`
(prompt → spoken reply with recall/wisdom riding along → the private feeling number → recorded turn →
compression → save), so window and conversation panel are the same machinery with different rendering.
One in-flight exchange per NPC (`_quickChatBusy`); a failed send puts the words back in the input box.
**Reach-outs become messages** (`SendInitiationsToChatWindow`, default on): after her recorded yes to
the desire question, there is NO accept/decline — `DeliverFirstWordAsync` has her simply speak
(`PromptBuilder.FirstWordLine`, stranger-aware, honest that the player may answer only later), records
it as a real Angel turn, fires a faced toast ("Ava sees you and says: …"), marks the thread unread,
and (window closed, notice UI available) parks a portrait map notice whose click now opens the window
on her thread. If the player never replies, nothing is faked: the `[place, time]` stamps on the
recorded turns already let her see the silence and its length — that falls out of the recorded-beats
architecture for free. The window is a VIEW over the memory stream: closing it loses nothing; replies
landing while it is closed toast "has answered" and wait as unread dots (session-scoped, deliberately
unpersisted — the words themselves are in `memories.json`). Layer plumbing: `GauntletLayer("name",
order)` ctor (this game version puts the name FIRST), `LoadMovie("ImmersiveChatWindow", vm)`, prefab
in `module\GUI\Prefabs\` using only Native/SandBox brushes+sprites, ticked from
`SubModule.OnApplicationTick` (hotkey when closed; Enter-to-send/Escape-to-close and scroll-to-bottom
via `ScrollablePanel.VerticalScrollbar.ValueFloat` when open). Everything degrades gracefully: a
prefab/layer failure toasts and closes; with `EnableChatWindow` off (or `SendInitiationsToChatWindow`
off) the old offer flow stands untouched. Config: `EnableChatWindow`, `ChatWindowHotkey`,
`SendInitiationsToChatWindow`.

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

**The gift of recall (NPC tool-use).** Mid-reply, an NPC can reach into the world's memory instead of
hallucinating: seven native tools (`Tools\WorldRecall` — `recall_person`, `recall_place`, `recall_clan`,
`recall_realm`, `recall_company`, `recall_troop`, `recall_market`) look up live campaign truth — kin and
house, whereabouts (phrased as hearsay, "last word places them at…"), who holds a town, clan renown, which
realms are at war — and hand it back as gentle second-person remembrance. `recall_market` (2026.07.10,
from Cunbert quoting an invented grain price) reads the real ledger where the asker stands —
`Town`/`Village.GetItemPrice` — one named good (buy + sell-back) or a staples survey from
`Items.AllTradeGoods`; its `item` parameter is optional (`ToolParameter(required: false)`).
Name-twins are resolved by closeness (`ClosenessTo`: kin > same party > same settlement > the player >
same clan) so a wife asked of "Vulgrim" recalls HER Vulgrim, not a stranger across the map (playtest find,
2026.07.10); a troop-name miss suggests the named people's real kinds ("Battanian recruit" → Battania
musters Volunteers). `recall_troop` (2026.07.10) weighs kinds of soldier
(tier as "rank of seasoning", manner of fighting, skills from `Skills.All`×`GetSkillValue`, gear from
`FirstBattleEquipment`, `UpgradeTargets` as "with seasoning they may become…"; filtered to
Soldier/Mercenary/Bandit occupations so "recruit" never matches a villager). Beside them rides
**`seek_wisdom` (`Tools\WebWisdom`, 2026.07.10; reframed 2026.07.11) — "don't ask Google; ask one of your
companions":** a real web search (DuckDuckGo HTML endpoint, no key, regex-parsed titles+snippets, 12s
timeout) framed to the NPC as searching "all I have ever read and heard" (the sages retired — Anton found
them too much); the query is first sharpened by a small refining LLM call (`RefineSearchQueryAsync` in the
behavior — plain-call, sees the last incoming words, returns one "Mount and Blade Bannerlord …" query;
failure falls back to game-name-prepended raw question) and the result closes by telling her to speak the
substance in her own world's words and let no meta terms pass her lips — that closing framing is the whole
fourth-wall defense, keep it. It runs off-thread (no game state) and shares the recall round budget.
Config: `EnableWebSearch`. Beside the aims' hand rides **`hold_truth` (`Tools\TruthTool`, 2026.07.11) — the
truths' hand:** mid-reply the NPC may set down (or release) ONE lasting truth into her `KnownFacts`
(Core `NpcMemory.AddKnownFact`/`DropKnownFact`, dedupe + `MaxKnownFacts` cap honored, honest "mind is full"
answer at the cap); applied to the LIVE memory instance the turn speaks from when one rides along
(`CompleteSpokenAsync`'s `liveMemory`) so the end-of-turn save can never clobber it, saved at once either
way, with a soft "sets down a truth to keep…" activity notice like the goals' hand. No config — rides
whenever the backend can carry tools; reflection still rewrites the whole list. Every tool call also fires
a soft **activity notice** ("X is remembering… (name)", "X takes stock of the company…", "X is researching…
(question)", "X sets an aim in order…", "X sets down a truth to keep…") via `NotifyActivity`/the resolvers
in the behavior — marshaled to the game thread, `ShowNpcActivity`. `recall_company` (2026.07.10, "Yngvald doesn't know his own
men") is the inward one — no name argument: the asker's OWN warband, known exactly (a captain reads his
muster roll): head-count, hale/wounded, companions by name, ranks by troop kind, prisoners in the train,
food-days from `Food`/`FoodChange`, morale in words + number, wages + own purse (leader only), what the
company is about (`DefaultBehavior`/`MapEvent`/`BesiegedSettlement` → gentle errand phrases), and the army
it marches in. `recall_person` also adds what the eyes see — garb and arms from real equipment (civilian
kit within walls, battle kit on the road) — when the person truly stands with the asker (same settlement
or party); that's ChatAi's equipment info made on-demand instead of crammed into every prompt. The
always-on situational whispers went to `SituationBuilder` instead (mined from ChatAi's WorldPromptHints,
2026.07.10): own-command line (party size even when berthed in a town — details via the tool), army
membership, under-siege/besieging/raiding, pregnancy, and a renown-tiered line about how far the
partner's name has traveled. **NPCs also know their own troubles** (2026.07.10, the Turvald playtest
find — a quest giver blank on his own quest): `TroubleBuilder` (Module, rides `SituationBuilder.Build`
right after the self paragraph) reads `Campaign.Current.IssueManager.Issues[npc]` — on this game
version `Title`/`IssueBriefByIssueGiver`/`IssueQuestSolutionExplanationByIssueGiver`/`IssueQuest` and
the `IsSolvingWith*` state flags are all PUBLIC (ChatAi needed reflection; we don't) — and narrates the
issue in the giver's own first-person words ("this is how you tell it: …"), where its resolving stands
(untaken / taken by the player, with the quest journal's last word + days remaining / companions sent /
laid in a lord's hands), plus up to two non-issue quests they gave (`QuestManager.Quests` by
`QuestGiver`). Always on, best-effort per sentence, no config. The loop is Core's `ToolLoopRunner` (complete → resolve → repeat, unit-tested): the final
round keeps sending the definitions but sets `tool_choice: none`, so the turn always ends in words; a
failed lookup returns an honest "Nothing surfaces…" so the model owns not knowing instead of inventing.
Both clients implement `IToolChatClient` (Anthropic `tool_use` blocks / OpenAI function calls — this is
NOT the in-message-mark problem: native tool calling is a first-class API channel on both backends, which
is exactly why it's reliable where inline text marks were not). Resolution runs on the game thread
(dispatcher + TCS, 15s timeout). Every spoken path goes through `CompleteSpokenAsync` — replies, recaps,
approach beats, letter composition; short utility calls (feeling number, yes/no desires) stay plain. The
NPC gets one whisper line about the gift only when the tools truly ride along (`NpcPersona.CanRecallWorld`).
Config: `EnableWorldRecall`, `MaxRecallsPerReply`.

**The company and the crafts (2026.07.12 — the roles-immersion wave).** Every soul now knows what they
are honestly good at: `CraftsBuilder` (Module) weighs real skills into craft-words ("What my hands and
wits are honestly good at: masterly in Medicine; able in Scouting…") on every sheet (`NpcPersona.Crafts`),
so wanderers answer "what would you be good at?" from truth; `recall_person` lists a looked-up soul's
strongest crafts too. Party duties run deep: the situation gives each duty-holder their charge in their
own words with their skill weighed in ("As its scout… my eyes are able at the craft"), a leader knows who
holds his duties, caravans speak of rounds and ledgers (not "warbands"), and the chat window tags your
own party "rides with you — your scout". Beside the recalls ride the **field-craft tools**
(`Tools\FieldCraft`, only when `npc.PartyBelongedTo != null` — a lean list keeps tools used; whisper flag
`NpcPersona.CanSurveyField`): `survey_surroundings` (bands within `SeeingRange`×1.5 with kind/faction/
strength/foe-or-friend/distance-in-rider's-words + who is swifter — the true "can we escape them?" — and
our own pace with the real `SpeedExplained` drag lines; counts coarsened below Scouting 125/50) and
`weigh_battle` (company-or-army vs a named band/army/walled place, garrison + militia at half weight,
compositions from real rosters, verdict by true `EstimatedStrength` ratio, confidence by Tactics —
NOTE: this game version has NO `PartyBase.TotalStrength`, use `EstimatedStrength`; `ExplainedNumber`
explanations come from the parameterless `GetLines()`; `DefaultSkills` lives in `TaleWorlds.Core`).
`recall_company` gained the surgeon's ledger (healing rates for named and ranks via the game's
`PartyHealingModel`). Stations carry one trade-knowledge sentence each (`PersonaBuilder.TradeKnowledge`:
artisan, tavern-keeper, ransom broker, smith, arena master, headman, merchant/caravan master). Family
deepened: children named WITH their other parent (polygamy-safe), grown children carry where life took
them, and a spouse's arrival states plainly that between wedded souls there is no ceremony. **The
beholder's eye**: a great lord (2+ clan tiers above, or crowned 1+) meeting a near-stranger (standing
< 10) gets ONE smashed-down sentence of what his eyes see — garb/blade by real item tiers, banner,
following, "no word of their deeds" when renown < 150 — and the welcome is left to his own nature;
`StrangerStationFactor` also shrinks the reach-out stranger floor for such lords (king → 0.2×), never
touching real bonds.

**Letters — the bond crosses the map.** The mirror of reaching-out for everyone `IsCoLocated` skips:
each hour, distant NPCs with history roll `LetterCourier.WriteRateFactor` (0.5) × their reaching-out
chance; one moved soul is asked by the Angel — privately, yes/no, recorded — whether they wish to write,
and on a yes composes the letter with their full self (persona, memory, the situation built *apart*
via `SituationBuilder.Build(..., apart: true)`, and the gift of recall). **The player's own clan writes
out of duty** (2026.07.12): `InitiationScorer.Pull(..., inPlayersService)` floors recency (0.6) and
closeness (0.5) for one's own companions/kin/governors — a caravan forty days on the road still writes
home — and their compose line invites a field report of their charge (`ComposeLetterLine(inService)`,
appended AFTER the marker fragment so recorded beats stay recognized). **A letter is readable only when
it arrives** (2026.07.12): `Letter.Logged` defers the letters.txt entry to delivery (default true so old
bags never double-log; dead writers' folders resolved by identity), and the chat window seals an
in-flight compose beat ("it is sealed, and rides toward you still" — `IsLetterOnRoadToPlayer`). The letter rides real in-game
days by map distance (Core `LetterCourier`: 150 units/day, 0.25–10 day rails) and persists across
save/load in `campaign_<id>\_letters.json` (Core `LetterBag`, atomic writes) — a letter is a promise,
unlike a live chat. **Arrival knocks like a chat now** (2026.07.22, Anton's ask): faced toast + a
persistent portrait map notice ("A letter has come", `ImmersiveLetterMapNotification` — saveable type
id 2 in the definer, keep registered forever — + `ImmersiveLetterNotificationItemVM`), whose click
opens the LETTER WINDOW on the writer's thread (`OpenWhenClear`, composer popups as fallback); the
letter is logged to letters.txt BEFORE the notice goes up, so X ("set it aside") or a reload loses
nothing — the words wait in the window. The old pausing inquiry ("Write back"/"Set it aside") remains
only for dead writers or when the notice UI / letter window is unavailable. The
player can also send first: a "Send a letter by courier" option in every town/castle/village menu opens
the LETTER WINDOW itself (2026.07.12 — the same one the letter hotkey opens; the old recipient-picker popups
remain only as the fallback when `EnableLetterWindow` is off or the window cannot come up; one courier
per bond at a time, co-located people pointed to go and speak). When the player's letter reaches the NPC, *reading it is a recorded moment* (the body
lives inside the Angel's line, so it enters memory even if they let it lie), and they may answer at most
once per letter — correspondence is a chain of choices, not an echo. Undeliverable (recipient dead) comes
back as a quiet notice. All beats are Angel turns in `memories.json`; each NPC folder keeps a plain
`letters.txt` of the whole correspondence. One letter LLM job at a time (3-min self-heal watchdog), at
most one delivery per direction per hour. Test lever: "[test — trigger them to write you a letter]"
(co-located → lands in ~6 game-hours). The odds view shows distant NPCs' letter chance. Config:
`EnableLetters`.

**The letter window (2026.07.11)** is the chat window's twin for correspondence (`UI\LetterWindow\`,
prefab `ImmersiveLetterWindow.xml`, hotkey `LetterWindowHotkey` default "Y"; the two managers yield to
each other so one window is up at a time; it carries the same search line and bond-stats line as the
chat window — 2026.07.12). It is a pure VIEW: correspondents enumerated from the
campaign's NPC folders (`CorrespondentsForLetters` — anyone with a letters.txt, even dead writers, plus
everyone with real history), the correspondence parsed from letters.txt by Core `CorrespondenceLog.Parse`
(letter cards with writer/stamp/provenance; asides as narration), the courier's road from the live
`LetterBag` (`CourierStatusFor`), and writing routed through the same `QueueLetter` as the courier menu
(`SendLetterFromWindow` + `CanWriteTo` — one courier per bond, co-located souls pointed to speak, the
dead cannot answer). Enter deliberately does NOT send here (a letter deserves a deliberate seal); the
composer's tall draft mirror is the "letter-writing screen" the encyclopedia task wanted — its remaining
half is only the encyclopedia button. "Write back" on an arrival opens the window preselected
(`OpenWriteBack`, next-tick via the dispatcher so the inquiry is gone; popup-composer fallback). In the
CHAT window's thread, letter beats now wear their letters openly: `PromptBuilder.IsComposeLetterBeat` and
`TryExtractReceivedLetter` (Core, unit-tested) recognize the recorded Angel letter turns and render them
as "✉ by letter" cards between the spoken messages — those markers must remain word-for-word fragments
of the shipped Angel letter templates (recorded memories carry the old phrasing forever), so change a
template and its marker together, never one.

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
