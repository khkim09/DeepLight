using UnityEngine;

namespace Project.Gameplay.Inventory
{
    public readonly struct InventoryPlacementResult
    {
        public readonly bool IsSuccess; // 배치 성공 여부
        public readonly Vector2Int Position; // 배치 좌표
        public readonly bool IsRotated; // 적용 회전 여부

        /// <summary>배치 결과 생성</summary>
        public InventoryPlacementResult(bool isSuccess, Vector2Int position, bool isRotated)
        {
            IsSuccess = isSuccess;
            Position = position;
            IsRotated = isRotated;
        }
    }
}
