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
