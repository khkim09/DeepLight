using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Data.World.Design;
using Project.Gameplay.World;
using Project.Gameplay.World.Content;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.6.1 + 14.7: Zone Content Placeholder Foundation + Metadata Binding Utility.
    /// A1~C10 each ZoneRoot child creates content placeholder structure based on design data.
    /// Phase 14.7 adds WorldMapZoneContentMarker metadata component binding and WorldMapZoneContentRegistry.
    /// Not actual gameplay prefab placement, but root/marker/spawn point/debug visualization foundation.
    ///
    /// Coordinate system convention:
    /// - worldPoint: TerrainPatch에서 샘플한 실제 월드 좌표 (CalculateDeterministicPosition 반환값)
    /// - marker는 ZoneRoot 하위 구조 유지를 위해 localPosition으로 저장
    /// - 즉, Inspector에서 marker를 선택했을 때 localPosition은 zone 내부 오프셋으로 해석 가능해야 함
    /// - marker.transform.position (world position)은 해당 ZoneRoot의 TerrainPatch bounds 내부에 존재해야 함
    /// </summary>
    public static class DeepLightMapZoneContentPlaceholderUtility
    {
        private const string ContentRootName = "Content";
        private const string ResourceSpawnsName = "ResourceSpawns";
        private const string HazardSpawnsName = "HazardSpawns";
        private const string LandmarkSpawnsName = "LandmarkSpawns";
        private const string NarrativeSpawnsName = "NarrativeSpawns";
        private const string RouteMarkersName = "RouteMarkers";
        private const string DebugMarkersName = "DebugMarkers";

        // ===== URP Material Cache =====
        // Category별 shared material을 캐싱해서 재사용 (primitive마다 새 material 생성 방지)
        private static Material _cachedResourceMaterial;
        private static Material _cachedHazardMaterial;
        private static Material _cachedLandmarkMaterial;
        private static Material _cachedNarrativeMaterial;
        private static Material _cachedRouteMaterial;

        // URP shader 우선순위 검색 목록
        private static readonly string[] UrpShaderPriority = new[]
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default"
        };

        /// <summary>
        /// URP 호환 shader를 우선순위로 찾는다.
        /// 우선순위: Universal Render Pipeline/Unlit > Universal Render Pipeline/Lit > Sprites/Default
        /// 모두 실패하면 null 반환.
        /// </summary>
        private static Shader FindUrpCompatibleShader()
        {
            foreach (string shaderName in UrpShaderPriority)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null && shader.isSupported)
                    return shader;
            }
            return null;
        }

        /// <summary>
        /// 주어진 material이 pink/unSupported 상태인지 검증한다.
        /// true 반환 = 문제 있음 (pink risk).
        /// </summary>
        private static bool IsMaterialPinkOrInvalid(Material mat, out string reason)
        {
            reason = null;
            if (mat == null) { reason = "sharedMaterial == null"; return true; }
            if (mat.shader == null) { reason = "shader == null"; return true; }
            if (mat.shader.name.Contains("InternalErrorShader") || mat.shader.name.Contains("Error"))
            { reason = $"shader.name contains Error: {mat.shader.name}"; return true; }
            if (!mat.shader.isSupported)
            { reason = $"shader.isSupported == false: {mat.shader.name}"; return true; }
            if (mat.name.Contains("Error"))
            { reason = $"material.name contains Error: {mat.name}"; return true; }
            // color가 magenta에 매우 가까운지 확인
            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                if (IsMagenta(c)) { reason = $"color is near-magenta: {c}"; return true; }
            }
            else if (mat.HasProperty("_Color"))
            {
                Color c = mat.GetColor("_Color");
                if (IsMagenta(c)) { reason = $"color is near-magenta: {c}"; return true; }
            }
            return false;
        }

        /// <summary>
        /// color가 magenta(R~1, G~0, B~1)에 매우 가까운지 판정.
        /// </summary>
        private static bool IsMagenta(Color c)
        {
            return c.r > 0.8f && c.g < 0.3f && c.b > 0.8f;
        }

        /// <summary>
        /// category별 URP shared material을 생성하거나 캐싱된 것을 반환한다.
        /// material 이름: DL_DebugMarker_{category}_URP
        /// </summary>
        private static Material GetOrCreateDebugMaterial(string category, Color baseColor)
        {
            // 캐싱된 material 확인
            Material cached = GetCachedMaterial(category);
            if (cached != null) return cached;

            // URP shader 찾기
            Shader urpShader = FindUrpCompatibleShader();
            if (urpShader == null)
            {
                Debug.LogError($"[ZoneContentPlaceholder] No URP-compatible shader found! Cannot create debug material for {category}.");
                return null;
            }

            // 새 material 생성
            Material mat = new Material(urpShader);
            mat.name = $"DL_DebugMarker_{category}_URP";

            // shader property에 맞게 색상 설정: _BaseColor 우선, 없으면 _Color
            bool colorSet = false;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", baseColor);
                colorSet = true;
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", baseColor);
                colorSet = true;
            }
            if (!colorSet)
            {
                Debug.LogWarning($"[ZoneContentPlaceholder] Material {mat.name} has no _BaseColor or _Color property. Shader: {urpShader.name}");
            }

            // 투명 설정: URP Unlit/Lit의 _Surface 또는 _Blend 모드 설정
            if (mat.HasProperty("_Surface"))
            {
                // 0 = Opaque, 1 = Transparent
                mat.SetFloat("_Surface", 1f);
            }
            if (mat.HasProperty("_Blend"))
            {
                // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
                mat.SetFloat("_Blend", 0f);
            }
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_AlphaClip"))
                mat.SetFloat("_AlphaClip", 0f);

            // render queue: Transparent
            mat.renderQueue = 3000;

            // 생성 후 캐싱
            SetCachedMaterial(category, mat);

            // 생성된 material 검증
            string validationReason;
            if (IsMaterialPinkOrInvalid(mat, out validationReason))
            {
                Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Newly created material {mat.name} is invalid: {validationReason}");
            }

            return mat;
        }

        private static Material GetCachedMaterial(string category)
        {
            switch (category)
            {
                case "Resource": return _cachedResourceMaterial;
                case "Hazard": return _cachedHazardMaterial;
                case "Landmark": return _cachedLandmarkMaterial;
                case "Narrative": return _cachedNarrativeMaterial;
                case "Route": return _cachedRouteMaterial;
                default: return null;
            }
        }

        private static void SetCachedMaterial(string category, Material mat)
        {
            switch (category)
            {
                case "Resource": _cachedResourceMaterial = mat; break;
                case "Hazard": _cachedHazardMaterial = mat; break;
                case "Landmark": _cachedLandmarkMaterial = mat; break;
                case "Narrative": _cachedNarrativeMaterial = mat; break;
                case "Route": _cachedRouteMaterial = mat; break;
            }
        }

        /// <summary>
        /// static material cache를 초기화한다.
        /// Rebuild 시작 시 호출하여 이전 빌드의 캐시를 리셋한다.
        /// </summary>
        private static void ClearMaterialCache()
        {
            _cachedResourceMaterial = null;
            _cachedHazardMaterial = null;
            _cachedLandmarkMaterial = null;
            _cachedNarrativeMaterial = null;
            _cachedRouteMaterial = null;
        }

        public static void RebuildZoneContentPlaceholders(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null) { Debug.LogError("[ZoneContentPlaceholder] Settings is null!"); return; }
            if (settings.WorldMapConfig == null) { Debug.LogError("[ZoneContentPlaceholder] WorldMapConfig is null!"); return; }
            if (settings.ZoneDesignDatabase == null) { Debug.LogError("[ZoneContentPlaceholder] ZoneDesignDatabase is null!"); return; }
            if (settings.ZoneDesignRuleDatabase == null) { Debug.LogError("[ZoneContentPlaceholder] ZoneDesignRuleDatabase is null!"); return; }
            if (settings.ZoneTerrainPlanDatabase == null) { Debug.LogError("[ZoneContentPlaceholder] ZoneTerrainPlanDatabase is null!"); return; }

            // 이전 빌드의 material cache 초기화
            ClearMaterialCache();

            var config = settings.WorldMapConfig;
            var designDb = settings.ZoneDesignDatabase;
            var ruleDb = settings.ZoneDesignRuleDatabase;
            var planDb = settings.ZoneTerrainPlanDatabase;

            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null) { Debug.LogError("[ZoneContentPlaceholder] GeneratedWorldRoot not found."); return; }

            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null) { Debug.LogError("[ZoneContentPlaceholder] ZoneRoots not found."); return; }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.6.1: Rebuild Zone Content Placeholders =====");
            log.AppendLine($"MaxResourceMarkers: {settings.MaxResourceMarkersPerZone}");
            log.AppendLine($"MaxHazardMarkers: {settings.MaxHazardMarkersPerZone}");
            log.AppendLine($"MaxLandmarkMarkers: {settings.MaxLandmarkMarkersPerZone}");
            log.AppendLine($"VisibleDebugMarkers: {settings.CreateVisibleContentDebugMarkers}");

            int totalZonesProcessed = 0;
            int totalResourceMarkers = 0, totalHazardMarkers = 0, totalLandmarkMarkers = 0, totalNarrativeMarkers = 0, totalRouteMarkers = 0;

            for (int colIndex = 0; colIndex < 3; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < 10; rowIndex++)
                {
                    char columnChar = (char)('A' + colIndex);
                    int rowNumber = rowIndex + 1;
                    string zoneIdStr = $"{columnChar}{rowNumber}";
                    string zoneRootName = $"ZoneRoot_{zoneIdStr}";

                    Transform zoneRootTransform = zoneRootsTransform.Find(zoneRootName);
                    if (zoneRootTransform == null) { LogIfVerbose(settings, $"[SKIP] {zoneRootName} not found."); continue; }

                    WorldMapZoneDesignEntry entry = designDb.GetEntry(zoneIdStr);
                    if (entry == null) { LogIfVerbose(settings, $"[SKIP] No design entry for {zoneIdStr}."); continue; }

                    WorldMapZoneDesignRule rule = ruleDb.GetRule(zoneIdStr);
                    if (rule == null) { LogIfVerbose(settings, $"[SKIP] No design rule for {zoneIdStr}."); continue; }

                    WorldMapZoneTerrainPlan plan = planDb.GetPlan(zoneIdStr);
                    if (plan == null) { LogIfVerbose(settings, $"[SKIP] No terrain plan for {zoneIdStr}."); continue; }

                    ZoneCoordinate coord = new ZoneCoordinate(colIndex, rowIndex);
                    Vector3 zoneCenter = coord.GetZoneCenterWorldPosition(config);
                    var (zoneMin, zoneMax) = coord.GetZoneBounds(config);
                    Bounds? terrainPatchBounds = FindTerrainPatchBounds(zoneRootTransform, zoneIdStr);

                    Transform contentTransform = CreateOrReplaceContentRoot(zoneRootTransform, zoneIdStr);
                    GameObject resourceRoot = GetOrCreateChild(contentTransform.gameObject, ResourceSpawnsName);
                    GameObject hazardRoot = GetOrCreateChild(contentTransform.gameObject, HazardSpawnsName);
                    GameObject landmarkRoot = GetOrCreateChild(contentTransform.gameObject, LandmarkSpawnsName);
                    GameObject narrativeRoot = GetOrCreateChild(contentTransform.gameObject, NarrativeSpawnsName);
                    GameObject routeRoot = GetOrCreateChild(contentTransform.gameObject, RouteMarkersName);
                    GameObject debugRoot = GetOrCreateChild(contentTransform.gameObject, DebugMarkersName);

                    CreateResourceMarkers(settings, resourceRoot, zoneRootTransform, zoneIdStr, entry, rule, plan, zoneCenter, zoneMin, zoneMax, terrainPatchBounds, out int rmc);
                    CreateHazardMarkers(settings, hazardRoot, zoneRootTransform, zoneIdStr, entry, rule, plan, zoneCenter, zoneMin, zoneMax, terrainPatchBounds, out int hmc);
                    CreateLandmarkMarkers(settings, landmarkRoot, zoneRootTransform, zoneIdStr, entry, rule, plan, zoneCenter, zoneMin, zoneMax, terrainPatchBounds, out int lmc);
                    CreateNarrativeMarkers(settings, narrativeRoot, zoneRootTransform, zoneIdStr, entry, rule, plan, zoneCenter, zoneMin, zoneMax, terrainPatchBounds, out int nmc);
                    CreateRouteMarkers(settings, routeRoot, zoneRootTransform, zoneIdStr, entry, rule, plan, zoneCenter, zoneMin, zoneMax, terrainPatchBounds, out int rtc);

                    totalResourceMarkers += rmc; totalHazardMarkers += hmc; totalLandmarkMarkers += lmc; totalNarrativeMarkers += nmc; totalRouteMarkers += rtc;
                    totalZonesProcessed++;
                    if (settings.LogZoneContentPlaceholderVerbose)
                        log.AppendLine($"  [OK] {zoneIdStr}: R={rmc} H={hmc} L={lmc} N={nmc} Rt={rtc}");
                }
            }

            log.AppendLine($"Zones processed: {totalZonesProcessed}");
            log.AppendLine($"Total markers: Resource={totalResourceMarkers} Hazard={totalHazardMarkers} Landmark={totalLandmarkMarkers} Narrative={totalNarrativeMarkers} Route={totalRouteMarkers}");

            // Phase 14.7: Attach WorldMapZoneContentRegistry to GeneratedWorldRoot
            AttachContentRegistry(generatedRoot);

            // Phase 14.7: Rebuild registry cache
            WorldMapZoneContentRegistry registry = generatedRoot.GetComponent<WorldMapZoneContentRegistry>();
            if (registry != null)
            {
                registry.RebuildCache();
                log.AppendLine($"Phase 14.7: WorldMapZoneContentRegistry attached and cache rebuilt. Marker count: {registry.CachedMarkerCount}");
            }

            log.AppendLine("===== Phase 14.6.1 + 14.7: Rebuild Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Phase 14.7: GeneratedWorldRoot에 WorldMapZoneContentRegistry를 부착한다.
        /// 이미 존재하면 재사용하고, 없으면 새로 추가한다.
        /// </summary>
        private static void AttachContentRegistry(GameObject generatedRoot)
        {
            WorldMapZoneContentRegistry registry = generatedRoot.GetComponent<WorldMapZoneContentRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapZoneContentRegistry>();
                Debug.Log("[ZoneContentPlaceholder] [Phase 14.7] WorldMapZoneContentRegistry added to GeneratedWorldRoot.");
            }
            else
            {
                Debug.Log("[ZoneContentPlaceholder] [Phase 14.7] WorldMapZoneContentRegistry already exists on GeneratedWorldRoot. Reusing.");
            }
        }

        /// <summary>
        /// Phase 14.7: marker GameObject에 WorldMapZoneContentMarker를 부착하고 metadata를 설정한다.
        /// </summary>
        private static void AttachContentMarker(
            GameObject marker,
            string zoneId,
            string markerId,
            WorldMapZoneContentMarkerType markerType,
            string[] tags,
            string sourceRuleSummary,
            string sourcePlanSummary,
            bool isRuntimeSpawnPoint,
            bool isDebugOnly)
        {
            WorldMapZoneContentMarker cm = marker.GetComponent<WorldMapZoneContentMarker>();
            if (cm == null)
            {
                cm = marker.AddComponent<WorldMapZoneContentMarker>();
            }
            cm.Configure(zoneId, markerId, markerType, tags, sourceRuleSummary, sourcePlanSummary, isRuntimeSpawnPoint, isDebugOnly);
        }

        public static void ValidateZoneContentPlaceholders(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null) { Debug.LogError("[ZoneContentPlaceholder] Settings is null!"); return; }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.6.1: Validate Zone Content Placeholders =====");
            int passCount = 0, failCount = 0, warnCount = 0;

            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null) { log.AppendLine("  [PASS] GeneratedWorldRoot exists."); passCount++; }
            else { log.AppendLine("  [FAIL] GeneratedWorldRoot not found!"); failCount++; }

            Transform zoneRootsTransform = null;
            if (generatedRoot != null)
            {
                zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
                if (zoneRootsTransform != null) { log.AppendLine("  [PASS] ZoneRoots exists."); passCount++; }
                else { log.AppendLine("  [FAIL] ZoneRoots not found!"); failCount++; }
            }

            int zoneRootFound = 0;
            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                    for (int row = 0; row < 10; row++)
                    {
                        char c = (char)('A' + col);
                        if (zoneRootsTransform.Find($"ZoneRoot_{c}{row + 1}") != null) zoneRootFound++;
                    }
            }
            if (zoneRootFound == 30) { log.AppendLine("  [PASS] All 30 ZoneRoot_A1~C10 exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] ZoneRoot_A1~C10 found: {zoneRootFound}/30"); failCount++; }

            if (settings.ZoneDesignDatabase != null) { log.AppendLine("  [PASS] WorldMapZoneDesignDatabase exists."); passCount++; }
            else { log.AppendLine("  [FAIL] WorldMapZoneDesignDatabase is null!"); failCount++; }

            if (settings.ZoneDesignRuleDatabase != null) { log.AppendLine("  [PASS] WorldMapZoneDesignRuleDatabase exists."); passCount++; }
            else { log.AppendLine("  [FAIL] WorldMapZoneDesignRuleDatabase is null!"); failCount++; }

            if (settings.ZoneTerrainPlanDatabase != null) { log.AppendLine("  [PASS] WorldMapZoneTerrainPlanDatabase exists."); passCount++; }
            else { log.AppendLine("  [FAIL] WorldMapZoneTerrainPlanDatabase is null!"); failCount++; }

            if (zoneRootsTransform == null) { log.AppendLine("  [FAIL] Cannot continue validation."); failCount++; Debug.LogWarning(log.ToString()); return; }

            int contentRootCount = 0, resourceSpawnRootCount = 0, hazardSpawnRootCount = 0, landmarkSpawnRootCount = 0;
            int narrativeSpawnRootCount = 0, routeMarkerRootCount = 0, debugMarkerRootCount = 0;
            int sparseZoneExcessiveMarkers = 0, landmarkRoleNoneMissing = 0, narrativeRequiredMissing = 0, hazardDensityPositiveMissing = 0;
            int pinkMaterialCount = 0, enabledColliderCount = 0, markerOutsideBounds = 0;
            int invalidLocalScaleCount = 0, invalidLocalRotationCount = 0, invalidLocalPositionCount = 0;
            int debugChildNotAtOriginCount = 0;

            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 10; row++)
                {
                    char c = (char)('A' + col);
                    string zoneIdStr = $"{c}{row + 1}";
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{zoneIdStr}");
                    if (zr == null) continue;

                    WorldMapZoneDesignEntry entry = settings.ZoneDesignDatabase?.GetEntry(zoneIdStr);
                    WorldMapZoneDesignRule rule = settings.ZoneDesignRuleDatabase?.GetRule(zoneIdStr);

                    Transform contentT = zr.Find(ContentRootName);
                    if (contentT != null)
                    {
                        contentRootCount++;
                        if (contentT.Find(ResourceSpawnsName) != null) resourceSpawnRootCount++;
                        if (contentT.Find(HazardSpawnsName) != null) hazardSpawnRootCount++;
                        if (contentT.Find(LandmarkSpawnsName) != null) landmarkSpawnRootCount++;
                        if (contentT.Find(NarrativeSpawnsName) != null) narrativeSpawnRootCount++;
                        if (contentT.Find(RouteMarkersName) != null) routeMarkerRootCount++;
                        if (contentT.Find(DebugMarkersName) != null) debugMarkerRootCount++;

                        Transform resourceRoot = contentT.Find(ResourceSpawnsName);
                        if (resourceRoot != null)
                        {
                            int count = resourceRoot.childCount;
                            if (entry != null && entry.intentionallySparse && count > settings.MaxResourceMarkersPerZone)
                                sparseZoneExcessiveMarkers++;
                        }

                        Transform hazardRoot = contentT.Find(HazardSpawnsName);
                        if (hazardRoot != null)
                        {
                            int count = hazardRoot.childCount;
                            if (rule != null && rule.hazardDensity01 > 0f && count == 0 && (entry == null || !entry.intentionallySparse))
                                hazardDensityPositiveMissing++;
                        }

                        Transform landmarkRoot = contentT.Find(LandmarkSpawnsName);
                        if (landmarkRoot != null)
                        {
                            int count = landmarkRoot.childCount;
                            if (rule != null && rule.landmarkRole != ZoneLandmarkRole.None && count == 0)
                                landmarkRoleNoneMissing++;
                        }

                        Transform narrativeRoot = contentT.Find(NarrativeSpawnsName);
                        if (narrativeRoot != null)
                        {
                            int count = narrativeRoot.childCount;
                            bool hasNarrativeNeed = (rule != null && rule.requiresNarrativeObject) ||
                                (entry != null && (!string.IsNullOrEmpty(entry.logOrHint) || !string.IsNullOrEmpty(entry.narrativeFunction)));
                            if (hasNarrativeNeed && count == 0) narrativeRequiredMissing++;
                        }

                        if (settings.CreateVisibleContentDebugMarkers)
                            CheckAllChildrenForIssues(contentT, zr, ref pinkMaterialCount, ref enabledColliderCount, ref markerOutsideBounds,
                                ref invalidLocalScaleCount, ref invalidLocalRotationCount, ref invalidLocalPositionCount, ref debugChildNotAtOriginCount);
                    }
                }
            }

            if (contentRootCount == 30) { log.AppendLine("  [PASS] All 30 Content roots exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] Content roots: {contentRootCount}/30"); failCount++; }

            if (resourceSpawnRootCount == 30) { log.AppendLine("  [PASS] All 30 ResourceSpawns roots exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] ResourceSpawns roots: {resourceSpawnRootCount}/30"); failCount++; }

            if (hazardSpawnRootCount == 30) { log.AppendLine("  [PASS] All 30 HazardSpawns roots exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] HazardSpawns roots: {hazardSpawnRootCount}/30"); failCount++; }

            if (landmarkSpawnRootCount == 30) { log.AppendLine("  [PASS] All 30 LandmarkSpawns roots exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] LandmarkSpawns roots: {landmarkSpawnRootCount}/30"); failCount++; }

            if (narrativeSpawnRootCount == 30) { log.AppendLine("  [PASS] All 30 NarrativeSpawns roots exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] NarrativeSpawns roots: {narrativeSpawnRootCount}/30"); failCount++; }

            if (routeMarkerRootCount == 30) { log.AppendLine("  [PASS] All 30 RouteMarkers roots exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] RouteMarkers roots: {routeMarkerRootCount}/30"); failCount++; }

            if (debugMarkerRootCount == 30) { log.AppendLine("  [PASS] All 30 DebugMarkers roots exist."); passCount++; }
            else { log.AppendLine($"  [FAIL] DebugMarkers roots: {debugMarkerRootCount}/30"); failCount++; }

            if (sparseZoneExcessiveMarkers == 0) { log.AppendLine("  [PASS] No sparse zones with excessive markers."); passCount++; }
            else { log.AppendLine($"  [WARN] {sparseZoneExcessiveMarkers} sparse zone(s) have excessive markers."); warnCount++; }

            if (landmarkRoleNoneMissing == 0) { log.AppendLine("  [PASS] All non-None landmarkRole zones have Landmark markers."); passCount++; }
            else { log.AppendLine($"  [FAIL] {landmarkRoleNoneMissing} zone(s) lack Landmark markers."); failCount++; }

            if (narrativeRequiredMissing == 0) { log.AppendLine("  [PASS] All narrative-required zones have Narrative markers."); passCount++; }
            else { log.AppendLine($"  [FAIL] {narrativeRequiredMissing} zone(s) lack Narrative markers."); failCount++; }

            if (hazardDensityPositiveMissing == 0) { log.AppendLine("  [PASS] All hazardDensity>0 zones have Hazard markers."); passCount++; }
            else { log.AppendLine($"  [WARN] {hazardDensityPositiveMissing} zone(s) with hazardDensity>0 lack Hazard markers."); warnCount++; }

            if (pinkMaterialCount == 0) { log.AppendLine("  [PASS] No pink materials."); passCount++; }
            else { log.AppendLine($"  [FAIL] {pinkMaterialCount} pink material(s) found!"); failCount++; }

            if (enabledColliderCount == 0) { log.AppendLine("  [PASS] No enabled colliders."); passCount++; }
            else { log.AppendLine($"  [FAIL] {enabledColliderCount} enabled collider(s) found!"); failCount++; }

            if (invalidLocalScaleCount == 0) { log.AppendLine("  [PASS] All markers have localScale = Vector3.one."); passCount++; }
            else { log.AppendLine($"  [FAIL] {invalidLocalScaleCount} marker(s) have non-default localScale."); failCount++; }

            if (invalidLocalRotationCount == 0) { log.AppendLine("  [PASS] All markers have localRotation = identity."); passCount++; }
            else { log.AppendLine($"  [FAIL] {invalidLocalRotationCount} marker(s) have non-identity localRotation."); failCount++; }

            if (invalidLocalPositionCount == 0) { log.AppendLine("  [PASS] All markers have valid local positions."); passCount++; }
            else { log.AppendLine($"  [FAIL] {invalidLocalPositionCount} marker(s) have invalid local positions (outside zone bounds)."); failCount++; }

            if (debugChildNotAtOriginCount == 0) { log.AppendLine("  [PASS] All debug primitive children at local origin."); passCount++; }
            else { log.AppendLine($"  [FAIL] {debugChildNotAtOriginCount} debug primitive child(ren) not at local origin."); failCount++; }

            if (GameObject.Find("MapSettings") != null) { log.AppendLine("  [PASS] MapSettings preserved."); passCount++; }
            else { log.AppendLine("  [FAIL] MapSettings not found!"); failCount++; }

            if (GameObject.Find("_WorldMap_Manual") != null) { log.AppendLine("  [PASS] _WorldMap_Manual preserved."); passCount++; }
            else { log.AppendLine("  [FAIL] _WorldMap_Manual not found!"); failCount++; }

            if (context != null) { log.AppendLine("  [PASS] DeepLightMapAutoBuilderContext preserved."); passCount++; }
            else { log.AppendLine("  [WARN] DeepLightMapAutoBuilderContext is null."); warnCount++; }

            // ===== Phase 14.7: Metadata Binding Validation =====
            log.AppendLine("\n--- Phase 14.7: Metadata Binding Validation ---");
            int p14Pass = 0, p14Fail = 0, p14Warn = 0;

            // 1. GeneratedWorldRoot has WorldMapZoneContentRegistry
            WorldMapZoneContentRegistry registry = generatedRoot != null ? generatedRoot.GetComponent<WorldMapZoneContentRegistry>() : null;
            if (registry != null) { log.AppendLine("  [PASS] GeneratedWorldRoot has WorldMapZoneContentRegistry."); p14Pass++; }
            else { log.AppendLine("  [FAIL] GeneratedWorldRoot missing WorldMapZoneContentRegistry!"); p14Fail++; }

            // 2. Registry marker count equals generated marker component count
            int registryMarkerCount = 0;
            int actualComponentCount = 0;
            if (registry != null)
            {
                registry.RebuildCache();
                registryMarkerCount = registry.CachedMarkerCount;
                // Count all WorldMapZoneContentMarker components under GeneratedWorldRoot (excluding MapSettings and _WorldMap_Manual)
                WorldMapZoneContentMarker[] allMarkers = generatedRoot.GetComponentsInChildren<WorldMapZoneContentMarker>(true);
                foreach (var m in allMarkers)
                {
                    if (m.transform.IsChildOf(generatedRoot.transform) &&
                        !IsUnderObject(m.transform, "MapSettings") &&
                        !IsUnderObject(m.transform, "_WorldMap_Manual"))
                    {
                        actualComponentCount++;
                    }
                }
                if (registryMarkerCount == actualComponentCount) { log.AppendLine($"  [PASS] Registry marker count ({registryMarkerCount}) matches component count ({actualComponentCount})."); p14Pass++; }
                else { log.AppendLine($"  [FAIL] Registry marker count ({registryMarkerCount}) != component count ({actualComponentCount})!"); p14Fail++; }
            }

            // 3-7. Check all spawn roots have WorldMapZoneContentMarker
            int resourceMarkerMissing = 0, hazardMarkerMissing = 0, landmarkMarkerMissing = 0, narrativeMarkerMissing = 0, routeMarkerMissing = 0;
            int zoneIdMismatch = 0, emptyMarkerId = 0, duplicateMarkerId = 0, unknownMarkerType = 0;
            int runtimeSpawnPointFail = 0, routeSpawnPointFail = 0;
            int markerOutsideLocalBounds = 0;
            int mapSettingsMarkerCount = 0, worldMapManualMarkerCount = 0;

            HashSet<string> markerIds = new HashSet<string>();

            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char c = (char)('A' + col);
                        string zoneIdStr = $"{c}{row + 1}";
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{zoneIdStr}");
                        if (zr == null) continue;

                        Transform contentT = zr.Find(ContentRootName);
                        if (contentT == null) continue;

                        // Check each spawn root
                        CheckSpawnRootMarkers(contentT, ResourceSpawnsName, zoneIdStr, WorldMapZoneContentMarkerType.Resource,
                            ref resourceMarkerMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                            ref unknownMarkerType, ref runtimeSpawnPointFail, ref routeSpawnPointFail,
                            ref markerOutsideLocalBounds, markerIds, zr);

                        CheckSpawnRootMarkers(contentT, HazardSpawnsName, zoneIdStr, WorldMapZoneContentMarkerType.Hazard,
                            ref hazardMarkerMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                            ref unknownMarkerType, ref runtimeSpawnPointFail, ref routeSpawnPointFail,
                            ref markerOutsideLocalBounds, markerIds, zr);

                        CheckSpawnRootMarkers(contentT, LandmarkSpawnsName, zoneIdStr, WorldMapZoneContentMarkerType.Landmark,
                            ref landmarkMarkerMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                            ref unknownMarkerType, ref runtimeSpawnPointFail, ref routeSpawnPointFail,
                            ref markerOutsideLocalBounds, markerIds, zr);

                        CheckSpawnRootMarkers(contentT, NarrativeSpawnsName, zoneIdStr, WorldMapZoneContentMarkerType.Narrative,
                            ref narrativeMarkerMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                            ref unknownMarkerType, ref runtimeSpawnPointFail, ref routeSpawnPointFail,
                            ref markerOutsideLocalBounds, markerIds, zr);

                        CheckSpawnRootMarkers(contentT, RouteMarkersName, zoneIdStr, WorldMapZoneContentMarkerType.Route,
                            ref routeMarkerMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                            ref unknownMarkerType, ref runtimeSpawnPointFail, ref routeSpawnPointFail,
                            ref markerOutsideLocalBounds, markerIds, zr);
                    }
                }
            }

            // Check MapSettings and _WorldMap_Manual for marker components
            GameObject mapSettings = GameObject.Find("MapSettings");
            if (mapSettings != null)
                mapSettingsMarkerCount = mapSettings.GetComponentsInChildren<WorldMapZoneContentMarker>(true).Length;
            GameObject worldMapManual = GameObject.Find("_WorldMap_Manual");
            if (worldMapManual != null)
                worldMapManualMarkerCount = worldMapManual.GetComponentsInChildren<WorldMapZoneContentMarker>(true).Length;

            // 3. Resource markers
            if (resourceMarkerMissing == 0) { log.AppendLine("  [PASS] All ResourceSpawn roots have WorldMapZoneContentMarker."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {resourceMarkerMissing} ResourceSpawn marker(s) missing WorldMapZoneContentMarker!"); p14Fail++; }

            // 4. Hazard markers
            if (hazardMarkerMissing == 0) { log.AppendLine("  [PASS] All HazardSpawn roots have WorldMapZoneContentMarker."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {hazardMarkerMissing} HazardSpawn marker(s) missing WorldMapZoneContentMarker!"); p14Fail++; }

            // 5. Landmark markers
            if (landmarkMarkerMissing == 0) { log.AppendLine("  [PASS] All LandmarkSpawn roots have WorldMapZoneContentMarker."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {landmarkMarkerMissing} LandmarkSpawn marker(s) missing WorldMapZoneContentMarker!"); p14Fail++; }

            // 6. Narrative markers
            if (narrativeMarkerMissing == 0) { log.AppendLine("  [PASS] All NarrativeSpawn roots have WorldMapZoneContentMarker."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {narrativeMarkerMissing} NarrativeSpawn marker(s) missing WorldMapZoneContentMarker!"); p14Fail++; }

            // 7. Route markers
            if (routeMarkerMissing == 0) { log.AppendLine("  [PASS] All RouteMarker roots have WorldMapZoneContentMarker."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {routeMarkerMissing} RouteMarker marker(s) missing WorldMapZoneContentMarker!"); p14Fail++; }

            // 8. ZoneId match
            if (zoneIdMismatch == 0) { log.AppendLine("  [PASS] All marker ZoneId values match parent ZoneRoot id."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {zoneIdMismatch} marker(s) have ZoneId mismatch!"); p14Fail++; }

            // 9. MarkerId non-empty and unique
            if (emptyMarkerId == 0) { log.AppendLine("  [PASS] All marker MarkerId values are non-empty."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {emptyMarkerId} marker(s) have empty MarkerId!"); p14Fail++; }
            if (duplicateMarkerId == 0) { log.AppendLine("  [PASS] All marker MarkerId values are unique."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {duplicateMarkerId} duplicate MarkerId(s) found!"); p14Fail++; }

            // 10. MarkerType not Unknown
            if (unknownMarkerType == 0) { log.AppendLine("  [PASS] All marker MarkerType values are not Unknown."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {unknownMarkerType} marker(s) have MarkerType=Unknown!"); p14Fail++; }

            // 11. Runtime spawn markers have IsRuntimeSpawnPoint=true
            if (runtimeSpawnPointFail == 0) { log.AppendLine("  [PASS] All runtime spawn markers have IsRuntimeSpawnPoint=true."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {runtimeSpawnPointFail} runtime spawn marker(s) have IsRuntimeSpawnPoint=false!"); p14Fail++; }

            // 12. Route markers have IsRuntimeSpawnPoint=false
            if (routeSpawnPointFail == 0) { log.AppendLine("  [PASS] All Route markers have IsRuntimeSpawnPoint=false."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {routeSpawnPointFail} Route marker(s) have IsRuntimeSpawnPoint=true!"); p14Fail++; }

            // 13. LocalPositionInZone inside zone local bounds
            if (markerOutsideLocalBounds == 0) { log.AppendLine("  [PASS] All marker LocalPositionInZone values are inside zone local bounds."); p14Pass++; }
            else { log.AppendLine($"  [WARN] {markerOutsideLocalBounds} marker(s) have LocalPositionInZone outside expected bounds."); p14Warn++; }

            // 14. No marker under MapSettings
            if (mapSettingsMarkerCount == 0) { log.AppendLine("  [PASS] No WorldMapZoneContentMarker under MapSettings."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {mapSettingsMarkerCount} WorldMapZoneContentMarker(s) found under MapSettings!"); p14Fail++; }

            // 15. No marker under _WorldMap_Manual
            if (worldMapManualMarkerCount == 0) { log.AppendLine("  [PASS] No WorldMapZoneContentMarker under _WorldMap_Manual."); p14Pass++; }
            else { log.AppendLine($"  [FAIL] {worldMapManualMarkerCount} WorldMapZoneContentMarker(s) found under _WorldMap_Manual!"); p14Fail++; }

            // 16. Existing FAIL=0 state must remain
            if (failCount == 0) { log.AppendLine("  [PASS] Existing FAIL=0 state preserved."); p14Pass++; }
            else { log.AppendLine($"  [WARN] Existing FAIL count = {failCount}. Phase 14.7 checks may add more."); p14Warn++; }

            // 17. hazardDensity > 0 but no Hazard marker remains WARN only
            if (hazardDensityPositiveMissing > 0) { log.AppendLine($"  [WARN] {hazardDensityPositiveMissing} zone(s) with hazardDensity>0 lack Hazard markers (existing WARN preserved)."); p14Warn++; }

            log.AppendLine($"\n===== Phase 14.7 Results: {p14Pass} PASS, {p14Fail} FAIL, {p14Warn} WARN =====");
            passCount += p14Pass; failCount += p14Fail; warnCount += p14Warn;
            log.AppendLine($"\n===== Combined Results: {passCount} PASS, {failCount} FAIL, {warnCount} WARN =====");
            Debug.LogWarning(log.ToString());
        }

        /// <summary>
        /// Phase 14.7: 특정 SpawnRoot 하위 marker들의 WorldMapZoneContentMarker metadata를 검증한다.
        /// </summary>
        private static void CheckSpawnRootMarkers(
            Transform contentRoot, string spawnRootName, string expectedZoneId, WorldMapZoneContentMarkerType expectedType,
            ref int missingComponent, ref int zoneIdMismatch, ref int emptyMarkerId, ref int duplicateMarkerId,
            ref int unknownMarkerType, ref int runtimeSpawnPointFail, ref int routeSpawnPointFail,
            ref int markerOutsideLocalBounds, HashSet<string> markerIds, Transform zoneRootTransform)
        {
            Transform spawnRoot = contentRoot.Find(spawnRootName);
            if (spawnRoot == null) return;

            foreach (Transform markerTransform in spawnRoot)
            {
                WorldMapZoneContentMarker cm = markerTransform.GetComponent<WorldMapZoneContentMarker>();
                if (cm == null)
                {
                    missingComponent++;
                    Debug.LogError($"[ZoneContentPlaceholder] [Phase 14.7 FAIL] Marker {markerTransform.name} missing WorldMapZoneContentMarker!");
                    continue;
                }

                // Check ZoneId
                if (cm.ZoneId != expectedZoneId)
                {
                    zoneIdMismatch++;
                    Debug.LogError($"[ZoneContentPlaceholder] [Phase 14.7 FAIL] Marker {cm.MarkerId} ZoneId='{cm.ZoneId}' != expected '{expectedZoneId}'!");
                }

                // Check MarkerId
                if (string.IsNullOrEmpty(cm.MarkerId))
                {
                    emptyMarkerId++;
                    Debug.LogError($"[ZoneContentPlaceholder] [Phase 14.7 FAIL] Marker has empty MarkerId!");
                }
                else if (markerIds.Contains(cm.MarkerId))
                {
                    duplicateMarkerId++;
                    Debug.LogError($"[ZoneContentPlaceholder] [Phase 14.7 FAIL] Duplicate MarkerId: {cm.MarkerId}!");
                }
                else
                {
                    markerIds.Add(cm.MarkerId);
                }

                // Check MarkerType
                if (cm.MarkerType == WorldMapZoneContentMarkerType.Unknown)
                {
                    unknownMarkerType++;
                    Debug.LogError($"[ZoneContentPlaceholder] [Phase 14.7 FAIL] Marker {cm.MarkerId} has MarkerType=Unknown!");
                }

                // Check IsRuntimeSpawnPoint
                if (expectedType != WorldMapZoneContentMarkerType.Route && expectedType != WorldMapZoneContentMarkerType.Debug)
                {
                    if (!cm.IsRuntimeSpawnPoint)
                    {
                        runtimeSpawnPointFail++;
                        Debug.LogError($"[ZoneContentPlaceholder] [Phase 14.7 FAIL] Marker {cm.MarkerId} ({expectedType}) has IsRuntimeSpawnPoint=false!");
                    }
                }
                else if (expectedType == WorldMapZoneContentMarkerType.Route)
                {
                    if (cm.IsRuntimeSpawnPoint)
                    {
                        routeSpawnPointFail++;
                        Debug.LogError($"[ZoneContentPlaceholder] [Phase 14.7 FAIL] Route marker {cm.MarkerId} has IsRuntimeSpawnPoint=true!");
                    }
                }

                // Check LocalPositionInZone inside zone local bounds
                if (zoneRootTransform != null)
                {
                    float patchSize = 400f;
                    Vector3 localPos = cm.LocalPositionInZone;
                    if (Mathf.Abs(localPos.x) > patchSize * 2 || Mathf.Abs(localPos.z) > patchSize * 2)
                    {
                        markerOutsideLocalBounds++;
                        Debug.LogWarning($"[ZoneContentPlaceholder] [Phase 14.7 WARN] Marker {cm.MarkerId} LocalPositionInZone outside expected bounds: {localPos}");
                    }
                }
            }
        }

        /// <summary>
        /// Phase 14.7: 주어진 transform이 특정 이름의 오브젝트 하위에 있는지 확인한다.
        /// </summary>
        private static bool IsUnderObject(Transform target, string rootName)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == rootName) return true;
                current = current.parent;
            }
            return false;
        }

        private static int CreateResourceMarkers(DeepLightMapAutoBuilderSettingsSO settings, GameObject parentRoot, Transform zoneRootTransform, string zoneId,
            WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan,
            Vector3 zoneCenter, Vector3 zoneMin, Vector3 zoneMax, Bounds? terrainPatchBounds, out int totalMarkers)
        {
            totalMarkers = 0;
            bool hasResourceTags = rule.resourceTags != null && rule.resourceTags.Length > 0;
            bool hasResourceGroups = !string.IsNullOrEmpty(entry.resourceGroups);
            if (!hasResourceTags && !hasResourceGroups) return 0;

            float density = rule.resourceDensity01;
            int maxCount = settings.MaxResourceMarkersPerZone;
            int count = Mathf.Clamp(Mathf.Max(1, Mathf.RoundToInt(density * maxCount)), 1, maxCount);

            string[] tags = rule.resourceTags;
            if (tags == null || tags.Length == 0)
            {
                string[] groups = entry.resourceGroups.Split(',');
                tags = new string[groups.Length];
                for (int i = 0; i < groups.Length; i++) tags[i] = groups[i].Trim();
            }

            string ruleSummary = rule != null ? rule.debugSummary : "";
            string planSummary = plan != null ? plan.debugSummary : "";

            for (int i = 0; i < count; i++)
            {
                string tag = (tags != null && tags.Length > 0) ? tags[i % tags.Length] : "Generic";
                string markerName = $"ResourceSpawn_{zoneId}_{i}_{tag}";
                Vector3 worldPoint = CalculateDeterministicPosition(zoneId, "Resource", i, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                GameObject marker = CreateMarkerGameObjectLocal(markerName, parentRoot, zoneRootTransform, worldPoint);
                if (settings.CreateVisibleContentDebugMarkers)
                    CreateDebugPrimitive(marker, PrimitiveType.Sphere, new Color(0.2f, 0.8f, 0.2f, 0.6f), "Resource");
                // Phase 14.7: Attach metadata
                AttachContentMarker(marker, zoneId, markerName, WorldMapZoneContentMarkerType.Resource, tags, ruleSummary, planSummary, true, false);
                totalMarkers++;
            }
            return totalMarkers;
        }

        private static int CreateHazardMarkers(DeepLightMapAutoBuilderSettingsSO settings, GameObject parentRoot, Transform zoneRootTransform, string zoneId,
            WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan,
            Vector3 zoneCenter, Vector3 zoneMin, Vector3 zoneMax, Bounds? terrainPatchBounds, out int totalMarkers)
        {
            totalMarkers = 0;
            bool hasHazardTags = rule.hazardTags != null && rule.hazardTags.Length > 0;
            bool hasHazards = !string.IsNullOrEmpty(entry.hazards);
            if (!hasHazardTags && !hasHazards) return 0;

            float density = rule.hazardDensity01;
            int maxCount = settings.MaxHazardMarkersPerZone;
            int count = Mathf.Clamp(Mathf.RoundToInt(density * maxCount), 0, maxCount);
            if (count == 0) return 0;

            string[] tags = rule.hazardTags;
            if (tags == null || tags.Length == 0)
            {
                string[] hazardList = entry.hazards.Split(',');
                tags = new string[hazardList.Length];
                for (int i = 0; i < hazardList.Length; i++) tags[i] = hazardList[i].Trim();
            }

            string ruleSummary = rule != null ? rule.debugSummary : "";
            string planSummary = plan != null ? plan.debugSummary : "";

            for (int i = 0; i < count; i++)
            {
                string tag = (tags != null && tags.Length > 0) ? tags[i % tags.Length] : "Generic";
                string markerName = $"HazardSpawn_{zoneId}_{i}_{tag}";
                Vector3 worldPoint = CalculateDeterministicPosition(zoneId, "Hazard", i, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                GameObject marker = CreateMarkerGameObjectLocal(markerName, parentRoot, zoneRootTransform, worldPoint);
                if (settings.CreateVisibleContentDebugMarkers)
                    CreateDebugPrimitive(marker, PrimitiveType.Cube, new Color(0.8f, 0.2f, 0.2f, 0.6f), "Hazard");
                // Phase 14.7: Attach metadata
                AttachContentMarker(marker, zoneId, markerName, WorldMapZoneContentMarkerType.Hazard, tags, ruleSummary, planSummary, true, false);
                totalMarkers++;
            }
            return totalMarkers;
        }

        private static int CreateLandmarkMarkers(DeepLightMapAutoBuilderSettingsSO settings, GameObject parentRoot, Transform zoneRootTransform, string zoneId,
            WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan,
            Vector3 zoneCenter, Vector3 zoneMin, Vector3 zoneMax, Bounds? terrainPatchBounds, out int totalMarkers)
        {
            totalMarkers = 0;
            bool hasLandmarkTags = rule.landmarkTags != null && rule.landmarkTags.Length > 0;
            bool hasLandmarkRole = rule.landmarkRole != ZoneLandmarkRole.None;
            bool isMajorLandmark = entry.isMajorLandmark;
            if (!hasLandmarkTags && !hasLandmarkRole && !isMajorLandmark) return 0;

            float weight = rule.landmarkWeight01;
            int maxCount = settings.MaxLandmarkMarkersPerZone;
            int count = Mathf.Clamp(Mathf.Max(1, Mathf.RoundToInt(weight * maxCount)), 1, maxCount);

            string[] tags = rule.landmarkTags;
            if (tags == null || tags.Length == 0) tags = new[] { rule.landmarkRole.ToString() };

            string ruleSummary = rule != null ? rule.debugSummary : "";
            string planSummary = plan != null ? plan.debugSummary : "";

            for (int i = 0; i < count; i++)
            {
                string tag = (tags != null && tags.Length > 0) ? tags[i % tags.Length] : rule.landmarkRole.ToString();
                string markerName = $"LandmarkSpawn_{zoneId}_{i}_{tag}";
                Vector3 worldPoint = CalculateDeterministicPosition(zoneId, "Landmark", i, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                GameObject marker = CreateMarkerGameObjectLocal(markerName, parentRoot, zoneRootTransform, worldPoint);
                if (settings.CreateVisibleContentDebugMarkers)
                    CreateDebugPrimitive(marker, PrimitiveType.Cylinder, new Color(0.8f, 0.8f, 0.2f, 0.6f), "Landmark");
                // Phase 14.7: Attach metadata
                AttachContentMarker(marker, zoneId, markerName, WorldMapZoneContentMarkerType.Landmark, tags, ruleSummary, planSummary, true, false);
                totalMarkers++;
            }
            return totalMarkers;
        }

        private static int CreateNarrativeMarkers(DeepLightMapAutoBuilderSettingsSO settings, GameObject parentRoot, Transform zoneRootTransform, string zoneId,
            WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan,
            Vector3 zoneCenter, Vector3 zoneMin, Vector3 zoneMax, Bounds? terrainPatchBounds, out int totalMarkers)
        {
            totalMarkers = 0;
            bool requiresNarrative = rule.requiresNarrativeObject;
            bool hasLogOrHint = !string.IsNullOrEmpty(entry.logOrHint);
            bool hasNarrativeFunction = !string.IsNullOrEmpty(entry.narrativeFunction);
            if (!requiresNarrative && !hasLogOrHint && !hasNarrativeFunction) return 0;

            string ruleSummary = rule != null ? rule.debugSummary : "";
            string planSummary = plan != null ? plan.debugSummary : "";

            string markerName = $"NarrativeSpawn_{zoneId}_0";
            Vector3 worldPoint = CalculateDeterministicPosition(zoneId, "Narrative", 0, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
            GameObject marker = CreateMarkerGameObjectLocal(markerName, parentRoot, zoneRootTransform, worldPoint);
            if (settings.CreateVisibleContentDebugMarkers)
                CreateDebugPrimitive(marker, PrimitiveType.Capsule, new Color(0.6f, 0.4f, 0.8f, 0.6f), "Narrative");
            // Phase 14.7: Attach metadata
            string[] narrativeTags = rule.requiresNarrativeObject ? new[] { "Narrative" } : new[] { "LogOrHint" };
            AttachContentMarker(marker, zoneId, markerName, WorldMapZoneContentMarkerType.Narrative, narrativeTags, ruleSummary, planSummary, true, false);
            totalMarkers = 1;
            return totalMarkers;
        }

        private static int CreateRouteMarkers(DeepLightMapAutoBuilderSettingsSO settings, GameObject parentRoot, Transform zoneRootTransform, string zoneId,
            WorldMapZoneDesignEntry entry, WorldMapZoneDesignRule rule, WorldMapZoneTerrainPlan plan,
            Vector3 zoneCenter, Vector3 zoneMin, Vector3 zoneMax, Bounds? terrainPatchBounds, out int totalMarkers)
        {
            totalMarkers = 0;
            ZoneRouteRole routeRole = rule.routeRole;
            int markerCount = 0;
            Vector3[] positions = null;

            switch (routeRole)
            {
                case ZoneRouteRole.MainRoute:
                    markerCount = 3;
                    positions = new Vector3[3];
                    positions[0] = CalculateDeterministicPosition(zoneId, "Route", 0, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                    positions[1] = CalculateDeterministicPosition(zoneId, "Route", 1, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                    positions[2] = CalculateDeterministicPosition(zoneId, "Route", 2, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                    break;
                case ZoneRouteRole.SideRoute:
                    markerCount = 1;
                    positions = new Vector3[1];
                    positions[0] = CalculateDeterministicPosition(zoneId, "Route", 0, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                    break;
                case ZoneRouteRole.Gate:
                    markerCount = 1;
                    positions = new Vector3[1];
                    positions[0] = CalculateDeterministicPosition(zoneId, "Route", 0, zoneCenter, zoneMin, zoneMax, terrainPatchBounds);
                    break;
                case ZoneRouteRole.FreeExploration:
                default:
                    return 0;
            }

            string ruleSummary = rule != null ? rule.debugSummary : "";
            string planSummary = plan != null ? plan.debugSummary : "";
            string[] routeTags = new[] { routeRole.ToString() };

            if (positions == null || markerCount == 0) return 0;

            for (int i = 0; i < markerCount && i < positions.Length; i++)
            {
                string markerName = $"RouteMarker_{zoneId}_{i}";
                Vector3 worldPoint = positions[i];
                GameObject marker = CreateMarkerGameObjectLocal(markerName, parentRoot, zoneRootTransform, worldPoint);
                if (settings.CreateVisibleContentDebugMarkers)
                {
                    // Route marker root는 항상 localScale = Vector3.one 유지
                    // 시각적 크기는 Debug child primitive의 localScale로만 표현
                    Vector3 routeVisualScale = new Vector3(2f, 0.1f, 2f);
                    CreateDebugPrimitive(marker, PrimitiveType.Cylinder, new Color(0.2f, 0.6f, 0.8f, 0.5f), "Route", routeVisualScale);
                }
                // Phase 14.7: Attach metadata (Route markers are not runtime spawn points)
                AttachContentMarker(marker, zoneId, markerName, WorldMapZoneContentMarkerType.Route, routeTags, ruleSummary, planSummary, false, false);
                totalMarkers++;
            }
            return totalMarkers;
        }

        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogZoneContentPlaceholderVerbose)
                Debug.Log($"[ZoneContentPlaceholder] {message}");
        }

        private static Bounds? FindTerrainPatchBounds(Transform zoneRootTransform, string zoneId)
        {
            Transform terrainPatch = zoneRootTransform.Find("TerrainPatch");
            if (terrainPatch == null) terrainPatch = zoneRootTransform.Find($"TerrainPatch_{zoneId}");
            if (terrainPatch == null)
            {
                foreach (Transform child in zoneRootTransform)
                {
                    if (child.name.Contains("TerrainPatch")) { terrainPatch = child; break; }
                }
            }
            if (terrainPatch == null) return null;
            MeshRenderer renderer = terrainPatch.GetComponent<MeshRenderer>();
            return renderer != null ? renderer.bounds : (Bounds?)null;
        }

        private static Transform CreateOrReplaceContentRoot(Transform zoneRootTransform, string zoneId)
        {
            Transform existing = zoneRootTransform.Find(ContentRootName);
            if (existing != null) GameObject.DestroyImmediate(existing.gameObject);
            GameObject contentRoot = new GameObject(ContentRootName);
            contentRoot.transform.SetParent(zoneRootTransform, false);
            contentRoot.transform.localPosition = Vector3.zero;
            contentRoot.transform.localRotation = Quaternion.identity;
            contentRoot.transform.localScale = Vector3.one;
            return contentRoot.transform;
        }

        private static GameObject GetOrCreateChild(GameObject parent, string childName)
        {
            Transform existing = parent.transform.Find(childName);
            if (existing != null) return existing.gameObject;
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        /// <summary>
        /// Marker를 ZoneRoot 하위 local 좌표계로 생성한다.
        /// worldPoint는 TerrainPatch에서 샘플한 월드 좌표이며,
        /// zoneRootTransform 기준 InverseTransformPoint로 localPosition을 계산한다.
        /// </summary>
        private static GameObject CreateMarkerGameObjectLocal(string name, GameObject parent, Transform zoneRootTransform, Vector3 worldPoint)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent.transform, false);
            // worldPoint를 zoneRoot 기준 local 좌표로 변환
            Vector3 localPoint = zoneRootTransform.InverseTransformPoint(worldPoint);
            marker.transform.localPosition = localPoint;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;

            // 디버그 로그: marker 이름과 world/local 좌표 출력
            string zoneId = ExtractZoneIdFromMarkerName(name);
            Debug.Log($"[DEBUG] {name} world=({worldPoint.x:F2}, {worldPoint.y:F2}, {worldPoint.z:F2}) local=({localPoint.x:F2}, {localPoint.y:F2}, {localPoint.z:F2}) parent={parent.name} zone={zoneId}");

            return marker;
        }

        /// <summary>
        /// Marker 이름에서 zoneId를 추출한다.
        /// 예: ResourceSpawn_A1_0_iron -> A1
        /// </summary>
        private static string ExtractZoneIdFromMarkerName(string markerName)
        {
            // markerName 형식: {Type}_{ZoneId}_{Index}_{Tag} 또는 {Type}_{ZoneId}_{Index}
            // zoneId는 두 번째 언더스코어 구간 (A1, B3, C10 등)
            string[] parts = markerName.Split('_');
            if (parts.Length >= 2)
            {
                // parts[1]이 zoneId여야 함 (예: ResourceSpawn_A1_0_iron -> parts[1] = "A1")
                string candidate = parts[1];
                if (candidate.Length >= 2 && candidate.Length <= 3 &&
                    char.IsLetter(candidate[0]) && char.IsDigit(candidate[1]))
                    return candidate;
            }
            return "??";
        }

        /// <summary>
        /// URP 호환 material을 사용하여 Debug primitive를 생성한다.
        /// Collider는 DestroyImmediate로 완전히 제거한다.
        /// visualScale을 지정하면 child primitive의 localScale을 해당 값으로 설정한다.
        /// marker root는 항상 localScale = Vector3.one을 유지하며,
        /// 시각적 크기 조절은 이 child primitive의 localScale로만 처리한다.
        /// </summary>
        private static void CreateDebugPrimitive(GameObject parent, PrimitiveType type, Color color, string category, Vector3? visualScale = null)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = $"Debug_{type}";
            primitive.transform.SetParent(parent.transform, false);
            primitive.transform.localPosition = Vector3.zero;
            primitive.transform.localRotation = Quaternion.identity;
            // visualScale이 지정되면 해당 값 사용, 없으면 기본 (0.5, 0.5, 0.5)
            primitive.transform.localScale = visualScale.HasValue ? visualScale.Value : (Vector3.one * 0.5f);

            // Collider 완전 제거 (disabled가 아니라 DestroyImmediate)
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                GameObject.DestroyImmediate(collider);
            }

            // URP material 적용
            MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material urpMat = GetOrCreateDebugMaterial(category, color);
                if (urpMat != null)
                {
                    renderer.sharedMaterial = urpMat;

                    // 적용 후 검증
                    string validationReason;
                    if (IsMaterialPinkOrInvalid(urpMat, out validationReason))
                    {
                        Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Pink/unsupported material detected! " +
                            $"Path: {GetGameObjectPath(primitive.transform)}, " +
                            $"Material: {urpMat.name}, Shader: {urpMat.shader?.name ?? "null"}, Reason: {validationReason}");
                    }
                }
                else
                {
                    // URP material 생성 실패 -> renderer 비활성화
                    renderer.enabled = false;
                    Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Cannot create URP material for {category}. " +
                        $"Path: {GetGameObjectPath(primitive.transform)}. Renderer disabled.");
                }
            }
        }

        /// <summary>
        /// GameObject의 전체 하이라키 경로를 반환한다.
        /// </summary>
        private static string GetGameObjectPath(Transform transform)
        {
            if (transform == null) return "null";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            Transform current = transform;
            while (current != null)
            {
                if (sb.Length > 0) sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }
            return sb.ToString();
        }

        private static Vector3 CalculateDeterministicPosition(string zoneId, string category, int index,
            Vector3 zoneCenter, Vector3 zoneMin, Vector3 zoneMax, Bounds? terrainPatchBounds)
        {
            string hashInput = $"{zoneId}_{category}_{index}";
            int hash = hashInput.GetHashCode();
            float tX = (float)((hash & 0xFFFF) % 10000) / 10000f;
            float tZ = (float)(((hash >> 16) & 0xFFFF) % 10000) / 10000f;
            float x = Mathf.Lerp(zoneMin.x, zoneMax.x, tX);
            float z = Mathf.Lerp(zoneMin.z, zoneMax.z, tZ);
            float y = terrainPatchBounds.HasValue ? terrainPatchBounds.Value.max.y + 0.5f : zoneCenter.y + 1f;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Content 하위 모든 자식을 검사하여 pink material, enabled collider, local transform 문제를 찾는다.
        /// </summary>
        private static void CheckAllChildrenForIssues(Transform contentRoot, Transform zoneRootTransform,
            ref int pinkMaterialCount, ref int enabledColliderCount, ref int markerOutsideBoundsCount,
            ref int invalidLocalScaleCount, ref int invalidLocalRotationCount, ref int invalidLocalPositionCount,
            ref int debugChildNotAtOriginCount)
        {
            foreach (Transform child in contentRoot)
                CheckChildForIssues(child, zoneRootTransform,
                    ref pinkMaterialCount, ref enabledColliderCount, ref markerOutsideBoundsCount,
                    ref invalidLocalScaleCount, ref invalidLocalRotationCount, ref invalidLocalPositionCount,
                    ref debugChildNotAtOriginCount);
        }

        /// <summary>
        /// 단일 Transform과 그 자식들을 재귀적으로 검사한다.
        /// </summary>
        private static void CheckChildForIssues(Transform target, Transform zoneRootTransform,
            ref int pinkMaterialCount, ref int enabledColliderCount, ref int markerOutsideBoundsCount,
            ref int invalidLocalScaleCount, ref int invalidLocalRotationCount, ref int invalidLocalPositionCount,
            ref int debugChildNotAtOriginCount)
        {
            string path = GetGameObjectPath(target);

            // 1. Pink/unsupported material 검사
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                string reason;
                if (IsMaterialPinkOrInvalid(renderer.sharedMaterial, out reason))
                {
                    pinkMaterialCount++;
                    Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Pink/unsupported material: {path}, " +
                        $"Material: {renderer.sharedMaterial.name}, Shader: {renderer.sharedMaterial.shader?.name ?? "null"}, Reason: {reason}");
                }
            }

            // 2. Enabled collider 검사
            Collider collider = target.GetComponent<Collider>();
            if (collider != null && collider.enabled)
            {
                enabledColliderCount++;
                Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Enabled collider: {path}");
            }

            // 3. Marker transform 검증 (SpawnRoot 직계 자식만 검사)
            //    SpawnRoot 이름 패턴: ResourceSpawns, HazardSpawns, LandmarkSpawns, NarrativeSpawns, RouteMarkers
            if (target.parent != null && IsSpawnRoot(target.parent.name))
            {
                // localScale == Vector3.one 검사
                if (target.localScale != Vector3.one)
                {
                    invalidLocalScaleCount++;
                    Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Marker localScale != Vector3.one: {path}, localScale={target.localScale}");
                }

                // localRotation == identity 검사
                if (target.localRotation != Quaternion.identity)
                {
                    invalidLocalRotationCount++;
                    Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Marker localRotation != identity: {path}, localRotation={target.localRotation}");
                }

                // localPosition이 zone 예상 범위를 벗어나는지 검사
                // zoneRootTransform 기준 world position 계산
                if (zoneRootTransform != null)
                {
                    Vector3 worldPos = target.position;
                    Vector3 localPos = target.localPosition;

                    // localPosition이 비정상적으로 큰 값인지 검사 (patchSize * 2 = 800)
                    float patchSize = 400f; // config.ZoneSize
                    if (Mathf.Abs(localPos.x) > patchSize * 2 || Mathf.Abs(localPos.z) > patchSize * 2)
                    {
                        invalidLocalPositionCount++;
                        Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Marker local position outside expected range: {path}, " +
                            $"local=({localPos.x:F2}, {localPos.y:F2}, {localPos.z:F2}), world=({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})");
                    }
                }
            }

            // 4. Debug primitive child가 local origin에 있는지 검사
            //    Debug_Sphere, Debug_Cylinder, Debug_Capsule 등의 이름 패턴
            if (target.name.StartsWith("Debug_"))
            {
                if (target.localPosition != Vector3.zero)
                {
                    debugChildNotAtOriginCount++;
                    Debug.LogError($"[ZoneContentPlaceholder] [FAIL] Debug primitive not at local origin: {path}, localPosition={target.localPosition}");
                }
            }

            // 5. 재귀적으로 자식 검사
            foreach (Transform child in target)
                CheckChildForIssues(child, zoneRootTransform,
                    ref pinkMaterialCount, ref enabledColliderCount, ref markerOutsideBoundsCount,
                    ref invalidLocalScaleCount, ref invalidLocalRotationCount, ref invalidLocalPositionCount,
                    ref debugChildNotAtOriginCount);
        }

        /// <summary>
        /// 주어진 이름이 SpawnRoot 이름인지 확인한다.
        /// </summary>
        private static bool IsSpawnRoot(string name)
        {
            return name == ResourceSpawnsName ||
                   name == HazardSpawnsName ||
                   name == LandmarkSpawnsName ||
                   name == NarrativeSpawnsName ||
                   name == RouteMarkersName;
        }
    }
}
