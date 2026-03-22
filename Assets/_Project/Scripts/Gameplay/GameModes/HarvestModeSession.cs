using Project.Gameplay.Harvest;

namespace Project.Gameplay.GameModes
{
    /// <summary>현재 채집 세션 대상과 진입 상태를 담당하는 클래스</summary>
    public class HarvestModeSession
    {
        private IHarvestTarget currentTarget; // 현재 채집 대상

        public IHarvestTarget CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null;

        /// <summary>채집 대상 설정</summary>
        public void SetTarget(IHarvestTarget target)
        {
            currentTarget = target; // 현재 대상 갱신
        }

        /// <summary>채집 대상 초기화</summary>
        public void ClearTarget()
        {
            currentTarget = null; // 대상 제거
        }
    }
}
