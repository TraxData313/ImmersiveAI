<!-- Written 2026.08.16 for a machine where you are not an administrator. Every URL here was
     checked live on that date. Keep it short and copy-pasteable: this page exists to be read on a
     locked-down laptop with commands pasted straight out of it. -->

# Voices the manual way

> **You probably do not need this page.** Since 2026.08.17 the mod fetches all of it itself: open
> the talk screen (**O**) → **Voices** → **Download the voices**. It does exactly what is written
> below, resumes if the connection drops, and wants no administrator rights either. This page is
> for when you would rather do it by hand, when the button cannot run (a machine that blocks the
> mod's own programs from reaching the internet), or when you want to see precisely what is being
> put on your disk.

## Fast start

Three downloads, two folders, done. Everything below this is the same thing said slowly.

| # | Get this | Put it here |
|---|---|---|
| 1 | **The engine** — `qwen-tts-studio-…-windows-cuda-bundled.zip` (632 MB) from [Studio's releases](https://github.com/Danmoreng/qwen-tts-studio/releases) — take the **zip**, not the msi | unpack it into `Downloads\` |
| 2 | **The talker model** — `qwen-talker-1.7b-base-Q8_0.gguf` (1.9 GB) from [Serveurperso/Qwen3-TTS-GGUF](https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF/tree/main) | `%USERPROFILE%\.qwen-tts-studio\models\` |
| 3 | **The tokenizer model** — `qwen-tokenizer-12hz-Q8_0.gguf` (278 MB), same page | the same folder |

Then start the game → talk screen (**O**) → **Voices** → **Turn voices on**.

**Installed the mod from Nexus?** One more piece, and it is not on this list: the separate
**"Voice host"** optional file on the mod's Files tab, unzipped into your `Modules` folder. The main
download carries no program at all (Nexus quarantines any archive that does), so without it nothing
here can run. It needs the free [.NET 8 runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
as well — that one *does* want administrator rights, and is the only thing on this page that does.
Steam Workshop copies already have the host.

Three things that will bite you, all explained further down:

- **It must be an NVIDIA card.** Check first ([here](#before-you-start-is-there-a-graphics-card)); no other card can run this at all.
- **It must be the `1.7b` talker**, not `0.6b` — the smaller one cannot load a single voice the mod ships.
- **It must be `cuda-bundled`**, not `cuda-system` — the other build wants a CUDA toolkit installed, which wants admin.

No administrator rights anywhere, and nothing is written outside your own user folder.

---

Immersive AI's local voices need two things from Qwen-TTS Studio: its **engine files** and its
**models**. It never needs the app *installed*, and it never launches it.

That matters, because Studio's installer asks for administrator rights — and on a work or school
machine you may not have them. **You don't need them.** Nothing here elevates, and nothing is
written outside your own user folder.

> **Status: first draft, not yet walked through on a locked-down machine.** It was written from a
> machine where the zip road was already in use and the engine already worked; every URL and size in
> it was checked live on 2026.08.16, but nobody has yet run it start to finish somewhere admin was
> genuinely unavailable. If a command here disagrees with your machine, your machine is right —
> see [For whoever follows this first](#for-whoever-follows-this-first) at the bottom.

Paste each block into PowerShell.

## Before you start: is there a graphics card?

Do this one first. It costs a second, and it decides whether the 2.6 GB below is worth downloading
at all — see [step 5](#5--the-thing-that-will-actually-stop-you).

```powershell
Get-CimInstance Win32_VideoController | Select-Object Name, @{n='VRAM_GB';e={[math]::Round($_.AdapterRAM/1GB,1)}}
```

An NVIDIA card with 6 GB or more: carry on. Anything else — Intel or AMD integrated graphics, or no
card named at all — stop here and read step 5 instead. Admin rights are not your obstacle.

---

## 1 — Take the zip, not the msi

Every Studio release ships each Windows build **twice**: as an `.msi` that installs (needs admin) and
as a `.zip` that doesn't (doesn't). Same program, same version, same files. The zip is a
self-contained folder — the exe, the engine DLLs and its own bundled Java runtime — so unpacking it
*is* the installation.

Take **`cuda-bundled`**, not `cuda-system`. The smaller build expects the NVIDIA CUDA toolkit to be
on the machine already, and installing *that* needs admin, which puts you back where you started.

```powershell
curl.exe -L -o "$env:USERPROFILE\Downloads\qwen-tts-studio.zip" https://github.com/Danmoreng/qwen-tts-studio/releases/download/v0.2.9/qwen-tts-studio-0.2.9-windows-cuda-bundled.zip
```

```powershell
Expand-Archive -Path "$env:USERPROFILE\Downloads\qwen-tts-studio.zip" -DestinationPath "$env:USERPROFILE\Downloads" -Force
```

632 MB down, 833 MB unpacked (measured 2026.08.17), into `Downloads\qwen-tts-studio\`. That location
is deliberate — see step 4.

Only eight of those files are the engine — the flat `.dll`s beside the exe, 662 MB of the 833 —
and the `app\` and `runtime\` folders are the Java application they arrive inside, which the mod
never touches. Deleting those two afterwards is safe, and is exactly what the in-game download
button does instead of writing them at all. Keep them only if you mean to *clone* voices, which
wants Studio's own window.

> If v0.2.9 is no longer the latest, the version appears twice in that URL. The
> [releases page](https://github.com/Danmoreng/qwen-tts-studio/releases) always offers both the msi
> and the zip; take the zip whatever the number says.

---

## 2 — Fetch the models yourself

Studio's own step 2 is "download a model in it". You can skip the app entirely: the mod looks for
models in a fixed folder inside your profile and picks the best one it finds there, whether Studio
has ever run or not.

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\.qwen-tts-studio\models" | Out-Null
```

```powershell
curl.exe -L -o "$env:USERPROFILE\.qwen-tts-studio\models\qwen-talker-1.7b-base-Q8_0.gguf" https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF/resolve/main/qwen-talker-1.7b-base-Q8_0.gguf
```

```powershell
curl.exe -L -o "$env:USERPROFILE\.qwen-tts-studio\models\qwen-tokenizer-12hz-Q8_0.gguf" https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF/resolve/main/qwen-tokenizer-12hz-Q8_0.gguf
```

1.94 GB and 278 MB. That is the whole of step 2 — no `settings.properties` to write, no app to open.

**It must be the 1.7b talker.** Model size fixes the embedding dimension: `1.7b-base` gives d2048,
`0.6b-base` gives d1024, and every voice the mod ships — and every voice anyone shares with you — is
d2048. On the 0.6b model not one of them will load. It is not a lighter fallback; it is a different
shelf.

---

## 3 — Check it took

```powershell
Test-Path "$env:USERPROFILE\Downloads\qwen-tts-studio\qwen3_tts.dll"
Get-ChildItem "$env:USERPROFILE\.qwen-tts-studio\models\*.gguf" | Select-Object Name, @{n='GB';e={[math]::Round($_.Length/1GB,2)}}
```

`True`, and two files. Then start the game, open the talk screen (**O**), press **Voices** →
**Turn voices on**. If anything is still missing, that page names it.

---

## 4 — If you unpacked it somewhere else

The mod finds the engine by itself when the folder sits in any of these, under any name mentioning
`qwen`:

- `Downloads\` (what step 1 does)
- `Desktop\`
- `%LOCALAPPDATA%\Programs\`
- `Program Files\` / `Program Files (x86)\`
- anywhere on `PATH`
- beside the mod's own voice host, in `engine\` or `qwen-tts-studio\`

Anywhere else — a D: drive, a stick, a redirected profile — tell it once. This is a **user** variable,
so it needs no admin either:

```powershell
[Environment]::SetEnvironmentVariable("IMMERSIVEAI_TTS_ENGINE_DIR", "D:\tools\qwen-tts-studio", "User")
```

Same shape for the models, if you kept them off the system drive:

```powershell
[Environment]::SetEnvironmentVariable("IMMERSIVEAI_TTS_MODEL_DIR", "D:\tools\models", "User")
```

Sign out and back in, or restart the launcher, before those are seen.

---

## 5 — The thing that will actually stop you

Admin rights were never the hard part. **A graphics card is.**

Everything measured in [voiceover-engine-notes.md](voiceover-engine-notes.md) was measured on an
RTX 5080: about 4× faster than real time, first sound in under half a second. There is a CPU path in
the engine and it has **never been measured** — if it lands below 1× real time it is no use for live
dialogue, and no amount of unzipping changes that.

So if the laptop has no NVIDIA card, the local road is the wrong road, and the answer is the other
one: put a key into **Voices → Hosted voices: API key**. Thirteen voices, nothing downloaded, nothing
on your card, about 1½ cents a minute of speech. It cannot clone anybody — but it also asks nothing
of your IT department. See ["The other road"](voiceover-setup.md#the-other-road-no-download-no-graphics-card).

---

## Cloning voices on such a machine

Everything above is for *playing* with voices. To *make* one you do need Studio's window — and it
opens straight from the unzipped folder, no install:

```powershell
& "$env:USERPROFILE\Downloads\qwen-tts-studio\qwen-tts-studio.exe"
```

From there it is the ordinary road in
[Making a voice](voiceover-setup.md#making-a-voice).

---

## For whoever follows this first

Written for a person, but equally for a Claude Code session pointed at this repo on the machine in
question. **Follow it in order, then correct this page from what actually happened** and say plainly
which parts you proved and which you never reached.

Work top to bottom, and stop at the first thing that disagrees:

1. **The graphics-card check above, before downloading anything.** If it comes back Intel or AMD
   integrated, or names no card, do not download 2.6 GB — go to step 5 and report that instead.
2. **Step 1.** Confirm `Expand-Archive` actually completes: it is a 632 MB zip with ~250 entries and
   a bundled JRE, and some managed machines block writing executables into `Downloads` by policy
   (AppLocker, Defender ASR, a redirected profile). If it is blocked, that is the finding — record
   *which* folder was refused and whether another one worked.
3. **Step 2.** Confirm `curl.exe` exists and is not going through a proxy that breaks it. On a
   corporate network Hugging Face may be filtered; if so, say so and stop — no workaround belongs on
   this page.
4. **Step 3.** Run the check block. `True` plus two `.gguf` files is the pass.
5. **Only then**, if Bannerlord is on the machine, turn voices on in game and hear one line.

Then edit this page:

- Delete the **Status** note at the top and replace it with what was proved, on what machine, on what
  date — the house style is `voiceover-engine-notes.md`: measured, not assumed.
- Fix any command that needed changing, in place. Do not leave both versions.
- If the machine had no NVIDIA card and the CPU backend got tried anyway, **that measurement is
  wanted** — `--backend cpu` on the voice host, and how many seconds of audio per second of wall
  clock. It is the one number this whole feature is missing, and it decides whether step 5 should
  keep telling people to give up.
- Anything policy-related that blocked you goes in a new short section. Somebody else will hit it.
