using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Celebrations;

namespace ImmersiveAI.Core.Weddings
{
    /// <summary>How great a wedding the player paid for. The values are persisted inside each
    /// wedding's own JSON, so they are load-bearing; <see cref="Unpaid"/> is what a wedding sealed
    /// outside our own door (vanilla's barter, another mod) carries, and it means "no feast was
    /// bought" rather than "the cheapest one" — the chronicler is simply told nothing of money.</summary>
    public enum WeddingScale
    {
        Unpaid = 0,
        Modest = 1,
        Invited = 2,
        Great = 3,
        Regal = 4,
        /// <summary>The one that cannot be bought with money alone: a town of the player's OWN,
        /// thrown open, the whole place feasting (2026.08.09, Anton's ask).</summary>
        Legendary = 5,
    }

    /// <summary>What kind of place a wedding is being held in, ranked as the world would rank it —
    /// a field is a field, and a hundred thousand denars cannot buy a hall where there is none
    /// (2026.08.09, Anton's ask). Ordered on purpose: a town satisfies every requirement below it.</summary>
    public enum WeddingVenue
    {
        OpenField = 0,
        Village = 1,
        Castle = 2,
        Town = 3,
    }

    /// <summary>
    /// What a wedding costs and what the coin actually buys (2026.08.09, Anton's design). The money
    /// does NOT buy adjectives in a paragraph — it buys MEMORY: how far the invitation reaches, and
    /// therefore how many souls carry that day in their own minds forever. A hundred denars weds you
    /// in front of the people who already ride with you; a hundred thousand fills the hall with the
    /// great names of the realm, and every one of them remembers standing there.
    ///
    /// From <see cref="WeddingScale.Invited"/> upward the invitation also reaches the souls the
    /// player has a real story with WHEREVER THEY ARE — a courier goes out, and the friend three
    /// weeks' ride away comes. That was Anton's explicit ask, and it is the heart of the feature:
    /// the guests are the people who matter to you, not merely the people who happened to be indoors.
    /// </summary>
    public static class WeddingTiers
    {
        public sealed class Tier
        {
            public WeddingScale Scale { get; }
            public int Price { get; }
            /// <summary>The short name the player picks by ("A great wedding").</summary>
            public string Name { get; }
            /// <summary>What the player is told they are buying, in plain words.</summary>
            public string PlayerDescription { get; }
            /// <summary>How the chronicler is told of it, in the register of the account.</summary>
            public string ChroniclerNote { get; }
            /// <summary>The most souls who may stand there (the player's own company always first).</summary>
            public int WitnessCap { get; }
            /// <summary>Whether couriers ride to the souls the player has a real story with, however
            /// far away they are.</summary>
            public bool InvitesRememberedBonds { get; }
            /// <summary>Whether the kin and the lords of the country round about are called.</summary>
            public bool InvitesLordsOfTheRealm { get; }
            /// <summary>Whether the great names — rulers and heads of houses — are called.</summary>
            public bool InvitesGreatNames { get; }
            /// <summary>Whether the souls who merely happen to stand in the place are counted as
            /// witnesses. False exactly where the day has a GUEST LIST — see
            /// <see cref="GuestRules.LocalsPresent"/> for why this one does not climb.</summary>
            public bool AdmitsLocalStrangers { get; }
            /// <summary>Whether couriers also ride to the kin of BOTH houses — the player's own,
            /// and the house this day joins them to.</summary>
            public bool InvitesKin { get; }

            /// <summary>These same rules, in the shape the birth chronicle and anything else we
            /// ever feast reads them.</summary>
            public GuestRules Guests => new GuestRules(WitnessCap, AdmitsLocalStrangers,
                InvitesRememberedBonds, InvitesKin, InvitesLordsOfTheRealm, InvitesGreatNames);
            /// <summary>The least place this wedding can honestly be held in. Gold cannot conjure a
            /// hall out of a hillside, and a crowd has to be housed and fed somewhere.</summary>
            public WeddingVenue MinVenue { get; }
            /// <summary>Whether the town must be the player's OWN — a thing no coin can buy, since
            /// only a lord of the place can throw its gates open and feast the whole of it.</summary>
            public bool RequiresOwnTown { get; }

            /// <summary>
            /// What the day adds to the house's name — PRICED, not guessed (Anton's research and
            /// his call, 2026.08.09). Renown has no shop, but the game gives it a market anyway: a
            /// mercenary contract trades roughly 250-400 denars for a point and tournaments run
            /// 100-300, so a wedding is priced against that — WITH A DUMPING THAT GROWS WITH THE
            /// TARIFF. Anchored on his two numbers, a hundred denars for one point and half a
            /// million for five hundred, the rate walks 100 → 200 → 333 → 667 → 1000 denars a
            /// point: the plain wedding pays honest market, and the grand ones pay steadily worse,
            /// because those are bought for the spectacle and for who will remember it, never as an
            /// investment.
            ///
            /// The taper is also the safety rail: with a polygamy mod loaded (Anton runs Marry
            /// Anyone) a player may wed again and again, and a wedding paying market rate at the
            /// top would be the cheapest renown shop in Calradia. Cross-check against the
            /// clan-tier thresholds (50 / 150 / 350 / 900 / 2200): the plain wedding is a ripple,
            /// the great one a season of good war, and the legendary one a real step up that costs
            /// half a million AND a town of your own.
            /// </summary>
            public int Renown { get; }

            internal Tier(WeddingScale scale, int price, string name, string playerDescription,
                string chroniclerNote, int witnessCap, bool remembered, bool lords, bool great,
                WeddingVenue minVenue, int renown, bool requiresOwnTown = false,
                bool admitsLocalStrangers = true, bool kin = false)
            {
                RequiresOwnTown = requiresOwnTown;
                Renown = renown;
                Scale = scale; Price = price; Name = name;
                PlayerDescription = playerDescription; ChroniclerNote = chroniclerNote;
                WitnessCap = witnessCap;
                InvitesRememberedBonds = remembered;
                InvitesLordsOfTheRealm = lords;
                InvitesGreatNames = great;
                AdmitsLocalStrangers = admitsLocalStrangers;
                InvitesKin = kin;
                MinVenue = minVenue;
            }

            /// <summary>Whether this wedding can be held in such a place.</summary>
            public bool FitsIn(WeddingVenue venue) => venue >= MinVenue;

            /// <summary>Whether this wedding can be held here at all, place and title both.</summary>
            public bool FitsIn(WeddingVenue venue, bool inOwnTown) =>
                FitsIn(venue) && (!RequiresOwnTown || inOwnTown);

            /// <summary>What the place must be, in plain words — for the player, never a rule-book.</summary>
            public string VenueRequirement =>
                RequiresOwnTown ? "only in a town you hold yourself — its gates are yours to throw open"
                : MinVenue == WeddingVenue.Town ? "only in a town — nowhere else can house and feed such a crowd"
                : MinVenue == WeddingVenue.Castle ? "a castle or a town; a village has no hall for it"
                : MinVenue == WeddingVenue.Village ? "a village at the very least — guests must have a roof"
                : "anywhere at all, even a field under the open sky";
        }

        public static readonly IReadOnlyList<Tier> All = new[]
        {
            new Tier(WeddingScale.Modest, 100, "A plain wedding",
                "Only those already at your side: your own company, and whoever stands in the place with you. Vows, bread and wine, and no more.",
                "It was a plain wedding, paid for with a hundred denars: no couriers went out, and those who stood there were the company already at their side. Write it as the small, true thing it was — a poor feast is not a poor marriage, and the chronicler of a plain wedding writes of the vow, not of the table.",
                8, remembered: false, lords: false, great: false, minVenue: WeddingVenue.OpenField, renown: 1),

            // THE GUEST LIST (2026.08.10, Anton's ask, reading his own chronicle back): a called
            // wedding is the ONE rung where no stranger stands. Couriers ride to your own house
            // and to every soul you truly have a story with, however far — and to nobody else.
            // Before this the notables of the town were swept in as well, and his hall held five
            // people he had never spoken a word to.
            new Tier(WeddingScale.Invited, 1000, "A wedding with invitations",
                "Couriers ride out to BOTH your houses — yours and theirs — and to those you truly know: the friends, companions and folk you have a history with, even those weeks away. No one else: this is a guest list, and strangers are not on it.",
                "A thousand denars were spent, and couriers went out: BOTH families were called — the bride's kin and the groom's — and with them every soul these two truly know, some riding days to be there. NO ONE ELSE stood in that hall: no strangers of the place, no lords come for the spectacle, only family and people these two could name. Write the coming-in of those guests as part of the day, and let the hall be the two houses and their own people.",
                16, remembered: true, lords: false, great: false, minVenue: WeddingVenue.Village, renown: 5,
                admitsLocalStrangers: false, kin: true),

            new Tier(WeddingScale.Great, 10000, "A great wedding",
                "Ten thousand denars: everyone above, and the lords of the country round about and of their house come to the feast.",
                "Ten thousand denars were spent — a great wedding. Beyond their own people and those they know, the lords of the country round about and of the houses concerned came to it, and the feast ran long. Write the scale of it plainly: many tables, many banners, a day the countryside will speak of.",
                26, remembered: true, lords: true, great: false, minVenue: WeddingVenue.Castle, renown: 30,
                kin: true),

            new Tier(WeddingScale.Regal, 100000, "A wedding fit for kings",
                "A hundred thousand denars: the great names of the realm are called — rulers and heads of houses — and the whole world hears of it.",
                "A hundred thousand denars were spent: a wedding fit for kings. The great names of the realm were called to it — rulers and heads of houses among them — and word of the day travelled to every corner of the land. Write it as a wedding the chroniclers of the realm would themselves set down, without ever losing the two souls at the middle of it.",
                40, remembered: true, lords: true, great: true, minVenue: WeddingVenue.Town, renown: 150,
                kin: true),

            new Tier(WeddingScale.Legendary, 500000, "A legendary wedding",
                "Half a million denars, and it can be given only in a town you hold yourself: the gates are thrown open and THE WHOLE TOWN feasts — your people, the great names of the realm, and everyone you have ever known. A day the world does not forget.",
                "Half a million denars were spent, in a town the couple hold themselves, and the gates were thrown open: THE WHOLE TOWN feasted — its own folk first, and with them the great names of the realm and everyone the couple had ever known. Write this as the day a country remembers: the streets themselves keeping the wedding, common people and crowned heads at the same feast, and the two souls in the middle of it who are still only two souls.",
                60, remembered: true, lords: true, great: true, minVenue: WeddingVenue.Town, renown: 500,
                requiresOwnTown: true, kin: true),
        };

        public static Tier? Of(WeddingScale scale) => All.FirstOrDefault(t => t.Scale == scale);

        /// <summary>The tier's price, or 0 for a wedding no coin of ours paid for.</summary>
        public static int PriceOf(WeddingScale scale) => Of(scale)?.Price ?? 0;

        /// <summary>The line handed to the chronicler about how great a day this was — empty for a
        /// wedding sealed outside our own door, where we know nothing of the purse.</summary>
        public static string ChroniclerNote(WeddingScale scale) => Of(scale)?.ChroniclerNote ?? string.Empty;

        /// <summary>The most souls who may stand there. A wedding we did not sell keeps the old
        /// twelve — that is what every wedding before this feature was written with.</summary>
        public const int DefaultWitnessCap = 12;

        public static int WitnessCap(WeddingScale scale) => Of(scale)?.WitnessCap ?? DefaultWitnessCap;

        /// <summary>Who is called to a wedding of this scale. A wedding sealed outside our own door
        /// knows nothing of any purse, so it gathers whoever truly stood there — which is exactly
        /// what every wedding before the tiers existed was written with.</summary>
        public static GuestRules Guests(WeddingScale scale) =>
            Of(scale)?.Guests ?? GuestRules.WhoeverWasThere(DefaultWitnessCap);

        /// <summary>What the day adds to the house's name; 0 for a wedding we sold them nothing of.</summary>
        public static int RenownOf(WeddingScale scale) => Of(scale)?.Renown ?? 0;

        /// <summary>The plainest reading of the name, for the record and the player's own eyes.</summary>
        public static string NameOf(WeddingScale scale) => Of(scale)?.Name ?? string.Empty;
    }
}
