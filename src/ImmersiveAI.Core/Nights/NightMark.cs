using System;

namespace ImmersiveAI.Core.Nights
{
    /// <summary>
    /// THE THIN TRACE OF ONE EVENING (2026.08.11, Anton's design — "така ще може да запазим дълго
    /// време на ниска цена"). A wife keeps her last FORTNIGHT of nights in full, because that is
    /// what she can still feel; but she also knows, the way anyone knows about their own marriage,
    /// roughly how the last month went — how often he came, how often he made something of it, how
    /// often she heard he was elsewhere and with whom.
    ///
    /// Holding thirty days of full records to answer that would triple the ledger and put a heap of
    /// prose in a file for the sake of five numbers. So a mark is kept instead: seven small fields,
    /// no prose at all, pruned by DAYS rather than by count. A hundred of these cost less than one
    /// written night, and they outlive the records they were made beside — which is the whole
    /// point, since the reckoning is about the nights she can no longer recite.
    /// </summary>
    public sealed class NightMark
    {
        public double GameDay { get; set; }
        public string WifeId { get; set; } = string.Empty;
        public NightKind Kind { get; set; } = NightKind.Unknown;

        /// <summary>What was laid out for it; 0 for a night that cost nothing.</summary>
        public int GiftPrice { get; set; }

        /// <summary>The name she keeps it by, when it was written down at all. For a night she only
        /// HEARD of, this is what she heard the OTHER night was called — the leak's own gift.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Who he was with, for a night she learned he spent elsewhere.</summary>
        public string OtherName { get; set; } = string.Empty;

        /// <summary>Whether a child began that night — the one fact no tally should ever lose.</summary>
        public bool Conceived { get; set; }

        /// <summary>What was laid out for the OTHER night she heard of — how loudly it was talked
        /// about, and therefore how much of it she has.</summary>
        public int OtherNightPrice { get; set; }

        /// <summary>The mark left by a night, whatever kind of night it was.</summary>
        public static NightMark From(NightRecord night)
        {
            if (night == null) throw new ArgumentNullException(nameof(night));
            return new NightMark
            {
                GameDay = night.GameDay,
                WifeId = night.WifeId ?? string.Empty,
                Kind = night.Kind,
                GiftPrice = night.GiftPrice,
                // Hers when she has one, else whatever she heard the other night was called.
                Title = !string.IsNullOrWhiteSpace(night.Title) ? night.Title.Trim()
                      : (night.OtherNightTitle ?? string.Empty).Trim(),
                OtherName = (night.OtherName ?? string.Empty).Trim(),
                Conceived = night.Conceived,
                OtherNightPrice = night.OtherNightPrice,
            };
        }

        /// <summary>Whether this mark is for the same wife and the same evening as that record —
        /// how a re-settled evening replaces its mark instead of doubling it.</summary>
        public bool IsSameEveningAs(NightRecord night) =>
            night != null
            && string.Equals(WifeId, night.WifeId ?? string.Empty, StringComparison.Ordinal)
            && Math.Floor(GameDay) == Math.Floor(night.GameDay)
            && Kind == night.Kind;
    }
}
