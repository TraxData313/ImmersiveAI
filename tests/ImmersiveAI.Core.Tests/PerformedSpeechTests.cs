using System.Collections.Generic;
using ImmersiveAI.Core.Voices;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    /// <summary>What an acting engine is handed: a laugh where she laughs, a whisper where she
    /// whispers, and nothing it said it cannot do.</summary>
    public class PerformedSpeechTests
    {
        private static readonly string[] Breeze = { "laugh", "sigh", "cough", "clears throat" };

        [Fact]
        public void A_short_laugh_gesture_becomes_the_laugh_itself()
        {
            var line = SpeakableText.Performed("*laughs softly* You caught me.", speakActed: true, Breeze, takesMood: true);
            Assert.Equal("(laugh) You caught me.", line.Text);
            Assert.Equal(string.Empty, line.Mood);
        }

        [Fact]
        public void Chuckles_and_giggles_are_the_one_laugh_the_engine_has()
        {
            Assert.StartsWith("(laugh)", SpeakableText.Performed("*chuckles* Well.", true, Breeze, true).Text);
            Assert.StartsWith("(laugh)", SpeakableText.Performed("*giggles* Well.", true, Breeze, true).Text);
        }

        [Fact]
        public void Sighs_coughs_and_a_cleared_throat_each_find_their_sound()
        {
            Assert.StartsWith("(sigh)", SpeakableText.Performed("*sighs deeply* Fine.", false, Breeze, true).Text);
            Assert.StartsWith("(cough)", SpeakableText.Performed("*coughs* Fine.", false, Breeze, true).Text);
            Assert.StartsWith("(clears throat)", SpeakableText.Performed("*clears her throat* Fine.", false, Breeze, true).Text);
        }

        [Fact]
        public void A_long_gesture_keeps_its_narration_after_the_sound()
        {
            var line = SpeakableText.Performed("*laughs and pours the wine for both of us* Drink.", true, Breeze, true);
            Assert.Equal("(laugh) laughs and pours the wine for both of us. Drink.", line.Text);
        }

        [Fact]
        public void With_acted_parts_off_only_the_sound_survives()
        {
            var line = SpeakableText.Performed("*laughs and pours the wine for both of us* Drink.", false, Breeze, true);
            Assert.Equal("(laugh) Drink.", line.Text);
        }

        [Fact]
        public void An_engine_with_no_sounds_gets_exactly_the_old_reading()
        {
            const string body = "*laughs softly* You caught me.";
            Assert.Equal(SpeakableText.SpokenWithGestures(body),
                         SpeakableText.Performed(body, true, new string[0], takesMood: false).Text);
            Assert.Equal(SpeakableText.SpokenOnly(body),
                         SpeakableText.Performed(body, false, null, takesMood: false).Text);
        }

        [Fact]
        public void Only_the_sounds_the_engine_names_are_asked_for()
        {
            var laughOnly = new List<string> { "laugh" };
            Assert.Equal("Fine.", SpeakableText.Performed("*sighs* Fine.", false, laughOnly, true).Text);
        }

        [Fact]
        public void A_whisper_makes_the_whole_line_a_whisper_and_is_not_read_out()
        {
            var line = SpeakableText.Performed("*leans close and whispers* They are listening.", true, Breeze, takesMood: true);
            Assert.Equal("whisper", line.Mood);
            Assert.Equal("They are listening.", line.Text);
        }

        [Fact]
        public void A_whisper_is_left_alone_on_an_engine_that_takes_no_mood()
        {
            var line = SpeakableText.Performed("*whispers* They are listening.", true, null, takesMood: false);
            Assert.Equal(string.Empty, line.Mood);
            Assert.Equal("whispers. They are listening.", line.Text);
        }

        [Fact]
        public void Bulgarian_gestures_are_understood_too()
        {
            Assert.StartsWith("(laugh)", SpeakableText.Performed("*смее се* Добре.", false, Breeze, true).Text);
            Assert.Equal("whisper", SpeakableText.Performed("*прошепва* Слушат ни.", false, null, true).Mood);
        }

        [Fact]
        public void A_word_that_only_looks_like_a_sound_is_not_one()
        {
            // "slaughter" holds "laugh"; the word boundary is what keeps it a word.
            var line = SpeakableText.Performed("*remembers the slaughter at the ford* Never again.", false, Breeze, true);
            Assert.Equal("Never again.", line.Text);
        }
    }
}
