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
    }
}
