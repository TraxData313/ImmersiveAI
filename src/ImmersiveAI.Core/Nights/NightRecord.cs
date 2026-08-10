using System;

namespace ImmersiveAI.Core.Nights
{
    /// <summary>What one night was, from ONE woman's side of it. A night the player spends with
    /// Sibylla writes a <see cref="Together"/> for her and, for every other wife, whatever she
    /// came to know of it — which is usually less than the truth, and sometimes nothing.</summary>
    public enum NightKind
    {
        /// <summary>He came to me.</summary>
        Together = 0,
        /// <summary>My door was closed to him: the custom of women was upon me.</summary>
        DoorClosed = 1,
        /// <summary>I learned he spent that night with another of his wives.</summary>
        Elsewhere = 2,
        /// <summary>I learned he spent that night alone.</summary>
        Alone = 3,
        /// <summary>I never learned where he laid his head.</summary>
        Unknown = 4,
    }

    /// <summary>
    /// One night, as one woman keeps it (2026.08.09). Written for every wife every night the
    /// question arises, so that fourteen of them read back as a fortnight of a marriage: the nights
    /// he came, the nights her door was closed, the nights she learned he was elsewhere, and the
    /// nights she simply did not see him come in.
    ///
    /// A night that cost coin also carries its own short account and the NAME she remembers it by.
    /// The name is the part that lasts: the full account is shown for the freshest few and then
    /// folds away to its title, and it is the title alone that goes into her memory as a beat —
    /// the flesh lives in this ledger, the essence in her.
    /// </summary>
    public sealed class NightRecord
    {
        public string Id { get; set; } = string.Empty;

        public double GameDay { get; set; }
        /// <summary>The day in the world's own words ("Autumn 14, Year 1084").</summary>
        public string DateText { get; set; } = string.Empty;

        /// <summary>Whose night this record belongs to.</summary>
        public string WifeId { get; set; } = string.Empty;
        public string WifeName { get; set; } = string.Empty;

        public NightKind Kind { get; set; } = NightKind.Unknown;

        /// <summary>Where it happened ("the town of Onira", "our camp on the road"); empty when she
        /// knows only that it happened somewhere.</summary>
        public string PlaceName { get; set; } = string.Empty;
        /// <summary>"town" / "castle" / "village" / "camp" / "sea" — what kind of place it was, which
        /// is also what decides how easily the other wives came to hear of it.</summary>
        public string PlaceKind { get; set; } = string.Empty;

        /// <summary>For <see cref="NightKind.Elsewhere"/>: whom he was with.</summary>
        public string OtherName { get; set; } = string.Empty;

        /// <summary>For <see cref="NightKind.Elsewhere"/>: the NAME that other night was given, when
        /// it was one he paid for and the word of it carried this far (2026.08.09, Anton — "това ще
        /// създаде истински хубави проблеми за многоженците"). A grand night is grand precisely
        /// because people talk about it, and what they repeat is its name. Filled in after the fact,
        /// when the chronicler hands the name back.</summary>
        public string OtherNightTitle { get; set; } = string.Empty;

        /// <summary>What HE laid out for that other night — nothing to do with her own purse, and
        /// the measure of how much of it reached her (2026.08.11, Anton: "с повече детайли колкото
        /// по грандиозна е била"). A jug of wine passes almost unremarked; a jewel is worn where
        /// the world can see it, and the whole house has an opinion about it by morning.</summary>
        public int OtherNightPrice { get; set; }
        /// <summary>Whether she learned it by hearsay rather than by seeing it — a wife a few days'
        /// ride away hears things, and says so plainly when she does.</summary>
        public bool ByHearsay { get; set; }

        /// <summary>Whether the company was at war that night — an honest reason not to have looked
        /// for him, and one she would never hold against him.</summary>
        public bool AtWar { get; set; }

        /// <summary>What was laid out for the night, in denars; 0 for a night that cost nothing.</summary>
        public int GiftPrice { get; set; }
        /// <summary>The gift in a word, kept on the record so an old night still reads right after
        /// the tiers are ever re-tuned.</summary>
        public string GiftName { get; set; } = string.Empty;

        /// <summary>The name she remembers this night by; empty for a night nobody wrote down.</summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>The short account of it, in her own first person. Empty until the chronicler
        /// answers — and a night saved before that answer must never be read as one that has none
        /// (the wedding's own lesson: guard on CONTENT, never on existence).</summary>
        public string Story { get; set; } = string.Empty;
        /// <summary>Whether an account was asked for at all. True with an empty <see cref="Story"/>
        /// means the chronicler still owes us one.</summary>
        public bool StoryWanted { get; set; }
        /// <summary>How many times we have gone back to ask for it; a night is not retried forever.</summary>
        public int StoryAttempts { get; set; }

        /// <summary>Where her body's season stood, in a phrase, for the record and the player's eye.</summary>
        public string SeasonWord { get; set; } = string.Empty;

        /// <summary>The reckoned chance this night would quicken, 0..1; -1 when none was rolled.</summary>
        public double Chance { get; set; } = -1;
        /// <summary>Whether it did.</summary>
        public bool Conceived { get; set; }
        /// <summary>The day she comes to know of it — a woman does not learn the same evening. Until
        /// then the world knows nothing either: the game's own pregnancy is not applied yet.</summary>
        public double RevealDay { get; set; } = -1;
        /// <summary>Whether that day has come and been acted on.</summary>
        public bool Revealed { get; set; }

        /// <summary>True once this night's one beat has been set down in her memory.</summary>
        public bool BeatDone { get; set; }

        /// <summary>A night with an account still owed, and still worth asking for.</summary>
        public bool NeedsStory(int maxAttempts) =>
            Kind == NightKind.Together && StoryWanted
            && string.IsNullOrWhiteSpace(Story) && StoryAttempts < maxAttempts;

        /// <summary>A night that was written down and has its writing.</summary>
        public bool IsStoried =>
            Kind == NightKind.Together && !string.IsNullOrWhiteSpace(Story);

        /// <summary>A conception waiting for the day she learns of it.</summary>
        public bool AwaitsReveal(double today) =>
            Conceived && !Revealed && RevealDay >= 0 && today >= RevealDay;

        /// <summary>The night's own short name for files and rolls.</summary>
        public string Describe()
        {
            var where = string.IsNullOrWhiteSpace(PlaceName) ? string.Empty : $", in {PlaceName.Trim()}";
            var when = string.IsNullOrWhiteSpace(DateText) ? $"day {(int)GameDay}" : DateText.Trim();
            return $"{when}{where}";
        }
    }
}
