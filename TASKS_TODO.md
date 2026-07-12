MUST BE DONE FOR V1 RELEASE:
- [ ] Release-defaults once-over + uninstall safety (the last hands-on pass before upload)
    one pass before upload: DevMode false, sane socialness default, MCM soft-dep verified absent;
    and VERIFY a save carrying a pending portrait notice loads with the mod removed (InformationData
    lives inside saves) — if it breaks the load, the Steam page's uninstall note must say so plainly
    (docs/steam-page-draft.md carries the [VERIFY] placeholder for the result).
- [ ] Pre release:
    - [ ] Steam page assets: 3-4 honest screenshots (chat window, letter window, a reach-out notice, the
        socialness stepper), a 30s clip of a real conversation, and the "clean-room, no ChatAi code"
        provenance note stated plainly. - I, Anton, will make them and will let you know whem I have them
    - [ ] Finalize the Workshop description from docs/steam-page-draft.md (privacy, costs, AI disclosure,
        prompt-files story, English-UI/any-language note are all drafted there; fill [VERSION], resolve
        the [VERIFY] uninstall note, tick Steam's AI-content disclosure on upload).
    - [ ] Playtest checklist for the new roles wave: a scout answering "can we escape
        them?", weigh_battle against a castle, a wife speaking of the children, a king receiving a
        tier-0 stranger, and a caravan letter arriving as a field report.
    - [ ] Playtest the info sections ("?" in the O and U windows) and the letter window itself.
    - [ ] Playtest the 2026.07.12 morning batch: cost notices per exchange (and their absence on
        sealed-letter flows), the odds view's session-cost line, log.txt filling, first-run popup
        (delete first_run_note_shown.txt + blank the key to see it), key-death quieting (wrong key →
        one amber notice, hourly flows silent, recovery on fixing it), and — if switching to OpenAI —
        gpt-5.6-terra actually answering (needs the new max_completion_tokens shape).
    - [ ] Playtest the map-party farewell fix: click a lord's party on the map → enter the
        Immersive AI section → Farewell → should return to the map with NO engage menu; and the
        same talk inside a town must NOT walk you out of the gates.
- [ ] steam release
    release to steam for everyone to enjoy with good descriptions; upload from tools\package.ps1's
    clean dist\ImmersiveAI layout

POST V1 or NOT FULLY DECIDED:
- [ ] OpenAI /v1/responses migration
    the chat-completions endpoint refuses function tools + reasoning together on gpt-5.6 (hit live
    2026.07.12), so tool-carrying replies run at reasoning "none" for now; the /v1/responses API
    lifts that limit (reasoning WITH tools) and is OpenAI's forward path — a contained rework of
    OpenAIChatClient's payload/response shapes when it's worth it.
- [ ] Utility model split (cost saving)
    a UtilityModel per backend (gpt-5.6-luna / claude-haiku-4-5) for the small calls — feeling number,
    desire yes/no, search refining — cuts roughly a third of cost; parked until the ledger's real
    numbers say it's worth the second client (see docs/models-and-costs.md).
- [ ] Localization wiring
    V1 ships English-only UI and says so on the page; the {=ImmersiveAI_*} ids exist if we ever wire
    the XML. (The NPCs already answer in whatever language the player writes — stated proudly on the page.)
- [ ] "Send letter" in hero's encyclopedia
    Milestone 2 GUI, letters chapter, the remaining half: a "Send letter" button on the encyclopedia hero page — needs swapping `EncyclopediaHeroPageVM` for a subclass (patch the page-VM factory) + overriding the big hero-page prefab to add the button; simplest wiring now is the button opening the letter window (2026.07.10) preselected on that hero. The letter-writing screen half is DONE — the letter window's composer (correspondence alongside, draft mirror, "Seal and send") covers it.
- [ ] Actions for the NPCs:
    NPCs that can ACT, not just know (found while mining ChatAi for the "what the NPC can interact with" task, 2026.07.10): ChatAi lets the LLM trigger real game actions via its NpcDecisionPlanner/AIActionEvaluator — travel to a settlement, patrol, join the player's party (or offer to for coin), accept a join offer, marry the player, give the player gold, start a spar/fight. The info half is done (recall_company + situation whispers); the acting half deserves its own design pass — likely the same native tool-call channel (an "act" tool family beside the recalls), each action gated and phrased to the NPC as a choice of their own will, never a command. Decide scope with Anton first: which actions, what limits, how consent/impossibility is narrated back.
- [ ] NPC to NPC chat
    In the future have a system that lets the NPC pick a person (another NPC) to talk to and for them to be able to exchange a few messages and for me to be able to see the log or watch them in real time talk, again maybe based on how popular they are, but even the unused to have the option to do it. So they should have a general deep memory, a per person deep memory and per person hist maybe
- [ ] date system
   Dates, trysts & the shared grammar of gestures (Deutheuda's dream) — plan drafted and Anton is mulling it over, see `docs/plan-dates-and-roleplay.md`. Short of it: Phase 1 "spend the evening together" (settlement menu option, hours+denars cost, date-occasion situation, Angel narrates the setting, closing whole-evening feeling call) + emote rendering (`*kissing you*` spans as soft narration in the chat window, whisper making the asterisk grammar mutual and weighted by the heart's standing); Phase 2 the binding promise (`propose_meeting` tool, LetterBag-style persistence, missed dates are recorded and felt); Phase 3 gifts, with the "NPCs that ACT" task. Open questions for Anton at the end of the doc.
