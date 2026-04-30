using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.World.Design
{
    /// <summary>
    /// 월드맵 존 디자인 규칙 데이터베이스 ScriptableObject.
    /// WorldMapZoneDesignEntry의 자연어/기획 텍스트를 정형화된 numeric rule/profile로
    /// 변환한 WorldMapZoneDesignRule 리스트를 중앙 관리한다.
    ///
    /// Phase 14.2에서 생성되며, Phase 14.3+에서 실제 지형/오브젝트 생성에 사용된다.
    ///
    /// [설계 원칙]
    /// - Runtime-safe: Editor 전용 코드 없음, Runtime에서 읽기 전용 접근 가능
    /// - 확장 가능: rules 리스트에 새 규칙 추가만으로 D~J열 확장
    /// - 검증 내장: ValidateUniqueZoneIds() 제공
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapZoneDesignRuleDatabase",
        menuName = "DeepLight/World/Zone Design Rule Database")]
    public class WorldMapZoneDesignRuleDatabaseSO : ScriptableObject
    {
        [Header("Zone Design Rules")]
        [SerializeField] private List<WorldMapZoneDesignRule> rules = new List<WorldMapZoneDesignRule>();

        /// <summary>모든 존 디자인 규칙 리스트</summary>
        public List<WorldMapZoneDesignRule> Rules => rules;

        /// <summary>
        /// zoneId로 규칙을 조회한다.
        /// </summary>
        /// <param name="zoneId">존 ID (예: "A1", "B5")</param>
        /// <returns>일치하는 규칙, 없으면 null</returns>
        public WorldMapZoneDesignRule GetRule(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || rules == null)
                return null;

            string upperId = zoneId.ToUpperInvariant();
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null &&
                    string.Equals(rules[i].zoneId, upperId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return rules[i];
                }
            }
            return null;
        }

        /// <summary>
        /// zoneId로 규칙 조회를 시도한다.
        /// </summary>
        /// <param name="zoneId">존 ID (예: "A1", "B5")</param>
        /// <param name="rule">일치하는 규칙 (없으면 null)</param>
        /// <returns>규칙 존재 시 true</returns>
        public bool TryGetRule(string zoneId, out WorldMapZoneDesignRule rule)
        {
            rule = GetRule(zoneId);
            return rule != null;
        }

        /// <summary>
        /// 모든 zoneId가 중복 없이 유일한지 검증한다.
        /// </summary>
        /// <returns>중복이 없으면 true</returns>
        public bool ValidateUniqueZoneIds()
        {
            if (rules == null || rules.Count == 0)
                return true;

            var seen = new HashSet<string>();
            bool allUnique = true;

            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null)
                    continue;

                string id = rules[i].zoneId;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[ZoneDesignRuleDB] Rule at index {i} has null/empty zoneId.", this);
                    allUnique = false;
                    continue;
                }

                string upperId = id.ToUpperInvariant();
                if (seen.Contains(upperId))
                {
                    Debug.LogWarning($"[ZoneDesignRuleDB] Duplicate zoneId found: '{id}'.", this);
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
