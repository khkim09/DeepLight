using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// 여러 WorldMapRuntimeFinalContentRequirementEntry를 보관하는 ScriptableObject.
    /// M-1 contract 기준으로 어떤 final content prefab/profile이 필요한지 requirement slot을 관리한다.
    /// MonoBehaviour가 아니므로 runtime/editor 양쪽에서 사용 가능.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapRuntimeFinalContentRequirementDatabase",
        menuName = "DeepLight/World/Content/Runtime Final Content Requirement Database",
        order = 215)]
    public class WorldMapRuntimeFinalContentRequirementDatabaseSO : ScriptableObject
    {
        [SerializeField, Tooltip("Final Content Requirement Entry 목록")]
        private List<WorldMapRuntimeFinalContentRequirementEntry> _entries = new List<WorldMapRuntimeFinalContentRequirementEntry>();

        // ===== Public Properties =====

        /// <summary>
        /// 등록된 모든 Requirement Entry의 읽기 전용 리스트.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentRequirementEntry> Entries => _entries;

        // ===== Public API =====

        /// <summary>
        /// Entry 리스트를 직접 수정할 수 있도록 내부 리스트를 반환한다.
        /// 에디터 유틸리티에서 entries를 추가/제거할 때 사용한다.
        /// </summary>
        public List<WorldMapRuntimeFinalContentRequirementEntry> GetEditableEntries()
        {
            return _entries;
        }

        /// <summary>
        /// 주어진 contract에 가장 적합한 WorldMapRuntimeFinalContentRequirementEntry를 찾는다.
        /// 매칭 정책:
        /// 1. exact match 우선: finalContentKind + runtimeCategory + runtimeKey + profileId
        /// 2. profileId가 비어있는 runtimeKey match
        /// 3. runtimeCategory fallback
        /// 4. finalContentKind fallback
        /// 5. priority 높은 entry 우선
        /// 6. configured 여부는 matching 성공과 별개로 판단한다.
        ///    즉, finalPrefab/finalProfile이 null이어도 requirement entry는 매칭될 수 있다.
        /// </summary>
        /// <param name="contract">매칭 대상 contract</param>
        /// <param name="entry">매칭 결과 (fallback 포함)</param>
        /// <returns>매칭 성공 시 true, 실패 시 false</returns>
        public bool TryFindBestRequirement(WorldMapRuntimeFinalContentContract contract, out WorldMapRuntimeFinalContentRequirementEntry entry)
        {
            entry = null;

            if (contract == null)
            {
                return false;
            }

            if (_entries == null || _entries.Count == 0)
            {
                return false;
            }

            // 1단계: exact match (finalContentKind + runtimeCategory + runtimeKey + profileId 모두 일치)
            WorldMapRuntimeFinalContentRequirementEntry exactMatch = null;
            int exactMatchPriority = int.MinValue;

            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;
                if (e.IsFallbackRequirement) continue; // fallback은 exact match에서 제외

                // Matches()로 기본 매칭 확인
                if (!e.Matches(contract))
                    continue;

                // exact match 조건: runtimeCategory, runtimeKey, profileId 모두 비어있지 않고 일치해야 함
                bool hasCategory = !string.IsNullOrEmpty(e.RuntimeCategory);
                bool hasKey = !string.IsNullOrEmpty(e.RuntimeKey);
                bool hasProfileId = !string.IsNullOrEmpty(e.ProfileId);

                if (!hasCategory || !hasKey || !hasProfileId)
                    continue;

                // 모든 조건 만족 = exact match
                if (e.Priority > exactMatchPriority)
                {
                    exactMatchPriority = e.Priority;
                    exactMatch = e;
                }
            }

            if (exactMatch != null)
            {
                entry = exactMatch;
                return true;
            }

            // 2단계: profileId가 비어있는 runtimeKey match (finalContentKind + runtimeCategory + runtimeKey 일치, profileId 무시)
            WorldMapRuntimeFinalContentRequirementEntry keyMatch = null;
            int keyMatchPriority = int.MinValue;

            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;
                if (e.IsFallbackRequirement) continue;

                // finalContentKind 일치
                if (e.FinalContentKind != contract.FinalContentKind)
                    continue;

                // runtimeCategory 일치 (비어있으면 skip)
                if (!string.IsNullOrEmpty(e.RuntimeCategory))
                {
                    if (!string.Equals(e.RuntimeCategory, contract.RuntimeCategory, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // runtimeKey 일치 (비어있으면 skip)
                if (!string.IsNullOrEmpty(e.RuntimeKey))
                {
                    if (!string.Equals(e.RuntimeKey, contract.RuntimeKey, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // profileId가 비어있어야 함 (이 단계의 조건)
                if (!string.IsNullOrEmpty(e.ProfileId))
                    continue;

                if (e.Priority > keyMatchPriority)
                {
                    keyMatchPriority = e.Priority;
                    keyMatch = e;
                }
            }

            if (keyMatch != null)
            {
                entry = keyMatch;
                return true;
            }

            // 3단계: runtimeCategory fallback (finalContentKind + runtimeCategory 일치, runtimeKey/profileId 무시)
            WorldMapRuntimeFinalContentRequirementEntry categoryMatch = null;
            int categoryMatchPriority = int.MinValue;

            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;
                if (e.IsFallbackRequirement) continue;

                // finalContentKind 일치
                if (e.FinalContentKind != contract.FinalContentKind)
                    continue;

                // runtimeCategory 일치 (비어있으면 모든 category 허용)
                if (!string.IsNullOrEmpty(e.RuntimeCategory))
                {
                    if (!string.Equals(e.RuntimeCategory, contract.RuntimeCategory, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // runtimeKey가 비어있어야 함 (category fallback 조건)
                if (!string.IsNullOrEmpty(e.RuntimeKey))
                    continue;

                if (e.Priority > categoryMatchPriority)
                {
                    categoryMatchPriority = e.Priority;
                    categoryMatch = e;
                }
            }

            if (categoryMatch != null)
            {
                entry = categoryMatch;
                return true;
            }

            // 4단계: finalContentKind fallback (IsFallbackRequirement=true이고 finalContentKind 일치)
            WorldMapRuntimeFinalContentRequirementEntry fallbackMatch = null;
            int fallbackPriority = int.MinValue;

            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;
                if (!e.IsFallbackRequirement) continue;
                if (!e.AllowFallbackUse) continue;

                // finalContentKind 일치
                if (e.FinalContentKind != contract.FinalContentKind)
                    continue;

                if (e.Priority > fallbackPriority)
                {
                    fallbackPriority = e.Priority;
                    fallbackMatch = e;
                }
            }

            if (fallbackMatch != null)
            {
                entry = fallbackMatch;
                return true;
            }

            // 5단계: 최종 fallback - IsFallbackRequirement=true이고 finalContentKind가 None/Unknown인 entry
            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;
                if (!e.IsFallbackRequirement) continue;
                if (!e.AllowFallbackUse) continue;

                // finalContentKind가 None 또는 Unknown이면 모든 contract에 매칭
                if (e.FinalContentKind == WorldMapRuntimeFinalContentKind.None ||
                    e.FinalContentKind == WorldMapRuntimeFinalContentKind.Unknown)
                {
                    if (entry == null || e.Priority > entry.Priority)
                    {
                        entry = e;
                    }
                }
            }

            return entry != null;
        }

        /// <summary>
        /// finalPrefab 또는 finalProfile이 할당된 entry의 개수를 반환한다.
        /// </summary>
        /// <returns>configured entry 개수</returns>
        public int CountConfigured()
        {
            if (_entries == null || _entries.Count == 0)
                return 0;

            int count = 0;
            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e != null && e.IsConfigured())
                    count++;
            }
            return count;
        }

        /// <summary>
        /// finalPrefab과 finalProfile이 모두 null인 entry의 개수를 반환한다.
        /// </summary>
        /// <returns>missing entry 개수</returns>
        public int CountMissing()
        {
            if (_entries == null || _entries.Count == 0)
                return 0;

            int count = 0;
            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e != null && !e.IsConfigured())
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 중복된 requirementId가 있는지 검사한다.
        /// </summary>
        /// <param name="duplicateId">발견된 첫 번째 중복 requirementId</param>
        /// <returns>중복이 있으면 true</returns>
        public bool HasDuplicateRequirementIds(out string duplicateId)
        {
            duplicateId = null;

            if (_entries == null || _entries.Count == 0)
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>();

            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;

                string id = e.RequirementId;
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
        /// 중복된 exact match key(finalContentKind|runtimeCategory|runtimeKey|profileId 조합)가 있는지 검사한다.
        /// IsFallbackRequirement=true인 entry는 검사에서 제외한다.
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

            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;
                if (e.IsFallbackRequirement) continue;

                string exactKey = e.BuildExactKey();

                if (seen.Contains(exactKey))
                {
                    duplicateKey = exactKey;
                    return true;
                }

                seen.Add(exactKey);
            }

            return false;
        }

        /// <summary>
        /// 특정 finalContentKind에 IsFallbackRequirement=true인 entry가 존재하는지 확인한다.
        /// </summary>
        /// <param name="kind">검사할 finalContentKind</param>
        /// <returns>fallback entry가 존재하면 true</returns>
        public bool HasFallbackForFinalContentKind(WorldMapRuntimeFinalContentKind kind)
        {
            if (_entries == null || _entries.Count == 0)
                return false;

            foreach (WorldMapRuntimeFinalContentRequirementEntry e in _entries)
            {
                if (e == null) continue;
                if (!e.IsFallbackRequirement) continue;

                if (e.FinalContentKind == kind)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
