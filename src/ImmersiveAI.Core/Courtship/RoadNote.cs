using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// One movement of the road, written down FOR THE PLAYER and for nobody else (2026.08.31,
    /// Anton: "the game just doesn't guide the player… instead of the funnest part it becomes the
    /// biggest irritation").
    ///
    /// <para>THE FAULT THIS EXISTS TO FIX. Every road movement already announced itself — steps,
    /// refusals, misgivings set down and laid to rest, all of it in Anton's own rose and frost-blue
    /// colour language — through <c>InformationManager.DisplayMessage</c>, which writes to the map's
    /// message log. The talk screen is a full-screen layer over the map. So the one place where all
    /// of this happens is the one place where none of it can be seen, and the mod spent a month
    /// narrating carefully into a covered speaker.</para>
    ///
    /// <para>WHY NOT SIMPLY RECORD BEATS, which would land in the thread for free: a beat is a
    /// MEMORY, and a memory is read back to her on every later exchange. Her misgivings already ride
    /// her sheet as a list; a beat saying she wrote one would be the same thing told twice, which is
    /// precisely the fault that retired the truths and the aims. And a REFUSAL recorded as a beat is
    /// worse than redundant — she would re-read "the world said no" forever and go on arguing with
    /// it. So the player's record is kept apart from hers, which is also the honest shape: he is the
    /// one who needs the machine explained.</para>
    ///
    /// <para>NOTHING HERE MAY EVER REACH A PROMPT. It is written by the game layer, read by the
    /// windows, and touched by no builder — guarded by a test that puts a note in a memory and
    /// asserts no built message carries it.</para>
    /// </summary>
    public sealed class RoadNote
    {
        /// <summary>Campaign day it happened, for the stamp.</summary>
        public double Day { get; set; }

        /// <summary>
        /// The soul's LIFETIME turn count at the moment it happened, which is how a note finds its
        /// place among the turns in the thread. Deliberately not the campaign day: the talk screen
        /// holds the world still, so every exchange of one conversation shares a day and the day
        /// could never order them.
        /// </summary>
        public int AfterTurn { get; set; }

        /// <summary>What kind of movement — the view picks a colour from it. See the constants on
        /// <see cref="RoadNotes"/>.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The player-facing line, already phrased by whoever recorded it.</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>The player's own record of the road — append, cap, and the kinds a view colours by.</summary>
    public static class RoadNotes
    {
        /// <summary>A heart or a hand moved forward — the rose of Anton's colour language.</summary>
        public const string KindMoved = "moved";
        /// <summary>Something froze: a doubt set down, a step back, a promise taken up again.</summary>
        public const string KindFroze = "froze";
        /// <summary>A reach the world or her own rails refused. The single most important kind:
        /// before these were visible, a refused reach was a private word between the mod and the
        /// model, and it read exactly like a broken mod (Steam, rmanicky, 2026.08.15).</summary>
        public const string KindRefused = "refused";
        /// <summary>A seal: the betrothal taken, the blessing bought, the wedding sealed.</summary>
        public const string KindSealed = "sealed";
        /// <summary>A promise taken back. Its own kind, and not merely a freeze, because Anton's
        /// colour language gives this one red alone — a broken troth is the only movement of this
        /// road that is a wound rather than a pause.</summary>
        public const string KindBroken = "broken";

        /// <summary>Enough to read back the whole of a courtship without becoming a ledger. Old
        /// notes fade from the front; nothing here is load-bearing, so losing the oldest costs
        /// nothing but scrollback.</summary>
        public const int MaxKept = 60;

        /// <summary>Writes one movement down. Blank text records nothing — a caller with nothing to
        /// say must leave no empty line behind.</summary>
        public static void Add(List<RoadNote>? notes, string kind, string? text, double day, int afterTurn)
        {
            if (notes == null || string.IsNullOrWhiteSpace(text)) return;
            notes.Add(new RoadNote
            {
                Day = day,
                AfterTurn = Math.Max(0, afterTurn),
                Kind = string.IsNullOrWhiteSpace(kind) ? KindMoved : kind.Trim(),
                Text = text!.Trim(),
            });
            while (notes.Count > MaxKept) notes.RemoveAt(0);
        }
    }
}
