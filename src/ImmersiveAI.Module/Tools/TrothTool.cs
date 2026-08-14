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
        }

        public static readonly ToolDefinition Tend = new ToolDefinition(TendCourtship,
            "Tend the road of my own heart toward the one I speak with — my troth is mine to tend, " +
            "and marriage is a road walked in steps: liking, then love, then readiness, then the " +
            "betrothal, then the wedding day. I move closer only when real talks between us have " +
            "truly earned the next depth — one step in a talk at the most, never for one warm word " +
            "alone, never to test or to play — and I set each arrival down in the very breath I " +
            "feel it, not some later day; above all the day I find that were the word spoken " +
            "between us I would give my hand, for a heart that has moved and never owns it has not " +
            "truly moved, and I would not have them ask me blind. ONLY when marriage has been " +
            "openly spoken and welcomed between us in our own words do I lay our betrothal (or, " +
            "betrothed already, our wedding day) before them; what I lay settles nothing — the " +
            "seal is wholly theirs, and if they let it lie I do not press. I move apart when a " +
            "wound or a true doubt sets me back — that is always mine to do. I never speak of " +
            "steps, stages, or rules: my heart simply knows where it stands, and speaks as a " +
            "heart does.",
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
            "Lay the blessing of my house on the match between the one of my kin who is promised to " +
            "the one I speak with — that blessing is mine to give or withhold, and by the custom of " +
            "the world it carries a bride-price. I call this ONLY when both are true: we have " +
            "plainly spoken of the match itself, and a price has been named and accepted between " +
            "us in words. Nothing is settled by this alone — the gold, and the choice, remain " +
            "wholly theirs. I never lay it unbidden and never volunteer my lowest; if they let my " +
            "offer lie I do not press, nor lay it again unless they themselves return to it. And " +
            "my word is not for sale to one I hold in contempt.",
            new[]
            {
                new ToolParameter("price",
                    "The bride-price in denars we truly agreed in words — a plain number. Leave it " +
                    "out to ask the custom's own reckoning. The honor and standing of my house set " +
                    "bounds: I will not go far above or beneath what our name is worth, however " +
                    "sweetly asked.",
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
