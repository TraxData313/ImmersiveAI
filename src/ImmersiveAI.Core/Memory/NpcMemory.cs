using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Courtship;

namespace ImmersiveAI.Core.Memory
{
    /// <summary>
    /// Long-term memory for one NPC. Three layers:
    /// - RecentTurns: verbatim recent exchanges, sent to the LLM as real multi-turn messages.
    /// - Summary: rolling prose summary of older exchanges, compressed by an LLM call.
    /// - KnownFacts: durable one-line facts the NPC has learned ("The player saved my caravan on day 34").
    /// This replaces ChatAi's flat truncated string list, which is why its NPCs forget and repeat.
    /// </summary>
    public sealed class NpcMemory
    {
        public int Version { get; set; } = 1;
        public string NpcId { get; set; } = string.Empty;
        public string NpcName { get; set; } = string.Empty;

        public List<ConversationTurn> RecentTurns { get; set; } = new List<ConversationTurn>();
        public string Summary { get; set; } = string.Empty;
        public List<string> KnownFacts { get; set; } = new List<string>();

        /// <summary>Lifetime count of exchanges ever shared with the player, never reduced by
        /// compression (RecentTurns is trimmed, this is not). It is the measure of how rich a shared
        /// story this NPC has — used to weight who is more likely to reach out to the player.</summary>
        public int TotalTurns { get; set; }

        /// <summary>Human-readable Calradia timestamp of when the Summary was last regrouped
        /// (set by the game layer at compression time — Core has no game clock). Lets the NPC and
        /// the player see that deep memories reflect a past moment and may be out of date.</summary>
        public string SummaryAsOf { get; set; } = string.Empty;

        public double LastConversationGameDay { get; set; } = -1;

        /// <summary>Campaign day this NPC last reached toward the player of their OWN will — spoke first,
        /// approached, or sent a spontaneous letter (also set, without the count below, when they weighed
        /// it and let it pass). -1 = never. Their pull rests after this day and recovers over time
        /// (see Initiation.InitiationScorer.OutreachDamping), so no one knocks twice in an afternoon.</summary>
        public double LastOutreachGameDay { get; set; } = -1;

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

        /// <summary>Her quiet asks of the one she would wed — written once by the matchmaker's
        /// ledger, private to her sheet, each carrying its checkable predicate (or "heart").</summary>
        public List<CourtshipAsk> CourtshipAsks { get; set; } = new List<CourtshipAsk>();

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

        /// <summary>Sets down one lasting truth mid-conversation (the hold_truth tool). Trimmed and
        /// deduplicated case-insensitively; refused when already held or when the mind is full —
        /// reflection remains the place where the whole list is resettled.</summary>
        public bool AddKnownFact(string fact, int maxFacts)
        {
            if (string.IsNullOrWhiteSpace(fact)) return false;
            var trimmed = fact.Trim();
            if (KnownFacts.Any(f => string.Equals(f, trimmed, StringComparison.OrdinalIgnoreCase))) return false;
            if (maxFacts > 0 && KnownFacts.Count >= maxFacts) return false;
            KnownFacts.Add(trimmed);
            return true;
        }

        /// <summary>Releases one held truth by restatement: an exact match first, then a containment
        /// match either way (the NPC sees the exact list in their prompt, so a close restatement is
        /// enough). Returns the released truth, or null when nothing matched — a miss is safer than
        /// a wrong release.</summary>
        public string? DropKnownFact(string fact)
        {
            if (string.IsNullOrWhiteSpace(fact)) return null;
            var needle = fact.Trim();

            var hit = KnownFacts.FirstOrDefault(f => string.Equals(f, needle, StringComparison.OrdinalIgnoreCase))
                ?? KnownFacts.FirstOrDefault(f =>
                    f.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || needle.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
            if (hit == null) return null;

            KnownFacts.Remove(hit);
            return hit;
        }

        /// <summary>
        /// Replaces the compressed turns with the new rolling summary and applies the facts the NPC
        /// chose to hold. With <paramref name="replaceFacts"/> true the given list IS her truths now —
        /// she was shown all of them and asked to keep, refine, or release, so what she did not restate
        /// falls away (this is what lets her refactor instead of only pile up rewordings). With it
        /// false (or a null list — e.g. a reply that carried no FACTS section at all) the old merge
        /// behavior applies: new facts are appended, existing ones are never touched, so a malformed
        /// reply can never wipe her memory.
        /// </summary>
        public void ApplyCompression(string newSummary, int consumedTurnCount, IEnumerable<string>? newFacts = null, bool replaceFacts = false)
        {
            if (consumedTurnCount < 0 || consumedTurnCount > RecentTurns.Count)
                throw new ArgumentOutOfRangeException(nameof(consumedTurnCount));

            Summary = newSummary ?? string.Empty;
            RecentTurns.RemoveRange(0, consumedTurnCount);

            if (newFacts == null) return;

            if (replaceFacts) KnownFacts.Clear();
            foreach (var fact in newFacts)
            {
                if (string.IsNullOrWhiteSpace(fact)) continue;
                var trimmed = fact.Trim();
                if (!KnownFacts.Any(f => string.Equals(f, trimmed, StringComparison.OrdinalIgnoreCase)))
                    KnownFacts.Add(trimmed);
            }
        }
    }
}
