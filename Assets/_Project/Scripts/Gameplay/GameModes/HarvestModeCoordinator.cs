using Project.Gameplay.Harvest;

namespace Project.Gameplay.GameModes
{
    // 카메라/입력/UI와 연결되는 조립용 클래스
    /// <summary>채집 대상 진입과 모드 전환 흐름 조정</summary>
    public class HarvestModeCoordinator
    {
        private readonly GameModeService gameModeService; // 게임 모드 서비스
        private readonly HarvestModeSession harvestModeSession; // 채집 세션 상태

        public HarvestModeSession HarvestModeSession => harvestModeSession;

        /// <summary>채집 모드 조정기 생성</summary>
        public HarvestModeCoordinator(GameModeService gameModeService, HarvestModeSession harvestModeSession)
        {
            this.gameModeService = gameModeService; // 모드 서비스 보관
            this.harvestModeSession = harvestModeSession; // 세션 보관
        }

        /// <summary>채집 대상과 함께 채집 모드 진입</summary>
        public bool TryEnterHarvestMode(IHarvestTarget target)
        {
            // 대상 유효성 검사
            if (target == null) return false;

            // 사용 가능 여부 검사
            if (!target.IsAvailable) return false;

            // 대상 저장
            harvestModeSession.SetTarget(target);

            // 모드 전환
            gameModeService.EnterHarvestMode();
            return true;
        }

        /// <summary>탐사 모드로 복귀</summary>
        public void ExitHarvestMode()
        {
            // 대상 초기화
            harvestModeSession.ClearTarget();

            // 탐사 모드 복귀
            gameModeService.EnterExplorationMode();
        }
    }
}
