using ImmersiveAI.Core.Handmaids;

namespace ImmersiveAI.Core.Tests;

public class HandmaidQuestTests
{
    [Theory]
    [InlineData("sea_raiders", "Sea Raiders", "Clear the Sea Raiders' den")]
    [InlineData("desert_bandits", "Desert Bandits", "Clear the Desert Bandits' den")]
    [InlineData("forest_bandits", "Forest Bandits", "Clear the Forest Bandits' den")]
    [InlineData("mountain_bandits", "Mountain Bandits", "Clear the Mountain Bandits' den")]
    [InlineData("steppe_bandits", "Steppe Bandits", "Clear the Steppe Bandits' den")]
    public void TitlesReadRight(string culture, string name, string title)
    {
        Assert.Equal(name, HandmaidQuestText.DenName(culture));
        Assert.Equal(name, HandmaidQuestText.DenName("Culture." + culture));
        Assert.Equal(title, HandmaidQuestText.Title(HandmaidQuestText.DenName(culture)));
    }

    [Fact]
    public void TheGameNameWinsAndAnUnknownDenStillReads()
    {
        Assert.Equal("Sea Wolves", HandmaidQuestText.DenName("sea_raiders", "  Sea Wolves "));
        Assert.Equal("Bandits", HandmaidQuestText.DenName("southern_pirates"));
        Assert.Equal("Clear the Bandit Kin's den", HandmaidQuestText.Title("Bandit Kin"));
        Assert.Equal("Clear the bandits' den", HandmaidQuestText.Title(null));
    }

    [Fact]
    public void EveryDenTypeHasItsOwnStory()
    {
        var cultures = HandmaidQuestText.DenCultures;
        Assert.Equal(5, cultures.Count);
        Assert.All(cultures, c =>
        {
            Assert.True(HandmaidQuestText.HasStory(c));
            Assert.False(string.IsNullOrWhiteSpace(HandmaidQuestText.StoryFor(c)));
            // Every den type of the quest leads to a girl of its own.
            Assert.Equal(c, HandmaidRoster.OwnOf(c)!.BanditCulture);
        });
        Assert.Equal(5, cultures.Select(HandmaidQuestText.StoryFor).Distinct().Count());
        Assert.False(HandmaidQuestText.HasStory("looters"));
        Assert.False(string.IsNullOrWhiteSpace(HandmaidQuestText.StoryFor("looters")));
    }

    [Fact]
    public void TheStoriesNeverNameTheGirl()
    {
        foreach (var girl in HandmaidRoster.All)
            foreach (var c in HandmaidQuestText.DenCultures)
                Assert.DoesNotContain(girl.Name, HandmaidQuestText.StoryFor(c));
    }

    [Fact]
    public void TheBriefsLinesAreWordForWord()
    {
        Assert.Equal("I hear you are looking for someone to deal with the Sea Raiders.",
            HandmaidQuestText.PlayerAsk("Sea Raiders"));
        Assert.Equal("I hear you are looking for someone to deal with the Desert Bandits. (this village's own)",
            HandmaidQuestText.PlayerAsk("Desert Bandits", devVillagesOwn: true));
        Assert.Equal("They den somewhere not far from here. I have no coin to spare for it, but you have armed men at your back, and the folk here are looking for someone to help them. Go quickly: if they took anyone alive, there may still be time.", HandmaidQuestText.GiverAsk(40));
        Assert.Contains("you have the look of a warrior", HandmaidQuestText.GiverAsk(0));
        Assert.DoesNotContain("{", HandmaidQuestText.GiverAsk(3));
        Assert.Equal("Then go with our thanks. One of our lads will mark the place on your map.", HandmaidQuestText.GiverAccepted);
        Assert.Equal("It is done, then. The roads are quieter for it. I have no coin to give you, but the folk here will not forget it, and neither will I.", HandmaidQuestText.GiverThanks);
        Assert.Equal("Bolat of Tasil is asking for someone to deal with the Steppe Bandits.",
            HandmaidQuestText.OfferNotice("Steppe Bandits", "Bolat", "Tasil"));
        Assert.Contains("A man of this village still waits for his niece", HandmaidQuestText.StoryFor("desert_bandits"));
        Assert.Contains("Our men found a woman dead by the water", HandmaidQuestText.StoryFor("sea_raiders"));
        Assert.StartsWith("Corwyn of Pravend told you", HandmaidQuestText.StartLog("Forest Bandits", "Corwyn", "Pravend"));
        Assert.Contains("of the village told you", HandmaidQuestText.StartLog("Forest Bandits", "Corwyn", null));
        Assert.Equal("Speak with Bolat about the Steppe Bandits", HandmaidQuestText.MenuOption("Steppe Bandits", "Bolat"));
        Assert.StartsWith("Hill bandits burned a village in the glen some days ago.",HandmaidQuestText.StoryFor("mountain_bandits"));
    }

    [Fact]
    public void NoLineLeavesABraceBehind()
    {
        var den = HandmaidQuestText.DenName("forest_bandits");
        var lines = new List<string>
        {
            HandmaidQuestText.Title(den),
            HandmaidQuestText.PlayerAsk(den), HandmaidQuestText.PlayerAsk(den, true),
            HandmaidQuestText.StartLog(den, "Corwyn", "Pravend"), HandmaidQuestText.StartLog(den, null, null),
            HandmaidQuestText.MarkedNotice(den, "Corwyn"),
            HandmaidQuestText.RetargetLog(den), HandmaidQuestText.WaitLog(den), HandmaidQuestText.FoundAgainLog(den),
            HandmaidQuestText.SuccessLog(den, "Corwyn", true), HandmaidQuestText.SuccessLog(den, "Corwyn", false),
            HandmaidQuestText.AbandonLog(den, "Corwyn", true), HandmaidQuestText.AbandonLog(den, "", false),
            HandmaidQuestText.OfferNotice(den, "Corwyn", "Pravend"), HandmaidQuestText.MenuOption(den, "Corwyn"),
            HandmaidQuestText.MenuGiveUp(den), HandmaidQuestText.GiveUpConfirmBody(den, "Corwyn"),
            HandmaidQuestText.PlayerStill(den), HandmaidQuestText.PlayerGiveUp(den),
            HandmaidQuestText.GiverGreeting, HandmaidQuestText.GiverAsk(0), HandmaidQuestText.GiverAsk(30), HandmaidQuestText.GiverAccepted,
            HandmaidQuestText.GiverStill, HandmaidQuestText.GiverGiveUp, HandmaidQuestText.PlayerAccept,
            HandmaidQuestText.PlayerNotNow, HandmaidQuestText.PlayerLeave,
        };
        lines.AddRange(HandmaidQuestText.DenCultures.Select(HandmaidQuestText.StoryFor));
        Assert.All(lines, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l));
            Assert.DoesNotContain("{", l);
            Assert.DoesNotContain("}", l);
        });
        Assert.Equal("You cleared the Forest Bandits' den. Corwyn sent word: \"It is done, then. The roads are quieter for it. I have no coin to give you, but the folk here will not forget it, and neither will I.\"",
            HandmaidQuestText.SuccessLog(den, "Corwyn", true));
        Assert.DoesNotContain("thanks", HandmaidQuestText.SuccessLog(den, "Corwyn", false));
    }

    [Fact]
    public void TheDevOrderPutsTheVillagesOwnFirst()
    {
        var order = HandmaidQuestText.DevOrder("steppe_bandits");
        Assert.Equal(new[] { "steppe_bandits", "sea_raiders", "desert_bandits", "forest_bandits", "mountain_bandits" }, order);
        Assert.Equal(HandmaidQuestText.DenCultures, HandmaidQuestText.DevOrder(null));
    }

    [Fact]
    public void NoWordStillBelongsToATownMerchant()
    {
        var den = HandmaidQuestText.DenName("forest_bandits");
        var lines = new List<string>
        {
            HandmaidQuestText.GiverAccepted, HandmaidQuestText.GiverThanks, HandmaidQuestText.GiverStill,
            HandmaidQuestText.GiverGiveUp, HandmaidQuestText.GiverGreeting, HandmaidQuestText.GiverAsk(0),
            HandmaidQuestText.GiveUpConfirmBody(den, "Corwyn"), HandmaidQuestText.PlayerAsk(den, true),
            HandmaidQuestText.StartLog(den, null, null), HandmaidQuestText.OfferNotice(den, null, null),
        };
        lines.AddRange(HandmaidQuestText.DenCultures.Select(HandmaidQuestText.StoryFor));
        Assert.All(lines, l =>
        {
            Assert.DoesNotContain("town", l, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("market", l, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("my men", l, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("my man ", l, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Which villages offer it now ──

    private static List<string> Villages(int n) => Enumerable.Range(0, n).Select(i => "village_" + i).ToList();

    [Fact]
    public void TheGateIsDeterministic()
    {
        foreach (var v in Villages(200))
        {
            var first = HandmaidQuestText.VillageOffersNow("campaign_ab12cd34_Toni", v, 37.25);
            Assert.Equal(first, HandmaidQuestText.VillageOffersNow("campaign_ab12cd34_Toni", v, 37.25));
            // Anywhere inside the same window, the same answer.
            Assert.Equal(first, HandmaidQuestText.VillageOffersNow("campaign_ab12cd34_Toni", v, 28.0));
            Assert.Equal(first, HandmaidQuestText.VillageOffersNow("campaign_ab12cd34_Toni", v, 41.99));
            Assert.Equal(first, HandmaidQuestText.VillageOffersIn("campaign_ab12cd34_Toni", v, 2));
        }
        Assert.Equal(2, HandmaidQuestText.OfferWindow(28.0));
        Assert.Equal(1, HandmaidQuestText.OfferWindow(27.99));
        Assert.Equal(0, HandmaidQuestText.OfferWindow(0));
        Assert.Equal("village_3|5", HandmaidQuestText.NoticeKey("village_3", 5));
    }

    [Fact]
    public void AboutAFifthOfTheVillagesOffer()
    {
        Assert.Equal(20, HandmaidQuestText.OfferPercent);
        Assert.Equal(14, HandmaidQuestText.OfferWindowDays);
        foreach (var seed in new[] { "campaign_legacy", "campaign_ab12cd34_Toni", "x" })
            foreach (var day in new[] { 0.0, 15.5, 400.0 })
            {
                int offering = Villages(1000).Count(v => HandmaidQuestText.VillageOffersNow(seed, v, day));
                Assert.InRange(offering, 150, 250);
            }
    }

    [Fact]
    public void ANewWindowOrANewCampaignMovesTheTrouble()
    {
        var villages = Villages(1000);
        var now = villages.Where(v => HandmaidQuestText.VillageOffersNow("campaign_ab12cd34_Toni", v, 3)).ToList();
        var nextWindow = villages.Where(v => HandmaidQuestText.VillageOffersNow("campaign_ab12cd34_Toni", v, 3 + 14)).ToList();
        var otherSeed = villages.Where(v => HandmaidQuestText.VillageOffersNow("campaign_99ff00ee_Ana", v, 3)).ToList();
        Assert.NotEqual(now, nextWindow);
        Assert.NotEqual(now, otherSeed);
        // Different sets, not merely shuffled edges: most of the offering villages change.
        Assert.True(now.Intersect(nextWindow).Count() < now.Count / 2);
        Assert.True(now.Intersect(otherSeed).Count() < now.Count / 2);
    }

    [Fact]
    public void NoCoinIsPromisedOnlyRegard()
    {
        Assert.Equal(0, HandmaidQuestText.RewardGold);
        Assert.True(HandmaidQuestText.RelationOnSuccess > 0);
        Assert.DoesNotContain("pay", HandmaidQuestText.GiverAsk(20));
    }
}
