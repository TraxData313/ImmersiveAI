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
                "Cast your eyes over the country about your company — every band, caravan, and army moving " +
                "within sight: whose they are, their strength as well as your eyes can count it, whether they " +
                "are friend or foe, and whether they or you are the swifter. Also any den of brigands your " +
                "company has spotted nearby, and your own company's pace and what slows it. Reach for this " +
                "before ever speaking of who is near, of hideouts and lairs, of pursuit, of escape, or of " +
                "the speed of the march."),

            new ToolDefinition(WeighBattle,
                "Set a foe upon the scales against your own company: their numbers and kinds of fighters " +
                "against yours, and how the day would likely go. Works against a band or army moving in the " +
                "country, against the garrison of a named town or castle, or against a spotted den of " +
                "brigands. Reach for this before ever counselling battle or retreat.",
                new[] { new ToolParameter("name",
                    "Who to weigh against: a war party or army by its leader's or its own name, or a town, " +
                    "castle, or brigands' den by name. Leave it out to weigh the nearest hostile band in sight.", required: false) }),
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
                return "You have no company upon the map to look out from — the country about is another's to watch.";

            var lines = new List<string>();
            int eyes = CraftsBuilder.ValueFor(asker, DefaultSkills.Scouting);

            // Our own pace first, with what truly slows it — the scout's answer to "can we go faster?".
            Try(() =>
            {
                float speed = party.Speed;
                lines.Add($"Your company moves at a pace of {speed:0.0} upon the map.");
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
            bool sawAnyone = false;
            Try(() =>
            {
                float range = Math.Max(10f, party.SeeingRange * 1.5f);
                var seen = MobileParty.All
                    .Where(p => p != null && p != party)
                    .Where(p => Safe(() => p.IsActive && p.CurrentSettlement == null))
                    .Select(p => new { Party = p, Dist = SafeDistance(p, party) })
                    .Where(x => x.Dist >= 0 && x.Dist <= range)
                    .OrderBy(x => x.Dist)
                    .Take(eyes >= 75 ? 8 : 5)
                    .ToList();
                if (seen.Count == 0) return;

                sawAnyone = true;
                lines.Add("Moving in the country about, nearest first:");
                foreach (var x in seen)
                    lines.Add("- " + BandBrief(x.Party, x.Dist, party, asker, eyes));
            });

            // The dens of brigands the company has SPOTTED — the fixed lairs the bands above ride out
            // from. Only what the map already knows: an unspotted den stays honestly unknown, so the
            // scout never turns oracle (2026.07.22, the "scouts are blind to hideouts" playtest find).
            bool sawDens = false;
            Try(() =>
            {
                float range = Math.Max(10f, party.SeeingRange * 1.5f);
                var dens = Settlement.All
                    .Where(s => s != null && s.IsHideout && Safe(() => s.Hideout.IsSpotted))
                    .Select(s => new { Den = s, Dist = SafeSettlementDistance(s, party) })
                    .Where(x => x.Dist >= 0 && x.Dist <= range)
                    .OrderBy(x => x.Dist)
                    .Take(3)
                    .ToList();
                if (dens.Count == 0) return;

                sawDens = true;
                lines.Add("And standing still in the country, the lairs your company has spotted:");
                foreach (var x in dens)
                    lines.Add("- " + DenBrief(x.Den, x.Dist, eyes));
            });

            if (!sawAnyone && !sawDens)
                lines.Add("The country about lies empty as far as your eyes reach — no band moves within sight, and no den of brigands is known nearby.");

            if (eyes < 50)
                lines.Add("(Your eyes are not the sharpest at this craft — trust the shapes, not the counts.)");

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
                sb.Append(foe ? " — FOES" : " — no quarrel with you");
            });

            sb.Append(", " + DistanceWords(dist));

            Try(() =>
            {
                bool foe = Safe(() => FactionManager.IsAtWarAgainstFaction(p.MapFaction, ours.MapFaction))
                    || Safe(() => p.IsBandit);
                if (!foe) return;
                float theirs = p.Speed, mine = ours.Speed;
                if (mine > theirs * 1.05f) sb.Append("; your company is the swifter — you could outdistance them");
                else if (theirs > mine * 1.05f) sb.Append("; they are the swifter — flight alone would not save you");
                else sb.Append("; the pace between you is even — a chase would run long");
            });

            return sb.ToString() + ".";
        }

        // One spotted den: whose brigands, how many lurk within (as well as the eyes can count),
        // and how far. A den the company never spotted is honestly unknown — the scout is no oracle.
        private static string DenBrief(Settlement den, float dist, int eyes)
        {
            var sb = new StringBuilder();
            sb.Append(DenName(den));
            int men = 0;
            Try(() => men = LurkersWithin(den));
            if (men <= 0) sb.Append(", lying quiet — its brigands gone, or out riding");
            else if (eyes >= 125) sb.Append($", {men} brigands lurking within");
            else sb.Append($", perhaps {RoundTo(men, 10)} brigands lurking within");
            sb.Append(" — FOES, " + DistanceWords(dist));
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
                return "You have no company of your own to set upon the scales.";

            // Our side: the whole army when we march within one, else the company alone.
            float ours = 0; int ourMen = 0; string ourWord = "your company";
            Try(() =>
            {
                if (party.Army != null)
                {
                    ours = party.Army.Parties?.Sum(p => Safe(() => p.Party?.EstimatedStrength ?? 0f, 0f)) ?? 0f;
                    ourMen = party.Army.TotalManCount;
                    ourWord = $"the army you march within ({party.Army.Name})";
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
                return place.IsHideout
                    ? WeighAgainstDen(place, asker, ours, ourMen, ourWord)
                    : WeighAgainstWalls(place, asker, ours, ourMen, ourWord);

            return string.IsNullOrWhiteSpace(name)
                ? "No hostile band stands within sight to weigh against — name a foe, a walled place, or a spotted den, and the scales can be set."
                : $"Search the country and your memory as you may, no band, walled place, or spotted den called \"{name}\" comes to mind to weigh against.";
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
                $"You set {theirWord} upon the scales against {ourWord}.",
                $"Yours: {ourMen} souls. Theirs: {theirMen}.",
            };
            Try(() => lines.Add("Your ranks: " + Composition(ours.MemberRoster) + "."));
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
                $"You set the walls of {s.Name} upon the scales against {ourWord} ({ourMen} souls).",
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
                $"You set {DenName(den)} upon the scales against {ourWord} ({ourMen} souls).",
                lurkers > 0
                    ? $"Some {lurkers} brigands lurk within it."
                    : "It seems to lie quiet — its brigands gone, or out riding the roads.",
            };
            if (theirs > 0) lines.Add(Verdict(ourStrength, theirs, asker)
                + " And know that a den is stormed by a chosen few, not a whole company — the boldest hands at your side, come what may.");
            return string.Join(" ", lines);
        }

        // The judgment itself, worded by the true strength ratio, its confidence by the asker's Tactics.
        private static string Verdict(float ours, float theirs, Hero asker)
        {
            if (theirs <= 0.01f) return "There is nothing on their side of the scales to speak of.";
            if (ours <= 0.01f) return "You have nothing on your side of the scales — this is no fight at all.";

            double r = ours / theirs;
            string call =
                r >= 2.0 ? "They could not stand against you — the scales fall wholly your way" :
                r >= 1.3 ? "The scales lean well toward you; the day should be yours, though not without cost" :
                r >= 0.8 ? "The scales stand near even — the day would be bought dear, and could fall either way" :
                r >= 0.5 ? "The scales lean against you; only ground, cunning, or fortune could turn it" :
                           "It would be folly — they would break you";

            var judgment = CraftsBuilder.WordFor(asker, DefaultSkills.Tactics);
            return $"{call}. So you judge — and your eye for such judgments is {judgment}.";
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

                // Prefer the nearest match: two name-twin warbands resolve to the one at hand.
                return MobileParty.All
                    .Where(p => p != null && p != ours && Safe(() => p.IsActive))
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
                var walled = Settlement.All.FirstOrDefault(s => s != null && (s.IsTown || s.IsCastle)
                    && (s.Name?.ToString()?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
                if (walled != null) return walled;

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
