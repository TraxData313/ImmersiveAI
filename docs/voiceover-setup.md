<!-- Rewritten 2026.09.24 when the voices moved out of the mod and into claude-voice, a separate
     free app, and again the same night when the whole install moved INSIDE the game (no setup
     window, progress drawn on the Voices page). Keep this URL path stable: README and all three
     store pages point at it. The earlier pages are in git history. -->

# Hearing them speak

## Fast start

Everything happens on one page, inside the game.

1. Open the talk screen (**O**). The **Voices** button in the top bar is **red** — nothing is set up
   yet. Press it.
2. The page shows three voice engines, with the one that suits **your** computer marked
   **Best for your computer**. Press **♪ Hear it** on any of them to hear a sample first.
3. Pick where to keep the voice files (your drives are listed with their free space), then press
   **Install**. That's the last click.
4. Watch it go: four steps with ticks, what is downloading, how fast, how long is left. Keep playing
   if you like — even quit the game; the install carries on and the page picks it up next time.
5. When the button turns **green**, whoever you are talking to says their first words.

Then a **♪** sits beside every line of every conversation, and **Backspace** silences anything,
anywhere. Everyone is given a voice of their own people and their own sex automatically; give
someone a different one on the Voices page whenever you like.

A voice problem never costs you a word — the reply always arrives, whatever the sound is doing.

---

## What makes the voices

**[claude-voice](https://github.com/TraxData313/claude-voice)** — a free, open app by the same
author that reads text aloud in natural voices, **on your own computer**. No account, no
subscription, nothing you hear ever leaves your PC, and nothing asks for administrator rights. It
runs beside the game; the game asks it to speak. You never have to open it — the Voices page is its
remote.

It has three voice engines. The page reads your graphics card and marks the best one; you do not
need to understand any of this to press Install.

| | **Breeze** — the actor | **Qwen** — the storyteller | **Pocket** — the light one |
|---|---|---|---|
| **What it is like** | laughs, sighs and whispers where a character does | warm, natural voices | clear and quick, a little plainer |
| **Languages** | English | **any** — Bulgarian, Russian, Chinese… | English, French, German, Spanish, Italian, Portuguese |
| **Needs** | NVIDIA **RTX 30-series or newer, 16 GB** of video memory (12 GB works, slower) | an **NVIDIA** card, about **4 GB** of video memory | **any Windows PC** — no graphics card at all |
| **Download** | ~11 GB | ~3 GB | ~1 GB |
| **Disk space** | ~14 GB | ~3.5 GB | ~1.5 GB |
| **Graphics card while it runs** | holds ~13 GB | holds ~3.5 GB | not used |
| **Voices** | all of Calradia's | all of Calradia's | its own English voices |

**The short version:** playing in English on a big NVIDIA card → **Breeze**. Playing in another
language, or a smaller NVIDIA card → **Qwen**. No NVIDIA card, a laptop, or the game stutters →
**Pocket**. The page's **They'll speak: English / Another language** switch changes its advice.

## After it is installed

The Voices page has two tabs once the voice app is running:

- **Who speaks how** — every voice, with ♪ to hear it; pick one and give it to the person you are
  talking to, to yourself (**me**), or send them back to **Their people's** voice.
- **Engines & storage** — the three engines again: **Switch** to one you have, **Install** another
  (the voices pause while it installs, then come back speaking with it). Below: **where every file
  is and how big**, with a button to open each folder, and **Remove the voice app…**, which takes all
  of it away again.

### The game stutters while they speak

The voice shares your graphics card with the game. Two cures, both on the Engines tab:

- **Switch to Pocket** — it runs on the processor and leaves the graphics card to the game.
- **Close the voice app** — frees the graphics card completely. It also closes by itself when you
  leave the game, if the game was what started it.

## Laughing and whispering

On **Breeze**, characters are told their voice can act: they open a reply with a mood — *(tender)*,
*(playful)*, *(sad)*… — and write *(laugh)* or *(sigh)* where it happens, and you hear it. The
thread shows those in orange. On **Qwen** they get the moods (performed more gently). On
**Pocket** nothing changes. It is the setting **Let them laugh and whisper**, on by default.

## Pocket and the voices of Calradia

Ninety-odd voices ship with the mod — the women and men of every people, plus the bandits. Qwen
and Breeze speak all of them. **Pocket** can only speak them by cloning each from a short clip, and
Kyutai keeps those cloning weights behind a free Hugging Face sign-in: accept the terms at
[huggingface.co/kyutai/pocket-tts](https://huggingface.co/kyutai/pocket-tts), then run
`hf auth login` once. Without it Pocket uses its own 21 English voices instead — every soul still
gets one of their own sex, just not of their own people.

## Who speaks for whom

- **Nobody cast by hand** gets a voice of their own people and sex, picked from their name — the
  same voice every session, through every reload.
- **Cast someone** on the Voices page: choose a voice, then press their name. **Their people's**
  gives them back their people's voice.
- **me** gives *your* lines a voice too, if you want to hear yourself.
- Each engine has its own voices. A voice you cast that the engine speaking now does not have is
  kept, and comes back when you switch back; meanwhile they speak with one of their people.

## Where everything goes

- **The app** — `%LOCALAPPDATA%\Programs\claude-voice` (about 100 MB).
- **The voice files** — the folder you picked: `%LOCALAPPDATA%\claude-voice` on your system drive,
  or `X:\claude-voice` on another drive.
- **Python** — only if your computer had none: a private copy, removed with the app.

The **Engines & storage** tab shows the real folders and sizes, each with an **Open** button.

## Taking it away

**Voices → Engines & storage → Remove the voice app…** — or Windows **Settings → Apps →
claude-voice → Uninstall**. Both take the app and every voice file it downloaded, and leave
anything that was on your computer before it. Voices turn off in the game; the Voices button turns
red again, and installing is one click away if you change your mind.

## When it goes wrong

- **The install stopped** — the page says why, in plain words. **Try again** carries on where it
  stopped, downloads included. **Show what happened** opens its log; **Use the setup window** runs
  the same install in a window of its own, which helps when a firewall or antivirus wants to ask
  first.
- **Installed, but silent** — the Voices button is red and the page says the app isn't running:
  press **Start the voice app**. The first start takes up to a minute.
- **Some lines are silent, others speak** — Breeze and Pocket cannot read Cyrillic, so Bulgarian
  or Russian lines stay quiet (you are told once). Switch to **Qwen**, which reads every language.
- **A voice rambles or will not stop** — **Backspace**, anywhere.
- **Rather install it by hand?** claude-voice's own
  [install guide](https://github.com/TraxData313/claude-voice/blob/main/docs/install.md).

## Coming from an older version

Until v3.3 the mod carried its own speech engine. It is gone — it was the reason the Nexus download
kept being quarantined — and the voice app does the same job better. What you had carries over:

- **Your castings** stay exactly as they were.
- **Voices you made** stay in `Configs\ImmersiveAI\Voices` and the voice app is told about them.
- **The voice models you downloaded** (about 2 GB) are found and reused by Qwen's setup.
- A leftover `VoiceHost` folder in the mod's install is removed by the update; nothing else to do.
