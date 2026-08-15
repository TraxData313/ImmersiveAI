# Life after the wedding — the design record

*The developer decision record for the post-marriage batch (designed 2026.08.15, Anton + Claude,
in conversation — concept locked, build not started). Read the WHOLE spirit section before
touching any detail: the implementer's job is the technical shape, not the moral one. That one
is settled here and is the point of the feature.*

Player-facing docs live elsewhere. Sibling records this leans on constantly:
`docs/nights-and-conception-design.md` (the nights, the leaks, the pregnancy traps),
`docs/marriage-courtship-design.md` (the road, the misgivings, the bargain mold).

## The spirit — read this first

Anton's own framing, and the reason this batch exists at all: from the Bible he holds that God
hates adultery — that though the man-woman bond is an image of the God-man bond, **a man cannot,
like God, love many women equally without wounding them**. He wants that truth to LIVE in the
mod — never preached, never written in any message, simply *true in the mechanics*, discovered
by playing. His words: "това е нещото което искам да оживее в този мод."

So the design gives a marriage three roads, and none of them is a stats screen:

- **Fidelity** — costly, slow, alive. The nights with their written stories, the children, the
  things said and unsaid. Already built.
- **The fall** — it never starts with a "seduce" button; it starts with an intrigued soul who
  approaches YOU, warm, on the market square. Then the lover road, the leak, the morning
  conversation, the closed door, the duty nights, the spiral. The player is never punished with
  numbers — he is punished with VOICES: a letter that hurts, a door with her written reasons, a
  spare sentence at the wall.
- **Repentance** — and this is the discovery of the design session: **it is already built.** The
  misgivings machinery IS the mechanics of repentance. She writes what stands between you; the
  player learns it, comes to her, talks; and SHE, by her own hand, through her own tool, judges
  whether the words reached her. There is no "apologize" button — there is a conversation that
  either finds her heart or does not. We never programmed forgiveness; we gave her a hand to
  forgive with.

**The two aches.** The system breathes because the wife and the lover hurt over DIFFERENT
things, and both aches are engines:

- The **wife** aches over betrayal. Her misgivings are about broken trust; her far pole is the
  closed door and the cold silence.
- The **lover** aches over shame. The order of the world ("to be hidden is shame") gives her a
  living, permanent craving: to be acknowledged, raised, her children given the name. Lovers
  PUSH — they want the promotion, they write, they remind. The adopt/promote events are not
  menus; they are answers to her pressure.

And from this follows the thesis become design: **there is no happy equilibrium with many
women.** Someone always aches — the wife from the other's existence, the lover from the shadow
she stands in. The mod states this in no sentence anywhere; it is simply so in the mechanics.

**Punishment is rare and heavy, never frequent and light.** The failure mode to fear is the
nagging simulator — every leak breeding a confrontation until the player goes numb. The cure is
already invented (outreach damping): she raises a thing ONCE, and after that comes the cold
silence, which is worse. The TogetherLine already shows the unaddressed; the quiet around it is
the punishment.

**Temptation must have flesh.** For the fall to work the lover must give something real, and
the answer is the most human one: PROXIMITY. The wife is in the town with the house and the
children; the lover is the one who is THERE — on the road, in the camp, by the fire. We invent
no reward; geography is the temptation.

## Decisions locked (2026.08.15, Anton's calls)

- **Scope of romance**: the player and UNATTACHED women only — wanderers and nobles, the ones
  who can be taken into the company. No married NPCs, no adultery toward other men's wives
  (deferred, maybe never). No same-sex changes beyond what the nights already settled.
- **Age gates are NOT touched.** The 14+ idea from the first brainstorm is DROPPED entirely
  (Anton: "да не пипаме за момента там… махаме го"). Everything stays at the game's own 18.
  Do not revisit without a new explicit decision.
- **The wife's far end is cold and distant, never gone**: no leaving, no withdrawing to her kin,
  no telling her father — all deferred ("после ще мислим за напускане и други").
- **Bastard children draw no world commentary yet**: no tidings, no rumors about whose the child
  is. The facts live in the family info lines only.
- **Marry Anyone: recognized, then retired.** Existing MA saves are honored (see its section);
  going forward OUR multiromance replaces it — the mod stops carrying new MA rails.
- **Lovers are secondary wives in the world's eyes; the first wife is the official, ceremonial
  one** — the one shown as "wife".
- **The duty night exists** ("Go to her anyway") — reframed from the first brainstorm's "claim
  your right": nobody forces anybody; she does not refuse, she PERFORMS. The weight is exactly
  that. No narrator ever writes such a night (code-enforced). The spiral is wanted: each duty
  night deepens the closure ("ако остави връзката там ще отиде много надалеч, много ми
  харесва").
- **The era norm is a law of the WORLD, never of the soul** — stated as what the world holds,
  with each woman's stance toward it her own (traits, spark). Anton: "тогава това е било
  нормалното… искам да влезнем в това време" — and it must not come out robotic.
- **The morphing chat-window button becomes one permanent door: "Between us"** — the page behind
  it changes, the button never does.
- **The hearth window (H) is rebuilt in the talk screen's shape** — list left, the chosen woman
  alive in the center via the conversation tableau, settings right; pose shifts on interaction
  are PURE LIVELINESS, never emotion mapping (the gestures decision holds: the words carry the
  feelings, the body saying it too reads as pantomime).

## The systems

### Temptation comes to you

The reach-out machinery is the delivery vehicle for temptation — an intrigued unattached woman
approaches the married player herself. No new flow: her pull gains an attraction component the
same way closeness already weighs |relation|. Scope-gated to the decided set (unattached,
company-eligible women). Tuning is the builder's: the effect should be FELT but rare — the
world offering, not pestering. Reuse `InitiationScorer`'s shape; whatever term is added must
ride BEFORE the damping like the presence floor does, and must never break the group-total law
(the day's expected visits stay ≈ rate × unionPull — see the settled initiation-rate memory).

### The lover road

The courtship mold applied to the second bond. `CourtshipRoad` grows a FORK, not a sibling:
the same tended stages by her own `tend_courtship` hand, but past the heart's waypoints the
destination may be the lover's bond instead of betrothal. Persisted in `NpcMemory` beside
`CourtshipStage` (snapshots rewind it for free, like everything else).

- **She must want it** — same principle as the wedding: the tool lays, ONLY the player's sealed
  popup binds, rules re-run at both (the `TrothBlockReason` discipline).
- **A noble lover leaves her clan**, and that is BOUGHT: the price is anchored to the worth of
  her own equipment ("the eq she has sell price, not cheap") — a readable, honest figure, not an
  invented one — with the standard haggle rails (±percent, MCM). Her father/clan head is NOT
  glad: a relation drop, and a LETTER to the player carrying his mind on it (the blessing
  bargain's machinery, negative sign — same mold, reversed).
- **A lover rides outside the companion limit** (Anton's original note: "ignores comp limit").
  She comes for love, not hire.
- **Reverting is real**: a lover may become an EX-lover (her own step-apart on the road, or the
  player's). She stays in whatever clan she is in — the buyout is not undone. Ex-lover is a
  remembered state, not an erasure.
- **Unlock/lose thresholds live in the named bands** (see that section): a lover's bond asks for
  MORE love than a wife's — she has no vows holding her, only feeling. That asymmetry is
  deliberate and era-true.

### Doors with reasons — the consequence engine

**This section deliberately SUPERSEDES one settled rule.** The nights record says: refusals were
cut, the only refusal is the custom days, "do not reintroduce her saying no without asking."
Anton has now asked — this design session IS the asking. From this batch on, **a wife's or
lover's door can be CLOSED, and the closure always carries HER WRITTEN REASONS.**

The mechanics are the misgivings model applied to the night — do not invent a second shape:

- When the bond is wounded (a leak lands, a duty night happens, something in the talk cuts), she
  may close the door — and when she closes it she WRITES why, and what would open it again, via
  her own tool hand (the `weigh_misgivings` pattern: her list, her words, capped standing-open,
  settle/release by her own judgment).
- The dusk popup shows the closed door plainly (greyed, like the custom days but with her
  reason's first words); the "Between us" page shows the full list.
- Reopening is REPENTANCE, and it is free machinery: the player talks, she settles her own
  written reason when the words truly reach her — or does not. No apologize button, no gold
  price, no timer-only decay. Time may soften; only she resolves.
- The doors are also where "prevent kids" lands when it wounds her (see Small graces), and where
  the duty-night spiral deepens.

### Leaks and the morning after

The night-leak machinery (`AwarenessMultiplier`, `LeakTheNameOfTheNight`, the reckoning marks)
extends to the affair itself:

- **Exchanges with a lover / a courted woman can leak to the wife** (and between lovers),
  scaled by the same honesty that prices night leaks: proximity and spentness. Same-settlement
  is reckless; a distant camp is quiet. A leak is a silent beat in the wounded woman's memory —
  she learns it as SHE would learn it, a rumor, not a report.
- **A fresh leak spikes the wounded one's pull for ~24–48 game hours** so the confrontation
  comes while it is hot — she comes to you in the morning, or writes if apart. One spike per
  leak; after that the outreach damping does its usual work and the cold silence takes over.
- **Event beats already fan out** (a wedding beats every witness, a night leaks its name); the
  new events ride the same rails: a night with one woman may reach another, a recognition
  reaches the mother, a promotion reaches everyone it wounds or honors. Beats + the pull spike
  are the whole implementation — no new confrontation subsystem.

### Duty nights — "Go to her anyway"

When her door is closed, the dusk popup still lists her — greyed, with one quiet option:
**"Go to her anyway."** Three words, no violence in them, and every player feels what they are
choosing. The third option (respect) is not a button; it is not pressing this one.

- **No chronicler, enforced in CODE, not prompt** (the rules-live-in-code lesson): a duty
  night's `NightRecord` carries a kind that the story call never fires for. No gift step, no
  night name, no ☾ card. There is nothing to tell — that IS the telling.
- **The player-facing line** is one spare sentence from a tiny deck (3–4), dealt by FNV hash
  from the night id (the ImageDeck discipline — never one fixed string, never LLM):
  - *"The duty of the marriage was kept. She said nothing."*
  - *"She did not refuse you. She was turned to the wall before you rose."*
  - *"What a wife owes, she gave. What she once gave unasked, she did not."*
  (Anton approved these three verbatim — keep this register: the difference between what is
  OWED and what is GIVEN FREELY. Do not brighten them.)
- **Her beat, the load-bearing piece**, short, fixed, first person:
  *"He came to me this night. I did what a wife does. We did not speak of it."*
  From there the LLM does the rest with her own voice — no staging.
- **The spiral**: each duty night deepens the closure — a new written reason of hers, or the
  standing one grows harder (implementation's choice, but it must be visible in her words on
  the Between-us page). The duty path makes the mending path LONGER. Without this the duty is
  farmable and the whole lesson evaporates.
- **Conception stays real** (the duty is real): the vanilla-honest odds ride as on any night,
  but with no gift multiplier and nothing adorned. A player who scorns the bond entirely can
  still make vanilla children forever — while the soul of the house walks further and further
  away. That contrast is the design.

### The order of the world — the era norm

The formula: **the norm is knowledge of the world; the stance toward it is the dowry of the
person.**

- ONE short passage joins the world layer (beside "Of this world, this I know" — same channel
  as the global prompt fold, so every soul carries it), in the era's own voice, sociology-free:

  *"Such is the order of the world: a woman weds, bears children, and her house is her honor.
  To be first and only is honor; to be second, or hidden, is shame. So it is held everywhere,
  by all."*

  Note the grammar: "so it is held" — never "so I feel". The norm is the air everyone breathes.
- **How SHE stands toward that air is personal**: traits, spark, lived story. Concrete lever:
  2–3 new muse cards in the director's-spark deck for exactly this — the woman who wears the
  order with pride; the one who quietly drowns in it; the one who has made it her armor. One
  norm, sixty stances — the opposite of robotic.
- The norm passage is also what makes "they crave kids" true WITHOUT a personality mandate:
  children are the expectation of the age; her own wanting is hers.

### Recognition — blood is the game's, honor is ours

The vanilla layer keeps BIOLOGY: a lover's child is the player's and hers, inheritance and kin
lines untouched, forever ("винаги във ванила ще си се води мое дете и на Х2"). Our layer keeps
HONOR: what has been SAID before the world. Recognition is a public act — "this child carries
my name" — the king's bastard whom everyone knows, versus the king saying it aloud.

- **At birth the choice is three-way**, riding the existing birth-chronicle feast offer:
  recognize with a feast / recognize quietly / do not recognize. One flag on `BirthRecord`.
  An unrecognized birth is a QUIET house: no herald, no witness beats, no announcement.
- **What the world says** (family info in every sheet, the beholder's lines, `recall_person`):
  "two children by his wife X; one recognized child by X2" — and the unrecognized child appears
  only in HER lines ("X2 is raising a son"), the world politely not saying whose. Honest caveat,
  accepted: the game's own encyclopedia shows the truth — but souls speak from OUR sheets, and
  the fiction lives in the world of words, which is where the mod lives.
- **The child stays in the player's clan** (vanilla untouched); the player decides as with any
  child where it is left or taken. In the WORLD'S WORDS an unrecognized child grows in its
  mother's shadow — which is what makes late recognition heavy: not just a name, a taking-in.
- **Late recognition — "the giving of the name"** — one rare, heavy EVENT, possible at any age,
  optionally feasted (a birth-feast arrived late). It moves the child from her lines to yours
  before every soul's eyes. The "adoption" idea from the first brainstorm IS this act — no
  separate mechanic.
- **The unrecognized child is the CONCRETE OBJECT of the lover's craving** — her pressure stops
  being abstract: "our child grows without your name." Recognize the child and the ladder does
  not end: SHE is still unrecognized. Two rungs of craving — first the child, then herself.
  That is what makes a lover a living character for years, not an episode.

### The child who knows their own story

Nearly free — a child is a soul, and souls already work this way:

- Birth, recognition, the giving of the name — all are ledgered events that can lay SILENT
  BEATS into the child's own memory FROM THE DAY OF ITS BIRTH (a hero's memory file exists
  whether or not they can yet speak). Growing up, its own history quietly accumulates: "I was
  born", "my father said my name before all" — or the silence of never hearing it.
- When it comes of age and first speaks, it ALREADY KNOWS WHO IT IS — nothing is briefed. The
  story-seed machinery covers children born before the feature.
- Compression is the child CHOOSING what to remember — Anton asked for exactly this without
  naming it: the rolling memory is rewritten whole by the soul itself; an unrecognized child
  may grow up having CHOSEN to remember the silence, or having forgiven and let it fade. Its
  choice, not our script — the same hand the wife forgives with.
- **Privacy holds**: the mother's first-person hour is NEVER handed to the child — it gets the
  facts of its day, not her voice (the CradleTool rule extended one person further).

### The named bands of the heart

Anton's ladder: **neutral → intrigued → like → love → deepLove.** The rework decision:

- **NO third number.** The bands are DERIVED — a pure function of what already exists (relation
  + road stage), never stored, never a new source of truth. Thresholds are the builder's to
  propose, tuned against the courtship gates that already exist (Warmth ≥ 0, Devotion ≥ 20,
  Ready ≥ 40).
- **She knows the ladder by NAME, never by number** (the numberless-refusals law): her sheet's
  standing line speaks the band and what it opens — "my heart stands at fondness, and a lover's
  bed asks more than fondness" — no tiers, no figures, ever.
- **The asymmetry is the design**: with a WIFE intimacy unlocks at *love* and is lost below
  *intrigued* (the vows hold a while even as the heart cools); with a LOVER it unlocks only at
  *deepLove* and is lost already below *like* (nothing holds her but feeling). Exact mapping of
  Anton's original note; keep the shape even if thresholds move in tuning.

### Small graces

- **"Do I have something special in mind?"** — the special-night flow gains one optional free-
  text line from the player ("I want us to spend the night under the stars"), passed into
  `BuildStoryPrompt` as HIS wish for the night, which the chronicler weighs in. One field, one
  prompt line. The perfect first stone — felt immediately.
- **The first night is marked**: the first written night between the player and a given woman is
  known to the ledger, told to the chronicler (a first night is not a fifteenth), and shown in
  Between us for a lover.
- **Prevent-kids honesty**: default = ASK before the night (a plain choice in the flow, MCM for
  the default). The chronicler is told the choice. SHE is told the choice — it happens in the
  room; the she-sees-what-she-would-see law — as part of the night's beat, plainly, no editorial.
  What she FEELS about it is hers (era norm + her stance); if it wounds her, the doors machinery
  is where it lands. Never script the feeling.
- **The family, visible**: the family info lines (kin/FamilyBuilder + situation) carry the
  public state — wife, lovers as the world knows them, children with recognition — and a
  `recall_house` tool answers the fuller event log on demand. STRICT rule: the tool reads the
  PUBLIC layer only; the privacy fences (night → spouse, hour → parents) hold under it.
- **Heir choice** — DEFERRED to a later phase; listed so it is not lost. It will live as a
  Between-us section when it comes.

## UI

### "Between us" — one door, many rooms

The chat window's morphing button (Misgivings n/m → Our wedding day → Our children) becomes ONE
permanent door labeled **"Between us"**, and the PAGE behind it adapts. (The morphing button
already pinched us — the label left saying "Our wedding day" over a children page because the
widget could not fit the truth.) Sections appear only when they have something to say:

- **Where we stand** — the band in words, the courtship road if walking, her misgivings with
  her notes (the current Misgivings view, given a home).
- **Our days** — the wedding, the children one by one with the recognized/unrecognized mark,
  the first night if a lover, the givings of names. The chronicles already drawn as ❦/❧ cards,
  ordered into one story.
- **What is unsaid** — the TogetherLine matter: the unaddressed, the coldness, the closed door
  with her written reasons. The player SEES the spiral here — not as a meter, as her words.

The page recomposes by soul: wife → everything; lover → her status, the craving, the children;
child → its own day, its name, its parents; plain companion → Where we stand only. The privacy
layer sits under all of it — the page shows what THIS bond may see.

### The hearth window becomes a stage

The H window is rebuilt in the talk screen's shape: wives/lovers listed LEFT, the chosen one
ALIVE in the center via the game's own conversation tableau, general + per-woman settings
RIGHT. The expensive part is already paid — the tableau hosting with its four traps (the
mission stub, nulling `CampaignMission.Current`, the ONE shared cached scene, the full-screen
widget rule) is solved in `ConversationSceneBuilder`; the hearth screen is the talk screen's
brother, not a new organism.

- **Pose shifts on interaction are pure liveliness** — `ShiftStance` wired to setting clicks
  the way it already steps per message. NEVER map settings to emotional poses (closing a door
  → guarded stance): that is the pantomime the gestures decision already rejected. Discussed
  and settled with Anton this session.
- **The shared scene is built by ONE screen at a time** — the talk screen and the hearth screen
  must yield to each other exactly as the two old windows' managers already do — and NEVER
  while a real conversation is active.

### Small UI debts

- **The purple empty notification circle** gets an icon/portrait — same family as the portrait
  problem already solved on the chat notification (`ImmersiveChatNotificationItemVM`'s dark-
  backdrop portrait); likely a known fix worn again.
- **The chat/talk contact list defaults**: the wife pinned on top, lovers right under her, then
  everyone else by the existing ordering.
- **The settlement menu teaches its hotkeys** (Anton's ask, same session): the existing
  town/castle/village option that opens the talk window carries its key in the label —
  "Send message… (O)" style, reading the live key from config like the info overlays already
  do — and directly under it a SECOND option in the same style opens the hearth window with
  its key ("…(H)"). Same menu, same voice, two doors.

## Marry Anyone — recognized, then retired

- Existing MA saves are HONORED: standing marriages are recognized as they are, the first by
  wedding date becomes the official wife, the others stand as secondary wives, nothing is
  dissolved, no one is demoted.
- Going forward the mod's own multiromance is the road, and new MA rails stop being added. The
  vanilla-polygamy question RESOLVES ITSELF in this design: **only the first wife is the
  vanilla spouse; lovers are OUR layer entirely** (no `MarriageAction`), so no polygamy patching
  is needed at all. "Promote lover to wife" while a wife lives is a CEREMONIAL act in our layer
  (the world's words change), not a vanilla marriage — unless the wife has died, in which case
  the real `MarriageAction` road stands open as ever.
- The existing MA detection/compat rails in courtship stay for the recognized saves; they
  simply stop growing.

## Dev levers

In the chat window's Dev panel, acting on the selected soul: **"Make her a lover"**, **"Make
her the wife"**, **"Show her doors & reasons"**, plus the existing wed/night/misgivings levers.
Anton has MA saves ready to test recognition on — the levers exist so he can stage households
without playing forty hours.

## Build order

1. **Special night intent** — tiny, felt immediately, exercises the chronicler plumbing.
2. **The lover road** — the fork, the buyout, the father's letter, outside-the-limit joining,
   ex-lover. The heart of the batch.
3. **Doors with reasons** — the consequence engine. Without it lovers are free.
4. **Leaks → the morning after** — the drama loop; machinery exists, this is wiring + tuning.
5. **Duty nights + the spiral** — needs doors first.
6. **Recognition + the child's story + recall_house / family lines.**
7. **Bands + the norm passage + muse cards** — can slot in beside any of the above.
8. **Between us page**, absorbing the misgivings view.
9. **Hearth-as-stage + small UI debts** — a parallel track, independent of 1–8.

Heir choice, world commentary on bastards, the wife's kin learning, married-NPC temptation:
all DEFERRED, deliberately, with Anton's explicit word.

## Guardrails — what must NOT happen

- **No narrator for duty nights** — enforced where the story call fires, not in prompt words.
- **Privacy is code, not prompt** — every new reader (recall_house, Between us, the child's
  beats) is fenced at the source like NuptialTool/CradleTool.
- **No third relationship number** — bands are derived, or they will drift from the truth.
- **The era norm never becomes a personality mandate** — it is world-knowledge; her stance is
  her own. If every woman sounds the same about it, we have failed the mod's founding rule.
- **No per-NPC stacking on temptation reach-outs** — the group-total law of initiations holds.
- **Refusals and bands never speak numbers** — the Sibuga floor lesson.
- **She sees what she would see; nothing scripts the feeling** — the nights' founding law,
  extended to prevent-kids, leaks, and recognition alike.
- **The duty-night deck and her beat line are fixed text** — never generated, never brightened.
- **The spiral must bite**: a duty night that costs nothing breaks the whole moral of the
  design. If playtests show it farmable, deepen the closure, do not soften the door.

## Open questions for the builder (technical, not moral)

- **Lover conception**: a lover has no vanilla spouse, so `RefreshSpouseVisit` never touches
  her — but OUR night flow must run the same `EnsureFatherSlot` discipline before
  `MakePregnantAction` (the father-at-that-instant trap), and verify the action is safe with an
  unmarried mother. All three nights-doc pregnancy traps apply; re-read them first.
- **Where the lover state persists**: `NpcMemory` beside `CourtshipStage` (snapshot-rewind for
  free) — exact fields (LoverSince, ExLover, FirstNightDay, RecognizedChildren…) are the
  builder's, but they ride `memories.json`, not a new file, unless size argues otherwise.
- **Leak odds tuning**: what a flirt-leak's base chance is, how proximity scales it, and the
  exact pull-spike shape (magnitude, 24–48h decay). Propose numbers, playtest, expect Anton to
  retune.
- **Band thresholds**: propose against the courtship gates; expect retuning.
- **Buyout arithmetic**: "her equipment's worth" needs a concrete reading (civilian + battle
  kit sell value? floor for the poorly-dressed noble?), plus the haggle percent's home in MCM.
- **The dusk popup's closed-door presentation**: her reason's first words in the grey line —
  how much fits before the widget clips.
- **Config keys**: a master toggle for the batch (suggestion: `EnableLoversRoad` or fold under
  a broader `EnableLifeAfterTheWedding`), defaults consistent with the nights (on), every new
  dial in MCM under the existing groups. The duty night should be individually toggleable
  (some players will not want the option to exist at all).
- **MA-save recognition**: detecting standing multiple marriages at load, dating them (the
  wedding ledger may know ours; MA's own are undated — first-by-StringId order as fallback?),
  and never re-running recognition twice.
