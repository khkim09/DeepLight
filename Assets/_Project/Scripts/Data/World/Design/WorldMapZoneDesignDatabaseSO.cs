using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.World.Design
{
    /// <summary>
    /// 월드맵 존 디자인 데이터베이스 ScriptableObject.
    /// 모든 존(A1~J10)의 기획 데이터를 중앙 관리한다.
    ///
    /// Phase 14.1: A/B/C열 30개 존 데이터 포함.
    /// Phase 14.2, 14.3에서 D~J열 데이터를 추가할 수 있도록 확장 가능.
    ///
    /// [설계 원칙]
    /// - Runtime-safe: Editor 전용 코드 없음, Runtime에서 읽기 전용 접근 가능
    /// - 확장 가능: entries 리스트에 새 엔트리 추가만으로 D~J열 확장
    /// - 검증 내장: ValidateUniqueZoneIds(), ValidateRequiredA1ToC10OnlyForPhase141() 제공
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapZoneDesignDatabase",
        menuName = "DeepLight/World/Zone Design Database")]
    public class WorldMapZoneDesignDatabaseSO : ScriptableObject
    {
        [Header("Zone Design Entries")]
        [SerializeField] private List<WorldMapZoneDesignEntry> entries = new List<WorldMapZoneDesignEntry>();

        /// <summary>모든 존 디자인 엔트리 리스트</summary>
        public List<WorldMapZoneDesignEntry> Entries => entries;

        /// <summary>
        /// zoneId로 엔트리를 조회한다.
        /// </summary>
        /// <param name="zoneId">존 ID (예: "A1", "B5")</param>
        /// <returns>일치하는 엔트리, 없으면 null</returns>
        public WorldMapZoneDesignEntry GetEntry(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || entries == null)
                return null;

            string upperId = zoneId.ToUpperInvariant();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null &&
                    string.Equals(entries[i].zoneId, upperId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return entries[i];
                }
            }
            return null;
        }

        /// <summary>
        /// zoneId로 엔트리 조회를 시도한다.
        /// </summary>
        /// <param name="zoneId">존 ID (예: "A1", "B5")</param>
        /// <param name="entry">일치하는 엔트리 (없으면 null)</param>
        /// <returns>엔트리 존재 시 true</returns>
        public bool TryGetEntry(string zoneId, out WorldMapZoneDesignEntry entry)
        {
            entry = GetEntry(zoneId);
            return entry != null;
        }

        /// <summary>
        /// 특정 열(column)의 모든 엔트리를 반환한다.
        /// </summary>
        /// <param name="column">열 문자 (예: "A", "B", "C")</param>
        /// <returns>해당 열의 엔트리 리스트</returns>
        public List<WorldMapZoneDesignEntry> GetEntriesByColumn(string column)
        {
            var result = new List<WorldMapZoneDesignEntry>();
            if (string.IsNullOrEmpty(column) || entries == null)
                return result;

            string upperCol = column.ToUpperInvariant();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null &&
                    string.Equals(entries[i].column, upperCol, System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entries[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// 특정 리전(regionKey)의 모든 엔트리를 반환한다.
        /// </summary>
        /// <param name="regionKey">리전 키</param>
        /// <returns>해당 리전의 엔트리 리스트</returns>
        public List<WorldMapZoneDesignEntry> GetEntriesByRegion(string regionKey)
        {
            var result = new List<WorldMapZoneDesignEntry>();
            if (string.IsNullOrEmpty(regionKey) || entries == null)
                return result;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null &&
                    string.Equals(entries[i].regionKey, regionKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entries[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// 모든 zoneId가 중복 없이 유일한지 검증한다.
        /// </summary>
        /// <returns>중복이 없으면 true</returns>
        public bool ValidateUniqueZoneIds()
        {
            if (entries == null || entries.Count == 0)
                return true;

            var seen = new HashSet<string>();
            bool allUnique = true;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null)
                    continue;

                string id = entries[i].zoneId;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[ZoneDesignDB] Entry at index {i} has null/empty zoneId.", this);
                    allUnique = false;
                    continue;
                }

                string upperId = id.ToUpperInvariant();
                if (seen.Contains(upperId))
                {
                    Debug.LogWarning($"[ZoneDesignDB] Duplicate zoneId found: '{id}'.", this);
                    allUnique = false;
                }
                else
                {
                    seen.Add(upperId);
                }
            }

            return allUnique;
        }

        /// <summary>
        /// Phase 14.1 요구사항: A1~C10 30개 엔트리가 모두 존재하는지 검증한다.
        /// </summary>
        /// <returns>모든 필수 엔트리가 존재하면 true</returns>
        public bool ValidateRequiredA1ToC10OnlyForPhase141()
        {
            if (entries == null)
                return false;

            string[] columns = { "A", "B", "C" };
            bool allPresent = true;

            for (int c = 0; c < columns.Length; c++)
            {
                for (int r = 1; r <= 10; r++)
                {
                    string zoneId = $"{columns[c]}{r}";
                    if (GetEntry(zoneId) == null)
                    {
                        Debug.LogWarning($"[ZoneDesignDB] Phase 14.1 required entry missing: '{zoneId}'.", this);
                        allPresent = false;
                    }
                }
            }

            return allPresent;
        }
    }
}
