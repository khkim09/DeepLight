using System.Collections.Generic;
using Project.Data.World;

namespace Project.Gameplay.World
{
    /// <summary>
    /// ZoneId 기반 8-이웃(인접) 존 계산 헬퍼.
    ///
    /// 그리드 좌표계에서 현재 존의 상하좌우 및 대각선 8개 이웃을 계산한다.
    /// 경계 밖의 좌표는 자동으로 제외된다.
    ///
    /// [설계 의도]
    /// - 중복되는 그리드-이웃 로직을 한 곳에 모아 유지보수성 향상
    /// - WorldMapConfigSO의 그리드 경계 정보를 사용하여 유효한 이웃만 반환
    /// - ZoneCoordinate 변환을 내부에서 처리하므로 호출자는 ZoneId만 알면 됨
    /// </summary>
    public static class ZoneNeighborHelper
    {
        // 8방향 오프셋: 상, 하, 좌, 우, 좌상, 우상, 좌하, 우하
        private static readonly (int dx, int dy)[] NeighborOffsets =
        {
            (-1, -1), (0, -1), (1, -1), // 위쪽 행
            (-1,  0),          (1,  0), // 같은 행 (좌, 우)
            (-1,  1), (0,  1), (1,  1)  // 아래쪽 행
        };

        /// <summary>
        /// 지정된 ZoneId의 8-이웃 ZoneId 목록을 반환한다.
        /// 그리드 경계를 벗어나는 좌표는 자동으로 제외된다.
        /// </summary>
        /// <param name="zoneId">중심 존 ID</param>
        /// <param name="config">월드맵 설정 (그리드 경계 확인용)</param>
        /// <returns>유효한 이웃 ZoneId 목록 (0~8개)</returns>
        public static List<ZoneId> GetNeighbors(ZoneId zoneId, WorldMapConfigSO config)
        {
            List<ZoneId> neighbors = new List<ZoneId>(8);

            ZoneCoordinate center = ZoneCoordinate.FromZoneId(zoneId);

            foreach (var (dx, dy) in NeighborOffsets)
            {
                int nx = center.X + dx;
                int ny = center.Y + dy;

                ZoneCoordinate neighborCoord = new ZoneCoordinate(nx, ny);

                // 그리드 경계 내에 있는 이웃만 추가
                if (config.IsZoneCoordinateInBounds(neighborCoord))
                {
                    neighbors.Add(neighborCoord.ToZoneId());
                }
            }

            return neighbors;
        }

        /// <summary>
        /// 지정된 ZoneId가 다른 ZoneId의 8-이웃 범위 내에 있는지 확인한다.
        /// </summary>
        /// <param name="targetZoneId">확인할 대상 존 ID</param>
        /// <param name="centerZoneId">중심 존 ID</param>
        /// <param name="config">월드맵 설정</param>
        /// <returns>targetZoneId가 centerZoneId의 이웃이면 true</returns>
        public static bool IsNeighborOf(ZoneId targetZoneId, ZoneId centerZoneId, WorldMapConfigSO config)
        {
            // 자기 자신은 이웃이 아님
            if (targetZoneId.Equals(centerZoneId))
                return false;

            ZoneCoordinate target = ZoneCoordinate.FromZoneId(targetZoneId);
            ZoneCoordinate center = ZoneCoordinate.FromZoneId(centerZoneId);

            int dx = target.X - center.X;
            int dy = target.Y - center.Y;

            // 8-이웃 범위: dx, dy가 각각 -1, 0, 1 중 하나 (0,0 제외)
            return dx >= -1 && dx <= 1 && dy >= -1 && dy <= 1 && (dx != 0 || dy != 0);
        }

        /// <summary>
        /// 지정된 ZoneId가 다른 ZoneId와 같거나 8-이웃 범위 내에 있는지 확인한다.
        /// </summary>
        /// <param name="targetZoneId">확인할 대상 존 ID</param>
        /// <param name="centerZoneId">중심 존 ID</param>
        /// <param name="config">월드맵 설정</param>
        /// <returns>targetZoneId가 centerZoneId와 같거나 이웃이면 true</returns>
        public static bool IsSameOrNeighborOf(ZoneId targetZoneId, ZoneId centerZoneId, WorldMapConfigSO config)
        {
            if (targetZoneId.Equals(centerZoneId))
                return true;

            return IsNeighborOf(targetZoneId, centerZoneId, config);
        }
    }
}
