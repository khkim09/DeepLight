using System;
using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.Items;
using Project.Data.Submarine;
using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>인벤토리 배치와 점유 검사를 담당하는 런타임 클래스</summary>
    [Serializable]
    public class InventoryGridData
    {
        [SerializeField] private int width; // 인벤토리 가로 칸 수
        [SerializeField] private int height; // 인벤토리 세로 칸 수

        private readonly List<InventoryItemInstance> items = new(); // 배치된 아이템 목록
        private InventoryItemInstance[,] occupiedGrid; // 칸 점유 정보
        private bool[,] usableMask; // 사용 가능 셀 마스크

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<InventoryItemInstance> Items => items;

        /// <summary>인벤토리 그리드를 생성한다</summary>
        public InventoryGridData(int width, int height)
        {
            // 기본 크기 저장
            this.width = width;
            this.height = height;

            // 점유 그리드 생성
            occupiedGrid = new InventoryItemInstance[width, height];

            // 기본은 전 칸 사용 가능
            usableMask = new bool[width, height];
            FillUsableMask(true);
        }

        /// <summary>그리드를 기본 크기로 초기화한다</summary>
        public void Initialize(int newWidth, int newHeight)
        {
            // 크기 저장
            width = newWidth;
            height = newHeight;

            // 아이템 목록 초기화
            items.Clear();

            // 점유 그리드 재생성
            occupiedGrid = new InventoryItemInstance[width, height];

            // 전 칸 사용 가능
            usableMask = new bool[width, height];
            FillUsableMask(true);
        }

        /// <summary>레이아웃 데이터로 그리드를 초기화한다</summary>
        public void Initialize(SubmarineInventoryLayoutSO layout)
        {
            // 레이아웃 유효성 실패면 중단
            if (layout == null || !layout.IsValid())
                return;

            // 크기 반영
            width = layout.GridSize.x;
            height = layout.GridSize.y;

            // 아이템 목록 초기화
            items.Clear();

            // 점유 그리드 재생성
            occupiedGrid = new InventoryItemInstance[width, height];

            // 사용 가능 마스크 생성
            usableMask = layout.BuildUsableMask();
        }

        /// <summary>모든 셀 사용 가능 상태를 채운다</summary>
        private void FillUsableMask(bool value)
        {
            // 전 셀 순회
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    usableMask[x, y] = value;
            }
        }

        /// <summary>좌표가 그리드 범위 내인지 확인한다</summary>
        public bool IsInBounds(Vector2Int position)
        {
            if (position.x < 0 || position.x >= width)
                return false;

            if (position.y < 0 || position.y >= height)
                return false;

            return true;
        }

        /// <summary>좌표가 실제 사용 가능한 칸인지 확인한다</summary>
        public bool IsUsableCell(Vector2Int position)
        {
            if (!IsInBounds(position))
                return false;

            if (usableMask == null)
                return true;

            return usableMask[position.x, position.y];
        }

        /// <summary>해당 칸 점유 아이템을 반환한다</summary>
        public InventoryItemInstance GetItemAt(Vector2Int position)
        {
            if (!IsInBounds(position))
                return null;

            return occupiedGrid[position.x, position.y];
        }

        /// <summary>아이템 배치 가능 여부를 확인한다</summary>
        public bool CanPlaceItem(ItemSO itemData, Vector2Int originPosition, bool isRotated)
        {
            if (itemData == null || !itemData.IsValid())
                return false;

            List<Vector2Int> occupiedCells = BuildPlacedCells(itemData, originPosition, isRotated);

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i];

                // 범위 밖이면 실패
                if (!IsInBounds(cell))
                    return false;

                // 사용 불가 칸이면 실패
                if (!IsUsableCell(cell))
                    return false;

                // 이미 점유 중이면 실패
                if (occupiedGrid[cell.x, cell.y] != null)
                    return false;
            }

            return true;
        }

        /// <summary>아이템 자동 배치 좌표를 탐색한다</summary>
        public InventoryPlacementResult FindPlacement(ItemSO itemData, bool allowRotation)
        {
            if (itemData == null || !itemData.IsValid())
                return new InventoryPlacementResult(false, Vector2Int.zero, false);

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

        /// <summary>아이템을 직접 배치한다</summary>
        public bool TryPlaceItem(ItemSO itemData, Vector2Int originPosition, bool isRotated, out InventoryItemInstance placedItem)
        {
            // 기본값 초기화
            placedItem = null;

            // 배치 가능성 검사
            if (!CanPlaceItem(itemData, originPosition, isRotated))
                return false;

            // 인스턴스 생성
            placedItem = new InventoryItemInstance(itemData, originPosition, isRotated);
            items.Add(placedItem);

            // 점유 셀 계산
            List<Vector2Int> occupiedCells = BuildPlacedCells(itemData, originPosition, isRotated);

            // 점유 반영
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i];
                occupiedGrid[cell.x, cell.y] = placedItem;
            }

            // 이벤트 발행
            EventBus.Publish(new InventoryItemAddedEvent(itemData.ItemId, 1));
            EventBus.Publish(new InventoryChangedEvent());
            return true;
        }

        /// <summary>아이템 자동 배치를 시도한다</summary>
        public bool TryAutoPlaceItem(ItemSO itemData, out InventoryItemInstance placedItem)
        {
            // 기본값 초기화
            placedItem = null;

            // 자동 배치 위치 탐색
            InventoryPlacementResult result = FindPlacement(itemData, true);
            if (!result.IsSuccess)
                return false;

            return TryPlaceItem(itemData, result.Position, result.IsRotated, out placedItem);
        }

        /// <summary>아이템을 제거한다</summary>
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

        /// <summary>총 적재 무게를 계산한다</summary>
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

        /// <summary>점유 정보를 다시 구성한다</summary>
        public void RebuildOccupiedGrid()
        {
            // 점유 그리드 초기화
            occupiedGrid = new InventoryItemInstance[width, height];

            // 아이템 순회
            for (int i = 0; i < items.Count; i++)
            {
                InventoryItemInstance itemInstance = items[i];
                List<Vector2Int> occupiedCells = BuildPlacedCells(itemInstance.ItemData, itemInstance.OriginPosition, itemInstance.IsRotated);

                for (int j = 0; j < occupiedCells.Count; j++)
                {
                    Vector2Int cell = occupiedCells[j];
                    if (!IsInBounds(cell))
                        continue;

                    if (!IsUsableCell(cell))
                        continue;

                    occupiedGrid[cell.x, cell.y] = itemInstance;
                }
            }
        }

        /// <summary>배치될 모든 셀 좌표를 생성한다</summary>
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

        /// <summary>회전 상태에 맞는 셀 좌표를 계산한다</summary>
        private Vector2Int RotateCell(Vector2Int localCell, Vector2Int originalBounds, bool isRotated)
        {
            if (!isRotated)
                return localCell;

            return new Vector2Int(originalBounds.y - 1 - localCell.y, localCell.x);
        }
    }
}
