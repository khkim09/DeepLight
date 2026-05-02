using System;
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
    /// Phase 14.10-C-1: A~J 전체 100개 ZoneRoot의 Content 하위에
    /// Resource/Hazard/Landmark/Narrative/Route marker를 자동 생성하는 유틸리티.
    /// 기존 prototype 17개 zone은 건드리지 않고, non-prototype 83개 zone에 대해서만
    /// deterministic pseudo-random 위치로 marker root + WorldMapZoneContentMarker metadata를 생성한다.
    /// Debug primitive visual child는 생성하지 않는다 (다음 phase에서 처리).
    /// </summary>
    public static class DeepLightMapFullContentMarkerGenerationUtility
    {
        // ===== Constants =====
        private const string ContentRootName = "Content";
        private const string ResourceSpawnsName = "ResourceSpawns";
        private const string HazardSpawnsName = "HazardSpawns";
        private const string LandmarkSpawnsName = "LandmarkSpawns";
        private const string NarrativeSpawnsName = "NarrativeSpawns";
        private const string RouteMarkersName = "RouteMarkers";
        private const string DebugMarkersName = "DebugMarkers";

        /// <summary>전체 Zone 열 개수 (A~J = 10열)</summary>
        private const int TotalColumns = 10;

        /// <summary>전체 Zone 행 개수 (1~10 = 10행)</summary>
        private const int TotalRows = 10;

        /// <summary>전체 Zone 개수 (10x10 = 100)</summary>
        private const int TotalZoneCount = 100;

        /// <summary>Zone local bounds 반폭 (zoneSize=400 기준, -180~180 범위로 제한)</summary>
        private const float ZoneLocalHalfExtent = 180f;

        /// <summary>marker localPosition 기본 Y값</summary>
        private const float DefaultMarkerLocalY = 1f;

        /// <summary>
        /// Prototype 17개 zone: Phase 14.8 override가 관리하므로 이번 full marker 생성에서 스킵한다.
        /// Hub: E5,F5,E6,F6
        /// Harbor: D5,D6,E4,F4,G5,G6,E7,F7
        /// Wreck: B5,C5,B6,C6,C7
        /// </summary>
        private static readonly HashSet<string> PrototypeZoneIds = new HashSet<string>
        {
            // Hub (4)
            "E5", "F5", "E6", "F6",
            // Harbor (8)
            "D5", "D6", "E4", "F4", "G5", "G6", "E7", "F7",
            // Wreck (5)
            "B5", "C5", "B6", "C6", "C7"
        };

        /// <summary>Content 하위 6개 child root 이름 목록</summary>
        private static readonly string[] ContentChildRootNames = new[]
        {
            ResourceSpawnsName,
            HazardSpawnsName,
            LandmarkSpawnsName,
            NarrativeSpawnsName,
            RouteMarkersName,
            DebugMarkersName
        };

        // ===== Public API =====

        /// <summary>
        /// A~J 전체 100개 ZoneRoot의 Content 하위에 marker를 재구축한다.
        /// prototype 17개 zone은 스킵하고, non-prototype 83개 zone에 대해서만
        /// 기존 marker 자식을 삭제 후 deterministic 위치에 재생성한다.
        /// </summary>
        public static void RebuildFullContentMarkers(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // 1. settings/context null check
            if (settings == null)
            {
                Debug.LogError("[FullContentMarker] Settings is null! Cannot rebuild full content markers.");
                return;
            }
            if (context == null)
            {
                Debug.LogError("[FullContentMarker] Context is null! Cannot rebuild full content markers.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-C-1: Rebuild Full Content Markers (A1~J10) =====");
            log.AppendLine($"  Prototype zones skipped: {PrototypeZoneIds.Count}");
            log.AppendLine($"  Non-prototype zones to process: {TotalZoneCount - PrototypeZoneIds.Count}");

            // 2. GeneratedWorldRoot와 ZoneRoots parent 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[FullContentMarker] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[FullContentMarker] '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // Database references
            WorldMapConfigSO config = settings.WorldMapConfig;
            WorldMapZoneDesignDatabaseSO designDb = settings.ZoneDesignDatabase;
            WorldMapZoneDesignRuleDatabaseSO ruleDb = settings.ZoneDesignRuleDatabase;
            WorldMapZoneTerrainPlanDatabaseSO planDb = settings.ZoneTerrainPlanDatabase;

            if (config == null)
            {
                Debug.LogError("[FullContentMarker] WorldMapConfig is null in settings.");
                return;
            }
            if (designDb == null)
            {
                Debug.LogError("[FullContentMarker] ZoneDesignDatabase is null in settings.");
                return;
            }
            if (ruleDb == null)
            {
                Debug.LogError("[FullContentMarker] ZoneDesignRuleDatabase is null in settings.");
                return;
            }
            if (planDb == null)
            {
                Debug.LogError("[FullContentMarker] ZoneTerrainPlanDatabase is null in settings.");
                return;
            }

            // 3. A1~J10 총 100개 zoneId 순회
            int totalZonesProcessed = 0;
            int totalZonesSkippedPrototype = 0;
            int totalZonesSkippedMissing = 0;
            int totalMarkersCreated = 0;
            int totalResourceMarkers = 0;
            int totalHazardMarkers = 0;
            int totalLandmarkMarkers = 0;
            int totalNarrativeMarkers = 0;
            int totalRouteMarkers = 0;

            ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
            {
                // 6. prototype 17개 zone 스킵
                if (PrototypeZoneIds.Contains(zoneIdStr))
                {
                    totalZonesSkippedPrototype++;
                    LogIfVerbose(settings, $"[SKIP] {zoneIdStr} is prototype zone. Skipped.");
                    return;
                }

                string zoneRootName = $"ZoneRoot_{zoneIdStr}";
                Transform zoneRootTransform = zoneRootsTransform.Find(zoneRootName);
                if (zoneRootTransform == null)
                {
                    totalZonesSkippedMissing++;
                    LogIfVerbose(settings, $"[SKIP] {zoneRootName} not found.");
                    return;
                }

                // 5. 각 zone의 database entry/rule/plan 찾기
                WorldMapZoneDesignEntry entry = designDb.GetEntry(zoneIdStr);
                if (entry == null)
                {
                    LogIfVerbose(settings, $"[SKIP] No design entry for {zoneIdStr}.");
                    return;
                }

                WorldMapZoneDesignRule rule = ruleDb.GetRule(zoneIdStr);
                if (rule == null)
                {
                    LogIfVerbose(settings, $"[SKIP] No design rule for {zoneIdStr}.");
                    return;
                }

                WorldMapZoneTerrainPlan plan = planDb.GetPlan(zoneIdStr);
                if (plan == null)
                {
                    LogIfVerbose(settings, $"[SKIP] No terrain plan for {zoneIdStr}.");
                    return;
                }

                // 4. Content root와 6개 child root 존재 확인/생성
                Transform contentTransform = zoneRootTransform.Find(ContentRootName);
                if (contentTransform == null)
                {
                    // Content root가 없으면 생성
                    GameObject contentRoot = new GameObject(ContentRootName);
                    contentRoot.transform.SetParent(zoneRootTransform, false);
                    contentRoot.transform.localPosition = Vector3.zero;
                    contentRoot.transform.localRotation = Quaternion.identity;
                    contentRoot.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(contentRoot, $"Create {ContentRootName} under {zoneRootName}");
                    contentTransform = contentRoot.transform;
                    LogIfVerbose(settings, $"[CREATE] {zoneRootName}/{ContentRootName}");
                }

                // 6개 child root 보장
                foreach (string childName in ContentChildRootNames)
                {
                    Transform childTransform = contentTransform.Find(childName);
                    if (childTransform == null)
                    {
                        GameObject childGo = new GameObject(childName);
                        childGo.transform.SetParent(contentTransform, false);
                        childGo.transform.localPosition = Vector3.zero;
                        childGo.transform.localRotation = Quaternion.identity;
                        childGo.transform.localScale = Vector3.one;
                        Undo.RegisterCreatedObjectUndo(childGo, $"Create {childName} under {zoneRootName}/{ContentRootName}");
                    }
                }

                // 7. 기존 marker 자식 삭제 후 재생성
                // 각 spawn root의 기존 자식을 모두 삭제
                Transform resourceRoot = contentTransform.Find(ResourceSpawnsName);
                Transform hazardRoot = contentTransform.Find(HazardSpawnsName);
                Transform landmarkRoot = contentTransform.Find(LandmarkSpawnsName);
                Transform narrativeRoot = contentTransform.Find(NarrativeSpawnsName);
                Transform routeRoot = contentTransform.Find(RouteMarkersName);

                // 기존 자식 삭제
                DestroyAllChildrenImmediate(resourceRoot);
                DestroyAllChildrenImmediate(hazardRoot);
                DestroyAllChildrenImmediate(landmarkRoot);
                DestroyAllChildrenImmediate(narrativeRoot);
                DestroyAllChildrenImmediate(routeRoot);

                // Zone coordinate 계산 (local bounds용)
                ZoneCoordinate coord = new ZoneCoordinate(colIndex, rowIndex);
                Vector3 zoneCenter = coord.GetZoneCenterWorldPosition(config);
                var (zoneMin, zoneMax) = coord.GetZoneBounds(config);

                // 17. marker 개수 산정 및 생성
                int rCount = CalculateResourceMarkerCount(rule, entry);
                int hCount = CalculateHazardMarkerCount(rule, entry);
                int lCount = CalculateLandmarkMarkerCount(rule, entry);
                int nCount = CalculateNarrativeMarkerCount(entry);
                int rtCount = CalculateRouteMarkerCount(rule);

                // 18. intentionallySparse zone은 marker 수를 줄인다
                if (entry.intentionallySparse)
                {
                    rCount = Mathf.Min(rCount, 1);
                    hCount = Mathf.Min(hCount, 1);
                    lCount = Mathf.Min(lCount, 1);
                    nCount = Mathf.Min(nCount, 1);
                    rtCount = Mathf.Min(rtCount, 1);
                }

                // Resource markers
                string[] resourceTags = GetResourceTags(rule, entry);
                string resourceRuleSummary = BuildResourceRuleSummary(rule);
                string resourcePlanSummary = BuildResourcePlanSummary(plan);
                for (int i = 0; i < rCount; i++)
                {
                    string tag = (resourceTags != null && resourceTags.Length > 0)
                        ? resourceTags[i % resourceTags.Length]
                        : "Generic";
                    string markerName = $"ResourceSpawn_{zoneIdStr}_{i}_{tag}";
                    Vector3 localPos = CalculateDeterministicLocalPosition(zoneIdStr, "Resource", i, zoneRootTransform, zoneCenter, zoneMin, zoneMax);
                    GameObject marker = CreateMarkerRoot(markerName, resourceRoot, localPos);
                    AttachContentMarker(marker, zoneIdStr, markerName,
                        WorldMapZoneContentMarkerType.Resource, resourceTags,
                        resourceRuleSummary, resourcePlanSummary,
                        isRuntimeSpawnPoint: true, isDebugOnly: false);
                    totalResourceMarkers++;
                }

                // Hazard markers
                string[] hazardTags = GetHazardTags(rule, entry);
                string hazardRuleSummary = BuildHazardRuleSummary(rule);
                string hazardPlanSummary = BuildHazardPlanSummary(plan);
                for (int i = 0; i < hCount; i++)
                {
                    string tag = (hazardTags != null && hazardTags.Length > 0)
                        ? hazardTags[i % hazardTags.Length]
                        : "Generic";
                    string markerName = $"HazardSpawn_{zoneIdStr}_{i}_{tag}";
                    Vector3 localPos = CalculateDeterministicLocalPosition(zoneIdStr, "Hazard", i, zoneRootTransform, zoneCenter, zoneMin, zoneMax);
                    GameObject marker = CreateMarkerRoot(markerName, hazardRoot, localPos);
                    AttachContentMarker(marker, zoneIdStr, markerName,
                        WorldMapZoneContentMarkerType.Hazard, hazardTags,
                        hazardRuleSummary, hazardPlanSummary,
                        isRuntimeSpawnPoint: true, isDebugOnly: false);
                    totalHazardMarkers++;
                }

                // Landmark markers
                string[] landmarkTags = GetLandmarkTags(rule, entry);
                string landmarkRuleSummary = BuildLandmarkRuleSummary(rule);
                string landmarkPlanSummary = BuildLandmarkPlanSummary(plan);
                for (int i = 0; i < lCount; i++)
                {
                    string tag = (landmarkTags != null && landmarkTags.Length > 0)
                        ? landmarkTags[i % landmarkTags.Length]
                        : rule.landmarkRole.ToString();
                    string markerName = $"LandmarkSpawn_{zoneIdStr}_{i}_{tag}";
                    Vector3 localPos = CalculateDeterministicLocalPosition(zoneIdStr, "Landmark", i, zoneRootTransform, zoneCenter, zoneMin, zoneMax);
                    GameObject marker = CreateMarkerRoot(markerName, landmarkRoot, localPos);
                    AttachContentMarker(marker, zoneIdStr, markerName,
                        WorldMapZoneContentMarkerType.Landmark, landmarkTags,
                        landmarkRuleSummary, landmarkPlanSummary,
                        isRuntimeSpawnPoint: true, isDebugOnly: false);
                    totalLandmarkMarkers++;
                }

                // Narrative markers
                string[] narrativeTags = GetNarrativeTags(entry);
                string narrativeRuleSummary = BuildNarrativeRuleSummary(rule);
                string narrativePlanSummary = BuildNarrativePlanSummary(plan);
                for (int i = 0; i < nCount; i++)
                {
                    string markerName = $"NarrativeSpawn_{zoneIdStr}_{i}";
                    Vector3 localPos = CalculateDeterministicLocalPosition(zoneIdStr, "Narrative", i, zoneRootTransform, zoneCenter, zoneMin, zoneMax);
                    GameObject marker = CreateMarkerRoot(markerName, narrativeRoot, localPos);
                    AttachContentMarker(marker, zoneIdStr, markerName,
                        WorldMapZoneContentMarkerType.Narrative, narrativeTags,
                        narrativeRuleSummary, narrativePlanSummary,
                        isRuntimeSpawnPoint: true, isDebugOnly: false);
                    totalNarrativeMarkers++;
                }

                // Route markers
                string[] routeTags = GetRouteTags(rule);
                string routeRuleSummary = BuildRouteRuleSummary(rule);
                string routePlanSummary = BuildRoutePlanSummary(plan);
                for (int i = 0; i < rtCount; i++)
                {
                    string markerName = $"RouteMarker_{zoneIdStr}_{i}";
                    Vector3 localPos = CalculateDeterministicLocalPosition(zoneIdStr, "Route", i, zoneRootTransform, zoneCenter, zoneMin, zoneMax);
                    GameObject marker = CreateMarkerRoot(markerName, routeRoot, localPos);
                    AttachContentMarker(marker, zoneIdStr, markerName,
                        WorldMapZoneContentMarkerType.Route, routeTags,
                        routeRuleSummary, routePlanSummary,
                        isRuntimeSpawnPoint: false, isDebugOnly: false);
                    totalRouteMarkers++;
                }

                totalZonesProcessed++;
                LogIfVerbose(settings, $"  [OK] {zoneIdStr}: R={rCount} H={hCount} L={lCount} N={nCount} Rt={rtCount}");
            });

            // 22. Registry가 있으면 RebuildCache() 호출
            WorldMapZoneContentRegistry registry = generatedRoot.GetComponent<WorldMapZoneContentRegistry>();
            if (registry != null)
            {
                registry.RebuildCache();
                log.AppendLine($"  [OK] WorldMapZoneContentRegistry cache rebuilt. Marker count: {registry.CachedMarkerCount}");
            }
            else
            {
                Debug.LogWarning("[FullContentMarker] WorldMapZoneContentRegistry not found on GeneratedWorldRoot. Cache not rebuilt.");
            }

            // 결과 요약
            totalMarkersCreated = totalResourceMarkers + totalHazardMarkers + totalLandmarkMarkers + totalNarrativeMarkers + totalRouteMarkers;
            log.AppendLine($"  Zones processed: {totalZonesProcessed}");
            log.AppendLine($"  Zones skipped (prototype): {totalZonesSkippedPrototype}");
            log.AppendLine($"  Zones skipped (missing): {totalZonesSkippedMissing}");
            log.AppendLine($"  Total markers created: {totalMarkersCreated}");
            log.AppendLine($"    Resource: {totalResourceMarkers}");
            log.AppendLine($"    Hazard: {totalHazardMarkers}");
            log.AppendLine($"    Landmark: {totalLandmarkMarkers}");
            log.AppendLine($"    Narrative: {totalNarrativeMarkers}");
            log.AppendLine($"    Route: {totalRouteMarkers}");
            log.AppendLine("===== Phase 14.10-C-1: Rebuild Full Content Markers Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// A~J 전체 100개 ZoneRoot의 Content marker 유효성을 검사한다.
        /// prototype 17개 zone은 validation 대상에서 제외하거나 preserved로 INFO 처리한다.
        /// </summary>
        public static void ValidateFullContentMarkers(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[FullContentMarker] Settings is null! Cannot validate.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-C-1: Validate Full Content Markers (A1~J10) =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot 존재 PASS/FAIL
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null)
            {
                log.AppendLine("  [PASS] GeneratedWorldRoot exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] GeneratedWorldRoot not found!");
                failCount++;
            }

            // 2. ZoneRoots parent 존재 PASS/FAIL
            Transform zoneRootsTransform = null;
            if (generatedRoot != null)
            {
                zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
                if (zoneRootsTransform != null)
                {
                    log.AppendLine("  [PASS] ZoneRoots parent exists.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [FAIL] ZoneRoots parent not found!");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [FAIL] Cannot check ZoneRoots parent (GeneratedWorldRoot missing).");
                failCount++;
            }

            // 3. A~J 100개 ZoneRoot 존재 PASS/FAIL
            int zoneRootFound = 0;
            if (zoneRootsTransform != null)
            {
                ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
                {
                    string zrName = $"ZoneRoot_{zoneIdStr}";
                    if (zoneRootsTransform.Find(zrName) != null)
                    {
                        zoneRootFound++;
                    }
                });
            }

            if (zoneRootFound == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} ZoneRoot_A1~J10 exist.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] ZoneRoot_A1~J10 found: {zoneRootFound}/{TotalZoneCount}");
                failCount++;
            }

            if (zoneRootsTransform == null)
            {
                log.AppendLine("  [FAIL] Cannot continue validation (ZoneRoots parent missing).");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-C-1: Validate Full Content Markers Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4. prototype 17개 zone은 marker validation 대상에서 제외하거나 preserved로 INFO 처리
            int prototypeZonesPreserved = 0;
            int nonPrototypeZonesWithContent = 0;
            int nonPrototypeZonesWithMarkers = 0;
            int nonPrototypeSparseZonesWithNoMarkers = 0;
            int nonPrototypeNonSparseZonesWithNoMarkers = 0;

            // 5-12. 상세 검사 카운터
            int markerComponentMissing = 0;
            int zoneIdMismatch = 0;
            int emptyMarkerId = 0;
            int duplicateMarkerId = 0;
            int invalidLocalScale = 0;
            int invalidLocalRotation = 0;
            int markerOutsideLocalBounds = 0;

            HashSet<string> allMarkerIds = new HashSet<string>();
            int totalMarkerComponentCount = 0;

            ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
            {
                string zrName = $"ZoneRoot_{zoneIdStr}";
                Transform zr = zoneRootsTransform.Find(zrName);
                if (zr == null) return;

                Transform contentT = zr.Find(ContentRootName);
                if (contentT == null) return;

                bool isPrototype = PrototypeZoneIds.Contains(zoneIdStr);

                if (isPrototype)
                {
                    prototypeZonesPreserved++;
                    // prototype zone은 Content root 존재만 확인하고 marker 검사는 하지 않음
                    log.AppendLine($"  [INFO] Prototype zone {zoneIdStr}: Content root preserved. Marker validation skipped.");
                    warnCount++;
                    return;
                }

                // non-prototype zone
                nonPrototypeZonesWithContent++;

                // 각 spawn root의 marker 검사
                int zoneMarkerCount = 0;

                zoneMarkerCount += ValidateSpawnRootMarkers(contentT, ResourceSpawnsName, zoneIdStr,
                    ref markerComponentMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                    ref invalidLocalScale, ref invalidLocalRotation, ref markerOutsideLocalBounds,
                    allMarkerIds, zr, ref totalMarkerComponentCount);

                zoneMarkerCount += ValidateSpawnRootMarkers(contentT, HazardSpawnsName, zoneIdStr,
                    ref markerComponentMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                    ref invalidLocalScale, ref invalidLocalRotation, ref markerOutsideLocalBounds,
                    allMarkerIds, zr, ref totalMarkerComponentCount);

                zoneMarkerCount += ValidateSpawnRootMarkers(contentT, LandmarkSpawnsName, zoneIdStr,
                    ref markerComponentMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                    ref invalidLocalScale, ref invalidLocalRotation, ref markerOutsideLocalBounds,
                    allMarkerIds, zr, ref totalMarkerComponentCount);

                zoneMarkerCount += ValidateSpawnRootMarkers(contentT, NarrativeSpawnsName, zoneIdStr,
                    ref markerComponentMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                    ref invalidLocalScale, ref invalidLocalRotation, ref markerOutsideLocalBounds,
                    allMarkerIds, zr, ref totalMarkerComponentCount);

                zoneMarkerCount += ValidateSpawnRootMarkers(contentT, RouteMarkersName, zoneIdStr,
                    ref markerComponentMissing, ref zoneIdMismatch, ref emptyMarkerId, ref duplicateMarkerId,
                    ref invalidLocalScale, ref invalidLocalRotation, ref markerOutsideLocalBounds,
                    allMarkerIds, zr, ref totalMarkerComponentCount);

                if (zoneMarkerCount > 0)
                {
                    nonPrototypeZonesWithMarkers++;
                }

                // 6. intentionallySparse가 아닌 곳은 최소 1개 이상 marker가 있어야 한다
                WorldMapZoneDesignEntry entry = settings.ZoneDesignDatabase?.GetEntry(zoneIdStr);
                if (entry != null && !entry.intentionallySparse && zoneMarkerCount == 0)
                {
                    nonPrototypeNonSparseZonesWithNoMarkers++;
                }
                else if (entry != null && entry.intentionallySparse && zoneMarkerCount == 0)
                {
                    nonPrototypeSparseZonesWithNoMarkers++;
                }
            });

            // 4. Prototype zone INFO
            if (prototypeZonesPreserved > 0)
            {
                log.AppendLine($"  [INFO] {prototypeZonesPreserved} prototype zones preserved (marker validation skipped).");
                warnCount++;
            }

            // 5. Non-prototype zone Content root 존재
            int expectedNonPrototypeZones = TotalZoneCount - PrototypeZoneIds.Count;
            if (nonPrototypeZonesWithContent == expectedNonPrototypeZones)
            {
                log.AppendLine($"  [PASS] All {expectedNonPrototypeZones} non-prototype zones have Content root.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Non-prototype zones with Content root: {nonPrototypeZonesWithContent}/{expectedNonPrototypeZones}");
                failCount++;
            }

            // 6. Non-prototype non-sparse zones have at least 1 marker
            if (nonPrototypeNonSparseZonesWithNoMarkers == 0)
            {
                log.AppendLine("  [PASS] All non-prototype, non-sparse zones have at least 1 marker.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {nonPrototypeNonSparseZonesWithNoMarkers} non-prototype, non-sparse zone(s) have 0 markers!");
                failCount++;
            }

            // 7. 모든 marker에 WorldMapZoneContentMarker가 있어야 한다
            if (markerComponentMissing == 0)
            {
                log.AppendLine("  [PASS] All markers have WorldMapZoneContentMarker component.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {markerComponentMissing} marker(s) missing WorldMapZoneContentMarker!");
                failCount++;
            }

            // 8. 모든 marker의 ZoneId가 parent ZoneRoot id와 일치해야 한다
            if (zoneIdMismatch == 0)
            {
                log.AppendLine("  [PASS] All marker ZoneId values match parent ZoneRoot id.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {zoneIdMismatch} marker(s) have ZoneId mismatch!");
                failCount++;
            }

            // 9. 모든 markerId가 non-empty이고 전체에서 unique 해야 한다
            if (emptyMarkerId == 0)
            {
                log.AppendLine("  [PASS] All marker MarkerId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyMarkerId} marker(s) have empty MarkerId!");
                failCount++;
            }
            if (duplicateMarkerId == 0)
            {
                log.AppendLine("  [PASS] All marker MarkerId values are unique.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {duplicateMarkerId} duplicate MarkerId(s) found!");
                failCount++;
            }

            // 10. 모든 marker root localScale == Vector3.one
            if (invalidLocalScale == 0)
            {
                log.AppendLine("  [PASS] All marker root localScale == Vector3.one.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {invalidLocalScale} marker(s) have non-one localScale.");
                failCount++;
            }

            // 11. 모든 marker root localRotation == Quaternion.identity
            if (invalidLocalRotation == 0)
            {
                log.AppendLine("  [PASS] All marker root localRotation == Quaternion.identity.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {invalidLocalRotation} marker(s) have non-identity localRotation.");
                failCount++;
            }

            // 12. 모든 marker localPosition이 zone local bounds 안에 있어야 한다
            if (markerOutsideLocalBounds == 0)
            {
                log.AppendLine("  [PASS] All marker localPosition within zone local bounds.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {markerOutsideLocalBounds} marker(s) have localPosition outside expected bounds.");
                warnCount++;
            }

            // 13. Registry가 있으면 registry marker count >= generated marker component count인지 검사
            WorldMapZoneContentRegistry registry = generatedRoot != null
                ? generatedRoot.GetComponent<WorldMapZoneContentRegistry>()
                : null;
            if (registry != null)
            {
                registry.RebuildCache();
                int registryCount = registry.CachedMarkerCount;
                if (registryCount >= totalMarkerComponentCount)
                {
                    log.AppendLine($"  [PASS] Registry marker count ({registryCount}) >= component count ({totalMarkerComponentCount}).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Registry marker count ({registryCount}) < component count ({totalMarkerComponentCount})!");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [INFO] WorldMapZoneContentRegistry not found. Registry count check skipped.");
                warnCount++;
            }

            // 14. PASS/FAIL/WARN summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-C-1: Validate Full Content Markers Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Marker Count Calculation =====

        /// <summary>
        /// Resource marker 개수 산정: rule.resourceDensity01 기준 0~3개
        /// </summary>
        private static int CalculateResourceMarkerCount(WorldMapZoneDesignRule rule, WorldMapZoneDesignEntry entry)
        {
            if (rule == null) return 0;
            bool hasResourceTags = rule.resourceTags != null && rule.resourceTags.Length > 0;
            bool hasResourceGroups = entry != null && !string.IsNullOrEmpty(entry.resourceGroups);
            if (!hasResourceTags && !hasResourceGroups) return 0;

            float density = rule.resourceDensity01;
            // density 기준 0~3개
            int count = Mathf.RoundToInt(density * 3f);
            return Mathf.Clamp(count, 0, 3);
        }

        /// <summary>
        /// Hazard marker 개수 산정: rule.hazardDensity01 기준 0~2개
        /// </summary>
        private static int CalculateHazardMarkerCount(WorldMapZoneDesignRule rule, WorldMapZoneDesignEntry entry)
        {
            if (rule == null) return 0;
            bool hasHazardTags = rule.hazardTags != null && rule.hazardTags.Length > 0;
            bool hasHazards = entry != null && !string.IsNullOrEmpty(entry.hazards);
            if (!hasHazardTags && !hasHazards) return 0;

            float density = rule.hazardDensity01;
            int count = Mathf.RoundToInt(density * 2f);
            return Mathf.Clamp(count, 0, 2);
        }

        /// <summary>
        /// Landmark marker 개수 산정: rule.landmarkWeight01 기준 0~2개
        /// </summary>
        private static int CalculateLandmarkMarkerCount(WorldMapZoneDesignRule rule, WorldMapZoneDesignEntry entry)
        {
            if (rule == null) return 0;
            bool hasLandmarkTags = rule.landmarkTags != null && rule.landmarkTags.Length > 0;
            bool hasLandmarkRole = rule.landmarkRole != ZoneLandmarkRole.None;
            bool isMajorLandmark = entry != null && entry.isMajorLandmark;
            if (!hasLandmarkTags && !hasLandmarkRole && !isMajorLandmark) return 0;

            float weight = rule.landmarkWeight01;
            int count = Mathf.RoundToInt(weight * 2f);
            return Mathf.Clamp(count, 0, 2);
        }

        /// <summary>
        /// Narrative marker 개수 산정: entry.logOrHint가 비어있지 않으면 1개
        /// </summary>
        private static int CalculateNarrativeMarkerCount(WorldMapZoneDesignEntry entry)
        {
            if (entry == null) return 0;
            bool hasLogOrHint = !string.IsNullOrEmpty(entry.logOrHint);
            bool hasNarrativeFunction = !string.IsNullOrEmpty(entry.narrativeFunction);
            bool requiresNarrative = false; // rule에서 확인하지만 entry 기반으로도 판단
            if (!hasLogOrHint && !hasNarrativeFunction) return 0;
            return 1;
        }

        /// <summary>
        /// Route marker 개수 산정: routeRole이 None이 아니면 1~2개
        /// </summary>
        private static int CalculateRouteMarkerCount(WorldMapZoneDesignRule rule)
        {
            if (rule == null) return 0;
            switch (rule.routeRole)
            {
                case ZoneRouteRole.MainRoute:
                    return 2;
                case ZoneRouteRole.SideRoute:
                case ZoneRouteRole.Gate:
                case ZoneRouteRole.Boundary:
                case ZoneRouteRole.HubApproach:
                    return 1;
                case ZoneRouteRole.FreeExploration:
                default:
                    return 0;
            }
        }

        // ===== Tag Resolution =====

        /// <summary>
        /// Resource tags: rule.resourceTags 우선, 없으면 entry.resourceGroups split fallback
        /// </summary>
        private static string[] GetResourceTags(WorldMapZoneDesignRule rule, WorldMapZoneDesignEntry entry)
        {
            if (rule != null && rule.resourceTags != null && rule.resourceTags.Length > 0)
                return rule.resourceTags;

            if (entry != null && !string.IsNullOrEmpty(entry.resourceGroups))
            {
                string[] groups = entry.resourceGroups.Split(',');
                string[] tags = new string[groups.Length];
                for (int i = 0; i < groups.Length; i++)
                    tags[i] = groups[i].Trim();
                return tags;
            }

            return new[] { "Generic" };
        }

        /// <summary>
        /// Hazard tags: rule.hazardTags 우선, 없으면 entry.hazards split fallback
        /// </summary>
        private static string[] GetHazardTags(WorldMapZoneDesignRule rule, WorldMapZoneDesignEntry entry)
        {
            if (rule != null && rule.hazardTags != null && rule.hazardTags.Length > 0)
                return rule.hazardTags;

            if (entry != null && !string.IsNullOrEmpty(entry.hazards))
            {
                string[] hazardList = entry.hazards.Split(',');
                string[] tags = new string[hazardList.Length];
                for (int i = 0; i < hazardList.Length; i++)
                    tags[i] = hazardList[i].Trim();
                return tags;
            }

            return new[] { "Generic" };
        }

        /// <summary>
        /// Landmark tags: rule.landmarkTags 우선, 없으면 entry.keyObjects split fallback
        /// </summary>
        private static string[] GetLandmarkTags(WorldMapZoneDesignRule rule, WorldMapZoneDesignEntry entry)
        {
            if (rule != null && rule.landmarkTags != null && rule.landmarkTags.Length > 0)
                return rule.landmarkTags;

            if (entry != null && !string.IsNullOrEmpty(entry.keyObjects))
            {
                string[] objects = entry.keyObjects.Split(',');
                string[] tags = new string[objects.Length];
                for (int i = 0; i < objects.Length; i++)
                    tags[i] = objects[i].Trim();
                return tags;
            }

            return new[] { rule != null ? rule.landmarkRole.ToString() : "Generic" };
        }

        /// <summary>
        /// Narrative tags: entry.logOrHint 또는 entry.narrativeFunction 기반 tag
        /// </summary>
        private static string[] GetNarrativeTags(WorldMapZoneDesignEntry entry)
        {
            if (entry == null) return new[] { "Generic" };

            var tags = new List<string>();
            if (!string.IsNullOrEmpty(entry.logOrHint))
                tags.Add(entry.logOrHint.Length > 32 ? entry.logOrHint.Substring(0, 32) : entry.logOrHint);
            if (!string.IsNullOrEmpty(entry.narrativeFunction))
                tags.Add(entry.narrativeFunction);
            if (tags.Count == 0)
                tags.Add("Generic");
            return tags.ToArray();
        }

        /// <summary>
        /// Route tags: rule.routeRole.ToString()
        /// </summary>
        private static string[] GetRouteTags(WorldMapZoneDesignRule rule)
        {
            if (rule == null) return new[] { "None" };
            return new[] { rule.routeRole.ToString() };
        }

        // ===== Summary Builders =====

        private static string BuildResourceRuleSummary(WorldMapZoneDesignRule rule)
        {
            if (rule == null) return "No rule";
            return $"density={rule.resourceDensity01:F2} tags={string.Join(",", rule.resourceTags ?? new[] { "none" })}";
        }

        private static string BuildResourcePlanSummary(WorldMapZoneTerrainPlan plan)
        {
            if (plan == null) return "No plan";
            return $"surface={plan.baseSurfaceMode} seabed={plan.seabedShapeMode} depth={plan.targetCenterDepth}";
        }

        private static string BuildHazardRuleSummary(WorldMapZoneDesignRule rule)
        {
            if (rule == null) return "No rule";
            return $"density={rule.hazardDensity01:F2} tags={string.Join(",", rule.hazardTags ?? new[] { "none" })}";
        }

        private static string BuildHazardPlanSummary(WorldMapZoneTerrainPlan plan)
        {
            if (plan == null) return "No plan";
            return $"surface={plan.baseSurfaceMode} seabed={plan.seabedShapeMode} depth={plan.targetCenterDepth}";
        }

        private static string BuildLandmarkRuleSummary(WorldMapZoneDesignRule rule)
        {
            if (rule == null) return "No rule";
            return $"weight={rule.landmarkWeight01:F2} role={rule.landmarkRole} tags={string.Join(",", rule.landmarkTags ?? new[] { "none" })}";
        }

        private static string BuildLandmarkPlanSummary(WorldMapZoneTerrainPlan plan)
        {
            if (plan == null) return "No plan";
            return $"surface={plan.baseSurfaceMode} seabed={plan.seabedShapeMode} depth={plan.targetCenterDepth}";
        }

        private static string BuildNarrativeRuleSummary(WorldMapZoneDesignRule rule)
        {
            if (rule == null) return "No rule";
            return $"requiresNarrative={rule.requiresNarrativeObject}";
        }

        private static string BuildNarrativePlanSummary(WorldMapZoneTerrainPlan plan)
        {
            if (plan == null) return "No plan";
            return $"surface={plan.baseSurfaceMode} seabed={plan.seabedShapeMode} depth={plan.targetCenterDepth}";
        }

        private static string BuildRouteRuleSummary(WorldMapZoneDesignRule rule)
        {
            if (rule == null) return "No rule";
            return $"role={rule.routeRole}";
        }

        private static string BuildRoutePlanSummary(WorldMapZoneTerrainPlan plan)
        {
            if (plan == null) return "No plan";
            return $"surface={plan.baseSurfaceMode} seabed={plan.seabedShapeMode} depth={plan.targetCenterDepth}";
        }

        // ===== Deterministic Position Calculation =====

        /// <summary>
        /// zoneId/index/tag 기반 hash로 deterministic pseudo random local position 생성.
        /// UnityEngine.Random 전역 상태를 사용하지 않는다.
        /// x,z는 -180~180 범위 안쪽으로 제한, y는 1f.
        /// </summary>
        private static Vector3 CalculateDeterministicLocalPosition(
            string zoneId, string markerType, int index,
            Transform zoneRootTransform, Vector3 zoneCenter, Vector3 zoneMin, Vector3 zoneMax)
        {
            // zoneId + markerType + index 기반 hash 생성
            string hashInput = $"{zoneId}_{markerType}_{index}";
            int hash = hashInput.GetHashCode();

            // hash 기반 pseudo random x,z offset (-ZoneLocalHalfExtent ~ +ZoneLocalHalfExtent)
            float xOffset = (hash % 1000) / 1000f; // 0~1
            float zOffset = ((hash >> 8) % 1000) / 1000f; // 0~1

            // -180~180 범위로 매핑
            float x = Mathf.Lerp(-ZoneLocalHalfExtent, ZoneLocalHalfExtent, xOffset);
            float z = Mathf.Lerp(-ZoneLocalHalfExtent, ZoneLocalHalfExtent, zOffset);

            return new Vector3(x, DefaultMarkerLocalY, z);
        }

        // ===== GameObject Creation Helpers =====

        /// <summary>
        /// marker root GameObject를 생성하고 transform을 설정한다.
        /// localPosition = zone local position, localRotation = identity, localScale = one
        /// </summary>
        private static GameObject CreateMarkerRoot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(go, $"Create marker {name}");
            return go;
        }

        /// <summary>
        /// marker root에 WorldMapZoneContentMarker 컴포넌트를 붙이고 Configure()로 metadata를 설정한다.
        /// </summary>
        private static void AttachContentMarker(
            GameObject markerRoot,
            string zoneId,
            string markerId,
            WorldMapZoneContentMarkerType markerType,
            string[] tags,
            string sourceRuleSummary,
            string sourcePlanSummary,
            bool isRuntimeSpawnPoint,
            bool isDebugOnly)
        {
            WorldMapZoneContentMarker marker = markerRoot.GetComponent<WorldMapZoneContentMarker>();
            if (marker == null)
            {
                marker = markerRoot.AddComponent<WorldMapZoneContentMarker>();
            }

            marker.Configure(
                zoneId: zoneId,
                markerId: markerId,
                markerType: markerType,
                tags: tags,
                sourceRuleSummary: sourceRuleSummary,
                sourcePlanSummary: sourcePlanSummary,
                isRuntimeSpawnPoint: isRuntimeSpawnPoint,
                isDebugOnly: isDebugOnly
            );
        }

        // ===== Child Management =====

        /// <summary>
        /// Transform의 모든 자식을 Editor에서 즉시 삭제한다.
        /// </summary>
        private static void DestroyAllChildrenImmediate(Transform parent)
        {
            if (parent == null) return;

            // 역순으로 삭제 (인덱스 변동 방지)
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        // ===== Zone Iteration =====

        /// <summary>
        /// A1~J10 총 100개 zoneId를 안정적으로 순회한다.
        /// </summary>
        private static void ForEachZoneId(Action<string, int, int> callback)
        {
            for (int col = 0; col < TotalColumns; col++)
            {
                char columnChar = (char)('A' + col);
                for (int row = 0; row < TotalRows; row++)
                {
                    int rowNumber = row + 1;
                    string zoneIdStr = $"{columnChar}{rowNumber}";
                    callback(zoneIdStr, col, row);
                }
            }
        }

        // ===== Validation Helpers =====

        /// <summary>
        /// 특정 spawn root의 marker들을 검사한다.
        /// </summary>
        private static int ValidateSpawnRootMarkers(
            Transform contentTransform,
            string spawnRootName,
            string expectedZoneId,
            ref int markerComponentMissing,
            ref int zoneIdMismatch,
            ref int emptyMarkerId,
            ref int duplicateMarkerId,
            ref int invalidLocalScale,
            ref int invalidLocalRotation,
            ref int markerOutsideLocalBounds,
            HashSet<string> allMarkerIds,
            Transform zoneRootTransform,
            ref int totalMarkerComponentCount)
        {
            Transform spawnRoot = contentTransform.Find(spawnRootName);
            if (spawnRoot == null) return 0;

            int markerCount = 0;
            for (int i = 0; i < spawnRoot.childCount; i++)
            {
                Transform child = spawnRoot.GetChild(i);
                markerCount++;

                // 7. WorldMapZoneContentMarker 존재 검사
                WorldMapZoneContentMarker marker = child.GetComponent<WorldMapZoneContentMarker>();
                if (marker == null)
                {
                    markerComponentMissing++;
                    continue;
                }
                totalMarkerComponentCount++;

                // 8. ZoneId 일치 검사
                if (!string.Equals(marker.ZoneId, expectedZoneId, StringComparison.OrdinalIgnoreCase))
                {
                    zoneIdMismatch++;
                }

                // 9. MarkerId non-empty & unique 검사
                if (string.IsNullOrEmpty(marker.MarkerId))
                {
                    emptyMarkerId++;
                }
                else if (!allMarkerIds.Add(marker.MarkerId))
                {
                    duplicateMarkerId++;
                }

                // 10. localScale == Vector3.one
                if (child.localScale != Vector3.one)
                {
                    invalidLocalScale++;
                }

                // 11. localRotation == Quaternion.identity
                if (child.localRotation != Quaternion.identity)
                {
                    invalidLocalRotation++;
                }

                // 12. localPosition이 zone local bounds 안에 있는지 검사
                // zoneRootTransform 기준 localPosition의 x,z가 -ZoneLocalHalfExtent~+ZoneLocalHalfExtent 범위인지 확인
                Vector3 localPos = child.localPosition;
                if (Mathf.Abs(localPos.x) > ZoneLocalHalfExtent || Mathf.Abs(localPos.z) > ZoneLocalHalfExtent)
                {
                    markerOutsideLocalBounds++;
                }
            }

            return markerCount;
        }

        // ===== Logging =====

        /// <summary>
        /// settings.verboseLogging이 true일 때만 로그를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[FullContentMarker] {message}");
            }
        }
    }
}
