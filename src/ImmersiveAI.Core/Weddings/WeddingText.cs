using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Weddings
{
    /// <summary>
    /// Every word of the wedding chronicle: the two prompts the chronicler is given, the taming of
    /// what comes back, the beats that carry the day into memory, and the accounts a soul reads
    /// when they call the day back (2026.08.09, Anton's ask — the cherry on the cake).
    ///
    /// THE TWO REGISTERS, and they are the whole point:
    /// • THE DAY (<see cref="BuildFeastPrompt"/>) is written as the old Scriptures write a wedding —
    ///   Isaac and Rebekah, Ruth and Boaz, the wine at Cana: plain, unhurried, joined with "and",
    ///   names spoken aloud, glad and dignified. Third person, because the whole hall carries it.
    /// • THE NIGHT (<see cref="BuildNightPrompt"/>) is written as the Song of Songs — HER own first
    ///   person, reverent, image-laden (wine and spices, the garden, the door, the watchmen, the
    ///   morning), tender and unashamed. Scripture neither leers nor looks away: it says plainly
    ///   that he knew her, and says the rest in images. Nothing coarse, nothing clinical, and
    ///   equally nothing coy. That line is load-bearing — it is exactly the thing that was asked
    ///   for, and the reason this feature is not a fade to black.
    ///
    /// THE TONGUE: both prompts carry the couple's own last words and are told to write in the same
    /// tongue as those words. The mod is played in Bulgarian as often as in English, and a wedding
    /// remembered in the wrong language would be no memory at all.
    ///
    /// The BEAT MARKS below are permanent, like every recorded phrasing in this mod: a memory keeps
    /// the words it was born with forever, so the recognizers must keep matching them. Never reword
    /// a mark; add a new one beside it if a new shape is ever needed.
    /// </summary>
    public static class WeddingText
    {
        // ------------------------- the marks memory keeps forever -------------------------

        /// <summary>Opens the public account inside any beat that carries it (the spouse's and every
        /// witness's alike). NEVER reword — recorded memories keep their phrasing forever.</summary>
        public const string DayAccountMark = "The wedding day, as it is told:";

        /// <summary>Opens the night's account in the spouse's memory. Never reword.</summary>
        public const string NightAccountMark = "Of the night that followed, this is mine alone to remember:";

        /// <summary>Whether this recorded line carries the public account of a wedding day.</summary>
        public static bool IsDayBeat(string? line) =>
            !string.IsNullOrEmpty(line) && line!.IndexOf(DayAccountMark, StringComparison.Ordinal) >= 0;

        /// <summary>Whether this recorded line carries the night's own account.</summary>
        public static bool IsNightBeat(string? line) =>
            !string.IsNullOrEmpty(line) && line!.IndexOf(NightAccountMark, StringComparison.Ordinal) >= 0;

        /// <summary>Splits a wedding beat into the soul's own framing line and the account itself,
        /// so a window can draw the account as its own card. False when the line is not such a beat.</summary>
        public static bool TrySplitBeat(string? line, out string frame, out string account)
        {
            frame = string.Empty;
            account = string.Empty;
            if (string.IsNullOrEmpty(line)) return false;

            var mark = IsNightBeat(line) ? NightAccountMark : IsDayBeat(line) ? DayAccountMark : null;
            if (mark == null) return false;

            int at = line!.IndexOf(mark, StringComparison.Ordinal);
            frame = line.Substring(0, at).Trim();
            account = line.Substring(at + mark.Length).Trim();
            return true;
        }

        // ------------------------- the beats themselves -------------------------

        /// <summary>The wedded soul's own beat carrying the public account of the day.</summary>
        public static string SpouseDayBeat(string playerName, string placePhrase, string account)
        {
            var where = string.IsNullOrWhiteSpace(placePhrase) ? string.Empty : $", in {placePhrase.Trim()}";
            return $"This day I was wed to {Name(playerName)}{where}. {DayAccountMark} {account?.Trim()}";
        }

        /// <summary>A witness's own beat carrying the same account — the day as the hall keeps it.</summary>
        public static string WitnessDayBeat(string playerName, string spouseName, string placePhrase, string account)
        {
            var where = string.IsNullOrWhiteSpace(placePhrase) ? string.Empty : $", in {placePhrase.Trim()}";
            return $"This day I stood among the witnesses at the wedding of {Name(playerName)} and {Name(spouseName)}{where}. "
                 + $"{DayAccountMark} {account?.Trim()}";
        }

        /// <summary>The night, in the wedded soul's own memory. Never given to a witness.</summary>
        public static string NightBeat(string account) =>
            $"{NightAccountMark} {account?.Trim()}";

        // ------------------------- what a soul reads when they call the day back -------------------------

        /// <summary>
        /// The record rendered for recall. <paramref name="includeNight"/> is the privacy rule made
        /// code: only the wedded soul is ever given the second part — a witness who asks receives
        /// the day and nothing more, and is told plainly that the rest was never theirs to see.
        /// </summary>
        public static string FullAccount(WeddingRecord record, bool includeNight)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();

            var head = new StringBuilder();
            head.Append(record.DateText?.Trim().Length > 0 ? record.DateText!.Trim() : "That day");
            if (!string.IsNullOrWhiteSpace(record.PlaceName)) head.Append(", in ").Append(record.PlaceName!.Trim());
            head.Append(" — ").Append(Name(record.PlayerName)).Append(" and ").Append(Name(record.SpouseName))
                .Append(" were wed.");
            sb.AppendLine(head.ToString());

            var standing = record.Witnesses?.Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name.Trim()).ToList() ?? new List<string>();
            if (standing.Count > 0)
                sb.AppendLine("Those who stood there: " + JoinNames(standing) + ".");
            if (!string.IsNullOrWhiteSpace(record.BlessedBy))
                sb.AppendLine(record.BridePrice > 0
                    ? $"The blessing of the house was given by {record.BlessedBy!.Trim()}, and {record.BridePrice} denars passed for it."
                    : $"The blessing of the house was given by {record.BlessedBy!.Trim()}.");

            if (!string.IsNullOrWhiteSpace(record.FeastAccount))
            {
                sb.AppendLine();
                sb.AppendLine(record.FeastAccount!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(record.NightAccount))
            {
                sb.AppendLine();
                sb.AppendLine(includeNight
                    ? record.NightAccount!.Trim()
                    : "Of the night that followed I know nothing, nor should I — that belongs to the two of them alone.");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>One line of the roll, when a soul has stood at more than one wedding.</summary>
        public static string RollEntry(WeddingRecord record)
        {
            if (record == null) return string.Empty;
            var when = string.IsNullOrWhiteSpace(record.DateText) ? string.Empty : $" ({record.DateText!.Trim()})";
            var where = string.IsNullOrWhiteSpace(record.PlaceName) ? string.Empty : $", in {record.PlaceName!.Trim()}";
            return $"{Name(record.PlayerName)} and {Name(record.SpouseName)}{where}{when}";
        }

        /// <summary>The entry appended to the readable weddings.txt — both parts, whole, forever.</summary>
        public static string ChronicleEntry(WeddingRecord record)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine("=== " + record.Title() + " ===");
            if (!string.IsNullOrWhiteSpace(record.DateText)) sb.AppendLine(record.DateText!.Trim());
            var standing = record.Witnesses?.Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name.Trim()).ToList() ?? new List<string>();
            if (standing.Count > 0) sb.AppendLine("Witnesses: " + JoinNames(standing) + ".");
            if (!string.IsNullOrWhiteSpace(record.BlessedBy))
                sb.AppendLine($"Blessed by {record.BlessedBy!.Trim()}"
                    + (record.BridePrice > 0 ? $", for {record.BridePrice} denars." : "."));
            if (!string.IsNullOrWhiteSpace(record.FeastAccount))
            {
                sb.AppendLine();
                sb.AppendLine("-- The day --");
                sb.AppendLine(record.FeastAccount!.Trim());
            }
            if (!string.IsNullOrWhiteSpace(record.NightAccount))
            {
                sb.AppendLine();
                sb.AppendLine("-- The night (theirs alone) --");
                sb.AppendLine(record.NightAccount!.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        // ------------------------- the chronicler's own facts -------------------------

        /// <summary>Everything the chronicler is told. All strings optional except the two names —
        /// an empty one simply leaves its line out of the prompt.</summary>
        public sealed class Facts
        {
            public string SpouseName = string.Empty;
            /// <summary>"woman" / "man".</summary>
            public string SpouseGenderWord = string.Empty;
            public int SpouseAge;
            /// <summary>What they are in the world ("a Sturgian wanderer riding as their companion").</summary>
            public string SpouseStation = string.Empty;
            /// <summary>Their cast of mind, from the world's reckoning.</summary>
            public string SpouseTraits = string.Empty;
            /// <summary>What they hold true of themselves (their own sheet's private truths).</summary>
            public string SpouseSelfText = string.Empty;

            public string PlayerName = string.Empty;
            /// <summary>"woman" / "man".</summary>
            public string PlayerGenderWord = string.Empty;
            /// <summary>The player's standing in the world, in a phrase.</summary>
            public string PlayerStanding = string.Empty;

            public string DateText = string.Empty;
            /// <summary>Where the day happened ("the town of Onira"); empty when they wed on the road.</summary>
            public string PlacePhrase = string.Empty;
            public string CultureName = string.Empty;
            /// <summary>The season and hour in the world's own words, when known.</summary>
            public string SeasonPhrase = string.Empty;

            /// <summary>Who stood there, with a word about each ("Yngvald, who rides as their scout").</summary>
            public List<string> Witnesses = new List<string>();

            /// <summary>The story the two already share, as the wedded soul remembers it.</summary>
            public string SharedStory = string.Empty;
            /// <summary>The road of the courtship in a sentence ("her heart turned some forty days
            /// ago; they were betrothed eleven days before this").</summary>
            public string RoadPhrase = string.Empty;
            /// <summary>The head of the house who blessed it and what was paid, in a phrase.</summary>
            public string BlessingPhrase = string.Empty;
            /// <summary>How great a wedding was bought, in the chronicler's own register — see
            /// <see cref="WeddingTiers.ChroniclerNote"/>. Empty when we sold them nothing, and then
            /// no word of money enters the account at all.</summary>
            public string ScaleNote = string.Empty;
            /// <summary>The doubts she once set down and laid to rest, in her own words.</summary>
            public List<string> MisgivingsAnswered = new List<string>();

            /// <summary>The last words that truly passed between them — the chronicler reads these
            /// for the two things no fact sheet carries: their tongue, and how they speak to
            /// each other.</summary>
            public string RecentWords = string.Empty;

            /// <summary>The keeper's world text, so the world's own flavour colours the day.</summary>
            public string WorldText = string.Empty;
        }

        // ------------------------- part one: the day -------------------------

        /// <summary>
        /// The chronicler's prompt for the public account. A plain meta utility call — the OUTPUT is
        /// an in-world document, third person, that every witness will carry as their memory of the
        /// day. Change this wording only with fresh live samples in hand.
        /// </summary>
        public static string BuildFeastPrompt(Facts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var sb = new StringBuilder();

            sb.AppendLine("You are the chronicler of a living medieval world — the hand that sets down what truly happened, in the manner of the old Scriptures: plain words, unhurried, joined simply one to the next; names spoken aloud; the day named and the place named; glad, dignified, and without sermon. You do not explain and you do not moralize. You tell.");
            sb.AppendLine();
            sb.AppendLine("This day you set down a wedding — the account the whole hall will carry, and the one every soul who stood there will keep in memory for the rest of their life.");
            sb.AppendLine();
            AppendFacts(sb, facts);

            bool hasPlace = !string.IsNullOrWhiteSpace(facts.PlacePhrase);
            bool hasWitnesses = facts.Witnesses != null && facts.Witnesses.Any(w => !string.IsNullOrWhiteSpace(w));
            bool hasBlessing = !string.IsNullOrWhiteSpace(facts.BlessingPhrase);

            sb.AppendLine();
            sb.AppendLine("Now write the account of the day: eight to fourteen sentences.");
            sb.AppendLine(hasWitnesses
                ? "- Third person, naming both of them by name. This is the day as the hall remembers it."
                : "- Third person, naming both of them by name. No hall remembers this day — only the two of them and the country around them, and that is how you tell it.");

            var stand = new StringBuilder("- Stand it on the truths above: the place, the day and its season");
            if (hasWitnesses) stand.Append(", the souls who truly stood there (name some of them, and let one or two of them do something small and real)");
            else stand.Append(", the emptiness of the country about them and what they had with them");
            stand.Append(", the road that brought these two here — the promise given and the waiting");
            if (hasBlessing) stand.Append(", the kin's blessing and its price");
            stand.Append(", the doubts once set down and how they came to rest.");
            sb.AppendLine(stand.ToString());

            // The concrete things must match the place, or the prompt fights its own facts: asking
            // for a doorway and a lamp at a wedding on the open road invents a room that was never
            // there (live find on gpt-5.6-terra, 2026.08.09).
            sb.AppendLine(hasPlace
                ? "- The cadence of the old Scriptures. Concrete, common things: bread and wine, hands and cloaks, a doorway, a lamp, the weather of that season. Small true details, never grand words."
                : "- The cadence of the old Scriptures. Concrete, common things, and all of them of the open country: bread and wine carried in a saddlebag, hands and cloaks, the horses, the ground they stood on, the sky and the weather of that season. Small true details, never grand words — and never a hall, a door or a hearth, for there were none.");
            sb.AppendLine("- Numbers of things — men, days, coins — are said in words, as a chronicle does, not in figures. THE DATE IS THE ONE EXCEPTION, and it has two halves: put the season and the day of it into the tongue you are writing in, as that tongue names its seasons — never leave them in the words given above — but let the YEAR stand as its plain figures, never spelled out.");
            sb.AppendLine("- No prophecy, no omens, no falling stars, no miracles. The world is as it is, and it is enough.");
            sb.AppendLine("- Nothing from outside their world may enter: no modern word, no thought of readers, chronicles, tales, or the telling itself.");
            sb.AppendLine("- End on the two of them.");
            sb.AppendLine("Output only the account itself: no title, no heading, no verse numbers, no quotation marks around the whole, no note before or after.");
            AppendTongueRule(sb, facts);

            return sb.ToString().TrimEnd();
        }

        // ------------------------- part two: the night -------------------------

        /// <summary>
        /// The chronicler's prompt for the night. The output is the wedded soul's OWN first-person
        /// memory and belongs to the two of them alone. The register is the Song of Songs — the
        /// Scripture's own way of speaking of the love of a wedded pair: openly, in images, with
        /// tenderness, and without coarseness. Both halves of that rule are load-bearing.
        /// </summary>
        public static string BuildNightPrompt(Facts facts, string dayAccount)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            // The narrator is the WEDDED soul; the beloved is the player. Both pronoun sets are
            // derived — a female player character weds a husband, and his account must never be
            // told to dwell on "her own hands" (review find, 2026.08.09).
            var she = Pronoun(facts.SpouseGenderWord);
            var her = Possessive(facts.SpouseGenderWord);
            var herObject = ObjectPronoun(facts.SpouseGenderWord);
            var him = ObjectPronoun(facts.PlayerGenderWord);
            var his = Possessive(facts.PlayerGenderWord);

            var sb = new StringBuilder();
            sb.AppendLine($"You are the same chronicler, and now you set down what no hall saw: the night that followed the wedding. This part belongs to the two of them alone. You write it IN {her.ToUpperInvariant()} OWN VOICE — first person, \"I\", as {she} {(she == "they" ? "keep" : "keeps")} it in {her} heart and would tell it to no one.");
            sb.AppendLine();
            sb.AppendLine("THE REGISTER, and it matters more here than anywhere: the Song of Songs. Scripture speaks of the love between a wedded pair openly and without shame, and it speaks in images — wine and spices, the garden and its fruit, the vineyard, the door, the lamp, the watchmen of the city, the night and the morning that comes after. Where it names the thing itself, it says simply that he knew her.");
            sb.AppendLine("So hold both halves of that: NOTHING coarse, nothing clinical, no part of the body named as a physician or a tavern would name it, nothing that reads as the writing of our own coarse age — and equally NOTHING coy, nothing evasive, no closing of the door in the reader's face. What passed between them is plainly there, said the way Scripture says it: in images, with tenderness, and without turning away.");
            sb.AppendLine();
            AppendFacts(sb, facts);

            if (!string.IsNullOrWhiteSpace(dayAccount))
            {
                sb.AppendLine();
                sb.AppendLine("The day that went before, as it was set down:");
                sb.AppendLine("\"\"\"");
                sb.AppendLine(dayAccount!.Trim());
                sb.AppendLine("\"\"\"");
            }

            sb.AppendLine();
            sb.AppendLine("Now write that night: six to twelve sentences.");
            sb.AppendLine($"- {Upper(her)} own \"I\", remembering it after; name {Name(facts.PlayerName)} by name.");
            sb.AppendLine($"- Let {herObject} carry into it the whole of who {she} truly {(she == "they" ? "are" : "is")}: {her} gladness and {her} fear both, the doubts {she} once set down and let go, the road that brought {herObject} here, what {she} was before this day.");
            // As in the day's prompt: the small true things must match where they truly were, or
            // the list conjures a room onto an open road (live find on terra, 2026.08.09).
            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? $"- Concrete and small, and every piece of it of the open country where they truly were — there was no room, no door, no bed, and you must not invent one: the fire or the dark, the cold and what they had against it, the horses standing near, cloaks spread on the ground, {his} hands and {her} own, what was said between them, what was not said, the first grey light after."
                : $"- Concrete and small: the room and its lamp, the cold or the warmth, a cup, a cloak laid aside, {his} hands and {her} own, what was said between them, what was not said, the first grey light after.");
            sb.AppendLine("- No sermon, no moral, no prophecy, nothing from outside their world.");
            sb.AppendLine("- End inside that night, or in the morning that follows it.");
            sb.AppendLine("Output only the account itself: no title, no heading, no quotation marks around the whole, no note before or after.");
            AppendTongueRule(sb, facts);

            return sb.ToString().TrimEnd();
        }

        // ------------------------- shared prompt parts -------------------------

        private static void AppendFacts(StringBuilder sb, Facts facts)
        {
            sb.AppendLine("The truths of this day:");

            var bride = new StringBuilder("- ");
            bride.Append(Name(facts.SpouseName));
            if (!string.IsNullOrWhiteSpace(facts.SpouseGenderWord)) bride.Append(" — ").Append(facts.SpouseGenderWord.Trim());
            if (facts.SpouseAge > 0) bride.Append(", about ").Append(facts.SpouseAge);
            if (!string.IsNullOrWhiteSpace(facts.SpouseStation)) bride.Append(", ").Append(facts.SpouseStation.Trim().TrimEnd('.'));
            bride.Append('.');
            sb.AppendLine(bride.ToString());
            if (!string.IsNullOrWhiteSpace(facts.SpouseTraits))
                sb.AppendLine($"- Their cast of mind: {facts.SpouseTraits.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.SpouseSelfText))
                sb.AppendLine($"- What they hold true of themselves: \"{Squeeze(facts.SpouseSelfText, 600)}\"");

            var groom = new StringBuilder("- ");
            groom.Append(Name(facts.PlayerName));
            if (!string.IsNullOrWhiteSpace(facts.PlayerGenderWord)) groom.Append(" — ").Append(facts.PlayerGenderWord.Trim());
            if (!string.IsNullOrWhiteSpace(facts.PlayerStanding)) groom.Append(", ").Append(facts.PlayerStanding.Trim().TrimEnd('.'));
            groom.Append(". They are the one being wed this day.");
            sb.AppendLine(groom.ToString());

            var when = new StringBuilder("- The day: ");
            when.Append(string.IsNullOrWhiteSpace(facts.DateText) ? "this day" : facts.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(facts.SeasonPhrase)) when.Append(", ").Append(facts.SeasonPhrase.Trim().TrimEnd('.'));
            when.Append('.');
            sb.AppendLine(when.ToString());
            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? "- The place: no hall and no town — they were wed on the road, with the sky over them."
                : $"- The place: {facts.PlacePhrase.Trim().TrimEnd('.')}"
                  + (string.IsNullOrWhiteSpace(facts.CultureName) ? "." : $", of {facts.CultureName.Trim()} custom."));

            var witnesses = facts.Witnesses?.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList()
                ?? new List<string>();
            sb.AppendLine(witnesses.Count == 0
                ? "- Who stood there: no one but the two of them; there was no company to witness it."
                : "- Who stood there: " + string.Join("; ", witnesses.Take(40)) + ".");

            // What was spent on the day, and therefore what kind of day it was (2026.08.09). Empty
            // for a wedding sealed outside our own door — there we know nothing of the purse and
            // must invent nothing.
            if (!string.IsNullOrWhiteSpace(facts.ScaleNote))
                sb.AppendLine("- The wedding they paid for: " + facts.ScaleNote.Trim());

            if (!string.IsNullOrWhiteSpace(facts.RoadPhrase))
                sb.AppendLine($"- The road that brought them here: {facts.RoadPhrase.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.BlessingPhrase))
                sb.AppendLine($"- The kin's blessing: {facts.BlessingPhrase.Trim().TrimEnd('.')}.");

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

        // The tongue rule rides LAST in both prompts (the model reads the end most closely), and
        // carries the couple's own words as its evidence — a wedding remembered in a language they
        // never spoke would be no memory of theirs at all.
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

        // ------------------------- taming what comes back -------------------------

        private static readonly Regex Heading = new Regex(@"^\s{0,3}#{1,6}\s*", RegexOptions.CultureInvariant);
        private static readonly Regex LeadingLabel = new Regex(
            @"^\s*(?:part\s+(?:one|two|1|2)|the\s+day|the\s+night|feast|night|day|account|chronicle)\s*[:\-—]\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Tames the chronicler's answer into a plain account: code fencing, markdown headings, a
        /// leading label the model may have added, and one layer of wrapping quotes are all shed;
        /// a runaway answer is cut back at a sentence boundary. Returns an empty string when nothing
        /// usable remains — the caller then records no beat rather than a mangled one.
        /// </summary>
        public static string CleanAccount(string? raw, int maxChars = 4000)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var text = raw!.Replace("\r\n", "\n").Trim();

            // Code fencing, whole-text.
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                int firstBreak = text.IndexOf('\n');
                if (firstBreak > 0) text = text.Substring(firstBreak + 1);
                int fence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) text = text.Substring(0, fence);
                text = text.Trim();
            }

            // Headings and a leading label, at the top only. A markdown heading line is a title the
            // prompt forbade — it is dropped whole, not de-hashed into the account's first sentence.
            var lines = text.Split('\n').ToList();
            while (lines.Count > 0)
            {
                var first = lines[0].Trim();
                if (first.Length == 0) { lines.RemoveAt(0); continue; }
                if (Heading.IsMatch(first)) { lines.RemoveAt(0); continue; }
                var unlabelled = LeadingLabel.Replace(first, string.Empty);
                if (unlabelled != first)
                {
                    if (unlabelled.Trim().Length == 0) { lines.RemoveAt(0); continue; }
                    lines[0] = unlabelled.Trim();
                }
                break;
            }
            text = string.Join("\n", lines).Trim();

            // One layer of wrapping quotes around the whole.
            if (text.Length > 1)
            {
                var pairs = new[] { ('"', '"'), ('“', '”'), ('«', '»') };
                foreach (var pair in pairs)
                    if (text[0] == pair.Item1 && text[text.Length - 1] == pair.Item2)
                    {
                        var inner = text.Substring(1, text.Length - 2).Trim();
                        // Only when the quotes truly wrap the whole, not when two quotes appear inside.
                        if (inner.IndexOf(pair.Item2) < 0) text = inner;
                        break;
                    }
            }

            if (maxChars > 0 && text.Length > maxChars)
                text = Memory.MemoryCompressor.TrimToLastCompleteSentence(text.Substring(0, maxChars));

            return text.Trim();
        }

        /// <summary>Whether an answer is worth keeping at all (a refusal, an apology, or two words
        /// is not an account). Deliberately generous: the shortest true account is still a paragraph.</summary>
        public static bool LooksLikeAnAccount(string? text) =>
            !string.IsNullOrWhiteSpace(text) && text!.Trim().Length >= 120;

        // ------------------------- small shared helpers -------------------------

        private static string Name(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "the traveler" : name!.Trim();

        private static string Pronoun(string genderWord) =>
            string.IsNullOrWhiteSpace(genderWord) ? "they" : (genderWord.Trim() == "woman" ? "she" : "he");

        private static string Possessive(string genderWord) =>
            string.IsNullOrWhiteSpace(genderWord) ? "their" : (genderWord.Trim() == "woman" ? "her" : "his");

        private static string ObjectPronoun(string genderWord) =>
            string.IsNullOrWhiteSpace(genderWord) ? "them" : (genderWord.Trim() == "woman" ? "her" : "him");

        private static string Upper(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static string JoinNames(IReadOnlyList<string> names)
        {
            if (names.Count == 0) return string.Empty;
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }

        private static string Squeeze(string? text, int max)
        {
            var t = (text ?? string.Empty).Replace("\r", " ").Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Length <= max ? t : t.Substring(0, max).TrimEnd() + "…";
        }
    }
}
