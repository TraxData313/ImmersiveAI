using ImmersiveAI.Core.Handmaids;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Core.Text;

namespace ImmersiveAI.Core.Tests;

/// <summary>The handmaid's own sheet (2026.11.11): her section, what is dropped for her, her seed.</summary>
[Collection(nameof(PromptTextsEndToEnd))]
public class HandmaidSheetTests
{
    private static readonly string Mark = PromptBuilder.Section(PromptBuilder.Sections.HerQuietSpirit);

    private static NpcPersona Persona(string spirit = "") => new()
    {
        Name = "Neya",
        RoleDescription = "A Sturgian noble of clan Toni's — a woman of some 20 years.",
        SpeechStyle = "Old and weary; speaks slowly, references the past, gives unasked-for advice.",
        WorldInstructions = "Speak shortly.",
        CustomInstructions = "I keep a cedar box beneath my bedroll.",
        EraNorm = "So it is held in these days.",
        EncourageActingOut = true,
        VoiceTakesMood = true,
        HandmaidSpirit = spirit,
    };

    private static string Sheet(NpcPersona p) =>
        PromptBuilder.BuildMarkedSheet(p, new NpcMemory { Summary = "He found me." }, "The road.", "Toni");

    [Fact]
    public void TheSection_RidesOnlyForAHandmaid()
    {
        Assert.DoesNotContain(Mark, Sheet(Persona()));
        var spirit = HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: false);
        var sheet = Sheet(Persona(spirit));
        Assert.Contains(Mark, sheet);
        Assert.Contains(spirit, sheet);
    }

    [Fact]
    public void TheSection_StandsAfterTheSpeechRules_AndBeforeThePlayersOwnWords()
    {
        var sheet = Sheet(Persona(HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: false)));
        int speak = sheet.IndexOf(PromptBuilder.Section(PromptBuilder.Sections.HowTheySpeak), StringComparison.Ordinal);
        int era = sheet.IndexOf("So it is held in these days.", StringComparison.Ordinal);
        int hers = sheet.IndexOf(Mark, StringComparison.Ordinal);
        int own = sheet.IndexOf(PromptBuilder.Section(PromptBuilder.Sections.YourOwnWords), StringComparison.Ordinal);
        Assert.True(speak >= 0 && era > speak && hers > era && own > hers, sheet);
    }

    [Fact]
    public void NoBuiltMessage_CarriesASectionMark()
    {
        var p = Persona(HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: true));
        var memory = new NpcMemory { Summary = "He found me." };
        var builder = new PromptBuilder();
        foreach (var list in new[]
                 {
                     builder.Build(p, memory, "The road.", "Toni", "Well met."),
                     builder.BuildInnerPrompt(p, memory, "The road.", "Toni", "He comes to me."),
                 })
        foreach (var m in list)
            Assert.DoesNotContain(PromptBuilder.SectionOpen, m.Content);
    }

    [Fact]
    public void Wed_And_Unwed_SayTheirOwnFirstAndLastLines()
    {
        var unwed = HandmaidText.SpiritFor(HandmaidRoster.Yasmin, "Toni", wed: false);
        var wed = HandmaidText.SpiritFor(HandmaidRoster.Yasmin, "Toni", wed: true);

        Assert.StartsWith("I am Yasmin, handmaid of Toni, who took me out of the bandits' den", unwed);
        Assert.EndsWith("I love him and I hope to be his wife. I do not hide it: I show it in small things, and when he opens the way I walk it gladly.", unwed);
        Assert.StartsWith("I am Yasmin, wife of Toni, who took me out of the bandits' den when I had no one and made me his own. I was his handmaid first, and I am still glad to serve him.", wed);
        Assert.EndsWith("I love him, and I am his wife. I do not hide it: I show it in small things, every day.", wed);
        Assert.DoesNotContain("hope to be his wife", wed);

        // The middle is the same in both.
        Assert.Contains("The old holy man who kept the well by our street taught me that a meek and quiet spirit", unwed);
        Assert.Contains("The old holy man who kept the well by our street taught me that a meek and quiet spirit", wed);
        Assert.Contains("my lord, my master, my husband, or Zawji, now one and now another;", wed);
    }

    [Theory]
    [InlineData("niya")]
    [InlineData("yasmin")]
    [InlineData("alys")]
    [InlineData("nessa")]
    [InlineData("ayla")]
    public void EverySlotIsFilled_ForEveryGirl(string id)
    {
        var girl = HandmaidRoster.ById(id)!;
        Assert.False(string.IsNullOrWhiteSpace(girl.TaughtBy));
        foreach (var wed in new[] { false, true })
        foreach (var player in new[] { "Toni", "", null })
        {
            var s = HandmaidText.SpiritFor(girl, player, wed);
            Assert.DoesNotContain("{", s);
            Assert.DoesNotContain("}", s);
            Assert.Contains("I am " + girl.Name + ",", s);
            Assert.Contains(girl.TaughtBy + " taught me", s);
            Assert.Contains("or " + girl.NativeTitle + ",", s);
        }
    }

    [Fact]
    public void ATeacherPhraseFitsTheHeadOfHerSentence()
    {
        Assert.Equal("My mother", HandmaidRoster.Neya.TaughtBy);
        Assert.Equal("The old holy man who kept the well by our street", HandmaidRoster.Yasmin.TaughtBy);
        Assert.Equal("My mother", HandmaidRoster.Alys.TaughtBy);
        Assert.Equal("The old hermit who taught me my herbs", HandmaidRoster.Nessa.TaughtBy);
        Assert.Equal("My grandmother", HandmaidRoster.Ayla.TaughtBy);
    }

    [Fact]
    public void HerSheet_DropsTheAssignedSpeechStyle_AndTheTaleClause()
    {
        var hers = Sheet(Persona(HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: false)));
        var anyone = Sheet(Persona());

        Assert.Contains("When I speak, it comes out like this:", anyone);
        Assert.DoesNotContain("When I speak, it comes out like this:", hers);
        Assert.DoesNotContain("unasked-for advice", hers);

        Assert.Contains("Only a tale asked of me runs longer.", anyone);
        Assert.DoesNotContain("runs longer", hers);
        Assert.Contains("a sentence or three, then I let them answer.", hers);

        // What stays for her: the drift rule, plain speech, the acts and the voice line.
        Assert.Contains(PromptBuilder.NoLengthDriftGuidance, hers);
        Assert.Contains(PromptBuilder.PlainSpeechGuidance, hers);
        Assert.Contains(PromptBuilder.ActingOutGuidance, hers);
        Assert.Contains("[voice:", hers);
    }

    [Theory]
    [InlineData(PromptBuilder.ReplyLength.Brief)]
    [InlineData(PromptBuilder.ReplyLength.Conversational)]
    [InlineData(PromptBuilder.ReplyLength.Full)]
    public void HerBrevityLine_NeverInvitesLength(PromptBuilder.ReplyLength length)
    {
        var line = PromptBuilder.HandmaidBrevityFor(length);
        Assert.DoesNotContain("longer", line);
        Assert.DoesNotContain("as fully as", line);
        Assert.StartsWith("- I speak", line);
        Assert.EndsWith(".", line);
    }

    [Fact]
    public void HerSeed_IsHerOwnPast_ThenHerTeacher()
    {
        var seed = HandmaidText.MemorySeedFor(HandmaidRoster.Nessa);
        Assert.Equal(HandmaidRoster.Nessa.Story + " " + HandmaidText.StoryCloseRemembered + "\n"
            + "Of the one who taught me: " + HandmaidRoster.Nessa.Teacher, seed);
        Assert.Contains("dragged me to their den", seed);
        Assert.EndsWith(HandmaidText.StoryClose, HandmaidText.StoryLine(HandmaidRoster.Nessa));
        Assert.DoesNotContain("{", seed);
    }
}

/// <summary>The handmaid's length guard (2026.11.11): back to the last whole sentence that fits.</summary>
public class ReplyTrimTests
{
    [Fact]
    public void WithinBudget_ComesBackUntouched()
    {
        const string reply = "I am well, my lord. *She smiles.*\n[voice: soft]";
        Assert.Same(reply, ReplyTrim.ToWordBudget(reply, 80));
        Assert.Equal(reply, ReplyTrim.ToWordBudget(reply, 0));   // 0 = off
        Assert.Equal(reply, ReplyTrim.ToWordBudget(reply, -5));
        Assert.Equal(string.Empty, ReplyTrim.ToWordBudget(null, 10));
    }

    [Fact]
    public void OverBudget_CutsBackToTheLastWholeSentence()
    {
        const string reply = "One two three. Four five six. Seven eight nine ten.";
        Assert.Equal("One two three. Four five six.", ReplyTrim.ToWordBudget(reply, 7));
        Assert.Equal("One two three. Four five six.", ReplyTrim.ToWordBudget(reply, 6));
        Assert.Equal("One two three.", ReplyTrim.ToWordBudget(reply, 5));
    }

    [Fact]
    public void AFirstSentenceTooLong_IsKeptWhole()
    {
        const string reply = "One two three four five six seven. Eight nine.";
        Assert.Equal("One two three four five six seven.", ReplyTrim.ToWordBudget(reply, 3));
    }

    [Fact]
    public void AGesture_IsNeverBrokenByItsOwnStops()
    {
        const string reply = "*She lowers her eyes. Then she kneels.* Yes, my lord. I will go. And I will carry the herbs.";
        Assert.Equal("*She lowers her eyes. Then she kneels.* Yes, my lord. I will go.", ReplyTrim.ToWordBudget(reply, 14));
        // The cut never lands inside the act.
        Assert.Equal("*She lowers her eyes. Then she kneels.* Yes, my lord.", ReplyTrim.ToWordBudget(reply, 11));
    }

    [Fact]
    public void AGestureTooBigBeforeAnyWords_IsLetGo_AndTheFirstSentenceStays()
    {
        const string reply = "*She sets down the basket of herbs and comes to kneel beside him.* Yes. More words here now.";
        Assert.Equal("Yes.", ReplyTrim.ToWordBudget(reply, 3));
    }

    [Fact]
    public void TheVoiceKey_IsKeptAsTheLastLine_AndNotCounted()
    {
        const string reply = "One two three. Four five six.\n[voice: low and gentle, a little shy]";
        Assert.Equal(reply, ReplyTrim.ToWordBudget(reply, 6));
        Assert.Equal("One two three.\n[voice: low and gentle, a little shy]", ReplyTrim.ToWordBudget(reply, 4));
    }

    [Fact]
    public void CyrillicAndArabicSentenceEnds_AreRead()
    {
        const string bg = "Добре съм, господарю. Благодаря ти за хляба! Ще дойда с теб?";
        Assert.Equal("Добре съм, господарю. Благодаря ти за хляба!", ReplyTrim.ToWordBudget(bg, 8));
        const string ar = "نعم يا زوجي. هل تريد الماء؟ سأحضره الآن.";
        Assert.Equal("نعم يا زوجي. هل تريد الماء؟", ReplyTrim.ToWordBudget(ar, 6));
    }

    [Fact]
    public void ANewLine_EndsAThought_AndQuotesStayWithTheirSentence()
    {
        const string reply = "My mother said: \"a soft answer turns away wrath.\" So I am soft.\nAnd I say little more today";
        Assert.Equal("My mother said: \"a soft answer turns away wrath.\" So I am soft.", ReplyTrim.ToWordBudget(reply, 13));
    }

    [Fact]
    public void Ideographs_CountAsWords()
    {
        Assert.Equal(3, ReplyTrim.CountWords("我很好。"));
        Assert.Equal("我很好。", ReplyTrim.ToWordBudget("我很好。你呢？我们走吧。", 4));
    }

    [Fact]
    public void Words_AreCountedAsHeard()
    {
        Assert.Equal(0, ReplyTrim.CountWords("  — ... * "));
        Assert.Equal(4, ReplyTrim.CountWords("Yes, my lord — gladly."));
    }

    [Theory]
    [InlineData(-1, "den when I had no one")]
    [InlineData(0.2, "den this very day, when I had no one")]
    [InlineData(1.5, "den only yesterday, when I had no one")]
    [InlineData(9, "den 9 days ago, when I had no one")]
    [InlineData(40, "den some 5 weeks ago, when I had no one")]
    [InlineData(100, "den a year ago, when I had no one")]
    [InlineData(260, "den some 3 years ago, when I had no one")]
    public void SheKnowsHowLongAgoSheWasTaken(double days, string expected)
    {
        var s = HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: false, daysSinceTaken: days);
        Assert.Contains(expected, s);
        Assert.DoesNotContain("{", s);
    }

    [Fact]
    public void SheCarriesTheNameSheHasNow()
    {
        var s = HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: true, ownName: "Sofiya");
        Assert.StartsWith("I am Sofiya, wife of Toni", s);
        Assert.DoesNotContain("Neya", s);
    }

    [Fact]
    public void WhileItIsFreshSheIsToldSoInASentenceOfItsOwn()
    {
        var today = HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: false, daysSinceTaken: 0.1);
        Assert.Contains("It happened today: only hours ago I was still their captive", today);
        Assert.Contains("to his face I say you", today);
        var later = HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: false, daysSinceTaken: 9);
        Assert.DoesNotContain("It happened", later);
        Assert.DoesNotContain("{", later);
        Assert.DoesNotContain("{", HandmaidText.SpiritFor(HandmaidRoster.Neya, "Toni", wed: true));
    }
}
