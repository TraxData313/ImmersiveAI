using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using ImmersiveAI.Core.Handmaids;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ImmersiveAI
{
    /// <summary>
    /// THE CAPTIVE IN THE DEN (2026.11.11, Toni's design). After the player clears a bandit
    /// hideout his men find a captive girl: the men's report (a popup), then a short SCRIPTED
    /// talk with her (vanilla dialog lines, no LLM). Taken in, she joins the player's clan as
    /// FAMILY — occupation Lord, in his party, never AddCompanionAction, so no companion slot.
    /// Sent away, she is removed for good the way vanilla removes a quest girl who should simply
    /// vanish (<c>KillCharacterAction.ApplyByRemove</c>, no notification).
    ///
    /// The hook is vanilla's own RescueFamilyQuestBehavior shape: a flag at
    /// <c>OnHideoutBattleCompletedEvent</c> when the player's side won, and the act later, once the
    /// player is back in the map state with no battle, encounter fight, conversation or popup up.
    /// The act is polled from the application tick because the campaign tick does not run while
    /// the hideout's menu holds time still.
    ///
    /// Once per campaign (persisted). With DevMode on it fires at every hideout cleared, with a
    /// pick list of the girl first; a DevMode dialog lever fires it without a hideout.
    ///
    /// THE MARKER later steps read: <see cref="HandmaidOf"/> / <see cref="IsHandmaid"/>, backed by
    /// the save (SyncData "ImmersiveAI_Handmaids", entries "heroStringId|girlId").
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private const string HmKneelVar = "IMMERSIVEAI_HM_KNEEL";
        private const string HmStoryVar = "IMMERSIVEAI_HM_STORY";
        private const string HmPleaVar = "IMMERSIVEAI_HM_PLEA";
        private const string HmSkillVar = "IMMERSIVEAI_HM_SKILL";
        private const string HmTeacherVar = "IMMERSIVEAI_HM_TEACHER";
        private const string HmAcceptedVar = "IMMERSIVEAI_HM_ACCEPTED";
        private const string HmRefusedVar = "IMMERSIVEAI_HM_REFUSED";

        // Frames the moment must stay calm before the popup shows (the hideout menu settles first).
        private const int HandmaidSafeFramesNeeded = 20;

        // A meeting whose conversation never opened is given up after this many frames.
        private const int HandmaidMeetingOpenFrames = 600;

        // ── Saved ──
        private bool _handmaidEventDone;
        // One-time heal for girls made before 2026.11.11's age fix (see GiveHandmaidHerAge).
        private bool _handmaidAgesHealed;
        private List<string> _handmaidMarks = new List<string>();

        // ── Session only ──
        private string? _handmaidPendingDen;   // bandit culture id; "" = the dev lever, no den
        private bool _handmaidPendingDev;
        private HandmaidGirl? _handmaidPendingGirl; // already chosen: straight to the report
        private int _handmaidSafeFrames;
        private bool _handmaidPromptUp;
        private HandmaidMeeting? _handmaidMeeting;

        private enum HandmaidOutcome { None, Accepted, Refused }

        private sealed class HandmaidMeeting
        {
            public HandmaidMeeting(Hero hero, HandmaidGirl girl) { Hero = hero; Girl = girl; }
            public Hero Hero { get; }
            public HandmaidGirl Girl { get; }
            public HandmaidOutcome Outcome { get; set; }
            public bool Started { get; set; }
            public bool Resolved { get; set; }
            public int FramesWaiting { get; set; }
        }

        // ------------------------------ the marker ------------------------------

        /// <summary>The roster girl this hero is, when she was taken in as a handmaid; else null.</summary>
        internal static HandmaidGirl? HandmaidOf(Hero? hero)
        {
            try
            {
                var marks = Current?._handmaidMarks;
                if (hero == null || marks == null) return null;
                var prefix = hero.StringId + "|";
                var mark = marks.FirstOrDefault(m => m != null && m.StartsWith(prefix, StringComparison.Ordinal));
                return mark == null ? null : HandmaidRoster.ById(mark.Substring(prefix.Length));
            }
            catch { return null; }
        }

        /// <summary>Whether this hero was taken in from a den as a handmaid.</summary>
        internal static bool IsHandmaid(Hero? hero) => HandmaidOf(hero) != null;

        private void RecordHandmaid(Hero hero, HandmaidGirl girl)
        {
            if (_handmaidMarks == null) _handmaidMarks = new List<string>();
            var prefix = hero.StringId + "|";
            _handmaidMarks.RemoveAll(m => m == null || m.StartsWith(prefix, StringComparison.Ordinal));
            _handmaidMarks.Add(prefix + girl.Id);
        }

        private void SyncHandmaidData(IDataStore dataStore)
        {
            dataStore.SyncData("ImmersiveAI_HandmaidEventDone", ref _handmaidEventDone);
            dataStore.SyncData("ImmersiveAI_HandmaidAgesHealed", ref _handmaidAgesHealed);
            dataStore.SyncData("ImmersiveAI_Handmaids", ref _handmaidMarks);
            if (_handmaidMarks == null) _handmaidMarks = new List<string>();
        }

        // ------------------------------ her own sheet ------------------------------

        /// <summary>Her quiet-spirit section for the sheet, wed or unwed by the game's own marriage;
        /// empty for anyone who is not a handmaid.</summary>
        private static string HandmaidSpiritFor(Hero? npc)
        {
            try
            {
                var girl = HandmaidOf(npc);
                if (girl == null || npc == null) return string.Empty;
                var player = Hero.MainHero;
                bool wed = player != null && Personas.FamilyBuilder.AreWed(npc, player);
                return HandmaidText.SpiritFor(girl, player?.Name?.ToString(), wed);
            }
            catch { return string.Empty; }
        }

        /// <summary>Seeds a handmaid's empty deep memory with her own story and her teacher's
        /// words, on the same rails as every story seed (in memory only, SeededFromStory). Called
        /// from SeedMemoryFromStory after its own guards; true when she was seeded. The marker is
        /// recorded before her first silent beat loads her memory, so that beat is saved together
        /// with this seed and the world's paragraph never reaches her.</summary>
        private static bool SeedHandmaidMemory(Hero npc, Core.Memory.NpcMemory memory)
        {
            var girl = HandmaidOf(npc);
            if (girl == null) return false;
            var seed = HandmaidText.MemorySeedFor(girl);
            if (string.IsNullOrWhiteSpace(seed)) return false;
            memory.Summary = seed;
            memory.SeededFromStory = true;
            return true;
        }

        /// <summary>The length guard: a handmaid's SPOKEN words cut back to the last whole sentence
        /// within <see cref="ModConfig.HandmaidReplyWords"/> (0 = off). Applied before the words are
        /// shown and recorded, so her memory holds what she truly said. Never letters, memory
        /// writing or the chronicles: only the spoken call sites pass through here.</summary>
        private string GuardHandmaidWords(Hero? npc, string words)
        {
            try
            {
                int budget = _config?.HandmaidReplyWords ?? 0;
                if (budget <= 0 || string.IsNullOrEmpty(words) || !IsHandmaid(npc)) return words;
                var trimmed = Core.Text.ReplyTrim.ToWordBudget(words, budget);
                if (!ReferenceEquals(trimmed, words) && trimmed != words)
                    ModLog.Info($"handmaid: {npc?.StringId} spoke {Core.Text.ReplyTrim.CountWords(Core.Voices.VoiceLine.Strip(words))} words; kept {Core.Text.ReplyTrim.CountWords(Core.Voices.VoiceLine.Strip(trimmed))} (budget {budget}).");
                return trimmed;
            }
            catch { return words; }
        }

        // ------------------------------ her road ------------------------------

        /// <summary>
        /// She already loves the man who freed her, so her road starts at WARMTH (2026.11.11): at
        /// the moment she is taken in, and for a handmaid already in a save without a stage, at
        /// load and at the next talk. Marked seeded, so the one-time story seeding never makes an
        /// LLM call for her. Mirrors the vanilla romance too, which keeps vanilla's marriage offers
        /// for the player's clan members away from her. True when she was seeded now.
        /// </summary>
        private bool SeedHandmaidRoad(Hero? hero, bool announce = false)
        {
            try
            {
                if (hero == null || !hero.IsAlive || !IsHandmaid(hero)) return false;
                if (_config == null || !_config.EnableConversationMarriage) return false;
                if (hero.Spouse != null) return false;
                var memory = LoadMemory(hero);
                if (memory.CourtshipStage != Core.Courtship.CourtshipStage.None)
                {
                    if (!memory.CourtshipSeeded) { memory.CourtshipSeeded = true; SaveMemory(hero, memory); }
                    return false;
                }
                memory.CourtshipStage = Core.Courtship.CourtshipStage.Warmth;
                memory.CourtshipSeeded = true;
                memory.CourtshipStepDay = CampaignTime.Now.ToDays;
                SaveMemory(hero, memory);
                MirrorRomance(hero, Core.Courtship.CourtshipStage.Warmth);
                if (announce) NotifyRoadStep(hero, Core.Courtship.CourtshipStage.Warmth, forward: true, seeded: true, memory: memory);
                ModLog.Info($"handmaid: {hero.StringId}'s road begins at warmth.");
                return true;
            }
            catch (Exception ex) { ModLog.Error("handmaid: seeding her road", ex); return false; }
        }

        /// <summary>At load: every handmaid of this save who has no stage yet begins at warmth.</summary>
        private void SeedHandmaidRoadsAtLoad()
        {
            try
            {
                foreach (var mark in (_handmaidMarks ?? new List<string>()).ToList())
                {
                    var id = mark?.Split('|')[0];
                    if (string.IsNullOrEmpty(id)) continue;
                    var hero = FindAliveHero(id!);
                    SeedHandmaidRoad(hero);
                    // A girl made before the age fix came out at her wanderer template's age
                    // (Niya was 27). Once per save, put her back at her own age.
                    var girl = HandmaidOf(hero);
                    if (!_handmaidAgesHealed && hero != null && girl != null
                        && hero.Age > HandmaidText.AgeOf(girl) + 1.5f)
                        GiveHandmaidHerAge(hero, girl);
                }
                _handmaidAgesHealed = true;
            }
            catch (Exception ex) { ModLog.Error("handmaid: seeding the roads at load", ex); }
        }

        // Vanilla IGNORES the age handed to CreateSpecialHero for a wanderer template
        // (DefaultHeroCreationModel.GetBirthAndDeathDay: template age + up to 4 years), so her
        // birthday is set again after she is made.
        private static void GiveHandmaidHerAge(Hero hero, HandmaidGirl girl)
        {
            try
            {
                int age = HandmaidText.AgeOf(girl);
                hero.SetBirthDay(HeroHelper.GetRandomBirthDayForAge(age));
                ModLog.Info($"handmaid: {hero.StringId} is {age} now.");
            }
            catch (Exception ex) { ModLog.Error("handmaid: setting her age", ex); }
        }

        private bool IsHandmaidMeetingHero(Hero? hero) =>
            hero != null && _handmaidMeeting != null && _handmaidMeeting.Hero == hero;

        // ------------------------------ the hook ------------------------------

        private void RegisterHandmaidEvents()
        {
            CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnHideoutBattleCompletedForHandmaid);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEndedForHandmaid);
        }

        private void OnHideoutBattleCompletedForHandmaid(BattleSideEnum winnerSide,
            HideoutEventComponent component, HideoutEventComponent.HideoutBattleEndState endState)
        {
            try
            {
                if (!_config.EnableHideoutHandmaid) return;
                if (endState != HideoutEventComponent.HideoutBattleEndState.Victory
                    && endState != HideoutEventComponent.HideoutBattleEndState.SendTroops) return;
                var den = component?.MapEvent?.MapEventSettlement;
                if (den == null) return;
                var attacker = den.LastAttackerParty;
                if (attacker == null || !attacker.IsMainParty) return;

                bool dev = _config.DevMode;
                if (_handmaidEventDone && !dev) return;

                _handmaidPendingDen = den.Culture?.StringId ?? string.Empty;
                _handmaidPendingDev = dev;
                _handmaidPendingGirl = null;
                _handmaidSafeFrames = 0;
                ModLog.Info($"handmaid: hideout {den.StringId} ({_handmaidPendingDen}) cleared; the meeting waits for a calm moment{(dev ? " (dev)" : "")}.");
            }
            catch (Exception ex) { ModLog.Error("handmaid: noting the hideout win", ex); }
        }

        /// <summary>The DevMode lever: the whole meeting without a hideout, as soon as it is calm.</summary>
        private void OnDebugCallHandmaid()
        {
            _handmaidPendingDen = string.Empty;
            _handmaidPendingDev = true;
            _handmaidPendingGirl = null;
            _handmaidSafeFrames = 0;
            ModLog.Info("handmaid: dev lever pulled; the meeting waits for a calm moment.");
        }

        /// <summary>Driven from SubModule.OnApplicationTick: the campaign tick sleeps in menus.</summary>
        internal static void HandmaidTick()
        {
            var me = Current;
            if (me == null) return;
            try { me.TickHandmaid(); }
            catch (Exception ex)
            {
                ModLog.Error("handmaid: tick", ex);
                me._handmaidPendingDen = null;
                me._handmaidPendingGirl = null;
                me._handmaidPromptUp = false;
            }
        }

        private void TickHandmaid()
        {
            var meeting = _handmaidMeeting;
            if (meeting != null)
            {
                // A meeting whose talk never opened must not wedge the event (or leave her standing).
                if (!meeting.Started && !meeting.Resolved && ++meeting.FramesWaiting > HandmaidMeetingOpenFrames
                    && !(Campaign.Current?.ConversationManager?.IsConversationInProgress ?? false))
                {
                    ModLog.Warn("handmaid: the meeting's talk never opened; she is let go.");
                    meeting.Resolved = true;
                    ResolveHandmaidMeeting(meeting);
                }
                return;
            }

            if (_handmaidPendingDen == null || _handmaidPromptUp) { _handmaidSafeFrames = 0; return; }
            if (_config == null || !_config.EnableHideoutHandmaid) { _handmaidPendingDen = null; return; }
            if (!HandmaidMomentIsSafe()) { _handmaidSafeFrames = 0; return; }
            if (++_handmaidSafeFrames < HandmaidSafeFramesNeeded) return;

            var den = _handmaidPendingDen;
            bool dev = _handmaidPendingDev;
            var chosen = _handmaidPendingGirl;
            _handmaidPendingDen = null;
            _handmaidPendingGirl = null;
            _handmaidSafeFrames = 0;
            if (chosen != null) ShowHandmaidPopup(chosen);
            else FireHandmaidEvent(den, dev);
        }

        // On the map, not a prisoner, no mission, no battle, no field encounter, no talk, no popup.
        // A settlement encounter with no battle (the cleared hideout's own menu, a town) is fine —
        // that is exactly where vanilla's RescueFamily quest opens its own talk.
        private bool HandmaidMomentIsSafe()
        {
            if (InitiationBlockReason().Length > 0) return false;
            if (MapEvent.PlayerMapEvent != null) return false;
            if (_pendingInitiation) return false;
            if (UI.TalkUI.IsOpen || UI.NightWindow.NightWindowManager.IsOpen) return false;
            if (InformationManager.IsAnyInquiryActive()) return false;
            return true;
        }

        // ------------------------------ the popups ------------------------------

        private void FireHandmaidEvent(string den, bool dev)
        {
            try
            {
                if (!dev && _handmaidEventDone) return;
                if (dev) ShowHandmaidPickList(den);
                else ShowHandmaidPopup(HandmaidRoster.ForBanditCulture(den));
            }
            catch (Exception ex)
            {
                _handmaidPromptUp = false;
                ModLog.Error("handmaid: firing the event", ex);
            }
        }

        private void ShowHandmaidPickList(string den)
        {
            var own = HandmaidRoster.OwnOf(den);
            var elements = HandmaidRoster.PickOrder(den)
                .Select(g => new InquiryElement(g, HandmaidText.PickLabel(g, g == own), null, true, null))
                .ToList();

            _handmaidPromptUp = true;
            var data = new MultiSelectionInquiryData(
                HandmaidText.DevPickTitle, HandmaidText.DevPickBody,
                elements, true, 1, 1,
                HandmaidText.DevPickAffirm, HandmaidText.DevPickCancel,
                chosen =>
                {
                    try
                    {
                        _handmaidPromptUp = false;
                        if (!(chosen?.FirstOrDefault()?.Identifier is HandmaidGirl girl)) return;
                        // A new inquiry never opens inside the closing one's own callback.
                        MainThreadDispatcher.Enqueue(() => ShowHandmaidPopup(girl));
                    }
                    catch (Exception ex) { ModLog.Error("handmaid: choosing the girl", ex); }
                },
                _ => { _handmaidPromptUp = false; });
            MBInformationManager.ShowMultiSelectionInquiry(data, true);
        }

        private void ShowHandmaidPopup(HandmaidGirl girl)
        {
            try
            {
                _handmaidPromptUp = true;
                InformationManager.ShowInquiry(new InquiryData(
                    HandmaidText.PopupTitle,
                    HandmaidText.PopupBody,
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: HandmaidText.PopupBringHer,
                    negativeText: HandmaidText.PopupLetHerGo,
                    affirmativeAction: () =>
                    {
                        _handmaidPromptUp = false;
                        MainThreadDispatcher.Enqueue(() => BeginHandmaidMeeting(girl));
                    },
                    negativeAction: () =>
                    {
                        _handmaidPromptUp = false;
                        _handmaidEventDone = true;
                        InformationManager.DisplayMessage(new InformationMessage(HandmaidText.LetGoNotice, ActivityColor));
                        ModLog.Info($"handmaid: {girl.Id} let go at the popup; the event is done.");
                    }), pauseGameActiveState: true);
            }
            catch (Exception ex)
            {
                _handmaidPromptUp = false;
                ModLog.Error("handmaid: the men's report", ex);
            }
        }

        // ------------------------------ the meeting ------------------------------

        // She is made only now, CLANLESS (as vanilla's own companion spawn makes a wanderer), hidden
        // from the encyclopedia, and joins the clan only on his word. A refusal removes a hero who
        // never belonged to anyone, which is the cleanest removal there is.
        private void BeginHandmaidMeeting(HandmaidGirl girl)
        {
            Hero? hero = null;
            try
            {
                if (!HandmaidMomentIsSafe())
                {
                    ModLog.Warn("handmaid: the moment turned unsafe before the meeting; it waits again.");
                    _handmaidPendingDen = girl.BanditCulture;
                    _handmaidPendingGirl = girl;
                    _handmaidSafeFrames = 0;
                    return;
                }

                var template = FindHandmaidTemplate(girl);
                if (template == null)
                {
                    ModLog.Warn($"handmaid: no wanderer template for {girl.Id} ({girl.TemplateId}); the meeting is cancelled.");
                    return;
                }

                hero = HeroCreator.CreateSpecialHero(template, NearestTownOfCulture(girl.Culture), null, null, HandmaidText.AgeOf(girl));
                var name = new TextObject(girl.Name);
                hero.SetName(name, name);
                GiveHandmaidHerAge(hero, girl);
                ApplyHandmaidFace(hero, girl);
                hero.HiddenInEncyclopedia = true;

                var meeting = new HandmaidMeeting(hero, girl);
                _handmaidMeeting = meeting;

                MBTextManager.SetTextVariable(HmKneelVar, HandmaidText.KneelLine(), false);
                MBTextManager.SetTextVariable(HmStoryVar, HandmaidText.StoryLine(girl), false);
                MBTextManager.SetTextVariable(HmPleaVar, HandmaidText.PleaLine(), false);
                MBTextManager.SetTextVariable(HmSkillVar, HandmaidText.SkillLine(girl), false);
                MBTextManager.SetTextVariable(HmTeacherVar, HandmaidText.TeacherLine(girl), false);
                MBTextManager.SetTextVariable(HmAcceptedVar, HandmaidText.AcceptedLine(), false);
                MBTextManager.SetTextVariable(HmRefusedVar, HandmaidText.RefusedLine(), false);

                ModLog.Info($"handmaid: {girl.Id} made as {hero.StringId}; opening the meeting.");
                OpenConversationWith(hero);
            }
            catch (Exception ex)
            {
                ModLog.Error("handmaid: beginning the meeting", ex);
                _handmaidMeeting = null;
                RemoveHandmaidHero(hero);
            }
        }

        private static CharacterObject? FindHandmaidTemplate(HandmaidGirl girl)
        {
            try
            {
                var t = MBObjectManager.Instance?.GetObject<CharacterObject>(girl.TemplateId);
                if (t != null) return t;
            }
            catch { }
            try
            {
                return CharacterHelper.GetRandomCompanionTemplateWithPredicate(
                    x => x.IsFemale && x.Culture?.StringId == girl.Culture);
            }
            catch { return null; }
        }

        private static Settlement? NearestTownOfCulture(string culture)
        {
            try
            {
                var party = MobileParty.MainParty;
                Settlement? best = null;
                float bestDist = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsTown || s.Culture?.StringId != culture) continue;
                    var d = party != null ? s.Position.DistanceSquared(party.Position) : 0f;
                    if (best == null || d < bestDist) { bestDist = d; best = s; }
                }
                return best;
            }
            catch { return null; }
        }

        // A face code the player supplies later; a bad one leaves the random face, never throws.
        private static void ApplyHandmaidFace(Hero hero, HandmaidGirl girl)
        {
            if (string.IsNullOrWhiteSpace(girl.FaceCode)) return;
            try
            {
                if (!HandmaidText.LooksLikeFaceCode(girl.FaceCode)
                    || !BodyProperties.FromString(girl.FaceCode.Trim(), out var body))
                {
                    ModLog.Warn($"handmaid: {girl.Id}'s face code was not read; she keeps a random face.");
                    return;
                }
                hero.StaticBodyProperties = body.StaticProperties;
                hero.Weight = body.Weight;
                hero.Build = body.Build;
            }
            catch (Exception ex) { ModLog.Error($"handmaid: applying {girl.Id}'s face (random face kept)", ex); }
        }

        private void AddHandmaidDialogs(CampaignGameStarter starter)
        {
            // Gated on this one meeting only, and above the forced-initiation lines (200), so the
            // vanilla greeting, "Speak freely" and the recap never see this talk.
            starter.AddDialogLine("immersiveai_hm_kneel", "start", "immersiveai_hm_story",
                "{=!}{" + HmKneelVar + "}", IsHandmaidTalk, OnHandmaidTalkStarted, 210);
            starter.AddDialogLine("immersiveai_hm_story", "immersiveai_hm_story", "immersiveai_hm_plea",
                "{=!}{" + HmStoryVar + "}", IsHandmaidTalk, null, 200);
            starter.AddDialogLine("immersiveai_hm_plea", "immersiveai_hm_plea", "immersiveai_hm_options",
                "{=!}{" + HmPleaVar + "}", IsHandmaidTalk, null, 200);

            starter.AddPlayerLine("immersiveai_hm_accept", "immersiveai_hm_options", "immersiveai_hm_accepted",
                "{=ImmersiveAI_HmAccept}" + HandmaidText.PlayerAccept, IsHandmaidTalk,
                () => SetHandmaidOutcome(HandmaidOutcome.Accepted), 200);
            starter.AddPlayerLine("immersiveai_hm_ask_skill", "immersiveai_hm_options", "immersiveai_hm_skill",
                "{=ImmersiveAI_HmAskSkill}" + HandmaidText.PlayerAskSkill, IsHandmaidTalk, null, 190);
            starter.AddPlayerLine("immersiveai_hm_ask_teacher", "immersiveai_hm_options", "immersiveai_hm_teacher",
                "{=ImmersiveAI_HmAskTeacher}" + HandmaidText.PlayerAskTeacher, IsHandmaidTalk, null, 180);
            starter.AddPlayerLine("immersiveai_hm_refuse", "immersiveai_hm_options", "immersiveai_hm_refused",
                "{=ImmersiveAI_HmRefuse}" + HandmaidText.PlayerRefuse, IsHandmaidTalk,
                () => SetHandmaidOutcome(HandmaidOutcome.Refused), 170);

            starter.AddDialogLine("immersiveai_hm_skill_line", "immersiveai_hm_skill", "immersiveai_hm_options",
                "{=!}{" + HmSkillVar + "}", IsHandmaidTalk, null, 200);
            starter.AddDialogLine("immersiveai_hm_teacher_line", "immersiveai_hm_teacher", "immersiveai_hm_options",
                "{=!}{" + HmTeacherVar + "}", IsHandmaidTalk, null, 200);
            starter.AddDialogLine("immersiveai_hm_accepted_line", "immersiveai_hm_accepted", "close_window",
                "{=!}{" + HmAcceptedVar + "}", IsHandmaidTalk, RequestLeaveFromPartyEncounter, 200);
            starter.AddDialogLine("immersiveai_hm_refused_line", "immersiveai_hm_refused", "close_window",
                "{=!}{" + HmRefusedVar + "}", IsHandmaidTalk, RequestLeaveFromPartyEncounter, 200);
        }

        private bool IsHandmaidTalk()
        {
            var m = _handmaidMeeting;
            return m != null && !m.Resolved && Hero.OneToOneConversationHero == m.Hero;
        }

        private void OnHandmaidTalkStarted()
        {
            if (_handmaidMeeting != null) _handmaidMeeting.Started = true;
        }

        private void SetHandmaidOutcome(HandmaidOutcome outcome)
        {
            if (_handmaidMeeting != null) _handmaidMeeting.Outcome = outcome;
        }

        private void OnConversationEndedForHandmaid(IEnumerable<CharacterObject> characters)
        {
            try
            {
                var m = _handmaidMeeting;
                if (m == null || m.Resolved) return;
                if (characters == null || !characters.Any(c => c?.HeroObject == m.Hero)) return;
                m.Resolved = true;
                // Next tick: the conversation must finish unwinding before she joins or leaves.
                MainThreadDispatcher.Enqueue(() => ResolveHandmaidMeeting(m));
            }
            catch (Exception ex) { ModLog.Error("handmaid: the end of the meeting", ex); }
        }

        private void ResolveHandmaidMeeting(HandmaidMeeting m)
        {
            try
            {
                if (m.Outcome == HandmaidOutcome.Accepted)
                {
                    JoinHandmaidToFamily(m);
                    return;
                }

                // Refused at the end of the talk, or a talk that ended any other way: she goes.
                var player = Hero.MainHero;
                if (m.Outcome == HandmaidOutcome.Refused && player != null && player.Gold >= HandmaidText.RoadSilver)
                    GiveGoldAction.ApplyBetweenCharacters(player, m.Hero, HandmaidText.RoadSilver);
                RemoveHandmaidHero(m.Hero);
                _handmaidEventDone = true;
                ModLog.Info($"handmaid: {m.Girl.Id} sent away ({m.Outcome}); the event is done.");
            }
            catch (Exception ex)
            {
                ModLog.Error("handmaid: resolving the meeting", ex);
                // Never leave a half-made clanless girl standing in the world.
                if (m.Hero.Clan != Clan.PlayerClan) RemoveHandmaidHero(m.Hero);
            }
            finally
            {
                if (_handmaidMeeting == m) _handmaidMeeting = null;
            }
        }

        // Family, not a companion: occupation Lord, the player's clan, his party. Never
        // AddCompanionAction — that sets CompanionOf, which is what counts against the limit.
        private void JoinHandmaidToFamily(HandmaidMeeting m)
        {
            var hero = m.Hero;
            var player = Hero.MainHero;

            hero.SetNewOccupation(Occupation.Lord);
            hero.Clan = Clan.PlayerClan;
            hero.HiddenInEncyclopedia = false;
            if (hero.HeroState != Hero.CharacterStates.Active)
                hero.ChangeState(Hero.CharacterStates.Active);
            AddHeroToPartyAction.Apply(hero, MobileParty.MainParty, showNotification: false);
            try { hero.UpdateHomeSettlement(); } catch { }

            RecordHandmaid(hero, m.Girl);
            _handmaidEventDone = true;

            try { if (player != null) CharacterRelationManager.SetHeroRelation(player, hero, HandmaidText.RelationOnJoin); }
            catch (Exception ex) { ModLog.Error("handmaid: setting her regard", ex); }

            // One silent first-person beat, no LLM call.
            AppendRecordedTurn(hero, HandmaidText.JoinMemoryFor(player?.Name?.ToString()), string.Empty,
                OutreachMark.PlayerEngaged);

            // She already loves him: her road begins at warmth.
            SeedHandmaidRoad(hero, announce: true);

            InformationManager.DisplayMessage(new InformationMessage(
                HandmaidText.JoinedNoticeFor(hero.Name?.ToString()), ActivityColor));
            ModLog.Info($"handmaid: {m.Girl.Id} ({hero.StringId}) joined the player's family.");
        }

        // Vanilla's own removal for a quest-made girl who should vanish without a death notice
        // (NotableWantsDaughterFoundIssueBehavior: ApplyByRemove(_daughterHero, false, true)).
        private static void RemoveHandmaidHero(Hero? hero)
        {
            try
            {
                if (hero != null && hero.IsAlive)
                    KillCharacterAction.ApplyByRemove(hero, false, true);
            }
            catch (Exception ex) { ModLog.Error("handmaid: removing her", ex); }
        }
    }
}
