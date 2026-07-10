# Plan: Spending time together — dates, trysts, and the shared grammar of gestures

*Drafted 2026.07.10 from a design chat between Anton and Claude (task: "Deutheuda dreams of a
date"). Status: Anton is mulling it over; nothing here is committed work yet.*

## The core insight

A date is a **promise, and promises are only real if they can be broken.** Everything that works
in this mod — letters, reach-outs, meeting notes — works because it has stakes and stamps. The
date mechanic should be the same shape: not a cutscene, but a commitment that lives in game time —
made in conversation, kept or missed on the map, remembered either way. This is the first feature
about *time spent together* rather than words exchanged.

## Phase 1 — the evening together (smallest slice, ~one session)

A "Spend the evening with someone" option beside "Speak with those near you" in settlement menus —
and possibly offered by *her*, through the existing reach-out channel ("come walk with me").

- **Costs something real:** ~3 game hours pass, a few denars for the tavern table. Cost is what
  separates a date from a chat.
- **A date situation:** `SituationBuilder` gains an *occasion* — no business, no war council; the
  two of you walking the ramparts at dusk, sharing a meal. The Angel narrates setting shifts
  between exchanges ("the lamps are lit; the common room empties around you") — the existing
  Angel-beat machinery doing exactly what it was born for.
- **Closes as a chapter:** the usual private feeling call at the end, asked about the whole
  evening rather than one line. It lands in memory as a lived thing: "the evening we walked
  Epicrotea's walls."

Rides wholesale on: the chat loop, recorded Angel turns, the feeling call, settlement menus.
Mostly prompt-craft plus a menu option and an hours/denars cost.

## Phase 2 — the tryst (the promise itself)

She says "meet me in Epicrotea when the week turns" — and it **binds**.

- **Made by her own act:** a native tool, `propose_meeting(place, when)` — her words and her act
  are one gesture, same channel as the recalls. (This quietly becomes the first stone of the
  "NPCs that ACT" task — the act-tool family — so its design should get a nod from that task.)
- **Persisted like letters:** an appointment must survive save/load — the `LetterBag` pattern
  (per-campaign JSON, atomic writes).
- **Kept:** the player is in the right settlement in the window → faced notice ("Deutheuda waits
  by the fountain") → the Phase-1 evening begins.
- **Missed:** the Angel tells her, honestly, that she waited and the player did not come.
  Recorded beat; her feeling call decides what it does to her heart. *That* is what makes showing
  up mean something.

## Phase 3 — gifts and gestures (later)

Bring her something from your inventory (`offer_gift`), pay for the meal, choose the setting.
Rides whatever act-tool family the "NPCs that ACT" task settles on. Not designed yet.

## The `*kissing you*` question — a shared grammar of gestures

The NPCs invented emotes on their own; formalize rather than fight it:

- **Render it:** in the chat window, `*asterisk*` spans draw as soft gray narration, like Angel
  beats — actions look like actions, words like words. Cheap and immediate.
- **Reciprocate it:** one whisper line telling her the convention cuts both ways — the player may
  write `*offers his arm*` too, and she should read it as done, not said. A shared physical
  grammar for the roleplay.
- **Keep it warm, not cheap:** the whisper should say gestures carry weight in proportion to what
  the heart has earned — an embrace at standing 80 and at standing 10 are different acts (she
  already knows where the heart stands from the situation). `RoleplayGuidance` stays the
  world-tone dial in Anton's hands.

## Recommended order

**Phase 1 + emote rendering together** as one coherent "make the roleplay real" drop; Phase 2
right after (its tool wants the act-channel design nod anyway); Phase 3 with the acts task.

## Open questions for Anton

- Start with the evening (Phase 1), or is the promise-and-consequence (Phase 2) the heart of it?
- Should dates feed the game's real courtship/romance progression, or stay purely ours for now?
- Who may be asked on a date — anyone, or only where the heart already leans?
- Costs: how much time and coin feels right for an evening?
