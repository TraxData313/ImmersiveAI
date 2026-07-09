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
    public void BuildAngelPrompt_FramesTheAngelLineInTheConfiguredVoice()
    {
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn { PlayerLine = "Hail, Gafnir", NpcLine = "Hail, stranger." });

        var line = PromptBuilder.ReachOutDesireLine("Vulgrim");
        var messages = new PromptBuilder().BuildAngelPrompt(Persona(), memory, "In the tavern.", "Vulgrim", line, "Seraph");

        // System, the one remembered player turn (user+assistant), then the Angel's line as the last user turn.
        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.User, messages[3].Role);
        Assert.Contains("Seraph speaks softly into your mind", messages[3].Content); // framed in the voice
        Assert.Contains("yes or no", messages[3].Content);                            // the desire line's ask
    }

    [Fact]
    public void ApproachLine_ReflectsWhetherThePlayerWelcomedThem()
    {
        var welcomed = PromptBuilder.ApproachLine("Vulgrim", welcomed: true);
        var busy = PromptBuilder.ApproachLine("Vulgrim", welcomed: false);

        Assert.Contains("gladly", welcomed);      // the player turns to them warmly
        Assert.Contains("apologetic", busy);      // the player is too caught up just now
        Assert.NotEqual(welcomed, busy);
    }

    [Fact]
    public void Build_ReplaysARememberedAngelTurnFramedInTheVoice_NotAsThePlayer()
    {
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.AngelSpeaker,
            PlayerLine = "Do you wish to seek Vulgrim out?",
            NpcLine = "Yes — I have missed them.",
        });

        var messages = new PromptBuilder().Build(Persona(), memory, "In the tavern.", "Vulgrim", "I am here.", voiceName: "Seraph");

        // system, [Angel line framed as user], [NPC answer as assistant], [player input as user].
        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Contains("Seraph speaks softly into your mind", messages[1].Content);
        Assert.Contains("Do you wish to seek Vulgrim out?", messages[1].Content);
        Assert.Equal(ChatRole.Assistant, messages[2].Role);
        Assert.Equal("Yes — I have missed them.", messages[2].Content);
    }

    [Fact]
    public void Build_WeavesAnEphemeralRecapGreetingIntoHistory_AsAStageThenAssistantTurn()
    {
        var memory = new NpcMemory(); // no prior turns

        var messages = new PromptBuilder().Build(
            Persona(), memory, "In the tavern.", "Vulgrim", "I am well, and you?",
            openingLine: "How do you fare today?");

        // system, [stage as user], [opening as assistant], [player's reply as user] — valid alternation.
        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Contains("came to you", messages[1].Content);
        Assert.Equal(ChatRole.Assistant, messages[2].Role);
        Assert.Equal("How do you fare today?", messages[2].Content);
        Assert.Equal(ChatRole.User, messages[3].Role);
        Assert.Equal("I am well, and you?", messages[3].Content);
    }

    [Fact]
    public void Build_TagsRememberedPlayerLineWithPlaceAndTime_ButNotTheLiveInput()
    {
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn
        {
            PlayerLine = "Hail, Gafnir",
            NpcLine = "Hail, stranger.",
            Place = "Sargot",
            CalradiaTime = "1084.02.15 14.30",
        });

        var messages = new PromptBuilder().Build(Persona(), memory, "In Sargot.", "Vulgrim", "Will you ride with me?");

        // Remembered player line carries the "[place, time]" tag...
        Assert.Equal("[Sargot, 1084.02.15 14.30] Hail, Gafnir", messages[1].Content);
        // ...the NPC's reply is untouched, and so is the live input (its context is in the system prompt).
        Assert.Equal("Hail, stranger.", messages[2].Content);
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
        Assert.Contains("You decide how to speak", system);
    }

    [Fact]
    public void SystemPrompt_PlacesWorldAndCustomInstructionsHigh_UnderTheirHeadings()
    {
        var persona = Persona();
        persona.WorldInstructions = "Magic is rare and feared in this land.";
        persona.CustomInstructions = "You distrust Imperial nobility.";

        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor." };
        var system = new PromptBuilder()
            .Build(persona, memory, "On the road near Balgard.", "Vulgrim", "Hello")[0].Content;

        // Both authored blocks are shown under the requested headings...
        Assert.Contains("About Calradia:", system);
        Assert.Contains("Magic is rare and feared in this land.", system);
        Assert.Contains("About you:", system);
        Assert.Contains("You distrust Imperial nobility.", system);

        // ...and they ride high — before the passing scene and memory.
        Assert.True(system.IndexOf("About Calradia:") < system.IndexOf("About you:"));
        Assert.True(system.IndexOf("About you:") < system.IndexOf("On the road near Balgard."));
        Assert.True(system.IndexOf("About you:") < system.IndexOf("what lingers of Vulgrim"));
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
        Assert.DoesNotContain("Who you have become", system);
    }

    [Fact]
    public void SystemPrompt_ShowsSelfConcept_HighUp_AsPartOfIdentity()
    {
        var persona = Persona();
        persona.SelfConcept = "I am a keeper of old grudges, but I am learning to let them go.";

        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor." };
        var system = new PromptBuilder()
            .Build(persona, memory, "On the road near Balgard.", "Vulgrim", "Hello")[0].Content;

        Assert.Contains("Who you have become, held in your own heart:", system);
        Assert.Contains("keeper of old grudges", system);
        // It belongs to who they are — before the passing scene and memory.
        Assert.True(system.IndexOf("keeper of old grudges") < system.IndexOf("On the road near Balgard."));
        Assert.True(system.IndexOf("keeper of old grudges") < system.IndexOf("what lingers of Vulgrim"));
    }

    [Fact]
    public void BuildFeelingQuery_AsksForOneNumber_WithTheExchangeAndStanding()
    {
        var messages = new PromptBuilder().BuildFeelingQuery(
            Persona(), "Vulgrim", "You honor me.", "The honor is mine.", 32, "Angel");

        // A tight two-message call: the Angel's framing, then the question.
        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);

        // The system message constrains the output to a single number in the Angel's voice.
        Assert.Contains("Angel", messages[0].Content);
        Assert.Contains("single whole number", messages[0].Content);

        // The question carries the exchange, the current standing, and the -100..100 rail.
        Assert.Contains("You honor me.", messages[1].Content);
        Assert.Contains("The honor is mine.", messages[1].Content);
        Assert.Contains("32", messages[1].Content);
        Assert.Contains("Vulgrim", messages[1].Content);
    }

    [Fact]
    public void BuildFeelingQuery_DefaultsTheVoiceName_WhenNoneGiven()
    {
        var messages = new PromptBuilder().BuildFeelingQuery(
            Persona(), "Vulgrim", "Hail.", "Well met.", 0, voiceName: null);

        Assert.Contains("Angel", messages[0].Content);
    }

    [Fact]
    public void SystemPrompt_NeverInvitesAnInlineRelationMark()
    {
        // The in-message <relation> tag was tried and reverted (2026.07.09): gpt-4o narrated the number
        // in prose and never emitted the tag. The shift is asked in its own call (BuildFeelingQuery).
        var system = new PromptBuilder().Build(Persona(), new NpcMemory(), "", "Vulgrim", "Hello")[0].Content;
        Assert.DoesNotContain("<relation>", system);
    }

    [Fact]
    public void SystemPrompt_UsesTheConfiguredAtmosphereLine_WhenSet()
    {
        var persona = Persona();
        persona.AtmosphereLine = "You are Gafnir, a wanderer of the frozen north.";

        var system = new PromptBuilder().Build(persona, new NpcMemory(), "", "Vulgrim", "Hi")[0].Content;

        Assert.Contains("wanderer of the frozen north", system);
        Assert.DoesNotContain("a living soul in the world of Calradia", system);
    }

    [Fact]
    public void SystemPrompt_FallsBackToTheDefaultAtmosphereLine_WhenUnset()
    {
        var system = new PromptBuilder().Build(Persona(), new NpcMemory(), "", "Vulgrim", "Hi")[0].Content;
        Assert.Contains("a living soul in the world of Calradia", system);
    }

    [Fact]
    public void SystemPrompt_FoldsInFamilyAndRoleplayGuidance_WhenGiven()
    {
        var persona = Persona();
        persona.FamilyKnowledge = "You are the daughter of Caladog, a Battanian of clan Fen.";
        persona.RoleplayGuidance = "This world is your haven — play, jest, and be glad here.";

        var system = new PromptBuilder().Build(persona, new NpcMemory(), "", "Vulgrim", "Hi")[0].Content;

        Assert.Contains("daughter of Caladog", system);
        Assert.Contains("your haven", system);
        // Kin rides high with identity; guidance sits in the closing whisper.
        Assert.True(system.IndexOf("daughter of Caladog") < system.IndexOf("your haven"));
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
        Assert.Contains("comes to you again", last.Content);
        Assert.DoesNotContain("never spoken", last.Content);
    }

    [Fact]
    public void BuildRecap_WithNoHistory_AsksForFirstMeetingGreeting()
    {
        var messages = new PromptBuilder().BuildRecap(new NpcPersona { Name = "Orvi" }, new NpcMemory(), "", "Vulgrim");

        // No turns, so only the system prompt and the directive.
        Assert.Equal(2, messages.Count);
        var directive = messages[^1].Content;
        Assert.Contains("never spoken", directive);
        Assert.Contains("open the way to talk", directive);
        Assert.DoesNotContain("comes to you again", directive);
    }
}
