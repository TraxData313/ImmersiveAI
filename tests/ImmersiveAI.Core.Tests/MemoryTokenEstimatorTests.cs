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
}
