using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Tests;

public class NpcGoalsTests
{
    [Fact]
    public void AddGoal_TakesUpNewAims_ButNotDuplicatesOrPastTheCap()
    {
        var goals = new NpcGoals();
        Assert.True(goals.AddGoal("Win back my father's hall"));
        Assert.True(goals.AddGoal("See my sister safely wed"));

        // A near-restatement (different punctuation/case/spacing) is a duplicate, not a new aim.
        Assert.False(goals.AddGoal("win back my  Father's Hall!"));
        Assert.Equal(2, goals.Goals.Count);

        // The cap holds.
        Assert.True(goals.AddGoal("Grow rich", max: 3));
        Assert.False(goals.AddGoal("Learn the sword", max: 3));
        Assert.Equal(3, goals.Goals.Count);
    }

    [Fact]
    public void DropGoal_ReleasesTheBestMatch_OrReportsAMiss()
    {
        var goals = new NpcGoals();
        goals.AddGoal("Win back my father's hall");
        goals.AddGoal("See my sister safely wed");

        // A close restatement (as the NPC, seeing the exact aim in its prompt, would give) still lands on it.
        var dropped = goals.DropGoal("see my sister wed");
        Assert.Equal("See my sister safely wed", dropped);
        Assert.Single(goals.Goals);

        // Nothing close enough → an honest miss, list untouched.
        Assert.Null(goals.DropGoal("sail across the western sea"));
        Assert.Single(goals.Goals);
    }

    [Fact]
    public void ReviseGoal_ReshapesAnExistingAim_ButNeverInventsOneOnAMiss()
    {
        var goals = new NpcGoals();
        goals.AddGoal("Win back my father's hall");

        var old = goals.ReviseGoal("reclaim my father's hall", "Win back my father's hall, and hold it in peace");
        Assert.Equal("Win back my father's hall", old);
        Assert.Equal("Win back my father's hall, and hold it in peace", Assert.Single(goals.Goals));

        // A revise that matches nothing adds nothing.
        Assert.Null(goals.ReviseGoal("some aim I never held", "a brand new aim"));
        Assert.Single(goals.Goals);
    }

    [Fact]
    public void SetAll_ReplacesWholesale_DroppingBlanksAndDuplicates_TrimmingToTheBudget()
    {
        var goals = new NpcGoals();
        goals.AddGoal("An old aim I am setting aside");

        goals.SetAll(new[] { "Serve my lord well", "  ", "serve my LORD well", "Earn my own land", "Retire in peace" }, max: 2);

        // Duplicate and blank dropped, list replaced, trimmed to the budget of 2.
        Assert.Equal(new[] { "Serve my lord well", "Earn my own land" }, goals.Goals);
    }

    [Fact]
    public void FindBestMatch_PrefersExact_ThenContainment_ThenOverlap_AndRejectsUnrelated()
    {
        var goals = new NpcGoals();
        goals.AddGoal("Win back my father's hall");
        goals.AddGoal("See my sister safely wed");

        Assert.Equal(0, goals.FindBestMatch("win back my father's hall")); // exact (normalized)
        Assert.Equal(0, goals.FindBestMatch("my father's hall"));          // containment
        Assert.Equal(1, goals.FindBestMatch("see my sister wed"));          // word overlap
        Assert.Equal(-1, goals.FindBestMatch("buy a fine warhorse"));       // unrelated → no false match
    }
}
