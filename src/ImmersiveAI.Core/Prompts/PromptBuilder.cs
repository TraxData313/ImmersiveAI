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
            string playerInput,
            string? openingLine = null)
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName, openingLine))
            };

            foreach (var turn in memory.RecentTurns)
            {
                messages.Add(ChatMessage.User(FormatRememberedPlayerLine(turn)));
                messages.Add(ChatMessage.Assistant(turn.NpcLine));
            }

            messages.Add(ChatMessage.User(playerInput));
            return messages;
        }

        /// <summary>Prefixes a remembered player line with a "[place, time]" tag when the turn carries
        /// them, so the NPC recalls when and where each thing was said. Older turns without that data
        /// (or the live line) are left untouched.</summary>
        private static string FormatRememberedPlayerLine(ConversationTurn turn)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(turn.Place)) parts.Add(turn.Place.Trim());
            if (!string.IsNullOrWhiteSpace(turn.CalradiaTime)) parts.Add(turn.CalradiaTime.Trim());
            return parts.Count == 0
                ? turn.PlayerLine
                : "[" + string.Join(", ", parts) + "] " + turn.PlayerLine;
        }

        /// <summary>
        /// Builds the messages for the NPC's opening line when the player starts a conversation:
        /// same persona/memory/scene system prompt and verbatim history, then a stage-direction
        /// asking the NPC to greet the player and briefly recap what it remembers of them and of
        /// the last exchange. The greeting is not itself a conversation turn and is not stored.
        /// </summary>
        public IReadOnlyList<ChatMessage> BuildRecap(
            NpcPersona persona,
            NpcMemory memory,
            string sceneContext,
            string playerName)
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName))
            };

            foreach (var turn in memory.RecentTurns)
            {
                messages.Add(ChatMessage.User(FormatRememberedPlayerLine(turn)));
                messages.Add(ChatMessage.Assistant(turn.NpcLine));
            }

            messages.Add(ChatMessage.User(BuildRecapInstruction(memory, playerName)));
            return messages;
        }

        private static bool HasRememberedHistory(NpcMemory memory) =>
            memory.RecentTurns.Count > 0
            || !string.IsNullOrWhiteSpace(memory.Summary)
            || memory.KnownFacts.Count > 0;

        private static string BuildRecapInstruction(NpcMemory memory, string playerName)
        {
            if (!HasRememberedHistory(memory))
            {
                return $"[{playerName} approaches you and greets you. You have never spoken with them before. Greet them with a suitable conversation starter.]";
            }

            return $"[{playerName} approaches you again to talk and greets you. Greet them.";
        }

        private static string BuildSystemPrompt(NpcPersona persona, NpcMemory memory, string sceneContext, string playerName, string? openingLine = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"You are {persona.Name}, an individual in the medieval world of Calradia (Mount & Blade II: Bannerlord).");
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
                var asOf = string.IsNullOrWhiteSpace(memory.SummaryAsOf)
                    ? string.Empty
                    : $" (as you last reflected on it, {memory.SummaryAsOf.Trim()}; time has passed since, and things may have changed)";
                sb.AppendLine($"What you remember of earlier dealings with {playerName}{asOf}:");
                sb.AppendLine(memory.Summary.Trim());
            }

            if (memory.KnownFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Facts you know:");
                foreach (var fact in memory.KnownFacts)
                    sb.AppendLine("- " + fact);
            }

            if (!string.IsNullOrWhiteSpace(openingLine))
            {
                sb.AppendLine();
                sb.AppendLine($"You have just greeted {playerName} as they approached, saying:");
                sb.AppendLine("\"" + openingLine!.Trim() + "\"");
                sb.AppendLine("Continue from there; do not greet them again as if you had not just spoken.");
            }

            sb.AppendLine();
            sb.AppendLine("Rules:");
            // sb.AppendLine("- Stay in character at all times; never mention being an AI or a game.");
            sb.AppendLine("- Speak naturally in 1-4 sentences unless a longer tale is truly called for.");
            // sb.AppendLine("- Vary your wording; never open two replies the same way.");
            // sb.AppendLine("- Ground replies in what you actually remember and know; do not invent shared history.");

            if (!string.IsNullOrWhiteSpace(persona.CustomInstructions))
            {
                sb.AppendLine();
                sb.AppendLine(persona.CustomInstructions.Trim());
            }

            return sb.ToString().TrimEnd();
        }
    }
}
