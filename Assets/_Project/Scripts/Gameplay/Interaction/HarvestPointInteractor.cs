using Project.Core.Events;
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
        [SerializeField] private KeyCode interactKey = KeyCode.F; // 채집 진입 키
        [SerializeField] private float interactRange = 3f; // 상호작용 거리
        [SerializeField] private LayerMask harvestLayerMask; // 채집 대상 레이어

        private HarvestModeCoordinator harvestModeCoordinator; // 채집 모드 조정기
        private IHarvestTarget currentTarget; // 현재 상호작용 대상
        private HarvestTargetHighlightController currentHighlight; // 현재 하이라이트 대상
        private bool isHarvestMode; // 현재 채집 모드 여부

        public KeyCode InteractKey => interactKey;
        public IHarvestTarget CurrentTarget => currentTarget;
        public float InteractRange => interactRange;

        /// <summary>채집 모드 조정기를 주입한다</summary>
        public void Initialize(HarvestModeCoordinator newHarvestModeCoordinator)
        {
            harvestModeCoordinator = newHarvestModeCoordinator;
        }

        /// <summary>이벤트 구독을 등록한다</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독을 해제한다</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>상호작용 대상 탐색과 입력 처리를 수행한다</summary>
        private void Update()
        {
            // 채집 모드 중에는 탐사 상호작용 완전 차단
            if (isHarvestMode)
                return;

            ScanTarget();

            if (harvestModeCoordinator == null)
                return;

            if (!HasAvailableTarget())
                return;

            if (!Input.GetKeyDown(interactKey))
                return;

            harvestModeCoordinator.TryEnterHarvestMode(currentTarget);
        }

        /// <summary>채집 모드 진입 시 탐사 상호작용을 잠근다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            ClearCurrentHighlight();
        }

        /// <summary>채집 모드 종료 시 탐사 상호작용을 다시 연다</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
        }

        /// <summary>현재 유효한 채집 대상 존재 여부를 반환한다</summary>
        public bool HasAvailableTarget()
        {
            if (currentTarget == null)
                return false;

            if (!currentTarget.IsAvailable)
                return false;

            if (currentTarget.TargetData == null)
                return false;

            if (!currentTarget.TargetData.IsValid())
                return false;

            return true;
        }

        /// <summary>현재 대상의 표시 이름을 반환한다</summary>
        public string GetCurrentTargetDisplayName()
        {
            if (!HasAvailableTarget())
                return string.Empty;

            HarvestTargetSO targetData = currentTarget.TargetData;
            ItemSO itemData = targetData.ItemData;
            if (itemData == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(itemData.DisplayName))
                return itemData.DisplayName;

            return itemData.ItemId;
        }

        /// <summary>정면 채집 대상을 탐색한다</summary>
        private void ScanTarget()
        {
            ClearCurrentHighlight();
            currentTarget = null;

            bool hasHit = Physics.Raycast(
                transform.position,
                transform.forward,
                out RaycastHit hit,
                interactRange,
                harvestLayerMask);

            if (!hasHit)
                return;

            IHarvestTarget foundTarget = hit.collider.GetComponentInParent<IHarvestTarget>();
            if (foundTarget == null)
                return;

            currentTarget = foundTarget;
            ApplyCurrentHighlight(hit.collider);
        }

        /// <summary>현재 대상 하이라이트를 적용한다</summary>
        private void ApplyCurrentHighlight(Collider hitCollider)
        {
            if (hitCollider == null)
                return;

            currentHighlight = hitCollider.GetComponentInParent<HarvestTargetHighlightController>();
            if (currentHighlight == null)
                return;

            currentHighlight.SetHighlight(true);
        }

        /// <summary>현재 대상 하이라이트를 해제한다</summary>
        private void ClearCurrentHighlight()
        {
            if (currentHighlight == null)
                return;

            currentHighlight.SetHighlight(false);
            currentHighlight = null;
        }
    }
}
