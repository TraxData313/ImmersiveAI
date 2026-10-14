using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The talk screen's editor for "Who they are" and the deep memory. The editor is one line, so the
/// question is always whether what the player meant survives the trip through it — and whether a
/// player who never edits anything sees exactly the sheet they saw before.
/// </summary>
public class InGameEditTests
{
    private static NpcPersona Persona() => new()
    {
        Name = "Gafnir",
        RoleDescription = "A Sturgian lord of clan Vidgrip.",
        PersonalityDescription = "Calculating, cautious, values loyalty.",
        Crafts = "I am able with a bow.",
        SpeechStyle = "Terse northern speech, dry humor, never flowery.",
    };

    private static string Sheet(NpcPersona persona) =>
        new PromptBuilder().Build(persona, new NpcMemory(), "", "Vulgrim", "Hello")[0].Content;

    [Fact]
    public void A_memorys_paragraphs_survive_the_one_line_editor()
    {
        var memory = "We met at Omor.\nHe was kind.\n\n\nThen he left.\n\n  \nI waited.";
        var line = EditLine.ToLine(memory);

        Assert.DoesNotContain("\n", line);
        Assert.Equal("We met at Omor. He was kind. ¶ Then he left. ¶ I waited.", line);
        Assert.Equal("We met at Omor. He was kind.\n\nThen he left.\n\nI waited.", EditLine.FromLine(line));
    }

    [Fact]
    public void Stray_marks_and_blank_pieces_make_no_empty_paragraphs()
    {
        Assert.Equal("One.\n\nTwo.", EditLine.FromLine(" ¶ One. ¶¶  ¶ Two. ¶ "));
        Assert.Equal(string.Empty, EditLine.FromLine(" ¶ "));
        Assert.Equal(string.Empty, EditLine.ToLine(null));
    }

    [Fact]
    public void The_built_block_is_the_sheet_the_game_always_sent()
    {
        var persona = Persona();
        var built = PromptBuilder.BuiltWhoTheyAre(persona);

        Assert.StartsWith("I am Gafnir, a living soul in the world of Calradia in feudal times.", built);
        Assert.Contains("My traits are calculating, cautious, values loyalty.", built);
        Assert.Contains("When I speak, it comes out like this: Terse northern speech", built);
        Assert.Contains(built.Replace("\r\n", "\n"), Sheet(persona).Replace("\r\n", "\n"));
    }

    [Fact]
    public void The_players_words_stand_in_place_of_the_whole_built_block()
    {
        var persona = Persona();
        persona.WhoTheyAre = "I am Gafnir, a reckless poet who has never held a sword.";
        var sheet = Sheet(persona);

        Assert.Contains("I am Gafnir, a reckless poet who has never held a sword.", sheet);
        Assert.DoesNotContain("A Sturgian lord of clan Vidgrip.", sheet);
        Assert.DoesNotContain("My traits are", sheet);
        Assert.DoesNotContain("When I speak, it comes out like this", sheet);
        Assert.DoesNotContain("I am able with a bow.", sheet);
    }
}
