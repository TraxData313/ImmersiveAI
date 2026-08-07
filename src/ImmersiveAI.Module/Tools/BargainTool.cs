using System;
using System.Text.RegularExpressions;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The bargain's hand (2026.08.07, Anton's ask): an unhired wanderer speaking with the player may
    /// STRIKE the hiring bargain from inside the conversation — the same three acts vanilla's tavern
    /// dialog performs, reached through her own words. Exploit-proof by construction: the tool only
    /// LAYS terms on the table; no gold moves and no one joins until the player seals the exact named
    /// price in a confirm popup, and the resolver enforces the game's own rules — the true
    /// hiring-price model with a hard haggling rail around it (ConversationHiringHagglePercent,
    /// default ±30%), enough gold, room under the companion limit — no matter what the model asked
    /// for. The daily wage is never negotiable.
    /// </summary>
    public static class BargainTool
    {
        public const string StrikeBargain = "strike_bargain";

        /// <summary>What one spoken turn laid on the table, filled by the resolver — the trunk shows
        /// the seal popup only after the reply lands, so her closing words come first. One offer per
        /// turn: a second call answers "already laid". (Same pattern as HeartTool.Tally — tool calls
        /// resolve one at a time inside the loop, so plain fields are safe.)</summary>
        public sealed class Tally
        {
            public bool Laid;
            public int Price;
        }

        public static readonly ToolDefinition Tool = new ToolDefinition(StrikeBargain,
            "Strike the hiring bargain: lay my agreed terms of service formally before the one I " +
            "speak with, for them to seal or let lie. I call this ONLY when both are true: they " +
            "have clearly said, in their own words, that they will take me into their company, and " +
            "the hiring price has been spoken and accepted between us. Nothing is settled by this " +
            "alone — the choice to seal it, and the gold, remain wholly theirs. I never lay it " +
            "unbidden, never to press or to test, and if they let my offer lie I do not lay it " +
            "again unless they themselves return to it. My daily keep afterward is not mine to " +
            "bargain — it is what any companion's keep is.",
            new[]
            {
                new ToolParameter("price",
                    "The hiring price in denars we truly agreed in words — a plain number. Leave it " +
                    "out to ask my own honest reckoning. My honor and my need both set bounds: I " +
                    "will not go far above or below my true worth, however sweetly asked.",
                    required: false),
            });

        /// <summary>The agreed price the call carries, or null when none was given or none can be
        /// read ("900", "900 denars", "1,200" all count; the first run of digits wins). A null is
        /// answered with the game's own reckoning, never refused.</summary>
        public static int? ParsePrice(ToolCall call)
        {
            try
            {
                var raw = JObject.Parse(call.ArgumentsJson)["price"]?.ToString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var m = Regex.Match(raw.Replace(",", "").Replace(" ", ""), @"-?\d+");
                if (!m.Success) return null;
                if (!long.TryParse(m.Value, out long v)) return null;
                if (v < 0) return -1;                      // a "negative price" is nonsense — refuse, don't default
                return v > 10_000_000 ? 10_000_000 : (int)v;
            }
            catch { return null; }
        }
    }
}
