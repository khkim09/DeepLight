namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// WorldMapRuntimeHarvestInteractionCandidateKind를 기존 Harvest 시스템이 consume할 수 있는
    /// runtime target 후보 종류로 변환한 enum.
    /// 기존 Harvest 시스템이 이 enum을 읽어 어떤 종류의 harvest target인지 식별할 수 있다.
    /// </summary>
    public enum WorldMapRuntimeHarvestInteractionTargetKind
    {
        /// <summary>정의되지 않음. 유효하지 않은 target.</summary>
        None = 0,

        /// <summary>일반 자원 노드 target (예: iron, copper)</summary>
        ResourceNodeTarget,

        /// <summary>스크랩/고철 노드 target (예: Iron Scrap)</summary>
        ScrapNodeTarget,

        /// <summary>센서 노드 target</summary>
        SensorNodeTarget,

        /// <summary>데이터 노드 target</summary>
        DataNodeTarget,

        /// <summary>배터리 노드 target</summary>
        BatteryNodeTarget,

        /// <summary>통신 노드 target</summary>
        CommunicationNodeTarget,

        /// <summary>연구 노드 target</summary>
        ResearchNodeTarget,

        /// <summary>알 수 없는 runtimeKey. fallback target으로 취급 가능.</summary>
        Unknown
    }
}
