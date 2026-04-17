using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>인벤토리 배치 판정 유형이다.</summary>
    public enum InventoryPlacementType
    {
        Invalid,
        Place,
        Swap
    }

    /// <summary>인벤토리 배치 평가 결과를 보관한다.</summary>
    public readonly struct InventoryPlacementResult
    {
        public readonly InventoryPlacementType PlacementType; // 배치 결과 유형
        public readonly Vector2Int Position; // 배치 기준 좌표
        public readonly int RotationQuarterTurns; // 적용 회전 값
        public readonly InventoryItemInstance SwappedItemInstance; // swap 시 손에 들게 될 기존 아이템

        public bool IsSuccess => PlacementType != InventoryPlacementType.Invalid;
        public bool IsDirectPlacement => PlacementType == InventoryPlacementType.Place;
        public bool IsSwap => PlacementType == InventoryPlacementType.Swap;

        /// <summary>배치 평가 결과를 생성한다.</summary>
        public InventoryPlacementResult(
            InventoryPlacementType placementType,
            Vector2Int position,
            int rotationQuarterTurns,
            InventoryItemInstance swappedItemInstance)
        {
            PlacementType = placementType;
            Position = position;
            RotationQuarterTurns = InventoryRotationUtility.NormalizeQuarterTurns(rotationQuarterTurns);
            SwappedItemInstance = swappedItemInstance;
        }
    }
}
