namespace Project.Gameplay.Harvest
{
    /// <summary>채집 판정 결과와 진단 리포트를 함께 보관한다.</summary>
    public readonly struct HarvestResolveResult
    {
        public readonly string ItemId; // 대상 아이템 ID
        public readonly bool IsSuccess; // 채집 성공 여부
        public readonly float FinalChance; // 최종 성공 확률
        public readonly bool AddedToInventory; // 인벤토리 적재 성공 여부
        public readonly bool ForcedExitByBattery; // 배터리 방전 종료 여부
        public readonly HarvestRecoveryDiagnosticReport DiagnosticReport; // 설명 가능한 판정 리포트

        /// <summary>채집 판정 결과를 생성한다.</summary>
        public HarvestResolveResult(
            string itemId,
            bool isSuccess,
            float finalChance,
            bool addedToInventory,
            bool forcedExitByBattery,
            HarvestRecoveryDiagnosticReport diagnosticReport)
        {
            ItemId = itemId;
            IsSuccess = isSuccess;
            FinalChance = finalChance;
            AddedToInventory = addedToInventory;
            ForcedExitByBattery = forcedExitByBattery;
            DiagnosticReport = diagnosticReport;
        }
    }
}
