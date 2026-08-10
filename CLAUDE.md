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
   multi-turn conversation, one deep rolling memory of everything older that they rewrite whole
   when they gather their thoughts, and a distinct speech style.
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
- **"Think" (the player's own next line)** → Core `Prompts\PlayerThought` (the aside + the answer-taming) + `Prompts\ConversationPresets` (the presets file model) + the `ImmersiveChatBehavior.Thoughts.cs` partial + both windows' VMs/prefabs.
- **Per-NPC files, paths, migration** → `NpcPaths` (Module).
- **What each NPC carries** → `NpcMemory` (per-person memory of the player) + `NpcSelf` (`self.txt`, their general self). NOTE two subsystems were RETIRED 2026.08.08 — the distilled `KnownFacts` (the `hold_truth` tool + the reflection `FACTS:` section) and `NpcGoals`/`goals.txt` (the `tend_goals` tool + the `GOALS:` section): both cramped what the rolling memory already held and read it back to her twice, and each cost a tool slot in every reply. Do not reintroduce either; the deep memory carries it all now.
- **NPC tool-use ("the gift of recall")** → `WorldRecall` (Module, the seven recall tools: person/place/clan/realm/troop/market lookups + `recall_company`, one's own warband — now with the surgeon's healing rates and, on `recall_person`, the looked-up soul's strongest crafts) + `FieldCraft` (Module, 2026.07.12: `survey_surroundings` + `weigh_battle`, the outward eyes and the scales of battle — ride ONLY for souls with a party on the map, counts coarsened by the asker's Scouting/Tactics; 2026.07.22: both also see the SPOTTED hideouts — the survey lists nearby dens named by their brigands' clan with lurker counts, and the scales weigh a den's lurking parties, "hideout"/"den"/"lair" resolving to the nearest spotted one) + `WebWisdom` (Module, `seek_wisdom` — web search framed as "all I have read and heard", queries sharpened by a small refining LLM call) + `ToolLoopRunner` (Core, the loop) + the two chat clients (native tool calling).
- **Letters** → `LetterBag` / `LetterCourier` / `CorrespondenceLog` (Core: queue + travel math + letters.txt parser) + `ImmersiveChatBehavior.Letters.cs` (Module, all flows + the window's view accessors) + `UI\LetterWindow\` (the letter window).
- **The birth chronicle** → Core `Births\` (`BirthRecord`/`BirthLedger` JSON-per-birth + `births.txt`,
  `BirthTiers` — the wedding's own ladder at a third of its renown, `BirthText` — the two prompts, the
  permanent marks, the accounts; unit-tested) + the `ImmersiveChatBehavior.Births.cs` partial (hooks
  `OnGivenBirthEvent`, captures BEFORE vanilla's death-in-labour roll, the hour written at once and the
  feast whenever it is bought) + `Tools\CradleTool` (`recall_birth`; the hour is refused to anyone but
  the two parents). Witness gathering is SHARED with the wedding — Core `Celebrations\GuestRules` +
  `ImmersiveChatBehavior.Celebrations.cs`.
- **The wedding chronicle** → Core `Weddings\` (`WeddingRecord`/`WeddingLedger` JSON-per-wedding + `weddings.txt`, `WeddingText` — the two chronicler prompts, the permanent beat marks, the accounts; unit-tested) + the `ImmersiveChatBehavior.Weddings.cs` partial (hooks `BeforeHeroesMarried`, captures the day BEFORE the clan change scatters it, two story calls off-thread, beats to the spouse + every witness) + `Tools\NuptialTool` (`recall_wedding`; the night is refused to anyone but the spouse).
- **The battle chronicle** → Core `Battles\` (`BattleRecord` data, `BattleLedger` JSON-per-battle + loose find-by-name, `BattleText` — titles/tales/beats/accounts, all unit-tested) + the `ImmersiveChatBehavior.Battles.cs` partial (capture at `OnPlayerBattleEnd` BEFORE the game commits gains, enrich one dispatcher tick later, per-hero downs via `OnHeroCombatHitEvent`) + `Tools\ChronicleTool` (`recall_battle`).
- **The road journal** → Core `Journey\` (`JourneyLog` visits/quests + pruning + JSON, `JourneyText` — the witness prose, unit-tested) + the `ImmersiveChatBehavior.Journey.cs` partial (nine campaign-event hooks: stops, trade, recruits, garrison drops, captives, quests) — the situation block only for souls riding IN the player's party.
- **The nights of a marriage & THE LINE** → Core `Nights\` + `Together\TogetherLine` (the line since we were last alone) (`NightRecord`/`NightLedger` `_nights.json` + `nights.txt`, `NightGifts` the 0/10/100/300/1000 tiers, `NightOdds` the fertility-spread arithmetic, `NightText` — the short Song-of-Songs prompt, the permanent beat marks, the roll; unit-tested) + `MoodTides.Fertility` + the `ImmersiveChatBehavior.Nights.cs` partial + `Nights\PregnancyPatch` (the SECOND Harmony touch) + `UI\NightWindow\` (hotkey H). Decision record docs/nights-and-conception-design.md.
- **Courtship & marriage** → Core `Courtship\` (CourtshipRoad rails + stages, CourtshipMisgiving + CourtshipMisgivings ops — HER OWN written doubts, the checkable-ask DSL/MatchmakerLedger retired 2026.08.08, CourtshipSeed, CourtshipText — every word she reads, numberless refusals) + Module `Tools\TrothTool` (tend_courtship + bless_marriage) + `Tools\MisgivingTool` (weigh_misgivings) + the `ImmersiveChatBehavior.Courtship.cs` partial (gates, seals, seeding, blessing, Marry Anyone compat, letter-borne offers) + docs/marriage-courtship-design.md.

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
  Battles/                BattleRecord (+side stats/participants/loot summary), BattleLedger (JSON
                          per battle + loose find-by-name), BattleText (titles/tales/beats/accounts)
  Births/                 BirthRecord (the day's facts, the children, the two written accounts),
                          BirthLedger (JSON per birth + births.txt, loose find, what still owes us a
                          part), BirthTiers (the wedding's ladder, a third of its renown), BirthText
                          (the two prompts — Scripture's own birth narratives for the hour, the
                          wedding day's register for the feast — the permanent beat marks, the accounts)
  Celebrations/           GuestRules — who is called to a day of the player's, shared by the wedding
                          and the birth (and the one flag that deliberately does not climb the ladder)
  Weddings/               WeddingRecord (the day's facts + the two written accounts), WeddingLedger
                          (JSON per wedding + weddings.txt, loose find), WeddingText (the chronicler's
                          two prompts — Scripture for the day, the Song of Songs for the night — the
                          permanent beat marks, the recall accounts, the answer-taming)
  Journey/                JourneyLog (visits + quests, pruning, JSON) and JourneyText (the road
                          journal in words) — the witness log of the player's everyday life
  Memory/                 NpcMemory (2-layer per-person: verbatim turns + the rolling memory),
                          NpcSelf (general self-concept), ConversationTurn, JsonMemoryStore,
                          MemoryCompressor (the SUMMARY: contract + the reflection's SELF:)
  Prompts/                PromptBuilder (multi-turn message assembly + first-person beat/letter lines
                          + legacy Angel replay), NpcPersona, PlayerThought + ConversationPresets (the
                          player's OWN next line: the closing aside, the answer-taming, the intents)
src/ImmersiveAI.Module/   net472 — the Bannerlord module; references game DLLs
  SubModule.cs            entry point: registers behavior, drains dispatcher each tick
  ImmersiveChatBehavior.cs  the campaign behavior: dialog + conversation turn orchestration
  ImmersiveChatBehavior.Births.cs   partial: the birth chronicle (the hook, the hour, the feast and
                          its deferred offer, the beats, the keepsake, the retries)
  ImmersiveChatBehavior.Celebrations.cs  partial: who stands at a day of the player's — the ONE
                          gathering shared by the wedding and the birth
  ImmersiveChatBehavior.Letters.cs  partial: every letter flow (NPC writes, player writes, arrivals)
  ImmersiveChatBehavior.Thoughts.cs partial: "Think" (Shift+Enter) — the PLAYER's own next line, thought
                          out on the NPC's own sheet (plain call, nothing recorded) + the intents file
  Llm/                    AnthropicChatClient, OpenAIChatClient (raw HttpClient, native tool use), factory
  Tools/WorldRecall.cs    the gift of recall: person/place/clan/realm lookups from live campaign data
  Tools/FieldCraft.cs     the field-craft (2026.07.12): survey_surroundings + weigh_battle — the country
                          about and the scales of battle, only for souls with a party on the map
  Tools/HeartTool.cs      the heart's own hand (move_heart), weighed every reply
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
                          + 2026.08.07 SellswordTerms (optional ModConfig param): an UNHIRED wanderer's
                          sheet opens with her worth, her FIXED day-wage, her private haggling bounds
                          (live percent) and the seller's mind — open high, concede only what the talk
                          earned, never volunteer the floor
  Personas/ThoughtFacts.cs   the few PLAIN facts the player's own thinking gets — them in the third
                          person, me in the first (never the persona sheet: its first person wins)
  Personas/CraftsBuilder.cs  real skills weighed into honest craft-words ("masterly in Medicine") —
                          the sheet line, the duty sentences, and recall_person all draw on it
  Personas/SituationBuilder.cs  builds the first-person "current situation" narration (+ mood, + the
                          moment, + party-duty depth, + the beholder's eye on unknown callers,
                          + 2026.08.07: one's OWN war-kit by real item name — "and nothing besides",
                          the anti-phantom-bow clause — and a wanderer's true hiring price + daily
                          wage from the game's own models, so sellswords quote real terms)
  PromptFiles.cs          loads user-editable global/per-NPC prompt files
  ModConfig.cs            JSON config (API keys, model, token/memory limits) — the single source of truth
  Mcm/                    ImmersiveAiMcmSettings + McmBridge + McmChoiceLists: the in-game MCM settings menu
                          (SOFT dependency — absent MCM = config.json only; present = a live two-way editor over
                          a subset, config.json still master). Hardened 2026.07.26 (2nd Nexus report — menus can
                          RENDER via MCM.UI's pipeline while MCM's own container never registers our instance, so
                          the bind silently never lands and edits strand in MCM's store): (1) TryRescueOnce reads
                          MCM's store file (Configs\ModSettings\Global\ImmersiveAI\ImmersiveAI_v1.json, dropdowns
                          as INDICES into McmChoiceLists — append-only, never reorder) and adopts stranded values
                          under never-clobber rules (keys fill only empty, models/endpoints only over defaults,
                          Backend only when the current one has no key and the store's can speak); (2) SyncTick
                          polls menu↔config snapshots ~1s after bind so sync never depends on MCM's SAVE_TRIGGERED
                          event; (3) bind success/failure logs + a one-time "menu could not connect" notice
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
CHANGELOG.md              the PLAYER-FACING running list: every player-visible change lands under
                          [Unreleased] the day it ships, as a ONE-LINE PILL — never a paragraph
                          (Anton, 2026.08.08: "на хубави хапчици а не голям чаршаф"). At release the
                          section feeds THREE tiers, all written at once: (1) NEXUS — a 255-CHARACTER
                          HARD CAP, so each version carries a fenced ~6-bullet block at the top of its
                          section, copied verbatim; (2) STEAM — tools\WorkshopUpdate.xml ChangeNotes,
                          room for group headlines + pills; (3) this file, the full grouped record.
                          Dev history stays in TASKS_DONE.md
tools/deploy.ps1          build + install into the game as Modules\ImmersiveAI.Dev — "Immersive AI (dev)",
                          its own Id so it coexists with the Workshop copy (enable only ONE); keep the
                          script ASCII-only (BOM-less .ps1 + em-dash bytes = smart quote = PS 5.1 parse error)
tools/package.ps1         clean dist\ImmersiveAI layout + version-stamped zip for the Workshop upload
docs/release-dance.md     THE RELEASE RUNBOOK — read this before shipping anything. Who does what
                          (Claude: version, notes, pages, package, the Steam uploader; Anton: the two
                          store descriptions + the Nexus file), the three change-note tiers written at
                          once, the byte budgets, and every trap learned the hard way — including that
                          a version PREPARED is not a version SHIPPED (v2.0.0 and v2.1.0 both sat
                          packaged and unuploaded while the Workshop item stayed at v1.4.1)
docs/steam-page-draft.md  SUPERSEDED draft; the LIVE pages are the .bbcode.txt files beside it —
                          steam-page-final.bbcode.txt, steam-faq.bbcode.txt (pinned), nexus-page.bbcode.txt.
                          All three are AT their length limit: any addition must be paid for by a cut
                          elsewhere in the same file (deep material goes to docs/ and is LINKED instead).
                          MEASURE IN BYTES, NOT CHARACTERS (learned 2026.08.08): the Steam description
                          caps at 8000 UTF-8 BYTES and fails with a bare "There was a problem trying to
                          save the title and description" — an em dash is 3 bytes and the page carries
                          ~42, so len(s) understates by ~90. Use len(s.encode('utf-8')) and leave 200+
                          bytes spare. Nexus's description is ASCII (bytes == chars); its per-version
                          CHANGELOG field is a separate hard 255-character cap — see CHANGELOG.md's header
docs/choosing-a-model.md  PLAYER-FACING "Which AI should I use?" — the one place the model/backend/local
                          detail lives, written in layers (pick-a-row table → costs → each provider's
                          one catch → local setup → why: tool calling, thinking-off, context). Linked
                          from README + all three store pages, so keep the URL path stable
docs/models-and-costs.md  the DEVELOPER decision record behind that guide: why these defaults, the
                          price-migration rule, the four thinking-off dialects
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
  WHOLE at each compression/reflection (`ApplyCompression` just takes it; a reply with no usable
  `SUMMARY:` leaves the old one standing). This is the anti-repetition core. Memory-writing calls
  run on a separate client with `MaxMemoryWriteTokens` breathing room — which, with FACTS and GOALS
  gone, is now the ONLY bound on how much of a person a soul may carry.
  **THE TRUTHS AND THE AIMS ARE RETIRED (2026.08.08, Anton's call).** A third layer of distilled
  one-line `KnownFacts` (the `hold_truth` tool + the reflection's `FACTS:` replace-all contract) and
  a parallel `NpcGoals`/`goals.txt` system (the `tend_goals` tool + `GOALS:`) rode here until then.
  They were cut for two reasons: the lists cramped the same material the rolling memory already held
  and then read it back to her a second time in her own sheet, and each cost a tool slot in every
  spoken reply (tools compete for attention — see the gpt-4o `move_heart` shyness). Their room went
  to the deep memory: a much richer `SUMMARY:` ask plus `MaxRecentTurns` 30→40 / keep 15→20.
  **Do not reintroduce either.** Compat rails, keep forever: `NpcMemory.KnownFacts` survives as a
  DEAD field so an old `memories.json` keeps what it already holds (read by nothing, written by
  nothing, never folded into a prompt); old `goals.txt` files are simply never read again; and
  `ParseResponse` still bounds its sections on stray `FACTS:`/`GOALS:` labels so an old-habit model
  can never silt a bullet list into the memory or the self.
  The one load-bearing piece of prose is `MemoryCompressor.AppendReplyFormat` — it must keep both
  inviting the particulars (names, promises, debts) and warning her the memory is written whole
  ("what I do not set down here fades from me"), or each pass quietly erodes what the truths used
  to nail down. It is guarded by tests; change it deliberately, never in passing.
- **Every NPC gets a distinct voice.** `PersonaBuilder` deterministically assigns a speech
  style from `Hero.StringId` so it's stable across sessions, plus personality from real
  traits. Distinct voices + relevant-only context are the levers against repetition.
- **Gemini and DeepSeek are first-class backends since 2026.08.02** (asked for on Steam — "weird to
  offer only Claude and OpenAI while Gemini allows free usage"). Both are OpenAI-compatible and ride
  `OpenAIChatClient` through a new `OpenAiDialect` enum whose ONLY job is how each provider is told to
  stop thinking: **Gemini** takes `reasoning_effort`, but `"none"` works only on 2.5 models — the 3.x
  line cannot be silenced, only turned down to `"minimal"`, and it defaults to HIGH when the field is
  omitted; **DeepSeek** takes `thinking: {"type":"disabled"}` and thinks by default without it. Because
  Gemini's ceiling covers thought AND speech, `ModConfig.GeminiThinkingFloor` (1500) raises its token
  budget — a 400-token spoken cap would be eaten in silence, the "..." bug wearing a new hat. A 400
  naming our quieting field drops it and retries once, so a renamed switch never mutes an NPC. Gemini's
  pitch is FREE (aistudio.google.com, no card, ~1,500 replies/day) and its paid rates are worse than
  luna's — **always disclose that Google's free tier trains on what it receives**; DeepSeek's pitch is
  cheap (~half an exchange's cost, prices DOUBLE in Beijing peak hours, servers in China). Defaults
  deliberately unchanged. Full rationale in `docs/models-and-costs.md`.
- **OpenRouter is the default backend since 2026.07.28**, model `openai/gpt-5.6-luna` — Anton's call:
  one key reaches everything, and luna + `gpt-5.4-mini` are the only two he has really tested. The
  recommendation order everywhere (README, first-run popup, MCM hints) is OpenRouter(luna → 5.4-mini)
  → OpenAI(same two) → Anthropic(works, untested at length) → anything typed by hand, at your own risk
  → Local, which is tinkerers-only and explicitly unsupported. Existing config.json files are NOT
  migrated; only fresh ones get the new defaults. Historical note: Anthropic was the default with
  `claude-haiku-4-5` (2026.07.13, price-matched to
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
- **A tool's contract lives in its SCHEMA, not in its prose** (2026.08.09, learned the hard way on
  `weigh_misgivings` — see TASKS_DONE). A closed set of words explained only in a parameter's
  description comes back as the model's own synonym ("resolve" for `settle`), and a parameter named
  `text` gets whatever the model has most to say (the ANSWER, with the misgiving pushed into `note`).
  So: put allowed values in `ToolParameter.AllowedValues` (both clients emit them as the schema's
  `enum`), NAME each parameter for what it holds and say in its description what it never holds,
  canonicalize honest synonyms in the parser as the second line of defense, and never let a resolver
  branch do nothing SILENTLY — say which words it wanted, the tool loop has rounds left to correct
  itself. And **probe a new tool live before trusting it**: the harness that found both of these
  reads the ToolDefinitions out of the C# sources, rebuilds the sheet from the NPC's own runtime
  files, and runs the real 3-round loop against the real backend.
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
- **THE ANGEL IS RETIRED (2026.08.07, Anton's call — "no more angels, they break the immersion").**
  There is NO narrator voice anywhere anymore: every beat an NPC lives — arrivals, meetings, ALL
  letter beats, the reach-out ponder, the feeling weighing, memory reflection/compression, the hiring
  beats — is their OWN first-person mind (`BuildInnerPrompt` / `InnerFrame` "(Within my own mind: …)",
  recorded with `ConversationTurn.InnerSpeaker`; memory work opens "(Within my own mind — I, Name: …)").
  Any doc/comment below still saying "the Angel narrates X" describes the pre-2026.08.07 shape — read
  it as "her own mind lives X, first person". NEVER reintroduce a narrator voice in any new feature.
  **Legacy rails (keep forever):** recorded Angel turns from older saves replay/render exactly as
  spoken (`AngelFrame`, `ConversationTurn.AngelSpeaker`, `SystemVoiceName` — all legacy-only now);
  the letter-beat markers recognize BOTH eras (`IsComposeLetterBeat`/`TryExtractReceivedLetter` hold
  the old Angel fragments beside the new first-person ones — never touch the legacy constants); and
  `MemoryCompressor`/window views still attribute old Angel turns by the voice's name. History: the
  Angel spoke second-person dialogue beats from the start; reach-outs were de-Angeled 2026.07.26
  ("what are you, my wife?"), everything else on 2026.08.07.
- **Player-authored prompts are first person too**: the global prompt folds in as "Of this world,
  this I know:", the per-NPC prompt as "Of myself, this I hold true:", and every hint (file
  templates, the in-game editors' titles, NPCs\_README) tells the player to write the per-NPC file
  in the character's own "I …" voice.
- **Never break the fourth wall to them.** No "AI", no "prompt", no game title, no "the player" as a
  cold label. To them, Calradia is simply the world they live in and the player is a person.
- **Short rules, more freedom.** Long prompt rules make every soul answer the same; keep guidance to
  the basics and offer the rest as invitation.
- Debug/inspection views the *player* sees (raw-prompt dump, etc.) may be plainer, but even there
  the system message is labeled as the NPC's own mind ("Name, within their own mind"), never `SYSTEM`.

The two builders that carry this tone are `PromptBuilder` (Core) and `SituationBuilder` (Module),
plus the reflection prompts in `MemoryCompressor` (first-person inner monologue too). The scene
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
- `config.json` — API keys, `Backend` ("OpenRouter"/"OpenAI"/"Gemini"/"DeepSeek"/"Anthropic"/"Local"),
  model, `MaxTokens`, memory limits,
  `GeminiApiKey` + `GeminiModel` (2026.08.02 — `Backend: "Gemini"`, the FREE road: the same
  OpenAIChatClient pointed at `ModConfig.GeminiEndpoint` (Google's OpenAI-compat door) with
  `OpenAiDialect.Gemini`; default `gemini-3.6-flash` — deliberately not a Lite, the tools need the
  stronger model; every "flash"/"flash-lite" id has a free tier, Pro is paid-only since April 2026;
  `GeminiThinkingFloor` = 1500 because 3.x thinking cannot be turned off and shares the reply's
  ceiling) + `DeepSeekApiKey` + `DeepSeekModel` (same day — `Backend: "DeepSeek"`, the CHEAPEST road:
  `ModConfig.DeepSeekEndpoint`, `OpenAiDialect.DeepSeek`, default `deepseek-v4-flash`; scores 50 vs
  luna's 51 at ~half the cost; both unplaytested as of writing),
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
  `EnableBattleChronicle` (2026.08.08 — THE BATTLE CHRONICLE, see its own section below: every player
  battle recorded as JSON + chronicle.txt in `_battles`, first-person beats in every allied hero's
  memory, the freshest shared battle whole in their situation, `recall_battle` by name; default on),
  `EnableJourneyLog` (2026.08.08 — THE ROAD JOURNAL, see its own section below: the witness log of
  stops/trade/men/captives/tasks in `_journey.json`, seen only by souls riding in the player's
  party; default on),
  `EnableBirthChronicle` (2026.08.10 — THE BIRTH CHRONICLE, see its own section below: every child
  born to the player written in two parts — THE HOUR in the mother's own voice, kept between the two
  parents, and THE FEAST if one is bought — kept forever in `_births`, recallable by `recall_birth`;
  one writing call for the hour, one more if it is feasted; default on),
  `EnableWeddingChronicle` (2026.08.09 — THE WEDDING CHRONICLE, see its own section below: the
  player's wedding written in two parts by the chronicler — the public day and the couple's own
  night — kept forever in `_weddings`, beat into the spouse and every witness, recallable by
  `recall_wedding`; two writing calls, once per wedding; default on),
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
  word-for-word fragments of the letter templates, both eras (first-person + legacy Angel); BOTH
  windows carry a "?" info overlay beside
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
  `SeedSelfFromWorldStory` (a never-spoken-with NPC's DEEP MEMORY opens with the story the world
  tells of them — a wanderer's tavern tale, a noble's encyclopedia repute — instead of an empty
  Summary; since 2026.08.08 a memory they rewrite at every compression, not a self.txt page; key
  name kept for compat; default on),
  `PersonaSparkMode` (2026.08.07 — THE DIRECTOR'S SPARK: at a soul's FIRST interaction, before their
  first words, one plain LLM call — the "casting director", a refiner-class utility call — writes 1–3
  first-person sentences of private starting truth into their custom_instructions.txt under a
  "# spark:" stamp comment, seeded from their real story (BackstoryBuilder), traits, assigned speech
  style, the global prompt + RoleplayGuidance, and steered by two drawn muse cards (~24-card deck) +
  a weighted intensity die (subtle 30% / marked 50% / vivid 20%) — Core `PersonaSpark` (deck, prompt,
  ClampToSentences; prompt LIVE-VALIDATED on gpt-5.6-terra before shipping), Module
  `EnsurePersonaSparkAsync` hooked into all five first-interaction paths (recap, player turn,
  reach-out ponder, letter write, letter answer; facts gathered before the first await = game
  thread). "Generate" (default) / "Ask" (once-per-soul popup, player-facing paths only; it GATES
  the first exchange — the reply is made only after the player's word, so a granted spark speaks
  from her very first answer (2026.08.07 playtest fix: the popup fired async at first and her reply
  outran it); a 10-min safety net proceeds plain and a late "Shape them" falls back to a background
  generation; declining writes a durable declined stamp) / "Off". The spark file is the PLAYER'S file: hand-written
  content blocks generation, deleting the file re-seeds, a `# spark:` stamp means done; DevMode
  lever "[test — reroll their spark]". MCM dropdown "Starting personality" (McmChoiceLists.SparkModes,
  menu "Ask first" ↔ config "Ask") wired through repair/signatures/push/pull/rescue),
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
  `EnableConversationHiring` + `ConversationHiringHagglePercent` (2026.08.07 — HIRING BY HANDSHAKE:
  an unhired wanderer facing the player may strike the hiring bargain in the talk itself via the
  `strike_bargain` tool (`Tools\BargainTool`), riding ONLY the live reply trunk. The one law: words
  alone can never hire — the tool only LAYS terms, the sole door is a confirm popup naming the exact
  price (plus the game's reckoning when they differ), and lay AND seal both re-run vanilla's own
  rules (free wanderer, co-located, gold, companion limit — `BargainBlockReason`). Haggling is railed
  hard to ±`ConversationHiringHagglePercent` (default 30, clamp 0..90, 0 = fixed price; MCM slider +
  checkbox, live) around `CompanionHiringPriceCalculationModel`; the daily wage is never negotiable
  and is quoted from `CharacterObject.TroopWage` — NEVER `PartyWageModel.GetCharacterWage`, which
  answers 1 for heroes (the "one denar a day" lie). Sealed = vanilla's three acts (GiveGold +
  AddCompanion + AddHeroToParty) + a silent first-person beat ("They hired me; I ride with them");
  declined/blocked = honest beats too. The
  seal inquiry (QueryManager) rides a global layer at order 19501, safely above the chat window's
  4500; both default on. The unhired one's terms + seller's mindset live at the TOP of her sheet —
  `PersonaBuilder.SellswordTerms` — and the tool's out-of-rails refusal deliberately does NOT name
  the floor as an offer: the first cut did, and she parroted it verbatim the next breath),
  `EnableConversationMarriage` + `AllowCompanionMarriage` + `MarriageNeedsFamilyConsent` +
  `MarriageDowryHagglePercent` + `CourtshipCharmSlack` + `MinBetrothalDays` (2026.08.08 — MARRIAGE
  BY COURTSHIP, the bargain mold applied to the biggest bond: an NPC walks her OWN persisted road
  (Core `CourtshipStage` in `NpcMemory` → snapshots rewind it) via `tend_courtship`
  (`Tools\TrothTool`), railed by Core `CourtshipRoad` — relation floors, one step/day, the STATION
  GATE (her station tier minus the charm slack, binding only from Ready — the heart is free, the
  HAND has rails) — and her OWN MISGIVINGS (2026.08.08, replacing the matchmaker's checkable-ask
  DSL the day after it shipped — Anton: the auto-judged gold/skill/trait stoppers read as "robotic
  bargains"; `MatchmakerLedger`/`CourtshipAsk` are DELETED, the old CourtshipAsks JSON field is
  ignored on load): when marriage truly enters the talk she writes her own doubts via the
  `weigh_misgivings` tool (`Tools\MisgivingTool`, rides beside tend_courtship on the same tally —
  set_down one-per-line or honest "none" / settle-with-a-light-word / release (strike out, no note)
  / revise / reopen; Core `CourtshipMisgiving`+`CourtshipMisgivings`, its own lenient fuzzy
  matching, unit-tested. THE LIST LIVES — 2026.08.08 evening, Anton: "не искам да остават статични":
  the cap of 5 binds only what STANDS OPEN at once (settled never block a new mid-talk doubt), past
  10 carried the oldest SETTLED fade, and the sheet tells her the list is hers to grow/strike/rework),
  and the road's only misgiving-rails are HERS: Ready + the betrothal wait for a weighed heart
  (`MisgivingsWeighed` — even "none" counts) with zero standing (`MisgivingsUnweighed`/
  `MisgivingsRemain` verdicts, numberless refusals; the wedding lay re-checks neither) — and she
  KNOWS it (the sheet says plainly: while any stands her hand waits, when none stands no doubt of
  hers bars the road — the anti-exploit anchor). The player
  SEES them: bond line "misgivings 2/4", a "Misgivings n/m" button in the chat window opens the
  list (settled ones with her note), and EVERY movement leaves a log line in Anton's color language
  — ROSE when the heart clears (settle/release/clear heart), FROST-BLUE when something freezes
  (set_down/reopen, and the road's own step-back; a broken troth alone stays red). Souls
  with real history are SEEDED once from their lived story (Core `CourtshipSeed`, capped at
  Betrothed). Betrothal + wedding lay only; popups seal; both re-run everything
  (`TrothBlockReason`). The wedding is REAL: nobles `MarriageAction.Apply`; a companion bride is
  graduated `SetNewOccupation(Lord)` (vanilla's companion-to-lord shape — keeps party place and
  duties) then the same Apply — cutscene/log/tidings arrive from vanilla's listeners. Noble kin:
  `bless_marriage` on her clan HEAD (a second bargain, ±haggle around her clan's Renown with
  vanilla's spinster relief; sealed = gold + a blessing beat reaching HER anywhere); once betrothed
  the head is UNLOCKED as a letter correspondent (ours or vanilla's CoupleAgreedOnMarriage).
  Stages mirror into vanilla Romance (Warmth+ → 4, Betrothed → 6 — removes her from the
  NPC-marriage lottery, re-asserted each exchange). Marry Anyone detected by assembly scan →
  the player's own marriage stops hard-blocking, the (patched) model stays the law. OFFERS RIDE
  LETTERS: the letter-answer flow carries bargain/troth/bless hands (`byLetter`), the laid offer
  persists ON the `Letter` (LaidKind/Price/Word/BrideId) and is presented at delivery; the
  wedding day alone refuses paper. Road stage shows in both windows' bond line + the odds view;
  DevMode levers "[test — courtship & quiet asks]" (now the road + her misgivings) + "[test —
  clear their marriage misgivings]". Lives in
  the `ImmersiveChatBehavior.Courtship.cs` partial; decision record docs/marriage-courtship-design.md),
  BOTH windows also carry the prompt-editing doors (2026.08.07): "Their prompt" (the selected NPC's
  custom_instructions.txt) and "World prompt" (global_prompt.txt) open an IN-GAME editor overlay
  (Anton asked for no-alt-tab the same day the first cut opened Notepad) — the letters-composer
  shape: tall wrapped mirror + single writing line (Gauntlet inputs hold no newlines, so the text
  edits as ONE flow), Save/Discard buttons, Escape discards first (before the info overlay, before
  closing), Enter never sends while it is up; saving keeps the file's #-comment lines gathered at
  the top (PromptFiles.LoadNpcPromptForEdit/SaveNpcPromptFromGame + the global pair) — no restart,
  prompts are re-read at every context build,
  `RevertMemoriesWithSaves` (save-scoped memory — each save photographs the whole campaign memory folder
  and loading it restores the photo, so a reload truly un-remembers a bad turn, the way the game already
  rewinds the relation inside the save; the fix for the reload-divergence "memories from the future" bug;
  default on) + `MaxMemorySnapshots` (disk cap on snapshots per campaign, oldest pruned first; default 40) +
  `MaxMemoryWriteTokens` (output budget for the memory-WRITING calls — reflection/compression run on
  their own client so the rolling memory + self never get squeezed by the spoken `MaxTokens` cap;
  default **4000** since 2026.08.08, never below `MaxTokens`. It was 1500 until the first playtest
  after the truths were retired severed a rich Bulgarian memory in mid-word: **text outside ASCII
  costs ~1.6× the tokens English does**, so every token budget in this mod is far tighter than it
  looks for most of the world. Migrated by ConfigVersion V4 where it still held the exact old
  1500. The mid-word cut itself is now impossible to SAVE — `MemoryCompressor.TrimToLastCompleteSentence`
  drops a severed tail back to the last finished sentence, refusing when that would throw away most
  of the text; and `MemoryTokenEstimator` charges non-ASCII characters 1.6× so the gauge and the
  compression triggers stop under-reading non-English play),
  `NotifyOnMemoryRefactor` (a soft activity-style notice the moment an NPC's automatic compression
  reworks her deep memory — "…turns over old memories of you, and settles them deeper"; default on),
  **the consolidation dials, all live and all in MCM's own "Memory" group since 2026.08.08** (Anton's
  ask — `MaxRecentMemoryPercent` / `MinRecentMemoryPercentAfterCompression`, `MaxRecentTurns` /
  `KeepRecentTurnsAfterCompression`, `MaxRecentDays` / `KeepRecentDaysAfterCompression`,
  `MaxMemoryWriteTokens`, `NotifyOnMemoryRefactor`): three ceilings, whichever is hit first at the
  moment a turn is recorded, so an edit bites on the very next exchange. Their rails, menu names and
  hints all live as consts in `MemorySettingsMetadata` — MCM attributes take compile-time constants,
  so the menu and `Normalize`'s clamps share the same literals. `Normalize` also halves a keep window
  that stands at or above its own ceiling (it would fire a compression folding nothing away), and the
  bridge mirrors ONLY those numbers back to the menu after a pull (`PushMemoryToMenu`) so a correction
  is visible — a full push there would mangle a text field mid-typing. `MaxRecentMemoryTokens` /
  `MinRecentMemoryTokensAfterCompression` in config.json are DERIVED from the percents and rewritten
  on every load: they are there to be read, not set. The chat window shows the live weight against
  all of it under the bond line (`MemoryTokenProfile.MemoryLoadLabel` — "memory 5.2% / 10% · 21k / 40k
  tokens · turns 12/30 · oldest 4d/30d · trims back to 5%"),
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
  400s; OpenAI default model is gpt-5.6-luna (2026.07.28 — and on 2026.08.02, luna having proven
  itself in live play AND become nearly the cheapest after the July price cut, it was SWAPPED to the
  top of both cloud dropdowns with Anton's blessing: a swap with 5.4-mini, not a shift, so only the
  two exchanged slots change meaning for old MCM store files — the one sanctioned deviation from
  McmChoiceLists' append-only rule; historical: 5.4-mini was the default 2026.07.12–28 after terra →
  luna both stumbled on access-propagation 401s; NO 5.5 mini/nano exist; older models live on as
  config.json hand edits), existing configs deliberately unmigrated — see docs/models-and-costs.md),
  `ConfigVersion` (format stamp, 4 — migrations key off it; V2 = the letter hotkey's U→Y move,
  V3 = correcting superseded built-in `ModelPrices` after OpenAI cut luna 80% / terra 20% on
  2026.07.30 — only entries still equal to the exact old figure move, hand-edits survive; the list
  of what was superseded lives in `ModConfig.SupersededModelPrices`; V4 = `MaxMemoryWriteTokens`
  1500 → 4000, same only-if-still-the-old-default rule. NOTE the asymmetry, it is
  deliberate: model DEFAULTS are never migrated, PRICES always are — a model swap changes voice and
  money and stays the player's call, a stale price is just a lie in the cost notice. The V4 budget
  migrates by that same test: a ceiling too low to finish the memory is not taste, it is a wound),
  `DevMode` (default **false**, for players: hides the `[Immersive AI • test]` levers and the
  "Reveal the whole of your mind" inspector in the face-to-face menu, and the deep-memory overview
  panel in the chat window; set true when working on the mod — Anton keeps it true).
- `global_prompt.txt` — world-wide instructions added to every NPC (lines starting with
  `#` or `//` are ignored, matching ChatAi's convention).
- `conversation_presets.txt` — the PLAYER's own standing conversation presets for "Think" (`name = wish` lines,
  same #-comment convention; Core `ConversationPresets` parses/formats, `PromptFiles` reads/writes,
  both windows edit it in game). Nothing an NPC ever sees. Ships with starter / romantic / ender.
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
  - `custom_instructions.txt` — per-NPC prompt (comment lines `#`/`//` ignored), written in the
    character's own first person; folds in as "Of myself, this I hold true:". Usually begins with
    the director's spark (a generated 1–3 sentence starting truth under a `# spark:` stamp comment —
    see `PersonaSparkMode`); the stamp marks the soul as sparked (or declined), hand-written content
    always wins, and deleting the file invites the director back.
  - `current_situation_info.txt` — environmental facts (when/where/who) snapshot plus recent world
    tidings and local rumors (see `TidingsBuilder` below), rewritten every time the player opens a
    chat; built by `SituationBuilder` relative to the party the NPC speaks with, written as a gentle
    second-person narration and folded into her prompt.
  - `self.txt` — the NPC's OWN evolving sense of self (`NpcSelf`), written by them in first
    person during reflection (not by the player). Kept separate from `memories.json` because
    the self is general to the NPC while memory is branching toward per-person files. Folded
    into the prompt as "Who you have become". Updated by `MemoryCompressor.ReflectAsync`.
    **THE BACKSTORY SEEDS THE DEEP MEMORY NOW, NOT THE SELF** (2026.08.08, Anton's call — the
    self-seed of 2026.07.10 stood forever at a fixed place and weight; as memory it is theirs to
    rewrite, keep, or let fade at every compression, and the empty starting Summary is put to
    work): a soul whose memory has never held anything opens `NpcMemory.Summary` with a
    wanderer's hand-written tavern tale (first person, from the game's
    `backstory_a..d`/`generic_backstory` strings keyed by character template) or a noble's
    encyclopedia account (`Hero.EncyclopediaText` if hand-authored, else the generated
    `Hero.SetHeroEncyclopediaTextAndLinks` paragraph, framed "So runs my story, as the world
    tells it:") — gathered by `BackstoryBuilder.BuildStorySeed` (Module), shaped by
    `StorySeedFormatter` (Core, was SelfSeedFormatter), hooked in the behavior's
    `SeedMemoryFromStory` (rides `LoadMemory`; in-memory until the first real interaction saves).
    A RENOWNED PLAYER'S RUMOR CLOSES THE SEED (same day, Anton's ask): what the world had told
    this soul of the player before they ever spoke — `StorySeedFormatter.FromPlayerFame`
    (Clan.PlayerClan.Renown, tiers aligned with the live lines: silent under 150 = the beholder's
    "no word of their deeds", 300 = carried far, 900 = "famed across all Calradia" like the clan
    recall), appended after the backstory (or standing alone for souls with none, e.g. notables);
    it too is memory — hers to keep or let fade — while the situation line keeps telling how far
    the name travels TODAY.
    The rails: `NpcMemory.SeededFromStory` keeps a seeded-only summary from counting as knowing
    the player (`HasRememberedHistory` — she still meets them as a stranger), the sheet heads it
    "The road of my life so far…" instead of "What X is to me" until something is truly lived
    (`StoryRichness == 0`), and the SUMMARY: contract's "my own road from before" clause invites
    her past to survive each whole-rewrite BY HER CHOICE — all three are load-bearing, tested.
    `self.txt` begins unwritten now (first reflection invites them to author it); souls seeded
    the old way (non-empty self.txt) are never seeded again. Toggle: `SeedSelfFromWorldStory`
    (key name kept for config compat).
  - `goals.txt` — RETIRED 2026.08.08. Held the NPC's own aims until then; nothing reads or writes
    it any more. Existing files are deliberately left where they lie (never deleted, never migrated),
    so the folder of a long-played campaign may still show one.
  - `letters.txt` — human-readable log of every letter carried between the player and this NPC,
    both directions, including "(read and let lie unanswered)" notes. Append-only record.
  - future per-NPC files go here too.
- `NPCs\campaign_<id>\_letters.json` — the letters currently ON THE ROAD for that campaign
  (Core `LetterBag`; letters travel real in-game days and must survive save/load). Delivered
  letters leave this file — they live on in NPC memory and `letters.txt`.
- `NPCs\campaign_<id>\_battles\` — the BATTLE CHRONICLE (Core `BattleLedger`, 2026.08.08): one JSON
  per battle the player fought (`d0123-2_<title-slug>.json` — sides by the four fighter kinds +
  seasoning, cost, prisoners taken/captives freed, spoils with top-5s, plunder/renown/influence,
  every allied hero's downs and fate) plus an append-only `chronicle.txt` of the full accounts.
  Lives inside the campaign folder ON PURPOSE: the save-scoped memory snapshots photograph it, so
  reloading rewinds the war together with the memories of it. Toggle `EnableBattleChronicle`.
- `NPCs\campaign_<id>\_births\` — the BIRTH CHRONICLE (Core `BirthLedger`, 2026.08.10): one JSON per
  child born to the player (`d1120_ira.json` — the day's facts, the children, the witnesses, and both
  written accounts) plus an append-only `births.txt`. The hour lives in the file but is handed only to
  the two parents — never to a witness, in memory or by tool. Same snapshot-rewind ride. Toggle
  `EnableBirthChronicle`.
- `NPCs\campaign_<id>\_nights.json` + `nights.txt` — the NIGHTS (Core `NightLedger`, 2026.08.09):
  every wife's rolling fortnight of them (who came to her, whose door was closed, what she learned of
  the rest) plus the append-only keepsake of every written night, in full, never pruned. Same
  snapshot-rewind ride. Toggle `EnableNights`.
- `NPCs\campaign_<id>\_journey.json` — the ROAD JOURNAL (Core `JourneyLog`, 2026.08.08): the light
  witness log of the player's everyday life — the last ~12 stops kept (place/kind/stay + trade
  values with chief pieces + recruits + garrison drops + captives sold/donated) and the tasks
  carried (taken → outcome with the game's own reason). Same snapshot-rewind ride. Toggle
  `EnableJourneyLog`.
- `NPCs\campaign_<id>\_weddings\` — the WEDDING CHRONICLE (Core `WeddingLedger`, 2026.08.09): one JSON
  per wedding of the player's (`d91123_sibylla.json` — the day's facts, the witnesses, and BOTH written
  accounts) plus an append-only `weddings.txt`. The night part lives in the file but is handed only to
  the spouse — never to a witness, in memory or by tool. Same snapshot-rewind ride. Toggle
  `EnableWeddingChronicle`.
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

Talking to any hero shows a **"Speak freely with me."** dialog option (the `[Immersive AI]` tags
came OFF every dialog/menu option in v1.4.0 — the first Steam feedback called them immersion-breaking;
localization ids unchanged, only the DevMode `[Immersive AI • test]` levers keep a tag) →
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
(`PromptBuilder.BuildFeelingQuery`, her own first-person inner weighing, one signed number via `FeelingParser.ParseShift`,
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
ephemeral — her own mind marks the arrival (`PromptBuilder.ArrivalLine`, first person, first-meeting vs
"comes to me again") and her greeting is stored as a real inner turn, exactly like reaching-out and letter beats, so
her memory shows WHEN the player came to her; the old `_lastGreeting` weaving hack is gone (the history
carries the greeting), and recorded beats replay with their `[place, time]` stamp. With recap disabled no
beat is recorded (nothing is fabricated). **The prompt sheet reads like a mind waking toward the moment**
(same day): identity → kin → self → About Calradia/About you → deep memory of the player (summary +
truths) → the situation LAST — itself ordered setting → who you are → tidings/rumors → "And now X comes
to you" + where the heart stands — so the arrival is the final breath before the live transcript. The
standing line lives only in the situation now (removed from `PersonaBuilder.BuildRole` — never tell her
the same heart twice).

**Even meetings without free chat are remembered** (2026.07.10): when any hero conversation ends
(`CampaignEvents.ConversationEnded`) that never became recorded beats (no free chat — `PrepareChat`
marks it via `_conversationBeatNpcId`; no accepted reach-out — `DeliverApproachAsync` marks it too), a
**silent first-person note** lands in their memory: "X and I met and spoke face to face for the first
time — a stranger no longer, though the words of it are not set down here" (`PromptBuilder.MeetingLine`,
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
0.1) of a full bond's pull, so a stranger may cross the room and begin their story (their own ponder
tells them honestly it would be a first acquaintance — `ReachOutPonderLine(stranger: true)` —
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
stays quiet and `DailyInitiationRate` is the day's total for a full bond.
**Outreach rests and silence is heard** (2026.07.26, the first Steam feedback — the same soul knocking/
writing about the same thing for hours; root cause was a FEEDBACK LOOP: outreach beats are recorded
turns, so an NPC's own reach-outs raised their richness/recency and made them likelier to reach out
again): `NpcMemory` tracks `LastOutreachGameDay` + `UnansweredOutreachCount`, and every pull is
multiplied by `InitiationScorer.OutreachDamping` — zero right after any self-initiated outreach,
recovering over 0.75d, with each UNANSWERED outreach adding +4d patience and ×0.4 on the ceiling
(2 unanswered ≈ 16% max). Any player engagement resets the count: a player turn (`AddTurn` auto-reset),
a meeting note, reading the player's letter, or the player's letter leaving the courier. The beats mark
themselves via `AppendRecordedTurn`'s `OutreachMark` (Reached / Considered / PlayerEngaged — desire
weighings and invited replies rest without the pride wound). The damping multiplies AFTER the presence
floor (else the floor re-arms the spam); `MemoryIndex` carries both fields so the hourly rolls stay
cheap, and BondStatsLabel/the odds view show the damped truth ("awaits your answer (2 unanswered)"). Firing only happens at *safe* moments
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

When one is moved, the reaching-out plays out as **recorded beats of her OWN mind** — nothing hidden from
her or from the player on inspect. **De-Angeled 2026.07.26** (Anton: the Angel's tender "from your own
heart" framing bred emotional small-talk visits — the steward asking how you feel, the quartermaster's
"troops are good, how are you today"): the beats are now first-person inner reckonings
(`ConversationTurn.InnerSpeaker` = "Self", replayed via `InnerFrame` "(Within my own mind: …)", rendered
in the window as "(Name, within: …)" narration and in reflection as "My own thought, then:"; since
2026.08.07 EVERY beat lives this way — arrivals, letters, reflection, all of it, no narrator
anywhere). The situation for these beats is the **NEARBY shape**
(`SituationBuilder.BuildNearby` — "X is nearby, about their own affairs"), because the meeting shape's
closing "And now X comes to me" contradicted the question of whether to go. The beats:
(1) `PromptBuilder.BuildInnerPrompt` with `PromptBuilder.ReachOutPonderLine` — the full sheet (news, mood,
duty, memory) plus ONE simple nudge: "Is there something I want to discuss with them just now?" — answered
**NO or "YES: the something"** (`InitiationParser.WantsToGo`, word-boundary-safe, old STAY/GO still read;
unreadable answers fall back to plain yes/no, then NO). Deliberately NO instruction about what a worthy
topic is — the first cut listed causes and banned courtesy, and that made every soul answer the same
("the AI stops being AI and becomes a program again", Anton 2026.07.27); YES/NO rather than STAY/GO so
the words never smell of physically leaving. Memory keeps a condensed note (`ReachOutPonderNote`,
prefix-matched by `IsPonderBeat` so the window folds reckoning+resolution into one narration line), and the
resolved **reason rides into the delivery** — `FirstWordLine`/`ApproachLine` carry "What brings me: …", and
the recorded `FirstWordNote`/`ApproachNote` keep it, so the next ponder sees what was already brought (the
content-repetition brake; inner beats also never reset `UnansweredOutreachCount`). For the offer shape the
reason travels in `PendingNotice.Reason` → `ShowInitiationInquiry` → `_currentApproachReason`.
(2) On GO, the player gets a
faced portrait toast and — with `UseMapNoticeForInitiations` on (default) — a **persistent, non-pausing
right-side map notice wearing her live portrait** (see the Harmony section below); clicking it opens the
accept/decline inquiry (which pauses per `PauseOnInitiationOffer`). The notice waits up to 2 in-game days,
then quietly lapses (she is not told of a door the player never reached); several NPCs can be knocking at
once. Without the notice UI the inquiry shows directly, as before. (3) The approach is narrated *after* the choice (`DeliverApproachAsync` with
`PromptBuilder.ApproachLine`): **Receive them** → she lives the welcome in her own mind and speaks her
greeting (a recorded inner turn — no weaving needed, so she never repeats it), the conversation opens
(`CampaignMapConversation.OpenConversation`) and falls into the talk loop; **Not now** → the closed door
passes through her own mind and she answers it in her own voice (recorded, shown back with her face) —
a lived moment, not a cold "you were refused". Two LLM calls per fired offer; she can always choose silence.
`MemoryCompressor` attributes legacy Angel turns by the voice's name (not "They") so summaries stay truthful.
Toggle with `EnableNpcInitiatedChats`. Nothing about the schedule is persisted (stateless hourly rolls), so
save/load is a non-issue. Three `[Immersive AI • test]` free-chat options
(gated by `DevMode` + `ShowInitiationTestButton`): `OnDebugForceReachOut` forces the NPC just spoken with to reach out
right after parting; `OnShowInitiationOdds` dumps, for every history NPC, whether they are co-located now
and their computed daily/hourly chance — the go-to answer for "why is it quiet?" (usually: no one
co-located, or near-neutral standings); `OnDebugRenameNpc` (2026.08.07) renames the very soul spoken
with via the game's own `Hero.SetName` (persists in the save; the memory folder heals by stringId, so
the story follows the name — first used to turn Sibuga into Сибила).

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
a thread** (inner-mind beats rendered as soft gray narration — nothing she remembers is hidden), with an
input line below. The player **writes first, with no arrival beat and no forced greeting** — the line
goes straight through `ExecutePlayerTurnAsync`, the shared trunk factored out of `RespondAsync`
(prompt → spoken reply with recall/wisdom riding along → the private feeling number → recorded turn →
compression → save), so window and conversation panel are the same machinery with different rendering.
One in-flight exchange per NPC (`_quickChatBusy`); a failed send puts the words back in the input box.
**Talking through the mod counts as meeting in vanilla's eyes** (2026.07.26): the game flips its
`Hero.HasMet` only when a native conversation screen ends, so window exchanges and delivered
reach-outs left the pair "strangers" and the next real dialog opened on the full "I am so-and-so"
introduction — `MarkMetInWorldsEyes` (game's own `SetHasMet()`, game thread) now rides the
quick-chat reply, the delivered first word, and the "not now" approach; letters deliberately don't.
**Reach-outs become messages** (`SendInitiationsToChatWindow`, default on): after her recorded yes to
the desire question, there is NO accept/decline — `DeliverFirstWordAsync` has her simply speak
(`PromptBuilder.FirstWordLine`, stranger-aware, honest that the player may answer only later), records
it as a real inner turn, fires a faced toast ("Ava sees you and says: …"), marks the thread unread,
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
`SendInitiationsToChatWindow`. **The window carries a DEV PANEL since 2026.08.08** (Anton's ask —
the face-to-face devmode menu grew crowded): a "Dev" button in the window bar (DevMode only) opens
an overlay with every test lever acting on the selected soul — reveal mind, courtship road, clear
misgivings, reroll spark, force reach-out/letter, forge battle, rename, the odds view — wired
through `ImmersiveChatBehavior.Dev*` static bridges to `*For(Hero)` refactors of the dialog levers
(popups ride the global inquiry layer, safely above the window). Beside it the **"Misgivings n/m"
button** (players too) opens the selected soul's written marriage misgivings. Escape folds: prompt
editor → dev panel → misgivings → info → close; Enter never sends under any overlay. The
face-to-face menu itself was re-ordered the same day: "Speak freely with me." rides priority 120
(top of the vanilla hub), "Farewell." dropped to 85 so it sits BELOW all the devmode levers
(95..88) instead of stranded mid-list.

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

**"Think" (Shift+Enter) — the player's own next line (2026.08.10).** The one lever that points INWARD:
every other feature gives the NPCs a mind, this lends the PLAYER theirs when the words will not
come. A button in both windows (Core `Prompts\PlayerThought` + `Prompts\ConversationPresets`, Module
`ImmersiveChatBehavior.Thoughts.cs`) hands the player everything the chosen one would read and asks
for their own next line.
**THE SEATING CHART IS THE WHOLE TRICK — learned twice in one evening (2026.08.10, playtested).**
Cut one hung an aside off the end of the NPC's own live chat (`BuildPlayerThought` = `Build` with a
different closing message): every word of the aside said "now it is Mizam's turn", and terra still
answered *as Sibylla* and handed Anton HER line to send back to her. Cut two kept her whole first-
person sheet as quoted material inside one user message — and it happened AGAIN ("Но първо ти
благодаря, че ми вярваш. Това ми дава сила, мъжо мой."). Of course it did: eleven thousand tokens of
"I am Sibylla" against two lines of system message is not an argument, it is a landslide. So the
sheet is OUT of the thinking altogether. `BuildPlayerThought` emits exactly TWO messages:
• **system** — the PLAYER's own mind, first person (`PlayerThought.MindFrame`: "I am Mizam… I set
  down my own words only… I do not answer in their voice").
• **user** — `ThoughtFacts` (Module): a handful of PLAIN THIRD-PERSON facts about them and first-
  person ones about me (who I am, who they are to me, what they are good at, how they stand toward
  me, where we both are), the world prompt (mine too), then the shared story as a NAMED SCRIPT
  (`RenderScript`: `[place, time] Mizam: …` / `Sibylla: …`, her inner beats as `(Sibylla, to
  themselves: …)`), closing on whose turn it is.
**Her own voice now appears in exactly one place — the transcript, quoted, never inhabited** — and
that is also the honester shape: the player has no access to her private mind, moods or misgivings,
only to who she is and everything the two of them have said. Guarded by a test asserting the message
count and the absence of any assistant role. LIVE-PROBED before shipping against terra and luna, on
Anton's own save, with a preset and with an empty box: four for four in his own voice, in Bulgarian,
with the *gesture* custom intact. It also cut the call from ~11k tokens to ~5k. The closing block is Anton's own shape: *[Now it is my turn to speak.] — What turns in my
mind: "…" — Mizam:*. That ONE frame carries all three uses with no branching prose: an EMPTY box
("nothing is settled in my mind yet — the moment itself must find it": a continuation, or an
opening), a half-typed RANT (read as half-formed thought, handed back as words), and a chosen PRESET
(slots in as naturally as anything else). Deliberately SHORT — his ask was "без чаршафи от
инструкции"; the speech rules, the acting-out custom and the whole story already ride in the material
above it, so the block adds only whose turn it is plus three light rails (same spirit, same tongue,
as short as talk truly is unless the thought asks for more). THREE HARD RAILS: it is a PLAIN call (no
tools — a thought must never move a heart or tend a courtship, and it stays one call), NOTHING is
recorded (the words go to the window's draft store and the writing box, theirs to keep or throw away;
the NPC learns of them only if they are sent), and the log speaks in the PLAYER's own first person
("What should I say… let me think." → "I think this is what I should say."), billed to the PLAYER's
name, with the ledger's `✒ … thought:` line beneath as the honest hint that a paid call was made —
Anton's call: the price tells it, so the words need not (which is also why the button is not named
"Think (AI)": "ще е имържън брейкър"). Keys are FIXED and dumb on purpose (an earlier cut had Enter
guess by what stood in the box): **Enter sends, Shift+Enter thinks**, both spelled into the button
labels ("Think  (Shift+Enter)" / "Send  (Enter)"), the pair riding a row of their own ABOVE the
writing line. The PRESETS are `conversation_presets.txt` ("name = wish", #-comments, lenient parse),
a scrollable menu opening above that row (5 rows then it scrolls) with an Edit page — click to use,
pen to rework, cross to strike out, Save to add, and "Restore the first three" behind a warning
popup. A chosen preset turns the draft mirror violet and says plainly it is a wish, not words to
send. Spoken thinking rides `_client` (the reply budget), a letter's rides `_storyClient` (the
written budget). Config: `EnableThinkForMe` (default on).

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
Config: `EnableWebSearch`. (Two personal hands rode beside these until 2026.08.08 and are now RETIRED:
`hold_truth`/`Tools\TruthTool`, the mid-talk hand on `KnownFacts`, and `tend_goals`/`Tools\GoalTool`,
the hand on `goals.txt`. The live-instance discipline they established survives in the courtship
resolvers — mutate `CompleteSpokenAsync`'s `liveMemory` and save at once, so the end-of-turn save can
never clobber a mid-reply write.) Every tool call also fires
a soft **activity notice** ("X is remembering… (name)", "X takes stock of the company…", "X is researching…
(question)") via `NotifyActivity`/the resolvers
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
`weigh_battle` (company-or-army vs a named band/army/walled place/village, garrison + militia at half weight,
compositions from real rosters, verdict by true `EstimatedStrength` ratio, confidence by Tactics —
NOTE: this game version has NO `PartyBase.TotalStrength`, use `EstimatedStrength`; `ExplainedNumber`
explanations come from the parameterless `GetLines()`; `DefaultSkills` lives in `TaleWorlds.Core`).
**The eyes gained the land itself (2026.07.27**, the "do you see those raiders burning the village north
of us?" playtest — the scout counted brigands while Stathymos burned in plain sight): the survey now also
tells (a) the **places** within sight — villages/towns/castles with whose they are and *how they fare*:
`Settlement.IsUnderRaid` ("and IT BURNS: <attacker> is at the sack of it even now", attacker read from
`s.Party.MapEvent.GetLeaderParty(BattleSideEnum.Attacker)` falling back to `LastAttackerParty`),
`IsUnderSiege` (+ `SiegeEvent.BesiegerCamp.LeaderParty`), `IsRaided` (= village lately Looted),
`InRebelliousState` — nearest four plus any troubled one further off; (b) **a bearing** on everything
(`Whereabouts`/`Bearing`, +Y north +X east — "a few hours' ride to the north", because the player points
at the map and says "north of us"); (c) **what each band is about** (`BandDoing`: raid/siege-assault/
battle from `MapEvent` for every eye, marching intent from `DefaultBehavior` only at Scouting ≥ 50);
(d) bands standing at a settlement are no longer dropped when they are FIGHTING there (the old
`CurrentSettlement == null` filter hid every raider), and a raider/besieger is never crowded out of the
list by nearer villager carts; (e) `weigh_battle` now resolves **villages** by name too — under raid it
weighs the raider ("Stathymos lies under the knife even now…"), else militia at half weight with "no
wall, no gate, no garrison" — and village carts no longer steal the village's own name.
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
chance × `LetterCourier.StoryDepthFactor` (richness/12 capped at 1 — one shallow conversation funds
half-weight letters at best, 2026.07.26) × the same `OutreachDamping` as the visits (a writer whose
letters met silence holds their pen — duty writers too: one field report, then patience until answered);
one moved soul weighs privately, within their own mind — yes/no, recorded — whether they wish to write,
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
lives inside the recorded line, so it enters memory even if they let it lie), and they may answer at most
once per letter — correspondence is a chain of choices, not an echo. Undeliverable (recipient dead) comes
back as a quiet notice. All beats are first-person inner turns in `memories.json`; each NPC folder keeps a plain
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
`TryExtractReceivedLetter` (Core, unit-tested) recognize the recorded letter turns of BOTH eras — the
first-person templates and the retired Angel's legacy fragments — and render them as "✉ by letter"
cards between the spoken messages. Each marker must remain a word-for-word fragment of its template
(recorded memories carry the phrasing they were born with forever), so change a live template and its
"Own" marker together, never one — and never touch the legacy marker constants.

**The battle chronicle (2026.08.08) — the mod's heart meets the game's.** Every battle the player
fights becomes a `BattleRecord` (Core `Battles\`) in `NPCs\campaign_<id>\_battles\` — one JSON per
battle + an append-only `chronicle.txt` — and a short first-person SILENT beat (the MeetingLine
pattern: empty NpcLine, `OutreachMark.PlayerEngaged`) in the memory of EVERY living allied hero who
stood in it: the odds, their own hand's work beside the player's, how they came out, and the forged
name ("The Grand Victory near Ortysia, over Thrice Our Number" — epithet thresholds live in
`BattleText.ForgeTitle`; `BattleText.BeatMark` opens every beat and must NEVER be reworded, recorded
memories keep their phrasing forever). CAPTURE ORDER IS THE LOAD-BEARING FIND (probed from the real
DLLs): `OnPlayerBattleEndEvent` fires inside `PlayerEncounter.DoApplyMapEventResults` BEFORE
`CalculateAndCommitMapEventResults` — so at that moment `MapEventParty.PlunderedGold` /
`GainedRenown` / `GainedInfluence` are calculated but not yet zeroed-by-commit, and the defeated
side's `PrisonRoster`s are still countable as captives-to-free; one dispatcher tick later
`PlayerEncounter.RosterToReceiveLoot*` stand filled (spoils, prisoners taken) and hero fates are
settled (capture runs after the event) — `FinalizeBattleRecord` enriches, re-saves, appends the
chronicle line, records the beats, and shows the one soft notice. `MapEventEnded` is only the
dedupe'd fallback. PER-HERO DOWNS COME FROM TWO PLACES, each honest only where it stands
(2026.08.08, the "4 bandits but 6 felled" playtest): `CampaignEvents.OnHeroCombatHitEvent` (from
`MapEventParty.OnTroopScoreHit`) fires for fought AND simulated battles, but ITS `isFatal` IS
TRUSTWORTHY ONLY WHEN SIMULATED — in a fought mission `BattleAgentLogic.OnAgentHit` computes it as
`affectedAgent.Health - blow.InflictedDamage < 1f` where `Health` is ALREADY post-damage (proof:
the same class's `OnScoreHit` uses bare `Health < 1f` as its own kill test), so the blow is
subtracted twice and EVERY heavy non-killing hit reports a kill. On the field the tally therefore
comes from `Battles\BattleDownsMissionBehavior` — a `MissionLogic` added in
`SubModule.OnMissionBehaviorInitialize`, reading `OnAgentRemoved`: one mark per soul who actually
falls (Unconscious/Killed only), a mount's blow credited to its rider, our side's hands only
(`Team.IsPlayerTeam || IsPlayerAlly`), gated on `MapEvent.PlayerMapEvent != null` so arenas,
tournaments and practice rings never leak in. The campaign event stands down whenever
`Mission.Current != null`, so the two never double-count. An empty tally beside ≥20 enemy
casualties — or a tally that outruns the foe's real losses (Fallen+Wounded) — records honest
`Downs = -1` ("no tally was kept"). Naval is
first-class on this game version: `MapEvent.IsNavalMapEvent` (`!Position.IsOnLand`), per-side
`ShipCasualties`, `party.Ships` — kind "sea" with its own titles. Casualties come from the per-party
`DiedInBattle`/`WoundedInBattle`/`RoutedInBattle` rosters (NEVER from roster wounded-states — those
include pre-battle wounds). The freshest shared battle rides the SITUATION whole
(`BattleChronicleBlock` → `BattleText.SituationBlock`: older ones as titled roll-lines, the deep
past folded into a count) and `recall_battle` (`Tools\ChronicleTool`) answers any shared battle by
loose name ("the storming of Varcheg", "ortysia", "last"), scoped to what the asker lived — the tool
rides only for souls with ≥1 shared battle. Beside it the situation gained THE BODY's honest state
(`SituationBuilder.BuildBody`/`TheirBody`, Anton's same-day ask): a mending soul knows its strength
in 100 and, past the game's own `IsWounded` threshold, that it is in no state to fight — and sees
the partner's wounds too (never through letters). `_battles` lives inside the campaign folder ON
PURPOSE so the save-scoped snapshots rewind the war with the memories. Config
`EnableBattleChronicle` (default on); DevMode lever "[test — forge a shared battle record]".
TRAINING BATTLES COMPAT (same day): the sibling mod's drills are REAL map events (split-army,
phantom enemy, siege/sea drills) — `IsTrainingBattle` skips them whole (no record, no beats, tally
cleared) by the party-id prefixes `training_opponents` / `training_mock_enemy`, a bare StringId
test with no reference and no reflection. CONTRACT: those prefixes in TrainingBattlesMod's
`TrainingBattleBehavior` must never be renamed without updating this check (noted in BOTH repos'
CLAUDE.md). Anton's rule for drills: NPCs must never mistake one for a real battle — so nothing at
all is written; a "we drilled today" note is a possible future garnish, deliberately not built.

**The road journal (2026.08.08, same day) — the everyday life, witnessed.** Beside the chronicle's
thunder, the small weather: souls riding IN the player's party (only them — the party is the
witness, a tavern-keeper never saw the road; `JourneyBlock` gates on `PartyBelongedTo == MainParty`)
carry in their situation the company's recent doings. Core `Journey\` (`JourneyLog` + `JourneyText`,
unit-tested): VISITS — the last ~12 stops kept, ~5 told, the freshest as a short detailed paragraph
and the older ones one line each ("In the village of Odrimir (Spring 3, we stayed half a day):
bought for 250 denars."), each stop carrying trade values + chief pieces ("Wool ×24"), recruits,
garrison drops, captives sold/donated; a "road" bucket catches caravan trades between stops (empty
road buckets are noise and never told). TASKS — taken quests stand with giver, taken-date and "N
days given, about M remain", and settle into "Lately settled" with the game's OWN reason
(`QuestCompleteDetails`: Success/Fail/Timeout/Cancel/FailWithBetrayal → "failed — the time ran out
on us", "failed — and by our own broken word at that", "set aside"). Module
`ImmersiveChatBehavior.Journey.cs`: nine campaign events (SettlementEntered/OnSettlementLeft,
PlayerInventoryExchange — the tuple's int IS the denars that changed hands, and `isTrading:false`
exchanges are battle loot/discards, never journaled — OnTroopRecruited, OnTroopGivenToSettlement,
OnPrisonerSold/OnPrisonerDonatedToSettlement, OnQuestStarted/Completed), persisted per mutation to
`_journey.json` (atomic, campaign-scoped → snapshots rewind the road). Config `EnableJourneyLog`
(default on). No LLM calls anywhere — the journal is free. RE-ENTRY RULE (the Onira bug, first
playtest): a save loaded inside a town re-fires SettlementEntered at the instant of the recorded
leave — `BeginVisit` therefore RESUMES a same-place stay within `ContinuedStayGapDays` (0.5d) and
`LoadFrom` heals already-split files via `MergeContinuedStays`, else an empty twin visit steals the
"latest stop, detailed" slot. JOURNEY BEATS (same day, Anton: "как битките — и аз да ги виждам в
чата"): closing a stop WITH doings, taking a task, and settling one each set a SILENT first-person
beat (`JourneyText.StopBeat`/`TaskTakenBeat`/`TaskSettledBeat`, markers `StopBeatMark`/`TaskBeatMark`
— never reword) into every party hero's memory via `RecordStopBeats` (flag `JourneyVisit.BeatDone`,
one beat per stay even across resumes; scan-based, so pre-feature stops backfill once) —
`OutreachMark.None`, pass-through stops stay situation-only to keep the memory stream lean. The
stop beat is DETAILED on purpose (Anton: the freshest stop must offer a conversation-opening
detail — goods by name, men by kind — without a dig through deeper memory); only the situation
roll of OLDER stops compresses to one-liners — time, not compression, moves a beat into the past.

**The wedding chronicle (2026.08.09) — the cherry on the cake.** The day the courtship road was
built for. When the player weds ANYONE, the chronicler writes it in TWO PARTS, in the register of
Scripture and in the tongue the couple actually speak. **THE DAY** — a third-person account in the
manner of the old wedding narratives (Isaac and Rebekah, Ruth, Cana): the place, the hour and season,
the witnesses BY NAME with what each did, the road that led here (the promise, the waiting, the kin's
blessing and its price, and the misgivings she once wrote and how each came to rest). **THE NIGHT** —
the Song of Songs' own register, in the wedded soul's FIRST PERSON: reverent, image-laden, and
holding both halves of one rule, which is the whole ask (Anton: "не вулгарно, но не и да не казва
детайли") — *nothing coarse, nothing clinical* AND *nothing coy, no closing of the door*; Scripture
neither leers nor looks away, it says plainly that he knew her and says the rest in images. Both
prompt bodies are guarded by tests; change them only with fresh live samples.
THE HOOK is `CampaignEvents.BeforeHeroesMarried` (decompile-verified 2026.08.09), NOT our own seal —
so a wedding arranged through vanilla's barter is chronicled too. It fires inside
`MarriageAction.ApplyInternal` with the spouses already set but BEFORE the clan change, and that
order is load-bearing: one heartbeat later `HandleClanChangeAfterMarriageForHero` →
`MakeHeroFugitiveAction` has swept a noble bride out of her settlement and her party, so the place
and the witnesses are captured INSIDE the handler, synchronously. TWO CALLS, not one (each register
is its own prompt, and two shorter answers sit safely inside the clients' 90s wall), on a third
client shell `_storyClient` at `MaxMemoryWriteTokens` — the spoken 400-token cap would sever a
wedding mid-sentence, sooner still in Cyrillic. THE PRIVACY RULE IS CODE, not prompt wording: the day
is beat into the spouse (`PlayerEngaged`) AND every witness (`OutreachMark.None` — witnessing is not
the player engaging them), the night into the spouse ALONE, and `NuptialTool` refuses it to any other
asker (`WeddingText.FullAccount(record, includeNight:)`). Witnesses are the player's own company
first, then the souls of the settlement who are not behind the keep's closed doors, capped at 12.
Both parts ride her verbatim memory as silent beats (so she keeps the day in full until she folds it
into her summary HER way — three consecutive silent beats all survive: `AppendRememberedTurns`
accumulates `pending`), while `_weddings` keeps them whole forever and `recall_wedding` calls the day
back by any loose name ("our wedding", "that day in Onira"). The chat window draws both as ❦ cards in
the road's rose. A day whose call FAILED (429, timeout, a refusal) is saved account-less and retried
on the hourly tick up to 3× a session — the guard is CONTENT-aware (`IsUnwritten`), never
existence-aware, or one bad minute would blank the wedding forever; beats for a soul whose own
exchange is in flight are parked and folded by `SaveMemory` (the `_pendingBlessingFolds` discipline).
Config `EnableWeddingChronicle` (default on); DevMode lever in the chat window's Dev panel ("Write
your wedding day anew"). Decision record + the review round's eight fixes:
docs/wedding-chronicle-design.md.

**The birth chronicle (2026.08.10) — the next day of a life.** The wedding's own shape, turned on a
cradle, and reusing its machinery down to the guest list (Anton's ask: "може да се преизползва много
от същата логика"). Every child born to the player is written in TWO PARTS, and here they are split
IN TIME as well as in register. **THE HOUR** — the mother's own first person, written the moment the
child comes, in the register of Scripture's own birth narratives (Rachel, Hannah, Elizabeth, the
stable): her hour came upon her, the women about her, FEAR NOT, the first cry, the child laid at her
breast, and she called his name. It carries the nights' two-halves rule wearing different clothes —
half the hour itself, half the child and the naming — and both halves of the register rule: *nothing
clinical*, no anatomy and no physician's word, AND *nothing coy*, "do not skip past the pain and the
fear to arrive at a clean, tidy joy". **THE FEAST** — the wedding day's register, third person,
written only if one is bought. **THE PRIVACY LINE RUNS BETWEEN PARENTS AND WITNESSES, and it is code:**
the hour reaches the mother's memory alone; the father gets the FACT and his own presence or absence
but NEVER her first person (`BirthText.FatherBeat` carries no account body — planting her private "I"
in his memory would be the small lie this mod exists not to tell); `CradleTool` hands a father the
hour framed as *what she told him of it*, and refuses it to a witness outright. THE FEAST MAY BE
BOUGHT DAYS LATER: a father away at war is asked when he next rides in (`AwaitingFeastOffer`, a
30-day window, one question an hour, and a refusal is remembered so he is not asked again every hour
he stands beside her) — so `SealTheDay` is what lays the day down once, into births.txt and before
the player, whichever part completes it. A child who did not live is never sent to the chronicler,
never feasted and never announced gladly: one mark, written by hand. THE HOOK is
`CampaignEvents.OnGivenBirthEvent` and three decompiled facts made it safe: **the subscribe method is
`AddNonSerializedListener`** — there is NO `AddListener` on `IMbEvent` in this version; **vanilla
rolls a 1.5% death-in-labour for the mother immediately AFTER the event**, so every fact is captured
synchronously (the wedding's clan-change lesson in a darker coat); and **this version never asks the
player to name the newborn** (byte-verified — the "Naming Newborn" string is dead content), so the
feast popup collides with nothing. The ladder is the WEDDING'S OWN, denar for denar (100 → 500 000),
paying about a third of its renown, because a child is joy and not an alliance and children are far
easier to come by than weddings. Config `EnableBirthChronicle` (default on); DevMode lever
"[test — write a child's day anew]". WHERE THE PLAYER FINDS IT (Anton's ask, 2026.08.10 — "историите
за децата може би да се пазят в H бутона, и като го цъкна да виждам раждането после историята"): the
children's own front door is **the hearth window (H)**, where `BirthEntriesFor` draws them at the TOP
of a wife's page, above the fortnight of nights — the hour first, then the feast, in the cradle's own
gold. It needed no prefab: the window already draws that card shape for the nights. Beside it the
chat window draws the beats as ❧ cards in the thread, and the couple's road-page button keeps the
wedding day with the children appended (label deliberately left at "Our wedding day" so it still fits
its widget; a couple with children and no wedding gets an "Our children" page of its own, with its
own hover text — a hardcoded wedding hint over a children page was a review find).

**The nights of a marriage (2026.08.09) — the wedding's morning after.** The game used to flip a
coin every day behind the player's back; now the nights are his to spend. Each evening (hour
`NightHour`, 21) he is asked where he will sleep; a woman's own month (`MoodTides.Fertility`, a curve
around the crest, ZERO through the days of the custom — her door is greyed shut and no popup fires
at all when every door is closed) decides what may come of it; and a night he pays for is written in
3–5 sentences with a NAME. FOUR RULES: conception is the player's doing (`Nights\PregnancyPatch`
prefixes the private `PregnancyCampaignBehavior.RefreshSpouseVisit` and skips ONLY women wed to the
player — fail-open, and then we deliberately do not roll either); THE ODDS ARE THE GAME'S OWN, only
spread (`V × L × f / MoodTides.FertileWindowSum`, so taking her whole window ≈ vanilla's month —
unit-tested); her door is hers; and she SEES what she would see while nothing ever scripts the
feeling (`move_heart` has been hers since day one). THREE DECOMPILE-VERIFIED TRAPS, all handled and
all silent killers: `GetDailyChanceOfPregnancyForHero` **NREs on a null Spouse** (it reads
`hero.Spouse.GetPerkValue` unchecked, and Marry Anyone empties that slot daily) → a mirrored formula
stands behind the model call; `MakePregnantAction` **fathers the child on whatever is in
`mother.Spouse` at that instant** → `EnsureFatherSlot` sets it first, or a second wife's child is
fathered by null and crashes at the delivery 36 days later; and vanilla **announces the pregnancy AT
conception** → the "she comes to know" delay (`ConceptionRevealDelayDays`, 7) is achieved by
deferring `Apply` itself, so the birth moves with it. **THE COIN BUYS THE LENGTH AND THE SHAPE OF THE
WRITING TOO (2026.08.10, Anton's ask):** `NightGifts.Tier` carries `MinSentences`/`MaxSentences` — 3-4
at ten denars, 5-6 at a hundred, 6-7 at three, 7-8 at a thousand, the ceiling deliberately under the
wedding night's twelve — and `BuildStoryPrompt` now asks for the account in TWO NAMED HALVES: the
coming to it and the surprise of it, then the knowing itself, "and this half is NOT a door politely
closed. Stay in the room with them." The second half names what a body does (breath caught, the
trembling like a small bird startled in the brush, the whisper, the shiver, her own wanting) and then
in the same breath forbids reusing any of it as WORDING — named images come back verbatim, the same
lesson that cut `ChroniclerNote` back to bare nouns — AND THE PROOF CAME BACK THE SAME NIGHT: probed
live on luna, the one image the prompt named (the bird startled in the brush) turned up in two
consecutive nights word for word. Hence **`NightText.ImageDeck` + `DrawImages(seed)`**: ~18 of
Scripture's own images (the sealed spring, myrrh on the handles of the lock, honey under the tongue,
the young hart on the mountains of spices…), three DEALT per night, stably by FNV-1a from the night
record's `Id` so a retry an hour later reaches for the same ones. Each card is its own hash — the
first cut walked a fixed stride from one hashed start, which gave eighteen possible hands instead of
eight hundred, and the unit test caught it immediately. Keep the deck long; that is the whole
defence. TWO CAPS HAD TO MOVE WITH IT and neither is
decorative: `TryParseStory`'s flat 1600-character cut became `AccountCharBudget(maxSentences)` (eight
rich Cyrillic sentences run past 2000, so the tier that exists to be longest would have been silently
shortened back), and `BuildRoll` gained `DefaultFullAccountBudget` (2600) — the roll rides WHOLE in
her sheet on EVERY reply, so five grand nights told in full would have cost ~4k tokens per exchange
for a fortnight; now the freshest are told whole until the room runs out. AND WHICH ONES, reworked 2026.08.11 (Anton: the roll chose by RECENCY alone, so the night a child was begun on scrolled out of her sheet in four days while two ordinary evenings sat there in full): TWO are privileged and always survive — the freshest written night, and the MOST SPECIAL one however old (`NightText.Specialness`: a conception outranks any purse, and beneath it the gift's own price IS the ranking). The rest are added best-first while the room lasts. Beside that, RUNS OF LIKE NIGHTS gather into one line (`RunLine`) — "from nine nights ago to last night he came to me nearly every night" — at a threshold of THREE, never two, because a pair is still two evenings and each keeps its own nuance (whether she saw him go or only heard of it); a written night is never swallowed by a run, and "nearly every night" is claimed only when the run truly covered its own span.
Also wired at last: `NightLedger.AwaitingBeats()`, written for this and never called, so a paid night
whose three story attempts all failed no longer leaves her memory blank. The gift (`NightGifts`) buys
three things — odds, a written memory, and TALK: its `AwarenessMultiplier` scales the other wives' chance of
hearing (×0.5 plain → ×2 for the jewel) and a leaked night **leaks its NAME too**
(`OtherNightTitle`, back-filled by `LeakTheNameOfTheNight` once the chronicler answers). A paid night
also costs the morning (`SetDisorganized`). TWO plain switches, not four poetic modes (Anton killed the cycling
"Change how the evenings go" button the day it shipped): `NightsAutoVisit` (manual = asked at dusk +
the window at any hour, the only way to gifts and written nights; auto = it goes on its own once the
hours are up, LATE IN THE EVENING and never earlier, so the whole day between stays the player's —
a floor under the marriage, not a ceiling) and `NightsPreventChild` (on auto it also picks whoever
rather than whoever is nearest her season, and either way cuts the night to a tenth). No nights at
all is `EnableNights = false`. An ignored dusk question is settled a day later as a night alone (`LastSettledNight`, a NIGHT-level mark because a night nobody noticed
writes no records). Memory keeps TITLES, never paragraphs — the flesh lives in the 14-night ledger
and `nights.txt`. **THE LINE** (Core `Together\TogetherLine`, folded in LAST by `SituationBuilder`, and drawn INLINE in the
chat thread for the player too — at the moment itself, never at the foot, or "from this moment"
means nothing; night beats carry only a name, so the thread fills the freshest 3 accounts from the
ledger under a ☾ card): ONE mark at the last moment the two of them had
time to themselves, then a plain dated list of everything since — nights, battles, markets, in
order. Without it a soul reads the sheet as settled background and greets him the morning after a
night elsewhere as though it had been had out. WHAT MOVES IT IS TIME ALONE and nothing else: a
talk that has ENDED, a night together, the wedding night — never a battle, a market, or hearing
where he slept. A RUNNING TALK MUST NOT MOVE IT (the sheet is rebuilt every reply, so her own first
answer would erase the thing she was raising): the talk side reads `NpcMemory.LastTalkEndedDay`,
stamped at `ConversationEnded` and at chat-window teardown, with an 8-hour grace as the fallback.
It disappears by itself when nothing stands after it — no flags. The nights roll stops AT the line
so nothing is told twice. It is ONE divider and the list, nothing else — cut down three times on
purpose (the opening mark is redundant beside the turns' own `[place, time]` stamps and the entries'
dates; the closing sentence told her how to USE the list, so it went too, to see how much a soul
works out unaided). The words carrying the old closing's work are **"a private
discussion"**: they leave the door open that light passing remarks were made while saying plainly the
two never sat down to any of it — which is why the flat "we have not spoken of this" had to go, it
read as estrangement. And **"from this moment"** anchors the divider to its own place: the block
stands BEFORE the transcript, so a backward-looking "since then" had nothing yet to point at. REFUSALS WERE
PROPOSED AND CUT (Anton: "като е жена да приемем че винаги иска и е готова и от мене зависи") — the
only refusal is the custom days; do not reintroduce her saying no without asking. Same-sex is not
modelled and a female player is only kept from crashing (`MotherOf`), his explicit call. Window:
`UI\NightWindow\`, hotkey **H** (I and P are vanilla Inventory/Party). Toggle `EnableNights`;
DevMode lever in the chat window's Dev panel ("Spend a night with them now"). Full record:
docs/nights-and-conception-design.md.

## Work flow for the TASKs
- Get the taks you work on from TASKS_TODO.md
- When dove move it to the end of TASKS_DONE.md, rename it if it changed or is badly formatted and add a done ts at the end (YYYY.MM.DD HH.MM.SS)
- **Every player-visible change also gets a one-liner in CHANGELOG.md under [Unreleased]** —
  written for players (no file names, no internals). At release time that section is retitled to
  the version + date and becomes the Workshop ChangeNotes / Nexus changelog verbatim, so the
  change notes are already written when it is time to ship (see tools/WORKSHOP-UPLOAD.md step 2).
- When done with changed and tested them, recompile so the mod is rebuild automaticaly in C:\Users\Trax\Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI - dont ask the user to rebuild

## Conventions

- Match the surrounding code style; keep comments about *constraints/intent*, not narration.
- End git commit messages with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- The user commits from GitHub Desktop too — write descriptive commit messages, expect a
  shared history. Closing VS Code / Explorer windows on the repo may be needed before folder
  renames on Windows.
- `<GameFolder>` currently: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`.
