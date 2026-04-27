using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>월드 좌표와 존 식별자 간 변환을 담당하는 구현체</summary>
    public class ZoneResolver : IZoneResolver
    {
        private readonly WorldMapConfigSO _config;

        /// <summary>월드맵 설정 참조</summary>
        public WorldMapConfigSO Config => _config;

        /// <summary>ZoneResolver 생성</summary>
        public ZoneResolver(WorldMapConfigSO config)
        {
            _config = config ?? throw new System.ArgumentNullException(nameof(config));
        }

        // Safe APIs

        /// <summary>월드 좌표로부터 ZoneId 계산 시도 (월드 경계 밖이면 false 반환)</summary>
        public bool TryGetZoneIdFromWorldPosition(Vector3 worldPosition, out ZoneId zoneId)
        {
            if (!IsWorldPositionInBounds(worldPosition))
            {
                zoneId = default;
                return false;
            }

            zoneId = GetClampedZoneIdFromWorldPosition(worldPosition);
            return true;
        }

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 시도 (월드 경계 밖이면 false 반환)</summary>
        public bool TryGetZoneCoordinateFromWorldPosition(Vector3 worldPosition, out ZoneCoordinate coordinate)
        {
            if (!IsWorldPositionInBounds(worldPosition))
            {
                coordinate = default;
                return false;
            }

            coordinate = GetClampedZoneCoordinateFromWorldPosition(worldPosition);
            return true;
        }

        // Clamped APIs

        /// <summary>월드 좌표로부터 ZoneId 계산 (월드 경계 밖이면 가장자리 존 반환)</summary>
        public ZoneId GetClampedZoneIdFromWorldPosition(Vector3 worldPosition)
        {
            ZoneCoordinate coordinate = GetClampedZoneCoordinateFromWorldPosition(worldPosition);
            return coordinate.ToZoneId();
        }

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 (월드 경계 밖이면 가장자리 좌표 반환)</summary>
        public ZoneCoordinate GetClampedZoneCoordinateFromWorldPosition(Vector3 worldPosition)
        {
            return ZoneCoordinate.FromWorldPosition(worldPosition, _config);
        }

        // Original APIs (kept for compatibility)

        /// <summary>월드 좌표로부터 ZoneId 계산 (레거시, 월드 경계 밖이면 예외 발생 가능)</summary>
        public ZoneId GetZoneIdFromWorldPosition(Vector3 worldPosition)
        {
            ZoneCoordinate coordinate = GetZoneCoordinateFromWorldPosition(worldPosition);
            return coordinate.ToZoneId();
        }

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 (레거시, 월드 경계 밖이면 예외 발생 가능)</summary>
        public ZoneCoordinate GetZoneCoordinateFromWorldPosition(Vector3 worldPosition)
        {
            return ZoneCoordinate.FromWorldPosition(worldPosition, _config);
        }

        /// <summary>ZoneId로부터 존 중심 월드 좌표 계산</summary>
        public Vector3 GetZoneCenterWorldPosition(ZoneId zoneId)
        {
            ZoneCoordinate coordinate = ZoneCoordinate.FromZoneId(zoneId);
            return coordinate.GetZoneCenterWorldPosition(_config);
        }

        /// <summary>ZoneId로부터 존 경계 월드 좌표 계산 (min, max)</summary>
        public (Vector3 min, Vector3 max) GetZoneBounds(ZoneId zoneId)
        {
            ZoneCoordinate coordinate = ZoneCoordinate.FromZoneId(zoneId);
            return coordinate.GetZoneBounds(_config);
        }

        /// <summary>월드 좌표가 월드 경계 내에 있는지 확인</summary>
        public bool IsWorldPositionInBounds(Vector3 worldPosition)
        {
            return _config.IsWorldPositionInBounds(worldPosition);
        }

        /// <summary>ZoneId가 그리드 경계 내에 있는지 확인</summary>
        public bool IsZoneIdInBounds(ZoneId zoneId)
        {
            return _config.IsZoneIdInBounds(zoneId);
        }

        /// <summary>디버그용: 현재 존 정보 문자열 생성</summary>
        public string GetDebugZoneInfo(Vector3 worldPosition)
        {
            if (!IsWorldPositionInBounds(worldPosition))
                return "Out of bounds";

            ZoneId zoneId = GetZoneIdFromWorldPosition(worldPosition);
            Vector3 center = GetZoneCenterWorldPosition(zoneId);
            (Vector3 min, Vector3 max) bounds = GetZoneBounds(zoneId);

            return $"Zone: {zoneId}, Center: {center}, Bounds: [{bounds.min} - {bounds.max}]";
        }
    }
}
