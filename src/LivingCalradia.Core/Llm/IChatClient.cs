using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LivingCalradia.Core.Llm
{
    /// <summary>
    /// Abstraction over an LLM backend. Implementations (Anthropic, OpenAI-compatible, local)
    /// live outside Core so this library stays free of HTTP/game dependencies and fully testable.
    /// </summary>
    public interface IChatClient
    {
        Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
    }
}
