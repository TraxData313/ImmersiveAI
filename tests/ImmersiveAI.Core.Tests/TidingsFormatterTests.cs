using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class TidingsFormatterTests
{
    [Fact]
    public void StripMarkup_RemovesLinkTagsAndSmoothsWhitespace()
    {
        Assert.Equal(
            "Derthert won the tournament at Sargot.",
            TidingsFormatter.StripMarkup("<a style=\"Link\" href=\"event:Hero\">Derthert</a>  won the tournament at <b>Sargot</b>."));
    }

    [Fact]
    public void StripMarkup_HandlesNullAndBlank()
    {
        Assert.Equal(string.Empty, TidingsFormatter.StripMarkup(null));
        Assert.Equal(string.Empty, TidingsFormatter.StripMarkup("   "));
    }

    [Fact]
    public void AgoPhrase_SpeaksTheAgeNaturally()
    {
        Assert.Equal("earlier today", TidingsFormatter.AgoPhrase(0.3));
        Assert.Equal("yesterday", TidingsFormatter.AgoPhrase(1.6));
        Assert.Equal("some 4 days past", TidingsFormatter.AgoPhrase(4.9));
    }

    [Fact]
    public void TidingLine_FoldsTheTrailingPeriodBeforeTheAge()
    {
        Assert.Equal(
            "- Vlandia declared war on Battania — yesterday.",
            TidingsFormatter.TidingLine("Vlandia declared war on Battania.", 1.2));
    }

    [Fact]
    public void TidingLine_EmptyFactYieldsEmptyLine()
    {
        Assert.Equal(string.Empty, TidingsFormatter.TidingLine("<a></a>", 2));
    }

    [Fact]
    public void RumorLine_QuotesTheOverheardWords_ButNotTwice()
    {
        Assert.Equal("- “It will fall hardest on the poor folk.”",
            TidingsFormatter.RumorLine("It will fall hardest on the poor folk."));
        Assert.Equal("- “Already quoted.”", TidingsFormatter.RumorLine("“Already quoted.”"));
    }

    [Fact]
    public void Compose_EmptyWhenThereIsNothingToTell()
    {
        Assert.Equal(string.Empty, TidingsFormatter.Compose(new string[0], new string[0], "Sargot"));
    }

    [Fact]
    public void Compose_WeavesTidingsAndStreetTalk()
    {
        var block = TidingsFormatter.Compose(
            new[] { "- A war began — yesterday." },
            new[] { "- “So it's war, then.”" },
            "Sargot");

        Assert.Contains("Tidings of the world's late doings have reached my ears:", block);
        Assert.Contains("- A war began — yesterday.", block);
        Assert.Contains("in the streets of Sargot", block);
        Assert.Contains("- “So it's war, then.”", block);
    }

    [Fact]
    public void Compose_RumorsAloneNeedNoTidingsHeading_AndNoPlaceIsFine()
    {
        var block = TidingsFormatter.Compose(new string[0], new[] { "- “Word travels.”" }, null);
        Assert.DoesNotContain("Tidings of the world's", block);
        Assert.Contains("And I have overheard the common folk say:", block);
    }
}
