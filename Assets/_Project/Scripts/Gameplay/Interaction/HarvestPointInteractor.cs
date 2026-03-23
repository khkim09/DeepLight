using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>채집 포인트 감지와 진입 입력 담당</summary>
    public class HarvestPointInteractor : MonoBehaviour
    {
        [SerializeField] private KeyCode interactKey = KeyCode.E; // 채집 진입 키
        [SerializeField] private float interactRange = 3f; // 상호작용 거리
        [SerializeField] private LayerMask harvestLayerMask; // 채집 대상 레이어

        private HarvestModeCoordinator harvestModeCoordinator; // 채집 모드 조정기
        private IHarvestTarget currentTarget; // 현재 상호작용 대상

        /// <summary>채집 모드 조정기 주입</summary>
        public void Initialize(HarvestModeCoordinator newHarvestModeCoordinator)
        {
            harvestModeCoordinator = newHarvestModeCoordinator; // 조정기 저장
        }

        /// <summary>상호작용 대상 탐색과 입력 처리</summary>
        private void Update()
        {
            // 대상 탐색
            ScanTarget();

            // 조정기 없으면 중단
            if (harvestModeCoordinator == null) return;
            if (currentTarget == null) return;
            if (!Input.GetKeyDown(interactKey)) return;

            // 채집 모드 진입 시도
            harvestModeCoordinator.TryEnterHarvestMode(currentTarget);
        }

        /// <summary>전방 채집 대상 탐색</summary>
        private void ScanTarget()
        {
            // 기본값 초기화
            currentTarget = null;

            // 전방 레이캐스트
            bool hasHit = Physics.Raycast(
                transform.position,
                transform.forward,
                out RaycastHit hit,
                interactRange,
                harvestLayerMask);

            // 타격 없으면 종료
            if (!hasHit) return;

            // 인터페이스 탐색
            currentTarget = hit.collider.GetComponentInParent<IHarvestTarget>();
        }
    }
}
