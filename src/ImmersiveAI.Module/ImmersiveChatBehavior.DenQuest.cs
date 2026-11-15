using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Handmaids;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImmersiveAI
{
    /// <summary>
    /// THE DEN QUEST (2026.11.14, the owner's ask: replayability). The captive in the den is no
    /// longer found in the first hideout of a campaign but in the den a VILLAGE asks the player to
    /// clear: "Clear the {den type} den". A village's giver is one of its notables drawn by lot and
    /// kept in the save; the den type is the bandit people of the hideout nearest that village, so
    /// the map decides where each girl is sought. Clearing the marked den, or ANY den of that
    /// people while the quest runs, succeeds it and brings the soldier's report of the captive
    /// (the Handmaid partial, unchanged).
    ///
    /// A VILLAGE ISSUE since 2026.11.15 (the owner: "a normal quest, like those the villages give",
    /// no special menu, not more frequent than the others): the game's issue manager asks us at its
    /// own issue check (<see cref="OnCheckForDenIssue"/>) and picks among every behavior's issues at
    /// their frequencies, so the den quest stands on some notables of some villages, with the
    /// game's blue mark and issue talk (<see cref="ImmersiveDenIssue"/>). The old village menu,
    /// entry notice and 14-day village windows are gone. DevMode keeps its own offer lines in any
    /// village notable's talk (the den type chosen there), through <see cref="VillageOffersDen"/>.
    ///
    /// Once per campaign like the meeting: after it has happened no village offers the quest again
    /// (DevMode: every village, always, with the den type chosen in his talk). The giver's talk is
    /// scripted vanilla dialog, no LLM, never a meeting note. The quest class is
    /// <see cref="ImmersiveDenQuest"/>; this partial finds givers and dens and drives it.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private const string DenMenuVar = "IMMERSIVEAI_DEN_MENU";
        private const string DenDevOfferVar = "IMMERSIVEAI_DEN_DEV_OFFER_";
        private const string DenStoryVar = "IMMERSIVEAI_DEN_STORY";
        private const string DenAskVar = "IMMERSIVEAI_DEN_ASK";
        private const string DenStillVar = "IMMERSIVEAI_DEN_STILL";
        private const string DenGiveUpVar = "IMMERSIVEAI_DEN_GIVE_UP";

        // ── Saved ──
        // "villageId|heroId": the notable who offers the den quest in that village, kept for the
        // campaign. Marks saved for a TOWN before 2026.11.14 stay here unused (towns offer nothing).
        private List<string> _denGivers = new List<string>();
        // "villageId|window": the village's offer notice was shown in that window. (The saved key
        // keeps its old "Towns" name; plain town ids from before are harmless and never matched.)
        private List<string> _denNoticedTowns = new List<string>();

        // ── Session only ──
        private readonly Dictionary<string, string> _denVillageCulture = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> DenNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private DenTalk? _denTalk;
        private Settlement? _denWonHideout;

        private sealed class DenTalk
        {
            public DenTalk(Hero giver, Settlement? home) { Giver = giver; Home = home; }
            public Hero Giver { get; }
            public Settlement? Home { get; }
            public string? Chosen { get; set; }
        }

        private void SyncDenQuestData(IDataStore dataStore)
        {
            dataStore.SyncData("ImmersiveAI_DenGivers", ref _denGivers);
            dataStore.SyncData("ImmersiveAI_DenNoticedTowns", ref _denNoticedTowns);
            if (_denGivers == null) _denGivers = new List<string>();
            if (_denNoticedTowns == null) _denNoticedTowns = new List<string>();
        }

        private void RegisterDenQuestEvents()
        {
            CampaignEvents.OnHideoutDeactivatedEvent.AddNonSerializedListener(this, OnHideoutDeactivatedForDen);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEndedForDen);
            CampaignEvents.OnCheckForIssueEvent.AddNonSerializedListener(this, OnCheckForDenIssue);
        }

        // ------------------------------ the village issue ------------------------------

        /// <summary>The game's issue check for one hero: a village notable near a den of one of the five
        /// peoples, while the captive can still be met, MAY carry the den issue; the issue manager
        /// decides whether he does, among all his possible issues, at the Common frequency.</summary>
        private void OnCheckForDenIssue(Hero hero)
        {
            try
            {
                string? culture = null;
                var village = hero?.CurrentSettlement;
                bool can = hero != null && hero.IsNotable && hero.IsAlive && !hero.IsChild
                    && village != null && village.IsVillage && village.Village != null
                    && !village.IsRaided && !village.IsUnderRaid
                    && DenOffersOpen()
                    && (culture = VillageDenCulture(village)) != null
                    && NearestActiveDen(village, culture, null) != null;
                var issues = Campaign.Current?.IssueManager;
                if (issues == null || hero == null) return;
                if (can)
                    issues.AddPotentialIssueData(hero, new TaleWorlds.CampaignSystem.Issues.PotentialIssueData(
                        OnDenIssueSelected, typeof(ImmersiveDenIssue), TaleWorlds.CampaignSystem.Issues.IssueBase.IssueFrequency.Common, culture));
                else
                    issues.AddPotentialIssueData(hero, new TaleWorlds.CampaignSystem.Issues.PotentialIssueData(
                        typeof(ImmersiveDenIssue), TaleWorlds.CampaignSystem.Issues.IssueBase.IssueFrequency.Common));
            }
            catch (Exception ex) { ModLog.Error("den issue: the issue check", ex); }
        }

        private static TaleWorlds.CampaignSystem.Issues.IssueBase OnDenIssueSelected(
            in TaleWorlds.CampaignSystem.Issues.PotentialIssueData pid, Hero issueOwner) =>
            new ImmersiveDenIssue(issueOwner, pid.RelatedObject as string ?? string.Empty);

        /// <summary>The issue stays while its quest runs, or while the captive can still be met, the
        /// village stands unharmed and its people still hold a den.</summary>
        internal static bool DenIssueMayStand(ImmersiveDenIssue issue, Settlement? village)
        {
            if (issue.IsSolvingWithQuest) return true;
            var b = Current;
            if (b == null || village == null || !village.IsVillage || village.IsRaided || village.IsUnderRaid) return false;
            if (!b.DenOffersOpen()) return false;
            return NearestActiveDen(village, issue.DenCulture, null) != null;
        }

        /// <summary>The den the quest marks when it starts: the nearest of its people's, or null to wait.</summary>
        internal static Settlement? NearestActiveDenFor(Settlement? from, string culture) => NearestActiveDen(from, culture, null);

        // ------------------------------ the facts ------------------------------

        /// <summary>The den quest the player carries now, or null.</summary>
        internal static ImmersiveDenQuest? ActiveDenQuest()
        {
            try
            {
                var quests = Campaign.Current?.QuestManager?.Quests;
                if (quests == null) return null;
                foreach (var q in quests)
                    if (q is ImmersiveDenQuest den && den.IsOngoing) return den;
                return null;
            }
            catch { return null; }
        }

        /// <summary>The den type as the player reads it: the bandit faction's own in-game name.</summary>
        internal static string DenNameOf(string? culture)
        {
            var key = culture ?? string.Empty;
            try
            {
                if (DenNames.TryGetValue(key, out var known)) return known;
                string? gameName = null;
                foreach (var clan in Clan.All)
                {
                    if (clan == null || !clan.IsBanditFaction) continue;
                    if (!string.Equals(clan.Culture?.StringId, key, StringComparison.OrdinalIgnoreCase)) continue;
                    gameName = clan.Name?.ToString();
                    if (!string.IsNullOrWhiteSpace(gameName)) break;
                }
                var name = HandmaidQuestText.DenName(key, gameName);
                DenNames[key] = name;
                return name;
            }
            catch { return HandmaidQuestText.DenName(key); }
        }

        /// <summary>Whether any village may offer the quest now: switched on, the meeting not yet had
        /// (DevMode: always), no den quest running and no meeting waiting or under way.</summary>
        private bool DenOffersOpen()
        {
            if (_config == null || !_config.EnableHideoutHandmaid) return false;
            if (!_config.DevMode && _handmaidEventDone) return false;
            if (_handmaidPendingDen != null || _handmaidMeeting != null || _handmaidSoldier != null) return false;
            return ActiveDenQuest() == null;
        }

        /// <summary>DevMode's offer door: any village notable's talk offers the den quest, the den type
        /// chosen there. Normal play has no such door: the village issue is the only way in.</summary>
        private bool VillageOffersDen(Settlement? village)
        {
            if (village == null || !village.IsVillage) return false;
            if (_config == null || !_config.DevMode) return false;
            return DenOffersOpen();
        }

        /// <summary>The bandit people of the hideout nearest this village, among the five the roster
        /// knows; null when the map holds none of theirs. Hideouts never move, so it is kept.</summary>
        private string? VillageDenCulture(Settlement? village)
        {
            if (village == null || !village.IsVillage) return null;
            if (_denVillageCulture.TryGetValue(village.StringId, out var known)) return known.Length == 0 ? null : known;
            string best = string.Empty;
            float bestDist = float.MaxValue;
            try
            {
                foreach (var h in Hideout.All)
                {
                    var s = h?.Settlement;
                    var c = s?.Culture?.StringId;
                    if (s == null || string.IsNullOrEmpty(c) || !HandmaidQuestText.HasStory(c)) continue;
                    var d = s.Position.DistanceSquared(village.Position);
                    if (d < bestDist) { bestDist = d; best = c!; }
                }
            }
            catch (Exception ex) { ModLog.Error("den quest: finding a village's den type", ex); return null; }
            _denVillageCulture[village.StringId] = best;
            return best.Length == 0 ? null : best;
        }

        /// <summary>The notable who offers it in this village: the one kept for the campaign while he
        /// lives and stays there, else any adult notable of the village (headman, landowner, any
        /// rural notable) drawn by lot once and kept. No notable, no offer.</summary>
        private Hero? DenGiverOf(Settlement? village)
        {
            if (village == null || !village.IsVillage) return null;
            try
            {
                var notables = village.Notables;
                if (notables == null || notables.Count == 0) return null;
                var prefix = village.StringId + "|";
                var mark = _denGivers.FirstOrDefault(m => m != null && m.StartsWith(prefix, StringComparison.Ordinal));
                var keptId = mark?.Substring(prefix.Length);
                if (!string.IsNullOrEmpty(keptId))
                {
                    var kept = notables.FirstOrDefault(n => n != null && n.StringId == keptId);
                    if (kept != null && kept.IsAlive) return kept;
                }

                var able = notables.Where(n => n != null && n.IsAlive && !n.IsChild && n != Hero.MainHero).ToList();
                if (able.Count == 0) return null;
                // Any notable of the village, by lot (the owner, 2026.11.14); the lot is drawn once
                // and kept in the save, so he stays the same man.
                var chosen = able[MBRandom.RandomInt(able.Count)];
                _denGivers.RemoveAll(m => m == null || m.StartsWith(prefix, StringComparison.Ordinal));
                _denGivers.Add(prefix + chosen.StringId);
                return chosen;
            }
            catch (Exception ex) { ModLog.Error("den quest: choosing a village's notable", ex); return null; }
        }

        private static bool HasBandits(Settlement s) => s.Parties.Any(p => p != null && p.IsBandit);

        /// <summary>The nearest den of this people still held by bandits (infested ones first, the
        /// way vanilla's hideout quest picks), or null when they hold none.</summary>
        private static Settlement? NearestActiveDen(Settlement? from, string culture, Settlement? except)
        {
            try
            {
                Settlement? best = null;
                bool bestInfested = false;
                float bestDist = float.MaxValue;
                foreach (var h in Hideout.All)
                {
                    var s = h?.Settlement;
                    if (s == null || s == except) continue;
                    if (!string.Equals(s.Culture?.StringId, culture, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!HasBandits(s)) continue;
                    bool infested = h!.IsInfested;
                    var d = from != null ? s.Position.DistanceSquared(from.Position) : 0f;
                    if (best == null || (infested && !bestInfested) || (infested == bestInfested && d < bestDist))
                    { best = s; bestInfested = infested; bestDist = d; }
                }
                return best;
            }
            catch (Exception ex) { ModLog.Error("den quest: finding a den", ex); return null; }
        }

        // ------------------------------ finding it ------------------------------

        private void AddDenQuestMenus(CampaignGameStarter starter)
        {
            // No offer in the village menu since 2026.11.15: the village issue is the way in.
            // The way out once the giver of a STANDALONE quest (old saves, dev) is dead: in villages (where givers live now) and in towns
            // (a quest given by a town notable before 2026.11.14).
            foreach (var menuId in new[] { "village", "town" })
                starter.AddGameMenuOption(menuId, "immersiveai_den_quest_give_up_" + menuId,
                    "{=!}{" + DenMenuVar + "}", OnDenGiveUpMenuCondition, _ => OnDenGiveUpMenuChosen(), false, -1, false, null);
        }

        // Vanilla's quest log has no "abandon" in this version; the giver is where the quest is given
        // back. Only when he can no longer be asked does any village (or town) offer this way out.
        private bool OnDenGiveUpMenuCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                var here = Settlement.CurrentSettlement;
                if (here == null || !(here.IsVillage || here.IsTown)) return false;
                var quest = ActiveDenQuest();
                if (quest == null || quest.GiverStillHere || quest.QuestGiver != null) return false;
                args.Text?.SetTextVariable(DenMenuVar, HandmaidQuestText.MenuGiveUp(quest.DenName));
                return true;
            }
            catch { return false; }
        }

        private void OnDenGiveUpMenuChosen()
        {
            try
            {
                var quest = ActiveDenQuest();
                if (quest == null) return;
                InformationManager.ShowInquiry(new InquiryData(
                    HandmaidQuestText.GiveUpConfirmTitle,
                    HandmaidQuestText.GiveUpConfirmBody(quest.DenName, quest.Giver?.Name?.ToString()),
                    true, true, HandmaidQuestText.GiveUpYes, HandmaidQuestText.GiveUpNo,
                    () =>
                    {
                        try { if (quest.IsOngoing) quest.Abandon(); ModLog.Info("den quest: given up (the giver is gone)."); }
                        catch (Exception ex) { ModLog.Error("den quest: giving it up", ex); }
                    },
                    null));
            }
            catch (Exception ex) { ModLog.Error("den quest: the give-up question", ex); }
        }

        // ------------------------------ the giver's talk ------------------------------

        private Hero? DenOfferGiverHere()
        {
            var hero = Hero.OneToOneConversationHero;
            var village = Settlement.CurrentSettlement;
            if (hero == null || !VillageOffersDen(village)) return null;
            return DenGiverOf(village) == hero ? hero : null;
        }

        /// <summary>DevMode: one offer line per den type, the village's own first and marked.</summary>
        private bool IsDenDevOfferLine(int index)
        {
            try
            {
                if (_config == null || !_config.DevMode) return false;
                if (DenOfferGiverHere() == null) return false;
                var own = VillageDenCulture(Settlement.CurrentSettlement);
                var order = HandmaidQuestText.DevOrder(own);
                if (index >= order.Count) return false;
                var c = order[index];
                MBTextManager.SetTextVariable(DenDevOfferVar + index,
                    HandmaidQuestText.PlayerAsk(DenNameOf(c), devVillagesOwn: own != null && string.Equals(c, own, StringComparison.OrdinalIgnoreCase)), false);
                return true;
            }
            catch { return false; }
        }

        private void ChooseDen(string? culture)
        {
            try
            {
                var hero = Hero.OneToOneConversationHero;
                if (hero == null || string.IsNullOrEmpty(culture)) return;
                if (_denTalk == null || _denTalk.Giver != hero)
                    _denTalk = new DenTalk(hero, Settlement.CurrentSettlement);
                _denTalk.Chosen = culture;
                MBTextManager.SetTextVariable(DenStoryVar, HandmaidQuestText.StoryFor(culture), false);
                int men = 0;
                try { men = (MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1) - 1; } catch { }
                MBTextManager.SetTextVariable(DenAskVar, HandmaidQuestText.GiverAsk(men), false);
            }
            catch (Exception ex) { ModLog.Error("den quest: choosing the den", ex); }
        }

        private void ChooseDevDen(int index)
        {
            var own = VillageDenCulture(Settlement.CurrentSettlement);
            var order = HandmaidQuestText.DevOrder(own);
            ChooseDen(index < order.Count ? order[index] : null);
        }

        private bool IsDenStoryTalk()
        {
            var t = _denTalk;
            return t != null && t.Chosen != null && Hero.OneToOneConversationHero == t.Giver;
        }

        /// <summary>The quest's giver stands before the player while his quest runs.</summary>
        private bool IsDenQuestGiverTalk()
        {
            try
            {
                var hero = Hero.OneToOneConversationHero;
                var q = ActiveDenQuest();
                // The village issue's quest talks through vanilla's own discuss flow.
                if (hero == null || q == null || q.Giver != hero || q.QuestGiver != null) return false;
                MBTextManager.SetTextVariable(DenStillVar, HandmaidQuestText.PlayerStill(q.DenName), false);
                MBTextManager.SetTextVariable(DenGiveUpVar, HandmaidQuestText.PlayerGiveUp(q.DenName), false);
                return true;
            }
            catch { return false; }
        }

        private bool IsDenTalkHero(Hero? hero) => hero != null && _denTalk != null && _denTalk.Giver == hero;

        private void MarkDenTalk()
        {
            var hero = Hero.OneToOneConversationHero;
            if (hero != null && (_denTalk == null || _denTalk.Giver != hero))
                _denTalk = new DenTalk(hero, Settlement.CurrentSettlement);
        }

        private void AddDenQuestDialogs(CampaignGameStarter starter)
        {
            // DevMode's offer lines and a standalone quest's word with its giver, in his ordinary talk.
            foreach (var (input, tag, prio) in new[] { ("hero_main_options", "hm", 110) })
            {
                for (int i = 0; i < HandmaidRoster.All.Count; i++)
                {
                    int index = i;
                    starter.AddPlayerLine("immersiveai_den_dev_offer_" + tag + "_" + index, input, "immersiveai_den_story",
                        "{=!}{" + DenDevOfferVar + index + "}", () => IsDenDevOfferLine(index),
                        () => ChooseDevDen(index), prio - 1 - index);
                }

                // While his quest runs: a word on it, and the way to give it back.
                starter.AddPlayerLine("immersiveai_den_still_" + tag, input, "immersiveai_den_still_line_" + tag,
                    "{=!}{" + DenStillVar + "}", IsDenQuestGiverTalk, MarkDenTalk, prio);
                starter.AddDialogLine("immersiveai_den_still_answer_" + tag, "immersiveai_den_still_line_" + tag, input,
                    "{=!}" + HandmaidQuestText.GiverStill, null, null, 200);
                starter.AddPlayerLine("immersiveai_den_give_up_" + tag, input, "immersiveai_den_give_up_line_" + tag,
                    "{=!}{" + DenGiveUpVar + "}", IsDenQuestGiverTalk, MarkDenTalk, prio - 1);
                starter.AddDialogLine("immersiveai_den_give_up_answer_" + tag, "immersiveai_den_give_up_line_" + tag, input,
                    "{=!}" + HandmaidQuestText.GiverGiveUp, null, AbandonDenQuestFromTalk, 200);
            }

            // His story, then his ask, then the player's answer.
            starter.AddDialogLine("immersiveai_den_story_line", "immersiveai_den_story", "immersiveai_den_ask",
                "{=!}{" + DenStoryVar + "}", IsDenStoryTalk, null, 200);
            starter.AddDialogLine("immersiveai_den_ask_line", "immersiveai_den_ask", "immersiveai_den_answer",
                "{=!}{" + DenAskVar + "}", IsDenStoryTalk, null, 200);
            starter.AddPlayerLine("immersiveai_den_accept", "immersiveai_den_answer", "immersiveai_den_accepted",
                "{=!}" + HandmaidQuestText.PlayerAccept, IsDenStoryTalk, null, 200);
            starter.AddPlayerLine("immersiveai_den_not_now", "immersiveai_den_answer", "close_window",
                "{=!}" + HandmaidQuestText.PlayerNotNow, IsDenStoryTalk, RequestLeaveFromPartyEncounter, 190);
            starter.AddDialogLine("immersiveai_den_accepted_line", "immersiveai_den_accepted", "close_window",
                "{=!}" + HandmaidQuestText.GiverAccepted, IsDenStoryTalk,
                () => { StartDenQuestFromTalk(); RequestLeaveFromPartyEncounter(); }, 200);
        }

        private void StartDenQuestFromTalk()
        {
            try
            {
                var t = _denTalk;
                if (t == null || t.Chosen == null) return;
                if (ActiveDenQuest() != null) { ModLog.Warn("den quest: one already runs; the new one is not started."); return; }
                var village = t.Home ?? t.Giver.CurrentSettlement;
                var culture = t.Chosen;
                var target = NearestActiveDen(village, culture, null);
                int reward = HandmaidQuestText.RewardGold;
                var id = "immersiveai_den_quest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var quest = new ImmersiveDenQuest(id, t.Giver, village!, culture, reward);
                quest.Begin(target);
                ModLog.Info($"den quest: {t.Giver.StringId} of {village?.StringId} asks for the {culture} den; target {target?.StringId ?? "(none yet)"}, reward {reward}.");
            }
            catch (Exception ex) { ModLog.Error("den quest: starting it", ex); }
        }

        private void AbandonDenQuestFromTalk()
        {
            try
            {
                var q = ActiveDenQuest();
                if (q == null) return;
                q.Abandon();
                ModLog.Info("den quest: given back to the giver.");
            }
            catch (Exception ex) { ModLog.Error("den quest: giving it back", ex); }
        }

        private void OnConversationEndedForDen(IEnumerable<CharacterObject> characters)
        {
            try
            {
                var t = _denTalk;
                if (t == null) return;
                if (characters == null || characters.Any(c => c?.HeroObject == t.Giver)) _denTalk = null;
            }
            catch { _denTalk = null; }
        }

        // ------------------------------ the den ------------------------------

        /// <summary>Called from the hideout-won hook. True when the cleared den answers the running
        /// quest (the marked one, or any den of its people); the quest succeeds a tick later, once
        /// the battle has finished unwinding.</summary>
        private bool NoteDenQuestWin(Settlement den)
        {
            try
            {
                var q = ActiveDenQuest();
                if (q == null) return false;
                if (den != q.Target && !string.Equals(den.Culture?.StringId, q.DenCulture, StringComparison.OrdinalIgnoreCase))
                    return false;
                _denWonHideout = den;
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        if (q.IsOngoing) q.Succeed();
                        ModLog.Info($"den quest: {den.StringId} cleared; the quest is done.");
                    }
                    catch (Exception ex) { ModLog.Error("den quest: completing it", ex); }
                    finally { if (_denWonHideout == den) _denWonHideout = null; }
                });
                return true;
            }
            catch (Exception ex) { ModLog.Error("den quest: noting the win", ex); return false; }
        }

        private void OnHideoutDeactivatedForDen(Settlement hideout)
        {
            try
            {
                var q = ActiveDenQuest();
                if (q == null || hideout == null || q.Target != hideout) return;
                // The player's own win: the battle hook decides, never a retarget.
                if (_denWonHideout == hideout) return;
                if (MapEvent.PlayerMapEvent?.MapEventSettlement == hideout) return;
                var next = NearestActiveDen(q.Home, q.DenCulture, hideout);
                q.Retarget(next, wasWaiting: false);
                ModLog.Info($"den quest: {hideout.StringId} went quiet; next {next?.StringId ?? "(none, waiting)"}.");
            }
            catch (Exception ex) { ModLog.Error("den quest: the den went quiet", ex); }
        }

        /// <summary>The quest's daily look: a quest waiting for a den finds the next one; a marked
        /// den that emptied without the event reaching us is replaced.</summary>
        internal static void DenQuestDailyCheck(ImmersiveDenQuest quest)
        {
            if (quest == null || !quest.IsOngoing) return;
            var target = quest.Target;
            if (target == null)
            {
                var next = NearestActiveDen(quest.Home, quest.DenCulture, null);
                if (next != null)
                {
                    quest.Retarget(next, wasWaiting: true);
                    ModLog.Info($"den quest: the {quest.DenCulture} gathered again at {next.StringId}.");
                }
                return;
            }
            if (Current?._denWonHideout == target) return;
            if (MapEvent.PlayerMapEvent?.MapEventSettlement == target) return;
            if (target.Parties.Count == 0)
            {
                var next = NearestActiveDen(quest.Home, quest.DenCulture, target);
                quest.Retarget(next, wasWaiting: false);
                ModLog.Info($"den quest: {target.StringId} stood empty; next {next?.StringId ?? "(none, waiting)"}.");
            }
        }
    }
}
