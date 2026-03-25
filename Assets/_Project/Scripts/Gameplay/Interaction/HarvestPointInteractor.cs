using Project.Data.Harvest;
using Project.Data.Items;
using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>채집 포인트 감지와 진입 입력을 담당하는 클래스</summary>
    public class HarvestPointInteractor : MonoBehaviour
    {
        [SerializeField] private KeyCode interactKey = KeyCode.E; // 채집 진입 키
        [SerializeField] private float interactRange = 3f; // 상호작용 거리
        [SerializeField] private LayerMask harvestLayerMask; // 채집 대상 레이어

        private HarvestModeCoordinator harvestModeCoordinator; // 채집 모드 조정기
        private IHarvestTarget currentTarget; // 현재 상호작용 대상
        private HarvestTargetHighlightController currentHighlight; // 현재 하이라이트 대상

        public KeyCode InteractKey => interactKey;
        public IHarvestTarget CurrentTarget => currentTarget;
        public float InteractRange => interactRange;

        /// <summary>채집 모드 조정기를 주입한다</summary>
        public void Initialize(HarvestModeCoordinator newHarvestModeCoordinator)
        {
            // 조정기 저장
            harvestModeCoordinator = newHarvestModeCoordinator;
        }

        /// <summary>상호작용 대상 탐색과 입력 처리를 수행한다</summary>
        private void Update()
        {
            // 정면 대상 스캔
            ScanTarget();

            // 조정기 없으면 중단
            if (harvestModeCoordinator == null)
                return;

            // 유효한 대상 없으면 중단
            if (!HasAvailableTarget())
                return;

            // 입력 없으면 중단
            if (!Input.GetKeyDown(interactKey))
                return;

            // 채집 모드 진입 시도
            harvestModeCoordinator.TryEnterHarvestMode(currentTarget);
        }

        /// <summary>현재 유효한 채집 대상 존재 여부를 반환한다</summary>
        public bool HasAvailableTarget()
        {
            // 현재 대상 없으면 실패
            if (currentTarget == null)
                return false;

            // 이미 사용 불가면 실패
            if (!currentTarget.IsAvailable)
                return false;

            // 데이터 없으면 실패
            if (currentTarget.TargetData == null)
                return false;

            // 데이터 유효성 실패면 실패
            if (!currentTarget.TargetData.IsValid())
                return false;

            return true;
        }

        /// <summary>현재 대상의 표시 이름을 반환한다</summary>
        public string GetCurrentTargetDisplayName()
        {
            // 대상 없으면 빈 문자열
            if (!HasAvailableTarget())
                return string.Empty;

            HarvestTargetSO targetData = currentTarget.TargetData; // 대상 데이터 참조
            ItemSO itemData = targetData.ItemData; // 아이템 데이터 참조
            if (itemData == null)
                return string.Empty;

            // 표시 이름 우선 반환
            if (!string.IsNullOrWhiteSpace(itemData.DisplayName))
                return itemData.DisplayName;

            return itemData.ItemId;
        }

        /// <summary>정면 채집 대상을 탐색한다</summary>
        private void ScanTarget()
        {
            // 이전 하이라이트 해제
            ClearCurrentHighlight();

            // 기본값 초기화
            currentTarget = null;

            // 전방 레이캐스트 수행
            bool hasHit = Physics.Raycast(
                transform.position,
                transform.forward,
                out RaycastHit hit,
                interactRange,
                harvestLayerMask);

            // 타격 없으면 종료
            if (!hasHit)
                return;

            // 부모 포함 인터페이스 탐색
            IHarvestTarget foundTarget = hit.collider.GetComponentInParent<IHarvestTarget>();
            if (foundTarget == null)
                return;

            // 현재 대상 저장
            currentTarget = foundTarget;

            // 하이라이트 적용
            ApplyCurrentHighlight(hit.collider);
        }

        /// <summary>현재 대상 하이라이트를 적용한다</summary>
        private void ApplyCurrentHighlight(Collider hitCollider)
        {
            // 콜라이더 없으면 중단
            if (hitCollider == null)
                return;

            // 하이라이트 탐색
            currentHighlight = hitCollider.GetComponentInParent<HarvestTargetHighlightController>();
            if (currentHighlight == null)
                return;

            // 하이라이트 활성화
            currentHighlight.SetHighlight(true);
        }

        /// <summary>현재 대상 하이라이트를 해제한다</summary>
        private void ClearCurrentHighlight()
        {
            // 하이라이트 없으면 중단
            if (currentHighlight == null)
                return;

            // 하이라이트 해제
            currentHighlight.SetHighlight(false);
            currentHighlight = null;
        }
    }
}
