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
    /// <summary>회수 성공률, 배터리/내구도 비용, 결과 처리를 계산한다.</summary>
    public class HarvestResolver
    {
        private readonly SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태
        private readonly InventoryService inventoryService; // 인벤토리 서비스
        private readonly HarvestRecoveryTuningSO tuning; // 회수 계산 튜닝

        /// <summary>잠수함 런타임 상태를 반환한다.</summary>
        public SubmarineRuntimeState SubmarineRuntimeState => submarineRuntimeState;

        /// <summary>회수 계산에 필요한 런타임 참조를 생성한다.</summary>
        public HarvestResolver(
            SubmarineRuntimeState submarineRuntimeState,
            InventoryService inventoryService,
            HarvestRecoveryTuningSO tuning)
        {
            this.submarineRuntimeState = submarineRuntimeState;
            this.inventoryService = inventoryService;
            this.tuning = tuning;
        }

        /// <summary>대상의 기본 회수 성공률을 계산한다.</summary>
        public float EvaluateBaseRecoveryChance(IHarvestTarget harvestTarget)
        {
            if (harvestTarget == null || !harvestTarget.IsAvailable || tuning == null)
                return 0f;

            HarvestTargetSO targetData = harvestTarget.TargetData;
            if (targetData == null || !targetData.IsValid())
                return 0f;

            ItemSO itemData = ResolveTargetItem(harvestTarget);
            if (itemData == null || !itemData.IsValid())
                return 0f;

            // 기본 난이도 = 아이템 난이도 + 타깃 추가 난이도
            float difficulty = Mathf.Clamp01(itemData.BaseCatchDifficulty + targetData.AdditionalDifficulty);

            // 배터리가 많을수록 약간 유리
            float batteryRatio = submarineRuntimeState.BaseStats.MaxBattery <= 0f
                ? 0f
                : submarineRuntimeState.CurrentBattery / submarineRuntimeState.BaseStats.MaxBattery;

            float batteryBonus = Mathf.Lerp(tuning.BatteryBonusAtEmpty, tuning.BatteryBonusAtFull, batteryRatio);

            return Mathf.Clamp01(
                tuning.BaseRecoveryChance
                - difficulty * tuning.DifficultyPenaltyMultiplier
                + batteryBonus);
        }

        /// <summary>현재 센서 모드의 스캔 1회 배터리 비용을 반환한다.</summary>
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

        /// <summary>세션 상태를 기준으로 추정 회수 결과를 계산한다.</summary>
        public void EvaluateRecoveryPlan(HarvestModeSession harvestModeSession, IReadOnlyList<HarvestScanPoint> allPoints)
        {
            if (harvestModeSession == null || harvestModeSession.CurrentTarget == null || tuning == null)
                return;

            float baseChance = EvaluateBaseRecoveryChance(harvestModeSession.CurrentTarget);
            int revealedCount = harvestModeSession.RevealedPointIds.Count;
            int selectedCount = harvestModeSession.GetAssignedPointCount();

            // 공개된 포인트 수 보너스
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

                    // 비어 있는 슬롯은 건너뛴다.
                    if (string.IsNullOrWhiteSpace(pointId))
                        continue;

                    if (!pointMap.TryGetValue(pointId, out HarvestScanPoint point))
                        continue;

                    if (i == 0)
                        sequenceBonus += point.FirstAnchorBias * tuning.FirstAnchorBiasMultiplier;
                    else
                        sequenceBonus += point.SequenceBias * tuning.SequenceBiasMultiplier;

                    sequenceBonus += point.BaseStability * tuning.BaseStabilityMultiplier;
                    sequenceRiskPenalty += point.RiskWeight * tuning.RiskPenaltyMultiplier;
                }
            }

            float finalChance = Mathf.Clamp01(baseChance + revealBonus + sequenceBonus - sequenceRiskPenalty);

            // 예상 배터리/내구도 비용 계산
            float averagePulseCost = (tuning.SonarPulseBatteryCost + tuning.LidarPulseBatteryCost) * 0.5f;
            float batteryCost = harvestModeSession.ScanPulseCount * averagePulseCost
                                + selectedCount * tuning.SelectedPointBatteryCost;

            float durabilityCost = selectedCount * tuning.SelectedPointDurabilityCost
                                   + sequenceRiskPenalty * tuning.RiskPenaltyToDurabilityMultiplier;

            harvestModeSession.SetEstimatedOutcome(finalChance, batteryCost, durabilityCost);

            EventBus.Publish(new HarvestRecoveryPreviewUpdatedEvent(
                harvestModeSession.EstimatedRecoveryChance,
                harvestModeSession.EstimatedBatteryCost,
                harvestModeSession.EstimatedDurabilityCost));
        }

        /// <summary>현재 세션의 회수 결과를 확정하고 배터리/내구도/인벤토리 반영까지 처리한다.</summary>
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

            ItemSO itemData = ResolveTargetItem(harvestTarget);
            if (itemData == null || !itemData.IsValid())
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            // 확정 직전 preview 재계산
            EvaluateRecoveryPlan(harvestModeSession, allPoints);

            float finalChance = harvestModeSession.EstimatedRecoveryChance;
            float batteryCost = harvestModeSession.EstimatedBatteryCost;
            float durabilityCost = harvestModeSession.EstimatedDurabilityCost;

            // 확정 시 예측된 배터리/내구도 비용을 즉시 차감
            submarineRuntimeState.ConsumeBattery(batteryCost);

            if (durabilityCost > 0f)
                submarineRuntimeState.DamageHull(durabilityCost);

            // 배터리 0 이하 강제 종료
            if (submarineRuntimeState.CurrentBattery <= 0f)
            {
                EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(targetData.TargetId));
                EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, false, 0f, false));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, 0f, false));

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, 0f, false, true);
            }

            bool chanceSuccess = Random.value <= finalChance;

            if (!chanceSuccess)
            {
                // 실패 시 페널티: 무게 환산 대신 고정치(혹은 Tuning 값) 사용
                float failureHullDamage = 10f; // 필요시 tuning.FailureHullDamage 등으로 교체하여 사용
                if (failureHullDamage > 0f)
                    submarineRuntimeState.DamageHull(failureHullDamage);

                EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, false, finalChance, false));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, finalChance, false));

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, finalChance, false, false);
            }
            else
            {
                // 성공 시 처리: 인벤토리 자동 적재 폐기, 아이템은 마우스 그랩(Grab) 상태로 넘어간다.
                if (targetData.ConsumeOnSuccess)
                    harvestTarget.Consume();

                // UI에 채집이 성공적으로 끝났음을 알리기 위해 AddedToInventory를 true로 넘김 (실제 적재 여부는 아님)
                EventBus.Publish(new HarvestRecoveryResolvedEvent(itemData.ItemId, true, finalChance, true));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, true, finalChance, true));

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, true, finalChance, true, false);
            }
        }

        /// <summary>현재 target에서 런타임 확정된 실제 아이템을 찾아 반환한다.</summary>
        private ItemSO ResolveTargetItem(IHarvestTarget harvestTarget)
        {
            if (harvestTarget is HarvestTargetBehaviour targetBehaviour)
                return targetBehaviour.GetResolvedItem();

            return null;
        }
    }
}
