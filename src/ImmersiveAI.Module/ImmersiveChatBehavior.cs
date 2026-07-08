using System;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Llm;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
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

            // Enter the free-chat menu from the normal conversation hub.
            starter.AddPlayerLine("immersiveai_start", "hero_main_options", "immersiveai_input",
                "{=ImmersiveAI_Speak}Speak freely with me. [Immersive AI]",
                () => Hero.OneToOneConversationHero != null, null, 110);

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
                var npcId = npc.StringId;
                var npcName = npc.Name?.ToString() ?? "Unknown";

                var memory = _memoryStore.Load(npcId);
                memory.NpcName = npcName;

                var persona = PersonaBuilder.Build(npc);
                persona.CustomInstructions = CombineInstructions(npcId, npcName);

                var scene = BuildSceneContext(npc);
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";

                var messages = _promptBuilder.Build(persona, memory, scene, playerName, playerInput);
                var reply = (await _client.CompleteAsync(messages).ConfigureAwait(false))?.Trim();
                if (string.IsNullOrEmpty(reply)) reply = "...";

                memory.AddTurn(new ConversationTurn
                {
                    PlayerLine = playerInput,
                    NpcLine = reply,
                    GameDay = CampaignTime.Now.ToDays,
                });

                if (memory.NeedsCompression(_config.MaxRecentTurns))
                {
                    try { await _compressor.CompressAsync(memory, _config.KeepRecentTurnsAfterCompression).ConfigureAwait(false); }
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
