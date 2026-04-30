using System;

namespace Project.Data.World.Design
{
    /// <summary>존 서사 단계 - 플레이어가 맵을 진행하며 느껴야 할 서사 흐름</summary>
    public enum ZoneNarrativePhase
    {
        /// <summary>초반 생존/회수 단계 - 아무 일도 없어 보이는 생계형 회수</summary>
        EarlySurvival,
        /// <summary>기술 흔적 전환 단계 - 무언가 이상하다는 감각</summary>
        TransitionTech,
        /// <summary>중반 연구 단계 - 누군가 관리하던 시설의 흔적</summary>
        MidResearch,
        /// <summary>후반 봉인 단계 - 마을 전체가 실험망 안에 있었다는 진실</summary>
        LateSealed,
        /// <summary>최종 핵심 단계 - 진실의 중심</summary>
        EndgameCore,
        /// <summary>의도적으로 비어 있는 압박 구간 - 보상보다 불안 유지</summary>
        EmptyPressure
    }

    /// <summary>존 지형 분위기 - 시각적/공간적 인상 중심</summary>
    public enum ZoneTerrainMood
    {
        /// <summary>평탄한 해류 훑기 - 거의 평탄하지만 해류가 바닥을 길게 훑는 빈 바다</summary>
        FlatCurrentSweep,
        /// <summary>폐선 집중 - 반쯤 가라앉은 선체가 시선 중심</summary>
        WreckFocus,
        /// <summary>잔해 완충 - 넓지만 얕은 패임과 돌출이 있는 잔해 지대</summary>
        DebrisBuffer,
        /// <summary>얕은 사면 - 바닥이 살짝 기울며 첫 지형 변화</summary>
        ShallowSlope,
        /// <summary>협곡 시작 - 바닥이 두 갈래로 갈라짐</summary>
        CanyonStart,
        /// <summary>협곡 접근 - 바닥 균열이 명확하고 진행 속도 자연스럽게 감속</summary>
        CanyonApproach,
        /// <summary>외해 경계 - 넓지만 바닥에 긴 균열선 반복</summary>
        OuterSeaBoundary,
        /// <summary>깊은 사면 - 경사가 분명하고 바닥이 흐트러짐</summary>
        DeepSlope,
        /// <summary>해류 압박 - 해류와 입자 효과로 시야 자주 끊김</summary>
        CurrentPressure,
        /// <summary>붕괴 가장자리 - 가장자리가 무너지듯 내려가는 구조</summary>
        CollapsingEdge,
        /// <summary>낮은 언덕 - 작은 언덕형 암반이 드문드문</summary>
        LowHill,
        /// <summary>열린 평원 - 넓고 낮으며 비어 있는 감각</summary>
        OpenPlain,
        /// <summary>정돈된 해저 - 자연 지형 같지만 인공성이 느껴짐</summary>
        ManagedSeabed,
        /// <summary>인공 통로 - 평탄한 길처럼 보이나 중간에 꺼진 홈</summary>
        ArtificialPassage,
        /// <summary>시설 접근 - 연구 장치 흔적이 보이기 시작</summary>
        FacilityApproach
    }

    /// <summary>존 위험 등급</summary>
    public enum ZoneRiskTier
    {
        /// <summary>안전 - 초반 학습 구간</summary>
        Safe,
        /// <summary>낮은 위험 - 기본 주의</summary>
        Low,
        /// <summary>중간 위험 - 장비 필요</summary>
        Medium,
        /// <summary>높은 위험 - 상당한 준비 필요</summary>
        High,
        /// <summary>금지 구역 - 접근 불가/극도 위험</summary>
        Forbidden
    }

    /// <summary>존 콘텐츠 밀도 - 오브젝트/자원/상호작용 밀집도</summary>
    public enum ZoneContentDensity
    {
        /// <summary>비어 있음 - 의도적으로 빈 공간</summary>
        Empty,
        /// <summary>희박 - 드문드문 배치</summary>
        Sparse,
        /// <summary>보통 - 일반적인 밀도</summary>
        Normal,
        /// <summary>밀집 - 많은 오브젝트/자원</summary>
        Dense,
        /// <summary>랜드마크 - 핵심 장소, 집중 배치</summary>
        Landmark
    }

    /// <summary>존 주요 목적 - 이 존이 게임플레이에서 담당하는 역할</summary>
    public enum ZonePrimaryPurpose
    {
        /// <summary>자원 학습 - 채집/회수 기본 학습</summary>
        ResourceLearning,
        /// <summary>경로 완충 - 이동 동선 연결/정리</summary>
        RouteBuffer,
        /// <summary>폐선 회수 - 핵심 파밍 포인트</summary>
        WreckRecovery,
        /// <summary>기술 전조 - 기술 흔적/이상 징후</summary>
        TechForeshadow,
        /// <summary>거점 지원 - 허브/안전 구역</summary>
        HubSupport,
        /// <summary>연구 단서 - 연구/서사 단서</summary>
        ResearchClue,
        /// <summary>경계 경고 - 더 나가면 안 된다는 감각</summary>
        WarningBoundary,
        /// <summary>압박 구역 - 보상보다 불안/긴장 유지</summary>
        PressureZone,
        /// <summary>서사 관문 - 스토리 진행 필수</summary>
        NarrativeGate
    }
}
