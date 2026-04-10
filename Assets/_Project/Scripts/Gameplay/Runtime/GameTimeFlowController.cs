using Project.Gameplay.GameModes;
using Project.Gameplay.Services;
using Project.Gameplay.UserInput;
using UnityEngine;

namespace Project.Gameplay.Runtime
{
    /// <summary>플레이어의 실제 플레이 활동 여부에 따라 게임 시간을 흐르게 하는 컨트롤러이다.</summary>
    public class GameTimeFlowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TestBedPlayerMover playerMover; // 실제 이동 상태 확인용
        [SerializeField] private Rigidbody playerRigidbody;      // 실제 속도 확인용 보조 참조

        [Header("Settings")]
        [SerializeField] private float minimumMoveSpeedForTimeFlow = 0.15f; // 이 속도 이상이면 이동 중으로 간주

        private GameTimeService gameTimeService; // 시간 서비스
        private GameModeService gameModeService; // 현재 모드 서비스
        private int externalInteractionCount;    // 마을 UI/상호작용 등 외부 활동 카운트

        /// <summary>시간 흐름 컨트롤러를 초기화한다.</summary>
        public void Initialize(
            GameTimeService newGameTimeService,
            GameModeService newGameModeService,
            TestBedPlayerMover newPlayerMover,
            Rigidbody newPlayerRigidbody)
        {
            gameTimeService = newGameTimeService;
            gameModeService = newGameModeService;
            playerMover = newPlayerMover;
            playerRigidbody = newPlayerRigidbody;
        }

        /// <summary>외부 상호작용 시작을 등록한다.</summary>
        public void BeginInteraction()
        {
            externalInteractionCount++;
        }

        /// <summary>외부 상호작용 종료를 등록한다.</summary>
        public void EndInteraction()
        {
            externalInteractionCount = Mathf.Max(0, externalInteractionCount - 1);
        }

        /// <summary>매 프레임 플레이 활동 상태를 검사해 시간이 흐를지 결정한다.</summary>
        private void Update()
        {
            if (gameTimeService == null)
                return;

            if (!ShouldAdvanceTime())
                return;

            gameTimeService.AdvanceByActiveRealSeconds(Time.deltaTime);
        }

        /// <summary>현재 플레이 활동 상태를 기준으로 시간이 흘러야 하는지 반환한다.</summary>
        private bool ShouldAdvanceTime()
        {
            if (externalInteractionCount > 0)
                return true;

            if (gameModeService != null && gameModeService.IsHarvestMode())
                return true;

            if (playerRigidbody != null && playerRigidbody.linearVelocity.magnitude >= minimumMoveSpeedForTimeFlow)
                return true;

            return false;
        }
    }
}
