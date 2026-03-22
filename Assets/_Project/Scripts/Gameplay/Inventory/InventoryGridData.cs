using System;
using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.Items;
using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>실제 점유/배치/회전 검사 담당</summary>
    [Serializable]
    public class InventoryGridData
    {
        [SerializeField] private int width; // 인벤토리 가로 칸 수
        [SerializeField] private int height; // 인벤토리 세로 칸 수

        private readonly List<InventoryItemInstance> items = new(); // 배치된 아이템 목록
        private InventoryItemInstance[,] occupiedGrid; // 칸 점유 정보

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<InventoryItemInstance> Items => items;

        /// <summary>인벤토리 그리드 생성</summary>
        public InventoryGridData(int width, int height)
        {
            this.width = width;
            this.height = height;
            occupiedGrid = new InventoryItemInstance[width, height];
        }

        /// <summary>그리드 크기 기준으로 초기화</summary>
        public void Initialize(int newWidth, int newHeight)
        {
            width = newWidth;
            height = newHeight;
            items.Clear();
            occupiedGrid = new InventoryItemInstance[width, height];
        }

        /// <summary>좌표가 인벤토리 범위 내인지 확인</summary>
        public bool IsInBounds(Vector2Int position)
        {
            if (position.x < 0 || position.x >= width)
                return false;

            if (position.y < 0 || position.y >= height)
                return false;

            return true;
        }

        /// <summary>해당 칸 점유 아이템 반환</summary>
        public InventoryItemInstance GetItemAt(Vector2Int position)
        {
            if (!IsInBounds(position))
                return null;

            return occupiedGrid[position.x, position.y];
        }

        /// <summary>아이템 배치 가능 여부 확인</summary>
        public bool CanPlaceItem(ItemSO itemData, Vector2Int originPosition, bool isRotated)
        {
            if (itemData == null || !itemData.IsValid())
                return false;

            List<Vector2Int> occupiedCells = BuildPlacedCells(itemData, originPosition, isRotated);

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i];

                if (!IsInBounds(cell))
                    return false;

                if (occupiedGrid[cell.x, cell.y] != null)
                    return false;
            }

            return true;
        }

        /// <summary>아이템 자동 배치 좌표 탐색</summary>
        public InventoryPlacementResult FindPlacement(ItemSO itemData, bool allowRotation)
        {
            if (itemData == null || !itemData.IsValid())
            {
                return new InventoryPlacementResult(false, Vector2Int.zero, false);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);

                    if (CanPlaceItem(itemData, position, false))
                        return new InventoryPlacementResult(true, position, false);

                    if (allowRotation && itemData.CanRotate && CanPlaceItem(itemData, position, true))
                        return new InventoryPlacementResult(true, position, true);
                }
            }

            return new InventoryPlacementResult(false, Vector2Int.zero, false);
        }

        /// <summary>아이템 직접 배치</summary>
        public bool TryPlaceItem(ItemSO itemData, Vector2Int originPosition, bool isRotated, out InventoryItemInstance placedItem)
        {
            placedItem = null;

            if (!CanPlaceItem(itemData, originPosition, isRotated))
                return false;

            placedItem = new InventoryItemInstance(itemData, originPosition, isRotated);
            items.Add(placedItem);

            List<Vector2Int> occupiedCells = BuildPlacedCells(itemData, originPosition, isRotated);

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i];
                occupiedGrid[cell.x, cell.y] = placedItem;
            }

            EventBus.Publish(new InventoryItemAddedEvent(itemData.ItemId, 1));
            EventBus.Publish(new InventoryChangedEvent());
            return true;
        }

        /// <summary>아이템 자동 배치 시도</summary>
        public bool TryAutoPlaceItem(ItemSO itemData, out InventoryItemInstance placedItem)
        {
            placedItem = null;

            InventoryPlacementResult result = FindPlacement(itemData, true);

            if (!result.IsSuccess)
                return false;

            return TryPlaceItem(itemData, result.Position, result.IsRotated, out placedItem);
        }

        /// <summary>아이템 제거</summary>
        public bool RemoveItem(InventoryItemInstance itemInstance)
        {
            if (itemInstance == null)
                return false;

            if (!items.Remove(itemInstance))
                return false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (occupiedGrid[x, y] == itemInstance)
                        occupiedGrid[x, y] = null;
                }
            }

            EventBus.Publish(new InventoryItemRemovedEvent(itemInstance.ItemData.ItemId, 1));
            EventBus.Publish(new InventoryChangedEvent());
            return true;
        }

        /// <summary>인벤토리 총 적재 무게 계산</summary>
        public float CalculateTotalWeight()
        {
            float totalWeight = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ItemData == null)
                    continue;

                totalWeight += items[i].ItemData.Weight;
            }

            return totalWeight;
        }

        /// <summary>그리드 점유 정보 재구성</summary>
        public void RebuildOccupiedGrid()
        {
            occupiedGrid = new InventoryItemInstance[width, height];

            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemInstance itemInstance = items[i];
                List<Vector2Int> occupiedCells = BuildPlacedCells(itemInstance.ItemData, itemInstance.OriginPosition, itemInstance.IsRotated);

                for (int j = 0; j < occupiedCells.Count; j++)
                {
                    Vector2Int cell = occupiedCells[j];

                    if (!IsInBounds(cell))
                        continue;

                    occupiedGrid[cell.x, cell.y] = itemInstance;
                }
            }
        }

        /// <summary>배치된 모든 월드 셀 좌표 생성</summary>
        private List<Vector2Int> BuildPlacedCells(ItemSO itemData, Vector2Int originPosition, bool isRotated)
        {
            List<Vector2Int> cells = new List<Vector2Int>();

            for (int i = 0; i < itemData.OccupiedCells.Count; i++)
            {
                Vector2Int localCell = itemData.OccupiedCells[i].Position;
                Vector2Int rotatedCell = RotateCell(localCell, itemData.ShapeBounds, isRotated);
                cells.Add(originPosition + rotatedCell);
            }

            return cells;
        }

        /// <summary>회전 상태에 맞는 셀 좌표 계산</summary>
        private Vector2Int RotateCell(Vector2Int localCell, Vector2Int originalBounds, bool isRotated)
        {
            if (!isRotated)
                return localCell;

            return new Vector2Int(originalBounds.y - 1 - localCell.y, localCell.x);
        }
    }
}
