using System;
using System.Linq;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// MORE THAN ONE WIFE — THE WEDDING STEP (2026.11.11, the owner's call; see <see cref="Household"/>
    /// for the shape and the cascade law, and Core <see cref="SecondWifeLaw"/> for the gates).
    ///
    /// Vanilla's <c>MarriageAction.Apply</c> refuses a man with a wife, and even if it did not, its
    /// first line (<c>firstHero.Spouse = secondHero</c>) would push the first wife into ExSpouses
    /// through the setter's cascade. So a FURTHER wedding is performed here, step for step in
    /// vanilla's own order, with the one write vanilla does through the setter done by field:
    /// <list type="number">
    /// <item>the bond: her slot points at him; his slot keeps the first wife;</item>
    /// <item>the relation gain, by the model's own figure;</item>
    /// <item><c>BeforeHeroesMarried</c>, with his slot LENT to the bride for that instant — every
    /// listener runs: our wedding chronicle (captures the day before the clan change), the log entry
    /// and the map notice, SetHasMet, the quick notice, the achievements, and the wedding scene,
    /// which reads <c>groom.Spouse</c> and so shows the right bride;</item>
    /// <item>the clan move by vanilla's own private rule (reflected; our own mirror of it if a game
    /// update hides it);</item>
    /// <item>her other courtships ended, his ended EXCEPT the marriages he already holds (vanilla's
    /// EndAllCourtships would mark his first marriage's romance "Ended");</item>
    /// <item>the romance set to Marriage.</item>
    /// </list>
    /// Everything after it — the companion's graduation, the stage, the beats, renown — is the seal's,
    /// shared with a first wedding.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        /// <summary>The one door every wedding of ours goes through. A first wedding is vanilla's own
        /// action, untouched; a further one is ours, and only while the setting allows it.</summary>
        private void PerformWedding(Hero player, Hero bride)
        {
            if (player == null || bride == null) return;
            bool slotTaken = player.Spouse != null;
            if (!slotTaken && bride.Spouse == null)
            {
                MarriageAction.Apply(player, bride);
                return;
            }
            if (bride.Spouse != null) return;   // her own marriage is never ours to dissolve
            if (!SecondWifeLaw.MayWedNow(PlayerIsWed(player), _config.AllowMoreThanOneWife)) return;
            WedAsFurtherSpouse(player, bride);
        }

        /// <summary>The further wedding itself. True when she is wed when it is over. Every step is
        /// wrapped on its own: once the bond is written she IS wed, and a later step failing costs
        /// only that step (logged), never the marriage or the first wife's.</summary>
        private bool WedAsFurtherSpouse(Hero holder, Hero bride, bool showNotification = true)
        {
            if (holder == null || bride == null || holder == bride) return false;
            if (!Household.CanWriteSlots)
            {
                ModLog.Warn("household: the game's spouse fields were not found; no further wedding is performed.");
                return false;
            }
            if (holder.Spouse == null || bride.Spouse != null || !holder.IsAlive || !bride.IsAlive) return false;

            var model = Campaign.Current.Models.MarriageModel;

            // 1. The bond.
            Household.BindFurther(holder, bride);
            ModLog.Info($"household: {bride.Name} is wed to {holder.Name} as a further spouse; {holder.Spouse?.Name} keeps the slot.");

            // 2. The relation, as vanilla gives it.
            try
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(holder, bride,
                    model.GetEffectiveRelationIncrease(holder, bride), showQuickNotification: false);
            }
            catch (Exception ex) { ModLog.Error("household: the wedding's relation", ex); }

            // 3. Who moves, by vanilla's own rule — the one whose clan is kept comes first.
            Clan? clanAfter = null;
            Hero first = holder, second = bride;
            try
            {
                clanAfter = model.GetClanAfterMarriage(holder, bride);
                if (clanAfter != first.Clan) { first = bride; second = holder; }
            }
            catch (Exception ex) { ModLog.Error("household: the clan after the wedding", ex); }

            // 4. The world is told — with his slot lent to her for that instant only.
            try
            {
                var f = first; var s = second;
                Household.WithSlotLent(holder, bride, () =>
                    CampaignEventDispatcher.Instance?.OnBeforeHeroesMarried(f, s, showNotification));
            }
            catch (Exception ex) { ModLog.Error("household: the wedding's tidings", ex); }

            // 5. The clan move.
            try
            {
                if (clanAfter != null)
                {
                    if (first.Clan != clanAfter) MoveToClanAfterMarriage(first, clanAfter);
                    if (second.Clan != clanAfter) MoveToClanAfterMarriage(second, clanAfter);
                }
            }
            catch (Exception ex) { ModLog.Error("household: the clan move", ex); }

            // 6. Courtships end; his marriages do not.
            try
            {
                EndCourtshipsKeepingMarriages(first);
                EndCourtshipsKeepingMarriages(second);
                ChangeRomanticStateAction.Apply(first, second, Romance.RomanceLevelEnum.Marriage);
            }
            catch (Exception ex) { ModLog.Error("household: the romance", ex); }

            return bride.Spouse == holder;
        }

        private static readonly System.Reflection.MethodInfo? VanillaClanMove =
            typeof(MarriageAction).GetMethod("HandleClanChangeAfterMarriageForHero",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        private void MoveToClanAfterMarriage(Hero hero, Clan clan)
        {
            if (VanillaClanMove != null)
            {
                VanillaClanMove.Invoke(null, new object[] { hero, clan });
                return;
            }
            // A game update hid vanilla's own: our step-for-step mirror of it (the lover's road's).
            if (clan == Clan.PlayerClan) LeaveHerHouse(hero);
            else hero.Clan = clan;
        }

        // Vanilla's EndAllCourtships, minus the marriages: it marks EVERY romantic state of the
        // hero Ended, the first wife's Marriage included, which no further wedding may do.
        private static void EndCourtshipsKeepingMarriages(Hero hero)
        {
            foreach (var state in Romance.RomanticStateList.ToList())
            {
                if (state == null || (state.Person1 != hero && state.Person2 != hero)) continue;
                if (state.Level == Romance.RomanceLevelEnum.Marriage) continue;
                state.Level = Romance.RomanceLevelEnum.Ended;
            }
        }

        // ------------------------------ the household is told ------------------------------

        /// <summary>
        /// The day he weds again, every other woman of his hearth learns it as a plain fact in her own
        /// first person (Core <see cref="SecondWifeText.FurtherWeddingBeat"/>). Silent, no LLM call,
        /// no wound marked, no door touched: what she makes of it is hers. A first wedding has no
        /// one to tell and writes nothing.
        /// </summary>
        private void TellTheHouseholdOfTheWedding(Hero newSpouse)
        {
            try
            {
                var player = Hero.MainHero;
                if (player == null || newSpouse == null) return;
                // A first wedding stays exactly what it was: only a FURTHER one is told this way.
                if (FamilyBuilder.SpousesOf(player).Count(s => s != newSpouse) == 0) return;
                var others = WomenOfTheHearth().Where(h => h != null && h != newSpouse && h != player).ToList();
                if (others.Count == 0) return;
                var beat = SecondWifeText.FurtherWeddingBeat(PlayerName(),
                    Safe(() => newSpouse.Name?.ToString() ?? string.Empty, string.Empty), newSpouse.IsFemale);
                foreach (var other in others)
                {
                    WriteNightBeat(other, beat, OutreachMark.None);
                    UI.TalkUI.OnThreadChanged(other, markUnread: true);
                }
            }
            catch (Exception ex) { ModLog.Error("telling the household of the wedding", ex); }
        }

        // ------------------------------ the deaths ------------------------------

        private void OnHeroKilledForHousehold(Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            Household.OnHeroKilled(victim);
        }

        // ------------------------------ the dev lever ------------------------------

        /// <summary>
        /// Dev: "[Immersive AI • test] Wed them now as a further wife" — the REAL further wedding on
        /// the soul spoken with, skipping the road (no courtship, no blessing, no purse) but
        /// respecting the setting and the world's own law, so the owner can test the household
        /// without courting for hours. Everything after the wedding runs as at a real seal.
        /// </summary>
        internal void WedAsFurtherWifeNow(Hero npc)
        {
            try
            {
                var player = Hero.MainHero;
                if (npc == null || player == null) return;
                void Say(string line) => InformationManager.DisplayMessage(new InformationMessage(line, SealGrey));

                if (!_config.AllowMoreThanOneWife) { Say($"\"{SecondWifeText.SettingName}\" is off in the mod's settings."); return; }
                if (!PlayerIsWed(player) || player.Spouse == null) { Say("You have no wife yet — this lever weds a further one."); return; }
                if (FamilyBuilder.AreWed(player, npc)) { Say($"{npc.Name} is already wed to you."); return; }
                if (npc.Spouse != null) { Say($"{npc.Name} is wed to someone else."); return; }
                if (npc.IsFemale == player.IsFemale) { Say("The world does not seal such a match."); return; }

                bool graduated = false;
                if (npc.IsWanderer)
                {
                    if (npc.CompanionOf != Clan.PlayerClan) { Say("They must first be taken into your service."); return; }
                    npc.SetNewOccupation(Occupation.Lord);
                    graduated = true;
                }
                if (!Household.LawAllows(player, npc))
                {
                    if (graduated) npc.SetNewOccupation(Occupation.Wanderer);
                    Say("The world refuses the match as things stand (kinship, clans, age, or a battle).");
                    return;
                }

                if (!WedAsFurtherSpouse(player, npc))
                {
                    if (graduated && npc.Spouse != player) npc.SetNewOccupation(Occupation.Wanderer);
                    Say("The further wedding did not land — see log.txt.");
                    return;
                }

                var playerName = PlayerName();
                var memory = LoadMemory(npc);
                memory.CourtshipStage = CourtshipStage.Wed;
                memory.CourtshipSeeded = true;
                memory.LoverBond = SecondWifeLaw.BondAfterWedding(memory.LoverBond);
                AddSilentInnerBeat(memory, npc, CourtshipText.WeddingSealedBeat(playerName));
                NoteRoad(npc, memory, RoadNotes.KindSealed, $"[test] This day you and {npc.Name} are wed.");
                SaveMemory(npc, memory);
                TellTheHouseholdOfTheWedding(npc);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[test] {npc.Name} is wed to you as a further wife.", SealGreen));
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("dev: a further wedding", ex); }
        }

        internal static void DevWedAsFurtherWife(Hero npc)
        {
            try { if (npc != null) Current?.WedAsFurtherWifeNow(npc); }
            catch (Exception ex) { ModLog.Error("dev: a further wedding", ex); }
        }
    }
}
