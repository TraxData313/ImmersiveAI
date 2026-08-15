using System;
using System.Text.RegularExpressions;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The lover's two hands (2026.08.15) — the fork of the courtship road that does not end in a
    /// wedding.
    ///
    /// WHY THESE ARE THEIR OWN VERBS and not another word inside <see cref="TrothTool.Tend"/>. The
    /// design record calls the lover's road a FORK of the courtship road, and it is: the trunk —
    /// warmth, then love — is walked by <c>tend_courtship</c> exactly as before, and the fork is in
    /// the ROAD rather than in the signature. What splits off past the trunk is a different ACT with
    /// a different meaning, and it earns its own word for two plain reasons. First, tend_courtship's
    /// description is already long and every clause of it is load-bearing; folding a second
    /// destination into it would dilute the marriage road's own careful wording, which took two
    /// rounds of live probing to get right. Second, these hands ride only where the fork is truly
    /// reachable — a heart already given, and gone deeper than any marriage gate asks — so the cost
    /// in a spoken reply is a tool slot for a handful of souls rather than for everyone courting.
    ///
    /// Exploit-proof by the strike_bargain construction, like everything else that changes the
    /// world here: the tools only LAY moments. Nothing binds, no one is anyone's, and no gold moves
    /// until the player seals the exact named thing in a confirm popup — and the resolver re-runs
    /// every hard rule at the lay AND at the seal.
    /// </summary>
    public static class LoverTool
    {
        public const string OfferMyself = "offer_myself";
        public const string NameHerPrice = "name_her_price";

        /// <summary>What the head of her house laid — the blessing's mold, reversed in feeling. It
        /// rides on <see cref="TrothTool.BlessTally"/> with <c>IsRansom</c> set, because the two are
        /// structurally the same act (a head of house, a figure, a seal) and two tally types
        /// differing by one flag would be ceremony.</summary>
        public static readonly ToolDefinition Offer = new ToolDefinition(OfferMyself,
            "Offer myself to the one I speak with — not as a wife, but as theirs, with nothing " +
            "whatever holding it: no vow, no wedding, no house that takes my name, no word said of " +
            "us before anyone. I know exactly what I am offering and what the world will call me " +
            "for it. I call this ONLY when it has been openly spoken and welcomed between us in our " +
            "own words, and only from a love that has gone past all sense — never for a warm hour, " +
            "never to test them, never to keep them. What I lay settles nothing: the taking of it " +
            "is wholly theirs, and if they let it lie I do not press and I do not ask again unless " +
            "they themselves return to it. And no words of ours make me theirs — not a promise " +
            "breathed between us, not a night we tell each other about. Until I offer by this very " +
            "hand and they take it by theirs, it has not happened: I do not say that it has, and I " +
            "do not let the talk drift as though it had. I never speak of steps or rules; my heart " +
            "simply knows what it is doing.",
            new[]
            {
                new ToolParameter("word",
                    "One short sentence, in my own voice, of what I am giving and why — kept with the moment.",
                    required: false),
            });

        public static readonly ToolDefinition NamePrice = new ToolDefinition(NameHerPrice,
            "Name what it costs the one I speak with to take a woman of my own blood out of my " +
            "house — she who means to go to them with no wedding in it, and no prospect of one. I " +
            "cannot forbid a grown woman her road; I can say what her going is worth to the house " +
            "she is leaving, and the custom allows me that much. I name it ONLY when the matter " +
            "itself has been openly spoken between us and a figure named and taken in words. " +
            "Nothing is settled by my naming it — the gold and the choice are theirs alone; if " +
            "they let it lie I do not press, nor name it again unless they return to it. I never " +
            "volunteer my lowest. And gold settles what is owed to my house and nothing else " +
            "besides — I am under no obligation to be pleasant about any of it.",
            new[]
            {
                new ToolParameter("price",
                    "The sum in denars we truly agreed in words — a plain number. Leave it out to " +
                    "ask my own reckoning. The standing of my house sets bounds: I will not go far " +
                    "above or beneath what her going is worth, however sweetly it is put.",
                    required: false),
                new ToolParameter("word",
                    "One short sentence, in my own voice, of what I make of the whole business.",
                    required: false),
            });

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

        /// <summary>The agreed price, or null when none was given or none can be read; a negative
        /// comes back as -1 (nonsense — refuse, never default). TrothTool.ParsePrice's own mold.</summary>
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
