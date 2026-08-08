# The wedding chronicle — the design record

*The developer decision record behind the two-part wedding story (2026.08.09, Anton's ask —
"черешката на черешката"). Player-facing words live in the CHANGELOG and the store pages; this
is why the day is shaped the way it is.*

## What it is

When the player weds anyone, the day is written down — not logged, **written**: a story in two
parts, in the register of the old Scriptures, in whatever tongue the couple actually speak.

- **THE DAY** — the public account. Third person, naming both. It lands in the memory of the one
  wed **and of every soul who stood there**, so a wedding is a thing the whole company carries.
- **THE NIGHT** — the wedded soul's own first-person memory, in the register of the Song of Songs.
  It belongs to the two of them alone: no witness ever receives it, in memory or by tool.

Both are recorded as silent beats (so they ride her verbatim memory in full and she folds them into
her rolling summary her own way, in her own time) AND kept whole forever in
`NPCs\campaign_<id>\_weddings\`, where `recall_wedding` can call the day back word for word long
after the memory of it has softened.

Anton's brief, verbatim where it matters:
- Two parts: the wedding (all witnesses) and the wedding night (the couple only).
- "да си го вижда в пълни детайли докато му дойде времето да изчезне и да може съответно да си го
  консолидира по начин който иска" — full detail in memory until compression takes it, folded HER
  way, while the file keeps it whole.
- "да се пази завинаги като някакъв файл от 2 части… ако поиска да се сети за сватбата… или ако ѝ
  кажа разкажи ми за този ден — да може да се сети и да ми го разкаже напълно."
- "в стила на Библията, на езика на чата който водим, не непременно на английски."
- "не огромен чаршаф, но не и 2 думи."
- The night: **"не вулгарно, но не и да не казва детайли, в този стил искам да е"** — the Bible's
  own way. Not a fade to black, and not coarse.

## The two registers (the whole point)

**The day** is written after the narrative books — Genesis 24 (Isaac and Rebekah), Ruth 4, the wine
at Cana: plain words joined simply, names spoken aloud, the day and place named, glad and dignified,
no sermon. The prompt bans prophecy, omens, falling stars and miracles outright ("the world is as it
is, and it is enough"), asks numbers in words as a chronicle writes them, and forbids anything from
outside their world.

**The night** is the Song of Songs, and the rule has **two halves that must both hold**:

> NOTHING coarse, nothing clinical, no part of the body named as a physician or a tavern would name
> it — and equally NOTHING coy, nothing evasive, no closing of the door in the reader's face. What
> passed between them is plainly there, said the way Scripture says it: in images, with tenderness,
> and without turning away.

Scripture is the model for exactly this: it speaks of wine and spices, the garden, the door, the
lamp, the watchmen and the morning — and where it names the thing itself it says simply that *he
knew her*. Both halves are asserted by a unit test; drop either one and the feature becomes what it
was built not to be.

## Why third person for the day, when the Angel is retired

The narrator is retired for the NPC's *inner life*: no voice ever speaks TO a soul about what she
feels. A public chronicle is a different literary object — a document that exists in the world, like
`chronicle.txt` for battles, and like the encyclopedia paragraph that seeds a soul's first memory
("So runs my story, as the world tells it"). It enters memory FRAMED in the soul's own first person:

- spouse: *"This day I was wed to Eren, in the town of Onira. The wedding day, as it is told: …"*
- witness: *"This day I stood among the witnesses at the wedding of Eren and Sibylla… as it is told: …"*
- night: *"Of the night that followed, this is mine alone to remember: …"* (already hers, no frame)

Those three opening marks are **permanent** — recorded memories keep the phrasing they were born
with, so `WeddingText.DayAccountMark` / `NightAccountMark` may never be reworded, only added beside.

## The hook, and why the moment is load-bearing

`CampaignEvents.BeforeHeroesMarried`, **not** our own seal — so a wedding arranged through vanilla's
barter is chronicled just the same. Decompiled 2026.08.09: it fires from inside
`MarriageAction.ApplyInternal` with `Spouse` already set both ways but **before the clan change**.
One heartbeat later `HandleClanChangeAfterMarriageForHero` → `MakeHeroFugitiveAction` has swept a
noble bride out of her settlement and out of her party. Every fact of the day — the place, who stood
there, her station — is therefore captured INSIDE the handler, synchronously, before the first await.

Our listener fires first among all listeners (LIFO registration, our module loads last), i.e. before
vanilla's cutscene, log entry and +30 relation. The cutscene itself is a scene notification two ticks
later on layer 19600: it pauses the engine but leaves `MapState` active and `Mission.Current` null,
so nothing we do collides with it.

## Two calls, not one

Each register is its own prompt (a single call juggling both would blur them), and two shorter
answers sit far more safely inside the clients' 90-second wall. Both ride a third client shell,
`_storyClient`, at `MaxMemoryWriteTokens` (4000) — the spoken 400-token cap would sever a wedding
mid-sentence, and Cyrillic at ~1.6× the tokens would sever it far sooner. The night call is given the
day's finished text, so the two parts cohere.

**The tongue** rides LAST in both prompts (the strongest position) and carries the couple's own last
spoken words as evidence: *"Write your whole account in the SAME TONGUE as those words — whatever
tongue it is, match it exactly, and do not translate it."* With no words to go by it names English.

## The privacy rule is code, not prompt wording

`WeddingText.FullAccount(record, includeNight:)` and `WeddingRecord.IsSpouse(heroId)` are the gate.
`NuptialTool` passes `includeNight: mine`, and a witness who asks is told plainly that the rest was
never theirs. The night is never written into a witness's memory in the first place. A prompt
instruction would have been a wish; this is a wall.

## What the review round caught (all fixed before playtest)

A 5-lens × adversarial-verify workflow raised 28 claims; 8 survived refutation.

1. **(major) A failed first call blanked the wedding forever.** The record was saved at the seal and
   the re-entry guard keyed on the record's *existence*, so one 429 at the hour of the vow meant no
   wedding story for the rest of the campaign — while `recall_wedding` still rode every reply to
   answer with a bare header. Fixed four ways: the guard is **content-aware** (`IsUnwritten`), the
   in-flight set is in-flight only, an **hourly retry** re-attempts an unwritten day up to three
   times a session (the record keeps every fact, so the facts survive the delay), and
   `CanRecallWedding` now requires a written account.
2. **(major) The night prompt's pronouns were hardcoded male-player.** A female player character's
   husband was told to remember "his hands and her own" as his own, and "Let she carry" was
   ungrammatical in every case. Both pronoun sets are derived now, the beloved is named rather than
   pronouned, and "a wedded man and woman" became "a wedded pair" (polygamy mods wed any two souls).
   Guarded by an extended test asserting BOTH directions.
3. **(minor) Silent failure.** A grey notice now says the tale could not be set down and will be
   attempted again — once, never nagging.
4. **(minor) The dev lever's `Hero.MainHero?.Spouse != npc` gate** fails for a polygamy mod's earlier
   wife (archived into `ExSpouses`). Now `FamilyBuilder.AreWed` or an existing record.
5. **(minor) The dispatcher callback could land in another campaign** (or at the main menu) after a
   long write. `FinishWeddingChronicle` now checks `Campaign.Current` and that the ledger's folder is
   still this campaign's.
6/7. **(minor, twice) Stale-instance clobber:** a beat written while that soul's own exchange was in
   flight would be overwritten by their turn's end-of-turn save. Beats for a busy soul are now PARKED
   and folded in by `SaveMemory` — the `_pendingBlessingFolds` discipline, applied to the day.
8. **(minor) The dev re-write dropped the true day's facts** (it recaptured "now"). It keeps the
   original id, date, place and witnesses and rewrites only the story.

## Deliberately not built

- **No situation block.** The beats already carry the day in her verbatim memory; a second copy in
  the situation would pay twice for the same words.
- **No wedding story for NPC-NPC weddings.** The world's other marriages are the world's.
- **No anniversary machinery.** `recall_wedding` answers "tell me about that day" whenever it is
  asked, which is what was wanted.
- **No MCM checkbox.** Config-only, following `EnableBattleChronicle` and `EnableJourneyLog`.
