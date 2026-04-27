using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// 월드맵 시나리오 프리셋 ScriptableObject.
    /// 여러 ZoneRule을 포함하여 특정 시나리오(예: "초반 튜토리얼", "중반 연구 단계")에 맞는
    /// 존 환경/위험/바이옴 구성을 정의한다.
    ///
    /// 사용 예:
    /// - ZoneRule 리스트로 각 존의 환경 프로필/위험도/바이옴을 오버라이드
    /// - TryGetRuleForZone()으로 특정 존에 맞는 규칙 조회
    /// - GetEnvironmentProfileOrDefault()로 환경 프로필 조회 (규칙 없으면 defaultOpenWaterProfile 반환)
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapScenario_",
        menuName = "DeepLight/World/World Map Scenario Preset")]
    public class WorldMapScenarioPresetSO : ScriptableObject
    {
        [Header("Zone Rules")]
        [SerializeField] private List<WorldMapScenarioZoneRule> zoneRules = new List<WorldMapScenarioZoneRule>();

        [Header("Defaults")]
        [SerializeField] private ZoneEnvironmentProfileSO defaultOpenWaterProfile; // 규칙에 매칭되지 않은 존의 기본 환경 프로필
        [SerializeField] private RegionId defaultRegionId; // 기본 리전 ID
        [SerializeField] private float defaultRiskLevel = 0.5f; // 기본 위험도
        [SerializeField] private Color defaultDebugColor = Color.gray; // 기본 디버그 색상

        // ===== Public Accessors =====

        /// <summary>존 규칙 리스트</summary>
        public List<WorldMapScenarioZoneRule> ZoneRules => zoneRules;

        /// <summary>기본 개방 수역 환경 프로필</summary>
        public ZoneEnvironmentProfileSO DefaultOpenWaterProfile => defaultOpenWaterProfile;

        /// <summary>기본 리전 ID</summary>
        public RegionId DefaultRegionId => defaultRegionId;

        /// <summary>기본 위험도</summary>
        public float DefaultRiskLevel => defaultRiskLevel;

        /// <summary>기본 디버그 색상</summary>
        public Color DefaultDebugColor => defaultDebugColor;

        // ===== Public Methods =====

        /// <summary>
        /// 주어진 ZoneId에 해당하는 규칙을 찾는다.
        /// zoneRules 리스트를 순회하며 IsZoneMatch()가 true인 첫 번째 규칙을 반환한다.
        /// </summary>
        /// <param name="zoneId">검사할 존 ID</param>
        /// <param name="rule">매칭된 규칙 (없으면 null)</param>
        /// <returns>규칙이 매칭되면 true</returns>
        public bool TryGetRuleForZone(ZoneId zoneId, out WorldMapScenarioZoneRule rule)
        {
            // zoneRules 리스트를 순회하며 매칭되는 규칙 검색
            if (zoneRules != null)
            {
                for (int i = 0; i < zoneRules.Count; i++)
                {
                    WorldMapScenarioZoneRule currentRule = zoneRules[i];
                    if (currentRule != null && currentRule.IsZoneMatch(zoneId))
                    {
                        rule = currentRule;
                        return true;
                    }
                }
            }

            rule = null;
            return false;
        }

        /// <summary>
        /// 주어진 ZoneId에 대한 환경 프로필을 조회한다.
        /// 매칭되는 규칙이 있으면 해당 규칙의 environmentProfile을 반환하고,
        /// 없으면 defaultOpenWaterProfile을 반환한다.
        /// </summary>
        /// <param name="zoneId">검사할 존 ID</param>
        /// <returns>환경 프로필 (없으면 null)</returns>
        public ZoneEnvironmentProfileSO GetEnvironmentProfileOrDefault(ZoneId zoneId)
        {
            // 규칙 매칭 우선
            if (TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
            {
                if (rule.EnvironmentProfile != null)
                    return rule.EnvironmentProfile;
            }

            // 규칙이 없거나 프로필이 null이면 기본값 반환
            return defaultOpenWaterProfile;
        }

        /// <summary>
        /// 주어진 ZoneId에 대한 바이옴 타입을 조회한다.
        /// 매칭되는 규칙이 있으면 해당 규칙의 biomeType을 반환하고,
        /// 없으면 ZoneBiomeType.OpenWater를 반환한다.
        /// </summary>
        /// <param name="zoneId">검사할 존 ID</param>
        /// <returns>바이옴 타입</returns>
        public ZoneBiomeType GetBiomeOrDefault(ZoneId zoneId)
        {
            // 규칙 매칭 우선
            if (TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
            {
                return rule.BiomeType;
            }

            // 규칙이 없으면 기본값 반환
            return ZoneBiomeType.OpenWater;
        }

        /// <summary>
        /// 주어진 ZoneId에 대한 수심 대역을 조회한다.
        /// 매칭되는 규칙이 있으면 해당 규칙의 depthBand를 반환하고,
        /// 없으면 ZoneDepthBand.Shallow를 반환한다.
        /// </summary>
        /// <param name="zoneId">검사할 존 ID</param>
        /// <returns>수심 대역</returns>
        public ZoneDepthBand GetDepthBandOrDefault(ZoneId zoneId)
        {
            // 규칙 매칭 우선
            if (TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
            {
                return rule.DepthBand;
            }

            // 규칙이 없으면 기본값 반환
            return ZoneDepthBand.Shallow;
        }

        /// <summary>
        /// 주어진 ZoneId에 대한 위험도를 조회한다.
        /// 매칭되는 규칙이 있으면 해당 규칙의 baseRiskLevel을 반환하고,
        /// 없으면 defaultRiskLevel을 반환한다.
        /// </summary>
        /// <param name="zoneId">검사할 존 ID</param>
        /// <returns>위험도 (0-1)</returns>
        public float GetRiskOrDefault(ZoneId zoneId)
        {
            // 규칙 매칭 우선
            if (TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
            {
                return rule.BaseRiskLevel;
            }

            // 규칙이 없으면 기본값 반환
            return defaultRiskLevel;
        }
    }
}
