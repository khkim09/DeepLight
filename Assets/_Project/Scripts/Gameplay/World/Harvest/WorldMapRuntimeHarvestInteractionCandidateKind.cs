namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// HarvestResource candidate의 runtimeKey를 기반으로 매핑된 상호작용 후보 종류.
    /// 기존 Harvest 시스템이 이 enum을 읽어 어떤 종류의 자원 노드인지 식별할 수 있다.
    /// </summary>
    public enum WorldMapRuntimeHarvestInteractionCandidateKind
    {
        /// <summary>정의되지 않음. 유효하지 않은 후보.</summary>
        None = 0,

        /// <summary>일반 자원 노드 (예: iron, copper)</summary>
        ResourceNode,

        /// <summary>스크랩/고철 노드 (예: Iron Scrap)</summary>
        ScrapNode,

        /// <summary>센서 노드</summary>
        SensorNode,

        /// <summary>데이터 노드</summary>
        DataNode,

        /// <summary>배터리 노드</summary>
        BatteryNode,

        /// <summary>통신 노드</summary>
        CommunicationNode,

        /// <summary>연구 노드</summary>
        ResearchNode,

        /// <summary>알 수 없는 runtimeKey. fallback 후보로 취급 가능.</summary>
        Unknown
    }
}
