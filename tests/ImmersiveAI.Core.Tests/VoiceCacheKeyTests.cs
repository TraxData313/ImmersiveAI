using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

public class VoiceCacheKeyTests
{
    [Fact]
    public void For_IsStableAcrossCalls()
    {
        var a = VoiceCacheKey.For("sibylla", "Well met again.", "talker-1.7b", -1);
        var b = VoiceCacheKey.For("sibylla", "Well met again.", "talker-1.7b", -1);
        Assert.Equal(a, b);
        Assert.Equal(16, a.Length);          // 64 bits of hex, always padded
    }

    [Fact]
    public void For_ChangesWithEverythingThatChangesTheSound()
    {
        var baseline = VoiceCacheKey.For("sibylla", "Well met.", "talker-1.7b", -1);
        Assert.NotEqual(baseline, VoiceCacheKey.For("achilles", "Well met.", "talker-1.7b", -1));
        Assert.NotEqual(baseline, VoiceCacheKey.For("sibylla", "Well met!", "talker-1.7b", -1));
        Assert.NotEqual(baseline, VoiceCacheKey.For("sibylla", "Well met.", "talker-0.6b", -1));
        Assert.NotEqual(baseline, VoiceCacheKey.For("sibylla", "Well met.", "talker-1.7b", 2069));
    }

    [Fact]
    public void For_IgnoresWhatOnlyChangesTheLook()
    {
        // Re-rendered with different spacing or a line break: still the same performance.
        var a = VoiceCacheKey.For("sibylla", "Well met again.\nThe road was long.");
        var b = VoiceCacheKey.For("sibylla", "  Well met again.   The road was long.  ");
        Assert.Equal(a, b);
    }

    [Fact]
    public void For_CaseAndPunctuationStillMatter()
    {
        // "Go." and "Go?" are different performances even though a reader might not care.
        Assert.NotEqual(VoiceCacheKey.For("s", "Go."), VoiceCacheKey.For("s", "Go?"));
        Assert.NotEqual(VoiceCacheKey.For("s", "go."), VoiceCacheKey.For("s", "Go."));
    }

    [Fact]
    public void For_FieldsCannotBleedIntoEachOther()
    {
        // Without a separator, ("ab","c") and ("a","bc") would hash identically — and the failure
        // would be one wrong voice on one line, unreproducibly.
        Assert.NotEqual(VoiceCacheKey.For("ab", "c"), VoiceCacheKey.For("a", "bc"));
        Assert.NotEqual(VoiceCacheKey.For("sib", "ylla says hi"), VoiceCacheKey.For("sibylla", " says hi"));
    }

    [Fact]
    public void For_HandlesCyrillic_AndDistinguishesIt()
    {
        var bg = VoiceCacheKey.For("sibylla", "Не бях помисляла, че ще те видя отново.");
        var en = VoiceCacheKey.For("sibylla", "I had not thought to see you again.");
        Assert.Equal(16, bg.Length);
        Assert.NotEqual(bg, en);
    }

    [Fact]
    public void For_NullsAreSafe()
    {
        Assert.Equal(16, VoiceCacheKey.For(null, null).Length);
        Assert.Equal(VoiceCacheKey.For(null, ""), VoiceCacheKey.For(null, "   "));
    }

    [Fact]
    public void For_NoCollisionsAcrossAWholeCampaignsWorthOfLines()
    {
        // A long campaign holds thousands of lines; a collision is the wrong person's voice, once.
        var seen = new HashSet<string>();
        foreach (var voice in new[] { "sibylla", "achilles", "briseis" })
            for (var i = 0; i < 4000; i++)
                Assert.True(seen.Add(VoiceCacheKey.For(voice, $"Line number {i} of the long road.")),
                            $"collision at {voice}/{i}");
        Assert.Equal(12000, seen.Count);
    }

    [Theory]
    [InlineData(0, "000.wav")]
    [InlineData(7, "007.wav")]
    [InlineData(42, "042.wav")]
    [InlineData(123, "123.wav")]
    public void ChunkFileName_SortsAsItPlays(int index, string expected)
    {
        Assert.Equal(expected, VoiceCacheKey.ChunkFileName(index));
    }

    [Fact]
    public void ChunkFileNames_LexicalOrderIsPlaybackOrder()
    {
        var names = Enumerable.Range(0, 12).Select(VoiceCacheKey.ChunkFileName).ToList();
        Assert.Equal(names, names.OrderBy(n => n, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Normalize_CollapsesEveryRunOfWhitespace()
    {
        Assert.Equal("a b c", VoiceCacheKey.Normalize("  a \t b\n\n c  "));
        Assert.Equal(string.Empty, VoiceCacheKey.Normalize(null));
        Assert.Equal(string.Empty, VoiceCacheKey.Normalize(" \n "));
    }

    [Fact]
    public void Key_MatchesWhatSpeakableTextWouldHandTheEngine()
    {
        // The realistic round trip: a reply with a gesture in it caches by its SPOKEN words only,
        // so editing a gesture never invalidates audio that has not changed.
        const string withGesture = "Sit with me. *I pour the wine* It was a hard day.";
        const string otherGesture = "Sit with me. *she sets down her cup* It was a hard day.";

        var a = VoiceCacheKey.For("sibylla", SpeakableText.SpokenOnly(withGesture));
        var b = VoiceCacheKey.For("sibylla", SpeakableText.SpokenOnly(otherGesture));
        Assert.Equal(a, b);
    }
}
