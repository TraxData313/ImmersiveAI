using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class FeelingParserTests
{
    [Fact]
    public void ParseShift_ReadsABarePositiveNumber()
    {
        Assert.Equal(5, FeelingParser.ParseShift("5"));
        Assert.Equal(5, FeelingParser.ParseShift("+5"));
    }

    [Fact]
    public void ParseShift_ReadsANegativeNumber()
    {
        Assert.Equal(-30, FeelingParser.ParseShift("-30"));
    }

    [Fact]
    public void ParseShift_ToleratesAWordOrTwoAroundTheNumber()
    {
        Assert.Equal(5, FeelingParser.ParseShift("about +5"));
        Assert.Equal(10, FeelingParser.ParseShift("I would say 10."));
    }

    [Fact]
    public void ParseShift_TakesTheFirstNumber_WhenTheModelEchoesTheTotal()
    {
        // We ask for the shift first, so the leading number is the delta even if it adds "(now 37)".
        Assert.Equal(5, FeelingParser.ParseShift("+5 (now 37)"));
    }

    [Fact]
    public void ParseShift_ClampsToTheStandingRail()
    {
        Assert.Equal(100, FeelingParser.ParseShift("250"));
        Assert.Equal(-100, FeelingParser.ParseShift("-999"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("no change")]
    [InlineData("nothing moved in me")]
    public void ParseShift_ReturnsNull_WhenNoNumberIsGiven(string? input)
    {
        Assert.Null(FeelingParser.ParseShift(input!));
    }
}
