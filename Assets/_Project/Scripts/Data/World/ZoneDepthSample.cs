using System;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// worldPosition을 넣었을 때 현재 Zone과 현재 수심 band 정보를 한 번에 담는 결과 객체.
    /// Runtime UI, debug, visual controller가 사용 가능하다.
    /// </summary>
    [Serializable]
    public struct ZoneDepthSample
    {
        [SerializeField] private ZoneId zoneId; // 현재 Zone ID
        [SerializeField] private RegionId regionId; // 현재 Region ID
        [SerializeField] private ZoneBiomeType biomeType; // 현재 바이옴 타입
        [SerializeField] private ZoneDepthBand depthBand; // 현재 수심 대역
        [SerializeField] private float y; // 샘플링한 Y 좌표
        [SerializeField] private float normalizedDepth01; // waterLevelY 기준 normalized 깊이 (0~1)
        [SerializeField] private float waterLevelY; // 수면 Y 좌표
        [SerializeField] private float maxDepth; // 최대 깊이
        [SerializeField] private string sourceRuleName; // 출처 Rule 이름
        [SerializeField] private ZoneEnvironmentProfileSO environmentProfile; // 환경 프로필 참조

        /// <summary>현재 Zone ID</summary>
        public ZoneId ZoneId => zoneId;

        /// <summary>현재 Region ID</summary>
        public RegionId RegionId => regionId;

        /// <summary>현재 바이옴 타입</summary>
        public ZoneBiomeType BiomeType => biomeType;

        /// <summary>현재 수심 대역</summary>
        public ZoneDepthBand DepthBand => depthBand;

        /// <summary>샘플링한 Y 좌표</summary>
        public float Y => y;

        /// <summary>waterLevelY 기준 normalized 깊이 (0~1, 0=수면, 1=maxDepth)</summary>
        public float NormalizedDepth01 => normalizedDepth01;

        /// <summary>수면 Y 좌표</summary>
        public float WaterLevelY => waterLevelY;

        /// <summary>최대 깊이</summary>
        public float MaxDepth => maxDepth;

        /// <summary>출처 Rule 이름</summary>
        public string SourceRuleName => sourceRuleName;

        /// <summary>환경 프로필 참조</summary>
        public ZoneEnvironmentProfileSO EnvironmentProfile => environmentProfile;

        /// <summary>
        /// ZoneDepthSample 생성자.
        /// </summary>
        public ZoneDepthSample(ZoneId zoneId, RegionId regionId, ZoneBiomeType biomeType,
            ZoneDepthBand depthBand, float y, float normalizedDepth01,
            float waterLevelY, float maxDepth, string sourceRuleName,
            ZoneEnvironmentProfileSO environmentProfile)
        {
            this.zoneId = zoneId;
            this.regionId = regionId;
            this.biomeType = biomeType;
            this.depthBand = depthBand;
            this.y = y;
            this.normalizedDepth01 = normalizedDepth01;
            this.waterLevelY = waterLevelY;
            this.maxDepth = maxDepth;
            this.sourceRuleName = sourceRuleName ?? "Default";
            this.environmentProfile = environmentProfile;
        }

        /// <summary>
        /// 디버그 문자열을 반환한다.
        /// </summary>
        public string ToDebugString()
        {
            return $"Zone={zoneId}, Region={regionId}, Biome={biomeType}, " +
                   $"DepthBand={depthBand}, Y={y:F1}, Normalized={normalizedDepth01:F3}, " +
                   $"WaterLevel={waterLevelY:F1}, MaxDepth={maxDepth:F1}, Rule={sourceRuleName}";
        }
    }
}
