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
        // player's (tagged with when/where it was said) or the NPC's own inner mind (InnerFrame); a turn
        // recorded by the retired Angel narrator (pre-2026.08.07 saves) is still framed in the Angel's
        // voice, exactly as it was when first spoken, so the NPC re-reads its own past truthfully.
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
            var line = turn.IsFromAngel ? AngelFrame(voice, turn.PlayerLine.Trim())
                : turn.IsInnerThought ? InnerFrame(turn.PlayerLine.Trim())
                : turn.PlayerLine;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(turn.Place)) parts.Add(turn.Place.Trim());
            if (!string.IsNullOrWhiteSpace(turn.CalradiaTime)) parts.Add(turn.CalradiaTime.Trim());
            return parts.Count == 0 ? line : "[" + string.Join(", ", parts) + "] " + line;
        }

        // LEGACY REPLAY ONLY (the Angel narrator retired 2026.08.07): how a recorded Angel turn from an
        // older save is still rendered to the NPC — softly, by name, into their mind, exactly as it was
        // when first spoken. No new Angel turns are ever minted; new beats are the NPC's own inner mind.
        private static string AngelFrame(string voice, string line) =>
            $"{voice} speaks softly into your mind: \"{line}\"";

        // How the NPC's OWN inner reckonings are rendered — no voice speaks; the line is their own mind at
        // work. Same frame live and on replay, so a remembered thought reads exactly as it did when thought.
        private static string InnerFrame(string line) => $"(Within my own mind: {line})";

        /// <summary>
        /// Builds an exchange in which the NPC's OWN mind poses itself a moment to weigh or act on — no
        /// Angel, no voice speaking to them (the reach-out flow since 2026.07.26; Anton found the Angel's
        /// tenderness there bred emotional small-talk approaches). The line is framed by
        /// <see cref="InnerFrame"/> exactly as <see cref="AppendRememberedTurns"/> will replay a recorded
        /// inner turn, so live and remembered thoughts read identically. The caller records the beat with
        /// <see cref="Memory.ConversationTurn.InnerSpeaker"/> — usually storing a condensed note (the
        /// Ponder/FirstWord/Approach notes below) rather than the full working instruction.
        /// </summary>
        public IReadOnlyList<ChatMessage> BuildInnerPrompt(
            NpcPersona persona,
            NpcMemory memory,
            string sceneContext,
            string playerName,
            string innerLine,
            string? voiceName = null)
        {
            var voice = Voice(voiceName);
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(BuildSystemPrompt(persona, memory, sceneContext, playerName))
            };

            var carried = AppendRememberedTurns(messages, memory, voice);
            messages.Add(ChatMessage.User(carried + InnerFrame(innerLine)));
            return messages;
        }

        /// <summary>The NPC's own reckoning on whether to approach the player — one simple nudge, first
        /// person: is there something I want to DISCUSS with them (not merely "do I want to say hi"), the
        /// rest left wholly to their own nature and what the sheet has stirred (news, mood, trade, memory).
        /// Deliberately free of instruction about what a worthy topic is — a list there made every soul
        /// answer the same (Anton, 2026.07.27: "the AI stops being AI and becomes a program again").
        /// Answered NO or "YES: the something" (see <see cref="Initiation.InitiationParser.WantsToGo"/>).</summary>
        public static string ReachOutPonderLine(string playerName, bool stranger = false) => stranger
            ? $"I notice {playerName} nearby — someone I know only by sight, for we have never spoken. " +
              "Is there something I would discuss with them? " +
              "I decide in one line: NO — or YES: what I want to discuss."
            : $"I notice {playerName} nearby, about their own affairs. " +
              "Is there something I want to discuss with them just now? " +
              "I decide in one line: NO — or YES: what I want to discuss.";

        /// <summary>The condensed note recorded for a ponder beat (the live prompt uses the full
        /// <see cref="ReachOutPonderLine"/>; memory keeps this short truthful note plus their answer).
        /// Both variants share the <see cref="IsPonderBeat"/> prefix — keep it word-for-word.</summary>
        public static string ReachOutPonderNote(string playerName, bool stranger = false) => stranger
            ? $"I marked {playerName} nearby — a stranger to me still — and weighed whether I had true cause to cross to them. I resolved:"
            : $"I marked {playerName} nearby and weighed whether I had true cause to go to them. I resolved:";

        // The word-for-word prefix of every recorded ponder note; the chat window folds such a beat —
        // reckoning and resolution both — into one soft line of narration (nothing spoken happened).
        private const string PonderNoteMark = "I marked ";

        /// <summary>True when this recorded inner line is a ponder note — a weighing with no spoken
        /// words in it, so views can render the whole beat as narration.</summary>
        public static bool IsPonderBeat(string? innerLine) =>
            (innerLine ?? string.Empty).TrimStart().StartsWith(PonderNoteMark, StringComparison.Ordinal);

        /// <summary>The NPC's own narration of crossing to the player after choosing an offered approach:
        /// when <paramref name="welcomed"/> the player receives them and they speak first; otherwise the
        /// player is too busy and the moment is theirs to spend.</summary>
        public static string ApproachLine(string playerName, bool welcomed, string? reason = null) => welcomed
            ? $"I rise and go to {playerName}. Seeing me come, they turn to me and give me their attention.{ReasonSentence(reason)} " +
              "I speak first now, in my own voice."
            : $"I rise and go to {playerName}, but as I near, they raise an apologetic hand — too caught up just now to speak with me. " +
              "The moment is still mine: I say or do with it what I will, here and now.";

        /// <summary>The condensed note recorded for an approach beat.</summary>
        public static string ApproachNote(string playerName, bool welcomed, string? reason = null) => welcomed
            ? $"Of my own accord I went to {playerName}{ReasonClause(reason)}; they received me, and I spoke first. My words:"
            : $"Of my own accord I went to {playerName}{ReasonClause(reason)}, but they were too caught up to speak with me just then. In that moment:";

        /// <summary>The NPC's own narration for a reaching-out that arrives as spoken words: they cross
        /// to the player and speak first — carrying what they resolved to discuss — knowing the answer
        /// may come at once or only later. The stranger variant states only the fact: they have never
        /// spoken (no imagined history; how to open is their own affair).</summary>
        public static string FirstWordLine(string playerName, bool stranger = false, string? reason = null) => stranger
            ? $"I cross to {playerName} now — we have never spoken.{ReasonSentence(reason)} " +
              "They are caught up in their own affairs; my words will reach them, but the answer may come at once or only later. " +
              "I speak my first words now, in my own voice."
            : $"I go to {playerName} now.{ReasonSentence(reason)} " +
              "They are caught up in their own affairs; my words will reach them, but the answer may come at once or only later. " +
              "I speak now, in my own voice.";

        /// <summary>The condensed note recorded for a first-word beat — the cause rides in it, so the
        /// next ponder sees plainly what was already brought and needs no second telling.</summary>
        public static string FirstWordNote(string playerName, string? reason = null) =>
            $"Of my own accord I crossed to {playerName} and spoke first{ReasonClause(reason)}. My words:";

        private static string ReasonSentence(string? reason) =>
            string.IsNullOrWhiteSpace(reason) ? string.Empty : $" What brings me: {reason!.Trim()}.";

        private static string ReasonClause(string? reason) =>
            string.IsNullOrWhiteSpace(reason) ? string.Empty : $" — what brought me: {reason!.Trim()}";

        // ------------------------- letters (correspondence across the map) -------------------------
        // Each beat below is the NPC's OWN mind at the writing desk (first person, recorded with
        // ConversationTurn.InnerSpeaker since 2026.08.07 — the Angel narrator is retired), so the
        // NPC's memory holds the whole correspondence truthfully — the wishing, the words, the reading.

        /// <summary>The NPC's own weighing of whether they wish, of their own will, to write to the
        /// far-away player (answered yes/no — see <see cref="Initiation.InitiationParser.WantsToReachOut"/>).</summary>
        public static string WriteLetterDesireLine(string playerName) =>
            $"The road lies long between me and {playerName} — they are far from here, beyond an easy ride. " +
            $"Yet a letter could reach them: a courier stands ready to carry my words across the distance. " +
            $"Do I wish, of my own will, to write to {playerName} now? " +
            "I answer in a single word — yes or no. The choice is wholly mine, and no one presses me.";

        /// <summary>The NPC sitting down to set the letter itself onto the page, in their own first
        /// person. For one in the player's own service (<paramref name="inService"/> — their clan: a
        /// party or caravan on the road, a governor at their post) a field-report invitation is added,
        /// so the letter home may carry word of their charge. The added sentence follows the marker
        /// fragment (<see cref="IsComposeLetterBeat"/> matches by prefix), so recorded beats stay
        /// recognized.</summary>
        public static string ComposeLetterLine(string playerName, bool inService = false) =>
            $"I sit, and set my heart to paper. What I set down now is only the letter itself — the words " +
            $"that will stand on the page before {playerName}'s eyes, in my own hand and my own voice. " +
            "I do not tell about the letter; I write it." +
            (inService
                ? $" And as one who serves their house, if there is aught to tell of my charge — my company " +
                  "and its state, the road behind me, battles fought or dangers passed — I let the letter " +
                  "carry my account of it, plainly, as a captain reports home."
                : string.Empty);

        /// <summary>A received letter in the NPC's own hands — the reading is part of the line, so it
        /// enters their memory even if they choose not to answer — closing on whether they wish to
        /// write back (yes/no).</summary>
        public static string AnswerLetterDesireLine(string playerName, string letterBody) =>
            $"A courier has found me, bearing a letter from {playerName}, written in their own hand. " +
            "I break the seal and read:\n\n" +
            $"{(letterBody ?? string.Empty).Trim()}\n\n" +
            $"Do I wish to write back to {playerName}? " +
            "I answer in a single word — yes or no. I may also let it lie unanswered; the choice is wholly mine.";

        /// <summary>The NPC sitting down to write their answer to a letter just read.</summary>
        public static string ComposeReplyLine(string playerName) =>
            $"I answer them now. What I set down is only the letter I send back to {playerName} — the words " +
            "that will stand on the page, in my own hand and my own voice. I do not tell about the letter; I write it.";

        // ------------------------------ recognizing letter beats ------------------------------
        // The letter moments live in memory as ordinary recorded turns; these markers let a VIEW
        // (the chat window's thread) recognize them and dress them as letters instead of raw
        // narration. Each marker must stay a word-for-word fragment of its template — recorded
        // memories carry the phrasing they were born with forever, so the LEGACY (Angel-voiced,
        // pre-2026.08.07) fragments stay recognized beside the first-person ones. Change a live
        // template and its "Own" marker together, never one; never touch the legacy markers.

        private const string ComposeLetterMark = "Then sit, and set your heart to paper";       // legacy (Angel)
        private const string ComposeReplyMark = "Then answer them. Give me only the letter";    // legacy (Angel)
        private const string ComposeLetterMarkOwn = "I sit, and set my heart to paper";
        private const string ComposeReplyMarkOwn = "I answer them now. What I set down is only the letter";
        private const string ReadLetterOpenMark = "You break the seal and read:";               // legacy (Angel)
        private const string ReadLetterCloseMark = "Tell me, from your own heart";               // legacy (Angel)
        private const string ReadLetterOpenMarkOwn = "I break the seal and read:";
        private const string ReadLetterCloseMarkOwn = "Do I wish to write back";

        /// <summary>True when this recorded line is the NPC sitting down to write a letter (first
        /// word or reply) — the turn's spoken side IS the letter that went to the player.</summary>
        public static bool IsComposeLetterBeat(string? recordedLine)
        {
            var line = (recordedLine ?? string.Empty).TrimStart();
            return line.StartsWith(ComposeLetterMarkOwn, StringComparison.Ordinal)
                || line.StartsWith(ComposeReplyMarkOwn, StringComparison.Ordinal)
                || line.StartsWith(ComposeLetterMark, StringComparison.Ordinal)
                || line.StartsWith(ComposeReplyMark, StringComparison.Ordinal);
        }

        /// <summary>When this recorded line placed the PLAYER's letter into the NPC's hands, hands back
        /// the letter's body (it lives inside the line so the reading is remembered verbatim).</summary>
        public static bool TryExtractReceivedLetter(string? recordedLine, out string body)
        {
            body = string.Empty;
            var line = recordedLine ?? string.Empty;

            int open = line.IndexOf(ReadLetterOpenMarkOwn, StringComparison.Ordinal);
            int markLen = ReadLetterOpenMarkOwn.Length;
            if (open < 0)
            {
                open = line.IndexOf(ReadLetterOpenMark, StringComparison.Ordinal);
                markLen = ReadLetterOpenMark.Length;
            }
            if (open < 0) return false;
            int start = open + markLen;

            // The tail differs between the legacy and first-person templates; whichever close
            // phrase comes first past the body bounds it.
            int close = -1;
            foreach (var mark in new[] { ReadLetterCloseMarkOwn, ReadLetterCloseMark })
            {
                int at = line.IndexOf(mark, start, StringComparison.Ordinal);
                if (at >= 0 && (close < 0 || at < close)) close = at;
            }
            body = (close > start ? line.Substring(start, close - start) : line.Substring(start)).Trim();
            return body.Length > 0;
        }

        /// <summary>True when this NPC carries any memory of the player at all — used to choose between
        /// the first-meeting and the familiar <see cref="ArrivalLine"/>.</summary>
        public static bool HasRememberedHistory(NpcMemory memory) =>
            memory.RecentTurns.Count > 0
            || !string.IsNullOrWhiteSpace(memory.Summary)
            || memory.KnownFacts.Count > 0;

        /// <summary>The NPC's own awareness of the player coming to them, closing on the greeting
        /// being theirs to speak. Spoken through <see cref="BuildInnerPrompt"/> and recorded — with
        /// the greeting — as a real inner turn, so every visit becomes a durable beat in her memory:
        /// she can later see WHEN the player came to her, just as she sees when she reached out or
        /// when letters travelled. (Angel-narrated until 2026.08.07; recorded arrivals keep whichever
        /// phrasing they were born with.)</summary>
        public static string ArrivalLine(string playerName, bool firstMeeting) => firstMeeting
            ? $"{playerName} draws near and greets me. We have never spoken before — they are a stranger to me. I greet them as I would, and open the way to talk."
            : $"{playerName} comes to me again and greets me. I greet them as one I have spoken with before, and let a little of what I remember of them colour my words.";

        // The shared marker phrase of every meeting beat — one distinctive clause present in both
        // variants of MeetingLine, so the game layer can recognize an already-recorded meeting
        // (IsMeetingLine) and not note the same day's meeting twice.
        private const string MeetingMarker = "though the words of it are not set down here";

        /// <summary>The NPC's own quiet note that they and the player met and spoke face to face
        /// OUTSIDE a free conversation — a bargain struck, a quest discussed, words on the road — so
        /// the meeting itself endures in memory even though no words were recorded. Stored as a
        /// SILENT inner turn (no reply asked or fabricated): at replay it folds into the next
        /// incoming line; the [place, time] stamp carries the when and where.</summary>
        public static string MeetingLine(string playerName, bool firstMeeting) => firstMeeting
            ? $"{playerName} and I met and spoke face to face for the first time — a stranger no longer, {MeetingMarker}."
            : $"{playerName} came and spoke with me awhile — of the business of the day, {MeetingMarker}.";

        /// <summary>True when a recorded line is a meeting beat (see <see cref="MeetingLine"/>). The
        /// marker clause is shared by the legacy Angel-voiced and the first-person variants, so both
        /// eras of recorded meetings are recognized.</summary>
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

        /// <summary>The acting-out invitation: the ONE exception to the plain-speech rule — a small
        /// acted gesture rides between single *asterisks*, apart from the spoken words, and the
        /// convention cuts both ways (the player's *offered arm* was done, not said). The mark is
        /// the act's ONLY home (firmed 2026.07.15 — playtests caught acts narrated bare in the
        /// spoken lines, which the chat window then reads as speech): every act takes the mark,
        /// no act walks unmarked. Kept sparing by its own wording — the rarer the act, the more
        /// it says — and weighted by the heart: the same touch is a different act from a stranger
        /// and from an old friend. Offered only when
        /// <see cref="NpcPersona.EncourageActingOut"/> is set (the game layer's toggle).</summary>
        public const string ActingOutGuidance =
            "- One mark alone escapes that rule: what I truly DO — a look, a small act of the body — " +
            "rides between single asterisks, set apart from my spoken words: *I pour the wine and " +
            "slide the cup across*. That mark is the act's only home — every act I make takes it, " +
            "and never do I tell an act bare among my spoken lines as though it were speech. " +
            "Sparingly, where it makes the moment live: one such act, rarely two, and always brief — " +
            "my words carry the scene, never a stage-play of directions. When the one before me " +
            "writes between asterisks, they did it, not said it. And a gesture weighs what the " +
            "heart has earned — the same touch is a boldness from a stranger and a warmth from " +
            "an old friend.";

        // Lowercases only the first character, so a persona fragment like "Calculating, cautious"
        // reads naturally after a lead-in ("In your nature, you are calculating, cautious").
        private static string LowerFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>
        /// Builds the tiny, isolated call in which the NPC — within their own mind, first person —
        /// weighs how the exchange just past moved their regard for the player, expecting only a
        /// single signed number back. Kept apart from the spoken reply on purpose: a chatty model
        /// will narrate a number in prose and forget any hidden mark, but it will reliably answer a
        /// question whose whole job is to return one number. The NPC still decides it themselves, and
        /// they are deliberately NOT told where their standing currently rests: the heart is asked
        /// only how the moment moved it, so a soul already at the deepest love can still be moved
        /// (+N shows even when the game rail is pinned at 100 — the shift is the impact, the rail is
        /// just where it lands; see <see cref="FeelingParser"/> and the game layer's ApplyRelationShift).
        /// (An in-message &lt;relation&gt; tag was tried on 2026.07.09 and reverted the same day: even with
        /// a firm instruction, gpt-4o spoke the number aloud in its reply and never emitted the tag.)
        /// </summary>
        public IReadOnlyList<ChatMessage> BuildFeelingQuery(
            NpcPersona persona,
            string playerName,
            string playerLine,
            string npcReply)
        {
            var system = new StringBuilder();
            system.AppendLine($"I am {persona.Name}, a living soul in the world of Calradia.");
            if (!string.IsNullOrWhiteSpace(persona.PersonalityDescription))
                system.AppendLine("My traits are " + LowerFirst(persona.PersonalityDescription.Trim()));
            system.AppendLine();
            system.AppendLine("For a quiet moment I look honestly into my own heart. What I answer is mine alone to decide.");
            system.AppendLine("I answer with a single whole number and nothing else — no words.");

            var user = new StringBuilder();
            user.AppendLine($"(Within my own mind: {playerName} came to me. They said:");
            user.AppendLine($"  “{playerLine.Trim()}”");
            user.AppendLine("and from my heart I answered:");
            user.AppendLine($"  “{npcReply.Trim()}”");
            user.AppendLine();
            user.AppendLine($"Only this, now: how far did that moment move my heart toward or away from {playerName}? One whole number — positive if they warmed me, negative if they wounded me, 0 if nothing truly changed. A word or a small kindness moves a heart a little (1 to 3); only something that shakes the soul moves it greatly. The number alone.)");

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
            // (the global prompt) and words meant for them alone (the per-NPC prompt). Both are folded
            // in as the NPC's OWN knowledge, first person — no narrator hands them anything.
            if (!string.IsNullOrWhiteSpace(persona.WorldInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("Of this world, this I know:");
                sb.AppendLine(persona.WorldInstructions.Trim());
            }

            if (!string.IsNullOrWhiteSpace(persona.CustomInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("Of myself, this I hold true:");
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
            // Immediately after the plain-speech rule, because it IS that rule's one exception.
            if (persona.EncourageActingOut)
                sb.AppendLine(ActingOutGuidance);

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

            // Offered only when the strike_bargain tool rides along (an unhired sellsword speaking
            // with the one who could take them on): the bargain's hand. The seal is never theirs —
            // the tool only lays terms, and the game layer holds every hard rule.
            if (persona.CanStrikeBargain)
                sb.AppendLine("- I am for hire, and the bargain is mine to strike — but only when they have plainly said they will take me on AND a price has truly been spoken between us do I lay the terms before them; nothing is settled until they seal it by their own hand, and if they let my offer lie I do not press it again. My price may bend only as far as my own worth and honor allow — perhaps not at all; my daily keep afterward is what it is, and not mine to bargain.");

            // The storyteller's gentle guidance on tone and spirit — offered as freedom, never a leash.
            if (!string.IsNullOrWhiteSpace(persona.RoleplayGuidance))
                sb.AppendLine(persona.RoleplayGuidance.Trim());

            return sb.ToString().TrimEnd();
        }
    }
}
