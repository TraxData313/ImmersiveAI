using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Nights
{
    /// <summary>
    /// What a man may lay out for a night with his wife, and what the coin actually buys
    /// (2026.08.09, Anton's design — the wedding tiers' little brother).
    ///
    /// The money buys TWO things and neither of them is a bigger adjective: it buys the night's
    /// odds — a woman courted is a woman disposed — and it buys the night a WRITING, a short
    /// account she keeps and a name she remembers it by. A night given nothing but yourself is a
    /// real night and is recorded as one; it simply passes without a chronicler.
    ///
    /// The prices are Anton's own rails, ten denars to a thousand, and the multipliers walk with
    /// them: a cup of wine is a courtesy, a jewel is a campaign. What it costs BESIDES the coin is
    /// the morning — a party that lingered breaks camp disorganized, and that is the whole trade.
    /// </summary>
    public static class NightGifts
    {
        public sealed class Tier
        {
            public int Price { get; }
            /// <summary>The short name the player picks by.</summary>
            public string Name { get; }
            /// <summary>What the player is told the coin buys, in plain in-world words.</summary>
            public string PlayerDescription { get; }
            /// <summary>
            /// What the chronicler is TOLD was there — bare facts, never a written sentence
            /// (2026.08.10, Anton: "да не стане репетитив след време"). These used to be finished
            /// prose, and finished prose handed to a model comes back almost word for word: every
            /// ten-denar night in a marriage would have read the same by the tenth one. A handful
            /// of nouns leaves the writing to the writer, and the prompt says plainly that these
            /// are the facts and not the phrasing.
            /// </summary>
            public string ChroniclerNote { get; }
            /// <summary>What it does to the night's odds.</summary>
            public double Multiplier { get; }

            /// <summary>
            /// How long an account this night earns, in sentences (2026.08.10, Anton's design —
            /// "по-скъпо организирана нощ, по-дълго описание за нея, по-специална"). The coin has
            /// always bought the odds and the fact of a writing; now it buys the LENGTH of that
            /// writing too, and length here is not padding: the prompt spends the room evenly on
            /// the two halves of a night, so a grander evening simply has more of both — more of
            /// what was made ready, and more of what followed.
            ///
            /// Anton's own rails are ten denars → three or four sentences, a hundred → five or
            /// six, a thousand → seven or eight; the three-hundred tier interpolates between its
            /// neighbours. The ceiling deliberately stays under the wedding night's twelve: that
            /// one is the once, and nothing after it should ever read as long.
            /// </summary>
            public int MinSentences { get; }
            public int MaxSentences { get; }

            /// <summary>
            /// What it does to the odds that the OTHER wives hear of it (2026.08.09, Anton's rule —
            /// and it is the sharpest edge in the whole feature). A night of nothing but yourselves
            /// halves the talk; a jug of wine is already noticed; and a thousand-denar jewel doubles
            /// it, because a jewel is worn where the world can see it and servants carry water up
            /// the stairs in front of everyone. The coin buys the odds of a child and the quality of
            /// the memory — and pays for both in the other women knowing.
            /// </summary>
            public double AwarenessMultiplier { get; }

            /// <summary>Whether this night is written down. Every night that cost something is.</summary>
            public bool WritesStory => Price > 0;

            internal Tier(int price, string name, string playerDescription, string chroniclerNote,
                double multiplier, double awarenessMultiplier,
                int minSentences = 3, int maxSentences = 4)
            {
                Price = price; Name = name; PlayerDescription = playerDescription;
                ChroniclerNote = chroniclerNote; Multiplier = multiplier;
                AwarenessMultiplier = awarenessMultiplier;
                MinSentences = minSentences; MaxSentences = maxSentences;
            }
        }

        /// <summary>The night that costs nothing: no coin, no writing, no lingering in the morning.</summary>
        public static readonly Tier Plain = new Tier(0, "Nothing but yourself",
            "You go to her as you are. No wine, no gift, nothing prepared — only the two of you and the night. It is remembered, but not written, and it draws no eyes.",
            string.Empty, 1.00, 0.50);

        // The chronicler's notes below are FACTS, deliberately terse. See Tier.ChroniclerNote.

        public static readonly IReadOnlyList<Tier> All = new[]
        {
            Plain,

            new Tier(10, "A cup of wine",
                "A jug of decent wine and bread set aside for the two of you. Small, but chosen — and somebody fetched it. She will keep a few lines of it.",
                "wine and bread set aside beforehand; ten denars' worth — small, and chosen",
                1.10, 0.75, minSentences: 3, maxSentences: 4),

            new Tier(100, "Hot water, oil, and a table for two",
                "Water carried up and heated, oil for her hair, and a supper laid for two with no one else at the table. Half the household is involved, and she will remember it at length.",
                "water carried and heated, oil for her hair, a supper laid for two only; a hundred denars — an evening made, not stumbled into",
                1.35, 1.10, minSentences: 5, maxSentences: 6),

            new Tier(300, "Cloth for a new gown",
                "Good cloth, dyed, and a seamstress paid to have it ready — a thing she will wear where others see her, and they will ask.",
                "good dyed cloth and a seamstress already paid; three hundred denars — a gift meant to be worn where others will see it",
                1.60, 1.50, minSentences: 6, maxSentences: 7),

            new Tier(1000, "A jewel",
                "A jewel chosen for her — the kind a woman wears once and is remembered in. A thousand denars, and everyone will know what it cost, including your other wives. Of all the nights, this is the one she writes down fullest.",
                // NOT "a thousand denars laid down before anything else" — live-probed three times
                // on luna and all three put a PURSE OF COINS on the table beside the jewel, which
                // is a merchant's gesture and not a husband's. The price is a MEASURE of the gift
                // here, never a prop in the room.
                "a jewel chosen for her, worth a thousand denars — the kind a woman is remembered wearing; it was waiting where she would see it first",
                2.00, 2.00, minSentences: 7, maxSentences: 8),
        };

        public static Tier? Of(int price) => All.FirstOrDefault(t => t.Price == price);

        /// <summary>The tier for a price, falling back to the plain night for anything unknown —
        /// a record written by an older version must never resolve to nothing.</summary>
        public static Tier Resolve(int price) => Of(price) ?? Plain;

        /// <summary>Every tier that costs coin, cheapest first — what the player is offered.</summary>
        public static IReadOnlyList<Tier> Paid => All.Where(t => t.Price > 0).ToList();
    }
}
