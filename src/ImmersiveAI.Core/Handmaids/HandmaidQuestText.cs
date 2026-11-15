using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveAI.Core.Handmaids
{
    /// <summary>
    /// Every player-visible word of the den quest (2026.11.14, the owner's ask): a village notable
    /// asks the player to clear the den of one bandit people, and in that den his men find the
    /// captive girl of that people. The notable never knew her: his story is the village's own loss,
    /// linked to hers only loosely. Since the owner's second ask the same day it is a VILLAGE quest,
    /// and only some villages carry the trouble at a time (<see cref="VillageOffersNow"/>). Placeholders are filled through the helpers below, so a line
    /// never reaches the player with a brace left in it.
    /// </summary>
    public static class HandmaidQuestText
    {
        // ── The numbers ────────────────────────────────────────────────────────────────────

        /// <summary>The giver's regard gained when the den is cleared (vanilla's hideout quest gives 5).</summary>
        public const int RelationOnSuccess = 5;

        /// <summary>NO COIN (the owner, 2026.11.14): the asker is a village notable with none to
        /// spare, speaking for the local folk. What the player earns is his regard, and theirs.</summary>
        public const int RewardGold = 0;

        /// <summary>From this many men in his party he is spoken to as a captain; below it, as a warrior.</summary>
        public const int MenForACaptain = 10;

        // ── The den's name ─────────────────────────────────────────────────────────────────

        // The game's own names (SandBox spclans.xml), used when the live faction cannot be read.
        private static readonly Dictionary<string, string> FallbackNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sea_raiders"] = "Sea Raiders",
            ["desert_bandits"] = "Desert Bandits",
            ["forest_bandits"] = "Forest Bandits",
            ["mountain_bandits"] = "Mountain Bandits",
            ["steppe_bandits"] = "Steppe Bandits",
        };

        /// <summary>The den type as the player reads it: the bandit faction's own in-game name when
        /// given, else the game's English name for that culture, else "bandits".</summary>
        public static string DenName(string? banditCulture, string? gameName = null)
        {
            if (!string.IsNullOrWhiteSpace(gameName)) return gameName!.Trim();
            var key = Normalize(banditCulture);
            return FallbackNames.TryGetValue(key, out var name) ? name : "Bandits";
        }

        /// <summary>"Sea Raiders'" for a plural name, "Bandit Clan's" for any other.</summary>
        public static string Possessive(string? name)
        {
            var s = (name ?? string.Empty).Trim();
            if (s.Length == 0) return "bandits'";
            return s.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? s + "'" : s + "'s";
        }

        // ── The quest log ──────────────────────────────────────────────────────────────────

        public const string TitleTemplate = "Clear the {den_of} den";

        public const string StartLogTemplate =
            "{giver} of {village} told you that the {den} have been preying on the villagers, and asked you to clear their den. The place is marked on your map.";

        public const string MarkedNoticeTemplate = "{giver} has marked the {den_of} den on your map.";

        public const string RetargetLogTemplate =
            "That den of the {den} stands empty now, but they keep another. It is marked on your map.";

        public const string WaitLogTemplate =
            "No den of the {den} stands anywhere now. When they gather again, the place will be marked on your map.";

        public const string FoundAgainLogTemplate = "The {den} have gathered again. Their den is marked on your map.";

        /// <summary>His one line when the den is cleared (sent as word, the way vanilla's hideout
        /// quest sends its thanks).</summary>
        public const string GiverThanks = "It is done, then. The roads are quieter for it. I have no coin to give you, but the folk here will not forget it, and neither will I.";

        public const string SuccessLogTemplate = "You cleared the {den_of} den. {giver} sent word: \"" + GiverThanks + "\"";

        public const string SuccessNoGiverLogTemplate = "You cleared the {den_of} den. The folk {giver} spoke for will not forget it.";

        public const string AbandonLogTemplate = "You told {giver} you could not see to the {den} after all.";

        public const string AbandonNoGiverLogTemplate = "You gave up the hunt for the {den_of} den.";

        // ── Finding it ─────────────────────────────────────────────────────────────────────

        /// <summary>The settlement menu's way out when the giver can no longer be asked.</summary>
        public const string MenuGiveUpTemplate = "Give up the hunt for the {den_of} den";

        public const string GiveUpConfirmTitle = "Give up the den?";

        public const string GiveUpConfirmBodyTemplate =
            "{giver} is gone, and nobody waits on the {den_of} den now. Give up the hunt? You may take such a task again in any village that asks.";

        public const string GiveUpYes = "Give it up";

        public const string GiveUpNo = "Keep at it";

        // ── The giver's talk ───────────────────────────────────────────────────────────────

        public const string PlayerAskTemplate = "I hear you are looking for someone to deal with the {den}.";

        public const string GiverAskTemplate =
            "They den somewhere not far from here. I have no coin to spare for it, but {why}, and the folk here are looking for someone to help them. Go quickly: if they took anyone alive, there may still be time.";

        /// <summary>Why he asks THIS traveller: the men at his back, or the look of him.</summary>
        public const string WhyCaptain = "you have armed men at your back";
        public const string WhyWarrior = "you have the look of a warrior";

        /// <summary>His ask, fitted to the one before him.</summary>
        public static string GiverAsk(int menInParty) =>
            GiverAskTemplate.Replace("{why}", menInParty >= MenForACaptain ? WhyCaptain : WhyWarrior);

        // ── As a vanilla village issue (2026.11.15, the owner: "a normal quest, like those the
        // villages give"): the game's own issue flow asks for these in this order, after the player's
        // "Do you have any problems?": his story, the player's question, his ask, the player's yes.

        /// <summary>How long the trouble stands in his village before he stops asking (vanilla's
        /// bandit-base issue: 15 days).</summary>
        public const int IssueDays = 15;

        /// <summary>The time to clear it once taken (vanilla's bandit-base quest: 30 days).</summary>
        public const int QuestDays = 30;

        public const string IssueAcceptTemplate = "So you need someone to deal with the {den}?";

        public const string DescriptionTemplate = "{giver} of {village} wants the {den_of} den cleared.";

        public const string GiverNewsTemplate = "Have you dealt with the {den} yet?";

        public const string TimedOutLogTemplate = "You did not clear the {den_of} den in time. {giver} has sought help elsewhere.";

        public const string RumorTemplate = "I hope someone deals with the {den} soon. {giver} says they took someone alive.";

        public static string IssueAccept(string? denName) => Fill(IssueAcceptTemplate, denName);
        public static string Description(string? denName, string? giver, string? village) => Fill(DescriptionTemplate, denName, giver, village);
        public static string GiverNews(string? denName) => Fill(GiverNewsTemplate, denName);
        public static string TimedOutLog(string? denName, string? giver) => Fill(TimedOutLogTemplate, denName, giver);
        public static string Rumor(string? denName, string? giver) => Fill(RumorTemplate, denName, giver);

        public const string PlayerAccept = "I will clear that den.";

        public const string PlayerNotNow = "Not now.";

        public const string GiverAccepted = "Then go with our thanks. One of our lads will mark the place on your map.";

        // While the quest runs: a word on it, and the way to give it up (vanilla's quest log has
        // no abandon button in this version, so the giver is where it is given back).
        public const string PlayerStillTemplate = "The {den_of} den still stands. I have not forgotten.";

        public const string GiverStill = "Good. The roads will be glad of it.";

        public const string PlayerGiveUpTemplate = "I cannot see to the {den} after all.";

        public const string GiverGiveUp = "Then the roads must wait for someone else. No harm done.";

        /// <summary>DevMode: marks the village's own den type among the five options.</summary>
        public const string DevOwnMark = " (this village's own)";

        // ── His story, one per den type ────────────────────────────────────────────────────

        private static readonly Dictionary<string, string> Stories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sea_raiders"] =
                "Sea raiders came up the river a few days ago and struck a herb-gatherers' hamlet. Our men found a woman dead by the water with her basket still on her arm, and a young man beside her. There was a daughter too, they say. Nobody has seen her since.",
            ["desert_bandits"] =
                "Desert bandits took a caravan on the road a few days past. A man of this village still waits for his niece, who rode with it after her father's storehouse burned. Only the camels came back.",
            ["forest_bandits"] =
                "Forest bandits came out of the trees a few days ago. We found a charcoal burner cut down beside his kiln, and a bread basket lying in the path. He had a daughter who carried it to him every day. She is not among the dead.",
            ["mountain_bandits"] =
                "Hill bandits burned a village in the glen some days ago. The shepherds say they came down first asking for the healer, for a wounded chief. We found the ashes, and no healer among the dead.",
            ["steppe_bandits"] =
                "Steppe bandits fell on a caravan at the well a few days ago. The guard captain held them off so the wagons could run, and we buried him there. He always rode with his daughter. We did not find her.",
        };

        /// <summary>The bandit cultures the quest can name, in roster order.</summary>
        public static IReadOnlyList<string> DenCultures => HandmaidRoster.All.Select(g => g.BanditCulture).ToList();

        /// <summary>His story for this den type; an unknown culture gets a plain line, never empty.</summary>
        public static string StoryFor(string? banditCulture) =>
            Stories.TryGetValue(Normalize(banditCulture), out var s)
                ? s
                : "Bandits have been preying on the villagers here, and the roads are not safe.";

        /// <summary>Whether this den type has a story of its own (and so a girl of its own).</summary>
        public static bool HasStory(string? banditCulture) => Stories.ContainsKey(Normalize(banditCulture));

        /// <summary>DevMode: the den types in the order the giver's talk lists them, the village's own first.</summary>
        public static IReadOnlyList<string> DevOrder(string? villagesOwn) =>
            HandmaidRoster.PickOrder(villagesOwn).Select(g => g.BanditCulture).ToList();

        // ── Fillers ────────────────────────────────────────────────────────────────────────

        /// <summary>Fills {den}, {den_of}, {giver} and {village}. A blank giver or village reads as a
        /// plain word, so nothing is left in braces.</summary>
        public static string Fill(string? template, string? denName, string? giver = null, string? village = null)
        {
            var den = string.IsNullOrWhiteSpace(denName) ? "bandits" : denName!.Trim();
            var s = template ?? string.Empty;
            s = s.Replace("{den_of}", Possessive(den));
            s = s.Replace("{den}", den);
            s = s.Replace("{giver}", string.IsNullOrWhiteSpace(giver) ? "A notable" : giver!.Trim());
            s = s.Replace("{village}", string.IsNullOrWhiteSpace(village) ? "the village" : village!.Trim());
            return s;
        }

        public static string Title(string? denName) => Fill(TitleTemplate, denName);

        public static string PlayerAsk(string? denName, bool devVillagesOwn = false) =>
            Fill(PlayerAskTemplate, denName) + (devVillagesOwn ? DevOwnMark : string.Empty);

        public static string StartLog(string? denName, string? giver, string? village) => Fill(StartLogTemplate, denName, giver, village);

        public static string MarkedNotice(string? denName, string? giver) => Fill(MarkedNoticeTemplate, denName, giver);

        public static string RetargetLog(string? denName) => Fill(RetargetLogTemplate, denName);

        public static string WaitLog(string? denName) => Fill(WaitLogTemplate, denName);

        public static string FoundAgainLog(string? denName) => Fill(FoundAgainLogTemplate, denName);

        public static string SuccessLog(string? denName, string? giver, bool giverStillHere) =>
            Fill(giverStillHere ? SuccessLogTemplate : SuccessNoGiverLogTemplate, denName, giver);

        public static string AbandonLog(string? denName, string? giver, bool giverStillHere) =>
            Fill(giverStillHere ? AbandonLogTemplate : AbandonNoGiverLogTemplate, denName, giver);

        public static string MenuGiveUp(string? denName) => Fill(MenuGiveUpTemplate, denName);

        public static string GiveUpConfirmBody(string? denName, string? giver) => Fill(GiveUpConfirmBodyTemplate, denName, giver);

        public static string PlayerStill(string? denName) => Fill(PlayerStillTemplate, denName);

        public static string PlayerGiveUp(string? denName) => Fill(PlayerGiveUpTemplate, denName);

        private static string Normalize(string? id)
        {
            var s = (id ?? string.Empty).Trim();
            const string prefix = "Culture.";
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(prefix.Length);
            return s;
        }
    }
}
