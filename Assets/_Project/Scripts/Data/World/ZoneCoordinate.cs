using System;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>월드 좌표계에서의 존 그리드 좌표 (0-based)</summary>
    [Serializable]
    public struct ZoneCoordinate : IEquatable<ZoneCoordinate>
    {
        /// <summary>그리드 X 좌표 (0-based, A=0, B=1, ...)</summary>
        public int X;

        /// <summary>그리드 Y 좌표 (0-based, 1=0, 2=1, ...)</summary>
        public int Y;

        /// <summary>존 좌표 생성</summary>
        public ZoneCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>ZoneId로부터 ZoneCoordinate 생성</summary>
        public static ZoneCoordinate FromZoneId(ZoneId zoneId)
        {
            int x = zoneId.Column - 'A';
            int y = zoneId.Row - 1;
            return new ZoneCoordinate(x, y);
        }

        /// <summary>ZoneCoordinate로부터 ZoneId 생성</summary>
        public ZoneId ToZoneId()
        {
            char column = (char)('A' + X);
            int row = Y + 1;
            return new ZoneId(column, row);
        }

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 (10x10 grid, 400 unit 기준)</summary>
        public static ZoneCoordinate FromWorldPosition(Vector3 worldPosition, WorldMapConfigSO config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // 월드 좌표를 그리드 좌표로 변환
            float gridX = (worldPosition.x - config.WorldMinX) / config.ZoneSize;
            float gridY = (worldPosition.z - config.WorldMinZ) / config.ZoneSize;

            int x = Mathf.FloorToInt(gridX);
            int y = Mathf.FloorToInt(gridY);

            // 경계 체크
            x = Mathf.Clamp(x, 0, config.GridWidth - 1);
            y = Mathf.Clamp(y, 0, config.GridHeight - 1);

            return new ZoneCoordinate(x, y);
        }

        /// <summary>존 중심 월드 좌표 계산</summary>
        public Vector3 GetZoneCenterWorldPosition(WorldMapConfigSO config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            float centerX = config.WorldMinX + (X * config.ZoneSize) + (config.ZoneSize * 0.5f);
            float centerZ = config.WorldMinZ + (Y * config.ZoneSize) + (config.ZoneSize * 0.5f);

            return new Vector3(centerX, 0f, centerZ);
        }

        /// <summary>존 경계 월드 좌표 계산 (min, max)</summary>
        public (Vector3 min, Vector3 max) GetZoneBounds(WorldMapConfigSO config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            float minX = config.WorldMinX + (X * config.ZoneSize);
            float minZ = config.WorldMinZ + (Y * config.ZoneSize);
            float maxX = minX + config.ZoneSize;
            float maxZ = minZ + config.ZoneSize;

            return (new Vector3(minX, -1000f, minZ), new Vector3(maxX, 1000f, maxZ));
        }

        /// <summary>동등성 비교</summary>
        public bool Equals(ZoneCoordinate other) => X == other.X && Y == other.Y;

        /// <summary>동등성 비교</summary>
        public override bool Equals(object obj) => obj is ZoneCoordinate other && Equals(other);

        /// <summary>해시 코드 생성</summary>
        public override int GetHashCode() => HashCode.Combine(X, Y);

        /// <summary>동등 연산자</summary>
        public static bool operator ==(ZoneCoordinate left, ZoneCoordinate right) => left.Equals(right);

        /// <summary>비동등 연산자</summary>
        public static bool operator !=(ZoneCoordinate left, ZoneCoordinate right) => !left.Equals(right);

        /// <summary>문자열 표현</summary>
        public override string ToString() => $"({X},{Y})";
    }
}
