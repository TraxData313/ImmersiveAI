using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Celebrations;
using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Births
{
    /// <summary>How great a feast the player threw for a child of theirs. Persisted inside each
    /// birth's own JSON, so the values are load-bearing; <see cref="Unfeasted"/> means "no feast
    /// was bought" — a birth nobody celebrated, which is most births in most lives.</summary>
    public enum BirthScale
    {
        Unfeasted = 0,
        Quiet = 1,
        Named = 2,
        Great = 3,
        Regal = 4,
        Legendary = 5,
    }

    /// <summary>
    /// What a child's feast costs and what the coin buys (2026.08.10, Anton's ask — the wedding
    /// tiers applied to the next day of a life). The ladder is deliberately the SAME ladder,
    /// hundred by hundred, up to the half million: a player who has learned what a thousand denars
    /// means at a wedding already knows what it means at a cradle, and one mental model is worth
    /// more than a cleverer second one.
    ///
    /// What differs is the RENOWN, and it differs on purpose. A wedding is an alliance and the
    /// world weighs it as one; a child is joy, and the world only truly cares about an heir. So
    /// every rung pays about a third of its wedding twin — which is also the honest safety rail,
    /// since children are a great deal easier to come by than weddings, doubly so with a polygamy
    /// mod loaded, and a feast that paid a wedding's renown would be the cheapest renown shop in
    /// Calradia within two years of marriage.
    ///
    /// THE NAMED FEAST IS THE ONE WITH THE GUEST LIST (Anton's explicit instruction, given for the
    /// wedding's thousand-denar rung and applied here from the first line): couriers ride to your
    /// own house and to every soul you truly know, however far — and to nobody else. No strangers
    /// of the town, no lords come to be seen. See <see cref="GuestRules.LocalsPresent"/>.
    /// </summary>
    public static class BirthTiers
    {
        public sealed class Tier
        {
            public BirthScale Scale { get; }
            public int Price { get; }
            /// <summary>The short name the player picks by.</summary>
            public string Name { get; }
            /// <summary>What the player is told they are buying, in plain words.</summary>
            public string PlayerDescription { get; }
            /// <summary>How the chronicler is told of it, in the register of the account.</summary>
            public string ChroniclerNote { get; }
            public int WitnessCap { get; }
            public bool AdmitsLocalStrangers { get; }
            public bool InvitesRememberedBonds { get; }
            /// <summary>The kin of BOTH houses — yours and the other parent's.</summary>
            public bool InvitesKin { get; }
            public bool InvitesLordsOfTheRealm { get; }
            public bool InvitesGreatNames { get; }
            /// <summary>The least place this feast can honestly be held in — a crowd has to be
            /// housed and fed somewhere, and gold conjures no hall onto a hillside.</summary>
            public WeddingVenue MinVenue { get; }
            /// <summary>Whether the town must be the player's own, its gates theirs to open.</summary>
            public bool RequiresOwnTown { get; }
            /// <summary>What the day adds to the house's name.</summary>
            public int Renown { get; }

            internal Tier(BirthScale scale, int price, string name, string playerDescription,
                string chroniclerNote, int witnessCap, WeddingVenue minVenue, int renown,
                bool admitsLocalStrangers = true, bool remembered = false, bool kin = false,
                bool lords = false, bool great = false, bool requiresOwnTown = false)
            {
                Scale = scale; Price = price; Name = name;
                PlayerDescription = playerDescription; ChroniclerNote = chroniclerNote;
                WitnessCap = witnessCap; MinVenue = minVenue; Renown = renown;
                AdmitsLocalStrangers = admitsLocalStrangers;
                InvitesRememberedBonds = remembered;
                InvitesKin = kin;
                InvitesLordsOfTheRealm = lords;
                InvitesGreatNames = great;
                RequiresOwnTown = requiresOwnTown;
            }

            public GuestRules Guests => new GuestRules(WitnessCap, AdmitsLocalStrangers,
                InvitesRememberedBonds, InvitesKin, InvitesLordsOfTheRealm, InvitesGreatNames);

            public bool FitsIn(WeddingVenue venue, bool inOwnTown) =>
                venue >= MinVenue && (!RequiresOwnTown || inOwnTown);

            /// <summary>What the place must be, in plain words — for the player, never a rule-book.</summary>
            public string VenueRequirement =>
                RequiresOwnTown ? "only in a town you hold yourself — its gates are yours to throw open"
                : MinVenue == WeddingVenue.Town ? "only in a town — nowhere else can house and feed such a crowd"
                : MinVenue == WeddingVenue.Castle ? "a castle or a town; a village has no hall for it"
                : MinVenue == WeddingVenue.Village ? "a village at the very least — guests must have a roof"
                : "anywhere at all, even a camp under the open sky";
        }

        public static readonly IReadOnlyList<Tier> All = new[]
        {
            new Tier(BirthScale.Quiet, 100, "Bread, salt and a fire",
                "Only those already at hand: your own company, and whoever stands in the place with you. Bread broken over the cradle, and the child's name said aloud once.",
                "A hundred denars: bread and salt and a fire, and nobody called from anywhere. Those who stood there were already standing there. Write the small, true thing it was — a poor table is not a poor welcome, and the chronicler of such a day writes of the child, not of the meat.",
                8, WeddingVenue.OpenField, renown: 1),

            new Tier(BirthScale.Named, 1000, "The naming feast",
                "Couriers ride out to BOTH your houses and to those you truly know — even those weeks away — to come and hear the child named. No one else: this is a guest list, and strangers are not on it.",
                "A thousand denars, and couriers went out: BOTH families were called — the mother's kin and the father's — and with them every soul these two truly know, some riding days to be there. NO ONE ELSE stood in that hall: no strangers of the place, no lords come to be seen, only family and people the parents could name. Write the coming-in of those guests, and the child's name spoken over them.",
                16, WeddingVenue.Village, renown: 4,
                admitsLocalStrangers: false, remembered: true, kin: true),

            new Tier(BirthScale.Great, 10000, "A great naming",
                "Ten thousand denars: everyone above, and the lords of the country round about come to see the child and drink its health.",
                "Ten thousand denars — a great naming. Beyond their own people and those they know, the lords of the country round about came to it, and the feast ran long. Write the scale of it plainly: many tables, the child carried among them, a day the countryside will speak of.",
                26, WeddingVenue.Castle, renown: 20,
                remembered: true, kin: true, lords: true),

            new Tier(BirthScale.Regal, 100000, "A feast fit for an heir",
                "A hundred thousand denars: the great names of the realm are called — rulers and heads of houses — to stand over a cradle and know whose child this is.",
                "A hundred thousand denars: a feast fit for an heir. The great names of the realm were called to it, rulers and heads of houses among them, and word of the child travelled to every corner of the land. Write it as a day the chroniclers of the realm would themselves set down — without ever losing the small living thing at the middle of it.",
                40, WeddingVenue.Town, renown: 90,
                remembered: true, kin: true, lords: true, great: true),

            new Tier(BirthScale.Legendary, 500000, "The town rejoices",
                "Half a million denars, and only in a town you hold yourself: the gates are thrown open and THE WHOLE TOWN feasts the child — your people, the great names of the realm, and everyone you have ever known.",
                "Half a million denars, in a town the parents hold themselves, and the gates were thrown open: THE WHOLE TOWN feasted the child — its own folk first, and with them the great names of the realm and everyone the parents had ever known. Write this as the day a country welcomed a child: the streets themselves keeping it, common people and crowned heads at the same tables, and one newborn who knows nothing of any of it.",
                60, WeddingVenue.Town, renown: 300,
                remembered: true, kin: true, lords: true, great: true, requiresOwnTown: true),
        };

        public static Tier? Of(BirthScale scale) => All.FirstOrDefault(t => t.Scale == scale);

        public static int PriceOf(BirthScale scale) => Of(scale)?.Price ?? 0;

        public static int RenownOf(BirthScale scale) => Of(scale)?.Renown ?? 0;

        public static string NameOf(BirthScale scale) => Of(scale)?.Name ?? string.Empty;

        /// <summary>The line handed to the chronicler about how great a feast this was — empty when
        /// none was bought, and then no word of money enters the account at all.</summary>
        public static string ChroniclerNote(BirthScale scale) => Of(scale)?.ChroniclerNote ?? string.Empty;

        /// <summary>The most souls who may stand at a feast we sold them nothing of.</summary>
        public const int DefaultWitnessCap = 12;

        public static GuestRules Guests(BirthScale scale) =>
            Of(scale)?.Guests ?? GuestRules.WhoeverWasThere(DefaultWitnessCap);
    }
}
