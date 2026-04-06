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
    /// <summary>회수 성공률, 비용, 실패 사유를 설명 가능한 구조로 계산한다.</summary>
    public class HarvestResolver
    {
        private const float EmptySequenceChanceMultiplier = 0.25f; // 포인트를 하나도 안 잡았을 때 성공 확률 감쇠
        private const float MinimumStableAnchorCount = 3f; // 온전한 회수 축을 잡기 위한 기준 포인트 수
        private const float FailureDangerHullDamageMin = 4f; // 실패 반동 최소 추가 피해
        private const float FailureDangerHullDamageMax = 18f; // 실패 반동 최대 추가 피해

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

            float difficulty = Mathf.Clamp01(itemData.BaseCatchDifficulty + targetData.AdditionalDifficulty);

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

            HarvestRecoveryDiagnosticReport previewReport = BuildDiagnosticReport(
                harvestModeSession,
                allPoints,
                isResolved: false,
                isSuccess: false,
                forcedExitByBattery: false,
                dangerHullDamage: 0f);

            // 세션 내부 추정값 캐시 갱신
            harvestModeSession.SetEstimatedOutcome(
                previewReport.FinalChance,
                previewReport.OperationBatteryCost,
                previewReport.OperationDurabilityCost);

            // 기존 상단 확률/비용 UI와 호환되는 preview 이벤트
            EventBus.Publish(new HarvestRecoveryPreviewUpdatedEvent(
                previewReport.FinalChance,
                previewReport.OperationBatteryCost,
                previewReport.OperationDurabilityCost));

            // 좌측 고정 패널용 요약 지표 이벤트
            EventBus.Publish(new HarvestRecoveryPlanMetricsUpdatedEvent(
                previewReport.RevealedPointCount,
                previewReport.TotalPointCount,
                previewReport.SelectedPointCount,
                Mathf.Min(3, Mathf.Max(1, previewReport.TotalPointCount)),
                previewReport.FirstAnchorScore01,
                previewReport.SequenceScore01,
                previewReport.FinalChance));
        }

        /// <summary>현재 세션의 회수 결과를 확정하고 배터리/내구도/인벤토리 반영까지 처리한다.</summary>
        public HarvestResolveResult ResolveCommittedRecovery(HarvestModeSession harvestModeSession, IReadOnlyList<HarvestScanPoint> allPoints)
        {
            if (harvestModeSession == null || harvestModeSession.CurrentTarget == null)
                return BuildInvalidResult();

            IHarvestTarget harvestTarget = harvestModeSession.CurrentTarget;
            if (!harvestTarget.IsAvailable)
                return BuildInvalidResult();

            HarvestTargetSO targetData = harvestTarget.TargetData;
            if (targetData == null || !targetData.IsValid())
                return BuildInvalidResult();

            ItemSO itemData = ResolveTargetItem(harvestTarget);
            if (itemData == null || !itemData.IsValid())
                return BuildInvalidResult();

            HarvestRecoveryDiagnosticReport previewReport = BuildDiagnosticReport(
                harvestModeSession,
                allPoints,
                isResolved: false,
                isSuccess: false,
                forcedExitByBattery: false,
                dangerHullDamage: 0f);

            // 정상 운영 소모는 경고 연출 없이 차감한다.
            submarineRuntimeState.ConsumeBatteryOperational(previewReport.OperationBatteryCost);

            if (previewReport.OperationDurabilityCost > 0f)
                submarineRuntimeState.DamageHullOperational(previewReport.OperationDurabilityCost);

            if (submarineRuntimeState.CurrentBattery <= 0f)
            {
                HarvestRecoveryDiagnosticReport forcedBatteryReport = BuildDiagnosticReport(
                    harvestModeSession,
                    allPoints,
                    isResolved: true,
                    isSuccess: false,
                    forcedExitByBattery: true,
                    dangerHullDamage: 0f);

                EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(targetData.TargetId));
                PublishResolveEvents(itemData.ItemId, false, 0f, false, forcedBatteryReport);

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, 0f, false, true, forcedBatteryReport);
            }

            const float guaranteedFailureThreshold01 = 0.20f; // 빨간 구간은 실제로도 강제 실패 처리

            bool chanceSuccess = previewReport.FinalChance >= guaranteedFailureThreshold01
                                && Random.value <= previewReport.FinalChance;

            if (!chanceSuccess)
            {
                float dangerHullDamage = EvaluateFailureDangerHullDamage(previewReport);

                if (dangerHullDamage > 0f)
                {
                    submarineRuntimeState.DamageHullDanger(
                        dangerHullDamage,
                        SubmarineDangerFeedbackType.HarvestFailureBacklash,
                        1f);
                }

                HarvestRecoveryDiagnosticReport failureReport = BuildDiagnosticReport(
                    harvestModeSession,
                    allPoints,
                    isResolved: true,
                    isSuccess: false,
                    forcedExitByBattery: false,
                    dangerHullDamage: dangerHullDamage);

                PublishResolveEvents(itemData.ItemId, false, previewReport.FinalChance, false, failureReport);

                harvestModeSession.MarkResolved();
                return new HarvestResolveResult(itemData.ItemId, false, previewReport.FinalChance, false, false, failureReport);
            }

            if (targetData.ConsumeOnSuccess)
                harvestTarget.Consume();

            HarvestRecoveryDiagnosticReport successReport = BuildDiagnosticReport(
                harvestModeSession,
                allPoints,
                isResolved: true,
                isSuccess: true,
                forcedExitByBattery: false,
                dangerHullDamage: 0f);

            PublishResolveEvents(itemData.ItemId, true, previewReport.FinalChance, true, successReport);

            harvestModeSession.MarkResolved();
            return new HarvestResolveResult(itemData.ItemId, true, previewReport.FinalChance, true, false, successReport);
        }

        /// <summary>유효하지 않은 입력에 대한 빈 결과를 생성한다.</summary>
        private HarvestResolveResult BuildInvalidResult()
        {
            HarvestRecoveryDiagnosticReport emptyReport = new HarvestRecoveryDiagnosticReport(
                string.Empty,
                false,
                false,
                0f,
                0f,
                0f,
                0f,
                0,
                0,
                0,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                HarvestFailureReasonType.None,
                HarvestFailureReasonType.None,
                HarvestFailureReasonType.None,
                string.Empty,
                string.Empty,
                string.Empty);

            return new HarvestResolveResult(string.Empty, false, 0f, false, false, emptyReport);
        }

        /// <summary>현재 세션을 설명 가능한 진단 리포트로 변환한다.</summary>
        private HarvestRecoveryDiagnosticReport BuildDiagnosticReport(
            HarvestModeSession harvestModeSession,
            IReadOnlyList<HarvestScanPoint> allPoints,
            bool isResolved,
            bool isSuccess,
            bool forcedExitByBattery,
            float dangerHullDamage)
        {
            string itemId = ResolveTargetItem(harvestModeSession.CurrentTarget)?.ItemId ?? string.Empty;
            float baseChance = EvaluateBaseRecoveryChance(harvestModeSession.CurrentTarget);

            Dictionary<string, HarvestScanPoint> pointMap = BuildPointMap(allPoints);
            List<HarvestScanPoint> selectedPoints = BuildSelectedPoints(harvestModeSession, pointMap);

            int totalPointCount = allPoints != null ? allPoints.Count(point => point != null) : 0;
            int revealedPointCount = harvestModeSession.RevealedPointIds.Count;
            int selectedPointCount = selectedPoints.Count;

            // 스캔 공개율
            float revealCoverage01 = totalPointCount <= 0
                ? 0f
                : Mathf.Clamp01((float)revealedPointCount / totalPointCount);

            // 선택 준비도: 3개 안정 기준에 대한 현재 확보율
            float selectionReadiness01 = totalPointCount <= 0
                ? 0f
                : Mathf.Clamp01(selectedPointCount / Mathf.Min(MinimumStableAnchorCount, totalPointCount));

            HarvestScanPoint firstPoint = selectedPointCount > 0 ? selectedPoints[0] : null;

            // 현재 1번 포인트 점수
            float firstAnchorScore01 = firstPoint == null
                ? 0f
                : Mathf.Clamp01(firstPoint.FirstAnchorBias * 0.7f + firstPoint.BaseStability * 0.3f);

            // 후속 순서 균형 점수
            float sequenceScore01 = 0.5f;
            if (selectedPointCount > 1)
            {
                float sequenceAccumulator = 0f;
                for (int i = 1; i < selectedPoints.Count; i++)
                    sequenceAccumulator += Mathf.Clamp01(selectedPoints[i].SequenceBias * 0.7f + selectedPoints[i].BaseStability * 0.3f);

                sequenceScore01 = Mathf.Clamp01(sequenceAccumulator / (selectedPoints.Count - 1));
            }

            // 전체 구조 안정성 평균
            float stabilityScore01 = selectedPointCount <= 0
                ? 0f
                : Mathf.Clamp01(selectedPoints.Average(point => point.BaseStability));

            // 전체 위험도 평균
            float riskScore01 = selectedPointCount <= 0
                ? 0f
                : Mathf.Clamp01(selectedPoints.Average(point => point.RiskWeight));

            float revealBonus = Mathf.Min(tuning.MaxRevealBonus, revealedPointCount * tuning.RevealBonusPerPoint);

            float firstAnchorBonus = firstPoint == null
                ? 0f
                : firstPoint.FirstAnchorBias * tuning.FirstAnchorBiasMultiplier;

            float sequenceBonus = 0f;
            float stabilityBonus = 0f;
            float totalRiskPenalty = 0f;

            for (int i = 0; i < selectedPoints.Count; i++)
            {
                HarvestScanPoint point = selectedPoints[i];

                if (i > 0)
                    sequenceBonus += point.SequenceBias * tuning.SequenceBiasMultiplier;

                stabilityBonus += point.BaseStability * tuning.BaseStabilityMultiplier;
                totalRiskPenalty += point.RiskWeight * tuning.RiskPenaltyMultiplier;
            }

            float finalChance = Mathf.Clamp01(baseChance + revealBonus + firstAnchorBonus + sequenceBonus + stabilityBonus - totalRiskPenalty);

            if (selectedPointCount <= 0)
            {
                finalChance *= EmptySequenceChanceMultiplier;
            }
            else
            {
                float readinessMultiplier = Mathf.Lerp(0.55f, 1f, selectionReadiness01);
                finalChance *= readinessMultiplier;
            }

            float averagePulseCost = (tuning.SonarPulseBatteryCost + tuning.LidarPulseBatteryCost) * 0.5f;
            float operationBatteryCost = harvestModeSession.ScanPulseCount * averagePulseCost
                                         + selectedPointCount * tuning.SelectedPointBatteryCost;

            float operationDurabilityCost = selectedPointCount * tuning.SelectedPointDurabilityCost
                                            + totalRiskPenalty * tuning.RiskPenaltyToDurabilityMultiplier;

            FailureReasonTriple reasons = ResolveFailureReasons(
                forcedExitByBattery,
                revealCoverage01,
                selectionReadiness01,
                firstAnchorScore01,
                sequenceScore01,
                stabilityScore01,
                riskScore01);

            string summaryTitle = BuildSummaryTitle(isResolved, isSuccess, forcedExitByBattery, finalChance);
            string summaryBody = BuildSummaryBody(
                isResolved,
                isSuccess,
                forcedExitByBattery,
                revealCoverage01,
                selectionReadiness01,
                firstAnchorScore01,
                sequenceScore01,
                stabilityScore01,
                riskScore01,
                dangerHullDamage);
            string guidanceMessage = BuildGuidanceMessage(reasons.Primary, selectionReadiness01);

            return new HarvestRecoveryDiagnosticReport(
                itemId,
                isSuccess,
                forcedExitByBattery,
                finalChance,
                operationBatteryCost,
                operationDurabilityCost,
                dangerHullDamage,
                revealedPointCount,
                selectedPointCount,
                totalPointCount,
                revealCoverage01,
                selectionReadiness01,
                firstAnchorScore01,
                sequenceScore01,
                stabilityScore01,
                riskScore01,
                reasons.Primary,
                reasons.Secondary,
                reasons.Tertiary,
                summaryTitle,
                summaryBody,
                guidanceMessage);
        }

        /// <summary>포인트 목록을 ID 기준 맵으로 변환한다.</summary>
        private Dictionary<string, HarvestScanPoint> BuildPointMap(IReadOnlyList<HarvestScanPoint> allPoints)
        {
            if (allPoints == null)
                return new Dictionary<string, HarvestScanPoint>();

            return allPoints
                .Where(point => point != null && !string.IsNullOrWhiteSpace(point.PointId))
                .GroupBy(point => point.PointId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        /// <summary>세션 슬롯 순서에 맞는 실제 선택 포인트 목록을 만든다.</summary>
        private List<HarvestScanPoint> BuildSelectedPoints(
            HarvestModeSession harvestModeSession,
            IReadOnlyDictionary<string, HarvestScanPoint> pointMap)
        {
            List<HarvestScanPoint> selected = new();
            if (harvestModeSession == null || pointMap == null)
                return selected;

            for (int i = 0; i < harvestModeSession.SelectedPointSequence.Count; i++)
            {
                string pointId = harvestModeSession.SelectedPointSequence[i];
                if (string.IsNullOrWhiteSpace(pointId))
                    continue;

                if (!pointMap.TryGetValue(pointId, out HarvestScanPoint point) || point == null)
                    continue;

                selected.Add(point);
            }

            return selected;
        }

        /// <summary>실패 시 구조 반동으로 인한 추가 내구도 피해를 계산한다.</summary>
        private float EvaluateFailureDangerHullDamage(HarvestRecoveryDiagnosticReport previewReport)
        {
            float severity01 = Mathf.Clamp01(
                (1f - previewReport.FinalChance) * 0.45f
                + previewReport.RiskScore01 * 0.35f
                + (1f - previewReport.StabilityScore01) * 0.20f);

            return Mathf.Lerp(FailureDangerHullDamageMin, FailureDangerHullDamageMax, severity01);
        }

        /// <summary>상태 점수로부터 상위 실패 원인 3개를 뽑는다.</summary>
        private FailureReasonTriple ResolveFailureReasons(
            bool forcedExitByBattery,
            float revealCoverage01,
            float selectionReadiness01,
            float firstAnchorScore01,
            float sequenceScore01,
            float stabilityScore01,
            float riskScore01)
        {
            if (forcedExitByBattery)
            {
                return new FailureReasonTriple(
                    HarvestFailureReasonType.BatteryExhausted,
                    HarvestFailureReasonType.None,
                    HarvestFailureReasonType.None);
            }

            List<ReasonSeverity> severities = new();

            AddSeverity(severities, HarvestFailureReasonType.InsufficientScans, Mathf.Clamp01(0.55f - revealCoverage01));
            AddSeverity(severities, HarvestFailureReasonType.InsufficientAnchorCount, Mathf.Clamp01(0.70f - selectionReadiness01));
            AddSeverity(severities, HarvestFailureReasonType.WeakFirstAnchor, Mathf.Clamp01(0.62f - firstAnchorScore01));
            AddSeverity(severities, HarvestFailureReasonType.PoorSequenceBalance, Mathf.Clamp01(0.58f - sequenceScore01));
            AddSeverity(severities, HarvestFailureReasonType.LowStructuralStability, Mathf.Clamp01(0.60f - stabilityScore01));
            AddSeverity(severities, HarvestFailureReasonType.HighRiskPoints, Mathf.Clamp01(riskScore01 - 0.42f));

            List<ReasonSeverity> ordered = severities
                .OrderByDescending(entry => entry.Severity)
                .ToList();

            HarvestFailureReasonType primary = ordered.Count > 0 ? ordered[0].Reason : HarvestFailureReasonType.None;
            HarvestFailureReasonType secondary = ordered.Count > 1 ? ordered[1].Reason : HarvestFailureReasonType.None;
            HarvestFailureReasonType tertiary = ordered.Count > 2 ? ordered[2].Reason : HarvestFailureReasonType.None;

            return new FailureReasonTriple(primary, secondary, tertiary);
        }

        /// <summary>유효한 심각도만 원인 후보에 추가한다.</summary>
        private void AddSeverity(List<ReasonSeverity> severities, HarvestFailureReasonType reason, float severity)
        {
            if (severity <= 0.001f)
                return;

            severities.Add(new ReasonSeverity(reason, severity));
        }

        /// <summary>결과 제목을 생성한다.</summary>
        private string BuildSummaryTitle(bool isResolved, bool isSuccess, bool forcedExitByBattery, float finalChance)
        {
            if (!isResolved)
                return "Recovery Preview";

            if (forcedExitByBattery)
                return "Recovery Interrupted";

            if (isSuccess)
            {
                if (finalChance >= 0.80f)
                    return "Stable Recovery Success";

                if (finalChance >= 0.55f)
                    return "Recovery Success";

                return "Narrow Recovery Success";
            }

            return "Recovery Failed";
        }

        /// <summary>결과 요약 본문을 생성한다.</summary>
        private string BuildSummaryBody(
            bool isResolved,
            bool isSuccess,
            bool forcedExitByBattery,
            float revealCoverage01,
            float selectionReadiness01,
            float firstAnchorScore01,
            float sequenceScore01,
            float stabilityScore01,
            float riskScore01,
            float dangerHullDamage)
        {
            if (!isResolved)
            {
                return $"Scanned {revealCoverage01 * 100f:0}% · Ready {selectionReadiness01 * 100f:0}% · " +
                       $"Anchor {firstAnchorScore01 * 100f:0}% · Balance {sequenceScore01 * 100f:0}% · " +
                       $"Stability {stabilityScore01 * 100f:0}% · Risk {riskScore01 * 100f:0}%";
            }

            if (forcedExitByBattery)
            {
                return "배터리 잔량이 회수 운영 비용을 버티지 못해 콘솔이 강제 종료되었습니다. 추가 스캔 전에 배터리 여유를 확보해야 합니다.";
            }

            if (isSuccess)
            {
                return "첫 고정점과 후속 균형축이 비교적 안정적으로 맞았고, 위험 포인트가 통제 가능한 수준이어서 회수에 성공했습니다.";
            }

            string backlashText = dangerHullDamage > 0f
                ? $" 구조 반동으로 내구도 {dangerHullDamage:0.0} 추가 손상."
                : string.Empty;

            return "선택한 순서가 하중 중심과 균형축을 충분히 잡지 못해 회수 도중 장력이 무너졌습니다." + backlashText;
        }

        /// <summary>주요 실패 원인에 맞는 다음 시도 가이드를 생성한다.</summary>
        private string BuildGuidanceMessage(HarvestFailureReasonType primaryReason, float selectionReadiness01)
        {
            if (selectionReadiness01 < 0.67f && primaryReason == HarvestFailureReasonType.None)
                primaryReason = HarvestFailureReasonType.InsufficientAnchorCount;

            return primaryReason switch
            {
                HarvestFailureReasonType.InsufficientScans =>
                    "소나와 라이다를 더 사용해 공개 포인트 수를 먼저 늘리세요. 스캔 정보가 늘어날수록 안전한 선택지가 보이고 성공률도 올라갑니다.",

                HarvestFailureReasonType.InsufficientAnchorCount =>
                    "1개만 고정하면 회수 축이 불안정합니다. 최소 2개, 가능하면 3개까지 안정적인 포인트를 확보한 뒤 시도하세요.",

                HarvestFailureReasonType.WeakFirstAnchor =>
                    "1번 포인트는 하중 분산 시작점입니다. 첫 점은 FirstAnchorBias와 BaseStability가 높은 포인트를 우선 배치하세요.",

                HarvestFailureReasonType.PoorSequenceBalance =>
                    "2번 이후 포인트는 균형축과 회수 방향 보정 역할입니다. 후속 포인트는 SequenceBias가 높은 순으로 배치하세요.",

                HarvestFailureReasonType.LowStructuralStability =>
                    "포인트 자체 안정성이 낮았습니다. BaseStability가 높은 포인트를 우선 선택하고, 애매한 포인트는 과감히 제외하세요.",

                HarvestFailureReasonType.HighRiskPoints =>
                    "위험도가 높은 포인트는 확률을 깎고 내구도 손실을 키웁니다. RiskWeight가 높아 보이는 포인트는 뒤로 미루거나 빼는 것이 좋습니다.",

                HarvestFailureReasonType.BatteryExhausted =>
                    "배터리가 많을수록 기본 성공 판정도 유리합니다. 스캔을 줄이거나 귀환 후 재충전한 뒤 재시도하세요.",

                _ =>
                    "기본 원칙은 동일합니다. 스캔으로 공개 수를 늘린 뒤, 1번은 하중 중심, 2번 이후는 균형축, 마지막은 저위험 보정 포인트로 배치하세요."
            };
        }

        /// <summary>현재 target에서 런타임 확정된 실제 아이템을 찾아 반환한다.</summary>
        private ItemSO ResolveTargetItem(IHarvestTarget harvestTarget)
        {
            if (harvestTarget is HarvestTargetBehaviour targetBehaviour)
                return targetBehaviour.GetResolvedItem();

            return null;
        }

        /// <summary>최종 판정 이벤트 묶음을 발행한다.</summary>
        private void PublishResolveEvents(
            string itemId,
            bool isSuccess,
            float finalChance,
            bool addedToInventory,
            HarvestRecoveryDiagnosticReport diagnosticReport)
        {
            EventBus.Publish(new HarvestRecoveryDiagnosticsEvent(diagnosticReport));
            EventBus.Publish(new HarvestRecoveryResolvedEvent(itemId, isSuccess, finalChance, addedToInventory));
            EventBus.Publish(new HarvestAttemptResolvedEvent(itemId, isSuccess, finalChance, addedToInventory));
        }

        /// <summary>실패 원인 3개를 함께 보관하는 묶음 구조체이다.</summary>
        private readonly struct FailureReasonTriple
        {
            public readonly HarvestFailureReasonType Primary;
            public readonly HarvestFailureReasonType Secondary;
            public readonly HarvestFailureReasonType Tertiary;

            public FailureReasonTriple(
                HarvestFailureReasonType primary,
                HarvestFailureReasonType secondary,
                HarvestFailureReasonType tertiary)
            {
                Primary = primary;
                Secondary = secondary;
                Tertiary = tertiary;
            }
        }

        /// <summary>실패 원인과 심각도를 함께 보관한다.</summary>
        private readonly struct ReasonSeverity
        {
            public readonly HarvestFailureReasonType Reason;
            public readonly float Severity;

            public ReasonSeverity(HarvestFailureReasonType reason, float severity)
            {
                Reason = reason;
                Severity = severity;
            }
        }
    }
}
