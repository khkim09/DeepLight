using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.World.Design
{
    /// <summary>
    /// 월드맵 존 지형 계획 데이터베이스 ScriptableObject.
    /// WorldMapZoneDesignRule을 실제 지형 생성/배치에 사용할 수 있는
    /// 구체적인 Terrain Plan(WorldMapZoneTerrainPlan) 리스트를 중앙 관리한다.
    ///
    /// Phase 14.3에서 생성되며, Phase 14.4+에서 실제 Terrain mesh 생성/Prop 배치에 사용된다.
    ///
    /// [설계 원칙]
    /// - Runtime-safe: Editor 전용 코드 없음, Runtime에서 읽기 전용 접근 가능
    /// - 확장 가능: plans 리스트에 새 계획 추가만으로 D~J열 확장
    /// - 검증 내장: ValidateUniqueZoneIds() 제공
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapZoneTerrainPlanDatabase",
        menuName = "DeepLight/World/Zone Terrain Plan Database")]
    public class WorldMapZoneTerrainPlanDatabaseSO : ScriptableObject
    {
        [Header("Zone Terrain Plans")]
        [SerializeField] private List<WorldMapZoneTerrainPlan> plans = new List<WorldMapZoneTerrainPlan>();

        /// <summary>모든 존 지형 계획 리스트</summary>
        public List<WorldMapZoneTerrainPlan> Plans => plans;

        /// <summary>
        /// zoneId로 지형 계획을 조회한다.
        /// </summary>
        /// <param name="zoneId">존 ID (예: "A1", "B5")</param>
        /// <returns>일치하는 계획, 없으면 null</returns>
        public WorldMapZoneTerrainPlan GetPlan(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || plans == null)
                return null;

            string upperId = zoneId.ToUpperInvariant();
            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i] != null &&
                    string.Equals(plans[i].zoneId, upperId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return plans[i];
                }
            }
            return null;
        }

        /// <summary>
        /// zoneId로 지형 계획 조회를 시도한다.
        /// </summary>
        /// <param name="zoneId">존 ID (예: "A1", "B5")</param>
        /// <param name="plan">일치하는 계획 (없으면 null)</param>
        /// <returns>계획 존재 시 true</returns>
        public bool TryGetPlan(string zoneId, out WorldMapZoneTerrainPlan plan)
        {
            plan = GetPlan(zoneId);
            return plan != null;
        }

        /// <summary>
        /// 모든 zoneId가 중복 없이 유일한지 검증한다.
        /// </summary>
        /// <returns>중복이 없으면 true</returns>
        public bool ValidateUniqueZoneIds()
        {
            if (plans == null || plans.Count == 0)
                return true;

            var seen = new HashSet<string>();
            bool allUnique = true;

            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i] == null)
                    continue;

                string id = plans[i].zoneId;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[ZoneTerrainPlanDB] Plan at index {i} has null/empty zoneId.", this);
                    allUnique = false;
                    continue;
                }

                string upperId = id.ToUpperInvariant();
                if (seen.Contains(upperId))
                {
                    Debug.LogWarning($"[ZoneTerrainPlanDB] Duplicate zoneId found: '{id}'.", this);
                    allUnique = false;
                }
                else
                {
                    seen.Add(upperId);
                }
            }

            return allUnique;
        }
    }
}
