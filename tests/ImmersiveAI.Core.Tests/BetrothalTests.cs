using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The proposal made an event (2026.08.31): the gift ladder, the chronicler's prompt and its
/// one law above all others — her answer was yes, and the telling never hedges it — the frozen
/// marks, the ledger, and the fade that thins the great day with distance.
/// </summary>
public class BetrothalTests
{
    private static BetrothalRecord Record(bool askedByPlayer = true, string account = "") => new()
    {
        Id = "d0100",
        GameDay = 100,
        DateText = "Spring 12, Year 1084",
        PlaceName = "the town of Onira",
        SpouseId = "hero_rhia",
        SpouseName = "Rhia",
        SpouseIsFemale = true,
        SpouseAge = 24,
        PlayerName = "Mizam",
        PlayerAge = 29,
        AskedByPlayer = askedByPlayer,
        Gift = BetrothalGift.SilverRing,
        GiftCost = 1000,
        GiftName = "a silver ring set with a small jewel",
        PlayerWish = "under the walnut tree where we first spoke",
        HerWord = askedByPlayer ? "" : "my heart has long been yours",
        Account = account,
    };

    // ------------------------- the gift ladder -------------------------

    [Fact]
    public void Gifts_ClimbInPriceAndInTelling_AndStayUnderTheWeddingNight()
    {
        int lastPrice = -1, lastMax = 0;
        foreach (var tier in BetrothalGifts.All)
        {
            Assert.True(tier.Price > lastPrice);
            Assert.True(tier.MinSentences <= tier.MaxSentences);
            Assert.True(tier.MaxSentences >= lastMax);
            // The proposal's ceiling stays under the wedding night's twelve — the greater day
            // keeps the greater telling.
            Assert.True(tier.MaxSentences <= 10);
            Assert.False(string.IsNullOrWhiteSpace(tier.Name));
            Assert.False(string.IsNullOrWhiteSpace(tier.ChroniclerNote));
            // Rich Cyrillic sentences run past 250 characters — the budget must hold the ceiling.
            Assert.True(tier.AccountCharBudget >= tier.MaxSentences * 250);
            lastPrice = tier.Price;
            lastMax = tier.MaxSentences;
        }

        // Words alone are a true asking: free, nameless, and still written down.
        var words = BetrothalGifts.Of(BetrothalGift.WordsAlone);
        Assert.Equal(0, words.Price);
        Assert.Equal(string.Empty, words.GiftName);
        // Every paid tier names the thing the coin bought.
        foreach (var tier in BetrothalGifts.All)
            if (tier.Price > 0)
                Assert.False(string.IsNullOrWhiteSpace(tier.GiftName));
    }

    // ------------------------- the prompt's laws -------------------------

    private static BetrothalText.Facts PromptFacts(bool askedByPlayer = true) => new()
    {
        SpouseName = "Rhia",
        SpouseGenderWord = "woman",
        SpouseAge = 24,
        PlayerName = "Mizam",
        PlayerGenderWord = "man",
        PlayerAge = 29,
        DateText = "Spring 12, Year 1084",
        PlacePhrase = "the town of Onira",
        AskedByPlayer = askedByPlayer,
        GiftNote = "A silver ring with a small jewel in it was set on the hand.",
        PlayerWish = "under the walnut tree where we first spoke",
        HerWord = askedByPlayer ? "" : "my heart has long been yours",
        MisgivingsAnswered = new List<string> { "I feared he would one day want a noble wife." },
        RecentWords = "Mizam: Ще бъдеш ли моя?\nRhia: С цялото си сърце.",
        MinSentences = 7,
        MaxSentences = 8,
    };

    [Fact]
    public void Prompt_CarriesTheYesLaw_TheWish_TheGift_AndTheTongue()
    {
        var prompt = BetrothalText.BuildPrompt(PromptFacts());

        // THE LAW: her yes is a fact of the day, never a question — a hedge or a "not yet" is
        // forbidden in as many words, because a model left free would soften it.
        Assert.Contains("THE ANSWER WAS YES", prompt);
        Assert.Contains("A caught breath", prompt);

        // His wish shapes the asking as intent, never as wording, and never writes her feelings.
        Assert.Contains("under the walnut tree", prompt);
        Assert.Contains("Never lift its wording", prompt);

        // The gift and the doubts stand among the truths.
        Assert.Contains("A silver ring with a small jewel", prompt);
        Assert.Contains("laid to rest", prompt);

        // The length the gift bought, in words as a chronicle counts.
        Assert.Contains("seven to eight sentences", prompt);

        // The tongue rides last on the couple's own words — a promise remembered in a language
        // they never spoke would be no memory of theirs.
        Assert.Contains("SAME TONGUE", prompt);
        Assert.Contains("Ще бъдеш ли моя?", prompt);
        Assert.True(prompt.IndexOf("SAME TONGUE", StringComparison.Ordinal)
                  > prompt.IndexOf("THE ANSWER WAS YES", StringComparison.Ordinal));
    }

    [Fact]
    public void Prompt_TellsHerOwnAsking_WhenSheLaidThePromise()
    {
        var prompt = BetrothalText.BuildPrompt(PromptFacts(askedByPlayer: false));
        Assert.Contains("WHO ASKED", prompt);
        Assert.Contains("my heart has long been yours", prompt);
        Assert.DoesNotContain("THE ANSWER WAS YES", prompt);
    }

    // ------------------------- the marks memory keeps forever -------------------------

    [Fact]
    public void AccountMark_IsFrozen_AndTheBeatSplitsCleanly()
    {
        // The mark is a recorded phrasing: memories keep the words they were born with, so this
        // exact string may never be reworded — add a new mark beside it if a new shape is needed.
        Assert.Equal("The day we were promised, as it is told:", BetrothalText.AccountMark);

        var beat = BetrothalText.SpouseBeat("Mizam", "the town of Onira", "And Mizam asked, and she said yes.");
        Assert.True(BetrothalText.IsAccountBeat(beat));
        Assert.True(BetrothalText.TrySplitBeat(beat, out var frame, out var account));
        Assert.Contains("Mizam and I were promised", frame);
        Assert.Contains("in the town of Onira", frame);
        Assert.Equal("And Mizam asked, and she said yes.", account);

        Assert.False(BetrothalText.TrySplitBeat("an ordinary inner thought", out _, out _));
    }

    [Fact]
    public void TheGreatDayThinsWithDistance_LikeTheWeddings()
    {
        var beat = BetrothalText.SpouseBeat("Mizam", "the town of Onira",
            "And Mizam asked for her hand. And she said yes, with a whole heart. "
            + "And the ring was set upon her finger. And they were promised before the evening came.");
        Assert.True(BeatFade.IsGreatAccount(beat));

        // Fresh: whole. Middle distance: the opening survives, the tail settles. Far: the day alone.
        Assert.Equal(beat, BeatFade.Fade(beat, turnsBack: 3));

        var faded = BeatFade.Fade(beat, turnsBack: 10);
        Assert.Contains(BetrothalText.AccountMark, faded);
        Assert.Contains("asked for her hand", faded);
        Assert.DoesNotContain("before the evening came", faded);

        var far = BeatFade.Fade(beat, turnsBack: 20);
        Assert.Contains("Mizam and I were promised", far);
        Assert.DoesNotContain("asked for her hand", far);
    }

    // ------------------------- what a soul reads back -------------------------

    [Fact]
    public void FullAccount_TellsWhoAsked_AndNeverPrintsTheSteeringLine()
    {
        var asked = Record(askedByPlayer: true, account: "And Mizam asked, and she said yes.");
        var page = BetrothalText.FullAccount(asked, yearsSince: 2);
        Assert.Contains("asked for Rhia's hand", page);
        Assert.Contains("a silver ring set with a small jewel", page);
        Assert.Contains("about 29", page);              // the ages on the day — the 2026.08.15 law
        Assert.Contains("2 years ago", page);
        Assert.Contains("And Mizam asked, and she said yes.", page);

        // The player's steering line did its work upstream and is never printed under the finished
        // day — a stage direction beneath a scene you have just watched (2026.08.31).
        Assert.DoesNotContain("walnut tree", page);
        Assert.DoesNotContain("What you had in mind", page);
        Assert.DoesNotContain("walnut tree", BetrothalText.ChronicleEntry(asked));

        var offered = BetrothalText.FullAccount(Record(askedByPlayer: false));
        Assert.Contains("laid their promise before", offered);
    }

    // ------------------------- the ledger -------------------------

    [Fact]
    public void Ledger_RoundTrips_AndFindsOnlyTheirOwn()
    {
        var folder = Path.Combine(Path.GetTempPath(), "iai-betrothals-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ledger = new BetrothalLedger(folder);
            var record = Record(account: "And they were promised.");
            ledger.Save(record);

            var loaded = BetrothalLedger.LoadFrom(folder);
            var own = loaded.OwnBetrothalOf("hero_rhia");
            Assert.NotNull(own);
            Assert.Equal("And they were promised.", own!.Account);
            Assert.Equal(BetrothalGift.SilverRing, own.Gift);
            Assert.Equal("under the walnut tree where we first spoke", own.PlayerWish);
            Assert.True(own.AskedByPlayer);

            // Only the two of the record — a stranger's id finds nothing.
            Assert.Null(loaded.OwnBetrothalOf("hero_someone_else"));

            // A second on the same day takes its own id.
            Assert.Equal("d0100-2", loaded.NextId(100));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { }
        }
    }
}
