namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Zone Content Marker의 타입을 정의하는 열거형.
    /// 각 marker는 생성 시점에 이 타입에 따라 metadata가 결정된다.
    /// </summary>
    public enum WorldMapZoneContentMarkerType
    {
        /// <summary>자원 스폰 포인트 (ResourceSpawn_*)</summary>
        Resource,
        /// <summary>위험 요소 스폰 포인트 (HazardSpawn_*)</summary>
        Hazard,
        /// <summary>랜드마크 스폰 포인트 (LandmarkSpawn_*)</summary>
        Landmark,
        /// <summary>서사 오브젝트 스폰 포인트 (NarrativeSpawn_*)</summary>
        Narrative,
        /// <summary>경로 마커 (RouteMarker_*)</summary>
        Route,
        /// <summary>디버그 전용 마커 (DebugMarkers 하위)</summary>
        Debug,
        /// <summary>알 수 없음 - 기본값, 유효하지 않은 상태</summary>
        Unknown
    }
}
