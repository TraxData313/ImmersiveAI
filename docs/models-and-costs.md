# Models & costs — the V1 decision (2026.07.12, Anthropic default revised 2026.07.13)

The "which models for what" rethink Anton asked for. Verified against provider docs 2026.07.

## The decision

**One model per backend for everything** (spoken replies, memory writes, utility calls), chosen
for conversation quality + tool reliability + price. Splitting utility calls (feeling number,
desire yes/no, search refining) onto a cheaper model is a real ~30–40% saving but adds a second
client, a second failure mode, and a second personality — postponed to post-V1 (task noted).

| Role | Anthropic (default backend) | OpenAI |
|---|---|---|
| Everything | `claude-haiku-4-5` — $1/$5 per MTok | `gpt-5.4-mini` — $0.75/$4.50 per MTok |

- **Anthropic default is `claude-haiku-4-5`** (2026.07.13, revised from opus-4-8 after Anton's
  first live Anthropic session: Opus ran ~3¢ per exchange where gpt-5.4-mini ran under 1¢ and
  played just as well — the default should match the tier a subscriber actually needs, and both
  backends' defaults should sit in the same price class). Haiku 4.5 is Anthropic's small-fast
  tier with the same reliable native tool calling; 200k context is plenty (the whole prompt
  sheet runs a few thousand tokens). The MCM dropdown offers the ladder: haiku-4-5 (default),
  sonnet-5 ($3/$15, the strong step-up), opus-4-8 ($5/$25, most capable — the old default,
  still one click away), fable-5 ($10/$50, the frontier flagship for whoever wants it).
- **OpenAI default is `gpt-5.4-mini`** (2026.07.12, Anton's third same-day revision, settled
  by live play: terra → luna → 5.4-mini). Luna played well but fresh accounts saw hours of
  flickering 401 "insufficient permissions" while OpenAI's access grant propagated; 5.4-mini
  ran hiccup-free at 3/4 the price, so the proven small model wins the default. The MCM
  dropdown offers 5.4-mini, luna, terra, sol, gpt-5.5 ($5/$30, the previous flagship — it has
  NO mini/nano siblings, the small tier jumped 5.4 → 5.6), gpt-5.4, gpt-5.4-nano. gpt-4o and
  kin are markedly worse with the NPCs (the whole move_heart saga) and live on solely as hand
  edits in config.json (a hand-set model still appears and works, via the bridge's SelectOrAdd).
- **Reasoning is OFF everywhere, hardcoded** (2026.07.13, revised from `OpenAIReasoningEffort:
  low` after Opus NPCs answered "..." — silent thinking spends billed tokens against the spoken
  budget and slows every reply). The clients enforce it themselves: OpenAI sends
  `reasoning_effort: "none"`, Anthropic sends `thinking: {"type":"disabled"}` explicitly
  (sonnet-5 thinks by default when the field is omitted; fable/mythos are the exception — always
  thinking, explicit disabled is a 400, so the field stays omitted there). The config knob and
  the MCM dial are gone; an old `OpenAIReasoningEffort` key in config.json is ignored on load.

## What a session costs (for the Steam page)

A typical exchange sends ~2–4k tokens of context and gets ~150–400 back.

| Model | Per exchange (approx.) | 100 exchanges |
|---|---|---|
| claude-opus-4-8 | ~1.5–3¢ | ~$1.50–3 |
| claude-sonnet-5 | ~1–2¢ | ~$1–2 |
| claude-haiku-4-5 | ~0.3–0.6¢ | ~$0.30–0.60 |
| gpt-5.6-terra | ~0.8–1.5¢ | ~$0.80–1.50 |
| gpt-5.6-luna | ~0.3–0.6¢ | ~$0.30–0.60 |
| gpt-5.4-mini | ~0.2–0.5¢ | ~$0.20–0.50 |

Reach-outs cost ~2 exchanges (desire + approach), letters ~2, memory compression ~1 larger call
every ~15 turns. The in-game cost notices show the real numbers as you play; `ModelPrices` in
config.json is the (editable) price table behind them.

## Code facts that ride with this (shipped 2026.07.12)

- The OpenAI client sends `max_completion_tokens` (not `max_tokens`) and `reasoning_effort: "none"`
  for gpt-5.x / o-series ids — without the token-param swap, gpt-5.6 requests are a hard 400. Older
  ids keep the classic `max_tokens` shape, so gpt-4o configs keep working untouched.
- **Existing configs are NOT auto-migrated**: a config.json that says `gpt-4o` keeps meaning
  gpt-4o. The new default only reaches fresh installs. (Deliberate — a model swap changes real
  money and voice; that choice stays with the player.)
- `ModelContextWindows` and `ModelPrices` both know the gpt-5.6 tier and current Claude models;
  unknown models still work — they just show tokens without a price.

## Post-V1 idea (parked)

A `UtilityModel` per backend (e.g. `gpt-5.6-luna` / `claude-haiku-4-5`) for the feeling number,
desire yes/no, and search refining — cuts ~a third of cost at some added complexity. Revisit
after V1 telemetry (the ledger now measures exactly how much those calls cost).
