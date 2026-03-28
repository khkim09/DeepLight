using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Submarine;
using Project.Gameplay.CameraSystem;
using Project.Gameplay.DebugView;
using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using Project.Gameplay.Interaction;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using UnityEngine;

namespace Project.Managers.Composition
{
    /// <summary>테스트베드 씬에서 런타임 서비스와 카메라 전환을 조립하는 클래스</summary>
    public class DeepLightTestBedBootstrapper : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SubmarineStatsSO submarineStats; // 잠수함 기본 데이터
        [SerializeField] private HarvestRecoveryTuningSO harvestRecoveryTuning; // 회수 계산 튜닝

        [Header("Scene References")]
        [SerializeField] private Transform playerTransform; // 플레이어 Transform
        [SerializeField] private HarvestPointInteractor harvestPointInteractor; // F키 Harvest 진입 컴포넌트
        [SerializeField] private HarvestWorldVisibilityController harvestWorldVisibilityController; // Harvest 중 월드 타깃 가시성 제어기

        [Header("Camera References")]
        [SerializeField] private PerspectiveSwapController perspectiveSwapController; // 카메라 전환 컨트롤러
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 컨트롤러
        [SerializeField] private HarvestConsoleCameraController harvestConsoleCameraController; // 회수 콘솔 카메라 컨트롤러
        [SerializeField] private Transform cockpitViewAnchor; // 조종실 1인칭 시점 앵커

        [Header("Input References")]
        [SerializeField] private HarvestConsoleController harvestConsoleController; // 회수 콘솔 입력 컨트롤러

        [Header("Debug View References")]
        [SerializeField] private InventoryGridDebugView inventoryGridDebugView; // 인벤토리 그리드 디버그 UI

        private SubmarineRuntimeState submarineRuntimeState; // 잠수함 상태
        private InventoryService inventoryService; // 인벤토리 서비스
        private HarvestResolver harvestResolver; // 채집 해석기
        private GameModeService gameModeService; // 게임 모드 서비스
        private HarvestModeSession harvestModeSession; // 채집 세션
        private HarvestModeCoordinator harvestModeCoordinator; // 채집 조정기

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

            inventoryService = new InventoryService(submarineRuntimeState);
            harvestResolver = new HarvestResolver(submarineRuntimeState, inventoryService, harvestRecoveryTuning);
            gameModeService = new GameModeService(GameModeType.Exploration3D);
            harvestModeSession = new HarvestModeSession();
            harvestModeCoordinator = new HarvestModeCoordinator(gameModeService, harvestModeSession);

            if (harvestPointInteractor != null)
                harvestPointInteractor.Initialize(harvestModeCoordinator);

            if (harvestWorldVisibilityController != null)
                harvestWorldVisibilityController.Initialize(harvestModeSession);

            if (explorationFollowCameraController != null && playerTransform != null)
                explorationFollowCameraController.SetTarget(playerTransform);

            if (harvestConsoleCameraController != null && cockpitViewAnchor != null)
                harvestConsoleCameraController.SetCockpitViewAnchor(cockpitViewAnchor);

            if (harvestConsoleController != null)
                harvestConsoleController.Initialize(
                    harvestModeSession,
                    harvestResolver,
                    harvestModeCoordinator);

            if (inventoryGridDebugView != null)
                inventoryGridDebugView.Initialize(submarineRuntimeState);
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
        }

        /// <summary>방전 강제 종료 이벤트를 구독한다</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>이벤트 구독을 해제한다</summary>
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
