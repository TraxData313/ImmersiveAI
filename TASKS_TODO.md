MUST BE DONE FOR V1 RELEASE:
- [ ] info sections
    maybe next to the O chat and U letter windows add a info, where there are, explanations, instructions and examples
- [ ] discuss with Claude
    get Fable to think of more TODOs that would be nice to have before we release a V1 to steam
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