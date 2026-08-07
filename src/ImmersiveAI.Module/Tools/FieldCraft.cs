using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Personas;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// The field-craft of a company on the march — two tools offered only to NPCs who ride with a
    /// party on the map (see CompleteSpokenAsync): the outward eyes (<see cref="SurveySurroundings"/>,
    /// who moves in the country about, how strong, how swift, whether flight would serve) and the
    /// scales of battle (<see cref="WeighBattle"/>, our strength against a named foe or a walled
    /// place). This is what makes a scout truly a scout and a quartermaster truly a quartermaster:
    /// asked "can we outrun them?" or "could we take that castle?", they look at the real map and
    /// the real rosters instead of inventing an answer.
    ///
    /// A green judge answers less surely than a master: counts are coarsened by the asker's own
    /// Scouting (the survey) or Tactics (the weighing), so skill honestly matters. Resolution runs
    /// on the game thread, same rails as WorldRecall.
    /// </summary>
    public static class FieldCraft
    {
        public const string SurveySurroundings = "survey_surroundings";
        public const string WeighBattle = "weigh_battle";

        public static readonly IReadOnlyList<ToolDefinition> Tools = new[]
        {
            new ToolDefinition(SurveySurroundings,
                "Cast my eyes over the country about my company — every band, caravan, and army moving " +
                "within sight: whose they are, which way they lie, their strength as well as my eyes can " +
                "count it, whether they are friend or foe, what they are about, and whether I or they are " +
                "the swifter. Also the villages, towns and castles within sight and how they fare — whether " +
                "one burns under a raid, lies under siege, or was lately plundered — any den of brigands my " +
                "company has spotted nearby, and my own company's pace and what slows it. Reach for this " +
                "before ever speaking of who or what is near, of a place that burns or is beset, of hideouts " +
                "and lairs, of pursuit, of escape, or of the speed of the march."),

            new ToolDefinition(WeighBattle,
                "Set a foe upon the scales against my own company: their numbers and kinds of fighters " +
                "against mine, and how the day would likely go. Works against a band or army moving in the " +
                "country, against the garrison of a named town or castle, against a village and whoever is " +
                "putting it to the torch, or against a spotted den of brigands. Reach for this before ever " +
                "counselling battle or retreat.",
                new[] { new ToolParameter("name",
                    "Who to weigh against: a war party or army by its leader's or its own name, or a town, " +
                    "castle, village, or brigands' den by name. Leave it out to weigh the nearest hostile " +
                    "band in sight.", required: false) }),
        };

        /// <summary>Answers one field-craft call for the asker. Same dispatcher rails as WorldRecall:
        /// game-thread lookup, honest blank on timeout.</summary>
        public static async Task<string> ResolveAsync(ToolCall call, Hero asker)
        {
            var name = ArgumentName(call);
            var lookup = OnGameThread(() =>
            {
                switch (call.Name)
                {
                    case SurveySurroundings: return Survey(asker);
                    case WeighBattle: return Weigh(name, asker);
                    default: return string.Empty;
                }
            });

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
                return ((string?)args["name"] ?? string.Empty).Trim();
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

        // ------------------------------ the outward eyes ------------------------------

        private static string Survey(Hero asker)
        {
            var party = asker?.PartyBelongedTo;
            if (party == null)
                return "I have no company upon the map to look out from — the country about is another's to watch.";

            var lines = new List<string>();
            int eyes = CraftsBuilder.ValueFor(asker, DefaultSkills.Scouting);
            float range = Math.Max(10f, Safe(() => party.SeeingRange * 1.5f, 30f));

            // Our own pace first, with what truly slows it — the scout's answer to "can we go faster?".
            Try(() =>
            {
                float speed = party.Speed;
                lines.Add($"My company moves at a pace of {speed:0.0} upon the map.");
                var drags = party.SpeedExplained.GetLines()?
                    .Where(l => l.number < -0.005f && !string.IsNullOrWhiteSpace(l.name))
                    .OrderBy(l => l.number)
                    .Select(l => Plain(l.name))
                    .Where(n => n.Length > 0)
                    .Distinct()
                    .Take(4)
                    .ToList();
                if (drags != null && drags.Count > 0)
                    lines.Add("What weighs on the pace: " + string.Join(", ", drags).ToLowerInvariant() + ".");
            });

            // Every band moving within sight, nearest first. A green eye sees fewer and counts rounder.
            // A band standing at a settlement counts as seen only when it is FIGHTING there — the
            // company sacking the village down the road is the one thing a scout must never miss
            // (2026.07.27); a lord merely resting inside walls is recall's business, not the eyes'.
            bool sawAnyone = false;
            Try(() =>
            {
                var inRange = MobileParty.All
                    .Where(p => p != null && p != party)
                    .Where(p => Safe(() => p.IsActive && !p.IsGarrison))
                    .Where(p => Safe(() => p.CurrentSettlement == null) || Safe(() => p.MapEvent != null))
                    .Select(p => new { Party = p, Dist = SafeDistance(p, party) })
                    .Where(x => x.Dist >= 0 && x.Dist <= range)
                    .OrderBy(x => x.Dist)
                    .ToList();
                if (inRange.Count == 0) return;

                // Nearest first, but a band with its hands in a raid or a siege is never crowded out
                // by three villagers' carts that happen to stand closer.
                int room = eyes >= 75 ? 8 : 5;
                var seen = inRange.Take(room).ToList();
                foreach (var x in inRange.Skip(room))
                {
                    if (seen.Count >= room + 3) break;
                    if (Safe(() => x.Party.MapEvent != null || x.Party.BesiegedSettlement != null)) seen.Add(x);
                }
                seen = seen.OrderBy(x => x.Dist).ToList();

                sawAnyone = true;
                lines.Add("Moving in the country about, nearest first:");
                foreach (var x in seen)
                    lines.Add("- " + BandBrief(x.Party, x.Dist, party, asker, eyes));
                if (inRange.Count > seen.Count)
                    lines.Add($"(And {inRange.Count - seen.Count} other bands besides, further off than these.)");
            });

            // The places themselves — the villages, towns and castles the eyes can reach, and above
            // all how they FARE: one burning under a raid, one beset by siege, one lately plundered.
            // Without this the scout could count brigands while a village burned in plain sight
            // (2026.07.27 playtest: "do you see those raiders burning the village north of us?").
            bool sawPlaces = false;
            Try(() =>
            {
                var near = Settlement.All
                    .Where(s => s != null && (s.IsVillage || s.IsTown || s.IsCastle))
                    .Select(s => new { Place = s, Dist = SafeSettlementDistance(s, party) })
                    .Where(x => x.Dist >= 0 && x.Dist <= range)
                    .OrderBy(x => x.Dist)
                    .ToList();
                if (near.Count == 0) return;

                var told = near.Take(4).ToList();
                foreach (var x in near.Skip(4))
                {
                    if (told.Count >= 7) break;
                    if (IsTroubled(x.Place)) told.Add(x);
                }
                told = told.OrderBy(x => x.Dist).ToList();

                sawPlaces = true;
                lines.Add("And the places within sight of me:");
                foreach (var x in told)
                    lines.Add("- " + PlaceBrief(x.Place, x.Dist, party));
            });

            // The dens of brigands the company has SPOTTED — the fixed lairs the bands above ride out
            // from. Only what the map already knows: an unspotted den stays honestly unknown, so the
            // scout never turns oracle (2026.07.22, the "scouts are blind to hideouts" playtest find).
            bool sawDens = false;
            Try(() =>
            {
                var dens = Settlement.All
                    .Where(s => s != null && s.IsHideout && Safe(() => s.Hideout.IsSpotted))
                    .Select(s => new { Den = s, Dist = SafeSettlementDistance(s, party) })
                    .Where(x => x.Dist >= 0 && x.Dist <= range)
                    .OrderBy(x => x.Dist)
                    .Take(3)
                    .ToList();
                if (dens.Count == 0) return;

                sawDens = true;
                lines.Add("And the lairs my company has spotted:");
                foreach (var x in dens)
                    lines.Add("- " + DenBrief(x.Den, x.Dist, party, eyes));
            });

            if (!sawAnyone && !sawPlaces && !sawDens)
                lines.Add("The country about lies empty as far as my eyes reach — no band moves within sight, no settlement stands near, and no den of brigands is known nearby.");
            else if (!sawAnyone)
                lines.Add("No band moves within sight of me just now.");

            if (eyes < 50)
                lines.Add("(My eyes are not the sharpest at this craft — trust the shapes, not the counts.)");

            return string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        // One seen band: whose, how many (as well as the eyes can count), friend or foe, how far,
        // and who is the swifter — the raw stuff of "can we escape them?".
        private static string BandBrief(MobileParty p, float dist, MobileParty ours, Hero asker, int eyes)
        {
            var sb = new StringBuilder();

            Try(() =>
            {
                string what;
                var leader = p.LeaderHero?.Name?.ToString();
                var faction = p.MapFaction?.Name?.ToString();
                if (Safe(() => p.Army != null && p.Army.LeaderParty == p))
                    what = $"the banners of {p.Army.Name} — a gathered army" + (faction != null ? $" of {faction}" : "");
                else if (p.IsCaravan) what = "a trading caravan" + (faction != null ? $" of {faction}" : "");
                else if (p.IsVillager) what = "village folk with their goods";
                else if (p.IsBandit) what = "a band of brigands";
                else if (p.IsLordParty) what = (leader != null ? $"{leader}'s warband" : "a lord's warband") + (faction != null ? $" of {faction}" : "");
                else what = "a band" + (faction != null ? $" of {faction}" : "");
                sb.Append(what);
            });

            Try(() =>
            {
                int men = p.MemberRoster?.TotalManCount ?? 0;
                if (men <= 0) return;
                // A master scout counts true; a lesser eye rounds to the nearest handful or score.
                if (eyes >= 125) sb.Append($", {men} strong");
                else if (eyes >= 50) sb.Append($", some {RoundTo(men, 5)} strong");
                else sb.Append($", perhaps {RoundTo(men, 10)} strong");
            });

            Try(() =>
            {
                bool foe = Safe(() => FactionManager.IsAtWarAgainstFaction(p.MapFaction, ours.MapFaction))
                    || Safe(() => p.IsBandit);
                sb.Append(foe ? " — FOES" : " — no quarrel with me");
            });

            sb.Append(", " + Whereabouts(dist, ours, () => p.Position));

            // What they are about. What is plainly seen — a raid, a siege, a battle joined — every
            // eye can read; a guess at where a marching band is BOUND wants some craft.
            Try(() =>
            {
                var doing = BandDoing(p, eyes);
                if (doing.Length > 0) sb.Append("; " + doing);
            });

            Try(() =>
            {
                bool foe = Safe(() => FactionManager.IsAtWarAgainstFaction(p.MapFaction, ours.MapFaction))
                    || Safe(() => p.IsBandit);
                if (!foe) return;
                float theirs = p.Speed, mine = ours.Speed;
                if (mine > theirs * 1.05f) sb.Append("; my company is the swifter — I could outdistance them");
                else if (theirs > mine * 1.05f) sb.Append("; they are the swifter — flight alone would not save me");
                else sb.Append("; the pace between us is even — a chase would run long");
            });

            return sb.ToString() + ".";
        }

        // What a seen band is presently about, read off the map the way an eye would read it.
        private static string BandDoing(MobileParty p, int eyes)
        {
            var ev = Safe(() => p.MapEvent, (MapEvent)null);
            if (ev != null)
            {
                var where = Safe(() => ev.MapEventSettlement?.Name?.ToString(), (string)null);
                if (Safe(() => ev.IsRaid))
                    return where != null
                        ? $"even now their hands are in the sack of {where} — smoke stands over it"
                        : "even now their hands are in a raid — smoke stands over the place";
                if (Safe(() => ev.IsSiegeAssault))
                    return where != null ? $"even now they storm the walls of {where}" : "even now they storm a wall";
                return where != null ? $"even now they are locked in battle at {where}" : "even now they stand in the press of battle";
            }

            var besieged = Safe(() => p.BesiegedSettlement?.Name?.ToString(), (string)null);
            if (besieged != null) return $"they lie encamped in siege about {besieged}";

            if (eyes < 50) return string.Empty;   // beyond this, one is reading intent, not banners

            var place = Safe(() => p.TargetSettlement?.Name?.ToString(), (string)null);
            var quarry = Safe(() => p.TargetParty?.LeaderHero?.Name?.ToString() ?? p.TargetParty?.Name?.ToString(), (string)null);
            switch (Safe(() => p.DefaultBehavior, AiBehavior.None))
            {
                case AiBehavior.RaidSettlement:
                case AiBehavior.AssaultSettlement:
                    return place != null ? $"they ride upon {place}, and not kindly" : "they ride upon a settlement, and not kindly";
                case AiBehavior.BesiegeSettlement:
                    return place != null ? $"they move to lay siege to {place}" : "they move to lay a siege";
                case AiBehavior.GoToSettlement:
                    return place != null ? $"they seem bound for {place}" : string.Empty;
                case AiBehavior.EngageParty:
                case AiBehavior.GoAroundParty:
                    return quarry != null ? $"they are hunting {quarry}" : "they are hunting someone in the field";
                case AiBehavior.EscortParty:
                    return quarry != null ? $"they ride escort to {quarry}" : "they ride escort";
                case AiBehavior.PatrolAroundPoint:
                    return place != null ? $"they patrol the country about {place}" : "they are on patrol";
                case AiBehavior.DefendSettlement:
                    return place != null ? $"they are sworn to the defense of {place}" : string.Empty;
                case AiBehavior.FleeToPoint:
                case AiBehavior.FleeToParty:
                case AiBehavior.FleeToGate:
                    return "they are withdrawing from something they do not care to meet";
                default:
                    return string.Empty;
            }
        }

        // One place within sight: what it is, whose it is, which way it lies — and above all how it
        // fares. A village under the knife is the whole reason this block exists.
        private static string PlaceBrief(Settlement s, float dist, MobileParty ours)
        {
            var sb = new StringBuilder();
            Try(() =>
            {
                string kind = s.IsVillage ? "a village" : s.IsCastle ? "a castle" : "a town";
                var realm = Safe(() => s.MapFaction?.Name?.ToString(), (string)null);
                var clan = Safe(() => s.OwnerClan?.Name?.ToString(), (string)null);
                sb.Append(s.Name?.ToString() ?? "a place");
                sb.Append(", " + kind);
                if (realm != null) sb.Append($" of {realm}");
                if (clan != null) sb.Append($" held by clan {clan}");
            });

            sb.Append(", " + Whereabouts(dist, ours, () => s.Position));

            Try(() =>
            {
                if (Safe(() => s.IsUnderRaid))
                {
                    var raider = AttackerAt(s);
                    sb.Append(raider != null
                        ? $" — and IT BURNS: {raider} is at the sack of it even now"
                        : " — and IT BURNS: it is being raided even now");
                    return;
                }
                if (Safe(() => s.IsUnderSiege))
                {
                    var besieger = Safe(() => s.SiegeEvent?.BesiegerCamp?.LeaderParty, (MobileParty)null);
                    var who = besieger == null ? null
                        : Safe(() => besieger.LeaderHero?.Name?.ToString() ?? besieger.Name?.ToString(), (string)null);
                    sb.Append(who != null
                        ? $" — and it lies under siege: {who} is encamped about its walls"
                        : " — and it lies under siege");
                    return;
                }
                if (Safe(() => s.IsRaided))
                {
                    sb.Append(" — lately plundered: its folk are stripped and its fields blackened");
                    return;
                }
                if (Safe(() => s.InRebelliousState)) sb.Append(" — and it is in rebellion against its lord");
            });

            return sb.ToString() + ".";
        }

        // Who is doing the deed at a place under attack — the raid's own attacker, else whoever
        // last laid hands on it.
        private static string AttackerAt(Settlement s)
        {
            var party = Safe(() => s.Party?.MapEvent?.GetLeaderParty(BattleSideEnum.Attacker)?.MobileParty, (MobileParty)null)
                ?? Safe(() => s.LastAttackerParty, (MobileParty)null);
            if (party == null) return null;
            return Safe(() =>
            {
                var leader = party.LeaderHero?.Name?.ToString();
                var faction = party.MapFaction?.Name?.ToString();
                if (party.IsBandit) return faction != null ? $"a band of {faction}" : "a band of brigands";
                if (leader != null) return faction != null ? $"{leader} of {faction}" : leader;
                return party.Name?.ToString();
            }, (string)null);
        }

        private static bool IsTroubled(Settlement s) =>
            Safe(() => s.IsUnderRaid) || Safe(() => s.IsUnderSiege) || Safe(() => s.IsRaided);

        // One spotted den: whose brigands, how many lurk within (as well as the eyes can count),
        // and how far. A den the company never spotted is honestly unknown — the scout is no oracle.
        private static string DenBrief(Settlement den, float dist, MobileParty ours, int eyes)
        {
            var sb = new StringBuilder();
            sb.Append(DenName(den));
            int men = 0;
            Try(() => men = LurkersWithin(den));
            if (men <= 0) sb.Append(", lying quiet — its brigands gone, or out riding");
            else if (eyes >= 125) sb.Append($", {men} brigands lurking within");
            else sb.Append($", perhaps {RoundTo(men, 10)} brigands lurking within");
            sb.Append(" — FOES, " + Whereabouts(dist, ours, () => den.Position));
            return sb.ToString() + ".";
        }

        // Dens carry no proper name of their own; the brigands' own name serves ("a den of Sea Raiders").
        private static string DenName(Settlement den)
        {
            var clan = Safe(() => den.Hideout?.MapFaction?.Name?.ToString(), (string?)null);
            return string.IsNullOrWhiteSpace(clan) ? "a den of brigands" : $"a den of {clan}";
        }

        private static int LurkersWithin(Settlement den) =>
            den.Parties?.Where(p => p != null && Safe(() => p.IsBandit))
                .Sum(p => Safe(() => p.MemberRoster?.TotalManCount ?? 0, 0)) ?? 0;

        // ------------------------------ the scales of battle ------------------------------

        private static string Weigh(string name, Hero asker)
        {
            var party = asker?.PartyBelongedTo;
            if (party == null)
                return "I have no company of my own to set upon the scales.";

            // Our side: the whole army when we march within one, else the company alone.
            float ours = 0; int ourMen = 0; string ourWord = "my company";
            Try(() =>
            {
                if (party.Army != null)
                {
                    ours = party.Army.Parties?.Sum(p => Safe(() => p.Party?.EstimatedStrength ?? 0f, 0f)) ?? 0f;
                    ourMen = party.Army.TotalManCount;
                    ourWord = $"the army I march within ({party.Army.Name})";
                }
                else
                {
                    ours = party.Party?.EstimatedStrength ?? 0f;
                    ourMen = party.MemberRoster?.TotalManCount ?? 0;
                }
            });

            // Their side: a named party or settlement, or the nearest hostile band in sight.
            var target = FindTargetParty(name, party);
            if (target != null)
                return WeighAgainstParty(target, party, asker, ours, ourMen, ourWord);

            var place = FindTargetSettlement(name, party);
            if (place != null)
            {
                if (place.IsHideout) return WeighAgainstDen(place, asker, ours, ourMen, ourWord);
                if (place.IsVillage) return WeighAgainstVillage(place, party, asker, ours, ourMen, ourWord);
                return WeighAgainstWalls(place, asker, ours, ourMen, ourWord);
            }

            return string.IsNullOrWhiteSpace(name)
                ? "No hostile band stands within sight to weigh against — name a foe, a place, or a spotted den, and the scales can be set."
                : $"Search the country and my memory as I may, no band, place, or spotted den called \"{name}\" comes to mind to weigh against.";
        }

        private static string WeighAgainstParty(MobileParty target, MobileParty ours, Hero asker, float ourStrength, int ourMen, string ourWord)
        {
            float theirs = 0; int theirMen = 0; string theirWord = "them";
            Try(() =>
            {
                if (target.Army != null && target.Army.LeaderParty == target)
                {
                    theirs = target.Army.Parties?.Sum(p => Safe(() => p.Party?.EstimatedStrength ?? 0f, 0f)) ?? 0f;
                    theirMen = target.Army.TotalManCount;
                    theirWord = $"the banners of {target.Army.Name}";
                }
                else
                {
                    theirs = target.Party?.EstimatedStrength ?? 0f;
                    theirMen = target.MemberRoster?.TotalManCount ?? 0;
                    var leader = target.LeaderHero?.Name?.ToString();
                    theirWord = leader != null ? $"{leader}'s company" : (target.Name?.ToString() ?? "that band");
                }
            });

            var lines = new List<string>
            {
                $"I set {theirWord} upon the scales against {ourWord}.",
                $"Mine: {ourMen} souls. Theirs: {theirMen}.",
            };
            Try(() => lines.Add("My ranks: " + Composition(ours.MemberRoster) + "."));
            Try(() => lines.Add("Their ranks: " + Composition(target.MemberRoster) + "."));
            lines.Add(Verdict(ourStrength, theirs, asker));
            return string.Join(" ", lines);
        }

        private static string WeighAgainstWalls(Settlement s, Hero asker, float ourStrength, int ourMen, string ourWord)
        {
            float theirs = 0; int garrison = 0; int militia = 0;
            Try(() => { garrison = s.Town?.GarrisonParty?.MemberRoster?.TotalManCount ?? 0; });
            Try(() => { theirs = s.Town?.GarrisonParty?.Party?.EstimatedStrength ?? 0f; });
            Try(() => { militia = (int)s.Militia; });

            var lines = new List<string>
            {
                $"I set the walls of {s.Name} upon the scales against {ourWord} ({ourMen} souls).",
                garrison > 0
                    ? $"Its garrison stands some {garrison} strong" + (militia > 0 ? $", and perhaps {militia} militia would take up arms beside them." : ".")
                    : (militia > 0 ? $"No true garrison holds it, only some {militia} militia." : "Neither garrison nor militia comes to mind for the place."),
            };

            // Militia weigh light: half-strength irregulars, so the verdict is not blind to them.
            float wallStrength = theirs + militia * 0.5f;
            if (wallStrength > 0) lines.Add(Verdict(ourStrength, wallStrength, asker)
                + " And walls are their own soldier: a stormed wall eats the attacker's advantage.");
            return string.Join(" ", lines);
        }

        // A village is no fortress: it has no walls and no true garrison. Asked to weigh one while it
        // burns, the scales that matter are against whoever holds the torch — that is the question
        // truly being asked (2026.07.27).
        private static string WeighAgainstVillage(Settlement v, MobileParty ours, Hero asker, float ourStrength, int ourMen, string ourWord)
        {
            var raider = Safe(() => v.Party?.MapEvent?.GetLeaderParty(BattleSideEnum.Attacker)?.MobileParty, (MobileParty)null);
            if (raider != null && raider != ours)
                return $"{v.Name} lies under the knife even now. " +
                       WeighAgainstParty(raider, ours, asker, ourStrength, ourMen, ourWord);

            int militia = 0;
            Try(() => militia = (int)v.Militia);
            var lines = new List<string>
            {
                $"I set {v.Name} upon the scales against {ourWord} ({ourMen} souls).",
                "It is a village: no wall, no gate, no garrison —" +
                (militia > 0 ? $" only some {militia} villagers who might take up what tools they have." : " only its folk."),
            };
            if (militia > 0) lines.Add(Verdict(ourStrength, militia * 0.5f, asker));
            return string.Join(" ", lines);
        }

        // A den is neither band nor wall: its strength is whatever brigand parties lurk inside it
        // right now, and the raid itself is fought by a handful, not the company's whole weight.
        private static string WeighAgainstDen(Settlement den, Hero asker, float ourStrength, int ourMen, string ourWord)
        {
            float theirs = 0; int lurkers = 0;
            Try(() =>
            {
                foreach (var p in den.Parties)
                {
                    if (p == null || !Safe(() => p.IsBandit)) continue;
                    lurkers += Safe(() => p.MemberRoster?.TotalManCount ?? 0, 0);
                    theirs += Safe(() => p.Party?.EstimatedStrength ?? 0f, 0f);
                }
            });

            var lines = new List<string>
            {
                $"I set {DenName(den)} upon the scales against {ourWord} ({ourMen} souls).",
                lurkers > 0
                    ? $"Some {lurkers} brigands lurk within it."
                    : "It seems to lie quiet — its brigands gone, or out riding the roads.",
            };
            if (theirs > 0) lines.Add(Verdict(ourStrength, theirs, asker)
                + " And know that a den is stormed by a chosen few, not a whole company — the boldest hands at my side, come what may.");
            return string.Join(" ", lines);
        }

        // The judgment itself, worded by the true strength ratio, its confidence by the asker's Tactics.
        private static string Verdict(float ours, float theirs, Hero asker)
        {
            if (theirs <= 0.01f) return "There is nothing on their side of the scales to speak of.";
            if (ours <= 0.01f) return "I have nothing on my side of the scales — this is no fight at all.";

            double r = ours / theirs;
            string call =
                r >= 2.0 ? "They could not stand against me — the scales fall wholly my way" :
                r >= 1.3 ? "The scales lean well toward me; the day should be mine, though not without cost" :
                r >= 0.8 ? "The scales stand near even — the day would be bought dear, and could fall either way" :
                r >= 0.5 ? "The scales lean against me; only ground, cunning, or fortune could turn it" :
                           "It would be folly — they would break me";

            var judgment = CraftsBuilder.WordFor(asker, DefaultSkills.Tactics);
            return $"{call}. So I judge — and my eye for such judgments is {judgment}.";
        }

        // "40 foot, 25 horse, 30 bowmen, 10 horse-archers" from a real roster.
        private static string Composition(TaleWorlds.CampaignSystem.Roster.TroopRoster roster)
        {
            if (roster == null) return "unknown";
            int foot = 0, horse = 0, bows = 0, horseBows = 0;
            foreach (var e in roster.GetTroopRoster())
            {
                var c = e.Character;
                if (c == null || e.Number <= 0) continue;
                switch (c.DefaultFormationClass)
                {
                    case FormationClass.Ranged: bows += e.Number; break;
                    case FormationClass.Cavalry: horse += e.Number; break;
                    case FormationClass.HorseArcher: horseBows += e.Number; break;
                    default: foot += e.Number; break;
                }
            }
            var parts = new List<string>();
            if (foot > 0) parts.Add($"{foot} foot");
            if (bows > 0) parts.Add($"{bows} bowmen");
            if (horse > 0) parts.Add($"{horse} horse");
            if (horseBows > 0) parts.Add($"{horseBows} horse-archers");
            return parts.Count == 0 ? "none under arms" : string.Join(", ", parts);
        }

        // ------------------------------ finding the foe ------------------------------

        private static MobileParty FindTargetParty(string name, MobileParty ours)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    // The nearest hostile band within sight — "that army there".
                    float range = Math.Max(10f, ours.SeeingRange * 1.5f);
                    return MobileParty.All
                        .Where(p => p != null && p != ours && Safe(() => p.IsActive && p.CurrentSettlement == null))
                        .Where(p => Safe(() => FactionManager.IsAtWarAgainstFaction(p.MapFaction, ours.MapFaction)) || Safe(() => p.IsBandit))
                        .Select(p => new { Party = p, Dist = SafeDistance(p, ours) })
                        .Where(x => x.Dist >= 0 && x.Dist <= range)
                        .OrderBy(x => x.Dist)
                        .FirstOrDefault()?.Party;
                }

                var needle = name.Trim();
                bool Matches(string s) => !string.IsNullOrWhiteSpace(s)
                    && s.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

                // Village carts carry their village's name ("Villagers of Stathymos") and would steal
                // every question about the village itself — leave them out unless truly asked for.
                bool Mentions(string word) => needle.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
                bool wantsFolk = Mentions("villager") || Mentions("peasant") || Mentions("folk");

                // Prefer the nearest match: two name-twin warbands resolve to the one at hand.
                return MobileParty.All
                    .Where(p => p != null && p != ours && Safe(() => p.IsActive))
                    .Where(p => wantsFolk || !Safe(() => p.IsVillager))
                    .Where(p => Safe(() => Matches(p.LeaderHero?.Name?.ToString()))
                             || Safe(() => Matches(p.Name?.ToString()))
                             || Safe(() => p.Army != null && Matches(p.Army.Name?.ToString()) && p.Army.LeaderParty == p))
                    .Select(p => new { Party = p, Dist = SafeDistance(p, ours) })
                    .Where(x => x.Dist >= 0)
                    .OrderBy(x => x.Dist)
                    .FirstOrDefault()?.Party;
            }
            catch { return null; }
        }

        private static Settlement FindTargetSettlement(string name, MobileParty ours)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return null;
                var needle = name.Trim();
                // Named places first — towns, castles AND villages (a burning village is a foe's
                // whereabouts as much as any wall), nearest match winning among name-twins.
                var named = Settlement.All
                    .Where(s => s != null && (s.IsTown || s.IsCastle || s.IsVillage))
                    .Where(s => (s.Name?.ToString()?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    .Select(s => new { Place = s, Dist = SafeSettlementDistance(s, ours) })
                    .Where(x => x.Dist >= 0)
                    .OrderBy(x => x.Dist)
                    .FirstOrDefault()?.Place;
                if (named != null) return named;

                // A den carries no proper name — the asker says "the hideout", "the Sea Raiders'
                // den" — so match the word or the brigands' own name, and let the NEAREST spotted
                // one answer (every den on the map shares the same bare label).
                bool denWords = needle.IndexOf("hideout", StringComparison.OrdinalIgnoreCase) >= 0
                    || needle.IndexOf("den", StringComparison.OrdinalIgnoreCase) >= 0
                    || needle.IndexOf("lair", StringComparison.OrdinalIgnoreCase) >= 0;
                return Settlement.All
                    .Where(s => s != null && s.IsHideout && Safe(() => s.Hideout.IsSpotted))
                    .Where(s => denWords
                        || (s.Name?.ToString()?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                        || Safe(() => (s.Hideout.MapFaction?.Name?.ToString()?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))
                    .Select(s => new { Den = s, Dist = SafeSettlementDistance(s, ours) })
                    .Where(x => x.Dist >= 0)
                    .OrderBy(x => x.Dist)
                    .FirstOrDefault()?.Den;
            }
            catch { return null; }
        }

        // ------------------------------ small helpers ------------------------------

        private static float SafeDistance(MobileParty a, MobileParty b)
        {
            try { return a.Position.Distance(b.Position); }
            catch { return -1f; }
        }

        private static float SafeSettlementDistance(Settlement s, MobileParty p)
        {
            try { return s.Position.Distance(p.Position); }
            catch { return -1f; }
        }

        // How far AND which way — "a few hours' ride to the north". The player points at the map and
        // says "the village north of us"; without a bearing the scout can only answer in circles.
        private static string Whereabouts(float dist, MobileParty ours, Func<CampaignVec2> theirs)
        {
            var words = DistanceWords(dist);
            var bearing = Safe(() =>
            {
                var here = ours.Position; var there = theirs();
                return Bearing(there.X - here.X, there.Y - here.Y);
            }, string.Empty);
            return bearing.Length == 0 ? words : words + " to the " + bearing;
        }

        // The eight winds, on the map's own reckoning: +Y is north, +X is east.
        private static string Bearing(float dx, float dy)
        {
            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f) return string.Empty;
            double deg = Math.Atan2(dx, dy) * 180.0 / Math.PI;   // 0 = north, turning east
            if (deg < 0) deg += 360.0;
            var winds = new[] { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return winds[(int)Math.Round(deg / 45.0) % 8];
        }

        // Distances in a rider's words, on the courier's scale (~150 map units to a day's ride).
        private static string DistanceWords(float dist)
        {
            if (dist < 5f) return "close at hand";
            if (dist < 12f) return "within an hour's ride";
            if (dist < 40f) return "a few hours' ride off";
            if (dist < 90f) return "half a day's march away";
            return "near a day's march away";
        }

        private static int RoundTo(int n, int step) => Math.Max(step, (int)(Math.Round(n / (double)step) * step));

        // Speed-explanation names arrive with game markup at times; keep only the plain words.
        private static string Plain(string s)
        {
            var text = (s ?? string.Empty).Trim();
            int open;
            while ((open = text.IndexOf('{')) >= 0)
            {
                int close = text.IndexOf('}', open);
                if (close < 0) break;
                text = text.Remove(open, close - open + 1);
            }
            return text.Trim();
        }

        private static void Try(Action a) { try { a(); } catch { /* skip this fact */ } }

        private static bool Safe(Func<bool> f) { try { return f(); } catch { return false; } }

        private static T Safe<T>(Func<T> f, T fallback) { try { return f(); } catch { return fallback; } }
    }
}
