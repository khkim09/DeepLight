using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// Zone + DepthBand 기반 Visual Profile Set을 정의하는 ScriptableObject.
    /// defaultRules와 biomeOverrides를 통해 수심/바이옴별 시각 프로필을 평가한다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapVisualProfileSet_",
        menuName = "DeepLight/World/World Map Visual Profile Set")]
    public class WorldMapVisualProfileSetSO : ScriptableObject
    {
        [Header("Default Rules")]
        [SerializeField] private List<WorldMapDepthVisualRule> defaultRules = new List<WorldMapDepthVisualRule>();

        [Header("Biome Overrides")]
        [SerializeField] private List<BiomeVisualRuleSet> biomeOverrides = new List<BiomeVisualRuleSet>();

        /// <summary>기본 Depth Visual Rule 목록</summary>
        public IReadOnlyList<WorldMapDepthVisualRule> DefaultRules => defaultRules.AsReadOnly();

        /// <summary>바이옴 오버라이드 목록</summary>
        public IReadOnlyList<BiomeVisualRuleSet> BiomeOverrides => biomeOverrides.AsReadOnly();

        /// <summary>
        /// 특정 바이옴에 대한 Depth Visual Rule 목록을 조회한다.
        /// biomeOverrides 중 biomeType이 일치하는 첫 번째 세트를 반환하고,
        /// 없으면 defaultRules를 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapDepthVisualRule> GetRulesForBiome(ZoneBiomeType biomeType)
        {
            // biomeOverrides 우선 검사
            if (biomeOverrides != null)
            {
                for (int i = 0; i < biomeOverrides.Count; i++)
                {
                    if (biomeOverrides[i] != null && biomeOverrides[i].BiomeType == biomeType)
                    {
                        return biomeOverrides[i].Rules;
                    }
                }
            }

            // 없으면 defaultRules 반환
            return defaultRules;
        }

        /// <summary>
        /// 주어진 biomeType, depthBand, normalizedDepth01에 해당하는 VisualProfile을 평가한다.
        /// 평가 우선순위:
        /// 1. biomeOverrides 중 biomeType 일치
        /// 2. defaultRules
        /// 3. fallback profile
        /// </summary>
        public bool TryEvaluate(ZoneBiomeType biomeType, ZoneDepthBand depthBand, float normalizedDepth01, out WorldMapVisualProfile profile)
        {
            // 1. biomeOverrides 우선 검사
            if (biomeOverrides != null)
            {
                for (int i = 0; i < biomeOverrides.Count; i++)
                {
                    if (biomeOverrides[i] != null && biomeOverrides[i].BiomeType == biomeType)
                    {
                        if (TryEvaluateFromRules(biomeOverrides[i].Rules, depthBand, normalizedDepth01, out profile))
                        {
                            return true;
                        }
                    }
                }
            }

            // 2. defaultRules 검사
            if (TryEvaluateFromRules(defaultRules, depthBand, normalizedDepth01, out profile))
            {
                return true;
            }

            // 3. fallback
            profile = WorldMapVisualProfile.CreateFallback(depthBand, biomeType, normalizedDepth01);
            return false;
        }

        /// <summary>
        /// 주어진 rules 목록에서 depthBand와 normalizedDepth01에 맞는 profile을 찾는다.
        /// </summary>
        private static bool TryEvaluateFromRules(List<WorldMapDepthVisualRule> rules, ZoneDepthBand depthBand, float normalizedDepth01, out WorldMapVisualProfile profile)
        {
            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    WorldMapDepthVisualRule rule = rules[i];
                    if (rule != null && rule.DepthBand == depthBand && rule.Contains(normalizedDepth01))
                    {
                        profile = rule.Evaluate(normalizedDepth01);
                        profile.ClampValues();
                        return true;
                    }
                }
            }

            profile = default;
            return false;
        }

        /// <summary>
        /// 바이옴별 Visual Rule Set을 정의하는 Nested Serializable Class.
        /// </summary>
        [System.Serializable]
        public class BiomeVisualRuleSet
        {
            [SerializeField] private ZoneBiomeType biomeType; // 바이옴 타입
            [SerializeField] private List<WorldMapDepthVisualRule> rules = new List<WorldMapDepthVisualRule>(); // Depth Rule 목록

            /// <summary>바이옴 타입</summary>
            public ZoneBiomeType BiomeType => biomeType;

            /// <summary>Depth Rule 목록</summary>
            public List<WorldMapDepthVisualRule> Rules => rules;
        }
    }
}
