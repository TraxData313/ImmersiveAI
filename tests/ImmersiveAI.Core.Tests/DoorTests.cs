using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Doors;
using ImmersiveAI.Core.Initiation;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Nights;
using ImmersiveAI.Core.Prompts;
using Xunit;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// THE DOORS (2026.08.15) — her own written reasons, the spiral the world lays down, and the one
/// verb collision that would have made her forgive at the exact moment she meant to take offence.
/// </summary>
public class DoorTests
{
    private static List<DoorReason> Fresh() => new List<DoorReason>();

    // ------------------------------ her own hand ------------------------------

    [Fact]
    public void SheWritesWhyAndWhatWouldAnswerIt()
    {
        var reasons = Fresh();
        Assert.True(DoorReasons.SetDown(reasons,
            "You gave Ira a jewel in front of the whole house and I heard of it before you told me",
            "That you tell me such things yourself, before the servants do", 10, out _));

        Assert.True(DoorReasons.AnyStanding(reasons));
        Assert.Equal(1, DoorReasons.OpenCount(reasons));
        Assert.Contains("before the servants do", reasons[0].Opens);
        Assert.False(reasons[0].LaidByTheWorld);
    }

    [Fact]
    public void SheMayNotKnowTheWayBack_AndThatIsAnAnswer()
    {
        var reasons = Fresh();
        Assert.True(DoorReasons.SetDown(reasons, "You went to her the night I lost the child", "", 10, out _));
        Assert.Equal(string.Empty, reasons[0].Opens);

        // The page says so plainly rather than pretending there is a combination to find.
        var page = DoorText.Page("Mizam", reasons);
        Assert.Contains("they have not said. They may not know", page);
    }

    [Fact]
    public void OnlyHerOwnHandLaysOneToRest_AndTheNoteIsKept()
    {
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "You went to her the night I lost the child", "", 10, out _);

        Assert.True(DoorReasons.Settle(reasons, "the night I lost the child",
            "he sat with me and did not once excuse himself", 20, out _));
        Assert.False(DoorReasons.AnyStanding(reasons));
        Assert.Contains("did not once excuse himself", reasons[0].SettledNote);
        Assert.Equal(20, reasons[0].SettledDay);
    }

    [Fact]
    public void ARestatementFindsTheLineItMeans_EvenInAnInflectedTongue()
    {
        // A model never names back its own sentence; it paraphrases. And Anton plays in Bulgarian,
        // where word-for-word comparison calls one word in two endings two strangers. Both cases
        // must land, or the settle verb silently does nothing — a woman who forgave and was not
        // recorded as forgiving.
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "Ти избереш нея пред мене всеки път когато има избор", "", 10, out _);

        Assert.NotNull(DoorReasons.Find(reasons, "избереш нея пред мене", standingOnly: true));
        Assert.NotNull(DoorReasons.Find(reasons, "че ме избираш нея пред мене всеки път", standingOnly: true));
    }

    [Fact]
    public void TheCapBoundsWhatStandsOpen_NeverWhatSheMayFeel()
    {
        var reasons = Fresh();
        for (int i = 0; i < DoorReasons.MaxStandingOpen; i++)
            Assert.True(DoorReasons.SetDown(reasons, $"the {i}th separate and quite distinct grievance of mine", "", 10, out _));

        Assert.False(DoorReasons.SetDown(reasons, "and one more entirely unrelated wound besides", "", 10, out var full));
        Assert.Contains("unanswered", full);

        // Settling one makes room again — the cap is on what is UNANSWERED, not on her lifetime.
        Assert.True(DoorReasons.Settle(reasons, "the 0th separate and quite distinct grievance of mine", "it passed", 20, out _));
        Assert.True(DoorReasons.SetDown(reasons, "and one more entirely unrelated wound besides", "", 21, out _));
    }

    [Fact]
    public void APruneNeverDropsAWoundNobodyAnswered()
    {
        var reasons = Fresh();
        for (int i = 0; i < DoorReasons.MaxKept + 4; i++)
            reasons.Add(new DoorReason { Text = $"settled matter number {i}", Settled = true, SettledDay = i });
        reasons.Add(new DoorReason { Text = "the one thing nobody ever answered", SetDownDay = 1 });

        DoorReasons.Prune(reasons);

        Assert.True(reasons.Count <= DoorReasons.MaxKept);
        Assert.Contains(reasons, r => r.Text == "the one thing nobody ever answered");
        // And the OLDEST settled were the ones that faded.
        Assert.DoesNotContain(reasons, r => r.Text == "settled matter number 0");
    }

    [Fact]
    public void AReviseTakesTwoStrings_AndRefusesToRewriteAThingToItself()
    {
        // THE BUG THIS PINS (found by review 2026.08.15, fixed 2026.08.16): the resolver handed the
        // SAME field as both "which one I mean" and "the new wording", so a revise could only ever
        // rewrite a line to itself — and then report that it had reworded it. A silent lie about
        // what she just decided is the one failure this system cannot carry.
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "You gave the jewel to Ira before the whole house", "tell me first", 10, out _);

        Assert.True(DoorReasons.Revise(reasons, "the jewel you gave Ira",
            "It is not the jewel. It is that I heard of it from other mouths.", "", out _));
        Assert.Equal("It is not the jewel. It is that I heard of it from other mouths.", reasons[0].Text);
        Assert.Equal("tell me first", reasons[0].Opens);   // untouched: she reworded only the matter

        // Nothing new to say is not a revise, whatever it reports. The misgivings' own revise has
        // refused a blanking since it shipped; this one used to succeed and announce a rewording.
        Assert.False(DoorReasons.Revise(reasons, "heard of it from other mouths", "", "", out var nothing));
        Assert.Contains("no new words", nothing);

        // The road back alone is a real revise — the matter stands, what would answer it has moved.
        Assert.True(DoorReasons.Revise(reasons, "heard of it from other mouths", "", "say it to me yourself", out _));
        Assert.Equal("say it to me yourself", reasons[0].Opens);
        Assert.Equal("It is not the jewel. It is that I heard of it from other mouths.", reasons[0].Text);
    }

    [Fact]
    public void AReviseWhoseHandsCameSwappedIsStillRead()
    {
        // The new wording is what a model has most to say, so it lands in the first field going —
        // the misgivings' settle verb did exactly this on every call it made. The guard is narrow
        // on purpose: it swaps ONLY when the matter names nothing and the other field names one.
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "You left me in Ortysia through the whole of the winter", "", 10, out _);

        const string wording = "Left through the winter, and not one letter came";
        Assert.True(DoorReasons.HandsCameSwapped(reasons, wording, "left me in Ortysia the whole winter",
            standingOnly: false));

        // Read the right way round it names nothing to swap.
        Assert.False(DoorReasons.HandsCameSwapped(reasons, "left me in Ortysia the whole winter", wording,
            standingOnly: false));
    }

    // ------------------------------ the verb table ------------------------------

    [Fact]
    public void CloseMeansShutTheDoor_NotLayItToRest()
    {
        // THE COLLISION, and it is the sharpest edge in this file. For a misgiving, "close" means
        // laying the matter to rest. For a DOOR it plainly means shutting it. Sharing the
        // misgivings' table would have made a woman forgive at the exact moment she meant to take
        // offence — silently, and in the most sensitive feature in the mod.
        Assert.Equal(DoorReasons.ActSetDown, DoorReasons.CanonicalDeed("close"));
        Assert.Equal(DoorReasons.ActSetDown, DoorReasons.CanonicalDeed("closed"));
        Assert.Equal(DoorReasons.ActSetDown, DoorReasons.CanonicalDeed("shut"));
        Assert.Equal(CourtshipMisgivings.ActSettle, CourtshipMisgivings.CanonicalAction("close"));

        // And the mirror of it.
        Assert.Equal(DoorReasons.ActSettle, DoorReasons.CanonicalDeed("open"));
        Assert.Equal(DoorReasons.ActSettle, DoorReasons.CanonicalDeed("forgive"));
    }

    [Fact]
    public void HonestSynonymsLandWhereSheMeantThem()
    {
        Assert.Equal(DoorReasons.ActSettle, DoorReasons.CanonicalDeed("resolve"));
        Assert.Equal(DoorReasons.ActSettle, DoorReasons.CanonicalDeed("Lay To Rest"));
        Assert.Equal(DoorReasons.ActSetDown, DoorReasons.CanonicalDeed("set-down"));
        Assert.Equal(DoorReasons.ActRelease, DoorReasons.CanonicalDeed("strike_out"));
        Assert.Equal(DoorReasons.ActRevise, DoorReasons.CanonicalDeed("reword"));
        Assert.Equal(DoorReasons.ActReopen, DoorReasons.CanonicalDeed("take_up"));

        // Nothing close came: the caller must SAY so, never do nothing in silence.
        Assert.Equal(string.Empty, DoorReasons.CanonicalDeed("ponder"));
        Assert.Equal(string.Empty, DoorReasons.CanonicalDeed(""));
    }

    [Fact]
    public void TheSwappedHandsAreCaught()
    {
        // Live on the misgivings, a model did this on EVERY settle it made: the answer went into
        // the matter's field and the matter into the note.
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "You went to her the night I lost the child", "", 10, out _);

        Assert.True(DoorReasons.HandsCameSwapped(reasons,
            matter: "he sat with me until morning and excused nothing",
            note: "the night I lost the child"));

        // And it is narrow: when the matter DOES name one, nothing is swapped.
        Assert.False(DoorReasons.HandsCameSwapped(reasons,
            matter: "the night I lost the child",
            note: "he sat with me until morning"));
    }

    // ------------------------------ where the door stands ------------------------------

    [Fact]
    public void HerSeasonIsNeverReportedAsAWound()
    {
        // It comes first, always. A wife in her custom days whose heart is perfectly whole would
        // otherwise be reported as cold, which is both false and cruel.
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "something real", "", 10, out _);

        Assert.Equal(DoorStanding.HerSeason,
            DoorReasons.StandingOf(reasons, herSeasonIsUpon: true, HeartBand.Love, BondKind.Wed));
        Assert.Equal(DoorStanding.HerWord,
            DoorReasons.StandingOf(reasons, herSeasonIsUpon: false, HeartBand.Love, BondKind.Wed));
    }

    [Fact]
    public void ColdnessIsToldPlainly_AndNeverDressedAsAGrievanceSheNeverMade()
    {
        Assert.Equal(DoorStanding.Coldness,
            DoorReasons.StandingOf(Fresh(), false, HeartBand.Neutral, BondKind.Wed));
        Assert.Equal(DoorStanding.Open,
            DoorReasons.StandingOf(Fresh(), false, HeartBand.Intrigued, BondKind.Wed));

        // A lover's door closes far sooner, because nothing holds her but the feeling.
        Assert.Equal(DoorStanding.Coldness,
            DoorReasons.StandingOf(Fresh(), false, HeartBand.Intrigued, BondKind.Lover));
        Assert.Equal(DoorStanding.Open,
            DoorReasons.StandingOf(Fresh(), false, HeartBand.Like, BondKind.Lover));

        var section = DoorText.ColdnessSection("Mizam", BondKind.Wed);
        Assert.Contains("I have set down no reason for it", section);
        Assert.Contains("will not manufacture a grievance", section);
    }

    // ------------------------------ the spiral ------------------------------

    [Fact]
    public void ADutyNightLaysSomethingDownWhetherOrNotSheIsInATalk()
    {
        // The guardrail: a duty night that costs nothing turns the whole design into a farm. This
        // must not depend on a model reaching for a tool.
        var reasons = Fresh();
        var card = DoorText.DrawDutyReason("night-1");
        Assert.True(DoorReasons.LayDownByTheWorld(reasons, card.Text, card.Opens, 10));

        Assert.True(DoorReasons.AnyStanding(reasons));
        Assert.True(reasons[0].LaidByTheWorld);
        Assert.Equal(1, DoorReasons.LaidByTheWorldCount(reasons));
    }

    [Fact]
    public void TheDutyDeckIsStablePerNight_AndSpreadAcrossNights()
    {
        // Stable, so a retry lays down the same thing rather than a second grievance.
        Assert.Equal(DoorText.DrawDutyReason("n-7").Text, DoorText.DrawDutyReason("n-7").Text);

        // And spread, so a marriage that goes this way does not read as one sentence repeated.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 60; i++) seen.Add(DoorText.DrawDutyReason("night-" + i).Text);
        Assert.True(seen.Count >= 3, $"the duty deck collapsed to {seen.Count} cards");
    }

    [Fact]
    public void TheDeckIsATemplate_NeverTheStoredObject()
    {
        // Settling "the" reason must not quietly settle it for every future marriage.
        var first = DoorText.DrawDutyReason("same");
        var second = DoorText.DrawDutyReason("same");
        Assert.NotSame(first, second);
        first.Settled = true;
        Assert.False(second.Settled);
        Assert.DoesNotContain(DoorText.DutyNightReasons, r => r.Settled);
    }

    [Fact]
    public void TheDutyNightHasNoStoryPathAtAll()
    {
        // The no-narrator rule is enforced by the KIND, not by prompt words. A duty night can never
        // be picked up by the chronicler's retry sweep and can never be "storied".
        var night = new NightRecord
        {
            Kind = NightKind.Duty,
            StoryWanted = true,          // even if something set this by mistake
            Story = string.Empty,
        };
        Assert.False(night.NeedsStory(3));
        Assert.False(night.IsStoried);

        night.Story = "a chronicler wrote something anyway";
        Assert.False(night.IsStoried);
    }

    [Fact]
    public void ThePlayersDutyLinesAreAntonsOwn_AndAreNotBrightened()
    {
        Assert.Equal(3, NightText.DutyNightLines.Count);
        Assert.Contains("The duty of the marriage was kept. She said nothing.", NightText.DutyNightLines);
        Assert.Contains("She did not refuse you. She was turned to the wall before you rose.", NightText.DutyNightLines);
        Assert.Contains("What a wife owes, she gave. What she once gave unasked, she did not.", NightText.DutyNightLines);

        Assert.Equal(NightText.DrawDutyLine("x"), NightText.DrawDutyLine("x"));
    }

    [Fact]
    public void HerDutyBeatIsFlatEnoughToLeaveHerEverything()
    {
        var beat = NightText.DutyBeat("Mizam");
        Assert.Contains("I did what a wife does", beat);
        Assert.Contains("We did not speak of it", beat);
        // It is a night beat like any other, so the thread and the memory already know it.
        Assert.True(NightText.IsNightBeat(beat));
        // And it carries no name, because there is none.
        Assert.False(NightText.IsNamedNightBeat(beat));
    }

    [Fact]
    public void ADutyNightIsCountedApartFromTheNightsHeWasWelcome()
    {
        var marks = new List<NightMark>
        {
            new NightMark { GameDay = 90, Kind = NightKind.Together },
            new NightMark { GameDay = 91, Kind = NightKind.Duty },
            new NightMark { GameDay = 92, Kind = NightKind.Duty },
        };
        var reckoning = NightText.BuildReckoning(marks, 93);

        Assert.Contains("he came to me once", reckoning);   // the duty nights are NOT folded in
        Assert.Contains("my door was shut and he came to me even so", reckoning);
        Assert.Contains("twice", reckoning);
    }

    // ------------------------------ what she and the player read ------------------------------

    [Fact]
    public void HerSheetCarriesTheAnchor_SoNeitherSulkingNorPretendingHasAnywhereToHide()
    {
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "you went to her that night", "time, and you not doing it again", 10, out _);
        var section = DoorText.Section("Mizam", reasons, doorIsShut: true);

        Assert.Contains("My door is shut to Mizam", section);
        Assert.Contains("you went to her that night", section);
        Assert.Contains("What would answer it", section);

        // Both halves of the anchor: it stays shut while something stands, AND she does not invent
        // a fresh grievance to keep it shut once nothing does.
        Assert.Contains("my door stays shut", section);
        Assert.Contains("nothing of mine bars the door", section);
        Assert.Contains("never for a warm word alone", section);

        // And it is hers to tend, exactly as the misgivings are.
        Assert.Contains("This list is mine", section);
    }

    [Fact]
    public void NothingEverWrittenIsNoSectionAtAll()
    {
        Assert.Equal(string.Empty, DoorText.Section("Mizam", Fresh(), doorIsShut: false));
        Assert.Equal(string.Empty, DoorText.Page("Mizam", Fresh()));
        Assert.Equal(string.Empty, DoorText.StandingLabel(Fresh()));
    }

    [Fact]
    public void TheDuskLineCarriesHerOwnFirstWords()
    {
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "You gave Ira a jewel in front of the whole house", "", 10, out _);

        var line = DoorText.DuskLine(DoorStanding.HerWord, reasons);
        Assert.Contains("her door is shut", line);
        Assert.Contains("gave Ira a jewel", line);

        // The season keeps its own wording EXACTLY — the evening's own code still matches on it.
        Assert.Equal("the custom of women is upon her", DoorText.DuskLine(DoorStanding.HerSeason, reasons));
        Assert.Equal(string.Empty, DoorText.DuskLine(DoorStanding.Open, reasons));
    }

    [Fact]
    public void ThePageTellsThePlayerThereIsNoButton()
    {
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "you went to her that night", "time, and you not doing it again", 10, out _);
        var page = DoorText.Page("Mizam", reasons);

        Assert.Contains("No coin settles any of this and no button opens it", page);
        Assert.Contains("they will lay it to rest themselves — or they will not", page);
    }

    // ------------------------------ the morning after ------------------------------

    [Fact]
    public void AFreshWoundMovesHerAtOnce_AndStopsBeingNewsWithinTwoDays()
    {
        Assert.Equal(InitiationScorer.WoundSpikeAtOnce, InitiationScorer.WoundSpike(0), 3);
        Assert.True(InitiationScorer.WoundSpike(12) > InitiationScorer.WoundSpike(24));
        Assert.Equal(0, InitiationScorer.WoundSpike(InitiationScorer.WoundFreshHours));
        Assert.Equal(0, InitiationScorer.WoundSpike(999));
        Assert.Equal(0, InitiationScorer.WoundSpike(-1));   // nothing fresh

        // It must be able to move a bond that has almost no pull of its own — the woman who most
        // needs to say something is very often the one who has been talked to least.
        double quiet = InitiationScorer.Pull(storyRichness: 2, relation: 5, daysSinceLastTalk: 30);
        Assert.True(InitiationScorer.WoundSpike(1) > quiet);
    }

    [Fact]
    public void AWoundedSoulTakesTheDaysVisit_ButNeverAddsOneToIt()
    {
        // The group-total law: a higher pull only pushes the union nearer 1, and the day's
        // expectation stays rate x unionPull <= rate. She becomes the one whose visit it is; she
        // does not create an extra visit.
        var calm = new List<double> { 0.2, 0.2, 0.2 };
        var wounded = new List<double> { 0.2, 0.2, InitiationScorer.WoundSpike(0) };

        double calmHourly = InitiationScorer.GroupHourlyChance(1.0, InitiationScorer.UnionPull(calm));
        double woundedHourly = InitiationScorer.GroupHourlyChance(1.0, InitiationScorer.UnionPull(wounded));

        Assert.True(woundedHourly > calmHourly);
        Assert.True(woundedHourly * 24.0 <= 1.0 + 1e-9);
    }

    [Fact]
    public void OneSpikePerWound_SpentTheMomentSheGoes()
    {
        var memory = new NpcMemory { FreshWoundDay = 91.0 };

        memory.NoteOutreach(91.2);
        Assert.Equal(-1, memory.FreshWoundDay);

        // And weighing it and letting it pass is an answer too — she is not asked twice.
        memory.FreshWoundDay = 95.0;
        memory.NoteOutreachConsidered(95.1);
        Assert.Equal(-1, memory.FreshWoundDay);
    }

    // ------------------------------ where it is kept ------------------------------

    [Fact]
    public void TheDoorRidesHerMemory_SoASaveRewindsIt()
    {
        var memory = new NpcMemory();
        Assert.NotNull(memory.DoorReasons);
        Assert.Empty(memory.DoorReasons);   // an old file loads an open door

        DoorReasons.SetDown(memory.DoorReasons, "something real", "", 10, out _);
        Assert.True(DoorReasons.AnyStanding(memory.DoorReasons));
    }

    [Fact]
    public void TheSheetPutsTheDoorLastBeforeTheMoment()
    {
        var persona = new NpcPersona { Name = "Sibylla" };
        var reasons = Fresh();
        DoorReasons.SetDown(reasons, "you went to her that night", "", 10, out _);
        persona.DoorTerms = DoorText.Section("Mizam", reasons, doorIsShut: true);

        var system = new PromptBuilder().Build(persona, new NpcMemory { Summary = "We were wed." },
            "In the hall." + PromptBuilder.MeetingSeparator + "And now Mizam comes to me.",
            "Mizam", "Hello")[0].Content;

        // A shut door is the most present fact of an evening; it must not be buried above her
        // memory of him, and the arrival still lands last.
        Assert.True(system.IndexOf("What Mizam is to me", StringComparison.Ordinal)
                  < system.IndexOf("My door is shut to Mizam", StringComparison.Ordinal));
        Assert.True(system.IndexOf("My door is shut to Mizam", StringComparison.Ordinal)
                  < system.IndexOf("And now Mizam comes to me.", StringComparison.Ordinal));
    }
}
