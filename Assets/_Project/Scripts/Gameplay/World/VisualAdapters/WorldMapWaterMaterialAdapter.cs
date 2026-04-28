using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World.VisualAdapters
{
    /// <summary>
    /// GeneratedWorldRoot/GlobalWater 아래 복제된 Water Renderer의 Material Instance에 VisualProfile 값을 적용한다.
    /// 원본 Material asset은 절대 수정하지 않는다.
    /// </summary>
    public class WorldMapWaterMaterialAdapter : MonoBehaviour, IWorldMapVisualAdapter
    {
        [Header("Target")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material runtimeMaterial;
        [SerializeField] private bool createMaterialInstanceOnInitialize = true;

        [Header("Apply Flags")]
        [SerializeField] private bool applyWaterTint = true;
        [SerializeField] private bool applyFogColorIfPropertyExists = true;
        [SerializeField] private bool logMissingProperties = false;

        // Internal
        private bool isReady;
        private bool hasLoggedMissingRenderer;
        private bool hasLoggedMissingProperties;

        // Water tint property name candidates
        private static readonly string[] WaterTintPropertyNames = new string[]
        {
            "_WaterTint", "_WaterColor", "_BaseColor", "_Color", "_ShallowColor", "_DeepColor"
        };

        // Fog color property name candidates
        private static readonly string[] FogColorPropertyNames = new string[]
        {
            "_FogColor", "_UnderwaterFogColor"
        };

        // ===== Public Getters =====

        /// <summary>대상 Renderer</summary>
        public Renderer TargetRenderer => targetRenderer;

        /// <summary>Runtime Material Instance</summary>
        public Material RuntimeMaterial => runtimeMaterial;

        // ===== IWorldMapVisualAdapter =====

        /// <summary>Adapter가 적용 가능한 상태인지 여부</summary>
        public bool IsReady => isReady;

        /// <summary>Adapter 식별 이름</summary>
        public string AdapterName => "WaterMaterialAdapter";

        /// <summary>
        /// Adapter를 초기화한다.
        /// targetRenderer가 있으면 Material Instance를 생성하고 할당한다.
        /// </summary>
        public void Initialize()
        {
            if (targetRenderer == null)
            {
                if (!hasLoggedMissingRenderer)
                {
                    UnityEngine.Debug.LogWarning("[WorldMapWaterMaterialAdapter] targetRenderer is null. Cannot initialize.");
                    hasLoggedMissingRenderer = true;
                }
                isReady = false;
                return;
            }

            // Material Instance 생성
            if (createMaterialInstanceOnInitialize && runtimeMaterial == null)
            {
                // sharedMaterial을 복제하여 Instance 생성 (원본 asset 수정 방지)
                Material original = targetRenderer.sharedMaterial;
                if (original != null)
                {
                    runtimeMaterial = new Material(original);
                    runtimeMaterial.name = $"{original.name}_RuntimeInstance";
                    targetRenderer.sharedMaterial = runtimeMaterial;
                    UnityEngine.Debug.Log($"[WorldMapWaterMaterialAdapter] Created runtime material instance: {runtimeMaterial.name}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[WorldMapWaterMaterialAdapter] targetRenderer has no sharedMaterial. Cannot create instance.");
                    isReady = false;
                    return;
                }
            }

            isReady = true;
            UnityEngine.Debug.Log($"[WorldMapWaterMaterialAdapter] Initialized. Target: {targetRenderer.name}, Material: {(runtimeMaterial != null ? runtimeMaterial.name : "null")}");
        }

        /// <summary>
        /// 현재 VisualProfile을 Water Material Instance에 적용한다.
        /// HasProperty 확인 후 SetColor/SetFloat를 수행하며, 없는 property는 warning summary만 남긴다.
        /// </summary>
        public void ApplyProfile(WorldMapVisualRuntimeState state, float deltaTime)
        {
            if (!isReady || runtimeMaterial == null) return;

            WorldMapVisualProfile profile = state.CurrentProfile;

            // Water tint 적용
            if (applyWaterTint)
            {
                bool tintApplied = TrySetColor(WaterTintPropertyNames, profile.WaterTint);
                if (!tintApplied && !hasLoggedMissingProperties && logMissingProperties)
                {
                    UnityEngine.Debug.LogWarning("[WorldMapWaterMaterialAdapter] No water tint property found on material. Skipping.");
                    hasLoggedMissingProperties = true;
                }
            }

            // Fog color 적용
            if (applyFogColorIfPropertyExists)
            {
                TrySetColor(FogColorPropertyNames, profile.FogColor);
            }
        }

        /// <summary>
        /// Adapter를 초기 상태로 리셋한다.
        /// </summary>
        public void ResetAdapter()
        {
            if (runtimeMaterial != null)
            {
                // Runtime Material Instance 정리 (선택적)
                if (Application.isPlaying)
                {
                    Destroy(runtimeMaterial);
                }
                else
                {
                    DestroyImmediate(runtimeMaterial);
                }
                runtimeMaterial = null;
            }

            isReady = false;
            hasLoggedMissingRenderer = false;
            hasLoggedMissingProperties = false;
        }

        // ===== Internal =====

        /// <summary>
        /// 후보 property 이름 목록에서 첫 번째로 존재하는 property에 Color 값을 설정한다.
        /// </summary>
        private bool TrySetColor(string[] propertyNames, Color color)
        {
            if (runtimeMaterial == null) return false;

            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (runtimeMaterial.HasProperty(propertyNames[i]))
                {
                    runtimeMaterial.SetColor(propertyNames[i], color);
                    return true;
                }
            }

            return false;
        }
    }
}
