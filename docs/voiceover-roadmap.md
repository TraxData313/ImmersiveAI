# Voiceover: what is left, and in what order

Written 2026.08.14, after the first working playtest. The pipeline speaks: one generation per reply,
one file when it must be gapless, three delivery roads live in MCM. This is the plan for the rest.

Background reading before touching any of it: `voiceover-engine-notes.md` (the measurements and the
traps) and the memory note `voice-pipeline-lessons`.

**The ordering principle:** first the things that make PLAYTESTING faster, because every later item is
tested by hand and the loop is currently slow. Then the known defects. Then features. Then the work
that makes the feature exist for anybody but its author.

---

## Wave 1 — make the loop fast, and make it safe

*Small, mostly mechanical, and each one pays for itself immediately.*

### 1.1 A play mark on every message
The talk screen's thread rows gain a small ▶. **Not only on spoken replies** — her own narration
beats (the inner-mind lines) and letters want it too. Asked for explicitly, partly so a line can be
re-heard without generating a new one, which is what makes everything below quicker to test.

- `ChatMessageVM` gains one command and one visibility flag; the row template in
  `ImmersiveTalkScreen.xml` gains the button (copy the 40×36 right-aligned glyph idiom already at
  `:349-374`).
- The row must NOT store audio state: `RefreshThread()` allocates a fresh `MBBindingList` on every
  change and would throw it away. Re-derive with `VoiceCacheKey.For` from the words the row holds —
  this is exactly what that key was built for.
- A letter row speaks its **body**, not its envelope furniture.
- Whoever is speaking is cut off first: newest words win, same rule as everywhere.

### 1.2 The panic stop
Because a derailed line that will not stop is the worst thing this feature can do to somebody.
Three layers, and the third is the real fix — full reasoning already in `TASKS_TODO.md`:

1. A **hotkey that works anywhere**, not behind a screen.
2. A stop control in the talk screen for the ordinary case.
3. **Cap `MaxAudioTokens` from the text's own length** (~3× the expected duration), so a runaway is
   guillotined at ten seconds instead of running to 4096 tokens.

### 1.3 Import from Qwen-TTS Studio
One button, and `VoiceLibrary.ImportFromStudio` behind it is already written and unit-tested. With
in-game cloning deferred this is the whole road from "I made a voice" to "she speaks with it", so it
stops being a nicety and becomes the workflow.

### 1.4 Streaming without the seam — the highest-value item left
Streaming still chains its pieces on the game tick, which puts a frame of silence inside every second
of speech. Build the next `SoundEvent` while the current one plays and start it the instant the last
ends, off the tick. Then Streaming is gapless AND starts in under a second, which makes it the right
default and retires Full read to a fallback for slow machines.

Do this early, not late: it changes which mode everything else is judged against.

### 1.5 The casting he actually wants
Sibylla 5 as the default female; import all the Sibyllas so he can A/B them. Small, and it stops him
testing against a voice he does not like.

---

## Wave 2 — the voice reaches everywhere it should

### 2.1 Reach-outs and letters speak
The other half of the original ask, still unwired. **Mind the presentation point**, which is the
mistake all three original designs made: a letter must speak when it is **opened**, never when it was
composed days of travel earlier — the compose beat literally renders as "it is sealed, and rides
toward you still". Reach-outs speak when their words are shown, not when they are generated.

Autoplay for reach-outs stays **off** by default until there is an idle check worth trusting:
`IsSafeToInitiate` fires while the player is doing anything on the map, including reading somebody
else's reply, and one global playback queue means Ava would cut Sibylla off mid-sentence.

### 2.2 The player's own voice
His own lines spoken in a voice he picks. `VoiceAssignments.Player` already exists and is honoured;
this is mostly UI plus a decision about the default (see Q2).

---

## Wave 3 — the player can drive it without a text editor

### 3.1 Male and female defaults in MCM
Currently only `assignments.json`. The wrinkle: MCM dropdowns are normally static, and this list
comes from the player's own voice folder — so it must be populated at bind time from the shelf, and
MCM persists dropdowns **as indices**, which is fragile when the underlying list can change between
sessions. Store the chosen voice **id** in config and match by id on bind, treating the index as
disposable.

### 3.2 The voice panel
The full thing, behind a button in the talk-screen bar available to everyone (copy the Dev button
verbatim, swap the command and the visibility flag). Pick a voice, preview it, assign it to this
soul, set the defaults, open the folder.

Follow the standard overlay shape, and join the Escape chain in **all three** places
(`TalkScreenManager` Escape chain, its Enter guard, and `TalkScreenVM.ExecuteBack`) or it misbehaves
exactly as that manager's own comments warn.

### 3.3 Creating a voice in game *(deferred — see Settled)*
The best affordance available and it is nearly free: **preview uses zero-shot**
(`synthesize_with_voice` on the reference clip — no extraction at all), so the player hears the clone
before committing. Only on "keep this voice" do we run the extraction and write the folder.

No microphone in v1. If it is ever built it belongs in the **host**, a normal desktop process, not in
the game's address space.

---

## Wave 4 — make it good rather than merely working

### 4.1 Catch a derail before it is heard
A derailed line runs long by definition, so "far more audio than this text justifies" is a sound
detector, in the host, before a sample reaches the speakers. At ~3.6× realtime a re-synthesis costs
about a second: discard, retry once, truncate only if it derails twice. **A suspect clip must never
reach the cache**, or one bad line is replayed for the rest of the campaign.

### 4.2 The quietness, properly settled
The host normalises now, which fixed it in practice. But the game defines no
`event:/Extra/voiceover` — FMOD merely tolerates the name — so we may not be riding the game's own
`vca:/Voiceover` fader at all. Worth one experiment against the real events
(`event:/mod/mission/voice` and friends) to find out. `VoiceSoundEvent` makes that a config edit
rather than a rebuild.

---

## Wave 5 — make it exist for people who are not us

### 5.1 The hosted rung
`CloudVoiceEngine` behind the same seam, on the OpenRouter or OpenAI key the player already has.
It cannot clone a voice, which is exactly why it is the **stranger's** road and Qwen is the author's.
Billed through `UsageLedger` with its own price line — TTS eats roughly 10× the text credits and the
cost story stays boringly honest. Without this, the feature realistically reaches single-digit
percent of players.

### 5.2 Shipping
- The default voices: built-ins were auditioned and rejected ("very stupid all of them"), so this
  means cloning a clean female and male from public-domain audio. **The author's own Sibylla and
  Achilles are Jessica Alba and Brad Pitt and must never be bundled** — see the memory note
  `voice-shipping-constraint`. His own machine keeps them; his local choice overrides the shipped
  defaults silently, so he loses nothing.
- `docs/voiceover-setup.md` needs the real Qwen-TTS Studio download link — there is a `TODO` in the
  file and the URL must not be guessed.
- CHANGELOG pills, the three store pages (each addition paid for by a cut in the same file, measured
  in BYTES), and the licence position: we redistribute no third-party binary and no model weights.

---

## Settled (Anton, 2026.08.14)

- **The player's own voice: OFF by default.** Hearing your own character read your words back is
  divisive, and it doubles the synthesis per exchange.
- **Streaming's seam moves up.** Fixing it is now item 1.4 rather than 4.1: once Streaming is
  gapless it is strictly better than Full read — first word in under a second AND no seams — so it
  becomes the default, and everything after it gets tested against the mode that will actually ship.
- **In-game cloning is deferred**, and the Studio road is documented properly instead
  (`voiceover-setup.md`, "Making a voice"). Consequence: the **Import from Studio button moves into
  Wave 1**, because with cloning gone it is the only thing standing between the player and a new
  voice — and its Core code is already written and tested.
- **Letters speak on the play mark only**, never unbidden when one is opened.
- **The shipped default is the model's own built-in speakers.** They were judged poor, but poor beats
  silent for somebody with no voices of their own, and they cost nothing to ship. Better ones come
  later: voices we generate ourselves from material that is free to use — alive and warm for the
  women, hard and weathered for the men — rather than cloned from anybody real.
  **THE WRINKLE, and it needs handling in Wave 5:** the built-in speakers live on
  `qwen-talker-1.7b-customvoice`, NOT on the `base` model that does the cloning. A player with the
  base model and no voices of their own has no fallback at all — so this must either tell them
  plainly which model to fetch, or the host must be able to bring up the other one.

## What "done" looks like

An NPC speaks her reply in a voice the player chose, starting within a second, in one unbroken take;
letters and reach-outs can be heard; any line can be replayed from its ▶; a glitch stops with one
key; the player can pick, preview, assign and share voices without leaving the game; and somebody
with no GPU and no patience for a 7 GB download can still turn voices on and have it work.
