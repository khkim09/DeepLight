using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>현재 활성 카메라의 깊이에 따라 수중 환경값을 갱신한다.</summary>
    public class DepthEnvironmentController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform activeCameraTransform; // 현재 깊이 판정에 사용할 활성 카메라
        [SerializeField] private Volume globalVolume; // 수중 후처리 제어용 글로벌 볼륨
        [SerializeField] private Material underwaterMaterial; // Full Screen Pass용 수중 머티리얼

        [Header("Depth Range")]
        [SerializeField] private float surfaceY = 0f;      // 수면 높이
        [SerializeField] private float maxDepthY = -100f;  // 최대 심해 기준 높이
        [SerializeField] private AnimationCurve depthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Underwater Material - Shallow To Deep")]
        [SerializeField] private Color shallowWaterColor = new(0.38f, 0.63f, 0.72f, 1f);
        [SerializeField] private Color deepWaterColor = new(0.06f, 0.18f, 0.25f, 1f);
        [SerializeField] private float shallowAlpha = 0.025f;
        [SerializeField] private float deepAlpha = 0.08f;
        [SerializeField] private float shallowDistance = 10f;
        [SerializeField] private float deepDistance = 4f;
        [SerializeField] private float shallowRefraction = 0.02f;
        [SerializeField] private float deepRefraction = 0.01f;

        [Header("Volume - Color Adjustments")]
        [SerializeField] private Color shallowColorFilter = new(0.78f, 0.90f, 1.00f, 1f);
        [SerializeField] private Color deepColorFilter = new(0.30f, 0.55f, 0.78f, 1f);
        [SerializeField] private float shallowPostExposure = -0.10f;
        [SerializeField] private float deepPostExposure = -1.20f;
        [SerializeField] private float shallowContrast = 0f;
        [SerializeField] private float deepContrast = -15f;
        [SerializeField] private float shallowSaturation = 0f;
        [SerializeField] private float deepSaturation = -35f;

        [Header("Volume - Vignette")]
        [SerializeField] private Color vignetteColor = Color.black;
        [SerializeField] private float shallowVignetteIntensity = 0.08f;
        [SerializeField] private float deepVignetteIntensity = 0.34f;
        [SerializeField] private float shallowVignetteSmoothness = 0.20f;
        [SerializeField] private float deepVignetteSmoothness = 0.42f;

        [Header("Volume - Depth Of Field")]
        [SerializeField] private float shallowFocusDistance = 25f;
        [SerializeField] private float deepFocusDistance = 7f;
        [SerializeField] private float shallowAperture = 5.6f;
        [SerializeField] private float deepAperture = 3.2f;
        [SerializeField] private float shallowFocalLength = 50f;
        [SerializeField] private float deepFocalLength = 65f;

        [Header("Debug")]
        [SerializeField] private bool logDepthChanges = false;

        private static readonly int ColorId = Shader.PropertyToID("_color");
        private static readonly int AlphaId = Shader.PropertyToID("_alpha");
        private static readonly int DistanceId = Shader.PropertyToID("_dis");
        private static readonly int RefractionId = Shader.PropertyToID("_refraction");

        private ColorAdjustments colorAdjustments;
        private DepthOfField depthOfField;
        private Vignette vignette;

        private float lastDepth01 = -999f;

        /// <summary>현재 깊이 정규화 값을 반환한다.</summary>
        public float CurrentDepth01 { get; private set; }

        /// <summary>현재 깊이 판정 카메라를 교체하고 즉시 반영한다.</summary>
        public void SetActiveCamera(Transform cameraTransform)
        {
            activeCameraTransform = cameraTransform;
            ApplyDepthEnvironment(forceApply: true);
        }

        /// <summary>볼륨 오버라이드를 캐싱한다.</summary>
        private void Awake()
        {
            CacheVolumeOverrides();
            ApplyDepthEnvironment(forceApply: true);
        }

        /// <summary>매 프레임 활성 카메라 높이에 따라 수중 환경을 갱신한다.</summary>
        private void LateUpdate()
        {
            ApplyDepthEnvironment(forceApply: false);
        }

        /// <summary>필요한 볼륨 오버라이드를 캐싱한다.</summary>
        private void CacheVolumeOverrides()
        {
            if (globalVolume == null || globalVolume.profile == null)
                return;

            globalVolume.profile.TryGet(out colorAdjustments);
            globalVolume.profile.TryGet(out depthOfField);
            globalVolume.profile.TryGet(out vignette);
        }

        /// <summary>현재 활성 카메라 깊이에 따라 머티리얼과 볼륨 값을 반영한다.</summary>
        private void ApplyDepthEnvironment(bool forceApply)
        {
            if (activeCameraTransform == null)
                return;

            float cameraY = activeCameraTransform.position.y;

            // surfaceY에서 0, maxDepthY에서 1이 되도록 정규화
            float rawDepth01 = Mathf.InverseLerp(surfaceY, maxDepthY, cameraY);
            rawDepth01 = Mathf.Clamp01(rawDepth01);

            float depth01 = depthCurve != null ? depthCurve.Evaluate(rawDepth01) : rawDepth01;
            CurrentDepth01 = depth01;

            if (!forceApply && Mathf.Abs(depth01 - lastDepth01) < 0.0025f)
                return;

            lastDepth01 = depth01;

            ApplyUnderwaterMaterial(depth01);
            ApplyVolumeOverrides(depth01);

            if (logDepthChanges)
                Debug.Log($"[DepthEnvironmentController] camera={activeCameraTransform.name}, y={cameraY:F2}, depth01={depth01:F3}");
        }

        /// <summary>수중 풀스크린 머티리얼 값을 깊이에 따라 조정한다.</summary>
        private void ApplyUnderwaterMaterial(float depth01)
        {
            if (underwaterMaterial == null)
                return;

            underwaterMaterial.SetColor(ColorId, Color.Lerp(shallowWaterColor, deepWaterColor, depth01));
            underwaterMaterial.SetFloat(AlphaId, Mathf.Lerp(shallowAlpha, deepAlpha, depth01));
            underwaterMaterial.SetFloat(DistanceId, Mathf.Lerp(shallowDistance, deepDistance, depth01));
            underwaterMaterial.SetFloat(RefractionId, Mathf.Lerp(shallowRefraction, deepRefraction, depth01));
        }

        /// <summary>볼륨 오버라이드 값을 깊이에 따라 조정한다.</summary>
        private void ApplyVolumeOverrides(float depth01)
        {
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.overrideState = true;
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.contrast.overrideState = true;
                colorAdjustments.saturation.overrideState = true;

                colorAdjustments.colorFilter.value = Color.Lerp(shallowColorFilter, deepColorFilter, depth01);
                colorAdjustments.postExposure.value = Mathf.Lerp(shallowPostExposure, deepPostExposure, depth01);
                colorAdjustments.contrast.value = Mathf.Lerp(shallowContrast, deepContrast, depth01);
                colorAdjustments.saturation.value = Mathf.Lerp(shallowSaturation, deepSaturation, depth01);
            }

            if (vignette != null)
            {
                vignette.color.overrideState = true;
                vignette.intensity.overrideState = true;
                vignette.smoothness.overrideState = true;

                vignette.color.value = vignetteColor;
                vignette.intensity.value = Mathf.Lerp(shallowVignetteIntensity, deepVignetteIntensity, depth01);
                vignette.smoothness.value = Mathf.Lerp(shallowVignetteSmoothness, deepVignetteSmoothness, depth01);
            }

            if (depthOfField != null)
            {
                depthOfField.mode.overrideState = true;
                depthOfField.focusDistance.overrideState = true;
                depthOfField.aperture.overrideState = true;
                depthOfField.focalLength.overrideState = true;

                depthOfField.mode.value = DepthOfFieldMode.Bokeh;
                depthOfField.focusDistance.value = Mathf.Lerp(shallowFocusDistance, deepFocusDistance, depth01);
                depthOfField.aperture.value = Mathf.Lerp(shallowAperture, deepAperture, depth01);
                depthOfField.focalLength.value = Mathf.Lerp(shallowFocalLength, deepFocalLength, depth01);
            }
        }
    }
}
