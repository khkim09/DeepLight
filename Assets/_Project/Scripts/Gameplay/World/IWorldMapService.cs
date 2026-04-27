using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>런타임 월드맵 서비스 인터페이스 (현재 존 추적 및 상태 질의)</summary>
    public interface IWorldMapService
    {
        /// <summary>현재 플레이어가 위치한 ZoneId</summary>
        ZoneId CurrentZoneId { get; }

        /// <summary>현재 플레이어가 위치한 RegionId</summary>
        RegionId CurrentRegionId { get; }

        /// <summary>현재 존이 유효하게 설정되었는지 여부</summary>
        bool HasCurrentZone { get; }

        /// <summary>서비스 초기화 (초기 월드 위치로 현재 존 설정)</summary>
        /// <param name="initialWorldPosition">초기 월드 위치</param>
        void Initialize(Vector3 initialWorldPosition);

        /// <summary>현재 존 갱신 (월드 위치 기반, 실제 변경 시에만 true 반환)</summary>
        /// <param name="worldPosition">현재 월드 위치</param>
        /// <returns>존이 실제로 변경되었으면 true, 아니면 false</returns>
        bool RefreshCurrentZone(Vector3 worldPosition);

        /// <summary>현재 존의 런타임 상태 조회 시도</summary>
        bool TryGetCurrentZoneState(out ZoneRuntimeState zoneState);

        /// <summary>특정 ZoneId의 런타임 상태 조회 시도</summary>
        bool TryGetZoneState(ZoneId zoneId, out ZoneRuntimeState zoneState);

        /// <summary>특정 ZoneId의 런타임 상태 평가 (데이터 없어도 안전한 폴백 상태 반환)</summary>
        ZoneRuntimeState GetZoneStateOrEvaluate(ZoneId zoneId);

        /// <summary>현재 존이 특정 ZoneId와 일치하는지 확인</summary>
        bool IsCurrentZone(ZoneId zoneId);
    }
}
