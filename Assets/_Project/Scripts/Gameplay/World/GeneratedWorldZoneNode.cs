using System.Collections.Generic;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// GeneratedWorldRoot/ZoneRoots/ZoneRoot_XX에 붙는 lightweight runtime metadata component.
    /// 기존 ZoneRoot 컴포넌트와 별개로 동작하며, ZoneDataSO 참조 없이 안전하게 동작한다.
    /// </summary>
    public class GeneratedWorldZoneNode : MonoBehaviour
    {
        [Header("Zone Identity")]
        [SerializeField] private ZoneId zoneId;
        [SerializeField] private RegionId regionId;
        [SerializeField] private ZoneBiomeType biomeType;
        [SerializeField] private ZoneDepthBand primaryDepthBand;

        [Header("Depth Configuration")]
        [SerializeField] private float waterLevelY;
        [SerializeField] private float minDepth;
        [SerializeField] private float maxDepth;
        [SerializeField] private float baseRiskLevel;

        [Header("Spatial Configuration")]
        [SerializeField] private Vector3 zoneCenter;
        [SerializeField] private Vector3 zoneSize;
        [SerializeField] private Bounds horizontalBounds;

        [Header("Environment")]
        [SerializeField] private ZoneEnvironmentProfileSO environmentProfile;

        [Header("Depth Slices")]
        [SerializeField] private List<ZoneDepthSlice> depthSlices = new List<ZoneDepthSlice>();

        [Header("Child Root References")]
        [SerializeField] private Transform geometryRoot;
        [SerializeField] private Transform propsRoot;
        [SerializeField] private Transform harvestSpawnsRoot;
        [SerializeField] private Transform logSpawnsRoot;
        [SerializeField] private Transform hazardsRoot;
        [SerializeField] private Transform triggersRoot;
        [SerializeField] private Transform debugGizmosRoot;
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private Transform underwaterAreaRoot;
        [SerializeField] private Transform seafloorRoot;
        [SerializeField] private Transform environmentPropsRoot;
        [SerializeField] private Transform dynamicEffectsRoot;
        [SerializeField] private Transform particlesRoot;

        // ===== Public Getters =====

        /// <summary>Zone ID (예: A1, F6, J10)</summary>
        public ZoneId ZoneId => zoneId;

        /// <summary>Region ID</summary>
        public RegionId RegionId => regionId;

        /// <summary>바이옴 타입</summary>
        public ZoneBiomeType BiomeType => biomeType;

        /// <summary>주 수심 대역</summary>
        public ZoneDepthBand PrimaryDepthBand => primaryDepthBand;

        /// <summary>수면 Y 좌표</summary>
        public float WaterLevelY => waterLevelY;

        /// <summary>최소 깊이 (위쪽 경계)</summary>
        public float MinDepth => minDepth;

        /// <summary>최대 깊이 (아래쪽 경계)</summary>
        public float MaxDepth => maxDepth;

        /// <summary>기본 위험도 (0~1)</summary>
        public float BaseRiskLevel => baseRiskLevel;

        /// <summary>Zone 중심 월드 좌표</summary>
        public Vector3 ZoneCenter => zoneCenter;

        /// <summary>Zone 크기 (X, Y, Z)</summary>
        public Vector3 ZoneSize => zoneSize;

        /// <summary>Zone XZ 평면 Bounds</summary>
        public Bounds HorizontalBounds => horizontalBounds;

        /// <summary>환경 프로필 참조</summary>
        public ZoneEnvironmentProfileSO EnvironmentProfile => environmentProfile;

        /// <summary>수심 구간 목록 (Surface ~ Forbidden)</summary>
        public IReadOnlyList<ZoneDepthSlice> DepthSlices => depthSlices.AsReadOnly();

        // ===== Child Root Getters =====

        /// <summary>Geometry 하위 루트</summary>
        public Transform GeometryRoot => geometryRoot;

        /// <summary>Props 하위 루트</summary>
        public Transform PropsRoot => propsRoot;

        /// <summary>Harvest Spawns 하위 루트</summary>
        public Transform HarvestSpawnsRoot => harvestSpawnsRoot;

        /// <summary>Log Spawns 하위 루트</summary>
        public Transform LogSpawnsRoot => logSpawnsRoot;

        /// <summary>Hazards 하위 루트</summary>
        public Transform HazardsRoot => hazardsRoot;

        /// <summary>Triggers 하위 루트</summary>
        public Transform TriggersRoot => triggersRoot;

        /// <summary>Debug Gizmos 하위 루트</summary>
        public Transform DebugGizmosRoot => debugGizmosRoot;

        /// <summary>Environment 하위 루트</summary>
        public Transform EnvironmentRoot => environmentRoot;

        /// <summary>UnderwaterArea 하위 루트</summary>
        public Transform UnderwaterAreaRoot => underwaterAreaRoot;

        /// <summary>Seafloor 하위 루트</summary>
        public Transform SeafloorRoot => seafloorRoot;

        /// <summary>Environment Props 하위 루트</summary>
        public Transform EnvironmentPropsRoot => environmentPropsRoot;

        /// <summary>Dynamic Effects 하위 루트</summary>
        public Transform DynamicEffectsRoot => dynamicEffectsRoot;

        /// <summary>Particles 하위 루트</summary>
        public Transform ParticlesRoot => particlesRoot;

        // ===== Public Methods =====

        /// <summary>
        /// ZoneNode를 설정한다. Editor Builder에서 호출 가능.
        /// </summary>
        public void Configure(
            ZoneId zoneId,
            RegionId regionId,
            ZoneBiomeType biomeType,
            ZoneDepthBand primaryDepthBand,
            float waterLevelY,
            float minDepth,
            float maxDepth,
            float baseRiskLevel,
            Vector3 zoneCenter,
            Vector3 zoneSize,
            Bounds horizontalBounds,
            ZoneEnvironmentProfileSO environmentProfile,
            List<ZoneDepthSlice> depthSlices,
            Transform geometryRoot,
            Transform propsRoot,
            Transform harvestSpawnsRoot,
            Transform logSpawnsRoot,
            Transform hazardsRoot,
            Transform triggersRoot,
            Transform debugGizmosRoot,
            Transform environmentRoot,
            Transform underwaterAreaRoot,
            Transform seafloorRoot,
            Transform environmentPropsRoot,
            Transform dynamicEffectsRoot,
            Transform particlesRoot)
        {
            this.zoneId = zoneId;
            this.regionId = regionId;
            this.biomeType = biomeType;
            this.primaryDepthBand = primaryDepthBand;
            this.waterLevelY = waterLevelY;
            this.minDepth = minDepth;
            this.maxDepth = maxDepth;
            this.baseRiskLevel = baseRiskLevel;
            this.zoneCenter = zoneCenter;
            this.zoneSize = zoneSize;
            this.horizontalBounds = horizontalBounds;
            this.environmentProfile = environmentProfile;
            this.depthSlices = depthSlices ?? new List<ZoneDepthSlice>();
            this.geometryRoot = geometryRoot;
            this.propsRoot = propsRoot;
            this.harvestSpawnsRoot = harvestSpawnsRoot;
            this.logSpawnsRoot = logSpawnsRoot;
            this.hazardsRoot = hazardsRoot;
            this.triggersRoot = triggersRoot;
            this.debugGizmosRoot = debugGizmosRoot;
            this.environmentRoot = environmentRoot;
            this.underwaterAreaRoot = underwaterAreaRoot;
            this.seafloorRoot = seafloorRoot;
            this.environmentPropsRoot = environmentPropsRoot;
            this.dynamicEffectsRoot = dynamicEffectsRoot;
            this.particlesRoot = particlesRoot;
        }

        /// <summary>
        /// 주어진 월드 좌표가 이 Zone의 XZ 평면 Bounds 내에 있는지 확인한다.
        /// epsilon을 사용하여 경계선 근처에서도 true를 반환할 수 있다.
        /// </summary>
        public bool ContainsHorizontal(Vector3 worldPosition, float epsilon = 0f)
        {
            Bounds expanded = horizontalBounds;
            expanded.Expand(new Vector3(epsilon, 0f, epsilon));
            Vector3 xzOnly = new Vector3(worldPosition.x, 0f, worldPosition.z);
            return expanded.Contains(xzOnly);
        }

        /// <summary>
        /// Y값을 기준으로 Depth Band를 판정한다.
        /// </summary>
        public ZoneDepthBand ResolveDepthBand(float y)
        {
            // 수면 위면 Surface
            if (y > waterLevelY)
                return ZoneDepthBand.Surface;

            // maxDepth보다 아래면 Forbidden
            if (y < maxDepth)
                return ZoneDepthBand.Forbidden;

            // DepthSlices를 순회하며 포함되는 slice 찾기
            if (depthSlices != null)
            {
                for (int i = 0; i < depthSlices.Count; i++)
                {
                    if (depthSlices[i].ContainsY(y))
                        return depthSlices[i].DepthBand;
                }
            }

            // fallback: Mid
            return ZoneDepthBand.Mid;
        }

        /// <summary>
        /// Y값을 기준으로 normalized depth (0~1)를 계산한다.
        /// 0 = waterLevelY, 1 = maxDepth
        /// </summary>
        public float CalculateNormalizedDepth01(float y)
        {
            // waterLevel과 maxDepth가 같으면 0 반환
            if (Mathf.Approximately(waterLevelY, maxDepth))
                return 0f;

            // y를 waterLevel~maxDepth 범위로 clamp
            float clampedY = Mathf.Clamp(y, maxDepth, waterLevelY);

            // InverseLerp: waterLevel=0, maxDepth=1
            return Mathf.InverseLerp(waterLevelY, maxDepth, clampedY);
        }

        /// <summary>
        /// 특정 Depth Band에 해당하는 Depth Slice를 조회한다.
        /// </summary>
        public bool TryGetDepthSlice(ZoneDepthBand band, out ZoneDepthSlice slice)
        {
            if (depthSlices != null)
            {
                for (int i = 0; i < depthSlices.Count; i++)
                {
                    if (depthSlices[i].DepthBand == band)
                    {
                        slice = depthSlices[i];
                        return true;
                    }
                }
            }

            slice = default;
            return false;
        }
    }
}
