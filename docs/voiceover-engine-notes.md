# The speech engine: what we measured, and what will bite you

The developer's record behind [voiceover-setup.md](voiceover-setup.md), which is the player-facing
page. Everything here was measured or verified on 2026.08.14 against the real engine on the author's
machine (RTX 5080 Laptop, 16 GB VRAM) — **nothing below is inferred from documentation**, because
there is none.

---

## The engine

`qwen3_tts.dll` ships with **Qwen-TTS Studio** and exports a clean C ABI beside its JNI wrappers, so
it can be driven by anything, not only that app. Models are GGUF, pulled by Studio from
[Serveurperso/Qwen3-TTS-GGUF](https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF).

We do **not** redistribute the DLL, its ggml/CUDA siblings, or any model weights. Their location is
discovered from `%USERPROFILE%\.qwen-tts-studio\settings.properties` (`appDir`, `modelDir`,
`modelName`). Never hardcode those paths.

Output is **16-bit mono PCM at 24000 Hz**, from a vocoder with 16 codebooks.

## Measured, on CUDA, 1.7b talker

| | |
|---|---|
| Model load (cold) | **1513 ms** — talker 992 ms, vocoder 364 ms, tokenizer 101 ms |
| Throughput | **median 4.15× realtime** (RTF 0.235) |
| Wall clock per line | p50 585 ms · p95 2327 ms · max 9129 ms |
| Stability | **200 consecutive syntheses, 0 failures**, 754 s of audio |
| Drift | none — the last quarter ran **14% faster** than the first |
| Memory | RSS 395 → 835 MB; VRAM ~2.5–3.8 GB for the model, freed cleanly on release |

4.15× is comfortably past the 1.5× threshold that makes sentence-by-sentence playback worthwhile,
so chunked synthesis is the right shape. **Re-measure before assuming this holds on weaker cards** —
the whole design of the playback layer keys off this number.

## Measured again, 2026.08.15 — the numbers the playback layer actually uses

Same card, game closed, driving the shipped host over its own protocol. These are the ones that
decided the streaming design, the derail guard, and which delivery mode is the default.

| | |
|---|---|
| Throughput, steady state | **~3.0× realtime** (13.4 s of audio in 4.4 s, twice) |
| Throughput, FIRST call after load | 2.26× — there is a real warm-up; do not measure once |
| **Time to first streamed piece** | **427 ms** |
| Streamed piece cadence | ~420 ms per 1.04 s of audio — the generator stays ~2.5× ahead of playback |
| Speech rate | **13–17 characters a second** (224 EN → 13.4 s · 202 BG → 15.3 s · 55 EN → 4.2 s) |
| **One audio token** | **exactly 1920 samples = 80 ms** (12.5 Hz) |

Two consequences, both load-bearing:

**Streaming can be the default.** It begins in under half a second and never starves — the
generator runs at two and a half times the speed the audio is consumed. Full read's four-second
wait buys nothing once the seams are gone (see `WavFiles.Join` and `VoicePlayback`).

**`MaxAudioTokens` is exact, and it is the anti-derail knob after all.** Asked for 256 tokens, the
engine returned 20.48 s — to the sample, from two different texts. So a per-line ceiling worked out
from the line's own length (Core `VoiceBudget`) truncates a runaway precisely, and the engine's own
default of 4096 tokens is 327.68 s.

> **Watch the host's own clamp.** `HostOptions` refuses a session default below 256 and silently
> rewrites it to 4096. A cap of 64 passed on the command line therefore did nothing at all, which
> is what made the first attempt look like the field was ignored. The PER-REQUEST ceiling has its
> own floor (40 tokens) and is the one the game uses.

## The derail, observed

It happened, unprompted, while the table above was being measured — which is worth recording because
it is otherwise a thing we only had second-hand:

> 202 characters of Bulgarian → **327.68 seconds of audio** (5½ minutes), 139 seconds of GPU.

327.68 s is 4096 × 80 ms exactly: the model missed its end-of-speech token and generated until it
hit its own ceiling. The same text with a sane ceiling read cleanly in 15.3 s a minute later, so it
is a dice roll, not a property of the text.

## The derail is NOT rare — counted from a live log, 2026.08.17

The paragraph above calls it "a dice roll". It is a much loaded die than that, and the number came
from the player's own `voicehost.log` after he reported hearing one:

| | |
|---|---|
| Streamed generations in the log | **196** |
| Runaways among them | **12 — six per cent** |
| Shape of every single one | *ran to its whole ceiling* — the model never emitted end-of-speech |
| The one he heard | 388 characters, an honest ~26 s reading, **56.48 s of audio** |

Two things follow, and the second is the open question.

**The guards were built for the wrong delivery mode.** Both length rails stop a runaway only after
it has run, which cost nothing while Full read was the default — nothing is heard until the
generation ends, so it is thrown away and retried in silence — and became the whole problem when
streaming (2026.08.15) started putting every second into the air as it was made. Note in the table
that the retry only ever fires for `joined into one` requests. So a **third guard** was added
2026.08.17: past the point where the words should have ended, the host judges each piece it makes
and stops a reading that has become a held note (`Wav.SpeechLikeness` + `SynthRequest.
ExpectedSamples`). Its thresholds are REASONED, NOT MEASURED — it logs its figure for every judged
piece so the next runaway settles them.

### The control group already existed — and it is not the engine

Anton's answer to the guard was the right one: *"there must be a deeper reason, I have this other app
that we build that you speak through and it never glitches and it read so much already"*. That app
(`claude-voice`, same machine) drives **this same DLL, on this same card, through this same streaming
C ABI**, and its own notes count the same failure with the same detector:

| | generations | runaways | rate |
|---|---|---|---|
| claude-voice | ~1000 | 1 | **0.1 %** |
| Immersive AI | 196 | 12 | **6 %** |

Sixty times worse on identical machinery. So the derail is not a property of the engine, of CUDA, or
of the model — it is something *we* hand it. There were exactly two candidates, and both are now
fixed:

**1. We sent the text raw; it sends a whitelist.** `voice_lib._normalize` passes every character
through `_SPOKEN` (typographic symbols → what they MEAN: an em dash becomes a comma, an ellipsis a
full stop) then keeps ASCII and any script's letters and digits, and turns everything else into a
space. We had nothing of the kind — `SpeakableText` stripped gestures and chunked, and the host's
`Sanitize` only removed NULs. So em dashes, curly quotes, `…`, non-breaking and zero-width spaces,
and the thread's own `❦ ☾ ✒` card marks went straight into the tokenizer. A rare token is precisely
what an autoregressive decoder wanders off after, and the mod's prose style reaches for typographic
dashes constantly. Ported whole into `SpeakableText.Normalize` (2026.08.17), with `Tidy` after it so
a dash-turned-comma does not leave `a , b`. **Letters of every script pass** — asking for
`[A-Za-z0-9]` would silently mute every Bulgarian word in the mod.

**2. We cooled the sampling; it uses Studio's own.** We shipped `temperature 0.55 / topP 0.85`
against Studio's `0.9 / 1.0`. Cooled sampling collapsing onto a repeated token is the textbook
failure of an autoregressive decoder, and a repeated audio token is exactly what a held vowel *is*.
Worse, **the reason for the cooling had expired**: it was introduced 2026.08.14 to stop the voice
changing person BETWEEN sentences, and streaming made a reply ONE generation the very next day, so
there was no seam left for it to hold. Restored to `0.9 / 1.0`, with `ModConfig.VoiceTemperature` /
`VoiceTopP` (0 = the engine's own) and `--temperature` / `--top-p` on the host, so a player whose ear
disagrees can put it back without a rebuild. The one road that still genuinely has seams is
`Delivery.ByLine`, which is legacy and not the default.

**What is NOT yet known** is which of the two carried the fault, or whether both did — they shipped
together, because both are right on their own merits. The way to tell is the log: `derail guard`
lines should now be rare. If they are not, the sampling is the half to try first, since it is one
config edit.

**Both length guards are in and both are proven live** (`guard-hit`, `guard-whole`, 2026.08.15):
a per-line token ceiling that the engine honours to the sample, and the host's own sample counter
that stops the generation itself if that ceiling ever stops meaning what it means. A generation that
runs to its whole ceiling is treated as a runaway on that fact alone — a sentence that ends by
itself practically never lands on the rail to the token — and a WHOLE reply retries once from
scratch before keeping anything. **A derailed clip is never cached**, which was always the point.

## The two models are not interchangeable

- `qwen-talker-1.7b-base` — the **cloning** road. Speaker embeddings and ICL prompts.
- `qwen-talker-1.7b-customvoice` — carries **9 named built-in speakers**:
  `aiden, dylan, eric, ono_anna, ryan, serena, sohee, uncle_fu, vivian`
  (`get_available_speakers` returns them newline-separated, and `caps.speakers == 9`).
  The base model reports none.

The built-ins were auditioned as shippable defaults on 2026.08.14 and **rejected by the author** —
"they are very stupid all of them". They remain a zero-asset fallback if that judgment ever changes.
The tokenizer GGUF is auto-discovered from the same directory and is never named in a call.

## The ABI, as confirmed by the M0 harness

The struct is returned **by value** (hidden pointer in RCX on x64) and the layout below round-tripped
against real audio 200 times.

```c
QwenResult { float* audio; int numSamples; int sampleRate; int success;
             int _pad; const char* error; int64 timeMs; }   // 0x28 bytes
```

`QwenParams` is a **64-byte block for the plain calls and 80 for the streaming ones**; always pass 80
zeroed bytes, which is safe for both. Two fields Studio leaves **uninitialised** (`0x24`, `0x3C`) —
zero them, or you are passing whatever was on the stack. Studio's own values: `maxAudioTokens 4096`,
`temperature 0.9`, `topP 1.0`, `topK 50`, `threads 4`, `languageId -1` (auto; `en = 2050`,
`ru = 2069`).

**`MaxAudioTokens` is at offset 0x00, and it is the anti-glitch knob** — see below.

Strings cross as UTF-8 `byte[]`. `free_result` / `free_string` are the deallocators; the error string
is owned by the result. `get_last_error` is the best oracle when a call fails.

## The ICL road is broken on a base model — use embeddings

**This one nearly made the whole feature look broken, so it is the first thing to know.** A voice
imported from Studio carries BOTH an `icl-prompt.json` and an `embedding.json`, and ICL is on paper
the better clone — so preferring it is the obvious choice and it is the wrong one.

Measured 2026.08.14 on `qwen-talker-1.7b-base`, same voice, same words:

| road | result |
|---|---|
| `embedding` | 12/12 clean, a five-second line in ~1.3 s, repeatable |
| `icl` | ~1 in 5 returns `"No speech codes generated"`; most of the rest come back **truncated** — `ok:true` with 1920 samples, i.e. eight hundredths of a second |

Note the failure shape: it mostly returns **success** with almost no audio. Anything that only checks
`ok` will sail straight past it. The M0 harness's own `log-abi.txt` recorded this and it was missed —
the "200 syntheses, zero failures" run used the embedding call.

Likely cause: ICL wants its prompt encoder loaded separately
(`qwen3_tts_load_icl_prompt_encoder_with_name`) and a base model has none. Untested. **If the ICL road
is ever made to work, prove it live before turning the preference around.**

The general lesson, which outlives this bug: **treat "far less audio than the text justifies" as a
failure**, the same detector the derail guard uses from the other end.

## Traps, each one paid for

**A failed allocation aborts the process.** ggml's failure mode is `GGML_ASSERT` → `abort()`. On the
game's runtime an access violation is fail-fast: a `try/catch` around a `DllImport` catches *nothing*,
and `HandleProcessCorruptedStateExceptions` does not exist there. **This is the entire reason the
engine runs in a sidecar** and not in the game process. Do not "simplify" it back in-process.

**Bad input is handled cleanly, though.** A missing ICL file returns
`success=0, error="Failed to load ICL prompt file"` rather than aborting — so validating paths before
calling in is cheap insurance that actually works.

**The engine is not re-entrant.** Serve one request at a time.

**`TaleWorlds.Engine.Path` collides with `System.IO.Path`.** Never add `using TaleWorlds.Engine;` to a
file that touches the filesystem; fully qualify `TaleWorlds.Engine.SoundEvent` instead.

**A stranded host is the worst outcome this design can produce.** ~4 GB of VRAM held after the player
quits. The watchdog (parent PID + stdin EOF) is not optional.

## Playback, verified in game

`SoundEvent.CreateEventFromExternalFile("event:/Extra/voiceover", wavPath, scene: null, is3d: false,
isBlocking: false)` then `.Play()` — **confirmed working**, played aloud in a live campaign on
2026.08.14 with a 9.76 s 24 kHz mono WAV. This rides FMOD's own voice-over bus, so the player's
volume sliders, mute-on-alt-tab and the game's ducking all come free.

**And the event is REAL — corrected 2026.08.15.** This page previously said the game defines no
`event:/Extra/voiceover` and that FMOD merely tolerated a name of ours. It does define it:
`Modules\Native\ModuleData\sound_event_data.gen.xml`, guid
`{2a2e4e13-d391-41bd-bf9e-91891d2c63f4}`, sitting beside `event:/Extra/external` (10 s) and
`event:/Extra/voicechat` / `voicechat3D`. That whole `Extra/` family is what the engine keeps for
audio the game did not ship, which is precisely our case.

So the first playtest's "veeeery quietly, can barely hear her" had **one** cause and not two: the
engine's own output sits 10-20 dB under a speaking level. The host normalises it now and that is the
end of it. If the event ever does need moving, `event:/Extra/external` is the nearest sibling and
allows a longer sound; it is a config edit (`VoiceSoundEvent`), not a rebuild.

`CreateEventFromSoundBuffer(..., byte[], ...)` **constructs successfully** but was never played (one
at a time). It has zero callers in the shipped game, so TaleWorlds never tested it. If it ever proves
to play, it would remove the disk-cache file-handle problem — but the cache is wanted anyway for
reuse, and a sidecar can only hand over a path, so the file road is the right default.

## The glitch, and why the cap is the real fix

Autoregressive TTS derails: the model misses its end-of-speech token and generates until it hits its
own ceiling, which comes out as babbling or screeching. The author reports (from experience elsewhere)
that it happens **"in the middle or at the end, but not ever in the beginning."**

That is *drift*, not bad conditioning, and three things follow:

1. **Truncation is safe** — every word before the derail is good.
2. **Sentence chunking is a reliability feature**, not only a latency one. A derail costs one
   sentence instead of a whole reply. Do not later "optimise" it into whole-reply synthesis.
3. **Detect and retry in the host, so the player never hears it.** A derailed line runs long by
   definition, so *samples far beyond what the text justifies* is a sound detector. At 4.15× realtime
   a 4 s sentence re-synthesizes in about a second: discard, retry once, truncate only if it derails
   twice. **A suspect clip must never reach the cache**, or one bad synthesis is replayed every time
   that line is scrolled back to, forever.

Set `MaxAudioTokens` from the text's own length (roughly 3× the expected duration) so a runaway is
guillotined at ten seconds rather than running to 4096 tokens.

## The hosted road, measured too (2026.08.15)

Live against OpenAI's `/v1/audio/speech`, `gpt-4o-mini-tts`, on the author's own key:

| | |
|---|---|
| English, 104 characters | 7.6 s of audio in **2586 ms** (~2.9× realtime) |
| Bulgarian, non-ASCII escaped as `\uXXXX` | 4.1 s in **1377 ms** — the escaping road works |
| Format returned for `response_format: "wav"` | **24 kHz, 16-bit, mono** |
| A bad key | HTTP 401, named plainly to the player |
| Price | **$0.015 a minute** of audio, so a 7.6 s line is two tenths of a cent |

The format is the happy part: **it is byte-for-byte the shape the local engine produces**, so the
cache, the joiner and the whole playback chain are shared with no conversion and no special cases.
And because the WAV's own header says how long it is, the cost notice is measured rather than
estimated — see `UsageLedger.NoteVoiceMinutes`.

## Still unmeasured

- **Weaker hardware.** Every number above is one laptop with a 5080. An 8 GB card sharing VRAM with
  Bannerlord is the case that decides whether the 0.6b model becomes the recommended default.
- **Whether `CreateEventFromSoundBuffer` actually plays.**
- **Whether `event:/Extra/voiceover` is on the same VCA as vanilla speech.** The event exists (above),
  but the bank's mixer graph is compressed and was not read. It matters only if somebody reports the
  voices ignoring their speech slider.
