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

            starter.AddPlayerLine("immersiveai_start", "hero_main_options", "immersiveai_input",
                "{=ImmersiveAI_Speak}Speak freely with me. [Immersive AI]",
                () => Hero.OneToOneConversationHero != null, null, 110);

            starter.AddPlayerLine("immersiveai_say", "immersiveai_input", "immersiveai_thinking",
                "{=ImmersiveAI_Say}Say something...", null, OnPlayerSpeaks, 110);

            starter.AddPlayerLine("immersiveai_bye", "immersiveai_input", "hero_main_options",
                "{=ImmersiveAI_Done}That is all for now.", null, null, 109);

            starter.AddDialogLine("immersiveai_thinking", "immersiveai_thinking", "immersiveai_reply",
                "{=ImmersiveAI_Thinking}(considers your words...)", null, null);

            starter.AddDialogLine("immersiveai_reply", "immersiveai_reply", "immersiveai_input",
                "{=!}{" + ResponseVar + "}", null, null);
        }

        private void OnPlayerSpeaks()
        {
            _currentNpc = Hero.OneToOneConversationHero;
            MBTextManager.SetTextVariable(ResponseVar, "...", false);

            var affirmative = new TextObject("{=ImmersiveAI_Send}Send").ToString();
            var negative = GameTexts.FindText("str_cancel", null)?.ToString() ?? "Cancel";

            var inquiry = new TextInquiryData(
                new TextObject("{=ImmersiveAI_Prompt}What do you say?").ToString(),
                string.Empty, true, true, affirmative, negative,
                new Action<string>(OnPlayerInputSubmitted),
                new Action(() => { }),
                false, null, "", "");

            InformationManager.ShowTextInquiry(inquiry, false);
        }

        private void OnPlayerInputSubmitted(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || _currentNpc == null)
            {
                MBTextManager.SetTextVariable(ResponseVar, "...", false);
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

                MainThreadDispatcher.Enqueue(() => MBTextManager.SetTextVariable(ResponseVar, reply, false));
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                MainThreadDispatcher.Enqueue(() =>
                {
                    MBTextManager.SetTextVariable(ResponseVar, "(...I cannot find the words. " + message + ")", false);
                    InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + message));
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
