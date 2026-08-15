using System;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// What she is to him outside the world's ceremonies. Persisted per NPC in memories.json beside
    /// <see cref="CourtshipStage"/> (so the save-scoped snapshots rewind it for free); the values are
    /// load-bearing, and an old file deserializes the missing field as 0 = None.
    /// </summary>
    public enum LoverBond
    {
        None = 0,
        /// <summary>His, and no vow anywhere says so.</summary>
        Lover = 1,
        /// <summary>She was, and is not. A remembered state and never an erasure — what happened
        /// happened, the gold that bought her out of her house is not returned, and both of them
        /// carry it.</summary>
        Former = 2,
    }

    /// <summary>
    /// THE LOVER'S ROAD (2026.08.15) — the courtship mold applied to the second bond, and a FORK of
    /// that road rather than a second one beside it.
    ///
    /// The trunk is shared and unchanged: None → Warmth → Devotion, walked by her own
    /// <c>tend_courtship</c> hand under <see cref="CourtshipRoad"/>'s rails. From Devotion the road
    /// divides. One branch is the marriage rails — Ready, the betrothal, the wedding day — with the
    /// station gate, the misgivings and the kin's blessing on it. The other is this: no ceremony, no
    /// house, no blessing, nothing promised, and a great deal more feeling asked for.
    ///
    /// WHY THERE IS NO STATION GATE HERE, which is the one rail a reader will look for and not find.
    /// The gate exists because a marriage is an alliance between two houses, and houses do not ally
    /// downward. A lover is not an alliance; a lover is a scandal. So the emperor's daughter may be
    /// his lover where she could never be his wife — and what she costs him instead is her house's
    /// price for losing her and her father's standing word about it. The gate did not vanish; it
    /// turned into money and anger, which is what it would truly have turned into.
    ///
    /// WHAT IT ASKS INSTEAD is <see cref="HeartBands.LoverAsks"/> — a love gone deep, above anything
    /// the whole marriage road ever asks. Deliberate and era-true: a wife has vows, a settlement, her
    /// children's name and the world's approval holding her in place. A lover has none of it. Only
    /// the feeling is holding her there, so the feeling has to be worth all of that.
    ///
    /// Verdicts are reason CODES. The words she reads for them live in <see cref="LoverText"/> and
    /// never quote a threshold, a stage, or a number.
    /// </summary>
    public static class LoverRoad
    {
        /// <summary>The stage of the shared trunk from which the fork may be taken: her heart truly
        /// given, by her own hand, on some earlier day.</summary>
        public const CourtshipStage ForksFrom = CourtshipStage.Devotion;

        public enum LoverVerdict
        {
            Allowed,
            /// <summary>She is already his.</summary>
            AlreadyHis,
            /// <summary>She is his WIFE. There is no lesser thing to offer a wife.</summary>
            AlreadyWed,
            /// <summary>A promise stands between them; that road ends at a wedding, not here.</summary>
            PromiseStands,
            /// <summary>Her heart has not walked far enough down the shared road to fork at all.</summary>
            RoadNotWalked,
            /// <summary>Her regard has not reached what this asks — and it asks a great deal.</summary>
            HeartNotDeepEnough,
            /// <summary>She is bound to someone in the world's eyes.</summary>
            SheIsBound,
            /// <summary>The world would not have the two of them at all (age, station, kind, custom).</summary>
            WorldRefuses,
        }

        /// <summary>The plain facts the judgment needs — gathered by the game layer, judged here.</summary>
        public sealed class LoverFacts
        {
            /// <summary>How far down the shared trunk her own hand has walked.</summary>
            public CourtshipStage Stage;
            /// <summary>What she already is to him.</summary>
            public LoverBond Bond;
            /// <summary>Her live regard for the player (−100..100).</summary>
            public int Relation;
            /// <summary>True when she is wed to the player — a wife, not a lover.</summary>
            public bool WedToPlayer;
            /// <summary>True when she is bound to anyone at all in the world's eyes. The scope of the
            /// whole feature is unattached women (Anton, 2026.08.15): no adultery toward other men.</summary>
            public bool BoundElsewhere;
            /// <summary>True when the world itself refuses the pair — the age, kind and custom legs
            /// of the marriage law, which hold here too. Gathered by the game layer, since only it
            /// can ask the game.</summary>
            public bool WorldRefusesThePair;
        }

        /// <summary>
        /// Whether her bond may be LAID before him now. Laying settles nothing — the seal is his
        /// alone, and the game layer re-runs this whole judgment at the seal (the strike_bargain
        /// discipline: a popup can sit open while the world moves).
        ///
        /// The player's own marriage is deliberately NOT a bar. That is the entire point of the
        /// feature and the one place where it parts company with the marriage rails.
        /// </summary>
        public static LoverVerdict JudgeTake(LoverFacts f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));

            if (f.WedToPlayer) return LoverVerdict.AlreadyWed;
            if (f.Bond == LoverBond.Lover) return LoverVerdict.AlreadyHis;
            if (f.BoundElsewhere) return LoverVerdict.SheIsBound;
            if (f.WorldRefusesThePair) return LoverVerdict.WorldRefuses;
            // A standing promise, or a marriage already sealed, is a road with its own end.
            if (f.Stage >= CourtshipStage.Betrothed) return LoverVerdict.PromiseStands;
            if (f.Stage < ForksFrom) return LoverVerdict.RoadNotWalked;
            if (HeartBands.Of(f.Relation, f.Stage) < HeartBands.LoverAsks) return LoverVerdict.HeartNotDeepEnough;
            return LoverVerdict.Allowed;
        }

        /// <summary>Whether the bond can be let go of — hers to do at any time, and his. The only
        /// thing that cannot be undone is the fact of it, which is why <see cref="LoverBond.Former"/>
        /// exists rather than a return to None.</summary>
        public static bool CanPart(LoverBond bond) => bond == LoverBond.Lover;

        /// <summary>
        /// Whether SHE would be the one to end it, unbidden — which she does when the feeling that
        /// was the whole of the bond has thinned past fondness. Nothing here ends it; this is the
        /// question the game layer asks before giving her the chance to answer it herself.
        /// </summary>
        public static bool HeartHasLeft(int relation, LoverBond bond) =>
            bond == LoverBond.Lover && HeartBands.OfRelation(relation) < HeartBand.Like;

        // ------------------------- what her house is paid -------------------------

        /// <summary>
        /// WHAT HER LEAVING COSTS (2026.08.15). A lover in another house stays in it — she is
        /// someone he visits — until her house is paid for losing her, and only then can she ride
        /// with him. The bond is free and private; the LEAVING is public and expensive, which is
        /// the right way round.
        ///
        /// The anchor is Anton's own: the worth of the gear she stands up in ("the eq she has sell
        /// price, not cheap"). It is honest, readable, and already in the world — nothing invented,
        /// and a player can look at her and roughly know. Beneath it sits a floor by her station,
        /// because a noblewoman's worth to her house was never her clothes: strip a lord's daughter
        /// of her armour and she does not become cheap to lose, she becomes cheap to LOOK at. The
        /// floor is what stops that being an exploit.
        /// </summary>
        public const int RansomFloorPerStationRung = 2500;

        public static int RansomReckoning(int equipmentWorth, int stationTier)
        {
            int worth = Math.Max(0, equipmentWorth);
            int floor = Math.Max(0, stationTier) * RansomFloorPerStationRung;
            return Math.Max(worth, floor);
        }

        /// <summary>The bounds he may argue between — the bargain's own rail, shared with the
        /// hiring handshake and the bride-price so all three haggle the same way. 0 percent means
        /// the figure does not move, which is a legitimate way to play.</summary>
        public static void RansomRails(int reckoning, int hagglePercent, out int min, out int max)
        {
            double rail = Math.Max(0, Math.Min(90, hagglePercent)) / 100.0;
            min = (int)Math.Ceiling(reckoning * (1 - rail));
            max = (int)Math.Floor(reckoning * (1 + rail));
            if (min < 0) min = 0;
            if (max < min) max = min;
        }

        /// <summary>The bond in one plain word — notes, notices and the developer's eyes.</summary>
        public static string BondName(LoverBond bond)
        {
            switch (bond)
            {
                case LoverBond.Lover: return "lover";
                case LoverBond.Former: return "once a lover";
                default: return "nothing of the kind";
            }
        }
    }
}
