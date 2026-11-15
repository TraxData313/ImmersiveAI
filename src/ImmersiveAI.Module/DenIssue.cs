using System;
using ImmersiveAI.Core.Handmaids;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// "Clear the {den type} den" as a VILLAGE ISSUE (2026.11.15, the owner: "a normal quest a village
    /// notable gives, not in every village, not more frequent than the other quests"). The game's own
    /// issue manager decides which notables carry it, at the Common frequency (vanilla's own bandit-base
    /// issue is VeryCommon); the game draws the blue mark over them, lists our title in the issue
    /// preview, and runs the issue talk: his story, the player's question, his ask, the player's yes,
    /// then the quest's offer flow (<see cref="ImmersiveDenQuest"/>, issue constructor).
    ///
    /// Shape after vanilla's NearbyBanditBaseIssue (as decompiled from the 1.5.7 game DLL), with no
    /// companion or lord solution: the captive is found by the player's own hand.
    ///
    /// Saved as type id 5 in <see cref="ImmersiveAISaveDefiner"/>: never renumber it.
    /// </summary>
    public class ImmersiveDenIssue : IssueBase
    {
        [SaveableField(1)]
        private string _denCulture = string.Empty;

        [SaveableField(2)]
        private Settlement? _village;

        public ImmersiveDenIssue(Hero issueOwner, string denCulture)
            : base(issueOwner, CampaignTime.DaysFromNow(HandmaidQuestText.IssueDays))
        {
            _denCulture = denCulture ?? string.Empty;
        }

        public string DenCulture => _denCulture ?? string.Empty;

        private string DenName => ImmersiveChatBehavior.DenNameOf(DenCulture);

        private Settlement? Village => _village ?? IssueOwner?.CurrentSettlement;

        protected override void AfterIssueCreation() => _village = IssueOwner?.CurrentSettlement;

        private static TextObject T(string s) => new TextObject("{=!}" + s);

        public override TextObject Title => T(HandmaidQuestText.Title(DenName));

        public override TextObject Description =>
            T(HandmaidQuestText.Description(DenName, IssueOwner?.Name?.ToString(), Village?.Name?.ToString()));

        public override TextObject IssueBriefByIssueGiver => T(HandmaidQuestText.StoryFor(DenCulture));

        public override TextObject IssueAcceptByPlayer => T(HandmaidQuestText.IssueAccept(DenName));

        public override TextObject IssueQuestSolutionExplanationByIssueGiver
        {
            get
            {
                int men = 0;
                try { men = (MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1) - 1; } catch { }
                return T(HandmaidQuestText.GiverAsk(men));
            }
        }

        public override TextObject IssueQuestSolutionAcceptByPlayer => T(HandmaidQuestText.PlayerAccept);

        public override TextObject IssueAsRumorInSettlement => T(HandmaidQuestText.Rumor(DenName, IssueOwner?.Name?.ToString()));

        public override bool IsThereAlternativeSolution => false;

        public override bool IsThereLordSolution => false;

        protected override bool IssueQuestCanBeDuplicated => false;

        public override IssueFrequency GetFrequency() => IssueFrequency.Common;

        // The captive is the player's to find: no lord ever solves it in his stead.
        public override bool CanBeCompletedByAI() => false;

        protected override void OnGameLoad() { }

        protected override void HourlyTick() { }

        protected override QuestBase GenerateIssueQuest(string questId) =>
            new ImmersiveDenQuest(questId, IssueOwner, Village!, DenCulture, CampaignTime.DaysFromNow(HandmaidQuestText.QuestDays));

        protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flags,
            out Hero relationHero, out SkillObject skill, out int requiredGold)
        {
            flags = PreconditionFlags.None;
            relationHero = null!;
            skill = null!;
            requiredGold = 0;
            if (issueGiver.GetRelationWithPlayer() < -10f)
            {
                flags |= PreconditionFlags.Relation;
                relationHero = issueGiver;
            }
            if (FactionManager.IsAtWarAgainstFaction(issueGiver.MapFaction, Hero.MainHero.MapFaction))
                flags |= PreconditionFlags.AtWar;
            return flags == PreconditionFlags.None;
        }

        /// <summary>It stands while the captive can still be met and a den of its people is held.</summary>
        public override bool IssueStayAliveConditions()
        {
            try { return ImmersiveChatBehavior.DenIssueMayStand(this, Village); }
            catch (Exception ex) { ModLog.Error("den issue: stay-alive check", ex); return false; }
        }

        protected override void CompleteIssueWithTimedOutConsequences() { }
    }
}
