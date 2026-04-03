using Project.Core.Events;
using Project.Data.Items;
using Project.Gameplay.Inventory;
using Project.Gameplay.Runtime;
using UnityEngine;

namespace Project.Gameplay.Services
{
    /// <summary>외부 시스템이 사용하는 인벤토리 조작 진입점이다.</summary>
    public class InventoryService
    {
        private readonly SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태

        public SubmarineRuntimeState SubmarineRuntimeState => submarineRuntimeState;

        /// <summary>인벤토리 서비스를 생성한다.</summary>
        public InventoryService(SubmarineRuntimeState submarineRuntimeState)
        {
            this.submarineRuntimeState = submarineRuntimeState;
        }

        /// <summary>아이템 수동 배치를 시도한다.</summary>
        public bool TryAddItem(ItemSO itemData, Vector2Int originPosition, int rotationQuarterTurns, out InventoryItemInstance itemInstance)
        {
            itemInstance = null;

            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return false;

            bool isSuccess = submarineRuntimeState.InventoryGrid.TryPlaceItem(
                itemData,
                originPosition,
                rotationQuarterTurns,
                out itemInstance);

            if (!isSuccess)
                return false;

            EventBus.Publish(new InventoryItemAddedEvent(itemData.ItemId, 1));
            EventBus.Publish(new InventoryChangedEvent());

            return true;
        }

        /// <summary>아이템 제거를 시도한다.</summary>
        public bool TryRemoveItem(InventoryItemInstance itemInstance)
        {
            if (submarineRuntimeState == null || submarineRuntimeState.InventoryGrid == null)
                return false;

            if (itemInstance == null || itemInstance.ItemData == null)
                return false;

            bool isSuccess = submarineRuntimeState.InventoryGrid.RemoveItem(itemInstance);
            if (!isSuccess)
                return false;

            EventBus.Publish(new InventoryItemRemovedEvent(itemInstance.ItemData.ItemId, 1));
            EventBus.Publish(new InventoryChangedEvent());

            return true;
        }
    }
}
