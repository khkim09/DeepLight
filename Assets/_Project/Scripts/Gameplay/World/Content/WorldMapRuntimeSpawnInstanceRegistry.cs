using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Gameplay runtime용 Runtime Spawn Instance Registry.
    /// Scene 내 runtime spawn instance(IsPreviewInstance==false)만 수집하여
    /// Dictionary/List cache 기반의 Query API를 제공한다.
    /// 매 프레임 FindObjectsByType을 사용하지 않고, RebuildCacheFromScene() 호출 시에만 scene scan한다.
    /// Update/LateUpdate 사용 금지.
    /// </summary>
    public class WorldMapRuntimeSpawnInstanceRegistry : MonoBehaviour
    {
        // ===== Runtime Initialization Options =====

        [SerializeField, Tooltip("Awake에서 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 cache가 비어 있으면 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnEnableIfEmpty = true;

        [SerializeField, Tooltip("GetComponentsInChildren에서 includeInactive 사용 여부")]
        private bool includeInactiveInstances = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization = false;

        // ===== Internal State =====

        /// <summary>중복 초기화 방지를 위한 내부 상태. ClearCache() 호출 시 false로 되돌아감.</summary>
        private bool hasRuntimeInitialized;

        // ===== Cache Fields =====

        /// <summary>모든 runtime instance의 flat list cache</summary>
        private List<WorldMapRuntimeSpawnInstanceTag> _allInstances = new List<WorldMapRuntimeSpawnInstanceTag>();

        /// <summary>ZoneId -> instance 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeSpawnInstanceTag>> _byZoneId = new Dictionary<string, List<WorldMapRuntimeSpawnInstanceTag>>();

        /// <summary>RuntimeCategory -> instance 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeSpawnInstanceTag>> _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeSpawnInstanceTag>>();

        /// <summary>MarkerType -> instance 목록 cache</summary>
        private Dictionary<WorldMapZoneContentMarkerType, List<WorldMapRuntimeSpawnInstanceTag>> _byMarkerType = new Dictionary<WorldMapZoneContentMarkerType, List<WorldMapRuntimeSpawnInstanceTag>>();

        /// <summary>MarkerId -> instance (첫 번째만, 중복 시 첫 번째 우선)</summary>
        private Dictionary<string, WorldMapRuntimeSpawnInstanceTag> _byMarkerId = new Dictionary<string, WorldMapRuntimeSpawnInstanceTag>();

        /// <summary>중복 MarkerId 목록 (internal 관리용)</summary>
        private List<string> _duplicateMarkerIds = new List<string>();

        // ===== Public Read-only Properties =====

        /// <summary>모든 runtime instance의 읽기 전용 목록</summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> AllInstances => _allInstances;

        /// <summary>중복 MarkerId 목록 (읽기 전용)</summary>
        public IReadOnlyList<string> DuplicateMarkerIds => _duplicateMarkerIds;

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeSpawnInstanceRegistry] Awake: rebuildOnAwake is true. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: rebuildOnEnableIfEmpty가 true이고 AllInstances count가 0이면
        /// RebuildCacheFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (rebuildOnEnableIfEmpty && _allInstances.Count == 0)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeSpawnInstanceRegistry] OnEnable: cache is empty. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Query API =====

        /// <summary>
        /// 지정한 ZoneId에 속한 runtime instance 목록을 반환한다.
        /// 해당 ZoneId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> GetByZoneId(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 runtime instance 목록을 반환한다.
        /// 해당 Category가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> GetByRuntimeCategory(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory))
                return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();

            if (_byRuntimeCategory.TryGetValue(runtimeCategory, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();
        }

        /// <summary>
        /// 지정한 MarkerType에 속한 runtime instance 목록을 반환한다.
        /// 해당 MarkerType이 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> GetByMarkerType(WorldMapZoneContentMarkerType markerType)
        {
            if (_byMarkerType.TryGetValue(markerType, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();
        }

        /// <summary>
        /// MarkerId로 runtime instance를 조회한다.
        /// 중복 MarkerId가 있는 경우 첫 번째 instance가 반환된다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetByMarkerId(string markerId, out WorldMapRuntimeSpawnInstanceTag result)
        {
            if (string.IsNullOrEmpty(markerId))
            {
                result = null;
                return false;
            }

            return _byMarkerId.TryGetValue(markerId, out result);
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 instance 개수를 반환한다.
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
        /// 지정한 ZoneId에 속한 instance 개수를 반환한다.
        /// </summary>
        public int CountByZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return 0;

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list.Count;

            return 0;
        }

        // ===== Cache Management =====

        /// <summary>
        /// Scene 내 모든 runtime spawn instance를 스캔하여 cache를 재구축한다.
        /// 내부적으로 RebuildCacheFromRoot(transform)을 호출한다.
        /// 수집 기준: WorldMapRuntimeSpawnInstanceTag 존재 && IsPreviewInstance == false
        ///   && RuntimeSpawnedInstances 하위 && WorldMapRuntimePlaceholderBinding 없음 && WorldMapZoneContentMarker 없음.
        /// WorldMapRuntimeSpawnPlaceholderTag 존재 여부는 제외 조건으로 사용하지 않는다.
        /// 매 프레임 호출 금지. RebuildCacheFromScene() 호출 시에만 scene scan.
        /// </summary>
        public void RebuildCacheFromScene()
        {
            // 내부적으로 자기 transform 기준 RebuildCacheFromRoot 호출
            RebuildCacheFromRoot(transform);
        }

        /// <summary>
        /// 지정한 root Transform 하위에서 모든 runtime spawn instance를 스캔하여 cache를 재구축한다.
        /// root가 null이면 자기 transform으로 fallback한다.
        /// includeInactive=true로 수집하여 비활성화된 object도 포함한다.
        /// </summary>
        /// <param name="root">검색 기준이 되는 root Transform. null이면 this.transform 사용.</param>
        public void RebuildCacheFromRoot(Transform root)
        {
            // root가 null이면 자기 transform으로 fallback
            if (root == null)
                root = transform;

            // 1. root 하위 모든 WorldMapRuntimeSpawnInstanceTag를 includeInactive=true로 수집
            WorldMapRuntimeSpawnInstanceTag[] allTags = root.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(includeInactiveInstances);

            // 2. cache 컨테이너 초기화
            _allInstances = new List<WorldMapRuntimeSpawnInstanceTag>();
            _byZoneId = new Dictionary<string, List<WorldMapRuntimeSpawnInstanceTag>>();
            _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeSpawnInstanceTag>>();
            _byMarkerType = new Dictionary<WorldMapZoneContentMarkerType, List<WorldMapRuntimeSpawnInstanceTag>>();
            _byMarkerId = new Dictionary<string, WorldMapRuntimeSpawnInstanceTag>();
            _duplicateMarkerIds = new List<string>();

            // 3. 각 tag를 순회하며 runtime instance만 수집
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allTags)
            {
                // null / destroyed object 체크
                if (tag == null || tag.gameObject == null)
                    continue;

                // ===== 포함 조건 =====

                // (1) Preview instance 제외
                if (tag.IsPreviewInstance)
                    continue;

                // (2) RuntimeSpawnedInstances 하위가 아니면 제외
                //     (RuntimeSpawnInstances 하위 preview instance는 IsPreviewInstance=true로 위에서 제외됨)
                if (!IsUnderRuntimeSpawnedInstances(tag.transform))
                    continue;

                // (3) WorldMapRuntimePlaceholderBinding이 붙은 원본 RuntimePlaceholder 제외
                //     RuntimeSpawn_... instance에는 binding이 없으므로 이 조건으로 원본 placeholder가 걸러짐
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // (4) WorldMapZoneContentMarker가 붙은 source marker 제외
                if (tag.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // (5) WorldMapRuntimeSpawnPlaceholderTag 존재 여부는 제외 조건으로 사용하지 않음.
                //     RuntimeSpawn_... instance는 editor placeholder prefab 기반이므로
                //     WorldMapRuntimeSpawnPlaceholderTag가 붙어 있을 수 있으나 정보성 태그로만 허용.

                // ===== 유효한 runtime instance =====
                _allInstances.Add(tag);

                // ZoneId 기준 추가
                string zoneId = tag.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_byZoneId.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapRuntimeSpawnInstanceTag>();
                        _byZoneId[zoneId] = zoneList;
                    }
                    zoneList.Add(tag);
                }

                // RuntimeCategory 기준 추가
                string category = tag.RuntimeCategory;
                if (!string.IsNullOrEmpty(category))
                {
                    if (!_byRuntimeCategory.TryGetValue(category, out var catList))
                    {
                        catList = new List<WorldMapRuntimeSpawnInstanceTag>();
                        _byRuntimeCategory[category] = catList;
                    }
                    catList.Add(tag);
                }

                // MarkerType 기준 추가
                WorldMapZoneContentMarkerType markerType = tag.MarkerType;
                if (!_byMarkerType.TryGetValue(markerType, out var typeList))
                {
                    typeList = new List<WorldMapRuntimeSpawnInstanceTag>();
                    _byMarkerType[markerType] = typeList;
                }
                typeList.Add(tag);

                // MarkerId 기준 추가 (중복 시 첫 번째만 유지)
                string markerId = tag.MarkerId;
                if (!string.IsNullOrEmpty(markerId))
                {
                    if (_byMarkerId.ContainsKey(markerId))
                    {
                        // 중복 발생 -> duplicate 목록에 추가 (아직 없으면)
                        if (!_duplicateMarkerIds.Contains(markerId))
                        {
                            _duplicateMarkerIds.Add(markerId);
                            UnityEngine.Debug.LogWarning($"[WorldMapRuntimeSpawnInstanceRegistry] Duplicate MarkerId found: '{markerId}' (Zone: {zoneId}, Category: {category}). First instance will be used for TryGetByMarkerId.");
                        }

                    }
                    else
                    {
                        _byMarkerId[markerId] = tag;
                    }
                }
            }

            // 4. 결과 로그
            UnityEngine.Debug.Log($"[WorldMapRuntimeSpawnInstanceRegistry] RebuildCacheFromRoot complete. " +
                $"Runtime instances: {_allInstances.Count}, " +
                $"Zones: {_byZoneId.Count}, " +
                $"Categories: {_byRuntimeCategory.Count}, " +
                $"MarkerTypes: {_byMarkerType.Count}, " +
                $"MarkerIds: {_byMarkerId.Count}, " +
                $"Duplicate MarkerIds: {_duplicateMarkerIds.Count}");
        }

        /// <summary>
        /// 모든 cache를 비운다. hasRuntimeInitialized도 false로 되돌린다.
        /// </summary>
        public void ClearCache()
        {
            _allInstances.Clear();
            _byZoneId.Clear();
            _byRuntimeCategory.Clear();
            _byMarkerType.Clear();
            _byMarkerId.Clear();
            _duplicateMarkerIds.Clear();

            // 중복 초기화 방지 상태도 초기화
            hasRuntimeInitialized = false;

            UnityEngine.Debug.Log("[WorldMapRuntimeSpawnInstanceRegistry] Cache cleared.");
        }

        // ===== Public Static Helper: Scene Runtime Instance Count =====

        /// <summary>
        /// RebuildCacheFromRoot와 동일한 필터 로직으로 scene 내 runtime instance 개수를 계산한다.
        /// Validate 등에서 RebuildCache와 scene count의 필터 로직이 달라지지 않도록 공용으로 사용한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>필터를 통과한 runtime instance 개수</returns>
        public static int CountSceneRuntimeInstances(Transform root)
        {
            if (root == null)
                return 0;

            WorldMapRuntimeSpawnInstanceTag[] allTags = root.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true);
            int count = 0;

            foreach (WorldMapRuntimeSpawnInstanceTag tag in allTags)
            {
                if (tag == null || tag.gameObject == null)
                    continue;

                // Preview instance 제외
                if (tag.IsPreviewInstance)
                    continue;

                // RuntimeSpawnedInstances 하위인지 확인
                if (!IsUnderRuntimeSpawnedInstances(tag.transform))
                    continue;

                // WorldMapRuntimePlaceholderBinding이 붙은 원본 placeholder 제외
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // WorldMapZoneContentMarker 제외
                if (tag.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // WorldMapRuntimeSpawnPlaceholderTag는 제외 조건으로 사용하지 않음
                count++;
            }

            return count;
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
        /// RuntimeSpawnedInstances 하위 runtime instance만 cache에 포함하기 위한 보조 필터.
        /// </summary>
        /// <param name="target">확인할 Transform</param>
        /// <returns>RuntimeSpawnedInstances 하위이면 true</returns>
        private static bool IsUnderRuntimeSpawnedInstances(Transform target)
        {
            return IsUnderNamedAncestor(target, "RuntimeSpawnedInstances");
        }

        /// <summary>
        /// 지정한 Transform의 ancestor 중 "RuntimeSpawnInstances" 이름을 가진 object가 있는지 확인한다.
        /// Preview instance가 RuntimeSpawnInstances 하위에 있는지 판별하기 위한 보조 필터.
        /// </summary>
        /// <param name="target">확인할 Transform</param>
        /// <returns>RuntimeSpawnInstances 하위이면 true</returns>
        private static bool IsUnderRuntimeSpawnPreviewInstances(Transform target)
        {
            return IsUnderNamedAncestor(target, "RuntimeSpawnInstances");
        }

        // ===== Debug / Validation Helpers =====

        /// <summary>
        /// 현재 cache 상태의 요약 문자열을 반환한다. (디버그/검증용)
        /// </summary>
        public string GetCacheSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== WorldMapRuntimeSpawnInstanceRegistry Cache Summary ===");
            sb.AppendLine($"Total Runtime Instances: {_allInstances.Count}");
            sb.AppendLine($"Unique Zones: {_byZoneId.Count}");
            sb.AppendLine($"Unique Categories: {_byRuntimeCategory.Count}");
            sb.AppendLine($"Unique MarkerTypes: {_byMarkerType.Count}");
            sb.AppendLine($"Unique MarkerIds: {_byMarkerId.Count}");
            sb.AppendLine($"Duplicate MarkerIds: {_duplicateMarkerIds.Count}");

            // Category breakdown
            sb.AppendLine("--- Category Breakdown ---");
            foreach (var kvp in _byRuntimeCategory)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            // Zone count breakdown
            sb.AppendLine("--- Zone Count Breakdown ---");
            foreach (var kvp in _byZoneId)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
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
