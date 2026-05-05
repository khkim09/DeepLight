using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// 기존 Harvest 시스템이 사용할 최종 consumer-facing facade.
    /// 내부적으로 WorldMapRuntimeHarvestInteractionTargetQueryService만 참조하고,
    /// scene scan은 하지 않는다. QueryService에서 받은 TargetAdapter를
    /// ConsumerContext로 변환하여 반환한다.
    /// </summary>
    public class WorldMapRuntimeHarvestInteractionTargetConsumerService : MonoBehaviour
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("Awake에서 자동으로 TryInitializeFromRoot(transform)를 호출할지 여부")]
        private bool initializeOnAwake = true;

        [SerializeField, Tooltip("OnEnable에서 QueryService가 비어 있으면 자동으로 TryInitializeFromRoot(transform)를 호출할지 여부")]
        private bool initializeOnEnableIfEmpty = true;

        [SerializeField, Tooltip("Runtime 초기화 관련 로그를 출력할지 여부")]
        private bool logRuntimeInitialization;

        [SerializeField, Tooltip("참조할 QueryService (Inspector에서 수동 할당 가능)")]
        private WorldMapRuntimeHarvestInteractionTargetQueryService queryService;

        // ===== Internal State =====

        /// <summary>초기화 완료 여부</summary>
        private bool isInitialized;

        // ===== Public Properties =====

        /// <summary>ConsumerService가 초기화되었는지 여부</summary>
        public bool IsInitialized => isInitialized;

        /// <summary>QueryService가 할당되어 있는지 여부</summary>
        public bool HasQueryService => queryService != null;

        /// <summary>ConsumerService가 관리하는 consumer context 총 개수</summary>
        public int Count => queryService != null ? queryService.Count : 0;

        // ===== Unity Lifecycle =====

        /// <summary>
        /// Awake: initializeOnAwake가 true이면 TryInitializeFromRoot(transform)를 호출한다.
        /// </summary>
        private void Awake()
        {
            if (initializeOnAwake && !isInitialized)
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionTargetConsumerService] Awake: initializeOnAwake is true. Initializing from root.");

                TryInitializeFromRoot(transform);
                isInitialized = true;
            }
        }

        /// <summary>
        /// OnEnable: initializeOnEnableIfEmpty가 true이고 QueryService가 없거나 count가 0이면
        /// TryInitializeFromRoot(transform)를 호출한다.
        /// </summary>
        private void OnEnable()
        {
            if (initializeOnEnableIfEmpty && (queryService == null || queryService.Count == 0))
            {
                if (logRuntimeInitialization)
                    UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionTargetConsumerService] OnEnable: queryService is null or empty. Initializing from root.");

                TryInitializeFromRoot(transform);
                isInitialized = true;
            }
        }

        // ===== Public API =====

        /// <summary>
        /// 지정한 root Transform 하위에서 QueryService를 찾거나 생성하고 초기화한다.
        /// 직접 FindObjectsByType을 호출하지 않고 root 하위 GetComponentInChildren만 사용한다.
        /// </summary>
        /// <param name="root">검색 기준 root Transform. null이면 this.transform 사용.</param>
        /// <returns>초기화 성공 여부</returns>
        public bool TryInitializeFromRoot(Transform root)
        {
            if (root == null)
                root = transform;

            // QueryService가 없으면 root에서 찾아보고 없으면 root.gameObject에 추가
            if (queryService == null)
            {
                queryService = root.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetQueryService>(true);

                if (queryService == null)
                {
                    // root.gameObject에 QueryService 추가
                    queryService = root.gameObject.AddComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>();
                    if (logRuntimeInitialization)
                        UnityEngine.Debug.Log("[WorldMapRuntimeHarvestInteractionTargetConsumerService] Added WorldMapRuntimeHarvestInteractionTargetQueryService to root.");
                }
            }

            // QueryService 초기화 (아직 초기화되지 않은 경우)
            if (!queryService.HasRegistry || queryService.Count == 0)
            {
                queryService.TryInitializeFromRoot(root);
            }

            isInitialized = queryService.Count > 0;
            return isInitialized;
        }

        /// <summary>
        /// 모든 consumer context를 반환한다.
        /// 매 호출마다 새 리스트를 생성하지만, GC 부담을 줄이기 위해
        /// 내부 adapter 목록을 순회하며 context로 변환한다.
        /// </summary>
        /// <returns>모든 consumer context의 읽기 전용 목록</returns>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetAll()
        {
            if (queryService == null || queryService.Count == 0)
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            var adapters = queryService.GetAll();
            var contexts = new List<WorldMapRuntimeHarvestInteractionTargetConsumerContext>(adapters.Count);

            foreach (var adapter in adapters)
            {
                var context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(adapter);
                contexts.Add(context);
            }

            return contexts;
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 consumer context 목록을 반환한다.
        /// </summary>
        /// <param name="zoneId">필터링할 Zone ID</param>
        /// <returns>해당 ZoneId의 consumer context 목록</returns>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByZoneId(string zoneId)
        {
            if (queryService == null || string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            var adapters = queryService.GetByZoneId(zoneId);
            return ConvertAdaptersToContexts(adapters);
        }

        /// <summary>
        /// 지정한 RuntimeKey에 속한 consumer context 목록을 반환한다.
        /// </summary>
        /// <param name="runtimeKey">필터링할 RuntimeKey</param>
        /// <returns>해당 RuntimeKey의 consumer context 목록</returns>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByRuntimeKey(string runtimeKey)
        {
            if (queryService == null || string.IsNullOrEmpty(runtimeKey))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            var adapters = queryService.GetByRuntimeKey(runtimeKey);
            return ConvertAdaptersToContexts(adapters);
        }

        /// <summary>
        /// 지정한 InteractionKind에 속한 consumer context 목록을 반환한다.
        /// </summary>
        /// <param name="interactionKind">필터링할 InteractionKind</param>
        /// <returns>해당 InteractionKind의 consumer context 목록</returns>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByInteractionKind(
            WorldMapRuntimeHarvestInteractionCandidateKind interactionKind)
        {
            if (queryService == null)
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            var adapters = queryService.GetByInteractionKind(interactionKind);
            return ConvertAdaptersToContexts(adapters);
        }

        /// <summary>
        /// 지정한 TargetKind에 속한 consumer context 목록을 반환한다.
        /// </summary>
        /// <param name="targetKind">필터링할 TargetKind</param>
        /// <returns>해당 TargetKind의 consumer context 목록</returns>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByTargetKind(
            WorldMapRuntimeHarvestInteractionTargetKind targetKind)
        {
            if (queryService == null)
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            var adapters = queryService.GetByTargetKind(targetKind);
            return ConvertAdaptersToContexts(adapters);
        }

        /// <summary>
        /// SourceMarkerId로 consumer context를 조회한다.
        /// </summary>
        /// <param name="sourceMarkerId">조회할 SourceMarkerId</param>
        /// <param name="context">찾은 consumer context (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetBySourceMarkerId(string sourceMarkerId, out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (queryService == null || string.IsNullOrEmpty(sourceMarkerId))
                return false;

            if (queryService.TryGetBySourceMarkerId(sourceMarkerId, out var adapter))
            {
                context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(adapter);
                return context.IsReady;
            }

            return false;
        }

        /// <summary>
        /// 지정한 position에서 가장 가까운 consumer context를 찾는다.
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="context">가장 가까운 consumer context (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 position, out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (queryService == null || queryService.Count == 0)
                return false;

            if (queryService.TryGetNearest(position, out var adapter))
            {
                context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(adapter);
                return context.IsReady;
            }

            return false;
        }

        /// <summary>
        /// 지정한 position에서 가장 가까운, 지정한 runtimeKey를 가진 consumer context를 찾는다.
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="runtimeKey">필터링할 RuntimeKey</param>
        /// <param name="context">가장 가까운 consumer context (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 position, string runtimeKey, out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (queryService == null || queryService.Count == 0 || string.IsNullOrEmpty(runtimeKey))
                return false;

            if (queryService.TryGetNearest(position, runtimeKey, out var adapter))
            {
                context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(adapter);
                return context.IsReady;
            }

            return false;
        }

        /// <summary>
        /// 지정한 position에서 가장 가까운, 지정한 InteractionKind를 가진 consumer context를 찾는다.
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="interactionKind">필터링할 InteractionKind</param>
        /// <param name="context">가장 가까운 consumer context (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 position, WorldMapRuntimeHarvestInteractionCandidateKind interactionKind,
            out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (queryService == null || queryService.Count == 0)
                return false;

            // QueryService에는 InteractionKind 기반 TryGetNearest가 없으므로
            // GetAll()에서 필터링 후 가장 가까운 것을 찾는다.
            float minDistSq = float.MaxValue;
            WorldMapRuntimeHarvestInteractionTargetAdapter nearest = null;

            foreach (var adapter in queryService.GetAll())
            {
                if (!adapter.IsInteractionKind(interactionKind))
                    continue;

                float distSq = (adapter.WorldPosition - position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = adapter;
                }
            }

            if (nearest != null)
            {
                context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(nearest);
                return context.IsReady;
            }

            return false;
        }

        /// <summary>
        /// 지정한 position에서 가장 가까운, 지정한 TargetKind를 가진 consumer context를 찾는다.
        /// </summary>
        /// <param name="position">기준 위치</param>
        /// <param name="targetKind">필터링할 TargetKind</param>
        /// <param name="context">가장 가까운 consumer context (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearest(Vector3 position, WorldMapRuntimeHarvestInteractionTargetKind targetKind,
            out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (queryService == null || queryService.Count == 0)
                return false;

            if (queryService.TryGetNearest(position, targetKind, out var adapter))
            {
                context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(adapter);
                return context.IsReady;
            }

            return false;
        }

        /// <summary>
        /// 지정한 ZoneId 내에서 position에 가장 가까운 consumer context를 찾는다.
        /// </summary>
        /// <param name="zoneId">필터링할 ZoneId</param>
        /// <param name="position">기준 위치</param>
        /// <param name="context">가장 가까운 consumer context (없으면 null)</param>
        /// <returns>찾은 경우 true, 없으면 false</returns>
        public bool TryGetNearestInZone(string zoneId, Vector3 position,
            out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (queryService == null || queryService.Count == 0 || string.IsNullOrEmpty(zoneId))
                return false;

            if (queryService.TryGetNearestInZone(zoneId, position, out var adapter))
            {
                context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(adapter);
                return context.IsReady;
            }

            return false;
        }

        /// <summary>
        /// 지정한 ZoneId에 속한 consumer context 개수를 반환한다.
        /// </summary>
        /// <param name="zoneId">조회할 Zone ID</param>
        /// <returns>해당 ZoneId의 consumer context 개수</returns>
        public int CountByZone(string zoneId)
        {
            if (queryService == null || string.IsNullOrEmpty(zoneId))
                return 0;

            return queryService.CountByZone(zoneId);
        }

        /// <summary>
        /// 지정한 RuntimeKey에 속한 consumer context 개수를 반환한다.
        /// </summary>
        /// <param name="runtimeKey">조회할 RuntimeKey</param>
        /// <returns>해당 RuntimeKey의 consumer context 개수</returns>
        public int CountByRuntimeKey(string runtimeKey)
        {
            if (queryService == null || string.IsNullOrEmpty(runtimeKey))
                return 0;

            return queryService.CountByRuntimeKey(runtimeKey);
        }

        /// <summary>
        /// 지정한 InteractionKind에 속한 consumer context 개수를 반환한다.
        /// </summary>
        /// <param name="interactionKind">조회할 InteractionKind</param>
        /// <returns>해당 InteractionKind의 consumer context 개수</returns>
        public int CountByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind interactionKind)
        {
            if (queryService == null)
                return 0;

            return queryService.CountByInteractionKind(interactionKind);
        }

        /// <summary>
        /// 지정한 TargetKind에 속한 consumer context 개수를 반환한다.
        /// </summary>
        /// <param name="targetKind">조회할 TargetKind</param>
        /// <returns>해당 TargetKind의 consumer context 개수</returns>
        public int CountByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind targetKind)
        {
            if (queryService == null)
                return 0;

            return queryService.CountByTargetKind(targetKind);
        }

        // ===== Private Helpers =====

        /// <summary>
        /// TargetAdapter 목록을 ConsumerContext 목록으로 변환한다.
        /// GC 부담을 줄이기 위해 매 호출마다 새 리스트를 생성하지만,
        /// 단순한 구조로 작성하여 성능 영향을 최소화한다.
        /// </summary>
        /// <param name="adapters">변환할 TargetAdapter 목록</param>
        /// <returns>변환된 ConsumerContext 목록</returns>
        private static IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> ConvertAdaptersToContexts(
            IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> adapters)
        {
            if (adapters == null || adapters.Count == 0)
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            var contexts = new List<WorldMapRuntimeHarvestInteractionTargetConsumerContext>(adapters.Count);

            foreach (var adapter in adapters)
            {
                if (adapter == null)
                    continue;

                var context = new WorldMapRuntimeHarvestInteractionTargetConsumerContext();
                context.Configure(adapter);
                contexts.Add(context);
            }

            return contexts;
        }
    }
}
