using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// 존의 환경(Environment) 프로필 ScriptableObject.
    /// 수심 대역, 바이옴 타입, 해저 머티리얼, 프롭/위험 밀도, 수중 템플릿 등을 정의한다.
    ///
    /// ambientProfile(조명/안개/BGM/위험 UI)과는 별개의 개념이다.
    /// environmentProfile = 수심/바이옴/해저/프롭/수중 템플릿
    /// ambientProfile = 조명/안개/BGM/위험 UI
    /// </summary>
    [CreateAssetMenu(
        fileName = "ZoneEnvProfile_",
        menuName = "DeepLight/World/Zone Environment Profile")]
    public class ZoneEnvironmentProfileSO : ScriptableObject
    {
        [Header("Depth & Biome")]
        [SerializeField] private ZoneDepthBand depthBand;
        [SerializeField] private ZoneBiomeType biomeType;

        [Header("Depth Bounds (World Y)")]
        [SerializeField] private float waterLevelY = 0f; // 수면 Y 좌표
        [SerializeField] private float minDepth; // 최소 깊이 (예: -50, 위쪽 경계)
        [SerializeField] private float maxDepth; // 최대 깊이 (예: -450, 아래쪽 경계)

        [Header("Seafloor")]
        [SerializeField] private Material seafloorMaterial; // 해저 머티리얼

        [Header("Debug")]
        [SerializeField] private Color debugColor = Color.gray; // 디버그 시각화 색상

        [Header("Prop & Hazard Density (0-1)")]
        [SerializeField] private float basePropDensity = 0.3f; // 기본 프롭 밀도
        [SerializeField] private float rockDensity = 0.2f; // 암석 밀도
        [SerializeField] private float vegetationDensity = 0.15f; // 식생 밀도
        [SerializeField] private float wreckDensity = 0.1f; // 난파선 밀도
        [SerializeField] private float ruinDensity = 0.05f; // 유적 밀도
        [SerializeField] private float hazardDensity = 0.1f; // 위험 요소 밀도

        [Header("Template Prefabs")]
        [SerializeField] private GameObject underwaterAreaTemplatePrefab; // 수중 영역 템플릿
        [SerializeField] private GameObject dynamicEffectTemplatePrefab; // 동적 이펙트 템플릿
        [SerializeField] private GameObject particleTemplatePrefab; // 파티클 템플릿

        // ===== Public Accessors =====

        /// <summary>수심 대역</summary>
        public ZoneDepthBand DepthBand => depthBand;

        /// <summary>바이옴 타입</summary>
        public ZoneBiomeType BiomeType => biomeType;

        /// <summary>수면 Y 좌표</summary>
        public float WaterLevelY => waterLevelY;

        /// <summary>최소 깊이 (위쪽 경계, 예: -50)</summary>
        public float MinDepth => minDepth;

        /// <summary>최대 깊이 (아래쪽 경계, 예: -450)</summary>
        public float MaxDepth => maxDepth;

        /// <summary>해저 머티리얼</summary>
        public Material SeafloorMaterial => seafloorMaterial;

        /// <summary>디버그 시각화 색상</summary>
        public Color DebugColor => debugColor;

        /// <summary>기본 프롭 밀도</summary>
        public float BasePropDensity => basePropDensity;

        /// <summary>암석 밀도</summary>
        public float RockDensity => rockDensity;

        /// <summary>식생 밀도</summary>
        public float VegetationDensity => vegetationDensity;

        /// <summary>난파선 밀도</summary>
        public float WreckDensity => wreckDensity;

        /// <summary>유적 밀도</summary>
        public float RuinDensity => ruinDensity;

        /// <summary>위험 요소 밀도</summary>
        public float HazardDensity => hazardDensity;

        /// <summary>수중 영역 템플릿 프리팹</summary>
        public GameObject UnderwaterAreaTemplatePrefab => underwaterAreaTemplatePrefab;

        /// <summary>동적 이펙트 템플릿 프리팹</summary>
        public GameObject DynamicEffectTemplatePrefab => dynamicEffectTemplatePrefab;

        /// <summary>파티클 템플릿 프리팹</summary>
        public GameObject ParticleTemplatePrefab => particleTemplatePrefab;

        /// <summary>
        /// 에디터에서 값 검증 및 보정.
        /// minDepth/maxDepth가 음수 깊이값 규칙을 깨지 않도록 보정한다.
        /// 깊이값은 실제 월드 Y 기준으로 음수값을 허용한다.
        /// minDepth가 maxDepth보다 더 위쪽 값이다. 즉 -50 > -450 이 정상이다.
        /// </summary>
        private void OnValidate()
        {
            // minDepth가 maxDepth보다 아래쪽에 있으면(값이 더 작으면) 교체
            // 예: minDepth = -450, maxDepth = -50 → minDepth가 더 아래쪽이므로 swap
            if (minDepth < maxDepth)
            {
                // minDepth가 maxDepth보다 더 작은 값(더 아래쪽)이면 swap
                float temp = minDepth;
                minDepth = maxDepth;
                maxDepth = temp;
                Debug.LogWarning($"[ZoneEnvironmentProfileSO] {name}: minDepth/maxDepth swapped. minDepth should be above (greater value) maxDepth. Corrected: minDepth={minDepth}, maxDepth={maxDepth}");
            }

            // 모든 밀도값 0~1 클램프
            basePropDensity = Mathf.Clamp01(basePropDensity);
            rockDensity = Mathf.Clamp01(rockDensity);
            vegetationDensity = Mathf.Clamp01(vegetationDensity);
            wreckDensity = Mathf.Clamp01(wreckDensity);
            ruinDensity = Mathf.Clamp01(ruinDensity);
            hazardDensity = Mathf.Clamp01(hazardDensity);
        }
    }
}
