using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// ZoneDepthSample을 받아 WorldMapVisualProfile을 계산하는 Resolver.
    /// WorldMapVisualProfileSetSO를 참조하여 biomeType/depthBand/normalizedDepth01 기반으로 평가한다.
    /// </summary>
    public class WorldMapVisualProfileResolver
    {
        private WorldMapVisualProfileSetSO profileSet;

        /// <summary>초기화 완료 여부</summary>
        public bool IsInitialized => profileSet != null;

        /// <summary>
        /// Resolver를 초기화한다.
        /// </summary>
        public void Initialize(WorldMapVisualProfileSetSO profileSet)
        {
            this.profileSet = profileSet;
        }

        /// <summary>
        /// ZoneDepthSample을 기반으로 VisualProfile을 평가한다.
        /// 성공하면 true와 함께 profile을 반환한다.
        /// </summary>
        public bool TryResolve(ZoneDepthSample sample, out WorldMapVisualProfile profile)
        {
            if (profileSet == null)
            {
                profile = GetFallbackProfile(sample);
                return false;
            }

            return profileSet.TryEvaluate(sample.BiomeType, sample.DepthBand, sample.NormalizedDepth01, out profile);
        }

        /// <summary>
        /// ZoneDepthSample을 기반으로 VisualProfile을 평가하거나 fallback을 반환한다.
        /// 항상 유효한 profile을 반환한다.
        /// </summary>
        public WorldMapVisualProfile ResolveOrFallback(ZoneDepthSample sample)
        {
            if (profileSet != null && profileSet.TryEvaluate(sample.BiomeType, sample.DepthBand, sample.NormalizedDepth01, out WorldMapVisualProfile profile))
            {
                profile.ClampValues();
                return profile;
            }

            // Fallback
            WorldMapVisualProfile fallback = GetFallbackProfile(sample);
            fallback.ClampValues();
            return fallback;
        }

        /// <summary>
        /// ZoneDepthSample 기반 Fallback Profile을 생성한다.
        /// </summary>
        public WorldMapVisualProfile GetFallbackProfile(ZoneDepthSample sample)
        {
            return WorldMapVisualProfile.CreateFallback(sample.DepthBand, sample.BiomeType, sample.NormalizedDepth01);
        }
    }
}
