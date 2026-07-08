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
        /// <summary>Default in-fiction name for the "System" voice that addresses the NPC.</summary>
        public const string DefaultSystemVoiceName = "Angel";

        private readonly IChatClient _client;

        public MemoryCompressor(IChatClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>Compresses the oldest turns, keeping the newest ones verbatim. Returns false if there was nothing to do.</summary>
        public async Task<bool> CompressAsync(NpcMemory memory, int keepMostRecent, string? systemVoiceName = null, CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            var turns = memory.GetTurnsToCompress(keepMostRecent);
            if (turns.Count == 0) return false;

            var request = BuildCompressionRequest(memory, turns, systemVoiceName);
            var response = await _client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            var parsed = ParseResponse(response);
            if (string.IsNullOrWhiteSpace(parsed.Summary)) return false;

            memory.ApplyCompression(parsed.Summary!, turns.Count, parsed.Facts);
            return true;
        }

        /// <summary>
        /// Builds the memory-reflection request. Rather than treating the NPC as a data store to be
        /// summarized, a named "System" voice addresses them directly, in the second person, and
        /// leaves it to them to decide what endures and what fades — they are an individual, not a log.
        /// They are shown their whole deep memory (rolling summary + durable facts) and what is still
        /// fresh (the recent turns that stay verbatim), so they update it with full context. The
        /// SUMMARY:/FACTS: reply format is preserved for parsing.
        /// </summary>
        public static IReadOnlyList<ChatMessage> BuildCompressionRequest(NpcMemory memory, IReadOnlyList<ConversationTurn> turns, string? systemVoiceName = null)
        {
            var voice = string.IsNullOrWhiteSpace(systemVoiceName) ? DefaultSystemVoiceName : systemVoiceName!.Trim();
            var name = string.IsNullOrWhiteSpace(memory.NpcName) ? "you" : memory.NpcName.Trim();

            // The turns being folded in are the oldest; the rest stay verbatim and are shown as
            // still-fresh context so the reflection is coherent with the present, not just the past.
            var freshTurns = memory.RecentTurns.Skip(turns.Count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"{voice} (System) addresses you, {name}:");
            sb.AppendLine("Time moves on, and older moments are slipping from sharp memory into the haze of the past. "
                + "It is yours alone to decide what to carry forward and what to let go. Keep what matters to who you "
                + "are and what you care about; release the rest. Speak in your own voice.");

            if (memory.KnownFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Truths you already hold as lasting (keep, refine, or let fall away as you see fit):");
                foreach (var fact in memory.KnownFacts)
                    sb.AppendLine("- " + fact);
            }

            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine();
                sb.AppendLine("What you already hold in memory (weave the fading moments into it):");
                sb.AppendLine(memory.Summary.Trim());
            }

            sb.AppendLine();
            sb.AppendLine("The moments now fading (fold these into your memory):");
            foreach (var turn in turns)
            {
                sb.AppendLine($"[Day {turn.GameDay:0}] They said: {turn.PlayerLine}");
                sb.AppendLine($"You answered: {turn.NpcLine}");
            }

            if (freshTurns.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Still fresh in your mind (context only — these stay with you, do not fold them in yet):");
                foreach (var turn in freshTurns)
                {
                    sb.AppendLine($"[Day {turn.GameDay:0}] They said: {turn.PlayerLine}");
                    sb.AppendLine($"You answered: {turn.NpcLine}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Answer {voice} in exactly this format:");
            sb.AppendLine("SUMMARY:");
            sb.AppendLine("<one short paragraph, in your own first-person voice, of what you choose to remember>");
            sb.AppendLine("FACTS:");
            sb.AppendLine("- <a lasting truth worth never forgetting, if any>");
            sb.AppendLine("Name at most 3 such truths; only what genuinely endures. If none, write FACTS: none.");

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
