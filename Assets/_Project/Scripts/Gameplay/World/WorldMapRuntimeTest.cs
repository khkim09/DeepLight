using System.Collections.Generic;
using System.Text;
using Project.Core.Events;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>WorldMapService 런타임 테스트 컴포넌트</summary>
    public class WorldMapRuntimeTest : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WorldMapConfigSO worldMapConfig;
        [SerializeField] private List<ZoneDataSO> zoneDataAssets;

        [Header("Tracking")]
        [SerializeField] private Transform trackedTransform;
        [SerializeField] private bool updateEveryFrame;

        [Header("Mock Progress")]
        [SerializeField] private bool useMockProgress = true;
        [SerializeField] private string testUpgradeId = "upgrade_battery_mk2";
        [SerializeField] private string testLogId = "log_intro_01";
        [SerializeField] private string testNarrativeFlag = "mara_research_analysis_done";
        [SerializeField] private int mockHullTier = 2;
        [SerializeField] private int mockDepthLevel = 3;
        [SerializeField] private float mockSensorAccuracy = 0.8f;
        [SerializeField] private int mockLogCount = 5;

        [Header("Debug Event Logging")]
        [SerializeField] private bool logEventsToConsole = true;

        [Header("ZoneRoot Validation (Pre-assign in Inspector)")]
        [SerializeField] private ZoneRoot[] zoneRoots; // 인스펙터에서 미리 할당 (FindObjectsOfType 사용 금지)

        // 내부 서비스
        private IZoneResolver _zoneResolver;
        private IZoneRepository _zoneRepository;
        private IUnlockConditionEvaluator _conditionEvaluator;
        private IZoneStateEvaluator _zoneStateEvaluator;
        private MockWorldProgressQuery _mockProgressQuery;
        private IWorldMapService _worldMapService;

        private bool _isInitialized;

        // 외부 주입 모드 (Phase 7: WorldMapRuntimeInstaller에서 주입)
        // [Removed unused field _hasExternalServiceGraph - CS0414]

        private void OnEnable()
        {
            // 디버그 이벤트 로깅 구독
            if (logEventsToConsole)
            {
                EventBus.Subscribe<ZoneChangedEvent>(OnZoneChangedEvent);
                EventBus.Subscribe<CurrentZoneStateChangedEvent>(OnCurrentZoneStateChangedEvent);
                EventBus.Subscribe<CurrentZoneClearedEvent>(OnCurrentZoneClearedEvent);
            }
        }

        private void OnDisable()
        {
            // 디버그 이벤트 로깅 구독 해제
            if (logEventsToConsole)
            {
                EventBus.Unsubscribe<ZoneChangedEvent>(OnZoneChangedEvent);
                EventBus.Unsubscribe<CurrentZoneStateChangedEvent>(OnCurrentZoneStateChangedEvent);
                EventBus.Unsubscribe<CurrentZoneClearedEvent>(OnCurrentZoneClearedEvent);
            }
        }

        /// <summary>ZoneChangedEvent 디버그 로깅</summary>
        private void OnZoneChangedEvent(ZoneChangedEvent evt)
        {
            UnityEngine.Debug.Log($"[EventBus] ZoneChanged: '{evt.PreviousZoneId}' -> '{evt.CurrentZoneId}' | Region: '{evt.PreviousRegionId}' -> '{evt.CurrentRegionId}' | " +
                $"Accessibility={evt.Accessibility}, Risk={evt.RiskLevel:F2}, Discovered={evt.IsDiscovered}, Unlocked={evt.IsUnlocked}");
        }

        /// <summary>CurrentZoneStateChangedEvent 디버그 로깅</summary>
        private void OnCurrentZoneStateChangedEvent(CurrentZoneStateChangedEvent evt)
        {
            UnityEngine.Debug.Log($"[EventBus] CurrentZoneStateChanged: Zone='{evt.ZoneId}' | " +
                $"Accessibility: {evt.PreviousAccessibility}->{evt.CurrentAccessibility}, " +
                $"Risk: {evt.PreviousRiskLevel:F2}->{evt.CurrentRiskLevel:F2}, " +
                $"Discovered: {evt.PreviousIsDiscovered}->{evt.CurrentIsDiscovered}");
        }

        /// <summary>CurrentZoneClearedEvent 디버그 로깅</summary>
        private void OnCurrentZoneClearedEvent(CurrentZoneClearedEvent evt)
        {
            UnityEngine.Debug.Log($"[EventBus] CurrentZoneCleared: LastZone='{evt.LastZoneId}', LastRegion='{evt.LastRegionId}', Reason='{evt.Reason}'");
        }

        private void Start()
        {
            // trackedTransform이 인스펙터에서 할당되지 않은 경우 Player 태그로 fallback
            if (trackedTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    trackedTransform = player.transform;
                    UnityEngine.Debug.Log($"[WorldMapRuntimeTest] Auto-assigned trackedTransform from Player tag: {player.name}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[WorldMapRuntimeTest] No trackedTransform assigned and no Player tag found in scene. " +
                        "Assign trackedTransform in Inspector or add 'Player' tag to Submarine.");
                }
            }

            InitializeServices();

            if (_isInitialized)
            {
                Vector3 initialPosition = trackedTransform != null ?
                    trackedTransform.position : Vector3.zero;

                _worldMapService.Initialize(initialPosition);

                if (_worldMapService.HasCurrentZone)
                {
                    UnityEngine.Debug.Log($"[WorldMapRuntimeTest] Auto-initialized at zone: {_worldMapService.CurrentZoneId}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[WorldMapRuntimeTest] Auto-initialization: position out of bounds or zone resolution failed.");
                }
            }
        }

        private void Update()
        {
            if (!updateEveryFrame || !_isInitialized || trackedTransform == null)
                return;

            // 매 프레임 현재 존 갱신
            bool zoneChanged = _worldMapService.RefreshCurrentZone(trackedTransform.position);
            if (zoneChanged)
            {
                UnityEngine.Debug.Log($"[WorldMapRuntimeTest] Zone changed to: {_worldMapService.CurrentZoneId}");

                // 존 변경 시 ZoneRoot 교차 검증 (인스펙터에서 할당된 배열만 순회)
                ValidateCurrentZoneAgainstZoneRoots();
            }
        }

        /// <summary>내부 서비스 초기화</summary>
        private void InitializeServices()
        {
            if (worldMapConfig == null)
            {
                UnityEngine.Debug.LogError("[WorldMapRuntimeTest] WorldMapConfigSO is not assigned.");
                return;
            }

            // 1. ZoneResolver 생성
            _zoneResolver = new ZoneResolver(worldMapConfig);

            // 2. ZoneRepository 생성
            _zoneRepository = new ZoneRepository(worldMapConfig, zoneDataAssets);

            // 3. ConditionEvaluator 생성
            _conditionEvaluator = new UnlockConditionEvaluator();

            // 4. ZoneStateEvaluator 생성
            _zoneStateEvaluator = new ZoneStateEvaluator(_conditionEvaluator);

            // 5. Mock 진행도 설정
            if (useMockProgress)
            {
                _mockProgressQuery = new MockWorldProgressQuery();
                _mockProgressQuery.AddUpgrade(testUpgradeId);
                _mockProgressQuery.AddLog(testLogId);
                _mockProgressQuery.AddNarrativeFlag(testNarrativeFlag);
                _mockProgressQuery.SetHullTier(mockHullTier);
                _mockProgressQuery.SetDepthLevel(mockDepthLevel);
                _mockProgressQuery.SetSensorAccuracy(mockSensorAccuracy);

                // 로그 수 설정
                for (int i = 0; i < mockLogCount; i++)
                {
                    _mockProgressQuery.AddLog($"log_test_{i}");
                }
            }

            // 6. WorldMapService 생성
            _worldMapService = new WorldMapService(
                _zoneResolver,
                _zoneRepository,
                _zoneStateEvaluator,
                useMockProgress ? _mockProgressQuery : null);

            _isInitialized = true;
            UnityEngine.Debug.Log("[WorldMapRuntimeTest] Services initialized.");
        }

        /// <summary>초기화 및 현재 위치 기반 현재 존 설정</summary>
        [ContextMenu("Initialize Runtime Test")]
        private void InitializeRuntimeTest()
        {
            InitializeServices();

            if (!_isInitialized) return;

            Vector3 initialPosition = trackedTransform != null ?
                trackedTransform.position : Vector3.zero;

            _worldMapService.Initialize(initialPosition);

            if (_worldMapService.HasCurrentZone)
            {
                UnityEngine.Debug.Log($"[WorldMapRuntimeTest] Initialized at zone: {_worldMapService.CurrentZoneId}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeTest] Initialization completed but no current zone set (position may be out of bounds).");
            }
        }

        /// <summary>현재 위치 기준 존 갱신</summary>
        [ContextMenu("Refresh Current Zone")]
        private void RefreshCurrentZone()
        {
            if (!_isInitialized)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeTest] Services not initialized. Run 'Initialize Runtime Test' first.");
                return;
            }

            Vector3 position = trackedTransform != null ?
                trackedTransform.position : Vector3.zero;

            bool zoneChanged = _worldMapService.RefreshCurrentZone(position);

            if (zoneChanged)
            {
                UnityEngine.Debug.Log($"[WorldMapRuntimeTest] Zone changed to: {_worldMapService.CurrentZoneId}");
            }
            else
            {
                UnityEngine.Debug.Log($"[WorldMapRuntimeTest] Zone unchanged: {_worldMapService.CurrentZoneId}");
            }
        }

        /// <summary>현재 존 상태 출력</summary>
        [ContextMenu("Print Current Zone State")]
        private void PrintCurrentZoneState()
        {
            if (!_isInitialized)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeTest] Services not initialized.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== WorldMapRuntimeTest: Current Zone State ===");

            if (!_worldMapService.HasCurrentZone)
            {
                sb.AppendLine("No current zone set. Call 'Initialize Runtime Test' first.");
                UnityEngine.Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine($"Current Zone ID: {_worldMapService.CurrentZoneId}");
            sb.AppendLine($"Current Region ID: {_worldMapService.CurrentRegionId}");

            if (_worldMapService.TryGetCurrentZoneState(out ZoneRuntimeState currentState))
            {
                sb.AppendLine($"--- Current Zone State ---");
                sb.AppendLine(currentState.GetDebugStateString());
                sb.AppendLine($"Display Name: {currentState.ZoneData?.DisplayName ?? "N/A"}");
                sb.AppendLine($"Base Risk: {currentState.ZoneData?.BaseRiskLevel:P0}");
                sb.AppendLine($"Is Starting Zone: {currentState.ZoneData?.IsStartingZone}");
                sb.AppendLine($"Is Always Unlocked: {currentState.ZoneData?.IsAlwaysUnlocked()}");
                sb.AppendLine($"Is Story Critical: {currentState.ZoneData?.IsStoryCritical}");

                if (currentState.FailedConditions.Count > 0)
                {
                    sb.AppendLine($"Failed Conditions: {currentState.GetFailedConditionsString()}");
                }
            }
            else
            {
                sb.AppendLine("Failed to get current zone state.");
            }

            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>특정 ZoneId의 상태 출력</summary>
        [ContextMenu("Print Test Zone State By Id")]
        private void PrintTestZoneStateById()
        {
            if (!_isInitialized)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeTest] Services not initialized.");
                return;
            }

            // 테스트용 ZoneId 목록
            ZoneId[] testZoneIds = new ZoneId[]
            {
                ZoneId.Parse("E5"),  // 허브
                ZoneId.Parse("I6"),  // 연구 구역
                ZoneId.Parse("F10"), // 봉인 북부
                ZoneId.Parse("A1"),  // 경계 존
                ZoneId.Parse("J10"), // 경계 존
            };

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== WorldMapRuntimeTest: Zone State By Id ===");

            foreach (ZoneId zoneId in testZoneIds)
            {
                ZoneRuntimeState state = _worldMapService.GetZoneStateOrEvaluate(zoneId);
                sb.AppendLine($"  {state.GetDebugStateString()} | {state.ZoneData?.DisplayName ?? "No Data"}");
            }

            UnityEngine.Debug.Log(sb.ToString());
        }

        // ===== ZoneRoot Validation (인스펙터 할당 배열 기반, FindObjectsOfType 사용 금지) =====

        /// <summary>좌표 기반 현재 존과 인스펙터 할당된 ZoneRoot 데이터를 교차 검증</summary>
        private void ValidateCurrentZoneAgainstZoneRoots()
        {
            if (!_isInitialized || !_worldMapService.HasCurrentZone || zoneRoots == null || zoneRoots.Length == 0)
                return;

            ZoneId currentZoneId = _worldMapService.CurrentZoneId;

            // 현재 존과 일치하는 ZoneRoot 찾기 (인스펙터 할당 배열만 순회)
            ZoneRoot matchingRoot = null;
            foreach (ZoneRoot root in zoneRoots)
            {
                if (root != null && root.GetZoneId().Equals(currentZoneId))
                {
                    matchingRoot = root;
                    break;
                }
            }

            if (matchingRoot != null)
            {
                // ZoneRoot가 있으면 ZoneData 일치 확인
                ZoneDataSO rootData = matchingRoot.GetZoneData();
                if (rootData != null && _worldMapService.TryGetCurrentZoneState(out ZoneRuntimeState currentState))
                {
                    ZoneDataSO serviceData = currentState.ZoneData;
                    if (serviceData != null && rootData != serviceData)
                    {
                        UnityEngine.Debug.LogWarning($"[WorldMapRuntimeTest] ZoneRoot '{matchingRoot.name}' has ZoneData '{rootData.name}' " +
                            $"but WorldMapService resolved '{serviceData.name}' for zone '{currentZoneId}'.", matchingRoot);
                    }
                }
            }
        }

        // ===== Phase 7: External Injection Support (WorldMapRuntimeInstaller) =====

        /// <summary>
        /// 외부에서 서비스 그래프를 주입받습니다 (WorldMapRuntimeInstaller에서 호출).
        /// 주입받으면 자체 InitializeServices()를 건너뛰고 주입된 서비스를 사용합니다.
        /// </summary>
        /// <param name="service">외부에서 생성된 IWorldMapService 인스턴스</param>
        /// <param name="tracked">추적할 Transform</param>
        public void SetRuntimeServices(IWorldMapService service, Transform tracked)
        {
            if (service == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeTest] SetRuntimeServices called with null service. Ignoring.");
                return;
            }

            // 외부 주입 모드 활성화
            _worldMapService = service;

            trackedTransform = tracked;
            _isInitialized = true;

            UnityEngine.Debug.Log("[WorldMapRuntimeTest] Switched to external injection mode. Self-host service graph disabled.");
        }

        // ===== Public Accessors for Debug HUD =====

        /// <summary>내부 WorldMapService 반환 (디버그 HUD 연동용)</summary>
        public IWorldMapService GetWorldMapService()
        {
            return _isInitialized ? _worldMapService : null;
        }

        /// <summary>추적 중인 Transform 반환 (디버그 HUD 연동용)</summary>
        public Transform GetTrackedTransform()
        {
            return trackedTransform;
        }

        /// <summary>에디터 Gizmo 시각화</summary>
        private void OnDrawGizmosSelected()
        {
            if (worldMapConfig == null)
                return;

            // 레이지 초기화 (에디터 Gizmo용)
            if (_zoneResolver == null)
            {
                _zoneResolver = new ZoneResolver(worldMapConfig);
            }

            if (trackedTransform == null)
                return;

            Vector3 testPos = trackedTransform.position;

            // safe API 사용: TryGetZoneIdFromWorldPosition으로 일관성 유지
            if (_zoneResolver.TryGetZoneIdFromWorldPosition(testPos, out ZoneId zoneId))
            {
                (Vector3 min, Vector3 max) bounds = _zoneResolver.GetZoneBounds(zoneId);

                // 존 경계 박스
                Gizmos.color = Color.green;
                Vector3 center = (bounds.min + bounds.max) / 2f;
                Vector3 size = new Vector3(
                    bounds.max.x - bounds.min.x,
                    10f,
                    bounds.max.z - bounds.min.z);
                Gizmos.DrawWireCube(center, size);

                // 존 중심
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_zoneResolver.GetZoneCenterWorldPosition(zoneId), 5f);

                // 추적 위치
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(testPos, 2f);

                // 전체 월드 경계
                Gizmos.color = Color.blue;
                Vector3 worldCenter = new Vector3(
                    (worldMapConfig.WorldMinX + worldMapConfig.WorldMaxX) / 2f,
                    0f,
                    (worldMapConfig.WorldMinZ + worldMapConfig.WorldMaxZ) / 2f);
                Vector3 worldSize = new Vector3(
                    worldMapConfig.WorldWidth,
                    10f,
                    worldMapConfig.WorldHeight);
                Gizmos.DrawWireCube(worldCenter, worldSize);
            }
        }
    }
}
