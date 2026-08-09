using System.Linq;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    /// <summary>
    /// "Let me think…" — the player's own next line, worked out on the NPC's own sheet. Two things
    /// are guarded here: the aside never asks for the wrong voice, and whatever the model wraps its
    /// answer in is stripped before it reaches the writing line.
    /// </summary>
    public class PlayerThoughtTests
    {
        // ------------------------------ the aside ------------------------------

        [Fact]
        public void MindFrame_is_the_players_own_voice_and_forbids_the_other_one()
        {
            var frame = PlayerThought.MindFrame("Vulgrim", "Sibylla");

            Assert.StartsWith("I am Vulgrim.", frame);
            Assert.Contains("I set down my own words only", frame);
            // The seating chart alone is not enough; say it too.
            Assert.Contains("I do not answer in their voice", frame);
            foreach (var forbidden in new[] { "AI", "prompt", "model", "player character", "Bannerlord" })
                Assert.DoesNotContain(forbidden, frame);

            Assert.Contains("what to write to Sibylla", PlayerThought.MindFrame("Vulgrim", "Sibylla", asLetter: true));
        }

        [Fact]
        public void SpokenLine_hands_the_turn_over_in_the_players_first_person()
        {
            var line = PlayerThought.SpokenLine("Vulgrim", null);

            // Anton's own frame: now it is my turn → what turns in my mind → I say:
            Assert.Contains("[Now it is my turn to speak.]", line);
            Assert.EndsWith("Vulgrim:", line);
            // The tongue and the spirit clauses, lightly — and the brevity rail.
            Assert.Contains("same spirit", line);
            Assert.Contains("same tongue", line);
            Assert.Contains("As short as talk truly is", line);
        }

        [Fact]
        public void SpokenLine_reads_the_box_as_half_formed_thought_however_it_was_filled()
        {
            // A chosen preset, a typed rant — both are the same thing here: what turns in the mind.
            var with = PlayerThought.SpokenLine("Vulgrim", "  something romantic, about her hair  ");
            Assert.Contains("What turns in my mind: “something romantic, about her hair”", with);
            Assert.Contains("half-formed thought, not the words themselves", with);

            // Empty is a real ask, not a missing one: the moment finds the words.
            var without = PlayerThought.SpokenLine("Vulgrim", "   ");
            Assert.Contains("Nothing is settled in my mind yet", without);
            Assert.Contains("must find what to say", without);
            Assert.DoesNotContain("turns in", without);
        }

        [Fact]
        public void SpokenLine_stays_short_enough_to_be_read()
        {
            // The whole ask was "no bedsheets of instruction" — the material above carries the rest.
            var line = PlayerThought.SpokenLine("Vulgrim", "something romantic");
            Assert.True(line.Length < 500, "the closing block has grown into a bedsheet: " + line.Length);
        }

        [Fact]
        public void LetterLine_asks_for_the_letter_itself_and_never_for_brevity()
        {
            var line = PlayerThought.LetterLine("Vulgrim", "ask after her health");

            Assert.Contains("[Now it is my turn to write.]", line);
            Assert.Contains("not a telling about them", line);
            Assert.Contains("ask after her health", line);
            Assert.EndsWith("Vulgrim writes:", line);
            // A letter runs as long as it runs — the spoken block's brevity rail must not leak here.
            Assert.DoesNotContain("As short as talk truly is", line);
            Assert.Contains("must find what to write", PlayerThought.LetterLine("Vulgrim", null));
        }

        [Fact]
        public void BuildPlayerThought_leaves_the_NPC_no_chair_to_answer_from()
        {
            var memory = new NpcMemory { NpcId = "npc_1", NpcName = "Sibylla" };
            memory.AddTurn(new ConversationTurn
            {
                PlayerLine = "How fare the horses?",
                NpcLine = "Well enough.",
                Place = "Sargot",
            });

            var messages = new PromptBuilder().BuildPlayerThought(
                "Who I am: Vulgrim.\nWho they are: Sibylla — my wife, the scout of my own company.",
                memory, "Vulgrim", "Sibylla", "something romantic",
                world: "The world is harsh and medieval.");

            // THE fix (first playtest, 2026.08.10): no assistant turn of hers for a model to carry
            // on from — two messages, and the only "I" anywhere is the player's.
            Assert.Equal(2, messages.Count);
            Assert.Equal(ChatRole.System, messages[0].Role);
            Assert.Equal(ChatRole.User, messages[1].Role);
            Assert.DoesNotContain(messages, m => m.Role == ChatRole.Assistant);
            Assert.StartsWith("I am Vulgrim.", messages[0].Content);

            // The facts ride in the third person, the world in mine — and her sheet nowhere at all.
            var body = messages[1].Content;
            Assert.Contains("Who they are: Sibylla — my wife", body);
            Assert.Contains("The world is harsh and medieval.", body);

            // …and the shared story as a named script, so whose turn it is cannot be in doubt.
            Assert.Contains("[Sargot] Vulgrim: How fare the horses?", body);
            Assert.Contains("Sibylla: Well enough.", body);
            Assert.EndsWith("Vulgrim:", body);
        }

        [Fact]
        public void BuildPlayerThought_quotes_her_inner_beats_instead_of_inhabiting_them()
        {
            var memory = new NpcMemory { NpcId = "npc_1", NpcName = "Sibylla" };
            memory.AddTurn(new ConversationTurn
            {
                PlayerLine = "I marked Vulgrim nearby and weighed whether to go to them. I resolved:",
                NpcLine = "YES: the horses",
                Speaker = ConversationTurn.InnerSpeaker,
            });

            var body = new PromptBuilder()
                .BuildPlayerThought("Who I am: Vulgrim.", memory, "Vulgrim", "Sibylla", null)[1].Content;

            Assert.Contains("(Sibylla, to themselves: I marked Vulgrim nearby", body);
            Assert.Contains("Sibylla: YES: the horses", body);
            Assert.DoesNotContain("Vulgrim: I marked", body);
        }

        [Fact]
        public void BuildPlayerThought_says_plainly_when_nothing_has_been_said_yet()
        {
            var body = new PromptBuilder().BuildPlayerThought(
                "Who I am: Vulgrim.", new NpcMemory { NpcId = "npc_1" }, "Vulgrim", "Sibylla", null)[1].Content;

            Assert.Contains("Sibylla and I have never yet spoken. Mine would be the first words.", body);
        }

        // ------------------------------ taming the answer ------------------------------

        [Theory]
        [InlineData("\"Well met, my lady.\"", "Well met, my lady.")]
        [InlineData("“Well met, my lady.”", "Well met, my lady.")]
        [InlineData("«Здравей, мила.»", "Здравей, мила.")]
        [InlineData("  Well met, my lady.  ", "Well met, my lady.")]
        public void Tame_unwraps_a_fenced_answer(string raw, string expected)
            => Assert.Equal(expected, PlayerThought.Tame(raw));

        [Fact]
        public void Tame_keeps_quotation_marks_that_are_not_the_whole_fence()
        {
            const string raw = "He said \"go north\" and I believed him.";
            Assert.Equal(raw, PlayerThought.Tame(raw));
        }

        [Fact]
        public void Tame_drops_a_lead_in_heading_but_not_a_real_spoken_colon()
        {
            Assert.Equal("Well met, my lady.",
                PlayerThought.Tame("Here is what you might say:\n\nWell met, my lady."));

            // A genuine line that happens to end its first breath on a colon is left alone.
            const string real = "Listen, brother:\nwe ride at dawn, and no later.";
            Assert.Equal(real, PlayerThought.Tame(real));
        }

        [Fact]
        public void Tame_drops_an_echoed_aside_and_the_speakers_own_name()
        {
            Assert.Equal("Well met.",
                PlayerThought.Tame("[An aside, outside the talk itself.]\nWell met.", "Vulgrim"));

            Assert.Equal("Well met.", PlayerThought.Tame("Vulgrim: Well met.", "Vulgrim"));
            // …but a line that merely begins with the name is not a label.
            Assert.Equal("Vulgrim would never say that.",
                PlayerThought.Tame("Vulgrim would never say that.", "Vulgrim"));
        }

        [Fact]
        public void Tame_drops_the_frames_own_arrow_however_it_comes_back()
        {
            Assert.Equal("Well met.", PlayerThought.Tame("→ Well met.", "Vulgrim"));
            Assert.Equal("Well met.", PlayerThought.Tame("Vulgrim says → Well met.", "Vulgrim"));
            Assert.Equal("Well met.", PlayerThought.Tame("Vulgrim writes -> Well met.", "Vulgrim"));
            // An em dash opens a spoken line in half the world's typography — never cut it.
            Assert.Equal("— Well met.", PlayerThought.Tame("— Well met.", "Vulgrim"));
        }

        [Fact]
        public void Tame_answers_empty_for_nothing_at_all()
        {
            Assert.Equal(string.Empty, PlayerThought.Tame(null));
            Assert.Equal(string.Empty, PlayerThought.Tame("   \n  "));
        }

        // ------------------------------ the presets file ------------------------------

        [Fact]
        public void Presets_parse_name_and_wish_and_skip_comments()
        {
            var list = ConversationPresets.Parse(
                "# a comment\n" +
                "// another\n" +
                "\n" +
                "romantic = I want to say something romantic.\n" +
                "ender=I want to close the talk kindly.\n");

            Assert.Equal(2, list.Count);
            Assert.Equal("romantic", list[0].Name);
            Assert.Equal("I want to say something romantic.", list[0].Text);
            Assert.Equal("ender", list[1].Name);
            Assert.Equal("I want to close the talk kindly.", list[1].Text);
        }

        [Fact]
        public void Presets_name_themselves_when_a_line_carries_only_a_wish()
        {
            var list = ConversationPresets.Parse("I want to ask after her mother");
            var only = Assert.Single(list);
            Assert.Equal("I want to", only.Name);
            Assert.Equal("I want to ask after her mother", only.Text);
        }

        [Fact]
        public void Presets_round_trip_through_the_file_shape()
        {
            var original = ConversationPresets.Defaults;
            var back = ConversationPresets.Parse(ConversationPresets.Format(original));

            Assert.Equal(original.Count, back.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original[i].Name, back[i].Name);
                Assert.Equal(original[i].Text, back[i].Text);
            }
            Assert.Equal(new[] { "starter", "romantic", "ender" }, original.Select(i => i.Name));
        }

        [Fact]
        public void Upsert_rewrites_by_name_and_Remove_strikes_out()
        {
            var list = ConversationPresets.Upsert(ConversationPresets.Defaults, "Romantic", "warmer words");
            Assert.Equal(3, list.Count);                            // same name, case aside: rewritten
            Assert.Equal("warmer words", list.Single(i => i.Name == "Romantic").Text);

            list = ConversationPresets.Upsert(list, "blunt", "I want to say it plainly.");
            Assert.Equal(4, list.Count);

            list = ConversationPresets.Remove(list, " BLUNT ");
            Assert.Equal(3, list.Count);
            Assert.DoesNotContain(list, i => i.Name == "blunt");
        }

        [Fact]
        public void Upsert_ignores_an_empty_wish_and_the_list_never_runs_away()
        {
            Assert.Empty(ConversationPresets.Upsert(null, "name", "   "));

            var many = ConversationPresets.Parse(
                string.Concat(Enumerable.Range(0, ConversationPresets.MaxPresets + 10)
                    .Select(i => $"n{i} = wish {i}\n")));
            Assert.Equal(ConversationPresets.MaxPresets, many.Count);
        }
    }
}
