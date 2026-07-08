using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// Folds an NPC's oldest conversation turns into the rolling summary and distills
    /// durable facts, using one LLM call. This is what keeps the live prompt small and
    /// relevant instead of an ever-growing wall of history.
    /// </summary>
    public sealed class MemoryCompressor
    {
        private readonly IChatClient _client;

        public MemoryCompressor(IChatClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>Compresses the oldest turns, keeping the newest ones verbatim. Returns false if there was nothing to do.</summary>
        public async Task<bool> CompressAsync(NpcMemory memory, int keepMostRecent, CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            var turns = memory.GetTurnsToCompress(keepMostRecent);
            if (turns.Count == 0) return false;

            var request = BuildCompressionRequest(memory, turns);
            var response = await _client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            var parsed = ParseResponse(response);
            if (string.IsNullOrWhiteSpace(parsed.Summary)) return false;

            memory.ApplyCompression(parsed.Summary!, turns.Count, parsed.Facts);
            return true;
        }

        public static IReadOnlyList<ChatMessage> BuildCompressionRequest(NpcMemory memory, IReadOnlyList<ConversationTurn> turns)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"You maintain the memory of {memory.NpcName}, a character in a medieval world.");
            sb.AppendLine("Condense the conversation excerpts below into what the character would remember.");
            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine();
                sb.AppendLine("Existing memory (merge it with the new exchanges):");
                sb.AppendLine(memory.Summary.Trim());
            }
            sb.AppendLine();
            sb.AppendLine("New exchanges to fold in:");
            foreach (var turn in turns)
            {
                sb.AppendLine($"[Day {turn.GameDay:0}] They said: {turn.PlayerLine}");
                sb.AppendLine($"{memory.NpcName} replied: {turn.NpcLine}");
            }
            sb.AppendLine();
            sb.AppendLine("Reply in exactly this format:");
            sb.AppendLine("SUMMARY:");
            sb.AppendLine("<one short paragraph, first person, of what the character remembers>");
            sb.AppendLine("FACTS:");
            sb.AppendLine("- <durable fact worth remembering long-term, if any>");
            sb.AppendLine("List at most 3 facts; only genuinely important, lasting ones. If none, write FACTS: none.");

            return new List<ChatMessage> { ChatMessage.User(sb.ToString()) };
        }

        public static CompressionResult ParseResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return new CompressionResult(null, new List<string>());

            var summaryIdx = response.IndexOf("SUMMARY:", StringComparison.OrdinalIgnoreCase);
            var factsIdx = response.IndexOf("FACTS:", StringComparison.OrdinalIgnoreCase);

            string summary;
            var facts = new List<string>();

            if (summaryIdx < 0)
            {
                // Model ignored the format; treat everything as the summary.
                summary = response.Trim();
            }
            else
            {
                var start = summaryIdx + "SUMMARY:".Length;
                var end = factsIdx > summaryIdx ? factsIdx : response.Length;
                summary = response.Substring(start, end - start).Trim();
            }

            if (factsIdx >= 0)
            {
                var factsBlock = response.Substring(factsIdx + "FACTS:".Length);
                facts = factsBlock
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => l.StartsWith("-"))
                    .Select(l => l.TrimStart('-', ' ').Trim())
                    .Where(l => l.Length > 0 && !string.Equals(l, "none", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new CompressionResult(summary.Length == 0 ? null : summary, facts);
        }

        public sealed class CompressionResult
        {
            public string? Summary { get; }
            public List<string> Facts { get; }

            public CompressionResult(string? summary, List<string> facts)
            {
                Summary = summary;
                Facts = facts;
            }
        }
    }
}
