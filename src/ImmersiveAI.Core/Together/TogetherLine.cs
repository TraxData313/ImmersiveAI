using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Together
{
    /// <summary>What sort of thing an entry tells — so a long run of look-alikes can be gathered
    /// into one honest line instead of ten (Anton, 2026.08.27: the token audit found the line
    /// retelling every battle the chronicle block had already told, one line each).</summary>
    public enum TogetherKind
    {
        /// <summary>Everything singular — nights, and whatever else earns its own line.</summary>
        Other = 0,
        /// <summary>A battle stood in together; the chronicle keeps the whole of it.</summary>
        Battle = 1,
        /// <summary>A stop on the shared road; the journal keeps the whole of it.</summary>
        Stop = 2,
    }

    /// <summary>One thing that has happened since the two of them were last alone, in her own
    /// first person and already dated. Deliberately ONE LINE — this is a list, not a chronicle;
    /// the depth lives in the blocks above it.</summary>
    public sealed class TogetherEntry
    {
        public double GameDay { get; set; }
        /// <summary>The day in the world's own words ("Winter 9"); empty falls back to nothing.</summary>
        public string DateText { get; set; } = string.Empty;
        /// <summary>What happened, in her voice, one line ("we were at the market in Baltakhand").</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>What sort of thing this is; Other never gathers.</summary>
        public TogetherKind Kind { get; set; } = TogetherKind.Other;
    }

    /// <summary>
    /// THE LINE (2026.08.09, Anton's design, and it replaced three cleverer attempts): one plain
    /// mark in a soul's sheet at the last moment the two of them had time to themselves, and after
    /// it a simple dated list of everything that has happened since.
    ///
    /// WHY IT EXISTS. Every event this mod records — a battle, a market, a night he spent elsewhere
    /// — lands in her sheet as knowledge, and knowledge reads as SETTLED BACKGROUND. She would greet
    /// him the morning after a night with another wife as though it had all been talked through
    /// off-stage. Marking the line fixes it in the one way that never sounds like an accusation:
    /// here is where our words stop, this came after, and raising it is hers if she wants it raised.
    ///
    /// WHAT MOVES THE LINE, and this is the whole of the rule: TIME ALONE. A conversation moves it.
    /// A night together moves it. The wedding night moves it. A battle does NOT, a market does NOT,
    /// hearing where he slept does NOT — those are the very things the list is for.
    ///
    /// AND IT DISAPPEARS ON ITS OWN. The moment she says anything the line is today, nothing stands
    /// after it, and <see cref="Build"/> returns nothing at all — until the next undiscussed thing
    /// happens. No state, no flags: it falls straight out of "only render what came after".
    /// </summary>
    public static class TogetherLine
    {
        /// <summary>
        /// THE WHOLE OF IT — one divider, then the list. Nothing else.
        ///
        /// IT WAS DELIBERATELY CUT DOWN TO THIS (Anton, 2026.08.09; the third and final wording).
        /// Two things stood here before and both are gone on purpose:
        /// • an opening mark naming when they were last alone and what it was — unnecessary, since
        ///   every recorded turn already carries its own [place, time] stamp and every entry below
        ///   is dated, so a soul can see the shape of it without being told;
        /// • a closing sentence granting the word-in-passing and reminding her the list was hers to
        ///   raise or to let lie — cut to find out how much a soul works out unaided. Long rules
        ///   make every soul answer the same; that lesson is already written into the reach-out
        ///   ponder, and it is worth spending here too.
        /// The two words carrying all of the old closing's work are A PRIVATE DISCUSSION: they leave
        /// the door open that light, passing remarks were made, while saying plainly that the two of
        /// them never sat down to any of it. If a live sample ever reads as an accusation, or as a
        /// briefing recited straight back, the closing is the thing to try again — but only AFTER
        /// seeing what they do with this.
        ///
        /// "FROM THIS MOMENT" points at the divider's own place in the reading, not at something
        /// earlier (Anton's last pass, and it fixes a real weakness in the version before it: the
        /// block stands in the sheet BEFORE the transcript, so a "since then" had nothing behind it
        /// to refer to yet). Here the mark IS the moment, and everything under it came after.
        /// </summary>
        public const string ListHeader =
            "From this moment until now we had not sat in a private discussion like now, "
            + "here is what happened since then:";

        /// <summary>
        /// The block, or an empty string when nothing has happened since they were last alone —
        /// which is the common case right after a talk, and is exactly when it should be invisible.
        /// </summary>
        /// <param name="since">Everything after the line; sorted and capped here.</param>
        /// <param name="maxEntries">The most lines to tell. Older ones fall away first — what is
        /// freshest is what is likeliest to be raised.</param>
        public static string Build(IReadOnlyList<TogetherEntry> since, int maxEntries = 12)
        {
            var kept = (since ?? new List<TogetherEntry>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Text))
                .OrderBy(e => e.GameDay)
                .ToList();
            if (kept.Count == 0) return string.Empty;
            kept = Gather(kept);
            if (maxEntries > 0 && kept.Count > maxEntries)
                kept = kept.Skip(kept.Count - maxEntries).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(ListHeader);
            foreach (var entry in kept) sb.Append("· ").AppendLine(Line(entry));
            return sb.ToString().TrimEnd();
        }

        /// <summary>A kind gathers only from this many entries up. THREE, never two — a pair is
        /// still two events and each keeps its own nuance (the nights' RunLine settled that).</summary>
        public const int GatherFrom = 3;

        // A kind that filled the list with look-alikes folds into one line carrying the span and
        // the count — the depth already lives in the chronicle and the journal above, and the
        // gathered line says where. Gathering runs BEFORE the cap, so a long war no longer pushes
        // the one night that mattered off the list. Other never gathers.
        private static List<TogetherEntry> Gather(List<TogetherEntry> entries)
        {
            foreach (var kind in new[] { TogetherKind.Battle, TogetherKind.Stop })
            {
                var ofKind = entries.Where(e => e.Kind == kind).ToList();
                if (ofKind.Count < GatherFrom) continue;

                var first = ofKind[0];
                var last = ofKind[ofKind.Count - 1];
                var gathered = new TogetherEntry
                {
                    GameDay = last.GameDay,
                    DateText = string.Empty,        // the span lives in the words instead
                    Kind = kind,
                    Text = GatheredText(kind, ofKind.Count, ShortDate(first.DateText), ShortDate(last.DateText)),
                };
                entries = entries.Where(e => e.Kind != kind).ToList();
                int at = entries.FindIndex(e => e.GameDay > gathered.GameDay);
                entries.Insert(at < 0 ? entries.Count : at, gathered);
            }
            return entries;
        }

        private static string GatheredText(TogetherKind kind, int count, string firstDate, string lastDate)
        {
            var span = firstDate.Length == 0 || firstDate == lastDate
                ? (lastDate.Length == 0 ? "in those days" : $"on {lastDate}")
                : $"from {firstDate} to {lastDate}";
            return kind == TogetherKind.Battle
                ? $"{span} we stood in {count} battles together — the chronicle keeps each of them by name"
                : $"{span} we made {count} stops about the company's business";
        }

        /// <summary>One entry, dated. A day with no name of its own simply goes undated rather than
        /// wearing a number no one in this world would use.</summary>
        public static string Line(TogetherEntry entry)
        {
            if (entry == null) return string.Empty;
            var text = (entry.Text ?? string.Empty).Trim();
            var date = ShortDate(entry.DateText);
            return date.Length == 0 ? text : $"{date}: {text}";
        }

        /// <summary>"Winter 9, Year 1084" → "Winter 9". The year is the same year; saying it on
        /// every line of a fortnight is noise.</summary>
        public static string ShortDate(string? dateText)
        {
            var text = (dateText ?? string.Empty).Trim();
            if (text.Length == 0) return string.Empty;
            int comma = text.IndexOf(',');
            return (comma > 0 ? text.Substring(0, comma) : text).Trim();
        }
    }
}
