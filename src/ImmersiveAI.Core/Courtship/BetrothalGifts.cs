using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>What was set on her hand when the question was asked. Persisted inside each
    /// betrothal's own JSON, so the values are load-bearing; <see cref="WordsAlone"/> is also what
    /// a betrothal sealed from HER laid offer carries — no gift passed, and the chronicler is told
    /// nothing of one.</summary>
    public enum BetrothalGift
    {
        WordsAlone = 0,
        SilverBand = 1,
        /// <summary>Silver, with a small stone in it.</summary>
        SilverRing = 2,
        /// <summary>Gold, with a fine one.</summary>
        GoldRing = 3,
        Heirloom = 4,
    }

    /// <summary>
    /// The proposal's own ladder (2026.08.31, Anton's design — "chose denars"). The coin buys the
    /// THING and the WEIGHT OF THE TELLING: the gift is named in the written account and kept in
    /// the record forever, and a greater gift buys a longer, richer day. Nothing else — no renown
    /// (a proposal is between two souls, not a feast), and the gold simply leaves the purse,
    /// because the ring is what it bought.
    ///
    /// The ChroniclerNote stays BARE NOUNS on purpose (the named-prose-comes-back-verbatim law):
    /// a note that says "a ring of gold" gives the chronicler a fact; one that says "a ring that
    /// caught the firelight" gives him a sentence he will lift word for word.
    /// </summary>
    public static class BetrothalGifts
    {
        public sealed class Tier
        {
            public BetrothalGift Gift { get; }
            public int Price { get; }
            /// <summary>The short name the player picks by ("A ring of gold").</summary>
            public string Name { get; }
            /// <summary>One tight line of what the player is buying.</summary>
            public string PlayerDescription { get; }
            /// <summary>The gift as the record and the account name it ("a ring of gold");
            /// empty when no gift was given.</summary>
            public string GiftName { get; }
            /// <summary>How the chronicler is told of it — bare nouns, never imagery.</summary>
            public string ChroniclerNote { get; }
            public int MinSentences { get; }
            public int MaxSentences { get; }

            internal Tier(BetrothalGift gift, int price, string name, string playerDescription,
                string giftName, string chroniclerNote, int minSentences, int maxSentences)
            {
                Gift = gift; Price = price; Name = name;
                PlayerDescription = playerDescription;
                GiftName = giftName; ChroniclerNote = chroniclerNote;
                MinSentences = minSentences; MaxSentences = maxSentences;
            }

            /// <summary>The character rail for the written account — the night chronicle's lesson:
            /// a flat cap silently shortens the very tier that exists to be longest, and rich
            /// Cyrillic sentences run past 250 characters each.</summary>
            public int AccountCharBudget => Math.Max(1400, MaxSentences * 260);
        }

        public static readonly IReadOnlyList<Tier> All = new[]
        {
            new Tier(BetrothalGift.WordsAlone, 0, "With words alone",
                "No gift — the asking stands on its own words.",
                string.Empty,
                "No gift was given; the asking stood on its own words, and that is how you tell it.",
                5, 6),
            new Tier(BetrothalGift.SilverBand, 100, "A band of silver",
                "A plain band, honestly made.",
                "a band of silver",
                "A band of silver was set on the hand — a plain thing, honestly made.",
                6, 7),
            new Tier(BetrothalGift.SilverRing, 1000, "A silver ring with a small jewel",
                "Silver, with a small stone set in it.",
                "a silver ring set with a small jewel",
                "A silver ring with a small jewel in it was set on the hand.",
                7, 8),
            new Tier(BetrothalGift.GoldRing, 10000, "A gold ring with a fine jewel",
                "Gold, with a stone of real price.",
                "a gold ring set with a fine jewel",
                "A gold ring with a fine jewel in it was set on the hand — a stone the like of which few in that country had held.",
                8, 9),
            new Tier(BetrothalGift.Heirloom, 100000, "An heirloom fit for a queen",
                "A treasure to outlive you both — spoken of wherever they go.",
                "an heirloom fit for a queen",
                "An heirloom fit for a queen was given — a treasure to outlive them both, and those who saw it never forgot it.",
                9, 10),
        };

        public static Tier Of(BetrothalGift gift) =>
            All.FirstOrDefault(t => t.Gift == gift) ?? All[0];

        public static int PriceOf(BetrothalGift gift) => Of(gift).Price;
        public static string NameOf(BetrothalGift gift) => Of(gift).GiftName;
    }
}
