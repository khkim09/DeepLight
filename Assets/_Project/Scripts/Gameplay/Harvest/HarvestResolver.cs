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
        private readonly SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태
        private readonly ClawRuntimeState clawRuntimeState; // 기존 장비 내구도 상태(임시 재사용)
        private readonly InventoryService inventoryService; // 인벤토리 서비스

        public SubmarineRuntimeState SubmarineRuntimeState => submarineRuntimeState;
        public ClawRuntimeState ClawRuntimeState => clawRuntimeState;

        /// <summary>회수 해석기 생성</summary>
        public HarvestResolver(
            SubmarineRuntimeState submarineRuntimeState,
            ClawRuntimeState clawRuntimeState,
            InventoryService inventoryService)
        {
            this.submarineRuntimeState = submarineRuntimeState;
            this.clawRuntimeState = clawRuntimeState;
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

            float totalDifficulty = Mathf.Clamp01(itemData.BaseCatchDifficulty + targetData.AdditionalDifficulty);

            return clawRuntimeState.BaseStats.EvaluateCatchChance(
                itemData.Weight,
                totalDifficulty,
                submarineRuntimeState.CurrentBattery,
                submarineRuntimeState.BaseStats.MaxBattery,
                clawRuntimeState.CurrentDurability);
        }

        /// <summary>스캔 펄스에 필요한 배터리 소모량을 계산한다</summary>
        public float GetScanPulseBatteryCost(HarvestScanMode scanMode)
        {
            float baseCost = Mathf.Max(0.5f, clawRuntimeState.BaseStats.BatteryCostPerDrop * 0.35f);

            return scanMode switch
            {
                HarvestScanMode.Sonar => baseCost,
                HarvestScanMode.Lidar => baseCost * 1.2f,
                _ => 0f
            };
        }

        /// <summary>세션 상태를 기준으로 추정 회수 수치를 계산한다</summary>
        public void EvaluateRecoveryPlan(HarvestModeSession harvestModeSession, IReadOnlyList<HarvestScanPoint> allPoints)
        {
            if (harvestModeSession == null || harvestModeSession.CurrentTarget == null)
                return;

            float baseChance = EvaluateBaseRecoveryChance(harvestModeSession.CurrentTarget); // 대상 기본 성공률
            int revealedCount = harvestModeSession.RevealedPointIds.Count; // 공개 포인트 수
            int selectedCount = harvestModeSession.SelectedPointSequence.Count; // 선택 포인트 수

            float revealBonus = Mathf.Min(0.15f, revealedCount * 0.04f); // 공개 정보 보너스
            float sequenceBonus = 0f; // 선택 순서 보너스
            float sequenceRiskPenalty = 0f; // 순서 위험 페널티

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
                        sequenceBonus += point.FirstAnchorBias * 0.12f; // 첫 점 안정 보너스
                    else
                        sequenceBonus += point.SequenceBias * 0.08f; // 후속 점 보너스

                    sequenceBonus += point.BaseStability * 0.05f; // 기본 안정성 보너스
                    sequenceRiskPenalty += point.RiskWeight * 0.04f; // 위험도 패널티
                }
            }

            float finalChance = Mathf.Clamp01(baseChance + revealBonus + sequenceBonus - sequenceRiskPenalty); // 추정 최종 확률
            float batteryCost = harvestModeSession.ScanPulseCount * 0.5f + selectedCount * 0.75f; // 추정 배터리 비용
            float durabilityCost = Mathf.Max(0f, selectedCount * 0.4f + sequenceRiskPenalty * 10f); // 추정 내구도 비용

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

            // 최신 추정치 재계산
            EvaluateRecoveryPlan(harvestModeSession, allPoints);

            float finalChance = harvestModeSession.EstimatedRecoveryChance;
            float batteryCost = harvestModeSession.EstimatedBatteryCost;
            float durabilityCost = harvestModeSession.EstimatedDurabilityCost;

            // 실제 자원 소모 적용
            submarineRuntimeState.ConsumeBattery(batteryCost);
            clawRuntimeState.Damage(durabilityCost);

            // 방전 시 즉시 실패
            if (submarineRuntimeState.CurrentBattery <= 0f)
            {
                EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(targetData.TargetId));
                EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, false, 0f, false));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, 0f, false));

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, 0f, false, false);
            }

            float roll = Random.value; // 최종 랜덤값
            bool chanceSuccess = roll <= finalChance; // 성공 여부

            if (!chanceSuccess)
            {
                EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, false, finalChance, false));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, finalChance, false));

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, finalChance, false, false);
            }

            bool addedToInventory = inventoryService.TryAddItem(itemData, out InventoryItemInstance instance); // 인벤토리 적재 시도
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
