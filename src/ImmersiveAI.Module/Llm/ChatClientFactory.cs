using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Llm
{
    public static class ChatClientFactory
    {
        /// <summary>Builds a chat client for the configured backend. An explicit
        /// <paramref name="maxTokensOverride"/> lets a caller give one purpose its own output budget —
        /// e.g. memory writing (reflection/compression) gets more room than a spoken reply.</summary>
        public static IChatClient Create(ModConfig config, int? maxTokensOverride = null)
        {
            var maxTokens = maxTokensOverride ?? config?.MaxTokens ?? 400;

            if (config != null && config.Backend == "OpenAI")
                return new OpenAIChatClient(config.OpenAIApiKey, config.OpenAIModel, maxTokens, config.OpenAIBaseUrl);

            // OpenRouter: the same OpenAI-shaped client pointed at the router's fixed door —
            // one key there reaches GPT and Claude models alike (ids like "openai/gpt-5.4-mini").
            if (config != null && config.Backend == "OpenRouter")
                return new OpenAIChatClient(config.OpenRouterApiKey, config.OpenRouterModel, maxTokens,
                    ModConfig.OpenRouterEndpoint, "OpenRouter");

            // Default to Anthropic
            return new AnthropicChatClient(
                config?.AnthropicApiKey ?? "",
                config?.AnthropicModel ?? "claude-haiku-4-5",
                maxTokens);
        }
    }
}
