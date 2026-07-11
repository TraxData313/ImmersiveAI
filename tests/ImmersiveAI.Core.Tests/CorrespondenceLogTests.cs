using ImmersiveAI.Core.Letters;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class CorrespondenceLogTests
{
    private const string SampleLog =
        "[Summer 2, 1084, morning] Gunjadrid writes to Aurelia (from Sargot, ~2.5 days on the road):\n" +
        "Come north when the snows thin.\n" +
        "The passes hold no fear for you.\n" +
        "\n" +
        "[Summer 5, 1084, evening] Aurelia writes to Gunjadrid (from a camp on the road, ~3.1 days on the road):\n" +
        "I will come.\n" +
        "\n" +
        "[Summer 9, 1084, noon] (Gunjadrid read the letter, and let it lie unanswered.)\n" +
        "\n";

    [Fact]
    public void Parse_ReadsLettersAndNotes_OldestFirst()
    {
        var entries = CorrespondenceLog.Parse(SampleLog);

        Assert.Equal(3, entries.Count);

        Assert.False(entries[0].IsNote);
        Assert.Equal("Gunjadrid", entries[0].FromName);
        Assert.Equal("Aurelia", entries[0].ToName);
        Assert.Equal("from Sargot, ~2.5 days on the road", entries[0].Detail);
        Assert.Equal("Summer 2, 1084, morning", entries[0].Stamp);
        Assert.StartsWith("Come north when the snows thin.", entries[0].Body);
        Assert.EndsWith("The passes hold no fear for you.", entries[0].Body);

        Assert.False(entries[1].IsNote);
        Assert.Equal("Aurelia", entries[1].FromName);
        Assert.Equal("I will come.", entries[1].Body);

        Assert.True(entries[2].IsNote);
        Assert.Contains("let it lie unanswered", entries[2].Body);
    }

    [Fact]
    public void Parse_EmptyOrBroken_IsEmptyNeverAnError()
    {
        Assert.Empty(CorrespondenceLog.Parse(null));
        Assert.Empty(CorrespondenceLog.Parse("   "));
        Assert.Empty(CorrespondenceLog.Parse("stray words with no header at all"));
    }

    [Fact]
    public void Parse_LetterBodyKeepsBlankInnerLines_UntilTheNextHeader()
    {
        var log =
            "[Summer 2, 1084] Ava writes to You (from Sargot, ~1 days on the road):\n" +
            "First paragraph.\n" +
            "\n" +
            "Second paragraph after a pause.\n" +
            "\n";
        var entries = CorrespondenceLog.Parse(log);

        var entry = Assert.Single(entries);
        Assert.Contains("First paragraph.", entry.Body);
        Assert.Contains("Second paragraph after a pause.", entry.Body);
    }

    // ------------------------- recognizing letter beats in the memory stream -------------------------

    [Fact]
    public void IsComposeLetterBeat_MatchesBothComposeLines_AndNothingElse()
    {
        Assert.True(PromptBuilder.IsComposeLetterBeat(PromptBuilder.ComposeLetterLine("Aurelia")));
        Assert.True(PromptBuilder.IsComposeLetterBeat(PromptBuilder.ComposeReplyLine("Aurelia")));

        Assert.False(PromptBuilder.IsComposeLetterBeat(PromptBuilder.WriteLetterDesireLine("Aurelia")));
        Assert.False(PromptBuilder.IsComposeLetterBeat(PromptBuilder.ArrivalLine("Aurelia", firstMeeting: false)));
        Assert.False(PromptBuilder.IsComposeLetterBeat(null));
    }

    [Fact]
    public void TryExtractReceivedLetter_HandsBackTheBodyInsideTheAngelLine()
    {
        var body = "Dear friend,\nthe snows have thinned.\nCome north.";
        var line = PromptBuilder.AnswerLetterDesireLine("Gunjadrid", body);

        Assert.True(PromptBuilder.TryExtractReceivedLetter(line, out var extracted));
        Assert.Equal(body, extracted);

        Assert.False(PromptBuilder.TryExtractReceivedLetter(PromptBuilder.ComposeLetterLine("Gunjadrid"), out _));
        Assert.False(PromptBuilder.TryExtractReceivedLetter(null, out _));
    }
}
