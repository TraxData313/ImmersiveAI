using System.Runtime.CompilerServices;
using System.Text;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The sheet of an ordinary soul, pinned byte for byte (2026.11.11, the handmaid's prompt step).
/// Her section and the lines dropped for her must never touch anyone else's sheet, so the golden
/// file was written by the builder as it stood BEFORE that step, and every non-handmaid sheet built
/// from the same inputs must still match it exactly. Regenerate only on purpose: set
/// IMMERSIVEAI_WRITE_GOLDEN=1 and run this test once.
/// </summary>
[Collection(nameof(PromptTextsEndToEnd))]
public class SheetGoldenTests
{
    private static string GoldenPath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "Golden", "ordinary_sheets.txt");

    private static NpcPersona Rich(PromptBuilder.ReplyLength length, bool onPaper) => new()
    {
        Name = "Rhia the Healer",
        AtmosphereLine = "I am Rhia the Healer, a living soul in the world of Calradia in feudal times.",
        RoleDescription = "A Battanian wanderer and sellsword — a woman of some 27 years.",
        PersonalityDescription = "Blunt, watchful",
        Crafts = "Masterly in Medicine, able in Scouting.",
        SpeechStyle = "Old and weary; speaks slowly, references the past, gives unasked-for advice.",
        FamilyKnowledge = "My mother taught me herbs.",
        SelfConcept = "I am steadier than I was.",
        WorldInstructions = "Speak shortly.",
        CustomInstructions = "I keep a cedar box beneath my bedroll.",
        RoleplayGuidance = "- I love a good jest.",
        EraNorm = "So it is held in these days.",
        CourtshipTerms = "My heart leans toward him.",
        EncourageActingOut = true,
        VoiceSounds = new List<string> { "laugh", "sigh" },
        VoiceTakesMood = true,
        ReplyLength = length,
        OnPaper = onPaper,
    };

    internal static string OrdinarySheets()
    {
        PromptTexts.Pack = PromptPack.Empty;
        var memory = new NpcMemory { Summary = "He hired me at Dunglanys.", SummaryAsOf = "Spring 3, 1084" };
        const string scene = "This moment finds me upon the road." + PromptBuilder.MeetingSeparator + "And now Ahil comes to me.";
        var sb = new StringBuilder();
        foreach (var length in new[] { PromptBuilder.ReplyLength.Brief, PromptBuilder.ReplyLength.Conversational, PromptBuilder.ReplyLength.Full })
        foreach (var paper in new[] { false, true })
        {
            var p = Rich(length, paper);
            sb.Append("==== ").Append(length).Append(paper ? " paper" : " spoken").Append('\n');
            sb.Append(PromptBuilder.BuildMarkedSheet(p, memory, scene, "Ahil")).Append('\n');
            foreach (var m in new PromptBuilder().Build(p, memory, scene, "Ahil", "Well met."))
                sb.Append("-- ").Append(m.Role).Append('\n').Append(m.Content).Append('\n');
        }
        var plain = new NpcPersona { Name = "Ulf" };
        sb.Append("==== bare\n").Append(PromptBuilder.BuildMarkedSheet(plain, new NpcMemory(), string.Empty, "Ahil")).Append('\n');
        return sb.ToString().Replace("\r\n", "\n");
    }

    [Fact]
    public void AnOrdinarySoulsSheet_IsByteIdenticalToBeforeTheHandmaid()
    {
        var now = OrdinarySheets();
        var path = GoldenPath();
        if (Environment.GetEnvironmentVariable("IMMERSIVEAI_WRITE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, now, new UTF8Encoding(false));
        }
        Assert.True(File.Exists(path), "golden file missing: " + path);
        var golden = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
        Assert.Equal(golden, now);
    }
}
