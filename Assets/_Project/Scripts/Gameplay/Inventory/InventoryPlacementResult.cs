using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>인벤토리 배치 결과를 보관한다.</summary>
    public readonly struct InventoryPlacementResult
    {
        public readonly bool IsSuccess; // 배치 성공 여부
        public readonly Vector2Int Position; // 배치 좌표
        public readonly int RotationQuarterTurns; // 적용된 90도 단위 회전 값

        /// <summary>배치 결과를 생성한다.</summary>
        public InventoryPlacementResult(bool isSuccess, Vector2Int position, int rotationQuarterTurns)
        {
            IsSuccess = isSuccess;
            Position = position;
            RotationQuarterTurns = InventoryRotationUtility.NormalizeQuarterTurns(rotationQuarterTurns);
        }
    }
}
