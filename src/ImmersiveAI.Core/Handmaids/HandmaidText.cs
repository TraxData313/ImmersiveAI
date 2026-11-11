using System;
using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Handmaids
{
    /// <summary>
    /// Every player-visible word of the captive-in-the-den meeting, in one place so it is easy to
    /// rewrite: the popup, the scripted talk, the dev pick list and the notices. Her own lines
    /// per girl live on <see cref="HandmaidGirl"/>; the shared ones live here. Anything with
    /// {title} is filled through <see cref="Fill"/>.
    /// </summary>
    public static class HandmaidText
    {
        /// <summary>What she calls the player in the scripted meeting.</summary>
        public const string ScriptedTitle = "my lord";

        // ── The numbers of the meeting ──────────────────────────────────────────────────────

        /// <summary>Her age when she is found.</summary>
        public const int Age = 20;

        /// <summary>Her standing with the player the moment she is taken in.</summary>
        public const int RelationOnJoin = 30;

        /// <summary>The silver for the road when he cannot take her.</summary>
        public const int RoadSilver = 100;

        // ── The popup (the men's report) ────────────────────────────────────────────────────

        public const string PopupTitle = "A captive in the den";

        public const string PopupReport = "\"" + SoldierReport + "\"";

        public const string PopupNote =
            "Immersive AI: this meeting comes once in a campaign. Take her in and she joins your family, using no companion slot. Send her away and she is gone for good (Dev mode in the mod's settings can call the meeting again at the next hideout you clear).";

        /// <summary>The popup body: the report, a blank line, then the small note to the player.</summary>
        public static string PopupBody => PopupReport + "\n\n" + PopupNote;

        public const string PopupBringHer = "Bring her forward";

        public const string PopupLetHerGo = "Give her bread and let her go";

        // ── The soldier's talk (one of his own men tells it; the popup is the fallback) ──────

        /// <summary>The soldier's opening report, the first time.</summary>
        public const string SoldierReport =
            "Captain. Behind the stores, chained to a post, we found a girl. Alive. She will not speak to us.";

        /// <summary>The soldier's opening when the talk opens again (dev: back from her).</summary>
        public const string SoldierAgain = "Captain?";

        public const string PlayerBringHer = "Bring her forward.";

        public const string PlayerLetHerGo = "Give her bread and let her go.";

        public const string PlayerAskMeeting = "[Immersive AI] What is this meeting?";

        /// <summary>The note to the player, shown as the soldier's line but plainly a note.</summary>
        public const string SoldierNote =
            "(Immersive AI: this meeting comes once in a campaign. Take her in and she joins your family, using no companion slot. Send her away, or let her go now, and she is gone for good. Dev mode in the mod's settings can call the meeting again.)";

        public const string SoldierAsYouSay = "As you say, captain.";

        /// <summary>The soldier's opening: the full report the first time, a short word after.</summary>
        public static string SoldierOpening(bool again) => again ? SoldierAgain : SoldierReport;

        /// <summary>Dev: one "bring her" option per girl, e.g. "Bring Yasmin (Aserai) forward. (this hideout's own)".</summary>
        public static string DevBringLabel(HandmaidGirl girl, bool isThisDensOwn) =>
            girl == null ? PlayerBringHer
                : $"Bring {girl.Name} ({girl.CultureName}) forward." + (isThisDensOwn ? DevOwnMark : string.Empty);

        // ── The scripted talk ──────────────────────────────────────────────────────────────

        public const string Kneel = "*She kneels.* You have slain them, {title}.";

        public const string Plea =
            "I have no house to return to, and no one who will ask after me. Take me with you, {title}. I will be your faithful handmaid. I ask nothing but a place near you.";

        public const string PlayerAccept = "Rise. You are of my house now.";

        public const string HerAccepted = "Then I am yours, {title}. Where you go, I will go.";

        public const string PlayerAskSkill = "What can you do?";

        public const string PlayerAskTeacher = "Who taught you to be so gentle, in a place like this?";

        public const string PlayerRefuse = "I cannot take you. Here is silver for the road.";

        public const string HerRefused = "As my lord wills. God keep you for what you did today.";

        /// <summary>Her one silent first-person memory of the day she was taken in.</summary>
        public const string JoinMemory =
            "{player} and his men stormed the bandits' den where I was held captive, and slew the men who kept me. " +
            "His men found me chained behind the stores and brought me before him. I knelt and begged a place near him, " +
            "and he took me into his own house as his handmaid. Until that hour I was the bandits' captive and served only them; " +
            "all I do in his service begins from here. I owe him my life and my freedom.";

        // ── Her own sheet: the quiet spirit (2026.11.11, the prompt step) ───────────────────
        //
        // Her OWN section of the LLM sheet, first person, placed as late as the sheet allows (after
        // the speech rules, before the player's own words) so it has the last say on her manner.
        // Slots: {name} {player} {teacher} {native}. 1 Peter 3:4 and Proverbs 10:19 in her own words.

        /// <summary>Line 1 while she is his handmaid, not yet his wife.</summary>
        public const string SpiritOpeningUnwed =
            "I am {name}, handmaid of {player}, who took me out of the bandits' den{when} when I had no one. I am his, and glad of it. It is {player} himself who speaks with me now.";

        /// <summary>Line 1 once she is his wife.</summary>
        public const string SpiritOpeningWed =
            "I am {name}, wife of {player}, who took me out of the bandits' den{when} when I had no one and made me his own. I was his handmaid first, and I am still glad to serve him. It is {player} himself who speaks with me now.";

        /// <summary>The middle lines, the same wed or unwed.</summary>
        public const string SpiritBody =
            "{teacher} taught me that a meek and quiet spirit is of great price, and that in many words there is no lack of fault. So I keep my words few and plain, and I do not advise or lecture unless he asks. When he bids me speak freely I do, gladly. A long tale I tell a piece at a time, and wait for his word to go on.\n" +
            "I was taught to call a husband lord, so I call him my lord, my master, my husband, or {native}, now one and now another; his name too, when it is tender.\n" +
            "What he bids, I do gladly. If I fear for him I say so once, softly, and yield.";

        /// <summary>The last line while she is his handmaid.</summary>
        public const string SpiritClosingUnwed =
            "I love him and I hope to be his wife. I do not hide it: I show it in small things, and when he opens the way I walk it gladly.";

        /// <summary>The last line once she is his wife.</summary>
        public const string SpiritClosingWed =
            "I love him, and I am his wife. I do not hide it: I show it in small things, every day.";

        /// <summary>The whole section, every slot filled. A blank player name reads "my lord", a
        /// blank teacher "My mother", so no raw brace ever reaches her sheet.</summary>
        public static string SpiritFor(HandmaidGirl girl, string? playerName, bool wed,
            string? ownName = null, double daysSinceTaken = -1)
        {
            if (girl == null) return string.Empty;
            // The name she carries NOW (the player may rename her), not the roster's.
            var name = string.IsNullOrWhiteSpace(ownName) ? girl.Name : ownName!.Trim();
            var text = (wed ? SpiritOpeningWed : SpiritOpeningUnwed) + "\n" + SpiritBody + "\n"
                       + (wed ? SpiritClosingWed : SpiritClosingUnwed);
            var player = string.IsNullOrWhiteSpace(playerName) ? ScriptedTitle : playerName!.Trim();
            var teacher = string.IsNullOrWhiteSpace(girl.TaughtBy) ? "My mother" : girl.TaughtBy.Trim();
            var native = string.IsNullOrWhiteSpace(girl.NativeTitle) ? ScriptedTitle : girl.NativeTitle.Trim();
            return text
                .Replace("{name}", name)
                .Replace("{when}", SinceTaken(daysSinceTaken))
                .Replace("{player}", player)
                .Replace("{teacher}", teacher)
                .Replace("{native}", native);
        }

        /// <summary>The length of a Calradian year in days (4 seasons of 21).</summary>
        public const int DaysInYear = 84;

        /// <summary>
        /// HOW LONG AGO he took her from the den, as the clause that follows "the bandits' den"
        /// in her opening line (2026.11.11: freed minutes before, she answered "near three years
        /// since you broke that den": nothing on her sheet said when, and the rest of it read as
        /// a settled life). Empty when the day is unknown, so an old save reads as it did.
        /// </summary>
        public static string SinceTaken(double days)
        {
            if (days < 0 || double.IsNaN(days)) return string.Empty;
            if (days < 1) return " this very day,";
            if (days < 2) return " only yesterday,";
            if (days < 21) return $" {(int)days} days ago,";
            if (days < DaysInYear) return $" some {(int)(days / 7)} weeks ago,";
            int years = (int)(days / DaysInYear);
            return years <= 1 ? " a year ago," : $" some {years} years ago,";
        }

        /// <summary>The label before her teacher's words in her seeded deep memory.</summary>
        public const string SeedTeacherLead = "Of the one who taught me: ";

        /// <summary>Her deep-memory seed: her own first-person past, in place of the world's
        /// encyclopedia paragraph a Lord would otherwise be seeded with.</summary>
        public static string MemorySeedFor(HandmaidGirl girl)
        {
            if (girl == null) return string.Empty;
            return StoryLine(girl).Trim() + "\n" + SeedTeacherLead + TeacherLine(girl).Trim();
        }

        // ── Her road to the wedding (2026.11.11, the courtship step) ────────────────────────
        //
        // The SAME road as anyone's, only gentler. These lines REPLACE the general wording where
        // the road section and the courtship hands tell an ordinary woman how to treat her doubts
        // and her steps; they are never added beside it (the token diet).

        /// <summary>How she treats her doubts: in place of the ordinary "only life answers a doubt".</summary>
        public const string RoadDoubtsWay =
            "My doubts are small ones, the kind a glad heart has. I ask each once, simply, when a tender moment comes; his plain kind word is answer enough, and I lay it down gladly and do not take it up again.";

        /// <summary>How she treats her steps: in place of the ordinary "only when real talks have earned it".</summary>
        public const string RoadStepsWay =
            "I do not make him wait. The moment I see he wishes it, I take the next step toward him myself.";

        /// <summary>The head of her standing list (the ordinary one carries the "never for a warm
        /// word" rule, which her doubts do not follow).</summary>
        public const string RoadStandingHead = "My small doubts about a life wed to {player}, set down by my own hand:";

        /// <summary>Her hour to choose them, in place of the ordinary invitation to weigh her heart.</summary>
        public const string RoadChooseNow =
            "My heart is given, and I have not yet set down my small doubts. Now is the hour: I set down the three truest to me.";

        /// <summary>When none of her small doubts stands any more.</summary>
        public const string RoadAllAnswered = "None of my small doubts stands now, so nothing of mine bars the road.";

        /// <summary>While one still stands (the code's rail, said where she reads).</summary>
        public const string RoadHandWaits = "While one still stands, my hand waits.";

        // ── Her role line (PersonaBuilder) ──────────────────────────────────────────────────

        /// <summary>
        /// The opening of her role line, in the shape of everyone's ("A Sturgian noble of clan
        /// ..."). The game makes her a Lord so she can be family without a companion slot, and the
        /// ordinary line would call her a noble of his clan, which is false. The age is added after
        /// by the builder, as for everyone.
        /// </summary>
        public static string RoleLine(string? culture, string? playerName, bool wed)
        {
            var people = string.IsNullOrWhiteSpace(culture) ? string.Empty : culture!.Trim() + " ";
            var player = string.IsNullOrWhiteSpace(playerName) ? "my lord" : playerName!.Trim();
            return $"A {people}woman of {player}'s household, {(wed ? "his wife" : "his handmaid")}";
        }

        // ── The honest wall for a married player (the Between-us page) ──────────────────────

        /// <summary>
        /// Phrased for the player, finishing "Nothing can be sealed while ...". Vanilla allows one
        /// wife; since 2026.11.11 the mod's own "More than one wife" setting lifts that (no other mod
        /// is the road any more), and the road should say so instead of letting him court her into a
        /// road with no end.
        /// </summary>
        public const string MarriedWallForPlayer =
            "you already have a wife; turn on \"" + Courtship.SecondWifeText.SettingName + "\" in the mod's settings to wed her";

        // ── Notices to the player ──────────────────────────────────────────────────────────

        public const string JoinedNotice = "{name} has joined your family.";

        public const string LetGoNotice = "Your men give her bread and loose her chains. She is gone.";

        // ── Dev mode ───────────────────────────────────────────────────────────────────────

        public const string DevPickTitle = "Which captive? (dev)";

        public const string DevPickBody = "Dev mode: choose the girl your men find in this den.";

        public const string DevPickAffirm = "This one";

        public const string DevPickCancel = "None";

        public const string DevOwnMark = " (this hideout's own)";

        public const string DevLever =
            "Let us part now. [Immersive AI • test — call the captive from the den]";

        /// <summary>Dev, in her options: she steps back and the soldier shows the others.</summary>
        public const string DevStepBack = "[Immersive AI • test] Step back. Show me the others.";

        /// <summary>The talk screen's Dev panel button.</summary>
        public const string DevCallButton = "Call the captive (as if a hideout was just cleared)";

        /// <summary>When the dev button is pressed with the feature switched off.</summary>
        public const string DevCallOffNotice =
            "Immersive AI: the captive in the den is switched off in the mod's settings.";

        // ── Helpers ────────────────────────────────────────────────────────────────────────

        /// <summary>Fills {title} (and {player}, {name} when given). A blank title falls back to
        /// <see cref="ScriptedTitle"/>, so a line never shows a raw placeholder.</summary>
        public static string Fill(string? text, string? title = null, string? player = null, string? name = null)
        {
            var s = text ?? string.Empty;
            var t = string.IsNullOrWhiteSpace(title) ? ScriptedTitle : title!.Trim();
            s = s.Replace("{title}", t);
            if (player != null) s = s.Replace("{player}", player);
            if (name != null) s = s.Replace("{name}", name);
            return s;
        }

        public static string KneelLine() => Fill(Kneel);
        public static string PleaLine() => Fill(Plea);
        public static string AcceptedLine() => Fill(HerAccepted);
        public static string RefusedLine() => Fill(HerRefused);
        public static string StoryLine(HandmaidGirl girl) => Fill(girl.Story);
        public static string SkillLine(HandmaidGirl girl) => Fill(girl.Skill);
        public static string TeacherLine(HandmaidGirl girl) => Fill(girl.Teacher);

        public static string JoinMemoryFor(string? playerName) =>
            Fill(JoinMemory, player: string.IsNullOrWhiteSpace(playerName) ? "My lord" : playerName!.Trim());

        public static string JoinedNoticeFor(string? name) => Fill(JoinedNotice, name: name ?? string.Empty);

        /// <summary>"Yasmin (Aserai)", plus the den mark for this hideout's own girl.</summary>
        public static string PickLabel(HandmaidGirl girl, bool isThisDensOwn) =>
            $"{girl.Name} ({girl.CultureName})" + (isThisDensOwn ? DevOwnMark : string.Empty);

        private static readonly Regex AgeAttribute =
            new Regex("\\bage\\s*=\\s*['\"]([0-9]+(?:\\.[0-9]+)?)['\"]", RegexOptions.CultureInvariant);

        /// <summary>Her age when found: the age written in her face code (a face is modelled at an
        /// age), held to 18..40; the common <see cref="Age"/> when she has no code or none is read.</summary>
        public static int AgeOf(HandmaidGirl girl)
        {
            var m = AgeAttribute.Match(girl?.FaceCode ?? string.Empty);
            if (!m.Success) return Age;
            if (!double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var a)) return Age;
            var n = (int)Math.Round(a);
            return n < 18 ? 18 : n > 40 ? 40 : n;
        }

        private static readonly Regex KeyAttribute =
            new Regex("key\\s*=\\s*['\"]([0-9A-Fa-f]+)['\"]", RegexOptions.CultureInvariant);

        /// <summary>
        /// A cheap shape check before a face code is handed to the game: a BodyProperties element
        /// with a hex key. It does not prove the game will accept it; the glue still guards the
        /// parse and falls back to the random face.
        /// </summary>
        public static bool LooksLikeFaceCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            var s = code!.Trim();
            if (s.IndexOf("<BodyProperties", StringComparison.OrdinalIgnoreCase) < 0) return false;
            var m = KeyAttribute.Match(s);
            return m.Success && m.Groups[1].Value.Length >= 16;
        }
    }
}
