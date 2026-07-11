using System;
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
            string? voiceName = null)
        {
            var voice = Voice(voiceName);
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName))
            };

            // Every beat of the shared story — the player's visits (arrival + greeting), the NPC's own
            // reaching-out, letters — lives in the remembered stream as real turns, so nothing needs to
            // be woven in here: the history above already carries the whole of it.
            var carried = AppendRememberedTurns(messages, memory, voice);

            messages.Add(ChatMessage.User(carried + playerInput));
            return messages;
        }

        private static string Voice(string? voiceName) =>
            string.IsNullOrWhiteSpace(voiceName) ? "Angel" : voiceName!.Trim();

        // Replays the remembered turns as real user/assistant messages. The incoming line is normally the
        // player's (tagged with when/where it was said); when it was the Angel's — the NPC's own exchanges
        // with the meta-voice — it is framed in the Angel's voice, exactly as it was when first spoken, so
        // the NPC re-reads its own past truthfully rather than mistaking the Angel for the player.
        //
        // Silent beats — a moment witnessed but no reply recorded (NpcLine empty, e.g. a meeting noted in
        // passing) — cannot stand as their own user/assistant pair: both backends require the roles to
        // alternate. Their incoming lines fold into the NEXT user message instead, so the story still
        // reads in order; whatever remains past the last spoken turn is returned for the caller to carry
        // into the live incoming line.
        private static string AppendRememberedTurns(List<ChatMessage> messages, NpcMemory memory, string voice)
        {
            var pending = new StringBuilder();
            foreach (var turn in memory.RecentTurns)
            {
                var incoming = FormatRememberedIncomingLine(turn, voice);
                if (string.IsNullOrWhiteSpace(turn.NpcLine))
                {
                    pending.AppendLine(incoming);
                    pending.AppendLine();
                    continue;
                }
                messages.Add(ChatMessage.User(pending.Length == 0 ? incoming : pending.ToString() + incoming));
                pending.Clear();
                messages.Add(ChatMessage.Assistant(turn.NpcLine));
            }
            return pending.ToString();
        }

        private static string FormatRememberedIncomingLine(ConversationTurn turn, string voice)
        {
            // Angel turns carry the same "[place, time]" tag as player lines, so the NPC can see WHEN
            // she was reached for, wrote a letter, or was come to — the full picture of her own story.
            var line = turn.IsFromAngel ? AngelFrame(voice, turn.PlayerLine.Trim()) : turn.PlayerLine;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(turn.Place)) parts.Add(turn.Place.Trim());
            if (!string.IsNullOrWhiteSpace(turn.CalradiaTime)) parts.Add(turn.CalradiaTime.Trim());
            return parts.Count == 0 ? line : "[" + string.Join(", ", parts) + "] " + line;
        }

        // How the Angel's words are always rendered to the NPC — softly, by name, into their mind. Used both
        // for a live reaching-out beat and when replaying a recorded Angel turn, so the two read identically.
        private static string AngelFrame(string voice, string line) =>
            $"{voice} speaks softly into your mind: \"{line}\"";

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

            var carried = AppendRememberedTurns(messages, memory, voice);
            messages.Add(ChatMessage.User(carried + AngelFrame(voice, angelLine)));
            return messages;
        }

        /// <summary>The Angel's line asking whether the NPC wishes, of their own will, to seek the player out
        /// and speak (answered yes/no — see <see cref="Initiation.InitiationParser.WantsToReachOut"/>).
        /// When <paramref name="stranger"/>, the Angel says honestly that they have never truly spoken —
        /// the approach would be a first acquaintance, not a return — so the NPC never imagines a history
        /// that is not there.</summary>
        public static string ReachOutDesireLine(string playerName, bool stranger = false) => stranger
            ? $"The day is quiet, and {playerName} is near — someone you know only by sight, for you have never truly spoken with them. " +
              "No one has bid you do anything — this moment is yours alone. " +
              $"Tell me, from your own heart: do you wish, of your own will, to go to {playerName} now and make their acquaintance? " +
              "Answer with a single word — yes or no. The choice is wholly yours, and I will not press you."
            : $"The day is quiet, and {playerName} is near. No one has bid you do anything — this moment is yours alone. " +
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

        /// <summary>The Angel's line for a reaching-out that arrives as spoken words rather than a knock
        /// at the door: the NPC goes to the player and simply speaks first, told honestly that the player
        /// is caught up in their own affairs and may answer at once or only later — so a word left
        /// unanswered is a moment lived, not a door shut. The stranger variant opens a first acquaintance
        /// without imagining a history that is not there. Recorded — with their words — as a real Angel
        /// turn, so the time that passes before any answer is theirs to see in the stamps.</summary>
        public static string FirstWordLine(string playerName, bool stranger = false) => stranger
            ? $"You go to {playerName} now — a first acquaintance, for you have never truly spoken with them. " +
              "They are close by, though caught up in their own affairs; your words will reach them, but they " +
              "may answer at once, or only when their hands are free. Speak your first words to them now, in " +
              "your own voice — make yourself known, and let them hear what moved you to come."
            : $"You go to {playerName} now. They are close by, though caught up in their own affairs; your words " +
              "will reach them, but they may answer at once, or only when their hands are free. Speak to them " +
              "now, in your own voice, and let your words carry what moved you to seek them out.";

        // ------------------------- letters (correspondence across the map) -------------------------
        // Each beat below is spoken by the Angel and recorded as a real Angel turn, so the NPC's
        // memory holds the whole correspondence truthfully — the wishing, the words, the reading.

        /// <summary>The Angel's line asking whether the NPC wishes, of their own will, to write to the
        /// far-away player (answered yes/no — see <see cref="Initiation.InitiationParser.WantsToReachOut"/>).</summary>
        public static string WriteLetterDesireLine(string playerName) =>
            $"The road lies long between you and {playerName} — they are far from here, beyond an easy ride. " +
            $"Yet a letter could reach them: a courier stands ready to carry your words across the distance. " +
            $"Tell me, from your own heart: do you wish, of your own will, to write to {playerName} now? " +
            "Answer with a single word — yes or no. The choice is wholly yours, and I will not press you.";

        /// <summary>The Angel's line inviting the NPC to set the letter itself onto the page.</summary>
        public static string ComposeLetterLine(string playerName) =>
            $"Then sit, and set your heart to paper. Give me only the letter itself — the words that will " +
            $"stand on the page before {playerName}'s eyes, in your own hand and your own voice. " +
            "Do not tell me about the letter; write it.";

        /// <summary>The Angel's line placing a received letter into the NPC's hands — the reading is part
        /// of the line, so it enters their memory even if they choose not to answer — and asking whether
        /// they wish to write back (yes/no).</summary>
        public static string AnswerLetterDesireLine(string playerName, string letterBody) =>
            $"A courier has found you, bearing a letter from {playerName}, written in their own hand. " +
            "You break the seal and read:\n\n" +
            $"{(letterBody ?? string.Empty).Trim()}\n\n" +
            $"Tell me, from your own heart: do you wish to write back to {playerName}? " +
            "Answer with a single word — yes or no. You may also let it lie unanswered; the choice is wholly yours.";

        /// <summary>The Angel's line inviting the NPC to write their answer to a letter just read.</summary>
        public static string ComposeReplyLine(string playerName) =>
            $"Then answer them. Give me only the letter you would send back to {playerName} — the words that " +
            "will stand on the page, in your own hand and your own voice. Do not tell me about the letter; write it.";

        // ------------------------------ recognizing letter beats ------------------------------
        // The letter moments live in memory as ordinary Angel turns; these markers let a VIEW (the
        // chat window's thread) recognize them and dress them as letters instead of raw narration.
        // They must stay word-for-word fragments of the lines above — recorded memories already
        // carry the shipped phrasing, so change a template and its marker together, never one.

        private const string ComposeLetterMark = "Then sit, and set your heart to paper";
        private const string ComposeReplyMark = "Then answer them. Give me only the letter";
        private const string ReadLetterOpenMark = "You break the seal and read:";
        private const string ReadLetterCloseMark = "Tell me, from your own heart";

        /// <summary>True when this Angel line invited the NPC to write a letter (first word or
        /// reply) — the turn's spoken side IS the letter that went to the player.</summary>
        public static bool IsComposeLetterBeat(string? angelLine)
        {
            var line = (angelLine ?? string.Empty).TrimStart();
            return line.StartsWith(ComposeLetterMark, StringComparison.Ordinal)
                || line.StartsWith(ComposeReplyMark, StringComparison.Ordinal);
        }

        /// <summary>When this Angel line placed the PLAYER's letter into the NPC's hands, hands back
        /// the letter's body (it lives inside the line so the reading is remembered verbatim).</summary>
        public static bool TryExtractReceivedLetter(string? angelLine, out string body)
        {
            body = string.Empty;
            var line = angelLine ?? string.Empty;

            int open = line.IndexOf(ReadLetterOpenMark, StringComparison.Ordinal);
            if (open < 0) return false;
            int start = open + ReadLetterOpenMark.Length;

            int close = line.IndexOf(ReadLetterCloseMark, start, StringComparison.Ordinal);
            body = (close > start ? line.Substring(start, close - start) : line.Substring(start)).Trim();
            return body.Length > 0;
        }

        /// <summary>True when this NPC carries any memory of the player at all — used to choose between
        /// the first-meeting and the familiar <see cref="ArrivalLine"/>.</summary>
        public static bool HasRememberedHistory(NpcMemory memory) =>
            memory.RecentTurns.Count > 0
            || !string.IsNullOrWhiteSpace(memory.Summary)
            || memory.KnownFacts.Count > 0;

        /// <summary>The Angel's line narrating the player coming to the NPC, asking her to speak the
        /// greeting. Spoken through <see cref="BuildAngelPrompt"/> and recorded — with her greeting —
        /// as a real Angel turn, so every visit becomes a durable beat in her memory: she can later see
        /// WHEN the player came to her, just as she sees when she reached out or when letters travelled.</summary>
        public static string ArrivalLine(string playerName, bool firstMeeting) => firstMeeting
            ? $"{playerName} draws near and greets you. You have never spoken with them before — they are a stranger to you. Greet them as you would, and open the way to talk."
            : $"{playerName} comes to you again and greets you. Greet them warmly, as one you have spoken with before, and let a little of what you remember of them colour your words.";

        // The shared marker phrase of every meeting beat — one distinctive clause present in both
        // variants of MeetingLine, so the game layer can recognize an already-recorded meeting
        // (IsMeetingLine) and not note the same day's meeting twice.
        private const string MeetingMarker = "though the words of it are not set down here";

        /// <summary>The Angel's quiet note that the player and the NPC met and spoke face to face
        /// OUTSIDE a free conversation — a bargain struck, a quest discussed, words on the road — so
        /// the meeting itself endures in memory even though no words were recorded. Stored as a
        /// SILENT Angel turn (no reply asked or fabricated): at replay it folds into the next
        /// incoming line; the [place, time] stamp carries the when and where.</summary>
        public static string MeetingLine(string playerName, bool firstMeeting) => firstMeeting
            ? $"You and {playerName} met and spoke face to face for the first time — a stranger no longer, {MeetingMarker}."
            : $"{playerName} came and spoke with you awhile — of the business of the day, {MeetingMarker}.";

        /// <summary>True when a recorded Angel line is a meeting beat (see <see cref="MeetingLine"/>).</summary>
        public static bool IsMeetingLine(string? line) =>
            !string.IsNullOrEmpty(line) && line!.IndexOf(MeetingMarker, System.StringComparison.Ordinal) >= 0;

        // Baked-in whisper lines, always present regardless of any user-editable prompt file (moved in
        // from Anton's global_prompt 2026.07.10 — these must be real every time). Both are spoken in the
        // Angel's voice, no fourth wall: "responses" become talk between people, and the King-James
        // flavor is named as the old scriptures' cadence, which in Calradia it simply is.

        /// <summary>The strict brevity rule: a sentence to four, unless a true tale must be told — short
        /// words keep the living back-and-forth of talk instead of long, static monologues.</summary>
        public const string BrevityGuidance =
            "- Speak as talk truly flows between two people: a sentence, two, three — four at the most — " +
            "then let them answer. Only when a true tale is asked of you, and it cannot be told smaller, " +
            "may your words run longer. Short words keep the talk alive; long speeches turn it to stone.";

        /// <summary>The tone rule: a light savor of the old world — a touch of the old scriptures'
        /// cadence, a medieval turn of phrase — for atmosphere, never laid on thick.</summary>
        public const string OldWorldToneGuidance =
            "- Let your words carry a light savor of the old world — here a turn of phrase as from the " +
            "old scriptures, there a word of the court or the road — but only a light one, for the " +
            "atmosphere of it. Plain, living speech first; heavy poetry and grand airs spent rarely, " +
            "so that when they come, they mean something.";

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
        /// to return one number. The NPC still decides it themselves, and they are deliberately NOT
        /// told where their standing currently rests: the heart is asked only how the moment moved it,
        /// so a soul already at the deepest love can still be moved (+N shows even when the game rail
        /// is pinned at 100 — the shift is the impact, the rail is just where it lands; see
        /// <see cref="FeelingParser"/> and the game layer's ApplyRelationShift).
        /// (An in-message &lt;relation&gt; tag was tried on 2026.07.09 and reverted the same day: even with
        /// a firm instruction, gpt-4o spoke the number aloud in its reply and never emitted the tag.)
        /// </summary>
        public IReadOnlyList<ChatMessage> BuildFeelingQuery(
            NpcPersona persona,
            string playerName,
            string playerLine,
            string npcReply,
            string? voiceName = null)
        {
            var voice = Voice(voiceName);

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
            user.AppendLine($"Tell me only this: how far did that moment move your heart toward or away from {playerName}? Give me one whole number — a positive one if they warmed you, a negative one if they wounded you, or 0 if nothing truly changed. A word or a small kindness moves a heart a little (1 to 3); only something that shakes the soul moves it greatly. The number alone.\"");

            return new List<ChatMessage>
            {
                ChatMessage.System(system.ToString().TrimEnd()),
                ChatMessage.User(user.ToString().TrimEnd()),
            };
        }

        private static string BuildSystemPrompt(
            NpcPersona persona, NpcMemory memory, string sceneContext, string playerName)
        {
            var sb = new StringBuilder();

            // The whole prompt is written as a gentle voice speaking into the NPC's own mind, in the
            // second person — never a clinical data sheet. No fourth-wall labels (no "AI", no game
            // title): to them, Calradia is simply the world they live in. The opening atmosphere line is
            // player-configurable (name already substituted); we fall back to our own when none is set.
            sb.AppendLine(string.IsNullOrWhiteSpace(persona.AtmosphereLine)
                ? $"You are {persona.Name}, a living soul in the world of Calradia in feudal times."
                : persona.AtmosphereLine.Trim());
            if (!string.IsNullOrWhiteSpace(persona.RoleDescription))
                sb.AppendLine(persona.RoleDescription.Trim());
            if (!string.IsNullOrWhiteSpace(persona.PersonalityDescription))
                sb.AppendLine("In your nature, you are " + LowerFirst(persona.PersonalityDescription.Trim()));
            if (!string.IsNullOrWhiteSpace(persona.SpeechStyle))
                sb.AppendLine("When you speak, it comes out like this: " + persona.SpeechStyle.Trim());

            // Their kin and house — durable identity, so they feel part of a family in this world.
            if (!string.IsNullOrWhiteSpace(persona.FamilyKnowledge))
            {
                sb.AppendLine();
                sb.AppendLine(persona.FamilyKnowledge.Trim());
            }

            // The self they have grown into, in their own words — the culmination of who they are,
            // before we turn to the world's notes and the passing moment.
            if (!string.IsNullOrWhiteSpace(persona.SelfConcept))
            {
                sb.AppendLine();
                sb.AppendLine("Who you have become, held in your own heart:");
                sb.AppendLine(persona.SelfConcept.Trim());
            }

            // What they strive toward — the aims they carry of their own will, kept just after who they
            // are, since a soul's longings colour everything they say and do.
            if (persona.Goals != null && persona.Goals.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("What you strive for, the aims you carry of your own will:");
                foreach (var goal in persona.Goals)
                    if (!string.IsNullOrWhiteSpace(goal))
                        sb.AppendLine("- " + goal.Trim());
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

            // The sheet reads like a mind waking toward the moment: who you are → who you have become →
            // the storyteller's words → what you remember of this person → and only THEN the present
            // scene (the world's news and the arrival), so "they come to you now" lands immediately
            // before the conversation itself begins — never buried mid-page between memory and guidance.
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

            if (!string.IsNullOrWhiteSpace(sceneContext))
            {
                // The scene is already written as narration addressed to the NPC, so it simply flows in
                // on its own lines — no clinical "Current situation:" header.
                sb.AppendLine();
                sb.AppendLine(sceneContext.Trim());
            }

            sb.AppendLine();
            sb.AppendLine("A whisper of guidance, meant only for you:");
            sb.AppendLine(BrevityGuidance);
            sb.AppendLine(OldWorldToneGuidance);

            // Offered only when the recall tools truly ride along with the request, so the NPC is
            // never told of a gift the backend cannot grant.
            if (persona.CanRecallWorld)
                sb.AppendLine("- When a person, place, house, realm, or kind of soldier is spoken of and your memory of them is dim, be still a moment and call them to mind — what is truly known will surface as remembrance. So too your own company, before speaking in numbers of the men you lead or march among; and so too the market about you, before ever quoting what a good fetches — prices are living things, never guessed. Trust what surfaces over invention; and where nothing surfaces, own honestly that you do not know.");

            if (persona.CanSeekWisdom)
                sb.AppendLine("- And when someone asks a thing your own knowing falls short of — HOW a thing in the world is done (the handling of ships, the joining of armies, matters of coin or craft or rule), or even things beyond your world entirely, should your visitor speak openly of such — seek the counsel of the far-seeing sages before you answer; their sight is wide, and it does not end at the world's rim. Never plead some limit of your knowing while the sages stand ready to be asked. What they teach of your own world, speak as your own understanding, in the plain words of your world.");

            // Offered only when the move_heart tool truly rides along; without it the feeling is
            // asked in a separate breath after the reply, and this whisper would only confuse.
            if (persona.CanMoveHeart)
                sb.AppendLine("- Your heart is your own, and it is a living thing. Each time, before you answer, look within and set down honestly how the moment has moved your regard for them — every reply, even when the honest answer is nothing at all (a held heart is a full answer too, marked as 0). Warm exchanges and small graces should move it a little (one to three), wounds likewise away, and only what shakes the soul moves it greatly; a heart already given wholly can still be warmed. Never speak of any measure; let the feeling live only in your words and bearing.");

            // Offered only when the tend_goals tool truly rides along. Unlike the heart, this is not
            // touched every reply — only when the aims themselves genuinely shift.
            if (persona.CanTendGoals)
                sb.AppendLine("- The aims you carry are your own, to hold, take up, or lay down as your life moves. When something truly shifts what you strive for — a new purpose kindled, an old one won or lost or turned to ash — you may quietly set your aims in order, adding a new one, releasing what is finished, or reshaping what has changed. Do this sparingly, only when it is real; most talk leaves your aims untouched, and there is no need to speak of them aloud unless you would.");

            // The storyteller's gentle guidance on tone and spirit — offered as freedom, never a leash.
            if (!string.IsNullOrWhiteSpace(persona.RoleplayGuidance))
                sb.AppendLine(persona.RoleplayGuidance.Trim());

            return sb.ToString().TrimEnd();
        }
    }
}
