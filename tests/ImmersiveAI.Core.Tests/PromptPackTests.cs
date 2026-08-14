using System;
using System.Collections.Generic;
using ImmersiveAI.Core.Prompts;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    /// <summary>
    /// The prompt pack is the file the PLAYER edits by hand, so every test here is really the same
    /// question asked in different clothes: can a person typing at two in the morning break their
    /// own game with it? The answer must always be no — the worst a bad line may cost is that one
    /// line, and the compiled-in default steps into its place.
    /// </summary>
    public class PromptPackTests
    {
        [Fact]
        public void Missing_key_falls_back_to_the_default()
        {
            var pack = PromptPack.Parse("greeting = \"hello\"");
            Assert.Equal("hello", pack.Get("greeting", "DEFAULT"));
            Assert.Equal("DEFAULT", pack.Get("absent", "DEFAULT"));
        }

        [Fact]
        public void An_empty_file_leaves_every_default_standing()
        {
            Assert.Equal("DEFAULT", PromptPack.Parse("").Get("anything", "DEFAULT"));
            Assert.Equal("DEFAULT", PromptPack.Parse(null).Get("anything", "DEFAULT"));
            Assert.Equal("DEFAULT", PromptPack.Empty.Get("anything", "DEFAULT"));
        }

        [Fact]
        public void Both_comment_conventions_are_ignored()
        {
            var pack = PromptPack.Parse("# a note\n// another\nkey = \"value\"");
            Assert.Equal("value", pack.Get("key", "x"));
        }

        [Fact]
        public void A_hash_inside_a_value_is_kept()
        {
            var pack = PromptPack.Parse("key = \"a # not a comment\"");
            Assert.Equal("a # not a comment", pack.Get("key", "x"));
        }

        [Fact]
        public void Unquoted_values_are_taken_as_written()
        {
            var pack = PromptPack.Parse("key = plain words");
            Assert.Equal("plain words", pack.Get("key", "x"));
        }

        [Fact]
        public void Blocks_keep_their_own_line_breaks()
        {
            var pack = PromptPack.Parse("key = \"\"\"\nfirst\nsecond\n\"\"\"");
            Assert.Equal("first\nsecond", pack.Get("key", "x"));
        }

        [Fact]
        public void A_block_that_was_never_closed_keeps_its_text_and_complains()
        {
            var pack = PromptPack.Parse("key = \"\"\"\nfirst\nsecond");
            Assert.Equal("first\nsecond", pack.Get("key", "x"));
            Assert.NotEmpty(pack.Complaints);
        }

        [Fact]
        public void A_broken_line_costs_only_itself()
        {
            var pack = PromptPack.Parse("good = \"kept\"\nthis line is nonsense\nalso = \"kept too\"");
            Assert.Equal("kept", pack.Get("good", "x"));
            Assert.Equal("kept too", pack.Get("also", "x"));
            Assert.NotEmpty(pack.Complaints);
        }

        [Fact]
        public void Escapes_survive_a_single_line_value()
        {
            var pack = PromptPack.Parse("key = \"one\\ntwo and a \\\"quote\\\"\"");
            Assert.Equal("one\ntwo and a \"quote\"", pack.Get("key", "x"));
        }

        [Fact]
        public void A_deliberately_emptied_key_stays_empty()
        {
            // Emptying a key is how a player switches a piece of guidance off — it must NOT quietly
            // fall back to the compiled-in text, or the switch does nothing.
            var pack = PromptPack.Parse("key = \"\"");
            Assert.True(pack.Has("key"));
            Assert.Equal("", pack.Get("key", "DEFAULT"));
        }

        [Fact]
        public void The_last_writing_of_a_repeated_key_wins()
        {
            var pack = PromptPack.Parse("key = \"first\"\nkey = \"second\"");
            Assert.Equal("second", pack.Get("key", "x"));
        }

        [Fact]
        public void Keys_are_matched_however_they_are_cased()
        {
            var pack = PromptPack.Parse("Sheet.Guidance = \"value\"");
            Assert.Equal("value", pack.Get("sheet.guidance", "x"));
        }

        [Fact]
        public void Whitespace_around_a_line_does_not_matter()
        {
            var pack = PromptPack.Parse("   key   =   \"value\"   ");
            Assert.Equal("value", pack.Get("key", "x"));
        }

        [Fact]
        public void Windows_line_endings_read_the_same_as_any_other()
        {
            var pack = PromptPack.Parse("key = \"\"\"\r\nfirst\r\nsecond\r\n\"\"\"\r\nother = \"x\"");
            Assert.Equal("first\nsecond", pack.Get("key", "?"));
            Assert.Equal("x", pack.Get("other", "?"));
        }

        [Fact]
        public void A_key_this_build_does_not_know_is_kept_aside_not_thrown_away()
        {
            var known = new List<string> { "known.key" };
            var pack = PromptPack.Parse("known.key = \"a\"\nfrom.the.future = \"b\"", known);
            Assert.Contains("from.the.future", pack.UnknownKeys);
            Assert.Equal("b", pack.Get("from.the.future", "x"));
        }

        [Fact]
        public void What_is_written_can_be_read_back_unchanged()
        {
            // The round trip is the whole promise of the format: whatever the file generator writes,
            // the parser must hand back the same words.
            const string oneLine = "I speak plainly, and \"quote\" now and then.";
            const string manyLines = "First line.\n\nThird line, after a blank one.";

            var text = PromptPack.RenderEntry("a.one", oneLine, "a note", new[] { "name" })
                       + PromptPack.RenderEntry("a.many", manyLines, "another note", null, "order is identity");

            var pack = PromptPack.Parse(text);
            Assert.Equal(oneLine, pack.Get("a.one", "?"));
            Assert.Equal(manyLines, pack.Get("a.many", "?"));
            Assert.Empty(pack.Complaints);
        }

        [Fact]
        public void A_rendered_entry_carries_its_explanation_as_comments()
        {
            var text = PromptPack.RenderEntry("k", "v", "why this exists", new[] { "name", "place" }, "do not reorder");
            Assert.Contains("# why this exists", text);
            Assert.Contains("{name}", text);
            Assert.Contains("{place}", text);
            Assert.Contains("# ! do not reorder", text);
        }

        [Fact]
        public void Multi_line_text_is_written_as_a_block_not_as_escapes()
        {
            // A wall of backslash-n is not something a person can edit. Anything with a line break
            // in it must come out fenced.
            var text = PromptPack.RenderEntry("k", "one\ntwo");
            Assert.Contains("\"\"\"", text);
            Assert.DoesNotContain("\\n", text);
        }
    }
}
