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
        CustomInstructions = "I distrust Imperial nobility."
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
    public void Build_NeverSpeaksOfTheRetiredAimsAndTruths()
    {
        // The aims list and the distilled truths were retired 2026.08.08 — neither the blocks nor
        // the two whispers that offered their tools may ever reappear on a sheet.
        var memory = new NpcMemory { Summary = "We have ridden together a long while." };
        memory.KnownFacts.Add("a truth she held under the old shape");

        var system = new PromptBuilder().Build(Persona(), memory, "In the tavern.", "Vulgrim", "Hello")[0].Content;

        Assert.Contains("We have ridden together a long while.", system);
        Assert.DoesNotContain("My goals are:", system);
        Assert.DoesNotContain("Truths I decided to hold:", system);
        Assert.DoesNotContain("a truth she held under the old shape", system);
        Assert.DoesNotContain("My aims are mine", system);
        Assert.DoesNotContain("among the truths I hold", system);
    }

    [Fact]
    public void Build_OffersTheActingOutInvitation_OnlyWhenInvited()
    {
        var invited = Persona();
        invited.EncourageActingOut = true;
        var on = new PromptBuilder().Build(invited, new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.Contains("between single asterisks", on);
        // It is the plain-speech rule's one exception, so it must follow that rule, never precede it.
        Assert.True(on.IndexOf("no marks of the pen") < on.IndexOf("between single asterisks"));

        var off = new PromptBuilder().Build(Persona(), new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.DoesNotContain("between single asterisks", off);
    }

    [Fact]
    public void Build_FoldsInTheCrafts_ButNotTheFieldWhisper()
    {
        var persona = Persona();
        persona.Crafts = "What my hands and wits are honestly good at: masterly in Medicine.";
        persona.CanSurveyField = true;
        var on = new PromptBuilder().Build(persona, new NpcMemory(), "scene", "Vulgrim", "Hello")[0].Content;
        Assert.Contains("masterly in Medicine", on);

        // The field-craft guidance itself moved into the tool definitions on 2026.08.14 — the sheet
        // carries who they are, the tool carries when to reach for it.
        Assert.DoesNotContain("cast my eyes over the country", on);
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
    public void ComposeLetterLine_RidesAsTheNpcsOwnMind_NoNarratorVoice_AndCarriesTheRoadItself()
    {
        // The Angel narrator is retired (2026.08.07): every letter beat is the NPC's own first-person
        // mind, framed through BuildInnerPrompt. The asking step in front of it is retired too
        // (2026.08.16), so the premise it used to set — the long road, the waiting courier — has to
        // ride here, and AFTER the opening marker fragment or the beat stops being recognized.
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn { PlayerLine = "Hail, Gafnir", NpcLine = "Hail, stranger." });

        var line = PromptBuilder.ComposeLetterLine("Vulgrim");
        var messages = new PromptBuilder().BuildInnerPrompt(Persona(), memory, "In the tavern.", "Vulgrim", line, "Seraph");

        // System, the one remembered player turn (user+assistant), then the writing itself as the last user turn.
        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.User, messages[3].Role);
        Assert.StartsWith("(Within my own mind:", messages[3].Content);
        Assert.DoesNotContain("Seraph", messages[3].Content);   // no voice speaks to her
        Assert.Contains("a courier stands ready", messages[3].Content);
        Assert.DoesNotContain("Do I wish", messages[3].Content); // nothing is being asked of her
        Assert.True(PromptBuilder.IsComposeLetterBeat(line));
    }

    [Fact]
    public void BuildInnerPrompt_FramesTheLineAsTheNpcsOwnMind_NoVoiceSpeaking()
    {
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn { PlayerLine = "Hail, Gafnir", NpcLine = "Hail, stranger." });

        var line = PromptBuilder.FirstWordLine("Vulgrim");
        var messages = new PromptBuilder().BuildInnerPrompt(Persona(), memory, "In the tavern.", "Vulgrim", line, "Seraph");

        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.User, messages[3].Role);
        Assert.StartsWith("(Within my own mind:", messages[3].Content); // their own moment, no Angel
        Assert.DoesNotContain("Seraph", messages[3].Content);
        Assert.Contains("in my own voice", messages[3].Content);        // the words are theirs to speak
    }

    [Fact]
    public void ApproachLine_ReflectsWhetherThePlayerWelcomedThem()
    {
        var welcomed = PromptBuilder.ApproachLine("Vulgrim", welcomed: true);
        var busy = PromptBuilder.ApproachLine("Vulgrim", welcomed: false);

        Assert.Contains("give me their attention", welcomed); // the player receives them
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
    public void Build_ReplaysARememberedInnerTurnAsTheirOwnMind_NotAsTheAngelOrThePlayer()
    {
        var memory = new NpcMemory();
        memory.AddTurn(new ConversationTurn
        {
            Speaker = ConversationTurn.InnerSpeaker,
            PlayerLine = PromptBuilder.ReachOutPonderNote("Vulgrim"),
            NpcLine = "GO: the granary tally is short.",
            Place = "Ostican",
            CalradiaTime = "1087.01.01 10.20",
        });

        var messages = new PromptBuilder().Build(Persona(), memory, "In Ostican.", "Vulgrim", "Hello", voiceName: "Seraph");

        // system, [inner note framed as their own mind], [their resolution as assistant], [player input].
        Assert.Equal(4, messages.Count);
        Assert.StartsWith("[Ostican, 1087.01.01 10.20] (Within my own mind:", messages[1].Content);
        Assert.DoesNotContain("Seraph", messages[1].Content);
        Assert.Equal("GO: the granary tally is short.", messages[2].Content);
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
        Assert.Contains("came and spoke with me awhile", messages[1].Content);
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

        var system = new PromptBuilder()
            .Build(Persona(), memory, "On the road near Balgard.", "Vulgrim", "Hello")[0].Content;

        Assert.Contains("Gafnir", system);
        Assert.Contains("Terse northern speech", system);
        Assert.Contains("On the road near Balgard.", system);
        Assert.Contains("You fought beside Vulgrim at Omor.", system);
        Assert.Contains("I distrust Imperial nobility.", system);
        Assert.Contains("How I speak:", system);
    }

    [Fact]
    public void SystemPrompt_PlacesWorldAndCustomInstructionsLast_AndClaimsPrecedence()
    {
        var persona = Persona();
        persona.WorldInstructions = "Magic is rare and feared in this land.";
        persona.CustomInstructions = "I distrust Imperial nobility.";

        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor." };
        var system = new PromptBuilder()
            .Build(persona, memory, "On the road near Balgard.", "Vulgrim", "Hello")[0].Content;

        // Both authored blocks are shown under first-person headings - the NPC's own knowledge,
        // never a narrator handing them anything.
        Assert.Contains("Of this world, this I know:", system);
        Assert.Contains("Magic is rare and feared in this land.", system);
        Assert.Contains("Of myself, this I hold true:", system);
        Assert.Contains("I distrust Imperial nobility.", system);
        Assert.True(system.IndexOf("Of this world, this I know:") < system.IndexOf("Of myself, this I hold true:"));

        // They CLOSE the sheet (2026.08.14). Mid-sheet they were quietly losing to thousands of
        // tokens of lived memory below them; last, and under a line that says which way a
        // contradiction falls, is what makes an edit to them actually bite.
        Assert.Contains(PromptBuilder.HeldTruestFrame, system);
        Assert.True(system.IndexOf(PromptBuilder.HeldTruestFrame) < system.IndexOf("Of this world, this I know:"));
        Assert.True(system.IndexOf("What Vulgrim is to me") < system.IndexOf(PromptBuilder.HeldTruestFrame));
        Assert.True(system.IndexOf("On the road near Balgard.") < system.IndexOf(PromptBuilder.HeldTruestFrame));
        Assert.True(system.IndexOf("How I speak:") < system.IndexOf(PromptBuilder.HeldTruestFrame));
    }

    [Fact]
    public void SystemPrompt_WithNoAuthoredWords_HasNoPrecedenceFrame()
    {
        var persona = Persona();
        persona.WorldInstructions = string.Empty;
        persona.CustomInstructions = string.Empty;
        var system = new PromptBuilder().Build(persona, new NpcMemory(), "scene", "Vulgrim", "Hi")[0].Content;

        Assert.DoesNotContain(PromptBuilder.HeldTruestFrame, system);
        Assert.DoesNotContain("Of this world, this I know:", system);
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
            Persona(), "Vulgrim", "You honor me.", "The honor is mine.");

        // A tight two-message call: her own mind, then the weighing.
        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);

        // The system message is her own first person and constrains the output to a single number.
        Assert.Contains("I am Gafnir", messages[0].Content);
        Assert.Contains("single whole number", messages[0].Content);

        // The weighing carries the exchange and asks only for the movement — within her own mind.
        Assert.StartsWith("(Within my own mind:", messages[1].Content);
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
            Persona(), "Vulgrim", "Hail.", "Well met.");

        Assert.DoesNotContain("my regard rests", messages[1].Content);
        Assert.DoesNotContain("rests at", messages[1].Content);
        Assert.DoesNotContain("stands at", messages[1].Content);
    }

    [Fact]
    public void BuildFeelingQuery_SpeaksNoNarratorVoice()
    {
        // The Angel narrator is retired (2026.08.07): the weighing is wholly her own mind.
        var messages = new PromptBuilder().BuildFeelingQuery(
            Persona(), "Vulgrim", "Hail.", "Well met.");

        Assert.DoesNotContain("Angel", messages[0].Content);
        Assert.DoesNotContain("Angel", messages[1].Content);
        Assert.DoesNotContain("whispers", messages[1].Content);
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
    public void SystemPrompt_CarriesNoPerToolProse_EvenWithEveryHandGranted()
    {
        // 2026.08.14: the eight per-tool paragraphs moved OUT of the sheet and into the tool
        // definitions, where a tool's contract belongs and where it is sent only on the calls that
        // actually carry the tool. This is the guard against them creeping back one at a time -
        // every hand is granted here, and the sheet must still say none of it.
        var everything = Persona();
        everything.CanRecallWorld = true;
        everything.CanSeekWisdom = true;
        everything.CanMoveHeart = true;
        everything.CanRecallChronicle = true;
        everything.CanSurveyField = true;
        everything.CanStrikeBargain = true;
        everything.CanTendTroth = true;
        everything.CanBlessTroth = true;

        var system = new PromptBuilder().Build(everything, new NpcMemory(), "scene", "Vulgrim", "Hi")[0].Content;

        Assert.DoesNotContain("My heart is my own", system);
        Assert.DoesNotContain("the bargain is mine to strike", system);
        Assert.DoesNotContain("My troth is my own to tend", system);
        Assert.DoesNotContain("My misgivings about a life together", system);
        Assert.DoesNotContain("blessing of that match", system);
        Assert.DoesNotContain("cast my eyes over the country", system);
        Assert.DoesNotContain("I call the whole of it back", system);
        Assert.DoesNotContain("all I have ever read and heard", system);

        // What DOES remain is three habits of speech and nothing more.
        Assert.Contains("How I speak:", system);
        Assert.Contains(PromptBuilder.BrevityGuidance, system);
    }

    [Fact]
    public void SystemPrompt_AlwaysCarriesTheBrevityAndOldWorldToneWhispers()
    {
        // Moved in from the user-editable global prompt (2026.07.10): these must be real every time,
        // whatever the prompt files say — short living talk, and only a light savor of the old world.
        var system = new PromptBuilder().Build(Persona(), new NpcMemory(), "", "Vulgrim", "Hi")[0].Content;

        Assert.Contains(PromptBuilder.BrevityGuidance, system);
        Assert.Contains(PromptBuilder.OldWorldToneGuidance, system);
        Assert.Contains("a sentence or three", system);
        Assert.Contains("light savor of the old world", system);
    }


    [Fact]
    public void ReachOutPonderNote_IsStillRecognizedAsAPonderBeat_ForMemoriesThatHoldOne()
    {
        // The ponder itself is retired (2026.08.16) but its recorded beats are forever — the windows
        // must go on folding reckoning and resolution into one line of narration.
        Assert.True(PromptBuilder.IsPonderBeat(PromptBuilder.ReachOutPonderNote("Vulgrim", stranger: true)));
        Assert.True(PromptBuilder.IsPonderBeat(PromptBuilder.ReachOutPonderNote("Vulgrim")));
        // Delivery notes are NOT ponders — their spoken words must stand as spoken.
        Assert.False(PromptBuilder.IsPonderBeat(PromptBuilder.FirstWordNote("Vulgrim")));
        Assert.False(PromptBuilder.IsPonderBeat(PromptBuilder.ApproachNote("Vulgrim", welcomed: true)));
    }

    [Fact]
    public void FirstWordLine_HandsThemTheMicrophone_WithNoQuestionInFrontOfIt()
    {
        // Since the ponder was retired the roll alone decides who speaks, so this line carries as a
        // PREMISE the bar the question used to set: something to tell, or to ask. What that something
        // is stays theirs — no list of worthy topics, which made every soul answer the same, and no
        // asking whether they have one, which spent a whole sheet to be told "no".
        var first = PromptBuilder.FirstWordLine("Vulgrim", stranger: true);
        var friend = PromptBuilder.FirstWordLine("Vulgrim");

        Assert.Contains("we have never spoken", first);
        Assert.DoesNotContain("we have never spoken", friend);
        foreach (var line in new[] { first, friend })
        {
            Assert.Contains("tell them", line);
            Assert.Contains("ask them", line);
            // Nothing left that reads as a decision to be made, or as a topic being policed.
            Assert.DoesNotContain("NO — or YES:", line);
            Assert.DoesNotContain("no cause", line);
            // Both are told the answer may not be immediate, so silence is a lived moment, not a rebuff.
            Assert.Contains("at once or only later", line);
        }
    }

    [Fact]
    public void ApproachLine_CarriesTheSamePremise_WhicheverWayThePlayerAnswered()
    {
        var welcomed = PromptBuilder.ApproachLine("Vulgrim", welcomed: true);
        var busy = PromptBuilder.ApproachLine("Vulgrim", welcomed: false);

        Assert.Contains("tell them, or to ask them", welcomed);
        Assert.Contains("tell them, or to ask them", busy);
    }

    [Fact]
    public void ArrivalLine_DistinguishesAStrangerFromAKnownFriend()
    {
        var first = PromptBuilder.ArrivalLine("Vulgrim", firstMeeting: true);
        var again = PromptBuilder.ArrivalLine("Vulgrim", firstMeeting: false);

        Assert.Contains("never spoken", first);
        Assert.Contains("open the way to talk", first);
        Assert.Contains("comes to me again", again);
        Assert.DoesNotContain("never spoken", again);
    }

    [Fact]
    public void LetterMarkers_RecognizeBothTheLegacyAngelPhrasing_AndTheFirstPersonOne()
    {
        // Recorded memories carry the phrasing they were born with forever: beats written by the
        // retired Angel narrator (pre-2026.08.07 saves) must stay recognized beside the live
        // first-person templates — the letter cards in the chat window depend on it.
        const string legacyCompose = "Then sit, and set your heart to paper. Give me only the letter itself — the words that will stand on the page before Vulgrim's eyes, in your own hand and your own voice. Do not tell me about the letter; write it.";
        const string legacyReply = "Then answer them. Give me only the letter you would send back to Vulgrim — the words that will stand on the page, in your own hand and your own voice. Do not tell me about the letter; write it.";
        const string legacyReceived = "A courier has found you, bearing a letter from Vulgrim, written in their own hand. " +
            "You break the seal and read:\n\nMeet me at Sargot.\n\nTell me, from your own heart: do you wish to write back to Vulgrim? " +
            "Answer with a single word — yes or no. You may also let it lie unanswered; the choice is wholly yours.";

        Assert.True(PromptBuilder.IsComposeLetterBeat(legacyCompose));
        Assert.True(PromptBuilder.IsComposeLetterBeat(legacyReply));
        Assert.True(PromptBuilder.TryExtractReceivedLetter(legacyReceived, out var legacyBody));
        Assert.Equal("Meet me at Sargot.", legacyBody);

        // And the live first-person templates are recognized the same way.
        Assert.True(PromptBuilder.IsComposeLetterBeat(PromptBuilder.ComposeLetterLine("Vulgrim")));
        Assert.True(PromptBuilder.IsComposeLetterBeat(PromptBuilder.ComposeReplyLine("Vulgrim")));
        Assert.True(PromptBuilder.TryExtractReceivedLetter(
            PromptBuilder.AnswerLetterDesireLine("Vulgrim", "Meet me at Sargot."), out var ownBody));
        Assert.Equal("Meet me at Sargot.", ownBody);
    }

    [Fact]
    public void LetterAndArrivalLines_SpeakOnlyInTheFirstPerson()
    {
        // No line an NPC newly receives may address them as "you" — the narrator is retired.
        foreach (var line in new[]
        {
            PromptBuilder.ComposeLetterLine("Vulgrim"),
            PromptBuilder.ComposeLetterLine("Vulgrim", inService: true),
            PromptBuilder.ComposeReplyLine("Vulgrim"),
            PromptBuilder.AnswerLetterDesireLine("Vulgrim", "Meet me at Sargot."),
            PromptBuilder.ArrivalLine("Vulgrim", firstMeeting: true),
            PromptBuilder.ArrivalLine("Vulgrim", firstMeeting: false),
            PromptBuilder.MeetingLine("Vulgrim", firstMeeting: true),
            PromptBuilder.MeetingLine("Vulgrim", firstMeeting: false),
        })
        {
            Assert.DoesNotContain(" you ", " " + line.Replace("\n", " ") + " ");
            Assert.DoesNotContain(" your ", " " + line.Replace("\n", " ") + " ");
        }
    }

    [Fact]
    public void HasRememberedHistory_TrueOnAnyLayerOfMemory()
    {
        Assert.False(PromptBuilder.HasRememberedHistory(new NpcMemory()));
        Assert.True(PromptBuilder.HasRememberedHistory(new NpcMemory { Summary = "s" }));

        // The retired truths field is no longer a layer of memory: a save carrying nothing but old
        // facts is a soul with no remembered history, and is greeted as a first meeting.
        var factsOnly = new NpcMemory();
        factsOnly.KnownFacts.Add("f");
        Assert.False(PromptBuilder.HasRememberedHistory(factsOnly));

        var withTurn = new NpcMemory();
        withTurn.AddTurn(new ConversationTurn { PlayerLine = "p", NpcLine = "n" });
        Assert.True(PromptBuilder.HasRememberedHistory(withTurn));
    }

    [Fact]
    public void HasRememberedHistory_ASeededBackstoryIsTheirStoryNotHistoryWithThePlayer()
    {
        // The deep memory opens seeded with the story of their own road (2026.08.08). That story is
        // THEIRS — a soul carrying only it has never met the player and is greeted as a stranger.
        var seededOnly = new NpcMemory { Summary = "So runs my story, as the world tells it: …", SeededFromStory = true };
        Assert.False(PromptBuilder.HasRememberedHistory(seededOnly));

        // Once anything is truly lived between them, the acquaintance is real — even after every
        // verbatim turn has been folded away, the lifetime count carries it.
        var seededThenLived = new NpcMemory { Summary = "Their road, and now the player in it.", SeededFromStory = true, TotalTurns = 3 };
        Assert.True(PromptBuilder.HasRememberedHistory(seededThenLived));
    }

    [Fact]
    public void SystemPrompt_PlacesDeepMemoryBeforeTheScene_SoTheMomentLandsLast()
    {
        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor." };

        var system = new PromptBuilder()
            .Build(Persona(), memory, "About Vulgrim:", "Vulgrim", "Hello")[0].Content;

        // The sheet wakes toward the moment: deep memory → the present scene → the closing whisper,
        // so "they come to me now" is the last thing held before the conversation itself.
        Assert.True(system.IndexOf("What Vulgrim is to me") < system.IndexOf("About Vulgrim:"));
        Assert.True(system.IndexOf("About Vulgrim:") < system.IndexOf("How I speak:"));
    }

    [Fact]
    public void SystemPrompt_SplitsTheSceneOnTheMeetingSeparator_MemoryBetweenSettingAndArrival()
    {
        // The game layer joins setting and THE MOMENT with the separator; the sheet slots deep memory
        // between them, so what I remember of the person sits right beside their arrival — and the
        // separator itself is plumbing that must never reach the LLM.
        var memory = new NpcMemory { Summary = "You fought beside Vulgrim at Omor.", SummaryAsOf = "1087.01.18" };

        var scene = "It is evening, and I am in Sargot."
            + "\n\n" + PromptBuilder.MeetingSeparator + "\n"
            + "About Vulgrim, my husband:";
        var system = new PromptBuilder().Build(Persona(), memory, scene, "Vulgrim", "Hello")[0].Content;

        Assert.DoesNotContain(PromptBuilder.MeetingSeparator, system);
        Assert.True(system.IndexOf("It is evening") < system.IndexOf("What Vulgrim is to me"));
        Assert.True(system.IndexOf("What Vulgrim is to me") < system.IndexOf("About Vulgrim, my husband:"));
        Assert.True(system.IndexOf("my husband") < system.IndexOf("How I speak:"));
        // The memory header carries when the thoughts were last gathered.
        Assert.Contains("as I last gathered my thoughts on 1087.01.18", system);
    }

    [Fact]
    public void SystemPrompt_ASeededOnlyMemoryIsHeadedAsTheirOwnRoad_NotAsThePlayer()
    {
        // Before anything is lived with this person, the deep memory holds only the seeded story of
        // the NPC's own road — heading it "What X is to me" would put a stranger inside it.
        var seeded = new NpcMemory { Summary = "So runs my story, as the world tells it: a lady of the Throsniring.", SeededFromStory = true };

        var system = new PromptBuilder()
            .Build(Persona(), seeded, "About Vulgrim:", "Vulgrim", "Hello")[0].Content;

        Assert.Contains("The road of my life so far, as I carry it in memory:", system);
        Assert.DoesNotContain("What Vulgrim is to me", system);

        // The first lived turn makes it memory of a person again, under the usual heading.
        seeded.AddTurn(new ConversationTurn { PlayerLine = "p", NpcLine = "n" });
        var after = new PromptBuilder()
            .Build(Persona(), seeded, "About Vulgrim:", "Vulgrim", "Hello")[0].Content;
        Assert.Contains("What Vulgrim is to me", after);
        Assert.DoesNotContain("The road of my life so far", after);
    }
}
