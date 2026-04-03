using System;
using Project.Data.Items;
using UnityEngine;

namespace Project.Gameplay.Inventory
{
    /// <summary>인벤토리 아이템의 런타임 배치 상태를 보관한다.</summary>
    [Serializable]
    public class InventoryItemInstance
    {
        [SerializeField] private ItemSO itemData; // 아이템 정적 데이터
        [SerializeField] private Vector2Int originPosition; // 배치 시작 좌표
        [SerializeField] private int rotationQuarterTurns; // 90도 단위 회전 값 (0~3)

        public ItemSO ItemData => itemData;
        public Vector2Int OriginPosition => originPosition;
        public int RotationQuarterTurns => InventoryRotationUtility.NormalizeQuarterTurns(rotationQuarterTurns);

        /// <summary>아이템 인스턴스를 생성한다.</summary>
        public InventoryItemInstance(ItemSO itemData, Vector2Int originPosition, int rotationQuarterTurns)
        {
            this.itemData = itemData;
            this.originPosition = originPosition;
            this.rotationQuarterTurns = InventoryRotationUtility.NormalizeQuarterTurns(rotationQuarterTurns);
        }

        /// <summary>배치 시작 좌표를 설정한다.</summary>
        public void SetOriginPosition(Vector2Int newOriginPosition)
        {
            originPosition = newOriginPosition;
        }

        /// <summary>회전 쿼터 턴 값을 설정한다.</summary>
        public void SetRotationQuarterTurns(int newRotationQuarterTurns)
        {
            rotationQuarterTurns = InventoryRotationUtility.NormalizeQuarterTurns(newRotationQuarterTurns);
        }

        /// <summary>회전 적용 후 외곽 크기를 반환한다.</summary>
        public Vector2Int GetBounds()
        {
            return InventoryRotationUtility.GetRotatedBounds(itemData.ShapeBounds, rotationQuarterTurns);
        }

        /// <summary>회전 적용 후 로컬 셀 좌표를 반환한다.</summary>
        public Vector2Int GetRotatedCellPosition(Vector2Int localCell)
        {
            return InventoryRotationUtility.RotateLocalCell(localCell, itemData.ShapeBounds, rotationQuarterTurns);
        }
    }
}
