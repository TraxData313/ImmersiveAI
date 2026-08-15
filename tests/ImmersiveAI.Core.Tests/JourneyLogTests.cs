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
    public void ReEnteringTheSamePlace_ResumesTheStay_InsteadOfMintingAnEmptyLatestStop()
    {
        // The Onira bug (2026.08.08 playtest): a save loaded inside a town re-fires the entered
        // event at the very instant of the recorded leave — the empty re-entry visit then stole
        // the "latest stop, detailed" slot and demoted the real trade to a single line.
        var log = new JourneyLog();
        var onira = log.BeginVisit("Onira", JourneyVisit.Kinds.Town, 107.9, "Autumn 10");
        onira.SoldValue = 3677; onira.PrisonersSold = 13;
        log.CloseOpenVisit(109.2);
        var again = log.BeginVisit("Onira", JourneyVisit.Kinds.Town, 109.2, "Autumn 12");

        Assert.Same(onira, again);           // the same stay, resumed
        Assert.True(again.LeaveDay < 0);     // and open once more
        Assert.Single(log.Visits);

        log.CloseOpenVisit(109.3);
        var block = JourneyText.SituationBlock(log, 109.4);
        Assert.Contains("Our latest stop — in the town of Onira", block);
        Assert.Contains("3,677 denars", block);

        // Days later is a genuinely new visit.
        var later = log.BeginVisit("Onira", JourneyVisit.Kinds.Town, 115.0, "Autumn 18");
        Assert.NotSame(onira, later);
        Assert.Equal(2, log.Visits.Count);
    }

    [Fact]
    public void LoadFrom_HealsAJournalSplitByReEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), "iai_journey_" + Guid.NewGuid().ToString("N"), "_journey.json");
        try
        {
            // A file written before the resume rule: the real visit and its empty same-instant twin.
            var split = new JourneyLog();
            var real = new JourneyVisit
            {
                Place = "Onira", Kind = JourneyVisit.Kinds.Town, ArriveDay = 107.9, ArriveText = "Autumn 10",
                LeaveDay = 109.2, SoldValue = 3677, BoughtValue = 964, PrisonersSold = 13,
            };
            real.SoldNotable.Add("Iron Ore ×11");
            var twin = new JourneyVisit
            {
                Place = "Onira", Kind = JourneyVisit.Kinds.Town, ArriveDay = 109.2, ArriveText = "Autumn 12",
                LeaveDay = 109.2,
            };
            split.Visits.Add(real); split.Visits.Add(twin);
            split.SaveTo(path);

            var healed = JourneyLog.LoadFrom(path);
            Assert.Single(healed.Visits);
            Assert.Equal(3677, healed.Visits[0].SoldValue);
            Assert.Equal(13, healed.Visits[0].PrisonersSold);
            Assert.Contains("Iron Ore ×11", healed.Visits[0].SoldNotable);
            Assert.Contains("3,677 denars", JourneyText.SituationBlock(healed, 110));
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Beats_SpeakTheStopAndTheTasks_AndWearTheirMarkers()
    {
        var log = SampleRoad();
        var stop = log.Visits[^1];
        var beat = JourneyText.StopBeat(stop);
        Assert.True(JourneyText.IsJourneyBeat(beat));
        Assert.StartsWith(JourneyText.StopBeatMark, beat);
        // The beat keeps the DETAIL — chief goods by name, men by kind — so the freshest stop
        // offers a conversation-opener without a dig through deeper memory.
        Assert.Contains("In the town of Ortysia (Spring 5, Year 1084, we stayed half a day).", beat);
        Assert.Contains("We sold goods worth 900 denars (Wool ×24, Hides ×11 the chief of it).", beat);
        Assert.Contains("We took on Vlandian Recruit ×2.", beat);
        Assert.Contains("5 captives were sold to the ransom broker.", beat);

        var taken = log.NoteQuestTaken("An Escort for the Caravan", "Lucon", 100, "Spring 3", 14)!;
        var takenBeat = JourneyText.TaskTakenBeat(taken);
        Assert.True(JourneyText.IsJourneyBeat(takenBeat));
        Assert.Contains("'An Escort for the Caravan' for Lucon — 14 days given", takenBeat);

        // A re-fired start event must not earn a second beat.
        Assert.Null(log.NoteQuestTaken("An Escort for the Caravan", "Lucon", 100, "Spring 3", 14));

        var settled = log.NoteQuestResolved("An Escort for the Caravan", "Lucon", JourneyText.Outcomes.Timeout, 115)!;
        var settledBeat = JourneyText.TaskSettledBeat(settled);
        Assert.Contains("is settled — failed — the time ran out on us.", settledBeat);
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

    // ---- the item flood: a computed view that aliased persisted state -------------------------

    [Fact]
    public void AnOpenStopIsNotWrittenTwiceAndDoesNotGrowOnEveryRoundTrip()
    {
        // THE BUG (Anton, 2026.08.16): OpenVisit aliases a live element of Visits. Serialized, the
        // file carried that visit twice; on load, Newtonsoft populated the instance it already had
        // — the same visit — appending its piece-lists to themselves. Every save-then-load with a
        // stop OPEN doubled them. Every existing round-trip test closed its stops first, which is
        // exactly why the suite never saw it.
        var file = Path.Combine(Path.GetTempPath(), "iai_journey_" + Guid.NewGuid().ToString("N"), "_journey.json");

        var log = new JourneyLog();
        var visit = log.BeginVisit("Akkalat", JourneyVisit.Kinds.Town, 10.0, "Winter 12");
        JourneyVisit.AddCounted(visit.SoldNotable, "Silver Ore", 6);
        JourneyVisit.AddCounted(visit.SoldNotable, "Cow", 4);
        JourneyVisit.AddCounted(visit.BoughtNotable, "Barbed Arrows", 1);
        visit.SoldValue = 4802;
        Assert.NotNull(log.OpenVisit);          // the stop is OPEN — the whole point

        for (var round = 0; round < 4; round++)
        {
            log.SaveTo(file);
            log = JourneyLog.LoadFrom(file);
        }

        var back = log.Visits.Single();
        Assert.Equal(new[] { "Silver Ore ×6", "Cow ×4" }, back.SoldNotable.ToArray());
        Assert.Equal(new[] { "Barbed Arrows" }, back.BoughtNotable.ToArray());
        Assert.Equal(4802, back.SoldValue);
    }

    [Fact]
    public void AJournalAlreadyFloodedIsHealedOnLoad()
    {
        // Fixing the writer mends nothing already on disk, and Anton has a live campaign. An honest
        // list can never hold one NAME twice — every write merges through AddCounted — so a repeat
        // is proof of a replayed copy, and the first of them carries the true count.
        var file = Path.Combine(Path.GetTempPath(), "iai_journey_" + Guid.NewGuid().ToString("N"), "_journey.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, @"{
          ""Visits"": [{
            ""Place"": ""Akkalat"", ""Kind"": ""town"", ""ArriveDay"": 10.0, ""LeaveDay"": 10.5,
            ""SoldValue"": 4802,
            ""SoldNotable"": [""Silver Ore ×6"", ""Cow ×4"", ""Silver Ore ×6"", ""Cow ×4"", ""Silver Ore ×6""],
            ""BoughtNotable"": [""Barbed Arrows"", ""Barbed Arrows""],
            ""Recruited"": [], ""LeftInGarrison"": []
          }],
          ""Quests"": []
        }");

        var log = JourneyLog.LoadFrom(file);
        var visit = log.Visits.Single();

        // Dropped, never summed: summing would invent goods that never changed hands.
        Assert.Equal(new[] { "Silver Ore ×6", "Cow ×4" }, visit.SoldNotable.ToArray());
        Assert.Equal(new[] { "Barbed Arrows" }, visit.BoughtNotable.ToArray());
        Assert.Equal(4802, visit.SoldValue);
    }

    [Fact]
    public void TheHealLeavesAnHonestJournalExactlyAsItWas()
    {
        var log = new JourneyLog();
        var visit = log.BeginVisit("Odokh", JourneyVisit.Kinds.Town, 3.0, "Winter 5");
        JourneyVisit.AddCounted(visit.SoldNotable, "Wool", 24);
        JourneyVisit.AddCounted(visit.SoldNotable, "Salt", 19);
        JourneyVisit.AddCounted(visit.SoldNotable, "Wool", 6);      // merges by name, as it always has
        log.CloseOpenVisit(3.5);

        log.DropReplayedLines();
        Assert.Equal(new[] { "Wool ×30", "Salt ×19" }, log.Visits.Single().SoldNotable.ToArray());
    }
}
