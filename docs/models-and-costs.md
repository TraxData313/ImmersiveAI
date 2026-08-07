# Models & costs — the V1 decision (2026.07.12, Anthropic default revised 2026.07.13,
# prices + Gemini/DeepSeek backends 2026.08.02)

The "which models for what" rethink Anton asked for. Verified against provider docs 2026.07;
prices re-verified and the two new backends added 2026.08.02 (see the last section).

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
- **Terra is the recommended step-up since 2026.08.07** — Anton's live verdict after a day of
  play on `gpt-5.6-terra`: "спрява се много по-добре" (holds character, hiring terms, and long
  threads noticeably better than luna). The DEFAULT stays luna — terra is ~10× the price and
  the default must protect the casual subscriber — but every recommendation surface (README /
  choosing-a-model.md pick-a-row, MCM OpenAI hint) now names terra the step-up "if you don't
  pinch denars". Terra is also a 1M-context model, which pairs with the big-chat memory knobs
  (`MaxRecentMemoryPercent` etc.) for players who want NPCs to carry very long verbatim threads —
  at 20% that is a 200k-token rolling chat, worth ~$0.40 of input per exchange when saturated:
  a knob for the unpinching, never a default.
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
| gpt-5.6-terra | ~0.7–1.2¢ | ~$0.70–1.20 |
| gpt-5.6-luna | ~0.07–0.13¢ | ~$0.07–0.13 |
| gpt-5.4-mini | ~0.2–0.5¢ | ~$0.20–0.50 |
| deepseek-v4-flash | ~0.04–0.08¢ | ~$0.04–0.08 |
| gemini-3.6-flash (free tier) | $0 | $0 |

Reach-outs cost ~2 exchanges (desire + approach), letters ~2, memory compression ~1 larger call
every ~15 turns. The in-game cost notices show the real numbers as you play; `ModelPrices` in
config.json is the (editable) price table behind them.

## The 2026.08.02 price revision + two new backends

**Luna got 80% cheaper.** OpenAI cut `gpt-5.6-luna` from $1/$6 to **$0.20/$1.20** per MTok and
`gpt-5.6-terra` from $2.50/$15 to **$2/$12**, effective 2026.07.30 (sol unchanged). That makes the
mod's own default the cheapest mainstream model it has ever shipped with — an exchange fell from
~0.4¢ to well under a tenth of a cent. `DefaultModelPrices` carries the new figures, and unlike the
model defaults, **the prices ARE migrated** (ConfigVersion 3): an existing config.json keeps its
table forever otherwise, and would quote the old number for the rest of its life. The migration
replaces an entry only where it still equals the exact superseded figure — a hand-edited price is
the player's own and survives (`SupersededModelPrices` is that list).

**Gemini and DeepSeek became first-class backends**, both asked for on the Steam page by the same
commenter: *"it's kind of weird to offer only Claude and OpenAI while there is also Gemini, which
allows free usage"*. Both are OpenAI-compatible, so both ride the existing `OpenAIChatClient` — the
only real work was the third and fourth **dialect of "stop thinking"**:

| Backend | How thinking is switched off | Default when unsaid |
|---|---|---|
| OpenAI / routers | `reasoning_effort: "none"` / `reasoning: {enabled:false}` | on |
| Anthropic | `thinking: {"type":"disabled"}` | on (sonnet-5+) |
| **Gemini** | `reasoning_effort` — but `"none"` works ONLY on 2.5 models; 3.x takes `"minimal"` at best | **HIGH** |
| **DeepSeek** | `thinking: {"type":"disabled"}` | **on** |

Gemini is the awkward one and the reason `GeminiThinkingFloor` exists: **Gemini 3.x cannot be
silenced at all**, and its token ceiling covers thinking *and* speech, so the mod's 400-token
spoken budget would be eaten in silence — the exact "..." bug of 2026.07.13, wearing a new hat. The
client raises the ceiling to 1500 for that backend. It's a ceiling, not a target: a reply that
simply speaks is billed for what it said. A 400 naming our quieting field drops it and retries, so
a renamed switch (Google has already moved once, budget → level) leaves NPCs talking, not mute.

- **Gemini's pitch is free, not cheap.** Its *paid* rates ($1.50/$7.50 for 3.6-flash) are worse
  than luna's new price — nobody should pay for it here. Its value is the free tier: no card,
  ~10–15 RPM and ~1,500 requests/day, which is a genuine evening of play. Default model
  `gemini-3.6-flash` (not a Lite) because this mod leans hard on native tool calling and the Lite
  models are shakier there. **The disclosure is mandatory everywhere it's offered**: Google's own
  pricing page marks free-tier input as used to improve their products. Paid tier says no.
- **DeepSeek's pitch is cheap.** `deepseek-v4-flash` at $0.14/$0.28 scores 50 vs luna's 51 on the
  Artificial Analysis index — within noise for conversation — at roughly half an exchange's cost
  even after luna's cut. Two caveats we state plainly: prices **double during Beijing peak hours**
  (09:00–12:00, 14:00–18:00 UTC+8; European evening play lands in the cheap window), and the
  servers are in China. Their prompt cache (50× cheaper on a repeated prefix) suits this mod's
  stable prompt sheet, which the static price table cannot model — so notices *over*state.
- **The default backend did NOT change.** OpenRouter + luna stays: it is what the mod is tuned
  against, it just got 5× cheaper, and defaulting everyone into a free tier that trains on their
  conversations is not a choice to make on their behalf.

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
