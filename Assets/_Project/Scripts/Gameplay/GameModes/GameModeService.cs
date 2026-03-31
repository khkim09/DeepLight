using Project.Core.Events;

namespace Project.Gameplay.GameModes
{
    /// <summary>게임 모드 전환과 현재 모드 상태를 관리하는 클래스이다.</summary>
    public class GameModeService
    {
        private GameModeType currentMode; // 현재 게임 모드

        /// <summary>현재 모드를 반환한다.</summary>
        public GameModeType CurrentMode => currentMode;

        /// <summary>게임 모드 서비스를 생성한다.</summary>
        public GameModeService(GameModeType initialMode)
        {
            currentMode = initialMode;
        }

        /// <summary>현재 탐사 모드인지 확인한다.</summary>
        public bool IsExplorationMode()
        {
            return currentMode == GameModeType.Exploration3D;
        }

        /// <summary>현재 회수 콘솔 모드인지 확인한다.</summary>
        public bool IsHarvestMode()
        {
            return currentMode == GameModeType.HarvestConsole;
        }

        /// <summary>지정 모드로 변경한다.</summary>
        public void ChangeMode(GameModeType nextMode)
        {
            if (currentMode == nextMode)
                return;

            GameModeType previousMode = currentMode;
            currentMode = nextMode;

            EventBus.Publish(new GameModeChangedEvent(previousMode, currentMode));
        }

        /// <summary>탐사 모드로 전환한다.</summary>
        public void EnterExplorationMode()
        {
            ChangeMode(GameModeType.Exploration3D);
        }

        /// <summary>회수 콘솔 모드로 전환한다.</summary>
        public void EnterHarvestMode()
        {
            ChangeMode(GameModeType.HarvestConsole);
        }
    }
}
