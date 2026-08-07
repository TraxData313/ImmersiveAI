using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ImmersiveAI.Core.Courtship
{
    /// <summary>
    /// One quiet requirement a soul holds for the one they would wed — written once by the
    /// matchmaker's ledger (see <see cref="MatchmakerLedger"/>), persisted in memories.json, and
    /// folded into her sheet privately ("I know these in my bones; I never recite them as a list").
    /// <see cref="Text"/> is her own first-person phrasing; <see cref="Check"/> is the small
    /// checkable predicate behind it, in the ledger's closed DSL — or "heart" for what no ledger
    /// can weigh.
    /// </summary>
    public sealed class CourtshipAsk
    {
        public string Text { get; set; } = string.Empty;
        public string Check { get; set; } = string.Empty;
    }

    public enum AskCheckKind { Heart, Gold, Renown, Skill, Trait, Relation, ClanTier }

    /// <summary>A parsed check: what to weigh, against what bar. Anything unreadable degrades to
    /// Heart — her phrasing is never lost, only the ledger's arithmetic.</summary>
    public sealed class AskCheck
    {
        public AskCheckKind Kind { get; set; } = AskCheckKind.Heart;
        /// <summary>Skill or trait name, when the kind carries one.</summary>
        public string Name { get; set; } = string.Empty;
        public int Threshold { get; set; }
    }

    /// <summary>The suitor's plain facts at judgment time — gathered by the game layer from live
    /// data, judged here. The lookups return null for names the world does not know, which sends
    /// that ask to the heart's judgment instead of a false verdict.</summary>
    public sealed class SuitorFacts
    {
        public int Gold;
        public int Renown;
        /// <summary>Her live regard for the suitor (−100..100).</summary>
        public int Relation;
        public int ClanTier;
        public Func<string, int?>? SkillOf;
        public Func<string, int?>? TraitOf;
    }

    /// <summary>
    /// The ledger's closed check language: <c>gold &gt;= N</c>, <c>renown &gt;= N</c>,
    /// <c>skill &lt;Name&gt; &gt;= N</c>, <c>trait &lt;Name&gt; &gt;= N</c>, <c>relation &gt;= N</c>,
    /// <c>tier &gt;= N</c>, or <c>heart</c>. Parsing is deliberately lenient (spacing, commas in
    /// numbers, "&gt;" for "&gt;=") and everything else degrades to heart.
    /// </summary>
    public static class CourtshipChecks
    {
        private static readonly Regex CheckShape = new Regex(
            @"^\s*(?<kind>gold|renown|relation|tier|skill|trait)\s*(?<name>[^\d>=<]*?)\s*>=?\s*(?<num>-?[\d,\. ]+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static AskCheck Parse(string? check)
        {
            var raw = (check ?? string.Empty).Trim();
            if (raw.Length == 0) return new AskCheck();

            var m = CheckShape.Match(raw);
            if (!m.Success) return new AskCheck(); // "heart", prose, anything else → the heart's judgment

            var numText = m.Groups["num"].Value.Replace(",", "").Replace(".", "").Replace(" ", "");
            if (!long.TryParse(numText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n))
                return new AskCheck();
            int threshold = n > int.MaxValue ? int.MaxValue : n < int.MinValue ? int.MinValue : (int)n;

            var name = m.Groups["name"].Value.Trim();
            switch (m.Groups["kind"].Value.ToLowerInvariant())
            {
                case "gold": return new AskCheck { Kind = AskCheckKind.Gold, Threshold = threshold };
                case "renown": return new AskCheck { Kind = AskCheckKind.Renown, Threshold = threshold };
                case "relation": return new AskCheck { Kind = AskCheckKind.Relation, Threshold = threshold };
                case "tier": return new AskCheck { Kind = AskCheckKind.ClanTier, Threshold = threshold };
                case "skill":
                    return name.Length == 0 ? new AskCheck()
                        : new AskCheck { Kind = AskCheckKind.Skill, Name = name, Threshold = threshold };
                case "trait":
                    return name.Length == 0 ? new AskCheck()
                        : new AskCheck { Kind = AskCheckKind.Trait, Name = name, Threshold = threshold };
                default: return new AskCheck();
            }
        }

        /// <summary>Whether the suitor meets this ask — or null when only her own heart can judge
        /// it (heart-asks, and any skill/trait name the world does not recognize).</summary>
        public static bool? IsMet(CourtshipAsk ask, SuitorFacts facts)
        {
            if (ask == null || facts == null) return null;
            var check = Parse(ask.Check);
            switch (check.Kind)
            {
                case AskCheckKind.Gold: return facts.Gold >= check.Threshold;
                case AskCheckKind.Renown: return facts.Renown >= check.Threshold;
                case AskCheckKind.Relation: return facts.Relation >= check.Threshold;
                case AskCheckKind.ClanTier: return facts.ClanTier >= check.Threshold;
                case AskCheckKind.Skill:
                    var skill = facts.SkillOf?.Invoke(check.Name);
                    return skill.HasValue ? skill.Value >= check.Threshold : (bool?)null;
                case AskCheckKind.Trait:
                    var trait = facts.TraitOf?.Invoke(check.Name);
                    return trait.HasValue ? trait.Value >= check.Threshold : (bool?)null;
                default: return null;
            }
        }
    }
}
