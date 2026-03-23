using Project.Core.Events;
using Project.Data.Harvest;
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

            // 대상 데이터 참조
            HarvestTargetSO targetData = target.TargetData;
            if (targetData == null || !targetData.IsValid()) return false;

            // 세션 대상 저장
            harvestModeSession.SetTarget(target);

            // 세션 시작 이벤트 발행
            EventBus.Publish(new HarvestSessionStartedEvent(targetData.TargetId, targetData.ItemData.ItemId));

            // 모드 전환
            gameModeService.EnterHarvestMode();
            return true;
        }

        /// <summary>탐사 모드로 복귀</summary>
        public void ExitHarvestMode()
        {
            // 현재 대상 ID 백업
            string targetId = string.Empty;
            if (harvestModeSession.CurrentTarget != null && harvestModeSession.CurrentTarget.TargetData != null)
                targetId = harvestModeSession.CurrentTarget.TargetData.TargetId;

            // 세션 종료 이벤트 발행
            if (!string.IsNullOrWhiteSpace(targetId))
                EventBus.Publish(new HarvestSessionEndedEvent(targetId));

            // 세션 정리
            harvestModeSession.ClearTarget();

            // 모드 복귀
            gameModeService.EnterExplorationMode();
        }
    }
}
