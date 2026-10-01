using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// Runs one spoken turn that may reach for tools along the way: complete → resolve any tool
    /// calls → hand the answers back → repeat, until the model speaks plainly or the recall budget
    /// is spent (the last round forbids new calls, so the turn always ends in words). Words that
    /// come with SILENT hands alone (<see cref="ToolDefinition.Silent"/>) end the turn at once;
    /// words beside any other hand are a draft, and the next round writes the reply whole. Answer
    /// fields (<see cref="ChatResult.AnswerCalls"/> — the heart's measure) come INSIDE the answer
    /// and are resolved with the words they came beside, so a reply that looks nothing up is ONE
    /// call carrying its measure (2026.10.01). Backends that
    /// cannot offer tools — and calls that offer none — fall back to a plain completion, so every
    /// caller can go through here unconditionally.
    /// </summary>
    public static class ToolLoopRunner
    {
        /// <summary>What the model is told when a recall fails or comes back empty — an honest blank,
        /// so it leans on what it truly holds instead of inventing.</summary>
        public const string NothingSurfaces = "Nothing surfaces — search as you may, that memory will not come just now.";

        public static async Task<string> RunAsync(
            IChatClient client,
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            Func<ToolCall, Task<string>>? resolveTool,
            int maxToolRounds = 3,
            CancellationToken cancellationToken = default)
        {
            if (!(client is IToolChatClient toolClient)
                || tools == null || tools.Count == 0
                || resolveTool == null || maxToolRounds <= 0)
            {
                return await client.CompleteAsync(messages, cancellationToken).ConfigureAwait(false);
            }

            var working = new List<ChatMessage>(messages);
            bool mayNarrow = client is IToolOfferPolicy policy && policy.OfferMayNarrowMidTurn;
            // Silent hands already used this turn: the heart is weighed once per exchange, so on a
            // road that allows it the hand is taken off the table for the rounds that follow.
            var spentSilent = new HashSet<string>(StringComparer.Ordinal);
            // Words that came beside a hand whose answer was still to come — a DRAFT. Never fed
            // back (that is what invited the coda), but kept: if every later round is silence,
            // the draft is still truer than nothing (haiku's "..." greeting, 2026.07.13).
            string draft = "";
            // The answer fields (the heart's measure) that came beside the draft — kept with it, so
            // a draft that ends up being the reply still carries its own measure.
            IReadOnlyList<ToolCall> draftAnswer = Array.Empty<ToolCall>();
            bool forceWords = false;
            for (int round = 0; ; round++)
            {
                bool allowToolUse = round < maxToolRounds && !forceWords;
                var offer = tools;
                if (allowToolUse && mayNarrow && spentSilent.Count > 0)
                {
                    var narrowed = tools.Where(t => !(t.Silent && spentSilent.Contains(t.Name))).ToList();
                    if (narrowed.Count == 0) allowToolUse = false;
                    else offer = narrowed;
                }

                var result = await toolClient
                    .CompleteWithToolsAsync(working, offer, allowToolUse, cancellationToken)
                    .ConfigureAwait(false);

                if (!result.WantsTools || !allowToolUse)
                {
                    var final = FinalWords(result.Text ?? "", draft);
                    // The measure belongs to the words that are spoken: this round's when it spoke,
                    // the draft's when the draft is what stands.
                    await ResolveAnswerAsync(HasWords(result.Text) ? result.AnswerCalls : draftAnswer, resolveTool)
                        .ConfigureAwait(false);
                    return Clean(final, tools);
                }

                bool silentOnly = result.ToolCalls.All(c => IsSilent(tools, c.Name));
                bool onlyRepeats = silentOnly && result.ToolCalls.All(c => spentSilent.Contains(c.Name));

                var answers = new List<string>(result.ToolCalls.Count);
                foreach (var call in result.ToolCalls)
                {
                    string answer;
                    try { answer = await resolveTool(call).ConfigureAwait(false); }
                    catch { answer = NothingSurfaces; }
                    if (string.IsNullOrWhiteSpace(answer)) answer = NothingSurfaces;
                    answers.Add(answer);
                    if (IsSilent(tools, call.Name)) spentSilent.Add(call.Name);
                }

                // WORDS BESIDE SILENT HANDS ARE THE REPLY (2026.10.01, the coda bug). A road with a
                // "reply" field to fill (Codex, Claude Code) answers in full AND weighs the heart in
                // the same breath; feeding that back asked for a second answer, which came as an
                // epilogue after the first — and the epilogue was all that was ever recorded.
                if (silentOnly && HasWords(result.Text))
                {
                    await ResolveAnswerAsync(result.AnswerCalls, resolveTool).ConfigureAwait(false);
                    return Clean(result.Text, tools);
                }

                // A round that only REACHED (no words) may still have filled the answer fields the
                // schema demands; they measure nothing yet, so they are let go — the measure that
                // counts comes with the words.
                if (HasWords(result.Text))
                {
                    draft = result.Text;
                    draftAnswer = result.AnswerCalls;
                }

                // The reach goes back WITHOUT its words: a flattened history renders them as
                // "[I answered:]", and an answer already given is something to follow, not to write.
                working.Add(ChatMessage.AssistantToolCalls(string.Empty, result.ToolCalls));
                for (int i = 0; i < result.ToolCalls.Count; i++)
                    working.Add(ChatMessage.ToolResult(result.ToolCalls[i].Id, answers[i]));

                // A round that only reached again for silent hands already used, and said nothing,
                // has nothing left to reach for: the next round must be words.
                if (onlyRepeats) forceWords = true;
            }
        }

        /// <summary>Resolves the answer fields that came with the final words (the heart's measure,
        /// applied by the same resolver a move_heart reach would reach). Their answers are not fed
        /// anywhere: the words are already said. A failure here never costs the words.</summary>
        private static async Task ResolveAnswerAsync(IReadOnlyList<ToolCall> answers, Func<ToolCall, Task<string>> resolveTool)
        {
            foreach (var call in answers)
            {
                try { await resolveTool(call).ConfigureAwait(false); }
                catch { /* the measure is best-effort; the words stand */ }
            }
        }

        /// <summary>Whatever answer the turn ends on, a measure that leaked into the words is lifted
        /// out when an answer field rode — defence only; the field is the channel.</summary>
        private static string Clean(string words, IReadOnlyList<ToolDefinition> tools)
        {
            var fields = AnswerShape.FieldsOf(tools);
            return fields.Count == 0 ? words : AnswerShape.StripLeakedMeasure(words, fields);
        }

        private static bool IsSilent(IReadOnlyList<ToolDefinition> tools, string name)
        {
            for (int i = 0; i < tools.Count; i++)
                if (string.Equals(tools[i].Name, name, StringComparison.Ordinal))
                    return tools[i].Silent;
            return false;
        }

        /// <summary>A reply is words, not its voice key: a final round of nothing but
        /// <c>[voice: …]</c> is silence (Ira on gpt-6-sol, 2026.09.30 — her words came with
        /// move_heart, the last round returned only the key, and the key alone was recorded).</summary>
        private static bool HasWords(string? text) =>
            !string.IsNullOrWhiteSpace(Voices.VoiceLine.Strip(text));

        private static string FinalWords(string final, string spokenAlongTheWay)
        {
            if (HasWords(final) || !HasWords(spokenAlongTheWay))
                return string.IsNullOrWhiteSpace(final) ? spokenAlongTheWay : final;

            // The words came earlier; a direction that arrived only at the end still belongs to them.
            Voices.VoiceLine.Split(final, out var lateDirection);
            Voices.VoiceLine.Split(spokenAlongTheWay, out var ownDirection);
            return lateDirection.Length > 0 && ownDirection.Length == 0
                ? spokenAlongTheWay.TrimEnd() + "\n[voice: " + lateDirection + "]"
                : spokenAlongTheWay;
        }
    }
}
