using System;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// Where one soul's heart stands on the road toward marrying the player. Persisted per NPC
    /// inside memories.json (see <see cref="Memory.NpcMemory.CourtshipStage"/>), so the save-scoped
    /// memory snapshots rewind a courtship with the save like any other memory. The values are
    /// load-bearing: old files deserialize the missing field as 0 = None.
    /// </summary>
    public enum CourtshipStage
    {
        None = 0,
        /// <summary>"I like them; their company draws me."</summary>
        Warmth = 1,
        /// <summary>"My heart is truly given; I love them."</summary>
        Devotion = 2,
        /// <summary>"No questions remain; were marriage asked between us, I would say yes."</summary>
        Ready = 3,
        /// <summary>"I have promised myself to them" — the first seal, the player's own click.</summary>
        Betrothed = 4,
        /// <summary>The second seal — the real game marriage; from here the road has no back-step.</summary>
        Wed = 5,
    }

    /// <summary>
    /// The rails of the courtship road — pure judgment over plain facts, so every verdict is
    /// unit-testable. The heart is free; the HAND has rails: Warmth and Devotion are feelings gated
    /// only by her real regard (a princess may come to love a commoner), while the step to Ready and
    /// both seals also run the station gate — required player clan tier from HER station, minus the
    /// charm slack her fully-won heart has earned (the Don-Juan discount). Verdicts are reason
    /// codes; the words the NPC reads for them live in <see cref="CourtshipText"/> and deliberately
    /// never quote a number or a tier (the Sibuga floor lesson: any rail an NPC reads becomes her
    /// next sentence).
    /// </summary>
    public static class CourtshipRoad
    {
        /// <summary>Her regard for the player must at least not be scorn before liking can root.</summary>
        public const int WarmthRelationFloor = 0;
        /// <summary>True devotion asks a real bond, built over real talks.</summary>
        public const int DevotionRelationFloor = 20;
        /// <summary>Readiness to wed asks a deep one.</summary>
        public const int ReadyRelationFloor = 40;
        /// <summary>Forward steps come at most one per game day — no one sprints the whole road in
        /// a single warm evening, however sweet the words.</summary>
        public const double DaysBetweenForwardSteps = 1.0;

        /// <summary>Why a forward step (or a laid seal) is or is not allowed. Codes, not words —
        /// the phrasing the NPC reads lives in <see cref="CourtshipText.ForwardRefusal"/>.</summary>
        public enum StepVerdict
        {
            Allowed,
            /// <summary>Already wed — the road has no further step.</summary>
            NoRoadFurther,
            /// <summary>The last forward step was taken less than a day ago.</summary>
            TooSoon,
            /// <summary>Her own regard has not yet reached the step's depth.</summary>
            HeartNotThere,
            /// <summary>The suitor's house stands too far beneath her station.</summary>
            StationTooFar,
            /// <summary>She has never sat with the question of her own misgivings — a heart that
            /// never looked cannot say no questions remain.</summary>
            MisgivingsUnweighed,
            /// <summary>A misgiving she set down by her own hand still stands unanswered.</summary>
            MisgivingsRemain,
            /// <summary>The betrothal is younger than the world asks a troth to season.</summary>
            TrothTooFresh,
        }

        /// <summary>The plain facts a forward judgment needs — gathered by the game layer, judged here.</summary>
        public sealed class StepFacts
        {
            public CourtshipStage Stage;
            /// <summary>Her live regard for the player (−100..100).</summary>
            public int Relation;
            /// <summary>Days since her last FORWARD step; PositiveInfinity when she never stepped.</summary>
            public double DaysSinceForwardStep = double.PositiveInfinity;
            public int PlayerClanTier;
            /// <summary>Her station rendered as a required-tier rung (see <see cref="StationTier"/>).</summary>
            public int HerStationTier = 1;
            /// <summary>The configured charm slack (0..4) — applied only where the gate binds.</summary>
            public int CharmSlack;
            /// <summary>True once she has sat with her own misgivings at least once — even to find
            /// none. Defaults permissive so a caller who does not carry the question is not judged
            /// by it (the game layer always sets it from her memory).</summary>
            public bool MisgivingsWeighed = true;
            /// <summary>How many misgivings she set down still stand unsettled by her own hand.</summary>
            public int OpenMisgivings;
            /// <summary>Days since the betrothal was sealed; negative when not betrothed.</summary>
            public double DaysBetrothed = -1;
            public int MinBetrothalDays;
        }

        /// <summary>
        /// Judges the next forward step from <paramref name="f"/>.Stage. Steps to Warmth and
        /// Devotion are hers alone (regard + the one-step-a-day rail); the step to Ready adds the
        /// station gate and her own misgivings (weighed at least once, none left standing); laying
        /// the BETROTHAL (from Ready) re-runs Ready's gates with no day rail (the proposal's moment
        /// belongs to the two of them); laying the WEDDING (from Betrothed) asks the troth to have
        /// seasoned and re-runs the station gate — her misgivings are deliberately NOT re-checked
        /// there: the promise was proven when it was given.
        /// </summary>
        public static StepVerdict JudgeForward(StepFacts f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            if (f.Stage >= CourtshipStage.Wed) return StepVerdict.NoRoadFurther;

            var target = f.Stage + 1;
            bool gate(int slack) => f.PlayerClanTier >= RequiredTier(f.HerStationTier, slack);

            switch (target)
            {
                case CourtshipStage.Warmth:
                    return f.Relation >= WarmthRelationFloor ? StepVerdict.Allowed : StepVerdict.HeartNotThere;

                case CourtshipStage.Devotion:
                    if (f.DaysSinceForwardStep < DaysBetweenForwardSteps) return StepVerdict.TooSoon;
                    return f.Relation >= DevotionRelationFloor ? StepVerdict.Allowed : StepVerdict.HeartNotThere;

                case CourtshipStage.Ready:
                    if (f.DaysSinceForwardStep < DaysBetweenForwardSteps) return StepVerdict.TooSoon;
                    if (f.Relation < ReadyRelationFloor) return StepVerdict.HeartNotThere;
                    if (!gate(f.CharmSlack)) return StepVerdict.StationTooFar;
                    if (!f.MisgivingsWeighed) return StepVerdict.MisgivingsUnweighed;
                    if (f.OpenMisgivings > 0) return StepVerdict.MisgivingsRemain;
                    return StepVerdict.Allowed;

                case CourtshipStage.Betrothed:
                    if (f.Relation < ReadyRelationFloor) return StepVerdict.HeartNotThere;
                    if (!gate(f.CharmSlack)) return StepVerdict.StationTooFar;
                    if (!f.MisgivingsWeighed) return StepVerdict.MisgivingsUnweighed;
                    if (f.OpenMisgivings > 0) return StepVerdict.MisgivingsRemain;
                    return StepVerdict.Allowed;

                case CourtshipStage.Wed:
                    if (f.DaysBetrothed >= 0 && f.DaysBetrothed < f.MinBetrothalDays) return StepVerdict.TrothTooFresh;
                    if (!gate(f.CharmSlack)) return StepVerdict.StationTooFar;
                    return StepVerdict.Allowed;

                default:
                    return StepVerdict.NoRoadFurther;
            }
        }

        /// <summary>
        /// Her station rendered as the clan-tier rung a suitor's house must reach: a clanless soul
        /// (wanderer, companion) 1; a noble house its own tier, walled to 2..5; the kin of a RULING
        /// house 6 — the emperor's daughter, exactly as asked. The charm slack is subtracted where
        /// the gate binds (Ready and the seals), never before — the heart may wander freely below.
        /// </summary>
        public static int StationTier(bool clanless, bool rulingClan, int clanTier)
        {
            if (clanless) return 1;
            if (rulingClan) return 6;
            return Math.Max(2, Math.Min(5, clanTier));
        }

        /// <summary>The player tier the gate asks for: her station minus the earned slack, floored at 0.</summary>
        public static int RequiredTier(int herStationTier, int charmSlack) =>
            Math.Max(0, herStationTier - Math.Max(0, charmSlack));

        /// <summary>The stage in her own plain word — for notes, notices, and debug views.</summary>
        public static string StageName(CourtshipStage stage)
        {
            switch (stage)
            {
                case CourtshipStage.Warmth: return "warmth";
                case CourtshipStage.Devotion: return "devotion";
                case CourtshipStage.Ready: return "ready";
                case CourtshipStage.Betrothed: return "betrothed";
                case CourtshipStage.Wed: return "wed";
                default: return "nothing yet";
            }
        }
    }
}
