using Project.Data.Harvest;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 대상 런타임 접근을 담당하는 인터페이스</summary>
    public interface IHarvestTarget
    {
        HarvestTargetSO TargetData { get; }
        bool IsAvailable { get; }

        /// <summary>채집 성공 후 대상 상태를 갱신</summary>
        void Consume();

        /// <summary>장애물 충돌 등의 반응을 처리</summary>
        void OnClawCollision();
    }
}
