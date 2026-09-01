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
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImmersiveAI
{
    /// <summary>
    /// THE PROPOSAL (2026.08.31, Anton's design — after the Rhia courtship turned the funnest part
    /// into the biggest irritation): once nothing in her would refuse — regard deep enough, station
    /// passed, her own misgivings weighed and none standing — the asking becomes the PLAYER'S, a
    /// visible button instead of a talk he must steer and a tool she must remember to reach for.
    /// He chooses the gift, adds a line of his own to shape the day, and the seal is code: she
    /// cannot say no, because every rail that could have said no has already passed. Then the
    /// chronicler writes the day — the wedding chronicle's own machinery, one register, one call —
    /// and both of them can call it back forever.
    ///
    /// Her OWN door stays open beside it: a promise she lays in a talk or a letter still seals
    /// through the old popup, and since this day it too is written down and recallable. The two
    /// doors meet in one record.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private BetrothalLedger? _betrothalLedger;

        private readonly HashSet<string> _betrothalsWriting = new HashSet<string>(StringComparer.Ordinal);
        private const int MaxBetrothalRetries = 3;
        private int _betrothalRetries;
        private bool _betrothalFailureTold;

        // The writing rides the wedding chronicle's switch on purpose: they are one family of
        // accounts, and a player who turned the wedding's off wanted none of them. The SEAL runs
        // regardless — turning off the chronicle must never take the button away.
        private bool BetrothalWritingOn => _config.EnableConversationMarriage
            && _config.EnableWeddingChronicle && _betrothalLedger != null;

        private static bool IsUnwritten(BetrothalRecord? record) =>
            record != null && string.IsNullOrWhiteSpace(record.Account);

        private void LoadBetrothalLedger()
        {
            try { _betrothalLedger = BetrothalLedger.LoadFrom(NpcPaths.BetrothalsFolder); }
            catch { _betrothalLedger = null; }
        }

        // ------------------------------ the door ------------------------------

        /// <summary>
        /// Whether the "Ask for their hand" door stands open for this pair RIGHT NOW. One judgment,
        /// shared by the page that shows the button and the seal that re-runs it: stage at devotion
        /// or ready (Anton's call — the asking IS the word being spoken, so her own step to
        /// readiness is carried by the question itself), every road rail passed, no world block,
        /// no promise standing elsewhere, and the two truly together — a proposal is face to face.
        /// </summary>
        private bool ProposalDoorOpen(Hero npc, NpcMemory memory)
        {
            try
            {
                if (npc == null || memory == null || !_config.EnableConversationMarriage) return false;
                var stage = memory.CourtshipStage;
                if (stage != CourtshipStage.Devotion && stage != CourtshipStage.Ready) return false;
                if (TrothBlockReason(npc, forWedding: false, _config) != TrothBlock.None) return false;
                if (PlayerPromisedToAnother(npc)) return false;
                if (CourtshipRoad.JudgeForward(RoadFactsOf(npc, memory)) != CourtshipRoad.StepVerdict.Allowed)
                    return false;
                return IsCoLocated(npc);
            }
            catch { return false; }
        }

        /// <summary>The windows' own way in.</summary>
        internal static void OpenProposalDoorFor(Hero npc)
        {
            try { if (npc != null) Current?.ShowProposalGiftInquiry(npc); }
            catch (Exception ex) { ModLog.Error("opening the proposal door", ex); }
        }

        // The gift first: what is set on her hand, and with it how richly the day is written.
        // The wedding scale inquiry's mold — the world's rules run before a single coin shows.
        private void ShowProposalGiftInquiry(Hero npc)
        {
            try
            {
                var player = Hero.MainHero;
                if (npc == null || player == null) return;

                var memory = LoadMemory(npc);
                if (!ProposalDoorOpen(npc, memory))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The asking is not open just now — see “Between us” for what stands in the way.", SealGrey));
                    return;
                }

                var elements = new List<InquiryElement>();
                foreach (var tier in BetrothalGifts.All)
                {
                    bool afford = player.Gold >= tier.Price;
                    var why = tier.PlayerDescription
                        + (tier.Price > 0 ? $"\n\nThe richer the gift, the fuller the written day." : "\n\nThe day is written all the same.")
                        + (afford ? string.Empty : "\n\n(Your purse cannot carry this.)");
                    elements.Add(new InquiryElement(
                        tier.Gift,
                        tier.Price > 0 ? $"{tier.Name} — {tier.Price} denars" : tier.Name,
                        null, afford, why));
                }

                var data = new MultiSelectionInquiryData(
                    new TextObject("{=ImmersiveAI_ProposeTitle}Ask for their hand").ToString(),
                    $"You are about to ask {npc.Name} to be yours. Nothing in them would refuse you.\n\n"
                    + $"Choose what you set on their hand. You hold {player.Gold} denars.",
                    elements, true, 1, 1,
                    new TextObject("{=ImmersiveAI_ProposeNext}So be it").ToString(),
                    new TextObject("{=ImmersiveAI_ProposeNotYet}Not yet").ToString(),
                    chosen =>
                    {
                        try
                        {
                            var pick = chosen?.FirstOrDefault()?.Identifier;
                            if (!(pick is BetrothalGift gift)) return;
                            // A new inquiry never opens inside the closing one's own callback.
                            MainThreadDispatcher.Enqueue(() => ShowProposalWishInquiry(npc, gift));
                        }
                        catch (Exception ex) { ModLog.Error("choosing the betrothal gift", ex); }
                    },
                    _ => { });
                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex) { ModLog.Error("showing the proposal's own door", ex); }
        }

        // Then his own line — the night chronicle's wish, worn by the asking. Optional; the affirm
        // is the seal itself, so this popup is the last thing before the promise is given.
        private void ShowProposalWishInquiry(Hero npc, BetrothalGift gift)
        {
            try
            {
                var tier = BetrothalGifts.Of(gift);
                var priceLine = tier.Price > 0 ? $" {tier.Name} — {tier.Price} denars." : string.Empty;
                var inquiry = new TextInquiryData(
                    new TextObject("{=ImmersiveAI_ProposeWish}How do you ask?").ToString(),
                    $"A line of your own to shape the day — the place, the words, the gesture — or leave it empty.{priceLine}",
                    true, true,
                    new TextObject("{=ImmersiveAI_ProposeAsk}Ask for their hand").ToString(),
                    new TextObject("{=ImmersiveAI_ProposeNotYet}Not yet").ToString(),
                    new Action<string>(wish => OnProposalSealed(npc, gift, wish ?? string.Empty)),
                    new Action(() => { }),
                    false, null, "", "");
                InformationManager.ShowTextInquiry(inquiry, true);
            }
            catch (Exception ex) { ModLog.Error("asking how you ask", ex); }
        }

        // ------------------------------ the seal ------------------------------

        /// <summary>
        /// The asking itself. Every rule re-runs here, never trusted from the popups (a battle's
        /// ransom may have emptied the purse between choosing and sealing). SHE IS NOT ASKED:
        /// the door existed only because nothing in her would refuse, so the yes is a fact the
        /// world already holds — the chronicler's job is to tell it, not to roll for it.
        /// From devotion the seal carries her readiness and the promise in one breath: the two
        /// steps run the identical gates, so one Allowed honestly covers both.
        /// </summary>
        private void OnProposalSealed(Hero npc, BetrothalGift gift, string wish)
        {
            try
            {
                var player = Hero.MainHero;
                var playerName = player?.Name?.ToString() ?? "the traveler";
                var memory = LoadMemory(npc);
                var tier = BetrothalGifts.Of(gift);

                var block = TrothBlockReason(npc, forWedding: false, _config);
                if (block == TrothBlock.None && PlayerPromisedToAnother(npc)) block = TrothBlock.PromisedElsewhere;
                if (block == TrothBlock.None
                    && memory.CourtshipStage != CourtshipStage.Devotion
                    && memory.CourtshipStage != CourtshipStage.Ready)
                    block = TrothBlock.WorldRefuses;
                if (block == TrothBlock.None
                    && CourtshipRoad.JudgeForward(RoadFactsOf(npc, memory)) != CourtshipRoad.StepVerdict.Allowed)
                    block = TrothBlock.WorldRefuses;
                if (block == TrothBlock.None && !IsCoLocated(npc)) block = TrothBlock.NotHere;

                if (block == TrothBlock.None && tier.Price > 0 && (player?.Gold ?? 0) < tier.Price)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The asking waits — {tier.Price} denars is more than you hold.", SealGrey));
                    return;
                }

                if (block != TrothBlock.None)
                {
                    var line = $"The promise could not be given — {TrothBlockForPlayer(block, npc)}.";
                    NoteRoad(npc, memory, RoadNotes.KindRefused, line);
                    InformationManager.DisplayMessage(new InformationMessage(line, SealGrey));
                    AppendRecordedTurn(npc,
                        CourtshipText.BetrothalBlockedBeat(playerName, TrothBlockForNpc(block, npc)),
                        string.Empty);
                    return;
                }

                // The record is captured BEFORE the seal mutates the road's days, so the road
                // phrase tells the courtship as it stood when the question was asked.
                var record = CaptureBetrothal(npc, memory, askedByPlayer: true, gift, wish, herWord: string.Empty);

                if (tier.Price > 0) player!.ChangeHeroGold(-tier.Price);

                var now = CampaignTime.Now.ToDays;
                memory.CourtshipStage = CourtshipStage.Betrothed;
                memory.BetrothedGameDay = now;
                memory.CourtshipStepDay = now;
                AddSilentInnerBeat(memory, npc, CourtshipText.ProposalSealedBeat(playerName, tier.GiftName));
                memory.NotePlayerEngaged();

                var sealedLine = $"You asked for {npc?.Name}'s hand, and the answer was yes. You are betrothed.";
                NoteRoad(npc, memory, RoadNotes.KindSealed, sealedLine);
                SaveMemory(npc, memory);

                InformationManager.DisplayMessage(new InformationMessage(sealedLine, SealGreen));
                MirrorRomance(npc, CourtshipStage.Betrothed);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);

                BeginBetrothalChronicleFor(npc, record);
            }
            catch (Exception ex) { ModLog.Error("sealing the proposal", ex); }
        }

        // ------------------------------ the facts of the day ------------------------------

        // The plain record, game thread only — captured at the seal, whichever door sealed it.
        private BetrothalRecord CaptureBetrothal(Hero npc, NpcMemory memory, bool askedByPlayer,
            BetrothalGift gift, string wish, string herWord)
        {
            var player = Hero.MainHero;
            var settlement = WeddingPlace();
            double now = CampaignTime.Now.ToDays;
            var tier = BetrothalGifts.Of(gift);

            return new BetrothalRecord
            {
                Id = _betrothalLedger?.NextId(now) ?? $"d{(int)Math.Floor(now):0000}",
                GameDay = now,
                DateText = CalradiaDate(),
                PlaceName = Safe(() => settlement == null ? string.Empty : SettlementPhrase(settlement), string.Empty),
                CultureName = Safe(() => settlement?.Culture?.Name?.ToString() ?? string.Empty, string.Empty),
                SpouseId = npc.StringId,
                SpouseName = Safe(() => npc.Name?.ToString() ?? "Unknown", "Unknown"),
                SpouseIsFemale = Safe(() => npc.IsFemale, true),
                SpouseStation = Safe(() => PersonaBuilder.Build(npc, _config)?.RoleDescription ?? string.Empty, string.Empty),
                SpouseAge = Safe(() => (int)npc.Age, 0),
                PlayerName = Safe(() => player?.Name?.ToString() ?? "the traveler", "the traveler"),
                PlayerIsFemale = Safe(() => player?.IsFemale ?? false, false),
                PlayerAge = Safe(() => (int)(player?.Age ?? 0f), 0),
                PlayerClanName = Safe(() => Clan.PlayerClan?.Name?.ToString() ?? string.Empty, string.Empty),
                CourtshipDays = memory.CourtshipStepDay < 0 ? -1 : Math.Max(0, now - memory.CourtshipStepDay),
                MisgivingsAnswered = memory.CourtshipMisgivings
                    .Where(m => m != null && m.Settled && !string.IsNullOrWhiteSpace(m.Text))
                    .Select(m => m.Text.Trim())
                    .Take(5).ToList(),
                AskedByPlayer = askedByPlayer,
                Gift = gift,
                GiftCost = tier.Price,
                GiftName = tier.GiftName,
                PlayerWish = (wish ?? string.Empty).Trim(),
                HerWord = (herWord ?? string.Empty).Trim(),
            };
        }

        private BetrothalText.Facts GatherBetrothalFacts(Hero npc, BetrothalRecord record)
        {
            var tier = BetrothalGifts.Of(record.Gift);
            var facts = new BetrothalText.Facts
            {
                SpouseName = record.SpouseName,
                SpouseGenderWord = record.SpouseIsFemale ? "woman" : "man",
                SpouseStation = record.SpouseStation,
                PlayerName = record.PlayerName,
                PlayerGenderWord = record.PlayerIsFemale ? "woman" : "man",
                DateText = record.DateText,
                PlacePhrase = record.PlaceName,
                CultureName = record.CultureName,
                MisgivingsAnswered = record.MisgivingsAnswered.ToList(),
                AskedByPlayer = record.AskedByPlayer,
                GiftNote = string.IsNullOrWhiteSpace(record.GiftName) ? string.Empty : tier.ChroniclerNote,
                PlayerWish = record.PlayerWish,
                HerWord = record.HerWord,
                MinSentences = tier.MinSentences,
                MaxSentences = tier.MaxSentences,
            };

            // Ages off the RECORD, never the live heroes — a retried account must still be the day.
            facts.SpouseAge = record.SpouseAge > 0 ? record.SpouseAge : Safe(() => (int)npc.Age, 0);
            facts.PlayerAge = record.PlayerAge > 0 ? record.PlayerAge : Safe(() => (int)(Hero.MainHero?.Age ?? 0f), 0);
            try
            {
                var persona = PersonaBuilder.Build(npc, _config);
                facts.SpouseTraits = persona?.PersonalityDescription ?? string.Empty;
            }
            catch { }
            try
            {
                facts.SpouseSelfText = PromptFiles.LoadNpcPrompt(
                    NpcPaths.CustomInstructionsFile(npc), record.SpouseName);
            }
            catch { }
            try { facts.PlayerStanding = PlayerStandingPhrase(); } catch { }
            try { facts.SeasonPhrase = SeasonPhrase(); } catch { }
            try
            {
                var memory = LoadMemory(npc);
                facts.SharedStory = memory.DeepMemoryText();
                facts.RecentWords = RecentSpokenWords(memory, record.PlayerName, record.SpouseName);
            }
            catch { }
            try
            {
                if (record.CourtshipDays >= 1)
                    facts.RoadPhrase = $"their hearts had walked this road some {(int)Math.Round(record.CourtshipDays)} days";
                else if (record.CourtshipDays >= 0)
                    facts.RoadPhrase = "their hearts had come to this within the day";
            }
            catch { }
            try
            {
                var world = PromptFiles.LoadGlobalPrompt();
                var guidance = ApplyTokens(_config.RoleplayGuidance, record.SpouseName);
                facts.WorldText = string.Join(" ", new[] { world, guidance }
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            }
            catch { }

            return facts;
        }

        // ------------------------------ the writing ------------------------------

        /// <summary>Both doors land here: the record is saved at once (the day exists even if no
        /// word is ever written of it), then one story call writes the account off-thread.</summary>
        private void BeginBetrothalChronicleFor(Hero npc, BetrothalRecord record)
        {
            try
            {
                if (npc == null || record == null) return;
                if (!BetrothalWritingOn) return;
                _betrothalLedger!.Save(record);
                NotifyWedding($"the chronicler takes up the pen for the day {record.PlayerName} and {record.SpouseName} were promised…");

                var facts = GatherBetrothalFacts(npc, record);
                BeginBetrothalStory(npc, record, facts);
            }
            catch (Exception ex) { ModLog.Error("beginning the betrothal chronicle", ex); }
        }

        private void BeginBetrothalStory(Hero npc, BetrothalRecord record, BetrothalText.Facts facts)
        {
            if (npc == null || !_betrothalsWriting.Add(npc.StringId)) return;
            _ = WriteBetrothalStoryAsync(npc, record, facts);
        }

        private async Task WriteBetrothalStoryAsync(Hero npc, BetrothalRecord record, BetrothalText.Facts facts)
        {
            using var _cost = UsageLedger.BeginInteraction("the betrothal chronicle", record.SpouseName);
            try
            {
                var raw = await _storyClient.CompleteAsync(new List<ChatMessage>
                {
                    ChatMessage.User(BetrothalText.BuildPrompt(facts)),
                }).ConfigureAwait(false);
                var account = Core.Weddings.WeddingText.CleanAccount(raw,
                    BetrothalGifts.Of(record.Gift).AccountCharBudget);
                if (!Core.Weddings.WeddingText.LooksLikeAnAccount(account))
                {
                    ModLog.Info("the betrothal chronicle: no usable account came back; the day waits, unwritten.");
                    MainThreadDispatcher.Enqueue(() => BetrothalStoryFailed(npc));
                    return;
                }
                record.Account = account;
                MainThreadDispatcher.Enqueue(() => FinishBetrothalChronicle(npc, record));
            }
            catch (Exception ex)
            {
                ModLog.Error("writing the betrothal chronicle", ex);
                MainThreadDispatcher.Enqueue(() => BetrothalStoryFailed(npc));
            }
            finally
            {
                MainThreadDispatcher.Enqueue(() => _betrothalsWriting.Remove(npc.StringId));
            }
        }

        private void BetrothalStoryFailed(Hero npc)
        {
            try
            {
                if (_betrothalFailureTold) return;
                _betrothalFailureTold = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"The tale of the day could not be set down just now — it will be attempted again shortly.",
                    SealGrey));
            }
            catch { }
        }

        // Game thread: the book, the readable log, her beat, and the day laid before the player.
        private void FinishBetrothalChronicle(Hero npc, BetrothalRecord record)
        {
            try
            {
                // The wedding chronicle's own campaign guards, verbatim in spirit: never write
                // this day into another campaign's book, and never from a ghost of a past session.
                if (_betrothalLedger == null || Campaign.Current == null) return;
                if (!ReferenceEquals(Current, this)) return;
                if (!string.Equals(_betrothalLedger.Folder, NpcPaths.BetrothalsFolder, StringComparison.OrdinalIgnoreCase))
                {
                    ModLog.Info("the betrothal chronicle: the campaign changed while it was written; the day is left to its own save.");
                    return;
                }
                _betrothalLedger.Save(record);
                _betrothalLedger.AppendToChronicle(BetrothalText.ChronicleEntry(record));

                // Her memory alone — a proposal has no witnesses. The wedding partial's parking
                // discipline carries it safely past an exchange in flight.
                WriteWeddingBeat(npc,
                    BetrothalText.SpouseBeat(record.PlayerName, record.PlaceName, record.Account),
                    OutreachMark.PlayerEngaged);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);

                NotifyWedding($"❦ The day {record.PlayerName} and {record.SpouseName} were promised is written — {record.SpouseName} will keep it.");
                ShowBetrothalKeepsake(record);
                ModLog.Info($"the betrothal chronicle: {record.Title()} written ({record.Account.Length} characters).");
            }
            catch (Exception ex) { ModLog.Error("finishing the betrothal chronicle", ex); }
        }

        // A day saved at the seal but never written gets its honest chances on later hours —
        // bounded, quiet while the service refuses, one at a time (the wedding's exact rails).
        private void RetryUnwrittenBetrothals()
        {
            try
            {
                if (!BetrothalWritingOn || _betrothalRetries >= MaxBetrothalRetries) return;
                if (LlmGate.AutonomyQuiet) return;
                if (_betrothalsWriting.Count > 0) return;

                foreach (var record in _betrothalLedger!.Records)
                {
                    if (!IsUnwritten(record)) continue;
                    var npc = FindAliveHero(record.SpouseId);
                    if (npc == null) continue;

                    _betrothalRetries++;
                    var facts = GatherBetrothalFacts(npc, record);
                    ModLog.Info($"the betrothal chronicle: attempting {record.Title()} again ({_betrothalRetries} of {MaxBetrothalRetries}).");
                    BeginBetrothalStory(npc, record, facts);
                    return;
                }
            }
            catch (Exception ex) { ModLog.Error("retrying the betrothal chronicle", ex); }
        }

        // ------------------------------ the keepsake ------------------------------

        private void ShowBetrothalKeepsake(BetrothalRecord record)
        {
            try
            {
                if (record == null) return;
                ShowScrollPopup(BetrothalKeepsakeTitle(record), BetrothalKeepsakeBody(record), pause: true);
            }
            catch (Exception ex) { ModLog.Error("laying the betrothal before the player", ex); }
        }

        private static string BetrothalKeepsakeTitle(BetrothalRecord record) =>
            $"{record.PlayerName} and {record.SpouseName} — the day you were promised";

        private static string BetrothalKeepsakeBody(BetrothalRecord record)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Core.Courtship.BetrothalText.FullAccount(record,
                Safe(() => CalradiaYears.Since(record.GameDay), -1.0)));
            sb.AppendLine();
            sb.AppendLine("— — —");
            sb.AppendLine("Kept in plain text, beside your other days:");
            sb.AppendLine(Safe(() => NpcPaths.BetrothalsFolder, "the campaign's _betrothals folder"));
            return sb.ToString().TrimEnd();
        }

        /// <summary>A written day of their own, for the pages and the recall gate. The day itself
        /// is READ IN THE "Between us" PAGE at every stage from the promise onward — it needs no
        /// door of its own, and giving it one would make the page's ✦ promise an act that is only
        /// a second look at what you already have open.</summary>
        private BetrothalRecord? WrittenBetrothalOf(Hero npc)
        {
            try
            {
                var record = _betrothalLedger?.OwnBetrothalOf(npc?.StringId ?? string.Empty);
                return record != null && !IsUnwritten(record) ? record : null;
            }
            catch { return null; }
        }
    }
}
