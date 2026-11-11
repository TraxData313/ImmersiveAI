using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveAI.Core.Handmaids
{
    /// <summary>
    /// One captive girl the player's men may find in a cleared bandit den (2026.11.11, Toni's
    /// design). Pure data: the game glue reads it to build her from a vanilla wanderer template,
    /// and later steps read <see cref="NativeTitle"/> for her prompt.
    /// </summary>
    public sealed class HandmaidGirl
    {
        public HandmaidGirl(string id, string name, string culture, string cultureName,
            string banditCulture, string templateId, string nativeTitle,
            string story, string skill, string teacher, string faceCode = "")
        {
            Id = id;
            Name = name;
            Culture = culture;
            CultureName = cultureName;
            BanditCulture = banditCulture;
            TemplateId = templateId;
            NativeTitle = nativeTitle;
            Story = story;
            Skill = skill;
            Teacher = teacher;
            FaceCode = faceCode ?? string.Empty;
        }

        /// <summary>Stable roster id, saved in the handmaid marker. Never rename one.</summary>
        public string Id { get; }

        /// <summary>Her first name, and her whole name: no "the X" epithet.</summary>
        public string Name { get; }

        /// <summary>The game's culture id of her own people (e.g. "sturgia").</summary>
        public string Culture { get; }

        /// <summary>Her people as the player reads it in the dev pick list (e.g. "Sturgia").</summary>
        public string CultureName { get; }

        /// <summary>The bandit culture id whose dens she is found in (e.g. "sea_raiders").</summary>
        public string BanditCulture { get; }

        /// <summary>The vanilla wanderer template she is cloned from (skills, traits, gear).</summary>
        public string TemplateId { get; }

        /// <summary>How she addresses the player in her own tongue (used by a later step).</summary>
        public string NativeTitle { get; }

        /// <summary>Her story line, with {title} unfilled.</summary>
        public string Story { get; }

        /// <summary>Her answer to "What can you do?", with {title} unfilled.</summary>
        public string Skill { get; }

        /// <summary>Her answer to "Who taught you to be so gentle?", with {title} unfilled.</summary>
        public string Teacher { get; }

        /// <summary>An optional fixed face: a game <c>&lt;BodyProperties .../&gt;</c> string.
        /// Empty means a random face from her template's range.</summary>
        public string FaceCode { get; }
    }

    /// <summary>The five girls, one per bandit culture that owns hideouts.</summary>
    public static class HandmaidRoster
    {
        public static readonly HandmaidGirl Niya = new HandmaidGirl(
            "niya", "Niya", "sturgia", "Sturgia", "sea_raiders", "spc_wanderer_sturgia_0", "Vladika moy",
            "I gathered herbs with my mother by the river. The raiders killed her, and the man I was promised to, and kept me because I can close a wound. All winter I bound up the men who slew them.",
            "I know herbs and wounds, {title}. I can keep your men alive.",
            "My mother. She said a soft answer turns away wrath, and she lived so until the day they killed her.");

        public static readonly HandmaidGirl Yasmin = new HandmaidGirl(
            "yasmin", "Yasmin", "aserai", "Aserai", "desert_bandits", "spc_wanderer_aserai_8", "Zawji",
            "My father sold pepper and saffron. They burned our storehouse for a debt he did not owe, and he died in it. I rode with a caravan to my uncle; the caravan never arrived.",
            "I can reckon weights and prices, {title}. No trader will cheat your purse while I watch it.",
            "An old holy man who kept the well by our street. He told me a quiet heart is worth more than my father's whole storehouse.");

        public static readonly HandmaidGirl Alys = new HandmaidGirl(
            "alys", "Alys", "vlandia", "Vlandia", "forest_bandits", "spc_wanderer_vlandia_9", "Messire",
            "I was born in this camp. My mother was taken; I was only born. She died two winters past, and I do not know what lies beyond these trees.",
            "I can cook for many from little, {title}, and mend, and move through a wood unheard.",
            "My mother. Even here, among them, she never raised her voice, and she taught me to pray while they slept.");

        public static readonly HandmaidGirl Nessa = new HandmaidGirl(
            "nessa", "Nessa", "battania", "Battania", "mountain_bandits", "spc_wanderer_battania_0", "M'fhear",
            "I was healer to a glen. The hill men brought a wounded chief and said: mend him or the village burns. I mended him. They burned it all the same, and kept me.",
            "I can set a bone and cool a fever, {title}.",
            "The old hermit who taught me my herbs. He said the hand that heals must not be proud.");

        public static readonly HandmaidGirl Ayla = new HandmaidGirl(
            "ayla", "Ayla", "khuzait", "Khuzait", "steppe_bandits", "spc_wanderer_khuzait_9", "Nökhör mini",
            "My father guarded caravans and I rode beside him, saving for my dowry. They fell on us at a well. He held them long enough for the others. Not for me.",
            "I can ride all day, {title}. I know horses, and the roads the caravans keep.",
            "My grandmother. She said the grass that bends is still standing after the storm.");

        /// <summary>All five, in the order the dev pick list shows them after the den's own girl.</summary>
        public static IReadOnlyList<HandmaidGirl> All { get; } = new[] { Niya, Yasmin, Alys, Nessa, Ayla };

        /// <summary>The girl a den with no known culture yields (a modded bandit clan, say).</summary>
        public static HandmaidGirl Fallback => Niya;

        /// <summary>The girl with this roster id, or null.</summary>
        public static HandmaidGirl? ById(string? id)
        {
            var key = (id ?? string.Empty).Trim();
            if (key.Length == 0) return null;
            return All.FirstOrDefault(g => string.Equals(g.Id, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The girl a den of this bandit culture yields, or null when no girl belongs to
        /// it. Accepts the bare id ("forest_bandits") or the xml form ("Culture.forest_bandits").</summary>
        public static HandmaidGirl? OwnOf(string? banditCultureId)
        {
            var key = Normalize(banditCultureId);
            if (key.Length == 0) return null;
            return All.FirstOrDefault(g => string.Equals(g.BanditCulture, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The girl for this den, never null: an unknown culture falls back safely.</summary>
        public static HandmaidGirl ForBanditCulture(string? banditCultureId) =>
            OwnOf(banditCultureId) ?? Fallback;

        /// <summary>The dev pick list's order: this den's own girl first (when there is one), then
        /// the rest in roster order.</summary>
        public static IReadOnlyList<HandmaidGirl> PickOrder(string? banditCultureId)
        {
            var own = OwnOf(banditCultureId);
            if (own == null) return All;
            var list = new List<HandmaidGirl> { own };
            list.AddRange(All.Where(g => g != own));
            return list;
        }

        private static string Normalize(string? id)
        {
            var s = (id ?? string.Empty).Trim();
            const string prefix = "Culture.";
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(prefix.Length);
            return s;
        }
    }
}
