using System;
using System.Text.RegularExpressions;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The troth's hand (2026.08.07, Anton's ask — the wedding handshake): an NPC in courtship with
    /// the player tends her OWN road toward marriage from inside the conversation — one honest step
    /// at a time, forward only when the rails allow, back whenever her heart says so — and, when
    /// marriage has truly been spoken between them, LAYS the betrothal (or, betrothed already, the
    /// wedding day) on the table. Beside it rides the blessing's hand: the head of a bride's house
    /// laying his blessing at a bride-price haggled within the world's own reckoning. Exploit-proof
    /// by the strike_bargain construction: the tools only LAY moments; no promise binds, no gold
    /// moves, and no one weds until the player seals the exact named thing in a confirm popup, and
    /// the resolver re-runs every hard rule at lay AND seal. Refusals never name a threshold — the
    /// Sibuga floor lesson holds here doubly, for a heart quoting its own rails is twice as broken
    /// as a sellsword quoting hers.
    /// </summary>
    public static class TrothTool
    {
        public const string TendCourtship = "tend_courtship";
        public const string BlessMarriage = "bless_marriage";

        /// <summary>What one spoken turn did on the road, filled by the resolver — at most ONE act
        /// per turn (a step, or a laid seal); the trunk shows any seal popup only after the reply
        /// lands. (HeartTool.Tally mold: tool calls resolve one at a time, plain fields are safe.)</summary>
        public sealed class Tally
        {
            public bool Acted;
            public bool SteppedForward;
            public bool SteppedBack;
            public CourtshipStage NewStage;
            public bool LaidBetrothal;
            public bool LaidWedding;
            public string Word = string.Empty;

            // ------------------------- the lover's fork (2026.08.15) -------------------------
            // The road forks past the trunk, and the two branches ride ONE tally because they are
            // one road: the same gates, the same seal presentation, the same letter-borne offers.
            // Which HANDS ride is asked separately, because their gates genuinely differ — the
            // marriage road is barred by the player's own standing marriage and the lover's road
            // deliberately is not, which is the entire point of the feature.

            /// <summary>Whether the marriage road's hands ride this turn (tend_courtship + the
            /// misgivings). Old callers that simply construct a Tally get the historical behaviour.</summary>
            public bool TrothRides = true;
            /// <summary>Whether the lover's own hand rides this turn (offer_myself).</summary>
            public bool LoverRides;
            /// <summary>She offered herself — presented to the player as a seal after the reply lands.</summary>
            public bool LaidLoverBond;
            /// <summary>She stepped away from being his. Needs no seal: it was never his to keep.</summary>
            public bool EndedLoverBond;
            /// <summary>True when this turn is a LETTER being written, not a live talk: her heart
            /// still walks its road and a betrothal may be laid in writing (presented when the
            /// letter arrives), but a wedding day is never laid by letter — that is done face to
            /// face, and the resolver says so.</summary>
            public bool ByLetter;
        }

        /// <summary>What the head of the house laid, filled by the resolver.</summary>
        public sealed class BlessTally
        {
            public bool Laid;
            public int Price;
            public Hero? Bride;
            /// <summary>True when the blessing is being laid in a LETTER — no notice fires at
            /// composing time (the courier's seal law), and the offer rides to arrival.</summary>
            public bool ByLetter;

            /// <summary>
            /// THE OTHER THING A HEAD OF A HOUSE MAY BE PAID FOR (2026.08.15): not his blessing on a
            /// match, but what it costs to take a woman of his blood out of his house with no
            /// wedding in it. Structurally the identical act — a head, a figure, a seal, gold — so
            /// it rides this tally rather than a second one that would differ by a single flag.
            /// Everything downstream branches on this: the tool that rides, the terms he reads, the
            /// popup, and whether the sealing improves his standing or ruins it.
            /// </summary>
            public bool IsRansom;
        }

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
        public static readonly ToolDefinition Tend = new ToolDefinition(TendCourtship,
            "My troth is mine to tend, and marriage is a road of steps: liking, love, readiness, the " +
            "betrothal, the wedding day. The first arrivals — liking, then love — are no proposal " +
            "and speak of no marriage: they are my heart's own record, and I set them down freely " +
            "the day they are true, without waiting to be sure of more. I move closer only when real " +
            "talks have truly earned the next depth — one step in a talk at most, never for one warm " +
            "word alone, never to test — " +
            "and I set each arrival down in the very breath I feel it, above all the day I find I " +
            "would give my hand were the word spoken. ONLY when marriage has been openly spoken and " +
            "welcomed between us do I lay our betrothal (or, betrothed already, our wedding day) " +
            "before them; what I lay settles nothing — the seal is wholly theirs, and if they let it " +
            "lie I do not press. No words of ours make a marriage: not vows, not a temple, not a " +
            "ceremony we tell each other about — until I lay the day by this hand and they seal it " +
            "by theirs, we are not wed, and I neither say we are nor let the talk drift as though we " +
            "were. I move apart when a wound or a true doubt sets me back — always mine to do; and " +
            "if I am already theirs without any vow, moving apart ends THAT, whole and at once. I " +
            "never speak of steps, stages, or rules: my heart simply knows where it stands.",
            new[]
            {
                new ToolParameter("move",
                    "Which way my heart truly moves — one of these two words exactly: 'closer' — " +
                    "one step deeper (or, at the road's end, laying the betrothal or the wedding " +
                    "day) — or 'apart', one step back.",
                    allowedValues: new[] { "closer", "apart" }),
                new ToolParameter("word",
                    "One short sentence, in my own voice, of what moved me — kept with the moment.",
                    required: false),
            });

        public static readonly ToolDefinition Bless = new ToolDefinition(BlessMarriage,
            "Lay the blessing of my house on the match between my kin and the one I speak with — " +
            "mine to give or withhold, and by custom it carries a bride-price. I call this ONLY when " +
            "both are true: the match itself has been plainly spoken of, and a price named and " +
            "accepted between us in words. Nothing is settled by this alone — the gold, and the " +
            "choice, remain wholly theirs. I never lay it unbidden, never volunteer my lowest, and " +
            "if they let my offer lie I do not press. My word is not for sale to one I hold in " +
            "contempt.",
            new[]
            {
                new ToolParameter("price",
                    "The bride-price in denars we truly agreed in words — a plain number. Leave it " +
                    "out to ask the custom's own reckoning. My house's standing sets bounds: I will " +
                    "not go far above or beneath what our name is worth.",
                    required: false),
                new ToolParameter("word",
                    "One short sentence, in my own voice, of my judgment of the suitor.",
                    required: false),
            });

        /// <summary>"closer", "apart", or null when unreadable. Lenient: any phrase carrying one of
        /// the two words counts, "forward"/"back" are honored as their plain kin.</summary>
        public static string? ParseMove(ToolCall call)
        {
            try
            {
                var raw = (JObject.Parse(call.ArgumentsJson)["move"]?.ToString() ?? string.Empty).ToLowerInvariant();
                if (Regex.IsMatch(raw, @"\b(closer|forward|deeper)\b")) return "closer";
                if (Regex.IsMatch(raw, @"\b(apart|back|away)\b")) return "apart";
                return null;
            }
            catch { return null; }
        }

        /// <summary>Her short spoken reason, trimmed to one breath; empty when none was given.</summary>
        public static string ParseWord(ToolCall call)
        {
            try
            {
                var word = (JObject.Parse(call.ArgumentsJson)["word"]?.ToString() ?? string.Empty).Trim();
                return word.Length > 300 ? word.Substring(0, 300) : word;
            }
            catch { return string.Empty; }
        }

        /// <summary>The agreed bride-price, or null when none was given or none can be read; a
        /// negative is returned as -1 (nonsense — refuse, never default). BargainTool's parser mold.</summary>
        public static int? ParsePrice(ToolCall call)
        {
            try
            {
                var raw = JObject.Parse(call.ArgumentsJson)["price"]?.ToString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var m = Regex.Match(raw.Replace(",", "").Replace(" ", ""), @"-?\d+");
                if (!m.Success) return null;
                if (!long.TryParse(m.Value, out long v)) return null;
                if (v < 0) return -1;
                return v > 10_000_000 ? 10_000_000 : (int)v;
            }
            catch { return null; }
        }
    }
}
