using System;
using System.Globalization;
using System.Linq;
using ImmersiveAI.Core.Memory;

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
                : backend == "Gemini" ? config?.GeminiModel
                : backend == "DeepSeek" ? config?.DeepSeekModel
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

        /// <summary>How heavy one bond's verbatim memory has grown, in the model's own coin — shown in
        /// the chat window so the moment before a compression is never a surprise (Anton's ask,
        /// 2026.08.08). Every number here is a live compression trigger: the share of the context
        /// window, the turn count, and the age of the oldest kept turn, each against its own ceiling,
        /// closing with where a compression would leave things. Best-effort by nature — the token
        /// figure is the same estimate the trigger itself uses, not the API's own count.</summary>
        public static string MemoryLoadLabel(ModConfig config, NpcMemory memory, double currentGameDay)
        {
            if (config == null) return string.Empty;
            var turns = memory?.RecentTurns;
            var tokens = turns == null ? 0 : MemoryTokenEstimator.EstimateRecentTurnsTokens(turns);
            var profile = Resolve(config);
            var maxTokens = config.MaxRecentMemoryTokens > 0
                ? config.MaxRecentMemoryTokens
                : profile.GetMaxRecentMemoryTokens(config.MaxRecentMemoryPercent);
            var percent = profile.ContextTokens > 0 ? tokens * 100.0 / profile.ContextTokens : 0.0;

            var text = string.Format(CultureInfo.InvariantCulture,
                "memory {0:0.#}% / {1}% · {2} / {3} tokens · turns {4}/{5}",
                percent, config.MaxRecentMemoryPercent,
                Amount(tokens), Amount(maxTokens),
                turns?.Count ?? 0, config.MaxRecentTurns);

            // The oldest kept turn's age is the third trigger. Turns recorded before the day was
            // stamped (very old saves) carry day 0 — their "age" would be the whole campaign, so
            // the segment simply stays off rather than lying.
            var oldest = turns != null && turns.Count > 0 ? turns.Min(t => t.GameDay) : 0.0;
            if (oldest > 0.0 && currentGameDay > 0.0 && config.MaxRecentDays > 0)
                text += string.Format(CultureInfo.InvariantCulture, " · oldest {0:0.#}d/{1}d",
                    Math.Max(0.0, currentGameDay - oldest), config.MaxRecentDays);

            return text + string.Format(CultureInfo.InvariantCulture, " · trims back to {0}%",
                config.MinRecentMemoryPercentAfterCompression);
        }

        /// <summary>A token count in a glanceable hand: 21.4k, 1.2M, 840.</summary>
        private static string Amount(int value)
        {
            if (value >= 1000000) return (value / 1000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            if (value >= 1000) return (value / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
