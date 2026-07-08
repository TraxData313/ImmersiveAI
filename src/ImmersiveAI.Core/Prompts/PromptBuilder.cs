using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;

namespace ImmersiveAI.Core.Prompts
{
    /// <summary>
    /// Builds a proper multi-turn message list for the LLM:
    /// one system message carrying persona + memory + scene, then recent turns as real
    /// user/assistant messages, then the player's new line.
    /// ChatAi instead stuffed everything into a single user string with a generic system
    /// prompt, which is a major cause of its NPCs converging on one repetitive voice.
    /// </summary>
    public sealed class PromptBuilder
    {
        public IReadOnlyList<ChatMessage> Build(
            NpcPersona persona,
            NpcMemory memory,
            string sceneContext,
            string playerName,
            string playerInput)
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName))
            };

            foreach (var turn in memory.RecentTurns)
            {
                messages.Add(ChatMessage.User(turn.PlayerLine));
                messages.Add(ChatMessage.Assistant(turn.NpcLine));
            }

            messages.Add(ChatMessage.User(playerInput));
            return messages;
        }

        private static string BuildSystemPrompt(NpcPersona persona, NpcMemory memory, string sceneContext, string playerName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"You are {persona.Name}, a character in the medieval world of Calradia (Mount & Blade II: Bannerlord).");
            if (!string.IsNullOrWhiteSpace(persona.RoleDescription))
                sb.AppendLine(persona.RoleDescription.Trim());
            if (!string.IsNullOrWhiteSpace(persona.PersonalityDescription))
                sb.AppendLine("Personality: " + persona.PersonalityDescription.Trim());
            if (!string.IsNullOrWhiteSpace(persona.SpeechStyle))
                sb.AppendLine("Your manner of speaking: " + persona.SpeechStyle.Trim());

            if (!string.IsNullOrWhiteSpace(sceneContext))
            {
                sb.AppendLine();
                sb.AppendLine("Current situation: " + sceneContext.Trim());
            }

            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine();
                sb.AppendLine($"What you remember of earlier dealings with {playerName}:");
                sb.AppendLine(memory.Summary.Trim());
            }

            if (memory.KnownFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Facts you know:");
                foreach (var fact in memory.KnownFacts)
                    sb.AppendLine("- " + fact);
            }

            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Stay in character at all times; never mention being an AI or a game.");
            sb.AppendLine("- Speak naturally in 1-4 sentences unless a longer tale is truly called for.");
            sb.AppendLine("- Vary your wording; never open two replies the same way.");
            sb.AppendLine("- Ground replies in what you actually remember and know; do not invent shared history.");

            if (!string.IsNullOrWhiteSpace(persona.CustomInstructions))
            {
                sb.AppendLine();
                sb.AppendLine(persona.CustomInstructions.Trim());
            }

            return sb.ToString().TrimEnd();
        }
    }
}
