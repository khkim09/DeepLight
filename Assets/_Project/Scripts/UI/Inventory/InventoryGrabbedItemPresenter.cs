using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Data.Input;
using Project.Data.Items;
using Project.Gameplay.Inventory;
using Project.Gameplay.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Inventory
{
    /// <summary>grab/release/rotate/discard와 fresh recovery grab, regrab 흐름을 총괄한다.</summary>
    public class InventoryGrabbedItemPresenter : MonoBehaviour
    {
        private enum GrabSourceType
        {
            None,
            FreshRecovery,
            RegrabFromInventory
        }

        private enum PlacementTriggerMode
        {
            None,
            ToggleClick,
            HoldRelease
        }

        [Header("Data")]
        [SerializeField] private GameInputBindingsSO inputBindings; // 전역 입력 바인딩

        [Header("References")]
        [SerializeField] private Canvas rootCanvas; // 최상위 UI 캔버스
        [SerializeField] private RectTransform previewRect; // footprint 루트
        [SerializeField] private RectTransform previewVisualRect; // 실제 회전된 스프라이트를 그리는 비주얼 자식
        [SerializeField] private Image previewImage; // 비주얼 자식의 Image
        [SerializeField] private InventoryGridPresenter gridPresenter; // 인벤토리 그리드 프리젠터

        private InventoryService inventoryService; // 인벤토리 런타임 서비스
        private InventoryItemTooltipPresenter tooltipPresenter; // 아이템 툴팁 프리젠터

        private ItemSO currentItem; // 현재 grab 중인 아이템
        private InventoryItemInstance grabbedFromPlacedInstance; // 배치 상태에서 뽑아온 인스턴스
        private bool isGrabbing; // grab 상태 여부
        private int rotationQuarterTurns; // 4방향 회전 상태
        private GrabSourceType grabSourceType = GrabSourceType.None; // grab 출처
        private PlacementTriggerMode placementTriggerMode = PlacementTriggerMode.None; // 배치 입력 방식

        private CancellationTokenSource shakeCts; // shake 태스크 취소 토큰
        private Vector2 currentShakeOffset; // shake 오프셋

        /// <summary>현재 어떤 종류든 아이템을 grab 중인지 여부를 반환한다.</summary>
        public bool IsAnyGrabActive => isGrabbing;

        /// <summary>서비스 초기화와 툴팁 참조 연결을 수행한다.</summary>
        public void Initialize(InventoryService newInventoryService)
        {
            inventoryService = newInventoryService;
            tooltipPresenter = gridPresenter != null ? gridPresenter.GetTooltipPresenter() : null;

            if (previewImage == null && previewRect != null)
                previewImage = previewRect.GetComponentInChildren<Image>(true);

            if (previewVisualRect == null && previewImage != null)
                previewVisualRect = previewImage.rectTransform;
        }

        /// <summary>시작 시 모든 grab 관련 UI를 비활성화한다.</summary>
        private void Awake()
        {
            if (previewImage == null && previewRect != null)
                previewImage = previewRect.GetComponentInChildren<Image>(true);

            if (previewVisualRect == null && previewImage != null)
                previewVisualRect = previewImage.rectTransform;

            EndGrabVisualOnly();
        }

        /// <summary>현재 grab 상태일 때 입력과 배치 흐름을 처리한다.</summary>
        private void Update()
        {
            if (!isGrabbing || currentItem == null || inventoryService == null || gridPresenter == null)
                return;

            UpdateGrabTooltipVisibility();
            UpdatePreviewPosition();

            if (Input.GetMouseButtonDown(1) && currentItem.CanRotate)
                RotateGrabbedItemClockwise();

            if (inputBindings != null && Input.GetKeyDown(inputBindings.InventoryDiscardKey))
            {
                DiscardCurrentGrab();
                return;
            }

            bool isHoveringGrid = gridPresenter.TryGetGridCellIndex(Input.mousePosition, out Vector2Int hoveredCell);
            bool isValidPlacement = false;
            Vector2Int resolvedOrigin = Vector2Int.zero;

            if (isHoveringGrid)
            {
                resolvedOrigin = ResolvePlacementOriginFromHoveredCell(hoveredCell);
                isValidPlacement = inventoryService.SubmarineRuntimeState.InventoryGrid.CanPlaceItem(
                    currentItem,
                    resolvedOrigin,
                    rotationQuarterTurns);

                gridPresenter.HighlightCells(currentItem, resolvedOrigin, rotationQuarterTurns, isValidPlacement);
            }
            else
            {
                gridPresenter.ClearHighlight();
            }

            if (placementTriggerMode == PlacementTriggerMode.HoldRelease)
            {
                if (!Input.GetMouseButton(0))
                {
                    TryReleaseGrab(isHoveringGrid, resolvedOrigin);

                    if (isGrabbing)
                        placementTriggerMode = PlacementTriggerMode.ToggleClick;
                }

                return;
            }

            if (placementTriggerMode == PlacementTriggerMode.ToggleClick)
            {
                if (Input.GetMouseButtonDown(0))
                    TryReleaseGrab(isHoveringGrid, resolvedOrigin);
            }
        }

        /// <summary>채집 성공 직후 fresh recovery grab 상태를 시작한다.</summary>
        public void StartRecoveredGrab(ItemSO itemData, bool isNewlyDiscovered)
        {
            if (itemData == null)
                return;

            currentItem = itemData;
            grabbedFromPlacedInstance = null;
            isGrabbing = true;
            rotationQuarterTurns = 0;
            grabSourceType = GrabSourceType.FreshRecovery;
            placementTriggerMode = PlacementTriggerMode.ToggleClick;
            currentShakeOffset = Vector2.zero;

            previewImage.sprite = itemData.Icon;
            previewImage.color = Color.white;
            previewImage.enabled = true;

            if (tooltipPresenter != null)
                tooltipPresenter.Hide();

            ApplyPreviewVisual();
            UpdatePreviewPosition();
        }

        /// <summary>배치된 아이템을 클릭 토글 방식으로 다시 grab한다.</summary>
        public void BeginToggleGrabFromPlacedItem(InventoryPlacedItemView placedItemView)
        {
            if (placedItemView == null || placedItemView.ItemInstance == null || placedItemView.ItemInstance.ItemData == null)
                return;

            if (isGrabbing)
                return;

            StartRegrabInternal(placedItemView, PlacementTriggerMode.ToggleClick);
        }

        /// <summary>배치된 아이템을 드래그 홀드 방식으로 다시 grab한다.</summary>
        public void BeginHoldGrabFromPlacedItem(InventoryPlacedItemView placedItemView)
        {
            if (placedItemView == null || placedItemView.ItemInstance == null || placedItemView.ItemInstance.ItemData == null)
                return;

            if (isGrabbing)
                return;

            StartRegrabInternal(placedItemView, PlacementTriggerMode.HoldRelease);
        }

        /// <summary>재그랩 공통 시작 로직을 수행한다.</summary>
        private void StartRegrabInternal(InventoryPlacedItemView placedItemView, PlacementTriggerMode triggerMode)
        {
            InventoryItemInstance sourceInstance = placedItemView.ItemInstance;

            if (!inventoryService.TryRemoveItem(sourceInstance))
                return;

            gridPresenter.RemovePlacedItemView(sourceInstance);
            gridPresenter.RefreshOccupiedState(inventoryService.SubmarineRuntimeState.InventoryGrid);

            currentItem = sourceInstance.ItemData;
            grabbedFromPlacedInstance = sourceInstance;
            isGrabbing = true;
            rotationQuarterTurns = sourceInstance.RotationQuarterTurns;
            grabSourceType = GrabSourceType.RegrabFromInventory;
            placementTriggerMode = triggerMode;
            currentShakeOffset = Vector2.zero;

            previewImage.sprite = currentItem.Icon;
            previewImage.color = Color.white;
            previewImage.enabled = true;

            if (tooltipPresenter != null)
                tooltipPresenter.ShowItem(currentItem);

            ApplyPreviewVisual();
            UpdatePreviewPosition();
        }

        /// <summary>현재 grab 상태의 release를 시도한다.</summary>
        private void TryReleaseGrab(bool isHoveringGrid, Vector2Int resolvedOrigin)
        {
            if (!isHoveringGrid)
                return;

            if (!inventoryService.TryAddItem(currentItem, resolvedOrigin, rotationQuarterTurns, out InventoryItemInstance placedInstance))
            {
                PlayShakeFeedback().Forget();
                return;
            }

            gridPresenter.CreatePlacedItemView(placedInstance, this);
            gridPresenter.RefreshOccupiedState(inventoryService.SubmarineRuntimeState.InventoryGrid);
            gridPresenter.ClearHighlight();

            EventBus.Publish(new InventoryItemPlacementConfirmedEvent(
                currentItem.ItemId,
                grabSourceType == GrabSourceType.FreshRecovery));

            EndGrabVisualOnly();
        }

        /// <summary>현재 grab 중인 아이템을 폐기한다.</summary>
        private void DiscardCurrentGrab()
        {
            if (!isGrabbing || currentItem == null)
                return;

            EventBus.Publish(new InventoryItemDiscardedEvent(
                currentItem.ItemId,
                grabSourceType == GrabSourceType.FreshRecovery));

            gridPresenter.ClearHighlight();
            EndGrabVisualOnly();
        }

        /// <summary>현재 grab 상태에 따라 툴팁 표시를 갱신한다.</summary>
        private void UpdateGrabTooltipVisibility()
        {
            if (tooltipPresenter == null)
                return;

            if (!isGrabbing || currentItem == null)
            {
                tooltipPresenter.Hide();
                return;
            }

            if (grabSourceType == GrabSourceType.RegrabFromInventory)
                tooltipPresenter.ShowItem(currentItem);
            else
                tooltipPresenter.Hide();
        }

        /// <summary>현재 아이템을 90도씩 시계 방향으로 회전한다.</summary>
        private void RotateGrabbedItemClockwise()
        {
            rotationQuarterTurns = InventoryRotationUtility.NormalizeQuarterTurns(rotationQuarterTurns + 1);
            ApplyPreviewVisual();

            Debug.Log($"[GrabRotate] quarterTurns={rotationQuarterTurns}, size={previewRect.sizeDelta}, euler={previewVisualRect.localEulerAngles}");
        }

        /// <summary>preview 루트 크기와 비주얼 자식 회전을 현재 상태에 맞게 적용한다.</summary>
        private void ApplyPreviewVisual()
        {
            if (previewRect == null || previewVisualRect == null || previewImage == null || currentItem == null || gridPresenter == null)
                return;

            Vector2 footprintSize = gridPresenter.GetItemPixelSize(currentItem, rotationQuarterTurns);
            previewRect.sizeDelta = footprintSize;
            previewRect.localEulerAngles = Vector3.zero;
            previewRect.gameObject.SetActive(true);

            Vector2 unrotatedVisualSize = gridPresenter.GetItemPixelSize(currentItem, 0);
            previewVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewVisualRect.pivot = new Vector2(0.5f, 0.5f);
            previewVisualRect.anchoredPosition = Vector2.zero;
            previewVisualRect.sizeDelta = unrotatedVisualSize;
            previewVisualRect.localEulerAngles = new Vector3(0f, 0f, InventoryRotationUtility.GetRotationZ(rotationQuarterTurns));
            previewVisualRect.localScale = Vector3.one;
            previewVisualRect.SetAsLastSibling();
        }

        /// <summary>프리뷰 rect를 현재 마우스 중심 위치로 갱신한다.</summary>
        private void UpdatePreviewPosition()
        {
            if (rootCanvas == null || previewRect == null)
                return;

            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                null,
                out Vector2 localMousePosition);

            previewRect.anchoredPosition = localMousePosition + currentShakeOffset;
        }

        /// <summary>현재 hover 셀을 기준으로 배치 origin을 역산한다.</summary>
        private Vector2Int ResolvePlacementOriginFromHoveredCell(Vector2Int hoveredCell)
        {
            if (currentItem == null)
                return hoveredCell;

            Vector2Int bounds = InventoryRotationUtility.GetRotatedBounds(currentItem.ShapeBounds, rotationQuarterTurns);

            int pivotOffsetX = Mathf.FloorToInt(bounds.x * 0.5f);
            int pivotOffsetY = Mathf.FloorToInt(bounds.y * 0.5f);

            return new Vector2Int(
                hoveredCell.x - pivotOffsetX,
                hoveredCell.y - pivotOffsetY);
        }

        /// <summary>배치 실패 시 아이템 프리뷰를 좌우로 짧게 흔든다.</summary>
        public async UniTask PlayShakeFeedback()
        {
            if (shakeCts != null)
            {
                shakeCts.Cancel();
                shakeCts.Dispose();
            }

            shakeCts = new CancellationTokenSource();
            CancellationToken token = shakeCts.Token;

            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Sin(elapsed * 50f) * 10f;
                currentShakeOffset = new Vector2(x, 0f);

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            currentShakeOffset = Vector2.zero;
        }

        /// <summary>grab 관련 시각 상태를 종료하고 내부 상태를 초기화한다.</summary>
        private void EndGrabVisualOnly()
        {
            isGrabbing = false;
            currentItem = null;
            grabbedFromPlacedInstance = null;
            rotationQuarterTurns = 0;
            grabSourceType = GrabSourceType.None;
            placementTriggerMode = PlacementTriggerMode.None;
            currentShakeOffset = Vector2.zero;

            if (previewRect != null)
            {
                previewRect.anchoredPosition = Vector2.zero;
                previewRect.localEulerAngles = Vector3.zero;
                previewRect.gameObject.SetActive(false);
            }

            if (previewVisualRect != null)
            {
                previewVisualRect.anchoredPosition = Vector2.zero;
                previewVisualRect.localEulerAngles = Vector3.zero;
            }

            if (tooltipPresenter != null)
                tooltipPresenter.Hide();
        }
    }
}
