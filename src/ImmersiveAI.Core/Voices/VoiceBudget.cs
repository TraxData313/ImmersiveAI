using System;

namespace ImmersiveAI.Core.Voices
{
    /// <summary>
    /// How much audio a line of text is ALLOWED to become — the arithmetic behind the guillotine.
    /// <para>
    /// Autoregressive speech models derail: the model misses its end-of-speech token and simply keeps
    /// generating until it hits its own ceiling, which comes out as babbling or screeching. Anton
    /// reported it from experience elsewhere ("sometimes the voice can glitch and start making crazy
    /// sounds and it wont stop"), and on 2026.08.15 it reproduced itself on this very engine while
    /// the numbers below were being measured: <b>202 characters of Bulgarian became 327.68 seconds of
    /// audio</b> — five and a half minutes — and cost 139 seconds of graphics card to make.
    /// </para>
    /// <para>
    /// THE MEASUREMENTS, all taken 2026.08.15 against qwen-talker-1.7b-base on the author's card, and
    /// none of them documented anywhere else:
    /// <list type="bullet">
    /// <item>One audio token is <b>exactly 1920 samples at 24 kHz = 80 ms</b>. A line asked for with a
    /// ceiling of 256 tokens came back at 20.48 s to the sample, twice, from different texts.</item>
    /// <item>The engine's own ceiling is 4096 tokens — which is 327.68 s, and is precisely where the
    /// derail above stopped. That is not a coincidence; it is the runaway hitting its rail.</item>
    /// <item>Real speech runs at <b>13–17 characters a second</b> (224 English characters → 13.4 s;
    /// 202 Bulgarian → 15.3 s; 55 English → 4.2 s). Cyrillic is no slower per character, which is
    /// worth knowing given how much of this mod's writing is about non-ASCII costing more.</item>
    /// </list>
    /// </para>
    /// <para>
    /// So the ceiling is set from the text's own length, generously enough that no honest reading is
    /// ever cut, and tightly enough that a runaway is ten or twenty seconds rather than five minutes.
    /// </para>
    /// </summary>
    public static class VoiceBudget
    {
        /// <summary>Samples one audio token is worth, at the engine's own rate. Measured, not read.</summary>
        public const int SamplesPerToken = 1920;

        /// <summary>Seconds one audio token is worth (80 ms).</summary>
        public const double SecondsPerToken = 0.08;

        /// <summary>The engine's own ceiling, and so ours: 4096 tokens is 327.68 seconds.</summary>
        public const int EngineMaxTokens = 4096;

        /// <summary>Never ask for less than this, whatever the text. A ceiling under a couple of
        /// seconds would cut "Yes, my lord." in half on a slow reading.</summary>
        public const int MinTokens = 40;          // 3.2 s

        /// <summary>The slowest plausible honest reading. Measured lines ran 13–17 characters a
        /// second; the low end is the safe one, because being generous costs nothing and being mean
        /// truncates a real sentence.</summary>
        public const double CharsPerSecond = 13.0;

        /// <summary>A flat allowance for the breath at either end, which a short line is all but made
        /// of — 55 characters measured 4.2 s where the rate alone predicts 4.2 s of speech and no
        /// room to begin.</summary>
        public const double GraceSeconds = 1.5;

        /// <summary>How far past an honest reading we let it run before calling it a derail. Under
        /// two, because the estimate above is already generous — a real line came in at 0.75× its own
        /// estimate, so 1.8× leaves a wide margin over anything that is genuinely speech.</summary>
        public const double DerailFactor = 1.8;

        /// <summary>How long an honest reading of this text should take.</summary>
        public static double ExpectedSeconds(string? text)
        {
            var chars = (text ?? string.Empty).Trim().Length;
            if (chars == 0) return 0;
            return GraceSeconds + chars / CharsPerSecond;
        }

        /// <summary>The longest this text may honestly become before we stop believing it is speech.</summary>
        public static double CeilingSeconds(string? text)
        {
            var expected = ExpectedSeconds(text);
            if (expected <= 0) return 0;
            var ceiling = expected * DerailFactor;
            var engineMax = EngineMaxTokens * SecondsPerToken;
            return ceiling > engineMax ? engineMax : ceiling;
        }

        /// <summary>
        /// The audio-token ceiling to ask the engine for. This is the FIRST line of defence and the
        /// cheap one — the engine enforces it exactly, so a runaway simply stops instead of being
        /// detected afterwards.
        /// </summary>
        public static int MaxTokensFor(string? text)
        {
            var seconds = CeilingSeconds(text);
            if (seconds <= 0) return MinTokens;

            var tokens = (int)Math.Ceiling(seconds / SecondsPerToken);
            if (tokens < MinTokens) tokens = MinTokens;
            if (tokens > EngineMaxTokens) tokens = EngineMaxTokens;
            return tokens;
        }

        /// <summary>
        /// The sample count at which a generation should be stopped mid-flight. The SECOND line of
        /// defence, and the one that does not depend on the engine honouring anything: the host
        /// counts what it has actually been handed and pulls the cord itself.
        /// <para>
        /// Deliberately a little looser than <see cref="MaxTokensFor"/> so that in the ordinary case
        /// the engine's own exact ceiling is what stops it, and this only fires when that ceiling was
        /// ignored — which is exactly what a future model with a different token rate would look like.
        /// </para>
        /// </summary>
        public static long MaxSamplesFor(string? text, int sampleRate)
        {
            if (sampleRate <= 0) sampleRate = 24000;
            var seconds = CeilingSeconds(text);
            if (seconds <= 0) return (long)MinTokens * SamplesPerToken;

            // A tenth over the asked-for ceiling, plus one chunk's worth of slack: the engine hands
            // audio over a second at a time and must never be cut off for overshooting by a hair.
            return (long)((seconds * 1.1 + 1.0) * sampleRate);
        }

        /// <summary>
        /// Whether what came back is far MORE audio than these words could justify — a derail, judged
        /// after the fact. Used on the reply as a whole, so a clip that slipped past both ceilings is
        /// still kept out of the cache.
        /// </summary>
        public static bool LooksDerailed(string? text, long samples, int sampleRate)
        {
            if (samples <= 0 || sampleRate <= 0) return false;
            var ceiling = CeilingSeconds(text);
            if (ceiling <= 0) return false;
            return samples / (double)sampleRate > ceiling * 1.15;
        }

        /// <summary>
        /// Whether what came back is far LESS audio than these words justify. The other end of the
        /// same detector, and it has already earned its place: the ICL road on a base model answers
        /// <c>ok:true</c> with 1920 samples — eight hundredths of a second — for a whole sentence, and
        /// anything checking only the verdict sails straight past it.
        /// </summary>
        public static bool LooksTruncated(string? text, long samples, int sampleRate)
        {
            if (samples <= 0 || sampleRate <= 0) return false;
            var chars = (text ?? string.Empty).Trim().Length;
            if (chars < 20) return false;              // short enough that any reading is honest

            // A quarter of the expected reading. Wide on purpose: real speech varies enormously with
            // language and voice, and rejecting a merely brisk reading would be the worse bug.
            return samples / (double)sampleRate < ExpectedSeconds(text) * 0.25;
        }
    }
}
