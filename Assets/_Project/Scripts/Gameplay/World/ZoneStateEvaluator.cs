using System.Collections.Generic;
using Project.Data.World;
using static Project.Data.World.UnlockConditionSetSO;

namespace Project.Gameplay.World
{
    /// <summary>존 상태 평가를 담당하는 구현체</summary>
    public class ZoneStateEvaluator : IZoneStateEvaluator
    {
        private readonly IUnlockConditionEvaluator _conditionEvaluator;

        /// <summary>ZoneStateEvaluator 생성</summary>
        public ZoneStateEvaluator(IUnlockConditionEvaluator conditionEvaluator)
        {
            _conditionEvaluator = conditionEvaluator ?? new UnlockConditionEvaluator();
        }

        /// <summary>존 상태 평가</summary>
        public ZoneRuntimeState EvaluateZoneState(
            ZoneId zoneId,
            ZoneDataSO zoneData,
            IWorldProgressQuery progressQuery)
        {
            ZoneRuntimeState state = new ZoneRuntimeState(zoneId, zoneData);

            // 진행도 질의 인터페이스가 없으면 기본 잠금 상태
            if (progressQuery == null)
            {
                state.Accessibility = ZoneAccessibility.Locked;
                state.LockReason = ZoneLockReason.Multiple;
                return state;
            }

            // 존 데이터가 없으면 기본 잠금 상태
            if (zoneData == null)
            {
                state.Accessibility = ZoneAccessibility.Locked;
                state.LockReason = ZoneLockReason.Multiple;
                return state;
            }

            // 시작 존은 항상 해금
            if (zoneData.IsAlwaysUnlocked())
            {
                state.Accessibility = ZoneAccessibility.Accessible;
                state.LockReason = ZoneLockReason.None;
                state.IsDiscovered = true;
                return state;
            }

            // 이미 발견된 존 처리
            if (progressQuery.IsZoneDiscovered(zoneId))
            {
                state.IsDiscovered = true;
            }

            // 이미 해금된 존 처리 (캐시)
            if (progressQuery.IsZoneUnlocked(zoneId))
            {
                state.Accessibility = ZoneAccessibility.Accessible;
                state.LockReason = ZoneLockReason.None;
                return state;
            }

            // 해금 조건 평가
            bool isUnlockable = IsZoneUnlockable(
                zoneId, zoneData, progressQuery,
                out ZoneLockReason lockReason, out List<ConditionEntry> failedConditions);

            if (isUnlockable)
            {
                // 위험도에 따른 접근 가능성 결정
                float riskLevel = zoneData.BaseRiskLevel;
                state.CurrentRiskLevel = riskLevel;

                if (riskLevel > 0.7f)
                {
                    state.Accessibility = ZoneAccessibility.AccessibleWithRisk;
                }
                else if (riskLevel > 0.3f)
                {
                    state.Accessibility = ZoneAccessibility.AccessibleWithRisk;
                }
                else
                {
                    state.Accessibility = ZoneAccessibility.Accessible;
                }

                state.LockReason = ZoneLockReason.None;
            }
            else
            {
                state.Accessibility = ZoneAccessibility.Locked;
                state.LockReason = lockReason;
                state.FailedConditions.AddRange(failedConditions);
            }

            return state;
        }

        /// <summary>존이 현재 해금 가능한지 평가</summary>
        public bool IsZoneUnlockable(
            ZoneId zoneId,
            ZoneDataSO zoneData,
            IWorldProgressQuery progressQuery,
            out ZoneLockReason lockReason,
            out List<ConditionEntry> failedConditions)
        {
            lockReason = ZoneLockReason.None;
            failedConditions = new List<ConditionEntry>();

            if (progressQuery == null || zoneData == null)
            {
                lockReason = ZoneLockReason.Multiple;
                return false;
            }

            // 시작 존은 항상 해금 가능
            if (zoneData.IsAlwaysUnlocked())
                return true;

            UnlockConditionSetSO conditionSet = zoneData.UnlockConditionSet;
            if (conditionSet == null || conditionSet.IsEmpty())
            {
                // 조건 세트가 없으면 잠금 (에셋 누락 방지)
                lockReason = ZoneLockReason.Multiple;
                return false;
            }

            // 4축 조건 평가: Traversal, Knowledge, Narrative, Risk
            bool traversalPassed = EvaluateConditionCategory(
                conditionSet.TraversalConditions, progressQuery, failedConditions);

            bool knowledgePassed = EvaluateConditionCategory(
                conditionSet.KnowledgeConditions, progressQuery, failedConditions);

            bool narrativePassed = EvaluateConditionCategory(
                conditionSet.NarrativeConditions, progressQuery, failedConditions);

            bool riskPassed = EvaluateConditionCategory(
                conditionSet.RiskConditions, progressQuery, failedConditions);

            // 실패한 축에 따른 잠금 원인 결정
            int failedCategories = 0;
            if (!traversalPassed) { lockReason = ZoneLockReason.Traversal; failedCategories++; }
            if (!knowledgePassed) { lockReason = ZoneLockReason.Knowledge; failedCategories++; }
            if (!narrativePassed) { lockReason = ZoneLockReason.Narrative; failedCategories++; }
            if (!riskPassed) { lockReason = ZoneLockReason.Risk; failedCategories++; }

            if (failedCategories > 1)
                lockReason = ZoneLockReason.Multiple;

            return traversalPassed && knowledgePassed && narrativePassed && riskPassed;
        }

        /// <summary>조건 카테고리 평가 (해당 카테고리의 모든 조건 통과 필요)</summary>
        private bool EvaluateConditionCategory(
            IReadOnlyList<ConditionEntry> conditions,
            IWorldProgressQuery progressQuery,
            List<ConditionEntry> failedConditions)
        {
            if (conditions.Count == 0)
                return true; // 조건이 없으면 통과

            bool allPassed = true;

            foreach (ConditionEntry condition in conditions)
            {
                bool passed = _conditionEvaluator.EvaluateCondition(
                    condition.ConditionType,
                    condition.ConditionKey,
                    condition.RequiredValue,
                    progressQuery);

                if (!passed)
                {
                    allPassed = false;
                    failedConditions.Add(condition);
                }
            }

            return allPassed;
        }

        /// <summary>디버그용: 존 상태 평가 요약 생성</summary>
        public string GetEvaluationSummary(
            ZoneId zoneId,
            ZoneDataSO zoneData,
            IWorldProgressQuery progressQuery)
        {
            ZoneRuntimeState state = EvaluateZoneState(zoneId, zoneData, progressQuery);

            string summary = $"Zone: {zoneId}\n";
            summary += $"Accessibility: {state.Accessibility}\n";
            summary += $"Lock Reason: {state.LockReason}\n";
            summary += $"Risk Level: {state.CurrentRiskLevel:P0}\n";
            summary += $"Discovered: {state.IsDiscovered}\n";

            if (state.FailedConditions.Count > 0)
            {
                summary += $"Failed Conditions: {state.GetFailedConditionsString()}\n";
            }

            return summary;
        }
    }
}
