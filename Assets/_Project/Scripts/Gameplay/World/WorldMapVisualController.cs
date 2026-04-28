using Project.Data.World;
using Project.Gameplay.World.VisualAdapters;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// WorldMapRuntimeZoneTracker에서 ZoneDepthSample을 받아 VisualProfile을 계산하고 부드럽게 보간한다.
    /// applyToAdapters가 true이면 WorldMapVisualAdapterRoot를 통해 실제 렌더링 시스템에 적용한다.
    /// </summary>
    public class WorldMapVisualController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WorldMapRuntimeZoneTracker zoneTracker;
        [SerializeField] private WorldMapVisualProfileSetSO profileSet;
        [SerializeField] private float blendSpeed = 2f;
        [SerializeField] private bool logVisualChanges = true;
        [SerializeField] private bool applyToAdapters = false;

        [Header("Adapter (Phase 9)")]
        [SerializeField] private WorldMapVisualAdapterRoot adapterRoot;

        [Header("Runtime State")]
        [SerializeField] private WorldMapVisualRuntimeState state = new WorldMapVisualRuntimeState();

        // Internal
        private WorldMapVisualProfileResolver resolver = new WorldMapVisualProfileResolver();
        private WorldMapVisualProfile currentProfile;
        private WorldMapVisualProfile targetProfile;
        private bool hasWarnedMissingTracker;
        private bool hasWarnedMissingProfileSet;
        private bool hasWarnedMissingAdapterRoot;
        private ZoneDepthBand lastDepthBand;
        private ZoneBiomeType lastBiomeType;
        private bool hasLoggedFirstSample;

        // ===== Public Getters =====

        /// <summary>Zone Tracker 참조</summary>
        public WorldMapRuntimeZoneTracker ZoneTracker => zoneTracker;

        /// <summary>Visual Profile Set 참조</summary>
        public WorldMapVisualProfileSetSO ProfileSet => profileSet;

        /// <summary>보간 속도</summary>
        public float BlendSpeed => blendSpeed;

        /// <summary>Visual 변경 로그 출력 여부</summary>
        public bool LogVisualChanges => logVisualChanges;

        /// <summary>Adapter 적용 여부 (이번 Phase에서는 false 기본값)</summary>
        public bool ApplyToAdapters => applyToAdapters;

        /// <summary>초기화 완료 여부</summary>
        public bool IsInitialized => zoneTracker != null && profileSet != null && resolver.IsInitialized;

        /// <summary>Adapter Root 참조 (Phase 9)</summary>
        public WorldMapVisualAdapterRoot AdapterRoot => adapterRoot;

        // ===== Public Methods =====

        /// <summary>
        /// VisualController를 초기화한다.
        /// </summary>
        public void Initialize(WorldMapRuntimeZoneTracker tracker, WorldMapVisualProfileSetSO profileSet)
        {
            this.zoneTracker = tracker;
            this.profileSet = profileSet;
            this.resolver.Initialize(profileSet);
        }

        /// <summary>
        /// VisualController의 모든 설정을 한 번에 구성한다.
        /// Editor Builder에서 호출 가능하며, null tracker/profileSet 상태에서도 안전하게 동작한다.
        /// </summary>
        public void Configure(WorldMapRuntimeZoneTracker tracker, WorldMapVisualProfileSetSO profileSet, float blendSpeed, bool logChanges, bool applyToAdapters, WorldMapVisualAdapterRoot adapterRoot = null)
        {
            this.zoneTracker = tracker;
            this.profileSet = profileSet;
            this.blendSpeed = blendSpeed;
            this.logVisualChanges = logChanges;
            this.applyToAdapters = applyToAdapters;
            this.adapterRoot = adapterRoot;

            if (profileSet != null)
            {
                this.resolver.Initialize(profileSet);
            }

            // 초기화 경고 플래그 리셋 (재구성 시 재시도 가능하도록)
            hasWarnedMissingTracker = false;
            hasWarnedMissingProfileSet = false;
            hasWarnedMissingAdapterRoot = false;
        }

        /// <summary>
        /// 강제로 현재 sample을 기반으로 VisualProfile을 갱신한다.
        /// </summary>
        public void ForceRefresh()
        {
            if (!IsInitialized)
            {
                TryAutoInitialize();
                if (!IsInitialized) return;
            }

            UpdateVisualState();
        }

        /// <summary>
        /// 현재 적용 중인 VisualProfile을 조회한다.
        /// </summary>
        public bool TryGetCurrentProfile(out WorldMapVisualProfile profile)
        {
            profile = currentProfile;
            return state.IsValid;
        }

        /// <summary>
        /// 현재 Runtime 상태를 조회한다.
        /// </summary>
        public bool TryGetRuntimeState(out WorldMapVisualRuntimeState runtimeState)
        {
            runtimeState = state;
            return state.IsValid;
        }

        // ===== Unity Lifecycle =====

        private void Start()
        {
            // Serialized field 기반 초기화
            TryAutoInitialize();
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                // 초기화 재시도
                TryAutoInitialize();
                return;
            }

            UpdateVisualState();
        }

        // ===== Internal =====

        /// <summary>
        /// Serialized field 기반으로 자동 초기화를 시도한다.
        /// </summary>
        private void TryAutoInitialize()
        {
            if (zoneTracker == null)
            {
                if (!hasWarnedMissingTracker)
                {
                    UnityEngine.Debug.LogWarning("[WorldMapVisualController] zoneTracker is null. Visual controller will be idle.");
                    hasWarnedMissingTracker = true;
                }
                return;
            }

            if (profileSet == null)
            {
                if (!hasWarnedMissingProfileSet)
                {
                    UnityEngine.Debug.LogWarning("[WorldMapVisualController] profileSet is null. Visual controller will be idle.");
                    hasWarnedMissingProfileSet = true;
                }
                return;
            }

            resolver.Initialize(profileSet);
        }

        /// <summary>
        /// AdapterRoot를 통해 모든 Visual Adapter에 currentProfile을 적용한다.
        /// </summary>
        private void ApplyToVisualAdapters()
        {
            if (adapterRoot == null)
            {
                if (!hasWarnedMissingAdapterRoot)
                {
                    UnityEngine.Debug.LogWarning("[WorldMapVisualController] adapterRoot is null. Cannot apply to adapters.");
                    hasWarnedMissingAdapterRoot = true;
                }
                return;
            }

            // AdapterRoot가 수집되지 않았으면 수집
            if (!adapterRoot.HasCollected)
            {
                adapterRoot.CollectAdapters();
                adapterRoot.InitializeAdapters();
            }

            // 모든 Adapter에 currentProfile 적용
            adapterRoot.ApplyProfile(state, Time.deltaTime);
        }

        /// <summary>
        /// 현재 sample을 기반으로 VisualProfile을 계산하고 보간한다.
        /// </summary>
        private void UpdateVisualState()
        {
            // ZoneTracker에서 현재 sample 조회
            if (!zoneTracker.TryGetCurrentSample(out ZoneDepthSample sample))
            {
                return;
            }

            // Resolver로 target profile 계산
            targetProfile = resolver.ResolveOrFallback(sample);

            // currentProfile이 초기화되지 않았으면 target으로 설정
            if (!state.IsValid)
            {
                currentProfile = targetProfile;
            }
            else
            {
                // 부드러운 보간
                currentProfile = WorldMapVisualProfile.Lerp(currentProfile, targetProfile, Time.deltaTime * blendSpeed);
                currentProfile.ClampValues();
            }

            // 상태 갱신
            state.Update(sample, targetProfile, currentProfile);

            // 변경 감지 및 로그
            bool depthBandChanged = sample.DepthBand != lastDepthBand;
            bool biomeChanged = sample.BiomeType != lastBiomeType;

            if (logVisualChanges && (depthBandChanged || biomeChanged || !hasLoggedFirstSample))
            {
                UnityEngine.Debug.Log($"[WorldMapVisualController] Visual state: {sample.ToDebugString()} => {currentProfile.ToDebugString()}");
                hasLoggedFirstSample = true;
            }

            lastDepthBand = sample.DepthBand;
            lastBiomeType = sample.BiomeType;

            // applyToAdapters가 true일 때만 실제 적용 로직 실행
            if (applyToAdapters)
            {
                ApplyToVisualAdapters();
            }
        }
    }
}
