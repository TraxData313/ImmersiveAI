using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// THE REGISTRY OVER THE PROMPT PACK — its first keys (2026.10.13). The player's
    /// <c>prompts.txt</c> (see <see cref="PromptPack"/> for the format and its laws) holds the words
    /// listed here; everything else an NPC reads is still compiled in. This first cut covers what a
    /// player asked for by name on Steam: the deep memory (how it is headed on the sheet, and the
    /// whole of what an NPC is told when they gather and settle it) and the backstory (how the story
    /// the world tells of them is framed, and what they had heard of the player before ever meeting).
    /// The rest of the sweep is docs/one-screen-plan.md, Phase 3.
    ///
    /// <para>
    /// An entry's <see cref="Entry.Default"/> is the compiled-in text, so a missing file, a missing
    /// key or a broken line changes nothing at all. Every key is written into the file with its
    /// default the first time it is created (or when a newer build adds the key), which is what lets
    /// a player see the words they are changing — and which is also why a CHANGED default needs its
    /// old wording moved into <see cref="Entry.Previously"/>: a file still holding the old wording
    /// untouched then counts as untouched and gets the new one, instead of pinning every existing
    /// player to the text as it shipped the day they installed.
    /// </para>
    ///
    /// <para>
    /// Parse labels (<c>SUMMARY:</c>, <c>SELF:</c>) and the <c>unchanged</c> marker are NOT here and
    /// must not be: they are read back out of the answer, and a reworded one would quietly stop
    /// memories being saved. The SELF block of a reflection stays compiled in for the same reason —
    /// it carries the marker in its own words.
    /// </para>
    /// </summary>
    public static class PromptTexts
    {
        /// <summary>One editable piece of text: its key in prompts.txt, the shipped words, the note
        /// written above it, the {slots} it fills in, and any wording it shipped with before.</summary>
        public sealed class Entry
        {
            public string Key { get; }
            public string Default { get; }
            public string Comment { get; }
            public IReadOnlyList<string> Slots { get; }
            public IReadOnlyList<string> Previously { get; }

            public Entry(string key, string @default, string comment, string[]? slots = null, string[]? previously = null)
            {
                Key = key;
                Default = @default;
                Comment = comment;
                Slots = slots ?? Array.Empty<string>();
                Previously = previously ?? Array.Empty<string>();
            }

            /// <summary>The words in force now: the player's when they wrote any, else the default.</summary>
            public string Text => Resolve(Pack, this);

            /// <summary><see cref="Text"/> with its {slots} filled in, as name/value pairs.</summary>
            public string Fill(params string[] slotsAndValues) => FillSlots(Text, slotsAndValues);
        }

        // Declared before the entries: static initializers run in the order they are written, and
        // every entry adds itself to this list as it is made.
        private static readonly List<Entry> Registry = new List<Entry>();

        private static Entry Add(string key, string @default, string comment,
                                 string[]? slots = null, string[]? previously = null)
        {
            var entry = new Entry(key, @default, comment, slots, previously);
            Registry.Add(entry);
            return entry;
        }

        // ── The deep memory, as it stands on the sheet ─────────────────────────────────────────

        public static readonly Entry DeepMemoryHeading = Add(
            "sheet.deep_memory.heading",
            "What {player} is to me{as_of}:",
            "The line above an NPC's deep memory of you on their sheet - the one memory they rewrite\n"
            + "whole each time they gather their thoughts.",
            new[] { "player", "as_of" });

        public static readonly Entry DeepMemoryAsOf = Add(
            "sheet.deep_memory.as_of",
            " (as I last gathered my thoughts on {date})",
            "Fills {as_of} above, once they have gathered their thoughts at least once. Keep the\n"
            + "leading space, or empty it to leave the date out.",
            new[] { "date" });

        public static readonly Entry DeepMemoryOwnStory = Add(
            "sheet.deep_memory.own_story",
            "The road of my life so far, as I carry it in memory:",
            "The same place, while their deep memory still holds only their own backstory - before\n"
            + "anything has truly passed between you.");

        // ── The deep memory, as it is gathered (an old conversation folded into it) ────────────

        public static readonly Entry GatherMoment = Add(
            "memory.gather.moment",
            "I sit a moment with my memory of this person and settle it.",
            "When a conversation grows long, its oldest moments are folded into the deep memory by one\n"
            + "private call. This opens it, inside \"(Within my own mind - I, <name>: ...)\".");

        public static readonly Entry GatherSpirit = Add(
            "memory.gather.spirit",
            "Time moves on, and older moments are slipping from sharp memory into the haze of the past. "
            + "It is mine alone to decide what to carry forward and what to let go. I keep what matters to who I "
            + "am and what I care about; I release the rest. I speak in my own voice.",
            "How they are to go about it.");

        public static readonly Entry GatherHolding = Add(
            "memory.gather.holding",
            "What I already hold in memory (I weave the fading moments into it):",
            "Heads the deep memory they already carry.");

        public static readonly Entry GatherFading = Add(
            "memory.gather.fading",
            "The moments now fading (I fold these into my memory):",
            "Heads the old moments being folded in.");

        public static readonly Entry GatherFresh = Add(
            "memory.gather.fresh",
            "Still fresh in my mind (context only — these stay with me; I do not fold them in yet):",
            "Heads the recent moments that stay word for word.");

        // ── The deep memory, as it is reflected on ("gather your thoughts") ──────────────────

        public static readonly Entry ReflectMoment = Add(
            "memory.reflect.moment",
            "I pause a while and gather my thoughts about this person.",
            "When you ask someone to gather their thoughts, they rewrite their deep memory of you on\n"
            + "purpose. This opens it, inside \"(Within my own mind - I, <name>: ...)\".");

        public static readonly Entry ReflectSpirit = Add(
            "memory.reflect.spirit",
            "I settle my memory of them as I see fit — I keep what matters to who I am and what I "
            + "care about, refine what has changed, and let go of what no longer serves. I speak in my own voice.",
            "How they are to go about it.");

        public static readonly Entry ReflectHolding = Add(
            "memory.reflect.holding",
            "What I already hold in memory (I revise it as I reflect):",
            "Heads the deep memory they already carry.");

        public static readonly Entry ReflectFading = Add(
            "memory.reflect.fading",
            "Older moments now fading (I fold these into my memory):",
            "Heads the old moments being folded in, when there are any.");

        public static readonly Entry ReflectFresh = Add(
            "memory.reflect.fresh",
            "Still fresh in my mind (these remain with me — I draw on them, but they are not fading yet):",
            "Heads the recent moments that stay word for word.");

        // ── What the new deep memory must be (both of the above) ───────────────────────────────

        public static readonly Entry ContractShape = Add(
            "memory.contract.shape",
            "I set it down in exactly this shape:",
            "Introduces the answer's shape. The SUMMARY: label that follows is read back by the mod\n"
            + "and is not editable.");

        public static readonly Entry ContractSummary = Add(
            "memory.contract.summary",
            "<all I carry of them, in my own first-person voice — who they are to me and how that came to be, "
            + "and the particular things I would not lose: what was said and promised between us, what was given and done, the names that matter. "
            + "Ledgers I do not hoard: gear, goods, and sums live with me as what they mean — a kindness, a debt, a trust growing — "
            + "never as lists of items and prices; the world's own chronicles keep the numbers. "
            + "What I carry of my own road from before also lives here — mine to keep, reshape, or let fade, as I choose.>",
            "What the deep memory should hold. This is the strongest lever on what they remember.");

        public static readonly Entry ContractWhole = Add(
            "memory.contract.whole",
            "I write it whole each time, for what I do not set down here fades from me. "
            + "I take as much room as the story truly asks — a few paragraphs, and more where there is more to hold.",
            "The deep memory is rewritten whole each time. Without a line like this one, each rewrite\n"
            + "quietly loses a little.");

        // ── The backstory: the first page of a deep memory ─────────────────────────────────────

        public static readonly Entry BackstoryWorldStory = Add(
            "backstory.world_story",
            "So runs my story, as the world tells it: {story}",
            "A noble's backstory - the account the world keeps of them - framed as their own memory.\n"
            + "A wanderer's tavern tale is their own words from the game and is used as it is.\n"
            + "Backstories are written into a deep memory ONCE, the first time you meet someone: a change\n"
            + "here reaches the people you meet from then on, not the ones who already carry theirs.",
            new[] { "story" });

        public static readonly Entry BackstoryFameFamed = Add(
            "backstory.fame.famed",
            "Of {player}, before ever we spoke, the world had long been telling: a name famed across all Calradia, whose deeds are recounted at many fires.",
            "What someone had heard of you before you ever met, closing their backstory. This one at\n"
            + "renown 900 and above; nothing at all below 150. Written once, like the backstory.",
            new[] { "player" });

        public static readonly Entry BackstoryFameKnown = Add(
            "backstory.fame.known",
            "Of {player}, before ever we spoke, the world had already told me much: a name carried far across the land, with no small tale of deeds behind it.",
            "The same, at renown 300 and above.",
            new[] { "player" });

        public static readonly Entry BackstoryFameHeard = Add(
            "backstory.fame.heard",
            "Of {player}, before ever we spoke, some word had already reached me: a name with deeds beginning to travel behind it.",
            "The same, at renown 150 and above.",
            new[] { "player" });

        /// <summary>Every editable entry, in the order the file lists them.</summary>
        public static IReadOnlyList<Entry> All => Registry;

        /// <summary>The keys this build knows, for <see cref="PromptPack.Parse"/>.</summary>
        public static ICollection<string> Keys =>
            new HashSet<string>(Registry.Select(e => e.Key), StringComparer.OrdinalIgnoreCase);

        private static volatile PromptPack _pack = PromptPack.Empty;

        /// <summary>The player's file as last read. The game layer re-reads it whenever it changes;
        /// until then — and in every test — this is empty and every entry answers its default.</summary>
        public static PromptPack Pack
        {
            get => _pack;
            set => _pack = value ?? PromptPack.Empty;
        }

        /// <summary>The words an entry answers with under a given pack. A key the player has not
        /// written, or still holds at a wording this entry shipped with before, answers the current
        /// default; anything else is the player's, an emptied key included (that is the off switch).</summary>
        public static string Resolve(PromptPack pack, Entry entry)
        {
            if (pack == null || !pack.Has(entry.Key)) return entry.Default;
            var mine = pack.Get(entry.Key, entry.Default);
            foreach (var old in entry.Previously)
                if (SameWords(mine, old)) return entry.Default;
            return mine;
        }

        private static string Normalize(string? text) =>
            (text ?? string.Empty).Replace("\r\n", "\n").Trim();

        private static bool SameWords(string? a, string? b) =>
            string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

        /// <summary>Fills {slots} from name/value pairs. A slot the player removed simply is not
        /// filled; one they misspelled stays visible as written, which is how they find it.</summary>
        public static string FillSlots(string template, params string[] slotsAndValues)
        {
            var text = template ?? string.Empty;
            for (int i = 0; i + 1 < slotsAndValues.Length; i += 2)
                text = text.Replace("{" + slotsAndValues[i] + "}", slotsAndValues[i + 1] ?? string.Empty);
            return text;
        }

        /// <summary>Appends a line unless the player emptied it — an emptied piece leaves no gap.</summary>
        public static void AppendLineIfAny(StringBuilder sb, string? line)
        {
            if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
        }

        /// <summary>The opening of a new prompts.txt: what it is and how to use it.</summary>
        public const string FileHeader =
@"# Immersive AI - prompts.txt: the words behind the prompt, yours to change
#
# Each entry below is a piece of text the characters are given, as it ships. Change the
# words between the quotes (or between the """""" fences, for longer ones) and the change takes
# effect from the next reply - no restart needed. Lines starting with # or // are ignored.
#
# - {words} in curly brackets are filled in by the game (a name, a date). Each entry lists
#   the ones it fills in; leave them where you want them to appear.
# - To switch a piece off, empty it:  key = """"
# - To bring back the shipped words, delete the whole entry. It is written back with the
#   shipped words the next time the file is read. Deleting this whole file restores them all.
# - Some text is not here on purpose: labels the game reads back out of the answers
#   (SUMMARY:, SELF:), which would quietly stop memories being saved if they were reworded.
#
# For what every character knows of the world, use global_prompt.txt (""World prompt"" in
# game); for one character's own truths, their custom_instructions.txt (""Their prompt"").
";

        /// <summary>A whole new file: the header and every entry with its shipped words.</summary>
        public static string RenderFile() => FileHeader + "\n" + RenderEntries(Registry);

        /// <summary>The entries a file does not have yet — to be appended, never rewritten over
        /// what the player already wrote. Empty when nothing is missing.</summary>
        public static string RenderMissing(PromptPack pack) =>
            RenderEntries(Registry.Where(e => pack == null || !pack.Has(e.Key)));

        private static string RenderEntries(IEnumerable<Entry> entries)
        {
            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(PromptPack.RenderEntry(e.Key, e.Default, e.Comment, e.Slots));
            }
            return sb.ToString();
        }
    }
}
