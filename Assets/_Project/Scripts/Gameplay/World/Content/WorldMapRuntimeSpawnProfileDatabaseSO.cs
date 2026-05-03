using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// 여러 WorldMapRuntimeSpawnProfileEntry를 보관하는 ScriptableObject.
    /// Candidate를 기준으로 가장 적합한 Entry를 찾는 런타임/에디터 공용 API를 제공한다.
    /// MonoBehaviour가 아니므로 runtime/editor 양쪽에서 사용 가능.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapRuntimeSpawnProfileDatabase",
        menuName = "DeepLight/World/Content/Runtime Spawn Profile Database",
        order = 210)]
    public class WorldMapRuntimeSpawnProfileDatabaseSO : ScriptableObject
    {
        [SerializeField, Tooltip("Spawn Profile Entry 목록")]
        private List<WorldMapRuntimeSpawnProfileEntry> _entries = new List<WorldMapRuntimeSpawnProfileEntry>();

        // ===== Public Properties =====

        /// <summary>
        /// 등록된 모든 Spawn Profile Entry의 읽기 전용 리스트.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnProfileEntry> Entries => _entries;

        // ===== Public API =====

        /// <summary>
        /// Entry 리스트를 직접 수정할 수 있도록 내부 리스트를 반환한다.
        /// 에디터 유틸리티에서 entries를 추가/제거할 때 사용한다.
        /// </summary>
        public List<WorldMapRuntimeSpawnProfileEntry> GetEditableEntries()
        {
            return _entries;
        }

        /// <summary>
        /// 주어진 candidate에 가장 적합한 WorldMapRuntimeSpawnProfileEntry를 찾는다.
        /// 매칭 정책:
        /// 1. candidate null이면 false
        /// 2. MarkerType 일치 우선
        /// 3. RuntimeCategory 일치
        /// 4. RuntimeKey 일치
        /// 5. MatchTags 중 하나라도 candidate.SemanticTags와 일치하면 가산점
        /// 6. Priority 높은 Entry 우선
        /// 7. 정확 매칭이 없으면 IsFallbackProfile=true인 entry 중 MarkerType/RuntimeCategory가 가장 가까운 것 선택
        /// 8. 그래도 없으면 false
        /// </summary>
        /// <param name="candidate">매칭 대상 candidate</param>
        /// <param name="entry">매칭 결과 (fallback 포함)</param>
        /// <returns>매칭 성공 시 true, 실패 시 false</returns>
        public bool TryFindBestMatch(WorldMapRuntimeSpawnProfileCandidate candidate, out WorldMapRuntimeSpawnProfileEntry entry)
        {
            entry = null;

            if (candidate == null)
            {
                return false;
            }

            if (_entries == null || _entries.Count == 0)
            {
                return false;
            }

            string normalizedCategory = NormalizeKey(candidate.RuntimeCategory);
            string normalizedKey = NormalizeKey(candidate.RuntimeKey);

            // 1단계: 정확 매칭 (MarkerType + RuntimeCategory + RuntimeKey 모두 일치)
            // Priority 높은 순으로 정렬하여 가장 높은 Priority의 entry 선택
            WorldMapRuntimeSpawnProfileEntry exactMatch = null;
            int exactMatchPriority = int.MinValue;

            foreach (WorldMapRuntimeSpawnProfileEntry e in _entries)
            {
                if (e == null) continue;

                // MarkerType 일치 확인 (Unknown이면 모든 타입 허용)
                bool markerMatch = (e.MarkerType == WorldMapZoneContentMarkerType.Unknown) || (e.MarkerType == candidate.MarkerType);
                if (!markerMatch) continue;

                // RuntimeCategory 일치 확인
                string entryCategory = NormalizeKey(e.RuntimeCategory);
                bool categoryMatch = string.IsNullOrEmpty(entryCategory) || string.Equals(entryCategory, normalizedCategory, StringComparison.OrdinalIgnoreCase);
                if (!categoryMatch) continue;

                // RuntimeKey 일치 확인
                string entryKey = NormalizeKey(e.RuntimeKey);
                bool keyMatch = string.IsNullOrEmpty(entryKey) || string.Equals(entryKey, normalizedKey, StringComparison.OrdinalIgnoreCase);
                if (!keyMatch) continue;

                // 정확 매칭 조건: MarkerType, Category, Key 모두 일치
                if (markerMatch && categoryMatch && keyMatch)
                {
                    // MatchTags 가산점: candidate.SemanticTags와 MatchTags가 하나라도 일치하면 Priority + 1000
                    int effectivePriority = e.Priority;
                    if (HasMatchingTag(e, candidate))
                    {
                        effectivePriority += 1000;
                    }

                    if (effectivePriority > exactMatchPriority)
                    {
                        exactMatchPriority = effectivePriority;
                        exactMatch = e;
                    }
                }
            }

            if (exactMatch != null)
            {
                entry = exactMatch;
                return true;
            }

            // 2단계: fallback 매칭 (IsFallbackProfile=true인 entry 중 MarkerType + RuntimeCategory 기반)
            WorldMapRuntimeSpawnProfileEntry fallbackMatch = null;
            int fallbackPriority = int.MinValue;

            foreach (WorldMapRuntimeSpawnProfileEntry e in _entries)
            {
                if (e == null) continue;
                if (!e.IsFallbackProfile) continue;

                // MarkerType 일치 확인 (Unknown이면 모든 타입 허용)
                bool markerMatch = (e.MarkerType == WorldMapZoneContentMarkerType.Unknown) || (e.MarkerType == candidate.MarkerType);
                if (!markerMatch) continue;

                // RuntimeCategory 일치 확인
                string entryCategory = NormalizeKey(e.RuntimeCategory);
                bool categoryMatch = string.IsNullOrEmpty(entryCategory) || string.Equals(entryCategory, normalizedCategory, StringComparison.OrdinalIgnoreCase);
                if (!categoryMatch) continue;

                // fallback은 RuntimeKey 불일치를 허용하지만, MatchTags 가산점 적용
                int effectivePriority = e.Priority;
                if (HasMatchingTag(e, candidate))
                {
                    effectivePriority += 500;
                }

                if (effectivePriority > fallbackPriority)
                {
                    fallbackPriority = effectivePriority;
                    fallbackMatch = e;
                }
            }

            if (fallbackMatch != null)
            {
                entry = fallbackMatch;
                return true;
            }

            // 3단계: 최종 fallback - IsFallbackProfile=true이고 MarkerType만 일치하는 entry
            foreach (WorldMapRuntimeSpawnProfileEntry e in _entries)
            {
                if (e == null) continue;
                if (!e.IsFallbackProfile) continue;

                // MarkerType 일치 확인 (Unknown이면 모든 타입 허용)
                bool markerMatch = (e.MarkerType == WorldMapZoneContentMarkerType.Unknown) || (e.MarkerType == candidate.MarkerType);
                if (!markerMatch) continue;

                // RuntimeCategory가 비어있으면 모든 category 허용
                string entryCategory = NormalizeKey(e.RuntimeCategory);
                if (!string.IsNullOrEmpty(entryCategory)) continue;

                if (entry == null || e.Priority > entry.Priority)
                {
                    entry = e;
                }
            }

            return entry != null;
        }

        /// <summary>
        /// 주어진 candidate가 매칭될 수 있는 entry의 개수를 반환한다.
        /// </summary>
        /// <param name="candidate">검사할 candidate</param>
        /// <returns>매칭 가능한 entry 개수</returns>
        public int CountMatches(WorldMapRuntimeSpawnProfileCandidate candidate)
        {
            if (candidate == null || _entries == null) return 0;

            string normalizedCategory = NormalizeKey(candidate.RuntimeCategory);
            string normalizedKey = NormalizeKey(candidate.RuntimeKey);
            int count = 0;

            foreach (WorldMapRuntimeSpawnProfileEntry e in _entries)
            {
                if (e == null) continue;

                // MarkerType 일치 확인
                bool markerMatch = (e.MarkerType == WorldMapZoneContentMarkerType.Unknown) || (e.MarkerType == candidate.MarkerType);
                if (!markerMatch) continue;

                // RuntimeCategory 일치 확인
                string entryCategory = NormalizeKey(e.RuntimeCategory);
                bool categoryMatch = string.IsNullOrEmpty(entryCategory) || string.Equals(entryCategory, normalizedCategory, StringComparison.OrdinalIgnoreCase);
                if (!categoryMatch) continue;

                // RuntimeKey 일치 확인
                string entryKey = NormalizeKey(e.RuntimeKey);
                bool keyMatch = string.IsNullOrEmpty(entryKey) || string.Equals(entryKey, normalizedKey, StringComparison.OrdinalIgnoreCase);
                if (!keyMatch) continue;

                count++;
            }

            return count;
        }

        /// <summary>
        /// 중복된 ProfileId가 있는지 검사한다.
        /// </summary>
        /// <param name="duplicateId">발견된 첫 번째 중복 ProfileId</param>
        /// <returns>중복이 있으면 true</returns>
        public bool HasDuplicateProfileIds(out string duplicateId)
        {
            duplicateId = null;

            if (_entries == null || _entries.Count == 0)
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>();

            foreach (WorldMapRuntimeSpawnProfileEntry e in _entries)
            {
                if (e == null) continue;

                string id = NormalizeKey(e.ProfileId);
                if (string.IsNullOrEmpty(id)) continue;

                if (seen.Contains(id))
                {
                    duplicateId = id;
                    return true;
                }

                seen.Add(id);
            }

            return false;
        }

        /// <summary>
        /// 중복된 정확 매칭 키(MarkerType + RuntimeCategory + RuntimeKey 조합)가 있는지 검사한다.
        /// IsFallbackProfile=true인 entry는 검사에서 제외한다.
        /// </summary>
        /// <param name="duplicateKey">발견된 첫 번째 중복 키 요약 문자열</param>
        /// <returns>중복이 있으면 true</returns>
        public bool HasDuplicateExactMatchKeys(out string duplicateKey)
        {
            duplicateKey = null;

            if (_entries == null || _entries.Count == 0)
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>();

            foreach (WorldMapRuntimeSpawnProfileEntry e in _entries)
            {
                if (e == null) continue;
                if (e.IsFallbackProfile) continue;

                string typeStr = e.MarkerType.ToString();
                string category = NormalizeKey(e.RuntimeCategory);
                string key = NormalizeKey(e.RuntimeKey);

                // MarkerType + Category + Key 조합으로 고유 키 생성
                string compositeKey = $"{typeStr}|{category}|{key}";

                if (seen.Contains(compositeKey))
                {
                    duplicateKey = compositeKey;
                    return true;
                }

                seen.Add(compositeKey);
            }

            return false;
        }

        /// <summary>
        /// 특정 RuntimeCategory에 IsFallbackProfile=true인 entry가 존재하는지 확인한다.
        /// </summary>
        /// <param name="runtimeCategory">검사할 RuntimeCategory</param>
        /// <returns>fallback entry가 존재하면 true</returns>
        public bool HasFallbackForCategory(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory) || _entries == null) return false;

            string normalized = NormalizeKey(runtimeCategory);

            foreach (WorldMapRuntimeSpawnProfileEntry e in _entries)
            {
                if (e == null) continue;
                if (!e.IsFallbackProfile) continue;

                string entryCategory = NormalizeKey(e.RuntimeCategory);
                if (string.Equals(entryCategory, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// entry의 MatchTags 중 하나라도 candidate.SemanticTags와 일치하는지 확인한다.
        /// </summary>
        private static bool HasMatchingTag(WorldMapRuntimeSpawnProfileEntry entry, WorldMapRuntimeSpawnProfileCandidate candidate)
        {
            if (entry.MatchTags == null || entry.MatchTags.Length == 0) return false;
            if (candidate.SemanticTags == null || candidate.SemanticTags.Length == 0) return false;

            foreach (string tag in entry.MatchTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                string normalizedTag = NormalizeKey(tag);

                foreach (string semanticTag in candidate.SemanticTags)
                {
                    if (string.IsNullOrEmpty(semanticTag)) continue;
                    string normalizedSemantic = NormalizeKey(semanticTag);

                    if (string.Equals(normalizedTag, normalizedSemantic, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// key 문자열을 정규화한다.
        /// WorldMapRuntimeSpawnProfileResolver.NormalizeKey와 동일한 로직.
        /// 앞뒤 공백 제거, 연속 공백을 단일 공백으로 축소, null/empty 체크.
        /// </summary>
        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            string trimmed = value.Trim();

            while (trimmed.Contains("  "))
            {
                trimmed = trimmed.Replace("  ", " ");
            }

            return trimmed;
        }
    }
}
