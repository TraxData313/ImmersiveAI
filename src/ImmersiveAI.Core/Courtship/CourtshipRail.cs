using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// THE ROAD, DRAWN AS A PATH (2026.08.31, Anton's design: "maybe at the top have the stages —
    /// considering, has misgivings to resolve, betrothed, married — indicating where we are in this
    /// path and what the path is at all").
    ///
    /// <para>Every rail of this road was carefully built and carefully explained TO THE NPC, in her
    /// own first person, and never once to the player: he saw a single word ("warmth") inside a grey
    /// debug string, with nothing before it and nothing after. A road nobody can see the shape of is
    /// not a road, it is a mood that occasionally changes. So the same stages the rails judge are
    /// rendered here as an ordered path with a lit position — the mod's own map of itself.</para>
    ///
    /// <para>THE NAMES ARE THE PLAYER'S, NOT THE ENUM'S. <see cref="CourtshipRoad.StageName"/>
    /// answers "warmth"/"devotion"/"ready", which are our words for her inner state; a player
    /// reading a path wants the words he would use himself, which is why Betrothed and Married are
    /// spelled as Anton spelled them when he asked for this.</para>
    ///
    /// <para>THE TWO CONDITIONAL RUNGS between the promise and the day — her kin's word, and the
    /// days a troth is asked to season — appear ONLY when they truly apply to this soul. A wanderer
    /// has no house to ask and a player with MinBetrothalDays at 0 waits for nothing, and drawing
    /// either as a greyed future step would invent an obstacle that does not exist. (Anton believed
    /// he had days to wait when he had none: an invisible road is filled in by guesswork, and the
    /// guesswork is always worse than the truth.)</para>
    /// </summary>
    public static class CourtshipRail
    {
        /// <summary>One rung of the road as the player sees it.</summary>
        public sealed class Node
        {
            public string Name = string.Empty;
            /// <summary>Passed already.</summary>
            public bool Done;
            /// <summary>Where the two of them stand right now — exactly one node carries this.</summary>
            public bool Current;
            /// <summary>A rung of the world rather than of her heart (her kin's word, the seasoning
            /// days). Drawn only when it applies, and worth marking so a view may soften it.</summary>
            public bool OfTheWorld;
        }

        public const string Warmth = "Warmth";
        public const string Love = "Love";
        public const string Ready = "Ready";
        public const string KinsWord = "Kin's word";
        public const string TheDays = "The days";
        public const string Betrothed = "Betrothed";
        public const string Married = "Married";

        /// <summary>
        /// The whole path with one rung lit. <paramref name="kinsWordRung"/> and
        /// <paramref name="seasonRung"/> say whether those two rungs exist for THIS pair at all;
        /// their Given/Done flags say whether they are behind us.
        /// </summary>
        public static IReadOnlyList<Node> Build(
            CourtshipStage stage,
            bool kinsWordRung = false, bool kinsWordGiven = false,
            bool seasonRung = false, bool seasonDone = false)
        {
            // The two rungs of the world stand BETWEEN the promise and the day. Where one of them
            // is what actually stands in the way, IT takes the light and the promise behind it does
            // not — otherwise the path would say "you are here: betrothed" while the thing to be
            // done is entirely elsewhere, which is the confusion this whole rail exists to end.
            bool atBetrothed = stage == CourtshipStage.Betrothed;
            bool kinCurrent = atBetrothed && kinsWordRung && !kinsWordGiven;
            bool seasonCurrent = atBetrothed && seasonRung && !seasonDone && !kinCurrent;

            var nodes = new List<Node>
            {
                new Node { Name = Warmth, Done = stage > CourtshipStage.Warmth, Current = stage == CourtshipStage.Warmth },
                new Node { Name = Love, Done = stage > CourtshipStage.Devotion, Current = stage == CourtshipStage.Devotion },
                new Node { Name = Ready, Done = stage > CourtshipStage.Ready, Current = stage == CourtshipStage.Ready },
                new Node { Name = Betrothed, Done = stage > CourtshipStage.Betrothed,
                           Current = atBetrothed && !kinCurrent && !seasonCurrent },
            };

            if (kinsWordRung)
                nodes.Add(new Node
                {
                    Name = KinsWord,
                    Done = stage > CourtshipStage.Betrothed || kinsWordGiven,
                    Current = kinCurrent,
                    OfTheWorld = true,
                });
            if (seasonRung)
                nodes.Add(new Node
                {
                    Name = TheDays,
                    Done = stage > CourtshipStage.Betrothed || seasonDone,
                    Current = seasonCurrent,
                    OfTheWorld = true,
                });
            nodes.Add(new Node { Name = Married, Done = false, Current = stage >= CourtshipStage.Wed });

            // Exactly one lit rung, always — the belt under every shape above, including a stage of
            // None, where the road has not begun and the first rung is what is being reached for.
            if (!nodes.Any(n => n.Current)) nodes[0].Current = true;
            // Nothing is behind you before the road has begun.
            if (stage <= CourtshipStage.None) foreach (var n in nodes) n.Done = false;
            return nodes;
        }

        /// <summary>The path on one line, the lit rung in brackets — for the page, the hover text
        /// and the tests. The view draws its own coloured chips from <see cref="Build"/>.</summary>
        public static string OneLine(IReadOnlyList<Node>? nodes)
        {
            if (nodes == null || nodes.Count == 0) return string.Empty;
            return string.Join("  ›  ", nodes.Select(n =>
                n == null ? string.Empty : n.Current ? "[ " + n.Name + " ]" : n.Name));
        }
    }
}
