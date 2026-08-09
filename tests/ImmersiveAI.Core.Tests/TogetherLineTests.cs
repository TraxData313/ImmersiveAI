using ImmersiveAI.Core.Together;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// THE LINE — one mark at the last moment the two of them had time to themselves, and after it a
/// plain dated list of everything that has happened since (2026.08.09, Anton's design).
/// </summary>
public class TogetherLineTests
{
    private static TogetherEntry Entry(double day, string date, string text) =>
        new TogetherEntry { GameDay = day, DateText = date, Text = text };

    [Fact]
    public void NothingSince_MeansNoLineAtAll()
    {
        // The moment there is nothing after the line, the whole block must vanish — that is how it
        // disappears once they have talked, with no state and no flags to keep straight.
        Assert.Equal(string.Empty, TogetherLine.Build(new List<TogetherEntry>()));
        Assert.Equal(string.Empty, TogetherLine.Build(null!));
        Assert.Equal(string.Empty, TogetherLine.Build(new[] { Entry(100, "Winter 9", "   ") }));
    }

    [Fact]
    public void TheDividerStandsAlone_AndTheListIsDatedAndInOrder()
    {
        var block = TogetherLine.Build(new[]
        {
            Entry(103, "Winter 12, Year 1084", "he went to Thyrsif, and not to me"),
            Entry(101, "Winter 10, Year 1084", "we were at the market in Baltakhand"),
            Entry(102, "Winter 11, Year 1084", "we fought — The Grand Victory near Ortysia"),
        });

        // ONE divider and the list — nothing before it, nothing after it. The opening mark and the
        // closing "mine to raise" were both cut on purpose (Anton, 2026.08.09): the turns carry
        // their own stamps and the entries carry dates, and the rest is left to the soul to work
        // out. "a private discussion" is what does the old closing's work, and "from this moment"
        // anchors the divider to its own place — the block stands BEFORE the transcript, so a
        // backward-looking "since then" would have had nothing behind it to point at.
        Assert.StartsWith(TogetherLine.ListHeader, block);
        Assert.Contains("From this moment", block);
        Assert.Contains("private discussion", block);
        Assert.DoesNotContain("time to ourselves", block);
        Assert.DoesNotContain("mine to raise", block);
        Assert.DoesNotContain("word in passing", block);
        Assert.DoesNotContain("he has not told me", block);

        // Oldest first, and the year is dropped — every line of a fortnight carrying it is noise.
        int market = block.IndexOf("market", StringComparison.Ordinal);
        int fought = block.IndexOf("we fought", StringComparison.Ordinal);
        int thyrsif = block.IndexOf("Thyrsif", StringComparison.Ordinal);
        Assert.True(market < fought && fought < thyrsif);
        Assert.Contains("Winter 10: we were at the market", block);
        Assert.DoesNotContain("Year 1084", block);
    }

    [Fact]
    public void TheFreshestEntriesSurviveTheCap()
    {
        var many = Enumerable.Range(0, 30)
            .Select(i => Entry(100 + i, "Winter " + i, "thing " + i))
            .ToList();

        var block = TogetherLine.Build(many, maxEntries: 5);
        Assert.Contains("thing 29", block);
        Assert.Contains("thing 25", block);
        Assert.DoesNotContain("thing 24", block);
    }

    [Fact]
    public void AnUndatedDayIsSimplyUndated_NeverANumberNoOneWouldUse()
    {
        Assert.Equal("he came to me", TogetherLine.Line(Entry(100, string.Empty, "he came to me")));
        Assert.Equal("Winter 9: he came to me", TogetherLine.Line(Entry(100, "Winter 9, Year 1084", "he came to me")));
        Assert.Equal(string.Empty, TogetherLine.ShortDate(null));
        Assert.Equal("Spring 3", TogetherLine.ShortDate("  Spring 3, Year 1085 "));
    }

}
