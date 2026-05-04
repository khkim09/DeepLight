namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// 최종 맵 content로 어떤 종류의 산출물이 되어야 하는지를 표현하는 열거형.
    /// GameplayAdapterKind보다 상위 개념으로, adapter category를 최종 content contract로 매핑할 때 사용한다.
    /// </summary>
    public enum WorldMapRuntimeFinalContentKind
    {
        /// <summary>초기화되지 않음 또는 미분류</summary>
        None = 0,

        /// <summary>Harvest/Resource 수집 가능 오브젝트 (최종 게임 내 resource)</summary>
        HarvestResource,

        /// <summary>Hazard 위험 요소 volume (전류, 압력 등)</summary>
        HazardVolume,

        /// <summary>Landmark 랜드마크/소품 오브젝트</summary>
        LandmarkProp,

        /// <summary>NarrativeLog 서사/로그 수집 오브젝트</summary>
        NarrativeLog,

        /// <summary>RouteTrigger 경로 트리거</summary>
        RouteTrigger,

        /// <summary>Debug 디버그 전용 마커</summary>
        DebugMarker,

        /// <summary>알 수 없음 - 매핑 실패 또는 미분류 category</summary>
        Unknown
    }
}
