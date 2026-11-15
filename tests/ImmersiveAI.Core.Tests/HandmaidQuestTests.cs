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
        Assert.Equal("So you need someone to deal with the Steppe Bandits?", HandmaidQuestText.IssueAccept("Steppe Bandits"));
        Assert.Equal("Bolat of Tasil wants the Steppe Bandits' den cleared.", HandmaidQuestText.Description("Steppe Bandits", "Bolat", "Tasil"));
        Assert.Equal("Have you dealt with the Steppe Bandits yet?", HandmaidQuestText.GiverNews("Steppe Bandits"));
        Assert.Contains("A man of this village still waits for his niece", HandmaidQuestText.StoryFor("desert_bandits"));
        Assert.Contains("Our men found a woman dead by the water", HandmaidQuestText.StoryFor("sea_raiders"));
        Assert.StartsWith("Corwyn of Pravend told you", HandmaidQuestText.StartLog("Forest Bandits", "Corwyn", "Pravend"));
        Assert.Contains("of the village told you", HandmaidQuestText.StartLog("Forest Bandits", "Corwyn", null));
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
            HandmaidQuestText.IssueAccept(den), HandmaidQuestText.Description(den, "Corwyn", "Pravend"),
            HandmaidQuestText.Description(den, null, null), HandmaidQuestText.GiverNews(den),
            HandmaidQuestText.TimedOutLog(den, "Corwyn"), HandmaidQuestText.Rumor(den, "Corwyn"),
            HandmaidQuestText.MenuGiveUp(den), HandmaidQuestText.GiveUpConfirmBody(den, "Corwyn"),
            HandmaidQuestText.PlayerStill(den), HandmaidQuestText.PlayerGiveUp(den),
            HandmaidQuestText.GiverAsk(0), HandmaidQuestText.GiverAsk(30), HandmaidQuestText.GiverAccepted,
            HandmaidQuestText.GiverStill, HandmaidQuestText.GiverGiveUp, HandmaidQuestText.PlayerAccept,
            HandmaidQuestText.PlayerNotNow,
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
            HandmaidQuestText.GiverGiveUp, HandmaidQuestText.GiverAsk(0), HandmaidQuestText.GiverNews(den),
            HandmaidQuestText.GiveUpConfirmBody(den, "Corwyn"), HandmaidQuestText.PlayerAsk(den, true),
            HandmaidQuestText.StartLog(den, null, null), HandmaidQuestText.Description(den, null, null),
            HandmaidQuestText.Rumor(den, null), HandmaidQuestText.TimedOutLog(den, null),
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

    [Fact]
    public void TheIssueKeepsVanillasTimes()
    {
        // Vanilla's bandit-base issue stands 15 days and gives 30 to finish.
        Assert.Equal(15, HandmaidQuestText.IssueDays);
        Assert.Equal(30, HandmaidQuestText.QuestDays);
    }

    [Fact]
    public void NoCoinIsPromisedOnlyRegard()
    {
        Assert.Equal(0, HandmaidQuestText.RewardGold);
        Assert.True(HandmaidQuestText.RelationOnSuccess > 0);
        Assert.DoesNotContain("pay", HandmaidQuestText.GiverAsk(20));
    }
}
