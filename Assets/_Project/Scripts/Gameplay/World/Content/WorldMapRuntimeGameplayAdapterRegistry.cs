using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// GeneratedWorldRoot에 부착되는 runtime registry MonoBehaviour.
    /// WorldMapRuntimeSpawnGameplayAdapter 목록을 cache하고,
    /// gameplay 시스템이 adapter를 category/zone/marker/profile/adapterKind 기준으로
    /// 빠르게 조회할 수 있도록 dictionary/list cache를 가진다.
    /// scene scan은 RebuildCacheFromRoot 호출 시에만 수행한다.
    /// </summary>
    public class WorldMapRuntimeGameplayAdapterRegistry : MonoBehaviour
    {
        // ===== Runtime Initialization Options =====

        [SerializeField, Tooltip("Awake에서 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 cache가 비어 있으면 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnEnableIfEmpty = true;

        [SerializeField, Tooltip("GetComponentsInChildren에서 includeInactive 사용 여부")]
        private bool includeInactiveAdapters = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization = false;

        // ===== Internal State =====

        /// <summary>중복 초기화 방지를 위한 내부 상태. ClearCache() 호출 시 false로 되돌아감.</summary>
        private bool hasRuntimeInitialized;

        // ===== Cache Fields =====

        /// <summary>모든 adapter의 flat list cache</summary>
        private List<WorldMapRuntimeSpawnGameplayAdapter> _allAdapters = new List<WorldMapRuntimeSpawnGameplayAdapter>();

        /// <summary>ZoneId -> adapter 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>> _byZoneId = new Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>>();

        /// <summary>RuntimeCategory -> adapter 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>> _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>>();

        /// <summary>AdapterKind -> adapter 목록 cache</summary>
        private Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeSpawnGameplayAdapter>> _byAdapterKind = new Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeSpawnGameplayAdapter>>();

        /// <summary>ProfileId -> adapter 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>> _byProfileId = new Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>>();

        /// <summary>MarkerId -> adapter (첫 번째만, 중복 시 첫 번째 우선)</summary>
        private Dictionary<string, WorldMapRuntimeSpawnGameplayAdapter> _byMarkerId = new Dictionary<string, WorldMapRuntimeSpawnGameplayAdapter>();

        /// <summary>중복 MarkerId 목록 (internal 관리용)</summary>
        private List<string> _duplicateMarkerIds = new List<string>();

        // ===== Public Read-only Properties =====

        /// <summary>모든 adapter의 읽기 전용 목록</summary>
        public IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> AllAdapters => _allAdapters;

        /// <summary>중복 MarkerId 목록 (읽기 전용)</summary>
        public IReadOnlyList<string> DuplicateMarkerIds => _duplicateMarkerIds;

        /// <summary>cache된 adapter 총 개수</summary>
        public int Count => _allAdapters.Count;

        /// <summary>Registry가 초기화되었는지 여부 (RebuildCacheFromRoot 호출 완료)</summary>
        public bool IsInitialized => hasRuntimeInitialized;

        // ===== Unity Lifecycle =====

        /// <summary>
        /// Awake: rebuildOnAwake가 true이면 RebuildCacheFromRoot(transform)를 호출하여
        /// PlayMode 진입 시 cache가 비어 있지 않도록 보장한다.
        /// </summary>
        private void Awake()
        {
            if (rebuildOnAwake && !hasRuntimeInitialized)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeGameplayAdapterRegistry] Awake: rebuildOnAwake is true. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: rebuildOnEnableIfEmpty가 true이고 AllAdapters count가 0이면
        /// RebuildCacheFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (rebuildOnEnableIfEmpty && _allAdapters.Count == 0)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeGameplayAdapterRegistry] OnEnable: cache is empty. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Cache Management =====

        /// <summary>
        /// Scene 내 모든 WorldMapRuntimeSpawnGameplayAdapter를 스캔하여 cache를 재구축한다.
        /// 내부적으로 RebuildCacheFromRoot(transform)을 호출한다.
        /// 수집 기준: RuntimeSpawnedInstances 하위 adapter만 cache 대상.
        /// RuntimeSpawnInstances 하위 preview instance 제외.
        /// RuntimePlaceholder 원본, WorldMapRuntimePlaceholderBinding만 있는 object,
        /// WorldMapZoneContentMarker가 붙은 object 제외.
        /// WorldMapRuntimeSpawnInstanceTag.IsPreviewInstance=true인 object 제외.
        /// 매 프레임 호출 금지.
        /// </summary>
        public void RebuildCacheFromScene()
        {
            RebuildCacheFromRoot(transform);
        }

        /// <summary>
        /// 지정한 root Transform 하위에서 모든 WorldMapRuntimeSpawnGameplayAdapter를 스캔하여
        /// cache를 재구축한다. root가 null이면 자기 transform으로 fallback한다.
        /// includeInactive=true로 수집하여 비활성화된 object도 포함한다.
        /// </summary>
        /// <param name="root">검색 기준이 되는 root Transform. null이면 this.transform 사용.</param>
        public void RebuildCacheFromRoot(Transform root)
        {
            // root가 null이면 자기 transform으로 fallback
            if (root == null)
                root = transform;

            // 1. root 하위 모든 WorldMapRuntimeSpawnGameplayAdapter를 includeInactive=true로 수집
            WorldMapRuntimeSpawnGameplayAdapter[] allAdapters = root.GetComponentsInChildren<WorldMapRuntimeSpawnGameplayAdapter>(includeInactiveAdapters);

            // 2. cache 컨테이너 초기화
            _allAdapters = new List<WorldMapRuntimeSpawnGameplayAdapter>();
            _byZoneId = new Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>>();
            _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>>();
            _byAdapterKind = new Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeSpawnGameplayAdapter>>();
            _byProfileId = new Dictionary<string, List<WorldMapRuntimeSpawnGameplayAdapter>>();
            _byMarkerId = new Dictionary<string, WorldMapRuntimeSpawnGameplayAdapter>();
            _duplicateMarkerIds = new List<string>();

            // 3. 각 adapter를 순회하며 유효한 항목만 수집
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                // null / destroyed object 체크
                if (adapter == null || adapter.gameObject == null)
                    continue;

                // ===== 제외 조건 =====

                // (1) RuntimeSpawnedInstances 하위가 아니면 제외
                if (!IsUnderRuntimeSpawnedInstances(adapter.transform))
                    continue;

                // (2) RuntimeSpawnInstances 하위 preview instance 제외
                //     (WorldMapRuntimeSpawnInstanceTag.IsPreviewInstance 체크)
                WorldMapRuntimeSpawnInstanceTag tag = adapter.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                if (tag != null && tag.IsPreviewInstance)
                    continue;

                // (3) WorldMapRuntimePlaceholderBinding이 붙은 원본 RuntimePlaceholder 제외
                if (adapter.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // (4) WorldMapZoneContentMarker가 붙은 source marker 제외
                if (adapter.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // (5) WorldMapRuntimeSpawnInstanceTag가 있고 IsPreviewInstance=true인 object 제외 (위에서 이미 체크)
                // (6) AdapterKind.None은 기본적으로 cache 대상에 포함 (Validate에서 판단)

                // ===== 유효한 adapter =====
                _allAdapters.Add(adapter);

                // ZoneId 기준 추가
                string zoneId = adapter.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_byZoneId.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapRuntimeSpawnGameplayAdapter>();
                        _byZoneId[zoneId] = zoneList;
                    }
                    zoneList.Add(adapter);
                }

                // RuntimeCategory 기준 추가
                string category = adapter.RuntimeCategory;
                if (!string.IsNullOrEmpty(category))
                {
                    if (!_byRuntimeCategory.TryGetValue(category, out var catList))
                    {
                        catList = new List<WorldMapRuntimeSpawnGameplayAdapter>();
                        _byRuntimeCategory[category] = catList;
                    }
                    catList.Add(adapter);
                }

                // AdapterKind 기준 추가
                WorldMapRuntimeGameplayAdapterKind kind = adapter.AdapterKind;
                if (!_byAdapterKind.TryGetValue(kind, out var kindList))
                {
                    kindList = new List<WorldMapRuntimeSpawnGameplayAdapter>();
                    _byAdapterKind[kind] = kindList;
                }
                kindList.Add(adapter);

                // ProfileId 기준 추가
                string profileId = adapter.ProfileId;
                if (!string.IsNullOrEmpty(profileId))
                {
                    if (!_byProfileId.TryGetValue(profileId, out var profileList))
                    {
                        profileList = new List<WorldMapRuntimeSpawnGameplayAdapter>();
                        _byProfileId[profileId] = profileList;
                    }
                    profileList.Add(adapter);
                }

                // MarkerId 기준 추가 (중복 시 첫 번째만 유지)
                string markerId = adapter.MarkerId;
                if (!string.IsNullOrEmpty(markerId))
                {
                    if (_byMarkerId.ContainsKey(markerId))
                    {
                        // 중복 발생 -> duplicate 목록에 추가 (아직 없으면)
                        if (!_duplicateMarkerIds.Contains(markerId))
                        {
                            _duplicateMarkerIds.Add(markerId);
                            UnityEngine.Debug.LogWarning($"[WorldMapRuntimeGameplayAdapterRegistry] Duplicate MarkerId found: '{markerId}' (Zone: {zoneId}, Category: {category}). First instance will be used for TryGetByMarkerId.");
                        }
                    }
                    else
                    {
                        _byMarkerId[markerId] = adapter;
                    }
                }
            }

            // 4. 결과 로그
            UnityEngine.Debug.Log($"[WorldMapRuntimeGameplayAdapterRegistry] RebuildCacheFromRoot complete. " +
                $"Adapters: {_allAdapters.Count}, " +
                $"Zones: {_byZoneId.Count}, " +
                $"Categories: {_byRuntimeCategory.Count}, " +
                $"AdapterKinds: {_byAdapterKind.Count}, " +
                $"ProfileIds: {_byProfileId.Count}, " +
                $"MarkerIds: {_byMarkerId.Count}, " +
                $"Duplicate MarkerIds: {_duplicateMarkerIds.Count}");
        }

        /// <summary>
        /// 모든 cache를 비운다. hasRuntimeInitialized도 false로 되돌린다.
        /// </summary>
        public void ClearCache()
        {
            _allAdapters.Clear();
            _byZoneId.Clear();
            _byRuntimeCategory.Clear();
            _byAdapterKind.Clear();
            _byProfileId.Clear();
            _byMarkerId.Clear();
            _duplicateMarkerIds.Clear();

            // 중복 초기화 방지 상태도 초기화
            hasRuntimeInitialized = false;

            UnityEngine.Debug.Log("[WorldMapRuntimeGameplayAdapterRegistry] Cache cleared.");
        }

        // ===== Query API =====

        /// <summary>
        /// 지정한 ZoneId에 속한 adapter 목록을 반환한다.
        /// 해당 ZoneId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> GetByZoneId(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeSpawnGameplayAdapter>();

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeSpawnGameplayAdapter>();
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 adapter 목록을 반환한다.
        /// 해당 Category가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> GetByRuntimeCategory(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory))
                return System.Array.Empty<WorldMapRuntimeSpawnGameplayAdapter>();

            if (_byRuntimeCategory.TryGetValue(runtimeCategory, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeSpawnGameplayAdapter>();
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 adapter 목록을 반환한다.
        /// 해당 AdapterKind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> GetByAdapterKind(WorldMapRuntimeGameplayAdapterKind adapterKind)
        {
            if (_byAdapterKind.TryGetValue(adapterKind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeSpawnGameplayAdapter>();
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 adapter 목록을 반환한다.
        /// 해당 ProfileId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> GetByProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return System.Array.Empty<WorldMapRuntimeSpawnGameplayAdapter>();

            if (_byProfileId.TryGetValue(profileId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeSpawnGameplayAdapter>();
        }

        /// <summary>
        /// MarkerId로 adapter를 조회한다.
        /// 중복 MarkerId가 있는 경우 첫 번째 adapter가 반환된다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetByMarkerId(string markerId, out WorldMapRuntimeSpawnGameplayAdapter adapter)
        {
            if (string.IsNullOrEmpty(markerId))
            {
                adapter = null;
                return false;
            }

            return _byMarkerId.TryGetValue(markerId, out adapter);
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 adapter 개수를 반환한다.
        /// </summary>
        public int CountByZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return 0;

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 adapter 개수를 반환한다.
        /// </summary>
        public int CountByCategory(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory))
                return 0;

            if (_byRuntimeCategory.TryGetValue(runtimeCategory, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 adapter 개수를 반환한다.
        /// </summary>
        public int CountByAdapterKind(WorldMapRuntimeGameplayAdapterKind adapterKind)
        {
            if (_byAdapterKind.TryGetValue(adapterKind, out var list))
                return list.Count;

            return 0;
        }

        // ===== Ancestor Helper Methods =====

        /// <summary>
        /// 지정한 Transform의 ancestor 중 지정된 이름을 가진 object가 있는지 재귀 탐색한다.
        /// </summary>
        /// <param name="target">확인할 Transform</param>
        /// <param name="ancestorName">찾을 ancestor 이름</param>
        /// <returns>해당 이름의 ancestor가 있으면 true</returns>
        private static bool IsUnderNamedAncestor(Transform target, string ancestorName)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == ancestorName)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// 지정한 Transform의 ancestor 중 "RuntimeSpawnedInstances" 이름을 가진 object가 있는지 확인한다.
        /// RuntimeSpawnedInstances 하위 adapter만 cache에 포함하기 위한 보조 필터.
        /// </summary>
        /// <param name="target">확인할 Transform</param>
        /// <returns>RuntimeSpawnedInstances 하위이면 true</returns>
        private static bool IsUnderRuntimeSpawnedInstances(Transform target)
        {
            return IsUnderNamedAncestor(target, "RuntimeSpawnedInstances");
        }

        // ===== Debug / Validation Helpers =====

        /// <summary>
        /// 현재 cache 상태의 요약 문자열을 반환한다. (디버그/검증용)
        /// </summary>
        public string GetCacheSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WorldMapRuntimeGameplayAdapterRegistry Cache Summary ===");
            sb.AppendLine($"Total Adapters: {_allAdapters.Count}");
            sb.AppendLine($"Unique Zones: {_byZoneId.Count}");
            sb.AppendLine($"Unique Categories: {_byRuntimeCategory.Count}");
            sb.AppendLine($"Unique AdapterKinds: {_byAdapterKind.Count}");
            sb.AppendLine($"Unique ProfileIds: {_byProfileId.Count}");
            sb.AppendLine($"Unique MarkerIds: {_byMarkerId.Count}");
            sb.AppendLine($"Duplicate MarkerIds: {_duplicateMarkerIds.Count}");

            // Category breakdown
            sb.AppendLine("--- Category Breakdown ---");
            foreach (var kvp in _byRuntimeCategory)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // AdapterKind breakdown
            sb.AppendLine("--- AdapterKind Breakdown ---");
            foreach (var kvp in _byAdapterKind)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // Zone count breakdown
            sb.AppendLine("--- Zone Count Breakdown ---");
            foreach (var kvp in _byZoneId)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // Duplicate MarkerIds 상세
            if (_duplicateMarkerIds.Count > 0)
            {
                sb.AppendLine("--- Duplicate MarkerIds ---");
                foreach (string dupId in _duplicateMarkerIds)
                {
                    sb.AppendLine($"  {dupId}");
                }
            }

            return sb.ToString();
        }
    }
}
