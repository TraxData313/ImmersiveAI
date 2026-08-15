using System;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>Where a heart stands, in the only currency a heart has: a name.</summary>
    public enum HeartBand
    {
        /// <summary>Nothing in particular — or worse.</summary>
        Neutral = 0,
        /// <summary>Drawn to look twice.</summary>
        Intrigued = 1,
        /// <summary>Fondness — real, and not yet love.</summary>
        Like = 2,
        Love = 3,
        /// <summary>A love gone deep, which is a different thing from love and asks more.</summary>
        DeepLove = 4,
    }

    /// <summary>What stands between two souls in the world's eyes, which is not the same as what
    /// stands between them in fact.</summary>
    public enum BondKind
    {
        /// <summary>Nothing sealed — a road being walked, or none.</summary>
        None = 0,
        /// <summary>His, without vows. Nothing holds her but feeling.</summary>
        Lover = 1,
        /// <summary>Wed. The vow holds a while even after the feeling has thinned.</summary>
        Wed = 2,
    }

    /// <summary>
    /// THE NAMED BANDS OF THE HEART (2026.08.15, Anton's ladder: neutral → intrigued → like → love →
    /// deepLove). The one rule that makes them worth having: <b>there is no third number</b>. A band
    /// is a pure function of what already exists — her live regard, and the stage of the road she has
    /// walked by her own hand — computed where it is needed and stored nowhere. A stored band would
    /// drift from the truth within a day, and then two systems would disagree about the same heart.
    ///
    /// SHE KNOWS THE LADDER BY NAME AND NEVER BY NUMBER. Every rail an NPC can read becomes her next
    /// sentence (the Sibuga floor lesson), and a heart quoting its own thresholds is twice as broken
    /// as a sellsword quoting hers. So nothing here renders a figure, and the words that reach her
    /// live in <see cref="LoverText"/>.
    ///
    /// THE ASYMMETRY IS THE DESIGN, and it is era-true rather than arithmetic: a WIFE's bed opens at
    /// love and closes only when the heart has fallen below even curiosity — the vow carries her some
    /// way past the feeling. A LOVER's opens only at a love gone deep and closes as soon as it thins
    /// below fondness, because nothing whatever holds her there but the feeling itself. A lover asks
    /// MORE than a wife and keeps it more easily lost. That is the whole shape of the thing.
    /// </summary>
    public static class HeartBands
    {
        // The rungs, set against the courtship road's own gates so the two ladders agree: Warmth
        // asks 0, Devotion 20, Ready 40. Deep love stands ABOVE everything the marriage road ever
        // asks for — which is exactly why the lover's bond can ask for it.
        public const int IntriguedFloor = CourtshipRoad.WarmthRelationFloor;   // 0
        public const int LikeFloor = CourtshipRoad.DevotionRelationFloor;      // 20
        public const int LoveFloor = CourtshipRoad.ReadyRelationFloor;         // 40
        public const int DeepLoveFloor = 70;

        /// <summary>
        /// The band from her live regard alone — the honest reading, and the one everything else is
        /// built on.
        /// </summary>
        public static HeartBand OfRelation(int relation)
        {
            if (relation >= DeepLoveFloor) return HeartBand.DeepLove;
            if (relation >= LoveFloor) return HeartBand.Love;
            if (relation >= LikeFloor) return HeartBand.Like;
            if (relation >= IntriguedFloor) return HeartBand.Intrigued;
            return HeartBand.Neutral;
        }

        /// <summary>
        /// The band as it truly stands, with one small mercy: while she is still WALKING the road,
        /// a stage she declared by her own hand holds her band up to what that declaration meant,
        /// even if the number has since dipped. She said the words; they are worth one rung of
        /// cushion against a bad week.
        ///
        /// PAST THE SEAL THERE IS NO CUSHION — betrothal, marriage, the lover's bond: from there
        /// what you see is what is there. That is not an oversight, it is the fall's whole
        /// mechanism. A wife's declared devotion must never go on reporting love long after the
        /// love has gone, or the closed door would arrive out of a clear sky and the player would
        /// have been told a comfortable lie right up to it.
        /// </summary>
        public static HeartBand Of(int relation, CourtshipStage stage)
        {
            var band = OfRelation(relation);
            if (stage >= CourtshipStage.Betrothed) return band;

            var declared = HeartBand.Neutral;
            switch (stage)
            {
                case CourtshipStage.Warmth: declared = HeartBand.Intrigued; break;
                case CourtshipStage.Devotion: declared = HeartBand.Like; break;
                case CourtshipStage.Ready: declared = HeartBand.Love; break;
            }
            return band >= declared ? band : declared;
        }

        /// <summary>What a lover's bond asks before it can be laid at all: a love gone deep.</summary>
        public const HeartBand LoverAsks = HeartBand.DeepLove;

        /// <summary>Whether this heart, at this band, opens to him tonight. The bond decides the
        /// rung, and the two rungs are deliberately far apart — see the class remarks.</summary>
        public static bool OpensTo(HeartBand band, BondKind bond)
        {
            switch (bond)
            {
                // The vow carries her past the feeling for a long while; only real coldness closes it.
                case BondKind.Wed: return band >= HeartBand.Intrigued;
                // Nothing holds her but the feeling, so the feeling is the whole of it.
                case BondKind.Lover: return band >= HeartBand.Like;
                // No bond, no bed. The bond is the door, and it is laid and sealed elsewhere.
                default: return false;
            }
        }

        /// <summary>The band in one plain word — for the player's own views, her sheet's standing
        /// line, and the developer's eyes. Never a number, anywhere, ever.</summary>
        public static string Word(HeartBand band)
        {
            switch (band)
            {
                case HeartBand.Intrigued: return "curiosity";
                case HeartBand.Like: return "fondness";
                case HeartBand.Love: return "love";
                case HeartBand.DeepLove: return "a love gone deep";
                default: return "nothing in particular";
            }
        }
    }
}
