using Project.Data.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Gameplay.World.VisualAdapters
{
    /// <summary>
    /// GeneratedWorldRoot/RuntimeBinding 하위 Runtime 전용 Volume에 VisualProfile 값을 적용한다.
    /// MapSettings 원본 Global Volume을 수정하지 않는다.
    /// </summary>
    public class WorldMapRuntimeVolumeAdapter : MonoBehaviour, IWorldMapVisualAdapter
    {
        [Header("Target")]
        [SerializeField] private Volume targetVolume;
        [SerializeField] private bool createRuntimeProfileInstance = true;

        [Header("Apply Flags")]
        [SerializeField] private bool applyColorAdjustments = true;
        [SerializeField] private bool applyVignette = true;

        // Internal
        private VolumeProfile runtimeProfile;
        private bool isReady;
        private bool hasLoggedMissingVolume;

        // Cached component references
        private UnityEngine.Rendering.Universal.ColorAdjustments colorAdjustments;
        private UnityEngine.Rendering.Universal.Vignette vignette;

        // ===== Public Getters =====

        /// <summary>대상 Volume</summary>
        public Volume TargetVolume => targetVolume;

        /// <summary>Runtime Volume Profile Instance</summary>
        public VolumeProfile RuntimeProfile => runtimeProfile;

        // ===== IWorldMapVisualAdapter =====

        /// <summary>Adapter가 적용 가능한 상태인지 여부</summary>
        public bool IsReady => isReady;

        /// <summary>Adapter 식별 이름</summary>
        public string AdapterName => "RuntimeVolumeAdapter";

        /// <summary>
        /// Adapter를 초기화한다.
        /// targetVolume이 있으면 Runtime 전용 VolumeProfile instance를 생성한다.
        /// </summary>
        public void Initialize()
        {
            if (targetVolume == null)
            {
                if (!hasLoggedMissingVolume)
                {
                    UnityEngine.Debug.LogWarning("[WorldMapRuntimeVolumeAdapter] targetVolume is null. Cannot initialize.");
                    hasLoggedMissingVolume = true;
                }
                isReady = false;
                return;
            }

            // Runtime 전용 VolumeProfile instance 생성
            if (createRuntimeProfileInstance && runtimeProfile == null)
            {
                // 원본 profile이 있으면 복제, 없으면 새로 생성
                if (targetVolume.sharedProfile != null)
                {
                    runtimeProfile = Object.Instantiate(targetVolume.sharedProfile);
                    runtimeProfile.name = $"{targetVolume.sharedProfile.name}_RuntimeInstance";
                }
                else
                {
                    runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    runtimeProfile.name = "RuntimeVisualVolumeProfile";
                }

                targetVolume.profile = runtimeProfile;
                UnityEngine.Debug.Log($"[WorldMapRuntimeVolumeAdapter] Created runtime volume profile instance: {runtimeProfile.name}");
            }

            // Volume component 캐싱
            if (runtimeProfile != null)
            {
                // ColorAdjustments
                if (applyColorAdjustments)
                {
                    if (!runtimeProfile.TryGet(out colorAdjustments))
                    {
                        colorAdjustments = runtimeProfile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(overrides: false);
                        colorAdjustments.SetAllOverridesTo(false);
                    }
                }

                // Vignette
                if (applyVignette)
                {
                    if (!runtimeProfile.TryGet(out vignette))
                    {
                        vignette = runtimeProfile.Add<UnityEngine.Rendering.Universal.Vignette>(overrides: false);
                        vignette.SetAllOverridesTo(false);
                    }
                }
            }

            isReady = true;
            UnityEngine.Debug.Log($"[WorldMapRuntimeVolumeAdapter] Initialized. Volume: {targetVolume.name}, Profile: {(runtimeProfile != null ? runtimeProfile.name : "null")}");
        }

        /// <summary>
        /// 현재 VisualProfile을 Runtime Volume에 적용한다.
        /// ColorAdjustments/Vignette component가 있으면 값을 설정한다.
        /// </summary>
        public void ApplyProfile(WorldMapVisualRuntimeState state, float deltaTime)
        {
            if (!isReady || targetVolume == null || runtimeProfile == null) return;

            WorldMapVisualProfile profile = state.CurrentProfile;

            // ColorAdjustments 적용
            if (applyColorAdjustments && colorAdjustments != null)
            {
                // exposure → postExposure
                colorAdjustments.postExposure.Override(profile.Exposure - 1f); // 0~2 범위를 -1~1로 변환
                colorAdjustments.postExposure.overrideState = true;

                // saturation
                colorAdjustments.saturation.Override(profile.Saturation * 100f - 100f); // 0~2 범위를 -100~100으로 변환
                colorAdjustments.saturation.overrideState = true;

                // contrast
                colorAdjustments.contrast.Override(profile.Contrast * 100f - 100f); // 0~2 범위를 -100~100으로 변환
                colorAdjustments.contrast.overrideState = true;
            }

            // Vignette 적용
            if (applyVignette && vignette != null)
            {
                // vignetteIntensity
                vignette.intensity.Override(profile.VignetteIntensity);
                vignette.intensity.overrideState = true;

                // vignette color → fogColor
                vignette.color.Override(profile.FogColor);
                vignette.color.overrideState = true;
            }
        }

        /// <summary>
        /// Adapter를 초기 상태로 리셋한다.
        /// </summary>
        public void ResetAdapter()
        {
            if (runtimeProfile != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeProfile);
                }
                else
                {
                    DestroyImmediate(runtimeProfile);
                }
                runtimeProfile = null;
            }

            colorAdjustments = null;
            vignette = null;
            isReady = false;
            hasLoggedMissingVolume = false;
        }
    }
}
