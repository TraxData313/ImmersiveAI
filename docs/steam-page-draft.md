# Steam Workshop page — draft (2026.07.12)

Working draft for the Workshop description. Anton owns the final wording and the assets
(screenshots, clip). Sections marked [REQUIRED] carry disclosures we decided must ship.

---

## Title

**Immersive AI — every character, a living mind**

## Short description

Talk to anyone in Calradia — really talk. Every hero answers through a modern AI with their own
personality, their own memories of you, their own goals and moods. They remember every meeting,
write you letters from across the map, seek you out when the bond is real, and know their own
world: their family, their company, their trade, the war, the road.

## The long pitch

- **Real conversations.** Speak freely with any hero — in the dialog screen, or through a quick
  chat window (hotkey O) with no ceremony at all. They answer as themselves: a king like a king,
  a surgeon like a surgeon, your wife like your wife.
- **Real memory.** Every exchange is remembered. Old talks compress into a rolling memory and
  lasting truths each character chooses to keep. Reload a save, and their memories rewind with it.
- **They know their world.** Live campaign facts (who holds what town, who's at war), their own
  warband and its ledger, their family, their trade, the latest tidings and town rumors. A scout
  can honestly answer "can we outrun them?"; a merchant quotes the town's real prices.
- **They act.** Characters seek you out when the bond moves them (tune it with the on-map
  SOCIALNESS dial, 0 = leave me be). Distant friends — and your own caravans and governors —
  write letters that travel real in-game days. A letter window (hotkey U) keeps every
  correspondence.
- **Their hearts move.** Each exchange can shift how they regard you — the real game relation,
  set by them, in character.
- **Yours to shape.** A global prompt file colors the whole world; each character has their own
  instruction file. Plain text, no restart needed.
- Speak any language to them — they answer in kind. (The mod's own UI is English for now.)

## [REQUIRED] What you need — and what it costs

This mod speaks through an AI service using **your own API key** (Anthropic by default, or
OpenAI). Keys come from console.anthropic.com or platform.openai.com; the mod shows you exactly
where to paste yours on first run.

**Costs are yours and visible.** The service bills by use. A typical exchange costs a fraction of
a cent to a few cents depending on the model; an evening of heavy talking is usually well under a
dollar on the default models. The mod shows each interaction's tokens and price as you play
(`ShowCostNotices`), keeps daily totals, and has an optional hard daily cap (`MaxDailyRequests`).

## [REQUIRED] Privacy

- Your in-game conversations — plus the world context around them (character sheets, campaign
  facts, your character's name) — are sent to the AI provider **you** configure, under **your**
  key and their privacy terms. Nothing is sent anywhere else.
- With `EnableWebSearch` on (default), characters may look things up mid-reply; those search
  queries go to DuckDuckGo. Turn it off to keep everything between you and your AI provider.
- The mod itself collects **no telemetry whatsoever**. All memories live as plain files on your
  own disk, yours to read, edit, or delete.

## [REQUIRED] AI content disclosure

All character dialogue in this mod is generated at play time by the AI model you configure.
(Tick Steam's AI-content disclosure when uploading.)

## [REQUIRED] Removing the mod mid-campaign

Best done at a quiet moment: dismiss any Immersive AI map notices (the portrait knocks on the
right side) and save before disabling the mod — a save carrying one of our notices references
the mod's classes. Memories live outside the saves and are never lost either way.
[VERIFY before release: load a notice-carrying save without the mod and record what happens.]

## Provenance

A clean-room rewrite inspired by the ChatAi mod (studied via decompilation only — no code
copied). Fully original source.

## Compatibility

- Bannerlord [VERSION]. New campaigns and existing saves both work.
- Optional: Mod Configuration Menu (MCM) for in-game settings; without it, everything is in
  `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\config.json`.
- One light Harmony patch (bundled); no vanilla behavior is altered.

## Shape your world (the prompt files)

Under `Documents\Mount and Blade II Bannerlord\Configs\ImmersiveAI\`:

- `global_prompt.txt` — a few sentences that color every mind in the world ("the year is one of
  famine", "people speak plainly and fear their lords"). Ships with commented examples.
- `NPCs\campaign_*\<character>\custom_instructions.txt` — per-character secrets and quirks
  ("you secretly resent the player").
- Each character's folder also holds their memories, self-image, goals, and correspondence as
  readable text — the whole inner life, yours to browse.

Changes apply on the next conversation. No restart.

---

## Asset checklist (Anton)

- [ ] 3–4 screenshots: chat window mid-conversation, letter window, a reach-out notice, the
      socialness stepper.
- [ ] ~30s clip of a real conversation.
- [ ] Workshop AI-content disclosure checkbox ticked.
- [ ] Fill in [VERSION] and resolve the [VERIFY] uninstall note after the test.
