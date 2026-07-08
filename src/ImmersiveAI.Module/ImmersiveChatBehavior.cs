using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Llm;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImmersiveAI
{
    /// <summary>
    /// Adds a "Speak freely" dialog option to every hero and drives one conversational
    /// turn: text input -> LLM (with persona + layered memory + prompt files) -> reply
    /// shown inside the conversation panel. Memory is compressed and persisted per NPC.
    /// </summary>
    public class ImmersiveChatBehavior : CampaignBehaviorBase
    {
        private const string ResponseVar = "IMMERSIVEAI_RESPONSE";
        private const string RecapVar = "IMMERSIVEAI_RECAP";
        private const string InfoVar = "IMMERSIVEAI_INFO";     // read-only views (deep memory, history)
        private const string UpdateVar = "IMMERSIVEAI_UPDATE";  // outcome of a manual memory update

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

        // The environmental facts (when/where/who) captured when the player opened this chat. Written
        // to current_situation_info.txt and reused as the scene context for every turn of this
        // conversation, so what the player inspects on disk is exactly what the NPC's prompt carries.
        private volatile string? _currentSituation;

        public ImmersiveChatBehavior(ModConfig config)
        {
            _config = config;
            _client = ChatClientFactory.Create(config);
            // Paths are resolved per-NPC via NpcPaths (one folder per NPC); the root here is only a
            // harmless base for the id-derived default API, which this behavior no longer uses.
            _memoryStore = new JsonMemoryStore(NpcPaths.NpcsRoot);
            _compressor = new MemoryCompressor(_client);
        }

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

        public override void RegisterEvents() { }
        public override void SyncData(IDataStore dataStore) { }

        public void AddDialogs(CampaignGameStarter starter)
        {
            MBTextManager.SetTextVariable(ResponseVar, " ", false);
            MBTextManager.SetTextVariable(RecapVar, " ", false);

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

            // Menu option: leave. "close_window" is the engine's token that ends the conversation.
            starter.AddPlayerLine("immersiveai_bye", "immersiveai_input", "close_window",
                "{=ImmersiveAI_Done}Farewell.", null, null, 100);

            // Await state, reply is in -> show it and return to the menu.
            // Registered before the "still thinking" line so it wins when the condition holds.
            starter.AddDialogLine("immersiveai_reply", "immersiveai_await", "immersiveai_input",
                "{=!}{" + ResponseVar + "}", () => _responseReady, null);

            // Await state, still waiting -> show a holding line and offer to wait more.
            starter.AddDialogLine("immersiveai_thinking", "immersiveai_await", "immersiveai_wait",
                "{=ImmersiveAI_Thinking}(considers your words...)", () => !_responseReady, null);

            // Re-checks the await state; loops until the reply arrives.
            starter.AddPlayerLine("immersiveai_wait", "immersiveai_wait", "immersiveai_await",
                "{=ImmersiveAI_Wait}(wait for them to answer)", null, null, 110);
        }

        private void OnPlayerSpeaks()
        {
            _currentNpc = Hero.OneToOneConversationHero;
            _responseReady = false;
            MBTextManager.SetTextVariable(ResponseVar, "...", false);

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
                var situation = SituationBuilder.Build(npc, Hero.MainHero);
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
                var messages = _promptBuilder.BuildRecap(ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName);
                var rawReply = await _client.CompleteAsync(messages).ConfigureAwait(false);
                var greeting = string.IsNullOrWhiteSpace(rawReply) ? "..." : rawReply.Trim();

                // The greeting is an opening recap, not a player-initiated exchange, so it is not
                // stored as a turn; the conversation memory only grows from real back-and-forth.
                // It is remembered just long enough to give the NPC's first reply context (see
                // RespondAsync), so the NPC doesn't greet the player twice.
                _lastGreeting = greeting;

                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(RecapVar, greeting, false);
                    _recapReady = true;
                });
            }
            catch (Exception ex)
            {
                var message = ex.Message;
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
                ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName, NextMessagePlaceholder, _lastGreeting);

            var name = npc.Name?.ToString() ?? "Unknown";
            var voice = string.IsNullOrWhiteSpace(_config.SystemVoiceName) ? "Angel" : _config.SystemVoiceName.Trim();

            var sb = new StringBuilder();
            foreach (var msg in messages)
            {
                sb.AppendLine("──────── " + RawPromptLabel(msg.Role, voice, name, ctx.PlayerName) + " ────────");
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }

            ShowScrollPopup(name + " — the full prompt she receives", sb.ToString().Trim());
            MBTextManager.SetTextVariable(InfoVar, "(She lets you see the whole of her mind.)", false);
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
                situation = SituationBuilder.Build(npc, Hero.MainHero);

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
                    sb.AppendLine(playerName + ": " + turn.PlayerLine);
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

                var messages = _promptBuilder.Build(ctx.Persona, memory, ctx.Scene, ctx.PlayerName, playerInput, opening);
                var rawReply = await _client.CompleteAsync(messages).ConfigureAwait(false);
                var reply = string.IsNullOrWhiteSpace(rawReply) ? "..." : rawReply.Trim();

                memory.AddTurn(new ConversationTurn
                {
                    PlayerLine = playerInput,
                    NpcLine = reply,
                    GameDay = CampaignTime.Now.ToDays,
                    CalradiaTime = SituationBuilder.Timestamp(),
                    Place = SituationBuilder.Place(npc),
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
                });
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(ResponseVar, "(...I cannot find the words. " + message + ")", false);
                    InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + message));
                    _responseReady = true;
                });
            }
        }

        // Everything an LLM call for this NPC needs: loaded memory (name set), persona with the
        // user's prompt-file instructions folded in, the scene line, and the player's name.
        private ChatContext BuildContext(Hero npc)
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

            // Reuse the situation snapshot captured when the chat opened; rebuild it if this context is
            // requested outside a normal chat-open flow (e.g. inspecting the prompt directly).
            var scene = string.IsNullOrWhiteSpace(_currentSituation)
                ? SituationBuilder.Build(npc, Hero.MainHero)
                : _currentSituation!;

            var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";

            return new ChatContext(memory, persona, scene, playerName);
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
