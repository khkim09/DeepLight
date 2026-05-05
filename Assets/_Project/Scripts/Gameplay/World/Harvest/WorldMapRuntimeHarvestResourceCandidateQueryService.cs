using System.Collections.Generic;
using UnityEngine;
using Project.Gameplay.World.Content;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// Harvest 시스템이 WorldMapRuntimeHarvestResourceCandidateRegistry를 직접 만지지 않도록
    /// 얇은 facade 역할을 하는 QueryService.
    /// Registry가 없으면 root에서 찾아보고 없으면 root.gameObject에 추가한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    public class WorldMapRuntimeHarvestResourceCandidateQueryService : MonoBehaviour
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("Awake에서 자동으로 TryInitializeFromRoot(transform)를 호출할지 여부")]
        private bool initializeOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 registry가 비어 있으면 자동으로 TryInitializeFromRoot(transform)를 호출할지 여부")]
        private bool initializeOnEnableIfEmpty = true;

        [SerializeField, Tooltip("Registry count가 0일 때 RebuildCacheFromRoot를 호출할지 여부")]
        private bool rebuildRegistryIfEmpty = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization = false;

        [SerializeField, Tooltip("참조할 Registry (Inspector에서 수동 할당 가능)")]
        private WorldMapRuntimeHarvestResourceCandidateRegistry _registry;

        // ===== Internal State =====

        /// <summary>초기화 완료 여부</summary>
        private bool _isInitialized;

        // ===== Public Properties =====

        /// <summary>Registry가 할당되어 있는지 여부</summary>
        public bool HasRegistry => _registry != null;

        /// <summary>QueryService가 관리하는 harvest candidate 총 개수</summary>
        public int Count => _registry != null ? _registry.Count : 0;

        // ===== Unity Lifecycle =====

        /// <summary>
        /// Awake: initializeOnAwake가 true이면 TryInitializeFromRoot(transform)를 호출한다.
        /// </summary>
        private void Awake()
        {
            if (initializeOnAwake && !_isInitialized)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestResourceCandidateQueryService] Awake: initializeOnAwake is true. Initializing from root.");

                TryInitializeFromRoot(transform);
                _isInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: initializeOnEnableIfEmpty가 true이고 registry가 없거나 count가 0이면
        /// TryInitializeFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (initializeOnEnableIfEmpty && (_registry == null || _registry.Count == 0))
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestResourceCandidateQueryService] OnEnable: registry is null or empty. Initializing from root.");

                TryInitializeFromRoot(transform);
                _isInitialized = true;
            }
        }

        // ===== Public API =====

        /// <summary>
        /// 지정한 root Transform 하위에서 Registry를 찾거나 생성하고,
        /// rebuildRegistryIfEmpty가 true이면 cache를 재구축한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform</param>
        /// <returns>초기화 성공 여부</returns>
        public bool TryInitializeFromRoot(Transform root)
        {
            if (root == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeHarvestResourceCandidateQueryService] TryInitializeFromRoot: root is null.");
                return false;
            }

            // Registry가 없으면 root에서 찾아보고 없으면 root.gameObject에 추가
            if (_registry == null)
            {
                _registry = root.GetComponentInChildren<WorldMapRuntimeHarvestResourceCandidateRegistry>(true);

                if (_registry == null)
                {
                    // root.gameObject에 Registry 추가
                    _registry = root.gameObject.AddComponent<WorldMapRuntimeHarvestResourceCandidateRegistry>();
                    if (logRuntimeInitialization)
                        UnityEngine.Debug.Log("[WorldMapRuntimeHarvestResourceCandidateQueryService] Added WorldMapRuntimeHarvestResourceCandidateRegistry to root.");
                }
            }

            // Registry count가 0이고 rebuildRegistryIfEmpty가 true면 RebuildCacheFromRoot 호출
            if (_registry.Count == 0 && rebuildRegistryIfEmpty)
            {
                _registry.RebuildCacheFromRoot(root);
            }

            _isInitialized = true;
            return _registry.Count > 0;
        }

        /// <summary>
        /// Registry의 cache를 강제로 재구축한다.
        /// </summary>
        public void Rebuild()
        {
            if (_registry != null)
            {
                _registry.RebuildCacheFromRoot(_registry.transform);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeHarvestResourceCandidateQueryService] Rebuild: registry is null. Call TryInitializeFromRoot first.");
            }
        }

        /// <summary>
        /// 모든 harvest candidate를 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> GetAll()
        {
            if (_registry == null)
                return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();

            return _registry.AllCandidates;
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 harvest candidate 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> GetByZoneId(string zoneId)
        {
            if (_registry == null || string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();

            return _registry.GetByZoneId(zoneId);
        }

        /// <summary>
        /// 지정한 RuntimeKey에 속한 harvest candidate 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> GetByRuntimeKey(string runtimeKey)
        {
            if (_registry == null || string.IsNullOrEmpty(runtimeKey))
                return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();

            return _registry.GetByRuntimeKey(runtimeKey);
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 harvest candidate 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> GetByProfileId(string profileId)
        {
            if (_registry == null || string.IsNullOrEmpty(profileId))
                return System.Array.Empty<WorldMapRuntimeHarvestResourceCandidate>();

            return _registry.GetByProfileId(profileId);
        }

        /// <summary>
        /// SourceMarkerId로 harvest candidate를 조회한다.
        /// </summary>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeHarvestResourceCandidate result)
        {
            if (_registry == null || string.IsNullOrEmpty(sourceMarkerId))
            {
                result = null;
                return false;
            }

            return _registry.TryGetBySourceMarkerId(sourceMarkerId, out result);
        }

        /// <summary>
        /// 지정한 position에서 가장 가까운 harvest candidate를 찾는다.
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="result">가장 가까운 candidate (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 position, out WorldMapRuntimeHarvestResourceCandidate result)
        {
            result = null;

            if (_registry == null || _registry.Count == 0)
                return false;

            float minDistSq = float.MaxValue;
            WorldMapRuntimeHarvestResourceCandidate nearest = null;

            foreach (var candidate in _registry.AllCandidates)
            {
                float distSq = (candidate.WorldPosition - position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = candidate;
                }
            }

            result = nearest;
            return result != null;
        }

        /// <summary>
        /// 지정한 position에서 가장 가까운, 지정한 runtimeKey를 가진 harvest candidate를 찾는다.
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="runtimeKey">필터링할 RuntimeKey</param>
        /// <param name="result">가장 가까운 candidate (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 position, string runtimeKey, out WorldMapRuntimeHarvestResourceCandidate result)
        {
            result = null;

            if (_registry == null || _registry.Count == 0 || string.IsNullOrEmpty(runtimeKey))
                return false;

            float minDistSq = float.MaxValue;
            WorldMapRuntimeHarvestResourceCandidate nearest = null;

            foreach (var candidate in _registry.AllCandidates)
            {
                if (!candidate.IsRuntimeKey(runtimeKey))
                    continue;

                float distSq = (candidate.WorldPosition - position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = candidate;
                }
            }

            result = nearest;
            return result != null;
        }

        /// <summary>
        /// 지정한 ZoneId 내에서 position에 가장 가까운 harvest candidate를 찾는다.
        /// </summary>
        /// <param name="zoneId">필터링할 ZoneId</param>
        /// <param name="position">기준 위치</param>
        /// <param name="result">가장 가까운 candidate (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearestInZone(string zoneId, Vector3 position, out WorldMapRuntimeHarvestResourceCandidate result)
        {
            result = null;

            if (_registry == null || _registry.Count == 0 || string.IsNullOrEmpty(zoneId))
                return false;

            float minDistSq = float.MaxValue;
            WorldMapRuntimeHarvestResourceCandidate nearest = null;

            foreach (var candidate in _registry.AllCandidates)
            {
                if (!candidate.IsZone(zoneId))
                    continue;

                float distSq = (candidate.WorldPosition - position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = candidate;
                }
            }

            result = nearest;
            return result != null;
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 harvest candidate 개수를 반환한다.
        /// </summary>
        public int CountByZone(string zoneId)
        {
            if (_registry == null || string.IsNullOrEmpty(zoneId))
                return 0;

            return _registry.CountByZone(zoneId);
        }

        /// <summary>
        /// 지정한 RuntimeKey에 속한 harvest candidate 개수를 반환한다.
        /// </summary>
        public int CountByRuntimeKey(string runtimeKey)
        {
            if (_registry == null || string.IsNullOrEmpty(runtimeKey))
                return 0;

            return _registry.CountByRuntimeKey(runtimeKey);
        }
    }
}
