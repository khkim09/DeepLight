namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Runtime spawned instance의 gameplay category를 나타내는 열거형.
    /// WorldMapRuntimeSpawnGameplayAdapter가 runtimeCategory 문자열을 기반으로
    /// 이 enum 값으로 매핑하여 gameplay 시스템이 안전하게 소비할 수 있도록 한다.
    /// </summary>
    public enum WorldMapRuntimeGameplayAdapterKind
    {
        /// <summary>초기화되지 않은 상태</summary>
        None = 0,
        /// <summary>Harvest/Resource 수집 가능 오브젝트</summary>
        HarvestResource,
        /// <summary>Hazard 위험 요소 (전류, 압력 등)</summary>
        Hazard,
        /// <summary>Landmark 랜드마크 오브젝트</summary>
        Landmark,
        /// <summary>NarrativeLog 서사/로그 오브젝트</summary>
        NarrativeLog,
        /// <summary>RouteTrigger 경로 트리거</summary>
        RouteTrigger,
        /// <summary>Debug 디버그 전용</summary>
        Debug,
        /// <summary>알 수 없음 - 매핑 실패 또는 미분류 category</summary>
        Unknown
    }
}
