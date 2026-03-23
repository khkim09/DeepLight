using Project.Core.Events;
using Project.Data.Claw;
using Project.Data.Submarine;
using Project.Gameplay.CameraSystem;
using Project.Gameplay.Debug;
using Project.Gameplay.DebugView;
using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using Project.Gameplay.Interaction;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using Project.Gameplay.UserInput;
using UnityEngine;

namespace Project.Managers.Composition
{
    /// <summary>테스트베드 씬에서 런타임 서비스와 입력 브리지를 조립하는 클래스</summary>
    public class DeepLightTestBedBootstrapper : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SubmarineStatsSO submarineStats; // 잠수함 기본 데이터
        [SerializeField] private ClawStatsSO clawStats; // 로봇 팔 기본 데이터

        [Header("Scene References")]
        [SerializeField] private Transform playerTransform; // 플레이어 Transform
        [SerializeField] private HarvestPointInteractor harvestPointInteractor; // E키 진입 컴포넌트
        [SerializeField] private HarvestAttemptInputController harvestAttemptInputController; // 스페이스 시도 컴포넌트
        [SerializeField] private TestHarvestInvoker testHarvestInvoker; // 테스트용 강제 진입 컴포넌트

        [Header("Camera References")]
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 컨트롤러
        [SerializeField] private HarvestCinematicCameraController harvestCinematicCameraController; // 채집 카메라 컨트롤러

        [Header("UI References")]
        [SerializeField] private InventoryGridDebugView inventoryGridDebugView; // 인벤토리 그리드 디버그 UI

        private SubmarineRuntimeState submarineRuntimeState; // 잠수함 상태
        private ClawRuntimeState clawRuntimeState; // 로봇 팔 상태
        private InventoryService inventoryService; // 인벤토리 서비스
        private HarvestResolver harvestResolver; // 채집 해석기
        private GameModeService gameModeService; // 게임 모드 서비스
        private HarvestModeSession harvestModeSession; // 채집 세션
        private HarvestModeCoordinator harvestModeCoordinator; // 채집 조정기

        /// <summary>런타임 조립과 주입을 수행한다</summary>
        private void Awake()
        {
            // 데이터 누락 검사
            if (submarineStats == null || clawStats == null)
            {
                UnityEngine.Debug.LogError("[DeepLight] Bootstrap data is missing.");
                enabled = false;
                return;
            }

            // 상태 생성
            submarineRuntimeState = new SubmarineRuntimeState(submarineStats);
            clawRuntimeState = new ClawRuntimeState(clawStats);

            // 서비스 생성
            inventoryService = new InventoryService(submarineRuntimeState);
            harvestResolver = new HarvestResolver(submarineRuntimeState, clawRuntimeState, inventoryService);
            gameModeService = new GameModeService(GameModeType.Exploration3D);
            harvestModeSession = new HarvestModeSession();
            harvestModeCoordinator = new HarvestModeCoordinator(gameModeService, harvestModeSession);

            // 상호작용 브리지 주입
            if (harvestPointInteractor != null)
                harvestPointInteractor.Initialize(harvestModeCoordinator);

            // 채집 시도 입력 브리지 주입
            if (harvestAttemptInputController != null)
                harvestAttemptInputController.Initialize(harvestModeSession, harvestResolver);

            // 테스트 진입 브리지 주입
            if (testHarvestInvoker != null)
                testHarvestInvoker.Initialize(harvestModeCoordinator);

            // 탐사 카메라 타깃 주입
            if (explorationFollowCameraController != null && playerTransform != null)
                explorationFollowCameraController.SetTarget(playerTransform);

            // 채집 카메라 타깃 주입
            if (harvestCinematicCameraController != null && playerTransform != null)
                harvestCinematicCameraController.SetTarget(playerTransform);

            // 인벤토리 UI 초기화
            if (inventoryGridDebugView != null)
                inventoryGridDebugView.Initialize(submarineRuntimeState);
        }

        /// <summary>초기 상태 이벤트를 발행한다</summary>
        private void Start()
        {
            // 비정상 초기화면 중단
            if (!enabled)
                return;

            // 초기 배터리 상태 동기화
            EventBus.Publish(new BatteryChangedEvent(submarineRuntimeState.CurrentBattery, submarineStats.MaxBattery));

            // 초기 선체 내구도 상태 동기화
            EventBus.Publish(new HullDurabilityChangedEvent(submarineRuntimeState.CurrentHullDurability, submarineStats.MaxHullDurability));

            // 초기 로봇 팔 내구도 상태 동기화
            EventBus.Publish(new ClawDurabilityChangedEvent(clawRuntimeState.CurrentDurability, clawStats.MaxDurability));
        }

        /// <summary>방전 강제 종료 이벤트를 구독한다</summary>
        private void OnEnable()
        {
            // 방전 시 채집 모드 강제 종료 감지
            EventBus.Subscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>이벤트 구독을 해제한다</summary>
        private void OnDisable()
        {
            // 구독 정리
            EventBus.Unsubscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>방전 시 채집 모드를 종료한다</summary>
        private void OnHarvestSessionForcedEndedByBattery(HarvestSessionForcedEndedByBatteryEvent publishedEvent)
        {
            // 조정기 없으면 중단
            if (harvestModeCoordinator == null)
                return;

            // 탐사 모드로 복귀
            harvestModeCoordinator.ExitHarvestMode();
        }
    }
}
