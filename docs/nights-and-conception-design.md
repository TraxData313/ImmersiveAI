# The nights of a marriage — design record

**Built 2026.08.09, Anton's design.** The wedding chronicle's own morning after: the day after the
wedding, the game went straight back to flipping a coin behind the player's back and handing him a
popup when it came up heads. This replaces that with nights he chooses, a woman's own month that
decides what may come of them, and a written account of the nights he pays for.

Sibling of `docs/wedding-chronicle-design.md` and `docs/marriage-courtship-design.md`. Read those
first if you are new here; the patterns are the same.

---

## The four rules everything else serves

1. **Conception is the player's doing.** Vanilla's nightly roll is skipped for his own marriages and
   re-cast on the night he chose.
2. **The odds are not ours.** They come from the game's own `PregnancyModel` and are only *spread*
   across her fertile nights. We invent no balance.
3. **Her door is hers.** Through the days of the custom the choice is not offered at all.
4. **She sees what she would see, and we never script the feeling.** Her hand has been on her own
   heart since `move_heart` shipped. The nights only give it something true to weigh.

---

## Taking conception over from the world

`PregnancyCampaignBehavior.DailyTickHero` → `RefreshSpouseVisit(hero)` → if nearby and
`MBRandom.RandomFloat <= PregnancyModel.GetDailyChanceOfPregnancyForHero(hero)` →
`MakePregnantAction.Apply`.

`Nights\PregnancyPatch` prefixes **`RefreshSpouseVisit`** (private) and answers "skip" for exactly
one kind of woman: one wed to the player (`FamilyBuilder.AreWed`), plus a female main hero. The rest
of Calradia keeps its own nights, and *deliveries* — a different branch of the same daily tick — are
never touched, so a pregnancy already begun always comes to term.

**Why not replace the model:** a `PregnancyModel` replacement fights every other mod that wants the
same seat, and silences the whole world rather than one household.

**It fails open and the mod is told.** No patch → `Applied` stays false → we do **not** roll either
(two systems reaching for the same child is worse than one), a plain notice says so once, and the
nights still happen and are still written.

### Three traps, all decompile-verified

1. **`GetDailyChanceOfPregnancyForHero` NREs on a null `Spouse`** — it reads
   `hero.Spouse.GetPerkValue(Virile)` with no check. Marry Anyone empties that slot constantly (it
   keeps exactly one wife in it at a time). So `VanillaDailyChanceFor` try/catches the model and
   falls back to `MirroredDailyChance`, our own copy of the same arithmetic. The model is always the
   *first* answer, so another mod's replacement still wins.
2. **`MakePregnantAction` records the father as whatever sits in `mother.Spouse` at that instant.**
   A second wife's child would be fathered by `null` and crash at the delivery **36 days later**.
   `EnsureFatherSlot` sets it first — the same move Marry Anyone's own daily prefix makes, and the
   only place this feature touches the world's family graph.
3. **Vanilla's "she has learned she is with child" fires at conception.** So the *delay* is achieved
   by deferring `MakePregnantAction.Apply` itself: the night rolls, the ledger holds the conception
   privately, and the world is told nothing until `ConceptionRevealDelayDays` (7) have passed. The
   birth moves the same days with it — mention that if the number is ever re-tuned.

`CampaignOptions.IsLifeDeathCycleDisabled` is respected everywhere.

---

## The odds

`MoodTides.Fertility(id, day)` — a curve, not four steps, keyed off the cycle `MoodTides` already
gives every woman (26–30 days, hers for life, deterministic in her id):

| day relative to the crest | weight |
|---|---|
| −5 … −3 | 0.25 / 0.45 / 0.65 |
| −2 / −1 / **0** | 0.85 / 1.00 / **1.00** |
| +1 / +2 | 0.35 / 0.10 |
| the days of the custom (1–5) | 0.00 — the door is closed |
| everything else | 0.03 |

**THE SPREAD IS OF HAZARD, NOT OF PROBABILITY** (corrected 2026.08.16 — Anton: "85% is very high for
a babe", and he was right):

```
H  = -ln(1 - V) × L                     the whole cycle's hazard, from the game's own daily chance
h  = H × f / MoodTides.CurveSum(L) × gift × dial
p  = 1 - e^(-h)
```

**The rule this serves:** taking every night of a cycle gives EXACTLY the monthly odds the world
would have given on its own — Σh = H by construction, so Π(1−p) = e^(−H) = (1−V)^L identically, for
every age, every gift and every cycle length. A test pins it to six places across four women, five
ages and both gift extremes.

*What was wrong before, and it is worth keeping because the shape of the mistake is a common one.*
The first cut shared the month's chance out additively: `V × L × f / FertileWindowSum`, so the nightly
chances summed to `V × L`. That is the EXPECTED COUNT of conceptions, not the chance of at least one,
and the two agree only while `V × L` is small. It is 3–4 here — three or four expected children packed
into eight nights — so the crest was forced to 66% and the jewel's doubling produced **173%**, a
number that is not a probability at all. `MaxNightlyChance` (0.85), whose comment calls it flavour,
was in fact the load-bearing clamp holding the model inside probability space; that is why the hearth
window read *"85% plainly, up to 85% with the grandest gift"* — both readings were over the rail.
Two smaller things fell out with it: the normaliser was `FertileWindowSum` (4.65) where the curve
truly sums to `CurveSum(28)` = 5.10, since the quiet days carry weight too — a further 9.7% too hot;
and the guard test's upper bound was `1.0`, so it could only ever fail if the mod were too BARREN
and could not see an overshoot at all. Both are fixed, and the test is now two-sided and exact.

For a childless wife of twenty-five (V ≈ 0.11): ~47% on the crest, ~23% four nights before, a fraction
of a percent in the quiet days, nothing while her door is closed; ~72% at the crest with the jewel.
Older or with children, the same curve sits far lower — the game's arithmetic doing the work, not
ours. `MaxNightlyChance` 0.85 remains, and now binds in only one corner (a wife of eighteen, the
Virile perk, and the grandest gift), which is what a flavour rail should look like.

**Worth saying plainly, because the number still surprises:** a young childless wife who is taken
through her whole season comes out around 96% over a month. That is VANILLA'S OWN figure for a wife
travelling with her husband — matching it is the promise. The dial for a calmer house is
`ConceptionChanceMultiplier`, which under the hazard form simply scales the exponent: halve it and
the month falls to ~81%, quarter it to ~56%.

---

## What coin buys (`NightGifts`)

The wedding tiers' little brother. 0 / 10 / 100 / 300 / 1000 denars.

| | odds × | **talk ×** | written? |
|---|---|---|---|
| nothing but yourself | 1.00 | **0.50** | no |
| a cup of wine (10) | 1.10 | 0.75 | yes |
| water, oil, a table for two (100) | 1.35 | 1.10 | yes |
| cloth for a gown (300) | 1.60 | 1.50 | yes |
| a jewel (1000) | 2.00 | **2.00** | yes |

**What the chronicler is TOLD of a gift is a handful of nouns, never a written sentence**
(2026.08.10). They began as finished prose — *"He had brought wine, a jug of it, and bread, set aside
for the two of them…"* — and finished prose handed to a model comes back almost word for word: every
ten-denar night in a marriage would have read the same by the tenth. Three rails now: the notes are
terse facts (a test fails if one grows back past 160 characters or starts writing sentences), the
prompt says out loud that everything it was given is **facts, not phrasing**, and it is handed the
NAMES of the last six written nights with one instruction — do not reuse them, and do not write the
same evening again in different words.

**The talk column is the sharpest edge in the feature** (Anton, 2026.08.09). A grand night is grand
precisely because people talk about it: servants carry water up the stairs, a woman wears a jewel
where everyone can see it. So the coin buys better odds *and* a written memory *and* your other
wives finding out — **including the night's name**. That last one (`NightRecord.OtherNightTitle`,
back-filled by `LeakTheNameOfTheNight` once the chronicler answers) is what makes polygamy a thing
to manage rather than a list.

And the morning: a paid night calls `MobileParty.SetDisorganized(true)` — slower party, worse battle
morale, no prisoners. Ordinary nights cost the road nothing, and say nothing about it.

---

## Two switches, not four modes

The first cut had a four-way `NightsAutoMode` behind one cycling button labelled "Change how the
evenings go". Anton's screenshot killed it: the label said nothing a player could act on, and the
mode's own description wrapped and slid under the button. It is two plain switches now, and they are
independent:

| | prevent OFF | prevent ON |
|---|---|---|
| **Visiting: Manual** (default) | asked at dusk; the window at any hour; gifts, written nights, her best days | you still choose everything, but no night is meant to make a child |
| **Visiting: Auto** | it looks after itself and goes to whoever is nearest her season | it looks after itself and goes to whoever, at a tenth the odds |

`NightsAutoVisit` and `NightsPreventChild`, both live, both in MCM. Wanting no nights at all is
`EnableNights = false` — where a player would actually look for it.

**Auto waits for the evening, and only for it.** It does not pounce the instant the hours are up: it
fires at `NightHour` and no earlier, so the whole day between belongs to the player. Come to the
window at noon, go yourself with a jewel and a written night, and the automatic one finds the clock
running again and stands down. **Auto is a floor under the marriage, never a ceiling on it** — and
because nothing is ever bought or written on an automatic night, wanting more than it gives is
always a reason to go yourself.

Nothing is ever bought or written on an automatic night. That is the trade for not being asked.

**An ignored question is an answer.** `CloseUnansweredNight` runs at the next dusk: if the ledger's
`LastSettledNight` does not cover last night, it is settled as a night slept alone, the other wives
roll for what they made of it, and the log says so. The mark is **night-level, not per-wife**,
because a night nobody noticed anything about writes no records at all.

**A wrapping label needs a measured margin.** The window's left column stacks the two switches in a
`ListPanel` and the wives' list below rides `ControlsHeight`, computed from the sentences' own
length — a fixed margin is exactly what clipped the words in the screenshot, and it is the letter
window's own old bug, now learned twice.

---

## What she keeps (`NightLedger`, `_nights.json`)

One record per wife per night, capped at `MaxNightsRemembered` (14) — a fortnight, which is exactly
enough to judge how the last two weeks of a marriage went.

`Together` · `DoorClosed` · `Elsewhere` (with whom, and by hearsay or not) · `Alone` · `Unknown`.

Awareness, rolled for every wife who was not the one visited:

- **With him**: the place decides (road 70 / village 50 / castle 30 / town 20), times the gift's talk
  multiplier, capped at 100.
- **Not with him**: she learns *only* that he was with another woman, at half those odds, and only
  within `WordOfMouthRange` (120). No "alone", no "unknown" — a wife three weeks away keeps no watch
  on his nights, and pretending otherwise was both wrong and expensive to debug (Anton's call).

`NightText.BuildRoll` renders it in her own first person: the freshest `MaxNightsToldInFull` (5)
written nights whole, older ones folded to the names she keeps them by, everything else one line, and
**a run of nights she never learned anything about collapsed into one honest line** — three "I don't
know" lines in a row read as an accusation and are nothing of the kind.

---

## THE LINE (`Core/Together/TogetherLine.cs`)

The most important thing in this batch, and it went through three cleverer versions before Anton
described the simple one: **one mark at the last moment the two of them had time to themselves, and
after it a plain dated list of everything that has happened since.** It closes the situation — the
last thing she reads before the arrival.

```
From this moment until now we had not sat in a private discussion like now, here is what happened since then:
· Winter 9: we were at the market in Baltakhand: sold for 1,240 denars, bought Wool ×24
· Winter 10: we fought — The Hard-Won Field near Ortysia
· Winter 11: he went to Тирсиф, and not to me — they call that night "Чашата кехлибарено вино"
· Winter 12: the custom of women was upon me, and my door was closed to him
· Winter 13: I saw him go to his own bed alone
```

**Why it exists.** Every event this mod records lands in her sheet as knowledge, and knowledge reads
as *settled background*. Without the line she greets him the morning after a night with another wife
as though it had all been talked through off-stage.

**What moves the line, and this is the whole of the rule: TIME ALONE.** A talk that has ended, a
night together of any kind, the wedding night. A battle does **not**. A market does **not**. Hearing
where he slept does **not** — those are exactly what the list is for.

**A running talk must not move it** (Anton's correction, and it is load-bearing): the sheet is
rebuilt for *every single reply*, so if her own first answer moved the line, the very thing she was
raising would vanish from under her halfway through raising it. So the talk side reads
`NpcMemory.LastTalkEndedDay` — stamped when a face-to-face conversation closes and when the chat
window lets a thread go — and falls back only to turns older than `TalkGraceHours` (8), which
catches a sitting that never announced its end.

**It disappears by itself.** No flags, no state: `Build` returns empty when nothing stands after the
line, so the moment you have talked it is gone, and it comes back the next time something happens.

**It was cut down THREE times, and the final cut is the point.** It began as a paragraph, became a
mark plus a closing sentence, and is now one divider and a list — Anton's call, to find out how much
a soul works out unaided (long rules make every soul answer the same; the reach-out ponder taught us
that once already). What went, and why:
• The opening mark naming when they were last alone: unnecessary. Every recorded turn already
  carries its own `[place, time]` stamp and every entry is dated.
• The closing sentence granting the word-in-passing and reminding her it was hers to raise: cut,
  because it also told her *how to use* the list.
**Two words carry all of that closing's work: A PRIVATE DISCUSSION.** They leave the door open that
light, passing remarks were made, while saying plainly the two of them never sat down to any of it —
which was the whole reason the flat "we have not spoken of this" had to go in the first place (it
read as estrangement, when the truth is only that the player was off playing).

**And "from this moment" points at the divider's own place in the reading**, which fixes a real
weakness in the version before it: the block stands in the sheet BEFORE the transcript, so a
backward-looking "since then" had nothing behind it yet to refer to. Here the mark IS the moment.
If a live sample ever reads as an accusation, or as a briefing recited straight back, the closing is
the thing to try again — but only after seeing what they do with this.

**The nights roll stops AT the line** (`NightsRollFor` filters to `GameDay <= line`), so nothing is
ever told twice — the double-read mistake the retired truths already cost us once.

**The player sees it too** (Anton's ask), and **it stands in its own place in the thread** — right
after the last exchange the two of them had alone, with everything that came after it below. Nailed
to the foot of the thread, "from this moment" meant nothing; drawn inline it is a real divider. It
falls back to the foot only when everything after the moment has already aged out of the verbatim
turns. Headed "— not yet discussed between you —", in the nights' dusk-violet.

**And a night reads in the chat, not just in the ledger.** A night's beat carries only its NAME, so
the thread fills the card from the ledger: the freshest `NightStoriesInThread` (3) written nights
are read in full under a ☾ header, older ones keep the name alone. Memory works that way anyway, and
the window of the hearth holds them all regardless.

---

## What memory keeps

**Beats are titles, not paragraphs.** A plain night leaves `NightText.PlainBeat`; a written one
leaves `NamedBeat` — the *name* only. The account itself lives in the ledger (five deep) and in
`nights.txt` forever. So memory keeps the essence, the ledger keeps the flesh, and a marriage of a
hundred nights does not silt her memory with a hundred paragraphs. The marks (`NightBeatMark`,
`NightNameMark`) are permanent, like every recorded phrasing in this mod.

Awareness entries are **never** beats — 14 a week across three wives would drown everything else.
They live in the roll, which she re-reads every conversation.

## The writing

One call per paid night, on `_storyClient` at `MaxMemoryWriteTokens` (a 400-token spoken cap would
sever it, sooner in Cyrillic). `NightText.BuildStoryPrompt`: the wedding night's Song-of-Songs
register — *nothing coarse and equally nothing coy*, both halves load-bearing — **made small**:
"THREE TO FIVE sentences… this is one evening of a long marriage, not the wedding happening again."
Contract is `TITLE: <name>` then the body; `TryParseStory` tames fencing, markdown and quotes, and an
answer too thin to be a night leaves the record standing without one. Failed nights retry a bounded
few times on the hour (`AwaitingStories`), guarded on **content**, never existence.

---

## Configuration

`EnableNights`, `NightHour` (21), `NightCooldownHours` (24), `AskEachEvening`, `NightsAutoMode`,
`CarefulNightChanceFactor` (0.10), `ConceptionChanceMultiplier` (1.0),
`ConceptionRevealDelayDays` (7), `ShowConceptionOdds`, `MaxNightsRemembered` (14),
`MaxNightsToldInFull` (5), the four `NightAwareness*Percent`, `PaidNightsDisorganizeParty`,
`MaxNightGift`, `EnableNightWindow`, `NightWindowHotkey` ("H" — the vanilla map already holds I, P,
C, N, K, Q and E). MCM carries the ones worth a dial, all live.

## The window (`UI\NightWindow\`, hotkey H)

The letter window's leaner twin: wives on the left with her season **in words** (a number only when
`ShowConceptionOdds` is on), her fortnight on the right, one "Go to her tonight" button that walks
the same road the dusk question walks, the two live switches, and a "?" page. Yields to the other two
windows; only one stands at a time. The dusk question offers "Let me look at my own house first",
which opens it.

**Its "?" page is the one place in the mod written for an operator, not a reader** (Anton, 2026.08.10:
"не бих чел, по-скоро бих експериментирал направо"). Short headed blocks, one fact to a line, and the
real numbers read live from the config — a page that says 24 hours is telling the truth about *that*
save. The immersive voice belongs to the souls; a help page nobody finishes helps nobody.

## Not built, deliberately

**She cannot refuse.** It was proposed and Anton cut it: a wife is willing, and it is the husband's
choice. The only refusal is the mechanical one — the days of the custom. Do not reintroduce it
without asking.

**Same-sex marriages are not modelled**, and a female player is only kept from crashing (`MotherOf`
picks whichever of the two can carry), not designed for. Anton's explicit call.
