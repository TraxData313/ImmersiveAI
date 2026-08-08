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

        // How much of a token one character costs, in tenths, so the arithmetic stays integer.
        // English prose runs about four characters to the token; text outside ASCII — Cyrillic,
        // Greek, CJK — tokenizes far worse, because the byte-pair vocabularies these models carry
        // are built mostly from English (measured on a Bulgarian memory, 2026.08.08: ~2.8 chars to
        // the token, i.e. each character costing ~1.6 of an English one). We round that up a
        // little on purpose: overestimating only compresses slightly early, while underestimating
        // sends a prompt bigger than the share of the context window the player asked for.
        private const int AsciiCharWeight = 10;
        private const int WideCharWeight = 16;
        private const int WeightPerToken = 40;

        public static int EstimateTextTokens(string? text)
        {
            var value = text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return 0;

            var trimmed = value.Trim();
            var weight = 0;
            foreach (var c in trimmed)
                weight += c < 128 ? AsciiCharWeight : WideCharWeight;

            return Math.Max(1, (weight + WeightPerToken - 1) / WeightPerToken);
        }
    }
}
