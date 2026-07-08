using System;

namespace ImmersiveAI
{
    public sealed class MemoryTokenProfile
    {
        public int ContextTokens { get; }
        public int DefaultMinRecentMemoryPercentAfterCompression { get; }
        public int DefaultMaxRecentMemoryPercent { get; }
        public int MinRecentMemoryTokensAfterCompression { get; }
        public int MaxRecentMemoryTokens { get; }

        private MemoryTokenProfile(
            int contextTokens,
            int defaultMinRecentMemoryPercentAfterCompression = 5,
            int defaultMaxRecentMemoryPercent = 10)
        {
            ContextTokens = contextTokens;
            DefaultMinRecentMemoryPercentAfterCompression = defaultMinRecentMemoryPercentAfterCompression;
            DefaultMaxRecentMemoryPercent = defaultMaxRecentMemoryPercent;
            MinRecentMemoryTokensAfterCompression = PercentToTokens(contextTokens, defaultMinRecentMemoryPercentAfterCompression);
            MaxRecentMemoryTokens = PercentToTokens(contextTokens, defaultMaxRecentMemoryPercent);
        }

        public int GetMinRecentMemoryTokensAfterCompression(int percent)
        {
            return PercentToTokens(ContextTokens, percent);
        }

        public int GetMaxRecentMemoryTokens(int percent)
        {
            return PercentToTokens(ContextTokens, percent);
        }

        public static int PercentToTokens(int contextTokens, int percent)
        {
            if (contextTokens <= 0) return 0;
            if (percent <= 0) return 0;
            return Math.Max(1, (int)Math.Round(contextTokens * (percent / 100.0)));
        }

        public static MemoryTokenProfile Resolve(ModConfig config)
        {
            var backend = config?.Backend ?? "Anthropic";
            var model = backend == "OpenAI"
                ? config?.OpenAIModel ?? ""
                : config?.AnthropicModel ?? "";

            model = model.ToLowerInvariant();

            if (backend == "OpenAI")
            {
                if (model.Contains("4o") || model.Contains("gpt-5"))
                    return new MemoryTokenProfile(128000);

                return new MemoryTokenProfile(128000);
            }

            if (model.Contains("haiku"))
                return new MemoryTokenProfile(200000);

            if (model.Contains("sonnet"))
                return new MemoryTokenProfile(200000);

            if (model.Contains("opus"))
                return new MemoryTokenProfile(200000);

            return new MemoryTokenProfile(128000);
        }
    }
}
