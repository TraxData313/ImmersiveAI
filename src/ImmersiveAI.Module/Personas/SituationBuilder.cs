using System;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Builds the "current situation" — the environmental facts about a conversation the moment
    /// it begins: when and where it happens, how the speaker presently stands, and who they are
    /// speaking with. It is written as the NPC's OWN first-person awareness (no clinical headers,
    /// no narrator), the same block that gets written to <c>current_situation_info.txt</c> and folded
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
            var settlement = SettlementOf(npc);
            var name = settlement?.Name?.ToString() ?? string.Empty;
            if (name.Trim().Length > 0) return name;
            return HasPartyOnMap(npc) ? "the road" : "the open field";
        }

        // Where this hero truly stands: their own settlement, their party's, and only for someone
        // actually WITH the player the player's settlement. The old unconditional fallback to
        // Settlement.CurrentSettlement made a distant party's letter claim it was written in
        // whatever town the PLAYER stood in (the moving-writers bug, 2026.07.12).
        private static Settlement SettlementOf(Hero h)
        {
            if (h == null) return Settlement.CurrentSettlement;
            var s = h.CurrentSettlement ?? h.PartyBelongedTo?.CurrentSettlement;
            if (s != null) return s;
            bool withPlayer = h == Hero.MainHero
                || (h.PartyBelongedTo != null && h.PartyBelongedTo == TaleWorlds.CampaignSystem.Party.MobileParty.MainParty);
            return withPlayer ? Settlement.CurrentSettlement : null;
        }

        private static bool HasPartyOnMap(Hero h)
        {
            try { return h?.PartyBelongedTo != null; }
            catch { return false; }
        }

        /// <summary>Fuller sentence describing where the conversation happens, with settlement type
        /// and the faction that holds it, for the situation block and the prompt.</summary>
        private static string PlaceDescription(Hero speaker)
        {
            var settlement = SettlementOf(speaker);
            if (settlement == null)
                return HasPartyOnMap(speaker)
                    ? "upon the road, away from any town or castle"
                    : "out in the open field, away from any town or castle";

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
        /// away and on their mind — the framing a letter is written or read in.
        ///
        /// The whole block is the speaker's OWN first-person awareness now (2026.07.11). The setting
        /// and THE MOMENT ("And now X comes to me…") are joined with
        /// <see cref="ImmersiveAI.Core.Prompts.PromptBuilder.MeetingSeparator"/>: the prompt splits
        /// there to slot deep memory of the person right before their arrival; the situation FILE
        /// writer replaces it with a soft divider.</summary>
        public static string Build(Hero speaker, Hero partner, ModConfig? config, bool apart)
        {
            var sb = new StringBuilder();
            var name = Name(speaker);

            // The narration moves like a mind waking toward the moment: the setting, then how I stand
            // in it, then what has lately stirred the world — and only at the end (past the separator,
            // with memory between) the person themselves.
            sb.AppendLine($"This moment finds me, {name}. It is {TimeOfDay()} — {Timestamp()} — and I am {PlaceDescription(speaker)}.");

            // How they presently stand: charge, condition, company, war — identity itself (culture,
            // station, kin) lives up top with the persona and the family, never repeated here.
            var self = DescribeSelf(speaker);
            if (self.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(self);
            }

            // The passing weather of the heart: the day's humor and, for the women, the body's own
            // season — part of who they are this day, so it follows the self (see MoodTides).
            if (config == null || config.EnableMoodSwings)
            {
                var mood = BuildMood(speaker, config);
                if (mood.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(mood);
                }
            }

            // The trouble they themselves carry — the issue laid on them and any quest they gave —
            // so a villager asked "what ails you?" knows his own problem (see TroubleBuilder).
            var trouble = TroubleBuilder.Build(speaker, partner);
            if (trouble.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(trouble);
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

            // And past the separator, the person: who stands before me (or writes from afar), named
            // with what they are to me — my husband, my daughter, my liege — and how my heart leans.
            var meeting = BuildMeeting(speaker, partner, apart);
            if (meeting.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(ImmersiveAI.Core.Prompts.PromptBuilder.MeetingSeparator);
                sb.AppendLine(meeting);
            }

            return sb.ToString().TrimEnd();
        }

        // The moment itself: the arrival (or the far-away thought), the person, and where my heart
        // stands — the closing breath of the sheet, placed right after my memory of them.
        private static string BuildMeeting(Hero speaker, Hero partner, bool apart)
        {
            if (partner == null) return string.Empty;

            var sb = new StringBuilder();
            var them = Name(partner);
            var kin = FamilyBuilder.KinshipTo(speaker, partner);
            var appos = kin == null ? string.Empty : $", {kin},";

            if (apart)
                sb.AppendLine($"My thoughts turn to {them}{appos} who is far from me now — the road between us is long.");
            else if (partner == Hero.MainHero)
                sb.AppendLine($"And now {them}{appos} comes to me.");
            else
                sb.AppendLine($"And now {them}{appos} comes to speak with me.");

            var themDesc = DescribeOther(speaker, partner);
            if (themDesc.Length > 0)
                sb.AppendLine(themDesc);

            return sb.ToString().TrimEnd();
        }

        // How the speaker presently STANDS — charge, condition, company, war. Identity (culture,
        // station, gender, years) moved up to the persona head, and kin to the family block
        // (2026.07.11), so the sheet never tells them the same thing twice.
        private static string DescribeSelf(Hero h)
        {
            if (h == null) return string.Empty;
            var sentences = new System.Collections.Generic.List<string>();

            // Charge: a governor knows the place given into their keeping.
            Try(() =>
            {
                var kept = h.GovernorOf?.Settlement?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(kept))
                    sentences.Add($"The keeping of {kept} — its walls, its garrison, its people — is given into my hands: I am its governor.");
            });

            Try(() => { if (h.IsFemale && h.IsPregnant) sentences.Add("I carry a child within me."); });

            // The company they keep upon the map — named even inside walls, so a captain berthed in
            // a town still holds his command in mind (details live in the recall of one's company).
            // A named duty in another's warband (scout, surgeon…) is their place in it — their role.
            Try(() =>
            {
                if (h.IsPrisoner) { sentences.Add("I am held captive, a prisoner."); return; }
                var party = h.PartyBelongedTo;
                if (party == null) return;
                var leader = party.LeaderHero;
                int men = 0;
                Try(() => men = party.MemberRoster?.TotalManCount ?? 0);
                if (leader == h)
                    sentences.Add(men > 0
                        ? $"A warband of some {men} souls rides under my command, looking to me for bread and orders."
                        : "A warband rides under my command.");
                else if (leader != null)
                {
                    var duty = PartyDuty(h, party);
                    var dutyClause = duty == null ? "" : $", and I serve as its {duty}";
                    sentences.Add(men > 0
                        ? $"I ride with {leader.Name}'s warband, some {men} strong{dutyClause}."
                        : $"I ride with {leader.Name}'s warband{dutyClause}.");
                }
                else if (h.CurrentSettlement == null)
                    sentences.Add("I am upon the road.");
            });

            // A gathered army, if their company marches within one.
            Try(() =>
            {
                var army = h.PartyBelongedTo?.Army;
                if (army == null) return;
                var armyName = army.Name?.ToString() ?? "a gathered army";
                if (army.LeaderParty == h.PartyBelongedTo)
                    sentences.Add($"More than that: the banners of {armyName} march at my word.");
                else
                {
                    var armyLeader = army.LeaderParty?.LeaderHero?.Name?.ToString();
                    sentences.Add(armyLeader != null
                        ? $"My company marches within {armyName}, under {armyLeader}."
                        : $"My company marches within {armyName}.");
                }
            });

            // War pressing on this very moment: walls besieged, or a siege or raid of their own.
            Try(() =>
            {
                var s = h.CurrentSettlement;
                if (s != null && s.IsUnderSiege)
                {
                    sentences.Add($"And a shadow lies over this place: {s.Name} is under siege even now.");
                    return;
                }
                var party = h.PartyBelongedTo;
                if (party == null) return;
                var besieged = party.BesiegedSettlement;
                if (besieged != null) { sentences.Add($"My company lies encamped in siege about {besieged.Name}."); return; }
                if (party.MapEvent != null && party.MapEvent.IsRaid)
                    sentences.Add("My company has its hands in a raid even now.");
            });

            return string.Join(" ", sentences);
        }

        // The named duty this hero holds in the party they ride with — scout, surgeon, engineer,
        // quartermaster — or null when they hold none. Best-effort against the live roles.
        private static string PartyDuty(Hero h, TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            try
            {
                if (party.EffectiveScout == h) return "scout";
                if (party.EffectiveSurgeon == h) return "surgeon";
                if (party.EffectiveEngineer == h) return "engineer";
                if (party.EffectiveQuartermaster == h) return "quartermaster";
            }
            catch { /* roles unavailable */ }
            return null;
        }

        // The mood paragraph: deterministic in the soul and the campaign day (see MoodTides in Core),
        // so a reload rerolls no one's weather. The monthly season goes to women in their childbearing
        // years; a woman carrying a child gets her own carrying-season instead — the body is never
        // simply silent for her.
        private static string BuildMood(Hero h, ModConfig config)
        {
            try
            {
                var id = h?.StringId;
                if (string.IsNullOrWhiteSpace(id)) return string.Empty;

                bool seasonAllowed = (config == null || config.EnableWomensCycle) && h.IsFemale;
                bool pregnant = Safe(() => h.IsPregnant);
                bool withChild = seasonAllowed && pregnant;
                bool withCycle = seasonAllowed && !pregnant && h.Age >= 15 && h.Age < 50;

                int campaignDay = (int)CampaignTime.Now.ToDays;
                return ImmersiveAI.Core.Prompts.MoodTides.BuildNarration(id, campaignDay, withCycle, withChild);
            }
            catch { return string.Empty; }
        }

        private static bool Safe(Func<bool> f) { try { return f(); } catch { return false; } }

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
            else head = $"{them} stands before me";
            head += GenderAgeClause(gender, age, false);
            sentences.Add(head.TrimEnd() + ".");

            if (clan != null && kingdom != null) sentences.Add($"Their house is clan {clan}, sworn to {kingdom}.");
            else if (clan != null) sentences.Add($"Their house is clan {clan}.");
            else if (kingdom != null) sentences.Add($"They are sworn to {kingdom}.");

            // How far their name has traveled: what even a stranger would have heard of them.
            Try(() =>
            {
                float renown = partner.Clan?.Renown ?? 0f;
                if (renown >= 300f) sentences.Add("Their name is carried far across Calradia — word of their deeds travels ahead of them.");
                else if (renown >= 150f) sentences.Add("I have heard their name spoken before now; word of their deeds has begun to travel.");
            });

            Try(() =>
            {
                int relation = speaker.GetRelation(partner);
                sentences.Add($"Where my heart stands toward them: {PersonaBuilder.DescribeRelation(relation)} ({relation}).");
            });

            Try(() =>
            {
                var f1 = speaker.MapFaction;
                var f2 = partner.MapFaction;
                if (f1 == null || f2 == null) return;
                if (f1 == f2) { sentences.Add("We stand beneath the same banner."); return; }
                sentences.Add(AreAtWar(f1, f2)
                    ? $"Our peoples — {f1.Name} and {f2.Name} — are at war."
                    : $"Our peoples — {f1.Name} and {f2.Name} — are at peace.");
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
