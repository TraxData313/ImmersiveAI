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
        /// <summary>
        /// How much of her sheet the nights told WHOLE may take up, in characters (2026.08.10).
        /// The roll rides in the situation on EVERY reply, so this is not a nicety — it is the
        /// price of the feature, paid over and over. It became load-bearing the day the grand
        /// tiers were allowed to run to eight sentences: five jewel nights told whole would have
        /// put some eight thousand characters into every exchange she has for a fortnight, and in
        /// Cyrillic that is nearer four thousand tokens. So the count ceiling gained a WEIGHT
        /// ceiling beneath it — the freshest are told whole until the room runs out, and a
        /// marriage of grand nights simply keeps fewer of them in full than a marriage of small
        /// ones. The freshest is ALWAYS told whole, whatever it costs; a night she cannot
        /// remember at all is worse than an expensive one.
        /// </summary>
        public const int DefaultFullAccountBudget = 2600;

        /// <summary>
        /// HOW MUCH A NIGHT IS WORTH REMEMBERING WHOLE (2026.08.11, Anton's ask — "искам най-
        /// специалните нощи да вижда дори по-стари"). Until now the roll chose by RECENCY alone, so
        /// the night a child was begun on scrolled out of her sheet in four days while two ordinary
        /// evenings sat there in full. Recency is a tiebreak now, not the rule.
        ///
        /// The night a child began is in a class of its own and always outranks any purse. Beneath
        /// that the gift's own price IS the ranking, which is what the tiers were built to mean.
        /// </summary>
        public static int Specialness(NightRecord night)
        {
            if (night == null) return 0;
            int score = night.Conceived ? 10000 : 0;
            return score + Math.Max(0, night.GiftPrice);
        }

        public static string BuildRoll(
            IReadOnlyList<NightRecord> nights,
            double today,
            int storiesInFull = NightLedger.DefaultStoriesInFull,
            int fullAccountBudget = DefaultFullAccountBudget)
        {
            if (nights == null || nights.Count == 0) return string.Empty;

            var ordered = nights.Where(n => n != null).OrderBy(n => n.GameDay).ToList();
            if (ordered.Count == 0) return string.Empty;

            // WHICH WRITTEN NIGHTS ARE GIVEN WHOLE (reworked 2026.08.11). Two are privileged and
            // always survive: the FRESHEST written night, because the newest thing is what she
            // would have nearest her, and the MOST SPECIAL one however old, because the night a
            // child began on is not something a wife stops carrying after four days. Everything
            // after those two is added best-first while the room lasts; the rest fold to the names
            // she keeps them by.
            var storied = ordered.Where(n => n.IsStoried).ToList();
            var fullSet = new HashSet<string>(StringComparer.Ordinal);
            if (storied.Count > 0 && storiesInFull > 0)
            {
                var privileged = new List<NightRecord>();
                var newest = storied.OrderByDescending(n => n.GameDay).First();
                privileged.Add(newest);
                var dearest = storied.OrderByDescending(Specialness).ThenByDescending(n => n.GameDay).First();
                if (!string.Equals(dearest.Id, newest.Id, StringComparison.Ordinal)) privileged.Add(dearest);

                int spent = 0;
                foreach (var night in privileged.Take(storiesInFull))
                {
                    fullSet.Add(night.Id);
                    spent += night.Story?.Length ?? 0;
                }
                foreach (var night in storied.OrderByDescending(Specialness).ThenByDescending(n => n.GameDay))
                {
                    if (fullSet.Count >= storiesInFull) break;
                    if (fullSet.Contains(night.Id)) continue;
                    int cost = night.Story?.Length ?? 0;
                    if (fullAccountBudget > 0 && spent + cost > fullAccountBudget) break;
                    fullSet.Add(night.Id);
                    spent += cost;
                }
            }

            var lines = new List<string>();

            // AND THE ORDINARY NIGHTS COME IN RUNS (Anton, 2026.08.11 — "ако влизам при нея всяка
            // нощ... с другите да са от тогава до тогава той беше с мене почти всяка нощ"). Ten
            // separate lines saying he came to her is ten lines saying one thing; a written night
            // always breaks a run and always stands alone, which is exactly the point.
            var run = new List<NightRecord>();
            NightKind runKind = NightKind.Unknown;

            // THREE, not two. A pair of nights is still two distinct evenings and each keeps its
            // own full wording — which is where the nuance lives (whether she SAW him go or only
            // heard of it, what they are calling his other night, whether there was fighting). A
            // run is what Anton actually described: night after night after night, all the same.
            const int RunThreshold = 3;

            void FlushRun()
            {
                if (run.Count == 0) return;
                if (run.Count >= RunThreshold) lines.Add(RunLine(runKind, run, today));
                else foreach (var one in run) lines.Add(LineFor(one, today, inFull: false));
                run = new List<NightRecord>();
            }

            foreach (var night in ordered)
            {
                // A night that carries a writing is never swallowed by a run.
                if (night.IsStoried)
                {
                    FlushRun();
                    lines.Add(LineFor(night, today, fullSet.Contains(night.Id)));
                    continue;
                }

                var kind = night.Kind == NightKind.Unknown && night.AtWar ? NightKind.Together : night.Kind;
                if (night.Kind == NightKind.Unknown && night.AtWar)
                {
                    // A war night is its own honest thing, and there is nothing to group it with.
                    FlushRun();
                    lines.Add(LineFor(night, today, inFull: false));
                    continue;
                }

                if (run.Count > 0 && kind != runKind) FlushRun();
                runKind = kind;
                run.Add(night);
            }
            FlushRun();

            var kept = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (kept.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(RollHeader);
            foreach (var line in kept) sb.AppendLine("· " + line);
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// A RUN OF LIKE NIGHTS in one line, in her voice — "from nine nights ago to last night he
        /// came to me nearly every night". Two or more of a kind only; a lone night keeps its own
        /// full wording, which is where all the nuance lives (hearsay, the name of his other night,
        /// the fighting). "Nearly every night" is claimed only when the run really did cover its
        /// own span, so the phrase never overstates what she actually saw.
        /// </summary>
        public static string RunLine(NightKind kind, IReadOnlyList<NightRecord> run, double today)
        {
            if (run == null || run.Count == 0) return string.Empty;
            if (run.Count == 1) return LineFor(run[0], today, inFull: false);

            int n = run.Count;
            var first = run[0];
            var last = run[run.Count - 1];
            var from = WhenPhrase(first.GameDay, today);
            var to = WhenPhrase(last.GameDay, today);
            var span = string.Equals(from, to, StringComparison.Ordinal)
                ? Upper(from)
                : $"From {from} to {to}";

            int days = (int)Math.Round(last.GameDay - first.GameDay) + 1;
            bool nearlyEvery = n >= days - 1;
            var howOften = nearlyEvery ? "nearly every night" : $"on {Spell(n)} of those nights";

            switch (kind)
            {
                case NightKind.Together:
                    return $"{span} he came to me {howOften}, plainly and without ceremony.";

                case NightKind.DoorClosed:
                    return $"{span} my door was closed to him, for the custom of women was upon me.";

                case NightKind.Alone:
                    return $"{span} he slept alone {howOften}.";

                case NightKind.Elsewhere:
                    var names = run.Select(r => r?.OtherName?.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.Ordinal).ToList();
                    if (names.Count == 1)
                        return $"{span} he was with {names[0]}, and not with me, {howOften}.";
                    if (names.Count > 1)
                        return $"{span} he was with others {howOften} — {JoinNames(names!)}.";
                    return $"{span} he was with another of his wives {howOften}.";

                default:
                    return n == 2
                        ? $"{span} I never saw him come in at all, nor learned where he slept."
                        : $"{span} I never saw him come in on {Spell(n)} of those nights, nor learned where he slept.";
            }
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

            /// <summary>How long an account this night earns (2026.08.10, Anton's design — the
            /// coin buys the LENGTH now, see <see cref="NightGifts.Tier.MinSentences"/>). The
            /// defaults are the old fixed three-to-five, so any caller that knows nothing of
            /// tiers writes exactly what it always did.</summary>
            public int MinSentences = 3;
            public int MaxSentences = 5;

            /// <summary>What this night's hand of images is dealt from — the night's own id, so a
            /// retry an hour later reaches for the same ones. Empty deals the deck's first hand,
            /// which is fine for a caller that has no id to give.</summary>
            public string ImageSeed = string.Empty;
        }

        /// <summary>
        /// THE SONG'S OWN IMAGES, as a deck to be drawn from (2026.08.10). Live-probed on
        /// gpt-5.6-luna, two nights in a row: an image NAMED in the prompt comes back in the
        /// answer, near enough word for word — the bird startled in the brush turned up in both,
        /// which is exactly the failure the gift notes were cut back to bare nouns for. Naming no
        /// images at all loses the register Anton asked for; naming the same three forever turns a
        /// marriage into one evening repeated. So a few are DRAWN per night instead, stably from
        /// the night's own id, and the prompt says plainly that they are a place to start and not
        /// a list to work through.
        ///
        /// Every card is Scripture's, and they are anchors rather than instructions: the point is
        /// that night eleven reaches for the sealed spring where night four reached for the
        /// vineyard. Add to the deck freely — a longer deck is strictly better here.
        /// </summary>
        public static readonly IReadOnlyList<string> ImageDeck = new[]
        {
            "a dove startled in the clefts of the rock, or a small bird in the brush",
            "a garden shut, and its spices carried on the air the moment it opens",
            "a vineyard in flower, and the tender grape giving its scent",
            "honey and milk under the tongue",
            "myrrh left on the hands, and on the handles of the lock",
            "a spring shut up and a fountain sealed — and then unsealed",
            "wine that goes down sweetly, moving the lips of those that are asleep",
            "a lily among thorns, and the field of lilies where he feeds",
            "the watchmen going about the city, and the walls left behind them",
            "an apple tree among the trees of the wood, and the sitting down under its shadow",
            "a seal set upon the heart, and a seal upon the arm",
            "coals of fire, a most vehement flame, that many waters cannot quench",
            "the north wind and the south wind, blowing upon a garden that its spices may flow out",
            "the mandrakes giving their smell at the gates",
            "pomegranates broken open",
            "a young hart upon the mountains of spices",
            "the morning looking forth, fair as the moon, clear as the sun",
            "the threshing floor, and the heap of wheat set about with lilies",
        };

        /// <summary>
        /// The cards this night is dealt — stable for a given seed, so a retried night an hour
        /// later reaches for the same images and does not become a different night. FNV-1a, the
        /// same hash the moods are drawn with, for the same reason: no state, no persistence, and
        /// a reload rerolls nothing.
        /// </summary>
        public static IReadOnlyList<string> DrawImages(string? seed, int count = 3)
        {
            int n = Math.Max(0, Math.Min(count, ImageDeck.Count));
            if (n == 0) return Array.Empty<string>();

            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in seed ?? string.Empty) { hash ^= c; hash *= 16777619; }

                var drawn = new List<string>(n);
                var taken = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    // EACH CARD IS ITS OWN HASH. The first cut walked forward from one hashed start
                    // by a fixed stride, which meant the second and third cards sat at a constant
                    // distance from the first — eighteen possible hands out of eight hundred, and
                    // neighbouring night ids collapsing onto the same one. The unit test caught it
                    // the minute it was written; keep that test.
                    uint h = hash;
                    h ^= (uint)(i + 1); h *= 16777619;
                    h ^= h >> 13; h *= 2246822519; h ^= h >> 16;

                    int at = (int)(h % (uint)ImageDeck.Count);
                    while (!taken.Add(at)) at = (at + 1) % ImageDeck.Count;
                    drawn.Add(ImageDeck[at]);
                }
                return drawn;
            }
        }

        /// <summary>The sentence range a night's account is asked for, in words ("seven to eight"),
        /// clamped to something a chronicler can actually hold. Public for the tests, which guard
        /// that the paid tiers really do buy more room.</summary>
        public static string SentenceRange(int min, int max)
        {
            int lo = Math.Max(2, Math.Min(min, 12));
            int hi = Math.Max(lo, Math.Min(max, 12));
            return lo == hi ? $"exactly {Spell(lo).ToUpperInvariant()}" : $"{Spell(lo).ToUpperInvariant()} TO {Spell(hi).ToUpperInvariant()}";
        }

        /// <summary>How much room an account of this many sentences needs before the tamer would
        /// start cutting into it. Non-ASCII costs roughly 1.6x the tokens and rather more of the
        /// characters, and the mod is played in Bulgarian as often as in English — a night of
        /// eight rich sentences runs past two thousand characters there, so the old flat 1600
        /// would have severed exactly the nights this change exists to lengthen.</summary>
        public static int AccountCharBudget(int maxSentences) =>
            Math.Max(1600, Math.Min(12, Math.Max(2, maxSentences)) * 340);

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
            sb.AppendLine("THE REGISTER: the Song of Songs. Scripture speaks of the love between a wedded pair openly and without shame, and it speaks in images — wine and spices, the garden and its fruit, the vineyard, the dove in the clefts of the rock, the door, the lamp, the night and the morning after. Where it names the thing itself, it says simply that he knew her.");
            sb.AppendLine("Hold BOTH halves of that, and it is the second half that gets forgotten: NOTHING coarse, nothing clinical, no part of the body named as a physician or a tavern would name it, nothing that reads as the writing of our own coarse age — and equally NOTHING coy, no fading out, no closing of the door in the reader's face, no ending the account at the threshold. What passed between them is plainly and warmly there, said the way Scripture says it: in images, with tenderness, and without turning away.");
            // THE METHOD, spelled out (2026.08.10, Anton: "тези образи, тези разказвания на детайли
            // но скриването им за образи и загатвания... искам начина на писане да е вдъхновен от
            // библията"). Naming the two halves bought the second one its room; this is what keeps
            // that room from being filled the wrong way. Without it a model reads "not coy" as
            // permission to be plain, and plain is the one register Scripture never uses here.
            sb.AppendLine("AND THIS IS THE METHOD, which is the whole of it: the particular thing is always TOLD, and always told AS AN IMAGE. Scripture never says less than what happened and never says it flatly — it says a garden and its fruit, a sealed spring, a vineyard kept or unkept, wine and honey, myrrh left on the hands, the beloved's own name spoken in the dark. So do not soften the detail away, and do not state it plainly either: find the image that carries it whole, and let the reader arrive at the thing by walking through the picture.");
            sb.AppendLine();
            sb.AppendLine("AND HOLD THE SCALE. This is one evening of a long marriage, not the wedding happening again: two people who already know one another, and easier with each other for it. What was laid out for this night tells you how much of an occasion it was — make it that much, and no more.");
            sb.AppendLine();

            AppendFacts(sb, facts);

            sb.AppendLine();
            sb.AppendLine("Now write that night. First a line of exactly this shape:");
            sb.AppendLine("TITLE: <the name she keeps this night by — three to six words, concrete, taken from something that was actually there; no punctuation at the end>");
            sb.AppendLine($"Then a blank line, then the account itself: {SentenceRange(facts.MinSentences, facts.MaxSentences)} sentences, and hold it in two roughly even halves.");
            sb.AppendLine();
            // THE TWO HALVES (2026.08.10, Anton's design). Before this the prompt asked only for
            // "concrete and small", and what came back was true but shy: three sentences of the
            // evening and one closing formula for the night itself, every time. Naming the halves
            // is what buys the second one its room — and the closing warning is what keeps the
            // named images from becoming the formula they replaced.
            sb.AppendLine("THE FIRST HALF — the coming to it, and the surprise of it. What he had made ready and what she had not expected; the hour and the place; the thing prepared standing there in plain sight; the first look, the first word, the first touch; and what moved in her when she understood what the evening was for. A small gift is a small surprise told closely — a jug of wine chosen with care is its own tenderness — while a great one is an evening somebody has plainly been planning for days.");
            sb.AppendLine("THE SECOND HALF — the knowing itself, and this half is NOT a door politely closed. Stay in the room with them. This is where breath catches and is held; where a whisper goes low and is answered; where something runs through her out to her fingertips until they are numb with it; where hunger and tenderness are the same motion; where a hand tightens and afterwards rests. Her own wanting is hers, and there is no shame in it. Every one of those things is told THROUGH AN IMAGE, as the Song tells them — never named the way our own age names them, and never left out. End inside that night, or in the first grey light after it.");
            sb.AppendLine("Those are the KINDS of thing a body and a night do — not a list to work through, and above all not wording to lift. Take the two or three that were true of THIS night, in your own words, and let the rest go unsaid.");

            // The hand of images, drawn per night. Naming a fixed few made them come back verbatim
            // in every account (live-probed, 2026.08.10); naming none lost the register. See ImageDeck.
            var hand = DrawImages(facts.ImageSeed).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if (hand.Count > 0)
            {
                sb.AppendLine("- The Song's own images are where you reach for the words. Tonight these are nearest to hand: "
                            + string.Join("; ", hand) + ". "
                            + "Take ONE, perhaps two, as a starting point and find the rest yourself — you are not obliged to use any of them, "
                            + "and you must not work through them like a list. Another night will have other images; this one has these.");
            }
            sb.AppendLine("- Whole sentences, and let each one carry its own weight. Do not pack half the night into one long chain of clauses to meet the count — if there is more to say than the sentences allow, say less.");
            sb.AppendLine();
            sb.AppendLine("- Her own \"I\", remembering it afterwards; name " + Name(facts.PartnerName) + " by name at least once.");
            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlacePhrase)
                ? $"- Where they truly were governs BOTH halves: the open country, and there was no room, no door, no bed — you must not invent one. The fire or the dark, the cold and what they had against it, cloaks on the ground, the horses standing near, the sky, {his} hands and her own."
                : $"- Where they truly were governs BOTH halves: the room and its lamp, the cold or the warmth, a cup, a cloak laid aside, the door shut on the rest of the house, {his} hands and her own.");
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
            sb.AppendLine("Write the TITLE and the account in the SAME TONGUE as those words — whatever tongue it is, match it exactly, and do not translate it into another. Take from them how these two speak to one another; take nothing else from them. Write the names of people and places in the LETTERS OF THAT TONGUE and spell each one the same way throughout — never half in one alphabet and half in another.");
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
        /// <param name="maxSentences">How long an account this night was asked for, so the tamer's
        /// cut is set above what was ordered. A flat cap under a raised ceiling is a silent
        /// truncation, which is the one failure a player would read as our bug and never report.</param>
        public static bool TryParseStory(string? raw, out string title, out string story,
            int maxSentences = 5)
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

            story = WeddingText.CleanAccount(string.Join("\n", lines),
                maxChars: AccountCharBudget(maxSentences));
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
