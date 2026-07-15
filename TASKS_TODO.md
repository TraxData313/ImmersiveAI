POST V1 or NOT FULLY DECIDED:
- [ ] Party commands by word and by letter — RESEARCHED, ready to build (see docs/party-commands-research.md)
    Leaders of the player's clan parties take orders through conversation or letters via a
    `set_party_course` native tool (v1 verbs: patrol / escort_player / go_to / hold / resume) —
    and being persons, may negotiate or refuse; a mailed order takes effect when the courier arrives
    (compose/reply already ride CompleteSpokenAsync with tools — zero extra plumbing).
    The verified-on-v1.4.7 technique: do NOT fight the AI with SetMoveX/DoNotMakeNewDecisions —
    inject the order into the party's own hourly deliberation (`CampaignEvents.AiHourlyTickEvent`
    → `PartyThinkParams.AddBehaviorScore` with `AIBehaviorData`, score 15f wins; `AiBehavior` enum
    is in TaleWorlds.CampaignSystem.Party; naval routing free via Helpers.AiHelper). No Harmony
    needed. Orders persist as plain strings in SyncData (no new saveable classes); they clear
    honestly on army-join/capture/party-death/target-turned-enemy/unreachable, and EVERY
    set/change/lapse fires a colored InformationManager.DisplayMessage — the left-side line AND
    the permanent event-log entry Anton asked for. Cut from v1: raid, besiege, disband, caravans,
    other lords. Reference source (MIT, supports exactly v1.4.0–1.4.7, studied 2026.07.15):
    ..\reference\Bannerlord.PartyAI; prior art: Finer Party Controls (closed, clan-screen panel +
    the "Thinks" framework that exists precisely because naive SetMove is unstable).
- [ ] NPCs that are in charge of Cities/Castles when they see ana enemy army they get the army and their party/castle info and get the option to send a letter (asking for help, letting the player know they can hold etc)
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
