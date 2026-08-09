using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Nights
{
    /// <summary>
    /// Every word of the nights (2026.08.09): the chronicler's short prompt and the taming of what
    /// comes back, the permanent marks the beats carry, the roll of the last fortnight as a wife
    /// keeps it, and the player's own keepsake.
    ///
    /// THE REGISTER is the wedding night's, made small. The Song of Songs speaks of a wedded pair
    /// openly and in images, and neither leers nor looks away — the same rule holds here, and both
    /// halves of it are load-bearing. What changes is the SCALE: a wedding night is the once; a
    /// Tuesday in a camp on the road is three or four sentences, and it should read like one
    /// evening of a long marriage, not like a wedding happening again every week.
    ///
    /// THE TITLE is the part that lasts. The full account is shown for the freshest few nights and
    /// then folds away to its name, and it is the NAME alone that goes into her memory as a beat.
    /// The flesh lives in the ledger; the essence lives in her.
    ///
    /// The BEAT MARKS below are permanent, as every recorded phrasing in this mod is: a memory
    /// keeps the words it was born with forever. Never reword one; add a new one beside it.
    /// </summary>
    public static class NightText
    {
        // ------------------------- the marks memory keeps forever -------------------------

        /// <summary>Opens every recorded night. NEVER reword.</summary>
        public const string NightBeatMark = "Of that night between us:";

        /// <summary>Carries the name of a night that was written down. Never reword.</summary>
        public const string NightNameMark = "And I keep a name for it:";

        /// <summary>Whether this recorded line is one of the nights.</summary>
        public static bool IsNightBeat(string? line) =>
            !string.IsNullOrEmpty(line) && line!.IndexOf(NightBeatMark, StringComparison.Ordinal) >= 0;

        /// <summary>Whether this recorded night carries a name — the ones the window draws as cards.</summary>
        public static bool IsNamedNightBeat(string? line) =>
            IsNightBeat(line) && line!.IndexOf(NightNameMark, StringComparison.Ordinal) >= 0;

        /// <summary>Pulls the name out of a named night's beat; empty when there is none.</summary>
        public static string ExtractNightName(string? line)
        {
            if (!IsNamedNightBeat(line)) return string.Empty;
            int at = line!.IndexOf(NightNameMark, StringComparison.Ordinal) + NightNameMark.Length;
            var tail = line.Substring(at).Trim().TrimEnd('.').Trim();
            return tail.Trim('"', '“', '”', '«', '»').Trim();
        }

        // ------------------------- the beats themselves -------------------------

        /// <summary>A night that cost nothing and was never written down — she keeps it plainly,
        /// which is what most nights of any marriage are.</summary>
        public static string PlainBeat(string partnerName, string placePhrase)
        {
            var where = string.IsNullOrWhiteSpace(placePhrase) ? string.Empty : $", in {placePhrase.Trim()}";
            return $"{NightBeatMark} {Name(partnerName)} came to me{where}.";
        }

        /// <summary>A night that was written down. The account itself is NOT in the beat — only its
        /// name, so a marriage of a hundred nights does not silt up her memory with a hundred
        /// paragraphs. The account lives in the ledger and is read back from there.</summary>
        public static string NamedBeat(string partnerName, string placePhrase, string title)
        {
            var plain = PlainBeat(partnerName, placePhrase);
            var name = (title ?? string.Empty).Trim().Trim('"', '“', '”');
            if (name.Length == 0) return plain;
            return $"{plain} {NightNameMark} \"{name}\".";
        }

        // ------------------------- the roll of the fortnight -------------------------

        /// <summary>The header the roll rides under in her own sheet.</summary>
        public const string RollHeader = "The nights lately, as I have known them:";

        /// <summary>
        /// The last fortnight of nights, in her own first person — the block that goes into her
        /// situation. The freshest written nights are given whole (<paramref name="storiesInFull"/>);
        /// older written ones fold to their names; the plain nights, the closed doors and what she
        /// came to hear of his other nights are one line each. A run of nights she never learned
        /// anything about collapses into a single honest line, because three of them in a row read
        /// as an accusation and are nothing of the kind.
        /// </summary>
        /// <summary>
        /// The roll of her nights. It deliberately does NOT mark what has and has not been talked
        /// about: that job moved to <see cref="Together.TogetherLine"/>, which draws ONE line at the
        /// last moment the two of them had time to themselves and lists everything after it once, in
        /// order, with the battles and the roads beside the nights (Anton's design, 2026.08.09 —
        /// "просто и ясно, без много чудене"). Callers pass only the nights up TO that line, so
        /// nothing is ever told twice.
        /// </summary>
        public static string BuildRoll(
            IReadOnlyList<NightRecord> nights,
            double today,
            int storiesInFull = NightLedger.DefaultStoriesInFull)
        {
            if (nights == null || nights.Count == 0) return string.Empty;

            var ordered = nights.Where(n => n != null).OrderBy(n => n.GameDay).ToList();
            if (ordered.Count == 0) return string.Empty;

            // Which written nights are still given whole: the freshest few.
            var fullSet = new HashSet<string>(
                ordered.Where(n => n.IsStoried)
                       .OrderByDescending(n => n.GameDay)
                       .Take(Math.Max(0, storiesInFull))
                       .Select(n => n.Id),
                StringComparer.Ordinal);

            var lines = new List<string>();
            int unknownRun = 0;

            void FlushUnknowns()
            {
                if (unknownRun == 0) return;
                lines.Add(unknownRun == 1
                    ? "One night in there I never saw him come in at all, and never learned where he slept."
                    : $"For {Spell(unknownRun)} of those nights I never saw him come in, nor learned where he slept.");
                unknownRun = 0;
            }

            foreach (var night in ordered)
            {
                if (night.Kind == NightKind.Unknown && !night.AtWar) { unknownRun++; continue; }
                FlushUnknowns();
                lines.Add(LineFor(night, today, fullSet.Contains(night.Id)));
            }
            FlushUnknowns();

            var kept = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (kept.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(RollHeader);
            foreach (var line in kept) sb.AppendLine("· " + line);
            return sb.ToString().TrimEnd();
        }

        /// <summary>One night of the roll, in her voice.</summary>
        public static string LineFor(NightRecord night, double today, bool inFull)
        {
            if (night == null) return string.Empty;
            var when = WhenPhrase(night.GameDay, today);
            var where = string.IsNullOrWhiteSpace(night.PlaceName) ? string.Empty : $", in {night.PlaceName.Trim()}";

            switch (night.Kind)
            {
                case NightKind.Together:
                    if (night.IsStoried && inFull)
                    {
                        var name = string.IsNullOrWhiteSpace(night.Title) ? string.Empty : $" — I call it to myself \"{night.Title.Trim()}\"";
                        return $"{Upper(when)}{where}, he came to me{name}. {night.Story!.Trim()}";
                    }
                    if (night.IsStoried)
                        return $"{Upper(when)}{where}, he came to me — the night I keep as \"{night.Title.Trim()}\".";
                    return $"{Upper(when)}{where}, he came to me.";

                case NightKind.DoorClosed:
                    return $"{Upper(when)} my door was closed to him, for the custom of women was upon me.";

                case NightKind.Elsewhere:
                    var other = string.IsNullOrWhiteSpace(night.OtherName) ? "another of his wives" : night.OtherName.Trim();
                    var told = night.ByHearsay
                        ? $"{Upper(when)} word reached me that he spent the night with {other}."
                        : $"{Upper(when)} he went to {other}, and not to me.";
                    // A night he paid for is a night people repeat the name of.
                    if (!string.IsNullOrWhiteSpace(night.OtherNightTitle))
                        told += $" They have a name for that night: \"{night.OtherNightTitle.Trim()}\".";
                    return told;

                case NightKind.Alone:
                    return $"{Upper(when)} he slept alone.";

                default:
                    if (night.AtWar)
                        return $"{Upper(when)} there was fighting, and I did not look for him.";
                    return $"{Upper(when)} I never learned where he laid his head.";
            }
        }

        /// <summary>"Last night", "three nights ago", "a fortnight past" — never a date, because no
        /// wife counts her marriage in dates.</summary>
        public static string WhenPhrase(double gameDay, double today)
        {
            int days = (int)Math.Round(today - gameDay);
            if (days <= 0) return "tonight";
            if (days == 1) return "last night";
            if (days <= 10) return $"{Spell(days)} nights ago";
            if (days <= 16) return "about a fortnight past";
            return "some weeks past";
        }

        // ------------------------- what the player reads -------------------------

        /// <summary>The entry appended to the readable nights.txt — the player's own keepsake, and
        /// the only place a night's account is kept beyond the rolling fortnight.</summary>
        public static string KeepsakeEntry(NightRecord night)
        {
            if (night == null) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine("=== " + (string.IsNullOrWhiteSpace(night.Title) ? "That night" : night.Title.Trim()) + " ===");

            var head = new StringBuilder();
            head.Append(string.IsNullOrWhiteSpace(night.DateText) ? $"Day {(int)night.GameDay}" : night.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(night.PlaceName)) head.Append(", in ").Append(night.PlaceName.Trim());
            head.Append(" — with ").Append(Name(night.WifeName)).Append('.');
            sb.AppendLine(head.ToString());

            if (night.GiftPrice > 0)
                sb.AppendLine($"Laid out for it: {night.GiftName?.Trim()} ({night.GiftPrice} denars).");
            if (!string.IsNullOrWhiteSpace(night.SeasonWord))
                sb.AppendLine("Her season: " + night.SeasonWord.Trim() + ".");
            if (night.Conceived)
                sb.AppendLine("And a child was begun that night.");

            if (!string.IsNullOrWhiteSpace(night.Story))
            {
                sb.AppendLine();
                sb.AppendLine(night.Story.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>The line the player is shown the moment a night is reckoned, when the odds are
        /// set to show. Green or grey is the caller's business; the words are here.</summary>
        public static string OddsLine(string wifeName, double chance, bool conceived) =>
            conceived
                ? $"{Name(wifeName)} — the chance stood at {Percent(chance)}, and a child was begun."
                : $"{Name(wifeName)} — the chance stood at {Percent(chance)}; no child came of it.";

        /// <summary>The quiet line for an evening when every wife's door is closed — no choice is
        /// offered, and the player is simply told why (Anton, 2026.08.09).</summary>
        public static string CustomDaysNotice(IReadOnlyList<string> names)
        {
            var kept = (names ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (kept.Count == 0) return string.Empty;
            return kept.Count == 1
                ? $"{kept[0]} is in the custom of women these days."
                : $"{JoinNames(kept)} are in the custom of women these days.";
        }

        public static string Percent(double chance) =>
            chance <= 0 ? "nothing at all" : $"{Math.Round(chance * 100.0)}%";

        // ------------------------- the chronicler's prompt -------------------------

        /// <summary>Everything the chronicler is told of one night. Every field is optional but the
        /// two names — an empty one simply leaves its line out.</summary>
        public sealed class Facts
        {
            public string WifeName = string.Empty;
            public int WifeAge;
            /// <summary>What she is in the world ("a Sturgian wanderer who rides with him").</summary>
            public string WifeStation = string.Empty;
            /// <summary>Her cast of mind, from the world's reckoning.</summary>
            public string WifeTraits = string.Empty;
            /// <summary>What she holds true of herself (her own sheet's private truths).</summary>
            public string WifeSelfText = string.Empty;
            /// <summary>Her body's season this night, in her own words (MoodTides).</summary>
            public string WifeSeason = string.Empty;
            /// <summary>Her humor this day, in her own words (MoodTides).</summary>
            public string WifeHumor = string.Empty;
            /// <summary>True when she is already carrying — the night is no less a night, but it is
            /// a different one, and the chronicler must not write her as a woman who might conceive.</summary>
            public bool WithChild;

            public string PartnerName = string.Empty;
            /// <summary>"woman" / "man".</summary>
            public string PartnerGenderWord = "man";

            public string DateText = string.Empty;
            /// <summary>Where they were ("the town of Onira", "our camp on the road"); empty for the
            /// open country, which the prompt then guards against furnishing with a room.</summary>
            public string PlacePhrase = string.Empty;
            /// <summary>"town" / "castle" / "village" / "camp" / "sea".</summary>
            public string PlaceKind = string.Empty;
            /// <summary>The season and the hour in the world's own words.</summary>
            public string SeasonPhrase = string.Empty;

            /// <summary>What was laid out for the night, as the chronicler is told of it
            /// (<see cref="NightGifts.Tier.ChroniclerNote"/>).</summary>
            public string GiftNote = string.Empty;

            /// <summary>How long since he last came to her, in plain words; empty when unknown.</summary>
            public string SinceLastPhrase = string.Empty;
            /// <summary>How long they have been wed, in plain words; empty when unknown.</summary>
            public string MarriedPhrase = string.Empty;
            /// <summary>What lies on the company just now — a siege, a war, a long road.</summary>
            public string CircumstancePhrase = string.Empty;

            /// <summary>The story these two share, as she remembers it.</summary>
            public string SharedStory = string.Empty;
            /// <summary>The world as its keeper wrote it (the global prompt).</summary>
            public string WorldText = string.Empty;
            /// <summary>The last words that truly passed between them — the tongue's evidence.</summary>
            public string RecentWords = string.Empty;

            /// <summary>The names the last few written nights already carry (2026.08.10). Handed
            /// over ONLY so this one does not repeat them or their shape — a marriage of thirty
            /// nights must not read as the same night thirty times.</summary>
            public List<string> PastNightNames = new List<string>();
        }

        /// <summary>
        /// The chronicler's prompt for one night of a marriage. Short by design: this is an evening,
        /// not a wedding. The output is HER own first-person memory of it, and it opens with the
        /// name she will keep it by.
        /// </summary>
        public static string BuildStoryPrompt(Facts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var him = facts.PartnerGenderWord?.Trim() == "woman" ? "her" : "him";
            var his = facts.PartnerGenderWord?.Trim() == "woman" ? "her" : "his";

            var sb = new StringBuilder();
            sb.AppendLine($"You are the chronicler of this house, and you set down one night of a marriage — not the wedding night, one night among the many that come after. You write it IN HER OWN VOICE: first person, \"I\", as {Name(facts.WifeName)} keeps it in her heart and would tell it to no one.");
            sb.AppendLine();
            sb.AppendLine("THE REGISTER: the Song of Songs. Scripture speaks of the love between a wedded pair openly and without shame, and it speaks in images — wine and spices, the garden, the vineyard, the door, the lamp, the night and the morning after. Where it names the thing itself, it says simply that he knew her.");
            sb.AppendLine("Hold both halves of that: NOTHING coarse, nothing clinical, no part of the body named as a physician or a tavern would name it — and equally NOTHING coy, no closing of the door in the reader's face. What passed between them is plainly there, said the way Scripture says it.");
            sb.AppendLine();
            sb.AppendLine("AND HOLD THE SCALE. This is one evening of a long marriage, not the wedding happening again. It is smaller, easier, more particular: two people who already know each other. Let it be the night it actually was and no grander.");
            sb.AppendLine();

            AppendFacts(sb, facts);

            sb.AppendLine();
            sb.AppendLine("Now write that night. First a line of exactly this shape:");
            sb.AppendLine("TITLE: <the name she keeps this night by — three to six words, concrete, taken from something that was actually in the room; no punctuation at the end>");
            sb.AppendLine("Then a blank line, then the account itself: THREE TO FIVE sentences, no more.");
            sb.AppendLine("- Her own \"I\", remembering it afterwards; name " + Name(facts.PartnerName) + " by name at least once.");
            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? $"- Concrete and small, and every piece of it of the open country where they truly were — there was no room, no door, no bed, and you must not invent one: the fire or the dark, the cold and what they had against it, cloaks on the ground, {his} hands and her own, what was said and what was not."
                : $"- Concrete and small: the room and its lamp, the cold or the warmth, a cup, a cloak laid aside, {his} hands and her own, what was said between them and what was not.");
            sb.AppendLine("- Let what she is carry into it — her humor this day, her body's season, whatever stands between them just now. A tired night is a tired night; a night after a quarrel is that.");
            sb.AppendLine("- No sermon, no moral, no prophecy, nothing from outside their world. Do not speak of a child unless she herself would be thinking of one.");
            sb.AppendLine("- Everything above is FACTS, not phrasing. Do not lift the wording of any of it into the account; say it your own way, or leave it unsaid.");

            // The one real defence against a marriage that reads as the same night over and over:
            // show it what it has already called the last few, and let it steer away (2026.08.10).
            var past = facts.PastNightNames?.Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim()).Take(6).ToList() ?? new List<string>();
            if (past.Count > 0)
            {
                sb.AppendLine("- Nights before this one already carry these names: "
                            + string.Join("; ", past.Select(n => "\"" + n + "\"")) + ". "
                            + "Do not reuse them, and do not write the same night again in different words "
                            + "— a marriage is many different evenings, not one evening repeated. Take a "
                            + "different hour of it, a different small thing, a different thing left unsaid.");
            }
            sb.AppendLine("Output only the TITLE line and the account. No heading, no quotation marks around the whole, no note before or after.");

            AppendTongueRule(sb, facts);
            return sb.ToString().TrimEnd();
        }

        private static void AppendFacts(StringBuilder sb, Facts facts)
        {
            sb.AppendLine("The truths of this night:");

            var her = new StringBuilder("- ");
            her.Append(Name(facts.WifeName)).Append(" — his wife");
            if (facts.WifeAge > 0) her.Append(", about ").Append(facts.WifeAge);
            if (!string.IsNullOrWhiteSpace(facts.WifeStation)) her.Append(", ").Append(facts.WifeStation.Trim().TrimEnd('.'));
            her.Append('.');
            sb.AppendLine(her.ToString());
            if (!string.IsNullOrWhiteSpace(facts.WifeTraits))
                sb.AppendLine($"- Her cast of mind: {facts.WifeTraits.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.WifeSelfText))
                sb.AppendLine($"- What she holds true of herself: \"{Squeeze(facts.WifeSelfText, 500)}\"");
            if (!string.IsNullOrWhiteSpace(facts.WifeHumor))
                sb.AppendLine($"- How the day found her: {facts.WifeHumor.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.WifeSeason))
                sb.AppendLine($"- Her body's season: {facts.WifeSeason.Trim().TrimEnd('.')}.");
            if (facts.WithChild)
                sb.AppendLine("- She is already carrying his child, and both of them know it.");

            sb.AppendLine($"- {Name(facts.PartnerName)} — her husband, the one who came to her this night.");

            var when = new StringBuilder("- The night: ");
            when.Append(string.IsNullOrWhiteSpace(facts.DateText) ? "this night" : facts.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(facts.SeasonPhrase)) when.Append(", ").Append(facts.SeasonPhrase.Trim().TrimEnd('.'));
            when.Append('.');
            sb.AppendLine(when.ToString());

            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? "- The place: no roof and no walls — the company was camped on the road, with the sky over them."
                : $"- The place: {facts.PlacePhrase.Trim().TrimEnd('.')}.");

            if (!string.IsNullOrWhiteSpace(facts.GiftNote))
                sb.AppendLine("- What he brought to it (the bare facts of it, not words to reuse): "
                            + facts.GiftNote.Trim());
            if (!string.IsNullOrWhiteSpace(facts.SinceLastPhrase))
                sb.AppendLine($"- Since he last came to her: {facts.SinceLastPhrase.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.MarriedPhrase))
                sb.AppendLine($"- They have been wed {facts.MarriedPhrase.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.CircumstancePhrase))
                sb.AppendLine($"- What lies on them just now: {facts.CircumstancePhrase.Trim().TrimEnd('.')}.");
            if (!string.IsNullOrWhiteSpace(facts.SharedStory))
                sb.AppendLine($"- The story these two share, as she remembers it: \"{Squeeze(facts.SharedStory, 900)}\"");
            if (!string.IsNullOrWhiteSpace(facts.WorldText))
                sb.AppendLine($"- The world they live in, as its keeper wrote it: \"{Squeeze(facts.WorldText, 500)}\"");
        }

        // The tongue rule rides LAST, as it does in the wedding's prompts — the model reads the end
        // most closely, and a night remembered in a language they never spoke is no memory of hers.
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
            sb.AppendLine(Squeeze(facts.RecentWords, 1200));
            sb.AppendLine("\"\"\"");
            sb.AppendLine("Write the TITLE and the account in the SAME TONGUE as those words — whatever tongue it is, match it exactly, and do not translate it into another. Take from them how these two speak to one another; take nothing else from them.");
        }

        // ------------------------- taming what comes back -------------------------

        private static readonly Regex TitleLine = new Regex(
            @"^\s*(?:title|name|заглавие|имя)\s*[:\-—]\s*(?<title>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Splits the chronicler's answer into the night's name and its account, and tames both.
        /// A missing TITLE line is not a failure — the account still stands and the caller falls
        /// back to a plain name; an account too thin to be one yields false, and the night keeps
        /// its place in the roll without a writing.
        /// </summary>
        public static bool TryParseStory(string? raw, out string title, out string story)
        {
            title = string.Empty;
            story = string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var text = raw!.Replace("\r\n", "\n").Trim();

            // Shed whole-text code fencing before looking for the title line.
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                int firstBreak = text.IndexOf('\n');
                if (firstBreak > 0) text = text.Substring(firstBreak + 1);
                int fence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) text = text.Substring(0, fence);
                text = text.Trim();
            }

            var lines = text.Split('\n').ToList();
            for (int i = 0; i < lines.Count && i < 4; i++)
            {
                var candidate = lines[i].Trim().TrimStart('#', '*', ' ').Trim();
                if (candidate.Length == 0) continue;
                var match = TitleLine.Match(candidate);
                if (!match.Success) break; // the title, if any, rides at the top
                title = CleanTitle(match.Groups["title"].Value);
                lines.RemoveRange(0, i + 1);
                break;
            }

            story = WeddingText.CleanAccount(string.Join("\n", lines), maxChars: 1600);
            if (!LooksLikeANight(story)) return false;

            if (title.Length == 0) title = string.Empty; // the caller names it
            return true;
        }

        /// <summary>Tidies a name: quotes, trailing punctuation, markdown and runaway length.</summary>
        public static string CleanTitle(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var t = raw!.Replace("\r", " ").Replace("\n", " ").Trim();
            t = t.Trim('*', '_', '#', ' ').Trim();
            t = t.Trim('"', '“', '”', '«', '»', '\'').Trim();
            t = t.TrimEnd('.', ',', ';', ':', '!', '—', '-').Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            if (t.Length > 70) t = t.Substring(0, 70).TrimEnd() + "…";
            return t;
        }

        /// <summary>Whether an answer is worth keeping (a refusal or two words is not a night).
        /// Lower than the wedding's bar on purpose — three sentences is the whole ask here.</summary>
        public static bool LooksLikeANight(string? text) =>
            !string.IsNullOrWhiteSpace(text) && text!.Trim().Length >= 60;

        // ------------------------- small shared helpers -------------------------

        private static string Name(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "my husband" : name!.Trim();

        private static string Upper(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static readonly string[] Numbers =
            { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

        private static string Spell(int n) =>
            n >= 0 && n < Numbers.Length ? Numbers[n] : n.ToString();

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
