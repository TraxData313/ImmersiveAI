using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The anti-derail arithmetic. Every number these tests lean on was MEASURED against the real engine
/// on 2026.08.15 (see VoiceBudget's own remarks), including one genuine runaway: 202 characters of
/// Bulgarian became 327.68 seconds of audio, which is exactly the engine's 4096-token ceiling.
/// </summary>
public class VoiceBudgetTests
{
    private const int Rate = 24000;

    // The two lines that were actually measured, with what the engine really answered.
    private const string MeasuredEnglish =
        "I have been thinking about what you said at the ford, and I find I cannot let it lie. If we ride north " +
        "before the snows close the passes, we may yet reach Varcheg with the grain intact, and no one need go " +
        "hungry this winter.";                                   // 224 chars -> 13.4 s

    private const string MeasuredBulgarian =
        "Мислих дълго за това, което ми каза при брода, и не мога да го оставя така. Ако тръгнем на север преди " +
        "снеговете да затворят проходите, ще стигнем Варчег със зърното, и никой няма да гладува тази зима.";
                                                                 // 202 chars -> 15.3 s

    [Fact]
    public void RealReadingsAreComfortablyInsideTheCeiling()
    {
        // If either of these ever fails, the guillotine has started cutting honest speech.
        Assert.True(VoiceBudget.CeilingSeconds(MeasuredEnglish) > 13.4);
        Assert.True(VoiceBudget.CeilingSeconds(MeasuredBulgarian) > 15.3);

        // And a short line: 55 characters read as 4.2 s.
        Assert.True(VoiceBudget.CeilingSeconds("Then we are agreed. I will tell the men at first light.") > 4.2);
    }

    [Fact]
    public void CeilingIsFarBelowTheRunawayItExistsToStop()
    {
        // The real derail ran to 327.68 s. Anything near that is a failure of the whole feature.
        Assert.True(VoiceBudget.CeilingSeconds(MeasuredBulgarian) < 60);
        Assert.True(VoiceBudget.CeilingSeconds(MeasuredEnglish) < 60);
    }

    [Fact]
    public void MaxTokensNeverAsksForLessThanAShortLineNeeds()
    {
        Assert.Equal(VoiceBudget.MinTokens, VoiceBudget.MaxTokensFor(""));
        Assert.Equal(VoiceBudget.MinTokens, VoiceBudget.MaxTokensFor("   "));
        Assert.True(VoiceBudget.MaxTokensFor("Aye.") >= VoiceBudget.MinTokens);

        // A token is 80 ms, so the floor is over three seconds - enough for any short line.
        Assert.True(VoiceBudget.MinTokens * VoiceBudget.SecondsPerToken >= 3.0);
    }

    [Fact]
    public void MaxTokensNeverExceedsTheEnginesOwnCeiling()
    {
        var enormous = new string('a', 100000);
        Assert.Equal(VoiceBudget.EngineMaxTokens, VoiceBudget.MaxTokensFor(enormous));
    }

    [Fact]
    public void MaxTokensGrowsWithTheText()
    {
        var shortLine = VoiceBudget.MaxTokensFor("Yes.");
        var mediumLine = VoiceBudget.MaxTokensFor(MeasuredEnglish);
        var longLine = VoiceBudget.MaxTokensFor(MeasuredEnglish + MeasuredEnglish);

        Assert.True(shortLine < mediumLine);
        Assert.True(mediumLine < longLine);
    }

    [Fact]
    public void TokensBuyTheMeasuredAmountOfAudio()
    {
        // 256 tokens came back as exactly 20.48 s, twice, from two different texts.
        Assert.Equal(20.48, 256 * VoiceBudget.SecondsPerToken, 3);
        Assert.Equal(327.68, VoiceBudget.EngineMaxTokens * VoiceBudget.SecondsPerToken, 2);
        Assert.Equal(VoiceBudget.SamplesPerToken / (double)Rate, VoiceBudget.SecondsPerToken, 6);
    }

    [Fact]
    public void LooksDerailed_CatchesTheRealRunawayAndNothingHonest()
    {
        // The measured runaway: 202 characters, 327.68 s of audio.
        Assert.True(VoiceBudget.LooksDerailed(MeasuredBulgarian, (long)(327.68 * Rate), Rate));

        // The measured honest readings.
        Assert.False(VoiceBudget.LooksDerailed(MeasuredBulgarian, (long)(15.3 * Rate), Rate));
        Assert.False(VoiceBudget.LooksDerailed(MeasuredEnglish, (long)(13.4 * Rate), Rate));

        // A slow reader is not a derail: half again as long as measured must still pass.
        Assert.False(VoiceBudget.LooksDerailed(MeasuredEnglish, (long)(20.0 * Rate), Rate));
    }

    [Fact]
    public void LooksTruncated_CatchesTheIclShapeAndNothingHonest()
    {
        // The ICL road on a base model answers ok:true with 1920 samples for a whole sentence.
        Assert.True(VoiceBudget.LooksTruncated(MeasuredEnglish, 1920, Rate));

        Assert.False(VoiceBudget.LooksTruncated(MeasuredEnglish, (long)(13.4 * Rate), Rate));

        // A brisk reading is not a truncation.
        Assert.False(VoiceBudget.LooksTruncated(MeasuredEnglish, (long)(9.0 * Rate), Rate));
    }

    [Fact]
    public void LooksTruncated_LeavesVeryShortLinesAlone()
    {
        // "Aye." legitimately produces very little audio; judging it would only ever be wrong.
        Assert.False(VoiceBudget.LooksTruncated("Aye.", 1920, Rate));
    }

    [Fact]
    public void JudgementsNeverThrowOnRubbish()
    {
        Assert.False(VoiceBudget.LooksDerailed(null, 0, 0));
        Assert.False(VoiceBudget.LooksTruncated(null, -5, -1));
        Assert.Equal(0, VoiceBudget.ExpectedSeconds(null));
        Assert.Equal(0, VoiceBudget.CeilingSeconds(""));
        Assert.True(VoiceBudget.MaxSamplesFor(null, 0) > 0);
    }

    /// <summary>
    /// The 2026.08.17 playtest, in arithmetic. A relative margin gives a long reply a proportionally
    /// long licence to babble — which was invisible while Full read was the default (nothing is heard
    /// until the generation ends) and audible the moment streaming put every second into the air.
    /// </summary>
    [Fact]
    public void SlackOverAnHonestReadingIsCappedHoweverLongTheText()
    {
        // A whole reply of several paragraphs — the shape that was actually heard running away.
        var reply = string.Join(" ", Enumerable.Repeat(MeasuredEnglish, 4));   // ~900 chars

        var honest = VoiceBudget.ExpectedSeconds(reply);
        var slack = VoiceBudget.CeilingSeconds(reply) - honest;

        Assert.True(slack <= VoiceBudget.MaxExcessSeconds + 0.001,
            $"a {honest:F0}s reply was given {slack:F0}s of rope");

        // And the old arithmetic really would have given it half a minute.
        Assert.True(honest * VoiceBudget.DerailFactor - honest > 20);
    }

    [Fact]
    public void ShortLinesKeepTheirProportionalSlack()
    {
        // The cap must bind only where the proportional margin has grown silly. A four-second line
        // still gets its full 0.8x, or a brisk-but-honest short reading starts being guillotined.
        const string shortLine = "Then we are agreed. I will tell the men at first light.";
        var honest = VoiceBudget.ExpectedSeconds(shortLine);
        var slack = VoiceBudget.CeilingSeconds(shortLine) - honest;

        Assert.Equal(honest * (VoiceBudget.DerailFactor - 1.0), slack, 3);
        Assert.True(slack < VoiceBudget.MaxExcessSeconds);
    }

    [Fact]
    public void ExpectedSamplesArmsTheGuardAfterTheWordsAndBeforeThePatience()
    {
        // The host begins listening at ExpectedSamples and gives up at CeilingSeconds. The first must
        // land past every honest reading measured, and inside the second, or the guard is either deaf
        // or trigger-happy.
        foreach (var text in new[] { MeasuredEnglish, MeasuredBulgarian })
        {
            var arm = VoiceBudget.ExpectedSamplesFor(text, Rate) / (double)Rate;
            Assert.True(arm < VoiceBudget.CeilingSeconds(text));
        }

        Assert.True(VoiceBudget.ExpectedSamplesFor(MeasuredEnglish, Rate) / (double)Rate > 13.4);
        Assert.True(VoiceBudget.ExpectedSamplesFor(MeasuredBulgarian, Rate) / (double)Rate > 15.3);

        // Nothing to say arms nothing at all: 0 means "do not listen".
        Assert.Equal(0, VoiceBudget.ExpectedSamplesFor("", Rate));
        Assert.Equal(0, VoiceBudget.ExpectedSamplesFor(null, 0));
    }

    [Fact]
    public void MaxSamplesIsLooserThanTheTokenCeiling()
    {
        // The engine's own exact rail should be what stops an ordinary runaway; the host's counting
        // guard is the backstop for the day that rail means something else.
        var tokens = VoiceBudget.MaxTokensFor(MeasuredEnglish);
        var fromTokens = (long)tokens * VoiceBudget.SamplesPerToken;
        Assert.True(VoiceBudget.MaxSamplesFor(MeasuredEnglish, Rate) > fromTokens);
    }
}
