using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World.VisualAdapters
{
    /// <summary>
    /// 실제 렌더링을 변경하지 않고 current profile 값을 Inspector에서 확인할 수 있는 Debug Adapter.
    /// Phase 9 검증용.
    /// </summary>
    public class WorldMapVisualDebugAdapter : MonoBehaviour, IWorldMapVisualAdapter
    {
        [Header("Debug State")]
        [SerializeField] private WorldMapVisualProfile lastAppliedProfile;
        [SerializeField] private ZoneDepthBand lastDepthBand;
        [SerializeField] private ZoneBiomeType lastBiomeType;
        [SerializeField] private float lastNormalizedDepth01;
        [SerializeField] private bool isReady;

        [Header("Configuration")]
        [SerializeField] private bool logOnChange = false;

        /// <summary>마지막으로 적용된 VisualProfile</summary>
        public WorldMapVisualProfile LastAppliedProfile => lastAppliedProfile;

        /// <summary>마지막 DepthBand</summary>
        public ZoneDepthBand LastDepthBand => lastDepthBand;

        /// <summary>마지막 BiomeType</summary>
        public ZoneBiomeType LastBiomeType => lastBiomeType;

        /// <summary>마지막 NormalizedDepth01</summary>
        public float LastNormalizedDepth01 => lastNormalizedDepth01;

        // ===== IWorldMapVisualAdapter =====

        /// <summary>Adapter가 적용 가능한 상태인지 여부</summary>
        public bool IsReady => isReady;

        /// <summary>Adapter 식별 이름</summary>
        public string AdapterName => "DebugAdapter";

        /// <summary>
        /// Adapter를 초기화한다. Debug Adapter는 별도 준비가 필요 없다.
        /// </summary>
        public void Initialize()
        {
            isReady = true;
            UnityEngine.Debug.Log("[WorldMapVisualDebugAdapter] Initialized. Ready to capture profiles.");
        }

        /// <summary>
        /// 현재 VisualProfile을 저장한다. 실제 렌더링에는 영향을 주지 않는다.
        /// </summary>
        public void ApplyProfile(WorldMapVisualRuntimeState state, float deltaTime)
        {
            if (!isReady) return;

            // state에서 currentProfile 저장
            lastAppliedProfile = state.CurrentProfile;
            lastDepthBand = state.DepthBand;
            lastBiomeType = state.BiomeType;
            lastNormalizedDepth01 = state.NormalizedDepth01;

            // 변경 감지 로그
            if (logOnChange)
            {
                UnityEngine.Debug.Log($"[WorldMapVisualDebugAdapter] Profile captured: DepthBand={lastDepthBand}, Biome={lastBiomeType}, " +
                            $"FogDensity={lastAppliedProfile.FogDensity:F3}, Visibility={lastAppliedProfile.VisibilityDistance:F1}, " +
                            $"Exposure={lastAppliedProfile.Exposure:F2}, NormalizedDepth={lastNormalizedDepth01:F3}");
            }
        }

        /// <summary>
        /// Adapter를 초기 상태로 리셋한다.
        /// </summary>
        public void ResetAdapter()
        {
            lastAppliedProfile = default;
            lastDepthBand = default;
            lastBiomeType = default;
            lastNormalizedDepth01 = 0f;
            isReady = false;
        }
    }
}
