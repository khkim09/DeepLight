using Project.Core.Events;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 실패 원인 분류이다.</summary>
    public enum HarvestFailureReasonType
    {
        None = 0,
        InsufficientScans = 1,
        InsufficientAnchorCount = 2,
        WeakFirstAnchor = 3,
        PoorSequenceBalance = 4,
        LowStructuralStability = 5,
        HighRiskPoints = 6,
        BatteryExhausted = 7
    }

    /// <summary>채집 판정 결과를 설명 가능한 형태로 보관하는 리포트이다.</summary>
    public readonly struct HarvestRecoveryDiagnosticReport
    {
        public readonly string ItemId; // 대상 아이템 ID
        public readonly bool IsSuccess; // 최종 성공 여부
        public readonly bool ForcedExitByBattery; // 배터리 방전 강제 종료 여부
        public readonly float FinalChance; // 최종 성공 확률
        public readonly float OperationBatteryCost; // 정상 운영 배터리 소모량
        public readonly float OperationDurabilityCost; // 정상 운영 내구도 소모량
        public readonly float DangerHullDamage; // 위험 피해로 인한 추가 내구도 손상량
        public readonly int RevealedPointCount; // 공개된 포인트 수
        public readonly int SelectedPointCount; // 실제 선택한 포인트 수
        public readonly int TotalPointCount; // 총 포인트 수
        public readonly float RevealCoverage01; // 공개 비율 점수
        public readonly float SelectionReadiness01; // 선택 준비도 점수
        public readonly float FirstAnchorScore01; // 첫 고정점 점수
        public readonly float SequenceScore01; // 후속 순서 점수
        public readonly float StabilityScore01; // 구조 안정성 점수
        public readonly float RiskScore01; // 구조 위험도 점수
        public readonly HarvestFailureReasonType PrimaryReason; // 주요 실패 원인
        public readonly HarvestFailureReasonType SecondaryReason; // 보조 실패 원인
        public readonly HarvestFailureReasonType TertiaryReason; // 추가 실패 원인
        public readonly string SummaryTitle; // 결과 제목
        public readonly string SummaryBody; // 결과 요약 본문
        public readonly string GuidanceMessage; // 다음 시도 가이드 문구

        /// <summary>진단 리포트를 생성한다.</summary>
        public HarvestRecoveryDiagnosticReport(
            string itemId,
            bool isSuccess,
            bool forcedExitByBattery,
            float finalChance,
            float operationBatteryCost,
            float operationDurabilityCost,
            float dangerHullDamage,
            int revealedPointCount,
            int selectedPointCount,
            int totalPointCount,
            float revealCoverage01,
            float selectionReadiness01,
            float firstAnchorScore01,
            float sequenceScore01,
            float stabilityScore01,
            float riskScore01,
            HarvestFailureReasonType primaryReason,
            HarvestFailureReasonType secondaryReason,
            HarvestFailureReasonType tertiaryReason,
            string summaryTitle,
            string summaryBody,
            string guidanceMessage)
        {
            ItemId = itemId;
            IsSuccess = isSuccess;
            ForcedExitByBattery = forcedExitByBattery;
            FinalChance = finalChance;
            OperationBatteryCost = operationBatteryCost;
            OperationDurabilityCost = operationDurabilityCost;
            DangerHullDamage = dangerHullDamage;
            RevealedPointCount = revealedPointCount;
            SelectedPointCount = selectedPointCount;
            TotalPointCount = totalPointCount;
            RevealCoverage01 = revealCoverage01;
            SelectionReadiness01 = selectionReadiness01;
            FirstAnchorScore01 = firstAnchorScore01;
            SequenceScore01 = sequenceScore01;
            StabilityScore01 = stabilityScore01;
            RiskScore01 = riskScore01;
            PrimaryReason = primaryReason;
            SecondaryReason = secondaryReason;
            TertiaryReason = tertiaryReason;
            SummaryTitle = summaryTitle;
            SummaryBody = summaryBody;
            GuidanceMessage = guidanceMessage;
        }
    }

    /// <summary>채집 판정 진단 리포트를 UI와 HUD에 전달하는 이벤트이다.</summary>
    public readonly struct HarvestRecoveryDiagnosticsEvent : IEvent
    {
        public readonly HarvestRecoveryDiagnosticReport Report; // 설명 가능한 판정 리포트

        /// <summary>진단 리포트 이벤트를 생성한다.</summary>
        public HarvestRecoveryDiagnosticsEvent(HarvestRecoveryDiagnosticReport report)
        {
            Report = report;
        }
    }
}
