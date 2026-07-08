using System;
using System.Collections.Generic;

namespace ImmersiveAI.Core.Memory
{
    public static class MemoryTokenEstimator
    {
        private const int TokensPerTurnOverhead = 8;

        public static int EstimateRecentTurnsTokens(IEnumerable<ConversationTurn> turns)
        {
            if (turns == null) throw new ArgumentNullException(nameof(turns));

            var total = 0;
            foreach (var turn in turns)
            {
                if (turn == null) continue;
                total += EstimateTextTokens(turn.PlayerLine) + EstimateTextTokens(turn.NpcLine) + TokensPerTurnOverhead;
            }

            return total;
        }

        public static int EstimateTextTokens(string? text)
        {
            var value = text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return 0;

            var chars = value.Trim().Length;
            return Math.Max(1, (chars + 3) / 4);
        }
    }
}
