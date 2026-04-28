using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.VisualAdapters
{
    /// <summary>
    /// 여러 IWorldMapVisualAdapter를 수집하고 일괄 적용하는 Aggregator.
    /// RuntimeBinding/VisualAdapters에 붙어 WorldMapVisualController에서 호출된다.
    /// </summary>
    public class WorldMapVisualAdapterRoot : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool autoCollectOnAwake = true;
        [SerializeField] private bool logAdapterEvents = true;
        [SerializeField] private List<MonoBehaviour> adapterBehaviours = new List<MonoBehaviour>();

        // Runtime adapter list (캐스팅된 IWorldMapVisualAdapter 목록)
        private List<IWorldMapVisualAdapter> adapters = new List<IWorldMapVisualAdapter>();
        private bool hasCollected;
        private bool hasWarnedInvalidAdapter;

        /// <summary>등록된 Adapter 개수</summary>
        public int AdapterCount => adapters.Count;

        /// <summary>수집 완료 여부</summary>
        public bool HasCollected => hasCollected;

        /// <summary>adapterBehaviours 목록의 개수 (Inspector 확인용)</summary>
        public int AdapterBehaviourCount => adapterBehaviours != null ? adapterBehaviours.Count : 0;

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            if (autoCollectOnAwake)
            {
                CollectAdapters();
            }
        }

        // ===== Public Methods =====

        /// <summary>
        /// adapterBehaviours 목록에서 IWorldMapVisualAdapter를 수집한다.
        /// null/잘못된 타입은 warning 1회만 출력하고 스킵한다.
        /// </summary>
        public void CollectAdapters()
        {
            adapters.Clear();
            hasWarnedInvalidAdapter = false;

            for (int i = 0; i < adapterBehaviours.Count; i++)
            {
                MonoBehaviour behaviour = adapterBehaviours[i];
                if (behaviour == null)
                {
                    if (!hasWarnedInvalidAdapter)
                    {
                        UnityEngine.Debug.LogWarning($"[WorldMapVisualAdapterRoot] Null adapter behaviour at index {i}. Skipping.");
                        hasWarnedInvalidAdapter = true;
                    }
                    continue;
                }

                // IWorldMapVisualAdapter로 캐스팅
                IWorldMapVisualAdapter adapter = behaviour as IWorldMapVisualAdapter;
                if (adapter == null)
                {
                    if (!hasWarnedInvalidAdapter)
                    {
                        UnityEngine.Debug.LogWarning($"[WorldMapVisualAdapterRoot] {behaviour.name} does not implement IWorldMapVisualAdapter. Skipping.");
                        hasWarnedInvalidAdapter = true;
                    }
                    continue;
                }

                adapters.Add(adapter);
            }

            hasCollected = true;

            if (logAdapterEvents)
            {
                UnityEngine.Debug.Log($"[WorldMapVisualAdapterRoot] Collected {adapters.Count} adapters from {adapterBehaviours.Count} behaviours.");
            }
        }

        /// <summary>
        /// 모든 Adapter를 초기화한다.
        /// </summary>
        public void InitializeAdapters()
        {
            if (!hasCollected)
            {
                CollectAdapters();
            }

            for (int i = 0; i < adapters.Count; i++)
            {
                try
                {
                    adapters[i].Initialize();
                    if (logAdapterEvents)
                    {
                        UnityEngine.Debug.Log($"[WorldMapVisualAdapterRoot] Initialized adapter: {adapters[i].AdapterName}");
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[WorldMapVisualAdapterRoot] Failed to initialize adapter '{adapters[i].AdapterName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 모든 Adapter에 VisualProfile을 적용한다.
        /// </summary>
        public void ApplyProfile(WorldMapVisualRuntimeState state, float deltaTime)
        {
            if (!hasCollected)
            {
                CollectAdapters();
            }

            for (int i = 0; i < adapters.Count; i++)
            {
                IWorldMapVisualAdapter adapter = adapters[i];
                if (adapter == null || !adapter.IsReady) continue;

                try
                {
                    adapter.ApplyProfile(state, deltaTime);
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[WorldMapVisualAdapterRoot] Error applying adapter '{adapter.AdapterName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 모든 Adapter를 리셋한다.
        /// </summary>
        public void ResetAdapters()
        {
            for (int i = 0; i < adapters.Count; i++)
            {
                try
                {
                    adapters[i].ResetAdapter();
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[WorldMapVisualAdapterRoot] Error resetting adapter '{adapters[i].AdapterName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 등록된 모든 Adapter 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<IWorldMapVisualAdapter> GetAdapters()
        {
            return adapters.AsReadOnly();
        }
    }
}
