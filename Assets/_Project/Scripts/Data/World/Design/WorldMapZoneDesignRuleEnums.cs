namespace Project.Data.World.Design
{
    /// <summary>
    /// Zone 지형 아키타입 - 실제 지형 생성에 사용할 정형화된 지형 분류.
    /// ZoneTerrainMood의 자연어 분위기를 구체적인 생성 규칙으로 변환한 결과.
    /// </summary>
    public enum ZoneTerrainArchetype
    {
        /// <summary>열린 평탄 지형 - 해류가 바닥을 훑는 빈 바다</summary>
        OpenFlat,
        /// <summary>얕은 선반 지형 - 바닥이 살짝 기울며 얕은 패임</summary>
        ShallowShelf,
        /// <summary>완만한 경사 지형 - 첫 지형 변화</summary>
        GentleSlope,
        /// <summary>잔해 지대 - 넓지만 얕은 패임과 돌출</summary>
        DebrisField,
        /// <summary>폐선 집중 지대 - 반쯤 가라앉은 선체 중심</summary>
        WreckField,
        /// <summary>협곡 입구 - 바닥이 두 갈래로 갈라짐</summary>
        CanyonEntry,
        /// <summary>협곡 심부 - 깊은 균열과 좁은 통로</summary>
        CanyonDeep,
        /// <summary>절벽 벽면 - 가파른 경사와 붕괴 가장자리</summary>
        CliffWall,
        /// <summary>인공 통로 - 평탄한 길처럼 보이나 인공성이 느껴짐</summary>
        ArtificialPassage,
        /// <summary>시설 접근 - 연구 장치 흔적이 보이는 구역</summary>
        FacilityApproach,
        /// <summary>희박 압박 - 의도적으로 비어 있는 압박 구간</summary>
        SparsePressure
    }

    /// <summary>
    /// Zone 경로 역할 - 이 Zone이 맵 전체 경로에서 담당하는 역할.
    /// </summary>
    public enum ZoneRouteRole
    {
        /// <summary>자유 탐색 - 특정 경로에 속하지 않음</summary>
        FreeExploration,
        /// <summary>메인 루트 - 주요 진행 경로</summary>
        MainRoute,
        /// <summary>사이드 루트 - 선택적 탐색 경로</summary>
        SideRoute,
        /// <summary>관문 - 진행을 위해 반드시 통과해야 하는 지점</summary>
        Gate,
        /// <summary>경계 - 더 나가면 안 되는 경계선</summary>
        Boundary,
        /// <summary>허브 접근 - 허브로 진입하는 경로</summary>
        HubApproach
    }

    /// <summary>
    /// Zone 랜드마크 역할 - 이 Zone의 핵심 랜드마크/서사 오브젝트 역할.
    /// </summary>
    public enum ZoneLandmarkRole
    {
        /// <summary>없음 - 특별한 랜드마크 없음</summary>
        None,
        /// <summary>허브 - 안전 구역/거점</summary>
        Hub,
        /// <summary>폐선 - 회수 가능한 난파선</summary>
        Wreck,
        /// <summary>연구 포인트 - 연구/기술 단서</summary>
        ResearchPoint,
        /// <summary>통신 단서 - 통신/드론 관련 서사</summary>
        CommunicationClue,
        /// <summary>위험 경고 - 위험을 알리는 경고</summary>
        HazardWarning,
        /// <summary>서사 관문 - 스토리 진행 필수 오브젝트</summary>
        NarrativeGate
    }

    /// <summary>
    /// Zone 충돌 요구사항 - 이 Zone의 Terrain Collider 요구 수준.
    /// </summary>
    public enum ZoneCollisionRequirement
    {
        /// <summary>충돌 불필요 - 순수 시각적 지형</summary>
        None,
        /// <summary>해저면만 - 바닥 충돌만 필요</summary>
        SeafloorOnly,
        /// <summary>해저면 + 대형 소품 - 바닥과 큰 장애물 충돌</summary>
        SeafloorAndLargeProps,
        /// <summary>차단 경계 - 진행을 막는 경계 충돌</summary>
        BlockingBoundary,
        /// <summary>항법 필수 - 정밀 항법에 필요한 충돌</summary>
        NavigationCritical
    }
}
