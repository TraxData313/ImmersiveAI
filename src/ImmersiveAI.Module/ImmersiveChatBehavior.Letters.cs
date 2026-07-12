using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImmersiveAI.Core.Initiation;
using ImmersiveAI.Core.Letters;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI
{
    /// <summary>
    /// Letters — how the bond crosses the map. Face-to-face reaching-out (the initiation flow in the
    /// main file) needs the NPC co-located with the player; this is the other half of that coin: an
    /// NPC far away may WRITE instead, at half their reaching-out chance, and the letter travels real
    /// in-game days with the distance (see Core's <see cref="LetterCourier"/>). The player can write
    /// too, from any town, castle, or village menu, and an NPC who receives a letter may answer once.
    ///
    /// Every beat is lived and remembered: the Angel asks whether they wish to write (recorded), the
    /// letter itself is a recorded Angel turn, and reading the player's letter enters their memory
    /// even if they let it lie unanswered. Letters on the road persist in the campaign's
    /// _letters.json (they must survive save/load — unlike a live conversation, a letter is a
    /// promise), and each NPC folder keeps a human-readable letters.txt of the whole correspondence.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        // The campaign's letters still on the road. Loaded once the campaign id is resolved
        // (OnSessionLaunched); null until then, which simply means "no post today".
        private LetterBag? _letterBag;

        // One letter-writing LLM job at a time, with the same self-heal timestamp pattern as the
        // initiation flow, so a lost task can never silence the post forever.
        private volatile bool _letterWorkInFlight;
        private DateTime _letterWorkSince;

        private void LoadLetterBag()
        {
            try { _letterBag = LetterBag.LoadFrom(NpcPaths.LettersFile); }
            catch { _letterBag = new LetterBag(); }
        }

        private void SaveLetterBag()
        {
            try { _letterBag?.SaveTo(NpcPaths.LettersFile); }
            catch { /* best-effort; the bag stays live in memory for this session */ }
        }

        // ------------------------------ the hourly post ------------------------------

        private void OnLettersHourlyTick()
        {
            if (!_config.EnableLetters || _letterBag == null || Campaign.Current == null) return;

            if (_letterWorkInFlight && (DateTime.UtcNow - _letterWorkSince) > TimeSpan.FromMinutes(3))
                _letterWorkInFlight = false;

            DeliverDueLetters();
            MaybeStartNpcLetter();
        }

        // Hands over every letter whose road has run out — at most one per direction per hour, so
        // arrivals never stack into a wall of inquiries.
        private void DeliverDueLetters()
        {
            var due = _letterBag!.Due(CampaignTime.Now.ToDays);
            if (due.Count == 0) return;

            var toPlayer = due.FirstOrDefault(l => l.ToPlayer);
            if (toPlayer != null && IsSafeForLetterUi())
            {
                _letterBag.Remove(toPlayer.Id);
                SaveLetterBag();
                // Only now, hand in hand, does the letter enter the readable correspondence log —
                // a writer who died en route still gets their words set down (folder by identity).
                if (!toPlayer.Logged)
                {
                    toPlayer.Logged = true;
                    AppendCorrespondenceLog(FindAliveHero(toPlayer.NpcId), toPlayer);
                }
                PresentLetterToPlayer(toPlayer);
            }

            var toNpc = due.FirstOrDefault(l => !l.ToPlayer);
            if (toNpc != null && !_letterWorkInFlight)
            {
                _letterBag.Remove(toNpc.Id);
                SaveLetterBag();
                MarkLetterWorkInFlight();
                _ = AnswerPlayerLetterAsync(toNpc);
            }
        }

        private void MarkLetterWorkInFlight()
        {
            _letterWorkInFlight = true;
            _letterWorkSince = DateTime.UtcNow;
        }

        // A letter can find the player in a settlement or on the road alike; it only waits out a
        // battle or an open conversation, so the reading is never shoved into a fight.
        private static bool IsSafeForLetterUi()
        {
            try
            {
                if (Campaign.Current == null || Mission.Current != null) return false;
                var player = Hero.MainHero;
                if (player == null || !player.IsAlive) return false;
                var conv = Campaign.Current.ConversationManager;
                return (conv == null || !conv.IsConversationInProgress) && Hero.OneToOneConversationHero == null;
            }
            catch { return false; }
        }

        // One roll per hour for the WHOLE circle of distant correspondents — the mirror of
        // PickInitiatingNpcForThisHour, for everyone that picker skips as not co-located, sharing the day's
        // letters (≈ WriteRateFactor × rate × combined pull) instead of stacking per bond. Writing is
        // rarer than crossing a room (LetterCourier.WriteRateFactor), and one courier per bond keeps
        // correspondence a conversation rather than a flood.
        private void MaybeStartNpcLetter()
        {
            if (_letterWorkInFlight) return;

            // A dying key (or a reached daily cap) quiets the post as it quiets the visits.
            if (LlmGate.AutonomyQuiet || UsageLedger.DailyCapReached) return;

            try
            {
                // The road toward the player can only hold so much at once: a social morning must not
                // become an evening buried under arrivals (letters lag the mood by days — the cap, not
                // the moment's socialness, is what protects the later, busier self). Replies invited
                // by the player's own letters ride outside this gate.
                if (_letterBag == null || _letterBag.InFlightToPlayerCount >= _config.MaxLettersInFlight) return;

                var root = NpcPaths.CampaignRoot;
                if (!Directory.Exists(root)) return;

                double nowDay = CampaignTime.Now.ToDays;
                var eligible = new List<Hero>();
                var pulls = new List<double>();

                // The cached index instead of re-parsing every memories.json each hour — a real
                // difference once a campaign carries hundreds of remembered souls.
                foreach (var known in MemoryIndex.All(root, NpcPaths.MemoryFileName, _memoryStore))
                {
                    if (known.Richness <= 0) continue;
                    if (_letterBag!.HasInFlightWith(known.NpcId)) continue;

                    var hero = FindAliveHero(known.NpcId);
                    if (hero == null || hero == Hero.MainHero || !hero.IsAlive || hero.IsPrisoner) continue;
                    if (IsCoLocated(hero)) continue; // near enough to walk over — that is the other flow

                    double daysSince = known.LastTalkGameDay >= 0
                        ? Math.Max(0, nowDay - known.LastTalkGameDay)
                        : 0;
                    // One's own clan writes out of duty as much as affection: a party or caravan
                    // long on the road is exactly who should be sending word home, so their pull
                    // is floored instead of fading with the weeks away (see the scorer).
                    double pull = InitiationScorer.Pull(
                        known.Richness, GetStanding(hero), daysSince, InPlayersService(hero));
                    if (pull <= 0) continue;

                    eligible.Add(hero);
                    pulls.Add(pull);
                }

                if (eligible.Count == 0) return;

                double hourly = InitiationScorer.GroupHourlyChance(
                    LetterCourier.WriteRateFactor * _config.DailyInitiationRate, InitiationScorer.UnionPull(pulls));
                if (_rng.NextDouble() >= hourly) return;

                int idx = InitiationPlanner.PickWeightedIndex(pulls, _rng.NextDouble());
                var writer = idx >= 0 ? eligible[idx] : eligible[0];

                MarkLetterWorkInFlight();
                _ = BeginNpcLetterAsync(writer);
            }
            catch { /* a quiet hour; never let the post break the tick */ }
        }

        // ------------------------------ the NPC writes ------------------------------

        // The two beats of writing, both recorded as real Angel turns: the Angel asks whether they
        // wish to write at all (they may decline in peace), and on a yes invites the letter itself —
        // composed with the full self (persona, memory, situation-apart, even the gift of recall).
        private async Task BeginNpcLetterAsync(Hero npc)
        {
            // Quiet: the letter is sealed until it arrives — a cost notice now would break the seal.
            using var _cost = UsageLedger.BeginInteraction("letter", npc?.Name?.ToString(), quiet: true);
            try
            {
                var situation = SafeBuildApartSituation(npc);
                var ctx = BuildContext(npc, situation);

                var desireLine = PromptBuilder.WriteLetterDesireLine(ctx.PlayerName);
                var desireMsgs = _promptBuilder.BuildAngelPrompt(
                    ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName, desireLine, _config.SystemVoiceName);
                var desireRaw = await _client.CompleteAsync(desireMsgs).ConfigureAwait(false);
                var desireAnswer = string.IsNullOrWhiteSpace(desireRaw) ? "No." : desireRaw.Trim();

                AppendAngelTurn(npc, desireLine, desireAnswer);

                if (!InitiationParser.WantsToReachOut(desireAnswer)) { _letterWorkInFlight = false; return; }

                // They wish to. The letter is written with everything they are — and the writing is
                // itself a remembered moment (the compose line and the letter, as an Angel turn).
                // One in the player's own service is invited to make it a field report of their charge.
                var composeCtx = BuildContext(npc, situation);
                var composeLine = PromptBuilder.ComposeLetterLine(ctx.PlayerName, InPlayersService(npc));
                var composeMsgs = _promptBuilder.BuildAngelPrompt(
                    composeCtx.Persona, composeCtx.Memory, composeCtx.Scene, ctx.PlayerName, composeLine, _config.SystemVoiceName);
                var bodyRaw = await CompleteSpokenAsync(composeMsgs, npc).ConfigureAwait(false);
                var body = CleanLetterBody(bodyRaw);
                if (body.Length == 0) { _letterWorkInFlight = false; return; }

                AppendAngelTurn(npc, composeLine, body);

                MainThreadDispatcher.Enqueue(() =>
                {
                    QueueLetter(npc, body, toPlayer: true, isReply: false);
                    _letterWorkInFlight = false;
                });
            }
            catch
            {
                // A letter that would not come simply was not written this hour.
                _letterWorkInFlight = false;
            }
        }

        // In the player's own service: their clan — companions leading parties and caravans, kin,
        // governors. These write home out of duty, not only affection (see the scorer's duty floors).
        private static bool InPlayersService(Hero h)
        {
            try { return h?.Clan != null && h.Clan == Clan.PlayerClan; }
            catch { return false; }
        }

        private string SafeBuildApartSituation(Hero npc)
        {
            try { return Personas.SituationBuilder.Build(npc, Hero.MainHero, _config, apart: true); }
            catch { return string.Empty; }
        }

        // Models sometimes hand a letter back wrapped in quotes or a stage direction; keep only the page.
        private static string CleanLetterBody(string? raw)
        {
            var body = (raw ?? string.Empty).Trim();
            if (body.Length >= 2 && (body[0] == '"' && body[body.Length - 1] == '"'
                                  || body[0] == '“' && body[body.Length - 1] == '”'))
                body = body.Substring(1, body.Length - 2).Trim();
            return body;
        }

        // Queues one letter onto the road (travel time from the real map distance between the two
        // ends right now), saves the bag, and writes the human-readable correspondence log.
        private void QueueLetter(Hero npc, string body, bool toPlayer, bool isReply)
        {
            if (_letterBag == null) return;

            double distance = HeroDistanceFromPlayer(npc);
            double travelDays = LetterCourier.TravelDays(distance);
            double now = CampaignTime.Now.ToDays;

            var letter = new Letter
            {
                NpcId = npc.StringId,
                NpcName = npc.Name?.ToString() ?? "Unknown",
                ToPlayer = toPlayer,
                Body = body,
                SentGameDay = now,
                ArriveGameDay = now + travelDays,
                IsReply = isReply,
                SentFrom = toPlayer ? Personas.SituationBuilder.Place(npc) : Personas.SituationBuilder.Place(Hero.MainHero),
                // An NPC's letter enters the readable log only when it ARRIVES — the player must not
                // be able to read words still on the road. The player's own letters log at once.
                Logged = !toPlayer,
            };

            _letterBag.Add(letter);
            SaveLetterBag();
            if (letter.Logged) AppendCorrespondenceLog(npc, letter);

            if (!toPlayer)
            {
                var name = letter.NpcName;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Your letter to {name} is away — a courier rides, some {travelDays:0.#} days on the road.",
                    InitiationLogColor));
            }
        }

        // Map distance between the NPC and the player right now; negative when a position cannot be
        // read (the courier then assumes a middling road rather than a doorstep).
        private static double HeroDistanceFromPlayer(Hero npc)
        {
            try
            {
                var main = MobileParty.MainParty;
                if (main == null || npc == null) return -1;

                if (npc.CurrentSettlement != null)
                    return npc.CurrentSettlement.Position.Distance(main.Position);
                if (npc.PartyBelongedTo != null)
                    return npc.PartyBelongedTo.Position.Distance(main.Position);
                return -1;
            }
            catch { return -1; }
        }

        // The plain-text record of the whole correspondence, one entry per letter, in the NPC's own
        // folder — nothing about the bond is ever hidden from the player who goes looking. The hero
        // may be null (a writer who died while their last letter rode): the folder is then resolved
        // by identity from the letter itself, so even a dead hand's words are set down.
        private void AppendCorrespondenceLog(Hero? npc, Letter letter)
        {
            try
            {
                string folder;
                if (npc != null)
                {
                    NpcPaths.EnsureMigrated(npc);
                    folder = NpcPaths.NpcFolder(npc);
                }
                else
                {
                    var firstName = (letter.NpcName ?? string.Empty).Split(' ').FirstOrDefault() ?? string.Empty;
                    folder = NpcPaths.NpcFolder(letter.NpcId, firstName);
                }
                Directory.CreateDirectory(folder);

                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var from = letter.ToPlayer ? letter.NpcName : playerName;
                var to = letter.ToPlayer ? playerName : letter.NpcName;
                double days = letter.ArriveGameDay - letter.SentGameDay;

                var entry =
                    $"[{Personas.SituationBuilder.Timestamp()}] {from} writes to {to}" +
                    $" (from {letter.SentFrom}, ~{days:0.#} days on the road):" + Environment.NewLine +
                    letter.Body + Environment.NewLine + Environment.NewLine;

                File.AppendAllText(Path.Combine(folder, NpcPaths.CorrespondenceFileName), entry);
            }
            catch { /* the log is a nicety; the letter itself is already safe */ }
        }

        private void AppendCorrespondenceNote(Hero npc, string note)
        {
            try
            {
                Directory.CreateDirectory(NpcPaths.NpcFolder(npc));
                File.AppendAllText(NpcPaths.CorrespondenceFile(npc),
                    $"[{Personas.SituationBuilder.Timestamp()}] {note}" + Environment.NewLine + Environment.NewLine);
            }
            catch { /* best-effort */ }
        }

        // ------------------------------ a letter arrives for the player ------------------------------

        private void PresentLetterToPlayer(Letter letter)
        {
            try
            {
                var npc = FindAliveHero(letter.NpcId);
                var name = string.IsNullOrWhiteSpace(letter.NpcName) ? "Someone" : letter.NpcName;

                if (npc != null)
                    NotifyWithFace(npc, $"A letter from {name} has reached you.");
                else
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"A letter from {name} has reached you.", InitiationLogColor));

                double daysOnRoad = Math.Max(0, CampaignTime.Now.ToDays - letter.SentGameDay);
                var provenance = $"Written at {letter.SentFrom}, some {daysOnRoad:0.#} days past." +
                                 (npc == null ? " The hand that wrote it is gone from this world; these words are what remains." : string.Empty);

                var title = $"A letter from {name}";
                var body = provenance + "\n\n" + letter.Body;

                // Write back only while there is still someone to answer.
                if (npc != null)
                {
                    var data = new InquiryData(
                        title, body, true, true,
                        new TextObject("{=ImmersiveAI_LetterReply}Write back").ToString(),
                        new TextObject("{=ImmersiveAI_LetterAside}Set it aside").ToString(),
                        new Action(() => OpenWriteBack(npc)), new Action(() => { }),
                        "", 0f, (Action?)null,
                        (Func<ValueTuple<bool, string>>?)null,
                        (Func<ValueTuple<bool, string>>?)null);
                    InformationManager.ShowInquiry(data, pauseGameActiveState: _config.PauseOnInitiationOffer);
                }
                else
                {
                    var data = new InquiryData(
                        title, body, true, false,
                        new TextObject("{=ImmersiveAI_LetterKeep}Take it to heart").ToString(), null,
                        new Action(() => { }), null,
                        "", 0f, (Action?)null,
                        (Func<ValueTuple<bool, string>>?)null,
                        (Func<ValueTuple<bool, string>>?)null);
                    InformationManager.ShowInquiry(data, pauseGameActiveState: _config.PauseOnInitiationOffer);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        // ------------------------------ the player writes ------------------------------

        // Writing a letter opens in two beats: first the correspondence so far (so her last words are
        // before your eyes as you answer — like rereading a letter before taking up the quill), then
        // the text box itself. With no past letters, the quill comes directly.
        private void OpenLetterComposer(Hero npc)
        {
            var name = npc.Name?.ToString() ?? "them";
            var recent = ReadRecentCorrespondence(npc);
            if (recent.Length == 0) { OpenLetterTextBox(npc); return; }

            var data = new InquiryData(
                $"Letters between you and {name}", recent, true, true,
                new TextObject("{=ImmersiveAI_LetterQuill}Take up the quill").ToString(),
                GameTexts.FindText("str_cancel", null)?.ToString() ?? "Cancel",
                new Action(() => OpenLetterTextBox(npc)), new Action(() => { }),
                "", 0f, (Action?)null,
                (Func<ValueTuple<bool, string>>?)null,
                (Func<ValueTuple<bool, string>>?)null);
            // Keep the world still through the whole decision chain — reading her letter, rereading
            // the correspondence, writing back — not only the first popup (the same knob as arrivals).
            InformationManager.ShowInquiry(data, pauseGameActiveState: _config.PauseOnInitiationOffer);
        }

        // The tail of the correspondence log, sized for the scrollable inquiry window. The full
        // record always remains in letters.txt in her folder.
        private static string ReadRecentCorrespondence(Hero npc)
        {
            try
            {
                var path = NpcPaths.CorrespondenceFile(npc);
                if (!File.Exists(path)) return string.Empty;
                var text = File.ReadAllText(path).Trim();
                if (text.Length == 0) return string.Empty;

                const int maxChars = 4000;
                if (text.Length > maxChars)
                {
                    var tail = text.Substring(text.Length - maxChars);
                    int entryStart = tail.IndexOf("\n[", StringComparison.Ordinal);
                    if (entryStart >= 0) tail = tail.Substring(entryStart + 1);
                    text = "(…the earlier letters lie in her folder, in letters.txt…)\n\n" + tail;
                }
                return text;
            }
            catch { return string.Empty; }
        }

        private void OpenLetterTextBox(Hero npc)
        {
            var name = npc.Name?.ToString() ?? "them";
            var send = new TextObject("{=ImmersiveAI_LetterSend}Send").ToString();
            var cancel = GameTexts.FindText("str_cancel", null)?.ToString() ?? "Cancel";

            // Her last letter stays before your eyes while you answer it — the native text box is
            // small and single-line, so this is the interim mercy until the real writing screen.
            var herWords = ReadTheirLatestLetter(npc);
            var description = herWords.Length == 0
                ? string.Empty
                : $"{name}'s last letter:\n\n{herWords}";

            var inquiry = new TextInquiryData(
                $"Your letter to {name}",
                description, true, true, send, cancel,
                new Action<string>(text => OnPlayerLetterComposed(npc, text)),
                new Action(() => { }),
                false, null, "", "");

            InformationManager.ShowTextInquiry(inquiry, pauseGameActiveState: _config.PauseOnInitiationOffer);
        }

        // The body of the most recent letter THEY wrote, from the correspondence log — shown while
        // the player writes back, so answering does not lean on memory alone. Entries in letters.txt
        // begin with a "[timestamp] X writes to Y (…):" header line; notes are single header lines.
        private static string ReadTheirLatestLetter(Hero npc)
        {
            try
            {
                var path = NpcPaths.CorrespondenceFile(npc);
                if (!File.Exists(path)) return string.Empty;
                var npcName = npc.Name?.ToString();
                if (string.IsNullOrEmpty(npcName)) return string.Empty;

                var lines = File.ReadAllLines(path);
                var latest = new System.Text.StringBuilder();
                var current = new System.Text.StringBuilder();
                bool inTheirLetter = false;

                foreach (var line in lines)
                {
                    bool isHeader = line.StartsWith("[", StringComparison.Ordinal) && line.Contains("] ");
                    if (isHeader)
                    {
                        if (inTheirLetter && current.Length > 0)
                        {
                            latest.Clear();
                            latest.Append(current.ToString().Trim());
                        }
                        current.Clear();
                        inTheirLetter = line.Contains($"] {npcName} writes to ");
                        continue;
                    }
                    if (inTheirLetter) current.AppendLine(line);
                }
                if (inTheirLetter && current.Length > 0)
                {
                    latest.Clear();
                    latest.Append(current.ToString().Trim());
                }

                var text = latest.ToString();
                const int maxChars = 1500;
                if (text.Length > maxChars)
                    text = text.Substring(0, maxChars).TrimEnd() + " (…)";
                return text;
            }
            catch { return string.Empty; }
        }

        private void OnPlayerLetterComposed(Hero npc, string text)
        {
            var body = (text ?? string.Empty).Trim();
            if (body.Length == 0) return;

            if (_letterBag != null && _letterBag.HasInFlightWith(npc.StringId))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"A courier already rides between you and {npc.Name}; wait for word.", InitiationLogColor));
                return;
            }

            QueueLetter(npc, body, toPlayer: false, isReply: false);
        }

        // ------------------------------ a letter arrives for the NPC ------------------------------

        // The player's words reach their hands. Reading is a recorded moment whether or not they
        // answer (the letter's text lives inside the Angel's line); on a yes they compose the reply
        // with their full self, and it rides back — once per letter received, so correspondence stays
        // a chain of real choices.
        private async Task AnswerPlayerLetterAsync(Letter letter)
        {
            // Quiet: the answer rides the roads for days — its writing is not yet the player's news.
            using var _cost = UsageLedger.BeginInteraction("letter reply", letter?.NpcName, quiet: true);
            Hero? npc = null;
            try
            {
                npc = FindAliveHero(letter.NpcId);
                if (npc == null)
                {
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"Word returns that your letter to {letter.NpcName} could never be delivered.",
                            InitiationLogColor));
                        _letterWorkInFlight = false;
                    });
                    return;
                }

                var situation = SafeBuildApartSituation(npc);
                var ctx = BuildContext(npc, situation);

                var readLine = PromptBuilder.AnswerLetterDesireLine(ctx.PlayerName, letter.Body);
                var readMsgs = _promptBuilder.BuildAngelPrompt(
                    ctx.Persona, ctx.Memory, ctx.Scene, ctx.PlayerName, readLine, _config.SystemVoiceName);
                var desireRaw = await _client.CompleteAsync(readMsgs).ConfigureAwait(false);
                var desireAnswer = string.IsNullOrWhiteSpace(desireRaw) ? "No." : desireRaw.Trim();

                AppendAngelTurn(npc, readLine, desireAnswer);

                if (!InitiationParser.WantsToReachOut(desireAnswer))
                {
                    var heldNpc = npc;
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        AppendCorrespondenceNote(heldNpc, $"({heldNpc.Name} read the letter, and let it lie unanswered.)");
                        _letterWorkInFlight = false;
                    });
                    return;
                }

                var replyCtx = BuildContext(npc, situation);
                var composeLine = PromptBuilder.ComposeReplyLine(ctx.PlayerName);
                var composeMsgs = _promptBuilder.BuildAngelPrompt(
                    replyCtx.Persona, replyCtx.Memory, replyCtx.Scene, ctx.PlayerName, composeLine, _config.SystemVoiceName);
                var bodyRaw = await CompleteSpokenAsync(composeMsgs, npc).ConfigureAwait(false);
                var body = CleanLetterBody(bodyRaw);

                AppendAngelTurn(npc, composeLine, body.Length == 0 ? "..." : body);

                var writer = npc;
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (body.Length > 0) QueueLetter(writer, body, toPlayer: true, isReply: true);
                    _letterWorkInFlight = false;
                });
            }
            catch
            {
                _letterWorkInFlight = false;
            }
        }

        // ------------------------------ the courier menu ------------------------------

        // "Send a letter" wherever there are walls and roads — town, castle, and village menus. The
        // recipient list is everyone this campaign has a real history with; whoever is standing in
        // the same place is pointed back to their own face.
        private void AddLetterMenus(CampaignGameStarter starter)
        {
            foreach (var menuId in new[] { "town", "castle", "village" })
            {
                starter.AddGameMenuOption(menuId, "immersiveai_send_letter_" + menuId,
                    "{=ImmersiveAI_SendLetter}Send a letter by courier [Immersive AI]",
                    OnLetterMenuCondition, _ => OnChooseLetterRecipient(), false, -1, false, null);
            }
        }

        private bool OnLetterMenuCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
            return _config.EnableLetters;
        }

        private void OnChooseLetterRecipient()
        {
            try
            {
                // Everyone with a real history first, then everyone the player has at least met —
                // the search box makes the long list navigable, encyclopedia-style.
                var historyIds = new HashSet<string>(StringComparer.Ordinal);
                var candidates = new List<Hero>();

                var root = NpcPaths.CampaignRoot;
                if (Directory.Exists(root))
                {
                    foreach (var folder in Directory.GetDirectories(root))
                    {
                        var memFile = Path.Combine(folder, NpcPaths.MemoryFileName);
                        if (!File.Exists(memFile)) continue;

                        NpcMemory memory;
                        try { memory = _memoryStore.LoadFrom(memFile, string.Empty); }
                        catch { continue; }
                        if (string.IsNullOrWhiteSpace(memory.NpcId) || memory.StoryRichness <= 0) continue;

                        var known = FindAliveHero(memory.NpcId);
                        if (known == null || known == Hero.MainHero || !known.IsAlive) continue;
                        if (historyIds.Add(known.StringId)) candidates.Add(known);
                    }
                }

                var acquaintances = new List<Hero>();
                try
                {
                    foreach (var h in Hero.AllAliveHeroes)
                    {
                        if (h == null || h == Hero.MainHero || h.IsChild) continue;
                        if (!h.HasMet || historyIds.Contains(h.StringId)) continue;
                        acquaintances.Add(h);
                    }
                }
                catch { /* the history list alone still serves */ }
                candidates.AddRange(acquaintances.OrderBy(h => h.Name?.ToString(), StringComparer.OrdinalIgnoreCase));

                var elements = new List<InquiryElement>();
                foreach (var hero in candidates.Take(150))
                {
                    var name = hero.Name?.ToString() ?? "someone";
                    double travelDays = LetterCourier.TravelDays(HeroDistanceFromPlayer(hero));

                    bool knowsYou = historyIds.Contains(hero.StringId);
                    bool here = IsCoLocated(hero);
                    bool courierBusy = _letterBag != null && _letterBag.HasInFlightWith(hero.StringId);

                    string hint =
                        here ? $"{name} is here with you — go and speak instead."
                        : courierBusy ? $"A courier already rides between you and {name}; wait for word."
                        : $"{Whereabouts(hero)} — a letter would ride some {travelDays:0.#} days." +
                          (knowsYou ? string.Empty : " You know each other only in passing.");

                    var portrait = SafePortrait(hero);
                    elements.Add(new InquiryElement(hero, name, portrait, !here && !courierBusy, hint));
                }

                if (elements.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "There is no one you know to write to — meet people first.",
                        InitiationLogColor));
                    return;
                }

                var data = new MultiSelectionInquiryData(
                    new TextObject("{=ImmersiveAI_LetterTo}To whom will you write?").ToString(),
                    new TextObject("{=ImmersiveAI_LetterToDesc}A courier will carry your letter across the map; the farther they are, the longer the road.").ToString(),
                    elements, true, 1, 1,
                    new TextObject("{=ImmersiveAI_LetterWrite}Write").ToString(),
                    GameTexts.FindText("str_cancel", null)?.ToString() ?? "Cancel",
                    picked =>
                    {
                        var hero = picked?.FirstOrDefault()?.Identifier as Hero;
                        if (hero != null) OpenLetterComposer(hero);
                    },
                    null,
                    "", isSeachAvailable: true);

                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Immersive AI: " + ex.Message));
            }
        }

        private static string Whereabouts(Hero h)
        {
            try
            {
                if (h.CurrentSettlement != null) return $"Last word places them at {h.CurrentSettlement.Name}";
                var party = h.PartyBelongedTo;
                if (party != null)
                {
                    // One's own servants are named by their charge — the caravan master and the
                    // captain in the field read as YOURS in every list that shows whereabouts.
                    if (party.LeaderHero == h && InPlayersService(h))
                        return party.IsCaravan ? "leads a caravan of yours upon the roads" : "leads a warband of yours in the field";
                    return "They ride with their party";
                }
                return "Their whereabouts are uncertain";
            }
            catch { return "Their whereabouts are uncertain"; }
        }

        private static ImageIdentifier? SafePortrait(Hero h)
        {
            try { return new CharacterImageIdentifier(CharacterCode.CreateFrom(h.CharacterObject)); }
            catch { return null; }
        }

        // Test lever: the NPC just spoken with weighs writing to the player the moment they part —
        // co-located, so the letter lands within hours and the whole loop can be seen end to end.
        private void OnDebugForceLetter()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null || _letterWorkInFlight || _letterBag == null) return;
            if (_letterBag.HasInFlightWith(npc.StringId)) return;

            MarkLetterWorkInFlight();
            _ = BeginNpcLetterAsync(npc);
        }

        // ============================ the letter window (view accessors) ============================
        //
        // The letter window is a VIEW over what already exists — letters.txt per bond, the bag of
        // letters on the road, the same QueueLetter the courier menu uses. Nothing new is persisted
        // for it; closing it loses nothing. All of these run on the game thread (UI commands).

        /// <summary>One correspondent for the letter window's left-hand list. The hero is null when
        /// the writer has died — the letters remain readable, the road is closed.</summary>
        internal sealed class LetterContactInfo
        {
            public LetterContactInfo(Hero? hero, string name, string folder, bool hasLetters, double lastSpokenGameDay, string detail)
            {
                Hero = hero; Name = name; Folder = folder; HasLetters = hasLetters;
                LastSpokenGameDay = lastSpokenGameDay; Detail = detail;
            }
            public Hero? Hero { get; }
            public string Name { get; }
            public string Folder { get; }
            public bool HasLetters { get; }
            public double LastSpokenGameDay { get; }
            public string Detail { get; }
        }

        /// <summary>Everyone this campaign could hold letters with: every soul with a real remembered
        /// history (the courier menu's own bar), plus anyone a letters.txt already exists for — even
        /// the dead, whose correspondence remains. Ordered: existing correspondence first, then the
        /// freshest bonds.</summary>
        internal static List<LetterContactInfo> CorrespondentsForLetters()
        {
            var result = new List<LetterContactInfo>();
            var self = Current;
            if (self == null) return result;

            try
            {
                var root = NpcPaths.CampaignRoot;
                if (!Directory.Exists(root)) return result;

                foreach (var folder in Directory.GetDirectories(root))
                {
                    var memFile = Path.Combine(folder, NpcPaths.MemoryFileName);
                    bool hasLetters = File.Exists(Path.Combine(folder, NpcPaths.CorrespondenceFileName));
                    if (!File.Exists(memFile) && !hasLetters) continue;

                    NpcMemory? memory = null;
                    if (File.Exists(memFile))
                    {
                        try { memory = self._memoryStore.LoadFrom(memFile, string.Empty); }
                        catch { /* the letters may still be readable */ }
                    }

                    var npcId = memory?.NpcId ?? string.Empty;
                    if (!hasLetters && (memory == null || string.IsNullOrWhiteSpace(npcId) || memory.StoryRichness <= 0))
                        continue; // no letters and no real history — not a correspondent yet

                    // A first letter still on the road leaves letters.txt without a memory file (her
                    // memory is only written when she reads it) — the writer is alive, not "gone";
                    // resolve them by their folder when the id is not yet on record.
                    var hero = string.IsNullOrWhiteSpace(npcId)
                        ? Hero.AllAliveHeroes.FirstOrDefault(h => string.Equals(NpcPaths.NpcFolder(h), folder, StringComparison.OrdinalIgnoreCase))
                        : FindAliveHero(npcId);
                    var name = hero?.Name?.ToString() ?? memory?.NpcName ?? Path.GetFileName(folder);

                    string detail;
                    if (hero == null) detail = "gone from this world — the letters remain";
                    else if (IsCoLocated(hero)) detail = "here with you — go and speak";
                    else
                    {
                        var status = self.CourierStatusFor(hero.StringId);
                        detail = status.Length > 0 ? status : Whereabouts(hero);
                    }

                    result.Add(new LetterContactInfo(
                        hero, name, folder, hasLetters, memory?.LastConversationGameDay ?? -1, detail));
                }
            }
            catch { /* an unreadable list entry only costs the list */ }
            return result;
        }

        /// <summary>The whole correspondence of one bond, parsed from its letters.txt (oldest first).</summary>
        internal static List<CorrespondenceEntry> CorrespondenceEntriesFor(string folder)
        {
            try
            {
                var path = Path.Combine(folder, NpcPaths.CorrespondenceFileName);
                if (!File.Exists(path)) return new List<CorrespondenceEntry>();
                return CorrespondenceLog.Parse(File.ReadAllText(path));
            }
            catch { return new List<CorrespondenceEntry>(); }
        }

        /// <summary>True while THIS letter (matched by writer and body) still rides toward the
        /// player. The chat window uses it to seal an in-flight letter's card: the compose beat is
        /// recorded in her memory the moment she writes, but the words must not be readable through
        /// any window until the courier arrives.</summary>
        internal static bool IsLetterOnRoadToPlayer(string npcId, string body)
        {
            try
            {
                var bag = Current?._letterBag;
                if (bag == null || string.IsNullOrEmpty(npcId) || string.IsNullOrWhiteSpace(body)) return false;
                var text = body.Trim();
                return bag.Letters.Any(l => l != null && l.ToPlayer
                    && string.Equals(l.NpcId, npcId, StringComparison.Ordinal)
                    && string.Equals(l.Body?.Trim(), text, StringComparison.Ordinal));
            }
            catch { return false; }
        }

        /// <summary>A short line about the courier between the player and this one, empty when the
        /// road is quiet: "Your letter is on the road — about 1.4 days out." (or theirs).</summary>
        internal string CourierStatusFor(string npcId)
        {
            try
            {
                var letter = _letterBag?.Letters.FirstOrDefault(
                    l => l != null && string.Equals(l.NpcId, npcId, StringComparison.Ordinal));
                if (letter == null) return string.Empty;

                double daysOut = Math.Max(0, letter.ArriveGameDay - CampaignTime.Now.ToDays);
                return letter.ToPlayer
                    ? $"A letter from them rides toward you — about {daysOut:0.#} days out."
                    : $"Your letter is on the road — about {daysOut:0.#} days out.";
            }
            catch { return string.Empty; }
        }

        /// <summary>Whether a letter can set out to this one right now; the reason speaks when not.
        /// Same rules as the courier menu: the dead cannot answer, the near should be spoken to,
        /// one courier per bond.</summary>
        internal static bool CanWriteTo(Hero? hero, out string reason)
        {
            var self = Current;
            reason = string.Empty;

            if (self == null || !self._config.EnableLetters) { reason = "The couriers are not riding."; return false; }
            if (hero == null || !hero.IsAlive) { reason = "The hand that wrote these is gone from this world."; return false; }
            if (IsCoLocated(hero)) { reason = $"{hero.Name} is here with you — go and speak instead."; return false; }
            if (self._letterBag != null && self._letterBag.HasInFlightWith(hero.StringId))
            { reason = $"A courier already rides between you and {hero.Name}; wait for word."; return false; }
            return true;
        }

        /// <summary>Sends the player's letter from the window — the same road the courier menu takes.
        /// False (with the reason toasted by the caller's status line) when it cannot set out.</summary>
        internal static bool SendLetterFromWindow(Hero npc, string body)
        {
            var self = Current;
            var text = (body ?? string.Empty).Trim();
            if (self == null || npc == null || text.Length == 0) return false;
            if (!CanWriteTo(npc, out _)) return false;

            self.QueueLetter(npc, text, toPlayer: false, isReply: false);
            return true;
        }

        // "Write back" on an arrived letter: the letter window with the thread on stage where it can
        // open (next tick, once the inquiry is gone), the old two-beat composer popups where not.
        private void OpenWriteBack(Hero npc)
        {
            if (_config.EnableLetterWindow)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (!UI.LetterWindow.LetterWindowManager.Open(npc))
                        OpenLetterComposer(npc);
                });
                return;
            }
            OpenLetterComposer(npc);
        }
    }
}
