using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Runtime Spawn Instance Registry에 대한 얇은 Query Facade.
    /// gameplay 시스템이 registry를 직접 강하게 참조하지 않도록 중간 계층을 제공한다.
    /// 매 프레임 FindObjectsByType 사용 금지. Update/LateUpdate 사용 금지.
    /// Rebuild() 또는 TryInitializeFromRoot() 호출 시에만 cache를 갱신한다.
    /// 실제 cache는 registry가 담당하고, QueryService는 안정적인 facade 역할만 한다.
    /// </summary>
    public class WorldMapRuntimeSpawnQueryService : MonoBehaviour
    {
        // ===== Runtime Initialization Options =====

        [SerializeField, Tooltip("Awake에서 자동으로 TryInitializeFromRoot(transform)를 호출할지 여부")]
        private bool initializeOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 QueryService.Count가 0이면 자동으로 TryInitializeFromRoot(transform)를 호출할지 여부")]
        private bool initializeOnEnableIfEmpty = true;

        [SerializeField, Tooltip("Registry cache가 비어 있을 때 RebuildCacheFromRoot를 호출할지 여부")]
        private bool rebuildRegistryIfEmpty = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization = false;

        [SerializeField, Tooltip("실제 cache를 보유한 Runtime Spawn Instance Registry")]
        private WorldMapRuntimeSpawnInstanceRegistry registry;

        // ===== Internal State =====

        /// <summary>중복 초기화 방지를 위한 내부 상태</summary>
        private bool hasRuntimeInitialized;

        // ===== Public Properties =====

        /// <summary>Registry가 할당되어 있는지 여부</summary>
        public bool HasRegistry => registry != null;

        /// <summary>Registry에 cache된 runtime instance 총 개수</summary>
        public int Count => registry != null ? registry.AllInstances.Count : 0;

        // ===== Unity Lifecycle =====

        /// <summary>
        /// Awake: initializeOnAwake가 true이면 TryInitializeFromRoot(transform)를 호출하여
        /// PlayMode 진입 시 QueryService가 초기화되어 있도록 보장한다.
        /// </summary>
        private void Awake()
        {
            if (initializeOnAwake && !hasRuntimeInitialized)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeSpawnQueryService] Awake: initializeOnAwake is true. Initializing from root.");

                TryInitializeFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: initializeOnEnableIfEmpty가 true이고 Count가 0이면
        /// TryInitializeFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (initializeOnEnableIfEmpty && Count == 0 && !hasRuntimeInitialized)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeSpawnQueryService] OnEnable: Count is 0. Initializing from root.");

                TryInitializeFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Initialization =====

        /// <summary>
        /// GeneratedWorldRoot Transform으로부터 Registry를 찾고 초기화한다.
        /// GeneratedWorldRoot에 WorldMapRuntimeSpawnInstanceRegistry가 없으면 false를 반환한다.
        /// </summary>
        /// <param name="generatedRoot">검색 기준이 되는 root Transform</param>
        /// <returns>Registry를 찾아 초기화했으면 true, 실패하면 false</returns>
        public bool TryInitializeFromRoot(Transform generatedRoot)
        {
            // root가 null이면 실패
            if (generatedRoot == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeSpawnQueryService] TryInitializeFromRoot: generatedRoot is null.");
                return false;
            }

            // GeneratedWorldRoot에서 Registry component 검색
            WorldMapRuntimeSpawnInstanceRegistry foundRegistry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();
            if (foundRegistry == null)
            {
                UnityEngine.Debug.LogWarning($"[WorldMapRuntimeSpawnQueryService] WorldMapRuntimeSpawnInstanceRegistry not found on '{generatedRoot.name}'.");
                registry = null;
                return false;
            }

            // Registry 할당
            registry = foundRegistry;

            // Registry cache가 비어 있고 rebuildRegistryIfEmpty가 true면 RebuildCacheFromRoot 호출
            if (registry.AllInstances.Count == 0 && rebuildRegistryIfEmpty)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeSpawnQueryService] Registry cache is empty. Calling RebuildCacheFromRoot.");

                registry.RebuildCacheFromRoot(generatedRoot);
            }

            UnityEngine.Debug.Log($"[WorldMapRuntimeSpawnQueryService] Initialized from '{generatedRoot.name}'. Registry instances: {registry.AllInstances.Count}");
            return true;
        }

        /// <summary>
        /// Registry의 cache를 강제로 재구축한다.
        /// registry가 null이면 아무 작업도 수행하지 않는다.
        /// </summary>
        public void Rebuild()
        {
            if (registry == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeSpawnQueryService] Rebuild skipped: registry is null.");
                return;
            }

            // registry의 RebuildCacheFromScene을 호출하여 scene scan + cache 갱신
            registry.RebuildCacheFromScene();

            UnityEngine.Debug.Log($"[WorldMapRuntimeSpawnQueryService] Rebuild complete. Registry instances: {registry.AllInstances.Count}");
        }

        // ===== Query API =====

        /// <summary>
        /// 모든 runtime instance의 읽기 전용 목록을 반환한다.
        /// registry가 null이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> GetAll()
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();

            return registry.AllInstances;
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 runtime instance 목록을 반환한다.
        /// registry가 null이거나 zoneId가 null/empty이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> GetByZoneId(string zoneId)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();

            return registry.GetByZoneId(zoneId);
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 runtime instance 목록을 반환한다.
        /// registry가 null이거나 runtimeCategory가 null/empty이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> GetByRuntimeCategory(string runtimeCategory)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();

            return registry.GetByRuntimeCategory(runtimeCategory);
        }

        /// <summary>
        /// 지정한 MarkerType에 속한 runtime instance 목록을 반환한다.
        /// registry가 null이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> GetByMarkerType(WorldMapZoneContentMarkerType markerType)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeSpawnInstanceTag>();

            return registry.GetByMarkerType(markerType);
        }

        /// <summary>
        /// MarkerId로 runtime instance를 조회한다.
        /// registry가 null이거나 markerId가 null/empty이면 false를 반환한다.
        /// </summary>
        /// <param name="markerId">검색할 MarkerId</param>
        /// <param name="result">찾은 instance, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetByMarkerId(string markerId, out WorldMapRuntimeSpawnInstanceTag result)
        {
            if (registry == null)
            {
                result = null;
                return false;
            }

            return registry.TryGetByMarkerId(markerId, out result);
        }

        /// <summary>
        /// 지정한 worldPosition에서 가장 가까운 runtime instance를 찾는다.
        /// registry가 null이거나 runtimeCategory가 null/empty이면 false를 반환한다.
        /// </summary>
        /// <param name="worldPosition">검색 기준 월드 좌표</param>
        /// <param name="runtimeCategory">필터링할 RuntimeCategory (null/empty면 모든 category 대상)</param>
        /// <param name="result">가장 가까운 instance, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 worldPosition, string runtimeCategory, out WorldMapRuntimeSpawnInstanceTag result)
        {
            result = null;

            if (registry == null)
                return false;

            // 검색 대상 목록 결정
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> candidates;
            if (!string.IsNullOrEmpty(runtimeCategory))
            {
                candidates = registry.GetByRuntimeCategory(runtimeCategory);
            }
            else
            {
                candidates = registry.AllInstances;
            }

            // candidates가 비어 있으면 실패
            if (candidates == null || candidates.Count == 0)
                return false;

            // 가장 가까운 instance 탐색 (거리 제곱 비교)
            float nearestSqrDist = float.MaxValue;
            WorldMapRuntimeSpawnInstanceTag nearest = null;

            foreach (WorldMapRuntimeSpawnInstanceTag candidate in candidates)
            {
                if (candidate == null || candidate.gameObject == null)
                    continue;

                float sqrDist = (candidate.transform.position - worldPosition).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }

            if (nearest != null)
            {
                result = nearest;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 지정한 Zone 내에서 worldPosition에 가장 가까운 runtime instance를 찾는다.
        /// registry가 null이거나 zoneId가 null/empty이면 false를 반환한다.
        /// </summary>
        /// <param name="zoneId">검색 범위가 되는 ZoneId</param>
        /// <param name="worldPosition">검색 기준 월드 좌표</param>
        /// <param name="runtimeCategory">필터링할 RuntimeCategory (null/empty면 모든 category 대상)</param>
        /// <param name="result">가장 가까운 instance, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearestInZone(string zoneId, Vector3 worldPosition, string runtimeCategory, out WorldMapRuntimeSpawnInstanceTag result)
        {
            result = null;

            if (registry == null)
                return false;

            if (string.IsNullOrEmpty(zoneId))
                return false;

            // ZoneId로 1차 필터링
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> zoneCandidates = registry.GetByZoneId(zoneId);
            if (zoneCandidates == null || zoneCandidates.Count == 0)
                return false;

            // RuntimeCategory로 2차 필터링 (지정된 경우)
            List<WorldMapRuntimeSpawnInstanceTag> candidates = null;
            if (!string.IsNullOrEmpty(runtimeCategory))
            {
                candidates = new List<WorldMapRuntimeSpawnInstanceTag>(zoneCandidates.Count);
                foreach (WorldMapRuntimeSpawnInstanceTag candidate in zoneCandidates)
                {
                    if (candidate != null && candidate.RuntimeCategory == runtimeCategory)
                    {
                        candidates.Add(candidate);
                    }
                }

                if (candidates.Count == 0)
                    return false;
            }
            else
            {
                // zoneCandidates를 List로 변환 (IReadOnlyList를 직접 순회)
                candidates = new List<WorldMapRuntimeSpawnInstanceTag>(zoneCandidates);
            }

            // 가장 가까운 instance 탐색 (거리 제곱 비교)
            float nearestSqrDist = float.MaxValue;
            WorldMapRuntimeSpawnInstanceTag nearest = null;

            foreach (WorldMapRuntimeSpawnInstanceTag candidate in candidates)
            {
                if (candidate == null || candidate.gameObject == null)
                    continue;

                float sqrDist = (candidate.transform.position - worldPosition).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }

            if (nearest != null)
            {
                result = nearest;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 runtime instance 개수를 반환한다.
        /// registry가 null이거나 zoneId가 null/empty이면 0을 반환한다.
        /// </summary>
        public int CountByZone(string zoneId)
        {
            if (registry == null)
                return 0;

            return registry.CountByZone(zoneId);
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 runtime instance 개수를 반환한다.
        /// registry가 null이거나 runtimeCategory가 null/empty이면 0을 반환한다.
        /// </summary>
        public int CountByCategory(string runtimeCategory)
        {
            if (registry == null)
                return 0;

            return registry.CountByCategory(runtimeCategory);
        }
    }
}
