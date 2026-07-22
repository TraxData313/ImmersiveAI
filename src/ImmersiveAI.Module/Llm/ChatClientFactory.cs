using System;
using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Llm
{
    public static class ChatClientFactory
    {
        /// <summary>Builds the chat client for the configured backend — a live shell that rebuilds
        /// its inner client whenever the connection settings change, so an MCM edit (backend, key,
        /// model, endpoint) takes hold on the next call without a restart. An explicit
        /// <paramref name="maxTokensOverride"/> lets a caller give one purpose its own output budget —
        /// e.g. memory writing (reflection/compression) gets more room than a spoken reply.</summary>
        public static IChatClient Create(ModConfig config, int? maxTokensOverride = null)
            => new LiveSwapChatClient(config, () => maxTokensOverride);

        /// <summary>Same, with a LIVE output budget: re-read at every (re)build, so a budget that
        /// follows a config value (like the memory-writing room) keeps following it.</summary>
        public static IChatClient Create(ModConfig config, Func<int?> maxTokensOverride)
            => new LiveSwapChatClient(config, maxTokensOverride);

        /// <summary>The raw, settings-frozen build the shell wraps — one concrete client for the
        /// backend the config names right now.</summary>
        internal static IChatClient Build(ModConfig config, int? maxTokensOverride = null)
        {
            var maxTokens = maxTokensOverride ?? config?.MaxTokens ?? 400;

            if (config != null && config.Backend == "OpenAI")
                return new OpenAIChatClient(config.OpenAIApiKey, config.OpenAIModel, maxTokens, config.OpenAIBaseUrl);

            // OpenRouter: the same OpenAI-shaped client pointed at the router's fixed door —
            // one key there reaches GPT and Claude models alike (ids like "openai/gpt-5.4-mini").
            if (config != null && config.Backend == "OpenRouter")
                return new OpenAIChatClient(config.OpenRouterApiKey, config.OpenRouterModel, maxTokens,
                    ModConfig.OpenRouterEndpoint, "OpenRouter");

            // Local: the same client speaking to a server on the player's own machine (LM Studio,
            // Ollama, llama.cpp). Keyless is normal there; errors name "Local AI" so a dead server
            // never sends anyone checking a cloud account.
            if (config != null && config.Backend == "Local")
                return new OpenAIChatClient(config.LocalApiKey, config.LocalModel, maxTokens,
                    config.LocalEndpoint, "Local AI", isLocal: true);

            // Default to Anthropic
            return new AnthropicChatClient(
                config?.AnthropicApiKey ?? "",
                config?.AnthropicModel ?? "claude-haiku-4-5",
                maxTokens);
        }
    }
}
