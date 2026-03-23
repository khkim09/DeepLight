using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>채집 모드에서 채집 시도 입력을 담당하는 클래스</summary>
    public class HarvestAttemptInputController : MonoBehaviour
    {
        [SerializeField] private KeyCode attemptKey = KeyCode.Space; // 채집 시도 키

        private HarvestModeSession harvestModeSession; // 현재 채집 세션
        private HarvestResolver harvestResolver; // 채집 해석기

        /// <summary>채집 입력 의존성을 주입</summary>
        public void Initialize(HarvestModeSession newHarvestModeSession, HarvestResolver newHarvestResolver)
        {
            harvestModeSession = newHarvestModeSession; // 세션 저장
            harvestResolver = newHarvestResolver; // 해석기 저장
        }

        /// <summary>채집 시도 입력을 처리</summary>
        private void Update()
        {
            if (harvestModeSession == null) return;
            if (harvestResolver == null) return;
            if (!harvestModeSession.HasTarget) return;
            if (!Input.GetKeyDown(attemptKey)) return;

            IHarvestTarget currentTarget = harvestModeSession.CurrentTarget;

            // 채집 시도
            harvestResolver.Resolve(currentTarget);
        }
    }
}
