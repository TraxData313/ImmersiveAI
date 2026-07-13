MUST BE DONE FOR V1 RELEASE:
- [ ] Pre release:
    - [ ] Steam page assets: 3-4 honest screenshots (chat window, letter window, a reach-out notice, the
        socialness stepper), a 30s clip of a real conversation, and the "clean-room, no ChatAi code"
        provenance note stated plainly. - I, Anton, will make them and will let you know whem I have them
    - [ ] Run the V1 playtest checklist — docs/playtest-checklist-v1.md (Anton, in-game): the roles
        wave (scout/weigh_battle/wife/king/caravan letter), the "?" info overlays + the letter window
        end-to-end, the morning batch (cost notices, sealed flows quiet, odds cost line, log.txt,
        first-run popup, key-death quieting, gpt-5.6 if on OpenAI), the map-party farewell fix, and
        the one-line in-game uninstall confirmation (code says it loads fine — belt-and-braces).
    - [ ] i need to set up antropic key to test the claude models if they work
- [ ] steam release
    release to steam for everyone to enjoy with good descriptions; upload from tools\package.ps1's
    clean dist\ImmersiveAI layout
    STATUS 2026.07.13: UPLOADED, private — Workshop item 3764210301, via the official
    TaleWorlds.MountAndBlade.SteamWorkshop.exe (manifests: tools\WorkshopCreate.xml /
    WorkshopUpdate.xml, quirks documented inside). Thumbnail done (Screenshots\preview_thumbnail.jpg,
    source tools\preview_thumbnail.html). Page ready in BBCode: docs\steam-page-final.bbcode.txt.
    Remains (Anton, on the item page): paste title+description, upload the numbered screenshots,
    tick the AI-content disclosure, subscribe+smoke-test the Workshop copy, flip Public — and make
    the GitHub repo public to match.

POST V1 or NOT FULLY DECIDED:
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
