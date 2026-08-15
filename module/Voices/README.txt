The voices that travel with the mod
===================================

Every voice folder in here is copied onto the player's own shelf the first time they run the
mod, so a fresh install is never a shelf with nothing on it.

Where things go
---------------

    module\Voices\
        female\
            battania\
                gwen\               one voice = one folder
                    voice.json          what it is called, and who it suits
                    embedding.json      THE VOICE ITSELF (Studio's voice-<n>-d2048.json, renamed)
                    reference.wav       optional: the clip it was cloned from
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

Only voices made on the player's own machine are given out this way; a hosted voice costs money
per line and is only ever used when the player puts one on somebody themselves.

There were once "all women" / "all men" slots that outranked all of this. They were retired
(2026.08.15): one press gave every man in the world the same voice, beating ninety-three
culture-matched ones, and nothing could undo it. Do not bring them back.

Adding a voice to a people later moves only about one soul in n onto it, not everybody: the
choice is made by scoring every candidate against the soul's name and taking the highest, not by
counting down a list. So the shelf can grow between versions without recasting the whole world.

Two rules the seeding keeps, and both are about not overruling the player
------------------------------------------------------------------------

1. A name already on their shelf is never written over. Someone who renamed or re-cloned a
   voice keeps their version through every update.
2. A voice already offered is never offered again. Deleting one MEANS something. That is what
   Configs\ImmersiveAI\Voices\_seeded.json records; adding a NEW voice here in a later version
   still arrives on its own.

A voice that is broken (no embedding, mangled voice.json) is skipped with a line in the log and
costs nothing but itself - and arrives on the next start once it is mended.

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
