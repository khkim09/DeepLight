using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Gameplay runtime용 Runtime Final Content Instance Registry.
    /// RuntimeFinalContentInstances 하위의 WorldMapRuntimeFinalContentInstanceTag들을 수집하고
    /// Dictionary/List cache 기반의 Query API를 제공한다.
    /// 매 프레임 FindObjectsByType을 사용하지 않고, RebuildCacheFromRoot() 호출 시에만 scene scan한다.
    /// Update/LateUpdate 사용 금지.
    /// K-1 Registry / L-2 Adapter Registry 패턴과 동일한 구조로 작성.
    /// </summary>
    public class WorldMapRuntimeFinalContentInstanceRegistry : MonoBehaviour
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

        /// <summary>모든 final content instance의 flat list cache</summary>
        private List<WorldMapRuntimeFinalContentInstanceTag> _allInstances = new List<WorldMapRuntimeFinalContentInstanceTag>();

        /// <summary>중복 SourceMarkerId 목록 (internal 관리용)</summary>
        private List<string> _duplicateSourceMarkerIds = new List<string>();

        /// <summary>ZoneId -> instance 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>> _byZoneId = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();

        /// <summary>RuntimeCategory -> instance 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>> _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();

        /// <summary>FinalContentKind -> instance 목록 cache</summary>
        private Dictionary<WorldMapRuntimeFinalContentKind, List<WorldMapRuntimeFinalContentInstanceTag>> _byFinalContentKind = new Dictionary<WorldMapRuntimeFinalContentKind, List<WorldMapRuntimeFinalContentInstanceTag>>();

        /// <summary>AdapterKind -> instance 목록 cache</summary>
        private Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeFinalContentInstanceTag>> _byAdapterKind = new Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeFinalContentInstanceTag>>();

        /// <summary>RequirementId -> instance 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>> _byRequirementId = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();

        /// <summary>ProfileId -> instance 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>> _byProfileId = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();

        /// <summary>SourceMarkerId -> instance (첫 번째만, 중복 시 첫 번째 우선)</summary>
        private Dictionary<string, WorldMapRuntimeFinalContentInstanceTag> _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeFinalContentInstanceTag>();

        // ===== Public Read-only Properties =====

        /// <summary>모든 final content instance의 읽기 전용 목록</summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> AllInstances => _allInstances;

        /// <summary>중복 SourceMarkerId 목록 (읽기 전용)</summary>
        public IReadOnlyList<string> DuplicateSourceMarkerIds => _duplicateSourceMarkerIds;

        /// <summary>cache된 final content instance 총 개수</summary>
        public int Count => _allInstances.Count;

        /// <summary>Registry가 초기화되었는지 여부</summary>
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
                    UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentInstanceRegistry] Awake: rebuildOnAwake is true. Rebuilding cache from root.");

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentInstanceRegistry] OnEnable: cache is empty. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Query API =====

        /// <summary>
        /// 지정한 ZoneId에 속한 final content instance 목록을 반환한다.
        /// 해당 ZoneId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> GetByZoneId(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 final content instance 목록을 반환한다.
        /// 해당 Category가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> GetByRuntimeCategory(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory))
                return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();

            if (_byRuntimeCategory.TryGetValue(runtimeCategory, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();
        }

        /// <summary>
        /// 지정한 FinalContentKind에 속한 final content instance 목록을 반환한다.
        /// 해당 FinalContentKind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> GetByFinalContentKind(WorldMapRuntimeFinalContentKind finalContentKind)
        {
            if (_byFinalContentKind.TryGetValue(finalContentKind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 final content instance 목록을 반환한다.
        /// 해당 AdapterKind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> GetByAdapterKind(WorldMapRuntimeGameplayAdapterKind adapterKind)
        {
            if (_byAdapterKind.TryGetValue(adapterKind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();
        }

        /// <summary>
        /// 지정한 RequirementId에 속한 final content instance 목록을 반환한다.
        /// 해당 RequirementId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> GetByRequirementId(string requirementId)
        {
            if (string.IsNullOrEmpty(requirementId))
                return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();

            if (_byRequirementId.TryGetValue(requirementId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 final content instance 목록을 반환한다.
        /// 해당 ProfileId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> GetByProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();

            if (_byProfileId.TryGetValue(profileId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentInstanceTag>();
        }

        /// <summary>
        /// SourceMarkerId로 final content instance를 조회한다.
        /// 중복 SourceMarkerId가 있는 경우 첫 번째 instance가 반환된다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeFinalContentInstanceTag result)
        {
            if (string.IsNullOrEmpty(sourceMarkerId))
            {
                result = null;
                return false;
            }

            return _bySourceMarkerId.TryGetValue(sourceMarkerId, out result);
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 final content instance 개수를 반환한다.
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
        /// 지정한 RuntimeCategory에 속한 final content instance 개수를 반환한다.
        /// </summary>
        public int CountByRuntimeCategory(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory))
                return 0;

            if (_byRuntimeCategory.TryGetValue(runtimeCategory, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 FinalContentKind에 속한 final content instance 개수를 반환한다.
        /// </summary>
        public int CountByFinalContentKind(WorldMapRuntimeFinalContentKind finalContentKind)
        {
            if (_byFinalContentKind.TryGetValue(finalContentKind, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 final content instance 개수를 반환한다.
        /// </summary>
        public int CountByAdapterKind(WorldMapRuntimeGameplayAdapterKind adapterKind)
        {
            if (_byAdapterKind.TryGetValue(adapterKind, out var list))
                return list.Count;

            return 0;
        }

        // ===== Cache Management =====

        /// <summary>
        /// Scene 내 모든 final content instance를 스캔하여 cache를 재구축한다.
        /// 내부적으로 RebuildCacheFromRoot(transform)을 호출한다.
        /// 수집 기준: WorldMapRuntimeFinalContentInstanceTag 존재 && IsGeneratedFinalInstance == true
        ///   && RuntimeFinalContentInstances 하위.
        /// WorldMapRuntimeFinalContentPlaceholderTag 존재 여부는 제외 조건으로 사용하지 않는다.
        /// 매 프레임 호출 금지. RebuildCacheFromScene() 호출 시에만 scene scan.
        /// </summary>
        public void RebuildCacheFromScene()
        {
            // 내부적으로 자기 transform 기준 RebuildCacheFromRoot 호출
            RebuildCacheFromRoot(transform);
        }

        /// <summary>
        /// 지정한 root Transform 하위에서 모든 final content instance를 스캔하여 cache를 재구축한다.
        /// root가 null이면 자기 transform으로 fallback한다.
        /// includeInactive 옵션에 따라 비활성화된 object도 포함 가능.
        /// </summary>
        /// <param name="root">검색 기준이 되는 root Transform. null이면 this.transform 사용.</param>
        public void RebuildCacheFromRoot(Transform root)
        {
            // root가 null이면 자기 transform으로 fallback
            if (root == null)
                root = transform;

            // 1. root 하위 모든 WorldMapRuntimeFinalContentInstanceTag를 includeInactive 옵션에 따라 수집
            WorldMapRuntimeFinalContentInstanceTag[] allTags = root.GetComponentsInChildren<WorldMapRuntimeFinalContentInstanceTag>(includeInactiveInstances);

            // 2. cache 컨테이너 초기화
            _allInstances = new List<WorldMapRuntimeFinalContentInstanceTag>();
            _duplicateSourceMarkerIds = new List<string>();
            _byZoneId = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();
            _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();
            _byFinalContentKind = new Dictionary<WorldMapRuntimeFinalContentKind, List<WorldMapRuntimeFinalContentInstanceTag>>();
            _byAdapterKind = new Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeFinalContentInstanceTag>>();
            _byRequirementId = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();
            _byProfileId = new Dictionary<string, List<WorldMapRuntimeFinalContentInstanceTag>>();
            _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeFinalContentInstanceTag>();

            // 3. 각 tag를 순회하며 final content instance만 수집
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allTags)
            {
                // null / destroyed object 체크
                if (tag == null || tag.gameObject == null)
                    continue;

                // ===== 포함 조건 =====

                // (1) IsGeneratedFinalInstance == true인 것만 포함
                if (!tag.IsGeneratedFinalInstance)
                    continue;

                // (2) RuntimeFinalContentInstances 하위가 아니면 제외
                if (!IsUnderRuntimeFinalContentInstances(tag.transform))
                    continue;

                // ===== 제외 조건 =====

                // (3) RuntimeSpawnedInstances 하위 source runtime instance 제외
                if (IsUnderRuntimeSpawnedInstances(tag.transform))
                    continue;

                // (4) RuntimeSpawnInstances 하위 preview instance 제외
                if (IsUnderRuntimeSpawnPreviewInstances(tag.transform))
                    continue;

                // (5) RuntimePlaceholder 원본 제외 (WorldMapRuntimePlaceholderBinding이 있는 object)
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // (6) WorldMapZoneContentMarker가 있는 object 제외
                if (tag.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // (7) WorldMapRuntimeSpawnInstanceTag가 있는 object 제외
                if (tag.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                    continue;

                // (8) WorldMapRuntimeSpawnGameplayAdapter가 있는 object 제외
                if (tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                    continue;

                // (9) WorldMapRuntimeFinalContentContract가 있는 object 제외
                if (tag.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    continue;

                // (10) WorldMapRuntimeFinalContentPlaceholderTag 존재 여부는 제외 조건으로 사용하지 않음.
                //     현재 generated placeholder final prefab에는 이 태그가 붙어 있으며,
                //     final content instance로 정상 수집되어야 한다.

                // ===== 유효한 final content instance =====
                _allInstances.Add(tag);

                // ZoneId 기준 추가
                string zoneId = tag.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_byZoneId.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapRuntimeFinalContentInstanceTag>();
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
                        catList = new List<WorldMapRuntimeFinalContentInstanceTag>();
                        _byRuntimeCategory[category] = catList;
                    }
                    catList.Add(tag);
                }

                // FinalContentKind 기준 추가
                WorldMapRuntimeFinalContentKind finalContentKind = tag.FinalContentKind;
                if (!_byFinalContentKind.TryGetValue(finalContentKind, out var kindList))
                {
                    kindList = new List<WorldMapRuntimeFinalContentInstanceTag>();
                    _byFinalContentKind[finalContentKind] = kindList;
                }
                kindList.Add(tag);

                // AdapterKind 기준 추가
                WorldMapRuntimeGameplayAdapterKind adapterKind = tag.AdapterKind;
                if (!_byAdapterKind.TryGetValue(adapterKind, out var adapterList))
                {
                    adapterList = new List<WorldMapRuntimeFinalContentInstanceTag>();
                    _byAdapterKind[adapterKind] = adapterList;
                }
                adapterList.Add(tag);

                // RequirementId 기준 추가
                string requirementId = tag.RequirementId;
                if (!string.IsNullOrEmpty(requirementId))
                {
                    if (!_byRequirementId.TryGetValue(requirementId, out var reqList))
                    {
                        reqList = new List<WorldMapRuntimeFinalContentInstanceTag>();
                        _byRequirementId[requirementId] = reqList;
                    }
                    reqList.Add(tag);
                }

                // ProfileId 기준 추가
                string profileId = tag.ProfileId;
                if (!string.IsNullOrEmpty(profileId))
                {
                    if (!_byProfileId.TryGetValue(profileId, out var profList))
                    {
                        profList = new List<WorldMapRuntimeFinalContentInstanceTag>();
                        _byProfileId[profileId] = profList;
                    }
                    profList.Add(tag);
                }

                // SourceMarkerId 기준 추가 (중복 시 첫 번째만 유지)
                string sourceMarkerId = tag.SourceMarkerId;
                if (!string.IsNullOrEmpty(sourceMarkerId))
                {
                    if (_bySourceMarkerId.ContainsKey(sourceMarkerId))
                    {
                        // 중복 발생 -> duplicate 목록에 추가 (아직 없으면)
                        if (!_duplicateSourceMarkerIds.Contains(sourceMarkerId))
                        {
                            _duplicateSourceMarkerIds.Add(sourceMarkerId);
                            UnityEngine.Debug.LogWarning($"[WorldMapRuntimeFinalContentInstanceRegistry] Duplicate SourceMarkerId found: '{sourceMarkerId}' (Zone: {zoneId}, Category: {category}). First instance will be used for TryGetBySourceMarkerId.");
                        }
                    }
                    else
                    {
                        _bySourceMarkerId[sourceMarkerId] = tag;
                    }
                }
            }

            // 4. 결과 로그
            UnityEngine.Debug.Log($"[WorldMapRuntimeFinalContentInstanceRegistry] RebuildCacheFromRoot complete. " +
                $"Final content instances: {_allInstances.Count}, " +
                $"Zones: {_byZoneId.Count}, " +
                $"Categories: {_byRuntimeCategory.Count}, " +
                $"FinalContentKinds: {_byFinalContentKind.Count}, " +
                $"AdapterKinds: {_byAdapterKind.Count}, " +
                $"RequirementIds: {_byRequirementId.Count}, " +
                $"ProfileIds: {_byProfileId.Count}, " +
                $"SourceMarkerIds: {_bySourceMarkerId.Count}, " +
                $"Duplicate SourceMarkerIds: {_duplicateSourceMarkerIds.Count}");
        }

        /// <summary>
        /// 모든 cache를 비운다. hasRuntimeInitialized도 false로 되돌린다.
        /// </summary>
        public void ClearCache()
        {
            _allInstances.Clear();
            _duplicateSourceMarkerIds.Clear();
            _byZoneId.Clear();
            _byRuntimeCategory.Clear();
            _byFinalContentKind.Clear();
            _byAdapterKind.Clear();
            _byRequirementId.Clear();
            _byProfileId.Clear();
            _bySourceMarkerId.Clear();

            // 중복 초기화 방지 상태도 초기화
            hasRuntimeInitialized = false;

            UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentInstanceRegistry] Cache cleared.");
        }

        // ===== Public Static Helper: Scene Final Content Instance Count =====

        /// <summary>
        /// RebuildCacheFromRoot와 동일한 필터 로직으로 scene 내 final content instance 개수를 계산한다.
        /// Validate 등에서 RebuildCache와 scene count의 필터 로직이 달라지지 않도록 공용으로 사용한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>필터를 통과한 final content instance 개수</returns>
        public static int CountSceneFinalContentInstances(Transform root)
        {
            if (root == null)
                return 0;

            WorldMapRuntimeFinalContentInstanceTag[] allTags = root.GetComponentsInChildren<WorldMapRuntimeFinalContentInstanceTag>(true);
            int count = 0;

            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allTags)
            {
                if (tag == null || tag.gameObject == null)
                    continue;

                // IsGeneratedFinalInstance == true인 것만 포함
                if (!tag.IsGeneratedFinalInstance)
                    continue;

                // RuntimeFinalContentInstances 하위인지 확인
                if (!IsUnderRuntimeFinalContentInstances(tag.transform))
                    continue;

                // RuntimeSpawnedInstances 하위 제외
                if (IsUnderRuntimeSpawnedInstances(tag.transform))
                    continue;

                // RuntimeSpawnInstances 하위 preview 제외
                if (IsUnderRuntimeSpawnPreviewInstances(tag.transform))
                    continue;

                // WorldMapRuntimePlaceholderBinding 제외
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // WorldMapZoneContentMarker 제외
                if (tag.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // WorldMapRuntimeSpawnInstanceTag 제외
                if (tag.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                    continue;

                // WorldMapRuntimeSpawnGameplayAdapter 제외
                if (tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                    continue;

                // WorldMapRuntimeFinalContentContract 제외
                if (tag.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    continue;

                // WorldMapRuntimeFinalContentPlaceholderTag는 제외 조건으로 사용하지 않음
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
        /// 지정한 Transform의 ancestor 중 "RuntimeFinalContentInstances" 이름을 가진 object가 있는지 확인한다.
        /// RuntimeFinalContentInstances 하위 final content instance만 cache에 포함하기 위한 보조 필터.
        /// </summary>
        /// <param name="target">확인할 Transform</param>
        /// <returns>RuntimeFinalContentInstances 하위이면 true</returns>
        private static bool IsUnderRuntimeFinalContentInstances(Transform target)
        {
            return IsUnderNamedAncestor(target, "RuntimeFinalContentInstances");
        }

        /// <summary>
        /// 지정한 Transform의 ancestor 중 "RuntimeSpawnedInstances" 이름을 가진 object가 있는지 확인한다.
        /// RuntimeSpawnedInstances 하위 source runtime instance를 제외하기 위한 보조 필터.
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
            sb.AppendLine("=== WorldMapRuntimeFinalContentInstanceRegistry Cache Summary ===");
            sb.AppendLine($"Total Final Content Instances: {_allInstances.Count}");
            sb.AppendLine($"Unique Zones: {_byZoneId.Count}");
            sb.AppendLine($"Unique Categories: {_byRuntimeCategory.Count}");
            sb.AppendLine($"Unique FinalContentKinds: {_byFinalContentKind.Count}");
            sb.AppendLine($"Unique AdapterKinds: {_byAdapterKind.Count}");
            sb.AppendLine($"Unique RequirementIds: {_byRequirementId.Count}");
            sb.AppendLine($"Unique ProfileIds: {_byProfileId.Count}");
            sb.AppendLine($"Unique SourceMarkerIds: {_bySourceMarkerId.Count}");
            sb.AppendLine($"Duplicate SourceMarkerIds: {_duplicateSourceMarkerIds.Count}");

            // Category breakdown
            sb.AppendLine("--- Category Breakdown ---");
            foreach (var kvp in _byRuntimeCategory)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // FinalContentKind breakdown
            sb.AppendLine("--- FinalContentKind Breakdown ---");
            foreach (var kvp in _byFinalContentKind)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // Zone count breakdown
            sb.AppendLine("--- Zone Count Breakdown ---");
            foreach (var kvp in _byZoneId)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // Duplicate SourceMarkerIds 상세
            if (_duplicateSourceMarkerIds.Count > 0)
            {
                sb.AppendLine("--- Duplicate SourceMarkerIds ---");
                foreach (string dupId in _duplicateSourceMarkerIds)
                {
                    sb.AppendLine($"  {dupId}");
                }
            }

            return sb.ToString();
        }
    }
}
