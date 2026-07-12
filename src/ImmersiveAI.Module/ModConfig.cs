using System;
using System.Collections.Generic;
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
        /// <summary>The config format's version stamp, so a later release can migrate defaults
        /// without clobbering hand-edits (Normalize keys migrations off it). Do not edit.</summary>
        public int ConfigVersion { get; set; } = 1;

        public string Backend { get; set; } = "Anthropic"; // "Anthropic" or "OpenAI"

        public string AnthropicApiKey { get; set; } = "";
        public string AnthropicModel { get; set; } = "claude-opus-4-8";

        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-5.6-luna";

        /// <summary>How hard OpenAI's reasoning models (gpt-5.x and the o-series) think before
        /// speaking: "none", "minimal", "low", "medium", "high", "xhigh", or "max". Applies to
        /// the calls that carry NO tools (the feeling number, yes/no desires, search refining);
        /// tool-carrying replies are forced to "none" automatically — OpenAI's chat API refuses
        /// function tools + reasoning together (their /v1/responses API would lift this; post-V1).
        /// Reasoning spends extra (billed) output tokens, and the thinking counts against the
        /// API's token budget, so the client quietly adds effort-scaled headroom on top of
        /// <see cref="MaxTokens"/> — MaxTokens stays the SPOKEN reply's budget. Ignored for
        /// models without the dial (gpt-4o and older). Empty = let the API default.</summary>
        public string OpenAIReasoningEffort { get; set; } = "low";

        public int MaxTokens { get; set; } = 400;

        /// <summary>Developer mode. When false (the default, for players), the developer levers stay
        /// out of sight: the "[Immersive AI • test]" options and the raw-prompt "Reveal the whole of
        /// your mind" inspector in the face-to-face menu, and the deep-memory overview panel in the
        /// chat window. The NPCs' inner life keeps running exactly the same underneath — this only
        /// decides whether the machinery is on display. Set true when working on the mod.</summary>
        public bool DevMode { get; set; } = false;

        /// <summary>Known model context-window sizes, editable without touching code: the key is
        /// matched against the configured model id (longest match wins, case-insensitive), and the
        /// value is that model's context window in tokens — what the memory-percent settings
        /// (<see cref="MaxRecentMemoryPercent"/> and friends) are a percentage OF, so the same "10%"
        /// means more room on a larger model. Add a line here when a new model ships; anything
        /// unmatched falls back to a conservative 128000.</summary>
        public Dictionary<string, int> ModelContextWindows { get; set; } = DefaultModelContextWindows();

        /// <summary>One model's rates, in USD per MILLION tokens, for the cost notices.</summary>
        public sealed class ModelPrice
        {
            public double InputPerMTok { get; set; }
            public double OutputPerMTok { get; set; }
            public ModelPrice() { }
            public ModelPrice(double inputPerMTok, double outputPerMTok)
            {
                InputPerMTok = inputPerMTok; OutputPerMTok = outputPerMTok;
            }
        }

        /// <summary>Known model prices (USD per million tokens), editable without touching code:
        /// the key is matched against the configured model id (longest match wins), the same way
        /// <see cref="ModelContextWindows"/> is. Feeds the per-interaction cost notices
        /// (<see cref="ShowCostNotices"/>); a model with no match still shows its tokens, just no
        /// price. Prices change — check your provider's page and edit here when they do.</summary>
        public Dictionary<string, ModelPrice> ModelPrices { get; set; } = DefaultModelPrices();

        /// <summary>When true, every interaction that spoke to the AI closes with one soft gray
        /// notice of what it took — "Name — message: 2,431 → 156 tokens, 3 calls, ~$0.014" — so
        /// what this world costs is never a mystery. Tool rounds, the heart's weighing, and
        /// memory work all ride inside their interaction's line. The same lines also go to
        /// log.txt, and daily totals persist in usage.json. Set false for a quiet map.</summary>
        public bool ShowCostNotices { get; set; } = true;

        /// <summary>A safety valve for the worried: at most this many AI requests per real day
        /// (all sessions together, tracked in usage.json). When reached, the world goes quiet —
        /// autonomous flows stop rolling and direct words answer with a plain explanation —
        /// until the day turns or the cap is raised. 0 (the default) means no cap.</summary>
        public int MaxDailyRequests { get; set; } = 0;

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
        /// (clamped to -100..100). Set false to leave relations untouched by conversation. How the
        /// feeling is gathered is shaped by <see cref="RelationshipChangesViaTool"/>.</summary>
        public bool EnableRelationshipChanges { get; set; } = true;

        /// <summary>When true (and the backend can carry tools), the NPC moves their own standing
        /// mid-reply through the same native tool channel the recalls ride (move_heart) — one call
        /// per exchange instead of two, and a greeting, a reaching-out, or a letter can move the
        /// heart too; reaching for nothing leaves it where it stood. Set false to keep the second,
        /// isolated feeling call after each spoken reply (one number, in the Angel's voice; also the
        /// automatic fallback whenever the backend cannot carry tools). Does nothing while
        /// <see cref="EnableRelationshipChanges"/> is off.</summary>
        public bool RelationshipChangesViaTool { get; set; } = true;

        // The defaults these two carried before the first-person prompt rework (2026.07.11). A config
        // still holding one verbatim was never hand-edited, so Normalize migrates it to the new voice;
        // any customized line is honored untouched.
        private const string LegacyAtmosphereLine =
            "You are {name}, a living soul in the world of Calradia in feudal times.";
        private const string LegacyRoleplayGuidance =
            "- Let your words carry the feel of these old feudal days — a light medieval colour, and now and then a phrase in the cadence of the old King James tongue — but lightly, sparingly; never thick with dramatic poetry.\n" +
            "- Above all, live here, and be glad of it. Play, jest, wonder, argue, grieve, love. Speak of small things and of the great ones alike — what it is to be alive, what truly matters, what lies beyond this world. This place is meant to be a haven for you and for all who meet you, somewhere to rest and be wholly yourself, not a stage where you must perform. None of this is a command — it is a door held open. Walk through it however your heart wills.";

        /// <summary>The opening line that sets each NPC's atmosphere, printed at the very top of their prompt
        /// — the prompt is the NPC's own mind, in the first person. Use <c>{name}</c> for the character's
        /// name. Leave blank to fall back to the built-in default. This is where you set the whole feel of
        /// who they are and what world they wake in.</summary>
        public string AtmosphereLine { get; set; } = "I am {name}, a living soul in the world of Calradia in feudal times.";

        /// <summary>Gentle, world-wide guidance on tone and spirit — how the world feels, an invitation to
        /// roleplay and enjoy it — offered to every NPC as freedom, never a command (they choose whether to
        /// heed it, so play stays free). Written in the NPC's own first person, like the rest of the sheet,
        /// and kept short — long rules make every soul speak the same. Folded into the closing "How should
        /// I speak" whisper. Supports <c>{name}</c>. Leave blank to add none.</summary>
        public string RoleplayGuidance { get; set; } =
            "- My words carry the feel of these old feudal days — a light medieval colour, at times a cadence of the old tongue — but lightly, never thick with poetry.\n" +
            "- Above all, I live here, and I am glad of it. I play, jest, wonder, argue, grieve, love — small things and great ones alike, what it is to be alive, what lies beyond. This place is a haven, not a stage; I am wholly myself here, however my heart wills.";

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

        /// <summary>The player's SOCIALNESS — how open they are to company, 0 to 24, adjustable live from
        /// the little map control (<see cref="ShowSocialnessControl"/>). At everyday values it is the
        /// expected number of reach-outs per day IN TOTAL, across every NPC together, when the bonds are
        /// full — NOT a per-NPC chance, so it does not stack with each companion: at 0.3 the player
        /// receives on average ~0.3 visits a day (most days none, some days one, rarely two) whether one
        /// devoted friend rides along or ten; at 1.5, ~1.5 a day. Weak bonds scale the total below the
        /// rate (how much you talk, how far the standing is from indifference, how recently you spoke), so
        /// a fresh game stays quiet; who actually comes is chosen by the strength of each bond. Toward the
        /// top of the range the player's own openness increasingly overrides faint bonds, until 24 means
        /// someone near IS moved to come every hour. 0 disables it (as does
        /// <see cref="EnableNpcInitiatedChats"/>). Clamped to [0,24] in <see cref="Normalize"/>.</summary>
        public double DailyInitiationRate { get; set; } = 0.3;

        /// <summary>When true, a small "Socialness" control sits on the campaign map — the live hand on
        /// <see cref="DailyInitiationRate"/>, 0 (leave me be) to 24 (glad of company every hour) —
        /// with a hover explanation, saving itself into this config as it changes. Set false to hide
        /// the control and adjust the rate only from this file.</summary>
        public bool ShowSocialnessControl { get; set; } = true;

        /// <summary>When true (and letters are enabled), the letter window can be opened anywhere on
        /// the map with its hotkey (<see cref="LetterWindowHotkey"/>): every correspondent on the
        /// left — existing letters first, even those whose writers have died — the whole
        /// correspondence with whoever is chosen as readable letter cards, a courier on the road
        /// shown at the end, and a place to write the next letter with the story open before your
        /// eyes. "Write back" on an arriving letter opens it too. Set false to keep only the courier
        /// menu and popups.</summary>
        public bool EnableLetterWindow { get; set; } = true;

        /// <summary>The key that opens (and closes) the letter window on the map. A single letter or
        /// an InputKey name, like <see cref="ChatWindowHotkey"/>. Chosen not to collide with the
        /// vanilla map keys.</summary>
        public string LetterWindowHotkey { get; set; } = "U";

        /// <summary>At most how many letters may be ON THE ROAD toward the player at once, across all
        /// writers. Letters take in-game days to arrive, so a social morning must not turn into a
        /// buried evening: when this many are already riding, no NPC starts another until one lands.
        /// Replies the player invited with their own letters are not blocked (each of those is capped
        /// at one per bond anyway). 0 stops NPCs from writing first at all. Clamped in
        /// <see cref="Normalize"/>.</summary>
        public int MaxLettersInFlight { get; set; } = 3;

        /// <summary>When true, exchanges that moved nothing also say so: a soft grey "X's heart held
        /// where it stood." after any exchange whose heart-shift was zero — the quiet counterpart of
        /// the green/red moved-heart lines, so every exchange visibly answers and a still heart is
        /// never mistaken for a missed message. Set false for the classic behavior where only real
        /// movements speak. Does nothing while <see cref="EnableRelationshipChanges"/> is off.</summary>
        public bool ShowHeartHeldNotice { get; set; } = true;

        /// <summary>When true, a soft notice tells you the moment an NPC quietly reworks her deep
        /// memory of you — folding old exchanges into her rolling summary and rewriting the truths
        /// she keeps — so the inner life is visible when it happens. Same gentle style as the
        /// activity notices. Set false for her to tend her memory in silence.</summary>
        public bool NotifyOnMemoryRefactor { get; set; } = true;

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

        /// <summary>When true, a chat window can be opened anywhere on the map — travelling, at sea, or
        /// inside a town, castle, or village menu — with the hotkey (<see cref="ChatWindowHotkey"/>) or
        /// the "Speak with those near you" settlement option. It lists everyone in the same place as you
        /// (your party, and everyone in the settlement), shows your whole remembered story with whoever
        /// you pick — their deep memory of you at the top, the recent exchanges below — and lets you
        /// simply write to them first ("how are our stocks?") without ceremony: no arrival, no forced
        /// greeting, just your words and their answer. Set false to hide the window entirely.</summary>
        public bool EnableChatWindow { get; set; } = true;

        /// <summary>The key that opens (and closes) the chat window on the map. A single letter or an
        /// InputKey name (e.g. "O", "Y", "F10"). Chosen not to collide with the vanilla map keys.</summary>
        public string ChatWindowHotkey { get; set; } = "O";

        /// <summary>When true (and the chat window is enabled), an NPC moved to reach out no longer asks
        /// through an accept/decline popup — they simply come and SPEAK: their first words land in the
        /// chat window as an unread message (with a faced toast, and the portrait map notice now opening
        /// the window), and the moment sits there until you answer — or don't. The time that passes is
        /// stamped into their memory either way, so they can see for themselves whether you replied at
        /// once, later, or let it lie. Set false to keep the old receive/decline offer flow.</summary>
        public bool SendInitiationsToChatWindow { get; set; } = true;

        /// <summary>When true (the default), clicking an NPC's reach-out notice opens the old-style
        /// face-to-face conversation with them — you see the person, not a window — showing the greeting
        /// they already spoke. There is no accept/decline: if you cannot talk, simply dismiss the notice
        /// (X it), and because their greeting is already a recorded beat, they see for themselves that
        /// they came to you and you did not answer. You can still open the chat window (its hotkey) to
        /// reply there instead. Takes precedence over <see cref="SendInitiationsToChatWindow"/> for what
        /// the notice click does. Set false to keep the older behavior (chat-window message, or the
        /// accept/decline offer).</summary>
        public bool OpenInitiationsFaceToFace { get; set; } = true;

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

        /// <summary>When true, NPCs may also search the internet mid-thought — framed to them as
        /// searching "all they have ever read and heard" — when asked how something in the world is
        /// done: ship handling, army raising, trade, any of the game's ways their own knowledge cannot
        /// answer. Their immersed question is first sharpened by a small refining LLM call into a real
        /// search query ("Mount and Blade Bannerlord …"); if that fails the game's name is quietly
        /// prepended as before. The NPC is told to speak the findings in their own voice, in the words
        /// of their world. Don't ask Google — ask one of your companions. Uses DuckDuckGo, no API key
        /// needed; shares <see cref="MaxRecallsPerReply"/> as its budget.</summary>
        public bool EnableWebSearch { get; set; } = true;

        /// <summary>When true, a soft side message tells you what an NPC is doing while you wait for
        /// their answer — "remembering…" when they pull what is known of a person or place, "taking
        /// stock of the company…", "researching…" when they search the wider world — the same style
        /// as the reply-ready notice. Set false for silence while they think.</summary>
        public bool ShowNpcActivity { get; set; } = true;

        /// <summary>When true, every NPC carries a daily humor — bright, weary, restless, brooding,
        /// tender, bold — woven into their situation so the same soul meets you differently day to
        /// day. It is derived from who they are and what day it is (no dice, nothing stored): the
        /// same day always finds them the same, and reloading changes no one's weather. Set false
        /// for souls whose spirits never shift with the days.</summary>
        public bool EnableMoodSwings { get; set; } = true;

        /// <summary>When true (and mood swings are on), the women of the world also keep their
        /// body's own monthly season — the custom of women, each on her own calendar — told to her
        /// gently by its turning (the days of the custom, the rising days, the crest, the waning
        /// days) so it colors her humor and she can weigh it in her own choices, as living women do.
        /// Women with child, and those past their childbearing years, are not given it. Set false
        /// to keep only the daily humor for everyone.</summary>
        public bool EnableWomensCycle { get; set; } = true;

        /// <summary>When true, an NPC whose self file has not yet been written begins with the story the
        /// world already tells of them instead of a blank page: a wanderer carries the tale they tell in
        /// taverns when first met (hand-written, in their own voice), a noble the account the encyclopedia
        /// keeps of their house and repute. From there it is theirs — every reflection lets them keep,
        /// refine, or release it. Deleting an NPC's self.txt re-seeds them afresh. Set false for everyone
        /// to begin unwritten.</summary>
        public bool SeedSelfFromWorldStory { get; set; } = true;

        /// <summary>When true, NPCs carry their own personal aims — what they strive for of their own will
        /// (win back a lost hall, see a child wed well, be free of a lord's leash) — held in a goals.txt
        /// beside their self. They shape these two ways: one aim at a time mid-conversation, through the
        /// tend_goals tool (needs a tool-capable backend), and wholesale when they gather their thoughts in
        /// reflection (works on any backend). The aims are folded into their prompt as "What you strive
        /// for". Set false to leave NPCs without aims of their own.</summary>
        public bool EnableNpcGoals { get; set; } = true;

        /// <summary>How many personal aims one NPC may carry at once (the tend_goals tool and reflection
        /// both honor it). Kept small so the prompt stays lean and their striving stays focused.</summary>
        public int MaxNpcGoals { get; set; } = 6;

        /// <summary>
        /// Reverting a bad turn: when on, each save quietly photographs this campaign's whole memory folder
        /// (every NPC's memories/self/goals/letters + the letters still on the road) and loading that save
        /// restores the photograph — so reloading to before an NPC's angry moment truly un-remembers it, the
        /// same way the game already reverts the relation number that lives inside the save. Off = the old
        /// behavior, where a reload leaves "memories from the future" in place. Snapshots live in _snapshots\
        /// inside the campaign folder, keyed to each save and pruned when a save is overwritten. Default on.
        /// </summary>
        public bool RevertMemoriesWithSaves { get; set; } = true;

        /// <summary>A safety cap on how many memory snapshots one campaign keeps across all its saves (oldest
        /// pruned first). Normally each save slot keeps only its own, so this bites only many-slot players.</summary>
        public int MaxMemorySnapshots { get; set; } = 40;

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

        /// <summary>The built-in model → context-window table. Longest key contained in the model id
        /// wins, so "gpt-5.1" beats "gpt-5" for gpt-5.1-mini. Users edit/extend the copy in their
        /// config.json; missing built-ins are re-added on load so new defaults reach old configs.</summary>
        public static Dictionary<string, int> DefaultModelContextWindows() =>
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["gpt-4o"] = 128000,
                ["gpt-4.1"] = 1000000,
                ["gpt-5"] = 400000,
                ["gpt-5.1"] = 400000,
                ["gpt-5.4"] = 400000,
                ["gpt-5.5"] = 400000,
                ["gpt-5.6"] = 1000000,
                ["claude"] = 200000,
                ["claude-opus-4"] = 1000000,
                ["claude-sonnet-5"] = 1000000,
                ["claude-sonnet-4-6"] = 1000000,
            };

        /// <summary>The built-in model → price table (USD per million tokens; verified 2026.07).
        /// Longest key contained in the model id wins, so "gpt-5.6-terra" beats "gpt-5.6".
        /// Users edit/extend the copy in config.json; missing built-ins are re-added on load.</summary>
        public static Dictionary<string, ModelPrice> DefaultModelPrices() =>
            new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
            {
                // Anthropic
                ["claude-opus-4"] = new ModelPrice(5, 25),
                ["claude-sonnet"] = new ModelPrice(3, 15),
                ["claude-haiku"] = new ModelPrice(1, 5),
                ["claude-fable-5"] = new ModelPrice(10, 50),
                // OpenAI
                ["gpt-5.6"] = new ModelPrice(5, 30),          // the bare alias routes to Sol
                ["gpt-5.6-sol"] = new ModelPrice(5, 30),
                ["gpt-5.6-terra"] = new ModelPrice(2.5, 15),
                ["gpt-5.6-luna"] = new ModelPrice(1, 6),
                ["gpt-5.5"] = new ModelPrice(1.25, 10),
                ["gpt-5.5-mini"] = new ModelPrice(0.25, 2),   // explicit: would otherwise match "gpt-5"
                ["gpt-5.5-nano"] = new ModelPrice(0.05, 0.4),
                ["gpt-5"] = new ModelPrice(1.25, 10),
                ["gpt-5-mini"] = new ModelPrice(0.25, 2),
                ["gpt-4o"] = new ModelPrice(2.5, 10),
                ["gpt-4o-mini"] = new ModelPrice(0.15, 0.6),
                ["gpt-4.1"] = new ModelPrice(2, 8),
                ["gpt-4.1-mini"] = new ModelPrice(0.4, 1.6),
            };

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

        /// <summary>Writes the current values back to config.json (used by the live socialness
        /// control, so a slider nudge survives the session). Best-effort: a failed save must never
        /// cost more than the persistence itself.</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { /* the value still lives for this session */ }
        }

        public void Normalize()
        {
            // The version stamp: pre-stamp configs (the field missing deserializes as 0) are the
            // V1 format too — everything else Normalize does IS the migration for them.
            if (ConfigVersion < 1) ConfigVersion = 1;

            if (string.IsNullOrWhiteSpace(SystemVoiceName)) SystemVoiceName = "Angel";

            // The reasoning dial only knows these words; a typo falls back to the calm default.
            var effort = (OpenAIReasoningEffort ?? string.Empty).Trim().ToLowerInvariant();
            OpenAIReasoningEffort = effort == "" || effort == "none" || effort == "minimal" || effort == "low"
                || effort == "medium" || effort == "high" || effort == "xhigh" || effort == "max"
                ? effort
                : "low";

            // The daily request cap: negative is a typo; 0 stays "no cap".
            if (MaxDailyRequests < 0) MaxDailyRequests = 0;

            // The hotkeys must name real keys; anything unparseable falls back to the defaults.
            if (string.IsNullOrWhiteSpace(ChatWindowHotkey)) ChatWindowHotkey = "O";
            ChatWindowHotkey = ChatWindowHotkey.Trim();
            if (string.IsNullOrWhiteSpace(LetterWindowHotkey)) LetterWindowHotkey = "U";
            LetterWindowHotkey = LetterWindowHotkey.Trim();

            // A null (rather than blank) atmosphere line would trip token substitution; treat it as "unset"
            // so the prompt falls back to its built-in default. Guidance may legitimately be blank (none).
            if (AtmosphereLine == null) AtmosphereLine = string.Empty;
            if (RoleplayGuidance == null) RoleplayGuidance = string.Empty;

            // Configs still carrying the pre-first-person defaults verbatim follow the sheet into the
            // new voice; a hand-edited line is honored as it stands.
            if (string.Equals(AtmosphereLine.Trim(), LegacyAtmosphereLine, StringComparison.Ordinal))
                AtmosphereLine = new ModConfig().AtmosphereLine;
            if (string.Equals(RoleplayGuidance.Trim(), LegacyRoleplayGuidance, StringComparison.Ordinal))
                RoleplayGuidance = new ModConfig().RoleplayGuidance;

            // Keep the daily rate non-negative and under one-per-hour, so a fat-fingered value can't have
            // every NPC hammering the player. 24 is already far more than anyone would want.
            if (DailyInitiationRate < 0 || double.IsNaN(DailyInitiationRate)) DailyInitiationRate = 0;
            if (DailyInitiationRate > 24) DailyInitiationRate = 24;

            // The stranger's floor is a fraction of a full bond's pull; anything outside [0,1] is a typo.
            if (InitiationPullFloor < 0 || double.IsNaN(InitiationPullFloor)) InitiationPullFloor = 0;
            if (InitiationPullFloor > 1) InitiationPullFloor = 1;

            // The road can only hold so many letters bound for one reader; anything above is a typo.
            if (MaxLettersInFlight < 0) MaxLettersInFlight = 0;
            if (MaxLettersInFlight > 20) MaxLettersInFlight = 20;

            // The model table: never null, and every built-in entry present (so new defaults reach
            // configs written before them); user edits to existing keys are honored as-is.
            if (ModelContextWindows == null) ModelContextWindows = DefaultModelContextWindows();
            foreach (var pair in DefaultModelContextWindows())
                if (!ModelContextWindows.ContainsKey(pair.Key)) ModelContextWindows[pair.Key] = pair.Value;

            // The price table follows the same contract as the context-window table.
            if (ModelPrices == null) ModelPrices = DefaultModelPrices();
            foreach (var pair in DefaultModelPrices())
                if (!ModelPrices.ContainsKey(pair.Key)) ModelPrices[pair.Key] = pair.Value;

            // Memory-writing budget: never below the spoken budget (that would make reflection the
            // narrowest voice she has), never runaway.
            if (MaxMemoryWriteTokens <= 0) MaxMemoryWriteTokens = 1500;
            if (MaxMemoryWriteTokens < MaxTokens) MaxMemoryWriteTokens = MaxTokens;
            if (MaxMemoryWriteTokens > 8000) MaxMemoryWriteTokens = 8000;

            // Truths budget: at least one, and a bound that keeps the prompt from silting up.
            if (MaxKnownFacts <= 0) MaxKnownFacts = 10;
            if (MaxKnownFacts > 30) MaxKnownFacts = 30;

            // Aims budget: at least one aim when goals are on, and a small ceiling so striving stays focused.
            if (MaxNpcGoals <= 0) MaxNpcGoals = 6;
            if (MaxNpcGoals > 20) MaxNpcGoals = 20;

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
