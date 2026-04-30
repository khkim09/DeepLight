using System;

namespace Project.Data.World.Design
{
    /// <summary>
    /// Zone 디자인 규칙 - WorldMapZoneDesignEntry의 자연어/기획 텍스트를
    /// 정형화된 numeric rule/profile로 변환한 결과.
    /// Phase 14.2에서 생성되며, Phase 14.3+에서 실제 지형/오브젝트 생성에 사용된다.
    ///
    /// [설계 원칙]
    /// - Runtime-safe: Editor 전용 코드 없음, Runtime에서 읽기 전용 접근 가능
    /// - 모든 float 값은 0.0 ~ 1.0 범위로 정규화됨
    /// - debugSummary는 사람이 읽기 쉬운 요약 문자열
    /// </summary>
    [Serializable]
    public class WorldMapZoneDesignRule
    {
        // ===== Identity =====
        /// <summary>존 ID (예: "A1", "B5", "C10")</summary>
        public string zoneId;

        // ===== Terrain Archetype =====
        /// <summary>지형 아키타입 - ZoneTerrainMood에서 변환된 정형 분류</summary>
        public ZoneTerrainArchetype terrainArchetype;

        // ===== Route & Landmark =====
        /// <summary>경로 역할</summary>
        public ZoneRouteRole routeRole;
        /// <summary>랜드마크 역할</summary>
        public ZoneLandmarkRole landmarkRole;
        /// <summary>충돌 요구사항</summary>
        public ZoneCollisionRequirement collisionRequirement;

        // ===== Depth =====
        /// <summary>최소 수심 (음수, 예: -80)</summary>
        public float targetMinDepth;
        /// <summary>최대 수심 (음수, 예: -250)</summary>
        public float targetMaxDepth;

        // ===== Terrain Profile (0.0 ~ 1.0) =====
        /// <summary>지형 거칠기 - 0=평탄, 1=매우 거침</summary>
        public float terrainRoughness01;
        /// <summary>경사 강도 - 0=평지, 1=급경사</summary>
        public float slopeIntensity01;
        /// <summary>협곡 강도 - 0=없음, 1=깊은 협곡</summary>
        public float canyonIntensity01;
        /// <summary>절벽 강도 - 0=없음, 1=수직 절벽</summary>
        public float cliffIntensity01;
        /// <summary>열린 수면 평탄도 - 0=복잡, 1=완전 평탄</summary>
        public float openWaterFlatness01;

        // ===== Content Density (0.0 ~ 1.0) =====
        /// <summary>자원 밀도 - 0=없음, 1=매우 풍부</summary>
        public float resourceDensity01;
        /// <summary>위험 요소 밀도 - 0=안전, 1=매우 위험</summary>
        public float hazardDensity01;
        /// <summary>랜드마크 가중치 - 0=일반, 1=핵심 랜드마크</summary>
        public float landmarkWeight01;
        /// <summary>시각적 강도 - 0=단조, 1=화려한 시각 효과</summary>
        public float visualIntensity01;

        // ===== Flags =====
        /// <summary>Terrain Collider 필요 여부</summary>
        public bool requiresTerrainCollider;
        /// <summary>표면 섬 시각 효과 허용 여부</summary>
        public bool allowsSurfaceIslandVisual;
        /// <summary>서사 오브젝트 필요 여부</summary>
        public bool requiresNarrativeObject;
        /// <summary>의도적으로 빈 공간인지 여부</summary>
        public bool intentionallySparse;

        // ===== Tags =====
        /// <summary>자원 태그 목록 (예: "Iron Scrap", "Sensor Fragment")</summary>
        public string[] resourceTags;
        /// <summary>위험 태그 목록 (예: "current", "pressure", "debris")</summary>
        public string[] hazardTags;
        /// <summary>랜드마크 태그 목록 (예: "wreck", "drone", "research")</summary>
        public string[] landmarkTags;

        // ===== Debug =====
        /// <summary>사람이 읽기 쉬운 디버그 요약</summary>
        public string debugSummary;
    }
}
