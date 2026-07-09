using System;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Builds the "current situation" — the environmental facts about a conversation the moment
    /// it begins: when and where it happens, who the speaker is, and who they are speaking with.
    /// It is written as a gentle voice speaking into the NPC's own mind (second person, no clinical
    /// headers), the same block that gets written to <c>current_situation_info.txt</c> and folded
    /// into the LLM prompt, so what the player inspects on disk is exactly what the NPC "sees".
    ///
    /// It is built with respect to the <paramref name="partner"/> the speaker is addressing
    /// (standing, war/peace, etc. are relative to that party), not hardcoded to the player, so the
    /// same builder serves future NPC-to-NPC conversations. Everything is best-effort: any missing
    /// or throwing game datum is skipped rather than aborting the block.
    /// </summary>
    public static class SituationBuilder
    {
        private static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };

        /// <summary>A human-readable Calradia timestamp, e.g.
        /// "1084.02.03 13.24 (Summer 3, Year 1084)". Also used to stamp conversation turns.</summary>
        public static string Timestamp()
        {
            var now = CampaignTime.Now;
            int year = now.GetYear;

            int dayOfYear = (int)now.ToDays % 84;
            if (dayOfYear < 0) dayOfYear += 84;
            int season = dayOfYear / 21;             // 0..3
            int dayOfSeason = dayOfYear % 21 + 1;    // 1..21

            HourMinute(out int hour, out int minute);

            return $"{year:0000}.{season + 1:00}.{dayOfSeason:00} {hour:00}.{minute:00} " +
                   $"({Seasons[season]} {dayOfSeason}, Year {year})";
        }

        /// <summary>Coarse time-of-day label derived from the game clock, e.g. "early afternoon".</summary>
        public static string TimeOfDay()
        {
            HourMinute(out int hour, out _);
            if (hour < 5) return "the dead of night";
            if (hour < 8) return "early morning";
            if (hour < 11) return "morning";
            if (hour < 13) return "midday";
            if (hour < 16) return "early afternoon";
            if (hour < 18) return "late afternoon";
            if (hour < 21) return "evening";
            return "night";
        }

        private static void HourMinute(out int hour, out int minute)
        {
            double totalHours = CampaignTime.Now.ToHours;
            hour = (int)(totalHours % 24);
            if (hour < 0) hour += 24;
            minute = (int)((totalHours - Math.Floor(totalHours)) * 60);
        }

        /// <summary>Short label for where the NPC currently is: the settlement name, or a field note.
        /// Used to stamp conversation turns.</summary>
        public static string Place(Hero npc)
        {
            var settlement = npc?.CurrentSettlement ?? Settlement.CurrentSettlement;
            var name = settlement?.Name?.ToString() ?? string.Empty;
            return name.Trim().Length == 0 ? "the open field" : name;
        }

        /// <summary>Fuller sentence describing where the conversation happens, with settlement type
        /// and the faction that holds it, for the situation block and the prompt.</summary>
        private static string PlaceDescription(Hero speaker)
        {
            var settlement = speaker?.CurrentSettlement ?? Settlement.CurrentSettlement;
            if (settlement == null)
                return "out in the open field, away from any town or castle";

            var type = SettlementType(settlement);
            var name = settlement.Name?.ToString() ?? "an unnamed place";
            var holder = settlement.OwnerClan?.Kingdom?.Name?.ToString()
                         ?? settlement.OwnerClan?.Name?.ToString();
            return holder == null
                ? $"in the {type} of {name}"
                : $"in the {type} of {name}, held by {holder}";
        }

        private static string SettlementType(Settlement s)
        {
            if (s.IsTown) return "town";
            if (s.IsCastle) return "castle";
            if (s.IsVillage) return "village";
            return "settlement";
        }

        /// <summary>
        /// The full "current situation" — written as a gentle voice speaking softly into the speaker's
        /// mind, in the second person, never a clinical data sheet: when and where this moment finds
        /// them, who they are, and who has come to speak with them (standing and war/peace felt toward
        /// that partner), and what has lately happened in the world around them (tidings and the talk of
        /// the town — see <see cref="TidingsBuilder"/>, gated by <paramref name="config"/>). This is what
        /// is saved to current_situation_info.txt and folded into the prompt.
        /// </summary>
        public static string Build(Hero speaker, Hero partner, ModConfig? config = null)
            => Build(speaker, partner, config, apart: false);

        /// <summary>Same situation block, but with <paramref name="apart"/> true the partner is NOT
        /// here: the scene opens on the speaker alone and the partner is described as someone far
        /// away and on their mind — the framing a letter is written or read in.</summary>
        public static string Build(Hero speaker, Hero partner, ModConfig? config, bool apart)
        {
            var sb = new StringBuilder();
            var name = Name(speaker);

            // The approach: who comes to them, when, and where — the opening breath of the scene.
            string approach;
            if (partner == null || apart)
                approach = $"This moment finds you, {name}";
            else if (partner == Hero.MainHero)
                approach = $"{Name(partner)} comes to you, {name}";
            else
                approach = $"{Name(partner)} comes to speak with you, {name}";
            sb.AppendLine($"{approach}. It is {TimeOfDay()} — {Timestamp()} — and you are {PlaceDescription(speaker)}.");
            if (partner != null && apart)
                sb.AppendLine($"{Name(partner)} is not here — they are far from you now, and the road between you is long.");

            // Who they are, gently recalled to them.
            var self = DescribeSelf(speaker);
            if (self.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(self);
            }

            // Who now stands before them, and how their heart leans toward that person.
            if (partner != null)
            {
                var them = DescribeOther(speaker, partner);
                if (them.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(them);
                }
            }

            // What has lately happened in the world, as far as it would have reached them (best-effort;
            // a null config — e.g. an older caller — keeps the defaults rather than losing the tidings).
            if (config == null || config.EnableWorldTidings)
            {
                var tidings = TidingsBuilder.Build(
                    speaker, partner, config?.MaxWorldTidings ?? 6, config?.MaxLocalRumors ?? 3);
                if (tidings.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(tidings);
                }
            }

            return sb.ToString().TrimEnd();
        }

        // A flowing second-person recollection of who the speaker is: birth, calling, house, and kin.
        private static string DescribeSelf(Hero h)
        {
            if (h == null) return string.Empty;
            var sentences = new System.Collections.Generic.List<string>();

            Facts(h, out string gender, out string age, out string culture, out string occ,
                  out string clan, out string kingdom);

            // Identity: "You are a Battanian noble, a woman of some 34 years."
            var head = "You are";
            if (culture != null && occ != null) head += $" {A(culture)} {culture} {occ}";
            else if (culture != null) head += $" of {culture} stock";
            else if (occ != null) head += $" {A(occ)} {occ}";
            head += GenderAgeClause(gender, age, culture == null && occ == null);
            sentences.Add(head.TrimEnd() + ".");

            // House and allegiance.
            if (clan != null && kingdom != null) sentences.Add($"You belong to clan {clan}, sworn to {kingdom}.");
            else if (clan != null) sentences.Add($"You belong to clan {clan}.");
            else if (kingdom != null) sentences.Add($"You are sworn to {kingdom}.");

            // Kin.
            Try(() =>
            {
                var spouse = h.Spouse?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(spouse)) sentences.Add($"{spouse} is wed to you.");
            });
            Try(() =>
            {
                var kids = h.Children?.Where(c => c != null && c.IsAlive)
                                      .Select(c => c.Name?.ToString())
                                      .Where(n => !string.IsNullOrWhiteSpace(n))
                                      .ToList();
                if (kids != null && kids.Count == 1) sentences.Add($"Your child is {kids[0]}.");
                else if (kids != null && kids.Count > 1) sentences.Add($"Your children are {JoinAnd(kids)}.");
            });

            // Only whereabouts worth noting beyond the place already named in the opening.
            Try(() =>
            {
                if (h.IsPrisoner) { sentences.Add("You are held captive, a prisoner."); return; }
                if (h.CurrentSettlement != null) return; // already stated in the opening line
                var party = h.PartyBelongedTo;
                if (party == null) return;
                var leader = party.LeaderHero;
                sentences.Add(leader != null && leader != h
                    ? $"You ride with {leader.Name}'s warband."
                    : "You are upon the road.");
            });

            return string.Join(" ", sentences);
        }

        // A flowing account of the one who has come to speak, and how the speaker's heart leans toward them.
        private static string DescribeOther(Hero speaker, Hero partner)
        {
            var sentences = new System.Collections.Generic.List<string>();
            var them = Name(partner);

            Facts(partner, out string gender, out string age, out string culture, out string occ,
                  out string clan, out string kingdom);

            var head = $"{them} is";
            if (culture != null && occ != null) head += $" {A(culture)} {culture} {occ}";
            else if (culture != null) head += $" of {culture} stock";
            else if (occ != null) head += $" {A(occ)} {occ}";
            else head = $"{them} stands before you";
            head += GenderAgeClause(gender, age, false);
            sentences.Add(head.TrimEnd() + ".");

            if (clan != null && kingdom != null) sentences.Add($"Their house is clan {clan}, sworn to {kingdom}.");
            else if (clan != null) sentences.Add($"Their house is clan {clan}.");
            else if (kingdom != null) sentences.Add($"They are sworn to {kingdom}.");

            Try(() =>
            {
                int relation = speaker.GetRelation(partner);
                sentences.Add($"Where your heart stands toward them: {PersonaBuilder.DescribeRelation(relation)} ({relation}).");
            });

            Try(() =>
            {
                var f1 = speaker.MapFaction;
                var f2 = partner.MapFaction;
                if (f1 == null || f2 == null) return;
                if (f1 == f2) { sentences.Add("You stand beneath the same banner."); return; }
                sentences.Add(AreAtWar(f1, f2)
                    ? $"Your peoples — {f1.Name} and {f2.Name} — are at war."
                    : $"Your peoples — {f1.Name} and {f2.Name} — are at peace.");
            });

            return string.Join(" ", sentences);
        }

        // Pulls the raw identity facts (each best-effort) so the two describers can weave them into prose.
        private static void Facts(Hero h, out string gender, out string age, out string culture,
                                  out string occ, out string clan, out string kingdom)
        {
            string g = null, a = null, c = null, o = null, cl = null, k = null;
            Try(() => g = h.IsFemale ? "a woman" : "a man");
            Try(() => { if (h.Age > 0) a = $"of some {h.Age:0} years"; });
            Try(() => { var cc = h.Culture?.Name?.ToString(); if (!string.IsNullOrWhiteSpace(cc)) c = cc.Trim(); });
            Try(() => { o = PrettyOccupation(h.Occupation.ToString()); });
            Try(() => { var xx = h.Clan?.Name?.ToString(); if (!string.IsNullOrWhiteSpace(xx)) cl = xx.Trim(); });
            Try(() => { var kk = h.Clan?.Kingdom?.Name?.ToString() ?? h.MapFaction?.Name?.ToString(); if (!string.IsNullOrWhiteSpace(kk)) k = kk.Trim(); });
            gender = g; age = a; culture = c; occ = o; clan = cl; kingdom = k;
        }

        // ", a woman of some 34 years" / ", a woman" / ", of some 34 years" — the trailing clause after
        // the identity head. When it is the whole sentence (no culture/occupation), it opens with "you are".
        private static string GenderAgeClause(string gender, string age, bool isWholeSentence)
        {
            string body;
            if (gender != null && age != null) body = $"{gender} {age}";
            else if (gender != null) body = gender;
            else if (age != null) body = age;
            else return string.Empty;

            return isWholeSentence ? " " + body : ", " + body;
        }

        // Turns the raw Occupation enum name into a warmer, gender-neutral station word.
        private static string PrettyOccupation(string occ)
        {
            if (string.IsNullOrWhiteSpace(occ)) return null;
            switch (occ.Trim().ToLowerInvariant())
            {
                case "lord":
                case "lady": return "noble";
                case "wanderer": return "wanderer and sellsword";
                case "gangleader": return "leader among the streets";
                case "mercenary": return "sellsword";
                case "artisan": return "artisan";
                case "merchant": return "merchant";
                case "healer": return "healer";
                case "preacher": return "preacher";
                case "notassigned": return null;
                default: return occ.Trim().ToLowerInvariant();
            }
        }

        // "a" or "an" for the word that follows, by its opening sound (good enough for our vocabulary).
        private static string A(string word)
        {
            if (string.IsNullOrEmpty(word)) return "a";
            return "aeiou".IndexOf(char.ToLowerInvariant(word[0])) >= 0 ? "an" : "a";
        }

        private static string JoinAnd(System.Collections.Generic.List<string> items)
        {
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return items[0] + " and " + items[1];
            return string.Join(", ", items.Take(items.Count - 1)) + ", and " + items[items.Count - 1];
        }

        private static bool AreAtWar(IFaction a, IFaction b)
        {
            try { return FactionManager.IsAtWarAgainstFaction(a, b); }
            catch { return false; }
        }

        private static string Name(Hero h) => h?.Name?.ToString() ?? "Unknown";

        // Individual game data lookups can throw on edge-case heroes; a missing fact should never
        // sink the whole situation block, so each is attempted independently.
        private static void Try(Action a) { try { a(); } catch { /* skip this fact */ } }
    }
}
