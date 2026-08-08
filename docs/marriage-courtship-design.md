# Marriage by courtship — the design record

*The developer decision record behind the wedding-handshake feature (2026.08.07, Anton's ask).
Player-facing docs live elsewhere; this is why the road is shaped the way it is.*

## What it is

The `strike_bargain` mold applied to the biggest thing a bond can become: **words move the
heart, but only the player's sealed click betroths — and only a second sealed click weds.**
An NPC walks her own courtship road toward the player, step by step, by her own hand (a native
tool); she writes her OWN misgivings about the marriage when the talk truly turns that way, and
lays each to rest by her own judgment (v2, 2026.08.08 — see below; the generated, auto-checked
"quiet asks" of v1 were retired the day after they shipped); her noble kin may demand a
bride-price the player can haggle over with the clan's own head; and the wedding itself is the
real game's `MarriageAction`, so the whole world — kin lines, encyclopedia, tidings — knows it
truly happened.

Anton's original asks, all honored here:
- The hiring-bargain shape, applied to marriage (tool lays, popup seals, rules re-run at both).
- Family consent ON by default, skippable from MCM.
- Already-close couples marry easily; an emperor's daughter is near-impossible for a nobody —
  station rails with a small hard-earned slack ("трябва да е някакъв Дон Жуан").
- Waypoints ("стълбове"): likes him → really likes him → no questions left, ready → betrothed →
  wed.
- ~~Generated, personality-grounded requirements she KNOWS but never recites as a list — they
  surface as the talk turns, each checkable against live game data where possible.~~ (v1;
  replaced 2026.08.08 by her OWN written misgivings — Anton found the auto-checked
  requirements "robotic bargains with hammered-in rules".)
- **Wanderer/companion brides are first-class** (vanilla forbids them; here they are the POINT —
  the first real bonds are with companions). MCM toggle, default ON.
- The father's permission is itself a bargain: the game names its reckoning, the player can
  haggle within ±30% (MCM), gold seals it.

## The road (courtship stages)

`None → Warmth → Devotion → Ready → Betrothed → Wed`, persisted per NPC inside
`memories.json` (new `NpcMemory` fields) — so `RevertMemoriesWithSaves` rewinds a courtship
with the save for free, exactly like every other memory.

**She moves herself** along it via one native tool, `tend_courtship` (the `move_heart`
family): `move: "closer"` steps forward one stage when the rails allow; `move: "apart"` steps
back freely (a wound can break a troth — always her right; only Wed has no back-step).
Forward steps are rate-limited to one per game day, so no one sprints the road in a single
warm evening.

**The rails bind the hand, not the heart.** Warmth and Devotion are free feelings gated only
by her real regard (relation ≥ 0 / ≥ 20) — a princess may come to love a commoner. But the
step to Ready and both seals run the **station gate**: required player clan tier from HER
station (ruling clan's kin 6 · great house 5 · middling 4 · lesser 3 · notable's kin 2 ·
wanderer 1), minus `CourtshipCharmSlack` (default 2, MCM 0..4) — the Don-Juan discount,
earned only once her heart is fully won (full slack from Devotion→Ready onward, none
before). So love without marriage is a possible, honest tragedy; and the emperor's daughter
at slack −2 still requires a tier-4 clan. Ready additionally requires relation ≥ 40, a heart
that has WEIGHED its own misgivings at least once (even to find none), and no misgiving of
her own still standing.

**Refusals never name a threshold** (the Sibuga floor lesson — any number a rail speaks
becomes her next sentence). The tool answers her in her own mind's voice: "my heart is not
there yet", "the distance between our houses is more than my kin would bear", never a tier or
a figure.

## The road already walked — seeding from lived history

A soul with a REAL story (like Sibylla — seventy turns of gifts, night talks of the Word,
a mutual "обичам те", and a deliberate "нека не бързаме към клетва") must not start at
None just because the feature arrived after the love did. At the courtship machinery's
first touch of an NPC with meaningful history (rich memory, stage never initialized), a
one-time utility LLM call — "where does my heart already stand" — reads her summary, held
truths, custom instructions, and recent turns, and answers a starting stage in her own
judgment. Seeded up to Betrothed at most (a wedding exists only through the real seal);
fresh souls with no story are marked seeded at None without any call. The seeding is
recorded as a silent first-person beat ("I look back on all that has passed between us —
my heart already stands at…"), so it is visible and honest, never a hidden number. Rails
apply from there forward; the lived story is taken as truth even where the rails would
have walked slower.

## Every step is seen — the road's notices

Each stage change fires a battle-log notice like the tool-use notices (the left-side
stream): a soft line for warmth/devotion/ready ("Sibylla's heart draws a step closer"),
a solemn one for betrothal and wedding, an honest grey one for a step back. The player
always sees the road move — the feedback loop that makes the courtship feel alive.

## Her misgivings — written by her own hand (v2, 2026.08.08)

**The v1 matchmaker's ledger is retired.** It generated 2–4 "quiet asks" per soul, each tied
to a checkable predicate (`gold >= 2000`, `skill Riding >= 50`, `trait Mercy >= 1`, `heart`),
auto-judged against live game data with met-marks in her sheet. One day of play showed the
problem (Sibylla's ledger was the exhibit): the checkable stoppers turned every courtship
into a robotic bargain with hammered-in rules — the player farms a number instead of talking
to a person, and her "requirements" were never really hers. `MatchmakerLedger` and the
`CourtshipAsk` DSL were deleted whole; old `CourtshipAsks` JSON fields are ignored on load,
so such souls simply begin with an unweighed heart. The one hard worldly rail that remains is
the station gate — deliberately kept, per Anton.

**In their place: misgivings, hers alone — and the list LIVES** (Anton, same evening: "не
искам да остават Х статични… да може да си променя мнението или в разговора да видя друго,
което ме притеснява"). When marriage truly enters the talk and she has not yet weighed her
heart, the sheet invites her to pause and do it honestly — via the `weigh_misgivings` tool
(rides beside `tend_courtship`, open from the first word of romance — even at stage None):

- `set_down` — her doubts in her OWN words, one per line — or the single word `none` if her
  heart is clear. Either way `MisgivingsWeighed` is set: a clear heart is weighed too. The
  cap (**five**) binds only what STANDS OPEN at once — settled ones never block a new doubt,
  so a worry born mid-conversation always has room. Past ten carried in all, the oldest
  SETTLED quietly fade (history, not an endless ledger; nothing standing ever fades).
- `settle` — lays one to rest, with a light word on what answered it, kept beside it
  ("He has shown me his ledgers, and his word held"). Only SHE judges when life answered it.
- `release` — strikes one out entirely: not answered, simply no longer truly hers (it proved
  empty, or she changed her mind). Distinct from settle on purpose — struck-out ones leave
  no note and free their room.
- `revise` — rewords one that changed; `reopen` — a settled one returned to her.

She KNOWS what the list means — the sheet says it plainly: while any of them stands she will
not give her hand, and when none stands no doubt of hers bars the road. That knowledge is the
anti-exploit anchor: the road to marriage runs THROUGH the list, by her own bookkeeping.

Core `CourtshipMisgivings` (lenient fuzzy matching, unit-tested) owns the ops; nothing is
generated, nothing is auto-checked. The rails only COUNT: Ready and the betrothal wait for
`MisgivingsWeighed && OpenMisgivings == 0` (verdicts `MisgivingsUnweighed` /
`MisgivingsRemain`, refusals numberless as ever); the wedding lay re-checks neither (the
promise was proven when it was given). Unlike the v1 asks, these are **discussable openly** —
the sheet says so — and **player-visible**: the bond line carries "misgivings 2/4", and a
"Misgivings n/m" button in the chat window opens the full list with her settling notes.
Every movement leaves a line in the message log, in Anton's color language (2026.08.08
evening): **rose when the heart clears** (settle, release, a clear heart), **frost-blue when
something freezes** (a new worry set down, a settled one reopened — and the road's own
step-back wears the same frost now; only a broken troth stays red). Revise rides the neutral
activity sea-glass.

## The two seals

**Betrothal** — from Ready, `move: "closer"` LAYS the moment (nothing changes yet); after
her reply lands, a confirm popup offers her promise in her own laid words. Seal → stage
Betrothed, recorded first-person beats, and the news is real. Decline → she remains Ready,
and the closed moment passes through her memory honestly. A betrothal needs NO family
blessing — a secret troth is the couple's own; the road to her kin starts there.

**Wedding** — from Betrothed (after `MinBetrothalDays`, default 3, MCM 0..30), `move:
"closer"` lays the wedding; the popup names everything the world requires (the blessing's
state, the game's own suitability verdict). Seal → `MarriageAction.Apply` + companion
fix-ups + beats — and one spoken beat where she speaks her first words as a wife, delivered
like any reply. The marriage is REAL game state: FamilyBuilder's kin lines, the encyclopedia,
vanilla's own log stream (tidings!) all inherit it with zero extra plumbing.

Both lays and both seals re-run every hard rule (station, eligibility, days, blessing,
gold) — the one law, inherited: **talk alone never weds.**

## The father's blessing — the second bargain

With `MarriageNeedsFamilyConsent` ON (default) and a bride who HAS kin above her (a clan
whose leader is neither she nor the player), the wedding stays barred until her clan's head
blesses the match. The blessing is won in conversation WITH HIM — the bargain mold again:

- His sheet (only while a kinswoman of his is betrothed to the player) opens with the
  suitor's case: the game's own reckoning of the bride-price, his private haggling bounds
  (±`MarriageDowryHagglePercent`, default 30, MCM 0..90), and the seller's mindset — open
  above the reckoning, concede only what the talk earned, never volunteer the floor.
- His tool `bless_marriage` lays price + blessing → popup names the exact gold → seal moves
  the denars to him and writes the blessing into HER memory (a silent beat reaches her:
  word that her kin have blessed the match — even across the map).
- Factions at war → the blessing is refused outright (no gold buys an enemy's daughter);
  with consent OFF, or a clanless/companion bride, or a bride who leads her own clan, no
  blessing is asked of anyone.

## The night's additions (2026.08.08, Anton's asks before bed)

- **The road in the windows**: the bond-stats line in BOTH windows carries the stand ("heart's
  road: devotion" / "betrothed to you"), and the odds view lists it per soul.
- **The betrothal's shield**: mirroring into vanilla romance (any state ≥ 3) removes a courted
  lady from the daily NPC-marriage lottery; vanilla can demote the mirror when its own dialog
  opens in a blocked hour, so the mirror is RE-ASSERTED on every courtship exchange (idempotent).
- **The father in the letter window**: once a betrothal stands — ours, or even one agreed the
  vanilla way (CoupleAgreedOnMarriage) — the head of the bride's house is unlocked as a letter
  correspondent, met or never met. His memory begins the day he reads the first letter.
- **Marry Anyone (polygamy)**: detected by loaded assembly name; with it present the player's
  own standing marriage no longer hard-blocks a new courtship — the marriage MODEL (which such
  mods patch) remains the law at every seal, so without the mod vanilla still refuses honestly.
  One standing troth at a time either way; wives archive into ExSpouses exactly as the mod
  ecosystem already expects (FamilyBuilder reads them as wives).
- **Offers ride letters**: the letter-ANSWER flow carries the bargaining hands (byLetter). What
  a reply lays — hiring terms, a betrothal, a blessing — travels WITH the letter (persisted in
  the bag; old letters load as plain) and is presented when it arrives, after the reading; the
  seal re-runs every rule days later. A letter-sealed hire brings her to the banner (teleport
  fallback); the WEDDING day alone refuses paper — face to face only. Offer letters skip the
  parked-notice road so the offer is never stranded behind an unopened window.
- **Same-sex matches**: deliberately not built — the road defers to the world's own law
  (vanilla's opposite-sex rule), no separate editorializing, no test investment.

## Config surface

| Key | Default | MCM |
|---|---|---|
| `EnableConversationMarriage` | on | checkbox |
| `AllowCompanionMarriage` | on | checkbox (wanderer/companion brides) |
| `MarriageNeedsFamilyConsent` | on | checkbox |
| `MarriageDowryHagglePercent` | 30 | slider 0..90 (0 = the reckoning is the price) |
| `CourtshipCharmSlack` | 2 | slider 0..4 |
| `MinBetrothalDays` | 3 | slider 0..30 (0 = may wed the same day) |

All live (no restart). DevMode levers: "[test — courtship & quiet asks]" (the road + her
misgivings + gate verdict), "[test — clear their marriage misgivings]" (she weighs her heart
afresh); the same levers live in the chat window's Dev panel. The odds view and
BondStatsLabel carry a road tag ("on the road: devotion" / "betrothed") and the misgivings
count ("misgivings 2/4").

## Eligibility (the hard floor under everything)

Feature on + tool-capable backend; both alive, adult, free (neither currently wed — checked
via FamilyBuilder truth, never bare `Spouse`; not her captor's prisoner); occupations Lord or
Wanderer only (notables' saves stay untouched); not close kin; vanilla's own pair rules
consulted where they don't fight the companion allowance. The player already wed → the tool
simply never rides (monogamy; polygamy mods keep their own flows).

## The two design walkthroughs

**Sibila** (wanderer companion, real history, authored secret love): gate 1 — station never
blocks; the road IS the feature. Warmth→Ready over a few true talks as the relation climbs;
her asks generated from her story (freedom-shaped, likely); no kin — no blessing; betrothal
popup, three days, wedding popup, her first words as a wife. Everything she is (spark, self,
memory) walks with her into the marriage.

**An emperor's daughter** at player tier 0: Warmth and even Devotion can bloom — but Ready
is walled until tier 4 (6 − 2 slack at best), her asks run hard (renown, deeds, faith per
the world text), and even betrothed, her father the ruler must be faced and paid. A
campaign-long prize, exactly as asked.

## API truths (verified against the game DLLs, ilspycmd 8.2, War Sails-era build)

*Read from decompiled code, not remembered from wikis. Full decompiles cached in the session
scratchpad; the essentials:*

- **`MarriageAction.Apply(a, b)` silently no-ops for unsuitable couples** (Debug.Print only) —
  always pre-check `MarriageModel.IsCoupleSuitableForMarriage` ourselves. Vanilla suitability
  requires BOTH heroes `IsLord` with live clans, opposite sexes, no shared ancestor within 3
  generations, age ≥ 18, no spouse, not in a map event or army, not "engaged" via a pending
  marriage offer. **A wanderer/companion can never pass — vanilla Apply is a no-op for them**,
  so the companion road needs our own path (Spouse assignment is safe: the `Hero.Spouse`
  setter itself mirrors the pair and archives any old spouse into ExSpouses).
- On success Apply does: Spouse both ways → relation +`GetEffectiveRelationIncrease` (20 +
  groom's Charm bonus) → clan move per `GetClanAfterMarriage` (**the player's clan always
  wins**; the moving hero loses governorship, leaves party/army, goes briefly fugitive) →
  fires `CampaignEvents.BeforeHeroesMarried` (the ONLY marriage event; spouses already set
  when it fires) → `Romance.EndAllCourtships` both → romance state `Marriage (7)`.
- **`Romance.RomanceLevelEnum` verbatim**: Ended −2, Rejection −1 (vanilla-unused), Untested 0,
  FailedInCompatibility 1, FailedInPracticalities 2, MatchMadeByFamily 3, CourtshipStarted 4,
  CoupleDecidedThatTheyAreCompatible 5, CoupleAgreedOnMarriage 6 (= betrothed), Marriage 7.
  `ChangeRomanticStateAction.Apply` is the one public mutator — no validation, no side
  effects beyond the state write + `RomanticStateChanged` event.
- **Mirroring our road into vanilla state is safe and useful**: any state ≥ 3 removes the
  hero from the daily NPC-marriage lottery and from AI marriage offers (so a courted lady is
  never married off under the player mid-courtship). We mirror Warmth+ →
  `CourtshipStarted (4)` and Betrothed → `CoupleAgreedOnMarriage (6)`. Vanilla never
  completes a marriage from 6 on its own. Its one hostile move: opening the vanilla dialog
  while the pair is momentarily unsuitable (at war etc.) can demote 3..6 → 1 — harmless to
  OUR persisted stage; we simply re-assert the mirror on our next stage change.
- **The world learns of every wedding for free**: `DefaultLogsCampaignBehavior` listens to
  `BeforeHeroesMarried` and adds a `CharacterMarriedLogEntry` unconditionally (kept ~4.6
  game-years). BUT the entry scores ZERO in vanilla's per-hero relevance — TidingsBuilder
  needs an editorial baseline for it. For the companion road (no BeforeHeroesMarried), we add
  the log entry ourselves — `LogEntry.AddLogEntry(new CharacterMarriedLogEntry(a, b))` is
  public, as is the `MarriageMapNotification` door.
- **The bride-price truth**: vanilla's `MarriageBarterable.GetUnitValueForFaction(her side)`
  is literally **minus her clan's Renown** (softened for a bride ~38+ by a cubic "spinster
  relief") — the family must be compensated ≈ their renown in barter value. Our blessing
  reckoning mirrors that: `max(floor, clan.Renown − spinsterRelief)`, haggled within
  ±`MarriageDowryHagglePercent`.
- **NRE mine (fix shipped with this feature)**: `ChangeRomanticStateLogEntry
  .GetConversationScoreAndComment` dereferences `Hero.OneToOneConversationHero` with no null
  check → NREs when scored OUTSIDE a native conversation for any courtship-level entry
  involving the player. TidingsBuilder calls exactly that API — per-entry guard added, since
  both our mirroring and plain vanilla courtship put such entries in the log.
- `Hero.CanMarry()` includes the `CanHeroMarryEvent` veto (quests can refuse) — respected.
