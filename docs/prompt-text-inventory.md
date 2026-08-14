# Immersive AI — Inventory of every string an LLM ever receives

Swept 2026-08-14, read-only, from `src\ImmersiveAI.Core\` and `src\ImmersiveAI.Module\` (all 7 tool-definition files, all prompt/formatter classes, all six behavior partials, plus `tests\` for guard coverage). Player-facing UI text (popups, notices, MCM hints, window labels, log lines) is **excluded** unless the same string also reaches a prompt.

## Legend

- **kind**: `sheet` = system-sheet template · `situ` = situation-prose · `beat` = recorded-beat template (lands in memories.json verbatim, replayed forever) · `MARK` = recognizer text matched against stored data or model output — **editing has consequences** · `mem` = memory-writing prompt · `chron` = chronicle-writing prompt · `tool-name`/`tool-desc`/`param` = tool schema text · `resolver` = tool-answer prose the model reads back · `util` = utility-call prompt (refine/feeling/yes-no/seed/spark/think/health) · `palette` = word/phrase list · `LEGACY` = recognition-only, must NEVER be user-editable
- **slots**: interpolated runtime values
- **flags**: `T` = asserted by tests\ · `R!` = recognizer-coupled (string or a fragment is later matched against recorded memories, model output, or config) · `LOCK` = permanent by design doc ("never reword")

---

## A. Core `Prompts\` — the sheet, the beats, the player's own mind

### PromptBuilder.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| default identity (BuildSystemPrompt fallback) | "I am {name}, a living soul in the world…" | sheet | name | T |
| "My traits are " lead | My traits are {lowered traits} | sheet | traits | T |
| "When I speak, it comes out like this: " | — | sheet | style | |
| "Who I have become:" header | — | sheet | self | T |
| "Of this world, this I know:" header | — | sheet | global prompt | T R! (same words in PromptFiles template + BuildPlayerThought label) |
| "Of myself, this I hold true:" header | — | sheet | npc prompt | T R! (named in PromptFiles template) |
| "The road of my life so far, as I carry it…" header | — | sheet | | T |
| "What {player} is to me{asOf}:" header | — | sheet | player, date | T |
| " (as I last gathered my thoughts on {date})" | — | sheet | date | T |
| "How should I speak:" header | — | sheet | | T |
| BrevityGuidance | "- I speak as talk truly flows between two people…" | sheet | | |
| OldWorldToneGuidance | "- My words carry a light savor of the old world…" | sheet | | T ("light savor of the old world") |
| PlainSpeechGuidance | "- I speak my words aloud; they are heard, not read…" | sheet | | T |
| ActingOutGuidance | "- One mark alone escapes that rule: what I truly DO…" | sheet | | T R! (teaches the exact single-asterisk grammar EmoteText parses) |
| whisper: CanRecallWorld | "- When a person, place, house, realm… comes up and my memory is dim…" | sheet | | |
| whisper: CanSeekWisdom | "- When I am asked how a thing in the world is done…" | sheet | | |
| whisper: CanMoveHeart | "- My heart is my own, a living thing…" | sheet | | T |
| whisper: CanRecallChronicle | "- Battles we have lived through side by side are set down…" | sheet | | |
| whisper: CanSurveyField | "- From where my company stands I may cast my eyes…" | sheet | | T |
| whisper: CanStrikeBargain | "- I am for hire, and the bargain is mine to strike…" | sheet | | T |
| whisper: CanTendTroth | "- My troth is my own to tend. Marriage in this world is a road…" | sheet | | T ("My troth is my own to tend"); arrival clause load-bearing per comment |
| whisper: misgivings (rides with TendTroth) | "- My misgivings about a life together are my own…" | sheet | | T |
| whisper: CanBlessTroth | "- The one of my house who is promised to them awaits my word…" | sheet | | T |
| AngelFrame | "{voice} speaks softly into your mind: \"{line}\"" | LEGACY frame | voice, line | T R! LOCK — replay of pre-2026.08.07 turns only |
| InnerFrame | "(Within my own mind: {line})" | beat frame | line | T R! (live + replay must render identically; window renders "(Name, within: …)") |
| ReachOutPonderLine ×2 (stranger/known) | "I notice {player} nearby… NO — or YES: what I want to discuss." | util | player | R! (answer shape parsed by InitiationParser.WantsToGo keywords NO/YES/GO/STAY) |
| ReachOutPonderNote ×2 | "I marked {player} nearby… I resolved:" | beat | player | T R! (prefix is PonderNoteMark) |
| PonderNoteMark | "I marked " | MARK | | R! LOCK (IsPonderBeat prefix match, window folding) |
| ApproachLine ×2 (welcomed/refused) | "I rise and go to {player}…" | beat | player, reason | T |
| ApproachNote ×2 | "Of my own accord I went to {player}…" | beat | player, reason | |
| FirstWordLine ×2 (stranger/known) | "I cross to {player} now — we have never spoken…" | beat | player, reason | T ("at once or only later") |
| FirstWordNote | "Of my own accord I crossed to {player} and spoke first…" | beat | player, reason | |
| ReasonSentence / ReasonClause | " What brings me: {reason}." / " — what brought me: {reason}" | beat glue | reason | |
| WriteLetterDesireLine | "The road lies long between me and {player}… yes or no." | util | player | T; answer parsed by WantsToReachOut |
| ComposeLetterLine | "I sit, and set my heart to paper…" (+ inService report clause) | beat | player | T R! LOCK — opening IS ComposeLetterMarkOwn; in-service clause appended AFTER the marker on purpose |
| AnswerLetterDesireLine | "A courier has found me… I break the seal and read: {body} … Do I wish to write back…" | beat | player, letterBody | T R! LOCK — contains ReadLetterOpenMarkOwn + ReadLetterCloseMarkOwn; the body is extracted BETWEEN the marks |
| ComposeReplyLine | "I answer them now. What I set down is only the letter…" | beat | player | T R! LOCK — opening IS ComposeReplyMarkOwn |
| ComposeLetterMark / ComposeReplyMark / ReadLetterOpenMark / ReadLetterCloseMark | "Then sit, and set your heart to paper" … | LEGACY MARK ×4 | | T R! LOCK — recorded pre-2026.08.07 memories carry these forever; NEVER editable |
| ComposeLetterMarkOwn / ComposeReplyMarkOwn / ReadLetterOpenMarkOwn / ReadLetterCloseMarkOwn | "I sit, and set my heart to paper" … | MARK ×4 | | T R! — must stay word-for-word fragments of the three templates above; change only in lockstep |
| ArrivalLine ×2 (first/again) | "{player} draws near and greets me…" | beat | player | T |
| MeetingMarker | "though the words of it are not set down here" | MARK | | T R! LOCK — IsMeetingLine dedupe (once/day), shared by both eras |
| MeetingLine ×2 (first/familiar) | "{player} and I met and spoke face to face…" | beat | player | T; embeds MeetingMarker |
| BuildFeelingQuery system (2 lines + traits) | "I am {name}, a living soul… I answer with a single whole number" | util | name, traits | T |
| BuildFeelingQuery user | "(Within my own mind: {player} came to me. They said: … how far did that moment move my heart…" | util | player, playerLine, npcReply | T ("single whole number"); answer parsed by FeelingParser |
| MeetingSeparator | "[[the-moment]]" | MARK (internal) | | T R! — split token, consumed before sending; situation file shows "· · ·" |
| BuildPlayerThought labels ×4 | "[What I know, standing here:]" / "[Of this world…]" / "[What has passed between us — our own words, in order:]" / "[{npc} and I have never yet spoken…]" | util | npc | T |
| RenderScript frames ×3 | "({npc}, to themselves: {line})" / "{player}: {line}" / "{npc}: {line}" | util | names, lines | T |

### PlayerThought.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| MindFrame ×2 (spoken/letter) | "I am {player}. I am working out what to say next to {npc}…" | util (system) | player, npc | T ("I set down my own words only", "I do not answer in their voice") — the seating-chart fix, guarded by message-count test |
| SpokenLine frame | "[Now it is my turn to speak.]" + same-spirit/same-tongue rails + "{player}:" | util | player | T ("As short as talk truly is") |
| SpokenLine acting clause | "A small act of mine may ride between single *asterisks*…" | util | | |
| LetterLine frame | "[Now it is my turn to write.]" … "{player} writes:" | util | player | T |
| AppendWish ×2 | "Nothing is settled in my mind yet…" / "What turns in my mind: \"{wish}\" — half-formed thought…" | util | wish | T |
| Tame() arrow/label/verb tokens | "→", "->", "says", "writes" | MARK (output-parse) | | T R! — recognizes the frame's own furniture echoed back |

### ConversationPresets.cs / PersonaSpark.cs / BeatFade.cs / MoodTides.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| Defaults ×3 (starter/romantic/ender) | "I want to open a talk with them…" | palette (player wish → rides into think call) | | T |
| PersonaSpark.Deck (24 muse cards) | "an old wound that never healed" … | palette | | T (two cards asserted) |
| PersonaSpark.Intensities ×3 clauses | "SUBTLE — barely a shade…" / "MARKED…" / "VIVID — bold, memorable…" | palette | | T ("Intensity drawn: VIVID") |
| PersonaSpark.BuildPrompt (~10 fragments) | "You are the casting director of a living medieval world…" / "The raw facts of them:" / "Muse cards drawn…take ONE…or discard both" / "Write 1 to 3 sentences in {their} own first-person voice…" | util | name, gender words, age, station, where, traits, style, backstory, worldText, cards, intensity | T heavily; live-validated on terra — change only with fresh samples |
| BeatFade.SettledTail | "The rest of it has settled the way a day settles…" | situ (replayed-beat replacement) | | T R! (works only because chronicle marks split correctly) |
| BeatFade.KeptTail | "I keep the whole of that day in me…" | situ | | T |
| MoodTides.Humors[20] | "in bright spirits — small things please me…" | palette | | T |
| MoodTides cluster indices (Menses/Rising/Crest/Waning/WithChild) | index arrays into Humors | palette-coupling | | editing Humors order silently rewires the clusters |
| "This day finds me " lead | — | situ | humor | |
| CycleSentence lead + 4 turnings | "And my body keeps its own season, as it does for every woman: …" | situ | | T ("these are the rising days"); also fed to night chronicler as WifeSeason |
| WithChildSentence ×4 | "And my body keeps a season of its own: the child within me…" | palette | | |
| PhaseWord ×4 | "the custom of women is upon her" … | player-facing (window/keepsake) | | |

---

## B. Core Memory & formatters

### MemoryCompressor.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| DefaultSystemVoiceName | "Angel" | LEGACY | | attribution of old turns only |
| InnerOpening ×2 | "(Within my own mind — I, {name}: {act})" | mem | name | T |
| compression opener act | "I sit a moment with my memory of this person and settle it." | mem | | |
| compression body | "Time moves on, and older moments are slipping… I keep what matters…" | mem | | |
| "What I already hold in memory (I weave the fading moments into it):" | — | mem | summary | |
| "The moments now fading (I fold these into my memory):" | — | mem | turns | |
| "Still fresh in my mind (context only — these stay with me…):" | — | mem | turns | T |
| AppendReplyFormat | "I set it down in exactly this shape: / SUMMARY: / \<all I carry of them… my own road from before also lives here…\> / I write it whole each time, for what I do not set down here fades from me…" | mem contract | | T heavily, R! (SUMMARY: parsed) — CLAUDE.md: change deliberately, never in passing |
| reflection opener act | "I pause a while and gather my thoughts about this person." | mem | | |
| reflection body | "I settle my memory of them as I see fit…" | mem | | |
| self-invite ×2 | "And I look inward, too… who have I become?" + first-time vs existing variants | mem | selfText | |
| "What I already hold in memory (I revise it as I reflect):" | — | mem | | |
| "Older moments now fading (I fold these into my memory):" | — | mem | | |
| "Still fresh in my mind (these remain with me…)" | — | mem | | |
| SELF: ask ×2 | "\<a short paragraph, in my own first-person voice… If nothing has changed, I write: unchanged.\>" | mem contract | | R! ("unchanged" + SELF: parsed) |
| AppendReflectedTurn frames ×5 | "[{stamp}] My own thought, then: " / "I: " / "{speaker} said: " / "I answered: " / speaker = "They" or voice | mem | stamp, lines | T |
| TurnStamp fallback | "Day {n}" | mem | day | |
| section labels "SUMMARY:" "SELF:" "FACTS:" "GOALS:" | — | MARK (parse) | | T R! LOCK — FACTS:/GOALS: are retired but must keep BOUNDING sections forever |
| "unchanged" marker + MarkerDressing | — | MARK (output-parse) | | T R! |

### TidingsFormatter.cs / StorySeedFormatter.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| AgoPhrase ×3 | "earlier today" / "yesterday" / "some {n} days past" | situ | days | T |
| TidingLine | "- {fact} — {ago}." | situ | fact, ago | T |
| RumorLine | "- “{overheard}”" | situ | text | T |
| Compose headers ×3 | "Tidings of the world's late doings have reached my ears:" / "And I have overheard the common folk say:" / "And in the streets of {place}…" | situ | place | T |
| FromWorldStory lead | "So runs my story, as the world tells it: " | mem-seed | story | T |
| FromPlayerFame ×3 tiers | "Of {name}, before ever we spoke, the world had long been telling…" | mem-seed | player, renown tier | T — tier thresholds (150/300/900) deliberately aligned with live situation lines |

---

## C. Core chronicles — weddings, births, nights, battles, journey, courtship, the line

### WeddingText.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| DayAccountMark | "The wedding day, as it is told:" | MARK | | T R! LOCK |
| NightAccountMark | "Of the night that followed, this is mine alone to remember:" | MARK | | T R! LOCK |
| SpouseDayBeat | "This day I was wed to {player}{, in place}. {mark} {account}" | beat | player, place, account | T |
| WitnessDayBeat | "This day I stood among the witnesses at the wedding of…" | beat | player, spouse, place, account | T |
| NightBeat | "{mark} {account}" | beat | account | T |
| FullAccount head + witnesses + blessing ×2 | "That day, in {place} — X and Y were wed." / "Those who stood there: …" / "The blessing of the house was given by…" | resolver | many | T |
| night-privacy refusal | "Of the night that followed I know nothing, nor should I — that belongs to the two of them alone." | resolver | | T — privacy rule as code |
| RollEntry / ChronicleEntry frames | "=== {title} ===" / "Witnesses: " / "Blessed by … for {n} denars." / "-- The day --" / "-- The night (theirs alone) --" | resolver/file | many | T |
| BuildFeastPrompt (~15 fragments) | "You are the chronicler of a living medieval world — … in the manner of the old Scriptures…" + 8-to-14-sentences ask + witness/no-hall variants + concrete-things (hall vs open-road variants) + numbers-in-words + date rule + no-prophecy + no-fourth-wall + end-on-the-two + output-only | chron | witnesses, place, blessing flags | T very heavily — change only with fresh live samples |
| BuildNightPrompt (~12 fragments) | "You are the same chronicler, and now you set down what no hall saw… IN {HER} OWN VOICE" + Song-of-Songs register + both-halves rule + facts + day-quote + 6-to-12 ask + concrete (room vs open-road) + end-inside-night | chron | pronoun sets (both spouses' genders), place, dayAccount | T very heavily; pronouns derived — a female player's husband must never get "her own hands" |
| AppendFacts (~15 labels) | "The truths of this day:" / "- Their cast of mind: …" / "- What they hold true of themselves: …" / "- The place: no hall and no town — they were wed on the road…" / "- Who stood there: no one but the two of them…" / "- The wedding they paid for: …" / "- The doubts they once wrote down…" / "- The story these two already share…" / "- The world they live in, as its keeper wrote it: …" | chron | ~15 fact slots | T |
| AppendTongueRule ×2 forms | "THE TONGUE: write in the tongue these two speak… match it exactly" | chron | recentWords | T — rides LAST on purpose |
| CleanAccount Heading/LeadingLabel regexes | `^#{1,6}` / "part one|the day|the night|feast…[:—-]" | MARK (output-parse) | | R! |
| LooksLikeAnAccount (≥120 chars) | — | output gate | | |

### BirthText.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| HourAccountMark | "Of the hour my child came into the world:" | MARK | | T R! LOCK |
| FeastAccountMark | "The feast for the child, as it is told:" | MARK | | T R! LOCK |
| FatherMark | "A child was born to me this day:" | MARK | | T R! LOCK |
| GriefMark | "A child of mine came into the world this day and did not stay in it:" | MARK | | T R! LOCK |
| MotherHourBeat | "This day I bore {father} {childWords}, {names}{, in place}. {mark} {account}" | beat | father, childWords, names, place, account | T |
| FatherBeat + presence ×2 | "{mark} {mother} bore me {childWords}… I was there for it." / "I was not there; word of it found me on the road." | beat | mother, childWords, names, place | T — deliberately NO account body (privacy in code) |
| ParentFeastBeat / WitnessFeastBeat | "This day we kept the feast for {names}…" / "This day I stood at the feast {parent} kept…" | beat | names, place, account | T |
| GriefBeat + twin clause | "{mark} it was mine and {other}'s{, in place}." + "Its brother or sister lived…" | beat | other, place | T — hand-written, never a model |
| FullAccount head + stillborn ×2 + witness list | "That day, in {place} — {mother} bore {childWords}, {names}." / "Not all of them lived." / "None of them lived." | resolver | many | T |
| hour-privacy refusal | "Of the hour itself I know nothing, nor should I — that belongs to the two of them." | resolver | | T |
| father's framing | "This is how she told me of that hour, in her own words:" | resolver | | T |
| RollEntry / ChronicleEntry frames | "{names}, born to {mother}…" / "(The father was away.)" / "Stillborn: {n}." / "The feast: {tier} ({n} denars)." / "-- The hour (the parents' own) --" / "-- The feast --" | resolver/file | many | T |
| BuildBirthPrompt (~16 fragments) | "You are the chronicler of this house, and you set down the hour a child came… IN HER OWN VOICE" + Scripture register (Rachel/Hannah/Elizabeth, FEAR NOT) + both-halves rule ("do not skip past the pain") + THE METHOD (image-carried) + two-halves SHAPE + FIVE TO EIGHT ask + place variants + no-prophecy-over-the-cradle + facts-not-phrasing + end-on-child | chron | mother, father, place | T very heavily |
| BuildFeastPrompt (~14 fragments) | chronicler frame + "the feast a house kept for a child of its own" + six-to-twelve + witness variants + "THE NAME SPOKEN ALOUD" + father-not-there rule + stillborn rule + date/numbers + "NOTHING about what this child will one day become" | chron | witnesses, place, flags | T very heavily — NEVER hand it the hour (privacy rule in the parameter gating, documented) |
| AppendFacts (~18 labels) | "- {name} — the mother…" / "- The child: … already named {names} — that name is settled, and you must use it and invent no other" / "- One other came with it and did not live." / "- This is their FIRST child…" / "- They have been wed {phrase}." / "- What lies on them just now: …" / "- The feast was NOT kept on the day of the birth: …" | chron | ~18 slots | T; forFeast gate decides which facts each prompt may see (privacy + retry-drift, documented) |
| AppendTongueRule (+ alphabet rule) | "…Write the names of people and places in the LETTERS OF THAT TONGUE…" | chron | recentWords | T |

### NightText.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| NightBeatMark | "Of that night between us:" | MARK | | R! LOCK |
| NightNameMark | "And I keep a name for it:" | MARK | | R! LOCK (ExtractNightName parses after it) |
| ChildNewsMark | "Word went round the company:" | MARK | | R! LOCK |
| PlainBeat / NamedBeat | "{mark} {partner} came to me{, in place}." (+ name clause) | beat | partner, place, title | T |
| ChildNewsBeat ×2 (wife/other) | "{mark} {mother} is with child, and it is {father}'s." | beat | mother, father | T — hand-written, never a model |
| RollHeader | "The nights lately, as I have known them:" | situ | | |
| LineFor (~10 variants) | "Tonight/{when}{, in place}, he came to me[ — the dearest of them all, the one I keep as "{title}"]." / door-closed / elsewhere (seen vs hearsay + other-night name) / alone / war / unknown | situ | when, place, title, other | T heavily |
| RunLine (~6 variants) | "From {a} to {b} he came to me nearly every night, plainly and without ceremony." … | situ | span, count kind | T ("nearly every night" claimed only when true) |
| BuildReckoning (~12 fragments) | "Reckoning the last thirty days as I have known them: he came to me {n}… and {m} of those he made something of. I heard he slept alone… I heard he was with {her}… — and for her he made a night the whole house is still talking about…" + "And on one of those nights our child was begun." | situ | counts, names, prices, titles | T heavily — leak detail scales with OtherNightPrice |
| WhenPhrase ×5 | "tonight" / "last night" / "{n} nights ago" / "about a fortnight past" / "some weeks past" | situ | days | T |
| KeepsakeEntry frames | "=== {title} ===" / "Laid out for it: …" / "Her season: …" / "And a child was begun that night." | player file | many | T |
| OddsLine ×2 / CustomDaysNotice ×2 / Percent | "{wife} — the chance stood at {p}%…" | player | | T |
| Facts labels (~16) | "The truths of this night:" / "- {name} — his wife…" / "- How the day found her: …" / "- Her body's season: …" / "- She is already carrying his child…" / "- What he brought to it (the bare facts of it, not words to reuse): …" / "- Since he last came to her: …" | chron | ~16 slots | T |
| ImageDeck[18] | "a dove startled in the clefts of the rock…" … "the threshing floor, and the heap of wheat set about with lilies" | palette | | T (per-card hash draw guarded — "consecutive nights are collapsing onto the same hand") — longer deck is strictly better; ORDER matters (hash indexes it) |
| SentenceRange | "THREE TO FIVE" / "exactly {N}" | chron | tier sentences | T |
| AccountCharBudget | 340/sentence, floor 1600 | output gate | | T (Cyrillic sizing) |
| BuildStoryPrompt (~20 fragments) | "You are the chronicler of this house, and you set down one night of a marriage — not the wedding night…" + Song register + both-halves + THE METHOD + HOLD THE SCALE + "TITLE: \<three to six words…\>" + TWO HALVES (coming-to-it / "NOT a door politely closed. Stay in the room with them.") + kinds-not-list + image hand ("Tonight these are nearest to hand: … Take ONE, perhaps two") + whole-sentences + place variants + humor/season carry + no-child-unless + facts-not-phrasing + past-names steer ("Do not reuse them… not one evening repeated") | chron | wife, partner, images, past names, tier range | T VERY heavily; live-probed (named images come back verbatim — the deck exists for this) |
| TongueRule (+ alphabet rule) | "Write the TITLE and the account in the SAME TONGUE…" | chron | recentWords | T |
| TitleLine regex | "title|name|заглавие|имя :" | MARK (output-parse) | | R! (parses the TITLE line the prompt asks for — bilingual on purpose) |
| CleanTitle / LooksLikeANight (≥60) | — | output gate | | T |

### BattleText.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| BeatMark | "Battle is behind us, and I set it down in my mind:" | MARK | | T R! LOCK |
| PlacePhrase ×7 arenas ×2 (named/bare) | "near {place}" / "at the walls of {place}" / "on the waters off {place}" / "at the den near {place}" / "among the fields of…" / "in the open field" | situ | place | T |
| ForgeTitle (~28 templates) | "The Storming of {place}" / "The Grand Victory near {place}, over {odds}" / "The Dear-Bought Victory…" / "The Skirmish Won…" / "The Fall of…" / naval + sally + raid + village + hideout families | title (persisted in records, quoted in beats forever) | place, odds, cost, scale | T heavily — a changed template changes only FUTURE titles, but titles live in saved JSON + beats |
| OddsWords ×4 | "Four Times Our Number" … "Half Again Our Number" | palette | | T |
| SideLine + SeasoningWord ×4 | "{n} — 30 foot, 14 bows… ; seasoned hands (about 2.7 of 6)" | resolver | roster | T |
| CostClause | "{n} fell, {m} were wounded, {k} broke and ran" / "not one was lost" | resolver | counts | T |
| MeetingVerb ×7 | "we fell upon" / "we stormed the walls held by" / "we met, on the water," … | situ | | T |
| OutcomeClause ×3 | "and broke them" / "and were broken" / "and parted, both sides bloodied…" | situ | | T |
| ShortTale | "With {n} {verb} {foe} — {m} strong — {place}, {outcome}." + losses/chains/freed clauses | situ | many | T |
| BeatLine (~10 clauses) | mark + place/time + verb + odds + "By my own hand I struck down {n}." / "None fell to my own hand that day — my part lay elsewhere." / "Of single hands no tally was kept in that press." + own fate ×4 + player's deeds/fate + "The chronicle keeps it as '{title}'." | beat | record, me, player | T heavily |
| FullAccount (~12 lines) | "'{title}' — {date}." / "Ours: …" / "The day was ours." / "The cost — ours: …" / "Deeds of note: …" / "Spoils worth some {n} denars…" / "The purse and the name: …" | resolver | record | T heavily |
| RollEntry | "'{title}' — {date}: {tale}" | situ | | T |
| SituationBlock frames ×4 | "Battles I have lived through at {player}'s side, as the chronicle keeps them:" / "- …and before these, {n} earlier battles besides." / "Of the last of them, the full tale still fresh in my mind:" / "Any of the older ones I may call back whole, by its name…" | situ | player, count | T |

### JourneyText.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| road header | "The road we have ridden of late, as I saw it myself:" | situ | | T |
| OneLine | "In the town of {X} ({when}, we stayed {stay}): sold for…, hired…, left … on the walls…" / ": we only passed through." | situ | visit | T |
| Detailed + DoingsSentences (~8) | "Our latest stop — in the town of {X}, {when}, where we are still." / "We sold goods worth {n} denars ({chief} the chief of it)." / "{n} captives were sold to the ransom broker." … | situ | visit | T |
| AtPlace ×4 / When / Stay ×4 | "upon the road" / "in the {kind} of {name}" / "an hour or two" … | situ | | T |
| tasks headers ×2 | "Tasks we carry:" / "Lately settled:" | situ | | |
| OpenLine | "'{title}' for {giver} (taken {when}; {n} days given, about {m} remain)" / "…the time is all but run out" | situ | quest | T |
| Outcomes ×5 | "succeeded" / "failed" / "failed — the time ran out on us" / "failed — and by our own broken word at that" / "set aside — the matter ended on its own" | palette | | T |
| StopBeatMark | "The road goes on, and I set the stop behind us down in my mind:" | MARK | | R! LOCK |
| TaskBeatMark | "I set it down in my mind:" | MARK | | R! LOCK |
| StopBeat / TaskTakenBeat / TaskSettledBeat | "{mark} {head}. {doings}" / "{mark} we have taken a task upon us, '{title}'…" | beat | visit/quest | T |

### CourtshipText.cs (+ Road, Misgivings, Seed)

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| RoadSection stage sentences ×4 | "Where my heart stands with {player} on the road toward marriage: warmth has taken root in me…" / devotion / ready / betrothed (+ blessing missing/given clauses) | sheet | player, head | T heavily |
| misgivings list frame | "What weighs on my heart about a life wed to {player} — set down by my own hand…" + per-item "(this still stands in me)" / "(laid to rest: {note})" + "This list lives with me: …while any of them still stands, I will not give my hand; when none stands, no doubt of mine bars the road…" | sheet | player, list | T heavily — the anti-exploit anchor |
| weighed-none frame | "I have sat with myself… found no misgiving standing — my heart is clear on it." | sheet | player | T |
| unweighed frame | "I have not yet sat with myself over what a life wed to {player} would truly ask… a few, five at the very most…" | sheet | player | T |
| waiting-to-be-asked | "So I wait now to be asked. Let {player} speak the word of marriage… the sealing of it is theirs alone…" | sheet | player | T |
| SuitorTerms (~6 fragments) | "A suitor stands before our house: {bride}, {kin}, is promised to {player}… reckons near {n} denars… never below {floor}, never above {ceiling}… I do not speak these numbers aloud… never volunteer my lowest… my word is not for sale to one I hold in contempt." | sheet | bride, kin, player, reckoning, floor, ceiling | T ("I do not speak these numbers aloud", "never volunteer my lowest") — the Sibuga anti-leak engineering |
| ForwardRefusal ×7 | NoRoadFurther/TooSoon/HeartNotThere/StationTooFar/MisgivingsUnweighed/MisgivingsRemain/TrothTooFresh + default | resolver | player | T — NUMBERLESS by design (any rail she reads becomes her next sentence) |
| SeededBeat / StepBeat / StepBackBeat ×2 | "I look back on all that has already passed between {player} and me…" / "My heart has taken its own step…" / "By my own hand I have taken back my promise…" | beat | player, stage, word | T |
| BetrothalSealed/Declined/Blocked, WeddingSealed/Declined/Blocked | "I laid my promise before {player}, and they took it by their own hand: we are betrothed…" | beat | player, reason | T |
| BlessingSealed/Declined/News | "I gave my word: the match between {bride} and {player} carries the blessing of our house, and {n} denars passed…" / "Word reaches me: {head}… has given {their} blessing…" | beat | bride, player, price, head, gender | T (possessive gender-correct — recorded forever) |
| StagePhrase ×6 / StageName ×6 | "warmth has taken root — I like them…" / "warmth"/"devotion"/… | palette | player | T |
| CourtshipMisgivings.CanonicalAction (~45 synonyms → 5 acts) | set/write_down/record/… → set_down; resolve/answered/lay_to_rest/… → settle; … | MARK (output-parse) | | T R! — the second line of defense behind the schema enum |
| CourtshipMisgivings.IsNone words ×5 | "none" / "nothing" / "no misgivings" / "i hold none" / "my heart is clear" | MARK (output-parse) | | T R! |
| CourtshipSeed.BuildPrompt (~12 fragments) | "You are a quiet reader of one soul's heart… judge from {her} own remembered words alone… be neither hopeful nor stingy…" + stage glossary ×5 + "Answer in exactly two lines: STAGE: … WHY: …" | util | npc, gender pronouns, partner, summary, self, excerpt | T |
| STAGE:/WHY: labels + stage words | — | MARK (output-parse) | | T R! |

### TogetherLine.cs / tiers / ToolLoopRunner

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| TogetherLine.ListHeader | "From this moment until now we had not sat in a private discussion like now, here is what happened since then:" | situ | | T ("a private discussion", "From this moment") — third and final wording, cut down deliberately; entries "· {date}: {text}" |
| WeddingTiers.ChroniclerNote ×5 | "It was a plain wedding, paid for with a hundred denars…" … "…THE WHOLE TOWN feasted…" | chron palette | | T ("the hall must grow with the purse") |
| BirthTiers.ChroniclerNote ×5 | "A hundred denars: bread and salt and a fire, and nobody called…" | chron palette | | T |
| NightGifts.ChroniclerNote ×4 | "wine and bread set aside beforehand; ten denars' worth — small, and chosen" | chron palette (BARE NOUNS by design) | | T R! (a test fails if a note grows back into a written sentence — finished prose comes back verbatim) |
| ToolLoopRunner.NothingSurfaces | "Nothing surfaces — search as you may, that memory will not come just now." | resolver | | T |

---

## D. Module builders — situation, persona, family, trouble, thought-facts, files, config

### SituationBuilder.cs (~50 fragments)

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| Timestamp format | "1084.02.03 13.24 (Summer 3, Year 1084)" + Seasons[4] | situ/stamp | clock | R! (stamps recorded turns; TurnStamp/window reuse) |
| TimeOfDay ×8 | "the dead of night" … "night" | palette | hour | |
| Place fallbacks ×2 | "the road" / "the open field" | situ | | |
| PlaceDescription ×3 + SettlementType ×4 | "in the {type} of {name}, held by {holder}" / "upon the road, away from any town or castle" | situ | settlement | |
| opening line | "This moment finds me, {name}. It is {timeOfDay} — {stamp} — and I am {place}." | situ | name, time, place | T-adjacent (asserted via prompt tests) |
| BuildBody ×3 | "My body is sorely hurt — my strength stands near {n} in 100, beneath the mark where one can hold a battle line…" | situ | percent | |
| TheirBody ×3 | "My eyes see plainly that {them} is sorely wounded…" | situ | them, percent | |
| meeting lines ×4 | "And now {them}{, kin,} comes to me." / "…comes to speak with me." / nearby / apart | situ | them, kinship | T ("And now Vulgrim, my husband, comes to me.") |
| wedded intimacy line | "Between us there is no ceremony and nothing held back for propriety's sake: we are wed…" | situ | | |
| FirstSightOfStranger ×2 heads + close | "I am a crowned head among my people, and this caller stands far beneath my station…" + "No word of their deeds has ever reached me." + "What welcome such a one merits… is mine alone to judge, by my own nature." | situ | garb, arms, banner, following | T (fame line mirrored in StorySeedFormatter) |
| GarbWords ×6 / ArmsWords ×5 / BannerWords ×3 / FollowingWords ×5 | "in splendid armor fit for a great lord" / "masterwork arms" / "a storied banner at their back" / "at the head of a warband ({n})" | palette | tiers, counts | |
| DescribeSelf: governor | "The keeping of {kept} — its walls, its garrison, its people — is given into my hands…" | situ | place | |
| DescribeSelf: pregnant | "I carry a child within me." | situ | | |
| DescribeSelf: war-kit | "My own gear, the whole of it: for arms I own {list}, and nothing besides; on the field I go {garb}; and for the road I have a {horse} of my own." | situ | items | anti-phantom-bow clause is load-bearing |
| DescribeSelf: wage | "My keep in this service is some {n} denars a day, paid from my captain's purse." | situ | wage (TroopWage!) | |
| DescribeSelf: company ×6 | "A warband of some {n} souls rides under my command…" / caravan rounds-and-ledgers variant / "I ride with {leader}'s {company}, some {n} strong, and I serve as its {duty}." / "I am held captive, a prisoner." / "I am upon the road." | situ | leader, counts, duties | |
| DutySentence ×4 | "As its scout, the road, the pace of the march… are mine to judge; my eyes are {word} at the craft." | situ | craft word | |
| HeldDuties | "In my company, {X} is my {duties}…" | situ | names | |
| army lines ×3 | "More than that: the banners of {army} march at my word." | situ | army, leader | |
| siege/raid lines ×3 | "And a shadow lies over this place: {name} is under siege even now." | situ | place | |
| DescribeOther head ×3 | "{them} is a man of some 30 years — a Khuzait free adventurer." (gender FIRST — gendered-tongue fix) | situ | gender, age, culture, station | |
| HouseLine + sworn/contract ×2 | "Their house is clan {clan}, sworn to {kingdom} — {fame}." | situ | clan, kingdom, tier | |
| renown lines ×2 | "Their name is carried far across Calradia…" / "I have heard their name spoken before now…" | situ | renown | thresholds shared with StorySeedFormatter tiers |
| heart-standing line | "Where my heart stands toward them: {word} ({n})." | situ | relation | |
| banner/war/peace ×3 | "We stand beneath the same banner." / "Our peoples — X and Y — are at war." | situ | factions | |
| PlayerStation ×7 | "free adventurer" / "sellsword captain" / "crowned queen"/"crowned king" / "noblewoman"/"nobleman" / "free captain at the head of a warband" | palette | | |
| ClanStandingWords ×7 | "a banner newly raised, its name not yet known to anyone" … | palette | tier | |
| PrettyOccupation (~12) | lord→"noble", wanderer→"wanderer and sellsword", gangleader→"leader among the streets"… | palette | | |

### PersonaBuilder.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| SpeechStyles[12] | "Terse and blunt; short sentences, dry wit, no flattery." … | palette | | T-adjacent (styles quoted in tests) — hash-indexed: ORDER IS IDENTITY (reordering re-voices every NPC in every campaign) |
| BuildRole heads ×4 + clan/sworn + gender-age | "A {culture} noble of clan {clan}, sworn to {kingdom} — a woman of some {age} years." | sheet | culture, clan, kingdom, gender, age | |
| OccupationHead (~13) | "A {culture} tavern-keeper" / "A {culture} ransom broker" / … / "A {culture} character" | palette | culture | |
| TradeKnowledge ×8 + caravan | "All the town's talk passes my counter: who is hiring, who is for hire…" | sheet | | |
| VillageLivelihood | " Our life and bread is the {primary} we send to market, beside some {other}…" | sheet | productions | |
| SellswordTerms (2 big sentences + fixed-price variant) | "I live by selling my sword, and I know my own terms before anyone asks: my service is honestly worth some {worth} denars… my keep thereafter is {wage} denars a day — … The hiring price alone can be bargained, and I keep my bounds to myself: never below {floor}, never above {ceiling}… never volunteer my lowest…" | sheet | worth, wage, floor, ceiling, percent | heavy interpolation; seller's-mind engineering (Sibuga) |
| DescribeRelation ×7 | "devoted friend" … "bitter enemy" | palette | relation | |
| BuildPersonality trait pairs ×10 + fallback | "honorable/deceitful, daring/cautious… " / "Unremarkable temperament." | palette | traits | |

### CraftsBuilder.cs / FamilyBuilder.cs / TroubleBuilder.cs / ThoughtFacts.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| Crafts line lead | "What my hands and wits are honestly good at: {word} in {skills}; …" | sheet | skills | T |
| craft Word scale ×6 | "green"/"middling"/"able"/"fine"/"masterly"/"among the finest in Calradia" | palette | value | shared by sheet + duty sentences + recall_person |
| Family header | "My kin and house, close to me:" | sheet | | |
| parentage ×3 / spouses ×3 / children clauses / siblings ×2 / clan ×4 / member-roll | "I am the daughter of {X} and {Y}." / "I am wed to {X}, a woman of some {n} years." / "My children — with {X}: {list}." / "My clan is {C}, led by my husband {L}." / "Among its people are {list}." | sheet | ~15 slots | polygamy-honest (ExSpouses); dedupe rules baked in |
| KinshipTo (~12 words) | "my wife"/"my husband"/"my father"/…/"the mother of my children"/"the head of my clan"/"my liege"/"my kinswoman" | palette | gender | used by situation arrival appositive too |
| DescribeFamilyOf (third-person mirror, ~6) | "{name} is wed to me." / "He is father to children — …" | situ | beholder="me" | |
| MemberClause/ChildClause doings (~8) | "governor of {X}" / "leading a warband of her own" / "a babe in arms" / "a girl of {n}" / "a man grown, of {n}, riding with {L}" | palette | | |
| Trouble: no-issue notable | "No true trouble weighs on me in these days — nothing worth hiring a fighting company for…" | situ | | |
| Trouble: issue telling ×4 | "A trouble weighs on me in these days — the matter of “{title}”." / "When any ask after it, this is how I tell it: “{brief}”" / "What I asked of them, in my own words: “{ask}”" | situ | title, brief, ask (game text) | |
| Trouble: solving-state ×4 + progress ×2 + given quests ×2 | "{player} has taken this burden up at my asking." / "Some {n} days remain before the chance is lost." / "And there is the matter of “{title}”…" | situ | player, quest | |
| ThoughtFacts labels ×5 | "Who I am: …" / "Who they are: {them} — …" / "What {them} is good at: …" / "How {them} stands toward me: {word} ({+n})." / "Where we both are: {place}. The hour: {stamp}." (+ apart variant) | util | many | T-adjacent (PlayerThought tests) |
| ThoughtFacts Bond fragments (~8) + StationWord ×8 | "my wife"/"the {duty} of my own company"/"a wanderer, free to be hired"/"a lady of the {clan} clan, under the banner of {f}" / "one of the folk" | palette | | |

### PromptFiles.cs / ModConfig.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| GlobalTemplate | "# Immersive AI - Global Prompt: how to shape your whole world…" (all comment lines; example lines a player may uncomment become LLM text) | file template | | R! (names the "Of this world, this I know:" fold-in — keep in sync with PromptBuilder) |
| Npc template | "# Immersive AI - Custom instructions for {npc}…" | file template | npcName | R! (names "Of myself, this I hold true:") |
| PresetsTemplate | "# Immersive AI - your conversation presets…" | file template | | |
| SparkStampPrefix | "# spark:" | MARK | | R! LOCK — presence = sparked/declined; two stamp comment lines interpolate {when} |
| ModConfig.AtmosphereLine default | "I am {name}, a living soul in the world of Calradia in feudal times." | sheet | {name} token | R! (Normalize migrates only the EXACT legacy string) |
| ModConfig.RoleplayGuidance default (2 bullets) | "- My words carry the feel of these old feudal days… - Above all, I live here, and I am glad of it…" | sheet | {name} token | |
| LegacyAtmosphereLine / LegacyRoleplayGuidance | "You are {name}, a living soul…" | LEGACY MARK | | R! LOCK — migration comparators, never editable |

---

## E. Module `Tools\` — schema text + resolver prose

### Tool definitions (all LLM-visible as the API tools array)

| tool | description first words | params (each has its own description) | flags |
|---|---|---|---|
| recall_person | "Call to mind what is truly known of a person of the world…" | name | |
| recall_company | "Take stock of my own company — the warband I lead or ride with…" | (none) | |
| recall_place | "Call to mind what is known of a town, castle, or village…" | name | |
| recall_clan | "Call to mind what is known of a clan or noble house…" | name | |
| recall_realm | "Call to mind what is known of a realm or kingdom…" | name | |
| recall_troop | "Call to mind what is known of a kind of soldier…" | name (with examples) | |
| recall_market | "Call to mind the day's trade in the market about me…" | item (optional) | |
| survey_surroundings | "Cast my eyes over the country about my company — every band, caravan, and army…" | (none) | |
| weigh_battle | "Set a foe upon the scales against my own company…" | name (optional, long description) | |
| seek_wisdom | "Search all I have ever read and heard tell of the world's ways…" | question; beyond (optional, 'yes' semantics) | R! (beyond parsed y/t) |
| move_heart | "Weigh my heart each time before I answer, and set down here — every reply, without exception — the honest measure…" | shift (calibration text: 0 full answer, ±1-3 small, more only what shakes) | T ("I never speak the measure aloud"); calibration IS the description |
| recall_battle | "Call back, whole, a battle I lived through at their side…" | battle (optional) | T |
| recall_wedding | "Call back, whole, a wedding day I lived through… (if the wedding was my own) the night that followed it, which is mine alone" | wedding (optional) | |
| recall_birth | "Call back, whole, the day a child of ours came into the world…" | birth (optional) | |
| strike_bargain | "Strike the hiring bargain: lay my agreed terms of service formally before…" | price (optional; bounds language) | |
| tend_courtship | "Tend the road of my own heart toward the one I speak with — the road toward marriage… I set each such arrival down in the very breath I feel it…" | move (**AllowedValues: closer/apart**); word (optional) | R! (parser honors closer/forward/deeper, apart/back/away) |
| bless_marriage | "Lay the blessing of my house on the match…" | price (optional); word (optional) | |
| weigh_misgivings | "Tend what weighs on my own heart about a life wed to… THE LIST LIVES WITH ME afterward…" | action (**AllowedValues: set_down/settle/release/revise/reopen** — description embeds the five words); misgiving ("THE MISGIVING ITSELF, never what answered it"); note ("never the misgiving itself") | T R! — the whole contract-lives-in-schema lesson; param names load-bearing |

### Resolver prose (what the model reads back — all first-person remembrance)

| file | family | count | notable strings | flags |
|---|---|---|---|---|
| WorldRecall | person miss/multi | 4 | "Search my memory as I may, no one called \"{name}\" comes clearly to mind." / "(Others in the world also bear the name: …)" | |
| WorldRecall | PersonRemembrance | ~10 | "{name} comes back to me: {role}, {culture} by blood, of some {age} years." / "They have passed from this world." / "Of their kin: …" / "Between me and them, the standing is {word} ({n})." / "Their crafts, as word has it: strongest in…" / "And they stand before my very eyes, clad in…, bearing…" | |
| WorldRecall | PersonRole ×10 | 10 | "ruler of her realm" / "a free captain, sworn to no crown" / "a wandering warrior who sells their sword" | palette |
| WorldRecall | Whereabouts ×3 | 3 | "Last word places them at {X}." (hearsay framing) | |
| WorldRecall | place | ~12 | "No town, castle, or village called \"{name}\" comes to mind." / livelihood / governor / "Its walls stand {word}." / intimate-vs-hearsay garrison ×2 / granary/fortunes (intimate only) / "Word is that it lies under siege even now." | intimate/hearsay split is design |
| WorldRecall | WallWords ×3, Loose rounding | 4 | "mighty, at their full third raising" | palette |
| WorldRecall | clan | ~5 | "Clan {X} comes back to me, led by…, sworn to…" / renown word ×4 ("famed across all Calradia"…) / holdings / people | renown tiers shared with seed/situation |
| WorldRecall | realm | ~4 | "The realm of {X} comes back to me, ruled by…" / "It is at peace, for now." / "It wages war against …" | |
| WorldRecall | troop | ~8 | miss + culture-suggest ("that people names their fighters otherwise. Of them I know these kinds: …") / multi / "of the {ordinal} rank of seasoning — they fight ahorse with missile arms." / craft / gear / "With seasoning they may become …" / "They stand at the end of their road…" | |
| WorldRecall | market | ~6 | "I stand in no market — out here there are no stalls…" / "This day in {X}, {item} trades near {n} denars (one brought in to sell would fetch closer to {m})." / staples survey | staples id list (22) is a palette |
| WorldRecall | company | ~14 | "My company comes to mind as clearly as my own hand: {n} souls…" / "At my side ride …" / "Among them: …, and others besides." / prisoners / food ×3 / morale words ×4 + line / surgeon's ledger / wages+purse ×2 (leader vs quartermaster) / CompanyDoing ×12 errand phrases / army ×3 | |
| FieldCraft | survey | ~12 | "I have no company upon the map to look out from…" / pace + drags / "Moving in the country about, nearest first:" / "(And {n} other bands besides…)" / places header / dens header / empty-country line / "(My eyes are not the sharpest at this craft — trust the shapes, not the counts.)" | |
| FieldCraft | BandBrief | ~12 | kind ×6, counts by skill ×3, "— FOES" / "— friends, riding under my own realm's banner" / neutrality-is-law line + shelter clause / speed verdict ×3 | |
| FieldCraft | BandDoing ×12 | 12 | "even now their hands are in the sack of {X} — smoke stands over it" / behavior readings | |
| FieldCraft | PlaceBrief + states ×5 | ~8 | "— and IT BURNS: {raider} is at the sack of it even now" / siege / "— lately plundered: its folk are stripped and its fields blackened" / rebellion | |
| FieldCraft | DenBrief/DenName | ~4 | "a den of {SeaRaiders}, {n} brigands lurking within — FOES, …" | |
| FieldCraft | weigh | ~14 | "I set {them} upon the scales against {ours}." / "Mine: {n} souls. Theirs: {m}." / ranks / walls lines ×3 + "And walls are their own soldier…" / village lines ×3 / den lines ×2 + "a den is stormed by a chosen few…" / misses ×2 | |
| FieldCraft | Verdict ×5 + confidence | 6 | "They could not stand against me — the scales fall wholly my way… So I judge — and my eye for such judgments is {word}." | |
| FieldCraft | Whereabouts glue | 3 | "{distance words} to the {wind}" — DistanceWords ×5, winds ×8 | palette |
| WebWisdom | SagesSilent | 1 | "I search all I have read and heard, and nothing rises to answer…" | |
| WebWisdom | result frame + closings ×2 | 3 | "It comes back to me — things read and heard over the years. The telling is in a strange tongue, of another world, but the substance is mine to take:" + own-world closing ("none of the strange terms — titles, numbers of versions, talk of screens and keys — pass my lips…") + beyond closing | the closing IS the fourth-wall defense — keep |
| HeartTool | Felt / Held | 2 | "It is felt, and it is mine — my heart has moved…" / "I look within, and my heart holds where it stood. I speak on." | |
| ChronicleTool | answers | 5 | "I search my memory, but no battle at their side is set down in it." / "It returns to me whole, as the day itself:" / "And my own part in it: …" / roll header + overflow | |
| NuptialTool | answers | 4 | "It returns to me whole, as though I stood in it again — our own wedding day:" / roll | privacy enforced by includeNight flag, not words |
| CradleTool | answers | 4 | "It returns to me whole, as though it were happening again — my own child:" / "The children whose coming I saw, oldest to youngest:" | content-not-existence gate |

---

## F. Behavior partials — inline prompts + resolver prose + fact-phrases

### ImmersiveChatBehavior.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| RefineSearchQueryAsync system | "You sharpen web search queries. Answer with the search query alone — one line…" | util | | |
| refine ask ×2 (game/beyond) | "A character inside the video game Mount & Blade II: Bannerlord wants to look up… starting with: Mount and Blade Bannerlord." | util | question, recentContext | the ONLY place the game's name is spoken to a model |
| ResolveBargainLay (~10 lines) | "This is not the moment for hiring terms…" / "The offer already lies before them, laid this very breath…" / below-floor refusal ("…nearer my true worth of {basePrice}, and I yield further ground only as they earn it") / above-ceiling / purse-short / company-full / bound-elsewhere / not-here / laid ×2 (live/letter: "The terms are laid: {price} denars for the hiring…") | resolver | price, basePrice | refusal deliberately does NOT name the floor (Sibuga) |
| GatherSparkFacts WherePhrase | "met at {place}" | util glue | place | |
| ApplyTokens | replaces "{name}" in Atmosphere/Guidance | token | | R! ({name} is the documented user-facing token) |

### ImmersiveChatBehavior.Courtship.cs

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| TrothBlockForNpc ×11 | "one of us is already bound in marriage; this road is closed while that vow stands." / promised-elsewhere / too-young / not-the-custom / not-for-marriage / companion-barred / unhired-wanderer ("Were they to take me into their service first, the road to a wedding would open.") / not-here / war-camp / at-war / world-refuses + default | resolver | head name | |
| ResolveTendCourtship (~12) | "This is not the moment for the road of my heart — I stay in the talk." / "My heart already moved this very breath…" / "I did not truly move — my heart holds where it stands." / step-back confirm ("…I owe no explanation beyond what I choose to give.") / "The world stands in the way: {block}" / wedding-not-by-letter ("A wedding day is not laid on paper — such a thing is done face to face…") / laid ("The day is laid: when my words here are done, our wedding will stand before them…") / step confirm ("It is so, and it is set down: {stagePhrase}… I need not name the change aloud…{whatFollows}") / catch fallback | resolver | stage, player, block | |
| ResolveWeighMisgivings (~14) | "This is not the moment for the weighing of my heart…" / already-weighed-clear ("What I set down before still stands written — a clear heart is not declared, it is earned…") / none-set-down ("It is weighed and set down: nothing stands in me…") / set-down confirm ("It is set down, in my own words — {n} now stand in me. The list lives with me…") / settle-already / settle-miss (names her open list back) / release miss+confirm ("It is struck out — not answered, simply no longer mine.") / revise miss+confirm / reopen miss+confirm ("…better a doubt spoken than a peace pretended.") / no-action ("My hand did not close on anything — I must name what I am doing…") | resolver | counts, list | miss lines SAY which words were wanted — the never-silently-do-nothing rule |
| ResolveBlessMarriage (~10) | "This is not the moment for my house's blessing…" / already-laid / bride-gone / at-war ("no gold buys the blessing of an enemy") / no-reckoning / nonsense-price / below-floor ("…nearer the custom's reckoning of {n}, and I yield ground only as they earn it, step by step.") / above-ceiling / purse-short / laid | resolver | reckoning, price | below-floor names the RECKONING, never the floor |
| RecentExcerpt labels | "(inner) " / "{player}: " / "Answer: " | util glue | | feeds CourtshipSeed |
| NothingStandsNow() suffix | (appended when the last misgiving clears) — "…no doubt of mine bars the road…" family | resolver | | |

### Weddings / Births / Nights / Celebrations partials (chronicler fact-phrases)

| identifier | first words | kind | slots | flags |
|---|---|---|---|---|
| SettlementPhrase ×3 | "the town of {X}" / "the castle of {X}" / "the village of {X}" | chron glue | name | T-adjacent |
| BlessingPhrase ×2 | "given by {X}, the head of their house, for {n} denars" | chron glue | head, price | T |
| RoadPhrase ×4 | "their hearts moved toward this only in the last day" / "the last step of their courtship was taken some {n} days ago" / "they were betrothed this very day" / "they had been betrothed {n} days" | chron glue | days | T (sample in tests) |
| SeasonPhrase words (6 time + 4 season) | "in the deep of the night" … "as the light went" / "in spring"… | palette | clock | |
| PlayerStandingPhrase | "of the house of {clan}, sworn to {kingdom}" | chron glue | clan, kingdom | |
| RecentSpokenWords labels | "{spouse}: {line}" / "{player}: {line}" | chron glue | names | the TONGUE evidence |
| Births FeastDelayPhrase ×2 | "the child was born {n} days before it(, and is no longer a thing of hours)" | chron glue | days | |
| Births MarriedPhrase ×2 | "only a matter of weeks" / "some {n} days" | chron glue | days | |
| Nights SinceLastNightPhrase ×4 | "it has been a long while, or it is the first since the wedding" / "only a night" / "{n} nights" / "{n} nights, which is longer than either of them likes" / "more than a fortnight" | chron glue | nights | |
| Nights CircumstancePhrase ×4 | "they are at a siege, and the walls are close" / "there was fighting this very day" / "the company marches with an army" / "he is carrying a wound" | chron glue | | |
| Nights NightTimelineLine ×7 | "he came to me — the night I keep as \"{title}\"" / "the custom of women was upon me, and my door was closed to him" / "word reached me that he spent the night with {other}" / "he went to {other}, and not to me — they call that night \"{t}\"" / "I saw him go to his own bed alone" / "there was fighting, and I did not look for him" | situ (THE LINE entries) | title, other | T (TogetherLine tests) |
| Nights BattleTimelineLine | "we fought — {title}" | situ | title | T |
| Celebrations WitnessDetail ×6 + PartyDutyWord ×4 | "who rides with them as their {scout}" / "who rides with them as their companion" / "of their own company" / "of their own house" / "a notable of {place}" / "of the house of {clan}" | chron glue | duty, place, clan | T ("Yngvald, who rides as their scout") |
| LlmHealthCheck ping ×2 | "Reply with the single word: OK" / "ping" | util | | |

---

## Summary counts

| bucket | rows above | individual strings (palettes expanded) | LLM-visible raw text |
|---|---|---|---|
| A. Core Prompts | ~90 | ~185 | ~19 KB |
| B. Memory & formatters | ~30 | ~45 | ~10 KB |
| C. Chronicles + courtship + line | ~150 | ~340 | ~46 KB |
| D. Module builders | ~110 | ~230 | ~17 KB |
| E. Tools (schema + resolver) | ~95 | ~250 | ~25 KB |
| F. Behavior partials (LLM share only) | ~55 | ~110 | ~14 KB |
| **Total** | **~530 keys** | **~1,160 strings** | **~130 KB** |

Of these: **~30 palette/word-list families** (≈ 240 entries: speech styles 12, humors 20, muse deck 24, image deck 18, craft scale 6, relation words 7, clan-standing 7, player-station 7, occupations ~25, garb/arms/banner/following 19, time-of-day 8, distance 5 + winds 8, seasoning 4, odds 4, walls 3, morale 4, outcomes 5, stage words 6+6, kinship 12, station-words 8, intensity 3, presets 3, with-child 4, staples 22…). Everything else is **sentence-shaped prose with interpolation** (~920 strings).

**Do-not-externalize set** (must stay code, or export read-only): the ~25 MARK constants (letter-beat markers ×8 incl. 4 legacy, chronicle marks ×11, MeetingMarker, PonderNoteMark, MeetingSeparator, SparkStampPrefix, SUMMARY:/SELF:/FACTS:/GOALS:, "unchanged"), all output-parse vocabularies (CanonicalAction table, IsNone, TITLE regex, STAGE:/WHY:, closer/apart synonyms, yes/no/stay/go keywords), and the two Legacy config comparators. Editing any of these orphans recorded memories, breaks dedupe, or silently disables a feature.
