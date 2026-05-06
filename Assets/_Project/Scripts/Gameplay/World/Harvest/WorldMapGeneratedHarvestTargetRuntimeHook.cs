using System.Collections.Generic;
using Project.Gameplay.Harvest;
using Project.Gameplay.World.Content;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// HarvestPointInteractor에 부착하여 generated harvest target을
    /// 기존 trigger 기반 target flow를 깨지 않고 보조 후보로 제공하는 얇은 helper 컴포넌트.
    /// 내부적으로 WorldMapHarvestInteractionTargetProvider를 source of truth로 사용하며,
    /// Provider를 통해 모든 generated target을 조회/캐시한다.
    /// Submarine처럼 GeneratedWorldRoot 하위가 아닌 오브젝트에 붙어 있어도
    /// Provider 기반 조회이므로 정상 동작한다.
    /// </summary>
    public class WorldMapGeneratedHarvestTargetRuntimeHook : MonoBehaviour
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("Generated target provider 참조 (Inspector에서 수동 할당 가능)")]
        private WorldMapHarvestInteractionTargetProvider provider;

        [SerializeField, Tooltip("Generated target 탐지 반경")]
        private float detectionRadius = 4f;

        [SerializeField, Tooltip("초기화/연결 관련 로그 출력 여부")]
        private bool logInitialization;

        // ===== Internal State =====

        /// <summary>초기화 완료 여부</summary>
        private bool isInitialized;

        /// <summary>
        /// Provider 기반으로 캐시된 IHarvestTarget 목록.
        /// Provider.GetAll()로 얻은 context에서 SourceMarkerId를 추출하고,
        /// scene 전체에서 WorldMapGeneratedHarvestTarget을 찾아 캐시한다.
        /// </summary>
        private readonly List<IHarvestTarget> cachedTargets = new();

        /// <summary>SourceMarkerId → WorldMapGeneratedHarvestTarget 빠른 조회용 딕셔너리</summary>
        private Dictionary<string, WorldMapGeneratedHarvestTarget> targetByMarkerId;

        // ===== Public Properties =====

        /// <summary>Hook이 초기화되었는지 여부</summary>
        public bool IsInitialized => isInitialized;

        /// <summary>Provider 참조 (외부 주입용)</summary>
        public WorldMapHarvestInteractionTargetProvider Provider
        {
            get => provider;
            set => provider = value;
        }

        /// <summary>탐지 반경</summary>
        public float DetectionRadius
        {
            get => detectionRadius;
            set => detectionRadius = Mathf.Max(0.1f, value);
        }

        /// <summary>
        /// 캐시된 target 개수.
        /// cachedTargets가 비어 있어도 provider.Count > 0이면 provider.Count를 fallback으로 반환한다.
        /// </summary>
        public int CachedTargetCount
        {
            get
            {
                if (cachedTargets.Count > 0)
                    return cachedTargets.Count;

                // cachedTargets가 0이어도 provider가 알고 있으면 provider.Count를 반환
                if (provider != null && provider.IsReady)
                    return provider.Count;

                return 0;
            }
        }

        /// <summary>Provider 참조가 할당되었는지 여부</summary>
        public bool HasProvider => provider != null;

        // ===== Unity Lifecycle =====

        /// <summary>
        /// Awake: provider가 없으면 scene에서 WorldMapHarvestInteractionTargetProvider를 찾고,
        /// Provider 기반으로 target 캐시를 구축한다.
        /// </summary>
        private void Awake()
        {
            TryInitialize();
        }

        // ===== Public API =====

        /// <summary>
        /// Provider를 할당하고 강제로 캐시를 리빌드한다.
        /// 이미 초기화된 경우에도 재할당 및 리빌드를 수행한다.
        /// </summary>
        /// <param name="newProvider">할당할 provider</param>
        public void AssignProvider(WorldMapHarvestInteractionTargetProvider newProvider)
        {
            provider = newProvider;
            isInitialized = false;
            ForceRebuildCache();
        }

        /// <summary>
        /// 초기화를 수행한다. 이미 초기화된 경우 아무 작업도 하지 않는다.
        /// Provider가 없으면 scene/root에서 WorldMapHarvestInteractionTargetProvider를 찾는다.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public bool TryInitialize()
        {
            if (isInitialized)
                return true;

            // 1. Provider 찾기
            if (provider == null)
            {
                TryFindProvider();
            }

            if (provider == null)
            {
                if (logInitialization)
                    UnityEngine.Debug.LogWarning("[WorldMapGeneratedHarvestTargetRuntimeHook] Provider not found. Hook disabled.");
                isInitialized = false;
                return false;
            }

            // 2. Provider 초기화
            if (!provider.IsReady)
            {
                provider.TryInitialize();
            }

            if (!provider.IsReady || provider.Count == 0)
            {
                if (logInitialization)
                    UnityEngine.Debug.LogWarning("[WorldMapGeneratedHarvestTargetRuntimeHook] Provider is not ready or empty. Hook disabled.");
                isInitialized = false;
                return false;
            }

            // 3. Provider 기반 캐시 구축
            ForceRebuildCache();

            isInitialized = true;

            if (logInitialization)
            {
                UnityEngine.Debug.Log($"[WorldMapGeneratedHarvestTargetRuntimeHook] Initialized. IsInitialized={isInitialized}, CachedTargetCount={CachedTargetCount}, ProviderCount={provider.Count}");
            }

            return isInitialized;
        }

        /// <summary>
        /// 지정한 provider로 초기화를 수행한다.
        /// provider 참조를 할당하고 초기화까지 한 번에 수행한다.
        /// </summary>
        /// <param name="targetProvider">할당할 provider</param>
        /// <returns>초기화 성공 여부</returns>
        public bool TryInitialize(WorldMapHarvestInteractionTargetProvider targetProvider)
        {
            provider = targetProvider;
            isInitialized = false;
            return TryInitialize();
        }

        /// <summary>
        /// Provider 기반으로 target 캐시를 강제로 리빌드한다.
        /// Provider.GetAll()로 모든 context를 가져오고,
        /// scene 전체에서 WorldMapGeneratedHarvestTarget을 찾아 SourceMarkerId로 매칭하여 캐시한다.
        /// </summary>
        public void ForceRebuildCache()
        {
            cachedTargets.Clear();
            targetByMarkerId = null;

            if (provider == null)
            {
                TryFindProvider();
            }

            if (provider == null)
            {
                isInitialized = false;
                return;
            }

            // Provider 초기화 (아직 안 된 경우)
            if (!provider.IsReady)
            {
                provider.TryInitialize();
            }

            if (!provider.IsReady || provider.Count == 0)
            {
                isInitialized = false;
                return;
            }

            // Provider.GetAll()로 모든 context 수집
            var allContexts = provider.GetAll();
            if (allContexts == null || allContexts.Count == 0)
            {
                isInitialized = false;
                return;
            }

            // scene 전체에서 WorldMapGeneratedHarvestTarget을 찾는다.
            // hierarchy에 관계없이 모든 target을 찾아 SourceMarkerId 기반 딕셔너리 구축
            var allTargetsInScene = FindObjectsByType<WorldMapGeneratedHarvestTarget>(FindObjectsSortMode.None);
            targetByMarkerId = new Dictionary<string, WorldMapGeneratedHarvestTarget>();

            foreach (var target in allTargetsInScene)
            {
                if (target == null || !target.IsAvailable)
                    continue;

                string markerId = target.SourceMarkerId;
                if (string.IsNullOrEmpty(markerId))
                    continue;

                // 중복 방지 (첫 번째 등록 유지)
                if (!targetByMarkerId.ContainsKey(markerId))
                {
                    targetByMarkerId.Add(markerId, target);
                }
            }

            // Provider context의 SourceMarkerId와 매칭되는 target만 캐시
            foreach (var context in allContexts)
            {
                if (context == null || !context.IsReady)
                    continue;

                string markerId = context.SourceMarkerId;
                if (string.IsNullOrEmpty(markerId))
                    continue;

                // targetByMarkerId에서 찾거나, 없으면 context 정보로 새 target을 찾을 수 없으므로 skip
                if (targetByMarkerId.TryGetValue(markerId, out var generatedTarget))
                {
                    if (generatedTarget != null && generatedTarget.IsAvailable)
                    {
                        cachedTargets.Add(generatedTarget);
                    }
                }
            }

            if (logInitialization)
            {
                UnityEngine.Debug.Log($"[WorldMapGeneratedHarvestTargetRuntimeHook] ForceRebuildCache. Provider contexts={allContexts.Count}, Scene targets={allTargetsInScene.Length}, Cached targets={cachedTargets.Count}");
            }
        }

        /// <summary>
        /// 지정한 position에서 maxDistance 내 가장 가까운 IHarvestTarget을 반환한다.
        /// maxDistance <= 0이면 detectionRadius를 사용한다.
        /// provider.TryGetNearest를 우선 사용하고, 실패 시 cachedTargets에서 거리 기반 검색을 수행한다.
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="maxDistance">최대 탐지 거리 (0 이하이면 detectionRadius 사용)</param>
        /// <param name="target">가장 가까운 generated IHarvestTarget (없으면 null)</param>
        /// <returns>generated target을 찾은 경우 true</returns>
        public bool TryGetNearestGeneratedTarget(Vector3 position, float maxDistance, out IHarvestTarget target)
        {
            target = null;

            float effectiveMaxDistance = maxDistance > 0f ? maxDistance : detectionRadius;

            // provider나 캐시가 준비되지 않았으면 초기화/리빌드 시도
            if (provider == null || !isInitialized || cachedTargets.Count == 0)
            {
                TryInitialize();
                if (cachedTargets.Count == 0)
                {
                    ForceRebuildCache();
                }
            }

            if (provider == null || !isInitialized)
                return false;

            // 우선 provider.TryGetNearest를 통해 nearest context 조회
            if (provider.TryGetNearest(position, out var nearestContext))
            {
                if (nearestContext != null && nearestContext.IsReady)
                {
                    // 거리 검증
                    float distanceSq = (nearestContext.WorldPosition - position).sqrMagnitude;
                    if (distanceSq <= effectiveMaxDistance * effectiveMaxDistance)
                    {
                        // SourceMarkerId로 WorldMapGeneratedHarvestTarget 조회
                        string markerId = nearestContext.SourceMarkerId;
                        if (!string.IsNullOrEmpty(markerId) && targetByMarkerId != null)
                        {
                            if (targetByMarkerId.TryGetValue(markerId, out var generatedTarget))
                            {
                                if (generatedTarget != null && generatedTarget.IsAvailable)
                                {
                                    target = generatedTarget;
                                    return true;
                                }
                            }
                        }

                        // targetByMarkerId에 없으면 cachedTargets에서 nearest 검색
                        return TryGetNearestFromCache(position, effectiveMaxDistance, out target);
                    }
                }
            }

            // provider.TryGetNearest 실패 시 cachedTargets에서 nearest 검색
            return TryGetNearestFromCache(position, effectiveMaxDistance, out target);
        }

        /// <summary>
        /// 지정한 position에서 detectionRadius 내 가장 가까운 IHarvestTarget을 반환한다.
        /// hasExistingTriggerTarget 파라미터는 이전 버전과의 호환성을 위해 유지하되,
        /// 실제 로직에는 영향을 주지 않는다 (provider 기반으로 동작).
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="hasExistingTriggerTarget">기존 trigger 기반 target 존재 여부 (무시됨)</param>
        /// <param name="target">가장 가까운 generated IHarvestTarget (없으면 null)</param>
        /// <returns>generated target을 찾은 경우 true</returns>
        public bool TryGetNearestGeneratedTarget(Vector3 position, bool hasExistingTriggerTarget, out IHarvestTarget target)
        {
            // hasExistingTriggerTarget 파라미터는 무시하고 detectionRadius 기준으로 검색
            return TryGetNearestGeneratedTarget(position, detectionRadius, out target);
        }

        /// <summary>
        /// SourceMarkerId로 WorldMapGeneratedHarvestTarget을 조회한다.
        /// 캐시 기반으로 동작하며 hierarchy scan을 수행하지 않는다.
        /// </summary>
        /// <param name="sourceMarkerId">조회할 SourceMarkerId</param>
        /// <param name="target">찾은 WorldMapGeneratedHarvestTarget (없으면 null)</param>
        /// <returns>찾은 경우 true</returns>
        public bool TryGetTargetBySourceMarkerId(string sourceMarkerId, out WorldMapGeneratedHarvestTarget target)
        {
            target = null;

            if (targetByMarkerId == null || string.IsNullOrEmpty(sourceMarkerId))
                return false;

            return targetByMarkerId.TryGetValue(sourceMarkerId, out target);
        }

        // ===== Private Methods =====

        /// <summary>
        /// scene/root에서 WorldMapHarvestInteractionTargetProvider를 찾는다.
        /// GeneratedWorldRoot 전체 기준으로 찾는다.
        /// </summary>
        private void TryFindProvider()
        {
            // 1. 부모 체인에서 찾기
            provider = GetComponentInParent<WorldMapHarvestInteractionTargetProvider>();

            if (provider == null)
            {
                // 2. GeneratedWorldRoot 찾기
                GameObject generatedRoot = GameObject.Find("GeneratedWorldRoot");
                if (generatedRoot != null)
                {
                    provider = generatedRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);
                }
            }

            if (provider == null)
            {
                // 3. scene 전체에서 찾기 (최후의 수단)
                var allProviders = FindObjectsByType<WorldMapHarvestInteractionTargetProvider>(FindObjectsSortMode.None);
                if (allProviders != null && allProviders.Length > 0)
                {
                    provider = allProviders[0];
                }
            }
        }

        /// <summary>
        /// cachedTargets에서 position 기준 effectiveMaxDistance 내 가장 가까운 target을 찾는다.
        /// </summary>
        private bool TryGetNearestFromCache(Vector3 position, float effectiveMaxDistance, out IHarvestTarget target)
        {
            target = null;

            if (cachedTargets.Count == 0)
                return false;

            float nearestDistSq = effectiveMaxDistance * effectiveMaxDistance;
            IHarvestTarget nearest = null;

            foreach (var cachedTarget in cachedTargets)
            {
                if (cachedTarget == null || !cachedTarget.IsAvailable)
                    continue;

                // WorldMapGeneratedHarvestTarget이면 WorldPosition 사용
                Vector3 targetPos;
                if (cachedTarget is WorldMapGeneratedHarvestTarget generatedTarget)
                {
                    targetPos = generatedTarget.WorldPosition;
                }
                else if (cachedTarget is MonoBehaviour mb)
                {
                    targetPos = mb.transform.position;
                }
                else
                {
                    continue;
                }

                float distSq = (targetPos - position).sqrMagnitude;
                if (distSq <= nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = cachedTarget;
                }
            }

            if (nearest != null)
            {
                target = nearest;
                return true;
            }

            return false;
        }
    }
}
