using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// 기존 Harvest 시스템이 generated map 기반 harvest 후보를 몰라도
    /// ConsumerService를 통해 후보를 받을 수 있게 하는 optional provider interface.
    /// 이 interface를 통해 기존 Harvest 시스템은 generated map 여부와 무관하게
    /// 동일한 API로 후보를 조회할 수 있다.
    /// </summary>
    public interface IHarvestInteractionTargetProvider
    {
        /// <summary>Provider가 초기화되어 사용 가능한 상태인지 여부</summary>
        bool IsReady { get; }

        /// <summary>Provider가 관리하는 consumer context 총 개수</summary>
        int Count { get; }

        /// <summary>모든 consumer context를 반환한다.</summary>
        IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetAll();

        /// <summary>지정한 ZoneId에 속한 consumer context 목록을 반환한다.</summary>
        IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByZoneId(string zoneId);

        /// <summary>지정한 RuntimeKey에 속한 consumer context 목록을 반환한다.</summary>
        IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByRuntimeKey(string runtimeKey);

        /// <summary>지정한 position에서 가장 가까운 consumer context를 찾는다.</summary>
        bool TryGetNearest(Vector3 position, out WorldMapRuntimeHarvestInteractionTargetConsumerContext context);

        /// <summary>지정한 position에서 가장 가까운, 지정한 runtimeKey를 가진 consumer context를 찾는다.</summary>
        bool TryGetNearest(Vector3 position, string runtimeKey, out WorldMapRuntimeHarvestInteractionTargetConsumerContext context);
    }
}
