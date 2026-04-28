using System;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 현재 적용 중인 시각 상태와 목표 시각 상태를 저장하는 Serializable class.
    /// Debug/Inspector 확인용.
    /// </summary>
    [Serializable]
    public class WorldMapVisualRuntimeState
    {
        [SerializeField] private ZoneId zoneId; // 현재 Zone ID
        [SerializeField] private RegionId regionId; // 현재 Region ID
        [SerializeField] private ZoneBiomeType biomeType; // 현재 바이옴 타입
        [SerializeField] private ZoneDepthBand depthBand; // 현재 수심 대역
        [SerializeField] private float normalizedDepth01; // normalized 깊이 (0~1)
        [SerializeField] private WorldMapVisualProfile targetProfile; // 목표 시각 프로필
        [SerializeField] private WorldMapVisualProfile currentProfile; // 현재 적용 중인 시각 프로필
        [SerializeField] private bool isValid; // 유효 상태

        /// <summary>현재 Zone ID</summary>
        public ZoneId ZoneId => zoneId;
        /// <summary>현재 Region ID</summary>
        public RegionId RegionId => regionId;
        /// <summary>현재 바이옴 타입</summary>
        public ZoneBiomeType BiomeType => biomeType;
        /// <summary>현재 수심 대역</summary>
        public ZoneDepthBand DepthBand => depthBand;
        /// <summary>normalized 깊이 (0~1)</summary>
        public float NormalizedDepth01 => normalizedDepth01;
        /// <summary>목표 시각 프로필</summary>
        public WorldMapVisualProfile TargetProfile => targetProfile;
        /// <summary>현재 적용 중인 시각 프로필</summary>
        public WorldMapVisualProfile CurrentProfile => currentProfile;
        /// <summary>유효 상태</summary>
        public bool IsValid => isValid;

        /// <summary>
        /// 상태를 갱신한다.
        /// </summary>
        public void Update(ZoneDepthSample sample, WorldMapVisualProfile target, WorldMapVisualProfile current)
        {
            zoneId = sample.ZoneId;
            regionId = sample.RegionId;
            biomeType = sample.BiomeType;
            depthBand = sample.DepthBand;
            normalizedDepth01 = sample.NormalizedDepth01;
            targetProfile = target;
            currentProfile = current;
            isValid = true;
        }

        /// <summary>
        /// 현재 profile만 갱신한다.
        /// </summary>
        public void UpdateCurrentProfile(WorldMapVisualProfile current)
        {
            currentProfile = current;
        }

        /// <summary>
        /// 상태를 초기화한다.
        /// </summary>
        public void Reset()
        {
            isValid = false;
        }
    }
}
