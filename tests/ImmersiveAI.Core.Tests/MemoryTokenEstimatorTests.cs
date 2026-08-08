using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Tests;

public class MemoryTokenEstimatorTests
{
    [Fact]
    public void EstimateTextTokens_UsesConservativeCharacterApproximation()
    {
        Assert.Equal(0, MemoryTokenEstimator.EstimateTextTokens(""));
        Assert.Equal(1, MemoryTokenEstimator.EstimateTextTokens("word"));
        Assert.Equal(2, MemoryTokenEstimator.EstimateTextTokens("five!"));
    }

    [Fact]
    public void EstimateRecentTurnsTokens_IncludesBothLinesAndTurnOverhead()
    {
        var turns = new[]
        {
            new ConversationTurn { PlayerLine = "hello", NpcLine = "well met", GameDay = 1 },
        };

        Assert.Equal(12, MemoryTokenEstimator.EstimateRecentTurnsTokens(turns));
    }

    [Fact]
    public void EstimateTextTokens_ChargesNonLatinTextMore_BecauseItTokenizesWorse()
    {
        // English runs about four characters to the token; Cyrillic, Greek and CJK cost far more,
        // because these models' byte-pair vocabularies are built mostly from English. Measured on
        // a real Bulgarian memory (2026.08.08): ~2.8 chars per token, against 4 for English. A
        // budget sized by the English figure silently overruns — that is how a rich memory came
        // to be cut off in mid-word.
        const string english = "the company rode north at dawn and took the ford";
        var cyrillic = new string('щ', english.Length);

        Assert.True(MemoryTokenEstimator.EstimateTextTokens(cyrillic)
                  > MemoryTokenEstimator.EstimateTextTokens(english));

        // And the estimate errs high rather than low: overestimating only compresses a little
        // early, while underestimating sends a prompt larger than the player's chosen share.
        Assert.True(MemoryTokenEstimator.EstimateTextTokens(new string('щ', 2800)) >= 1000);
    }
}
