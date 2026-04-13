using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Submarine;
using Project.Gameplay.CameraSystem;
using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using Project.Gameplay.Interaction;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using Project.Gameplay.UserInput;
using Project.UI.Harvest;
using Project.UI.Inventory;
using UnityEngine;

namespace Project.Managers.Composition
{
    /// <summary>테스트베드 씬에서 런타임 서비스와 카메라 전환을 조립하는 클래스</summary>
    public class DeepLightTestBedBootstrapper : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SubmarineStatsSO submarineStats; // 잠수함 기본 데이터
        [SerializeField] private HarvestRecoveryTuningSO harvestRecoveryTuning; // 회수 계산 튜닝

        [Header("Time Settings")]
        [SerializeField] private int startDay = 1; // 시작 Day
        [SerializeField] private float startHourOfDay = 9f; // 시작 시각 (12시간제 기준 예: 늦은 시간 표현 가능)
        [SerializeField] private float activeRealSecondsPerGameHour = 180f; // 인게임 1시간당 실제 액티브 시간
        [SerializeField] private GameDayLengthMode initialDayLengthMode = GameDayLengthMode.TwelveHour; // 초반 하루 길이 모드

        [Header("Scene References")]
        [SerializeField] private Transform playerTransform; // 플레이어 Transform
        [SerializeField] private TestBedPlayerMover playerMover; // 잠수함 이동 컨트롤러
        [SerializeField] private Rigidbody playerRigidbody; // 잠수함 rigidbody
        [SerializeField] private HarvestPointInteractor harvestPointInteractor; // F키 Harvest 진입 컴포넌트
        [SerializeField] private HarvestWorldVisibilityController harvestWorldVisibilityController; // Harvest 중 월드 타깃 가시성 제어기
        [SerializeField] private SubmarineCollisionDangerSensor collisionDangerSensor; // 충돌 위험 감지 센서
        [SerializeField] private SubmarineLightController submarineLightController; // 잠수함 조명 컨트롤러

        [Header("Camera References")]
        [SerializeField] private PerspectiveSwapController perspectiveSwapController; // 카메라 전환 컨트롤러
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 컨트롤러
        [SerializeField] private HarvestConsoleCameraController harvestConsoleCameraController; // 회수 콘솔 카메라 컨트롤러
        [SerializeField] private Transform cockpitViewAnchor; // 조종실 1인칭 시점 앵커

        [Header("Runtime Helpers")]
        [SerializeField] private GameTimeFlowController gameTimeFlowController; // 시간 흐름 컨트롤러
        [SerializeField] private ExplorationNavigationTelemetryPublisher navigationTelemetryPublisher; // 방향 HUD 백엔드

        [Header("Input References")]
        [SerializeField] private HarvestConsoleController harvestConsoleController; // 회수 콘솔 입력 컨트롤러

        [Header("UI Controllers (To Initialize)")]
        [SerializeField] private HarvestResultUIController harvestResultUIController; // 채집 결과 UI
        [SerializeField] private InventoryGrabbedItemPresenter grabbedItemPresenter; // grab/release 프리젠터
        [SerializeField] private InventoryGridBuilder gridBuilder; // 슬롯 생성기
        [SerializeField] private InventoryGridPresenter gridPresenter; // 그리드 프리젠터

        private SubmarineRuntimeState submarineRuntimeState; // 잠수함 상태
        private HarvestModeSession harvestModeSession; // 채집 세션
        private HarvestModeCoordinator harvestModeCoordinator; // 채집 조정기
        private HarvestResolver harvestResolver; // 채집 해석기
        private GameModeService gameModeService; // 게임 모드 서비스
        private EncyclopediaService encyclopediaService; // 도감 서비스
        private InventoryService inventoryService; // 인벤토리 서비스
        private GameTimeService gameTimeService; // 인게임 시간 서비스
        private HarvestRetryPenaltyService harvestRetryPenaltyService; // 타깃별 재시도 패널티 서비스

        /// <summary>런타임 조립과 주입을 수행한다</summary>
        private void Awake()
        {
            if (submarineStats == null)
            {
                Debug.LogError("[DeepLight] Bootstrap data is missing.");
                enabled = false;
                return;
            }

            submarineRuntimeState = new SubmarineRuntimeState(submarineStats);
            harvestModeSession = new HarvestModeSession();

            encyclopediaService = new EncyclopediaService();
            inventoryService = new InventoryService(submarineRuntimeState);

            // 초반은 12시간제 하루로 시작한다.
            gameTimeService = new GameTimeService(
                startDay,
                startHourOfDay,
                activeRealSecondsPerGameHour,
                initialDayLengthMode);

            gameModeService = new GameModeService(GameModeType.Exploration3D);
            harvestRetryPenaltyService = new HarvestRetryPenaltyService(gameTimeService);
            harvestResolver = new HarvestResolver(
                submarineRuntimeState,
                inventoryService,
                harvestRecoveryTuning,
                harvestRetryPenaltyService);

            harvestModeCoordinator = new HarvestModeCoordinator(gameModeService, harvestModeSession);

            // 시스템 초기화
            if (harvestPointInteractor != null)
                harvestPointInteractor.Initialize(harvestModeCoordinator, harvestRetryPenaltyService);

            if (harvestWorldVisibilityController != null)
                harvestWorldVisibilityController.Initialize(harvestModeSession);

            if (collisionDangerSensor != null)
                collisionDangerSensor.Initialize(submarineRuntimeState);

            if (playerMover == null && playerTransform != null)
                playerMover = playerTransform.GetComponent<TestBedPlayerMover>();

            if (playerRigidbody == null && playerTransform != null)
                playerRigidbody = playerTransform.GetComponent<Rigidbody>();

            if (playerMover != null)
                playerMover.Initialize(gameTimeService);

            if (gameTimeFlowController != null)
            {
                gameTimeFlowController.Initialize(
                    gameTimeService,
                    gameModeService,
                    playerMover,
                    playerRigidbody);
            }

            if (navigationTelemetryPublisher != null && playerTransform != null)
                navigationTelemetryPublisher.SetTarget(playerTransform);

            if (harvestConsoleCameraController != null && cockpitViewAnchor != null)
                harvestConsoleCameraController.SetCockpitViewAnchor(cockpitViewAnchor);

            if (harvestConsoleController != null)
                harvestConsoleController.Initialize(harvestModeSession, harvestResolver, harvestModeCoordinator);

            if (harvestResultUIController != null)
                harvestResultUIController.Initialize(encyclopediaService, gameTimeService);

            if (grabbedItemPresenter != null)
                grabbedItemPresenter.Initialize(inventoryService);

            if (submarineStats != null && submarineStats.InventoryLayout != null)
            {
                if (gridBuilder != null)
                    gridBuilder.Initialize(submarineStats.InventoryLayout);

                if (gridPresenter != null)
                {
                    gridPresenter.Initialize(
                        submarineStats.InventoryLayout,
                        encyclopediaService,
                        gameTimeService);

                    gridPresenter.RefreshOccupiedState(submarineRuntimeState.InventoryGrid);
                }
            }
        }

        /// <summary>초기 상태 이벤트를 발행한다</summary>
        private void Start()
        {
            if (!enabled)
                return;

            EventBus.Publish(new BatteryChangedEvent(
                submarineRuntimeState.CurrentBattery,
                submarineStats.MaxBattery));

            EventBus.Publish(new HullDurabilityChangedEvent(
                submarineRuntimeState.CurrentHullDurability,
                submarineStats.MaxHullDurability));

            gameTimeService?.PublishCurrentState();
        }

        /// <summary>방전 강제 종료 이벤트를 구독한다</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>이벤트를 해제한다</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>방전 시 회수 콘솔 모드를 종료한다</summary>
        private void OnHarvestSessionForcedEndedByBattery(HarvestSessionForcedEndedByBatteryEvent publishedEvent)
        {
            if (harvestModeCoordinator == null)
                return;

            harvestModeCoordinator.ExitHarvestMode();
        }
    }
}
