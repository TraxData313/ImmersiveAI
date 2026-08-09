using System;
using System.Collections.Generic;
using System.Linq;
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
        public string Summary { get; set; } = string.Empty;

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

        /// <summary>True once she has truly sat with the question of what might weigh on her about
        /// this marriage — set by her first set_down, INCLUDING the honest "none". Readiness and the
        /// betrothal ask this of her; a heart that never looked cannot say no questions remain.</summary>
        public bool MisgivingsWeighed { get; set; }

        /// <summary>Campaign day her house's head blessed the match; -1 = not given. Only asked
        /// for at all when the world requires family consent and she has kin above her.</summary>
        public double FamilyBlessingDay { get; set; } = -1;

        /// <summary>The bride-price paid for the blessing, for the record's honesty.</summary>
        public int FamilyBlessingPrice { get; set; }

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
        }

        /// <summary>They weighed reaching out and let the moment pass (or answered a letter they were
        /// invited to answer): the outreach day still rests them for a while, but their pride is
        /// untouched — nothing of theirs waits unanswered.</summary>
        public void NoteOutreachConsidered(double gameDay)
        {
            LastOutreachGameDay = gameDay;
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

        public int GetKeepMostRecentForCompression(
            int keepRecentTurns,
            double currentGameDay,
            int keepRecentDays,
            int minRecentMemoryTokensAfterCompression)
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
    }
}
