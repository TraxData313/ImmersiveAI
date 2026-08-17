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

### Fixed

- A voice that loses its way mid-sentence — a long, wordless held note in place of the words — is
  now cut within about two seconds instead of running on for half a minute.
- When that happens you are told plainly that the voice stumbled, so it reads as a hiccup rather
  than as something gone wrong with the game. The line is spoken afresh next time it is asked for.
- And it should now happen far more rarely: typographic marks that a voice cannot read — long
  dashes, curly quotes, invisible spaces, the little symbols the conversation draws its cards with —
  are turned into what they mean before a word is spoken, instead of being handed over as-is.
- The voices also read with the speech engine's own settings again. We had quietened them to fix an
  older problem that no longer exists, and the quietening was making the voices lose their way.

## v3.1.2 — 2026.08.17

The mod's own download would not stay unblocked. Nexus quarantines any archive that carries a
program file, and the voices needed one — so the voices have moved out into a small separate
download of their own, and the mod itself installs cleanly again. If you do not use voices there is
nothing to do; if you do, there is one extra file to grab, once.

**The Nexus changelog (255 max — copy this verbatim):**

```
* The mod's download installs cleanly again
* Voices are now a separate optional download
* Grab "Voice host" too if you want NPCs to speak
* Everything else works without it
```

### Changed

- The voice program is no longer part of the main download. It is a separate optional file on the
  mod page — unzip it into your Modules folder exactly like the mod, and voices work as before.
- The mod finds it by itself whichever way you installed it, including older installs.
- The Voices page now tells you plainly when that extra file is missing, instead of implying
  something is broken.

## v3.1.1 — 2026.08.17

A repair release with two halves. Nexus's scanner had quarantined the last two downloads — the
voice program shipped as a single packed file, which their rules read as a self-extracting archive.
It now installs as ordinary files in a folder of its own, and nothing about it changes for you.
The other half is about language: if you play in your own tongue, the things an NPC writes down
once and keeps forever were still coming out in English.

**The Nexus changelog (255 max — copy this verbatim):**

```
* The download is no longer flagged by Nexus's scanner
* Voices now install as plain files in their own folder
* NPCs remember you in the language you play in
* A soul's first personality is written in it too
```

### Fixed

- The download is no longer flagged and quarantined by Nexus's virus scanner.
- The voice program now installs as ordinary files in its own folder, beside the mod rather than
  inside it. Nothing to do differently — it is found automatically, old copies included.
- What an NPC writes into their deep memory now stays in the language you actually play in, instead
  of drifting into English part-way through. The same for who they have become.
- The private personality a soul is given at your first meeting is written in your language too,
  taken from your own world prompt. Write that file in your tongue and every soul you meet from now
  on is sparked in it. (Souls you have already met keep what they were given — delete their
  `custom_instructions.txt` to invite a fresh one.)
- A soul's sense of self can no longer be wiped out by a single stray word when they had nothing new
  to say about themselves.

## v3.1.0 — 2026.08.17

Voices used to ask you to install a program, find a model list and download the right thing out of
it. Now they ask you to press one button — and they tell you honestly, before anything is
downloaded, whether your machine can run them at all.

**The Nexus changelog (255 max — copy this verbatim):**

```
* Voices now install themselves - one button, no setup
* Says up front that local voices need an NVIDIA card
* Hosted voices still need nothing but a key
* A broken download carries on where it stopped
* Fixed two symbols the fonts could not draw
```
<!-- 246 characters, measured 2026.08.17 (pure ASCII, so characters and bytes agree). Nine
     characters of room. Re-measure after ANY edit here. -->

### Hearing them speak
- New: **one button sets voices up.** Talk screen → Voices → "Download the voices", and the mod fetches the speech engine and its models itself — about 2.8 GB, in the background, while you carry on playing. No program to install, no model list to find, no administrator rights.
- New: an interrupted download carries on from where it stopped rather than starting over, and stopping it costs nothing.
- Changed: local voices need an **NVIDIA** graphics card, and the mod now says so before anything is downloaded rather than failing later. On any other machine it points you at the hosted voices instead, which ask nothing of your hardware.
- Fixed: the disk figure on the setup page was a guess and was wrong — voices want about 2.9 GB, not 7.
- Fixed: the Voices page described the play mark with a symbol the game's fonts cannot draw, so the two lines telling you what to press showed empty boxes instead. It now shows the same ♪ the buttons use.
- Fixed: the unread mark beside a name used a symbol from that same unsupported family.

## v3.0.0 — 2026.08.16

The big one: one screen for everyone you know, voices you can hear, every child written down,
and life after the wedding. Much of this is freshly built — this is a hobby project, one pair of
hands — so if something misbehaves, please say so in the comments and it will be mended.

**The Nexus changelog (255 max — copy this verbatim):**

```
* One screen for everyone you know, drawn there alive
* Hear them speak - local or hosted voices
* Every child written down: the hour, the feast, the name
* Lovers, shut doors, and nights that count
* Big update, much is fresh - report what breaks!
```
<!-- 248 characters, measured 2026.08.16 (Nexus counts characters, not bytes: this block is pure
     ASCII so the two agree). Seven characters of room. Re-measure after ANY edit here. -->


### Hearing them speak
- New: a small ▶ beside every line in the talk screen reads it aloud — their replies, their letters, and their own quiet thoughts alike. Press it again on anything ever said between you; a line heard once is instant ever after.
- New: a **Voices** page in the talk screen, there for everyone: pick a voice, hear it before you commit, give it to the person before you or to yourself. It says plainly what is missing when something is.
- New: the mod brings a handful of voices with it, laid on your shelf the first time you run it, so turning voices on never shows you an empty list. They are yours from that moment: rename them, recast them, throw them away — one you delete stays deleted, and one you have edited is never written over by an update.
- New: one button brings over every voice you made in Qwen-TTS Studio. Press it again after making another and only the new ones arrive.
- New: anyone you have not cast by hand is given a voice of **their own people and their own sex** — a Battanian woman sounds Battanian, without you casting five hundred souls one at a time. The choice is made from their own name, so it is the same voice every session and survives every reload. Anything you cast yourself always wins. You are given one too, so your own lines can be read back.
- Only voices made on your own machine are handed out this way. A hosted voice is billed by the minute, so one is never given to anybody automatically — put one on someone yourself and it speaks for them happily.
- Changed: the "all women" and "all men" voice slots are gone, in the talk screen and in the settings. They sat above the voices given by people, so one press left everybody sharing a voice with no way to undo it, and with per-people casting they are simply not needed — a shelf holding one woman's voice already gives it to every woman. Any that were set are emptied once, and voices you cast on individual people are untouched.
- New: the Voices page now says where each voice sits — `female/battania/Gwen` — and lists them grouped that way, which is how you find one on a shelf of a hundred.
- New: the Voices page is now in folders — one per people for the voices made on your machine, one for the speech model's own, one for the hosted ones. Click a folder to fold it shut. The folder of whoever you are talking to opens by itself.
- New: the Voices page shows only voices of the right sex for the person in front of you. "Every voice" lifts that when you want to give someone a voice from outside the usual choice.
- New: the voice someone speaks with is now written beside their name in the talk screen, so you can tell who they sound like without opening anything — and it says *why* they have it: chosen for them, or given for their own people.
- Turning voices on without the speech engine installed now tells you what to actually do — which free program to install, where to get it, and which model to download — instead of naming a file you have never heard of. If the engine is there and only a model is missing, it says that instead.
- New: **Backspace** silences a voice instantly, wherever you are — on the map, in a battle, with every window shut. A **Stop** button also appears in the talk screen whenever anything is speaking.
- New: voices without a graphics card. Put a key for a hosted speech service in the settings and thirteen voices appear beside your own — about 1½ cents a minute, shown in the same cost line as everything else. It cannot clone anyone; that is what your own machine is for.
- New: the speech model's own nine voices show up by themselves if you downloaded the model that carries them, so you can hear something before cloning anything.
- New: your own lines can be read back to you in a voice you choose. Off by default.
- New: someone who comes to you unbidden speaks their first words aloud as they arrive. If two arrive close together the second cuts off the first — Backspace silences it, and there is a switch under Voices if you would rather they waited to be asked.
- Fixed: a spoken reply no longer breaks up. The pieces are now poured together and handed over on the clock instead of a frame late, so a long reply is one unbroken take — and it starts in under half a second, which makes it the right way round for everyone.
- Fixed: a voice that loses its ending and rambles is now cut off after seconds instead of minutes, and the ruined line is never kept, so it cannot come back the next time you scroll past those words.
- Changed: every voice on the shelf now says where it comes from — "(Qwen TTS Studio)" for the ones made on your own machine, "(Qwen TTS, built-in)" for the speech model's own, "(OpenAI)" for the hosted ones. Before, only the hosted ones were marked, which read as though the rest came from nowhere.
- New: the small acted parts — *sets down her cup* — are now read aloud too, as narration between the spoken words, so a reply that answers with a gesture alone is no longer silence. The asterisks themselves are never spoken. There is a switch under Voices if you would rather hear only what was said.
- New: an answer now speaks itself even with the screen shut, so you can send a line and hear the reply while you ride on. The ping and the unread mark still show you the way back to it. A switch under Voices puts it back the old way.

### What they notice
- New: someone riding with you now notices when you change their gear — what you put into their hands, what you took, and what each piece is worth. A companion has no other way of knowing whether the mail you just gave her is a courtesy or a fortune; now she does, measured against her own wage when the sum is large. Nothing is said about gear the game changes on its own, nor about a session you cancel, and trying a helmet on and taking it off again is not a change. Switch under "Life of the NPCs".

### Between us
- Fixed: on the older chat window the "Between us" page told you a child of yours had never been owned before the world and gave you no button to do it. The act now has its door on both windows.
- Fixed: the "Between us" button did nothing on the hearth side of the talk screen. Her page was lying over it and taking the press, so the button was there to be seen and could not be pressed.

### The hearth
- New: the hearth (H) is now a stage. The women of your house are listed on the left, the one you turn to stands there ALIVE in her own place — the same picture the game uses for talking on the map — and her page is on the right: her season, the fortnight of nights, your children's cards, the two switches, and the one decision. It is the same screen the talking happens on, turned over; a button in the bar moves between the two.

### The talk screen
- Fixed: someone you meet in a tavern is now drawn IN the tavern. Everyone inside a town was being shown out in the street, whatever room they were really in.
- New: inside a town you can move the talk — the town, the tavern, or the keep when it is open to you. One button in the bar, and it goes back to wherever they truly stand when you turn to somebody else.

### What they can see for you
- Fixed: your scouts can now tell you how many of a band go wounded and whether it drags prisoners — naming captive lords among them — where before they could not report what you can read off the party's own nameplate. The same facts ride into weighing a battle, on both sides, since men too hurt to stand in a line are not men you are fighting.

- Fixed: a gang leader no longer tells you he has nobody at his back. His men hold an alley rather than riding a warband, and the mod was only ever looking for a warband — so a man with a dozen knives answering to him described himself as alone. He now knows his own ground and who holds it, and can count them for you.

### The road journal
- Fixed: a town stop no longer lists the same goods over and over ("Silver Ore ×6, Cow ×4, Salt ×19…" four times in one breath). The journal was writing an open stop into its file twice, and reading it back appended that stop's goods to themselves — so every save and load while you stood in a town doubled the list again. Journals already swollen this way tidy themselves the next time they are read.

### The nights
- Fixed: the chance a night quickens was too high, and a gift appeared to buy nothing — the hearth window showed the same figure with and without one. The month's odds were being shared out across her season as though they simply added up, which pushed a good night far past what it should be and a gift past what a chance can even mean, so both were being quietly clipped at the same ceiling. Taking a wife's whole season is still exactly as likely to give a child as the game's own reckoning — that promise is now exact rather than approximate — but any single night now reads honestly, and a gift is worth what it costs again.
- Changed: the night's clock now keeps the sun instead of counting hours. One night an evening, and the house is ready again at the same hour every afternoon — whatever time you kept the night before. Before, a late night pushed the next one later still, until the evening's question came round while you were not ready yet and the day was simply lost. A night after midnight now belongs to the evening it grew out of rather than to the morning it landed in. The hour is yours to set in the options.

### What they are told about themselves
- Changed: your own words now have the LAST word. What you write in the world prompt and in a character's own prompt closes their mind instead of sitting in the middle of it, under a line saying plainly that where anything else they remember disagrees, your words stand. Edits there used to lose quietly to a long memory; now they bite.
- Changed: the "how I speak" part of a character's mind is three short habits instead of a wall of rules. Everything about their gifts — recalling the world, weighing their heart, tending a courtship — now sits with the gift itself, so it reaches them only when they can actually use it. Less telling them who to be leaves more room to be someone.
- Changed: a character is no longer told "and now so-and-so comes to me" on every single reply — it was announcing a fresh arrival on the twentieth turn of the same conversation, and they kept greeting you again. They are simply told who they are speaking with.
- Changed: the built-in roleplay guidance lost its "live here and be glad of it" half. Warmly meant, but it was still telling every soul how to be, and they all answered a little more alike for it. If you had left that setting untouched it updates itself; if you wrote your own, yours stands.
- Changed: the room a character gets for writing their memories now starts at 2,000 words' worth instead of 4,000 — cheaper per exchange, and enough for the deep memory to finish its thought. Your own setting, if you changed it, is untouched.

### One place for everyone you know
- Fixed: typing a message could throw you out of the conversation and onto the hearth page mid-word. The key that opens the hearth was still listening while you wrote, so an "h" in a sentence turned the whole screen over. It now stays out of the way while the screen is up — the buttons in the bar do the turning, as they always did.
- Fixed: the key that stops a voice mid-reading no longer fires while you are writing, so deleting a typo just deletes a typo.
- The world now holds still while the talk screen is open, the way it does in an ordinary conversation, and starts again when you step away.
- New setting: a frame limit for the talk screen, 60 by default. Nothing moves there but one person breathing, so your machine can rest; set it to 0 to leave your own limit alone (MCM, under Windows & Hotkeys).
- New: they change how they stand as you talk — a hand on the hip, arms folded, weight shifted — the way they do in a face-to-face conversation. Which poses you see depends on how they feel about you: an old friend stands easy with you, someone who cannot stand you keeps their guard up.
- New: the chat window and the letter window are ONE screen now — everyone you know in a single list, near or far, with the person you chose standing before you in the middle.
- New: the same writing box does both. They can hear you? Write and press Enter. They are a kingdom away? The button becomes "Seal and send" and the words ride out as a letter.
- New: letters read as part of the same story, in their place among the spoken words, wearing a ✉ so you can see which words travelled.
- New: one letter at a time between you and anyone — while a courier rides in either direction, the seal waits for word. A correspondence, not a shouting match.
- Changed: "Speak freely with me." on the map now opens that screen on whoever you clicked, instead of running the talk in the old dialog box. Inside a town's own streets the old panel still carries the conversation.
- Changed: both old keys (O and Y) open the one screen, so nobody's habits break.
- Changed: someone who has died stays in the list, marked "(gone)" — their letters remain readable forever.
- Fixed: someone whose band you have ridden right up to on the open map now counts as standing with you — they show as "here", you can simply speak to them, and they can come to you unbidden. Until now only your own party and the town you were standing in counted, so a lord one step away was "too far for words". Anyone marching in your army counts too. You have to be all but touching them, not merely in sight — the range is the game's own "close enough to bump into each other", which is wider at sea where ships are.
- Changed: the settlement menu now offers one door — "Speak with those you know" — instead of a line for speaking and another for letters, since both opened the same screen.
- Changed: someone who comes to you unbidden now opens the talk screen when you answer their knock, where you can see them, rather than the old dialog box.
- Changed: scrolling up past the oldest word now shows what they may reach for while answering FIRST, above their own mind, so it is found instead of buried under thousands of words.
- Fixed: clicking a party on the map, speaking freely, and closing the screen no longer left you standing in a stand-off with them.

### Coming to you
- New: the one you are wed to comes to you far more readily than anyone else — three times as often as a companion who shares the same amount of story with you, and she will cross a room to begin even if the two of you have never spoken.
- New: your own household — companions, kin, the lords of your clan — are somewhat likelier to come to you than the nobles and townsfolk around them. A nudge, not a landslide.
- New: your spouse now heads the talk screen's list wherever in Calradia she is, and is who the screen opens on.
- Fixed: people almost never came to you any more, and it cost you to be told so. Whenever someone noticed you nearby they first weighed, in private, whether they had anything worth saying — a whole turn of thought, usually answered "no". Nobody is asked now: when the moment falls to someone they simply cross the room and speak, about whatever the day has actually put in front of them. Half the price, twice the company.
- Changed: **the cold now runs one way.** Someone who has come to dislike you used to seek you out exactly as eagerly as someone who loves you — so wronging your wife made her cross the room *more*. Ill feeling now quiets a person instead: fewer visits, fewer letters, down to a rare word at the very bottom. Never total silence, and never at the cost of the morning after — a fresh hurt still brings her to you while it is fresh, and the cold begins after that.
- Note: because nobody is talked out of it any more, people really will come as often as your **socialness** setting promises. If your camp feels busy, turn it down a notch — that dial finally means what it says.

### The post
- Changed: someone far away no longer sits and wonders whether to write to you. When the post falls to them they write, and the letter you get is the same letter — one fewer thought paid for on the way to it.
- Fixed: a courier now always outrides you. You could beat your own letter to its reader, and then stand in front of them with it still on the road between you — which also barred you from writing again.
- Fixed: a letter still travelling to someone you have since ridden up to is handed over on the spot, whenever the two of you meet.

### The birth chronicle
- New: every child born to you is written down in two parts — the hour of it, set down in the mother's own voice and belonging to the two of you, and the feast that welcomed the child, which everyone who stood at it remembers ever after.
- New: when a child comes you choose how to welcome it, from bread and salt at a campfire to a whole town of your own thrown open. What you spend decides who is called.
- New: if you were away when it happened, you are asked when you next ride in to see the child — you have a month.
- New: whoever stands at that feast carries the day for the rest of their life, and your house gains a name for it.
- New: ask an NPC about that day and they will tell it back to you — the parents get the hour as well, and nobody else ever does.
- New: your children live in the hearth window (H) under their mother's name — the hour first, then the feast, kept there for good.
- A child who does not live is recorded and grieved, never feasted and never written up.

### Doors, and what it costs to walk through one
- New: a wife's or a lover's door can be **shut against you** — and when it is, she has written down why, in her own words, and what would answer it. You can read both.
- Nothing opens it but her own judgment. There is no apology button, no price, and no timer: you talk to her, and if what you say truly reaches her she lays her own reason to rest — or she does not.
- She can also strike one out that proved to be nothing, reword one that changed shape, or take back up one she thought was answered and was not.
- Her body's own season is still her season, and is never treated as a grievance.
- Sometimes there is nothing written at all and the door is simply shut. She will tell you that plainly rather than invent a reason, because she is not going to manufacture one to explain herself.
- New: when a door is shut against you, the evening offers one more thing — **"Go to her anyway."** She will not refuse you. She will not welcome you either.
- Nothing is ever written of such a night: no gift, no name, no account. There is nothing to tell, and that is the telling. She keeps one flat line about it and nothing else.
- Each one makes the way back longer, in her own words, whether or not you ever speak to her again. It is never offered during her season, and you can turn the whole option out of your game in the settings.
- New: when she learns something — that you were elsewhere, that another woman is yours now — it is the loudest thing in her for about a day and a half, and she will cross a room or write a letter to say something about it. Once. After that comes the quiet, which is worse.

### Children, and whose they are
- New: a child born to a woman who is not your wife asks you a question the day it comes — **own it before the world with a feast, own it quietly, or say nothing at all.**
- The child is yours by blood either way; the game's own records never change. What changes is what the world is allowed to say — and in this age, a child a man has not owned is not counted his, whatever the whole town privately knows.
- A child you have not owned is spoken of only as its mother's. That is what makes owning one late so heavy: it is not a name, it is a taking-in, in front of everyone who spent years not saying it.
- New: **you can still give the name, at any age** — from the "Between us" page of the child's mother.
- New: every child now keeps its **own memory from the day it is born** — where it was born, who its parents are, and whether anything was said of it. So when it grows up and first speaks to you, it already knows who it is. Nobody briefs it. And the silence is recorded too.
- A child is never given its mother's own account of the hour. It gets the facts of its day; her voice stays hers.
- Fixed: **marrying a woman makes her child yours before the world too.** A child born before the wedding is no longer spoken of as one born outside it — by her, by your other women, or by anyone reading your house. If you once said nothing of that child on purpose, that still stands, and giving the name still means something.
- Fixed: **"No feast" now means no feast.** Declining a party for a child of your own wife could quietly record that you had refused to acknowledge it — and tell her so. The refusal is only ever written down when you were actually asked the question.
- Fixed: a mother whose child you welcomed with a hall full of people remembers the hall. She was being told you had said it quietly in a corridor, whatever you had spent.

### One door, many rooms
- Changed: the little button under someone's name no longer changes its name. It says **"Between us"**, always, and the page behind it holds everything that is between you: what stands unanswered, where you stand, the road, your wedding day, your children, and what is still owed to one of them.
- Fixed: the evening's "where will you sleep tonight?" notice now wears her face, like every other notice in the mod. It was the only one carrying nobody at all, so the circle drew empty.
- New: the town, castle and village menus now tell you the keys — "Speak with those you know (O)" — and carry a second door to your own hearth beside it.

### The lover's road
- New: a courtship can fork. A woman whose heart has gone deeper than any marriage asks may offer herself to you as your lover — no vow, no wedding, no house that takes her name, and no word of the two of you said before anyone. She offers it by her own hand; nothing at all is settled until you take it.
- Your own marriage is no bar to it. A married man's acquaintance can still warm to him and come to love him — what a standing marriage now closes is the road toward a *second* wedding, not the road of a heart.
- It asks more than a marriage does, on purpose. A wife has vows, a settlement and the world's approval holding her in place; a lover has nothing whatever but what she feels, so she has to feel a great deal more before she will say it.
- New: a woman of another house stays under her father's roof until he is paid for losing her. He names what her going costs — anchored to the worth of the gear she stands up in, and haggled like any other bargain — and taking the gold does not reconcile him to it in the slightest.
- A lover rides with you without being hired, and without being counted against the company you may keep. She comes for love, not wages.
- New: what she is to you is written plainly in her own mind — that she is not your wife, that the world has a name for a woman in that place, and that what she makes of that is her own affair. Two women in the same position will not tell you the same thing about it.
- New: everyone in the world now knows what the world holds about all this — that a woman weds, that to be first and only is honor and to be second or hidden is shame, and that a man's children are the ones he owns before everyone. It is stated as what is *held*, never as what anyone feels; where each soul stands toward it is theirs, and some of them will surprise you.
- A lover shares the evenings, the leaks and the hearth with your wives now — including the chance of a child, which is reckoned honestly and works.
- Both the offer and the price can travel by letter, and are put to you when the courier arrives.
- Words alone never make it so. If she reaches for it and the world says no, you are told what she reached for and why nothing came of it — no more talk of something that quietly never happened.
- She can end it, and so can you. What was between you is remembered afterward either way; the gold that bought her out of her house does not come back, and she does not go home.

### The nights
- New: before a night you have laid something out for, you are asked in your own words whether you have anything in mind for it — a place, an hour, something you mean to say or to do — and the account of it is shaped by what you wrote. What she makes of it is still hers, and if what you wanted could not be had where you were, that is part of the night too. Leave the box empty and the evening finds its own way.
- Changed: the more you lay out for a night with your wife, the longer and more particular the account she keeps of it — and every account now tells the whole of it, not only the evening before.
- Changed: the account reaches for different images each time, so a long marriage stops reading like one evening written over and over.
- Changed: her roll of nights keeps the SPECIAL ones whole however old they are — the night a child began on no longer scrolls away in four days — and a run of ordinary nights gathers into one line instead of ten.
- Fixed: a night you paid for whose account never arrived left no trace at all in her memory; she now keeps the night itself.
- New: under her roll of nights a wife now keeps the reckoning of the last thirty days — how often you came, how many of those you made something of, how often she heard you slept alone, and how often, and with whom, she heard you were elsewhere.
- New: the grander the night you gave another woman, the more of it reaches the rest of the house — a cup of wine passes almost unremarked, a jewel is talked about for weeks and named.
- New: when a child becomes known, everyone with you learns it that same day — and your other wives learn it wherever they are.
- Changed: the special night is now named as such in her mind — "the dearest of them all, the one I keep as…".

### Weddings
- Changed: a wedding with invitations is now a guest list, and a guest list excludes. Couriers ride to BOTH families and to the people you truly know, and to nobody else — no more halls full of townsfolk you have never spoken to.
- Fixed: a courtship that cannot go forward yet now says so **in the log**, naming what was reached for and why nothing was sealed. Before, the refusal was told only to them — so if they went on to describe a wedding anyway, nothing on your screen said the world had not moved, and you could believe you were married when you were not.
- Changed: they now know plainly that no words make a marriage — no vows between the two of you, no temple, no ceremony described to each other. Until they lay the day down and you seal it, they will say so warmly instead of playing along.
- Fixed: opening your wedding day again draws the two of you at the age you were on the day, not the age you are now.
- New: your wedding day and each child's day now say how old you both were, and how long ago it was — so a soul telling the story years later tells it about the people you were then.

### What they remember, and what it costs you
- Changed: the long wedding and birth accounts fade in an NPC's mind as any memory does — whole while fresh, then their opening, then just the day itself. They can still tell you the whole of it if you ask.
- Changed: everyday happenings — battles, roads, nights — can no longer crowd real conversation out of what an NPC remembers word for word.

## v2.2.1 - 2026.08.10

**The Nexus changelog (255 max - copy this verbatim):**

```
* Fix: a drafted line came back with line breaks in it, which the writing box does not take. It is one clean line now.
```

- Fixed: what "Think" hands you now always arrives as one unbroken line — line breaks in a drafted line confused the writing box.

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
