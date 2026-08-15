BUGS:

NEXT UPDATE:
- [ ] THE ONE SCREEN REWORK (2026.08.14, Anton's big batch — THE PLAN LIVES IN docs/one-screen-plan.md,
      read it first; research in docs/talk-screen-research.md + docs/prompt-text-inventory.md)
    - [x] Phase 1 — THE SCREEN: built + deployed 2026.08.14, UNPLAYTESTED
    - [x] Phase 2 — THE SCROLLBACK PROMPT: built + deployed 2026.08.14, UNPLAYTESTED
    - [ ] PLAYTEST THE SCREEN (open with O or Y, or "Speak freely" from a map talk):
          1. Everyone in ONE list — party, town, and the far-off you know. Tags (here)/(away)/(gone).
          2. The chosen one STANDS THERE in the middle, alive, in their own place's air. Someone far
             away should show against THEIR sky, not yours. If nobody appears, say so — the face is
             the one part borrowed from the game's own Talk and the likeliest thing to have moved.
          3. Pick someone away: the button becomes "Seal and send", Enter does NOT send, the seal
             does. While any letter rides either way, the seal is grayed and says why.
          4. SCROLL UP past the oldest words: their whole sheet, then the hands they may reach for,
             then the conversation. That is exactly what they get when you write. Check it matches
             what you expect — and tell me if the order or the cut into cards reads badly.
          5. Letters sit in the thread among the spoken words with a ✉.
          6. Escape folds overlays one at a time, then closes. Drafts survive closing.
          7. If it ever fails to open it falls back to the old windows and says so — tell me if
             that happens, with the log line.
    - [ ] Phase 3 — THE PROMPT PACK: prompts.txt — every editable NPC-visible string (~530 keys) in
          one commented hot-reloaded file; marks/recognizers stay in code
- [ ] PLAYTEST: НОЩИТЕ, РАЖДАНЕТО, ПАМЕТТА (2026.08.11 — 463 теста зелени, два кръга ревю,
      промптите ЖИВО пробвани с luna върху Сибила; но НИЩО не е пипано в игра)

      А. НОЩИТЕ — просто ѝ занеси подарък
      1. За 10 денара → 3-4 изречения. За 1000 → 7-8. Гледай ВТОРАТА половина да остава в стаята,
         а не да се затваря на вратата.
      2. 3-4 платени нощи подред → ОБРАЗИТЕ да са различни (градина, запечатан извор, мед под
         езика, миро по дръжката…). Ако се повтарят, кажи — тестето се удължава за минута.
      3. Ако разказ пак свърши с еднаква формула („той ме позна") — трябва още натиск в промпта,
         не повече изречения.

      Б. СВИТЪКЪТ НА НОЩИТЕ (това се вижда чак след 8-10 нощи, търпение)
      4. Нощта, от която е започнало дете, трябва да остава ЦЯЛА в главата ѝ, дори когато вече е
         стара — не бива да изпада само защото има по-нови.
      5. Влизай при нея много нощи подред обикновено → в свитъка ѝ трябва да се слее в един ред
         („from … to … he came to me nearly every night"), а не десет реда.
      6. ДВЕ нощи от един вид НЕ се сливат (нарочно) — сливането почва от три.

      В. РАЖДАНЕТО — лост в разговор: „[Immersive AI • test — write a child's day anew]" при майка
      на твое дете (или изчакай истинско раждане)
      7. Известие „X роди син — Име", после ЧАСЪТ, написан от нейно име.
      8. Попъп „Как ще посрещнеш детето?" — 5 тиера, 100 до 500 000. Ако имаш ПОД 100 денара,
         попъпът НЕ трябва да излиза изобщо (беше бъг, който паузираше света всеки час).
      9. Ако не си бил при нея — попъпът идва чак когато я видиш пак (до 30 дни).
      10. Празник за 1000: в залата САМО наши хора + ДВАТА рода — твоят и нейният. Без мелничари,
          без нобели дошли да се покажат.
      11. H прозорецът: децата стоят НАЙ-ДОЛУ под името на майката — първо часът, после празникът.
          Като отвориш прозореца трябва да ги виждаш веднага, без да скролваш.
      12. Питай ГОСТ за раждането → дава ти празника, ОТКАЗВА часа. Питай МАЙКАТА → дава всичко.
      13. Файлове: Configs\ImmersiveAI\NPCs\campaign_*\_births\ + births.txt.

      Г. ПАМЕТТА И ЦЕНАТА (най-трудното за забелязване — гледай след 10+ разговора)
      14. Веднага след сватба/раждане свидетелят те поздравява с целия разказ — това искаме.
      15. След 7-8 разговора същият разказ трябва да е СВИТ (начало + „the rest has settled"),
          а след 14 — само редът кой ден е бил.
      16. И тогава го попитай направо за деня → трябва да го разкаже ЦЯЛ (вика си регистъра).
          Ако вместо това започне да си измисля — това е бъгът, който търсим.
      17. При човек, с когото говориш често, дословната памет не бива да е предимно битки/пътища.

      Д. СМЕТКАТА НА МЕСЕЦА (ново, 2026.08.11 — иска 10+ нощи, за да се напълни)
      18. Под свитъка ѝ трябва да стои ред от рода на: „Като преброя последните 30 дни: дойде при
          мен 20 пъти, и два от тях направи нещо от вечерта. Чух, че два пъти е спал сам. Чух, че
          е бил с Тирсиф четири пъти — и за нея е направил нощ, за която цялата къща още говори,
          и оттогава я носи — онази, която наричат „Гривната от Аскалат"."
      19. Колкото по-скъпа е била чуждата нощ, толкова повече трябва да е чула: чаша вино минава
          почти незабелязано, а бижу се говори със седмици и с име.
      20. Като забременее някоя — ВСИЧКИ с теб трябва да научат същия ден, и другите ти жени,
          където и да са. Виж го в чатовете им.
      21. Специалната нощ трябва да е назована като такава: „the dearest of them all, the one I
          keep as…".

      ЗНАМ ЗА ЕДНО ДРЕБНО, не съм го решил: имената на градове излизат в различни изписвания
      („Аккалàт" / „Аскалат" / „Аkkалат"), понякога полулатински, и „динара" вместо „денара" —
      артефакт на модела при транслитерация. Ако те дразни, има по-твърда хватка.

- [ ] ПРОВЕРИ СВАТБАТА ЗА 1 000 — вече е списък с гости: твоят род, нейният род, и хората с които
      наистина имаме история. Без непознати.
- [ ] В репортите за битките, за специалните роли, например лечителя, да му пише освен дали е повалил някой в боя, да пише колко е спасил, и това да е видимо за всички, но и късичко. За другите може и да се помисли, ако имат ефект във ванила какъв е и кога в тези логове да се добави, може би Quatermaster-а би се итересувал повече от продажбите с дажбите храна, даже може би да има специални активации, с които да може да каже "Хей, храната/парите в хазната е вече за по-малко от 5 дни", но не на забит руул а от сошълнеса и с намаляването на храната например да се вдига възможността да ме предупреди и някакъв минимален праг в който дори да съм сошълнес 0 да каже просто скриптнато "Хей, храната така и така" и после като говорим да го види като събитие, след което още не сме имали време да говорим насаме и ще е добра тема на разговор, но да го виждат така, нещо "докладвах му че хранта привършва", защото така няма да имаме проблем да оцелим характера му и едновременно няма да харчим ей ай кол, например "добре, че спря там да вземеш зърно, защото бяхме на ръба". Ще мисля в тази посока (Тони)
    - за медика може би да помни колко са ранени и да репортне като всички са оздравяли, или за състоянието на мене или компанионите нещо
    - за скаута може би да казва като забележи някой (пак, в зависимост от сошълнес и тук може би опасност - големина дали е вражествен, особено и ако е по-бърз), но тези неща без колове, а като конверсейшън стартъри, а само с голям сошълнес, да може да тригърне и идване е казване в свободен текст
    - инженера може би да има такива тригъри за готовност на машини при обсада или корабите в морето
    - в морето 2те морски роли да се включват и да заместват на 75% скаута и клотърмастъра и може би да говорят и за бури и състоянието на корабите
    - жената, там трябва да помисля на нея какви женски причини да й дадем
    - така малко по малко може да преминем и понамалим сличайните и да минем към такива които имат смисъл, и без това юзърите се оплакват, че ги пинват само "здрасти как си"... "здрасти само да видя как се чувстваш"...
    Като оправиме да имат истинки причини, трябва да оправим този промпт който ги плащи да не идват, защото преди идваха само с тъпотиите "здрасти как си"... "здрасти само да видя как се чувстваш"
- [ ] Победите/загубите в турнирите да влизат като битките в чата, инфо дали аз и NPCто е участвал и кой е спечелил, и кой в кой рунд е загубил

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
- [ ] Steam page: FAQ + cost plain-talk + local-model note — MOSTLY DONE, two gaps left
    (from ChatAi comment mining, 2026.07.17). DONE: the pinned FAQ thread exists and carries the
    cost plain-talk, the local-models rules, the privacy answer, and (2026.08.08) the compat line
    for War Sails / Marry Anyone / Training Battles. STILL OPEN, both needing a real test before
    they can be written honestly: (a) Diplomacy and Dramalord compatibility — never actually
    verified, only assumed from "we replace no vanilla behavior"; (b) the Steam Deck / Proton
    note, which needs one run on Linux.
    Original note kept for context — ChatAi's comments are dominated by (a) "is this free/safe?
    API = virus?" fear, (b) "does it work with X mod / War Sails / Linux?" questions, (c) local-model
    setup confusion. The remaining shape: one plain sentence of what a typical hour costs in cents
    on the default models and that the key goes ONLY to the provider you chose; a "local models:
    what to expect"
    note (the Local backend → LM Studio/Ollama works, but small models are shaky with our eleven tools —
    point at RelationshipChangesViaTool:false as the fallback). Cheap words, preempts the loudest
    complaint threads on the competitor's page.
- [ ] PLAYTEST THE VOICES (built through the night of 2026.08.15 — the whole roadmap in one pass,
      NOTHING of it heard yet; docs/voiceover-roadmap.md has the full account, and what was proved
      against the real engine is in docs/voiceover-engine-notes.md)
      Turn them on in the talk screen: the "Voices" button in the top bar, then "Turn voices on".
      Sibylla 5 is already the women's voice and Maximus the men's; all five Sibyllas and both
      Achilleses are on the shelf so you can hear them against each other.

      1. ONE LONG REPLY, START TO FINISH. This is the one that matters. It should be a single
         unbroken take — no gap, no stutter, no seam every second — and she should start speaking
         in well under a second. Full read is retired to a fallback and Streaming is now the
         default; if you hear breaking-up, say so and I will look at the pouring (log.txt says how
         many pieces went into how many sounds).
      2. THE ▶ ON AN OLD LINE, far up the thread. Instant if it was ever spoken before, about a
         second if not. It is on EVERYTHING now — her replies, her letters, her own quiet thoughts,
         a wedding account, a night, a child's hour.
      3. BACKSPACE MID-SENTENCE. It must stop dead, from the map and from inside the screen. There
         is a "Stop" button in the bar too, which only appears while something is speaking.
      4. THE PANEL. Press ▶ beside a voice to hear it before giving it to anyone; give one to the
         person in front of you, to all women, to all men, or to yourself ("me" — off by default,
         and you may well hate it, which is why it is off).
      5. A HOSTED VOICE, if you want to see the other road: paste an OpenAI key into
         Voices → "Hosted voices: API key" in MCM and thirteen voices appear beside your own. Live-
         tested last night on your key (three calls, about a third of a cent); it comes back as the
         same 24 kHz mono WAV the local engine makes, so everything downstream is shared.
      6. IF A VOICE EVER RAMBLES OR SCREECHES — tell me, but it should now be seconds rather than
         minutes: every line carries an audio ceiling worked out from its own length, and a reading
         that runs past it is cut off and never cached. A real derail was caught and measured while
         building this (202 characters of Bulgarian → 327 seconds of noise), so this is not
         theoretical.

      KNOWN NOT DONE, deliberately: no cloning from inside the game (the Studio road is documented
      instead), no microphone, and reach-outs do not speak by themselves unless you switch it on.
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

- [ ] PLAYTEST THE NIGHTS (built 2026.08.09, 393 tests green, unplaytested — docs/nights-and-conception-design.md)
      Wed and then watch for, in order:
      (1) At 21:00 a popup "Where will you sleep tonight?" listing your wives, nearest-her-season
          first, each with a plain-words hint; her name greyed with "the custom of women is upon her"
          during her closed days; "Let me look at my own house first" opens the H window.
      (2) Pick her, then a gift. Free = a short beat in her chat and nothing else. Paid = a ☾ notice
          with the night's NAME, the account in the hearth window and in nights.txt, and the party
          disorganized in the morning.
      (3) The green/grey odds line, if ShowConceptionOdds is on. Roughly 66% on her crest for a
          young wife is CORRECT — that is vanilla's own monthly chance, spread onto her season.
      (4) Ignore the popup one evening: the NEXT dusk should log "Last night you slept alone".
      (5) With two wives (Marry Anyone): the other one should sometimes learn where you were, and if
          it was a paid night she learns its NAME too. THE ONE THING TO WATCH FOR BUGS: a second
          wife's pregnancy — the child's father is set by hand before vanilla records it, and a
          mistake there would only surface as a crash at the BIRTH, 36 days later.
      (6) Seven days after a conception: vanilla's "she has learned she is with child", her own beat
          in the chat, and her coming to tell you (or writing, if apart).
      (7) The H window: her season in words, the fortnight, the mode button cycling four ways.
      (8) THE LINE, and it is the one to read carefully. Talk to her the morning after a night you
          spent elsewhere: at the foot of the chat thread you should see "— since you two were last
          alone —" with the mark and a dated list (markets, battles, nights). She should know it
          happened and know it has not been had out, WITHOUT sounding like you have been estranged.
          Watch that it does NOT vanish mid-conversation once she answers, and that it IS gone the
          next time you open the thread after that talk. If the balance of the wording is off,
          quote her back to me. It rides for every companion, not only wives — try a companion
          you have fought and shopped with too.
      Dev lever: the chat window's Dev panel, "Spend a night with them now (if wed)".
