using System;
using Project.Core.Events;
using Project.Data.Submarine;
using Project.Gameplay.Inventory;
using UnityEngine;

namespace Project.Gameplay.Runtime
{
    /// <summary>현재 배터리, 내구도, 인벤토리 상태를 보관한다.</summary>
    [Serializable]
    public class SubmarineRuntimeState
    {
        [SerializeField] private SubmarineStatsSO baseStats; // 잠수함 기본 스탯 데이터
        [SerializeField] private float currentBattery; // 현재 배터리
        [SerializeField] private float currentHullDurability; // 현재 선체 내구도
        [SerializeField] private InventoryGridData inventoryGrid; // 인벤토리 런타임 데이터

        public SubmarineStatsSO BaseStats => baseStats;
        public float CurrentBattery => currentBattery;
        public float CurrentHullDurability => currentHullDurability;
        public InventoryGridData InventoryGrid => inventoryGrid;

        /// <summary>잠수함 런타임 상태 생성</summary>
        public SubmarineRuntimeState(SubmarineStatsSO baseStats)
        {
            this.baseStats = baseStats;

            currentBattery = baseStats.MaxBattery;
            currentHullDurability = baseStats.MaxHullDurability;

            if (baseStats.InventoryLayout != null)
                inventoryGrid = new InventoryGridData(baseStats.InventoryLayout);
        }

        /// <summary>잠수함 런타임 상태를 지정된 스탯으로 다시 초기화한다.</summary>
        public void Initialize(SubmarineStatsSO newBaseStats)
        {
            this.baseStats = newBaseStats;
            currentBattery = baseStats.MaxBattery;
            currentHullDurability = baseStats.MaxHullDurability;

            if (baseStats.InventoryLayout != null && baseStats.InventoryLayout.IsValid())
            {
                if (inventoryGrid == null)
                    inventoryGrid = new InventoryGridData(baseStats.InventoryLayout);
                else
                    inventoryGrid.Initialize(baseStats.InventoryLayout);
            }
        }

        /// <summary>배터리를 소비한다.</summary>
        public void ConsumeBattery(float amount)
        {
            currentBattery = Mathf.Max(0f, currentBattery - amount);
            EventBus.Publish(new BatteryChangedEvent(currentBattery, baseStats.MaxBattery));
        }

        /// <summary>배터리를 회복한다.</summary>
        public void RestoreBattery(float amount)
        {
            currentBattery = Mathf.Min(baseStats.MaxBattery, currentBattery + amount);
            EventBus.Publish(new BatteryChangedEvent(currentBattery, baseStats.MaxBattery));
        }

        /// <summary>선체 내구도를 감소시킨다.</summary>
        public void DamageHull(float amount)
        {
            currentHullDurability = Mathf.Max(0f, currentHullDurability - amount);
            EventBus.Publish(new HullDurabilityChangedEvent(currentHullDurability, baseStats.MaxHullDurability));
        }

        /// <summary>선체 내구도를 회복한다.</summary>
        public void RepairHull(float amount)
        {
            currentHullDurability = Mathf.Min(baseStats.MaxHullDurability, currentHullDurability + amount);
            EventBus.Publish(new HullDurabilityChangedEvent(currentHullDurability, baseStats.MaxHullDurability));
        }

        /// <summary>현재 이동 속도를 반환한다.</summary>
        public float GetCurrentMoveSpeed()
        {
            return baseStats.GetMoveSpeed();
        }

        /// <summary>현재 선회 속도를 반환한다.</summary>
        public float GetCurrentTurnSpeed()
        {
            return baseStats.GetTurnSpeed();
        }
    }
}
