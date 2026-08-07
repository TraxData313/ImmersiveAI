BUGS:

NEXT UPDATE:
- [ ] MARRIAGE BY COURTSHIP — the wedding handshake (Anton, 2026.08.07; DESIGN BRIEF, take in a
      FRESH session: needs an ilspycmd pass over vanilla Romance/Marriage first). The bargain-tool
      mold applied to marriage: words move the heart, ONLY the player's sealed click weds.
      THE ONE LAW (inherited from strike_bargain): talk alone never marries — a native tool only
      LAYS the moment (betrothal, then wedding), the only door is a confirm popup, and lay AND seal
      both re-run every hard rule. The refusal must never name a threshold verbatim (the Sibuga
      floor lesson — she parroted it); she hints, she never quotes her own rails.
      • THE ROAD (his "стълбове"): a persistent courtship stage per NPC — Stranger → Warmth ("I
        like him") → Devotion ("I really like him") → Ready ("no questions left; I feel ready") →
        Betrothed (годеж — a recorded first-person beat, "I have promised myself to them") → Wed.
        She moves HERSELF along it via a new native tool (plight_troth / tend_courtship — the
        move_heart family), one step at a time, forward only when her asks + the rails allow,
        backward always free (a wound can break it). PERSIST THE STAGE INSIDE memories.json (new
        NpcMemory fields) — then RevertMemoriesWithSaves rewinds courtship with the save for free.
      • THE STATION GATE (his clan-size rail, like the haggle %): required player Clan.Tier from
        HER station — ruler's daughter/kin 6, great lord's kin 4–5, minor noble 3, notable's
        daughter 2, wanderer 1 — minus up to CourtshipCharmSlack (default 2, MCM 0..4) earned only
        at full Devotion/Ready (the Don-Juan discount). Checked at every stage-up toward Betrothed,
        rechecked at both seals. So the emperor's daughter at tier-0 player = walls, exactly asked.
      • HER QUIET ASKS (generated, private, checkable): at the first true marriage-talk (or on
        reaching Warmth) a one-time utility call — the persona-spark's sibling, "the matchmaker's
        ledger" — writes 2–4 private requirements from her story/traits/spark/world text, EACH tied
        where possible to a checkable predicate the resolver can answer from live data: "a man of
        his word" (Honor/known deeds/relation), "he must hold at least X denars" (Hero.Gold),
        "no stranger to the sword" (renown/skills), "he must love God" (Anton's Biblical world
        text). Folded into her sheet PRIVATELY ("What I quietly ask of a husband — never as a
        list, they surface as talk turns"), exactly his 'real woman' ask: she KNOWS them, she
        never recites them. weigh_troth (or the same tool's look mode) tells HER which are truly
        met — she speaks it humanly.
      • THE TWO SEALS: proposal at Ready → tool lays BETROTHAL → popup ("Take her promise?") →
        recorded beats both sides; later (config: at once, or after N days of betrothal — decide
        with Anton) the wedding lay → popup naming what the world requires (dowry via vanilla
        MarriageModel? optional) → MarriageAction.Apply + honest beats ("We are wed; I am his/hers").
      • VANILLA RESEARCH LIST (verify with ilspycmd against the real DLLs before designing final):
        Romance.GetRomanticLevel/ChangeRomanticState + RomanceLevelEnum (does courtship state help
        or fight us?), MarriageAction.Apply overloads (companion brides? clan moves?), MarriageModel
        (IsCoupleSuitableForMarriage, dowry), consent/family — and the MCM toggle
        MarriageNeedsFamilyConsent (his explicit ask: configurable skip of parent approval; decide
        default with him). Marry Anyone interplay: ALWAYS FamilyBuilder.AreWed/SpousesOf, never bare
        Spouse (see memory polygamy-exspouses-truth); what does a second marriage mean here?
      • THE TWO WALKTHROUGHS to design against: SIBILA (wanderer, tier gate 1, real history + his
        authored secret-love instruction — the road is the feature: Warmth→Ready over a few true
        talks, asks like "he must not cage me; I ride free"); vs AN EMPEROR'S DAUGHTER (gate 6,
        slack −2 at best → tier 4 floor, asks harder, family consent looming — near-impossible for
        a nobody, a campaign-long prize for a Don Juan).
      • MCM: EnableConversationMarriage (default on?), MarriageNeedsFamilyConsent,
        CourtshipCharmSlack; test levers: show-the-road odds view line + a DevMode "reveal her
        quiet asks" + reroll-asks lever.
- [ ] BUG The buttons "Their prompt", "World prompt", Memory (dev option, forgot the real name) are stacking over each other and over the NPC name and details (example screenshot C:\Program Files (x86)\Steam\userdata\258577504\760\remote\261550\screenshots\20260807193017_1)

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
- [ ] Utility model split (cost saving) — BUILT AND REVERTED 2026.07.27, pick it up from the code
    Anton's call: it was written and working-by-build, but there was no time to playtest it and it
    did not feel right to ship untested (his current haiku setup would see no change anyway — the
    saving belongs to whoever speaks with sonnet / opus / 5.6). The whole implementation lives in
    commit 86e061f (reverted by a0c0a64): `git show 86e061f` restores it in one step.
    What it did: five MECHANICAL calls move to a cheaper model on the SAME backend, key and endpoint —
    the private feeling number, the reach-out ponder, both letter yes/no weighings, the search-query
    refiner. Everything an NPC SAYS, REMEMBERS or WRITES stays on the main model. THE LINE TO HOLD if
    this is ever revisited: does a human ever read these words? If yes, main model.
    Design worth keeping: `UseUtilityModel` (bool) + `UtilityModel` (blank = auto) + MCM fields;
    auto picks the backend's small tier and never crosses provider families on OpenRouter; it resolves
    to NO SPLIT when the main model already is that model, when the price table says the pick is not
    cheaper (a nano user must never be moved UP to mini), on Local (one model is loaded), and on a
    custom endpoint whose catalogue we cannot know — and with no split `UtilityClient` hands back the
    main client, so that path stays byte-for-byte the old one. Mechanism: `ChatClientFactory.CreateUtility`
    → `LiveSwapChatClient` with a model override that MUST join `Signature()`; memory + utility shells
    pass `announceSwaps: false` so only the voice the player hears announces a model change.
    Open question for next time: the reach-out PONDER carries the whole sheet, so it is where the money
    is — but it is also the "does this soul want to seek you out" judgment Anton has tuned twice. Test
    that one specifically on a big model before believing the saving is free.
    Untestable by unit test as written: the resolution logic sits in `ModConfig` (Module, net472) while
    the test project is Core (net8). If it returns, consider moving the pure resolver into Core so it
    can be covered.
- [ ] Localization wiring
    V1 ships English-only UI and says so on the page; the {=ImmersiveAI_*} ids exist if we ever wire
    the XML. (The NPCs already answer in whatever language the player writes — stated proudly on the page.)
- [ ] "Send letter" in hero's encyclopedia
    Milestone 2 GUI, letters chapter, the remaining half: a "Send letter" button on the encyclopedia hero page — needs swapping `EncyclopediaHeroPageVM` for a subclass (patch the page-VM factory) + overriding the big hero-page prefab to add the button; simplest wiring now is the button opening the letter window (2026.07.10) preselected on that hero. The letter-writing screen half is DONE — the letter window's composer (correspondence alongside, draft mirror, "Seal and send") covers it.
- [ ] Actions for the NPCs:
    NPCs that can ACT, not just know (found while mining ChatAi for the "what the NPC can interact with" task, 2026.07.10): ChatAi lets the LLM trigger real game actions via its NpcDecisionPlanner/AIActionEvaluator — travel to a settlement, patrol, join the player's party (or offer to for coin), accept a join offer, marry the player, give the player gold, start a spar/fight. The info half is done (recall_company + situation whispers); the acting half deserves its own design pass — likely the same native tool-call channel (an "act" tool family beside the recalls), each action gated and phrased to the NPC as a choice of their own will, never a command. Decide scope with Anton first: which actions, what limits, how consent/impossibility is narrated back.
- [ ] Reply language option (from ChatAi comment mining, 2026.07.17 — the single most repeated ask over there: Polish, Korean, Russian, Turkish, French, Spanish...)
    A `ReplyLanguage` config key (+ MCM text field) that, when set, injects one gentle line into every
    sheet — "Answer always in X, whatever tongue is spoken to you" — so NPCs hold their language even
    when the player types English UI terms or mixed text. Today NPCs already mirror whatever language
    the player writes (stated on the page); this makes it a firm, discoverable setting instead of an
    emergent behavior. Near-zero cost, one line through PromptBuilder; distinct from the UI
    localization task above (this is the NPCs' tongue, not the mod's strings).
- [ ] Quest-taken Angel note (from ChatAi comment mining, 2026.07.17)
    ChatAi players complained NPCs are confused when a quest is accepted via VANILLA dialogue and then
    discussed in chat — the LLM never saw the acceptance. We already cover most of it (TroubleBuilder
    narrates issue state incl. taken-by-player; silent meeting beats record that a talk happened), but
    a silent Angel note on the GIVER's memory the day their quest is taken — "This day you gave X your
    trouble to carry: <quest title>" — closes it fully and makes the memory itself carry the moment,
    not just the situation sheet. Same pattern as MeetingLine (CampaignEvents quest-started hook, no
    LLM call, one per quest, dedupe like IsMeetingLine).
- [ ] Steam page: FAQ + cost plain-talk + local-model note (from ChatAi comment mining, 2026.07.17)
    ChatAi's comments are dominated by (a) "is this free/safe? API = virus?" fear, (b) "does it work
    with X mod / War Sails / Linux?" questions, (c) local-model setup confusion. Add to our page:
    a short FAQ (compat: Diplomacy/Dramalord/Marry Anyone/overhauls; War Sails yes; Steam Deck/Proton
    note after one test); one plain sentence of what a typical hour costs in cents on the default
    models and that the key goes ONLY to the provider you chose; and a "local models: what to expect"
    note (the Local backend → LM Studio/Ollama works, but small models are shaky with our eleven tools —
    point at RelationshipChangesViaTool:false as the fallback). Cheap words, preempts the loudest
    complaint threads on the competitor's page.
- [ ] Voice-over via OpenRouter audio (POST V1 — researched 2026.07.17, see memory chatai-comments-and-voice-backends)
    NPCs speaking their replies aloud. Backend: OpenRouter's dedicated OpenAI-compatible endpoints —
    `/api/v1/audio/speech` (TTS) and `/api/v1/audio/transcriptions` (STT) — the SAME sk-or- key the
    mod already carries; models incl. Kokoro 82M (very cheap, 54 preset voices — enough to assign a
    stable per-NPC voice from StringId like the speech styles) and gpt-4o-mini-tts / Gemini Flash TTS.
    OpenAI backend works too (tts-1 $15/1M chars); Anthropic has no audio API. Request PCM/WAV output
    so .NET 4.7.2 playback stays simple (System.Media.SoundPlayer handles WAV — no mp3 decoder).
    Rails: OPTIONAL and default OFF, its own toggle + voice/model config, every synthesis billed
    through UsageLedger with its own price line (ChatAi's users report TTS eats ~10x the text credits —
    the cost story must stay boringly honest), fail-soft (a dead audio call never blocks the spoken
    text), and off-thread synthesis marshaled like every other LLM call. STT (player speaking) is a
    separate later step — TTS first.
- [ ] Dramalord compatibility (asked on Nexus by coca1colax, 2026.07.25)
    Fold Dramalord's relationship state (lovers, affairs, friendships, its emotion values) into what
    each NPC knows of themselves and the player — so a Dramalord lover speaks AS a lover without the
    player explaining it in chat. Likely shape: a soft dependency read by reflection (the MCM pattern —
    absent Dramalord, nothing changes) feeding SituationBuilder/FamilyBuilder ("What X is to me" +
    the situation's standing lines), maybe Dramalord events as tidings. Needs a study pass over
    Dramalord's data layer first (it keeps per-hero attraction/trust/love + relations in its own
    save data); decide scope with Anton — read-only awareness first, our NPCs ACTING on it later.
- [ ] Total-conversion world frame (asked for indirectly on Nexus by LukeCage718, 2026.07.27 — "can't wait
      to try this with ROT" = Realm of Thrones, the Game of Thrones overhaul; second comment implying a
      converted map)
    Everything already works on an overhaul — we read live campaign data through public APIs, and
    self.txt even seeds from the overhaul's own encyclopedia lore. The one hardcoded assumption is the
    world's NAME: `RefineSearchQueryAsync` (ImmersiveChatBehavior) prepends "Mount and Blade Bannerlord"
    to every `seek_wisdom` query, so a Westerosi lord asked about his own house gets searched as a
    Bannerlord question. Make the frame a config key (`WorldName`, default "Mount and Blade Bannerlord")
    used by the refiner and its raw-question fallback. Consider a second sentence on the Steam/Nexus page
    pointing overhaul players at `AtmosphereLine` + `RoleplayGuidance` — the existing keys that already
    let them tell every soul they live in Westeros and that the tale is their own, not the written one
    (worth saying out loud: a model that recognizes the setting will otherwise drift into show canon).
    Small change, serves every total conversion, not just ROT.
- [ ] NPC to NPC chat
    In the future have a system that lets the NPC pick a person (another NPC) to talk to and for them to be able to exchange a few messages and for me to be able to see the log or watch them in real time talk, again maybe based on how popular they are, but even the unused to have the option to do it. So they should have a general deep memory, a per person deep memory and per person hist maybe
