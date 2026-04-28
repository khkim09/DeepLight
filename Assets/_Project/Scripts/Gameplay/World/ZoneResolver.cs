using System.Collections.Generic;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 월드 좌표와 존 식별자 간 변환을 담당하는 구현체.
    /// Phase 6 확장: 좌표 기반 Zone 판정, 경계선 처리, Depth Band 세분화, Snapshot 캐싱.
    ///
    /// 우선순위 정책:
    /// 1순위: 좌표 기반 계산 (ZoneResolver)
    /// 2순위: Trigger는 진입/디버그 보조
    /// 3순위: 경계선에서는 현재 Zone 유지 또는 가장 가까운 Zone center 선택
    /// </summary>
    public class ZoneResolver : IZoneResolver
    {
        /// <summary>경계선 판정에 사용하는 기본 epsilon 값</summary>
        public const float DefaultBoundaryEpsilon = 0.05f;

        /// <summary>수면 위 Surface 영역 높이 (waterLevelY 기준 위쪽)</summary>
        public const float SurfaceHeightAboveWater = 20f;

        private readonly WorldMapConfigSO _config;
        private WorldMapScenarioPresetSO _scenarioPreset;
        private Dictionary<ZoneId, ZoneRuntimeSnapshot> _snapshotByZone;

        /// <summary>월드맵 설정 참조</summary>
        public WorldMapConfigSO Config => _config;

        /// <summary>시나리오 프리셋 참조 (null 가능)</summary>
        public WorldMapScenarioPresetSO ScenarioPreset => _scenarioPreset;

        /// <summary>Zone별 Snapshot Dictionary (읽기 전용)</summary>
        public IReadOnlyDictionary<ZoneId, ZoneRuntimeSnapshot> SnapshotByZone =>
            _snapshotByZone as IReadOnlyDictionary<ZoneId, ZoneRuntimeSnapshot>;

        /// <summary>ZoneResolver 생성 (config만 받음)</summary>
        public ZoneResolver(WorldMapConfigSO config)
        {
            _config = config ?? throw new System.ArgumentNullException(nameof(config));
            _snapshotByZone = new Dictionary<ZoneId, ZoneRuntimeSnapshot>();
        }

        /// <summary>
        /// ZoneResolver 초기화: ScenarioPreset을 기반으로 모든 Zone의 Snapshot을 미리 계산한다.
        /// Initialize 호출 후 TryResolveSnapshot이 빠르게 동작한다.
        /// </summary>
        public void Initialize(WorldMapScenarioPresetSO scenarioPreset)
        {
            _scenarioPreset = scenarioPreset;
            _snapshotByZone = new Dictionary<ZoneId, ZoneRuntimeSnapshot>();

            if (_config == null) return;

            // 모든 ZoneId에 대해 Snapshot 생성
            foreach (ZoneId zoneId in _config.GetAllZoneIds())
            {
                ZoneRuntimeSnapshot snapshot = BuildSnapshot(zoneId);
                _snapshotByZone[zoneId] = snapshot;
            }
        }

        // ===== IZoneResolver Interface Implementation =====

        /// <summary>월드 좌표로부터 ZoneId 계산 시도 (월드 경계 밖이면 false 반환)</summary>
        public bool TryGetZoneIdFromWorldPosition(Vector3 worldPosition, out ZoneId zoneId)
        {
            if (!IsWorldPositionInBounds(worldPosition))
            {
                zoneId = default;
                return false;
            }

            zoneId = GetClampedZoneIdFromWorldPosition(worldPosition);
            return true;
        }

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 시도 (월드 경계 밖이면 false 반환)</summary>
        public bool TryGetZoneCoordinateFromWorldPosition(Vector3 worldPosition, out ZoneCoordinate coordinate)
        {
            if (!IsWorldPositionInBounds(worldPosition))
            {
                coordinate = default;
                return false;
            }

            coordinate = GetClampedZoneCoordinateFromWorldPosition(worldPosition);
            return true;
        }

        /// <summary>월드 좌표로부터 ZoneId 계산 (월드 경계 밖이면 가장자리 존 반환)</summary>
        public ZoneId GetClampedZoneIdFromWorldPosition(Vector3 worldPosition)
        {
            ZoneCoordinate coordinate = GetClampedZoneCoordinateFromWorldPosition(worldPosition);
            return coordinate.ToZoneId();
        }

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 (월드 경계 밖이면 가장자리 좌표 반환)</summary>
        public ZoneCoordinate GetClampedZoneCoordinateFromWorldPosition(Vector3 worldPosition)
        {
            return ZoneCoordinate.FromWorldPosition(worldPosition, _config);
        }

        /// <summary>월드 좌표로부터 ZoneId 계산 (레거시, 월드 경계 밖이면 예외 발생 가능)</summary>
        public ZoneId GetZoneIdFromWorldPosition(Vector3 worldPosition)
        {
            ZoneCoordinate coordinate = GetZoneCoordinateFromWorldPosition(worldPosition);
            return coordinate.ToZoneId();
        }

        /// <summary>월드 좌표로부터 ZoneCoordinate 계산 (레거시, 월드 경계 밖이면 예외 발생 가능)</summary>
        public ZoneCoordinate GetZoneCoordinateFromWorldPosition(Vector3 worldPosition)
        {
            return ZoneCoordinate.FromWorldPosition(worldPosition, _config);
        }

        /// <summary>ZoneId로부터 존 중심 월드 좌표 계산</summary>
        public Vector3 GetZoneCenterWorldPosition(ZoneId zoneId)
        {
            ZoneCoordinate coordinate = ZoneCoordinate.FromZoneId(zoneId);
            return coordinate.GetZoneCenterWorldPosition(_config);
        }

        /// <summary>ZoneId로부터 존 경계 월드 좌표 계산 (min, max)</summary>
        public (Vector3 min, Vector3 max) GetZoneBounds(ZoneId zoneId)
        {
            ZoneCoordinate coordinate = ZoneCoordinate.FromZoneId(zoneId);
            return coordinate.GetZoneBounds(_config);
        }

        /// <summary>월드 좌표가 월드 경계 내에 있는지 확인</summary>
        public bool IsWorldPositionInBounds(Vector3 worldPosition)
        {
            return _config.IsWorldPositionInBounds(worldPosition);
        }

        /// <summary>ZoneId가 그리드 경계 내에 있는지 확인</summary>
        public bool IsZoneIdInBounds(ZoneId zoneId)
        {
            return _config.IsZoneIdInBounds(zoneId);
        }

        /// <summary>디버그용: 현재 존 정보 문자열 생성</summary>
        public string GetDebugZoneInfo(Vector3 worldPosition)
        {
            if (!IsWorldPositionInBounds(worldPosition))
                return "Out of bounds";

            ZoneId zoneId = GetZoneIdFromWorldPosition(worldPosition);
            Vector3 center = GetZoneCenterWorldPosition(zoneId);
            (Vector3 min, Vector3 max) bounds = GetZoneBounds(zoneId);

            return $"Zone: {zoneId}, Center: {center}, Bounds: [{bounds.min} - {bounds.max}]";
        }

        // ===== Phase 6: Enhanced Zone Resolution =====

        /// <summary>
        /// 월드 좌표로부터 ZoneId를 계산한다.
        /// currentZoneId가 유효하고 경계선 epsilon 내에 있으면 currentZoneId를 유지한다.
        /// 경계선에서 currentZoneId가 없으면 가장 가까운 Zone center를 선택한다.
        /// </summary>
        /// <param name="worldPosition">월드 좌표</param>
        /// <param name="currentZoneId">현재 Zone ID (null 가능)</param>
        /// <param name="zoneId">결과 Zone ID</param>
        /// <returns>유효한 Zone이면 true</returns>
        public bool TryResolveZone(Vector3 worldPosition, ZoneId? currentZoneId, out ZoneId zoneId)
        {
            // 1. 월드 경계 밖이면 false
            if (!IsWorldPositionInBounds(worldPosition))
            {
                zoneId = default;
                return false;
            }

            // 2. currentZoneId가 유효하면 경계선 epsilon 내에서 유지
            if (currentZoneId.HasValue && IsZoneIdInBounds(currentZoneId.Value))
            {
                // currentZone의 Bounds를 가져와서 epsilon 확장 후 포함 여부 확인
                ZoneRuntimeSnapshot snapshot;
                if (_snapshotByZone.TryGetValue(currentZoneId.Value, out snapshot))
                {
                    if (snapshot.ContainsXZ(worldPosition, DefaultBoundaryEpsilon))
                    {
                        zoneId = currentZoneId.Value;
                        return true;
                    }
                }
                else
                {
                    // Snapshot이 없으면 직접 Bounds 계산
                    (Vector3 min, Vector3 max) bounds = GetZoneBounds(currentZoneId.Value);
                    Bounds currentBounds = new Bounds();
                    Vector3 center = new Vector3(
                        (bounds.min.x + bounds.max.x) * 0.5f,
                        0f,
                        (bounds.min.z + bounds.max.z) * 0.5f);
                    Vector3 size = new Vector3(
                        bounds.max.x - bounds.min.x + DefaultBoundaryEpsilon * 2f,
                        2000f,
                        bounds.max.z - bounds.min.z + DefaultBoundaryEpsilon * 2f);
                    currentBounds.center = center;
                    currentBounds.size = size;

                    Vector3 xzPos = new Vector3(worldPosition.x, 0f, worldPosition.z);
                    if (currentBounds.Contains(xzPos))
                    {
                        zoneId = currentZoneId.Value;
                        return true;
                    }
                }
            }

            // 3. 일반 좌표 계산
            ZoneId calculatedZoneId = GetClampedZoneIdFromWorldPosition(worldPosition);

            // 4. 경계선에서 currentZoneId가 없으면 가장 가까운 Zone center 선택
            //    (이미 clamp된 결과이므로 기본적으로 가장 가까운 Zone이 반환됨)
            zoneId = calculatedZoneId;
            return true;
        }

        /// <summary>
        /// 월드 좌표로부터 ZoneId를 계산한다 (currentZoneId 없음).
        /// </summary>
        public bool TryResolveZone(Vector3 worldPosition, out ZoneId zoneId)
        {
            return TryResolveZone(worldPosition, null, out zoneId);
        }

        /// <summary>
        /// ZoneId에 대한 Snapshot을 조회한다.
        /// Initialize()가 호출된 상태여야 한다.
        /// </summary>
        public bool TryResolveSnapshot(ZoneId zoneId, out ZoneRuntimeSnapshot snapshot)
        {
            return _snapshotByZone.TryGetValue(zoneId, out snapshot);
        }

        /// <summary>
        /// 월드 좌표로부터 ZoneDepthSample을 계산한다.
        /// 현재 Zone + 현재 수심 band 정보를 한 번에 반환한다.
        /// </summary>
        public bool TrySampleDepth(Vector3 worldPosition, out ZoneDepthSample sample)
        {
            return TrySampleDepth(worldPosition, null, out sample);
        }

        /// <summary>
        /// 월드 좌표로부터 ZoneDepthSample을 계산한다 (currentZoneId 지원).
        /// </summary>
        public bool TrySampleDepth(Vector3 worldPosition, ZoneId? currentZoneId, out ZoneDepthSample sample)
        {
            sample = default;

            // 1. Zone 판정
            if (!TryResolveZone(worldPosition, currentZoneId, out ZoneId zoneId))
            {
                return false;
            }

            // 2. Snapshot 조회
            if (!TryResolveSnapshot(zoneId, out ZoneRuntimeSnapshot snapshot))
            {
                // Snapshot이 없으면 fallback으로 기본 정보 생성
                sample = CreateFallbackSample(worldPosition, zoneId);
                return true;
            }

            // 3. Depth Band 판정
            ZoneDepthBand depthBand = ResolveDepthBand(snapshot, worldPosition.y);

            // 4. Normalized Depth 계산
            float normalizedDepth = CalculateNormalizedDepth01(snapshot, worldPosition.y);

            // 5. Sample 생성
            sample = new ZoneDepthSample(
                zoneId: zoneId,
                regionId: snapshot.RegionId,
                biomeType: snapshot.BiomeType,
                depthBand: depthBand,
                y: worldPosition.y,
                normalizedDepth01: normalizedDepth,
                waterLevelY: snapshot.WaterLevelY,
                maxDepth: snapshot.MaxDepth,
                sourceRuleName: GetSourceRuleName(zoneId),
                environmentProfile: snapshot.EnvironmentProfile
            );

            return true;
        }

        /// <summary>
        /// ZoneId의 중심 좌표를 반환한다.
        /// </summary>
        public Vector3 GetZoneCenter(ZoneId zoneId)
        {
            return GetZoneCenterWorldPosition(zoneId);
        }

        /// <summary>
        /// ZoneId의 XZ 평면 Bounds를 반환한다.
        /// </summary>
        public Bounds GetZoneHorizontalBounds(ZoneId zoneId)
        {
            (Vector3 min, Vector3 max) bounds = GetZoneBounds(zoneId);
            Vector3 center = new Vector3(
                (bounds.min.x + bounds.max.x) * 0.5f,
                0f,
                (bounds.min.z + bounds.max.z) * 0.5f);
            Vector3 size = new Vector3(
                bounds.max.x - bounds.min.x,
                2000f,
                bounds.max.z - bounds.min.z);
            return new Bounds(center, size);
        }

        /// <summary>
        /// Snapshot과 Y값을 기반으로 Depth Band를 판정한다.
        /// </summary>
        public ZoneDepthBand ResolveDepthBand(ZoneRuntimeSnapshot snapshot, float y)
        {
            // 수면 위면 Surface
            if (y > snapshot.WaterLevelY)
                return ZoneDepthBand.Surface;

            // maxDepth보다 아래면 Forbidden
            if (y < snapshot.MaxDepth)
                return ZoneDepthBand.Forbidden;

            // DepthSlices를 순회하며 포함되는 slice 찾기
            if (snapshot.DepthSlices != null)
            {
                for (int i = 0; i < snapshot.DepthSlices.Count; i++)
                {
                    if (snapshot.DepthSlices[i].ContainsY(y))
                        return snapshot.DepthSlices[i].DepthBand;
                }
            }

            // fallback: Mid
            return ZoneDepthBand.Mid;
        }

        /// <summary>
        /// Y값을 기준으로 normalized depth (0~1)를 계산한다.
        /// 0 = waterLevelY, 1 = maxDepth
        /// </summary>
        public float CalculateNormalizedDepth01(ZoneRuntimeSnapshot snapshot, float y)
        {
            float waterLevel = snapshot.WaterLevelY;
            float maxDepth = snapshot.MaxDepth;

            // waterLevel과 maxDepth가 같으면 0 반환
            if (Mathf.Approximately(waterLevel, maxDepth))
                return 0f;

            // y를 waterLevel~maxDepth 범위로 clamp
            float clampedY = Mathf.Clamp(y, maxDepth, waterLevel);

            // InverseLerp: waterLevel=0, maxDepth=1
            return Mathf.InverseLerp(waterLevel, maxDepth, clampedY);
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// 특정 ZoneId에 대한 Snapshot을 빌드한다.
        /// ScenarioPreset의 Rule을 조회하여 정보를 채운다.
        /// </summary>
        private ZoneRuntimeSnapshot BuildSnapshot(ZoneId zoneId)
        {
            // Zone 중심 좌표
            Vector3 center = GetZoneCenterWorldPosition(zoneId);

            // Zone XZ Bounds
            Bounds horizontalBounds = GetZoneHorizontalBounds(zoneId);

            // ScenarioPreset에서 Rule 조회
            ZoneEnvironmentProfileSO envProfile = null;
            RegionId regionId = new RegionId("Default");
            ZoneBiomeType biomeType = ZoneBiomeType.OpenWater;
            float waterLevelY = 0f;
            float minDepth = -100f;
            float maxDepth = -600f;

            if (_scenarioPreset != null)
            {
                // EnvironmentProfile 조회
                envProfile = _scenarioPreset.GetEnvironmentProfileOrDefault(zoneId);

                // RegionId 조회
                if (_scenarioPreset.TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
                {
                    regionId = rule.RegionId;
                    biomeType = rule.BiomeType;
                    minDepth = rule.MinDepth;
                    maxDepth = rule.MaxDepth;

                    // Rule의 EnvironmentProfile이 있으면 우선 사용
                    if (rule.EnvironmentProfile != null)
                    {
                        envProfile = rule.EnvironmentProfile;
                    }
                }
                else
                {
                    regionId = _scenarioPreset.DefaultRegionId;
                    biomeType = _scenarioPreset.GetBiomeOrDefault(zoneId);
                }
            }

            // EnvironmentProfile에서 depth 정보 보강
            if (envProfile != null)
            {
                waterLevelY = envProfile.WaterLevelY;
                // minDepth/maxDepth는 Rule 우선, 없으면 Profile 값 사용
                if (!_scenarioPreset?.TryGetRuleForZone(zoneId, out _) ?? true)
                {
                    minDepth = envProfile.MinDepth;
                    maxDepth = envProfile.MaxDepth;
                }
            }

            // Depth Slices 생성
            List<ZoneDepthSlice> depthSlices = BuildDepthSlices(waterLevelY, maxDepth);

            return new ZoneRuntimeSnapshot(
                zoneId: zoneId,
                center: center,
                horizontalBounds: horizontalBounds,
                waterLevelY: waterLevelY,
                minDepth: minDepth,
                maxDepth: maxDepth,
                regionId: regionId,
                biomeType: biomeType,
                environmentProfile: envProfile,
                depthSlices: depthSlices
            );
        }

        /// <summary>
        /// waterLevelY와 maxDepth를 기준으로 Depth Slice 목록을 생성한다.
        /// maxDepth가 얕으면 불필요한 slice는 만들지 않는다.
        /// </summary>
        private List<ZoneDepthSlice> BuildDepthSlices(float waterLevelY, float maxDepth)
        {
            var slices = new List<ZoneDepthSlice>();

            // Surface: waterLevelY + 20 ~ waterLevelY
            float surfaceTop = waterLevelY + SurfaceHeightAboveWater;
            float surfaceBottom = waterLevelY;
            slices.Add(new ZoneDepthSlice(
                ZoneDepthBand.Surface,
                surfaceTop, surfaceBottom,
                0f, 0f,
                new Color(0.5f, 0.8f, 1f, 0.5f) // 하늘색
            ));

            // maxDepth가 waterLevelY보다 위면 여기서 종료
            if (maxDepth >= waterLevelY)
                return slices;

            // Shallow: waterLevelY ~ max(waterLevelY - 200, maxDepth)
            float shallowBottom = Mathf.Max(waterLevelY - 200f, maxDepth);
            if (shallowBottom < waterLevelY)
            {
                float shallowNormStart = CalculateNormalized(waterLevelY, maxDepth, waterLevelY);
                float shallowNormEnd = CalculateNormalized(waterLevelY, maxDepth, shallowBottom);
                slices.Add(new ZoneDepthSlice(
                    ZoneDepthBand.Shallow,
                    waterLevelY, shallowBottom,
                    shallowNormStart, shallowNormEnd,
                    new Color(0.3f, 0.7f, 0.3f, 0.5f) // 연두색
                ));
            }

            // maxDepth가 Shallow 범위 내에 있으면 여기서 종료
            if (maxDepth >= waterLevelY - 200f)
                return slices;

            // Mid: -200 ~ max(-600, maxDepth)
            float midTop = waterLevelY - 200f;
            float midBottom = Mathf.Max(waterLevelY - 600f, maxDepth);
            if (midBottom < midTop)
            {
                float midNormStart = CalculateNormalized(waterLevelY, maxDepth, midTop);
                float midNormEnd = CalculateNormalized(waterLevelY, maxDepth, midBottom);
                slices.Add(new ZoneDepthSlice(
                    ZoneDepthBand.Mid,
                    midTop, midBottom,
                    midNormStart, midNormEnd,
                    new Color(0.3f, 0.3f, 0.7f, 0.5f) // 청색
                ));
            }

            // maxDepth가 Mid 범위 내에 있으면 여기서 종료
            if (maxDepth >= waterLevelY - 600f)
                return slices;

            // Deep: -600 ~ max(-1200, maxDepth)
            float deepTop = waterLevelY - 600f;
            float deepBottom = Mathf.Max(waterLevelY - 1200f, maxDepth);
            if (deepBottom < deepTop)
            {
                float deepNormStart = CalculateNormalized(waterLevelY, maxDepth, deepTop);
                float deepNormEnd = CalculateNormalized(waterLevelY, maxDepth, deepBottom);
                slices.Add(new ZoneDepthSlice(
                    ZoneDepthBand.Deep,
                    deepTop, deepBottom,
                    deepNormStart, deepNormEnd,
                    new Color(0.1f, 0.1f, 0.4f, 0.5f) // 진청색
                ));
            }

            // maxDepth가 Deep 범위 내에 있으면 여기서 종료
            if (maxDepth >= waterLevelY - 1200f)
                return slices;

            // Forbidden: -1200 ~ maxDepth
            float forbiddenTop = waterLevelY - 1200f;
            float forbiddenBottom = maxDepth;
            if (forbiddenBottom < forbiddenTop)
            {
                float forbiddenNormStart = CalculateNormalized(waterLevelY, maxDepth, forbiddenTop);
                float forbiddenNormEnd = CalculateNormalized(waterLevelY, maxDepth, forbiddenBottom);
                slices.Add(new ZoneDepthSlice(
                    ZoneDepthBand.Forbidden,
                    forbiddenTop, forbiddenBottom,
                    forbiddenNormStart, forbiddenNormEnd,
                    new Color(0.5f, 0.1f, 0.1f, 0.5f) // 적색
                ));
            }

            return slices;
        }

        /// <summary>
        /// waterLevelY(0) ~ maxDepth(1) 기준으로 y의 normalized 값을 계산한다.
        /// </summary>
        private static float CalculateNormalized(float waterLevelY, float maxDepth, float y)
        {
            if (Mathf.Approximately(waterLevelY, maxDepth))
                return 0f;
            return Mathf.InverseLerp(waterLevelY, maxDepth, y);
        }

        /// <summary>
        /// ZoneId에 해당하는 Rule 이름을 반환한다.
        /// Rule이 없으면 "Default"를 반환한다.
        /// </summary>
        private string GetSourceRuleName(ZoneId zoneId)
        {
            if (_scenarioPreset != null && _scenarioPreset.TryGetRuleForZone(zoneId, out WorldMapScenarioZoneRule rule))
            {
                return rule.RuleName;
            }
            return "Default";
        }

        /// <summary>
        /// Snapshot이 없을 때 사용할 fallback sample을 생성한다.
        /// </summary>
        private ZoneDepthSample CreateFallbackSample(Vector3 worldPosition, ZoneId zoneId)
        {
            return new ZoneDepthSample(
                zoneId: zoneId,
                regionId: new RegionId("Default"),
                biomeType: ZoneBiomeType.OpenWater,
                depthBand: ZoneDepthBand.Mid,
                y: worldPosition.y,
                normalizedDepth01: 0.5f,
                waterLevelY: 0f,
                maxDepth: -600f,
                sourceRuleName: "Fallback (no snapshot)",
                environmentProfile: null
            );
        }
    }
}
