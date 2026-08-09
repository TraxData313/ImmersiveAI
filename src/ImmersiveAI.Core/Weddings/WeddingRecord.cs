using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Weddings
{
    /// <summary>
    /// One soul who stood at the wedding and carries the day in their memory — kept by id so the
    /// record can answer "was this asker there?" long after names and parties have changed.
    /// </summary>
    public sealed class WeddingWitness
    {
        public string HeroId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>A word about who they were on that day ("your scout", "of your own clan") — the
        /// chronicler is given these so the account can name people as people, not as a list.</summary>
        public string Detail { get; set; } = string.Empty;
    }

    /// <summary>
    /// The record of one wedding of the player's, kept forever (2026.08.09, Anton's ask — "черешката
    /// на тортата"): the plain facts of the day, and the two written accounts of it.
    ///
    /// TWO PARTS, and the difference between them is a rule, not a detail:
    /// <see cref="FeastAccount"/> is the PUBLIC account of the day — every witness carries it, and
    /// it is written as a chronicle of this world, naming both wedded souls;
    /// <see cref="NightAccount"/> is the wedding night, written in the SPOUSE'S OWN first person and
    /// belonging to the two of them alone — it never reaches a witness's memory, and the recall tool
    /// refuses it to anyone but the spouse.
    ///
    /// Both are written once by the chronicler's call and never rewritten. They also enter memory as
    /// silent beats, where time and compression will fold them as any memory is folded — that is the
    /// point: she keeps the day her own way, while the record keeps it whole forever.
    /// </summary>
    public sealed class WeddingRecord
    {
        public string Id { get; set; } = string.Empty;

        public double GameDay { get; set; }
        /// <summary>The day in the world's own words ("Autumn 14, Year 1084").</summary>
        public string DateText { get; set; } = string.Empty;
        /// <summary>Where it happened ("the town of Onira"), or empty when they wed on the road.</summary>
        public string PlaceName { get; set; } = string.Empty;
        /// <summary>The place's culture, for the chronicler's texture ("Sturgian").</summary>
        public string CultureName { get; set; } = string.Empty;

        public string SpouseId { get; set; } = string.Empty;
        public string SpouseName { get; set; } = string.Empty;
        public bool SpouseIsFemale { get; set; } = true;
        /// <summary>What they were in the world that day ("a wanderer riding as your companion").</summary>
        public string SpouseStation { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;
        public bool PlayerIsFemale { get; set; }
        public string PlayerClanName { get; set; } = string.Empty;

        public List<WeddingWitness> Witnesses { get; set; } = new List<WeddingWitness>();

        /// <summary>Days from the first step of the courtship road to this day; -1 when unknown.</summary>
        public double CourtshipDays { get; set; } = -1;
        /// <summary>Days the betrothal stood before the wedding; -1 when unknown.</summary>
        public double BetrothalDays { get; set; } = -1;
        /// <summary>The head of her house who blessed the match, empty when none was needed.</summary>
        public string BlessedBy { get; set; } = string.Empty;
        /// <summary>The bride-price paid for that blessing; 0 when none.</summary>
        public int BridePrice { get; set; }
        /// <summary>The misgivings she once set down and laid to rest, in her own words — the
        /// truest material the chronicler has, for these are the doubts the day answered.</summary>
        public List<string> MisgivingsAnswered { get; set; } = new List<string>();

        /// <summary>How great a wedding was paid for (2026.08.09) — it decides how far the
        /// invitation reached and therefore who carries the day, and the chronicler is told of it.
        /// <see cref="WeddingScale.Unpaid"/> for a wedding sealed outside our own door.</summary>
        public WeddingScale Scale { get; set; } = WeddingScale.Unpaid;
        /// <summary>What the feast cost the player's purse; 0 when we sold them nothing.</summary>
        public int FeastCost { get; set; }

        /// <summary>Part one: the day, as the world tells it. Public to every witness.</summary>
        public string FeastAccount { get; set; } = string.Empty;
        /// <summary>Part two: the night, in the spouse's own voice. Theirs alone — never a witness's.</summary>
        public string NightAccount { get; set; } = string.Empty;

        /// <summary>Whether this soul lived this day (the spouse, or one of the witnesses).</summary>
        public bool WasThere(string? heroId) =>
            !string.IsNullOrEmpty(heroId)
            && (string.Equals(SpouseId, heroId, StringComparison.Ordinal)
                || Witnesses.Any(w => w != null && string.Equals(w.HeroId, heroId, StringComparison.Ordinal)));

        /// <summary>Whether this soul may remember the night as well as the day.</summary>
        public bool IsSpouse(string? heroId) =>
            !string.IsNullOrEmpty(heroId) && string.Equals(SpouseId, heroId, StringComparison.Ordinal);

        /// <summary>The day's own short name, for rolls and file names ("the wedding of Eren and
        /// Sibylla, in the town of Onira").</summary>
        public string Title()
        {
            var who = string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(SpouseName)
                ? "the wedding"
                : $"the wedding of {PlayerName} and {SpouseName}";
            return string.IsNullOrWhiteSpace(PlaceName) ? who : $"{who}, in {PlaceName}";
        }
    }
}
