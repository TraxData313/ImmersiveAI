# The One Screen & the Prompt Pack — the 2026.08.14 rework plan

Anton's big-batch directive, planned in one sitting so no future session starts cold. This is
the standing plan: read it, check the status log at the bottom, continue from there.

## What Anton asked for (2026.08.14, his words compressed)

1. **One window, not two.** Merge the Chat "O" and Letter "Y" windows. When the chosen soul is
   away, the send button becomes **Seal** (a letter); letters live inside the same message
   thread, marked as letters. No separate correspondence view.
2. **Scroll up = the prompt.** Kill the deep-memory panel and every "reveal the prompt" button.
   The thread shows chat history at the bottom as normal; scrolling UP walks through **everything
   the NPC receives, in the exact order they receive it** — sheet, deep memory, situation, tool
   descriptions, then the turns. For ALL players, not DevMode. "I want good visibility with just
   scrolling up."
3. **All prompt text in one editable file.** Every piece of text an NPC ever reads, in one
   `# commented`, `key = "value"` config file, loaded at runtime with safe fallbacks — so Anton
   can change any word himself, no recompile, no asking.
4. **The Talk-style screen.** Not a small centered widget: a vanilla-map-Talk-like experience —
   the NPC standing live in the center with a real place-appropriate backdrop — with our NPC
   list on the left and our thread + write box on the right. His annotated screenshot
   (2026.08.14): NPC list left, live NPC center, "chat hist" right-top, "write" right-bottom,
   vanilla dialog box crossed out. Away NPCs render the same way for now (a letter-desk variant
   maybe later).

## Decisions locked (2026.08.14)

- **Rendering approach: host the map-conversation tableau ourselves** (option B of the spike).
  Vanilla map-Talk is NOT a mission — it is a render-to-texture tableau, public API end to end.
  See docs/talk-screen-research.md for the full recipe. Piggybacking the real conversation
  (OverrideView on MapConversationView) was rejected: we would own every vanilla map dialog.
- **"Speak freely" unifies on the map** (Anton's pick): from a MAP conversation, choosing
  "Speak freely with me." closes the vanilla dialog and opens the new screen on that soul.
  Inside town/village 3D scenes the old conversation-panel loop STAYS (a map-layer screen
  cannot cover a scene you are standing in). Reach-out notices, letter arrivals, settlement
  menu options, hotkeys — all open the new screen.
- **One prompts file** (Anton's pick): `Configs\ImmersiveAI\prompts.txt`, ~530 keys, TOC at
  top, loud section banners. Not split.
- **Away NPCs render in their OWN locale's backdrop** — the tableau takes atmosphere facts, so
  a wife in Marunath shows against Marunath's air, not yours. (Verify null-party heroes in
  build; ConversationCharacterData's party is optional per the research.)
- **Enter rules unchanged in spirit:** Enter sends when the contact is (here); for (away) only
  the Seal button sends — a letter deserves a deliberate seal. Shift+Enter thinks, as shipped.
- **Hotkeys:** O opens the screen. Y becomes an alias for the same screen (muscle memory,
  zero cost); `LetterWindowHotkey`/`EnableLetterWindow` keys stay in config for compat,
  the letter window itself retires.
- **Contact list = union:** co-located "(here)", known-away "(away)", dead correspondents
  "(gone)" read-only. Search line stays. Here-first, then by last-spoken.
- **Thread = memory + courier bag:** recorded turns (letter beats already render as ✉ cards,
  both eras), in-flight letters from LetterBag as a sealed card ("rides toward her, ~N days"),
  THE LINE inline, ☾/❦/❧ cards as today. letters.txt keeps being written (players like the
  artifact); CorrespondenceLog's PARSE retires from UI with the letter window.
- **Composer:** same input both modes; when (away) the draft mirror grows tall (letters run
  long) and the button reads "Seal and send".
- **One letter on the road per bond, EITHER direction** (Anton, 2026.08.14 "go" message): while
  any letter between the player and this soul is in flight — theirs or ours — Seal is grayed
  with the reason ("a letter is already on the road between you"), and the NPC's spontaneous
  hourly writes skip the bond too. The one exemption: an NPC's single reply to a just-delivered
  player letter queues even if the player managed to seal a new one during the composing gap —
  blocking there would silently eat her one answer. Turn-based correspondence falls out
  naturally: her letter arrives → road is clear → you may write.
- **Fallback insurance:** the current chat-window widget code stays alive behind
  `UseClassicChatWindow` (config, default false) and an automatic session fallback if tableau
  init throws (game-patch insurance — same class of risk as the MapNotificationItem.xml
  override). Retire the classic shell only after the screen survives a few releases.
- **Scrollback prompt is for everyone.** Removes: the deep-memory overview panel, the chat
  window "reveal" affordances, and the face-to-face "Reveal the whole of your mind" option.
  The Dev panel gains "write full prompt snapshot to file" (bug reports still need a file).
- **Marks stay in code.** ~25 recognizer/mark constants (letter markers, chronicle beat marks,
  SUMMARY:/SELF: labels, ponder-note prefixes, legacy Angel fragments) are NOT in the prompts
  file — documented there as `# LOCKED (in code)` notes only. Templates whose fragments double
  as recognizers get accumulate-recognition: current ∪ shipped-default ∪ legacy.

## Research records

- **docs/talk-screen-research.md** — the tableau chain, hostability recipe, liveness, risks.
  Distillation: `MapConversationTableau` (SandBox.View, public) draws a cached prebuilt scene
  `scn_conversation_tableau` + AgentVisuals from pure CharacterObject data into a TextureWidget;
  backdrop = atmosphere name from terrain/culture/time/weather; only the PARTNER renders (the
  camera is the player). Host recipe: our movie includes `<MapConversationTableauWidget
  Data="@TableauData"/>`; plant `MapScreen.Instance.GetMapView<MapConversationView>()
  .ConversationMission = new MapConversationView.MapConversationMission()` (public field,
  public nested class — the tableau's only external coupling) before setting
  `TableauData = MapConversationTableauData.CreateFrom(playerCD, new
  ConversationCharacterData(hero.CharacterObject, party), terrain, timeOfDay, snow, settlement,
  locationId, rain, snowing)`; clear on close. Gestures: call the public
  `tableau.OnConversationPlay(...)` ourselves with ids from the public `ConversationAnims`
  dict (vanilla parses `[ib:]/[if:]/[rb:]/[rf:]` tokens). Idle/breathing/blinking are free.
  Zero co-location dependency. Switching = `SetData` respawn, sub-second.
  Top risks: (1) stub discipline — plant before data, clear on close, hard-close while
  `MapState.MapConversationActive` or missions run, else NRE/scene contention with real Talk;
  (2) version facts — scene name, entity tags, the ConversationMission coupling; feature-flag
  with fallback; (3) lifetime corners — widget finalize with never-set Data NREs, gate
  `OnConversationPlay` on `ConversationTableau != null` + try/catch, never animate the
  widget's size.
- **docs/prompt-text-inventory.md** — every LLM-visible string, grouped, flagged. Totals:
  ~530 externalizable keys / ~1,160 strings / ~130 KB raw text; full defaults file lands
  ~160 KB, ~3,000 lines. The 10 trickiest items are listed there (letter markers, SUMMARY
  contract, 11 chronicle beat marks, ponder-parse coupling, misgivings tool schema, NightText
  image deck hash-dealing, sellsword floor-never-leaks interpolations, ForgeTitle persistence,
  order-is-identity palettes).

## Phase 1 — THE SCREEN (shell + the merge)

Deliverable: the new Talk-style screen replaces both windows; Anton playtests it.

1. **VMs first, shell-agnostic.** Unify ChatWindowVM + LetterWindowVM into `UI\TalkScreen\`
   (TalkScreenVM + contact/message VMs, TalkScreenManager, ConversationTableauController).
   Carries over: search, drafts (survive closing), unread marks, bond line, "Misgivings n/m",
   info overlay "?", both prompt editors, the Dev panel, scroll-to-bottom, one-in-flight rails,
   MarkMetInWorldsEyes, socialness untouched.
2. **Shell.** New movie `ImmersiveTalkScreen.xml`: list left, `MapConversationTableauWidget`
   center, thread + composer right. Tableau controller per the research recipe (stub plant,
   data build from hero + HER locale atmosphere, gesture nudge on send/receive, teardown rails,
   auto-fallback to classic widget on any throw).
3. **Wiring.** Hotkeys O and Y → the screen. Settlement menu: collapse "Speak with those near
   you" + "Send a letter by courier" into one entry opening the screen. Notice/letter-arrival
   clicks → the screen on that thread. Reach-out first words land there. Map-conversation
   "Speak freely" → close dialog, OpenWhenClear → the screen (in-scene missions keep the old
   panel loop). Letter queueing through the same QueueLetter road (one courier per bond,
   co-located souls get Send not Seal, dead souls read-only).
4. **Retire.** Letter window (VMs, manager, prefab) goes; classic chat window stays as the
   flagged fallback only. CHANGELOG pills.

## Phase 2 — THE SCROLLBACK PROMPT

Deliverable: scrolling up from the thread walks the exact next-message prompt.

1. **Preview builder.** Reuse the inspector path (BuildMessages) to assemble the NEXT-message
   prompt for the selected soul: spoken shape when (here), letter-compose shape when (away).
   No LLM call; rebuilt on select/open and after each exchange. Game-thread data only.
2. **Rendering.** Above the oldest turn, in receive-order: the system sheet split into cards at
   its own first-person headers, then "the hands I may reach with" (each riding tool's name +
   description + parameter descriptions as the API carries them), then the recorded turns as
   today; the composer is visibly "the next user message". Exact text, no paraphrase; sectioned
   cards for Gauntlet perf (one giant RichText will choke).
3. **Removals.** Deep-memory panel, reveal buttons (window + face-to-face menu). Dev panel
   gains the snapshot-to-file entry.

## Phase 3 — THE PROMPT PACK (prompts.txt)

Deliverable: every editable NPC-visible string external, hot-reloaded; Anton edits alone.

1. **Core `PromptPack`** — DONE 2026.08.14 (`src/ImmersiveAI.Core/Prompts/PromptPack.cs`, 18 tests).
   Format: `key = "one-liner"` / `key = """block"""`, `#` and `//` comments, `{slot}` placeholders
   documented per key by `RenderEntry`. Laws it already enforces, all tested: a missing or broken
   key never costs more than itself (the compiled-in default stands); an unclosed block keeps its
   text; a deliberately EMPTIED key stays empty (that is how a player switches guidance off, so it
   must not fall back); keys are case-blind; unknown keys are kept aside, never dropped; render →
   parse is a clean round trip; multi-line text is always fenced, never `\n`-escaped.
   Key scheme per the inventory: `sheet.*`, `situation.*`, `beat.*`, `memory.*`, `chronicle.*`,
   `tool.*`, `courtship.*`, `utility.*`, `palette.*`, `tiers.*`.

   STILL TO BUILD around it: the **registry** (key → default + comment + slots + warning). Core and
   Module each declare their own entries; the Module merges the two at load (Core cannot see Module)
   and `PromptFiles` owns reading/writing `Configs\ImmersiveAI\prompts.txt` — appending only new
   keys, never rewriting a line the player has touched, re-read per context build.
2. **The sweep.** Mechanical conversion bucket by bucket (inventory buckets A–F), fan-out-able
   to worktree subagents per bucket. C# interpolations become named-slot templates. Marks and
   parse-labels stay code. Recognizer-coupled templates get accumulate-recognition.
   Order-is-identity palettes (speech styles, humors, image deck) ship with per-key warnings:
   reword freely, never reorder/insert mid-list.
3. **Tests + probe.** PromptPack parser round-trip; defaults-registry completeness; the
   existing prompt-guard tests keep guarding the DEFAULTS. Then a live probe of at least one
   tool contract and one chronicle prompt on the real backend (the weigh_misgivings lesson:
   probe before trusting).

## Rails & risks

- Phase order 1 → 2 → 3. Phase 2 rides the new screen but works in the classic fallback too
  (it is thread content). Phase 3 is UI-independent but touches the same text-bearing files —
  do not run it concurrently with 1/2 edits to PromptBuilder.
- `dotnet test` green after every Core touch; rebuild + deploy.ps1 at each phase end so Anton
  just opens the game (his standing rule).
- The nights/births/memory playtest list in TASKS_TODO.md is STILL OPEN — this rework does not
  touch those systems' logic, but land it in a separate commit train so a playtest bug bisects
  cleanly.
- Never let the new screen's input rails swallow the map: HitTest lessons from Socialness, the
  encyclopedia/typing gates (MapOverlays) must guard the new hotkeys identically.
- The tableau seam is version-sensitive: keep the classic fallback until proven, and log any
  tableau init failure loudly enough to diagnose from a player report.

## Status log

- 2026.08.14 — Plan agreed with Anton (screen approach B, unify-on-map, one prompts file).
  Research docs landed. Phases not started.
- 2026.08.14 — **Phase 1 + Phase 2 BUILT and deployed, unplaytested.** 466 tests green.
  - New `UI\TalkScreen\` (TalkScreenVM/TalkContactVM/TalkScreenManager + ConversationTableauController
    + ConversationSceneBuilder) and `module\GUI\Prefabs\ImmersiveTalkScreen.xml`.
  - `UI\TalkUI.cs` — the façade every call site now goes through, so retiring the two old windows
    later is a ONE-FILE change. Old windows kept whole behind `UseClassicChatWindow` (default false)
    plus an automatic session fallback if the screen ever fails to rise.
  - `ImmersiveChatBehavior.Talk.cs` — `ContactsForTalk` (one circle: here / away / gone),
    `ReachOf` (Spoken / Letter / Closed), and the Phase-2 `PromptPreviewFor`.
  - The in-flight letter rule Anton asked for on 2026.08.14 turned out to be ALREADY BUILT:
    `LetterBag.HasInFlightWith` is direction-blind and already gates both `CanWriteTo` (the player's
    seal) and the NPCs' hourly writes. The screen just surfaces it now (grayed Seal + the reason).
  - Phase 2 landed with Phase 1: the scrollback is the sheet (split at its own paragraph breaks) +
    the real tool list from `GatherSpokenTools` — factored OUT of `CompleteSpokenAsync` so the
    preview and the real call can never drift. No LLM call, no recording.
  - Tableau facts verified against the live DLLs (spike C, 9 corrections to the first research —
    notably `CampaignMission.Current` MUST be nulled after planting the stub or army management
    breaks, and the layer must come down BEFORE the scene is released).
  - NOT done: gestures (idle/breathing only — the gesture call needs a reflected speaker plant, and
    it was not worth the only reflection on the path for v1); Phase 3.
- 2026.08.14 — **First playtest (Anton) + a review pass; both fed one round of fixes.**
  It WORKS: the tableau raises real souls in real places, the thread reads well, letters are fine.
  What came back, and what it turned out to be:
  - *"it opens the old chat window in the middle"* — the old managers poll the SAME keys from the
    same global input, and were still being ticked beside the screen, so one press of O opened both,
    stacked at the same layer order. `TalkUI.Tick` now only listens through the shape in use, and all
    three old managers' `CanOpenNow` yield to an open screen.
  - *"when I click on someone else it squeezes him"* — THE INSTRUCTIVE ONE. The tableau renders
    through a camera whose shape follows its widget, and vanilla only ever hands it the WHOLE SCREEN
    (`Modules\SandBox\GUI\Prefabs\Map\MapConversation.xml`). Penned into our narrow middle column it
    drew the same picture into a different shape — and re-measured differently after each `SetData`,
    which is why switching away and back changed it. It is full-screen now, with the two panels lying
    on top, which is Anton's original sketch anyway. RULE: never give this widget anything but the
    full screen.
  - *"the buttons up top right dont work"* — they shared a parent with the pane below them, which is
    stretched to fill and therefore lay over them like glass: perfectly visible, completely dead.
    Later siblings win the mouse, so the bar now rides last.
  - *"not render the guys in the back if the NPC is in my own party"* — done; `party: null` is the
    only thing that suppresses them (no other flag is read). Own company only; a stranger keeps their
    following, since that is part of who they are.
  - From the review, all fixed: selecting a "(gone)" correspondent CRASHED (`Show(null)` unplanted
    the mission while the widget still held data — `Hide()` is now distinct from `Clear()`);
    `OpenWhenClear` spent all 40 retries inside ONE frame (the dispatcher drains what the drain adds,
    so a self-requeueing retry is a next-tick try in disguise — it is tick-counted now, as the letter
    window learned to do in 2026.07.15) which had silently killed the new "Speak freely" door; the
    face latched off forever on a merely transient build failure; the screen did not yield when the
    game's own map conversation started; an away soul with a courier already riding was labelled
    "Send (Enter)"; a dropped selection left the previous soul's thread and face standing; the movie
    was never explicitly released (it holds the SHARED scene); and a failed open stamped "talk ended"
    for someone never spoken to.
- 2026.08.14 — **GESTURES built, then cut down within the hour** (Anton asked for them: "като ѝ
  напиша нещо… тя да си сменя позите, да реагира" — then saw the result: "странно изглежда,
  overdoing it… просто както преди… тя просто заемаше малко по-различна поза като натисна Enter").
  Three goes, and the third is right because the GAME'S OWN CODE settles it. `OnConversationPlay`
  branches: a non-empty `reactionId` plays a one-off GESTURE; an empty one with a set `idleActionId`
  plays `IdleAnimStart` — they simply TAKE THAT STANCE. Cut 1 mapped feelings onto gestures (stance
  on select + listening pose + a reaction keyed to her heart's shift) → "странно изглежда, overdoing
  it". Cut 2 kept one small gesture → "правят едно и също движение… не съм го виждал преди". What
  vanilla actually does, and what Anton remembered, is neither: **the pose changes** — "веднъж стои с
  ръка на кръста, веднъж нещо друго". A hand on the hip is the `hip` IDLE, not a reaction.
  So: reaction always EMPTY, and a DIFFERENT idle each time the player speaks. Relation-banded set,
  one step along it per message, each soul starting at their own offset (FNV of id) so companions are
  not mirrors. Note `DoesActionContinueWithCurrentAction` — the same idle twice does nothing visible,
  which is exactly why cut 2's stable stance looked frozen and its gesture did all the moving.
  **The lesson beyond the feature**: the words already carry the feeling; the body saying it too is
  one telling too many.
  The single piece of reflection in the feature is `ConversationManager._speakerAgent` and it cannot
  be avoided — `OnConversationPlay` keeps its whole animation body behind
  `SpeakerAgent.Character.IsPlayerCharacter`, and hosting the tableau leaves that null, so a
  try/catch would have hidden the throw and the gestures would have silently never fired.
  (`ConversationAgents` is safe: the backing list is initialised at its declaration, never null.)
- NEXT: Anton's second playtest, then Phase 3 (prompts.txt).
