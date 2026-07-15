# Party commands by word and by letter — research (2026.07.15)

The feature: the player asks a party leader of their own clan — face to face, in the chat
window, or **by letter** — to patrol a town, follow them, garrison somewhere, or come home;
the NPC, being a person, may comply, negotiate, or refuse, and when they DO take the order
their hand on the map is a native tool call. Orders by letter take effect when the letter
*arrives* — commands that travel at the speed of a courier.

Researched in depth before building (Anton's ask). Everything below is verified against the
installed game, **v1.4.7** (War Sails), with `ilspycmd` on the real DLLs — not from docs of
older versions.

## Prior art studied

- **Finer Party Controls (FPC)** — [Steam Workshop 3685593525](https://steamcommunity.com/sharedfiles/filedetails/?id=3685593525).
  Clan-screen command panel: escort player / escort friendly party / wait at settlement /
  patrol around settlement / raid settlement / hold position, plus deep recruitment
  (composition targets, tier/culture whitelists, per-party size caps) and permissions
  (may join AI armies, may sail, may donate troops). Closed source. Depends on **Thinks**
  ([a shared "stable AI movement" infrastructure mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3117962790&searchtext=) ecosystem)
  — the telling part: the community needed a whole framework mod because naïve
  `Ai.SetMoveX()` calls get overridden by the party's own hourly thinking. FPC is the
  feature ceiling to know about, not the model to copy: its recruitment/composition half is
  spreadsheet territory, not conversation.

- **Party AI Overhaul and Commands** — [GitLab source](https://gitlab.com/octaviusmods/bannerlord-party-ai-overhaul-and-commands),
  the genre's grandfather (e1.2 era). Older technique: Harmony patches on the short-term AI
  + dialog-driven orders. Cloned to `..\reference\PartyAIOverhaul-gitlab` for the record;
  superseded by the next one.

- **Bannerlord.PartyAI (Party AI Controls)** — [GitHub, MIT](https://github.com/adwitkow/Bannerlord.PartyAI),
  **supports exactly v1.4.0–v1.4.7** (our installed version) and is naval-aware. THE
  reference implementation. Cloned to `..\reference\Bannerlord.PartyAI` — consult freely;
  MIT means we may even adapt with attribution, though our surface is small enough to write
  clean. Orders: patrol around settlement, patrol clan lands, escort party, stay in / visit
  settlement, besiege, defend, attack party, plus recruitment templates. UI is clan-screen
  (UIExtenderEx mixins) — all of which we replace with conversation.

## The verified technique (the whole trick)

**Do not fight the AI with `SetMoveX` + `DoNotMakeNewDecisions` — steer its own mind.**
Every hour each AI party "thinks": vanilla behaviors propose `(AIBehaviorData, float score)`
pairs into a `PartyThinkParams`, highest score wins. `CampaignEvents.AiHourlyTickEvent`
(`IMbEvent<MobileParty, PartyThinkParams>`, public, verified v1.4.7) hands every mod a seat
at that table. Inject the ordered behavior with a dominant score and the party *wants* to
obey — no Harmony, no per-tick re-asserting, no fighting `DefaultBehavior`.

- `PartyThinkParams` (v1.4.7): `AIBehaviorScores` (`MBReadOnlyList<(AIBehaviorData, float)>`),
  `TryGetBehaviorScore(in AIBehaviorData, out float)`, `SetBehaviorScore(in AIBehaviorData, float)`,
  `AddBehaviorScore(in (AIBehaviorData, float))`, `DoNotChangeBehavior`, `WillGatherAnArmy`.
- `AIBehaviorData` (struct, v1.4.7): ctor `(IMapPoint party, AiBehavior, MobileParty.NavigationType,
  bool willGatherArmy, bool isFromPort, bool isTargetingPort)` — or a `CampaignVec2 position`
  overload. Target settlements/parties are `IMapPoint`.
- `AiBehavior` enum lives in **`TaleWorlds.CampaignSystem.Party`** (not the root namespace):
  `Hold, None, GoToSettlement, AssaultSettlement, RaidSettlement, BesiegeSettlement,
  EngageParty, JoinParty, GoAroundParty, GoToPoint, FleeToPoint, FleeToGate, FleeToParty,
  PatrolAroundPoint, EscortParty, DefendSettlement, DoOperation, MoveToNearestLandOrPort`.
- **Score 15f wins.** The reference mod's `Constants.BehaviorScore = 15f`, proven on 1.4.7
  (vanilla scores run lower).
- **Naval routing comes free**: `Helpers.AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party,
  settlement, isTargetingPort, out NavigationType, out distance, out isFromPort)` and
  `...OfMobilePartyForMobileParty(...)` (both verified) pick land/sea routes; a result of
  `MobileParty.NavigationType.None` = genuinely unreachable → the NPC refuses in honest words
  instead of pathing into a wall. War Sails ports/sea handled by the game itself.
- Small garnishes from the reference mod: escort adds `party.Ai.SetInitiative(0f, 0.33f, 2f)`
  (don't peel off to chase fights); patrol sets `party.SetPartyObjective(PartyObjective.Defensive)`
  and caches the previous objective to restore on release.
- The reference mod's ONLY production Harmony patch is a postfix on
  `AiMilitaryBehavior.AiHourlyTick` zeroing Raid/Besiege scores for "may not raid" permissions —
  we don't need it for v1 (our verbs don't include raid), noted for a possible v2 permissions pass.

## Order lifecycle (mirror the reference mod's edge-case checklist)

An order is standing state; it must **clear itself honestly** when the world moves on:
- `CampaignEvents.OnPartyJoinedArmyEvent` → order cleared, message "no longer patrolling —
  called to the army" (armies own their members; also refuse NEW orders while in an army:
  "I ride under the banner; I am not free to turn aside").
- `CampaignEvents.HeroPrisonerTaken` → leader captured, order dies with a message.
- `CampaignEvents.MobilePartyDestroyed` → clear when the ordered party dies AND when the
  *target* of someone's escort order dies.
- Each hourly assert re-checks: target still exists, target faction not now at war with us
  (a patrol around a town that fell to the enemy lapses with a message), still reachable
  (`NavigationType.None` → lapse). Dead/disabled leaders swept daily.
- Every set/change/lapse fires a **colored `InformationManager.DisplayMessage`** — which is
  exactly Anton's notification requirement: it shows as the fading left-side line AND lands
  permanently in the event/battle log (the log key). One channel, both asks.

## Our shape (Immersive AI, v1)

The differentiator: FPC gives you a control panel; we give you a *person*. Nothing enforces
obedience — the tool is the NPC's own hand, and a companion may negotiate ("the men are three
days hungry; let me victual at Ortysia first") or refuse. Orders arrive through words, and
through **letters** — the compose/reply paths already ride `CompleteSpokenAsync` with tools
aboard, and a player's letter is read (and answered) on arrival day, so a mailed order takes
effect when the courier does. Zero extra plumbing for the flagship trick.

- **Gate**: tool rides only when the NPC leads a `MobileParty` of the **player's own clan**
  (`PartyBelongedTo != null` + `LeaderHero == npc` + `ActualClan == Clan.PlayerClan` — same
  family as the FieldCraft gate). Not other lords (v3 kingdom-politics territory), not
  caravans (their AI is their livelihood; FPC manages them, we deliberately don't in v1),
  refuse while in an army.
- **One tool, `set_party_course`** (`Tools\PartyCommandTool.cs` beside HeartTool/GoalTool),
  verbs: `patrol` (settlement), `escort_player`, `go_to` (settlement), `hold`, `resume`
  (release back to their own judgment — the always-available escape hatch). Settlement names
  resolved with the same closeness-forgiveness as `recall_place`. Persona whisper
  (`NpcPersona.CanSetPartyCourse`): "I lead a warband of my lord's clan; if it is asked of
  me I may set my company's course."
- **Deliberately cut from v1**: raid/rampage (war/criminal consequences — the easiest way a
  hallucinated call wrecks a campaign; v2 behind its own toggle if ever), besiege/defend
  (v2 candidates, the enum is ready), disband (destructive; the clan screen does it
  deliberately), attack party, recruitment/composition (FPC's spreadsheet half — likely never;
  not conversation).
- **Persistence**: partyId/heroId → order record (verb + target stringId + issued day)
  serialized as plain strings in `ImmersiveChatBehavior.SyncData` — primitives only, **no new
  saveable classes, no save-definer risk**. Order model + (de)serialization + verb parsing in
  Core (unit-tested); the `AiHourlyTickEvent` listener + clear-event listeners are a small
  Module behavior.
- **Notifications** (Anton's explicit ask, satisfied by the lifecycle above): colored
  DisplayMessage on set ("⚔ Aldric turns his company to patrol around Marunath."), on lapse
  (magenta, with the reason), plus the usual soft activity notice while the tool call is in
  flight ("Aldric weighs your order…", `ShowNpcActivity`). Optional: current standing order
  shown in the chat window's bond-stats line ("under orders: patrolling Marunath").
- **Config**: `EnablePartyCommands` (default on). The NPC's memory records the exchange like
  any other turn — the order lives in her story, not just in a table.
- **Compatibility caveat** to note on the Steam page when shipped: players running FPC /
  Party AI Controls have two masters injecting scores for the same parties; highest score
  wins per hour. Not corrupting, but confusing — recommend picking one commander per party.

## Open design questions (settle with Anton at build time)

1. Refusal calibration — does low relation make a companion drag their feet, or is the LLM's
   own judgment (fed the relation via the sheet) enough? (Lean: let the person decide; no
   mechanical gate.)
2. Does `resume` restore the cached `PartyObjective`, and should a lapsed order notify the
   NPC herself (an Angel aside next talk: "your company was called to the army")? (Lean: yes,
   it's one recorded line and keeps her story truthful.)
3. Show standing orders anywhere player-facing beyond messages — clan screen is FPC's turf;
   our bond-stats line is probably enough.

Related TODO sibling: "Actions for the NPCs" (the wider act-tool family mined from ChatAi —
travel, join party, gifts, sparring). Party commands is that family's first and best-scoped
member; build it first, let it set the pattern.
