namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimeFinalContentGameplayBinding을 실제 gameplay 시스템에서 어떤 consumer가 소비할 수 있는지
    /// 정의하는 consumer contract 열거형. 실제 Harvest/Hazard/Narrative/Landmark/RouteTrigger 시스템에
    /// 직접 연결하지 않고, "소비 계약/후보 계약"만 metadata로 정의한다.
    /// </summary>
    public enum WorldMapRuntimeGameplayConsumerKind
    {
        /// <summary>초기화되지 않음 또는 미분류</summary>
        None = 0,

        /// <summary>Harvest/Resource 수집 시스템이 소비할 수 있는 contract</summary>
        HarvestResourceConsumer,

        /// <summary>Hazard 위험 요소 volume 시스템이 소비할 수 있는 contract</summary>
        HazardVolumeConsumer,

        /// <summary>Landmark 랜드마크/소품 시스템이 소비할 수 있는 contract</summary>
        LandmarkConsumer,

        /// <summary>NarrativeLog 서사/로그 수집 시스템이 소비할 수 있는 contract</summary>
        NarrativeLogConsumer,

        /// <summary>RouteTrigger 경로 트리거 시스템이 소비할 수 있는 contract</summary>
        RouteTriggerConsumer,

        /// <summary>Debug 디버그 전용 consumer contract</summary>
        DebugConsumer,

        /// <summary>알 수 없음 - 매핑 실패 또는 미분류 consumer</summary>
        Unknown
    }
}
