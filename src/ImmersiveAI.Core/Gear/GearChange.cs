using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Gear
{
    /// <summary>
    /// Where a piece of gear sits, in the words a person would actually use.
    /// <para>
    /// ARMS IS ONE SLOT HERE AND FOUR IN THE GAME, deliberately. The game's armour slots each admit
    /// exactly one kind of thing — a helmet can only go on the head — so the slot IS the word. Its
    /// four weapon slots admit anything: a bow, a spear, a shield and a quiver may sit in any order,
    /// and players reorder them constantly. Naming a weapon slot would therefore say something the
    /// game does not guarantee, so weapons are gathered under one honest heading and named by the
    /// ITEM, whose own name already says what it is.
    /// </para>
    /// </summary>
    public enum GearSlot
    {
        Arms,
        Head,
        Body,
        Legs,
        Hands,
        Cape,
        Mount,
        Harness,
        Banner,
    }

    /// <summary>One piece changing hands. Either side may be empty: given only, taken only, or both
    /// (a swap).</summary>
    public sealed class GearChange
    {
        public GearChange(GearSlot slot,
                          string givenName = "", int givenValue = 0,
                          string takenName = "", int takenValue = 0)
        {
            Slot = slot;
            GivenName = givenName ?? string.Empty;
            GivenValue = givenValue;
            TakenName = takenName ?? string.Empty;
            TakenValue = takenValue;
        }

        public GearSlot Slot { get; }

        /// <summary>What was put into their hands. Empty when something was only taken.</summary>
        public string GivenName { get; }
        public int GivenValue { get; }

        /// <summary>What was taken from them. Empty when something was only given.</summary>
        public string TakenName { get; }
        public int TakenValue { get; }

        public bool WasGiven => GivenName.Length > 0;
        public bool WasTaken => TakenName.Length > 0;
        public bool IsSwap => WasGiven && WasTaken;

        /// <summary>What this one change did to what they carry, in denars.</summary>
        public int Net => GivenValue - TakenValue;
    }

    /// <summary>Everything one visit to the inventory did to one soul's gear.</summary>
    public sealed class GearChangeSet
    {
        public GearChangeSet(IEnumerable<GearChange>? changes = null)
        {
            Changes = (changes ?? Enumerable.Empty<GearChange>()).Where(c => c != null).ToList();
        }

        public IReadOnlyList<GearChange> Changes { get; }

        public bool Any => Changes.Count > 0;

        /// <summary>Richer or poorer than they were, all told.</summary>
        public int NetWorth => Changes.Sum(c => c.Net);
    }
}
