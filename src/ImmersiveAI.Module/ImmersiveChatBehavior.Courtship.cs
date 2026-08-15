using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI
{
    /// <summary>
    /// Marriage by courtship — the wedding handshake (2026.08.07, Anton's ask). The strike_bargain
    /// mold applied to the biggest thing a bond can become: an NPC walks her OWN road toward the
    /// player (Warmth → Devotion → Ready → Betrothed → Wed, persisted in memories.json so the
    /// save-scoped snapshots rewind a courtship for free), moved by her own hand (tend_courtship)
    /// under hard rails — relation floors, one step a day, the STATION GATE (required player clan
    /// tier from her station, minus the charm slack a fully-won heart earns), and her OWN written
    /// misgivings (weigh_misgivings — set down by her hand when marriage truly enters the talk,
    /// laid to rest only by her own judgment; the matchmaker's checkable asks were retired
    /// 2026.08.08, "no robotic bargains"). A soul with a real lived story is SEEDED once from that
    /// story, so the feature's arrival never erases a love already spoken. Noble kin demand a
    /// bride-price (bless_marriage on the clan's head — the second bargain, ±MarriageDowryHagglePercent
    /// around the world's own reckoning, which is her clan's renown as vanilla's own barter prices
    /// it). THE ONE LAW, inherited: talk alone never betroths and never weds — the tools only LAY
    /// the moment, the only doors are the confirm popups, and lay AND seal both re-run every hard
    /// rule. The wedding itself is the REAL game marriage (a companion bride is first graduated to
    /// lordship by vanilla's own companion-to-lord shape, so the cutscene, the log entry the world
    /// gossips about, and the kin lines all follow for free). Refusals never name a threshold — the
    /// Sibuga floor lesson, held by Core tests.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        // Souls whose seeding/matchmaker calls are mid-flight — one at a time each. Touched from
        // the game thread (live trunk) AND worker threads (the letter flow), so entry and exit are
        // atomic under a lock: the check-then-act is one motion, never a race.
        private readonly HashSet<string> _courtshipBusy = new HashSet<string>();
        private readonly object _courtshipBusyLock = new object();

        private bool TryBeginCourtshipWork(string npcId)
        {
            lock (_courtshipBusyLock)
            {
                if (_courtshipBusy.Contains(npcId)) return false;
                _courtshipBusy.Add(npcId);
                return true;
            }
        }

        private void EndCourtshipWork(string npcId)
        {
            lock (_courtshipBusyLock) _courtshipBusy.Remove(npcId);
        }

        // A blessing sealed while the BRIDE'S own exchange is mid-flight would be clobbered by her
        // turn's end-of-exchange save (it holds a stale memory instance loaded before the seal).
        // The paid truth therefore also lives here until folded: SaveMemory folds it into ANY
        // instance of hers being written that does not carry it yet — so the gold can never buy
        // a blessing the world forgets. Session-scoped; the fold is idempotent.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (double Day, int Price, string NewsBeat)>
            _pendingBlessingFolds = new System.Collections.Concurrent.ConcurrentDictionary<string, (double, int, string)>();

        // Called by SaveMemory for every write: folds a sealed blessing into a stale instance.
        private static void FoldPendingBlessing(Hero npc, NpcMemory memory)
        {
            try
            {
                if (npc == null || memory == null) return;
                if (!_pendingBlessingFolds.TryGetValue(npc.StringId, out var fold)) return;
                if (memory.FamilyBlessingDay >= 0) return; // already carried — nothing to heal
                memory.FamilyBlessingDay = fold.Day;
                memory.FamilyBlessingPrice = fold.Price;
                if (!string.IsNullOrEmpty(fold.NewsBeat)
                    && !memory.RecentTurns.Any(t => t != null && t.PlayerLine == fold.NewsBeat))
                    AddSilentInnerBeat(memory, npc, fold.NewsBeat);
            }
            catch { /* the direct write already landed once; the fold is the safety net */ }
        }

        // The road's own notice colors — Anton's color language (2026.08.08 evening): warm rose
        // when the heart moves WELL (steps forward, misgivings answered, a clear heart), frost
        // blue when something freezes or a doubt appears/returns — so the left-side log reads the
        // courtship's weather at a glance, and every movement leaves a line in it.
        private static readonly Color RoadColor = new Color(0.93f, 0.62f, 0.72f, 1f);
        private static readonly Color FrostColor = new Color(0.58f, 0.76f, 0.95f, 1f);
        private static readonly Color SealGreen = new Color(0.45f, 0.85f, 0.45f, 1f);
        private static readonly Color SealGrey = new Color(0.65f, 0.65f, 0.65f, 1f);

        // ------------------------- gates: when the hands ride -------------------------

        // The troth's hand rides the live reply trunk — and the letter-answer flow (byLetter: a
        // heart moves in writing too; only the wedding day itself refuses to be laid on paper).
        // Same shape as the bargain: whisper and tool keyed to one tally.
        private bool CanTendTroth(Hero npc, bool byLetter = false)
        {
            if (!_config.EnableConversationMarriage) return false;
            if (!(_client is IToolChatClient)) return false;
            try
            {
                // "May this hand ride at all" is the trunk's question, so it is asked as the trunk:
                // whether a married player's acquaintance may take one more step of feeling. What
                // is reached for is judged again, properly, in the resolver.
                return TrothBlockReason(npc, forWedding: false, _config, forHand: false) == TrothBlock.None
                    && (byLetter || IsCoLocated(npc));
            }
            catch { return false; }
        }

        // The blessing's hand rides only for the head of a house whose kinswoman (or kinsman) is
        // betrothed to the player with the blessing still unspoken — and never across a war. By
        // letter too (Anton's ask, 2026.08.08 — where in the world would one even FIND the father?):
        // the blessing and its price can be settled in writing, sealed when his reply arrives.
        private bool CanBlessTroth(Hero npc, out Hero? bride, bool byLetter = false)
        {
            bride = null;
            if (!_config.EnableConversationMarriage || !_config.MarriageNeedsFamilyConsent) return false;
            if (!(_client is IToolChatClient)) return false;
            try
            {
                var player = Hero.MainHero;
                if (npc == null || player == null || !npc.IsAlive || npc.IsPrisoner) return false;
                var clan = npc.Clan;
                if (clan == null || clan == Clan.PlayerClan || clan.Leader != npc) return false;
                if (npc.MapFaction != null && player.MapFaction != null
                    && npc.MapFaction.IsAtWarWith(player.MapFaction)) return false;
                if (!byLetter && !IsCoLocated(npc)) return false;

                foreach (var kin in clan.Heroes)
                {
                    if (kin == null || kin == npc || !kin.IsAlive) continue;
                    var known = MemoryIndex.Get(NpcPaths.MemoryFile(kin), _memoryStore);
                    if (known == null || (CourtshipStage)known.CourtshipStage != CourtshipStage.Betrothed) continue;
                    var memory = LoadMemory(kin);
                    if (memory.CourtshipStage == CourtshipStage.Betrothed && memory.FamilyBlessingDay < 0)
                    {
                        bride = kin;
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        // ------------------------- the hard rules (checked at lay AND seal) -------------------------

        private enum TrothBlock
        {
            None, Gone, NotFree, PromisedElsewhere, TooYoung, NotTheCustom, NotForMarriage,
            CompanionBarred, UnhiredWanderer, NotHere, WarCamp, AtWar, WorldRefuses, BlessingMissing,
        }

        // Marry Anyone compatibility (Anton runs it on his saves): with a polygamy mod loaded, the
        // player's OWN standing marriage no longer bars a new courtship — the LAW remains the
        // marriage model, which such mods patch, so the seal still asks the world's (patched) rules;
        // without one, vanilla's model refuses at the seal, honestly. Her side stays as the world
        // wills it either way. Detected once by loaded assembly name (module DLLs load before any
        // campaign), same soft-reflection stance as the MCM bridge.
        private static bool? _polygamyLoaded;
        private static bool PolygamyLoose()
        {
            if (_polygamyLoaded.HasValue) return _polygamyLoaded.Value;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName()?.Name ?? string.Empty;
                    if (name.IndexOf("MarryAnyone", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _polygamyLoaded = true;
                        return true;
                    }
                }
            }
            catch { }
            _polygamyLoaded = false;
            return false;
        }

        // The world's own law over the pair — vanilla's own suitability where it can speak (nobles),
        // our matching checks where vanilla has no words (companion brides). forWedding adds the
        // legs only a wedding needs; the courtship road itself may be walked by an unhired wanderer.
        /// <param name="forHand">Whether what is being reached for is ABOUT marriage — readiness,
        /// the betrothal, the wedding, a blessing. Defaults true, so every caller written before
        /// 2026.08.15 keeps exactly the behaviour it had. Only the trunk's own feeling-steps pass
        /// false, and only that lets the wall below stand where it belongs.</param>
        private static TrothBlock TrothBlockReason(Hero npc, bool forWedding, ModConfig? config = null,
            bool forHand = true)
        {
            try
            {
                var cfg = config ?? SubModule.Config;
                var player = Hero.MainHero;
                if (npc == null || player == null || !npc.IsAlive || !player.IsAlive
                    || npc.IsPrisoner || npc == player) return TrothBlock.Gone;
                if (npc.IsFemale == player.IsFemale) return TrothBlock.NotTheCustom;
                if (npc.Spouse != null) return TrothBlock.NotFree;
                // The player's own marriage bars a new road only in a monogamous world — with a
                // polygamy mod (Marry Anyone) loaded, the road opens and the model stays the judge.
                //
                // AND SINCE 2026.08.15 IT BARS ONLY WHAT IS ABOUT MARRIAGE. With the lover's road
                // open, the trunk — warmth, then love — may be walked by a married man's
                // acquaintance, because the trunk is nothing but feelings and a feeling has never
                // needed anyone's leave; the road's own comment has said "the heart is free, the
                // HAND has rails" since the day it was written. The wall now stands at readiness,
                // which is the first rung that speaks of a hand at all. Without this the whole
                // post-marriage batch was unreachable by the one player it exists for: no new
                // courtship could start, so no heart could reach the fork, so nobody could ever be
                // offered anything. Gated on the feature, so a game with it turned off behaves
                // exactly as it did before.
                if (player.Spouse != null && !PolygamyLoose())
                {
                    bool loversOpen = cfg.EnableLoversRoad && cfg.EnableConversationMarriage;
                    if (forHand || !loversOpen) return TrothBlock.NotFree;
                }
                if (npc.IsChild || npc.Age < 18f || player.Age < 18f) return TrothBlock.TooYoung;
                if (npc.IsNotable || npc.IsMinorFactionHero || npc.IsTemplate) return TrothBlock.NotForMarriage;

                bool wanderer = npc.IsWanderer;
                if (!wanderer && !npc.IsLord) return TrothBlock.NotForMarriage;
                if (wanderer && !cfg.AllowCompanionMarriage) return TrothBlock.CompanionBarred;

                if (forWedding)
                {
                    if (wanderer && npc.CompanionOf != Clan.PlayerClan) return TrothBlock.UnhiredWanderer;
                    // The world weds no one out of an army's camp or a battle joined (vanilla's own leg).
                    if (npc.PartyBelongedTo?.MapEvent != null || npc.PartyBelongedTo?.Army != null) return TrothBlock.WarCamp;
                    if (MobileParty.MainParty?.MapEvent != null || MobileParty.MainParty?.Army != null) return TrothBlock.WarCamp;
                    if (!wanderer)
                    {
                        if (npc.MapFaction != null && player.MapFaction != null
                            && npc.MapFaction.IsAtWarWith(player.MapFaction)) return TrothBlock.AtWar;
                        // Vanilla's whole verdict — kinship within three generations, vows elsewhere,
                        // quest vetoes, clans — asked directly, so our seal can never outrun the game's law.
                        try
                        {
                            if (!Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(player, npc))
                                return TrothBlock.WorldRefuses;
                        }
                        catch { }
                    }
                }
                return TrothBlock.None;
            }
            catch { return TrothBlock.Gone; }
        }

        // How her own mind reads a blocked moment — honest, hers, numberless.
        private static string TrothBlockForNpc(TrothBlock block, Hero npc)
        {
            switch (block)
            {
                case TrothBlock.NotFree: return "one of us is already bound in marriage; this road is closed while that vow stands.";
                case TrothBlock.PromisedElsewhere: return "their word is already promised to another; I will not lay mine beside a standing troth.";
                case TrothBlock.TooYoung: return "the world does not wed those not yet grown; the years must pass first.";
                case TrothBlock.NotTheCustom: return "such a match is not the custom of this world, and the world will not seal it.";
                case TrothBlock.NotForMarriage: return "my station is not one the world weds; this road is not open to me.";
                case TrothBlock.CompanionBarred: return "the custom of this world does not wed a free sword; this road is closed to me.";
                case TrothBlock.UnhiredWanderer: return "I stand outside their house and their company — my hand can be given only from within it. Were they to take me into their service first, the road to a wedding would open.";
                case TrothBlock.NotHere: return "we do not truly stand together just now; such things pass face to face.";
                case TrothBlock.WarCamp: return "the drums of war stand too near — an army's camp or a battle joined is no hour for a wedding; a freer hour must come.";
                case TrothBlock.AtWar: return "our realms are at war; no wedding crosses that line while it holds.";
                case TrothBlock.WorldRefuses: return "the world itself refuses the match as things now stand — kinship, vows, or the law of houses bars the way.";
                case TrothBlock.BlessingMissing:
                    var head = npc?.Clan?.Leader?.Name?.ToString();
                    return string.IsNullOrEmpty(head)
                        ? "the blessing of my kin is not given, and my hand cannot pass without it."
                        : $"the blessing of my kin is not given — it is {head}'s to give, and my hand cannot pass without it. Let them seek {head}'s word.";
                default: return "one of us is not free in the world's eyes just now.";
            }
        }

        // The same moments phrased for the player's notices.
        private static string TrothBlockForPlayer(TrothBlock block, Hero npc)
        {
            switch (block)
            {
                case TrothBlock.NotFree: return "one of you is already married";
                case TrothBlock.PromisedElsewhere: return "your word is already given to another";
                case TrothBlock.TooYoung: return "the world weds only the grown";
                case TrothBlock.NotTheCustom: return "the world does not seal such a match";
                case TrothBlock.NotForMarriage: return "their station is not one the world weds";
                case TrothBlock.CompanionBarred: return "companion marriage is turned off in the mod options";
                case TrothBlock.UnhiredWanderer: return "they must first be taken into your service";
                case TrothBlock.NotHere: return "you no longer truly stand together";
                case TrothBlock.WarCamp: return "an army's camp or a battle stands too near";
                case TrothBlock.AtWar: return "your realms are at war";
                case TrothBlock.BlessingMissing: return $"their kin's blessing is not yet given{(npc?.Clan?.Leader != null ? $" — seek {npc.Clan.Leader.Name}" : string.Empty)}";
                default: return "the world refuses the match as things stand";
            }
        }

        // Whether this bride's house must bless the match before a wedding can be sealed.
        private bool BlessingRequired(Hero npc)
        {
            try
            {
                if (!_config.MarriageNeedsFamilyConsent) return false;
                if (npc == null || npc.IsWanderer) return false; // her own word is her family
                var clan = npc.Clan;
                return clan != null && clan != Clan.PlayerClan
                    && clan.Leader != null && clan.Leader.IsAlive
                    && clan.Leader != npc && clan.Leader != Hero.MainHero;
            }
            catch { return false; }
        }

        // The player may walk many roads, but may hold only ONE standing troth at a time — with
        // a LIVING soul: the dead cannot release a promise by their own hand, so a betrothed who
        // died must never wedge every future troth (review find, 2026.08.08).
        private bool PlayerPromisedToAnother(Hero npc)
        {
            try
            {
                var root = NpcPaths.CampaignRoot;
                foreach (var known in MemoryIndex.All(root, NpcPaths.MemoryFileName, _memoryStore))
                {
                    if (known.NpcId == npc.StringId) continue;
                    if ((CourtshipStage)known.CourtshipStage != CourtshipStage.Betrothed) continue;
                    var other = FindAliveHero(known.NpcId);
                    if (other != null && other.IsAlive) return true;
                }
                return false;
            }
            catch { return false; }
        }

        // ------------------------- the road's live facts -------------------------

        private static int StationTierOf(Hero npc)
        {
            try
            {
                bool clanless = npc.IsWanderer || npc.Clan == null || npc.CompanionOf != null;
                bool ruling = !clanless && npc.Clan?.Kingdom != null && npc.Clan.Kingdom.RulingClan == npc.Clan;
                return CourtshipRoad.StationTier(clanless, ruling, npc.Clan?.Tier ?? 0);
            }
            catch { return 1; }
        }

        private CourtshipRoad.StepFacts RoadFactsOf(Hero npc, NpcMemory memory)
        {
            var now = CampaignTime.Now.ToDays;
            return new CourtshipRoad.StepFacts
            {
                Stage = memory.CourtshipStage,
                Relation = GetStanding(npc),
                DaysSinceForwardStep = memory.CourtshipStepDay < 0 ? double.PositiveInfinity : now - memory.CourtshipStepDay,
                PlayerClanTier = Clan.PlayerClan?.Tier ?? 0,
                HerStationTier = StationTierOf(npc),
                CharmSlack = _config.CourtshipCharmSlack,
                MisgivingsWeighed = memory.MisgivingsWeighed,
                OpenMisgivings = CourtshipMisgivings.OpenCount(memory.CourtshipMisgivings),
                DaysBetrothed = memory.BetrothedGameDay < 0 ? -1 : now - memory.BetrothedGameDay,
                MinBetrothalDays = _config.MinBetrothalDays,
            };
        }

        // ------------------------- the sheet's courtship sections -------------------------

        // Her private road-and-misgivings section — rides EVERY sheet while a road is walked (a
        // betrothed woman writing a letter knows she is betrothed), not only when the tool rides.
        private string BuildRoadTerms(Hero npc, NpcMemory memory)
        {
            try
            {
                if (!_config.EnableConversationMarriage) return string.Empty;
                if (memory.CourtshipStage <= CourtshipStage.None || memory.CourtshipStage >= CourtshipStage.Wed)
                    return string.Empty;
                // A soul already wed (to anyone — vanilla barter, another mod, the world's own
                // turnings) wears no courtship section: the kin lines carry the marriage.
                if (npc?.Spouse != null) return string.Empty;
                // Nor does one who took the other branch: this section would go on telling her she
                // is walking toward a wedding, while her own lover's section says plainly that
                // there is no wedding in it. One sheet, one truth (2026.08.15).
                if (memory.LoverBond == LoverBond.Lover) return string.Empty;

                var misgivings = memory.CourtshipMisgivings
                    .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Text))
                    .Select(m => new CourtshipText.MisgivingView { Text = m.Text, Settled = m.Settled, Note = m.SettledNote })
                    .ToList();

                return CourtshipText.RoadSection(
                    Hero.MainHero?.Name?.ToString() ?? "the traveler",
                    memory.CourtshipStage,
                    misgivings,
                    memory.MisgivingsWeighed,
                    BlessingRequired(npc),
                    memory.FamilyBlessingDay >= 0,
                    npc?.Clan?.Leader?.Name?.ToString() ?? string.Empty);
            }
            catch { return string.Empty; }
        }

        // The head of the house's suitor case — only while the blessing's hand truly rides.
        private string BuildSuitorTerms(Hero npc, Hero bride)
        {
            try
            {
                if (!BlessingRails(bride, out int reckoning, out int min, out int max)) return string.Empty;
                bool haggle = _config.MarriageDowryHagglePercent > 0;
                var kinWord = bride.IsFemale ? "a daughter of our house" : "a son of our house";
                return CourtshipText.SuitorTerms(
                    Hero.MainHero?.Name?.ToString() ?? "the traveler",
                    bride.Name?.ToString() ?? "our kin",
                    kinWord, reckoning, min, max, haggle);
            }
            catch { return string.Empty; }
        }

        // The bride-price rails: vanilla's own family reckoning (her clan's renown, softened for an
        // older bride by the same cubic relief the barter uses), haggled within the configured rail.
        private bool BlessingRails(Hero bride, out int reckoning, out int min, out int max)
        {
            reckoning = min = max = 0;
            try
            {
                if (bride == null) return false;
                float renown = bride.Clan?.Renown ?? 0f;
                float comesOfAge = 18f;
                try { comesOfAge = Campaign.Current.Models.AgeModel.HeroComesOfAge; } catch { }
                float over = Math.Max(0f, bride.Age - comesOfAge);
                float relief = -2f * (float)Math.Pow(Math.Min(0f, 20f - over), 3);
                reckoning = Math.Max(500, (int)Math.Round(renown - relief));

                double rail = Math.Max(0, Math.Min(90, _config.MarriageDowryHagglePercent)) / 100.0;
                min = (int)Math.Ceiling(reckoning * (1 - rail));
                max = (int)Math.Floor(reckoning * (1 + rail));
                return true;
            }
            catch { return false; }
        }

        // ------------------------- the resolvers (the tools' hands) -------------------------

        // A silent inner beat added to the LIVE memory the turn speaks from — so the end-of-turn
        // save carries it and can never clobber it (the live-instance discipline every mid-reply
        // hand on memory follows).
        private static void AddSilentInnerBeat(NpcMemory memory, Hero npc, string line)
        {
            memory.AddTurn(new ConversationTurn
            {
                Speaker = ConversationTurn.InnerSpeaker,
                PlayerLine = line,
                NpcLine = string.Empty,
                GameDay = CampaignTime.Now.ToDays,
                CalradiaTime = SituationBuilder.Timestamp(),
                Place = SituationBuilder.Place(npc),
            });
        }

        private string ResolveTendCourtship(Core.Llm.ToolCall call, Hero npc, Tools.TrothTool.Tally? troth, NpcMemory? liveMemory)
        {
            try
            {
                if (troth == null)
                    return "This is not the moment for the road of my heart — I stay in the talk.";
                if (troth.Acted)
                    return "My heart already moved this very breath — one step in a talk is enough, and I need not move twice.";

                var memory = liveMemory ?? LoadMemory(npc);
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var move = Tools.TrothTool.ParseMove(call);
                var word = Tools.TrothTool.ParseWord(call);
                var stage = memory.CourtshipStage;

                if (move == null)
                    return "I did not truly move — my heart holds where it stands.";

                if (move == "apart")
                {
                    // THE BOND STEPS BACK FIRST (2026.08.15). If she is his without vows, that is
                    // the deepest thing standing between them, and a step back is a step back from
                    // THAT — not from a courtship stage underneath it, which would leave her
                    // simultaneously cooled and still his, and be no truthful state at all. One
                    // step and it is over, because there was never anything holding it; her own
                    // tool description tells her as much before she can reach for this.
                    if (memory.LoverBond == LoverBond.Lover)
                        return EndBondByHerHand(npc, memory, troth, word);

                    if (stage <= CourtshipStage.None)
                        return "There is no road between us to step back from; my heart stands where it always stood.";
                    if (stage >= CourtshipStage.Wed)
                        return CourtshipText.ForwardRefusal(CourtshipRoad.StepVerdict.NoRoadFurther, playerName);

                    var to = stage - 1;
                    memory.CourtshipStage = to;
                    bool brokeTroth = stage == CourtshipStage.Betrothed;
                    if (brokeTroth) memory.BetrothedGameDay = -1;
                    troth.Acted = true; troth.SteppedBack = true; troth.NewStage = to; troth.Word = word;
                    AddSilentInnerBeat(memory, npc, CourtshipText.StepBackBeat(playerName, stage, to, word));
                    SaveMemory(npc, memory);
                    // A letter's heart-movement stays sealed until the letter arrives (the courier's
                    // law) — no notice spoils words still on the road.
                    if (!troth.ByLetter) NotifyRoadStep(npc, to, forward: false, brokeTroth: brokeTroth);
                    MirrorRomance(npc, to);
                    return "My heart has stepped back, and it is set down. I speak from where I now truly stand — and I owe no explanation beyond what I choose to give.";
                }

                // closer —
                var target = stage + 1;

                // Every refusal below is answered TWICE over: to her, in her own numberless words
                // plus the rail that words are not a wedding; and to the PLAYER, in a plain line
                // saying what was reached for and why nothing was sealed. Before 2026.08.15 only
                // the first half existed, and a refused reach was completely invisible — a player
                // watched a soul narrate a wedding in a temple, saw no popup, and had no way at all
                // to learn that the world had said no (Steam, rmanicky). A rail nobody can see is
                // indistinguishable from a broken mod.
                string Refused(string herWords, string playerReason)
                {
                    if (!troth.ByLetter) NotifyRoadRefused(npc, target, playerReason);
                    return herWords + " " + CourtshipText.WordsDoNotWed;
                }

                // Readiness is the first rung that speaks of a hand, so it is the first that a
                // standing marriage of the player's own may bar.
                var block = TrothBlockReason(npc, forWedding: target == CourtshipStage.Wed, _config,
                    forHand: target >= CourtshipStage.Ready);
                if (block != TrothBlock.None)
                    return Refused("The world stands in the way: " + TrothBlockForNpc(block, npc),
                        TrothBlockForPlayer(block, npc));
                if (target == CourtshipStage.Betrothed && PlayerPromisedToAnother(npc))
                    return Refused("The world stands in the way: " + TrothBlockForNpc(TrothBlock.PromisedElsewhere, npc),
                        TrothBlockForPlayer(TrothBlock.PromisedElsewhere, npc));
                if (target == CourtshipStage.Wed && BlessingRequired(npc) && memory.FamilyBlessingDay < 0)
                    return Refused("The world stands in the way: " + TrothBlockForNpc(TrothBlock.BlessingMissing, npc),
                        TrothBlockForPlayer(TrothBlock.BlessingMissing, npc));

                var verdict = CourtshipRoad.JudgeForward(RoadFactsOf(npc, memory));
                if (verdict != CourtshipRoad.StepVerdict.Allowed)
                    return Refused(CourtshipText.ForwardRefusal(verdict, playerName),
                        CourtshipText.ForwardRefusalForPlayer(verdict));

                if (target == CourtshipStage.Betrothed)
                {
                    troth.Acted = true; troth.LaidBetrothal = true; troth.Word = word;
                    if (!troth.ByLetter) NotifyRoadActivity(npc, "lays their promise before you…");
                    return troth.ByLetter
                        ? "The promise is laid in this very letter: when it reaches their hands, it will stand before them — to take, or to let lie, by their own hand. Nothing is settled until they choose, and I do not press."
                        : "The moment is laid: when my words here are done, my promise will stand before them — to take, or to let lie, by their own hand. Nothing is settled until they choose, and I do not press.";
                }
                if (target == CourtshipStage.Wed)
                {
                    if (troth.ByLetter)
                        return "A wedding day is not laid on paper — such a thing is done face to face, hand in hand. I may say so in my letter, warmly, and long for the meeting.";
                    troth.Acted = true; troth.LaidWedding = true; troth.Word = word;
                    NotifyRoadActivity(npc, "reaches for the wedding day…");
                    return "The day is laid: when my words here are done, our wedding will stand before them — to seal, or to let lie, by their own hand. Nothing is settled until they choose.";
                }

                // An inner step of her own road.
                memory.CourtshipStage = target;
                memory.CourtshipStepDay = CampaignTime.Now.ToDays;
                troth.Acted = true; troth.SteppedForward = true; troth.NewStage = target; troth.Word = word;
                AddSilentInnerBeat(memory, npc, CourtshipText.StepBeat(playerName, target, word));
                SaveMemory(npc, memory);
                if (!troth.ByLetter) NotifyRoadStep(npc, target, forward: true);
                MirrorRomance(npc, target);
                // Arriving at readiness, she is told what readiness now MEANS in her own hands —
                // the same in-the-moment telling as the misgivings' last rest (Anton, 2026.08.09).
                var whatFollows = target == CourtshipStage.Ready
                    ? " From here I wait to be asked: let the word be spoken between us and I say yes gladly, and lay my promise before them by my own hand — the sealing of it is theirs alone."
                    : string.Empty;
                return $"It is so, and it is set down: {CourtshipText.StagePhrase(target, playerName)}. I speak now from this truth — warmly, in my own way — and I need not name the change aloud unless my heart moves me to.{whatFollows}";
            }
            catch { return "The moment does not allow it; I let the matter rest."; }
        }

        // The misgivings' hand: her own writing upon what weighs on her heart before marriage.
        // Rides on the troth tally (same gate as tend_courtship); mutates the LIVE memory the turn
        // speaks from (the live-instance discipline) and saves at once, so the end-of-turn save can
        // never clobber it. Notices stay quiet for letters (the courier's seal law).
        private string ResolveWeighMisgivings(Core.Llm.ToolCall call, Hero npc, Tools.TrothTool.Tally? troth, NpcMemory? liveMemory)
        {
            try
            {
                if (troth == null)
                    return "This is not the moment for the weighing of my heart — I stay in the talk.";

                var memory = liveMemory ?? LoadMemory(npc);
                var list = memory.CourtshipMisgivings;
                var action = Tools.MisgivingTool.ParseAction(call);
                var text = Tools.MisgivingTool.ParseText(call);
                var note = Tools.MisgivingTool.ParseNote(call);
                bool quiet = troth.ByLetter;

                // The two hands, untangled when they arrive crossed — the misgiving written into
                // her light word and the word into the misgiving's place (live, 2026.08.09). Only
                // where a misgiving is being POINTED AT, and only when the swap is the sole reading
                // that finds one; set_down writes new lines and can never be untangled this way.
                if (action == Tools.MisgivingTool.ActSettle || action == Tools.MisgivingTool.ActRelease
                    || action == Tools.MisgivingTool.ActRevise || action == Tools.MisgivingTool.ActReopen)
                {
                    bool settledSide = action == Tools.MisgivingTool.ActReopen;
                    if (CourtshipMisgivings.HandsCameSwapped(list, text, note, settledSide))
                    {
                        var swap = text; text = note; note = swap;
                        ModLog.Info($"weigh_misgivings ({action}): the misgiving and the note came crossed — untangled.");
                    }
                }

                int Open() => CourtshipMisgivings.OpenCount(list);
                int Total() => CourtshipMisgivings.TotalCount(list);

                // The moment the last doubt falls silent, the hand's own answer tells her what is
                // now open to her (Anton's ask, 2026.08.09). The sheet says it too, but a sheet is
                // read once at the top of a long prompt; THIS arrives in the very breath she acts,
                // and the arrival is exactly when a heart decides. Tailored to where her road
                // actually stands, and it never promises what the rails may still refuse.
                string NothingStandsNow()
                {
                    if (Open() > 0) return string.Empty;
                    if (memory.CourtshipStage >= CourtshipStage.Betrothed) return string.Empty;
                    return memory.CourtshipStage == CourtshipStage.Ready
                        ? " And my heart already stands ready and waits to be asked: let them speak the word between us and I say yes gladly, and lay my promise before them by my own hand — the sealing of it is theirs alone."
                        : " And nothing of mine bars the road now. If my heart has truly come to the place where I would give my hand were the word spoken, I own that in this same breath and set it down by my own hand — I would not have them ask me blind.";
                }

                switch (action)
                {
                    case Tools.MisgivingTool.ActSetDown:
                        if (CourtshipMisgivings.IsNone(text))
                        {
                            if (Open() > 0)
                                return "What I set down before still stands written — a clear heart is not declared, it is earned: I lay each to rest as life answers it, or strike out one that proved empty.";
                            memory.MisgivingsWeighed = true;
                            SaveMemory(npc, memory);
                            if (!quiet && Total() == 0)
                                NotifyMisgivings(npc, "weighs their heart about a life together — and finds it clear.", RoadColor);
                            return "It is weighed and set down: nothing stands in me — my heart is clear on this. I speak on from that truth." + NothingStandsNow();
                        }
                        else
                        {
                            int added = CourtshipMisgivings.SetDown(list, text);
                            if (added == 0)
                                return Open() >= Core.Courtship.CourtshipMisgivings.MaxMisgivings
                                    ? "I already carry as many open as a heart can honestly hold at once — before I set down another, I first lay one to rest or strike out one that proved empty."
                                    : "Nothing new was set down — what I named, I carry already.";
                            memory.MisgivingsWeighed = true;
                            SaveMemory(npc, memory);
                            if (!quiet) NotifyMisgivings(npc,
                                $"sets down what weighs on their heart about a life together ({Open()} of {Total()} standing).",
                                FrostColor);
                            return $"It is set down, in my own words — {Open()} now stand{(Open() == 1 ? "s" : string.Empty)} in me. The list lives with me: I speak of them openly, and while any stands, my hand waits.";
                        }

                    case Tools.MisgivingTool.ActSettle:
                    {
                        var settled = CourtshipMisgivings.Settle(list, text, note);
                        if (settled == null)
                        {
                            // Reaching for one already at rest is not a failure and must not be
                            // answered as one — she would only try again, or doubt her own hand.
                            if (CourtshipMisgivings.FindBestMatch(list, text, m => m.Settled) != null)
                                return "That one I laid to rest already — it stands answered, and a " +
                                    "thing answered is not answered twice. I say so warmly and speak on.";
                            return "No misgiving of mine matches those words — I lay nothing to rest " +
                                "that I did not set down. If I meant one of the ones I truly hold, I " +
                                "reach again with the MISGIVING'S own words in its own place, and what " +
                                "answered it in the note beside — never the two the other way about.";
                        }
                        SaveMemory(npc, memory);
                        if (!quiet)
                        {
                            var tail = string.IsNullOrWhiteSpace(settled.SettledNote) ? string.Empty : $" — “{settled.SettledNote}”";
                            // The player is told the door is open in the same breath she is, so
                            // "now would be the hour to ask" never has to be guessed at.
                            var clear = memory.CourtshipStage < CourtshipStage.Betrothed
                                ? " — their heart is clear, and they wait to be asked."
                                : " — their heart is clear.";
                            NotifyMisgivings(npc, Open() == 0
                                ? $"lays their last misgiving to rest{tail}{clear}"
                                : $"lays a misgiving to rest{tail} ({Open()} of {Total()} still stand).",
                                RoadColor);
                        }
                        return Open() == 0
                            ? "It is laid to rest — and with it, nothing stands in me any longer. My heart is clear, and I may say so." + NothingStandsNow()
                            : $"It is laid to rest, with my word on what answered it. {Open()} still stand{(Open() == 1 ? "s" : string.Empty)} in me.";
                    }

                    case Tools.MisgivingTool.ActRelease:
                    {
                        var released = CourtshipMisgivings.Release(list, text);
                        if (released == null)
                            return "No misgiving of mine matches those words, so nothing was struck out.";
                        SaveMemory(npc, memory);
                        if (!quiet) NotifyMisgivings(npc, Open() == 0
                            ? "strikes a misgiving out — it was never truly theirs; nothing stands in their heart now."
                            : $"strikes a misgiving out — it was never truly theirs ({Open()} of {Total()} still stand).",
                            RoadColor);
                        return "It is struck out — not answered, simply no longer mine. My list holds only what my heart truly asks." + NothingStandsNow();
                    }

                    case Tools.MisgivingTool.ActRevise:
                    {
                        var revised = CourtshipMisgivings.Revise(list, text, note);
                        if (revised == null)
                            return "No misgiving of mine matches those words, so nothing was reworded.";
                        SaveMemory(npc, memory);
                        if (!quiet) NotifyMisgivings(npc, "turns a misgiving over, and words it anew.", ActivityColor);
                        return "It is reworded — the same weight, seen truer. I speak on.";
                    }

                    case Tools.MisgivingTool.ActReopen:
                    {
                        var reopened = CourtshipMisgivings.Reopen(list, text);
                        if (reopened == null)
                            return "Nothing I had laid to rest matches those words — nothing returned.";
                        SaveMemory(npc, memory);
                        if (!quiet) NotifyMisgivings(npc,
                            $"takes an old misgiving up again ({Open()} of {Total()} standing).",
                            FrostColor);
                        return "It stands in me again, and I own it honestly — better a doubt spoken than a peace pretended.";
                    }

                    default:
                        // Nothing was done — and she is told plainly WHY and with what words to
                        // reach again, because the round budget still allows it. A silent "I did
                        // not move" here is how three misgivings were laid to rest three times over
                        // and none of them moved (live, 2026.08.09).
                        return "My hand did not close on anything — I must name what I am doing with " +
                            "one of these words exactly: " + Tools.MisgivingTool.ActSetDown + ", " +
                            Tools.MisgivingTool.ActSettle + ", " + Tools.MisgivingTool.ActRelease + ", " +
                            Tools.MisgivingTool.ActRevise + ", " + Tools.MisgivingTool.ActReopen +
                            ". If I truly meant to lay one to rest or to set one down, I reach again " +
                            "now with the right word and the misgiving's own words; otherwise I let " +
                            "it be and stay in the talk.";
                }
            }
            catch { return "The moment does not allow it; I let the matter rest."; }
        }

        // The misgivings' own notices — rose when the heart clears (settled, struck out, clear),
        // frost blue when a doubt appears or returns. DisplayMessage lines live on in the message
        // log, so the whole weather of the courtship stays readable after the fact.
        private void NotifyMisgivings(Hero npc, string doing, Color color)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage($"{name} {doing}", color)));
            }
            catch { /* the notice is a nicety */ }
        }

        private string ResolveBlessLay(Core.Llm.ToolCall call, Hero npc, Tools.TrothTool.BlessTally? bless)
        {
            try
            {
                if (bless == null || bless.Bride == null)
                    return "This is not the moment for my house's blessing — I let the talk carry on.";
                if (bless.Laid)
                    return "The blessing already lies before them, laid this very breath — theirs to seal or let lie, and I need not lay it twice.";

                var player = Hero.MainHero;
                var bride = bless.Bride;
                if (bride == null || !bride.IsAlive)
                    return "The one whose match we would speak of is beyond such talk now; I let the matter rest.";
                if (npc?.MapFaction != null && player?.MapFaction != null
                    && npc.MapFaction.IsAtWarWith(player.MapFaction))
                    return "Our banners are at war with theirs; no gold buys the blessing of an enemy, and I say so plainly.";
                if (!BlessingRails(bride, out int reckoning, out int min, out int max))
                    return "No honest reckoning can be made for this just now; I let the matter rest.";

                int price = Tools.TrothTool.ParsePrice(call) ?? reckoning;
                if (price < 0)
                    return "No such price makes sense; I let it pass and keep to plain dealing.";
                // The rails hold, and the refusal never hands the floor over as a ready-made offer.
                if (price < min)
                    return $"That lies beneath what our name can accept, and I know my own bounds — I do not answer with my lowest. I answer in my own words with a figure their words and standing have honestly earned, nearer the custom's reckoning of {reckoning}, and I yield ground only as they earn it, step by step.";
                if (price > max)
                    return $"That is more than custom lets a house ask — I name instead a fair figure near the reckoning of {reckoning}, and I say it in my own words.";
                if (player == null || player.Gold < price)
                    return $"Their purse cannot carry it: {price} denars is more than they hold just now. No blessing can be laid — I may say so plainly, and leave the door open for a richer day.";

                bless.Laid = true;
                bless.Price = price;
                if (!bless.ByLetter) NotifyRoadActivity(npc, "weighs the suitor of their house…");
                return bless.ByLetter
                    ? $"It is laid in this very letter: my blessing on the match, at {price} denars as the bride-price. When it reaches their hands the offer will stand before them, to seal or let lie by their own hand; nothing is settled until they choose, and I do not press."
                    : $"It is laid: my blessing on the match, at {price} denars as the bride-price. When my words here are done the offer will stand before them, to seal or let lie by their own hand; nothing is settled until they choose, and I do not press.";
            }
            catch { return "The moment does not allow it; I let the matter rest."; }
        }

        // ------------------------- mirrors, notices -------------------------

        // Mirrors the road into the game's own romance ledger for LORDS (wanderers have no vanilla
        // romance to mirror): any state ≥ MatchMadeByFamily removes her from the daily NPC-marriage
        // lottery and from AI marriage offers, so no one is married off under the player mid-courtship.
        // Betrothed mirrors as CoupleAgreedOnMarriage — vanilla's own "final terms remain" state,
        // which nothing in vanilla completes on its own (decompile-verified 2026.08.07).
        private static void MirrorRomance(Hero npc, CourtshipStage stage)
        {
            try
            {
                if (npc == null || !npc.IsLord || Hero.MainHero == null) return;
                var target = stage >= CourtshipStage.Betrothed ? Romance.RomanceLevelEnum.CoupleAgreedOnMarriage
                    : stage >= CourtshipStage.Warmth ? Romance.RomanceLevelEnum.CourtshipStarted
                    : Romance.RomanceLevelEnum.Untested;
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var current = Romance.GetRomanticLevel(Hero.MainHero, npc);
                        if (current == Romance.RomanceLevelEnum.Marriage) return;
                        if (current != target)
                            ChangeRomanticStateAction.Apply(Hero.MainHero, npc, target);
                    }
                    catch { /* the mirror is a nicety; our own road is the truth */ }
                });
            }
            catch { }
        }

        // A colored line in the message stream for every movement of the road — Anton's ask: the
        // player always sees the heart move, like the tool-use notices in the battle log.
        private void NotifyRoadStep(Hero npc, CourtshipStage stage, bool forward, bool brokeTroth = false, bool seeded = false)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                string line;
                Color color = RoadColor;
                if (seeded)
                    line = $"{name}'s heart owns where it already stands: {CourtshipRoad.StageName(stage)}.";
                else if (!forward)
                {
                    // Frost blue for the freeze, red only for a broken troth (Anton's color language).
                    color = brokeTroth ? new Color(0.85f, 0.45f, 0.45f, 1f) : FrostColor;
                    line = brokeTroth
                        ? $"{name} has taken back their promise — the troth is broken."
                        : $"{name}'s heart draws back a step.";
                }
                else switch (stage)
                {
                    case CourtshipStage.Warmth: line = $"{name}'s heart warms toward you — a first step on a longer road."; break;
                    case CourtshipStage.Devotion: line = $"{name}'s heart is truly given."; break;
                    case CourtshipStage.Ready: line = $"{name}'s heart is ready — were the word spoken, the answer would be yes."; break;
                    default: line = $"{name}'s heart moves along its road."; break;
                }
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage(line, color)));
            }
            catch { /* the notice is a nicety */ }
        }

        // A reach that the world turned back (2026.08.15). NOT gated on ShowNpcActivity: this is not
        // a nicety like "remembering…", it is the only thing standing between "the mod refused, and
        // here is why" and a player watching a wedding happen in words and nowhere else. Frost blue,
        // with the rest of the road's weather.
        private void NotifyRoadRefused(Hero npc, CourtshipStage target, string reason)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                var why = string.IsNullOrWhiteSpace(reason) ? "the world does not allow it yet" : reason.Trim();
                string line;
                switch (target)
                {
                    case CourtshipStage.Betrothed:
                        line = $"{name} would give you their promise, but it cannot stand yet — {why}. Nothing has been sealed.";
                        break;
                    case CourtshipStage.Wed:
                        line = $"{name} reaches for your wedding day, but it cannot be sealed yet — {why}. Nothing has been sealed; whatever is said now, you are not wed.";
                        break;
                    default:
                        line = $"{name}'s heart reaches further, but not yet — {why}.";
                        break;
                }
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage(line, FrostColor)));
            }
            catch { /* the notice is the point, but it may never cost a reply */ }
        }

        // The activity-voice sibling for the laid moments ("lays their promise…"), same gate as
        // the other hands' notices.
        private void NotifyRoadActivity(Hero npc, string doing)
        {
            if (!_config.ShowNpcActivity) return;
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage($"{name} {doing}", ActivityColor)));
            }
            catch { }
        }

        // ------------------------- the seeding and the matchmaker -------------------------

        // Before a courtship-open exchange: a soul with a real lived story is seeded ONCE from that
        // story ("where does my heart already stand" — Anton's ask: Sibylla must not start at None),
        // and a soul on the road without her quiet asks receives them from the matchmaker's ledger.
        // Both are one-time plain utility calls (the spark's siblings), billed under the ambient
        // interaction; a garbled answer writes nothing and simply tries again another day.
        private async Task EnsureCourtshipReadyAsync(Hero npc)
        {
            try
            {
                if (npc == null) return;
                var memory = LoadMemory(npc);

                // A marriage completed OUTSIDE our seal (vanilla's own final-terms barter — the
                // mirror parks her at exactly the state whose barter vanilla offers) leaves the
                // road's stage behind the world's truth: reconcile, so a wed pair never wears a
                // "betrothed" sheet (review find, 2026.08.08).
                if (memory.CourtshipStage > CourtshipStage.None
                    && memory.CourtshipStage < CourtshipStage.Wed
                    && npc.Spouse == Hero.MainHero && Hero.MainHero != null)
                {
                    memory.CourtshipStage = CourtshipStage.Wed;
                    memory.CourtshipSeeded = true;
                    SaveMemory(npc, memory);
                    return;
                }

                bool needsSeed = !memory.CourtshipSeeded;
                if (!needsSeed)
                {
                    // The betrothal's shield, re-asserted (Anton's ask: no one takes her while we
                    // are still arranging): vanilla can demote a mirrored romance state when its own
                    // dialog opens in a blocked hour (a war, a map event) — re-mirroring on every
                    // courtship exchange heals it, so a courted or betrothed soul never falls back
                    // into the daily NPC-marriage lottery. Idempotent: MirrorRomance writes only on
                    // a real difference.
                    if (memory.CourtshipStage > CourtshipStage.None && memory.CourtshipStage < CourtshipStage.Wed)
                        MirrorRomance(npc, memory.CourtshipStage);
                    return;
                }

                if (!TryBeginCourtshipWork(npc.StringId)) return;
                try
                {
                    var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                    var npcName = npc.Name?.ToString() ?? "Unknown";
                    var genderWord = npc.IsFemale ? "woman" : "man";

                    if (needsSeed)
                    {
                        if (memory.StoryRichness < 6)
                        {
                            // No real story yet — the road simply begins at its beginning.
                            memory.CourtshipSeeded = true;
                            SaveMemory(npc, memory);
                        }
                        else
                        {
                            var selfText = PromptFiles.LoadNpcPrompt(NpcPaths.CustomInstructionsFile(npc), npcName);
                            var prompt = CourtshipSeed.BuildPrompt(
                                npcName, genderWord, playerName,
                                memory.Summary,
                                selfText,
                                RecentExcerpt(memory, playerName));
                            var raw = await _client.CompleteAsync(
                                new List<ChatMessage> { ChatMessage.User(prompt) }).ConfigureAwait(false);

                            if (CourtshipSeed.TryParseSeed(raw, out var stage, out var why))
                            {
                                if (stage > CourtshipStage.Betrothed) stage = CourtshipStage.Betrothed;
                                memory.CourtshipSeeded = true;
                                if (stage > CourtshipStage.None)
                                {
                                    var now = CampaignTime.Now.ToDays;
                                    memory.CourtshipStage = stage;
                                    memory.CourtshipStepDay = now;
                                    // A seeded promise begins its seasoning now — never pre-aged.
                                    if (stage == CourtshipStage.Betrothed) memory.BetrothedGameDay = now;
                                    AddSilentInnerBeat(memory, npc, CourtshipText.SeededBeat(playerName, stage, why));
                                    NotifyRoadStep(npc, stage, forward: true, seeded: true);
                                    MirrorRomance(npc, stage);
                                }
                                SaveMemory(npc, memory);
                                ModLog.Info($"the road already walked: {npcName} seeds at {CourtshipRoad.StageName(stage)} — {why}");
                            }
                            // else: unusable answer — no seed mark, tried again next talk.
                        }
                    }

                    // (No matchmaker follows the seeding anymore — since 2026.08.08 her misgivings
                    // are HER OWN to set down mid-talk via weigh_misgivings, never generated.)
                }
                finally { EndCourtshipWork(npc.StringId); }
            }
            catch (Exception ex)
            {
                ModLog.Error("readying the courtship road for " + (npc?.Name?.ToString() ?? "?"), ex);
            }
        }

        // The last few remembered exchanges, condensed for the seeding call's eyes.
        private static string RecentExcerpt(NpcMemory memory, string playerName)
        {
            try
            {
                var sb = new StringBuilder();
                int start = Math.Max(0, memory.RecentTurns.Count - 6);
                for (int i = start; i < memory.RecentTurns.Count; i++)
                {
                    var turn = memory.RecentTurns[i];
                    var incoming = Squeeze(turn.PlayerLine, 220);
                    var reply = Squeeze(turn.NpcLine, 220);
                    if (incoming.Length > 0)
                        sb.AppendLine((turn.IsInnerThought || turn.IsFromAngel ? "(inner) " : playerName + ": ") + incoming);
                    if (reply.Length > 0) sb.AppendLine("Answer: " + reply);
                }
                return sb.ToString().TrimEnd();
            }
            catch { return string.Empty; }
        }

        private static string Squeeze(string? text, int max)
        {
            var t = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Length <= max ? t : t.Substring(0, max) + "…";
        }

        // ------------------------- the seals (the only doors) -------------------------

        // An offer that rode with a LETTER, presented as its reading closes: the same seal doors,
        // re-run rules and all — only the courier carried the laying. Called from the letter-arrival
        // inquiry's callbacks (game thread); a dead writer's offers die with the writer upstream.
        private void PresentLetterOfferIfAny(Hero npc, Core.Letters.Letter letter)
        {
            try
            {
                if (npc == null || letter == null || string.IsNullOrEmpty(letter.LaidKind)) return;
                switch (letter.LaidKind)
                {
                    case "hiring":
                        ShowBargainSealInquiry(npc, letter.LaidPrice, byLetter: true);
                        break;
                    case "betrothal":
                        ShowBetrothalInquiry(npc, letter.LaidWord);
                        break;
                    case "lover":
                        ShowLoverInquiry(npc, letter.LaidWord);
                        break;
                    case "ransom":
                        var kin = FindAliveHero(letter.LaidBrideId);
                        if (kin == null) break;
                        ShowRansomInquiry(npc, new Tools.TrothTool.BlessTally
                        {
                            Laid = true,
                            Price = letter.LaidPrice,
                            Bride = kin,
                            IsRansom = true,
                        });
                        break;
                    case "blessing":
                        var bride = FindAliveHero(letter.LaidBrideId);
                        if (bride == null) break;
                        ShowBlessingInquiry(npc, new Tools.TrothTool.BlessTally
                        {
                            Laid = true,
                            Price = letter.LaidPrice,
                            Bride = bride,
                        });
                        break;
                }
            }
            catch (Exception ex) { ModLog.Error("presenting a letter-borne offer", ex); }
        }

        // Called from both render blocks AFTER the reply is shown, exactly like the bargain's seal.
        private void PresentTrothIfAny(Hero npc, TurnOutcome outcome)
        {
            try
            {
                if (outcome.Bless != null && outcome.Bless.Laid)
                {
                    if (outcome.Bless.IsRansom) ShowRansomInquiry(npc, outcome.Bless);
                    else ShowBlessingInquiry(npc, outcome.Bless);
                }
                if (outcome.Troth == null) return;
                if (outcome.Troth.LaidBetrothal) ShowBetrothalInquiry(npc, outcome.Troth.Word);
                else if (outcome.Troth.LaidWedding) ShowWeddingInquiry(npc);
                else if (outcome.Troth.LaidLoverBond) ShowLoverInquiry(npc, outcome.Troth.Word);
            }
            catch (Exception ex) { ModLog.Error("presenting the troth", ex); }
        }

        private void ShowBetrothalInquiry(Hero npc, string word)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                var their = npc != null && npc.IsFemale ? "her" : "his";
                var wordLine = string.IsNullOrWhiteSpace(word) ? string.Empty : $"\n\n“{word.Trim()}”";
                var body =
                    $"{name} offers you {their} promise: to be betrothed — bound to wed you when the day comes.{wordLine}\n\n" +
                    "A betrothal moves no gold and no banners; it is a promise between two souls, and the wedding day comes only when you both reach for it.\n\n" +
                    $"Take {their} promise, and give your own?";
                var data = new InquiryData(
                    new TextObject("{=ImmersiveAI_TrothTitle}A promise is offered").ToString(),
                    body, true, true,
                    new TextObject("{=ImmersiveAI_TrothAccept}Be betrothed").ToString(),
                    new TextObject("{=ImmersiveAI_TrothDecline}Let it lie").ToString(),
                    () => OnBetrothalSealed(npc),
                    () => OnBetrothalDeclined(npc),
                    "", 0f, null, null, null);
                InformationManager.ShowInquiry(data, pauseGameActiveState: true);
            }
            catch (Exception ex) { ModLog.Error("showing the betrothal inquiry", ex); }
        }

        private void OnBetrothalSealed(Hero npc)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var block = TrothBlockReason(npc, forWedding: false, _config);
                if (block == TrothBlock.None && PlayerPromisedToAnother(npc)) block = TrothBlock.PromisedElsewhere;

                var memory = LoadMemory(npc);
                if (block == TrothBlock.None && memory.CourtshipStage != CourtshipStage.Ready)
                    block = TrothBlock.WorldRefuses; // her heart moved between lay and seal — never force it
                if (block == TrothBlock.None
                    && CourtshipRoad.JudgeForward(RoadFactsOf(npc, memory)) != CourtshipRoad.StepVerdict.Allowed)
                    block = TrothBlock.WorldRefuses;

                if (block != TrothBlock.None)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The promise could not be given — {TrothBlockForPlayer(block, npc)}.", SealGrey));
                    AppendRecordedTurn(npc,
                        CourtshipText.BetrothalBlockedBeat(playerName, TrothBlockForNpc(block, npc)),
                        string.Empty);
                    return;
                }

                var now = CampaignTime.Now.ToDays;
                memory.CourtshipStage = CourtshipStage.Betrothed;
                memory.BetrothedGameDay = now;
                memory.CourtshipStepDay = now;
                AddSilentInnerBeat(memory, npc, CourtshipText.BetrothalSealedBeat(playerName));
                memory.NotePlayerEngaged();
                SaveMemory(npc, memory);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"You are betrothed to {npc?.Name}. The promise is given, both ways.", SealGreen));
                MirrorRomance(npc, CourtshipStage.Betrothed);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("sealing the betrothal", ex); }
        }

        private void OnBetrothalDeclined(Hero npc)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                InformationManager.DisplayMessage(new InformationMessage("You let the promise lie.", SealGrey));
                AppendRecordedTurn(npc, CourtshipText.BetrothalDeclinedBeat(playerName), string.Empty);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("declining the betrothal", ex); }
        }

        /// <summary>The road button's own door to the wedding: what kind of day to give them
        /// (2026.08.09, Anton's design). The coin does not buy adjectives — it buys how far the
        /// invitation travels, and every soul it reaches carries the day in memory forever. Rides
        /// the global inquiry layer, safely above the chat window.</summary>
        internal void ShowWeddingScaleInquiry(Hero npc)
        {
            try
            {
                var player = Hero.MainHero;
                if (npc == null || player == null) return;

                // The world's own rules first — never open a purse for a day that cannot happen.
                var block = TrothBlockReason(npc, forWedding: true, _config);
                if (block == TrothBlock.None && BlessingRequired(npc) && LoadMemory(npc).FamilyBlessingDay < 0)
                    block = TrothBlock.BlessingMissing;
                if (block != TrothBlock.None)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You cannot wed {npc.Name} just now — {TrothBlockForPlayer(block, npc)}.", SealGrey));
                    return;
                }

                var venue = CurrentWeddingVenue(out bool ownTown);
                var elements = new List<InquiryElement>();
                foreach (var tier in Core.Weddings.WeddingTiers.All)
                {
                    bool afford = player.Gold >= tier.Price;
                    bool fits = tier.FitsIn(venue, ownTown);
                    var why = new StringBuilder(tier.PlayerDescription);
                    why.Append("\n\nHeld ").Append(tier.VenueRequirement).Append('.');
                    why.Append(" Your house gains ").Append(tier.Renown).Append(" renown.");
                    if (!fits)
                        why.Append($"\n\n(Not here: you stand {WeddingPlacePhrase()}. Ride somewhere worthier and open this again.)");
                    else if (!afford)
                        why.Append("\n\n(Your purse cannot carry this.)");
                    elements.Add(new InquiryElement(
                        tier.Scale,
                        $"{tier.Name} — {tier.Price} denars",
                        null, afford && fits, why.ToString()));
                }

                var data = new MultiSelectionInquiryData(
                    new TextObject("{=ImmersiveAI_WedScaleTitle}What wedding will you give them?").ToString(),
                    $"You are to wed {npc.Name}, {WeddingPlacePhrase()} — a wedding is held where you stand when you seal it.\n\n"
                    + "What you spend decides how far the invitation travels — and everyone who stands there will carry this day in their memory for the rest of their life.\n\n"
                    + $"You hold {player.Gold} denars.",
                    elements, true, 1, 1,
                    new TextObject("{=ImmersiveAI_WedScaleAccept}Wed them this day").ToString(),
                    new TextObject("{=ImmersiveAI_WedScaleDecline}Not yet").ToString(),
                    chosen =>
                    {
                        try
                        {
                            var pick = chosen?.FirstOrDefault()?.Identifier;
                            if (!(pick is Core.Weddings.WeddingScale scale)) return;
                            OnWeddingSealed(npc, scale);
                        }
                        catch (Exception ex) { ModLog.Error("choosing the wedding", ex); }
                    },
                    _ => { });
                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex) { ModLog.Error("showing the wedding's own door", ex); }
        }

        private void ShowWeddingInquiry(Hero npc)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                bool isFemale = npc?.IsFemale ?? true;
                var spouseWord = isFemale ? "wife" : "husband";
                // Honest to the letter: the line appears only when the blessing was truly given
                // (the lay already refuses without it, but the popup never claims what is not).
                string blessingLine = string.Empty;
                if (BlessingRequired(npc) && LoadMemory(npc).FamilyBlessingDay >= 0)
                    blessingLine = "\n\nTheir kin have blessed the match.";
                string standingLine = npc != null && npc.IsWanderer
                    ? $"\n\nThey will stand a full member of your house from this day — your {spouseWord}, and still at your side on the road."
                    : "\n\nThey will leave their banner and join your house, as the custom of the world moves the wedded.";
                var body =
                    $"{name} would wed you this day — the promise between you fulfilled.{blessingLine}{standingLine}\n\n" +
                    "Seal the wedding?";
                var data = new InquiryData(
                    new TextObject("{=ImmersiveAI_WedTitle}A wedding day is reached for").ToString(),
                    body, true, true,
                    new TextObject("{=ImmersiveAI_WedAccept}Wed them this day").ToString(),
                    new TextObject("{=ImmersiveAI_WedDecline}Not yet").ToString(),
                    // Whichever door the wedding is reached through — her own laid day, or the road
                    // button — the KIND of wedding is chosen in one place. Dispatched a tick later
                    // so the new inquiry never opens inside the closing one's own callback.
                    () => MainThreadDispatcher.Enqueue(() => ShowWeddingScaleInquiry(npc)),
                    () => OnWeddingDeclined(npc),
                    "", 0f, null, null, null);
                InformationManager.ShowInquiry(data, pauseGameActiveState: true);
            }
            catch (Exception ex) { ModLog.Error("showing the wedding inquiry", ex); }
        }

        /// <summary>The seal itself. <paramref name="scale"/> is the wedding the player bought — it
        /// is stashed for the chronicle hook (which fires INSIDE MarriageAction.Apply, before we
        /// could hand it anything) and its price leaves the purse only once every rule has passed.</summary>
        private void OnWeddingSealed(Hero npc, Core.Weddings.WeddingScale scale = Core.Weddings.WeddingScale.Unpaid)
        {
            bool graduated = false;
            try
            {
                var player = Hero.MainHero;
                var playerName = player?.Name?.ToString() ?? "the traveler";
                var memory = LoadMemory(npc);
                int price = Core.Weddings.WeddingTiers.PriceOf(scale);

                var block = TrothBlockReason(npc, forWedding: true, _config);
                // The purse is a rule like any other, and it is re-run here, not trusted from the
                // popup: a battle's ransom could have emptied it between choosing and sealing.
                if (block == TrothBlock.None && price > 0 && (player?.Gold ?? 0) < price)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The wedding could not be sealed — {price} denars is more than you hold.", SealGrey));
                    return;
                }
                if (block == TrothBlock.None && BlessingRequired(npc) && memory.FamilyBlessingDay < 0)
                    block = TrothBlock.BlessingMissing;
                if (block == TrothBlock.None && memory.CourtshipStage != CourtshipStage.Betrothed)
                    block = TrothBlock.WorldRefuses;
                if (block == TrothBlock.None
                    && CourtshipRoad.JudgeForward(RoadFactsOf(npc, memory)) != CourtshipRoad.StepVerdict.Allowed)
                    block = TrothBlock.WorldRefuses;

                if (block == TrothBlock.None && npc.IsWanderer)
                {
                    // The companion bride's graduation — vanilla's own companion-to-lord shape, the
                    // smallest sound diff (decompile-verified 2026.08.07): her occupation becomes
                    // Lord so the world's own marriage law accepts her, while she KEEPS her companion
                    // place, her party slot, and her duties — no clan change happens (her Clan already
                    // reads the player's through CompanionOf), so no fugitive, no teleport.
                    npc.SetNewOccupation(Occupation.Lord);
                    graduated = true;
                }

                if (block == TrothBlock.None)
                {
                    try
                    {
                        if (!Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(player, npc))
                            block = TrothBlock.WorldRefuses;
                    }
                    catch { block = TrothBlock.WorldRefuses; }
                }

                if (block != TrothBlock.None)
                {
                    if (graduated) npc.SetNewOccupation(Occupation.Wanderer);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The wedding could not be sealed — {TrothBlockForPlayer(block, npc)}.", SealGrey));
                    AppendRecordedTurn(npc,
                        CourtshipText.WeddingBlockedBeat(playerName, TrothBlockForNpc(block, npc)),
                        string.Empty);
                    return;
                }

                // The day they bought, laid where the chronicle hook can find it: BeforeHeroesMarried
                // fires INSIDE MarriageAction.Apply, so there is no later moment to hand it over.
                SetPendingWeddingScale(npc, scale);
                if (price > 0) player!.ChangeHeroGold(-price);

                // The REAL game marriage: spouse both ways, relation, the wedding cutscene, the log
                // entry the world's tidings gossip about, SetHasMet — all vanilla's own listeners.
                MarriageAction.Apply(player, npc);

                if (player.Spouse != npc)
                {
                    // The action refused after all (it no-ops silently on an unsuitable couple) —
                    // never leave a half-married state behind, and never keep coin for a feast
                    // that never happened.
                    if (graduated) npc.SetNewOccupation(Occupation.Wanderer);
                    if (price > 0) player.ChangeHeroGold(price);
                    SetPendingWeddingScale(npc, Core.Weddings.WeddingScale.Unpaid);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The wedding could not be sealed — the world refused the match at the last.", SealGrey));
                    AppendRecordedTurn(npc,
                        CourtshipText.WeddingBlockedBeat(playerName,
                            TrothBlockForNpc(TrothBlock.WorldRefuses, npc)),
                        string.Empty);
                    return;
                }

                memory = LoadMemory(npc); // reload: marriage listeners may have run in between
                memory.CourtshipStage = CourtshipStage.Wed;
                AddSilentInnerBeat(memory, npc, CourtshipText.WeddingSealedBeat(playerName));
                memory.NotePlayerEngaged();
                SaveMemory(npc, memory);

                // What the day added to the house's name — given after the marriage truly landed,
                // never before, so a refused wedding can never leave renown behind it.
                int renown = Core.Weddings.WeddingTiers.RenownOf(scale);
                if (renown > 0)
                {
                    try { GainRenownAction.Apply(player!, renown, doNotNotify: true); } catch { }
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The wedding is spoken of: your house gains {renown} renown.", RoadColor));
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"This day you and {npc.Name} are wed.", SealGreen));
                NotifyWithFace(npc, $"{npc.Name} is wed to you this day — what was promised is fulfilled.");
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex)
            {
                ModLog.Error("sealing the wedding", ex);
                try { if (graduated && npc != null && Hero.MainHero?.Spouse != npc) npc.SetNewOccupation(Occupation.Wanderer); }
                catch { }
            }
        }

        private void OnWeddingDeclined(Hero npc)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                InformationManager.DisplayMessage(new InformationMessage("You let the wedding day lie.", SealGrey));
                AppendRecordedTurn(npc, CourtshipText.WeddingDeclinedBeat(playerName), string.Empty);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("declining the wedding", ex); }
        }

        private void ShowBlessingInquiry(Hero npc, Tools.TrothTool.BlessTally bless)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                var bride = bless.Bride;
                var brideName = bride?.Name?.ToString() ?? "their kin";
                string reckon = string.Empty;
                if (BlessingRails(bride, out int reckoning, out _, out _) && reckoning != bless.Price)
                    reckon = $" (The custom's own reckoning for one of their name is {reckoning} denars.)";
                var body =
                    $"{name} offers the blessing of their house on your match with {brideName}, " +
                    $"for {bless.Price} denars as the bride-price — the sum spoken between you.{reckon}\n\n" +
                    "Seal the blessing?";
                var data = new InquiryData(
                    new TextObject("{=ImmersiveAI_BlessTitle}A blessing is laid before you").ToString(),
                    body, true, true,
                    new TextObject("{=ImmersiveAI_BlessAccept}Seal it — pay the bride-price").ToString(),
                    new TextObject("{=ImmersiveAI_BlessDecline}Let it lie").ToString(),
                    () => OnBlessingSealed(npc, bless),
                    () => OnBlessingDeclined(npc, bless),
                    "", 0f, null, null, null);
                InformationManager.ShowInquiry(data, pauseGameActiveState: true);
            }
            catch (Exception ex) { ModLog.Error("showing the blessing inquiry", ex); }
        }

        private void OnBlessingSealed(Hero npc, Tools.TrothTool.BlessTally bless)
        {
            try
            {
                var player = Hero.MainHero;
                var playerName = player?.Name?.ToString() ?? "the traveler";
                var bride = bless.Bride;
                var brideName = bride?.Name?.ToString() ?? "their kin";

                string? blocked = null;
                if (bride == null || !bride.IsAlive || npc == null || !npc.IsAlive) blocked = "one of them is beyond the world's reach";
                else if (player == null || player.Gold < bless.Price) blocked = "your purse no longer carries the price";
                else if (npc.MapFaction != null && player.MapFaction != null
                    && npc.MapFaction.IsAtWarWith(player.MapFaction)) blocked = "your realms now stand at war";
                // The pair's own hard rules re-run at the seal too (the one law): a bride married
                // off in the meantime, or a player no longer free, must never sell a blessing —
                // gold for a match that can no longer be wed (review find, 2026.08.08).
                else if (TrothBlockReason(bride, forWedding: false, _config) != TrothBlock.None)
                    blocked = "the match itself no longer stands in the world's eyes";
                else
                {
                    var brideMemory = LoadMemory(bride);
                    if (brideMemory.CourtshipStage != CourtshipStage.Betrothed) blocked = "the betrothal no longer stands";
                    else if (brideMemory.FamilyBlessingDay >= 0) blocked = "the blessing is already given";
                }

                if (blocked != null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The blessing could not be sealed — {blocked}.", SealGrey));
                    AppendRecordedTurn(npc,
                        $"I laid my blessing at {bless.Price} denars, but the world would not allow the sealing — {blocked}. No gold passed, and the word of our house stays unspent.",
                        string.Empty);
                    return;
                }

                GiveGoldAction.ApplyBetweenCharacters(player, npc, bless.Price);

                var newsBeat = CourtshipText.BlessingNewsBeat(playerName,
                    npc.Name?.ToString() ?? "the head of my house", npc.IsFemale);
                var day = CampaignTime.Now.ToDays;

                // The paid truth, twice held: written to her file now, AND parked to fold into any
                // stale in-flight instance of hers that saves later — the gold can never buy a
                // blessing the world forgets (review find, 2026.08.08).
                _pendingBlessingFolds[bride.StringId] = (day, bless.Price, newsBeat);
                var memory = LoadMemory(bride);
                memory.FamilyBlessingDay = day;
                memory.FamilyBlessingPrice = bless.Price;
                // The news reaches HER, wherever she stands — a silent beat in her own memory.
                AddSilentInnerBeat(memory, bride, newsBeat);
                SaveMemory(bride, memory);

                AppendRecordedTurn(npc,
                    CourtshipText.BlessingSealedBeat(playerName, brideName, bless.Price),
                    string.Empty, OutreachMark.PlayerEngaged);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"The blessing is sealed: {bless.Price} denars to {npc.Name}, and the road to your wedding with {brideName} lies open.", SealGreen));
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("sealing the blessing", ex); }
        }

        private void OnBlessingDeclined(Hero npc, Tools.TrothTool.BlessTally bless)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var brideName = bless.Bride?.Name?.ToString() ?? "their kin";
                InformationManager.DisplayMessage(new InformationMessage("You let the blessing lie.", SealGrey));
                AppendRecordedTurn(npc,
                    CourtshipText.BlessingDeclinedBeat(playerName, brideName, bless.Price),
                    string.Empty);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("declining the blessing", ex); }
        }

        // ------------------------- DevMode levers -------------------------

        // The plain developer's view: the road, the gate's arithmetic (plain here — the PLAYER'S
        // debug eyes, not hers), her misgivings as they stand, and the blessing's state.
        private void OnDebugRevealCourtship()
        {
            var npc = Hero.OneToOneConversationHero ?? _currentNpc;
            if (npc == null) return;
            RevealCourtshipFor(npc);
            MBTextManager.SetTextVariable(InfoVar, "(You read the road of their heart.)", false);
        }

        internal void RevealCourtshipFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                var memory = LoadMemory(npc);
                var facts = RoadFactsOf(npc, memory);
                var sb = new StringBuilder();
                sb.AppendLine($"Road with {npc.Name}: {CourtshipRoad.StageName(memory.CourtshipStage)} (seeded: {memory.CourtshipSeeded})");
                sb.AppendLine($"Relation {facts.Relation} · station tier {facts.HerStationTier} · player tier {facts.PlayerClanTier} · slack {facts.CharmSlack} → required {CourtshipRoad.RequiredTier(facts.HerStationTier, facts.CharmSlack)}");
                if (memory.BetrothedGameDay >= 0)
                    sb.AppendLine($"Betrothed {facts.DaysBetrothed:0.#}d ago (min {facts.MinBetrothalDays}d)");
                sb.AppendLine($"Blessing: {(BlessingRequired(npc) ? (memory.FamilyBlessingDay >= 0 ? $"given ({memory.FamilyBlessingPrice} denars)" : "required, not given") : "not required")}");
                sb.AppendLine($"Next step verdict: {CourtshipRoad.JudgeForward(facts)}");
                sb.AppendLine();
                if (!memory.MisgivingsWeighed && memory.CourtshipMisgivings.Count == 0)
                    sb.AppendLine("Misgivings: not yet weighed — they write their own when marriage truly enters the talk.");
                else if (memory.CourtshipMisgivings.Count == 0)
                    sb.AppendLine("Misgivings: weighed — none; their heart is clear.");
                else
                {
                    sb.AppendLine($"Misgivings ({facts.OpenMisgivings} of {CourtshipMisgivings.TotalCount(memory.CourtshipMisgivings)} standing):");
                    foreach (var m in memory.CourtshipMisgivings)
                    {
                        if (m == null || string.IsNullOrWhiteSpace(m.Text)) continue;
                        sb.AppendLine(m.Settled
                            ? $"✓ {m.Text}{(string.IsNullOrWhiteSpace(m.SettledNote) ? string.Empty : $" — laid to rest: {m.SettledNote}")}"
                            : $"• {m.Text}");
                    }
                }
                ShowScrollPopup($"The road of {npc.Name}'s heart", sb.ToString().Trim());
            }
            catch (Exception ex) { ModLog.Error("revealing the courtship road", ex); }
        }

        // DevMode: shove the road one step forward, rails and waiting and all — the only way to
        // reach a wedding in minutes instead of a week of game days, which is what testing the
        // wedding chronicle actually needs (2026.08.09). Never reachable by a player.
        internal void AdvanceCourtshipFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                var memory = LoadMemory(npc);
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var now = CampaignTime.Now.ToDays;

                // Whatever she has not yet weighed or laid to rest, take as answered — the rails
                // ask for it and this lever exists precisely to skip the asking.
                memory.MisgivingsWeighed = true;
                foreach (var m in memory.CourtshipMisgivings)
                    if (m != null && !m.Settled) { m.Settled = true; m.SettledNote = "(set aside for testing)"; }

                if (memory.CourtshipStage < CourtshipStage.Betrothed)
                {
                    memory.CourtshipStage = memory.CourtshipStage + 1;
                    memory.CourtshipStepDay = now - CourtshipRoad.DaysBetweenForwardSteps - 1;
                    if (memory.CourtshipStage == CourtshipStage.Betrothed)
                        memory.BetrothedGameDay = now - Math.Max(0, _config.MinBetrothalDays) - 1;
                    memory.CourtshipSeeded = true;
                    SaveMemory(npc, memory);
                    MirrorRomance(npc, memory.CourtshipStage);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[test] {npc.Name}'s road now stands at {CourtshipRoad.StageName(memory.CourtshipStage)}"
                        + (memory.CourtshipStage == CourtshipStage.Betrothed
                            ? " — the troth is already seasoned; speak of the wedding day and she may lay it."
                            : " — misgivings cleared, the day's rail lifted."), ActivityColor));
                }
                else if (memory.CourtshipStage == CourtshipStage.Betrothed)
                {
                    memory.BetrothedGameDay = now - Math.Max(0, _config.MinBetrothalDays) - 1;
                    SaveMemory(npc, memory);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[test] {npc.Name} is betrothed and the waiting is behind you — speak of the wedding day and she may lay it.", ActivityColor));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[test] {npc.Name} and you are already wed; the road has no further step.", SealGrey));
                }
            }
            catch (Exception ex) { ModLog.Error("advancing the courtship road", ex); }
        }

        internal static void DevAdvanceCourtship(Hero npc)
        {
            try { if (npc != null) Current?.AdvanceCourtshipFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: advancing the road", ex); }
        }

        private void OnDebugClearMisgivings()
        {
            var npc = Hero.OneToOneConversationHero ?? _currentNpc;
            if (npc == null) return;
            ClearMisgivingsFor(npc);
        }

        // Releases the written misgivings AND the weighed mark, so the soul sits with the question
        // afresh the next time marriage truly enters the talk — nothing is generated in their place.
        internal void ClearMisgivingsFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                var memory = LoadMemory(npc);
                memory.CourtshipMisgivings.Clear();
                memory.MisgivingsWeighed = false;
                SaveMemory(npc, memory);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{npc.Name}'s misgivings are released — they will weigh their heart anew when marriage next enters the talk.", ActivityColor));
            }
            catch (Exception ex) { ModLog.Error("clearing the misgivings", ex); }
        }

        // ------------------------- the road's own button -------------------------

        /// <summary>What the one little button under a soul's name is RIGHT NOW. The road answers
        /// "what do I do next?" at every stage instead of only showing a list of doubts (Anton,
        /// 2026.08.09): the misgivings while she is deciding, then her kin's blessing to be sought,
        /// then the days of preparation counting down, then the wedding itself to be paid for and
        /// sealed — and once wed, the day itself, kept forever.</summary>
        internal enum RoadPageKind { None, Misgivings, Blessing, Preparations, Wedding, WeddingDay, BetweenUs }

        internal sealed class RoadPage
        {
            public RoadPageKind Kind = RoadPageKind.None;
            /// <summary>The button's own words. Since 2026.08.15 this is ALWAYS "Between us" — see
            /// <see cref="RoadPageFor"/>. The stage-specific labels live on inside the page.</summary>
            public string Label = string.Empty;
            /// <summary>The hover text — this is where the player is TOLD what to do next.</summary>
            public string Hint = string.Empty;
            public string Title = string.Empty;
            public string Body = string.Empty;
            /// <summary>An act the page offers, when it offers one ("Give the child your name",
            /// "Choose the wedding day"). Empty means the page is only something to read.</summary>
            public string ActionLabel = string.Empty;
            /// <summary>Who the act is about, when it is not the soul whose page this is — the
            /// child being owned, rather than its mother.</summary>
            public Hero? ActionSubject;
        }

        /// <summary>
        /// ONE DOOR, MANY ROOMS (2026.08.15, Anton's design). The little button under a soul's name
        /// used to morph — Misgivings 2/4, then Our wedding day, then Our children — and that
        /// pinched us exactly once and memorably: the label went on saying "Our wedding day" over a
        /// page of children, because the widget could not hold the truth. So the LABEL never changes
        /// again and the PAGE adapts, which is the right way round.
        ///
        /// The page is composed rather than chosen: whatever the road's own stage has to say, plus
        /// what stands between them, plus what is still owed to a child. Sections appear only when
        /// they have something to say, and when nothing does there is no button at all.
        /// </summary>
        internal static RoadPage? RoadPageFor(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null) return null;

                var page = RoadStagePage(npc);
                var sb = new StringBuilder();
                var hint = new StringBuilder();

                // WHAT IS UNSAID comes FIRST, always. A shut door is the most present fact between
                // two people and the one thing a player most needs to be able to read.
                var doorBody = DoorPageFor(npc);
                if (!string.IsNullOrWhiteSpace(doorBody))
                {
                    sb.AppendLine(doorBody.Trim());
                    sb.AppendLine();
                    hint.Append("Something of theirs stands unanswered between you. ");
                }

                // WHERE WE STAND — what they are to you outside the world's ceremonies.
                var lover = LoverStandingLine(npc);
                if (!string.IsNullOrEmpty(lover))
                {
                    sb.AppendLine("Where you stand: " + Upper(lover) + ".");
                    sb.AppendLine();
                }

                // OUR DAYS — whatever the road's own stage was already saying.
                if (page != null && !string.IsNullOrWhiteSpace(page.Body))
                {
                    sb.AppendLine(page.Body.Trim());
                    sb.AppendLine();
                }

                // AND WHAT IS STILL OWED to a child of theirs.
                var owed = ChildrenAwaitingTheNameBy(npc);
                if (owed.Count > 0)
                {
                    var names = string.Join(", ", owed.Select(c => c.Name?.ToString() ?? "the child"));
                    sb.AppendLine($"You have never owned {names} before the world. The child is yours by blood and "
                                + "always will be; what has not been said is that it is yours. Until it is, the world "
                                + "speaks of the child as its mother's and of nothing else.");
                    sb.AppendLine();
                    hint.Append("A child of theirs is still waiting on your name. ");
                }

                var body = sb.ToString().TrimEnd();
                if (body.Length == 0) return null;

                if (page != null && !string.IsNullOrWhiteSpace(page.Hint)) hint.Append(page.Hint);
                if (hint.Length == 0) hint.Append("Everything that stands between the two of you, in their own words and yours.");

                // THE PRESS ALWAYS OPENS THE PAGE (self-review, 2026.08.15). The first cut kept the
                // stage's own kind so the old click-routing still fired — which meant a WED soul
                // went straight to the wedding view and the composed page was never seen at all.
                // Wives are exactly who has doors, so the doors were invisible to precisely the
                // people who have them. The stage's own DOOR is not lost: it moved into the page as
                // its action, which is the better shape anyway — you read what you are about to do.
                var stageAction = page?.Kind == RoadPageKind.Wedding ? "Choose the wedding day"
                    : page?.Kind == RoadPageKind.WeddingDay ? "See the day itself"
                    : string.Empty;

                return new RoadPage
                {
                    Kind = page?.Kind ?? RoadPageKind.BetweenUs,
                    Label = "Between us",
                    Title = $"Between you and {npc.Name}",
                    Body = body,
                    Hint = hint.ToString().Trim(),
                    // A child waiting on a name outranks the stage's own act: it is the rarer and
                    // heavier of the two, and the stage's door comes back the moment it is given.
                    ActionLabel = owed.Count > 0
                        ? (owed.Count == 1 ? $"Give {owed[0].Name} your name" : "Give them your name")
                        : stageAction,
                    ActionSubject = owed.FirstOrDefault(),
                };
            }
            catch { return null; }
        }

        /// <summary>Children of THIS soul's that the player has never owned before the world.</summary>
        private static List<Hero> ChildrenAwaitingTheNameBy(Hero mother)
        {
            try
            {
                if (mother == null) return new List<Hero>();
                return ChildrenAwaitingTheName()
                    .Where(c => c != null && c.Mother == mother)
                    .ToList();
            }
            catch { return new List<Hero>(); }
        }

        /// <summary>The road's own stage in one call, in the order the stages actually come. Composed
        /// into the one door by <see cref="RoadPageFor"/>.</summary>
        private static RoadPage? RoadStagePage(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null) return null;

                // Wed already — the day itself takes the button, forever.
                // The hint comes FROM the page, because the page is sometimes a wedding day, and
                // sometimes only the children of a bond that never had one.
                if (TryGetWeddingView(npc, out var wLabel, out var wTitle, out var wBody, out var wHint))
                    return new RoadPage
                    {
                        Kind = RoadPageKind.WeddingDay,
                        Label = wLabel,
                        Title = wTitle,
                        Body = wBody,
                        Hint = wHint,
                    };

                if (!self._config.EnableConversationMarriage) return self.MisgivingsPage(npc);

                var memory = self.LoadMemory(npc);
                if (memory.CourtshipStage != CourtshipStage.Betrothed || npc.Spouse != null)
                    return self.MisgivingsPage(npc);

                // Betrothed: her kin's word first, if her house asks one.
                if (self.BlessingRequired(npc) && memory.FamilyBlessingDay < 0)
                    return self.BlessingPage(npc);

                // Then the days the world asks a promise to season in.
                int need = Math.Max(0, self._config.MinBetrothalDays);
                double stood = memory.BetrothedGameDay < 0 ? need : CampaignTime.Now.ToDays - memory.BetrothedGameDay;
                if (stood < need) return self.PreparationsPage(npc, stood, need);

                return self.WeddingPage(npc);
            }
            catch { return null; }
        }

        // Her house has not spoken yet — and this is the page that tells the player, plainly, that
        // the road now runs through her kin and every way there is to reach them.
        private RoadPage BlessingPage(Hero npc)
        {
            var head = Safe(() => npc.Clan?.Leader, (Hero?)null);
            var headName = head?.Name?.ToString() ?? "the head of their house";
            var her = npc.IsFemale ? "her" : "his";
            bool near = Safe(() => head != null && IsCoLocated(head), false);
            var whereabouts = Safe(() => WhereaboutsOf(head), string.Empty);

            var sb = new StringBuilder();
            sb.AppendLine($"You are betrothed to {npc.Name}, but by the custom of this world {her} hand cannot be given without the word of {her} house — and that word is {headName}'s to give.");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(whereabouts)) sb.AppendLine(whereabouts);
            sb.AppendLine();
            sb.AppendLine("There are three ways to their word, and any of them serves:");
            sb.AppendLine($"• Stand before them and speak of the match — the bride-price is haggled in the talk itself, and {headName} will lay the offer before you when you have agreed on a figure.");
            sb.AppendLine($"• Write to them. A betrothal opens the road for letters, so {headName} is a correspondent now — the offer can be struck in writing and is laid before you when the reply comes.");
            sb.AppendLine("• Or settle it the world's own way, through the ordinary talk of houses, if that is where you find them.");
            sb.AppendLine();
            sb.AppendLine("Nothing is spent until you seal it yourself, and the price is never fixed: their house has its bounds, and what you pay inside them is what your words have earned.");

            return new RoadPage
            {
                Kind = RoadPageKind.Blessing,
                Label = $"{headName}'s blessing",
                Title = $"The blessing of {npc.Name}'s house",
                Hint = near
                    ? $"Their kin must bless the match before the wedding. {headName} is here with you — speak with them of it, agree a bride-price, and seal it."
                    : $"Their kin must bless the match before the wedding. Go to {headName} and speak of it, or write — a betrothal opens the road for letters, and the bride-price can be settled on paper.",
                Body = sb.ToString().TrimEnd(),
            };
        }

        // The promise is given and the house has spoken; now the world simply asks for days.
        private RoadPage PreparationsPage(Hero npc, double stood, int need)
        {
            int done = (int)Math.Floor(Math.Max(0, stood));
            if (done >= need) done = Math.Max(0, need - 1);
            double left = Math.Max(0, need - stood);
            var leftWord = left >= 1 ? $"about {Math.Ceiling(left):0} more day{(Math.Ceiling(left) == 1 ? string.Empty : "s")}" : "less than a day more";

            var sb = new StringBuilder();
            sb.AppendLine($"You and {npc.Name} are promised to one another, and their kin have no objection left. What remains is the world's own asking: a troth is given time to season, and a wedding takes preparing.");
            sb.AppendLine();
            sb.AppendLine($"{done} of {need} days have passed — {leftWord}. Ride, fight, trade, talk: the days pass as days do, and nothing here needs tending.");
            sb.AppendLine();
            sb.AppendLine("When they are done, this button becomes the wedding itself, and you will choose what kind of day to give them.");

            return new RoadPage
            {
                Kind = RoadPageKind.Preparations,
                Label = $"Preparations {done}/{need}",
                Title = $"Preparing to wed {npc.Name}",
                Hint = $"You are betrothed. The world asks {need} days for the promise to season and the preparations to be made — {leftWord}. Then this becomes the wedding itself.",
                Body = sb.ToString().TrimEnd(),
            };
        }

        // Everything asked has been given: the day is the player's to buy and to seal.
        private RoadPage WeddingPage(Hero npc)
        {
            // WHERE it will happen, said before a single denar is spent (Anton, 2026.08.09): the
            // wedding is held where the player is STANDING at the moment they seal it, and nobody
            // should discover that only afterwards, married in a ditch and wondering where the hall
            // went. It is the first line of the page and it rides the hover text too.
            var wherePhrase = WeddingPlacePhrase();

            var sb = new StringBuilder();
            sb.AppendLine($"Everything the world asked has been given. You may wed {npc.Name} this day.");
            sb.AppendLine();
            sb.AppendLine("WHERE: " + wherePhrase + " — a wedding is held where the two of you stand when you seal it. If you would be wed somewhere worthier, close this, ride there, and open it again; the day will wait for you.");
            sb.AppendLine();
            sb.AppendLine("What it costs is your own choice, and the choice is not only a price: it decides how far the invitation travels, and therefore who stands there — and every soul who stands there carries that day in their own memory for the rest of their life.");
            sb.AppendLine();
            foreach (var tier in Core.Weddings.WeddingTiers.All)
            {
                sb.AppendLine($"• {tier.Name} — {tier.Price} denars. {tier.PlayerDescription}");
                sb.AppendLine($"   Held {tier.VenueRequirement}. Your house gains {tier.Renown} renown.");
            }
            sb.AppendLine();
            sb.AppendLine("Choose, and the day is written down as it truly was.");

            return new RoadPage
            {
                Kind = RoadPageKind.Wedding,
                Label = $"Wed {npc.Name}",
                Title = $"Your wedding with {npc.Name}",
                Hint = $"Everything has been given: the promise, the days, the blessing. The wedding is held WHERE YOU STAND — right now that is {wherePhrase}. Open this to choose what kind of day to give them; the price decides how far the invitation reaches, and who will remember standing there.",
                Body = sb.ToString().TrimEnd(),
            };
        }

        /// <summary>The chat window's own door to the wedding — the road button's last stage.</summary>
        internal static void OpenWeddingDoorFor(Hero npc)
        {
            try { if (npc != null) Current?.ShowWeddingScaleInquiry(npc); }
            catch (Exception ex) { ModLog.Error("opening the wedding door", ex); }
        }

        /// <summary>Where a wedding sealed at this instant would be held.</summary>
        private static Settlement? WeddingPlaceNow()
        {
            try
            {
                return Hero.MainHero?.CurrentSettlement
                    ?? MobileParty.MainParty?.CurrentSettlement
                    ?? Settlement.CurrentSettlement;
            }
            catch { return null; }
        }

        /// <summary>What kind of place that is — and whether the town is the player's own, which is
        /// the one thing the legendary wedding asks that no purse can supply.</summary>
        internal static Core.Weddings.WeddingVenue CurrentWeddingVenue(out bool ownTown)
        {
            ownTown = false;
            try
            {
                var settlement = WeddingPlaceNow();
                if (settlement == null) return Core.Weddings.WeddingVenue.OpenField;
                if (settlement.IsTown)
                {
                    ownTown = Safe(() => settlement.OwnerClan == Clan.PlayerClan, false);
                    return Core.Weddings.WeddingVenue.Town;
                }
                if (settlement.IsCastle) return Core.Weddings.WeddingVenue.Castle;
                if (settlement.IsVillage) return Core.Weddings.WeddingVenue.Village;
                return Core.Weddings.WeddingVenue.OpenField;
            }
            catch { return Core.Weddings.WeddingVenue.OpenField; }
        }

        /// <summary>Where a wedding sealed at this instant would be held, in plain words.</summary>
        internal static string WeddingPlacePhrase()
        {
            try
            {
                var settlement = WeddingPlaceNow();
                var name = settlement?.Name?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    return "out on the open road, under the sky, with no hall and no town about you";
                if (settlement!.IsTown)
                    return Safe(() => settlement.OwnerClan == Clan.PlayerClan, false)
                        ? $"in your own town of {name}"
                        : $"in the town of {name}";
                if (settlement.IsCastle) return $"in the castle of {name}";
                if (settlement.IsVillage) return $"in the village of {name}";
                return "in " + name;
            }
            catch { return "wherever you happen to be standing"; }
        }

        // The first stage, and the only one that existed before: her own written doubts.
        private RoadPage? MisgivingsPage(Hero npc)
        {
            if (!TryGetMisgivingsView(npc, out var label, out var title, out var body)) return null;
            return new RoadPage
            {
                Kind = RoadPageKind.Misgivings,
                Label = label,
                Title = title,
                Body = body,
                Hint = "Their own words, written by their own hand mid-talk — a living list, never a checklist: new doubts may join it, empty ones get struck out, settled ones may return. They lay one to rest only when life truly answers it; while any stands, their hand waits.",
            };
        }

        // Where a soul was last heard of, phrased as hearsay — the same courtesy WorldRecall gives.
        private static string WhereaboutsOf(Hero? hero)
        {
            try
            {
                if (hero == null || !hero.IsAlive) return string.Empty;
                var where = hero.CurrentSettlement?.Name?.ToString()
                    ?? hero.PartyBelongedTo?.CurrentSettlement?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(where)) return $"Last word places {hero.Name} at {where}.";
                return hero.PartyBelongedTo != null
                    ? $"Last word has {hero.Name} somewhere on the road with their own company."
                    : string.Empty;
            }
            catch { return string.Empty; }
        }

        // ------------------------- the misgivings, shown to the player -------------------------

        /// <summary>The chat window's misgivings view for one soul (Anton's ask, 2026.08.08 — a
        /// little button, so what she set down is readable, not hidden bookkeeping). Returns false
        /// when there is nothing to show (no road walked and nothing written).</summary>
        internal static bool TryGetMisgivingsView(Hero npc, out string buttonLabel, out string title, out string body)
        {
            buttonLabel = title = body = string.Empty;
            try
            {
                var self = Current;
                if (self == null || npc == null || !self._config.EnableConversationMarriage) return false;
                var memory = self.LoadMemory(npc);
                var stage = memory.CourtshipStage;
                bool onRoad = stage > CourtshipStage.None && stage < CourtshipStage.Wed && npc.Spouse == null;
                int total = CourtshipMisgivings.TotalCount(memory.CourtshipMisgivings);
                if (!onRoad && total == 0) return false;

                int open = CourtshipMisgivings.OpenCount(memory.CourtshipMisgivings);
                buttonLabel = total > 0 ? $"Misgivings {open}/{total}"
                    : memory.MisgivingsWeighed ? "Misgivings: none"
                    : "Misgivings: unweighed";

                var name = npc.Name?.ToString() ?? "Their";
                title = $"What weighs on {name}'s heart before marriage";

                var sb = new StringBuilder();
                if (total == 0)
                {
                    sb.AppendLine(memory.MisgivingsWeighed
                        ? "They searched their heart and found nothing standing against a life together — it is clear. A true doubt born in a later talk may still be written down."
                        : "They have not yet sat with the question. When marriage truly enters your talks, they will weigh their heart and set down what troubles them — in their own words, a few at the most, or none at all.");
                }
                else
                {
                    foreach (var m in memory.CourtshipMisgivings)
                    {
                        if (m == null || string.IsNullOrWhiteSpace(m.Text)) continue;
                        if (m.Settled)
                        {
                            sb.AppendLine($"✓ {m.Text}");
                            if (!string.IsNullOrWhiteSpace(m.SettledNote))
                                sb.AppendLine($"      — laid to rest: {m.SettledNote}");
                        }
                        else sb.AppendLine($"• {m.Text}");
                        sb.AppendLine();
                    }
                    sb.AppendLine(open > 0
                        ? $"{open} of {total} still stand. These are theirs alone to lay to rest — talk with them, and let life answer what they wrote."
                        : $"All {total} are laid to rest — nothing they wrote still stands between you.");
                    sb.AppendLine();
                    sb.AppendLine("The list lives: new worries may join it mid-talk (rose and frost-blue lines in the log mark every change), ones that prove empty they strike out — and only when nothing stands is their hand free to be given.");
                }
                body = sb.ToString().TrimEnd();
                return true;
            }
            catch { return false; }
        }
    }
}
