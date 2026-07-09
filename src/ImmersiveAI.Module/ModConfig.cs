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

        /// <summary>When true, the NPC may set — in character, however they truly feel — how each exchange
        /// moves their regard for the player, and that shift is folded into the real game standing
        /// (clamped to -100..100). Set false to leave relations untouched by conversation.</summary>
        public bool EnableRelationshipChanges { get; set; } = true;

        /// <summary>When true, NPCs who are in the same place as the player may reach out to them of their
        /// own accord: each such NPC's daily chance is scaled by how close the bond is (see
        /// <see cref="DailyInitiationRate"/>), and if one is moved to, they are privately asked whether they
        /// truly wish to — and only then does the player get a ransom-style offer to receive them or not.</summary>
        public bool EnableNpcInitiatedChats { get; set; } = true;

        /// <summary>The daily reaching-out chance for a FULL-BLOWN bond — someone the player speaks with
        /// often and holds at a strong standing (love or enmity). Every actual NPC's chance is this scaled
        /// down by how much they talk and how far their standing is from indifference, so a fresh game stays
        /// quiet while a devoted, frequent companion may write nearly every day. 0.3 ≈ a maxed bond reaching
        /// out ~30% of days; raise toward ~1.5 to let the closest bonds write daily. 0 disables it (as does
        /// <see cref="EnableNpcInitiatedChats"/>). Clamped to a sane ceiling in <see cref="Normalize"/>.</summary>
        public double DailyInitiationRate { get; set; } = 0.3;

        /// <summary>When true, the accept/reject offer that appears when an NPC reaches out pauses the game
        /// while it is up, so the player can always stop and decide (otherwise, at fast-forward the moment
        /// can slip by). Set false to let time keep flowing while the offer waits. The eventual right-side
        /// portrait map-notice (a future UI task) will make this moot.</summary>
        public bool PauseOnInitiationOffer { get; set; } = true;

        /// <summary>When true, a "[Immersive AI • test]" option appears in the free-chat menu that makes the
        /// NPC you are speaking with reach out to you right after you part — a way to exercise the
        /// initiation flow on demand instead of waiting on the daily odds. Set false to hide it.</summary>
        public bool ShowInitiationTestButton { get; set; } = true;

        /// <summary>The in-fiction name of the "System" voice that addresses an NPC directly when the
        /// mod asks them to do something out-of-conversation (e.g. decide what to remember or forget
        /// when their memory is compressed). Treats each NPC as an individual rather than a data store.</summary>
        public string SystemVoiceName { get; set; } = "Angel";

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
            if (string.IsNullOrWhiteSpace(SystemVoiceName)) SystemVoiceName = "Angel";

            // Keep the daily rate non-negative and under one-per-hour, so a fat-fingered value can't have
            // every NPC hammering the player. 24 is already far more than anyone would want.
            if (DailyInitiationRate < 0 || double.IsNaN(DailyInitiationRate)) DailyInitiationRate = 0;
            if (DailyInitiationRate > 24) DailyInitiationRate = 24;

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
