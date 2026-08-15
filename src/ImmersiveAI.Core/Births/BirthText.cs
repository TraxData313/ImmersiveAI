using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Births
{
    /// <summary>
    /// Every word of the birth chronicle (2026.08.10): the two prompts, the permanent marks the
    /// beats carry, the accounts a soul reads when they call the day back, and the taming of what
    /// comes back. The wedding's <see cref="WeddingText"/> is the pattern; what changes is the
    /// Scripture being borrowed from.
    ///
    /// THE TWO REGISTERS:
    /// • THE HOUR (<see cref="BuildBirthPrompt"/>) is the mother's OWN first person, written the
    ///   way Scripture writes a birth — Rachel, Hannah, Elizabeth, the stable: her hour came upon
    ///   her; the women about her; hard labour and the word "fear not"; the first cry; the child
    ///   wrapped and laid at her breast; AND SHE CALLED HIS NAME. The rule that carries it is the
    ///   nights' rule wearing different clothes: nothing clinical and nothing coarse — no
    ///   physician's word for any of it — and equally nothing coy, no skipping the pain and the
    ///   fear to arrive at a clean joy. Scripture says plainly that it was hard, and says the rest
    ///   in images.
    /// • THE FEAST (<see cref="BuildFeastPrompt"/>) is the wedding day's own register: third
    ///   person, plain, joined with "and", names spoken aloud, glad and dignified.
    ///
    /// THE PRIVACY LINE runs between PARENTS and WITNESSES, and it is code, not prose: only the
    /// two who made the child are ever given the hour. A witness who asks is told the feast and
    /// told plainly that the rest was never theirs.
    ///
    /// The BEAT MARKS are permanent, as every recorded phrasing in this mod is — a memory keeps the
    /// words it was born with forever. Never reword one; add a new one beside it.
    /// </summary>
    public static class BirthText
    {
        // ------------------------- the marks memory keeps forever -------------------------

        /// <summary>Opens the mother's own account of the hour. NEVER reword.</summary>
        public const string HourAccountMark = "Of the hour my child came into the world:";

        /// <summary>Opens the public account of the feast, in any beat that carries it. Never reword.</summary>
        public const string FeastAccountMark = "The feast for the child, as it is told:";

        /// <summary>Opens a father's plain mark of the day. Never reword.</summary>
        public const string FatherMark = "A child was born to me this day:";

        /// <summary>Opens the mark of a child who did not live. Never reword.</summary>
        public const string GriefMark = "A child of mine came into the world this day and did not stay in it:";

        public static bool IsHourBeat(string? line) => Has(line, HourAccountMark);
        public static bool IsFeastBeat(string? line) => Has(line, FeastAccountMark);
        public static bool IsFatherBeat(string? line) => Has(line, FatherMark);
        public static bool IsGriefBeat(string? line) => Has(line, GriefMark);

        /// <summary>Whether this recorded line is any of the birth chronicle's beats — what a window
        /// asks before drawing it as a card of its own.</summary>
        public static bool IsBirthBeat(string? line) =>
            IsHourBeat(line) || IsFeastBeat(line) || IsFatherBeat(line) || IsGriefBeat(line);

        /// <summary>Splits a birth beat into the soul's own framing line and the account itself, so
        /// a window can draw the account as its own card. False when the line is not such a beat,
        /// or carries no account (a father's plain mark, a grief mark).</summary>
        public static bool TrySplitBeat(string? line, out string frame, out string account)
        {
            frame = string.Empty;
            account = string.Empty;
            if (string.IsNullOrEmpty(line)) return false;

            var mark = IsHourBeat(line) ? HourAccountMark : IsFeastBeat(line) ? FeastAccountMark : null;
            if (mark == null) return false;

            int at = line!.IndexOf(mark, StringComparison.Ordinal);
            frame = line.Substring(0, at).Trim();
            account = line.Substring(at + mark.Length).Trim();
            return true;
        }

        private static bool Has(string? line, string mark) =>
            !string.IsNullOrEmpty(line) && line!.IndexOf(mark, StringComparison.Ordinal) >= 0;

        // ------------------------- the beats themselves -------------------------

        /// <summary>The mother's own beat, carrying the hour whole. Hers, and the father's — never
        /// a witness's.</summary>
        public static string MotherHourBeat(string fatherName, string placePhrase,
            string childWords, string childNames, string account)
        {
            var where = Where(placePhrase);
            return $"This day I bore {Name(fatherName)} {childWords}, {childNames}{where}. "
                 + $"{HourAccountMark} {account?.Trim()}";
        }

        /// <summary>
        /// The father's own beat. It deliberately carries NO account body: the hour is written in
        /// HER first person, and handing her private "I" to him as though it were his own memory
        /// would be a small lie of exactly the kind this mod exists not to tell. He is given the
        /// fact, and whether he was there — which for a man at war is often the whole of it — and
        /// he may still call the hour back through the recall tool, framed as what she told him.
        /// </summary>
        public static string FatherBeat(string motherName, string placePhrase,
            string childWords, string childNames, bool wasThere)
        {
            var where = Where(placePhrase);
            var presence = wasThere
                ? " I was there for it."
                : " I was not there; word of it found me on the road.";
            return $"{FatherMark} {Name(motherName)} bore me {childWords}, {childNames}{where}.{presence}";
        }

        /// <summary>A parent's beat carrying the feast — glad, and public.</summary>
        public static string ParentFeastBeat(string childNames, string placePhrase, string account)
        {
            var where = Where(placePhrase);
            return $"This day we kept the feast for {childNames}{where}. {FeastAccountMark} {account?.Trim()}";
        }

        /// <summary>A witness's beat carrying the same feast — the day as the hall keeps it.</summary>
        public static string WitnessFeastBeat(string parentName, string childNames,
            string placePhrase, string account)
        {
            var where = Where(placePhrase);
            return $"This day I stood at the feast {Name(parentName)} kept for {childNames}{where}. "
                 + $"{FeastAccountMark} {account?.Trim()}";
        }

        /// <summary>The mark for a child who did not live. No chronicler is called for this and no
        /// feast is offered — the record simply holds it, and so does she. Written by hand and
        /// never by a model, because there is nothing here worth a paid call and a great deal
        /// worth getting wrong.</summary>
        public static string GriefBeat(string otherParentName, string placePhrase, bool twinLived)
        {
            var where = Where(placePhrase);
            var rest = twinLived
                ? " Its brother or sister lived, and I have both of them in me at once."
                : string.Empty;
            return $"{GriefMark} it was mine and {Name(otherParentName)}'s{where}.{rest}";
        }

        private static string Where(string? placePhrase) =>
            string.IsNullOrWhiteSpace(placePhrase) ? string.Empty : $", in {placePhrase!.Trim()}";

        // ------------------------- what a soul reads when they call the day back -------------------------

        /// <summary>
        /// The record rendered for recall. <paramref name="includeHour"/> is the privacy rule made
        /// code: only a parent is ever given the hour. <paramref name="asMother"/> frames it — the
        /// mother reads her own memory back, while the father reads what she told him of it, which
        /// is the honest way a man comes to know an hour he may not even have been in the room for.
        /// </summary>
        /// <param name="yearsSince">Years between that day and today; negative when unknown. See
        /// <see cref="TheirYearsThatDay"/> — a child's day is the one most likely of all to be told
        /// back decades later, when everyone in it has become someone else.</param>
        public static string FullAccount(BirthRecord record, bool includeHour, bool asMother, double yearsSince = -1)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();

            var head = new StringBuilder();
            head.Append(string.IsNullOrWhiteSpace(record.DateText) ? "That day" : record.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(record.PlaceName)) head.Append(", in ").Append(record.PlaceName.Trim());
            head.Append(" — ").Append(Name(record.MotherName)).Append(" bore ")
                .Append(record.ChildWords()).Append(", ").Append(record.ChildNames()).Append('.');
            sb.AppendLine(head.ToString());

            var then = TheirYearsThatDay(record, yearsSince);
            if (then.Length > 0) sb.AppendLine(then);

            if (record.StillbornCount > 0)
                sb.AppendLine(record.AnyLived
                    ? "Not all of them lived."
                    : "None of them lived.");

            var standing = record.Witnesses?.Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name.Trim()).ToList() ?? new List<string>();
            if (standing.Count > 0)
                sb.AppendLine("Those who stood at the feast: " + JoinNames(standing) + ".");

            if (!string.IsNullOrWhiteSpace(record.BirthAccount))
            {
                sb.AppendLine();
                if (!includeHour)
                {
                    sb.AppendLine("Of the hour itself I know nothing, nor should I — that belongs to the two of them.");
                }
                else
                {
                    if (!asMother) sb.AppendLine("This is how she told me of that hour, in her own words:");
                    sb.AppendLine(record.BirthAccount.Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(record.FeastAccount))
            {
                sb.AppendLine();
                sb.AppendLine(record.FeastAccount.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// How old the parents were on the day, how long ago it was, and therefore how old the
        /// child would be now — the anchor that keeps a birth told years later from being told
        /// about the people they have since become. Empty when the record kept no ages and the day
        /// is recent enough that nothing needs saying.
        /// </summary>
        public static string TheirYearsThatDay(BirthRecord record, double yearsSince)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();

            if (record.MotherAge > 0 || record.FatherAge > 0)
            {
                sb.Append("On that day ");
                if (record.MotherAge > 0 && record.FatherAge > 0)
                    sb.Append(Name(record.MotherName)).Append(" was about ").Append(record.MotherAge)
                      .Append(" and ").Append(Name(record.FatherName)).Append(" about ").Append(record.FatherAge);
                else if (record.MotherAge > 0)
                    sb.Append(Name(record.MotherName)).Append(" was about ").Append(record.MotherAge);
                else
                    sb.Append(Name(record.FatherName)).Append(" was about ").Append(record.FatherAge);
                sb.Append('.');
            }

            int years = (int)Math.Floor(yearsSince);
            if (years >= 1)
            {
                if (sb.Length > 0) sb.Append(' ');
                // "would be" and not "is": the ledger knows what was born, never who is still living.
                var child = record.AnyLived
                    ? (record.Children.Count > 1 ? " — they would be that old now." : " — the child would be that old now.")
                    : ".";
                sb.Append(years == 1 ? "That was a year ago" : $"That was some {years} years ago")
                  .Append(child == "." ? "." : child);
            }
            return sb.ToString();
        }

        /// <summary>One line of the roll, when a soul has seen more than one of these days.</summary>
        public static string RollEntry(BirthRecord record)
        {
            if (record == null) return string.Empty;
            var when = string.IsNullOrWhiteSpace(record.DateText) ? string.Empty : $" ({record.DateText.Trim()})";
            var where = string.IsNullOrWhiteSpace(record.PlaceName) ? string.Empty : $", in {record.PlaceName.Trim()}";
            return $"{record.ChildNames()}, born to {Name(record.MotherName)}{where}{when}";
        }

        /// <summary>The entry appended to the readable births.txt — both parts, whole, forever.</summary>
        public static string ChronicleEntry(BirthRecord record)
        {
            if (record == null) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine("=== " + record.Title() + " ===");
            if (!string.IsNullOrWhiteSpace(record.DateText)) sb.AppendLine(record.DateText.Trim());
            sb.AppendLine($"{Name(record.MotherName)} and {Name(record.FatherName)} — {record.ChildWords()}."
                + (record.FatherWasThere ? string.Empty : " (The father was away.)"));
            if (record.StillbornCount > 0)
                sb.AppendLine($"Stillborn: {record.StillbornCount}.");
            if (record.FeastCost > 0)
                sb.AppendLine($"The feast: {BirthTiers.NameOf(record.Scale)} ({record.FeastCost} denars).");
            var standing = record.Witnesses?.Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name.Trim()).ToList() ?? new List<string>();
            if (standing.Count > 0) sb.AppendLine("Witnesses: " + JoinNames(standing) + ".");

            if (!string.IsNullOrWhiteSpace(record.BirthAccount))
            {
                sb.AppendLine();
                sb.AppendLine("-- The hour (the parents' own) --");
                sb.AppendLine(record.BirthAccount.Trim());
            }
            if (!string.IsNullOrWhiteSpace(record.FeastAccount))
            {
                sb.AppendLine();
                sb.AppendLine("-- The feast --");
                sb.AppendLine(record.FeastAccount.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        // ------------------------- the chronicler's own facts -------------------------

        /// <summary>Everything the chronicler is told. All optional but the names — an empty field
        /// simply leaves its line out of the prompt.</summary>
        public sealed class Facts
        {
            public string MotherName = string.Empty;
            public int MotherAge;
            /// <summary>What she is in the world ("a Sturgian wanderer who rides with him").</summary>
            public string MotherStation = string.Empty;
            public string MotherTraits = string.Empty;
            /// <summary>What she holds true of herself (her own sheet's private truths).</summary>
            public string MotherSelfText = string.Empty;

            public string FatherName = string.Empty;
            /// <summary>His age on the day. The mother's was always given and his never was (fixed
            /// 2026.08.15) — the wedding's own oversight, wearing the other parent's clothes.</summary>
            public int FatherAge;
            public string FatherStanding = string.Empty;
            /// <summary>Whether the father stood there when it happened.</summary>
            public bool FatherWasThere = true;

            /// <summary>"a son", "twin daughters" — the plain words for what came.</summary>
            public string ChildWords = string.Empty;
            /// <summary>The names the world has already given them.</summary>
            public string ChildNames = string.Empty;
            /// <summary>How many did not live. The chronicler must never be gentle with this by
            /// leaving it out, and must never dwell on it either.</summary>
            public int StillbornCount;

            /// <summary>How many living children these two already had; -1 when unknown.</summary>
            public int OlderChildren = -1;
            /// <summary>How long they have been wed, in plain words; empty when unknown.</summary>
            public string MarriedPhrase = string.Empty;

            public string DateText = string.Empty;
            /// <summary>Where it happened ("the town of Akkalat"); empty for a camp on the road,
            /// which the prompt then guards against furnishing with a room.</summary>
            public string PlacePhrase = string.Empty;
            public string CultureName = string.Empty;
            public string SeasonPhrase = string.Empty;
            /// <summary>What lies on them just now — a siege, a war, a long road.</summary>
            public string CircumstancePhrase = string.Empty;

            /// <summary>Who stood at the feast, with a word about each.</summary>
            public List<string> Witnesses = new List<string>();
            /// <summary>How great a feast was bought, in the chronicler's register. Empty when none.</summary>
            public string ScaleNote = string.Empty;
            /// <summary>How long the child had already been alive when the feast was kept, in plain
            /// words; empty when it was kept the same day. A father away at war keeps it when he
            /// rides in, and a chronicler told nothing of that writes a newborn into a hall it left
            /// three weeks earlier.</summary>
            public string FeastDelayPhrase = string.Empty;

            /// <summary>The story these two share, as she remembers it.</summary>
            public string SharedStory = string.Empty;
            /// <summary>The world as its keeper wrote it (the global prompt).</summary>
            public string WorldText = string.Empty;
            /// <summary>The last words that truly passed between them — the tongue's evidence.</summary>
            public string RecentWords = string.Empty;
        }

        // ------------------------- part one: the hour -------------------------

        /// <summary>
        /// The chronicler's prompt for the hour of the birth. The output is the MOTHER'S own
        /// first-person memory and belongs to the two parents alone. Change this wording only with
        /// fresh live samples in hand.
        /// </summary>
        public static string BuildBirthPrompt(Facts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var sb = new StringBuilder();

            sb.AppendLine($"You are the chronicler of this house, and you set down the hour a child came into the world. You write it IN HER OWN VOICE: first person, \"I\", as {Name(facts.MotherName)} keeps that hour in herself for the rest of her life.");
            sb.AppendLine();
            sb.AppendLine("THE REGISTER: the old Scriptures, where children are born. Rachel and Hannah and Elizabeth; the hour that comes upon a woman; the women who stand about her; the hard labour and the word spoken over it, FEAR NOT; the first cry; the child wrapped in cloths and laid at her breast; and she called his name.");
            sb.AppendLine("Hold BOTH halves of that rule. NOTHING clinical — not one word a physician or a midwife's manual would use, no anatomy, no measurements, no modern medicine. And equally NOTHING coy: do not skip past the pain and the fear to arrive at a clean, tidy joy. It was hard, and it was frightening, and she may well have believed she would die; Scripture says so plainly and then says the rest in images. Both are true at once and both belong in it.");
            // The same method the nights carry (Anton, 2026.08.10) — told, and told as an image.
            sb.AppendLine("AND THIS IS THE METHOD: the hard particular thing is always TOLD, and told AS AN IMAGE — a woman in her travail, a cup she could not hold, waters and a narrow gate, the darkness before the lamp is trimmed, a word spoken over her that she held on to. Do not soften the detail away and do not state it flatly; find the image that carries it, and let the reader arrive at the thing by walking through the picture.");
            sb.AppendLine();
            sb.AppendLine("THE SHAPE, in two roughly even halves:");
            sb.AppendLine("- THE FIRST HALF is the hour itself: when it began and how she knew; who was with her and who was not; the room or the ground under her; what she held on to; how long it went; the fear at the worst of it and the one thing that carried her through — a face, a hand, a voice, something she told herself.");
            sb.AppendLine("- THE SECOND HALF is the child: the first cry and what it did to her; the first look at that face and what she found there; the weight of it on her; the name and who said it first; and what she thought of the one who fathered it, whether he was standing there or a week's ride away.");
            sb.AppendLine("Those are the kinds of thing such an hour holds — not a list to work through, and not wording to lift. Take what was true of THIS hour and let the rest go unsaid.");
            sb.AppendLine();

            AppendFacts(sb, facts, forFeast: false);

            sb.AppendLine();
            sb.AppendLine("Now write that hour: FIVE TO EIGHT sentences.");
            sb.AppendLine($"- Her own \"I\", remembering it afterwards; name {Name(facts.FatherName)} by name at least once, and the child by name.");
            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? "- Every piece of it of the open country where they truly were — there was no room, no bed, no hall, and you must not invent one: the tent or the cart or the ground, the fire, the cold, the women of the company, cloaks and water and what little there was."
                : "- Concrete and small, and all of it of the place they truly were: the room, a lamp, water, cloths, the women who came and went, the sounds of the house beyond the door.");
            sb.AppendLine("- No sermon, no moral, no prophecy over the cradle, no omen, nothing from outside their world. The child is a child, not a destiny.");
            sb.AppendLine("- Everything above is FACTS, not phrasing. Do not lift the wording of any of it; say it your own way, or leave it unsaid.");
            sb.AppendLine("- End on the child, or on her holding it.");
            sb.AppendLine("Output only the account itself: no title, no heading, no quotation marks around the whole, no note before or after.");

            AppendTongueRule(sb, facts);
            return sb.ToString().TrimEnd();
        }

        // ------------------------- part two: the feast -------------------------

        /// <summary>
        /// The chronicler's prompt for the feast — the public account every witness will carry.
        /// Written only when a feast was actually bought.
        ///
        /// IT IS NEVER GIVEN THE HOUR, and that is the whole privacy rule standing up (2026.08.10
        /// review). The first cut handed the mother's private account over "for your understanding
        /// only — it is hers, and no one at the feast knows it", which is prose asking a model to
        /// keep a secret while its answer is copied verbatim into up to sixty witnesses' memories.
        /// A rule enforced by asking nicely is not enforced. The only two things the feast honestly
        /// needs from that hour — whether the father reached it, and whether every child lived —
        /// ride in <see cref="Facts.FatherWasThere"/> and <see cref="Facts.StillbornCount"/> as
        /// plain facts. Do not add the parameter back.
        /// </summary>
        public static string BuildFeastPrompt(Facts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var sb = new StringBuilder();

            sb.AppendLine("You are the chronicler of a living medieval world — the hand that sets down what truly happened, in the manner of the old Scriptures: plain words, unhurried, joined simply one to the next; names spoken aloud; the day named and the place named; glad, dignified, and without sermon. You do not explain and you do not moralize. You tell.");
            sb.AppendLine();
            sb.AppendLine("This day you set down the feast a house kept for a child of its own — the account the whole hall will carry, and the one every soul who stood there will keep in memory.");
            sb.AppendLine();

            AppendFacts(sb, facts, forFeast: true);

            bool hasWitnesses = facts.Witnesses != null && facts.Witnesses.Any(w => !string.IsNullOrWhiteSpace(w));
            bool hasPlace = !string.IsNullOrWhiteSpace(facts.PlacePhrase);

            sb.AppendLine();
            sb.AppendLine("Now write the account of the feast: six to twelve sentences.");
            sb.AppendLine(hasWitnesses
                ? "- Third person, naming the parents and the child. This is the day as the hall remembers it."
                : "- Third person, naming the parents and the child. No hall remembers this day — only the few who were there, and that is how you tell it.");
            var stand = new StringBuilder("- Stand it on the truths above: the place, the day and its season");
            if (hasWitnesses) stand.Append(", the souls who truly stood there (name some of them, and let one or two of them do something small and real — a gift set down, a cup filled, a rough joke)");
            stand.Append(", the child carried in and shown, THE NAME SPOKEN ALOUD over the company, and what the parents were to each other as it was said.");
            sb.AppendLine(stand.ToString());
            sb.AppendLine(hasPlace
                ? "- The cadence of the old Scriptures. Concrete, common things: bread and salt, wine, a fire, cloths, a doorway, the weather of that season. Small true details, never grand words."
                : "- The cadence of the old Scriptures. Concrete, common things, and all of them of the open country: bread and salt out of a saddlebag, a fire, the horses, the ground, the sky and the weather of that season. Never a hall, a door or a hearth, for there were none.");
            if (!facts.FatherWasThere)
                sb.AppendLine("- The father was NOT there when the child came. If he stands at this feast he has ridden hard to reach it; do not pretend he saw the birth.");
            if (facts.StillbornCount > 0)
                sb.AppendLine("- Not every child of this birth lived. The feast is real and the gladness is real, and the absence is in the room with them; say it once, plainly, and do not dwell on it.");
            sb.AppendLine("- Numbers of things — men, days, coins — are said in words, as a chronicle does, not in figures. THE DATE IS THE ONE EXCEPTION, and it has two halves: put the season and the day of it into the tongue you are writing in, as that tongue names its seasons — never leave them in the words given above — but let the YEAR stand as its plain figures, never spelled out.");
            sb.AppendLine("- No prophecy, no omens, no falling stars, no miracles, and NOTHING about what this child will one day become. The world is as it is, and it is enough.");
            sb.AppendLine("- Nothing from outside their world may enter: no modern word, no thought of readers, chronicles, tales, or the telling itself.");
            sb.AppendLine("- End on the child, or on the parents with the child.");
            sb.AppendLine("Output only the account itself: no title, no heading, no verse numbers, no quotation marks around the whole, no note before or after.");

            AppendTongueRule(sb, facts);
            return sb.ToString().TrimEnd();
        }

        // ------------------------- shared prompt parts -------------------------

        /// <param name="forFeast">
        /// WHICH PROMPT IS BEING BUILT, and it decides what the chronicler may see (2026.08.10
        /// review — this cut both ways at once). The HOUR must not be told the guest list or the
        /// price: it happened whether or not anyone feasted it, and on a RETRY days later the
        /// record has since accumulated both, so her private account of the labour would have had
        /// a naming feast written into it. And the FEAST must not be told her private self-text or
        /// her deep memory of the marriage: that answer is copied verbatim into up to sixty
        /// witnesses' memories, and the hall has no business with either. Gated HERE rather than
        /// only in the caller because this is the place both prompts pass through.
        /// </param>
        private static void AppendFacts(StringBuilder sb, Facts facts, bool forFeast)
        {
            sb.AppendLine("The truths of this day:");

            var mother = new StringBuilder("- ");
            mother.Append(Name(facts.MotherName)).Append(" — the mother");
            if (facts.MotherAge > 0) mother.Append(", about ").Append(facts.MotherAge);
            if (!string.IsNullOrWhiteSpace(facts.MotherStation)) mother.Append(", ").Append(facts.MotherStation.Trim().TrimEnd('.'));
            mother.Append('.');
            sb.AppendLine(mother.ToString());
            if (!string.IsNullOrWhiteSpace(facts.MotherTraits))
                sb.AppendLine($"- Her cast of mind: {facts.MotherTraits.Trim().TrimEnd('.')}.");
            // Hers alone: the hall never sees what she holds true of herself.
            if (!forFeast && !string.IsNullOrWhiteSpace(facts.MotherSelfText))
                sb.AppendLine($"- What she holds true of herself: \"{Squeeze(facts.MotherSelfText, 600)}\"");

            var father = new StringBuilder("- ");
            father.Append(Name(facts.FatherName)).Append(" — the father");
            if (facts.FatherAge > 0) father.Append(", about ").Append(facts.FatherAge);
            if (!string.IsNullOrWhiteSpace(facts.FatherStanding)) father.Append(", ").Append(facts.FatherStanding.Trim().TrimEnd('.'));
            father.Append('.');
            father.Append(facts.FatherWasThere
                ? " He was there when the child came."
                : " He was NOT there when the child came — he was away, and word had to find him.");
            sb.AppendLine(father.ToString());

            var child = new StringBuilder("- The child: ");
            child.Append(string.IsNullOrWhiteSpace(facts.ChildWords) ? "a child" : facts.ChildWords.Trim());
            if (!string.IsNullOrWhiteSpace(facts.ChildNames))
                child.Append(", already named ").Append(facts.ChildNames.Trim())
                     .Append(" — that name is settled, and you must use it and invent no other");
            child.Append('.');
            sb.AppendLine(child.ToString());
            if (facts.StillbornCount > 0)
                sb.AppendLine(facts.StillbornCount == 1
                    ? "- One other came with it and did not live."
                    : $"- {facts.StillbornCount} others came with it and did not live.");
            if (facts.OlderChildren > 0)
                sb.AppendLine($"- They had {facts.OlderChildren} living {(facts.OlderChildren == 1 ? "child" : "children")} already, so this is not their first.");
            else if (facts.OlderChildren == 0)
                sb.AppendLine("- This is their FIRST child. Nothing about it has ever happened to either of them before.");
            if (!string.IsNullOrWhiteSpace(facts.MarriedPhrase))
                sb.AppendLine($"- They have been wed {facts.MarriedPhrase.Trim().TrimEnd('.')}.");

            var when = new StringBuilder("- The day: ");
            when.Append(string.IsNullOrWhiteSpace(facts.DateText) ? "this day" : facts.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(facts.SeasonPhrase)) when.Append(", ").Append(facts.SeasonPhrase.Trim().TrimEnd('.'));
            when.Append('.');
            sb.AppendLine(when.ToString());

            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? "- The place: no roof and no walls — the company was on the road, with the sky over them."
                : $"- The place: {facts.PlacePhrase.Trim().TrimEnd('.')}"
                  + (string.IsNullOrWhiteSpace(facts.CultureName) ? "." : $", of {facts.CultureName.Trim()} custom."));

            if (!string.IsNullOrWhiteSpace(facts.CircumstancePhrase))
                sb.AppendLine($"- What lies on them just now: {facts.CircumstancePhrase.Trim().TrimEnd('.')}.");

            var witnesses = forFeast
                ? (facts.Witnesses?.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList()
                   ?? new List<string>())
                : new List<string>();
            if (witnesses.Count > 0)
                sb.AppendLine("- Who stood at the feast: " + string.Join("; ", witnesses.Take(40)) + ".");

            if (forFeast && !string.IsNullOrWhiteSpace(facts.FeastDelayPhrase))
                sb.AppendLine($"- The feast was NOT kept on the day of the birth: {facts.FeastDelayPhrase.Trim().TrimEnd('.')}. The child is that much older at this table, and the mother is up and about.");
            if (forFeast && !string.IsNullOrWhiteSpace(facts.ScaleNote))
                sb.AppendLine("- The feast they paid for: " + facts.ScaleNote.Trim());

            if (!forFeast && !string.IsNullOrWhiteSpace(facts.SharedStory))
                sb.AppendLine($"- The story these two share, as she remembers it: \"{Squeeze(facts.SharedStory, 1000)}\"");
            if (!string.IsNullOrWhiteSpace(facts.WorldText))
                sb.AppendLine($"- The world they live in, as its keeper wrote it: \"{Squeeze(facts.WorldText, 500)}\"");
        }

        // The tongue rule rides LAST in both prompts, as it does in the wedding's and the nights' —
        // the model reads the end most closely, and a day remembered in a language they never spoke
        // is no memory of theirs at all.
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
            sb.AppendLine(Squeeze(facts.RecentWords, 1400));
            sb.AppendLine("\"\"\"");
            sb.AppendLine("Write your whole account in the SAME TONGUE as those words — whatever tongue it is, match it exactly, and do not translate it into another. Take from them how these two speak to one another; take nothing else from them. Write the names of people and places in the LETTERS OF THAT TONGUE and spell each one the same way throughout — never half in one alphabet and half in another.");
        }

        // ------------------------- taming what comes back -------------------------

        /// <summary>Tames the chronicler's answer. The wedding's tamer does the work — code fences,
        /// headings, a leading label, wrapping quotes, and a sentence-boundary cut.</summary>
        public static string CleanAccount(string? raw, int maxChars = 4000) =>
            WeddingText.CleanAccount(raw, maxChars);

        /// <summary>Whether an answer is worth keeping at all (a refusal or two words is not an
        /// account). The wedding's bar, for the same reason: the shortest true account is a paragraph.</summary>
        public static bool LooksLikeAnAccount(string? text) =>
            !string.IsNullOrWhiteSpace(text) && text!.Trim().Length >= 120;

        // ------------------------- small shared helpers -------------------------

        private static string Name(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "the traveler" : name!.Trim();

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
