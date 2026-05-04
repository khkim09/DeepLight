using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Gameplay binding 전용 Registry.
    /// RuntimeFinalContentInstances 하위의 WorldMapRuntimeFinalContentGameplayBinding을 수집하고
    /// Dictionary/List cache 기반의 Query API를 제공한다.
    /// 매 프레임 FindObjectsByType을 사용하지 않고, RebuildCacheFromRoot() 호출 시에만 scene scan한다.
    /// K-1 Registry / L-2 Adapter Registry / M-6 Instance Registry 패턴과 동일한 구조로 작성.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    public class WorldMapRuntimeFinalContentGameplayBindingRegistry : MonoBehaviour
    {
        // ===== Runtime Initialization Options =====

        [SerializeField, Tooltip("Awake에서 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 cache가 비어 있으면 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnEnableIfEmpty = true;

        [SerializeField, Tooltip("GetComponentsInChildren에서 includeInactive 사용 여부")]
        private bool includeInactiveBindings = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization = false;

        // ===== Internal State =====

        /// <summary>중복 초기화 방지를 위한 내부 상태. ClearCache() 호출 시 false로 되돌아감.</summary>
        private bool hasRuntimeInitialized;

        // ===== Cache Fields =====

        /// <summary>모든 gameplay binding의 flat list cache</summary>
        private List<WorldMapRuntimeFinalContentGameplayBinding> _allBindings = new List<WorldMapRuntimeFinalContentGameplayBinding>();

        /// <summary>중복 SourceMarkerId 목록 (internal 관리용)</summary>
        private List<string> _duplicateSourceMarkerIds = new List<string>();

        /// <summary>ZoneId -> binding 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>> _byZoneId = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();

        /// <summary>RuntimeCategory -> binding 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>> _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();

        /// <summary>FinalContentKind -> binding 목록 cache</summary>
        private Dictionary<WorldMapRuntimeFinalContentKind, List<WorldMapRuntimeFinalContentGameplayBinding>> _byFinalContentKind = new Dictionary<WorldMapRuntimeFinalContentKind, List<WorldMapRuntimeFinalContentGameplayBinding>>();

        /// <summary>BindingKind -> binding 목록 cache</summary>
        private Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, List<WorldMapRuntimeFinalContentGameplayBinding>> _byBindingKind = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, List<WorldMapRuntimeFinalContentGameplayBinding>>();

        /// <summary>AdapterKind -> binding 목록 cache</summary>
        private Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeFinalContentGameplayBinding>> _byAdapterKind = new Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeFinalContentGameplayBinding>>();

        /// <summary>RequirementId -> binding 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>> _byRequirementId = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();

        /// <summary>ProfileId -> binding 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>> _byProfileId = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();

        /// <summary>SourceMarkerId -> binding (첫 번째만, 중복 시 첫 번째 우선)</summary>
        private Dictionary<string, WorldMapRuntimeFinalContentGameplayBinding> _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeFinalContentGameplayBinding>();

        // ===== Public Read-only Properties =====

        /// <summary>모든 gameplay binding의 읽기 전용 목록</summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> AllBindings => _allBindings;

        /// <summary>중복 SourceMarkerId 목록 (읽기 전용)</summary>
        public IReadOnlyList<string> DuplicateSourceMarkerIds => _duplicateSourceMarkerIds;

        /// <summary>cache된 gameplay binding 총 개수</summary>
        public int Count => _allBindings.Count;

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentGameplayBindingRegistry] Awake: rebuildOnAwake is true. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: rebuildOnEnableIfEmpty가 true이고 AllBindings count가 0이면
        /// RebuildCacheFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (rebuildOnEnableIfEmpty && _allBindings.Count == 0)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentGameplayBindingRegistry] OnEnable: cache is empty. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Query API =====

        /// <summary>
        /// 지정한 ZoneId에 속한 gameplay binding 목록을 반환한다.
        /// 해당 ZoneId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByZoneId(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 gameplay binding 목록을 반환한다.
        /// 해당 Category가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByRuntimeCategory(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory))
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            if (_byRuntimeCategory.TryGetValue(runtimeCategory, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();
        }

        /// <summary>
        /// 지정한 FinalContentKind에 속한 gameplay binding 목록을 반환한다.
        /// 해당 FinalContentKind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByFinalContentKind(WorldMapRuntimeFinalContentKind kind)
        {
            if (_byFinalContentKind.TryGetValue(kind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();
        }

        /// <summary>
        /// 지정한 BindingKind에 속한 gameplay binding 목록을 반환한다.
        /// 해당 BindingKind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByBindingKind(WorldMapRuntimeFinalContentGameplayBindingKind kind)
        {
            if (_byBindingKind.TryGetValue(kind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 gameplay binding 목록을 반환한다.
        /// 해당 AdapterKind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByAdapterKind(WorldMapRuntimeGameplayAdapterKind kind)
        {
            if (_byAdapterKind.TryGetValue(kind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();
        }

        /// <summary>
        /// 지정한 RequirementId에 속한 gameplay binding 목록을 반환한다.
        /// 해당 RequirementId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByRequirementId(string requirementId)
        {
            if (string.IsNullOrEmpty(requirementId))
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            if (_byRequirementId.TryGetValue(requirementId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 gameplay binding 목록을 반환한다.
        /// 해당 ProfileId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            if (_byProfileId.TryGetValue(profileId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();
        }

        /// <summary>
        /// SourceMarkerId로 gameplay binding을 조회한다.
        /// 중복 SourceMarkerId가 있는 경우 첫 번째 binding이 반환된다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeFinalContentGameplayBinding result)
        {
            if (string.IsNullOrEmpty(sourceMarkerId))
            {
                result = null;
                return false;
            }

            return _bySourceMarkerId.TryGetValue(sourceMarkerId, out result);
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 gameplay binding 개수를 반환한다.
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
        /// 지정한 RuntimeCategory에 속한 gameplay binding 개수를 반환한다.
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
        /// 지정한 FinalContentKind에 속한 gameplay binding 개수를 반환한다.
        /// </summary>
        public int CountByFinalContentKind(WorldMapRuntimeFinalContentKind kind)
        {
            if (_byFinalContentKind.TryGetValue(kind, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 BindingKind에 속한 gameplay binding 개수를 반환한다.
        /// </summary>
        public int CountByBindingKind(WorldMapRuntimeFinalContentGameplayBindingKind kind)
        {
            if (_byBindingKind.TryGetValue(kind, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 gameplay binding 개수를 반환한다.
        /// </summary>
        public int CountByAdapterKind(WorldMapRuntimeGameplayAdapterKind kind)
        {
            if (_byAdapterKind.TryGetValue(kind, out var list))
                return list.Count;

            return 0;
        }

        // ===== Cache Management =====

        /// <summary>
        /// Scene 내 모든 gameplay binding을 스캔하여 cache를 재구축한다.
        /// 내부적으로 RebuildCacheFromRoot(transform)을 호출한다.
        /// 수집 기준: WorldMapRuntimeFinalContentGameplayBinding 존재 && IsReady() == true
        ///   && RuntimeFinalContentInstances 하위.
        /// 매 프레임 호출 금지. RebuildCacheFromScene() 호출 시에만 scene scan.
        /// </summary>
        public void RebuildCacheFromScene()
        {
            // 내부적으로 자기 transform 기준 RebuildCacheFromRoot 호출
            RebuildCacheFromRoot(transform);
        }

        /// <summary>
        /// 지정한 root Transform 하위에서 모든 gameplay binding을 스캔하여 cache를 재구축한다.
        /// root가 null이면 자기 transform으로 fallback한다.
        /// includeInactive 옵션에 따라 비활성화된 object도 포함 가능.
        /// </summary>
        /// <param name="root">검색 기준이 되는 root Transform. null이면 this.transform 사용.</param>
        public void RebuildCacheFromRoot(Transform root)
        {
            // root가 null이면 자기 transform으로 fallback
            if (root == null)
                root = transform;

            // 1. root 하위 모든 WorldMapRuntimeFinalContentGameplayBinding을 includeInactive 옵션에 따라 수집
            WorldMapRuntimeFinalContentGameplayBinding[] allBindings = root.GetComponentsInChildren<WorldMapRuntimeFinalContentGameplayBinding>(includeInactiveBindings);

            // 2. cache 컨테이너 초기화
            _allBindings = new List<WorldMapRuntimeFinalContentGameplayBinding>();
            _duplicateSourceMarkerIds = new List<string>();
            _byZoneId = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();
            _byRuntimeCategory = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();
            _byFinalContentKind = new Dictionary<WorldMapRuntimeFinalContentKind, List<WorldMapRuntimeFinalContentGameplayBinding>>();
            _byBindingKind = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, List<WorldMapRuntimeFinalContentGameplayBinding>>();
            _byAdapterKind = new Dictionary<WorldMapRuntimeGameplayAdapterKind, List<WorldMapRuntimeFinalContentGameplayBinding>>();
            _byRequirementId = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();
            _byProfileId = new Dictionary<string, List<WorldMapRuntimeFinalContentGameplayBinding>>();
            _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeFinalContentGameplayBinding>();

            // 3. 각 binding을 순회하며 유효한 binding만 수집
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                // null / destroyed object 체크
                if (binding == null || binding.gameObject == null)
                    continue;

                // ===== 포함 조건 =====

                // (1) IsReady() == true인 것만 포함
                if (!binding.IsReady())
                    continue;

                // (2) RuntimeFinalContentInstances 하위가 아니면 제외
                if (!IsUnderRuntimeFinalContentInstances(binding.transform))
                    continue;

                // ===== 제외 조건 =====

                // (3) RuntimeSpawnedInstances 하위 source runtime instance 제외
                if (IsUnderRuntimeSpawnedInstances(binding.transform))
                    continue;

                // (4) RuntimeSpawnInstances 하위 preview instance 제외
                if (IsUnderRuntimeSpawnPreviewInstances(binding.transform))
                    continue;

                // (5) WorldMapRuntimePlaceholderBinding이 있는 object 제외
                if (binding.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // (6) WorldMapZoneContentMarker가 있는 object 제외
                if (binding.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // (7) WorldMapRuntimeSpawnInstanceTag가 있는 object 제외
                if (binding.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                    continue;

                // (8) WorldMapRuntimeSpawnGameplayAdapter가 있는 object 제외
                if (binding.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                    continue;

                // (9) WorldMapRuntimeFinalContentContract가 있는 object 제외
                if (binding.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    continue;

                // (10) WorldMapRuntimeFinalContentInstanceTag가 없으면 제외
                if (binding.GetComponent<WorldMapRuntimeFinalContentInstanceTag>() == null)
                    continue;

                // (11) WorldMapRuntimeFinalContentPlaceholderTag 존재 여부는 제외 조건으로 사용하지 않음.
                //     generated placeholder final content는 정상 gameplay binding 대상이다.

                // ===== 유효한 gameplay binding =====
                _allBindings.Add(binding);

                // ZoneId 기준 추가
                string zoneId = binding.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_byZoneId.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapRuntimeFinalContentGameplayBinding>();
                        _byZoneId[zoneId] = zoneList;
                    }
                    zoneList.Add(binding);
                }

                // RuntimeCategory 기준 추가
                string category = binding.RuntimeCategory;
                if (!string.IsNullOrEmpty(category))
                {
                    if (!_byRuntimeCategory.TryGetValue(category, out var catList))
                    {
                        catList = new List<WorldMapRuntimeFinalContentGameplayBinding>();
                        _byRuntimeCategory[category] = catList;
                    }
                    catList.Add(binding);
                }

                // FinalContentKind 기준 추가
                WorldMapRuntimeFinalContentKind finalContentKind = binding.FinalContentKind;
                if (!_byFinalContentKind.TryGetValue(finalContentKind, out var kindList))
                {
                    kindList = new List<WorldMapRuntimeFinalContentGameplayBinding>();
                    _byFinalContentKind[finalContentKind] = kindList;
                }
                kindList.Add(binding);

                // BindingKind 기준 추가
                WorldMapRuntimeFinalContentGameplayBindingKind bindingKind = binding.BindingKind;
                if (!_byBindingKind.TryGetValue(bindingKind, out var bindingKindList))
                {
                    bindingKindList = new List<WorldMapRuntimeFinalContentGameplayBinding>();
                    _byBindingKind[bindingKind] = bindingKindList;
                }
                bindingKindList.Add(binding);

                // AdapterKind 기준 추가
                WorldMapRuntimeGameplayAdapterKind adapterKind = binding.AdapterKind;
                if (!_byAdapterKind.TryGetValue(adapterKind, out var adapterList))
                {
                    adapterList = new List<WorldMapRuntimeFinalContentGameplayBinding>();
                    _byAdapterKind[adapterKind] = adapterList;
                }
                adapterList.Add(binding);

                // RequirementId 기준 추가
                string requirementId = binding.RequirementId;
                if (!string.IsNullOrEmpty(requirementId))
                {
                    if (!_byRequirementId.TryGetValue(requirementId, out var reqList))
                    {
                        reqList = new List<WorldMapRuntimeFinalContentGameplayBinding>();
                        _byRequirementId[requirementId] = reqList;
                    }
                    reqList.Add(binding);
                }

                // ProfileId 기준 추가
                string profileId = binding.ProfileId;
                if (!string.IsNullOrEmpty(profileId))
                {
                    if (!_byProfileId.TryGetValue(profileId, out var profList))
                    {
                        profList = new List<WorldMapRuntimeFinalContentGameplayBinding>();
                        _byProfileId[profileId] = profList;
                    }
                    profList.Add(binding);
                }

                // SourceMarkerId 기준 추가 (중복 시 첫 번째만 유지)
                string sourceMarkerId = binding.SourceMarkerId;
                if (!string.IsNullOrEmpty(sourceMarkerId))
                {
                    if (_bySourceMarkerId.ContainsKey(sourceMarkerId))
                    {
                        // 중복 발생 -> duplicate 목록에 추가 (아직 없으면)
                        if (!_duplicateSourceMarkerIds.Contains(sourceMarkerId))
                        {
                            _duplicateSourceMarkerIds.Add(sourceMarkerId);
                            UnityEngine.Debug.LogWarning($"[WorldMapRuntimeFinalContentGameplayBindingRegistry] Duplicate SourceMarkerId found: '{sourceMarkerId}' (Zone: {zoneId}, Category: {category}). First instance will be used for TryGetBySourceMarkerId.");
                        }
                    }
                    else
                    {
                        _bySourceMarkerId[sourceMarkerId] = binding;
                    }
                }
            }

            // 4. 결과 로그
            UnityEngine.Debug.Log($"[WorldMapRuntimeFinalContentGameplayBindingRegistry] RebuildCacheFromRoot complete. " +
                $"Gameplay bindings: {_allBindings.Count}, " +
                $"Zones: {_byZoneId.Count}, " +
                $"Categories: {_byRuntimeCategory.Count}, " +
                $"FinalContentKinds: {_byFinalContentKind.Count}, " +
                $"BindingKinds: {_byBindingKind.Count}, " +
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
            _allBindings.Clear();
            _duplicateSourceMarkerIds.Clear();
            _byZoneId.Clear();
            _byRuntimeCategory.Clear();
            _byFinalContentKind.Clear();
            _byBindingKind.Clear();
            _byAdapterKind.Clear();
            _byRequirementId.Clear();
            _byProfileId.Clear();
            _bySourceMarkerId.Clear();

            // 중복 초기화 방지 상태도 초기화
            hasRuntimeInitialized = false;

            UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentGameplayBindingRegistry] Cache cleared.");
        }

        // ===== Public Static Helper: Scene Gameplay Binding Count =====

        /// <summary>
        /// RebuildCacheFromRoot와 동일한 필터 로직으로 scene 내 gameplay binding 개수를 계산한다.
        /// Validate 등에서 RebuildCache와 scene count의 필터 로직이 달라지지 않도록 공용으로 사용한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>필터를 통과한 gameplay binding 개수</returns>
        public static int CountSceneGameplayBindings(Transform root)
        {
            if (root == null)
                return 0;

            WorldMapRuntimeFinalContentGameplayBinding[] allBindings = root.GetComponentsInChildren<WorldMapRuntimeFinalContentGameplayBinding>(true);
            int count = 0;

            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || binding.gameObject == null)
                    continue;

                // IsReady() == true인 것만 포함
                if (!binding.IsReady())
                    continue;

                // RuntimeFinalContentInstances 하위인지 확인
                if (!IsUnderRuntimeFinalContentInstances(binding.transform))
                    continue;

                // RuntimeSpawnedInstances 하위 제외
                if (IsUnderRuntimeSpawnedInstances(binding.transform))
                    continue;

                // RuntimeSpawnInstances 하위 preview 제외
                if (IsUnderRuntimeSpawnPreviewInstances(binding.transform))
                    continue;

                // WorldMapRuntimePlaceholderBinding 제외
                if (binding.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // WorldMapZoneContentMarker 제외
                if (binding.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // WorldMapRuntimeSpawnInstanceTag 제외
                if (binding.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                    continue;

                // WorldMapRuntimeSpawnGameplayAdapter 제외
                if (binding.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                    continue;

                // WorldMapRuntimeFinalContentContract 제외
                if (binding.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    continue;

                // WorldMapRuntimeFinalContentInstanceTag가 없으면 제외
                if (binding.GetComponent<WorldMapRuntimeFinalContentInstanceTag>() == null)
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
        /// RuntimeFinalContentInstances 하위 binding만 cache에 포함하기 위한 보조 필터.
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
            sb.AppendLine("=== WorldMapRuntimeFinalContentGameplayBindingRegistry Cache Summary ===");
            sb.AppendLine($"Total Gameplay Bindings: {_allBindings.Count}");
            sb.AppendLine($"Unique Zones: {_byZoneId.Count}");
            sb.AppendLine($"Unique Categories: {_byRuntimeCategory.Count}");
            sb.AppendLine($"Unique FinalContentKinds: {_byFinalContentKind.Count}");
            sb.AppendLine($"Unique BindingKinds: {_byBindingKind.Count}");
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

            // BindingKind breakdown
            sb.AppendLine("--- BindingKind Breakdown ---");
            foreach (var kvp in _byBindingKind)
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
