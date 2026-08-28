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
    public async Task CompressAsync_FoldsOldTurnsIntoTheRollingMemory()
    {
        var client = new FakeChatClient
        {
            Response = "SUMMARY:\nWe fought together at Omor and grew to trust one another. He saved my life there."
        };
        var memory = MemoryWithTurns(10);

        var compressed = await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 4);

        Assert.True(compressed);
        Assert.Equal(4, memory.RecentTurns.Count);
        Assert.Equal("p6", memory.RecentTurns[0].PlayerLine);
        Assert.Contains("Omor", memory.Summary);
        Assert.Contains("saved my life", memory.Summary);
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

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        var prompt = client.LastRequest![0].Content;
        // Whole deep memory is visible so they can update it with full context.
        Assert.Contains("Old friends from the north.", prompt);
        // Oldest turns are the ones being folded in.
        Assert.Contains("The moments now fading", prompt);
        Assert.Contains("p0", prompt);
        Assert.Contains("p3", prompt);
        // The kept turns are shown as still-fresh context, not folded in.
        Assert.Contains("Still fresh in my mind", prompt);
        Assert.Contains("p4", prompt);
        Assert.Contains("p5", prompt);
    }

    [Fact]
    public async Task ReflectAsync_WithNothingToFold_StillRewritesSummaryAndKeepsAllTurns()
    {
        var client = new FakeChatClient
        {
            Response = "SUMMARY:\nOn reflection, we are not yet wed, but I hope for it. We are betrothed, no more."
        };
        var memory = MemoryWithTurns(3);
        memory.Summary = "An old, stale summary.";

        // keepMostRecent >= turn count, so there is nothing old enough to fold away.
        var reflected = await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5);

        Assert.True(reflected);
        Assert.NotNull(client.LastRequest); // she actually re-thinks (an LLM call happened)
        Assert.Equal(3, memory.RecentTurns.Count); // no turns dropped
        Assert.Contains("not yet wed", memory.Summary); // memory rewritten whole
        Assert.DoesNotContain("stale", memory.Summary);
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
    public async Task CompressAsync_IsTheNpcsOwnFirstPersonMind_NoNarratorVoice()
    {
        // The Angel narrator is retired (2026.08.07): memory work is her own inner monologue. The
        // voice name is kept only to attribute legacy Angel turns in the transcript — it must never
        // speak the prompt itself.
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: "Muse");

        var prompt = client.LastRequest![0].Content;
        Assert.StartsWith("(Within my own mind — I, Gafnir:", prompt);
        Assert.Contains("what to carry forward and what to let go", prompt);
        Assert.DoesNotContain("Muse", prompt);   // no legacy turns folded, so the voice appears nowhere
        Assert.Contains("I set it down in exactly this shape:", prompt);
        // Kept the machine-readable contract the parser depends on.
        Assert.Contains("SUMMARY:", prompt);
        // The keyed-notes experiment lived for one day (2026.08.27) and was reverted: the deep
        // memory is prose again, and nothing asks her for a shelf of keys.
        Assert.DoesNotContain("BITES:", prompt);
    }

    [Fact]
    public void BuildCompressionRequest_AsksForNeitherTruthsNorAims()
    {
        // Both lists were retired 2026.08.08; the rolling memory carries the whole of it now. Asking
        // for them again would resurrect exactly the cramped, repeated shape we removed.
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildCompressionRequest(memory, memory.RecentTurns)[0].Content;

        Assert.DoesNotContain("FACTS:", prompt);
        Assert.DoesNotContain("GOALS:", prompt);
    }

    [Fact]
    public void BuildCompressionRequest_WarnsHerTheMemoryIsWrittenWhole_AndInvitesTheParticulars()
    {
        // The load-bearing half of the ask. Without the "what I do not set down fades" warning every
        // pass quietly erodes what the retired truths used to nail down; without the invitation to
        // the particulars she writes a bare précis. Change this wording deliberately, never in passing.
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildCompressionRequest(memory, memory.RecentTurns)[0].Content;

        // The memory is written WHOLE, so the warning must stand or each pass quietly erodes what
        // she simply did not think to re-copy.
        Assert.Contains("I write it whole each time", prompt);
        Assert.Contains("fades from me", prompt);
        Assert.Contains("the story truly asks", prompt);
        // And the invitation to the PARTICULARS, without which she writes a bare précis.
        Assert.Contains("what was said and promised between us", prompt);
        // The anti-hoarding clause from the token audit survives the revert: gifts are remembered
        // for what they meant, never as an itemised gear ledger.
        Assert.Contains("Ledgers I do not hoard", prompt);
        // The deep memory opens seeded with her own backstory (2026.08.08): without this clause the
        // "all I carry of them" framing would erode her past by shape, not by her choice.
        Assert.Contains("my own road", prompt);
        Assert.Contains("as I choose", prompt);
    }

    [Fact]
    public void BuildCompressionRequest_AttributesLegacyAngelTurnsByTheVoiceName()
    {
        // Turns recorded by the retired narrator (older saves) must still be attributed truthfully
        // when folded in, so the summary never mistakes the old voice for the player.
        var memory = new NpcMemory { NpcId = "lord_1", NpcName = "Gafnir" };
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.AngelSpeaker,
            PlayerLine = "Vulgrim comes to you again and greets you.",
            NpcLine = "Well met!",
            GameDay = 1,
        });

        var prompt = MemoryCompressor.BuildCompressionRequest(memory, memory.RecentTurns, systemVoiceName: "Muse")[0].Content;

        Assert.Contains("Muse said: Vulgrim comes to you again", prompt);
        Assert.Contains("I answered: Well met!", prompt);
    }

    [Fact]
    public async Task CompressAsync_NeverTouchesTheRetiredTruthsField()
    {
        // Whatever an older save still carries there stays exactly as it lies: unread, unwritten,
        // and above all not destroyed by the first compression after the update.
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(6);
        memory.KnownFacts.Add("a truth she held under the old shape");

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2);

        Assert.Equal(new[] { "a truth she held under the old shape" }, memory.KnownFacts);
        // ...and it is not read back to her either.
        Assert.DoesNotContain("a truth she held under the old shape", client.LastRequest![0].Content);
    }

    [Fact]
    public void BuildCompressionRequest_DefaultsTheLegacyAttributionVoiceToAngel()
    {
        // With no voice given, a legacy narrator turn is attributed by the old default name — the
        // only place the retired Angel still appears, and only for turns that already carry it.
        var memory = new NpcMemory { NpcId = "lord_1", NpcName = "Gafnir" };
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.AngelSpeaker,
            PlayerLine = "Do you wish to write to Vulgrim?",
            NpcLine = "Yes.",
            GameDay = 1,
        });

        var prompt = MemoryCompressor.BuildCompressionRequest(memory, memory.RecentTurns)[0].Content;

        Assert.Contains("Angel said: Do you wish to write to Vulgrim?", prompt);
    }

    [Fact]
    public void ParseResponse_HandlesASummaryAlone()
    {
        var result = MemoryCompressor.ParseResponse("SUMMARY:\nJust a summary.");

        Assert.Equal("Just a summary.", result.Summary);
        Assert.Null(result.Self);
    }

    [Fact]
    public void ParseResponse_NoHeaders_TreatsWholeTextAsSummary()
    {
        var result = MemoryCompressor.ParseResponse("The model just wrote prose.");

        Assert.Equal("The model just wrote prose.", result.Summary);
    }

    [Fact]
    public void ParseResponse_AStrayRetiredSection_NeverSiltsUpTheSummaryOrTheSelf()
    {
        // Nothing asks for FACTS/GOALS any more, but a model with the old habit may volunteer one.
        // It is discarded — and, crucially, still BOUNDS its neighbours, so no bullet list can leak
        // into the memory or the self.
        var result = MemoryCompressor.ParseResponse(
            "SUMMARY:\nWe spoke of the war.\nFACTS:\n- The player spared my brother\nSELF:\nI am wearier than I was.\nGOALS:\n- to go home");

        Assert.Equal("We spoke of the war.", result.Summary);
        Assert.Equal("I am wearier than I was.", result.Self);
    }

    [Fact]
    public void ParseResponse_SelfSection_IsExtracted()
    {
        var result = MemoryCompressor.ParseResponse(
            "SUMMARY:\nWe spoke of the war.\nSELF:\nI am wearier than I was, but I still hope.");

        Assert.Equal("We spoke of the war.", result.Summary);
        Assert.Equal("I am wearier than I was, but I still hope.", result.Self);
    }

    [Fact]
    public void ParseResponse_NoSelfSection_LeavesSelfNull()
    {
        var result = MemoryCompressor.ParseResponse("SUMMARY:\nok");

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

        Assert.Contains("who have I become", prompt);
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

    [Fact]
    public void BuildReflectionRequest_AsksForNeitherTruthsNorAims()
    {
        // Both were retired 2026.08.08 — the reflection settles the rolling memory and the self,
        // nothing else. (The retired blocks read the same lists back to her that the memory held.)
        var memory = MemoryWithTurns(2);

        var prompt = MemoryCompressor.BuildReflectionRequest(
            memory, System.Array.Empty<ConversationTurn>(), systemVoiceName: null, selfText: "I am a cautious soul.")[0].Content;

        Assert.DoesNotContain("FACTS:", prompt);
        Assert.DoesNotContain("GOALS:", prompt);
        Assert.DoesNotContain("what I strive for", prompt);
        Assert.DoesNotContain("Truths I already hold", prompt);
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

    [Fact]
    public void ParseResponse_ASummaryCutOffMidWord_FallsBackToTheLastWholeSentence()
    {
        // The Sibylla case (2026.08.08): the memory-write call ran out of output budget and her
        // memory was saved ending "…хора от Южната империя, разг". It is written whole each time,
        // so that severed tail would be read back to her forever.
        var result = MemoryCompressor.ParseResponse(
            "SUMMARY:\nWe rode north and took the ford at dawn, and he kept his word about the grain. "
            + "In Onira we sold what the wagons could spare and he asked me twice whether I was well. "
            + "I told him the truth, which is that I was not, and that I would say so again when it mattered. "
            + "He kept his word about the grain. "
            + "On the road we saw Vealos burning, and the company of Sihanis, southerners, spo");

        Assert.EndsWith("He kept his word about the grain.", result.Summary);
    }

    [Theory]
    // Already whole — nothing is touched, closing marks and all.
    [InlineData("She kept her word.", "She kept her word.")]
    [InlineData("He asked me: \"Will you stay?\"", "He asked me: \"Will you stay?\"")]
    [InlineData("Запомних боя като „Схватката край Корения“.", "Запомних боя като „Схватката край Корения“.")]
    // A severed tail is dropped back to the last finished sentence.
    [InlineData("One whole thought. And then a second one. But this one was cu",
                "One whole thought. And then a second one.")]
    // Refusing to amputate: with no sentence end anywhere near, a strange memory beats a mangled one.
    [InlineData("A short opener. and then a very long unpunctuated stretch of prose that simply never closes and would lose almost everything",
                "A short opener. and then a very long unpunctuated stretch of prose that simply never closes and would lose almost everything")]
    [InlineData("no punctuation at all", "no punctuation at all")]
    [InlineData("", "")]
    public void TrimToLastCompleteSentence_CutsOnlyASeveredTail(string summary, string expected)
    {
        Assert.Equal(expected, MemoryCompressor.TrimToLastCompleteSentence(summary));
    }

    // ---------------- THE TONGUE (2026.08.17) ----------------
    // These two calls see neither the sheet nor the world prompt, so their language came from the
    // transcript alone and a weak model drifted to English mid-summary — in the two most load-bearing
    // generated texts the mod holds.

    [Fact]
    public async Task CompressionPrompt_EndsOnTheTongueRule()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(8);

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 3);
        var prompt = client.LastRequest![0].Content.TrimEnd();

        Assert.Contains("THE TONGUE", prompt);
        // It must be the LAST thing read, after the SUMMARY: contract — that is the whole placement.
        Assert.True(prompt.LastIndexOf("THE TONGUE", StringComparison.Ordinal)
                    > prompt.LastIndexOf("SUMMARY:", StringComparison.Ordinal));
        // And it must cost nothing: the turns are pointed at, never quoted a second time.
        Assert.DoesNotContain("\"\"\"", prompt);
    }

    [Fact]
    public async Task ReflectionPrompt_PutsTheTongueRuleAfterTheSelfBlock()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nunchanged" };
        var memory = MemoryWithTurns(6);

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 3,
            systemVoiceName: null, self: new NpcSelf { Text = "I am cautious." });
        var prompt = client.LastRequest![0].Content;

        // self.txt drifts into English exactly as readily as the summary does, so the rule rides
        // past the SELF: slot rather than merely past the SUMMARY: contract.
        Assert.True(prompt.LastIndexOf("THE TONGUE", StringComparison.Ordinal)
                    > prompt.LastIndexOf("SELF:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReflectionPrompt_PinsTheUnchangedMarkerAgainstTranslation()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nunchanged" };
        var memory = MemoryWithTurns(6);

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 3,
            systemVoiceName: null, self: new NpcSelf { Text = "I am cautious." });
        var prompt = client.LastRequest![0].Content;

        // The one word here that is PRODUCED rather than copied, and the one the tongue rule would
        // otherwise translate straight past IsUnchangedMarker.
        Assert.Contains("whatever tongue the rest is in: unchanged", prompt);
    }

    [Fact]
    public async Task ReflectAsync_KeepsTheSelf_WhenTheMarkerCameBackTranslated()
    {
        // непроменено = "unchanged". IsUnchangedMarker cannot know every language; what it can know
        // is that a self was asked for as a paragraph and one bare word is never one.
        foreach (var translated in new[] { "непроменено", "unverändert", "**inalterado**" })
        {
            var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + translated };
            var memory = MemoryWithTurns(3);
            var self = new NpcSelf { Text = "I am who I was." };

            await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
                systemVoiceName: null, self: self);

            Assert.Equal("I am who I was.", self.Text);
        }
    }

    [Fact]
    public async Task ReflectAsync_StillTakesAShortRealSelf()
    {
        // The guard is "one bare word", not "short" — a terse two-word self is still a self.
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nСтанах смела." };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "I am timid." };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
            systemVoiceName: null, self: self);

        Assert.Equal("Станах смела.", self.Text);
    }

    [Fact]
    public void ParseResponse_IgnoresHerselfInsideSummarySentence()
    {
        var response = "SUMMARY:\nShe reminded herself: never trust a stranger on the road.\nSELF:\nI walk alone.";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("She reminded herself: never trust a stranger on the road.", parsed.Summary);
        Assert.Equal("I walk alone.", parsed.Self);
    }

    [Fact]
    public void ParseResponse_FindsSelfMidLineAfterFullStop()
    {
        var response = "SUMMARY:\nWe met on the road. SELF:\nI am bold.";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("We met on the road.", parsed.Summary);
        Assert.Equal("I am bold.", parsed.Self);
    }

    [Fact]
    public void ParseResponse_FindsSectionLabelDirectlyAfterChineseWord()
    {
        var response = "SUMMARY:\n我們在戰場上並肩作戰自己SELF:\n我已不再膽怯。";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("我們在戰場上並肩作戰自己", parsed.Summary);
        Assert.Equal("我已不再膽怯。", parsed.Self);
    }

    [Fact]
    public void ParseResponse_FindsSectionLabelWithFullwidthColon()
    {
        var response = "SUMMARY：\n我們在戰場上並肩作戰。\nSELF：\n我是一名忠誠的戰士。";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("我們在戰場上並肩作戰。", parsed.Summary);
        Assert.Equal("我是一名忠誠的戰士。", parsed.Self);
    }

    [Fact]
    public void ParseResponse_SelfWithMidSentenceGoals_PreservesWholeSelf()
    {
        var response = "SUMMARY:\nWe fought together.\nSELF:\nI am a scout who has seen the worst of people. My goals: to go home before I am too old to know the road.";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("I am a scout who has seen the worst of people. My goals: to go home before I am too old to know the road.", parsed.Self);
        Assert.Equal("We fought together.", parsed.Summary);
    }

    [Fact]
    public void ParseResponse_SelfWithLineStartingGoalsFollowedByProse_PreservesWholeSelf()
    {
        var response = "SUMMARY:\nWe fought together.\nSELF:\nI am a scout who has seen the worst of people.\nGoals: to go home before I am too old to know the road.";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("I am a scout who has seen the worst of people.\nGoals: to go home before I am too old to know the road.", parsed.Self);
        Assert.Equal("We fought together.", parsed.Summary);
    }

    [Fact]
    public void ParseResponse_SummaryWithMidSentenceGoals_PreservesWholeSummary()
    {
        var response = "SUMMARY:\nWe met near the castle. My goals: to protect our lands and avenge our fallen comrades.\nSELF:\nI am a loyal guardian.";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("We met near the castle. My goals: to protect our lands and avenge our fallen comrades.", parsed.Summary);
        Assert.Equal("I am a loyal guardian.", parsed.Self);
    }

    [Fact]
    public void ParseResponse_SummaryWithLineStartingFactsFollowedByProse_PreservesWholeSummary()
    {
        var response = "SUMMARY:\nWe met near the castle.\nFacts: they were outnumbered three to one and held their ground.\nSELF:\nI am a loyal guardian.";
        var parsed = MemoryCompressor.ParseResponse(response);
        Assert.Equal("We met near the castle.\nFacts: they were outnumbered three to one and held their ground.", parsed.Summary);
        Assert.Equal("I am a loyal guardian.", parsed.Self);
    }

    [Fact]
    public async Task ReflectAsync_RejectsChineseUnchangedMarker()
    {
        // In Chinese, "未變" or "沒有變化" are single tokens without spaces.
        foreach (var marker in new[] { "未變", "沒有變化", "**沒有變化**", "（未變）", "未改變。" })
        {
            var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + marker };
            var memory = MemoryWithTurns(3);
            var self = new NpcSelf { Text = "我是個謹慎的人。" };

            await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
                systemVoiceName: null, self: self);

            Assert.Equal("我是個謹慎的人。", self.Text);
        }
    }

    [Fact]
    public async Task ReflectAsync_AcceptsChineseSingleParagraphSelf_WithoutWordSpaces()
    {
        // A single continuous Chinese paragraph without any spaces (single token).
        var chineseSelf = "我已不再是當初那個只顧逃命的膽小鬼，在與他們的歷次血戰中，我學會了握緊手中的劍，並誓死守衛身邊的每一位同伴。";
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + chineseSelf };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "我是個謹慎的人。" };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
            systemVoiceName: null, self: self);

        Assert.Equal(chineseSelf, self.Text);
    }

    [Fact]
    public async Task ReflectAsync_AcceptsChineseMultiParagraphSelf()
    {
        var chineseSelf = "我是西比拉，出生於戰火之中。\n\n如今我追隨在隊長身旁，歷經風霜與征戰，早已將這支隊伍視為我的歸宿與榮耀所在。";
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + chineseSelf };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "我是個謹慎的人。" };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
            systemVoiceName: null, self: self);

        Assert.Equal(chineseSelf, self.Text);
    }

    [Fact]
    public async Task ReflectAsync_AcceptsChineseSelfWithMarkdownAndEnglishPunctuation()
    {
        var chineseSelf = "**我是西比拉**，一名在帝國邊境游蕩的傭兵. 我不再信任任何領主, 唯有手中的鋼劍與並肩作戰的同伴才是真實的!";
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + chineseSelf };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "我是個謹慎的人。" };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
            systemVoiceName: null, self: self);

        Assert.Equal(chineseSelf, self.Text);
    }

    [Fact]
    public async Task ReflectAsync_RejectsShortChineseMarkerBelowThreshold()
    {
        // 6 characters (below MinSelfLength = 24)
        var shortMarker = "完全沒有變化";
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + shortMarker };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "我是個謹慎的人。" };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
            systemVoiceName: null, self: self);

        Assert.Equal("我是個謹慎的人。", self.Text);
    }

    [Fact]
    public async Task ReflectAsync_AcceptsKoreanSelf_ThroughWordCountPath()
    {
        // Korean uses spaces between words, so it exercises the original word-count path.
        var koreanSelf = "나는 더 용감해졌고 그들과 함께 싸운다.";
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\n" + koreanSelf };
        var memory = MemoryWithTurns(3);
        var self = new NpcSelf { Text = "I am timid." };

        await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 5,
            systemVoiceName: null, self: self);

        Assert.Equal(koreanSelf, self.Text);
    }

    [Fact]
    public async Task CompressAsync_WithoutSelf_DoesNotAskForSelf()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(5);

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: null, self: null);

        var prompt = client.LastRequest![0].Content;
        Assert.DoesNotContain("SELF:", prompt);
        Assert.DoesNotContain("who have I become", prompt);
    }

    [Fact]
    public async Task CompressAsync_WithSelf_AsksForSelfBesideTheSummary()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nI am a brave warrior now." };
        var memory = MemoryWithTurns(5);
        var self = new NpcSelf { Text = "I am a timid merchant." };

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: null, self: self);

        var prompt = client.LastRequest![0].Content;
        Assert.Contains("SUMMARY:", prompt);
        Assert.Contains("SELF:", prompt);
        Assert.Contains("I am a timid merchant.", prompt);
        Assert.Equal("I am a brave warrior now.", self.Text);
    }

    [Fact]
    public async Task CompressAsync_FirstEverSelf_InvitesWithoutOfferingUnchanged()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok" };
        var memory = MemoryWithTurns(5);
        var self = new NpcSelf { Text = "" };

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: null, self: self);

        var prompt = client.LastRequest![0].Content;
        Assert.Contains("not yet put into words", prompt);
        Assert.DoesNotContain("whatever tongue the rest is in: unchanged", prompt);
    }

    [Fact]
    public async Task CompressAsync_LeavesSelfUnchanged_OnUnchangedKeyword()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nunchanged" };
        var memory = MemoryWithTurns(5);
        var self = new NpcSelf { Text = "I am a wanderer from the west." };

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: null, self: self);

        Assert.Equal("I am a wanderer from the west.", self.Text);
    }

    [Fact]
    public async Task CompressAsync_SelfWithoutSummary_TakesTheSelfAndKeepsEveryTurn()
    {
        var client = new FakeChatClient
        {
            Response = "SELF:\nI have grown bolder through these battles."
        };
        var memory = MemoryWithTurns(6);
        var self = new NpcSelf { Text = "I was once fearful." };

        var compressed = await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 2, systemVoiceName: null, self: self);

        Assert.False(compressed);
        Assert.Equal("I have grown bolder through these battles.", self.Text);
        Assert.Equal(6, memory.RecentTurns.Count);
    }

    [Fact]
    public async Task ReflectAsync_SelfWithoutSummary_TakesTheSelfAndKeepsEveryTurn()
    {
        var client = new FakeChatClient
        {
            Response = "SELF:\nI have grown bolder through these battles."
        };
        var memory = MemoryWithTurns(6);
        var self = new NpcSelf { Text = "I was once fearful." };

        var reflected = await new MemoryCompressor(client).ReflectAsync(memory, keepMostRecent: 2, systemVoiceName: null, self: self);

        Assert.False(reflected);
        Assert.Equal("I have grown bolder through these battles.", self.Text);
        Assert.Equal(6, memory.RecentTurns.Count);
    }

    [Fact]
    public async Task CompressAsync_TongueRuleRidesAfterTheSelfSlot()
    {
        var client = new FakeChatClient { Response = "SUMMARY:\nok\nSELF:\nunchanged" };
        var memory = MemoryWithTurns(6);
        var self = new NpcSelf { Text = "I am an archer." };

        await new MemoryCompressor(client).CompressAsync(memory, keepMostRecent: 3, systemVoiceName: null, self: self);
        var prompt = client.LastRequest![0].Content;

        Assert.True(prompt.LastIndexOf("THE TONGUE", StringComparison.Ordinal)
                    > prompt.LastIndexOf("SELF:", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseResponse_LowercaseSelfInProse_IsNotReadAsALabel()
    {
        // A standalone "self:" clears the word-boundary guard that rejects "herself:", and the reply
        // contract primes the word by asking her, in the same breath, to say who she has become. Read
        // as a label it cut the page in half and saved "colder than the man I met." as her whole self.
        var response = "SUMMARY:\nHe showed me a different self: colder than the man I met.";
        var parsed = MemoryCompressor.ParseResponse(response);

        Assert.Equal("He showed me a different self: colder than the man I met.", parsed.Summary);
        Assert.Null(parsed.Self);
    }

    [Fact]
    public void ParseResponse_LowercaseSelfInProse_DoesNotOutrankTheRealSelfLabel()
    {
        var response = "SUMMARY:\nI have become a harder self: one that does not flinch.\nSELF:\nI am steadier than I was.";
        var parsed = MemoryCompressor.ParseResponse(response);

        Assert.Equal("I have become a harder self: one that does not flinch.", parsed.Summary);
        Assert.Equal("I am steadier than I was.", parsed.Self);
    }

    [Fact]
    public void ParseResponse_LowercaseSummaryInProse_DoesNotBoundTheSelf()
    {
        var response = "SELF:\nI keep my word. In summary: I am my father's daughter.";
        var parsed = MemoryCompressor.ParseResponse(response);

        Assert.Equal("I keep my word. In summary: I am my father's daughter.", parsed.Self);
    }
}
