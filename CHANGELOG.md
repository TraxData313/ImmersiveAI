# Changelog

The player-facing history of Immersive AI — short lines written for players, no file names, no
internals (the developer's full record is `TASKS_DONE.md`).

**The running list:** every player-visible change lands under **[Unreleased]** the day it is
made, as a **one-line pill** — never a paragraph. At release time: bump the version in
`module\SubModule.xml` (`package.ps1` stamps the zip from it), retitle the [Unreleased] section
to the new version + date, start a fresh empty [Unreleased] above it, and fill the three change-note
tiers the section feeds (see `tools/WORKSHOP-UPLOAD.md`):

1. **Nexus — 255 characters, hard.** The short block at the top of each version below. Copy it verbatim.
2. **Steam Workshop** — `tools\WorkshopUpdate.xml` (`ChangeNotes`), room for the group headlines.
3. **This file** — the full readable record, grouped under headers, one pill per line.

## [Unreleased]

## v2.2.0 — 2026.08.10

When the words will not come, your own hero finds them for you.

**The Nexus changelog (255 max — copy this verbatim):**

```
* Think (Shift+Enter): your hero drafts your next line
* It reads everything the two of you have said
* Empty box? It finds something worth saying
* A half-typed rant? It turns it into words
* Presets steer it - starter, romantic, ender, or your own
```

- **New: "Think" (Shift+Enter) in the chat and letter windows.** Your own character works out what to say — or write — next, and the words land in your writing box, yours to keep, change, or throw away. Nothing is sent until you send it.
- They think from what you would actually know: who the person before you is, how they stand toward you, where you both are, and every word the two of you have ever exchanged. Not from that person's private mind — you do not have that, and neither does the thinking.
- **Leave the box empty** and they find something worth saying from the moment itself — a continuation, or an opening with someone you have never spoken to.
- **Half-type a rant** and it is read as a half-formed thought, not as wording: it comes back as words a person would actually say, in the language you two speak.
- **Conversation presets** steer it — "something romantic", "a courteous way to end this". Three to start with (*starter*, *romantic*, *ender*), a scrollable menu above the buttons, and an Edit page to add, rework or strike out your own. "Restore the first three" puts them back, after asking.
- A chosen preset is a wish, never a message: the box turns violet, says so, and **Send stays shut** until you make the words your own. Change a single word of it and it is yours to send.
- Try to send one anyway and it tells you why rather than sitting there dead.
- Enter sends, Shift+Enter thinks — fixed, and written on the buttons.
- The message log tells it in your own voice ("What should I say… let me think."), and the cost line beneath is the honest note that it was a paid call. Toggle: EnableThinkForMe.

## v2.1.0 — 2026.08.10

The wedding's morning after: the nights of a marriage, and one plain line for everything the two
of you have not sat down to yet.

**The Nexus changelog (255 max — copy this verbatim):**

```
* Choose which wife you sleep with, each evening
* A child comes from a night you picked, not a hidden roll
* Pay for a night and it gets written - and talked about
* Wives track your nights, and what you have not discussed
* New hearth window (H)
```

- **New: the nights of your marriage are yours to spend.** A child is now begun on a night you actually chose, not by a coin the game flips behind your back.
- Each evening a notice waits on the right of the map — the same place a companion's knock or an arriving letter waits — asking where you will sleep.
- A woman's body keeps its own month, and you can see where hers stands in plain words. The nights near its crest are the ones that may quicken; through the days of the custom her door is closed and no one is asked anything.
- Take every night of her season and a child is about as likely over a month as the game would have made it anyway — miss the season and you have missed the month. The old odds are still the odds; you just have to be there.
- **What you lay out for a night buys three things.** Wine (10 denars) up to a jewel (1,000): better chances, a WRITTEN account of that night in her own voice with a name she keeps it by — and talk. The grander the night, the more surely your other wives hear of it, and they hear its name too.
- And it costs you the morning: a night you paid for leaves the company slow to break camp. Ordinary nights cost the road nothing and say nothing about it.
- Every wife keeps a rolling fortnight of nights — the ones you came to her, the ones her door was closed, the ones she learned you were elsewhere, and the ones she simply never saw you come in. Nothing tells her how to feel about any of it. That has always been her own.
- A wife far away keeps no watch on your nights — but word of another woman travels.
- **New window, on H:** your own hearth. Every wife, where she stands, how her season runs, when the next night is yours, and the fortnight she keeps.
- Click the notice and the choice opens; wave it away with the X and you are not asked at dusk for a week; leave it and it lapses at first light, and that night you slept alone.
- Two plain switches in the window: **Visiting — Manual or Auto**, and **Try to prevent a child — On or Off**. Manual asks you at dusk and lets you go at any hour from the window; Auto goes on its own once the hours are up, late in the evening, with nothing asked, bought or written.
- Auto never takes the day away from you: it waits for the evening, and a night you chose yourself resets the clock. It is a floor under your marriage, not a ceiling — want more than it gives, or want a night written, go yourself in the afternoon.
- Taking care means you go to whoever rather than to whoever is nearest her season, and any night's chance of a child falls to a tenth. Small, never nothing.
- A child begun is not a child known: the announcement now waits a sensible week, and when she learns of it she may come and tell you herself — or write, if you are apart. Even if she would never otherwise reach out.
- Optional line in the log after each night: the chance that stood, and whether a child was begun.
- The written nights should keep surprising you: the storyteller is handed the bare facts of what you brought rather than a ready-made sentence, and it is shown what the last few nights were already called so it does not write the same evening twice.
- The hearth window's "?" page is a quick reference now — short blocks, one fact a line, your own actual numbers — instead of a wall of prose.
- Toggle: EnableNights (on by default). A night costs one writing call only if you paid for it.
- **New: characters now know what has NOT been talked about yet.** One plain divider in their sense of you, at the last moment the two of you had time to yourselves — and after it a dated list of everything since: the markets, the battles, the nights. So the morning after you went to another wife, she knows it happened and knows the two of you have not sat down to it.
- Only time alone moves that divider: a talk that ended, a night together, your wedding night. A battle does not, a market does not, hearing where you slept does not. And it stays put while you are talking, so what she meant to raise cannot vanish from under her mid-sentence.
- Nothing tells them what to do with the list — whether to raise it, and how, is theirs.
- **You see the same list** in the chat window, standing exactly where it belongs: right after the last words you two had alone, with everything that came after it below. The honest answer to "why are they being like this with me".
- And a written night is read in the chat itself — the last few in full, older ones by the name they keep it by. The window of the hearth still holds them all.
- Once you have talked it away it simply disappears, and comes back the next time something happens that you have not gone over together.
- **New: your wedding day is written down — in two parts.** When you wed, the day itself is set down as a story in the manner of the old Scriptures, in whatever tongue you and they speak: the place, the hour, who stood there and what they did, the road that brought you both here, the doubts they once wrote and how each came to rest.
- The one you wed remembers it — and so does everyone who stood there. It appears in their chat as the wedding day's own card, and they will speak of it as people speak of a day like that.
- And a second part, the night that followed, written in your beloved's own voice — theirs and yours alone. It never reaches a guest's memory, and no one else can ever be told it, however they ask.
- When it is written, the day is laid before you in its own window with the world held still, so you can sit and read it — and it tells you where to find it again.
- In the chat window, once you are wed, their misgivings button becomes **Our wedding day** — the whole thing, both parts, openable forever, with the plain-text file's location at the bottom should you want to keep a copy of your own.
- Those who stood with you are chosen by the story you share with them: the notable whose fields you saved and the wanderer you have talked with for hours are at the front of the hall, never crowded out by strangers who merely happened to be indoors.
- Both are kept whole forever. Long after the memory has softened into "we were wed in Onira", ask them to tell you about that day and they will call it back word for word.
- Toggle: EnableWeddingChronicle (on by default). Costs two writing calls, once per wedding.
- Marriage misgivings are a living list now: a new worry can be born in any later talk, one that proves empty they strike out entirely, and only what still STANDS is capped — a heart may change its mind the way a person does, and old settled worries never block a new true doubt.
- The courtship's weather is color-coded in the message log, and every movement leaves a line there: rose when the heart clears (a worry answered or struck out, a clear heart), frost-blue when something freezes (a new worry written, a settled one returning, the heart's road drawing back a step).
- An NPC's backstory now begins as their earliest deep memory instead of a fixed page — over time they decide what of their old life to keep, reshape, or let fade.
- If your name is renowned, a soul you meet for the first time already carries the rumor of you as an early memory — faint word, tales traveling far, or fame across all Calradia.
- **Fixed: marriage misgivings could never actually be laid to rest.** A character would decide, in her own words and at the right moment, that a worry had been answered — and nothing happened, every time. Both causes are gone, so a courtship can now reach a wedding.
- **Fixed: a courtship could never actually reach a wedding.** A character would tell you plainly that she was ready — that she would say yes if you asked — and her heart never recorded arriving there, so the betrothal could never be offered. She now sets down each step the moment she feels it.
- **New: the little button under their name now walks the whole road to a wedding.** It stops being a list of worries and becomes "what do I do next?" — their worries, then their kin's blessing to be sought, then the days of preparation counting down, then the wedding itself. Hover it at any stage and it tells you plainly what the road is waiting for, and where to go for it.
- **New: you choose what wedding to give them — and the money buys who remembers it.** A plain wedding (100 denars) is witnessed by whoever already stood there. From an invited wedding (1,000) upward, couriers ride out to the people you truly have a history with, wherever in the world they are, and they come. A great wedding (10,000) brings the lords of the country round about; a regal one (100,000) the great names of the realm. Every soul who stands there carries that day in their memory for the rest of their life — that is what you are actually buying.
- **New: a legendary wedding (500,000 denars), and it can only be held in a town you hold yourself** — the gates thrown open and the whole town feasting. No amount of gold buys it anywhere else.
- The place is a real choice: a wedding happens where you are standing when you seal it, and the button says so before you spend a denar. Grander weddings need worthier places — a village at the least, then a castle, then a town.
- A wedding adds to your house's renown, from a ripple for a plain one to a real leap for a legendary one — and the chronicler is told what kind of day it was, so the account of a quiet vow and the account of a country celebrating do not read alike.
- Opening your wedding day now plays the wedding once more, and the written account follows when you click through it.
- The moment the last of a character's marriage worries is answered, she knows what it opens: she owns to herself that she is ready, waits to be asked — and when you speak the word, she says yes and lays her promise before you. The log tells you too, so you never have to guess when the hour has come.
- Fixed: a worry stated loosely, or in an inflected language, now finds the worry it means instead of missing it — and a character reciting her whole list back no longer duplicates it.
- Fixed: reaching for a worry that was already answered no longer reads as though nothing was found — she simply says it stands answered.
- Fixed: when a character pauses to consolidate her memories, she says so at the START — so a slower answer explains itself while you wait, instead of after.
- Fixed: a long memory could be saved cut off in mid-word. Characters now get a much larger budget for writing their memory (existing setups are raised too), and a memory cut short falls back to its last finished sentence instead of being kept half-written.
- Playing in a language other than English is measured honestly now: Cyrillic, Greek and Asian scripts cost the AI about 1.6× what English does, so memories are no longer quietly cut short and the memory gauge tells the truth.

## v2.0.0 — 2026.08.08

The biggest release since the mod was born. Playtested and shipped.

**The Nexus changelog (255 max — copy this verbatim):**

```
* Court and marry anyone through the chat
* Hire wanderers by handshake
* Battles, roads and deeds live in companions' memories
* Every soul starts with a private truth of its own
* One deeper memory instead of three lists
* Prompts editable in-game
```

### New — marriage by courtship

- Court anyone in conversation: their heart walks its own road — liking, love, readiness, betrothal, wedding.
- One honest step at a time, moved by their own judgment of your talks, not by a menu.
- They write their own misgivings about a life together, in their own words — up to five, or none at all.
- Talk those worries over openly; only they decide when life has answered one.
- A settled worry is laid to rest with their own little note on what settled it.
- Their readiness to wed waits until nothing they wrote still stands.
- You see it all: the bond line counts them ("misgivings 2/4"), a button opens the full list.
- A soft rose line marks every worry written down or laid to rest.
- Station guards the hand, not the heart: a great house's daughter may love anyone.
- But her hand asks a suitor of standing — softened by a few ranks once her heart is fully won.
- An emperor's daughter is a campaign-long prize, exactly as it should be.
- Noble kin must bless: the head of her house asks a bride-price, haggled in talk or by letter.
- Once betrothed, her house's head appears in your letter window even if you never met him.
- Companion brides and grooms — vanilla forbids them; here they are the point.
- At the wedding a companion is raised to lordship, keeping her place and duties in your party.
- A love already lived is honored: real history starts the road where their heart already stands.
- Nothing is sealed by words alone: betrothal and wedding each take your confirming click.
- The wedding is the real game marriage — cutscene, clan, children, the world's gossip and all.
- Every step fires its own soft notice ("Her heart is truly given.").
- Both windows show the stand beside the bond line ("betrothed to you").
- A betrothed character is shielded from the game marrying them off to someone else.
- Offers ride letters too — hiring terms, a betrothal, a blessing — sealed when the courier arrives.
- The wedding day alone is never laid on paper; that is done face to face.
- Plays well with polygamy mods (Marry Anyone): an existing marriage no longer bars a new courtship.
- New options under "Life of the NPCs": on/off, companion brides, family consent, bride-price haggling, charm slack, betrothal days.

### New — the battle chronicle

- Every battle you fight is set down the moment it ends.
- Where and when, attack or defense, field, siege, hideout or sea-fight (War Sails included).
- Both armies by size and kind — foot, bows, horse, horse-archers — with their seasoning.
- The fallen and the wounded on both sides, prisoners taken, captives freed from the defeated's chains.
- The spoils: total worth, kinds, the richest and most numerous pieces. Plunder, renown, influence.
- Battles earn names by their deeds: "The Grand Victory near Ortysia, over Thrice Our Number".
- Everyone who fought at your side keeps a short first-person note of the day in their own memory.
- What their own hand did ("By my own hand I struck down 4; you felled 11").
- Whether they came out unhurt, wounded, or captive — and the name the chronicle keeps.
- The freshest shared battle is fresh in their mind, in full detail, unprompted.
- Older ones they know by name and can call back whole: "what happened at the storming of Varcheg?"
- Yours to read too: one file per battle plus a running `chronicle.txt` telling the whole war in order.
- Reloading an older save rewinds the chronicle with everything else.
- Characters know their own body: how much strength is back, when they are in no state to fight.
- And they see your wounds too, and may ask after them.
- Plays clean with Training Battles: drills never enter the chronicle.
- Toggle: EnableBattleChronicle (on by default).

### New — the road journal

- Characters riding with you see the last few stops of the road.
- Where you called and for how long, what you traded there — its worth and the chief goods.
- The men you hired or left in a garrison, the captives you sold or gave to a dungeon.
- The freshest stop in detail, the older ones in one line each. Never a bloated ledger.
- Each quest you take stands in their awareness with its giver and deadline ("about 9 remain").
- When it ends they know how — succeeded, failed, the time ran out, or set aside.
- So "how did the caravan job end?" is a real conversation.
- The road shows in the chat too: stops, tasks taken and tasks settled appear as soft narration.
- Toggle: EnableJourneyLog (on by default).

### New — hiring by handshake

- Agree service and price with an unhired wanderer in the conversation itself.
- A confirmation popup names the exact price (and the fair reckoning beside it).
- Only your click pays and hires. Enough gold, room in your company — all the usual rules hold.
- The daily wage is never negotiable.
- Haggling within honest bounds: talked up or down, never beyond a hard limit.
- Sellswords bargain like people who live by it: they open at their worth.
- They concede only what your words have earned, and never volunteer their lowest price.
- They quote their true hiring cost and real daily wage — the game's own numbers, not invented ones.
- Characters know their own gear now: ask what she carries and she answers from her real equipment.
- New options: "Hiring by handshake" on/off, "Haggling range" (0–90%, 0 = fixed price).

### New — the director's spark

- The first time you meet a character, one small AI call writes them a private starting truth.
- 1–3 sentences in their own voice: an old wound, an odd habit, a vanity, sometimes something wilder.
- Grown from their real story, traits, way of speaking and your world prompt.
- It lands in their editable prompt file — read it, rewrite it, erase it, or delete it to re-shape them.
- A soft notice marks the moment: "Something takes shape in them…".
- New option "Starting personality": Generate (default), Ask first, or Off.
- In Ask mode their first reply waits for your choice, so a granted spark speaks from their first words.

### The narrator is gone — everything is first person

- Characters no longer hear an unseen "Angel" voice narrating their lives.
- Arrivals, letters written and received, the urge to seek you out, a hiring struck — all first person.
- Even the quiet settling of old memories now passes through their own mind ("A courier has found me…").
- Old saves keep their recorded moments exactly as they were.
- Your world prompt enters every mind as "Of this world, this I know:".
- Each character's own prompt enters as "Of myself, this I hold true:" — write it in their voice.
- Their inner tools answer them in their own voice too: "Ilya comes back to me…".

### One deeper memory instead of three lists

- The separate rosters of "lasting truths" and "personal goals" are retired.
- They restated what memory already held and made souls repeat themselves.
- Everything now lives in the one memory they rewrite when they gather their thoughts.
- That memory is invited to be far richer — the names, promises, debts and particulars.
- NPCs hold 40 exchanges word for word before folding older ones away (was 30), and keep 20 (was 15).
- Whatever truths and goals your characters already wrote are left where they are. Nothing is deleted.
- You can see how full a memory is: a live gauge under their name in the chat window.
- The share of the AI's memory, the tokens, the exchanges, the age of the oldest — every number a real trigger.
- All the memory dials are in the mod options now, in their own "Memory" section.
- Each names its default, takes hold on the very next exchange, and corrects an impossible value in front of you.

### The windows

- Edit prompts without leaving the game: "Their prompt" and "World prompt" open an editor inside both windows.
- Save, and the change speaks from the very next reply. No restart, no alt-tab.
- Your `#` comment notes in the prompt files are kept.
- Tidier headers: the grey lines under a name stack instead of printing over each other.
- The two prompt buttons keep a row of their own, so a long name is no longer swallowed.
- The deep memory opens as its own page instead of a cramped strip, and starts folded.
- Every page has a "← Back" button — "← Back (discards)" on the prompt editors.
- The talk menu is tidied: "Speak freely with me." at the top, "Farewell." at the bottom.
- DevMode: every test lever now also lives in a Dev panel inside the chat window.

### Fixes

- Battle tallies tell the truth: a heavy blow that didn't kill was counted as a kill (4 bandits, "6 felled").
- A tally that outruns the enemy's real losses is now reported as no tally kept, not a flattering number.
- Scouts no longer mistrust the peaceable: a band at peace with you is named plainly as no threat.
- A strong neutral warband is even pointed out as a shadow brigands keep clear of — shelter, not danger.
- The player is no longer mis-gendered in gendered languages.
- The model guide now names `gpt-5.6-terra` the recommended step-up for those who don't pinch denars.

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
