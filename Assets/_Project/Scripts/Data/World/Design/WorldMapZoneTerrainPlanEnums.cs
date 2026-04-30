namespace Project.Data.World.Design
{
    /// <summary>
    /// Zone 지형 표면 모드 - Zone의 전체적인 표면 유형을 정의한다.
    /// Phase 14.3에서 WorldMapZoneDesignRule의 terrainArchetype을 기반으로 결정된다.
    /// </summary>
    public enum ZoneBaseSurfaceMode
    {
        /// <summary>열린 수면 - 해저면이 깊거나 보이지 않는 넓은 수역</summary>
        OpenWater,
        /// <summary>얕은 선반 - 바닥이 얕고 완만한 경사를 가진 선반 지형</summary>
        ShallowShelf,
        /// <summary>해안 선반 - 연안에 가까운 얕은 지형</summary>
        CoastalShelf,
        /// <summary>심해 - 깊은 수심의 해저 지형</summary>
        DeepSea,
        /// <summary>인공 구역 - 인공 구조물이 있는 평탄/가공된 지형</summary>
        ArtificialArea
    }

    /// <summary>
    /// Zone 해저면 형상 모드 - 해저면의 구체적인 3D 형상을 정의한다.
    /// 실제 Terrain mesh 생성 시 이 모드를 기준으로 지형을 변형한다.
    /// </summary>
    public enum ZoneSeabedShapeMode
    {
        /// <summary>평탄 - 완전히 평평한 해저면</summary>
        Flat,
        /// <summary>완만한 경사 - 일정한 방향으로 서서히 깊어짐</summary>
        GentleSlope,
        /// <summary>구릉 해저 - 완만한 언덕과 골짜기가 반복됨</summary>
        RollingSeabed,
        /// <summary>잔해 산재 - 불규칙한 잔해/돌출이 흩어져 있음</summary>
        DebrisScattered,
        /// <summary>폐선 함몰 - 난파선 중심의 움푹 파인 지형</summary>
        WreckDepression,
        /// <summary>협곡 절개 - 바닥이 갈라진 협곡 입구 형태</summary>
        CanyonCut,
        /// <summary>깊은 협곡 - 깊고 좁은 협곡 균열</summary>
        DeepCanyon,
        /// <summary>절벽 낙하 - 급격한 높이 차이의 절벽</summary>
        CliffDrop,
        /// <summary>시설 바닥 - 인공적으로 평탄화된 바닥</summary>
        FacilityFloor,
        /// <summary>희박 공허 - 의도적으로 비어 있는 공간</summary>
        SparseVoid
    }

    /// <summary>
    /// Zone 경로 형상 모드 - 이 Zone의 항법/이동 경로 형태를 정의한다.
    /// </summary>
    public enum ZoneRouteShapeMode
    {
        /// <summary>자유 이동 - 특정 경로 제약 없음</summary>
        FreeRoam,
        /// <summary>넓은 주 경로 - 주요 진행 경로가 넓게 열려 있음</summary>
        WideMainPath,
        /// <summary>좁은 통로 - 좁은 협곡/통로 형태</summary>
        NarrowPassage,
        /// <summary>분기 경로 - 여러 갈래로 나뉘는 경로</summary>
        BranchingPath,
        /// <summary>막다른 단서 - 끝이 막힌 단서/목적지</summary>
        DeadEndClue,
        /// <summary>경계 가장자리 - 맵 경계를 따라 흐르는 경로</summary>
        BoundaryEdge,
        /// <summary>허브 접근 - 허브로 진입하는 경로</summary>
        HubApproach
    }

    /// <summary>
    /// Zone 경계 모드 - 이 Zone의 외부 경계 처리 방식을 정의한다.
    /// </summary>
    public enum ZoneBoundaryMode
    {
        /// <summary>경계 없음 - 자연스러운 전환</summary>
        None,
        /// <summary>부드러운 시각적 경계 - 시각적 페이드/안개로 경계 표시</summary>
        SoftVisualBoundary,
        /// <summary>지형 벽 - 실제 지형 벽으로 경계 생성</summary>
        TerrainWall,
        /// <summary>심압 경계 - 깊이/압력으로 인한 진행 불가 경계</summary>
        DeepPressureBoundary,
        /// <summary>잠긴 서사 경계 - 스토리 진행으로 해제되는 경계</summary>
        LockedNarrativeBoundary
    }

    /// <summary>
    /// Zone 지형 충돌 모드 - 이 Zone의 Collider 적용 방식을 정의한다.
    /// </summary>
    public enum ZoneTerrainColliderMode
    {
        /// <summary>충돌 없음 - 순수 시각적 지형</summary>
        None,
        /// <summary>해저면만 - 바닥 충돌만 적용</summary>
        SeafloorOnly,
        /// <summary>해저면 + 대형 소품 - 바닥과 큰 장애물 충돌</summary>
        SeafloorAndLargeProps,
        /// <summary>항법 필수 - 정밀 항법에 필요한 충돌</summary>
        NavigationCritical,
        /// <summary>하드 경계 - 물리적으로 통과 불가능한 경계</summary>
        HardBoundary
    }
}
