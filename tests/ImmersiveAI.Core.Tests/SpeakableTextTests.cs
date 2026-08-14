using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

public class SpeakableTextTests
{
    // ---------------- what is said, and what is only done ----------------

    [Fact]
    public void SpokenOnly_DropsGestures()
    {
        var said = SpeakableText.SpokenOnly("Sit with me. *I pour the wine* It was a hard day.");
        Assert.Equal("Sit with me. It was a hard day.", said);
    }

    [Fact]
    public void SpokenOnly_AllGesture_IsNothingToSay()
    {
        Assert.Equal(string.Empty, SpeakableText.SpokenOnly("*turns away without a word*"));
        Assert.False(SpeakableText.IsWorthSpeaking("*turns away without a word*"));
    }

    [Fact]
    public void SpokenOnly_MarkdownResidue_StaysLiteral()
    {
        // **bold** is not the acting-out grammar; it must not be mistaken for a gesture and eaten.
        var said = SpeakableText.SpokenOnly("I will **not** go.");
        Assert.Contains("**not**", said);
    }

    [Fact]
    public void SpokenWithGestures_KeepsThemInOrder()
    {
        var said = SpeakableText.SpokenWithGestures("Sit with me. *I pour the wine* It was a hard day.");
        Assert.Equal("Sit with me. I pour the wine It was a hard day.", said);
    }

    [Fact]
    public void IsWorthSpeaking_EmptyAndNull_AreNot()
    {
        Assert.False(SpeakableText.IsWorthSpeaking(null));
        Assert.False(SpeakableText.IsWorthSpeaking("   "));
    }

    // ---------------- cutting it into bites ----------------

    [Fact]
    public void Chunk_SplitsAtSentenceEnds()
    {
        var bites = SpeakableText.Chunk(
            "The road from Ortysia was long and the horses are spent. " +
            "We lost two men to the fords before we ever saw a foe. " +
            "I would rest here a day if you will allow it.");
        Assert.Equal(3, bites.Count);
        Assert.StartsWith("The road", bites[0]);
        Assert.EndsWith("allow it.", bites[2]);
    }

    [Fact]
    public void Chunk_EllipsisTrailingIntoLowerCase_IsOneBreath()
    {
        // The hesitation and its pick-up belong to the same bite; a cut there is an audible seam
        // exactly where the voice was meant to falter.
        var bites = SpeakableText.Chunk(
            "I had not thought to see you again... not after everything that passed between us. " +
            "And yet here you stand!? I hardly know what to say to that.");

        Assert.Contains(bites, b => b.Contains("again... not after"));
        // No bite may begin on the tail of a mark run — that is what "stays whole" means.
        Assert.All(bites, b => Assert.False(
            b.StartsWith(".") || b.StartsWith("?") || b.StartsWith("!"),
            $"bite begins mid-mark: {b}"));
        Assert.Contains(bites, b => b.Contains("stand!?"));
    }

    [Fact]
    public void Chunk_EllipsisEndingAThought_DoesCut()
    {
        // Same marks, upper case after: that IS an ending, and long enough to stand alone.
        var bites = SpeakableText.Chunk(
            "There was nothing left of the village by the time we came down off the ridge... " +
            "We buried what we could find of them before the light went.");
        Assert.Equal(2, bites.Count);
        Assert.EndsWith("ridge...", bites[0]);
    }

    [Fact]
    public void Chunk_AbbreviationDoesNotEndASentence()
    {
        // A stop followed by lower case is an abbreviation, not an ending.
        var bites = SpeakableText.Chunk(
            "We met at the shrine of св. Иван and spoke a long while of the war to come there.");
        Assert.Single(bites);
    }

    [Fact]
    public void Chunk_ShortSentencesGatherInsteadOfBarking()
    {
        var bites = SpeakableText.Chunk("Yes. No. Perhaps. I cannot say which of those is the truth of it.");
        Assert.Single(bites);
    }

    [Fact]
    public void Chunk_TrailingScrapFoldsBackwards()
    {
        var bites = SpeakableText.Chunk(
            "The siege will not hold past the winter, whatever the marshal tells his captains. Go.");
        Assert.Single(bites);
        Assert.EndsWith("Go.", bites[0]);
    }

    [Fact]
    public void Chunk_ClosingQuoteRidesWithItsSentence()
    {
        var bites = SpeakableText.Chunk(
            "He said to me, \"the gate will be open by dusk, and not one hour later.\" " +
            "I did not believe a word of it then and I do not now.");
        Assert.Equal(2, bites.Count);
        Assert.EndsWith("later.\"", bites[0]);
    }

    [Fact]
    public void Chunk_LineBreakIsAPlaceToBreathe()
    {
        var bites = SpeakableText.Chunk(
            "The first thing you must understand is that the ford is watched\n" +
            "The second is that the watchers are not ours to buy");
        Assert.Equal(2, bites.Count);
    }

    [Fact]
    public void Chunk_LongSentenceSplitsAtAPause()
    {
        var run = "I rode through the night past the burning steads and the empty byres, "
                + "past the mill where we sheltered in the spring, past the boundary stone your father set, "
                + "and never once did I see a living soul upon that road until the walls came up out of the mist.";
        var bites = SpeakableText.Chunk(run, maxChars: 100);
        Assert.True(bites.Count > 1);
        Assert.All(bites, b => Assert.True(b.Length <= 130, $"bite too long: {b.Length}"));
    }

    [Fact]
    public void Chunk_NeverSlicesAWordInHalf()
    {
        var run = string.Join(" ", Enumerable.Repeat("Calradia", 60));
        var bites = SpeakableText.Chunk(run, maxChars: 50);
        foreach (var bite in bites)
            foreach (var word in bite.Split(' '))
                Assert.Equal("Calradia", word);
    }

    [Fact]
    public void Chunk_EmptyInput_IsNoBites()
    {
        Assert.Empty(SpeakableText.Chunk(null));
        Assert.Empty(SpeakableText.Chunk("   "));
    }

    [Fact]
    public void Chunk_ReassemblesToTheSameWords()
    {
        // The whole reply must survive the cutting — nothing said may go unsaid.
        const string body = "Well met again. *she rises* I did not look for you before the thaw... "
                          + "and yet you are here, dust and all. Sit. Eat. There is bread enough!";
        var bites = SpeakableText.BitesFor(body);

        var spoken = SpeakableText.SpokenOnly(body).Replace(" ", "");
        var rejoined = string.Concat(bites).Replace(" ", "");
        Assert.Equal(spoken, rejoined);
    }

    [Fact]
    public void BitesFor_GestureOnly_IsSilent()
    {
        Assert.Empty(SpeakableText.BitesFor("*shrugs*"));
    }
}
