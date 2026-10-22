using System;
using System.IO;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using EngineTexture = TaleWorlds.Engine.Texture;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace ImmersiveAI.UI.TalkScreen
{
    /// <summary>
    /// THE SEA STAGE (2026.10.03, Anton's screenshots: vanilla talked to Nartiros on a ship's deck with
    /// the open sea behind him; our screen drew the same soul in a meadow).
    /// <para>
    /// Why it has to exist at all: the map-conversation tableau we borrow on land has NO sea. Its one
    /// scene (Native's scn_conversation_tableau) carries plains/desert/steppe/forest/snow/halls, and
    /// the default data provider maps every other terrain — OpenSea included — to plains. Vanilla
    /// never meets that gap because at sea it does not use the tableau: PlayerEncounter opens a full
    /// conversation MISSION, and War Sails' conversation_scenes.xml points every water terrain at its
    /// own deck, <c>conversation_scene_sea</c>. That deck cannot simply be handed to the game's
    /// tableau either: it lacks the camera mark and the rain/snow entities the tableau reads with no
    /// null check. So this is a small tableau of our own, built on the same public pieces vanilla's
    /// is (Scene + AgentVisuals), drawn by a SceneView into a render target as the cutscene popups
    /// are, reading the deck's OWN marks: the soul stands on <c>opponent_infantry_spawn</c>, turned to
    /// the eye, which is <c>custom_camera_same_ship</c> for one's own company and
    /// <c>custom_camera_level</c> for a soul from another ship (vanilla's own rule).
    /// </para>
    /// <para>
    /// THE FALLBACK IS THE POINT, not an afterthought: any throw while raising the deck latches the
    /// sea off for the session and puts the soul back on the land stage (<see cref="Failed"/> →
    /// <see cref="ConversationTableauController.OnSeaStageFailed"/>). A NATIVE crash cannot be caught
    /// from here, which is why the engine sequence below copies vanilla's order call for call and
    /// adds nothing of its own. The model is NOT the land tableau but War Sails' PORT SCREEN
    /// (GauntletPortScreen), which puts ships on live water outside a mission in every campaign:
    /// advanced water on before the read, the campaign's weather on the waves, nothing placed until
    /// the view is ready to draw, and the water's CPU simulation waited on before every tick, spawn
    /// and release. Two cuts modelled on anything else crashed the game at sea (2026.10.03).
    /// A native crash is remembered by <see cref="CrashGuard"/>, so the same build never repeats it.
    /// </para>
    /// <para>
    /// The scene and the view that draws it live and die TOGETHER (2026.10.22, after three crashes
    /// on the way back from an away soul): read when someone first stands on the deck, let go whole
    /// when nobody does or the screen closes, the way the game's cutscenes keep theirs. Keeping a water
    /// scene past its view, or a view parked off, both crashed natively. It is also let go at
    /// <c>OnGameEnd</c>.
    /// </para>
    /// </summary>
    internal sealed class SeaStageData
    {
        internal SeaStageData(Hero hero, float timeOfDay)
        {
            Hero = hero;
            TimeOfDay = timeOfDay;
        }

        internal Hero Hero { get; }
        internal float TimeOfDay { get; }
    }

    internal static class SeaStage
    {
        internal const string SceneName = "conversation_scene_sea";

        private static bool _failed;

        /// <summary>Latched by the first throw: this build cannot raise the deck, so the land stage
        /// carries every talk at sea for the rest of the session. Also true from the start when this
        /// very build died raising the deck in an earlier run (see <see cref="CrashGuard"/>).</summary>
        internal static bool Failed => _failed || CrashGuard.DiedHereBefore;

        /// <summary>The tableau drawing right now, so a stance can reach it. One widget, one tableau.</summary>
        internal static SeaStageTableau? Current { get; set; }

        // The deck, read once and kept (see the class note).
        private static Scene? _scene;
        private static MBAgentRendererSceneController? _agentRenderer;

        /// <summary>
        /// The deck is raised EXACTLY as War Sails raises its PORT SCREEN — GauntletPortScreen.
        /// CreateScene, the one place the game puts ships on live water outside a mission, every day
        /// in every campaign: advanced water rendering switched on BEFORE the read, a lean init (physics
        /// and flora, nothing else), then fixed tick, cloth, async physx. Two earlier cuts crashed the
        /// game natively inside the read itself, right as the water prefabs loaded (2026.10.03): the
        /// first modelled on the land tableau, the second on the sea death cutscene — neither switched
        /// the advanced water on, and both read with every default flag raised.
        /// </summary>
        internal static Scene AcquireScene()
        {
            if (_scene != null) return _scene;

            // BREADCRUMBS: a native crash leaves no trace of ours, so each step is written to the log
            // (synchronously) before it is taken — the last line standing names the call that died.
            Step("creating the scene");
            var scene = Scene.CreateNewScene(true, false);
            scene.SetName("ImmersiveSeaStage");
            scene.SetUseAdvancedWaterRendering(true);
            var init = default(SceneInitializationData);
            init.InitPhysicsWorld = true;
            init.InitFloraNodes = true;

            // The theory after three crashes inside this very read: vanilla only ever reads a sea
            // scene from a screen of its OWN, while the map is not drawing — we read it with the map
            // live underneath, its own water simulating on another thread. So the map's water is
            // waited on first, the same wait vanilla puts before every touch of a water scene.
            Step("waiting on the map's own water");
            SandBox.View.Map.MapScreen.Instance?.MapScene?.WaitWaterRendererCPUSimulation();

            // A script on the deck that throws during the read would crash the process from inside
            // native code, unseen. Listen for any managed exception while the read runs.
            Step("reading " + SceneName);
            AppDomain.CurrentDomain.FirstChanceException += LogDuringRead;
            try { scene.Read(SceneName, ref init); }
            finally { AppDomain.CurrentDomain.FirstChanceException -= LogDuringRead; }

            Step("fixed tick, cloth, async physx");
            scene.EnableFixedTick();
            scene.SetClothSimulationState(true);
            scene.EnableInclusiveAsyncPhysx();
            Step("agent renderer");
            _agentRenderer = MBAgentRendererSceneController.CreateNewAgentRendererSceneController(scene);
            _scene = scene;
            Step("the deck is raised");
            return scene;
        }

        internal static void Step(string what) => ModLog.Info("sea stage: " + what);

        private static void LogDuringRead(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;
            ModLog.Warn("sea stage: during the read — " + ex.GetType().FullName + ": " + ex.Message
                        + " @ " + (ex.StackTrace ?? new System.Diagnostics.StackTrace(1, false).ToString()));
        }

        /// <summary>The sea's weather where the player is, set as the port screen sets it: the wind
        /// from the campaign's own weather model drives the waves, and the light follows the hour.</summary>
        internal static void SetTheWeather(Scene scene)
        {
            var at = Campaign.Current.MainParty.Position;
            var atmosphere = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(at);
            float wind = MathF.Max(4f, atmosphere.NauticalInfo.WindVector.Length);
            scene.SetWaterStrength(MathF.Max(2f, wind / 4f));
            var windVector = wind * Vec2.Forward;
            scene.SetGlobalWindVelocity(in windVector);
            scene.SetPhotoAtmosphereViaTod(atmosphere.TimeInfo.TimeOfDay, wind > 20f);
        }

        /// <summary>
        /// THE WATER RULE: every engine call that touches a water scene — a tick, a spawn, a pose,
        /// a release — first waits for the water renderer's CPU simulation, which runs on a thread
        /// of its own. Vanilla does it before every tick of its sea cutscene and before every agent
        /// a mission spawns; skipping it is a race in native code, which no try/catch can see.
        /// </summary>
        internal static void WaitForTheWater(Scene? scene) => scene?.WaitWaterRendererCPUSimulation();

        /// <summary>Lets the deck go — at campaign end, when no screen can be drawing it.</summary>
        internal static void ReleaseScene()
        {
            var scene = _scene;
            _scene = null;
            if (scene == null) return;
            try
            {
                WaitForTheWater(scene);
                if (_agentRenderer != null)
                    MBAgentRendererSceneController.DestructAgentRendererSceneController(scene, _agentRenderer, false);
            }
            catch (Exception ex) { ModLog.Error("sea stage: releasing its agent renderer", ex); }
            _agentRenderer = null;
            try
            {
                scene.ClearAll();
                scene.ManualInvalidate();
            }
            catch (Exception ex) { ModLog.Error("sea stage: releasing the deck", ex); }
        }

        internal static void Fail(Exception ex)
        {
            if (_failed) return;
            _failed = true;
            CrashGuard.Survived();
            ModLog.Error("raising the ship's deck on the talk screen (the land stage carries on instead)", ex);
            // Out of the widget's tick: swapping the stage means re-binding the screen, which must
            // not happen from inside the very property propagation that is drawing it.
            MainThreadDispatcher.Enqueue(() =>
            {
                try { ConversationTableauController.OnSeaStageFailed(); }
                catch (Exception e) { ModLog.Error("sea stage: falling back to land", e); }
            });
        }
    }

    /// <summary>
    /// A native crash cannot be caught, but it can be REMEMBERED. A marker file is written just
    /// before the deck is raised and removed once it has been drawn for a while (or the raising threw
    /// a managed exception the fallback handled). Finding it at the next start means the game died
    /// right there — so that same build never tries the deck again and talks at sea go to the land
    /// stage. The marker carries the build's own identity: a NEW build gets one fresh chance, which
    /// is how a fix is ever tried at all.
    /// </summary>
    internal static class CrashGuard
    {
        private static string MarkerPath => System.IO.Path.Combine(ModConfig.ConfigDirectory, "sea_stage_raising.txt");
        private static string Build => typeof(CrashGuard).Assembly.ManifestModule.ModuleVersionId.ToString();

        private static bool? _diedHereBefore;
        private static bool _armed;

        internal static bool DiedHereBefore
        {
            get
            {
                if (_diedHereBefore.HasValue) return _diedHereBefore.Value;
                bool died = false;
                try
                {
                    if (File.Exists(MarkerPath))
                    {
                        if (File.ReadAllText(MarkerPath).Trim() == Build)
                        {
                            died = true;
                            ModLog.Info("sea stage: this build crashed the game raising the ship's deck once already — talks at sea stay on the land stage.");
                        }
                        else File.Delete(MarkerPath);
                    }
                }
                catch (Exception ex) { ModLog.Error("sea stage: reading the crash marker", ex); }
                _diedHereBefore = died;
                return died;
            }
        }

        internal static void Arm()
        {
            if (_armed) return;
            try
            {
                Directory.CreateDirectory(ModConfig.ConfigDirectory);
                File.WriteAllText(MarkerPath, Build);
                _armed = true;
            }
            catch (Exception ex) { ModLog.Error("sea stage: writing the crash marker", ex); }
        }

        internal static void Survived()
        {
            if (!_armed) return;
            _armed = false;
            try { if (File.Exists(MarkerPath)) File.Delete(MarkerPath); }
            catch (Exception ex) { ModLog.Error("sea stage: clearing the crash marker", ex); }
        }
    }

    /// <summary>The deck, drawn into a texture — vanilla's MapConversationTableau in miniature, with
    /// only what one soul on one deck needs (no escort: vanilla's deck mission shows none either).</summary>
    internal sealed class SeaStageTableau
    {
        private static int _tableauIndex;

        private Scene? _scene;
        private SeaStageData? _data;
        private bool _initialized;
        private bool _isEnabled = true;

        private Camera? _camera;
        private GameEntity? _cameraEntity;
        private float _baseCameraFov = -1f;
        private float _cameraRatio = 16f / 9f;
        private MatrixFrame _frame = MatrixFrame.Identity;
        private AgentVisuals? _visual;
        private int _sizeX;
        private int _sizeY;

        internal EngineTexture? Texture { get; private set; }

        // A full SceneView drawing into a render target — the way the game's own cutscene popups put a
        // sea on a UI texture (SceneTableau, GauntletSceneNotification). NOT a TableauView: on that
        // lighter path the deck drew but its water came out BLACK, and ReadyToRender never came
        // (2026.10.22, cut 5 live). A SceneView is the renderer the port screen and the deck mission
        // use, water and all.
        private SceneView? _view;

        internal SeaStageTableau()
        {
            SeaStage.Current = this;
        }

        internal void SetEnabled(bool enabled)
        {
            if (_isEnabled == enabled) return;
            _isEnabled = enabled;
            // Switching OFF is safe from anywhere; switching ON only from a tick (FirstTimeInit /
            // OnTick), where the scene is ticked in the same breath.
            if (!enabled && _view != null)
            {
                SeaStage.Step("the view is switched off");
                _view.SetEnable(false);
            }
        }

        private void CreateTexture()
        {
            if (_scene == null || _sizeX <= 0 || _sizeY <= 0) return;
            Texture = EngineTexture.CreateRenderTarget($"ImmersiveSeaStage_{_tableauIndex++}", _sizeX, _sizeY, true, false, false, false);
            _view = SceneView.CreateSceneView();
            _view.SetScene(_scene);
            _view.SetRenderTarget(Texture);
            _view.SetAutoDepthTargetCreation(true);
            _view.SetSceneUsesSkybox(true);
            _view.SetClearColor(4278190080u);
            // Born OFF: only a tick switches it on (see OnTick), so it never draws unticked water.
            _view.SetEnable(false);
        }

        // Lets the view and its texture go. While the screen lives the texture is only RETIRED — the
        // UI may still draw it this very frame — and released on the next tick, once the provider
        // has taken the new one (2026.10.22: switching souls at sea released a texture mid-draw and
        // the next native call, the new soul's CreateAgentVisuals, died on corrupted memory).
        private void DropTexture(bool now)
        {
            SeaStage.WaitForTheWater(_scene);
            _view?.SetEnable(false);
            _view?.AddClearTask(false);
            _view = null;
            if (now)
            {
                Texture?.Release();
                ReleaseRetired();
            }
            else if (Texture != null)
            {
                ReleaseRetired();
                _retired = Texture;
            }
            Texture = null;
        }

        private EngineTexture? _retired;

        private void ReleaseRetired()
        {
            _retired?.Release();
            _retired = null;
        }

        internal void SetData(object? data)
        {
            if (ReferenceEquals(_data, data)) return;
            if (_data != null) DropVisual();
            _data = data as SeaStageData;
            // Nobody on the deck (an away soul is drawn on the LAND stage and this widget hides): the
            // WHOLE deck goes — view, texture and scene together — and the next soul at sea gets one
            // read fresh, exactly as on a first open (which never failed). Three wrong turns, all
            // 2026.10.22, all crashes on the way back: switching the kept view on from here (water
            // drawn unticked), destroying the view but keeping the scene (the next wind setting died
            // on corrupted memory), and keeping both, merely switched off (the same native address
            // as the first). The game's own cutscenes keep this rule: scene and view live and die as
            // one (GauntletSceneNotification).
            if (_data == null && (_view != null || _scene != null))
            {
                SeaStage.Step("nobody on the deck — the deck is let go whole");
                LetTheDeckGo();
            }
        }

        private void LetTheDeckGo()
        {
            DropTexture(now: true);
            _camera?.ReleaseCameraEntity();
            _camera = null;
            _cameraEntity = null;
            _cameraTag = null;
            _baseCameraFov = -1f;
            _spawn = null;
            _scene = null;
            SeaStage.ReleaseScene();
        }

        internal void SetTargetSize(int width, int height)
        {
            // A HIDDEN widget reports no size (an away soul chosen at sea hides the deck). Rebuilding
            // the view for that, and again on the way back, crashed the game natively (2026.10.22:
            // companion → away soul → companion). The one view lives as long as the screen; only a
            // real new size makes another.
            if (width <= 0 || height <= 0) return;
            float scale = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.ResolutionScale) / 100f;
            int x = (int)(width * scale);
            int y = (int)(height * scale);
            if (x == _sizeX && y == _sizeY && _view != null) return;
            if (_view != null) SeaStage.Step("the view is made anew for " + x + "x" + y);
            _sizeX = x;
            _sizeY = y;
            _cameraRatio = (float)_sizeX / _sizeY;
            DropTexture(now: false);
            CreateTexture();
        }

        internal void OnTick(float dt)
        {
            // The provider took the new texture before this tick (see its Tick): the old one is free.
            ReleaseRetired();
            if (!_isEnabled || SeaStage.Failed) return;

            if (_data != null && !_initialized)
            {
                try { FirstTimeInit(); }
                catch (Exception ex)
                {
                    _data = null;
                    SeaStage.Fail(ex);
                    return;
                }
            }

            var view = _view;
            if (view != null && _scene != null)
            {
                ApplyRenderSettings(view, _scene);
                view.SetEnable(true);
            }
            if (_initialized && _scene != null)
            {
                // The port screen's frame tick, call for call: nothing touches the water until the
                // view says the scene is ready to draw; then wait on the water, place what stands on
                // it, and from there wait-then-tick every frame.
                if (!_soulOnDeck)
                {
                    // A TABLEAU never reports ready (2026.10.22, cut 4's first live run: the deck was
                    // read and DRAWN, the readiness never came, and ~18 s of drawing water nobody
                    // ticked crashed the game). The port screen asks it of a full-screen SceneView;
                    // vanilla's own conversation tableau never asks at all — it places at once and
                    // ticks. So wait a few frames for it, then go on regardless.
                    // And never in the frame a soul was taken off the deck: a few ticks of the scene
                    // first, so the old one's visuals are truly gone before the engine builds new
                    // ones (the switch crash above died inside CreateAgentVisuals).
                    bool ready = view != null && view.ReadyToRender() && view.CheckSceneReadyToRender();
                    ++_framesWaited;
                    if (ready)
                    {
                        SeaStage.WaitForTheWater(_scene);
                        _scene.Tick(dt);
                    }
                    if (_framesWaited < FramesBeforePlacing || (!ready && _framesWaited < FramesToWaitForReady)) return;
                    SeaStage.Step(ready
                        ? "the view is ready — placing them on the deck"
                        : "the view never said ready after " + _framesWaited + " frames — placing them on the deck anyway");
                    SeaStage.WaitForTheWater(_scene);
                    SpawnSoul(_data!.Hero.CharacterObject, _spawn!);
                    _soulOnDeck = true;
                    SeaStage.Step("first tick");
                }
                SeaStage.WaitForTheWater(_scene);
                _scene.Tick(dt);
                if (++_framesDrawn == FramesUntilTrusted)
                {
                    CrashGuard.Survived();
                    SeaStage.Step("drawn and ticked for " + FramesUntilTrusted + " frames — trusted");
                }
            }
            _visual?.TickVisuals();
        }

        private bool _soulOnDeck;
        private GameEntity? _spawn;

        // The port screen's own count of frames after its view is ready (_framesToWaitAfterInit).
        private const int FramesToWaitForReady = 10;
        private const int FramesBeforePlacing = 3;
        private int _framesWaited;

        // About two seconds of the deck drawn and ticked: past the raising, past the first renders.
        private const int FramesUntilTrusted = 120;
        private int _framesDrawn;

        internal void OnFinalize()
        {
            if (ReferenceEquals(SeaStage.Current, this)) SeaStage.Current = null;
            // Closed cleanly before the frame count was reached: the deck still did not kill us.
            if (_initialized) CrashGuard.Survived();
            SeaStage.WaitForTheWater(_scene);
            // At once, not next frame: the scene goes with it (see SetData — scene and view live
            // and die as one, so the next open reads a fresh deck).
            _visual?.Reset();
            _visual = null;
            _soulOnDeck = false;
            LetTheDeckGo();
            _initialized = false;
        }

        private void DropVisual()
        {
            _initialized = false;
            _soulOnDeck = false;
            SeaStage.WaitForTheWater(_scene);
            _visual?.Reset();
            _visual = null;
        }

        private void FirstTimeInit()
        {
            var data = _data!;
            var character = data.Hero.CharacterObject
                            ?? throw new InvalidOperationException("no character to stand on the deck");

            CrashGuard.Arm();
            if (_scene != null) SeaStage.Step("someone new steps onto the deck" + (_view == null ? " — the view is made anew" : ""));
            if (_scene == null) _scene = SeaStage.AcquireScene();
            // A deck kept from an earlier talk may still have its water simulating; a fresh one has none yet.
            else SeaStage.WaitForTheWater(_scene);
            SeaStage.SetTheWeather(_scene);
            _framesDrawn = 0;
            _framesWaited = 0;
            _soulOnDeck = false;

            _spawn = _scene.FindEntityWithTag("opponent_infantry_spawn")
                     ?? throw new InvalidOperationException("the deck has no place for them to stand");

            // Vanilla's own choice (ConversationMissionLogic.GetNavalConversationCameraTag): one of
            // the player's own company stands on the SAME deck; anyone else is hailed across from
            // another ship. "level" for everyone was the wrong ship for a wife at your side
            // (2026.10.22, Anton's vanilla screenshot beside ours).
            var eyeTag = SameShip(data.Hero) ? "custom_camera_same_ship" : "custom_camera_level";
            if (_camera == null || _cameraEntity == null || _baseCameraFov < 0f || _cameraTag != eyeTag)
            {
                _cameraTag = eyeTag;
                _cameraEntity = _scene.FindEntityWithTag(eyeTag)
                                ?? _scene.FindEntityWithTag("custom_camera_level")
                                ?? throw new InvalidOperationException("the deck has no eye to look through");
                _camera = Camera.CreateCamera();
                var dof = default(Vec3);
                _cameraEntity.GetCameraParamsFromCameraScript(_camera, ref dof);
                _baseCameraFov = _camera.HorizontalFov;
            }

            // The soul is NOT placed here: like the port screen's ships, they are set on the deck in
            // OnTick once the view reports the scene ready to draw (see there).

            // A texture asked for before the deck existed had no scene to draw, so the view is made
            // here the first time. Switching souls keeps it: the deck and its eye stay, only who
            // stands there changes.
            if (_view == null)
            {
                _sizeX = 0;
                _sizeY = 0;
                SetTargetSize((int)Screen.RealScreenResolutionWidth, (int)Screen.RealScreenResolutionHeight);
            }
            _cameraRatio = Screen.RealScreenResolutionWidth / Screen.RealScreenResolutionHeight;
            _initialized = true;
        }

        private string? _cameraTag;

        private static bool SameShip(Hero hero)
        {
            try
            {
                var party = hero.PartyBelongedTo;
                return party == null || party == MobileParty.MainParty;
            }
            catch { return true; }
        }

        // Vanilla's SpawnOpponentLeader, battle kit (a deck is not a town street) and no banner held.
        private void SpawnSoul(CharacterObject character, GameEntity spawn)
        {
            var hero = character.HeroObject;
            var equipment = (character.IsHero ? character.FirstBattleEquipment : character.BattleEquipments.First()).Clone();
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                if (!equipment[slot].IsEmpty && equipment[slot].Item.Type == ItemObject.ItemTypeEnum.Banner)
                {
                    equipment[slot] = EquipmentElement.Invalid;
                    break;
                }
            }

            // The spawn mark faces where the OTHER ship would hail from; in the deck mission the two
            // turn to each other. Here the other is the eye, so she turns to face it, upright —
            // facing the mark's way she stood with her back to the player (2026.10.22, live).
            var standAt = spawn.GetGlobalFrame();
            var toEye = _cameraEntity!.GetGlobalFrame().origin - standAt.origin;
            toEye.z = 0f;
            if (toEye.LengthSquared > 0.01f)
            {
                standAt.rotation.f = toEye.NormalizedCopy();
                standAt.rotation.u = Vec3.Up;
                standAt.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            _frame = standAt;

            var party = hero?.PartyBelongedTo?.Party;
            var colors = CharacterHelper.GetDeterministicColorsForCharacter(character, party);
            var monster = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(character.Race);
            var action = ActionIndexCache.Create(DefaultIdleFor(character, party));

            var visualsData = new AgentVisualsData()
                .Banner(hero?.ClanBanner)
                .Equipment(equipment)
                .Race(character.Race)
                .BodyProperties(hero?.BodyProperties ?? character.GetBodyProperties(equipment, -1))
                .Frame(standAt)
                .UseMorphAnims(true)
                .ActionSet(MBGlobals.GetActionSetWithSuffix(monster, character.IsFemale, "_warrior"))
                .ActionCode(in action)
                .Scene(_scene)
                .Monster(monster)
                .PrepareImmediately(true)
                .SkeletonType(character.IsFemale ? SkeletonType.Female : SkeletonType.Male)
                .ClothColor1(colors.Item1)
                .ClothColor2(colors.Item2);

            var visual = AgentVisuals.Create(visualsData, "ImmersiveSeaStage", true, false, false);
            visual.GetVisuals().GetSkeleton().TickAnimationsAndForceUpdate(0.1f, _frame, true);
            var eye = visual.GetVisuals().GetGlobalStableEyePoint(true);
            visual.SetLookDirection(_cameraEntity!.GetGlobalFrame().origin - eye);
            visual.GetVisuals().GetSkeleton().SetFacialAnimation(
                Agent.FacialAnimChannel.Mid, CharacterHelper.GetDefaultFaceIdle(character), false, true);
            visual.SetAgentLodZeroOrMaxExternal(true);
            _visual = visual;
        }

        private static string DefaultIdleFor(CharacterObject character, PartyBase? party)
        {
            var name = CharacterHelper.GetStandingBodyIdle(character, party);
            var anims = Campaign.Current?.ConversationManager?.ConversationAnimationManager?.ConversationAnims;
            if (anims != null && anims.TryGetValue(name, out var anim))
                return !string.IsNullOrEmpty(anim.IdleAnimStart) ? anim.IdleAnimStart : anim.IdleAnimLoop;
            return "act_inventory_idle_start";
        }

        /// <summary>Has them settle into another way of standing — the same change of pose the land
        /// stage makes (see ConversationSceneBuilder's note: never a reaction, always a new idle).</summary>
        internal bool PlayStance(Hero hero, string idleId, string faceId)
        {
            if (!_initialized || _visual == null || _data == null || !ReferenceEquals(_data.Hero, hero)) return false;
            var anims = Campaign.Current?.ConversationManager?.ConversationAnimationManager?.ConversationAnims;
            if (anims == null || !anims.TryGetValue(idleId, out var anim) || string.IsNullOrEmpty(anim.IdleAnimStart))
                return false;

            var action = ActionIndexCache.Create(anim.IdleAnimStart);
            SeaStage.WaitForTheWater(_scene);
            if (!_visual.DoesActionContinueWithCurrentAction(in action))
                _visual.SetAction(in action, 0f, false);
            if (!string.IsNullOrEmpty(faceId))
                _visual.GetVisuals().GetSkeleton().SetFacialAnimation(Agent.FacialAnimChannel.Mid, faceId, false, true);
            return true;
        }

        // Every frame, as the game's cutscene popup sets its view (SceneTableau.
        // PopupSceneContinuousRenderFunction), framed by vanilla's conversation camera.
        private void ApplyRenderSettings(SceneView view, Scene scene)
        {
            scene.SetShadow(true);
            scene.EnsurePostfxSystem();
            scene.SetDofMode(true);
            scene.SetMotionBlurMode(false);
            scene.SetBloom(true);
            scene.SetDynamicShadowmapCascadesRadiusMultiplier(0.31f);
            view.SetRenderWithPostfx(true);
            view.SetSceneUsesShadows(true);
            view.SetPostfxConfigParams(unchecked((int)(uint.MaxValue & 0xFFFFFBFFu)));
            view.SetSceneUsesSkybox(true);
            view.SetFocusedShadowmap(true, ref _frame.origin, 1.55f);
            if (_camera == null) return;

            float ratio = _cameraRatio / (16f / 9f);
            _camera.SetFovHorizontal(ratio * _baseCameraFov, _cameraRatio, 0.2f, 1500f);
            view.SetCamera(_camera);
        }
    }

    /// <summary>Found by the game by its type NAME (TextureProviderFactory), so the name is the
    /// contract with the widget below. Every setter is reached by reflection from inside the
    /// widget's property propagation, so none of them may throw.</summary>
    public class ImmersiveSeaStageTextureProvider : TextureProvider
    {
        private readonly SeaStageTableau _tableau = new SeaStageTableau();
        private EngineTexture? _texture;
        private UiTexture? _provided;

        public object? Data
        {
            set
            {
                try { _tableau.SetData(value); }
                catch (Exception ex) { SeaStage.Fail(ex); }
            }
        }

        public bool IsEnabled
        {
            set
            {
                try { _tableau.SetEnabled(value); }
                catch (Exception ex) { SeaStage.Fail(ex); }
            }
        }

        public override void Clear(bool clearNextFrame)
        {
            try { _tableau.OnFinalize(); }
            catch (Exception ex) { ModLog.Error("sea stage: finalizing", ex); }
            base.Clear(clearNextFrame);
        }

        private void CheckTexture()
        {
            if (_texture == _tableau.Texture) return;
            _texture = _tableau.Texture;
            _provided = _texture != null
                ? new UiTexture(new TaleWorlds.Engine.GauntletUI.EngineTexture(_texture))
                : null;
        }

        protected override UiTexture? OnGetTextureForRender(TaleWorlds.TwoDimension.TwoDimensionContext twoDimensionContext, string name)
        {
            CheckTexture();
            return _provided;
        }

        public override void SetTargetSize(int width, int height)
        {
            base.SetTargetSize(width, height);
            try { _tableau.SetTargetSize(width, height); }
            catch (Exception ex) { SeaStage.Fail(ex); }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            try
            {
                CheckTexture();
                _tableau.OnTick(dt);
            }
            catch (Exception ex) { SeaStage.Fail(ex); }
        }
    }

    /// <summary>The widget the talk screen's prefab names. Vanilla's MapConversationTableauWidget,
    /// pointed at our provider — same lifecycle, line for line.</summary>
    public class ImmersiveSeaStageWidget : TextureWidget
    {
        private object? _data;

        [Editor(false)]
        public object? Data
        {
            get => _data;
            set
            {
                if (value == _data) return;
                _data = value;
                OnPropertyChanged(value, "Data");
                SetTextureProviderProperty("IsEnabled", _data != null);
                SetTextureProviderProperty("Data", value);
                if (_data != null) _isRenderRequestedPreviousFrame = true;
            }
        }

        public ImmersiveSeaStageWidget(UIContext context) : base(context)
        {
            TextureProviderName = nameof(ImmersiveSeaStageTextureProvider);
            _isRenderRequestedPreviousFrame = false;
            UpdateTextureWidget();
            EventManager.AddAfterFinalizedCallback(OnEventManagerIsFinalized);
        }

        private void OnEventManagerIsFinalized()
        {
            if (!SetForClearNextFrame)
            {
                TextureProvider?.SetProperty("IsReleased", true);
                TextureProvider?.Clear(false);
            }
        }

        public override void OnClearTextureProvider()
        {
        }
    }
}
