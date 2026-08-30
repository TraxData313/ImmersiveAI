WHERE THINGS STAND (2026.08.30 — **v3.2.0 IS ON STEAM; NEXUS STILL TO UPLOAD**)

  ** LEFT FOR ANTON — THE NEXUS HALF OF v3.2.0
      Steam is done (uploaded 2026.08.30, item 3764210301). Nexus mod 12119 → Manage → Files:
        - Update existing file, tick Archive existing, and SET THE DISPLAY NAME BACK to
          `ImmersiveAI V3.2.0` (picking the old file silently overwrites it).
        - Paste the 255-char block from CHANGELOG.md's v3.2.0 section into BOTH the file
          Description and Add changelog.
        - Set the MOD version to 3.2.0 on the General step — that is the field Vortex reads.
        - COME BACK AN HOUR LATER for the virus scan. v3.0.0 was quarantined for a day.
        - The zip: dist\ImmersiveAI_v3.2.0.zip
      The Steam description needs no repaste — the page describes the mod, not the build.


  ** NEXT PLAYTEST — DID THE VOICES STOP DERAILING? (2026.08.17)
      Anton heard Sibylla hold one wordless note for ~30 s mid-reply. Three things shipped for it:
      a guard that cuts such a note within ~2 s, a character whitelist before the engine, and the
      speech engine's own sampling restored (we had cooled it for a problem streaming had already
      solved). Root cause was narrowed by comparing against claude-voice — same DLL, same card,
      0.1% derails against our 6% — but WHICH of the two differences carried it is not known.
      WHAT TO CHECK, all in Configs\ImmersiveAI\voicehost.log:
        - `derail guard` lines. Twelve in 196 readings before; they should now be rare or gone.
          If they are NOT, set VoiceTemperature 0.55 / VoiceTopP 0.85 in config.json to put half
          the change back and compare — one edit, no rebuild.
        - `listening to ...: swing= quiet=` lines, which the new guard writes for every piece it
          judges. These are the first real numbers for what a derail measures against speech; the
          two thresholds (DroneSwing 0.35, DroneQuietFraction 0.05) were REASONED, not measured.
        - Whether the guard ever cut an honest reading. It only judges past the end of the words,
          so it should be impossible — but "should be" is why this is on the list.
      BY EAR: the restored sampling is the one change that could cost something. Listen to a long
      multi-sentence reply and check the voice still sounds like the one that was cloned. If it
      wanders, VoiceTemperature is the dial and lower is tighter.
      ALSO SEEN, not chased: a VoiceHost found alive with the game long closed, holding its VRAM.
      The watchdog (parent PID + stdin EOF) is supposed to make that impossible.

  ** LEFT FOR ANTON:
      1. WATCH THE NEXUS VIRUS SCAN. v3.0.0 was AUTOMATICALLY QUARANTINED there ("may be unsafe",
         undownloadable) and it sat that way for a day unnoticed — almost certainly the voice host
         .exe, which first shipped in that release. v3.1.0 is in the scanner queue now; if it is
         quarantined too, the file page’s "How can I fix this?" appeal can only be filed by the
         account owner. Until it clears, NEXUS HAS NO WORKING DOWNLOAD and Steam is the only road.
      2. Press the download button on a machine with no engine — the one path in v3.1.0 that has
         been read but never clicked. (Anton has the engine already, so the button hides for him.)
      3. Optional, whenever the pages are next touched: both store descriptions gained one clause
         about voices ("one button installs it (NVIDIA card, or a key for hosted voices)").
         docs\steam-page-final.bbcode.txt is 7709 bytes of 8000. A paste, nothing more.

  v3.0.0 went up to the Workshop on 2026.08.16 ("Uploading done!", 10,747,516 bytes): 54 commits
  since v2.2.1 six days before — the one talk screen, the voices whole, the birth chronicle, the
  entire after-the-wedding batch, the reworked coming-to-you, and a long tail of real fixes. It
  ships with an honest note at the top of every change-note tier saying much of it is freshly built,
  that this is one pair of hands, and asking for reports — Anton's call, and the right one.

  804 Core tests green, everything below built, committed and DEPLOYED — and almost none of it
  played. The post-marriage batch, the voices, and this night's whole pass are all unplaytested;
  what comes back from players is now the fastest road to knowing which parts are real.

  THIS NIGHT LANDED, in order: the voices reading the *acted* parts; her answer spoken with the
  screen shut; the night's clock kept by the sun instead of a drifting hour count; the voice BUZZ
  (it was a WAV piece poured before it was finished, byte-shifting everything after it); the item
  flood in towns (a computed view that aliased what it described, doubling on every save/load); the
  scouts who could not see prisoners or wounded; a gang leader who believed he had no men; the
  conception maths, which was genuinely wrong and is now provably right; and THE STAGE, all three
  entries of it — the tavern set, moving the talk between a town's rooms, and the hearth rebuilt as
  a mode of the talk screen.

  THE STAGE'S ARCHITECTURE, since it was the open question: there is no scene arbitration, because
  the hearth REUSES the talk screen's host instead of being a second one. Two tableau-hosting
  screens over one shared cached scene is where the hard native crash lives, and a MODE never
  creates that situation. Reasoning in docs/after-the-wedding-design.md under "The hearth window
  becomes a stage" — the banner's three questions are answered there now.

  UNAPPLIED AND WAITING: docs/workflow-results-2026-08-16.json holds one reader's verdicts on the
  twenty untriaged review leads (the refuting half died on a session limit, twice). Three births
  findings in it look real and are named in their own TODO entry below. Nothing from that file has
  been applied.

  Docs that hold the rest: docs/after-the-wedding-design.md (its SPIRIT section IS the feature and
  must be read whole), docs/next-session-handoff.md, docs/review-findings-2026-08-15.md, CLAUDE.md.

BUGS:
- [x] THE VOICE BUZZES ON A LONG READING — FIXED 2026.08.16 (Anton, reproduced twice on a night
      account, then confirmed general: "when the message is too long it bugs, most places between
      new lines... when I turn the whole thing to be loaded it works fine").
      NOT the text, and not the budget. Both earlier suspects are dead and should not be read again:
      the moon card hands `WithVoice` the CLEAN account (the stamp is its own narration row with no
      play mark), and Full read of the SAME words is clean, which clears the words themselves.
      THE CAUSE IS THE JOIN. Streaming publishes a piece the instant it EXISTS on disk, not the
      instant it is finished (`File.Exists`, VoiceService.RunJobAsync's onChunk), and `WavFiles
      .TryRead` deliberately clamps an over-long declared data size down to the bytes really there —
      so a piece caught mid-write reads back as a perfectly healthy SHORT clip. Pour that after
      another piece and every sample downstream is byte-shifted: sixteen-bit samples read half a
      frame out are not a click, they are a drone for the rest of the take. Full read never showed
      it because the host joins its own pieces and only announces a closed file. Longer readings
      have more pieces and so more chances to catch one — which is exactly the length correlation.
      THE FIX, in Core `WavFiles` with three tests: `WavInfo` remembers what the header CLAIMED
      (`DeclaredDataBytes`) beside what it clamped to, `LooksUnfinished` tells the two apart —
      carefully NOT fooled by the `0xFFFFFFFF` "I do not know yet" a streaming writer leaves — and
      `Join` refuses the whole join if any piece is unfinished, falling back to playing them one at
      a time. A seam is a far smaller wound than a buzz. Every piece is also poured on a whole
      number of sample FRAMES, so one stray byte can never shift the rest.
      The lesson was already written in that same file, one method up: "a lenient reader is a good
      thing, but it HIDES malformed input from everything downstream that is not lenient."
      STILL WORTH DOING, not blocking: the join could WAIT for an unfinished piece and re-join
      rather than giving up on the take, and the publish could hold a piece back until the host says
      it is closed. Both want the real engine in front of them. UNPLAYTESTED — Anton should hear a
      long reply on Streaming and say whether it is now one clean take, seams and all.

- [x] THE ITEM FLOOD IN TOWNS — FIXED 2026.08.16 (Anton's screenshot: one town stop listing
      "Silver Ore x6, Cow x4, Salt x19, Scarf, Curved Boots, Rugged Saddle" four times over).
      NOT the writer, and not the prose. A JSON ROUND-TRIP ARTIFACT: `JourneyLog.OpenVisit` is a
      computed get-only property that ALIASES a live element of `Visits`. Newtonsoft serialized it,
      so an open stop was written to the file TWICE; and on the way back in, its default
      `ObjectCreationHandling.Auto` does not skip a readable-but-not-writable member holding a
      non-null object — it POPULATES the instance already there, which is that same visit, appending
      its piece-lists to themselves. Every save-then-load with a stop open doubled them again, which
      is why it is towns and why it grows. Proved against Anton's own snapshots (Akkalat at four
      copies; Odokh, never open at a save, clean).
      THE FIX: `[JsonIgnore]` on every computed view over persisted state — `OpenVisit`,
      `OpenQuests`, `ResolvedQuests`, `JourneyVisit.HasAnyDoings` — plus `DropReplayedLines()`,
      called from `LoadFrom` BEFORE `MergeContinuedStays` (order is load-bearing: the merge SUMS by
      name, so healing after it would turn the flood into inflated counts, which looks plausible and
      is worse). The heal rides one invariant: every honest write goes through `AddCounted`, which
      merges by name, so a repeated NAME is proof of a replayed copy and the first carries the true
      count. Repeats are DROPPED, never summed — summing would invent goods that never changed hands.
      WHY THE SUITE NEVER CAUGHT IT: every existing round-trip test closes its stops before saving,
      so `OpenVisit` was null and the second copy was never written. The new test saves with a stop
      OPEN, four times over. THE GENERAL LESSON, written at the site: on a persisted type a computed
      view must be JsonIgnore'd — it is not merely dead weight in the file, because a view handing
      back a live reference makes the round trip MUTATE what it was only meant to describe.
      Existing corrupted journals heal themselves on the next load. 802 Core tests green.

- [x] SCOUTS COULD NOT SEE PRISONERS OR WOUNDED — FIXED 2026.08.16 (Anton asked his scout whether a
      band they were closing on held captives; she could not tell him something he could read off
      the game's own nameplate).
      It was never divination withheld: `FieldCraft` simply never looked. It read `TotalManCount`
      and nothing else about a band's people — a count that INCLUDES the wounded and says nothing
      of the prison train. Both `survey_surroundings` and `weigh_battle` now carry the hurt and the
      bound, with captive lords NAMED (the game's own tooltip names them, so it is a face and not a
      number). Counts are coarsened on the SAME Scouting bands the head-count already used (125 /
      50, no number at all below that), with one guard that matters: a count smaller than the
      rounding step is told true, because `RoundTo` floors at its own step and rounding three
      prisoners would print ten — inventing rather than coarsening.
      TWO THINGS TO KNOW IF THIS IS TOUCHED AGAIN: `wholeArmy` must always mirror whatever body of
      men the head-count beside it covers, or the two numbers argue with each other (that is why the
      army test is hoisted out of the Try in `WeighAgainstParty`); and BOTH TOOL DESCRIPTIONS were
      widened to say the eyes now cover this — a tool's contract lives in its schema and its
      description, and a capability nobody is told about is one nobody reaches for. UNPLAYTESTED.

- [x] A BAND LEADER IN A TOWN BELIEVED HE HAD NO MEN — FIXED 2026.08.16. Akadan the Widow-maker, a
      gang leader holding his own ground in Odokh, told the player "I've no men at my heel just now".
      NOT a missing tool: `recall_company` is not party-gated and did ride that reply (only the
      field-craft asks for `PartyBelongedTo`). The tool ANSWERED WRONGLY. A gang leader's command is
      his ALLEY and the knives who hold it — real men, mustered by the game's own alley model, who
      never become a `MobileParty` — and both `WorldRecall.DescribeCompany` and
      `SituationBuilder`'s own-command line read `PartyBelongedTo` and nothing else, so one told him
      he commanded nobody and the other left his sheet silent.
      Both now ask for the alleys first (`Hero.OwnedAlleys` + `AlleyModel.GetTroopsOfAIOwnedAlley`,
      counted from the game's ledger so the number does not jitter between replies); the sheet gets
      ONE sentence and the muster stays in the tool, as for any other captain. TWO GUARDS ARE
      LOAD-BEARING and are commented at both sites: the alley model reads `alley.Owner.Power`
      unchecked, so it may only ever be handed an alley the asker truly owns, and the PLAYER's own
      alleys belong to a different behaviour entirely, so the player is skipped outright.
      Beside it, `PersonaBuilder.TradeKnowledge` gained its missing `GangLeader` case — every other
      station carries one sentence of what its trade knows, which is the other reason his sheet had
      nothing to say about the streets he runs. UNPLAYTESTED.

- [x] THE HEARTH SHOWED 85% PLAINLY AND 85% WITH THE BEST GIFT — FIXED 2026.08.16, and it was not the
      display. Anton: "85% is very high for a babe, can you check the maths." He was right.
      THE SPREAD WAS ADDITIVE. `NightOdds` shared the month's chance out as `V x L x f /
      FertileWindowSum`, so the nightly chances SUMMED to `V x L` — which is the expected COUNT of
      conceptions, not the chance of at least one. The two agree only while `V x L` is small; it is
      3–4 here, so the crest was forced to 66% and the jewel's doubling produced **173%**, a number
      that is not a probability. `MaxNightlyChance` (0.85), whose comment calls it flavour, was in
      fact the load-bearing clamp holding the model inside probability space — hence both readings
      sitting on the rail and reading identical.
      NOW THE HAZARD IS SPREAD, not the probability: `H = -ln(1-V) x L`, `h = H x f / CurveSum(L) x
      gift x dial`, `p = 1 - e^-h`. Sum of h is H by construction, so the whole-cycle match with
      vanilla is EXACT rather than approximate, for every age, gift and cycle length — a test pins
      it to six places across four women and five ages.
      TWO MORE FELL OUT WITH IT. The normaliser was `FertileWindowSum` (4.65) where the curve truly
      sums to `MoodTides.CurveSum(28)` = 5.10, because the quiet days carry weight too — a further
      9.7% too hot; that is what `CurveSum` is for. And the guard test's upper bound was `1.0`, so
      it could only ever fail if the mod were too BARREN and was structurally blind to an overshoot.
      Now two-sided and exact.
      A childless wife of 25 reads ~47% at the crest and ~72% with the jewel, so the gift is felt
      again. WORTH KNOWING: her whole season still comes out ~96% over a month — that is VANILLA'S
      OWN number for a wife who travels with her husband, and matching it is the promise. The dial
      for a calmer house is `ConceptionChanceMultiplier`, which now cleanly scales the exponent
      (0.5 -> ~81% a cycle, 0.25 -> ~56%). Design record updated with the whole derivation.

- [x] COMPANIONS NOTICE THE GEAR YOU GIVE THEM — BUILT 2026.08.16 (Anton's ask: "tell them if I
      take off an item, if I add item… add the item values, cause they might not know it and its
      giving info on how vaiable it is"). Core `Gear\` (`GearChange`/`GearChangeSet`, `GearText` —
      the beat, the marker, the tally, all unit-tested) + Module
      `ImmersiveChatBehavior.Gear.cs` + `EnableGearNotes` (config + MCM, default on).
      THE HOOK IS A BRACKET, and that IS the design. There is no equipment-changed event on this
      version — the only three naming Equipment are smelting, a caravan sale, and a GATE. So the
      baseline is taken when `InventoryState` becomes the active state (frame tick, rising edge
      only) and the diff when the screen CLOSES, riding the `PlayerInventoryExchange` we already
      subscribe to — it fires unconditionally from `DoneLogic`, trade or none.
      WHY NOT A REMEMBERED BASELINE: the game rewrites equipment by itself at coming-of-age, at
      becoming a ruler, when OUR OWN courtship raises a companion to lord, and over every hero on
      every load (`CheckInvalidEquipmentsAndReplaceIfNeeded`). A persisted baseline would answer
      "he took my helmet" for half the roster after any of that. A bracket cannot see any of it.
      Cancelling the screen also rolls equipment back BEFORE the event, so a cancelled session
      diffs to nothing for free.
      AND THE BRACKET IS THE DEDUPE: trying a helmet on and off is start==end and silent; three
      swords through one slot is one line; reordering her weapons says nothing, because ARMS ARE A
      MULTISET. No timer, no cap, no per-day merge — that lever is deliberately held back.
      ANTON'S OWN DOUBT ABOUT WEAPON SLOTS WAS RIGHT and is answered by not asking the slot: armour
      slots each admit exactly one kind of thing so the slot IS the word ("my head", "my hands"),
      while the four weapon slots admit anything — so weapons are gathered under "for arms" and
      named by the ITEM, whose own name already says what it is.
      THE NUMBER IS `ItemValue` (worth, modifier included), NOT a trade price: a price swings with
      the town's stock and the player's haggling, and a woman wearing a sword does not experience
      that. Worth belongs to the person; prices belong to the market tool. Beside a great sum sits
      ONE yardstick, her own daily wage from `TroopWage` (never `GetCharacterWage`, which answers 1
      for heroes) — and nothing else: no judgment word, ever, guarded by its own test. She is handed
      the figure and feels about it herself.
      Deliberately NO situation block: she already recites her whole kit every reply, and a "lately
      given" paragraph would be the third telling of the same sword. UNPLAYTESTED.

NEXT UPDATE:
- [~] THE STAGE — all three entries BUILT 2026.08.16, none of them played.
      Everything below is the SAME FILE, `ConversationSceneBuilder`, and doing them apart means
      solving the scene question three times. The design record's own section is
      docs/after-the-wedding-design.md → "The hearth window becomes a stage"; READ THE WARNING
      UNDER IT — that section is a CONCEPT and not a build-ready design, and the three questions
      it does not answer are listed there.
    - [x] THE HEARTH IS A STAGE — BUILT 2026.08.16. H raises the ONE screen turned to the hearth:
          the women of it listed left, the chosen one ALIVE in the middle in her own place, her page
          on the right. A button in the bar ("The hearth" / "Talk") turns it either way.
          THE UNSOLVED QUESTION WAS NEVER ANSWERED — IT WAS DISSOLVED. Two tableau-hosting screens
          over one shared cached scene is where the hard native crash lives; a MODE of the existing
          screen never creates that situation. Same layer, same host, same stub, same teardown, and
          the four documented traps stay paid for exactly once by code that already works. The
          design record's own banner suspected this shape; it is the right one.
          WHAT IS ON THE RIGHT: everything the old window carried, nothing dropped — her season, her
          state, the odds, the two switches, the rolling fortnight with the children's cards among
          them, and the Go button. It is `NightWindowVM` ITSELF, hung as a nested `{Hearth}` data
          source rather than reimplemented: every one of those readings was already right and
          already playtested, and copying them would have meant maintaining two answers to one
          question. Its own contact list simply goes unbound. Tabs were considered and refused — the
          hearth is the one page written for the player as an OPERATOR, and an operator's page must
          not hide half its state behind a click.
          THREE THINGS A READER SHOULD KNOW: the circle is FILTERED, not gathered afresh (same souls,
          same stage); the mode is set BEFORE the first selection so the soul chosen is one of the
          women; and `ExecuteGo` now closes the talk screen too, because that screen HOLDS THE WORLD
          STILL and a night that cannot let an hour pass is a night that never settles.
          The old window is whole behind `UseClassicChatWindow` and the session fallback.
          FIRST PLAYTEST, 2026.08.16 (Anton): she was DRAWN correctly and the Talk/Hearth button
          worked, but her whole page was blank — no season, no switches, no fortnight, no Go.
          TWO CAUSES, both now fixed, and the first is the one worth remembering:
          • A NESTED DataSource IS BOUND WHEN THE WIDGET IS CREATED. `Hearth` was built lazily on
            the first turn to the hearth, so at movie-load it was null, the panel bound to nothing,
            and no later notification ever brought it back. It is now built in the VM's constructor,
            before the movie loads — the same rule the face already follows two lines above it
            ("choosing here happens BEFORE the movie loads").
          • Her page's own contact list is unbound but is still what `TrySelect` matches against, so
            it must be refreshed before selecting or the page selects nobody and every reading is
            blank.
          The layout was rebuilt as ONE SCROLLING COLUMN while I was there: her season, state and
          odds can each wrap to two lines, and anything pinned under them at a fixed offset gets sat
          on — the letter window's old bug in a new coat. Only the Go button is pinned, because a
          decision should not have to be scrolled to. The prefab is now XML-validated before deploy.
          SECOND PLAYTEST, same day: STILL blank, and the real cause was neither of those.
          **A WIDGET THAT DECLARES A DataSource RESOLVES EVERY BINDING ON ITSELF AGAINST THAT NEW
          SOURCE — ITS OWN `IsVisible` INCLUDED.** The panel carried `DataSource="{Hearth}"` and
          `IsVisible="@IsHearth"` on the SAME widget, so the game looked for `IsHearth` on the night
          window's view model, found nothing, and never showed it. The thread hid correctly the whole
          time precisely because it has no DataSource of its own. Split in two now: the OUTER widget
          owns the visibility in the screen's own scope, the INNER one owns the data. Write it down —
          the same trap is waiting for every future nested panel in this prefab.
          Worth noting what was NOT the cause, since both were plausible and both cost a round: the
          lazy construction (fixed anyway, and correctly) and the layout margins (rebuilt anyway, and
          better for it). No exception was ever logged, which was the clue that pointed here: nothing
          was failing, something simply was not being asked for.
          THIRD PLAYTEST: it draws, and Anton asked for four changes, three of them done:
          • THE SWITCHES ARE THE HOUSE'S, NOT HERS — visiting and prevent-a-child govern the whole
            household, so they moved to the LEFT column above the list of souls, where the settings
            of the house belong. The list's top offset is MEASURED from the wrapped sentences
            (`ContactsTopMargin` over the old window's own `ControlsHeight`), because how far each
            wraps depends on which way its own switch stands.
          • THE ROLL OPENS ON THE NEWEST NIGHT, scrolling up for earlier ones — the same manner the
            thread already had, now shared (`ScrollToEnd` takes the scroller's id).
          • HER STATE MOVED UP under her name and out of the roll, and the parenthetical beside her
            name is now the BOND — "(wife)" / "(lover)" — where the voice badge had been reading as
            a stutter, "Sibylla (Sibylla)". The badge still stands on the talk side, where it means
            something.
          NOT CHANGED, and stated rather than guessed: "Go to her tonight" still closes the screen
          before the gift question. It has to — the screen HOLDS THE WORLD STILL, and a night that
          cannot let an hour pass is a night that never settles. Whether it should REOPEN on her
          afterwards is Anton's call and was left for him.
          FOURTH PLAYTEST: the page reads right. Two more, both fixed:
          • THE SET TOGGLED ITS LABEL AND SHE NEVER LEFT THE STREET. `MoveTo` rebuilt the tableau
            and the controller held the new one, but the widget draws whatever it was last HANDED —
            so the screen had to be told (`OnPropertyChanged("TableauData")` / `"HasFace"`, the same
            pair `RefreshContacts` already raises). Only the label was being refreshed.
          • THE TALK-SIDE INFO LINES OVERLAPPED HER STATE. The reach line, the bond line and the
            memory weight are the messages side's business — how to reach her, how the bond runs,
            what her memory costs — and on her page they said nothing anyone came for while sitting
            straight under her own state. Hidden whole in hearth mode (Anton: "I dont need that info
            string here... it will leave the child info ok").
          FIFTH: the set button started on the WRONG room. Both the label and the cycle assumed
          the list's first entry, so meeting a wanderer in a tavern the button said "In the town"
          over a woman standing in the tavern, and the first press "moved" her to where she already
          was and looked broken. Both now read `RoomShownFor` — whatever was moved to, or failing
          that the room she is truly in — so the label is honest from the first frame and the first
          press always takes her somewhere new. A room we do not offer (a prison, a barred keep)
          names none rather than lying about which one it is.
          SIXTH, both polish (Anton, 2026.08.16):
          • THE BAR RAN OFF ITS OWN PANE. It lived inside the right-hand pane, right-aligned and
            grown by its children, so each button added pushed the row further past that pane's left
            edge until the last of them sat loose on the scene. Lifted to the ROOT with a strip of
            its own running the whole width beside the list of souls — which is what it had looked
            like it wanted for some time. It is still the LAST root child: later siblings win the
            mouse in Gauntlet, and the panes are stretched to fill, so anything before them is a
            pane of glass over dead buttons (the 2026.08.14 lesson, unchanged and still true one
            level up).
          • THE DRAFT MIRROR SAT ON THE LAST SPOKEN LINE. `MessagesBottomMargin` grew by three lines
            once there is a draft (210 -> 280, and 280 -> 350 for a soul away), since the mirror of
            the whole message is taller than the box that raises it.
          STILL UNPLAYTESTED IN THIS SHAPE.
    - [x] A WANDERER IN A TAVERN IS DRAWN IN THE TOWN — FIXED 2026.08.16. `ConversationSceneBuilder`
          now passes the room the soul is really in (`LocationComplex.GetLocationOfCharacter(hero)`),
          where it had been passing null. NOTE the tableau wants the room's own StringId as a
          STRING, not a Location — the seventh argument of `MapConversationTableauData.CreateFrom`.
          The old comment argued null was safer because the hall sets are culture-bound; that was
          the right worry in the wrong place, since `SceneSettlementFor` already answers null for a
          culture whose sets we do not ship, so a non-null settlement IS the proof its interiors
          exist. UNPLAYTESTED — worth a look in a tavern, a keep, and a War Sails town (which should
          still be drawn out of doors).
    - [x] CHANGE THE SET INSIDE A TOWN — BUILT 2026.08.16. A button in the talk screen's bar,
          "In the town" / "In the tavern" / "In the keep", which CYCLES rather than opening a menu:
          three rooms at most, and a menu for three is heavier than the choice it carries. Shown
          only inside walls and only when there IS more than one room.
          `ConversationSceneBuilder.SetsFor` offers them and `BuildFor` takes an optional set;
          `ConversationTableauController` keeps `_chosenSet` beside a `_builtSet` so a set change
          redraws while a re-selection of the same soul still costs nothing, and every path that
          empties the stage clears both (a stale `_builtSet` would make the next `Show` of that soul
          short-circuit into drawing nothing).
          THREE DECISIONS WORTH KEEPING: the keep is offered only when the game's OWN access model
          says the player may walk in, bribe included — offering a barred door would be the mod
          promising what the world refuses, and unlike the talk-range test this one fails CLOSED;
          the default is always where the soul truly stands, and choosing a different contact drops
          the move, because the room belongs to this conversation and not to the player's standing
          taste; and the label only changes when the stage actually rebuilt, so it can never claim a
          room the player is not looking at.
          The original ask closed "and note who else is standing there"; Anton WITHDREW that on
          2026.08.16 ("no, I dont care if they see who else is in the tavern") — neither other souls
          drawn into the scene nor a line naming them. Do not reinstate it as a missing requirement.
          UNPLAYTESTED, and this one genuinely needs eyes: it asks the tableau for a room the soul
          is NOT in, which is a thing vanilla never does.
      DO THEM TOGETHER, and in that order: the two smaller ones teach the scene selection that the
      stage then has to arbitrate between two screens.

- [ ] TRIAGE RESULTS ARE ON DISK AND UNAPPLIED (2026.08.16). A triage run over the twenty untriaged
      leads finished its READING phase and then died on a session limit part-way through verifying.
      **Ten of its readers returned in full and their verdicts are saved in the repo**:
        docs/workflow-results-2026-08-16.json       — the structured verdicts (verdict + evidence +
                                                      a named fix per finding, file:line each)
        docs/workflow-results-2026-08-16.full.txt   — the same run's raw output, for anything the
                                                      JSON truncated
      SIX AREAS were read whole: births/recognition, doors + duty nights, heart-bands + the lover
      road, the two windows' "Between us", leaks + the wound spike, and the dusk flow. FOUR planners
      returned too (the voice buzz, the acted parts, speak-when-closed, and — dead, the night clock,
      which was built by hand instead).
      **THE VERDICTS ARE ONE READER'S WORD AND NOTHING MORE.** The refuters — whose whole job is
      killing false positives — are the half that died, exactly as in the run before it. Read the
      code at each cited line before believing any of it.
      **THE THREE BIRTHS FINDINGS ARE DONE** — triaged by hand, all three real, FIXED and
      deployed 2026.08.16. Work order + reasoning: docs/birth-recognition-fix-plan.md; the account
      of what was built is at the end of TASKS_DONE.md.
      The REST of the twenty verdicts remain untriaged and unapplied. What is left in this file
      is one reader's word on the other areas — read the code at each cited line before believing
      any of it.

- [ ] REVIEW FINDINGS STILL OPEN (adversarial pass over the after-the-wedding batch, 2026.08.15).
      ALL 35 RAW FINDINGS ARE IN docs/review-findings-2026-08-15.md — recovered from the run's
      journal, with file:line and a scenario each, grouped high/medium/low. They are UNVERIFIED:
      the verify phase (24 refuters whose job is killing false positives) died on a session limit,
      so the run's "0 confirmed" means nothing. Ten were checked by hand and fixed the same day;
      the five below were judged plausible; the remaining twenty have not been triaged at all.
      A finding there is not a bug until someone reads the code.
    - [x] `weigh_what_stands` REVISE cannot work as wired — FIXED 2026.08.16. The tool gained its
          own `reworded` field (falling back to `note`, where the misgivings' older tool keeps it),
          the resolver passes the two strings separately and runs the same narrow swap guard a
          settle does, and Core's `Revise` now REFUSES a revise that would change nothing rather
          than reporting a rewording it did not do.
    - [x] The fresh-wound spike was read only in `CoLocatedPull` — FIXED 2026.08.16. The letter roll
          applies the same floor over the damped pull, and reads the wound BEFORE the story-depth
          gate so a woman wed through vanilla and never spoken with can still write. The same
          floor was missing from `CoLocatedPull`'s own richness-0 early return (raw finding at
          docs/review-findings-2026-08-15.md:267, which is the same bug wearing the other coat) —
          fixed with it. The spike is still spent exactly once: the letter ponder marks
          `Considered` and the write marks `Reached`, and both clear the stamp.
    - [ ] The duty-night spiral stops biting at `DoorReasons.MaxStandingOpen` (5). After about five
          duty nights nothing further is laid down. Commented as deliberate; the guardrail says
          "if playtests show it farmable, deepen the closure" — so this is a tuning question to
          settle with real play, not a bug yet.
    - [x] The classic chat window had no `RoadActionText`/`HasRoadAction` binding — CONFIRMED REAL
          and FIXED 2026.08.16. The page named the deed ("You have never owned X before the world…")
          and offered no way on earth to do it, on a path that is both supported
          (`UseClassicChatWindow`) and automatic (the talk screen's own session fallback). The window
          now mirrors the screen's `RoadActionText` / `HasRoadAction` / `ExecuteRoadAction` and its
          prefab carries the button, inside the page as it is there, with the list's bottom margin
          making room for it so nothing runs underneath.
    - [ ] A feast/owning offer that lapses past its 30-day window leaves `Owned = NeverArose`, which
          reads as owned. That is the SAFE default and probably right — but it means silence can
          become accidental recognition, and it should be a decision rather than an accident.

- [ ] FOUR ASKS FROM THE 2026.08.15 PLAYTEST (Anton, while the batch was building):
    - [x] THE NIGHT'S CLOCK, BY THE SUN — BUILT 2026.08.16. Core `Nights\NightClock` (CycleOf /
          SameCycle / HoursUntilReset, unit-tested) + `NightLedger.IsCycleSettled`. A night now
          belongs to a CYCLE — late afternoon to late afternoon — and every "have we already had
          tonight?" in the nights flow counts in those instead of calendar days or hours-since:
          `CooldownHoursLeft`, `_nightAskedOnDay`, `_nightNoticeDay`/`NightNoticeUp`,
          `IsNightNoticeStillAlive` and `CloseUnansweredNight`. Config `NightDayResetHour` (16,
          MCM slider "Hour the house is ready again", live); `NightCooldownHours` is RETIRED — the
          property stands so an existing config.json neither breaks nor loses a familiar line, and
          nothing reads it.
          BOTH HARD-WON COMMENTS SURVIVE UNTOUCHED, which was the whole constraint: the evening
          still asks LATE AND NOT BEFORE (`IsWithinEveningWindow` is unchanged and still gates
          `HandleTheEvening`, so the automatic night waits for dusk and the day between stays the
          player's), and it still asks ACROSS A WINDOW OF HOURS with the stamp going down only when
          something is truly put to the player. The reset hour governs READINESS only; the evening
          hour governs WHEN IT IS ASKED. That separation IS Anton's ask.
          THE BUG IT KILLS: the flat cooldown drifted — a night at 23:30 put the next out of reach
          until 23:30, which is after the evening's question has been and gone, so a house that went
          to bed a little later each night walked its own clock out of the day. A test pins it. The
          small hours are the other half: a night at 01:00 used to settle the day just beginning and
          cost the whole of the following evening; it now belongs to the evening it grew out of.
          NOTE `NightHour` was deliberately LEFT at 21 rather than moved to Anton's "~22h" — 21 is
          already late evening, it is a shipped default, and defaults are never migrated. It is one
          MCM slider away for anyone who wants 22. UNPLAYTESTED.
    - [ ] CHANGE THE SET INSIDE A TOWN → moved into **THE STAGE** above, with the tavern bug and
          the hearth rebuild. Same file; do not do it alone.
    - [x] LET THE VOICES READ THE *ACTED* PARTS — BUILT 2026.08.16. `VoiceSpeakActedParts` (config
          + MCM "Read the acted parts aloud too", live, default ON) picks between
          `SpeakableText.SpokenOnly` and `SpokenWithGestures` in `VoiceService.PlanFor`, and
          `BitesFor` takes the same flag. THREE THINGS WORTH KNOWING: the asterisks never reach the
          engine (EmoteText hands back the gesture's CONTENT, so it was already safe); a gesture
          ending on a word is now CLOSED with a stop, or it runs into the sentence after it and the
          reading has nowhere to breathe; and the cache needed NOTHING — `VoiceCacheKey` is built
          from the spoken text itself, so the two settings key differently for free. A reply that
          answers only with *turns away without a word* is now speakable where it used to be silent.
          UNPLAYTESTED: nobody has heard a gesture read aloud yet, and whether it lands as narration
          or as a robot reading stage directions is exactly the kind of thing only listening answers.
    - [x] SPEAK HER ANSWER WHEN THE CHAT IS CLOSED — BUILT 2026.08.16. `VoiceSpeakWhenClosed`
          (config + MCM "Speak answers you are not watching", live, default ON) and one shared rule,
          `ImmersiveChatBehavior.ShouldSpeakNow(viewing)`, now read by BOTH gated sites — the quick-
          chat reply and the opening greeting. It RIDES `VoiceAutoSpeak`: with that off nothing
          anywhere speaks unasked, which is what that switch promises. The ready-ping still fires
          when she is not being watched whether or not it also speaks — the notice is the way BACK
          to the thread, and hearing her is no substitute for finding her. The prewarm above the
          call already made the sound for a local voice, so a closed-window answer speaks at once.
          NOTE this deliberately reverses a written rule ("a voice from a conversation they walked
          away from is a ghost in the room"); the asking happened, so it is a switch of its own
          rather than an edit of the old one. TWO KNOWN COSTS, both stated in the hint: one voice at
          a time, so two answers landing together means the second cuts off the first; and a HOSTED
          voice is billed without anyone pressing anything. UNPLAYTESTED.

- [ ] THE ONE SCREEN REWORK (2026.08.14, Anton's big batch — THE PLAN LIVES IN docs/one-screen-plan.md,
      read it first; research in docs/talk-screen-research.md + docs/prompt-text-inventory.md)
    - [x] Phase 1 — THE SCREEN: built + deployed 2026.08.14, UNPLAYTESTED
    - [x] Phase 2 — THE SCROLLBACK PROMPT: built + deployed 2026.08.14, UNPLAYTESTED
    - [ ] PLAYTESTS are underway

- [ ] LIFE AFTER THE WEDDING (designed with Anton 2026.08.15 — concept LOCKED, BUILD UNDERWAY;
      THE PLAN LIVES IN docs/after-the-wedding-design.md, read its spirit section FIRST and whole.
      Each item's own entry in that doc records what was built and why, as it lands.)
    - [x] 1. Special night intent — built 2026.08.15, UNPLAYTESTED
    - [x] 2. The lover road — built 2026.08.15, UNPLAYTESTED (Core bands + fork + text, the two
          hands, the buyout, the father's invitation, outside-the-limit joining, ex-lover, dev
          levers in both windows). NOTE it moved one existing wall: a married player's courtships
          may now walk the trunk (feelings) and are barred only from readiness onward, gated on
          EnableLoversRoad — without that the whole batch was unreachable.
    - [x] 3. Doors with reasons — built 2026.08.15, UNPLAYTESTED
    - [x] 4. Leaks and the morning after — built 2026.08.15, UNPLAYTESTED
    - [x] 5. Duty nights and the spiral — built 2026.08.15, UNPLAYTESTED
    - [x] 6. Recognition + the child's own story + the house lines — built 2026.08.15, UNPLAYTESTED
          (recall_house itself deferred; the family lines carry the public state)
    - [x] 7. Heart-bands + the era norm + spark muse cards — built 2026.08.15, UNPLAYTESTED
    - [x] 8. "Between us" — one permanent door, composed page — built 2026.08.15, UNPLAYTESTED
    - [x] 9a. Small UI debts — built 2026.08.15. The empty purple notice circle now wears her face
          (Anton's screenshot: the evening's notice was the only one of the three carrying no Hero
          at all, so the portrait widget had nothing to bind to); wife pinned top of the contact
          list with lovers under her; the settlement menus teach their hotkeys and carry a second
          door to the hearth.
    - [ ] 9b. THE HEARTH-AS-TABLEAU-STAGE → moved OUT to **THE STAGE** at the top of NEXT UPDATE,
          because it is one job with the tavern bug and "change the set", and because buried as the
          last line of a batch whose other eight items are [x] nobody would ever find it.

      WHAT IS UNPLAYTESTED, which is nearly all of it: every one of items 1–8 shipped without a
      single minute in the game. docs/next-session-handoff.md ranks what to check by what hurts
      most if it is wrong — the world-mutating lover buyout and a lover's child first, then whether
      she reaches for the door's hand at all, then whether the era norm makes every woman sound the
      same (that last one is the founding rule of the mod and only real play can answer it).
      The post-marriage batch: temptation that comes to you, the lover road (buyout from her clan,
      the father's letter, outside the companion limit), doors-with-reasons (misgivings applied to
      the night — SUPERSEDES the nights doc's no-refusals rule, the asking happened), leaks → the
      morning after, duty nights + the spiral ("Go to her anyway" — no narrator, code-enforced),
      the era norm as world-law + spark muse cards, recognition of a lover's children (blood is
      the game's, honor is ours) + the child who knows its own story, derived heart-bands (no
      third number), "Between us" one-door page, hearth window as a tableau stage, Marry Anyone
      recognized-then-retired. Build order + guardrails + open technical questions are in the doc.

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

      ALSO WORTH A LOOK NOW (2026.08.15): everyone you have not cast is given a voice of their own
      culture and sex, so talk to a few Battanians and a few Aserai and see whether the shelf is
      spread the way you expect. The Voices page says where each voice sits (female/battania/Gwen)
      and lists them grouped that way. It only bites once module\Voices\<gender>\<culture>\ has
      voices in it — until then everyone still falls back to Sibylla/Maximus. NOTE the two of them
      moved to female\other\ and male\other\, which is what "belongs to no people" looks like.
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
