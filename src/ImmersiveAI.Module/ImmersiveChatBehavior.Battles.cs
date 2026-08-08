using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Battles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// The battle chronicle — where the heart of the mod meets the heart of the game. Every battle
    /// the player fights is set down as a <see cref="BattleRecord"/> in the campaign's _battles
    /// folder (JSON per battle + a running chronicle.txt), and every allied hero who stood in it
    /// gets a short first-person beat in their own memory — their hand's work beside the player's,
    /// how they came out of it, and the name the chronicle keeps — so the war they share becomes
    /// something they can truly talk about. The freshest shared battle rides each soul's situation
    /// in full; the older ones live as titles, recallable whole through the recall_battle tool.
    ///
    /// The numbers are the game's own, captured at the two honest moments: OnPlayerBattleEnd fires
    /// BEFORE the loot is distributed and the gains committed (renown/influence/plunder still
    /// readable on the main party's MapEventParty, the defeated side's prison roster still countable
    /// as "souls we freed"), and one dispatcher tick later the loot rosters stand filled on
    /// PlayerEncounter — so the record is written at the field's edge and enriched with the spoils
    /// a breath after. Per-hero downs come from two places, each honest where it stands: on the
    /// field from <see cref="Battles.BattleDownsMissionBehavior"/> (the agent-removal event, one
    /// mark per soul who falls), and in a simulated press from OnHeroCombatHitEvent — whose isFatal
    /// flag is trustworthy ONLY there. Everything here is best-effort: a failed chronicle must
    /// never break a battle.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        // The campaign's book of battles. Loaded once the campaign id is resolved (OnSessionLaunched,
        // beside the letter bag); null until then — no chronicle outside a campaign.
        private BattleLedger? _battleLedger;

        // Foes struck down by each allied hero's own hand in the CURRENT player battle, by StringId.
        // Cleared when a player battle begins and consumed when its record is written.
        private readonly Dictionary<string, int> _battleDowns = new Dictionary<string, int>(StringComparer.Ordinal);

        // The one battle already recorded (dedupe: OnPlayerBattleEnd is the rich path, MapEventEnded
        // the fallback — whichever fires first writes, the other must stand down).
        private MapEvent? _battleRecorded;

        // Renown/influence/purse snapshot taken when the player's battle began, for the honest
        // fallback when the per-party values cannot be read (joined battles, odd paths).
        private MapEvent? _battleSnapshotEvent;
        private float _battleStartRenown, _battleStartInfluence;
        private int _battleStartGold;

        private void LoadBattleLedger()
        {
            try { _battleLedger = BattleLedger.LoadFrom(NpcPaths.BattlesFolder); }
            catch { _battleLedger = null; }
        }

        // ------------------------------ the campaign hooks ------------------------------

        private void OnMapEventStartedForChronicle(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            try
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
                _battleDowns.Clear();
                _battleSnapshotEvent = mapEvent;
                _battleStartRenown = Safe(() => Clan.PlayerClan?.Renown ?? 0f, 0f);
                _battleStartInfluence = Safe(() => Clan.PlayerClan?.Influence ?? 0f, 0f);
                _battleStartGold = Safe(() => Hero.MainHero?.Gold ?? 0, 0);
            }
            catch { /* the chronicle must never touch the battle itself */ }
        }

        // A blow struck on the field, marked by the hand that landed it — handed in by
        // BattleDownsMissionBehavior, which counts souls as they FALL and so counts them once.
        internal void TallyBattleDownOnTheField(string heroId)
        {
            try
            {
                if (string.IsNullOrEmpty(heroId)) return;
                _battleDowns.TryGetValue(heroId, out int downs);
                _battleDowns[heroId] = downs + 1;
            }
            catch { /* a lost tally mark is nothing; the battle goes on */ }
        }

        // The game scores every downing blow a hero strikes — in fought missions and simulated
        // presses alike — through this one event. We keep the tally only for the player's battle.
        private void OnHeroCombatHitForChronicle(CharacterObject attacker, CharacterObject attacked,
            PartyBase party, WeaponComponentData weapon, bool isFatal, int xp)
        {
            try
            {
                // On the field itself this event lies: its isFatal is the victim's health AFTER the
                // blow minus that same blow's damage, so one heavy hit short of killing is counted
                // as a kill (four bandits answering for six downs). Whenever a mission is on stage,
                // BattleDownsMissionBehavior keeps the true tally from the removal event instead.
                // With no mission running the press is simulated, and here the flag is honest.
                if (TaleWorlds.MountAndBlade.Mission.Current != null) return;
                if (!isFatal || attacker == null || !attacker.IsHero) return;
                var mapEvent = Safe(() => party?.MapEvent, (MapEvent?)null);
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
                if (Safe(() => party!.Side != mapEvent.PlayerSide, true)) return;

                var id = attacker.HeroObject?.StringId;
                if (string.IsNullOrEmpty(id)) return;
                _battleDowns.TryGetValue(id!, out int downs);
                _battleDowns[id!] = downs + 1;
            }
            catch { /* a lost tally mark is nothing; the battle goes on */ }
        }

        // The rich path: fired before loot distribution and the committing of gains, while every
        // number still stands readable. Builds and writes the record, then hands the enrichment
        // (spoils, settled fates, the beats) to the next dispatcher tick.
        private void OnPlayerBattleEnded(MapEvent mapEvent)
        {
            try
            {
                if (!_config.EnableBattleChronicle || _battleLedger == null) return;
                if (mapEvent == null || _battleRecorded == mapEvent) return;
                RecordBattle(mapEvent, lootAlreadyDistributed: false);
            }
            catch { /* best-effort, always */ }
        }

        // The fallback: some battle shapes may end without the player-battle event (odd encounter
        // paths). By MapEventEnded the loot has already been distributed, so the freed-captives
        // count is gone — but the rest of the record still stands true.
        private void OnMapEventEndedForChronicle(MapEvent mapEvent)
        {
            try
            {
                if (!_config.EnableBattleChronicle || _battleLedger == null) return;
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent || _battleRecorded == mapEvent) return;
                RecordBattle(mapEvent, lootAlreadyDistributed: true);
            }
            catch { /* best-effort, always */ }
        }

        // ------------------------------ building the record ------------------------------

        private void RecordBattle(MapEvent e, bool lootAlreadyDistributed)
        {
            // Training Battles compatibility (the sibling mod): its drills are REAL map events —
            // the player's own split-off half, a phantom enemy, a siege drill at own walls — and
            // without this guard every sparring would enter the chronicle as a war and every
            // companion would "remember" fighting their own comrades. A drill is not a battle:
            // nothing is chronicled, no beats are set down, and the sparring tally never leaks
            // into the next true fight.
            if (IsTrainingBattle(e))
            {
                _battleRecorded = e;
                _battleDowns.Clear();
                return;
            }

            var ourSide = e.PlayerSide;
            var theirSide = e.GetOtherSide(ourSide);
            var ours = CountSide(e.GetMapEventSide(ourSide));
            var theirs = CountSide(e.GetMapEventSide(theirSide));

            string outcome;
            if (e.WinningSide == ourSide) outcome = BattleRecord.Outcomes.Victory;
            else if (e.WinningSide == theirSide) outcome = BattleRecord.Outcomes.Defeat;
            else outcome = BattleRecord.Outcomes.Draw;

            // A stand-down with no blood on either side is not a battle — nothing to chronicle.
            int blood = ours.Fallen + ours.Wounded + theirs.Fallen + theirs.Wounded;
            if (outcome == BattleRecord.Outcomes.Draw && blood == 0) return;

            var record = new BattleRecord
            {
                Id = _battleLedger!.NextId(CampaignTime.Now.ToDays),
                GameDay = CampaignTime.Now.ToDays,
                DateText = CalradiaDate(),
                TimeOfDay = Personas.SituationBuilder.TimeOfDay(),
                PlaceName = NearestPlaceName(e),
                Kind = KindOf(e),
                PlayerAttacked = ourSide == BattleSideEnum.Attacker,
                Outcome = outcome,
                EnemyName = EnemyNameOf(e, theirSide),
                Ours = ours,
                Theirs = theirs,
            };

            // Souls they held in chains, counted while the defeated still hold them — our victory
            // is what strikes those chains (the distribution a breath later frees or recruits them).
            if (outcome == BattleRecord.Outcomes.Victory && !lootAlreadyDistributed)
                record.CaptivesFreed = Safe(() => e.GetMapEventSide(theirSide).Parties
                    .Sum(p => Safe(() => p.Party?.PrisonRoster?.TotalManCount ?? 0, 0)), 0);

            // The player's own gains, read off the main party's MapEventParty before the game
            // commits them (committing zeroes the plunder and hands the renown on).
            var mainParty = Safe(() => e.GetMapEventSide(ourSide).Parties
                .FirstOrDefault(p => p.Party == PartyBase.MainParty), (MapEventParty?)null);
            if (mainParty != null)
            {
                record.GoldGained = Safe(() => mainParty.PlunderedGold, 0);
                record.RenownGained = (int)Math.Round(Safe(() => mainParty.GainedRenown, 0f));
                record.InfluenceGained = (int)Math.Round(Safe(() => mainParty.GainedInfluence, 0f));
            }

            // Every hero who stood on our side, the player first — with what the tally holds of
            // their hand. If no blow at all reached the tally in a battle with real casualties,
            // the honest answer is "no tally was kept", never a page of zeroes.
            var heroes = HeroesOfSide(e, ourSide);
            int theirLosses = theirs.Fallen + theirs.Wounded;
            int tallied = heroes.Sum(h => { _battleDowns.TryGetValue(h.StringId, out int d); return d; });
            // Belt and braces: no hand can have felled more men than the field gave up. If the
            // tally ever outruns the truth again, say plainly that none was kept — never hand a
            // soul a number that flatters them.
            bool noTally = (_battleDowns.Count == 0 && theirLosses >= 20) || tallied > theirLosses;
            foreach (var hero in heroes)
            {
                _battleDowns.TryGetValue(hero.StringId, out int downs);
                record.Participants.Add(new BattleParticipant
                {
                    HeroId = hero.StringId,
                    Name = hero.Name?.ToString() ?? hero.StringId,
                    IsPlayer = hero == Hero.MainHero,
                    Downs = noTally ? -1 : downs,
                    State = ParticipantStateOf(hero),
                });
            }

            record.Title = BattleText.ForgeTitle(record);
            record.ShortTale = BattleText.ShortTale(record);
            record.FullTale = BattleText.FullAccount(record);
            _battleLedger.Save(record);

            _battleRecorded = e;
            _battleDowns.Clear();

            // A tick later the loot rosters stand filled and every fate is settled (a defeated
            // side's heroes are only taken captive after this event returns) — enrich, write the
            // chronicle line, and set the beats down in every ally's memory.
            var heroById = heroes.ToDictionary(h => h.StringId, h => h);
            MainThreadDispatcher.Enqueue(() => FinalizeBattleRecord(record, heroById, e));
        }

        // The enrichment pass, one dispatcher tick after the battle's end.
        private void FinalizeBattleRecord(BattleRecord record, Dictionary<string, Hero> heroById, MapEvent e)
        {
            try
            {
                // The spoils, as offered to the player (the loot screen's own rosters).
                var enc = PlayerEncounter.Current;
                if (enc != null && record.Outcome == BattleRecord.Outcomes.Victory)
                {
                    record.PrisonersTaken = Safe(() => enc.RosterToReceiveLootPrisoners?.TotalManCount ?? 0, 0);
                    record.Loot = BattleLoot.Summarize(CollectLoot(enc));
                }

                // Fates settle only after the event finishes (capture runs past the end event).
                foreach (var p in record.Participants)
                    if (heroById.TryGetValue(p.HeroId, out var hero))
                        p.State = ParticipantStateOf(hero);

                // The name's gains, by the honest fallback when the party values read empty —
                // the clan's ledgers moved between the battle's first and last breath.
                if (_battleSnapshotEvent == e)
                {
                    if (record.RenownGained == 0)
                        record.RenownGained = Math.Max(0, (int)Math.Round(Safe(() => Clan.PlayerClan?.Renown ?? 0f, 0f) - _battleStartRenown));
                    if (record.InfluenceGained == 0)
                        record.InfluenceGained = Math.Max(0, (int)Math.Round(Safe(() => Clan.PlayerClan?.Influence ?? 0f, 0f) - _battleStartInfluence));
                    if (record.GoldGained == 0)
                        record.GoldGained = Math.Max(0, Safe(() => Hero.MainHero?.Gold ?? 0, 0) - _battleStartGold);
                    _battleSnapshotEvent = null;
                }

                record.ShortTale = BattleText.ShortTale(record);
                record.FullTale = BattleText.FullAccount(record);
                _battleLedger?.Save(record);
                _battleLedger?.AppendToChronicle(record.FullTale);

                RecordBattleBeats(record, heroById);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"The chronicle sets down: '{record.Title}'.", ActivityColor));
            }
            catch { /* the first write already stands; enrichment is a nicety */ }
        }

        // The short first-person beat in every LIVING ally's memory — the player's own memory is
        // the chronicle itself. Fighting side by side is the player engaging, whatever letters
        // waited unanswered before.
        private void RecordBattleBeats(BattleRecord record, Dictionary<string, Hero> heroById)
        {
            var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
            foreach (var participant in record.Participants)
            {
                try
                {
                    if (participant.IsPlayer) continue;
                    if (!heroById.TryGetValue(participant.HeroId, out var hero)) continue;
                    if (hero == null || !hero.IsAlive) continue;

                    AppendRecordedTurn(hero, BattleText.BeatLine(record, participant, playerName),
                        string.Empty, OutreachMark.PlayerEngaged);
                }
                catch { /* one soul's missed beat must not silence the rest */ }
            }
        }

        // Detects a Training Battles drill by the party ids that mod mints for every drill shape
        // (field split, phantom enemy, siege/sea drills): "training_opponents*" and
        // "training_mock_enemy*" — a plain StringId test on both sides, no reference and no
        // reflection, so it costs nothing when that mod is absent (no party ever wears these ids).
        // CONTRACT with the sibling repo: TrainingBattlesMod's OpponentPartyIdPrefix /
        // MockEnemyPartyIdPrefix must never be renamed without updating this check (noted in its
        // CLAUDE.md too).
        private static bool IsTrainingBattle(MapEvent e)
        {
            try
            {
                foreach (var side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
                {
                    var parties = Safe(() => e.GetMapEventSide(side)?.Parties, (MBReadOnlyList<MapEventParty>?)null);
                    if (parties == null) continue;
                    foreach (var party in parties)
                    {
                        var id = Safe(() => party.Party?.MobileParty?.StringId, (string?)null);
                        if (id != null && (id.StartsWith("training_opponents", StringComparison.Ordinal)
                            || id.StartsWith("training_mock_enemy", StringComparison.Ordinal)))
                            return true;
                    }
                }
            }
            catch { /* an unreadable side is no drill */ }
            return false;
        }

        // ------------------------------ reading the world ------------------------------

        // The arena, from the event's own kind — with the water overriding the open field
        // (War Sails: a battle off the coast is a sea-fight whatever else it is).
        private static string KindOf(MapEvent e)
        {
            if (Safe(() => e.IsHideoutBattle, false)) return BattleRecord.Kinds.Hideout;
            if (Safe(() => e.IsSiegeAssault, false)) return BattleRecord.Kinds.Siege;
            if (Safe(() => e.IsSallyOut || e.IsBlockadeSallyOut, false)) return BattleRecord.Kinds.Sally;
            if (Safe(() => e.IsRaid, false)) return BattleRecord.Kinds.Raid;
            if (Safe(() => e.IsForcingVolunteers || e.IsForcingSupplies, false)) return BattleRecord.Kinds.Village;
            if (Safe(() => e.IsNavalMapEvent, false)) return BattleRecord.Kinds.Sea;
            return BattleRecord.Kinds.Field;
        }

        // The settlement the fighting belongs to, or the nearest named one — the player points at
        // the map and says "near Ortysia", and the chronicle should speak the same tongue.
        private static string NearestPlaceName(MapEvent e)
        {
            var own = Safe(() => e.MapEventSettlement?.Name?.ToString(), (string?)null);
            if (!string.IsNullOrWhiteSpace(own)) return own!;

            try
            {
                var at = e.Position;
                Settlement? nearest = null;
                float best = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || s.IsHideout) continue;
                    float d = Safe(() => at.DistanceSquared(s.Position), float.MaxValue);
                    if (d < best) { best = d; nearest = s; }
                }
                return nearest?.Name?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        // Who we fought, named for the telling: the army if one marched, else the leader's
        // war-band with their realm, else the party's own name — plus the bands beside.
        private static string EnemyNameOf(MapEvent e, BattleSideEnum theirSide)
        {
            try
            {
                var side = e.GetMapEventSide(theirSide);
                var leaderParty = Safe(() => e.GetLeaderParty(theirSide), (PartyBase?)null);
                int bands = Safe(() => side.Parties.Count, 1);

                var army = Safe(() => leaderParty?.MobileParty?.Army?.Name?.ToString(), (string?)null);
                if (!string.IsNullOrWhiteSpace(army)) return "the " + army!.Trim();

                var leader = Safe(() => leaderParty?.LeaderHero, (Hero?)null);
                string name;
                if (leader != null)
                {
                    var realm = Safe(() => leader.MapFaction?.Name?.ToString(), (string?)null);
                    name = $"the war-band of {leader.Name}" + (string.IsNullOrWhiteSpace(realm) ? "" : $" of {realm}");
                }
                else
                {
                    name = Safe(() => leaderParty?.Name?.ToString(), (string?)null) ?? "our foes";
                }

                if (bands > 1) name += $", and {(bands - 1 == 1 ? "another band" : $"{bands - 1} bands")} beside";
                return name;
            }
            catch { return "our foes"; }
        }

        // One side counted whole: every soul in the event's own flattened rosters, the four kinds
        // of fighter and the seasoning from the troops (heroes counted in the total, not the
        // classes), the cost from the per-party in-battle rosters — never from pre-battle wounds.
        private static BattleSideStats CountSide(MapEventSide side)
        {
            var stats = new BattleSideStats();
            if (side == null) return stats;

            double tierSum = 0; int tierN = 0;
            foreach (var party in side.Parties)
            {
                try
                {
                    stats.Fallen += Safe(() => party.DiedInBattle?.TotalManCount ?? 0, 0);
                    stats.Wounded += Safe(() => party.WoundedInBattle?.TotalManCount ?? 0, 0);
                    stats.Routed += Safe(() => party.RoutedInBattle?.TotalManCount ?? 0, 0);
                    stats.Ships += Safe(() => party.Ships?.Count ?? 0, 0);

                    var troops = party.Troops;
                    if (troops == null) continue;
                    foreach (var el in troops)
                    {
                        var troop = Safe(() => el.Troop, (CharacterObject?)null);
                        if (troop == null) continue;
                        stats.Total++;
                        if (troop.IsHero) continue;

                        tierSum += Safe(() => troop.Tier, 0); tierN++;
                        bool ranged = Safe(() => troop.IsRanged, false);
                        bool mounted = Safe(() => troop.IsMounted, false);
                        if (ranged && mounted) stats.HorseArchers++;
                        else if (mounted) stats.Horse++;
                        else if (ranged) stats.Archers++;
                        else stats.Foot++;
                    }
                }
                catch { /* one unreadable party must not blank the side */ }
            }
            stats.TierAverage = tierN > 0 ? Math.Round(tierSum / tierN, 1) : 0;
            stats.ShipsLost = Safe(() => side.ShipCasualties, 0);
            return stats;
        }

        private static List<Hero> HeroesOfSide(MapEvent e, BattleSideEnum ourSide)
        {
            var heroes = new List<Hero>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var party in e.GetMapEventSide(ourSide).Parties)
                {
                    var troops = Safe(() => party.Troops, (TaleWorlds.CampaignSystem.Roster.FlattenedTroopRoster?)null);
                    if (troops == null) continue;
                    foreach (var el in troops)
                    {
                        var hero = Safe(() => el.Troop?.HeroObject, (Hero?)null);
                        if (hero == null || !seen.Add(hero.StringId)) continue;
                        if (hero == Hero.MainHero) heroes.Insert(0, hero);
                        else heroes.Add(hero);
                    }
                }
            }
            catch { /* whoever was read, was read */ }
            return heroes;
        }

        private static string ParticipantStateOf(Hero hero)
        {
            if (Safe(() => !hero.IsAlive, false)) return BattleParticipant.States.Fell;
            if (Safe(() => hero.IsPrisoner, false)) return BattleParticipant.States.Captured;
            if (Safe(() => hero.IsWounded, false)) return BattleParticipant.States.Wounded;
            return BattleParticipant.States.Hale;
        }

        // The loot roster read into plain lines, ordered so the telling runs weapons → banners.
        private static List<BattleLootItem> CollectLoot(PlayerEncounter enc)
        {
            var items = new List<BattleLootItem>();
            try
            {
                var roster = enc.RosterToReceiveLootItems;
                if (roster == null) return items;
                foreach (var el in roster)
                {
                    var item = Safe(() => el.EquipmentElement.Item, (ItemObject?)null);
                    if (item == null || el.Amount <= 0) continue;
                    items.Add(new BattleLootItem(
                        Safe(() => item.Name?.ToString(), (string?)null) ?? "goods",
                        el.Amount,
                        Safe(() => item.Value, 0),
                        LootCategoryOf(item)));
                }
                items.Sort((a, b) => CategoryRank(a.Category).CompareTo(CategoryRank(b.Category)));
            }
            catch { /* spoils are a nicety of the record */ }
            return items;
        }

        // The game's many item types folded into the chronicle's few telling kinds.
        private static string LootCategoryOf(ItemObject item)
        {
            switch (Safe(() => item.Type, ItemObject.ItemTypeEnum.Goods))
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket:
                    return "weapons";
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Sling:
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                case ItemObject.ItemTypeEnum.SlingStones:
                case ItemObject.ItemTypeEnum.Bullets:
                    return "bows & bolts";
                case ItemObject.ItemTypeEnum.Shield:
                    return "shields";
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.ChestArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return "pieces of armor";
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.Animal:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return "horses & beasts";
                case ItemObject.ItemTypeEnum.Banner:
                    return "banners";
                default:
                    return "goods";
            }
        }

        private static int CategoryRank(string category)
        {
            switch (category)
            {
                case "weapons": return 0;
                case "bows & bolts": return 1;
                case "shields": return 2;
                case "pieces of armor": return 3;
                case "horses & beasts": return 4;
                case "goods": return 5;
                case "banners": return 6;
                default: return 7;
            }
        }

        // The Calradia date alone (the Timestamp helper carries the clock too, which a chronicle
        // has no use for): "Summer 12, Year 1084".
        private static string CalradiaDate()
        {
            try
            {
                var stamp = Personas.SituationBuilder.Timestamp();
                int open = stamp.IndexOf('(');
                int close = stamp.IndexOf(')');
                return open >= 0 && close > open ? stamp.Substring(open + 1, close - open - 1) : stamp;
            }
            catch { return string.Empty; }
        }

        // ------------------------------ what the rest of the mod reads ------------------------------

        /// <summary>The chronicle as one soul's situation carries it: the roll of battles lived at
        /// the player's side, the freshest told in full. Empty when they never shared a field.</summary>
        internal string BattleChronicleBlock(Hero speaker, Hero partner)
        {
            try
            {
                if (!_config.EnableBattleChronicle || _battleLedger == null) return string.Empty;
                if (speaker == null || partner != Hero.MainHero) return string.Empty;

                var shared = _battleLedger.SharedWith(speaker.StringId);
                if (shared.Count == 0) return string.Empty;

                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                return BattleText.SituationBlock(shared, playerName, mentionRecall: CanRecallChronicle(speaker));
            }
            catch { return string.Empty; }
        }

        /// <summary>True when the recall_battle tool should ride for this soul: the chronicle is on
        /// and they have at least one battle shared with the player to recall.</summary>
        private bool CanRecallChronicle(Hero npc)
        {
            try
            {
                return _config.EnableBattleChronicle
                    && _battleLedger != null
                    && npc != null
                    && _battleLedger.SharedWith(npc.StringId).Count > 0;
            }
            catch { return false; }
        }

        // ------------------------------ the test lever ------------------------------

        // Forges a plausible battle record for the very soul spoken with — the whole pipeline
        // (JSON, chronicle.txt, the beat, the situation block, the recall tool) testable without
        // waiting for a real fight. The muster is the player's true party; the foe is invented.
        private void OnDebugForgeBattle()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null) return;
            ForgeBattleFor(npc);
        }

        internal void ForgeBattleFor(Hero npc)
        {
            try
            {
                if (npc == null || _battleLedger == null) return;

                var ours = new BattleSideStats();
                double tierSum = 0; int tierN = 0;
                try
                {
                    foreach (var el in MobileParty.MainParty.MemberRoster.GetTroopRoster())
                    {
                        if (el.Character == null) continue;
                        ours.Total += el.Number;
                        if (el.Character.IsHero) continue;
                        tierSum += (double)el.Character.Tier * el.Number; tierN += el.Number;
                        bool ranged = el.Character.IsRanged, mounted = el.Character.IsMounted;
                        if (ranged && mounted) ours.HorseArchers += el.Number;
                        else if (mounted) ours.Horse += el.Number;
                        else if (ranged) ours.Archers += el.Number;
                        else ours.Foot += el.Number;
                    }
                }
                catch { }
                ours.TierAverage = tierN > 0 ? Math.Round(tierSum / tierN, 1) : 1.5;
                if (ours.Total <= 0) { ours.Total = 20; ours.Foot = 20; }
                ours.Fallen = Math.Max(1, ours.Total / 12);
                ours.Wounded = Math.Max(1, ours.Total / 8);

                var theirs = new BattleSideStats
                {
                    Total = (int)(ours.Total * 2.3),
                    TierAverage = 1.6,
                };
                theirs.Foot = (int)(theirs.Total * 0.7);
                theirs.Archers = theirs.Total - theirs.Foot;
                theirs.Fallen = theirs.Total / 3;
                theirs.Wounded = theirs.Total / 4;
                theirs.Routed = Math.Max(0, theirs.Total - theirs.Fallen - theirs.Wounded - theirs.Total / 4);

                var record = new BattleRecord
                {
                    Id = _battleLedger.NextId(CampaignTime.Now.ToDays),
                    GameDay = CampaignTime.Now.ToDays,
                    DateText = CalradiaDate(),
                    TimeOfDay = Personas.SituationBuilder.TimeOfDay(),
                    PlaceName = Safe(() => Personas.SituationBuilder.Place(Hero.MainHero), string.Empty),
                    Kind = BattleRecord.Kinds.Field,
                    PlayerAttacked = true,
                    Outcome = BattleRecord.Outcomes.Victory,
                    EnemyName = "a lawless brotherhood of the roads",
                    Ours = ours,
                    Theirs = theirs,
                    PrisonersTaken = theirs.Total / 4,
                    CaptivesFreed = 3,
                    GoldGained = 240,
                    RenownGained = 9,
                    InfluenceGained = 4,
                    Loot = BattleLoot.Summarize(new[]
                    {
                        new BattleLootItem("Notched Longsword", 3, 410, "weapons"),
                        new BattleLootItem("Hunting Bow", 4, 130, "bows & bolts"),
                        new BattleLootItem("Worn Round Shield", 5, 70, "shields"),
                        new BattleLootItem("Patched Gambeson", 6, 180, "pieces of armor"),
                        new BattleLootItem("Sumpter Horse", 2, 120, "horses & beasts"),
                        new BattleLootItem("Grain", 14, 12, "goods"),
                    }),
                };
                record.Participants.Add(new BattleParticipant
                {
                    HeroId = Hero.MainHero.StringId,
                    Name = Hero.MainHero.Name?.ToString() ?? "the traveler",
                    IsPlayer = true,
                    Downs = 7,
                    State = BattleParticipant.States.Hale,
                });
                record.Participants.Add(new BattleParticipant
                {
                    HeroId = npc.StringId,
                    Name = npc.Name?.ToString() ?? npc.StringId,
                    Downs = 3,
                    State = Safe(() => npc.IsWounded, false) ? BattleParticipant.States.Wounded : BattleParticipant.States.Hale,
                });

                record.Title = BattleText.ForgeTitle(record);
                record.ShortTale = BattleText.ShortTale(record);
                record.FullTale = BattleText.FullAccount(record);
                _battleLedger.Save(record);
                _battleLedger.AppendToChronicle(record.FullTale);

                var heroById = new Dictionary<string, Hero> { [npc.StringId] = npc };
                RecordBattleBeats(record, heroById);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"The chronicle sets down: '{record.Title}'. (forged for testing)", ActivityColor));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: could not forge the record — " + ex.Message));
            }
        }

        // ------------------------------ tiny guards ------------------------------

        private static T Safe<T>(Func<T> read, T fallback)
        {
            try { return read(); }
            catch { return fallback; }
        }
    }
}
