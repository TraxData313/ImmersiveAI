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
