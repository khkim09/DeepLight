using System.Collections.Generic;
using System.Text;
using Project.Gameplay.World.Content;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// RuntimeFinalContentInstances 하위에 부착된 WorldMapRuntimeHarvestInteractionTargetAdapter를
    /// 수집해 cache/index를 구축한다.
    /// RuntimeSpawnedInstances / RuntimeSpawnInstances preview / RuntimePlaceholder / marker object는 제외한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    public class WorldMapRuntimeHarvestInteractionTargetRegistry : MonoBehaviour
    {
        // ===== Runtime Initialization Options =====

        [SerializeField, Tooltip("Awake에서 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 cache가 비어 있으면 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnEnableIfEmpty = true;

        [SerializeField, Tooltip("GetComponentsInChildren에서 includeInactive 사용 여부")]
        private bool includeInactiveTargets = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization = false;

        // ===== Internal State =====

        /// <summary>중복 초기화 방지를 위한 내부 상태. ClearCache() 호출 시 false로 되돌아감.</summary>
        private bool hasRuntimeInitialized;

        // ===== Cache Fields =====

        /// <summary>모든 target adapter의 flat list cache</summary>
        private List<WorldMapRuntimeHarvestInteractionTargetAdapter> _allTargets = new List<WorldMapRuntimeHarvestInteractionTargetAdapter>();

        /// <summary>중복 SourceMarkerId 목록 (internal 관리용)</summary>
        private List<string> _duplicateSourceMarkerIds = new List<string>();

        /// <summary>ZoneId -> target adapter 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>> _byZoneId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();

        /// <summary>RuntimeKey -> target adapter 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>> _byRuntimeKey = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();

        /// <summary>ProfileId -> target adapter 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>> _byProfileId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();

        /// <summary>InteractionKind -> target adapter 목록 cache</summary>
        private Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, List<WorldMapRuntimeHarvestInteractionTargetAdapter>> _byInteractionKind = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();

        /// <summary>TargetKind -> target adapter 목록 cache</summary>
        private Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, List<WorldMapRuntimeHarvestInteractionTargetAdapter>> _byTargetKind = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();

        /// <summary>SourceMarkerId -> target adapter (첫 번째만, 중복 시 첫 번째 우선)</summary>
        private Dictionary<string, WorldMapRuntimeHarvestInteractionTargetAdapter> _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeHarvestInteractionTargetAdapter>();

        // ===== Public Read-only Properties =====

        /// <summary>모든 target adapter의 읽기 전용 목록</summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> AllTargets => _allTargets;

        /// <summary>중복 SourceMarkerId 목록 (읽기 전용)</summary>
        public IReadOnlyList<string> DuplicateSourceMarkerIds => _duplicateSourceMarkerIds;

        /// <summary>cache된 target adapter 총 개수</summary>
        public int Count => _allTargets.Count;

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionTargetRegistry] Awake: rebuildOnAwake is true. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: rebuildOnEnableIfEmpty가 true이고 AllTargets count가 0이면
        /// RebuildCacheFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (rebuildOnEnableIfEmpty && _allTargets.Count == 0)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionTargetRegistry] OnEnable: cache is empty. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Query API =====

        /// <summary>
        /// 지정한 ZoneId에 속한 target adapter 목록을 반환한다.
        /// 해당 ZoneId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> GetByZoneId(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();
        }

        /// <summary>
        /// 지정한 RuntimeKey에 속한 target adapter 목록을 반환한다.
        /// 해당 RuntimeKey가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> GetByRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();

            if (_byRuntimeKey.TryGetValue(runtimeKey, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 target adapter 목록을 반환한다.
        /// 해당 ProfileId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> GetByProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();

            if (_byProfileId.TryGetValue(profileId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();
        }

        /// <summary>
        /// 지정한 InteractionKind에 속한 target adapter 목록을 반환한다.
        /// 해당 Kind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind kind)
        {
            if (_byInteractionKind.TryGetValue(kind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();
        }

        /// <summary>
        /// 지정한 TargetKind에 속한 target adapter 목록을 반환한다.
        /// 해당 Kind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> GetByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind kind)
        {
            if (_byTargetKind.TryGetValue(kind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetAdapter>();
        }

        /// <summary>
        /// SourceMarkerId로 target adapter를 조회한다.
        /// 중복 SourceMarkerId가 있는 경우 첫 번째 adapter가 반환된다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeHarvestInteractionTargetAdapter result)
        {
            if (string.IsNullOrEmpty(sourceMarkerId))
            {
                result = null;
                return false;
            }

            return _bySourceMarkerId.TryGetValue(sourceMarkerId, out result);
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 target adapter 개수를 반환한다.
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
        /// 지정한 RuntimeKey에 속한 target adapter 개수를 반환한다.
        /// </summary>
        public int CountByRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return 0;

            if (_byRuntimeKey.TryGetValue(runtimeKey, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 InteractionKind에 속한 target adapter 개수를 반환한다.
        /// </summary>
        public int CountByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind kind)
        {
            if (_byInteractionKind.TryGetValue(kind, out var list))
                return list.Count;

            return 0;
        }

        /// <summary>
        /// 지정한 TargetKind에 속한 target adapter 개수를 반환한다.
        /// </summary>
        public int CountByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind kind)
        {
            if (_byTargetKind.TryGetValue(kind, out var list))
                return list.Count;

            return 0;
        }

        // ===== Cache Management =====

        /// <summary>
        /// Scene 내 모든 target adapter를 스캔하여 cache를 재구축한다.
        /// 내부적으로 RebuildCacheFromRoot(transform)을 호출한다.
        /// </summary>
        public void RebuildCacheFromScene()
        {
            RebuildCacheFromRoot(transform);
        }

        /// <summary>
        /// 지정한 root Transform 하위에서 WorldMapRuntimeHarvestInteractionTargetAdapter를 찾아
        /// cache를 재구축한다. root가 null이면 자기 transform으로 fallback한다.
        /// RuntimeFinalContentInstances 하위 object만 포함하며,
        /// RuntimeSpawnedInstances / RuntimeSpawnInstances preview / RuntimePlaceholder / marker object는 제외한다.
        /// </summary>
        /// <param name="root">검색 기준이 되는 root Transform. null이면 this.transform 사용.</param>
        public void RebuildCacheFromRoot(Transform root)
        {
            // root가 null이면 자기 transform으로 fallback
            if (root == null)
                root = transform;

            // 1. root 하위에서 모든 WorldMapRuntimeHarvestInteractionTargetAdapter를 찾는다.
            WorldMapRuntimeHarvestInteractionTargetAdapter[] allAdapters = root.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionTargetAdapter>(includeInactiveTargets);

            // 2. cache 컨테이너 초기화
            _allTargets = new List<WorldMapRuntimeHarvestInteractionTargetAdapter>();
            _duplicateSourceMarkerIds = new List<string>();
            _byZoneId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();
            _byRuntimeKey = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();
            _byProfileId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();
            _byInteractionKind = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();
            _byTargetKind = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, List<WorldMapRuntimeHarvestInteractionTargetAdapter>>();
            _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeHarvestInteractionTargetAdapter>();

            // 3. 각 adapter를 필터링하여 cache에 추가
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in allAdapters)
            {
                if (adapter == null)
                    continue;

                // IsReady()==true인 adapter만 cache에 포함
                if (!adapter.IsReady())
                    continue;

                // RuntimeFinalContentInstances 하위 object만 포함
                if (!IsUnderRuntimeFinalContentInstances(adapter.transform, root))
                    continue;

                _allTargets.Add(adapter);

                // ZoneId 기준 추가
                string zoneId = adapter.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_byZoneId.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                        _byZoneId[zoneId] = zoneList;
                    }
                    zoneList.Add(adapter);
                }

                // RuntimeKey 기준 추가
                string runtimeKey = adapter.RuntimeKey;
                if (!string.IsNullOrEmpty(runtimeKey))
                {
                    if (!_byRuntimeKey.TryGetValue(runtimeKey, out var keyList))
                    {
                        keyList = new List<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                        _byRuntimeKey[runtimeKey] = keyList;
                    }
                    keyList.Add(adapter);
                }

                // ProfileId 기준 추가
                string profileId = adapter.ProfileId;
                if (!string.IsNullOrEmpty(profileId))
                {
                    if (!_byProfileId.TryGetValue(profileId, out var profList))
                    {
                        profList = new List<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                        _byProfileId[profileId] = profList;
                    }
                    profList.Add(adapter);
                }

                // InteractionKind 기준 추가
                WorldMapRuntimeHarvestInteractionCandidateKind interactionKind = adapter.InteractionKind;
                if (!_byInteractionKind.TryGetValue(interactionKind, out var kindList))
                {
                    kindList = new List<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                    _byInteractionKind[interactionKind] = kindList;
                }
                kindList.Add(adapter);

                // TargetKind 기준 추가
                WorldMapRuntimeHarvestInteractionTargetKind targetKind = adapter.TargetKind;
                if (!_byTargetKind.TryGetValue(targetKind, out var targetKindList))
                {
                    targetKindList = new List<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                    _byTargetKind[targetKind] = targetKindList;
                }
                targetKindList.Add(adapter);

                // SourceMarkerId 기준 추가 (중복 시 첫 번째만 유지)
                string sourceMarkerId = adapter.SourceMarkerId;
                if (!string.IsNullOrEmpty(sourceMarkerId))
                {
                    if (_bySourceMarkerId.ContainsKey(sourceMarkerId))
                    {
                        // 중복 발생 -> duplicate 목록에 추가 (아직 없으면)
                        if (!_duplicateSourceMarkerIds.Contains(sourceMarkerId))
                        {
                            _duplicateSourceMarkerIds.Add(sourceMarkerId);
                            UnityEngine.Debug.LogWarning($"[WorldMapRuntimeHarvestInteractionTargetRegistry] Duplicate SourceMarkerId found: '{sourceMarkerId}' (Zone: {zoneId}). First instance will be used for TryGetBySourceMarkerId.");
                        }
                    }
                    else
                    {
                        _bySourceMarkerId[sourceMarkerId] = adapter;
                    }
                }
            }

            // 4. 결과 로그
            UnityEngine.Debug.Log($"[WorldMapRuntimeHarvestInteractionTargetRegistry] RebuildCacheFromRoot complete. " +
                $"Target adapters: {_allTargets.Count}, " +
                $"Zones: {_byZoneId.Count}, " +
                $"RuntimeKeys: {_byRuntimeKey.Count}, " +
                $"ProfileIds: {_byProfileId.Count}, " +
                $"InteractionKinds: {_byInteractionKind.Count}, " +
                $"TargetKinds: {_byTargetKind.Count}, " +
                $"SourceMarkerIds: {_bySourceMarkerId.Count}, " +
                $"Duplicate SourceMarkerIds: {_duplicateSourceMarkerIds.Count}");
        }

        /// <summary>
        /// 모든 cache를 비운다. hasRuntimeInitialized도 false로 되돌린다.
        /// </summary>
        public void ClearCache()
        {
            _allTargets.Clear();
            _duplicateSourceMarkerIds.Clear();
            _byZoneId.Clear();
            _byRuntimeKey.Clear();
            _byProfileId.Clear();
            _byInteractionKind.Clear();
            _byTargetKind.Clear();
            _bySourceMarkerId.Clear();

            hasRuntimeInitialized = false;

            UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionTargetRegistry] Cache cleared.");
        }

        // ===== Public Static Helper: Scene Harvest Interaction Target Count =====

        /// <summary>
        /// RebuildCacheFromRoot와 동일한 로직으로 scene 내 harvest interaction target adapter의 개수를 계산한다.
        /// Validate 등에서 RebuildCache와 scene count의 필터 로직이 달라지지 않도록 공용으로 사용한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>필터를 통과한 target adapter 개수</returns>
        public static int CountSceneHarvestInteractionTargets(Transform root)
        {
            if (root == null)
                return 0;

            // root 하위에서 모든 WorldMapRuntimeHarvestInteractionTargetAdapter를 찾는다.
            WorldMapRuntimeHarvestInteractionTargetAdapter[] allAdapters = root.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionTargetAdapter>(true);
            int count = 0;

            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in allAdapters)
            {
                if (adapter == null)
                    continue;

                if (!adapter.IsReady())
                    continue;

                if (!IsUnderRuntimeFinalContentInstances(adapter.transform, root))
                    continue;

                count++;
            }

            return count;
        }

        // ===== Private Helpers =====

        /// <summary>
        /// 지정한 transform이 RuntimeFinalContentInstances 하위에 있는지 확인한다.
        /// RuntimeSpawnedInstances / RuntimeSpawnInstances preview / RuntimePlaceholder / marker object는 제외한다.
        /// </summary>
        /// <param name="target">확인할 transform</param>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>RuntimeFinalContentInstances 하위면 true</returns>
        private static bool IsUnderRuntimeFinalContentInstances(Transform target, Transform root)
        {
            if (target == null || root == null)
                return false;

            // 부모 체인을 따라 올라가면서 제외 조건 확인
            Transform current = target;
            while (current != null && current != root)
            {
                string name = current.name;

                // RuntimeSpawnedInstances 하위 object 제외
                if (name == "RuntimeSpawnedInstances")
                    return false;

                // RuntimeSpawnInstances preview object 제외
                if (name == "RuntimeSpawnInstances")
                    return false;

                // RuntimePlaceholder object 제외
                if (name == "RuntimePlaceholder")
                    return false;

                // WorldMapZoneContentMarker object 제외
                if (current.GetComponent<WorldMapZoneContentMarker>() != null)
                    return false;

                // WorldMapRuntimeSpawnInstanceTag object 제외
                if (current.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                    return false;

                // WorldMapRuntimeSpawnGameplayAdapter object 제외
                if (current.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                    return false;

                // WorldMapRuntimeFinalContentContract object 제외
                if (current.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    return false;

                // RuntimeFinalContentInstances를 찾으면 true 반환
                if (name == "RuntimeFinalContentInstances")
                    return true;

                current = current.parent;
            }

            // root까지 올라갔는데 RuntimeFinalContentInstances를 찾지 못함
            return false;
        }

        // ===== Debug / Validation Helpers =====

        /// <summary>
        /// 현재 cache 상태의 요약 문자열을 반환한다. (디버그/검증용)
        /// </summary>
        public string GetCacheSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WorldMapRuntimeHarvestInteractionTargetRegistry Cache Summary ===");
            sb.AppendLine($"Total Target Adapters: {_allTargets.Count}");
            sb.AppendLine($"Unique Zones: {_byZoneId.Count}");
            sb.AppendLine($"Unique RuntimeKeys: {_byRuntimeKey.Count}");
            sb.AppendLine($"Unique ProfileIds: {_byProfileId.Count}");
            sb.AppendLine($"Unique InteractionKinds: {_byInteractionKind.Count}");
            sb.AppendLine($"Unique TargetKinds: {_byTargetKind.Count}");
            sb.AppendLine($"Unique SourceMarkerIds: {_bySourceMarkerId.Count}");
            sb.AppendLine($"Duplicate SourceMarkerIds: {_duplicateSourceMarkerIds.Count}");

            // Zone breakdown
            sb.AppendLine("--- Zone Breakdown ---");
            foreach (var kvp in _byZoneId)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // RuntimeKey breakdown
            sb.AppendLine("--- RuntimeKey Breakdown ---");
            foreach (var kvp in _byRuntimeKey)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // InteractionKind breakdown
            sb.AppendLine("--- InteractionKind Breakdown ---");
            foreach (var kvp in _byInteractionKind)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value.Count}");
            }

            // TargetKind breakdown
            sb.AppendLine("--- TargetKind Breakdown ---");
            foreach (var kvp in _byTargetKind)
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
