using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Gear;
using ImmersiveAI.Core.Memory;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ImmersiveAI
{
    /// <summary>
    /// SHE NOTICES WHEN YOU CHANGE HER GEAR (Anton, 2026.08.16: "tell them if I take off an item, if
    /// I add item… add the item values, cause they might not know it and its giving info on how
    /// valuable it is").
    ///
    /// THE HOOK IS A BRACKET, AND THAT IS THE WHOLE DESIGN. There is no equipment-changed event on
    /// this game version — the only three events naming Equipment are smelting, a caravan
    /// transaction, and a GATE (`CanHeroEquipmentBeChanged`), none of which announce a slot moving.
    /// What there is instead is a pair of moments we can stand between:
    ///
    ///   • the inventory screen OPENING, seen on the frame tick as `InventoryState` becoming the
    ///     active game state — where the baseline is taken;
    ///   • the inventory screen CLOSING, which fires `PlayerInventoryExchange` unconditionally from
    ///     `InventoryLogic.DoneLogic()`, trade or none — where the diff is taken.
    ///
    /// TWO REASONS THE BRACKET BEATS A REMEMBERED BASELINE, both of them things a persisted one
    /// would get badly wrong:
    ///
    ///   • The game rewrites equipment BY ITSELF, in several places: coming of age, becoming a
    ///     ruler, and — our own doing — a companion raised to lordship at her wedding. It also runs
    ///     `CheckInvalidEquipmentsAndReplaceIfNeeded` over every hero on EVERY LOAD. A remembered
    ///     baseline would answer "he took my helmet" for half the roster after any of that. A
    ///     baseline that only exists while the screen is open cannot see any of it, by construction.
    ///   • Cancelling the screen rolls the equipment back BEFORE the close event fires, so a
    ///     cancelled session diffs to nothing without a line of code spent on it.
    ///
    /// AND THE SAME BRACKET IS THE DEDUPE, which is the part that would otherwise be got wrong.
    /// Trying a helmet on and taking it off again is start == end, so it is silent. Three swords
    /// through one slot is one line, first to last. Reordering her four weapons is no change at all,
    /// because arms are compared as a MULTISET rather than slot by slot. None of that needs a timer
    /// or a cap; it falls out of only ever looking twice.
    /// </summary>
    public sealed partial class ImmersiveChatBehavior
    {
        /// <summary>What everyone's kit looked like when the screen opened. SESSION-ONLY and never
        /// persisted — see the class remark: a baseline that outlives the screen is a baseline that
        /// blames the player for the game's own doing.</summary>
        private readonly Dictionary<string, EquipmentElement[]> _gearBaseline =
            new Dictionary<string, EquipmentElement[]>();

        private bool GearNotesOn => _config != null && _config.EnableGearNotes;

        /// <summary>The inventory screen has come up: remember what everyone carries. Called from
        /// the frame tick on the rising edge only.</summary>
        internal static void NoteInventoryOpened()
        {
            try { Current?.TakeGearBaseline(); }
            catch (Exception ex) { ModLog.Error("remembering what they carried", ex); }
        }

        private void TakeGearBaseline()
        {
            _gearBaseline.Clear();
            if (!GearNotesOn) return;

            foreach (var hero in GearWatched())
            {
                var kit = SnapshotOf(hero);
                if (kit != null) _gearBaseline[hero.StringId] = kit;
            }
        }

        /// <summary>Whose gear the player could plausibly have just changed: the souls riding with
        /// them, and their own clan's heroes (the screen reaches another of your parties too).
        /// Never the player themselves, and never the dead — a dead hero's equipment is a SHARED
        /// GLOBAL object on this version, so every corpse would diff equal to every other.</summary>
        private static List<Hero> GearWatched()
        {
            var seen = new List<Hero>();
            try
            {
                void Consider(Hero? h)
                {
                    if (h == null || h == Hero.MainHero || !h.IsAlive) return;
                    if (seen.Contains(h)) return;
                    seen.Add(h);
                }

                var party = MobileParty.MainParty;
                if (party?.MemberRoster != null)
                    foreach (var entry in party.MemberRoster.GetTroopRoster())
                        if (entry.Character != null && entry.Character.IsHero)
                            Consider(entry.Character.HeroObject);

                var clan = Clan.PlayerClan;
                if (clan?.Heroes != null)
                    foreach (var h in clan.Heroes) Consider(h);
            }
            catch { /* an incomplete watch list is better than none */ }
            return seen;
        }

        // Battle kit only. The civilian and stealth sets change with it far more often than a person
        // would remark on, and one beat about the sword she was handed is the thing Anton asked for.
        private static EquipmentElement[]? SnapshotOf(Hero hero)
        {
            try
            {
                var kit = hero.BattleEquipment;
                if (kit == null) return null;
                var slots = new EquipmentElement[(int)EquipmentIndex.NumEquipmentSetSlots];
                for (int i = 0; i < slots.Length; i++) slots[i] = kit[i];
                return slots;
            }
            catch { return null; }
        }

        /// <summary>The screen has closed: see what changed, and let each of them know.</summary>
        private void NoteGearChanges()
        {
            if (!GearNotesOn || _gearBaseline.Count == 0) return;

            // Taken FIRST, so a throw halfway through cannot leave a stale baseline standing that
            // would be diffed against some later session and blame the player for it.
            var before = new Dictionary<string, EquipmentElement[]>(_gearBaseline);
            _gearBaseline.Clear();

            foreach (var hero in GearWatched())
            {
                try
                {
                    if (!before.TryGetValue(hero.StringId, out var was)) continue;
                    var now = SnapshotOf(hero);
                    if (now == null) continue;

                    var set = Diff(was, now);
                    if (!set.Any) continue;

                    var line = GearText.Beat(set, PlayerName(), WageOf(hero));
                    if (line.Length == 0) continue;

                    // A silent beat, exactly as a battle or a stop is: no LLM call, no answer
                    // invented for her. PlayerEngaged rather than None on purpose — witnessing a
                    // trade is not attention, but being handed a sword is, and it should quiet the
                    // "you never answer me" damping the same way a meeting does. Two visits to the
                    // saddlebags in one day are one story, not two beats (the 2026.08.27 token
                    // audit): if her freshest memory is already today's gear beat, this joins it.
                    if (!TryJoinTodaysGearBeat(hero, line))
                        AppendRecordedTurn(hero, line, string.Empty, OutreachMark.PlayerEngaged);
                }
                catch (Exception ex) { ModLog.Error("setting down a change of gear", ex); }
            }
        }

        /// <summary>Joins a new gear change onto TODAY'S gear beat when that beat is the very last
        /// thing she remembers — a battle, a word, anything between them means the new change is
        /// its own moment again. The mark stays hers from the first beat; only the body grows.</summary>
        private bool TryJoinTodaysGearBeat(Hero hero, string line)
        {
            try
            {
                var memory = LoadMemory(hero);
                var turns = memory.RecentTurns;
                if (turns.Count == 0) return false;
                var last = turns[turns.Count - 1];
                if (!GearText.IsGearBeat(last.PlayerLine)) return false;
                if (!string.IsNullOrEmpty(last.NpcLine)) return false;
                if ((int)last.GameDay != (int)CampaignTime.Now.ToDays) return false;

                var tail = line.Substring(GearText.GearBeatMark.Length).Trim();
                if (tail.Length == 0) return false;
                last.PlayerLine = last.PlayerLine.TrimEnd() + " And later that day: " + tail;
                SaveMemory(hero, memory);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Their own daily wage — the one yardstick the beat offers beside a great sum, and
        /// a fact they already own. NEVER `PartyWageModel.GetCharacterWage`, which answers 1 for a
        /// hero (the "one denar a day" lie the hiring bargain already had to learn).</summary>
        private static int WageOf(Hero hero)
        {
            try { return hero.CharacterObject?.TroopWage ?? 0; }
            catch { return 0; }
        }

        private static GearChangeSet Diff(EquipmentElement[] was, EquipmentElement[] now)
        {
            var changes = new List<GearChange>();

            // The armour and the beasts, slot by slot: each of these admits exactly one kind of
            // thing, so the slot IS the word for it.
            foreach (var pair in NamedSlots)
            {
                int i = (int)pair.Key;
                if (i >= was.Length || i >= now.Length) continue;

                var before = was[i];
                var after = now[i];
                if (Same(before, after)) continue;

                changes.Add(new GearChange(pair.Value,
                    NameOf(after), ValueOf(after),
                    NameOf(before), ValueOf(before)));
            }

            // The arms, as a MULTISET. Four slots that admit anything, reordered constantly — a
            // shuffle of the same weapons is not a change to what she carries and must say nothing.
            var armsBefore = ArmsIn(was);
            var armsAfter = ArmsIn(now);
            foreach (var gone in Missing(armsBefore, armsAfter))
                changes.Add(new GearChange(GearSlot.Arms, takenName: gone.Key, takenValue: gone.Value));
            foreach (var got in Missing(armsAfter, armsBefore))
                changes.Add(new GearChange(GearSlot.Arms, got.Key, got.Value));

            return new GearChangeSet(changes);
        }

        private static readonly Dictionary<EquipmentIndex, GearSlot> NamedSlots =
            new Dictionary<EquipmentIndex, GearSlot>
            {
                // NOTE the enum's aliases collide — Head == NumAllWeaponSlots == 5, Horse ==
                // ArmorItemEndSlot == 10 — so one canonical name per value, and only these.
                { EquipmentIndex.Head, GearSlot.Head },
                { EquipmentIndex.Body, GearSlot.Body },
                { EquipmentIndex.Leg, GearSlot.Legs },
                { EquipmentIndex.Gloves, GearSlot.Hands },
                { EquipmentIndex.Cape, GearSlot.Cape },
                { EquipmentIndex.Horse, GearSlot.Mount },
                { EquipmentIndex.HorseHarness, GearSlot.Harness },
            };

        private static List<KeyValuePair<string, int>> ArmsIn(EquipmentElement[] kit)
        {
            var arms = new List<KeyValuePair<string, int>>();
            for (int i = (int)EquipmentIndex.WeaponItemBeginSlot; i <= (int)EquipmentIndex.ExtraWeaponSlot; i++)
            {
                if (i >= kit.Length) break;
                var name = NameOf(kit[i]);
                if (name.Length == 0) continue;
                arms.Add(new KeyValuePair<string, int>(name, ValueOf(kit[i])));
            }
            return arms;
        }

        /// <summary>What is in the first list and not in the second, counting duplicates — so a pair
        /// of javelins losing one of them is honestly one javelin gone.</summary>
        private static List<KeyValuePair<string, int>> Missing(
            List<KeyValuePair<string, int>> from, List<KeyValuePair<string, int>> against)
        {
            var left = new List<KeyValuePair<string, int>>(against);
            var gone = new List<KeyValuePair<string, int>>();
            foreach (var item in from)
            {
                int at = left.FindIndex(x => string.Equals(x.Key, item.Key, StringComparison.Ordinal));
                if (at >= 0) left.RemoveAt(at);
                else gone.Add(item);
            }
            return gone;
        }

        /// <summary>Item AND its modifier: a Masterwork blade is not the plain one. Cosmetics are
        /// deliberately ignored, which is what the game's own comparison does.</summary>
        private static bool Same(EquipmentElement a, EquipmentElement b)
        {
            try
            {
                if (a.Item == null && b.Item == null) return true;
                if (a.Item == null || b.Item == null) return false;
                return a.IsEqualTo(b);
            }
            catch { return true; }   // a comparison we cannot make is not a change we may claim
        }

        private static string NameOf(EquipmentElement e)
        {
            try { return e.Item == null ? string.Empty : (e.GetModifiedItemName()?.ToString() ?? string.Empty); }
            catch { return string.Empty; }
        }

        /// <summary>What the thing is WORTH, not what a market would pay for it today. A trade price
        /// swings with the town's stock and the player's haggling; a woman wearing a sword does not
        /// experience that. Worth belongs to the person, prices belong to the market tool.</summary>
        private static int ValueOf(EquipmentElement e)
        {
            try { return e.Item == null ? 0 : e.ItemValue; }
            catch { return 0; }
        }
    }
}
