using Project.Data.Submarine;
using System.Collections.Generic;
using UnityEngine;

namespace Project.UI.Inventory
{
    /// <summary>SubmarineLayoutSO 데이터를 기반으로 런타임에 슬롯 UI를 동적 생성한다.</summary>
    public class InventoryGridBuilder : MonoBehaviour
    {
        [Header("UI Prefabs")]
        [SerializeField] private InventorySlotUI slotPrefab; // 슬롯 프리팹

        private SubmarineInventoryLayoutSO layoutSO;
        private readonly List<InventorySlotUI> activeSlots = new();

        /// <summary>Bootstrapper에서 SO를 주입받아 그리드 생성을 시작한다.</summary>
        public void Initialize(SubmarineInventoryLayoutSO layout)
        {
            layoutSO = layout;
            GenerateGrid();
        }

        /// <summary>슬롯 UI를 생성한다.</summary>
        private void GenerateGrid()
        {
            ClearExistingSlots();

            if (layoutSO == null || !layoutSO.IsValid() || slotPrefab == null)
                return;

            float totalWidth = (layoutSO.GridSize.x * layoutSO.CellSize.x) + ((layoutSO.GridSize.x - 1) * layoutSO.Spacing.x);
            float totalHeight = (layoutSO.GridSize.y * layoutSO.CellSize.y) + ((layoutSO.GridSize.y - 1) * layoutSO.Spacing.y);

            float startX = -totalWidth * 0.5f + (layoutSO.CellSize.x * 0.5f);
            float startY = totalHeight * 0.5f - (layoutSO.CellSize.y * 0.5f);

            foreach (Vector2Int cell in layoutSO.UsableCells)
            {
                CreateSlot(cell.x, cell.y, startX, startY);
            }
        }

        /// <summary>지정 좌표에 슬롯 하나를 생성한다.</summary>
        private void CreateSlot(int x, int y, float startX, float startY)
        {
            InventorySlotUI newSlot = Instantiate(slotPrefab, transform);
            newSlot.gameObject.name = $"Slot_{x}_{y}";

            RectTransform slotRect = newSlot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = layoutSO.CellSize;

            float posX = startX + (x * (layoutSO.CellSize.x + layoutSO.Spacing.x));
            float posY = startY - (y * (layoutSO.CellSize.y + layoutSO.Spacing.y));
            slotRect.anchoredPosition = new Vector2(posX, posY);

            newSlot.Initialize(x, y);
            activeSlots.Add(newSlot);
        }

        /// <summary>기존에 생성된 슬롯 UI만 정리한다.</summary>
        private void ClearExistingSlots()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);

                // 슬롯 프리팹만 제거하고 PlacedItemRoot 같은 보조 루트는 유지한다.
                if (child.GetComponent<InventorySlotUI>() == null)
                    continue;

                Destroy(child.gameObject);
            }

            activeSlots.Clear();
        }
    }
}
