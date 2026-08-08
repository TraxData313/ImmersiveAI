# Changelog

The player-facing history of Immersive AI — short lines written for players, no file names, no
internals (the developer's full record is `TASKS_DONE.md`).

**The running list:** every player-visible change lands under **[Unreleased]** the day it is
made. At release time: bump the version in `module\SubModule.xml` (`package.ps1` stamps the zip
from it), retitle the [Unreleased] section to the new version + date, start a fresh empty
[Unreleased] above it, and copy the section's text into `tools\WorkshopUpdate.xml`
(`ChangeNotes`) for the Workshop and into the Nexus changelog field when uploading — so the
change notes are already written when it is time to ship (see `tools/WORKSHOP-UPLOAD.md`).

## [Unreleased]

## v2.0.0 — 2026.08.08

The biggest release since the mod was born. Characters can now be **courted and married**, they
**remember the battles you fought together** and the **road you walked**, they can be **hired by
handshake**, and every one of them **begins as somebody** — with a private truth of their own.
Underneath, the unseen narrator is gone: everything a character lives now passes through their
own first-person mind.

### Marriage by courtship — win a heart, walk its road, wed for real

- **Characters can now truly be courted through conversation.** Their heart walks its own road —
  liking, love, readiness, betrothal, wedding — moved by their own judgment of your talks, one
  honest step at a time. Nothing is sealed by words alone: the betrothal and the wedding each
  take your confirming click, and the wedding is the real game marriage — cutscene, clan,
  children, the world's gossip and all.
- **They write their own misgivings.** When marriage truly enters your talks, a character pauses
  and sets down what honestly worries them about a life together — in their own words, up to five
  things, or none at all if their heart is clear. You can talk those worries over openly, and only
  they decide when life has answered one, laying it to rest with a little note on what settled it.
  Their readiness to wed simply waits until nothing they wrote still stands. You see it all: the
  bond line counts them ("misgivings 2/4"), a button in the chat window opens the full list with
  their settling notes, and a soft rose line marks every worry written down or laid to rest.
- **Station guards the hand, not the heart.** A great house's daughter may come to love anyone,
  but her hand asks a suitor of standing — softened by up to a few ranks once her heart is fully
  won (the "charm slack"). An emperor's daughter is a campaign-long prize, exactly as it should be.
- **Noble kin must bless the match.** Before a noble bride can wed, the head of her house asks a
  bride-price — haggled in real talk (or by letter) around the world's own reckoning, sealed only
  by your click. Once betrothed, the head of her house appears in your letter window even if you
  have never met — no more combing the map for a father.
- **Companion brides and grooms are first-class** (vanilla forbids them; here they are the point):
  wed the wanderer who rides at your side — at the wedding they are raised to lordship the game's
  own way, keeping their place and duties in your party.
- A love already lived is honored: a character with real history (love spoken across many talks)
  starts the road where their own remembered story says their heart already stands — not at zero
  just because the feature is new.
- The road is visible: every step fires its own soft notice ("Her heart is truly given."), both
  windows show the stand beside the bond line ("betrothed to you"), and a betrothed character is
  shielded from the game marrying them off to someone else while you arrange the rest.
- Plays well with polygamy mods (Marry Anyone): with one installed, an existing marriage no longer
  bars a new courtship — the game's (patched) marriage rules stay the judge.
- New mod options under "Life of the NPCs": Marriage by courtship (on/off), Companion brides &
  grooms, Family consent for noble brides, Bride-price haggling range, Courtship charm slack, and
  Days of betrothal before the wedding.

### The battle chronicle — your battles become their memories

- **Every battle you fight is set down the moment it ends:** where and when it was fought, attack
  or defense, field, siege, hideout or sea-fight (War Sails included), both armies by size and
  kind — foot, bows, horse, horse-archers, with their seasoning — the fallen and the wounded on
  both sides, prisoners taken, captives freed from the defeated's chains, the spoils (total worth,
  kinds, the richest and most numerous pieces), plunder gold, renown and influence won. Battles
  earn names by their deeds: "The Grand Victory near Ortysia, over Thrice Our Number", "The
  Storming of Varcheg", "The Dear-Bought Victory", "The Fall of the Walls".
- **Everyone who fought at your side remembers it:** companions and allied lords each keep a short
  first-person note of the battle in their own memory — what their own hand did ("By my own hand I
  struck down 4; you felled 11"), whether they came out unhurt, wounded, or captive, and the name
  the chronicle keeps. So there is always something true to talk about afterward.
- The freshest battle you share is fresh in their mind in full detail, unprompted; older ones they
  know by name and can call back whole in conversation — ask "what happened at the storming of
  Varcheg?" and they answer from the record, not from fog.
- The chronicle is yours to read too: every battle is a file in the campaign's `_battles` folder,
  with a running `chronicle.txt` telling the whole war in order. Reloading an older save rewinds
  the chronicle with everything else. Toggle: EnableBattleChronicle (on by default).
- Characters now know their own body: a wounded soul sees plainly how much strength they have back
  ("my strength stands near 40 in 100") and knows, past the game's own threshold, that they are in
  no state to fight until they mend — and they see your wounds too, and may ask after them.
- Plays clean with the Training Battles mod: drills, phantom-enemy fights and siege musters are
  never mistaken for real battles — nothing enters the chronicle, and no one "remembers" a war
  against their own comrades.

### The road journal — your companions witness your everyday life

- **Characters riding with you now see the last few stops of the road:** where you called and for
  how long, what you traded there (with its worth and the chief goods), the men you hired or left
  in a garrison, the captives you sold or gave to a dungeon — the freshest stop in detail, the
  older ones in one line each, never a bloated ledger.
- They see the tasks you carry too: each quest you take stands in their awareness with its giver
  and its deadline ("14 days given, about 9 remain"), and when it ends they know how — succeeded,
  failed, the time ran out, or set aside — so "how did the caravan job end?" is a real
  conversation. Toggle: EnableJourneyLog (on by default).
- The road is visible in the chat too, like the battles: when the company rides on from a stop
  where something was done, and when a task is taken or settled, each companion quietly sets it
  down in their memory — so those moments appear in their chat history as soft narration lines you
  can scroll back to.

### Hiring by handshake

- **Agree on service and price with an unhired wanderer in conversation itself**, and she can
  strike the bargain — a confirmation popup names the exact price (and the fair reckoning beside
  it), and only your click pays and hires. All the usual rules hold: enough gold, room in your
  company, and the daily wage is never negotiable.
- Haggling, within honest bounds: the hiring price can be talked up or down, but never beyond a
  hard limit around the game's own reckoning. New settings in the mod options — "Hiring by
  handshake" on/off and "Haggling range" (0–90%, 0 = fixed price).
- Sellswords now bargain like people who live by it: they open at their worth, concede only what
  your words have earned, and never volunteer their lowest price. They quote their true hiring
  cost and their real daily wage — the same numbers the game charges — instead of inventing
  figures.
- Characters also know their own gear now. Ask a wanderer what she carries and she answers from
  her real equipment — no more promised bows she never owned.

### The director's spark — every soul begins as somebody

- **The first time you interact with a character, one small AI call writes them a private starting
  truth** (1–3 sentences in their own voice — an old wound, an odd habit, a vanity, sometimes
  something wilder), grown from their real story, traits, way of speaking and your world prompt.
  It lands in their editable prompt file, so you can read, rewrite or erase it anytime ("Their
  prompt" in the windows); delete the file to have them re-shaped. While it happens, a soft notice
  marks the moment: "Something takes shape in them — they are becoming somebody all their own…".
- New mod option "Starting personality": Generate (default), Ask first (a popup per new face —
  their first reply waits for your choice, so a granted spark speaks from their very first words),
  or Off.

### The narrator is gone — everything is first person now

- **Characters no longer hear an unseen "Angel" voice narrating their lives:** arriving visitors,
  letters written and received, the urge to seek you out, a hiring struck, even the quiet settling
  of old memories — all of it now passes through their own mind, in their own first-person voice
  ("A courier has found me…", "They hired me; I ride with them"). Old saves keep their recorded
  moments exactly as they were.
- Your prompts speak from inside their heads too: the world prompt enters every mind as "Of this
  world, this I know:", and each character's personal prompt as "Of myself, this I hold true:" —
  write the personal one in the character's own voice ("I stutter when I am nervous"), as the file
  templates and the in-game editors now hint.
- Characters' inner tools (recall, the survey, the scales of battle, web-wisdom, the heart) now
  answer them in their own voice as well — "Ilya comes back to me…", "I take stock of my
  company…" — instead of a voice talking at them.

### One deeper memory instead of three lists

- **NPCs no longer keep a separate roster of "lasting truths" about you or a list of "personal
  goals" beside what they remember.** Those lists mostly restated what their memory already held,
  and made souls repeat themselves. Everything now lives in the one memory they rewrite when they
  gather their thoughts — and that memory is invited to be far richer, holding the names, promises,
  debts and particulars the truths used to keep.
- **NPCs remember more of what you actually said.** They now hold 40 exchanges word for word
  before folding the older ones into their deeper memory (was 30), and keep 20 after (was 15).
- Whatever truths and goals your characters already wrote are left exactly where they are —
  nothing is deleted, they are simply no longer used. (The two related settings are gone from the
  mod options.)
- **You can see how full an NPC's memory is.** Under their name in the chat window a grey line now
  shows the weight of what they keep of you word for word — the share of the AI's memory it fills
  against the point where it is condensed, the tokens against the same ceiling, the exchanges, and
  the age of the oldest one ("memory 5.2% / 10% · 21k / 40k tokens · turns 12/30 · oldest 4d/30d ·
  trims back to 5%"). Every number there is a real trigger, so the moment before an NPC turns her
  memories over is never a surprise.
- **All the memory-condensing dials are in the mod options menu now**, in their own "Memory"
  section, each with an explanation and the default named in it: when memory is condensed (by
  share of the AI's memory, by number of exchanges, or by age), what is kept afterwards, how much
  room the memory-writing itself gets, and the notice when it happens. Every one takes hold on the
  very next exchange — no restart — and a value that cannot work (keeping more than the ceiling
  allows) is corrected in front of you instead of quietly ignored.

### The windows

- **Edit prompts without leaving the game.** The chat and letter windows gained editing doors:
  "Their prompt" (the selected character's personal instructions) and "World prompt" (the whole
  world's) open an editor right inside the game — edit, Save, and the change speaks from the very
  next reply. No restart, no alt-tab; your `#` comment notes in the files are kept.
- **Tidier headers.** The grey lines under a character's name (their bond with you, the weight of
  what they remember) no longer print over each other when one runs long — they stack, and
  everything below them moves down to make room. The two prompt buttons keep a row of their own at
  the top, so a long name is no longer swallowed by them. Same fixes in the letter window.
- **The deep memory opens as its own page** — the same shape as the marriage-misgivings view —
  instead of a cramped strip above the conversation, and it starts folded.
- **Every one of these pages now has a "← Back" button**, so it is clear how to step back without
  closing the whole window (Escape still does the same). On the two prompt editors it says
  "← Back (discards)" — Save is still the door that keeps your writing.
- **The talk menu is tidied:** "Speak freely with me." now sits at the very top of the conversation
  options, and "Farewell." at the very bottom — no more hunting for either between the test
  entries.
- For tinkerers with DevMode on: all the test levers now also live in a **Dev panel inside the chat
  window** (a "Dev" button in its top bar) — reveal a soul's whole mind, their courtship road,
  force a reach-out or a letter, forge a battle record, rename them, or read the reach-out odds,
  all without walking over for a face-to-face talk.

### Fixes

- Battle tallies tell the truth again: in battles you fight yourself, the count of who struck down
  whom was inflated — a heavy blow that didn't kill was still counted as a kill, so four bandits
  could leave you "credited" with six. Every hand's work is now counted as men actually fall, and a
  tally that somehow outruns the enemy's real losses is honestly reported as no tally kept rather
  than a flattering number.
- Scouts no longer mistrust the peaceable: when surveying the country about, a band whose realm is
  at peace with yours is now named plainly as no threat — by the law of the land it cannot raise
  arms against you — and a strong neutral warband is even pointed out as a shadow brigands keep
  well clear of, so your scout may counsel sheltering near it instead of fleeing it.
- The player is no longer mis-gendered in gendered languages (the NPC is told who you are in a way
  the model can't miss).
- The model guide now names `gpt-5.6-terra` the recommended step-up for those who don't pinch
  denars — live play found it noticeably sharper than the default at holding character and long
  threads (the cheap default stays the default).

## v1.5.0 — 2026.08.02

- **Added DeepSeek and Gemini as built-in backends.** DeepSeek (platform.deepseek.com) is the
  cheapest paid option — about half the default's cost. Gemini (aistudio.google.com) has a real
  free tier: no card, ~1,500 replies a day — with two catches told plainly: Google trains on
  free-tier traffic, and Gemini 3.x replies run slow (its thinking can't be switched off;
  gemini-2.5-flash in the dropdown is the one that truly switches off).
- **GPT-5.6-Luna prices updated** — OpenAI cut it 80% on July 30 ($0.20/$1.20 per MTok). The
  cost table in existing configs is corrected automatically (hand-edited prices are honored),
  and Luna now heads the model dropdowns.
- Full model comparison: [Which AI should I use?](docs/choosing-a-model.md)

## v1.4.3 — 2026.07.27

- **Scouts and companions now see the land, not only the bands moving on it.** Asked what is
  about, they tell you the villages, towns and castles within sight and how they fare — one
  burning under a raid (and who is at the sack of it), one under siege, one lately plundered —
  which way each band and place lies from you, and what each band is doing. Before this, someone
  could count brigands for you while a village burned in plain sight. Weighing a fight now works
  on a village by name too: under raid it weighs whoever holds the torch.
- **Fixed: the bright pink backdrop behind the portraits** on the notices for someone seeking you
  out or a letter arriving. It is dark now, as it always should have been.

## v1.4.2 — 2026.07.27

- **Mod-menu connection hardened again** (continuing the Nexus report — the new log lines did
  their job and named the failure). On the affected setup, MCM itself throws errors while the
  mod connects to the menu — consistent with MCM building its settings object half-made under
  mismatched dependencies. The mod now repairs such a half-made menu (rebuilding its dropdowns
  by hand), syncs field-by-field so one broken control can't take the rest down, keeps retrying
  the connection for the whole session instead of giving up after three tries, and logs the full
  error detail so the next report pinpoints the exact spot inside MCM. If the menu still cannot
  connect, a notice says so plainly — and config.json always works.

## v1.4.1 — 2026.07.27

- **Fixed: characters you had already talked with introduced themselves all over again.** If your
  whole acquaintance had happened through the chat window or a character coming to you, the game
  itself still counted you as strangers — so the next ordinary conversation opened with the full
  "Greetings, I am so-and-so of clan such-and-such". Speaking through the mod now counts as having
  met, exactly as a face-to-face talk does.
- **Characters reaching out now come with something to discuss.** The moment that decides whether
  someone approaches you asked, in effect, "do you want to say hello?" — and hellos are all it
  produced: a quartermaster reporting the same fine troops, a steward asking how you feel. It now
  asks whether they have **something they want to discuss**, and leaves the what entirely to them,
  their mood, their trade, and the news of the day. Fewer knocks at your tent, and a reason behind
  the ones that come.

## v1.4.0 — 2026.07.26

- **The repetition tune-down** (the first Steam feedback — thank you, Gguy). Characters who
  reached out or wrote to you could circle back on the same topic again and again — a companion
  mailing "the troops are in line" for hours, a lord writing letter after letter you never
  answered. Cause found and fixed at the root: a character's own reach-outs and letters were
  feeding the very score that decides who reaches out next. Now every character **rests after
  reaching out** (no knocking twice in an afternoon, even between friends), and **your silence
  is heard**: each unanswered visit or letter makes them wait days longer and try softer, until
  they hold their peace — one word from you (a talk, a reply, a letter) restores the bond whole.
  Letters also now need a real acquaintance: one shallow conversation no longer funds a
  correspondence. No new settings — the odds view and the bond line under a character's name
  show the truth in plain words ("awaits your answer", "resting after reaching out").
- **The [Immersive AI] tags are gone from the dialogue options** (also asked for). "Speak freely
  with me." now stands on its own — the options read like the game's own.

## v1.3.3 — 2026.07.26

- **Fixed harder: mod-menu settings not taking effect** (the follow-up Nexus report — thank you
  again). On some MCM setups the options menu renders fine but never actually connects to the
  mod underneath, so every edit — Backend, keys, models — landed only in MCM's own files and
  the mod kept speaking with the old backend ("Anthropic API key is not set" after choosing
  OpenRouter). Three fixes ride together: on startup the mod now reads MCM's own settings store
  directly and recovers anything stranded there (a saved key, a chosen backend or model) into
  its config — you'll see a "recovered mod-menu settings" notice when it happens; menu edits
  are synced by watching the menu itself instead of trusting MCM to announce saves; and if the
  menu truly cannot connect, the mod now says so plainly and points you to config.json instead
  of failing silently. Recovery never overwrites anything you set by hand — it only fills what
  was missing.

## v1.3.2 — 2026.07.25

- **Fixed: settings changed at the main menu could be silently reverted** (the first Nexus bug
  report — thank you). Editing Mod Options before loading a campaign — switching the Backend,
  pasting an API key, picking a model — could fail to reach the mod's config and quietly snap
  back when the campaign started, leaving errors like "Anthropic API key is not set" after
  choosing OpenRouter. Settings now take hold wherever and whenever you edit them, main menu
  included. If this bit you: update, set your Backend (and key) once more, and it sticks.

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
