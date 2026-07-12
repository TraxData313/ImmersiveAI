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
using TaleWorlds.Core;

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
        public const string RecallCompany = "recall_company";
        public const string RecallTroop = "recall_troop";
        public const string RecallMarket = "recall_market";

        public static readonly IReadOnlyList<ToolDefinition> Tools = new[]
        {
            new ToolDefinition(RecallPerson,
                "Call to mind what is truly known of a person of the world — who they are, their kin and house, " +
                "their standing, where word last placed them, and, if they stand before your eyes, what you see " +
                "of their garb and arms. Reach for this whenever a person is spoken of and your memory of them " +
                "is dim, rather than guessing.",
                new[] { new ToolParameter("name", "The person's name, as best you know it.") }),

            new ToolDefinition(RecallCompany,
                "Take stock of your own company — the warband you lead or ride with: how many souls it counts, " +
                "the kinds of fighters among them, the hale and the wounded, prisoners in your train, the food " +
                "in the wagons, the men's spirits, their wages, and what the company is presently about. Reach " +
                "for this before ever speaking in numbers of your own men."),

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

            new ToolDefinition(RecallTroop,
                "Call to mind what is known of a kind of soldier — recruit, warrior, knight, of any people: " +
                "how seasoned they are, their skill at arms, the gear they carry, and what they may become " +
                "with training. Reach for this when soldiers or their worth are spoken of — and when weighing " +
                "one kind against another, call each to mind in turn before you judge.",
                new[] { new ToolParameter("name", "The soldier kind's name, e.g. \"Vlandian Recruit\" or \"Battanian Fian\".") }),

            new ToolDefinition(RecallMarket,
                "Call to mind the day's trade in the market about you — what goods truly fetch here, this " +
                "day, in the place where you stand. Reach for this before ever quoting a price or speaking " +
                "of what the market bears; prices shift with the seasons and the wars, and yesterday's " +
                "figure is a lie by morning.",
                new[] { new ToolParameter("item", "One good to price — grain, tools, wine, a horse. Leave it out to survey the market's staples.", required: false) }),
        };

        /// <summary>
        /// Answers one recall for the given NPC (the one whose mind is reaching). Called from LLM
        /// background threads; the actual lookup runs on the game thread.
        /// </summary>
        public static async Task<string> ResolveAsync(ToolCall call, Hero asker)
        {
            // The company recall reaches inward and the market recall reads the place itself —
            // neither needs a name to look up.
            var name = ArgumentName(call);
            if (call.Name != RecallCompany && call.Name != RecallMarket && string.IsNullOrWhiteSpace(name))
                return ToolLoopRunner.NothingSurfaces;

            var lookup = OnGameThread(() =>
            {
                switch (call.Name)
                {
                    case RecallPerson: return DescribePerson(name, asker);
                    case RecallPlace: return DescribePlace(name, asker);
                    case RecallClan: return DescribeClan(name);
                    case RecallRealm: return DescribeRealm(name);
                    case RecallCompany: return DescribeCompany(asker);
                    case RecallTroop: return DescribeTroop(name);
                    case RecallMarket: return DescribeMarket(name, asker);
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
            try
            {
                var args = JObject.Parse(call.ArgumentsJson);
                return ((string?)args["name"] ?? (string?)args["item"] ?? string.Empty).Trim();
            }
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

            // Several share the name. The heart knows which one is meant: kin before comrades,
            // comrades before neighbors, the one who stands here before a stranger far away —
            // so a wife asked about "Vulgrim" recalls HER Vulgrim, not some name-twin across the map.
            var ranked = matches
                .OrderByDescending(h => ClosenessTo(h, asker))
                .ToList();
            int best = ClosenessTo(ranked[0], asker);
            if (best > 0 && (ranked.Count < 2 || ClosenessTo(ranked[1], asker) < best))
            {
                var answer = new StringBuilder(PersonRemembrance(ranked[0], asker));
                var others = ranked.Skip(1).Take(2).Select(PersonBrief).ToList();
                if (others.Count > 0)
                    answer.Append(" (Others in the world also bear the name: " + string.Join("; ", others) + ".)");
                return answer.ToString();
            }

            // No one of them is close to the asker: recall each briefly so they can be told apart.
            var sb = new StringBuilder();
            sb.AppendLine($"More than one soul called {name} comes to mind:");
            foreach (var h in ranked.Take(3))
                sb.AppendLine("- " + PersonBrief(h));
            return sb.ToString().TrimEnd();
        }

        // How near this person stands to the asker's own life — used to pick the meant one among
        // name-twins. Kin outweigh comrades, comrades outweigh neighbors, the person standing here
        // outweighs a stranger across the map.
        private static int ClosenessTo(Hero h, Hero asker)
        {
            if (asker == null || h == null) return 0;
            if (Safe(() => h.Spouse == asker || asker.Spouse == h
                || h.Father == asker || h.Mother == asker || asker.Father == h || asker.Mother == h)) return 5;
            if (Safe(() => h.PartyBelongedTo != null && h.PartyBelongedTo == asker.PartyBelongedTo)) return 4;
            if (Safe(() => h.CurrentSettlement != null && h.CurrentSettlement == asker.CurrentSettlement)) return 3;
            if (Safe(() => h == Hero.MainHero)) return 2;
            if (Safe(() => h.Clan != null && h.Clan == asker.Clan)) return 1;
            return 0;
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

            // What they are honestly good at — the crafts a captain weighs before hiring: would this
            // one make a fine scout, a passable surgeon? Word travels of such things.
            Try(() =>
            {
                if (!h.IsAlive) return;
                int ValueOf(SkillObject s) { try { return h.GetSkillValue(s); } catch { return 0; } }
                var crafts = TaleWorlds.CampaignSystem.Extensions.Skills.All?
                    .Where(s => s != null)
                    .Select(s => new { Name = s.Name?.ToString(), Value = ValueOf(s) })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.Value >= 60)
                    .OrderByDescending(x => x.Value)
                    .Take(4)
                    .Select(x => $"{x.Name} ({Personas.CraftsBuilder.WordForValue(x.Value)})")
                    .ToList();
                if (crafts != null && crafts.Count > 0)
                    lines.Add($"Their crafts, as word has it: strongest in {string.Join(", ", crafts)}.");
            });

            // If they truly stand where the asker can see them, the eyes add what hearsay cannot.
            Try(() =>
            {
                if (asker == null || asker == h || !h.IsAlive) return;
                bool together = Safe(() =>
                    (h.CurrentSettlement != null && h.CurrentSettlement == asker.CurrentSettlement) ||
                    (h.PartyBelongedTo != null && h.PartyBelongedTo == asker.PartyBelongedTo));
                if (!together) return;
                var sight = AttireBrief(h, civilian: Safe(() => h.CurrentSettlement != null));
                if (sight.Length > 0) lines.Add(sight);
            });

            return string.Join(" ", lines);
        }

        // What the eyes see of someone present: garb and arms, from the equipment they would truly
        // be wearing here (civilian garments within walls, war gear on the road).
        private static string AttireBrief(Hero h, bool civilian)
        {
            var eq = civilian ? h.CivilianEquipment : h.BattleEquipment;
            if (eq == null) return string.Empty;

            string ItemAt(EquipmentIndex i)
            {
                try { return eq[i].Item?.Name?.ToString(); } catch { return null; }
            }

            var garb = new[] { EquipmentIndex.Body, EquipmentIndex.Head, EquipmentIndex.Cape, EquipmentIndex.Gloves, EquipmentIndex.Leg }
                .Select(ItemAt).Where(n => !string.IsNullOrWhiteSpace(n)).Take(4).ToList();
            var arms = new[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 }
                .Select(ItemAt).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

            if (garb.Count == 0 && arms.Count == 0) return string.Empty;
            var clad = garb.Count > 0 ? "clad in " + string.Join(", ", garb) : "clad plainly";
            var bearing = arms.Count > 0 ? "bearing " + string.Join(", ", arms) : "bearing no arms you can see";
            return $"And they stand before your very eyes, {clad}, {bearing}.";
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
            // The player's young clan is no noble house until it truly is: sworn to a crown makes a
            // noble, a contract makes a sellsword captain, unsworn makes a free captain or adventurer.
            if (Safe(() => h == Hero.MainHero && h.Clan != null && h.Clan.Kingdom == null))
                return Safe(() => (h.PartyBelongedTo?.MemberRoster?.TotalManCount ?? 0) >= 15)
                    ? "a free captain, sworn to no crown"
                    : "a free adventurer, sworn to no crown";
            if (Safe(() => h == Hero.MainHero && h.Clan?.IsUnderMercenaryService == true))
                return "a sellsword captain under contract";
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

            // A village is known by what it sends to market — its true production, not a guess
            // (the Cadugan playtest find: an iron village must never be recalled as farmland).
            Try(() =>
            {
                var type = s.Village?.VillageType;
                var primary = type?.PrimaryProduction?.Name?.ToString();
                if (string.IsNullOrWhiteSpace(primary)) return;
                var others = new List<string>();
                foreach (var (item, _) in type.Productions)
                {
                    var n = item?.Name?.ToString();
                    if (string.IsNullOrWhiteSpace(n) || n == primary) continue;
                    if (!others.Contains(n)) others.Add(n);
                    if (others.Count == 2) break;
                }
                var line = $"Its livelihood is {primary.ToLowerInvariant()}";
                if (others.Count == 1) line += $", beside some {others[0].ToLowerInvariant()}";
                else if (others.Count == 2) line += $", beside some {others[0].ToLowerInvariant()} and {others[1].ToLowerInvariant()}";
                lines.Add(line + ".");
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
                int renown = (int)clan.Renown;
                var word = renown >= 900 ? "famed across all Calradia"
                         : renown >= 300 ? "well known, and spoken of with respect"
                         : renown >= 50 ? "known in its own lands"
                         : "little known beyond its own hearth";
                lines.Add($"Its name is {word} (renown near {renown}).");
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

        // ------------------------------ kinds of soldier ------------------------------

        // A soldier kind, recalled as a captain who has seen them fight would recall them: seasoning,
        // craft, gear, and what training may make of them. This is what lets an NPC honestly weigh
        // "which recruit is better" instead of guessing.
        private static string DescribeTroop(string name)
        {
            var troops = new List<CharacterObject>();
            Try(() => troops.AddRange((CharacterObject.All ?? Enumerable.Empty<CharacterObject>())
                .Where(c => c != null && !c.IsHero && Safe(() =>
                    c.Occupation == Occupation.Soldier || c.Occupation == Occupation.Mercenary || c.Occupation == Occupation.Bandit))));

            var matches = troops.Where(c => string.Equals(c.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
                matches = troops.Where(c => (c.Name?.ToString() ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (matches.Count == 0)
            {
                // The name misses, but a people may be named in it ("Battanian recruit" — Battania
                // musters Volunteers, not Recruits): offer that people's kinds so the asker can call
                // the right one to mind instead of shrugging.
                var kindsOfPeople = TroopKindsOfNamedCulture(troops, name);
                return kindsOfPeople != null
                    ? $"No kind of soldier called \"{name}\" comes to mind — that people names their fighters otherwise. Of them you know these kinds: {kindsOfPeople}. Call the one you mean to mind."
                    : $"No kind of soldier called \"{name}\" comes to mind.";
            }
            if (matches.Count > 1)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"More than one kind of soldier answers to \"{name}\" — call the one you mean to mind by its fuller name:");
                foreach (var c in matches.Take(6))
                    sb.AppendLine("- " + c.Name);
                return sb.ToString().TrimEnd();
            }

            var t = matches[0];
            var lines = new List<string>();

            Try(() =>
            {
                var opening = $"The {t.Name} comes to mind";
                var culture = t.Culture?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(culture)) opening += $": a soldier of the {culture} people";
                opening += $", of the {Ordinal(t.Tier)} rank of seasoning";
                var manner = t.IsMounted && t.IsRanged ? "they fight ahorse with missile arms"
                           : t.IsMounted ? "they fight from horseback"
                           : t.IsRanged ? "they fight at range"
                           : "they fight on foot, blade to blade";
                lines.Add(opening + $" — {manner}.");
            });

            Try(() =>
            {
                int ValueOf(SkillObject s) { try { return t.GetSkillValue(s); } catch { return 0; } }
                var crafts = TaleWorlds.CampaignSystem.Extensions.Skills.All?
                    .Where(s => s != null)
                    .Select(s => new { Skill = s, Value = ValueOf(s) })
                    .Where(x => x.Value > 20)
                    .OrderByDescending(x => x.Value)
                    .Take(5)
                    .Select(x => $"{x.Skill.Name} near {x.Value}")
                    .ToList();
                if (crafts != null && crafts.Count > 0)
                    lines.Add("Their craft: " + string.Join(", ", crafts) + ".");
            });

            Try(() =>
            {
                var eq = t.FirstBattleEquipment;
                if (eq == null) return;
                string ItemAt(EquipmentIndex i) { try { return eq[i].Item?.Name?.ToString(); } catch { return null; } }
                var arms = new[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 }
                    .Select(ItemAt).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
                var garb = new[] { EquipmentIndex.Body, EquipmentIndex.Head }
                    .Select(ItemAt).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                if (arms.Count > 0) lines.Add("They carry " + string.Join(", ", arms) + ".");
                if (garb.Count > 0) lines.Add("They wear " + string.Join(" and ", garb) + $" (armor about the body near {(int)t.GetBodyArmorSum()}).");
            });

            Try(() =>
            {
                var next = t.UpgradeTargets?
                    .Where(u => u != null)
                    .Select(u => u.Name?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                if (next != null && next.Count > 0)
                    lines.Add("With seasoning they may become " + string.Join(" or ", next) + ".");
                else
                    lines.Add("They stand at the end of their road; training will make nothing further of them.");
            });

            return string.Join(" ", lines);
        }

        // If the failed troop query names a people ("Battanian…", "of Vlandia…"), lists that
        // people's soldier kinds, greenest first, so the asker can re-ask by the right name.
        private static string TroopKindsOfNamedCulture(List<CharacterObject> troops, string query)
        {
            try
            {
                var byCulture = troops
                    .Where(c => c.Culture?.Name != null)
                    .GroupBy(c => c.Culture.Name.ToString())
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    // "Battanian recruit" contains "Battania", "Vlandian" contains "Vlandia" — the
                    // adjective always carries the people's name inside it.
                    .FirstOrDefault(g => query.IndexOf(g.Key, StringComparison.OrdinalIgnoreCase) >= 0);
                if (byCulture == null) return null;

                var kinds = byCulture
                    .Where(c => Safe(() => c.Occupation == Occupation.Soldier))
                    .OrderBy(c => Safe(() => c.Tier > 0) ? c.Tier : 99)
                    .Select(c => c.Name?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .Take(10)
                    .ToList();
                return kinds.Count > 0 ? string.Join(", ", kinds) : null;
            }
            catch { return null; }
        }

        private static string Ordinal(int n)
        {
            switch (n)
            {
                case 1: return "first";
                case 2: return "second";
                case 3: return "third";
                case 4: return "fourth";
                case 5: return "fifth";
                case 6: return "sixth";
                default: return n + "th";
            }
        }

        // ------------------------------ the market about you ------------------------------

        // The day's trade where the asker stands, read from the settlement's real market — so a
        // headman quotes what grain truly fetches in his village today instead of inventing a
        // plausible number. One named good, or a survey of the staples.
        private static string DescribeMarket(string itemName, Hero asker)
        {
            Settlement? s = null;
            Try(() => s = asker?.CurrentSettlement ?? Settlement.CurrentSettlement);
            if (s == null)
                return "You stand in no market — out here there are no stalls, no scales, and no day's prices to call to mind.";

            // Towns, castles, and villages all keep their own ledger of what a thing trades at.
            // Priced against the player's party, so the figures match the trade screen to the denar
            // (the bare price is the market's midpoint; the buy/sell margin lives in the party terms).
            Func<ItemObject, bool, int>? priceOf = null;
            Try(() =>
            {
                var buyer = MobileParty.MainParty;
                if (s.Town != null) priceOf = (item, selling) => s.Town.GetItemPrice(item, buyer, selling);
                else if (s.Village != null) priceOf = (item, selling) => s.Village.GetItemPrice(item, buyer, selling);
            });
            if (priceOf == null)
                return $"The trade of {s.Name} does not come to mind clearly.";

            if (!string.IsNullOrWhiteSpace(itemName))
            {
                ItemObject? item = null;
                Try(() =>
                {
                    var all = TaleWorlds.CampaignSystem.Extensions.Items.All?.Where(i => i != null).ToList();
                    if (all == null) return;
                    item = all.FirstOrDefault(i => string.Equals(i.Name?.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
                        ?? all.FirstOrDefault(i => (i.Name?.ToString() ?? "").IndexOf(itemName, StringComparison.OrdinalIgnoreCase) >= 0);
                });
                if (item == null)
                    return $"No good called \"{itemName}\" comes to mind among the wares of the world.";

                try
                {
                    int buy = priceOf(item, false);
                    int sell = priceOf(item, true);
                    return $"This day in {s.Name}, {item.Name} trades near {buy} denars" +
                           (sell > 0 && sell != buy ? $" (one brought in to sell would fetch closer to {sell})." : ".");
                }
                catch { return $"The price of {item.Name} here does not come to mind."; }
            }

            // No good named: survey the staples of any market.
            var lines = new List<string>();
            Try(() =>
            {
                var staples = new[] { "grain", "fish", "meat", "butter", "cheese", "olives", "grape", "wine", "beer",
                                      "wool", "linen", "hides", "leather", "iron", "hardwood", "tools", "pottery",
                                      "salt", "oil", "clay", "fur", "date_fruit", "flax" };
                var priced = TaleWorlds.CampaignSystem.Extensions.Items.AllTradeGoods?
                    .Where(i => i != null && staples.Contains(i.StringId))
                    .Select(i =>
                    {
                        try { return $"{i.Name} near {priceOf(i, false)}"; }
                        catch { return null; }
                    })
                    .Where(p => p != null)
                    .Take(12)
                    .ToList();
                if (priced != null && priced.Count > 0)
                    lines.Add($"The day's trade in {s.Name} comes to mind — what the stalls ask, in denars: " +
                              string.Join(", ", priced) + ".");
            });

            return lines.Count > 0 ? string.Join(" ", lines)
                : $"The stalls of {s.Name} do not come clearly to mind this day.";
        }

        // ------------------------------ one's own company ------------------------------

        // The asker taking stock of their own warband. Their own command is known to them exactly —
        // no hearsay rounding here; a captain knows his muster roll, his wagons, and his purse.
        private static string DescribeCompany(Hero asker)
        {
            if (asker == null) return ToolLoopRunner.NothingSurfaces;

            MobileParty party = null;
            Try(() => party = asker.PartyBelongedTo);
            if (party == null)
            {
                string kept = null;
                Try(() => kept = asker.GovernorOf?.Settlement?.Name?.ToString());
                return kept != null
                    ? $"No warband rides under you upon the road — your charge is {kept} itself, and its garrison comes to mind when you think upon the place."
                    : "You take stock, and the truth is plain: no company rides with you now — no warband of your own, and none you march among.";
            }

            var lines = new List<string>();
            Hero leader = null;
            Try(() => leader = party.LeaderHero);
            bool leading = leader == asker;

            Try(() =>
            {
                int total = party.MemberRoster?.TotalManCount ?? 0;
                int wounded = party.MemberRoster?.TotalWounded ?? 0;
                var woundedNote = wounded > 0 ? $", {wounded} of them nursing wounds" : "";
                if (leading)
                    lines.Add($"Your company comes to mind as clearly as your own hand: {total} souls ride under your command{woundedNote}.");
                else if (leader != null)
                    lines.Add($"You take stock of {leader.Name}'s company, which you ride with: {total} souls in all{woundedNote}.");
                else
                    lines.Add($"You take stock of the company you march among: {total} souls in all{woundedNote}.");
            });

            // The named few first, then the ranks by kind — a captain reads his roll this way.
            Try(() =>
            {
                var roster = party.MemberRoster?.GetTroopRoster();
                if (roster == null) return;

                var companions = roster
                    .Where(e => Safe(() => e.Character != null && e.Character.IsHero
                        && e.Character.HeroObject != asker && e.Character.HeroObject != leader))
                    .Select(e => e.Character.HeroObject?.Name?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n)).Take(8).ToList();
                if (companions.Count > 0)
                    lines.Add("At your side ride " + string.Join(", ", companions) + ".");

                var kinds = roster
                    .Where(e => Safe(() => e.Character != null && !e.Character.IsHero && e.Number > 0))
                    .OrderByDescending(e => e.Number)
                    .Select(e => $"{e.Number} {e.Character.Name}")
                    .ToList();
                if (kinds.Count > 0)
                {
                    var shown = string.Join(", ", kinds.Take(8));
                    lines.Add(kinds.Count > 8
                        ? $"Among them: {shown}, and others besides."
                        : $"Among them: {shown}.");
                }
            });

            Try(() =>
            {
                int prisoners = party.PrisonRoster?.TotalManCount ?? 0;
                if (prisoners > 0) lines.Add($"Some {prisoners} prisoners are marched along in your train.");
            });

            Try(() =>
            {
                float food = party.Food;
                float change = party.FoodChange;
                if (food <= 0f) lines.Add("The wagons are all but empty — hunger walks close to the company.");
                else if (change < 0f) lines.Add($"The wagons hold food for some {(int)Math.Floor(food / -change)} days more.");
                else lines.Add("The wagons want for nothing; the stores hold and grow.");
            });

            Try(() =>
            {
                int morale = (int)party.Morale;
                var word = morale >= 70 ? "high" : morale >= 50 ? "steady" : morale >= 30 ? "low" : "near breaking";
                lines.Add($"The men's spirits stand {word} ({morale} of a hundred).");
            });

            // How the hurt mend — the surgeon's ledger, read from the game's own healing model, so a
            // surgeon asked "how fast will they heal?" answers with the true rates.
            Try(() =>
            {
                int wounded = party.MemberRoster?.TotalWounded ?? 0;
                var model = Campaign.Current?.Models?.PartyHealingModel;
                if (model == null) return;
                int named = (int)model.GetDailyHealingHpForHeroes(party.Party, false).ResultNumber;
                int ranks = (int)model.GetDailyHealingForRegulars(party.Party, false).ResultNumber;
                var surgeon = party.EffectiveSurgeon;
                var care = surgeon == null ? "with no surgeon named to the charge"
                    : surgeon == asker ? "under your own care"
                    : $"under {surgeon.Name}'s care";
                if (wounded > 0)
                    lines.Add($"The hurt mend {care}: the named heal some {named} points of vigor a day, the ranks some {ranks}.");
            });

            // The captain carries the ledger of coin — and the quartermaster keeps its books
            // (the Brunda find, 2026.07.12: asked of the war chest, she rightly could not see it).
            Try(() =>
            {
                if (leading)
                {
                    lines.Add($"Their keep runs some {party.TotalWage} denars a day in wages, and your own purse holds {asker.Gold}.");
                    return;
                }
                if (leader != null && Safe(() => party.EffectiveQuartermaster == asker))
                    lines.Add($"You keep the books yourself: the keep runs some {party.TotalWage} denars a day in wages, and the war chest — {leader.Name}'s purse — holds some {leader.Gold} denars.");
            });

            Try(() => lines.Add(CompanyDoing(party)));

            Try(() =>
            {
                var army = party.Army;
                if (army == null) return;
                var armyName = army.Name?.ToString() ?? "a gathered army";
                if (army.LeaderParty == party)
                    lines.Add($"And more than that: the banners of {armyName} — {army.TotalManCount} men in all — march at your word.");
                else
                {
                    var armyLeader = army.LeaderParty?.LeaderHero?.Name?.ToString();
                    lines.Add(armyLeader != null
                        ? $"The company marches within {armyName}, {army.TotalManCount} men in all, under {armyLeader}."
                        : $"The company marches within {armyName}, {army.TotalManCount} men in all.");
                }
            });

            var answer = string.Join(" ", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
            return answer.Length > 0 ? answer : ToolLoopRunner.NothingSurfaces;
        }

        // What the company is presently about, read from its errand on the map.
        private static string CompanyDoing(MobileParty party)
        {
            if (party.MapEvent != null)
                return party.MapEvent.IsRaid
                    ? "Even now the company has its hands in a raid."
                    : "Even now the company stands in the press of battle.";

            var besieged = party.BesiegedSettlement;
            if (besieged != null) return $"The company lies encamped in siege about {besieged.Name}.";

            var place = party.TargetSettlement?.Name?.ToString();
            var quarry = party.TargetParty?.Name?.ToString();
            switch (party.DefaultBehavior)
            {
                case AiBehavior.GoToSettlement: return place != null ? $"The company is bound for {place}." : "The company is on the road to a settlement.";
                case AiBehavior.RaidSettlement:
                case AiBehavior.AssaultSettlement: return place != null ? $"The company is set upon {place}, and not kindly." : "The company moves against a settlement.";
                case AiBehavior.BesiegeSettlement: return place != null ? $"The company moves to lay siege to {place}." : "The company moves to lay a siege.";
                case AiBehavior.EngageParty:
                case AiBehavior.GoAroundParty: return quarry != null ? $"The company moves against {quarry}." : "The company moves against an enemy in the field.";
                case AiBehavior.EscortParty: return quarry != null ? $"The company rides escort to {quarry}." : "The company rides escort.";
                case AiBehavior.PatrolAroundPoint: return place != null ? $"The company patrols the country around {place}." : "The company is on patrol.";
                case AiBehavior.DefendSettlement: return place != null ? $"The company is sworn to the defense of {place}." : "The company moves to a defense.";
                case AiBehavior.FleeToPoint:
                case AiBehavior.FleeToParty:
                case AiBehavior.FleeToGate: return "The company is withdrawing from a danger it does not care to meet.";
                default: return string.Empty;
            }
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
