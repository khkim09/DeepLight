using System.Collections.Generic;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 특정 Zone의 런타임 계산 결과를 캐싱하는 읽기 전용 구조체.
    /// ZoneResolver가 매번 ScenarioPreset을 순회하지 않도록 snapshot dictionary를 만들 수 있게 한다.
    /// </summary>
    public readonly struct ZoneRuntimeSnapshot
    {
        /// <summary>Zone ID</summary>
        public ZoneId ZoneId { get; }

        /// <summary>Zone 중심 월드 좌표</summary>
        public Vector3 Center { get; }

        /// <summary>Zone XZ 평면 Bounds</summary>
        public Bounds HorizontalBounds { get; }

        /// <summary>수면 Y 좌표</summary>
        public float WaterLevelY { get; }

        /// <summary>최소 깊이 (위쪽 경계, 예: -50)</summary>
        public float MinDepth { get; }

        /// <summary>최대 깊이 (아래쪽 경계, 예: -450)</summary>
        public float MaxDepth { get; }

        /// <summary>Region ID</summary>
        public RegionId RegionId { get; }

        /// <summary>바이옴 타입</summary>
        public ZoneBiomeType BiomeType { get; }

        /// <summary>환경 프로필 참조</summary>
        public ZoneEnvironmentProfileSO EnvironmentProfile { get; }

        /// <summary>수심 구간 목록 (Surface ~ Forbidden)</summary>
        public IReadOnlyList<ZoneDepthSlice> DepthSlices { get; }

        /// <summary>
        /// ZoneRuntimeSnapshot 생성자.
        /// </summary>
        public ZoneRuntimeSnapshot(
            ZoneId zoneId,
            Vector3 center,
            Bounds horizontalBounds,
            float waterLevelY,
            float minDepth,
            float maxDepth,
            RegionId regionId,
            ZoneBiomeType biomeType,
            ZoneEnvironmentProfileSO environmentProfile,
            IReadOnlyList<ZoneDepthSlice> depthSlices)
        {
            ZoneId = zoneId;
            Center = center;
            HorizontalBounds = horizontalBounds;
            WaterLevelY = waterLevelY;
            MinDepth = minDepth;
            MaxDepth = maxDepth;
            RegionId = regionId;
            BiomeType = biomeType;
            EnvironmentProfile = environmentProfile;
            DepthSlices = depthSlices ?? new List<ZoneDepthSlice>();
        }

        /// <summary>
        /// 주어진 월드 XZ 좌표가 이 Zone의 수평 Bounds 내에 있는지 확인한다.
        /// epsilon을 사용하여 경계선 근처에서도 true를 반환할 수 있다.
        /// </summary>
        public bool ContainsXZ(Vector3 worldPosition, float epsilon = 0f)
        {
            Bounds expanded = HorizontalBounds;
            expanded.Expand(new Vector3(epsilon, 0f, epsilon));
            Vector3 xzOnly = new Vector3(worldPosition.x, 0f, worldPosition.z);
            return expanded.Contains(xzOnly);
        }
    }
}
