using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// THE SECTION MARKS (2026.08.27) — planted so the talk screen can draw named, coloured headers
/// over the sheet, and STRIPPED before anything is sent. A mark that reached a model would be the
/// fourth wall in its plainest form, so the stripping is what these tests actually guard.
/// </summary>
public class SectionMarkTests
{
    private static NpcPersona Persona() => new()
    {
        Name = "Rhia the Healer",
        RoleDescription = "I am a healer of Battania.",
        PersonalityDescription = "Blunt, watchful",
        FamilyKnowledge = "My mother taught me herbs.",
        SelfConcept = "I am steadier than I was.",
        WorldInstructions = "Speak shortly.",
        CustomInstructions = "I keep a cedar box beneath my bedroll.",
    };

    [Fact]
    public void TheSheetTheModelSees_CarriesNoMarks()
    {
        var memory = new NpcMemory { Summary = "He hired me at Dunglanys." };
        var messages = new PromptBuilder().Build(
            Persona(), memory, "This moment finds me upon the road.", "Ahil", "Well met.");

        foreach (var m in messages)
        {
            Assert.DoesNotContain(PromptBuilder.SectionOpen, m.Content);
            Assert.DoesNotContain("[[section:", m.Content);
        }
    }

    [Fact]
    public void TheMarkedSheet_CarriesThemForTheScreenAlone()
    {
        var memory = new NpcMemory { Summary = "He hired me at Dunglanys." };
        var sheet = PromptBuilder.BuildMarkedSheet(
            Persona(), memory, "This moment finds me upon the road.", "Ahil");

        Assert.Contains(PromptBuilder.Section(PromptBuilder.Sections.WhoTheyAre), sheet);
        Assert.Contains(PromptBuilder.Section(PromptBuilder.Sections.DeepMemory), sheet);
        Assert.Contains(PromptBuilder.Section(PromptBuilder.Sections.YourOwnWords), sheet);

        // And the same text, stripped, is exactly what the model would have been given.
        var stripped = PromptBuilder.StripSections(sheet);
        Assert.DoesNotContain("[[section:", stripped);
        Assert.Contains("He hired me at Dunglanys.", stripped);
    }

    [Fact]
    public void StripSections_EatsOnlyTheMarkLines()
    {
        var text = "before\n" + PromptBuilder.Section("Anything") + "\nafter";
        Assert.Equal("before\nafter", PromptBuilder.StripSections(text).Replace("\r\n", "\n"));

        // A line that merely mentions brackets is left alone.
        const string innocent = "I said [[the-moment]] was near.";
        Assert.Equal(innocent, PromptBuilder.StripSections(innocent));
        Assert.Equal(string.Empty, PromptBuilder.StripSections(null));
    }
}
