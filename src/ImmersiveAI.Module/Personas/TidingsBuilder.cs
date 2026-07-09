using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Gathers what has lately happened in the world for one NPC's ears: the campaign's own log
    /// history (<see cref="LogEntryHistory"/>) — the same stream vanilla lords draw their "congratulations
    /// on winning the tournament" remarks from — filtered to what would plausibly have reached this
    /// speaker, plus the talk of the town where they stand (the game's own <c>GetAsRumor</c> lines,
    /// already written in a commoner's voice).
    ///
    /// Relevance leans on the game's own judgment first — the per-hero conversation score and the
    /// per-clan importance TaleWorlds already computes — topped with a small editorial baseline for
    /// news everyone would speak of (wars, the fall of realms, towns changing hands, deaths and
    /// marriages of the great). Everything is best-effort per entry: a throwing datum drops that one
    /// event, never the block. The final prose is shaped by <see cref="TidingsFormatter"/> (Core, tested).
    /// </summary>
    public static class TidingsBuilder
    {
        // The log keeps most entries only days anyway (KeepInHistoryTime), but both rails guard a
        // long-running campaign: nothing older than this is news, and no more than this many entries
        // are ever walked on one chat open.
        private const float MaxAgeDays = 21f;
        private const int MaxEntriesScanned = 3000;

        public static string Build(Hero speaker, Hero? partner, int maxTidings, int maxRumors)
        {
            if (speaker == null || (maxTidings <= 0 && maxRumors <= 0)) return string.Empty;
            try
            {
                var logs = Campaign.Current?.LogEntryHistory?.GameActionLogs;
                if (logs == null || logs.Count == 0) return string.Empty;

                var settlement = speaker.CurrentSettlement ?? Settlement.CurrentSettlement;
                var tidings = new List<Candidate>();
                var rumors = new List<Candidate>();

                int stop = Math.Max(0, logs.Count - MaxEntriesScanned);
                for (int i = logs.Count - 1; i >= stop; i--)
                {
                    var entry = logs[i];
                    if (entry == null) continue;

                    float age;
                    try { age = entry.GameTime.ElapsedDaysUntilNow; } catch { continue; }
                    if (age > MaxAgeDays) break; // the log is chronological — everything before is older still

                    try { if (!entry.IsValid()) continue; } catch { continue; }

                    if (maxTidings > 0 && !(entry is PlayerMeetLordLogEntry)) // "you met X" logs spam importance for every clan
                    {
                        int score = Score(entry, speaker, partner);
                        if (score >= 2)
                        {
                            var text = RenderFact(entry);
                            if (text.Length > 0)
                                tidings.Add(new Candidate(entry.Id, score, age, text));
                        }
                    }

                    if (maxRumors > 0 && settlement != null)
                    {
                        try
                        {
                            int weight = entry.GetAsRumor(settlement, out var comment);
                            var text = TidingsFormatter.StripMarkup(comment?.ToString());
                            if (weight > 0 && text.Length > 0)
                                rumors.Add(new Candidate(entry.Id, weight, age, text));
                        }
                        catch { /* this event will not be whispered about */ }
                    }
                }

                // The most telling few, then back into the order they happened so the account reads
                // like days passing. Repeated identical news (a village raided again and again) is
                // told once, and an event already given as a tiding is not repeated as street-talk.
                var chosenTidings = tidings
                    .OrderByDescending(c => c.Score).ThenBy(c => c.AgeDays)
                    .GroupBy(c => c.Text).Select(g => g.First())
                    .Take(maxTidings)
                    .OrderByDescending(c => c.AgeDays)
                    .ToList();
                var tidingIds = new HashSet<long>(chosenTidings.Select(c => c.EntryId));
                var chosenRumors = rumors
                    .Where(c => !tidingIds.Contains(c.EntryId))
                    .OrderByDescending(c => c.Score).ThenBy(c => c.AgeDays)
                    .GroupBy(c => c.Text).Select(g => g.First())
                    .Take(maxRumors)
                    .ToList();

                return TidingsFormatter.Compose(
                    chosenTidings.Select(c => TidingsFormatter.TidingLine(c.Text, c.AgeDays)).ToList(),
                    chosenRumors.Select(c => TidingsFormatter.RumorLine(c.Text)).ToList(),
                    settlement?.Name?.ToString());
            }
            catch
            {
                return string.Empty; // no tidings is always safe; a broken block never sinks the situation
            }
        }

        private struct Candidate
        {
            public readonly long EntryId;
            public readonly int Score;
            public readonly float AgeDays;
            public readonly string Text;
            public Candidate(long entryId, int score, float ageDays, string text)
            { EntryId = entryId; Score = score; AgeDays = ageDays; Text = text; }
        }

        // How much this event matters to this speaker, on the game's own 0..9 importance scale:
        // the vanilla per-hero conversation score, the per-clan importance for both sides of the
        // conversation, and our own baseline for news the whole land would speak of.
        private static int Score(LogEntry entry, Hero speaker, Hero? partner)
        {
            int score = 0;
            try
            {
                entry.GetConversationScoreAndComment(speaker, findString: false, out _, out var s);
                score = (int)s;
            }
            catch { }
            try { if (speaker.Clan != null) score = Math.Max(score, (int)entry.GetImportanceForClan(speaker.Clan)); } catch { }
            try { if (partner?.Clan != null) score = Math.Max(score, (int)entry.GetImportanceForClan(partner.Clan)); } catch { }
            try { score = Math.Max(score, BaselineNewsScore(entry, speaker, partner)); } catch { }
            return score;
        }

        // News that travels on its own legs, whether or not the game scores it for this clan: wars and
        // their endings, realms falling, towns changing hands, and the deaths, weddings and triumphs of
        // notable people — weighted up when the speaker's or partner's own folk are touched.
        private static int BaselineNewsScore(LogEntry entry, Hero speaker, Hero? partner)
        {
            switch (entry)
            {
                case DeclareWarLogEntry war:
                    return TouchesFaction(speaker, partner, war.Faction1, war.Faction2) ? 6 : 4;
                case MakePeaceLogEntry peace:
                    return TouchesFaction(speaker, partner, peace.Faction1, peace.Faction2) ? 5 : 3;
                case KingdomDestroyedLogEntry _:
                    return 7;
                case RebellionStartedLogEntry _:
                    return 4;
                case ChangeSettlementOwnerLogEntry owner:
                    return TouchesFaction(speaker, partner, owner.PreviousClan?.MapFaction, owner.NewClan?.MapFaction) ? 6 : 3;
                case BesiegeSettlementLogEntry siege:
                    return TouchesFaction(speaker, partner, siege.Settlement?.MapFaction, siege.BesiegerFaction) ? 5 : 3;
                case CharacterKilledLogEntry killed:
                    if (TouchesClan(speaker, partner, killed.VictimClan ?? killed.Victim?.Clan)) return 7;
                    return IsNotable(killed.Victim) ? 5 : 2;
                case CharacterMarriedLogEntry married:
                    return TouchesClan(speaker, partner, married.MarriedHero?.Clan)
                        || TouchesClan(speaker, partner, married.MarriedTo?.Clan) ? 5 : 2;
                case ChildbirthLogEntry birth:
                    return TouchesClan(speaker, partner, birth.Mother?.Clan) ? 4 : 1;
                case TournamentWonLogEntry tourney:
                    if (tourney.Winner == partner || tourney.Winner == speaker) return 5;
                    return tourney.Town?.Settlement == Settlement.CurrentSettlement ? 2 : 1;
                default:
                    return 0;
            }
        }

        private static bool TouchesFaction(Hero speaker, Hero? partner, params IFaction?[] factions)
        {
            var mine = speaker?.MapFaction;
            var theirs = partner?.MapFaction;
            foreach (var f in factions)
                if (f != null && (f == mine || f == theirs)) return true;
            return false;
        }

        private static bool TouchesClan(Hero speaker, Hero? partner, Clan? clan) =>
            clan != null && (clan == speaker?.Clan || clan == partner?.Clan);

        private static bool IsNotable(Hero? h) =>
            h != null && (h.IsFactionLeader || (h.Clan != null && h.Clan.Leader == h));

        // The plain fact of what happened, in the game's own words — the notification line lords and
        // the encyclopedia already use ("X won the tournament at Y."), markup stripped for plain ears.
        private static string RenderFact(LogEntry entry)
        {
            try
            {
                if (entry is IChatNotification chat)
                {
                    var text = TidingsFormatter.StripMarkup(chat.GetNotificationText()?.ToString());
                    if (text.Length > 0) return text;
                }
            }
            catch { }
            try
            {
                if (entry is IEncyclopediaLog ency)
                    return TidingsFormatter.StripMarkup(ency.GetEncyclopediaText()?.ToString());
            }
            catch { }
            return string.Empty;
        }
    }
}
