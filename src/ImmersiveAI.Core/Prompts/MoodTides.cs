using System;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// The passing weather of the heart: every soul carries a daily humor, and the women of the
    /// world also keep their body's own monthly season — the custom of women — whose phase colors
    /// the humor and is narrated to her gently so she can weigh it in her own choices, as living
    /// women do. Everything derives from the NPC's stable id and the campaign day (no state, no
    /// persistence, no rolls): the same soul on the same day is always in the same weather across
    /// saves and sessions, and tomorrow the wind simply turns. Narration is in the NPC's own first
    /// person, like the rest of the sheet — a body's season and a day's humor, never a chart.
    /// </summary>
    public static class MoodTides
    {
        /// <summary>The body's season, in four turnings of the monthly cycle. Named in-world:
        /// the days of the custom itself, the rising days after, the crest, and the waning days
        /// before it returns.</summary>
        public enum CyclePhase
        {
            Menses,
            Rising,
            Crest,
            Waning
        }

        // The palette of daily humors — each phrase completes "This day finds me ...". Kept in the
        // NPC's own first person, like the rest of the sheet; a couple of even-keeled entries so not
        // every day blows a wind.
        private static readonly string[] Humors =
        {
            /*  0 bright     */ "in bright spirits — small things please me, and laughter comes easily",
            /*  1 even       */ "even and steady — no great wind moves me, and the day is simply the day",
            /*  2 weary      */ "weary in my bones — the days have asked much of me, and my patience runs a little shorter than my love",
            /*  3 melancholy */ "touched with melancholy — old memories drift near, and my thoughts run deep and quiet",
            /*  4 restless   */ "restless — stillness sits ill with me today, and I itch for something to happen",
            /*  5 prickly    */ "prickly — small annoyances land harder than they should, and I know it of myself",
            /*  6 hopeful    */ "hopeful — tomorrow looks kind from where I stand, and plans come easily",
            /*  7 playful    */ "playful — jests rise quick to my tongue, and I am inclined to tease and be teased",
            /*  8 pensive    */ "pensive — my mind keeps turning to the large questions, and talk of deep things would suit me",
            /*  9 tender     */ "tender-hearted — the sorrows of others touch me quickly today, and kindness comes before judgment",
            /* 10 bold       */ "bold — my blood is up, and I would sooner dare than wait",
            /* 11 brooding   */ "brooding — something I cannot quite name sits on my chest, and words come slower for it",
            /* 12 warm       */ "warm — glad of company, quick to praise, and slow to take offense",
            /* 13 wistful    */ "wistful — thinking of places and people far away, and of roads not taken",
            /* 14 vigorous   */ "brimming with vigor — sleep served me well, and I could move mountains before supper",
            /* 15 guarded    */ "guarded — I hold my cards a little closer today, and trust must earn its place",
            /* 16 stern      */ "stern — my patience for folly is thin today, and I speak my mind more plainly than is kind",
            /* 17 merry      */ "quietly merry — a small gladness hums under everything I do, though I could not say from where",
            /* 18 hearthsick */ "longing for the hearth — the road wearies me, and my thoughts keep drifting to home fires and familiar faces",
            /* 19 openhanded */ "open-handed — today I would give more than I ought, and forgive more than is wise",
        };

        // Which humors each turning of the body's season leans toward. Two days of three the season
        // colors the pick from its own cluster; the third day the whole palette stays open, so the
        // interplay of season and day still surprises.
        private static readonly int[] MensesHumors = { 2, 3, 9, 11, 13, 18 };
        private static readonly int[] RisingHumors = { 0, 6, 8, 12, 14, 19 };
        private static readonly int[] CrestHumors = { 0, 7, 10, 12, 14, 17 };
        private static readonly int[] WaningHumors = { 2, 4, 5, 11, 15, 16 };

        // The humors a woman carrying a child leans toward — tenderness, weariness, warmth, and the
        // pull toward home; the same two-days-of-three coloring as the monthly turnings.
        private static readonly int[] WithChildHumors = { 2, 9, 12, 13, 18 };

        /// <summary>This woman's own cycle length in days — hers for life, seeded from her id.</summary>
        public static int CycleLength(string npcId) => 26 + (int)(Hash(npcId + ":cycle") % 5u);

        /// <summary>
        /// Where the body's season stands on the given campaign day. The offset is seeded from the
        /// id, so each woman keeps her own calendar; <paramref name="dayOfCycle"/> runs 1..length.
        /// Phases scale with the length (for the classic 28: the custom days 1–5, rising 6–13,
        /// crest 14–16, waning 17–28).
        /// </summary>
        public static CyclePhase PhaseOf(string npcId, int campaignDay, out int dayOfCycle)
        {
            int length = CycleLength(npcId);
            int offset = (int)(Hash(npcId + ":offset") % (uint)length);
            dayOfCycle = ((campaignDay + offset) % length + length) % length + 1;

            int crestStart = length / 2;
            if (dayOfCycle <= 5) return CyclePhase.Menses;
            if (dayOfCycle < crestStart) return CyclePhase.Rising;
            if (dayOfCycle <= crestStart + 2) return CyclePhase.Crest;
            return CyclePhase.Waning;
        }

        /// <summary>
        /// The day's humor for this soul — deterministic in the id and the day. When a
        /// <paramref name="phase"/> rides along (a woman in her body's season), two days of three
        /// the pick leans into that season's cluster; otherwise the whole palette is open.
        /// </summary>
        public static string DailyHumor(string npcId, int campaignDay, CyclePhase? phase = null)
        {
            uint roll = Hash(npcId + ":humor:" + campaignDay);
            if (phase.HasValue && roll % 3u != 0u)
            {
                var cluster = ClusterOf(phase.Value);
                return Humors[cluster[(int)((roll / 3u) % (uint)cluster.Length)]];
            }
            return Humors[(int)((roll / 3u) % (uint)Humors.Length)];
        }

        /// <summary>Where her body's season stands, in her own words — reverent and plain, in the old
        /// phrasing ("the custom of women"), never a clinician's chart. One sentence-run, closing on
        /// how it may color her, so she can weigh it in her own choices.</summary>
        public static string CycleSentence(CyclePhase phase)
        {
            const string lead = "And my body keeps its own season, as it does for every woman: ";
            switch (phase)
            {
                case CyclePhase.Menses:
                    return lead + "the custom of women is upon me in these days. My strength runs " +
                           "lower than usual, tears lie nearer the surface, and my thoughts can mist " +
                           "over — my body asks for gentleness, and it is no weakness to grant it.";
                case CyclePhase.Rising:
                    return lead + "these are the rising days, the custom lately passed — strength " +
                           "returning and growing, a clear head, and company sitting well with me.";
                case CyclePhase.Crest:
                    return lead + "this is the crest of it, a few days only — I feel bold and warm " +
                           "and full of life, drawn toward people and glad of warmth returned.";
                default:
                    return lead + "these are the waning days before the custom of women returns. My " +
                           "feelings run closer to the skin than usual — quicker to stir, quicker to " +
                           "bruise, quicker to flare — and comfort calls to me; what would slide off " +
                           "me another week may sting today. Knowing it of myself is half the " +
                           "mastery of it.";
            }
        }

        /// <summary>How a woman carrying a child feels her body's season this day — the monthly
        /// turnings rest while the child grows, but the body is anything but silent. A handful of
        /// first-person variants picked deterministically by the day, so the days differ and a
        /// reload changes nothing.</summary>
        public static string WithChildSentence(string npcId, int campaignDay)
        {
            var sentences = new[]
            {
                "And my body keeps a season of its own: the child within me makes itself known — I " +
                "tire sooner than I would, and I guard my steps.",
                "And my body keeps a season of its own: the child within me sits quiet today, and I " +
                "feel strong — almost myself, and something more.",
                "And my body keeps a season of its own: the child within me stirs, and a fierce " +
                "tenderness swells in me — tears lie near the surface, for joy as much as anything.",
                "And my body keeps a season of its own: the child within me hungers, and so do I — " +
                "strange longings for tastes I could not name yesterday.",
            };
            return sentences[(int)(Hash(npcId + ":carrying:" + campaignDay) % (uint)sentences.Length)];
        }

        /// <summary>
        /// The whole mood paragraph for the situation block: the day's humor, and — when
        /// <paramref name="withCycle"/> (a woman in her childbearing years, not with child) — the
        /// body's season after it; a woman WITH child (<paramref name="withChild"/>) gets her own
        /// carrying-season instead, never both. Empty id yields an honest nothing rather than a
        /// shared weather.
        /// </summary>
        public static string BuildNarration(string npcId, int campaignDay, bool withCycle, bool withChild = false)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return string.Empty;

            CyclePhase? phase = null;
            if (withCycle && !withChild) phase = PhaseOf(npcId, campaignDay, out _);

            string humor;
            if (withChild)
            {
                uint roll = Hash(npcId + ":humor:" + campaignDay);
                humor = roll % 3u != 0u
                    ? Humors[WithChildHumors[(int)((roll / 3u) % (uint)WithChildHumors.Length)]]
                    : Humors[(int)((roll / 3u) % (uint)Humors.Length)];
            }
            else
            {
                humor = DailyHumor(npcId, campaignDay, phase);
            }

            var narration = "This day finds me " + humor + ".";
            if (phase.HasValue) narration += " " + CycleSentence(phase.Value);
            else if (withChild) narration += " " + WithChildSentence(npcId, campaignDay);
            return narration;
        }

        private static int[] ClusterOf(CyclePhase phase)
        {
            switch (phase)
            {
                case CyclePhase.Menses: return MensesHumors;
                case CyclePhase.Rising: return RisingHumors;
                case CyclePhase.Crest: return CrestHumors;
                default: return WaningHumors;
            }
        }

        // FNV-1a: stable across runtimes and sessions, unlike string.GetHashCode — determinism here
        // is the whole feature (a reload must not reroll anyone's day).
        private static uint Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s ?? string.Empty)
                {
                    h ^= c;
                    h *= 16777619;
                }
                return h;
            }
        }
    }
}
