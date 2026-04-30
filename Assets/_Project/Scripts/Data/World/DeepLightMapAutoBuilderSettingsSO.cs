using Project.Data.World.Design;
using UnityEngine;

namespace Project.Data.World

{
    /// <summary>
    /// DeepLight Map Auto Builder의 설정을 저장하는 ScriptableObject.
    /// Project Asset 참조와 생성 규칙만 보관한다.
    /// Scene GameObject 참조는 DeepLightMapAutoBuilderSceneContext에 저장한다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DL_MapAutoBuilderSettings",
        menuName = "DeepLight/World/Map Auto Builder Settings")]
    public class DeepLightMapAutoBuilderSettingsSO : ScriptableObject
    {
        [Header("Core References")]
        [SerializeField] private WorldMapConfigSO worldMapConfig; // 월드맵 설정 (그리드 크기, 존 크기, 경계)
        [SerializeField] private WorldMapScenarioPresetSO scenarioPreset; // 시나리오 프리셋 (Zone Rule 목록)

        [Header("Project Prefab Templates (Fallback)")]
        [SerializeField] private GameObject globalWaterPrefab; // ThirdParty/Stylized Water 3 Prefab Asset (SceneContext 미할당 시 fallback)
        [SerializeField] private GameObject underwaterAreaPrefab; // Underwater Area Prefab Asset (Phase 4+, SceneContext 미할당 시 fallback)
        [SerializeField] private GameObject bubbleParticlePrefab; // Bubble Particle Prefab Asset (Phase 4+, SceneContext 미할당 시 fallback)
        [SerializeField] private GameObject dynamicEffectPrefab; // Dynamic Effect Prefab Asset (Phase 4+, SceneContext 미할당 시 fallback)

        [Header("Generated Root Names")]
        [SerializeField] private string generatedRootName = "GeneratedWorldRoot"; // 생성될 루트 GameObject 이름
        [SerializeField] private string globalWaterRootName = "GlobalWater"; // 글로벌 워터 부모 이름
        [SerializeField] private string zoneRootParentName = "ZoneRoots"; // ZoneRoot 부모 이름
        [SerializeField] private string runtimeEnvironmentRootName = "RuntimeEnvironment"; // 런타임 환경 부모 이름
        [SerializeField] private string underwaterAreasRootName = "UnderwaterAreas"; // 수중 영역 부모 이름
        [SerializeField] private string dynamicEffectsRootName = "DynamicEffects"; // 동적 이펙트 부모 이름
        [SerializeField] private string sharedParticlesRootName = "SharedParticles"; // 공유 파티클 부모 이름
        [SerializeField] private string debugRootName = "Debug"; // 디버그 부모 이름

        [Header("Generation Flags")]
        [SerializeField] private bool createGlobalWaterClone = true; // 글로벌 워터 복제본 생성 여부
        [SerializeField] private bool createZoneRoots = true; // ZoneRoot 생성 여부 (Phase 4에서 사용)
        [SerializeField] private bool createZoneTriggers = true; // ZoneTrigger 생성 여부 (Phase 4에서 사용)
        [SerializeField] private bool createEnvironmentRoots = true; // 환경 루트 생성 여부
        [SerializeField] private bool createDebugVisuals = true; // 디버그 시각화 생성 여부

        [Header("Phase 5: Environment Generation")]
        [SerializeField] private bool createZoneUnderwaterAreas = true; // Zone별 UnderwaterArea 복제 여부 (Phase 5)
        [SerializeField] private bool createSeafloorPlaceholders = true; // Zone별 Seafloor placeholder 생성 여부 (Phase 5)
        [SerializeField] private bool cloneParticlesPerZone = false; // Zone별 Particle 복제 여부 (기본 false)
        [SerializeField] private bool cloneDynamicEffectsPerZone = false; // Zone별 DynamicEffect 복제 여부 (기본 false)
        [SerializeField] private bool generateOnlyScenarioRuleZones = false; // Scenario Rule에 있는 Zone만 생성 여부 (기본 false)

        [Header("Phase 5.5: Underwater Volume Correction")]
        [SerializeField, Tooltip("UnderwaterArea BoxCollider top이 waterLevelY보다 최소 이 값만큼 위로 올라가도록 보정한다.")]
        private float underwaterVolumeTopPadding = 20f; // 수중 볼륨 상단 패딩 (기본 20)
        [SerializeField, Tooltip("복제된 UnderwaterArea가 Ocean source를 찾지 못해 경고를 낼 경우, 가능하면 fixed/manual water level 방식으로 전환한다.")]
        private bool forceUnderwaterAreaFixedWaterLevel = true; // Fixed water level 강제 전환 여부 (기본 true)
        [SerializeField, Tooltip("fixed/manual water level로 전환 가능한 경우 사용할 수면 높이")]
        private float fixedUnderwaterWaterLevelY = 0f; // 고정 수면 높이 (기본 0)
        [SerializeField, Tooltip("Stylized Water UnderwaterArea 컴포넌트의 SerializedProperty 이름을 조사할 때만 true로 켠다.")]
        private bool logUnderwaterAreaSerializedProperties = false; // SerializedProperty 로그 출력 여부 (기본 false)

        [Header("Phase 7: Runtime Binding")]
        [SerializeField, Tooltip("Runtime Binding 생성 여부 (Phase 7)")]
        private bool createRuntimeBindings = true;
        [SerializeField, Tooltip("Runtime Zone Tracker 생성 여부 (Phase 7)")]
        private bool createRuntimeZoneTracker = true;
        [SerializeField, Tooltip("Runtime Zone Tracker 기본 활성화 여부")]
        private bool runtimeZoneTrackerEnabledByDefault = true;
        [SerializeField, Tooltip("Runtime Zone Tracker 샘플 간격 (초)")]
        private float runtimeZoneTrackerSampleInterval = 0.1f;
        [SerializeField, Tooltip("Runtime Zone Tracker Zone 변경 로그 출력 여부")]
        private bool runtimeZoneTrackerLogChanges = true;

        [Header("Phase 8: Visual Profile Runtime")]
        [SerializeField, Tooltip("Visual Profile Set (Project Asset)")]
        private WorldMapVisualProfileSetSO visualProfileSet;
        [SerializeField, Tooltip("Visual Runtime Controller 생성 여부 (Phase 8)")]
        private bool createVisualRuntimeController = true;
        [SerializeField, Tooltip("Visual Controller 기본 활성화 여부")]
        private bool visualControllerEnabledByDefault = true;
        [SerializeField, Tooltip("Visual Profile 보간 속도")]
        private float visualBlendSpeed = 2f;
        [SerializeField, Tooltip("Visual Profile 변경 로그 출력 여부")]
        private bool logVisualProfileChanges = true;

        [Header("Phase 9: Visual Adapter Binding")]
        [SerializeField, Tooltip("Visual Adapter Binding 생성 여부 (Phase 9)")]
        private bool createVisualAdapterBinding = true;
        [SerializeField, Tooltip("RuntimeVisualVolume GameObject 생성 시 Volume component의 isGlobal 기본값")]
        private bool runtimeVolumeIsGlobal = true;
        [SerializeField, Tooltip("RuntimeVisualVolume의 priority 값")]
        private float runtimeVolumePriority = 0f;

        [Header("Phase 11: Safe Runtime Rendering Finalization")]
        [SerializeField, Tooltip("Safe Runtime Rendering 활성화 여부 (Phase 11)")]
        private bool createSafeRuntimeRendering = true;
        [SerializeField, Tooltip("Visual Controller 기본 활성화 여부 (Phase 11)")]
        private bool safeRuntimeRenderingEnabledByDefault = true;
        [SerializeField, Tooltip("Visual Controller applyToAdapters 기본값 (반드시 false)")]
        private bool visualControllerApplyToAdaptersByDefault = false;
        [SerializeField, Tooltip("Safe Runtime Blend Speed")]
        private float safeRuntimeBlendSpeed = 2f;
        [SerializeField, Tooltip("RuntimeVisualVolume의 profile을 neutral 값으로 복구할지 여부")]
        private bool restoreNeutralRuntimeVisualProfileOnGenerate = true;
        [SerializeField, Tooltip("Runtime Rendering 검증 실행 여부")]
        private bool validateRuntimeRenderingAfterGenerate = true;

        [Header("Phase 12: Terrain Source Binding")]
        [SerializeField, Tooltip("Terrain Source Binding 생성 여부 (Phase 12)")]
        private bool createTerrainSourceBinding = true;
        [SerializeField, Tooltip("Scene 내 기존 Terrain 오브젝트를 clone할지 여부")]
        private bool cloneTerrainSourceFromScene = true;
        [SerializeField, Tooltip("Project Asset에서 Terrain Prefab/Model을 검색하여 clone할지 여부")]
        private bool cloneTerrainSourceFromProjectAsset = true;
        [SerializeField, Tooltip("Terrain Source가 존재할 때 Seafloor placeholder를 숨길지 여부")]
        private bool hideSeafloorPlaceholdersWhenTerrainSourceExists = true;
        [SerializeField, Tooltip("Terrain Source 루트 이름")]
        private string terrainSourceRootName = "TerrainSource";
        [SerializeField, Tooltip("생성된 Terrain 루트 이름")]
        private string generatedTerrainRootName = "Terrain";
        [SerializeField, Tooltip("Terrain Fallback Material (pink material 방지)")]
        private Material terrainFallbackMaterial;
        [SerializeField, Tooltip("Terrain Source 상세 로그 출력 여부")]
        private bool logTerrainSourceVerbose = true;

        [Header("Phase 13: Terrain Source Layout Stabilization")]
        [SerializeField, Tooltip("Terrain Source Layout 생성 여부 (Phase 13)")]
        private bool createTerrainSourceLayout = true;
        [SerializeField, Tooltip("최종 Terrain 루트 이름")]
        private string terrainLayoutRootName = "Terrain";
        [SerializeField, Tooltip("Terrain Source Staging 루트 이름")]
        private string terrainSourceStagingRootName = "TerrainSource";
        [SerializeField, Tooltip("Decorative 후보 루트 이름")]
        private string decorativeCandidateRootName = "TerrainDecorativeCandidates";
        [SerializeField, Tooltip("Disabled 후보 루트 이름")]
        private string disabledCandidateRootName = "TerrainDisabledCandidates";
        [SerializeField, Tooltip("Scene Source clone을 Base Terrain으로 우선할지 여부")]
        private bool preferSceneTerrainSource = true;
        [SerializeField, Tooltip("Project Asset은 Scene Source가 없을 때만 fallback으로 사용")]
        private bool useProjectAssetAsFallbackOnly = true;
        [SerializeField, Tooltip("최대 Base Terrain clone 개수")]
        private int maxBaseTerrainCloneCount = 1;
        [SerializeField, Tooltip("Base Terrain을 Map Bounds에 자동 맞춤")]
        private bool autoFitBaseTerrainToMapBounds = true;
        [SerializeField, Tooltip("Terrain Source의 World Transform 유지")]
        private bool preserveTerrainSourceWorldTransform = true;
        [SerializeField, Tooltip("사용하지 않는 Terrain Source Renderer 비활성화")]
        private bool disableUnusedTerrainSourceRenderers = true;
        [SerializeField, Tooltip("Terrain Layout 상세 로그 출력 여부")]
        private bool logTerrainLayoutVerbose = false;

        [Header("Phase 14: Zone Design Database")]
        [SerializeField, Tooltip("Zone Design Database 생성 여부 (Phase 14.1)")]
        private bool createZoneDesignDatabase = true;
        [SerializeField, Tooltip("Zone Design Database 생성 후 검증 실행 여부")]
        private bool validateZoneDesignDatabaseAfterGenerate = true;
        [SerializeField, Tooltip("Zone Design Database 상세 로그 출력 여부")]
        private bool logZoneDesignVerbose = false;
        [SerializeField, Tooltip("Zone Design Database (Project Asset)")]
        private WorldMapZoneDesignDatabaseSO zoneDesignDatabase;

        [Header("Phase 14.2: Zone Design Rule Interpreter")]
        [SerializeField, Tooltip("Zone Design Rule 생성 여부 (Phase 14.2)")]
        private bool createZoneDesignRules = true;
        [SerializeField, Tooltip("Zone Design Rule 생성 후 검증 실행 여부")]
        private bool validateZoneDesignRulesAfterGenerate = true;
        [SerializeField, Tooltip("Zone Design Rule 상세 로그 출력 여부")]
        private bool logZoneDesignRuleVerbose = false;
        [SerializeField, Tooltip("Zone Design Rule Database (Project Asset)")]
        private WorldMapZoneDesignRuleDatabaseSO zoneDesignRuleDatabase;

        [Header("Phase 14.3: Zone Terrain Plan Foundation")]
        [SerializeField, Tooltip("Zone Terrain Plan 생성 여부 (Phase 14.3)")]
        private bool createZoneTerrainPlans = true;
        [SerializeField, Tooltip("Zone Terrain Plan 생성 후 검증 실행 여부")]
        private bool validateZoneTerrainPlansAfterGenerate = true;
        [SerializeField, Tooltip("Zone Terrain Plan 상세 로그 출력 여부")]
        private bool logZoneTerrainPlanVerbose = false;
        [SerializeField, Tooltip("Zone Terrain Plan Database (Project Asset)")]
        private WorldMapZoneTerrainPlanDatabaseSO zoneTerrainPlanDatabase;

        [Header("Phase 14.4: Zone Terrain Plan Mesh Patch Generation")]
        [SerializeField, Tooltip("Zone Terrain Patch 생성 여부 (Phase 14.4)")]
        private bool createZoneTerrainPatches = true;
        [SerializeField, Tooltip("Zone Terrain Patch 생성 후 검증 실행 여부")]
        private bool validateZoneTerrainPatchesAfterGenerate = true;
        [SerializeField, Tooltip("Zone Terrain Patch 상세 로그 출력 여부")]
        private bool logZoneTerrainPatchVerbose = false;
        [SerializeField, Tooltip("Terrain Patch 해상도 (기본 32 = 33x33 vertices)")]
        private int terrainPatchResolution = 32;
        [SerializeField, Tooltip("Terrain Patch Material (없으면 fallback 검색)")]
        private Material terrainPatchMaterial;
        [SerializeField, Tooltip("Terrain Patch MeshCollider 생성 여부")]
        private bool createTerrainPatchMeshCollider = true;
        [SerializeField, Tooltip("Terrain Patch 생성 시 기존 Seafloor placeholder 숨김 여부")]
        private bool hideLegacySeafloorPlaceholdersWhenPatchExists = true;
        [SerializeField, Tooltip("인접 Zone 경계 seam 블렌드 폭 (unit)")]
        private float terrainPatchSeamBlendWidth = 20f;
        [SerializeField, Tooltip("Terrain Patch 노이즈 스케일")]
        private float terrainPatchNoiseScale = 0.015f;
        [SerializeField, Tooltip("Terrain Patch 노이즈 강도")]
        private float terrainPatchNoiseStrength = 8f;

        [Header("Phase 14.5: Zone Terrain Patch Interior Detail")]
        [SerializeField, Tooltip("Terrain Patch 내부 디테일 변형 생성 여부 (Phase 14.5)")]
        private bool createZoneTerrainPatchInteriorDetail = true;
        [SerializeField, Tooltip("Terrain Patch 내부 디테일 변형 강도 (0=없음, 1=최대)")]
        private float terrainPatchInteriorDetailStrength = 0.6f;
        [SerializeField, Tooltip("Terrain Patch 경계 edge falloff 폭 (vertex 단위, seam 보호용)")]
        private int terrainPatchEdgeFalloffWidth = 3;
        [SerializeField, Tooltip("Terrain Patch 내부 디테일 생성 후 검증 실행 여부")]
        private bool validateZoneTerrainPatchInteriorDetailAfterGenerate = true;
        [SerializeField, Tooltip("Terrain Patch 내부 디테일 노이즈 스케일 (PerlinNoise 주파수)")]
        private float terrainPatchInteriorNoiseScale = 0.025f;
        [SerializeField, Tooltip("Terrain Patch 내부 디테일 노이즈 강도 (최대 높이 변화)")]
        private float terrainPatchInteriorNoiseStrength = 6f;
        [SerializeField, Tooltip("Terrain Patch 내부 디테일 canyon/cliff 추가 강도 승수")]
        private float terrainPatchInteriorFeatureStrength = 1.2f;
        [SerializeField, Tooltip("Terrain Patch 내부 디테일 상세 로그 출력 여부")]
        private bool logZoneTerrainPatchInteriorDetailVerbose = false;

        [Header("Phase 14.6: Zone Content Placeholder Foundation")]
        [SerializeField, Tooltip("Zone Content Placeholder 생성 여부 (Phase 14.6)")]
        private bool createZoneContentPlaceholders = true;
        [SerializeField, Tooltip("Zone Content Placeholder 생성 후 검증 실행 여부")]
        private bool validateZoneContentPlaceholdersAfterGenerate = true;
        [SerializeField, Tooltip("Zone Content Placeholder 상세 로그 출력 여부")]
        private bool logZoneContentPlaceholderVerbose = false;
        [SerializeField, Tooltip("Visible Content Debug Marker 생성 여부")]
        private bool createVisibleContentDebugMarkers = true;
        [SerializeField, Tooltip("Zone 당 최대 Resource Marker 개수")]
        private int maxResourceMarkersPerZone = 4;
        [SerializeField, Tooltip("Zone 당 최대 Hazard Marker 개수")]
        private int maxHazardMarkersPerZone = 3;
        [SerializeField, Tooltip("Zone 당 최대 Landmark Marker 개수")]
        private int maxLandmarkMarkersPerZone = 2;

        [Header("Logging")]



        [SerializeField] private bool logVerbose = true; // 상세 로그 출력 여부


        // ===== Public Getters =====

        /// <summary>월드맵 설정 (그리드 크기, 존 크기, 경계)</summary>
        public WorldMapConfigSO WorldMapConfig => worldMapConfig;

        /// <summary>시나리오 프리셋 (Zone Rule 목록)</summary>
        public WorldMapScenarioPresetSO ScenarioPreset => scenarioPreset;

        /// <summary>GlobalWater Prefab Asset (ThirdParty/Stylized Water 3, SceneContext 미할당 시 fallback)</summary>
        public GameObject GlobalWaterPrefab => globalWaterPrefab;

        /// <summary>Underwater Area Prefab Asset (Phase 4+, SceneContext 미할당 시 fallback)</summary>
        public GameObject UnderwaterAreaPrefab => underwaterAreaPrefab;

        /// <summary>Bubble Particle Prefab Asset (Phase 4+, SceneContext 미할당 시 fallback)</summary>
        public GameObject BubbleParticlePrefab => bubbleParticlePrefab;

        /// <summary>Dynamic Effect Prefab Asset (Phase 4+, SceneContext 미할당 시 fallback)</summary>
        public GameObject DynamicEffectPrefab => dynamicEffectPrefab;

        /// <summary>생성될 루트 GameObject 이름</summary>
        public string GeneratedRootName => string.IsNullOrEmpty(generatedRootName) ? "GeneratedWorldRoot" : generatedRootName;

        /// <summary>글로벌 워터 부모 이름</summary>
        public string GlobalWaterRootName => string.IsNullOrEmpty(globalWaterRootName) ? "GlobalWater" : globalWaterRootName;

        /// <summary>ZoneRoot 부모 이름</summary>
        public string ZoneRootParentName => string.IsNullOrEmpty(zoneRootParentName) ? "ZoneRoots" : zoneRootParentName;

        /// <summary>런타임 환경 부모 이름</summary>
        public string RuntimeEnvironmentRootName => string.IsNullOrEmpty(runtimeEnvironmentRootName) ? "RuntimeEnvironment" : runtimeEnvironmentRootName;

        /// <summary>수중 영역 부모 이름</summary>
        public string UnderwaterAreasRootName => string.IsNullOrEmpty(underwaterAreasRootName) ? "UnderwaterAreas" : underwaterAreasRootName;

        /// <summary>동적 이펙트 부모 이름</summary>
        public string DynamicEffectsRootName => string.IsNullOrEmpty(dynamicEffectsRootName) ? "DynamicEffects" : dynamicEffectsRootName;

        /// <summary>공유 파티클 부모 이름</summary>
        public string SharedParticlesRootName => string.IsNullOrEmpty(sharedParticlesRootName) ? "SharedParticles" : sharedParticlesRootName;

        /// <summary>디버그 부모 이름</summary>
        public string DebugRootName => string.IsNullOrEmpty(debugRootName) ? "Debug" : debugRootName;

        /// <summary>글로벌 워터 복제본 생성 여부</summary>
        public bool CreateGlobalWaterClone => createGlobalWaterClone;

        /// <summary>ZoneRoot 생성 여부 (Phase 4에서 사용)</summary>
        public bool CreateZoneRoots => createZoneRoots;

        /// <summary>ZoneTrigger 생성 여부 (Phase 4에서 사용)</summary>
        public bool CreateZoneTriggers => createZoneTriggers;

        /// <summary>환경 루트 생성 여부</summary>
        public bool CreateEnvironmentRoots => createEnvironmentRoots;

        /// <summary>디버그 시각화 생성 여부</summary>
        public bool CreateDebugVisuals => createDebugVisuals;

        /// <summary>Zone별 UnderwaterArea 복제 여부 (Phase 5)</summary>
        public bool CreateZoneUnderwaterAreas => createZoneUnderwaterAreas;

        /// <summary>Zone별 Seafloor placeholder 생성 여부 (Phase 5)</summary>
        public bool CreateSeafloorPlaceholders => createSeafloorPlaceholders;

        /// <summary>Zone별 Particle 복제 여부 (기본 false)</summary>
        public bool CloneParticlesPerZone => cloneParticlesPerZone;

        /// <summary>Zone별 DynamicEffect 복제 여부 (기본 false)</summary>
        public bool CloneDynamicEffectsPerZone => cloneDynamicEffectsPerZone;

        /// <summary>Scenario Rule에 있는 Zone만 생성 여부 (기본 false)</summary>
        public bool GenerateOnlyScenarioRuleZones => generateOnlyScenarioRuleZones;

        /// <summary>UnderwaterArea BoxCollider top이 waterLevelY보다 최소 이 값만큼 위로 올라가도록 보정한다. (Phase 5.5)</summary>
        public float UnderwaterVolumeTopPadding => underwaterVolumeTopPadding;

        /// <summary>복제된 UnderwaterArea가 Ocean source를 찾지 못해 경고를 낼 경우, 가능하면 fixed/manual water level 방식으로 전환한다. (Phase 5.5)</summary>
        public bool ForceUnderwaterAreaFixedWaterLevel => forceUnderwaterAreaFixedWaterLevel;

        /// <summary>fixed/manual water level로 전환 가능한 경우 사용할 수면 높이 (Phase 5.5)</summary>
        public float FixedUnderwaterWaterLevelY => fixedUnderwaterWaterLevelY;

        /// <summary>Stylized Water UnderwaterArea 컴포넌트의 SerializedProperty 이름을 조사할 때만 true로 켠다. (Phase 5.5)</summary>
        public bool LogUnderwaterAreaSerializedProperties => logUnderwaterAreaSerializedProperties;

        /// <summary>Runtime Binding 생성 여부 (Phase 7)</summary>
        public bool CreateRuntimeBindings => createRuntimeBindings;

        /// <summary>Runtime Zone Tracker 생성 여부 (Phase 7)</summary>
        public bool CreateRuntimeZoneTracker => createRuntimeZoneTracker;

        /// <summary>Runtime Zone Tracker 기본 활성화 여부</summary>
        public bool RuntimeZoneTrackerEnabledByDefault => runtimeZoneTrackerEnabledByDefault;

        /// <summary>Runtime Zone Tracker 샘플 간격 (초)</summary>
        public float RuntimeZoneTrackerSampleInterval => runtimeZoneTrackerSampleInterval;

        /// <summary>Runtime Zone Tracker Zone 변경 로그 출력 여부</summary>
        public bool RuntimeZoneTrackerLogChanges => runtimeZoneTrackerLogChanges;

        /// <summary>Visual Profile Set (Project Asset)</summary>
        public WorldMapVisualProfileSetSO VisualProfileSet => visualProfileSet;

        /// <summary>Visual Runtime Controller 생성 여부 (Phase 8)</summary>
        public bool CreateVisualRuntimeController => createVisualRuntimeController;

        /// <summary>Visual Controller 기본 활성화 여부</summary>
        public bool VisualControllerEnabledByDefault => visualControllerEnabledByDefault;

        /// <summary>Visual Profile 보간 속도</summary>
        public float VisualBlendSpeed => visualBlendSpeed;

        /// <summary>Visual Profile 변경 로그 출력 여부</summary>
        public bool LogVisualProfileChanges => logVisualProfileChanges;

        /// <summary>Visual Adapter Binding 생성 여부 (Phase 9)</summary>
        public bool CreateVisualAdapterBinding => createVisualAdapterBinding;

        /// <summary>RuntimeVisualVolume GameObject 생성 시 Volume component의 isGlobal 기본값</summary>
        public bool RuntimeVolumeIsGlobal => runtimeVolumeIsGlobal;

        /// <summary>RuntimeVisualVolume의 priority 값</summary>
        public float RuntimeVolumePriority => runtimeVolumePriority;

        /// <summary>Safe Runtime Rendering 활성화 여부 (Phase 11)</summary>
        public bool CreateSafeRuntimeRendering => createSafeRuntimeRendering;

        /// <summary>Visual Controller 기본 활성화 여부 (Phase 11)</summary>
        public bool SafeRuntimeRenderingEnabledByDefault => safeRuntimeRenderingEnabledByDefault;

        /// <summary>Visual Controller applyToAdapters 기본값 (반드시 false)</summary>
        public bool VisualControllerApplyToAdaptersByDefault => visualControllerApplyToAdaptersByDefault;

        /// <summary>Safe Runtime Blend Speed</summary>
        public float SafeRuntimeBlendSpeed => safeRuntimeBlendSpeed;

        /// <summary>RuntimeVisualVolume의 profile을 neutral 값으로 복구할지 여부</summary>
        public bool RestoreNeutralRuntimeVisualProfileOnGenerate => restoreNeutralRuntimeVisualProfileOnGenerate;

        /// <summary>Runtime Rendering 검증 실행 여부</summary>
        public bool ValidateRuntimeRenderingAfterGenerate => validateRuntimeRenderingAfterGenerate;

        /// <summary>Terrain Source Binding 생성 여부 (Phase 12)</summary>
        public bool CreateTerrainSourceBinding => createTerrainSourceBinding;

        /// <summary>Scene 내 기존 Terrain 오브젝트를 clone할지 여부</summary>
        public bool CloneTerrainSourceFromScene => cloneTerrainSourceFromScene;

        /// <summary>Project Asset에서 Terrain Prefab/Model을 검색하여 clone할지 여부</summary>
        public bool CloneTerrainSourceFromProjectAsset => cloneTerrainSourceFromProjectAsset;

        /// <summary>Terrain Source가 존재할 때 Seafloor placeholder를 숨길지 여부</summary>
        public bool HideSeafloorPlaceholdersWhenTerrainSourceExists => hideSeafloorPlaceholdersWhenTerrainSourceExists;

        /// <summary>Terrain Source 루트 이름</summary>
        public string TerrainSourceRootName => string.IsNullOrEmpty(terrainSourceRootName) ? "TerrainSource" : terrainSourceRootName;

        /// <summary>생성된 Terrain 루트 이름</summary>
        public string GeneratedTerrainRootName => string.IsNullOrEmpty(generatedTerrainRootName) ? "Terrain" : generatedTerrainRootName;

        /// <summary>Terrain Fallback Material (pink material 방지)</summary>
        public Material TerrainFallbackMaterial => terrainFallbackMaterial;

        /// <summary>Terrain Source 상세 로그 출력 여부</summary>
        public bool LogTerrainSourceVerbose => logTerrainSourceVerbose;

        /// <summary>Terrain Source Layout 생성 여부 (Phase 13)</summary>
        public bool CreateTerrainSourceLayout => createTerrainSourceLayout;

        /// <summary>최종 Terrain 루트 이름</summary>
        public string TerrainLayoutRootName => string.IsNullOrEmpty(terrainLayoutRootName) ? "Terrain" : terrainLayoutRootName;

        /// <summary>Terrain Source Staging 루트 이름</summary>
        public string TerrainSourceStagingRootName => string.IsNullOrEmpty(terrainSourceStagingRootName) ? "TerrainSource" : terrainSourceStagingRootName;

        /// <summary>Decorative 후보 루트 이름</summary>
        public string DecorativeCandidateRootName => string.IsNullOrEmpty(decorativeCandidateRootName) ? "TerrainDecorativeCandidates" : decorativeCandidateRootName;

        /// <summary>Disabled 후보 루트 이름</summary>
        public string DisabledCandidateRootName => string.IsNullOrEmpty(disabledCandidateRootName) ? "TerrainDisabledCandidates" : disabledCandidateRootName;

        /// <summary>Scene Source clone을 Base Terrain으로 우선할지 여부</summary>
        public bool PreferSceneTerrainSource => preferSceneTerrainSource;

        /// <summary>Project Asset은 Scene Source가 없을 때만 fallback으로 사용</summary>
        public bool UseProjectAssetAsFallbackOnly => useProjectAssetAsFallbackOnly;

        /// <summary>최대 Base Terrain clone 개수</summary>
        public int MaxBaseTerrainCloneCount => maxBaseTerrainCloneCount;

        /// <summary>Base Terrain을 Map Bounds에 자동 맞춤</summary>
        public bool AutoFitBaseTerrainToMapBounds => autoFitBaseTerrainToMapBounds;

        /// <summary>Terrain Source의 World Transform 유지</summary>
        public bool PreserveTerrainSourceWorldTransform => preserveTerrainSourceWorldTransform;

        /// <summary>사용하지 않는 Terrain Source Renderer 비활성화</summary>
        public bool DisableUnusedTerrainSourceRenderers => disableUnusedTerrainSourceRenderers;

        /// <summary>Terrain Layout 상세 로그 출력 여부</summary>
        public bool LogTerrainLayoutVerbose => logTerrainLayoutVerbose;

        /// <summary>Zone Design Database 생성 여부 (Phase 14.1)</summary>
        public bool CreateZoneDesignDatabase => createZoneDesignDatabase;

        /// <summary>Zone Design Database 생성 후 검증 실행 여부</summary>
        public bool ValidateZoneDesignDatabaseAfterGenerate => validateZoneDesignDatabaseAfterGenerate;

        /// <summary>Zone Design Database 상세 로그 출력 여부</summary>
        public bool LogZoneDesignVerbose => logZoneDesignVerbose;

        /// <summary>Zone Design Database (Project Asset)</summary>
        public WorldMapZoneDesignDatabaseSO ZoneDesignDatabase => zoneDesignDatabase;

        /// <summary>Zone Design Rule 생성 여부 (Phase 14.2)</summary>
        public bool CreateZoneDesignRules => createZoneDesignRules;

        /// <summary>Zone Design Rule 생성 후 검증 실행 여부</summary>
        public bool ValidateZoneDesignRulesAfterGenerate => validateZoneDesignRulesAfterGenerate;

        /// <summary>Zone Design Rule 상세 로그 출력 여부</summary>
        public bool LogZoneDesignRuleVerbose => logZoneDesignRuleVerbose;

        /// <summary>Zone Design Rule Database (Project Asset)</summary>
        public WorldMapZoneDesignRuleDatabaseSO ZoneDesignRuleDatabase => zoneDesignRuleDatabase;

        /// <summary>Zone Terrain Plan 생성 여부 (Phase 14.3)</summary>
        public bool CreateZoneTerrainPlans => createZoneTerrainPlans;

        /// <summary>Zone Terrain Plan 생성 후 검증 실행 여부</summary>
        public bool ValidateZoneTerrainPlansAfterGenerate => validateZoneTerrainPlansAfterGenerate;

        /// <summary>Zone Terrain Plan 상세 로그 출력 여부</summary>
        public bool LogZoneTerrainPlanVerbose => logZoneTerrainPlanVerbose;

        /// <summary>Zone Terrain Plan Database (Project Asset)</summary>
        public WorldMapZoneTerrainPlanDatabaseSO ZoneTerrainPlanDatabase => zoneTerrainPlanDatabase;

        /// <summary>Zone Terrain Patch 생성 여부 (Phase 14.4)</summary>
        public bool CreateZoneTerrainPatches => createZoneTerrainPatches;

        /// <summary>Zone Terrain Patch 생성 후 검증 실행 여부</summary>
        public bool ValidateZoneTerrainPatchesAfterGenerate => validateZoneTerrainPatchesAfterGenerate;

        /// <summary>Zone Terrain Patch 상세 로그 출력 여부</summary>
        public bool LogZoneTerrainPatchVerbose => logZoneTerrainPatchVerbose;

        /// <summary>Terrain Patch 해상도 (기본 32 = 33x33 vertices)</summary>
        public int TerrainPatchResolution => terrainPatchResolution;

        /// <summary>Terrain Patch Material (없으면 fallback 검색)</summary>
        public Material TerrainPatchMaterial => terrainPatchMaterial;

        /// <summary>Terrain Patch MeshCollider 생성 여부</summary>
        public bool CreateTerrainPatchMeshCollider => createTerrainPatchMeshCollider;

        /// <summary>Terrain Patch 생성 시 기존 Seafloor placeholder 숨김 여부</summary>
        public bool HideLegacySeafloorPlaceholdersWhenPatchExists => hideLegacySeafloorPlaceholdersWhenPatchExists;

        /// <summary>인접 Zone 경계 seam 블렌드 폭 (unit)</summary>
        public float TerrainPatchSeamBlendWidth => terrainPatchSeamBlendWidth;

        /// <summary>Terrain Patch 노이즈 스케일</summary>
        public float TerrainPatchNoiseScale => terrainPatchNoiseScale;

        /// <summary>Terrain Patch 노이즈 강도</summary>
        public float TerrainPatchNoiseStrength => terrainPatchNoiseStrength;

        // ===== Phase 14.5 Public Getters =====

        /// <summary>Terrain Patch 내부 디테일 변형 생성 여부 (Phase 14.5)</summary>
        public bool CreateZoneTerrainPatchInteriorDetail => createZoneTerrainPatchInteriorDetail;

        /// <summary>Terrain Patch 내부 디테일 변형 강도 (0=없음, 1=최대)</summary>
        public float TerrainPatchInteriorDetailStrength => terrainPatchInteriorDetailStrength;

        /// <summary>Terrain Patch 경계 edge falloff 폭 (vertex 단위, seam 보호용)</summary>
        public int TerrainPatchEdgeFalloffWidth => terrainPatchEdgeFalloffWidth;

        /// <summary>Terrain Patch 내부 디테일 생성 후 검증 실행 여부</summary>
        public bool ValidateZoneTerrainPatchInteriorDetailAfterGenerate => validateZoneTerrainPatchInteriorDetailAfterGenerate;

        /// <summary>Terrain Patch 내부 디테일 노이즈 스케일 (PerlinNoise 주파수)</summary>
        public float TerrainPatchInteriorNoiseScale => terrainPatchInteriorNoiseScale;

        /// <summary>Terrain Patch 내부 디테일 노이즈 강도 (최대 높이 변화)</summary>
        public float TerrainPatchInteriorNoiseStrength => terrainPatchInteriorNoiseStrength;

        /// <summary>Terrain Patch 내부 디테일 canyon/cliff 추가 강도 승수</summary>
        public float TerrainPatchInteriorFeatureStrength => terrainPatchInteriorFeatureStrength;

        /// <summary>Terrain Patch 내부 디테일 상세 로그 출력 여부</summary>
        public bool LogZoneTerrainPatchInteriorDetailVerbose => logZoneTerrainPatchInteriorDetailVerbose;

        // ===== Phase 14.6 Public Getters =====

        /// <summary>Zone Content Placeholder 생성 여부 (Phase 14.6)</summary>
        public bool CreateZoneContentPlaceholders => createZoneContentPlaceholders;

        /// <summary>Zone Content Placeholder 생성 후 검증 실행 여부</summary>
        public bool ValidateZoneContentPlaceholdersAfterGenerate => validateZoneContentPlaceholdersAfterGenerate;

        /// <summary>Zone Content Placeholder 상세 로그 출력 여부</summary>
        public bool LogZoneContentPlaceholderVerbose => logZoneContentPlaceholderVerbose;

        /// <summary>Visible Content Debug Marker 생성 여부</summary>
        public bool CreateVisibleContentDebugMarkers => createVisibleContentDebugMarkers;

        /// <summary>Zone 당 최대 Resource Marker 개수</summary>
        public int MaxResourceMarkersPerZone => maxResourceMarkersPerZone;

        /// <summary>Zone 당 최대 Hazard Marker 개수</summary>
        public int MaxHazardMarkersPerZone => maxHazardMarkersPerZone;

        /// <summary>Zone 당 최대 Landmark Marker 개수</summary>
        public int MaxLandmarkMarkersPerZone => maxLandmarkMarkersPerZone;

        /// <summary>상세 로그 출력 여부</summary>

        public bool LogVerbose => logVerbose;




        // ===== Public Setters (Editor Auto-Fill 용, Runtime에서는 사용 금지) =====


        /// <summary>월드맵 설정 설정 (Editor Auto-Fill 전용)</summary>
        public void SetWorldMapConfig(WorldMapConfigSO value) { worldMapConfig = value; }

        /// <summary>시나리오 프리셋 설정 (Editor Auto-Fill 전용)</summary>
        public void SetScenarioPreset(WorldMapScenarioPresetSO value) { scenarioPreset = value; }

        /// <summary>GlobalWater Prefab Asset 설정 (Editor Auto-Fill 전용)</summary>
        public void SetGlobalWaterPrefab(GameObject value) { globalWaterPrefab = value; }

        /// <summary>Underwater Area Prefab Asset 설정 (Editor Auto-Fill 전용)</summary>
        public void SetUnderwaterAreaPrefab(GameObject value) { underwaterAreaPrefab = value; }

        /// <summary>Bubble Particle Prefab Asset 설정 (Editor Auto-Fill 전용)</summary>
        public void SetBubbleParticlePrefab(GameObject value) { bubbleParticlePrefab = value; }

        /// <summary>Dynamic Effect Prefab Asset 설정 (Editor Auto-Fill 전용)</summary>
        public void SetDynamicEffectPrefab(GameObject value) { dynamicEffectPrefab = value; }

        /// <summary>Visual Profile Set 설정 (Editor Auto-Fill 전용)</summary>
        public void SetVisualProfileSet(WorldMapVisualProfileSetSO value) { visualProfileSet = value; }
    }
}
