using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using TaleWorlds.Library;

namespace ImmersiveAI.Llm
{
    /// <summary>
    /// The client the rest of the mod actually holds: a thin shell that builds the real backend
    /// client lazily and REBUILDS it the moment the connection settings change — backend, key,
    /// model, endpoint, output budget. This is what makes an MCM (or config.json-reload) edit take
    /// effect on the very next call instead of asking for a restart: the behavior's readonly
    /// <c>_client</c> field never changes, only the inner client behind it does. A call already in
    /// flight on the old inner client simply finishes there; nothing is torn down under it.
    /// </summary>
    public sealed class LiveSwapChatClient : IToolChatClient
    {
        private readonly ModConfig _config;
        private readonly Func<int?> _maxTokensOverride;
        private readonly object _gate = new object();
        private IChatClient? _inner;
        private string _signature = string.Empty;

        // The connection as it stood at the last build — so a rebuild caused only by a changed
        // token budget stays silent (see Inner()).
        private string _connectionSignature = string.Empty;

        internal LiveSwapChatClient(ModConfig config, Func<int?> maxTokensOverride)
        {
            _config = config;
            _maxTokensOverride = maxTokensOverride;
        }

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Inner().CompleteAsync(messages, cancellationToken);

        public Task<ChatResult> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool allowToolUse = true,
            CancellationToken cancellationToken = default)
        {
            // Both real backends carry tools; the guard only matters if a tool-less client ever
            // joins the factory — then the loop's opening probe (this shell IS IToolChatClient)
            // would lie, so answer with a plain spoken completion instead of a crash.
            if (Inner() is IToolChatClient toolClient)
                return toolClient.CompleteWithToolsAsync(messages, tools, allowToolUse, cancellationToken);
            return SpokenOnlyAsync(messages, cancellationToken);
        }

        private async Task<ChatResult> SpokenOnlyAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct)
            => new ChatResult(await Inner().CompleteAsync(messages, ct).ConfigureAwait(false), Array.Empty<ToolCall>());

        private IChatClient Inner()
        {
            var signature = Signature();
            lock (_gate)
            {
                if (_inner != null && string.Equals(signature, _signature, StringComparison.Ordinal))
                    return _inner;

                // A rebuild whose CONNECTION is unchanged says nothing: the memory-writing budget
                // now grows with a bond (2026.08.27), so the shell is rebuilt often and honestly,
                // but "now speaking with <backend>" is news about the connection alone — announcing
                // it every compression would be noise the player cannot act on.
                bool isSwap = _inner != null
                    && !string.Equals(ConnectionSignature(), _connectionSignature, StringComparison.Ordinal);
                _inner = ChatClientFactory.Build(_config, _maxTokensOverride());
                _signature = signature;
                _connectionSignature = ConnectionSignature();
                if (isSwap) AnnounceSwap();
                return _inner;
            }
        }

        /// <summary>Everything the concrete clients capture in their constructors. When any of it
        /// changes, the next call speaks through a freshly built client.</summary>
        private string Signature() =>
            ConnectionSignature() + "|" + (_maxTokensOverride()?.ToString() ?? "")
            + "|" + _config.MaxTokens.ToString();

        /// <summary>The connection alone — everything a "now speaking with…" notice is actually
        /// about. Kept apart from the token budgets (2026.08.27) so a purpose-sized budget may move
        /// freely without telling the player the backend changed when it did not.</summary>
        private string ConnectionSignature()
        {
            var c = _config;
            return string.Join("",
                c.Backend,
                c.AnthropicApiKey, c.AnthropicModel,
                c.OpenAIApiKey, c.OpenAIModel, c.OpenAIBaseUrl,
                c.OpenRouterApiKey, c.OpenRouterModel,
                c.GeminiApiKey, c.GeminiModel,
                c.DeepSeekApiKey, c.DeepSeekModel,
                c.ClaudeCodeModel, c.ClaudeCodePath,
                c.LocalEndpoint, c.LocalModel, c.LocalApiKey);
        }

        // A soft one-liner so the player knows the settings change truly took hold — the quiet
        // answer to "do I have to restart?". Marshaled: a rebuild can trigger on any thread.
        private void AnnounceSwap()
        {
            try
            {
                var model = ModelInUse();
                MainThreadDispatcher.Enqueue(() => InformationManager.DisplayMessage(new InformationMessage(
                    $"Immersive AI: now speaking with {_config.Backend} · {model}.")));
            }
            catch { /* the notice is a nicety */ }
        }

        private string ModelInUse()
        {
            switch (_config.Backend)
            {
                case "OpenAI": return _config.OpenAIModel;
                case "OpenRouter": return _config.OpenRouterModel;
                case "Gemini": return _config.GeminiModel;
                case "DeepSeek": return _config.DeepSeekModel;
                case "ClaudeCode": return _config.ClaudeCodeModel + " (your Claude plan)";
                case "Local": return string.IsNullOrWhiteSpace(_config.LocalModel) ? "the loaded local model" : _config.LocalModel;
                default: return _config.AnthropicModel;
            }
        }
    }
}
