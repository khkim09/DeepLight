using System.Collections.Generic;
using Project.Data.World;
using static Project.Data.World.UnlockConditionSetSO;

namespace Project.Gameplay.World
{
    /// <summary>런타임에서의 존 상태 정보</summary>
    public class ZoneRuntimeState
    {
        /// <summary>존 식별자</summary>
        public ZoneId ZoneId { get; }

        /// <summary>존 데이터 참조</summary>
        public ZoneDataSO ZoneData { get; }

        /// <summary>존 접근 가능성 상태</summary>
        public ZoneAccessibility Accessibility { get; set; }

        /// <summary>존 잠금 원인 (잠겨 있을 경우)</summary>
        public ZoneLockReason LockReason { get; set; }

        /// <summary>실패한 조건 목록 (잠금 원인 상세)</summary>
        public List<Project.Data.World.UnlockConditionSetSO.ConditionEntry> FailedConditions { get; } = new();

        /// <summary>현재 위험도 (0-1)</summary>
        public float CurrentRiskLevel { get; set; }

        /// <summary>발견 여부</summary>
        public bool IsDiscovered { get; set; }

        /// <summary>해금 여부</summary>
        public bool IsUnlocked => Accessibility != ZoneAccessibility.Locked;

        /// <summary>위험 존 여부</summary>
        public bool IsRisky => Accessibility == ZoneAccessibility.AccessibleWithRisk;

        /// <summary>안전하게 접근 가능 여부</summary>
        public bool IsSafe => Accessibility == ZoneAccessibility.Accessible;

        /// <summary>ZoneRuntimeState 생성</summary>
        public ZoneRuntimeState(ZoneId zoneId, ZoneDataSO zoneData)
        {
            ZoneId = zoneId;
            ZoneData = zoneData;
            Accessibility = ZoneAccessibility.Locked;
            LockReason = ZoneLockReason.None;
            CurrentRiskLevel = zoneData?.BaseRiskLevel ?? 0f;
            IsDiscovered = false;
        }

        /// <summary>디버그용 상태 문자열 생성</summary>
        public string GetDebugStateString()
        {
            string status = IsUnlocked ? "UNLOCKED" : "LOCKED";
            string risk = CurrentRiskLevel > 0.5f ? "HIGH RISK" : CurrentRiskLevel > 0.2f ? "MEDIUM RISK" : "LOW RISK";
            string discovered = IsDiscovered ? "DISCOVERED" : "UNDISCOVERED";

            return $"{ZoneId}: {status} | {risk} | {discovered} | Reason: {LockReason}";
        }

        /// <summary>실패한 조건 정보 문자열 생성</summary>
        public string GetFailedConditionsString()
        {
            if (FailedConditions.Count == 0)
                return "No failed conditions";

            List<string> conditions = new();
            foreach (ConditionEntry condition in FailedConditions)
            {
                conditions.Add($"{condition.ConditionType}: {condition.ConditionKey} ({condition.RequiredValue})");
            }

            return string.Join("; ", conditions);
        }
    }
}
