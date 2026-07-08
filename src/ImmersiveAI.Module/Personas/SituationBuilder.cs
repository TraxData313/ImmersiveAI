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
    /// This is the same block that gets written to the NPC's <c>current_situation_info.txt</c> and
    /// folded into the LLM prompt as the "Current situation" section, so what the player inspects
    /// on disk is exactly what the NPC "sees".
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
        /// The full environmental-facts block: when, where, who the speaker is, and who they are
        /// speaking with (standing and war/peace measured toward that partner).
        /// </summary>
        public static string Build(Hero speaker, Hero partner)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== As this conversation begins, the situation is:");
            sb.AppendLine($"When: {Timestamp()} — {TimeOfDay()}.");
            sb.AppendLine($"Where: {PlaceDescription(speaker)}.");

            sb.AppendLine();
            sb.AppendLine($"-- You ({Name(speaker)}) --");
            AppendHeroFacts(sb, speaker, includeFamily: true);
            AppendWhereabouts(sb, speaker);

            if (partner != null)
            {
                var tag = partner == Hero.MainHero ? " (the player you are speaking with)" : " (speaking with you)";
                sb.AppendLine();
                sb.AppendLine($"-- {Name(partner)}{tag} --");
                AppendHeroFacts(sb, partner, includeFamily: false);
                AppendRelationship(sb, speaker, partner);
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendHeroFacts(StringBuilder sb, Hero h, bool includeFamily)
        {
            if (h == null) return;

            var bits = new System.Collections.Generic.List<string>();
            Try(() => bits.Add(h.IsFemale ? "female" : "male"));
            Try(() => { if (h.Age > 0) bits.Add($"{h.Age:0} years old"); });
            Try(() => { var c = h.Culture?.Name?.ToString(); if (!string.IsNullOrWhiteSpace(c)) bits.Add(c); });
            Try(() => { var occ = h.Occupation.ToString(); if (!string.IsNullOrWhiteSpace(occ)) bits.Add(occ.ToLowerInvariant()); });
            if (bits.Count > 0) sb.AppendLine(string.Join(", ", bits) + ".");

            Try(() =>
            {
                var clan = h.Clan?.Name?.ToString();
                var kingdom = h.Clan?.Kingdom?.Name?.ToString() ?? h.MapFaction?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(clan) && !string.IsNullOrWhiteSpace(kingdom))
                    sb.AppendLine($"Clan {clan}, of {kingdom}.");
                else if (!string.IsNullOrWhiteSpace(clan))
                    sb.AppendLine($"Clan {clan}.");
                else if (!string.IsNullOrWhiteSpace(kingdom))
                    sb.AppendLine($"Sworn to {kingdom}.");
            });

            if (includeFamily)
            {
                Try(() =>
                {
                    var spouse = h.Spouse?.Name?.ToString();
                    if (!string.IsNullOrWhiteSpace(spouse)) sb.AppendLine($"Married to {spouse}.");
                });
                Try(() =>
                {
                    var kids = h.Children?.Where(c => c != null && c.IsAlive)
                                          .Select(c => c.Name?.ToString())
                                          .Where(n => !string.IsNullOrWhiteSpace(n))
                                          .ToList();
                    if (kids != null && kids.Count > 0)
                        sb.AppendLine("Children: " + string.Join(", ", kids) + ".");
                });
            }
        }

        private static void AppendWhereabouts(StringBuilder sb, Hero h)
        {
            Try(() =>
            {
                if (h.IsPrisoner) { sb.AppendLine("Currently held prisoner."); return; }

                var settlement = h.CurrentSettlement;
                if (settlement != null)
                {
                    sb.AppendLine($"Currently at {settlement.Name}.");
                    return;
                }

                var party = h.PartyBelongedTo;
                if (party != null)
                {
                    var leader = party.LeaderHero;
                    if (leader != null && leader != h)
                        sb.AppendLine($"Currently travelling with {leader.Name}'s party.");
                    else
                        sb.AppendLine("Currently travelling the roads.");
                }
            });
        }

        private static void AppendRelationship(StringBuilder sb, Hero speaker, Hero partner)
        {
            Try(() =>
            {
                int relation = speaker.GetRelation(partner);
                sb.AppendLine($"Your standing with {Name(partner)}: {PersonaBuilder.DescribeRelation(relation)} ({relation}).");
            });

            Try(() =>
            {
                var f1 = speaker.MapFaction;
                var f2 = partner.MapFaction;
                if (f1 == null || f2 == null) return;
                if (f1 == f2) { sb.AppendLine("You share the same faction."); return; }
                sb.AppendLine(AreAtWar(f1, f2)
                    ? $"Your factions ({f1.Name} and {f2.Name}) are at WAR."
                    : $"Your factions ({f1.Name} and {f2.Name}) are at peace.");
            });
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
