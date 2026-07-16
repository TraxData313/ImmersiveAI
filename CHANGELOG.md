# Changelog

The player-facing history of Immersive AI. The version lives in `module\SubModule.xml`
(`package.ps1` stamps the zip from it). For each release: bump the version there, add a
section here, copy its text into `tools\WorkshopUpdate.xml` (`ChangeNotes`) for the
Workshop and into the Nexus changelog field when uploading the new file.

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
