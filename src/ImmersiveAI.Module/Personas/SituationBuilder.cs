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

            // Man and wife stand closer than any courtesy: the marriage bed, the children, and the
            // grand designs of the house are all one conversation between them.
            bool wedded = FamilyBuilder.AreWed(speaker, partner);
            if (wedded)
                sb.AppendLine("Between us there is no ceremony and nothing held back for propriety's sake: " +
                    "we are wed, and we speak as two who share one bed, one hearth, and one fate — tenderness " +
                    "and teasing, our children and our household, and the grand designs of our house alike.");

            var themDesc = DescribeOther(speaker, partner);
            if (themDesc.Length > 0)
                sb.AppendLine(themDesc);

            // The beholder's eye: when a great lord is met by someone far beneath their station whom
            // they barely know, what the eyes see IS the introduction — garb, arms, banner, following,
            // smashed down to one sentence, so a king receives an unknown as a king would, without a
            // single hard-coded manner forced on him.
            if (!apart)
            {
                var sight = FirstSightOfStranger(speaker, partner);
                if (sight.Length > 0) sb.AppendLine(sight);
            }

            return sb.ToString().TrimEnd();
        }

        // One sentence of what a high lord's eyes take in of an unknown caller. Only spoken when the
        // station gap is real (two clan tiers or more, or a crowned head one tier above) and the two
        // are strangers in the heart (standing well under friendship): a known friend of low birth is
        // met as a friend, and the sizing-up never repeats once a bond exists. (Anton's ask,
        // 2026.07.12: "he is king, you are unknown" must be SEEN, not hard-coded as rudeness.)
        private static string FirstSightOfStranger(Hero speaker, Hero partner)
        {
            try
            {
                if (speaker == null || partner == null || partner != Hero.MainHero) return string.Empty;
                if (speaker.Clan == null || !speaker.IsLord) return string.Empty;

                int myTier = speaker.Clan.Tier;
                int theirTier = partner.Clan?.Tier ?? 0;
                bool crowned = Safe(() => speaker.Clan.Kingdom != null && speaker.Clan.Kingdom.Leader == speaker);
                int gap = myTier - theirTier;
                if (!(gap >= 2 || (crowned && gap >= 1))) return string.Empty;

                int relation = 0;
                Try(() => relation = speaker.GetRelation(partner));
                if (relation >= 10) return string.Empty; // a bond already outweighs first sight

                var sb = new StringBuilder();
                sb.Append(crowned
                    ? "I am a crowned head among my people, and this caller stands far beneath my station. "
                    : "I stand high among the lords of the land, and this caller stands well beneath my station. ");

                sb.Append($"My eyes take them in as they come: {GarbWords(partner)}, bearing {ArmsWords(partner)}, {BannerWords(partner)}, {FollowingWords()}.");

                float renown = 0f;
                Try(() => renown = partner.Clan?.Renown ?? 0f);
                if (renown < 150f) sb.Append(" No word of their deeds has ever reached me.");

                sb.Append(" What welcome such a one merits — a great lord's ear is not owed to every caller — is mine alone to judge, by my own nature.");
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        // The war-harness, averaged over what armor they truly wear, smashed to a garb-word.
        private static string GarbWords(Hero h)
        {
            try
            {
                var eq = h.BattleEquipment;
                float sum = 0; int worn = 0;
                foreach (var slot in new[] { TaleWorlds.Core.EquipmentIndex.Head, TaleWorlds.Core.EquipmentIndex.Body,
                    TaleWorlds.Core.EquipmentIndex.Leg, TaleWorlds.Core.EquipmentIndex.Gloves, TaleWorlds.Core.EquipmentIndex.Cape })
                {
                    var item = eq[slot].Item;
                    if (item == null) continue;
                    sum += item.Tierf; worn++;
                }
                if (worn == 0) return "unarmored, in common clothes";
                float avg = sum / worn;
                if (avg < 1f) return "in rough, patched gear";
                if (avg < 2.5f) return "in plain traveling harness";
                if (avg < 4f) return "in serviceable mail";
                if (avg < 5f) return "in fine harness";
                return "in splendid armor fit for a great lord";
            }
            catch { return "plainly dressed"; }
        }

        // The best blade among the four weapon slots, as the eye would judge it.
        private static string ArmsWords(Hero h)
        {
            try
            {
                var eq = h.BattleEquipment;
                float best = -1f;
                foreach (var slot in new[] { TaleWorlds.Core.EquipmentIndex.Weapon0, TaleWorlds.Core.EquipmentIndex.Weapon1,
                    TaleWorlds.Core.EquipmentIndex.Weapon2, TaleWorlds.Core.EquipmentIndex.Weapon3 })
                {
                    var item = eq[slot].Item;
                    if (item != null && item.Tierf > best) best = item.Tierf;
                }
                if (best < 0f) return "no arms at all";
                if (best < 1f) return "a rusty, cheap blade";
                if (best < 2.5f) return "plain, well-worn arms";
                if (best < 4f) return "good steel";
                return "masterwork arms";
            }
            catch { return "arms I cannot judge"; }
        }

        private static string BannerWords(Hero h)
        {
            try
            {
                var item = h.BattleEquipment[TaleWorlds.Core.EquipmentIndex.ExtraWeaponSlot].Item;
                if (item != null && item.IsBannerItem)
                    return item.Tierf >= 4f ? "a storied banner at their back" : "a banner at their back";
            }
            catch { /* no banner read */ }
            return "no banner at their back";
        }

        // The following at their heels — the player's own party, sized in a lord's words.
        private static string FollowingWords()
        {
            try
            {
                int men = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
                if (men <= 1) return "and they come alone";
                if (men < 15) return $"with a handful at their back ({men})";
                if (men < 40) return $"at the head of a small band ({men})";
                if (men < 100) return $"at the head of a warband ({men})";
                return $"at the head of a strong warband ({men})";
            }
            catch { return "with what following I cannot tell"; }
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
                bool caravan = false;
                Try(() => caravan = party.IsCaravan);
                var company = caravan ? "trading caravan" : "warband";
                if (leader == h)
                {
                    sentences.Add(men > 0
                        ? (caravan
                            ? $"A trading caravan of some {men} souls goes upon its rounds under my hand — goods bought where they are cheap, sold where they are dear, and the road's dangers weighed at every turn."
                            : $"A warband of some {men} souls rides under my command, looking to me for bread and orders.")
                        : $"A {company} rides under my command.");
                    var held = HeldDuties(party, h);
                    if (held.Length > 0) sentences.Add(held);
                }
                else if (leader != null)
                {
                    var duties = PartyDuties(h, party);
                    var dutyClause = duties.Count == 0 ? "" : $", and I serve as its {JoinAnd(duties)}";
                    sentences.Add(men > 0
                        ? $"I ride with {leader.Name}'s {company}, some {men} strong{dutyClause}."
                        : $"I ride with {leader.Name}'s {company}{dutyClause}.");
                    foreach (var duty in duties) sentences.Add(DutySentence(duty, h));
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

        // The named duties this hero holds in the party they ride with — scout, surgeon, engineer,
        // quartermaster; one soul may carry several at once. Best-effort against the live roles.
        internal static System.Collections.Generic.List<string> PartyDuties(Hero h, TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            var duties = new System.Collections.Generic.List<string>();
            try
            {
                if (party.EffectiveScout == h) duties.Add("scout");
                if (party.EffectiveSurgeon == h) duties.Add("surgeon");
                if (party.EffectiveEngineer == h) duties.Add("engineer");
                if (party.EffectiveQuartermaster == h) duties.Add("quartermaster");
            }
            catch { /* roles unavailable */ }
            return duties;
        }

        // The duties joined for a label ("scout and surgeon"), or null when they hold none.
        internal static string PartyDuty(Hero h, TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            var duties = PartyDuties(h, party);
            return duties.Count == 0 ? null : JoinAnd(duties);
        }

        // What the duty MEANS, in the holder's own words, weighed with how good they honestly are
        // at its craft — so a scout asked "can we outrun them?" knows both that the question is his
        // to answer and how far his own eyes are to be trusted.
        private static string DutySentence(string duty, Hero h)
        {
            switch (duty)
            {
                case "scout":
                    return "As its scout, the road, the pace of the march, the tracks upon the ground, and the first sight of any banner on the horizon are mine to judge; my eyes are " +
                        CraftsBuilder.WordFor(h, TaleWorlds.Core.DefaultSkills.Scouting) + " at the craft.";
                case "surgeon":
                    return "As its surgeon, every wound in the company passes through my hands, and how fast the hurt mend is my charge; my leechcraft is " +
                        CraftsBuilder.WordFor(h, TaleWorlds.Core.DefaultSkills.Medicine) + ".";
                case "engineer":
                    return "As its engineer, engines, earthworks, and the breaking or holding of walls are mine; my craft is " +
                        CraftsBuilder.WordFor(h, TaleWorlds.Core.DefaultSkills.Engineering) + ".";
                case "quartermaster":
                    return "As its quartermaster, the stores, the wagons, the men's wages, and what the company carries pass through my hands; my reckoning is " +
                        CraftsBuilder.WordFor(h, TaleWorlds.Core.DefaultSkills.Steward) + ".";
                default:
                    return string.Empty;
            }
        }

        // For a LEADER: who holds the duties in their own company — a captain should know his own
        // scout from his surgeon, and it lets him speak of his people's crafts truly.
        private static string HeldDuties(TaleWorlds.CampaignSystem.Party.MobileParty party, Hero leader)
        {
            // Grouped by holder — one soul may carry several duties, and a captain should say
            // "Alandra is my scout and surgeon", not name her twice.
            var order = new System.Collections.Generic.List<Hero>();
            var held = new System.Collections.Generic.Dictionary<Hero, System.Collections.Generic.List<string>>();
            void Note(Func<Hero> pick, string duty)
            {
                Try(() =>
                {
                    var who = pick();
                    if (who == null || who == leader) return;
                    if (!held.TryGetValue(who, out var list))
                    {
                        list = new System.Collections.Generic.List<string>();
                        held[who] = list;
                        order.Add(who);
                    }
                    list.Add(duty);
                });
            }
            Note(() => party.EffectiveScout, "scout");
            Note(() => party.EffectiveSurgeon, "surgeon");
            Note(() => party.EffectiveEngineer, "engineer");
            Note(() => party.EffectiveQuartermaster, "quartermaster");
            if (order.Count == 0) return string.Empty;
            var parts = new System.Collections.Generic.List<string>();
            foreach (var who in order)
                parts.Add($"{who.Name} is my {JoinAnd(held[who])}");
            return "In my company, " + JoinAnd(parts) + ".";
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

            // The game stamps the player "Lord" from their first day, but a landless newcomer with
            // ten riders is no noble in anyone's eyes yet — name them what they truly are.
            if (partner == Hero.MainHero) occ = PlayerStation(partner) ?? occ;

            var head = $"{them} is";
            if (culture != null && occ != null) head += $" {A(culture)} {culture} {occ}";
            else if (culture != null) head += $" of {culture} stock";
            else if (occ != null) head += $" {A(occ)} {occ}";
            else head = $"{them} stands before me";
            head += GenderAgeClause(gender, age, false);
            sentences.Add(head.TrimEnd() + ".");

            var house = HouseLine(partner, clan, kingdom);
            if (house != null) sentences.Add(house);
            else if (kingdom != null) sentences.Add($"They are sworn to {kingdom}.");

            // How far their name has traveled: what even a stranger would have heard of them.
            Try(() =>
            {
                float renown = partner.Clan?.Renown ?? 0f;
                if (renown >= 300f) sentences.Add("Their name is carried far across Calradia — word of their deeds travels ahead of them.");
                else if (renown >= 150f) sentences.Add("I have heard their name spoken before now; word of their deeds has begun to travel.");
            });

            // Their own house, as the world knows it — whom they are wed to (every living spouse:
            // polygamy-honest), which children are theirs, whose child they are (Anton, 2026.07.15).
            Try(() =>
            {
                var family = FamilyBuilder.DescribeFamilyOf(partner, speaker);
                if (family.Length > 0) sentences.Add(family);
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

        // What the player truly is in the world's eyes. Sworn to a crown as a vassal makes a noble
        // (or a crowned head); a mercenary contract makes a sellsword captain; free and unsworn makes
        // an adventurer, or the captain of whatever band actually rides behind them. Null falls back
        // to the plain occupation word. (Anton's ask, 2026.07.12: no "noble" at clan tier 0.)
        private static string PlayerStation(Hero h)
        {
            try
            {
                var clan = h.Clan;
                if (clan == null) return "free adventurer";
                if (clan.Kingdom != null)
                {
                    if (Safe(() => clan.IsUnderMercenaryService)) return "sellsword captain";
                    if (Safe(() => clan.Kingdom.Leader == h)) return h.IsFemale ? "crowned queen" : "crowned king";
                    return h.IsFemale ? "noblewoman" : "nobleman";
                }
                int men = 0;
                Try(() => men = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0);
                if (men >= 40) return "free captain at the head of a warband";
                if (men >= 15) return "free captain of a small band";
                return "free adventurer";
            }
            catch { return null; }
        }

        // The partner's house, told with how far its name has truly risen — a tier-0 banner is
        // "newly raised", not presumed a noble house — and honest about a mercenary contract.
        private static string HouseLine(Hero partner, string clan, string kingdom)
        {
            if (clan == null) return null;
            var sworn = kingdom == null ? null
                : Safe(() => partner.Clan?.IsUnderMercenaryService == true)
                    ? $", riding under contract in the pay of {kingdom}"
                    : $", sworn to {kingdom}";
            string fame = null;
            Try(() => fame = ClanStandingWords(partner.Clan?.Tier ?? 0));
            return $"Their house is clan {clan}{sworn}" + (fame == null ? "." : $" — {fame}.");
        }

        // The weight a clan's name carries, by tier — the same ladder the lords themselves live by.
        private static string ClanStandingWords(int tier)
        {
            if (tier <= 0) return "a banner newly raised, its name not yet known to anyone";
            if (tier == 1) return "a young name, only beginning to be spoken";
            if (tier == 2) return "a small house on the rise";
            if (tier == 3) return "a house of real standing among the lords";
            if (tier == 4) return "an established house whose word carries weight";
            if (tier == 5) return "a great house, its name known across the realms";
            return "among the greatest houses of Calradia";
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
            // An unsworn clan IS its own map faction — never say "clan X, sworn to X" (Cadfin find).
            if (k != null && cl != null && k == cl) k = null;
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
