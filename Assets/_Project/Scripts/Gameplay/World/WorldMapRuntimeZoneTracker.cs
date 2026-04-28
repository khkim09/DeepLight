using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// Runtime에서 target position을 주기적으로 샘플링하여 current Zone / DepthBand를 추적하는 선택적 component.
    /// 기존 WorldMapService를 대체하지 않으며, Debug/검증용에 가깝다.
    /// Trigger 이벤트를 사용하지 않고 ZoneResolver 좌표 계산을 우선한다.
    /// </summary>
    public class WorldMapRuntimeZoneTracker : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WorldMapConfigSO worldMapConfig;
        [SerializeField] private WorldMapScenarioPresetSO scenarioPreset;
        [SerializeField] private GeneratedWorldZoneRegistry registry;
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindTarget = true;
        [SerializeField] private string autoFindTargetName = "Submarine";
        [SerializeField] private float sampleInterval = 0.1f;
        [SerializeField] private bool logZoneChanges = true;
        [SerializeField] private bool logDepthBandChanges = true;

        // Runtime state
        private ZoneResolver resolver;
        private ZoneId currentZoneId;
        private ZoneDepthBand currentDepthBand;
        private ZoneDepthSample currentSample;
        private float nextSampleTime;
        private bool hasWarnedMissingTarget;

        // ===== Public Getters =====

        /// <summary>현재 Zone ID</summary>
        public ZoneId CurrentZoneId => currentZoneId;

        /// <summary>현재 Depth Band</summary>
        public ZoneDepthBand CurrentDepthBand => currentDepthBand;

        /// <summary>현재 샘플 (최신)</summary>
        public ZoneDepthSample CurrentSample => currentSample;

        /// <summary>초기화 완료 여부</summary>
        public bool IsInitialized => resolver != null && registry != null;

        // ===== Public Methods =====

        /// <summary>
        /// Tracker를 초기화한다. Start()에서 자동 호출되지만, 외부에서 명시적으로 호출할 수도 있다.
        /// </summary>
        public void Initialize(WorldMapConfigSO config, WorldMapScenarioPresetSO preset, GeneratedWorldZoneRegistry registry, Transform target)
        {
            this.worldMapConfig = config;
            this.scenarioPreset = preset;
            this.registry = registry;
            this.target = target;

            InitializeInternal();
        }

        /// <summary>
        /// 즉시 샘플링을 강제로 수행한다.
        /// </summary>
        public void ForceSample()
        {
            if (!IsInitialized)
            {
                // 초기화되지 않았으면 자동 초기화 시도
                InitializeInternal();
                if (!IsInitialized) return;
            }

            PerformSample();
        }

        /// <summary>
        /// 현재 샘플을 조회한다.
        /// </summary>
        public bool TryGetCurrentSample(out ZoneDepthSample sample)
        {
            sample = currentSample;
            return IsInitialized;
        }

        /// <summary>
        /// 현재 Zone에 해당하는 GeneratedWorldZoneNode를 조회한다.
        /// </summary>
        public bool TryGetCurrentNode(out GeneratedWorldZoneNode node)
        {
            node = null;
            if (!IsInitialized) return false;
            return registry.TryGetNode(currentZoneId, out node);
        }

        // ===== Unity Lifecycle =====

        private void Start()
        {
            // Serialized field 기반 초기화
            InitializeInternal();
        }

        private void Update()
        {
            // sampleInterval 기반 주기적 샘플링
            if (Time.time < nextSampleTime) return;
            nextSampleTime = Time.time + sampleInterval;

            if (!IsInitialized)
            {
                // 초기화 재시도 (Start 이후에 할당될 수 있음)
                InitializeInternal();
                return;
            }

            PerformSample();
        }

        // ===== Internal =====

        /// <summary>
        /// Serialized field 기반으로 내부 상태를 초기화한다.
        /// </summary>
        private void InitializeInternal()
        {
            // Config가 없으면 초기화 불가
            if (worldMapConfig == null) return;

            // Resolver 생성
            if (resolver == null)
            {
                resolver = new ZoneResolver(worldMapConfig);
            }

            // ScenarioPreset이 있으면 Initialize
            if (scenarioPreset != null)
            {
                resolver.Initialize(scenarioPreset);
            }

            // Target 자동 찾기
            if (target == null && autoFindTarget)
            {
                TryAutoFindTarget();
            }
        }

        /// <summary>
        /// autoFindTargetName으로 Scene에서 target을 찾는다.
        /// </summary>
        private void TryAutoFindTarget()
        {
            if (string.IsNullOrEmpty(autoFindTargetName)) return;

            GameObject found = GameObject.Find(autoFindTargetName);
            if (found != null)
            {
                target = found.transform;
                UnityEngine.Debug.Log($"[WorldMapRuntimeZoneTracker] Auto-found target: {autoFindTargetName}");
            }
            else
            {
                // 1회만 warning 출력
                if (!hasWarnedMissingTarget)
                {
                    UnityEngine.Debug.LogWarning($"[WorldMapRuntimeZoneTracker] Target '{autoFindTargetName}' not found in scene. Tracker will be idle.");
                    hasWarnedMissingTarget = true;
                }
            }
        }

        /// <summary>
        /// 현재 target 위치를 샘플링하고 Zone/DepthBand 변경을 감지한다.
        /// </summary>
        private void PerformSample()
        {
            // Target이 없으면 스킵
            if (target == null)
            {
                // autoFindTarget이 true이면 재시도
                if (autoFindTarget)
                {
                    TryAutoFindTarget();
                }
                return;
            }

            Vector3 position = target.position;

            // ZoneResolver로 샘플링
            if (!resolver.TrySampleDepth(position, currentZoneId, out ZoneDepthSample sample))
            {
                // 월드 경계 밖이면 스킵
                return;
            }

            // 이전 샘플과 비교
            bool zoneChanged = sample.ZoneId != currentZoneId;
            bool depthBandChanged = sample.DepthBand != currentDepthBand;

            // 상태 갱신
            currentSample = sample;
            currentZoneId = sample.ZoneId;
            currentDepthBand = sample.DepthBand;

            // 로그 출력
            if (zoneChanged && logZoneChanges)
            {
                UnityEngine.Debug.Log($"[WorldMapRuntimeZoneTracker] Zone changed: {currentZoneId} (Region: {sample.RegionId}, Biome: {sample.BiomeType})");
            }

            if (depthBandChanged && logDepthBandChanges)
            {
                UnityEngine.Debug.Log($"[WorldMapRuntimeZoneTracker] DepthBand changed: {currentDepthBand} (Y: {sample.Y:F1}, Normalized: {sample.NormalizedDepth01:F3})");
            }
        }
    }
}
