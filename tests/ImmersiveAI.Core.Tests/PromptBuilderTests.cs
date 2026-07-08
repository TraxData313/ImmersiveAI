using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

public class PromptBuilderTests
{
    private static NpcPersona Persona() => new()
    {
        Name = "Gafnir",
        RoleDescription = "A Sturgian lord of clan Vidgrip.",
        PersonalityDescription = "Calculating, cautious, values loyalty.",
        SpeechStyle = "Terse northern speech, dry humor, never flowery.",
        CustomInstructions = "You distrust Imperial nobility."
    };

    [Fact]
    public void Build_ProducesSystemThenHistoryThenNewInput()
    {
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn { PlayerLine = "Hail, Gafnir", NpcLine = "Hail, stranger." });

        var messages = new PromptBuilder().Build(Persona(), memory, "In the tavern of Varcheg.", "Vulgrim", "Will you ride with me?");

        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Equal("Hail, Gafnir", messages[1].Content);
        Assert.Equal(ChatRole.Assistant, messages[2].Role);
        Assert.Equal(ChatRole.User, messages[3].Role);
        Assert.Equal("Will you ride with me?", messages[3].Content);
    }

    [Fact]
    public void SystemPrompt_ContainsPersonaMemoryAndScene()
    {
        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor." };
        memory.KnownFacts.Add("Vulgrim rules Sargot");

        var system = new PromptBuilder()
            .Build(Persona(), memory, "On the road near Balgard.", "Vulgrim", "Hello")[0].Content;

        Assert.Contains("Gafnir", system);
        Assert.Contains("Terse northern speech", system);
        Assert.Contains("On the road near Balgard.", system);
        Assert.Contains("You fought beside Vulgrim at Omor.", system);
        Assert.Contains("Vulgrim rules Sargot", system);
        Assert.Contains("You distrust Imperial nobility.", system);
        Assert.Contains("never mention being an AI", system);
    }

    [Fact]
    public void SystemPrompt_OmitsEmptySections()
    {
        var persona = new NpcPersona { Name = "Orvi" };
        var memory = new NpcMemory();

        var system = new PromptBuilder().Build(persona, memory, "", "Vulgrim", "Hello")[0].Content;

        Assert.DoesNotContain("What you remember", system);
        Assert.DoesNotContain("Facts you know:", system);
        Assert.DoesNotContain("Current situation:", system);
    }

    [Fact]
    public void BuildRecap_EndsWithRecapDirectiveAfterHistory()
    {
        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor." };
        memory.AddTurn(new ConversationTurn { PlayerLine = "Well met", NpcLine = "Aye." });

        var messages = new PromptBuilder().BuildRecap(Persona(), memory, "In the tavern.", "Vulgrim");

        Assert.Equal(ChatRole.System, messages[0].Role);
        // History is replayed as real user/assistant turns before the directive.
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Equal("Well met", messages[1].Content);
        Assert.Equal(ChatRole.Assistant, messages[2].Role);

        var last = messages[^1];
        Assert.Equal(ChatRole.User, last.Role);
        Assert.Contains("Vulgrim", last.Content);
        Assert.Contains("last spoke about", last.Content);
        Assert.DoesNotContain("first time", last.Content);
    }

    [Fact]
    public void BuildRecap_WithNoHistory_AsksForFirstMeetingGreeting()
    {
        var messages = new PromptBuilder().BuildRecap(new NpcPersona { Name = "Orvi" }, new NpcMemory(), "", "Vulgrim");

        // No turns, so only the system prompt and the directive.
        Assert.Equal(2, messages.Count);
        var directive = messages[^1].Content;
        Assert.Contains("never spoken", directive);
        Assert.Contains("first time", directive);
        Assert.DoesNotContain("last spoke about", directive);
    }
}
