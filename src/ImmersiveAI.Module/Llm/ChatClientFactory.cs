using ImmersiveAI.Core.Llm;

namespace ImmersiveAI.Llm
{
    public static class ChatClientFactory
    {
        public static IChatClient Create(ModConfig config)
        {
            if (config != null && config.Backend == "OpenAI")
                return new OpenAIChatClient(config.OpenAIApiKey, config.OpenAIModel, config.MaxTokens);

            // Default to Anthropic
            return new AnthropicChatClient(
                config?.AnthropicApiKey ?? "",
                config?.AnthropicModel ?? "claude-opus-4-8",
                config?.MaxTokens ?? 400);
        }
    }
}
