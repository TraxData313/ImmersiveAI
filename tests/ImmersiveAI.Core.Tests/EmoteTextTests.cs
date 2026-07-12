using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class EmoteTextTests
{
    private static (string Text, bool IsGesture)[] Flat(string? body)
    {
        var segments = EmoteText.Split(body);
        var flat = new (string, bool)[segments.Count];
        for (int i = 0; i < segments.Count; i++) flat[i] = (segments[i].Text, segments[i].IsGesture);
        return flat;
    }

    [Fact]
    public void Split_PlainSpeech_IsOneSpeechSegment()
    {
        var segs = Flat("Well met, my friend. The road was long.");
        Assert.Single(segs);
        Assert.Equal(("Well met, my friend. The road was long.", false), segs[0]);
    }

    [Fact]
    public void Split_LeadingGesture_ThenSpeech()
    {
        var segs = Flat("*smiles* Well met.");
        Assert.Equal(2, segs.Length);
        Assert.Equal(("smiles", true), segs[0]);
        Assert.Equal(("Well met.", false), segs[1]);
    }

    [Fact]
    public void Split_SpeechGestureSpeech_KeepsOrder()
    {
        var segs = Flat("Sit with me. *I pour the wine and slide the cup across* It was a hard day.");
        Assert.Equal(3, segs.Length);
        Assert.Equal(("Sit with me.", false), segs[0]);
        Assert.Equal(("I pour the wine and slide the cup across", true), segs[1]);
        Assert.Equal(("It was a hard day.", false), segs[2]);
    }

    [Fact]
    public void Split_GestureOnly_IsOneGestureSegment()
    {
        var segs = Flat("*meets your eyes, and says nothing*");
        Assert.Single(segs);
        Assert.Equal(("meets your eyes, and says nothing", true), segs[0]);
    }

    [Fact]
    public void Split_DoubledAsterisks_StayLiteral()
    {
        var segs = Flat("This is **not** a gesture.");
        Assert.Single(segs);
        Assert.Equal(("This is **not** a gesture.", false), segs[0]);
    }

    [Fact]
    public void Split_UnclosedOrSpacePaddedSpans_StayLiteral()
    {
        var unclosed = Flat("An honest 2*3 is 6, *unclosed to the end");
        Assert.Single(unclosed);
        Assert.Equal(("An honest 2*3 is 6, *unclosed to the end", false), unclosed[0]);
        // "* smiles *" hugs nothing — a stray pair of asterisks, not the grammar.
        var padded = Flat("A padded * smiles * stays words.");
        Assert.Single(padded);
        Assert.Equal(("A padded * smiles * stays words.", false), padded[0]);
    }

    [Fact]
    public void Split_SpanMayNotCrossALineBreak()
    {
        var segs = Flat("*first line\nsecond line*");
        Assert.Single(segs);
        Assert.False(segs[0].IsGesture);
    }

    [Fact]
    public void Split_BlankInput_IsEmpty()
    {
        Assert.Empty(EmoteText.Split(null));
        Assert.Empty(EmoteText.Split("   "));
    }

    [Fact]
    public void HasGesture_SeesOnlyTheStrictGrammar()
    {
        Assert.True(EmoteText.HasGesture("*bows* My lord."));
        Assert.False(EmoteText.HasGesture("A **bold** claim with 2*3 math."));
    }
}
