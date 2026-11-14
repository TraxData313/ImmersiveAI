using System;
using Helpers;
using ImmersiveAI.Core.Handmaids;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// "Clear the {den type} den" (2026.11.14, the owner's ask): the town quest that leads to the
    /// captive in the den. Its shape is vanilla's NearbyBanditBaseIssueQuest (the target hideout
    /// spotted, made visible, tracked and pointed at with a map arrow; thanks sent as word with the
    /// gold) on the frame of a quest that stands without an issue, as War Sails' InquireAtOstican
    /// does: a fixed due time of Never and a SpecialQuestType, without which QuestManager.OnGameLoaded
    /// cancels any quest that no issue owns.
    ///
    /// QuestGiver is left null ON PURPOSE. A vanilla quest giver cannot get a new issue while the
    /// quest runs and, as a notable, cannot die; with no time limit on this quest that would freeze
    /// him for as long as the player likes. The notable is kept in our own saved field instead.
    ///
    /// The behavior drives it (the Handmaid quest partial): this class holds the saved facts and
    /// the quest-log steps, and never throws past its own walls.
    ///
    /// Saved as type id 4 in <see cref="ImmersiveAISaveDefiner"/>: never renumber it.
    /// </summary>
    public class ImmersiveDenQuest : QuestBase
    {
        public const string SpecialType = "ImmersiveAI_DenQuest";

        [SaveableField(1)]
        private string _denCulture = string.Empty;

        [SaveableField(2)]
        private Settlement? _town;

        [SaveableField(3)]
        private Settlement? _target;

        [SaveableField(4)]
        private Hero? _giver;

        public ImmersiveDenQuest(string questId, Hero giver, Settlement town, string denCulture, int rewardGold)
            : base(questId, null, CampaignTime.Never, rewardGold)
        {
            _giver = giver;
            _town = town;
            _denCulture = denCulture ?? string.Empty;
        }

        public string DenCulture => _denCulture ?? string.Empty;
        public Settlement? Town => _town;
        public Settlement? Target => _target;
        public Hero? Giver => _giver;

        public string DenName => ImmersiveChatBehavior.DenNameOf(DenCulture);

        public override TextObject Title => new TextObject("{=!}" + HandmaidQuestText.Title(DenName));

        public override bool IsRemainingTimeHidden => true;

        public override string SpecialQuestType => SpecialType;

        protected override void SetDialogs() { }

        protected override void InitializeQuestOnGameLoad() { }

        // Never holds the giver (or anyone) alive: see the class note.
        public override void OnHeroCanDieInfoIsRequested(Hero hero, KillCharacterAction.KillCharacterActionDetail causeOfDeath, ref bool result) { }

        // The quest manager ticks every quest daily: a quest still waiting for a den looks again.
        protected override void DailyTick()
        {
            try { ImmersiveChatBehavior.DenQuestDailyCheck(this); }
            catch (Exception ex) { ModLog.Error("den quest: the daily look for a den", ex); }
        }

        private static string NameOf(Hero? hero) => hero?.Name?.ToString() ?? string.Empty;

        private static string LinkOf(Hero? hero)
        {
            try { return hero?.EncyclopediaLinkWithName?.ToString() ?? NameOf(hero); }
            catch { return NameOf(hero); }
        }

        private static string LinkOf(Settlement? s)
        {
            try { return s?.EncyclopediaLinkWithName?.ToString() ?? s?.Name?.ToString() ?? string.Empty; }
            catch { return s?.Name?.ToString() ?? string.Empty; }
        }

        private void Log(string text)
        {
            try { AddLog(new TextObject("{=!}" + text)); }
            catch (Exception ex) { ModLog.Error("den quest: writing the log", ex); }
        }

        internal bool GiverStillHere => _giver != null && _giver.IsAlive;

        /// <summary>Starts the quest: the log, the den revealed, a word from the giver.</summary>
        internal void Begin(Settlement? target)
        {
            StartQuest();
            Log(HandmaidQuestText.StartLog(DenName, LinkOf(_giver), LinkOf(_town)));
            if (target != null) Mark(target, arrowFromTown: true);
            else Log(HandmaidQuestText.WaitLog(DenName));
            try { MBInformationManager.AddQuickInformation(new TextObject("{=!}" + HandmaidQuestText.MarkedNotice(DenName, NameOf(_giver)))); }
            catch { }
        }

        // Vanilla's reveal (NearbyBanditBaseIssueQuest.OnQuestAccepted, as of the 2026.11.11 game DLL).
        private void Mark(Settlement target, bool arrowFromTown)
        {
            _target = target;
            try
            {
                // This game version has no separate "spotted" flag on a hideout: visible is spotted.
                target.IsVisible = true;
                if (!IsTracked(target)) AddTrackedObject(target);
                if (arrowFromTown && _town != null)
                    QuestHelper.AddMapArrowFromPointToTarget(new TextObject("{=!}" + DenName),
                        _town.Position, target.Position, 5f, 0.1f);
            }
            catch (Exception ex) { ModLog.Error("den quest: marking the den", ex); }
        }

        private void Unmark()
        {
            var old = _target;
            _target = null;
            try { if (old != null && IsTracked(old)) RemoveTrackedObject(old); }
            catch (Exception ex) { ModLog.Error("den quest: unmarking the old den", ex); }
        }

        /// <summary>The marked den went quiet without the player's win: the next one, or the wait.</summary>
        internal void Retarget(Settlement? next, bool wasWaiting)
        {
            Unmark();
            if (next != null)
            {
                Mark(next, arrowFromTown: false);
                Log(wasWaiting ? HandmaidQuestText.FoundAgainLog(DenName) : HandmaidQuestText.RetargetLog(DenName));
            }
            else if (!wasWaiting)
            {
                Log(HandmaidQuestText.WaitLog(DenName));
            }
        }

        /// <summary>The den is cleared: his word (when he still lives), the gold, his regard.</summary>
        internal void Succeed()
        {
            bool here = GiverStillHere;
            Log(HandmaidQuestText.SuccessLog(DenName, LinkOf(_giver), here));
            try { if (RewardGold > 0 && Hero.MainHero != null) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, RewardGold); }
            catch (Exception ex) { ModLog.Error("den quest: the reward", ex); }
            try { if (here) ChangeRelationAction.ApplyPlayerRelation(_giver, HandmaidQuestText.RelationOnSuccess); }
            catch (Exception ex) { ModLog.Error("den quest: the giver's regard", ex); }
            CompleteQuestWithSuccess();
        }

        /// <summary>Given back: no cost, and any town may offer it again.</summary>
        internal void Abandon()
        {
            CompleteQuestWithCancel(new TextObject("{=!}" + HandmaidQuestText.AbandonLog(DenName, LinkOf(_giver), GiverStillHere)));
        }
    }
}
