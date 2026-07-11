using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Builds an NPC's kin and house in their OWN first person — parents and where they come from,
    /// spouse, children (with ages), siblings, and their clan and its people with what each is
    /// presently about — so on every chat they feel like part of a living family, not a lone voice.
    /// Durable identity, folded into the prompt (see <c>NpcPersona.FamilyKnowledge</c>) rather than
    /// the passing situation block. The clan's head is named with their kinship when there is one
    /// ("led by my husband Vulgrim"), so no one wonders who their own family is to them.
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
                    lines.Add($"I am the {child} of {Describe(father)} and {Describe(mother)}.");
                else if (father != null)
                    lines.Add($"I am the {child} of {Describe(father)}.");
                else if (mother != null)
                    lines.Add($"I am the {child} of {Describe(mother)}.");
            });

            // Spouse.
            Try(() =>
            {
                var spouse = npc.Spouse;
                if (spouse == null) return;
                lines.Add(spouse.IsAlive
                    ? $"I am wed to {DescribePerson(spouse)}."
                    : $"I was wed to {NameOf(spouse)}, now passed.");
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
                    ? $"My child is {kids[0]}."
                    : $"My children are {JoinAnd(kids)}.");
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
                    ? $"My brother or sister is {kin[0]}."
                    : $"My brothers and sisters are {JoinAnd(kin)}.");
            });

            // The clan and its people — the wider house they belong to, each named with what they
            // are presently about, and the head named with their kinship to me when there is one.
            Try(() =>
            {
                var clan = npc.Clan;
                if (clan == null) return;
                var clanName = clan.Name?.ToString();
                if (string.IsNullOrWhiteSpace(clanName)) return;

                var leader = clan.Leader;
                if (leader == npc)
                    lines.Add($"I myself lead our clan, {clanName}.");
                else if (leader != null)
                {
                    var kinship = KinshipTo(npc, leader, closeOnly: true);
                    lines.Add(kinship == null
                        ? $"My clan is {clanName}, led by {NameOf(leader)}."
                        : $"My clan is {clanName}, led by {kinship} {NameOf(leader)}.");
                }
                else
                    lines.Add($"My clan is {clanName}.");

                // Close kin already named above (spouse, children, parents, siblings) stay out of
                // the clan roll — Menja must not appear twice, once as "my child" and once as a
                // clanswoman (Anton's Thyrsif snapshot, 2026.07.12).
                var kin = clan.Heroes?
                    .Where(h => h != null && h.IsAlive && h != npc && h != leader
                        && !Safe(() => h == npc.Spouse || h == npc.Father || h == npc.Mother
                            || h.Father == npc || h.Mother == npc
                            || (npc.Siblings != null && npc.Siblings.Contains(h))))
                    .Select(MemberClause)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .Take(8)
                    .ToList();
                if (kin != null && kin.Count > 0)
                    lines.Add($"Among its people are {JoinAnd(kin)}.");
            });

            if (lines.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("My kin and house, close to me:");
            sb.Append(string.Join(" ", lines));
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// What <paramref name="other"/> is to <paramref name="speaker"/>, as the speaker would name
        /// them — "my husband", "my daughter", "the head of my clan", "my liege" — or null when they
        /// are no close kin. Shared by the kin block and the situation's arrival line, so an NPC
        /// never wonders who their own family is when one comes to speak. With
        /// <paramref name="closeOnly"/> only blood-and-marriage names are returned (the clan-led
        /// line must never read "led by the head of my clan").
        /// </summary>
        public static string KinshipTo(Hero speaker, Hero other, bool closeOnly = false)
        {
            if (speaker == null || other == null || speaker == other) return null;
            try
            {
                if (speaker.Spouse == other) return other.IsFemale ? "my wife" : "my husband";
                if (speaker.Father == other) return "my father";
                if (speaker.Mother == other) return "my mother";
                if (other.Father == speaker || other.Mother == speaker)
                    return other.IsFemale ? "my daughter" : "my son";
                if (Safe(() => speaker.Siblings != null && speaker.Siblings.Contains(other)))
                    return other.IsFemale ? "my sister" : "my brother";
                // A shared child binds even where the game's single Spouse slot does not point here
                // (polygamy mods park further wives elsewhere — Anton's Thyrsif knew Vulgrim only as
                // "the head of my clan" while carrying his child, 2026.07.12).
                if (Safe(() => speaker.Children != null
                        && speaker.Children.Any(c => c != null && (c.Father == other || c.Mother == other))))
                    return other.IsFemale ? "the mother of my children" : "the father of my children";
                if (closeOnly) return null;
                if (Safe(() => speaker.Clan != null && speaker.Clan.Leader == other))
                    return "the head of my clan";
                if (Safe(() => speaker.Clan?.Kingdom != null && speaker.Clan.Kingdom.Leader == other))
                    return "my liege";
                if (Safe(() => speaker.Clan != null && other.Clan == speaker.Clan))
                    return other.IsFemale ? "my kinswoman" : "my kinsman";
            }
            catch { /* no kinship surfaced */ }
            return null;
        }

        // "Yfinja (leading a warband of her own)" — a clan member named with what they are about,
        // so the house feels alive and I know where my people stand.
        private static string MemberClause(Hero h)
        {
            var name = NameOf(h);
            string doing = null;
            Try(() =>
            {
                var kept = h.GovernorOf?.Settlement?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(kept)) { doing = $"governor of {kept}"; return; }

                var party = h.PartyBelongedTo;
                if (party != null && party.LeaderHero == h)
                { doing = h.IsFemale ? "leading a warband of her own" : "leading a warband of his own"; return; }
                if (party != null && party.LeaderHero != null)
                { doing = $"riding with {NameOf(party.LeaderHero)}"; return; }

                int years = (int)h.Age;
                if (years < 1) { doing = "a babe in arms"; return; }
                if (years < 18) { doing = h.IsFemale ? $"a girl of {years}" : $"a boy of {years}"; return; }

                var at = h.CurrentSettlement?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(at)) doing = $"now at {at}";
            });
            return doing == null ? name : $"{name} ({doing})";
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

        // "Yorwen (a girl of 8)" — a child with their years; the newest ones as babes, not "of 0".
        private static string ChildClause(Hero c)
        {
            var name = NameOf(c);
            var word = c.IsFemale ? "girl" : "boy";
            int age = -1;
            Try(() => age = (int)c.Age);
            if (age == 0) return $"{name} (a babe in arms)";
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

        private static bool Safe(Func<bool> f) { try { return f(); } catch { return false; } }
    }
}
