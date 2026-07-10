using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Personas;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The NPCs' "gift of recall": tools an NPC may quietly call upon mid-thought to bring to mind
    /// what is truly known of a person, place, clan, or realm — live campaign data instead of
    /// hallucinated cousins and misplaced towns. Definitions are in-world ("call to mind..."), and
    /// every answer is written as gentle second-person remembrance, so nothing here breaks the
    /// fourth wall for the NPC.
    ///
    /// Resolution touches campaign state, so it is marshaled to the game thread via
    /// MainThreadDispatcher (drained every application tick) and awaited with a timeout; a recall
    /// that cannot be answered comes back as an honest blank, never an exception into the turn.
    /// </summary>
    public static class WorldRecall
    {
        public const string RecallPerson = "recall_person";
        public const string RecallPlace = "recall_place";
        public const string RecallClan = "recall_clan";
        public const string RecallRealm = "recall_realm";

        public static readonly IReadOnlyList<ToolDefinition> Tools = new[]
        {
            new ToolDefinition(RecallPerson,
                "Call to mind what is truly known of a person of the world — who they are, their kin and house, " +
                "their standing, and where word last placed them. Reach for this whenever a person is spoken of " +
                "and your memory of them is dim, rather than guessing.",
                new[] { new ToolParameter("name", "The person's name, as best you know it.") }),

            new ToolDefinition(RecallPlace,
                "Call to mind what is known of a town, castle, or village — who holds it, whose realm it lies " +
                "in, its walls and garrison, and how it fares. Reach for this when a place is spoken of and " +
                "your memory of it is dim, and always before speaking in numbers of its defenses.",
                new[] { new ToolParameter("name", "The place's name, as best you know it.") }),

            new ToolDefinition(RecallClan,
                "Call to mind what is known of a clan or noble house — who leads it, whom it serves, its people " +
                "and its holdings.",
                new[] { new ToolParameter("name", "The clan's name, as best you know it.") }),

            new ToolDefinition(RecallRealm,
                "Call to mind what is known of a realm or kingdom — who rules it, its great houses, its lands, " +
                "and the wars it wages.",
                new[] { new ToolParameter("name", "The realm's name, as best you know it.") }),
        };

        /// <summary>
        /// Answers one recall for the given NPC (the one whose mind is reaching). Called from LLM
        /// background threads; the actual lookup runs on the game thread.
        /// </summary>
        public static async Task<string> ResolveAsync(ToolCall call, Hero asker)
        {
            var name = ArgumentName(call);
            if (string.IsNullOrWhiteSpace(name)) return ToolLoopRunner.NothingSurfaces;

            var lookup = OnGameThread(() =>
            {
                switch (call.Name)
                {
                    case RecallPerson: return DescribePerson(name, asker);
                    case RecallPlace: return DescribePlace(name, asker);
                    case RecallClan: return DescribeClan(name);
                    case RecallRealm: return DescribeRealm(name);
                    default: return string.Empty;
                }
            });

            // The dispatcher drains every application tick; if the game somehow stalls the queue,
            // an honest blank beats a turn hung forever on a recall.
            var finished = await Task.WhenAny(lookup, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
            if (finished != lookup) return ToolLoopRunner.NothingSurfaces;

            var answer = await lookup.ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(answer) ? ToolLoopRunner.NothingSurfaces : answer;
        }

        private static string ArgumentName(ToolCall call)
        {
            try { return ((string?)JObject.Parse(call.ArgumentsJson)["name"] ?? string.Empty).Trim(); }
            catch { return string.Empty; }
        }

        private static Task<string> OnGameThread(Func<string> lookup)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            MainThreadDispatcher.Enqueue(() =>
            {
                try { tcs.TrySetResult(lookup()); }
                catch { tcs.TrySetResult(string.Empty); }
            });
            return tcs.Task;
        }

        // ------------------------------ people ------------------------------

        private static string DescribePerson(string name, Hero asker)
        {
            var matches = FindHeroes(name);
            if (matches.Count == 0)
                return $"Search your memory as you may, no one called \"{name}\" comes clearly to mind.";

            if (matches.Count == 1)
                return PersonRemembrance(matches[0], asker);

            // Several share the name: recall each briefly so the NPC can tell them apart in speech.
            var sb = new StringBuilder();
            sb.AppendLine($"More than one soul called {name} comes to mind:");
            foreach (var h in matches.Take(3))
                sb.AppendLine("- " + PersonBrief(h));
            return sb.ToString().TrimEnd();
        }

        private static List<Hero> FindHeroes(string name)
        {
            var all = new List<Hero>();
            Try(() => all.AddRange(Hero.AllAliveHeroes ?? Enumerable.Empty<Hero>()));
            Try(() => all.AddRange(Hero.DeadOrDisabledHeroes ?? Enumerable.Empty<Hero>()));

            List<Hero> Match(Func<Hero, bool> predicate) =>
                all.Where(h => h != null && Safe(() => predicate(h))).ToList();

            // Exact full name first, then exact first name, then a contains match — so "Rhagaea"
            // finds the empress before some cousin whose name merely contains the letters.
            var exact = Match(h => string.Equals(h.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase));
            if (exact.Count > 0) return exact;

            var first = Match(h => string.Equals(h.FirstName?.ToString(), name, StringComparison.OrdinalIgnoreCase));
            if (first.Count > 0) return first;

            return Match(h => (h.Name?.ToString() ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string PersonRemembrance(Hero h, Hero asker)
        {
            var lines = new List<string>();
            var name = h.Name?.ToString() ?? "That one";

            Try(() =>
            {
                var what = PersonRole(h);
                var culture = h.Culture?.Name?.ToString();
                var opening = $"{name} comes back to you: {what}";
                if (!string.IsNullOrWhiteSpace(culture)) opening += $", {culture} by blood";
                if (h.IsAlive) opening += $", of some {(int)h.Age} years";
                lines.Add(opening + ".");
            });

            if (!h.IsAlive)
                lines.Add("They have passed from this world.");

            Try(() =>
            {
                var clan = h.Clan;
                if (clan == null) return;
                var line = $"They belong to clan {clan.Name}";
                if (clan.Kingdom != null) line += $", sworn to {clan.Kingdom.Name}";
                if (clan.Leader == h) line += ", and lead it themselves";
                lines.Add(line + ".");
            });

            Try(() =>
            {
                var kin = new List<string>();
                if (h.Spouse != null) kin.Add($"wed to {h.Spouse.Name}" + (h.Spouse.IsAlive ? "" : " (now passed)"));
                if (h.Father != null) kin.Add($"child of {h.Father.Name}" + (h.Mother != null ? $" and {h.Mother.Name}" : ""));
                else if (h.Mother != null) kin.Add($"child of {h.Mother.Name}");
                var children = h.Children?.Where(c => c != null && c.IsAlive).Select(c => c.Name?.ToString()).Where(n => n != null).Take(6).ToList();
                if (children != null && children.Count > 0) kin.Add("parent to " + string.Join(", ", children));
                if (kin.Count > 0) lines.Add("Of their kin: " + string.Join("; ", kin) + ".");
            });

            if (h.IsAlive)
            {
                Try(() =>
                {
                    var where = Whereabouts(h);
                    if (!string.IsNullOrWhiteSpace(where)) lines.Add(where);
                });
            }

            Try(() =>
            {
                if (asker == null || asker == h) return;
                int standing = h.GetRelation(asker);
                if (standing != 0)
                    lines.Add($"Between you and them, the standing is {PersonaBuilder.DescribeRelation(standing)} ({standing}).");
            });

            return string.Join(" ", lines);
        }

        private static string PersonBrief(Hero h)
        {
            var parts = new List<string> { h.Name?.ToString() ?? "someone" };
            Try(() => { parts.Add(PersonRole(h)); });
            Try(() => { if (h.Clan != null) parts.Add($"of clan {h.Clan.Name}"); });
            if (!h.IsAlive) parts.Add("now passed");
            return string.Join(", ", parts);
        }

        private static string PersonRole(Hero h)
        {
            var she = h.IsFemale;
            if (Safe(() => h.MapFaction?.Leader == h && h.MapFaction is Kingdom))
                return she ? "ruler of her realm" : "ruler of his realm";
            if (Safe(() => h.Clan?.Leader == h)) return "head of a noble house";
            if (Safe(() => h.IsLord)) return she ? "a noblewoman" : "a nobleman";
            if (Safe(() => h.IsWanderer)) return "a wandering warrior who sells their sword";
            if (Safe(() => h.IsNotable)) return "a person of note among the common folk";
            return she ? "a woman of the world" : "a man of the world";
        }

        // "Last word places them ..." — where the world currently holds this hero. Softened as
        // hearsay: the recall is what would have reached the NPC's ears, not an all-seeing eye.
        private static string Whereabouts(Hero h)
        {
            var settlement = h.CurrentSettlement;
            if (settlement != null) return $"Last word places them at {settlement.Name}.";

            var party = h.PartyBelongedTo;
            if (party != null)
            {
                var near = NearestSettlementName(party);
                return near == null
                    ? "Last word has them on the road with their party."
                    : $"Last word has them on the road with their party, near {near}.";
            }

            return string.Empty;
        }

        private static string? NearestSettlementName(MobileParty party)
        {
            try
            {
                Settlement? best = null;
                float bestDist = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !(s.IsTown || s.IsCastle)) continue;
                    var d = s.Position.DistanceSquared(party.Position);
                    if (d < bestDist) { bestDist = d; best = s; }
                }
                return best?.Name?.ToString();
            }
            catch { return null; }
        }

        // ------------------------------ places ------------------------------

        private static string DescribePlace(string name, Hero asker)
        {
            var s = FindByName(Settlement.All, name, x => x.Name?.ToString());
            if (s == null)
                return $"No town, castle, or village called \"{name}\" comes to mind.";

            var lines = new List<string>();
            var kind = s.IsTown ? "a town" : s.IsCastle ? "a castle" : s.IsVillage ? "a village" : "a place";

            Try(() =>
            {
                var line = $"{s.Name} comes back to you: {kind}";
                var culture = s.Culture?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(culture)) line += $" of {culture} lands";
                lines.Add(line + ".");
            });

            Try(() =>
            {
                var owner = s.OwnerClan;
                if (owner == null) return;
                var line = $"It is held by clan {owner.Name}";
                if (owner.Leader != null) line += $" under {owner.Leader.Name}";
                if (owner.Kingdom != null) line += $", within the realm of {owner.Kingdom.Name}";
                lines.Add(line + ".");
            });

            Try(() =>
            {
                if (s.IsVillage && s.Village?.Bound != null)
                    lines.Add($"It lives in the shadow of {s.Village.Bound.Name}.");
            });

            Try(() => lines.AddRange(FortificationLedger(s, asker)));

            Try(() =>
            {
                if (s.IsUnderSiege) lines.Add("Word is that it lies under siege even now.");
            });

            return string.Join(" ", lines);
        }

        // The defenses and fortunes of a town or castle. An asker who truly knows the place — its
        // governor, someone within its walls, or one of the clan that holds it — recalls the ledger
        // exactly; anyone else gets what word of mouth would carry: walls and rough numbers, never
        // granary counts or the mood of streets they have not walked.
        private static IEnumerable<string> FortificationLedger(Settlement s, Hero asker)
        {
            var lines = new List<string>();
            if (!(s.IsTown || s.IsCastle)) return lines;
            var town = s.Town;
            if (town == null) return lines;

            bool intimate = asker != null && Safe(() =>
                town.Governor == asker || asker.CurrentSettlement == s || (asker.Clan != null && asker.Clan == s.OwnerClan));

            Try(() =>
            {
                var governor = town.Governor;
                if (governor == null) return;
                lines.Add(governor == asker
                    ? "Its keeping rests in your own hands — you are its governor."
                    : $"Its keeping rests in the hands of {governor.Name}, its governor.");
            });

            Try(() =>
            {
                int wall = town.GetWallLevel();
                if (wall > 0) lines.Add($"Its walls stand {WallWords(wall)}.");
            });

            Try(() =>
            {
                int garrison = town.GarrisonParty?.MemberRoster?.TotalManCount ?? 0;
                int militia = (int)s.Militia;
                if (intimate)
                    lines.Add($"Some {garrison} soldiers hold its garrison, and about {militia} militia stand among the folk.");
                else
                    lines.Add($"Last word puts its garrison near {Loose(garrison)} soldiers, with perhaps {Loose(militia)} militia among the folk.");
            });

            if (intimate)
            {
                Try(() =>
                {
                    int food = (int)town.FoodStocks;
                    float change = town.FoodChange;
                    var trend = change > 0.5f ? "and they grow day by day"
                              : change < -0.5f ? "and they dwindle day by day"
                              : "holding steady";
                    lines.Add($"The granaries hold stores near {food}, {trend}.");
                });

                if (s.IsTown)
                {
                    Try(() => lines.Add(
                        $"Of its fortunes: prosperity near {(int)town.Prosperity}, the folk's loyalty near {(int)town.Loyalty}, and order in its streets near {(int)town.Security}."));
                }
            }

            return lines;
        }

        private static string WallWords(int level)
        {
            switch (level)
            {
                case 1: return "modest, at the first of three raisings";
                case 2: return "strong, at the second of three raisings";
                default: return "mighty, at their full third raising";
            }
        }

        // Rounds a count to what hearsay would carry: nothing finer than tens, nothing above a
        // hundred finer than fifties.
        private static int Loose(int n)
        {
            if (n >= 100) return (int)Math.Round(n / 50.0) * 50;
            return (int)Math.Round(n / 10.0) * 10;
        }

        // ------------------------------ clans ------------------------------

        private static string DescribeClan(string name)
        {
            var clan = FindByName(Clan.All?.Where(c => c != null && !c.IsBanditFaction), name, x => x.Name?.ToString());
            if (clan == null)
                return $"No clan called \"{name}\" comes to mind.";

            var lines = new List<string>();

            Try(() =>
            {
                var line = $"Clan {clan.Name} comes back to you";
                if (clan.Leader != null) line += $", led by {clan.Leader.Name}";
                if (clan.Kingdom != null) line += $", sworn to {clan.Kingdom.Name}";
                lines.Add(line + ".");
            });

            Try(() =>
            {
                var fiefs = clan.Fiefs?.Select(f => f?.Name?.ToString()).Where(n => !string.IsNullOrWhiteSpace(n)).Take(6).ToList();
                if (fiefs != null && fiefs.Count > 0)
                    lines.Add("Its holdings include " + string.Join(", ", fiefs) + ".");
            });

            Try(() =>
            {
                var members = clan.Heroes?
                    .Where(h => h != null && h.IsAlive && h != clan.Leader)
                    .Select(h => h.Name?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .Take(8)
                    .ToList();
                if (members != null && members.Count > 0)
                    lines.Add("Among its people are " + string.Join(", ", members) + ".");
            });

            return string.Join(" ", lines);
        }

        // ------------------------------ realms ------------------------------

        private static string DescribeRealm(string name)
        {
            var kingdom = FindByName(Kingdom.All, name, x => x.Name?.ToString());
            if (kingdom == null)
                return $"No realm called \"{name}\" comes to mind.";

            var lines = new List<string>();

            Try(() =>
            {
                var line = $"The realm of {kingdom.Name} comes back to you";
                if (kingdom.Leader != null) line += $", ruled by {kingdom.Leader.Name}";
                var culture = kingdom.Culture?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(culture)) line += $", {culture} in blood and custom";
                lines.Add(line + ".");
            });

            Try(() =>
            {
                var clans = kingdom.Clans?
                    .Where(c => c != null)
                    .Select(c => c.Name?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Take(8)
                    .ToList();
                if (clans != null && clans.Count > 0)
                    lines.Add($"Its great houses number {kingdom.Clans?.Count ?? clans.Count}, among them " + string.Join(", ", clans) + ".");
            });

            Try(() =>
            {
                var wars = Kingdom.All?
                    .Where(k => k != null && k != kingdom && Safe(() => FactionManager.IsAtWarAgainstFaction(kingdom, k)))
                    .Select(k => k.Name?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                lines.Add(wars == null || wars.Count == 0
                    ? "It is at peace, for now."
                    : "It wages war against " + string.Join(", ", wars) + ".");
            });

            return string.Join(" ", lines);
        }

        // ------------------------------ helpers ------------------------------

        private static T? FindByName<T>(IEnumerable<T>? source, string name, Func<T, string?> nameOf) where T : class
        {
            try
            {
                var all = (source ?? Enumerable.Empty<T>()).Where(x => x != null).ToList();
                return all.FirstOrDefault(x => string.Equals(nameOf(x), name, StringComparison.OrdinalIgnoreCase))
                    ?? all.FirstOrDefault(x => (nameOf(x) ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch { return null; }
        }

        private static void Try(Action a) { try { a(); } catch { /* skip this fact */ } }

        private static bool Safe(Func<bool> f) { try { return f(); } catch { return false; } }
    }
}
