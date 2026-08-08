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
        /// <summary>LEGACY: the default name of the retired narrator voice (the Angel, retired
        /// 2026.08.07). Still needed to attribute recorded Angel turns from older saves truthfully
        /// when they are folded into a reflection; no new prompt speaks in this voice.</summary>
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

            memory.ApplyCompression(parsed.Summary!, turns.Count);
            return true;
        }

        /// <summary>
        /// A deliberate reflection (the player asks the NPC to settle her memory now). Unlike
        /// <see cref="CompressAsync"/> this always runs while there is anything to reflect on — even
        /// when nothing is old enough to fold away — so she re-thinks and may rewrite her rolling
        /// summary and durable facts. It only drops the oldest turns beyond <paramref name="keepMostRecent"/>;
        /// when there is nothing to drop, every recent turn is kept verbatim. Returns false only if
        /// there is no memory at all to reflect on, or the LLM returned nothing usable.
        /// </summary>
        public async Task<bool> ReflectAsync(NpcMemory memory, int keepMostRecent, string? systemVoiceName = null, NpcSelf? self = null, CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (keepMostRecent < 0) keepMostRecent = 0;

            // The oldest turns beyond the keep window are folded in; this may be empty (nothing to drop).
            var turns = memory.GetTurnsToCompress(keepMostRecent);

            // Nothing to reflect on at all — no history and no prior deep memory.
            if (memory.RecentTurns.Count == 0 && string.IsNullOrWhiteSpace(memory.Summary))
                return false;

            // When a self is supplied, the reflection also invites the NPC to look inward and, if they
            // wish, revise who they feel themselves to be — this is how their self-concept grows.
            var request = BuildReflectionRequest(memory, turns, systemVoiceName, self?.Text);
            var response = await _client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            var parsed = ParseResponse(response);
            if (string.IsNullOrWhiteSpace(parsed.Summary)) return false;

            memory.ApplyCompression(parsed.Summary!, turns.Count);

            // Only rewrite the self when they actually offered a new one; "unchanged" (however the model
            // punctuates or capitalizes it) or nothing leaves their sense of self exactly as it was.
            if (self != null && !string.IsNullOrWhiteSpace(parsed.Self) && !IsUnchangedMarker(parsed.Self))
                self.Text = parsed.Self!.Trim();

            return true;
        }

        // Everything a model may dress the marker in: quotes, brackets, markdown bold/italic/rules/
        // headings, stray sentence punctuation. Safe to strip aggressively because the only thing we
        // ever compare against afterwards is the single word "unchanged".
        private static readonly char[] MarkerDressing =
            { '"', '\'', '(', ')', '[', ']', '*', '.', '!', '?', ' ', '\t', '\r', '-', '_', '#', '`', '~', ':', '>' };

        /// <summary>
        /// True when a self-concept value is really just the "nothing has changed" marker, not prose —
        /// e.g. "unchanged", "Unchanged.", "(unchanged)", or markdown-dressed forms like a decoration
        /// line above the word ("**\nUnchanged."). Judged line by line: lines that carry no words are
        /// decoration; the marker is exactly one meaningful line saying "unchanged".
        /// </summary>
        public static bool IsUnchangedMarker(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var meaningful = text!.Split('\n')
                .Select(l => l.Trim().Trim(MarkerDressing))
                .Where(l => l.Length > 0)
                .ToList();
            return meaningful.Count == 1 && string.Equals(meaningful[0], "unchanged", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the memory-compression request as the NPC's OWN inner monologue, first person — no
        /// narrator hands them anything (the Angel retired 2026.08.07): they sit with their memory and
        /// decide themselves what endures and what fades — they are an individual, not a log.
        /// They are shown their whole deep memory (the rolling summary) and what is still fresh (the
        /// recent turns that stay verbatim), so they update it with full context. The SUMMARY: reply
        /// label is preserved for parsing. The voice name is kept only to attribute recorded Angel
        /// turns from older saves truthfully in the transcript.
        /// </summary>
        public static IReadOnlyList<ChatMessage> BuildCompressionRequest(NpcMemory memory, IReadOnlyList<ConversationTurn> turns, string? systemVoiceName = null)
        {
            var voice = string.IsNullOrWhiteSpace(systemVoiceName) ? DefaultSystemVoiceName : systemVoiceName!.Trim();

            // The turns being folded in are the oldest; the rest stay verbatim and are shown as
            // still-fresh context so the reflection is coherent with the present, not just the past.
            var freshTurns = memory.RecentTurns.Skip(turns.Count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(InnerOpening(memory, "I sit a moment with my memory of this person and settle it."));
            sb.AppendLine("Time moves on, and older moments are slipping from sharp memory into the haze of the past. "
                + "It is mine alone to decide what to carry forward and what to let go. I keep what matters to who I "
                + "am and what I care about; I release the rest. I speak in my own voice.");

            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine();
                sb.AppendLine("What I already hold in memory (I weave the fading moments into it):");
                sb.AppendLine(memory.Summary.Trim());
            }

            sb.AppendLine();
            sb.AppendLine("The moments now fading (I fold these into my memory):");
            foreach (var turn in turns)
                AppendReflectedTurn(sb, turn, voice);

            if (freshTurns.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Still fresh in my mind (context only — these stay with me; I do not fold them in yet):");
                foreach (var turn in freshTurns)
                    AppendReflectedTurn(sb, turn, voice);
            }

            AppendReplyFormat(sb);

            return new List<ChatMessage> { ChatMessage.User(sb.ToString()) };
        }

        // The first line of every memory-work prompt: the NPC's own mind opening, named when the
        // name is known ("(Within my own mind — I, Ilya — ...)"), plain otherwise. Mirrors the
        // InnerFrame shape the live beats use, so all inner work reads as one voice.
        private static string InnerOpening(NpcMemory memory, string act)
        {
            var name = string.IsNullOrWhiteSpace(memory.NpcName) ? null : memory.NpcName.Trim();
            return name == null
                ? $"(Within my own mind: {act})"
                : $"(Within my own mind — I, {name}: {act})";
        }

        // The SUMMARY: reply contract shared by compression and reflection — the single lever on how
        // good the deep memory is, since 2026.08.08 the ONLY durable thing she carries of a person
        // (the distilled FACTS list is retired). Two things it must keep doing, both inherited from
        // that retired contract: invite the particulars a bare précis would smooth away, and warn her
        // plainly that the summary is rewritten WHOLE — without that line each pass quietly erodes
        // what the truths used to nail down. No length ceiling on purpose; MaxMemoryWriteTokens is
        // the only real bound, and it no longer shares its room with FACTS and GOALS.
        private static void AppendReplyFormat(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("I set it down in exactly this shape:");
            sb.AppendLine("SUMMARY:");
            sb.AppendLine("<all I carry of them, in my own first-person voice — who they are to me and how that came to be, "
                + "and the particular things I would not lose: what was said and promised between us, what was given and done, the names that matter.>");
            sb.AppendLine("I write it whole each time, for what I do not set down here fades from me. "
                + "I take as much room as the story truly asks — a few paragraphs, and more where there is more to hold.");
        }

        /// <summary>
        /// Builds a deliberate-reflection request as the NPC's own first-person inner monologue. Like
        /// the compression request it shows her whole deep memory, but it always asks her to settle
        /// her memory — folding in the fading turns if there are any, and simply revising her summary
        /// if there are none, while the recent turns stay with her. Same SUMMARY: reply contract as
        /// compression.
        /// </summary>
        public static IReadOnlyList<ChatMessage> BuildReflectionRequest(NpcMemory memory, IReadOnlyList<ConversationTurn> turnsToFold, string? systemVoiceName = null, string? selfText = null)
        {
            var voice = string.IsNullOrWhiteSpace(systemVoiceName) ? DefaultSystemVoiceName : systemVoiceName!.Trim();

            // A null self means "do not touch the self this time"; an empty (but non-null) self means the
            // NPC has one but has not yet put it into words, and is being invited to do so.
            var reflectOnSelf = selfText != null;

            var freshTurns = memory.RecentTurns.Skip(turnsToFold.Count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(InnerOpening(memory, "I pause a while and gather my thoughts about this person."));
            sb.AppendLine("I settle my memory of them as I see fit — I keep what matters to who I am and what I "
                + "care about, refine what has changed, and let go of what no longer serves. I speak in my own voice.");

            if (reflectOnSelf)
            {
                sb.AppendLine();
                sb.AppendLine("And I look inward, too, for a moment — who have I become?");
                if (string.IsNullOrWhiteSpace(selfText))
                    sb.AppendLine("I have not yet put into words who I feel myself to be. If I wish, I may do so now.");
                else
                {
                    sb.AppendLine("This is how I have seen myself, in my own heart (I keep it, refine it, or let it change as I have):");
                    sb.AppendLine(selfText!.Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine();
                sb.AppendLine("What I already hold in memory (I revise it as I reflect):");
                sb.AppendLine(memory.Summary.Trim());
            }

            if (turnsToFold.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Older moments now fading (I fold these into my memory):");
                foreach (var turn in turnsToFold)
                    AppendReflectedTurn(sb, turn, voice);
            }

            if (freshTurns.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Still fresh in my mind (these remain with me — I draw on them, but they are not fading yet):");
                foreach (var turn in freshTurns)
                    AppendReflectedTurn(sb, turn, voice);
            }

            AppendReplyFormat(sb);
            if (reflectOnSelf)
            {
                sb.AppendLine("SELF:");
                // On a first-ever self (nothing written yet) don't offer the "unchanged" escape — gently
                // invite them to actually put themselves into words. Once they have a self, allow it to stand.
                sb.AppendLine(string.IsNullOrWhiteSpace(selfText)
                    ? "<a short paragraph, in my own first-person voice, of who I feel myself to be — my spirit, my longings, what I hold dear.>"
                    : "<a short paragraph, in my own first-person voice, of who I feel myself to be now — my spirit, my longings, what I hold dear. If nothing has changed, I write: unchanged.>");
            }

            return new List<ChatMessage> { ChatMessage.User(sb.ToString()) };
        }

        // Renders one remembered turn into the reflection transcript, attributing the incoming line to
        // whoever spoke it — the player ("They"), the NPC's own mind, or the retired Angel narrator (by
        // name; legacy saves only) — so a folded-in exchange is never misattributed in the summary.
        private static void AppendReflectedTurn(StringBuilder sb, ConversationTurn turn, string voice)
        {
            // An inner beat is no one speaking TO them — it is their own mind at work (a reach-out
            // weighed, an arrival met, a letter written); attribute it so the summary never invents
            // a second voice.
            if (turn.IsInnerThought)
            {
                sb.AppendLine($"[{TurnStamp(turn)}] My own thought, then: {turn.PlayerLine}");
                if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                    sb.AppendLine($"I: {turn.NpcLine}");
                return;
            }

            var speaker = turn.IsFromAngel ? voice : "They";
            sb.AppendLine($"[{TurnStamp(turn)}] {speaker} said: {turn.PlayerLine}");
            // A silent beat (a meeting noted, no reply recorded) carries no answer — never invent one.
            if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                sb.AppendLine($"I answered: {turn.NpcLine}");
        }

        /// <summary>A short "where and when" label for a turn: place and/or Calradia time if recorded,
        /// otherwise the campaign day it was saved with (older turns predate place/time tracking).</summary>
        private static string TurnStamp(ConversationTurn turn)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(turn.Place)) parts.Add(turn.Place.Trim());
            if (!string.IsNullOrWhiteSpace(turn.CalradiaTime)) parts.Add(turn.CalradiaTime.Trim());
            return parts.Count == 0 ? $"Day {turn.GameDay:0}" : string.Join(", ", parts);
        }

        public static CompressionResult ParseResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return new CompressionResult(null, null);

            var summaryIdx = response.IndexOf("SUMMARY:", StringComparison.OrdinalIgnoreCase);
            var selfIdx = response.IndexOf("SELF:", StringComparison.OrdinalIgnoreCase);

            // The retired sections (FACTS:/GOALS:, 2026.08.08) are no longer asked for, but a model
            // trained on the habit — or an NPC whose replayed memory still shows the old shape — may
            // volunteer one anyway. They are never read; they only still BOUND their neighbours, so a
            // stray list can never silt up the summary or the self with bullet points.
            var factsIdx = response.IndexOf("FACTS:", StringComparison.OrdinalIgnoreCase);
            var goalsIdx = response.IndexOf("GOALS:", StringComparison.OrdinalIgnoreCase);

            string summary;
            if (summaryIdx < 0)
            {
                // Model ignored the SUMMARY label; treat everything up to the first known section as summary.
                var end = NextSection(0, response.Length, selfIdx, factsIdx, goalsIdx);
                summary = response.Substring(0, end).Trim();
            }
            else
            {
                var start = summaryIdx + "SUMMARY:".Length;
                var end = NextSection(start, response.Length, selfIdx, factsIdx, goalsIdx);
                summary = response.Substring(start, end - start).Trim();
            }

            string? self = null;
            if (selfIdx >= 0)
            {
                var start = selfIdx + "SELF:".Length;
                var end = NextSection(start, response.Length, factsIdx, goalsIdx);
                var block = response.Substring(start, end - start).Trim();
                if (block.Length > 0) self = block;
            }

            return new CompressionResult(summary.Length == 0 ? null : summary, self);
        }

        // The earliest section-label position at or after <afterPos>, or <length> if none of them apply.
        // Used to bound one section's text so it never bleeds into the next (SUMMARY -> SELF).
        private static int NextSection(int afterPos, int length, params int[] sectionIndices)
        {
            var best = length;
            foreach (var idx in sectionIndices)
                if (idx >= afterPos && idx < best) best = idx;
            return best;
        }

        public sealed class CompressionResult
        {
            /// <summary>The whole of what she now remembers of this person, rewritten from scratch;
            /// null when the reply carried nothing usable, in which case the caller keeps the old one.</summary>
            public string? Summary { get; }

            /// <summary>The NPC's rewritten sense of self, if they offered one this reflection; otherwise
            /// null (no SELF section was asked for or returned). "unchanged" is handled by the caller.</summary>
            public string? Self { get; }

            public CompressionResult(string? summary, string? self = null)
            {
                Summary = summary;
                Self = self;
            }
        }
    }
}
