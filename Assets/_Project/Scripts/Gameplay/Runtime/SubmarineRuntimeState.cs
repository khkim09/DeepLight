using System;
using System.Collections.Generic;
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
        [SerializeField] private SubmarineInventoryLayoutSO currentInventoryLayout; // 현재 적용 중인 인벤토리 레이아웃

        public SubmarineStatsSO BaseStats => baseStats;
        public float CurrentBattery => currentBattery;
        public float CurrentHullDurability => currentHullDurability;
        public InventoryGridData InventoryGrid => inventoryGrid;
        public SubmarineInventoryLayoutSO CurrentInventoryLayout => currentInventoryLayout;

        /// <summary>잠수함 런타임 상태 생성</summary>
        public SubmarineRuntimeState(SubmarineStatsSO baseStats)
        {
            this.baseStats = baseStats;

            currentBattery = baseStats.MaxBattery;
            currentHullDurability = baseStats.MaxHullDurability;
            currentInventoryLayout = baseStats.InventoryLayout;

            if (currentInventoryLayout != null)
                inventoryGrid = new InventoryGridData(currentInventoryLayout);
        }

        /// <summary>잠수함 런타임 상태를 지정된 스탯으로 다시 초기화한다.</summary>
        public void Initialize(SubmarineStatsSO newBaseStats)
        {
            baseStats = newBaseStats;
            currentBattery = baseStats.MaxBattery;
            currentHullDurability = baseStats.MaxHullDurability;
            currentInventoryLayout = baseStats.InventoryLayout;

            if (currentInventoryLayout != null && currentInventoryLayout.IsValid())
            {
                if (inventoryGrid == null)
                    inventoryGrid = new InventoryGridData(currentInventoryLayout);
                else
                    inventoryGrid.Initialize(currentInventoryLayout);
            }
        }

        /// <summary>현재 배치 아이템을 유지한 채 새 인벤토리 레이아웃 적용을 시도한다.</summary>
        public bool TryApplyInventoryLayout(SubmarineInventoryLayoutSO newLayout)
        {
            if (newLayout == null || !newLayout.IsValid())
                return false;

            if (inventoryGrid == null)
            {
                currentInventoryLayout = newLayout;
                inventoryGrid = new InventoryGridData(newLayout);
                EventBus.Publish(new InventoryChangedEvent());
                return true;
            }

            // 기존 아이템 상태를 안전하게 복사해 둔다.
            List<InventoryItemInstance> previousItems = new();
            IReadOnlyList<InventoryItemInstance> currentItems = inventoryGrid.Items;

            for (int i = 0; i < currentItems.Count; i++)
            {
                InventoryItemInstance itemInstance = currentItems[i];
                if (itemInstance == null || itemInstance.ItemData == null)
                    continue;

                previousItems.Add(new InventoryItemInstance(
                    itemInstance.ItemData,
                    itemInstance.OriginPosition,
                    itemInstance.RotationQuarterTurns));
            }

            // 새 레이아웃 그리드에 같은 위치/회전으로 모두 들어가는지 검사한다.
            InventoryGridData rebuiltGrid = new InventoryGridData(newLayout);

            for (int i = 0; i < previousItems.Count; i++)
            {
                bool isPlaced = rebuiltGrid.TryPlaceExistingInstance(previousItems[i]);
                if (!isPlaced)
                    return false;
            }

            currentInventoryLayout = newLayout;
            inventoryGrid = rebuiltGrid;

            EventBus.Publish(new InventoryChangedEvent());
            return true;
        }

        /// <summary>정상 운영 비용으로 배터리를 소비한다.</summary>
        public void ConsumeBatteryOperational(float amount)
        {
            if (baseStats == null)
                return;

            currentBattery = Mathf.Max(0f, currentBattery - Mathf.Max(0f, amount));
            EventBus.Publish(new BatteryChangedEvent(currentBattery, baseStats.MaxBattery));
        }

        /// <summary>위험 상황으로 배터리를 소비하고 위험 피드백 이벤트를 발행한다.</summary>
        public void ConsumeBatteryDanger(float amount, float intensityMultiplier = 1f)
        {
            if (baseStats == null)
                return;

            float clampedAmount = Mathf.Max(0f, amount);
            if (clampedAmount <= 0f)
                return;

            currentBattery = Mathf.Max(0f, currentBattery - clampedAmount);

            // 실제 배터리 값 갱신
            EventBus.Publish(new BatteryChangedEvent(currentBattery, baseStats.MaxBattery));

            // HUD 바운스/지연 손실 연출용 danger 피드백
            EventBus.Publish(new BatteryDangerFeedbackEvent(
                clampedAmount,
                currentBattery,
                baseStats.MaxBattery,
                Mathf.Max(0f, intensityMultiplier)));
        }

        /// <summary>정상 운영 비용으로 선체 내구도를 감소시킨다.</summary>
        public void DamageHullOperational(float amount)
        {
            if (baseStats == null)
                return;

            currentHullDurability = Mathf.Max(0f, currentHullDurability - Mathf.Max(0f, amount));
            EventBus.Publish(new HullDurabilityChangedEvent(currentHullDurability, baseStats.MaxHullDurability));
        }

        /// <summary>위험 피해로 선체 내구도를 감소시키고 위험 피드백 이벤트를 발행한다.</summary>
        public void DamageHullDanger(
            float amount,
            SubmarineDangerFeedbackType feedbackType,
            float intensityMultiplier = 1f)
        {
            if (baseStats == null)
                return;

            float clampedAmount = Mathf.Max(0f, amount);
            if (clampedAmount <= 0f)
                return;

            currentHullDurability = Mathf.Max(0f, currentHullDurability - clampedAmount);
            EventBus.Publish(new HullDurabilityChangedEvent(currentHullDurability, baseStats.MaxHullDurability));

            EventBus.Publish(new SubmarineDangerFeedbackEvent(
                feedbackType,
                clampedAmount,
                currentHullDurability,
                baseStats.MaxHullDurability,
                Mathf.Max(0f, intensityMultiplier)));
        }

        /// <summary>배터리를 소비한다. 레거시 호출은 정상 운영 비용으로 처리한다.</summary>
        public void ConsumeBattery(float amount)
        {
            ConsumeBatteryOperational(amount);
        }

        /// <summary>배터리를 회복한다.</summary>
        public void RestoreBattery(float amount)
        {
            if (baseStats == null)
                return;

            currentBattery = Mathf.Min(baseStats.MaxBattery, currentBattery + Mathf.Max(0f, amount));
            EventBus.Publish(new BatteryChangedEvent(currentBattery, baseStats.MaxBattery));
        }

        /// <summary>선체 내구도를 감소시킨다. 레거시 호출은 정상 운영 비용으로 처리한다.</summary>
        public void DamageHull(float amount)
        {
            DamageHullOperational(amount);
        }

        /// <summary>선체 내구도를 회복한다.</summary>
        public void RepairHull(float amount)
        {
            if (baseStats == null)
                return;

            currentHullDurability = Mathf.Min(baseStats.MaxHullDurability, currentHullDurability + Mathf.Max(0f, amount));
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
