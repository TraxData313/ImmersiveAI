using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class MoodTidesTests
{
    [Fact]
    public void SameSoulSameDay_AlwaysTheSameWeather()
    {
        // Determinism is the whole feature: a reload must not reroll anyone's day.
        var a = MoodTides.BuildNarration("lord_7_13_1", 412, withCycle: true);
        var b = MoodTides.BuildNarration("lord_7_13_1", 412, withCycle: true);
        Assert.Equal(a, b);
        Assert.False(string.IsNullOrWhiteSpace(a));
    }

    [Fact]
    public void TheWindTurns_AcrossDaysAndSouls()
    {
        // Over a month one soul feels several different humors...
        var humors = new HashSet<string>();
        for (int day = 0; day < 30; day++)
            humors.Add(MoodTides.DailyHumor("lord_7_13_1", day));
        Assert.True(humors.Count >= 4);

        // ...and two souls do not share one weather on the same day, at least somewhere in the month.
        bool everDiffer = false;
        for (int day = 0; day < 30 && !everDiffer; day++)
            everDiffer = MoodTides.DailyHumor("lord_7_13_1", day) != MoodTides.DailyHumor("spc_wanderer_9", day);
        Assert.True(everDiffer);
    }

    [Fact]
    public void CycleLength_StaysOnItsRails_AndIsHersForLife()
    {
        foreach (var id in new[] { "a", "lord_1_1", "spc_wanderer_42", "lord_7_13_1", "x_y_z" })
        {
            int length = MoodTides.CycleLength(id);
            Assert.InRange(length, 26, 30);
            Assert.Equal(length, MoodTides.CycleLength(id));
        }
    }

    [Fact]
    public void PhaseOf_WalksTheWholeCycle_InOrderAndInBounds()
    {
        const string id = "lady_3_2";
        int length = MoodTides.CycleLength(id);

        var phaseDays = new Dictionary<MoodTides.CyclePhase, int>();
        int previousDay = -1;
        for (int day = 0; day < length; day++)
        {
            var phase = MoodTides.PhaseOf(id, day, out int dayOfCycle);
            Assert.InRange(dayOfCycle, 1, length);

            // Consecutive campaign days advance the cycle by exactly one, wrapping at the length.
            if (previousDay >= 0)
                Assert.Equal(previousDay % length + 1, dayOfCycle);
            previousDay = dayOfCycle;

            phaseDays[phase] = phaseDays.TryGetValue(phase, out int n) ? n + 1 : 1;
        }

        // All four turnings occur, with the classic shape: five days of the custom, three of the crest.
        Assert.Equal(5, phaseDays[MoodTides.CyclePhase.Menses]);
        Assert.Equal(3, phaseDays[MoodTides.CyclePhase.Crest]);
        Assert.True(phaseDays[MoodTides.CyclePhase.Rising] > 0);
        Assert.True(phaseDays[MoodTides.CyclePhase.Waning] > 0);
    }

    [Fact]
    public void Narration_CarriesTheBodysSeason_OnlyWhenGiven()
    {
        const string seasonMark = "my body keeps its own season";

        var withCycle = MoodTides.BuildNarration("lady_3_2", 100, withCycle: true);
        Assert.Contains(seasonMark, withCycle);
        Assert.StartsWith("This day finds me ", withCycle);

        var without = MoodTides.BuildNarration("lord_4_4", 100, withCycle: false);
        Assert.DoesNotContain(seasonMark, without);
        Assert.StartsWith("This day finds me ", without);
    }

    [Fact]
    public void EveryTurning_HasItsOwnSentence_InTheOldWords()
    {
        var sentences = new HashSet<string>();
        foreach (MoodTides.CyclePhase phase in Enum.GetValues(typeof(MoodTides.CyclePhase)))
        {
            var sentence = MoodTides.CycleSentence(phase);
            Assert.False(string.IsNullOrWhiteSpace(sentence));
            sentences.Add(sentence);
        }
        Assert.Equal(4, sentences.Count);

        // The custom itself and its approach are named in the old words, never a clinician's terms.
        Assert.Contains("custom of women", MoodTides.CycleSentence(MoodTides.CyclePhase.Menses));
        Assert.Contains("custom of women", MoodTides.CycleSentence(MoodTides.CyclePhase.Waning));
    }

    [Fact]
    public void AnEmptySoul_GetsNoSharedWeather()
    {
        Assert.Equal(string.Empty, MoodTides.BuildNarration("", 100, withCycle: false));
        Assert.Equal(string.Empty, MoodTides.BuildNarration(null!, 100, withCycle: true));
    }

    [Fact]
    public void AWomanWithChild_GetsHerCarryingSeason_NeverTheMonthlyOne()
    {
        var narration = MoodTides.BuildNarration("lady_3_2", 100, withCycle: true, withChild: true);

        Assert.StartsWith("This day finds me ", narration);
        Assert.Contains("the child within me", narration);
        // The monthly turnings rest while she carries — never both seasons at once.
        Assert.DoesNotContain("as it does for every woman", narration);

        // Deterministic like all the weather, and varying across the days.
        Assert.Equal(narration, MoodTides.BuildNarration("lady_3_2", 100, withCycle: true, withChild: true));
        var sentences = new HashSet<string>();
        for (int day = 0; day < 12; day++)
            sentences.Add(MoodTides.WithChildSentence("lady_3_2", day));
        Assert.True(sentences.Count >= 3);
    }
}
