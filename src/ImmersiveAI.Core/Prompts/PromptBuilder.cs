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
            int total = memory.RecentTurns.Count;
            for (int at = 0; at < total; at++)
            {
                var turn = memory.RecentTurns[at];
                // A great day thins with distance (see BeatFade): whole while it is fresh, then its
                // opening, then only the day itself. The record is untouched — this is what the
                // PROMPT carries — and the recall tools read the ledgers, so she can still tell the
                // whole of it at any distance.
                var incoming = FormatRememberedIncomingLine(turn, voice, total - 1 - at);
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

        private static string FormatRememberedIncomingLine(ConversationTurn turn, string voice,
            int turnsBack = 0)
        {
            var recorded = BeatFade.Fade(turn.PlayerLine, turnsBack);

            // Angel turns carry the same "[place, time]" tag as player lines, so the NPC can see WHEN
            // she was reached for, wrote a letter, or was come to — the full picture of her own story.
            var line = turn.IsFromAngel ? AngelFrame(voice, recorded.Trim())
                : turn.IsInnerThought ? InnerFrame(recorded.Trim())
                : recorded;

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

        /// <summary>
        /// The player's own next line, thought out from everything the one before them would read —
        /// the same identity, memory, situation and shared story, no more and no less.
        ///
        /// SHAPE (reworked after the first playtest, 2026.08.10): ONE system message in the PLAYER's
        /// own first person, then ONE user message carrying the NPC's sheet and the transcript as
        /// MATERIAL, closing on whose turn it is. It deliberately does NOT reuse <see cref="Build"/>:
        /// there, the sheet says "I am Sibylla" and the last assistant turn is hers, so a model asked
        /// for the player's line simply carried on as her — and handed the player HER words to send
        /// back to her. With no chair of hers to sit in, the only "I" in the call is the player's.
        ///
        /// Nothing here is ever recorded: the answer goes into the player's writing box, and no turn
        /// is made of it.
        /// </summary>
        public IReadOnlyList<ChatMessage> BuildPlayerThought(
            string facts,
            NpcMemory memory,
            string playerName,
            string npcName,
            string? wish,
            bool asLetter = false,
            bool mayAct = false,
            string? world = null)
        {
            var them = string.IsNullOrWhiteSpace(npcName) ? "them" : npcName.Trim();

            var user = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(facts))
            {
                user.AppendLine("[What I know, standing here:]");
                user.AppendLine(facts.Trim());
                user.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(world))
            {
                user.AppendLine("[Of this world, this I know:]");
                user.AppendLine(world.Trim());
                user.AppendLine();
            }

            var script = RenderScript(memory, playerName, them);
            user.AppendLine(script.Length > 0
                ? "[What has passed between us — our own words, in order:]"
                : $"[{them} and I have never yet spoken. Mine would be the first words.]");
            if (script.Length > 0) user.AppendLine(script);

            user.AppendLine();
            user.Append(asLetter
                ? PlayerThought.LetterLine(playerName, wish)
                : PlayerThought.SpokenLine(playerName, wish, mayAct));

            return new List<ChatMessage>
            {
                ChatMessage.System(PlayerThought.MindFrame(playerName, them, asLetter)),
                ChatMessage.User(user.ToString()),
            };
        }

        // The remembered turns as a plain script — the same stream the chat window draws, named for
        // who spoke each line so whose turn it is can never be in doubt. This is the ONE place the
        // NPC's own voice belongs in a thinking call, and it is quoted, not inhabited. Her inner
        // beats ride along as asides (the window shows the player those too, so nothing is smuggled
        // in), and a silent beat simply stands with no answer under it.
        private static string RenderScript(NpcMemory memory, string playerName, string npcName)
        {
            var sb = new StringBuilder();
            foreach (var turn in memory.RecentTurns)
            {
                var stamp = StampOf(turn);
                if (turn.IsFromAngel || turn.IsInnerThought)
                    sb.AppendLine($"{stamp}({npcName}, to themselves: {turn.PlayerLine.Trim()})");
                else
                    sb.AppendLine($"{stamp}{playerName}: {turn.PlayerLine.Trim()}");

                if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                    sb.AppendLine($"{npcName}: {Tighten(turn.NpcLine)}");
            }
            return sb.ToString().TrimEnd();
        }

        // A spoken turn may hold blank lines of its own; in a script those read as the turn ending.
        // Tightened to single breaks — the words and their *acted* marks stand untouched.
        private static string Tighten(string line)
        {
            var kept = (line ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0);
            return string.Join("\n", kept);
        }

        private static string StampOf(ConversationTurn turn)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(turn.Place)) parts.Add(turn.Place.Trim());
            if (!string.IsNullOrWhiteSpace(turn.CalradiaTime)) parts.Add(turn.CalradiaTime.Trim());
            return parts.Count == 0 ? string.Empty : "[" + string.Join(", ", parts) + "] ";
        }

        // ---------------- the reach-out, and the question that used to stand before it ----------------
        // THE PONDER IS RETIRED (2026.08.16, Anton's call). Until now a soul the hourly roll had picked
        // was first ASKED — in a full-sheet call of its own — whether it had anything to say at all
        // ("NO, or YES: the something"), and only a YES ever reached the player. The verb in that
        // question was tuned at both extremes over three weeks (discuss → tell/ask) and the lesson of
        // the tuning is what killed it: the answer is decided by the sheet, not by the wording, and the
        // sheet now carries battles, the road journal, births, weddings, the nights, the line since we
        // were last alone, tidings and rumours. There is always something to bring. Paying a whole
        // prompt to be told "no" was buying silence at the price of speech.
        //
        // So the dice pick, and the picked soul simply gets the microphone. What tempers the frequency
        // is the roll itself (DailyInitiationRate × the pull) and OutreachDamping — never a question;
        // damping is where the anti-spam load has belonged since 2026.07.26, when a feedback loop, not
        // an eager prompt, turned out to be the real cause. Do not re-introduce an asking step here.
        //
        // TWO THINGS SURVIVE THE CUT. Recorded ponder beats keep their words forever (IsPonderBeat
        // still folds them into one line of narration in the windows) — and silence is still possible,
        // it is simply no longer solicited: nobody is forced to speak, and words that never come never
        // arrive.

        /// <summary>LEGACY (pre-2026.08.16): the condensed note recorded for a ponder beat, back when
        /// the reach-out opened with a question. Nothing writes new ones — it stays because it defines
        /// the <see cref="IsPonderBeat"/> prefix by which old memories are still recognized, and old
        /// notes keep their old words forever, as all recorded beats do.</summary>
        public static string ReachOutPonderNote(string playerName, bool stranger = false) => stranger
            ? $"I marked {playerName} nearby — a stranger to me still — and weighed whether I had anything to say to them. I resolved:"
            : $"I marked {playerName} nearby and weighed whether I had anything to say to them. I resolved:";

        // The word-for-word prefix of every recorded ponder note; the chat window folds such a beat —
        // reckoning and resolution both — into one soft line of narration (nothing spoken happened).
        private const string PonderNoteMark = "I marked ";

        /// <summary>True when this recorded inner line is a ponder note — a weighing with no spoken
        /// words in it, so views can render the whole beat as narration.</summary>
        public static bool IsPonderBeat(string? innerLine) =>
            (innerLine ?? string.Empty).TrimStart().StartsWith(PonderNoteMark, StringComparison.Ordinal);

        /// <summary>The NPC's own narration of crossing to the player after choosing an offered approach:
        /// when <paramref name="welcomed"/> the player receives them and they speak first; otherwise the
        /// player is too busy and the moment is theirs to spend. Since the ponder was retired this line
        /// carries as a PREMISE the very bar the question used to set — something to tell, or to ask —
        /// and leaves what that is wholly to their own nature and to what the sheet has stirred.</summary>
        public static string ApproachLine(string playerName, bool welcomed) => welcomed
            ? $"I rise and go to {playerName}, for there is something I want to tell them, or to ask them. " +
              "Seeing me come, they turn to me and give me their attention. I speak first now, in my own voice."
            : $"I rise and go to {playerName} — there is something I want to tell them, or to ask them — but as I near, " +
              "they raise an apologetic hand: too caught up just now to speak with me. " +
              "The moment is still mine: I say or do with it what I will, here and now.";

        /// <summary>The condensed note recorded for an approach beat.</summary>
        public static string ApproachNote(string playerName, bool welcomed) => welcomed
            ? $"Of my own accord I went to {playerName}; they received me, and I spoke first. My words:"
            : $"Of my own accord I went to {playerName}, but they were too caught up to speak with me just then. In that moment:";

        /// <summary>The NPC's own narration for a reaching-out that arrives as spoken words: they cross
        /// to the player and speak first, knowing the answer may come at once or only later. Same premise
        /// as <see cref="ApproachLine"/> — a thing to tell or to ask, never a list of what would count.
        /// The stranger variant states only the fact: they have never spoken (no imagined history; how
        /// to open is their own affair).</summary>
        public static string FirstWordLine(string playerName, bool stranger = false) => stranger
            ? $"I cross to {playerName} now — we have never spoken. Something moves me to it: a thing I want to tell them, or to ask them. " +
              "They are caught up in their own affairs; my words will reach them, but the answer may come at once or only later. " +
              "I speak my first words now, in my own voice."
            : $"I go to {playerName} now, of my own accord: there is something I want to tell them, or to ask them. " +
              "They are caught up in their own affairs; my words will reach them, but the answer may come at once or only later. " +
              "I speak now, in my own voice.";

        /// <summary>The condensed note recorded for a first-word beat. The words they actually spoke ride
        /// the same turn, and THAT is the repetition brake now the ponder's stated cause is gone: the next
        /// reach-out reads what was really said last time, not a summary of what was meant by it.</summary>
        public static string FirstWordNote(string playerName) =>
            $"Of my own accord I crossed to {playerName} and spoke first. My words:";

        // ------------------------- letters (correspondence across the map) -------------------------
        // Each beat below is the NPC's OWN mind at the writing desk (first person, recorded with
        // ConversationTurn.InnerSpeaker since 2026.08.07 — the Angel narrator is retired), so the
        // NPC's memory holds the whole correspondence truthfully — the wishing, the words, the reading.

        // The spontaneous letter's own asking step went the same way as the reach-out ponder on
        // 2026.08.16 (WriteLetterDesireLine, "do I wish, of my own will, to write now?" — one full-sheet
        // call answered yes or no). Same reasoning, same roll: the post has its own dice
        // (LetterCourier.WriteRateFactor × the pull × the depth of the story × the damping), and when
        // they come up a writer's way they sit to the page. The premise the question used to establish —
        // the long road, the courier standing ready — moved into the compose line below, AFTER its
        // opening marker fragment, so recorded beats stay recognized. The letter a player WROTE is a
        // different matter: answering one is a reply, not an outreach, and letting it lie unanswered
        // stays a real choice (see AnswerLetterDesireLine).

        /// <summary>The NPC sitting down to set the letter itself onto the page, in their own first
        /// person. For one in the player's own service (<paramref name="inService"/> — their clan: a
        /// party or caravan on the road, a governor at their post) a field-report invitation is added,
        /// so the letter home may carry word of their charge. Everything after the first sentence
        /// follows the marker fragment (<see cref="IsComposeLetterBeat"/> matches by prefix), so
        /// recorded beats stay recognized.</summary>
        public static string ComposeLetterLine(string playerName, bool inService = false) =>
            $"I sit, and set my heart to paper. The road lies long between me and {playerName} — they are far " +
            $"from here, beyond an easy ride — but a courier stands ready to carry my words across it. " +
            $"What I set down now is only the letter itself — the words " +
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
        /// the first-meeting and the familiar <see cref="ArrivalLine"/>. A summary that holds only the
        /// seeded story of their own road (<see cref="NpcMemory.SeededFromStory"/>) is THEIR story, not
        /// history with the player — such a soul still meets the player as a stranger.</summary>
        public static bool HasRememberedHistory(NpcMemory memory) =>
            memory.RecentTurns.Count > 0
            || memory.TotalTurns > 0
            || (!string.IsNullOrWhiteSpace(memory.Summary) && !memory.SeededFromStory);

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

        // Cut back hard on 2026.08.14 (Anton: "too big and too telling them what to do"). The long
        // per-tool paragraphs that used to stand here moved into the tool definitions themselves,
        // where a tool's contract belongs; what remains is three short habits of speech and nothing
        // else. Long rule-lists make every soul answer the same — the whole reason this section is
        // kept short is so there is room left to actually be someone.

        /// <summary>
        /// The line that gives the player's own words the last say, spoken as something the soul
        /// holds rather than a rule handed down. Two jobs at once: it stands LAST in the sheet
        /// (recency), and it says plainly which way a contradiction falls (precedence) — because
        /// placement alone was not winning against thousands of tokens of lived memory above it.
        /// Deliberately without the word "instruction", "rule" or "must": to her these are simply
        /// the truths she is surest of.
        /// </summary>
        public const string HeldTruestFrame =
            "And these I hold truest of all, above anything else in my mind. Where some other thing " +
            "I remember or believe stands against them, these stand:";

        /// <summary>The brevity rule: short words keep the living back-and-forth of talk instead of
        /// long, static monologues.</summary>
        public const string BrevityGuidance =
            "- I speak as talk truly flows between two people — a sentence or three, then I let them " +
            "answer. Only a tale asked of me runs longer.";

        /// <summary>The tone rule: a light savor of the old world, for atmosphere, never laid on thick.</summary>
        public const string OldWorldToneGuidance =
            "- My words carry a light savor of the old world, lightly worn — plain, living speech first.";

        /// <summary>The plain-page rule: replies land on a page that shows every mark exactly as
        /// written — nothing is rendered — so pen-marks (**word**, dash-lists, headers) arrive as
        /// literal clutter around the words. Told in-world: the voice carries, not the pen. New
        /// lines are honored by the panel, so they stay the one shape speech may take.</summary>
        public const string PlainSpeechGuidance =
            "- My words are heard, not read from a page: no marks of the pen ride in them. A new line " +
            "for a new thought is all the shape my speech needs.";

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
            "- One mark alone escapes that: what I truly DO rides between single asterisks, apart from " +
            "my words — *I pour the wine and slide the cup across*. That mark is an act's only home, " +
            "and I never tell an act bare among my spoken lines as though it were speech. Sparingly — " +
            "one such act, rarely two. When the one before me writes between asterisks, they did it, " +
            "not said it.";

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

            // NOTE: the player-authored guidance used to stand here, mid-sheet. It moved to the very
            // end on 2026.08.14 — see AppendPlayerAuthored below for why.

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
                if (memory.SeededFromStory && memory.StoryRichness == 0)
                {
                    // Nothing lived with this person yet — the deep memory holds only the seeded
                    // story of the road that made me. Headed honestly as my own; the player enters
                    // it only once something is truly lived between us.
                    sb.AppendLine("The road of my life so far, as I carry it in memory:");
                }
                else
                {
                    var asOf = string.IsNullOrWhiteSpace(memory.SummaryAsOf)
                        ? string.Empty
                        : $" (as I last gathered my thoughts on {memory.SummaryAsOf.Trim()})";
                    sb.AppendLine($"What {playerName} is to me{asOf}:");
                }
                sb.AppendLine(memory.Summary.Trim());
            }

            // The courtship road rides beside the deep memory of this person — where the heart
            // stands and what it quietly asks — and, for a clan head, the suitor's case. Both are
            // built by the game layer (persisted stage + live met-marks) and placed here so they
            // sit with "What {player} is to me", the last knowledge before the moment itself.
            if (!string.IsNullOrWhiteSpace(persona.CourtshipTerms))
            {
                sb.AppendLine();
                sb.AppendLine(persona.CourtshipTerms.Trim());
            }

            // What the world is allowed to say about his house — which children he has owned and
            // which he has left unsaid. It belongs here, beside what he IS to her, because for the
            // women of a household this is not gossip about a third party; it is the shape of their
            // own lives.
            if (!string.IsNullOrWhiteSpace(persona.PlayerHouseLine))
            {
                sb.AppendLine();
                sb.AppendLine(persona.PlayerHouseLine.Trim());
            }

            // The road's other branch sits in the same place, for the same reason: what she IS to
            // this person belongs beside what she remembers of them, not off in some other section.
            if (!string.IsNullOrWhiteSpace(persona.LoverTerms))
            {
                sb.AppendLine();
                sb.AppendLine(persona.LoverTerms.Trim());
            }
            // The rail against pretending the bond into being rides wherever the hand does — and it
            // is needed MOST before the bond exists, which is exactly when LoverTerms is still
            // empty. So it hangs off the hand, never off the section.
            if (persona.CanOfferSelf)
            {
                sb.AppendLine();
                sb.AppendLine(Courtship.LoverText.WordsDoNotBind);
            }

            // What stands between them sits here too, and it is the LAST thing she reads before the
            // moment itself — deliberately, because a shut door is the single most present fact of
            // an evening and must not be buried above her memory of him.
            if (!string.IsNullOrWhiteSpace(persona.DoorTerms))
            {
                sb.AppendLine();
                sb.AppendLine(persona.DoorTerms.Trim());
            }

            if (!string.IsNullOrWhiteSpace(persona.SuitorTerms))
            {
                sb.AppendLine();
                sb.AppendLine(persona.SuitorTerms.Trim());
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
            sb.AppendLine("How I speak:");
            sb.AppendLine(BrevityGuidance);
            sb.AppendLine(OldWorldToneGuidance);
            sb.AppendLine(PlainSpeechGuidance);
            // Immediately after the plain-speech rule, because it IS that rule's one exception.
            if (persona.EncourageActingOut)
                sb.AppendLine(ActingOutGuidance);

            // The eight per-tool whisper paragraphs that used to stand here moved INTO the tool
            // definitions themselves on 2026.08.14 (Anton: the section had grown "too big and too
            // telling them what to do"). A tool's contract belongs beside its schema, where it is
            // sent on every call that carries the tool — not in a wall of sheet prose. The persona's
            // Can* flags still decide which tools are offered at all; only the words moved.

            // The storyteller's gentle guidance on tone and spirit — offered as freedom, never a leash.
            if (!string.IsNullOrWhiteSpace(persona.RoleplayGuidance))
                sb.AppendLine(persona.RoleplayGuidance.Trim());

            // THE ORDER OF THE WORLD, if this game is carrying it — the air everybody in the era
            // breathes about a woman's place. It sits HERE, ahead of the player-authored block and
            // outside it, on purpose: it is background knowledge and not a rule she is handed, and
            // it must never wear the frame that says "this is what I hold truest", which belongs to
            // the player's own words alone.
            if (!string.IsNullOrWhiteSpace(persona.EraNorm))
            {
                sb.AppendLine();
                sb.AppendLine(persona.EraNorm.Trim());
            }

            AppendPlayerAuthored(sb, persona);

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The player's own words, and the last thing in the sheet.
        /// <para>
        /// They used to sit mid-sheet, just after the self — and were quietly losing (Anton,
        /// 2026.08.14: "when I change stuff there it just gets ignored, if the NPC remembers other
        /// things"). Of course they were: two lines of hand-written intent were standing in the
        /// middle of a page that then went on to spend thousands of tokens on lived memory, the
        /// scene, the roll of nights, the moment. Whatever comes last, and whatever plainly claims
        /// precedence, is what survives that. So they close the sheet, under one line that says out
        /// loud which way a contradiction falls — in her own voice, as a thing she holds, never as a
        /// rule handed to her.
        /// </para>
        /// </summary>
        internal static void AppendPlayerAuthored(StringBuilder sb, NpcPersona persona)
        {
            var world = persona.WorldInstructions?.Trim() ?? string.Empty;
            var mine = persona.CustomInstructions?.Trim() ?? string.Empty;
            if (world.Length == 0 && mine.Length == 0) return;

            sb.AppendLine();
            sb.AppendLine(HeldTruestFrame);

            if (world.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Of this world, this I know:");
                sb.AppendLine(world);
            }

            if (mine.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Of myself, this I hold true:");
                sb.AppendLine(mine);
            }
        }
    }
}
