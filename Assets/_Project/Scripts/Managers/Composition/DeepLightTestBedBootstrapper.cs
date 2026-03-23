using Project.Core.Events;
using Project.Data.Claw;
using Project.Data.Submarine;
using Project.Gameplay.Debug;
using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using Project.Gameplay.Interaction;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using Project.Gameplay.UserInput;
using UnityEngine;

namespace Project.Managers.Composition
{
    /// <summary>테스트베드 씬에서 런타임 서비스와 입력 브리지 조립 담당</summary>
    public class DeepLightTestBedBootstrapper : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SubmarineStatsSO submarineStats; // 잠수함 기본 데이터
        [SerializeField] private ClawStatsSO clawStats; // 로봇 팔 기본 데이터

        [Header("Scene References")]
        [SerializeField] private HarvestPointInteractor harvestPointInteractor; // E키 진입 컴포넌트
        [SerializeField] private HarvestAttemptInputController harvestAttemptInputController; // 스페이스 시도 컴포넌트
        [SerializeField] private TestHarvestInvoker testHarvestInvoker; // 테스트용 강제 진입 컴포넌트

        private SubmarineRuntimeState submarineRuntimeState; // 잠수함 상태
        private ClawRuntimeState clawRuntimeState; // 로봇 팔 상태
        private InventoryService inventoryService; // 인벤토리 서비스
        private HarvestResolver harvestResolver; // 채집 해석기
        private GameModeService gameModeService; // 게임 모드 서비스
        private HarvestModeSession harvestModeSession; // 채집 세션
        private HarvestModeCoordinator harvestModeCoordinator; // 채집 조정기

        /// <summary>런타임 조립과 주입을 수행</summary>
        private void Awake()
        {
            // 데이터 누락 검사
            if (submarineStats == null || clawStats == null)
            {
                Debug.LogError("[DeepLight] Bootstrap data is missing.");
                enabled = false; // 이후 콜백 중단
                return;
            }

            // 상태 생성
            submarineRuntimeState = new SubmarineRuntimeState(submarineStats); // 잠수함 상태 생성
            clawRuntimeState = new ClawRuntimeState(clawStats); // 로봇 팔 상태 생성

            // 서비스 생성
            inventoryService = new InventoryService(submarineRuntimeState); // 인벤토리 서비스 생성
            harvestResolver = new HarvestResolver(submarineRuntimeState, clawRuntimeState, inventoryService); // 채집 해석기 생성
            gameModeService = new GameModeService(GameModeType.Exploration3D); // 초기 모드 설정
            harvestModeSession = new HarvestModeSession(); // 채집 세션 생성
            harvestModeCoordinator = new HarvestModeCoordinator(gameModeService, harvestModeSession); // 조정기 생성

            // 상호작용 브리지 주입
            if (harvestPointInteractor != null)
                harvestPointInteractor.Initialize(harvestModeCoordinator);

            // 채집 시도 입력 브리지 주입
            if (harvestAttemptInputController != null)
                harvestAttemptInputController.Initialize(harvestModeSession, harvestResolver);

            // 테스트용 진입/종료 브리지 주입
            if (testHarvestInvoker != null)
                testHarvestInvoker.Initialize(harvestModeCoordinator);
        }

        /// <summary>초기 상태 이벤트를 발행</summary>
        private void Start()
        {
            // 비정상 초기화면 중단
            if (!enabled) return;

            // 초기 상태 동기화 (배터리, 선체/로봇 팔 내구도)
            EventBus.Publish(new BatteryChangedEvent(submarineRuntimeState.CurrentBattery, submarineStats.MaxBattery));
            EventBus.Publish(new HullDurabilityChangedEvent(submarineRuntimeState.CurrentHullDurability, submarineStats.MaxHullDurability));
            EventBus.Publish(new ClawDurabilityChangedEvent(clawRuntimeState.CurrentDurability, clawStats.MaxDurability));
        }

        /// <summary>방전 강제 종료 이벤트를 구독</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>이벤트 구독을 해제</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestSessionForcedEndedByBatteryEvent>(OnHarvestSessionForcedEndedByBattery);
        }

        /// <summary>방전 시 채집 모드를 종료</summary>
        private void OnHarvestSessionForcedEndedByBattery(HarvestSessionForcedEndedByBatteryEvent publishedEvent)
        {
            if (harvestModeCoordinator == null) return;

            harvestModeCoordinator.ExitHarvestMode();
        }
    }
}
