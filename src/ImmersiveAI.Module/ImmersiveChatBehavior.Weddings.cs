using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Weddings;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// THE WEDDING CHRONICLE (2026.08.09, Anton's ask — "черешката на черешката"): the day the mod
    /// has been walking toward since the courtship road was laid. When the player weds anyone, the
    /// chronicler writes the day in TWO PARTS, in the register of Scripture and in the tongue the
    /// two of them actually speak:
    ///
    /// • THE DAY — the public account. It lands in the memory of the one wed AND of every soul who
    ///   stood there, so the wedding is a thing the whole company remembers, not a private note.
    /// • THE NIGHT — written in the wedded soul's own first person, the Song of Songs' register, and
    ///   belonging to the two of them alone. No witness ever carries it, and the recall tool refuses
    ///   it to anyone but the spouse. That refusal is a rule, not a nicety.
    ///
    /// Both parts are recorded as SILENT BEATS (the battle-chronicle pattern), so they ride in her
    /// verbatim memory in full detail and are folded into her rolling summary, her own way, when
    /// time comes for it — while the record keeps them whole forever in _weddings and can be called
    /// back by name through recall_wedding ("разкажи ми за онзи ден").
    ///
    /// THE HOOK is <c>CampaignEvents.BeforeHeroesMarried</c>, and the moment matters (decompiled
    /// 2026.08.09): it fires from inside MarriageAction.ApplyInternal with the spouses already set
    /// but BEFORE the clan change — which, for a noble bride, immediately makes her a Fugitive with
    /// no settlement and no party. Every fact of the day (where, who stood there) is therefore
    /// captured INSIDE the handler, synchronously. Hooking the game's own event rather than our seal
    /// also means a wedding arranged through vanilla's barter is chronicled just the same.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private WeddingLedger? _weddingLedger;

        // Weddings whose story is being written RIGHT NOW (by spouse id) — a story job is long, and
        // neither the hourly retry nor a dev click may start a second one over it. Entered and left
        // on the game thread only.
        private readonly HashSet<string> _weddingsWriting = new HashSet<string>(StringComparer.Ordinal);

        // How many times an unwritten day may be attempted again in one session. A dying key must
        // not be hammered once an hour forever; a passing 429 gets three honest chances.
        private const int MaxWeddingRetries = 3;
        private int _weddingRetries;
        private bool _weddingFailureTold;

        // Wedding beats that could not be written the instant they were made because that soul's own
        // exchange was in flight (their turn holds a memory instance loaded before the beat and would
        // save right over it). SaveMemory folds them into ANY instance of theirs being written that
        // does not carry them yet — the _pendingBlessingFolds discipline, applied to the day itself.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<string>>
            _pendingWeddingBeats = new System.Collections.Concurrent.ConcurrentDictionary<string, List<string>>();

        // Called by SaveMemory for every write, beside FoldPendingBlessing.
        private static void FoldPendingWeddingBeats(Hero npc, NpcMemory memory)
        {
            try
            {
                if (npc == null || memory == null) return;
                if (!_pendingWeddingBeats.TryRemove(npc.StringId, out var beats) || beats == null) return;
                foreach (var beat in beats)
                {
                    if (string.IsNullOrWhiteSpace(beat)) continue;
                    if (memory.RecentTurns.Any(t => t != null && t.PlayerLine == beat)) continue;
                    AddSilentInnerBeat(memory, npc, beat);
                }
            }
            catch { /* the beat is a memory, not a load-bearing rail */ }
        }

        private bool WeddingsOn => _config.EnableWeddingChronicle && _weddingLedger != null;

        /// <summary>Whether this record still awaits its story (saved at the seal, never written).</summary>
        private static bool IsUnwritten(WeddingRecord? record) =>
            record != null && string.IsNullOrWhiteSpace(record.FeastAccount);

        private void LoadWeddingLedger()
        {
            try { _weddingLedger = WeddingLedger.LoadFrom(NpcPaths.WeddingsFolder); }
            catch { _weddingLedger = null; }
        }

        // ------------------------------ the hook ------------------------------

        // Fired from inside MarriageAction.ApplyInternal — ours, vanilla's barter, or another mod's.
        // Only the player's own weddings are chronicled; the world's other marriages are the world's.
        private void OnHeroesMarriedForChronicle(Hero hero1, Hero hero2, bool showNotification)
        {
            try
            {
                if (!WeddingsOn) return;
                var player = Hero.MainHero;
                if (player == null || hero1 == null || hero2 == null) return;
                if (hero1 != player && hero2 != player) return;

                var spouse = hero1 == player ? hero2 : hero1;
                if (spouse == null || spouse == player) return;
                if (_weddingsWriting.Contains(spouse.StringId)) return;
                // Guarded by CONTENT, not existence: only a day already WRITTEN stands down. A day
                // saved at the seal but never written is the very thing the retry exists for — and
                // at a real wedding there is no record at all yet, which is the case that must
                // above all be let through (a "!IsUnwritten(null)" here made the whole feature dead
                // code — the pre-ship sweep's catch, 2026.08.09).
                var already = _weddingLedger!.OwnWeddingOf(spouse.StringId);
                if (already != null && !IsUnwritten(already)) return;

                // Everything below reads live campaign state and MUST run here, synchronously:
                // one heartbeat later a noble bride has left her settlement and her party.
                var record = CaptureWedding(spouse);
                var facts = GatherWeddingFacts(spouse, record);

                _weddingLedger.Save(record);   // the day exists even if no word is ever written of it
                NotifyWedding($"the chronicler takes up the pen for the wedding of {player.Name} and {spouse.Name}…");

                BeginWeddingStory(spouse, record, facts);
            }
            catch (Exception ex) { ModLog.Error("beginning the wedding chronicle", ex); }
        }

        // ------------------------------ the facts of the day ------------------------------

        // The plain record: when, where, who stood there, and what road led here. Game thread only.
        private WeddingRecord CaptureWedding(Hero spouse)
        {
            var player = Hero.MainHero;
            var settlement = WeddingPlace();
            var memory = LoadMemory(spouse);
            double now = CampaignTime.Now.ToDays;
            var scale = TakePendingWeddingScale(spouse);

            var record = new WeddingRecord
            {
                Id = _weddingLedger!.NextId(now),
                GameDay = now,
                DateText = CalradiaDate(),
                PlaceName = Safe(() => settlement == null ? string.Empty : SettlementPhrase(settlement), string.Empty),
                CultureName = Safe(() => settlement?.Culture?.Name?.ToString() ?? string.Empty, string.Empty),
                SpouseId = spouse.StringId,
                SpouseName = Safe(() => spouse.Name?.ToString() ?? "Unknown", "Unknown"),
                SpouseIsFemale = Safe(() => spouse.IsFemale, true),
                SpouseStation = Safe(() => PersonaBuilder.Build(spouse, _config)?.RoleDescription ?? string.Empty, string.Empty),
                SpouseAge = Safe(() => (int)spouse.Age, 0),
                PlayerName = Safe(() => player?.Name?.ToString() ?? "the traveler", "the traveler"),
                PlayerIsFemale = Safe(() => player?.IsFemale ?? false, false),
                PlayerAge = Safe(() => (int)(player?.Age ?? 0f), 0),
                PlayerClanName = Safe(() => Clan.PlayerClan?.Name?.ToString() ?? string.Empty, string.Empty),
                CourtshipDays = memory.CourtshipStepDay < 0 ? -1 : Math.Max(0, now - memory.CourtshipStepDay),
                BetrothalDays = memory.BetrothedGameDay < 0 ? -1 : Math.Max(0, now - memory.BetrothedGameDay),
                BridePrice = memory.FamilyBlessingPrice,
                BlessedBy = memory.FamilyBlessingDay >= 0
                    ? Safe(() => spouse.Clan?.Leader?.Name?.ToString() ?? string.Empty, string.Empty)
                    : string.Empty,
                MisgivingsAnswered = memory.CourtshipMisgivings
                    .Where(m => m != null && m.Settled && !string.IsNullOrWhiteSpace(m.Text))
                    .Select(m => m.Text.Trim())
                    .Take(5).ToList(),
                Scale = scale,
                FeastCost = WeddingTiers.PriceOf(scale),
            };

            foreach (var witness in GatherWitnesses(spouse, settlement, scale))
                record.Witnesses.Add(witness);

            return record;
        }

        // The wedding the player PAID for, laid aside by the seal a heartbeat before
        // MarriageAction.Apply, since BeforeHeroesMarried fires inside it and there is no later
        // moment to hand it anything (2026.08.09). A wedding reached any other way — vanilla's own
        // barter, another mod — simply has no entry here and is chronicled as Unpaid, which means
        // "we know nothing of the purse", never "the cheapest one".
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, WeddingScale>
            _pendingWeddingScale = new System.Collections.Concurrent.ConcurrentDictionary<string, WeddingScale>();

        private static void SetPendingWeddingScale(Hero spouse, WeddingScale scale)
        {
            try
            {
                if (spouse == null) return;
                if (scale == WeddingScale.Unpaid) _pendingWeddingScale.TryRemove(spouse.StringId, out _);
                else _pendingWeddingScale[spouse.StringId] = scale;
            }
            catch { }
        }

        private static WeddingScale TakePendingWeddingScale(Hero spouse)
        {
            try
            {
                return spouse != null && _pendingWeddingScale.TryRemove(spouse.StringId, out var scale)
                    ? scale : WeddingScale.Unpaid;
            }
            catch { return WeddingScale.Unpaid; }
        }

        // Where the player stands at the moment of the vow. Null on the open road — a wedding under
        // the sky is a true wedding, and the chronicler is told so plainly.
        private static Settlement? WeddingPlace()
        {
            try
            {
                return Hero.MainHero?.CurrentSettlement
                    ?? MobileParty.MainParty?.CurrentSettlement
                    ?? Settlement.CurrentSettlement;
            }
            catch { return null; }
        }

        private static string SettlementPhrase(Settlement settlement)
        {
            var name = settlement?.Name?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            if (settlement!.IsTown) return "the town of " + name;
            if (settlement.IsCastle) return "the castle of " + name;
            if (settlement.IsVillage) return "the village of " + name;
            return name!;
        }

        // Who truly stood there. The gathering itself now lives in the Celebrations partial, shared
        // with the birth chronicle — the bride and the player are simply excluded, since neither is
        // a witness to their own wedding, and her house is handed over as the house this day joins.
        //
        // THE PURSE DECIDES HOW FAR THE INVITATION TRAVELS (2026.08.09, Anton's design): the coin
        // does not buy adjectives in a paragraph, it buys MEMORY. A plain wedding is witnessed by
        // whoever already stood there; an invited one by your own house and every soul you truly
        // have a story with, wherever in the world they are, AND BY NOBODY ELSE (2026.08.10 — a
        // guest list is a thing that excludes); grander still calls the lords of the country round
        // about, and grandest the great names of the realm.
        private List<WeddingWitness> GatherWitnesses(Hero spouse, Settlement? settlement,
            WeddingScale scale = WeddingScale.Unpaid)
        {
            var rules = WeddingTiers.Guests(scale);
            return GatherCelebrationWitnesses(settlement, rules,
                    excluded: new[] { spouse }, otherHouse: Safe(() => spouse?.Clan, null))
                .Select(hero => new WeddingWitness
                {
                    HeroId = hero.StringId,
                    Name = Safe(() => hero.Name?.ToString(), "someone") ?? "someone",
                    Detail = Safe(() => WitnessDetail(hero), string.Empty),
                })
                .ToList();
        }

        // Everything the chronicler is told, gathered on the game thread before any await.
        private WeddingText.Facts GatherWeddingFacts(Hero spouse, WeddingRecord record)
        {
            var facts = new WeddingText.Facts
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
                Witnesses = record.Witnesses
                    .Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                    .Select(w => string.IsNullOrWhiteSpace(w.Detail) ? w.Name : $"{w.Name}, {w.Detail}")
                    .ToList(),
            };

            // Both ages come off the RECORD, not off the live heroes: a wedding whose account had to
            // be retried, or one written from an older record, must still be the day it was.
            facts.SpouseAge = record.SpouseAge > 0 ? record.SpouseAge : Safe(() => (int)spouse.Age, 0);
            facts.PlayerAge = record.PlayerAge > 0 ? record.PlayerAge : Safe(() => (int)(Hero.MainHero?.Age ?? 0f), 0);
            try
            {
                var persona = PersonaBuilder.Build(spouse, _config);
                facts.SpouseTraits = persona?.PersonalityDescription ?? string.Empty;
            }
            catch { }
            try
            {
                facts.SpouseSelfText = PromptFiles.LoadNpcPrompt(
                    NpcPaths.CustomInstructionsFile(spouse), record.SpouseName);
            }
            catch { }
            try { facts.PlayerStanding = PlayerStandingPhrase(); } catch { }
            try { facts.SeasonPhrase = SeasonPhrase(); } catch { }
            try
            {
                var memory = LoadMemory(spouse);
                facts.SharedStory = (memory.Summary ?? string.Empty).Trim();
                facts.RecentWords = RecentSpokenWords(memory, record.PlayerName, record.SpouseName);
            }
            catch { }
            try { facts.RoadPhrase = RoadPhrase(record); } catch { }
            try
            {
                if (!string.IsNullOrWhiteSpace(record.BlessedBy))
                    facts.BlessingPhrase = record.BridePrice > 0
                        ? $"given by {record.BlessedBy}, the head of their house, for {record.BridePrice} denars"
                        : $"given by {record.BlessedBy}, the head of their house";
            }
            catch { }
            try { facts.ScaleNote = WeddingTiers.ChroniclerNote(record.Scale); } catch { }
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

        private static string PlayerStandingPhrase()
        {
            try
            {
                var clan = Clan.PlayerClan;
                var sb = new StringBuilder();
                if (clan != null && !string.IsNullOrWhiteSpace(clan.Name?.ToString()))
                    sb.Append("of the house of ").Append(clan.Name);
                var kingdom = clan?.Kingdom?.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(kingdom))
                    sb.Append(sb.Length > 0 ? ", sworn to " : "sworn to ").Append(kingdom);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        private static string SeasonPhrase()
        {
            try
            {
                var season = CampaignTime.Now.GetSeasonOfYear;
                var hour = CampaignTime.Now.CurrentHourInDay;
                string timeWord = hour < 5 ? "in the deep of the night"
                    : hour < 10 ? "in the morning"
                    : hour < 14 ? "at midday"
                    : hour < 18 ? "in the afternoon"
                    : hour < 21 ? "as the light went"
                    : "after dark";
                string seasonWord = season == CampaignTime.Seasons.Spring ? "in spring"
                    : season == CampaignTime.Seasons.Summer ? "in summer"
                    : season == CampaignTime.Seasons.Autumn ? "in autumn" : "in winter";
                return seasonWord + ", " + timeWord;
            }
            catch { return string.Empty; }
        }

        private static string RoadPhrase(WeddingRecord record)
        {
            var parts = new List<string>();
            if (record.CourtshipDays >= 0)
                parts.Add(record.CourtshipDays < 1
                    ? "their hearts moved toward this only in the last day"
                    : $"the last step of their courtship was taken some {(int)Math.Round(record.CourtshipDays)} days ago");
            if (record.BetrothalDays >= 0)
                parts.Add(record.BetrothalDays < 1
                    ? "they were betrothed this very day"
                    : $"they had been betrothed {(int)Math.Round(record.BetrothalDays)} days");
            return string.Join("; ", parts);
        }

        // The last words that truly passed between them — the chronicler's only evidence of their
        // tongue and of how these two speak to one another. Inner beats are skipped: a chronicle
        // written in the voice of her private thoughts would read as eavesdropping, and they carry
        // no dialogue anyway.
        private static string RecentSpokenWords(NpcMemory memory, string playerName, string spouseName)
        {
            try
            {
                var lines = new List<string>();
                for (int i = memory.RecentTurns.Count - 1; i >= 0 && lines.Count < 10; i--)
                {
                    var turn = memory.RecentTurns[i];
                    if (turn == null || turn.IsInnerThought || turn.IsFromAngel) continue;
                    if (!string.IsNullOrWhiteSpace(turn.NpcLine))
                        lines.Insert(0, $"{spouseName}: {Squeeze(turn.NpcLine, 300)}");
                    if (!string.IsNullOrWhiteSpace(turn.PlayerLine))
                        lines.Insert(0, $"{playerName}: {Squeeze(turn.PlayerLine, 300)}");
                }
                return string.Join("\n", lines);
            }
            catch { return string.Empty; }
        }

        // ------------------------------ the writing ------------------------------

        // Two calls, deliberately: the day and the night are different registers, and two shorter
        // answers sit far more safely inside the clients' 90-second wall than one long one. Both
        // ride the story client's roomy budget — a 400-token spoken cap would sever a wedding in
        // mid-sentence, and in Bulgarian at ~1.6x the tokens it would sever it twice as soon.
        // The one door into the writing: marks the soul as being written for, so no second job (an
        // hourly retry, a dev click) can pile onto the same day.
        private void BeginWeddingStory(Hero spouse, WeddingRecord record, WeddingText.Facts facts)
        {
            if (spouse == null || !_weddingsWriting.Add(spouse.StringId)) return;
            _ = WriteWeddingStoryAsync(spouse, record, facts);
        }

        private async Task WriteWeddingStoryAsync(Hero spouse, WeddingRecord record, WeddingText.Facts facts)
        {
            using var _cost = UsageLedger.BeginInteraction("the wedding chronicle", record.SpouseName);
            try
            {
                var dayRaw = await _storyClient.CompleteAsync(new List<ChatMessage>
                {
                    ChatMessage.User(WeddingText.BuildFeastPrompt(facts)),
                }).ConfigureAwait(false);
                var day = WeddingText.CleanAccount(dayRaw);
                if (!WeddingText.LooksLikeAnAccount(day))
                {
                    ModLog.Info("the wedding chronicle: no usable account of the day came back; the day waits, unwritten.");
                    MainThreadDispatcher.Enqueue(() => WeddingStoryFailed(spouse));
                    return;
                }
                record.FeastAccount = day;

                // The night is written knowing the day, so the two parts hold together.
                string night = string.Empty;
                try
                {
                    var nightRaw = await _storyClient.CompleteAsync(new List<ChatMessage>
                    {
                        ChatMessage.User(WeddingText.BuildNightPrompt(facts, day)),
                    }).ConfigureAwait(false);
                    night = WeddingText.CleanAccount(nightRaw);
                    if (!WeddingText.LooksLikeAnAccount(night)) night = string.Empty;
                }
                catch (Exception ex)
                {
                    // The day is written and worth keeping even when the night never arrives.
                    ModLog.Error("writing the wedding night", ex);
                }
                record.NightAccount = night;

                MainThreadDispatcher.Enqueue(() => FinishWeddingChronicle(spouse, record));
            }
            catch (Exception ex)
            {
                ModLog.Error("writing the wedding chronicle", ex);
                MainThreadDispatcher.Enqueue(() => WeddingStoryFailed(spouse));
            }
            finally
            {
                MainThreadDispatcher.Enqueue(() => _weddingsWriting.Remove(spouse.StringId));
            }
        }

        // A day that could not be written yet: say so ONCE (a silent nothing where the wedding
        // story should be is the worst possible outcome), and leave the record unwritten so the
        // hourly retry may try again while the world settles.
        private void WeddingStoryFailed(Hero spouse)
        {
            try
            {
                if (_weddingFailureTold) return;
                _weddingFailureTold = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"The tale of your wedding with {spouse?.Name} could not be set down just now — it will be attempted again shortly.",
                    SealGrey));
            }
            catch { }
        }

        // Game thread: the book, the readable log, the beats, and the one glad notice.
        private void FinishWeddingChronicle(Hero spouse, WeddingRecord record)
        {
            try
            {
                // The world may have moved on while the chronicler wrote: a campaign closed, or
                // another loaded. Writing this day into THAT campaign's folder would be a lie —
                // the ledger's own folder is the honest test of which campaign it belongs to, and
                // `Current != this` catches a RELOAD of the same campaign, where the folder still
                // matches but this behavior instance is a ghost of the session before.
                if (_weddingLedger == null || Campaign.Current == null) return;
                if (!ReferenceEquals(Current, this)) return;
                if (!string.Equals(_weddingLedger.Folder, NpcPaths.WeddingsFolder, StringComparison.OrdinalIgnoreCase))
                {
                    ModLog.Info("the wedding chronicle: the campaign changed while it was written; the day is left to its own save.");
                    return;
                }
                _weddingLedger.Save(record);
                _weddingLedger.AppendToChronicle(WeddingText.ChronicleEntry(record));

                RecordWeddingBeats(spouse, record);

                var written = string.IsNullOrWhiteSpace(record.NightAccount)
                    ? "the day is written"
                    : "the day and the night are written";
                NotifyWedding($"❦ The wedding of {record.PlayerName} and {record.SpouseName}: {written}. "
                            + $"{record.SpouseName} will remember this day"
                            + (record.Witnesses.Count > 0 ? ", and so will those who stood there." : "."));

                // And the day is laid before the player to READ, with the world held still for it.
                ShowWeddingKeepsake(record);
                ModLog.Info($"the wedding chronicle: {record.Title()} written "
                          + $"({record.FeastAccount.Length} + {record.NightAccount.Length} characters, "
                          + $"{record.Witnesses.Count} witnesses).");
            }
            catch (Exception ex) { ModLog.Error("finishing the wedding chronicle", ex); }
        }

        // The day into every memory that lived it; the night into one only.
        private void RecordWeddingBeats(Hero spouse, WeddingRecord record)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(record.FeastAccount)) return;

                WriteWeddingBeat(spouse,
                    WeddingText.SpouseDayBeat(record.PlayerName, record.PlaceName, record.FeastAccount),
                    OutreachMark.PlayerEngaged);

                if (!string.IsNullOrWhiteSpace(record.NightAccount))
                    WriteWeddingBeat(spouse, WeddingText.NightBeat(record.NightAccount), OutreachMark.None);

                UI.TalkUI.OnThreadChanged(spouse, markUnread: false);

                foreach (var witness in record.Witnesses)
                {
                    if (witness == null || string.IsNullOrWhiteSpace(witness.HeroId)) continue;
                    var hero = FindAliveHero(witness.HeroId);
                    if (hero == null) continue;
                    // Witnessing a wedding is not the player engaging THEM — no outreach mark, the
                    // journey-beat rule (their waiting pride is neither wounded nor mended by it).
                    WriteWeddingBeat(hero,
                        WeddingText.WitnessDayBeat(record.PlayerName, record.SpouseName, record.PlaceName, record.FeastAccount),
                        OutreachMark.None);
                    UI.TalkUI.OnThreadChanged(hero, markUnread: false);
                }
            }
            catch (Exception ex) { ModLog.Error("recording the wedding beats", ex); }
        }

        // One beat, written safely: a soul whose own exchange is in flight holds a memory instance
        // loaded before this moment and would save straight over a beat written now — so theirs is
        // PARKED and folded in by SaveMemory when their turn lands (the blessing's discipline).
        //
        // BOTH kinds of exchange count, and the second one is the likely one at a wedding: the seal
        // happens inside a face-to-face talk, and a player who has just been wed usually says
        // something to their spouse before walking away — that turn holds its own memory instance
        // across the very seconds the chronicler is writing (pre-ship sweep, 2026.08.09).
        private bool IsExchangeInFlight(Hero hero)
        {
            try
            {
                if (hero == null) return false;
                if (IsQuickChatBusy(hero)) return true;
                return Hero.OneToOneConversationHero == hero || _currentNpc == hero;
            }
            catch { return false; }
        }

        private void WriteWeddingBeat(Hero hero, string beat, OutreachMark mark)
        {
            try
            {
                if (hero == null || string.IsNullOrWhiteSpace(beat)) return;
                if (IsExchangeInFlight(hero))
                {
                    _pendingWeddingBeats.AddOrUpdate(hero.StringId,
                        _ => new List<string> { beat },
                        (_, existing) => { existing.Add(beat); return existing; });
                    return;
                }
                AppendRecordedTurn(hero, beat, string.Empty, mark);
            }
            catch (Exception ex) { ModLog.Error("writing a wedding beat", ex); }
        }

        // A parked beat lands at that soul's next memory write — but a talk that simply ENDS writes
        // nothing, so the hour sweeps up whatever is still waiting. Without this a wedding beat
        // parked during the closing words of the wedding talk could wait forever.
        private void FlushParkedWeddingBeats()
        {
            try
            {
                if (_pendingWeddingBeats.IsEmpty) return;
                foreach (var heroId in _pendingWeddingBeats.Keys.ToList())
                {
                    var hero = FindAliveHero(heroId);
                    if (hero == null)
                    {
                        _pendingWeddingBeats.TryRemove(heroId, out _);
                        continue;
                    }
                    if (IsExchangeInFlight(hero)) continue;      // their own turn will fold it in
                    if (!_pendingWeddingBeats.TryRemove(heroId, out var beats) || beats == null) continue;
                    foreach (var beat in beats)
                        if (!string.IsNullOrWhiteSpace(beat)) AppendRecordedTurn(hero, beat, string.Empty);
                }
            }
            catch (Exception ex) { ModLog.Error("flushing the parked wedding beats", ex); }
        }

        // ------------------------------ the retry ------------------------------

        // A day saved at the seal but never written (a 429, a timeout, a refusal) is not lost: the
        // record keeps every fact of it, so the chronicler can be asked again on a later hour.
        // Bounded per session, quiet while the service is refusing calls, and never while another
        // wedding is being written.
        private void RetryUnwrittenWeddings()
        {
            try
            {
                if (!WeddingsOn || _weddingRetries >= MaxWeddingRetries) return;
                if (LlmGate.AutonomyQuiet) return;
                if (_weddingsWriting.Count > 0) return;

                foreach (var record in _weddingLedger!.Records)
                {
                    if (!IsUnwritten(record)) continue;
                    var spouse = FindAliveHero(record.SpouseId);
                    if (spouse == null) continue;

                    _weddingRetries++;
                    var facts = GatherWeddingFacts(spouse, record);
                    ModLog.Info($"the wedding chronicle: attempting {record.Title()} again ({_weddingRetries} of {MaxWeddingRetries}).");
                    BeginWeddingStory(spouse, record, facts);
                    return;   // one at a time; the next hour may take the next
                }
            }
            catch (Exception ex) { ModLog.Error("retrying the wedding chronicle", ex); }
        }

        // The road's own rose, for a day that belongs to the same story as the courtship's steps.
        private void NotifyWedding(string line)
        {
            try
            {
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage(line, RoadColor)));
            }
            catch { /* the notice is a nicety; the record is the thing */ }
        }

        // ------------------------------ the keepsake: the day, laid before the player ------------------------------

        /// <summary>
        /// The wedding read at leisure, with the world held still (Anton's ask, 2026.08.09 — a story
        /// this size deserves more than a line in the log). It opens once when the chronicler is
        /// done, and thereafter whenever the player wants it: the chat window's own button carries
        /// the same page for as long as the marriage lasts. The scene notification of vanilla's
        /// wedding cutscene draws above the inquiry layer, so if the cutscene is still running the
        /// popup simply waits beneath it and is there when it clears.
        /// </summary>
        private void ShowWeddingKeepsake(WeddingRecord record)
        {
            try
            {
                if (record == null) return;
                ShowScrollPopup(WeddingKeepsakeTitle(record), WeddingKeepsakeBody(record, opened: false), pause: true);
            }
            catch (Exception ex) { ModLog.Error("laying the wedding before the player", ex); }
        }

        private static string WeddingKeepsakeTitle(WeddingRecord record) =>
            $"{record.PlayerName} and {record.SpouseName} — your wedding day";

        // The page itself. `opened` is true when the player asked for it (the chat window's button),
        // false the first time it comes on its own — only the opening line differs.
        private static string WeddingKeepsakeBody(WeddingRecord record, bool opened)
        {
            var sb = new StringBuilder();
            sb.AppendLine(opened
                ? "(Your wedding day, kept whole. It is here whenever you want it.)"
                : "(This day is yours to keep — you can open it again at any time from the chat window, under their name.)");
            sb.AppendLine();

            var head = new StringBuilder();
            head.Append(string.IsNullOrWhiteSpace(record.DateText) ? "That day" : record.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(record.PlaceName)) head.Append(", in ").Append(record.PlaceName.Trim());
            sb.AppendLine(head.ToString());
            var years = Safe(() => WeddingText.TheirYearsThatDay(record, CalradiaYears.Since(record.GameDay)), string.Empty);
            if (!string.IsNullOrWhiteSpace(years)) sb.AppendLine(years);
            var standing = record.Witnesses?.Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name.Trim()).ToList() ?? new List<string>();
            sb.AppendLine(standing.Count > 0
                ? "Those who stood with you: " + string.Join(", ", standing) + "."
                : "No one stood with you but each other.");
            if (!string.IsNullOrWhiteSpace(record.BlessedBy))
                sb.AppendLine(record.BridePrice > 0
                    ? $"Blessed by {record.BlessedBy.Trim()}, for {record.BridePrice} denars."
                    : $"Blessed by {record.BlessedBy.Trim()}.");

            if (!string.IsNullOrWhiteSpace(record.FeastAccount))
            {
                sb.AppendLine();
                sb.AppendLine("THE DAY");
                sb.AppendLine(record.FeastAccount.Trim());
            }
            if (!string.IsNullOrWhiteSpace(record.NightAccount))
            {
                sb.AppendLine();
                sb.AppendLine("THE NIGHT — yours and theirs alone");
                sb.AppendLine(record.NightAccount.Trim());
            }

            sb.AppendLine();
            sb.AppendLine("— — —");
            sb.AppendLine("Kept on your own machine, in plain text, for as long as you keep the folder:");
            sb.AppendLine(Safe(() => NpcPaths.WeddingsFolder, "the campaign's _weddings folder"));
            sb.AppendLine("Copy it somewhere of your own if you would like to keep it past this playthrough.");
            return sb.ToString().TrimEnd();
        }

        /// <summary>The chat window's own page for this soul once you are wed: the wedding replaces
        /// the misgivings there, since the doubts are answered and the day is what remains. False
        /// when no written wedding of theirs exists.</summary>
        internal static bool TryGetWeddingView(Hero npc, out string buttonLabel, out string title, out string body) =>
            TryGetWeddingView(npc, out buttonLabel, out title, out body, out _);

        /// <param name="hint">The hover text, which MUST come from here rather than from the caller:
        /// the page is sometimes a wedding, sometimes a wedding and its children, and sometimes only
        /// children — and a hardcoded "your wedding day, kept whole" over a childless couple's
        /// children page is a small lie the player reads every time they hover (2026.08.10 review).</param>
        internal static bool TryGetWeddingView(Hero npc, out string buttonLabel, out string title,
            out string body, out string hint)
        {
            buttonLabel = title = body = hint = string.Empty;
            try
            {
                var self = Current;
                if (self == null || npc == null) return false;

                var record = self._config.EnableWeddingChronicle
                    ? self._weddingLedger?.OwnWeddingOf(npc.StringId) : null;
                bool hasWedding = record != null && !IsUnwritten(record);

                // The children born of this bond ride on the SAME page (2026.08.10): one door per
                // soul, holding the whole household — and it is also the only door a couple who
                // never had a wedding of ours have to their children's days.
                var children = Safe(() => ChildrenPageFor(npc), string.Empty);
                bool hasChildren = !string.IsNullOrWhiteSpace(children);
                if (!hasWedding && !hasChildren) return false;

                if (hasWedding)
                {
                    // The label stays SHORT and unchanged — it has to fit a button whose width is
                    // set in the prefab, and the children's own front door is the hearth window.
                    // Here they ride as an appendix to the day that began the household.
                    buttonLabel = "Our wedding day";
                    title = WeddingKeepsakeTitle(record!);
                    body = WeddingKeepsakeBody(record!, opened: true)
                         + (hasChildren ? Environment.NewLine + children : string.Empty);
                    hint = "Your wedding day, kept whole — the day itself, and the night that is yours and theirs alone. "
                         + "Opening it plays the wedding once more, and the account follows it."
                         + (hasChildren ? " Your children of this marriage are kept with it." : string.Empty);
                    return true;
                }

                buttonLabel = "Our children";
                title = $"{Safe(() => Hero.MainHero?.Name?.ToString(), "You")} and {npc.Name} — your children";
                body = children.TrimStart();
                hint = "The children you have of this bond, kept whole: the hour each of them came into the world, "
                     + "in their mother's own words, and the feast that welcomed them.";
                return true;
            }
            catch { return false; }
        }

        /// <summary>Opens that page from the chat window — but the DAY ITSELF comes first (Anton,
        /// 2026.08.09): the game's own wedding scene plays again, and the written account follows
        /// the moment the player clicks through it. A scene that cannot be played (an old save, a
        /// game that moved the API) simply falls through to the account, as before.</summary>
        internal static void ShowWeddingViewFor(Hero npc)
        {
            try
            {
                if (!TryGetWeddingView(npc, out _, out var title, out var body)) return;
                // The scene is a WEDDING scene: it is replayed only where there truly was one, or a
                // couple who merely have children would be shown a wedding they never had.
                // CONTENT, not existence — the wedding chronicle's own standing rule: a record
                // saved at the seal whose account never arrived is not a wedding to replay, and a
                // couple who merely have children never had one at all.
                var wedding = Safe(() => Current?._weddingLedger?.OwnWeddingOf(npc.StringId), null);
                if (wedding == null || IsUnwritten(wedding)
                    || !UI.WeddingSceneReplay.TryPlay(npc, wedding.GameDay,
                        () => ShowScrollPopup(title, body, pause: true),
                        wedding.SpouseAge, wedding.PlayerAge))
                    ShowScrollPopup(title, body, pause: true);
            }
            catch (Exception ex) { ModLog.Error("opening the wedding keepsake", ex); }
        }

        // ------------------------------ what the rest of the mod reads ------------------------------

        /// <summary>The wedding's hand rides only for a soul who truly stood at one of the player's
        /// weddings — their own, or one they witnessed (a lean tool list keeps tools used).</summary>
        private bool CanRecallWedding(Hero npc)
        {
            try
            {
                return _config.EnableWeddingChronicle
                    && _weddingLedger != null
                    && npc != null
                    // A day with no account written is nothing to recall — the tool must not ride
                    // in every reply only to answer with a bare header (review find, 2026.08.09).
                    && _weddingLedger.SharedWith(npc.StringId).Any(r => !IsUnwritten(r));
            }
            catch { return false; }
        }

        // ------------------------------ the test lever ------------------------------

        // Writes the chronicle again for the soul spoken with — the whole pipeline (both calls, the
        // JSON, weddings.txt, the beats, the cards, the recall tool) testable without a second
        // wedding. Only for a soul already wed to the player.
        internal void ForgeWeddingChronicleFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                if (!WeddingsOn)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The wedding chronicle is turned off in the mod options.", SealGrey));
                    return;
                }
                var existing = _weddingLedger!.OwnWeddingOf(npc.StringId);
                // NEVER a bare Spouse test: a polygamy mod archives earlier living wives into
                // ExSpouses, so the first wife would read as "not wed to you" (the ExSpouses truth,
                // and a review find here). A record of their own wedding is proof enough besides.
                bool wed = existing != null || Safe(() => Personas.FamilyBuilder.AreWed(Hero.MainHero, npc), false);
                if (!wed)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{npc.Name} is not wed to you — there is no wedding day to set down.", SealGrey));
                    return;
                }

                var record = CaptureWedding(npc);
                if (existing != null)
                {
                    // Rewrite the same day, not a second one — and keep the facts the true day was
                    // captured with, since the world has long since moved on from that hour.
                    record.Id = existing.Id;
                    record.GameDay = existing.GameDay;
                    record.DateText = existing.DateText;
                    if (!string.IsNullOrWhiteSpace(existing.PlaceName)) record.PlaceName = existing.PlaceName;
                    if (!string.IsNullOrWhiteSpace(existing.CultureName)) record.CultureName = existing.CultureName;
                    if (existing.Witnesses.Count > 0) record.Witnesses = existing.Witnesses;
                }
                var facts = GatherWeddingFacts(npc, record);

                _weddingLedger.Save(record);
                NotifyWedding($"the chronicler takes up the pen for the wedding of {record.PlayerName} and {record.SpouseName}…");
                BeginWeddingStory(npc, record, facts);
            }
            catch (Exception ex) { ModLog.Error("forging the wedding chronicle", ex); }
        }

        internal static void DevForgeWedding(Hero npc)
        {
            try { if (npc != null) Current?.ForgeWeddingChronicleFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: forging the wedding chronicle", ex); }
        }
    }
}
