using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class PersonaSparkTests
{
    private static PersonaSpark.Facts IlyaFacts() => new()
    {
        Name = "Ilya of the Boar's Hide",
        GenderWord = "woman",
        Age = 27,
        Station = "a Sturgian wanderer (a sellsword seeking work)",
        WherePhrase = "found in the tavern of Varcheg",
        Traits = "honest, daring, somewhat merciful",
        SpeechStyle = "terse northern speech, dry humor, never flowery",
        Backstory = "My father was a hunter who sold hides in Varcheg's market.",
        WorldText = "The world is harsh and medieval.",
    };

    [Fact]
    public void DrawCards_AlwaysHandsTwoDistinctCardsFromTheDeck()
    {
        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            var (first, second) = PersonaSpark.DrawCards(rng);
            Assert.NotEqual(first, second);
            Assert.Contains(first, PersonaSpark.Deck);
            Assert.Contains(second, PersonaSpark.Deck);
        }
    }

    [Fact]
    public void DrawIntensity_HitsEveryFace_AndRoughlyHonorsTheWeights()
    {
        var rng = new Random(11);
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < 3000; i++)
        {
            var face = PersonaSpark.DrawIntensity(rng);
            counts[face.Name] = counts.TryGetValue(face.Name, out var n) ? n + 1 : 1;
        }

        Assert.Equal(3, counts.Count);
        // MARKED (0.50) must dominate; VIVID (0.20) must be the rarest. Loose bounds — this
        // guards the weighting logic, not the RNG's exact arithmetic.
        Assert.True(counts["MARKED"] > counts["SUBTLE"]);
        Assert.True(counts["SUBTLE"] > counts["VIVID"]);
        Assert.True(counts["VIVID"] > 300); // ~600 expected; far above zero
    }

    [Fact]
    public void BuildPrompt_CarriesTheFactsTheCardsAndTheContract()
    {
        var prompt = PersonaSpark.BuildPrompt(
            IlyaFacts(), "an old wound that never healed", "a vanity", PersonaSpark.Intensities[2]);

        Assert.Contains("casting director", prompt);
        Assert.Contains("Ilya of the Boar's Hide — woman, about 27", prompt);
        Assert.Contains("found in the tavern of Varcheg", prompt);
        Assert.Contains("Her cast of mind, from the world's reckoning: honest, daring, somewhat merciful.", prompt);
        Assert.Contains("Her way of speaking, already set: terse northern speech", prompt);
        Assert.Contains("Her story, as the world tells it:", prompt);
        Assert.Contains("The world she lives in, as its keeper wrote it: \"The world is harsh and medieval.\"", prompt);
        Assert.Contains("- an old wound that never healed", prompt);
        Assert.Contains("- a vanity", prompt);
        Assert.Contains("Intensity drawn: VIVID", prompt);
        // The output contract: first person, 1-3 sentences, concrete, no meta.
        Assert.Contains("Write 1 to 3 sentences in her own first-person voice", prompt);
        Assert.Contains("never repeat what the facts above already say", prompt);
        Assert.Contains("no talk of cards or directors", prompt);
    }

    [Fact]
    public void BuildPrompt_LeavesEmptySectionsOut_AndSpeaksHisForAMan()
    {
        var facts = new PersonaSpark.Facts { Name = "Valdemir", GenderWord = "man", Age = 52 };
        var prompt = PersonaSpark.BuildPrompt(
            facts, "a rule they never break", "a vanity", PersonaSpark.Intensities[1]);

        Assert.Contains("Valdemir — man, about 52.", prompt);
        Assert.Contains("in his own first-person voice", prompt);
        Assert.Contains("private truths he holds about himself", prompt);
        Assert.DoesNotContain("cast of mind", prompt);      // no traits given
        Assert.DoesNotContain("way of speaking", prompt);   // no style given
        Assert.DoesNotContain("as its keeper wrote it", prompt); // no world text given
    }

    [Theory]
    [InlineData("One. Two. Three. Four.", "One. Two. Three.")]
    [InlineData("Only one sentence here", "Only one sentence here")]
    [InlineData("\"I count hides. I draw first.\"", "I count hides. I draw first.")]
    [InlineData("“I count hides.”", "I count hides.")]
    [InlineData("```\nI count hides.\n```", "I count hides.")]
    [InlineData("I wait... then I strike. Second. Third. Fourth.", "I wait... then I strike. Second. Third.")]
    [InlineData("   ", "")]
    public void ClampToSentences_KeepsAtMostThree_AndStripsWrapping(string raw, string expected)
    {
        Assert.Equal(expected, PersonaSpark.ClampToSentences(raw));
    }

    [Fact]
    public void ClampToSentences_LeavesAThreeSentenceSparkUntouched()
    {
        const string spark = "I sleep with my father's last boar hide over my face. " +
            "Before any road fight, I press my ear to it! If I hear nothing, I draw first.";
        Assert.Equal(spark, PersonaSpark.ClampToSentences(spark));
    }
}
