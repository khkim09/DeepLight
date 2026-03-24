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
        [SerializeField] private PerspectiveSwapController perspectiveSwapController; // 카메라 전환 컨트롤러
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 컨트롤러
        [SerializeField] private HarvestCinematicCameraController harvestCinematicCameraController; // 채집 카메라 컨트롤러

        [Header("Debug View References")]
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
                Debug.LogError("[DeepLight] Bootstrap data is missing.");
                enabled = false; // 이후 처리 중단
                return;
            }

            // 상태 생성
            submarineRuntimeState = new SubmarineRuntimeState(submarineStats); // 잠수함 상태 생성
            clawRuntimeState = new ClawRuntimeState(clawStats); // 로봇 팔 상태 생성

            // 서비스 생성
            inventoryService = new InventoryService(submarineRuntimeState); // 인벤토리 서비스 생성
            harvestResolver = new HarvestResolver(submarineRuntimeState, clawRuntimeState, inventoryService); // 채집 해석기 생성
            gameModeService = new GameModeService(GameModeType.Exploration3D); // 초기 게임 모드 생성
            harvestModeSession = new HarvestModeSession(); // 채집 세션 생성
            harvestModeCoordinator = new HarvestModeCoordinator(gameModeService, harvestModeSession); // 채집 조정기 생성

            // 상호작용 브리지 주입
            if (harvestPointInteractor != null)
                harvestPointInteractor.Initialize(harvestModeCoordinator);

            // 채집 입력 브리지 주입
            if (harvestAttemptInputController != null)
                harvestAttemptInputController.Initialize(harvestModeSession, harvestResolver);

            // 테스트용 진입 브리지 주입
            if (testHarvestInvoker != null)
                testHarvestInvoker.Initialize(harvestModeCoordinator);

            // 탐사 카메라 타깃 주입
            if (explorationFollowCameraController != null && playerTransform != null)
                explorationFollowCameraController.SetTarget(playerTransform);

            // 채집 카메라 고정 포즈 스냅
            if (harvestCinematicCameraController != null)
                harvestCinematicCameraController.SnapToDesiredPose();

            // 카메라 전환 컨트롤러 참조 주입
            if (perspectiveSwapController != null)
            {
                // 별도 look target은 이제 직접 따라보지 않지만, 추후 확장 대비 playerTransform을 연결
                // inspector에서 참조가 이미 연결되어 있더라도 안전하게 유지
                // 현재 구조상 PerspectiveSwapController는 각 카메라 컨트롤러에서 목표 포즈를 얻는다.
            }

            // 인벤토리 디버그 UI 초기화
            if (inventoryGridDebugView != null)
                inventoryGridDebugView.Initialize(submarineRuntimeState);
        }

        /// <summary>초기 상태 이벤트를 발행한다</summary>
        private void Start()
        {
            // 비정상 초기화면 중단
            if (!enabled) return;

            // 초기 상태 동기화
            EventBus.Publish(new BatteryChangedEvent(submarineRuntimeState.CurrentBattery, submarineStats.MaxBattery));
            EventBus.Publish(new HullDurabilityChangedEvent(submarineRuntimeState.CurrentHullDurability, submarineStats.MaxHullDurability));
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
            EventBus.Unsubscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>방전 시 채집 모드를 종료한다</summary>
        private void OnHarvestSessionForcedEndedByBattery(HarvestSessionForcedEndedByBatteryEvent publishedEvent)
        {
            if (harvestModeCoordinator == null) return;

            // 탐사 모드로 복귀
            harvestModeCoordinator.ExitHarvestMode();
        }
    }
}
