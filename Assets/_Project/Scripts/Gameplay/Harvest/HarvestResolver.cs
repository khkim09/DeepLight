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
    /// <summary>회수 성공률과 결과 처리를 계산하는 클래스</summary>
    public class HarvestResolver
    {
        private readonly SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태
        private readonly InventoryService inventoryService; // 인벤토리 서비스
        private readonly HarvestRecoveryTuningSO tuning; // 회수 계산 튜닝

        public SubmarineRuntimeState SubmarineRuntimeState => submarineRuntimeState;

        /// <summary>회수 계산에 필요한 런타임 참조를 생성한다</summary>
        public HarvestResolver(
            SubmarineRuntimeState submarineRuntimeState,
            InventoryService inventoryService,
            HarvestRecoveryTuningSO tuning)
        {
            this.submarineRuntimeState = submarineRuntimeState;
            this.inventoryService = inventoryService;
            this.tuning = tuning;
        }

        /// <summary>대상의 기본 회수 성공률을 계산한다</summary>
        public float EvaluateBaseRecoveryChance(IHarvestTarget harvestTarget)
        {
            if (harvestTarget == null || !harvestTarget.IsAvailable || tuning == null)
                return 0f;

            HarvestTargetSO targetData = harvestTarget.TargetData;
            if (targetData == null || !targetData.IsValid())
                return 0f;

            ItemSO itemData = targetData.ItemData;
            if (itemData == null || !itemData.IsValid())
                return 0f;

            // 대상 난이도, 배터리 상태, 무게 패널티를 합쳐 기본 성공률을 만든다.
            float difficulty = Mathf.Clamp01(itemData.BaseCatchDifficulty + targetData.AdditionalDifficulty);

            float batteryRatio = submarineRuntimeState.BaseStats.MaxBattery <= 0f
                ? 0f
                : submarineRuntimeState.CurrentBattery / submarineRuntimeState.BaseStats.MaxBattery;

            float batteryBonus = Mathf.Lerp(tuning.BatteryBonusAtEmpty, tuning.BatteryBonusAtFull, batteryRatio);
            float weightPenalty = Mathf.Clamp01(itemData.Weight / Mathf.Max(1f, tuning.WeightNormalization)) * tuning.WeightPenaltyMultiplier;

            return Mathf.Clamp01(tuning.BaseRecoveryChance - difficulty * tuning.DifficultyPenaltyMultiplier + batteryBonus - weightPenalty);
        }

        /// <summary>현재 센서 모드의 스캔 1회 배터리 비용을 반환한다</summary>
        public float GetScanPulseBatteryCost(HarvestScanMode scanMode)
        {
            if (tuning == null)
                return 0f;

            return scanMode switch
            {
                HarvestScanMode.Sonar => tuning.SonarPulseBatteryCost,
                HarvestScanMode.Lidar => tuning.LidarPulseBatteryCost,
                _ => 0f
            };
        }

        /// <summary>세션 상태를 기준으로 추정 회수 결과를 계산한다</summary>
        public void EvaluateRecoveryPlan(HarvestModeSession harvestModeSession, IReadOnlyList<HarvestScanPoint> allPoints)
        {
            if (harvestModeSession == null || harvestModeSession.CurrentTarget == null || tuning == null)
                return;

            float baseChance = EvaluateBaseRecoveryChance(harvestModeSession.CurrentTarget);
            int revealedCount = harvestModeSession.RevealedPointIds.Count;
            int selectedCount = harvestModeSession.SelectedPointSequence.Count;

            // 공개 포인트 수가 많을수록 정보량이 증가한다.
            float revealBonus = Mathf.Min(tuning.MaxRevealBonus, revealedCount * tuning.RevealBonusPerPoint);

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

                    // 첫 포인트와 후속 포인트의 의미를 다르게 반영한다.
                    if (i == 0)
                        sequenceBonus += point.FirstAnchorBias * tuning.FirstAnchorBiasMultiplier;
                    else
                        sequenceBonus += point.SequenceBias * tuning.SequenceBiasMultiplier;

                    sequenceBonus += point.BaseStability * tuning.BaseStabilityMultiplier;
                    sequenceRiskPenalty += point.RiskWeight * tuning.RiskPenaltyMultiplier;
                }
            }

            float finalChance = Mathf.Clamp01(baseChance + revealBonus + sequenceBonus - sequenceRiskPenalty);

            // 현재 버전은 스캔 평균 비용과 선택 포인트 비용을 합산한다.
            float averagePulseCost = (tuning.SonarPulseBatteryCost + tuning.LidarPulseBatteryCost) * 0.5f;
            float batteryCost = harvestModeSession.ScanPulseCount * averagePulseCost + selectedCount * tuning.SelectedPointBatteryCost;
            float durabilityCost = selectedCount * tuning.SelectedPointDurabilityCost +
                                   sequenceRiskPenalty * tuning.RiskPenaltyToDurabilityMultiplier;

            harvestModeSession.SetEstimatedOutcome(finalChance, batteryCost, durabilityCost);

            EventBus.Publish(new HarvestRecoveryPreviewUpdatedEvent(
                harvestModeSession.EstimatedRecoveryChance,
                harvestModeSession.EstimatedBatteryCost,
                harvestModeSession.EstimatedDurabilityCost));
        }

        /// <summary>현재 세션의 회수 결과를 확정하고 자원 획득을 처리한다</summary>
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

            // 회수 확정 시 배터리를 즉시 소모한다.
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
