# Models & costs — the V1 decision (2026.07.12)

The "which models for what" rethink Anton asked for. Verified against provider docs 2026.07.

## The decision

**One model per backend for everything** (spoken replies, memory writes, utility calls), chosen
for conversation quality + tool reliability + price. Splitting utility calls (feeling number,
desire yes/no, search refining) onto a cheaper model is a real ~30–40% saving but adds a second
client, a second failure mode, and a second personality — postponed to post-V1 (task noted).

| Role | Anthropic (default backend) | OpenAI |
|---|---|---|
| Everything | `claude-opus-4-8` — $5/$25 per MTok | `gpt-5.6-terra` — $2.50/$15 per MTok |

- **Anthropic default stays `claude-opus-4-8`.** Best-in-class roleplay voice and the most
  reliable native tool calling we've seen (the recalls, move_heart). 200k context is plenty
  (the whole prompt sheet runs a few thousand tokens). Budget alternative: `claude-sonnet-5`
  at $3/$15 — strong, noticeably cheaper; worth offering in the Steam FAQ.
- **OpenAI default moves `gpt-4o` → `gpt-5.6-terra`** (Anton's instinct, confirmed): Terra is
  OpenAI's current mid-tier — GPT-5.5-level quality at half the price, 1M context, and a real
  reasoning dial. gpt-4o is two generations old, was persistently shy with tools (the whole
  move_heart saga), and at $2.50/$10 is barely cheaper than Terra. Sol ($5/$30) is overkill for
  NPC chat; Luna ($1/$6) is the budget pick to mention on the page.
- **`OpenAIReasoningEffort` = `low`** (new config): reasoning tokens are billed output, so
  conversation wants little of it — but a little makes tool use far more reliable than none.
  Players can set `none` for the cheapest/fastest replies or higher for cleverer companions.

## What a session costs (for the Steam page)

A typical exchange sends ~2–4k tokens of context and gets ~150–400 back.

| Model | Per exchange (approx.) | 100 exchanges |
|---|---|---|
| claude-opus-4-8 | ~1.5–3¢ | ~$1.50–3 |
| claude-sonnet-5 | ~1–2¢ | ~$1–2 |
| gpt-5.6-terra | ~0.8–1.5¢ | ~$0.80–1.50 |
| gpt-5.6-luna | ~0.3–0.6¢ | ~$0.30–0.60 |

Reach-outs cost ~2 exchanges (desire + approach), letters ~2, memory compression ~1 larger call
every ~15 turns. The in-game cost notices show the real numbers as you play; `ModelPrices` in
config.json is the (editable) price table behind them.

## Code facts that ride with this (shipped 2026.07.12)

- The OpenAI client sends `max_completion_tokens` (not `max_tokens`) and `reasoning_effort` for
  gpt-5.x / o-series ids — without this, gpt-5.6 requests are a hard 400. Older ids keep the
  classic `max_tokens` shape, so gpt-4o configs keep working untouched.
- **Existing configs are NOT auto-migrated**: a config.json that says `gpt-4o` keeps meaning
  gpt-4o. The new default only reaches fresh installs. (Deliberate — a model swap changes real
  money and voice; that choice stays with the player.)
- `ModelContextWindows` and `ModelPrices` both know the gpt-5.6 tier and current Claude models;
  unknown models still work — they just show tokens without a price.

## Post-V1 idea (parked)

A `UtilityModel` per backend (e.g. `gpt-5.6-luna` / `claude-haiku-4-5`) for the feeling number,
desire yes/no, and search refining — cuts ~a third of cost at some added complexity. Revisit
after V1 telemetry (the ledger now measures exactly how much those calls cost).
