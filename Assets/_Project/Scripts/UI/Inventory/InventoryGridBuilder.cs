using System.Collections.Generic;
using Project.Data.Submarine;
using UnityEngine;

namespace Project.UI.Inventory
{
    /// <summary>SubmarineLayoutSO 데이터를 기반으로 슬롯 UI를 Slots 루트 아래에 생성한다.</summary>
    public class InventoryGridBuilder : MonoBehaviour
    {
        [Header("UI Prefabs")]
        [SerializeField] private InventorySlotUI slotPrefab; // 슬롯 프리팹

        [Header("Runtime Roots")]
        [SerializeField] private RectTransform slotsRoot; // 슬롯 생성 전용 루트

        private SubmarineInventoryLayoutSO layoutSO; // 현재 레이아웃 데이터
        private readonly List<InventorySlotUI> activeSlots = new(); // 생성된 슬롯 캐시

        /// <summary>Bootstrapper에서 SO를 주입받아 슬롯 생성을 시작한다.</summary>
        public void Initialize(SubmarineInventoryLayoutSO layout)
        {
            layoutSO = layout;
            GenerateGrid();
        }

        /// <summary>슬롯 UI 전체를 다시 생성한다.</summary>
        private void GenerateGrid()
        {
            ClearExistingSlots();

            if (layoutSO == null || !layoutSO.IsValid() || slotPrefab == null)
                return;

            RectTransform targetRoot = slotsRoot != null ? slotsRoot : transform as RectTransform;
            if (targetRoot == null)
                return;

            float totalWidth = (layoutSO.GridSize.x * layoutSO.CellSize.x) + ((layoutSO.GridSize.x - 1) * layoutSO.Spacing.x);
            float totalHeight = (layoutSO.GridSize.y * layoutSO.CellSize.y) + ((layoutSO.GridSize.y - 1) * layoutSO.Spacing.y);

            float startX = -totalWidth * 0.5f + (layoutSO.CellSize.x * 0.5f);
            float startY = totalHeight * 0.5f - (layoutSO.CellSize.y * 0.5f);

            foreach (Vector2Int cell in layoutSO.UsableCells)
                CreateSlot(targetRoot, cell.x, cell.y, startX, startY);
        }

        /// <summary>지정 좌표에 슬롯 하나를 생성한다.</summary>
        private void CreateSlot(RectTransform parentRoot, int x, int y, float startX, float startY)
        {
            InventorySlotUI newSlot = Instantiate(slotPrefab, parentRoot);
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

        /// <summary>기존 슬롯 UI를 Slots 루트 아래에서만 제거한다.</summary>
        private void ClearExistingSlots()
        {
            RectTransform targetRoot = slotsRoot != null ? slotsRoot : transform as RectTransform;
            if (targetRoot == null)
                return;

            for (int i = targetRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = targetRoot.GetChild(i);

                if (child.GetComponent<InventorySlotUI>() == null)
                    continue;

                child.SetParent(null, false);

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            activeSlots.Clear();
        }
    }
}
