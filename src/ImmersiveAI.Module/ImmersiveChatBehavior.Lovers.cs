using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Nights;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImmersiveAI
{
    /// <summary>
    /// THE LOVER'S ROAD (2026.08.15, Anton's design — the post-marriage batch's heart).
    ///
    /// The courtship road forks at devotion. One branch runs on through readiness, the betrothal and
    /// the wedding day, with the station gate and the misgivings and her kin's blessing on it. The
    /// other branch is this one, and it has none of that: no ceremony, no house that takes her name,
    /// no word said of them before anyone. What it asks instead is a great deal more feeling — a love
    /// gone deeper than any marriage gate ever asks for — because a wife has vows and a settlement
    /// and the world's approval holding her in place, and a lover has nothing at all but what she
    /// feels.
    ///
    /// FOUR RULES THIS FILE EXISTS TO HOLD:
    /// • WORDS NEVER BIND. She may only be his when she offers by her own hand and the player seals
    ///   it in a popup naming the thing exactly — the strike_bargain construction, and the anti-
    ///   pretence rail beside it, because the moment a soul is told "not yet" is the moment she
    ///   starts narrating it into being instead (Steam, rmanicky).
    /// • THE PLAYER'S OWN MARRIAGE IS NOT A BAR. It is the whole point. Every other rule of the
    ///   marriage road is re-run here; that one deliberately is not.
    /// • HER HOUSE IS PAID, NOT ASKED. There is no blessing on this and there never will be. A
    ///   noblewoman stays in her father's house until he is paid for losing her, and he takes the
    ///   gold without being reconciled by it — that is what the coin buys and all it buys.
    /// • NOTHING IS ERASED. A bond that ends becomes <see cref="LoverBond.Former"/>, the gold is
    ///   not returned, and she does not go home.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private bool LoversOn => _config.EnableLoversRoad && _config.EnableConversationMarriage;

        // ------------------------------ gates: when the hands ride ------------------------------

        /// <summary>
        /// Whether her own hand may offer herself in this turn. Deliberately NOT built on
        /// <see cref="CanTendTroth"/>: that gate refuses the moment the player has a standing
        /// marriage, which is exactly the case this feature exists for.
        /// </summary>
        private bool CanOfferSelf(Hero npc, bool byLetter = false)
        {
            if (!LoversOn) return false;
            if (!(_client is IToolChatClient)) return false;
            try
            {
                if (npc == null) return false;
                if (!byLetter && !IsCoLocated(npc)) return false;
                if (LoverWorldBlock(npc) != TrothBlock.None) return false;

                // The narrow gate that keeps this hand off almost every sheet, and keeps it CHEAP:
                // her heart must already have walked the shared trunk, and the index knows that
                // without opening a file. No memory file at all is no road walked.
                var known = MemoryIndex.Get(NpcPaths.MemoryFile(npc), _memoryStore);
                if (known == null || (CourtshipStage)known.CourtshipStage < LoverRoad.ForksFrom) return false;

                var memory = LoadMemory(npc);
                return LoverRoad.JudgeTake(LoverFactsOf(npc, memory)) == LoverRoad.LoverVerdict.Allowed;
            }
            catch { return false; }
        }

        /// <summary>The head of a house whose kinswoman is the player's lover and still under his
        /// roof — the blessing's own gate, and its exact opposite in feeling. By letter too: where
        /// in the world would one even find the father (the blessing's own 2026.08.08 lesson).</summary>
        private bool CanNameHerPrice(Hero npc, out Hero? kin, bool byLetter = false)
        {
            kin = null;
            if (!LoversOn) return false;
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

                foreach (var soul in clan.Heroes)
                {
                    if (soul == null || soul == npc || !soul.IsAlive) continue;
                    var known = MemoryIndex.Get(NpcPaths.MemoryFile(soul), _memoryStore);
                    if (known == null || (LoverBond)known.LoverBond != LoverBond.Lover) continue;
                    // Only the ransom itself needs the file — and only for the one soul who passed.
                    if (LoadMemory(soul).LoverRansomPaid > 0) continue;   // bought once; she is his to keep
                    kin = soul;
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        // ------------------------------ the hard rules (lay AND seal) ------------------------------

        /// <summary>
        /// The world's law over the pair, for THIS road. It borrows the marriage block's reason
        /// codes (their player-facing and her-facing wordings already exist and fit) but computes
        /// its own answer, because three of the marriage road's legs deliberately do not apply:
        /// the player's own standing marriage, the station gate, and the companion-marriage
        /// setting, which is about weddings and has nothing to say about this.
        /// </summary>
        private static TrothBlock LoverWorldBlock(Hero npc)
        {
            try
            {
                var player = Hero.MainHero;
                if (npc == null || player == null || !npc.IsAlive || !player.IsAlive
                    || npc.IsPrisoner || npc == player) return TrothBlock.Gone;
                if (npc.IsFemale == player.IsFemale) return TrothBlock.NotTheCustom;
                // Scope, and it is Anton's own call: unattached women only. No adultery toward
                // another man's wife, in this batch or (probably) ever.
                if (npc.Spouse != null) return TrothBlock.NotFree;
                if (npc.IsChild || npc.Age < 18f || player.Age < 18f) return TrothBlock.TooYoung;
                if (npc.IsNotable || npc.IsMinorFactionHero || npc.IsTemplate) return TrothBlock.NotForMarriage;
                if (!npc.IsWanderer && !npc.IsLord) return TrothBlock.NotForMarriage;
                return TrothBlock.None;
            }
            catch { return TrothBlock.Gone; }
        }

        private LoverRoad.LoverFacts LoverFactsOf(Hero npc, NpcMemory memory)
        {
            return new LoverRoad.LoverFacts
            {
                Stage = memory.CourtshipStage,
                Bond = memory.LoverBond,
                Relation = GetStanding(npc),
                WedToPlayer = Safe(() => FamilyBuilder.AreWed(Hero.MainHero, npc), false),
                BoundElsewhere = Safe(() => npc.Spouse != null && npc.Spouse != Hero.MainHero, false),
                WorldRefusesThePair = LoverWorldBlock(npc) != TrothBlock.None,
            };
        }

        // ------------------------------ the sheet ------------------------------

        /// <summary>Her private section while the bond stands (or once stood) — rides EVERY sheet,
        /// tool or no tool, exactly as the courtship's does: a woman who is his knows she is his
        /// while writing a letter as much as while standing in front of him.</summary>
        private string BuildLoverTerms(Hero npc, NpcMemory memory)
        {
            try
            {
                if (!LoversOn || npc == null) return string.Empty;
                if (memory.LoverBond == LoverBond.None) return string.Empty;

                bool rides = Safe(() => npc.PartyBelongedTo == MobileParty.MainParty, false);
                return LoverText.BondSection(
                    Hero.MainHero?.Name?.ToString() ?? "the traveler",
                    memory.LoverBond,
                    ridesWithHim: rides,
                    boughtFromHerHouse: memory.LoverRansomPaid > 0,
                    hisOtherWomen: OtherWomenOfPlayer(npc).Count,
                    // The concrete thing she might ask for. Abstract craving is a mood; a specific
                    // child with no name is a character with a reason to keep raising it.
                    unnamedChildren: UnnamedChildrenOf(npc));
            }
            catch { return string.Empty; }
        }

        /// <summary>Everyone else the player keeps — his wives and his other lovers, excluding the
        /// one asking. She would know; a household has no secrets, and the situation block has said
        /// as much since the nights shipped.</summary>
        private List<Hero> OtherWomenOfPlayer(Hero except)
        {
            var women = new List<Hero>();
            try
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (except != null) seen.Add(except.StringId);
                foreach (var wife in SpousesOfPlayer())
                    if (wife != null && seen.Add(wife.StringId)) women.Add(wife);
                foreach (var lover in LoversOfPlayer())
                    if (lover != null && seen.Add(lover.StringId)) women.Add(lover);
            }
            catch { }
            return women;
        }

        /// <summary>The head of the house's own case, while his hand truly rides.</summary>
        private string BuildRansomTerms(Hero npc, Hero kin)
        {
            try
            {
                if (!RansomRails(kin, out int reckoning, out int min, out int max)) return string.Empty;
                var kinWord = kin.IsFemale ? "a daughter of our house" : "a son of our house";
                return LoverText.RansomTerms(
                    Hero.MainHero?.Name?.ToString() ?? "the traveler",
                    kin.Name?.ToString() ?? "our kin",
                    kinWord, reckoning, min, max, _config.LoverRansomHagglePercent > 0);
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// What her leaving costs: the worth of the gear she stands up in, floored by her station.
        /// Anton's own anchor — honest, already in the world, and something a player can look at her
        /// and roughly judge. Both equipment sets are counted because both are hers.
        /// </summary>
        private bool RansomRails(Hero kin, out int reckoning, out int min, out int max)
        {
            reckoning = min = max = 0;
            try
            {
                if (kin == null) return false;
                int worth = EquipmentWorth(kin);
                reckoning = LoverRoad.RansomReckoning(worth, StationTierOf(kin));
                LoverRoad.RansomRails(reckoning, _config.LoverRansomHagglePercent, out min, out max);
                return true;
            }
            catch { return false; }
        }

        private static int EquipmentWorth(Hero hero)
        {
            int total = 0;
            try
            {
                foreach (var kit in new[] { hero.BattleEquipment, hero.CivilianEquipment })
                {
                    if (kit == null) continue;
                    for (int slot = 0; slot < (int)EquipmentIndex.NumEquipmentSetSlots; slot++)
                    {
                        var element = kit[(EquipmentIndex)slot];
                        var item = element.Item;
                        if (item != null) total += item.Value;
                    }
                }
            }
            catch { /* a kit we cannot read is simply worth nothing, and the station floor stands */ }
            return total;
        }

        // ------------------------------ the resolvers ------------------------------

        private string ResolveOfferSelf(ToolCall call, Hero npc, Tools.TrothTool.Tally? troth, NpcMemory? liveMemory)
        {
            try
            {
                if (troth == null || !troth.LoverRides)
                    return "This is not the moment for such a thing — I stay in the talk.";
                if (troth.Acted)
                    return "My heart already moved this very breath; one such moment in a talk is enough.";

                var memory = liveMemory ?? LoadMemory(npc);
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var word = Tools.LoverTool.ParseWord(call);

                var verdict = LoverRoad.JudgeTake(LoverFactsOf(npc, memory));
                if (verdict != LoverRoad.LoverVerdict.Allowed)
                {
                    // Answered twice over, as every refusal on this road is: to her in her own
                    // numberless words plus the rail that words alone do nothing, and to the PLAYER
                    // in a plain line naming what was reached for and why nothing was sealed.
                    if (!troth.ByLetter)
                        NotifyLoverRefused(npc, LoverText.TakeRefusalForPlayer(verdict));
                    return LoverText.TakeRefusal(verdict, playerName) + " " + LoverText.WordsDoNotBind;
                }

                troth.Acted = true;
                troth.LaidLoverBond = true;
                troth.Word = word;
                if (!troth.ByLetter) NotifyRoadActivity(npc, "offers you what they have to give…");
                return troth.ByLetter
                    ? "It is offered in this very letter: when it reaches their hands it will stand before them, to take or to let lie by their own hand. Nothing whatever is settled until they choose, and I do not press."
                    : "It is offered: when my words here are done, it will stand before them — to take, or to let lie, by their own hand. Nothing whatever is settled until they choose, and I do not press.";
            }
            catch { return "The moment does not allow it; I let the matter rest."; }
        }

        /// <summary>
        /// Her own hand ending it, reached through <c>tend_courtship</c>'s "apart" — because the
        /// bond IS the deepest thing on that road, and it is what a step back steps back from first.
        /// One step and it is over, which is the honest shape: there was never anything holding it.
        /// Her own tool description tells her that plainly before she can reach for it.
        /// </summary>
        private string EndBondByHerHand(Hero npc, NpcMemory memory, Tools.TrothTool.Tally troth, string word)
        {
            var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
            memory.LoverBond = LoverBond.Former;
            memory.LoverEndedDay = CampaignTime.Now.ToDays;
            troth.Acted = true;
            troth.EndedLoverBond = true;
            troth.Word = word;
            AddSilentInnerBeat(memory, npc, LoverText.PartedByHerBeat(playerName));
            SaveMemory(npc, memory);
            if (!troth.ByLetter)
            {
                NotifyLover($"{npc?.Name} is no longer yours. Nothing was holding it but what they felt.", FrostColor);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            return "It is over, and it is set down. There was never anything holding it but what I felt, and I do not feel it now. "
                 + "I speak from where I truly stand, and I owe no explanation beyond what I choose to give.";
        }

        private string ResolveRansomLay(ToolCall call, Hero npc, Tools.TrothTool.BlessTally? bless)
        {
            try
            {
                if (bless == null || !bless.IsRansom || bless.Bride == null)
                    return "There is no such matter between us just now; I stay in the talk.";
                if (bless.Laid)
                    return "I have named my figure this very breath. I do not name it twice in one conversation.";

                var kin = bless.Bride;
                if (!RansomRails(kin, out int reckoning, out int min, out int max))
                    return "I cannot reckon such a thing just now; let the matter wait.";

                int price = Tools.LoverTool.ParsePrice(call) ?? reckoning;
                if (price < 0)
                    return "That is not a figure. Name a plain sum in denars or let me name my own.";
                if (price < min || price > max)
                    return $"That is outside what I will hear. The standing of my house sets the bounds, and I have named them; "
                         + "I will not go beyond them however it is put, and I do not name my lowest as an offer.";

                bless.Laid = true;
                bless.Price = price;
                if (!bless.ByLetter) NotifyRoadActivity(npc, "names what her going will cost…");
                return bless.ByLetter
                    ? $"The figure is named in this letter: {price} denars, and nothing of hers goes with her beyond what she stands up in. When it reaches them, the choice is theirs alone."
                    : $"The figure is named: {price} denars, and nothing of hers goes with her beyond what she stands up in. When my words here are done it will stand before them, to take or to leave. I do not press, and I do not soften it.";
            }
            catch { return "The matter does not allow it just now; I let it rest."; }
        }

        // ------------------------------ the seals (the only doors) ------------------------------

        // Both seals are presented by PresentTrothIfAny, which already runs after the reply lands
        // on every render path — one door for the whole road, both branches of it.
        private void ShowLoverInquiry(Hero npc, string word)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                var their = npc != null && npc.IsFemale ? "her" : "his";
                var they = npc != null && npc.IsFemale ? "she" : "he";
                var wordLine = string.IsNullOrWhiteSpace(word) ? string.Empty : $"\n\n“{word.Trim()}”";

                // The popup says the whole truth before the press, including what it will DO —
                // the mod's own discipline, and the only place the player is told the cost.
                var rides = WillRideWithYou(npc, out string whyNot);
                var joining = rides
                    ? $"\n\n{name} will ride with you from now on — not hired, and not counted against the company you may keep."
                    : (string.IsNullOrEmpty(whyNot) ? string.Empty : $"\n\n{name} stays where {they} is for now — {whyNot}.");

                var body =
                    $"{name} offers you {their}self. Not {their} hand — there is no wedding in this and no prospect of one: " +
                    $"no vow, no house that takes {their} name, and no word of the two of you said before anyone.{wordLine}\n\n" +
                    $"The world has a name for a woman in that place, and {they} knows it as well as you do.{joining}\n\n" +
                    "Take what is offered?";
                var data = new InquiryData(
                    new TextObject("{=ImmersiveAI_LoverTitle}Something is offered you").ToString(),
                    body, true, true,
                    new TextObject("{=ImmersiveAI_LoverAccept}Take it").ToString(),
                    new TextObject("{=ImmersiveAI_LoverDecline}Let it lie").ToString(),
                    () => OnLoverSealed(npc),
                    () => OnLoverDeclined(npc),
                    "", 0f, null, null, null);
                InformationManager.ShowInquiry(data, pauseGameActiveState: true);
            }
            catch (Exception ex) { ModLog.Error("showing the offer", ex); }
        }

        private void OnLoverSealed(Hero npc)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var memory = LoadMemory(npc);

                // Every rule re-run at the seal, never trusted from the lay: a popup can sit open
                // while the world moves, and this one changes a life.
                var verdict = LoverRoad.JudgeTake(LoverFactsOf(npc, memory));
                if (verdict != LoverRoad.LoverVerdict.Allowed)
                {
                    var why = LoverText.TakeRefusalForPlayer(verdict);
                    NotifyLover($"It could not be taken — {why}.", SealGrey);
                    AppendRecordedTurn(npc, LoverText.BondBlockedBeat(playerName, why + "."), string.Empty);
                    return;
                }

                double now = CampaignTime.Now.ToDays;
                memory.LoverBond = LoverBond.Lover;
                memory.LoverSinceDay = now;
                memory.LoverEndedDay = -1;
                AddSilentInnerBeat(memory, npc, LoverText.BondSealedBeat(playerName));
                memory.NotePlayerEngaged();
                SaveMemory(npc, memory);

                MarkMetInWorldsEyes(npc);
                NotifyLover($"{npc?.Name} is yours. Nothing was promised, and nothing was said before anyone.", RoadColor);

                // Nothing was said before anyone — which has never once stopped a household knowing.
                LeakTheBond(npc);

                // She comes for love, not hire — so she rides, and the company's own limit has
                // nothing to say about it. A woman still under her father's roof stays there until
                // he is paid, which is a separate door and a separate price.
                if (BringHerAlong(npc, out string joinNote) && !string.IsNullOrEmpty(joinNote))
                    NotifyLover(joinNote, RoadColor);

                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("sealing the bond", ex); }
        }

        private void OnLoverDeclined(Hero npc)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                NotifyLover("You let it lie.", SealGrey);
                AppendRecordedTurn(npc, LoverText.BondDeclinedBeat(playerName), string.Empty);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("declining the offer", ex); }
        }

        private void ShowRansomInquiry(Hero npc, Tools.TrothTool.BlessTally bless)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                var kin = bless.Bride;
                var kinName = kin?.Name?.ToString() ?? "their kin";
                string reckon = string.Empty;
                if (RansomRails(kin, out int reckoning, out _, out _) && reckoning != bless.Price)
                    reckon = $" (Their own reckoning of her leaving is {reckoning} denars.)";
                var body =
                    $"{name} names what it costs to take {kinName} out of their house: {bless.Price} denars — " +
                    $"the sum spoken between you.{reckon}\n\n" +
                    $"There is no blessing in this and no wedding at the end of it. The gold settles what is owed " +
                    $"to the house and nothing else; {name} will not think better of you for paying it.\n\n" +
                    $"Pay, and take her with you?";
                var data = new InquiryData(
                    new TextObject("{=ImmersiveAI_RansomTitle}A price is named").ToString(),
                    body, true, true,
                    new TextObject("{=ImmersiveAI_RansomAccept}Pay it").ToString(),
                    new TextObject("{=ImmersiveAI_RansomDecline}Let it lie").ToString(),
                    () => OnRansomSealed(npc, bless),
                    () => OnRansomDeclined(npc, bless),
                    "", 0f, null, null, null);
                InformationManager.ShowInquiry(data, pauseGameActiveState: true);
            }
            catch (Exception ex) { ModLog.Error("showing the price", ex); }
        }

        private void OnRansomSealed(Hero npc, Tools.TrothTool.BlessTally bless)
        {
            try
            {
                var player = Hero.MainHero;
                var playerName = player?.Name?.ToString() ?? "the traveler";
                var kin = bless.Bride;
                var kinName = kin?.Name?.ToString() ?? "their kin";

                string? blocked = null;
                if (kin == null || !kin.IsAlive || npc == null || !npc.IsAlive) blocked = "one of them is beyond the world's reach";
                else if (player == null || player.Gold < bless.Price) blocked = "your purse no longer carries the price";
                else if (npc.MapFaction != null && player.MapFaction != null
                    && npc.MapFaction.IsAtWarWith(player.MapFaction)) blocked = "your realms now stand at war";
                else if (kin.Clan != npc.Clan) blocked = "she is no longer of their house";
                else
                {
                    var hers = LoadMemory(kin);
                    if (hers.LoverBond != LoverBond.Lover) blocked = "she is not yours to take";
                    else if (hers.LoverRansomPaid > 0) blocked = "her house has been paid for her already";
                }

                if (blocked != null)
                {
                    NotifyLover($"Nothing was settled — {blocked}.", SealGrey);
                    AppendRecordedTurn(npc,
                        $"I named what {kinName}'s going would cost and the thing came to nothing — {blocked}. No gold passed, and she is still under my roof.",
                        string.Empty);
                    return;
                }

                GiveGoldAction.ApplyBetweenCharacters(player, npc, bless.Price);

                var memory = LoadMemory(kin!);
                memory.LoverRansomPaid = bless.Price;
                AddSilentInnerBeat(memory, kin!, LoverText.RansomNewsBeat(playerName,
                    npc!.Name?.ToString() ?? string.Empty, bless.Price, npc.IsFemale));
                SaveMemory(kin!, memory);

                // He is paid and he is not appeased — the one thing the coin does not buy, and the
                // reason this is the blessing's mold with its sign reversed.
                ApplyRelationShift(npc, -RansomInsult);
                AppendRecordedTurn(npc,
                    LoverText.RansomTakenBeat(playerName, kinName, bless.Price),
                    string.Empty, OutreachMark.PlayerEngaged);

                // And he may want to say something about it. NOT a scripted letter of grievance —
                // the beat above is the FACT, and what he makes of it is his; he is asked in his
                // own mind whether he wishes to write at all, and a head of a house who decides to
                // say nothing is saying a great deal. The same door the nights open when a woman
                // has news that will not wait its turn.
                InviteHimToSayHisPiece(npc);

                bool left = LeaveHerHouse(kin!);
                NotifyLover(left
                    ? $"{bless.Price} denars to {npc.Name}, and {kinName} is out of their house."
                    : $"{bless.Price} denars to {npc.Name} for {kinName}.", RoadColor);

                if (left && BringHerAlong(kin!, out string joinNote) && !string.IsNullOrEmpty(joinNote))
                    NotifyLover(joinNote, RoadColor);

                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
                UI.TalkUI.OnThreadChanged(kin!, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("paying for her", ex); }
        }

        private void OnRansomDeclined(Hero npc, Tools.TrothTool.BlessTally bless)
        {
            try
            {
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var kinName = bless.Bride?.Name?.ToString() ?? "their kin";
                NotifyLover("You let the price lie.", SealGrey);
                AppendRecordedTurn(npc, LoverText.RansomDeclinedBeat(playerName, kinName, bless.Price), string.Empty);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("declining the price", ex); }
        }

        /// <summary>
        /// The one outreach the world's quiet does not hold back: a man whose daughter has just
        /// been bought out from under him does not wait his turn in the socialness roll. He is only
        /// ASKED — face to face if the player is still standing there, by letter if not, and in
        /// either case his own mind answers first and may well answer no.
        /// </summary>
        private void InviteHimToSayHisPiece(Hero head)
        {
            try
            {
                if (head == null) return;
                if (LlmGate.AutonomyQuiet || UsageLedger.DailyCapReached) return;
                if (IsCoLocated(head))
                {
                    if (_initiationInFlight || !IsSafeToInitiate()) return;
                    MarkInitiationInFlight();
                    _ = BeginInitiationAsync(head);
                }
                else if (_config.EnableLetters && !_letterWorkInFlight)
                {
                    MarkLetterWorkInFlight();
                    _ = BeginNpcLetterAsync(head);
                }
            }
            catch (Exception ex) { ModLog.Error("letting him say his piece", ex); }
        }

        /// <summary>What her house's head loses of his regard for the player when the gold is taken.
        /// Enough to be felt for a long while, short of the outright enmity that would close every
        /// other road between the two houses forever.</summary>
        private const int RansomInsult = 20;

        // ------------------------------ the world act ------------------------------

        /// <summary>Whether she would ride with the player if the bond were sealed right now, and
        /// the plain reason when she would not — asked BEFORE the seal so the popup can say it.</summary>
        private bool WillRideWithYou(Hero npc, out string whyNot)
        {
            whyNot = string.Empty;
            try
            {
                if (npc == null) return false;
                if (npc.PartyBelongedTo == MobileParty.MainParty) { whyNot = string.Empty; return false; }
                if (IsOfPlayerHousehold(npc)) return true;
                var memory = LoadMemory(npc);
                if (memory.LoverRansomPaid > 0) return true;
                whyNot = $"her house has not been paid for her, and {npc.Clan?.Leader?.Name?.ToString() ?? "its head"} will want paying";
                return false;
            }
            catch { return false; }
        }

        /// <summary>Whether she is already the player's to bring along — a companion, or of his own
        /// clan — rather than a daughter of some other house.</summary>
        private static bool IsOfPlayerHousehold(Hero npc)
        {
            try
            {
                if (npc == null) return false;
                if (npc.CompanionOf == Clan.PlayerClan) return true;
                if (npc.Clan == Clan.PlayerClan) return true;
                // An unhired wanderer belongs to nobody, and belongs to nobody's house.
                return npc.IsWanderer && npc.Clan == null;
            }
            catch { return false; }
        }

        /// <summary>
        /// She rides. NOT hired: no wage bargain, no price, and — the promise this keeps — no check
        /// against the company's own limit, which governs who you may HIRE and has nothing to say
        /// about who loves you. An unhired wanderer is taken into the clan's company so the game
        /// has somewhere to put her; a woman already of the player's house simply joins the party.
        /// </summary>
        private bool BringHerAlong(Hero npc, out string note)
        {
            note = string.Empty;
            try
            {
                if (npc == null) return false;
                if (npc.PartyBelongedTo == MobileParty.MainParty) return false;
                if (!IsOfPlayerHousehold(npc))
                {
                    var memory = LoadMemory(npc);
                    if (memory.LoverRansomPaid <= 0) return false;
                }
                if (!IsCoLocated(npc)) return false;

                // The wanderer needs a place in the clan before she can have a place in the party.
                // Deliberately NOT gated on Clan.CompanionLimit — see the remarks above.
                if (npc.IsWanderer && npc.CompanionOf != Clan.PlayerClan)
                    AddCompanionAction.Apply(Clan.PlayerClan, npc);

                AddHeroToPartyAction.Apply(npc, MobileParty.MainParty, showNotification: false);
                note = $"{npc.Name} rides with you now — not hired, and not counted against the company you may keep.";
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("bringing her along", ex);
                return false;
            }
        }

        /// <summary>
        /// She leaves her father's house. Vanilla's own clan-change housekeeping, mirrored from
        /// <c>MarriageAction.HandleClanChangeAfterMarriageForHero</c> (decompile-verified
        /// 2026.08.15) and done step by step so a failure anywhere costs only that step: her
        /// governorship goes, her command is stood down, she is made a fugitive of her old house,
        /// and only THEN does the clan change — the same order vanilla uses, for the same reason.
        /// Disbanding a lord's party when she elopes is not a side effect to be avoided; it is
        /// what her going actually costs her house.
        /// </summary>
        private bool LeaveHerHouse(Hero npc)
        {
            try
            {
                var target = Clan.PlayerClan;
                if (npc == null || target == null) return false;
                if (npc.Clan == target) return true;
                var old = npc.Clan;

                try { if (npc.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(npc); } catch { }

                try
                {
                    var party = npc.PartyBelongedTo;
                    if (party != null)
                    {
                        if (old?.Kingdom != target.Kingdom && party.Army != null)
                        {
                            if (party.Army.LeaderParty == party) DisbandArmyAction.ApplyByUnknownReason(party.Army);
                            else party.Army = null;
                        }
                        bool wasLeader = party.LeaderHero == npc;
                        party.MemberRoster.RemoveTroop(npc.CharacterObject);
                        MakeHeroFugitiveAction.Apply(npc);
                        if (wasLeader && party.IsLordParty) DisbandPartyAction.StartDisband(party);
                    }
                }
                catch (Exception ex) { ModLog.Error("standing down her command", ex); }

                npc.Clan = target;

                try
                {
                    foreach (var soul in old?.Heroes ?? Enumerable.Empty<Hero>()) soul?.UpdateHomeSettlement();
                    foreach (var soul in target.Heroes) soul?.UpdateHomeSettlement();
                }
                catch { }

                return npc.Clan == target;
            }
            catch (Exception ex)
            {
                ModLog.Error("taking her out of her house", ex);
                return false;
            }
        }

        // ------------------------------ the leak ------------------------------

        /// <summary>
        /// WORD GETS ROUND (2026.08.15). The day a woman becomes his, the other women of his hearth
        /// may hear of it — and they hear it as anyone hears such things, sideways, before he tells
        /// them. It is priced by the same honesty the night leaks are: a town hides a great deal,
        /// a camp on the road hides nothing.
        ///
        /// TWO THINGS IT DOES NOT DO, both deliberate. It does not tell her how to feel — she gets
        /// one flat silent beat with the fact in it, and everything after that is hers. And it does
        /// not write anything on her door: if she wants that written down she will write it herself,
        /// in her own words, in the talk this leak is about to bring about. That whole loop —
        /// leak, spike, confrontation, her own hand — is the design, and none of it is scripted.
        /// </summary>
        private void LeakTheBond(Hero her)
        {
            try
            {
                if (her == null || !LoversOn) return;
                var herName = Safe(() => her.Name?.ToString() ?? "she", "she");
                var playerName = Hero.MainHero?.Name?.ToString() ?? "he";

                foreach (var other in OtherWomenOfPlayer(her))
                {
                    if (other == null) continue;
                    int odds = LeakOdds(other);
                    if (odds <= 0 || MBRandom.RandomInt(100) >= odds) continue;

                    bool sheIsAWife = Safe(() => FamilyBuilder.AreWed(Hero.MainHero, other), false);
                    // WriteNightBeat, not a raw load-and-save: a soul whose own exchange is in
                    // flight holds a memory instance loaded before this moment and would save
                    // straight over the beat. Hers is parked and folded in by SaveMemory — the
                    // discipline every mid-flight write in this mod follows (self-review, 2026.08.15).
                    WriteNightBeat(other, LoverText.OtherWomanLearnsBeat(playerName, herName, sheIsAWife),
                        OutreachMark.None);
                    // And for a day and a half it is the loudest thing in her. A stamp lost to a
                    // race costs only a quieter morning, which is why it does not need parking.
                    MarkFreshWound(other, CampaignTime.Now.ToDays);
                    UI.TalkUI.OnThreadChanged(other, markUnread: true);
                }
            }
            catch (Exception ex) { ModLog.Error("word getting round", ex); }
        }

        /// <summary>
        /// How likely she is to hear of it. The nights' own reckoning, reused rather than reinvented:
        /// where the player is standing decides how loud his life is, and a woman who is not with
        /// him at all only hears what word carries — which, for this, is a good deal, because this
        /// is exactly the kind of thing word carries.
        /// </summary>
        private int LeakOdds(Hero other)
        {
            try
            {
                var settlement = Safe(() => Hero.MainHero?.CurrentSettlement ?? MobileParty.MainParty?.CurrentSettlement,
                    (Settlement?)null);
                string placeKind = PlaceKindNow(settlement);
                // A new bond is louder than an ordinary night and quieter than a jewel: it is news,
                // and news travels.
                int odds = AwarenessPercent(placeKind, NightGifts.Resolve(300));
                if (!IsCoLocated(other)) odds = WordCanReach(other) ? odds / 2 : 0;
                return odds;
            }
            catch { return 0; }
        }

        // ------------------------------ what the rest of the mod reads ------------------------------

        /// <summary>Whether this soul is the player's lover right now.</summary>
        internal static bool IsLoverOfPlayer(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null || !self.LoversOn) return false;
                var known = MemoryIndex.Get(NpcPaths.MemoryFile(npc), self._memoryStore);
                return known != null && (LoverBond)known.LoverBond == LoverBond.Lover;
            }
            catch { return false; }
        }

        /// <summary>Every living soul who is the player's lover — the wives' counterpart, and the
        /// list the hearth, the leaks and the sheet all draw on.</summary>
        internal static List<Hero> LoversOfPlayer()
        {
            var list = new List<Hero>();
            try
            {
                var self = Current;
                if (self == null || !self.LoversOn) return list;
                // The index answers this without opening a single memory file — which is the whole
                // reason it stamps the bond (see MemoryIndex.Entry.LoverBond).
                foreach (var known in MemoryIndex.All(NpcPaths.CampaignRoot, NpcPaths.MemoryFileName, self._memoryStore))
                {
                    if ((LoverBond)known.LoverBond != LoverBond.Lover) continue;
                    var hero = FindAliveHero(known.NpcId);
                    if (hero == null || !hero.IsAlive || hero.IsPrisoner) continue;
                    list.Add(hero);
                }
            }
            catch (Exception ex) { ModLog.Error("gathering the lovers", ex); }
            return list;
        }

        /// <summary>Where this bond stands, in words and never in numbers — for the player's own
        /// views.</summary>
        internal static string LoverStandingLine(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null || !self.LoversOn) return string.Empty;
                var memory = self.LoadMemory(npc);
                if (memory.LoverBond == LoverBond.None) return string.Empty;
                return LoverText.StandingLine(memory.LoverBond,
                    HeartBands.Of(GetStanding(npc), memory.CourtshipStage));
            }
            catch { return string.Empty; }
        }

        // ------------------------------ notices ------------------------------

        private void NotifyLover(string line, Color? color = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage(line, color ?? RoadColor)));
            }
            catch { /* the notice is a nicety */ }
        }

        /// <summary>The player's own line when a reach on this road was refused. NOT gated on
        /// ShowNpcActivity, for the same reason the courtship's own refusal notice is not: a rail
        /// nobody can see is indistinguishable from a broken mod.</summary>
        private void NotifyLoverRefused(Hero npc, string reason)
        {
            try
            {
                var name = npc?.Name?.ToString() ?? "They";
                NotifyLover($"{name} reached for something more than words with you, and it came to nothing — {reason}. "
                          + "Whatever is said now, nothing has been given.", FrostColor);
            }
            catch { }
        }

        // ------------------------------ DevMode levers ------------------------------

        /// <summary>Dev: stage the whole household without playing forty hours (Anton's ask). Walks
        /// her heart to where the fork opens and seals the bond outright — the REAL seal, so the
        /// machinery is proved rather than mimed.</summary>
        internal void MakeLoverFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                if (!LoversOn)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The lover's road is turned off in the mod options.", SealGrey));
                    return;
                }
                var block = LoverWorldBlock(npc);
                if (block != TrothBlock.None)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Not possible — {TrothBlockForPlayer(block, npc)}.", SealGrey));
                    return;
                }

                var memory = LoadMemory(npc);
                if (memory.CourtshipStage < LoverRoad.ForksFrom)
                {
                    memory.CourtshipStage = LoverRoad.ForksFrom;
                    memory.CourtshipStepDay = CampaignTime.Now.ToDays;
                }
                SaveMemory(npc, memory);
                if (GetStanding(npc) < HeartBands.DeepLoveFloor)
                    ApplyRelationShift(npc, HeartBands.DeepLoveFloor - GetStanding(npc));

                OnLoverSealed(npc);
            }
            catch (Exception ex) { ModLog.Error("dev: making a lover", ex); }
        }

        internal static void DevMakeLover(Hero npc)
        {
            try { if (npc != null) Current?.MakeLoverFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: making a lover", ex); }
        }

        /// <summary>Dev: the player's own hand ending it — the other half of "or the player's".</summary>
        internal void EndLoverFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                var memory = LoadMemory(npc);
                if (memory.LoverBond != LoverBond.Lover)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{npc.Name} is not yours.", SealGrey));
                    return;
                }
                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                memory.LoverBond = LoverBond.Former;
                memory.LoverEndedDay = CampaignTime.Now.ToDays;
                AddSilentInnerBeat(memory, npc, LoverText.SetAsideBeat(playerName));
                SaveMemory(npc, memory);
                NotifyLover($"{npc.Name} is no longer yours. The gold that bought her out is not coming back.", FrostColor);
                UI.TalkUI.OnThreadChanged(npc, markUnread: true);
            }
            catch (Exception ex) { ModLog.Error("ending the bond", ex); }
        }

        internal static void DevEndLover(Hero npc)
        {
            try { if (npc != null) Current?.EndLoverFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: ending a bond", ex); }
        }
    }
}
