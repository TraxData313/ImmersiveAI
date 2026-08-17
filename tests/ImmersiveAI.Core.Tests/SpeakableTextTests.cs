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
        // The gesture is CLOSED, or it runs into the line after it and the whole thing is read as
        // one breathless sentence with no place to breathe.
        var said = SpeakableText.SpokenWithGestures("Sit with me. *I pour the wine* It was a hard day.");
        Assert.Equal("Sit with me. I pour the wine. It was a hard day.", said);
    }

    [Fact]
    public void SpokenWithGestures_ClosesAGestureThatEndsOnAWord()
    {
        var said = SpeakableText.SpokenWithGestures("*she looks up, slowly* Well?");
        Assert.Equal("she looks up, slowly. Well?", said);
    }

    [Fact]
    public void SpokenWithGestures_LeavesTheWritersOwnPunctuationAlone()
    {
        // Already ended — a second stop would be the mod correcting her writing.
        var said = SpeakableText.SpokenWithGestures("*she laughs!* Well?");
        Assert.Equal("she laughs! Well?", said);
    }

    [Fact]
    public void SpokenWithGestures_NeverSpeaksTheAsterisks()
    {
        var said = SpeakableText.SpokenWithGestures("*sets down her cup* Say it again.");
        Assert.DoesNotContain("*", said);
    }

    [Fact]
    public void BitesFor_WithGestures_CarriesWhatSpokenOnlyDrops()
    {
        var without = SpeakableText.BitesFor("*turns away without a word*");
        Assert.Empty(without);

        var with = SpeakableText.BitesFor("*turns away without a word*", includeGestures: true);
        Assert.Single(with);
        Assert.Contains("turns away", with[0]);
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

    // ---------------- the whitelist: what reaches the speech engine ----------------
    //
    // Ported from the sister project (claude-voice), which drives the same engine on the same card
    // and derailed ONCE in ~1000 generations where this mod derailed 12 times in 196. It passes every
    // character through a whitelist; we passed none. A rare token is what an autoregressive model
    // wanders off after, and the mod's own prose style reaches for typographic dashes constantly.

    [Fact]
    public void Normalize_TurnsTypographyIntoThePausesItMeans()
    {
        // An em dash IS a pause, so it becomes one rather than being dropped into a run-on.
        Assert.Equal("I shall be ready soon, and I shall come back to you whole.",
            SpeakableText.SpokenOnly("I shall be ready soon—and I shall come back to you whole."));

        Assert.Equal("I had not thought to see you again. not after everything",
            SpeakableText.SpokenOnly("I had not thought to see you again… not after everything"));

        Assert.Equal("She said 'no' and meant \"never\".",
            SpeakableText.SpokenOnly("She said ‘no’ and meant “never”."));
    }

    [Fact]
    public void Normalize_KeepsEveryScriptsLettersAndDigits()
    {
        // Anton plays in Bulgarian. Asking for [A-Za-z0-9] would silently mute half the mod.
        const string bg = "Мислих дълго за това, което ми каза при брода.";
        Assert.Equal(bg, SpeakableText.SpokenOnly(bg));

        Assert.Equal("13 men and 4 horses.", SpeakableText.SpokenOnly("13 men and 4 horses."));
    }

    [Fact]
    public void Normalize_SweepsAwayTheInvisibleAndTheDecorative()
    {
        // A zero-width space is the worst of them: it cannot be seen in any log or any editor. It
        // closes up rather than becoming a space, so a word it was hiding inside stays one word.
        Assert.Equal("no gaphere", SpeakableText.SpokenOnly("no gap​here"));

        // A non-breaking space is a space; the card marks the thread draws are not speech at all.
        Assert.Equal("a b", SpeakableText.SpokenOnly("a b"));
        Assert.DoesNotContain("❮", SpeakableText.SpokenOnly("❦ our wedding day"));
    }

    [Fact]
    public void Normalize_SaysTheSymbolsWorthSaying()
    {
        Assert.Contains("times", SpeakableText.SpokenOnly("Wool ×24 was the whole of it."));
        Assert.Contains("degrees", SpeakableText.SpokenOnly("It stood at 30° that day."));
    }

    [Fact]
    public void Normalize_NeverThrowsAndKeepsPlainTextExactly()
    {
        Assert.Equal(string.Empty, SpeakableText.Normalize(null));
        Assert.Equal(string.Empty, SpeakableText.Normalize(""));

        const string plain = "Then we are agreed. I will tell the men at first light!";
        Assert.Equal(plain, SpeakableText.SpokenOnly(plain));
    }
}
