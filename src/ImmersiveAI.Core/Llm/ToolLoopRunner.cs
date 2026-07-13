using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// Runs one spoken turn that may reach for tools along the way: complete → resolve any tool
    /// calls → hand the answers back → repeat, until the model speaks plainly or the recall budget
    /// is spent (the last round forbids new calls, so the turn always ends in words). Backends that
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
            // Some models speak their words IN the tool-calling round and stay silent in the
            // forced final one (haiku beside move_heart — the "..." greeting, 2026.07.13).
            // Those words are the real reply; keep the latest and use them if the end is silence.
            string spokenAlongTheWay = "";
            for (int round = 0; ; round++)
            {
                bool allowToolUse = round < maxToolRounds;
                var result = await toolClient
                    .CompleteWithToolsAsync(working, tools, allowToolUse, cancellationToken)
                    .ConfigureAwait(false);

                if (!result.WantsTools || !allowToolUse)
                    return string.IsNullOrWhiteSpace(result.Text) ? spokenAlongTheWay : result.Text;

                if (!string.IsNullOrWhiteSpace(result.Text))
                    spokenAlongTheWay = result.Text;

                working.Add(ChatMessage.AssistantToolCalls(result.Text, result.ToolCalls));
                foreach (var call in result.ToolCalls)
                {
                    string answer;
                    try { answer = await resolveTool(call).ConfigureAwait(false); }
                    catch { answer = NothingSurfaces; }
                    if (string.IsNullOrWhiteSpace(answer)) answer = NothingSurfaces;

                    working.Add(ChatMessage.ToolResult(call.Id, answer));
                }
            }
        }
    }
}
