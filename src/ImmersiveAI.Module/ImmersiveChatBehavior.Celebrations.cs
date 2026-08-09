using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Celebrations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImmersiveAI
{
    /// <summary>
    /// WHO STANDS THERE — the one gathering shared by every day of the player's the mod writes
    /// down (2026.08.10). The wedding wrote this first and kept it to itself; the birth needed the
    /// same hall filled the same way, so it moved here whole rather than being copied, and the
    /// next feast we ever throw inherits it for free.
    ///
    /// THE ORDER IS THE MEANING, and it changed with the move (Anton's ask, reading his own
    /// wedding back): the coin buys the people who MATTER, so the souls a courier was sent to are
    /// taken before the souls who merely live in the town. Under the old order a crowded market
    /// day could eat the cap with millers before the friend who rode a week to be there was ever
    /// reached. Your own company is always first and is never sorted away — they were
    /// unquestionably present, whatever the day cost.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        /// <summary>How far a rider may be from the place and still be called to it, in the map's
        /// own units. Beyond that the news simply would not reach them in time.</summary>
        private const float CalledFromWithin = 120f;

        /// <summary>
        /// Everyone who truly stands at this day, in the order the rules mean them to be taken.
        /// Returns heroes; each feature wraps them in its own record type.
        /// </summary>
        /// <param name="settlement">Where it happens, or null for the open road.</param>
        /// <param name="rules">Which of them are called — see <see cref="GuestRules"/>.</param>
        /// <param name="excluded">The souls at the middle of the day, who are not witnesses to it.</param>
        /// <param name="otherHouse">The house this day joins us to, if any, whose lords are called
        /// once the tier reaches that far.</param>
        private List<Hero> GatherCelebrationWitnesses(Settlement? settlement, GuestRules rules,
            IEnumerable<Hero?> excluded, Clan? otherHouse = null)
        {
            var standing = new List<Hero>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var player = Hero.MainHero;
            int cap = Math.Max(0, rules?.Cap ?? 0);

            try
            {
                foreach (var hero in excluded ?? Enumerable.Empty<Hero?>())
                    if (hero != null) seen.Add(hero.StringId);
            }
            catch { }
            try { if (player != null) seen.Add(player.StringId); } catch { }

            bool Eligible(Hero? hero)
            {
                try
                {
                    return hero != null && hero.IsAlive && !hero.IsPrisoner && !hero.IsChild
                        && !seen.Contains(hero.StringId);
                }
                catch { return false; }
            }

            void Take(Hero? hero)
            {
                try
                {
                    if (hero == null || standing.Count >= cap || !seen.Add(hero.StringId)) return;
                    standing.Add(hero);
                }
                catch { /* one soul missed is not the day lost */ }
            }

            bool Room() => standing.Count < cap;

            // 1. The company that rides with them — never in doubt, never sorted away.
            try
            {
                var party = MobileParty.MainParty;
                if (party != null)
                    foreach (var member in party.MemberRoster.GetTroopRoster().ToList())
                    {
                        var hero = member.Character != null && member.Character.IsHero
                            ? member.Character.HeroObject : null;
                        if (Eligible(hero)) Take(hero);
                    }
            }
            catch { }

            // 2. BOTH houses. Family is not a guest-list decision: once a courier rides at all, one
            //    of them rides to your own brother and one rides to her mother (Anton, 2026.08.10).
            //    Kin are taken before the couriers to friends, because at a wedding or a cradle the
            //    two families are the front row and a crowded list must not push them out of it.
            try
            {
                if (rules != null && rules.Kin && Room())
                {
                    var kin = new List<Hero>();
                    foreach (var clan in new[] { Clan.PlayerClan, otherHouse })
                    {
                        if (clan == null) continue;
                        foreach (var hero in clan.Heroes.ToList())
                            if (Eligible(hero)) kin.Add(hero);
                    }
                    foreach (var hero in kin.OrderByDescending(SharedStoryWeight)) Take(hero);
                }
            }
            catch { }

            // 3. THE COURIERS: the souls the player truly shares a story with, wherever they stand.
            //    Richest story first, so the coin buys the people who actually matter — and taken
            //    BEFORE the townsfolk, so a crowded place can never crowd them out.
            try
            {
                if (rules != null && rules.RememberedBonds && Room())
                {
                    var invited = MemoryIndex.All(NpcPaths.CampaignRoot, NpcPaths.MemoryFileName, _memoryStore)
                        .Where(k => k != null && k.Richness > 0)
                        .OrderByDescending(k => k.Richness)
                        .Select(k => FindAliveHero(k.NpcId))
                        .Where(h => Eligible(h))
                        .ToList();
                    foreach (var hero in invited) Take(hero);
                }
            }
            catch { }

            // 4. And the souls of the place — but ONLY where the day called nobody, since a guest
            //    list is a thing that excludes. Richest shared story first even so, and the keep's
            //    closed doors keep their lords out of the hall as everywhere else in the mod.
            try
            {
                if (rules != null && rules.LocalsPresent && settlement != null && Room())
                {
                    var locals = new List<Hero>();
                    foreach (var hero in settlement.HeroesWithoutParty.ToList())
                        if (Eligible(hero) && !IsBehindClosedDoors(hero, settlement)) locals.Add(hero);
                    foreach (var party in settlement.Parties.ToList())
                    {
                        var leader = party?.LeaderHero;
                        if (Eligible(leader) && !IsBehindClosedDoors(leader!, settlement)) locals.Add(leader!);
                    }

                    foreach (var hero in locals
                        .OrderByDescending(SharedStoryWeight)
                        .ThenBy(h => h.Name?.ToString(), StringComparer.OrdinalIgnoreCase))
                        Take(hero);
                }
            }
            catch { }

            // 5. The lords of the country round about, and of the house this day joins us to.
            try
            {
                if (rules != null && rules.LordsOfTheRealm && Room())
                {
                    var kin = new List<Hero>();
                    foreach (var clan in new[] { otherHouse, Clan.PlayerClan })
                        if (clan != null)
                            foreach (var hero in clan.Heroes.ToList())
                                if (Eligible(hero)) kin.Add(hero);
                    foreach (var hero in kin.OrderByDescending(SharedStoryWeight)) Take(hero);

                    if (Room() && settlement != null)
                    {
                        var near = Clan.All.SelectMany(c => (IEnumerable<Hero>?)c?.AliveLords ?? Enumerable.Empty<Hero>())
                            .Where(h => Eligible(h))
                            .Select(h => new { Hero = h, Distance = DistanceToPlace(h, settlement) })
                            .Where(x => x.Distance < CalledFromWithin)
                            .OrderBy(x => x.Distance)
                            .Select(x => x.Hero)
                            .ToList();
                        foreach (var hero in near) Take(hero);
                    }
                }
            }
            catch { }

            // 6. And the great names: those who rule, and those who head a house.
            try
            {
                if (rules != null && rules.GreatNames && Room())
                {
                    var great = Kingdom.All.Select(k => k?.Leader).Where(h => Eligible(h)).ToList();
                    great.AddRange(Clan.All.Select(c => c?.Leader).Where(h => Eligible(h))!);
                    foreach (var hero in great.OrderByDescending(h => Safe(() => h!.Clan?.Renown ?? 0f, 0f)))
                        Take(hero);
                }
            }
            catch { }

            return standing;
        }

        /// <summary>
        /// THE HOUSE THIS SOUL CAME FROM, which after a wedding is emphatically not the house they
        /// belong to (2026.08.10 review — and it silently defeated the whole "both families are
        /// called" rule the day it shipped). Vanilla's `DefaultMarriageModel.GetClanAfterMarriage`
        /// returns the PLAYER'S clan whenever either party is the player, so a wife's `Clan` reads
        /// as `Clan.PlayerClan` ever after: passing it as the other house simply named our own
        /// house twice and no kin of hers were ever sent for.
        ///
        /// So: their current house when it is truly another one — which is the case at a WEDDING,
        /// where the chronicle hook fires before the clan change — and otherwise their blood, which
        /// is the case at every cradle months later. Null when nothing of theirs differs from ours,
        /// which is the honest answer for a wanderer with no house at all.
        /// </summary>
        private static Clan? OtherHouseOf(Hero? hero)
        {
            try
            {
                if (hero == null) return null;
                var mine = Clan.PlayerClan;

                var now = hero.Clan;
                if (now != null && now != mine) return now;

                foreach (var kin in new[] { hero.Father, hero.Mother })
                {
                    var clan = kin?.Clan;
                    if (clan != null && clan != mine) return clan;
                }
                foreach (var sibling in (IEnumerable<Hero>?)hero.Siblings ?? Enumerable.Empty<Hero>())
                {
                    var clan = sibling?.Clan;
                    if (clan != null && clan != mine) return clan;
                }
                return null;
            }
            catch { return null; }
        }

        // How far a soul stands from the place, in the map's own units; huge when it cannot be told.
        private static float DistanceToPlace(Hero hero, Settlement settlement)
        {
            try
            {
                var where = hero.CurrentSettlement?.Position ?? hero.PartyBelongedTo?.Position;
                if (where == null) return float.MaxValue;
                return settlement.Position.Distance(where.Value);
            }
            catch { return float.MaxValue; }
        }

        // How much story the player truly shares with this soul — the cheap cached read the hourly
        // rolls use, so ordering a crowded hall costs nothing.
        private int SharedStoryWeight(Hero hero)
        {
            try
            {
                var known = MemoryIndex.Get(NpcPaths.MemoryFile(hero), _memoryStore);
                return known?.Richness ?? 0;
            }
            catch { return 0; }
        }

        // A word about who this soul was on that day, so the chronicler can name them as a person
        // rather than as a list.
        private string WitnessDetail(Hero hero)
        {
            try
            {
                if (hero.PartyBelongedTo == MobileParty.MainParty)
                {
                    var duty = Safe(() => PartyDutyWord(hero), string.Empty);
                    if (!string.IsNullOrWhiteSpace(duty)) return $"who rides with them as their {duty}";
                    return hero.CompanionOf == Clan.PlayerClan ? "who rides with them as their companion" : "of their own company";
                }
                if (hero.Clan == Clan.PlayerClan) return "of their own house";
                if (hero.IsNotable) return Safe(() => $"a notable of {hero.CurrentSettlement?.Name}", "a notable of the place");
                if (hero.IsLord) return Safe(() => hero.Clan == null ? "a lord" : $"of the house of {hero.Clan.Name}", "a lord");
                return "of the place";
            }
            catch { return string.Empty; }
        }

        // The duty they hold in the player's party, in a plain word ("scout", "surgeon"), or empty.
        private static string PartyDutyWord(Hero hero)
        {
            try
            {
                var party = MobileParty.MainParty;
                if (party == null) return string.Empty;
                if (party.EffectiveScout == hero) return "scout";
                if (party.EffectiveSurgeon == hero) return "surgeon";
                if (party.EffectiveQuartermaster == hero) return "quartermaster";
                if (party.EffectiveEngineer == hero) return "engineer";
                return string.Empty;
            }
            catch { return string.Empty; }
        }
    }
}
