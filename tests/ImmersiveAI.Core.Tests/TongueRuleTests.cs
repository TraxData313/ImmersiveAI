using System.Text;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The tongue rule keeps a generated-once text in the language the player actually plays in. Its
/// whole defence is that it SHOWS rather than TELLS, so what these tests guard is that real words
/// reach the model as evidence — and that a call site with nothing to show still says something
/// rather than falling silently back to English by omission.
/// </summary>
public class TongueRuleTests
{
    private static string Quoted(string? evidence, bool ownMind = false)
    {
        var sb = new StringBuilder();
        TongueRule.AppendQuoted(sb, evidence, "These are the keeper's words:", "the sentences", ownMind);
        return sb.ToString();
    }

    [Fact]
    public void AppendQuoted_CarriesTheEvidenceItself_NotAClaimAboutIt()
    {
        var prompt = Quoted("Светът е суров и средновековен.");

        Assert.Contains("Светът е суров и средновековен.", prompt);
        Assert.Contains("SAME TONGUE", prompt);
        Assert.Contains("do not translate it into another", prompt);
        // The words must arrive fenced, or a long world prompt bleeds into the instruction around it.
        Assert.Contains("\"\"\"", prompt);
    }

    [Fact]
    public void AppendQuoted_WithNothingToShow_StillStatesTheRule()
    {
        foreach (var nothing in new[] { null, "", "   " })
        {
            var prompt = Quoted(nothing);
            Assert.Contains("THE TONGUE", prompt);
            Assert.Contains("write in English", prompt);
            Assert.DoesNotContain("\"\"\"", prompt);
        }
    }

    [Fact]
    public void AppendQuoted_SpeaksAsTheOwnMind_WhenAsked()
    {
        Assert.Contains("I set down the sentences", Quoted("some words", ownMind: true));
        Assert.Contains("Write the sentences", Quoted("some words"));
    }

    [Fact]
    public void AppendQuoted_CutsLongEvidenceAtAWordBoundary()
    {
        var evidence = string.Join(" ", Enumerable.Repeat("word", 800));
        var sb = new StringBuilder();
        TongueRule.AppendQuoted(sb, evidence, "intro", "it", maxChars: 100);
        var prompt = sb.ToString();

        Assert.Contains("…", prompt);
        Assert.DoesNotContain("wor…", prompt);          // never severed mid-word
        Assert.True(prompt.Length < 400);
    }

    [Fact]
    public void AppendFromAbove_PointsAtTheEvidence_WithoutRepeatingIt()
    {
        var sb = new StringBuilder();
        TongueRule.AppendFromAbove(sb, "the words set down above", "all I set down here");
        var prompt = sb.ToString();

        Assert.Contains("the words set down above", prompt);
        Assert.Contains("SAME TONGUE", prompt);
        // The point of this shape is that it costs nothing — up to forty turns already stand above.
        Assert.DoesNotContain("\"\"\"", prompt);
        Assert.True(prompt.Length < 400);
    }

    [Fact]
    public void AppendFromAbove_IsTheOwnMindByDefault()
    {
        var sb = new StringBuilder();
        TongueRule.AppendFromAbove(sb, "the words above", "my memory");
        Assert.Contains("I write my memory", sb.ToString());
    }
}
