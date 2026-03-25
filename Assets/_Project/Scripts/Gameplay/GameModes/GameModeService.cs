using Project.Core.Events;

namespace Project.Gameplay.GameModes
{
    /// <summary>게임 모드 전환과 현재 모드 상태를 관리하는 클래스</summary>
    public class GameModeService
    {
        private GameModeType currentMode; // 현재 게임 모드

        public GameModeType CurrentMode => currentMode;

        /// <summary>게임 모드 서비스 생성</summary>
        public GameModeService(GameModeType initialMode)
        {
            currentMode = initialMode; // 초기 모드 저장
        }

        /// <summary>현재 탐사 모드인지 확인한다</summary>
        public bool IsExplorationMode()
        {
            return currentMode == GameModeType.Exploration3D;
        }

        /// <summary>현재 회수 콘솔 모드인지 확인한다</summary>
        public bool IsHarvestMode()
        {
            return currentMode == GameModeType.HarvestConsole;
        }

        /// <summary>지정 모드로 변경한다</summary>
        public void ChangeMode(GameModeType nextMode)
        {
            // 같은 모드면 중단
            if (currentMode == nextMode)
                return;

            GameModeType previousMode = currentMode; // 이전 모드 저장
            currentMode = nextMode; // 현재 모드 갱신

            // 공통 모드 변경 이벤트 발행
            EventBus.Publish(new GameModeChangedEvent(previousMode, currentMode));

            // 회수 콘솔 진입 이벤트 발행
            if (currentMode == GameModeType.HarvestConsole)
                EventBus.Publish(new HarvestModeEnteredEvent());

            // 회수 콘솔 종료 이벤트 발행
            if (previousMode == GameModeType.HarvestConsole && currentMode != GameModeType.HarvestConsole)
                EventBus.Publish(new HarvestModeExitedEvent());
        }

        /// <summary>탐사 모드로 전환한다</summary>
        public void EnterExplorationMode()
        {
            ChangeMode(GameModeType.Exploration3D);
        }

        /// <summary>회수 콘솔 모드로 전환한다</summary>
        public void EnterHarvestMode()
        {
            ChangeMode(GameModeType.HarvestConsole);
        }
    }
}
