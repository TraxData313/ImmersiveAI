namespace ImmersiveAI.Core.Celebrations
{
    /// <summary>
    /// Who stands at a day of the player's, and how many of them (2026.08.10). The wedding wrote
    /// these rules first; the birth reuses them word for word, and anything else we ever feast
    /// will too — so they live here, apart from either, and each ladder of tiers simply says which
    /// rules its rung buys.
    ///
    /// THE ONE RULE THAT IS NOT A LADDER is <see cref="LocalsPresent"/>, and it is deliberate
    /// (Anton, 2026.08.10, reading his own wedding chronicle back and finding five strangers of
    /// Baltakhand in the hall: "нека са познатите ни, дори да не са с нас, викнати, без
    /// непознати"). A day nobody was called to is witnessed by whoever happened to be standing
    /// there — that is honest, and it is what the plainest tier is. But the moment couriers go
    /// out, the day has a GUEST LIST, and a guest list is a thing that excludes: the miller who
    /// merely lives in the town is not on it. Higher still the country itself is called and the
    /// place fills again, strangers and all. So the flag runs true → false → true up the ladder,
    /// and that is the shape of the thing, not a bug in it.
    /// </summary>
    public sealed class GuestRules
    {
        /// <summary>The most souls who may stand there. The player's own company is taken first and
        /// is never sorted away — they were unquestionably present.</summary>
        public int Cap { get; }

        /// <summary>Whether the souls who merely HAPPEN to be in the place are counted as standing
        /// there. See the class remarks: this is true only at the tiers where nobody was called.</summary>
        public bool LocalsPresent { get; }

        /// <summary>Whether couriers ride to the souls the player truly shares a story with, however
        /// far away they are — the friend three weeks' ride away comes.</summary>
        public bool RememberedBonds { get; }

        /// <summary>
        /// Whether the KIN OF BOTH HOUSES are called — the player's own, and the house this day
        /// joins them to. Family is not a guest-list decision: once a courier is riding at all,
        /// one of them rides to your own brother, and one rides to her mother (Anton, 2026.08.10 —
        /// "нека дойде и нейния род"). This is NOT the same as
        /// <see cref="LordsOfTheRealm"/>, which calls the whole country round about; two houses
        /// standing at a wedding or a cradle are simply the family.
        /// </summary>
        public bool Kin { get; }

        /// <summary>Whether the kin of the other house and the lords of the country round about
        /// are called.</summary>
        public bool LordsOfTheRealm { get; }

        /// <summary>Whether the great names — those who rule, and those who head a house — are called.</summary>
        public bool GreatNames { get; }

        public GuestRules(int cap, bool localsPresent, bool rememberedBonds, bool kin,
            bool lordsOfTheRealm, bool greatNames)
        {
            Cap = cap;
            LocalsPresent = localsPresent;
            RememberedBonds = rememberedBonds;
            Kin = kin;
            LordsOfTheRealm = lordsOfTheRealm;
            GreatNames = greatNames;
        }

        /// <summary>What a day we sold them nothing of gathers: whoever was there, up to the dozen
        /// every wedding before the tiers existed was written with. Changing this number would
        /// quietly rewrite what an unpaid day means, so it is a constant and not a guess.</summary>
        public static GuestRules WhoeverWasThere(int cap = 12) =>
            new GuestRules(cap, localsPresent: true, rememberedBonds: false, kin: false,
                lordsOfTheRealm: false, greatNames: false);
    }
}
