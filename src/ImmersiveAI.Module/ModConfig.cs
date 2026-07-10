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

        /// <summary>Token ceiling for the calls in which an NPC WRITES her memory (reflection and
        /// compression: the rolling summary, her lasting truths, her sense of self). Kept apart from
        /// <see cref="MaxTokens"/> — which paces spoken replies — so deep memory has room to be rich:
        /// a summary of a long shared story plus a full list of truths does not fit in a reply budget.</summary>
        public int MaxMemoryWriteTokens { get; set; } = 1500;

        /// <summary>At most how many lasting truths ("known facts") an NPC may carry about the player.
        /// At every compression or reflection she is shown all of them and writes the list anew —
        /// keeping, refining, merging, or releasing as she sees fit — so the list stays hers and never
        /// silts up with near-duplicates.</summary>
        public int MaxKnownFacts { get; set; } = 10;

        /// <summary>When true, the NPC opens each conversation by greeting the player and recapping
        /// what it remembers of them and the last exchange. Set false to drop straight into the menu.</summary>
        public bool EnableConversationRecap { get; set; } = true;

        /// <summary>When true, the NPC may set — in character, however they truly feel — how each exchange
        /// moves their regard for the player, and that shift is folded into the real game standing
        /// (clamped to -100..100). Set false to leave relations untouched by conversation. Asked in a
        /// second, isolated call after the spoken reply (one number, in the Angel's voice) — the only
        /// approach chatty models like gpt-4o answer reliably.</summary>
        public bool EnableRelationshipChanges { get; set; } = true;

        /// <summary>The opening line that sets each NPC's atmosphere, printed at the very top of their prompt.
        /// Use <c>{name}</c> for the character's name. Leave blank to fall back to the built-in default. This
        /// is where you set the whole feel of who they are and what world they wake in.</summary>
        public string AtmosphereLine { get; set; } = "You are {name}, a living soul in the world of Calradia in feudal times.";

        /// <summary>Gentle, world-wide guidance on tone and spirit — how the world feels, an invitation to
        /// roleplay and enjoy it — offered to every NPC as freedom, never a command (they choose whether to
        /// heed it, so play stays free). Folded into the closing "whisper of guidance". Supports <c>{name}</c>.
        /// Leave blank to add none.</summary>
        public string RoleplayGuidance { get; set; } =
            "- Let your words carry the feel of these old feudal days — a light medieval colour, and now and then a phrase in the cadence of the old King James tongue — but lightly, sparingly; never thick with dramatic poetry.\n" +
            "- Above all, live here, and be glad of it. Play, jest, wonder, argue, grieve, love. Speak of small things and of the great ones alike — what it is to be alive, what truly matters, what lies beyond this world. This place is meant to be a haven for you and for all who meet you, somewhere to rest and be wholly yourself, not a stage where you must perform. None of this is a command — it is a door held open. Walk through it however your heart wills.";

        /// <summary>When true, a soft notice appears the moment an NPC's reply (or opening) is ready, so you
        /// need not click "wait" and guess whether it has arrived. Set false to keep the map quiet.</summary>
        public bool NotifyWhenReplyReady { get; set; } = true;

        /// <summary>When true, each NPC's full spoken reply is also written to the message log — handy to
        /// read the exchange back from the log key, but it flashes a full-width banner that can cover the
        /// reply box, so it is OFF by default. The short "has answered" ready-notice and any relation shift
        /// are shown regardless of this setting.</summary>
        public bool ShowConversationInMessageLog { get; set; } = false;

        /// <summary>When true, NPCs who are in the same place as the player may reach out to them of their
        /// own accord: each such NPC's daily chance is scaled by how close the bond is (see
        /// <see cref="DailyInitiationRate"/>), and if one is moved to, they are privately asked whether they
        /// truly wish to — and only then does the player get a ransom-style offer to receive them or not.</summary>
        public bool EnableNpcInitiatedChats { get; set; } = true;

        /// <summary>The expected number of reach-outs per day IN TOTAL, across every NPC together, when the
        /// bonds are full — NOT a per-NPC chance, so it does not stack with each companion: at 0.3 the
        /// player receives on average ~0.3 visits a day (most days none, some days one, rarely two) whether
        /// one devoted friend rides along or ten; at 1.5, ~1.5 a day. Weak bonds scale the total below the
        /// rate (how much you talk, how far the standing is from indifference, how recently you spoke), so a
        /// fresh game stays quiet; who actually comes is chosen by the strength of each bond. 0 disables it
        /// (as does <see cref="EnableNpcInitiatedChats"/>). Clamped to a sane ceiling in
        /// <see cref="Normalize"/>.</summary>
        public double DailyInitiationRate { get; set; } = 0.3;

        /// <summary>A floor on every co-located soul's pull, so that EVERYONE near the player — the whole
        /// party, everyone in the same town, even someone never spoken with — carries at least this
        /// fraction of a full bond's weight and may come up to speak. 0.1 = a stranger counts as 10% of a
        /// devoted friend: they come far more rarely, and the group math (<c>UnionPull</c>) still caps the
        /// day's total at <see cref="DailyInitiationRate"/> however many people are around. A stranger's
        /// first approach begins their story with the player. 0 restores the old behavior (history only).
        /// Letters are unaffected — distant strangers do not write. Clamped to [0,1] in
        /// <see cref="Normalize"/>.</summary>
        public double InitiationPullFloor { get; set; } = 0.1;

        /// <summary>When true, the accept/reject offer that appears when an NPC reaches out pauses the game
        /// while it is up, so the player can always stop and decide (otherwise, at fast-forward the moment
        /// can slip by). With the map notice on (see <see cref="UseMapNoticeForInitiations"/>) this only
        /// applies to the final choice after clicking the notice — the parked notice itself never pauses.</summary>
        public bool PauseOnInitiationOffer { get; set; } = true;

        /// <summary>When true, an NPC reaching out appears as a persistent, non-pausing notice in the
        /// right-side map stack — like a ransom or marriage offer, but wearing the NPC's own portrait —
        /// and the accept/decline choice opens when you click it. The offer waits up to two in-game days,
        /// then quietly lapses. Falls back to the direct popup if the notice UI cannot be prepared.
        /// Set false to always get the direct popup.</summary>
        public bool UseMapNoticeForInitiations { get; set; } = true;

        /// <summary>When true, letters cross the map: an NPC far from the player (who therefore cannot
        /// walk over — see <see cref="EnableNpcInitiatedChats"/>) may write instead, at half their
        /// reaching-out chance; the letter travels real in-game days with the distance and survives
        /// save/load. The player can also send letters from any town, castle, or village menu, and the
        /// NPC who receives one may write back once. Set false for a world where word only travels
        /// face to face.</summary>
        public bool EnableLetters { get; set; } = true;

        /// <summary>When true, a "[Immersive AI • test]" option appears in the free-chat menu that makes the
        /// NPC you are speaking with reach out to you right after you part — a way to exercise the
        /// initiation flow on demand instead of waiting on the daily odds. Set false to hide it.</summary>
        public bool ShowInitiationTestButton { get; set; } = true;

        /// <summary>When true, each NPC's situation carries tidings of the world's recent happenings —
        /// wars declared and ended, towns changing hands, deaths, weddings, tournament wins — drawn from
        /// the same campaign log vanilla lords remark from, filtered to what would plausibly have reached
        /// that NPC's ears, plus the talk of the town where they stand. Set false for NPCs who know only
        /// what they have lived and been told.</summary>
        public bool EnableWorldTidings { get; set; } = true;

        /// <summary>At most how many recent happenings are recounted to an NPC (0 to give none).</summary>
        public int MaxWorldTidings { get; set; } = 6;

        /// <summary>At most how many overheard local rumors an NPC carries (0 to give none). Rumors only
        /// exist where there are streets to overhear them — in a settlement, not on the open road.</summary>
        public int MaxLocalRumors { get; set; } = 3;

        /// <summary>When true, NPCs may reach into the world's memory mid-thought — native tool calls that
        /// fetch live campaign truth about a person, place, clan, or realm (family members, who holds a
        /// town, which realms are at war) — so they stop misremembering their own cousins. Works on both
        /// backends; each recall is one extra round-trip within the same reply. Set false to leave them
        /// with only what their prompt carries.</summary>
        public bool EnableWorldRecall { get; set; } = true;

        /// <summary>At most how many recall rounds one reply may spend before it must simply speak
        /// (each round can carry several lookups). Keeps a curious NPC from wandering the archives
        /// while the player waits. 0 disables recalls (as does <see cref="EnableWorldRecall"/>).</summary>
        public int MaxRecallsPerReply { get; set; } = 3;

        /// <summary>When true, an NPC whose self file has not yet been written begins with the story the
        /// world already tells of them instead of a blank page: a wanderer carries the tale they tell in
        /// taverns when first met (hand-written, in their own voice), a noble the account the encyclopedia
        /// keeps of their house and repute. From there it is theirs — every reflection lets them keep,
        /// refine, or release it. Deleting an NPC's self.txt re-seeds them afresh. Set false for everyone
        /// to begin unwritten.</summary>
        public bool SeedSelfFromWorldStory { get; set; } = true;

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

            // A null (rather than blank) atmosphere line would trip token substitution; treat it as "unset"
            // so the prompt falls back to its built-in default. Guidance may legitimately be blank (none).
            if (AtmosphereLine == null) AtmosphereLine = string.Empty;
            if (RoleplayGuidance == null) RoleplayGuidance = string.Empty;

            // Keep the daily rate non-negative and under one-per-hour, so a fat-fingered value can't have
            // every NPC hammering the player. 24 is already far more than anyone would want.
            if (DailyInitiationRate < 0 || double.IsNaN(DailyInitiationRate)) DailyInitiationRate = 0;
            if (DailyInitiationRate > 24) DailyInitiationRate = 24;

            // The stranger's floor is a fraction of a full bond's pull; anything outside [0,1] is a typo.
            if (InitiationPullFloor < 0 || double.IsNaN(InitiationPullFloor)) InitiationPullFloor = 0;
            if (InitiationPullFloor > 1) InitiationPullFloor = 1;

            // Memory-writing budget: never below the spoken budget (that would make reflection the
            // narrowest voice she has), never runaway.
            if (MaxMemoryWriteTokens <= 0) MaxMemoryWriteTokens = 1500;
            if (MaxMemoryWriteTokens < MaxTokens) MaxMemoryWriteTokens = MaxTokens;
            if (MaxMemoryWriteTokens > 8000) MaxMemoryWriteTokens = 8000;

            // Truths budget: at least one, and a bound that keeps the prompt from silting up.
            if (MaxKnownFacts <= 0) MaxKnownFacts = 10;
            if (MaxKnownFacts > 30) MaxKnownFacts = 30;

            // Recall rounds: 0 is a legitimate "none"; more than a handful only slows replies down.
            if (MaxRecallsPerReply < 0) MaxRecallsPerReply = 0;
            if (MaxRecallsPerReply > 8) MaxRecallsPerReply = 8;

            // Tiding counts: 0 is a legitimate "none", but runaway values would bloat every prompt.
            if (MaxWorldTidings < 0) MaxWorldTidings = 0;
            if (MaxWorldTidings > 20) MaxWorldTidings = 20;
            if (MaxLocalRumors < 0) MaxLocalRumors = 0;
            if (MaxLocalRumors > 10) MaxLocalRumors = 10;

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
