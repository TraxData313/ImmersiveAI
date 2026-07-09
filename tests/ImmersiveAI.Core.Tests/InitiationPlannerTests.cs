using System.Collections.Generic;
using ImmersiveAI.Core.Initiation;

namespace ImmersiveAI.Core.Tests;

public class InitiationPlannerTests
{
    [Fact]
    public void PickWeightedIndex_EmptyOrAllZero_ReturnsMinusOne()
    {
        Assert.Equal(-1, InitiationPlanner.PickWeightedIndex(new List<double>(), 0.5));
        Assert.Equal(-1, InitiationPlanner.PickWeightedIndex(new List<double> { 0, 0 }, 0.5));
    }

    [Fact]
    public void PickWeightedIndex_FavoursTheHeavierWeight()
    {
        // Ana 30, Eva 70 -> the 0..30% of the roll lands on Ana, the rest on Eva.
        var weights = new List<double> { 30, 70 };
        Assert.Equal(0, InitiationPlanner.PickWeightedIndex(weights, 0.0));
        Assert.Equal(0, InitiationPlanner.PickWeightedIndex(weights, 0.29));
        Assert.Equal(1, InitiationPlanner.PickWeightedIndex(weights, 0.30));
        Assert.Equal(1, InitiationPlanner.PickWeightedIndex(weights, 0.99));
    }

    [Fact]
    public void PickWeightedIndex_SkipsZeroWeightBuckets()
    {
        var weights = new List<double> { 0, 50, 0, 50 };
        Assert.Equal(1, InitiationPlanner.PickWeightedIndex(weights, 0.0));
        Assert.Equal(3, InitiationPlanner.PickWeightedIndex(weights, 0.75));
    }

    [Fact]
    public void PickWeightedIndex_HandlesAStrayRollOfOne()
    {
        var weights = new List<double> { 40, 60 };
        Assert.Equal(1, InitiationPlanner.PickWeightedIndex(weights, 1.0));
    }
}
