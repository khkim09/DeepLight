using System;
using Project.Data.Items;
using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>런타임 배치 상태</summary>
    [Serializable]
    public class InventoryItemInstance
    {
        [SerializeField] private ItemSO itemData; // 아이템 정적 데이터
        [SerializeField] private Vector2Int originPosition; // 배치 시작 좌표
        [SerializeField] private bool isRotated; // 회전 여부

        public ItemSO ItemData => itemData;
        public Vector2Int OriginPosition => originPosition;
        public bool IsRotated => isRotated;

        /// <summary>아이템 인스턴스 생성</summary>
        public InventoryItemInstance(ItemSO itemData, Vector2Int originPosition, bool isRotated)
        {
            this.itemData = itemData;
            this.originPosition = originPosition;
            this.isRotated = isRotated;
        }

        /// <summary>배치 시작 좌표 설정</summary>
        public void SetOriginPosition(Vector2Int newOriginPosition)
        {
            originPosition = newOriginPosition;
        }

        /// <summary>회전 상태 설정</summary>
        public void SetRotation(bool rotated)
        {
            isRotated = rotated;
        }

        /// <summary>회전 적용 후 외곽 크기 반환</summary>
        public Vector2Int GetBounds()
        {
            Vector2Int bounds = itemData.ShapeBounds;

            if (!isRotated) return bounds;

            return new Vector2Int(bounds.y, bounds.x);
        }

        /// <summary>회전 적용 후 로컬 셀 좌표 반환</summary>
        public Vector2Int GetRotatedCellPosition(Vector2Int localCell)
        {
            if (!isRotated) return localCell;

            Vector2Int originalBounds = itemData.ShapeBounds;
            return new Vector2Int(originalBounds.y - 1 - localCell.y, localCell.x);
        }
    }
}
