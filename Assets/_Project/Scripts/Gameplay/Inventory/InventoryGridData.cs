using System;
using System.Collections.Generic;
using Project.Data.Items;
using Project.Data.Submarine;
using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>인벤토리의 논리적 배치와 칸 점유 상태를 관리하는 데이터 클래스이다.</summary>
    [Serializable]
    public class InventoryGridData
    {
        private int width; // 가로 크기
        private int height; // 세로 크기

        private readonly List<InventoryItemInstance> items = new(); // 배치된 아이템 목록
        private InventoryItemInstance[,] occupiedGrid; // 칸당 점유 아이템 정보
        private bool[,] usableMask; // 사용 가능 칸 마스크

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<InventoryItemInstance> Items => items;

        /// <summary>그리드 데이터를 초기화하고 레이아웃 마스크를 생성한다.</summary>
        public InventoryGridData(SubmarineInventoryLayoutSO layout)
        {
            Initialize(layout);
        }

        /// <summary>외부 런타임 상태에서 그리드를 재초기화한다.</summary>
        public void Initialize(SubmarineInventoryLayoutSO layout)
        {
            if (layout == null || !layout.IsValid())
                return;

            width = layout.GridSize.x;
            height = layout.GridSize.y;

            items.Clear();
            occupiedGrid = new InventoryItemInstance[width, height];
            usableMask = layout.BuildUsableMask();
        }

        /// <summary>지정 위치와 회전 상태에서 아이템 배치가 가능한지 검사한다.</summary>
        public bool CanPlaceItem(ItemSO item, Vector2Int origin, int rotationQuarterTurns)
        {
            if (item == null)
                return false;

            List<Vector2Int> targetCells = BuildPlacedCells(item, origin, rotationQuarterTurns);

            foreach (Vector2Int cell in targetCells)
            {
                // 그리드 범위 검사
                if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
                    return false;

                // 사용 가능한 칸인지 검사
                if (!usableMask[cell.x, cell.y])
                    return false;

                // 이미 점유된 칸인지 검사
                if (occupiedGrid[cell.x, cell.y] != null)
                    return false;
            }

            return true;
        }

        /// <summary>아이템을 특정 위치에 배치하고 점유 정보를 갱신한다.</summary>
        public bool TryPlaceItem(ItemSO itemData, Vector2Int origin, int rotationQuarterTurns, out InventoryItemInstance instance)
        {
            instance = null;

            if (!CanPlaceItem(itemData, origin, rotationQuarterTurns))
                return false;

            instance = new InventoryItemInstance(itemData, origin, rotationQuarterTurns);
            items.Add(instance);

            List<Vector2Int> cells = BuildPlacedCells(itemData, origin, rotationQuarterTurns);
            foreach (Vector2Int cell in cells)
            {
                occupiedGrid[cell.x, cell.y] = instance;
            }

            return true;
        }

        /// <summary>인벤토리에서 아이템을 제거하고 점유 상태를 해제한다.</summary>
        public bool RemoveItem(InventoryItemInstance itemInstance)
        {
            if (itemInstance == null || !items.Contains(itemInstance))
                return false;

            items.Remove(itemInstance);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (occupiedGrid[x, y] == itemInstance)
                        occupiedGrid[x, y] = null;
                }
            }

            return true;
        }

        /// <summary>아이템 점유 셀 목록을 회전과 위치를 고려하여 생성한다.</summary>
        private List<Vector2Int> BuildPlacedCells(ItemSO itemData, Vector2Int origin, int rotationQuarterTurns)
        {
            List<Vector2Int> cells = new();

            foreach (Vector2Int localCell in itemData.OccupiedCells)
            {
                Vector2Int rotatedLocal = InventoryRotationUtility.RotateLocalCell(
                    localCell,
                    itemData.ShapeBounds,
                    rotationQuarterTurns);

                cells.Add(origin + rotatedLocal);
            }

            return cells;
        }
    }
}
