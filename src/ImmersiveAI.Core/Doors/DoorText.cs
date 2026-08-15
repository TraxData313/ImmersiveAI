using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Courtship;

namespace ImmersiveAI.Core.Doors
{
    /// <summary>
    /// Every word of the doors (2026.08.15) — her own section of the sheet, the fixed reasons the
    /// world lays down, the grey line at dusk, and the page the player reads.
    ///
    /// THE ONE RULE: nothing here tells her how to feel about anything. It tells her what is true —
    /// that her door is shut, that these are the words she wrote, that opening it is hers and
    /// nobody else's — and then gets out of the way. Every sentence was checked against that.
    ///
    /// The permanent marks are permanent, as all recorded phrasing here is.
    /// </summary>
    public static class DoorText
    {
        /// <summary>Opens the beat where the world laid something down. NEVER reword.</summary>
        public const string DoorBeatMark = "Of what stands between us:";

        public static bool IsDoorBeat(string? line) =>
            !string.IsNullOrEmpty(line) && line!.IndexOf(DoorBeatMark, StringComparison.Ordinal) >= 0;

        // ------------------------- her own sheet -------------------------

        /// <summary>
        /// Her private section, folded in beside the rest of what she carries of the player. Empty
        /// when nothing stands and nothing was ever settled — a woman with an open door and a clear
        /// history does not need a paragraph telling her so.
        /// </summary>
        public static string Section(string playerName, IReadOnlyList<DoorReason>? reasons,
            bool doorIsShut)
        {
            var kept = (reasons ?? new List<DoorReason>())
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Text)).ToList();
            if (kept.Count == 0) return string.Empty;

            var who = string.IsNullOrWhiteSpace(playerName) ? "him" : playerName.Trim();
            var sb = new StringBuilder();

            sb.AppendLine(doorIsShut
                ? $"My door is shut to {who}, and this is why — set down by my own hand, in my own words:"
                : $"What has stood between {who} and me, in my own words:");

            foreach (var reason in kept)
            {
                sb.Append("- ").Append(reason.Text.Trim().TrimEnd('.')).Append('.');
                if (reason.Settled)
                {
                    sb.Append(" (laid to rest");
                    if (!string.IsNullOrWhiteSpace(reason.SettledNote))
                        sb.Append(": ").Append(reason.SettledNote.Trim().TrimEnd('.'));
                    sb.Append(')');
                }
                else
                {
                    sb.Append(" (this still stands)");
                    if (!string.IsNullOrWhiteSpace(reason.Opens))
                        sb.Append(" What would answer it: ").Append(reason.Opens.Trim().TrimEnd('.')).Append('.');
                }
                sb.AppendLine();
            }

            // Hers to tend, exactly as the misgivings are — grow it, strike out what proved empty,
            // reword what changed shape, take back up what did not stay settled.
            sb.AppendLine("This list is mine and it lives with me: a fresh hurt I set down when it comes, one that "
                        + "proves to have been nothing I strike out, one that has changed shape I reword, and one I "
                        + "thought answered I may take up again if it turns out it was not. I am not shy of any of it.");

            // THE ANCHOR, and it is the same one the misgivings carry: she is told plainly what the
            // rule is, so that neither pretending nor sulking has anywhere to hide. Without this a
            // soul either holds the door shut forever out of drama, or opens it for a warm sentence.
            sb.Append("And I hold this plainly: while any of it still stands unanswered, my door stays shut, and no "
                    + "sweetness of an evening changes that. When nothing of mine stands any longer, nothing of mine "
                    + "bars the door — I do not invent a fresh grievance to keep it closed. Only my own hand lays one "
                    + "to rest, and I lay it to rest only when life has truly answered it, never for a warm word alone.");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// What she is told when the door is shut for no written reason at all — the feeling is
        /// simply gone. Deliberately spare, and deliberately NOT a grievance: inventing one would be
        /// the mod putting words in her mouth, which is the thing it exists not to do. It says the
        /// state and leaves her the whole of it.
        /// </summary>
        public static string ColdnessSection(string playerName, BondKind bond)
        {
            var who = string.IsNullOrWhiteSpace(playerName) ? "him" : playerName.Trim();
            return bond == BondKind.Wed
                ? $"My door is shut to {who} at present, and I have set down no reason for it. There is not one thing "
                + "I could point at; what used to be there simply is not, and I will not manufacture a grievance to "
                + "explain myself. If that changes, it will change because something between us changes."
                : $"My door is shut to {who} at present. There was never anything holding us but what I felt, and "
                + "what I felt has thinned. I owe no one an accounting of it.";
        }

        // ------------------------- what the world lays down -------------------------

        /// <summary>
        /// THE SPIRAL'S OWN WORDS — fixed, never generated, never brightened (the duty-night deck's
        /// own discipline, and the same reason: this is the one register in the mod that must not be
        /// made prettier by a model having a good day). Dealt by a stable hash so a night keeps the
        /// same one on a retry, and so a marriage that goes this way does not read as one sentence
        /// repeated.
        ///
        /// Every card holds the difference the whole design turns on: what is OWED against what was
        /// once GIVEN FREELY. One of them deliberately has no road back at all, because sometimes
        /// there is not one, and a system that always offers a way out is a system with no weight.
        /// </summary>
        public static readonly IReadOnlyList<DoorReason> DutyNightReasons = new[]
        {
            new DoorReason
            {
                Text = "He came to me on a night my door was shut, and I did what a wife does",
                Opens = "Time, and him not doing it again. There is nothing quicker than that.",
            },
            new DoorReason
            {
                Text = "He came anyway. I did not refuse him, and he never asked",
                Opens = "That he says out loud that he knows what he did, and does not dress it up.",
            },
            new DoorReason
            {
                Text = "He took what is owed him. That is not the same thing as what I used to give",
                Opens = "I do not know. That is the honest answer, and I am not going to invent a better one.",
            },
            new DoorReason
            {
                Text = "I lay still and waited for it to be over, in my own house, with my own husband",
                Opens = "Nothing he can say tonight. Something he does, over a long while.",
            },
        };

        /// <summary>The card this night is dealt — stable for a given seed, so a retry an hour later
        /// lays down the same thing rather than a second grievance.</summary>
        public static DoorReason DrawDutyReason(string? seed)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in seed ?? string.Empty) { hash ^= c; hash *= 16777619; }
                hash ^= hash >> 13; hash *= 2246822519; hash ^= hash >> 16;
                var card = DutyNightReasons[(int)(hash % (uint)DutyNightReasons.Count)];
                // A fresh instance every time: the deck is a template and must never be mutated by
                // a caller settling "the" reason and quietly settling it for every future marriage.
                return new DoorReason { Text = card.Text, Opens = card.Opens, LaidByTheWorld = true };
            }
        }

        // ------------------------- what the player reads -------------------------

        /// <summary>The grey line at dusk, beside her greyed-out name. Her first standing reason in
        /// her own words, cut to what the widget can hold — enough to know WHY without opening
        /// anything, which is the difference between a rule and a story.</summary>
        public static string DuskLine(DoorStanding standing, IReadOnlyList<DoorReason>? reasons,
            int maxChars = 90)
        {
            switch (standing)
            {
                case DoorStanding.HerSeason:
                    return "the custom of women is upon her";
                case DoorStanding.Coldness:
                    return "her door is shut, and she has given no reason";
                case DoorStanding.HerWord:
                    var first = DoorReasons.Standing(reasons).FirstOrDefault();
                    if (first == null) return "her door is shut";
                    var body = first.Text.Trim().TrimEnd('.');
                    if (body.Length > maxChars) body = body.Substring(0, maxChars).TrimEnd() + "…";
                    return "her door is shut: \"" + body + "\"";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The line under her name in the windows — the count, plainly, so a player can see
        /// a spiral getting deeper without opening a page.</summary>
        public static string StandingLabel(IReadOnlyList<DoorReason>? reasons)
        {
            int open = DoorReasons.OpenCount(reasons);
            int total = DoorReasons.TotalCount(reasons);
            if (total == 0) return string.Empty;
            if (open == 0) return "her door is open; what stood between you is at rest";
            return open == 1 ? "one thing stands between you" : $"{open} things stand between you";
        }

        /// <summary>The whole of it, for the page behind the one door — her words, what she said
        /// would answer each, and her own note on the ones she has laid to rest. This is the only
        /// feedback the repentance loop ever gives, and it is enough.</summary>
        public static string Page(string playerName, IReadOnlyList<DoorReason>? reasons)
        {
            var kept = (reasons ?? new List<DoorReason>())
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Text)).ToList();
            if (kept.Count == 0) return string.Empty;

            var who = string.IsNullOrWhiteSpace(playerName) ? "you" : playerName.Trim();
            var sb = new StringBuilder();
            var standing = kept.Where(r => r.Stands).ToList();
            var rested = kept.Where(r => !r.Stands).ToList();

            if (standing.Count > 0)
            {
                sb.AppendLine("What stands between you, in their own words:");
                sb.AppendLine();
                foreach (var reason in standing)
                {
                    sb.Append("• ").AppendLine(reason.Text.Trim().TrimEnd('.') + ".");
                    sb.AppendLine(string.IsNullOrWhiteSpace(reason.Opens)
                        ? "   What would answer it: they have not said. They may not know."
                        : "   What would answer it: " + reason.Opens.Trim());
                    sb.AppendLine();
                }
                sb.AppendLine("No coin settles any of this and no button opens it. Talk to them, and if what you say "
                            + "truly reaches them, they will lay it to rest themselves — or they will not.");
            }
            else
            {
                sb.AppendLine("Nothing stands between you now.");
            }

            if (rested.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Laid to rest:");
                foreach (var reason in rested)
                {
                    sb.Append("• ").Append(reason.Text.Trim().TrimEnd('.')).Append('.');
                    if (!string.IsNullOrWhiteSpace(reason.SettledNote))
                        sb.Append("  — \"").Append(reason.SettledNote.Trim()).Append('"');
                    sb.AppendLine();
                }
            }

            int fromTheWorld = DoorReasons.LaidByTheWorldCount(kept);
            if (fromTheWorld > 1)
            {
                sb.AppendLine();
                sb.AppendLine($"Of these, {fromTheWorld} were not raised in any conversation. They are nights.");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
