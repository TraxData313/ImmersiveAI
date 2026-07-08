using System;
using System.IO;
using Newtonsoft.Json;

namespace ImmersiveAI
{
    /// <summary>
    /// Mod configuration, stored as JSON under the Bannerlord Documents config folder.
    /// A commented template is written on first run so the user can paste in an API key.
    /// (An in-game MCM settings screen is planned for a later milestone.)
    /// </summary>
    public sealed class ModConfig
    {
        public string Backend { get; set; } = "Anthropic"; // "Anthropic" or "OpenAI"

        public string AnthropicApiKey { get; set; } = "";
        public string AnthropicModel { get; set; } = "claude-opus-4-8";

        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-4o";

        public int MaxTokens { get; set; } = 400;

        /// <summary>When true, the NPC opens each conversation by greeting the player and recapping
        /// what it remembers of them and the last exchange. Set false to drop straight into the menu.</summary>
        public bool EnableConversationRecap { get; set; } = true;

        /// <summary>How many verbatim turns an NPC keeps before old ones are compressed into the summary.</summary>
        public int MaxRecentTurns { get; set; } = 30;

        /// <summary>How many of the newest turns stay verbatim after a compression pass.</summary>
        public int KeepRecentTurnsAfterCompression { get; set; } = 15;

        /// <summary>How many in-game days of verbatim turns an NPC keeps before old ones are compressed.</summary>
        public int MaxRecentDays { get; set; } = 30;

        /// <summary>How many in-game days of newest turns stay verbatim after a compression pass.</summary>
        public int KeepRecentDaysAfterCompression { get; set; } = 15;

        /// <summary>Percent of the selected model's context window allowed for verbatim recent memory before compression starts.</summary>
        public int MaxRecentMemoryPercent { get; set; } = 10;

        /// <summary>Percent of the selected model's context window kept verbatim after compression.</summary>
        public int MinRecentMemoryPercentAfterCompression { get; set; } = 5;

        /// <summary>Estimated recent-memory token ceiling, derived from MaxRecentMemoryPercent and the selected model.</summary>
        public int MaxRecentMemoryTokens { get; set; } = 0;

        /// <summary>Estimated recent-memory token target after compression, derived from MinRecentMemoryPercentAfterCompression and the selected model.</summary>
        public int MinRecentMemoryTokensAfterCompression { get; set; } = 0;

        public static string ConfigDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Configs", "ImmersiveAI");

        public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

        public static ModConfig LoadOrCreate()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var loaded = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(ConfigFilePath));
                    if (loaded != null)
                    {
                        loaded.Normalize();
                        File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(loaded, Formatting.Indented));
                        return loaded;
                    }
                }

                var fresh = new ModConfig();
                fresh.Normalize();
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(fresh, Formatting.Indented));
                return fresh;
            }
            catch
            {
                return new ModConfig();
            }
        }

        public void Normalize()
        {
            if (MaxRecentTurns <= 0) MaxRecentTurns = 30;
            if (KeepRecentTurnsAfterCompression <= 0) KeepRecentTurnsAfterCompression = 15;
            if (MaxRecentDays <= 0) MaxRecentDays = 30;
            if (KeepRecentDaysAfterCompression <= 0) KeepRecentDaysAfterCompression = 15;

            var profile = MemoryTokenProfile.Resolve(this);
            if (MaxRecentMemoryPercent <= 0) MaxRecentMemoryPercent = profile.DefaultMaxRecentMemoryPercent;
            if (MinRecentMemoryPercentAfterCompression <= 0)
                MinRecentMemoryPercentAfterCompression = profile.DefaultMinRecentMemoryPercentAfterCompression;

            MaxRecentMemoryPercent = Clamp(
                MaxRecentMemoryPercent,
                MemorySettingsMetadata.MinMemoryPercent,
                MemorySettingsMetadata.MaxMemoryPercent);

            MinRecentMemoryPercentAfterCompression = Clamp(
                MinRecentMemoryPercentAfterCompression,
                MemorySettingsMetadata.MinMemoryPercent,
                MemorySettingsMetadata.MaxMemoryPercent);

            if (MinRecentMemoryPercentAfterCompression >= MaxRecentMemoryPercent)
                MinRecentMemoryPercentAfterCompression = Math.Max(
                    MemorySettingsMetadata.MinMemoryPercent,
                    MaxRecentMemoryPercent / 2);

            MaxRecentMemoryTokens = profile.GetMaxRecentMemoryTokens(MaxRecentMemoryPercent);
            MinRecentMemoryTokensAfterCompression =
                profile.GetMinRecentMemoryTokensAfterCompression(MinRecentMemoryPercentAfterCompression);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
