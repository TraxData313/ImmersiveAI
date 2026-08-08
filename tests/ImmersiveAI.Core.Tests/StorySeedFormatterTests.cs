using ImmersiveAI.Core.Memory;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class StorySeedFormatterTests
    {
        // ---- FromOwnStory: a wanderer's tavern tale, told in parts ----

        [Fact]
        public void FromOwnStory_JoinsPartsAsParagraphs()
        {
            var tale = StorySeedFormatter.FromOwnStory(new[] { "I'm an engineer.", "Well, I don't play that game." });
            Assert.Equal("I'm an engineer.\n\nWell, I don't play that game.", tale);
        }

        [Fact]
        public void FromOwnStory_SkipsBlankParts()
        {
            var tale = StorySeedFormatter.FromOwnStory(new[] { null, "  ", "So I came to the town.", "" });
            Assert.Equal("So I came to the town.", tale);
        }

        [Fact]
        public void FromOwnStory_StripsMarkupAndSmoothsWhitespace()
        {
            var tale = StorySeedFormatter.FromOwnStory(new[] { "I served  under <a href=\"x\">Derthert</a>\nfor years." });
            Assert.Equal("I served under Derthert for years.", tale);
        }

        [Fact]
        public void FromOwnStory_EmptyWhenNothingToTell()
        {
            Assert.Equal(string.Empty, StorySeedFormatter.FromOwnStory(null));
            Assert.Equal(string.Empty, StorySeedFormatter.FromOwnStory(new string?[] { null, "" }));
        }

        // ---- FromWorldStory: the account the world keeps of a noble ----

        [Fact]
        public void FromWorldStory_FramesTheTellingAsTheirOwn()
        {
            var seed = StorySeedFormatter.FromWorldStory("Gunjadrid is a lady of the Throsniring.");
            Assert.Equal("So runs my story, as the world tells it: Gunjadrid is a lady of the Throsniring.", seed);
        }

        [Fact]
        public void FromWorldStory_StripsEncyclopediaLinkMarkup()
        {
            var seed = StorySeedFormatter.FromWorldStory(
                "Bjorgir is a lord of the <a style=\"Link\" href=\"event:x\">Gauting</a> clan.");
            Assert.Contains("lord of the Gauting clan", seed);
            Assert.DoesNotContain("<", seed);
        }

        [Fact]
        public void FromWorldStory_EmptyWhenTheWorldHasNothingToSay()
        {
            Assert.Equal(string.Empty, StorySeedFormatter.FromWorldStory(null));
            Assert.Equal(string.Empty, StorySeedFormatter.FromWorldStory("   "));
            // Markup alone is not a story.
            Assert.Equal(string.Empty, StorySeedFormatter.FromWorldStory("<img src=\"x\"/>"));
        }

        // ---- FromPlayerFame: the hearsay of the player, carried before ever they spoke ----

        [Fact]
        public void FromPlayerFame_SilentBelowTheNoWordThreshold()
        {
            // Under 150 the beholder's own line says "No word of their deeds has ever reached me" —
            // the seeded hearsay must agree and say nothing at all.
            Assert.Equal(string.Empty, StorySeedFormatter.FromPlayerFame("Vulgrim", 0f));
            Assert.Equal(string.Empty, StorySeedFormatter.FromPlayerFame("Vulgrim", 149.9f));
        }

        [Fact]
        public void FromPlayerFame_TiersRiseWithRenown()
        {
            var faint = StorySeedFormatter.FromPlayerFame("Vulgrim", 150f);
            var far = StorySeedFormatter.FromPlayerFame("Vulgrim", 300f);
            var famed = StorySeedFormatter.FromPlayerFame("Vulgrim", 900f);

            Assert.Contains("some word had already reached me", faint);
            Assert.Contains("carried far across the land", far);
            Assert.Contains("famed across all Calradia", famed);

            // Every tier is remembered hearsay of a time before acquaintance, named and owned.
            foreach (var line in new[] { faint, far, famed })
            {
                Assert.Contains("Of Vulgrim, before ever we spoke", line);
            }
        }

        [Fact]
        public void FromPlayerFame_EmptyWithoutANameToCarry()
        {
            Assert.Equal(string.Empty, StorySeedFormatter.FromPlayerFame(null, 900f));
            Assert.Equal(string.Empty, StorySeedFormatter.FromPlayerFame("   ", 900f));
        }
    }
}
