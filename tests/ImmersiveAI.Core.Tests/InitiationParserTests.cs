using ImmersiveAI.Core.Initiation;

namespace ImmersiveAI.Core.Tests;

public class InitiationParserTests
{
    [Theory]
    [InlineData("Yes")]
    [InlineData("yes.")]
    [InlineData("Yes, with all my heart.")]
    [InlineData("Aye")]
    [InlineData("Gladly — I have missed them.")]
    public void WantsToReachOut_ReadsAssent(string reply)
    {
        Assert.True(InitiationParser.WantsToReachOut(reply));
    }

    [Theory]
    [InlineData("No")]
    [InlineData("no, not today")]
    [InlineData("Nay")]
    [InlineData("Not now, my heart is heavy.")]
    [InlineData("")]
    [InlineData(null)]
    public void WantsToReachOut_TreatsRefusalOrSilenceAsNo(string? reply)
    {
        Assert.False(InitiationParser.WantsToReachOut(reply!));
    }


    [Theory]
    [InlineData("GO: the granary tally is short and they must hear of it", "the granary tally is short and they must hear of it")]
    [InlineData("GO — the wounded lack herbs", "the wounded lack herbs")]
    [InlineData("go: \"their smith pays double for iron\"", "their smith pays double for iron")]
    [InlineData("**GO:** the war with the Vlandians touches our caravan", "the war with the Vlandians touches our caravan")]
    [InlineData("I go: my lord's letter must be answered", "my lord's letter must be answered")]
    public void WantsToGo_ReadsTheDecisionAndHandsBackTheCause(string reply, string expectedReason)
    {
        Assert.True(InitiationParser.WantsToGo(reply, out var reason));
        Assert.Equal(expectedReason, reason);
    }

    [Theory]
    [InlineData("GO")]
    [InlineData("Yes")]
    public void WantsToGo_AcceptsABareAssentWithNoCause(string reply)
    {
        Assert.True(InitiationParser.WantsToGo(reply, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("STAY")]
    [InlineData("Stay.")]
    [InlineData("stay — I have nothing of substance for them today")]
    [InlineData("I stay; my ledgers need me more than talk does.")]
    [InlineData("I remain at my work.")]
    [InlineData("Good day to them, but I have my rounds.")] // "Go..."-lookalike must not read as GO
    [InlineData("No")]
    [InlineData("")]
    [InlineData(null)]
    public void WantsToGo_TreatsStayRefusalOrMumbleAsStay(string? reply)
    {
        Assert.False(InitiationParser.WantsToGo(reply, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("none")]
    [InlineData("None.")]
    [InlineData("(none)")]
    [InlineData("pass")]
    [InlineData("no")]
    [InlineData("nothing")]
    [InlineData("not now")]
    public void IsDecline_ReadsASingleWordDecliningAnswer(string? reply)
    {
        Assert.True(InitiationParser.IsDecline(reply!));
    }

    [Theory]
    [InlineData("Come to me by the river; I have news of your brother that will not keep.")]
    [InlineData("No wonder you have not visited — the roads north are thick with bandits. Come, sit.")]
    [InlineData("I have been thinking of you.")]
    public void IsDecline_TreatsRealOpeningWordsAsReachingOut(string reply)
    {
        // A genuine greeting that merely contains "no" must still count as a wish to talk.
        Assert.False(InitiationParser.IsDecline(reply));
    }
}
