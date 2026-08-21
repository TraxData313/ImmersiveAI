<!-- Written 2026.08.14; finished 2026.08.15 when the rest of the feature landed. The play marks,
     the panic key, the Studio import button, the voice panel, the hosted road and the built-in
     speakers are all shipped, and this page now describes them as they are rather than as planned.
     NOT yet playtested in a live campaign: if a label here disagrees with the game, the game is
     right and this page is stale. Keep this URL path stable — README and all three store pages
     point at it.

     2026.08.17: the four manual setup steps became ONE BUTTON — the mod fetches the engine and the
     models itself. The old road is kept in voices-without-admin.md rather than here. The same day
     the NVIDIA requirement was stated plainly everywhere (it was always true and this page said
     only "a graphics card"), and the disk figure was corrected from a guessed ~7 GB to a MEASURED
     2.9 GB: 662 MB of libraries and 2.2 GB of models. Measure before you write a number down. -->

# Hearing them speak

## Fast start

Five steps, and only one of them is a wait. Nothing to install first — the mod carries everything
it needs, and the rest it fetches itself.

1. **Have an NVIDIA card?** If not, skip to [the other road](#the-other-road-no-download-no-graphics-card) — a key, no download, no card. It is the one requirement with no way around it.
2. In game, open the talk screen (**O**) → press **Voices**.
3. Press **Download the voices** → say yes. ~2.8 GB, in the background, while you carry on playing. Nothing installs, nothing asks for administrator rights. Connection drops? Press it again, it carries on.
4. Press **Turn voices on**. You are told when it is ready.
5. Press **♪** beside a voice to hear it, and give it to whoever you are talking to. Or give it to nobody: everyone is handed a voice of their own people and their own sex, automatically.

Then: a **▶** sits beside every line, and **Backspace** silences anything, anywhere.

Nothing happening? Jump to [When it goes wrong](#when-it-goes-wrong). A voice problem never costs
you a word — the reply always arrives, whatever the sound is doing.

---

Immersive AI can read every NPC's words aloud, in a voice you choose — including voices **you
clone yourself** from a few seconds of audio. This page tells you how. It gets more detailed as
you scroll: **read only as far as you need.**

Voices are **off by default**, and turning them on is a real decision — it wants about three
gigabytes and an **NVIDIA** graphics card. Everything below is about whether that trade is worth it
to you.

---

## Just tell me what I need

| If you want… | You need | Cost |
|---|---|---|
| **Any voice at all** | An **NVIDIA** card and ~3 GB free | free, runs on your PC |
| **Your own cloned voices** | The same, plus a clip of the voice | free |
| **No download, no GPU** | A key for a hosted speech service | ~1½ cents a minute of speech |
| **No voices** | Nothing — leave it off | — |

The local road is the good one: it is free forever, it never sends a word off your machine, and it
is the only one that can clone a voice. The hosted road exists so people without a gaming PC still
get spoken NPCs.

> **It has to be NVIDIA.** The speech engine ships only as a CUDA build — there is no AMD or Intel
> build, and no CPU one to fall back to. On any other card the download would be spent only to fail
> when the model loads, so the mod checks before it offers, and points you at the hosted road
> instead. This is the one requirement with no way around it.

---

## What it actually costs you

Not money — the local engine is free and unlimited. It costs **hardware**:

| | |
|---|---|
| **Disk** | ~2.9 GB — 662 MB of speech engine, 2.2 GB of models |
| **Video memory** | ~3–4 GB *on top of what Bannerlord is already using* |
| **Speed** | About 4× faster than real time on a modern card — a four-second line takes about a second to make |
| **First line after loading** | ~1.5 seconds extra, once, while the model wakes up |

**The video memory is the part to think about.** If your card has 8 GB and you play at high
settings, you are going to be tight. There is a smaller, faster model that sounds slightly worse
and costs about half as much — switch to it if things get choppy.

Speech is made **one line at a time, as it is needed**, and kept afterwards. Scroll back to
something said an hour ago and it plays instantly; nothing is generated twice.

---

## The other road: no download, no graphics card

If none of the above is going to happen on your machine, put a key for a hosted speech service into
**Voices → Hosted voices: API key** (in the mod's settings), and thirteen voices appear in the panel
alongside your own. Nothing is downloaded and nothing is loaded onto your card.

What it costs, and what it costs you:

- **About 1½ cents per minute of speech** — roughly two tenths of a cent for a typical reply. It is
  billed to your key and shown in the same cost line as everything else the mod spends.
- **It cannot clone anybody.** You choose from a fixed shelf; the whole point of the local engine is
  that it can be made to sound like a particular person, and this cannot.
- **The words leave your machine**, exactly as they already do if you are using a cloud AI for the
  conversations themselves.

Both roads can be used at once: cloned voices for the people you care about, hosted ones for
everybody else. Which road a voice takes is decided by the voice, never by a setting.

---

## Setting it up

1. **Open the talk screen (O) and press Voices.**
2. **Press "Download the voices".** It says what it is about to fetch and where it will put it;
   say yes and it does the rest — about 2.8 GB, in the background, while you carry on playing.
   Nothing is installed, nothing asks for administrator rights, and everything is written inside
   your own user folder. If the connection drops, press it again: it carries on from where it
   stopped rather than starting over.
3. **Press "Turn voices on".** That is it — you will be told when it is ready.
4. **Pick who speaks.** Press **♪** beside a voice to hear it, then give it to the person you are
   talking to — or to nobody at all, and let everyone be given a voice of their own people (see
   below). The mod brings a shelf of voices with it, so there is something to choose from at once.

If anything is missing, voices simply stay quiet and one grey line tells you why. **A voice problem
never blocks, delays or loses a reply** — the words always arrive, whatever the sound is doing.

<details>
<summary><b>What that button actually fetches, and doing it by hand</b></summary>

Two things, and they are the same two Qwen-TTS Studio would have fetched for you:

- **The speech engine** — eight native libraries, taken out of the `windows-cuda-bundled` package
  on [Qwen-TTS Studio's own releases page](https://github.com/Danmoreng/qwen-tts-studio/releases),
  into `%LOCALAPPDATA%\Programs\qwen-tts-studio`. The Java application those libraries arrive
  inside is thrown away rather than written out — the mod has never needed Studio *installed*, and
  never launches it.
- **Two models**, from [Serveurperso/Qwen3-TTS-GGUF](https://huggingface.co/Serveurperso/Qwen3-TTS-GGUF)
  on Hugging Face, into `%USERPROFILE%\.qwen-tts-studio\models`. The talker turns text into audio
  tokens; the tokenizer turns those back into sound. It must be the **1.7b** talker — model size
  fixes the embedding dimension, and every voice the mod ships is d2048, which only the larger
  model produces.

If you would rather do it yourself — or the button is not offered because your machine is locked
down — [the manual road is here](voices-without-admin.md), and the mod finds the pieces either way.
Already have Studio installed with a model in it? Then there is nothing to fetch and the button
never appears.

</details>

### You do not have to cast everybody

Anyone you have not given a voice to yourself is handed one of **their own people and their own
sex** — so a Battanian woman sounds Battanian and an Aserai lord sounds Aserai, without you casting
five hundred souls one at a time. Each voice on the shelf says where it belongs:
`female/battania/Gwen`.

Three things worth knowing:

- **It never changes.** The choice is worked out from the person's own name, not rolled, so they
  sound the same next session and after any reload.
- **You always outrank it.** A voice you cast on someone by hand wins, always.
- **If their people have no voices yet**, one belonging to nobody in particular is used, and failing
  that, any voice of the right sex. Nobody falls silent for want of a match.
- **Only voices made on your machine** are given out this way. Hosted voices are billed by the
  minute, so one is never handed to anybody automatically — put one on somebody yourself and it
  speaks for them happily.
- **You get one too**, by the same rule, so your own lines can be read back. Cast yourself something
  else with **me**, or leave it.

Turn it off under **Voices → give everyone a voice of their own people** if you would rather cast
every soul yourself.

### Voices without cloning anything

**The mod brings a few voices with it.** They are put on your shelf the first time you run it, so
step 4 has something in it before you have made anything. From that moment they are *yours*: rename
them, give them to whoever you like, or delete the ones you have no use for — a voice you delete
stays deleted, and one you have edited is never written over when the mod updates. They are cloned
from freely-licensed audio, which is a real constraint on how good they can be; your own five
minutes with a clean clip will beat them.

Beyond those, the cloning model (`qwen-talker-1.7b-base`) carries **no ready-made voices at all** — it is a model
for making them, not for having them. If you want to hear something the moment you turn voices on,
also download **`qwen-talker-1.7b-customvoice`** in Studio and select it there: it carries nine
built-in speakers (Serena, Vivian, Sohee, Ono Anna, Aiden, Dylan, Eric, Ryan, Uncle Fu), and they
appear in the Voices panel by themselves when that model is the one loaded. They are serviceable
rather than good. One model is loaded at a time, so this is a choice between *ready-made* voices and
*your own* — and your own is the better road if you have five minutes.

### If a voice ever goes wrong

Speech models very occasionally miss their own ending and ramble. Three things guard against it, and
you only ever need the third:

- Every line is given an audio ceiling worked out from its own length, so a runaway is cut off in
  seconds rather than minutes, and the ruined clip is never kept.
- **Stop** appears in the talk screen's top bar whenever anything is speaking.
- **Backspace** silences a voice instantly, anywhere — on the map, in a battle, with every window
  shut. (Change it under Voices in the mod's settings.)

---

## Making a voice

Voices are cloned in **Qwen-TTS Studio** and then brought across. (Cloning from inside the game is
planned; until then this is the road — and it is the one most people will keep using anyway.)

**This is the one thing the download button does not give you.** It fetches the engine and the
models, which is everything needed to *play* with voices; Studio's own window is a separate program
and you only want it if you mean to *make* a voice. Get it from
[its releases page](https://github.com/Danmoreng/qwen-tts-studio/releases) — take the `.zip` of
`windows-cuda-bundled` and just unpack it, no installing and no administrator rights — and point it
at the models you already have, under `%USERPROFILE%\.qwen-tts-studio\models`.

### What you need

**A few seconds of clean speech** — one person, no music, no background noise, no second voice.
Ten to thirty seconds is plenty; more is not better. A `.wav` file.

**The clip decides everything.** Nine times out of ten, a clone that "sounds nothing like it" is a
clip with a soundtrack under it, two people talking over each other, or four seconds of material.

### In Qwen-TTS Studio

1. Load the **`qwen-talker-1.7b-base`** model. This is the one that clones — the `customvoice` model
   carries nine ready-made speakers instead and cannot learn a new voice.
2. Go to **Voices**.
3. Under **Create Speaker Preset**: give it a **Preset Name**, **Browse** to your `.wav`, and write a
   short **Reference Transcript** describing the voice. Optional, but it is what you will see later
   when choosing between six presets all called "Sibylla".
4. **Create Preset.** It will show `D1024 ready` / `D2048 ready`.
5. **Try it before you trust it** — go to **Studio**, pick the preset, speak a line. If it is not
   right, the fix is nearly always a better clip.

### Bringing it into the game

**Press "Bring over from Studio"** in the game's Voice panel and every preset you have made comes
across at once. That is the whole of it — the rest of this section is for anyone who would rather
move a voice by hand, or who wants to know what a voice actually is.

Studio keeps its voices under `C:\Users\<you>\.qwen-tts-studio\` :

```
.qwen-tts-studio\
    voice-presets.tsv                         the index - your preset names are in here
    embeddings\voice-<number>-d2048.json      THE VOICE ITSELF - this is the file you want
    icl-prompts\voice-<number>-d2048.json     a richer form, deliberately unused (see the note)
```

Open `voice-presets.tsv` in a text editor. Each line starts with the preset's id and its name, so you
can tell which `voice-<number>` is which.

Then make a folder for it under the game's config. The folder name is the voice's id — lowercase, no
spaces:

```
Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\Voices\
    sibylla\
        voice.json          you write this - see below
        embedding.json      a copy of embeddings\voice-<number>-d2048.json, RENAMED
        reference.wav       optional: the clip it came from, so it can be re-cloned or shared honestly
```

`voice.json` needs four things:

```json
{
  "Name": "Sibylla",
  "Gender": 1,
  "Dimension": 2048,
  "ReferenceText": "Warm young woman, American, carries a smile in it."
}
```

`Gender` is `1` female, `2` male, `0` neither. It is used **only** to decide who gets this voice by
default before you have assigned anyone by hand — put any voice on anybody you like.

Start the game, open the talk screen: it is in the list.

> **Use the `d2048` embedding, not the `icl-prompt`.** On the base model the ICL road returns a
> fraction of a second of audio about one time in five, and the failure looks like success. The mod
> prefers the embedding for exactly that reason.

### One rule, and it matters

**Clone only voices you have the right to use.** Your own voice, a friend's with their blessing, or
audio that is genuinely free to reuse. Not a celebrity, not an actor, not someone off YouTube — and
never a real person without their permission.

On your own PC, for yourself, this is your business and the mod will not stop you. But a voice folder
carries the original clip inside it, so the moment you **share** one you are handing someone else a
copy of a real person's voice. That is a different thing, and it can land on you.

## Sharing voices

A voice is **just a folder**:

```
Configs\ImmersiveAI\Voices\
    sibylla\
        voice.json          what it is called, and who it suits
        embedding.json      the voice itself
        icl-prompt.json     the voice itself, richer
        reference.wav       the clip it came from
```

Zip that folder, send it to someone, tell them to unzip it into their own `Voices\` folder. Done —
no settings to edit, no paths to fix. The **Open the voices folder** button in the Voice panel takes
you straight there.

---

## When it goes wrong

**She babbles, screeches, or will not stop.** Speech models occasionally lose their place partway
through a line. Press **Backspace** and everything stops instantly, wherever you are. You should
rarely need it: every line is given an audio ceiling worked out from its own length, so a runaway is
cut off after seconds instead of running on — and a line that had to be cut is never kept, so it
cannot come back the next time you scroll past those words.

**Nothing happens at all.** Open the Voices panel — it says in plain words what is missing, and if
anything is, the download button is right there. The mod also writes where it looked for the engine
into its log.

**I pressed "Download the voices" and nothing happened.** It works quietly, in the background,
while you carry on playing — there is no window and no progress bar outside the Voices panel, so
give it a minute and look there. If the panel still says nothing is installed, the fetch never
started: `log.txt` in `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\` says why in
plain words, and it is worth posting those lines in a bug report.

**There is no "Download the voices" button.** Three reasons, and the panel says which: everything is
already installed (nothing to fetch), or the machine has no NVIDIA card (see the top of this page —
the hosted road is the answer), or the mod's own voice program is missing from its folder, which
reinstalling Immersive AI puts back.

**The game stutters while she talks.** Speech and Bannerlord are sharing your graphics card. Try
the smaller model, or lower your graphics settings a notch.

**It sounds nothing like the clip.** Almost always the clip: background music, two people talking,
or too short. Try a cleaner ten seconds.

---

## Deeper: why it works this way

### The speech engine runs beside the game, not inside it

Voices are produced by a small separate program that Immersive AI starts and stops for you. This
looks like extra machinery, and it is deliberate.

The speech engine is a large AI model doing heavy work on your graphics card, at the same moment
Bannerlord is drawing a battle on the same card. When that kind of code runs out of memory it does
not politely return an error — it takes the whole process down with it. **Inside the game, that is
your campaign.** Beside the game, the very same failure is one line saying the voices have stopped,
while your battle carries on.

It also means you can never be left with speech quietly eating your graphics card after you quit:
the helper watches the game, and goes when it goes.

### Why the words are cut into sentences

A reply is spoken sentence by sentence rather than all at once. Two reasons: she starts talking
much sooner, because only the first sentence has to exist before sound comes out — and if the model
does derail, it costs one sentence rather than the whole answer.

### Gestures are never read aloud

When a character writes `*sets down her cup*`, that is something they **did**, not something they
said. Spoken aloud it would sound like stage directions. The mod strips those out and speaks only
the words — the gesture still appears in the conversation, as it always did.

---

## Turning it off

Set voices off in the options. Nothing else changes — every word still arrives as text, exactly as
it did before. Your voice folders stay where they are, so turning it back on costs nothing.

To reclaim the disk space, delete `%USERPROFILE%\.qwen-tts-studio\models` (2.2 GB) and
`%LOCALAPPDATA%\Programs\qwen-tts-studio` (662 MB) — the two folders the download button wrote, and
nothing else on your machine depends on either. To clear just the
generated speech, delete `Configs\ImmersiveAI\Voices\_cache\` — it is rebuilt as needed and safe to
remove at any time.
