using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.Debug
{
    /// <summary>에디터 테스트용으로 채집 진입과 종료를 강제 실행하는 클래스</summary>
    public class TestHarvestInvoker : MonoBehaviour
    {
        [SerializeField] private KeyCode enterHarvestKey = KeyCode.Alpha1; // 테스트용 채집 진입 키
        [SerializeField] private KeyCode exitHarvestKey = KeyCode.Alpha2; // 테스트용 채집 종료 키
        [SerializeField] private HarvestTargetBehaviour testTarget; // 테스트용 채집 대상

        private HarvestModeCoordinator harvestModeCoordinator; // 채집 모드 조정기

        /// <summary>채집 모드 조정기 주입</summary>
        public void Initialize(HarvestModeCoordinator newHarvestModeCoordinator)
        {
            harvestModeCoordinator = newHarvestModeCoordinator; // 조정기 저장
        }

        /// <summary>테스트 입력 처리</summary>
        private void Update()
        {
            if (harvestModeCoordinator == null) return;

            // 1번키로 채집 진입
            if (Input.GetKeyDown(enterHarvestKey) && testTarget != null)
                harvestModeCoordinator.TryEnterHarvestMode(testTarget);

            // 2번키로 채집 종료
            if (Input.GetKeyDown(exitHarvestKey))
                harvestModeCoordinator.ExitHarvestMode();
        }
    }
}
