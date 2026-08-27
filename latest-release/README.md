# Download Immersive AI

The current version is **v3.1.3**. Everything the mod needs is inside that one file.

---

## Install it in 4 steps

**1. Download**

Click **[ImmersiveAI_v3.1.3.zip](ImmersiveAI_v3.1.3.zip)** above, then press the **Download**
button on the page that opens. (It is 13 MB.)

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
    VoiceHost\
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

**My antivirus flagged something.**
The mod includes a small program (`VoiceHost\ImmersiveAI.VoiceHost.exe`) that speaks the
NPCs' lines out loud. It is only used if you turn voices on, and it is off by default.
If you would rather not have it, delete the whole `VoiceHost` folder — the mod works fine
without it, just silently. Source code for it is in this same repository, in
`src/ImmersiveAI.VoiceHost`.

**Do I need a good graphics card for the voices?**
For voices generated on your own machine, yes — an NVIDIA card. There is also a hosted
option that works on any machine. Either way, voices are **off** until you switch them on.

**Can I keep my old version?**
Yes. The previous release, `ImmersiveAI_v3.1.2.zip`, is kept in this folder as a fallback.
Only download it if v3.1.3 gives you trouble.

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
