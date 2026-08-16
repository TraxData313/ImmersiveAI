# Birth recognition — the three fixes (work order, 2026.08.16)

*Written for the implementing session by the session that triaged them, with Anton. The three
findings come from docs/workflow-results-2026-08-16.json; all three were verified against the
code at the cited sites and are REAL. Anton agreed the resolution below in conversation on
2026.08.16. Read docs/after-the-wedding-design.md → "Recognition — blood is the game's, honor
is ours" before starting; the fixes must answer to it.*

## The law being applied (agreed with Anton, 2026.08.16)

**Marrying the mother heals silence, never speech.** One shared judgment — *is this child of a
marriage, in the world's eyes?* — true when:

- `BornInMarriage` (captured truth of the day, never changes), **or**
- the child's two parents are wed **today** *and* nothing was ever explicitly said
  (`Owned != Acknowledgement.Withheld`).

The era's own rule (legitimatio per subsequens matrimonium): a later marriage legitimates the
child born before it. But an explicit withholding is a SAID thing — the world heard him say
nothing — and a wedding does not unsay it; only the giving of the name does. That keeps the
rare, heavy late-recognition act meaningful.

Why one helper: today three sites answer this question three different ways — the feast popup
(record OR wed-today), the house line (record only), `WithholdTheName` (record only). The
popup's own self-review comment already found the right principle ("the question is asked of
the LIVE world, not only of the record"); the bug is that it was applied in one place and not
shared.

## The three bugs, for orientation

1. **House line describes trueborn children as acknowledged bastards.**
   `HouseOfThePlayerLine` (ImmersiveChatBehavior.Births.cs, ~line 738) reads
   `record.BornInMarriage` raw. Every birth recorded before the field existed (2026.08.15)
   loads `false`, so an existing campaign's legitimate children flip `anyOutside` and ride
   every wife's/lover's sheet in the vocabulary reserved for children owned outside a marriage.
   Also fires on NEW records when a child is born outside marriage and the player then marries
   the mother (the lover road's happy ending).

2. **"No feast" can quietly disown a child.** The popup computes
   `mustOwnIt = !BornInMarriage && !AreWed(player, mother)` and, when false, shows the plain
   two-way "How will you welcome the child?" whose decline button says "No feast". But the
   decline handler calls `DeclineTheFeast` → `WithholdTheName`, which checks ONLY
   `record.BornInMarriage` — so for a child born outside marriage whose mother is now the
   player's wife, clicking "No feast" (meaning: no party) records an explicit withholding,
   writes the mother a beat saying the name was withheld, and puts the child on the
   awaits-the-name list a lover can press on. The third answer of a three-way question, written
   from a two-way question. A withholding must be a decision, never an accident.

3. **A bought feast is remembered as no feast.** `OwnTheChild(record, bool late, bool
   withFeast = false)` — nothing in the mod ever passes `withFeast: true`. The feast-buy branch
   (~line 606) calls `OwnTheChild(record, late: false)` then `HoldTheFeast`, so the mother's
   `MotherNameBeat` always takes the quiet-owning wording and the with-feast branch of
   `BirthText.MotherNameBeat` is dead in production (only tests reach it, and they only pass
   false). Honor is what is SAID; the feast is saying it loudest, and her memory of the
   grandest hall must not read as a quiet word in a corridor.

## The changes

All in `src\ImmersiveAI.Module\ImmersiveChatBehavior.Births.cs` unless said otherwise. Line
numbers are from 2026.08.16 and may have drifted — anchor by the quoted code, not the number.

### A. The shared helper

Add to the Births partial, near `HouseOfThePlayerLine`:

```csharp
/// <summary>
/// Whether the world counts this child as of a marriage — the ONE answer to a question three
/// sites used to answer three ways (the feast popup, the house line, the withholding guard).
/// True for a child born in wedlock; true when the parents are wed TODAY and nothing was ever
/// explicitly said (the era's own rule: a later marriage legitimates the child born before
/// it — and it also heals every record written before BornInMarriage existed, which all load
/// false). An explicit withholding is a SAID thing and survives the wedding — only the giving
/// of the name unsays it, which is what keeps that act heavy.
/// A record whose parent cannot be resolved (dead, gone) falls back to the captured flag —
/// the accepted old-record artifact, narrowed to widowers (Anton, 2026.08.16).
/// </summary>
internal static bool OfTheMarriageInTheWorldsEyes(BirthRecord record)
```

Semantics:
- `record == null` → false.
- `record.BornInMarriage` → true.
- `record.Owned == Acknowledgement.Withheld` → false (speech survives).
- Else resolve the pair: mother = `MotherIsPlayer ? Hero.MainHero : FindAliveHero(record.MotherId)`,
  father = `FatherIsPlayer ? Hero.MainHero : FindAliveHero(record.FatherId)`; if both non-null
  and `FamilyBuilder.AreWed(mother, father)` → true. NOTE this is the same PAIR the capture at
  birth uses (`AreWed(mother, father)`, ~line 179) — and it quietly fixes the female-player
  case, where the popup's current inline test compares the player against herself.
- Any failure → fall back to `record.BornInMarriage` (fail toward current behavior). Use the
  existing `Safe(...)` wrapper style of the file.
- ALWAYS `FamilyBuilder.AreWed`, never a bare `Spouse` check (the polygamy rail — Marry Anyone
  parks living wives in ExSpouses; see the polygamy memory note).

### B. House line (bug 1)

In `HouseOfThePlayerLine`, compute `bool ofMarriage = OfTheMarriageInTheWorldsEyes(record)`
once per record; use it for both:
- `if (!record.BornInMarriage) anyOutside = true;` → `if (!ofMarriage) anyOutside = true;`
- `InMarriage = record.BornInMarriage` → `InMarriage = ofMarriage`

Effect: a wife's trueborn children stop being described in the bastard vocabulary; a child he
owned and whose mother he then married folds in as simply theirs (the line exists only for
houses with something to explain, and that house has nothing).

### C. The decline honors its own label (bug 2)

Three touches:

1. In the feast offer, replace the inline `mustOwnIt` computation
   (`!record.BornInMarriage && !Safe(() => FamilyBuilder.AreWed(Hero.MainHero, mother), false)`)
   with `!OfTheMarriageInTheWorldsEyes(record)`. Equivalent on this path — the offer gate
   already excludes `FeastOffered` records, so a Withheld record can never reach it — but now
   it is the one shared meaning. Keep the existing comment block about the live world; trim it
   to point at the helper rather than restating.

2. The decline callback `_ => DeclineTheFeast(record)` → `_ => DeclineTheFeast(record, mustOwnIt)`.
   `DeclineTheFeast` gains the parameter (`bool askedTheOwning`) and calls `WithholdTheName`
   ONLY when it is true. `MarkFeastOffered` and `TrySealIfWhole` stay unconditional. Comment
   the intent: the negative button is labeled "Say nothing" only when the owning question was
   truly put; when it says "No feast" it must mean no feast and nothing more.

3. Belt and braces in `WithholdTheName` itself:
   `if (record == null || record.BornInMarriage) return;` →
   `if (record == null || OfTheMarriageInTheWorldsEyes(record)) return;`
   so no future caller can disown a child the world counts as of a marriage. KEEP the next
   guard (`Owned != NeverArose` → return) exactly as is — only silence can become withholding.

### D. The feast is remembered (bug 3)

In the feast-buy branch of the popup callback:
`if (pick is BirthScale scale) { OwnTheChild(record, late: false); HoldTheFeast(record, scale); }`
→ pass `withFeast: true` to that `OwnTheChild` call. The `QuietOwning` branch stays as is
(default false). The late path (`GiveTheNameTo` → `OwnTheChild(record, late: true)`) stays as
is — there is no late-feast flow today.

## Tests

- Core tests (net8) cannot reach the helper (Module, net472, Hero/FamilyBuilder). The pure part
  is trivial; do not force it. Say so in a comment only if natural.
- ADD a Core test in `RecognitionTests` for `BirthText.MotherNameBeat(..., given: true,
  withFeast: true, late: false)` asserting the with-feast wording is produced and differs from
  the quiet-owning wording (the branch is dead in tests today — every existing call passes
  false). Read `src\ImmersiveAI.Core\Births\BirthText.cs` ~line 220–240 for the actual wording
  before asserting; assert on a stable fragment, not the whole string.
- `dotnet test -c Release` — all green before deploy (804 at last count).

## Do NOT

- Do NOT migrate or rewrite old records. Anton's call ("only care for the new ones"): old
  records heal through the live test where the mother is alive and wed; a dead mother leaves
  the accepted artifact.
- Do NOT touch the lapse question (a feast offer lapsing past its 30-day window leaves
  `Owned = NeverArose`, which reads as owned). That is a SEPARATE open decision item in
  TASKS_TODO.md, deliberately undecided.
- Do NOT reword any beat text or marker (`BirthText` markers are permanent — recorded memories
  keep their phrasing forever). The new test asserts existing wording; it does not change it.
- Do NOT rename `BornInMarriage` or change its capture at birth (~line 179) — the captured
  truth of the day is correct and the design comment there explains why.

## After

1. `dotnet test -c Release` green.
2. Rebuild + deploy: `powershell -ExecutionPolicy Bypass -File tools\deploy.ps1` (game closed).
3. CHANGELOG.md `[Unreleased]`, one player-facing pill, e.g.: "A wife's own children are no
   longer spoken of as children owned outside the marriage, declining a feast can no longer
   quietly disown a child, and a feasted child's mother remembers the feast."
4. TASKS_TODO.md: in the "TRIAGE RESULTS ARE ON DISK AND UNAPPLIED" entry, mark the three
   births findings as applied (pointing here); the REST of that entry stands — the other
   triage verdicts remain unapplied.
5. Leave a breadcrumb in the memory folder per CLAUDE.md's wrap-up rule.
