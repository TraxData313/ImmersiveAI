using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Births
{
    /// <summary>One child of this birth. Twins are one record with two of these.</summary>
    public sealed class BirthChild
    {
        public string HeroId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsFemale { get; set; }
    }

    /// <summary>
    /// WHETHER HE HAS OWNED THE CHILD BEFORE THE WORLD (2026.08.15). Blood is the game's and is
    /// never touched: in vanilla's eyes the child is his and hers forever, inheritance and kin
    /// lines and all. HONOR is ours — what has been SAID, out loud, where people could hear it.
    /// The king's bastard whom everybody knows, against the king saying it aloud.
    ///
    /// <see cref="NeverArose"/> is 0 ON PURPOSE, so that both a record written before this existed
    /// and any value that fails to load land on the safe answer. A child of a marriage is his by
    /// the fact of the marriage and is never asked about.
    /// </summary>
    public enum Acknowledgement
    {
        NeverArose = 0,
        /// <summary>He said nothing. The world is left to talk, and it will.</summary>
        Withheld = 1,
        /// <summary>He owned the child before the world — quietly, or with a hall full of witnesses.</summary>
        Given = 2,
    }

    /// <summary>One soul who stood at the feast and carries the day — kept by id, like the
    /// wedding's, so the record can answer "was this asker there?" forever.</summary>
    public sealed class BirthWitness
    {
        public string HeroId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
    }

    /// <summary>
    /// The record of one child born to the player, kept forever (2026.08.10, Anton's ask — the
    /// wedding chronicle's own shape turned on the next day of a life). The wedding record is its
    /// pattern almost line for line, and the two differences are the ones that matter:
    ///
    /// • THE TWO PARTS ARE SPLIT IN TIME, not only in register. <see cref="BirthAccount"/> is
    ///   written the moment the child comes, because the hour happened whether or not anyone
    ///   feasted it. <see cref="FeastAccount"/> is written only if a feast is bought, and it may
    ///   be bought days later — a father away at war is offered it when he finally rides in.
    ///   So each part is guarded on ITS OWN content, never on the record existing.
    ///
    /// • THE PRIVACY LINE RUNS BETWEEN PARENTS AND WITNESSES, not between spouse and hall. The
    ///   hour of the birth belongs to the two who made the child; the feast belongs to everyone
    ///   who stood at it. <see cref="IsParent"/> is that rule, and the recall tool enforces it in
    ///   code exactly as the wedding's does.
    /// </summary>
    public sealed class BirthRecord
    {
        public string Id { get; set; } = string.Empty;

        public double GameDay { get; set; }
        /// <summary>The day in the world's own words ("Winter 12, Year 1084").</summary>
        public string DateText { get; set; } = string.Empty;
        /// <summary>Where the child was born ("the town of Akkalat"), or empty for the open road.</summary>
        public string PlaceName { get; set; } = string.Empty;
        public string CultureName { get; set; } = string.Empty;

        public string MotherId { get; set; } = string.Empty;
        public string MotherName { get; set; } = string.Empty;
        /// <summary>True when the player IS the mother — then no NPC holds the hour in their own
        /// voice, and the account lives in the record and the keepsake alone.</summary>
        public bool MotherIsPlayer { get; set; }

        public string FatherId { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public bool FatherIsPlayer { get; set; }

        /// <summary>How old the parents were ON THE DAY; 0 in records written before we kept it
        /// (2026.08.15, the wedding's own lesson). The account is fixed prose but the sheet around
        /// it is always today's, so a day called back twenty years on has nothing in it to say the
        /// two of them were young — unless the day itself said so.</summary>
        public int MotherAge { get; set; }
        public int FatherAge { get; set; }

        /// <summary>Whether the PLAYER was actually there when the child came — what the keepsake
        /// says to them, and nothing else.</summary>
        public bool PlayerWasThere { get; set; }

        /// <summary>Whether THE FATHER was there, which is a different question the moment the
        /// player is the mother: her husband may be three weeks' ride away while she is certainly
        /// present at her own labour. Conflating the two told the chronicler a man stood at a
        /// bedside he had never reached (2026.08.10 review).</summary>
        public bool FatherWasThere { get; set; }

        public List<BirthChild> Children { get; set; } = new List<BirthChild>();
        /// <summary>How many of the offspring did not live. The game rolls this per child.</summary>
        public int StillbornCount { get; set; }

        /// <summary>How many living children these two already had before this one; -1 unknown.</summary>
        public int OlderChildren { get; set; } = -1;
        /// <summary>How long they had been wed, in days; -1 when it cannot be told.</summary>
        public double MarriedDays { get; set; } = -1;

        public List<BirthWitness> Witnesses { get; set; } = new List<BirthWitness>();

        /// <summary>How great a feast was paid for; <see cref="BirthScale.Unfeasted"/> until one is,
        /// which is also what a birth nobody celebrated keeps forever.</summary>
        public BirthScale Scale { get; set; } = BirthScale.Unfeasted;
        /// <summary>What the feast cost the purse; 0 when there was none.</summary>
        public int FeastCost { get; set; }
        /// <summary>Whether the player has already been asked about a feast for this child, so a
        /// father who said no is not asked again every hour he stands beside her.</summary>
        public bool FeastOffered { get; set; }

        /// <summary>The day the feast was actually kept, in the world's own words. It is often NOT
        /// the day of the birth — a father away at war keeps it when he rides in — and the
        /// chronicler must be told the day it happened, not the day the child came.</summary>
        public string FeastDateText { get; set; } = string.Empty;
        /// <summary>The day of the feast, for reckoning how long the child had already been alive.</summary>
        public double FeastGameDay { get; set; } = -1;
        /// <summary>Where the feast was kept, when that is not where the child was born — a father
        /// away at war carries the child home before he feasts it, and an account set in the town
        /// the mother left three weeks earlier is simply wrong.</summary>
        public string FeastPlaceName { get; set; } = string.Empty;
        public string FeastCultureName { get; set; } = string.Empty;

        // ------------------------- blood is the game's, honor is ours (2026.08.15) -------------------------

        /// <summary>Whether the two of them were wed on the day. Such a child is his before the
        /// world by the fact of it and no choice ever arises. Old records load false — which is why
        /// <see cref="IsOwnedBeforeTheWorld"/> leans on <see cref="Acknowledgement.NeverArose"/>
        /// rather than on this, so a campaign's worth of existing children are not all quietly
        /// disowned by an update.</summary>
        public bool BornInMarriage { get; set; }

        /// <summary>What he said before the world. See <see cref="Acknowledgement"/>.</summary>
        public Acknowledgement Owned { get; set; } = Acknowledgement.NeverArose;

        /// <summary>The day he owned the child, when it was not the day it was born — the giving of
        /// the name, which may come at any age and is the heaviest thing on this road. -1 = never,
        /// or on the day itself.</summary>
        public double NameGivenDay { get; set; } = -1;
        public string NameGivenDateText { get; set; } = string.Empty;

        /// <summary>Whether the owning came LATE — years after, before everyone's eyes. It is a
        /// different act from owning a newborn and the world remembers which it was.</summary>
        public bool NameGivenLate { get; set; }

        /// <summary>
        /// Whether the world may call this child his. True for a child of a marriage, true once he
        /// has said so, and true for every record written before the question existed — only an
        /// explicit withholding reads as unowned, which is the one reading that must never happen
        /// by accident.
        /// </summary>
        public bool IsOwnedBeforeTheWorld => Owned != Acknowledgement.Withheld;

        /// <summary>Whether this child is waiting on a name it may still be given.</summary>
        public bool AwaitsTheName => Owned == Acknowledgement.Withheld && AnyLived;

        /// <summary>Part one: the hour itself, in the mother's own first person. The parents' alone.</summary>
        public string BirthAccount { get; set; } = string.Empty;
        /// <summary>Part two: the feast, as the hall tells it. Every witness carries this.</summary>
        public string FeastAccount { get; set; } = string.Empty;

        /// <summary>How many times the chronicler has been asked for each part, so a dying key is
        /// not hammered forever.</summary>
        public int BirthAttempts { get; set; }
        public int FeastAttempts { get; set; }

        /// <summary>True once the day has been laid before the player and written into births.txt.
        /// The day is complete at DIFFERENT moments depending on what was chosen — when the feast
        /// is written, or when the hour is and no feast will follow — and this is what keeps the
        /// popup and the readable log from happening twice for one child.</summary>
        public bool DaySealed { get; set; }

        /// <summary>True once the hour's beat is in the mother's memory.</summary>
        public bool BirthBeatDone { get; set; }
        /// <summary>True once the feast's beats have gone out to everyone who stood there.</summary>
        public bool FeastBeatDone { get; set; }

        /// <summary>Whether a child of this birth lived. A birth with none is recorded and grieved,
        /// never feasted and never written by the chronicler (see the module's handler).</summary>
        public bool AnyLived => Children != null && Children.Count > 0;

        /// <summary>Whether this soul made this child. The hour is theirs and no one else's.</summary>
        public bool IsParent(string? heroId) =>
            !string.IsNullOrEmpty(heroId)
            && (string.Equals(MotherId, heroId, StringComparison.Ordinal)
                || string.Equals(FatherId, heroId, StringComparison.Ordinal));

        /// <summary>Whether this soul lived this day at all — a parent, or one who stood at the feast.</summary>
        public bool WasThere(string? heroId) =>
            IsParent(heroId)
            || (!string.IsNullOrEmpty(heroId)
                && Witnesses != null
                && Witnesses.Any(w => w != null && string.Equals(w.HeroId, heroId, StringComparison.Ordinal)));

        /// <summary>The children's names as the world would say them ("Ira", "Ira and Tulag").</summary>
        public string ChildNames()
        {
            var names = Children?.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => c.Name.Trim()).ToList() ?? new List<string>();
            if (names.Count == 0) return "the child";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }

        /// <summary>"a son" / "a daughter" / "twin sons" / "a son and a daughter" — the plain words
        /// the notices and the beats are built from.</summary>
        public string ChildWords()
        {
            var kids = Children?.Where(c => c != null).ToList() ?? new List<BirthChild>();
            if (kids.Count == 0) return "a child";
            if (kids.Count == 1) return kids[0].IsFemale ? "a daughter" : "a son";
            if (kids.All(c => c.IsFemale)) return "twin daughters";
            if (kids.All(c => !c.IsFemale)) return "twin sons";
            return "a son and a daughter";
        }

        /// <summary>The day's own short name, for rolls, file names and log lines.</summary>
        public string Title()
        {
            var who = string.IsNullOrWhiteSpace(MotherName)
                ? $"the birth of {ChildNames()}"
                : $"the birth of {ChildNames()}, to {MotherName}";
            return string.IsNullOrWhiteSpace(PlaceName) ? who : $"{who}, in {PlaceName}";
        }
    }
}
