# Adversarial review of the after-the-wedding batch — 2026.08.15

All 35 raw findings from the review pass over the post-marriage batch, recovered from the run's
journal. **They are UNVERIFIED.** The workflow's verify phase — 24 adversarial refuters whose whole
job is to kill false positives — died on a session limit, so the run reported "0 confirmed", and
that number means nothing at all. Treat every entry below as a LEAD.

What was done with them on the day: the titles were read, the worst were checked by hand against
the code, and **ten were real and were fixed** (see CHANGELOG and the fix comments, all stamped
"self-review, 2026.08.15"):

- the house line spoke in the PLAYER's first person inside HER sheet
- "Between us" never opened for a wed soul, so the doors were invisible to the only people with one
- "Go to her anyway" checked no presence and no cooldown
- a lover's door could never go cold (the declared-stage cushion never lifted)
- the free "own the child" choice was gated behind the cheapest feast's price
- pre-update birth records asked a married player whether his wife's child was his
- every child's first memory asserted a recognition not yet chosen
- the bond leak wrote another woman's memory raw, clobbering an in-flight exchange
- a duty night was invisible in the "since we were last alone" list
- BorrowTheFatherSlot swallowed a throw that the wife's path lets retry

Five more were judged plausible-but-not-yet-checked and live in TASKS_TODO.md. The rest of this
file has not been triaged. A finding here is not a bug until someone reads the code.
## HIGH

### "Between us" page is unreachable for exactly the souls it was built for (wed / with children)
[v2:15ceb433356901149c5acbe4dd772c66f9ab472ca885bdeff51721977ce2fdb5] `src/ImmersiveAI.Module/UI/TalkScreen/TalkScreenVM.cs:1024`

**Breaks:** RoadPageFor composes the door reasons, the lover standing line, the "children awaiting your name" section and ActionLabel into one Body, but keeps the stage Kind (`Kind = page?.Kind ?? RoadPageKind.BetweenUs`, ImmersiveChatBehavior.Courtship.cs:1730). TryGetWeddingView returns true whenever there is a written wedding record OR any children (Weddings.cs:742), so for a wife or a mother the Kind is WeddingDay. Both windows' button handlers short-circuit on WeddingDay into ShowWeddingViewFor, which re-derives title/body from TryGetWeddingView and never sets IsMisgivingsShown Ã¢â‚¬â€ so the composed Body is never rendered and the RoadActionText button (inside that panel) is never shown. Breaks the design's "What is unsaid" guardrail: the door's written reasons and "what would answer it" are the ONLY feedback the repentance loop gives.

**Scenario:** Player weds Sibylla through the courtship road (wedding chronicled). A duty night lays a DoorReason on her list. The talk screen shows "Between us" under her name and the bond line says "one thing stands between you". Clicking "Between us" replays the wedding cutscene and shows the wedding-day scroll; her written reason and what would open the door are nowhere in the UI. Same for a lover with an unrecognized child: Kind is WeddingDay via the children branch, so the "Give <child> your name" button Ã¢â‚¬â€ the only non-DevMode route to a late recognition Ã¢â‚¬â€ can never be pressed. Identical code path at UI/ChatWindow/ChatWindowVM.cs:778.

### BornInMarriage defaults false on pre-update birth records, so existing legitimate children are described as acknowledged bastards
[v2:15ceb433356901149c5acbe4dd772c66f9ab472ca885bdeff51721977ce2fdb5] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Births.cs:735`

**Breaks:** BirthRecord.BornInMarriage is new and has no migration; every _births/*.json written before this batch deserializes it as false. HouseOfThePlayerLine uses it twice: `if (!record.BornInMarriage) anyOutside = true` (line 735) defeats the "most houses pay nothing for this" guard, and `InMarriage = record.BornInMarriage` (line 743) files every existing child under BirthText.HouseLine's "owned" group. IsOwnedBeforeTheWorld correctly protects them from reading as unowned, but nothing protects them from reading as born outside the marriage. The line then rides persona.PlayerHouseLine on every reply of every wife and lover (ImmersiveChatBehavior.cs:3937).

**Scenario:** Existing campaign: player married to Sibylla with two children, Ira and Tamir, both chronicled before this update. On the first reply after updating, Sibylla's own system sheet contains: "Ira and Tamir I have owned before the world as mine, by Sibylla." Ã¢â‚¬â€ telling her that her own children of the marriage are children he acknowledged outside one. A safe read would require treating Acknowledgement.NeverArose (or a missing BornInMarriage on a record whose parents are wed) as in-marriage.

### "Go to her anyway" checks no presence, captivity or cooldown Ã¢â‚¬â€ only the door
[v2:15ceb433356901149c5acbe4dd772c66f9ab472ca885bdeff51721977ce2fdb5] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Doors.cs:115`

**Breaks:** CanGoToHerAnyway tests only DoorsOn, AllowDutyNights, AreWed and DoorStandingFor. GoToHerAnyway (Nights.cs:776) re-checks exactly that plus DoorBlockFor, and never IsCoLocated / IsAlive / IsPrisoner / IsBehindClosedDoors / CooldownHoursLeft Ã¢â‚¬â€ all of which NightBlockFor enforces for an ordinary night. The comment at Nights.cs:784 claims "everything about her presence still binds", but nothing enforces it. HandleTheEvening puts her in `shutAgainstYou` on `block.Length > 0 && CanGoToHerAnyway(wife)` (Nights.cs:287) and AskWhereYouWillSleep adds the duty element on the same test (Nights.cs:466) with enabled=true.

**Scenario:** Player's wife Sibylla is in Ortysia; the player rides in Battania. Her relation is -5, so DoorStandingFor returns Coldness (HeartBands.OpensTo(Wed) needs relation >= 0 Ã¢â‚¬â€ very common on an existing save). NightBlockFor returns "she is not with you", which is not the custom-of-women string, so she lands in shutAgainstYou and the dusk question fires with "Sibylla Ã¢â‚¬â€ go to her anyway" ENABLED. Clicking it writes a NightKind.Duty record, rolls RollForAChild (she can conceive), lays a DoorReason and fires the duty-night line Ã¢â‚¬â€ for a wife hundreds of leagues away. Same holds for a wife held prisoner in an enemy dungeon, or an hour after a night already spent with another woman (cooldown never consulted).

### "Go to her anyway" ignores presence, captivity and cooldown Ã¢â‚¬â€ a duty night with a wife three weeks away
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Nights.cs:466`

**Breaks:** The duty option is offered whenever `block.Length > 0 && CanGoToHerAnyway(wife)`, but `block` may be *any* refusal Ã¢â‚¬â€ "she is not with you", "she is held captive", "you cannot reach her here", or the cooldown Ã¢â‚¬â€ while `CanGoToHerAnyway` (Doors.cs:119-125) only inspects `DoorStandingFor`, which knows nothing about presence. `GoToHerAnyway` (Nights.cs:776-790) then re-checks only `CanGoToHerAnyway` and `DoorBlockFor`, so nothing about her whereabouts is ever enforced. The method's own comment at Nights.cs:784 asserts "Everything about her presence still binds; only the door is being gone through" Ã¢â‚¬â€ the code does not do that. It also silently defeats `NightCooldownHours`, since `NightBlockFor` returns the door block before it ever reaches the cooldown branch.

**Scenario:** Sibylla is the player's wife, governor of Ortysia, three weeks' ride away; her relation has drifted to -5 so `DoorStandingFor` returns Coldness. At dusk the popup lists "Sibylla Ã¢â‚¬â€ go to her anyway", enabled (the ordinary entry beside it is correctly greyed). Clicking it records a Duty NightRecord, runs `RollForAChild` (a conception with a woman on the other side of Calradia), lays a door reason on her, and writes an `Elsewhere` night into every other woman of the hearth saying he spent that night with Sibylla. The same click works for a wife who is a prisoner, one behind a keep's closed doors, or one four hours after he already spent a night with someone else.

### A lover's door can never go cold Ã¢â‚¬â€ the declared-stage cushion floors her band at exactly the lover threshold
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Doors.cs:93`

**Breaks:** `HeartBands.Of(relation, stage)` only skips the cushion at `stage >= Betrothed`, but a lover's stage is always Devotion or Ready (`LoverRoad.JudgeTake` requires `>= ForksFrom` and `< Betrothed`), so her band is floored at Like (Devotion) or Love (Ready) forever. `OpensTo(band, BondKind.Lover)` asks for `>= Like`, so `DoorReasons.StandingOf` can never return `Coldness` for a lover no matter how far the relation falls. This is the exact asymmetry the design record calls "the design" ("with a LOVER it unlocks only at deepLove and is lost already below like"), and `HeartBands`' own remark claims it: "PAST THE SEAL THERE IS NO CUSHION Ã¢â‚¬â€ betrothal, marriage, the lover's bond." The lover's bond is never passed in; the API has no bond parameter.

**Scenario:** Ira is the player's lover (CourtshipStage.Devotion, LoverBond.Lover). The player wounds her repeatedly and her relation falls to -100. `HeartBands.Of(-100, Devotion)` still returns Like, `OpensTo(Like, Lover)` is true, and `DoorStandingFor` reports Open Ã¢â‚¬â€ her door stays open to him at -100 with nothing written. The same call at Lovers.cs:814 makes `LoverStandingLine` tell the player her heart "stands at fondness" while she hates him. Only `LoverRoad.HeartHasLeft` (which uses the uncushioned `OfRelation`) sees the truth, and it is deliberately never called.

### The free "Own the child" choice is hidden behind a gold check meant only for feasts
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Births.cs:529`

**Breaks:** `TryOfferTheFeast` refuses to show the popup when the purse cannot carry the cheapest feast tier (100 denars). Since 2026.08.15 that same popup is the only door to the zero-cost "Own the child Ã¢â‚¬â€ no feast" option (Births.cs:295-299) and to the recorded `Withheld` decision. Gating a free, irreversible act of honor on a feast's price makes the design's three-way choice unreachable for a poor player, and there is no other entry point: `OwnTheChild` is reached only from this popup and from `GiveTheNameTo`, which requires `AwaitsTheName` (i.e. an explicit `Withheld` that this popup is the only writer of).

**Scenario:** The player fathers a child on his lover Ira while holding 40 denars. `OfferPendingFeasts` calls `TryOfferTheFeast` every hour for 30 days; every call returns false at the gold check, so he is never asked. `record.Owned` stays `NeverArose`, `AwaitsTheName` is false forever, the "Give <child> your name" action never appears on the Between-us page, and `UnnamedChildrenOf(Ira)` returns 0 Ã¢â‚¬â€ so the lover's craving, the concrete object the design builds her whole character arc on, never engages for that child.

### A feast/owning offer that lapses is silently read as "he owned the child"
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Core/Births/BirthRecord.cs:157`

**Breaks:** `IsOwnedBeforeTheWorld => Owned != Acknowledgement.Withheld` treats `NeverArose` as owned. That default is correct for records written before the feature, but nothing converts a *new* out-of-marriage record to `Withheld` when the offer window expires: `WithholdTheName` is called only from `DeclineTheFeast` (Births.cs:824), and `AwaitingFeastOffer` simply stops returning the record after `FeastOfferDays` (30). `BornInMarriage` is now captured, so the two cases are distinguishable and are not being distinguished.

**Scenario:** The player fathers a child on his lover while campaigning; he never rides back to her inside 30 days, so `TryOfferTheFeast` never fires (it requires `IsCoLocated(mother)`). The record keeps `Owned = NeverArose`. `HouseOfThePlayerLine` then emits "Ã¢â‚¬Â¦I have owned before the world as mine, by Ira" Ã¢â‚¬â€ a public claim the player never made and was never asked to make Ã¢â‚¬â€ while `AwaitsTheName` stays false, so the late giving-of-the-name is permanently unreachable for that child.

### HouseOfThePlayerLine reports every pre-existing child as born outside the marriage
[v2:9cfd3bd2ef24aae0b6008e963f9a1b0c3468fde747e73e8710b3963176a3ce67] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Births.cs:735`

**Breaks:** `BirthRecord.BornInMarriage` is new and its own doc-comment states "Old records load false" Ã¢â‚¬â€ which is precisely why `IsOwnedBeforeTheWorld` was made to lean on `Acknowledgement.NeverArose` instead. But `HouseOfThePlayerLine` reads `BornInMarriage` directly, both for the `anyOutside` gate (line 735) and for `HouseChild.InMarriage` (line 743), so the migration guard is bypassed on the other side: old records are all classified as born OUTSIDE a marriage. The resulting line is attached to every wife's and every lover's sheet (ImmersiveChatBehavior.cs:3936-3937).

**Scenario:** A player loads an existing campaign in which Mizam and his wife Sibylla have two children already recorded in `_births`. Those JSON files have no `bornInMarriage` field, so it deserializes false. `anyOutside` becomes true, the line is emitted, and because `Owned` is NeverArose (so `IsOwnedBeforeTheWorld` is true) both children land in the `owned` bucket: Sibylla's own sheet now reads "Ira and Tulag I have owned before the world as mine, by Sibylla." Ã¢â‚¬â€ the mod telling a wife her legitimate children are acknowledged bastards, on every reply, for the rest of the campaign.

### The declared-stage cushion never expires for a lover, so her band can never fall
[v2:9cfd3bd2ef24aae0b6008e963f9a1b0c3468fde747e73e8710b3963176a3ce67] `src/ImmersiveAI.Core/Courtship/HeartBands.cs:87`

**Breaks:** HeartBands' own class remark declares the invariant "PAST THE SEAL THERE IS NO CUSHION Ã¢â‚¬â€ betrothal, marriage, THE LOVER'S BOND: from there what you see is what is there", and the design record's guardrail "No third relationship number Ã¢â‚¬â€ bands are derived, or they will drift from the truth". The code suppresses the cushion only for `stage >= CourtshipStage.Betrothed`; the lover's bond is not a CourtshipStage, and `Of` has no bond parameter. A lover's `CourtshipStage` is permanently pinned at Devotion or Ready (LoverRoad.ForksFrom = Devotion; JudgeTake rejects Betrothed+; OnLoverSealed writes only `memory.LoverBond`, never the stage), so the `declared` floor applies to her forever.

**Scenario:** Sibylla is the player's lover, sealed at CourtshipStage.Ready. Her regard collapses to -100. `HeartBands.Of(-100, Ready)` returns Love (OfRelation gives Neutral, declared gives Love, the max wins). At ImmersiveChatBehavior.Doors.cs:93 that Love goes into `DoorReasons.StandingOf`, `OpensTo(Love, Lover)` is true, so DoorStanding is Open Ã¢â‚¬â€ a lover's door can never report Coldness no matter how far the feeling thins, which is exactly the wife/lover asymmetry the file exists to implement. At ImmersiveChatBehavior.Lovers.cs:814 the bond line tells the player "yours Ã¢â‚¬Â¦ which stands at love" for a woman who despises him, while `LoverRoad.HeartHasLeft(-100, Lover)` (which correctly uses the uncushioned `OfRelation`) says the opposite. The unit tests only exercise `StandingOf` with a hand-supplied band, so they pass.

### "Between us" never opens its composed page for a wife or a mother Ã¢â‚¬â€ the doors and the naming action are unreachable
[v2:ab8b77d28b6bb3b6eee6a1c3f4f3a78ba22ed89878b79cc1e572688547008178] `src/ImmersiveAI.Module/UI/TalkScreen/TalkScreenVM.cs:1024`

**Breaks:** RoadPageFor composes the whole page (DoorPageFor's written reasons first, the lover line, the stage page, and the "you have never owned X before the world" section plus its ActionLabel) into page.Body/page.ActionLabel, and RefreshSelectionState assigns them to MisgivingsBodyText/RoadActionText (TalkScreenVM.cs:727-728). But ExecuteToggleMisgivings short-circuits on the *stage* Kind: WeddingDay calls ShowWeddingViewFor(npc) and returns, Wedding calls OpenWeddingDoorFor(npc) and returns Ã¢â‚¬â€ neither ever sets IsMisgivingsShown, and ShowWeddingViewFor renders the wedding *keepsake* body, not page.Body. RoadStagePage returns Kind=WeddingDay for ANY soul with a written wedding record OR any written child account (TryGetWeddingView, ImmersiveChatBehavior.Weddings.cs:733-764), so every wife and every mother of a chronicled child lands in that branch. The design record makes this page the only feedback the repentance loop gives ("the player SEES the spiral here Ã¢â‚¬â€ not as a meter, as her words") and the only door to the late giving of the name. Same code at ChatWindowVM.cs:778.

**Scenario:** Player is wed to Sibylla (wedding chronicled). A duty night lays a reason on her door via LayDutyNightReason; DoorLabelFor shows "one thing stands between you" in the bond line, and RoadPageFor builds a body containing her written reason and what would answer it. Player clicks "Between us" Ã¢â€ â€™ the wedding cutscene replays and the wedding keepsake popup opens; her reasons are never shown, at any point, by any button. Same click on a lover with an unowned child whose hour was written: the page carrying ActionLabel "Give Ira your name" is skipped, so ExecuteRoadAction can never fire and the child can never be owned late.

### "Go to her anyway" is offered Ã¢â‚¬â€ and executed Ã¢â‚¬â€ for a wife who is nowhere near the player
[v2:c5ae195111e4173eb6f038470e313c835090124565801a34c7110d24f5164bdc] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Doors.cs:115`

**Breaks:** `CanGoToHerAnyway` checks the door standing but never presence (no IsCoLocated, no IsBehindClosedDoors, no IsPrisoner). Every other path into a night gets that from `NightBlockFor`, but the duty option is deliberately offered *because* NightBlockFor returned a block, so the presence half of that block is discarded. `GoToHerAnyway` (Nights.cs:776-790) re-checks only `CanGoToHerAnyway` and that `DoorBlockFor` is non-empty Ã¢â‚¬â€ and DoorBlockFor is non-empty for a woman on the other side of Calradia.

**Scenario:** A wife sits in Onira at relation -5 (band Neutral Ã¢â€ â€™ DoorStanding.Coldness Ã¢â‚¬â€ no written reason needed) while the player is a week's ride away. `WomenOfTheHearth()` returns all wives regardless of location, so at dusk `NightBlockFor` gives "she is not with you" and greys her row Ã¢â‚¬â€ then Nights.cs:466 adds an ENABLED "<name> Ã¢â‚¬â€ go to her anyway" element beside it. Clicking it records a NightKind.Duty night, runs `RollForAChild` (which has no presence check of its own), can conceive a child, consumes the hearth-wide cooldown, and lays a spiral DoorReason Ã¢â‚¬â€ with a woman the player is not standing next to. `HandleTheEvening` (Nights.cs:294) even raises the dusk question on an evening where the ONLY candidate is that distant wife, since `shutAgainstYou.Count > 0` short-circuits the "every door closed, stay quiet" branch.

### A lover's door can never go cold Ã¢â‚¬â€ the declared-stage cushion floors her band at Like forever
[v2:c5ae195111e4173eb6f038470e313c835090124565801a34c7110d24f5164bdc] `src/ImmersiveAI.Core/Courtship/HeartBands.cs:87`

**Breaks:** Breaks the design's stated asymmetry ("with a LOVER it unlocks only at deepLove and is lost already below like") and HeartBands' own doc contract ("PAST THE SEAL THERE IS NO CUSHION Ã¢â‚¬â€ betrothal, marriage, the lover's bond"). `Of()` only drops the cushion at `stage >= Betrothed`, but a lover's bond never advances CourtshipStage Ã¢â‚¬â€ `OnLoverSealed` leaves it where it was and `MakeLoverFor` pins it to `LoverRoad.ForksFrom` (= Devotion). Devotion's declared band IS `HeartBand.Like`, which is exactly the rung `OpensTo(band, BondKind.Lover)` requires.

**Scenario:** Sibylla is sealed as a lover at CourtshipStage.Devotion. Her relation later falls to -100 (leaks, a duty night elsewhere, whatever). `HeartBands.Of(-100, Devotion)` returns Like, not Neutral, so `DoorReasons.StandingOf` (Doors.cs:71, called from ImmersiveChatBehavior.Doors.cs:93) can never return `DoorStanding.Coldness` for her Ã¢â‚¬â€ a lover's door only ever shuts if she happens to write a reason with her tool. In the same breath `LoverStandingLine` (Lovers.cs:814) tells the player "yours, with nothing holding it but the heart Ã¢â‚¬â€ which stands at fondness" about a woman who despises him. `LoverRoad.HeartHasLeft` uses `OfRelation` (uncushioned) and is uncalled, so nothing else catches it.

## MEDIUM

### A pending pre-update feast offer asks the ownership question and can mark a legitimate child Withheld
[v2:15ceb433356901149c5acbe4dd772c66f9ab472ca885bdeff51721977ce2fdb5] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Births.cs:540`

**Breaks:** Same missing migration on BornInMarriage. `bool mustOwnIt = !record.BornInMarriage;` drives the popup title, the "Own the child Ã¢â‚¬â€ no feast" option, the extra body paragraph ("<mother> is not your wifeÃ¢â‚¬Â¦") and the decline label; and DeclineTheFeast calls WithholdTheName (line 819+), whose only guard is `record.BornInMarriage` (line 669). A record saved before this batch reads false on both.

**Scenario:** Player's wife bears a child; the player is away at war so the feast offer is deferred (AwaitingFeastOffer, 30-day window). The player updates the mod mid-window. Next time he rides in, the popup asks "Is this child yours before the world?" and says "<wife> is not your wife." Ã¢â‚¬â€ about his own wife. Choosing "Say nothing" sets Owned = Withheld, so AwaitsTheName becomes true and the child now appears forever in the unsaid group: "<wife> is raising a child I have never owned before anyone."

### A duty night is silently dropped from the TogetherLine
[v2:15ceb433356901149c5acbe4dd772c66f9ab472ca885bdeff51721977ce2fdb5] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Nights.cs:1845`

**Breaks:** NightTimelineLine has no case for NightKind.Duty, so it falls to `default:` and returns string.Empty (AtWar is never set by BuildNightRecord). TogetherBlockFor drops empty texts (line 1735). Meanwhile NightsRollFor only shows nights up TO the alone-line (line 1623) and LastAloneDay counts only NightKind.Together (line 1675), so a duty night sits after the line and is excluded from the roll as well. The design record makes the TogetherLine the home of "the unaddressed, the coldness, the closed door" Ã¢â‚¬â€ the duty night is the single most consequential entry it can carry.

**Scenario:** Player goes through his wife's shut door on day 120. Her memory gets the fixed DutyBeat and a DoorReason is laid, but her situation block's "since we were last alone" list Ã¢â‚¬â€ which is rebuilt on every reply and is where she reads what has happened and not been spoken of Ã¢â‚¬â€ shows nothing at all for that night. It only surfaces later, in the fortnight roll, once a talk ends and moves the alone-line past day 120.

### Clicking the dusk notice drops lovers, and does nothing at all in a lover-only household
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Nights.cs:392`

**Breaks:** `OnNightNoticeInspected` builds the choice list from `SpousesOfPlayer()`, while `HandleTheEvening` and `RaiseNightNotice` both use `WomenOfTheHearth()` (wives + lovers). The notice is raised for, and wears the portrait of, a woman the popup it opens may not contain. `WomenOfTheHearth`'s own remark says the whole batch depends on that list being complete.

**Scenario:** The player has no wife and one lover, Ira, riding with him. At dusk `HandleTheEvening` finds Ira open, raises the notice wearing her face and the line "Ira is here. Where will you sleep tonight?". The player clicks it: `SpousesOfPlayer()` is empty, `wives.Count > 0` is false, and nothing opens Ã¢â‚¬â€ the notice is consumed (`_nightNoticeDay` reset) and the evening is lost. With a wife *and* a lover, the popup opens listing only the wife, and the lover the notice was raised about is absent from it.

### "Give them your name" has no door in the classic chat window
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/UI/ChatWindow/ChatWindowVM.cs:517`

**Breaks:** `RoadPageFor` now returns `ActionLabel`/`ActionSubject`, and only `TalkScreenVM` grew `RoadActionText`/`HasRoadAction`/`ExecuteRoadAction` plus the button in ImmersiveTalkScreen.xml. `ChatWindowVM` consumes the same `RoadPage` but reads only `Label`/`Title`/`Body`, and ImmersiveChatWindow.xml got no action button Ã¢â‚¬â€ so the late recognition act has no player-facing entry point on that path at all (`GiveTheNameTo` is reached nowhere else).

**Scenario:** A player running `UseClassicChatWindow: true`, or any player after the talk screen's automatic session fallback fires, opens Between us on his lover. The page tells him "You have never owned Vlad before the worldÃ¢â‚¬Â¦ Until it is, the world speaks of the child as its mother's" and the hint says "A child of theirs is still waiting on your name" Ã¢â‚¬â€ and there is no button anywhere to give it. The one heavy act of the recognition road is unreachable for the rest of that session.

### PlayerHouseLine is written in the player's first person and folded into the NPC's own first-person sheet
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Core/Prompts/PromptBuilder.cs:669`

**Breaks:** The whole system sheet is the NPC's own mind in the first person (the project's standing law), but `BirthText.HouseLine` (BirthText.cs:170, 177, 187) writes the PLAYER's voice Ã¢â‚¬â€ "Of my house: Ã¢â‚¬Â¦ born in my marriage.", "Ã¢â‚¬Â¦ I have owned before the world as mine, by <mother>.", "Ã¢â‚¬Â¦ I have never owned before anyone Ã¢â‚¬Â¦ I have said nothing." It is assigned at ImmersiveChatBehavior.cs:3937 to any soul with a bed (wife or lover) and appended raw beside `CourtshipTerms`, which are all hers. Every "I" in the block reads as the NPC's.

**Scenario:** Player Mizam, wife Sibylla, lover Ira with an unowned son Vlad. Sibylla's sheet reads "Of my house: Ida and Toma, born in my marriage. Ira is raising a child I have never owned before anyone. What the world privately believes about that is its own affair; I have said nothing." She is handed Mizam's acts and Mizam's silence as her own, in the middle of a sheet where every other first person is hers Ã¢â‚¬â€ and "born in my marriage" over a co-wife's children on top of it.

### The child's own first beat asserts recognition before the player has been asked
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Births.cs:473`

**Breaks:** `RecordChildsOwnBeginning` runs from `RecordHourBeats`, i.e. as soon as the hour account lands (minutes after the birth), and reads `record.IsOwnedBeforeTheWorld` Ã¢â‚¬â€ which at that moment is always true for an out-of-marriage child, because the three-way owning question has not been put yet and `Owned` is still `NeverArose`. Beats are permanent and never reworded (BirthText's own rule), so the child's memory keeps a claim that the ledger, the mother's `MotherNameBeat` and `HouseLine` may all contradict a day later. The design's whole point of this beat is that "an unrecognized child may grow up having CHOSEN to remember the silence" Ã¢â‚¬â€ there is no silence to remember.

**Scenario:** Ira bears the player a son. The hour is written and the son's memory gets "Of my own beginning: I was born to IraÃ¢â‚¬Â¦ <Player> is my father, and the world was told so." Two days later the father rides in and picks "Say nothing"; `WithholdTheName` sets `Owned = Withheld` and writes the mother "he has not owned him before anyone". The son comes of age eighteen years later already knowing his father owned him publicly Ã¢â‚¬â€ the exact opposite of what happened, in the one memory the feature exists to create.

### The duty-night spiral stops biting: a 4-card deck against a cap of 5 standing reasons, and a refused lay is discarded
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Doors.cs:297`

**Breaks:** `LayDutyNightReason` draws one of `DoorText.DutyNightReasons` (exactly 4 cards, DoorText.cs:114) by FNV of the night id and calls `SetDown`, which refuses on `LooseMatch.Restates` against anything already standing and refuses past `MaxStandingOpen` (5). The refusal is dropped with `out _` and `return` Ã¢â‚¬â€ there is no fallback that hardens the standing reason instead. The design's guardrail is unconditional: "The spiral must bite: a duty night that costs nothing breaks the whole moral of the design. If playtests show it farmable, deepen the closure, do not soften the door."

**Scenario:** The player's wife closes her door with three written reasons of her own. He goes to her anyway; card 2 is laid (4 standing). He goes again the next night; card 2 is drawn again (1 in 4) Ã¢â‚¬â€ `Restates` matches, `SetDown` returns false, and the night costs nothing at all. He goes a third night; a new card lands (5 standing, at the cap). From the fourth duty night onward every single one is refused by the cap, forever: the road back never lengthens, nothing new appears on the Between-us page, and the duty night is free.

### The fresh-wound spike is only read for co-located souls; a wounded wife who is apart is stamped and never moved
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.cs:2436`

**Breaks:** `WoundSpikeFor` is consulted in exactly one place, `CoLocatedPull`. `MaybeStartNpcLetter` (Letters.cs:220-228) builds its pull from `Pull Ãƒâ€” StoryDepthFactor Ãƒâ€” OutreachDamping` and never reads `FreshWoundDay`, yet `MarkFreshWound` is fired for the distant/hearsay branch too (Nights.cs:1219, reached from the `!here` path at Nights.cs:1257-1268) and `LeakTheBond` stamps distant women as well (Lovers.cs:735). The design record states the spike explicitly for both roads: "she comes to you in the morning, or writes if apart." Half of it is wired.

**Scenario:** The player keeps his wife in his town with the house and children (the design's own premise for why proximity is the temptation) and takes a lover on the road. The lover-bond leak reaches the wife by word of mouth; `LeakTheBond` writes her beat and stamps `FreshWoundDay`. Because she is not co-located, the only roll she can enter is the letter roll, which never reads the stamp Ã¢â‚¬â€ so her damped, weeks-stale pull decides everything and no letter comes while it is hot. The stamp then decays to nothing on its own, and the confrontation the whole leakÃ¢â€ â€™spikeÃ¢â€ â€™talk loop exists for simply never happens for the most common wounded woman in the design.

### The wound spike never reaches a distant soul, so no leak ever produces a letter
[v2:9cfd3bd2ef24aae0b6008e963f9a1b0c3468fde747e73e8710b3963176a3ce67] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Letters.cs:220`

**Breaks:** `CoLocatedPull` applies `WoundSpikeFor` as a floor (ImmersiveChatBehavior.cs:2436), but the hourly letter roll computes its pull from `InitiationScorer.Pull Ãƒâ€” StoryDepthFactor Ãƒâ€” OutreachDamping` with no wound term. The design record says the spike exists so "she comes to you in the morning, or writes if apart", and the comment at ImmersiveChatBehavior.Nights.cs:1219 claims outright that the wound "will move her to come and say something, wherever she stands". For anyone not co-located, it moves nothing.

**Scenario:** A wife governing a town three weeks' ride away hears by rumour that the player spent the night with another woman; `MarkFreshWound` stamps `FreshWoundDay`. `MaybeStartNpcLetter` skips her (she is not co-located, so the face-to-face path never sees her) and computes her letter pull from the ordinary damped bond, which after an earlier unanswered letter can be near zero. The confrontation the leak was supposed to trigger never arrives in any form, and the stamp simply decays or gets cleared by an unrelated outreach.

### A duty night is invisible in the TogetherLine Ã¢â‚¬â€ no NightKind.Duty case in NightTimelineLine
[v2:9cfd3bd2ef24aae0b6008e963f9a1b0c3468fde747e73e8710b3963176a3ce67] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Nights.cs:1845`

**Breaks:** `NightKind.Duty` was added to the Core enum and handled in `NightText.LineFor`/`RunLine`, but `NightTimelineLine`'s switch has no case for it, so it falls to `default:` and returns `string.Empty` (AtWar is never set on a record built by `BuildNightRecord`). `TogetherLine.Build` filters out entries with blank Text. Meanwhile `NightsRollFor` (line 1623) keeps only nights with `GameDay <= line`, and `LastAloneDay` (line 1675) advances the line only on `NightKind.Together` Ã¢â‚¬â€ so a duty night sits after the line and is dropped by the one block that exists to carry what happened after it. This is the guardrail "The spiral must bite" losing its most visible half.

**Scenario:** The wife's door is shut. On Autumn 12 the player picks "Go to her anyway"; they do not speak afterwards. On the next exchange her situation block lists every market and battle since they last sat down, but the duty night produces an empty entry and is silently omitted from the since-list, and is also excluded from the nights roll because it post-dates the line. The only trace left in her sheet is the door reason; the night itself is nowhere in either nights block.

### The doors resolver's 'revise' overwrites her written reason with the search query
[v2:9cfd3bd2ef24aae0b6008e963f9a1b0c3468fde747e73e8710b3963176a3ce67] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Doors.cs:200`

**Breaks:** `DoorReasons.Revise(reasons, which, text, opens, out trouble)` takes the identifying query and the NEW wording as two separate arguments, but the resolver passes `matter` for both. `DoorTool`'s schema has no field carrying the new wording: `matter` is documented as "For every other deed, enough of the one I mean that it can be told apart from the rest" and `note` as "Only for 'settle'". (The misgivings' equivalent, `CourtshipMisgivings.Revise(list, which, into)`, gets `into` from its own `note` field Ã¢â‚¬â€ see MisgivingTool.cs:69.) So the verb either destroys her text or cannot fire at all.

**Scenario:** Her standing reason is "You gave Ira a jewel in front of the whole house and I heard of it before you told me". The model calls weigh_what_stands with deed="revise", matter="the jewel he gave Ira" (an identifier, exactly as the schema asks), opens="that you tell me such things yourself". `Find` matches by containment, then `found.Text = Tidy(matter)` replaces her whole grievance with the four-word fragment Ã¢â‚¬â€ the wording the player reads on the Between-us page and that she reads on her own sheet is now gone. The other branch is no better: if the model supplies a genuinely reworded sentence, LooseMatch.Best often fails to match it against the old one and the resolver answers "no reason of hers matches those words", so revise never works.

### The free "Own the child" choice is suppressed by a purse check meant for feasts
[v2:ab8b77d28b6bb3b6eee6a1c3f4f3a78ba22ed89878b79cc1e572688547008178] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Births.cs:529`

**Breaks:** TryOfferTheFeast returns false when the player's gold is under BirthTiers.All.Min(t => t.Price) (100 denars). That gate predates the three-way recognition choice added at line 540-546, where QuietOwning ("Own the child Ã¢â‚¬â€ no feast") costs nothing. The whole popup Ã¢â‚¬â€ including the only path to Acknowledgement.Given or Withheld Ã¢â‚¬â€ is hidden. If the offer window (BirthLedger.FeastOfferDays) then lapses, record.Owned stays NeverArose, which IsOwnedBeforeTheWorld reads as owned and AwaitsTheName reads as false (Core/Births/BirthRecord.cs:157-160), so the child never enters ChildrenAwaitingTheName and the late giving of the name is unreachable forever.

**Scenario:** Player with 60 denars fathers a child by his lover. OnGivenBirthForChronicle enqueues TryOfferTheFeast; the purse gate returns false. OfferPendingFeasts retries hourly with the same result while he is broke. Thirty days later AwaitingFeastOffer stops yielding the record; the bastard is silently treated as owned before the world, HouseOfThePlayerLine reports it as recognized, and the lover's craving (UnnamedChildrenOf) counts zero.

### The lover road never asks the game's marriage-suitability model, so the player's own kin pass every gate
[v2:ab8b77d28b6bb3b6eee6a1c3f4f3a78ba22ed89878b79cc1e572688547008178] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Lovers.cs:118`

**Breaks:** LoverWorldBlock checks alive/prisoner/sex/Spouse/age/occupation and nothing else. The only kinship check anywhere on this road is MarriageModel.IsCoupleSuitableForMarriage, which TrothBlockReason runs solely under `forWedding` (ImmersiveChatBehavior.Courtship.cs:245-250) Ã¢â‚¬â€ and the trunk that feeds the fork is now walked with forHand:false (Courtship.cs:528-529), so it never runs either. CanOfferSelf Ã¢â€ â€™ LoverRoad.JudgeTake takes WorldRefusesThePair straight from LoverWorldBlock, and OnLoverSealed re-runs the same judgment, so the seal passes too.

**Scenario:** The player's own unmarried adult sister (IsLord, Clan == PlayerClan, Spouse == null) walks the shared trunk to Devotion via tend_courtship, reaches relation 70, calls offer_myself, and OnLoverSealed seals the bond. IsOfPlayerHousehold is true so BringHerAlong adds her to the party with no ransom; the dusk question lists her, and RollForAChild/BorrowTheFatherSlot then produce a child of the player and his sister via MakePregnantAction.

### A duty night appears in neither the nights roll nor the TogetherLine Ã¢â‚¬â€ the wife's sheet never mentions it
[v2:ab8b77d28b6bb3b6eee6a1c3f4f3a78ba22ed89878b79cc1e572688547008178] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Nights.cs:1823`

**Breaks:** NightTimelineLine's switch has cases for Together/DoorClosed/Elsewhere/Alone and a default that returns string.Empty unless AtWar; NightKind.Duty falls into the default, and TogetherBlockFor drops empty text (ImmersiveChatBehavior.Nights.cs:1731). Meanwhile NightsRollFor only includes nights with GameDay <= LastAloneDay, and LastAloneDay is advanced only by Kind == Together, a talk that ended, or the wedding (ImmersiveChatBehavior.Nights.cs:1671, 1559) Ã¢â‚¬â€ a duty night moves neither. Core handles Duty deliberately everywhere it is reached (NightText.cs:274, 465-483; NightLedger.cs:185, 207), so this is the one place it was missed.

**Scenario:** Player picks "Ira Ã¢â‚¬â€ go to her anyway" at dusk. The record is stored with Kind=Duty. Her next reply is built: NightsRollFor filters it out (its GameDay > the line, which last moved at the previous talk's end), and TogetherBlockFor iterates it but NightTimelineLine returns "" so it is skipped. The night the whole spiral is built on is absent from both blocks of her sheet.

### Clicking the dusk notice opens the evening for wives only Ã¢â‚¬â€ a lover-only household gets a dead click
[v2:ab8b77d28b6bb3b6eee6a1c3f4f3a78ba22ed89878b79cc1e572688547008178] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Nights.cs:391`

**Breaks:** HandleTheEvening builds its list from WomenOfTheHearth() (wives + lovers, ImmersiveChatBehavior.Nights.cs:281) and RaiseNightNotice is handed that list, but the notice's click handler calls SpousesOfPlayer() instead, then `if (wives.Count > 0)`. The whole point of WomenOfTheHearth (its own doc comment: "a lover who was not in it would have no nights, so nothing to leakÃ¢â‚¬Â¦") is defeated on the one path the player actually uses when UseMapNoticeForInitiations is on, which is the default.

**Scenario:** Player is unmarried and has one lover riding with him. At hour 21 HandleTheEvening finds her open, stamps _nightAskedOnDay, and raises the portrait notice "Ira is here. Where will you sleep tonight?". Player clicks it: _nightNoticeDay is cleared, SpousesOfPlayer() returns empty, no inquiry opens, the notice is removed, and _nightAskedOnDay blocks any retry that day Ã¢â‚¬â€ the evening is silently lost. With a wife *and* a lover, the popup that opens lists only the wife.

### weigh_what_stands 'revise' overwrites her written reason with the fragment used to find it
[v2:ab8b77d28b6bb3b6eee6a1c3f4f3a78ba22ed89878b79cc1e572688547008178] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Doors.cs:200`

**Breaks:** DoorReasons.Revise's signature is (reasons, which, text, opens, out trouble) Ã¢â‚¬â€ `which` locates the reason, `text` is the new wording, and it assigns `found.Text = Tidy(text)` (Doors/DoorReasons.cs:152-163). The resolver passes `matter` for both. DoorTool's schema defines `matter` for non-set_down deeds as "enough of the one I mean that it can be told apart from the rest" (Tools/DoorTool.cs:62-66) and `note` as settle-only, so no parameter carries the new wording at all: revise can only no-op or truncate. The parallel misgivings resolver passes text and note correctly (ImmersiveChatBehavior.Courtship.cs:707).

**Scenario:** Her standing reason reads "He gave the jewel to Ira in front of the whole house, and I heard of it from a servant before he told me." She calls weigh_what_stands with deed="revise", matter="the jewel to Ira". LooseMatch.Best finds the reason, body is non-empty, and found.Text is replaced with "the jewel to Ira" Ã¢â‚¬â€ persisted to memories.json, shown on her sheet, in DuskLine, and on the Between-us page Ã¢â‚¬â€ while the resolver answers "I have reworded it."

### weigh_what_stands 'revise' has no field for the new wording, so the resolver rewrites a reason with itself and reports success
[v2:c5ae195111e4173eb6f038470e313c835090124565801a34c7110d24f5164bdc] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Doors.cs:200`

**Breaks:** The schema tells the model that `matter` is the LOCATOR for every deed but set_down ("enough of the one I mean that it can be told apart from the rest"), that `note` is "Only for 'settle'", and that `opens` is the way-back Ã¢â‚¬â€ there is no parameter that holds new text. The resolver nonetheless calls `DoorReasons.Revise(reasons, which: matter, text: matter, opens, Ã¢â‚¬Â¦)`, so a model that follows the schema sets `found.Text` to the string it just matched on. `Revise` returns true whenever it found something, so the branch reports success, consumes the turn's single deed (`door.Acted = true`), and fires the frost/rose notice Ã¢â‚¬â€ violating the project's own "a resolver branch must never do nothing SILENTLY" rule in the loudest possible way (it says the opposite of nothing). The misgivings tool solved exactly this by carrying the new wording in `note` (MisgivingTool.cs:66-70).

**Scenario:** Her door carries "he gave the jewel to Ira in front of the whole house". She calls weigh_what_stands{deed:"revise", matter:"he gave the jewel to Ira", opens:"Ã¢â‚¬Â¦"} intending to reword it. LooseMatch finds the reason, sets its Text to "he gave the jewel to Ira" (no change of substance), and the tool answers "I have reworded it. The shape of it changed, as such things do when they are talked around and not answered." Nothing on the list moved, the turn's one deed is spent, and the model has been told it succeeded, so it will not retry in the rounds it has left.

### The player's house line is written in the PLAYER's first person and folded unframed into the NPC's own first-person sheet
[v2:c5ae195111e4173eb6f038470e313c835090124565801a34c7110d24f5164bdc] `src/ImmersiveAI.Core/Prompts/PromptBuilder.cs:672`

**Breaks:** `BirthText.HouseLine` emits the player's own "I" Ã¢â‚¬â€ "Of my house: Ã¢â‚¬Â¦ born in my marriage.", "Ã¢â‚¬Â¦ I have owned before the world as mine, by <mother>.", "Ã¢â‚¬Â¦ I have never owned before anyone Ã¢â‚¬Â¦ I have said nothing." PromptBuilder appends it with no heading and no attribution, sandwiched between `CourtshipTerms` and `LoverTerms`, inside the block whose every other line is the NPC's own first person. CLAUDE.md's standing law is that the system sheet is the NPC's OWN mind in the first person; this hands her the player's mind wearing the same pronoun.

**Scenario:** The player has a son Ives by his wife Ira and an unrecognized child by his lover Sibylla. `BuildContext` (ImmersiveChatBehavior.cs:3936) sets `PlayerHouseLine` for both women, so Ira's sheet reads, in her own voice: "Of my house: Ives, born in my marriage. Sibylla is raising a child I have never owned before anyone. What the world privately believes about that is its own affair; I have said nothing." She will speak as though she fathered the children and withheld the name; Sibylla, who has no marriage, reads "born in my marriage" about someone else's son.

### A child's own first memory asserts it was owned before the world, days before the player is asked
[v2:c5ae195111e4173eb6f038470e313c835090124565801a34c7110d24f5164bdc] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Births.cs:476`

**Breaks:** `RecordChildsOwnBeginning` passes `record.IsOwnedBeforeTheWorld`, which is `Owned != Withheld` Ã¢â‚¬â€ true while `Owned` is still the default `NeverArose`. But the beat is written at the HOUR (`RecordHourBeats`, called from `FinishBirthHour`), while `Owned` is only set later, by `OwnTheChild`/`WithholdTheName` in the feast-offer callbacks, which the design deliberately allows to arrive up to 30 days later (`AwaitingFeastOffer`). The beat is never revised; only an additional `ChildNameBeat` is appended if the child is later owned. This is the exact fiction the recognition feature exists to model, written backwards.

**Scenario:** The player's lover bears his child while he is at war. `TryOfferTheFeast` defers (father not present); the hour account returns minutes later and each newborn's memory gets `BirthText.ChildBornBeat(..., owned: true)` Ã¢â€ â€™ "<Father> is my father, and the world was told so." Twelve days later the player rides in, is asked, and picks "Say nothing" Ã¢â€ â€™ `Owned = Withheld`. The child now grows up with a memory file that says the world was told, `RoadPageFor` tells the player it never was, and when the child comes of age it speaks from the false version.

## LOW

### The classic chat window never shows the road page's action button
[v2:15ceb433356901149c5acbe4dd772c66f9ab472ca885bdeff51721977ce2fdb5] `src/ImmersiveAI.Module/UI/ChatWindow/ChatWindowVM.cs:522`

**Breaks:** RoadPage gained ActionLabel/ActionSubject and TalkScreenVM binds them to RoadActionText / HasRoadAction / ExecuteRoadAction with a button in ImmersiveTalkScreen.xml. ChatWindowVM's equivalent block (lines 517-528) reads only Label/Title/Body, and ImmersiveChatWindow.xml got only the four new Dev buttons. UseClassicChatWindow is a supported, documented path (and the automatic session fallback when the talk screen bows out).

**Scenario:** Player sets UseClassicChatWindow: true (or the talk screen falls back after a tableau failure), has an unrecognized child by a lover, and opens "Between us". The page says "You have never owned <child> before the worldÃ¢â‚¬Â¦" but there is no button anywhere to do it Ã¢â‚¬â€ the only remaining route is the DevMode-gated DevGiveTheName.

### The bond leak and the wound stamp bypass the in-flight-exchange discipline and can be silently overwritten
[v2:2db0eb889d8cfdb4da2338b25ab0c6fe3052fcb876686f016c680b95e3ef762e] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Lovers.cs:732`

**Breaks:** `LoadMemory` re-reads `memories.json` from disk on every call, so a soul with an exchange in flight is holding an instance loaded earlier that will be written back when her reply lands. Every other writer into another soul's memory guards for this Ã¢â‚¬â€ `WriteNightBeat` parks the beat via `IsExchangeInFlight` + `_pendingWeddingBeats`, and `SaveMemory` folds pending beats back in. `LeakTheBond` (Lovers.cs:732-736) and `MarkFreshWound` (Nights.cs:1233-1237) call `LoadMemory`/`SaveMemory` directly with no such guard.

**Scenario:** The player sends a line to his wife Sibylla in the talk screen, then Ã¢â‚¬â€ while her reply is still in flight Ã¢â‚¬â€ seals the lover bond with Ira. `LeakTheBond` loads Sibylla's memory, appends `OtherWomanLearnsBeat`, sets `FreshWoundDay` and saves. Sibylla's reply lands a second later and `SaveMemory` writes her older instance over it. The beat and the wound stamp are gone: she never learned, so she never confronts him, and nothing anywhere records that the leak was lost.

### CoLocatedPull returns before the wound spike for a soul with no shared story
[v2:9cfd3bd2ef24aae0b6008e963f9a1b0c3468fde747e73e8710b3963176a3ce67] `src/ImmersiveAI.Module/ImmersiveChatBehavior.cs:2413`

**Breaks:** The `known == null || known.Richness <= 0` branch returns `floor * hearth * stationFactor` and never reaches `Math.Max(pull, WoundSpikeFor(known, nowDay))` on line 2436. That contradicts `WoundSpikeAtOnce`'s own stated purpose Ã¢â‚¬â€ "High enough that a bond with almost no pull of its own still crosses the room, because the woman who most needs to say something is very often the one who has been talked to least" Ã¢â‚¬â€ and the rationale asserted by DoorTests.AFreshWoundMovesHerAtOnce_AndStopsBeingNewsWithinTwoDays, which only checks the Core function in isolation.

**Scenario:** A wife married through vanilla whom the player has never spoken to through the mod (memories.json exists with TotalTurns 0, because MarkFreshWound just created it) is in the same town and learns he spent the night with another wife. Her pull stays at 0.1 Ãƒâ€” 4.5 = 0.45 instead of being floored at WoundSpike(0) = 0.85, so the one moment the feature was built for is roughly halved in likelihood for exactly the soul the constant's comment names.

### LeakTheBond writes another woman's memory without the in-flight parking discipline
[v2:ab8b77d28b6bb3b6eee6a1c3f4f3a78ba22ed89878b79cc1e572688547008178] `src/ImmersiveAI.Module/ImmersiveChatBehavior.Lovers.cs:732`

**Breaks:** It does LoadMemory(other) Ã¢â€ â€™ AddSilentInnerBeat Ã¢â€ â€™ set FreshWoundDay Ã¢â€ â€™ SaveMemory(other, Ã¢â‚¬Â¦) directly. Every sibling that writes into a third party's memory checks IsExchangeInFlight first and parks the beat (WriteNightBeat, ImmersiveChatBehavior.Nights.cs:1593; WriteBirthBeat, Births.cs:1101; WriteWeddingBeat), and SaveMemory only folds the three registered pending kinds (blessing, wedding beats, birth beats Ã¢â‚¬â€ ImmersiveChatBehavior.cs:836-838). A soul mid-exchange holds a memory instance loaded before this write and saves over it at the end of her turn, taking both the leak beat and the FreshWoundDay stamp with it. MarkFreshWound (Nights.cs:1234) has the same shape.

**Scenario:** In the talk screen the player sends a line to his wife Sibylla; her reply is still in flight. He switches to Ira's thread, her offer_myself lands, and he seals it. OnLoverSealed Ã¢â€ â€™ LeakTheBond rolls a hit for Sibylla, writes her beat and stamps FreshWoundDay. Sibylla's reply then lands and SaveMemory writes her stale instance: the beat and the wound stamp are gone, so the WoundSpike floor never fires and the morning-after confrontation Ã¢â‚¬â€ the design's core loop Ã¢â‚¬â€ never happens.

