using Project.Data.Items;
using Project.Gameplay.Inventory;
using Project.Gameplay.Runtime;
using UnityEngine;

namespace Project.Gameplay.Services
{
    /// <summary>외부 시스템이 쓰는 조합용 진입점</summary>
    public class InventoryService
    {
        private readonly SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태

        public SubmarineRuntimeState SubmarineRuntimeState => submarineRuntimeState;

        /// <summary>인벤토리 서비스 생성</summary>
        public InventoryService(SubmarineRuntimeState submarineRuntimeState)
        {
            this.submarineRuntimeState = submarineRuntimeState;
        }

        /// <summary>아이템 자동 적재 시도</summary>
        public bool TryAddItem(ItemSO itemData, out InventoryItemInstance itemInstance)
        {
            itemInstance = null;

            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return false;

            return submarineRuntimeState.InventoryGrid.TryAutoPlaceItem(itemData, out itemInstance);
        }

        /// <summary>아이템 수동 배치 시도</summary>
        public bool TryAddItem(ItemSO itemData, Vector2Int originPosition, bool isRotated, out InventoryItemInstance itemInstance)
        {
            itemInstance = null;

            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return false;

            return submarineRuntimeState.InventoryGrid.TryPlaceItem(itemData, originPosition, isRotated, out itemInstance);
        }

        /// <summary>아이템 제거 시도</summary>
        public bool TryRemoveItem(InventoryItemInstance itemInstance)
        {
            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return false;

            return submarineRuntimeState.InventoryGrid.RemoveItem(itemInstance);
        }

        /// <summary>현재 총 적재 중량 반환</summary>
        public float GetTotalCargoWeight()
        {
            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return 0f;

            return submarineRuntimeState.InventoryGrid.CalculateTotalWeight();
        }
    }
}
