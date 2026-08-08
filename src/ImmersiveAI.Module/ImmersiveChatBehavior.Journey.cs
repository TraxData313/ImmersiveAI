using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Journey;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ImmersiveAI
{
    /// <summary>
    /// The road journal — the witness log of the player's everyday campaign life (Anton's ask,
    /// 2026.08.08: "търговия, войници, задачи — да ги въвлечем повече в живота на играта"). The
    /// souls riding WITH the player see, in their situation, the last few stops of the road —
    /// where we called and for how long, what we traded (values, the chief pieces), whom we hired
    /// or left on walls, captives sold or given to dungeons — and the tasks the company carries,
    /// each settled later with its honest outcome (the game's own completion detail gives the
    /// reason: succeeded, failed, the time ran out, our own broken word). The freshest stop is
    /// told whole; older ones are one line each; nothing here ever grows into a great ledger
    /// (Core's <see cref="JourneyLog"/> prunes, <see cref="JourneyText"/> speaks).
    ///
    /// Everything is captured from the game's own campaign events — no polling, no Harmony — and
    /// persisted as _journey.json in the campaign folder, so the save-scoped snapshots rewind the
    /// road together with the memories. Best-effort throughout: a lost line must never touch play.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private JourneyLog? _journeyLog;

        private void LoadJourneyLog()
        {
            try { _journeyLog = JourneyLog.LoadFrom(NpcPaths.JourneyFile); }
            catch { _journeyLog = new JourneyLog(); }
        }

        private void SaveJourneyLog()
        {
            try { _journeyLog?.SaveTo(NpcPaths.JourneyFile); }
            catch { /* the journal is a nicety; never let it touch play */ }
        }

        private bool JournalOn => _config.EnableJourneyLog && _journeyLog != null;

        // ------------------------------ the stops ------------------------------

        private void OnSettlementEnteredForJourney(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                if (!JournalOn || party != MobileParty.MainParty || settlement == null) return;
                var kind = settlement.IsCastle ? JourneyVisit.Kinds.Castle
                    : settlement.IsVillage ? JourneyVisit.Kinds.Village : JourneyVisit.Kinds.Town;
                _journeyLog!.BeginVisit(settlement.Name?.ToString() ?? string.Empty, kind,
                    CampaignTime.Now.ToDays, CalradiaDate());
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        private void OnSettlementLeftForJourney(MobileParty party, Settlement settlement)
        {
            try
            {
                if (!JournalOn || party != MobileParty.MainParty) return;
                _journeyLog!.CloseOpenVisit(CampaignTime.Now.ToDays);
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        // ------------------------------ trade ------------------------------

        // Fired when the player closes an inventory exchange; each tuple's int is the denars that
        // actually changed hands for that stack. Only true TRADE is journaled — battle loot and
        // discards ride the same event with isTrading false, and spoils already have the chronicle.
        private void OnPlayerTradeForJourney(List<(ItemRosterElement, int)> purchased,
            List<(ItemRosterElement, int)> sold, bool isTrading)
        {
            try
            {
                if (!JournalOn || !isTrading) return;
                if ((purchased == null || purchased.Count == 0) && (sold == null || sold.Count == 0)) return;

                var visit = _journeyLog!.CurrentOrRoad(CampaignTime.Now.ToDays, CalradiaDate());
                NoteTradeSide(purchased, v => visit.BoughtValue += v, visit.BoughtNotable);
                NoteTradeSide(sold, v => visit.SoldValue += v, visit.SoldNotable);
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        private static void NoteTradeSide(List<(ItemRosterElement, int)>? lines, Action<int> addValue, List<string> notable)
        {
            if (lines == null) return;
            foreach (var (element, denars) in lines)
                addValue(Math.Abs(denars));

            // The chief pieces of this exchange — by what they fetched — merged into the visit's
            // short list, which stays short (a manifest is exactly what this journal is not).
            foreach (var (element, denars) in lines.OrderByDescending(l => Math.Abs(l.Item2)).Take(3))
            {
                if (notable.Count >= 6) break;
                var name = Safe(() => element.EquipmentElement.Item?.Name?.ToString(), (string?)null);
                if (name != null) JourneyVisit.AddCounted(notable, name, Math.Max(1, element.Amount));
            }
        }

        // ------------------------------ men taken on, men left behind ------------------------------

        private void OnTroopRecruitedForJourney(Hero recruiter, Settlement settlement, Hero source,
            CharacterObject troop, int count)
        {
            try
            {
                if (!JournalOn || recruiter != Hero.MainHero || troop == null) return;
                var visit = _journeyLog!.CurrentOrRoad(CampaignTime.Now.ToDays, CalradiaDate());
                JourneyVisit.AddCounted(visit.Recruited, troop.Name?.ToString() ?? "a recruit", count);
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        private void OnTroopGivenToSettlementForJourney(Hero giver, Settlement settlement, TroopRoster roster)
        {
            try
            {
                if (!JournalOn || giver != Hero.MainHero || roster == null) return;
                var visit = _journeyLog!.CurrentOrRoad(CampaignTime.Now.ToDays, CalradiaDate());
                foreach (var el in roster.GetTroopRoster())
                {
                    if (el.Character == null || el.Number <= 0) continue;
                    JourneyVisit.AddCounted(visit.LeftInGarrison, el.Character.Name?.ToString() ?? "a soldier", el.Number);
                }
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        // ------------------------------ captives ------------------------------

        private void OnPrisonerSoldForJourney(PartyBase seller, PartyBase buyer, TroopRoster prisoners)
        {
            try
            {
                if (!JournalOn || seller != PartyBase.MainParty || prisoners == null) return;
                var visit = _journeyLog!.CurrentOrRoad(CampaignTime.Now.ToDays, CalradiaDate());
                visit.PrisonersSold += Math.Max(0, prisoners.TotalManCount);
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        private void OnPrisonerDonatedForJourney(MobileParty party, FlattenedTroopRoster roster, Settlement settlement)
        {
            try
            {
                if (!JournalOn || party != MobileParty.MainParty || roster == null) return;
                int count = 0;
                foreach (var _ in roster) count++;
                if (count <= 0) return;
                var visit = _journeyLog!.CurrentOrRoad(CampaignTime.Now.ToDays, CalradiaDate());
                visit.PrisonersDonated += count;
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        // ------------------------------ the tasks ------------------------------

        private void OnQuestStartedForJourney(QuestBase quest)
        {
            try
            {
                if (!JournalOn || quest == null) return;
                int daysGiven = 0;
                if (!Safe(() => quest.IsRemainingTimeHidden, true))
                {
                    double days = Safe(() => (quest.QuestDueTime - CampaignTime.Now).ToDays, 0.0);
                    if (days > 0 && days < 500) daysGiven = (int)Math.Round(days);
                }
                _journeyLog!.NoteQuestTaken(
                    Safe(() => quest.Title?.ToString(), (string?)null) ?? "a task",
                    Safe(() => quest.QuestGiver?.Name?.ToString(), (string?)null) ?? string.Empty,
                    CampaignTime.Now.ToDays, CalradiaDate(), daysGiven);
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        private void OnQuestCompletedForJourney(QuestBase quest, QuestBase.QuestCompleteDetails detail)
        {
            try
            {
                if (!JournalOn || quest == null) return;
                string outcome;
                switch (detail)
                {
                    case QuestBase.QuestCompleteDetails.Success: outcome = JourneyText.Outcomes.Success; break;
                    case QuestBase.QuestCompleteDetails.Timeout: outcome = JourneyText.Outcomes.Timeout; break;
                    case QuestBase.QuestCompleteDetails.FailWithBetrayal: outcome = JourneyText.Outcomes.Betrayal; break;
                    case QuestBase.QuestCompleteDetails.Cancel: outcome = JourneyText.Outcomes.Canceled; break;
                    default: outcome = JourneyText.Outcomes.Fail; break;
                }
                _journeyLog!.NoteQuestResolved(
                    Safe(() => quest.Title?.ToString(), (string?)null) ?? "a task",
                    Safe(() => quest.QuestGiver?.Name?.ToString(), (string?)null) ?? string.Empty,
                    outcome, CampaignTime.Now.ToDays);
                SaveJourneyLog();
            }
            catch { /* best-effort */ }
        }

        // ------------------------------ what the situation reads ------------------------------

        /// <summary>The road journal as one witness carries it — only for souls truly riding WITH
        /// the player (the party is the witness; a tavern-keeper never saw our road). Empty
        /// otherwise, and empty when the journal holds nothing worth a word.</summary>
        internal string JourneyBlock(Hero speaker, Hero partner)
        {
            try
            {
                if (!JournalOn) return string.Empty;
                if (speaker == null || partner != Hero.MainHero) return string.Empty;
                if (Safe(() => speaker.PartyBelongedTo, (MobileParty?)null) != MobileParty.MainParty) return string.Empty;
                return JourneyText.SituationBlock(_journeyLog!, CampaignTime.Now.ToDays);
            }
            catch { return string.Empty; }
        }
    }
}
