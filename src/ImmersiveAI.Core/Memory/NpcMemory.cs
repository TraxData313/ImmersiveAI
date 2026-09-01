using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Courtship;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// Long-term memory for one NPC. Two layers:
    /// - RecentTurns: verbatim recent exchanges, sent to the LLM as real multi-turn messages.
    /// - Summary: rolling prose memory of everything older, rewritten whole by an LLM call.
    /// This replaces ChatAi's flat truncated string list, which is why its NPCs forget and repeat.
    /// (A third layer of distilled one-line "known facts" rode here until 2026.08.08; it cramped
    /// the same things the summary already held and read them back to her twice — see
    /// <see cref="KnownFacts"/>.)
    /// </summary>
    public sealed class NpcMemory
    {
        public int Version { get; set; } = 1;
        public string NpcId { get; set; } = string.Empty;
        public string NpcName { get; set; } = string.Empty;

        public List<ConversationTurn> RecentTurns { get; set; } = new List<ConversationTurn>();

        /// <summary>THE DEEP MEMORY: everything she durably carries of this person, in her own
        /// first-person prose, rewritten WHOLE at every compression. Briefly split into keyed notes
        /// on 2026.08.27 and restored the same day (Anton, having played both: "revert it back to
        /// the original text and the old way they managed it"). See <see cref="Bites"/> for what
        /// happens to a soul who was mid-experiment.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// LEGACY, and it lived for one day (2026.08.27): the deep memory as small keyed notes she
        /// edited one at a time. Anton played both shapes and chose the prose back — so nothing
        /// writes this any more, and <see cref="HealLegacyNotes"/> folds whatever a save still
        /// holds back into <see cref="Summary"/> the first time it is loaded.
        /// <para>KEPT FOREVER as a field all the same: a soul who compressed during that one day
        /// has her whole memory in here and nowhere else, and a dropped property is a deleted
        /// person. The same rule as the retired <see cref="KnownFacts"/>.</para>
        /// </summary>
        public Dictionary<string, string> Bites { get; set; } = new Dictionary<string, string>();

        /// <summary>LEGACY, retired 2026.08.08: the distilled one-line truths an NPC used to hold
        /// beside the summary (the hold_truth tool and the reflection's FACTS section, both gone).
        /// Nothing reads or writes it any more — it survives only so an older memories.json keeps
        /// what it already holds instead of losing it on the next save. Never fold it into a prompt
        /// again; the rolling <see cref="Summary"/> carries the whole of what she remembers now.</summary>
        public List<string> KnownFacts { get; set; } = new List<string>();

        /// <summary>Lifetime count of exchanges ever shared with the player, never reduced by
        /// compression (RecentTurns is trimmed, this is not). It is the measure of how rich a shared
        /// story this NPC has — used to weight who is more likely to reach out to the player.</summary>
        public int TotalTurns { get; set; }

        /// <summary>Human-readable Calradia timestamp of when the Summary was last regrouped
        /// (set by the game layer at compression time — Core has no game clock). Lets the NPC and
        /// the player see that deep memories reflect a past moment and may be out of date.</summary>
        public string SummaryAsOf { get; set; } = string.Empty;

        /// <summary>True when the Summary began as the seeded story of the NPC's own road — the tale
        /// the world tells of them, set down as the first page of an otherwise empty deep memory
        /// (2026.08.08, replacing the old self.txt seeding) so it lives under the rolling summary's
        /// own rules: rewritten whole at every compression, theirs to keep or let fade. Distinguishes
        /// "carries only their own backstory" from "remembers the player" — a seeded-only memory still
        /// meets the player as a stranger. Never cleared; once real history arrives,
        /// <see cref="TotalTurns"/> carries the acquaintance.</summary>
        public bool SeededFromStory { get; set; }

        public double LastConversationGameDay { get; set; } = -1;

        /// <summary>Campaign day this NPC last reached toward the player of their OWN will — spoke first,
        /// approached, or sent a spontaneous letter (also set, without the count below, when they weighed
        /// it and let it pass). -1 = never. Their pull rests after this day and recovers over time
        /// (see Initiation.InitiationScorer.OutreachDamping), so no one knocks twice in an afternoon.</summary>
        public double LastOutreachGameDay { get; set; } = -1;

        /// <summary>Campaign day their last talk with the player ENDED (2026.08.09). Not when it
        /// began and not the last turn of it — the moment they parted, which is when everything
        /// said in it becomes "said". It anchors the line in <see cref="Together.TogetherLine"/>:
        /// while a talk is still running the line must stay where it was, or the very thing she
        /// meant to raise would vanish from her sheet halfway through raising it. -1 = never.</summary>
        public double LastTalkEndedDay { get; set; } = -1;

        /// <summary>How many of their own outreaches have met the player's silence since the player last
        /// engaged (spoke with them, met them, or wrote). Each one stretches how long they wait and lowers
        /// how strongly they try again — two letters into silence is enough for anyone's pride. Reset the
        /// moment the player answers in any form.</summary>
        public int UnansweredOutreachCount { get; set; }

        /// <summary>How rich the shared story with the player is, for weighting who reaches out. Uses the
        /// lifetime turn count, but never falls below what is still held verbatim — so memories saved before
        /// <see cref="TotalTurns"/> existed (where it loads as 0) still weigh in by their surviving turns.</summary>
        public int StoryRichness => Math.Max(TotalTurns, RecentTurns.Count);

        // ------------------------- the courtship road (2026.08.07) -------------------------
        // Persisted here, beside the rest of what this NPC carries of the player, so the save-scoped
        // memory snapshots rewind a courtship with the save for free. Old files load the defaults:
        // no road walked, never seeded — the one-time seeding then reads the lived story.

        /// <summary>Where her heart stands on the road toward marrying the player
        /// (see <see cref="Courtship.CourtshipStage"/>). Old files load as None.</summary>
        public CourtshipStage CourtshipStage { get; set; } = CourtshipStage.None;

        /// <summary>Campaign day of her last FORWARD step on the road; -1 = never. Rails forward
        /// steps to one a day.</summary>
        public double CourtshipStepDay { get; set; } = -1;

        /// <summary>Campaign day the betrothal was sealed; -1 = not betrothed. Feeds the
        /// troth-seasoning rail (MinBetrothalDays).</summary>
        public double BetrothedGameDay { get; set; } = -1;

        /// <summary>True once the one-time "where does my heart already stand" seeding has run (or
        /// was judged unnecessary). False on every old file, so a soul with real history gets her
        /// lived story honored the first time the road touches her.</summary>
        public bool CourtshipSeeded { get; set; }

        /// <summary>Her own written misgivings about a life wed to the player — set down by her own
        /// hand mid-talk (the weigh_misgivings tool), each laid to rest only by her own judgment.
        /// Replaced the matchmaker's checkable quiet asks on 2026.08.08; the old CourtshipAsks JSON
        /// field is simply ignored on load, so an old file begins with an unweighed heart.</summary>
        public List<CourtshipMisgiving> CourtshipMisgivings { get; set; } = new List<CourtshipMisgiving>();

        /// <summary>
        /// THE PLAYER'S OWN RECORD of every movement of the road with this soul — and HERS OF
        /// NOTHING. It is written by the game layer, read by the windows, and touched by no prompt
        /// builder anywhere; a test asserts a built prompt never carries one. See
        /// <see cref="Courtship.RoadNote"/> for why the player's record is kept apart from her
        /// memory rather than folded into it as beats.
        /// </summary>
        public List<Courtship.RoadNote> RoadNotes { get; set; } = new List<Courtship.RoadNote>();

        /// <summary>True once she has truly sat with the question of what might weigh on her about
        /// this marriage — set by her first set_down, INCLUDING the honest "none". Readiness and the
        /// betrothal ask this of her; a heart that never looked cannot say no questions remain.</summary>
        public bool MisgivingsWeighed { get; set; }

        /// <summary>Campaign day her house's head blessed the match; -1 = not given. Only asked
        /// for at all when the world requires family consent and she has kin above her.</summary>
        public double FamilyBlessingDay { get; set; } = -1;

        /// <summary>The bride-price paid for the blessing, for the record's honesty.</summary>
        public int FamilyBlessingPrice { get; set; }

        // ------------------------- the lover's fork (2026.08.15) -------------------------
        // The road above forks at Devotion: one branch runs on to a wedding, the other stops here.
        // It rides in the same file for the same reason the courtship does — the save-scoped memory
        // snapshots rewind it with everything else, and a bond that survived a reload the player
        // undid would be the worst kind of bug in exactly the most sensitive feature in the mod.
        // Old files load the defaults: nothing of the kind, never bought, never begun.

        /// <summary>What she is to the player outside the world's ceremonies. Old files load as None.</summary>
        public LoverBond LoverBond { get; set; } = LoverBond.None;

        /// <summary>Campaign day the bond was sealed; -1 = never. Kept through Former, because "we
        /// were, once, for two years" is a different sentence from "we were, once, for a week".</summary>
        public double LoverSinceDay { get; set; } = -1;

        /// <summary>Campaign day it ended; -1 = it has not. Set alongside <see cref="LoverBond"/>
        /// becoming Former.</summary>
        public double LoverEndedDay { get; set; } = -1;

        /// <summary>What her house was paid for her leaving it; 0 = never bought. Survives the end
        /// of the bond ON PURPOSE — the gold is not returned, she did not go home, and both of them
        /// carry that whatever else changed.</summary>
        public int LoverRansomPaid { get; set; }

        /// <summary>Whether she has ever been his, now or once. The plain question most callers
        /// actually want.</summary>
        public bool HasLoverHistory => LoverBond != LoverBond.None;

        // ------------------------- the door (2026.08.15) -------------------------

        /// <summary>
        /// Why her door is shut, in her own words — the misgivings' model applied to the night, and
        /// kept in the same file for the same reason: a save-scoped snapshot must rewind a wound
        /// with everything else, or reloading past a bad evening would leave her still hurt by
        /// something that, on that timeline, never happened.
        ///
        /// Written by her own hand through her own tool, and by the WORLD for a duty night — see
        /// <see cref="Doors.DoorReasons.LayDownByTheWorld"/> for why that one exception exists.
        /// Old files load an empty list, which is an open door.
        /// </summary>
        public List<Doors.DoorReason> DoorReasons { get; set; } = new List<Doors.DoorReason>();

        /// <summary>
        /// The campaign day she LEARNED something that wounded her — he went to another, another
        /// woman is his now. -1 = nothing fresh. For a day and a half it is the loudest thing in
        /// her and she will cross a room or write a letter to say so
        /// (<see cref="Initiation.InitiationScorer.WoundSpike"/>); after that it is simply true, and
        /// the cold quiet takes over.
        ///
        /// ONE SPIKE PER WOUND, and this stamp is how: it is cleared the moment she acts on it, so
        /// a wound cannot keep producing visits for a day and a half. Nothing here decides what she
        /// FEELS or whether she raises it — only whether she is moved to come.
        /// </summary>
        public double FreshWoundDay { get; set; } = -1;

        /// <summary>She has said her piece, or written it. The wound stops being news.</summary>
        public void NoteWoundSpoken() => FreshWoundDay = -1;

        public void AddTurn(ConversationTurn turn)
        {
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            RecentTurns.Add(turn);
            TotalTurns++;
            LastConversationGameDay = turn.GameDay;
            // A turn whose incoming line is the player's IS the player engaging — whatever silence
            // the NPC's own outreaches met before, it is answered now. Angel beats and the NPC's own
            // inner reckonings are their side of the story, never the player's answer.
            if (!turn.IsFromAngel && !turn.IsInnerThought) UnansweredOutreachCount = 0;
        }

        /// <summary>They went to the player of their own will (or their letter left their hand):
        /// the outreach day is set and one more waits unanswered until the player engages.</summary>
        public void NoteOutreach(double gameDay)
        {
            LastOutreachGameDay = gameDay;
            UnansweredOutreachCount++;
            // ONE SPIKE PER WOUND: she has gone to him, whatever she chose to say when she got
            // there. Without this a single leak would keep pushing her across the room for a day
            // and a half, and the design's own rule is that punishment is rare and heavy rather
            // than frequent and light.
            NoteWoundSpoken();
        }

        /// <summary>They spent the moment without reaching out of their own will — today that means
        /// answering a letter they were invited to answer (the reach-out and spontaneous-letter
        /// weighings that also landed here were retired 2026.08.16). The outreach day still rests them
        /// for a while, but their pride is untouched — nothing of theirs waits unanswered.</summary>
        public void NoteOutreachConsidered(double gameDay)
        {
            LastOutreachGameDay = gameDay;
            // The moment was hers and she spent it. That IS her answer to the wound, and she does not
            // get moved across a room about the same one again.
            NoteWoundSpoken();
        }

        /// <summary>The player engaged outside the turn stream (met them face to face without free chat,
        /// or a letter of the player's reached their hands): whatever waited unanswered is answered.</summary>
        public void NotePlayerEngaged()
        {
            UnansweredOutreachCount = 0;
        }

        /// <summary>True when older turns should be compressed into the rolling summary.</summary>
        public bool NeedsCompression(int maxRecentTurns)
        {
            return RecentTurns.Count > maxRecentTurns;
        }

        /// <summary>True when older turns should be compressed by turn count, age, or estimated token pressure.</summary>
        public bool NeedsCompression(int maxRecentTurns, double currentGameDay, int maxRecentDays, int maxRecentMemoryTokens)
        {
            return NeedsCompression(maxRecentTurns)
                || HasTurnsOlderThan(currentGameDay, maxRecentDays)
                || MemoryTokenEstimator.EstimateRecentTurnsTokens(RecentTurns) > maxRecentMemoryTokens;
        }

        /// <summary>
        /// The most of the verbatim window that may be BEATS rather than words actually exchanged
        /// (2026.08.11, Anton's call — "случките да не могат да заемат повече от една трета").
        /// Battles, roads, nights, weddings and children all leave silent marks, and on a hard
        /// campaign they arrive far faster than conversations do; measured across one of Anton's
        /// own campaigns they were already 41% of every recorded turn. Nothing is lost when they
        /// go — a folded beat is in the rolling memory, which is where it was always headed — but
        /// what she has WORD FOR WORD should be the talking, not the bookkeeping.
        /// </summary>
        public const double DefaultMaxBeatShare = 1.0 / 3.0;

        /// <summary>Whether this turn is a silent mark rather than something either of them said.
        /// PUBLIC because the prompt builder caps the same share at RENDER time and must judge it
        /// by the very same rule — one question, one answer (2026.08.27).</summary>
        public static bool IsBeat(ConversationTurn turn) =>
            turn != null && string.IsNullOrWhiteSpace(turn.NpcLine)
            && (turn.IsInnerThought || turn.IsFromAngel);

        public int GetKeepMostRecentForCompression(
            int keepRecentTurns,
            double currentGameDay,
            int keepRecentDays,
            int minRecentMemoryTokensAfterCompression,
            double maxBeatShare = DefaultMaxBeatShare)
        {
            if (keepRecentTurns < 0) throw new ArgumentOutOfRangeException(nameof(keepRecentTurns));
            if (keepRecentDays < 0) throw new ArgumentOutOfRangeException(nameof(keepRecentDays));
            if (minRecentMemoryTokensAfterCompression < 0) throw new ArgumentOutOfRangeException(nameof(minRecentMemoryTokensAfterCompression));

            var keepCount = Math.Min(RecentTurns.Count, keepRecentTurns);

            if (keepRecentDays > 0)
            {
                var cutoffDay = currentGameDay - keepRecentDays;
                var turnsInsideWindow = RecentTurns.Count(t => t.GameDay >= cutoffDay);
                keepCount = Math.Min(keepCount, turnsInsideWindow);
            }

            if (minRecentMemoryTokensAfterCompression > 0)
            {
                while (keepCount > 0 && MemoryTokenEstimator.EstimateRecentTurnsTokens(RecentTurns.Skip(RecentTurns.Count - keepCount)) > minRecentMemoryTokensAfterCompression)
                    keepCount--;
            }

            // And the bookkeeping does not get to hold the window (see DefaultMaxBeatShare). The
            // kept slice is a suffix, so shrinking it lets go of its OLDEST entry first — which is
            // exactly "the oldest happening settles deeper, sooner". A window that is ALL beats can
            // still shrink to nothing here, and that is right: there was no conversation in it.
            if (maxBeatShare > 0 && maxBeatShare < 1)
            {
                while (keepCount > 1)
                {
                    var kept = RecentTurns.Skip(RecentTurns.Count - keepCount).ToList();
                    int beats = kept.Count(IsBeat);
                    if (beats <= Math.Max(1, (int)Math.Floor(keepCount * maxBeatShare))) break;
                    keepCount--;
                }
            }

            return keepCount;
        }

        private bool HasTurnsOlderThan(double currentGameDay, int maxRecentDays)
        {
            if (maxRecentDays <= 0 || RecentTurns.Count == 0) return false;
            var cutoffDay = currentGameDay - maxRecentDays;
            return RecentTurns.Any(t => t.GameDay < cutoffDay);
        }

        /// <summary>The oldest turns that should be folded into the summary, keeping the newest ones verbatim.</summary>
        public IReadOnlyList<ConversationTurn> GetTurnsToCompress(int keepMostRecent)
        {
            if (keepMostRecent < 0) throw new ArgumentOutOfRangeException(nameof(keepMostRecent));
            int count = RecentTurns.Count - keepMostRecent;
            return count <= 0
                ? (IReadOnlyList<ConversationTurn>)Array.Empty<ConversationTurn>()
                : RecentTurns.Take(count).ToList();
        }

        /// <summary>
        /// Drops the turns that were folded away and takes the rewritten rolling summary as the whole
        /// of what she now remembers. The summary is written anew at every compression — she is shown
        /// all of it and asked to set it down whole — so what she did not carry forward is gone, which
        /// is what lets her refactor her memory instead of piling up rewordings.
        /// </summary>
        public void ApplyCompression(string newSummary, int consumedTurnCount)
        {
            if (consumedTurnCount < 0 || consumedTurnCount > RecentTurns.Count)
                throw new ArgumentOutOfRangeException(nameof(consumedTurnCount));

            Summary = newSummary ?? string.Empty;
            RecentTurns.RemoveRange(0, consumedTurnCount);
        }

        /// <summary>
        /// The compression as it works now the page is retired (2026.08.27 evening): the note edits
        /// are a DELTA — only what she named is touched, everything else keeps standing word for
        /// word — and <paramref name="newSummary"/> is normally NULL, which means "leave the old
        /// page alone".
        ///
        /// <para>
        /// AND THE PAGE IS CLEARED THE MOMENT SHE HAS NOTES TO STAND ON. That is the whole migration:
        /// an old save carries its long page until the first time she gathers her thoughts, that pass
        /// invites her to lift all of it into notes, and the page then goes. It is deliberately keyed
        /// on her having WRITTEN something — a pass that produced no notes at all leaves the page
        /// untouched, so a stumbling backend can never erase a soul's whole memory and leave nothing
        /// in its place.
        /// </para>
        /// </summary>
        /// <summary>
        /// Everything she durably holds of this person, as readable text. ONE place answers it so
        /// the chroniclers, the stranger test and the views can never disagree about whether a soul
        /// remembers anybody.
        /// </summary>
        public string DeepMemoryText() => (Summary ?? string.Empty).Trim();

        /// <summary>True when anything durable is held at all.</summary>
        public bool HasDeepMemory => !string.IsNullOrWhiteSpace(Summary);

        /// <summary>
        /// THE ONE-DAY MIGRATION BACK (2026.08.27). A soul who compressed while the memory was keyed
        /// notes has her whole deep memory sitting in <see cref="Bites"/> with an EMPTY page — so
        /// reverting without this would not restore her memory, it would delete it. Her notes are
        /// folded back in as plain lines, exactly as they were written (Anton: "if there are current
        /// NPCs like Rhia that switched to key: value just leave it as a text"), and she reshapes
        /// them into her own prose at her next compression, which is where prose belongs anyway.
        /// <para>Idempotent and additive: it runs at load, never drops a page that already stands,
        /// and clears the notes only once they are safely inside it.</para>
        /// </summary>
        public void HealLegacyNotes()
        {
            if (Bites == null || Bites.Count == 0) return;

            var sb = new StringBuilder();
            var page = (Summary ?? string.Empty).Trim();
            if (page.Length > 0) sb.AppendLine(page).AppendLine();

            foreach (var pair in Bites)
            {
                if (string.IsNullOrWhiteSpace(pair.Value)) continue;
                sb.Append(pair.Key).Append(": ").AppendLine(pair.Value.Trim());
            }

            Summary = sb.ToString().TrimEnd();
            Bites.Clear();
        }

        /// <summary>
        /// Folds twin misgivings a save already carries into one line each — see
        /// <see cref="ImmersiveAI.Core.Courtship.CourtshipMisgivings.HealTwins"/>. Teaching the
        /// razor (2026.08.30) only stopped NEW twins; a list written before it could still hold a
        /// doubt she had answered under one wording and could no longer reach under the other, and
        /// that is a road walled shut with nothing in her own hand to open it. Runs at load beside
        /// the other heals, and is idempotent — a healed list folds nothing on the next pass.
        /// </summary>
        public void HealTwinMisgivings() =>
            ImmersiveAI.Core.Courtship.CourtshipMisgivings.HealTwins(CourtshipMisgivings);

        /// <summary>
        /// Undresses recorded spoken lines that took a raw structured-output envelope in whole —
        /// the 2026.08.28 Claude Code find: a wrapped answer failed strict parsing, and
        /// <c>{"reply": "…"}</c> walked into the thread and the memory wearing its braces. Runs at
        /// load beside <see cref="HealLegacyNotes"/>: idempotent (an unwrapped line no longer
        /// matches), touches only lines that ARE envelopes, and keeps only the speech — tool calls
        /// recorded inside one are dead history and must never re-fire.
        /// </summary>
        public void HealEnvelopeLines()
        {
            if (RecentTurns == null) return;
            foreach (var turn in RecentTurns)
            {
                if (turn == null || string.IsNullOrEmpty(turn.NpcLine)) continue;
                if (Llm.ClaudeCliShape.TryUnwrapSpokenEnvelope(turn.NpcLine, out var reply))
                    turn.NpcLine = reply;
            }
        }

        /// <summary>
        /// Drops the turns that were folded away and takes the rewritten deep memory as the whole of
        /// what she now remembers. It is written anew at every compression — she is shown all of it
        /// and asked to set it down whole — so what she did not carry forward is gone, which is what
        /// lets her refactor her memory instead of piling up rewordings.
        /// </summary>
        public void ApplyCompression(string newSummary, int consumedTurnCount, string? unusedLegacy)
        {
            ApplyCompression(newSummary, consumedTurnCount);
        }
    }
}
