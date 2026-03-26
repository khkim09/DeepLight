using System.Collections.Generic;
using System.Linq;
using Project.Core.Events;
using Project.Data.Enums;
using Project.Data.Harvest;
using Project.Data.Items;
using Project.Gameplay.GameModes;
using Project.Gameplay.Inventory;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Gameplay.Harvest
{
    /// <summary>회수 콘솔의 추정 계산과 최종 회수 결과를 담당하는 클래스</summary>
    public class HarvestResolver
    {
        private readonly SubmarineRuntimeState submarineRuntimeState;
        private readonly InventoryService inventoryService;

        public SubmarineRuntimeState SubmarineRuntimeState => submarineRuntimeState;

        public HarvestResolver(
            SubmarineRuntimeState submarineRuntimeState,
            InventoryService inventoryService)
        {
            this.submarineRuntimeState = submarineRuntimeState;
            this.inventoryService = inventoryService;
        }

        /// <summary>대상의 기본 회수 성공률을 계산한다</summary>
        public float EvaluateBaseRecoveryChance(IHarvestTarget harvestTarget)
        {
            if (harvestTarget == null || !harvestTarget.IsAvailable)
                return 0f;

            HarvestTargetSO targetData = harvestTarget.TargetData;
            if (targetData == null || !targetData.IsValid())
                return 0f;

            ItemSO itemData = targetData.ItemData;
            if (itemData == null || !itemData.IsValid())
                return 0f;

            float difficulty = Mathf.Clamp01(itemData.BaseCatchDifficulty + targetData.AdditionalDifficulty);

            float batteryRatio = submarineRuntimeState.BaseStats.MaxBattery <= 0f
                ? 0f
                : submarineRuntimeState.CurrentBattery / submarineRuntimeState.BaseStats.MaxBattery;

            float batteryBonus = Mathf.Lerp(-0.15f, 0.1f, batteryRatio);
            float weightPenalty = Mathf.Clamp01(itemData.Weight / 100f) * 0.12f;

            return Mathf.Clamp01(0.78f - difficulty * 0.45f + batteryBonus - weightPenalty);
        }

        /// <summary>스캔 펄스에 필요한 배터리 소모량을 계산한다</summary>
        public float GetScanPulseBatteryCost(HarvestScanMode scanMode)
        {
            return scanMode switch
            {
                HarvestScanMode.Sonar => 0.6f,
                HarvestScanMode.Lidar => 0.9f,
                _ => 0f
            };
        }

        /// <summary>세션 상태를 기준으로 추정 회수 수치를 계산한다</summary>
        public void EvaluateRecoveryPlan(HarvestModeSession harvestModeSession, IReadOnlyList<HarvestScanPoint> allPoints)
        {
            if (harvestModeSession == null || harvestModeSession.CurrentTarget == null)
                return;

            float baseChance = EvaluateBaseRecoveryChance(harvestModeSession.CurrentTarget);
            int revealedCount = harvestModeSession.RevealedPointIds.Count;
            int selectedCount = harvestModeSession.SelectedPointSequence.Count;

            float revealBonus = Mathf.Min(0.18f, revealedCount * 0.045f);
            float sequenceBonus = 0f;
            float sequenceRiskPenalty = 0f;

            if (allPoints != null && selectedCount > 0)
            {
                Dictionary<string, HarvestScanPoint> pointMap = allPoints
                    .Where(point => point != null)
                    .GroupBy(point => point.PointId)
                    .ToDictionary(group => group.Key, group => group.First());

                for (int i = 0; i < harvestModeSession.SelectedPointSequence.Count; i++)
                {
                    string pointId = harvestModeSession.SelectedPointSequence[i];
                    if (!pointMap.TryGetValue(pointId, out HarvestScanPoint point))
                        continue;

                    if (i == 0)
                        sequenceBonus += point.FirstAnchorBias * 0.13f;
                    else
                        sequenceBonus += point.SequenceBias * 0.09f;

                    sequenceBonus += point.BaseStability * 0.05f;
                    sequenceRiskPenalty += point.RiskWeight * 0.05f;
                }
            }

            float finalChance = Mathf.Clamp01(baseChance + revealBonus + sequenceBonus - sequenceRiskPenalty);
            float batteryCost = harvestModeSession.ScanPulseCount * 0.6f + selectedCount * 0.8f;
            float durabilityCost = 0f;

            harvestModeSession.SetEstimatedOutcome(finalChance, batteryCost, durabilityCost);

            EventBus.Publish(new HarvestRecoveryPreviewUpdatedEvent(
                harvestModeSession.EstimatedRecoveryChance,
                harvestModeSession.EstimatedBatteryCost,
                harvestModeSession.EstimatedDurabilityCost));
        }

        /// <summary>현재 세션의 회수 계획을 확정하고 최종 결과를 계산한다</summary>
        public HarvestResolveResult ResolveCommittedRecovery(HarvestModeSession harvestModeSession, IReadOnlyList<HarvestScanPoint> allPoints)
        {
            if (harvestModeSession == null || harvestModeSession.CurrentTarget == null)
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            IHarvestTarget harvestTarget = harvestModeSession.CurrentTarget;
            if (!harvestTarget.IsAvailable)
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            HarvestTargetSO targetData = harvestTarget.TargetData;
            if (targetData == null || !targetData.IsValid())
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            ItemSO itemData = targetData.ItemData;
            if (itemData == null || !itemData.IsValid())
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            EvaluateRecoveryPlan(harvestModeSession, allPoints);

            float finalChance = harvestModeSession.EstimatedRecoveryChance;
            float batteryCost = harvestModeSession.EstimatedBatteryCost;

            submarineRuntimeState.ConsumeBattery(batteryCost);

            if (submarineRuntimeState.CurrentBattery <= 0f)
            {
                EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(targetData.TargetId));
                EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, false, 0f, false));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, 0f, false));

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, 0f, false, false);
            }

            bool chanceSuccess = Random.value <= finalChance;
            if (!chanceSuccess)
            {
                EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, false, finalChance, false));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, finalChance, false));

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, finalChance, false, false);
            }

            bool addedToInventory = inventoryService.TryAddItem(itemData, out InventoryItemInstance instance);
            bool finalSuccess = addedToInventory;

            if (finalSuccess && targetData.ConsumeOnSuccess)
                harvestTarget.Consume();

            EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, finalSuccess, finalChance, addedToInventory));
            EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, finalSuccess, finalChance, addedToInventory));

            harvestModeSession.MarkResolved();
            return new HarvestResolveResult(itemData.ItemId, finalSuccess, finalChance, addedToInventory, false);
        }
    }
}
