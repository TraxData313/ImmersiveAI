using ImmersiveAI.Core.Nights;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class NightsTests
{
    private const string Her = "lord_7_13_1";

    // ------------------------------ the body's own season ------------------------------

    [Fact]
    public void Fertility_IsZeroWhileTheDoorIsClosed_AndPeaksAtTheCrest()
    {
        int length = MoodTides.CycleLength(Her);
        double peak = 0;
        bool sawClosedDay = false;

        for (int day = 0; day < length * 3; day++)
        {
            var phase = MoodTides.PhaseOf(Her, day, out int dayOfCycle);
            double f = MoodTides.Fertility(Her, day);
            Assert.InRange(f, 0.0, 1.0);

            if (phase == MoodTides.CyclePhase.Menses)
            {
                sawClosedDay = true;
                Assert.Equal(0.0, f);
                Assert.True(MoodTides.DoorIsClosed(Her, day));
            }
            else
            {
                Assert.False(MoodTides.DoorIsClosed(Her, day));
                Assert.True(f > 0.0, $"day {dayOfCycle} of her cycle should never be quite nothing");
            }
            peak = Math.Max(peak, f);
        }

        Assert.True(sawClosedDay);
        Assert.Equal(1.0, peak);
    }

    [Fact]
    public void Fertility_IsHersForLife_AndDeterministic()
    {
        for (int day = 0; day < 40; day++)
            Assert.Equal(MoodTides.Fertility(Her, day), MoodTides.Fertility(Her, day));

        // Two women do not share one calendar.
        bool everDiffer = false;
        for (int day = 0; day < 40 && !everDiffer; day++)
            everDiffer = MoodTides.Fertility(Her, day) != MoodTides.Fertility("spc_wanderer_9", day);
        Assert.True(everDiffer);
    }

    [Fact]
    public void FertileWindow_NeverOverlapsTheDaysOfTheCustom()
    {
        // The normalizer is one constant for every woman only because the window stands clear of
        // the closed days — if a shorter cycle ever put them together, the odds would silently sag.
        foreach (var id in new[] { "a", "b", "lord_1_1", "spc_wanderer_42", "x_y_z", Her })
        {
            int length = MoodTides.CycleLength(id);
            double sum = 0;
            for (int day = 0; day < length; day++)
            {
                double f = MoodTides.Fertility(id, day);
                if (f > MoodTides.QuietFertility) sum += f;
            }
            Assert.Equal(MoodTides.FertileWindowSum, sum, 6);
        }
    }

    // ------------------------------ the odds ------------------------------

    [Fact]
    public void TakingTheWholeWindow_MatchesTheWorldsOwnMonthlyReckoning()
    {
        // Anton's one rule: a player who takes every night of the window should father a child
        // about as readily as the game would have given him one on its own.
        const double vanillaDaily = 0.11; // a young wife with no children
        int length = MoodTides.CycleLength(Her);

        double vanillaMonthly = 1.0 - Math.Pow(1.0 - vanillaDaily, length);

        // EVERY night of the cycle, not only the window's. The quiet days carry weight too, and
        // leaving them out is precisely how the old normaliser came to be a tenth short.
        double missedEveryNight = 1.0;
        for (int day = 0; day < length; day++)
            missedEveryNight *= 1.0 - NightOdds.NightlyChanceFor(Her, day, vanillaDaily);
        double oursMonthly = 1.0 - missedEveryNight;

        // TWO-SIDED, AND THAT IS THE POINT (2026.08.16). The old bound was
        // InRange(ours, vanilla - 0.08, 1.0) — an upper limit of ONE, so the test could only ever
        // fail if the mod were too BARREN. It could not see an overshoot, and there was a large
        // one: the spread was additive, so the nightly chances summed to the expected COUNT of
        // conceptions rather than the chance of at least one, and a young wife's crest was being
        // clamped down from 66% (173% with a gift) by a rail meant only for flavour.
        // Spreading the hazard makes the match exact rather than approximate, so this is now
        // pinned tight from both sides.
        Assert.Equal(vanillaMonthly, oursMonthly, 6);
    }

    [Fact]
    public void TheMatchHoldsForEveryAgeEveryGiftAndEveryCycleLength()
    {
        // The invariant is provable, not fitted: sum of the hazards is the cycle's own hazard by
        // construction, so this holds identically wherever it is sampled.
        foreach (var who in new[] { Her, "lord_2_1_1", "companion_11", "lord_9_4_2" })
        {
            int length = MoodTides.CycleLength(who);
            foreach (var vanillaDaily in new[] { 0.0139, 0.0624, 0.1104, 0.1440, 0.1872 })
            {
                double vanillaMonthly = 1.0 - Math.Pow(1.0 - vanillaDaily, length);
                double missed = 1.0;
                for (int day = 0; day < length; day++)
                    missed *= 1.0 - NightOdds.NightlyChanceFor(who, day, vanillaDaily);
                Assert.Equal(vanillaMonthly, 1.0 - missed, 6);
            }
        }
    }

    [Fact]
    public void ACrestNightIsNoLongerPinnedAgainstTheRail()
    {
        // The symptom Anton reported: 85% plainly AND 85% with the grandest gift, because both
        // readings had left probability space and were being clipped by the same ceiling.
        const double vanillaDaily = 0.1104;                  // a childless wife of twenty-five
        int crest = CrestDayOf(Her);

        double plain = NightOdds.NightlyChanceFor(Her, crest, vanillaDaily);
        double jewel = NightOdds.NightlyChanceFor(Her, crest, vanillaDaily, giftMultiplier: 2.0);

        Assert.InRange(plain, 0.40, 0.55);
        Assert.True(jewel > plain + 0.15, $"a gift must still be felt: {plain:P1} vs {jewel:P1}");
        Assert.True(jewel < NightOdds.MaxNightlyChance, "and neither may sit on the rail");
    }

    private static int CrestDayOf(string who)
    {
        int length = MoodTides.CycleLength(who);
        int best = 0;
        double most = -1;
        for (int day = 0; day < length; day++)
        {
            double f = MoodTides.Fertility(who, day);
            if (f > most) { most = f; best = day; }
        }
        return best;
    }

    [Fact]
    public void ClosedDoorAndBarrenReckoningsBothAnswerNothing()
    {
        int closedDay = -1;
        for (int day = 0; day < 40 && closedDay < 0; day++)
            if (MoodTides.DoorIsClosed(Her, day)) closedDay = day;
        Assert.True(closedDay >= 0);

        Assert.Equal(0.0, NightOdds.NightlyChanceFor(Her, closedDay, 0.11));
        // A woman the world gives no chance (too old, already carrying) stays at nothing on any day.
        for (int day = 0; day < 40; day++)
            Assert.Equal(0.0, NightOdds.NightlyChanceFor(Her, day, 0.0));
    }

    [Fact]
    public void GiftsRaiseTheOdds_ButNoNightIsEverCertain()
    {
        int crestDay = -1;
        for (int day = 0; day < 40 && crestDay < 0; day++)
            if (MoodTides.Fertility(Her, day) >= 1.0) crestDay = day;
        Assert.True(crestDay >= 0);

        double plain = NightOdds.NightlyChanceFor(Her, crestDay, 0.11, NightGifts.Plain.Multiplier);
        double jewel = NightOdds.NightlyChanceFor(Her, crestDay, 0.11, NightGifts.Resolve(1000).Multiplier);
        Assert.True(jewel > plain);
        Assert.True(jewel <= NightOdds.MaxNightlyChance);

        // Even an absurd purse and an absurd dial cannot buy a certainty.
        Assert.Equal(NightOdds.MaxNightlyChance,
            NightOdds.NightlyChanceFor(Her, crestDay, 0.5, 5.0, 10.0));
    }

    [Fact]
    public void GiftTiers_ClimbInPriceAndInPromise_AndThePlainOneIsFree()
    {
        Assert.Equal(0, NightGifts.Plain.Price);
        Assert.False(NightGifts.Plain.WritesStory);
        Assert.Equal(1.0, NightGifts.Plain.Multiplier);

        var paid = NightGifts.Paid;
        Assert.Equal(4, paid.Count);
        for (int i = 1; i < paid.Count; i++)
        {
            Assert.True(paid[i].Price > paid[i - 1].Price);
            Assert.True(paid[i].Multiplier > paid[i - 1].Multiplier);
        }
        foreach (var tier in paid)
        {
            Assert.True(tier.WritesStory);
            Assert.False(string.IsNullOrWhiteSpace(tier.ChroniclerNote));
            Assert.False(string.IsNullOrWhiteSpace(tier.PlayerDescription));
        }
        // An unknown price from an older record still resolves to a real tier.
        Assert.Equal(NightGifts.Plain, NightGifts.Resolve(7));
    }

    [Fact]
    public void NightsFromCrest_FindsTheWomanNearestHerSeason()
    {
        for (int day = 0; day < 40; day++)
        {
            int off = NightOdds.NightsFromCrest(Her, day);
            if (MoodTides.DoorIsClosed(Her, day)) Assert.Equal(int.MaxValue, off);
            else Assert.InRange(off, -20, 20);
        }
    }

    // ------------------------------ the beats ------------------------------

    [Fact]
    public void Beats_CarryTheirMarks_AndTheNamedOneGivesItsNameBack()
    {
        var plain = NightText.PlainBeat("Mizam", "the town of Onira");
        Assert.True(NightText.IsNightBeat(plain));
        Assert.False(NightText.IsNamedNightBeat(plain));
        Assert.Contains("Mizam", plain);
        Assert.Contains("Onira", plain);

        var named = NightText.NamedBeat("Mizam", "the town of Onira", "The Cup of Amber Wine");
        Assert.True(NightText.IsNightBeat(named));
        Assert.True(NightText.IsNamedNightBeat(named));
        Assert.Equal("The Cup of Amber Wine", NightText.ExtractNightName(named));

        // The account itself never rides in the beat — only its name.
        Assert.True(named.Length < 200);

        // A night with no name falls back to the plain beat rather than an empty quotation.
        Assert.Equal(plain, NightText.NamedBeat("Mizam", "the town of Onira", "   "));
        Assert.False(NightText.IsNightBeat("He came to me."));
    }

    // ------------------------------ the roll ------------------------------

    private static NightRecord Night(double day, NightKind kind, string id = "n") => new NightRecord
    {
        Id = id,
        GameDay = day,
        WifeId = "wife",
        WifeName = "Sibylla",
        Kind = kind,
        PlaceName = "the town of Onira",
    };

    [Fact]
    public void Roll_TellsTheFreshestWritingsWhole_AndFoldsTheOlderOnesToTheirNames()
    {
        var nights = new List<NightRecord>();
        for (int i = 0; i < 8; i++)
        {
            var n = Night(100 + i, NightKind.Together, "n" + i);
            n.Title = "Night " + i;
            n.Story = new string('x', 80) + " and so it was.";
            n.StoryWanted = true;
            nights.Add(n);
        }

        var roll = NightText.BuildRoll(nights, today: 108, storiesInFull: 3);

        // The three freshest carry their accounts; the older ones only their names.
        Assert.Contains("Night 7", roll);
        Assert.Contains("and so it was.", roll);
        int accounts = roll.Split(new[] { "and so it was." }, StringSplitOptions.None).Length - 1;
        Assert.Equal(3, accounts);
        Assert.Contains("\"Night 0\"", roll);
    }

    [Fact]
    public void Roll_SpeaksEveryKindOfNight_InHerOwnVoice()
    {
        var closed = Night(100, NightKind.DoorClosed, "a");
        var elsewhere = Night(101, NightKind.Elsewhere, "b");
        elsewhere.OtherName = "Thyrsif";
        var heard = Night(102, NightKind.Elsewhere, "c");
        heard.OtherName = "Thyrsif";
        heard.ByHearsay = true;
        var alone = Night(103, NightKind.Alone, "d");
        var plain = Night(104, NightKind.Together, "e");

        var roll = NightText.BuildRoll(new[] { closed, elsewhere, heard, alone, plain }, today: 105);

        Assert.Contains(NightText.RollHeader, roll);
        Assert.Contains("custom of women", roll);
        Assert.Contains("he went to Thyrsif", roll);
        Assert.Contains("word reached me", roll);
        Assert.Contains("he slept alone", roll);
        Assert.Contains("he came to me", roll);
    }

    [Fact]
    public void Roll_KeepsTheDEAREST_NightWhole_HoweverOldItIs()
    {
        // Anton, 2026.08.11: "искам най-специалните нощи да вижда дори по-стари". Until now the
        // roll chose by recency alone, so the night a child was begun on scrolled out of her sheet
        // in four days while two ordinary evenings sat there in full.
        var jewel = Night(100, NightKind.Together, "jewel");
        jewel.GiftPrice = 1000;
        jewel.Conceived = true;
        jewel.Title = "Пръстенът на възглавницата";
        jewel.Story = "И тази нощ той ме позна, и от нея започна дете. " + new string('щ', 400);

        var nights = new List<NightRecord> { jewel };
        for (int day = 101; day <= 108; day++)
        {
            var small = Night(day, NightKind.Together, "n" + day);
            small.GiftPrice = 10;
            small.Title = "Виното " + day;
            small.Story = "Дребна вечер, и той дойде при мен. " + new string('я', 300);
            nights.Add(small);
        }

        var roll = NightText.BuildRoll(nights, today: 109, storiesInFull: 3);

        // The dearest night is told WHOLE though it is the oldest of the nine…
        Assert.Contains("от нея започна дете", roll);
        // …and so is the freshest, because the newest thing is what she has nearest her.
        Assert.Contains("Дребна вечер", roll);
        // The rest fold to the names she keeps them by, and no more than the three asked for are
        // ever told whole — eight ordinary evenings in full is the bloat this exists to prevent.
        Assert.Contains("\"Виното 104\"", roll);
        int told = roll.Split(new[] { "Дребна вечер" }, StringSplitOptions.None).Length - 1;
        Assert.InRange(told, 1, 2);

        // And the ranking itself: a child outranks any purse, and a purse outranks a cup of wine.
        Assert.True(NightText.Specialness(jewel) > NightText.Specialness(nights[1]));
        var plainJewel = Night(100, NightKind.Together, "x");
        plainJewel.GiftPrice = 1000;
        Assert.True(NightText.Specialness(jewel) > NightText.Specialness(plainJewel));
    }

    [Fact]
    public void Roll_GathersARunOfLikeNightsIntoOneLine()
    {
        // "ако влизам при нея всяка нощ авто, обикновенно... от тогава до тогава той беше с мене
        // почти всяка нощ" — ten lines saying one thing is ten lines saying one thing.
        var nights = new List<NightRecord>();
        for (int day = 100; day <= 108; day++) nights.Add(Night(day, NightKind.Together, "n" + day));
        var roll = NightText.BuildRoll(nights, today: 109);

        Assert.Contains("nearly every night", roll);
        Assert.Equal(1, roll.Split(new[] { "he came to me" }, StringSplitOptions.None).Length - 1);

        // A run that did NOT cover its own span must not claim it did.
        var sparse = new List<NightRecord>
        {
            Night(100, NightKind.Together, "a"), Night(104, NightKind.Together, "b"),
            Night(108, NightKind.Together, "c"),
        };
        var sparseRoll = NightText.BuildRoll(sparse, today: 109);
        Assert.Contains("on three of those nights", sparseRoll);
        Assert.DoesNotContain("nearly every night", sparseRoll);

        // A PAIR is still two evenings and keeps every word of its own nuance — she saw him go on
        // one of them and only heard of the other, and that difference is the whole point.
        var seen = Night(100, NightKind.Elsewhere, "a"); seen.OtherName = "Thyrsif";
        var heard = Night(101, NightKind.Elsewhere, "b"); heard.OtherName = "Thyrsif"; heard.ByHearsay = true;
        var pair = NightText.BuildRoll(new[] { seen, heard }, today: 102);
        Assert.Contains("he went to Thyrsif", pair);
        Assert.Contains("word reached me", pair);

        // But three of them gather, and the gathering names her.
        var three = NightText.BuildRoll(new[] { seen, heard, Night(102, NightKind.Elsewhere, "c") }, today: 103);
        Assert.Contains("Thyrsif", three);
        Assert.Contains("and not with me", three);

        // A written night is NEVER swallowed by a run — it always stands alone.
        var storied = Night(104, NightKind.Together, "s");
        storied.GiftPrice = 100; storied.Title = "Хлябът до лампата";
        storied.Story = "И той дойде, и лампата гореше ниско.";
        var mixed = new List<NightRecord>
        {
            Night(100, NightKind.Together, "a"), Night(101, NightKind.Together, "b"),
            Night(102, NightKind.Together, "c"), storied,
            Night(105, NightKind.Together, "d"), Night(106, NightKind.Together, "e"),
            Night(107, NightKind.Together, "f"),
        };
        var mixedRoll = NightText.BuildRoll(mixed, today: 108);
        Assert.Contains("лампата гореше ниско", mixedRoll);
        Assert.Equal(2, mixedRoll.Split(new[] { "nearly every night" }, StringSplitOptions.None).Length - 1);
    }

    // ------------------------------ the reckoning of a month (2026.08.11) ------------------------------

    private static NightMark Mark(double day, NightKind kind, int gift = 0, string other = "",
        string title = "", int otherPrice = 0, bool conceived = false) => new NightMark
        {
            GameDay = day, WifeId = "w", Kind = kind, GiftPrice = gift,
            OtherName = other, Title = title, OtherNightPrice = otherPrice, Conceived = conceived,
        };

    [Fact]
    public void TheReckoning_CountsTheMonthSheCanNoLongerRecite()
    {
        var marks = new List<NightMark>();
        for (int d = 70; d < 82; d++) marks.Add(Mark(d, NightKind.Together));
        marks.Add(Mark(82, NightKind.Together, gift: 100, title: "Топлата вода"));
        for (int d = 83; d < 86; d++) marks.Add(Mark(d, NightKind.Elsewhere, other: "Тирсиф"));
        marks.Add(Mark(86, NightKind.Elsewhere, other: "Тирсиф", title: "Гривната", otherPrice: 1000));
        marks.Add(Mark(87, NightKind.Alone));
        marks.Add(Mark(88, NightKind.Alone));
        for (int d = 91; d < 96; d++) marks.Add(Mark(d, NightKind.Together));
        marks.Add(Mark(96, NightKind.Together, gift: 1000, title: "Пръстенът", conceived: true));

        var said = NightText.BuildReckoning(marks, today: 99);

        Assert.Contains("he came to me", said);
        Assert.Contains("two of those he made something of", said);   // the 100 and the 1000
        Assert.Contains("slept alone twice", said);
        Assert.Contains("Тирсиф four times", said);
        Assert.Contains("our child was begun", said);

        // What lies OUTSIDE the window is outside it — this is a month, not a lifetime.
        Assert.DoesNotContain("Тирсиф", NightText.BuildReckoning(marks, today: 99, days: 5));
    }

    [Fact]
    public void TheReckoning_HearsMoreOfAGrandNightThanOfASmallOne()
    {
        // Anton, 2026.08.11: "с повече детайли колкото по грандиозна е била". A jug of wine passes
        // almost unremarked; a jewel is worn where the world can see it.
        var jewel = NightText.BuildReckoning(new List<NightMark>
        { Mark(90, NightKind.Elsewhere, other: "Тирсиф", title: "Гривната", otherPrice: 1000) }, today: 91);
        Assert.Contains("the whole house is still talking about", jewel);
        Assert.Contains("\"Гривната\"", jewel);

        var gown = NightText.BuildReckoning(new List<NightMark>
        { Mark(90, NightKind.Elsewhere, other: "Тирсиф", title: "Роклята", otherPrice: 300) }, today: 91);
        Assert.Contains("it was seen on her", gown);
        Assert.DoesNotContain("still talking about", gown);

        var wine = NightText.BuildReckoning(new List<NightMark>
        { Mark(90, NightKind.Elsewhere, other: "Тирсиф", title: "Чашата", otherPrice: 10) }, today: 91);
        Assert.Contains("a little something of it", wine);
        Assert.DoesNotContain("seen on her", wine);

        // A night she merely heard about, with nothing spent on it, stays a bare fact.
        var plain = NightText.BuildReckoning(new List<NightMark>
        { Mark(90, NightKind.Elsewhere, other: "Тирсиф") }, today: 91);
        Assert.Contains("he was with Тирсиф once", plain);
        Assert.DoesNotContain("—", plain);

        // And an empty month reckons nothing at all rather than saying so at length.
        Assert.Equal(string.Empty, NightText.BuildReckoning(new List<NightMark>(), today: 91));
        Assert.Equal(string.Empty, NightText.BuildReckoning(null, today: 91));
    }

    [Fact]
    public void TheThinMarksOutliveTheRecordsTheyWereMadeBeside()
    {
        // The whole point: a fortnight of prose buys a month of knowing.
        var ledger = new NightLedger();
        for (int day = 70; day <= 99; day++)
            ledger.Add(new NightRecord { WifeId = "w", GameDay = day, Kind = NightKind.Together }, maxPerWife: 14);

        Assert.Equal(14, ledger.For("w").Count);          // the records are pruned by count…
        Assert.Equal(30, ledger.MarksFor("w").Count);     // …and the marks are not.

        // Twenty-nine, not thirty: the window is the last 30 days, so the night 30 days ago is
        // already outside it. The point is that the count comes from far past the fourteen records.
        var said = NightText.BuildReckoning(ledger.MarksFor("w"), today: 100);
        Assert.Contains("he came to me 29 times", said);

        // An evening settled twice leaves ONE mark, not two.
        ledger.Add(new NightRecord { WifeId = "w", GameDay = 99, Kind = NightKind.Together }, maxPerWife: 14);
        Assert.Equal(30, ledger.MarksFor("w").Count);

        // And a night the chronicler names minutes later can be re-marked, or the reckoning would
        // never hear the name.
        var heard = ledger.Add(new NightRecord { WifeId = "w", GameDay = 100, Kind = NightKind.Elsewhere,
            OtherName = "Тирсиф" }, maxPerWife: 14);
        heard.OtherNightTitle = "Гривната";
        heard.OtherNightPrice = 1000;
        ledger.RefreshMark(heard);
        Assert.Contains("\"Гривната\"", NightText.BuildReckoning(ledger.MarksFor("w"), today: 101));
    }

    [Fact]
    public void Roll_CollapsesARunOfNightsSheNeverLearnedOf()
    {
        // Three "I don't know" lines in a row read as an accusation and are nothing of the kind.
        var nights = new List<NightRecord>
        {
            Night(100, NightKind.Unknown, "a"),
            Night(101, NightKind.Unknown, "b"),
            Night(102, NightKind.Unknown, "c"),
            Night(103, NightKind.Together, "d"),
        };
        var roll = NightText.BuildRoll(nights, today: 104);

        int unknowns = roll.Split(new[] { "never learned" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(0, unknowns);
        Assert.Contains("three of those nights", roll);
        Assert.Contains("he came to me", roll);
    }

    [Fact]
    public void Roll_TellsAWarNightApart_AndAnEmptyRollIsAnHonestNothing()
    {
        var war = Night(100, NightKind.Unknown, "a");
        war.AtWar = true;
        Assert.Contains("there was fighting", NightText.BuildRoll(new[] { war }, today: 101));
        Assert.Equal(string.Empty, NightText.BuildRoll(new List<NightRecord>(), today: 100));
    }

    [Fact]
    public void GiftTiers_BuyTalkAsWellAsOdds()
    {
        // The sharpest edge in the feature: a plain night halves the talk, a jewel doubles it.
        Assert.Equal(0.50, NightGifts.Plain.AwarenessMultiplier);
        Assert.Equal(2.00, NightGifts.Resolve(1000).AwarenessMultiplier);

        var paid = NightGifts.Paid;
        for (int i = 1; i < paid.Count; i++)
            Assert.True(paid[i].AwarenessMultiplier > paid[i - 1].AwarenessMultiplier);
        Assert.True(paid[0].AwarenessMultiplier > NightGifts.Plain.AwarenessMultiplier);
    }

    [Fact]
    public void ALeakedGrandNight_LeaksItsNameToo()
    {
        var heard = Night(100, NightKind.Elsewhere, "a");
        heard.OtherName = "Thyrsif";
        Assert.DoesNotContain("name for that night", NightText.LineFor(heard, 101, false));

        heard.OtherNightTitle = "The Cup of Amber Wine";
        var told = NightText.LineFor(heard, 101, false);
        Assert.Contains("They have a name for that night: \"The Cup of Amber Wine\"", told);
        Assert.Contains("Thyrsif", told);
    }

    [Fact]
    public void Ledger_RemembersWhichEveningsWereSettled()
    {
        // An evening never answered must be closable a day later — and a night where nobody
        // happened to notice anything writes no records at all, so the mark cannot be per-wife.
        var ledger = new NightLedger();
        Assert.False(ledger.IsNightSettled(100));

        ledger.SettleNight(100);
        Assert.True(ledger.IsNightSettled(100));
        Assert.True(ledger.IsNightSettled(99.5));
        Assert.False(ledger.IsNightSettled(101));

        ledger.SettleNight(99);                 // never walks backwards
        Assert.True(ledger.IsNightSettled(100));
    }

    [Fact]
    public void WhenPhrase_CountsInNights_NeverInDates()
    {
        Assert.Equal("tonight", NightText.WhenPhrase(100, 100));
        Assert.Equal("last night", NightText.WhenPhrase(99, 100));
        Assert.Equal("three nights ago", NightText.WhenPhrase(97, 100));
        Assert.Contains("fortnight", NightText.WhenPhrase(86, 100));
        Assert.Contains("weeks", NightText.WhenPhrase(70, 100));
    }

    // ------------------------------ the ledger ------------------------------

    [Fact]
    public void Ledger_KeepsAFortnightPerWife_AndNeverDoublesANight()
    {
        var ledger = new NightLedger();
        for (int i = 0; i < 20; i++)
            ledger.Add(new NightRecord { GameDay = 100 + i, WifeId = "a", Kind = NightKind.Alone });
        for (int i = 0; i < 5; i++)
            ledger.Add(new NightRecord { GameDay = 100 + i, WifeId = "b", Kind = NightKind.Alone });

        Assert.Equal(NightLedger.DefaultMaxPerWife, ledger.For("a").Count);
        Assert.Equal(5, ledger.For("b").Count);
        // The oldest went, the freshest stayed.
        Assert.Equal(119, ledger.For("a").Last().GameDay);

        // The evening tick firing twice across a save must not mint a twin.
        ledger.Add(new NightRecord { GameDay = 119.4, WifeId = "a", Kind = NightKind.Together });
        Assert.Equal(NightLedger.DefaultMaxPerWife, ledger.For("a").Count);
        Assert.Equal(NightKind.Together, ledger.For("a").Last().Kind);
        Assert.True(ledger.HasNightOn("a", 119.9));
        Assert.False(ledger.HasNightOn("a", 120.0));
    }

    [Fact]
    public void Ledger_FindsTheWorkStillOwed()
    {
        var ledger = new NightLedger();

        var owed = new NightRecord { GameDay = 100, WifeId = "a", Kind = NightKind.Together, StoryWanted = true };
        var written = new NightRecord
        {
            GameDay = 101, WifeId = "a", Kind = NightKind.Together,
            StoryWanted = true, Story = "It was so.", Title = "A name",
        };
        var spent = new NightRecord
        {
            GameDay = 102, WifeId = "a", Kind = NightKind.Together,
            StoryWanted = true, StoryAttempts = NightLedger.MaxStoryAttempts,
        };
        ledger.Add(owed); ledger.Add(written); ledger.Add(spent);

        var awaiting = ledger.AwaitingStories();
        Assert.Single(awaiting);
        Assert.Equal(100, awaiting[0].GameDay);

        Assert.True(written.IsStoried);
        Assert.False(owed.IsStoried);
        Assert.Equal(3, ledger.AwaitingBeats().Count);

        Assert.Equal(102, ledger.LastTogether("a")!.GameDay);
        Assert.Equal(102, ledger.LastTogetherWithAnyone()!.GameDay);
    }

    [Fact]
    public void Ledger_HoldsAConceptionUntilTheDayOfKnowing()
    {
        var ledger = new NightLedger();
        var night = new NightRecord
        {
            GameDay = 100, WifeId = "a", Kind = NightKind.Together,
            Conceived = true, RevealDay = 107,
        };
        ledger.Add(night);

        Assert.True(ledger.HasPendingConception("a"));
        Assert.Empty(ledger.DueForReveal(106.9));
        Assert.Single(ledger.DueForReveal(107.0));

        night.Revealed = true;
        Assert.Empty(ledger.DueForReveal(120));
        Assert.False(ledger.HasPendingConception("a"));
    }

    [Fact]
    public void Ledger_SurvivesTheRoundTrip_AndAMissingFileIsAnEmptyBook()
    {
        var path = Path.Combine(Path.GetTempPath(), "iai_nights_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var ledger = new NightLedger();
            ledger.Add(new NightRecord
            {
                GameDay = 100, WifeId = "a", WifeName = "Sibylla", Kind = NightKind.Together,
                Title = "The Cup of Amber Wine", Story = "It was so.", GiftPrice = 10, Conceived = true,
            });
            ledger.SaveTo(path);

            var read = NightLedger.LoadFrom(path);
            Assert.Single(read.Nights);
            Assert.Equal("The Cup of Amber Wine", read.Nights[0].Title);
            Assert.Equal(NightKind.Together, read.Nights[0].Kind);
            Assert.True(read.Nights[0].Conceived);

            Assert.Empty(NightLedger.LoadFrom(path + ".missing").Nights);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // ------------------------------ the chronicler ------------------------------

    private static NightText.Facts SomeFacts() => new NightText.Facts
    {
        WifeName = "Sibylla",
        WifeAge = 24,
        WifeStation = "a Sturgian wanderer who rides with him",
        WifeSeason = "these are the rising days",
        WifeHumor = "in bright spirits",
        PartnerName = "Mizam",
        DateText = "Autumn 14, Year 1084",
        PlacePhrase = "the town of Onira",
        GiftNote = NightGifts.Resolve(10).ChroniclerNote,
        RecentWords = "Обичам те. — И аз теб.",
    };

    [Fact]
    public void StoryPrompt_HoldsBothHalvesOfTheRule_AndTheScale()
    {
        var prompt = NightText.BuildStoryPrompt(SomeFacts());

        Assert.Contains("Song of Songs", prompt);
        Assert.Contains("NOTHING coarse", prompt);
        Assert.Contains("NOTHING coy", prompt);          // the half that is always the first to be lost
        Assert.Contains("not the wedding happening again", prompt);
        Assert.Contains("THREE TO FIVE sentences", prompt);
        Assert.Contains("TITLE:", prompt);
        Assert.Contains("Sibylla", prompt);
        Assert.Contains("Mizam", prompt);
        Assert.Contains("Onira", prompt);

        // The tongue rule rides last, carrying their own words as its evidence.
        Assert.Contains("THE TONGUE", prompt);
        Assert.Contains("Обичам те", prompt);
        Assert.True(prompt.IndexOf("THE TONGUE", StringComparison.Ordinal)
                  > prompt.IndexOf("TITLE:", StringComparison.Ordinal));
    }

    // ------------------------------ what HE had in mind (2026.08.15) ------------------------------

    [Fact]
    public void ThePlayersWish_ReachesTheChronicler_AndStaysHis()
    {
        var facts = SomeFacts();
        facts.PlayerWish = "I want us to spend the night under the stars, away from the walls";
        var prompt = NightText.BuildStoryPrompt(facts);

        // It arrives as a fact of the night, in his own words, marked as his.
        Assert.Contains("What HE had in mind for this night, in his own words", prompt);
        Assert.Contains("under the stars", prompt);

        // And it arrives fenced. The first rail keeps it from being copied as prose; the second is
        // the load-bearing one — this is the only line in the whole feature the player writes, so
        // it is the only place he could reach past his own side of the night and script her.
        Assert.Contains("shapes the evening as far as a man can shape one", prompt);
        Assert.Contains("he does not write her", prompt);
        Assert.Contains("could not be had where they actually were", prompt);
    }

    [Fact]
    public void NoWish_LeavesThePromptExactlyAsItWas()
    {
        // The usual night is the overwhelmingly common one, and it must not pay a syllable for a
        // feature it is not using — neither the fact nor its rails may appear unasked.
        var prompt = NightText.BuildStoryPrompt(SomeFacts());

        Assert.DoesNotContain("had in mind", prompt);
        Assert.DoesNotContain("he does not write her", prompt);
    }

    [Fact]
    public void TheWishIsKeptInHisOwnKeepsake()
    {
        var night = new NightRecord
        {
            GameDay = 91.0,
            DateText = "Autumn 14, Year 1084",
            WifeName = "Sibylla",
            Kind = NightKind.Together,
            PlaceName = "the town of Onira",
            GiftPrice = 100,
            GiftName = "Hot water, oil, and a table for two",
            Wish = "I want to tell her about my mother",
            Title = "The Table Laid For Two",
            Story = "He had the water carried up before I came in from the yard, and the room smelled of it.",
        };

        var entry = NightText.KeepsakeEntry(night);
        Assert.Contains("What you had in mind: I want to tell her about my mother", entry);

        night.Wish = string.Empty;
        Assert.DoesNotContain("What you had in mind", NightText.KeepsakeEntry(night));
    }

    // ------------------------------ what the coin buys the writing (2026.08.10) ------------------------------

    [Fact]
    public void GiftTiers_BuyRoomAsWellAsOdds_OnAntonsOwnRails()
    {
        // His three rails, exactly: ten denars is three or four sentences, a hundred is five or
        // six, a thousand is seven or eight. The three-hundred tier interpolates between them.
        Assert.Equal(3, NightGifts.Resolve(10).MinSentences);
        Assert.Equal(4, NightGifts.Resolve(10).MaxSentences);
        Assert.Equal(5, NightGifts.Resolve(100).MinSentences);
        Assert.Equal(6, NightGifts.Resolve(100).MaxSentences);
        Assert.Equal(7, NightGifts.Resolve(1000).MinSentences);
        Assert.Equal(8, NightGifts.Resolve(1000).MaxSentences);

        var paid = NightGifts.Paid;
        for (int i = 1; i < paid.Count; i++)
        {
            Assert.True(paid[i].MinSentences >= paid[i - 1].MinSentences,
                $"{paid[i].Name} should never buy less room than {paid[i - 1].Name}");
            Assert.True(paid[i].MaxSentences > paid[i - 1].MaxSentences,
                $"{paid[i].Name} should buy more room than {paid[i - 1].Name}");
            Assert.True(paid[i].MinSentences <= paid[i].MaxSentences);
        }

        // And the grandest night of a marriage still stays under the wedding night's own twelve:
        // that one is the once, and nothing after it may read as long.
        Assert.True(paid[paid.Count - 1].MaxSentences < 12);
    }

    [Fact]
    public void StoryPrompt_AsksForBothHalvesOfTheNight_AndForThisTiersRoom()
    {
        var facts = SomeFacts();
        var jewel = NightGifts.Resolve(1000);
        facts.GiftNote = jewel.ChroniclerNote;
        facts.MinSentences = jewel.MinSentences;
        facts.MaxSentences = jewel.MaxSentences;
        var prompt = NightText.BuildStoryPrompt(facts);

        Assert.Contains("SEVEN TO EIGHT sentences", prompt);
        Assert.Contains("two roughly even halves", prompt);

        // Half one is the surprise and the setting; half two is the act, and the whole point of
        // naming it is that it may NOT be a door politely closed.
        Assert.Contains("THE FIRST HALF", prompt);
        Assert.Contains("THE SECOND HALF", prompt);
        Assert.Contains("NOT a door politely closed", prompt);
        Assert.Contains("Stay in the room with them", prompt);

        // The register still binds both halves.
        Assert.Contains("NOTHING coarse", prompt);
        Assert.Contains("NOTHING coy", prompt);

        // And the named images must stay images, never a checklist — the same lesson the gift
        // notes were cut back for. Finished prose handed to a model comes back word for word.
        Assert.Contains("not a list to work through", prompt);
        Assert.Contains("not wording to lift", prompt);

        // A ten-denar night is told the smaller truth about its own length.
        var small = SomeFacts();
        small.MinSentences = NightGifts.Resolve(10).MinSentences;
        small.MaxSentences = NightGifts.Resolve(10).MaxSentences;
        Assert.Contains("THREE TO FOUR sentences", NightText.BuildStoryPrompt(small));
    }

    [Fact]
    public void TheImageDeck_DealsAStableHand_AndADifferentOneEachNight()
    {
        // WHY THE DECK EXISTS, live-probed on gpt-5.6-luna: an image NAMED in the prompt comes back
        // in the answer near enough word for word — the bird startled in the brush turned up in two
        // nights running. Naming none loses the register; naming the same three forever turns a
        // marriage into one evening repeated. So a hand is dealt per night instead.
        var hand = NightText.DrawImages("d1131");
        Assert.Equal(3, hand.Count);
        Assert.Equal(hand.Count, hand.Distinct().Count());
        foreach (var card in hand) Assert.Contains(card, NightText.ImageDeck);

        // STABLE: a night retried an hour later must reach for the same images, or it becomes a
        // different night the second time it is written.
        Assert.Equal(hand, NightText.DrawImages("d1131"));

        // And a marriage's nights must not all be dealt the same hand. Neighbouring ids are the
        // real case (they are minted a day apart), so those are what is checked.
        var seeds = Enumerable.Range(1100, 40).Select(d => "d" + d).ToList();
        var hands = seeds.Select(s => string.Join("|", NightText.DrawImages(s))).ToList();
        Assert.True(hands.Distinct().Count() >= seeds.Count * 3 / 4,
            "consecutive nights are collapsing onto the same hand");

        // The second-of-a-day id shape must not shadow the first's.
        Assert.NotEqual(string.Join("|", NightText.DrawImages("d1120")),
                        string.Join("|", NightText.DrawImages("d1120-2")));
    }

    [Fact]
    public void TheImageDeck_SurvivesEveryDegenerateAsk()
    {
        Assert.Empty(NightText.DrawImages("d1", 0));
        Assert.Empty(NightText.DrawImages("d1", -3));
        Assert.Equal(NightText.ImageDeck.Count, NightText.DrawImages("d1", 999).Count);
        Assert.Equal(NightText.ImageDeck.Count, NightText.DrawImages("d1", 999).Distinct().Count());

        // A caller with no id to give still gets a usable hand rather than an exception.
        Assert.Equal(3, NightText.DrawImages(null).Count);
        Assert.Equal(3, NightText.DrawImages(string.Empty).Count);

        // The deck is worth keeping long — that is the whole defence against repetition.
        Assert.True(NightText.ImageDeck.Count >= 12);
        Assert.Equal(NightText.ImageDeck.Count, NightText.ImageDeck.Distinct().Count());
    }

    [Fact]
    public void StoryPrompt_CarriesTonightsHand_AndTellsItNotToWorkThroughIt()
    {
        var facts = SomeFacts();
        facts.ImageSeed = "d1131";
        var prompt = NightText.BuildStoryPrompt(facts);

        foreach (var card in NightText.DrawImages("d1131"))
            Assert.Contains(card, prompt);
        Assert.Contains("Take ONE, perhaps two", prompt);
        Assert.Contains("must not work through them like a list", prompt);
        Assert.Contains("Another night will have other images", prompt);

        // The one fixed image that used to be hardcoded into the second half is gone from it, so
        // the deck is the ONLY place images come from.
        Assert.DoesNotContain("like a small bird startled in the brush, out to her fingertips", prompt);

        // And whole sentences, because at three sentences it packed the whole act into one
        // run-on chain of clauses (live probe, 2026.08.10).
        Assert.Contains("Whole sentences", prompt);
        Assert.Contains("Do not pack half the night into one long chain of clauses", prompt);
    }

    [Fact]
    public void ARicherNightIsNotSilentlyCutBackToAPlainOnesLength()
    {
        // The failure this guards against is the quietest kind: the ceiling is raised, the model
        // answers at the new length, and the tamer's old flat cut takes the last third off — a bug
        // the player would read as the chronicler trailing away, and would never think to report.
        // Cyrillic makes it certain rather than likely: these sentences run ~250 characters each.
        var eight = string.Join(" ", Enumerable.Repeat(
            "В стаята лампата гореше ниско, а той остави плаща си на стола и ми подаде чашата, "
          + "сякаш ми поднасяше нещо много по-скъпо от вино, и аз се засмях тихо на това, защото "
          + "знаех колко път е изминал заради тази вечер и колко малко думи ще намери за нея.", 8));
        var raw = "TITLE: Пръстенът на възглавницата\n\n" + eight;

        Assert.True(NightText.TryParseStory(raw, out _, out var whole, maxSentences: 8));
        Assert.True(whole.Length > 1600, "the test's own sample must be longer than the old flat cap");
        Assert.EndsWith("за нея.", whole);

        // The same answer asked for at a plain night's length IS cut — which is what makes the
        // budget load-bearing rather than decorative.
        Assert.True(NightText.TryParseStory(raw, out _, out var cut, maxSentences: 4));
        Assert.True(cut.Length < whole.Length);

        Assert.True(NightText.AccountCharBudget(8) > NightText.AccountCharBudget(4));
        Assert.True(NightText.AccountCharBudget(2) >= 1600, "no tier may buy LESS room than we always gave");
    }

    [Fact]
    public void WhatTheChroniclerIsToldOfAGift_StaysFactsAndNeverBecomesProse()
    {
        // Finished prose handed to a model comes back almost word for word, and every ten-denar
        // night in a marriage would read the same by the tenth (Anton, 2026.08.10). These are a
        // handful of nouns, and the prompt says so out loud.
        foreach (var tier in NightGifts.Paid)
        {
            var note = tier.ChroniclerNote;
            Assert.True(note.Length < 160, $"{tier.Name}'s note is growing back into prose");
            Assert.False(note.Contains("He had "), $"{tier.Name}'s note is a written sentence again");
            Assert.Equal(note.TrimStart(), note);
        }

        var prompt = NightText.BuildStoryPrompt(SomeFacts());
        Assert.Contains("the bare facts of it, not words to reuse", prompt);
        Assert.Contains("FACTS, not phrasing", prompt);
    }

    [Fact]
    public void StoryPrompt_SteersAwayFromTheNightsAlreadyWritten()
    {
        var facts = SomeFacts();
        facts.PastNightNames = new List<string> { "Чашата кехлибарено вино", "The Lamp Burned Down" };
        var prompt = NightText.BuildStoryPrompt(facts);

        Assert.Contains("already carry these names", prompt);
        Assert.Contains("Чашата кехлибарено вино", prompt);
        Assert.Contains("The Lamp Burned Down", prompt);
        Assert.Contains("not one evening repeated", prompt);

        // A first night has nothing to steer away from, and must not be told it has.
        Assert.DoesNotContain("already carry these names", NightText.BuildStoryPrompt(SomeFacts()));
    }

    [Fact]
    public void StoryPrompt_WillNotFurnishARoomOntoAnOpenRoad()
    {
        var facts = SomeFacts();
        facts.PlacePhrase = string.Empty;
        var prompt = NightText.BuildStoryPrompt(facts);

        Assert.Contains("no roof and no walls", prompt);
        Assert.Contains("there was no room, no door, no bed", prompt);
    }

    [Fact]
    public void StoryPrompt_TellsTheChroniclerWhenSheIsAlreadyCarrying()
    {
        var facts = SomeFacts();
        facts.WithChild = true;
        Assert.Contains("already carrying", NightText.BuildStoryPrompt(facts));
    }

    [Fact]
    public void ParseStory_TakesTheNameOffTheTop_AndTamesTheRest()
    {
        var raw = "TITLE: The Cup of Amber Wine\n\n"
                + "He came in with the wine still cold from the cellar, and I laughed at him for it. "
                + "We drank it slowly, and he told me of the road. The lamp burned down while we talked.";

        Assert.True(NightText.TryParseStory(raw, out var title, out var story));
        Assert.Equal("The Cup of Amber Wine", title);
        Assert.StartsWith("He came in with the wine", story);
        Assert.DoesNotContain("TITLE", story);
    }

    [Fact]
    public void ParseStory_SurvivesMarkdownFencingAndQuotedNames()
    {
        var raw = "```\n**TITLE:** \"The Lamp Burned Down\"\n\n"
                + "The lamp burned down while we talked, and neither of us rose to trim it. "
                + "He was cold from the road and I warmed him as a wife does. Morning came too soon.\n```";

        Assert.True(NightText.TryParseStory(raw, out var title, out var story));
        Assert.Equal("The Lamp Burned Down", title);
        Assert.DoesNotContain("```", story);
        Assert.StartsWith("The lamp burned down", story);
    }

    [Fact]
    public void ParseStory_AcceptsAnAccountWithNoName_ButRefusesARefusal()
    {
        var noTitle = "He came to me late, smelling of horses and rain, and said nothing for a while. "
                    + "I did not need him to. The night was ours and it was enough.";
        Assert.True(NightText.TryParseStory(noTitle, out var title, out var story));
        Assert.Equal(string.Empty, title);
        Assert.False(string.IsNullOrWhiteSpace(story));

        Assert.False(NightText.TryParseStory("I'm sorry, I can't.", out _, out _));
        Assert.False(NightText.TryParseStory("   ", out _, out _));
        Assert.False(NightText.TryParseStory(null, out _, out _));
    }

    [Fact]
    public void CleanTitle_StripsTheDressing()
    {
        Assert.Equal("The Cup of Amber Wine", NightText.CleanTitle("**\"The Cup of Amber Wine.\"**"));
        Assert.Equal("Чашата кехлибарено вино", NightText.CleanTitle("  Чашата кехлибарено вино  "));
        Assert.Equal(string.Empty, NightText.CleanTitle(null));
        Assert.True(NightText.CleanTitle(new string('x', 200)).Length <= 71);
    }

    // ------------------------------ what the player reads ------------------------------

    [Fact]
    public void KeepsakeEntry_HoldsTheNightWhole()
    {
        var night = new NightRecord
        {
            GameDay = 100, DateText = "Autumn 14, Year 1084", PlaceName = "the town of Onira",
            WifeName = "Sibylla", Kind = NightKind.Together,
            GiftPrice = 1000, GiftName = "A jewel", Title = "The Cup of Amber Wine",
            Story = "It was so, and the lamp burned down.", SeasonWord = "her season is at its height",
            Conceived = true,
        };

        var entry = NightText.KeepsakeEntry(night);
        Assert.Contains("The Cup of Amber Wine", entry);
        Assert.Contains("Sibylla", entry);
        Assert.Contains("1000 denars", entry);
        Assert.Contains("a child was begun", entry);
        Assert.Contains("the lamp burned down", entry);
    }

    [Fact]
    public void OddsLine_AndTheCustomDaysNotice_SpeakPlainly()
    {
        Assert.Contains("40%", NightText.OddsLine("Sibylla", 0.4, conceived: true));
        Assert.Contains("a child was begun", NightText.OddsLine("Sibylla", 0.4, conceived: true));
        Assert.Contains("no child came of it", NightText.OddsLine("Sibylla", 0.4, conceived: false));

        Assert.Contains("Sibylla is in the custom", NightText.CustomDaysNotice(new[] { "Sibylla" }));
        Assert.Contains("Sibylla and Thyrsif are", NightText.CustomDaysNotice(new[] { "Sibylla", "Thyrsif" }));
        Assert.Equal(string.Empty, NightText.CustomDaysNotice(new string[0]));
    }

    [Fact]
    public void LikelihoodWord_NeverLeaksANumber()
    {
        foreach (var f in new[] { 0.0, 0.03, 0.25, 0.45, 0.65, 0.85, 1.0 })
        {
            var word = NightOdds.LikelihoodWord(f, doorClosed: false);
            Assert.False(string.IsNullOrWhiteSpace(word));
            Assert.DoesNotContain("%", word);
        }
        Assert.Contains("no child can come of it", NightOdds.LikelihoodWord(0.0, doorClosed: true));
    }

    // ------------------------------ the night's clock, by the sun ------------------------------

    private const int Reset = NightClock.DefaultResetHour;   // 16:00

    private static double At(int day, double hour) => day + hour / 24.0;

    [Fact]
    public void ACycleRunsFromOneLateAfternoonToTheNext()
    {
        Assert.Equal(5, NightClock.CycleOf(At(5, 16), Reset));    // the turn itself opens the cycle
        Assert.Equal(5, NightClock.CycleOf(At(5, 21), Reset));    // the evening's question
        Assert.Equal(5, NightClock.CycleOf(At(6, 1), Reset));     // one in the morning is still that evening
        Assert.Equal(5, NightClock.CycleOf(At(6, 15.9), Reset));  // and so is the whole day after it
        Assert.Equal(6, NightClock.CycleOf(At(6, 16), Reset));    // until the sun comes round again
    }

    [Fact]
    public void TheSmallHoursBelongToTheEveningTheyGrewOutOf()
    {
        // THE WHOLE POINT of the cycle. A night at one in the morning used to settle the day that
        // was only just beginning, which cost the player the entire following evening.
        var night = At(6, 1);
        var nextEvening = At(6, 21);
        Assert.False(NightClock.SameCycle(night, nextEvening, Reset));

        var ledger = new NightLedger();
        ledger.SettleNight(night);
        Assert.True(ledger.IsNightSettled(nextEvening));            // the calendar says yes...
        Assert.False(ledger.IsCycleSettled(nextEvening, Reset));    // ...and the sun says he is free
    }

    [Fact]
    public void ANightIsSpentOncePerCycleHoweverLateItRan()
    {
        var ledger = new NightLedger();
        ledger.SettleNight(At(5, 23.5));                              // half past eleven
        Assert.True(ledger.IsCycleSettled(At(6, 1), Reset));          // still the same evening
        Assert.True(ledger.IsCycleSettled(At(6, 15), Reset));         // and all the next day
        Assert.False(ledger.IsCycleSettled(At(6, 16), Reset));        // ready again at the turn

        // The drift the flat cooldown had: 23.5 + 24 = 23.5 the next night, hours past the
        // evening's own question. The sun does not drift.
        Assert.False(ledger.IsCycleSettled(At(6, 21), Reset));
    }

    [Fact]
    public void HoursUntilResetCountsToTheTurnAndNeverToZero()
    {
        Assert.Equal(1.0, NightClock.HoursUntilReset(At(5, 15), Reset), 3);
        Assert.Equal(19.0, NightClock.HoursUntilReset(At(5, 21), Reset), 3);
        Assert.Equal(15.0, NightClock.HoursUntilReset(At(6, 1), Reset), 3);
        Assert.Equal(24.0, NightClock.HoursUntilReset(At(5, 16), Reset), 3);   // just missed it
    }

    [Fact]
    public void AHandEditedResetHourCanNeverThrow()
    {
        foreach (var hour in new[] { -5, 0, 23, 24, 99 })
        {
            var cycle = NightClock.CycleOf(At(5, 12), hour);
            var left = NightClock.HoursUntilReset(At(5, 12), hour);
            Assert.True(cycle == 4 || cycle == 5);
            Assert.InRange(left, 0.001, 24.0);
        }
    }

    [Fact]
    public void AnEmptyLedgerHasSettledNothing()
    {
        var ledger = new NightLedger();
        Assert.False(ledger.IsCycleSettled(At(5, 21), Reset));
    }
}
