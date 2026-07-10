using ImmersiveAI.Core.Memory;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class SelfSeedFormatterTests
    {
        // ---- FromOwnStory: a wanderer's tavern tale, told in parts ----

        [Fact]
        public void FromOwnStory_JoinsPartsAsParagraphs()
        {
            var tale = SelfSeedFormatter.FromOwnStory(new[] { "I'm an engineer.", "Well, I don't play that game." });
            Assert.Equal("I'm an engineer.\n\nWell, I don't play that game.", tale);
        }

        [Fact]
        public void FromOwnStory_SkipsBlankParts()
        {
            var tale = SelfSeedFormatter.FromOwnStory(new[] { null, "  ", "So I came to the town.", "" });
            Assert.Equal("So I came to the town.", tale);
        }

        [Fact]
        public void FromOwnStory_StripsMarkupAndSmoothsWhitespace()
        {
            var tale = SelfSeedFormatter.FromOwnStory(new[] { "I served  under <a href=\"x\">Derthert</a>\nfor years." });
            Assert.Equal("I served under Derthert for years.", tale);
        }

        [Fact]
        public void FromOwnStory_EmptyWhenNothingToTell()
        {
            Assert.Equal(string.Empty, SelfSeedFormatter.FromOwnStory(null));
            Assert.Equal(string.Empty, SelfSeedFormatter.FromOwnStory(new string?[] { null, "" }));
        }

        // ---- FromWorldStory: the account the world keeps of a noble ----

        [Fact]
        public void FromWorldStory_FramesTheTellingAsTheirOwn()
        {
            var seed = SelfSeedFormatter.FromWorldStory("Gunjadrid is a lady of the Throsniring.");
            Assert.Equal("So runs my story, as the world tells it: Gunjadrid is a lady of the Throsniring.", seed);
        }

        [Fact]
        public void FromWorldStory_StripsEncyclopediaLinkMarkup()
        {
            var seed = SelfSeedFormatter.FromWorldStory(
                "Bjorgir is a lord of the <a style=\"Link\" href=\"event:x\">Gauting</a> clan.");
            Assert.Contains("lord of the Gauting clan", seed);
            Assert.DoesNotContain("<", seed);
        }

        [Fact]
        public void FromWorldStory_EmptyWhenTheWorldHasNothingToSay()
        {
            Assert.Equal(string.Empty, SelfSeedFormatter.FromWorldStory(null));
            Assert.Equal(string.Empty, SelfSeedFormatter.FromWorldStory("   "));
            // Markup alone is not a story.
            Assert.Equal(string.Empty, SelfSeedFormatter.FromWorldStory("<img src=\"x\"/>"));
        }
    }
}
