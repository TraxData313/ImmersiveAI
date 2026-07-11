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
        /// <summary>Default name for the gentle in-fiction voice (the Angel) that speaks into the NPC's
        /// mind. Never surfaced as "System" — the NPC is addressed as a living individual, not a log.</summary>
        public const string DefaultSystemVoiceName = "Angel";

        /// <summary>How many lasting truths an NPC may carry when the caller does not say otherwise.</summary>
        public const int DefaultMaxKnownFacts = 10;

        private readonly IChatClient _client;

        public MemoryCompressor(IChatClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>Compresses the oldest turns, keeping the newest ones verbatim. Returns false if there was nothing to do.</summary>
        public async Task<bool> CompressAsync(NpcMemory memory, int keepMostRecent, string? systemVoiceName = null, int maxFacts = DefaultMaxKnownFacts, CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            var turns = memory.GetTurnsToCompress(keepMostRecent);
            if (turns.Count == 0) return false;

            var request = BuildCompressionRequest(memory, turns, systemVoiceName, maxFacts);
            var response = await _client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            var parsed = ParseResponse(response);
            if (string.IsNullOrWhiteSpace(parsed.Summary)) return false;

            ApplyParsedFacts(memory, parsed, turns.Count, maxFacts);
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
        public async Task<bool> ReflectAsync(NpcMemory memory, int keepMostRecent, string? systemVoiceName = null, NpcSelf? self = null, int maxFacts = DefaultMaxKnownFacts, NpcGoals? goals = null, int maxGoals = NpcGoals.DefaultMaxGoals, CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (keepMostRecent < 0) keepMostRecent = 0;

            // The oldest turns beyond the keep window are folded in; this may be empty (nothing to drop).
            var turns = memory.GetTurnsToCompress(keepMostRecent);

            // Nothing to reflect on at all — no history and no prior deep memory.
            if (memory.RecentTurns.Count == 0 && string.IsNullOrWhiteSpace(memory.Summary) && memory.KnownFacts.Count == 0)
                return false;

            // When a self is supplied, the reflection also invites the NPC to look inward and, if they
            // wish, revise who they feel themselves to be — this is how their self-concept grows. When
            // goals are supplied, it likewise invites them to rework the aims they carry.
            var request = BuildReflectionRequest(memory, turns, systemVoiceName, self?.Text, maxFacts, goals?.Goals, maxGoals);
            var response = await _client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            var parsed = ParseResponse(response);
            if (string.IsNullOrWhiteSpace(parsed.Summary)) return false;

            ApplyParsedFacts(memory, parsed, turns.Count, maxFacts);

            // Only rewrite the self when they actually offered a new one; "unchanged" (however the model
            // punctuates or capitalizes it) or nothing leaves their sense of self exactly as it was.
            if (self != null && !string.IsNullOrWhiteSpace(parsed.Self) && !IsUnchangedMarker(parsed.Self))
                self.Text = parsed.Self!.Trim();

            // Goals are replace-all like the truths: a present GOALS section IS the whole of the aims
            // they now carry (an empty one under "GOALS: none" releases them all); no section leaves
            // them untouched, so a malformed reply can never cost them their aims.
            if (goals != null && parsed.HasGoalsSection)
                goals.SetAll(parsed.Goals, maxGoals);

            return true;
        }

        // The NPC is shown her whole list of truths and asked to write anew all she chooses to keep —
        // so when her reply carries a FACTS section, that list IS her truths now (replace, letting the
        // unnamed ones fall away, trimmed to the budget). A reply with no FACTS section at all leaves
        // her truths exactly as they were: a malformed answer must never cost her memory.
        private static void ApplyParsedFacts(NpcMemory memory, CompressionResult parsed, int consumedTurnCount, int maxFacts)
        {
            if (maxFacts < 1) maxFacts = 1;
            if (parsed.HasFactsSection)
                memory.ApplyCompression(parsed.Summary!, consumedTurnCount, parsed.Facts.Take(maxFacts), replaceFacts: true);
            else
                memory.ApplyCompression(parsed.Summary!, consumedTurnCount);
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
        /// Builds the memory-reflection request. Rather than treating the NPC as a data store to be
        /// summarized, a gentle named voice (the Angel) speaks into their mind directly, in the second
        /// person, and leaves it to them to decide what endures and what fades — they are an individual, not a log.
        /// They are shown their whole deep memory (rolling summary + durable facts) and what is still
        /// fresh (the recent turns that stay verbatim), so they update it with full context. The
        /// SUMMARY:/FACTS: reply format is preserved for parsing.
        /// </summary>
        public static IReadOnlyList<ChatMessage> BuildCompressionRequest(NpcMemory memory, IReadOnlyList<ConversationTurn> turns, string? systemVoiceName = null, int maxFacts = DefaultMaxKnownFacts)
        {
            var voice = string.IsNullOrWhiteSpace(systemVoiceName) ? DefaultSystemVoiceName : systemVoiceName!.Trim();
            var name = string.IsNullOrWhiteSpace(memory.NpcName) ? "you" : memory.NpcName.Trim();

            // The turns being folded in are the oldest; the rest stay verbatim and are shown as
            // still-fresh context so the reflection is coherent with the present, not just the past.
            var freshTurns = memory.RecentTurns.Skip(turns.Count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"{voice} speaks gently into your mind, {name}:");
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
                AppendReflectedTurn(sb, turn, voice);

            if (freshTurns.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Still fresh in your mind (context only — these stay with you, do not fold them in yet):");
                foreach (var turn in freshTurns)
                    AppendReflectedTurn(sb, turn, voice);
            }

            AppendReplyFormat(sb, voice, maxFacts);

            return new List<ChatMessage> { ChatMessage.User(sb.ToString()) };
        }

        // The SUMMARY:/FACTS: reply contract shared by compression and reflection. The FACTS ask is
        // written for replace semantics: she must restate every truth she chooses to keep carrying,
        // because the list she returns becomes the whole of what she holds (see ApplyParsedFacts).
        private static void AppendReplyFormat(StringBuilder sb, string voice, int maxFacts)
        {
            if (maxFacts < 1) maxFacts = 1;
            sb.AppendLine();
            sb.AppendLine($"Answer {voice} in exactly this format:");
            sb.AppendLine("SUMMARY:");
            sb.AppendLine("<what you choose to remember, in your own first-person voice — a paragraph, or two or three if the story asks for them>");
            sb.AppendLine("FACTS:");
            sb.AppendLine("- <a lasting truth worth never forgetting, if any>");
            sb.AppendLine($"Write your truths anew, in full — every one you choose to go on holding: the kept, the refined, and the newly won alike, each named once. "
                + $"What you do not write here falls away from you. Hold at most {maxFacts}; if none endure, write FACTS: none.");
        }

        /// <summary>
        /// Builds a deliberate-reflection request. Like the compression request it addresses the NPC by
        /// name through the System voice and shows her whole deep memory, but it always asks her to
        /// settle her memory — folding in the fading turns if there are any, and simply revising her
        /// summary and facts if there are none, while the recent turns stay with her. Same SUMMARY:/FACTS:
        /// reply contract as compression.
        /// </summary>
        public static IReadOnlyList<ChatMessage> BuildReflectionRequest(NpcMemory memory, IReadOnlyList<ConversationTurn> turnsToFold, string? systemVoiceName = null, string? selfText = null, int maxFacts = DefaultMaxKnownFacts, IReadOnlyList<string>? goals = null, int maxGoals = NpcGoals.DefaultMaxGoals)
        {
            var voice = string.IsNullOrWhiteSpace(systemVoiceName) ? DefaultSystemVoiceName : systemVoiceName!.Trim();
            var name = string.IsNullOrWhiteSpace(memory.NpcName) ? "you" : memory.NpcName.Trim();

            // A null self means "do not touch the self this time"; an empty (but non-null) self means the
            // NPC has one but has not yet put it into words, and is being invited to do so.
            var reflectOnSelf = selfText != null;

            // A null goals list means "do not touch the aims this time"; an empty (but non-null) list means
            // they carry none yet and are invited to name any that have taken shape.
            var reflectOnGoals = goals != null;

            var freshTurns = memory.RecentTurns.Skip(turnsToFold.Count).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"{voice} speaks gently into your mind, {name}:");
            sb.AppendLine("Pause a while and gather your thoughts about this person. Settle your memory of them as "
                + "you see fit — keep what matters to who you are and what you care about, refine what has changed, "
                + "and let go of what no longer serves. Speak in your own voice.");

            if (reflectOnSelf)
            {
                sb.AppendLine();
                sb.AppendLine("And look inward, too, for a moment — consider who you have become.");
                if (string.IsNullOrWhiteSpace(selfText))
                    sb.AppendLine("You have not yet put into words who you feel yourself to be. If you wish, you may do so now.");
                else
                {
                    sb.AppendLine("This is how you have seen yourself, in your own heart (keep it, refine it, or let it change as you have):");
                    sb.AppendLine(selfText!.Trim());
                }
            }

            if (reflectOnGoals)
            {
                sb.AppendLine();
                sb.AppendLine("And weigh, too, what you strive for — the aims you carry of your own will.");
                if (goals!.Count == 0)
                    sb.AppendLine("You have named no aim yet. If any longing has taken shape in you, you may set it down now.");
                else
                {
                    sb.AppendLine("These are the aims you have been carrying (keep them, reshape them, let go of what is won or lost, take up what is new):");
                    foreach (var goal in goals)
                        sb.AppendLine("- " + goal);
                }
            }

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
                sb.AppendLine("What you already hold in memory (revise it as you reflect):");
                sb.AppendLine(memory.Summary.Trim());
            }

            if (turnsToFold.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Older moments now fading (fold these into your memory):");
                foreach (var turn in turnsToFold)
                    AppendReflectedTurn(sb, turn, voice);
            }

            if (freshTurns.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Still fresh in your mind (these remain with you — draw on them, but they are not fading yet):");
                foreach (var turn in freshTurns)
                    AppendReflectedTurn(sb, turn, voice);
            }

            AppendReplyFormat(sb, voice, maxFacts);
            if (reflectOnSelf)
            {
                sb.AppendLine("SELF:");
                // On a first-ever self (nothing written yet) don't offer the "unchanged" escape — gently
                // invite them to actually put themselves into words. Once they have a self, allow it to stand.
                sb.AppendLine(string.IsNullOrWhiteSpace(selfText)
                    ? "<a short paragraph, in your own first-person voice, of who you feel yourself to be — your spirit, your longings, what you hold dear.>"
                    : "<a short paragraph, in your own first-person voice, of who you feel yourself to be now — your spirit, your longings, what you hold dear. If nothing has changed, write: unchanged.>");
            }
            if (reflectOnGoals)
            {
                if (maxGoals < 1) maxGoals = 1;
                sb.AppendLine("GOALS:");
                sb.AppendLine("- <an aim you strive for, in your own first-person voice, if any>");
                sb.AppendLine($"Write your aims anew, in full — every one you mean to keep striving toward, each named once. "
                    + $"What you do not write here you have set aside. Carry at most {maxGoals}; if you strive for nothing just now, write GOALS: none.");
            }

            return new List<ChatMessage> { ChatMessage.User(sb.ToString()) };
        }

        // Renders one remembered turn into the reflection transcript, attributing the incoming line to whoever
        // spoke it — the player ("They") or the Angel (by name) — so a folded-in Angel exchange is never
        // mistaken in the summary for something the player said.
        private static void AppendReflectedTurn(StringBuilder sb, ConversationTurn turn, string voice)
        {
            var speaker = turn.IsFromAngel ? voice : "They";
            sb.AppendLine($"[{TurnStamp(turn)}] {speaker} said: {turn.PlayerLine}");
            // A silent beat (a meeting noted, no reply recorded) carries no answer — never invent one.
            if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                sb.AppendLine($"You answered: {turn.NpcLine}");
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
            if (string.IsNullOrWhiteSpace(response)) return new CompressionResult(null, new List<string>(), null);

            var summaryIdx = response.IndexOf("SUMMARY:", StringComparison.OrdinalIgnoreCase);
            var factsIdx = response.IndexOf("FACTS:", StringComparison.OrdinalIgnoreCase);
            var selfIdx = response.IndexOf("SELF:", StringComparison.OrdinalIgnoreCase);
            var goalsIdx = response.IndexOf("GOALS:", StringComparison.OrdinalIgnoreCase);

            string summary;
            var facts = new List<string>();

            if (summaryIdx < 0)
            {
                // Model ignored the SUMMARY label; treat everything up to the first known section as summary.
                var end = NextSection(0, response.Length, factsIdx, selfIdx, goalsIdx);
                summary = response.Substring(0, end).Trim();
            }
            else
            {
                var start = summaryIdx + "SUMMARY:".Length;
                var end = NextSection(start, response.Length, factsIdx, selfIdx, goalsIdx);
                summary = response.Substring(start, end - start).Trim();
            }

            if (factsIdx >= 0)
            {
                // Facts run until the next section (self or goals), so neither is mistaken for a fact.
                var start = factsIdx + "FACTS:".Length;
                var end = NextSection(start, response.Length, selfIdx, goalsIdx);
                facts = ParseBulletList(response.Substring(start, end - start));
            }

            string? self = null;
            if (selfIdx >= 0)
            {
                // Self runs until the goals section (if any), so a goals list is never folded into the self.
                var start = selfIdx + "SELF:".Length;
                var end = NextSection(start, response.Length, goalsIdx);
                var block = response.Substring(start, end - start).Trim();
                if (block.Length > 0) self = block;
            }

            var goals = new List<string>();
            if (goalsIdx >= 0)
                goals = ParseBulletList(response.Substring(goalsIdx + "GOALS:".Length));

            return new CompressionResult(
                summary.Length == 0 ? null : summary, facts, self,
                hasFactsSection: factsIdx >= 0, goals: goals, hasGoalsSection: goalsIdx >= 0);
        }

        // A "- item" bullet block, with a bare "none" honored as "the empty list" (a deliberate clearing).
        private static List<string> ParseBulletList(string block) =>
            block
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("-"))
                .Select(l => l.TrimStart('-', ' ').Trim())
                .Where(l => l.Length > 0 && !string.Equals(l, "none", StringComparison.OrdinalIgnoreCase))
                .ToList();

        // The earliest section-label position at or after <afterPos>, or <length> if none of them apply.
        // Used to bound one section's text so it never bleeds into the next (SUMMARY -> FACTS -> SELF).
        private static int NextSection(int afterPos, int length, params int[] sectionIndices)
        {
            var best = length;
            foreach (var idx in sectionIndices)
                if (idx >= afterPos && idx < best) best = idx;
            return best;
        }

        public sealed class CompressionResult
        {
            public string? Summary { get; }
            public List<string> Facts { get; }

            /// <summary>The NPC's rewritten sense of self, if they offered one this reflection; otherwise
            /// null (no SELF section was asked for or returned). "unchanged" is handled by the caller.</summary>
            public string? Self { get; }

            /// <summary>True when the reply actually carried a FACTS section. Only then does
            /// <see cref="Facts"/> speak for the whole of her truths (replace semantics) — an empty list
            /// under a present section is her choosing to release them all ("FACTS: none"), while an
            /// absent section means the reply was malformed and her truths must stand untouched.</summary>
            public bool HasFactsSection { get; }

            /// <summary>The aims the NPC chose to carry on, if the reply carried a GOALS section
            /// (replace semantics, exactly like <see cref="Facts"/>).</summary>
            public List<string> Goals { get; }

            /// <summary>True when the reply actually carried a GOALS section — the same replace/none
            /// contract as <see cref="HasFactsSection"/>, so a malformed reply never wipes their aims.</summary>
            public bool HasGoalsSection { get; }

            public CompressionResult(
                string? summary, List<string> facts, string? self = null, bool hasFactsSection = false,
                List<string>? goals = null, bool hasGoalsSection = false)
            {
                Summary = summary;
                Facts = facts;
                Self = self;
                HasFactsSection = hasFactsSection;
                Goals = goals ?? new List<string>();
                HasGoalsSection = hasGoalsSection;
            }
        }
    }
}
