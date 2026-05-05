using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// IHarvestInteractionTargetProvider의 MonoBehaviour 구현체.
    /// 내부적으로 WorldMapRuntimeHarvestInteractionTargetConsumerService를 참조하여
    /// generated map 기반 harvest 후보를 기존 Harvest 시스템에 제공한다.
    /// Awake/OnEnable에서 ConsumerService를 찾고, 실패 시 IsReady=false로 유지된다.
    /// 매 프레임 FindObjectsByType을 호출하지 않고 캐시된 참조만 사용한다.
    /// </summary>
    public class WorldMapHarvestInteractionTargetProvider : MonoBehaviour, IHarvestInteractionTargetProvider
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("ConsumerService 참조 (Inspector에서 수동 할당 가능)")]
        private WorldMapRuntimeHarvestInteractionTargetConsumerService consumerService;

        [SerializeField, Tooltip("Awake에서 자동 초기화할지 여부")]
        private bool initializeOnAwake = true;

        [SerializeField, Tooltip("초기화/연결 관련 로그 출력 여부")]
        private bool logInitialization;

        // ===== Internal State =====

        /// <summary>초기화 완료 여부</summary>
        private bool isReady;

        /// <summary>ConsumerService를 찾기 위한 root Transform 캐시</summary>
        private Transform cachedRoot;

        // ===== Public Properties =====

        /// <summary>Provider가 초기화되어 사용 가능한 상태인지 여부</summary>
        public bool IsReady => isReady;

        /// <summary>Provider가 관리하는 consumer context 총 개수</summary>
        public int Count
        {
            get
            {
                if (!isReady || consumerService == null)
                    return 0;
                return consumerService.Count;
            }
        }

        // ===== Unity Lifecycle =====

        /// <summary>
        /// Awake: initializeOnAwake가 true이면 TryInitialize()를 호출한다.
        /// </summary>
        private void Awake()
        {
            if (initializeOnAwake && !isReady)
            {
                TryInitialize();
            }
        }

        /// <summary>
        /// OnEnable: isReady가 false이고 consumerService가 없으면 TryInitialize()를 재시도한다.
        /// </summary>
        private void OnEnable()
        {
            if (!isReady && consumerService == null)
            {
                TryInitialize();
            }
        }

        // ===== Public API =====

        /// <summary>
        /// ConsumerService를 찾고 초기화한다.
        /// 이미 초기화된 경우 아무 작업도 하지 않는다.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public bool TryInitialize()
        {
            if (isReady && consumerService != null)
                return true;

            // ConsumerService가 없으면 root에서 찾는다.
            if (consumerService == null)
            {
                // cachedRoot가 없으면 this.transform을 root로 사용
                Transform root = cachedRoot ?? transform;

                // root 하위에서 ConsumerService 찾기 (GetComponentInChildren, 매 프레임 아님)
                consumerService = root.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);

                if (consumerService == null)
                {
                    // GeneratedWorldRoot 또는 parent root에서 한 번 더 찾기
                    Transform parentRoot = transform.parent;
                    while (parentRoot != null && consumerService == null)
                    {
                        consumerService = parentRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);
                        parentRoot = parentRoot.parent;
                    }
                }

                if (consumerService == null)
                {
                    if (logInitialization)
                        UnityEngine.Debug.LogWarning("[WorldMapHarvestInteractionTargetProvider] ConsumerService not found in hierarchy. IsReady=false.");
                    isReady = false;
                    return false;
                }

                // root 캐시
                cachedRoot = consumerService.transform;
            }

            // ConsumerService가 초기화되지 않았으면 초기화
            if (!consumerService.IsInitialized || consumerService.Count == 0)
            {
                consumerService.TryInitializeFromRoot(cachedRoot ?? consumerService.transform);
            }

            isReady = consumerService.Count > 0;

            if (logInitialization)
            {
                UnityEngine.Debug.Log($"[WorldMapHarvestInteractionTargetProvider] Initialized. IsReady={isReady}, Count={consumerService.Count}");
            }

            return isReady;
        }

        /// <summary>
        /// ConsumerService 참조를 외부에서 주입한다.
        /// 이미 초기화된 경우 무시된다.
        /// </summary>
        /// <param name="service">주입할 ConsumerService</param>
        public void SetConsumerService(WorldMapRuntimeHarvestInteractionTargetConsumerService service)
        {
            if (service == null)
                return;

            consumerService = service;
            cachedRoot = service.transform;

            if (!consumerService.IsInitialized || consumerService.Count == 0)
            {
                consumerService.TryInitializeFromRoot(cachedRoot);
            }

            isReady = consumerService.Count > 0;
        }

        // ===== IHarvestInteractionTargetProvider API =====

        /// <summary>모든 consumer context를 반환한다.</summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetAll()
        {
            if (!isReady || consumerService == null)
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            return consumerService.GetAll();
        }

        /// <summary>지정한 ZoneId에 속한 consumer context 목록을 반환한다.</summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByZoneId(string zoneId)
        {
            if (!isReady || consumerService == null || string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            return consumerService.GetByZoneId(zoneId);
        }

        /// <summary>지정한 RuntimeKey에 속한 consumer context 목록을 반환한다.</summary>
        public IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetConsumerContext> GetByRuntimeKey(string runtimeKey)
        {
            if (!isReady || consumerService == null || string.IsNullOrEmpty(runtimeKey))
                return System.Array.Empty<WorldMapRuntimeHarvestInteractionTargetConsumerContext>();

            return consumerService.GetByRuntimeKey(runtimeKey);
        }

        /// <summary>지정한 position에서 가장 가까운 consumer context를 찾는다.</summary>
        public bool TryGetNearest(Vector3 position, out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (!isReady || consumerService == null)
                return false;

            return consumerService.TryGetNearest(position, out context);
        }

        /// <summary>지정한 position에서 가장 가까운, 지정한 runtimeKey를 가진 consumer context를 찾는다.</summary>
        public bool TryGetNearest(Vector3 position, string runtimeKey, out WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            context = null;

            if (!isReady || consumerService == null || string.IsNullOrEmpty(runtimeKey))
                return false;

            return consumerService.TryGetNearest(position, runtimeKey, out context);
        }
    }
}
