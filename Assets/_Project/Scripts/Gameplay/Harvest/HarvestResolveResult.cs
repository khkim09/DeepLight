namespace Project.Gameplay.Harvest
{
    /// <summary>채집 판정 결과</summary>
    public readonly struct HarvestResolveResult
    {
        public readonly string ItemId; // 대상 아이템 ID
        public readonly bool IsSuccess; // 채집 성공 여부
        public readonly float FinalChance; // 최종 성공 확률
        public readonly bool AddedToInventory; // 인벤토리 적재 성공 여부
        public readonly bool ForcedExitByBattery; // 배터리 방전 종료 여부

        /// <summary>채집 판정 결과 생성</summary>
        public HarvestResolveResult(string itemId, bool isSuccess, float finalChance, bool addedToInventory, bool forcedExitByBattery)
        {
            ItemId = itemId; // 아이템 ID 저장
            IsSuccess = isSuccess; // 성공 여부 저장
            FinalChance = finalChance; // 확률 저장
            AddedToInventory = addedToInventory; // 인벤토리 결과 저장
            ForcedExitByBattery = forcedExitByBattery; // 강제 종료 여부 저장
        }
    }
}
