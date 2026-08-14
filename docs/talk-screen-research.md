# Spike A — Hosting the map-conversation visual in our own full-screen chat window

Research date: 2026-08-14. Game: current live Bannerlord (War Sails era), decompiled from
`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`.
All decompiled sources cached in `spikeA-decomp\` beside this file (ilspycmd 8.2, `DOTNET_ROLL_FORWARD=LatestMajor`).

**Headline: the vanilla map-conversation visual is NOT a Mission and NOT tied to the dialog system.
It is a render-to-texture tableau (`SandBox.View.Map.MapConversationTableau`) drawn into a plain
Gauntlet `TextureWidget` from a prebuilt cached scene + `AgentVisuals` built purely from
`CharacterObject` data. Every type and member on the critical path is `public`. A mod can host the
exact same visual inside its own GauntletLayer movie with ~30 lines of glue and zero Harmony.**

---

## 1. THE CHAIN — what happens when the player clicks Talk on the map

Class-by-class, decompile-verified:

1. **`TaleWorlds.CampaignSystem.Conversation.CampaignMapConversation.OpenConversation(playerCD, partnerCD)`**
   (public static; the mod already calls this) → `Campaign.Current.ConversationManager.OpenMapConversation(...)`:

   ```csharp
   public void OpenMapConversation(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
   {
       (GameStateManager.Current?.ActiveState as MapState).OnMapConversationStarts(playerCharacterData, conversationPartnerData);
       SetupAndStartMapConversation(conversationPartnerData.Party?.MobileParty,
           new MapConversationAgent(conversationPartnerData.Character),
           new MapConversationAgent(CharacterObject.PlayerCharacter));
   }
   ```
   Two forks: (a) the **visual** via MapState → MapScreen, (b) the **dialog flow** via
   `SetupAndStartMapConversation` (public) which starts the dialog tree and installs the
   `IConversationStateHandler`.

2. **`MapState.OnMapConversationStarts`** → `_handler?.OnMapConversationStarts(...)`. The handler is
   `MapScreen` (implements `IMapStateHandler`):

   ```csharp
   private void HandleMapConversationInit(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
   {
       _mapViewsContainer.ForeachReverse(view => view.OnMapConversationStart());
       _menuViewContext?.OnMapConversationActivated();
       _conversationView.InitializeConversation(playerCharacterData, conversationPartnerData);
       MapCursor.SetVisible(value: false);
       ...
   }
   ```

   **Crucial:** `_conversationView` is created ONCE at MapScreen init and lives for the whole map
   session (MapScreen.OnInitialize: `AddMapView<MapConversationView>(); _conversationView = GetMapView<MapConversationView>();`).
   `AddMapView<T>` resolves through **`SandBoxViewCreator.CreateMapView<T>`**, which honors
   `[OverrideView(typeof(MapConversationView))]` → the actual instance is
   **`SandBox.GauntletUI.Map.GauntletMapConversationView`**.

3. **`GauntletMapConversationView.InitializeConversation`** (protected override):
   - `CreateConversationMissionIfMissing()` — plants **`MapConversationView.MapConversationMission`**,
     a *fake mission*: a public nested class implementing `ICampaignMission` whose ctor does
     `CampaignMission.Current = this`. It has NO agents, NO scene, mode = `MissionMode.Conversation`.
     Its only real job: receive `OnConversationPlay(idleActionId, idleFaceAnimId, reactionId,
     reactionFaceAnimId, soundPath)` from the ConversationManager and forward (or queue, pre-init) to
     the tableau via `SetConversationTableau`/`ConversationTableau` (both public).
   - `CreateConversationView()` (private):
     ```csharp
     base.Layer = new GauntletLayer("MapConversation", 205, false);
     _dataSource = new MapConversationVM(OnContinue, GetContinueKeyText);
     _conversationMovie = _layerAsGauntletLayer.LoadMovie("MapConversation", _dataSource);
     // input restrictions, hotkey categories "Generic"/"GenericPanelGameKeyCategory"/"GenericCampaignPanelsGameKeyCategory"/"ConversationHotKeyCategory"
     MapScreen.AddLayer(Layer); Layer.IsFocusLayer = true; ScreenManager.TrySetFocus(Layer);
     Campaign.Current.ConversationManager.Handler = this;               // IConversationStateHandler
     Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);  // pauses campaign
     ```
   - `CreateConversationTableau()` — gathers the tableau inputs (all from campaign statics, all public):
     ```csharp
     float timeOfDay = CampaignTime.Now.CurrentHourInDay * (24 / CampaignTime.HoursInDay);
     WeatherEvent w = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(MobileParty.MainParty.Position.ToVec2());
     string locationId = LocationComplex.Current?.GetLocationOfCharacter(partner.HeroObject)?.StringId; // null-safe
     _tableauData = MapConversationTableauData.CreateFrom(
         _playerCharacterData, _conversationPartnerData,
         Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace),
         timeOfDay, isCurrentTerrainUnderSnow, Hero.MainHero.CurrentSettlement, locationId,
         isRaining, isSnowing);
     _dataSource.TableauData = _tableauData;   // ← THE handoff to the widget
     ```

4. **The movie** — `Modules\SandBox\GUI\Prefabs\Map\MapConversation.xml` (entire body):
   ```xml
   <MapConversationScreenButtonWidget ... IsBarterActive="@IsBarterActive" Command.Click="ExecuteContinue">
     <Children>
       <Widget ... Sprite="BlankWhiteSquare_9" Color="#000000FF" />        <!-- black backdrop -->
       <Widget ...><Children>
         <MapConversationTableauWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                                       Data="@TableauData" IsEnabled="false"/>
       </Children></Widget>
       <SPConversation Id="ConversationParent" DataSource="{DialogController}" />   <!-- vanilla text UI -->
     </Children>
   </MapConversationScreenButtonWidget>
   ```
   `MapConversationVM` is trivial: `[DataSourceProperty] object TableauData`,
   `MissionConversationVM DialogController`, `bool IsBarterActive`. **The whole visual is the one
   `MapConversationTableauWidget` bound to `TableauData`.**

5. **`MapConversationTableauWidget : TextureWidget`** (public, `TaleWorlds.MountAndBlade.GauntletUI.Widgets.Map.MapConversation`):
   ctor sets `TextureProviderName = "MapConversationTextureProvider"`; its `Data` setter forwards
   `IsEnabled=(data!=null)` and `Data=value` to the texture provider. Providers are resolved by
   **bare class name** via `TextureProviderFactory.CreateInstance` — the factory's
   `RefreshProviderTypes()` scans **all AppDomain assemblies** for non-abstract `TextureProvider`
   subclasses (called from `WidgetInfo.Refresh()` at UI init, i.e. AFTER module DLLs load — so
   mod-defined providers register too; beware: `Dictionary.Add` → duplicate class names crash).

6. **`MapConversationTextureProvider : TextureProvider`** (public, SandBox.GauntletUI): ctor
   `new MapConversationTableau()`; `Data`/`IsEnabled` setters forward; `Tick(dt)` → `tableau.OnTick(dt)`;
   `OnGetTextureForRender` → `tableau.Texture`.

7. **`MapConversationTableau`** (public, SandBox.View.Map) — the actual visual machine:
   - **Scene**: `_tableauScene = ThumbnailCacheManager.Current.GetCachedMapConversationTableauScene()` —
     a single PREBUILT scene **`scn_conversation_tableau`**, read once per campaign session in
     `ThumbnailCacheManager.InitializeSandboxValues()` (public static):
     ```csharp
     Current._mapConversationScene = Scene.CreateNewScene(true, false, 0, "mono_renderscene");
     Current._mapConversationScene.Read("scn_conversation_tableau", ref initData, "");
     Current._mapConversationSceneAgentRenderer = MBAgentRendererSceneController.CreateNewAgentRendererSceneController(...);
     ```
     `GetCachedMapConversationTableauScene()` just returns it and flips a `bool _mapConversationSceneBeingUsed`.
     One shared instance — never host two tableaus at once.
   - **Backdrop** (`FirstTimeInit`): atmosphere by name from the data provider
     (`SandBoxViewSubModule.MapConversationDataProvider`, public static get + public
     `SetMapConversationDataProvider`). `DefaultMapConversationDataProvider.GetAtmosphereNameFromData`:
     time bucket (night/noon/sunset by `TimeOfDay`), then
     `Settlement == null || IsHideout` → `conv_snow_X_0` / `conv_desert_X_0` (TerrainType 2) /
     `conv_steppe_X_0` (5) / `conv_forest_X_0` (4) / `conv_plains_X_0`;
     else culture route → `conv_{culture}_tavern_0`, `conv_{culture}_lordshall_0`,
     `conv_{culture}_shipyard_X_0` (locationId "port"), default `conv_{culture}_town_X_0`.
     Then `_tableauScene.SetAtmosphereWithName(name)` + entities tagged with that name made visible;
     rain/snow entities (`raining_entity`/`snowing_entity` tags) toggled by the data booleans.
   - **Camera**: entity tag `player_infantry_to_infantry` → `GetCameraParamsFromCameraScript`. The
     PLAYER IS NEVER RENDERED — the camera stands where the player would. Only the partner (+ up to 2
     bodyguards from `Party.MemberRoster`, ordered by level, spawn tag `player_bodyguard_infantry_spawn`).
   - **The character** (`SpawnOpponentLeader`) — built ENTIRELY from `CharacterObject`/`Hero` data:
     ```csharp
     AgentVisuals.Create(new AgentVisualsData()
         .Banner(hero?.ClanBanner).Equipment(clonedEquip).Race(char.Race)
         .BodyProperties(hero?.BodyProperties ?? char.GetBodyProperties(equip, seed))
         .Frame(spawnEntity.GetGlobalFrame()).UseMorphAnims(true)
         .ActionSet(MBGlobals.GetActionSetWithSuffix(monster, isFemale, "_warrior"))
         .ActionCode(idleAction).Scene(_tableauScene).Monster(monster)
         .PrepareImmediately(true).SkeletonType(female?1:0)
         .ClothColor1(c1).ClothColor2(c2),
       "MapConversationTableau", true, false, false);
     visuals.SetLookDirection(cameraPos - eyePoint);
     MBSkeletonExtensions.SetFacialAnimation(skeleton, FacialAnimChannel.Mid, CharacterHelper.GetDefaultFaceIdle(char), ...);
     ```
     Equipment: `IsCivilianEquipmentRequiredForLeader ? FirstCivilianEquipment : FirstBattleEquipment`
     (heroes), cloned, banner item stripped. `NoHorse`/`NoWeapon` flags exist on the data struct.
   - **Render**: `TableauView.AddTableau($"MapConvTableau_{n}", CharacterTableauContinuousRenderFunction, scene, w, h)`
     → continuous render-to-texture with postfx/DOF/skybox; `SetTargetSize` follows the widget size ×
     the user's render-scale option.

Answer to "is it a Mission?": **No.** It is a tableau (offscreen scene + camera + AgentVisuals →
texture → widget) plus a fake `ICampaignMission` stub for animation routing. The campaign is paused
by `RegisterActiveStateDisableRequest`, not by a state change; `MapState` stays active.

Inputs, complete list (`MapConversationTableauData.CreateFrom`, public static):
`ConversationCharacterData playerCD` (UNUSED by the tableau — only the partner renders),
`ConversationCharacterData partnerCD` (Character required, Party optional),
`TerrainType`, `float timeOfDay` (0–24), `bool isCurrentTerrainUnderSnow`,
`Settlement` (nullable), `string locationId` (nullable; "tavern"/"lordshall"/"port" special-cased),
`bool isRaining`, `bool isSnowing`.

`ConversationCharacterData` is a public struct:
`(CharacterObject character, PartyBase party = null, bool noHorse = false, bool noWeapon = false,
bool spawnAfterFight = false, bool isCivilianEquipmentRequiredForLeader = false,
bool isCivilianEquipmentRequiredForBodyGuardCharacters = false, bool noBodyguards = false)`.

---

## 2. HOSTABILITY — can a mod instantiate the visual in its own layer?

**Yes, through public surface only.** The minimal recipe:

```csharp
// our VM:  [DataSourceProperty] public object TableauData { get; set; }
// our prefab (module GUI\Prefabs\ImmersiveTalkWindow.xml), inside our window layout:
//   <MapConversationTableauWidget WidthSizePolicy="..." HeightSizePolicy="..." Data="@TableauData" />

// 1. the coupling fix — plant the stub the tableau reports to:
var convView = MapScreen.Instance.GetMapView<MapConversationView>();       // always exists on map
if (convView.ConversationMission == null)                                   // public FIELD
    convView.ConversationMission = new MapConversationView.MapConversationMission(); // public nested class
CampaignMission.Current = null;   // optional: undo the ctor's global side-effect (public static get/set)

// 2. feed it:
vm.TableauData = MapConversationTableauData.CreateFrom(
    new ConversationCharacterData(CharacterObject.PlayerCharacter),
    new ConversationCharacterData(hero.CharacterObject, hero.PartyBelongedTo?.Party,
        isCivilianEquipmentRequiredForLeader: heroIsInTown),
    terrainType, timeOfDay, underSnow, settlementForBackdrop, locationId, raining, snowing);

// 3. the live tableau handle (for gestures), available one tick later:
MapConversationTableau tableau = convView.ConversationMission.ConversationTableau;   // public property

// 4. teardown on window close:
vm.TableauData = null;                     // widget clears provider → tableau.SetEnabled(false)/OnFinalize
convView.ConversationMission.OnFinalize(); // nulls CampaignMission.Current
convView.ConversationMission = null;
```

Why the stub is needed — the ONLY coupling in the tableau (verified, both call sites):
```csharp
// MapConversationTableau.SetData (when replacing non-null data):
(MapScreen.Instance?.GetMapView<MapConversationView>()).ConversationMission.SetConversationTableau(null);
// MapConversationTableau.OnTick (after FirstTimeInit):
(MapScreen.Instance?.GetMapView<MapConversationView>()).ConversationMission.SetConversationTableau(this);
```
`ConversationMission` is a public field; `MapConversationMission` is public with a public ctor; both
`SetConversationTableau` and `ConversationTableau` are public. No Harmony needed.

Public/internal audit of every type on the path:
- `CampaignMapConversation`, `ConversationManager` (incl. `SetupAndStartMapConversation`,
  `ConversationAnimationManager` field), `ConversationCharacterData`, `MapState`,
  `CampaignMission.Current { get; set; }` — public.
- `MapConversationView` (+ public field `ConversationMission`, public nested `MapConversationMission`),
  `MapConversationTableau` (public ctor + `SetData`/`SetEnabled`/`OnTick`/`OnConversationPlay`/`OnFinalize`),
  `MapConversationTableauData.CreateFrom`, `DefaultMapConversationDataProvider`,
  `IMapConversationDataProvider`, `SandBoxViewSubModule.MapConversationDataProvider` (+ public setter) — public.
- `MapConversationTextureProvider`, `MapConversationTableauWidget` — public.
- `ThumbnailCacheManager.Current.GetCachedMapConversationTableauScene()` — public.
- `AgentVisuals.Create`, `AgentVisualsData` fluent builder, `TableauView.AddTableau` — public.
- `GauntletMapConversationView` — public, but everything interesting in it is private; we don't need it.

Sharp edges (all avoidable):
- **Never let the widget be finalized without ever having received Data**: `MapConversationTableau.OnFinalize`
  dereferences `View`/`Texture`/`_tableauScene` unguarded → NRE if no data was ever set. Set
  `TableauData` in the same frame the window opens (vanilla does).
- **One scene instance**: `scn_conversation_tableau` is a single cached scene. Our window must never
  coexist with a real map conversation (or a second copy of itself). The mod's existing gates already
  refuse the chat window during conversations; additionally close/refuse on `MapState.MapConversationActive`.
- **Real conversations self-heal**: vanilla's `InitializeConversation` calls `DestroyConversationMission()`
  then `CreateConversationMissionIfMissing()` — a stale stub of ours would be replaced, but the shared
  scene means we must be closed by then anyway.
- **`OnTick` indexes `_agentVisuals[0]` unguarded** after init — a partner with `Character == null`
  leaves the list empty → crash. Always pass a real CharacterObject (we always have one).
- **`SetTargetSize` recreates the render target** — don't animate the widget's size; fixed layout only.
- The tableau auto-rebuilds if the partner's equipment code changes mid-display (it re-inits itself; fine).
- `RegisterActiveStateDisableRequest` is the vanilla pause; OUR window can skip it (chat window today
  doesn't pause) or adopt it — our choice, it is not required by the tableau.

---

## 3. LIVENESS — what animates, free vs driven

**Free (no calls from us, baked into the tableau):**
- Idle body animation with variation: `CharacterHelper.GetStandingBodyIdle(character, party)` picks a
  character-appropriate idle (bandits get "aggressive", wounded-after-defeat "weary"), mapped through
  `ConversationManager.ConversationAnimationManager.ConversationAnims` (public
  `Dictionary<string, ConversationAnimData>`; `ConversationAnimData` = `{ IdleAnimStart, IdleAnimLoop,
  FamilyType, MountFamilyType, Dictionary<string,string> Reactions }`). Idle changes are rate-limited
  by an 8s timer (`MinimumTimeRequiredToChangeIdleAction`).
- Facial idle: `CharacterHelper.GetDefaultFaceIdle(character)` via `MBSkeletonExtensions.SetFacialAnimation`.
- Breathing/blinking/morph: `UseMorphAnims(true)` + `_agentVisuals[0].TickVisuals()` every tick.
- Backdrop mood: DOF, bloom, skybox, rain/snow particles per data flags.

**Driven (vanilla's per-line reactions):** `ConversationManager.ProcessSentence` extracts animation
tokens **from the sentence text itself** — `MBTextManager.GetConversationAnimations(TextObject)` parses
`[ib:X]` (idle body), `[if:X]` (idle face), `[rb:X]` (reaction body), `[rf:X]` (reaction face) — and calls
`CampaignMission.Current.OnConversationPlay(ib, if, rb, rf, soundPath)` → stub → tableau:

```csharp
public void OnConversationPlay(string idleActionId, string idleFaceAnimId, string reactionId, string reactionFaceAnimId, string soundPath)
// reactionId → ConversationAnims[currentIdle].Reactions[reactionId] → SetAction (one-shot gesture)
// idleActionId → switches the standing idle (rate-limited unless a reaction rides along)
// reactionFaceAnimId / idleFaceAnimId → SetFacialAnimation
// soundPath → voice-over + Rhubarb lip-sync from an external audio file (pass "")
```

**Hosted liveness:** everything free stays free. For gestures we call the PUBLIC
`tableau.OnConversationPlay(idleId, idleFaceId, reactionId, reactionFaceId, "")` ourselves — e.g. a
talk gesture + `convo_animated` face when a reply arrives, `convo_bored` on long idle, or map the mod's
`*acted gestures*` / heart shifts to reaction ids. Valid ids come from the public `ConversationAnims`
dictionary (populated from XML; the guard: only play ids present in the dict — the tableau already
falls back gracefully via `GetDefaultAnimForCharacter`). One caveat: the tableau asserts (debug-only)
if `OnConversationPlay` arrives before first init — check `convView.ConversationMission.ConversationTableau != null`
first (it is set on the tableau's first ticked frame). Note `OnConversationPlay` consults
`ConversationManager.SpeakerAgent` only to skip animating while the PLAYER speaks — when hosted, no
conversation runs, so `SpeakerAgent` may be stale/null → guard: only call it for NPC-reply moments
(wrap in try/catch; a null SpeakerAgent chain would throw — cheap to shield).
   ⚠ Verified detail: `SpeakerAgent.Character.IsPlayerCharacter` is dereferenced UNGUARDED at the top —
   when hosting, either ensure `ConversationManager` still holds agents from a previous talk (stale is
   fine: worst case the gesture is skipped) or wrap the call; prefer wrapping.

---

## 4. PIGGYBACK OPTION — keep the real conversation, replace its UI

Where vanilla loads its conversation UI: `GauntletMapConversationView.CreateConversationView`
(private) — `new GauntletLayer("MapConversation", 205, false)`; `LoadMovie("MapConversation", _dataSource)`;
text UI = the `SPConversation` prefab bound to `MapConversationVM.DialogController`
(`MissionConversationVM`, public — holds `AnswerList`, `CurrentCharacterNameLB`, `Tick`, `ExecuteContinue`,
`OnConversationContinue`); continue-click/continue-key handled in the view's `Tick` + `OnContinue`.

Seams to replace the UI, best first:
1. **`[OverrideView(typeof(MapConversationView))]` on our own MapView subclass** — no Harmony.
   `SandBoxViewCreator.CheckOverridenViews` collects `[OverrideView]` types assignable to
   `MapView/MenuView/MissionView/ScreenBase` from every assembly referencing SandBox.View; the pick
   loop walks the candidate list BACKWARD and takes the first whose assembly is in
   `ModuleHelper.GetActiveGameAssemblies()` — mod assemblies load after SandBox.GauntletUI, so ours
   wins. MapScreen then instantiates OUR view as `_conversationView` for the whole campaign. We would
   reimplement `InitializeConversation`/`FinalizeConversation`/`Tick` (~250 lines; every used type is
   public, including `MapConversationVM`, `MissionConversationVM`, `BarterManager` events,
   `IConversationStateHandler`) and load OUR movie instead of "MapConversation".
   **Burden:** our view then serves ALL map conversations — vanilla quest/encounter dialogs included —
   so the replacement must reproduce the dialog UI (or embed `<SPConversation DataSource="{DialogController}"/>`
   inside our own movie beside our chat UI, which is legal — the sub-prefab is resolved by name).
2. Harmony-postfix `CreateConversationView`/`OnActivate` to `ReleaseMovie` + `LoadMovie("OurMovie", ourVM)`
   on THEIR layer — smaller code, same burden, plus patch fragility.
3. Overlay our own higher-order layer and hide vanilla's widgets — **not viable**: the view's `Tick`
   re-grabs focus every frame (`if (FocusedLayer != Layer) TrySetFocus(Layer)`), and the movie root is
   a full-screen click-to-continue button; constant input fights.

Lifecycle facts for piggyback: the conversation stays open until the dialog flow ends
(`ConversationManager.EndConversation` → `Handler.OnConversationUninstall` → `MapState.OnMapConversationOver`
→ `MapScreen` → `FinalizeConversation`). The mod already keeps a conversation open across async LLM
turns today, so that machinery is proven. But: switching partners means ending and reopening the whole
flow (vanilla "switch" = `InitializeConversation` again + 2-frame guard, still within the dialog
system), the campaign is force-paused, barter/escape plumbing rides along, and every vanilla dialog
line of the partner fires (greetings) unless the mod's dialog set intercepts — all the reasons the mod
moved to its own window in the first place. Away NPCs: `OpenConversation` itself never checks
distance (`MapConversationAgent` is location-free), but encounter/behavior dialog conditions assume
presence — usable only with care, and the paused-campaign feel remains.

---

## 5. AWAY NPCs — presence dependency

**The tableau has none.** Verified inputs: partner `CharacterObject` (equipment/body/race/banner via
`HeroObject`), optional `PartyBase` (bodyguards, deterministic cloth colors, face seed for non-heroes),
and the atmosphere facts. `Party == null` → bodyguard block skipped (guarded), colors default.
`Settlement == null` → terrain-based backdrop. No Agent, no Mission, no LocationComplex — the one
`LocationComplex.GetLocationOfCharacter` probe lives in the vanilla VIEW's data gathering, which we
replace. Nothing checks co-location, life state, or map presence.

For a far-away NPC we synthesize the data OURSELVES — and can even render *their* weather, not ours:
- In a settlement (`hero.CurrentSettlement != null`): pass that Settlement (+ locationId "tavern"/"lordshall"
  when we know it) → culture-correct town/tavern/hall backdrop.
- Marching (`hero.PartyBelongedTo != null`): `Campaign.Current.MapSceneWrapper.GetTerrainTypeAtPosition(party.Position)`
  (public on `IMapScene`, takes `in CampaignVec2` — no nav face needed) + Settlement null.
- Weather at their spot: `MapWeatherModel.GetWeatherEventInPosition(pos.ToVec2())`.
- Unknown → plains/noon. TimeOfDay is ours to choose (their local hour == campaign hour).

Vanilla precedent that character-only rendering is safe for anyone: the encyclopedia hero page
renders EVERY hero (alive, dead, never met) full-body via `EncyclopediaHeroPageVM.HeroCharacter =
new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace)` + `FillFrom(hero, -1, hero.IsNotable,
useCharacteristicIdleAction: true)` → `EncyclopediaCharacterTableauWidget` (a `CharacterTableauWidget`,
provider `"CharacterTableauTextureProvider"` → `TaleWorlds.MountAndBlade.View.Tableaus.CharacterTableau`).
That path feeds string-serialized codes (`BodyProperties`, `EquipmentCode`, `BannerCodeText`,
`MountCreationKey`, `CharStringId`, `Race`, `IsFemale`, `StanceIndex`, `ArmorColor1/2`, `IdleAction`,
`IdleFaceAnim`, `CustomAnimation`...) — all bindable widget properties, plus custom-animation playback
(`CharacterTableau.SetCustomAnimation/StartCustomAnimation`, loop support). No backdrop scene —
brush-styled background instead (dead heroes get `SaturationFactor = -100`).

---

## 6. SWITCHING COST — swapping the displayed character

`SetData(newData)` (public) → resets/clears the `AgentVisuals` list (old visuals `Reset()`), flags
uninitialized → next `OnTick`: `FirstTimeInit()` = atmosphere name + `SetAtmosphereWithName` + tag
visibility flips + `SpawnOpponentLeader` (AgentVisuals build with `PrepareImmediately(true)`) +
bodyguards + `ForceLoadResources` + `scene.Tick(3f)` + render-target recreate. **The scene is NOT
re-read** (cached since campaign load) and no state/mission churn occurs. This is exactly vanilla's
conversation-switch path, which gates on 2 engine frames (`_minimumAvailableConversationInstallFrame =
Utilities.EngineFrameNo + 2`) — i.e. TaleWorlds treats a full character swap as a ~2-frame operation
plus asset streaming. The continuous-render callback spin-waits `CheckResources(true)` for the new
visuals, so a heavy armor set may cost one visible hitch (sub-second) the first time it's shown; the
texture keeps displaying (black on the very first open until first render). Practical verdict: list
switching at click speed is realistic — same data flow as vanilla switching between two parties in one
encounter. If we ever want instant flips we could keep TWO tableau widgets and cross-fade, but that
doubles the shared-scene problem (one cached scene!) — don't; single widget + swap is the design.
Atmosphere-only changes also ride `SetData` (full re-init) — cheap either way.

---

## The three candidate architectures, judged

**(A) Piggyback the real conversation, replace its UI.**
Works today for co-located; the UI seam is `[OverrideView(typeof(MapConversationView))]` (clean,
no Harmony) but makes us responsible for EVERY vanilla map dialog (quests, encounters, barter),
forces the campaign pause, keeps the dialog tree firing under our chat, and away-NPC support fights
encounter assumptions. High blast radius for the thing we actually want (a window).

**(B) Own full-screen GauntletLayer hosting the map-conversation tableau.** ← RECOMMENDED
Vanilla widget (`MapConversationTableauWidget Data="@TableauData"`) + vanilla provider + vanilla
tableau, in OUR movie with OUR chat thread and OUR contact list. Needs: the 3-line
`ConversationMission` stub plant (public field/class), `MapConversationTableauData.CreateFrom(...)`
per selected NPC, teardown discipline, and a guard against real conversations. Gets: the exact Talk
look (terrain/culture/time/weather backdrop, live breathing idling reacting character), away NPCs
(with *their* local backdrop — a nice flourish the letter-desk variant can also use), ~2-frame
switching, no campaign pause, no Harmony, no dialog tree. LLM-triggered gestures via public
`tableau.OnConversationPlay` with ids from the public `ConversationAnims` dict.

**(C) Own layer with the encyclopedia-style `CharacterTableauWidget`.**
`HeroViewModel.FillFrom(hero, ...)` + `EncyclopediaCharacterTableauWidget` — renders anyone
(even the dead), custom animations, zero coupling, but NO terrain/place backdrop (flat brush), a
museum pose rather than a person standing before you, and a separate render pipeline anyway. Keep as
the FALLBACK if `scn_conversation_tableau` internals shift in a patch — and as the proven pattern that
character data alone renders anyone.

### Recommended: B. Top risks
1. **The stub + shared scene discipline** — plant `MapConversationView.ConversationMission` before
   data, clear on close, and hard-refuse/auto-close while `MapState.MapConversationActive` (or any
   mission). A leak here NREs vanilla's next Talk or double-uses the one cached scene. All rails are
   code we control; wrap open/close in try/catch and fall back to the current window (the mod's
   standard degrade).
2. **Patch fragility of engine internals** — scene name `scn_conversation_tableau`, entity tags
   (`player_infantry_spawn`, `player_infantry_to_infantry`, atmosphere tag convention), and the
   `ConversationMission` coupling are version facts, same risk class as the MapNotificationItem.xml
   override the mod already ships. Mitigation: feature-flag + graceful fallback to the portrait window;
   re-verify tags after game patches.
3. **Texture/lifetime corners** — never finalize the widget with never-set data (NRE in OnFinalize),
   never animate the widget's size (render-target churn), first-frame hitch on heavy armor;
   `OnConversationPlay` before first tick asserts and reads `ConversationManager.SpeakerAgent`
   unguarded — gate on `ConversationTableau != null` + try/catch.

### Leftover facts worth keeping
- Layer order: vanilla conversation layer is order 205, `GauntletLayer("MapConversation", 205, false)` —
  name-first ctor confirmed again, with a third bool.
- Continue key: hotkey category "ConversationHotKeyCategory", id "ContinueClick"/"ContinueKey".
- `MapConversationVM` is public with a public ctor — but our own VM with one `object TableauData`
  property is all the widget needs.
- The atmosphere provider is globally swappable (`SandBoxViewSubModule.SetMapConversationDataProvider`) —
  we should NOT swap it (global); choose backdrops by synthesizing the DATA instead.
- `ConversationAnimationManager.ConversationAnims` is populated from XML (`ConversationAnimData`) —
  ids like "normal"/"aggressive"/"weary" + per-anim `Reactions` dictionary; enumerate at runtime for
  the valid-gesture vocabulary rather than hardcoding.
- Naval: no special-casing anywhere in the tableau; a "shipyard" backdrop exists via locationId "port".
