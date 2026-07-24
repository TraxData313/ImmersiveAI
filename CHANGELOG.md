# Changelog

The player-facing history of Immersive AI. The version lives in `module\SubModule.xml`
(`package.ps1` stamps the zip from it). For each release: bump the version there, add a
section here, copy its text into `tools\WorkshopUpdate.xml` (`ChangeNotes`) for the
Workshop and into the Nexus changelog field when uploading the new file.

## v1.3.1 — 2026.07.24

- **Type any AI model id in Mod Options** (asked for on Nexus): each cloud backend's model
  dropdown now has a "(type any id)" field right below it — while it holds text it overrides
  the dropdown; clear it and the dropdown chooses again. Use any Anthropic or OpenAI id, or
  anything from OpenRouter's full catalog, pasted exactly as openrouter.ai/models spells it.
  Takes effect on the very next reply, no restart. Unlisted models work fine — the cost
  estimate just may not know their prices, and a mistyped id tells you so plainly.

## v1.3.0 — 2026.07.22

- **Changing AI settings no longer needs a restart.** Backend, API keys, models, endpoints and
  reply length all take effect on the very next reply — swap gpt-5.4-mini for Claude mid-game
  and a soft "now speaking with…" notice confirms the change took hold. Every Connection
  setting in Mod Options is now live.
- **Letters now arrive like chats do**: a persistent portrait notice in the map's right-side
  stack ("A letter has come"), and clicking it opens the **letter window** on the writer's
  thread — read and answer where the whole correspondence lives, instead of a popup blocking
  the map. Dismissing the notice loses nothing; the letter waits in the window (hotkey Y).
- **Scouts finally see hideouts**: ask a party member what's around and the survey now lists
  the dens of brigands your company has spotted — named by their band ("a den of Sea
  Raiders"), with lurker counts as sharp as the scout's own eyes. They can also weigh a raid
  on a den for you ("could we take that hideout?"), same as against a warband or walls.
  Unspotted dens stay honestly unknown — no map-cheating oracle.
- **New Mod Options toggle to hide the on-map socialness stepper** (asked for on Steam) — the
  little control folds away or returns the moment you tick the box, restart-free. The
  Socialness slider in Mod Options still sets the pace while it is hidden.

## v1.2.0 — 2026.07.17

- **Local models are now a built-in backend** (asked for by testers): pick **Local** in Mod
  Options and the NPCs think through **LM Studio** (the default, localhost:1234) or **Ollama**
  (paste `http://localhost:11434/v1`) running on your own machine — free, private, no API key,
  nothing leaves your PC. Set the exact model id your server serves and the context length you
  loaded it with; the connection check at campaign start tells you plainly whether it worked
  (including "is your local server running?" when it isn't).
- Honest expectations for local: the model must carry native tool calling (worth a try:
  Qwen3.6-35B-A3B instruct, GPT-OSS-20B, Mistral Small 24B), you want a 12–16+ GB VRAM GPU and
  32 GB RAM, and replies are slower — the chat window (hotkey O) handles the wait far better
  than the face-to-face panel. If relations never move on a small model, set
  `RelationshipChangesViaTool` to false.
- Local time runs slower, and the mod now knows it: local requests get up to 5 minutes (cloud
  keeps its 90 seconds), the connection check gives a still-loading model 3 minutes, and the
  autonomous flows' watchdogs breathe wider so a slow local reply is never mistaken for a lost
  one. Leaked `<think>` blocks are stripped from local replies, and a model that thought without
  ever speaking is called out in log.txt with the fix (turn thinking off / use an instruct build).
- Existing setups are untouched — nothing changes unless you pick the Local backend.

## v1.1.0 — 2026.07.16

- **OpenRouter is now a built-in backend** (the most-requested feature after release): pick
  **OpenRouter** in Mod Options, paste one key from openrouter.ai, and choose a model from the
  dropdown — **GPT, Claude, Gemini, Grok, DeepSeek and Mistral** all verified working with the
  NPCs' native tool calling (recall, feelings, goals), at the providers' own prices.
  `openai/gpt-5.4-mini` and `anthropic/claude-haiku-4.5` are the proven picks;
  `deepseek/deepseek-v4-flash` is the cheapest of all ($0.10/$0.20 per million tokens). Any
  other id from openrouter.ai/models set in config.json appears in the dropdown too. Models
  that refuse to run with their thinking turned off (fable, grok, gemini-3.5) are handled
  automatically — the mod retries and lets them think.
- **Custom endpoint support** for everything else: the OpenAI backend can point at any
  OpenAI-compatible service — set **Custom endpoint** in Mod Options (or `OpenAIBaseUrl` in
  config.json) to the service's base URL ending in `/v1`. Covers NanoGPT and local servers
  (Ollama / LM Studio — at your own risk; small local models are often shaky with the mod's
  tool calling).
- The connection check at campaign start names the service it reached ("connected to
  OpenRouter · …"), so you know at once whether your setup works.
- MCM hint texts shortened so they no longer overflow the tooltip box.
- Existing setups are untouched — nothing changes unless you pick a new backend or endpoint.

## v1.0.0 — 2026.07.15

- First public release (Steam Workshop + Nexus Mods).
- Letter window key moved from **U** to **Y** (War Sails uses U for the ship manager at sea).
  Configs still on the old default switch automatically; a hand-picked key is left untouched.
