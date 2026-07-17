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

        /// <summary>What any model without a table entry is assumed to hold — deliberately the
        /// smallest common window, so an unknown model is never over-promised memory room.</summary>
        public const int FallbackContextTokens = 128000;

        public static MemoryTokenProfile Resolve(ModConfig config)
        {
            var backend = config?.Backend ?? "Anthropic";

            // The Local backend's window is whatever the player's own server loads the model with —
            // declared in config (LocalContextWindow), never derivable from a model table: the same
            // model id can be served at 4k or 128k depending on the loading settings.
            if (backend == "Local")
            {
                var localWindow = config?.LocalContextWindow ?? 0;
                return new MemoryTokenProfile(localWindow > 0 ? localWindow : FallbackContextTokens);
            }

            var model = ((backend == "OpenAI" ? config?.OpenAIModel
                : backend == "OpenRouter" ? config?.OpenRouterModel
                : config?.AnthropicModel) ?? "").ToLowerInvariant();

            // The configured (user-editable) model table decides; the longest key contained in the
            // model id wins, so "gpt-5.1" beats "gpt-5" for gpt-5.1-mini and "claude" catches every
            // Anthropic id. Comparison is done here in lowercase so the table's comparer never
            // matters, whatever JSON round-tripping did to it.
            var table = config?.ModelContextWindows ?? ModConfig.DefaultModelContextWindows();
            int best = 0;
            int bestLength = -1;
            foreach (var pair in table)
            {
                if (pair.Value <= 0 || string.IsNullOrWhiteSpace(pair.Key)) continue;
                var key = pair.Key.Trim().ToLowerInvariant();
                if (key.Length > bestLength && model.Contains(key))
                {
                    best = pair.Value;
                    bestLength = key.Length;
                }
            }

            return new MemoryTokenProfile(best > 0 ? best : FallbackContextTokens);
        }
    }
}
