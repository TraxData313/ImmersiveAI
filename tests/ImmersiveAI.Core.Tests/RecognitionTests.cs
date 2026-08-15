using System.Collections.Generic;
using ImmersiveAI.Core.Births;
using Xunit;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// RECOGNITION (2026.08.15) — blood is the game's and untouched; honor is ours, and honor is
/// nothing but what has been SAID.
/// </summary>
public class RecognitionTests
{
    private static BirthRecord Child(string name, bool inMarriage, Acknowledgement owned) =>
        new BirthRecord
        {
            MotherName = "Sibylla",
            FatherName = "Mizam",
            BornInMarriage = inMarriage,
            Owned = owned,
            Children = { new BirthChild { HeroId = "kid_" + name, Name = name } },
        };

    [Fact]
    public void AnOldRecordIsNeverQuietlyDisowned()
    {
        // THE SAFETY THAT MATTERS MOST HERE. Every child in every existing campaign was written
        // before this question existed, and an update that read them all as unacknowledged would be
        // the single most upsetting bug this feature could possibly ship.
        var old = new BirthRecord();
        Assert.Equal(Acknowledgement.NeverArose, old.Owned);
        Assert.True(old.IsOwnedBeforeTheWorld);
        Assert.False(old.AwaitsTheName);

        // And it holds even if the value fails to load at all: NeverArose IS the zero.
        Assert.Equal(0, (int)default(Acknowledgement));
    }

    [Fact]
    public void AChildOfTheMarriageIsHisWithoutAnyoneAsking()
    {
        var born = Child("Ira", inMarriage: true, Acknowledgement.NeverArose);
        Assert.True(born.IsOwnedBeforeTheWorld);
        Assert.False(born.AwaitsTheName);
    }

    [Fact]
    public void OnlyAnExplicitWithholdingReadsAsUnowned()
    {
        var said = Child("Ira", inMarriage: false, Acknowledgement.Given);
        var unsaid = Child("Tulag", inMarriage: false, Acknowledgement.Withheld);

        Assert.True(said.IsOwnedBeforeTheWorld);
        Assert.False(said.AwaitsTheName);

        Assert.False(unsaid.IsOwnedBeforeTheWorld);
        Assert.True(unsaid.AwaitsTheName);
    }

    [Fact]
    public void AWithheldChildAppearsOnlyInHerLine_AndTheWorldDoesNotSayWhose()
    {
        var line = BirthText.HouseLine(new List<BirthText.HouseChild>
        {
            new BirthText.HouseChild { Name = "Ira", MotherName = "Sibylla", InMarriage = true, Owned = true },
            new BirthText.HouseChild { Name = "Tulag", MotherName = "Ira the younger", InMarriage = false, Owned = true },
            new BirthText.HouseChild { Name = "Nadea", MotherName = "Liena", InMarriage = false, Owned = false },
        }, "Mizam");

        Assert.Contains("Ira, born of his marriage", line);
        Assert.Contains("Tulag he has owned before the world as his, by Ira the younger", line);

        // The unowned one is spoken of ONLY as hers. That politeness is what makes a late owning
        // heavy: it is not a name, it is a taking-in, in front of everyone who never said it.
        Assert.Contains("Liena is raising a child he has never owned before anyone", line);
        Assert.DoesNotContain("Nadea", line);
        Assert.Contains("he has said nothing of it", line);

        // AND IT IS IN THE THIRD PERSON THROUGHOUT. This line rides the sheets of the women of his
        // household, where "I" means HER — written in his own first person it made every wife and
        // lover claim his children as her own (self-review, 2026.08.15).
        Assert.Contains("Of Mizam's house", line);
        Assert.DoesNotContain("I have owned", line);
        Assert.DoesNotContain("my marriage", line);
    }

    [Fact]
    public void AHouseWithNothingToExplainSaysNothing()
    {
        Assert.Equal(string.Empty, BirthText.HouseLine(null, "Mizam"));
        Assert.Equal(string.Empty, BirthText.HouseLine(new List<BirthText.HouseChild>(), "Mizam"));
    }

    [Fact]
    public void SheIsToldTheFactAndNeverHowToTakeIt()
    {
        var withheld = BirthText.MotherNameBeat("Mizam", "Nadea", given: false, withFeast: false, late: false);
        Assert.Contains("has not owned Nadea before anyone", withheld);
        Assert.Contains("none of that is the same as his having said so", withheld);
        Assert.DoesNotContain("I am ashamed", withheld);
        Assert.DoesNotContain("it hurts", withheld);

        var quiet = BirthText.MotherNameBeat("Mizam", "Nadea", given: true, withFeast: false, late: false);
        Assert.Contains("quietly, with no hall", quiet);

        var late = BirthText.MotherNameBeat("Mizam", "Nadea", given: true, withFeast: false, late: true);
        Assert.Contains("after all this time", late);
        Assert.Contains("It is said now", late);
    }

    // ------------------------------ the child who knows its own story ------------------------------

    [Fact]
    public void AChildKeepsItsOwnBeginning_AndNeverItsMothersVoice()
    {
        var beat = BirthText.ChildBornBeat("Sibylla", "Mizam", "the town of Onira", owned: true);
        Assert.True(BirthText.IsOwnBeginningBeat(beat));
        Assert.Contains("I was born to Sibylla", beat);
        Assert.Contains("the world was told so", beat);

        // The privacy fence, one person further than the tool's: the hour is the parents' and the
        // child is given the facts of its day, never her first person.
        Assert.DoesNotContain(BirthText.HourAccountMark, beat);

        var silent = BirthText.ChildBornBeat("Sibylla", "Mizam", "the town of Onira", owned: false);
        Assert.Contains("Nothing was said of it before anyone", silent);
    }

    [Fact]
    public void TheSilenceIsRecordedInTheChildTooAndSoIsTheDayItEnded()
    {
        // It will still be there when the child is grown enough to have something to say about it,
        // and by then the compression is ITS choice to keep or let fade — which is the whole idea.
        var late = BirthText.ChildNameBeat("Mizam", late: true);
        Assert.Contains("stood up and owned me before the world", late);
        Assert.Contains("I know exactly how long", late);

        var atOnce = BirthText.ChildNameBeat("Mizam", late: false);
        Assert.Contains("owned me before the world and gave me his name", atOnce);
        Assert.DoesNotContain("I know exactly how long", atOnce);
    }

    [Fact]
    public void TheMarksAreTheirOwn_SoTheThreadCanTellThemApart()
    {
        Assert.True(BirthText.IsNameBeat(BirthText.ChildNameBeat("Mizam", late: false)));
        Assert.True(BirthText.IsNameBeat(BirthText.MotherNameBeat("Mizam", "Ira", true, false, false)));
        Assert.False(BirthText.IsNameBeat(BirthText.ChildBornBeat("Sibylla", "Mizam", "", true)));
    }
}
