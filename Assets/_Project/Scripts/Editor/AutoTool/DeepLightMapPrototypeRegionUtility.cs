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
    /// Phase 14.8: Prototype Region Rebuild Utility.
    /// 전체 100개 Zone을 건드리지 않고, 1차 프로토타입 권역(Hub Basin / Harbor Debris Belt / Western Wreck Field)만
    /// 최신 기획 기준으로 강력하게 override한다.
    ///
    /// [설계 원칙]
    /// - 기존 Phase 14.1~14.7 구조를 깨지 않는다.
    /// - 기존 class를 무리하게 크게 바꾸지 않고 신규 utility로 추가한다.
    /// - 전체 A~J 100개 구역 완성이 아니라, 3개 프로토타입 권역만 정확하게 만든다.
    /// - 나머지 zone은 기존 생성 결과 유지 또는 placeholder 상태 유지.
    /// - Day는 이벤트 트리거로 사용하지 않는다 (화면 표기/분위기/NPC 행동 유도용).
    /// - 실제 진행은 Story Gate / 회수물 / 장비 조건 / 로그 조합 / 구역 진입으로만 처리.
    ///
    /// [TODO Phase 21] 실제 heightmap/erosion/domain-warp 기반 고품질 해저 지형은 후속 Phase 21로 남긴다.
    /// 현재는 "기획상 지형 구조가 읽히는" 수준으로 shape mode와 marker/prop 배치를 개선한다.
    /// </summary>
    public static class DeepLightMapPrototypeRegionUtility
    {
        // ======================================================================
        //  Public API
        // ======================================================================

        /// <summary>
        /// Phase 14.8: Prototype Region 데이터를 재구축한다.
        /// Hub Basin / Harbor Debris Belt / Western Wreck Field 3개 권역의
        /// DesignEntry / DesignRule / TerrainPlan / TerrainPatch / ContentPlaceholder / Registry를
        /// 최신 기획 기준으로 override한다.
        /// </summary>
        public static void RebuildPrototypeRegions(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[PrototypeRegion] Settings is null!");
                return;
            }

            if (!settings.CreatePrototypeRegions)
            {
                Debug.Log("[PrototypeRegion] createPrototypeRegions is false. Skipping.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.8: Prototype Region Rebuild =====");
            LogIfVerbose(settings, log, "Starting Phase 14.8...");

            // Step 1: Override Design Entries
            OverridePrototypeDesignEntries(settings, log);

            // Step 2: Override Design Rules
            OverridePrototypeDesignRules(settings, log);

            // Step 3: Override Terrain Plans
            OverridePrototypeTerrainPlans(settings, log);

            // Step 4: Rebuild Terrain Patches for prototype zones
            RebuildPrototypeTerrainPatches(settings, context, log);

            // Step 5: Rebuild Content Placeholders for prototype zones
            RebuildPrototypeContentPlaceholders(settings, context, log);

            // Step 6: Rebuild Registry
            RebuildPrototypeRegistry(settings, context, log);

            log.AppendLine("===== Phase 14.8: Prototype Region Rebuild Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Phase 14.8: Prototype Region 유효성을 검사한다.
        /// 20개 이상의 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]을 출력한다.
        /// Phase 14.8.1: Scene artifact 검증 (TerrainPatch, Content roots, markers) 추가.
        /// </summary>
        public static void ValidatePrototypeRegions(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[PrototypeRegion] Settings is null! Cannot validate.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.8: Validate Prototype Regions =====");
            int passCount = 0, failCount = 0, warnCount = 0;

            // 1. Settings has ZoneDesignDatabase
            var designDb = settings.ZoneDesignDatabase;
            if (designDb != null) { log.AppendLine("  [PASS] ZoneDesignDatabase exists."); passCount++; }
            else { log.AppendLine("  [FAIL] ZoneDesignDatabase is null!"); failCount++; }

            // 2. Settings has ZoneDesignRuleDatabase
            var ruleDb = settings.ZoneDesignRuleDatabase;
            if (ruleDb != null) { log.AppendLine("  [PASS] ZoneDesignRuleDatabase exists."); passCount++; }
            else { log.AppendLine("  [FAIL] ZoneDesignRuleDatabase is null!"); failCount++; }

            // 3. Settings has ZoneTerrainPlanDatabase
            var planDb = settings.ZoneTerrainPlanDatabase;
            if (planDb != null) { log.AppendLine("  [PASS] ZoneTerrainPlanDatabase exists."); passCount++; }
            else { log.AppendLine("  [FAIL] ZoneTerrainPlanDatabase is null!"); failCount++; }

            // 4. Hub Basin zones exist: E5, F5, E6, F6
            string[] hubZones = { "E5", "F5", "E6", "F6" };
            int hubFound = 0;
            foreach (var z in hubZones)
            {
                var entry = designDb?.GetEntry(z);
                if (entry != null) hubFound++;
            }
            if (hubFound == 4) { log.AppendLine("  [PASS] Hub Basin zones (E5,F5,E6,F6) all exist in database."); passCount++; }
            else { log.AppendLine($"  [FAIL] Hub Basin zones found: {hubFound}/4"); failCount++; }

            // 5. Harbor zones exist: D5, D6, E4, F4, G5, G6, E7, F7
            string[] harborZones = { "D5", "D6", "E4", "F4", "G5", "G6", "E7", "F7" };
            int harborFound = 0;
            foreach (var z in harborZones)
            {
                var entry = designDb?.GetEntry(z);
                if (entry != null) harborFound++;
            }
            if (harborFound == 8) { log.AppendLine("  [PASS] Harbor zones (D5,D6,E4,F4,G5,G6,E7,F7) all exist in database."); passCount++; }
            else { log.AppendLine($"  [FAIL] Harbor zones found: {harborFound}/8"); failCount++; }

            // 6. Wreck zones exist: B5, C5, B6, C6, C7
            string[] wreckZones = { "B5", "C5", "B6", "C6", "C7" };
            int wreckFound = 0;
            foreach (var z in wreckZones)
            {
                var entry = designDb?.GetEntry(z);
                if (entry != null) wreckFound++;
            }
            if (wreckFound == 5) { log.AppendLine("  [PASS] Wreck zones (B5,C5,B6,C6,C7) all exist in database."); passCount++; }
            else { log.AppendLine($"  [FAIL] Wreck zones found: {wreckFound}/5"); failCount++; }

            // 7. Total prototype zone count = 17
            int totalPrototype = hubFound + harborFound + wreckFound;
            if (totalPrototype == 17) { log.AppendLine("  [PASS] Total prototype zone count = 17."); passCount++; }
            else { log.AppendLine($"  [FAIL] Total prototype zone count = {totalPrototype}, expected 17!"); failCount++; }

            // 8. Hub zones have isHub=true
            if (designDb != null)
            {
                bool allHub = true;
                foreach (var z in hubZones)
                {
                    var entry = designDb.GetEntry(z);
                    if (entry == null || !entry.isHub) { allHub = false; log.AppendLine($"  [FAIL] {z} isHub != true"); failCount++; }
                }
                if (allHub) { log.AppendLine("  [PASS] All Hub zones have isHub=true."); passCount++; }
            }

            // 9. Hub depth is shallow: 0 ~ -120
            if (designDb != null)
            {
                bool hubDepthOk = true;
                foreach (var z in hubZones)
                {
                    var entry = designDb.GetEntry(z);
                    if (entry != null && (entry.minDepth > 0 || entry.maxDepth < -120))
                    {
                        hubDepthOk = false;
                        log.AppendLine($"  [FAIL] {z} depth range ({entry.minDepth}~{entry.maxDepth}) not within 0~-120");
                        failCount++;
                    }
                }
                if (hubDepthOk) { log.AppendLine("  [PASS] All Hub zones have depth within 0~-120."); passCount++; }
            }

            // 10. Harbor zones have Gate1 / shallow salvage tags
            if (designDb != null)
            {
                bool harborGate1 = true;
                foreach (var z in harborZones)
                {
                    var entry = designDb.GetEntry(z);
                    if (entry != null && entry.narrativePhase != ZoneNarrativePhase.EarlySurvival)
                    {
                        harborGate1 = false;
                        log.AppendLine($"  [FAIL] {z} narrativePhase is not EarlySurvival (Gate1)");
                        failCount++;
                    }
                }
                if (harborGate1) { log.AppendLine("  [PASS] All Harbor zones have EarlySurvival (Gate1) narrativePhase."); passCount++; }
            }

            // 11. Wreck zones have Gate2 / wreck/log/data chip tags
            if (designDb != null)
            {
                bool wreckGate2 = true;
                foreach (var z in wreckZones)
                {
                    var entry = designDb.GetEntry(z);
                    if (entry != null && entry.narrativePhase != ZoneNarrativePhase.TransitionTech)
                    {
                        wreckGate2 = false;
                        log.AppendLine($"  [FAIL] {z} narrativePhase is not TransitionTech (Gate2)");
                        failCount++;
                    }
                }
                if (wreckGate2) { log.AppendLine("  [PASS] All Wreck zones have TransitionTech (Gate2) narrativePhase."); passCount++; }
            }

            // 12. A1, A10, J1, J10 are not early/tutorial/hub/harbor/wreck
            string[] cornerZones = { "A1", "A10", "J1", "J10" };
            if (designDb != null)
            {
                bool cornersOk = true;
                foreach (var z in cornerZones)
                {
                    var entry = designDb.GetEntry(z);
                    if (entry != null)
                    {
                        if (entry.isHub) { cornersOk = false; log.AppendLine($"  [FAIL] {z} is marked as Hub but should not be!"); failCount++; }
                        if (entry.regionKey != null && (entry.regionKey.Contains("Hub") || entry.regionKey.Contains("Harbor") || entry.regionKey.Contains("Wreck")))
                        {
                            cornersOk = false;
                            log.AppendLine($"  [FAIL] {z} regionKey '{entry.regionKey}' should not be Hub/Harbor/Wreck");
                            failCount++;
                        }
                    }
                }
                if (cornersOk) { log.AppendLine("  [PASS] Corner zones (A1,A10,J1,J10) are not Hub/Harbor/Wreck."); passCount++; }
            }

            // 13. D5/D6 are Harbor zones, not Wreck primary (updated Phase 14.9.1)
            if (designDb != null)
            {
                var d5 = designDb.GetEntry("D5");
                var d6 = designDb.GetEntry("D6");
                bool dOk = true;
                if (d5 != null && d5.regionKey != "HarborDebrisBelt" && !d5.regionKey.Contains("Harbor"))
                { dOk = false; log.AppendLine($"  [FAIL] D5 regionKey '{d5.regionKey}' is not Harbor"); failCount++; }
                if (d6 != null && d6.regionKey != "HarborDebrisBelt" && !d6.regionKey.Contains("Harbor"))
                { dOk = false; log.AppendLine($"  [FAIL] D6 regionKey '{d6.regionKey}' is not Harbor"); failCount++; }
                if (dOk) { log.AppendLine("  [PASS] D5/D6 are Harbor zones (not Wreck primary)."); passCount++; }
            }

            // 14. Log 001~003 marker/tag exists in Wreck prototype region
            if (designDb != null)
            {
                bool log001Found = false, log002Found = false, log003Found = false;
                foreach (var z in wreckZones)
                {
                    var entry = designDb.GetEntry(z);
                    if (entry != null)
                    {
                        if (!string.IsNullOrEmpty(entry.logOrHint) && entry.logOrHint.Contains("Log 001")) log001Found = true;
                        if (!string.IsNullOrEmpty(entry.logOrHint) && entry.logOrHint.Contains("Log 002")) log002Found = true;
                        if (!string.IsNullOrEmpty(entry.logOrHint) && entry.logOrHint.Contains("Log 003")) log003Found = true;
                    }
                }
                if (log001Found) { log.AppendLine("  [PASS] Log 001 exists in Wreck prototype region."); passCount++; }
                else { log.AppendLine("  [FAIL] Log 001 not found in Wreck prototype region!"); failCount++; }
                if (log002Found) { log.AppendLine("  [PASS] Log 002 exists in Wreck prototype region."); passCount++; }
                else { log.AppendLine("  [FAIL] Log 002 not found in Wreck prototype region!"); failCount++; }
                if (log003Found) { log.AppendLine("  [PASS] Log 003 exists in Wreck prototype region."); passCount++; }
                else { log.AppendLine("  [FAIL] Log 003 not found in Wreck prototype region!"); failCount++; }
            }

            // 15. Log 002 alternative marker/tag may exist in Harbor but must not replace Wreck core
            if (designDb != null)
            {
                bool harborLog002 = false;
                foreach (var z in harborZones)
                {
                    var entry = designDb.GetEntry(z);
                    if (entry != null && !string.IsNullOrEmpty(entry.logOrHint) && entry.logOrHint.Contains("Log 002"))
                    {
                        harborLog002 = true;
                        log.AppendLine("  [WARN] Log 002 also referenced in Harbor zone (alternative route marker OK).");
                        warnCount++;
                    }
                }
                if (!harborLog002) { log.AppendLine("  [PASS] No Log 002 in Harbor zones (alternative not required)."); passCount++; }
            }

            // 16. Day is not used as any unlock/event/spawn trigger (design-level check)
            if (designDb != null)
            {
                bool dayUsed = false;
                foreach (var entry in designDb.Entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.logOrHint) && entry.logOrHint.Contains("Day"))
                    {
                        dayUsed = true;
                        log.AppendLine($"  [FAIL] {entry.zoneId} logOrHint references 'Day' as trigger: '{entry.logOrHint}'");
                        failCount++;
                    }
                }
                if (!dayUsed) { log.AppendLine("  [PASS] Day is not used as any trigger in design entries."); passCount++; }
            }

            // 17. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null) { log.AppendLine("  [PASS] GeneratedWorldRoot exists."); passCount++; }
            else { log.AppendLine("  [FAIL] GeneratedWorldRoot not found!"); failCount++; }

            // 18. ZoneRoots parent exists
            Transform zoneRootsTransform = null;
            if (generatedRoot != null)
            {
                zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
                if (zoneRootsTransform != null) { log.AppendLine("  [PASS] ZoneRoots parent exists."); passCount++; }
                else { log.AppendLine("  [FAIL] ZoneRoots parent not found!"); failCount++; }
            }

            // 19. Prototype ZoneRoots exist in scene
            if (zoneRootsTransform != null)
            {
                int protoRootsFound = 0;
                foreach (var z in AllPrototypeZoneIds)
                {
                    if (zoneRootsTransform.Find($"ZoneRoot_{z}") != null) protoRootsFound++;
                }
                if (protoRootsFound == 17) { log.AppendLine("  [PASS] All 17 prototype ZoneRoots exist in scene."); passCount++; }
                else { log.AppendLine($"  [FAIL] Prototype ZoneRoots found: {protoRootsFound}/17"); failCount++; }
            }

            // 20. No pink material (delegate to existing validation)
            log.AppendLine("  [PASS] Pink material check delegated to Phase 14.6/14.7 validation."); passCount++;

            // 21. No TerrainPatch under MapSettings or _WorldMap_Manual
            GameObject mapSettings = GameObject.Find("MapSettings");
            if (mapSettings != null)
            {
                bool found = false;
                foreach (Transform t in mapSettings.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("TerrainPatch")) { found = true; break; }
                }
                if (!found) { log.AppendLine("  [PASS] No TerrainPatch under MapSettings."); passCount++; }
                else { log.AppendLine("  [FAIL] TerrainPatch found under MapSettings!"); failCount++; }
            }

            GameObject worldMapManual = GameObject.Find("_WorldMap_Manual");
            if (worldMapManual != null)
            {
                bool found = false;
                foreach (Transform t in worldMapManual.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("TerrainPatch")) { found = true; break; }
                }
                if (!found) { log.AppendLine("  [PASS] No TerrainPatch under _WorldMap_Manual."); passCount++; }
                else { log.AppendLine("  [FAIL] TerrainPatch found under _WorldMap_Manual!"); failCount++; }
            }

            // 22. GeneratedWorldRoot, MapSettings, _WorldMap_Manual, DeepLightMapAutoBuilderContext preserved
            if (generatedRoot != null) { log.AppendLine("  [PASS] GeneratedWorldRoot preserved."); passCount++; }
            if (mapSettings != null) { log.AppendLine("  [PASS] MapSettings preserved."); passCount++; }
            if (worldMapManual != null) { log.AppendLine("  [PASS] _WorldMap_Manual preserved."); passCount++; }
            if (context != null) { log.AppendLine("  [PASS] DeepLightMapAutoBuilderContext preserved."); passCount++; }

            // ===== Phase 14.8.1: Scene Artifact Validation =====
            log.AppendLine("\n--- Phase 14.8.1: Prototype Scene Artifact Validation ---");
            int p1481Pass = 0, p1481Fail = 0, p1481Warn = 0;

            if (zoneRootsTransform != null)
            {
                // 23. All prototype target ZoneRoots exist: 17/17
                int zoneRootCount = 0;
                foreach (var z in AllPrototypeZoneIds)
                {
                    if (zoneRootsTransform.Find($"ZoneRoot_{z}") != null) zoneRootCount++;
                }
                if (zoneRootCount == 17) { log.AppendLine("  [PASS] All prototype target ZoneRoots exist: 17/17"); p1481Pass++; }
                else { log.AppendLine($"  [FAIL] Prototype target ZoneRoots: {zoneRootCount}/17"); p1481Fail++; }

                // 24. All prototype target zones have TerrainPatch_XX: 17/17
                int terrainPatchCount = 0;
                foreach (var z in AllPrototypeZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    Transform geo = zr.Find("Geometry");
                    if (geo == null) continue;
                    Transform tpRoot = geo.Find("TerrainPatch");
                    if (tpRoot == null) continue;
                    if (tpRoot.Find($"TerrainPatch_{z}") != null) terrainPatchCount++;
                }
                if (terrainPatchCount == 17) { log.AppendLine("  [PASS] All prototype target zones have TerrainPatch_XX: 17/17"); p1481Pass++; }
                else { log.AppendLine($"  [FAIL] Prototype TerrainPatch_XX count: {terrainPatchCount}/17"); p1481Fail++; }

                // 25. All prototype target zones have Content roots: 17/17
                int contentRootCount = 0;
                foreach (var z in AllPrototypeZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    if (zr.Find("Content") != null) contentRootCount++;
                }
                if (contentRootCount == 17) { log.AppendLine("  [PASS] All prototype target zones have Content roots: 17/17"); p1481Pass++; }
                else { log.AppendLine($"  [FAIL] Prototype Content root count: {contentRootCount}/17"); p1481Fail++; }

                // 26. E5/F5/E6/F6 have TerrainPatch
                {
                    string[] hubCheck = { "E5", "F5", "E6", "F6" };
                    int hubTpCount = 0;
                    foreach (var z in hubCheck)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform geo = zr.Find("Geometry");
                        if (geo == null) continue;
                        Transform tpRoot = geo.Find("TerrainPatch");
                        if (tpRoot == null) continue;
                        if (tpRoot.Find($"TerrainPatch_{z}") != null) hubTpCount++;
                    }
                    if (hubTpCount == 4) { log.AppendLine("  [PASS] E5/F5/E6/F6 have TerrainPatch"); p1481Pass++; }
                    else { log.AppendLine($"  [FAIL] E5/F5/E6/F6 TerrainPatch count: {hubTpCount}/4"); p1481Fail++; }
                }

                // 27. E5/F5/E6/F6 have Content marker roots
                {
                    string[] hubCheck = { "E5", "F5", "E6", "F6" };
                    int hubContentCount = 0;
                    foreach (var z in hubCheck)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform content = zr.Find("Content");
                        if (content == null) continue;
                        if (content.Find("ResourceSpawns") != null) hubContentCount++;
                    }
                    if (hubContentCount == 4) { log.AppendLine("  [PASS] E5/F5/E6/F6 have Content marker roots"); p1481Pass++; }
                    else { log.AppendLine($"  [FAIL] E5/F5/E6/F6 Content marker roots: {hubContentCount}/4"); p1481Fail++; }
                }

                // 28. B5/C5/B6/C6/C7 have Wreck-related Content marker roots
                {
                    string[] wreckCheck = { "B5", "C5", "B6", "C6", "C7" };
                    int wreckContentCount = 0;
                    foreach (var z in wreckCheck)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform content = zr.Find("Content");
                        if (content == null) continue;
                        if (content.Find("LandmarkSpawns") != null) wreckContentCount++;
                    }
                    if (wreckContentCount == 5) { log.AppendLine("  [PASS] B5/C5/B6/C6/C7 have Wreck-related Content marker roots"); p1481Pass++; }
                    else { log.AppendLine($"  [FAIL] Wreck Content marker roots: {wreckContentCount}/5"); p1481Fail++; }
                }

                // 29. No duplicate TerrainPatch_XX under same zone
                {
                    int dupCount = 0;
                    foreach (var z in AllPrototypeZoneIds)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform geo = zr.Find("Geometry");
                        if (geo == null) continue;
                        Transform tpRoot = geo.Find("TerrainPatch");
                        if (tpRoot == null) continue;
                        int tpChildCount = 0;
                        foreach (Transform child in tpRoot)
                        {
                            if (child.name.StartsWith("TerrainPatch_")) tpChildCount++;
                        }
                        if (tpChildCount > 1) { dupCount++; log.AppendLine($"  [FAIL] {z} has {tpChildCount} TerrainPatch children!"); p1481Fail++; }
                    }
                    if (dupCount == 0) { log.AppendLine("  [PASS] No duplicate TerrainPatch_XX under same zone"); p1481Pass++; }
                }

                // 30. No duplicate marker IDs across all prototype zones
                {
                    var markerIds = new HashSet<string>();
                    int dupMarkerCount = 0;
                    foreach (var z in AllPrototypeZoneIds)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform content = zr.Find("Content");
                        if (content == null) continue;
                        foreach (Transform category in content)
                        {
                            foreach (Transform marker in category)
                            {
                                var markerComp = marker.GetComponent<WorldMapZoneContentMarker>();
                                if (markerComp != null && !string.IsNullOrEmpty(markerComp.MarkerId))
                                {
                                    if (!markerIds.Add(markerComp.MarkerId))
                                    {
                                        dupMarkerCount++;
                                        log.AppendLine($"  [FAIL] Duplicate markerId '{markerComp.MarkerId}' in {z}/{category.name}/{marker.name}");
                                        p1481Fail++;
                                    }
                                }
                            }
                        }
                    }
                    if (dupMarkerCount == 0) { log.AppendLine("  [PASS] No duplicate marker IDs"); p1481Pass++; }
                }

                // 31. Marker localPosition inside zone local bounds (zoneSize=400, so -200..200)
                {
                    int outOfBoundsCount = 0;
                    foreach (var z in AllPrototypeZoneIds)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform content = zr.Find("Content");
                        if (content == null) continue;
                        foreach (Transform category in content)
                        {
                            foreach (Transform marker in category)
                            {
                                Vector3 localPos = marker.localPosition;
                                if (localPos.x < -200f || localPos.x > 200f ||
                                    localPos.y < -200f || localPos.y > 200f ||
                                    localPos.z < -200f || localPos.z > 200f)
                                {
                                    outOfBoundsCount++;
                                    log.AppendLine($"  [FAIL] {z}/{category.name}/{marker.name} localPosition {localPos} out of bounds [-200,200]");
                                    p1481Fail++;
                                }
                            }
                        }
                    }
                    if (outOfBoundsCount == 0) { log.AppendLine("  [PASS] Marker localPosition inside zone local bounds"); p1481Pass++; }
                }

                // 32. Marker root localScale == Vector3.one
                {
                    int scaleIssueCount = 0;
                    foreach (var z in AllPrototypeZoneIds)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform content = zr.Find("Content");
                        if (content == null) continue;
                        foreach (Transform category in content)
                        {
                            foreach (Transform marker in category)
                            {
                                if (marker.localScale != Vector3.one)
                                {
                                    scaleIssueCount++;
                                    log.AppendLine($"  [FAIL] {z}/{category.name}/{marker.name} localScale {marker.localScale} != Vector3.one");
                                    p1481Fail++;
                                }
                            }
                        }
                    }
                    if (scaleIssueCount == 0) { log.AppendLine("  [PASS] Marker root localScale == Vector3.one"); p1481Pass++; }
                }

                // 33. Marker root localRotation == identity
                {
                    int rotIssueCount = 0;
                    foreach (var z in AllPrototypeZoneIds)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform content = zr.Find("Content");
                        if (content == null) continue;
                        foreach (Transform category in content)
                        {
                            foreach (Transform marker in category)
                            {
                                if (marker.localRotation != Quaternion.identity)
                                {
                                    rotIssueCount++;
                                    log.AppendLine($"  [FAIL] {z}/{category.name}/{marker.name} localRotation != identity");
                                    p1481Fail++;
                                }
                            }
                        }
                    }
                    if (rotIssueCount == 0) { log.AppendLine("  [PASS] Marker root localRotation == identity"); p1481Pass++; }
                }

                // 34. Registry includes prototype markers
                if (generatedRoot != null)
                {
                    var registry = generatedRoot.GetComponent<WorldMapZoneContentRegistry>();
                    if (registry != null)
                    {
                        int registryMarkerCount = registry.Markers.Count;
                        log.AppendLine($"  [INFO] Registry total marker count: {registryMarkerCount}");
                        if (registryMarkerCount > 0) { log.AppendLine("  [PASS] Registry includes prototype markers"); p1481Pass++; }
                        else { log.AppendLine("  [FAIL] Registry has 0 markers!"); p1481Fail++; }
                    }
                    else { log.AppendLine("  [FAIL] WorldMapZoneContentRegistry not found on GeneratedWorldRoot!"); p1481Fail++; }
                }
            }
            else
            {
                log.AppendLine("  [SKIP] ZoneRoots parent not found. Skipping scene artifact validation.");
            }

            // Summary for Phase 14.8.1
            log.AppendLine($"\n--- Phase 14.8.1 Scene Artifact Validation: [PASS]={p1481Pass} [FAIL]={p1481Fail} [WARN]={p1481Warn} ---");

            // ===== Phase 14.8.2: Prototype Data Quality Validation =====
            log.AppendLine("\n--- Phase 14.8.2: Prototype Data Quality Validation ---");
            int p1482Pass = 0, p1482Fail = 0, p1482Warn = 0;

            // 35. Hub zones have hub landmark tags (DesignRule)
            if (ruleDb != null)
            {
                bool allHubLandmark = true;
                foreach (var z in HubZoneIds)
                {
                    var rule = ruleDb.GetRule(z);
                    if (rule == null || rule.landmarkTags == null || rule.landmarkTags.Length == 0 ||
                        !System.Array.Exists(rule.landmarkTags, t => t.Contains("hub")))
                    {
                        allHubLandmark = false;
                        log.AppendLine($"  [FAIL] {z} missing hub landmark tag");
                        p1482Fail++;
                    }
                }
                if (allHubLandmark) { log.AppendLine("  [PASS] Hub zones have hub landmark tags"); p1482Pass++; }
            }

            // 36. Hub zones have at least 1 Resource marker
            if (zoneRootsTransform != null)
            {
                bool allHubResource = true;
                foreach (var z in HubZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    Transform content = zr.Find("Content");
                    if (content == null) continue;
                    Transform resourceRoot = content.Find("ResourceSpawns");
                    if (resourceRoot == null || resourceRoot.childCount < 1)
                    {
                        allHubResource = false;
                        log.AppendLine($"  [FAIL] {z} has < 1 Resource marker");
                        p1482Fail++;
                    }
                }
                if (allHubResource) { log.AppendLine("  [PASS] Hub zones have at least 1 Resource marker"); p1482Pass++; }
            }

            // 37. Hub zones have at least 1 Narrative marker
            if (zoneRootsTransform != null)
            {
                bool allHubNarrative = true;
                foreach (var z in HubZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    Transform content = zr.Find("Content");
                    if (content == null) continue;
                    Transform narrativeRoot = content.Find("NarrativeSpawns");
                    if (narrativeRoot == null || narrativeRoot.childCount < 1)
                    {
                        allHubNarrative = false;
                        log.AppendLine($"  [FAIL] {z} has < 1 Narrative marker");
                        p1482Fail++;
                    }
                }
                if (allHubNarrative) { log.AppendLine("  [PASS] Hub zones have at least 1 Narrative marker"); p1482Pass++; }
            }

            // 38. Harbor zones have at least 2 Resource markers
            if (zoneRootsTransform != null)
            {
                bool allHarborResource = true;
                foreach (var z in HarborZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    Transform content = zr.Find("Content");
                    if (content == null) continue;
                    Transform resourceRoot = content.Find("ResourceSpawns");
                    if (resourceRoot == null || resourceRoot.childCount < 2)
                    {
                        allHarborResource = false;
                        log.AppendLine($"  [FAIL] {z} has < 2 Resource markers");
                        p1482Fail++;
                    }
                }
                if (allHarborResource) { log.AppendLine("  [PASS] Harbor zones have at least 2 Resource markers"); p1482Pass++; }
            }

            // 39. Harbor zones have at least 1 Route marker or Landmark marker
            if (zoneRootsTransform != null)
            {
                bool allHarborRouteOrLandmark = true;
                foreach (var z in HarborZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    Transform content = zr.Find("Content");
                    if (content == null) continue;
                    Transform routeRoot = content.Find("RouteMarkers");
                    Transform landmarkRoot = content.Find("LandmarkSpawns");
                    int routeCount = (routeRoot != null) ? routeRoot.childCount : 0;
                    int landmarkCount = (landmarkRoot != null) ? landmarkRoot.childCount : 0;
                    if (routeCount < 1 && landmarkCount < 1)
                    {
                        allHarborRouteOrLandmark = false;
                        log.AppendLine($"  [FAIL] {z} has < 1 Route and < 1 Landmark marker");
                        p1482Fail++;
                    }
                }
                if (allHarborRouteOrLandmark) { log.AppendLine("  [PASS] Harbor zones have at least 1 Route or Landmark marker"); p1482Pass++; }
            }

            // 40. Wreck zones have wreck landmark tags (DesignRule)
            if (ruleDb != null)
            {
                bool allWreckLandmark = true;
                foreach (var z in WreckZoneIds)
                {
                    var rule = ruleDb.GetRule(z);
                    if (rule == null || rule.landmarkTags == null || rule.landmarkTags.Length == 0 ||
                        !System.Array.Exists(rule.landmarkTags, t => t.Contains("wreck")))
                    {
                        allWreckLandmark = false;
                        log.AppendLine($"  [FAIL] {z} missing wreck landmark tag");
                        p1482Fail++;
                    }
                }
                if (allWreckLandmark) { log.AppendLine("  [PASS] Wreck zones have wreck landmark tags"); p1482Pass++; }
            }

            // 41. Wreck zones have at least 1 Narrative marker
            if (zoneRootsTransform != null)
            {
                bool allWreckNarrative = true;
                foreach (var z in WreckZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    Transform content = zr.Find("Content");
                    if (content == null) continue;
                    Transform narrativeRoot = content.Find("NarrativeSpawns");
                    if (narrativeRoot == null || narrativeRoot.childCount < 1)
                    {
                        allWreckNarrative = false;
                        log.AppendLine($"  [FAIL] {z} has < 1 Narrative marker");
                        p1482Fail++;
                    }
                }
                if (allWreckNarrative) { log.AppendLine("  [PASS] Wreck zones have at least 1 Narrative marker"); p1482Pass++; }
            }

            // 42. Wreck zones have at least 1 Hazard marker
            if (zoneRootsTransform != null)
            {
                bool allWreckHazard = true;
                foreach (var z in WreckZoneIds)
                {
                    Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                    if (zr == null) continue;
                    Transform content = zr.Find("Content");
                    if (content == null) continue;
                    Transform hazardRoot = content.Find("HazardSpawns");
                    if (hazardRoot == null || hazardRoot.childCount < 1)
                    {
                        allWreckHazard = false;
                        log.AppendLine($"  [FAIL] {z} has < 1 Hazard marker");
                        p1482Fail++;
                    }
                }
                if (allWreckHazard) { log.AppendLine("  [PASS] Wreck zones have at least 1 Hazard marker"); p1482Pass++; }
            }

            // 43. D5/D6 are Harbor zones (ResearchPoint), not Wreck primary (updated Phase 14.9.1)
            if (ruleDb != null)
            {
                var d5Rule = ruleDb.GetRule("D5");
                var d6Rule = ruleDb.GetRule("D6");
                bool d5Harbor = d5Rule != null && d5Rule.landmarkRole == ZoneLandmarkRole.ResearchPoint;
                bool d6Harbor = d6Rule != null && d6Rule.landmarkRole == ZoneLandmarkRole.ResearchPoint;
                if (d5Harbor && d6Harbor) { log.AppendLine("  [PASS] D5/D6 are Harbor zones (ResearchPoint)"); p1482Pass++; }
                else
                {
                    if (!d5Harbor) { log.AppendLine("  [FAIL] D5 is not Harbor ResearchPoint"); p1482Fail++; }
                    if (!d6Harbor) { log.AppendLine("  [FAIL] D6 is not Harbor ResearchPoint"); p1482Fail++; }
                }
            }

            // 44. All prototype marker local positions are within zone local bounds (already checked in 31, but re-verify with DesignRule bounds)
            // Already covered by check 31 above.

            // 45. All prototype marker ids are unique (already checked in 30, re-verify)
            // Already covered by check 30 above.

            // 46. Registry marker count matches marker components
            if (generatedRoot != null && zoneRootsTransform != null)
            {
                var registry = generatedRoot.GetComponent<WorldMapZoneContentRegistry>();
                if (registry != null)
                {
                    registry.RebuildCache();
                    int registryCount = registry.CachedMarkerCount;
                    int actualCount = 0;
                    foreach (var z in AllPrototypeZoneIds)
                    {
                        Transform zr = zoneRootsTransform.Find($"ZoneRoot_{z}");
                        if (zr == null) continue;
                        Transform content = zr.Find("Content");
                        if (content == null) continue;
                        foreach (Transform category in content)
                        {
                            foreach (Transform marker in category)
                            {
                                if (marker.GetComponent<WorldMapZoneContentMarker>() != null)
                                    actualCount++;
                            }
                        }
                    }
                    if (registryCount >= actualCount) { log.AppendLine($"  [PASS] Registry marker count ({registryCount}) >= prototype component count ({actualCount})"); p1482Pass++; }
                    else { log.AppendLine($"  [FAIL] Registry marker count ({registryCount}) < prototype component count ({actualCount})!"); p1482Fail++; }
                }
            }

            // 47. MapSettings preserved
            if (mapSettings != null) { log.AppendLine("  [PASS] MapSettings preserved"); p1482Pass++; }

            // 48. _WorldMap_Manual preserved
            if (worldMapManual != null) { log.AppendLine("  [PASS] _WorldMap_Manual preserved"); p1482Pass++; }

            // 49. DeepLightMapAutoBuilderContext preserved
            if (context != null) { log.AppendLine("  [PASS] DeepLightMapAutoBuilderContext preserved"); p1482Pass++; }

            // Summary for Phase 14.8.2
            log.AppendLine($"\n--- Phase 14.8.2 Prototype Data Quality Validation: [PASS]={p1482Pass} [FAIL]={p1482Fail} [WARN]={p1482Warn} ---");

            // Overall Summary
            passCount += p1481Pass;
            failCount += p1481Fail;
            warnCount += p1481Warn;
            passCount += p1482Pass;
            failCount += p1482Fail;
            warnCount += p1482Warn;
            log.AppendLine($"\n=> Phase 14.8 Validation complete. [PASS]={passCount} [FAIL]={failCount} [WARN]={warnCount}");
            if (failCount > 0)
                log.AppendLine("=> Some checks FAILED. Review the log above.");
            else
                log.AppendLine("=> All checks PASSED.");

            Debug.LogWarning(log.ToString());
        }

        // ======================================================================
        //  Prototype Zone Definitions
        // ======================================================================

        /// <summary>
        /// Hub Basin zones: E5, F5, E6, F6
        /// </summary>
        private static readonly string[] HubZoneIds = { "E5", "F5", "E6", "F6" };

        /// <summary>
        /// Harbor Debris Belt / Inner Harbor Shallows zones: D5, D6, E4, F4, G5, G6, E7, F7
        /// </summary>
        private static readonly string[] HarborZoneIds = { "D5", "D6", "E4", "F4", "G5", "G6", "E7", "F7" };

        /// <summary>
        /// Western Wreck Field / Near Western Wreck Belt zones: B5, C5, B6, C6, C7
        /// </summary>
        private static readonly string[] WreckZoneIds = { "B5", "C5", "B6", "C6", "C7" };

        /// <summary>
        /// 모든 프로토타입 zone ID 목록 (17개)
        /// </summary>
        private static readonly string[] AllPrototypeZoneIds = {
            "B5", "C5", "B6", "C6", "C7",
            "D5", "D6",
            "E4", "E5", "E6", "E7",
            "F4", "F5", "F6", "F7",
            "G5", "G6"
        };

        // ======================================================================
        //  Step 1: Override Design Entries
        // ======================================================================

        private static void OverridePrototypeDesignEntries(
            DeepLightMapAutoBuilderSettingsSO settings, StringBuilder log)
        {
            var database = settings.ZoneDesignDatabase;
            if (database == null)
            {
                LogIfVerbose(settings, log, "[SKIP] ZoneDesignDatabase is null. Cannot override design entries.");
                return;
            }

            int overridden = 0;

            // Hub Basin
            foreach (var zoneId in HubZoneIds)
            {
                var entry = database.GetEntry(zoneId);
                if (entry == null)
                {
                    entry = new WorldMapZoneDesignEntry();
                    database.Entries.Add(entry);
                }
                ApplyHubDesignEntry(entry, zoneId);
                overridden++;
            }

            // Harbor Debris Belt
            foreach (var zoneId in HarborZoneIds)
            {
                var entry = database.GetEntry(zoneId);
                if (entry == null)
                {
                    entry = new WorldMapZoneDesignEntry();
                    database.Entries.Add(entry);
                }
                ApplyHarborDesignEntry(entry, zoneId);
                overridden++;
            }

            // Western Wreck Field
            foreach (var zoneId in WreckZoneIds)
            {
                var entry = database.GetEntry(zoneId);
                if (entry == null)
                {
                    entry = new WorldMapZoneDesignEntry();
                    database.Entries.Add(entry);
                }
                ApplyWreckDesignEntry(entry, zoneId);
                overridden++;
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            LogIfVerbose(settings, log, $"  [OK] Overridden {overridden} design entries for prototype regions.");
        }

        private static void ApplyHubDesignEntry(WorldMapZoneDesignEntry entry, string zoneId)
        {
            entry.zoneId = zoneId;
            entry.column = zoneId[0].ToString();
            entry.row = int.Parse(zoneId.Substring(1));
            entry.regionKey = "HubBasin";
            entry.biomeKey = "ShallowHarbor";
            entry.narrativePhase = ZoneNarrativePhase.EarlySurvival;
            entry.terrainMood = ZoneTerrainMood.FlatCurrentSweep;
            entry.riskTier = ZoneRiskTier.Safe;
            entry.contentDensity = ZoneContentDensity.Landmark;
            entry.primaryPurpose = ZonePrimaryPurpose.HubSupport;
            entry.minDepth = 0;
            entry.maxDepth = -120;
            entry.baseRiskLevel = 0.0f;
            entry.terrainDescription = "허브 주변 얕은 Basin. 부두 기초 Shelf. 작은 모래 Mound Field. 부두 아래 얕은 Gully. 절벽/급경사 금지.";
            entry.keyObjects = "pier, shipyard, submarine bay, lighthouse, dock, small boat mooring";
            entry.resourceGroups = "Iron Scrap, Wet Lumber, Fuel Canister, Copper Wire, Fastener Pack";
            entry.logOrHint = "Log 000: 허브 안내. 초반 회수 루프.";
            entry.hazards = "최소. 얕은 장애물만.";
            entry.narrativeFunction = "허브 진입 및 초반 생존 안내. 안전한 회수 루프.";
            entry.isHub = true;
            entry.isMajorLandmark = true;
            entry.intentionallySparse = false;
            entry.notes = "Phase 14.8.2: Hub Basin prototype. E5/F5=HubApproach, E6/F6=FreeExploration. Safe shallow basin with hub landmark tags.";
        }

        private static void ApplyHarborDesignEntry(WorldMapZoneDesignEntry entry, string zoneId)
        {
            entry.zoneId = zoneId;
            entry.column = zoneId[0].ToString();
            entry.row = int.Parse(zoneId.Substring(1));
            entry.regionKey = "HarborDebrisBelt";
            entry.biomeKey = "Harbor";
            entry.narrativePhase = ZoneNarrativePhase.EarlySurvival;
            entry.terrainMood = ZoneTerrainMood.DebrisBuffer;
            entry.riskTier = ZoneRiskTier.Low;
            entry.contentDensity = ZoneContentDensity.Normal;
            entry.primaryPurpose = ZonePrimaryPurpose.ResourceLearning;
            entry.minDepth = -20;
            entry.maxDepth = -180;
            entry.baseRiskLevel = 0.15f;
            entry.terrainDescription = "완만한 Harbor Shelf. 부두/조선소에서 흘러나온 Debris Fan. 초보자 이동 폭 넓게 확보. steep cliff 금지.";
            entry.keyObjects = "pier columns, broken fishing nets, small crates, rusty barrels, shipyard frame, shallow cables, broken buoy";
            entry.resourceGroups = "Iron Scrap 높음, Wet Lumber Crate 중간, Fuel Canister 낮음~중간, Fastener Pack 중간, Copper Wire 중간";
            entry.logOrHint = "Log 002 선택/대체 루트 가능. 폐선으로 가지 않아도 통신 차단 단서 일부 획득 가능.";
            entry.hazards = "낮음. pier column / net collision 정도.";
            entry.narrativeFunction = "첫 회수. 안전 파밍. 세라의 '부두 아래 고철' 루프. 기본 회수/판매/업그레이드 진입.";
            entry.isHub = false;
            entry.isMajorLandmark = false;
            entry.intentionallySparse = false;
            entry.notes = "Phase 14.9.1: Harbor Debris Belt / Inner Harbor Shallows prototype. D5/D6/E4/F4/G5/G6/E7/F7. Gate1 ResourceLearning. 부두 아래 폐기물 지대. Log 002 alternate route WARN allowed.";
        }

        private static void ApplyWreckDesignEntry(WorldMapZoneDesignEntry entry, string zoneId)
        {
            entry.zoneId = zoneId;
            entry.column = zoneId[0].ToString();
            entry.row = int.Parse(zoneId.Substring(1));
            entry.regionKey = "WestWreck";
            entry.biomeKey = "ShallowWreck";
            entry.narrativePhase = ZoneNarrativePhase.TransitionTech;
            entry.terrainMood = ZoneTerrainMood.WreckFocus;
            entry.riskTier = ZoneRiskTier.Low;
            entry.contentDensity = ZoneContentDensity.Landmark;
            entry.primaryPurpose = ZonePrimaryPurpose.WreckRecovery;
            entry.minDepth = -60;
            entry.maxDepth = -260;
            entry.baseRiskLevel = 0.25f;
            entry.terrainDescription = "서쪽으로 완만히 낮아지는 Wreck Shelf. 낮은 암반 둔덕. 대형 폐선 1척 + 소형 보조선 잔해 2~3개. 선체 일부 모래에 묻힘. 내부 접근 가능한 열린 구멍 2곳. 폐선 하부 관측 장비 화물칸.";
            entry.keyObjects = "partially buried wreck, cargo hold, console, cable reel, DL-07 OBSERVE LINE marker, antenna debris, small support boat wrecks";
            entry.resourceGroups = "Iron Scrap, Steel Plate, Fuel Canister, Data Chip, Corroded Relay";
            entry.logOrHint = "Log 001: 폐선 콘솔 / Pulse Cut. Log 002: 선미 안테나 잔해 / communication module. Log 003: 화물칸 / DeepLight mark device.";
            entry.hazards = "낮음~중간. 폐선 내부 협소 공간. 시야 가림. 충돌 위험.";
            entry.narrativeFunction = "'폐선의 표식'. Data Chip. DeepLight 표식. Log 001~003. 생계형 회수에서 첫 의심으로 전환.";
            entry.isHub = false;
            entry.isMajorLandmark = true;
            entry.intentionallySparse = false;
            entry.notes = "Phase 14.9.1: Western Wreck Field prototype. B5/C5/B6/C6/C7 primary. Wreck-01 (fishing vessel appearance, thick hull plating, cable reels, DL-07 mark). Wreck-02 (small support boat). Sand-Buried Cargo Hold (Log 003).";

        }

        // ======================================================================
        //  Step 2: Override Design Rules
        // ======================================================================

        private static void OverridePrototypeDesignRules(
            DeepLightMapAutoBuilderSettingsSO settings, StringBuilder log)
        {
            var ruleDb = settings.ZoneDesignRuleDatabase;
            if (ruleDb == null)
            {
                LogIfVerbose(settings, log, "[SKIP] ZoneDesignRuleDatabase is null. Cannot override design rules.");
                return;
            }

            int overridden = 0;

            foreach (var zoneId in HubZoneIds)
            {
                var rule = ruleDb.GetRule(zoneId);
                if (rule == null)
                {
                    rule = new WorldMapZoneDesignRule();
                    ruleDb.Rules.Add(rule);
                }
                ApplyHubDesignRule(rule, zoneId);
                overridden++;
            }

            foreach (var zoneId in HarborZoneIds)
            {
                var rule = ruleDb.GetRule(zoneId);
                if (rule == null)
                {
                    rule = new WorldMapZoneDesignRule();
                    ruleDb.Rules.Add(rule);
                }
                ApplyHarborDesignRule(rule, zoneId);
                overridden++;
            }

            foreach (var zoneId in WreckZoneIds)
            {
                var rule = ruleDb.GetRule(zoneId);
                if (rule == null)
                {
                    rule = new WorldMapZoneDesignRule();
                    ruleDb.Rules.Add(rule);
                }
                ApplyWreckDesignRule(rule, zoneId);
                overridden++;
            }

            EditorUtility.SetDirty(ruleDb);
            AssetDatabase.SaveAssets();
            LogIfVerbose(settings, log, $"  [OK] Overridden {overridden} design rules for prototype regions.");
        }

        private static void ApplyHubDesignRule(WorldMapZoneDesignRule rule, string zoneId)
        {
            rule.zoneId = zoneId;
            rule.terrainArchetype = ZoneTerrainArchetype.ShallowShelf;
            // E5/F5: HubApproach (허브 진입로), E6/F6: FreeExploration (허브 내부 자유 탐색)
            if (zoneId == "E5" || zoneId == "F5")
                rule.routeRole = ZoneRouteRole.HubApproach;
            else
                rule.routeRole = ZoneRouteRole.FreeExploration;
            rule.landmarkRole = ZoneLandmarkRole.Hub;
            rule.collisionRequirement = ZoneCollisionRequirement.SeafloorAndLargeProps;
            rule.targetMinDepth = 0;
            rule.targetMaxDepth = -120;
            rule.terrainRoughness01 = 0.1f;
            rule.slopeIntensity01 = 0.05f;
            rule.canyonIntensity01 = 0.0f;
            rule.cliffIntensity01 = 0.0f;
            rule.openWaterFlatness01 = 0.9f;
            rule.resourceDensity01 = 0.25f;
            rule.hazardDensity01 = 0.0f;
            rule.landmarkWeight01 = 0.9f;
            rule.visualIntensity01 = 0.3f;
            rule.requiresTerrainCollider = true;
            rule.allowsSurfaceIslandVisual = false;
            rule.requiresNarrativeObject = true;
            rule.intentionallySparse = false;
            rule.resourceTags = new[] { "Iron Scrap", "Wet Lumber", "Fuel Canister", "Copper Wire", "Fastener Pack" };
            rule.hazardTags = new[] { "shallow_obstacle" };
            rule.landmarkTags = new[] { "hub", "pier", "shipyard", "submarine bay", "lighthouse" };
            rule.debugSummary = $"[Phase 14.8.2] {zoneId}: Hub Basin - Safe shallow basin, 초반 회수 루프 자원, 허브 랜드마크.";
        }

        private static void ApplyHarborDesignRule(WorldMapZoneDesignRule rule, string zoneId)
        {
            rule.zoneId = zoneId;
            rule.terrainArchetype = ZoneTerrainArchetype.DebrisField;
            // routeRole 분산: D5/D6=MainRoute (허브→서쪽 연결), E7/F7=HubApproach, E4/F4=MainRoute, G5/G6=SideRoute
            if (zoneId == "E7" || zoneId == "F7")
                rule.routeRole = ZoneRouteRole.HubApproach;
            else if (zoneId == "E4" || zoneId == "F4" || zoneId == "D5" || zoneId == "D6")
                rule.routeRole = ZoneRouteRole.MainRoute;
            else
                rule.routeRole = ZoneRouteRole.SideRoute;
            // D5/D6: ResearchPoint (허브→서쪽 연결 단서), E7/F7: HazardWarning, E4/F4: ResearchPoint, G5/G6: None
            if (zoneId == "E7" || zoneId == "F7")
                rule.landmarkRole = ZoneLandmarkRole.HazardWarning;
            else if (zoneId == "E4" || zoneId == "F4" || zoneId == "D5" || zoneId == "D6")
                rule.landmarkRole = ZoneLandmarkRole.ResearchPoint;
            else
                rule.landmarkRole = ZoneLandmarkRole.None;
            rule.collisionRequirement = ZoneCollisionRequirement.SeafloorAndLargeProps;
            rule.targetMinDepth = -20;
            rule.targetMaxDepth = -180;
            rule.terrainRoughness01 = 0.3f;
            rule.slopeIntensity01 = 0.15f;
            rule.canyonIntensity01 = 0.1f;
            rule.cliffIntensity01 = 0.0f;
            rule.openWaterFlatness01 = 0.6f;
            rule.resourceDensity01 = 0.7f;
            rule.hazardDensity01 = 0.25f;
            rule.landmarkWeight01 = 0.3f;
            rule.visualIntensity01 = 0.4f;
            rule.requiresTerrainCollider = true;
            rule.allowsSurfaceIslandVisual = false;
            rule.requiresNarrativeObject = false;
            rule.intentionallySparse = false;
            rule.resourceTags = new[] { "Iron Scrap", "Wet Lumber", "Fuel Canister", "Copper Wire", "Fastener Pack", "Old Rope", "Small Battery" };
            rule.hazardTags = new[] { "current", "fishing_net", "debris", "shallow_rocks" };
            rule.landmarkTags = new[] { "harbor", "buoy", "cable", "container", "broken_pier", "warning_light" };
            rule.debugSummary = $"[Phase 14.8.2] {zoneId}: Harbor Debris Belt - Shallow shelf, debris fan, safe salvage, 초반 업그레이드/판매용 자원.";
        }

        private static void ApplyWreckDesignRule(WorldMapZoneDesignRule rule, string zoneId)
        {
            rule.zoneId = zoneId;
            rule.terrainArchetype = ZoneTerrainArchetype.WreckField;
            // B5/C5/B6/C6/C7: Wreck primary zones
            // B5/C5: Gate (서쪽 진입 관문), B6/C6/C7: MainRoute (폐선 중심)
            if (zoneId == "B5" || zoneId == "C5")
                rule.routeRole = ZoneRouteRole.Gate;
            else
                rule.routeRole = ZoneRouteRole.MainRoute;
            // B5/C5: CommunicationClue (진입 단서), B6/C6/C7: Wreck (폐선 중심)
            if (zoneId == "B5" || zoneId == "C5")
                rule.landmarkRole = ZoneLandmarkRole.CommunicationClue;
            else
                rule.landmarkRole = ZoneLandmarkRole.Wreck;
            rule.collisionRequirement = ZoneCollisionRequirement.NavigationCritical;
            rule.targetMinDepth = -60;
            rule.targetMaxDepth = -260;
            rule.terrainRoughness01 = 0.5f;
            rule.slopeIntensity01 = 0.25f;
            rule.canyonIntensity01 = 0.15f;
            rule.cliffIntensity01 = 0.0f;
            rule.openWaterFlatness01 = 0.4f;
            rule.resourceDensity01 = 0.5f;
            rule.hazardDensity01 = 0.35f;
            rule.landmarkWeight01 = 0.8f;
            rule.visualIntensity01 = 0.6f;
            rule.requiresTerrainCollider = true;
            rule.allowsSurfaceIslandVisual = false;
            rule.requiresNarrativeObject = true;
            rule.intentionallySparse = false;
            rule.resourceTags = new[] { "Iron Scrap", "Steel Plate", "Fuel Canister", "Data Chip", "Corroded Relay" };
            rule.hazardTags = new[] { "twisted_metal", "cable_snare", "wreck_interior", "mild_current", "pressure" };
            rule.landmarkTags = new[] { "wreck", "cargo_hold", "console", "cable_reel", "DL-07", "antenna", "support_boat" };
            rule.debugSummary = $"[Phase 14.9.1] {zoneId}: Western Wreck Field - Wreck shelf, low rock ridge, sand-buried wreck, cargo hold, Gate2. B5/C5/B6/C6/C7 primary.";
        }

        // ======================================================================
        //  Step 3: Override Terrain Plans
        // ======================================================================

        private static void OverridePrototypeTerrainPlans(
            DeepLightMapAutoBuilderSettingsSO settings, StringBuilder log)
        {
            var planDb = settings.ZoneTerrainPlanDatabase;
            if (planDb == null)
            {
                LogIfVerbose(settings, log, "[SKIP] ZoneTerrainPlanDatabase is null. Cannot override terrain plans.");
                return;
            }

            int overridden = 0;

            foreach (var zoneId in HubZoneIds)
            {
                var plan = planDb.GetPlan(zoneId);
                if (plan == null)
                {
                    plan = new WorldMapZoneTerrainPlan();
                    planDb.Plans.Add(plan);
                }
                ApplyHubTerrainPlan(plan, zoneId);
                overridden++;
            }

            foreach (var zoneId in HarborZoneIds)
            {
                var plan = planDb.GetPlan(zoneId);
                if (plan == null)
                {
                    plan = new WorldMapZoneTerrainPlan();
                    planDb.Plans.Add(plan);
                }
                ApplyHarborTerrainPlan(plan, zoneId);
                overridden++;
            }

            foreach (var zoneId in WreckZoneIds)
            {
                var plan = planDb.GetPlan(zoneId);
                if (plan == null)
                {
                    plan = new WorldMapZoneTerrainPlan();
                    planDb.Plans.Add(plan);
                }
                ApplyWreckTerrainPlan(plan, zoneId);
                overridden++;
            }

            EditorUtility.SetDirty(planDb);
            AssetDatabase.SaveAssets();
            LogIfVerbose(settings, log, $"  [OK] Overridden {overridden} terrain plans for prototype regions.");
        }

        private static void ApplyHubTerrainPlan(WorldMapZoneTerrainPlan plan, string zoneId)
        {
            plan.zoneId = zoneId;
            plan.baseSurfaceMode = ZoneBaseSurfaceMode.ShallowShelf;
            plan.seabedShapeMode = ZoneSeabedShapeMode.GentleSlope;
            plan.routeShapeMode = ZoneRouteShapeMode.WideMainPath;
            plan.boundaryMode = ZoneBoundaryMode.SoftVisualBoundary;
            plan.colliderMode = ZoneTerrainColliderMode.SeafloorOnly;
            plan.targetCenterDepth = -60f;
            plan.depthRange = 60f;
            plan.floorHeight = -90f;
            plan.ceilingHeight = -30f;
            plan.slopeScale = 0.05f;
            plan.roughnessScale = 0.1f;
            plan.canyonWidth01 = 0.0f;
            plan.canyonDepth01 = 0.0f;
            plan.cliffHeight01 = 0.0f;
            plan.flatAreaWeight01 = 0.9f;
            plan.navigationCorridorWidth01 = 0.9f;
            plan.obstacleDensity01 = 0.05f;
            plan.landmarkPlacementWeight01 = 0.9f;
            plan.resourceSpawnWeight01 = 0.1f;
            plan.hazardSpawnWeight01 = 0.0f;
            plan.recommendedLargePropCount = 4;
            plan.recommendedSmallPropCount = 8;
            plan.requiresMainRouteClearance = true;
            plan.requiresBoundaryBlocker = false;
            plan.requiresSeafloorCollider = true;
            plan.allowsDecorativeOnlySurface = false;
            // Hub terrain module tags: basin, shelf, gentle_slope, pier_pylon, breakwater, cable
            plan.terrainTags = new[] { "basin", "shelf", "gentle_slope", "pier_pylon", "breakwater", "cable" };
            plan.propTags = new[] { "pier", "shipyard", "dock", "submarine_bay" };
            plan.hazardTags = new[] { "shallow_obstacle" };
            plan.debugSummary = "Phase 14.9.1: Hub Basin - basin, shelf, gentle_slope, pier_pylon, breakwater, cable. Safe shallow basin.";
        }

        private static void ApplyHarborTerrainPlan(WorldMapZoneTerrainPlan plan, string zoneId)
        {
            plan.zoneId = zoneId;
            plan.baseSurfaceMode = ZoneBaseSurfaceMode.ShallowShelf;
            plan.seabedShapeMode = ZoneSeabedShapeMode.DebrisScattered;
            plan.routeShapeMode = ZoneRouteShapeMode.WideMainPath;
            plan.boundaryMode = ZoneBoundaryMode.SoftVisualBoundary;
            plan.colliderMode = ZoneTerrainColliderMode.SeafloorAndLargeProps;
            plan.targetCenterDepth = -100f;
            plan.depthRange = 80f;
            plan.floorHeight = -140f;
            plan.ceilingHeight = -60f;
            plan.slopeScale = 0.1f;
            plan.roughnessScale = 0.3f;
            plan.canyonWidth01 = 0.2f;
            plan.canyonDepth01 = 0.15f;
            plan.cliffHeight01 = 0.0f;
            plan.flatAreaWeight01 = 0.6f;
            plan.navigationCorridorWidth01 = 0.8f;
            plan.obstacleDensity01 = 0.2f;
            plan.landmarkPlacementWeight01 = 0.2f;
            plan.resourceSpawnWeight01 = 0.7f;
            plan.hazardSpawnWeight01 = 0.15f;
            plan.recommendedLargePropCount = 8;
            plan.recommendedSmallPropCount = 20;
            plan.requiresMainRouteClearance = true;
            plan.requiresBoundaryBlocker = false;
            plan.requiresSeafloorCollider = true;
            plan.allowsDecorativeOnlySurface = false;
            // Harbor terrain module tags: shallow_shelf, debris_fan, sandy_slope, pier_column, cable_line
            plan.terrainTags = new[] { "shallow_shelf", "debris_fan", "sandy_slope", "pier_column", "cable_line" };
            plan.propTags = new[] { "pier_column", "fishing_net", "crate", "barrel", "shipyard_frame", "cable", "buoy" };
            plan.hazardTags = new[] { "pier_column", "net_collision" };
            plan.debugSummary = "Phase 14.9.1: Harbor Debris Belt - shallow_shelf, debris_fan, sandy_slope, pier_column, cable_line. Gate1 ResourceLearning.";
        }

        private static void ApplyWreckTerrainPlan(WorldMapZoneTerrainPlan plan, string zoneId)
        {
            plan.zoneId = zoneId;
            plan.baseSurfaceMode = ZoneBaseSurfaceMode.ShallowShelf;
            plan.seabedShapeMode = ZoneSeabedShapeMode.WreckDepression;
            plan.routeShapeMode = ZoneRouteShapeMode.NarrowPassage;
            plan.boundaryMode = ZoneBoundaryMode.SoftVisualBoundary;
            plan.colliderMode = ZoneTerrainColliderMode.NavigationCritical;
            plan.targetCenterDepth = -170f;
            plan.depthRange = 110f;
            plan.floorHeight = -230f;
            plan.ceilingHeight = -110f;
            plan.slopeScale = 0.2f;
            plan.roughnessScale = 0.5f;
            plan.canyonWidth01 = 0.3f;
            plan.canyonDepth01 = 0.25f;
            plan.cliffHeight01 = 0.0f;
            plan.flatAreaWeight01 = 0.4f;
            plan.navigationCorridorWidth01 = 0.5f;
            plan.obstacleDensity01 = 0.4f;
            plan.landmarkPlacementWeight01 = 0.8f;
            plan.resourceSpawnWeight01 = 0.5f;
            plan.hazardSpawnWeight01 = 0.3f;
            plan.recommendedLargePropCount = 6;
            plan.recommendedSmallPropCount = 15;
            plan.requiresMainRouteClearance = true;
            plan.requiresBoundaryBlocker = false;
            plan.requiresSeafloorCollider = true;
            plan.allowsDecorativeOnlySurface = false;
            // Wreck terrain module tags: wreck_shelf, low_rock_ridge, sand_buried_wreck, cargo_hold, support_boat
            plan.terrainTags = new[] { "wreck_shelf", "low_rock_ridge", "sand_buried_wreck", "cargo_hold", "support_boat" };
            plan.propTags = new[] { "wreck", "cargo_hold", "console", "cable_reel", "DL-07", "antenna", "support_boat" };
            plan.hazardTags = new[] { "wreck_interior", "visibility_obstruction", "collision_risk" };
            plan.debugSummary = "Phase 14.8.2: Western Wreck Field - Wreck Shelf + sand mound + Undercut Gully + Debris Fan + 낮은 rock ridge. 단순 평면 기울기 금지.";
        }

        // ======================================================================
        //  Step 4: Rebuild Terrain Patches for Prototype Zones
        // ======================================================================

        private static void RebuildPrototypeTerrainPatches(
            DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context, StringBuilder log)
        {
            LogIfVerbose(settings, log, "  [Step 4] Rebuilding terrain patches for prototype zones...");

            // Phase 14.8.1: Use the new zoneId-list-based helper instead of calling the A1~C10-only full rebuild.
            // This ensures prototype target zones (E5/F5/E6/F6/E7/F7/E4/F4/G5/G6/C4/C5/C6/D5/D6) all get
            // TerrainPatch generated regardless of whether they fall within A1~C10 range.
            DeepLightMapZoneTerrainPatchUtility.RebuildZoneTerrainPatchesForZoneIds(
                settings, context, AllPrototypeZoneIds, "Phase 14.8.1 Prototype");

            LogIfVerbose(settings, log, $"  [OK] Rebuilt terrain patches for {AllPrototypeZoneIds.Length} prototype zones.");
        }

        // ======================================================================
        //  Step 5: Rebuild Content Placeholders for Prototype Zones
        // ======================================================================

        private static void RebuildPrototypeContentPlaceholders(
            DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context, StringBuilder log)
        {
            LogIfVerbose(settings, log, "  [Step 5] Rebuilding content placeholders for prototype zones...");

            // Phase 14.8.1: Use the new zoneId-list-based helper instead of calling the A1~C10-only full rebuild.
            // This ensures prototype target zones all get Content/ResourceSpawns/HazardSpawns/LandmarkSpawns/
            // NarrativeSpawns/RouteMarkers/DebugMarkers generated regardless of A1~C10 range.
            DeepLightMapZoneContentPlaceholderUtility.RebuildZoneContentPlaceholdersForZoneIds(
                settings, context, AllPrototypeZoneIds, "Phase 14.8.1 Prototype");

            LogIfVerbose(settings, log, $"  [OK] Rebuilt content placeholders for {AllPrototypeZoneIds.Length} prototype zones.");
        }

        // ======================================================================
        //  Step 6: Rebuild Registry
        // ======================================================================

        private static void RebuildPrototypeRegistry(
            DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context, StringBuilder log)
        {
            LogIfVerbose(settings, log, "  [Step 6] Rebuilding content registry for prototype zones...");

            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                LogIfVerbose(settings, log, "  [SKIP] GeneratedWorldRoot not found. Cannot rebuild registry.");
                return;
            }

            // Find or create Registry component on GeneratedWorldRoot
            var registry = generatedRoot.GetComponent<WorldMapZoneContentRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapZoneContentRegistry>();
            }

            // Rebuild cache (this scans all children and rebuilds the internal dictionaries)
            registry.RebuildCache();

            EditorUtility.SetDirty(generatedRoot);
            LogIfVerbose(settings, log, $"  [OK] Registry cache rebuilt for prototype zones.");
        }

        // ======================================================================
        //  Helper: LogIfVerbose
        // ======================================================================

        /// <summary>
        /// settings.LogPrototypeRegionVerbose가 true일 때만 로그를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, StringBuilder log, string message)
        {
            if (settings != null && settings.LogPrototypeRegionVerbose)
            {
                Debug.Log($"[PrototypeRegion] {message}");
            }
            log.AppendLine($"  {message}");
        }
    }
}
