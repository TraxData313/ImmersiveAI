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

        public const string PopupReport =
            "\"Captain. Behind the stores, chained to a post, we found a girl. Alive. She will not speak to us.\"";

        public const string PopupNote =
            "Immersive AI: this meeting comes once in a campaign. Take her in and she joins your family, using no companion slot. Send her away and she is gone for good (Dev mode in the mod's settings can call the meeting again at the next hideout you clear).";

        /// <summary>The popup body: the report, a blank line, then the small note to the player.</summary>
        public static string PopupBody => PopupReport + "\n\n" + PopupNote;

        public const string PopupBringHer = "Bring her forward";

        public const string PopupLetHerGo = "Give her bread and let her go";

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
            "{player} cleared the bandits' den where I was held, and when I begged a place near him he took me into his own house as his handmaid. I owe him my life and my freedom.";

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
