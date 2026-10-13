using System.Text.RegularExpressions;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// prompts.txt's first keys: the deep memory and the backstory. What matters is that nothing
/// changes for a player who never opens the file, and that what they write is what is sent.
/// </summary>
public class PromptTextsTests
{
    [Fact]
    public void Every_key_is_unique()
    {
        var keys = PromptTexts.All.Select(e => e.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_slot_a_default_fills_is_named_in_its_entry()
    {
        foreach (var entry in PromptTexts.All)
            foreach (Match m in Regex.Matches(entry.Default, @"\{(\w+)\}"))
                Assert.Contains(m.Groups[1].Value, entry.Slots);
    }

    [Fact]
    public void A_fresh_file_reads_back_as_every_shipped_word()
    {
        var pack = PromptPack.Parse(PromptTexts.RenderFile(), PromptTexts.Keys);

        Assert.Empty(pack.Complaints);
        Assert.Empty(pack.UnknownKeys);
        foreach (var entry in PromptTexts.All)
        {
            Assert.True(pack.Has(entry.Key), entry.Key);
            Assert.Equal(entry.Default, PromptTexts.Resolve(pack, entry));
        }
        Assert.Equal(string.Empty, PromptTexts.RenderMissing(pack));
    }

    [Fact]
    public void Only_what_a_file_lacks_is_offered_to_append()
    {
        var pack = PromptPack.Parse("sheet.deep_memory.heading = \"Mine\"", PromptTexts.Keys);
        var missing = PromptTexts.RenderMissing(pack);

        Assert.DoesNotContain("sheet.deep_memory.heading =", missing);
        Assert.Contains("sheet.deep_memory.as_of =", missing);
        Assert.Contains("backstory.world_story =", missing);
    }

    [Fact]
    public void The_players_words_win_and_an_emptied_one_stays_empty()
    {
        var entry = PromptTexts.DeepMemoryOwnStory;
        Assert.Equal("My road:", PromptTexts.Resolve(PromptPack.Parse($"{entry.Key} = \"My road:\""), entry));
        Assert.Equal(string.Empty, PromptTexts.Resolve(PromptPack.Parse($"{entry.Key} = \"\""), entry));
        Assert.Equal(entry.Default, PromptTexts.Resolve(PromptPack.Empty, entry));
    }

    [Fact]
    public void Words_a_default_shipped_with_before_count_as_untouched()
    {
        var entry = new PromptTexts.Entry("test.key", "New words.", "a note", previously: new[] { "Old words." });

        Assert.Equal("New words.", PromptTexts.Resolve(PromptPack.Parse("test.key = \"Old words.\""), entry));
        Assert.Equal("Mine.", PromptTexts.Resolve(PromptPack.Parse("test.key = \"Mine.\""), entry));
    }

    [Fact]
    public void Slots_fill_by_name_and_a_removed_one_is_simply_not_there()
    {
        Assert.Equal("What Ilya is to me:", PromptTexts.FillSlots("What {player} is to me{as_of}:", "player", "Ilya", "as_of", ""));
        Assert.Equal("Of me:", PromptTexts.FillSlots("Of me:", "player", "Ilya"));
    }
}

/// <summary>
/// The same keys, end to end through the builders. <see cref="PromptTexts.Pack"/> is one value
/// for the whole process, so these run on their own: in parallel they would reword the sheets
/// every other test is asserting on.
/// </summary>
[CollectionDefinition(nameof(PromptTextsEndToEnd), DisableParallelization = true)]
public class PromptTextsEndToEnd
{
}

[Collection(nameof(PromptTextsEndToEnd))]
public class PromptTextsInTheBuildersTests
{
    private static void WithPack(string file, Action test)
    {
        PromptTexts.Pack = PromptPack.Parse(file, PromptTexts.Keys);
        try { test(); }
        finally { PromptTexts.Pack = PromptPack.Empty; }
    }

    [Fact]
    public void The_sheet_heads_the_deep_memory_with_the_players_words()
    {
        WithPack("sheet.deep_memory.heading = \"All {player} has been to me{as_of}:\"\n"
                 + "sheet.deep_memory.as_of = \" (since {date})\"", () =>
        {
            var memory = new NpcMemory { Summary = "We rode to Omor.", SummaryAsOf = "1087.01.18" };
            var system = new PromptBuilder().Build(new NpcPersona { Name = "Gafnir" }, memory, "", "Vulgrim", "Hello")[0].Content;

            Assert.Contains("All Vulgrim has been to me (since 1087.01.18):", system);
            Assert.DoesNotContain("What Vulgrim is to me", system);
        });
    }

    [Fact]
    public void Gathering_a_memory_uses_the_players_words_and_keeps_the_label()
    {
        WithPack("memory.gather.spirit = \"I keep only the kind things.\"\n"
                 + "memory.contract.summary = \"<what I keep>\"\n"
                 + "memory.contract.whole = \"\"", () =>
        {
            var memory = new NpcMemory { NpcName = "Gafnir", Summary = "Old friends." };
            for (int i = 0; i < 4; i++)
                memory.AddTurn(new ConversationTurn { PlayerLine = $"p{i}", NpcLine = $"n{i}", GameDay = i });
            var prompt = MemoryCompressor.BuildCompressionRequest(memory, memory.RecentTurns.Take(2).ToList())[0].Content;

            Assert.Contains("I keep only the kind things.", prompt);
            Assert.Contains("SUMMARY:\n<what I keep>", prompt.Replace("\r\n", "\n"));
            Assert.DoesNotContain("I write it whole each time", prompt);
            Assert.DoesNotContain("Time moves on", prompt);
        });
    }

    [Fact]
    public void A_backstory_is_framed_in_the_players_words()
    {
        WithPack("backstory.world_story = \"The world says this of me: {story}\"\n"
                 + "backstory.fame.famed = \"Everyone knows {player}.\"", () =>
        {
            Assert.Equal("The world says this of me: She holds Varcheg.", StorySeedFormatter.FromWorldStory("She holds Varcheg."));
            Assert.Equal("Everyone knows Vulgrim.", StorySeedFormatter.FromPlayerFame("Vulgrim", 1000f));
            Assert.StartsWith("Of Vulgrim, before ever we spoke, the world had already told me much",
                              StorySeedFormatter.FromPlayerFame("Vulgrim", 400f));
        });
    }
}
