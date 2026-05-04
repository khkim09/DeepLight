using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// WorldMapRuntimeFinalContentGameplayBindingRegistry에 대한 얇은 Query Facade.
    /// gameplay 시스템이 registry를 직접 강하게 참조하지 않도록 중간 계층을 제공한다.
    /// 매 프레임 FindObjectsByType 사용 금지. Update/LateUpdate 사용 금지.
    /// Rebuild() 또는 TryInitializeFromRoot() 호출 시에만 cache를 갱신한다.
    /// 실제 cache는 registry가 담당하고, QueryService는 안정적인 facade 역할만 한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    public class WorldMapRuntimeFinalContentGameplayBindingQueryService : MonoBehaviour
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

        [SerializeField, Tooltip("실제 cache를 보유한 Gameplay Binding Registry")]
        private WorldMapRuntimeFinalContentGameplayBindingRegistry registry;

        // ===== Internal State =====

        /// <summary>중복 초기화 방지를 위한 내부 상태</summary>
        private bool hasRuntimeInitialized;

        // ===== Public Properties =====

        /// <summary>Registry가 할당되어 있는지 여부</summary>
        public bool HasRegistry => registry != null;

        /// <summary>Registry에 cache된 gameplay binding 총 개수</summary>
        public int Count => registry != null ? registry.Count : 0;

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentGameplayBindingQueryService] Awake: initializeOnAwake is true. Initializing from root.");

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
                    UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentGameplayBindingQueryService] OnEnable: Count is 0. Initializing from root.");

                TryInitializeFromRoot(transform);
                hasRuntimeInitialized = true;
            }
        }

        // ===== Initialization =====

        /// <summary>
        /// GeneratedWorldRoot Transform으로부터 Registry를 찾고 초기화한다.
        /// GeneratedWorldRoot에 WorldMapRuntimeFinalContentGameplayBindingRegistry가 없으면
        /// 같은 root GameObject에 추가하고 RebuildCacheFromRoot를 호출한다.
        /// </summary>
        /// <param name="generatedRoot">검색 기준이 되는 root Transform</param>
        /// <returns>Registry를 찾아 초기화했으면 true, 실패하면 false</returns>
        public bool TryInitializeFromRoot(Transform generatedRoot)
        {
            // root가 null이면 실패
            if (generatedRoot == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeFinalContentGameplayBindingQueryService] TryInitializeFromRoot: generatedRoot is null.");
                return false;
            }

            // GeneratedWorldRoot에서 Registry component 검색
            WorldMapRuntimeFinalContentGameplayBindingRegistry foundRegistry = generatedRoot.GetComponent<WorldMapRuntimeFinalContentGameplayBindingRegistry>();
            if (foundRegistry == null)
            {
                // Registry가 없으면 같은 root GameObject에 추가
                foundRegistry = generatedRoot.gameObject.AddComponent<WorldMapRuntimeFinalContentGameplayBindingRegistry>();
                UnityEngine.Debug.Log($"[WorldMapRuntimeFinalContentGameplayBindingQueryService] Added WorldMapRuntimeFinalContentGameplayBindingRegistry to '{generatedRoot.name}'.");
            }

            // Registry 할당
            registry = foundRegistry;

            // Registry cache가 비어 있고 rebuildRegistryIfEmpty가 true면 RebuildCacheFromRoot 호출
            if (registry.Count == 0 && rebuildRegistryIfEmpty)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeFinalContentGameplayBindingQueryService] Registry cache is empty. Calling RebuildCacheFromRoot.");

                registry.RebuildCacheFromRoot(generatedRoot);
            }

            UnityEngine.Debug.Log($"[WorldMapRuntimeFinalContentGameplayBindingQueryService] Initialized from '{generatedRoot.name}'. Registry gameplay bindings: {registry.Count}");
            return true;
        }

        /// <summary>
        /// Registry의 cache를 강제로 재구축한다.
        /// registry가 null이면 아무 작업도 수행하지 않는다.
        /// </summary>
        public bool Rebuild()
        {
            if (registry == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeFinalContentGameplayBindingQueryService] Rebuild skipped: registry is null.");
                return false;
            }

            // registry의 RebuildCacheFromScene을 호출하여 scene scan + cache 갱신
            registry.RebuildCacheFromScene();

            UnityEngine.Debug.Log($"[WorldMapRuntimeFinalContentGameplayBindingQueryService] Rebuild complete. Registry gameplay bindings: {registry.Count}");
            return true;
        }

        // ===== Query API =====

        /// <summary>
        /// 모든 gameplay binding의 읽기 전용 목록을 반환한다.
        /// registry가 null이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetAll()
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.AllBindings;
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 gameplay binding 목록을 반환한다.
        /// registry가 null이거나 zoneId가 null/empty이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByZoneId(string zoneId)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.GetByZoneId(zoneId);
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 gameplay binding 목록을 반환한다.
        /// registry가 null이거나 runtimeCategory가 null/empty이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByRuntimeCategory(string runtimeCategory)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.GetByRuntimeCategory(runtimeCategory);
        }

        /// <summary>
        /// 지정한 FinalContentKind에 속한 gameplay binding 목록을 반환한다.
        /// registry가 null이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByFinalContentKind(WorldMapRuntimeFinalContentKind kind)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.GetByFinalContentKind(kind);
        }

        /// <summary>
        /// 지정한 BindingKind에 속한 gameplay binding 목록을 반환한다.
        /// registry가 null이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByBindingKind(WorldMapRuntimeFinalContentGameplayBindingKind kind)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.GetByBindingKind(kind);
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 gameplay binding 목록을 반환한다.
        /// registry가 null이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByAdapterKind(WorldMapRuntimeGameplayAdapterKind kind)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.GetByAdapterKind(kind);
        }

        /// <summary>
        /// 지정한 RequirementId에 속한 gameplay binding 목록을 반환한다.
        /// registry가 null이거나 requirementId가 null/empty이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByRequirementId(string requirementId)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.GetByRequirementId(requirementId);
        }

        /// <summary>
        /// 지정한 ProfileId에 속한 gameplay binding 목록을 반환한다.
        /// registry가 null이거나 profileId가 null/empty이면 빈 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> GetByProfileId(string profileId)
        {
            if (registry == null)
                return System.Array.Empty<WorldMapRuntimeFinalContentGameplayBinding>();

            return registry.GetByProfileId(profileId);
        }

        /// <summary>
        /// SourceMarkerId로 gameplay binding을 조회한다.
        /// registry가 null이거나 sourceMarkerId가 null/empty이면 false를 반환한다.
        /// </summary>
        /// <param name="sourceMarkerId">검색할 SourceMarkerId</param>
        /// <param name="result">찾은 gameplay binding, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeFinalContentGameplayBinding result)
        {
            if (registry == null)
            {
                result = null;
                return false;
            }

            return registry.TryGetBySourceMarkerId(sourceMarkerId, out result);
        }

        /// <summary>
        /// 지정한 worldPosition에서 가장 가까운 gameplay binding을 찾는다.
        /// registry가 null이면 false를 반환한다.
        /// bindingKind == None이면 전체 대상에서 nearest를 찾는다.
        /// </summary>
        /// <param name="worldPosition">검색 기준 월드 좌표</param>
        /// <param name="bindingKind">필터링할 BindingKind (None이면 전체 대상)</param>
        /// <param name="result">가장 가까운 gameplay binding, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 worldPosition, WorldMapRuntimeFinalContentGameplayBindingKind bindingKind, out WorldMapRuntimeFinalContentGameplayBinding result)
        {
            result = null;

            if (registry == null)
                return false;

            // 검색 대상 목록 결정
            // bindingKind == None이면 전체 대상에서 nearest를 찾는다.
            IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> candidates;
            if (bindingKind != WorldMapRuntimeFinalContentGameplayBindingKind.None)
            {
                candidates = registry.GetByBindingKind(bindingKind);
            }
            else
            {
                candidates = registry.AllBindings;
            }

            // candidates가 비어 있으면 실패
            if (candidates == null || candidates.Count == 0)
                return false;

            // 가장 가까운 binding 탐색 (거리 제곱 비교)
            float nearestSqrDist = float.MaxValue;
            WorldMapRuntimeFinalContentGameplayBinding nearest = null;

            foreach (WorldMapRuntimeFinalContentGameplayBinding candidate in candidates)
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
        /// 지정한 worldPosition에서 가장 가까운 gameplay binding을 FinalContentKind 기준으로 찾는다.
        /// registry가 null이면 false를 반환한다.
        /// finalContentKind == None이면 전체 대상에서 nearest를 찾는다.
        /// </summary>
        /// <param name="worldPosition">검색 기준 월드 좌표</param>
        /// <param name="finalContentKind">필터링할 FinalContentKind (None이면 전체 대상)</param>
        /// <param name="result">가장 가까운 gameplay binding, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 worldPosition, WorldMapRuntimeFinalContentKind finalContentKind, out WorldMapRuntimeFinalContentGameplayBinding result)
        {
            result = null;

            if (registry == null)
                return false;

            // 검색 대상 목록 결정
            // FinalContentKind == None이면 전체 대상에서 nearest를 찾는다.
            IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> candidates;
            if (finalContentKind != WorldMapRuntimeFinalContentKind.None)
            {
                candidates = registry.GetByFinalContentKind(finalContentKind);
            }
            else
            {
                candidates = registry.AllBindings;
            }

            // candidates가 비어 있으면 실패
            if (candidates == null || candidates.Count == 0)
                return false;

            // 가장 가까운 binding 탐색 (거리 제곱 비교)
            float nearestSqrDist = float.MaxValue;
            WorldMapRuntimeFinalContentGameplayBinding nearest = null;

            foreach (WorldMapRuntimeFinalContentGameplayBinding candidate in candidates)
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
        /// 지정한 worldPosition에서 가장 가까운 gameplay binding을 RuntimeCategory 기준으로 찾는다.
        /// registry가 null이면 false를 반환한다.
        /// runtimeCategory가 null/empty이면 전체 대상에서 nearest를 찾는다.
        /// </summary>
        /// <param name="worldPosition">검색 기준 월드 좌표</param>
        /// <param name="runtimeCategory">필터링할 RuntimeCategory (null/empty면 전체 대상)</param>
        /// <param name="result">가장 가까운 gameplay binding, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 worldPosition, string runtimeCategory, out WorldMapRuntimeFinalContentGameplayBinding result)
        {
            result = null;

            if (registry == null)
                return false;

            // 검색 대상 목록 결정
            IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> candidates;
            if (!string.IsNullOrEmpty(runtimeCategory))
            {
                candidates = registry.GetByRuntimeCategory(runtimeCategory);
            }
            else
            {
                candidates = registry.AllBindings;
            }

            // candidates가 비어 있으면 실패
            if (candidates == null || candidates.Count == 0)
                return false;

            // 가장 가까운 binding 탐색 (거리 제곱 비교)
            float nearestSqrDist = float.MaxValue;
            WorldMapRuntimeFinalContentGameplayBinding nearest = null;

            foreach (WorldMapRuntimeFinalContentGameplayBinding candidate in candidates)
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
        /// 지정한 Zone 내에서 worldPosition에 가장 가까운 gameplay binding을 찾는다.
        /// registry가 null이거나 zoneId가 null/empty이면 false를 반환한다.
        /// bindingKind == None이면 해당 Zone 전체 대상에서 nearest를 찾는다.
        /// </summary>
        /// <param name="zoneId">검색 범위가 되는 ZoneId</param>
        /// <param name="worldPosition">검색 기준 월드 좌표</param>
        /// <param name="bindingKind">필터링할 BindingKind (None이면 Zone 전체 대상)</param>
        /// <param name="result">가장 가까운 gameplay binding, 없으면 null</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearestInZone(string zoneId, Vector3 worldPosition, WorldMapRuntimeFinalContentGameplayBindingKind bindingKind, out WorldMapRuntimeFinalContentGameplayBinding result)
        {
            result = null;

            if (registry == null)
                return false;

            if (string.IsNullOrEmpty(zoneId))
                return false;

            // ZoneId로 1차 필터링
            IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> zoneCandidates = registry.GetByZoneId(zoneId);
            if (zoneCandidates == null || zoneCandidates.Count == 0)
                return false;

            // BindingKind로 2차 필터링 (None이 아닌 경우)
            List<WorldMapRuntimeFinalContentGameplayBinding> candidates = null;
            if (bindingKind != WorldMapRuntimeFinalContentGameplayBindingKind.None)
            {
                candidates = new List<WorldMapRuntimeFinalContentGameplayBinding>(zoneCandidates.Count);
                foreach (WorldMapRuntimeFinalContentGameplayBinding candidate in zoneCandidates)
                {
                    if (candidate != null && candidate.BindingKind == bindingKind)
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
                candidates = new List<WorldMapRuntimeFinalContentGameplayBinding>(zoneCandidates);
            }

            // 가장 가까운 binding 탐색 (거리 제곱 비교)
            float nearestSqrDist = float.MaxValue;
            WorldMapRuntimeFinalContentGameplayBinding nearest = null;

            foreach (WorldMapRuntimeFinalContentGameplayBinding candidate in candidates)
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
        /// 지정한 ZoneId에 속한 gameplay binding 개수를 반환한다.
        /// registry가 null이거나 zoneId가 null/empty이면 0을 반환한다.
        /// </summary>
        public int CountByZone(string zoneId)
        {
            if (registry == null)
                return 0;

            return registry.CountByZone(zoneId);
        }

        /// <summary>
        /// 지정한 RuntimeCategory에 속한 gameplay binding 개수를 반환한다.
        /// registry가 null이거나 runtimeCategory가 null/empty이면 0을 반환한다.
        /// </summary>
        public int CountByRuntimeCategory(string runtimeCategory)
        {
            if (registry == null)
                return 0;

            return registry.CountByRuntimeCategory(runtimeCategory);
        }

        /// <summary>
        /// 지정한 FinalContentKind에 속한 gameplay binding 개수를 반환한다.
        /// registry가 null이면 0을 반환한다.
        /// </summary>
        public int CountByFinalContentKind(WorldMapRuntimeFinalContentKind kind)
        {
            if (registry == null)
                return 0;

            return registry.CountByFinalContentKind(kind);
        }

        /// <summary>
        /// 지정한 BindingKind에 속한 gameplay binding 개수를 반환한다.
        /// registry가 null이면 0을 반환한다.
        /// </summary>
        public int CountByBindingKind(WorldMapRuntimeFinalContentGameplayBindingKind kind)
        {
            if (registry == null)
                return 0;

            return registry.CountByBindingKind(kind);
        }

        /// <summary>
        /// 지정한 AdapterKind에 속한 gameplay binding 개수를 반환한다.
        /// registry가 null이면 0을 반환한다.
        /// </summary>
        public int CountByAdapterKind(WorldMapRuntimeGameplayAdapterKind kind)
        {
            if (registry == null)
                return 0;

            return registry.CountByAdapterKind(kind);
        }
    }
}
