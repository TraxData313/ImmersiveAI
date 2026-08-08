using ImmersiveAI.Core.Journey;

namespace ImmersiveAI.Core.Tests;

public class JourneyLogTests
{
    private static JourneyLog SampleRoad()
    {
        var log = new JourneyLog();
        var odrimir = log.BeginVisit("Odrimir", JourneyVisit.Kinds.Village, 100.0, "Spring 3, Year 1084");
        odrimir.BoughtValue = 250;
        odrimir.BoughtNotable.Add("Grain ×20");
        log.CloseOpenVisit(100.1);

        var ortysia = log.BeginVisit("Ortysia", JourneyVisit.Kinds.Town, 102.0, "Spring 5, Year 1084");
        ortysia.SoldValue = 900;
        ortysia.SoldNotable.Add("Wool ×24");
        ortysia.SoldNotable.Add("Hides ×11");
        JourneyVisit.AddCounted(ortysia.Recruited, "Vlandian Recruit", 2);
        ortysia.PrisonersSold = 5;
        log.CloseOpenVisit(102.5);
        return log;
    }

    [Fact]
    public void Visits_OpenCloseAndRoadBucket()
    {
        var log = new JourneyLog();
        log.BeginVisit("Ortysia", JourneyVisit.Kinds.Town, 10.0, "d");
        Assert.NotNull(log.OpenVisit);

        // A caravan met after leaving lands in a road bucket; entering a town closes it.
        log.CloseOpenVisit(10.4);
        Assert.Null(log.OpenVisit);
        var road = log.CurrentOrRoad(10.8, "d2");
        Assert.Equal(JourneyLog.RoadPlace, road.Place);
        log.BeginVisit("Odrimir", JourneyVisit.Kinds.Village, 11.0, "d3");
        Assert.Equal(3, log.Visits.Count);
        Assert.True(road.LeaveDay >= 0);
    }

    [Fact]
    public void Visits_PruneKeepsTheFreshest()
    {
        var log = new JourneyLog();
        for (int i = 0; i < JourneyLog.MaxVisitsKept + 5; i++)
        {
            log.BeginVisit("Stop " + i, JourneyVisit.Kinds.Town, i, "d");
            log.CloseOpenVisit(i + 0.5);
        }
        Assert.Equal(JourneyLog.MaxVisitsKept, log.Visits.Count);
        Assert.Equal("Stop 16", log.Visits[^1].Place);
    }

    [Fact]
    public void AddCounted_MergesTheSameKindOfMan()
    {
        var lines = new List<string>();
        JourneyVisit.AddCounted(lines, "Vlandian Recruit", 2);
        JourneyVisit.AddCounted(lines, "Sturgian Warrior", 1);
        JourneyVisit.AddCounted(lines, "Vlandian Recruit", 3);
        Assert.Equal(2, lines.Count);
        Assert.Equal("Vlandian Recruit ×5", lines[0]);
        Assert.Equal("Sturgian Warrior", lines[1]);
    }

    [Fact]
    public void Quests_TakenResolvedAndDeduped()
    {
        var log = new JourneyLog();
        log.NoteQuestTaken("An Escort for the Caravan", "Lucon", 100, "Spring 3", 14);
        log.NoteQuestTaken("An Escort for the Caravan", "Lucon", 100, "Spring 3", 14); // re-fired start
        Assert.Single(log.OpenQuests);

        log.NoteQuestResolved("An Escort for the Caravan", "Lucon", JourneyText.Outcomes.Timeout, 115);
        Assert.Empty(log.OpenQuests);
        Assert.Single(log.ResolvedQuests);
        Assert.Contains("time ran out", log.ResolvedQuests[0].Outcome);

        // A quest never seen taken still gets its settled line.
        log.NoteQuestResolved("Family Feud", "Radagos", JourneyText.Outcomes.Success, 116);
        Assert.Equal(2, log.ResolvedQuests.Count);
    }

    [Fact]
    public void Text_OldStopsOneLine_FreshStopDetailed()
    {
        var block = JourneyText.SituationBlock(SampleRoad(), 103.0);
        Assert.Contains("The road we have ridden of late", block);
        // The older stop: one line with its values.
        Assert.Contains("- In the village of Odrimir (Spring 3, Year 1084, we stayed an hour or two): bought for 250 denars.", block);
        // The freshest: the detailed paragraph.
        Assert.Contains("Our latest stop — in the town of Ortysia, Spring 5, Year 1084, where we stayed half a day.", block);
        Assert.Contains("We sold goods worth 900 denars (Wool ×24, Hides ×11 the chief of it).", block);
        Assert.Contains("We took on Vlandian Recruit ×2.", block);
        Assert.Contains("5 captives were sold to the ransom broker.", block);
    }

    [Fact]
    public void Text_TasksCarryDaysAndSettledReasons()
    {
        var log = SampleRoad();
        log.NoteQuestTaken("An Escort for the Caravan", "Lucon", 100, "Spring 3, Year 1084", 14);
        log.NoteQuestResolved("Family Feud", "Radagos", JourneyText.Outcomes.Betrayal, 101);

        var block = JourneyText.SituationBlock(log, 105.0);
        Assert.Contains("Tasks we carry:", block);
        Assert.Contains("'An Escort for the Caravan' for Lucon (taken Spring 3, Year 1084; 14 days given, about 9 remain)", block);
        Assert.Contains("Lately settled:", block);
        Assert.Contains("'Family Feud' for Radagos — failed — and by our own broken word at that.", block);
    }

    [Fact]
    public void Text_EmptyJournalSaysNothing_AndEmptyRoadBucketsAreNoise()
    {
        Assert.Equal(string.Empty, JourneyText.SituationBlock(new JourneyLog(), 10));

        var log = new JourneyLog();
        log.CurrentOrRoad(10, "d"); // a road bucket with no doings
        log.CloseOpenVisit(10.1);
        Assert.Equal(string.Empty, JourneyText.SituationBlock(log, 11));
    }

    [Fact]
    public void Journal_SavesAndLoads()
    {
        var path = Path.Combine(Path.GetTempPath(), "iai_journey_" + Guid.NewGuid().ToString("N"), "_journey.json");
        try
        {
            var log = SampleRoad();
            log.NoteQuestTaken("An Escort for the Caravan", "Lucon", 100, "Spring 3", 14);
            log.SaveTo(path);

            var loaded = JourneyLog.LoadFrom(path);
            Assert.Equal(2, loaded.Visits.Count);
            Assert.Equal("Ortysia", loaded.Visits[^1].Place);
            Assert.Equal(900, loaded.Visits[^1].SoldValue);
            Assert.Single(loaded.OpenQuests);

            Assert.Empty(JourneyLog.LoadFrom(path + ".missing").Visits);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); } catch { }
        }
    }
}
