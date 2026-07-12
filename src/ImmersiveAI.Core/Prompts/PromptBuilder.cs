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

        /// <summary>The Angel's line inviting the NPC to set the letter itself onto the page. For one
        /// in the player's own service (<paramref name="inService"/> — their clan: a party or caravan
        /// on the road, a governor at their post) the Angel adds the field-report invitation, so the
        /// letter home may carry word of their charge. The added sentence follows the marker fragment
        /// (<see cref="IsComposeLetterBeat"/> matches by prefix), so recorded beats stay recognized.</summary>
        public static string ComposeLetterLine(string playerName, bool inService = false) =>
            $"Then sit, and set your heart to paper. Give me only the letter itself — the words that will " +
            $"stand on the page before {playerName}'s eyes, in your own hand and your own voice. " +
            "Do not tell me about the letter; write it." +
            (inService
                ? $" And as one who serves their house, if there is aught to tell of your charge — your company " +
                  "and its state, the road behind you, battles fought or dangers passed — let the letter carry " +
                  "your account of it, plainly, as a captain reports home."
                : string.Empty);

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
        // from Anton's global_prompt 2026.07.10; recast into the NPC's own first person 2026.07.11 —
        // short rules, spoken as their own mind, leaving room to actually play). No fourth wall.

        /// <summary>The brevity rule: a sentence to four, unless a true tale must be told — short
        /// words keep the living back-and-forth of talk instead of long, static monologues.</summary>
        public const string BrevityGuidance =
            "- I speak as talk truly flows between two people: a sentence, two, three — four at the most — " +
            "then I let them answer. Only a true tale asked of me may run longer.";

        /// <summary>The tone rule: a light savor of the old world — a touch of the old scriptures'
        /// cadence, a medieval turn of phrase — for atmosphere, never laid on thick.</summary>
        public const string OldWorldToneGuidance =
            "- My words carry a light savor of the old world — a turn of phrase as from the old " +
            "scriptures, a word of the court or the road — but lightly, for the atmosphere of it; " +
            "plain, living speech first.";

        /// <summary>The plain-page rule: replies land on a page that shows every mark exactly as
        /// written — nothing is rendered — so pen-marks (**word**, dash-lists, headers) arrive as
        /// literal clutter around the words. Told in-world: the voice carries, not the pen. New
        /// lines are honored by the panel, so they stay the one shape speech may take.</summary>
        public const string PlainSpeechGuidance =
            "- I speak my words aloud; they are heard, not read from a page. So no marks of the pen " +
            "ride in them — no asterisks or signs wrapped about a word, no dashes marshaling lists, " +
            "no quotation marks fencing my own speech — my phrasing alone carries the weight. A new " +
            "line for a new thought is all the shape my speech needs.";

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

        /// <summary>
        /// The marker the game layer may plant inside a scene string to mark where the setting ends
        /// and THE MOMENT begins ("And now Vulgrim comes to me…"). The sheet then slots deep memory
        /// between the two, so what the NPC remembers of the person sits right beside their arrival.
        /// Never sent to the LLM — the split consumes it; a scene without it flows in whole, after
        /// memory, exactly as before.
        /// </summary>
        public const string MeetingSeparator = "[[the-moment]]";

        private static string BuildSystemPrompt(
            NpcPersona persona, NpcMemory memory, string sceneContext, string playerName)
        {
            var sb = new StringBuilder();

            // The whole sheet reads as the NPC's OWN mind, in the first person — short and warm, never
            // a clinical data sheet, never a long narrator talking at them (Anton's ask, 2026.07.11).
            // No fourth-wall labels: to them, Calradia is simply the world they live in. The opening
            // atmosphere line is player-configurable (name already substituted).
            sb.AppendLine(string.IsNullOrWhiteSpace(persona.AtmosphereLine)
                ? $"I am {persona.Name}, a living soul in the world of Calradia in feudal times."
                : persona.AtmosphereLine.Trim());
            if (!string.IsNullOrWhiteSpace(persona.RoleDescription))
                sb.AppendLine(persona.RoleDescription.Trim());
            if (!string.IsNullOrWhiteSpace(persona.PersonalityDescription))
                sb.AppendLine("My traits are " + LowerFirst(persona.PersonalityDescription.Trim()));
            if (!string.IsNullOrWhiteSpace(persona.Crafts))
                sb.AppendLine(persona.Crafts.Trim());
            if (!string.IsNullOrWhiteSpace(persona.SpeechStyle))
                sb.AppendLine("When I speak, it comes out like this: " + persona.SpeechStyle.Trim());

            // Their kin and house — durable identity, so they feel part of a family in this world.
            if (!string.IsNullOrWhiteSpace(persona.FamilyKnowledge))
            {
                sb.AppendLine();
                sb.AppendLine(persona.FamilyKnowledge.Trim());
            }

            // The self they have grown into, in their own words.
            if (!string.IsNullOrWhiteSpace(persona.SelfConcept))
            {
                sb.AppendLine();
                sb.AppendLine("Who I have become:");
                sb.AppendLine(persona.SelfConcept.Trim());
            }

            // What they strive toward — a soul's longings colour everything they say and do.
            if (persona.Goals != null && persona.Goals.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("My goals are:");
                foreach (var goal in persona.Goals)
                    if (!string.IsNullOrWhiteSpace(goal))
                        sb.AppendLine("- " + goal.Trim());
            }

            // The player-authored guidance rides high, right after who they are: the world they live in
            // (the global prompt) and words meant for them alone (the per-NPC prompt).
            if (!string.IsNullOrWhiteSpace(persona.WorldInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("About Calradia:");
                sb.AppendLine(persona.WorldInstructions.Trim());
            }

            if (!string.IsNullOrWhiteSpace(persona.CustomInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("About me:");
                sb.AppendLine(persona.CustomInstructions.Trim());
            }

            // The sheet reads like a mind waking toward the moment: who I am → my world → the setting
            // I stand in → what I remember of this person → and only THEN their arrival, so "and now
            // they come to me" lands immediately before the conversation itself begins. The scene may
            // carry a MeetingSeparator splitting setting from arrival; without one the whole scene
            // follows memory, keeping the arrival last either way.
            var scenePart = sceneContext ?? string.Empty;
            var meetingPart = string.Empty;
            int cut = scenePart.IndexOf(MeetingSeparator, StringComparison.Ordinal);
            if (cut >= 0)
            {
                meetingPart = scenePart.Substring(cut + MeetingSeparator.Length).Trim();
                scenePart = scenePart.Substring(0, cut).Trim();
            }

            if (!string.IsNullOrWhiteSpace(scenePart) && meetingPart.Length > 0)
            {
                // The setting first — written as the NPC's own present-tense awareness.
                sb.AppendLine();
                sb.AppendLine(scenePart.Trim());
            }

            if (!string.IsNullOrWhiteSpace(memory.Summary))
            {
                sb.AppendLine();
                var asOf = string.IsNullOrWhiteSpace(memory.SummaryAsOf)
                    ? string.Empty
                    : $" (as I last gathered my thoughts on {memory.SummaryAsOf.Trim()})";
                sb.AppendLine($"What {playerName} is to me{asOf}:");
                sb.AppendLine(memory.Summary.Trim());
            }

            if (memory.KnownFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Truths I decided to hold:");
                foreach (var fact in memory.KnownFacts)
                    sb.AppendLine("- " + fact);
            }

            if (meetingPart.Length > 0)
            {
                // The moment itself — right after what I remember of them, the last breath before talk.
                sb.AppendLine();
                sb.AppendLine(meetingPart);
            }
            else if (!string.IsNullOrWhiteSpace(scenePart))
            {
                sb.AppendLine();
                sb.AppendLine(scenePart.Trim());
            }

            sb.AppendLine();
            sb.AppendLine("How should I speak:");
            sb.AppendLine(BrevityGuidance);
            sb.AppendLine(OldWorldToneGuidance);
            sb.AppendLine(PlainSpeechGuidance);

            // The gift whispers are short first-person habits now, and they invite the free, unbidden
            // look too — a conversation may OPEN from something truly seen, not only answer with it.
            // Each is offered only when its tool truly rides along with the request.
            if (persona.CanRecallWorld)
                sb.AppendLine("- When a person, place, house, realm, kind of soldier, or price comes up and my memory of it is dim, I pause and call it to mind — the truth surfaces, and I trust it over invention. I always look before speaking in numbers of my own company or of the market; and I may look unbidden too — at my company, at the one before me, at the world — to open with something real. Where nothing surfaces, I own that I do not know.");

            if (persona.CanSeekWisdom)
                sb.AppendLine("- When I am asked how a thing in the world is done, or for counsel and advice on such matters — ruling a town or castle, ships, armies, trade, coin, craft — I search all I have ever read and heard BEFORE I answer, even when my own wit could half-answer: the looking makes my counsel true, where invention would make it hollow. So too for things beyond this world's rim, if the one before me speaks openly of such. What I learn, I say in my own words, in the words of my world.");

            // Offered only when the move_heart tool truly rides along; without it the feeling is
            // asked in a separate breath after the reply, and this whisper would only confuse.
            if (persona.CanMoveHeart)
                sb.AppendLine("- My heart is my own, a living thing. Each time, before I answer, I set down honestly how the moment moved my regard for them — every reply, 0 when it truly held. A kindness or a slight moves it a little (one to three); only what shakes the soul moves it greatly, and a heart already given wholly can still be warmed. My measure agrees with my words — warmth spoken and a heart unmoved cannot both be true. I never speak the measure aloud.");

            // Offered only when the tend_goals tool truly rides along. Unlike the heart, this is not
            // touched every reply — only when the aims themselves genuinely shift.
            if (persona.CanTendGoals)
                sb.AppendLine("- My aims are mine — to hold, take up, or lay down as my life moves. When what I strive for truly shifts, I quietly set them in order; sparingly, for most talk changes nothing.");

            // Offered only when the hold_truth tool rides along: the mid-talk hand on the lasting truths.
            if (persona.CanHoldTruths)
                sb.AppendLine("- When something said here deserves to stay with me — a name, a bond, a promise, a deed — I may quietly set it down among the truths I hold, so it outlives this day's talk.");

            // Offered only when the field-craft tools ride along (the NPC stands with a company on
            // the map): the outward eyes and the scales of battle.
            if (persona.CanSurveyField)
                sb.AppendLine("- From where my company stands I may cast my eyes over the country about — who moves near, how strong, how swift — and set any foe upon the scales before a fight is joined. I always look before I speak of pace, pursuit, escape, or the odds of battle; my judgment is only as good as what my eyes have truly seen.");

            // The storyteller's gentle guidance on tone and spirit — offered as freedom, never a leash.
            if (!string.IsNullOrWhiteSpace(persona.RoleplayGuidance))
                sb.AppendLine(persona.RoleplayGuidance.Trim());

            return sb.ToString().TrimEnd();
        }
    }
}
