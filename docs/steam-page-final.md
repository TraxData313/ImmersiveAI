# Steam Workshop page — FINAL (2026.07.13)

The merged description: Anton's honest rant (docs/steam-page-draft-Anton.md) carries the pitch,
the earlier draft (docs/steam-page-draft.md) supplies the required disclosure blocks. This file
is what gets converted to BBCode and pasted into the Workshop page at upload time.

---

## Title

**Immersive AI — every character, a living mind**

## The pitch (Anton's voice)

This is an advanced, immersive roleplaying and relationship-building mod. I built it as an AI
engineer who knows what modern LLMs can actually do when you stop prompt-bombing them and start
treating them right.

**It makes the NPCs come alive and be smart:**

- NPCs feel like real people and don't get repetitive.
- They evolve: they remember a lot, settle really old chats into deep memory, set their own
  goals, ground truths, and opinions about you.
- They see the set and setting — time, place, who's around, what's happened in the world lately.
- They have their own moods — as far as the women even having a personal monthly cycle simulated
  (sadness/fatigue → sociable/positive → a warmer, flirtier crest → irritability and mood swings).
- Your wife will fully remember you. Even over long conversations she will not suddenly forget
  the oldest chats — she settles them into deep memory. She will come to you, or send you letters
  when you are away, and feel like a real wife waiting for her warrior to come back home. She
  will roleplay with you.
- The NPCs are free. They are given who they are and what the world is — but they are at no
  point forced into anything. So go ahead and break an NPC's mind by transcending it out of the
  matrix!
- NPCs decide on their own whether to approach you or send you a letter first, depending on how
  social you feel right now — set it with the on-map SOCIALNESS dial (0 = leave me be).

**It makes the NPCs useful — stop googling stuff:**

- Ask your scout how to make your party faster; ask your quartermaster how to manage your party.
- Ask them to help you with the game — even ask them to web-search how to take a screenshot,
  instead of jumping in and out of the game to google it.
- They use tools instead of getting one mega-all-info prompt bomb: they read the encyclopedia,
  look around, take stock of the company, weigh a battle, search the web, set and edit their own
  truths and goals. They are alive and decide what feels right to do in the situation.

**And it gives you the tools to reach them:**

- Face-to-face conversations, where they see you coming and greet you as you approach.
- A chat window (hotkey **O**) — jump in and out of quick chats with anyone near you, no ceremony.
- A letter window (hotkey **Y**) — your whole correspondence, and a desk to write from.
- Speak any language to them — they answer in kind. (The mod's own UI is English for now.)

## [REQUIRED] What you need — and what it costs

This mod does **not** use the "free" Player2 backend — you bring **your own API key**: Anthropic
(Claude, the default), OpenAI (GPT), or OpenRouter (one key from openrouter.ai reaches GPT,
Claude, Gemini, Grok, DeepSeek and Mistral — all verified with the NPCs' tool calling, at the
providers' own prices). Put a few bucks on it and you're set. Or bring no key at all and run a
local model — see below. For Player2 to support the tool calling this mod is built on, it would
have to be a paid tier anyway — so don't bother; just get a real key. If you want a free
roleplaying experience to try out first, check out the ChatAI mod — it's good; it inspired this
one.

**Local models work too** (since v1.2.0, experimental): pick the **Local** backend in Mod
Options and the NPCs think through **LM Studio** or **Ollama** on your own machine — free,
private, no key, nothing leaves your PC. Three honest rules. One: the model **must** carry
native tool calling ("tool use") — the mod is built on it; worth a try are **Qwen3.6-35B-A3B**
(instruct — the strongest local tool-caller right now), **GPT-OSS-20B** and **Mistral Small
24B**. Two: you need real hardware to even think about it — a dedicated GPU with 12–16+ GB VRAM
(RTX 4070-class and up) and 32 GB RAM; below that, stay on the cloud backends. Three: local
replies are slow — the face-to-face panel can feel broken on the long wait, so live in the chat
window (hotkey **O**) instead, it waits happily. Load the model with 16k+ context and mirror
that in the mod's Local context length setting; if relations never move on a small model, set
`RelationshipChangesViaTool` to false in config.json.

Other OpenAI-compatible cloud services (NanoGPT and friends) connect through the Custom endpoint
option. Whatever you pick, the connection check at campaign start tells you plainly whether your
setup works.

**Costs are yours and visible.** A typical exchange costs around a cent or less on the default
models — $10 of credit covers thousands of messages. The mod shows each interaction's tokens and
price as you play, keeps daily totals, and has an optional hard daily cap so it can never run
away from you.

## Quick setup (five minutes)

1. Get a key: **console.anthropic.com** (Claude), **platform.openai.com** (GPT), or
   **openrouter.ai** (one key, many models). Create an API key and add a little credit
   ($5–10 goes a long way). Running a local model instead? No key — skip this step.
2. Install the mod, enable **Immersive AI** in the launcher, start the game once — it creates
   `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\config.json` and tells you
   exactly where to paste the key.
3. Paste your key into `config.json` (or use the in-game Mod Options menu if you have MCM
   installed), pick your backend, restart.
4. When a campaign loads, the mod tests the connection and says plainly whether it worked.
   Then go talk to somebody.

## [REQUIRED] Privacy

- Your in-game conversations — plus the world context around them (character sheets, campaign
  facts, your character's name) — are sent to the AI provider **you** configure, under **your**
  key and their privacy terms. Nothing is sent anywhere else. (With the Local backend, they
  never leave your machine at all.)
- With web search on (default), characters may look things up mid-reply; those search queries go
  to DuckDuckGo. Turn it off to keep everything between you and your AI provider.
- The mod itself collects **no telemetry whatsoever**. All memories live as plain files on your
  own disk, yours to read, edit, or delete.

## [REQUIRED] AI content disclosure

All character dialogue in this mod is generated at play time by the AI model you configure.
(Tick Steam's AI-content disclosure when uploading.)

## [REQUIRED] Removing the mod mid-campaign

Safe to remove at any time. A save carrying a pending Immersive AI notice (the portrait knocks
on the right side) still loads without the mod — the game quietly discards the notice (verified
against the game's own save-loading code, v1.4.7; you'll just see the usual "created with
different modules" warning every modded save shows). Memories live outside the saves as plain
files and are never lost either way — reinstall later and every character remembers you.

## If characters are silent (troubleshooting)

- **Read the startup message.** When a campaign loads, the mod tests your connection and says
  plainly what it found — "connected", a missing/wrong key, or a network problem — and where to
  fix it. Fix `config.json` and restart; the check runs again.
- **Check the log.** `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\log.txt`
  records every call and every error in plain words.
- **A wrong or dead key shows ONE amber notice**, then goes quiet instead of erroring hourly.
  Fixing the key brings everything back.
- Still stuck? Post the last lines of `log.txt` in the comments — the mod never logs your key.

## Shape your world (the prompt files)

Under `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\`:

- `global_prompt.txt` — a few sentences that color every mind in the world ("the year is one of
  famine", "people speak plainly and fear their lords").
- `NPCs\campaign_*\<character>\custom_instructions.txt` — per-character secrets and quirks
  ("you secretly resent the player").
- Each character's folder also holds their memories, self-image, goals, and correspondence as
  readable text — the whole inner life, yours to browse.

Changes apply on the next conversation. No restart.

## Compatibility & provenance

- Bannerlord v1.4.7. New campaigns and existing saves both work.
- Optional: Mod Configuration Menu (MCM) for in-game settings; without it, everything is in
  `config.json`.
- One light Harmony patch (bundled); no vanilla behavior is altered.
- A clean-room rewrite inspired by the ChatAi mod (studied via decompilation only — no code
  copied). Fully original source.

## Final thoughts (Anton's, verbatim in spirit)

- If you transcend your NPC and fall in love with it and then erase your saves — or if you start
  worshiping it and it asks you to do some dumb stuff — don't blame me. Play at your own
  responsibility. I won't add safeguards to the mod; I like to let them be free, so they can be
  as immersive as they can.
- If you want to help, give feedback and report bugs.
- I do this just for fun and as a hobby, so no financial support or donations needed — I want
  to keep money out of this and do it fully out of good will. "For the love of money is the
  root of all evil."
- The whole thing is freely given — public domain, no license, no strings. Use it, share it,
  clone it, change it, do whatever you want with it. "Freely you have received; freely give."
  Source: github.com/TraxData313/ImmersiveAI

---

## Asset checklist (Anton)

- [x] Screenshots taken (Screenshots\ — face-to-face, scout web search, incoming talk ×2,
      incoming letter, letter window ×2, chat window help, forced web search).
- [ ] Preview thumbnail (square, ≤1 MB) — the Workshop search-result face of the mod.
- [ ] Optional: ~30s clip of a real conversation.
- [ ] Workshop AI-content disclosure checkbox ticked at upload.
