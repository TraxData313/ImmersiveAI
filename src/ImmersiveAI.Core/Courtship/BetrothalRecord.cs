using System;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// The record of one betrothal of the player's, kept forever (2026.08.31, Anton's design —
    /// the proposal made an EVENT, "similar game like the wedding"): the plain facts of the day
    /// and the one written account of it.
    ///
    /// ONE PART, unlike the wedding's two, and private to the two of them by construction: no
    /// witnesses are gathered (a proposal is between two souls), the account beats into HER memory
    /// alone, and the recall tool answers nobody else. The player's own copy is the keepsake, the
    /// thread card, and betrothals.txt on disk.
    ///
    /// The account is written once by the chronicler's call and never rewritten; it also enters
    /// her memory as a silent beat, where time and compression fold it her own way — while the
    /// record keeps it whole forever.
    /// </summary>
    public sealed class BetrothalRecord
    {
        public string Id { get; set; } = string.Empty;

        public double GameDay { get; set; }
        /// <summary>The day in the world's own words ("Autumn 14, Year 1084").</summary>
        public string DateText { get; set; } = string.Empty;
        /// <summary>Where it happened ("the town of Onira"), or empty on the open road.</summary>
        public string PlaceName { get; set; } = string.Empty;
        public string CultureName { get; set; } = string.Empty;

        public string SpouseId { get; set; } = string.Empty;
        public string SpouseName { get; set; } = string.Empty;
        public bool SpouseIsFemale { get; set; } = true;
        /// <summary>What they were in the world that day ("a wanderer riding as your healer").</summary>
        public string SpouseStation { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;
        public bool PlayerIsFemale { get; set; }
        public string PlayerClanName { get; set; } = string.Empty;

        /// <summary>How old the two of them were ON THE DAY (the 2026.08.15 law: a day retold
        /// years later is told at the age it happened, never at today's).</summary>
        public int SpouseAge { get; set; }
        public int PlayerAge { get; set; }

        /// <summary>Days from the first step of the courtship road to this day; -1 when unknown.</summary>
        public double CourtshipDays { get; set; } = -1;
        /// <summary>The doubts she once set down and laid to rest — the truest material the
        /// chronicler has, for these are what the asking answered.</summary>
        public System.Collections.Generic.List<string> MisgivingsAnswered { get; set; }
            = new System.Collections.Generic.List<string>();

        /// <summary>Whether the PLAYER asked (the button's door) or she laid her promise first
        /// (her own hand in a talk or a letter). Both are true betrothals; the telling differs.</summary>
        public bool AskedByPlayer { get; set; }

        /// <summary>What was set on her hand; WordsAlone when nothing was.</summary>
        public BetrothalGift Gift { get; set; } = BetrothalGift.WordsAlone;
        /// <summary>What the gift cost the purse; 0 when none.</summary>
        public int GiftCost { get; set; }
        /// <summary>The gift as the day names it ("a ring of gold"); empty when none.</summary>
        public string GiftName { get; set; } = string.Empty;

        /// <summary>The player's own steering line for the asking, verbatim — kept in THEIR
        /// keepsake only, never read back to her (the account already wove it).</summary>
        public string PlayerWish { get; set; } = string.Empty;
        /// <summary>Her own word when SHE laid the promise; empty on the player's asking.</summary>
        public string HerWord { get; set; } = string.Empty;

        /// <summary>The account of the day, written once. Empty while it still awaits its story.</summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>Whether this soul lived this day — only the two of them ever did.</summary>
        public bool IsOfTheTwo(string? heroId) =>
            !string.IsNullOrEmpty(heroId) && string.Equals(SpouseId, heroId, StringComparison.Ordinal);

        /// <summary>The day's own short name, for the roll and file names.</summary>
        public string Title()
        {
            var who = string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(SpouseName)
                ? "the betrothal"
                : $"the betrothal of {PlayerName} and {SpouseName}";
            return string.IsNullOrWhiteSpace(PlaceName) ? who : $"{who}, in {PlaceName}";
        }
    }
}
