using System;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// THE NOTE-KEEPING HAND (2026.08.27, Anton's design) — she files a small keyed note the moment
    /// something is said, instead of waiting for a compression twenty turns away that would then
    /// rewrite her whole memory to hold it.
    ///
    /// <para>
    /// The contract is deliberately about WHAT DESERVES A KEY rather than about the mechanism: a
    /// plain fact worth keeping — a name, a promise, a debt, a wage, a thing owned or owed. Feelings
    /// are NOT notes; they live in the prose bite she writes at compression, and saying so here is
    /// what stops the shelf filling with moods.
    /// </para>
    ///
    /// <para>
    /// The resolver mutates the LIVE memory instance and saves at once — the discipline the retired
    /// truth/goal hands established and the courtship resolvers still keep. Anything else and the
    /// end-of-turn save would clobber a mid-reply write.
    /// </para>
    /// </summary>
    public static class NoteTool
    {
        public const string KeepNote = "keep_note";

        public const string ActWrite = "write";
        public const string ActStrike = "strike";

        public static readonly ToolDefinition Tool = new ToolDefinition(KeepNote,
            "Keep my own short notes about the one I speak with, each under a single word — a person, " +
            "a promise, a debt, my wage, a place, a thing owned or owed. I write one the moment a " +
            "plain fact worth keeping is said, and rewrite that word when the fact changes; I strike " +
            "one out when it is no longer true or no longer matters. A note is a LINE, never a " +
            "paragraph, and it holds facts, not feelings — how I feel about them is not a note. " +
            "Reusing a word I already keep replaces what stood under it, so one subject never " +
            "becomes four. I keep few, and keep them tight.",
            new[]
            {
                new ToolParameter("deed",
                    "One of these two words exactly: \"" + ActWrite + "\" — set a note down under a " +
                    "word (new, or replacing what that word held); \"" + ActStrike + "\" — strike out " +
                    "the note under a word.",
                    allowedValues: new[] { ActWrite, ActStrike }),
                // NAMED for what it holds, per the standing law (the misgivings' `text` lesson):
                // a field called "key" invites a model to file its whole answer under it.
                new ToolParameter("word",
                    "The single word or short phrase this note is filed under — the SUBJECT and " +
                    "nothing else: a name, a matter, a thing. Never the note itself, never my reply."),
                new ToolParameter("note",
                    "For \"" + ActWrite + "\": the fact itself, one short line in my own voice. Never " +
                    "my reply to them. Left out when striking one out.",
                    required: false),
            });

        /// <summary>The deed, canonicalised. Second line of defence behind the schema enum: honest
        /// synonyms count, and anything unreadable comes back empty so the resolver can say which
        /// words it wanted rather than doing nothing in silence.</summary>
        public static string ParseDeed(ToolCall call)
        {
            try
            {
                var raw = (JObject.Parse(call.ArgumentsJson)["deed"]?.ToString() ?? string.Empty)
                    .Trim().ToLowerInvariant();
                if (raw.Length == 0) return string.Empty;
                if (raw.Contains("strike") || raw.Contains("drop") || raw.Contains("remove")
                    || raw.Contains("delete") || raw.Contains("forget")) return ActStrike;
                if (raw.Contains("write") || raw.Contains("set") || raw.Contains("keep")
                    || raw.Contains("add") || raw.Contains("update") || raw.Contains("edit")) return ActWrite;
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public static string ParseWord(ToolCall call) => Field(call, "word", MemoryBites.MaxKeyChars * 2);

        public static string ParseNote(ToolCall call) => Field(call, "note", MemoryBites.MaxBiteChars * 2);

        private static string Field(ToolCall call, string name, int cap)
        {
            try
            {
                var text = (JObject.Parse(call.ArgumentsJson)[name]?.ToString() ?? string.Empty).Trim();
                return text.Length > cap ? text.Substring(0, cap) : text;
            }
            catch { return string.Empty; }
        }

        /// <summary>What she hears back. Always says plainly what happened — a resolver branch that
        /// does nothing SILENTLY is the bug the misgivings tool taught us; the loop has rounds left
        /// to correct itself if it is told.</summary>
        public static string Apply(NpcMemory memory, string deed, string word, string note)
        {
            if (memory == null) return "Nothing of mine takes it down just now.";

            if (deed.Length == 0)
                return "I did not say what I meant to do with it. I write a note down, or I strike one out.";
            if (MemoryBites.CanonicalKey(word).Length == 0)
                return "I named no word to file it under, so there is nothing to keep. A note needs its own word.";
            if (MemoryBites.IsProseKey(word))
                return "How things stand between us is not a note — that I set down in my own words when I "
                     + "next gather my thoughts. A note holds a plain fact.";

            if (deed == ActStrike)
            {
                var struck = MemoryBites.Drop(memory.Bites, word);
                return struck.Length > 0
                    ? $"It is struck out; I keep nothing under '{struck}' now."
                    : $"I keep no note under '{MemoryBites.CanonicalKey(word)}', so there is nothing to strike out.";
            }

            if (note.Trim().Length == 0)
                return "I gave the word but not the fact to keep under it. A note needs its line.";

            var canon = MemoryBites.CanonicalKey(word);
            bool replacing = memory.Bites.ContainsKey(canon);
            if (!replacing && MemoryBites.IsFull(memory.Bites))
                return MemoryBites.ShelfFullNote() + " I strike one out first, then set this down.";

            var written = MemoryBites.Set(memory.Bites, word, note);
            if (written.Length == 0)
                return "It would not take. I try once more, shorter, under a plainer word.";

            return replacing
                ? $"It is written; '{written}' now holds what I have just set down, and what stood there is gone."
                : $"It is written, under '{written}'. I speak on.";
        }
    }
}
