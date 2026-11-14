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
    /// longer found in the first hideout of a campaign but in the den a TOWN asks the player to
    /// clear: "Clear the {den type} den". In every town one notable offers it (the most powerful,
    /// a merchant preferred, kept for the campaign); the den type is the bandit people of the
    /// hideout nearest that town, so the map decides where each girl is sought. Clearing the
    /// marked den, or ANY den of that people while the quest runs, succeeds it and brings the
    /// soldier's report of the captive (the Handmaid partial, unchanged).
    ///
    /// Once per campaign like the meeting: after it has happened no town offers the quest again
    /// (DevMode: every town, always, with the den type chosen in his talk). The giver's talk is
    /// scripted vanilla dialog, no LLM, never a meeting note. The quest class is
    /// <see cref="ImmersiveDenQuest"/>; this partial finds givers and dens and drives it.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private const string DenMenuVar = "IMMERSIVEAI_DEN_MENU";
        private const string DenOfferVar = "IMMERSIVEAI_DEN_OFFER_ASK";
        private const string DenDevOfferVar = "IMMERSIVEAI_DEN_DEV_OFFER_";
        private const string DenStoryVar = "IMMERSIVEAI_DEN_STORY";
        private const string DenStillVar = "IMMERSIVEAI_DEN_STILL";
        private const string DenGiveUpVar = "IMMERSIVEAI_DEN_GIVE_UP";

        // ── Saved ──
        // "townId|heroId": the notable who offers the den quest in that town, kept for the campaign.
        private List<string> _denGivers = new List<string>();
        // Towns whose offer notice was already shown once this campaign.
        private List<string> _denNoticedTowns = new List<string>();

        // ── Session only ──
        private readonly Dictionary<string, string> _denTownCulture = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> DenNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private DenTalk? _denTalk;
        private Settlement? _denWonHideout;

        private sealed class DenTalk
        {
            public DenTalk(Hero giver, Settlement? town, bool fromMenu) { Giver = giver; Town = town; FromMenu = fromMenu; }
            public Hero Giver { get; }
            public Settlement? Town { get; }
            public bool FromMenu { get; }
            public bool Started { get; set; }
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
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEnteredForDen);
            CampaignEvents.OnHideoutDeactivatedEvent.AddNonSerializedListener(this, OnHideoutDeactivatedForDen);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEndedForDen);
        }

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

        /// <summary>Whether a town may offer the quest now: switched on, the meeting not yet had
        /// (DevMode: always), no den quest running and no meeting waiting or under way.</summary>
        private bool DenOffersOpen()
        {
            if (_config == null || !_config.EnableHideoutHandmaid) return false;
            if (!_config.DevMode && _handmaidEventDone) return false;
            if (_handmaidPendingDen != null || _handmaidMeeting != null || _handmaidSoldier != null) return false;
            return ActiveDenQuest() == null;
        }

        /// <summary>The bandit people of the hideout nearest this town, among the five the roster
        /// knows; null when the map holds none of theirs. Hideouts never move, so it is kept.</summary>
        private string? TownDenCulture(Settlement? town)
        {
            if (town == null || !town.IsTown) return null;
            if (_denTownCulture.TryGetValue(town.StringId, out var known)) return known.Length == 0 ? null : known;
            string best = string.Empty;
            float bestDist = float.MaxValue;
            try
            {
                foreach (var h in Hideout.All)
                {
                    var s = h?.Settlement;
                    var c = s?.Culture?.StringId;
                    if (s == null || string.IsNullOrEmpty(c) || !HandmaidQuestText.HasStory(c)) continue;
                    var d = s.Position.DistanceSquared(town.Position);
                    if (d < bestDist) { bestDist = d; best = c!; }
                }
            }
            catch (Exception ex) { ModLog.Error("den quest: finding a town's den type", ex); return null; }
            _denTownCulture[town.StringId] = best;
            return best.Length == 0 ? null : best;
        }

        /// <summary>The notable who offers it in this town: the one kept for the campaign while he
        /// lives and stays there, else the most powerful merchant, else the most powerful notable.</summary>
        private Hero? DenGiverOf(Settlement? town)
        {
            if (town == null || !town.IsTown) return null;
            try
            {
                var notables = town.Notables;
                if (notables == null || notables.Count == 0) return null;
                var prefix = town.StringId + "|";
                var mark = _denGivers.FirstOrDefault(m => m != null && m.StartsWith(prefix, StringComparison.Ordinal));
                var keptId = mark?.Substring(prefix.Length);
                if (!string.IsNullOrEmpty(keptId))
                {
                    var kept = notables.FirstOrDefault(n => n != null && n.StringId == keptId);
                    if (kept != null && kept.IsAlive) return kept;
                }

                var able = notables.Where(n => n != null && n.IsAlive && !n.IsChild && n != Hero.MainHero).ToList();
                if (able.Count == 0) return null;
                var pool = able.Any(n => n.IsMerchant) ? able.Where(n => n.IsMerchant).ToList() : able;
                var chosen = pool.OrderByDescending(n => n.Power).ThenBy(n => n.StringId, StringComparer.Ordinal).First();
                _denGivers.RemoveAll(m => m == null || m.StartsWith(prefix, StringComparison.Ordinal));
                _denGivers.Add(prefix + chosen.StringId);
                return chosen;
            }
            catch (Exception ex) { ModLog.Error("den quest: choosing a town's notable", ex); return null; }
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

        private void OnSettlementEnteredForDen(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                if (party == null || !party.IsMainParty || settlement == null || !settlement.IsTown) return;
                if (Hero.MainHero == null || Hero.MainHero.IsPrisoner) return;
                if (!DenOffersOpen()) return;
                if (_denNoticedTowns.Contains(settlement.StringId)) return;
                var culture = TownDenCulture(settlement);
                var giver = DenGiverOf(settlement);
                if (culture == null || giver == null) return;
                _denNoticedTowns.Add(settlement.StringId);
                InformationManager.DisplayMessage(new InformationMessage(
                    HandmaidQuestText.OfferNotice(DenNameOf(culture), giver.Name?.ToString(), settlement.Name?.ToString()),
                    ActivityColor));
            }
            catch (Exception ex) { ModLog.Error("den quest: the town's notice", ex); }
        }

        private void AddDenQuestMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "immersiveai_den_quest_talk",
                "{=!}{" + DenMenuVar + "}", OnDenMenuCondition, _ => OnDenMenuChosen(), false, -1, false, null);
            starter.AddGameMenuOption("town", "immersiveai_den_quest_give_up",
                "{=!}{" + DenMenuVar + "}", OnDenGiveUpMenuCondition, _ => OnDenGiveUpMenuChosen(), false, -1, false, null);
        }

        /// <summary>Who the town menu's den option would open a talk with, and about which den; null
        /// when the option should not show.</summary>
        private (Hero giver, string den)? DenMenuTarget()
        {
            var town = Settlement.CurrentSettlement;
            if (town == null || !town.IsTown || Hero.MainHero == null || Hero.MainHero.IsPrisoner) return null;
            if (_config == null || !_config.EnableHideoutHandmaid) return null;

            var quest = ActiveDenQuest();
            if (quest != null)
            {
                var g = quest.Giver;
                if (g == null || !g.IsAlive || g.CurrentSettlement != town) return null;
                return (g, quest.DenCulture);
            }

            if (!DenOffersOpen()) return null;
            var giver = DenGiverOf(town);
            if (giver == null || giver.CurrentSettlement != town) return null;
            var culture = TownDenCulture(town);
            if (culture == null)
            {
                if (!_config.DevMode) return null;
                culture = HandmaidQuestText.DenCultures[0];
            }
            return (giver, culture);
        }

        private bool OnDenMenuCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                var target = DenMenuTarget();
                if (target == null) return false;
                args.Text?.SetTextVariable(DenMenuVar,
                    HandmaidQuestText.MenuOption(DenNameOf(target.Value.den), target.Value.giver.Name?.ToString()));
                return true;
            }
            catch (Exception ex) { ModLog.Error("den quest: the town menu", ex); return false; }
        }

        private void OnDenMenuChosen()
        {
            try
            {
                var target = DenMenuTarget();
                if (target == null) return;
                var giver = target.Value.giver;
                var town = Settlement.CurrentSettlement;
                // A conversation never opens inside the menu's own click.
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        if (!giver.IsAlive || Hero.MainHero?.CurrentSettlement != town) return;
                        if (!IsSafeToInitiate()) return;
                        _denTalk = new DenTalk(giver, town, fromMenu: true);
                        OpenConversationWith(giver);
                    }
                    catch (Exception ex) { _denTalk = null; ModLog.Error("den quest: opening the giver's talk", ex); }
                });
            }
            catch (Exception ex) { ModLog.Error("den quest: the town menu's choice", ex); }
        }

        // Vanilla's quest log has no "abandon" in this version; the giver is where the quest is given
        // back. Only when he can no longer be asked does any town offer this way out.
        private bool OnDenGiveUpMenuCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                var town = Settlement.CurrentSettlement;
                if (town == null || !town.IsTown) return false;
                var quest = ActiveDenQuest();
                if (quest == null || quest.GiverStillHere) return false;
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
            if (hero == null || !DenOffersOpen()) return null;
            var town = Settlement.CurrentSettlement;
            if (town == null || !town.IsTown) return null;
            return DenGiverOf(town) == hero ? hero : null;
        }

        /// <summary>The single offer line (normal play): the town's own den type.</summary>
        private bool IsDenOfferLine()
        {
            try
            {
                if (_config == null || _config.DevMode) return false;
                if (DenOfferGiverHere() == null) return false;
                var culture = TownDenCulture(Settlement.CurrentSettlement);
                if (culture == null) return false;
                MBTextManager.SetTextVariable(DenOfferVar, HandmaidQuestText.PlayerAsk(DenNameOf(culture)), false);
                return true;
            }
            catch { return false; }
        }

        /// <summary>DevMode: one offer line per den type, the town's own first and marked.</summary>
        private bool IsDenDevOfferLine(int index)
        {
            try
            {
                if (_config == null || !_config.DevMode) return false;
                if (DenOfferGiverHere() == null) return false;
                var own = TownDenCulture(Settlement.CurrentSettlement);
                var order = HandmaidQuestText.DevOrder(own);
                if (index >= order.Count) return false;
                var c = order[index];
                MBTextManager.SetTextVariable(DenDevOfferVar + index,
                    HandmaidQuestText.PlayerAsk(DenNameOf(c), devTownsOwn: own != null && string.Equals(c, own, StringComparison.OrdinalIgnoreCase)), false);
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
                    _denTalk = new DenTalk(hero, Settlement.CurrentSettlement, fromMenu: false);
                _denTalk.Chosen = culture;
                MBTextManager.SetTextVariable(DenStoryVar, HandmaidQuestText.StoryFor(culture), false);
            }
            catch (Exception ex) { ModLog.Error("den quest: choosing the den", ex); }
        }

        private void ChooseDevDen(int index)
        {
            var own = TownDenCulture(Settlement.CurrentSettlement);
            var order = HandmaidQuestText.DevOrder(own);
            ChooseDen(index < order.Count ? order[index] : null);
        }

        private bool IsDenStoryTalk()
        {
            var t = _denTalk;
            return t != null && t.Chosen != null && Hero.OneToOneConversationHero == t.Giver;
        }

        private bool IsDenMenuTalkOpening()
        {
            var t = _denTalk;
            return t != null && t.FromMenu && !t.Started && Hero.OneToOneConversationHero == t.Giver;
        }

        /// <summary>The quest's giver stands before the player while his quest runs.</summary>
        private bool IsDenQuestGiverTalk()
        {
            try
            {
                var hero = Hero.OneToOneConversationHero;
                var q = ActiveDenQuest();
                if (hero == null || q == null || q.Giver != hero) return false;
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
                _denTalk = new DenTalk(hero, Settlement.CurrentSettlement, fromMenu: false);
        }

        private void AddDenQuestDialogs(CampaignGameStarter starter)
        {
            // Opened from the town menu: his short greeting, above every other "start" line (the
            // forced initiations are 200), so no vanilla greeting and no "Speak freely" sees it.
            starter.AddDialogLine("immersiveai_den_open", "start", "immersiveai_den_options",
                "{=!}" + HandmaidQuestText.GiverGreeting, IsDenMenuTalkOpening,
                () => { if (_denTalk != null) _denTalk.Started = true; }, 210);

            // The same player lines from his ordinary talk (walked up to) and from the menu's talk.
            foreach (var (input, tag, prio) in new[] { ("hero_main_options", "hm", 110), ("immersiveai_den_options", "do", 200) })
            {
                starter.AddPlayerLine("immersiveai_den_offer_" + tag, input, "immersiveai_den_story",
                    "{=!}{" + DenOfferVar + "}", IsDenOfferLine,
                    () => ChooseDen(TownDenCulture(Settlement.CurrentSettlement)), prio);
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
            starter.AddPlayerLine("immersiveai_den_leave", "immersiveai_den_options", "close_window",
                "{=!}" + HandmaidQuestText.PlayerLeave, null, RequestLeaveFromPartyEncounter, 100);

            // His story, then his ask, then the player's answer.
            starter.AddDialogLine("immersiveai_den_story_line", "immersiveai_den_story", "immersiveai_den_ask",
                "{=!}{" + DenStoryVar + "}", IsDenStoryTalk, null, 200);
            starter.AddDialogLine("immersiveai_den_ask_line", "immersiveai_den_ask", "immersiveai_den_answer",
                "{=!}" + HandmaidQuestText.GiverAsk, IsDenStoryTalk, null, 200);
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
                var town = t.Town ?? t.Giver.CurrentSettlement;
                var culture = t.Chosen;
                var target = NearestActiveDen(town, culture, null);
                int reward = HandmaidQuestText.RewardFor(Clan.PlayerClan?.Tier ?? 0);
                var id = "immersiveai_den_quest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var quest = new ImmersiveDenQuest(id, t.Giver, town!, culture, reward);
                quest.Begin(target);
                ModLog.Info($"den quest: {t.Giver.StringId} of {town?.StringId} asks for the {culture} den; target {target?.StringId ?? "(none yet)"}, reward {reward}.");
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
                var next = NearestActiveDen(q.Town, q.DenCulture, hideout);
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
                var next = NearestActiveDen(quest.Town, quest.DenCulture, null);
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
                var next = NearestActiveDen(quest.Town, quest.DenCulture, target);
                quest.Retarget(next, wasWaiting: false);
                ModLog.Info($"den quest: {target.StringId} stood empty; next {next?.StringId ?? "(none, waiting)"}.");
            }
        }
    }
}
