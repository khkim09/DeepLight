using System;

namespace Project.Data.World.Design
{
    /// <summary>
    /// 존 디자인 데이터의 단일 엔트리.
    /// 각 존(A1~J10)의 기획 데이터를 구조화하여 저장한다.
    /// 이 데이터는 ScriptableObject에 List 형태로 저장되며,
    /// 이후 Phase 14.2, 14.3에서 D~J열 데이터를 추가할 수 있다.
    /// </summary>
    [Serializable]
    public class WorldMapZoneDesignEntry
    {
        // ===== Zone Identity =====
        /// <summary>존 ID (예: "A1", "B5", "C10")</summary>
        public string zoneId;
        /// <summary>그리드 열 문자 (A-J)</summary>
        public string column;
        /// <summary>그리드 행 번호 (1-10)</summary>
        public int row;
        /// <summary>리전 키 (예: "SouthWestOuterEntry", "WestWreck")</summary>
        public string regionKey;
        /// <summary>바이옴 키 (예: "OpenWater", "ShallowWreck", "Canyon", "ResearchField", "OuterSea")</summary>
        public string biomeKey;

        // ===== Narrative & Mood =====
        /// <summary>서사 단계</summary>
        public ZoneNarrativePhase narrativePhase;
        /// <summary>지형 분위기</summary>
        public ZoneTerrainMood terrainMood;
        /// <summary>위험 등급</summary>
        public ZoneRiskTier riskTier;
        /// <summary>콘텐츠 밀도</summary>
        public ZoneContentDensity contentDensity;
        /// <summary>주요 목적</summary>
        public ZonePrimaryPurpose primaryPurpose;

        // ===== Depth & Risk =====
        /// <summary>최소 수심 (양수, 예: -80)</summary>
        public float minDepth;
        /// <summary>최대 수심 (양수, 예: -250)</summary>
        public float maxDepth;
        /// <summary>기본 위험도 (0.0 ~ 1.0)</summary>
        public float baseRiskLevel;

        // ===== Design Description =====
        /// <summary>지형 설명 (Inspector에서 읽기 쉬운 요약)</summary>
        public string terrainDescription;
        /// <summary>핵심 오브젝트 목록 (쉼표 구분)</summary>
        public string keyObjects;
        /// <summary>자원군 목록 (쉼표 구분)</summary>
        public string resourceGroups;
        /// <summary>로그 또는 서사 힌트 (예: "Log #001", "Research trace begins")</summary>
        public string logOrHint;
        /// <summary>위험 요소 목록 (쉼표 구분)</summary>
        public string hazards;
        /// <summary>서사 기능 설명</summary>
        public string narrativeFunction;

        // ===== Flags =====
        /// <summary>허브 존 여부</summary>
        public bool isHub;
        /// <summary>메이저 랜드마크 여부</summary>
        public bool isMajorLandmark;
        /// <summary>의도적으로 빈 공간인지 여부 (true면 resourceGroups 부족해도 validation 통과)</summary>
        public bool intentionallySparse;
        /// <summary>메모/비고</summary>
        public string notes;
    }
}
