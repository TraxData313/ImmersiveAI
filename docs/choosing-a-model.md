# Which AI should I use?

Immersive AI runs on **your own API key**. This page helps you pick one. It gets more detailed
as you scroll — **read only as far as you need.**

---

## Just tell me what to pick

| If you want… | Pick | Key from |
|---|---|---|
| **It to just work** | OpenRouter + `openai/gpt-5.6-luna` — the default | openrouter.ai |
| **To pay nothing** | Gemini + `gemini-3.6-flash` — real free tier, no card | aistudio.google.com |
| **The lowest bill** | DeepSeek + `deepseek-v4-flash` | platform.deepseek.com |
| **The best writing** | OpenRouter + `openai/gpt-5.6-terra` or `anthropic/claude-sonnet-5` | openrouter.ai |
| **Nothing to leave your PC** | Local (LM Studio / Ollama) — [see below](#local-models-tinkerers-only) | — |

**Not sure? Take the first row.** It's what the mod is built and tested against, and since
OpenAI cut its price 80% on 30 July 2026 it's also one of the cheapest options there is.

---

## What it costs

| Model | Per exchange | $10 lasts about |
|---|---|---|
| **Gemini free tier** | **$0** | forever — ~1,500 replies/day |
| `deepseek-v4-flash` | ~0.05¢ | ~20,000 exchanges |
| `gpt-5.6-luna` *(default)* | ~0.1¢ | ~10,000 exchanges |
| `gpt-5.4-mini` | ~0.35¢ | ~3,000 exchanges |
| `claude-haiku-4.5` | ~0.4¢ | ~2,500 exchanges |
| `gpt-5.6-terra` | ~0.9¢ | ~1,100 exchanges |
| `claude-sonnet-5` | ~1.3¢ | ~750 exchanges |

Rough figures. Reach-outs, letters and memory upkeep make their own calls behind the scenes, so
real use runs somewhat higher — **the in-game cost notice shows you the truth as you play**, keeps
daily totals, and `MaxDailyRequests` sets a hard cap you cannot outrun.

---

## The catch with each one

Every option has exactly one thing worth knowing before you commit.

| Backend | The catch |
|---|---|
| **OpenRouter** | None, really. One key reaches every model. Avoid the `:free` models — they're rate-limited and congested. |
| **OpenAI** | None. Same models as above, going direct. |
| **Gemini** | **Google trains on free-tier traffic.** Their own pricing page says free-tier content is used to improve their products. Paying moves the same key to the paid tier, where they say they don't. Also: its *paid* rates are worse than luna's, so only use it free. |
| **DeepSeek** | Prices **double during Beijing peak hours** (09:00–12:00 and 14:00–18:00 UTC+8 — European evenings fall in the cheap window). Servers are in China. |
| **Anthropic** | Works fine, just less tested here. Pricier per word than the rest. |
| **Local** | It's a project, not a setting. [See below.](#local-models-tinkerers-only) |

### Is the free Gemini tier actually good enough?

Yes — `gemini-3.6-flash` is a genuinely capable model and it carries the tools the NPCs need.
The limits are ~10–15 requests/minute and ~1,500/day, which is a long evening of play.

The reason it isn't the mod's default is the training clause, not the quality. **If you'd rather
not have your roleplay read, don't take the free tier** — luna costs about a tenth of a cent per
exchange, and $5 will outlast your campaign.

---

## Switching later

Nothing is locked in. Change the backend or model in the **Mod Options** menu (with MCM
installed) or in `config.json`, and it takes hold **on the very next reply — no restart.** Your
NPCs and all their memories are untouched by a model change.

If a key or model is wrong, the startup check says so plainly and tells you where to fix it.

---

## Deeper: why these models?

Three things decide whether a model can carry this mod. If you want to run something not on the
list, judge it by these.

### 1. It must have native tool calling — this is not optional

The NPCs don't get one giant info-dump prompt. They **reach for tools** mid-thought: look up a
person in the encyclopedia, survey the country around them, read the muster roll, weigh a battle,
check the market ledger, search the web, set down a lasting truth, move their own regard for you.

A model without native tool calling still talks, but it goes noticeably duller — it can't look
anything up, so it invents. **Any mainstream cloud model from 2025 onward has this.** Small local
models are where it gets shaky.

### 2. Its thinking must be switchable off

Reasoning/thinking models spend tokens silently before they speak. With a ~400-token reply budget
that means the NPC thinks its whole allowance away and answers `...`. The mod therefore **turns
thinking off on every backend**, and each provider spells that differently:

| Provider | How it's silenced | Thinks by default? |
|---|---|---|
| OpenAI / OpenRouter | `reasoning_effort: "none"` | yes |
| Anthropic | `thinking: disabled` | yes |
| DeepSeek | `thinking: disabled` | yes |
| **Gemini** | **can't be, on 3.x — only turned down** | **yes, at maximum** |

Gemini is the awkward one: its 3.x models cannot be silenced at all, and their token ceiling
covers thinking *and* speech. The mod handles this by quietly widening Gemini's budget so there's
always room left to actually say something. You don't have to do anything — but it's why Gemini
replies cost a little more thought than the number suggests.

### 3. Context window

The mod scales each NPC's verbatim memory against the model's context window, so a bigger window
means more remembered word-for-word before it gets folded into deep memory. Every recommended
model has 200k–1M, which is far more than enough. If you use something unusual, add it to
`ModelContextWindows` in `config.json`.

---

## Local models: tinkerers only

The **Local** backend (LM Studio, Ollama) exists because people asked, not because it plays well.
Nothing leaves your machine and it costs nothing — but you own the setup entirely. **I don't debug
local setups.**

**What it needs:**

| | Requirement |
|---|---|
| Model | An **instruct** model with **native tool calling** — Qwen3.6-35B-A3B instruct, GPT-OSS-20B, Mistral Small 24B |
| Thinking | **OFF in your server.** A thinking model burns its budget in silence and answers `...` |
| Context | 16k+, and mirror that number in the mod's **Local context length** setting |
| Reply tokens | 400+ |
| Hardware | 12–16+ GB VRAM, 32 GB RAM |

**Set the context length honestly.** The mod budgets memory against what you tell it. Claim more
than your server actually loaded and you get silent truncation and strange amnesia.

**Expect slowness.** Replies take real seconds to minutes, so live in the chat window (hotkey `O`)
rather than face-to-face dialogue. The mod already gives local backends far wider timeouts.

**If hearts never move**, your model is going shy of the tool calls. Set
`RelationshipChangesViaTool` to `false` in `config.json` — that falls back to asking the NPC how
they felt in a separate, simpler call.

---

## Other services

Any **OpenAI-compatible** service works through the **Custom endpoint** field: paste its base URL
ending in `/v1`, put its key in the OpenAI key field, and type its model id in the custom OpenAI
model field. NanoGPT and friends connect this way.

Whatever you pick, the connection check when a campaign loads tells you plainly whether it works.

---

*Prices and model names verified 2 August 2026. Providers change both — `ModelPrices` and
`ModelContextWindows` in `config.json` are yours to edit, and the mod re-reads them on load.*
