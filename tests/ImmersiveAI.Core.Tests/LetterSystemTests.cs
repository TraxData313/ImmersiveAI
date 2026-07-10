using ImmersiveAI.Core.Letters;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class LetterSystemTests
{
    // ------------------------------ the courier ------------------------------

    [Fact]
    public void TravelDays_GrowsWithDistanceAndStaysOnTheRails()
    {
        Assert.Equal(LetterCourier.MinDays, LetterCourier.TravelDays(0));
        Assert.True(LetterCourier.TravelDays(300) > LetterCourier.TravelDays(50));
        Assert.Equal(LetterCourier.MaxDays, LetterCourier.TravelDays(1_000_000));
    }

    [Fact]
    public void TravelDays_UnknownDistance_IsAMiddlingRoadNotADoorstep()
    {
        Assert.Equal(LetterCourier.MaxDays / 2, LetterCourier.TravelDays(-1));
        Assert.Equal(LetterCourier.MaxDays / 2, LetterCourier.TravelDays(double.NaN));
    }

    // ------------------------------ the bag ------------------------------

    private static Letter MakeLetter(string npcId, double arriveDay, bool toPlayer = true) => new()
    {
        NpcId = npcId,
        NpcName = "Gunjadrid",
        ToPlayer = toPlayer,
        Body = "Come north when the snows thin.",
        SentGameDay = arriveDay - 2,
        ArriveGameDay = arriveDay,
    };

    [Fact]
    public void Due_ReturnsOnlyArrivedLetters_OldestFirst()
    {
        var bag = new LetterBag();
        bag.Add(MakeLetter("lord_1", arriveDay: 12));
        bag.Add(MakeLetter("lord_2", arriveDay: 10));
        bag.Add(MakeLetter("lord_3", arriveDay: 99));

        var due = bag.Due(nowGameDay: 12);

        Assert.Equal(2, due.Count);
        Assert.Equal("lord_2", due[0].NpcId);
        Assert.Equal("lord_1", due[1].NpcId);
    }

    [Fact]
    public void HasInFlightWith_TracksOneCourierPerBond()
    {
        var bag = new LetterBag();
        var letter = MakeLetter("lord_1", arriveDay: 10);
        bag.Add(letter);

        Assert.True(bag.HasInFlightWith("lord_1"));
        Assert.False(bag.HasInFlightWith("lord_2"));

        bag.Remove(letter.Id);
        Assert.False(bag.HasInFlightWith("lord_1"));
    }

    [Fact]
    public void InFlightToPlayerCount_CountsOnlyLettersRidingTowardThePlayer()
    {
        // The cap on spontaneous NPC letters gates on how many are bound FOR the player; the
        // player's own outgoing letters must never spend that allowance.
        var bag = new LetterBag();
        Assert.Equal(0, bag.InFlightToPlayerCount);

        bag.Add(MakeLetter("lord_1", arriveDay: 10));                  // NPC → player
        bag.Add(MakeLetter("lord_2", arriveDay: 11));                  // NPC → player
        bag.Add(MakeLetter("lord_3", arriveDay: 12, toPlayer: false)); // player → NPC

        Assert.Equal(2, bag.InFlightToPlayerCount);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTheRoad()
    {
        var path = Path.Combine(Path.GetTempPath(), "immersiveai_tests", Guid.NewGuid().ToString("N"), "_letters.json");
        try
        {
            var bag = new LetterBag();
            bag.Add(MakeLetter("lord_1", arriveDay: 10, toPlayer: false));
            bag.SaveTo(path);

            var loaded = LetterBag.LoadFrom(path);

            var letter = Assert.Single(loaded.Letters);
            Assert.Equal("lord_1", letter.NpcId);
            Assert.False(letter.ToPlayer);
            Assert.Equal("Come north when the snows thin.", letter.Body);
            Assert.Equal(10, letter.ArriveGameDay);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(path))!, recursive: true); } catch { }
        }
    }

    [Fact]
    public void LoadFrom_MissingOrBrokenFile_IsAnEmptyBagNeverAnError()
    {
        Assert.Empty(LetterBag.LoadFrom(Path.Combine(Path.GetTempPath(), "no_such_file.json")).Letters);

        var path = Path.Combine(Path.GetTempPath(), "immersiveai_broken_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ not json");
            Assert.Empty(LetterBag.LoadFrom(path).Letters);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    // ------------------------------ the Angel's letter lines ------------------------------

    [Fact]
    public void LetterLines_CarryTheNamesAndTheWords()
    {
        Assert.Contains("Aeron", PromptBuilder.WriteLetterDesireLine("Aeron"));
        Assert.Contains("yes or no", PromptBuilder.WriteLetterDesireLine("Aeron"));

        Assert.Contains("Aeron", PromptBuilder.ComposeLetterLine("Aeron"));

        var read = PromptBuilder.AnswerLetterDesireLine("Aeron", "Meet me at Sargot.");
        Assert.Contains("Aeron", read);
        Assert.Contains("Meet me at Sargot.", read); // the reading enters their memory verbatim
        Assert.Contains("yes or no", read);

        Assert.Contains("Aeron", PromptBuilder.ComposeReplyLine("Aeron"));
    }
}
