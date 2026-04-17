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
            return CanPlaceItemIgnoring(item, origin, rotationQuarterTurns, null);
        }

        /// <summary>특정 아이템 하나를 무시한 상태로 배치 가능 여부를 검사한다.</summary>
        public bool CanPlaceItemIgnoring(
            ItemSO item,
            Vector2Int origin,
            int rotationQuarterTurns,
            InventoryItemInstance ignoredItemInstance)
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
                InventoryItemInstance occupiedItem = occupiedGrid[cell.x, cell.y];
                if (occupiedItem != null && occupiedItem != ignoredItemInstance)
                    return false;
            }

            return true;
        }

        /// <summary>직접 배치/교체 배치/불가 여부를 종합 평가한다.</summary>
        public InventoryPlacementResult EvaluatePlacement(ItemSO item, Vector2Int origin, int rotationQuarterTurns)
        {
            rotationQuarterTurns = InventoryRotationUtility.NormalizeQuarterTurns(rotationQuarterTurns);

            if (item == null)
            {
                return new InventoryPlacementResult(
                    InventoryPlacementType.Invalid,
                    origin,
                    rotationQuarterTurns,
                    null);
            }

            List<Vector2Int> targetCells = BuildPlacedCells(item, origin, rotationQuarterTurns);
            HashSet<InventoryItemInstance> overlappedItems = new();

            for (int i = 0; i < targetCells.Count; i++)
            {
                Vector2Int cell = targetCells[i];

                // 범위 밖이면 즉시 실패
                if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
                {
                    return new InventoryPlacementResult(
                        InventoryPlacementType.Invalid,
                        origin,
                        rotationQuarterTurns,
                        null);
                }

                // 비사용 칸이면 즉시 실패
                if (!usableMask[cell.x, cell.y])
                {
                    return new InventoryPlacementResult(
                        InventoryPlacementType.Invalid,
                        origin,
                        rotationQuarterTurns,
                        null);
                }

                InventoryItemInstance occupiedItem = occupiedGrid[cell.x, cell.y];
                if (occupiedItem != null)
                    overlappedItems.Add(occupiedItem);
            }

            // 완전 빈 공간이면 일반 배치 가능
            if (overlappedItems.Count == 0)
            {
                return new InventoryPlacementResult(
                    InventoryPlacementType.Place,
                    origin,
                    rotationQuarterTurns,
                    null);
            }

            // 2개 이상과 충돌하면 교체 불가
            if (overlappedItems.Count > 1)
            {
                return new InventoryPlacementResult(
                    InventoryPlacementType.Invalid,
                    origin,
                    rotationQuarterTurns,
                    null);
            }

            // 정확히 1개와만 충돌하면, 그 아이템만 비웠을 때 정상 배치 가능한지 검사한다.
            InventoryItemInstance swapCandidate = null;
            foreach (InventoryItemInstance overlappedItem in overlappedItems)
            {
                swapCandidate = overlappedItem;
                break;
            }

            bool canSwapPlace = CanPlaceItemIgnoring(
                item,
                origin,
                rotationQuarterTurns,
                swapCandidate);

            return new InventoryPlacementResult(
                canSwapPlace ? InventoryPlacementType.Swap : InventoryPlacementType.Invalid,
                origin,
                rotationQuarterTurns,
                canSwapPlace ? swapCandidate : null);
        }

        /// <summary>아이템을 특정 위치에 직접 배치하고 점유 정보를 갱신한다.</summary>
        public bool TryPlaceItem(
            ItemSO itemData,
            Vector2Int origin,
            int rotationQuarterTurns,
            out InventoryItemInstance instance)
        {
            instance = null;

            InventoryPlacementResult placementResult = EvaluatePlacement(itemData, origin, rotationQuarterTurns);
            if (!placementResult.IsDirectPlacement)
                return false;

            instance = new InventoryItemInstance(itemData, origin, rotationQuarterTurns);
            return TryPlaceExistingInstance(instance);
        }

        /// <summary>직접 배치 또는 단일 아이템 교체 배치를 시도한다.</summary>
        public bool TryPlaceOrSwapItem(
            ItemSO itemData,
            Vector2Int origin,
            int rotationQuarterTurns,
            out InventoryItemInstance placedInstance,
            out InventoryItemInstance swappedInstance)
        {
            placedInstance = null;
            swappedInstance = null;

            InventoryPlacementResult placementResult = EvaluatePlacement(itemData, origin, rotationQuarterTurns);
            if (!placementResult.IsSuccess)
                return false;

            // 일반 배치
            if (placementResult.IsDirectPlacement)
                return TryPlaceItem(itemData, origin, rotationQuarterTurns, out placedInstance);

            // 교체 배치
            swappedInstance = placementResult.SwappedItemInstance;
            if (swappedInstance == null)
                return false;

            bool removed = RemoveItem(swappedInstance);
            if (!removed)
                return false;

            bool placed = TryPlaceItem(itemData, origin, rotationQuarterTurns, out placedInstance);
            if (placed)
                return true;

            // 예기치 못한 실패 시 기존 아이템을 원복한다.
            TryPlaceExistingInstance(swappedInstance);
            swappedInstance = null;
            return false;
        }

        /// <summary>이미 존재하는 인벤토리 인스턴스를 현재 그리드에 다시 배치한다.</summary>
        public bool TryPlaceExistingInstance(InventoryItemInstance itemInstance)
        {
            if (itemInstance == null || itemInstance.ItemData == null)
                return false;

            bool canPlace = CanPlaceItem(
                itemInstance.ItemData,
                itemInstance.OriginPosition,
                itemInstance.RotationQuarterTurns);

            if (!canPlace)
                return false;

            items.Add(itemInstance);

            List<Vector2Int> cells = BuildPlacedCells(
                itemInstance.ItemData,
                itemInstance.OriginPosition,
                itemInstance.RotationQuarterTurns);

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                occupiedGrid[cell.x, cell.y] = itemInstance;
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

        /// <summary>지정 칸을 점유 중인 아이템 인스턴스를 반환한다.</summary>
        public InventoryItemInstance GetItemAtCell(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
                return null;

            return occupiedGrid[cell.x, cell.y];
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
