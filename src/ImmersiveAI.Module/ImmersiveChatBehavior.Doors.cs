using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Courtship;
using ImmersiveAI.Core.Doors;
using ImmersiveAI.Core.Llm;
using ImmersiveAI.Core.Memory;
using ImmersiveAI.Core.Nights;
using ImmersiveAI.Core.Prompts;
using ImmersiveAI.Personas;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// THE DOORS (2026.08.15, Anton's design — and it deliberately supersedes a settled rule).
    ///
    /// The nights record said refusals were cut and the only refusal was the days of her custom:
    /// "do not reintroduce her saying no without asking." He asked. So from here a wife's or a
    /// lover's door may be shut — and the closure always carries HER OWN WRITTEN REASONS, which is
    /// the whole of the feature and not a decoration on it. A door that shuts for reasons the player
    /// cannot read is a random number generator wearing a woman's face.
    ///
    /// AND THE WAY BACK IS ALREADY BUILT, which was the discovery of the design session: there is no
    /// apologise button, no gold price and no timer. The player talks; and SHE, by her own hand and
    /// her own judgment, lays her own written reason to rest — or does not. We never programmed
    /// forgiveness. We gave her a hand to forgive with.
    ///
    /// THREE RAILS THIS FILE HOLDS:
    /// • THE SEASON IS NOT A WOUND. Her custom days are reported as themselves and can never be
    ///   gone past. "Go to her anyway" is offered for a door shut by her word or by coldness, and
    ///   NEVER for her body's own season. That line is not negotiable.
    /// • THE SPIRAL BITES, IN CODE. A duty night lays a reason down whether or not she is in a talk
    ///   and whether or not a model felt like reaching for a tool. Otherwise the whole thing is
    ///   farmable by simply not speaking to her, and the moral of the design evaporates.
    /// • NOTHING SCRIPTS THE FEELING. The mod records what happened, in the flattest words it owns.
    ///   What she makes of it has been hers since the day move_heart shipped.
    /// </summary>
    public partial class ImmersiveChatBehavior
    {
        private bool DoorsOn => _config.EnableClosedDoors && _config.EnableNights;

        private static readonly Color DoorShutColor = new Color(0.58f, 0.76f, 0.95f, 1f);  // frost
        private static readonly Color DoorEasedColor = new Color(0.93f, 0.62f, 0.72f, 1f); // rose

        // ------------------------------ what she is to him ------------------------------

        /// <summary>Which bed, if any, this is about — the door only exists where there is one.</summary>
        private BondKind BondKindOf(Hero npc, NpcMemory memory)
        {
            try
            {
                if (npc == null) return BondKind.None;
                if (FamilyBuilder.AreWed(Hero.MainHero, npc)) return BondKind.Wed;
                if (memory != null && memory.LoverBond == LoverBond.Lover) return BondKind.Lover;
                return BondKind.None;
            }
            catch { return BondKind.None; }
        }

        /// <summary>Whether her body's own season has closed it — asked apart from everything else,
        /// because it is not a wound and must never be reported as one.</summary>
        private static bool HerSeasonIsUpon(Hero npc)
        {
            try
            {
                var mother = MotherOf(npc);
                return mother != null && !mother.IsPregnant
                    && MoodTides.DoorIsClosed(mother.StringId, (int)CampaignTime.Now.ToDays);
            }
            catch { return false; }
        }

        /// <summary>Where her door stands, all told.</summary>
        private DoorStanding DoorStandingFor(Hero npc, NpcMemory memory)
        {
            try
            {
                if (npc == null || memory == null) return DoorStanding.Open;
                var bond = BondKindOf(npc, memory);
                if (bond == BondKind.None) return DoorStanding.Open;

                bool season = HerSeasonIsUpon(npc);
                if (!DoorsOn)
                {
                    // With the feature off the only refusal is the one the nights always had.
                    return season ? DoorStanding.HerSeason : DoorStanding.Open;
                }

                // NO CUSHION ONCE A BOND IS SEALED, and this bit me: HeartBands.Of holds the band up
                // to what a DECLARED stage meant, which is right while a road is being walked and
                // wrong the moment there is a bed. A lover's stage sits at Devotion or Ready
                // forever, so the cushion floored her at fondness permanently and her door could
                // never go cold at all — the exact gate the whole asymmetry was built for, dead on
                // arrival (self-review, 2026.08.15). HeartBands already says this for Betrothed and
                // beyond; a lover's bond is a seal too, it simply is not one the enum knows about.
                var band = bond == BondKind.None
                    ? HeartBands.Of(GetStanding(npc), memory.CourtshipStage)
                    : HeartBands.OfRelation(GetStanding(npc));
                return DoorReasons.StandingOf(memory.DoorReasons, season, band, bond);
            }
            catch { return DoorStanding.Open; }
        }

        /// <summary>The dusk line for a shut door, in her own first words; empty when it is open.</summary>
        private string DoorBlockFor(Hero npc)
        {
            try
            {
                if (npc == null) return string.Empty;
                var memory = LoadMemory(npc);
                var standing = DoorStandingFor(npc, memory);
                if (standing == DoorStanding.Open) return string.Empty;
                return DoorText.DuskLine(standing, memory.DoorReasons);
            }
            catch { return string.Empty; }
        }

        /// <summary>Whether "Go to her anyway" may be offered at all. NEVER during her season — the
        /// one refusal this mod has always had and the one it will not put a button beside.</summary>
        private bool CanGoToHerAnyway(Hero npc)
        {
            try
            {
                if (!DoorsOn || !_config.AllowDutyNights || npc == null) return false;
                if (!FamilyBuilder.AreWed(Hero.MainHero, npc)) return false;   // a wife owes; a lover does not
                // EVERYTHING THAT IS NOT THE DOOR STILL BINDS. Only the door is being gone through:
                // a wife three weeks' ride away is not available to be gone to at all, and the
                // hours between nights are not suspended by a quarrel. Offering it anyway put "go
                // to her anyway" beside an absent wife, and let one be spent twice in an evening
                // (self-review, 2026.08.15).
                if (PresenceBlockFor(npc, out _).Length > 0) return false;
                var memory = LoadMemory(npc);
                var standing = DoorStandingFor(npc, memory);
                return standing == DoorStanding.HerWord || standing == DoorStanding.Coldness;
            }
            catch { return false; }
        }

        // ------------------------------ the hand ------------------------------

        /// <summary>Whether her own hand on the door rides this turn — wherever there is a bed and
        /// the talk is with the one who shares it.</summary>
        private bool CanWeighTheDoor(Hero npc, bool byLetter = false)
        {
            if (!DoorsOn) return false;
            if (!(_client is IToolChatClient)) return false;
            try
            {
                if (npc == null) return false;
                if (!byLetter && !IsCoLocated(npc)) return false;
                var known = MemoryIndex.Get(NpcPaths.MemoryFile(npc), _memoryStore);
                // Cheap pre-filter first: a wife is a world fact, a lover the index already stamps.
                bool wed = Safe(() => FamilyBuilder.AreWed(Hero.MainHero, npc), false);
                bool lover = known != null && (LoverBond)known.LoverBond == LoverBond.Lover;
                return wed || lover;
            }
            catch { return false; }
        }

        /// <summary>
        /// Her hand, resolved. Mutates the LIVE memory the turn speaks from and saves at once — the
        /// live-instance discipline every mid-reply hand on memory follows, or the end-of-turn save
        /// clobbers what she just decided.
        /// </summary>
        private string ResolveWeighTheDoor(ToolCall call, Hero npc, Tools.DoorTool.Tally? door,
            NpcMemory? liveMemory)
        {
            try
            {
                if (door == null)
                    return "This is not the moment for such a reckoning — I stay in the talk.";
                if (door.Acted)
                    return "I have already settled one such thing this very breath; one is enough for a conversation.";

                var memory = liveMemory ?? LoadMemory(npc);
                double now = CampaignTime.Now.ToDays;
                var deed = Tools.DoorTool.ParseDeed(call);
                var matter = Tools.DoorTool.ParseMatter(call);
                var opens = Tools.DoorTool.ParseOpens(call);
                var note = Tools.DoorTool.ParseNote(call);

                // NEVER SILENTLY DO NOTHING (the weigh_misgivings lesson, 2026.08.09): say which
                // words were wanted, because the loop still has rounds left to correct itself.
                if (deed == null)
                    return "I did not make myself plain. Let me say exactly what I do: set_down, settle, release, revise, or reopen — one of those words.";

                // THE HANDS COME IN SWAPPED, and on the misgivings a model did it on EVERY settle
                // it made: the answer went into the matter's field and the matter into the note, so
                // nothing matched and nothing was ever laid to rest. Named fields are the first fix;
                // this is the one that holds when it reads them its own way anyway.
                if (deed == DoorReasons.ActSettle
                    && DoorReasons.HandsCameSwapped(memory.DoorReasons, matter, note))
                {
                    var swap = matter; matter = note; note = swap;
                }

                bool ok;
                string trouble;
                switch (deed)
                {
                    case DoorReasons.ActSetDown:
                        ok = DoorReasons.SetDown(memory.DoorReasons, matter, opens, now, out trouble);
                        break;
                    case DoorReasons.ActSettle:
                        ok = DoorReasons.Settle(memory.DoorReasons, matter, note, now, out trouble);
                        break;
                    case DoorReasons.ActRelease:
                        ok = DoorReasons.Release(memory.DoorReasons, matter, out trouble);
                        break;
                    case DoorReasons.ActRevise:
                    {
                        // TWO strings, and they were the same one until 2026.08.16: `matter` names
                        // which of hers she means, `reworded` is what it now says. Handed `matter`
                        // for both, a revise could only ever rewrite a thing to itself and then
                        // report that it had reworded it.
                        var reworded = Tools.DoorTool.ParseReworded(call);
                        // And they come in swapped here as readily as on a settle — the new wording
                        // is what a model has most to say, so it lands in the first field going.
                        // standingOnly:false, because a settled reason may be reworded too.
                        if (DoorReasons.HandsCameSwapped(memory.DoorReasons, matter, reworded, standingOnly: false))
                        {
                            var swap = matter; matter = reworded; reworded = swap;
                        }
                        ok = DoorReasons.Revise(memory.DoorReasons, matter, reworded, opens, out trouble);
                        break;
                    }
                    case DoorReasons.ActReopen:
                        ok = DoorReasons.Reopen(memory.DoorReasons, matter, now, out trouble);
                        break;
                    default:
                        return "I did not make myself plain; let the matter rest for now.";
                }

                if (!ok)
                    return $"It does not go down that way — {trouble}. I let it be, and stay in the talk.";

                SaveMemory(npc, memory);
                door.Acted = true;
                door.Word = matter;

                bool nowShut = DoorReasons.AnyStanding(memory.DoorReasons);
                bool freezing = deed == DoorReasons.ActSetDown || deed == DoorReasons.ActReopen;
                door.Shut = freezing;
                door.Eased = deed == DoorReasons.ActSettle || deed == DoorReasons.ActRelease;

                if (!door.ByLetter)
                {
                    // Anton's own colour language: frost when something freezes, rose when the
                    // heart clears. Not gated on ShowNpcActivity — a movement the player cannot see
                    // is indistinguishable from a broken mod.
                    var name = npc?.Name?.ToString() ?? "They";
                    if (freezing)
                        NotifyDoor($"{name}: {DoorText.StandingLabel(memory.DoorReasons)}.", DoorShutColor);
                    else if (door.Eased)
                        NotifyDoor(nowShut
                            ? $"{name} lets one thing rest. {Upper(DoorText.StandingLabel(memory.DoorReasons))}."
                            : $"{name} lets it rest. Their door is open to you again.", DoorEasedColor);
                    UI.TalkUI.OnThreadChanged(npc, markUnread: false);
                }

                switch (deed)
                {
                    case DoorReasons.ActSetDown:
                        return "It is set down, in my own words, and it stands. I speak from where I truly am — I may raise it plainly, or I may say nothing of it at all; that is mine. Nothing but a real answer takes it off the list, and no sweetness of an evening does.";
                    case DoorReasons.ActSettle:
                        return nowShut
                            ? "That one is truly answered, and I lay it to rest. It is not everything — something of mine still stands — but that much is done, and I say so in my own way."
                            : "That one is truly answered, and I lay it to rest. Nothing of mine stands now, and nothing of mine bars my door. I need not announce it; I simply am not holding it any more.";
                    case DoorReasons.ActRelease:
                        return nowShut
                            ? "I strike that out. It was never really the thing, and I will not carry what is not true."
                            : "I strike that out. It was never really the thing — and nothing of mine stands now.";
                    case DoorReasons.ActRevise:
                        return "I have reworded it. The shape of it changed, as such things do when they are talked around and not answered.";
                    default:
                        return "I take that one back up. It was not answered after all, whatever I thought at the time — and I would rather be honest about it than tidy.";
                }
            }
            catch { return "The moment does not allow it; I let the matter rest."; }
        }

        // ------------------------------ the sheet ------------------------------

        /// <summary>Her private section — rides every sheet where a bed exists, tool or no tool: a
        /// woman whose door is shut knows it while writing a letter as surely as face to face.</summary>
        private string BuildDoorTerms(Hero npc, NpcMemory memory)
        {
            try
            {
                if (!DoorsOn || npc == null || memory == null) return string.Empty;
                var bond = BondKindOf(npc, memory);
                if (bond == BondKind.None) return string.Empty;

                var playerName = Hero.MainHero?.Name?.ToString() ?? "the traveler";
                var standing = DoorStandingFor(npc, memory);

                // The coldness case has nothing written under it, so there is nothing to list —
                // and a grievance invented to fill the gap would be the mod speaking for her.
                if (standing == DoorStanding.Coldness)
                    return DoorText.ColdnessSection(playerName, bond);

                return DoorText.Section(playerName, memory.DoorReasons,
                    doorIsShut: standing == DoorStanding.HerWord);
            }
            catch { return string.Empty; }
        }

        // ------------------------------ the world's own hand ------------------------------

        /// <summary>
        /// A duty night lays one down. CODE, not prose, and not routed through her tool — see the
        /// class remarks: a closure that only deepens when she happens to be in a talk is a closure
        /// a player farms by not talking to her.
        /// </summary>
        private void LayDutyNightReason(Hero wife, string seed)
        {
            try
            {
                if (!DoorsOn || wife == null) return;
                var memory = LoadMemory(wife);
                var card = DoorText.DrawDutyReason(seed);
                if (!DoorReasons.SetDown(memory.DoorReasons, card.Text, card.Opens,
                        CampaignTime.Now.ToDays, out _, byTheWorld: true))
                    return;   // already as deep as this mechanism goes; piling on is only noise
                SaveMemory(wife, memory);
            }
            catch (Exception ex) { ModLog.Error("laying down what a duty night cost", ex); }
        }

        // ------------------------------ what the rest of the mod reads ------------------------------

        /// <summary>The plain count for the bond line in both windows.</summary>
        internal static string DoorLabelFor(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null || !self.DoorsOn) return string.Empty;
                var memory = self.LoadMemory(npc);
                if (self.BondKindOf(npc, memory) == BondKind.None) return string.Empty;
                if (self.DoorStandingFor(npc, memory) == DoorStanding.Coldness)
                    return "their door is shut, and they have given no reason";
                return DoorText.StandingLabel(memory.DoorReasons);
            }
            catch { return string.Empty; }
        }

        /// <summary>The whole of it, for the one door's page.</summary>
        internal static string DoorPageFor(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null || !self.DoorsOn) return string.Empty;
                var memory = self.LoadMemory(npc);
                if (self.BondKindOf(npc, memory) == BondKind.None) return string.Empty;
                var body = DoorText.Page(Hero.MainHero?.Name?.ToString() ?? "you", memory.DoorReasons);
                if (!string.IsNullOrEmpty(body)) return body;
                return self.DoorStandingFor(npc, memory) == DoorStanding.Coldness
                    ? "Their door is shut, and they have set down no reason for it. There is nothing to point at — "
                    + "what was there simply is not, and they will not manufacture a grievance to explain themselves."
                    : string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>Whether anything of hers stands unanswered — the cheap question, for the UI.</summary>
        internal static bool DoorIsShutFor(Hero npc)
        {
            try
            {
                var self = Current;
                if (self == null || npc == null || !self.DoorsOn) return false;
                return self.DoorStandingFor(npc, self.LoadMemory(npc)) != DoorStanding.Open;
            }
            catch { return false; }
        }

        private void NotifyDoor(string line, Color? color = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                MainThreadDispatcher.Enqueue(() =>
                    InformationManager.DisplayMessage(new InformationMessage(line, color ?? DoorShutColor)));
            }
            catch { /* the notice is a nicety */ }
        }

        // ------------------------------ DevMode levers ------------------------------

        /// <summary>Dev: shut a door by hand so the whole road back can be walked in one sitting.</summary>
        internal void ShutDoorFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                if (!DoorsOn)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Closed doors are turned off in the mod options.", SealGrey));
                    return;
                }
                var memory = LoadMemory(npc);
                if (BondKindOf(npc, memory) == BondKind.None)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{npc.Name} shares no bed with you, so there is no door to shut.", SealGrey));
                    return;
                }
                var card = DoorText.DrawDutyReason(npc.StringId + CampaignTime.Now.ToDays.ToString("0"));
                if (DoorReasons.SetDown(memory.DoorReasons, card.Text, card.Opens,
                        CampaignTime.Now.ToDays, out string trouble, byTheWorld: true))
                {
                    SaveMemory(npc, memory);
                    NotifyDoor($"[test] {npc.Name}: {DoorText.StandingLabel(memory.DoorReasons)}.", DoorShutColor);
                    UI.TalkUI.OnThreadChanged(npc, markUnread: false);
                }
                else NotifyDoor($"[test] nothing was added — {trouble}.", SealGrey);
            }
            catch (Exception ex) { ModLog.Error("dev: shutting a door", ex); }
        }

        internal static void DevShutDoor(Hero npc)
        {
            try { if (npc != null) Current?.ShutDoorFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: shutting a door", ex); }
        }

        /// <summary>Dev: clear everything standing, so the far end of the road can be reached too.</summary>
        internal void OpenDoorFor(Hero npc)
        {
            try
            {
                if (npc == null) return;
                var memory = LoadMemory(npc);
                int cleared = 0;
                foreach (var reason in memory.DoorReasons.Where(r => r != null && r.Stands).ToList())
                {
                    reason.Settled = true;
                    reason.SettledNote = "[set at rest by hand, for testing]";
                    reason.SettledDay = CampaignTime.Now.ToDays;
                    cleared++;
                }
                SaveMemory(npc, memory);
                NotifyDoor($"[test] {cleared} thing{(cleared == 1 ? string.Empty : "s")} laid to rest for {npc.Name}.",
                    DoorEasedColor);
                UI.TalkUI.OnThreadChanged(npc, markUnread: false);
            }
            catch (Exception ex) { ModLog.Error("dev: opening a door", ex); }
        }

        internal static void DevOpenDoor(Hero npc)
        {
            try { if (npc != null) Current?.OpenDoorFor(npc); }
            catch (Exception ex) { ModLog.Error("dev: opening a door", ex); }
        }
    }
}
