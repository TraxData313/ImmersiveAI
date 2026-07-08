using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// Long-term memory for one NPC. Three layers:
    /// - RecentTurns: verbatim recent exchanges, sent to the LLM as real multi-turn messages.
    /// - Summary: rolling prose summary of older exchanges, compressed by an LLM call.
    /// - KnownFacts: durable one-line facts the NPC has learned ("The player saved my caravan on day 34").
    /// This replaces ChatAi's flat truncated string list, which is why its NPCs forget and repeat.
    /// </summary>
    public sealed class NpcMemory
    {
        public int Version { get; set; } = 1;
        public string NpcId { get; set; } = string.Empty;
        public string NpcName { get; set; } = string.Empty;

        public List<ConversationTurn> RecentTurns { get; set; } = new List<ConversationTurn>();
        public string Summary { get; set; } = string.Empty;
        public List<string> KnownFacts { get; set; } = new List<string>();

        /// <summary>Human-readable Calradia timestamp of when the Summary was last regrouped
        /// (set by the game layer at compression time — Core has no game clock). Lets the NPC and
        /// the player see that deep memories reflect a past moment and may be out of date.</summary>
        public string SummaryAsOf { get; set; } = string.Empty;

        public double LastConversationGameDay { get; set; } = -1;

        public void AddTurn(ConversationTurn turn)
        {
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            RecentTurns.Add(turn);
            LastConversationGameDay = turn.GameDay;
        }

        /// <summary>True when older turns should be compressed into the rolling summary.</summary>
        public bool NeedsCompression(int maxRecentTurns)
        {
            return RecentTurns.Count > maxRecentTurns;
        }

        /// <summary>True when older turns should be compressed by turn count, age, or estimated token pressure.</summary>
        public bool NeedsCompression(int maxRecentTurns, double currentGameDay, int maxRecentDays, int maxRecentMemoryTokens)
        {
            return NeedsCompression(maxRecentTurns)
                || HasTurnsOlderThan(currentGameDay, maxRecentDays)
                || MemoryTokenEstimator.EstimateRecentTurnsTokens(RecentTurns) > maxRecentMemoryTokens;
        }

        public int GetKeepMostRecentForCompression(
            int keepRecentTurns,
            double currentGameDay,
            int keepRecentDays,
            int minRecentMemoryTokensAfterCompression)
        {
            if (keepRecentTurns < 0) throw new ArgumentOutOfRangeException(nameof(keepRecentTurns));
            if (keepRecentDays < 0) throw new ArgumentOutOfRangeException(nameof(keepRecentDays));
            if (minRecentMemoryTokensAfterCompression < 0) throw new ArgumentOutOfRangeException(nameof(minRecentMemoryTokensAfterCompression));

            var keepCount = Math.Min(RecentTurns.Count, keepRecentTurns);

            if (keepRecentDays > 0)
            {
                var cutoffDay = currentGameDay - keepRecentDays;
                var turnsInsideWindow = RecentTurns.Count(t => t.GameDay >= cutoffDay);
                keepCount = Math.Min(keepCount, turnsInsideWindow);
            }

            if (minRecentMemoryTokensAfterCompression > 0)
            {
                while (keepCount > 0 && MemoryTokenEstimator.EstimateRecentTurnsTokens(RecentTurns.Skip(RecentTurns.Count - keepCount)) > minRecentMemoryTokensAfterCompression)
                    keepCount--;
            }

            return keepCount;
        }

        private bool HasTurnsOlderThan(double currentGameDay, int maxRecentDays)
        {
            if (maxRecentDays <= 0 || RecentTurns.Count == 0) return false;
            var cutoffDay = currentGameDay - maxRecentDays;
            return RecentTurns.Any(t => t.GameDay < cutoffDay);
        }

        /// <summary>The oldest turns that should be folded into the summary, keeping the newest ones verbatim.</summary>
        public IReadOnlyList<ConversationTurn> GetTurnsToCompress(int keepMostRecent)
        {
            if (keepMostRecent < 0) throw new ArgumentOutOfRangeException(nameof(keepMostRecent));
            int count = RecentTurns.Count - keepMostRecent;
            return count <= 0
                ? (IReadOnlyList<ConversationTurn>)Array.Empty<ConversationTurn>()
                : RecentTurns.Take(count).ToList();
        }

        /// <summary>
        /// Replaces the compressed turns with the new rolling summary and merges any newly
        /// distilled facts (case-insensitive de-duplication).
        /// </summary>
        public void ApplyCompression(string newSummary, int consumedTurnCount, IEnumerable<string>? newFacts = null)
        {
            if (consumedTurnCount < 0 || consumedTurnCount > RecentTurns.Count)
                throw new ArgumentOutOfRangeException(nameof(consumedTurnCount));

            Summary = newSummary ?? string.Empty;
            RecentTurns.RemoveRange(0, consumedTurnCount);

            if (newFacts == null) return;
            foreach (var fact in newFacts)
            {
                if (string.IsNullOrWhiteSpace(fact)) continue;
                var trimmed = fact.Trim();
                if (!KnownFacts.Any(f => string.Equals(f, trimmed, StringComparison.OrdinalIgnoreCase)))
                    KnownFacts.Add(trimmed);
            }
        }
    }
}
