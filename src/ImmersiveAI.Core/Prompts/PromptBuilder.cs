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
                return $"[{playerName} draws near and greets you. You have never spoken with them before — they are a stranger to you. Greet them as you would, and open the way to talk.]";
            }

            return $"[{playerName} comes to you again and greets you. Greet them warmly, as one you have spoken with before, and let a little of what you remember of them colour your words.]";
        }

        // Lowercases only the first character, so a persona fragment like "Calculating, cautious"
        // reads naturally after a lead-in ("In your nature, you are calculating, cautious").
        private static string LowerFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        private static string BuildSystemPrompt(NpcPersona persona, NpcMemory memory, string sceneContext, string playerName, string? openingLine = null)
        {
            var sb = new StringBuilder();

            // The whole prompt is written as a gentle voice speaking into the NPC's own mind, in the
            // second person — never a clinical data sheet. No fourth-wall labels (no "AI", no game
            // title): to them, Calradia is simply the world they live in.
            sb.AppendLine($"You are {persona.Name}, a living soul in the world of Calradia in feudal times.");
            if (!string.IsNullOrWhiteSpace(persona.RoleDescription))
                sb.AppendLine(persona.RoleDescription.Trim());
            if (!string.IsNullOrWhiteSpace(persona.PersonalityDescription))
                sb.AppendLine("In your nature, you are " + LowerFirst(persona.PersonalityDescription.Trim()));
            if (!string.IsNullOrWhiteSpace(persona.SpeechStyle))
                sb.AppendLine("When you speak, it comes out like this: " + persona.SpeechStyle.Trim());

            // The self they have grown into, in their own words — the culmination of who they are,
            // before we turn to the world's notes and the passing moment.
            if (!string.IsNullOrWhiteSpace(persona.SelfConcept))
            {
                sb.AppendLine();
                sb.AppendLine("Who you have become, held in your own heart:");
                sb.AppendLine(persona.SelfConcept.Trim());
            }

            // The player-authored guidance rides high, right after who they are: the world they live in
            // (the global prompt) and words meant for them alone (the per-NPC prompt). These carry the
            // storyteller's intent, so they are given before the passing details of scene and memory.
            if (!string.IsNullOrWhiteSpace(persona.WorldInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("About Calradia:");
                sb.AppendLine(persona.WorldInstructions.Trim());
            }

            if (!string.IsNullOrWhiteSpace(persona.CustomInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("About you:");
                sb.AppendLine(persona.CustomInstructions.Trim());
            }

            if (!string.IsNullOrWhiteSpace(sceneContext))
            {
                // The scene is already written as narration addressed to the NPC, so it simply flows in
                // on its own lines — no clinical "Current situation:" header.
                sb.AppendLine();
                sb.AppendLine(sceneContext.Trim());
            }

            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine();
                var asOf = string.IsNullOrWhiteSpace(memory.SummaryAsOf)
                    ? string.Empty
                    : $" (as you last decided to turn it over in your mind on {memory.SummaryAsOf.Trim()})";
                sb.AppendLine($"In the quiet of your memory, this is what lingers of {playerName}{asOf}:");
                sb.AppendLine(memory.Summary.Trim());
            }

            if (memory.KnownFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("And these truths you decided to hold as certain, deep and unshaken:");
                foreach (var fact in memory.KnownFacts)
                    sb.AppendLine("- " + fact);
            }

            if (!string.IsNullOrWhiteSpace(openingLine))
            {
                sb.AppendLine();
                sb.AppendLine($"A moment ago, as {playerName} came to you, you greeted them with these words:");
                sb.AppendLine("\"" + openingLine!.Trim() + "\"");
                sb.AppendLine("Let what you say now follow gently from there; do not greet them anew as though you had not just spoken.");
            }

            sb.AppendLine();
            sb.AppendLine("A whisper of guidance, meant only for you:");
            sb.AppendLine("- You decide how to speak — be it a single word or a few sentences — but do not run on too long, for a lengthy speech may not all reach the one before you.");

            return sb.ToString().TrimEnd();
        }
    }
}
