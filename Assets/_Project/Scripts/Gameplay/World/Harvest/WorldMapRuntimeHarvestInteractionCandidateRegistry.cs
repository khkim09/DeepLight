using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// WorldMapRuntimeHarvestResourceCandidateQueryService에서 수집한 HarvestResource candidate를
    /// WorldMapRuntimeHarvestInteractionCandidate로 변환하여 캐시한다.
    /// RuntimeFinalContentInstances 하위 object를 직접 스캔하지 않고,
    /// 반드시 HarvestResourceCandidateQueryService를 통해 들어온 candidate만 사용한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    public class WorldMapRuntimeHarvestInteractionCandidateRegistry : MonoBehaviour
    {
        // ===== Runtime Initialization Options =====

        [SerializeField, Tooltip("Awake에서 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 cache가 비어 있으면 자동으로 RebuildCacheFromRoot(transform)를 호출할지 여부")]
        private bool rebuildOnEnableIfEmpty = true;

        [SerializeField, Tooltip("GetComponentsInChildren에서 includeInactive 사용 여부")]
        private bool includeInactiveCandidates = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization = false;

        // ===== Internal State =====

        /// <summary>중복 초기화 방지를 위한 내부 상태. ClearCache() 호출 시 false로 되돌아감.</summary>
        private bool hasRuntimeInitialized;

        // ===== Cache Fields =====

        /// <summary>모든 interaction candidate의 flat list cache</summary>
        private List<WorldMapRuntimeHarvestInteractionCandidate> _allCandidates = new List<WorldMapRuntimeHarvestInteractionCandidate>();

        /// <summary>중복 SourceMarkerId 목록 (internal 관리용)</summary>
        private List<string> _duplicateSourceMarkerIds = new List<string>();

        /// <summary>ZoneId -> candidate 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>> _byZoneId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>>();

        /// <summary>RuntimeKey -> candidate 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>> _byRuntimeKey = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>>();

        /// <summary>ProfileId -> candidate 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>> _byProfileId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>>();

        /// <summary>InteractionKind -> candidate 목록 cache</summary>
        private Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, List<WorldMapRuntimeHarvestInteractionCandidate>> _byInteractionKind = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, List<WorldMapRuntimeHarvestInteractionCandidate>>();

        /// <summary>SourceMarkerId -> candidate (첫 번째만, 중복 시 첫 번째 우선)</summary>
        private Dictionary<string, WorldMapRuntimeHarvestInteractionCandidate> _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeHarvestInteractionCandidate>();

        // ===== Public Read-only Properties =====

        /// <summary>모든 interaction candidate의 읽기 전용 목록</summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> AllCandidates => _allCandidates;

        /// <summary>중복 SourceMarkerId 목록 (읽기 전용)</summary>
        public IReadOnlyList<string> DuplicateSourceMarkerIds => _duplicateSourceMarkerIds;

        /// <summary>cache된 interaction candidate 총 개수</summary>
        public int Count => _allCandidates.Count;

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionCandidateRegistry] Awake: rebuildOnAwake is true. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: rebuildOnEnableIfEmpty가 true이고 AllCandidates count가 0이면
        /// RebuildCacheFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (rebuildOnEnableIfEmpty && _allCandidates.Count == 0)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionCandidateRegistry] OnEnable: cache is empty. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Query API =====

        /// <summary>
        /// 지정한 ZoneId에 속한 interaction candidate 목록을 반환한다.
        /// 해당 ZoneId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> GetByZoneId(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionCandidate>();

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionCandidate>();
        }

        /// <summary>
        /// 지정한 RuntimeKey에 속한 interaction candidate 목록을 반환한다.
        /// 해당 RuntimeKey가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> GetByRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionCandidate>();

            if (_byRuntimeKey.TryGetValue(runtimeKey, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionCandidate>();
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 interaction candidate 목록을 반환한다.
        /// 해당 ProfileId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> GetByProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionCandidate>();

            if (_byProfileId.TryGetValue(profileId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionCandidate>();
        }

        /// <summary>
        /// 지정한 InteractionKind에 속한 interaction candidate 목록을 반환한다.
        /// 해당 Kind가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind kind)
        {
            if (_byInteractionKind.TryGetValue(kind, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestInteractionCandidate>();
        }

        /// <summary>
        /// SourceMarkerId로 interaction candidate를 조회한다.
        /// 중복 SourceMarkerId가 있는 경우 첫 번째 candidate가 반환된다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeHarvestInteractionCandidate result)
        {
            if (string.IsNullOrEmpty(sourceMarkerId))
            {
                result = null;
                return false;
            }

            return _bySourceMarkerId.TryGetValue(sourceMarkerId, out result);
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 interaction candidate 개수를 반환한다.
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
        /// 지정한 RuntimeKey에 속한 interaction candidate 개수를 반환한다.
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
        /// 지정한 InteractionKind에 속한 interaction candidate 개수를 반환한다.
        /// </summary>
        public int CountByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind kind)
        {
            if (_byInteractionKind.TryGetValue(kind, out var list))
                return list.Count;

            return 0;
        }

        // ===== Cache Management =====

        /// <summary>
        /// Scene 내 모든 interaction candidate를 스캔하여 cache를 재구축한다.
        /// 내부적으로 RebuildCacheFromRoot(transform)을 호출한다.
        /// </summary>
        public void RebuildCacheFromScene()
        {
            RebuildCacheFromRoot(transform);
        }

        /// <summary>
        /// 지정한 root Transform 하위에서 WorldMapRuntimeHarvestResourceCandidateQueryService를 찾고,
        /// GetAll()로 모든 HarvestResourceCandidate를 가져와 interaction candidate cache를 재구축한다.
        /// root가 null이면 자기 transform으로 fallback한다.
        /// 직접 scene object scan으로 RuntimeFinalContentInstances 하위 object를 찾지 않는다.
        /// 반드시 HarvestResourceCandidateQueryService를 통해 들어온 candidate만 사용한다.
        /// </summary>
        /// <param name="root">검색 기준이 되는 root Transform. null이면 this.transform 사용.</param>
        public void RebuildCacheFromRoot(Transform root)
        {
            // root가 null이면 자기 transform으로 fallback
            if (root == null)
                root = transform;

            // 1. root 하위에서 WorldMapRuntimeHarvestResourceCandidateQueryService를 찾는다.
            WorldMapRuntimeHarvestResourceCandidateQueryService queryService = root.GetComponentInChildren<WorldMapRuntimeHarvestResourceCandidateQueryService>(includeInactiveCandidates);

            // 2. 없으면 root에 추가/초기화
            if (queryService == null)
            {
                queryService = root.gameObject.AddComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>();
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionCandidateRegistry] Added WorldMapRuntimeHarvestResourceCandidateQueryService to root.");
            }

            // 3. QueryService 초기화 (registry가 없으면 생성, cache가 비어 있으면 rebuild)
            queryService.TryInitializeFromRoot(root);

            // 4. QueryService.GetAll()로 HarvestResourceCandidate 64개를 가져온다.
            IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> sourceCandidates = queryService.GetAll();

            // 5. cache 컨테이너 초기화
            _allCandidates = new List<WorldMapRuntimeHarvestInteractionCandidate>();
            _duplicateSourceMarkerIds = new List<string>();
            _byZoneId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>>();
            _byRuntimeKey = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>>();
            _byProfileId = new Dictionary<string, List<WorldMapRuntimeHarvestInteractionCandidate>>();
            _byInteractionKind = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, List<WorldMapRuntimeHarvestInteractionCandidate>>();
            _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeHarvestInteractionCandidate>();

            // 6. 각 candidate를 WorldMapRuntimeHarvestInteractionCandidate로 변환
            foreach (WorldMapRuntimeHarvestResourceCandidate sourceCandidate in sourceCandidates)
            {
                if (sourceCandidate == null)
                    continue;

                var interactionCandidate = new WorldMapRuntimeHarvestInteractionCandidate();
                interactionCandidate.Configure(sourceCandidate);

                // IsReady()==true인 interaction candidate만 cache에 포함
                if (!interactionCandidate.IsReady)
                    continue;

                _allCandidates.Add(interactionCandidate);

                // ZoneId 기준 추가
                string zoneId = interactionCandidate.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_byZoneId.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapRuntimeHarvestInteractionCandidate>();
                        _byZoneId[zoneId] = zoneList;
                    }
                    zoneList.Add(interactionCandidate);
                }

                // RuntimeKey 기준 추가
                string runtimeKey = interactionCandidate.RuntimeKey;
                if (!string.IsNullOrEmpty(runtimeKey))
                {
                    if (!_byRuntimeKey.TryGetValue(runtimeKey, out var keyList))
                    {
                        keyList = new List<WorldMapRuntimeHarvestInteractionCandidate>();
                        _byRuntimeKey[runtimeKey] = keyList;
                    }
                    keyList.Add(interactionCandidate);
                }

                // ProfileId 기준 추가
                string profileId = interactionCandidate.ProfileId;
                if (!string.IsNullOrEmpty(profileId))
                {
                    if (!_byProfileId.TryGetValue(profileId, out var profList))
                    {
                        profList = new List<WorldMapRuntimeHarvestInteractionCandidate>();
                        _byProfileId[profileId] = profList;
                    }
                    profList.Add(interactionCandidate);
                }

                // InteractionKind 기준 추가
                WorldMapRuntimeHarvestInteractionCandidateKind kind = interactionCandidate.InteractionKind;
                if (!_byInteractionKind.TryGetValue(kind, out var kindList))
                {
                    kindList = new List<WorldMapRuntimeHarvestInteractionCandidate>();
                    _byInteractionKind[kind] = kindList;
                }
                kindList.Add(interactionCandidate);

                // SourceMarkerId 기준 추가 (중복 시 첫 번째만 유지)
                string sourceMarkerId = interactionCandidate.SourceMarkerId;
                if (!string.IsNullOrEmpty(sourceMarkerId))
                {
                    if (_bySourceMarkerId.ContainsKey(sourceMarkerId))
                    {
                        // 중복 발생 -> duplicate 목록에 추가 (아직 없으면)
                        if (!_duplicateSourceMarkerIds.Contains(sourceMarkerId))
                        {
                            _duplicateSourceMarkerIds.Add(sourceMarkerId);
                            UnityEngine.Debug.LogWarning($"[WorldMapRuntimeHarvestInteractionCandidateRegistry] Duplicate SourceMarkerId found: '{sourceMarkerId}' (Zone: {zoneId}). First instance will be used for TryGetBySourceMarkerId.");
                        }
                    }
                    else
                    {
                        _bySourceMarkerId[sourceMarkerId] = interactionCandidate;
                    }
                }
            }

            // 7. 결과 로그
            UnityEngine.Debug.Log($"[WorldMapRuntimeHarvestInteractionCandidateRegistry] RebuildCacheFromRoot complete. " +
                $"Interaction candidates: {_allCandidates.Count}, " +
                $"Zones: {_byZoneId.Count}, " +
                $"RuntimeKeys: {_byRuntimeKey.Count}, " +
                $"ProfileIds: {_byProfileId.Count}, " +
                $"InteractionKinds: {_byInteractionKind.Count}, " +
                $"SourceMarkerIds: {_bySourceMarkerId.Count}, " +
                $"Duplicate SourceMarkerIds: {_duplicateSourceMarkerIds.Count}");
        }

        /// <summary>
        /// 모든 cache를 비운다. hasRuntimeInitialized도 false로 되돌린다.
        /// </summary>
        public void ClearCache()
        {
            _allCandidates.Clear();
            _duplicateSourceMarkerIds.Clear();
            _byZoneId.Clear();
            _byRuntimeKey.Clear();
            _byProfileId.Clear();
            _byInteractionKind.Clear();
            _bySourceMarkerId.Clear();

            hasRuntimeInitialized = false;

            UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionCandidateRegistry] Cache cleared.");
        }

        // ===== Public Static Helper: Scene Harvest Interaction Candidate Count =====

        /// <summary>
        /// RebuildCacheFromRoot와 동일한 로직으로 scene 내 HarvestResource candidate를
        /// interaction candidate로 변환했을 때의 개수를 계산한다.
        /// Validate 등에서 RebuildCache와 scene count의 필터 로직이 달라지지 않도록 공용으로 사용한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>필터를 통과한 interaction candidate 개수</returns>
        public static int CountSceneHarvestInteractionCandidates(Transform root)
        {
            if (root == null)
                return 0;

            // root 하위에서 WorldMapRuntimeHarvestResourceCandidateQueryService를 찾는다.
            WorldMapRuntimeHarvestResourceCandidateQueryService queryService = root.GetComponentInChildren<WorldMapRuntimeHarvestResourceCandidateQueryService>(true);

            if (queryService == null)
                return 0;

            // QueryService 초기화
            queryService.TryInitializeFromRoot(root);

            // GetAll()로 모든 candidate를 가져온다.
            IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> sourceCandidates = queryService.GetAll();
            int count = 0;

            foreach (WorldMapRuntimeHarvestResourceCandidate sourceCandidate in sourceCandidates)
            {
                if (sourceCandidate == null)
                    continue;

                var interactionCandidate = new WorldMapRuntimeHarvestInteractionCandidate();
                interactionCandidate.Configure(sourceCandidate);

                if (interactionCandidate.IsReady)
                    count++;
            }

            return count;
        }

        // ===== Debug / Validation Helpers =====

        /// <summary>
        /// 현재 cache 상태의 요약 문자열을 반환한다. (디버그/검증용)
        /// </summary>
        public string GetCacheSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WorldMapRuntimeHarvestInteractionCandidateRegistry Cache Summary ===");
            sb.AppendLine($"Total Interaction Candidates: {_allCandidates.Count}");
            sb.AppendLine($"Unique Zones: {_byZoneId.Count}");
            sb.AppendLine($"Unique RuntimeKeys: {_byRuntimeKey.Count}");
            sb.AppendLine($"Unique ProfileIds: {_byProfileId.Count}");
            sb.AppendLine($"Unique InteractionKinds: {_byInteractionKind.Count}");
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
