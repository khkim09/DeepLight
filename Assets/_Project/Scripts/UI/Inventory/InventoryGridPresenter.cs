using System.Collections.Generic;
using Project.Data.Items;
using Project.Data.Submarine;
using Project.Gameplay.Inventory;
using Project.Gameplay.Services;
using UnityEngine;

namespace Project.UI.Inventory
{
    /// <summary>인벤토리 그리드 좌표 계산, 슬롯 피드백, 배치 아이템 뷰 생성을 담당한다.</summary>
    public class InventoryGridPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform gridContainer; // 슬롯이 생성되는 실제 그리드 루트
        [SerializeField] private RectTransform placedItemRoot; // 배치 아이템 뷰 루트
        [SerializeField] private InventoryPlacedItemView placedItemViewPrefab; // 배치 아이템 뷰 프리팹
        [SerializeField] private InventoryItemTooltipPresenter tooltipPresenter; // 툴팁 프리젠터

        [Header("Feedback Colors")]
        [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.6f);
        [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.6f);

        private SubmarineInventoryLayoutSO layoutSO;
        private readonly Dictionary<Vector2Int, InventorySlotUI> slotMap = new();
        private readonly Dictionary<InventoryItemInstance, InventoryPlacedItemView> placedItemViewMap = new();

        /// <summary>Bootstrapper에서 SO를 주입받고 슬롯 캐시를 구축한다.</summary>
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

        /// <summary>현재 배치 예정 셀에 초록/빨강 하이라이트를 적용한다.</summary>
        public void HighlightCells(ItemSO itemData, Vector2Int origin, int rotationQuarterTurns, bool isValid)
        {
            ClearHighlight();

            if (itemData == null)
                return;

            Color targetColor = isValid ? validColor : invalidColor;

            foreach (Vector2Int localCell in itemData.OccupiedCells)
            {
                Vector2Int rotatedLocal = InventoryRotationUtility.RotateLocalCell(
                    localCell,
                    itemData.ShapeBounds,
                    rotationQuarterTurns);

                Vector2Int targetPos = origin + rotatedLocal;

                if (!slotMap.TryGetValue(targetPos, out InventorySlotUI slotUI))
                    continue;

                if (!slotUI.IsOccupied)
                    slotUI.SetHighlightColor(targetColor);
            }
        }

        /// <summary>모든 슬롯 하이라이트를 초기화한다.</summary>
        public void ClearHighlight()
        {
            foreach (KeyValuePair<Vector2Int, InventorySlotUI> pair in slotMap)
            {
                if (pair.Value != null)
                    pair.Value.ApplyBaseColor();
            }
        }

        /// <summary>현재 인벤토리 논리 데이터 기준으로 슬롯 점유 시각을 다시 동기화한다.</summary>
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

            placedItemRoot.SetAsLastSibling();

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
                Destroy(view.gameObject);

            placedItemViewMap.Remove(itemInstance);
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

            for (int i = 0; i < gridContainer.childCount; i++)
            {
                InventorySlotUI slotUI = gridContainer.GetChild(i).GetComponent<InventorySlotUI>();
                if (slotUI == null)
                    continue;

                Vector2Int key = new Vector2Int(slotUI.GridX, slotUI.GridY);
                if (!slotMap.ContainsKey(key))
                    slotMap.Add(key, slotUI);
            }
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
