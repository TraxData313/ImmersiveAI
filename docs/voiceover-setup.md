<!-- Rewritten 2026.09.24 when the voices moved out of the mod and into claude-voice, a separate
     free app. Keep this URL path stable: README and all three store pages point at it.
     The earlier page (the in-mod engine, the download button, the hosted road) is in git history. -->

# Hearing them speak

## Fast start

Four steps, and only one of them is a wait.

1. In game, open the talk screen (**O**) and press **Voices**.
2. Press **Install the voice app**, then **Install**. A small setup window opens beside the game —
   **Alt+Tab** if it is hidden behind it.
3. In that window, keep the choice marked **recommended** and press **Install**. Go back to playing:
   it takes 10–30 minutes, almost all of it downloading, and carries on if the connection drops.
4. When it says **All set**, the Voices page lights up. Press **♪** beside a voice to hear it.

Then a **▶** sits beside every line of every conversation, and **Backspace** silences anything,
anywhere. Everyone is given a voice of their own people and their own sex automatically; give
someone a different one on the Voices page whenever you like.

A voice problem never costs you a word — the reply always arrives, whatever the sound is doing.

---

## What makes the voices

**[claude-voice](https://github.com/TraxData313/claude-voice)** — a free, open app by the same
author that reads text aloud in natural voices, **on your own computer**. No account, no
subscription, nothing you hear ever leaves your PC, and nothing asks for administrator rights. It
runs beside the game; the game asks it to speak.

It has three voice engines. The setup looks at your computer and marks the best one for you — you
do not need to understand any of this to press Install.

| | **Breeze** — the actor | **Qwen** — the storyteller | **Pocket** — the light one |
|---|---|---|---|
| **What it is like** | laughs, sighs and whispers where a character does | warm, natural voices | clear and quick, a little plainer |
| **Languages** | English | **any** — Bulgarian, Russian, Chinese… | English, French, German, Spanish, Italian, Portuguese |
| **Needs** | NVIDIA **RTX 30-series or newer, 16 GB** of video memory (12 GB works, slower) | an **NVIDIA** card, about **4 GB** of video memory | **any Windows PC** — no graphics card at all |
| **Download** | ~11 GB | ~3 GB | ~1 GB |
| **Disk space** | ~20 GB | ~3.5 GB | ~1.5 GB |

**The short version:** playing in English on a big NVIDIA card → **Breeze**. Playing in another
language, or a smaller NVIDIA card → **Qwen**. No NVIDIA card, or a laptop that runs hot → **Pocket**.

You can add another engine later: on the Voices page, an engine name with a **+** after it is not
installed yet — press it and its setup opens with it chosen. Switch between installed ones there
too; the first line after a switch waits while it loads (up to a minute).

### What every engine needs

- **Windows 10 or 11.**
- **Internet**, once, for the download.
- **Python** — the app runs on it, and the setup fetches it for you if you have none.
- **Room while playing.** The voice runs beside the game: Qwen holds about 3.5 GB of video
  memory, Breeze 9–13 GB, Pocket about 1 GB of ordinary memory and a little processor time. If the
  game stutters with voices on, Pocket is the lightest.

## Laughing and whispering

On **Breeze**, when a character laughs in their acted parts — *\*laughs softly\** — you hear the
laugh instead of the word; the same for a sigh, a cough, a cleared throat. A *\*whispered\** line
is whispered on Breeze and Qwen. It is the setting **Let them laugh and whisper**, on by default;
on Pocket it changes nothing.

## Pocket and the voices of Calradia

Ninety-odd voices ship with the mod — the women and men of every people, plus the bandits. Qwen
and Breeze speak all of them. **Pocket** speaks them by cloning each from a short clip, and that
uses Kyutai's voice-cloning weights, which want a free Hugging Face account: accept the terms at
[huggingface.co/kyutai/pocket-tts](https://huggingface.co/kyutai/pocket-tts), then run
`hf auth login` once. Without it Pocket uses its own 21 English voices instead — every soul still
gets one of their own sex, just not of their own people.

## Who speaks for whom

- **Nobody cast by hand** gets a voice of their own people and sex, picked from their name — the
  same voice every session, through every reload.
- **Cast someone** on the Voices page: choose a voice, then press their name. **Take it away**
  gives them back their people's voice.
- **me** gives *your* lines a voice too, if you want to hear yourself.
- Each engine has its own voices. A voice you cast that the engine speaking now does not have is
  kept, and comes back when you switch back; meanwhile they speak with one of their people.

## The voice app itself

- **Voices → Open the voice app** shows its own window: volume, pause, and a history of what was
  said. It no longer floats on top of everything, so it will not sit over your game.
- **Close the voice app** frees its memory. With voices on, the game starts it again when a
  campaign loads (the setting **Start the voice app with the game**) — and closes it on exit, but
  only if the game opened it.
- It can also read **Claude Code** answers aloud, if you use that and ticked the box in its setup.
  It is off otherwise.

## When it goes wrong

- **"The voice app isn't installed yet"** — press **Install the voice app** on the Voices page.
- **The setup window never appeared** — it is behind the game: **Alt+Tab**.
- **The setup stopped** — press **Try again** in its window; it picks up where it stopped, downloads
  included. **Copy details** puts the whole story on your clipboard if you want to report it.
- **Installed, but silent** — the Voices page says whether the app is running. If not, press
  **Start the voice app**; the very first start takes up to a minute.
- **Some lines are silent, others speak** — Breeze and Pocket cannot read Cyrillic, so Bulgarian
  or Russian lines stay quiet (you are told once). Switch to **Qwen**, which reads every language.
- **A voice rambles or will not stop** — **Backspace**, anywhere.
- **Windows warns about ClaudeVoiceSetup.exe** — it is small, open-source and unsigned; the source
  is on [GitHub](https://github.com/TraxData313/claude-voice/tree/main/setup-app).
- **Rather install it by hand?** claude-voice's own
  [install guide](https://github.com/TraxData313/claude-voice/blob/main/docs/install.md).

## Coming from an older version

Until v3.3 the mod carried its own speech engine. It is gone — it was the reason the Nexus download
kept being quarantined — and the voice app does the same job better. What you had carries over:

- **Your castings** stay exactly as they were.
- **Voices you made** stay in `Configs\ImmersiveAI\Voices` and the voice app is told about them.
- **The voice models you downloaded** (about 2 GB) are found and reused by Qwen's setup.
- A leftover `VoiceHost` folder in the mod's install is removed by the update; nothing else to do.
