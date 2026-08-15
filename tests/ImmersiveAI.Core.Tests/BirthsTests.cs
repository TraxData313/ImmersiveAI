using ImmersiveAI.Core.Births;
using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Tests;

public class BirthsTests
{
    private const string MotherId = "lord_7_13_1";
    private const string FatherId = "main_hero";
    private const string WitnessId = "companion_4";
    private const string StrangerId = "lord_9_9_9";

    private static BirthRecord SomeBirth() => new BirthRecord
    {
        Id = "d1120",
        GameDay = 1120,
        DateText = "Winter 12, Year 1084",
        PlaceName = "the town of Akkalat",
        CultureName = "Khuzait",
        MotherId = MotherId,
        MotherName = "Sibylla",
        FatherId = FatherId,
        FatherName = "Mizam",
        FatherIsPlayer = true,
        PlayerWasThere = true,
        OlderChildren = 0,
        Children = { new BirthChild { HeroId = "CharacterObject_9001", Name = "Ira", IsFemale = false } },
        Witnesses = { new BirthWitness { HeroId = WitnessId, Name = "Yngvald", Detail = "their scout" } },
        BirthAccount = "Часът дойде върху мен призори и не пуснах ръката му, докато не се роди.",
        FeastAccount = "И се събраха в залата, и хлябът беше разчупен, и името му беше изречено.",
    };

    // ------------------------- what a feast costs, and what the coin buys -------------------------

    [Fact]
    public void Tiers_ClimbLikeTheWeddings_ButPayFarLessForTheName()
    {
        var tiers = BirthTiers.All;
        Assert.Equal(5, tiers.Count);

        for (int i = 1; i < tiers.Count; i++)
        {
            Assert.True(tiers[i].Price > tiers[i - 1].Price, "prices must climb");
            Assert.True(tiers[i].WitnessCap > tiers[i - 1].WitnessCap, "the hall must grow with the purse");
            Assert.True(tiers[i].Renown > tiers[i - 1].Renown, "so must the name");
            Assert.True(tiers[i].MinVenue >= tiers[i - 1].MinVenue, "and the place asked for");
        }

        // The ladder is deliberately the SAME ladder as a wedding's, denar for denar — one mental
        // model is worth more than a cleverer second one.
        Assert.Equal(WeddingTiers.All.Select(t => t.Price).ToList(), tiers.Select(t => t.Price).ToList());

        // But a child is joy, not an alliance, and children are far easier to come by than
        // weddings — so no rung may pay MORE than its wedding twin, and every rung with room to
        // move pays strictly less. Without this a polygamous house would have found the cheapest
        // renown shop in Calradia. (The smallest feast is already at the floor of one point;
        // there is nowhere under it that is not zero.)
        for (int i = 0; i < tiers.Count; i++)
            Assert.True(tiers[i].Renown <= WeddingTiers.All[i].Renown,
                $"{tiers[i].Name} must never pay more than a wedding's renown");
        for (int i = 1; i < tiers.Count; i++)
            Assert.True(tiers[i].Renown < WeddingTiers.All[i].Renown,
                $"{tiers[i].Name} must pay markedly less than its wedding twin");
        Assert.Equal(1, tiers[0].Renown);

        Assert.True(BirthTiers.Of(BirthScale.Legendary)!.RequiresOwnTown);
        Assert.False(BirthTiers.Of(BirthScale.Regal)!.RequiresOwnTown);
    }

    [Fact]
    public void TheNamingFeast_CallsOurOwnAndNoStrangers()
    {
        // Anton's instruction, given for the wedding's thousand-denar rung and applied here from
        // the first line: at a thousand denars nobody stands there who was not called.
        var named = BirthTiers.Of(BirthScale.Named)!;
        Assert.Equal(1000, named.Price);
        Assert.False(named.AdmitsLocalStrangers);
        Assert.True(named.InvitesRememberedBonds);
        Assert.True(named.InvitesKin);
        Assert.False(named.InvitesLordsOfTheRealm);
        Assert.False(named.InvitesGreatNames);
        Assert.Contains("NO ONE ELSE", named.ChroniclerNote);

        // The quiet feast called nobody, so whoever was at hand was at hand.
        Assert.True(BirthTiers.Of(BirthScale.Quiet)!.AdmitsLocalStrangers);
        Assert.False(BirthTiers.Of(BirthScale.Quiet)!.InvitesRememberedBonds);

        // And the shared shape both chronicles read carries it faithfully.
        var rules = BirthTiers.Guests(BirthScale.Named);
        Assert.False(rules.LocalsPresent);
        Assert.True(rules.RememberedBonds && rules.Kin);
        Assert.Equal(named.WitnessCap, rules.Cap);

        // A birth nobody feasted gathers nobody by invitation.
        var none = BirthTiers.Guests(BirthScale.Unfeasted);
        Assert.False(none.RememberedBonds);
        Assert.Equal(BirthTiers.DefaultWitnessCap, none.Cap);
        Assert.Equal(0, BirthTiers.PriceOf(BirthScale.Unfeasted));
        Assert.Equal(string.Empty, BirthTiers.ChroniclerNote(BirthScale.Unfeasted));
    }

    [Theory]
    [InlineData(BirthScale.Quiet, WeddingVenue.OpenField, true)]
    [InlineData(BirthScale.Named, WeddingVenue.OpenField, false)]
    [InlineData(BirthScale.Named, WeddingVenue.Village, true)]
    [InlineData(BirthScale.Great, WeddingVenue.Village, false)]
    [InlineData(BirthScale.Great, WeddingVenue.Castle, true)]
    [InlineData(BirthScale.Regal, WeddingVenue.Castle, false)]
    [InlineData(BirthScale.Regal, WeddingVenue.Town, true)]
    public void Tiers_AskThePlaceToBeWorthyOfThem(BirthScale scale, WeddingVenue venue, bool fits) =>
        Assert.Equal(fits, BirthTiers.Of(scale)!.FitsIn(venue, inOwnTown: false));

    // ------------------------- the record's own plain words -------------------------

    [Theory]
    [InlineData(false, false, "a son")]
    [InlineData(true, false, "a daughter")]
    public void ChildWords_SayWhatCame(bool female, bool twin, string expected)
    {
        var record = new BirthRecord();
        record.Children.Add(new BirthChild { Name = "Ira", IsFemale = female });
        if (twin) record.Children.Add(new BirthChild { Name = "Tulag", IsFemale = female });
        Assert.Equal(expected, record.ChildWords());
    }

    [Fact]
    public void ChildWords_AndNames_HandleTwinsAndAnEmptyCradle()
    {
        var twoSons = new BirthRecord();
        twoSons.Children.Add(new BirthChild { Name = "Ira", IsFemale = false });
        twoSons.Children.Add(new BirthChild { Name = "Tulag", IsFemale = false });
        Assert.Equal("twin sons", twoSons.ChildWords());
        Assert.Equal("Ira and Tulag", twoSons.ChildNames());

        var mixed = new BirthRecord();
        mixed.Children.Add(new BirthChild { Name = "Ira", IsFemale = false });
        mixed.Children.Add(new BirthChild { Name = "Aliya", IsFemale = true });
        Assert.Equal("a son and a daughter", mixed.ChildWords());

        // A birth with nothing living still answers plainly rather than throwing.
        var none = new BirthRecord();
        Assert.False(none.AnyLived);
        Assert.Equal("a child", none.ChildWords());
        Assert.Equal("the child", none.ChildNames());
    }

    // ------------------------- the privacy line, which is code and not prose -------------------------

    [Fact]
    public void TheHourBelongsToTheParents_AndTheWitnessIsToldSoPlainly()
    {
        var record = SomeBirth();

        Assert.True(record.IsParent(MotherId));
        Assert.True(record.IsParent(FatherId));
        Assert.False(record.IsParent(WitnessId));
        Assert.True(record.WasThere(WitnessId));
        Assert.False(record.WasThere(StrangerId));

        // The mother reads her own memory back, whole.
        var hers = BirthText.FullAccount(record, includeHour: true, asMother: true);
        Assert.Contains(record.BirthAccount, hers);
        Assert.DoesNotContain("she told me", hers);

        // The father is given the same hour, but framed as what she told him of it — which is the
        // honest way a man comes to know an hour he may not have been in the room for.
        var his = BirthText.FullAccount(record, includeHour: true, asMother: false);
        Assert.Contains(record.BirthAccount, his);
        Assert.Contains("she told me of that hour", his);

        // A witness gets the feast and is refused the rest, in words, not by silence.
        var theirs = BirthText.FullAccount(record, includeHour: false, asMother: false);
        Assert.DoesNotContain(record.BirthAccount, theirs);
        Assert.Contains("belongs to the two of them", theirs);
        Assert.Contains(record.FeastAccount, theirs);
    }

    [Fact]
    public void TheDayIsToldAtTheAgeItHappened_AndSaysHowOldTheChildWouldBe()
    {
        var record = SomeBirth();
        record.MotherAge = 24;
        record.FatherAge = 27;

        var told = BirthText.FullAccount(record, includeHour: true, asMother: true, yearsSince: 6.9);
        Assert.Contains("Sibylla was about 24 and Mizam about 27", told);
        Assert.Contains("some 6 years ago", told);
        // "would be", never "is": the ledger knows what was born, not who is still living.
        Assert.Contains("the child would be that old now", told);

        var fresh = BirthText.FullAccount(record, includeHour: true, asMother: true, yearsSince: 0.2);
        Assert.Contains("was about 24", fresh);
        Assert.DoesNotContain("years ago", fresh);

        // A record from before we kept ages claims nothing.
        var quiet = BirthText.FullAccount(SomeBirth(), includeHour: true, asMother: true, yearsSince: -1);
        Assert.DoesNotContain("was about", quiet);
        Assert.DoesNotContain("years ago", quiet);
    }

    [Fact]
    public void TheFathersBeat_CarriesTheFactAndNeverHerVoice()
    {
        // He is a parent and may call the hour back through the tool — but her private "I" is
        // never planted in his memory as though it were his own.
        var beat = BirthText.FatherBeat("Sibylla", "the town of Akkalat", "a son", "Ira", wasThere: false);
        Assert.True(BirthText.IsFatherBeat(beat));
        Assert.True(BirthText.IsBirthBeat(beat));
        Assert.Contains("Sibylla", beat);
        Assert.Contains("Ira", beat);
        Assert.Contains("Akkalat", beat);
        Assert.Contains("I was not there", beat);
        Assert.False(BirthText.TrySplitBeat(beat, out _, out _), "a father's mark carries no account");

        Assert.Contains("I was there for it.",
            BirthText.FatherBeat("Sibylla", "", "a son", "Ira", wasThere: true));
    }

    [Fact]
    public void Beats_CarryTheirMarks_AndTheAccountSplitsBackOut()
    {
        var hour = BirthText.MotherHourBeat("Mizam", "the town of Akkalat", "a son", "Ira", "И тогава го видях.");
        Assert.True(BirthText.IsHourBeat(hour));
        Assert.True(BirthText.TrySplitBeat(hour, out var frame, out var account));
        Assert.Contains("Mizam", frame);
        Assert.Equal("И тогава го видях.", account);

        var feast = BirthText.WitnessFeastBeat("Mizam", "Ira", "the town of Akkalat", "И се събраха.");
        Assert.True(BirthText.IsFeastBeat(feast));
        Assert.True(BirthText.TrySplitBeat(feast, out _, out var feastBody));
        Assert.Equal("И се събраха.", feastBody);

        // A child who did not live gets a mark written by hand and never by a model.
        var grief = BirthText.GriefBeat("Mizam", "the town of Akkalat", twinLived: false);
        Assert.True(BirthText.IsGriefBeat(grief));
        Assert.True(BirthText.IsBirthBeat(grief));
        Assert.False(BirthText.TrySplitBeat(grief, out _, out _));

        // Plain speech is never mistaken for any of them.
        Assert.False(BirthText.IsBirthBeat("We spoke of the road, and of the winter ahead."));
        Assert.False(BirthText.IsBirthBeat(null));
    }

    // ------------------------- what the chronicler is asked -------------------------

    private static BirthText.Facts SomeFacts() => new BirthText.Facts
    {
        MotherName = "Sibylla",
        MotherAge = 24,
        MotherStation = "a Sturgian wanderer who rides with him",
        FatherName = "Mizam",
        FatherWasThere = true,
        ChildWords = "a son",
        ChildNames = "Ira",
        OlderChildren = 0,
        DateText = "Winter 12, Year 1084",
        PlacePhrase = "the town of Akkalat",
        RecentWords = "Обичам те. — И аз теб.",
    };

    [Fact]
    public void TheHourPrompt_HoldsBothHalvesOfTheRule_AndBothHalvesOfTheShape()
    {
        var prompt = BirthText.BuildBirthPrompt(SomeFacts());

        // The register, and the two halves of its rule — the second is the one always lost first.
        Assert.Contains("FEAR NOT", prompt);
        Assert.Contains("NOTHING clinical", prompt);
        Assert.Contains("NOTHING coy", prompt);
        Assert.Contains("do not skip past the pain", prompt);

        // The shape Anton asked for at the nights, wearing its own clothes here.
        Assert.Contains("THE FIRST HALF", prompt);
        Assert.Contains("THE SECOND HALF", prompt);
        Assert.Contains("FIVE TO EIGHT sentences", prompt);
        Assert.Contains("not wording to lift", prompt);

        // Her own voice, their real names, and the name the game already gave the child.
        Assert.Contains("Sibylla", prompt);
        Assert.Contains("Mizam", prompt);
        Assert.Contains("already named Ira", prompt);
        Assert.Contains("invent no other", prompt);
        Assert.Contains("FIRST child", prompt);

        // Nothing may be prophesied over a cradle — the surest way to break a world.
        Assert.Contains("no prophecy over the cradle", prompt);

        // The tongue rule rides last, carrying their own words as its evidence.
        Assert.Contains("THE TONGUE", prompt);
        Assert.Contains("Обичам те", prompt);
        Assert.True(prompt.IndexOf("THE TONGUE", StringComparison.Ordinal)
                  > prompt.IndexOf("THE SECOND HALF", StringComparison.Ordinal));
    }

    [Fact]
    public void TheHourPrompt_WillNotFurnishARoomOntoAnOpenRoad_AndTellsTheHardTruths()
    {
        var road = SomeFacts();
        road.PlacePhrase = string.Empty;
        road.FatherWasThere = false;
        road.StillbornCount = 1;
        road.OlderChildren = 3;
        var prompt = BirthText.BuildBirthPrompt(road);

        Assert.Contains("no roof and no walls", prompt);
        Assert.Contains("there was no room, no bed, no hall", prompt);
        Assert.Contains("He was NOT there", prompt);
        Assert.Contains("did not live", prompt);
        Assert.Contains("3 living children already", prompt);
        Assert.DoesNotContain("FIRST child", prompt);
    }

    [Fact]
    public void TheFeastPrompt_NamesTheHall_AndKeepsTheHourOutOfIt()
    {
        var facts = SomeFacts();
        facts.Witnesses.Add("Yngvald, their scout");
        facts.ScaleNote = BirthTiers.ChroniclerNote(BirthScale.Named);
        var prompt = BirthText.BuildFeastPrompt(facts);

        Assert.Contains("six to twelve sentences", prompt);
        Assert.Contains("Third person", prompt);
        Assert.Contains("THE NAME SPOKEN ALOUD", prompt);
        Assert.Contains("Yngvald, their scout", prompt);
        Assert.Contains("NO ONE ELSE", prompt);            // the guest list reaches the chronicler

        // THE PRIVACY RULE, and it is the whole reason this assertion exists: this answer is copied
        // verbatim into every witness's memory, so the mother's own account of the hour must never
        // be in the room where it is written. The first cut handed it over with a prose request to
        // keep it secret, which is not a rule at all. There is no overload that takes it any more.
        var hour = "Часът дойде върху мен призори и не пуснах ръката му.";
        Assert.DoesNotContain(hour, prompt);
        Assert.DoesNotContain("no one at the feast knows it", prompt);

        // No cradle-side prophecy here either — the likeliest place a model would reach for one.
        Assert.Contains("NOTHING about what this child will one day become", prompt);
        Assert.True(prompt.IndexOf("THE TONGUE", StringComparison.Ordinal)
                  > prompt.IndexOf("Third person", StringComparison.Ordinal));
    }

    [Fact]
    public void EachPromptSeesOnlyItsOwnDay()
    {
        // THE TWO-WAY LEAK, and it cut both ways at once (2026.08.10 review). The facts are
        // gathered from the RECORD, and by the time the hour is retried days later that record has
        // accumulated a guest list and a price — so her private account of the labour was being
        // handed a naming feast to write into it. And the feast, whose answer is copied verbatim
        // into up to sixty witnesses' memories, was being handed her private self-text and her
        // whole deep memory of the marriage.
        var facts = SomeFacts();
        facts.MotherSelfText = "Обичам Мизам от мига, в който го зърнах.";
        facts.SharedStory = "Аз, Сибила, съм разузнавач в неговата дружина и негова жена.";
        facts.Witnesses.Add("Yngvald, their scout");
        facts.ScaleNote = BirthTiers.ChroniclerNote(BirthScale.Named);
        facts.FeastDelayPhrase = "the child was born 11 days before it";

        // The hour is hers: her own truths, and NOTHING of any feast.
        var hour = BirthText.BuildBirthPrompt(facts);
        Assert.Contains("Обичам Мизам", hour);
        Assert.Contains("разузнавач", hour);
        Assert.DoesNotContain("Yngvald", hour);
        Assert.DoesNotContain("Who stood at the feast", hour);
        Assert.DoesNotContain("The feast they paid for", hour);
        Assert.DoesNotContain("was NOT kept on the day of the birth", hour);

        // The feast is the hall's: the guests and the purse, and NOTHING private of hers.
        var feast = BirthText.BuildFeastPrompt(facts);
        Assert.Contains("Yngvald", feast);
        Assert.Contains("The feast they paid for", feast);
        Assert.Contains("was NOT kept on the day of the birth", feast);
        Assert.DoesNotContain("Обичам Мизам", feast);
        Assert.DoesNotContain("разузнавач", feast);
        Assert.DoesNotContain("What she holds true of herself", feast);
        Assert.DoesNotContain("The story these two share", feast);
    }

    [Fact]
    public void TheFeastsOwnAskFitsInsideTheTamersCut()
    {
        // The prompt asks for up to twelve sentences; the tamer must not sit below that, or the
        // grandest feast is silently trimmed — the nights' own lesson, inherited before it bit.
        var twelve = string.Join(" ", Enumerable.Repeat(
            "И се събраха в залата на Акалат, и хлябът беше разчупен над люлката, и името на детето "
          + "беше изречено високо, та всички го чуха и го повториха един на друг.", 12));
        Assert.True(twelve.Length > 1600);
        Assert.Equal(twelve, BirthText.CleanAccount(twelve));
    }

    [Fact]
    public void TheFeastPrompt_DoesNotPutAnAbsentFatherAtTheBedside()
    {
        var away = SomeFacts();
        away.FatherWasThere = false;
        away.StillbornCount = 1;
        var prompt = BirthText.BuildFeastPrompt(away);

        Assert.Contains("do not pretend he saw the birth", prompt);
        Assert.Contains("the absence is in the room with them", prompt);

        var there = BirthText.BuildFeastPrompt(SomeFacts());
        Assert.DoesNotContain("do not pretend he saw the birth", there);
        Assert.DoesNotContain("the absence is in the room", there);
    }

    // ------------------------- the book itself -------------------------

    [Fact]
    public void Ledger_KeepsEachDay_AndFindsItTheLooseWayASoulNamesIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "iai_births_" + Guid.NewGuid().ToString("N"));
        try
        {
            var ledger = new BirthLedger(folder);
            var first = SomeBirth();
            first.Id = ledger.NextId(1000);
            first.GameDay = 1000;
            first.Children.Clear();
            first.Children.Add(new BirthChild { HeroId = "c1", Name = "Ira" });
            first.PlaceName = "the town of Akkalat";
            ledger.Save(first);

            var second = SomeBirth();
            second.Id = ledger.NextId(1200);
            second.GameDay = 1200;
            second.Children.Clear();
            second.Children.Add(new BirthChild { HeroId = "c2", Name = "Tulag", IsFemale = true });
            second.PlaceName = "the village of Odrimir";
            ledger.Save(second);

            var read = BirthLedger.LoadFrom(folder);
            Assert.Equal(2, read.Records.Count);
            Assert.Equal("Ira", read.Records[0].ChildNames());

            // By the child's own name, by the place, and by the words a soul actually uses.
            Assert.Equal("Tulag", read.Find("Tulag", MotherId)!.ChildNames());
            Assert.Equal("Ira", read.Find("that day in Akkalat", MotherId)!.ChildNames());
            Assert.Equal("Ira", read.Find("our first", MotherId)!.ChildNames());
            Assert.Equal("Tulag", read.Find("the youngest", MotherId)!.ChildNames());

            // "when our child was born" names nothing the record carries — it falls to the newest,
            // which is what a parent means nine times in ten.
            Assert.Equal("Tulag", read.Find("when our child was born", MotherId)!.ChildNames());

            // Scoped to what the asker truly lived: a stranger shares none of it.
            Assert.Empty(read.SharedWith(StrangerId));
            Assert.Null(read.Find("Ira", StrangerId));
            Assert.Equal(2, read.ChildrenOf(MotherId).Count);
            Assert.Equal("Ira", read.ForChild("c1")!.ChildNames());
            Assert.Null(read.ForChild("nobody"));

            // A rewrite replaces the day rather than adding a second one.
            second.FeastAccount = "И пак се събраха.";
            read.Save(second);
            Assert.Equal(2, BirthLedger.LoadFrom(folder).Records.Count);

            read.AppendToChronicle(BirthText.ChronicleEntry(second));
            var chronicle = File.ReadAllText(Path.Combine(folder, BirthLedger.ChronicleFileName));
            Assert.Contains("Tulag", chronicle);
            Assert.Contains("The feast", chronicle);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void WhatStillOwesUsSomething_IsJudgedByContentAndNeverByExistence()
    {
        // The wedding chronicle's most expensive lesson, inherited on purpose: a record saved at
        // the hour with no account YET is precisely what the retry exists for. Guarding on the
        // record existing instead would make the whole retry dead code.
        var ledger = new BirthLedger(string.Empty);

        var fresh = SomeBirth();
        fresh.Id = "d1";
        fresh.BirthAccount = string.Empty;
        fresh.FeastAccount = string.Empty;
        ledger.Save(fresh);

        Assert.Single(ledger.AwaitingHour());
        Assert.Empty(ledger.AwaitingFeast());          // no feast was ever bought — nothing is owed

        fresh.Scale = BirthScale.Named;
        Assert.Single(ledger.AwaitingFeast());

        fresh.BirthAccount = "Часът дойде.";
        fresh.FeastAccount = "И се събраха.";
        Assert.Empty(ledger.AwaitingHour());
        Assert.Empty(ledger.AwaitingFeast());

        // And a dying key is not hammered forever.
        var stubborn = SomeBirth();
        stubborn.Id = "d2";
        stubborn.BirthAccount = string.Empty;
        stubborn.BirthAttempts = BirthLedger.MaxAttempts;
        ledger.Save(stubborn);
        Assert.Empty(ledger.AwaitingHour());

        // A birth where nothing lived is never asked of the chronicler at all.
        var lost = SomeBirth();
        lost.Id = "d3";
        lost.BirthAccount = string.Empty;
        lost.Children.Clear();
        lost.StillbornCount = 1;
        ledger.Save(lost);
        Assert.Empty(ledger.AwaitingHour());
    }

    [Fact]
    public void AFatherWhoWasAway_IsAskedAboutTheFeastLater_ButNotForever()
    {
        var ledger = new BirthLedger(string.Empty);
        var record = SomeBirth();
        record.Id = "d1";
        record.GameDay = 1000;
        ledger.Save(record);

        // He rides in a week later and is asked.
        Assert.Single(ledger.AwaitingFeastOffer(today: 1007));

        // He says no, or keeps a feast: either way he is not asked again every hour he stands
        // beside her.
        record.FeastOffered = true;
        Assert.Empty(ledger.AwaitingFeastOffer(today: 1007));

        record.FeastOffered = false;
        record.Scale = BirthScale.Quiet;
        Assert.Empty(ledger.AwaitingFeastOffer(today: 1007));

        // And a cradle-side feast half a year late is a stranger's idea of a christening.
        record.Scale = BirthScale.Unfeasted;
        Assert.Empty(ledger.AwaitingFeastOffer(today: 1000 + BirthLedger.FeastOfferDays + 1));

        // Nothing is ever offered for a child that did not live.
        record.Children.Clear();
        Assert.Empty(ledger.AwaitingFeastOffer(today: 1007));
    }

    [Fact]
    public void WhetherTheFatherWasThere_IsHisOwnQuestion_NotThePlayers()
    {
        // With a female player the two come apart completely: she is certainly present at her own
        // labour, while her husband may be three weeks' ride away. Reading the player's presence
        // as the father's put a man at a bedside he never reached (2026.08.10 review).
        var record = SomeBirth();
        record.MotherIsPlayer = true;
        record.FatherIsPlayer = false;
        record.PlayerWasThere = true;
        record.FatherWasThere = false;

        Assert.Contains("(The father was away.)", BirthText.ChronicleEntry(record));

        record.FatherWasThere = true;
        Assert.DoesNotContain("The father was away", BirthText.ChronicleEntry(record));
    }

    [Fact]
    public void CleanAccount_TamesTheAnswer_AndARefusalIsNotAnAccount()
    {
        Assert.Equal("И тя го нарече Ира, и заспа.",
            BirthText.CleanAccount("```\n# The Hour\nИ тя го нарече Ира, и заспа.\n```"));

        Assert.False(BirthText.LooksLikeAnAccount("I'm sorry, I can't help with that."));
        Assert.False(BirthText.LooksLikeAnAccount("   "));
        Assert.False(BirthText.LooksLikeAnAccount(null));
        Assert.True(BirthText.LooksLikeAnAccount(new string('щ', 130)));
    }
}
