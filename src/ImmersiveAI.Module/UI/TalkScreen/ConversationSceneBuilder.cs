using System;
using System.Collections.Generic;
using System.Reflection;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// The ONE file that names the game's own conversation-visual types. Everything else in the talk
    /// screen speaks to it through <see cref="ConversationTableauController"/> and never learns what
    /// is behind it — so when a game patch moves these types, exactly one file has to change.
    ///
    /// What it builds: the data vanilla's map Talk hands its tableau — who stands there, and the
    /// atmosphere of the place (terrain, settlement, hour, weather). Ours differs from vanilla's in
    /// one deliberate way: the atmosphere comes from where the CHOSEN ONE stands, not from where the
    /// player does, so a wife wintering in Marunath is shown against Marunath's sky.
    ///
    /// Four things here are load-bearing, every one of them decompile-verified (2026.08.14) and none
    /// of them obvious:
    ///
    /// • THE STUB. The tableau calls <c>ConversationMission.SetConversationTableau()</c> unguarded,
    ///   so a hosted tableau needs a MapConversationMission planted on the map's conversation view
    ///   or it NREs on its first tick.
    /// • …AND ITS SIDE EFFECT MUST BE UNDONE. That mission's constructor sets
    ///   <c>CampaignMission.Current</c>, and a non-null Current makes the game believe the player is
    ///   in a mission: army management answers "disabled while in a mission" and the tournament model
    ///   changes its gear. We null it again the same breath. This is not tidiness, it is a bug.
    /// • ONE SCENE, SHARED. There is a single cached conversation scene; vanilla's own Talk uses it
    ///   too, and nothing in the engine guards against two users — it simply corrupts quietly. So we
    ///   never build while a real conversation is live, and always strike our stage on the way out.
    /// • CULTURE. The scene ships hall interiors for the six base cultures only; the War Sails
    ///   cultures have none — and those are exactly the lords Anton sails with. A settlement of an
    ///   unknown culture is therefore shown by its LAND instead of its hall, which always works.
    /// </summary>
    internal static class ConversationSceneBuilder
    {
        // Ours, and only ours — so teardown can tell our stub from a real conversation's mission and
        // never unplant something the game itself put there.
        private static MapConversationView.MapConversationMission? _stub;

        /// <summary>Builds the tableau data for one soul, or null when it cannot be built right now
        /// (no map, or the game's own conversation holds the scene).</summary>
        internal static object? BuildFor(Hero hero)
        {
            if (hero?.CharacterObject == null) return null;
            if (Campaign.Current == null) return null;

            var view = MapScreen.Instance?.GetMapView<MapConversationView>();
            if (view == null) return null;
            // A real Talk owns the one cached scene; we do not contend with it.
            if (view.IsConversationActive) return null;

            PlantStub(view);

            var place = SceneSettlementFor(hero);
            var position = PositionOf(hero, place);
            var terrain = Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(position);
            var weather = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(position.ToVec2());

            // Vanilla's own conversion of the campaign hour into the tableau's clock.
            float timeOfDay = CampaignTime.Now.CurrentHourInDay * (24 / CampaignTime.HoursInDay);

            bool underSnow = weather == MapWeatherModel.WeatherEvent.Snowy
                             || weather == MapWeatherModel.WeatherEvent.Blizzard;

            return MapConversationTableauData.CreateFrom(
                // Never read by the tableau — the camera IS the player — but the shape wants it.
                new ConversationCharacterData(CharacterObject.PlayerCharacter),
                new ConversationCharacterData(
                    hero.CharacterObject,
                    // The party is the ONE thing that decides whether hangers-on spawn behind them,
                    // and null is the way to say no (verified — none of the other flags are read).
                    // Your own people get no escort: they ride with you, so the strangers standing
                    // at their shoulder were nobody, and Anton asked for them gone (2026.08.14).
                    // A stranger keeps theirs — that following is part of who they are.
                    RidesWithPlayer(hero) ? null : hero.PartyBelongedTo?.Party,
                    isCivilianEquipmentRequiredForLeader: place != null,
                    isCivilianEquipmentRequiredForBodyGuardCharacters: place != null),
                terrain,
                timeOfDay,
                underSnow,
                place,
                LocationFor(hero, place),
                weather == MapWeatherModel.WeatherEvent.HeavyRain,
                weather == MapWeatherModel.WeatherEvent.Blizzard);
        }

        // ============================ she changes how she stands ============================
        //
        // (2026.08.14, arrived at in three goes and the third is the right one — the game's own code
        // settles it. Read this before touching it.)
        //
        // `OnConversationPlay` branches on whether a REACTION was asked for:
        //
        //     if (reactionId is not empty)   -> plays Reactions[reactionId]: a one-off motion
        //     else if (idleActionId is set)  -> plays IdleAnimStart: she TAKES THAT STANCE
        //
        // The first branch is a gesture, and gestures are what Anton threw out twice — "странно
        // движение… не съм го виждал преди". The second is what he actually remembered from talking
        // to people the vanilla way: "веднъж стои с ръка на кръста, веднъж нещо друго". A hand on
        // the hip is not a reaction at all, it is the "hip" idle, and vanilla reaches a new one
        // whenever a written line happens to carry a different animation token.
        //
        // So: NO reaction, ever. We hand her a DIFFERENT STANCE each time you speak and she settles
        // into it. (The engine's own `DoesActionContinueWithCurrentAction` means handing her the
        // same one twice does nothing visible — which is exactly why the stable-per-soul stance of
        // the previous attempt looked frozen and the reaction was doing all the moving.)
        //
        // The one piece of reflection in the feature lives here, and it is unavoidable:
        // `OnConversationPlay` keeps its entire animation body behind
        // `ConversationManager.SpeakerAgent.Character.IsPlayerCharacter`, and SpeakerAgent is set
        // ONLY by a live dialogue flow. Hosting the tableau ourselves leaves it null, so the call
        // would throw on that first line — a try/catch would hide it and nobody would ever move.
        // One private field, planted while our screen is up, put back to null after (exactly the
        // state the game itself leaves it in).

        private static FieldInfo? _speakerField;
        private static bool _speakerPlanted;

        /// <summary>Has them settle into a different way of standing, as talk moves on. False when
        /// the stage cannot carry it — the tableau exists only after its first ticked frame, and the
        /// game's own Talk owns it whenever that is running.</summary>
        internal static bool ShiftStance(Hero hero)
        {
            if (hero?.CharacterObject == null || Campaign.Current == null) return false;

            var view = MapScreen.Instance?.GetMapView<MapConversationView>();
            var tableau = view?.ConversationMission?.ConversationTableau;
            if (view == null || tableau == null || view.IsConversationActive) return false;

            if (!PlantSpeaker(hero)) return false;

            var stance = NextStanceFor(hero);
            // Reaction and reaction-face BOTH empty — that is what makes this a change of pose
            // rather than a gesture (see the note above). The last argument is a voice-over file,
            // left empty on purpose: a real path would send the engine off to load a sound and
            // lip-sync it, and our souls speak in written words.
            tableau.OnConversationPlay(stance.Idle, stance.Face, string.Empty, string.Empty, string.Empty);
            return true;
        }

        private static bool PlantSpeaker(Hero hero)
        {
            try
            {
                var manager = Campaign.Current?.ConversationManager;
                if (manager == null) return false;

                if (_speakerField == null)
                    _speakerField = typeof(ConversationManager).GetField(
                        "_speakerAgent", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_speakerField == null) return false;

                _speakerField.SetValue(manager, new MapConversationAgent(hero.CharacterObject));
                _speakerPlanted = true;
                return true;
            }
            catch { return false; }
        }

        private static void RestoreSpeaker()
        {
            if (!_speakerPlanted) return;
            try
            {
                var manager = Campaign.Current?.ConversationManager;
                if (manager != null && _speakerField != null) _speakerField.SetValue(manager, null);
            }
            catch { /* the game nulls it itself at the end of any real conversation */ }
            _speakerPlanted = false;
        }

        // How they stand while we talk. WHICH stances are available is their standing toward the
        // player — an old friend stands easy with you, someone who cannot bear you does not — but
        // WHICH ONE of them they are in right now simply moves along as the talk does, which is all
        // the "different pose each time" ever was in vanilla. All ids are real entries of the game's
        // own conversation_animations.xml (hip = a hand on the hip, the one Anton remembered).
        //
        // Where in the set each soul begins is their own (a hash of their id), so two companions
        // standing through the same conversation are not mirrors of one another.
        private static readonly Dictionary<string, int> _stanceStep =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private static (string Idle, string Face) NextStanceFor(Hero hero)
        {
            int relation = 0;
            try { relation = ImmersiveChatBehavior.RelationValue(hero); }
            catch { /* neutral is a fine assumption */ }

            string[] set;
            string face;
            if (relation >= 30) { set = Fond; face = "convo_relaxed_happy"; }
            else if (relation >= 0) { set = Easy; face = "convo_normal"; }
            else if (relation > -30) { set = Guarded; face = "convo_grave"; }
            else { set = Hostile; face = "convo_stern"; }

            var id = hero.StringId ?? string.Empty;
            _stanceStep.TryGetValue(id, out var step);
            _stanceStep[id] = step + 1;

            return (set[(StableIndex(id, set.Length) + step) % set.Length], face);
        }

        private static readonly string[] Fond = { "confident", "hip", "confident2", "normal", "confident3", "hip2" };
        private static readonly string[] Easy = { "normal", "hip", "normal2", "demure", "hip2", "confident" };
        private static readonly string[] Guarded = { "closed", "weary", "closed2", "nervous", "weary2" };
        private static readonly string[] Hostile = { "aggressive", "warrior", "aggressive2", "closed", "warrior2" };

        // FNV-1a, the same steady hand the speech styles are dealt with.
        private static int StableIndex(string? id, int count)
        {
            if (count <= 0) return 0;
            unchecked
            {
                uint hash = 2166136261u;
                foreach (var c in id ?? string.Empty) { hash ^= c; hash *= 16777619u; }
                return (int)(hash % (uint)count);
            }
        }

        /// <summary>Lets go of the stage — unplants our stub if it is still ours, and undoes the
        /// mission side effect. Safe to call twice, and safe to call having never built.</summary>
        internal static void Release()
        {
            RestoreSpeaker();

            try
            {
                var view = MapScreen.Instance?.GetMapView<MapConversationView>();
                if (view != null && _stub != null && ReferenceEquals(view.ConversationMission, _stub))
                    view.ConversationMission = null;
            }
            catch { /* the map may already be gone */ }

            try
            {
                if (_stub != null && ReferenceEquals(CampaignMission.Current, _stub))
                    CampaignMission.Current = null;
            }
            catch { /* best-effort */ }

            _stub = null;
        }

        // The tableau reaches for the conversation mission unguarded on its very first tick. Vanilla's
        // own create/destroy pair is protected, so a host plants the public field itself.
        private static void PlantStub(MapConversationView view)
        {
            if (view.ConversationMission != null) return;

            _stub = new MapConversationView.MapConversationMission();
            view.ConversationMission = _stub;

            // The ctor above sets CampaignMission.Current. Left standing, the game thinks the player
            // is inside a mission — see the class note. Undone at once, not at teardown.
            if (ReferenceEquals(CampaignMission.Current, _stub))
                CampaignMission.Current = null;
        }

        // Someone of the player's own company — they need no escort drawn at their back.
        private static bool RidesWithPlayer(Hero hero)
        {
            try { return hero.PartyBelongedTo != null && hero.PartyBelongedTo == MobileParty.MainParty; }
            catch { return false; }
        }

        /// <summary>
        /// WHICH ROOM they are standing in (2026.08.16, Anton: "a wanderer in a tavern is drawn in
        /// the town"). Vanilla's own Talk hands the tableau the speaker's real location, and we were
        /// handing it null — so every soul inside a settlement was drawn in its open air, and the
        /// wanderer you found by the fire was standing in the street.
        /// <para>
        /// The old comment argued that null was safer because "the hall sets are culture-bound". It
        /// was solving the right problem in the wrong place: <see cref="SceneSettlementFor"/> ALREADY
        /// answers null for a culture whose sets we do not ship, so a non-null <paramref name="place"/>
        /// is itself the proof that this settlement's interiors exist. The tableau wants the room's
        /// own id (a string), which is what vanilla passes it. Outside walls there is no room
        /// to be in and the answer is null, exactly as before.
        /// </para>
        /// </summary>
        private static string? LocationFor(Hero hero, Settlement? place)
        {
            if (place == null || hero == null) return null;
            try { return place.LocationComplex?.GetLocationOfCharacter(hero)?.StringId; }
            catch { return null; }   // an unknown room is simply the open air again
        }

        // Where to draw them: inside a settlement's air if they are lodged in one whose culture the
        // scene actually ships, otherwise simply on the land where they stand.
        private static Settlement? SceneSettlementFor(Hero hero)
        {
            try
            {
                var place = hero.CurrentSettlement;
                if (place == null) return null;
                return HasSceneryFor(place.Culture) ? place : null;
            }
            catch { return null; }
        }

        // The six cultures the conversation scene ships sets for. An overhaul's or an expansion's
        // culture (nord, vakken…) is not a failure — it is simply shown out of doors.
        private static bool HasSceneryFor(CultureObject? culture)
        {
            var id = culture?.StringId;
            if (string.IsNullOrEmpty(id)) return false;
            switch (id)
            {
                case "empire":
                case "sturgia":
                case "aserai":
                case "vlandia":
                case "battania":
                case "khuzait":
                    return true;
                default:
                    return false;
            }
        }

        private static CampaignVec2 PositionOf(Hero hero, Settlement? place)
        {
            try
            {
                if (place != null) return place.Position;
                if (hero.CurrentSettlement != null) return hero.CurrentSettlement.Position;
                if (hero.PartyBelongedTo != null) return hero.PartyBelongedTo.Position;
                return MobileParty.MainParty.Position;
            }
            catch { return MobileParty.MainParty.Position; }
        }
    }
}
