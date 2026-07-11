MUST BE DONE FOR V1 RELEASE:
- [ ] info sections
    maybe next to the O chat and U letter windows add a info, where there are, explanations, instructions and examples
- [ ] discuss with Claude
    get Fable to think of more TODOs that would be nice to have before we release a V1 to steam
    FABLE'S V1 PROPOSALS (2026.07.12, the night shift — react to these, strike what you don't want):
    - [ ] First-run experience: a player with no API key gets ONE clear, kind popup on first campaign
        entry pointing at config.json / the MCM page (the health check already speaks, but a brand-new
        Steam player needs the "here is where the key goes, here is where keys come from" version, once).
    - [ ] Cost visibility: a rough per-session token/request counter (even just requests made + a soft
        note in DevMode) — Steam players WILL ask "what is this costing me"; a config `MaxDailyRequests`
        safety valve (default off) would calm the worried.
    - [ ] Graceful key-death mid-session: today a 429/insufficient-quota mid-talk reads as a mute NPC
        with an error toast; catch the classified error once and tell the player plainly, then quiet the
        hourly flows (letters/reach-outs) until the next successful call so the log isn't a wall of red.
    - [ ] The prompt-files story for players: NPCs\_README.txt exists, but a short "how to shape your
        world" section in the Steam description + a sample global_prompt.txt with commented examples
        would turn the folder from mystery to feature.
    - [ ] Performance guard: MaybeStartNpcLetter and the odds view read EVERY memory file each hour/use;
        fine at 30 NPCs, worth a cached index (id → richness/lastDay, invalidated on save) before Steam
        players show up with 300-NPC campaigns.
    - [ ] Localization pass at least for the dialog-option strings ({=ImmersiveAI_*} ids exist — decide
        V1 ships English-only and say so, or wire the XML).
    - [ ] Version + migration stamp inside config.json (a "ConfigVersion": 1) so V1.1 can migrate
        defaults without clobbering hand-edits.
    - [ ] A "panic switch": one MCM toggle / config key that disables ALL autonomous behavior (reach-outs,
        letters, notices) in one move for players who only want the talk-when-I-talk experience.
    - [ ] Steam page assets: 3-4 honest screenshots (chat window, letter window, a reach-out notice, the
        socialness stepper), a 30s clip of a real conversation, and the "clean-room, no ChatAi code"
        provenance note stated plainly.
    - [ ] Playtest checklist for the new roles wave before release: a scout answering "can we escape
        them?", weigh_battle against a castle, a wife speaking of the children, a king receiving a
        tier-0 stranger, and a caravan letter arriving as a field report.
- [ ] steam release
    release to steam for everyone to enjoy with good descriptions

POST V1 or NOT FULLY DECIDED:
- [ ] "Send letter" in hero's encyclopedia
    Milestone 2 GUI, letters chapter, the remaining half: a "Send letter" button on the encyclopedia hero page — needs swapping `EncyclopediaHeroPageVM` for a subclass (patch the page-VM factory) + overriding the big hero-page prefab to add the button; simplest wiring now is the button opening the letter window (2026.07.10) preselected on that hero. The letter-writing screen half is DONE — the letter window's composer (correspondence alongside, draft mirror, "Seal and send") covers it.
- [ ] Actions for the NPCs:
    NPCs that can ACT, not just know (found while mining ChatAi for the "what the NPC can interact with" task, 2026.07.10): ChatAi lets the LLM trigger real game actions via its NpcDecisionPlanner/AIActionEvaluator — travel to a settlement, patrol, join the player's party (or offer to for coin), accept a join offer, marry the player, give the player gold, start a spar/fight. The info half is done (recall_company + situation whispers); the acting half deserves its own design pass — likely the same native tool-call channel (an "act" tool family beside the recalls), each action gated and phrased to the NPC as a choice of their own will, never a command. Decide scope with Anton first: which actions, what limits, how consent/impossibility is narrated back.
- [ ] NPC to NPC chat
    In the future have a system that lets the NPC pick a person (another NPC) to talk to and for them to be able to exchange a few messages and for me to be able to see the log or watch them in real time talk, again maybe based on how popular they are, but even the unused to have the option to do it. So they should have a general deep memory, a per person deep memory and per person hist maybe
- [ ] date system
   Dates, trysts & the shared grammar of gestures (Deutheuda's dream) — plan drafted and Anton is mulling it over, see `docs/plan-dates-and-roleplay.md`. Short of it: Phase 1 "spend the evening together" (settlement menu option, hours+denars cost, date-occasion situation, Angel narrates the setting, closing whole-evening feeling call) + emote rendering (`*kissing you*` spans as soft narration in the chat window, whisper making the asterisk grammar mutual and weighted by the heart's standing); Phase 2 the binding promise (`propose_meeting` tool, LetterBag-style persistence, missed dates are recorded and felt); Phase 3 gifts, with the "NPCs that ACT" task. Open questions for Anton at the end of the doc.