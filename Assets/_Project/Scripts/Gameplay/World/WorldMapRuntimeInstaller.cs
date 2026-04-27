using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 월드맵 시스템의 런타임 컴포지션 루트 (Phase 8).
    /// 하나의 서비스 그래프를 생성하고 모든 소비자(AmbientReactionController, DebugHUD, RuntimeTest)에 주입한다.
    ///
    /// 책임:
    /// 1. WorldMapService + 의존성 그래프를 정확히 한 번 생성
    /// 2. 동일한 IWorldMapService 인스턴스를 모든 관심 컴포넌트에 주입
    /// 3. IWorldMapTrackedTargetProvider 기반 Update 루프 실행 (매 프레임 RefreshCurrentZone)
    /// 4. AmbientReactionController.Initialize() 호출 (Applier 생성 + 주입)
    /// 5. DebugHUD에 서비스 직접 주입 (RuntimeTest 경유 불필요)
    /// 6. RuntimeTest가 존재하면 SetRuntimeServices()로 외부 주입 모드 전환
    ///
    /// Phase 8 변경사항:
    /// - InstallerMode 열거형 추가 (Debug / Gameplay)
    /// - IWorldMapTrackedTargetProvider 추상화 도입
    /// - late binding 지원 (target이 없어도 Awake/Start 실패하지 않음)
    /// - target 획득 시에만 Initialize() 실행
    /// - Debug 모드: DebugTrackedTargetProvider (태그/이름 검색)
    /// - Gameplay 모드: GameplayTrackedTargetProvider (명시적 주입)
    ///
    /// 설계 의도:
    /// - Installer가 Awake()에서 서비스 그래프를 생성하고 Start()에서 주입을 완료한다.
    /// - Installer가 비활성화되면 Update 루프도 중단되지만 서비스 그래프는 유지된다.
    /// - DebugWorldAmbientApplier는 Installer가 직접 생성하여 AmbientReactionController에 주입한다.
    /// - RuntimeTest는 Installer가 주입한 서비스를 사용하거나, fallback으로 자체 생성 모드를 유지한다.
    /// - 기존 DebugHUD의 RuntimeTestSource fallback 경로는 그대로 유지된다 (하위 호환성).
    /// </summary>
    public class WorldMapRuntimeInstaller : MonoBehaviour
    {
        /// <summary>설치자 모드 (Phase 8)</summary>
        public enum InstallerMode
        {
            /// <summary>디버그 모드: DebugTrackedTargetProvider 사용, 태그/이름 검색, mock 진행도</summary>
            Debug,

            /// <summary>게임플레이 모드: GameplayTrackedTargetProvider 사용, 명시적 주입 필요</summary>
            Gameplay
        }

        [Header("Mode")]
        [SerializeField] private InstallerMode mode = InstallerMode.Debug;

        [Header("Configuration")]
        [SerializeField] private WorldMapConfigSO worldMapConfig;
        [SerializeField] private List<ZoneDataSO> zoneDataAssets;

        [Header("Tracking")]
        [SerializeField] private Transform trackedTransform; // 명시적 Transform (Debug 모드 우선, Gameplay 모드 fallback)

        [Header("Optional References")]
        [SerializeField] private WorldMapRuntimeTest runtimeTest; // (옵션) 외부 주입 모드로 전환
        [SerializeField] private WorldMapAmbientReactionController ambientController; // (옵션) Initialize() 호출 대상
        [SerializeField] private MonoBehaviour debugHUDComponent; // (옵션) WorldMapDebugHUDRuntime
        [SerializeField] private Camera targetCamera; // (옵션) null이면 Camera.main 사용

        [Header("Phase 9: User-Facing HUD (Optional)")]
        [SerializeField] private WorldMapCurrentZoneHUDController currentZoneHUD; // (옵션) 사용자용 현재 존 HUD
        [SerializeField] private WorldMapMiniGridController miniGridController; // (옵션) 미니맵 시드

        [Header("Phase 10: Zone Content Activation (Optional)")]
        [SerializeField] private WorldMapZoneContentController zoneContentController; // (옵션) 존 기반 콘텐츠 활성화 컨트롤러

        [Header("Ambient Fallback")]
        [SerializeField] private ZoneAmbientProfileSO outOfBoundsProfile; // (옵션) 경계 밖 폴백 프로필

        [Header("Mock Progress (Debug Mode)")]
        [SerializeField] private bool useMockProgress = true;
        [SerializeField] private string testUpgradeId = "upgrade_battery_mk2";
        [SerializeField] private string testLogId = "log_intro_01";
        [SerializeField] private string testNarrativeFlag = "mara_research_analysis_done";
        [SerializeField] private int mockHullTier = 2;
        [SerializeField] private int mockDepthLevel = 3;
        [SerializeField] private float mockSensorAccuracy = 0.8f;
        [SerializeField] private int mockLogCount = 5;

        // ===== 생성된 서비스 그래프 (읽기 전용 public accessor) =====
        private IZoneResolver _zoneResolver;
        private IZoneRepository _zoneRepository;
        private IUnlockConditionEvaluator _conditionEvaluator;
        private IZoneStateEvaluator _zoneStateEvaluator;
        private MockWorldProgressQuery _mockProgressQuery;
        private IWorldMapService _worldMapService;
        private IWorldAmbientApplier _ambientApplier;

        // Phase 8: 추적 대상 제공자
        private IWorldMapTrackedTargetProvider _targetProvider;

        private bool _isInitialized;
        private bool _isWorldMapInitialized; // WorldMapService.Initialize()가 호출되었는지
        private bool _hasLoggedWaitingForTarget;

        /// <summary>Installer가 생성한 WorldMapService (읽기 전용)</summary>
        public IWorldMapService WorldMapService => _worldMapService;

        /// <summary>Installer가 생성한 AmbientApplier (읽기 전용)</summary>
        public IWorldAmbientApplier AmbientApplier => _ambientApplier;

        /// <summary>Installer 초기화 완료 여부 (서비스 그래프 생성 완료)</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>현재 모드</summary>
        public InstallerMode Mode => mode;

        /// <summary>현재 추적 대상 제공자 (외부 주입용)</summary>
        public IWorldMapTrackedTargetProvider TargetProvider => _targetProvider;

        /// <summary>Gameplay 모드에서 외부에서 추적 대상 주입 (WorldMapBootstrapBridge 등에서 호출)</summary>
        public void SetGameplayTarget(Transform target)
        {
            if (_targetProvider is GameplayTrackedTargetProvider gameplayProvider)
            {
                gameplayProvider.SetTarget(target);
            }
            else
            {
                // Debug 모드에서도 호출 가능하도록 fallback
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeInstaller] SetGameplayTarget called but mode is not Gameplay. " +
                    "Assign trackedTransform in Inspector instead.");
                trackedTransform = target;
            }
        }

        private void Awake()
        {
            // Awake에서 서비스 그래프 생성 (다른 컴포넌트의 Start()보다 먼저 실행)
            BuildServiceGraph();
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                UnityEngine.Debug.LogError("[WorldMapRuntimeInstaller] Service graph build failed. Cannot initialize runtime.");
                return;
            }

            // Phase 8: 모드에 따라 추적 대상 제공자 생성
            CreateTargetProvider();

            // 1. AmbientReactionController 초기화 (Applier 생성 + 주입)
            InitializeAmbientController();

            // 2. DebugHUD에 서비스 직접 주입
            InjectServiceToDebugHUD();

            // 3. RuntimeTest에 외부 서비스 주입 (fallback 자체 생성 모드 방지)
            InjectServiceToRuntimeTest();

            // 4. Phase 9: 사용자용 HUD 및 미니맵 시드 주입
            InjectUserFacingHUD();

            // 5. WorldMapService 초기화 시도 (target이 이미 있으면 바로, 없으면 Update에서 대기)
            TryInitializeWorldMap();

            // 6. Phase 10: Zone Content Controller 초기화 (서비스 + config 주입)
            InitializeZoneContentController();

            UnityEngine.Debug.Log($"[WorldMapRuntimeInstaller] Runtime composition complete. Mode: {mode}. " +
                $"WorldMap initialized: {_isWorldMapInitialized}");
        }

        private void Update()
        {
            if (!_isInitialized || _worldMapService == null)
                return;

            // Phase 8: late binding — target 획득 시도
            if (_targetProvider != null)
            {
                _targetProvider.TryAcquireTarget();

                // 아직 WorldMap이 초기화되지 않았고 target이 생겼으면 초기화
                if (!_isWorldMapInitialized && _targetProvider.IsTargetAvailable)
                {
                    _worldMapService.Initialize(_targetProvider.CurrentWorldPosition);
                    _isWorldMapInitialized = _worldMapService.HasCurrentZone;

                    if (_isWorldMapInitialized)
                    {
                        UnityEngine.Debug.Log($"[WorldMapRuntimeInstaller] WorldMap initialized at zone: {_worldMapService.CurrentZoneId} (late binding)");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[WorldMapRuntimeInstaller] WorldMap initialization completed but no current zone set (position may be out of bounds).");
                    }
                    return;
                }

                // target이 없으면 대기 로그 (스팸 방지)
                if (!_isWorldMapInitialized && !_targetProvider.IsTargetAvailable)
                {
                    if (!_hasLoggedWaitingForTarget)
                    {
                        UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] Waiting for tracked target to become available...");
                        _hasLoggedWaitingForTarget = true;
                    }
                    return;
                }
            }

            // target이 없으면 skip
            Transform target = GetCurrentTarget();
            if (target == null)
                return;

            // 매 프레임 trackedTransform 위치 기반 현재 존 갱신
            _worldMapService.RefreshCurrentZone(target.position);
        }

        /// <summary>현재 추적 대상 Transform 반환 (provider 우선, fallback으로 trackedTransform)</summary>
        private Transform GetCurrentTarget()
        {
            if (_targetProvider != null && _targetProvider.IsTargetAvailable)
                return _targetProvider.CurrentTarget;

            return trackedTransform;
        }

        // ===== Phase 8: Target Provider Creation =====

        /// <summary>모드에 따라 추적 대상 제공자 생성</summary>
        private void CreateTargetProvider()
        {
            switch (mode)
            {
                case InstallerMode.Debug:
                    // Debug 모드: 명시적 Transform > Player 태그 > 오브젝트 이름
                    _targetProvider = new DebugTrackedTargetProvider(trackedTransform, "Submarine");
                    UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] Using DebugTrackedTargetProvider (tag: Player, fallback name: Submarine).");
                    break;

                case InstallerMode.Gameplay:
                    // Gameplay 모드: 명시적 주입 기대, trackedTransform을 초기값으로 사용
                    _targetProvider = new GameplayTrackedTargetProvider(trackedTransform);
                    UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] Using GameplayTrackedTargetProvider. " +
                        "Call SetGameplayTarget() from bootstrap to assign the tracked transform.");
                    break;
            }
        }

        /// <summary>WorldMapService 초기화 시도 (target이 있으면 바로 실행)</summary>
        private void TryInitializeWorldMap()
        {
            Transform target = GetCurrentTarget();
            if (target == null)
            {
                _hasLoggedWaitingForTarget = false; // Update에서 로그
                UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] No tracked target available yet. WorldMap will initialize once target is acquired.");
                return;
            }

            Vector3 initialPosition = target.position;
            _worldMapService.Initialize(initialPosition);
            _isWorldMapInitialized = _worldMapService.HasCurrentZone;

            if (_isWorldMapInitialized)
            {
                UnityEngine.Debug.Log($"[WorldMapRuntimeInstaller] WorldMap initialized at zone: {_worldMapService.CurrentZoneId}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeInstaller] WorldMap initialization completed but no current zone set (position may be out of bounds).");
            }
        }

        // ===== Service Graph Construction =====

        /// <summary>서비스 그래프 빌드 (정확히 한 번 실행)</summary>
        private void BuildServiceGraph()
        {
            if (worldMapConfig == null)
            {
                UnityEngine.Debug.LogError("[WorldMapRuntimeInstaller] WorldMapConfigSO is not assigned.");
                return;
            }

            // 1. ZoneResolver
            _zoneResolver = new ZoneResolver(worldMapConfig);

            // 2. ZoneRepository
            _zoneRepository = new ZoneRepository(worldMapConfig, zoneDataAssets);

            // 3. ConditionEvaluator
            _conditionEvaluator = new UnlockConditionEvaluator();

            // 4. ZoneStateEvaluator
            _zoneStateEvaluator = new ZoneStateEvaluator(_conditionEvaluator);

            // 5. MockProgressQuery (디버그 모드)
            if (useMockProgress)
            {
                _mockProgressQuery = new MockWorldProgressQuery();
                _mockProgressQuery.AddUpgrade(testUpgradeId);
                _mockProgressQuery.AddLog(testLogId);
                _mockProgressQuery.AddNarrativeFlag(testNarrativeFlag);
                _mockProgressQuery.SetHullTier(mockHullTier);
                _mockProgressQuery.SetDepthLevel(mockDepthLevel);
                _mockProgressQuery.SetSensorAccuracy(mockSensorAccuracy);

                for (int i = 0; i < mockLogCount; i++)
                {
                    _mockProgressQuery.AddLog($"log_test_{i}");
                }
            }

            // 6. WorldMapService
            _worldMapService = new WorldMapService(
                _zoneResolver,
                _zoneRepository,
                _zoneStateEvaluator,
                useMockProgress ? _mockProgressQuery : null);

            // 7. DebugWorldAmbientApplier (Camera 참조)
            _ambientApplier = new DebugWorldAmbientApplier(
                targetCamera != null ? targetCamera : Camera.main,
                applyFog: true,
                applyAmbientLight: true,
                logBgmChanges: true);

            _isInitialized = true;
            UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] Service graph built successfully.");
        }

        // ===== Injection Methods =====

        /// <summary>AmbientReactionController 초기화 (Applier 생성 + 주입)</summary>
        private void InitializeAmbientController()
        {
            if (ambientController == null)
                return;

            // Applier 초기화
            _ambientApplier.Initialize();

            // Controller.Initialize() 호출 (service, applier, fallbackProfile)
            ambientController.Initialize(_worldMapService, _ambientApplier, outOfBoundsProfile);

            UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] AmbientReactionController initialized with shared service and applier.");
        }

        /// <summary>DebugHUD에 서비스 직접 주입 (IDebugHUDServiceReceiver 인터페이스 경유, 리플렉션 불필요)</summary>
        private void InjectServiceToDebugHUD()
        {
            if (debugHUDComponent == null)
                return;

            // IDebugHUDServiceReceiver 인터페이스로 캐스팅 시도
            // WorldMapDebugHUDRuntime의 Controller가 이 인터페이스를 구현함
            if (debugHUDComponent is IDebugHUDServiceReceiver directReceiver)
            {
                Transform target = GetCurrentTarget();
                directReceiver.SetService(_worldMapService, target);
                UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] DebugHUD injected with shared service directly (via IDebugHUDServiceReceiver).");
                return;
            }

            // fallback: Controller 프로퍼티를 통해 간접 접근 (WorldMapDebugHUDRuntime 구조)
            System.Type hudType = debugHUDComponent.GetType();
            System.Reflection.PropertyInfo controllerProp = hudType.GetProperty("Controller");
            if (controllerProp == null)
                return;

            object controller = controllerProp.GetValue(debugHUDComponent);
            if (controller == null)
                return;

            // Controller가 IDebugHUDServiceReceiver를 구현했는지 확인
            if (controller is IDebugHUDServiceReceiver receiver)
            {
                Transform target = GetCurrentTarget();
                receiver.SetService(_worldMapService, target);
                UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] DebugHUD injected with shared service directly (via Controller property + IDebugHUDServiceReceiver).");
            }
        }

        /// <summary>RuntimeTest에 외부 서비스 주입 (자체 생성 모드 방지)</summary>
        private void InjectServiceToRuntimeTest()
        {
            if (runtimeTest == null)
                return;

            Transform target = GetCurrentTarget();

            // SetRuntimeServices()로 외부 주입 모드 전환
            runtimeTest.SetRuntimeServices(_worldMapService, target);

            UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] RuntimeTest switched to external injection mode.");
        }

        // ===== Phase 9: User-Facing HUD Injection =====

        /// <summary>사용자용 현재 존 HUD 및 미니맵 시드에 서비스 주입</summary>
        private void InjectUserFacingHUD()
        {
            Transform target = GetCurrentTarget();

            // 현재 존 HUD 주입
            if (currentZoneHUD != null)
            {
                currentZoneHUD.Initialize(_worldMapService, target);
                UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] CurrentZoneHUD initialized with shared service.");
            }

            // 미니맵 시드 주입
            if (miniGridController != null)
            {
                miniGridController.Initialize(_worldMapService);
                UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] MiniGridController initialized with shared service.");
            }
        }

        // ===== Phase 10: Zone Content Activation =====

        /// <summary>ZoneContentController 초기화 (서비스 + config 주입)</summary>
        private void InitializeZoneContentController()
        {
            if (zoneContentController == null)
                return;

            // Initialize()를 호출하여 서비스와 config 주입
            zoneContentController.Initialize(_worldMapService, worldMapConfig);

            UnityEngine.Debug.Log("[WorldMapRuntimeInstaller] ZoneContentController initialized with shared service and config.");
        }
    }
}
