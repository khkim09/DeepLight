namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimeFinalContentInstances 하위 final content instance의 category별 gameplay binding 종류를 나타내는 열거형.
    /// 실제 Harvest/Hazard/Narrative/Landmark/RouteTrigger 시스템에 직접 연결하지 않고,
    /// gameplay 시스템이 안전하게 소비할 수 있는 category 후보를 식별하기 위한 얇은 binding 계층.
    /// </summary>
    public enum WorldMapRuntimeFinalContentGameplayBindingKind
    {
        /// <summary>초기화되지 않음 또는 미분류</summary>
        None = 0,

        /// <summary>Harvest/Resource 수집 가능 오브젝트 후보</summary>
        HarvestResourceCandidate,

        /// <summary>Hazard 위험 요소 volume 후보</summary>
        HazardVolumeCandidate,

        /// <summary>Landmark 랜드마크/소품 오브젝트 후보</summary>
        LandmarkCandidate,

        /// <summary>NarrativeLog 서사/로그 수집 오브젝트 후보</summary>
        NarrativeLogCandidate,

        /// <summary>RouteTrigger 경로 트리거 후보</summary>
        RouteTriggerCandidate,

        /// <summary>Debug 디버그 전용 마커 후보</summary>
        DebugCandidate,

        /// <summary>알 수 없음 - 매핑 실패 또는 미분류 category</summary>
        Unknown
    }
}
