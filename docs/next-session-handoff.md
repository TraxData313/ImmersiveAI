# Handoff — after the 2026.08.15 batch

Written at the end of the session that built the whole post-marriage batch (items 1–8 of
`docs/after-the-wedding-design.md`, plus the small UI debts). Everything below is deployed and in
the game. **Read `docs/after-the-wedding-design.md`'s spirit section first, whole** — the moral
shape of this feature is the feature, and none of the checks below matter if that is lost.

---

## Part 1 — what Anton found playing it

Both already in `TASKS_TODO.md` under BUGS, repeated here with what is known.

**1. The voice buzzes on a night account.** Pressing ♪ on the ☾ night-account card ("The Jug by the
Fire") plays a "buuuuu" noise where the first part of the account should be. **Reproduced twice**,
both times on the first part. Suspects in order:

- The card's body may carry furniture the words never should — the whole stamp line
  `[the road, 1084.04.19 02.29 (Winter 19, Year 1084)] ☾ Of that night between us: …` plus the ☾
  glyph. `SpeakableText` strips this for the ordinary card shapes; it may not for THIS one.
- Or the account's length trips `VoiceBudget`'s ceiling and the runaway guard cuts mid-stream —
  a night account runs long, and Cyrillic runs longer per character.

Start at `ChatMessageVM.WithVoice` and find what body the ☾ card actually hands it. Then
`docs/voiceover-engine-notes.md` — every number in it was measured against the real engine, and a
real derail (202 Bulgarian characters → 327 seconds) is documented there.

**2. A wanderer in a tavern is drawn in the town, not the tavern.** Vanilla's own Talk puts her in
the tavern set; our tableau picks a hall interior by culture and settlement only. Lives in
`ConversationSceneBuilder`. **Same root as Anton's "change the set" wish** — do them together, and
together with the hearth-as-stage job, since all three are that one file.

**3. Four asks from the same evening** — full text in `TASKS_TODO.md` under NEXT UPDATE:
the night's clock by the sun rather than a 24-hour count; changing the set inside a town; the
voices reading the `*acted*` parts (MCM toggle, on by default); and speaking her answer when the
chat is closed so it can be listened to while doing something else.

---

## Part 2 — what I would like checked

**Nothing in the batch below was playtested before it shipped.** These are ranked by what would
hurt most if wrong, not by how likely they are.

### Rank 1 — it mutates the world, and a mistake strands a hero

**The lover seal, and the buyout.** `OnLoverSealed` → `BringHerAlong`, and `OnRansomSealed` →
`LeaveHerHouse`. The second one mirrors vanilla's own clan-change housekeeping (governorship
removed, command stood down, fugitive, clan set, home settlements refreshed) and it can disband a
lord's party. Decompile-verified against `MarriageAction`, never run.

- Dev panel → **"Make them your lover"** on a *wanderer*: does she join the party, and is the
  companion limit genuinely not consulted?
- Then on a *noble of another clan*: she should NOT join yet. Then the clan head should be able to
  name a price (or you can force it), and after paying she should leave her house and ride.
- Watch for: a hero left with no party and no clan; a disbanded party that should not have been; an
  army that vanished.

**A lover's child.** `BorrowTheFatherSlot` — vanilla snapshots the father out of `mother.Spouse` at
the instant of conception and holds it for 36 days, and a lover has no spouse. **The failure mode
is a crash at the delivery, five weeks after anything you could connect it to.** Use the dev lever
"Hasten a child with them" on a *lover*, then let the pregnancy run to term. Also confirm she is
NOT left married to you afterwards (the slot is borrowed and put straight back).

### Rank 2 — the feature does not happen at all

**Does she reach for the door's hand?** `weigh_what_stands` is a new tool. This project has been
burned exactly here twice (gpt-4o going shy of `move_heart`; `weigh_misgivings` silently doing
nothing for a whole live session). Provoke a real grievance in conversation and see whether
anything is set down. If nothing ever is, the door never closes and the entire consequence engine
is inert.

**Known broken, do not spend time confirming:** `weigh_what_stands`'s **revise** deed. The resolver
calls `DoorReasons.Revise(list, matter, matter, opens)` — the same field as both "which one I mean"
and "the new wording", so it cannot work either way. It needs a second parameter (the misgivings'
own tool has one) or revise should be dropped from the deed enum.

**Does a married player's courtship still work, and can a new one now start?** `TrothBlockReason`
gained a `forHand` parameter and a standing marriage now bars readiness and beyond rather than the
whole road. On an existing married save: an old courtship should behave exactly as before, and a
new acquaintance should be able to warm and then love. If courtship looks wrong for a married
player, this is the change.

### Rank 3 — it happens, but wrongly

**The duty night.** Shut a door (dev panel → "Shut their door against you"), then at dusk take
**"Go to her anyway"**. Check all of: no gift question; no ☾ notice and no name; one flat beat in
her memory; one of Anton's three spare lines in the log; and a *new* reason on her list afterwards.
Then check it is NOT offered when she is far away or the hours are not up, and NEVER during her
season.

**"Between us."** The button should say that and only that, for everyone. It must open the composed
page for a **wed** soul — that routing was wrong until the last hour of the session and wives are
exactly who has doors. The page's action button should appear for a wedding, and for a child
awaiting a name.

**Recognition.** A child born to a lover should ask a three-way question — feast / own quietly /
say nothing — and the free option must be offered even with an empty purse. On an **existing
save**, any child of your marriage must still be treated as yours without being asked anything.

**The era norm.** Every soul now carries one passage about the order of the world. The founding
rule of this whole mod is that they do not sound alike: talk to three different women about their
place and see whether you get three different answers. If they converge, the passage is doing
too much and must be cut back — this is the single most important qualitative check in the batch.

**The night notice portrait.** Anton's purple circle should now be her face.

### Rank 4 — the untriaged pile

`docs/review-findings-2026-08-15.md` — all 35 raw findings from the adversarial pass, with
file:line and a scenario each. **Unverified**: the verify phase died on a session limit, so the
run's "0 confirmed" means nothing. Ten were checked by hand and fixed; five are flagged in
`TASKS_TODO.md`; **twenty have not been triaged at all**. Triaging them is a cheap, well-scoped job
— read the code at each cited line and fix what is real. Expect a good share to be deliberate
design that a reviewer without context mistook for a defect.

---

## Standing rules for whoever picks this up

- `dotnet test` after touching Core. 784 green at handoff.
- Deploy for Anton when done: `powershell -ExecutionPolicy Bypass -File tools\deploy.ps1`
  (the game must be closed, or the DLL is locked).
- Every player-visible change gets a one-line pill in `CHANGELOG.md` under `[Unreleased]`.
- The permanent beat marks are permanent. Never reword one; add a new one beside it.
- Two things in this batch were deliberately NOT built as specified, both to protect the mod's
  founding law, and both are argued in the design record: no permanent band line on her sheet
  (telling a woman her feeling is thinning is scripting the feeling), and nothing writes on her
  door for her when a leak lands (she learns it, she comes, and she writes it herself if she
  wants to). Do not "fix" either without reading why.
