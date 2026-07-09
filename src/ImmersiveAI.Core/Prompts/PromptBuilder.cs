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
            string? openingLine = null,
            string? voiceName = null)
        {
            var voice = Voice(voiceName);
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName))
            };

            AppendRememberedTurns(messages, memory, voice);

            // A recap greeting (the NPC's opening when the player walks up) is ephemeral — not a stored turn —
            // so it is woven in here as a real assistant message, otherwise the NPC would treat the player's
            // reply as the first thing said and greet anew. A tiny stage-direction fills the user slot before
            // it, since the models cannot lead with an assistant turn. (Reaching-out greetings need none of
            // this: they are recorded as real Angel turns and already sit in the history above.)
            if (!string.IsNullOrWhiteSpace(openingLine))
            {
                messages.Add(ChatMessage.User($"[{playerName} came to you, and you were the first to speak, greeting them:]"));
                messages.Add(ChatMessage.Assistant(openingLine!.Trim()));
            }

            messages.Add(ChatMessage.User(playerInput));
            return messages;
        }

        private static string Voice(string? voiceName) =>
            string.IsNullOrWhiteSpace(voiceName) ? "Angel" : voiceName!.Trim();

        // Replays the remembered turns as real user/assistant messages. The incoming line is normally the
        // player's (tagged with when/where it was said); when it was the Angel's — the NPC's own exchanges
        // with the meta-voice — it is framed in the Angel's voice, exactly as it was when first spoken, so
        // the NPC re-reads its own past truthfully rather than mistaking the Angel for the player.
        private static void AppendRememberedTurns(List<ChatMessage> messages, NpcMemory memory, string voice)
        {
            foreach (var turn in memory.RecentTurns)
            {
                messages.Add(ChatMessage.User(FormatRememberedIncomingLine(turn, voice)));
                messages.Add(ChatMessage.Assistant(turn.NpcLine));
            }
        }

        private static string FormatRememberedIncomingLine(ConversationTurn turn, string voice)
        {
            if (turn.IsFromAngel)
                return AngelFrame(voice, turn.PlayerLine.Trim());

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(turn.Place)) parts.Add(turn.Place.Trim());
            if (!string.IsNullOrWhiteSpace(turn.CalradiaTime)) parts.Add(turn.CalradiaTime.Trim());
            return parts.Count == 0
                ? turn.PlayerLine
                : "[" + string.Join(", ", parts) + "] " + turn.PlayerLine;
        }

        // How the Angel's words are always rendered to the NPC — softly, by name, into their mind. Used both
        // for a live reaching-out beat and when replaying a recorded Angel turn, so the two read identically.
        private static string AngelFrame(string voice, string line) =>
            $"{voice} speaks softly into your mind: \"{line}\"";

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
            string playerName,
            string? voiceName = null)
        {
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName))
            };

            AppendRememberedTurns(messages, memory, Voice(voiceName));
            messages.Add(ChatMessage.User(BuildRecapInstruction(memory, playerName)));
            return messages;
        }

        /// <summary>
        /// Builds an exchange in which the Angel speaks a given line into the NPC's mind and the NPC answers.
        /// This is the shape of every reaching-out beat — the Angel asking whether they wish to go to the
        /// player (<see cref="ReachOutDesireLine"/>), then narrating the approach and its welcome
        /// (<see cref="ApproachLine"/>). The NPC's reply to each is recorded as a real Angel turn, so their
        /// whole exchange with the meta-voice lives in the same remembered stream — never hidden from them.
        /// The caller stores <paramref name="angelLine"/> verbatim as the turn's incoming line; here it is
        /// framed in the Angel's voice, identically to how <see cref="AppendRememberedTurns"/> will replay it.
        /// </summary>
        public IReadOnlyList<ChatMessage> BuildAngelPrompt(
            NpcPersona persona,
            NpcMemory memory,
            string sceneContext,
            string playerName,
            string angelLine,
            string? voiceName = null)
        {
            var voice = Voice(voiceName);
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName))
            };

            AppendRememberedTurns(messages, memory, voice);
            messages.Add(ChatMessage.User(AngelFrame(voice, angelLine)));
            return messages;
        }

        /// <summary>The Angel's line asking whether the NPC wishes, of their own will, to seek the player out
        /// and speak (answered yes/no — see <see cref="Initiation.InitiationParser.WantsToReachOut"/>).</summary>
        public static string ReachOutDesireLine(string playerName) =>
            $"The day is quiet, and {playerName} is near. No one has bid you do anything — this moment is yours alone. " +
            $"Tell me, from your own heart: do you wish, of your own will, to go to {playerName} now and speak with them? " +
            "Answer with a single word — yes or no. The choice is wholly yours, and I will not press you.";

        /// <summary>The Angel's line narrating the NPC crossing to the player: when <paramref name="welcomed"/>
        /// the player turns to them gladly and they greet first; otherwise the player is too busy just now and
        /// the NPC answers that moment however their heart moves them (they may resolve to come another time).</summary>
        public static string ApproachLine(string playerName, bool welcomed) => welcomed
            ? $"You rise and go to {playerName}. Seeing you come, they turn to you gladly and give you their whole attention. " +
              $"Speak first now, in your own voice and to their face — greet {playerName}, and let your words carry what moved you to seek them out."
            : $"You rise and go to {playerName}, but as you near, they raise an apologetic hand — they are too caught up just now to speak with you. " +
              "This moment is still yours: say or do what is in your heart, here and now.";

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

        /// <summary>
        /// Builds the tiny, isolated call that asks the NPC — in the Angel's gentle voice — how the
        /// exchange just past moved their regard for the player, expecting only a single signed number
        /// back. Kept apart from the spoken reply on purpose: a chatty model will narrate a number in
        /// prose and forget any hidden mark, but it will reliably answer a question whose whole job is
        /// to return one number. The NPC still decides it themselves, with no ceiling but the -100..100
        /// rail; we only fold their answer into the game standing (see <see cref="FeelingParser"/>).
        /// </summary>
        public IReadOnlyList<ChatMessage> BuildFeelingQuery(
            NpcPersona persona,
            string playerName,
            string playerLine,
            string npcReply,
            int currentStanding,
            string? voiceName = null)
        {
            var voice = string.IsNullOrWhiteSpace(voiceName) ? "Angel" : voiceName!.Trim();

            var system = new StringBuilder();
            system.AppendLine($"You are {persona.Name}, a living soul in the world of Calradia.");
            if (!string.IsNullOrWhiteSpace(persona.PersonalityDescription))
                system.AppendLine("In your nature, you are " + LowerFirst(persona.PersonalityDescription.Trim()));
            system.AppendLine();
            system.AppendLine($"A gentle voice you trust — {voice} — speaks quietly into your mind, asking you to look honestly at your own heart. What you answer is yours alone to decide.");
            system.AppendLine("Answer with a single whole number and nothing else — no words.");

            var user = new StringBuilder();
            user.AppendLine($"{voice} whispers: \"{playerName} came to you. They said:");
            user.AppendLine($"  “{playerLine.Trim()}”");
            user.AppendLine("and from your heart you answered:");
            user.AppendLine($"  “{npcReply.Trim()}”");
            user.AppendLine();
            user.AppendLine($"As things stand, your regard for {playerName} rests at {currentStanding}, on a scale from -100 (bitter enmity) to 100 (the deepest love). Tell me only this: how far did that moment move your heart? Give me one whole number — a positive one if they warmed you, a negative one if they wounded you, or 0 if nothing truly changed. The number alone.\"");

            return new List<ChatMessage>
            {
                ChatMessage.System(system.ToString().TrimEnd()),
                ChatMessage.User(user.ToString().TrimEnd()),
            };
        }

        private static string BuildSystemPrompt(NpcPersona persona, NpcMemory memory, string sceneContext, string playerName)
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

            sb.AppendLine();
            sb.AppendLine("A whisper of guidance, meant only for you:");
            sb.AppendLine("- You decide how to speak — be it a single word or a few sentences — but do not run on too long, for a lengthy speech may not all reach the one before you.");

            return sb.ToString().TrimEnd();
        }
    }
}
