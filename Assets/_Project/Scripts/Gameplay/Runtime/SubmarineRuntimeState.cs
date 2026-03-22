using System;
using Project.Core.Events;
using Project.Data.Submarine;
using Project.Gameplay.Inventory;
using UnityEngine;

namespace Project.Gameplay.Runtime
{
    /// <summary>현재 배터리, 내구도, 적재 중량 등 상태 보관</summary>
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
        public float CurrentCargoWeight => inventoryGrid != null ? inventoryGrid.CalculateTotalWeight() : 0f;

        /// <summary>잠수함 런타임 상태 생성</summary>
        public SubmarineRuntimeState(SubmarineStatsSO baseStats)
        {
            this.baseStats = baseStats;
            currentBattery = baseStats.MaxBattery;
            currentHullDurability = baseStats.MaxHullDurability;
            inventoryGrid = new InventoryGridData(baseStats.InventoryWidth, baseStats.InventoryHeight);
        }

        /// <summary>잠수함 런타임 상태 초기화</summary>
        public void Initialize(SubmarineStatsSO newBaseStats)
        {
            baseStats = newBaseStats;
            currentBattery = baseStats.MaxBattery;
            currentHullDurability = baseStats.MaxHullDurability;

            if (inventoryGrid == null)
            {
                inventoryGrid = new InventoryGridData(baseStats.InventoryWidth, baseStats.InventoryHeight);
                return;
            }

            inventoryGrid.Initialize(baseStats.InventoryWidth, baseStats.InventoryHeight);
        }

        /// <summary>배터리 소비</summary>
        public void ConsumeBattery(float amount)
        {
            currentBattery = Mathf.Max(0f, currentBattery - amount);
            EventBus.Publish(new BatteryChangedEvent(currentBattery, baseStats.MaxBattery));
        }

        /// <summary>배터리 회복</summary>
        public void RestoreBattery(float amount)
        {
            currentBattery = Mathf.Min(baseStats.MaxBattery, currentBattery + amount);
            EventBus.Publish(new BatteryChangedEvent(currentBattery, baseStats.MaxBattery));
        }

        /// <summary>선체 내구도 감소</summary>
        public void DamageHull(float amount)
        {
            currentHullDurability = Mathf.Max(0f, currentHullDurability - amount);
            EventBus.Publish(new HullDurabilityChangedEvent(currentHullDurability, baseStats.MaxHullDurability));
        }

        /// <summary>선체 내구도 회복</summary>
        public void RepairHull(float amount)
        {
            currentHullDurability = Mathf.Min(baseStats.MaxHullDurability, currentHullDurability + amount);
            EventBus.Publish(new HullDurabilityChangedEvent(currentHullDurability, baseStats.MaxHullDurability));
        }

        /// <summary>현재 적재 중량 기준 이동 속도 반환</summary>
        public float GetCurrentMoveSpeed()
        {
            return baseStats.GetMoveSpeed(CurrentCargoWeight);
        }

        /// <summary>현재 적재 중량 기준 선회 속도 반환</summary>
        public float GetCurrentTurnSpeed()
        {
            return baseStats.GetTurnSpeed(CurrentCargoWeight);
        }
    }
}
