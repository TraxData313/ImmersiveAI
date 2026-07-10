using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Tests;

public class MemoryCompressorTests
{
    private sealed class FakeChatClient : IChatClient
    {
        public string Response = "";
        public IReadOnlyList<ChatMessage>? LastRequest;

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            LastRequest = messages;
            return Task.FromResult(Response);
        }
    }

    private static NpcMemory MemoryWithTurns(int count)
    {
        var memory = new NpcMemory { NpcId = "lord_1", NpcName = "Gafnir" };
        for (int i = 0; i < count; i++)
            memory.AddTurn(new ConversationTurn { PlayerLine = $"p{i}", NpcLine = $"n{i}", GameDay = i });
        return memory;
    }

    [Fact]
    public async Task CompressAsync_FoldsOldTurnsIntoSummaryAndFacts()
    {
        var client = new FakeChatClient
        {
            Response = "SUMMARY:\nWe fought together at Omor and grew to trust one another.\nFACTS:\n- Vulgrim saved my life at Omor\n- none of importance"
        };
        var memory = MemoryWithTurns(10);

        var compressed = await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 4);

        Assert.True(compressed);
        Assert.Equal(4, memory.RecentTurns.Count);
        Assert.Equal("p6", memory.RecentTurns[0].PlayerLine);
        Assert.Contains("Omor", memory.Summary);
        Assert.Contains("Vulgrim saved my life at Omor", memory.KnownFacts);
    }

    [Fact]
    public async Task CompressAsync_NothingToCompress_ReturnsFalse()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nirrelevant" };
        var memory = MemoryWithTurns(3);

        var compressed = await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 5);

        Assert.False(compressed);
        Assert.Null(client.LastRequest);
        Assert.Equal(3, memory.RecentTurns.Count);
    }

    [Fact]
    public async Task CompressAsync_ShowsWholeDeepMemoryPlusFadingAndFreshTurns()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);
        memory.Summary = "Old friends from the north.";
        memory.KnownFacts.Add("Vulgrim rules Sargot");

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        var prompt = client.LastRequest![0].Content;
        // Whole deep memory is visible so they can update it with full context.
        Assert.Contains("Old friends from the north.", prompt);
        Assert.Contains("Vulgrim rules Sargot", prompt);
        // Oldest turns are the ones being folded in.
        Assert.Contains("The moments now fading", prompt);
        Assert.Contains("p0", prompt);
        Assert.Contains("p3", prompt);
        // The kept turns are shown as still-fresh context, not folded in.
        Assert.Contains("Still fresh in your mind", prompt);
        Assert.Contains("p4", prompt);
        Assert.Contains("p5", prompt);
    }

    [Fact]
    public async Task ReflectAsync_WithNothingToFold_StillRewritesSummaryAndKeepsAllTurns()
    {
        var client = new FakeChatClient
        {
            Response = "SUMMARY:\nOn reflection, we are not yet wed, but I hope for it.\nFACTS:\n- We are betrothed, not married"
        };
        var memory = MemoryWithTurns(3);
        memory.Summary = "An old, stale summary.";

        // keepMostRecent >= turn count, so there is nothing old enough to fold away.
        var reflected = await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5);

        Assert.True(reflected);
        Assert.NotNull(client.LastRequest); // she actually re-thinks (an LLM call happened)
        Assert.Equal(3, memory.RecentTurns.Count); // no turns dropped
        Assert.Contains("not yet wed", memory.Summary); // summary rewritten
        Assert.Contains("We are betrothed, not married", memory.KnownFacts);
    }

    [Fact]
    public async Task ReflectAsync_FoldsExcessTurnsBeyondKeepWindow()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(10);

        var reflected = await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 4);

        Assert.True(reflected);
        Assert.Equal(4, memory.RecentTurns.Count);
        Assert.Equal("p6", memory.RecentTurns[0].PlayerLine);
    }

    [Fact]
    public async Task ReflectAsync_WithNoMemoryAtAll_ReturnsFalseWithoutCallingLlm()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nirrelevant" };
        var memory = new NpcMemory { NpcId = "lord_1", NpcName = "Gafnir" };

        var reflected = await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5);

        Assert.False(reflected);
        Assert.Null(client.LastRequest);
    }

    [Fact]
    public async Task CompressAsync_AddressesNpcAsIndividualViaNamedSystemVoice()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: "Muse");

        var prompt = client.LastRequest![0].Content;
        Assert.Contains("Muse speaks gently into your mind, Gafnir:", prompt);
        Assert.Contains("what to carry forward and what to let go", prompt);
        Assert.Contains("Answer Muse", prompt);
        // Kept the machine-readable contract the parser depends on.
        Assert.Contains("SUMMARY:", prompt);
        Assert.Contains("FACTS:", prompt);
    }

    [Fact]
    public async Task CompressAsync_RepliedFactsReplaceTheOldList_SoSheCanRefactorThem()
    {
        // The old merge-only behavior piled rewordings up forever; now the FACTS she returns
        // ARE her truths (she is shown the whole list and asked to write it anew).
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nFACTS:\n- Vulgrim's castle feels like home" };
        var memory = MemoryWithTurns(6);
        memory.KnownFacts.Add("The warmth and hospitality of Vulgrim's castle create a sense of home");
        memory.KnownFacts.Add("Vulgrim's castle is warm and hospitable");

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        var fact = Assert.Single(memory.KnownFacts);
        Assert.Equal("Vulgrim's castle feels like home", fact);
    }

    [Fact]
    public async Task CompressAsync_FactsNone_IsHerChoiceToReleaseThemAll()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nFACTS: none" };
        var memory = MemoryWithTurns(6);
        memory.KnownFacts.Add("a truth she has let go");

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        Assert.Empty(memory.KnownFacts);
    }

    [Fact]
    public async Task CompressAsync_ReplyWithoutFactsSection_LeavesHerTruthsUntouched()
    {
        // A malformed reply (no FACTS section at all) must never wipe her memory.
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);
        memory.KnownFacts.Add("a truth that must survive");

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        Assert.Equal(new[] { "a truth that must survive" }, memory.KnownFacts);
    }

    [Fact]
    public async Task CompressAsync_TrimsRepliedFactsToTheBudget()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nFACTS:\n- f1\n- f2\n- f3" };
        var memory = MemoryWithTurns(6);

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: null, maxFacts: 2);

        Assert.Equal(new[] { "f1", "f2" }, memory.KnownFacts);
    }

    [Fact]
    public void BuildCompressionRequest_AsksHerToWriteTruthsAnew_WithTheBudget()
    {
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildCompressionRequest(
            memory, memory.RecentTurns, systemVoiceName: null, maxFacts: 7)[0].Content;

        Assert.Contains("Write your truths anew", prompt);
        Assert.Contains("at most 7", prompt);
        Assert.Contains("falls away from you", prompt);
    }

    [Fact]
    public void ParseResponse_ReportsWhetherAFactsSectionWasPresent()
    {
        Assert.True(MemoryCompressor.ParseResponse("SUMMARY:\ns\nFACTS: none").HasFactsSection);
        Assert.True(MemoryCompressor.ParseResponse("SUMMARY:\ns\nFACTS:\n- f").HasFactsSection);
        Assert.False(MemoryCompressor.ParseResponse("SUMMARY:\ns").HasFactsSection);
    }

    [Fact]
    public void BuildCompressionRequest_DefaultsSystemVoiceToAngel()
    {
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildCompressionRequest(memory, memory.RecentTurns)[0].Content;

        Assert.Contains("Angel speaks gently into your mind, Gafnir:", prompt);
    }

    [Fact]
    public void ParseResponse_HandlesMissingFactsSection()
    {
        var result = MemoryCompressor.ParseResponse("SUMMARY:\nJust a summary.");

        Assert.Equal("Just a summary.", result.Summary);
        Assert.Empty(result.Facts);
    }

    [Fact]
    public void ParseResponse_NoHeaders_TreatsWholeTextAsSummary()
    {
        var result = MemoryCompressor.ParseResponse("The model just wrote prose.");

        Assert.Equal("The model just wrote prose.", result.Summary);
    }

    [Fact]
    public void ParseResponse_FactsNone_YieldsNoFacts()
    {
        var result = MemoryCompressor.ParseResponse("SUMMARY:\ns\nFACTS:\n- none");

        Assert.Empty(result.Facts);
    }

    [Fact]
    public void ParseResponse_SelfSection_IsExtracted_AndNotMistakenForFacts()
    {
        var result = MemoryCompressor.ParseResponse(
            "SUMMARY:\nWe spoke of the war.\nFACTS:\n- The player spared my brother\nSELF:\nI am wearier than I was, but I still hope.");

        Assert.Equal("We spoke of the war.", result.Summary);
        Assert.Contains("The player spared my brother", result.Facts);
        Assert.Single(result.Facts); // the self paragraph did not leak into facts
        Assert.Equal("I am wearier than I was, but I still hope.", result.Self);
    }

    [Fact]
    public void ParseResponse_NoSelfSection_LeavesSelfNull()
    {
        var result = MemoryCompressor.ParseResponse("SUMMARY:\nok\nFACTS:\n- a truth");

        Assert.Null(result.Self);
    }

    [Fact]
    public void BuildReflectionRequest_WithoutSelf_DoesNotAskForSelf()
    {
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildReflectionRequest(memory, System.Array.Empty<ConversationTurn>())[0].Content;

        Assert.DoesNotContain("SELF:", prompt);
        Assert.DoesNotContain("who you have become", prompt);
    }

    [Fact]
    public void BuildReflectionRequest_FirstEverSelf_InvitesWithoutOfferingUnchanged()
    {
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildReflectionRequest(
            memory, System.Array.Empty<ConversationTurn>(), systemVoiceName: null, selfText: "")[0].Content;

        Assert.Contains("SELF:", prompt);
        Assert.Contains("not yet put into words", prompt);   // she's told she has no self yet
        Assert.DoesNotContain("write: unchanged", prompt);   // and isn't handed the easy way out
    }

    [Fact]
    public void BuildReflectionRequest_WithSelf_ShowsCurrentSelfAndAsksForSelf()
    {
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildReflectionRequest(
            memory, System.Array.Empty<ConversationTurn>(), systemVoiceName: null, selfText: "I am a cautious soul.")[0].Content;

        Assert.Contains("consider who you have become", prompt);
        Assert.Contains("I am a cautious soul.", prompt); // current self shown for revision
        Assert.Contains("SELF:", prompt);                 // and the SELF answer slot is offered
    }

    [Fact]
    public async Task ReflectAsync_UpdatesSelf_WhenANewOneIsOffered()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nI have grown bolder of late." };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "I am timid." };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5, systemVoiceName: null, self: self);

        Assert.Equal("I have grown bolder of late.", self.Text);
    }

    [Fact]
    public async Task ReflectAsync_LeavesSelfUnchanged_OnUnchangedKeyword()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nunchanged" };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "I am who I was." };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5, systemVoiceName: null, self: self);

        Assert.Equal("I am who I was.", self.Text);
    }

    [Theory]
    [InlineData("Unchanged.")]
    [InlineData("(unchanged)")]
    [InlineData("  *Unchanged*  ")]
    [InlineData("**  \nUnchanged.")] // the exact shape that once overwrote a real self (2026.07.10)
    [InlineData("---\nunchanged\n---")]
    public async Task ReflectAsync_TreatsPunctuatedUnchangedAsNoChange(string selfReply)
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + selfReply };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "I am who I was." };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5, systemVoiceName: null, self: self);

        Assert.Equal("I am who I was.", self.Text); // marker never leaks in as a real self
    }

    [Theory]
    [InlineData("unchanged", true)]
    [InlineData("Unchanged.", true)]
    [InlineData("(unchanged)", true)]
    [InlineData("**  \nUnchanged.", true)]          // markdown decoration line above the word
    [InlineData("### \n> *Unchanged!*\n---", true)] // heavier dressing, still only the one word
    [InlineData("I have grown bolder.", false)]
    [InlineData("I am unchanged in my love for the sea.", false)] // prose containing the word is prose
    [InlineData("Unchanged in most ways.\nYet the war weighs on me.", false)] // two meaningful lines
    [InlineData("", false)]
    public void IsUnchangedMarker_RecognizesTheMarkerNotProse(string text, bool expected)
    {
        Assert.Equal(expected, MemoryCompressor.IsUnchangedMarker(text));
    }
}
