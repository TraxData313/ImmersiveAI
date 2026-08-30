using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Nights;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Core.Together;
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
    /// THE NIGHTS (2026.08.09, Anton's design — the wedding's own morning after). The game used to
    /// flip a coin every day behind the player's back and hand him a popup when it came up heads.
    /// Now the nights of a marriage are his to spend: each evening he is asked where he will sleep,
    /// a woman's own season decides what may come of it, and a night he pays for is written down —
    /// three or four sentences in the register of the wedding night, with a name he and she will
    /// both keep it by.
    ///
    /// THE FOUR RULES THIS FILE EXISTS TO HOLD:
    /// • CONCEPTION IS THE PLAYER'S DOING. Vanilla's nightly roll is skipped for his own marriages
    ///   (<see cref="Nights.PregnancyPatch"/>) and re-cast on the night he chose, weighted by
    ///   <see cref="MoodTides.Fertility"/>. If the patch could not be applied, we do NOT roll at all
    ///   — two systems each reaching for the same child is worse than either alone.
    /// • THE ODDS ARE NOT OURS. They come from the game's own PregnancyModel and are merely spread
    ///   across her fertile nights, so taking the season is worth about what the world would have
    ///   given anyway (<see cref="NightOdds"/>).
    /// • HER DOOR IS HERS. Through the days of the custom the choice is not offered at all, and the
    ///   evening simply says so.
    /// • SHE SEES WHAT SHE WOULD SEE. Every wife keeps her own fortnight — the nights he came, the
    ///   nights her door was closed, and whatever she came to learn of the rest. We never script a
    ///   feeling about any of it; she has had her own hand on her heart since the day move_heart
    ///   shipped, and this only gives her something true to weigh.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private NightLedger? _nightLedger;

        // Nights whose account is being written RIGHT NOW (by record id) — a story job is long, and
        // neither the hourly retry nor a dev click may start a second one over it.
        private readonly HashSet<string> _nightsWriting = new HashSet<string>(StringComparer.Ordinal);

        // The campaign day whose evening question has already been put, so a re-entered hour (a save
        // loaded at dusk) never asks twice. Not persisted: the ledger's own night-per-day rule is
        // the durable guard, this is only the polite one.
        private int _nightAskedOnDay = int.MinValue;

        // "Don't ask me for a while" — the player's own hand on the evening popup.
        private double _dontAskNightsUntilDay = -1;

        // The one-time honest word when conception could not be taken over from the world.
        private bool _nightPatchWarned;

        private static readonly Color NightColor = new Color(0.72f, 0.70f, 0.92f, 1f);
        private static readonly Color QuickenedColor = new Color(0.45f, 0.85f, 0.45f, 1f);
        private static readonly Color BarrenColor = new Color(0.80f, 0.55f, 0.55f, 1f);

        private bool NightsOn => _config.EnableNights && _nightLedger != null;

        private void LoadNightLedger()
        {
            try { _nightLedger = NightLedger.LoadFrom(NpcPaths.NightsFile); }
            catch { _nightLedger = null; }
        }

        private void SaveNightLedger()
        {
            try { _nightLedger?.SaveTo(NpcPaths.NightsFile); }
            catch (Exception ex) { ModLog.Error("saving the book of nights", ex); }
        }

        // ------------------------------ who is of this hearth ------------------------------

        /// <summary>The player's living wives (or husbands), by the polygamy-safe reckoning — a
        /// second wife lives in ExSpouses while both are alive, so the bare Spouse slot is never
        /// the truth.</summary>
        private static List<Hero> SpousesOfPlayer()
        {
            try
            {
                var player = Hero.MainHero;
                if (player == null) return new List<Hero>();
                return FamilyBuilder.SpousesOf(player)
                    .Where(h => h != null && h.IsAlive && !h.IsPrisoner && h != player)
                    .ToList();
            }
            catch { return new List<Hero>(); }
        }

        /// <summary>
        /// EVERY WOMAN OF THE HEARTH (2026.08.15) — his wives, and since the road forked, his
        /// lovers too. The whole batch depends on this one list being complete: a lover who was not
        /// in it would have no nights, so nothing to leak, so nothing for a wife to hear about, and
        /// the entire consequence engine would be reasoning over half a household.
        ///
        /// Wives first, deliberately. The first wife is the official, ceremonial one and the dusk
        /// question should read that way; the sort within each group is the season's own.
        /// </summary>
        /// <summary>The same roster, for the hearth side of the talk screen. Internal rather than
        /// public: the screen is the only outside caller, and everything else must keep asking the
        /// nights themselves.</summary>
        internal static List<Hero> HearthRoster() => WomenOfTheHearth();

        private static List<Hero> WomenOfTheHearth()
        {
            var women = new List<Hero>();
            try
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var wife in SpousesOfPlayer())
                    if (wife != null && seen.Add(wife.StringId)) women.Add(wife);
                foreach (var lover in LoversOfPlayer())
                    if (lover != null && lover.IsAlive && !lover.IsPrisoner && seen.Add(lover.StringId))
                        women.Add(lover);
            }
            catch { }
            return women;
        }

        /// <summary>Which of the two could carry a child. Null when neither can, and the night is
        /// then simply a night — which is the honest answer, not an error.
        ///
        /// A CHILD NEEDS ONE OF EACH, and that test is the whole of it (2026.08.30, Pip0XnXx on
        /// Steam). The old shape asked only "is anybody here a woman?" and answered with the first
        /// one it found — so a woman player with a woman lover was handed a conception fathered by
        /// herself, and the game builds a male newborn out of the father's own CharacterObject.
        /// Same-sex is not modelled here (see the design record); the honest answer for two women
        /// or two men is that the night was a night.</summary>
        private static Hero? MotherOf(Hero partner)
        {
            try
            {
                var player = Hero.MainHero;
                if (partner == null || player == null) return null;
                if (partner.IsFemale == player.IsFemale) return null;
                return partner.IsFemale ? partner : player;
            }
            catch { return null; }
        }

        /// <summary>And whose child it is: for a man, his own; for a woman, the man who lay with
        /// her. Vanilla holds the father for thirty-six days and then reads him at the delivery,
        /// so naming him wrongly here is not felt until a month later — and naming him NOT AT ALL
        /// is a hard crash (<c>HeroCreator.DeliverOffSpring</c> touches <c>father.CharacterObject</c>
        /// before anything else).</summary>
        private static Hero? FatherOf(Hero mother, Hero partner)
        {
            try
            {
                var player = Hero.MainHero;
                if (mother == null || player == null) return null;
                var father = mother == player ? partner : player;
                if (father == null || father == mother || father.IsFemale) return null;
                return father;
            }
            catch { return null; }
        }

        /// <summary>
        /// THE RESCUE'S QUESTION (2026.08.30): this woman is delivering TODAY and her pregnancy
        /// names no father, which the game cannot survive. Who should it have been?
        ///
        /// Answered only where it can be answered honestly — a father invented here would be
        /// permanent lineage in her children's blood. Null means we stand aside and the game does
        /// whatever it would have done without us.
        /// </summary>
        internal static Hero? FatherForLostChild(Hero mother)
        {
            try
            {
                var self = Current;
                var player = Hero.MainHero;
                if (mother == null || player == null) return null;

                // Whoever the world already believes she is with — the plainest answer there is.
                var spouse = Safe(() => mother.Spouse, null);
                if (spouse != null && spouse != mother && !spouse.IsFemale) return spouse;

                if (mother == player)
                {
                    if (!player.IsFemale || self == null) return null;

                    // Her own bed, in her own ledger: the night that took, else the last night at all.
                    var record = self._nightLedger?.Nights
                            .Where(n => n != null && n.Conceived)
                            .OrderByDescending(n => n.GameDay).FirstOrDefault()
                        ?? self._nightLedger?.Nights
                            .Where(n => n != null && n.Kind == NightKind.Together)
                            .OrderByDescending(n => n.GameDay).FirstOrDefault();
                    var man = record == null ? null : FindHeroAnywhere(record.WifeId);
                    if (man != null && !man.IsFemale) return man;

                    // Or, failing every record: the one man she is bound to at all. One, or none —
                    // a guess between two would be exactly the invention this refuses to make.
                    var men = WomenOfTheHearth().Where(h => h != null && !h.IsFemale).ToList();
                    return men.Count == 1 ? men[0] : null;
                }

                // One of his own women, and he is the man of the house.
                if (!player.IsFemale
                    && (Safe(() => FamilyBuilder.AreWed(player, mother), false) || IsLoverOfPlayer(mother)))
                    return player;

                return null;
            }
            catch { return null; }
        }

        // Alive or dead: a father may have fallen in the thirty-six days between the night and the
        // birth, and the child still needs him named.
        private static Hero? FindHeroAnywhere(string stringId)
        {
            if (string.IsNullOrEmpty(stringId)) return null;
            var found = FindAliveHero(stringId);
            if (found != null) return found;
            try { return Hero.DeadOrDisabledHeroes?.FirstOrDefault(h => h != null && h.StringId == stringId); }
            catch { return null; }
        }

        /// <summary>The Harmony hook: whether this woman's nights are the player's own to spend, and
        /// therefore whether the world must keep its hands off her. False for the whole world, and
        /// false for us too whenever the feature is asleep — the safe answer is always "vanilla".</summary>
        internal static bool NightsOwnConceptionFor(Hero hero)
        {
            try
            {
                var self = Current;
                if (self == null || hero == null || !self.NightsOn) return false;
                if (self._config.ConceptionChanceMultiplier <= 0) return false;

                var player = Hero.MainHero;
                if (player == null) return false;
                if (hero == player) return player.IsFemale;                 // a female player's own nights
                return FamilyBuilder.AreWed(player, hero);
            }
            catch { return false; }
        }

        // ------------------------------ the evening ------------------------------

        // Rides the same hour as everything else. Three duties: the question at dusk, the day a
        // conception becomes known, and any account the chronicler still owes us.
        private void OnNightsHourlyTick()
        {
            if (!NightsOn) return;

            try { RevealDueConceptions(); }
            catch (Exception ex) { ModLog.Error("revealing a conception", ex); }

            try { RetryUnwrittenNights(); }
            catch (Exception ex) { ModLog.Error("retrying a night's account", ex); }

            try
            {
                // LATE IN THE EVENING AND NOT BEFORE (Anton, 2026.08.10). The automatic night does
                // not pounce the instant the hours are up — it waits for dusk, so the whole day
                // between belongs to the player: he may come to the window at noon and go himself,
                // with a gift and a written night, and the automatic one simply finds the clock
                // running again and stands down. A floor under the marriage, never a ceiling.
                //
                // BUT IT IS A WINDOW OF HOURS, NOT ONE HOUR (the sweep's find, 2026.08.10, and it
                // was already costing whole days in play). A single-hour gate loses the evening to
                // any passing reason: the cooldown thirty minutes short because last night was
                // answered at 21:30, a conversation open at exactly 21:00, every door closed for
                // one wife of two. Worse, the day was stamped as ASKED before any of that was
                // weighed — which is why the question reappeared after a reload and only then
                // (the field is not persisted). The stamp now goes down only when something has
                // truly been put to the player, and dusk keeps knocking for a few hours.
                //
                // AND THE CLOCK IT ALL HANGS ON IS THE SUN'S (Anton, 2026.08.15). Everything below
                // counts in NIGHT-CYCLES — late afternoon to late afternoon — rather than in
                // calendar days or in hours since the last night. Both of the paragraphs above
                // survive it untouched: the evening still asks LATE and never before, and it still
                // asks across a window of hours. What changed is only when the house becomes ready
                // again, which is now the same hour every day instead of drifting later with every
                // late night.
                if (!IsWithinEveningWindow()) return;

                int cycle = NightCycleNow();
                if (_nightAskedOnDay == cycle) return;
                if (_nightLedger!.IsCycleSettled(CampaignTime.Now.ToDays, NightResetHour)) return;

                HandleTheEvening();
            }
            catch (Exception ex) { ModLog.Error("the evening's question", ex); }
        }

        /// <summary>How many hours past <see cref="ModConfig.NightHour"/> the evening keeps asking.
        /// Long enough to outlast a cooldown that ran a little over, short enough that it is still
        /// plainly night.</summary>
        private const int EveningWindowHours = 5;

        private bool IsWithinEveningWindow()
        {
            int hour = (int)CampaignTime.Now.CurrentHourInDay;
            int start = _config.NightHour;
            for (int i = 0; i <= EveningWindowHours; i++)
                if (hour == (start + i) % 24) return true;
            return false;
        }

        /// <summary>The hour the house is ready again — the turn a night-cycle is measured from.</summary>
        private int NightResetHour => _config.NightDayResetHour;

        /// <summary>Which night-cycle this moment falls in. Every "have we already had tonight?"
        /// question in this file counts in these, not in calendar days: an hour after midnight
        /// belongs to the evening it grew out of.</summary>
        private int NightCycleNow() => NightClock.CycleOf(CampaignTime.Now.ToDays, NightResetHour);

        // Why a given wife cannot be gone to tonight. Empty means she can.
        private string NightBlockFor(Hero wife, out double hoursLeft)
        {
            var presence = PresenceBlockFor(wife, out hoursLeft);
            if (presence.Length > 0) return presence;

            // Her season, and — since 2026.08.15 — whatever she has written down about him. One
            // call, because both are the same door and the player must never be told a half-truth
            // about why it is shut. DuskLine returns the season's own wording unchanged, so
            // everything downstream that reads it still works.
            return DoorBlockFor(wife);
        }

        /// <summary>
        /// Everything that is NOT the door: whether she is alive, free, here, reachable, and
        /// whether the hours between nights have passed. Split out (2026.08.15) because "go to her
        /// anyway" goes through a shut DOOR and through nothing else — a wife three weeks away is
        /// not available to be gone to at all, and a quarrel does not suspend the clock.
        /// </summary>
        private string PresenceBlockFor(Hero wife, out double hoursLeft)
        {
            hoursLeft = 0;
            try
            {
                if (wife == null || !wife.IsAlive) return "she is beyond your reach";
                if (wife.IsPrisoner) return "she is held captive";
                if (!IsCoLocated(wife)) return "she is not with you";

                var settlement = Hero.MainHero?.CurrentSettlement ?? MobileParty.MainParty?.CurrentSettlement;
                if (settlement != null && IsBehindClosedDoors(wife, settlement)) return "you cannot reach her here";

                double cooldown = CooldownHoursLeft();
                if (cooldown > 0) { hoursLeft = cooldown; return $"not yet — {(int)Math.Ceiling(cooldown)} hours until you are ready"; }

                return string.Empty;
            }
            catch { return "not tonight"; }
        }

        // Whether tonight has already been spent — across the whole hearth, because a man cannot be
        // in two beds in one evening. Counted in CYCLES rather than in hours since (Anton,
        // 2026.08.15): what is answered is "has there been a night since the house was last ready?",
        // and what is counted back is the hours to the next turn, so the greyed line still tells the
        // player a true number to wait.
        private double CooldownHoursLeft()
        {
            try
            {
                var last = _nightLedger?.LastTogetherWithAnyone();
                if (last == null) return 0;
                double now = CampaignTime.Now.ToDays;
                if (!NightClock.SameCycle(last.GameDay, now, NightResetHour)) return 0;
                return NightClock.HoursUntilReset(now, NightResetHour);
            }
            catch { return 0; }
        }

        // TWO PLAIN SWITCHES, not four poetic modes (Anton, 2026.08.10 — the cycling
        // "Change how the evenings go" button said nothing a player could act on). Auto decides WHO
        // KNOCKS; taking care decides WHAT THE NIGHT IS FOR. They are independent, and between them
        // they cover every one of the old four:
        //   manual + not careful = you are asked at dusk and choose everything (the default)
        //   manual + careful     = you still choose, but no night is meant to make a child
        //   auto   + not careful = it looks after itself and aims for children
        //   auto   + careful     = it looks after itself and aims not to
        // Wanting no nights at all is EnableNights = false, which is where a player would look.
        private bool AutoVisit => _config.NightsAutoVisit;
        private bool PreventChild => _config.NightsPreventChild;

        private void HandleTheEvening()
        {
            var wives = WomenOfTheHearth();
            if (wives.Count == 0) return;

            // Last night, if it was never answered, was a night slept alone — and it is settled as
            // one now, a full day later, with the wives rolling for what they made of it (Anton,
            // 2026.08.09: an ignored question IS an answer, and it must not quietly vanish).
            CloseUnansweredNight();

            // Whoever can be gone to, and why the rest cannot.
            var open = new List<Hero>();
            var closedDoors = new List<Hero>();
            var shutAgainstYou = new List<Hero>();
            foreach (var wife in wives)
            {
                var block = NightBlockFor(wife, out _);
                if (block.Length == 0) open.Add(wife);
                else if (block.IndexOf("custom of women", StringComparison.Ordinal) >= 0) closedDoors.Add(wife);
                // A door shut by her own word is a different silence, and it must not be swallowed:
                // the evening still has something to say, and may still have a choice in it.
                else if (CanGoToHerAnyway(wife)) shutAgainstYou.Add(wife);
            }

            // Every door closed: no choice is offered at all, only the quiet reason (Anton's ask —
            // a popup of greyed-out names is a worse silence than none). Unless one of them is shut
            // against YOU rather than by her season, in which case the question still stands, and
            // it is the hardest one this mod asks.
            if (open.Count == 0 && shutAgainstYou.Count == 0)
            {
                if (closedDoors.Count > 0 && closedDoors.Count == wives.Count(w => IsCoLocated(w)))
                    NotifyNight(NightText.CustomDaysNotice(closedDoors.Select(w => w.Name?.ToString() ?? "she").ToList()));
                RecordTheRestOfTheNight(null);
                return;
            }
            if (open.Count == 0 && AutoVisit)
            {
                // Nothing to visit of its own accord. The automatic night NEVER walks through a
                // shut door — that choice is the player's alone to make, by hand, every time.
                RecordTheRestOfTheNight(null);
                return;
            }

            if (AutoVisit) { SpendTheNightAutomatically(open); return; }

            if (!_config.AskEachEvening) return;                       // the window's key is the only door
            if (CampaignTime.Now.ToDays < _dontAskNightsUntilDay) return;

            // THE QUESTION WAITS IN THE STACK, it does not seize the screen (Anton, 2026.08.10):
            // a portrait notice on the right, beside the reach-outs and the arriving letters, that
            // opens the evening's choice when clicked. X it and you are not asked for a week; leave
            // it and it lapses at first light like any other. Only when that UI is unavailable does
            // the old immediate popup stand in — and even then, never mid-battle or mid-talk.
            _nightAskedOnDay = NightCycleNow();

            if (UI.MapNoticePatch.Applied && _config.UseMapNoticeForInitiations)
            {
                RaiseNightNotice(wives);
                return;
            }

            if (!string.IsNullOrEmpty(InitiationBlockReason())) { _nightAskedOnDay = int.MinValue; return; }
            AskWhereYouWillSleep(wives);
        }

        // ------------------------------ the evening's notice ------------------------------

        // The night-cycle a notice was raised on. A CYCLE and not a bool, so that a notice which
        // simply lapsed cannot leave the flag stuck and silence every evening after it — the cycle
        // turning clears it by itself, with nothing to remember to reset. Not persisted: a reloaded
        // save just lets that evening pass, exactly as the reach-out notices do.
        private int _nightNoticeDay = int.MinValue;

        private bool NightNoticeUp => _nightNoticeDay == NightCycleNow();

        private void RaiseNightNotice(List<Hero> wives)
        {
            try
            {
                if (NightNoticeUp) return;

                // Whose face the notice wears (2026.08.15): the one it is actually about. With one
                // open door that is simply her; with several it is whoever heads the very list the
                // click is about to open, so the picture and the popup agree. A notice with nobody
                // behind it — every door shut — still goes up, and simply wears the plain icon.
                var open = wives.Where(w => NightBlockFor(w, out _).Length == 0)
                    .OrderBy(NightSortKey).ToList();
                var line = open.Count == 1
                    ? $"{open[0].Name?.ToString() ?? "She"} is here. Where will you sleep tonight?"
                    : "Where will you sleep tonight?";

                _nightNoticeDay = NightCycleNow();
                Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(
                    new UI.ImmersiveNightMapNotification(new TextObject("{=!}" + line),
                        open.FirstOrDefault() ?? wives.FirstOrDefault()));
            }
            catch (Exception ex)
            {
                _nightNoticeDay = int.MinValue;
                ModLog.Error("raising the evening's notice", ex);
            }
        }

        /// <summary>Whether the evening's notice still has anything to stand for. False the moment
        /// the night is settled some other way, or dawn has come.</summary>
        internal static bool IsNightNoticeStillAlive()
        {
            try
            {
                var self = Current;
                if (self == null || !self.NightsOn || !self.NightNoticeUp) return false;
                if (self._nightLedger!.IsCycleSettled(CampaignTime.Now.ToDays, self.NightResetHour)) return false;
                return self.IsWithinEveningWindow();
            }
            catch { return false; }
        }

        /// <summary>Clicked: the evening's own choice opens.</summary>
        internal static void OnNightNoticeInspected()
        {
            try
            {
                var self = Current;
                if (self == null) return;
                self._nightNoticeDay = int.MinValue;
                var wives = WomenOfTheHearth();
                if (wives.Count > 0) self.AskWhereYouWillSleep(wives);
            }
            catch (Exception ex) { ModLog.Error("opening the evening's choice", ex); }
        }

        /// <summary>Waved away by hand: that is an answer, and it stands for a week (Anton's rule).
        /// A notice that merely LAPSED never reaches here — see the item VM.</summary>
        internal static void OnNightNoticeDismissed()
        {
            try
            {
                var self = Current;
                if (self == null) return;
                self._nightNoticeDay = int.MinValue;
                self._dontAskNightsUntilDay = CampaignTime.Now.ToDays + 7;
                self.RecordTheRestOfTheNight(null);
                self.NotifyNight("You wave the question away. You will not be asked at dusk for a week — "
                               + "the window of the hearth is still there whenever you want it.");
            }
            catch (Exception ex) { ModLog.Error("waving the evening away", ex); }
        }

        // An evening that was asked about and never answered — the popup dismissed by a battle, a
        // load, or simply the player's shrug. A day later it is settled honestly: he slept alone,
        // nothing could come of it, and everyone who might have noticed rolls for whether they did.
        private void CloseUnansweredNight()
        {
            try
            {
                double lastNight = CampaignTime.Now.ToDays - 1;
                if (lastNight < 0) return;
                if (_nightLedger!.IsCycleSettled(lastNight, NightResetHour)) return;
                if (_nightLedger.LastSettledNight < 0)          // the very first evening settles nothing
                {
                    _nightLedger.SettleNight(lastNight);
                    SaveNightLedger();
                    return;
                }

                RecordTheRestOfTheNight(null, lastNight);
                NotifyNight("Last night you slept alone — no child could have come of it.", BarrenColor);
            }
            catch (Exception ex) { ModLog.Error("closing an unanswered evening", ex); }
        }

        // ------------------------------ the question ------------------------------

        private void AskWhereYouWillSleep(List<Hero> wives)
        {
            try
            {
                var elements = new List<InquiryElement>();
                int today = (int)CampaignTime.Now.ToDays;

                foreach (var wife in wives.OrderBy(w => NightSortKey(w)))
                {
                    var name = wife.Name?.ToString() ?? "she";
                    var block = NightBlockFor(wife, out _);
                    var mother = MotherOf(wife);

                    string hint;
                    if (block.Length > 0) hint = Upper(block) + ".";
                    else if (mother == null) hint = "The night is yours.";
                    else if (mother.IsPregnant) hint = "She is already carrying your child.";
                    else hint = Upper(NightOdds.LikelihoodWord(
                        MoodTides.Fertility(mother.StringId, today),
                        MoodTides.DoorIsClosed(mother.StringId, today))) + ".";

                    elements.Add(new InquiryElement(wife, name, SafePortrait(wife), block.Length == 0, hint));

                    // AND THE HARDEST THREE WORDS IN THE MOD, offered only where a door is shut
                    // against HIM and never where it is her body's own season. There is no third
                    // option here on purpose: respecting it is not a button, it is not pressing
                    // this one.
                    if (block.Length > 0 && CanGoToHerAnyway(wife))
                        elements.Add(new InquiryElement(new DutyChoice(wife), $"{name} — go to her anyway",
                            SafePortrait(wife), true,
                            "She will not refuse you. She will not welcome you either, and she will remember it — "
                            + "in her own words, on the list of what stands between you. Nothing is written of such "
                            + "a night."));
                }

                elements.Add(new InquiryElement(AloneChoice, "Not tonight — I have other cares",
                    null, true, "You sleep alone. Whoever is near may or may not notice."));
                if (_config.EnableNightWindow)
                    elements.Add(new InquiryElement(WindowChoice, "Let me look at my own house first",
                        null, true, "Opens the window of the hearth: where each of them stands, how her season runs, and the fortnight of nights she keeps."));
                elements.Add(new InquiryElement(DontAskChoice, "Leave me to it for a week",
                    null, true, "No one asks you this at dusk for the next seven days. The window still opens on its key."));

                var data = new MultiSelectionInquiryData(
                    new TextObject("{=ImmersiveAI_NightWhere}Where will you sleep tonight?").ToString(),
                    new TextObject("{=ImmersiveAI_NightWhereDesc}The night is yours to spend as you will.").ToString(),
                    elements, true, 1, 1,
                    new TextObject("{=ImmersiveAI_NightChoose}Choose").ToString(),
                    new TextObject("{=ImmersiveAI_NightLeaveIt}Leave it").ToString(),
                    picked => OnNightChoice(picked?.FirstOrDefault()?.Identifier),
                    _ => OnNightChoice(AloneChoice),
                    "", isSeachAvailable: false);

                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex) { ModLog.Error("asking where the night will be spent", ex); }
        }

        /// <summary>The one identifier that is a person AND a different question about her. Its own
        /// type so the choice can never be confused with simply picking her — going to a woman whose
        /// door is open and going through a door she shut are not the same act and must not share a
        /// code path.</summary>
        private sealed class DutyChoice
        {
            public readonly Hero Wife;
            public DutyChoice(Hero wife) { Wife = wife; }
        }

        // The three answers that are not a person.
        private const string AloneChoice = "immersiveai_night_alone";
        private const string DontAskChoice = "immersiveai_night_dontask";
        private const string WindowChoice = "immersiveai_night_window";

        // Nearest her season first, so the choice that matters is at the top of the list.
        private int NightSortKey(Hero wife)
        {
            try
            {
                if (NightBlockFor(wife, out _).Length > 0) return 1000;
                var mother = MotherOf(wife);
                if (mother == null || mother.IsPregnant) return 500;
                return Math.Abs(NightOdds.NightsFromCrest(mother.StringId, (int)CampaignTime.Now.ToDays));
            }
            catch { return 999; }
        }

        private void OnNightChoice(object? identifier)
        {
            try
            {
                if (identifier is string choice)
                {
                    // Looking at the house first settles nothing: the evening stays open, and the
                    // window's own "Go to her" walks the same road. If it cannot come up, the night
                    // is simply spent alone rather than swallowed.
                    if (choice == WindowChoice)
                    {
                        UI.NightWindow.NightWindowManager.OpenWhenClear(() => RecordTheRestOfTheNight(null));
                        return;
                    }
                    if (choice == DontAskChoice)
                    {
                        _dontAskNightsUntilDay = CampaignTime.Now.ToDays + 7;
                        NotifyNight("You will not be asked at dusk for a week.");
                    }
                    RecordTheRestOfTheNight(null);       // alone, either way
                    return;
                }

                // Through a shut door there is no gift question and no wish: nothing is being made
                // of this evening, which is the whole of what it says.
                if (identifier is DutyChoice duty) { GoToHerAnyway(duty.Wife); return; }
                if (identifier is Hero wife) AskWhatYouBring(wife);
            }
            catch (Exception ex) { ModLog.Error("taking the night's choice", ex); }
        }

        // What is laid out for it — the only place coin enters this feature, and the only thing that
        // buys a writing. The list is priced against the purse: a gift you cannot pay for is shown
        // greyed with its price, never silently hidden.
        private void AskWhatYouBring(Hero wife)
        {
            try
            {
                int purse = Safe(() => Hero.MainHero?.Gold ?? 0, 0);
                var elements = new List<InquiryElement>
                {
                    new InquiryElement(NightGifts.Plain, NightGifts.Plain.Name, null, true,
                        NightGifts.Plain.PlayerDescription),
                };

                foreach (var tier in NightGifts.Paid)
                {
                    if (tier.Price > _config.MaxNightGift) continue;
                    bool affordable = purse >= tier.Price;
                    var hint = $"{tier.Price} denars. {tier.PlayerDescription}"
                             + (affordable ? string.Empty : "  (You do not have it.)");
                    elements.Add(new InquiryElement(tier, $"{tier.Name} — {tier.Price}", null, affordable, hint));
                }

                var data = new MultiSelectionInquiryData(
                    new TextObject("{=ImmersiveAI_NightBring}What will you bring to her?").ToString(),
                    new TextObject("{=ImmersiveAI_NightBringDesc}What is laid out for a night is remembered — and a night made of something is written down. It also costs you the morning: the company breaks camp slowly after.").ToString(),
                    elements, true, 1, 1,
                    new TextObject("{=ImmersiveAI_NightGo}Go to her").ToString(),
                    GameTexts.FindText("str_cancel", null)?.ToString() ?? "Cancel",
                    picked =>
                    {
                        var tier = picked?.FirstOrDefault()?.Identifier as NightGifts.Tier;
                        if (tier != null) AskWhatYouHaveInMind(wife, tier);
                    },
                    // Backing out of the GIFT question settles nothing. It used to close the whole
                    // evening as a night alone, which is a harsh reading of "let me think about it"
                    // — the day is still yours from the window, and the next dusk's sweep will
                    // settle the night honestly if nothing comes of it (sweep, 2026.08.10).
                    _ => { },
                    "", isSeachAvailable: false);

                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex) { ModLog.Error("asking what is brought to the night", ex); }
        }

        // ------------------------------ what he has in mind ------------------------------

        /// <summary>
        /// THE ONE LINE THE PLAYER WRITES HIMSELF (2026.08.15, Anton's design — "I want us to spend
        /// the night under the stars"). Everything else about a night is chosen from a list; this is
        /// typed, in his own words and his own tongue, and it goes to the chronicler as HIS intent
        /// for the evening.
        ///
        /// THREE THINGS ABOUT THE SHAPE, all of them deliberate:
        /// • It is asked ONLY for a night that will actually be written. A wish handed to a plain
        ///   night has nowhere to land — there is no account for it to shape — and asking for one
        ///   anyway would be the mod promising something it then quietly drops.
        /// • BOTH buttons go to her. "Nothing in particular" is an answer, not a way out: the way
        ///   out was the gift question, which is still the last thing that can be backed out of, and
        ///   turning a shrug here into a cancelled evening would be a trap rather than a question.
        /// • Nothing is charged and nothing is rolled until <see cref="SpendTheNightWith"/>, so a
        ///   popup that sits open while the world moves costs nothing and is re-checked at the seal
        ///   exactly as before.
        /// </summary>
        private void AskWhatYouHaveInMind(Hero wife, NightGifts.Tier tier)
        {
            try
            {
                if (wife == null || tier == null) return;
                if (!_config.AskWhatYouHaveInMind || !tier.WritesStory)
                {
                    SpendTheNightWith(wife, tier);
                    return;
                }

                var name = Safe(() => wife.Name?.ToString() ?? "her", "her");
                var inquiry = new TextInquiryData(
                    new TextObject("{=ImmersiveAI_NightWish}Have you something in mind?").ToString(),
                    $"If there is something you want of this night with {name} — a place, an hour, a thing "
                    + "you mean to say or to do — set it down and the night will be shaped by it as far as it "
                    + "can be. What she makes of it is hers. Leave it empty and let the evening find its own way.",
                    true, true,
                    new TextObject("{=ImmersiveAI_NightGoWith}Go to her").ToString(),
                    new TextObject("{=ImmersiveAI_NightGoPlain}Nothing in particular").ToString(),
                    new Action<string>(text => SpendTheNightWith(wife, tier, wish: text)),
                    new Action(() => SpendTheNightWith(wife, tier)),
                    false, null, "", "");

                InformationManager.ShowTextInquiry(inquiry, true);
            }
            catch (Exception ex)
            {
                // A question that could not be put must never cost the night itself.
                ModLog.Error("asking what you have in mind for the night", ex);
                SpendTheNightWith(wife, tier);
            }
        }

        /// <summary>A wish is a line, not a letter: long enough for a real thought, short enough that
        /// it steers the account rather than becoming it.</summary>
        private const int MaxWishChars = 300;

        private static string TrimWish(string? raw)
        {
            var text = (raw ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text.Length <= MaxWishChars ? text : text.Substring(0, MaxWishChars).TrimEnd();
        }

        // ------------------------------ the night itself ------------------------------

        private void SpendTheNightAutomatically(List<Hero> open)
        {
            try
            {
                if (open.Count == 0) { RecordTheRestOfTheNight(null); return; }

                // Seeking: whoever stands nearest her season. Taking care: whoever — deliberately
                // NOT the ripest, which is half of what taking care has always meant.
                var wife = PreventChild
                    ? open[MBRandom.RandomInt(open.Count)]
                    : open.OrderBy(NightSortKey).FirstOrDefault();

                if (wife == null) { RecordTheRestOfTheNight(null); return; }
                SpendTheNightWith(wife, NightGifts.Plain, automatic: true);
            }
            catch (Exception ex) { ModLog.Error("the automatic night", ex); }
        }

        private void SpendTheNightWith(Hero wife, NightGifts.Tier tier, bool automatic = false,
            string? wish = null)
        {
            try
            {
                if (wife == null || !NightsOn) return;

                // Everything is re-checked at the seal, never only at the offer: an evening's popup
                // can sit open while the world moves (the bargain tool's own law).
                var block = NightBlockFor(wife, out _);
                if (block.Length > 0)
                {
                    NotifyNight($"You could not go to {wife.Name} — {block}.", SealGrey);
                    RecordTheRestOfTheNight(null);
                    return;
                }

                int price = Math.Max(0, tier?.Price ?? 0);
                if (price > 0)
                {
                    if (Safe(() => Hero.MainHero?.Gold ?? 0, 0) < price)
                    {
                        NotifyNight($"You could not pay the {price} denars for it.", SealGrey);
                        RecordTheRestOfTheNight(null);
                        return;
                    }
                    Safe(() => { GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, price, true); return 0; }, 0);
                }

                var chosen = tier ?? NightGifts.Plain;
                var record = BuildNightRecord(wife, chosen);
                record.Wish = TrimWish(wish);
                RollForAChild(wife, record, chosen);

                // The evening may already have been settled as a night alone — the dusk question
                // came and went, and only now, at midnight, does he open the window and go. Forget
                // what was written of this night and write it again, or the other wives would keep
                // remembering that he slept alone on the very night he did not (2026.08.10).
                _nightLedger!.ClearNight(record.GameDay);
                _nightLedger.Add(record, _config.MaxNightsRemembered);
                RecordTheRestOfTheNight(wife, -1, chosen);  // what the other wives came to know
                SaveNightLedger();

                // The morning's price: a company that lingered is slow to form up. Only for a night
                // that was made of something — an ordinary night costs the road nothing, and saying
                // otherwise every morning was "fast and dumb" (Anton, 2026.08.09).
                if (price > 0 && _config.PaidNightsDisorganizeParty)
                    Safe(() => { MobileParty.MainParty?.SetDisorganized(true); return 0; }, 0);

                if (automatic)
                    NotifyNight($"You spent the night with {wife.Name}.");

                // The plain beat lands now; a written night's beat waits for its name.
                if (!(tier?.WritesStory ?? false))
                {
                    WriteNightBeat(wife, NightText.PlainBeat(PlayerName(), record.PlaceName));
                    record.BeatDone = true;
                    SaveNightLedger();
                    UI.TalkUI.OnThreadChanged(wife, markUnread: false);
                }
                else
                {
                    record.StoryWanted = true;
                    SaveNightLedger();
                    BeginNightStory(wife, record);
                }
            }
            catch (Exception ex) { ModLog.Error("spending the night", ex); }
        }

        /// <summary>
        /// "GO TO HER ANYWAY" (2026.08.15). Nobody forces anybody: she does not refuse, she
        /// PERFORMS, and the weight of it is exactly that.
        ///
        /// THE NO-NARRATOR RULE IS THIS METHOD'S SHAPE, not a sentence in a prompt. There is no
        /// gift step, no wish, no title, no chronicler call and no ☾ notice anywhere below — the
        /// story path is not reachable from here at all, and <see cref="NightKind.Duty"/> makes it
        /// unreachable from the retry sweep too. There is nothing to tell. That is the telling.
        ///
        /// The one thing that DOES happen is the spiral: a reason is laid down on her list, by the
        /// world and not by her tool, so it costs the same whether or not the player ever speaks to
        /// her again. Conception stays real and vanilla-honest — a man who scorns the bond entirely
        /// can still father children forever, while the soul of the house walks further away. That
        /// contrast is the design.
        /// </summary>
        private void GoToHerAnyway(Hero wife)
        {
            try
            {
                if (wife == null || !NightsOn) return;
                // Re-checked at the act and never trusted from the popup — she can walk away, her
                // season can begin, or she can open her door, while the question sits on screen.
                // CanGoToHerAnyway now carries the presence and cooldown checks too.
                if (!CanGoToHerAnyway(wife))
                {
                    NotifyNight($"You did not go to {wife.Name}.", SealGrey);
                    RecordTheRestOfTheNight(null);
                    return;
                }
                if (DoorBlockFor(wife).Length == 0)
                {
                    AskWhatYouBring(wife);   // her door opened after all: this is an ordinary night
                    return;
                }

                var record = BuildNightRecord(wife, NightGifts.Plain);
                record.Kind = NightKind.Duty;
                record.GiftName = string.Empty;
                RollForAChild(wife, record, NightGifts.Plain);

                _nightLedger!.ClearNight(record.GameDay);
                _nightLedger.Add(record, _config.MaxNightsRemembered);
                RecordTheRestOfTheNight(wife, -1, NightGifts.Plain);
                SaveNightLedger();

                // Her beat: short, fixed, first person, and utterly flat. From here her own voice
                // does the rest with no staging whatever, which is why there is nothing in it to
                // perform.
                WriteNightBeat(wife, NightText.DutyBeat(PlayerName()));
                record.BeatDone = true;
                SaveNightLedger();

                // And the spiral, in code.
                LayDutyNightReason(wife, record.Id);

                // One spare sentence from Anton's own approved deck. Never generated, never
                // brightened, and deliberately not colored like a night that went well.
                NotifyNight(NightText.DrawDutyLine(record.Id), SealGrey);
                UI.TalkUI.OnThreadChanged(wife, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("going to her anyway", ex); }
        }

        private static string PlayerName() => Safe(() => Hero.MainHero?.Name?.ToString() ?? "he", "he");

        private NightRecord BuildNightRecord(Hero wife, NightGifts.Tier tier)
        {
            double now = CampaignTime.Now.ToDays;
            var settlement = Safe(() => Hero.MainHero?.CurrentSettlement ?? MobileParty.MainParty?.CurrentSettlement, (Settlement?)null);
            var mother = MotherOf(wife);

            return new NightRecord
            {
                GameDay = now,
                DateText = CalradiaDate(),
                WifeId = wife.StringId,
                WifeName = Safe(() => wife.Name?.ToString() ?? "she", "she"),
                Kind = NightKind.Together,
                PlaceName = Safe(() => PlaceNameNow(settlement), string.Empty),
                PlaceKind = PlaceKindNow(settlement),
                GiftPrice = tier.Price,
                GiftName = tier.Name,
                SeasonWord = mother == null ? string.Empty
                    : MoodTides.PhaseWord(MoodTides.PhaseOf(mother.StringId, (int)now, out _)),
            };
        }

        private static string PlaceNameNow(Settlement? settlement)
        {
            if (settlement == null) return Safe(() => MobileParty.MainParty?.IsMoving ?? false, false)
                ? "our camp on the road" : "our camp";
            var name = settlement.Name?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return "our camp";
            if (settlement.IsTown) return "the town of " + name;
            if (settlement.IsCastle) return "the castle of " + name;
            if (settlement.IsVillage) return "the village of " + name;
            return name!;
        }

        private static string PlaceKindNow(Settlement? settlement)
        {
            try
            {
                if (settlement == null) return "camp";
                if (settlement.IsTown) return "town";
                if (settlement.IsCastle) return "castle";
                if (settlement.IsVillage) return "village";
                return "camp";
            }
            catch { return "camp"; }
        }

        // ------------------------------ whether a child was begun ------------------------------

        private void RollForAChild(Hero wife, NightRecord record, NightGifts.Tier tier)
        {
            try
            {
                var mother = MotherOf(wife);
                if (mother == null) return;

                // The world still holds the reins when our patch could not take them: rolling here
                // as well would give the same child two chances, which is worse than either.
                //
                // A LOVER IS THE EXCEPTION, and provably so rather than hopefully: vanilla's own
                // nightly roll lives behind `hero.Spouse != null` (decompile-verified 2026.08.15,
                // PregnancyCampaignBehavior.DailyTickHero), and a lover has no spouse by the scope
                // rule of the whole feature. The world cannot reach her, so there is nothing to
                // collide with and no reason to make her barren for want of a patch.
                bool sheIsWed = Safe(() => FamilyBuilder.AreWed(Hero.MainHero, wife), false);
                if (sheIsWed && !Nights.PregnancyPatch.Applied) { WarnConceptionNotOurs(); return; }
                if (Safe(() => CampaignOptions.IsLifeDeathCycleDisabled, false)) return;
                if (mother.IsPregnant || _nightLedger!.HasPendingConception(wife.StringId)) return;

                // Taking care cuts the whole night down to a tenth, gift and season and all — the
                // grandest evening of your life still mostly does not make a child if you meant it
                // not to (Anton's rule, 2026.08.09).
                double dial = _config.ConceptionChanceMultiplier;
                if (PreventChild)
                    dial *= Math.Max(0, Math.Min(1, _config.CarefulNightChanceFactor));

                double chance = NightOdds.NightlyChanceFor(
                    mother.StringId, (int)CampaignTime.Now.ToDays,
                    VanillaDailyChanceFor(mother),
                    tier.Multiplier,
                    dial);

                record.Chance = chance;
                record.Conceived = chance > 0 && MBRandom.RandomFloat <= chance;
                if (record.Conceived)
                    record.RevealDay = CampaignTime.Now.ToDays + Math.Max(0, _config.ConceptionRevealDelayDays);

                if (_config.ShowConceptionOdds)
                    NotifyNight(NightText.OddsLine(record.WifeName, chance, record.Conceived),
                        record.Conceived ? QuickenedColor : BarrenColor);
            }
            catch (Exception ex) { ModLog.Error("reckoning the night", ex); }
        }

        /// <summary>
        /// THE NEWS OF A CHILD TRAVELS (2026.08.11, Anton's ask — "като някоя забременее да
        /// разберат всички с мене, включително ако имам други жени"). A pregnancy is not a private
        /// fact for long: the company sees it, and the other women of the house are told whether
        /// they are standing there or three weeks away, because in a household this is the news.
        ///
        /// Written by hand, no LLM call, one silent beat each, and `OutreachMark.None` — hearing
        /// something is not the player engaging them. The MOTHER is skipped (she has her own,
        /// better beat) and so is the player.
        /// </summary>
        private void SpreadTheNews(Hero mother, NightRecord record)
        {
            try
            {
                var player = Hero.MainHero;
                if (mother == null || player == null) return;

                var motherName = Safe(() => mother.Name?.ToString() ?? record.WifeName, record.WifeName);
                var fatherName = PlayerName();
                var told = new HashSet<string>(StringComparer.Ordinal) { mother.StringId, player.StringId };

                // The other women of his hearth first, wherever they are — for them this is not
                // gossip. Wives and lovers alike: a lover hearing that another woman is carrying
                // his child is hearing exactly the thing that matters most to her.
                foreach (var other in WomenOfTheHearth())
                {
                    if (other == null || !told.Add(other.StringId)) continue;
                    WriteNightBeat(other, NightText.ChildNewsBeat(motherName, fatherName, toAnotherWife: true),
                        OutreachMark.None);
                    UI.TalkUI.OnThreadChanged(other, markUnread: false);
                }

                // And whoever is with him — they have eyes, and a camp has no secrets.
                var nearby = Safe(() => Hero.AllAliveHeroes
                    .Where(h => h != null && h.IsAlive && !h.IsPrisoner && !h.IsChild && IsCoLocated(h))
                    .ToList(), new List<Hero>());
                foreach (var soul in nearby)
                {
                    if (soul == null || !told.Add(soul.StringId)) continue;
                    WriteNightBeat(soul, NightText.ChildNewsBeat(motherName, fatherName, toAnotherWife: false),
                        OutreachMark.None);
                    UI.TalkUI.OnThreadChanged(soul, markUnread: false);
                }
            }
            catch (Exception ex) { ModLog.Error("spreading the news of a child", ex); }
        }

        private void WarnConceptionNotOurs()
        {
            if (_nightPatchWarned) return;
            _nightPatchWarned = true;
            NotifyNight("Your nights are your own, but whether a child comes of them is still the world's to decide — "
                      + "this game's version did not let that part be taken over.", SealGrey);
        }

        /// <summary>The game's own daily chance for this woman — her age, her children, her house and
        /// her perks, all already weighed by whichever PregnancyModel is installed. It throws on a
        /// woman whose Spouse slot happens to be empty (vanilla reads the spouse's perk without
        /// checking, and a polygamy mod empties that slot constantly), so a mirror of the same
        /// arithmetic stands behind it rather than a silent zero.</summary>
        private static double VanillaDailyChanceFor(Hero mother)
        {
            try
            {
                var model = Campaign.Current?.Models?.PregnancyModel;
                if (model != null) return model.GetDailyChanceOfPregnancyForHero(mother);
            }
            catch { /* the empty-Spouse trap; fall through to our own reckoning */ }
            return MirroredDailyChance(mother);
        }

        // DefaultPregnancyModel's own formula, kept only for the case above. If the game ever
        // retunes it this drifts — which is why it is the FALLBACK and never the first answer.
        private static double MirroredDailyChance(Hero mother)
        {
            try
            {
                if (mother == null) return 0;
                float age = mother.Age;
                if (age < 18f || age > 45f) return 0;
                int children = Safe(() => mother.Children?.Count ?? 0, 0) + 1;
                double chance = (1.2 - (age - 18f) * 0.04) / (children * children) * 0.12;
                return Math.Max(0, chance);
            }
            catch { return 0; }
        }

        // ------------------------------ the day she comes to know ------------------------------

        // A child begun is not a child known. The world is told nothing until this day comes — which
        // is also why vanilla's own "she has learned she is with child" no longer lands the same
        // evening like a receipt.
        private void RevealDueConceptions()
        {
            if (!NightsOn) return;
            double today = CampaignTime.Now.ToDays;

            foreach (var record in _nightLedger!.DueForReveal(today).ToList())
            {
                // The mark goes down only once something has actually been DONE about it. Marking
                // it first cost nothing on a good day and quietly killed a child on a bad one: a
                // hero not resolvable this instant (mid-load, a prisoner exchange in flight) would
                // have had the conception struck off and never looked at again (sweep, 2026.08.10).
                var wife = FindAliveHero(record.WifeId);
                var mother = wife == null ? null : MotherOf(wife);
                if (wife == null || mother == null)
                {
                    // Dead is settled; merely not-found-right-now waits for the next hour.
                    if (!Safe(() => Hero.AllAliveHeroes.Any(h => h.StringId == record.WifeId), true))
                    {
                        record.Revealed = true;
                        SaveNightLedger();
                    }
                    continue;
                }

                try
                {
                    if (mother.IsPregnant) { record.Revealed = true; SaveNightLedger(); continue; }
                    if (Safe(() => CampaignOptions.IsLifeDeathCycleDisabled, false))
                    {
                        record.Revealed = true; SaveNightLedger(); continue;
                    }

                    // Who fathered it — and if that cannot be answered, NOTHING is applied. A
                    // fatherless pregnancy does not fail here; it fails thirty-six days later,
                    // inside the game's own delivery, as a hard crash that repeats on every load
                    // (Steam, 2026.08.30). A child we cannot name a father for is a child we do
                    // not begin.
                    var father = FatherOf(mother, wife);
                    if (father == null)
                    {
                        ModLog.Warn($"the nights: no father could be named for {record.WifeName}'s night, so no child is begun.");
                        record.Revealed = true; SaveNightLedger(); continue;
                    }

                    // vanilla's own door: its announcement, its due date — and, either way, the
                    // father snatched out of mother.Spouse at that very instant.
                    if (mother != Hero.MainHero && Safe(() => FamilyBuilder.AreWed(Hero.MainHero, wife), false))
                    {
                        EnsureFatherSlot(mother);
                        MakePregnantAction.Apply(mother);
                    }
                    else BorrowTheFatherSlot(mother, father, () => MakePregnantAction.Apply(mother));

                    // Only now is it truly done. An exception on the line above leaves the mark
                    // down and the record waiting, so the next hour tries again.
                    record.Revealed = true;
                    ModLog.Info($"the nights: {record.WifeName} is with child (begun on day {(int)record.GameDay}).");

                    // And her own word of it, in her own memory, so the chat carries the moment too.
                    WriteNightBeat(wife, ChildKnownBeat(record), OutreachMark.None);
                    UI.TalkUI.OnThreadChanged(wife, markUnread: true);

                    // And the news travels: everyone riding or standing with the player learns it
                    // the same day, and his other wives learn it whether they are there or not.
                    SpreadTheNews(wife, record);

                    // She may want to tell the player herself — and this once, the world's quiet
                    // does not hold her back: no damping, no socialness, no waiting her turn.
                    InviteHerToTellYou(wife);
                }
                catch (Exception ex) { ModLog.Error("applying a conception", ex); }
                SaveNightLedger();
            }
        }

        // Vanilla records the father as whatever sits in the mother's Spouse slot at the instant of
        // MakePregnantAction — and a polygamy mod keeps exactly one wife in that slot at a time, so
        // a second wife's child would be fathered by null and crash at the delivery 36 days later.
        // Setting it here is the same move Marry Anyone's own daily prefix makes, and it is the one
        // place in this file that touches the world's family graph.
        private static void EnsureFatherSlot(Hero mother)
        {
            try
            {
                var player = Hero.MainHero;
                if (mother == null || player == null || mother == player) return;
                if (mother.Spouse == player) return;
                if (!FamilyBuilder.AreWed(player, mother)) return;
                mother.Spouse = player;
            }
            catch (Exception ex) { ModLog.Error("naming the father before a conception", ex); }
        }

        /// <summary>
        /// THE LOVER'S OWN VERSION OF THE FATHER TRAP (2026.08.15), and it is the same silent killer
        /// wearing different clothes. Decompile-verified: vanilla's <c>ChildConceived</c> listener
        /// builds <c>new Pregnancy(mother, mother.Spouse, dueDate)</c> — the father is snatched out
        /// of the spouse slot AT THAT INSTANT and then held for thirty-six days. A lover has no
        /// spouse, so the child would be fathered by null and the game would crash at the delivery,
        /// more than a month after anything a player could connect it to.
        ///
        /// A wife's slot is simply SET and left (that is Marry Anyone's own move, and she really is
        /// his wife). A lover's is BORROWED: the dispatcher is synchronous, so by the time
        /// <c>Apply</c> returns the father has been captured, and the slot goes straight back. It
        /// must go back — leaving it would quietly marry her to him in the world's eyes, which is
        /// precisely the thing this whole road exists not to do.
        /// </summary>
        private static void BorrowTheFatherSlot(Hero mother, Hero father, Action act)
        {
            Hero? previous = null;
            bool borrowed = false;
            try
            {
                // A WOMAN PLAYER BORROWS TOO, and that is the half this was missing: she is her own
                // mother here, her lover is the father, and her spouse slot is empty (or holds a
                // husband who is not the father of this child). The old guard read mother != player
                // and simply let her conceive fatherless.
                if (mother != null && father != null && mother != father && mother.Spouse != father)
                {
                    previous = mother.Spouse;
                    mother.Spouse = father;
                    borrowed = true;
                }
                act();
            }
            finally
            {
                // The slot goes back whatever happened — but the FAILURE does not stop here. The
                // wife's path lets a throw reach the caller's catch, which leaves the conception
                // unmarked so the next hour tries again; swallowing it here would mark a child
                // revealed that was never actually applied, and it would never be retried
                // (self-review, 2026.08.15).
                if (borrowed)
                {
                    try { mother.Spouse = previous; }
                    catch (Exception ex) { ModLog.Error("giving back the father's slot", ex); }
                }
            }
        }

        private static string ChildKnownBeat(NightRecord record)
        {
            var named = string.IsNullOrWhiteSpace(record.Title)
                ? string.Empty
                : $" I think it was the night I keep as \"{record.Title.Trim()}\".";
            return $"{NightText.NightBeatMark} I have known it for some days now, and today I am sure: "
                 + $"I am carrying {PlayerName()}'s child.{named}";
        }

        // The one outreach the world's quiet never silences: a woman does not wait her turn to say
        // this. Face to face if he is near, by letter if he is not.
        private void InviteHerToTellYou(Hero wife)
        {
            try
            {
                if (LlmGate.AutonomyQuiet || UsageLedger.DailyCapReached) return;
                if (IsCoLocated(wife))
                {
                    if (_initiationInFlight || !IsSafeToInitiate()) return;
                    MarkInitiationInFlight();
                    _ = BeginInitiationAsync(wife);
                }
                else if (_config.EnableLetters && !_letterWorkInFlight)
                {
                    MarkLetterWorkInFlight();
                    _ = BeginNpcLetterAsync(wife);
                }
                // Both paths still ASK her first, in her own mind, whether she wants to come or to
                // write — which is the point. All this does is put the question, where the world's
                // own quiet would never have raised it.
            }
            catch (Exception ex) { ModLog.Error("letting her bring you the news", ex); }
        }

        // ------------------------------ what the other wives came to know ------------------------------

        // Every wife who was not the one visited gets her own honest line for the night: what she
        // saw, what word reached her, or that she simply never learned. A wife who is NOT with the
        // player learns nothing at all — except that he was with another, which is the one thing
        // word travels with (Anton's call, 2026.08.09: keep the distant case to that alone).
        private void RecordTheRestOfTheNight(Hero? visited, double forDay = -1, NightGifts.Tier? tier = null)
        {
            try
            {
                if (!NightsOn) return;
                double now = forDay >= 0 ? forDay : CampaignTime.Now.ToDays;
                int today = (int)now;
                bool atWar = Safe(() => MobileParty.MainParty?.MapEvent != null
                                     || MobileParty.MainParty?.SiegeEvent != null, false);
                var settlement = Safe(() => Hero.MainHero?.CurrentSettlement ?? MobileParty.MainParty?.CurrentSettlement, (Settlement?)null);
                string placeKind = PlaceKindNow(settlement);
                string placeName = Safe(() => PlaceNameNow(settlement), string.Empty);
                int odds = AwarenessPercent(placeKind, tier ?? NightGifts.Plain);

                foreach (var wife in WomenOfTheHearth())
                {
                    if (visited != null && wife == visited) continue;
                    if (_nightLedger!.HasNightOn(wife.StringId, now)) continue;

                    bool here = IsCoLocated(wife);
                    var mother = MotherOf(wife);
                    bool doorClosed = mother != null && !mother.IsPregnant
                        && MoodTides.DoorIsClosed(mother.StringId, today);

                    // Her own closed door is hers to know whether or not she saw anything else.
                    if (doorClosed && here)
                        _nightLedger.Add(new NightRecord
                        {
                            GameDay = now, DateText = CalradiaDate(),
                            WifeId = wife.StringId, WifeName = Safe(() => wife.Name?.ToString() ?? "she", "she"),
                            Kind = NightKind.DoorClosed, PlaceName = placeName, PlaceKind = placeKind,
                        }, _config.MaxNightsRemembered);

                    var learned = WhatSheLearned(wife, visited, here, placeKind, atWar, odds, now);
                    if (learned == null) continue;
                    if (doorClosed && here && learned.Kind == NightKind.Unknown) continue;   // one honest line is enough

                    // The closed-door line took her slot for tonight; hers is the second of two.
                    if (doorClosed && here)
                    {
                        learned.GameDay = now + 0.01;
                        _nightLedger.AddBeside(learned, _config.MaxNightsRemembered);
                    }
                    else _nightLedger.Add(learned, _config.MaxNightsRemembered);

                    // AND LEARNING HE WAS ELSEWHERE IS A WOUND (2026.08.15). For a day and a half
                    // it will move her to come and say something, wherever she stands — and what
                    // she says, or whether she says anything at all, is entirely hers. Only THIS
                    // kind of night counts: hearing that he slept alone is not an injury, and
                    // never seeing him come in is not news.
                    if (learned.Kind == NightKind.Elsewhere) MarkFreshWound(wife, now);
                }

                _nightLedger!.SettleNight(now);
                SaveNightLedger();
            }
            catch (Exception ex) { ModLog.Error("recording what the night was to the others", ex); }
        }

        /// <summary>She has just learned something. It stays hot for a day and a half; after that
        /// the ordinary quiet takes over, which the design record is quite clear is worse.</summary>
        private void MarkFreshWound(Hero her, double gameDay)
        {
            try
            {
                if (her == null) return;
                var memory = LoadMemory(her);
                memory.FreshWoundDay = gameDay;
                SaveMemory(her, memory);
            }
            catch { /* a wound that fails to register is only a quieter morning */ }
        }

        private NightRecord? WhatSheLearned(Hero wife, Hero? visited, bool here, string placeKind,
            bool atWar, int odds, double gameDay)
        {
            try
            {
                var record = new NightRecord
                {
                    GameDay = gameDay,
                    DateText = CalradiaDate(),
                    WifeId = wife.StringId,
                    WifeName = Safe(() => wife.Name?.ToString() ?? "she", "she"),
                    AtWar = atWar,
                    PlaceKind = placeKind,
                };

                if (!here)
                {
                    // Far from him she keeps no watch on his nights — but word of another woman
                    // travels, and ONLY that (Anton's call: keep the distant case to the one thing
                    // that would actually be repeated).
                    if (visited == null || visited == wife) return null;
                    if (!WordCanReach(wife)) return null;
                    if (MBRandom.RandomInt(100) >= odds / 2) return null;
                    record.Kind = NightKind.Elsewhere;
                    record.ByHearsay = true;
                    record.OtherName = Safe(() => visited.Name?.ToString() ?? string.Empty, string.Empty);
                    return record;
                }

                if (MBRandom.RandomInt(100) >= odds)
                {
                    record.Kind = NightKind.Unknown;
                    return record;
                }

                if (visited == null) record.Kind = NightKind.Alone;
                else
                {
                    record.Kind = NightKind.Elsewhere;
                    record.OtherName = Safe(() => visited.Name?.ToString() ?? string.Empty, string.Empty);
                }
                return record;
            }
            catch { return null; }
        }

        // How likely a wife standing right here is to learn where he spent the night: the place
        // hides or reveals it, and the GIFT does the rest. A night of nothing but themselves halves
        // the talk; a jewel doubles it, because servants carry water up the stairs and a woman wears
        // a jewel where everyone can see it. Capped at a certainty, floored at never.
        private int AwarenessPercent(string placeKind, NightGifts.Tier tier)
        {
            int baseOdds;
            switch (placeKind)
            {
                case "town": baseOdds = _config.NightAwarenessInTownPercent; break;
                case "castle": baseOdds = _config.NightAwarenessInCastlePercent; break;
                case "village": baseOdds = _config.NightAwarenessInVillagePercent; break;
                default: baseOdds = _config.NightAwarenessOnRoadPercent; break;
            }
            double scaled = baseOdds * (tier?.AwarenessMultiplier ?? 0.5);
            return (int)Math.Round(Math.Max(0, Math.Min(100, scaled)));
        }

        // Word travels a few days' ride and no further — beyond that a wife hears nothing of his
        // nights, which is the honest answer and also the cheap one.
        private const float WordOfMouthRange = 120f;

        private static bool WordCanReach(Hero wife)
        {
            try
            {
                var mine = Hero.MainHero?.CurrentSettlement?.Position ?? MobileParty.MainParty?.Position;
                var hers = wife.CurrentSettlement?.Position ?? wife.PartyBelongedTo?.Position;
                if (mine == null || hers == null) return false;
                return mine.Value.Distance(hers.Value) <= WordOfMouthRange;
            }
            catch { return false; }
        }

        // ------------------------------ the writing ------------------------------

        private void BeginNightStory(Hero wife, NightRecord record)
        {
            try
            {
                if (wife == null || record == null) return;
                if (!_nightsWriting.Add(record.Id)) return;
                record.StoryAttempts++;
                var facts = GatherNightFacts(wife, record);
                _ = WriteNightStoryAsync(wife, record, facts);
            }
            catch (Exception ex)
            {
                _nightsWriting.Remove(record?.Id ?? string.Empty);
                ModLog.Error("beginning a night's account", ex);
            }
        }

        private async Task WriteNightStoryAsync(Hero wife, NightRecord record, NightText.Facts facts)
        {
            using var _cost = UsageLedger.BeginInteraction("the night", record.WifeName);
            try
            {
                var raw = await _storyClient.CompleteAsync(new List<ChatMessage>
                {
                    ChatMessage.User(NightText.BuildStoryPrompt(facts)),
                }).ConfigureAwait(false);

                // The tamer's cut must sit ABOVE what this tier was asked for, or a grand night is
                // silently shortened back to a plain one's length.
                if (!NightText.TryParseStory(raw, out var title, out var story, facts.MaxSentences))
                {
                    ModLog.Info("the nights: no usable account came back; the night waits, unwritten.");
                    return;
                }
                MainThreadDispatcher.Enqueue(() => FinishNightStory(wife, record, title, story));
            }
            catch (Exception ex) { ModLog.Error("writing a night's account", ex); }
            finally
            {
                MainThreadDispatcher.Enqueue(() => _nightsWriting.Remove(record.Id));
            }
        }

        private void FinishNightStory(Hero wife, NightRecord record, string title, string story)
        {
            try
            {
                // The world may have moved on while it was written: a campaign closed, another
                // loaded, or this behavior left behind by a reload. Writing this night into THAT
                // campaign's book would be a lie (the wedding chronicle's own guard).
                if (_nightLedger == null || Campaign.Current == null) return;
                if (!ReferenceEquals(Current, this)) return;

                record.Title = string.IsNullOrWhiteSpace(title) ? DefaultNightName(record) : title;
                record.Story = story;
                LeakTheNameOfTheNight(record);

                if (!record.BeatDone)
                {
                    WriteNightBeat(wife, NightText.NamedBeat(PlayerName(), record.PlaceName, record.Title));
                    record.BeatDone = true;
                }
                SaveNightLedger();
                NightLedger.AppendReadable(NpcPaths.NightsKeepsakeFile, record);

                UI.TalkUI.OnThreadChanged(wife, markUnread: false);
                NotifyNight($"☾ {record.WifeName} will remember this night — \"{record.Title}\".");
            }
            catch (Exception ex) { ModLog.Error("finishing a night's account", ex); }
        }

        // If a wife already learned he was elsewhere that night, she learns what they are CALLING
        // it too (Anton, 2026.08.09). The name arrives after the fact — the chronicler writes it
        // minutes later — so it is carried back to the records that already leaked, and only to
        // those: a night nobody heard of keeps its name between the two of them.
        private void LeakTheNameOfTheNight(NightRecord record)
        {
            try
            {
                if (_nightLedger == null || string.IsNullOrWhiteSpace(record.Title)) return;
                foreach (var other in _nightLedger.Nights)
                {
                    if (other == null || other.Kind != NightKind.Elsewhere) continue;
                    if (Math.Floor(other.GameDay) != Math.Floor(record.GameDay)) continue;
                    if (!string.Equals(other.OtherName, record.WifeName, StringComparison.Ordinal)) continue;
                    other.OtherNightTitle = record.Title;
                    other.OtherNightPrice = record.GiftPrice;
                    // The mark was left the evening it happened, minutes before the chronicler
                    // answered — refresh it, or the month's reckoning never hears the name.
                    _nightLedger.RefreshMark(other);
                }
            }
            catch { /* gossip that fails to travel is only gossip */ }
        }

        // A night whose chronicler never answered still deserves a name rather than a blank.
        private static string DefaultNightName(NightRecord record) =>
            string.IsNullOrWhiteSpace(record.PlaceName) ? "That night" : $"That night in {record.PlaceName}";

        // A night saved but never written (a 429, a timeout) is not lost: the record keeps every
        // fact of it, so the chronicler can be asked again on a later hour, a bounded few times.
        private void RetryUnwrittenNights()
        {
            if (!NightsOn) return;
            SweepNightsThatNeverGotTheirBeat();
            if (LlmGate.AutonomyQuiet || _nightsWriting.Count > 0) return;
            foreach (var record in _nightLedger!.AwaitingStories())
            {
                var wife = FindAliveHero(record.WifeId);
                if (wife == null) continue;
                BeginNightStory(wife, record);
                return;                                  // one at a time; the next hour may take the next
            }
        }

        /// <summary>
        /// A night the chronicler has finally given up on still gets its plain mark in her memory
        /// (2026.08.10). <see cref="NightLedger.AwaitingBeats"/> had been written for exactly this
        /// and never called, so a paid night whose three attempts all failed left NO trace at all:
        /// the gold was spent, the odds were rolled, and she remembered nothing of the evening.
        /// An unwritten night is a disappointment; an unremembered one is a bug.
        /// </summary>
        private void SweepNightsThatNeverGotTheirBeat()
        {
            try
            {
                if (_nightLedger == null) return;
                bool changed = false;
                foreach (var record in _nightLedger.AwaitingBeats())
                {
                    // ONLY nights that were owed a writing. A plain night gets its beat the instant
                    // it is spent, so anything else without one is bookkeeping rather than a lived
                    // evening — the DevMode conception lever forges exactly such a row, and without
                    // this the sweep would have set down "he came to me" for a night nobody had
                    // (2026.08.10 review).
                    if (!record.StoryWanted) continue;
                    // Still owed an account and still worth asking for: leave it to the retry, or
                    // she would keep the plain mark and then be given the named one as well.
                    if (record.NeedsStory(NightLedger.MaxStoryAttempts)) continue;
                    if (_nightsWriting.Contains(record.Id)) continue;

                    var wife = FindAliveHero(record.WifeId);
                    if (wife == null) continue;

                    WriteNightBeat(wife, NightText.PlainBeat(PlayerName(), record.PlaceName));
                    record.BeatDone = true;
                    changed = true;
                    UI.TalkUI.OnThreadChanged(wife, markUnread: false);
                }
                if (changed) SaveNightLedger();
            }
            catch (Exception ex) { ModLog.Error("sweeping the nights that were never written", ex); }
        }

        private NightText.Facts GatherNightFacts(Hero wife, NightRecord record)
        {
            // The gift decides how much room this night's account gets (2026.08.10): the coin has
            // always bought the odds and the fact of a writing, and now it buys the length too.
            var gift = NightGifts.Resolve(record.GiftPrice);
            var facts = new NightText.Facts
            {
                WifeName = record.WifeName,
                PartnerName = PlayerName(),
                PartnerGenderWord = Safe(() => Hero.MainHero?.IsFemale ?? false, false) ? "woman" : "man",
                DateText = record.DateText,
                PlacePhrase = record.PlaceKind == "camp" ? string.Empty : record.PlaceName,
                PlaceKind = record.PlaceKind,
                GiftNote = gift.ChroniclerNote,
                // His own line for this night, if he set one down. It rides on the record, so a
                // night retried an hour after a failed call is still the night he asked for.
                PlayerWish = record.Wish ?? string.Empty,
                MinSentences = gift.MinSentences,
                MaxSentences = gift.MaxSentences,
                // The night's own id, so a retry an hour later is dealt the same images and stays
                // the same night rather than becoming a different one.
                ImageSeed = record.Id ?? string.Empty,
            };

            try { facts.WifeAge = (int)wife.Age; } catch { }
            try
            {
                var persona = PersonaBuilder.Build(wife, _config);
                facts.WifeStation = persona?.RoleDescription ?? string.Empty;
                facts.WifeTraits = persona?.PersonalityDescription ?? string.Empty;
            }
            catch { }
            try { facts.WifeSelfText = PromptFiles.LoadNpcPrompt(NpcPaths.CustomInstructionsFile(wife), record.WifeName); }
            catch { }
            try { facts.SeasonPhrase = SeasonPhrase(); } catch { }
            try
            {
                var mother = MotherOf(wife);
                int today = (int)CampaignTime.Now.ToDays;
                facts.WithChild = mother != null && mother.IsPregnant;
                if (mother != null && _config.EnableWomensCycle && !facts.WithChild)
                    facts.WifeSeason = MoodTides.CycleSentence(MoodTides.PhaseOf(mother.StringId, today, out _));
                if (_config.EnableMoodSwings)
                    facts.WifeHumor = MoodTides.DailyHumor(wife.StringId, today);
            }
            catch { }
            try
            {
                var memory = LoadMemory(wife);
                facts.SharedStory = memory.DeepMemoryText();
                facts.RecentWords = RecentSpokenWords(memory, PlayerName(), record.WifeName);
            }
            catch { }
            try { facts.SinceLastPhrase = SinceLastNightPhrase(wife); } catch { }
            try
            {
                // What the last few nights are already called, so this one steers away from them.
                facts.PastNightNames = _nightLedger!.For(wife.StringId)
                    .Where(n => n.IsStoried && n.Id != record.Id && !string.IsNullOrWhiteSpace(n.Title))
                    .OrderByDescending(n => n.GameDay)
                    .Take(6)
                    .Select(n => n.Title.Trim())
                    .ToList();
            }
            catch { }
            try { facts.CircumstancePhrase = CircumstancePhrase(); } catch { }
            try
            {
                var world = PromptFiles.LoadGlobalPrompt();
                var guidance = ApplyTokens(_config.RoleplayGuidance, record.WifeName);
                facts.WorldText = string.Join(" ", new[] { world, guidance }
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            }
            catch { }

            return facts;
        }

        private string SinceLastNightPhrase(Hero wife)
        {
            var last = _nightLedger?.For(wife.StringId)
                .Where(n => n.Kind == NightKind.Together && n.GameDay < CampaignTime.Now.ToDays - 0.5)
                .LastOrDefault();
            if (last == null) return "it has been a long while, or it is the first since the wedding";
            int nights = (int)Math.Round(CampaignTime.Now.ToDays - last.GameDay);
            if (nights <= 1) return "only a night";
            if (nights <= 3) return $"{nights} nights";
            if (nights <= 10) return $"{nights} nights, which is longer than either of them likes";
            return "more than a fortnight";
        }

        private static string CircumstancePhrase()
        {
            try
            {
                var party = MobileParty.MainParty;
                if (party?.SiegeEvent != null) return "they are at a siege, and the walls are close";
                if (party?.MapEvent != null) return "there was fighting this very day";
                if (party?.Army != null) return "the company marches with an army";
                if (Hero.MainHero?.IsWounded ?? false) return "he is carrying a wound";
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        // ------------------------------ the beats ------------------------------

        // One beat, written safely — the wedding chronicle's own discipline: a soul whose exchange
        // is in flight holds a memory instance loaded before this moment and would save right over
        // a beat written now, so hers is parked and folded in by SaveMemory.
        private void WriteNightBeat(Hero wife, string beat, OutreachMark mark = OutreachMark.None)
        {
            try
            {
                if (wife == null || string.IsNullOrWhiteSpace(beat)) return;
                if (IsExchangeInFlight(wife))
                {
                    _pendingWeddingBeats.AddOrUpdate(wife.StringId,
                        _ => new List<string> { beat },
                        (_, existing) => { existing.Add(beat); return existing; });
                    return;
                }
                AppendRecordedTurn(wife, beat, string.Empty, mark);
            }
            catch (Exception ex) { ModLog.Error("writing a night's beat", ex); }
        }

        // ------------------------------ what the rest of the mod reads ------------------------------

        /// <summary>Her own fortnight of nights, for her situation. Empty for anyone not wed to the
        /// player, and empty when the feature is off.</summary>
        internal string NightsRollFor(Hero npc)
        {
            try
            {
                if (!NightsOn || npc == null) return string.Empty;
                // The roll stops AT the line: everything after it is told once, in order, by the
                // block that closes the sheet. Telling a night twice is the double-read mistake the
                // retired truths cost us once already.
                double line = LastAloneDay(npc);
                var nights = _nightLedger!.For(npc.StringId)
                    .Where(n => line < 0 || n.GameDay <= line).ToList();
                if (nights.Count == 0) return string.Empty;
                return NightText.BuildRoll(nights, CampaignTime.Now.ToDays, _config.MaxNightsToldInFull,
                    NightText.DefaultFullAccountBudget,
                    Safe(() => _nightLedger!.MarksFor(npc.StringId), null));
            }
            catch { return string.Empty; }
        }

        // ------------------------------ the line ------------------------------

        // How long a talk with no clean ending still counts as running. The chat window has no
        // "goodbye" the way a face-to-face conversation does, so a session that never announced its
        // end still moves the line by itself once the day has plainly turned over.
        private const double TalkGraceHours = 8.0;

        /// <summary>
        /// When the two of them last had TIME TO THEMSELVES — the anchor of the whole line
        /// (2026.08.09, Anton's design). Three things move it and nothing else does: a talk that has
        /// ENDED, a night together of any kind, and the wedding night. A battle does not, a market
        /// does not, hearing where he slept does not — those are what the list after the line is for.
        ///
        /// A RUNNING TALK MUST NOT MOVE IT (his correction, and it is the load-bearing part): the
        /// sheet is rebuilt for every single reply, so if her own first answer moved the line, the
        /// very thing she was raising would vanish from under her halfway through raising it. So the
        /// talk side reads the parted-stamp, and falls back only to turns old enough that no sitting
        /// could still be going on.
        /// </summary>
        private double LastAloneDay(Hero npc)
        {
            double day = -1;

            try
            {
                var memory = LoadMemory(npc);
                day = memory.LastTalkEndedDay;

                double settled = CampaignTime.Now.ToDays - TalkGraceHours / 24.0;
                for (int i = memory.RecentTurns.Count - 1; i >= 0; i--)
                {
                    var turn = memory.RecentTurns[i];
                    if (turn == null || !turn.IsSpokenExchange) continue;
                    if (turn.GameDay > settled) continue;           // a sitting that may still be going on
                    if (turn.GameDay > day) day = turn.GameDay;
                    break;
                }
            }
            catch { }

            try
            {
                var night = _nightLedger?.For(npc.StringId).LastOrDefault(n => n.Kind == NightKind.Together);
                if (night != null && night.GameDay > day) day = night.GameDay;
            }
            catch { }

            try
            {
                var wedding = _weddingLedger?.OwnWeddingOf(npc.StringId);
                if (wedding != null && wedding.GameDay > day) day = wedding.GameDay;
            }
            catch { }

            return day;
        }

        /// <summary>Marks a talk as ended, so from now on what was in it counts as said. Called when
        /// a face-to-face conversation closes and when a window lets a thread go.
        ///
        /// A TALK WITH NO WORDS IN IT IS NOT A TALK (2026.08.28, Anton: opening her thread and closing
        /// it again wiped the line as though they had had it all out). Every caller knows only that a
        /// screen closed, so the bar lives in this one place rather than in each of them: the stamp
        /// moves only when something was truly SAID since the last time it moved. Otherwise the line
        /// stands exactly where it stood, and she can still raise it the next time he sits down.
        ///
        /// A reply still in flight when the window closes is not lost, only late — its turn lands
        /// afterwards and the grace fallback in <see cref="LastAloneDay"/> takes it from there.</summary>
        internal void NoteTalkEnded(Hero npc)
        {
            try
            {
                if (npc == null) return;
                var memory = LoadMemory(npc);
                double now = CampaignTime.Now.ToDays;
                if (memory.LastTalkEndedDay >= now) return;
                if (!HasSpokenSince(memory, memory.LastTalkEndedDay)) return;
                memory.LastTalkEndedDay = now;
                SaveMemory(npc, memory);
            }
            catch { /* the stamp is a nicety; the grace fallback still moves the line */ }
        }

        /// <summary>Whether words truly passed between them after <paramref name="since"/>. Strictly
        /// after: the talk screen holds the world still, so two sittings can share one campaign instant,
        /// and a turn standing ON the mark was already counted by the mark.</summary>
        private static bool HasSpokenSince(NpcMemory memory, double since)
        {
            if (memory == null) return false;
            for (int i = memory.RecentTurns.Count - 1; i >= 0; i--)
            {
                var turn = memory.RecentTurns[i];
                if (turn == null) continue;
                if (turn.GameDay <= since) break;                   // kept in order; everything older still
                if (turn.IsSpokenExchange) return true;
            }
            return false;
        }

        internal static void NoteTalkEndedWith(Hero npc)
        {
            try { if (npc != null) Current?.NoteTalkEnded(npc); }
            catch { }
        }

        /// <summary>
        /// The whole block: the line, and after it everything that has happened since, dated and one
        /// line each — her nights, the fields they stood on, the roads and markets they walked if she
        /// rides with him. Empty when nothing has happened since, which is exactly when it should be
        /// invisible; it comes back on its own the next time something does.
        /// </summary>
        internal string TogetherBlockFor(Hero npc)
        {
            try
            {
                if (npc == null || Campaign.Current == null) return string.Empty;
                double line = LastAloneDay(npc);
                if (line < 0) return string.Empty;                 // they have never been alone at all

                var since = new List<TogetherEntry>();

                // Her own nights.
                try
                {
                    if (NightsOn)
                        foreach (var night in _nightLedger!.For(npc.StringId).Where(n => n.GameDay > line))
                        {
                            var text = NightTimelineLine(night);
                            if (!string.IsNullOrWhiteSpace(text))
                                since.Add(new TogetherEntry { GameDay = night.GameDay, DateText = night.DateText, Text = text });
                        }
                }
                catch { }

                // The fields they stood on together.
                try
                {
                    if (_config.EnableBattleChronicle && _battleLedger != null)
                        foreach (var battle in _battleLedger.SharedWith(npc.StringId).Where(b => b.GameDay > line))
                            since.Add(new TogetherEntry
                            {
                                GameDay = battle.GameDay,
                                DateText = battle.DateText,
                                Text = BattleTimelineLine(battle),
                                Kind = TogetherKind.Battle,      // three or more gather into one span line
                            });
                }
                catch { }

                // And the everyday road — only for a soul who actually rode it with him.
                try
                {
                    if (_config.EnableJourneyLog && _journeyLog != null
                        && npc.PartyBelongedTo == MobileParty.MainParty)
                        foreach (var visit in _journeyLog.Visits)
                        {
                            if (visit == null || visit.ArriveDay <= line || !visit.HasAnyDoings) continue;
                            var text = Core.Journey.JourneyText.OneLine(visit);
                            if (string.IsNullOrWhiteSpace(text)) continue;
                            since.Add(new TogetherEntry
                            {
                                GameDay = visit.ArriveDay,
                                DateText = visit.ArriveText,
                                Text = text.Trim(),
                                Kind = TogetherKind.Stop,        // three or more gather into one span line
                            });
                        }
                }
                catch { }

                return TogetherLine.Build(since);
            }
            catch (Exception ex) { ModLog.Error("drawing the line since we were last alone", ex); return string.Empty; }
        }

        internal static string TogetherBlock(Hero npc)
        {
            try { return Current?.TogetherBlockFor(npc) ?? string.Empty; }
            catch { return string.Empty; }
        }

        /// <summary>The day the line stands on, so the chat thread can draw it in its own place —
        /// right after the last exchange the two of them had alone, not nailed to the foot of the
        /// thread where "from this moment" would mean nothing (Anton's screenshot, 2026.08.10).</summary>
        internal static double LastAloneDayOf(Hero npc)
        {
            try { return Current?.LastAloneDay(npc) ?? double.MaxValue; }
            catch { return double.MaxValue; }
        }

        /// <summary>How many written nights are read IN FULL in the chat thread. The freshest few
        /// only (Anton: "за последните 2-3"): a night keeps its name in memory forever, but the
        /// account of it belongs to the days when it was still fresh — and the window of the hearth
        /// holds them all anyway.</summary>
        private const int NightStoriesInThread = 3;

        /// <summary>The account of the night recorded on this day, when it is one of the freshest
        /// few; empty otherwise, and the thread then shows only the name she keeps it by.</summary>
        internal static string NightStoryInThread(Hero npc, double gameDay)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null || !self.NightsOn) return string.Empty;

                var storied = self._nightLedger!.For(npc.StringId)
                    .Where(n => n.IsStoried)
                    .OrderByDescending(n => n.GameDay)
                    .Take(NightStoriesInThread)
                    .ToList();

                var match = storied.FirstOrDefault(n => Math.Floor(n.GameDay) == Math.Floor(gameDay));
                return match?.Story?.Trim() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        // A night as one line of the list. The closed door and what she saw of him that same night
        // ride together with a comma, as Anton asked — and when she saw nothing there is simply
        // nothing after the comma.
        private string NightTimelineLine(NightRecord night)
        {
            switch (night.Kind)
            {
                case NightKind.Together:
                    return night.IsStoried && !string.IsNullOrWhiteSpace(night.Title)
                        ? $"he came to me — the night I keep as \"{night.Title.Trim()}\""
                        : "he came to me";
                case NightKind.DoorClosed:
                    return "the custom of women was upon me, and my door was closed to him";
                case NightKind.Elsewhere:
                    var other = string.IsNullOrWhiteSpace(night.OtherName) ? "another of his wives" : night.OtherName.Trim();
                    var told = night.ByHearsay
                        ? $"word reached me that he spent the night with {other}"
                        : $"he went to {other}, and not to me";
                    if (!string.IsNullOrWhiteSpace(night.OtherNightTitle))
                        told += $" — they call that night \"{night.OtherNightTitle.Trim()}\"";
                    return told;
                // Flat, and never omitted. Falling through to the default left a duty night INVISIBLE
                // in the "since we were last alone" list — the one place she is supposed to be able
                // to see it standing there unaddressed (self-review, 2026.08.15).
                case NightKind.Duty:
                    return "my door was shut and he came to me even so";
                case NightKind.Alone:
                    return "I saw him go to his own bed alone";
                default:
                    return night.AtWar ? "there was fighting, and I did not look for him" : string.Empty;
            }
        }

        private static string BattleTimelineLine(Core.Battles.BattleRecord battle)
        {
            var title = string.IsNullOrWhiteSpace(battle.Title) ? "a battle" : battle.Title.Trim();
            return $"we fought — {title}";
        }

        /// <summary>The static door the situation builder reaches through.</summary>
        internal static string NightsBlockFor(Hero npc)
        {
            try { return Current?.NightsRollFor(npc) ?? string.Empty; }
            catch { return string.Empty; }
        }

        // ------------------------------ what the window reads ------------------------------

        /// <summary>One wife, as the window's list shows her.</summary>
        internal struct NightContactInfo
        {
            public Hero Hero;
            public string Detail;
            public string SeasonText;
            public string SeasonColor;
            public int SeasonRank;
            public string Block;
        }

        /// <summary>One line of her fortnight, as the window draws it.</summary>
        internal struct NightEntryLine
        {
            public string Header;
            public string HeaderColor;
            public string Body;
            public bool IsNarration;
        }

        /// <summary>Everything the window needs about the chosen one.</summary>
        internal struct NightView
        {
            public string Status;
            public string Odds;
            public string Relation;
            public string RelationColor;
            public string GoLabel;
            public string GoHint;
            public bool CanGo;
            public List<NightEntryLine> Entries;
        }

        private const string SeasonHighColor = "#E8B4C8FF";   // the crest, in the road's own rose
        private const string SeasonClosedColor = "#9BB7D4FF"; // the days of the custom, in frost
        private const string SeasonPlainColor = "#8E8A80FF";

        internal static List<NightContactInfo> NightContacts()
        {
            var list = new List<NightContactInfo>();
            try
            {
                var self = Current;
                if (self == null || !self.NightsOn) return list;
                int today = (int)CampaignTime.Now.ToDays;

                foreach (var wife in WomenOfTheHearth())
                {
                    var block = self.NightBlockFor(wife, out _);
                    var mother = MotherOf(wife);

                    string season;
                    string color = SeasonPlainColor;
                    if (mother == null) season = string.Empty;
                    else if (mother.IsPregnant) { season = "she is carrying your child"; color = SeasonHighColor; }
                    else
                    {
                        var phase = MoodTides.PhaseOf(mother.StringId, today, out _);
                        season = MoodTides.PhaseWord(phase);
                        if (phase == MoodTides.CyclePhase.Menses) color = SeasonClosedColor;
                        else if (MoodTides.Fertility(mother.StringId, today) >= 0.85) color = SeasonHighColor;
                    }

                    list.Add(new NightContactInfo
                    {
                        Hero = wife,
                        Detail = block.Length > 0 ? Upper(block) : "she is with you",
                        SeasonText = season,
                        SeasonColor = color,
                        SeasonRank = self.NightSortKey(wife),
                        Block = block,
                    });
                }
                list = list.OrderBy(c => c.SeasonRank).ToList();
            }
            catch (Exception ex) { ModLog.Error("gathering the hearth", ex); }
            return list;
        }

        internal static NightView NightViewFor(Hero wife)
        {
            var view = new NightView { Entries = new List<NightEntryLine>(), RelationColor = SeasonPlainColor };
            try
            {
                var self = Current;
                if (self == null || wife == null || !self.NightsOn) return view;

                int today = (int)CampaignTime.Now.ToDays;
                var block = self.NightBlockFor(wife, out double hoursLeft);
                var mother = MotherOf(wife);

                view.CanGo = block.Length == 0;
                view.GoLabel = "Go to her tonight";
                view.Status = block.Length == 0
                    ? "She is with you, and the night is yours to spend."
                    : Upper(block) + ".";

                if (mother != null && mother.IsPregnant)
                    view.GoHint = "She is already carrying your child; the nights are still yours, and still worth having.";
                else if (mother != null)
                    view.GoHint = Upper(NightOdds.LikelihoodWord(
                        MoodTides.Fertility(mother.StringId, today),
                        MoodTides.DoorIsClosed(mother.StringId, today))) + ".";

                // The reckoning itself, only for a player who asked to be shown it.
                if (self._config.ShowConceptionOdds && mother != null && !mother.IsPregnant)
                {
                    double plain = NightOdds.NightlyChanceFor(mother.StringId, today,
                        VanillaDailyChanceFor(mother), 1.0, self._config.ConceptionChanceMultiplier);
                    double best = NightOdds.NightlyChanceFor(mother.StringId, today,
                        VanillaDailyChanceFor(mother), 2.0, self._config.ConceptionChanceMultiplier);
                    view.Odds = $"tonight: {NightText.Percent(plain)} plainly, up to {NightText.Percent(best)} with the grandest gift";
                }

                try
                {
                    int relation = (int)Math.Round((double)Hero.MainHero.GetRelation(wife));
                    view.Relation = relation > 0 ? $"+{relation}" : relation.ToString();
                    view.RelationColor = relation > 0 ? "#7BC47FFF" : relation < 0 ? "#D08A8AFF" : SeasonPlainColor;
                }
                catch { }

                // Her fortnight, newest last, drawn as the window's soft narration lines.
                double now = CampaignTime.Now.ToDays;
                double lastSpoke = self.LastAloneDay(wife);
                var hers = self._nightLedger!.For(wife.StringId);
                // The same rule her own sheet follows: the freshest few written nights whole, the
                // older ones folded to the names she keeps them by.
                var inFullIds = new HashSet<string>(
                    hers.Where(n => n.IsStoried).OrderByDescending(n => n.GameDay)
                        .Take(Math.Max(0, self._config.MaxNightsToldInFull)).Select(n => n.Id),
                    StringComparer.Ordinal);

                int nightsDrawn = 0;
                foreach (var night in hers)
                {
                    bool inFull = night.IsStoried && inFullIds.Contains(night.Id);
                    var body = NightText.LineFor(night, now, inFull);
                    if (string.IsNullOrWhiteSpace(body)) continue;
                    view.Entries.Add(new NightEntryLine
                    {
                        Header = night.IsStoried ? "☾ " + night.Title : string.Empty,
                        HeaderColor = night.Kind == NightKind.Together ? SeasonHighColor : SeasonClosedColor,
                        Body = body,
                        IsNarration = night.Kind != NightKind.Together,
                    });
                    nightsDrawn++;
                }
                // The empty line is about the NIGHTS, so it is judged on the nights alone — a wife
                // with a child and no nights yet must still be told the nights will stand here.
                if (nightsDrawn == 0)
                    view.Entries.Add(new NightEntryLine
                    {
                        Body = "Nothing yet. What passes between you from tonight on will stand here.",
                        IsNarration = true,
                        HeaderColor = SeasonPlainColor,
                    });
                else if (lastSpoke >= 0)
                {
                    // The same honesty her own sheet carries, so the player can see what she sees.
                    var newer = self._nightLedger.For(wife.StringId).Any(n => n.GameDay > lastSpoke);
                    if (newer)
                        view.Entries.Add(new NightEntryLine
                        {
                            Body = "— you two last had time to yourselves " + NightText.WhenPhrase(lastSpoke, now)
                                 + "; what came after has not been gone over between you.",
                            IsNarration = true,
                            HeaderColor = SeasonPlainColor,
                        });
                }

                // THE CHILDREN LAST, and that is deliberate (2026.08.10, Anton: "като го цъкна да
                // виждам раждането после историята"). The panel is bottom-anchored — the window
                // opens on the freshest night — so children put at the TOP would sit off-screen
                // until he scrolled up for them. At the foot they are the first thing he sees, and
                // within each child the hour still comes before the feast, which is the order he
                // asked for.
                view.Entries.AddRange(BirthEntriesFor(wife));
            }
            catch (Exception ex) { ModLog.Error("drawing a wife's nights", ex); }
            return view;
        }

        /// <summary>The window's own door into the evening: the same gift question the dusk asks.</summary>
        internal static void GoToHerFromWindow(Hero wife)
        {
            try { if (wife != null) Current?.AskWhatYouBring(wife); }
            catch (Exception ex) { ModLog.Error("going to her from the window", ex); }
        }

        /// <summary>The visiting switch in plain words — what it is, and what it means for you.
        /// Deliberately unpoetic: this is a control, and a control has to say what it does.</summary>
        internal static string NightVisitModeLine()
        {
            try
            {
                var self = Current;
                if (self == null) return string.Empty;
                return self.AutoVisit
                    ? "Visiting the women: AUTO. Once the hours are up, you go to one of them on your own, late in the evening. No questions asked of you, no gifts, no story written."
                    : "Visiting the women: MANUAL. You are asked at dusk, and you can also go at any hour from this window. Gifts, written nights and picking her best days are only possible this way.";
            }
            catch { return string.Empty; }
        }

        /// <summary>The same for taking care.</summary>
        internal static string NightPreventLine()
        {
            try
            {
                var self = Current;
                if (self == null) return string.Empty;
                int tenth = (int)Math.Round(Math.Max(0, Math.Min(1, self._config.CarefulNightChanceFactor)) * 100);
                return self.PreventChild
                    ? $"Trying to prevent a child: ON. On auto you go to whoever, not to whoever is nearest her season, and any night's chance of a child falls to about {tenth}% of what it would be. Small, never nothing."
                    : "Trying to prevent a child: OFF. Nothing is done against one. On auto you go to whichever wife stands nearest her season, so over a month the odds work out close to the game's own.";
            }
            catch { return string.Empty; }
        }

        internal static string NightVisitButtonText() =>
            "Visiting: " + (Current != null && Current.AutoVisit ? "Auto" : "Manual");

        internal static string NightPreventButtonText() =>
            "Prevent a child: " + (Current != null && Current.PreventChild ? "On" : "Off");

        internal static void ToggleNightAutoVisit()
        {
            try
            {
                var self = Current;
                if (self == null) return;
                self._config.NightsAutoVisit = !self._config.NightsAutoVisit;
                self._config.Save();
                self.NotifyNight(NightVisitModeLine());
            }
            catch (Exception ex) { ModLog.Error("changing how the women are visited", ex); }
        }

        internal static void ToggleNightPreventChild()
        {
            try
            {
                var self = Current;
                if (self == null) return;
                self._config.NightsPreventChild = !self._config.NightsPreventChild;
                self._config.Save();
                self.NotifyNight(NightPreventLine());
            }
            catch (Exception ex) { ModLog.Error("changing whether a child is prevented", ex); }
        }

        // ------------------------------ the notices and the test lever ------------------------------

        private void NotifyNight(string line, Color? color = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage(line, color ?? NightColor)));
            }
            catch { /* the notice is a nicety */ }
        }

        private static string Upper(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Dev: spend a night with the soul spoken with, at the grandest gift, right now —
        /// the whole pipeline (the roll, the record, the account, the beat, the keepsake) without
        /// waiting for dusk.</summary>
        internal void ForgeNightFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                if (!NightsOn)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The nights are turned off in the mod options.", SealGrey));
                    return;
                }
                if (!FamilyBuilder.AreWed(Hero.MainHero, npc))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{npc.Name} is not wed to you.", SealGrey));
                    return;
                }
                SpendTheNightWith(npc, NightGifts.Resolve(1000));
            }
            catch (Exception ex) { ModLog.Error("forging a night", ex); }
        }

        internal static void DevForgeNight(Hero npc)
        {
            try { if (npc != null) Current?.ForgeNightFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: forging a night", ex); }
        }

        /// <summary>
        /// Dev: bring the whole conception chain forward to right now — no week of waiting to find
        /// out whether it works. If a child is already begun and merely unknown, its day of knowing
        /// is pulled to today; if not, one is begun outright and revealed. Either way the next
        /// hourly tick runs the REAL path (father slot, MakePregnantAction, vanilla's own
        /// announcement, her beat, her coming to tell you), so this proves the machinery rather
        /// than pretending to.
        /// </summary>
        internal void HastenConceptionFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                if (!NightsOn)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The nights are turned off in the mod options.", SealGrey));
                    return;
                }
                var mother = MotherOf(npc);
                if (mother == null || !FamilyBuilder.AreWed(Hero.MainHero, npc))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{npc.Name} is not a wife of yours who could carry a child.", SealGrey));
                    return;
                }
                if (mother.IsPregnant)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{npc.Name} is already with child.", SealGrey));
                    return;
                }

                double now = CampaignTime.Now.ToDays;
                var pending = _nightLedger!.For(npc.StringId)
                    .LastOrDefault(n => n.Conceived && !n.Revealed);

                if (pending == null)
                {
                    pending = _nightLedger.For(npc.StringId).LastOrDefault(n => n.Kind == NightKind.Together);
                    if (pending == null)
                    {
                        pending = BuildNightRecord(npc, NightGifts.Plain);
                        _nightLedger.Add(pending, _config.MaxNightsRemembered);
                    }
                    pending.Conceived = true;
                    pending.Revealed = false;
                }
                pending.RevealDay = now;
                SaveNightLedger();

                NotifyNight($"[test] a child of yours and {npc.Name}'s is begun, and the day of knowing is now — "
                          + "the next hour will run the whole of it.", QuickenedColor);

                // And run it at once rather than waiting for the hour to turn.
                RevealDueConceptions();
            }
            catch (Exception ex) { ModLog.Error("dev: hastening a conception", ex); }
        }

        internal static void DevHastenConception(Hero npc)
        {
            try { if (npc != null) Current?.HastenConceptionFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: hastening a conception", ex); }
        }
    }
}
