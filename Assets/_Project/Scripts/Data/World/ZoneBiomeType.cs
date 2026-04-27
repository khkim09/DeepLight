namespace Project.Data.World
{
    /// <summary>
    /// 존의 바이옴(Biome) 타입을 나타내는 열거형.
    /// 각 값은 특정 생태계/지형 스타일과 시각적 테마를 의미한다.
    /// </summary>
    public enum ZoneBiomeType
    {
        /// <summary>중앙 허브/마을 구역</summary>
        Hub,

        /// <summary>얕은 난파선 지대</summary>
        ShallowWreck,

        /// <summary>연구 시설 필드</summary>
        ResearchField,

        /// <summary>봉인된 북부 구역</summary>
        SealedNorth,

        /// <summary>외해/외곽 해역</summary>
        OuterSea,

        /// <summary>해저 협곡</summary>
        Canyon,

        /// <summary>심해 실험 구역</summary>
        AbyssExperiment,

        /// <summary>개방 수역 (기본값)</summary>
        OpenWater
    }
}
