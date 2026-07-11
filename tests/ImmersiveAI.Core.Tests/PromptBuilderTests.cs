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
    public void Build_FoldsInTheNpcsAims_AsMyGoals()
    {
        var persona = Persona();
        persona.Goals = new() { "Win back my father's hall", "See my sister safely wed" };

        var system = new PromptBuilder().Build(persona, new NpcMemory(), "In the tavern.", "Vulgrim", "Hello")[0].Content;

        Assert.Contains("My goals are:", system);
        Assert.Contains("Win back my father's hall", system);
        Assert.Contains("See my sister safely wed", system);
    }

    [Fact]
    public void Build_OffersTheTendGoalsWhisper_OnlyWhenTheAimsHandRidesAlong()
    {
        var withTool = Persona();
        withTool.CanTendGoals = true;
        var on = new PromptBuilder().Build(withTool, new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.Contains("My aims are mine", on);

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.DoesNotContain("My aims are mine", off);
    }

    [Fact]
    public void Build_OffersTheHoldTruthWhisper_OnlyWhenTheTruthsHandRidesAlong()
    {
        var withTool = Persona();
        withTool.CanHoldTruths = true;
        var on = new PromptBuilder().Build(withTool, new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.Contains("among the truths I hold", on);

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.DoesNotContain("among the truths I hold", off);
    }

    [Fact]
    public void Build_FoldsInTheCrafts_AndOffersTheFieldWhisperOnlyWhenItRides()
    {
        var persona = Persona();
        persona.Crafts = "What my hands and wits are honestly good at: masterly in Medicine.";
        persona.CanSurveyField = true;
        var on = new PromptBuilder().Build(persona, new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.Contains("masterly in Medicine", on);
        Assert.Contains("cast my eyes over the country", on);

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.DoesNotContain("cast my eyes over the country", off);
    }

    [Fact]
    public void ComposeLetterLine_InService_StaysARecognizedLetterBeat()
    {
        // The field-report invitation is appended AFTER the marker fragment, so both variants must
        // keep being recognized as compose beats (recorded memories depend on the prefix forever).
        var plain = PromptBuilder.ComposeLetterLine("Vulgrim");
        var report = PromptBuilder.ComposeLetterLine("Vulgrim", inService: true);

        Assert.True(PromptBuilder.IsComposeLetterBeat(plain));
        Assert.True(PromptBuilder.IsComposeLetterBeat(report));
        Assert.Contains("as a captain reports home", report);
        Assert.DoesNotContain("as a captain reports home", plain);
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
    public void Build_TagsARememberedAngelTurnWithPlaceAndTime_LikeAPlayerLine()
    {
        // The arrival/letter/reaching-out beats must not float in time when replayed: she should see
        // WHEN the player came to her just as she sees when a remembered player line was spoken.
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.AngelSpeaker,
            PlayerLine = "Vulgrim comes to you again and greets you.",
            NpcLine = "Well met!",
            Place = "Ostican",
            CalradiaTime = "1087.01.01 10.20",
        });

        var messages = new PromptBuilder().Build(Persona(), memory, "In Ostican.", "Vulgrim", "Hello");

        Assert.StartsWith("[Ostican, 1087.01.01 10.20] Angel speaks softly into your mind", messages[1].Content);
        Assert.Contains("Vulgrim comes to you again", messages[1].Content);
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
    public void Build_FoldsASilentMeetingBeatIntoTheNextIncomingLine_RolesStayAlternating()
    {
        // A meeting noted without words (NpcLine empty) cannot stand as its own user/assistant pair —
        // both backends demand alternation — so it rides at the head of the next incoming message.
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.AngelSpeaker,
            PlayerLine = PromptBuilder.MeetingLine("Vulgrim", firstMeeting: true),
            NpcLine = string.Empty,
            Place = "Sargot",
        });
        memory.AddTurn(new ConversationTurn { PlayerLine = "Hail again", NpcLine = "Well met." });

        var messages = new PromptBuilder().Build(Persona(), memory, "In Sargot.", "Vulgrim", "How fare you?");

        // system, [meeting note + next player line as ONE user message], [reply], [live input].
        Assert.Equal(4, messages.Count);
        Assert.Contains("met and spoke face to face for the first time", messages[1].Content);
        Assert.Contains("Hail again", messages[1].Content);
        Assert.Equal(ChatRole.Assistant, messages[2].Role);
        Assert.Equal("How fare you?", messages[3].Content);
    }

    [Fact]
    public void Build_CarriesATrailingSilentBeatIntoTheLiveInput()
    {
        // The meeting was the LAST thing that happened — nothing spoken since — so it rides into
        // the live incoming line: she reads of the meeting in the same breath as the new words.
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.AngelSpeaker,
            PlayerLine = PromptBuilder.MeetingLine("Vulgrim", firstMeeting: false),
            NpcLine = string.Empty,
        });

        var messages = new PromptBuilder().Build(Persona(), memory, "In Sargot.", "Vulgrim", "Hello again");

        // system + one combined user message; no empty assistant message anywhere.
        Assert.Equal(2, messages.Count);
        Assert.Contains("came and spoke with you awhile", messages[1].Content);
        Assert.EndsWith("Hello again", messages[1].Content);
        Assert.DoesNotContain(messages, m => m.Role == ChatRole.Assistant);
    }

    [Fact]
    public void MeetingLine_IsRecognizedByIsMeetingLine_ProseIsNot()
    {
        Assert.True(PromptBuilder.IsMeetingLine(PromptBuilder.MeetingLine("Vulgrim", firstMeeting: true)));
        Assert.True(PromptBuilder.IsMeetingLine(PromptBuilder.MeetingLine("Vulgrim", firstMeeting: false)));
        Assert.False(PromptBuilder.IsMeetingLine("Vulgrim comes to you again and greets you."));
        Assert.False(PromptBuilder.IsMeetingLine(null));
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
        Assert.Contains("How should I speak:", system);
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
        Assert.Contains("About me:", system);
        Assert.Contains("You distrust Imperial nobility.", system);

        // ...and they ride high — before the passing scene and memory.
        Assert.True(system.IndexOf("About Calradia:") < system.IndexOf("About me:"));
        Assert.True(system.IndexOf("About me:") < system.IndexOf("On the road near Balgard."));
        Assert.True(system.IndexOf("About me:") < system.IndexOf("What Vulgrim is to me"));
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

        Assert.Contains("Who I have become:", system);
        Assert.Contains("keeper of old grudges", system);
        // It belongs to who they are — before the passing scene and memory.
        Assert.True(system.IndexOf("keeper of old grudges") < system.IndexOf("On the road near Balgard."));
        Assert.True(system.IndexOf("keeper of old grudges") < system.IndexOf("What Vulgrim is to me"));
    }

    [Fact]
    public void BuildFeelingQuery_AsksForOneNumber_WithTheExchange()
    {
        var messages = new PromptBuilder().BuildFeelingQuery(
            Persona(), "Vulgrim", "You honor me.", "The honor is mine.", "Angel");

        // A tight two-message call: the Angel's framing, then the question.
        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);

        // The system message constrains the output to a single number in the Angel's voice.
        Assert.Contains("Angel", messages[0].Content);
        Assert.Contains("single whole number", messages[0].Content);

        // The question carries the exchange and asks only for the movement.
        Assert.Contains("You honor me.", messages[1].Content);
        Assert.Contains("The honor is mine.", messages[1].Content);
        Assert.Contains("Vulgrim", messages[1].Content);
    }

    [Fact]
    public void BuildFeelingQuery_NeverRevealsTheCurrentStanding()
    {
        // The heart is asked only how the moment moved it — never where it currently rests — so a
        // soul already at the deepest love can still be moved, and the shown shift is the impact.
        var messages = new PromptBuilder().BuildFeelingQuery(
            Persona(), "Vulgrim", "Hail.", "Well met.", "Angel");

        Assert.DoesNotContain("your regard for", messages[1].Content);
        Assert.DoesNotContain("rests at", messages[1].Content);
    }

    [Fact]
    public void BuildFeelingQuery_DefaultsTheVoiceName_WhenNoneGiven()
    {
        var messages = new PromptBuilder().BuildFeelingQuery(
            Persona(), "Vulgrim", "Hail.", "Well met.", voiceName: null);

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
    public void SystemPrompt_AlwaysCarriesTheBrevityAndOldWorldToneWhispers()
    {
        // Moved in from the user-editable global prompt (2026.07.10): these must be real every time,
        // whatever the prompt files say — short living talk, and only a light savor of the old world.
        var system = new PromptBuilder().Build(Persona(), new NpcMemory(), "", "Vulgrim", "Hi")[0].Content;

        Assert.Contains(PromptBuilder.BrevityGuidance, system);
        Assert.Contains(PromptBuilder.OldWorldToneGuidance, system);
        Assert.Contains("four at the most", system);
        Assert.Contains("light savor of the old world", system);
    }

    [Fact]
    public void SystemPrompt_OffersTheHeartWhisper_OnlyWhenTheToolTrulyRides()
    {
        // The move_heart whisper must appear only when the tool rides along (CanMoveHeart), so an
        // NPC is never told of a hand they cannot lift — and told nothing of numbers either way.
        var withHeart = Persona();
        withHeart.CanMoveHeart = true;

        var granted = new PromptBuilder().Build(withHeart, new NpcMemory(), "", "Vulgrim", "Hi")[0].Content;
        var withheld = new PromptBuilder().Build(Persona(), new NpcMemory(), "", "Vulgrim", "Hi")[0].Content;

        Assert.Contains("My heart is my own", granted);
        Assert.Contains("I never speak the measure aloud", granted);
        Assert.DoesNotContain("My heart is my own", withheld);
    }

    [Fact]
    public void ReachOutDesireLine_TellsAStrangerHonestlyThereIsNoHistoryYet()
    {
        // With the pull floor, someone never spoken with may be moved to approach; the Angel must not
        // let them imagine a past that is not there — the approach is a first acquaintance.
        var stranger = PromptBuilder.ReachOutDesireLine("Vulgrim", stranger: true);
        var friend = PromptBuilder.ReachOutDesireLine("Vulgrim");

        Assert.Contains("never truly spoken", stranger);
        Assert.Contains("make their acquaintance", stranger);
        Assert.DoesNotContain("never truly spoken", friend);
        // Both leave the choice wholly theirs.
        Assert.Contains("yes or no", stranger);
        Assert.Contains("yes or no", friend);
    }

    [Fact]
    public void FirstWordLine_SpeaksFirstAndKnowsTheAnswerMayComeLater()
    {
        // The chat-window reaching-out: no accept/decline stands between them — she simply speaks,
        // and the Angel is honest that the player may be occupied and answer only later.
        var first = PromptBuilder.FirstWordLine("Vulgrim", stranger: true);
        var friend = PromptBuilder.FirstWordLine("Vulgrim");

        Assert.Contains("never truly spoken", first);
        Assert.Contains("make yourself known", first);
        Assert.DoesNotContain("never truly spoken", friend);
        // Both are told the answer may not be immediate, so silence is a lived moment, not a rebuff.
        Assert.Contains("only when their hands are free", first);
        Assert.Contains("only when their hands are free", friend);
    }

    [Fact]
    public void ArrivalLine_DistinguishesAStrangerFromAKnownFriend()
    {
        var first = PromptBuilder.ArrivalLine("Vulgrim", firstMeeting: true);
        var again = PromptBuilder.ArrivalLine("Vulgrim", firstMeeting: false);

        Assert.Contains("never spoken", first);
        Assert.Contains("open the way to talk", first);
        Assert.Contains("comes to you again", again);
        Assert.DoesNotContain("never spoken", again);
    }

    [Fact]
    public void HasRememberedHistory_TrueOnAnyLayerOfMemory()
    {
        Assert.False(PromptBuilder.HasRememberedHistory(new NpcMemory()));
        Assert.True(PromptBuilder.HasRememberedHistory(new NpcMemory { Summary = "s" }));

        var withFact = new NpcMemory();
        withFact.KnownFacts.Add("f");
        Assert.True(PromptBuilder.HasRememberedHistory(withFact));

        var withTurn = new NpcMemory();
        withTurn.AddTurn(new ConversationTurn { PlayerLine = "p", NpcLine = "n" });
        Assert.True(PromptBuilder.HasRememberedHistory(withTurn));
    }

    [Fact]
    public void SystemPrompt_PlacesDeepMemoryBeforeTheScene_SoTheMomentLandsLast()
    {
        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor." };
        memory.KnownFacts.Add("Vulgrim rules Sargot");

        var system = new PromptBuilder()
            .Build(Persona(), memory, "And now Vulgrim comes to me.", "Vulgrim", "Hello")[0].Content;

        // The sheet wakes toward the moment: memory → truths → the present scene → the closing whisper,
        // so "they come to me now" is the last thing held before the conversation itself.
        Assert.True(system.IndexOf("What Vulgrim is to me") < system.IndexOf("Vulgrim rules Sargot"));
        Assert.True(system.IndexOf("Vulgrim rules Sargot") < system.IndexOf("And now Vulgrim comes to me."));
        Assert.True(system.IndexOf("And now Vulgrim comes to me.") < system.IndexOf("How should I speak:"));
    }

    [Fact]
    public void SystemPrompt_SplitsTheSceneOnTheMeetingSeparator_MemoryBetweenSettingAndArrival()
    {
        // The game layer joins setting and THE MOMENT with the separator; the sheet slots deep memory
        // between them, so what I remember of the person sits right beside their arrival — and the
        // separator itself is plumbing that must never reach the LLM.
        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor.", SummaryAsOf = "1087.01.18" };
        memory.KnownFacts.Add("Vulgrim rules Sargot");

        var scene = "It is evening, and I am in Sargot."
            + "\n\n" + PromptBuilder.MeetingSeparator + "\n"
            + "And now Vulgrim, my husband, comes to me.";
        var system = new PromptBuilder().Build(Persona(), memory, scene, "Vulgrim", "Hello")[0].Content;

        Assert.DoesNotContain(PromptBuilder.MeetingSeparator, system);
        Assert.True(system.IndexOf("It is evening") < system.IndexOf("What Vulgrim is to me"));
        Assert.True(system.IndexOf("What Vulgrim is to me") < system.IndexOf("Vulgrim rules Sargot"));
        Assert.True(system.IndexOf("Vulgrim rules Sargot") < system.IndexOf("And now Vulgrim, my husband, comes to me."));
        Assert.True(system.IndexOf("my husband") < system.IndexOf("How should I speak:"));
        // The memory header carries when the thoughts were last gathered.
        Assert.Contains("as I last gathered my thoughts on 1087.01.18", system);
    }
}
