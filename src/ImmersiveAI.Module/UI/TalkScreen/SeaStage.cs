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
    /// is (Scene + AgentVisuals + TableauView), reading the deck's OWN marks: the soul stands on
    /// <c>opponent_infantry_spawn</c>, the eye is the deck's <c>custom_camera_level</c> camera.
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
    /// The scene is read ONCE and kept for the campaign, exactly as vanilla keeps its own: reading
    /// and freeing a scene under a texture the engine may still be drawing that frame is the kind of
    /// teardown that ends in a native crash. It is let go at <c>OnGameEnd</c>.
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

            var scene = Scene.CreateNewScene(true, false);
            scene.SetName("ImmersiveSeaStage");
            scene.SetUseAdvancedWaterRendering(true);
            var init = default(SceneInitializationData);
            init.InitPhysicsWorld = true;
            init.InitFloraNodes = true;
            scene.Read(SceneName, ref init);
            scene.EnableFixedTick();
            scene.SetClothSimulationState(true);
            scene.EnableInclusiveAsyncPhysx();
            _agentRenderer = MBAgentRendererSceneController.CreateNewAgentRendererSceneController(scene);
            _scene = scene;
            return scene;
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

        private TableauView? View => Texture?.TableauView;

        internal SeaStageTableau()
        {
            View?.SetEnable(_isEnabled);
            SeaStage.Current = this;
        }

        internal void SetEnabled(bool enabled)
        {
            if (_isEnabled == enabled) return;
            if (enabled)
            {
                View?.SetEnable(false);
                View?.AddClearTask(true);
                Texture?.Release();
                Texture = TableauView.AddTableau($"ImmersiveSeaStage_{_tableauIndex++}", RenderFunction, _scene, _sizeX, _sizeY);
                Texture.TableauView.SetSceneUsesContour(false);
                Texture.TableauView.SetPointlightResolutionMultiplier(0f);
            }
            else
            {
                View?.SetEnable(false);
                View?.ClearAll(false, false);
            }
            _isEnabled = enabled;
        }

        internal void SetData(object? data)
        {
            if (ReferenceEquals(_data, data)) return;
            if (_data != null) DropVisual();
            _data = data as SeaStageData;
        }

        internal void SetTargetSize(int width, int height)
        {
            int x, y;
            if (width <= 0 || height <= 0) { x = 10; y = 10; }
            else
            {
                float scale = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.ResolutionScale) / 100f;
                x = (int)(width * scale);
                y = (int)(height * scale);
            }
            if (x == _sizeX && y == _sizeY) return;
            _sizeX = x;
            _sizeY = y;
            _cameraRatio = (float)_sizeX / _sizeY;
            View?.SetEnable(false);
            View?.AddClearTask(true);
            Texture?.Release();
            Texture = TableauView.AddTableau($"ImmersiveSeaStage_{_tableauIndex++}", RenderFunction, _scene, _sizeX, _sizeY);
        }

        internal void OnTick(float dt)
        {
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

            var view = View;
            if (view != null)
            {
                if (_camera == null) _camera = Camera.CreateCamera();
                view.SetDoNotRenderThisFrame(false);
            }
            if (_initialized && _scene != null)
            {
                // The port screen's frame tick, call for call: nothing touches the water until the
                // view says the scene is ready to draw; then wait on the water, place what stands on
                // it, and from there wait-then-tick every frame.
                if (!_soulOnDeck)
                {
                    if (view == null || !view.ReadyToRender() || !view.CheckSceneReadyToRender()) return;
                    SeaStage.WaitForTheWater(_scene);
                    SpawnSoul(_data!.Hero.CharacterObject, _spawn!);
                    _soulOnDeck = true;
                }
                SeaStage.WaitForTheWater(_scene);
                _scene.Tick(dt);
                if (++_framesDrawn == FramesUntilTrusted) CrashGuard.Survived();
            }
            _visual?.TickVisuals();
        }

        private bool _soulOnDeck;
        private GameEntity? _spawn;

        // About two seconds of the deck drawn and ticked: past the raising, past the first renders.
        private const int FramesUntilTrusted = 120;
        private int _framesDrawn;

        internal void OnFinalize()
        {
            if (ReferenceEquals(SeaStage.Current, this)) SeaStage.Current = null;
            // Closed cleanly before the frame count was reached: the deck still did not kill us.
            if (_initialized) CrashGuard.Survived();
            SeaStage.WaitForTheWater(_scene);
            View?.SetEnable(false);
            _camera?.ReleaseCameraEntity();
            _camera = null;
            _visual?.ResetNextFrame();
            _visual = null;
            View?.ClearAll(false, false);
            Texture?.Release();
            Texture = null;
            // The scene itself stays: it is the campaign's, not this widget's (see SeaStage).
            _scene = null;
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
            if (_scene == null) _scene = SeaStage.AcquireScene();
            // A deck kept from an earlier talk may still have its water simulating; a fresh one has none yet.
            else SeaStage.WaitForTheWater(_scene);
            SeaStage.SetTheWeather(_scene);
            _framesDrawn = 0;
            _soulOnDeck = false;

            _spawn = _scene.FindEntityWithTag("opponent_infantry_spawn")
                     ?? throw new InvalidOperationException("the deck has no place for them to stand");

            if (_cameraEntity == null || _baseCameraFov < 0f)
            {
                _cameraEntity = _scene.FindEntityWithTag("custom_camera_level")
                                ?? _scene.FindEntityWithTag("custom_camera_same_ship")
                                ?? throw new InvalidOperationException("the deck has no eye to look through");
                _camera = Camera.CreateCamera();
                var dof = default(Vec3);
                _cameraEntity.GetCameraParamsFromCameraScript(_camera, ref dof);
                _baseCameraFov = _camera.HorizontalFov;
            }

            // The soul is NOT placed here: like the port screen's ships, they are set on the deck in
            // OnTick once the view reports the scene ready to draw (see there).

            // A texture made before the deck existed was made around NO scene, and its render
            // function retires it on the first frame. Forget the size so this one is made anew.
            _sizeX = 0;
            _sizeY = 0;
            SetTargetSize((int)Screen.RealScreenResolutionWidth, (int)Screen.RealScreenResolutionHeight);
            _cameraRatio = Screen.RealScreenResolutionWidth / Screen.RealScreenResolutionHeight;
            View?.SetPostfxConfigParams(unchecked((int)(uint.MaxValue & 0xFFFFFBFFu)));
            View?.SetEnable(true);
            _initialized = true;
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

            var party = hero?.PartyBelongedTo?.Party;
            var colors = CharacterHelper.GetDeterministicColorsForCharacter(character, party);
            var monster = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(character.Race);
            var action = ActionIndexCache.Create(DefaultIdleFor(character, party));

            var visualsData = new AgentVisualsData()
                .Banner(hero?.ClanBanner)
                .Equipment(equipment)
                .Race(character.Race)
                .BodyProperties(hero?.BodyProperties ?? character.GetBodyProperties(equipment, -1))
                .Frame(spawn.GetGlobalFrame())
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

        // Vanilla's continuous render function, call for call.
        private void RenderFunction(EngineTexture sender, EventArgs e)
        {
            var scene = (Scene)sender.UserData;
            Texture = sender;
            var view = sender.TableauView;
            if (scene == null)
            {
                view.SetContinuousRendering(false);
                view.SetDeleteAfterRendering(true);
                return;
            }

            scene.EnsurePostfxSystem();
            scene.SetDofMode(true);
            scene.SetMotionBlurMode(false);
            scene.SetBloom(true);
            scene.SetDynamicShadowmapCascadesRadiusMultiplier(0.31f);
            view.SetRenderWithPostfx(true);
            view.SetPostfxConfigParams(unchecked((int)(uint.MaxValue & 0xFFFFFBFFu)));
            if (_camera == null) return;

            float ratio = _cameraRatio / (16f / 9f);
            _camera.SetFovHorizontal(ratio * _baseCameraFov, _cameraRatio, 0.2f, 1500f);
            view.SetCamera(_camera);
            view.SetScene(scene);
            view.SetSceneUsesSkybox(true);
            view.SetDeleteAfterRendering(false);
            view.SetContinuousRendering(true);
            view.SetClearColor(0u);
            view.SetClearGbuffer(true);
            view.DoNotClear(false);
            view.SetFocusedShadowmap(true, ref _frame.origin, 1.55f);
            scene.ForceLoadResources(true);
            bool ready;
            do
            {
                ready = _visual == null || _visual.GetVisuals().CheckResources(true);
            }
            while (!ready);
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
