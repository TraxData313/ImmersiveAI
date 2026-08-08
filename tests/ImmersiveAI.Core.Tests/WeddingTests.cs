using ImmersiveAI.Core.Weddings;

namespace ImmersiveAI.Core.Tests;

public class WeddingTests
{
    private static WeddingRecord Record(
        string id = "d0123",
        string spouseId = "npc_1",
        string spouseName = "Sibylla",
        string playerName = "Eren",
        string place = "the town of Onira",
        string date = "Autumn 14, Year 1084",
        double gameDay = 91123,
        params string[] witnessIds)
    {
        var record = new WeddingRecord
        {
            Id = id,
            GameDay = gameDay,
            DateText = date,
            PlaceName = place,
            SpouseId = spouseId,
            SpouseName = spouseName,
            PlayerName = playerName,
            FeastAccount = "And in the hall of Onira they were joined, and there was bread upon the board.",
            NightAccount = "I laid my hand upon his breast, and the lamp burned low until the morning.",
        };
        foreach (var w in witnessIds)
            record.Witnesses.Add(new WeddingWitness { HeroId = w, Name = w.ToUpperInvariant() });
        return record;
    }

    // ------------------------- who lived the day, and who may remember the night -------------------------

    [Fact]
    public void TheRecordKnowsWhoWasThere_AndWhoIsTheSpouse()
    {
        var record = Record(witnessIds: new[] { "npc_2", "npc_3" });

        Assert.True(record.WasThere("npc_1"));   // the spouse
        Assert.True(record.WasThere("npc_2"));   // a witness
        Assert.False(record.WasThere("npc_9"));  // someone elsewhere that day
        Assert.False(record.WasThere(null));

        Assert.True(record.IsSpouse("npc_1"));
        Assert.False(record.IsSpouse("npc_2"));  // a witness is never the spouse
    }

    [Fact]
    public void TheNightIsTheirsAlone_ARecallByAWitnessNeverCarriesIt()
    {
        var record = Record(witnessIds: new[] { "npc_2" });

        var forSpouse = WeddingText.FullAccount(record, includeNight: true);
        Assert.Contains("the lamp burned low", forSpouse);

        var forWitness = WeddingText.FullAccount(record, includeNight: false);
        Assert.Contains("bread upon the board", forWitness);          // the day is everyone's
        Assert.DoesNotContain("the lamp burned low", forWitness);     // the night is not
        Assert.Contains("belongs to the two of them alone", forWitness);
    }

    // ------------------------- the marks memory keeps forever -------------------------

    [Fact]
    public void BeatsCarryTheirMarks_AndSplitBackIntoFrameAndAccount()
    {
        var day = WeddingText.SpouseDayBeat("Eren", "the town of Onira", "And they were joined in the hall.");
        Assert.StartsWith("This day I was wed to Eren, in the town of Onira.", day);
        Assert.True(WeddingText.IsDayBeat(day));
        Assert.False(WeddingText.IsNightBeat(day));

        Assert.True(WeddingText.TrySplitBeat(day, out var frame, out var account));
        Assert.Equal("This day I was wed to Eren, in the town of Onira.", frame);
        Assert.Equal("And they were joined in the hall.", account);

        var witness = WeddingText.WitnessDayBeat("Eren", "Sibylla", "the town of Onira", "And they were joined in the hall.");
        Assert.Contains("I stood among the witnesses at the wedding of Eren and Sibylla", witness);
        Assert.True(WeddingText.IsDayBeat(witness));

        var night = WeddingText.NightBeat("I laid my hand upon his breast.");
        Assert.True(WeddingText.IsNightBeat(night));
        Assert.True(WeddingText.TrySplitBeat(night, out _, out var nightBody));
        Assert.Equal("I laid my hand upon his breast.", nightBody);

        // An ordinary remembered line is neither.
        Assert.False(WeddingText.IsDayBeat("We spoke of the harvest."));
        Assert.False(WeddingText.TrySplitBeat("We spoke of the harvest.", out _, out _));
    }

    [Fact]
    public void ABeatWithoutAPlace_ReadsWholeAnyway()
    {
        var day = WeddingText.SpouseDayBeat("Eren", string.Empty, "And they were joined upon the road.");
        Assert.StartsWith("This day I was wed to Eren.", day);
        Assert.True(WeddingText.IsDayBeat(day));
    }

    // ------------------------- the ledger -------------------------

    [Fact]
    public void Ledger_SavesLoadsAndFinds_TheirOwnWeddingWinningByDefault()
    {
        var dir = Path.Combine(Path.GetTempPath(), "immersiveai-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var ledger = new WeddingLedger(dir);
            var first = Record(id: "d0100", spouseId: "npc_1", spouseName: "Sibylla",
                place: "the town of Onira", gameDay: 91100, witnessIds: new[] { "npc_2" });
            var second = Record(id: "d0200", spouseId: "npc_2", spouseName: "Ira",
                place: "the castle of Varcheg", gameDay: 91200, witnessIds: new[] { "npc_1" });
            ledger.Save(first);
            ledger.Save(second);

            var reloaded = WeddingLedger.LoadFrom(dir);
            Assert.Equal(2, reloaded.Records.Count);
            Assert.Equal("d0100", reloaded.Records[0].Id);   // oldest first

            // Both souls stood at both days, but each has their OWN wedding.
            Assert.Equal(2, reloaded.SharedWith("npc_1").Count);
            Assert.Equal("d0100", reloaded.OwnWeddingOf("npc_1")!.Id);
            Assert.Equal("d0200", reloaded.OwnWeddingOf("npc_2")!.Id);
            Assert.Null(reloaded.OwnWeddingOf("npc_9"));

            // "our wedding day" names nothing the record carries — it must still land on theirs.
            Assert.Equal("d0100", reloaded.Find("our wedding day", "npc_1")!.Id);
            Assert.Equal("d0200", reloaded.Find("our wedding day", "npc_2")!.Id);
            // A named place or person wins over the default.
            Assert.Equal("d0200", reloaded.Find("that day in Varcheg", "npc_1")!.Id);
            Assert.Equal("d0100", reloaded.Find("when you wed Sibylla", "npc_2")!.Id);
            // The roll words still work.
            Assert.Equal("d0200", reloaded.Find("the last one", "npc_1")!.Id);
            Assert.Equal("d0100", reloaded.Find("the first", "npc_1")!.Id);
            // Someone who stood at neither remembers nothing.
            Assert.Null(reloaded.Find("our wedding", "npc_9"));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Ledger_RewritesTheSameFile_WhenTheAccountsArriveAfterTheSeal()
    {
        var dir = Path.Combine(Path.GetTempPath(), "immersiveai-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var ledger = new WeddingLedger(dir);
            var record = Record(id: "d0100");
            record.FeastAccount = string.Empty;
            record.NightAccount = string.Empty;
            ledger.Save(record);                        // sealed, not yet written
            Assert.Single(Directory.GetFiles(dir, "*.json"));

            record.FeastAccount = "And they were joined in the hall of Onira.";
            record.NightAccount = "I remember the lamp.";
            ledger.Save(record);                        // the chronicler's answer lands
            Assert.Single(Directory.GetFiles(dir, "*.json"));   // same file, not a second one

            var reloaded = WeddingLedger.LoadFrom(dir);
            Assert.Single(reloaded.Records);
            Assert.Contains("joined in the hall", reloaded.Records[0].FeastAccount);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Ledger_MissingFolderIsAnEmptyBook_AndIdsAreDayScoped()
    {
        var missing = WeddingLedger.LoadFrom(
            Path.Combine(Path.GetTempPath(), "immersiveai-tests", Path.GetRandomFileName()));
        Assert.Empty(missing.Records);
        Assert.Null(missing.OwnWeddingOf("npc_1"));
        Assert.Null(missing.Find("our wedding", "npc_1"));

        // Ids are minted per campaign day. A folderless ledger keeps its records in hand and
        // writes nothing, so the arithmetic is testable without touching the disk at all.
        var ledger = new WeddingLedger(string.Empty);
        Assert.Equal("d91123", ledger.NextId(91123.7));
        ledger.Save(Record(id: ledger.NextId(91123.7), gameDay: 91123.7));
        Assert.Equal("d91123-2", ledger.NextId(91123.9));
        Assert.Equal("d91124", ledger.NextId(91124.1));
    }

    // ------------------------- the chronicler's prompts -------------------------

    private static WeddingText.Facts Facts() => new()
    {
        SpouseName = "Sibylla",
        SpouseGenderWord = "woman",
        SpouseAge = 21,
        SpouseStation = "a Sturgian wanderer riding as their companion",
        SpouseTraits = "honest, daring",
        SpouseSelfText = "I keep my father's knife under my pillow.",
        PlayerName = "Eren",
        PlayerGenderWord = "man",
        PlayerStanding = "the head of a rising house",
        DateText = "Autumn 14, Year 1084",
        PlacePhrase = "the town of Onira",
        CultureName = "Sturgian",
        SeasonPhrase = "in the cold of late autumn",
        Witnesses = { "Yngvald, who rides as their scout", "Ira, of their own clan" },
        SharedStory = "They met on the road to Varcheg and never truly parted.",
        RoadPhrase = "her heart turned some forty days ago; they were betrothed eleven days before this",
        BlessingPhrase = "given by Lucon, the head of her house, for one thousand five hundred denars",
        MisgivingsAnswered = { "I feared the road would own him more than I would" },
        RecentWords = "Ерен: Ще се оженим ли?\nСибила: Да. Да, ще се оженим.",
        WorldText = "A world where the Word is read aloud in the halls.",
    };

    [Fact]
    public void FeastPrompt_CarriesTheDay_TheHall_AndTheScripturalRegister()
    {
        var prompt = WeddingText.BuildFeastPrompt(Facts());

        Assert.Contains("chronicler of a living medieval world", prompt);
        Assert.Contains("manner of the old Scriptures", prompt);
        Assert.Contains("Sibylla", prompt);
        Assert.Contains("Eren", prompt);
        Assert.Contains("the town of Onira", prompt);
        Assert.Contains("Autumn 14, Year 1084", prompt);
        Assert.Contains("Yngvald, who rides as their scout", prompt);
        Assert.Contains("Lucon", prompt);
        Assert.Contains("I feared the road would own him more than I would", prompt);
        Assert.Contains("Third person", prompt);
        Assert.Contains("eight to fourteen sentences", prompt);
        // The rails that keep the day from turning into fantasy or a modern scene.
        Assert.Contains("no falling stars", prompt);
        Assert.Contains("Nothing from outside their world", prompt);
        Assert.Contains("no title, no heading", prompt);
    }

    [Fact]
    public void NightPrompt_IsHerOwnVoice_AndHoldsBothHalvesOfTheSongsRule()
    {
        var prompt = WeddingText.BuildNightPrompt(Facts(), "And they were joined in the hall of Onira.");

        // Her voice, not a narrator's.
        Assert.Contains("IN HER OWN VOICE", prompt);
        Assert.Contains("first person", prompt);
        // The register, named outright.
        Assert.Contains("Song of Songs", prompt);
        Assert.Contains("he knew her", prompt);
        // BOTH halves of the rule — neither coarse nor coy. This is the whole ask; guard it.
        Assert.Contains("NOTHING coarse", prompt);
        Assert.Contains("NOTHING coy", prompt);
        Assert.Contains("without turning away", prompt);
        // The day before it, so the two parts cohere.
        Assert.Contains("And they were joined in the hall of Onira.", prompt);
        Assert.Contains("six to twelve sentences", prompt);
    }

    [Fact]
    public void BothPrompts_DemandTheCouplesOwnTongue_AndCarryTheirWordsAsProof()
    {
        var facts = Facts();
        foreach (var prompt in new[] { WeddingText.BuildFeastPrompt(facts), WeddingText.BuildNightPrompt(facts, "…") })
        {
            Assert.Contains("THE TONGUE", prompt);
            Assert.Contains("SAME TONGUE", prompt);
            Assert.Contains("Сибила: Да. Да, ще се оженим.", prompt);
        }

        // With no words to go by, the rule still stands and names a fallback.
        var mute = Facts();
        mute.RecentWords = string.Empty;
        var quiet = WeddingText.BuildFeastPrompt(mute);
        Assert.Contains("write in English", quiet);
    }

    [Fact]
    public void Prompts_SurviveAWeddingWithNoHallAndNoWitnesses()
    {
        var facts = Facts();
        facts.PlacePhrase = string.Empty;
        facts.Witnesses.Clear();
        facts.BlessingPhrase = string.Empty;
        facts.MisgivingsAnswered.Clear();

        var prompt = WeddingText.BuildFeastPrompt(facts);
        Assert.Contains("they were wed on the road", prompt);
        Assert.Contains("no company to witness it", prompt);
    }

    [Fact]
    public void NightPrompt_SpeaksTheRightPronouns_ForAHusbandToo()
    {
        // A female player character weds a husband: HE is the one remembering, and the beloved
        // whose hands the account dwells on is HER. Both pronoun sets must turn over together —
        // a hardcoded "his hands and her own" told a husband to remember his own hands as hers.
        var facts = Facts();
        facts.SpouseName = "Vulgrim";
        facts.SpouseGenderWord = "man";
        facts.PlayerName = "Erenwyn";
        facts.PlayerGenderWord = "woman";

        var prompt = WeddingText.BuildNightPrompt(facts, string.Empty);
        Assert.Contains("IN HIS OWN VOICE", prompt);
        Assert.DoesNotContain("IN HER OWN VOICE", prompt);
        Assert.Contains("name Erenwyn by name", prompt);       // the beloved, named — never a bare "him"
        Assert.Contains("her hands and his own", prompt);      // hers are the beloved's, his are his
        Assert.Contains("Let him carry into it", prompt);      // object pronoun, not "Let he"
        Assert.DoesNotContain("his hands and her own", prompt);

        // And the shipped default (a wife remembering her husband) reads the other way round.
        var wife = WeddingText.BuildNightPrompt(Facts(), string.Empty);
        Assert.Contains("IN HER OWN VOICE", wife);
        Assert.Contains("name Eren by name", wife);
        Assert.Contains("his hands and her own", wife);
        Assert.Contains("Let her carry into it", wife);
        Assert.DoesNotContain("Let she ", wife);

        // The register never assumes the pair's shape — a polygamy mod may wed any two souls.
        Assert.Contains("a wedded pair", wife);
        Assert.DoesNotContain("a wedded man and woman", wife);
    }

    // ------------------------- taming the chronicler's answer -------------------------

    [Fact]
    public void CleanAccount_ShedsFencesHeadingsLabelsAndWrappingQuotes()
    {
        Assert.Equal("And they were joined in the hall.",
            WeddingText.CleanAccount("```\nAnd they were joined in the hall.\n```"));
        Assert.Equal("And they were joined in the hall.",
            WeddingText.CleanAccount("## The Wedding Day\nAnd they were joined in the hall."));
        Assert.Equal("And they were joined in the hall.",
            WeddingText.CleanAccount("THE DAY: And they were joined in the hall."));
        Assert.Equal("And they were joined in the hall.",
            WeddingText.CleanAccount("“And they were joined in the hall.”"));
        Assert.Equal(string.Empty, WeddingText.CleanAccount("   "));
        Assert.Equal(string.Empty, WeddingText.CleanAccount(null));
    }

    [Fact]
    public void CleanAccount_KeepsQuotedSpeechInsideTheAccount()
    {
        // Two quotes inside are dialogue, not a wrapper — the account must survive whole.
        const string spoken = "“I am yours,” she said, and he answered, “And I am yours.”";
        Assert.Equal(spoken, WeddingText.CleanAccount(spoken));
    }

    [Fact]
    public void CleanAccount_CutsARunawayAnswerAtASentenceEnd()
    {
        var long1 = string.Concat(Enumerable.Repeat("And they went up to the hall. ", 400));
        var cleaned = WeddingText.CleanAccount(long1, maxChars: 200);
        Assert.True(cleaned.Length <= 200);
        Assert.EndsWith(".", cleaned);
    }

    [Fact]
    public void LooksLikeAnAccount_RefusesARefusalOrAShrug()
    {
        Assert.False(WeddingText.LooksLikeAnAccount(null));
        Assert.False(WeddingText.LooksLikeAnAccount("I cannot help with that."));
        Assert.False(WeddingText.LooksLikeAnAccount("They were wed."));
        Assert.True(WeddingText.LooksLikeAnAccount(
            "And in the hall of Onira they were joined, and there was bread upon the board, "
            + "and the men of the town came in out of the cold, and Yngvald poured the wine, "
            + "and she took his hand before them all."));
    }

    // ------------------------- what is written down for the player -------------------------

    [Fact]
    public void ChronicleEntry_KeepsBothPartsUnderTheirOwnHeads()
    {
        var record = Record(witnessIds: new[] { "npc_2" });
        record.BlessedBy = "Lucon";
        record.BridePrice = 1500;

        var entry = WeddingText.ChronicleEntry(record);
        Assert.Contains("the wedding of Eren and Sibylla, in the town of Onira", entry);
        Assert.Contains("Witnesses: NPC_2.", entry);
        Assert.Contains("Blessed by Lucon, for 1500 denars.", entry);
        Assert.Contains("-- The day --", entry);
        Assert.Contains("-- The night (theirs alone) --", entry);
        Assert.Contains("bread upon the board", entry);
        Assert.Contains("the lamp burned low", entry);
    }

    [Fact]
    public void RollEntry_NamesTheDayInOneLine()
    {
        Assert.Equal("Eren and Sibylla, in the town of Onira (Autumn 14, Year 1084)",
            WeddingText.RollEntry(Record()));
    }
}
