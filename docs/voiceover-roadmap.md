# Voiceover: what was left, and what is left now

Written 2026.08.14 after the first working playtest; **worked through in one pass on the night of
2026.08.15**. Everything in Waves 1–5 below is built, and what could be proved without the game
running was proved against the real engine and the real hosted API. **None of it has been played
yet.**

Background reading before touching any of it: `voiceover-engine-notes.md` (the measurements and the
traps — considerably richer now) and the memory notes `voice-pipeline-lessons` and
`voice-shipping-constraint`.

---

## Done, and how far each was proved

| | What landed | Proof |
|---|---|---|
| **1.1** | A ▶ on every thread row — replies, letters, inner-mind beats, wedding/birth/night accounts | built; unplayed |
| **1.2** | The panic stop, all three layers: **Backspace** anywhere, a Stop button while speaking, and a per-line audio ceiling | ceiling **proved live** |
| **1.3** | "Bring over from Studio" in the voice panel — imports what is not already on the shelf, by name | built; the shelf import itself was run for real |
| **1.4** | Streaming without the seam: pieces poured into one file, the next sound built early, handover on the clock | **measured**; unheard |
| **1.5** | All five Sibyllas + Achilles 2 imported; **Sibylla 5 is the default female voice** | done on disk |
| **2.1** | Reach-outs can speak as they arrive (off by default); letters speak from their ▶ only | built; unplayed |
| **2.2** | The player's own voice, off by default, settable in the panel and in MCM | built; unplayed |
| **3.1** | Male/female/player defaults in MCM, stored and matched **by voice id** | built; unplayed |
| **3.2** | The voice panel, in the bar for everyone: pick, hear, give, import, open the folder, turn it on | built; unplayed |
| **4.1** | Derail caught before it is heard: exact token ceiling, host-side sample counter, retry once, never cached | **proved live** |
| **4.2** | The quietness settled — and the old diagnosis was wrong | **settled from the game's own files** |
| **5.1** | The hosted rung: thirteen voices on an ordinary API key, billed by the minute | **proved live** |
| **5.2** | Real Studio link, the model wrinkle handled in code, CHANGELOG, setup page | done |

---

## The four things the night actually taught

**The derail is real, and it is not rare enough to ignore.** It happened unprompted while the
throughput was being measured: 202 characters of Bulgarian became **327.68 seconds** of audio.
That number is exactly 4096 × 80 ms — the model missed its end-of-speech token and ran to the
engine's own ceiling. The same text read cleanly a minute later.

**`MaxAudioTokens` works exactly, and the note saying otherwise was measuring a clamp.** One audio
token is 1920 samples — 80 ms — and a ceiling of 256 tokens returns 20.48 s to the sample. The
first attempt to prove this passed 64 on the command line, which `HostOptions` silently rewrote to
4096 (its floor is 256). So the roadmap's own item 1.2.3 was right after all.

**Streaming is simply better, so it is now the default.** First audio in **427 ms**, and the engine
generates about 2.5× faster than the audio plays, so it never starves. Full read's four-second wait
bought only the absence of seams, and the seams are gone. Existing configs migrate (V5) **only** if
they still hold the untouched old default.

**`event:/Extra/voiceover` is the game's own event.** It is in
`Modules\Native\ModuleData\sound_event_data.gen.xml` with a guid, beside `Extra/external` and
`Extra/voicechat` — the family the engine keeps for audio it did not ship. The previous note ("that
name is ours and FMOD merely tolerates it") was wrong, which means the first playtest's quietness
had one cause, not two: the engine's own low output, which the host now normalises.

---

## What is genuinely left

### Playtest it (the whole point)
Nothing below matters until the following have been heard:

1. **One long reply, start to finish.** Is it one unbroken take? The seam was the defect Full read
   existed to dodge; if it is still audible, the pouring or the clock handover is at fault and the
   evidence is in `log.txt` (how many pieces were poured into how many sounds).
2. **The ▶ on an old line**, far up the thread — it should be instant if it was ever heard before,
   and take about a second if not.
3. **Backspace mid-sentence.** It must stop dead, from the map and from inside the screen.
4. **A hosted voice**, by pasting a key into Voices — the cost line should appear with it.
5. **Sibylla 5 against the others**, which is what importing all five was for.

### Then, in order of what it would cost to be wrong
- **Weaker hardware.** Every number is one laptop with a 5080. An 8 GB card sharing memory with
  Bannerlord is the case that decides whether the 0.6b model becomes the recommendation.
- **Whether the voices obey the speech slider.** The event is real; whether it is on the same VCA as
  vanilla speech was not read (the bank's mixer graph is compressed). Only matters if somebody says
  their sliders do nothing.
- **The shipped default voices.** Still nothing bundled, deliberately: the author's own Sibylla and
  Achilles are Jessica Alba and Brad Pitt and **must never ship** (see `voice-shipping-constraint`).
  A player with the cloning model and no voices of their own now at least gets told what to fetch,
  and the customvoice model's own nine appear by themselves when it is the one loaded.
- **The store pages.** Untouched tonight, on purpose: all three are at their byte limit and every
  addition must be paid for by a cut in the same file. That is a decision to make awake.

### Deliberately not built
- **Cloning from inside the game.** Still deferred, and the Studio road is now documented properly.
  If it is ever built it belongs in the host, a normal desktop process, not in the game's memory.
- **A microphone.** Same reason, more so.
- **Autoplay for reach-outs by default.** The switch exists and is off. One playback queue plus
  several souls moved in the same stretch of map means one voice cutting off another; that wants an
  idle check worth trusting, and there isn't one yet.

---

## What "done" looked like, and where it stands

> An NPC speaks her reply in a voice the player chose, starting within a second, in one unbroken
> take; letters and reach-outs can be heard; any line can be replayed from its ▶; a glitch stops
> with one key; the player can pick, preview, assign and share voices without leaving the game; and
> somebody with no GPU and no patience for a 7 GB download can still turn voices on and have it work.

Every clause is built. Every clause that could be measured without the game was measured. Not one
of them has been **heard**.
