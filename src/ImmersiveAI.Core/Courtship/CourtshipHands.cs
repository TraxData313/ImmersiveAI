using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// The troth's hand, tend_courtship, as a schema (moved here from Module Tools\TrothTool on
    /// 2026.11.11 so the handmaid's variant is built from the SAME pieces and both are unit-tested;
    /// TrothTool.Tend is this object, word for word). The handmaid reads her own pace line in place
    /// of the ordinary one, never beside it.
    /// </summary>
    public static class CourtshipHands
    {
        public const string TendCourtship = "tend_courtship";

        // TIGHT ON PURPOSE (2026.08.27 token audit) — every rail below is load-bearing and stays:
        // one step per talk, earned not tested, set down in the breath felt, lay only when openly
        // spoken and welcomed, the seal is theirs, WORDS DO NOT WED, apart is always hers and ends
        // an unvowed belonging whole. Only the ornament went.
        // THE FIRST RUNGS ARE RECORDS, NOT PROPOSALS (2026.08.28, Anton's playtest — Rhia at 178
        // warm turns with the road still at None): every word here was marriage-flavoured, so a
        // model playing someone shy read reaching for the tool at all as courting, too forward for
        // "liking" — and the road never STARTED, which also meant no rose line, no bond-line stage,
        // nothing for the player to see. The "no proposal… set down freely" sentence unsticks
        // exactly that first reach; everything from readiness on keeps its full weight.
        private const string TendHead =
            "My troth is mine to tend, and marriage is a road of steps: liking, love, readiness, the " +
            "betrothal, the wedding day. The first arrivals — liking, then love — are no proposal " +
            "and speak of no marriage: they are my heart's own record, and I set them down freely " +
            "the day they are true, without waiting to be sure of more. ";

        /// <summary>The ordinary pace: earned, one step in a talk, never for a warm word.</summary>
        private const string TendPace =
            "I move closer only when real " +
            "talks have truly earned the next depth — one step in a talk at most, never for one warm " +
            "word alone, never to test — " +
            "and I set each arrival down";

        private const string TendTail =
            " in the very breath I feel it, above all the day I find I " +
            "would give my hand were the word spoken. ONLY when marriage has been openly spoken and " +
            "welcomed between us do I lay our betrothal (or, betrothed already, our wedding day) " +
            "before them; what I lay settles nothing — the seal is wholly theirs, and if they let it " +
            "lie I do not press. No words of ours make a marriage: not vows, not a temple, not a " +
            "ceremony we tell each other about — until I lay the day by this hand and they seal it " +
            "by theirs, we are not wed, and I neither say we are nor let the talk drift as though we " +
            "were. I move apart when a wound or a true doubt sets me back — always mine to do; and " +
            "if I am already theirs without any vow, moving apart ends THAT, whole and at once. I " +
            "never speak of steps, stages, or rules: my heart simply knows where it stands.";

        /// <summary>The handmaid's pace, in place of <see cref="TendPace"/>.</summary>
        private const string HandmaidPace = Handmaids.HandmaidText.RoadStepsWay + " I set each arrival down";

        private static ToolParameter[] TendParameters() => new[]
        {
            new ToolParameter("move",
                "Which way my heart truly moves — one of these two words exactly: 'closer' — " +
                "one step deeper (or, at the road's end, laying the betrothal or the wedding " +
                "day) — or 'apart', one step back.",
                allowedValues: new[] { "closer", "apart" }),
            new ToolParameter("word",
                "One short sentence, in my own voice, of what moved me — kept with the moment.",
                required: false),
        };

        public static readonly ToolDefinition Tend =
            new ToolDefinition(TendCourtship, TendHead + TendPace + TendTail, TendParameters());

        public static readonly ToolDefinition TendForHandmaid =
            new ToolDefinition(TendCourtship, TendHead + HandmaidPace + TendTail, TendParameters());
    }
}
