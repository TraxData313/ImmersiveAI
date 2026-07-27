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
    [InlineData("YES: the granary tally is short and they must hear of it", "the granary tally is short and they must hear of it")]
    [InlineData("Yes — the wounded lack herbs", "the wounded lack herbs")]
    [InlineData("yes: \"their smith pays double for iron\"", "their smith pays double for iron")]
    [InlineData("**YES:** the war with the Vlandians touches our caravan", "the war with the Vlandians touches our caravan")]
    [InlineData("GO: the tolls on the west road", "the tolls on the west road")] // the old shape still reads
    [InlineData("I go: my lord's letter must be answered", "my lord's letter must be answered")]
    public void WantsToGo_ReadsTheDecisionAndHandsBackTheTopic(string reply, string expectedReason)
    {
        Assert.True(InitiationParser.WantsToGo(reply, out var reason));
        Assert.Equal(expectedReason, reason);
    }

    [Theory]
    [InlineData("YES")]
    [InlineData("Yes.")]
    [InlineData("Aye")]
    public void WantsToGo_AcceptsABareAssentWithNoTopic(string reply)
    {
        Assert.True(InitiationParser.WantsToGo(reply, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("NO")]
    [InlineData("No.")]
    [InlineData("no — nothing needs saying today")]
    [InlineData("Nay, my ledgers need me more than talk does.")]
    [InlineData("STAY")] // the old shape still reads
    [InlineData("I stay; my ledgers need me more than talk does.")]
    [InlineData("I remain at my work.")]
    [InlineData("Good day to them, but I have my rounds.")] // "Go..."-lookalike must not read as assent
    [InlineData("Nothing comes to mind.")]
    [InlineData("")]
    [InlineData(null)]
    public void WantsToGo_TreatsRefusalOrMumbleAsNo(string? reply)
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
