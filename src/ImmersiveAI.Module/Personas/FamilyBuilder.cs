using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Builds an NPC's kin and house as a gentle second-person recollection — parents and where they
    /// come from, spouse, children (with ages), siblings, and their clan and its people — so on every
    /// chat they feel like part of a family in this world, not a lone voice. Durable identity, folded
    /// into the prompt (see <c>NpcPersona.FamilyKnowledge</c>) rather than the passing situation block.
    ///
    /// Everything is best-effort: any missing or throwing game datum is simply skipped, never aborting
    /// the block. Deceased close kin are still named (softly), because who someone has lost is part of
    /// who they are.
    /// </summary>
    public static class FamilyBuilder
    {
        public static string Build(Hero npc)
        {
            if (npc == null) return string.Empty;

            var lines = new List<string>();

            // Parentage — who they are the child of, and the house they were born to.
            Try(() =>
            {
                var father = npc.Father;
                var mother = npc.Mother;
                var child = npc.IsFemale ? "daughter" : "son";
                if (father != null && mother != null)
                    lines.Add($"You are the {child} of {Describe(father)} and {Describe(mother)}.");
                else if (father != null)
                    lines.Add($"You are the {child} of {Describe(father)}.");
                else if (mother != null)
                    lines.Add($"You are the {child} of {Describe(mother)}.");
            });

            // Spouse.
            Try(() =>
            {
                var spouse = npc.Spouse;
                if (spouse == null) return;
                lines.Add(spouse.IsAlive
                    ? $"You are wed to {DescribePerson(spouse)}."
                    : $"You were wed to {NameOf(spouse)}, now passed.");
            });

            // Living children, with their ages so the years show.
            Try(() =>
            {
                var kids = npc.Children?
                    .Where(c => c != null && c.IsAlive)
                    .Select(ChildClause)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (kids == null || kids.Count == 0) return;
                lines.Add(kids.Count == 1
                    ? $"Your child is {kids[0]}."
                    : $"Your children are {JoinAnd(kids)}.");
            });

            // Living siblings, by name.
            Try(() =>
            {
                var kin = npc.Siblings?
                    .Where(s => s != null && s.IsAlive)
                    .Select(NameOf)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();
                if (kin == null || kin.Count == 0) return;
                lines.Add(kin.Count == 1
                    ? $"Your brother or sister is {kin[0]}."
                    : $"Your brothers and sisters are {JoinAnd(kin)}.");
            });

            // The clan and its people — the wider house they belong to.
            Try(() =>
            {
                var clan = npc.Clan;
                if (clan == null) return;
                var clanName = clan.Name?.ToString();
                if (string.IsNullOrWhiteSpace(clanName)) return;

                var leader = clan.Leader;
                if (leader != null && leader != npc)
                    lines.Add($"Your clan is {clanName}, led by {NameOf(leader)}.");
                else if (leader == npc)
                    lines.Add($"You yourself lead clan {clanName}.");
                else
                    lines.Add($"Your clan is {clanName}.");

                var kin = clan.Heroes?
                    .Where(h => h != null && h.IsAlive && h != npc && h != leader)
                    .Select(NameOf)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .Take(6)
                    .ToList();
                if (kin != null && kin.Count > 0)
                    lines.Add($"Among its people are {JoinAnd(kin)}.");
            });

            if (lines.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("Your kin and house, ever close to your heart:");
            sb.Append(string.Join(" ", lines));
            return sb.ToString().TrimEnd();
        }

        // "Corein, a Battanian of clan Fen Irtaz" — culture and house for a close relative (best-effort).
        private static string Describe(Hero h)
        {
            var name = NameOf(h);
            var culture = h?.Culture?.Name?.ToString();
            var clan = h?.Clan?.Name?.ToString();
            var sb = new StringBuilder(name);
            if (!string.IsNullOrWhiteSpace(culture)) sb.Append($", {A(culture!)} {culture!.Trim()}");
            if (!string.IsNullOrWhiteSpace(clan)) sb.Append($" of clan {clan!.Trim()}");
            var s = sb.ToString();
            return h != null && !h.IsAlive ? s + " (now passed)" : s;
        }

        // "Arwa, a woman of some 30 years" — gender and age for spouse.
        private static string DescribePerson(Hero h)
        {
            var name = NameOf(h);
            var gender = h.IsFemale ? "a woman" : "a man";
            int age = 0;
            Try(() => age = (int)h.Age);
            return age > 0 ? $"{name}, {gender} of some {age} years" : $"{name}, {gender}";
        }

        // "Yorwen (a girl of 8)" — a child with their years.
        private static string ChildClause(Hero c)
        {
            var name = NameOf(c);
            var word = c.IsFemale ? "girl" : "boy";
            int age = 0;
            Try(() => age = (int)c.Age);
            return age > 0 ? $"{name} (a {word} of {age})" : $"{name} (a {word})";
        }

        private static string NameOf(Hero h) => h?.Name?.ToString()?.Trim() ?? "someone unknown";

        private static string A(string word)
        {
            if (string.IsNullOrEmpty(word)) return "a";
            return "aeiou".IndexOf(char.ToLowerInvariant(word[0])) >= 0 ? "an" : "a";
        }

        private static string JoinAnd(List<string> items)
        {
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return items[0] + " and " + items[1];
            return string.Join(", ", items.Take(items.Count - 1)) + ", and " + items[items.Count - 1];
        }

        private static void Try(Action a) { try { a(); } catch { /* skip this fact */ } }
    }
}
