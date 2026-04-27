namespace Project.Data.World
{
    /// <summary>
    /// 존의 수심 대역(Depth Band)을 나타내는 열거형.
    /// 각 값은 특정 깊이 범위와 생태/환경 특성을 의미한다.
    /// </summary>
    public enum ZoneDepthBand
    {
        /// <summary>섬/수면/항구권 (0m 부근)</summary>
        Surface,

        /// <summary>얕은 수심 (0 ~ -200m)</summary>
        Shallow,

        /// <summary>중간 수심 (-200 ~ -600m)</summary>
        Mid,

        /// <summary>깊은 수심 (-600 ~ -1200m)</summary>
        Deep,

        /// <summary>금지 구역 (-1200m 이하, 접근 불가)</summary>
        Forbidden
    }
}
