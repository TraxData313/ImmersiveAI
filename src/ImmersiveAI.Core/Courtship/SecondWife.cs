using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// MORE THAN ONE WIFE (2026.11.11, the owner's call, reversing the lovers-only decision of
    /// 2026.08.15): "If enabled, be able to get a second wife; if disabled, only lovers, so the
    /// players can choose."
    ///
    /// Every pure decision the setting touches lives here, so the gates can be tested as a function
    /// of it: with the setting OFF each answer is exactly what the road answered before the setting
    /// existed, and with it ON the player's own standing marriage is simply no longer a bar. The
    /// world's other laws (her own marriage, age, kind, kinship, war, the blessing, her misgivings)
    /// are untouched either way and stay where they were.
    /// </summary>
    public static class SecondWifeLaw
    {
        /// <summary>
        /// Whether the player's OWN standing marriage bars the step being reached for.
        /// <paramref name="forHand"/> is true for anything about a hand (readiness, the promise, the
        /// wedding, a blessing); the trunk's feeling-steps pass false. With the setting off this is
        /// the exact rule of 2026.08.15: the hand is barred, and the trunk too unless the lover's
        /// road is open. With it on, nothing about his marriage bars anything.
        /// </summary>
        public static bool PlayerMarriageBars(bool playerMarried, bool moreThanOneWife, bool forHand,
            bool loversRoadOpen)
        {
            if (!playerMarried) return false;
            if (moreThanOneWife) return false;
            return forHand || !loversRoadOpen;
        }

        /// <summary>Whether a NEW wedding may be performed now. Only a new one: a wife already wed
        /// stays a wife whatever the setting says later.</summary>
        public static bool MayWedNow(bool playerMarried, bool moreThanOneWife) =>
            !playerMarried || moreThanOneWife;

        /// <summary>Whether a lover's sheet carries the road toward a wedding. Off, a lover's road
        /// ends where it always did (one sheet, one truth: her lover's section says there is no
        /// wedding in it). On, the road may go on to a wedding, and she is told so by the road
        /// itself.</summary>
        public static bool RoadShownForLover(bool isLover, bool moreThanOneWife) =>
            !isLover || moreThanOneWife;

        /// <summary>
        /// What the lover's bond becomes on the day she is wed. A wife is not a lover: her lover's
        /// section would go on telling her "I am not his wife" beside a kin line that says she is.
        /// So the bond ends in favour of the marriage — None, not Former, because Former says "that
        /// is over now", and what happened is the opposite of over. Every other bond is left as it is.
        /// </summary>
        public static LoverBond BondAfterWedding(LoverBond before) =>
            before == LoverBond.Lover || before == LoverBond.Former ? LoverBond.None : before;
    }

    /// <summary>Every word the second-wife feature puts before anyone: the player's one-line walls
    /// and notices, a co-wife's true kinship word, and the plain household facts a wife is told.
    /// None of it tells anyone how to feel.</summary>
    public static class SecondWifeText
    {
        /// <summary>The setting's own name, exactly as the menu shows it.</summary>
        public const string SettingName = "More than one wife";

        /// <summary>The player's wall when his own marriage is what stands in the way (the setting
        /// off). Finishes "No marriage road can open with them — ..." and "... could not be sealed
        /// — ...". It names OUR setting: no other mod is the road any more.</summary>
        public static string PlayerWedWall(bool playerIsFemale) =>
            (playerIsFemale ? "you already have a husband" : "you already have a wife")
            + $"; turn on \"{SettingName}\" in the mod's settings to wed again";

        /// <summary>One line, once, when Marry Anyone is found loaded.</summary>
        public const string MarryAnyoneNotice =
            "Marry Anyone is installed, but Immersive AI's own \"" + SettingName + "\" setting is what allows a second wife.";

        /// <summary>The true word for the person who shares my spouse: "my husband's other wife"
        /// (and the same for a woman player's husbands). Never "my kinswoman".</summary>
        public static string CoSpouseWord(bool otherIsFemale) =>
            otherIsFemale ? "my husband's other wife" : "my wife's other husband";

        /// <summary>
        /// The household fact a wife carries in her kin block when she is not the only one: who the
        /// others are. Once, short, and a fact — nothing about what it means to her. Empty when she
        /// is the only one, so every single-wife sheet stays exactly as it was.
        /// </summary>
        public static string OtherSpousesLine(string spouseName, IReadOnlyList<string> otherNames, bool speakerIsFemale)
        {
            var names = (otherNames ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList();
            if (names.Count == 0) return string.Empty;
            var who = string.IsNullOrWhiteSpace(spouseName) ? (speakerIsFemale ? "My husband" : "My wife") : spouseName.Trim();
            var word = speakerIsFemale ? "wife" : "husband";
            var words = speakerIsFemale ? "wives" : "husbands";
            return names.Count == 1
                ? $"{who} has another lawful {word} besides me: {names[0]}."
                : $"{who} has other lawful {words} besides me: {JoinAnd(names)}.";
        }

        /// <summary>Opens the beat the other women of the house get on the day he weds again.
        /// NEVER reword.</summary>
        public const string HouseholdMark = "Word of the household:";

        /// <summary>
        /// What each other woman of his hearth learns the day he takes another lawful spouse: the
        /// fact, in her own first person, and nothing about how to hold it. No wound is marked and
        /// no door is touched by it — what she makes of it is hers.
        /// </summary>
        public static string FurtherWeddingBeat(string playerName, string newSpouseName, bool newSpouseIsFemale)
        {
            var who = string.IsNullOrWhiteSpace(playerName) ? "He" : playerName.Trim();
            var other = string.IsNullOrWhiteSpace(newSpouseName) ? (newSpouseIsFemale ? "another woman" : "another man") : newSpouseName.Trim();
            var role = newSpouseIsFemale ? "his lawful wife" : "her lawful husband";
            return $"{HouseholdMark} {who} has wed {other} this day; {other} is {role} now.";
        }

        /// <summary>The chronicler's fact for a further wedding: the marriage already standing,
        /// stated as a fact of the day and nothing more.</summary>
        public static string AlreadyWedFact(string playerName, IReadOnlyList<string> wedTo)
        {
            var names = (wedTo ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList();
            if (names.Count == 0) return string.Empty;
            var who = string.IsNullOrWhiteSpace(playerName) ? "The one being wed" : playerName.Trim();
            return $"{who} is already wed to {JoinAnd(names)}; this is a further marriage, lawful in their house, and that marriage stands.";
        }

        private static string JoinAnd(IReadOnlyList<string> items)
        {
            if (items.Count == 0) return string.Empty;
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return items[0] + " and " + items[1];
            return string.Join(", ", items.Take(items.Count - 1)) + " and " + items[items.Count - 1];
        }
    }
}
