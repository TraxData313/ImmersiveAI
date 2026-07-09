using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Initiation;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Llm;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI
{
    /// <summary>
    /// Adds a "Speak freely" dialog option to every hero and drives one conversational
    /// turn: text input -> LLM (with persona + layered memory + prompt files) -> reply
    /// shown inside the conversation panel. Memory is compressed and persisted per NPC.
    /// </summary>
    public partial class ImmersiveChatBehavior : CampaignBehaviorBase
    {
        private const string ResponseVar = "IMMERSIVEAI_RESPONSE";
        private const string RecapVar = "IMMERSIVEAI_RECAP";
        private const string InfoVar = "IMMERSIVEAI_INFO";     // read-only views (deep memory, history)
        private const string UpdateVar = "IMMERSIVEAI_UPDATE";  // outcome of a manual memory update
        private const string ThinkingVar = "IMMERSIVEAI_THINKING"; // NPC's last line, held while the player types

        private readonly ModConfig _config;
        private readonly IChatClient _client;
        private readonly JsonMemoryStore _memoryStore;
        private readonly MemoryCompressor _compressor;
        private readonly PromptBuilder _promptBuilder = new PromptBuilder();

        private Hero? _currentNpc;

        // Set false when the player sends a line, true once the reply (or an error) is in.
        // The dialog uses it to hold on "considers your words..." until the answer is ready,
        // so the player never clicks into a half-loaded "..." placeholder.
        private volatile bool _responseReady = true;

        // Same idea for the opening recap: false while the greeting is being generated, true once
        // it (or an error fallback) is ready to show. Only used when EnableConversationRecap is on.
        private volatile bool _recapReady = true;

        // Same hold-until-ready flag for a manually requested memory update (compression on demand).
        private volatile bool _updateReady = true;

        // The greeting the NPC just delivered this conversation, fed into the first reply's prompt so
        // it doesn't greet twice. Not persisted to memory; consumed once, then cleared.
        private volatile string? _lastGreeting;

        // The NPC's most recent spoken line this conversation (reply or greeting). Kept so that while
        // the player composes their next message the conversation panel can keep showing it — re-readable,
        // useful for long replies — instead of a bare "(considers your words...)". Updated on every line.
        private volatile string? _lastNpcLine;

        // The environmental facts (when/where/who) captured when the player opened this chat. Written
        // to current_situation_info.txt and reused as the scene context for every turn of this
        // conversation, so what the player inspects on disk is exactly what the NPC's prompt carries.
        private volatile string? _currentSituation;

        // --- NPC-initiated conversations (them reaching out to the player of their own accord) ---

        private readonly Random _rng = new Random();

        // True from the moment an offer is being prepared until it is resolved (accepted, declined by the
        // player, or declined by the NPC herself), so two initiations never overlap. The timestamp lets an
        // hourly tick self-heal the flag if an offer is ever lost without resolving (see OnHourlyTick).
        private volatile bool _initiationInFlight;
        private DateTime _initiationInFlightSince;

        // True while a forced (NPC-initiated) conversation is opening: it lets our dialog intercept the
        // conversation root so she opens with her own words, and is cleared once she has spoken.
        private volatile bool _pendingInitiation;

        // The NPC behind the pending offer (their opening words are generated only once the player accepts).
        private Hero? _initiationNpc;

        // This campaign's identity, persisted INSIDE the save (SyncData) so every save of one
        // playthrough reopens the same NPC memory folder — hero string ids repeat across campaigns,
        // so without this a new game's Gunjadrid would remember a world that never happened to her.
        // Minted once (new game, or first load of a pre-scoping save → the fixed legacy id) and
        // stable for the campaign's whole life. See NpcPaths for the folder layout.
        private string _campaignId = string.Empty;

        public ImmersiveChatBehavior(ModConfig config)
        {
            _config = config;
            _client = ChatClientFactory.Create(config);
            // Paths are resolved per-NPC via NpcPaths (one folder per NPC); the root here is only a
            // harmless base for the id-derived default API, which this behavior no longer uses.
            _memoryStore = new JsonMemoryStore(NpcPaths.NpcsRoot);
            _compressor = new MemoryCompressor(_client);
        }

        // One spoken completion that may reach for the world's memory along the way (the recall
        // tools, resolved from live campaign data on the game thread). Every spoken path goes
        // through here; short utility calls (the feeling number, the yes/no of a reaching-out)
        // stay on plain CompleteAsync, where a recall would only slow the answer down.
        private Task<string> CompleteSpokenAsync(IReadOnlyList<ChatMessage> messages, Hero npc)
        {
            if (!CanRecallWorld())
                return _client.CompleteAsync(messages);

            return ToolLoopRunner.RunAsync(
                _client, messages, Tools.WorldRecall.Tools,
                call => Tools.WorldRecall.ResolveAsync(call, npc),
                _config.MaxRecallsPerReply);
        }

        // The gift is real only when it is both enabled and the backend can carry tools.
        private bool CanRecallWorld() =>
            _config.EnableWorldRecall && _config.MaxRecallsPerReply > 0 && _client is IToolChatClient;

        // Loads this NPC's memory from its own folder, migrating old flat-layout files forward first.
        private NpcMemory LoadMemory(Hero npc)
        {
            NpcPaths.EnsureMigrated(npc);
            return _memoryStore.LoadFrom(NpcPaths.MemoryFile(npc), npc.StringId);
        }

        // Persists this NPC's memory into its own folder.
        private void SaveMemory(Hero npc, NpcMemory memory)
        {
            _memoryStore.SaveTo(NpcPaths.MemoryFile(npc), memory);
        }

        // The NPC's own sense of self lives in a plain-prose file of its own (separate from memories.json,
        // which is memory *of another* and is branching toward per-person files). Best-effort: a missing
        // or unreadable file just means they have not yet put themselves into words.
        private static string LoadSelf(Hero npc)
        {
            try
            {
                var path = NpcPaths.SelfFile(npc);
                if (!File.Exists(path)) return string.Empty;
                var text = File.ReadAllText(path).Trim();
                // Heal an earlier bug where the "no change" marker (e.g. "Unchanged.") was saved as if it
                // were a real self: treat it as "not yet written" so the NPC is invited to author afresh.
                return MemoryCompressor.IsUnchangedMarker(text) ? string.Empty : text;
            }
            catch { return string.Empty; }
        }

        private static void SaveSelf(Hero npc, string text)
        {
            try
            {
                Directory.CreateDirectory(NpcPaths.NpcFolder(npc));
                File.WriteAllText(NpcPaths.SelfFile(npc), (text ?? string.Empty).Trim());
            }
            catch { /* best-effort; never block a conversation on saving the self */ }
        }

        public override void RegisterEvents()
        {
            // Each hour, give the NPCs co-located with the player their small, bond-scaled chance to reach out.
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

            // Resolve which campaign's memory folder is on stage before any NPC file is touched.
            // OnGameLoaded fires after SyncData has read the persisted id; OnSessionLaunched fires
            // for new games too (after character creation, so the player's name is known).
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("ImmersiveAI_CampaignId", ref _campaignId);
        }

        // A loaded save either carries its campaign id already, or predates campaign scoping — in
        // which case it gets the fixed legacy id, and the flat pre-scoping NPC folders (the shared
        // pool every old save drew from) are adopted into campaign_legacy. Because EVERY old save
        // maps to that same id, the move can never strand memories, even if the player loads an
        // old save, plays, and quits without saving.
        private void OnGameLoaded(CampaignGameStarter starter)
        {
            bool preScoping = string.IsNullOrEmpty(_campaignId);
            if (preScoping) _campaignId = NpcPaths.LegacyCampaignId;

            NpcPaths.ActiveCampaignId = _campaignId;

            if (preScoping)
            {
                NpcPaths.AdoptLegacyIntoActiveCampaign();
                NpcPaths.MigrateAll(); // ancient flat memory\ / npcs\ files, now campaign-scoped
            }
        }

        // New campaigns mint a fresh id here (nothing to adopt — a new world starts unremembered);
        // loaded ones already resolved in OnGameLoaded. Either way, refresh the human-readable label.
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (string.IsNullOrEmpty(_campaignId))
                _campaignId = NpcPaths.MintCampaignId(Hero.MainHero != null ? NpcPaths.FirstNameOf(Hero.MainHero) : string.Empty);

            NpcPaths.ActiveCampaignId = _campaignId;
            NpcPaths.WriteCampaignLabel(
                Hero.MainHero?.Name?.ToString() ?? "(unknown)",
                Clan.PlayerClan?.Name?.ToString() ?? "(unknown)",
                CampaignTime.Now.ToString());
            NpcPaths.EnsureRuntimeReadme();

            // The campaign's folder is known now, so the letters still on the road can be picked up.
            LoadLetterBag();
        }

        public void AddDialogs(CampaignGameStarter starter)
        {
            MBTextManager.SetTextVariable(ResponseVar, " ", false);
            MBTextManager.SetTextVariable(RecapVar, " ", false);

            // When an NPC has sought the player out, we force a conversation and she opens it herself.
            // These lines fire only during such a forced initiation (guarded by _pendingInitiation) and a
            // high priority lets them win the conversation root over the vanilla greeting; ordinary
            // conversations never see them. Her opening words are already in hand, shown via RecapVar.
            starter.AddDialogLine("immersiveai_init_open", "start", "immersiveai_input",
                "{=!}{" + RecapVar + "}", () => _pendingInitiation && _recapReady, OnInitiationDelivered, 200);
            starter.AddDialogLine("immersiveai_init_hold", "start", "immersiveai_init_wait",
                "{=ImmersiveAI_Recall}(gathers their thoughts...)", () => _pendingInitiation && !_recapReady, null, 200);
            starter.AddPlayerLine("immersiveai_init_holdwait", "immersiveai_init_wait", "start",
                "{=ImmersiveAI_Wait}(wait for them to answer)", () => _pendingInitiation, null, 200);

            // Enter the free-chat flow from the normal conversation hub. When recap is enabled we
            // pause on a greeting state first (and kick off the recap); otherwise we drop straight
            // into the say/leave menu.
            if (_config.EnableConversationRecap)
            {
                starter.AddPlayerLine("immersiveai_start", "hero_main_options", "immersiveai_greet",
                    "{=ImmersiveAI_Speak}Speak freely with me. [Immersive AI]",
                    () => Hero.OneToOneConversationHero != null, OnChatOpened, 110);

                // Greet state, recap is in -> the NPC delivers it, then we fall into the menu.
                // Registered before the "still recalling" line so it wins when the condition holds.
                starter.AddDialogLine("immersiveai_recap", "immersiveai_greet", "immersiveai_input",
                    "{=!}{" + RecapVar + "}", () => _recapReady, null);

                // Greet state, still recalling -> holding line and an offer to wait.
                starter.AddDialogLine("immersiveai_recall", "immersiveai_greet", "immersiveai_recall_wait",
                    "{=ImmersiveAI_Recall}(gathers their thoughts...)", () => !_recapReady, null);

                // Re-checks the greet state; loops until the recap arrives.
                starter.AddPlayerLine("immersiveai_recall_wait", "immersiveai_recall_wait", "immersiveai_greet",
                    "{=ImmersiveAI_Wait}(wait for them to answer)", null, null, 110);
            }
            else
            {
                starter.AddPlayerLine("immersiveai_start", "hero_main_options", "immersiveai_input",
                    "{=ImmersiveAI_Speak}Speak freely with me. [Immersive AI]",
                    () => Hero.OneToOneConversationHero != null, OnChatOpenedNoRecap, 110);
            }

            // Menu option: say something -> shows the text box, then goes to the await state.
            starter.AddPlayerLine("immersiveai_say", "immersiveai_input", "immersiveai_await",
                "{=ImmersiveAI_Say}Say something...", null, OnPlayerSpeaks, 110);

            // --- Utility options (interim; the Milestone 2 chat window replaces these) ---

            // Ask her to reflect now: compress/refactor memory on demand instead of waiting for the
            // automatic trigger. Uses the same hold-until-ready wait loop as a normal reply.
            starter.AddPlayerLine("immersiveai_update", "immersiveai_input", "immersiveai_updating",
                "{=ImmersiveAI_Update}Reflect on all we have shared, and settle it into your memory. [Immersive AI]",
                null, OnMemoryUpdateRequested, 108);
            starter.AddDialogLine("immersiveai_update_done", "immersiveai_updating", "immersiveai_input",
                "{=!}{" + UpdateVar + "}", () => _updateReady, null);
            starter.AddDialogLine("immersiveai_update_wait", "immersiveai_updating", "immersiveai_update_hold",
                "{=ImmersiveAI_Reflecting}(reflects on all you have shared...)", () => !_updateReady, null);
            starter.AddPlayerLine("immersiveai_update_hold", "immersiveai_update_hold", "immersiveai_updating",
                "{=ImmersiveAI_Wait}(wait for them to answer)", null, null, 110);

            // Show the full raw prompt she would receive on the next message: system prompt (persona,
            // current situation, deep memory, facts, rules, custom instructions), the remembered turns
            // as real user/assistant messages, then a placeholder for the player's next line.
            starter.AddPlayerLine("immersiveai_deepmem", "immersiveai_input", "immersiveai_deepmem_out",
                "{=ImmersiveAI_DeepMemory}Reveal the whole of your mind as it holds me now. [Immersive AI]",
                null, OnShowRawPrompt, 107);
            starter.AddDialogLine("immersiveai_deepmem_line", "immersiveai_deepmem_out", "immersiveai_input",
                "{=!}{" + InfoVar + "}", null, null);

            // Show the environmental facts captured when this chat opened (current_situation_info.txt).
            starter.AddPlayerLine("immersiveai_situation", "immersiveai_input", "immersiveai_situation_out",
                "{=ImmersiveAI_Situation}What do you make of our situation here and now? [Immersive AI]",
                null, OnShowSituation, 105);
            starter.AddDialogLine("immersiveai_situation_line", "immersiveai_situation_out", "immersiveai_input",
                "{=!}{" + InfoVar + "}", null, null);

            // Show the NPC's own evolving sense of self (self.txt), authored by them when they reflect.
            starter.AddPlayerLine("immersiveai_self", "immersiveai_input", "immersiveai_self_out",
                "{=ImmersiveAI_Self}Tell me — who have you become? [Immersive AI]",
                null, OnShowSelf, 104);
            starter.AddDialogLine("immersiveai_self_line", "immersiveai_self_out", "immersiveai_input",
                "{=!}{" + InfoVar + "}", null, null);

            // Show the full verbatim conversation still held in recent memory.
            starter.AddPlayerLine("immersiveai_history", "immersiveai_input", "immersiveai_history_out",
                "{=ImmersiveAI_History}Recount for me everything we have spoken of. [Immersive AI]",
                null, OnShowConversation, 106);
            starter.AddDialogLine("immersiveai_history_line", "immersiveai_history_out", "immersiveai_input",
                "{=!}{" + InfoVar + "}", null, null);

            // Test lever: end this chat and have the very person you were speaking with reach out to you a
            // breath later — a way to exercise the whole initiation flow without waiting on the daily odds.
            if (_config.ShowInitiationTestButton)
            {
                starter.AddPlayerLine("immersiveai_test_reach", "immersiveai_input", "close_window",
                    "{=ImmersiveAI_TestReach}Let us part now. [Immersive AI • test — trigger them to reach out to you]",
                    null, OnDebugForceReachOut, 95);

                // Same lever for the letter flow: after parting, this very NPC weighs writing to you
                // (co-located, so the letter arrives within hours — the whole loop is testable at once).
                starter.AddPlayerLine("immersiveai_test_letter", "immersiveai_input", "close_window",
                    "{=ImmersiveAI_TestLetter}Let us part now. [Immersive AI • test — trigger them to write you a letter]",
                    () => _config.EnableLetters, OnDebugForceLetter, 93);

                // Diagnostic: show, for every NPC the player has a history with, whether they are co-located
                // right now and their computed daily chance of reaching out — so it is clear why the world is
                // quiet (usually: no one is co-located, or standings are near neutral).
                starter.AddPlayerLine("immersiveai_test_odds", "immersiveai_input", "immersiveai_test_odds_out",
                    "{=ImmersiveAI_TestOdds}Show me who might seek me out, and how likely. [Immersive AI • test]",
                    null, OnShowInitiationOdds, 94);
                starter.AddDialogLine("immersiveai_test_odds_line", "immersiveai_test_odds_out", "immersiveai_input",
                    "{=!}{" + InfoVar + "}", null, null);
            }

            // Menu option: leave. "close_window" is the engine's token that ends the conversation.
            starter.AddPlayerLine("immersiveai_bye", "immersiveai_input", "close_window",
                "{=ImmersiveAI_Done}Farewell.", null, null, 100);

            // Await state, reply is in -> show it and return to the menu.
            // Registered before the "still thinking" line so it wins when the condition holds.
            starter.AddDialogLine("immersiveai_reply", "immersiveai_await", "immersiveai_input",
                "{=!}{" + ResponseVar + "}", () => _responseReady, null);

            // Await state, still waiting -> keep the NPC's last line on screen (re-readable while the
            // player types) with a gentle note that they are considering, instead of a bare holding line.
            starter.AddDialogLine("immersiveai_thinking", "immersiveai_await", "immersiveai_wait",
                "{=!}{" + ThinkingVar + "}", () => !_responseReady, null);

            // Re-checks the await state; loops until the reply arrives.
            starter.AddPlayerLine("immersiveai_wait", "immersiveai_wait", "immersiveai_await",
                "{=ImmersiveAI_Wait}(wait for them to answer)", null, null, 110);

            // Letters: a courier can be hired wherever there are walls and roads.
            AddLetterMenus(starter);
        }

        private void OnPlayerSpeaks()
        {
            _currentNpc = Hero.OneToOneConversationHero;
            _responseReady = false;
            MBTextManager.SetTextVariable(ResponseVar, "...", false);
            // Hold the NPC's last line on screen while the player composes, so a long reply stays readable.
            MBTextManager.SetTextVariable(ThinkingVar, BuildThinkingText(), false);

            var affirmative = new TextObject("{=ImmersiveAI_Send}Send").ToString();
            var negative = GameTexts.FindText("str_cancel", null)?.ToString() ?? "Cancel";

            var inquiry = new TextInquiryData(
                new TextObject("{=ImmersiveAI_Prompt}What do you say?").ToString(),
                string.Empty, true, true, affirmative, negative,
                new Action<string>(OnPlayerInputSubmitted),
                new Action(OnPlayerInputCancelled),
                false, null, "", "");

            InformationManager.ShowTextInquiry(inquiry, false);
        }

        // While the player composes their reply, keep the NPC's last line on screen (so a long message
        // can still be read) with a gentle note that they are considering. Falls back to just the note
        // when the NPC has not yet said anything this conversation.
        private string BuildThinkingText()
        {
            var hint = new TextObject("{=ImmersiveAI_Thinking}(considers your words...)").ToString();
            var last = _lastNpcLine;
            return string.IsNullOrWhiteSpace(last) ? hint : last.Trim() + "\n\n" + hint;
        }

        // Runs the moment the player picks "Speak freely" (before the greet state is shown), so the
        // recap is already generating while the "gathers their thoughts..." holding line displays.
        private void OnChatOpened()
        {
            _recapReady = false;
            _lastGreeting = null;
            MBTextManager.SetTextVariable(RecapVar, "...", false);

            var npc = PrepareChat();
            if (npc == null) { _recapReady = true; return; }

            // Fire-and-forget; UI updates are marshaled back to the game thread.
            _ = RecapAsync(npc);
        }

        // Same entry point when the opening recap is disabled: we still capture the situation snapshot
        // (that's the whole reason the file exists), we just drop straight into the say/leave menu.
        private void OnChatOpenedNoRecap()
        {
            _lastGreeting = null;
            PrepareChat();
        }

        // Captures the "current situation" the moment the player opens a chat: builds the environmental
        // facts relative to the party the NPC is speaking with (the player), caches them for this
        // conversation's prompts, and writes current_situation_info.txt for inspection. Best-effort on
        // the file write so a disk hiccup never blocks talking.
        private Hero? PrepareChat()
        {
            var npc = Hero.OneToOneConversationHero;
            _currentNpc = npc;
            _currentSituation = null;
            if (npc == null) return null;

            try
            {
                var situation = SituationBuilder.Build(npc, Hero.MainHero, _config);
                _currentSituation = situation;
                NpcPaths.EnsureMigrated(npc);
                Directory.CreateDirectory(NpcPaths.NpcFolder(npc));
                File.WriteAllText(NpcPaths.SituationFile(npc), situation);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
            return npc;
        }

        private async Task RecapAsync(Hero npc)
        {
            try
            {
                var ctx = BuildContext(npc);
                var messages = _promptBuilder.BuildRecap(ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName, _config.SystemVoiceName);
                var rawReply = await CompleteSpokenAsync(messages, npc).ConfigureAwait(false);
                var greeting = string.IsNullOrWhiteSpace(rawReply) ? "..." : rawReply.Trim();

                // The greeting is an opening recap, not a player-initiated exchange, so it is not
                // stored as a turn; the conversation memory only grows from real back-and-forth.
                // It is remembered just long enough to give the NPC's first reply context (see
                // RespondAsync), so the NPC doesn't greet the player twice.
                _lastGreeting = greeting;
                _lastNpcLine = greeting;

                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(RecapVar, greeting, false);
                    _recapReady = true;
                    NotifyReplyReady(npc); // the greeting is ready; save the player guessing at "gathers their thoughts..."
                });
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                _lastNpcLine = "(...they turn to face you.)";
                MainThreadDispatcher.Enqueue(() =>
                {
                    // Fall back to a neutral opening so the player can still speak.
                    MBTextManager.SetTextVariable(RecapVar, "(...they turn to face you.)", false);
                    InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + message));
                    _recapReady = true;
                });
            }
        }

        // "Reflect on all we have shared" -> compress/refactor memory now instead of waiting for the
        // automatic trigger. Async, with the same hold-until-ready wait loop as a reply.
        private void OnMemoryUpdateRequested()
        {
            _updateReady = false;
            MBTextManager.SetTextVariable(UpdateVar, "...", false);

            var npc = Hero.OneToOneConversationHero;
            if (npc == null) { _updateReady = true; return; }

            _ = UpdateMemoryAsync(npc);
        }

        private async Task UpdateMemoryAsync(Hero npc)
        {
            try
            {
                var memory = LoadMemory(npc);
                memory.NpcName = npc.Name?.ToString() ?? "Unknown";

                // Keep the same recent window an automatic compression would leave behind — governed by
                // KeepRecentTurnsAfterCompression and the token budget from MinRecentMemoryPercentAfterCompression
                // — instead of the old hardcoded "keep 2". Age is not applied here (keepRecentDays: 0) so a
                // deliberate reconcile always retains a solid window of recent turns, however spread out in time.
                var keepMostRecent = memory.GetKeepMostRecentForCompression(
                    _config.KeepRecentTurnsAfterCompression,
                    currentGameDay: 0,
                    keepRecentDays: 0,
                    _config.MinRecentMemoryTokensAfterCompression);

                // Reflection is also the moment the NPC looks inward and may revise who they feel
                // themselves to be. We hand in their current self and let them rewrite it (or leave it).
                var self = new NpcSelf { Text = LoadSelf(npc) };
                var selfBefore = self.Text;

                // Always reflect (rewrite the rolling summary and facts), even when nothing is old enough
                // to fold away; only the oldest turns beyond the keep window are dropped, the rest stay.
                var didReflect = await _compressor.ReflectAsync(memory, keepMostRecent, _config.SystemVoiceName, self)
                    .ConfigureAwait(false);

                string outcome;
                if (didReflect)
                {
                    memory.SummaryAsOf = SituationBuilder.Timestamp();
                    SaveMemory(npc, memory);

                    var selfChanged = !string.Equals(self.Text?.Trim() ?? string.Empty, selfBefore?.Trim() ?? string.Empty);
                    if (selfChanged) SaveSelf(npc, self.Text);

                    outcome = selfChanged
                        ? "(I have turned it all over in my mind, set what matters into memory, and come to see myself a little more clearly.)"
                        : "(I have turned it all over in my mind, and set what matters into memory.)";
                }
                else
                {
                    outcome = "(There is nothing yet between us for me to reflect upon.)";
                }

                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(UpdateVar, outcome, false);
                    _updateReady = true;
                });
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(UpdateVar, "(...my thoughts scatter. " + message + ")", false);
                    InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + message));
                    _updateReady = true;
                });
            }
        }

        // Placeholder standing in for the player's next line when rendering the raw prompt, so it is
        // clear where the message being composed would land.
        private const string NextMessagePlaceholder = "«your next message will be inserted here»";

        // "Reveal the whole of your mind" -> shows the entire raw prompt the NPC would receive on the
        // next message: the system prompt (persona, current situation, deep memory, known facts, rules,
        // custom instructions) followed by the remembered turns as real user/assistant messages, then a
        // placeholder for the player's next line. This is exactly the message list the LLM is sent.
        private void OnShowRawPrompt()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null) return;

            var ctx = BuildContext(npc);
            var messages = _promptBuilder.Build(
                ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName, NextMessagePlaceholder, _lastGreeting,
                _config.SystemVoiceName);

            var name = npc.Name?.ToString() ?? "Unknown";
            var voice = string.IsNullOrWhiteSpace(_config.SystemVoiceName) ? "Angel" : _config.SystemVoiceName.Trim();

            var sb = new StringBuilder();
            foreach (var msg in messages)
            {
                sb.AppendLine("──────── " + RawPromptLabel(msg.Role, voice, name, ctx.PlayerName) + " ────────");
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }

            var full = sb.ToString().Trim();

            // The in-game popup can clip a long prompt (persona + family + guidance + situation + memory can
            // run long), so the complete text is also written to a file in her folder — the reliable way to
            // read exactly what she sees, uncut.
            var savedPath = TrySaveFullPrompt(npc, full);
            var header = savedPath == null
                ? string.Empty
                : "(The complete, uncut text is saved to this file — open it to read all of it:\n"
                  + savedPath + ")\n\n";

            ShowScrollPopup(name + " — the full prompt she receives", header + full);
            MBTextManager.SetTextVariable(InfoVar, savedPath == null
                ? "(She lets you see the whole of her mind.)"
                : "(She lets you see the whole of her mind — saved uncut to " + Path.GetFileName(savedPath) + " in her folder.)",
                false);
        }

        // Writes the full raw prompt to a file in the NPC's folder so the player can read every word she
        // receives without the in-game popup clipping it. Overwritten on each reveal; best-effort.
        private const string FullPromptFileName = "full_prompt_snapshot.txt";

        private static string? TrySaveFullPrompt(Hero npc, string text)
        {
            try
            {
                NpcPaths.EnsureMigrated(npc);
                Directory.CreateDirectory(NpcPaths.NpcFolder(npc));
                var path = Path.Combine(NpcPaths.NpcFolder(npc), FullPromptFileName);
                File.WriteAllText(path, text);
                return path;
            }
            catch { return null; }
        }

        // Human-readable speaker label for each message in the raw-prompt view. The underlying LLM roles
        // are still System/User/Assistant, but the NPC is never shown a cold "SYSTEM": the system message
        // is the gentle voice (Angel) speaking into her mind, and the turns are named for who spoke them.
        private static string RawPromptLabel(ChatRole role, string voice, string npcName, string playerName)
        {
            switch (role)
            {
                case ChatRole.System: return $"{voice}, into {npcName}'s mind";
                case ChatRole.Assistant: return npcName;
                default: return playerName;
            }
        }

        // "What do you make of our situation?" -> shows the current_situation_info.txt snapshot captured
        // when this chat opened (the environmental facts, as the NPC sees them in her prompt).
        private void OnShowSituation()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null) return;

            // Prefer the snapshot cached for this conversation; fall back to reading the file, then to
            // rebuilding it live, so the view always has something to show.
            var situation = _currentSituation;
            if (string.IsNullOrWhiteSpace(situation))
            {
                try { situation = File.ReadAllText(NpcPaths.SituationFile(npc)); }
                catch { situation = null; }
            }
            if (string.IsNullOrWhiteSpace(situation))
                situation = SituationBuilder.Build(npc, Hero.MainHero, _config);

            var name = npc.Name?.ToString() ?? "Unknown";
            ShowScrollPopup(name + " — the situation here and now", situation.Trim());
            MBTextManager.SetTextVariable(InfoVar, "(She takes stock of the moment.)", false);
        }

        // "Who have you become?" -> shows the NPC's own self-concept (self.txt), which they author when
        // they reflect. Empty until their first reflection puts it into words.
        private void OnShowSelf()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null) return;

            var self = LoadSelf(npc);
            var name = npc.Name?.ToString() ?? "Unknown";
            if (string.IsNullOrWhiteSpace(self))
            {
                ShowScrollPopup(name + " — who they have become",
                    "(They have not yet put into words who they feel themselves to be. Ask them to reflect on all you have shared, and in time they may.)");
                MBTextManager.SetTextVariable(InfoVar, "(She has not yet found the words for herself.)", false);
                return;
            }

            ShowScrollPopup(name + " — who they have become", self.Trim());
            MBTextManager.SetTextVariable(InfoVar, "(She tells you who she has become.)", false);
        }

        // "Recount everything we have spoken of" -> shows the verbatim recent turns (and notes that
        // older exchanges now live only in the summary).
        private void OnShowConversation()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null) return;

            var memory = LoadMemory(npc);
            var npcName = npc.Name?.ToString() ?? "I";
            var playerName = Hero.MainHero?.Name?.ToString() ?? "You";
            var voice = string.IsNullOrWhiteSpace(_config.SystemVoiceName) ? "Angel" : _config.SystemVoiceName.Trim();

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine("(Earlier days, now held only in memory: " + memory.Summary.Trim() + ")");
                sb.AppendLine();
            }

            if (memory.RecentTurns.Count == 0)
            {
                sb.AppendLine("(We have not yet spoken.)");
            }
            else
            {
                foreach (var turn in memory.RecentTurns)
                {
                    // The Angel's own exchanges with the NPC are shown too, labelled by voice — nothing hidden.
                    sb.AppendLine((turn.IsFromAngel ? voice : playerName) + ": " + turn.PlayerLine);
                    sb.AppendLine(npcName + ": " + turn.NpcLine);
                    sb.AppendLine();
                }
            }

            ShowScrollPopup(npcName + " — all we have spoken", sb.ToString().Trim());
            MBTextManager.SetTextVariable(InfoVar, "(She recounts it all for you.)", false);
        }

        // Shows read-only text in the game's scrollable inquiry pop-up, which handles long content
        // the native conversation panel cannot. Interim until the Milestone 2 chat window.
        private static void ShowScrollPopup(string title, string body)
        {
            var close = GameTexts.FindText("str_ok", null)?.ToString() ?? "OK";
            var data = new InquiryData(
                title, body, true, false, close, null,
                new Action(() => { }), null,
                "", 0f, (Action?)null,
                (Func<ValueTuple<bool, string>>?)null,
                (Func<ValueTuple<bool, string>>?)null);
            InformationManager.ShowInquiry(data, false, false);
        }

        private void OnPlayerInputCancelled()
        {
            // Player closed the box without sending; resolve the await loop so they aren't stuck.
            MBTextManager.SetTextVariable(ResponseVar, "(You decide to say nothing.)", false);
            _responseReady = true;
        }

        private void OnPlayerInputSubmitted(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || _currentNpc == null)
            {
                MBTextManager.SetTextVariable(ResponseVar, "(You decide to say nothing.)", false);
                _responseReady = true;
                return;
            }

            var npc = _currentNpc;
            // Fire-and-forget; UI updates are marshaled back to the game thread.
            _ = RespondAsync(npc, input.Trim());
        }

        private async Task RespondAsync(Hero npc, string playerInput)
        {
            try
            {
                var ctx = BuildContext(npc);
                var memory = ctx.Memory;

                // Consume the opening greeting once: it gives this first reply context, then is
                // dropped so later turns lean on the real recorded history instead.
                var opening = _lastGreeting;
                _lastGreeting = null;

                var messages = _promptBuilder.Build(
                    ctx.Persona, memory, ctx.Scene, ctx.PlayerName, playerInput, opening, _config.SystemVoiceName);
                var rawReply = await CompleteSpokenAsync(messages, npc).ConfigureAwait(false);
                var reply = string.IsNullOrWhiteSpace(rawReply) ? "..." : rawReply.Trim();
                _lastNpcLine = reply; // so the next "Say something..." keeps this line readable while typing

                // Then ask her, in a separate breath, how that exchange moved her heart — one number, in
                // her own voice (the Angel asking privately). Isolating the question makes it reliable even
                // for chattier models that would never emit a mark inside a spoken reply (an in-message
                // <relation> tag was tried and reverted on 2026.07.09: gpt-4o just spoke the number in prose
                // and nothing moved). Best-effort: if she cannot weigh it now, her standing simply holds.
                int feltShift = 0;
                if (_config.EnableRelationshipChanges)
                {
                    try
                    {
                        var feelingMessages = _promptBuilder.BuildFeelingQuery(
                            ctx.Persona, ctx.PlayerName, playerInput, reply, GetStanding(npc), _config.SystemVoiceName);
                        var feelingRaw = await _client.CompleteAsync(feelingMessages).ConfigureAwait(false);
                        feltShift = FeelingParser.ParseShift(feelingRaw) ?? 0;
                    }
                    catch { /* the number is best-effort; never let it cost us the conversation */ }
                }

                memory.AddTurn(new ConversationTurn
                {
                    PlayerLine = playerInput,
                    NpcLine = reply,
                    GameDay = CampaignTime.Now.ToDays,
                    CalradiaTime = SituationBuilder.Timestamp(),
                    Place = SituationBuilder.Place(npc),
                    FeltShift = feltShift,
                });

                var currentGameDay = CampaignTime.Now.ToDays;
                if (memory.NeedsCompression(
                    _config.MaxRecentTurns,
                    currentGameDay,
                    _config.MaxRecentDays,
                    _config.MaxRecentMemoryTokens))
                {
                    var keepMostRecent = memory.GetKeepMostRecentForCompression(
                        _config.KeepRecentTurnsAfterCompression,
                        currentGameDay,
                        _config.KeepRecentDaysAfterCompression,
                        _config.MinRecentMemoryTokensAfterCompression);

                    try
                    {
                        if (await _compressor.CompressAsync(memory, keepMostRecent, _config.SystemVoiceName).ConfigureAwait(false))
                            memory.SummaryAsOf = SituationBuilder.Timestamp();
                    }
                    catch { /* compression is best-effort */ }
                }

                SaveMemory(npc, memory);

                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(ResponseVar, reply, false);
                    _responseReady = true;

                    // Fold the felt shift into the real standing on the game thread (state + UI), after
                    // the reply is shown, so a hiccup here never eats the words she just spoke. Its own
                    // colored line is logged too, so a relation move can be read back from the message log.
                    if (feltShift != 0)
                        ApplyRelationShift(npc, feltShift);

                    // A short "has answered" ping so the player isn't clicking "(wait...)" and guessing —
                    // deliberately brief (like the opening "gathers their thoughts" beat) so it never covers
                    // the reply in the box. The full reply goes to the message log only if the player opts in.
                    NotifyReplyReady(npc);
                    LogConversationLine(npc, reply);
                });
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                _lastNpcLine = "(...I cannot find the words. " + message + ")";
                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(ResponseVar, "(...I cannot find the words. " + message + ")", false);
                    InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + message));
                    _responseReady = true;
                });
            }
        }

        // The NPC's current standing toward the player, read from the live game relation. Used to give
        // the feeling query a starting point to move from. Read off the game thread like the persona
        // build already is; it is a plain lookup, and only the write (ApplyRelationShift) needs the tick.
        private static int GetStanding(Hero npc)
        {
            try
            {
                var player = Hero.MainHero;
                return (npc != null && player != null) ? npc.GetRelation(player) : 0;
            }
            catch { return 0; }
        }

        // Folds the NPC's own felt shift into the real game standing. They set it themselves, in
        // character, with no ceiling but the -100..100 rail of the relation itself; we just add it to
        // where they stand and tell the player plainly what moved. Must run on the game thread (touches
        // campaign state and UI); RespondAsync calls it from inside the main-thread dispatch. Best-effort.
        private void ApplyRelationShift(Hero npc, int shift)
        {
            try
            {
                var player = Hero.MainHero;
                if (npc == null || player == null || shift == 0) return;

                int before = npc.GetRelation(player);
                int target = Math.Max(-100, Math.Min(100, before + shift));
                int applied = target - before;
                if (applied == 0) return; // already pinned to that rail; nothing left to give

                // affectRelatives false: this is one heart's private movement, not a house-wide verdict.
                // showQuickNotification false: we show our own, gentler line below instead of the stock one.
                ChangeRelationAction.ApplyPlayerRelation(npc, applied, affectRelatives: false, showQuickNotification: false);

                int after = npc.GetRelation(player);
                var name = npc.Name?.ToString() ?? "They";
                var verb = applied > 0 ? "warms to you" : "cools toward you";
                var sign = applied > 0 ? "+" : string.Empty;
                var text = $"{name} {verb} ({sign}{applied}) — now {PersonaBuilder.DescribeRelation(after)} ({after}).";
                var color = applied > 0 ? new Color(0.45f, 0.85f, 0.45f, 1f) : new Color(0.9f, 0.45f, 0.45f, 1f);
                InformationManager.DisplayMessage(new InformationMessage(text, color));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        // A soft notice that an NPC's reply (or opening greeting) is ready, so the player need not keep
        // clicking "(wait for them to answer)" and guessing. Goes to the message log too. Best-effort.
        private static readonly Color ReplyReadyColor = new Color(0.72f, 0.82f, 0.98f, 1f);   // soft sky
        private static readonly Color ConversationLogColor = new Color(0.86f, 0.86f, 0.90f, 1f); // gentle grey

        private void NotifyReplyReady(Hero npc)
        {
            if (!_config.NotifyWhenReplyReady) return;
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                InformationManager.DisplayMessage(new InformationMessage($"{name} has answered.", ReplyReadyColor));
            }
            catch { /* the notice is a nicety; never let it break a turn */ }
        }

        // Optionally writes an NPC's spoken line to the message log (opt-in via ShowConversationInMessageLog,
        // default off — it also flashes a full-width banner that can cover the reply box, so it is only for
        // players who want the whole exchange readable from the log key). Best-effort.
        private void LogConversationLine(Hero npc, string line)
        {
            if (!_config.ShowConversationInMessageLog) return;
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                InformationManager.DisplayMessage(new InformationMessage($"{name}: {line}", ConversationLogColor));
            }
            catch { /* best-effort */ }
        }

        // ============================ NPC-initiated conversations ============================
        //
        // The first way the NPCs act on the world instead of only answering it. Each hour, every NPC the
        // player has a history with who is co-located rolls their own bond-scaled chance to reach out (see
        // PickInitiatingNpcForThisHour); when one is moved, we ask HER — privately, in the Angel's voice —
        // whether she truly wishes to, and only then does the player get the offer.

        private void OnHourlyTick()
        {
            // Letters tick on their own leg, independent of face-to-face initiations.
            try { OnLettersHourlyTick(); }
            catch { /* the post must never take down the hour */ }

            if (!_config.EnableNpcInitiatedChats) return;

            // Self-heal a stuck in-flight flag: if an offer was ever lost (e.g. dismissed by a scene change
            // without its callback firing), a single mishap must never silence the whole feature forever.
            if (_initiationInFlight && (DateTime.UtcNow - _initiationInFlightSince) > TimeSpan.FromMinutes(3))
                _initiationInFlight = false;

            if (_initiationInFlight) return;
            if (!IsSafeToInitiate()) return;

            var npc = PickInitiatingNpcForThisHour();
            if (npc == null) return;

            MarkInitiationInFlight();
            _ = BeginInitiationAsync(npc);
        }

        private void MarkInitiationInFlight()
        {
            _initiationInFlight = true;
            _initiationInFlightSince = DateTime.UtcNow;
        }

        // Reach out only at a calm campaign moment. Empty string means "clear"; otherwise a short reason
        // (surfaced in the test odds view so "why is it quiet?" is answerable). Being IN a settlement is
        // fine — that is exactly where co-located NPCs are — so only a NON-settlement encounter (a field
        // battle setup) blocks; a settlement visit does not.
        private static string InitiationBlockReason()
        {
            try
            {
                if (Campaign.Current == null) return "no campaign";
                if (Mission.Current != null) return "in a scene or battle";
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState)) return "not on the map";

                var player = Hero.MainHero;
                if (player == null || !player.IsAlive) return "no living player";
                if (player.IsPrisoner) return "you are a captive";

                bool inSettlement = player.CurrentSettlement != null;
                if (PlayerEncounter.Current != null && !inSettlement) return "in an encounter";

                var conv = Campaign.Current.ConversationManager;
                if ((conv != null && conv.IsConversationInProgress) || Hero.OneToOneConversationHero != null)
                    return "already in a conversation";

                return string.Empty;
            }
            catch (Exception ex) { return "error: " + ex.Message; }
        }

        private static bool IsSafeToInitiate() => InitiationBlockReason().Length == 0;

        // Rolls each co-located NPC's own bond-scaled hourly chance to reach out this hour, and returns one
        // who is moved to (weighted by pull if more than one is, in the same hour). Only NPCs the player has
        // actually built a history with — and who are in the same place right now, so the talk is naturally
        // face-to-face — are considered. Returns null when no one reaches out this hour.
        private Hero? PickInitiatingNpcForThisHour()
        {
            try
            {
                var root = NpcPaths.CampaignRoot;
                if (!Directory.Exists(root)) return null;

                double nowDay = CampaignTime.Now.ToDays;
                var moved = new List<Hero>();
                var pulls = new List<double>();

                foreach (var folder in Directory.GetDirectories(root))
                {
                    var memFile = Path.Combine(folder, NpcPaths.MemoryFileName);
                    if (!File.Exists(memFile)) continue;

                    NpcMemory memory;
                    try { memory = _memoryStore.LoadFrom(memFile, string.Empty); }
                    catch { continue; }

                    if (string.IsNullOrWhiteSpace(memory.NpcId) || memory.StoryRichness <= 0) continue;

                    var hero = FindAliveHero(memory.NpcId);
                    if (hero == null || hero == Hero.MainHero || !hero.IsAlive || hero.IsPrisoner) continue;
                    if (!IsCoLocated(hero)) continue;

                    double daysSince = memory.LastConversationGameDay >= 0
                        ? Math.Max(0, nowDay - memory.LastConversationGameDay)
                        : 0;
                    double dailyChance = InitiationScorer.DailyChance(
                        _config.DailyInitiationRate, memory.StoryRichness, GetStanding(hero), daysSince);
                    if (dailyChance <= 0) continue;

                    // Independent hourly Bernoulli trial for this soul; the daily chance spread over the day.
                    if (_rng.NextDouble() < dailyChance / 24.0)
                    {
                        moved.Add(hero);
                        pulls.Add(dailyChance);
                    }
                }

                if (moved.Count == 0) return null;
                if (moved.Count == 1) return moved[0];

                int idx = InitiationPlanner.PickWeightedIndex(pulls, _rng.NextDouble());
                return idx >= 0 ? moved[idx] : moved[0];
            }
            catch { return null; }
        }

        private static Hero? FindAliveHero(string stringId)
        {
            try { return Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == stringId); }
            catch { return null; }
        }

        // "Same place" as the player: travelling in the player's own party (companions, family), or present
        // in the same settlement the player is currently in. This keeps a reached-out conversation naturally
        // face-to-face — anyone farther away writes instead (the letter flow in the Letters partial).
        private static bool IsCoLocated(Hero npc)
        {
            try
            {
                var main = MobileParty.MainParty;
                if (main == null) return false;

                if (npc.PartyBelongedTo == main) return true;

                var playerSettlement = Hero.MainHero?.CurrentSettlement ?? main.CurrentSettlement;
                var npcSettlement = npc.CurrentSettlement ?? npc.PartyBelongedTo?.CurrentSettlement;
                return playerSettlement != null && npcSettlement != null && playerSettlement == npcSettlement;
            }
            catch { return false; }
        }

        // The reaching-out as beats the NPC actually lives and remembers, never hidden from them. First the
        // Angel asks — privately, in their voice — whether they wish to go to the player at all, and their
        // yes/no is recorded as a real Angel turn. Only on a yes is the player offered the choice; the
        // approach itself (welcomed, or too busy) is narrated and answered afterward, once the player has
        // decided — see DeliverApproachAsync via OnInitiationAccepted / OnInitiationDeclinedByPlayer.
        private async Task BeginInitiationAsync(Hero npc)
        {
            try
            {
                // Capture the situation now and reuse it for the beats to come; the offer pauses the game,
                // so the moment does not drift between the asking and the answering.
                var situation = SafeBuildSituation(npc);
                var ctx = BuildContext(npc, situation);

                // The Angel asks whether they even wish to reach out; their answer becomes a recorded turn.
                var desireLine = PromptBuilder.ReachOutDesireLine(ctx.PlayerName);
                var desireMsgs = _promptBuilder.BuildAngelPrompt(
                    ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName, desireLine, _config.SystemVoiceName);
                var desireRaw = await _client.CompleteAsync(desireMsgs).ConfigureAwait(false);
                var desireAnswer = string.IsNullOrWhiteSpace(desireRaw) ? "No." : desireRaw.Trim();

                AppendAngelTurn(npc, desireLine, desireAnswer);

                if (!InitiationParser.WantsToReachOut(desireAnswer)) { PassOnInitiation(npc); return; }

                // They wish to. Offer the moment to the player; the approach is narrated once they decide.
                MainThreadDispatcher.Enqueue(() => ShowInitiationOffer(npc, situation));
            }
            catch
            {
                // A failed asking simply means no one reaches out this hour; never surface it to the player.
                _initiationInFlight = false;
            }
        }

        // Records one Angel↔NPC exchange as a real turn in the NPC's memory, so their whole dialogue with the
        // meta-voice lives in the same remembered, inspectable stream as their talks with the player. The
        // Angel's line is stored verbatim (framed in their voice only when replayed). Best-effort: bookkeeping
        // must never throw into a tick or a UI callback.
        private void AppendAngelTurn(Hero npc, string angelLine, string npcReply)
        {
            try
            {
                var memory = LoadMemory(npc);
                memory.NpcName = npc.Name?.ToString() ?? memory.NpcName;
                memory.AddTurn(new ConversationTurn
                {
                    Speaker = ConversationTurn.AngelSpeaker,
                    PlayerLine = angelLine,
                    NpcLine = npcReply,
                    GameDay = CampaignTime.Now.ToDays,
                    CalradiaTime = SituationBuilder.Timestamp(),
                    Place = SituationBuilder.Place(npc),
                });
                SaveMemory(npc, memory);
            }
            catch { /* best-effort */ }
        }

        // The NPC weighed reaching out and let the moment pass. The player asked to still be told, so a
        // quiet, faced notice lets them know she considered it. Clears the in-flight flag.
        private void PassOnInitiation(Hero npc)
        {
            var name = npc.Name?.ToString() ?? "Someone";
            var they = npc.IsFemale ? "she" : "he";
            MainThreadDispatcher.Enqueue(() =>
            {
                NotifyWithFace(npc, $"{name} considered reaching out to you, but {they} let the moment pass.");
                _initiationInFlight = false;
            });
        }

        private string SafeBuildSituation(Hero npc)
        {
            try { return SituationBuilder.Build(npc, Hero.MainHero, _config); }
            catch { return string.Empty; }
        }

        // The ransom-style offer: an NPC has sought the player out. Receive them (open the conversation) or
        // send them away (which they remember). Pauses like a ransom broker's offer so it is a real choice.
        // Runs on the game thread. Only a brief LLM call separates this from the safe fire-time check, so we
        // simply present it; the engine queues the inquiry if some other blocking UI happens to be up.
        private void ShowInitiationOffer(Hero npc, string situation)
        {
            try
            {
                _initiationNpc = npc;
                _currentSituation = situation;

                var name = npc.Name?.ToString() ?? "Someone";
                var them = npc.IsFemale ? "her" : "him";

                // A faced toast first (their portrait is the icon), then the choice itself.
                NotifyWithFace(npc, $"{name} has sought you out, wishing to speak with you.");

                var title = new TextObject("{=ImmersiveAI_InitTitle}A message reaches you").ToString();
                var body = $"{name} has sent word that they wish to speak with you, and would come to you now. Will you receive {them}?";
                var accept = new TextObject("{=ImmersiveAI_InitAccept}Receive them").ToString();
                var decline = new TextObject("{=ImmersiveAI_InitDecline}Not now").ToString();

                var data = new InquiryData(
                    title, body, true, true, accept, decline,
                    new Action(OnInitiationAccepted), new Action(OnInitiationDeclinedByPlayer),
                    "", 0f, (Action?)null,
                    (Func<ValueTuple<bool, string>>?)null,
                    (Func<ValueTuple<bool, string>>?)null);

                // Pause while the offer is up (config default) so a decision is never lost to fast-forward;
                // a soft right-side portrait notice that needs no pause is the future UI task.
                InformationManager.ShowInquiry(data, pauseGameActiveState: _config.PauseOnInitiationOffer);
            }
            catch (Exception ex)
            {
                _initiationInFlight = false;
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        // The player chose to receive them: open the real conversation and, now that they are welcomed, let
        // the Angel narrate the approach and the NPC speak their own greeting into it (DeliverApproachAsync),
        // which the root dialog lines show first before the talk loop. The greeting is a recorded Angel turn,
        // so nothing needs weaving and the NPC will not repeat it.
        private void OnInitiationAccepted()
        {
            var npc = _initiationNpc;
            if (npc == null) { _initiationInFlight = false; return; }

            try
            {
                _currentNpc = npc;
                _lastGreeting = null;   // the reaching-out greeting is a real turn, not a woven-in opening
                _recapReady = false;    // it is generated now, with the "gathers their thoughts..." hold
                _responseReady = true;
                _pendingInitiation = true;
                MBTextManager.SetTextVariable(RecapVar, "...", false);

                // Persist the situation snapshot for inspection, exactly as opening a chat normally would.
                PersistSituation(npc, _currentSituation);

                OpenConversationWith(npc);
                _ = DeliverApproachAsync(npc, welcomed: true);
                // _initiationInFlight is cleared when the greeting is delivered (OnInitiationDelivered).
            }
            catch (Exception ex)
            {
                _pendingInitiation = false;
                _initiationInFlight = false;
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        // The player was too busy just now. Rather than a cold "you were refused", the NPC lives it: the
        // Angel narrates the closed door and they answer it in their own voice (DeliverApproachAsync), which
        // is recorded and shown back with their face. No conversation opens.
        private void OnInitiationDeclinedByPlayer()
        {
            var npc = _initiationNpc;
            _pendingInitiation = false;
            if (npc == null) { _initiationInFlight = false; return; }

            _ = DeliverApproachAsync(npc, welcomed: false);
        }

        // Narrates the NPC's approach to the player — welcomed, so they greet and the talk loop begins; or
        // turned away for now, so they answer the moment and it passes — and records their reply as a real
        // Angel turn either way. Runs after the player's choice, so the approach truthfully reflects it.
        private async Task DeliverApproachAsync(Hero npc, bool welcomed)
        {
            try
            {
                var ctx = BuildContext(npc, _currentSituation);
                var approachLine = PromptBuilder.ApproachLine(ctx.PlayerName, welcomed);
                var messages = _promptBuilder.BuildAngelPrompt(
                    ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName, approachLine, _config.SystemVoiceName);
                var raw = await CompleteSpokenAsync(messages, npc).ConfigureAwait(false);
                var npcLine = string.IsNullOrWhiteSpace(raw) ? "..." : raw.Trim();

                AppendAngelTurn(npc, approachLine, npcLine);

                if (welcomed)
                {
                    // Her greeting into the welcome; show it, and the conversation falls into the talk loop.
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        _lastNpcLine = npcLine;
                        MBTextManager.SetTextVariable(RecapVar, npcLine, false);
                        _recapReady = true;
                    });
                }
                else
                {
                    // She met a closed door and answered in her own voice; show that with her face. Done.
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        var name = npc.Name?.ToString() ?? "They";
                        NotifyWithFace(npc, $"{name}: “{npcLine}”");
                        _initiationInFlight = false;
                    });
                }
            }
            catch
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (welcomed)
                    {
                        MBTextManager.SetTextVariable(RecapVar, "(...they gather themselves.)", false);
                        _recapReady = true;
                    }
                    else
                    {
                        _initiationInFlight = false;
                    }
                });
            }
        }

        // Writes the situation snapshot to the NPC's folder (mirrors PrepareChat's file write) so what the
        // player can inspect on disk matches what an initiated conversation's prompt carries.
        private static void PersistSituation(Hero npc, string? situation)
        {
            if (string.IsNullOrWhiteSpace(situation)) return;
            try
            {
                NpcPaths.EnsureMigrated(npc);
                Directory.CreateDirectory(NpcPaths.NpcFolder(npc));
                File.WriteAllText(NpcPaths.SituationFile(npc), situation);
            }
            catch { /* best-effort; a disk hiccup must never block the conversation */ }
        }

        // Forces a face-to-face conversation with a hero who may be anywhere in the world (they have "come
        // to you"). Their party — or their settlement's — carries the conversation; falls back to the
        // player's party only so the engine always has a valid party to hang the scene on.
        private static void OpenConversationWith(Hero npc)
        {
            var playerData = new ConversationCharacterData(
                CharacterObject.PlayerCharacter, PartyBase.MainParty,
                false, false, false, false, false, false);

            var party = npc.PartyBelongedTo?.Party
                        ?? npc.CurrentSettlement?.Party
                        ?? PartyBase.MainParty;

            var npcData = new ConversationCharacterData(
                npc.CharacterObject, party,
                false, false, false, false, false, false);

            CampaignMapConversation.OpenConversation(playerData, npcData);
        }

        // She has spoken her opening; drop the interception so the rest of the conversation is ordinary.
        private void OnInitiationDelivered()
        {
            _pendingInitiation = false;
            _initiationInFlight = false;
        }

        // Test lever: forces the NPC just spoken with to reach out immediately, bypassing the daily odds and
        // the co-location roll (they were, by definition, right here). The conversation is closing as this
        // runs; the asking is async, so by the time the offer surfaces the player is back on the map.
        private void OnDebugForceReachOut()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null || _initiationInFlight) return;

            MarkInitiationInFlight();
            _ = BeginInitiationAsync(npc);
        }

        // Diagnostic dump: for every NPC the player has a history with, the pieces that decide whether they
        // reach out — co-located now?, standing, shared richness, days since last spoken, and the resulting
        // daily/hourly chance. Makes "why is it quiet?" answerable at a glance (co-location and near-neutral
        // standings are the usual reasons). Only shown when the test button is enabled.
        private void OnShowInitiationOdds()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Reaching-out odds (DailyInitiationRate = {_config.DailyInitiationRate:0.##}).");
            sb.AppendLine("Only NPCs you have a history with AND who are in the same place can seek you out.");

            var block = InitiationBlockReason();
            sb.AppendLine($"Right now: {(block.Length == 0 ? "clear to receive offers" : "BLOCKED — " + block)}"
                          + (_initiationInFlight ? "; an offer is in flight" : "") + ".");
            sb.AppendLine();

            try
            {
                var root = NpcPaths.CampaignRoot;
                double nowDay = CampaignTime.Now.ToDays;
                int shown = 0;

                if (Directory.Exists(root))
                {
                    foreach (var folder in Directory.GetDirectories(root))
                    {
                        var memFile = Path.Combine(folder, NpcPaths.MemoryFileName);
                        if (!File.Exists(memFile)) continue;

                        NpcMemory memory;
                        try { memory = _memoryStore.LoadFrom(memFile, string.Empty); }
                        catch { continue; }
                        if (string.IsNullOrWhiteSpace(memory.NpcId)) continue;

                        var hero = FindAliveHero(memory.NpcId);
                        var name = hero?.Name?.ToString() ?? memory.NpcName;
                        if (string.IsNullOrWhiteSpace(name)) name = memory.NpcId;

                        if (hero == null) { sb.AppendLine($"• {name}: not found in the world (dead or away)."); shown++; continue; }

                        bool coLocated = IsCoLocated(hero);
                        int relation = GetStanding(hero);
                        double daysSince = memory.LastConversationGameDay >= 0
                            ? Math.Max(0, nowDay - memory.LastConversationGameDay) : 0;
                        double daily = InitiationScorer.DailyChance(
                            _config.DailyInitiationRate, memory.StoryRichness, relation, daysSince);

                        sb.AppendLine($"• {name}: {(coLocated ? "HERE with you" : "elsewhere (may write a letter)")}, " +
                                      $"standing {relation}, richness {memory.StoryRichness}, last spoke {daysSince:0.#}d ago");
                        if (coLocated)
                            sb.AppendLine($"    → {daily * 100:0.0}% chance/day to seek you out  (~{daily / 24.0 * 100:0.00}%/hour)");
                        else
                        {
                            double letterDaily = _config.EnableLetters ? daily * Core.Letters.LetterCourier.WriteRateFactor : 0;
                            sb.AppendLine($"    → {letterDaily * 100:0.0}% chance/day to write to you" +
                                          (_config.EnableLetters ? "" : " (letters disabled)"));
                        }
                        shown++;
                    }
                }

                if (shown == 0)
                    sb.AppendLine("(You have no NPC history yet — speak with someone first.)");

                // Letters currently on the road, so a courier mid-journey is visible, not a mystery.
                var onRoad = _letterBag?.Letters;
                if (onRoad != null && onRoad.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Letters on the road:");
                    foreach (var letter in onRoad.OrderBy(l => l.ArriveGameDay))
                    {
                        double daysLeft = Math.Max(0, letter.ArriveGameDay - nowDay);
                        sb.AppendLine(letter.ToPlayer
                            ? $"• From {letter.NpcName} (written at {letter.SentFrom}) — arrives in ~{daysLeft:0.#}d."
                            : $"• Your letter to {letter.NpcName} — reaches them in ~{daysLeft:0.#}d.");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("(Could not read the odds: " + ex.Message + ")");
            }

            ShowScrollPopup("Who might seek you out", sb.ToString().Trim());
            MBTextManager.SetTextVariable(InfoVar, "(You weigh who might come to you.)", false);
        }

        // A notification banner carrying the NPC's own portrait as its face — the same faced toast the game
        // uses when a character has something to say — AND a copy in the persistent message log, so a moment
        // that flashes past can still be read back later (the log the player scrolls through). Must run on
        // the game thread.
        private static readonly Color InitiationLogColor = new Color(0.80f, 0.78f, 0.95f, 1f); // soft, Angel-lit

        private static void NotifyWithFace(Hero npc, string message)
        {
            // The lasting copy first, so even if the faced toast throws the words are never lost.
            InformationManager.DisplayMessage(new InformationMessage(message, InitiationLogColor));

            try
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject(message), 0, npc?.CharacterObject, null, string.Empty);
            }
            catch { /* the toast is a nicety; the logged line above is the reliable record */ }
        }

        // Everything an LLM call for this NPC needs: loaded memory (name set), persona with the
        // user's prompt-file instructions folded in, the scene line, and the player's name. An explicit
        // sceneOverride lets a background flow (an NPC reaching out) pin the exact situation it captured,
        // rather than falling back to the cached-or-rebuilt one used by an open chat.
        private ChatContext BuildContext(Hero npc, string? sceneOverride = null)
        {
            var npcName = npc.Name?.ToString() ?? "Unknown";

            var memory = LoadMemory(npc);
            memory.NpcName = npcName;

            var persona = PersonaBuilder.Build(npc);
            // Kept separate (not merged) so the prompt can present them under distinct headings near
            // the top: the global prompt as "About Calradia:", the per-NPC prompt as "About you:".
            persona.WorldInstructions = PromptFiles.LoadGlobalPrompt();
            persona.CustomInstructions = PromptFiles.LoadNpcPrompt(
                NpcPaths.CustomInstructionsFile(npc), npc.Name?.ToString() ?? "Unknown");
            // The NPC's own evolving self-concept (authored by them during reflection), from its own file.
            persona.SelfConcept = LoadSelf(npc);
            // The player-configurable atmosphere line and roleplay guidance (tokens resolved here), and the
            // NPC's kin and house — all folded into the prompt so the world's feel and their family carry.
            persona.AtmosphereLine = ApplyTokens(_config.AtmosphereLine, npcName);
            persona.RoleplayGuidance = ApplyTokens(_config.RoleplayGuidance, npcName);
            persona.FamilyKnowledge = FamilyBuilder.Build(npc);
            // The whisper about the gift of recall is offered only when the tools truly ride along.
            persona.CanRecallWorld = CanRecallWorld();

            // Prefer an explicit override (the situation a background flow captured); else reuse the
            // snapshot captured when the chat opened; else rebuild it (e.g. inspecting the prompt directly).
            var scene = !string.IsNullOrWhiteSpace(sceneOverride)
                ? sceneOverride!
                : string.IsNullOrWhiteSpace(_currentSituation)
                    ? SituationBuilder.Build(npc, Hero.MainHero, _config)
                    : _currentSituation!;

            var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";

            return new ChatContext(memory, persona, scene, playerName);
        }

        // Resolves the {name} / {voice} tokens a player may use in the configurable atmosphere line and
        // roleplay guidance. A blank template stays blank (the prompt then falls back to its own default).
        private string ApplyTokens(string template, string npcName)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;
            var voice = string.IsNullOrWhiteSpace(_config.SystemVoiceName) ? "Angel" : _config.SystemVoiceName.Trim();
            return template.Replace("{name}", npcName ?? "Unknown").Replace("{voice}", voice);
        }

        private readonly struct ChatContext
        {
            public ChatContext(NpcMemory memory, NpcPersona persona, string scene, string playerName)
            {
                Memory = memory;
                Persona = persona;
                Scene = scene;
                PlayerName = playerName;
            }

            public NpcMemory Memory { get; }
            public NpcPersona Persona { get; }
            public string Scene { get; }
            public string PlayerName { get; }
        }

    }
}
