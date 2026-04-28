using System;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// 특정 Zone + DepthBand에서 사용해야 하는 시각 상태를 담는 Serializable struct.
    /// 실제 렌더러를 직접 수정하지 않고, Runtime Controller와 Adapter가 참조할 데이터다.
    /// </summary>
    [Serializable]
    public struct WorldMapVisualProfile
    {
        [Header("Color")]
        [SerializeField] private Color waterTint; // 수중 색조
        [SerializeField] private Color fogColor; // 안개 색상
        [SerializeField] private Color ambientColor; // 앰비언트 색상

        [Header("Fog & Visibility")]
        [SerializeField] private float fogDensity; // 안개 밀도 (0~1)
        [SerializeField] private float visibilityDistance; // 가시 거리 (meters)

        [Header("Post-Processing")]
        [SerializeField] private float exposure; // 노출 (0~2)
        [SerializeField] private float saturation; // 채도 (0~2)
        [SerializeField] private float contrast; // 대비 (0~2)
        [SerializeField] private float vignetteIntensity; // 비네트 강도 (0~1)

        [Header("Water Effects")]
        [SerializeField] private float causticsIntensity; // 코스틱 강도 (0~1)
        [SerializeField] private float lightShaftIntensity; // 라이트 샤프트 강도 (0~1)

        [Header("Particles")]
        [SerializeField] private float particleDensityMultiplier; // 파티클 밀도 배율 (0~2)

        [Header("Metadata")]
        [SerializeField] private float normalizedDepth01; // normalized 깊이 (0~1)
        [SerializeField] private ZoneDepthBand depthBand; // 수심 대역
        [SerializeField] private ZoneBiomeType biomeType; // 바이옴 타입

        // ===== Public Getters =====

        /// <summary>수중 색조</summary>
        public Color WaterTint => waterTint;
        /// <summary>안개 색상</summary>
        public Color FogColor => fogColor;
        /// <summary>앰비언트 색상</summary>
        public Color AmbientColor => ambientColor;
        /// <summary>안개 밀도 (0~1)</summary>
        public float FogDensity => fogDensity;
        /// <summary>가시 거리 (meters)</summary>
        public float VisibilityDistance => visibilityDistance;
        /// <summary>노출 (0~2)</summary>
        public float Exposure => exposure;
        /// <summary>채도 (0~2)</summary>
        public float Saturation => saturation;
        /// <summary>대비 (0~2)</summary>
        public float Contrast => contrast;
        /// <summary>비네트 강도 (0~1)</summary>
        public float VignetteIntensity => vignetteIntensity;
        /// <summary>코스틱 강도 (0~1)</summary>
        public float CausticsIntensity => causticsIntensity;
        /// <summary>라이트 샤프트 강도 (0~1)</summary>
        public float LightShaftIntensity => lightShaftIntensity;
        /// <summary>파티클 밀도 배율 (0~2)</summary>
        public float ParticleDensityMultiplier => particleDensityMultiplier;
        /// <summary>normalized 깊이 (0~1)</summary>
        public float NormalizedDepth01 => normalizedDepth01;
        /// <summary>수심 대역</summary>
        public ZoneDepthBand DepthBand => depthBand;
        /// <summary>바이옴 타입</summary>
        public ZoneBiomeType BiomeType => biomeType;

        // ===== Public Constructor =====

        /// <summary>
        /// 모든 필드를 초기화하는 생성자.
        /// </summary>
        public WorldMapVisualProfile(
            Color waterTint, Color fogColor, Color ambientColor,
            float fogDensity, float visibilityDistance,
            float exposure, float saturation, float contrast, float vignetteIntensity,
            float causticsIntensity, float lightShaftIntensity, float particleDensityMultiplier,
            float normalizedDepth01, ZoneDepthBand depthBand, ZoneBiomeType biomeType)
        {
            this.waterTint = waterTint;
            this.fogColor = fogColor;
            this.ambientColor = ambientColor;
            this.fogDensity = fogDensity;
            this.visibilityDistance = visibilityDistance;
            this.exposure = exposure;
            this.saturation = saturation;
            this.contrast = contrast;
            this.vignetteIntensity = vignetteIntensity;
            this.causticsIntensity = causticsIntensity;
            this.lightShaftIntensity = lightShaftIntensity;
            this.particleDensityMultiplier = particleDensityMultiplier;
            this.normalizedDepth01 = normalizedDepth01;
            this.depthBand = depthBand;
            this.biomeType = biomeType;
        }

        // ===== Public Methods =====

        /// <summary>
        /// 두 VisualProfile을 t(0~1)로 Lerp한다.
        /// </summary>
        public static WorldMapVisualProfile Lerp(WorldMapVisualProfile a, WorldMapVisualProfile b, float t)
        {
            t = Mathf.Clamp01(t);
            return new WorldMapVisualProfile(
                Color.Lerp(a.waterTint, b.waterTint, t),
                Color.Lerp(a.fogColor, b.fogColor, t),
                Color.Lerp(a.ambientColor, b.ambientColor, t),
                Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                Mathf.Lerp(a.visibilityDistance, b.visibilityDistance, t),
                Mathf.Lerp(a.exposure, b.exposure, t),
                Mathf.Lerp(a.saturation, b.saturation, t),
                Mathf.Lerp(a.contrast, b.contrast, t),
                Mathf.Lerp(a.vignetteIntensity, b.vignetteIntensity, t),
                Mathf.Lerp(a.causticsIntensity, b.causticsIntensity, t),
                Mathf.Lerp(a.lightShaftIntensity, b.lightShaftIntensity, t),
                Mathf.Lerp(a.particleDensityMultiplier, b.particleDensityMultiplier, t),
                Mathf.Lerp(a.normalizedDepth01, b.normalizedDepth01, t),
                t < 0.5f ? a.depthBand : b.depthBand,
                t < 0.5f ? a.biomeType : b.biomeType
            );
        }

        /// <summary>
        /// 모든 값을 안전 범위로 Clamp한다.
        /// </summary>
        public void ClampValues()
        {
            fogDensity = Mathf.Clamp(fogDensity, 0f, 1f);
            visibilityDistance = Mathf.Max(0f, visibilityDistance);
            exposure = Mathf.Clamp(exposure, 0f, 2f);
            saturation = Mathf.Clamp(saturation, 0f, 2f);
            contrast = Mathf.Clamp(contrast, 0f, 2f);
            vignetteIntensity = Mathf.Clamp01(vignetteIntensity);
            causticsIntensity = Mathf.Clamp01(causticsIntensity);
            lightShaftIntensity = Mathf.Clamp01(lightShaftIntensity);
            particleDensityMultiplier = Mathf.Clamp(particleDensityMultiplier, 0f, 2f);
            normalizedDepth01 = Mathf.Clamp01(normalizedDepth01);
        }

        /// <summary>
        /// 디버그 문자열을 반환한다.
        /// </summary>
        public string ToDebugString()
        {
            return $"DepthBand={depthBand}, Biome={biomeType}, " +
                   $"FogDensity={fogDensity:F3}, Visibility={visibilityDistance:F1}, " +
                   $"Exposure={exposure:F2}, Saturation={saturation:F2}, " +
                   $"Vignette={vignetteIntensity:F2}, Caustics={causticsIntensity:F2}, " +
                   $"NormalizedDepth={normalizedDepth01:F3}";
        }

        /// <summary>
        /// 기본 Fallback Profile을 생성한다.
        /// </summary>
        public static WorldMapVisualProfile CreateFallback(ZoneDepthBand depthBand, ZoneBiomeType biomeType, float normalizedDepth01)
        {
            WorldMapVisualProfile profile = new WorldMapVisualProfile(
                waterTint: Color.cyan,
                fogColor: new Color(0.1f, 0.2f, 0.3f),
                ambientColor: new Color(0.2f, 0.3f, 0.4f),
                fogDensity: 0.05f,
                visibilityDistance: 100f,
                exposure: 1f,
                saturation: 1f,
                contrast: 1f,
                vignetteIntensity: 0f,
                causticsIntensity: 0.5f,
                lightShaftIntensity: 0.5f,
                particleDensityMultiplier: 1f,
                normalizedDepth01: normalizedDepth01,
                depthBand: depthBand,
                biomeType: biomeType
            );
            profile.ClampValues();
            return profile;
        }
    }
}
