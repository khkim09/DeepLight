using System;

namespace Project.Data.World.Design
{
    /// <summary>
    /// Zone 지형 계획 - WorldMapZoneDesignRule을 실제 지형 생성/배치에 사용할 수 있는
    /// 구체적인 Terrain Plan으로 변환한 결과.
    /// Phase 14.3에서 생성되며, Phase 14.4+에서 실제 Terrain mesh 생성/Prop 배치에 사용된다.
    ///
    /// [설계 원칙]
    /// - Runtime-safe: Editor 전용 코드 없음, Runtime에서 읽기 전용 접근 가능
    /// - 모든 float 값은 0.0 ~ 1.0 범위로 정규화됨 (depth/floor/ceiling 제외)
    /// - depth/floor/ceiling 값은 world Y 기준 음수 depth 정책 유지
    /// - debugSummary는 사람이 읽기 쉬운 요약 문자열
    /// </summary>
    [Serializable]
    public class WorldMapZoneTerrainPlan
    {
        // ===== Identity =====
        /// <summary>존 ID (예: "A1", "B5", "C10")</summary>
        public string zoneId;

        // ===== Surface & Shape Modes =====
        /// <summary>지형 표면 모드 - 전체적인 표면 유형</summary>
        public ZoneBaseSurfaceMode baseSurfaceMode;
        /// <summary>해저면 형상 모드 - 구체적인 3D 형상</summary>
        public ZoneSeabedShapeMode seabedShapeMode;
        /// <summary>경로 형상 모드 - 항법/이동 경로 형태</summary>
        public ZoneRouteShapeMode routeShapeMode;
        /// <summary>경계 모드 - 외부 경계 처리 방식</summary>
        public ZoneBoundaryMode boundaryMode;
        /// <summary>지형 충돌 모드 - Collider 적용 방식</summary>
        public ZoneTerrainColliderMode colliderMode;

        // ===== Depth (world Y, 음수 depth 정책) =====
        /// <summary>타겟 중앙 수심 (음수, 예: -165)</summary>
        public float targetCenterDepth;
        /// <summary>수심 범위 (양수, 예: 170)</summary>
        public float depthRange;
        /// <summary>바닥 높이 (targetMaxDepth에 가깝게, 음수)</summary>
        public float floorHeight;
        /// <summary>천장 높이 (targetMinDepth에 가깝게, 음수)</summary>
        public float ceilingHeight;

        // ===== Terrain Profile (0.0 ~ 1.0) =====
        /// <summary>경사 스케일 - 0=평지, 1=급경사</summary>
        public float slopeScale;
        /// <summary>거칠기 스케일 - 0=평탄, 1=매우 거침</summary>
        public float roughnessScale;
        /// <summary>협곡 너비 (0~1) - 0=좁음, 1=넓음</summary>
        public float canyonWidth01;
        /// <summary>협곡 깊이 (0~1) - 0=얕음, 1=깊음</summary>
        public float canyonDepth01;
        /// <summary>절벽 높이 (0~1) - 0=낮음, 1=매우 높음</summary>
        public float cliffHeight01;
        /// <summary>평탄 구역 가중치 (0~1) - 0=복잡, 1=완전 평탄</summary>
        public float flatAreaWeight01;

        // ===== Navigation & Content (0.0 ~ 1.0) =====
        /// <summary>항법 통로 너비 (0~1) - 0=좁음, 1=넓음</summary>
        public float navigationCorridorWidth01;
        /// <summary>장애물 밀도 (0~1) - 0=없음, 1=매우 많음</summary>
        public float obstacleDensity01;
        /// <summary>랜드마크 배치 가중치 (0~1) - 0=일반, 1=핵심 랜드마크</summary>
        public float landmarkPlacementWeight01;
        /// <summary>자원 스폰 가중치 (0~1) - 0=없음, 1=매우 풍부</summary>
        public float resourceSpawnWeight01;
        /// <summary>위험 요소 스폰 가중치 (0~1) - 0=안전, 1=매우 위험</summary>
        public float hazardSpawnWeight01;

        // ===== Prop Count Recommendations =====
        /// <summary>권장 대형 소품 개수</summary>
        public int recommendedLargePropCount;
        /// <summary>권장 소형 소품 개수</summary>
        public int recommendedSmallPropCount;

        // ===== Flags =====
        /// <summary>메인 루트 명확성 확보 필요 여부</summary>
        public bool requiresMainRouteClearance;
        /// <summary>경계 블로커 필요 여부</summary>
        public bool requiresBoundaryBlocker;
        /// <summary>해저면 Collider 필요 여부</summary>
        public bool requiresSeafloorCollider;
        /// <summary>장식 전용 표면 허용 여부 (충돌 없는 시각적 지형)</summary>
        public bool allowsDecorativeOnlySurface;

        // ===== Tags =====
        /// <summary>지형 태그 목록 (예: "canyon", "wreck", "research")</summary>
        public string[] terrainTags;
        /// <summary>소품 태그 목록 (예: "wreck_part", "debris", "structure")</summary>
        public string[] propTags;
        /// <summary>위험 태그 목록 (예: "current", "pressure", "debris")</summary>
        public string[] hazardTags;

        // ===== Debug =====
        /// <summary>사람이 읽기 쉬운 디버그 요약</summary>
        public string debugSummary;
    }
}
