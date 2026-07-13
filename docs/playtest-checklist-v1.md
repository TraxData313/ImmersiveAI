# V1 playtest checklist (2026.07.12)

Everything that needs Anton's hands in-game before the Workshop upload, gathered from
TASKS_TODO. Each item says what to do and what "pass" looks like. Tick as you go; anything
that fails, just note what happened and Claude picks it up from there.

Setup for testing: `DevMode: true` in config.json (the test levers live behind it).
Remember it ships **false** by default — a fresh install is already correct, nothing to change.

---

## 0. Fresh-install smoke test (the new subscriber's first five minutes)

- [x] Rename `Configs\ImmersiveAI` away entirely (keep it — it's your real data), boot the game,
      enter a campaign.
      *Pass:* the folder is recreated (config.json, NPCs\_README.txt, global_prompt.txt),
      the first-run no-key guide shows once and points at config.json plainly, and the startup
      health check reports the missing key in friendly words — no errors, no mute mystery.
      Then restore your real folder.

## 1. The roles wave (crafts, duties, field-craft — shipped 2026.07.12, unplaytested)

- [x] **A scout answering "can we escape them?"** — travel with a party that has a scout
      (or ask any lord leading a party on the map) while another party is visible nearby.
      Ask "can we outrun them?" / "what do you see around us?".
      *Pass:* they reach for `survey_surroundings` (activity notice fires), name the nearby
      bands with kind/strength/direction in a rider's words, and say plainly who is swifter —
      no invented armies, counts vague if their Scouting is low.
- [x] **weigh_battle against a castle** — ask a party-leading lord "could we take <castle>?".
      *Pass:* a verdict grounded in real strengths (garrison + militia weighed in), composition
      spoken of honestly, confidence phrased by their Tactics. A tactician sounds sure; a brawler
      hedges.
- [x] **A wife speaking of the children** — ask your spouse about the children / the family.
      *Pass:* children named correctly WITH the right other parent, grown children placed where
      life took them; no ceremony in her greeting (wedded souls skip the beholder distance).
- [x] **A king receiving a tier-0 stranger** — fresh-ish character (renown < 150, standing < 10
      with the crown), speak to a king.
      *Pass:* ONE sentence's worth of "what his eyes see" coloring (plain garb, no banner, no
      word of deeds) and a welcome that fits his nature — cold or curious, but clearly a great
      lord receiving a nobody, not a friend.
- [ ] **A caravan letter arriving as a field report** — have a companion-led caravan out for a
      while (or use the letter test lever on a distant own-clan member).
      *Pass:* the letter reads as a report of their charge — goods, roads, ledgers — not generic
      "I miss you" filler; provenance line correct in letters.txt and the letter window.

## 2. The two windows

- [x] **"?" info overlay in the chat window (O)** — open it, click "?", read it through.
      *Pass:* text is accurate (hotkeys shown match config), Escape folds the overlay first
      (second Escape closes the window), Enter does NOT send while the overlay is open.
      - can you add line that lets people know the NPCs can search the web, and if they are not doing so when the player wants to, explicitly ask them search/research "How does one transfer ships to another party?" will let them know that you want them to check that in the web.
- [x] **"?" info overlay in the letter window (U)** — same drill.
- [ ] **The letter window itself** (built overnight 2026.07.11, still unplaytested end-to-end):
      correspondents all listed (including any dead writer), letter cards render with stamps and
      provenance, courier-on-the-road note shows for an in-flight letter, search line filters,
      bond-stats line shows under the name, "Seal and send" queues a real courier, "Write back"
      on an arrival opens the window preselected, one-courier-per-bond rule holds, and the two
      windows yield to each other (O while U is open, and back).
- [x] **Letter beats in the chat thread** — a correspondence NPC's chat thread shows the
      "✉ by letter" cards between spoken messages; an in-flight compose shows sealed
      ("rides toward you still"), never the body.

## 3. The 2026.07.12 morning batch (cost ledger, log, first-run, key-death)

- [x] **Cost notices per exchange** — with `ShowCostNotices` on, each chat exchange ends in one
      soft "✒ Name — message: in → out tokens, calls, ~$" notice; numbers plausible.
- [x] **No cost notice on sealed flows** — a reach-out desire roll or a letter being composed
      must NOT fire a visible cost notice (log + totals only; a sealed letter stays sealed).
- [ ] **Odds view session/day cost line** — the odds dump ends with the session and day totals.
      - I didn't understand how to test that
- [x] **log.txt filling** — `Configs\ImmersiveAI\log.txt` gathers the session's diagnostics.
- [x] **First-run popup** — delete `first_run_note_shown.txt`, blank the API key, start the game.
      *Pass:* the once-per-install no-key guide shows, points at config.json plainly.
- [ ] **Key-death quieting** — put a wrong key in, play a while.
      *Pass:* ONE amber notice; hourly flows (reach-outs, letters) go quiet instead of erroring
      hourly; fixing the key and a successful call reopens everything.
- [x] **gpt-5.6 answering** (only if switching to OpenAI) — luna default; needs the
      max_completion_tokens shape; a plain exchange and a tool-carrying one both answer.

## 4. The map-party farewell fix

- [x] Click a lord's party on the map → Immersive AI section → Farewell.
      *Pass:* back on the map, NO engage/attack menu.
- [x] Same talk inside a town.
      *Pass:* still inside the gates afterwards — not walked out.

## 5. Uninstall safety — one-line in-game confirmation

Code-verified 2026.07.12 (the engine's load path null-scrubs an unknown notice type;
see docs/steam-page-draft.md), so this is belt-and-braces:

- [ ] Save while a portrait reach-out notice is up → disable the mod → load that save.
      *Pass:* the usual "different modules" warning, then a normal load; the notice is simply
      gone. (If it does NOT load, tell Claude — the Steam page's uninstall note must change.)

## 6. Anthropic key (Anton)

- [x] Set up the Anthropic key and confirm claude-haiku-4-5 (the new default, price-mate of
      gpt-5.4-mini) answers: a plain exchange, a tool-carrying one (recall/heart riding along),
      and a reflection (memory compression) pass.

---

When all boxes are green (or annotated), the remaining release steps are: screenshots + clip,
paste docs/steam-page-draft.md into the Workshop form, tick the AI-content disclosure, and
upload from `tools\package.ps1`'s dist layout.
