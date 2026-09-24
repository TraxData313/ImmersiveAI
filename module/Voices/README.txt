The voices that travel with the mod
===================================

Since 2026.09.24 the voices are spoken by claude-voice, a separate free app the Voices page
installs. The mod tells it about this folder every time it comes up, and it reads every voice in
here WHERE IT LIES - nothing is copied onto a shelf any more.

Each voice needs two things to speak on all three of claude-voice's engines:

    embedding.json                          Qwen speaks from this
    breeze-reference.wav + .txt             Breeze learns the voice from this clip and its exact
                                            words; Pocket clones from it (where cloning is set up)

A voice with only the embedding speaks on Qwen alone. Make the clip with claude-voice:

    python make_reference_clips.py <this folder>

It renders the same short Calradian line in every voice that lacks one, about 13 seconds each.

Where things go
---------------

    module\Voices\
        female\
            battania\
                gwen\               one voice = one folder
                    voice.json          what it is called, and who it suits
                    embedding.json      THE VOICE ITSELF (Studio's voice-<n>-d2048.json, renamed)
                    breeze-reference.wav/.txt   a clip of it and its words, for Breeze and Pocket
            empire\ vlandia\ sturgia\ aserai\ khuzait\ nord\
            other\
                sibylla\            belongs to no people - offered to everyone
        male\
            ...
        README.txt              this file - never seeded, files at the top never are

SEX, THEN PEOPLE, THEN THE VOICE. The middle rung is the game's own culture id, lowercased -
empire, vlandia, sturgia, aserai, khuzait, battania (and nord / vakken from War Sails). It is
what lets a soul nobody has cast be given a voice of their own people; put a voice under
"other" (or "misc", or "any") and it belongs to nobody in particular, which is the right home
for a voice cloned off a friend.

Both shallower shapes still work and always will: female\sibylla\ is a woman of no people, and
a voice folder sitting loose at the top belongs to nobody at all.

The voice folder's name becomes its id on the player's shelf, so keep it lowercase and plain.
Two peoples may safely both have a "gwen" - the second is filed as gwen-vlandia. Neither the
sex nor the culture folder is ever guessed from the voice: a voice.json that states its own
Gender or Culture keeps them, whichever folder it happens to sit in.

voice.json
----------

    {
      "Name": "Gwen",
      "Dimension": 2048,
      "ReferenceText": "Battanian, young, a laugh never far off."
    }

Gender and Culture are filled in from the folders it sits under, so neither need be written here
- and when either IS written here, it wins. ReferenceText is what a player reads when choosing
between six voices, so give it one honest line.

Who gets which voice
--------------------

A soul the player has cast by hand keeps that voice. Everyone else - the player included - is
given one of their own people and their own sex, chosen from their own name, so it is the same
voice every session, survives every reload, and is never written down anywhere. If their people
have no voices yet, the ones under "other" are used, and failing those, anyone of the right sex.

There were once "all women" / "all men" slots that outranked all of this. They were retired
(2026.08.15): one press gave every man in the world the same voice, beating ninety-three
culture-matched ones, and nothing could undo it. Do not bring them back.

Adding a voice to a people later moves only about one soul in n onto it, not everybody: the
choice is made by scoring every candidate against the soul's name and taking the highest, not by
counting down a list. So the shelf can grow between versions without recasting the whole world.

The seeding is retired
----------------------

Until 2026.09.24 these folders were copied onto the player's shelf once each, with a ledger so a
deleted one was never offered again. claude-voice reads them in place now, so there is nothing to
copy and nothing to remember. Voices a player made in those days stay on their shelf
(Configs\ImmersiveAI\Voices) and claude-voice is told about that folder too.

Where the voices in here came from
---------------------------------

The culture voices were made this way (2026.08.15): the brief was the ACCENT each people should
read as - Battanian Celtic, Vlandian French-ish, Sturgian and Nord northern, Aserai and Khuzait
eastern, the Empire British - prompts were drafted from that brief, the source audio was sourced
on Hugging Face from voices offered free to train on, and the clones were made in Qwen-TTS Studio.
Each voice.json records it in its own Source line, so the provenance travels with the folder
instead of living in somebody's memory.

The names and the pitch figures in their descriptions come from a survey of the game's own
dialogue voiceover, which was used as the SPEC to match against - what a Khuzait curt man should
sound like - and never as the source audio. That distinction is the whole reason these may ship.

The rule that actually matters
------------------------------

ONLY VOICES WE HAVE THE RIGHT TO SHIP. A voice folder carries the reference clip inside it, so
shipping one hands every player a copy of whatever it was cloned from. Public-domain or
CC0-licensed source audio only - kyutai/tts-voices on Hugging Face is 228 clips donated
deliberately for exactly this. Not a celebrity, not an actor, not someone off YouTube.

Anything cloned from a real person's voice without their blessing stays on the machine it was
made on and never enters this folder. package.ps1 enforces both halves: a name list, and a
"NOT FOR RELEASE" mark inside voice.json for anything a name list cannot keep up with.
