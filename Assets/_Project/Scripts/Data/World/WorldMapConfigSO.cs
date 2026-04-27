using UnityEngine;

namespace Project.Data.World
{
    /// <summary>월드맵 전체 구성 설정 (10x10 grid, 400 unit 기준)</summary>
    [CreateAssetMenu(
        fileName = "WorldMapConfig_",
        menuName = "Project/World/World Map Config")]
    public class WorldMapConfigSO : ScriptableObject
    {
        [Header("Grid Configuration")]
        [SerializeField] private int gridWidth = 10; // 그리드 가로 크기 (열 수)
        [SerializeField] private int gridHeight = 10; // 그리드 세로 크기 (행 수)
        [SerializeField] private float zoneSize = 400f; // 각 존의 크기 (단위: Unity unit)

        [Header("World Bounds")]
        [SerializeField] private float worldMinX = -2000f; // 월드 최소 X 좌표
        [SerializeField] private float worldMaxX = 2000f; // 월드 최대 X 좌표
        [SerializeField] private float worldMinZ = -2000f; // 월드 최소 Z 좌표
        [SerializeField] private float worldMaxZ = 2000f; // 월드 최대 Z 좌표

        [Header("Default Zone Data")]
        [SerializeField] private ZoneDataSO defaultZoneData; // 기본 존 데이터 (존별 데이터가 없을 때 사용)

        /// <summary>그리드 가로 크기 (열 수)</summary>
        public int GridWidth => Mathf.Max(1, gridWidth);

        /// <summary>그리드 세로 크기 (행 수)</summary>
        public int GridHeight => Mathf.Max(1, gridHeight);

        /// <summary>각 존의 크기 (단위: Unity unit)</summary>
        public float ZoneSize => Mathf.Max(1f, zoneSize);

        /// <summary>월드 최소 X 좌표</summary>
        public float WorldMinX => worldMinX;

        /// <summary>월드 최대 X 좌표</summary>
        public float WorldMaxX => worldMaxX;

        /// <summary>월드 최소 Z 좌표</summary>
        public float WorldMinZ => worldMinZ;

        /// <summary>월드 최대 Z 좌표</summary>
        public float WorldMaxZ => worldMaxZ;

        /// <summary>기본 존 데이터</summary>
        public ZoneDataSO DefaultZoneData => defaultZoneData;

        /// <summary>월드 전체 너비 계산</summary>
        public float WorldWidth => worldMaxX - worldMinX;

        /// <summary>월드 전체 높이 계산</summary>
        public float WorldHeight => worldMaxZ - worldMinZ;

        /// <summary>월드 좌표가 월드 경계 내에 있는지 확인</summary>
        public bool IsWorldPositionInBounds(Vector3 worldPosition)
        {
            return worldPosition.x >= worldMinX && worldPosition.x <= worldMaxX &&
                    worldPosition.z >= worldMinZ && worldPosition.z <= worldMaxZ;
        }

        /// <summary>ZoneCoordinate가 그리드 경계 내에 있는지 확인</summary>
        public bool IsZoneCoordinateInBounds(ZoneCoordinate coordinate)
        {
            return coordinate.X >= 0 && coordinate.X < GridWidth &&
                    coordinate.Y >= 0 && coordinate.Y < GridHeight;
        }

        /// <summary>ZoneId가 그리드 경계 내에 있는지 확인</summary>
        public bool IsZoneIdInBounds(ZoneId zoneId)
        {
            ZoneCoordinate coordinate = ZoneCoordinate.FromZoneId(zoneId);
            return IsZoneCoordinateInBounds(coordinate);
        }

        /// <summary>모든 가능한 ZoneId 열거</summary>
        public System.Collections.Generic.IEnumerable<ZoneId> GetAllZoneIds()
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    char column = (char)('A' + x);
                    int row = y + 1;
                    yield return new ZoneId(column, row);
                }
            }
        }

        /// <summary>월드 좌표를 정규화된 그리드 좌표로 변환 (0~1 범위)</summary>
        public Vector2 GetNormalizedGridPosition(Vector3 worldPosition)
        {
            float normalizedX = (worldPosition.x - worldMinX) / WorldWidth;
            float normalizedZ = (worldPosition.z - worldMinZ) / WorldHeight;
            return new Vector2(normalizedX, normalizedZ);
        }
    }
}
