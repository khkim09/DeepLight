using Project.Data.World;

namespace Project.Gameplay.World
{
    /// <summary>존 상태 평가를 담당하는 인터페이스</summary>
    public interface IZoneStateEvaluator
    {
        /// <summary>존 상태 평가</summary>
        /// <param name="zoneId">평가할 존 ID</param>
        /// <param name="zoneData">존 데이터 (없을 경우 null)</param>
        /// <param name="progressQuery">진행도 질의 인터페이스</param>
        /// <returns>평가된 존 런타임 상태</returns>
        ZoneRuntimeState EvaluateZoneState(
            ZoneId zoneId,
            ZoneDataSO zoneData,
            IWorldProgressQuery progressQuery);

        /// <summary>존이 현재 해금 가능한지 평가</summary>
        bool IsZoneUnlockable(
            ZoneId zoneId,
            ZoneDataSO zoneData,
            IWorldProgressQuery progressQuery,
            out ZoneLockReason lockReason,
            out System.Collections.Generic.List<UnlockConditionSetSO.ConditionEntry> failedConditions);
    }
}
