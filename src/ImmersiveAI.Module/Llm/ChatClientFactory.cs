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
                return new OpenAIChatClient(config.OpenAIApiKey, config.OpenAIModel, maxTokens, config.OpenAIReasoningEffort);

            // Default to Anthropic
            return new AnthropicChatClient(
                config?.AnthropicApiKey ?? "",
                config?.AnthropicModel ?? "claude-opus-4-8",
                maxTokens);
        }
    }
}
