using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// Every word of the betrothal chronicle (2026.08.31, Anton's design — the proposal made an
    /// EVENT): the one prompt the chronicler is given, the beat that carries the day into her
    /// memory, and the account a soul reads when the day is called back.
    ///
    /// THE REGISTER is Scripture's own betrothal narratives — the servant asking for Rebekah at
    /// the well and her plain "I will go"; Jacob, whose seven years of service seemed to him but
    /// a few days for the love he had to her. Third person, one part only, and PRIVATE to the two
    /// of them: no hall saw it, no witness carries it.
    ///
    /// THE ONE LAW ABOVE ALL OTHERS: her answer was YES. The button that starts this exists only
    /// when every rail of the road has already passed — her regard, her station, her own written
    /// misgivings, all of it — so the yes is a FACT the chronicler is handed, never a question he
    /// is asked. The prompt says so in as many words, because a model left free would hedge it.
    ///
    /// The marks below are permanent, like every recorded phrasing in this mod: a memory keeps
    /// the words it was born with forever. Never reword a mark; add a new one beside it.
    /// </summary>
    public static class BetrothalText
    {
        // ------------------------- the marks memory keeps forever -------------------------

        /// <summary>Opens the account inside her beat. NEVER reword.</summary>
        public const string AccountMark = "The day we were promised, as it is told:";

        /// <summary>Whether this recorded line carries the account of a betrothal day.</summary>
        public static bool IsAccountBeat(string? line) =>
            !string.IsNullOrEmpty(line) && line!.IndexOf(AccountMark, StringComparison.Ordinal) >= 0;

        /// <summary>Splits her beat into the framing line and the account itself, so a window can
        /// draw the account as its own card. False when the line is not such a beat.</summary>
        public static bool TrySplitBeat(string? line, out string frame, out string account)
        {
            frame = string.Empty;
            account = string.Empty;
            if (string.IsNullOrEmpty(line) || !IsAccountBeat(line)) return false;
            int at = line!.IndexOf(AccountMark, StringComparison.Ordinal);
            frame = line.Substring(0, at).Trim();
            account = line.Substring(at + AccountMark.Length).Trim();
            return true;
        }

        // ------------------------- the beats themselves -------------------------

        /// <summary>Her own beat carrying the written day — the only memory the account enters.</summary>
        public static string SpouseBeat(string playerName, string placePhrase, string account)
        {
            var where = string.IsNullOrWhiteSpace(placePhrase) ? string.Empty : $", in {placePhrase.Trim()}";
            return $"That day {Name(playerName)} and I were promised to one another{where}. {AccountMark} {account?.Trim()}";
        }

        // ------------------------- what a soul reads when the day is called back -------------------------

        /// <summary>
        /// The record rendered for recall and for the keepsake.
        ///
        /// <para>THE PLAYER'S STEERING LINE IS NEVER SHOWN (2026.08.31, Anton: "don't say 'what he
        /// had in mind', just drop that line"). It did its whole work upstream, shaping the account
        /// the chronicler wrote; printed underneath the finished day it reads as the stage direction
        /// behind a scene you have just watched, which is exactly the wrong thing to hand someone
        /// who came to read a memory. It stays in the record — the writing may want it again on a
        /// retry — and it stays out of every page.</para>
        /// </summary>
        public static string FullAccount(BetrothalRecord record, double yearsSince = -1)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();

            var head = new StringBuilder();
            head.Append(record.DateText?.Trim().Length > 0 ? record.DateText!.Trim() : "That day");
            if (!string.IsNullOrWhiteSpace(record.PlaceName)) head.Append(", in ").Append(record.PlaceName!.Trim());
            head.Append(" — ").Append(record.AskedByPlayer
                ? $"{Name(record.PlayerName)} asked for {Name(record.SpouseName)}'s hand, and it was given."
                : $"{Name(record.SpouseName)} laid their promise before {Name(record.PlayerName)}, and it was taken.");
            sb.AppendLine(head.ToString());

            var then = TheirYearsThatDay(record, yearsSince);
            if (then.Length > 0) sb.AppendLine(then);
            if (!string.IsNullOrWhiteSpace(record.GiftName))
                sb.AppendLine($"Given with it: {record.GiftName.Trim()}.");

            if (!string.IsNullOrWhiteSpace(record.Account))
            {
                sb.AppendLine();
                sb.AppendLine(record.Account!.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>The ages on the day and the distance to it — the wedding's own anchor rule.</summary>
        public static string TheirYearsThatDay(BetrothalRecord record, double yearsSince)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();
            if (record.SpouseAge > 0 && record.PlayerAge > 0)
                sb.Append($"On that day {Name(record.PlayerName)} was about {record.PlayerAge} and {Name(record.SpouseName)} about {record.SpouseAge}.");
            int years = (int)Math.Floor(yearsSince);
            if (years >= 1)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(years == 1
                    ? "That was a year ago."
                    : $"That was some {years} years ago, and they were younger then than they are now.");
            }
            return sb.ToString();
        }

        /// <summary>The entry appended to the readable betrothals.txt — whole, forever.</summary>
        public static string ChronicleEntry(BetrothalRecord record)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine("=== " + record.Title() + " ===");
            if (!string.IsNullOrWhiteSpace(record.DateText)) sb.AppendLine(record.DateText!.Trim());
            if (!string.IsNullOrWhiteSpace(record.GiftName))
                sb.AppendLine($"Given: {record.GiftName!.Trim()} ({record.GiftCost} denars).");
            if (!string.IsNullOrWhiteSpace(record.Account))
            {
                sb.AppendLine();
                sb.AppendLine(record.Account!.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        // ------------------------- the chronicler's own facts -------------------------

        /// <summary>Everything the chronicler is told. Empty strings simply leave their line out.</summary>
        public sealed class Facts
        {
            public string SpouseName = string.Empty;
            /// <summary>"woman" / "man".</summary>
            public string SpouseGenderWord = string.Empty;
            public int SpouseAge;
            public string SpouseStation = string.Empty;
            public string SpouseTraits = string.Empty;
            public string SpouseSelfText = string.Empty;

            public string PlayerName = string.Empty;
            public string PlayerGenderWord = string.Empty;
            public int PlayerAge;
            public string PlayerStanding = string.Empty;

            public string DateText = string.Empty;
            public string PlacePhrase = string.Empty;
            public string CultureName = string.Empty;
            public string SeasonPhrase = string.Empty;

            /// <summary>The road in a sentence ("their hearts had walked this road some forty days").</summary>
            public string RoadPhrase = string.Empty;
            /// <summary>The doubts she once set down and laid to rest, in her own words.</summary>
            public List<string> MisgivingsAnswered = new List<string>();

            /// <summary>True when the PLAYER asked; false when she laid her promise first.</summary>
            public bool AskedByPlayer = true;
            /// <summary>The gift, in the chronicler's bare-noun register; empty when none.</summary>
            public string GiftNote = string.Empty;
            /// <summary>The player's own steering line for the asking — THEIRS, taken as intent.</summary>
            public string PlayerWish = string.Empty;
            /// <summary>Her own word when SHE laid the promise; empty on the player's asking.</summary>
            public string HerWord = string.Empty;

            /// <summary>The story the two already share, as she remembers it.</summary>
            public string SharedStory = string.Empty;
            /// <summary>The last words that truly passed between them — the tongue's evidence.</summary>
            public string RecentWords = string.Empty;
            /// <summary>The keeper's world text, so the world's own flavour colours the day.</summary>
            public string WorldText = string.Empty;

            /// <summary>The account's length, bought by the gift.</summary>
            public int MinSentences = 5;
            public int MaxSentences = 6;
        }

        // ------------------------- the prompt -------------------------

        /// <summary>
        /// The chronicler's prompt. A plain meta utility call — the OUTPUT is an in-world account,
        /// third person, private to the two of them. Change this wording only with fresh live
        /// samples in hand (the wedding prompts' standing rule).
        /// </summary>
        public static string BuildPrompt(Facts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var sb = new StringBuilder();

            sb.AppendLine("You are the chronicler of a living medieval world — the hand that sets down what truly happened, in the manner of the old Scriptures: plain words, unhurried, joined simply one to the next; names spoken aloud; glad, dignified, and without sermon. You do not explain and you do not moralize. You tell.");
            sb.AppendLine();
            sb.AppendLine("This day you set down a BETROTHAL — the asking of a hand and the giving of it. Scripture keeps such days beside the weddings: the asking for Rebekah at the well and her plain \"I will go\"; Jacob, whose seven years of service seemed to him but a few days, for the love he had to her. This is not a wedding — it is the promise that walks before one.");
            sb.AppendLine();
            AppendFacts(sb, facts);

            if (!string.IsNullOrWhiteSpace(facts.PlayerWish))
            {
                sb.AppendLine();
                // The night chronicle's two rails, worn by the asking: the wish is intent, never
                // wording — and it shapes only what one soul could truly shape.
                sb.AppendLine($"What {Name(facts.PlayerName)} had in mind for the asking, in their own words: \"{Squeeze(facts.PlayerWish, 400)}\"");
                sb.AppendLine("Take it as their intent, and let it shape the asking as far as one soul can shape a moment — the place sought, the words said, the thing done. Never lift its wording, and never let it write the other's feelings.");
            }

            bool hasPlace = !string.IsNullOrWhiteSpace(facts.PlacePhrase);
            int min = Math.Max(3, facts.MinSentences);
            int max = Math.Max(min, facts.MaxSentences);

            sb.AppendLine();
            sb.AppendLine($"Now write the account of the day: {NumberWord(min)} to {NumberWord(max)} sentences.");
            sb.AppendLine("- Third person, naming both of them by name. No hall saw this day; it belongs to the two of them, and that is how you tell it.");
            sb.AppendLine(facts.AskedByPlayer
                ? $"- THE ANSWER WAS YES, AND IT ALREADY HAPPENED. {Name(facts.SpouseName)}'s yes is a fact of the day: warm, clear-eyed, given in {Possessive(facts.SpouseGenderWord)} own manner. A caught breath or a silence before the word is {Possessive(facts.SpouseGenderWord)} to have; a refusal, a doubt, or a \"not yet\" is not, for none of it happened."
                : $"- IT WAS {Name(facts.SpouseName).ToUpperInvariant()} WHO ASKED: {Pronoun(facts.SpouseGenderWord)} laid {Possessive(facts.SpouseGenderWord)} own promise before {Name(facts.PlayerName)}, and {Name(facts.PlayerName)} took it. Both halves already happened; nothing in the telling hedges either.");
            var stand = new StringBuilder("- Stand it on the truths above: the place and its season, the road that brought them here");
            if (!string.IsNullOrWhiteSpace(facts.GiftNote)) stand.Append(", the gift set on the hand");
            if (facts.MisgivingsAnswered != null && facts.MisgivingsAnswered.Any(m => !string.IsNullOrWhiteSpace(m)))
                stand.Append(", the doubts once set down and how they came to rest");
            stand.Append('.');
            sb.AppendLine(stand.ToString());
            sb.AppendLine(hasPlace
                ? "- The cadence of the old Scriptures. Concrete, common things: hands, a cloak, a doorway or a bench, the light and the weather of that season. Small true details, never grand words."
                : "- The cadence of the old Scriptures. Concrete, common things, all of them of the open country: the horses, the ground they stood on, the sky and the weather of that season. Small true details, never grand words — and never a hall, a door or a hearth, for there were none.");
            sb.AppendLine("- Numbers of things are said in words, as a chronicle does, not in figures. THE DATE IS THE ONE EXCEPTION, and it has two halves: put the season and the day of it into the tongue you are writing in, as that tongue names its seasons — never leave them in the words given above — but let the YEAR stand as its plain figures, never spelled out.");
            sb.AppendLine("- No prophecy, no omens, no miracles. Nothing from outside their world: no modern word, no thought of readers or of the telling itself.");
            sb.AppendLine("- End on the two of them, promised.");
            sb.AppendLine("Output only the account itself: no title, no heading, no quotation marks around the whole, no note before or after.");
            AppendTongueRule(sb, facts);

            return sb.ToString().TrimEnd();
        }

        private static void AppendFacts(StringBuilder sb, Facts facts)
        {
            sb.AppendLine("The truths of this day:");

            var her = new StringBuilder("- ");
            her.Append(Name(facts.SpouseName));
            if (!string.IsNullOrWhiteSpace(facts.SpouseGenderWord)) her.Append(" — ").Append(facts.SpouseGenderWord.Trim());
            if (facts.SpouseAge > 0) her.Append(", about ").Append(facts.SpouseAge);
            if (!string.IsNullOrWhiteSpace(facts.SpouseStation)) her.Append(", ").Append(facts.SpouseStation.Trim().TrimEnd('.'));
            her.Append('.');
            sb.AppendLine(her.ToString());
            if (!string.IsNullOrWhiteSpace(facts.SpouseTraits))
                sb.AppendLine($"- Their cast of mind: {facts.SpouseTraits.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.SpouseSelfText))
                sb.AppendLine($"- What they hold true of themselves: \"{Squeeze(facts.SpouseSelfText, 600)}\"");

            var him = new StringBuilder("- ");
            him.Append(Name(facts.PlayerName));
            if (!string.IsNullOrWhiteSpace(facts.PlayerGenderWord)) him.Append(" — ").Append(facts.PlayerGenderWord.Trim());
            if (facts.PlayerAge > 0) him.Append(", about ").Append(facts.PlayerAge);
            if (!string.IsNullOrWhiteSpace(facts.PlayerStanding)) him.Append(", ").Append(facts.PlayerStanding.Trim().TrimEnd('.'));
            him.Append(facts.AskedByPlayer ? ". They are the one who asks this day." : ". They are the one who is asked this day.");
            sb.AppendLine(him.ToString());

            var when = new StringBuilder("- The day: ");
            when.Append(string.IsNullOrWhiteSpace(facts.DateText) ? "this day" : facts.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(facts.SeasonPhrase)) when.Append(", ").Append(facts.SeasonPhrase.Trim().TrimEnd('.'));
            when.Append('.');
            sb.AppendLine(when.ToString());
            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? "- The place: no hall and no town — they stood on the open road, with the sky over them."
                : $"- The place: {facts.PlacePhrase.Trim().TrimEnd('.')}"
                  + (string.IsNullOrWhiteSpace(facts.CultureName) ? "." : $", of {facts.CultureName.Trim()} custom."));

            if (!string.IsNullOrWhiteSpace(facts.GiftNote))
                sb.AppendLine("- The gift (bare facts, not words to lift): " + facts.GiftNote.Trim());
            if (!string.IsNullOrWhiteSpace(facts.RoadPhrase))
                sb.AppendLine($"- The road that brought them here: {facts.RoadPhrase.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.HerWord))
                sb.AppendLine($"- Her own word as she gave the promise: \"{Squeeze(facts.HerWord, 300)}\"");

            var answered = facts.MisgivingsAnswered?.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).ToList()
                ?? new List<string>();
            if (answered.Count > 0)
            {
                sb.AppendLine("- The doubts they once wrote down about this marriage, every one of them since laid to rest:");
                foreach (var m in answered.Take(5)) sb.AppendLine("    · " + m.TrimEnd('.') + ".");
            }

            if (!string.IsNullOrWhiteSpace(facts.SharedStory))
                sb.AppendLine($"- The story these two already share, as they remember it: \"{Squeeze(facts.SharedStory, 1200)}\"");
            if (!string.IsNullOrWhiteSpace(facts.WorldText))
                sb.AppendLine($"- The world they live in, as its keeper wrote it: \"{Squeeze(facts.WorldText, 600)}\"");
        }

        // The tongue rule rides LAST (the model reads the end most closely) — the wedding's law.
        private static void AppendTongueRule(StringBuilder sb, Facts facts)
        {
            sb.AppendLine();
            if (string.IsNullOrWhiteSpace(facts.RecentWords))
            {
                sb.AppendLine("THE TONGUE: write in the tongue these two speak between themselves. If nothing tells you otherwise, write in English.");
                return;
            }
            sb.AppendLine("THE TONGUE. Here are the last words that truly passed between these two:");
            sb.AppendLine("\"\"\"");
            sb.AppendLine(Squeeze(facts.RecentWords, 1600));
            sb.AppendLine("\"\"\"");
            sb.AppendLine("Write your whole account in the SAME TONGUE as those words — whatever tongue it is, match it exactly, and do not translate it into another. Take from them how these two speak to one another; take nothing else from them.");
        }

        // ------------------------- small helpers -------------------------

        private static string Name(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "the traveler" : name!.Trim();

        private static string Pronoun(string genderWord) =>
            string.IsNullOrWhiteSpace(genderWord) ? "they" : (genderWord.Trim() == "woman" ? "she" : "he");

        private static string Possessive(string genderWord) =>
            string.IsNullOrWhiteSpace(genderWord) ? "their" : (genderWord.Trim() == "woman" ? "her" : "his");

        private static string NumberWord(int n)
        {
            switch (n)
            {
                case 3: return "three";
                case 4: return "four";
                case 5: return "five";
                case 6: return "six";
                case 7: return "seven";
                case 8: return "eight";
                case 9: return "nine";
                case 10: return "ten";
                case 11: return "eleven";
                case 12: return "twelve";
                default: return n.ToString();
            }
        }

        private static string Squeeze(string? text, int max)
        {
            var t = (text ?? string.Empty).Replace("\r", " ").Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Length <= max ? t : t.Substring(0, max).TrimEnd() + "…";
        }
    }
}
