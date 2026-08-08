using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Battles
{
    /// <summary>
    /// One battle the player lived through, set down for good — the chronicle's unit. Written by the
    /// game layer at the battle's end (numbers from the game's own reckoning, never invented) and kept
    /// as one JSON file per battle in the campaign's _battles folder, so the record survives beside the
    /// memories it feeds: every allied hero who stood in it gets a short recorded beat pointing here,
    /// and the recall_battle tool reads the full account back on request. Everything in it is plain
    /// data; all prose lives in <see cref="BattleText"/>.
    /// </summary>
    public sealed class BattleRecord
    {
        public int Version { get; set; } = 1;

        /// <summary>Stable id, "d0123-2" = the second battle of campaign day 123. Doubles as the
        /// file-name prefix, so a record can be found and rewritten (loot arrives a breath after
        /// the fighting ends).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>The name the battle is remembered by — "The Grand Victory near Ortysia, over
        /// Thrice Our Number". Forged once by <see cref="BattleText.ForgeTitle"/>; never changes.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Human-readable Calradia date ("Summer 12, Year 1084").</summary>
        public string DateText { get; set; } = string.Empty;

        /// <summary>Coarse hour of the fighting ("early afternoon").</summary>
        public string TimeOfDay { get; set; } = string.Empty;

        public double GameDay { get; set; }

        /// <summary>The nearest named place — a settlement's plain name ("Ortysia"), or empty when
        /// truly nowhere. Kind-aware phrasing ("near X", "at the walls of X", "on the waters off X")
        /// is prose, not data — see <see cref="BattleText.PlacePhrase"/>.</summary>
        public string PlaceName { get; set; } = string.Empty;

        /// <summary>Where and how it was fought — one of the <see cref="Kinds"/> constants.</summary>
        public string Kind { get; set; } = Kinds.Field;

        /// <summary>True when our side gave the attack; false when we stood to receive it.</summary>
        public bool PlayerAttacked { get; set; }

        /// <summary>How the day ended — one of the <see cref="Outcomes"/> constants.</summary>
        public string Outcome { get; set; } = Outcomes.Draw;

        /// <summary>Who we fought, named for the telling — "the war-band of Akrum of the Khuzaits,
        /// and two bands beside".</summary>
        public string EnemyName { get; set; } = string.Empty;

        public BattleSideStats Ours { get; set; } = new BattleSideStats();
        public BattleSideStats Theirs { get; set; } = new BattleSideStats();

        /// <summary>Souls of theirs led away in our train when the field was ours.</summary>
        public int PrisonersTaken { get; set; }

        /// <summary>Captives of THEIRS set free by our victory — the people they had in chains.</summary>
        public int CaptivesFreed { get; set; }

        /// <summary>Denars of plunder that reached the player's own purse (the game's plundered-gold
        /// share, not the worth of goods — that lives in <see cref="Loot"/>).</summary>
        public int GoldGained { get; set; }

        /// <summary>Renown the player's name gained by the day, rounded; 0 when none or unknowable.</summary>
        public int RenownGained { get; set; }

        /// <summary>Influence gained among the realm's peers, rounded; 0 when none.</summary>
        public int InfluenceGained { get; set; }

        /// <summary>The spoils offered to the player when the field was won — totals, kinds, and the
        /// notable pieces. Empty (zero total, no lines) when the day yielded none.</summary>
        public BattleLoot Loot { get; set; } = new BattleLoot();

        /// <summary>Every hero who stood on OUR side — the player first, then companions and allied
        /// lords — each with their own deeds and how they came out of it.</summary>
        public List<BattleParticipant> Participants { get; set; } = new List<BattleParticipant>();

        /// <summary>One or two sentences in the shared "we" voice — what the roll of battles shows
        /// for this day once newer battles have pushed it down the list.</summary>
        public string ShortTale { get; set; } = string.Empty;

        /// <summary>The full account (see <see cref="BattleText.FullAccount"/>), rendered at the
        /// record's last write so the chronicle file is readable on its own.</summary>
        public string FullTale { get; set; } = string.Empty;

        public BattleParticipant? ParticipantById(string heroId) =>
            string.IsNullOrEmpty(heroId)
                ? null
                : Participants.FirstOrDefault(p => p != null && string.Equals(p.HeroId, heroId, StringComparison.Ordinal));

        public BattleParticipant? Player =>
            Participants.FirstOrDefault(p => p != null && p.IsPlayer);

        /// <summary>The arena a battle was fought in. Strings, not an enum, so records survive any
        /// future kind without a schema change.</summary>
        public static class Kinds
        {
            public const string Field = "field";
            public const string Siege = "siege";        // assault on walls; PlayerAttacked tells whose
            public const string Sally = "sally";        // the besieged bursting out
            public const string Sea = "sea";            // fought on the water (War Sails)
            public const string Hideout = "hideout";    // rooting brigands from their den
            public const string Raid = "raid";          // a village put to the torch
            public const string Village = "village";    // pressed for volunteers or supplies
        }

        /// <summary>How a day of battle can end.</summary>
        public static class Outcomes
        {
            public const string Victory = "victory";
            public const string Defeat = "defeat";
            /// <summary>Blood was shed but nobody broke — a withdrawal or a parting of ways.</summary>
            public const string Draw = "draw";
        }
    }

    /// <summary>One side's muster and its cost, counted by the game's own rosters. The four kinds of
    /// fighter plus a seasoning average describe an army's strength without a ledger's sprawl.</summary>
    public sealed class BattleSideStats
    {
        /// <summary>Souls under arms when the lines met.</summary>
        public int Total { get; set; }

        public int Foot { get; set; }
        public int Archers { get; set; }
        public int Horse { get; set; }
        public int HorseArchers { get; set; }

        /// <summary>Average troop tier (0..7), the "seasoning" of the muster.</summary>
        public double TierAverage { get; set; }

        public int Fallen { get; set; }
        public int Wounded { get; set; }

        /// <summary>Those who broke and ran rather than fell.</summary>
        public int Routed { get; set; }

        /// <summary>Ships fielded (sea battles; 0 elsewhere).</summary>
        public int Ships { get; set; }
        public int ShipsLost { get; set; }
    }

    /// <summary>One hero of OUR side in one battle: what their own hand did and how they came out.</summary>
    public sealed class BattleParticipant
    {
        public string HeroId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsPlayer { get; set; }

        /// <summary>Foes struck down by their own hand (the game scores every downing blow, fought
        /// or simulated). -1 when no tally reached us — an honest unknown, never a zero.</summary>
        public int Downs { get; set; }

        /// <summary>How they came out of it — one of the <see cref="States"/> constants.</summary>
        public string State { get; set; } = States.Hale;

        public static class States
        {
            public const string Hale = "hale";
            public const string Wounded = "wounded";
            public const string Fell = "fell";
            public const string Captured = "captured";
        }
    }

    /// <summary>The spoils of a won field, summarized: what it was all worth, how the pieces group
    /// by kind, and the few worth naming. Built from plain lines by <see cref="Summarize"/> so the
    /// game layer only has to enumerate the loot roster.</summary>
    public sealed class BattleLoot
    {
        /// <summary>Worth of everything offered, in denars (each piece at its own value).</summary>
        public int TotalValue { get; set; }

        /// <summary>Counts by kind, in a stable telling order — "weapons", "bows & bolts",
        /// "shields", "armor", "horses & beasts", "goods", "banners".</summary>
        public List<BattleLootCount> Categories { get; set; } = new List<BattleLootCount>();

        /// <summary>The richest pieces (by the worth of one), up to five.</summary>
        public List<BattleLootLine> TopValued { get; set; } = new List<BattleLootLine>();

        /// <summary>The most numerous pieces, up to five.</summary>
        public List<BattleLootLine> TopNumerous { get; set; } = new List<BattleLootLine>();

        public bool IsEmpty => TotalValue <= 0 && Categories.Count == 0;

        /// <summary>Folds raw loot lines (name, count, worth-of-one, kind) into the summary. The
        /// category names arrive from the game layer already grouped into telling-words; this keeps
        /// their first-seen order so weapons lead and banners trail as the caller listed them.</summary>
        public static BattleLoot Summarize(IEnumerable<BattleLootItem> items, int topCount = 5)
        {
            var loot = new BattleLoot();
            if (items == null) return loot;

            var lines = new List<BattleLootItem>();
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Name) || item.Count <= 0) continue;
                lines.Add(item);
            }
            if (lines.Count == 0) return loot;

            long total = 0;
            var categories = new List<BattleLootCount>();
            foreach (var line in lines)
            {
                total += (long)line.UnitValue * line.Count;
                var category = string.IsNullOrWhiteSpace(line.Category) ? "goods" : line.Category.Trim();
                var slot = categories.FirstOrDefault(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
                if (slot == null) categories.Add(new BattleLootCount { Category = category, Count = line.Count });
                else slot.Count += line.Count;
            }

            loot.TotalValue = total > int.MaxValue ? int.MaxValue : (int)total;
            loot.Categories = categories;
            loot.TopValued = lines
                .OrderByDescending(l => l.UnitValue)
                .ThenByDescending(l => l.Count)
                .Take(Math.Max(0, topCount))
                .Select(l => new BattleLootLine { Name = l.Name.Trim(), Count = l.Count, UnitValue = l.UnitValue })
                .ToList();
            loot.TopNumerous = lines
                .OrderByDescending(l => l.Count)
                .ThenByDescending(l => l.UnitValue)
                .Take(Math.Max(0, topCount))
                .Select(l => new BattleLootLine { Name = l.Name.Trim(), Count = l.Count, UnitValue = l.UnitValue })
                .ToList();
            return loot;
        }
    }

    /// <summary>A named piece among the spoils.</summary>
    public sealed class BattleLootLine
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        /// <summary>The worth of ONE such piece, in denars.</summary>
        public int UnitValue { get; set; }
    }

    /// <summary>How many pieces of one kind the spoils held.</summary>
    public sealed class BattleLootCount
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>One raw loot line as the game layer reads it off the roster, before summarizing.</summary>
    public sealed class BattleLootItem
    {
        public BattleLootItem() { }
        public BattleLootItem(string name, int count, int unitValue, string category)
        {
            Name = name; Count = count; UnitValue = unitValue; Category = category;
        }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public int UnitValue { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
