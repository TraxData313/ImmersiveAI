# Download Immersive AI

The current version is **v3.4.0**. Everything the mod needs is inside that one file.

---

## Install it in 4 steps

**1. Download**

Click **[ImmersiveAI_v3.4.0.zip](ImmersiveAI_v3.4.0.zip)** above, then press the **Download**
button on the page that opens. (It is 48 MB — most of it is the voices of Calradia.)

**2. Find your Bannerlord `Modules` folder**

It is here:

```
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules
```

Quick way to get there: in Steam, right-click **Mount & Blade II: Bannerlord** →
**Manage** → **Browse local files**, then open the `Modules` folder.

**3. Unzip it into that folder**

Right-click the downloaded file → **Extract All…** → choose the `Modules` folder.

When it is done you should have this — one folder called `ImmersiveAI`, with a
`SubModule.xml` file sitting directly inside it:

```
Modules\
  ImmersiveAI\
    SubModule.xml
    bin\
    GUI\
    Voices\
```

> **If it does not look like that**, you probably have `Modules\ImmersiveAI\ImmersiveAI\…`
> — a folder inside a folder. Just move the inner `ImmersiveAI` folder up one level and
> delete the empty outer one.

**4. Turn it on**

Start the Bannerlord launcher, open the **Mods** tab, tick **Immersive AI**, and play.

---

## One more thing: your API key

The mod talks to an AI service, so it needs a key of your own. Start the game once and it
will tell you where to put it. The short version:

1. Get a key from [openrouter.ai](https://openrouter.ai) (works with everything, one key).
2. Start a campaign — a message tells you the exact file to edit.
3. Paste the key in, save, restart the game.

The full guide, including free options and what each one costs, is here:
**[Which AI should I use?](../docs/choosing-a-model.md)**

---

## Questions you might have

**Do I need to install anything else first?**
No. No .NET, no separate downloads, nothing. This one file is everything.

**Is there a program inside?**
No. The download holds no executable at all — only the mod's library, its screens and the
voice files.

**How do I hear the voices? Do I need a good graphics card?**
Voices are **off** until you switch them on. In the game, open the talk screen (**O**) →
**Voices**, pick an engine and press **Install it** — it installs
[claude-voice](https://github.com/TraxData313/claude-voice), a free app that speaks on your own
PC. Qwen reads every language (NVIDIA, 4 GB), Breeze laughs and whispers (English, NVIDIA
16 GB), Pocket runs on any PC. More: [Hearing them speak](../docs/voiceover-setup.md).

**Can I keep my old version?**
Yes. The previous release, `ImmersiveAI_v3.3.0.zip`, is kept in this folder as a fallback.
Only download it if v3.4.0 gives you trouble.

**Updating from an older version?**
Delete the old `Modules\ImmersiveAI` folder first, then unzip the new one. Your settings
and all your NPCs' memories live somewhere else entirely and are never touched:
`Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI`.

**Something is broken.**
Open an [issue](https://github.com/TraxData313/ImmersiveAI/issues) and say what happened.
There is a log at `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\log.txt`
that usually explains it.

---

*What changed in this version: see the [changelog](../CHANGELOG.md).*
