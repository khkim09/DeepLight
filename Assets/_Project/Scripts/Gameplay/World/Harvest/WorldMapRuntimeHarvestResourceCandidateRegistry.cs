using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Project.Gameplay.World.Content;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// RuntimeFinalContentInstances 하위 WorldMapRuntimeGameplayConsumerContract 중
    /// HarvestResourceConsumer만 수집해서 WorldMapRuntimeHarvestResourceCandidate 리스트를 캐시한다.
    /// 매 프레임 FindObjectsByType을 사용하지 않고, RebuildCacheFromRoot() 호출 시에만 scene scan한다.
    /// N-4 ConsumerContractRegistry 패턴과 동일한 구조로 작성.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    public class WorldMapRuntimeHarvestResourceCandidateRegistry : MonoBehaviour
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

        /// <summary>모든 harvest candidate의 flat list cache</summary>
        private List<WorldMapRuntimeHarvestResourceCandidate> _allCandidates = new List<WorldMapRuntimeHarvestResourceCandidate>();

        /// <summary>중복 SourceMarkerId 목록 (internal 관리용)</summary>
        private List<string> _duplicateSourceMarkerIds = new List<string>();

        /// <summary>ZoneId -> candidate 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>> _byZoneId = new Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>>();

        /// <summary>RuntimeKey -> candidate 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>> _byRuntimeKey = new Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>>();

        /// <summary>ProfileId -> candidate 목록 cache</summary>
        private Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>> _byProfileId = new Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>>();

        /// <summary>SourceMarkerId -> candidate (첫 번째만, 중복 시 첫 번째 우선)</summary>
        private Dictionary<string, WorldMapRuntimeHarvestResourceCandidate> _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeHarvestResourceCandidate>();

        // ===== Public Read-only Properties =====

        /// <summary>모든 harvest candidate의 읽기 전용 목록</summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> AllCandidates => _allCandidates;

        /// <summary>중복 SourceMarkerId 목록 (읽기 전용)</summary>
        public IReadOnlyList<string> DuplicateSourceMarkerIds => _duplicateSourceMarkerIds;

        /// <summary>cache된 harvest candidate 총 개수</summary>
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
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestResourceCandidateRegistry] Awake: rebuildOnAwake is true. Rebuilding cache from root.");

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestResourceCandidateRegistry] OnEnable: cache is empty. Rebuilding cache from root.");

                RebuildCacheFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Query API =====

        /// <summary>
        /// 지정한 ZoneId에 속한 harvest candidate 목록을 반환한다.
        /// 해당 ZoneId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> GetByZoneId(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();

            if (_byZoneId.TryGetValue(zoneId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();
        }

        /// <summary>
        /// 지정한 RuntimeKey에 속한 harvest candidate 목록을 반환한다.
        /// 해당 RuntimeKey가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> GetByRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();

            if (_byRuntimeKey.TryGetValue(runtimeKey, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 harvest candidate 목록을 반환한다.
        /// 해당 ProfileId가 없으면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> GetByProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();

            if (_byProfileId.TryGetValue(profileId, out var list))
                return list;

            return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();
        }

        /// <summary>
        /// SourceMarkerId로 harvest candidate를 조회한다.
        /// 중복 SourceMarkerId가 있는 경우 첫 번째 candidate가 반환된다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeHarvestResourceCandidate result)
        {
            if (string.IsNullOrEmpty(sourceMarkerId))
            {
                result = null;
                return false;
            }

            return _bySourceMarkerId.TryGetValue(sourceMarkerId, out result);
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 harvest candidate 개수를 반환한다.
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
        /// 지정한 RuntimeKey에 속한 harvest candidate 개수를 반환한다.
        /// </summary>
        public int CountByRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return 0;

            if (_byRuntimeKey.TryGetValue(runtimeKey, out var list))
                return list.Count;

            return 0;
        }

        // ===== Cache Management =====

        /// <summary>
        /// Scene 내 모든 harvest candidate를 스캔하여 cache를 재구축한다.
        /// 내부적으로 RebuildCacheFromRoot(transform)을 호출한다.
        /// </summary>
        public void RebuildCacheFromScene()
        {
            RebuildCacheFromRoot(transform);
        }

        /// <summary>
        /// 지정한 root Transform 하위에서 모든 consumer contract를 스캔하여
        /// HarvestResourceConsumer만 추출해 candidate cache를 재구축한다.
        /// root가 null이면 자기 transform으로 fallback한다.
        /// includeInactive 옵션에 따라 비활성화된 object도 포함 가능.
        /// </summary>
        /// <param name="root">검색 기준이 되는 root Transform. null이면 this.transform 사용.</param>
        public void RebuildCacheFromRoot(Transform root)
        {
            // root가 null이면 자기 transform으로 fallback
            if (root == null)
                root = transform;

            // 1. root 하위 모든 WorldMapRuntimeGameplayConsumerContract를 includeInactive 옵션에 따라 수집
            WorldMapRuntimeGameplayConsumerContract[] allContracts = root.GetComponentsInChildren<WorldMapRuntimeGameplayConsumerContract>(includeInactiveCandidates);

            // 2. cache 컨테이너 초기화
            _allCandidates = new List<WorldMapRuntimeHarvestResourceCandidate>();
            _duplicateSourceMarkerIds = new List<string>();
            _byZoneId = new Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>>();
            _byRuntimeKey = new Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>>();
            _byProfileId = new Dictionary<string, List<WorldMapRuntimeHarvestResourceCandidate>>();
            _bySourceMarkerId = new Dictionary<string, WorldMapRuntimeHarvestResourceCandidate>();

            // 3. 각 contract를 순회하며 HarvestResourceConsumer만 수집
            foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
            {
                // null / destroyed object 체크
                if (contract == null || contract.gameObject == null)
                    continue;

                // ===== 포함 조건 =====

                // (1) IsReady() == true인 것만 포함
                if (!contract.IsReady())
                    continue;

                // (2) ConsumerKind == HarvestResourceConsumer인 것만 포함
                if (contract.ConsumerKind != WorldMapRuntimeGameplayConsumerKind.HarvestResourceConsumer)
                    continue;

                // (3) RuntimeFinalContentInstances 하위가 아니면 제외
                if (!IsUnderRuntimeFinalContentInstances(contract.transform))
                    continue;

                // (4) WorldMapRuntimeFinalContentInstanceTag가 없으면 제외
                if (contract.GetComponent<WorldMapRuntimeFinalContentInstanceTag>() == null)
                    continue;

                // ===== 제외 조건 =====

                // (5) RuntimeSpawnedInstances 하위 source runtime instance 제외
                if (IsUnderRuntimeSpawnedInstances(contract.transform))
                    continue;

                // (6) RuntimeSpawnInstances 하위 preview instance 제외
                if (IsUnderRuntimeSpawnPreviewInstances(contract.transform))
                    continue;

                // (7) WorldMapRuntimePlaceholderBinding이 있는 object 제외
                if (contract.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // (8) WorldMapZoneContentMarker가 있는 object 제외
                if (contract.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // (9) WorldMapRuntimeSpawnInstanceTag가 있는 object 제외
                if (contract.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                    continue;

                // (10) WorldMapRuntimeSpawnGameplayAdapter가 있는 object 제외
                if (contract.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                    continue;

                // (11) WorldMapRuntimeFinalContentContract가 있는 object 제외
                if (contract.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    continue;

                // (12) WorldMapRuntimeFinalContentPlaceholderTag는 제외 조건으로 사용하지 않음.
                //     generated placeholder final content는 정상 Harvest candidate로 취급한다.

                // ===== 유효한 contract -> candidate 생성 =====
                var candidate = new WorldMapRuntimeHarvestResourceCandidate();
                candidate.Configure(contract);

                // Configure 후에도 isReady가 false면 제외 (필수 필드 누락)
                if (!candidate.IsReady)
                    continue;

                _allCandidates.Add(candidate);

                // ZoneId 기준 추가
                string zoneId = candidate.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_byZoneId.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapRuntimeHarvestResourceCandidate>();
                        _byZoneId[zoneId] = zoneList;
                    }
                    zoneList.Add(candidate);
                }

                // RuntimeKey 기준 추가
                string runtimeKey = candidate.RuntimeKey;
                if (!string.IsNullOrEmpty(runtimeKey))
                {
                    if (!_byRuntimeKey.TryGetValue(runtimeKey, out var keyList))
                    {
                        keyList = new List<WorldMapRuntimeHarvestResourceCandidate>();
                        _byRuntimeKey[runtimeKey] = keyList;
                    }
                    keyList.Add(candidate);
                }

                // ProfileId 기준 추가
                string profileId = candidate.ProfileId;
                if (!string.IsNullOrEmpty(profileId))
                {
                    if (!_byProfileId.TryGetValue(profileId, out var profList))
                    {
                        profList = new List<WorldMapRuntimeHarvestResourceCandidate>();
                        _byProfileId[profileId] = profList;
                    }
                    profList.Add(candidate);
                }

                // SourceMarkerId 기준 추가 (중복 시 첫 번째만 유지)
                string sourceMarkerId = candidate.SourceMarkerId;
                if (!string.IsNullOrEmpty(sourceMarkerId))
                {
                    if (_bySourceMarkerId.ContainsKey(sourceMarkerId))
                    {
                        // 중복 발생 -> duplicate 목록에 추가 (아직 없으면)
                        if (!_duplicateSourceMarkerIds.Contains(sourceMarkerId))
                        {
                            _duplicateSourceMarkerIds.Add(sourceMarkerId);
                            UnityEngine.Debug.LogWarning($"[WorldMapRuntimeHarvestResourceCandidateRegistry] Duplicate SourceMarkerId found: '{sourceMarkerId}' (Zone: {zoneId}). First instance will be used for TryGetBySourceMarkerId.");
                        }
                    }
                    else
                    {
                        _bySourceMarkerId[sourceMarkerId] = candidate;
                    }
                }
            }

            // 4. 결과 로그
            UnityEngine.Debug.Log($"[WorldMapRuntimeHarvestResourceCandidateRegistry] RebuildCacheFromRoot complete. " +
                $"Harvest candidates: {_allCandidates.Count}, " +
                $"Zones: {_byZoneId.Count}, " +
                $"RuntimeKeys: {_byRuntimeKey.Count}, " +
                $"ProfileIds: {_byProfileId.Count}, " +
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
            _bySourceMarkerId.Clear();

            hasRuntimeInitialized = false;

            UnityEngine.Debug.Log("[WorldMapRuntimeHarvestResourceCandidateRegistry] Cache cleared.");
        }

        // ===== Public Static Helper: Scene Harvest Candidate Count =====

        /// <summary>
        /// RebuildCacheFromRoot와 동일한 필터 로직으로 scene 내 HarvestResourceConsumer candidate 개수를 계산한다.
        /// Validate 등에서 RebuildCache와 scene count의 필터 로직이 달라지지 않도록 공용으로 사용한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>필터를 통과한 harvest candidate 개수</returns>
        public static int CountSceneHarvestCandidates(Transform root)
        {
            if (root == null)
                return 0;

            WorldMapRuntimeGameplayConsumerContract[] allContracts = root.GetComponentsInChildren<WorldMapRuntimeGameplayConsumerContract>(true);
            int count = 0;

            foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
            {
                if (contract == null || contract.gameObject == null)
                    continue;

                // IsReady() == true인 것만 포함
                if (!contract.IsReady())
                    continue;

                // ConsumerKind == HarvestResourceConsumer인 것만 포함
                if (contract.ConsumerKind != WorldMapRuntimeGameplayConsumerKind.HarvestResourceConsumer)
                    continue;

                // RuntimeFinalContentInstances 하위인지 확인
                if (!IsUnderRuntimeFinalContentInstances(contract.transform))
                    continue;

                // WorldMapRuntimeFinalContentInstanceTag가 없으면 제외
                if (contract.GetComponent<WorldMapRuntimeFinalContentInstanceTag>() == null)
                    continue;

                // RuntimeSpawnedInstances 하위 제외
                if (IsUnderRuntimeSpawnedInstances(contract.transform))
                    continue;

                // RuntimeSpawnInstances 하위 preview 제외
                if (IsUnderRuntimeSpawnPreviewInstances(contract.transform))
                    continue;

                // WorldMapRuntimePlaceholderBinding 제외
                if (contract.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // WorldMapZoneContentMarker 제외
                if (contract.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // WorldMapRuntimeSpawnInstanceTag 제외
                if (contract.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                    continue;

                // WorldMapRuntimeSpawnGameplayAdapter 제외
                if (contract.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                    continue;

                // WorldMapRuntimeFinalContentContract 제외
                if (contract.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
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
        /// </summary>
        private static bool IsUnderRuntimeFinalContentInstances(Transform target)
        {
            return IsUnderNamedAncestor(target, "RuntimeFinalContentInstances");
        }

        /// <summary>
        /// 지정한 Transform의 ancestor 중 "RuntimeSpawnedInstances" 이름을 가진 object가 있는지 확인한다.
        /// </summary>
        private static bool IsUnderRuntimeSpawnedInstances(Transform target)
        {
            return IsUnderNamedAncestor(target, "RuntimeSpawnedInstances");
        }

        /// <summary>
        /// 지정한 Transform의 ancestor 중 "RuntimeSpawnInstances" 이름을 가진 object가 있는지 확인한다.
        /// </summary>
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
            sb.AppendLine("=== WorldMapRuntimeHarvestResourceCandidateRegistry Cache Summary ===");
            sb.AppendLine($"Total Harvest Candidates: {_allCandidates.Count}");
            sb.AppendLine($"Unique Zones: {_byZoneId.Count}");
            sb.AppendLine($"Unique RuntimeKeys: {_byRuntimeKey.Count}");
            sb.AppendLine($"Unique ProfileIds: {_byProfileId.Count}");
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
