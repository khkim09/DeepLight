using Project.Data.World;

namespace Project.Gameplay.World.VisualAdapters
{
    /// <summary>
    /// VisualProfile을 실제 렌더링 대상에 적용하는 Adapter의 공통 인터페이스.
    /// WorldMapVisualController가 구체적인 렌더링 구현을 몰라도 Adapter를 호출할 수 있게 한다.
    /// </summary>
    public interface IWorldMapVisualAdapter
    {
        /// <summary>Adapter가 적용 가능한 상태인지 여부</summary>
        bool IsReady { get; }

        /// <summary>Adapter 식별 이름</summary>
        string AdapterName { get; }

        /// <summary>
        /// Adapter를 초기화한다.
        /// Material Instance 생성, Volume Profile 생성 등 준비 작업을 수행한다.
        /// </summary>
        void Initialize();

        /// <summary>
        /// 현재 VisualProfile을 실제 대상에 적용한다.
        /// </summary>
        /// <param name="state">현재 Visual Runtime State (currentProfile 포함)</param>
        /// <param name="deltaTime">프레임 델타 타임</param>
        void ApplyProfile(WorldMapVisualRuntimeState state, float deltaTime);

        /// <summary>
        /// Adapter를 초기 상태로 리셋한다.
        /// Material Instance 정리, Volume Profile 초기화 등.
        /// </summary>
        void ResetAdapter();
    }
}
