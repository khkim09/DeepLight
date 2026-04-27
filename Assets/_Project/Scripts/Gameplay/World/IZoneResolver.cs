using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>월드 좌표와 존 식별자 간 변환을 담당하는 인터페이스</summary>
    public interface IZoneResolver
    {
        /// <summary>월드맵 설정 참조</summary>
        WorldMapConfigSO Config { get; }

        // Safe APIs (return false if out of bounds)

        /// <summary>월드 좌표로부터 ZoneId 계산 시도 (월드 경계 밖이면 false 반환)</summary>
        bool TryGetZoneIdFromWorldPosition(Vector3 worldPosition, out ZoneId zoneId);

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 시도 (월드 경계 밖이면 false 반환)</summary>
        bool TryGetZoneCoordinateFromWorldPosition(Vector3 worldPosition, out ZoneCoordinate coordinate);

        // Clamped APIs (for debug use, clamp to bounds)

        /// <summary>월드 좌표로부터 ZoneId 계산 (월드 경계 밖이면 가장자리 존 반환)</summary>
        ZoneId GetClampedZoneIdFromWorldPosition(Vector3 worldPosition);

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 (월드 경계 밖이면 가장자리 좌표 반환)</summary>
        ZoneCoordinate GetClampedZoneCoordinateFromWorldPosition(Vector3 worldPosition);

        // Original APIs (kept for compatibility)

        /// <summary>월드 좌표로부터 ZoneId 계산 (레거시, 월드 경계 밖이면 예외 발생 가능)</summary>
        ZoneId GetZoneIdFromWorldPosition(Vector3 worldPosition);

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 (레거시, 월드 경계 밖이면 예외 발생 가능)</summary>
        ZoneCoordinate GetZoneCoordinateFromWorldPosition(Vector3 worldPosition);

        /// <summary>ZoneId로부터 존 중심 월드 좌표 계산</summary>
        Vector3 GetZoneCenterWorldPosition(ZoneId zoneId);

        /// <summary>ZoneId로부터 존 경계 월드 좌표 계산 (min, max)</summary>
        (Vector3 min, Vector3 max) GetZoneBounds(ZoneId zoneId);

        /// <summary>월드 좌표가 월드 경계 내에 있는지 확인</summary>
        bool IsWorldPositionInBounds(Vector3 worldPosition);

        /// <summary>ZoneId가 그리드 경계 내에 있는지 확인</summary>
        bool IsZoneIdInBounds(ZoneId zoneId);

        /// <summary>디버그용: 현재 존 정보 문자열 생성</summary>
        string GetDebugZoneInfo(Vector3 worldPosition);
    }
}
