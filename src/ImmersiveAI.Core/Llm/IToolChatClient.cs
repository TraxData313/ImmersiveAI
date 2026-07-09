using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// A chat backend that can also offer the model tools (native tool/function calling).
    /// Kept as a separate interface so plain <see cref="IChatClient"/> implementations and
    /// test fakes keep working unchanged; callers that want tools probe for this and fall
    /// back to a plain completion when the backend cannot provide them.
    /// </summary>
    public interface IToolChatClient : IChatClient
    {
        /// <summary>
        /// One completion with tools on offer. <paramref name="allowToolUse"/> false still sends the
        /// tool definitions (backends require them to validate a history that already contains tool
        /// calls) but forbids new calls — used to force a final spoken answer when the recall budget
        /// is spent.
        /// </summary>
        Task<ChatResult> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool allowToolUse = true,
            CancellationToken cancellationToken = default);
    }
}
