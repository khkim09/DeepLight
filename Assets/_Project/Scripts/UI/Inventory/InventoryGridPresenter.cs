using System.Collections.Generic;
using Project.Data.Items;
using Project.Data.Submarine;
using Project.Gameplay.Inventory;
using Project.Gameplay.Services;
using UnityEngine;

namespace Project.UI.Inventory
{
    /// <summary>인벤토리 그리드 좌표 계산, 슬롯 프리뷰, 배치 아이템 뷰 생성을 담당한다.</summary>
    public class InventoryGridPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform gridContainer; // 전체 그리드 기준 Rect
        [SerializeField] private RectTransform placedItemRoot; // 배치 아이템 뷰 루트
        [SerializeField] private InventoryPlacedItemView placedItemViewPrefab; // 배치 아이템 뷰 프리팹
        [SerializeField] private InventoryItemTooltipPresenter tooltipPresenter; // 툴팁 프리젠터

        private SubmarineInventoryLayoutSO layoutSO; // 현재 인벤토리 레이아웃
        private readonly Dictionary<Vector2Int, InventorySlotUI> slotMap = new(); // 셀 좌표 -> 슬롯 UI
        private readonly Dictionary<InventoryItemInstance, InventoryPlacedItemView> placedItemViewMap = new(); // 런타임 인스턴스 -> UI 뷰

        /// <summary>Bootstrapper에서 SO를 주입받고 슬롯 캐시를 다시 구축한다.</summary>
        public void Initialize(
            SubmarineInventoryLayoutSO layout,
            EncyclopediaService encyclopediaService,
            GameTimeService gameTimeService)
        {
            layoutSO = layout;
            RebuildSlotCache();
        }

        /// <summary>화면 좌표를 인벤토리 셀 좌표로 변환한다.</summary>
        public bool TryGetGridCellIndex(Vector2 screenPos, out Vector2Int cellIndex)
        {
            cellIndex = Vector2Int.zero;

            if (layoutSO == null || gridContainer == null)
                return false;

            if (!RectTransformUtility.RectangleContainsScreenPoint(gridContainer, screenPos))
                return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainer, screenPos, null, out Vector2 localPos);

            float totalWidth = (layoutSO.GridSize.x * layoutSO.CellSize.x) + ((layoutSO.GridSize.x - 1) * layoutSO.Spacing.x);
            float totalHeight = (layoutSO.GridSize.y * layoutSO.CellSize.y) + ((layoutSO.GridSize.y - 1) * layoutSO.Spacing.y);

            float startX = -totalWidth * 0.5f;
            float startY = totalHeight * 0.5f;

            float relativeX = localPos.x - startX;
            float relativeY = startY - localPos.y;

            int x = Mathf.FloorToInt(relativeX / (layoutSO.CellSize.x + layoutSO.Spacing.x));
            int y = Mathf.FloorToInt(relativeY / (layoutSO.CellSize.y + layoutSO.Spacing.y));

            if (x < 0 || x >= layoutSO.GridSize.x || y < 0 || y >= layoutSO.GridSize.y)
                return false;

            cellIndex = new Vector2Int(x, y);
            return true;
        }

        /// <summary>아이템의 현재 회전 상태에 맞는 점유 footprint 픽셀 크기를 계산한다.</summary>
        public Vector2 GetItemPixelSize(ItemSO itemData, int rotationQuarterTurns)
        {
            if (itemData == null || layoutSO == null)
                return Vector2.zero;

            Vector2Int bounds = InventoryRotationUtility.GetRotatedBounds(itemData.ShapeBounds, rotationQuarterTurns);

            float width = bounds.x * layoutSO.CellSize.x + Mathf.Max(0, bounds.x - 1) * layoutSO.Spacing.x;
            float height = bounds.y * layoutSO.CellSize.y + Mathf.Max(0, bounds.y - 1) * layoutSO.Spacing.y;

            return new Vector2(width, height);
        }

        /// <summary>현재 배치 평가 결과를 슬롯 프리뷰 색상으로 표시한다.</summary>
        public void HighlightPlacement(ItemSO itemData, InventoryPlacementResult placementResult)
        {
            ClearHighlight();

            if (itemData == null)
                return;

            InventorySlotUI.SlotPreviewState previewState = placementResult.PlacementType switch
            {
                InventoryPlacementType.Place => InventorySlotUI.SlotPreviewState.Valid,
                InventoryPlacementType.Swap => InventorySlotUI.SlotPreviewState.Swap,
                _ => InventorySlotUI.SlotPreviewState.Invalid
            };

            foreach (Vector2Int targetCell in BuildTargetCells(itemData, placementResult.Position, placementResult.RotationQuarterTurns))
            {
                if (!slotMap.TryGetValue(targetCell, out InventorySlotUI slotUI) || slotUI == null)
                    continue;

                slotUI.SetPreviewState(previewState);
            }
        }

        /// <summary>모든 슬롯 프리뷰 상태를 기본값으로 되돌린다.</summary>
        public void ClearHighlight()
        {
            foreach (KeyValuePair<Vector2Int, InventorySlotUI> pair in slotMap)
            {
                if (pair.Value == null)
                    continue;

                pair.Value.ResetPreviewState();
            }
        }

        /// <summary>현재 인벤토리 논리 데이터 기준으로 슬롯 점유 상태를 다시 동기화한다.</summary>
        public void RefreshOccupiedState(InventoryGridData gridData)
        {
            foreach (KeyValuePair<Vector2Int, InventorySlotUI> pair in slotMap)
            {
                if (pair.Value != null)
                    pair.Value.SetOccupied(false);
            }

            if (gridData == null)
                return;

            IReadOnlyList<InventoryItemInstance> items = gridData.Items;
            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemInstance itemInstance = items[i];
                if (itemInstance == null || itemInstance.ItemData == null)
                    continue;

                foreach (Vector2Int localCell in itemInstance.ItemData.OccupiedCells)
                {
                    Vector2Int rotatedLocal = itemInstance.GetRotatedCellPosition(localCell);
                    Vector2Int targetCell = itemInstance.OriginPosition + rotatedLocal;

                    if (slotMap.TryGetValue(targetCell, out InventorySlotUI slotUI) && slotUI != null)
                        slotUI.SetOccupied(true);
                }
            }
        }

        /// <summary>배치 아이템 UI를 생성한다.</summary>
        public void CreatePlacedItemView(InventoryItemInstance itemInstance, InventoryGrabbedItemPresenter grabbedItemPresenter)
        {
            if (itemInstance == null || itemInstance.ItemData == null)
                return;

            if (placedItemRoot == null || placedItemViewPrefab == null || layoutSO == null)
                return;

            RemovePlacedItemView(itemInstance);

            InventoryPlacedItemView newView = Instantiate(placedItemViewPrefab, placedItemRoot, false);
            newView.gameObject.name = $"Placed_{itemInstance.ItemData.ItemId}";
            newView.gameObject.SetActive(true);

            RectTransform viewRect = newView.RectTransform;
            viewRect.anchorMin = new Vector2(0.5f, 0.5f);
            viewRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.localScale = Vector3.one;

            Vector2 footprintSize = GetItemPixelSize(itemInstance.ItemData, itemInstance.RotationQuarterTurns);
            viewRect.sizeDelta = footprintSize;
            viewRect.localEulerAngles = Vector3.zero;

            Vector2 anchoredPosition = GetItemBoundingCenter(itemInstance.OriginPosition, itemInstance.GetBounds());
            viewRect.anchoredPosition = anchoredPosition;

            newView.Initialize(itemInstance, grabbedItemPresenter, tooltipPresenter);

            Vector2 unrotatedVisualSize = GetItemPixelSize(itemInstance.ItemData, 0);
            newView.ApplyVisual(unrotatedVisualSize, itemInstance.RotationQuarterTurns);

            viewRect.SetAsLastSibling();
            placedItemViewMap[itemInstance] = newView;

            Canvas.ForceUpdateCanvases();
        }

        /// <summary>특정 인벤토리 인스턴스의 배치 UI를 제거한다.</summary>
        public void RemovePlacedItemView(InventoryItemInstance itemInstance)
        {
            if (itemInstance == null)
                return;

            if (!placedItemViewMap.TryGetValue(itemInstance, out InventoryPlacedItemView view))
                return;

            if (view != null)
            {
                if (Application.isPlaying)
                    Destroy(view.gameObject);
                else
                    DestroyImmediate(view.gameObject);
            }

            placedItemViewMap.Remove(itemInstance);
        }

        /// <summary>현재 생성된 모든 배치 아이템 UI를 제거한다.</summary>
        public void ClearAllPlacedItemViews()
        {
            foreach (KeyValuePair<InventoryItemInstance, InventoryPlacedItemView> pair in placedItemViewMap)
            {
                if (pair.Value == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(pair.Value.gameObject);
                else
                    DestroyImmediate(pair.Value.gameObject);
            }

            placedItemViewMap.Clear();
        }

        /// <summary>툴팁 프리젠터를 반환한다.</summary>
        public InventoryItemTooltipPresenter GetTooltipPresenter()
        {
            return tooltipPresenter;
        }

        /// <summary>슬롯 자식들을 순회하여 좌표 캐시를 다시 생성한다.</summary>
        private void RebuildSlotCache()
        {
            slotMap.Clear();

            if (gridContainer == null)
                return;

            InventorySlotUI[] slots = gridContainer.GetComponentsInChildren<InventorySlotUI>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlotUI slotUI = slots[i];
                if (slotUI == null)
                    continue;

                Vector2Int key = new Vector2Int(slotUI.GridX, slotUI.GridY);
                if (!slotMap.ContainsKey(key))
                    slotMap.Add(key, slotUI);
            }
        }

        /// <summary>현재 origin/rotation 기준 실제 점유 대상 셀 목록을 만든다.</summary>
        private List<Vector2Int> BuildTargetCells(ItemSO itemData, Vector2Int origin, int rotationQuarterTurns)
        {
            List<Vector2Int> result = new();

            if (itemData == null)
                return result;

            foreach (Vector2Int localCell in itemData.OccupiedCells)
            {
                Vector2Int rotatedLocal = InventoryRotationUtility.RotateLocalCell(
                    localCell,
                    itemData.ShapeBounds,
                    rotationQuarterTurns);

                result.Add(origin + rotatedLocal);
            }

            return result;
        }

        /// <summary>원점 셀 기준 footprint 중심 anchoredPosition을 계산한다.</summary>
        private Vector2 GetItemBoundingCenter(Vector2Int origin, Vector2Int bounds)
        {
            Vector2 originCellCenter = GetCellCenter(origin.x, origin.y);

            float stepX = layoutSO.CellSize.x + layoutSO.Spacing.x;
            float stepY = layoutSO.CellSize.y + layoutSO.Spacing.y;

            float offsetX = (bounds.x - 1) * stepX * 0.5f;
            float offsetY = -(bounds.y - 1) * stepY * 0.5f;

            return originCellCenter + new Vector2(offsetX, offsetY);
        }

        /// <summary>특정 셀의 중심 anchoredPosition을 계산한다.</summary>
        private Vector2 GetCellCenter(int x, int y)
        {
            float totalWidth = (layoutSO.GridSize.x * layoutSO.CellSize.x) + ((layoutSO.GridSize.x - 1) * layoutSO.Spacing.x);
            float totalHeight = (layoutSO.GridSize.y * layoutSO.CellSize.y) + ((layoutSO.GridSize.y - 1) * layoutSO.Spacing.y);

            float startX = -totalWidth * 0.5f + (layoutSO.CellSize.x * 0.5f);
            float startY = totalHeight * 0.5f - (layoutSO.CellSize.y * 0.5f);

            float posX = startX + x * (layoutSO.CellSize.x + layoutSO.Spacing.x);
            float posY = startY - y * (layoutSO.CellSize.y + layoutSO.Spacing.y);

            return new Vector2(posX, posY);
        }
    }
}
