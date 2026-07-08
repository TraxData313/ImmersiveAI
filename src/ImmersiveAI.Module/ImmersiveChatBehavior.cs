using System;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Llm;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Settlements;
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

        // The greeting the NPC just delivered this conversation, fed into the first reply's prompt so
        // it doesn't greet twice. Not persisted to memory; consumed once, then cleared.
        private volatile string? _lastGreeting;

        public ImmersiveChatBehavior(ModConfig config)
        {
            _config = config;
            _client = ChatClientFactory.Create(config);
            _memoryStore = new JsonMemoryStore(System.IO.Path.Combine(ModConfig.ConfigDirectory, "memory"));
            _compressor = new MemoryCompressor(_client);
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
                    () => Hero.OneToOneConversationHero != null, null, 110);
            }

            // Menu option: say something -> shows the text box, then goes to the await state.
            starter.AddPlayerLine("immersiveai_say", "immersiveai_input", "immersiveai_await",
                "{=ImmersiveAI_Say}Say something...", null, OnPlayerSpeaks, 110);

            // Menu option: leave. "close_window" is the engine's token that ends the conversation.
            starter.AddPlayerLine("immersiveai_bye", "immersiveai_input", "close_window",
                "{=ImmersiveAI_Done}Farewell.", null, null, 109);

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
            _currentNpc = Hero.OneToOneConversationHero;
            _recapReady = false;
            _lastGreeting = null;
            MBTextManager.SetTextVariable(RecapVar, "...", false);

            var npc = _currentNpc;
            if (npc == null) { _recapReady = true; return; }

            // Fire-and-forget; UI updates are marshaled back to the game thread.
            _ = RecapAsync(npc);
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

                    try { await _compressor.CompressAsync(memory, keepMostRecent).ConfigureAwait(false); }
                    catch { /* compression is best-effort */ }
                }

                _memoryStore.Save(memory);

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
            var npcId = npc.StringId;
            var npcName = npc.Name?.ToString() ?? "Unknown";

            var memory = _memoryStore.Load(npcId);
            memory.NpcName = npcName;

            var persona = PersonaBuilder.Build(npc);
            persona.CustomInstructions = CombineInstructions(npcId, npcName);

            var scene = BuildSceneContext(npc);
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

        private static string CombineInstructions(string npcId, string npcName)
        {
            var global = PromptFiles.LoadGlobalPrompt();
            var npcSpecific = PromptFiles.LoadNpcPrompt(npcId, npcName);
            var sb = new StringBuilder();
            if (global.Length > 0) sb.AppendLine(global);
            if (npcSpecific.Length > 0) sb.AppendLine(npcSpecific);
            return sb.ToString().Trim();
        }

        private static string BuildSceneContext(Hero npc)
        {
            var sb = new StringBuilder();
            var settlement = npc.CurrentSettlement ?? Settlement.CurrentSettlement;
            if (settlement != null)
                sb.Append("You are in " + settlement.Name + ". ");
            else
                sb.Append("You are out in the field. ");

            sb.Append("The season is " + CampaignTime.Now.GetSeasonOfYear + ".");
            return sb.ToString();
        }
    }
}
