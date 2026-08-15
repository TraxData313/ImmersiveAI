using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImmersiveAI.Core.Births;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Weddings;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI
{
    /// <summary>
    /// THE BIRTH CHRONICLE (2026.08.10, Anton's ask — "истории, подобни на тази на сватбата, за
    /// раждане на детенце"). The wedding chronicle's own shape, turned on the next day of a life,
    /// and reusing its machinery down to the guest list.
    ///
    /// TWO PARTS, and here they are split IN TIME as well as in register:
    /// • THE HOUR — the mother's own first person, written the moment the child comes, because the
    ///   hour happened whether or not anyone feasted it. It belongs to the two PARENTS: it goes
    ///   into her memory, never a witness's, and the recall tool refuses it to anyone else. The
    ///   father is given the fact and his own presence or absence, never her private "I".
    /// • THE FEAST — the public account, written only if a feast is bought, and it may be bought
    ///   days later: a father away at war is asked when he finally rides in to see the child.
    ///
    /// THE HOOK is <c>CampaignEvents.OnGivenBirthEvent</c> (decompile-verified 2026.08.10 — and
    /// note the subscribe method is <c>AddNonSerializedListener</c>; <c>AddListener</c> does not
    /// exist on IMbEvent in this version). It fires ONCE per birth, after every newborn has been
    /// built whole by HeroCreator.DeliverOffSpring — so at our moment the children are fully
    /// formed, already named by the game (this version never asks the player for a name, verified
    /// by decompile, so our own popup collides with nothing) and already sitting in mother.Children.
    ///
    /// WHY EVERYTHING IS CAPTURED SYNCHRONOUSLY: vanilla rolls a 1.5% death-in-labour for the
    /// mother immediately AFTER this event. One heartbeat later she may be gone, and with her the
    /// place, the party and every fact of the day. The wedding's clan-change lesson, wearing a
    /// darker coat.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private BirthLedger? _birthLedger;

        // The colour of a cradle: warm, and its own, so a birth never reads as a wedding.
        private static readonly Color CradleGold = new Color(0.96f, 0.84f, 0.48f, 1f);

        // Records whose account is being written RIGHT NOW (by record id + part), so neither the
        // hourly retry nor a dev click can pile a second job onto the same words.
        private readonly HashSet<string> _birthsWriting = new HashSet<string>(StringComparer.Ordinal);

        // Beats that could not be written the instant they were made because that soul's own
        // exchange was in flight — the wedding's _pendingWeddingBeats discipline, exactly.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<string>>
            _pendingBirthBeats = new System.Collections.Concurrent.ConcurrentDictionary<string, List<string>>();

        private bool _birthFailureTold;

        private bool BirthsOn => _config.EnableBirthChronicle && _birthLedger != null;

        // Called by SaveMemory for every write, beside FoldPendingWeddingBeats.
        private static void FoldPendingBirthBeats(Hero npc, NpcMemory memory)
        {
            try
            {
                if (npc == null || memory == null) return;
                if (!_pendingBirthBeats.TryRemove(npc.StringId, out var beats) || beats == null) return;
                foreach (var beat in beats)
                {
                    if (string.IsNullOrWhiteSpace(beat)) continue;
                    if (memory.RecentTurns.Any(t => t != null && t.PlayerLine == beat)) continue;
                    AddSilentInnerBeat(memory, npc, beat);
                }
            }
            catch { /* the beat is a memory, not a load-bearing rail */ }
        }

        private void LoadBirthLedger()
        {
            try { _birthLedger = BirthLedger.LoadFrom(NpcPaths.BirthsFolder); }
            catch { _birthLedger = null; }
        }

        // ------------------------------ the hook ------------------------------

        // Fires from inside PregnancyCampaignBehavior.CheckOffspringToDeliver. Only the player's
        // own children are chronicled; the world's other cradles are the world's.
        private void OnGivenBirthForChronicle(Hero mother, List<Hero> aliveChildren, int stillbornCount)
        {
            try
            {
                if (!BirthsOn || mother == null) return;
                var player = Hero.MainHero;
                if (player == null) return;

                var newborns = (aliveChildren ?? new List<Hero>()).Where(c => c != null).ToList();
                var father = Safe(() => newborns.FirstOrDefault()?.Father, null)
                             ?? Safe(() => FatherByMarriage(mother), null);

                bool playerIsMother = mother == player;
                bool playerIsFather = father == player;
                if (!playerIsMother && !playerIsFather) return;

                // Everything below reads live campaign state and MUST run here, synchronously: the
                // death-in-labour roll comes next, and it may take her and her whereabouts with her.
                var record = CaptureBirth(mother, father, newborns, stillbornCount, playerIsMother);
                _birthLedger!.Save(record);

                if (!record.AnyLived)
                {
                    // No chronicler is called for this. There is nothing here worth a paid call and
                    // a great deal worth getting wrong; the mark is written by hand, and the feast
                    // is never offered.
                    RecordGrief(mother, record);
                    return;
                }

                NotifyBirth($"❧ {record.MotherName} has borne {record.ChildWords()} — {record.ChildNames()}.");
                var facts = GatherBirthFacts(mother, father, record);
                BeginBirthHour(record, facts);

                // And the feast, once the world has finished settling this instant.
                MainThreadDispatcher.Enqueue(() => TryOfferTheFeast(record));
            }
            catch (Exception ex) { ModLog.Error("beginning the birth chronicle", ex); }
        }

        // A father the newborn does not name (an odd save, another mod's hand): fall back to whom
        // she is actually wed to, never to a guess.
        private static Hero? FatherByMarriage(Hero mother)
        {
            try
            {
                var player = Hero.MainHero;
                if (player != null && FamilyBuilder.AreWed(player, mother)) return player;
                return mother.Spouse;
            }
            catch { return null; }
        }

        // ------------------------------ the facts of the day ------------------------------

        private BirthRecord CaptureBirth(Hero mother, Hero? father, List<Hero> newborns,
            int stillbornCount, bool playerIsMother)
        {
            var player = Hero.MainHero;
            var settlement = BirthPlace(mother);
            double now = CampaignTime.Now.ToDays;
            var newbornIds = new HashSet<string>(newborns.Select(c => c.StringId), StringComparer.Ordinal);

            var record = new BirthRecord
            {
                Id = _birthLedger!.NextId(now),
                GameDay = now,
                DateText = CalradiaDate(),
                PlaceName = Safe(() => settlement == null ? string.Empty : SettlementPhrase(settlement), string.Empty),
                CultureName = Safe(() => settlement?.Culture?.Name?.ToString() ?? string.Empty, string.Empty),

                MotherId = Safe(() => mother.StringId, string.Empty),
                MotherName = Safe(() => mother.Name?.ToString() ?? "she", "she"),
                MotherIsPlayer = playerIsMother,
                MotherAge = Safe(() => (int)mother.Age, 0),

                FatherId = Safe(() => father?.StringId ?? string.Empty, string.Empty),
                FatherName = Safe(() => father?.Name?.ToString() ?? string.Empty, string.Empty),
                FatherIsPlayer = father != null && father == player,
                FatherAge = Safe(() => (int)(father?.Age ?? 0f), 0),

                // Whether the two of them were wed ON THE DAY, captured here with everything else
                // and for the same reason: a marriage can be made or unmade later, and a child born
                // outside one does not retroactively become a child of it (2026.08.15).
                BornInMarriage = Safe(() => father != null && FamilyBuilder.AreWed(mother, father), false),

                StillbornCount = Math.Max(0, stillbornCount),
                PlayerWasThere = Safe(() => WasPresentAt(player, mother), false),
                FatherWasThere = Safe(() => WasPresentAt(father, mother), false),
                // The children THESE TWO share, not every child she has ever borne. The prompt
                // asserts the count of the couple ("they had N children already"), so counting her
                // half-siblings there would have handed the chronicler a plain falsehood about
                // them (2026.08.10 review).
                OlderChildren = Safe(() => mother.Children?.Count(c => c != null && c.IsAlive
                                        && !newbornIds.Contains(c.StringId)
                                        && (father == null || c.Father == father || c.Mother == father)) ?? -1, -1),
            };

            foreach (var child in newborns)
                record.Children.Add(new BirthChild
                {
                    HeroId = Safe(() => child.StringId, string.Empty),
                    Name = Safe(() => child.Name?.ToString() ?? "the child", "the child"),
                    IsFemale = Safe(() => child.IsFemale, false),
                });

            // How long they have been wed — from OUR OWN wedding book, since the world keeps no
            // wedding date anywhere. Absent (a marriage from before the chronicle, or none) it
            // simply goes untold rather than guessed.
            try
            {
                var npcParent = playerIsMother ? father : mother;
                var wedding = npcParent == null ? null : _weddingLedger?.OwnWeddingOf(npcParent.StringId);
                if (wedding != null) record.MarriedDays = Math.Max(0, now - wedding.GameDay);
            }
            catch { }

            return record;
        }

        // Whether this soul truly stood where the child was born. Deliberately PHYSICAL — the same
        // party or the same walls — and not IsCoLocated, whose closed-keep rules are about whether
        // two people can reach each other to TALK, which is a different question at a bedside.
        private static bool WasPresentAt(Hero? who, Hero mother)
        {
            try
            {
                if (who == null || mother == null) return false;
                if (who == mother) return true;
                var party = who.PartyBelongedTo;
                if (party != null && party == mother.PartyBelongedTo) return true;
                var his = who.CurrentSettlement;
                return his != null && his == mother.CurrentSettlement;
            }
            catch { return false; }
        }

        // Where the child came into the world: where the MOTHER stands, never where the player
        // does. The newborn's own CurrentSettlement is null at this instant (it is derived from a
        // party, and a newborn belongs to none) — decompile-verified, and a trap worth naming.
        private static Settlement? BirthPlace(Hero mother)
        {
            try
            {
                return mother.CurrentSettlement
                    ?? mother.PartyBelongedTo?.CurrentSettlement
                    ?? (mother == Hero.MainHero ? Settlement.CurrentSettlement : null);
            }
            catch { return null; }
        }

        // Everything the chronicler is told, gathered on the game thread before any await.
        private BirthText.Facts GatherBirthFacts(Hero mother, Hero? father, BirthRecord record,
            bool forFeast = false)
        {
            var facts = new BirthText.Facts
            {
                MotherName = record.MotherName,
                FatherName = string.IsNullOrWhiteSpace(record.FatherName) ? PlayerName() : record.FatherName,
                FatherWasThere = record.FatherWasThere,
                ChildWords = record.ChildWords(),
                ChildNames = record.ChildNames(),
                StillbornCount = record.StillbornCount,
                OlderChildren = record.OlderChildren,
                DateText = record.DateText,
                PlacePhrase = record.PlaceName,
                CultureName = record.CultureName,
                Witnesses = record.Witnesses
                    .Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                    .Select(w => string.IsNullOrWhiteSpace(w.Detail) ? w.Name : $"{w.Name}, {w.Detail}")
                    .ToList(),
                ScaleNote = BirthTiers.ChroniclerNote(record.Scale),
            };

            // A feast kept later is dated to the day it was KEPT, and the chronicler is told how
            // much older the child already is — or it writes a newborn into a hall it left weeks
            // ago (live probe, 2026.08.10).
            try
            {
                if (forFeast && !string.IsNullOrWhiteSpace(record.FeastDateText))
                {
                    facts.DateText = record.FeastDateText;
                    if (!string.IsNullOrWhiteSpace(record.FeastPlaceName)) facts.PlacePhrase = record.FeastPlaceName;
                    if (!string.IsNullOrWhiteSpace(record.FeastCultureName)) facts.CultureName = record.FeastCultureName;
                    int days = record.FeastGameDay > 0 ? (int)Math.Round(record.FeastGameDay - record.GameDay) : 0;
                    if (days >= 2)
                        facts.FeastDelayPhrase = days < 10
                            ? $"the child was born {days} days before it"
                            : $"the child was born some {days} days before it, and is no longer a thing of hours";
                }
            }
            catch { }

            // Both off the RECORD, not the live heroes: a feast bought a month later, or an account
            // retried, must still be told at the age the day itself held.
            facts.MotherAge = record.MotherAge > 0 ? record.MotherAge : Safe(() => (int)mother.Age, 0);
            facts.FatherAge = record.FatherAge > 0
                ? record.FatherAge
                : Safe(() => (int)(FindAliveHero(record.FatherId)?.Age ?? 0f), 0);
            try
            {
                // The persona sheet is the NPC parent's; when the player is the mother there is no
                // sheet to read, and the chronicler simply gets fewer facts.
                if (!record.MotherIsPlayer)
                {
                    var persona = PersonaBuilder.Build(mother, _config);
                    facts.MotherStation = persona?.RoleDescription ?? string.Empty;
                    facts.MotherTraits = persona?.PersonalityDescription ?? string.Empty;
                    facts.MotherSelfText = PromptFiles.LoadNpcPrompt(
                        NpcPaths.CustomInstructionsFile(mother), record.MotherName);
                }
            }
            catch { }
            try { facts.FatherStanding = record.FatherIsPlayer ? PlayerStandingPhrase() : string.Empty; } catch { }
            try { facts.SeasonPhrase = SeasonPhrase(); } catch { }
            try { facts.CircumstancePhrase = CircumstancePhrase(); } catch { }
            try
            {
                if (record.MarriedDays >= 0)
                    facts.MarriedPhrase = record.MarriedDays < 40
                        ? "only a matter of weeks"
                        : $"some {(int)Math.Round(record.MarriedDays)} days";
            }
            catch { }
            try
            {
                var npcParent = NpcParentOf(record);
                if (npcParent != null)
                {
                    var memory = LoadMemory(npcParent);
                    facts.SharedStory = (memory.Summary ?? string.Empty).Trim();
                    facts.RecentWords = RecentSpokenWords(memory, PlayerName(),
                        Safe(() => npcParent.Name?.ToString() ?? string.Empty, string.Empty));
                }
            }
            catch { }
            try
            {
                var world = PromptFiles.LoadGlobalPrompt();
                var guidance = ApplyTokens(_config.RoleplayGuidance, record.MotherName);
                facts.WorldText = string.Join(" ", new[] { world, guidance }
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            }
            catch { }

            return facts;
        }

        /// <summary>The parent who is NOT the player — the only one of the two with a memory to
        /// write into. Null when they have died since.</summary>
        private Hero? NpcParentOf(BirthRecord record)
        {
            try
            {
                var id = record.MotherIsPlayer ? record.FatherId : record.MotherId;
                return string.IsNullOrWhiteSpace(id) ? null : FindAliveHero(id);
            }
            catch { return null; }
        }

        // ------------------------------ part one: the hour ------------------------------

        private void BeginBirthHour(BirthRecord record, BirthText.Facts facts)
        {
            if (record == null || !_birthsWriting.Add(record.Id + ":hour")) return;
            // A disk failure here must never strand the guard: stranded, this record can never be
            // attempted again for the rest of the session.
            try { record.BirthAttempts++; _birthLedger?.Save(record); }
            catch (Exception ex) { ModLog.Error("saving a birth before writing its hour", ex); }
            _ = WriteBirthHourAsync(record, facts);
        }

        private async Task WriteBirthHourAsync(BirthRecord record, BirthText.Facts facts)
        {
            using var _cost = UsageLedger.BeginInteraction("the birth chronicle", record.MotherName);
            try
            {
                var raw = await _storyClient.CompleteAsync(new List<ChatMessage>
                {
                    ChatMessage.User(BirthText.BuildBirthPrompt(facts)),
                }).ConfigureAwait(false);

                var hour = BirthText.CleanAccount(raw);
                if (!BirthText.LooksLikeAnAccount(hour))
                {
                    ModLog.Info("the birth chronicle: no usable account of the hour came back; it waits, unwritten.");
                    MainThreadDispatcher.Enqueue(() => BirthStoryFailed(record));
                    return;
                }
                MainThreadDispatcher.Enqueue(() => FinishBirthHour(record, hour));
            }
            catch (Exception ex)
            {
                ModLog.Error("writing the hour of a birth", ex);
                MainThreadDispatcher.Enqueue(() => BirthStoryFailed(record));
            }
            finally
            {
                MainThreadDispatcher.Enqueue(() => _birthsWriting.Remove(record.Id + ":hour"));
            }
        }

        private void FinishBirthHour(BirthRecord record, string hour)
        {
            try
            {
                if (!StillOurCampaign()) return;
                record.BirthAccount = hour;
                _birthLedger!.Save(record);
                RecordHourBeats(record);
                TrySealIfWhole(record);
                ModLog.Info($"the birth chronicle: {record.Title()} — the hour written ({hour.Length} characters).");
            }
            catch (Exception ex) { ModLog.Error("finishing the hour of a birth", ex); }
        }

        // The hour into the mother's memory (hers alone), and the plain fact into the father's.
        private void RecordHourBeats(BirthRecord record)
        {
            try
            {
                if (record.BirthBeatDone || string.IsNullOrWhiteSpace(record.BirthAccount)) return;

                if (!record.MotherIsPlayer)
                {
                    var mother = FindAliveHero(record.MotherId);
                    if (mother != null)
                    {
                        WriteBirthBeat(mother, BirthText.MotherHourBeat(
                            string.IsNullOrWhiteSpace(record.FatherName) ? PlayerName() : record.FatherName,
                            record.PlaceName, record.ChildWords(), record.ChildNames(), record.BirthAccount),
                            OutreachMark.PlayerEngaged);
                        UI.TalkUI.OnThreadChanged(mother, markUnread: false);
                    }
                }

                // The father, when he is one of ours. He is NEVER handed her first person — only
                // the fact, and whether he was standing there, which for a man at war is often the
                // whole of what he has of it.
                if (!record.FatherIsPlayer && !string.IsNullOrWhiteSpace(record.FatherId))
                {
                    var father = FindAliveHero(record.FatherId);
                    if (father != null)
                    {
                        WriteBirthBeat(father, BirthText.FatherBeat(record.MotherName, record.PlaceName,
                            record.ChildWords(), record.ChildNames(), record.FatherWasThere),
                            OutreachMark.PlayerEngaged);
                        UI.TalkUI.OnThreadChanged(father, markUnread: false);
                    }
                }

                // AND THE CHILD ITSELF (2026.08.15). A hero has a memory file from the day it is
                // born, so its own history simply begins accumulating — and the day it comes of age
                // and first opens its mouth, it already knows who it is. Nothing briefs it, nothing
                // is generated, and this costs one hand-written line.
                //
                // THE PRIVACY FENCE RUNS ONE PERSON FURTHER HERE: it is given the facts of its own
                // day and never its mother's first-person account of the hour. Planting her private
                // "I" in her child's memory would be exactly the small lie this mod exists not to
                // tell, and this is the code that stops it rather than a sentence in a prompt.
                RecordChildsOwnBeginning(record);

                record.BirthBeatDone = true;
                _birthLedger?.Save(record);
            }
            catch (Exception ex) { ModLog.Error("recording the beats of a birth", ex); }
        }

        /// <summary>One flat line into each newborn's own memory. Never the mother's account.</summary>
        private void RecordChildsOwnBeginning(BirthRecord record)
        {
            try
            {
                if (record?.Children == null) return;
                foreach (var child in record.Children)
                {
                    var hero = FindAliveHero(child?.HeroId);
                    if (hero == null) continue;
                    // owned: only what is CERTAIN at this instant. This runs when the hour's
                    // account arrives, which is BEFORE the player has answered the owning question
                    // — and IsOwnedBeforeTheWorld reads "yes" while it is still unasked, so using
                    // it here made every child's own first memory assert a recognition that had not
                    // happened yet (self-review, 2026.08.15). A child of the marriage is the only
                    // one certainly owned now; the rest get their line from OwnTheChild when the
                    // answer actually comes.
                    WriteBirthBeat(hero, BirthText.ChildBornBeat(
                        record.MotherName,
                        string.IsNullOrWhiteSpace(record.FatherName) ? PlayerName() : record.FatherName,
                        record.PlaceName, record.BornInMarriage), OutreachMark.None);
                }
            }
            catch (Exception ex) { ModLog.Error("setting down a child's own beginning", ex); }
        }

        // A child who never drew breath: one plain mark, written by hand, and nothing else. No
        // chronicler, no feast, no notice dressed up as gladness.
        private void RecordGrief(Hero mother, BirthRecord record)
        {
            try
            {
                if (record.MotherIsPlayer) return;      // the player's own grief is not ours to narrate
                var npcMother = FindAliveHero(record.MotherId);
                if (npcMother == null) return;
                WriteBirthBeat(npcMother, BirthText.GriefBeat(
                    string.IsNullOrWhiteSpace(record.FatherName) ? PlayerName() : record.FatherName,
                    record.PlaceName, twinLived: false), OutreachMark.None);
                record.BirthBeatDone = true;
                _birthLedger?.Save(record);
                UI.TalkUI.OnThreadChanged(npcMother, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("recording a child who did not live", ex); }
        }

        // ------------------------------ part two: the feast ------------------------------

        /// <summary>
        /// Asks what feast the child will have — but only where the father can honestly be at it.
        /// A man three weeks' ride away does not throw a feast by letter; he is asked when he
        /// finally rides in, and <see cref="OfferPendingFeasts"/> is what remembers to ask him.
        /// </summary>
        /// <returns>True only when a question was actually put to the player. The caller uses this
        /// to keep walking its list: a child who cannot be asked about — a mother two weeks' ride
        /// away, a mother dead in labour — must never eat the hour's one question and leave every
        /// younger child unasked behind it (2026.08.10 review).</returns>
        private bool TryOfferTheFeast(BirthRecord record)
        {
            try
            {
                if (!BirthsOn || record == null || !record.AnyLived) return false;
                if (record.FeastOffered || record.Scale != BirthScale.Unfeasted) return false;

                var mother = record.MotherIsPlayer ? Hero.MainHero : FindAliveHero(record.MotherId);
                if (mother == null || !mother.IsAlive) return false;
                if (!record.MotherIsPlayer && !IsCoLocated(mother)) return false;   // he is not there yet
                if (!CanAskTheFeastNow()) return false;                             // mid-battle, mid-talk
                // An empty purse is not a question. A modal whose every option is greyed out asks
                // nothing and answers nothing — and a decline of it could not be allowed to settle
                // the record either, so the hour re-asked it EVERY HOUR for thirty days, pausing
                // the world each time (2026.08.10, second review: introduced by the FIRST review's
                // own fix). So it is simply never shown, and the question comes on its own the
                // moment the purse can carry the cheapest feast.
                // THE CHOICE IS THREE-WAY FOR A CHILD BORN OUTSIDE A MARRIAGE (2026.08.15) — and
                // the question is asked of the LIVE world, not only of the record: every birth
                // written before this field existed carries BornInMarriage = false, and asking a
                // long-married player whether his wife's child is his would be absurd
                // (self-review). If she is his wife today, no such question arises.
                bool mustOwnIt = !record.BornInMarriage
                    && !Safe(() => FamilyBuilder.AreWed(Hero.MainHero, mother), false);

                // An empty purse is not a question — EXCEPT when one of the answers is free. Owning
                // a child quietly costs nothing and is the whole point of the choice; gating it
                // behind the cheapest feast meant a broke man could never say the child was his
                // (self-review, 2026.08.15).
                if (!mustOwnIt && (Hero.MainHero?.Gold ?? 0) < BirthTiers.All.Min(t => t.Price)) return false;

                var player = Hero.MainHero;
                var settlement = BirthPlace(mother);
                var venue = VenueOf(settlement, out bool ownTown);

                // Blood is the game's and untouched; what is asked here is HONOR — whether he will
                // say out loud, where people can hear it, that this child is his. Feast it, own it
                // quietly, or say nothing. For a child of the marriage no such question exists and
                // the popup is exactly what it always was.
                var elements = new List<InquiryElement>();
                if (mustOwnIt)
                    elements.Add(new InquiryElement(QuietOwning, "Own the child — no feast", null, true,
                        "You say it plainly, where it will be heard, and keep no hall. It costs nothing and "
                        + "changes everything the world is allowed to say about this child — and about its mother."));

                foreach (var tier in BirthTiers.All)
                {
                    bool afford = (player?.Gold ?? 0) >= tier.Price;
                    bool fits = tier.FitsIn(venue, ownTown);
                    var why = new StringBuilder(tier.PlayerDescription);
                    why.Append("\n\nHeld ").Append(tier.VenueRequirement).Append('.');
                    why.Append(" Your house gains ").Append(tier.Renown).Append(" renown.");
                    if (!fits) why.Append("\n\n(Not here — ride somewhere worthier while the child is still new, and you will be asked again.)");
                    else if (!afford) why.Append("\n\n(Your purse cannot carry this.)");
                    elements.Add(new InquiryElement(tier.Scale,
                        $"{tier.Name} — {tier.Price} denars", null, afford && fits, why.ToString()));
                }

                var body = new StringBuilder();
                body.Append($"{record.MotherName} has borne {record.ChildWords()} — {record.ChildNames()}.");
                if (!record.PlayerWasThere) body.Append(" You were not there for it.");
                if (mustOwnIt)
                    body.Append("\n\n").Append(record.MotherName).Append(" is not your wife. In this world a man's ")
                        .Append("children are the ones he owns before everyone — and what he does not own is not ")
                        .Append("counted his, whatever the whole town privately knows. Say nothing and the child is ")
                        .Append("still yours by blood, and grows up in its mother's shadow.");
                body.Append("\n\nWhat you spend decides who is called, and everyone who stands there will carry this day for the rest of their life. ")
                    .Append("A child welcomed with nothing is welcomed all the same — the hour itself is already written down.\n\n")
                    .Append($"You hold {player?.Gold ?? 0} denars.");

                var data = new MultiSelectionInquiryData(
                    new TextObject(mustOwnIt
                        ? "{=ImmersiveAI_BirthOwnTitle}Is this child yours before the world?"
                        : "{=ImmersiveAI_BirthFeastTitle}How will you welcome the child?").ToString(),
                    body.ToString(),
                    elements, true, 1, 1,
                    new TextObject("{=ImmersiveAI_BirthFeastAccept}Keep the feast").ToString(),
                    new TextObject(mustOwnIt
                        ? "{=ImmersiveAI_BirthSayNothing}Say nothing"
                        : "{=ImmersiveAI_BirthFeastDecline}No feast").ToString(),
                    chosen =>
                    {
                        try
                        {
                            MarkFeastOffered(record);
                            var pick = chosen?.FirstOrDefault()?.Identifier;
                            // Every feast is an owning; the free choice is an owning with no hall.
                            if (pick is BirthScale scale) { OwnTheChild(record, late: false); HoldTheFeast(record, scale); }
                            else if (pick as string == QuietOwning) { OwnTheChild(record, late: false); TrySealIfWhole(record); }
                        }
                        catch (Exception ex) { ModLog.Error("choosing the child's feast", ex); }
                    },
                    // Declining settles it — a father who said no is not asked again every hour he
                    // stands beside her. UNLESS nothing was open to him in the first place: the
                    // hints promise "ride somewhere worthier and you will be asked again", and a
                    // decline that was never a choice must not quietly make a liar of that.
                    // A decline ALWAYS settles it. Anything else re-asks a world-pausing question
                    // on the next hourly tick, and the next, and the next.
                    _ => DeclineTheFeast(record));

                MBInformationManager.ShowMultiSelectionInquiry(data, true);
                return true;
            }
            catch (Exception ex) { ModLog.Error("offering the child's feast", ex); return false; }
        }

        /// <summary>The one answer in the feast question that is not a feast and not a refusal.</summary>
        private const string QuietOwning = "immersiveai_birth_own_quietly";

        /// <summary>
        /// THE OWNING (2026.08.15) — the whole of what this feature adds, and it is a layer of WORDS
        /// over blood the game already settled. Nothing about the child's parentage, clan or
        /// inheritance moves here, and nothing ever will: what moves is what the world is allowed to
        /// say, which in this era is the entire difference between a son and somebody's boy.
        ///
        /// <paramref name="late"/> is the giving of the name — the same act, years afterwards, in
        /// front of everyone who spent those years not saying it. The record keeps which it was
        /// because they are not the same thing and nobody involved would ever confuse them.
        /// </summary>
        private void OwnTheChild(BirthRecord record, bool late, bool withFeast = false)
        {
            try
            {
                if (record == null || record.Owned == Acknowledgement.Given) return;
                record.Owned = Acknowledgement.Given;
                record.NameGivenLate = late;
                if (late)
                {
                    record.NameGivenDay = CampaignTime.Now.ToDays;
                    record.NameGivenDateText = CalradiaDate();
                }
                _birthLedger?.Save(record);

                var fatherName = string.IsNullOrWhiteSpace(record.FatherName) ? PlayerName() : record.FatherName;

                // The mother learns it, wherever she stands. She is told the fact and nothing about
                // how to take it — hers has been hers since the day move_heart shipped.
                var mother = record.MotherIsPlayer ? null : FindAliveHero(record.MotherId);
                if (mother != null)
                {
                    WriteBirthBeat(mother, BirthText.MotherNameBeat(fatherName, record.ChildNames(),
                        given: true, withFeast: withFeast, late: late), OutreachMark.PlayerEngaged);
                    UI.TalkUI.OnThreadChanged(mother, markUnread: true);
                }

                // And the child, in whose own memory it will still be waiting when the child is
                // grown enough to have something to say about it.
                foreach (var kid in record.Children ?? new List<BirthChild>())
                {
                    var hero = FindAliveHero(kid?.HeroId);
                    if (hero == null) continue;
                    WriteBirthBeat(hero, BirthText.ChildNameBeat(fatherName, late), OutreachMark.None);
                }

                NotifyBirth(late
                    ? $"❧ You have owned {record.ChildNames()} before the world, and given the child your name."
                    : $"❧ {record.ChildNames()} is yours before the world.");
            }
            catch (Exception ex) { ModLog.Error("owning a child before the world", ex); }
        }

        /// <summary>He said nothing. Recorded as a decision rather than an absence — it is one.</summary>
        private void WithholdTheName(BirthRecord record)
        {
            try
            {
                if (record == null || record.BornInMarriage) return;
                if (record.Owned != Acknowledgement.NeverArose) return;
                record.Owned = Acknowledgement.Withheld;
                _birthLedger?.Save(record);

                var mother = record.MotherIsPlayer ? null : FindAliveHero(record.MotherId);
                if (mother != null)
                {
                    WriteBirthBeat(mother, BirthText.MotherNameBeat(
                        string.IsNullOrWhiteSpace(record.FatherName) ? PlayerName() : record.FatherName,
                        record.ChildNames(), given: false, withFeast: false, late: false),
                        OutreachMark.PlayerEngaged);
                    UI.TalkUI.OnThreadChanged(mother, markUnread: true);
                }
            }
            catch (Exception ex) { ModLog.Error("withholding a name", ex); }
        }

        /// <summary>
        /// THE GIVING OF THE NAME — the late owning, possible at any age and heavier the longer it
        /// waited. The design record calls the "adoption" idea from the first brainstorm exactly
        /// this act, and it needs no separate mechanic: the child is already the player's by blood,
        /// so what changes is only, and entirely, what has been said.
        /// </summary>
        internal void GiveTheNameTo(Hero child)
        {
            try
            {
                if (child == null || _birthLedger == null) return;
                var record = _birthLedger.Records.FirstOrDefault(r =>
                    r != null && r.AwaitsTheName
                    && r.Children != null
                    && r.Children.Any(c => c != null && string.Equals(c.HeroId, child.StringId, StringComparison.Ordinal)));
                if (record == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{child.Name} is not waiting on a name from you.", SealGrey));
                    return;
                }
                OwnTheChild(record, late: true);
            }
            catch (Exception ex) { ModLog.Error("giving the name", ex); }
        }

        /// <summary>
        /// HOW THE PLAYER'S HOUSE IS SPOKEN OF (2026.08.15) — one line for the sheet, built from the
        /// ledger's own record of what has been SAID rather than from the game's record of blood.
        /// A child of the marriage is simply his; one he has owned is his and the world says how; one
        /// he has not owned appears in nobody's line but its mother's.
        ///
        /// Empty for a house with nothing to explain, which is most houses — a player who has never
        /// fathered a child outside a marriage pays nothing for this.
        /// </summary>
        internal static string HouseOfThePlayerLine()
        {
            try
            {
                var self = Current;
                if (self?._birthLedger == null || !self.BirthsOn) return string.Empty;

                var children = new List<BirthText.HouseChild>();
                bool anyOutside = false;
                foreach (var record in self._birthLedger.Records)
                {
                    if (record == null || !record.AnyLived) continue;
                    if (!record.FatherIsPlayer && !record.MotherIsPlayer) continue;
                    if (!record.BornInMarriage) anyOutside = true;
                    foreach (var kid in record.Children ?? new List<BirthChild>())
                    {
                        if (kid == null || string.IsNullOrWhiteSpace(kid.Name)) continue;
                        children.Add(new BirthText.HouseChild
                        {
                            Name = kid.Name,
                            MotherName = record.MotherName,
                            InMarriage = record.BornInMarriage,
                            Owned = record.IsOwnedBeforeTheWorld,
                        });
                    }
                }

                // Nothing outside a marriage means nothing the world needs explaining, and the kin
                // lines already carry the family. No line, no tokens.
                if (!anyOutside) return string.Empty;
                return BirthText.HouseLine(children, PlayerName());
            }
            catch { return string.Empty; }
        }

        /// <summary>How many of THIS woman's children by the player have never been owned before the
        /// world. The concrete object of a lover's asking — her craving stops being an abstraction
        /// the moment there is a specific child with no name, and that is what makes her a living
        /// character for years instead of an episode.</summary>
        internal static int UnnamedChildrenOf(Hero mother)
        {
            try
            {
                var self = Current;
                if (self?._birthLedger == null || mother == null || !self.BirthsOn) return 0;
                return self._birthLedger.Records
                    .Where(r => r != null && r.AwaitsTheName
                             && string.Equals(r.MotherId, mother.StringId, StringComparison.Ordinal))
                    .Sum(r => r.Children?.Count ?? 0);
            }
            catch { return 0; }
        }

        /// <summary>Every child of the player's still waiting to be owned — what the one door's page
        /// offers, and what a lover has to be asking about.</summary>
        internal static List<Hero> ChildrenAwaitingTheName()
        {
            var waiting = new List<Hero>();
            try
            {
                var self = Current;
                if (self?._birthLedger == null) return waiting;
                foreach (var record in self._birthLedger.Records)
                {
                    if (record == null || !record.AwaitsTheName) continue;
                    foreach (var kid in record.Children ?? new List<BirthChild>())
                    {
                        var hero = FindAliveHero(kid?.HeroId);
                        if (hero != null) waiting.Add(hero);
                    }
                }
            }
            catch { }
            return waiting;
        }

        internal static void DevGiveTheName(Hero child)
        {
            try { if (child != null) Current?.GiveTheNameTo(child); }
            catch (Exception ex) { ModLog.Error("dev: giving the name", ex); }
        }

        private void MarkFeastOffered(BirthRecord record)
        {
            try
            {
                if (record == null) return;
                record.FeastOffered = true;
                _birthLedger?.Save(record);
            }
            catch { }
        }

        // No feast, then. The day is complete as soon as the hour is written — so if it already is,
        // it is laid before the player now; if the chronicler is still at it, FinishBirthHour will.
        private void DeclineTheFeast(BirthRecord record)
        {
            try
            {
                MarkFeastOffered(record);
                // For a child born outside a marriage, declining is not "no feast" — it is saying
                // nothing, which in this world is its own loud act. Recorded as the decision it is.
                WithholdTheName(record);
                TrySealIfWhole(record);
            }
            catch (Exception ex) { ModLog.Error("declining the child's feast", ex); }
        }

        /// <summary>
        /// Whether nothing more is coming for the feast side of this day — either no feast was
        /// ever bought and the question is answered or its window has closed, or one was bought
        /// and its account has arrived or will never arrive.
        /// </summary>
        private static bool FeastSideSettled(BirthRecord record, double today, bool motherGone) =>
            record.Scale == BirthScale.Unfeasted
                // Nobody left to keep a feast with is nothing left to wait for: without this, a
                // mother lost in labour held her child's whole day unsealed for a month.
                ? motherGone || record.FeastOffered || today - record.GameDay > BirthLedger.FeastOfferDays
                : !string.IsNullOrWhiteSpace(record.FeastAccount)
                  || record.FeastAttempts >= BirthLedger.MaxAttempts;

        /// <summary>Whether nothing more is coming for the HOUR either — it is written, or it has
        /// been asked for as often as it ever will be, or there is no mother left to write it for.
        /// Symmetric with the feast ON PURPOSE: the first cut demanded a WRITTEN hour before any
        /// day could be laid down, so a feast the player had actually paid for and received never
        /// reached births.txt at all when the hour's own calls had failed (2026.08.10 review).</summary>
        private static bool HourSideSettled(BirthRecord record, bool motherGone) =>
            !string.IsNullOrWhiteSpace(record.BirthAccount)
            || record.BirthAttempts >= BirthLedger.MaxAttempts
            || motherGone;

        /// <summary>
        /// Lays the day down the moment it is whole, and never before — the hour written, and the
        /// feast side finished one way or another. EVERY path that could complete a day calls this
        /// and lets it judge; the paths are too many to reason about one at a time, which is how
        /// the first cut got it wrong in three different ways at once (2026.08.10 review).
        /// </summary>
        private void TrySealIfWhole(BirthRecord record)
        {
            try
            {
                if (record == null || record.DaySealed) return;
                // A day with NOTHING written is not a day to lay down. A day with EITHER part
                // written and nothing further coming is.
                if (string.IsNullOrWhiteSpace(record.BirthAccount)
                    && string.IsNullOrWhiteSpace(record.FeastAccount)) return;

                bool motherGone = !record.MotherIsPlayer
                    && Safe(() => FindAliveHero(record.MotherId) == null, false);
                double today = Safe(() => CampaignTime.Now.ToDays, record.GameDay);
                if (!HourSideSettled(record, motherGone)) return;
                if (!FeastSideSettled(record, today, motherGone)) return;
                SealTheDay(record);
            }
            catch (Exception ex) { ModLog.Error("judging whether a child's day is whole", ex); }
        }

        /// <summary>The day laid down once and for all: into the readable births.txt, and before
        /// the player to read with the world held still. Idempotent, because the moment it happens
        /// at depends on what was chosen and more than one path can reach it.</summary>
        private void SealTheDay(BirthRecord record)
        {
            try
            {
                if (record == null || record.DaySealed) return;
                record.DaySealed = true;
                _birthLedger?.Save(record);
                _birthLedger?.AppendToChronicle(BirthText.ChronicleEntry(record));
                ShowBirthKeepsake(record);
            }
            catch (Exception ex) { ModLog.Error("sealing a child's day", ex); }
        }

        // A question worth answering deserves a moment the player can answer it in.
        private static bool CanAskTheFeastNow()
        {
            try
            {
                if (Campaign.Current == null) return false;
                if (Mission.Current != null) return false;
                if (Hero.OneToOneConversationHero != null) return false;
                if (MapEvent.PlayerMapEvent != null) return false;
                return true;
            }
            catch { return false; }
        }

        private static WeddingVenue VenueOf(Settlement? settlement, out bool ownTown)
        {
            ownTown = false;
            try
            {
                if (settlement == null) return WeddingVenue.OpenField;
                ownTown = settlement.OwnerClan == Clan.PlayerClan;
                if (settlement.IsTown) return WeddingVenue.Town;
                if (settlement.IsCastle) return WeddingVenue.Castle;
                if (settlement.IsVillage) return WeddingVenue.Village;
                return WeddingVenue.OpenField;
            }
            catch { return WeddingVenue.OpenField; }
        }

        // The feast bought: the purse, the hall, the house's name, and then the chronicler.
        private void HoldTheFeast(BirthRecord record, BirthScale scale)
        {
            try
            {
                if (!BirthsOn || record == null) return;
                var player = Hero.MainHero;
                int price = BirthTiers.PriceOf(scale);

                // The purse is re-run here, never trusted from the popup: a ransom could have
                // emptied it between the choosing and the keeping.
                if (price > 0 && (player?.Gold ?? 0) < price)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The feast could not be kept — {price} denars is more than you hold.", SealGrey));
                    return;
                }

                var mother = record.MotherIsPlayer ? player : FindAliveHero(record.MotherId);
                if (mother == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "There is no one left to keep the feast with.", SealGrey));
                    return;
                }

                if (price > 0) player!.ChangeHeroGold(-price);
                record.Scale = scale;
                record.FeastCost = price;
                record.FeastDateText = Safe(() => CalradiaDate(), record.DateText);
                record.FeastGameDay = Safe(() => CampaignTime.Now.ToDays, record.GameDay);

                var settlement = BirthPlace(mother);
                // Where the feast is KEPT, which after a delay is rarely where the child was born.
                record.FeastPlaceName = Safe(() => settlement == null ? string.Empty : SettlementPhrase(settlement), record.PlaceName);
                record.FeastCultureName = Safe(() => settlement?.Culture?.Name?.ToString() ?? string.Empty, record.CultureName);
                record.Witnesses.Clear();
                // BOTH parents are excluded, not "the mother and the player" — with a female
                // player those are the same soul, and her husband was being gathered as a witness
                // to his own child and then written the feast twice, once as a parent and once as
                // a guest (2026.08.10 review).
                // The other house is the NPC parent's — so at the naming feast her kin are called
                // beside the player's own (Anton, 2026.08.10: "нека дойде и нейния род").
                var npcParent = NpcParentOf(record);
                foreach (var hero in GatherCelebrationWitnesses(settlement, BirthTiers.Guests(scale),
                             excluded: new[] { mother, player, FindAliveHero(record.FatherId) },
                             otherHouse: OtherHouseOf(npcParent)))
                    record.Witnesses.Add(new BirthWitness
                    {
                        HeroId = hero.StringId,
                        Name = Safe(() => hero.Name?.ToString(), "someone") ?? "someone",
                        Detail = Safe(() => WitnessDetail(hero), string.Empty),
                    });
                _birthLedger!.Save(record);

                int renown = BirthTiers.RenownOf(scale);
                if (renown > 0)
                {
                    try { GainRenownAction.Apply(player!, renown, doNotNotify: true); } catch { }
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The child is spoken of: your house gains {renown} renown.", CradleGold));
                }

                NotifyBirth($"the chronicler takes up the pen for the feast of {record.ChildNames()}…");
                BeginBirthFeast(record, GatherBirthFacts(mother, FindAliveHero(record.FatherId), record, forFeast: true));
            }
            catch (Exception ex) { ModLog.Error("keeping the child's feast", ex); }
        }

        private void BeginBirthFeast(BirthRecord record, BirthText.Facts facts)
        {
            if (record == null || !_birthsWriting.Add(record.Id + ":feast")) return;
            try { record.FeastAttempts++; _birthLedger?.Save(record); }
            catch (Exception ex) { ModLog.Error("saving a birth before writing its feast", ex); }
            _ = WriteBirthFeastAsync(record, facts);
        }

        private async Task WriteBirthFeastAsync(BirthRecord record, BirthText.Facts facts)
        {
            using var _cost = UsageLedger.BeginInteraction("the birth chronicle", record.ChildNames());
            try
            {
                var raw = await _storyClient.CompleteAsync(new List<ChatMessage>
                {
                    // The hour is deliberately NOT handed over: this answer is copied verbatim
                    // into every witness's memory, so the parents' own account must never be in
                    // the room where it is written. See BuildFeastPrompt's remarks.
                    ChatMessage.User(BirthText.BuildFeastPrompt(facts)),
                }).ConfigureAwait(false);

                var feast = BirthText.CleanAccount(raw);
                if (!BirthText.LooksLikeAnAccount(feast))
                {
                    ModLog.Info("the birth chronicle: no usable account of the feast came back; it waits, unwritten.");
                    MainThreadDispatcher.Enqueue(() => BirthStoryFailed(record));
                    return;
                }
                MainThreadDispatcher.Enqueue(() => FinishBirthFeast(record, feast));
            }
            catch (Exception ex)
            {
                ModLog.Error("writing the feast of a birth", ex);
                MainThreadDispatcher.Enqueue(() => BirthStoryFailed(record));
            }
            finally
            {
                MainThreadDispatcher.Enqueue(() => _birthsWriting.Remove(record.Id + ":feast"));
            }
        }

        private void FinishBirthFeast(BirthRecord record, string feast)
        {
            try
            {
                if (!StillOurCampaign()) return;
                record.FeastAccount = feast;
                _birthLedger!.Save(record);
                RecordFeastBeats(record);

                NotifyBirth($"❧ The feast for {record.ChildNames()} is written. "
                          + (record.Witnesses.Count > 0
                             ? "Those who stood there will remember it."
                             : "It was a small day, and it is kept."));
                TrySealIfWhole(record);
                ModLog.Info($"the birth chronicle: {record.Title()} — the feast written "
                          + $"({feast.Length} characters, {record.Witnesses.Count} witnesses).");
            }
            catch (Exception ex) { ModLog.Error("finishing the feast of a birth", ex); }
        }

        private void RecordFeastBeats(BirthRecord record)
        {
            try
            {
                if (record.FeastBeatDone || string.IsNullOrWhiteSpace(record.FeastAccount)) return;

                var npcParent = NpcParentOf(record);
                if (npcParent != null)
                {
                    WriteBirthBeat(npcParent,
                        BirthText.ParentFeastBeat(record.ChildNames(), record.PlaceName, record.FeastAccount),
                        OutreachMark.PlayerEngaged);
                    UI.TalkUI.OnThreadChanged(npcParent, markUnread: false);
                }

                foreach (var witness in record.Witnesses)
                {
                    if (witness == null || string.IsNullOrWhiteSpace(witness.HeroId)) continue;
                    // Belt and braces behind the exclusion at gathering time: a record written by
                    // an older build could still carry a parent in its witness list, and a parent
                    // must never be handed the feast twice.
                    if (record.IsParent(witness.HeroId)) continue;
                    var hero = FindAliveHero(witness.HeroId);
                    if (hero == null) continue;
                    // Standing at a feast is not the player engaging THEM — the journey-beat rule.
                    WriteBirthBeat(hero, BirthText.WitnessFeastBeat(PlayerName(), record.ChildNames(),
                        record.PlaceName, record.FeastAccount), OutreachMark.None);
                    UI.TalkUI.OnThreadChanged(hero, markUnread: false);
                }

                record.FeastBeatDone = true;
                _birthLedger?.Save(record);
            }
            catch (Exception ex) { ModLog.Error("recording the feast beats", ex); }
        }

        // ------------------------------ the shared plumbing ------------------------------

        // A soul whose own exchange is in flight holds a memory instance loaded before this moment
        // and would save straight over a beat written now — so theirs is PARKED and folded in by
        // SaveMemory when their turn lands (the wedding's discipline, and it matters more here:
        // a birth very often happens in the middle of a conversation with the mother).
        private void WriteBirthBeat(Hero hero, string beat, OutreachMark mark)
        {
            try
            {
                if (hero == null || string.IsNullOrWhiteSpace(beat)) return;
                if (IsExchangeInFlight(hero))
                {
                    _pendingBirthBeats.AddOrUpdate(hero.StringId,
                        _ => new List<string> { beat },
                        (_, existing) => { existing.Add(beat); return existing; });
                    return;
                }
                AppendRecordedTurn(hero, beat, string.Empty, mark);
            }
            catch (Exception ex) { ModLog.Error("writing a birth beat", ex); }
        }

        // A parked beat lands at that soul's next memory write — but a talk that simply ENDS writes
        // nothing, so the hour sweeps up whatever is still waiting.
        private void FlushParkedBirthBeats()
        {
            try
            {
                if (_pendingBirthBeats.IsEmpty) return;
                foreach (var heroId in _pendingBirthBeats.Keys.ToList())
                {
                    var hero = FindAliveHero(heroId);
                    if (hero == null) { _pendingBirthBeats.TryRemove(heroId, out _); continue; }
                    if (IsExchangeInFlight(hero)) continue;
                    if (!_pendingBirthBeats.TryRemove(heroId, out var beats) || beats == null) continue;
                    foreach (var beat in beats)
                        if (!string.IsNullOrWhiteSpace(beat)) AppendRecordedTurn(hero, beat, string.Empty);
                }
            }
            catch (Exception ex) { ModLog.Error("flushing the parked birth beats", ex); }
        }

        // The world may have moved on while the chronicler wrote: a campaign closed, another
        // loaded, or this behavior left behind by a reload. Writing this day into THAT campaign's
        // book would be a lie (the wedding chronicle's own guard, verbatim).
        private bool StillOurCampaign()
        {
            try
            {
                if (_birthLedger == null || Campaign.Current == null) return false;
                if (!ReferenceEquals(Current, this)) return false;
                if (!string.Equals(_birthLedger.Folder, NpcPaths.BirthsFolder, StringComparison.OrdinalIgnoreCase))
                {
                    ModLog.Info("the birth chronicle: the campaign changed while it was written; the day is left to its own save.");
                    return false;
                }
                return true;
            }
            catch { return false; }
        }

        private void BirthStoryFailed(BirthRecord record)
        {
            try
            {
                if (_birthFailureTold) return;
                _birthFailureTold = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"The tale of {record?.ChildNames()} could not be set down just now — it will be attempted again shortly.",
                    SealGrey));
            }
            catch { }
        }

        private void NotifyBirth(string line)
        {
            try
            {
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage(line, CradleGold)));
            }
            catch { /* the notice is a nicety; the record is the thing */ }
        }

        // ------------------------------ the hourly hands ------------------------------

        /// <summary>Both parts are retried on the hour, one call at a time, and a father who was
        /// away is asked about the feast the moment he is beside her again.</summary>
        private void TendTheBirthChronicle()
        {
            try
            {
                if (!BirthsOn) return;
                FlushParkedBirthBeats();
                OfferPendingFeasts();
                if (LlmGate.AutonomyQuiet || _birthsWriting.Count > 0) return;

                foreach (var record in _birthLedger!.AwaitingHour())
                {
                    var mother = record.MotherIsPlayer ? Hero.MainHero : FindAliveHero(record.MotherId);
                    if (mother == null) continue;
                    ModLog.Info($"the birth chronicle: attempting the hour of {record.Title()} again "
                              + $"({record.BirthAttempts + 1} of {BirthLedger.MaxAttempts}).");
                    BeginBirthHour(record, GatherBirthFacts(mother, FindAliveHero(record.FatherId), record));
                    return;                                  // one at a time
                }

                foreach (var record in _birthLedger.AwaitingFeast())
                {
                    var mother = record.MotherIsPlayer ? Hero.MainHero : FindAliveHero(record.MotherId);
                    if (mother == null) continue;
                    ModLog.Info($"the birth chronicle: attempting the feast of {record.Title()} again "
                              + $"({record.FeastAttempts + 1} of {BirthLedger.MaxAttempts}).");
                    BeginBirthFeast(record, GatherBirthFacts(mother, FindAliveHero(record.FatherId), record, forFeast: true));
                    return;
                }

                // A birth the chronicler has finally given up on still gets its plain mark, so a
                // child is never born into total silence (the nights' own lesson, applied here
                // before it could ever bite).
                SweepBirthsThatNeverGotTheirBeat();
            }
            catch (Exception ex) { ModLog.Error("tending the birth chronicle", ex); }
        }

        private void OfferPendingFeasts()
        {
            try
            {
                double now = CampaignTime.Now.ToDays;
                foreach (var record in _birthLedger!.AwaitingFeastOffer(now))
                {
                    // One question an hour, never a queue — but the question goes to the first
                    // child it can actually be put about. Returning on a child who cannot be asked
                    // about would let one unreachable mother block every younger child for a month.
                    if (TryOfferTheFeast(record)) return;
                }
            }
            catch (Exception ex) { ModLog.Error("offering a waiting feast", ex); }
        }

        private void SweepBirthsThatNeverGotTheirBeat()
        {
            try
            {
                // A SNAPSHOT, not the live list: Records hands back the ledger's own List, and
                // both branches below save records — which replaces an element and bumps the
                // list's version, throwing InvalidOperationException on the next step of a live
                // foreach. Every other reader on the ledger already returns a copy; this was the
                // one live handle (2026.08.10 review).
                foreach (var record in _birthLedger!.Records.ToList())
                {
                    if (record == null || !record.AnyLived) continue;

                    // A day nobody ever came back to answer for must still be laid down: a father
                    // who never rode home inside the offer window, a mother who died in labour, a
                    // bought feast whose account met three refusals. TrySealIfWhole judges all of
                    // them by the same rule, so no path is reasoned about twice.
                    TrySealIfWhole(record);

                    if (record.BirthBeatDone) continue;
                    if (!string.IsNullOrWhiteSpace(record.BirthAccount)) { RecordHourBeats(record); continue; }
                    if (record.BirthAttempts < BirthLedger.MaxAttempts) continue;   // still hoped for

                    if (!record.MotherIsPlayer)
                    {
                        var mother = FindAliveHero(record.MotherId);
                        if (mother == null) continue;
                        WriteBirthBeat(mother, BirthText.MotherHourBeat(
                            string.IsNullOrWhiteSpace(record.FatherName) ? PlayerName() : record.FatherName,
                            record.PlaceName, record.ChildWords(), record.ChildNames(),
                            "I have not set the hour itself down, but I carry it."),
                            OutreachMark.PlayerEngaged);
                    }
                    // And the father, exactly as the ordinary path does. His mark never depended on
                    // the chronicler at all, so losing it here was pure oversight.
                    if (!record.FatherIsPlayer && !string.IsNullOrWhiteSpace(record.FatherId))
                    {
                        var father = FindAliveHero(record.FatherId);
                        if (father != null)
                            WriteBirthBeat(father, BirthText.FatherBeat(record.MotherName, record.PlaceName,
                                record.ChildWords(), record.ChildNames(), record.FatherWasThere),
                                OutreachMark.PlayerEngaged);
                    }
                    record.BirthBeatDone = true;
                    _birthLedger.Save(record);
                }
            }
            catch (Exception ex) { ModLog.Error("sweeping the births that were never written", ex); }
        }

        // ------------------------------ what the player reads ------------------------------

        private void ShowBirthKeepsake(BirthRecord record)
        {
            try
            {
                if (record == null) return;
                ShowScrollPopup(BirthKeepsakeTitle(record), BirthKeepsakeBody(record, opened: false), pause: true);
            }
            catch (Exception ex) { ModLog.Error("laying the birth before the player", ex); }
        }

        private static string BirthKeepsakeTitle(BirthRecord record) =>
            $"{record.ChildNames()} — born to {record.MotherName}";

        private static string BirthKeepsakeBody(BirthRecord record, bool opened)
        {
            var sb = new StringBuilder();
            sb.AppendLine(opened
                ? "(Kept whole. It is here whenever you want it.)"
                : "(This day is yours to keep — you can open it again at any time from the chat window, under their name.)");
            sb.AppendLine();

            var head = new StringBuilder();
            head.Append(string.IsNullOrWhiteSpace(record.DateText) ? "That day" : record.DateText.Trim());
            if (!string.IsNullOrWhiteSpace(record.PlaceName)) head.Append(", in ").Append(record.PlaceName.Trim());
            sb.AppendLine(head.ToString());
            sb.AppendLine($"{record.MotherName} bore {record.ChildWords()}: {record.ChildNames()}.");
            if (!record.PlayerWasThere) sb.AppendLine("You were not there for it.");
            if (record.StillbornCount > 0)
                sb.AppendLine(record.AnyLived ? "Not all of them lived." : "None of them lived.");
            if (record.FeastCost > 0)
                sb.AppendLine($"The feast: {BirthTiers.NameOf(record.Scale)} — {record.FeastCost} denars.");
            var standing = record.Witnesses?.Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name.Trim()).ToList() ?? new List<string>();
            if (standing.Count > 0)
                sb.AppendLine("Those who stood there: " + string.Join(", ", standing) + ".");

            if (!string.IsNullOrWhiteSpace(record.BirthAccount))
            {
                sb.AppendLine();
                sb.AppendLine("THE HOUR — hers, and yours");
                sb.AppendLine(record.BirthAccount.Trim());
            }
            if (!string.IsNullOrWhiteSpace(record.FeastAccount))
            {
                sb.AppendLine();
                sb.AppendLine("THE FEAST");
                sb.AppendLine(record.FeastAccount.Trim());
            }

            sb.AppendLine();
            sb.AppendLine("— — —");
            sb.AppendLine("Kept on your own machine, in plain text, for as long as you keep the folder:");
            sb.AppendLine(Safe(() => NpcPaths.BirthsFolder, "the campaign's _births folder"));
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The children of this bond, drawn for the window of the hearth (2026.08.10, Anton's ask
        /// — "историите за децата може би да се пазят в H бутона, и като го цъкна да виждам
        /// раждането после историята"). They stand at the TOP of her page, above the fortnight of
        /// nights, because that is the order the two things matter in: the nights are the passing
        /// weather of a marriage and the children are what it left behind.
        ///
        /// Each child is up to two cards, in the order he asked for: THE HOUR first — her own
        /// account, which the player is one of the only two souls entitled to — and then THE FEAST
        /// if one was kept. It needs no prefab of its own: the hearth window already draws exactly
        /// this shape for the nights.
        /// </summary>
        internal static List<NightEntryLine> BirthEntriesFor(Hero wife)
        {
            var lines = new List<NightEntryLine>();
            try
            {
                var self = Current;
                if (self == null || wife == null || !self._config.EnableBirthChronicle) return lines;
                var records = self._birthLedger?.ChildrenOf(wife.StringId);
                if (records == null) return lines;

                foreach (var record in records)
                {
                    if (record == null || !record.AnyLived) continue;

                    var head = new StringBuilder("❧ ").Append(record.ChildNames());
                    if (!string.IsNullOrWhiteSpace(record.DateText)) head.Append(" — ").Append(record.DateText.Trim());
                    if (!string.IsNullOrWhiteSpace(record.PlaceName)) head.Append(", in ").Append(record.PlaceName.Trim());

                    var hour = record.BirthAccount?.Trim();
                    lines.Add(new NightEntryLine
                    {
                        Header = head.ToString(),
                        HeaderColor = CradleGoldHex,
                        Body = string.IsNullOrWhiteSpace(hour)
                            ? (record.BirthAttempts >= BirthLedger.MaxAttempts
                                ? "The hour of it was never set down."
                                : "The hour of it is still being written.")
                            : hour,
                        IsNarration = string.IsNullOrWhiteSpace(hour),
                    });

                    if (!string.IsNullOrWhiteSpace(record.FeastAccount))
                        lines.Add(new NightEntryLine
                        {
                            Header = $"❧ The feast for {record.ChildNames()}"
                                   + (record.FeastCost > 0 ? $" — {BirthTiers.NameOf(record.Scale)}" : string.Empty),
                            HeaderColor = CradleGoldHex,
                            Body = record.FeastAccount.Trim(),
                            IsNarration = false,
                        });
                    else if (record.Scale == BirthScale.Unfeasted && record.FeastOffered)
                        lines.Add(new NightEntryLine
                        {
                            Header = string.Empty,
                            HeaderColor = CradleGoldHex,
                            Body = $"No feast was kept for {record.ChildNames()}.",
                            IsNarration = true,
                        });
                }
            }
            catch (Exception ex) { ModLog.Error("drawing a wife's children", ex); }
            return lines;
        }

        // The cradle's colour as the windows want it — the same gold as the notices, in hex.
        private const string CradleGoldHex = "#F5D67AFF";

        /// <summary>Every written birth this soul shares with the player, for the chat window's own
        /// page — appended under the wedding day, so one page holds a whole household.</summary>
        internal static string ChildrenPageFor(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null || !self._config.EnableBirthChronicle) return string.Empty;
                var records = self._birthLedger?.ChildrenOf(npc.StringId)
                    .Where(r => r != null && (!string.IsNullOrWhiteSpace(r.BirthAccount)
                                           || !string.IsNullOrWhiteSpace(r.FeastAccount)))
                    .ToList();
                if (records == null || records.Count == 0) return string.Empty;

                var sb = new StringBuilder();
                foreach (var record in records)
                {
                    sb.AppendLine();
                    sb.AppendLine("— — —");
                    sb.AppendLine(BirthKeepsakeTitle(record).ToUpperInvariant());
                    sb.AppendLine(BirthKeepsakeBody(record, opened: true));
                }
                return sb.ToString().TrimEnd();
            }
            catch { return string.Empty; }
        }

        // ------------------------------ what the rest of the mod reads ------------------------------

        /// <summary>The cradle's hand rides only for a soul who truly lived one of these days —
        /// a parent, or one who stood at a feast (a lean tool list keeps tools used).</summary>
        private bool CanRecallBirth(Hero npc)
        {
            try
            {
                return _config.EnableBirthChronicle
                    && _birthLedger != null
                    && npc != null
                    && _birthLedger.SharedWith(npc.StringId)
                        .Any(r => !string.IsNullOrWhiteSpace(r.BirthAccount)
                               || !string.IsNullOrWhiteSpace(r.FeastAccount));
            }
            catch { return false; }
        }

        // ------------------------------ the test lever ------------------------------

        /// <summary>Writes the chronicle again for a child of this soul's — the whole pipeline
        /// (both calls, the JSON, births.txt, the beats, the recall tool) testable without waiting
        /// nine months. Falls back to forging a record for their youngest living child together.</summary>
        internal void ForgeBirthChronicleFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                if (!BirthsOn)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The birth chronicle is turned off in the mod options.", SealGrey));
                    return;
                }

                var existing = _birthLedger!.ChildrenOf(npc.StringId).LastOrDefault();
                if (existing != null)
                {
                    existing.BirthAccount = string.Empty;
                    existing.BirthBeatDone = false;
                    existing.BirthAttempts = 0;
                    existing.DaySealed = false;
                    _birthLedger.Save(existing);
                    NotifyBirth($"the chronicler takes up the pen again for {existing.ChildNames()}…");
                    BeginBirthHour(existing, GatherBirthFacts(npc, Hero.MainHero, existing));
                    return;
                }

                // No record yet: forge one from a living child these two truly share.
                var player = Hero.MainHero;
                var child = Safe(() => npc.Children?.FirstOrDefault(c => c != null && c.IsAlive
                                        && (c.Father == player || c.Mother == player)), null);
                if (child == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{npc.Name} has no child of yours to set down.", SealGrey));
                    return;
                }

                bool playerIsMother = Safe(() => child.Mother == player, false);
                var mother = playerIsMother ? player! : npc;
                var record = CaptureBirth(mother, playerIsMother ? npc : player,
                    new List<Hero> { child }, 0, playerIsMother);
                _birthLedger.Save(record);
                NotifyBirth($"the chronicler takes up the pen for {record.ChildNames()}…");
                BeginBirthHour(record, GatherBirthFacts(mother, playerIsMother ? npc : player, record));
                MainThreadDispatcher.Enqueue(() => TryOfferTheFeast(record));
            }
            catch (Exception ex) { ModLog.Error("forging the birth chronicle", ex); }
        }

        internal static void DevForgeBirth(Hero npc)
        {
            try { if (npc != null) Current?.ForgeBirthChronicleFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: forging the birth chronicle", ex); }
        }
    }
}
