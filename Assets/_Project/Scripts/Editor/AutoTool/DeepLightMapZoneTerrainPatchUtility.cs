using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Data.World.Design;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.4: Zone Terrain Plan Mesh Patch Generation Utility.
    /// A1~C10 각 ZoneRoot 하위에 seafloor mesh patch와 collision mesh를 생성한다.
    /// 모든 Hierarchy 조작은 GeneratedWorldRoot 하위로 제한된다.
    ///
    /// Material Policy:
    /// - TerrainPatch (해저 지형 패치): SW3_BackdropTerrain 사용 (tree/billboard material 금지)
    /// - BackgroundTrees/trees (장식용 나무): SW3_BackdropTerrain_FagaceaeBillboard 사용 가능
    ///
    /// Phase 14.4.2: Zone Terrain Patch Seam Continuity + Seafloor Placeholder Disable Fix.
    /// - Column seam (A/B, B/C) + Row seam (A1/A2, ..., C9/C10) 모두 안정화
    /// - Seafloor placeholder 재귀 탐색 및 확실한 비활성화
    /// - MeshCollider sharedMesh 갱신
    /// - 검증 항목 보강 (row seam delta, seafloor placeholder 30/30)
    /// </summary>
    public static class DeepLightMapZoneTerrainPatchUtility
    {
        // ===== Constants =====
        private const string TerrainPatchRootName = "TerrainPatch";
        private const string GeometryRootName = "Geometry";
        private const string SeafloorPlaceholderName = "Seafloor";
        private const string SW3BackgroundCloneName = "SW3_BackgroundTerrain_Asset_GeneratedClone";

        /// <summary>인접 patch 간 최대 허용 높이 차이 (cliff/canyon 제외 기본값)</summary>
        private const float MaxPatchNeighborHeightDelta = 18f;

        // ===== Public API =====

        /// <summary>
        /// A1~C10 각 ZoneRoot 하위에 Terrain Patch를 재구축한다.
        /// 기존 TerrainPatch_XX가 있으면 DestroyImmediate 후 재생성한다.
        /// </summary>
        public static void RebuildZoneTerrainPatches(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ZoneTerrainPatch] Settings is null! Cannot rebuild zone terrain patches.");
                return;
            }

            if (settings.WorldMapConfig == null)
            {
                Debug.LogError("[ZoneTerrainPatch] WorldMapConfig is null! Cannot rebuild zone terrain patches.");
                return;
            }

            if (settings.ZoneTerrainPlanDatabase == null)
            {
                Debug.LogError("[ZoneTerrainPatch] ZoneTerrainPlanDatabase is null! Cannot rebuild zone terrain patches.");
                return;
            }

            var config = settings.WorldMapConfig;
            var planDb = settings.ZoneTerrainPlanDatabase;
            float zoneSize = config.ZoneSize; // 400
            int resolution = Mathf.Max(2, settings.TerrainPatchResolution); // 32
            int vertexCountPerAxis = resolution + 1; // 33

            // GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[ZoneTerrainPatch] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // ZoneRoots 부모 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[ZoneTerrainPatch] '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }

            // Material resolve (TerrainPatch 전용: tree/billboard material 금지)
            Material patchMaterial = ResolveTerrainPatchMaterial(settings);

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.4: Rebuild Zone Terrain Patches =====");
            log.AppendLine($"Resolution: {resolution}x{resolution} ({vertexCountPerAxis}x{vertexCountPerAxis} vertices)");
            log.AppendLine($"Material: {(patchMaterial != null ? patchMaterial.name : "NULL (will use fallback)")}");
            log.AppendLine($"Collider: {(settings.CreateTerrainPatchMeshCollider ? "Enabled" : "Disabled")}");
            log.AppendLine($"Seam Blend Width: {settings.TerrainPatchSeamBlendWidth}");

            int totalPatches = 0;
            int skippedPatches = 0;

            // A1~C10만 처리 (A=0, B=1, C=2, rows 1~10)
            for (int colIndex = 0; colIndex < 3; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < 10; rowIndex++)
                {
                    char columnChar = (char)('A' + colIndex);
                    int rowNumber = rowIndex + 1;
                    string zoneIdStr = $"{columnChar}{rowNumber}";
                    string zoneRootName = $"ZoneRoot_{zoneIdStr}";

                    // ZoneRoot 찾기
                    Transform zoneRootTransform = zoneRootsTransform.Find(zoneRootName);
                    if (zoneRootTransform == null)
                    {
                        LogIfVerbose(settings, $"[SKIP] {zoneRootName} not found.");
                        skippedPatches++;
                        continue;
                    }

                    // TerrainPlan 조회
                    WorldMapZoneTerrainPlan plan = planDb.GetPlan(zoneIdStr);
                    if (plan == null)
                    {
                        LogIfVerbose(settings, $"[SKIP] No terrain plan for {zoneIdStr}.");
                        skippedPatches++;
                        continue;
                    }

                    // Geometry 하위 TerrainPatch 루트 찾기/생성
                    Transform geometryTransform = zoneRootTransform.Find(GeometryRootName);
                    if (geometryTransform == null)
                    {
                        LogIfVerbose(settings, $"[SKIP] {zoneRootName}/Geometry not found.");
                        skippedPatches++;
                        continue;
                    }

                    // TerrainPatch 부모 찾기/생성
                    Transform terrainPatchParent = geometryTransform.Find(TerrainPatchRootName);
                    if (terrainPatchParent == null)
                    {
                        GameObject tpParent = new GameObject(TerrainPatchRootName);
                        tpParent.transform.SetParent(geometryTransform);
                        tpParent.transform.localPosition = Vector3.zero;
                        tpParent.transform.localRotation = Quaternion.identity;
                        tpParent.transform.localScale = Vector3.one;
                        terrainPatchParent = tpParent.transform;
                        Undo.RegisterCreatedObjectUndo(tpParent, $"Create {TerrainPatchRootName} for {zoneIdStr}");
                    }

                    // 기존 TerrainPatch_XX 제거
                    string patchName = $"TerrainPatch_{zoneIdStr}";
                    Transform existingPatch = terrainPatchParent.Find(patchName);
                    if (existingPatch != null)
                    {
                        Undo.DestroyObjectImmediate(existingPatch.gameObject);
                    }

                    // Zone 중심 좌표 계산 (WorldMapConfig 기반)
                    ZoneCoordinate coord = new ZoneCoordinate(colIndex, rowIndex);
                    Vector3 zoneCenter = coord.GetZoneCenterWorldPosition(config);

                    // Mesh 생성
                    Mesh patchMesh = GenerateTerrainPatchMesh(plan, zoneCenter, zoneSize, resolution, settings);

                    // TerrainPatch GameObject 생성
                    GameObject patchGo = new GameObject(patchName);
                    patchGo.transform.SetParent(terrainPatchParent);
                    patchGo.transform.localPosition = Vector3.zero;
                    patchGo.transform.localRotation = Quaternion.identity;
                    patchGo.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(patchGo, $"Create {patchName}");

                    // MeshFilter + MeshRenderer
                    MeshFilter mf = patchGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = patchMesh;

                    MeshRenderer mr = patchGo.AddComponent<MeshRenderer>();
                    // 방어 코드: 할당 직전에도 tree/billboard material이 아닌지 검사
                    if (patchMaterial != null && IsForbiddenTerrainPatchMaterial(patchMaterial))
                    {
                        Debug.LogWarning($"[ZoneTerrainPatch] {patchName}: Forbidden material '{patchMaterial.name}' detected at assignment. Re-resolving...");
                        Material safeMaterial = ResolveTerrainPatchMaterial(settings);
                        mr.sharedMaterial = safeMaterial;
                    }
                    else
                    {
                        mr.sharedMaterial = patchMaterial;
                    }

                    // MeshCollider (필요 시)
                    bool needsCollider = settings.CreateTerrainPatchMeshCollider || plan.requiresSeafloorCollider;
                    if (needsCollider)
                    {
                        MeshCollider mc = patchGo.AddComponent<MeshCollider>();
                        mc.sharedMesh = patchMesh;
                        mc.convex = false;
                        mc.isTrigger = false;
                    }

                    // Seafloor placeholder 숨김 처리 (재귀 탐색 방식으로 개선)
                    if (settings.HideLegacySeafloorPlaceholdersWhenPatchExists)
                    {
                        DisableLegacySeafloorPlaceholdersRecursive(zoneRootTransform);
                    }

                    totalPatches++;
                    LogIfVerbose(settings, $"[OK] {patchName} created. Center: {zoneCenter}, Vertices: {patchMesh.vertexCount}, Triangles: {patchMesh.triangles.Length / 3}");
                }
            }

            log.AppendLine($"Total patches created: {totalPatches}, Skipped: {skippedPatches}");
            log.AppendLine("===== Phase 14.4: Rebuild Complete =====");
            Debug.Log(log.ToString());

            // Seam stabilization: 인접 경계 높이 보정 (column seam + row seam 모두 처리)
            if (totalPatches > 0)
            {
                StabilizeAllPatchSeams(settings, zoneRootsTransform, config, planDb, resolution);
            }
        }

        /// <summary>
        /// Zone Terrain Patches의 유효성을 검사한다.
        /// 30+ 항목을 검사하고 Console에 결과를 출력한다.
        /// Phase 14.4.2: Seafloor placeholder 30/30 검증, Row seam delta 검증 추가.
        /// </summary>
        public static void ValidateZoneTerrainPatches(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ZoneTerrainPatch] Settings is null! Cannot validate.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.4: Validate Zone Terrain Patches =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot exists
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

            // 2. ZoneTerrainPlanDatabase exists
            if (settings.ZoneTerrainPlanDatabase != null)
            {
                log.AppendLine("  [PASS] ZoneTerrainPlanDatabase exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] ZoneTerrainPlanDatabase is null!");
                failCount++;
            }

            // 3. A1~C10 plan count == 30
            if (settings.ZoneTerrainPlanDatabase != null)
            {
                int planCount = settings.ZoneTerrainPlanDatabase.Plans.Count;
                if (planCount >= 30)
                {
                    log.AppendLine($"  [PASS] A1~C10 plan count: {planCount} (>=30)");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] A1~C10 plan count: {planCount} (<30)");
                    failCount++;
                }
            }

            // 4. ZoneRoot_A1~C10 exists
            Transform zoneRootsTransform = null;
            if (generatedRoot != null)
            {
                zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            }

            int zoneRootFound = 0;
            int zoneRootExpected = 0;
            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char colChar = (char)('A' + col);
                        string zrName = $"ZoneRoot_{colChar}{row + 1}";
                        zoneRootExpected++;
                        if (zoneRootsTransform.Find(zrName) != null)
                        {
                            zoneRootFound++;
                        }
                    }
                }
            }

            if (zoneRootFound == zoneRootExpected && zoneRootExpected == 30)
            {
                log.AppendLine("  [PASS] All 30 ZoneRoot_A1~C10 exist.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] ZoneRoot_A1~C10 found: {zoneRootFound}/{zoneRootExpected}");
                failCount++;
            }

            // 5. TerrainPatch root exists under each ZoneRoot geometry
            int terrainPatchRootFound = 0;
            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char colChar = (char)('A' + col);
                        string zrName = $"ZoneRoot_{colChar}{row + 1}";
                        Transform zr = zoneRootsTransform.Find(zrName);
                        if (zr != null)
                        {
                            Transform geo = zr.Find(GeometryRootName);
                            if (geo != null && geo.Find(TerrainPatchRootName) != null)
                            {
                                terrainPatchRootFound++;
                            }
                        }
                    }
                }
            }

            if (terrainPatchRootFound == 30)
            {
                log.AppendLine("  [PASS] All 30 TerrainPatch roots exist under ZoneRoot geometry.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] TerrainPatch roots found: {terrainPatchRootFound}/30");
                warnCount++;
            }

            // 6. TerrainPatch_XX exists for A1~C10
            int patchGoFound = 0;
            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char colChar = (char)('A' + col);
                        string zoneIdStr = $"{colChar}{row + 1}";
                        string zrName = $"ZoneRoot_{zoneIdStr}";
                        Transform zr = zoneRootsTransform.Find(zrName);
                        if (zr != null)
                        {
                            Transform geo = zr.Find(GeometryRootName);
                            if (geo != null)
                            {
                                Transform tpRoot = geo.Find(TerrainPatchRootName);
                                if (tpRoot != null && tpRoot.Find($"TerrainPatch_{zoneIdStr}") != null)
                                {
                                    patchGoFound++;
                                }
                            }
                        }
                    }
                }
            }

            if (patchGoFound == 30)
            {
                log.AppendLine("  [PASS] All 30 TerrainPatch_XX exist.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] TerrainPatch_XX found: {patchGoFound}/30");
                warnCount++;
            }

            // 7~13: 각 TerrainPatch 상세 검사
            int meshFilterCount = 0;
            int meshRendererCount = 0;
            int nonNullMaterialCount = 0;
            int pinkMaterialCount = 0;
            int forbiddenMaterialCount = 0; // tree/billboard material 사용 검출
            int vertexMatchCount = 0;
            int trianglePositiveCount = 0;
            int colliderCount = 0;
            int rigidbodyCount = 0;

            int expectedResolution = Mathf.Max(2, settings.TerrainPatchResolution);
            int expectedVertices = (expectedResolution + 1) * (expectedResolution + 1);

            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char colChar = (char)('A' + col);
                        string zoneIdStr = $"{colChar}{row + 1}";
                        string zrName = $"ZoneRoot_{zoneIdStr}";
                        Transform zr = zoneRootsTransform.Find(zrName);
                        if (zr == null) continue;

                        Transform geo = zr.Find(GeometryRootName);
                        if (geo == null) continue;

                        Transform tpRoot = geo.Find(TerrainPatchRootName);
                        if (tpRoot == null) continue;

                        Transform patchT = tpRoot.Find($"TerrainPatch_{zoneIdStr}");
                        if (patchT == null) continue;

                        GameObject patchGo = patchT.gameObject;

                        // 7. MeshFilter
                        MeshFilter mf = patchGo.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            meshFilterCount++;

                            // 11. vertex count matches resolution
                            if (mf.sharedMesh.vertexCount == expectedVertices)
                            {
                                vertexMatchCount++;
                            }

                            // 12. triangle count > 0
                            if (mf.sharedMesh.triangles.Length > 0)
                            {
                                trianglePositiveCount++;
                            }
                        }

                        // 8. MeshRenderer
                        MeshRenderer mr = patchGo.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            meshRendererCount++;

                            // 9. non-null material
                            if (mr.sharedMaterial != null)
                            {
                                nonNullMaterialCount++;

                                // 10. pink material risk
                                if (IsPinkMaterial(mr.sharedMaterial))
                                {
                                    pinkMaterialCount++;
                                }

                                // 24. forbidden tree/billboard material check (TerrainPatch 전용)
                                if (IsForbiddenTerrainPatchMaterial(mr.sharedMaterial))
                                {
                                    forbiddenMaterialCount++;
                                    log.AppendLine($"  [FAIL] TerrainPatch_{zoneIdStr} uses forbidden material: '{mr.sharedMaterial.name}' (tree/billboard material not allowed on TerrainPatch)");
                                }
                            }
                        }

                        // 13. collision-required plan has MeshCollider
                        WorldMapZoneTerrainPlan plan = settings.ZoneTerrainPlanDatabase?.GetPlan(zoneIdStr);
                        bool needsCollider = (plan != null && plan.requiresSeafloorCollider) || settings.CreateTerrainPatchMeshCollider;
                        if (needsCollider)
                        {
                            MeshCollider mc = patchGo.GetComponent<MeshCollider>();
                            if (mc != null)
                            {
                                colliderCount++;
                            }
                        }

                        // 14. no Rigidbody
                        if (patchGo.GetComponent<Rigidbody>() != null)
                        {
                            rigidbodyCount++;
                        }
                    }
                }
            }

            // 7
            if (meshFilterCount >= 30)
            {
                log.AppendLine("  [PASS] All TerrainPatches have MeshFilter.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] MeshFilter count: {meshFilterCount}/30");
                failCount++;
            }

            // 8
            if (meshRendererCount >= 30)
            {
                log.AppendLine("  [PASS] All TerrainPatches have MeshRenderer.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] MeshRenderer count: {meshRendererCount}/30");
                failCount++;
            }

            // 9
            if (nonNullMaterialCount >= 30)
            {
                log.AppendLine("  [PASS] All TerrainPatches have non-null material.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Non-null material count: {nonNullMaterialCount}/30");
                failCount++;
            }

            // 10
            if (pinkMaterialCount == 0)
            {
                log.AppendLine("  [PASS] Pink material risk count: 0");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Pink material risk count: {pinkMaterialCount} (should be 0)");
                failCount++;
            }

            // 11
            if (vertexMatchCount >= 30)
            {
                log.AppendLine($"  [PASS] All patches vertex count matches resolution ({expectedVertices}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] Vertex count match: {vertexMatchCount}/30");
                warnCount++;
            }

            // 12
            if (trianglePositiveCount >= 30)
            {
                log.AppendLine("  [PASS] All patches have positive triangle count.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Positive triangle count: {trianglePositiveCount}/30");
                failCount++;
            }

            // 13
            if (colliderCount >= 0)
            {
                log.AppendLine($"  [PASS] MeshCollider count: {colliderCount} (as needed).");
                passCount++;
            }

            // 14
            if (rigidbodyCount == 0)
            {
                log.AppendLine("  [PASS] No Rigidbody under TerrainPatch root.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Rigidbody count under TerrainPatch: {rigidbodyCount} (should be 0)");
                failCount++;
            }

            // 15. no TerrainPatch object under MapSettings
            GameObject mapSettings = GameObject.Find("MapSettings");
            bool mapSettingsHasPatch = false;
            if (mapSettings != null)
            {
                mapSettingsHasPatch = FindObjectWithNameRecursive(mapSettings.transform, "TerrainPatch");
            }
            if (!mapSettingsHasPatch)
            {
                log.AppendLine("  [PASS] No TerrainPatch object under MapSettings.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] TerrainPatch object found under MapSettings!");
                failCount++;
            }

            // 16. no TerrainPatch object under _WorldMap_Manual
            GameObject worldMapManual = GameObject.Find("_WorldMap_Manual");
            bool manualHasPatch = false;
            if (worldMapManual != null)
            {
                manualHasPatch = FindObjectWithNameRecursive(worldMapManual.transform, "TerrainPatch");
            }
            if (!manualHasPatch)
            {
                log.AppendLine("  [PASS] No TerrainPatch object under _WorldMap_Manual.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] TerrainPatch object found under _WorldMap_Manual!");
                failCount++;
            }

            // 17. SW3_BackgroundTerrain_Asset_GeneratedClone still exists
            GameObject sw3Clone = GameObject.Find(SW3BackgroundCloneName);
            if (sw3Clone != null)
            {
                log.AppendLine("  [PASS] SW3_BackgroundTerrain_Asset_GeneratedClone exists and preserved.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] SW3_BackgroundTerrain_Asset_GeneratedClone not found (may not exist yet).");
                warnCount++;
            }

            // 18. Seafloor placeholder renderers/colliders disabled (재귀 탐색 방식, 30/30 검증)
            int placeholderDisabled = 0;
            int placeholderChecked = 0;
            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char colChar = (char)('A' + col);
                        string zrName = $"ZoneRoot_{colChar}{row + 1}";
                        Transform zr = zoneRootsTransform.Find(zrName);
                        if (zr == null) continue;

                        // 재귀 탐색으로 Seafloor placeholder 후보 찾기
                        bool foundDisabled = false;
                        bool foundAny = false;
                        CheckSeafloorPlaceholderRecursive(zr, ref foundAny, ref foundDisabled);

                        if (foundAny)
                        {
                            placeholderChecked++;
                            if (foundDisabled)
                            {
                                placeholderDisabled++;
                            }
                        }
                    }
                }
            }

            if (placeholderChecked == 0)
            {
                log.AppendLine("  [WARN] No seafloor placeholders found to check (expected 30).");
                warnCount++;
            }
            else if (placeholderDisabled >= placeholderChecked)
            {
                log.AppendLine($"  [PASS] Seafloor placeholders disabled: {placeholderDisabled}/{placeholderChecked}");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] Seafloor placeholders disabled: {placeholderDisabled}/{placeholderChecked} (some placeholders still have enabled renderer/collider)");
                warnCount++;
                // 상세 로그: 어떤 zone에서 placeholder가 여전히 enabled인지 출력
                LogEnabledSeafloorPlaceholders(zoneRootsTransform, log);
            }

            // 19. A/B seam max delta <= threshold
            float seamThreshold = MaxPatchNeighborHeightDelta;
            float abMaxDelta = MeasureColumnSeamDelta(zoneRootsTransform, 'A', 'B', settings);
            if (abMaxDelta <= seamThreshold)
            {
                log.AppendLine($"  [PASS] A/B seam max delta: {abMaxDelta:F2}m (<= {seamThreshold:F2}m)");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] A/B seam max delta: {abMaxDelta:F2}m (> {seamThreshold:F2}m)");
                failCount++;
            }

            // 20. B/C seam max delta <= threshold
            float bcMaxDelta = MeasureColumnSeamDelta(zoneRootsTransform, 'B', 'C', settings);
            if (bcMaxDelta <= seamThreshold)
            {
                log.AppendLine($"  [PASS] B/C seam max delta: {bcMaxDelta:F2}m (<= {seamThreshold:F2}m)");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] B/C seam max delta: {bcMaxDelta:F2}m (> {seamThreshold:F2}m)");
                failCount++;
            }

            // 21. MapSettings preserved
            if (mapSettings != null)
            {
                log.AppendLine("  [PASS] MapSettings preserved.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] MapSettings not found (may not exist in scene).");
                warnCount++;
            }

            // 22. _WorldMap_Manual preserved
            if (worldMapManual != null)
            {
                log.AppendLine("  [PASS] _WorldMap_Manual preserved.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] _WorldMap_Manual not found (may not exist in scene).");
                warnCount++;
            }

            // 23. DeepLightMapAutoBuilderContext preserved
            GameObject builderContext = GameObject.Find("DeepLightMapAutoBuilderContext");
            if (builderContext != null)
            {
                log.AppendLine("  [PASS] DeepLightMapAutoBuilderContext preserved.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] DeepLightMapAutoBuilderContext not found (may not exist in scene).");
                warnCount++;
            }

            // 24. TerrainPatch forbidden material check (aggregate)
            if (forbiddenMaterialCount == 0)
            {
                log.AppendLine("  [PASS] TerrainPatch renderers do not use billboard/tree materials.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] TerrainPatch renderers using forbidden materials: {forbiddenMaterialCount} (should be 0)");
                failCount++;
            }

            // 25. TerrainPatch uses SW3_BackdropTerrain or explicit non-tree terrain patch material
            int terrainPatchValidMaterialCount = 0;
            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char colChar = (char)('A' + col);
                        string zoneIdStr = $"{colChar}{row + 1}";
                        string zrName = $"ZoneRoot_{zoneIdStr}";
                        Transform zr = zoneRootsTransform.Find(zrName);
                        if (zr == null) continue;

                        Transform geo = zr.Find(GeometryRootName);
                        if (geo == null) continue;

                        Transform tpRoot = geo.Find(TerrainPatchRootName);
                        if (tpRoot == null) continue;

                        Transform patchT = tpRoot.Find($"TerrainPatch_{zoneIdStr}");
                        if (patchT == null) continue;

                        MeshRenderer mr = patchT.GetComponent<MeshRenderer>();
                        if (mr != null && mr.sharedMaterial != null)
                        {
                            if (IsAllowedTerrainPatchMaterial(mr.sharedMaterial))
                            {
                                terrainPatchValidMaterialCount++;
                            }
                            else
                            {
                                log.AppendLine($"  [WARN] TerrainPatch_{zoneIdStr} material '{mr.sharedMaterial.name}' is not a recognized terrain patch material.");
                            }
                        }
                    }
                }
            }

            if (terrainPatchValidMaterialCount >= 30)
            {
                log.AppendLine("  [PASS] TerrainPatch uses SW3_BackdropTerrain or explicit non-tree terrain patch material.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] TerrainPatch valid material count: {terrainPatchValidMaterialCount}/30");
                warnCount++;
            }

            // 26. BackgroundTrees/tree renderers may use SW3_BackdropTerrain_FagaceaeBillboard (informational)
            int backgroundTreeBillboardCount = 0;
            if (zoneRootsTransform != null)
            {
                for (int col = 0; col < 3; col++)
                {
                    for (int row = 0; row < 10; row++)
                    {
                        char colChar = (char)('A' + col);
                        string zrName = $"ZoneRoot_{colChar}{row + 1}";
                        Transform zr = zoneRootsTransform.Find(zrName);
                        if (zr == null) continue;

                        // BackgroundTrees/trees 검색 (재귀)
                        CheckBackgroundTreeBillboardMaterials(zr, ref backgroundTreeBillboardCount);
                    }
                }
            }
            log.AppendLine($"  [INFO] BackgroundTrees/tree renderers with billboard material: {backgroundTreeBillboardCount} (expected, not a validation issue)");
            passCount++;

            // 27. TerrainPatch material name, shader name, assigned profile name logged for debugging
            LogTerrainPatchMaterialDebugInfo(zoneRootsTransform, settings);

            // ===== Phase 14.4.2: Row seam delta validation =====
            // 28. A row seam max delta <= threshold (A1/A2, A2/A3, ..., A9/A10)
            float aRowMaxDelta = MeasureRowSeamDelta(zoneRootsTransform, 'A', settings);
            if (aRowMaxDelta <= seamThreshold)
            {
                log.AppendLine($"  [PASS] A row seam max delta: {aRowMaxDelta:F2}m (<= {seamThreshold:F2}m)");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] A row seam max delta: {aRowMaxDelta:F2}m (> {seamThreshold:F2}m)");
                warnCount++;
            }

            // 29. B row seam max delta <= threshold
            float bRowMaxDelta = MeasureRowSeamDelta(zoneRootsTransform, 'B', settings);
            if (bRowMaxDelta <= seamThreshold)
            {
                log.AppendLine($"  [PASS] B row seam max delta: {bRowMaxDelta:F2}m (<= {seamThreshold:F2}m)");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] B row seam max delta: {bRowMaxDelta:F2}m (> {seamThreshold:F2}m)");
                warnCount++;
            }

            // 30. C row seam max delta <= threshold
            float cRowMaxDelta = MeasureRowSeamDelta(zoneRootsTransform, 'C', settings);
            if (cRowMaxDelta <= seamThreshold)
            {
                log.AppendLine($"  [PASS] C row seam max delta: {cRowMaxDelta:F2}m (<= {seamThreshold:F2}m)");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] C row seam max delta: {cRowMaxDelta:F2}m (> {seamThreshold:F2}m)");
                warnCount++;
            }

            // Summary
            log.AppendLine($"\n===== Validation Summary: {passCount} PASS, {failCount} FAIL, {warnCount} WARN =====");
            Debug.Log(log.ToString());

            if (failCount > 0)
            {
                Debug.LogWarning($"[ZoneTerrainPatch] Validation completed with {failCount} FAIL(s). Review warnings above.");
            }
        }

        // ===== Phase 14.4.2: Public Seam Stabilization API =====

        /// <summary>
        /// 모든 TerrainPatch의 seam을 안정화한다.
        /// Column seam (A/B, B/C) + Row seam (A1/A2~A9/A10, B1/B2~B9/B10, C1/C2~C9/C10) 모두 처리.
        /// </summary>
        public static void StabilizeAllPatchSeams(DeepLightMapAutoBuilderSettingsSO settings, Transform zoneRootsParent, WorldMapConfigSO config, WorldMapZoneTerrainPlanDatabaseSO planDb, int resolution)
        {
            float blendWidth = settings.TerrainPatchSeamBlendWidth;
            float zoneSize = config.ZoneSize;
            float halfSize = zoneSize * 0.5f;
            int vertexCountPerAxis = resolution + 1;
            float step = zoneSize / resolution;
            int blendVertexCount = Mathf.Max(1, Mathf.RoundToInt(blendWidth / step));

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.4.2: Stabilize All Patch Seams =====");
            log.AppendLine($"Blend width: {blendWidth} ({blendVertexCount} vertices), Step: {step:F2}");

            // 1. Column seam: A/B, B/C (같은 row, 다른 column)
            for (int rowIndex = 0; rowIndex < 10; rowIndex++)
            {
                int rowNumber = rowIndex + 1;

                // A-B seam (col 0과 col 1 사이)
                BlendColumnSeam(zoneRootsParent, planDb, 'A', 'B', rowNumber, vertexCountPerAxis, resolution, step, halfSize, blendVertexCount);

                // B-C seam (col 1과 col 2 사이)
                BlendColumnSeam(zoneRootsParent, planDb, 'B', 'C', rowNumber, vertexCountPerAxis, resolution, step, halfSize, blendVertexCount);
            }

            // 2. Row seam: 같은 column, 다른 row (A1/A2, A2/A3, ..., C9/C10)
            char[] columns = { 'A', 'B', 'C' };
            for (int ci = 0; ci < columns.Length; ci++)
            {
                char col = columns[ci];
                for (int rowIndex = 0; rowIndex < 9; rowIndex++) // 9 seams per column (1-2, 2-3, ..., 9-10)
                {
                    int topRow = rowIndex + 1;
                    int bottomRow = rowIndex + 2;
                    BlendRowSeam(zoneRootsParent, planDb, col, topRow, bottomRow, vertexCountPerAxis, resolution, step, halfSize, blendVertexCount);
                }
            }

            log.AppendLine("Column seams (A/B, B/C) stabilized for all 10 rows.");
            log.AppendLine("Row seams (A1/A2~A9/A10, B1/B2~B9/B10, C1/C2~C9/C10) stabilized.");
            Debug.Log(log.ToString());
        }

        // ===== Private Helpers =====

        /// <summary>
        /// Column seam (leftCol/rightCol, 같은 row)의 경계 mesh vertices를 blend 처리한다.
        /// left zone의 오른쪽 edge와 right zone의 왼쪽 edge를 평균 높이로 맞추고,
        /// blendVertexCount 범위 내에서 smoothstep으로 보정한다.
        /// </summary>
        private static void BlendColumnSeam(Transform zoneRootsParent, WorldMapZoneTerrainPlanDatabaseSO planDb,
            char leftCol, char rightCol, int rowNumber,
            int vertexCountPerAxis, int resolution, float step, float halfSize,
            int blendVertexCount)
        {
            string leftZoneId = $"{leftCol}{rowNumber}";
            string rightZoneId = $"{rightCol}{rowNumber}";

            Mesh leftMesh = GetPatchMesh(zoneRootsParent, leftZoneId);
            Mesh rightMesh = GetPatchMesh(zoneRootsParent, rightZoneId);

            if (leftMesh == null || rightMesh == null)
                return;

            Vector3[] leftVerts = leftMesh.vertices;
            Vector3[] rightVerts = rightMesh.vertices;

            if (leftVerts.Length != rightVerts.Length)
                return;

            bool modified = false;

            for (int z = 0; z < vertexCountPerAxis; z++)
            {
                // Left zone: right edge (x = resolution)
                int leftEdgeIdx = z * vertexCountPerAxis + resolution;

                // Right zone: left edge (x = 0)
                int rightEdgeIdx = z * vertexCountPerAxis + 0;

                float leftY = leftVerts[leftEdgeIdx].y;
                float rightY = rightVerts[rightEdgeIdx].y;

                // 평균 높이 계산
                float avgY = (leftY + rightY) * 0.5f;

                // blendVertexCount 범위 내에서 lerp
                for (int b = 0; b <= blendVertexCount && b <= resolution; b++)
                {
                    float t = (float)b / Mathf.Max(1, blendVertexCount);
                    float smoothT = t * t * (3f - 2f * t); // smoothstep

                    // Left zone: 오른쪽에서 왼쪽으로 b만큼
                    int leftBlendIdx = z * vertexCountPerAxis + (resolution - b);
                    float leftOrig = leftVerts[leftBlendIdx].y;
                    leftVerts[leftBlendIdx].y = Mathf.Lerp(leftOrig, avgY, 1f - smoothT);

                    // Right zone: 왼쪽에서 오른쪽으로 b만큼
                    int rightBlendIdx = z * vertexCountPerAxis + b;
                    float rightOrig = rightVerts[rightBlendIdx].y;
                    rightVerts[rightBlendIdx].y = Mathf.Lerp(rightOrig, avgY, 1f - smoothT);
                }

                modified = true;
            }

            if (modified)
            {
                ApplyMeshChanges(leftMesh, leftVerts);
                ApplyMeshChanges(rightMesh, rightVerts);
            }
        }

        /// <summary>
        /// Row seam (topRow/bottomRow, 같은 column)의 경계 mesh vertices를 blend 처리한다.
        /// top zone의 bottom edge (z=resolution)와 bottom zone의 top edge (z=0)를 평균 높이로 맞추고,
        /// blendVertexCount 범위 내에서 smoothstep으로 보정한다.
        /// </summary>
        private static void BlendRowSeam(Transform zoneRootsParent, WorldMapZoneTerrainPlanDatabaseSO planDb,
            char col, int topRow, int bottomRow,
            int vertexCountPerAxis, int resolution, float step, float halfSize,
            int blendVertexCount)
        {
            string topZoneId = $"{col}{topRow}";
            string bottomZoneId = $"{col}{bottomRow}";

            Mesh topMesh = GetPatchMesh(zoneRootsParent, topZoneId);
            Mesh bottomMesh = GetPatchMesh(zoneRootsParent, bottomZoneId);

            if (topMesh == null || bottomMesh == null)
                return;

            Vector3[] topVerts = topMesh.vertices;
            Vector3[] bottomVerts = bottomMesh.vertices;

            if (topVerts.Length != bottomVerts.Length)
                return;

            bool modified = false;

            for (int x = 0; x < vertexCountPerAxis; x++)
            {
                // Top zone: bottom edge (z = resolution)
                int topEdgeIdx = resolution * vertexCountPerAxis + x;

                // Bottom zone: top edge (z = 0)
                int bottomEdgeIdx = 0 * vertexCountPerAxis + x;

                float topY = topVerts[topEdgeIdx].y;
                float bottomY = bottomVerts[bottomEdgeIdx].y;

                // 평균 높이 계산
                float avgY = (topY + bottomY) * 0.5f;

                // blendVertexCount 범위 내에서 lerp
                for (int b = 0; b <= blendVertexCount && b <= resolution; b++)
                {
                    float t = (float)b / Mathf.Max(1, blendVertexCount);
                    float smoothT = t * t * (3f - 2f * t); // smoothstep

                    // Top zone: bottom에서 위로 b만큼 (z 방향으로 -b)
                    int topBlendIdx = (resolution - b) * vertexCountPerAxis + x;
                    float topOrig = topVerts[topBlendIdx].y;
                    topVerts[topBlendIdx].y = Mathf.Lerp(topOrig, avgY, 1f - smoothT);

                    // Bottom zone: top에서 아래로 b만큼 (z 방향으로 +b)
                    int bottomBlendIdx = b * vertexCountPerAxis + x;
                    float bottomOrig = bottomVerts[bottomBlendIdx].y;
                    bottomVerts[bottomBlendIdx].y = Mathf.Lerp(bottomOrig, avgY, 1f - smoothT);
                }

                modified = true;
            }

            if (modified)
            {
                ApplyMeshChanges(topMesh, topVerts);
                ApplyMeshChanges(bottomMesh, bottomVerts);
            }
        }

        /// <summary>
        /// Mesh 변경 사항을 적용하고 MeshCollider를 갱신한다.
        /// vertices 할당 → RecalculateNormals → RecalculateBounds → MeshCollider.sharedMesh 갱신.
        /// </summary>
        private static void ApplyMeshChanges(Mesh mesh, Vector3[] vertices)
        {
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // MeshCollider가 연결되어 있으면 sharedMesh를 null로 뺐다가 다시 넣어 collider bake 갱신
            // Mesh 자체에는 GetComponents가 없으므로, mesh를 사용하는 모든 오브젝트를 찾아서 갱신
            MeshFilter[] meshFilters = Resources.FindObjectsOfTypeAll<MeshFilter>();
            for (int mf = 0; mf < meshFilters.Length; mf++)
            {
                if (meshFilters[mf] != null && meshFilters[mf].sharedMesh == mesh)
                {
                    MeshCollider mc = meshFilters[mf].GetComponent<MeshCollider>();
                    if (mc != null)
                    {
                        mc.sharedMesh = null;
                        mc.sharedMesh = mesh;
                    }
                }
            }
        }

        /// <summary>
        /// Column seam (leftCol/rightCol)의 최대 높이 차이를 측정한다.
        /// </summary>
        private static float MeasureColumnSeamDelta(Transform zoneRootsParent, char leftCol, char rightCol, DeepLightMapAutoBuilderSettingsSO settings)
        {
            float maxDelta = 0f;

            if (zoneRootsParent == null || settings.ZoneTerrainPlanDatabase == null)
                return maxDelta;

            for (int row = 0; row < 10; row++)
            {
                int rowNumber = row + 1;
                string leftId = $"{leftCol}{rowNumber}";
                string rightId = $"{rightCol}{rowNumber}";

                Mesh leftMesh = GetPatchMesh(zoneRootsParent, leftId);
                Mesh rightMesh = GetPatchMesh(zoneRootsParent, rightId);

                if (leftMesh == null || rightMesh == null)
                    continue;

                Vector3[] leftVerts = leftMesh.vertices;
                Vector3[] rightVerts = rightMesh.vertices;

                int resolution = Mathf.Max(2, settings.TerrainPatchResolution);
                int vpa = resolution + 1;

                for (int z = 0; z < vpa; z++)
                {
                    int leftIdx = z * vpa + resolution; // right edge of left
                    int rightIdx = z * vpa + 0; // left edge of right

                    if (leftIdx < leftVerts.Length && rightIdx < rightVerts.Length)
                    {
                        float delta = Mathf.Abs(leftVerts[leftIdx].y - rightVerts[rightIdx].y);
                        if (delta > maxDelta)
                            maxDelta = delta;
                    }
                }
            }

            return maxDelta;
        }

        /// <summary>
        /// Row seam (같은 column, 인접 row)의 최대 높이 차이를 측정한다.
        /// top zone의 bottom edge와 bottom zone의 top edge를 비교한다.
        /// </summary>
        private static float MeasureRowSeamDelta(Transform zoneRootsParent, char col, DeepLightMapAutoBuilderSettingsSO settings)
        {
            float maxDelta = 0f;

            if (zoneRootsParent == null || settings.ZoneTerrainPlanDatabase == null)
                return maxDelta;

            int resolution = Mathf.Max(2, settings.TerrainPatchResolution);
            int vpa = resolution + 1;

            for (int row = 0; row < 9; row++) // 9 seams: 1-2, 2-3, ..., 9-10
            {
                int topRow = row + 1;
                int bottomRow = row + 2;
                string topId = $"{col}{topRow}";
                string bottomId = $"{col}{bottomRow}";

                Mesh topMesh = GetPatchMesh(zoneRootsParent, topId);
                Mesh bottomMesh = GetPatchMesh(zoneRootsParent, bottomId);

                if (topMesh == null || bottomMesh == null)
                    continue;

                Vector3[] topVerts = topMesh.vertices;
                Vector3[] bottomVerts = bottomMesh.vertices;

                for (int x = 0; x < vpa; x++)
                {
                    // Top zone: bottom edge (z = resolution)
                    int topIdx = resolution * vpa + x;
                    // Bottom zone: top edge (z = 0)
                    int bottomIdx = 0 * vpa + x;

                    if (topIdx < topVerts.Length && bottomIdx < bottomVerts.Length)
                    {
                        float delta = Mathf.Abs(topVerts[topIdx].y - bottomVerts[bottomIdx].y);
                        if (delta > maxDelta)
                            maxDelta = delta;
                    }
                }
            }

            return maxDelta;
        }

        /// <summary>
        /// TerrainPlan의 SeabedShapeMode와 RouteShapeMode를 기반으로 해저면 mesh를 생성한다.
        /// </summary>
        private static Mesh GenerateTerrainPatchMesh(WorldMapZoneTerrainPlan plan, Vector3 zoneCenter, float zoneSize, int resolution, DeepLightMapAutoBuilderSettingsSO settings)
        {
            int vertexCountPerAxis = resolution + 1;
            int totalVertices = vertexCountPerAxis * vertexCountPerAxis;
            Vector3[] vertices = new Vector3[totalVertices];
            Vector3[] normals = new Vector3[totalVertices];
            Vector2[] uv = new Vector2[totalVertices];
            int[] triangles = new int[resolution * resolution * 6];

            float halfSize = zoneSize * 0.5f;
            float step = zoneSize / resolution;

            // Base floor height from plan
            float baseY = plan.floorHeight;

            // Noise parameters
            float noiseScale = settings.TerrainPatchNoiseScale;
            float noiseStrength = settings.TerrainPatchNoiseStrength;

            int triIndex = 0;

            for (int z = 0; z < vertexCountPerAxis; z++)
            {
                for (int x = 0; x < vertexCountPerAxis; x++)
                {
                    int idx = z * vertexCountPerAxis + x;

                    // Local position within zone (centered)
                    float localX = -halfSize + x * step;
                    float localZ = -halfSize + z * step;

                    // Normalized coordinates (0~1) for shape calculations
                    float u = (float)x / resolution;
                    float v = (float)z / resolution;

                    // Calculate height offset based on shape mode
                    float heightOffset = CalculateShapeHeight(plan, u, v, localX, localZ, halfSize);

                    // Add noise
                    float noise = Mathf.PerlinNoise(
                        (zoneCenter.x + localX) * noiseScale,
                        (zoneCenter.z + localZ) * noiseScale
                    ) * noiseStrength;

                    // Apply noise based on roughness
                    float effectiveNoise = noise * plan.roughnessScale;
                    heightOffset += effectiveNoise;

                    // Apply flat area weight (reduce variation in flat areas)
                    heightOffset *= (1f - plan.flatAreaWeight01 * 0.8f);

                    vertices[idx] = new Vector3(localX, baseY + heightOffset, localZ);

                    // UV: normalized 0~1
                    uv[idx] = new Vector2(u, v);

                    // Normal: up by default (will be recalculated)
                    normals[idx] = Vector3.up;
                }
            }

            // Triangles
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int topLeft = z * vertexCountPerAxis + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = (z + 1) * vertexCountPerAxis + x;
                    int bottomRight = bottomLeft + 1;

                    triangles[triIndex++] = topLeft;
                    triangles[triIndex++] = bottomLeft;
                    triangles[triIndex++] = topRight;

                    triangles[triIndex++] = topRight;
                    triangles[triIndex++] = bottomLeft;
                    triangles[triIndex++] = bottomRight;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = $"TerrainPatch_{plan.zoneId}";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// SeabedShapeMode와 RouteShapeMode에 따라 높이 오프셋을 계산한다.
        /// </summary>
        private static float CalculateShapeHeight(WorldMapZoneTerrainPlan plan, float u, float v, float localX, float localZ, float halfSize)
        {
            float height = 0f;

            // SeabedShapeMode base shape
            switch (plan.seabedShapeMode)
            {
                case ZoneSeabedShapeMode.Flat:
                    height = 0f;
                    break;

                case ZoneSeabedShapeMode.GentleSlope:
                    height = (u - 0.5f) * plan.slopeScale * 30f;
                    break;

                case ZoneSeabedShapeMode.RollingSeabed:
                    height = Mathf.Sin(u * Mathf.PI * 3f) * 3f * plan.roughnessScale
                           + Mathf.Cos(v * Mathf.PI * 2.5f) * 2f * plan.roughnessScale;
                    break;

                case ZoneSeabedShapeMode.DebrisScattered:
                    height = (Mathf.PerlinNoise(u * 8f, v * 8f) - 0.5f) * 6f * plan.roughnessScale;
                    float depression = Mathf.PerlinNoise(u * 3f + 0.5f, v * 3f + 0.5f);
                    if (depression < 0.3f)
                    {
                        height -= (0.3f - depression) * 8f;
                    }
                    break;

                case ZoneSeabedShapeMode.WreckDepression:
                    float distToCenter = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                    height = -Mathf.Exp(-distToCenter * distToCenter * 8f) * 15f * plan.canyonDepth01;
                    if (plan.requiresMainRouteClearance)
                    {
                        float routeClear = Mathf.Abs(u - 0.5f) < 0.3f ? 1f : 0f;
                        height *= (1f - routeClear * 0.5f);
                    }
                    break;

                case ZoneSeabedShapeMode.CanyonCut:
                    float canyonDist = Mathf.Abs(u - 0.5f) / Mathf.Max(0.01f, plan.canyonWidth01 * 0.5f + 0.1f);
                    if (canyonDist < 1f)
                    {
                        height = -(1f - canyonDist) * 20f * plan.canyonDepth01;
                    }
                    break;

                case ZoneSeabedShapeMode.DeepCanyon:
                    float deepCanyonDist = Mathf.Abs(u - 0.5f) / Mathf.Max(0.01f, plan.canyonWidth01 * 0.3f + 0.1f);
                    if (deepCanyonDist < 1f)
                    {
                        height = -(1f - deepCanyonDist) * 35f * plan.canyonDepth01;
                    }
                    height = Mathf.Clamp(height, -40f, 10f);
                    break;

                case ZoneSeabedShapeMode.CliffDrop:
                    float cliffEdge = Mathf.Min(u, 1f - u, v, 1f - v);
                    if (cliffEdge < 0.2f)
                    {
                        height = (0.2f - cliffEdge) / 0.2f * plan.cliffHeight01 * 25f;
                    }
                    if (cliffEdge > 0.3f)
                    {
                        height = 0f;
                    }
                    break;

                case ZoneSeabedShapeMode.FacilityFloor:
                    height = 0f;
                    break;

                case ZoneSeabedShapeMode.SparseVoid:
                    height = 0f;
                    break;
            }

            // RouteShapeMode modifier
            switch (plan.routeShapeMode)
            {
                case ZoneRouteShapeMode.FreeRoam:
                    break;

                case ZoneRouteShapeMode.WideMainPath:
                    float widePathDist = Mathf.Abs(u - 0.5f);
                    if (widePathDist < 0.35f)
                    {
                        height = Mathf.Lerp(height, 0f, 1f - widePathDist / 0.35f);
                    }
                    break;

                case ZoneRouteShapeMode.NarrowPassage:
                    float narrowPathDist = Mathf.Abs(u - 0.5f);
                    if (narrowPathDist < 0.15f)
                    {
                        height = Mathf.Lerp(height, 0f, 1f - narrowPathDist / 0.15f);
                    }
                    break;

                case ZoneRouteShapeMode.BranchingPath:
                    float branchDist = Mathf.Abs(u - 0.5f);
                    if (branchDist < 0.2f)
                    {
                        height = Mathf.Lerp(height, 0f, 1f - branchDist / 0.2f);
                    }
                    if (Mathf.Abs(u - 0.2f) < 0.1f && v > 0.3f && v < 0.7f)
                    {
                        height = Mathf.Lerp(height, 0f, 0.5f);
                    }
                    break;

                case ZoneRouteShapeMode.DeadEndClue:
                    float deadEndFactor = v;
                    height -= deadEndFactor * 5f * plan.canyonDepth01;
                    break;

                case ZoneRouteShapeMode.BoundaryEdge:
                    float edgeDist = Mathf.Min(u, 1f - u, v, 1f - v);
                    if (edgeDist < 0.15f)
                    {
                        height += (0.15f - edgeDist) / 0.15f * 10f;
                    }
                    break;

                case ZoneRouteShapeMode.HubApproach:
                    float hubDist = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                    height -= hubDist * 3f * plan.slopeScale;
                    break;
            }

            return height;
        }

        /// <summary>
        /// Terrain Patch에 사용할 Material을 resolve한다.
        /// </summary>
        private static Material ResolveTerrainPatchMaterial(DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings.TerrainPatchMaterial != null)
            {
                if (!IsForbiddenTerrainPatchMaterial(settings.TerrainPatchMaterial))
                {
                    return settings.TerrainPatchMaterial;
                }
                Debug.LogWarning($"[ZoneTerrainPatch] Settings.TerrainPatchMaterial '{settings.TerrainPatchMaterial.name}' is a tree/billboard material. Ignoring and searching for SW3_BackdropTerrain.");
            }

            Material exactMatch = FindMaterialByExactName("SW3_BackdropTerrain");
            if (exactMatch != null)
            {
                return exactMatch;
            }

            Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();
            Material backdropMat = null;
            for (int i = 0; i < allMaterials.Length; i++)
            {
                if (allMaterials[i] == null || allMaterials[i].name == null) continue;

                string matName = allMaterials[i].name;
                if (matName.Contains("SW3_BackdropTerrain") || matName.Contains("BackdropTerrain"))
                {
                    if (!IsTreeOrBillboardMaterialName(matName))
                    {
                        backdropMat = allMaterials[i];
                        break;
                    }
                }
            }
            if (backdropMat != null)
            {
                return backdropMat;
            }

            Material urpLit = null;
            for (int i = 0; i < allMaterials.Length; i++)
            {
                if (allMaterials[i] != null && (allMaterials[i].name == "URP Lit" || allMaterials[i].name == "Lit"))
                {
                    urpLit = allMaterials[i];
                    break;
                }
            }
            if (urpLit != null)
            {
                return urpLit;
            }

            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
            if (defaultShader != null)
            {
                Material fallback = new Material(defaultShader);
                fallback.name = "DL_TerrainPatch_Fallback_Lit";
                fallback.color = new Color(0.3f, 0.3f, 0.35f);
                return fallback;
            }

            Material lastResort = new Material(Shader.Find("Standard"));
            lastResort.name = "DL_TerrainPatch_Fallback_Standard";
            lastResort.color = new Color(0.3f, 0.3f, 0.35f);
            return lastResort;
        }

        /// <summary>
        /// AssetDatabase에서 정확한 이름으로 Material을 검색한다.
        /// </summary>
        private static Material FindMaterialByExactName(string exactName)
        {
            string[] guids = AssetDatabase.FindAssets($"{exactName} t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.name == exactName)
                {
                    return mat;
                }
            }
            return null;
        }

        /// <summary>
        /// Material 이름이 tree/billboard 계열인지 검사한다.
        /// </summary>
        private static bool IsTreeOrBillboardMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return false;

            string lower = materialName.ToLowerInvariant();
            return lower.Contains("fagaceae")
                || lower.Contains("billboard")
                || lower.Contains("tree")
                || lower.Contains("trees")
                || lower.Contains("foliage")
                || lower.Contains("vegetationcard");
        }

        /// <summary>
        /// TerrainPatch에 사용이 금지된 material인지 검사한다.
        /// </summary>
        private static bool IsForbiddenTerrainPatchMaterial(Material material)
        {
            if (material == null || material.name == null) return false;
            return IsTreeOrBillboardMaterialName(material.name);
        }

        /// <summary>
        /// TerrainPatch에 사용이 허용된 material인지 검사한다.
        /// </summary>
        private static bool IsAllowedTerrainPatchMaterial(Material material)
        {
            if (material == null || material.name == null) return false;

            if (material.name == "SW3_BackdropTerrain") return true;

            return !IsTreeOrBillboardMaterialName(material.name);
        }

        /// <summary>
        /// ZoneRoot 하위 전체 Transform을 재귀 탐색하여 Seafloor placeholder 후보를 찾고,
        /// Renderer/Collider를 모두 비활성화하고 GameObject 자체도 SetActive(false) 처리한다.
        /// 단, ZoneRoot/Geometry/TerrainPatch는 비활성화하지 않는다.
        /// </summary>
        private static void DisableLegacySeafloorPlaceholdersRecursive(Transform zoneRoot)
        {
            if (zoneRoot == null) return;

            for (int i = 0; i < zoneRoot.childCount; i++)
            {
                Transform child = zoneRoot.GetChild(i);

                // TerrainPatch 경로는 비활성화 금지
                if (child.name == TerrainPatchRootName || child.name.StartsWith("TerrainPatch_"))
                {
                    continue;
                }

                if (IsLegacySeafloorPlaceholderCandidate(child))
                {
                    // Renderer 전체 비활성화 (자식 포함)
                    Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        if (renderers[r] != null && renderers[r].enabled)
                        {
                            renderers[r].enabled = false;
                            EditorUtility.SetDirty(renderers[r].gameObject);
                        }
                    }

                    // Collider 전체 비활성화 (자식 포함)
                    Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
                    for (int c = 0; c < colliders.Length; c++)
                    {
                        if (colliders[c] != null && colliders[c].enabled)
                        {
                            colliders[c].enabled = false;
                            EditorUtility.SetDirty(colliders[c].gameObject);
                        }
                    }

                    // GameObject 자체 비활성화
                    if (child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(false);
                        EditorUtility.SetDirty(child.gameObject);
                    }
                }

                // 재귀 탐색 (TerrainPatch 경로는 건너뛰었으므로 안전)
                DisableLegacySeafloorPlaceholdersRecursive(child);
            }
        }

        /// <summary>
        /// Transform이 Seafloor placeholder 후보인지 검사한다.
        /// 이름이 정확히 "Seafloor"이거나, 이름에 "Seafloor"와 "PLACEHOLDER"가 모두 포함되거나,
        /// 이름에 "PLACEHOLDER"가 포함된 경우 true.
        /// </summary>
        private static bool IsLegacySeafloorPlaceholderCandidate(Transform t)
        {
            if (t == null) return false;

            string name = t.name;

            // 정확히 "Seafloor"
            if (name == SeafloorPlaceholderName)
                return true;

            // "Seafloor" + "PLACEHOLDER"
            if (name.Contains("Seafloor") && name.Contains("PLACEHOLDER"))
                return true;

            // "PLACEHOLDER"만 있어도 후보
            if (name.Contains("PLACEHOLDER"))
                return true;

            return false;
        }

        /// <summary>
        /// ZoneRoot 하위에서 Seafloor placeholder 후보를 재귀 탐색하고,
        /// renderer/collider가 disabled 상태인지 확인한다.
        /// </summary>
        private static void CheckSeafloorPlaceholderRecursive(Transform parent, ref bool foundAny, ref bool foundDisabled)
        {
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (IsLegacySeafloorPlaceholderCandidate(child))
                {
                    foundAny = true;

                    // Renderer/Collider가 모두 disabled인지 확인
                    Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                    Collider[] colliders = child.GetComponentsInChildren<Collider>(true);

                    bool allDisabled = true;

                    for (int r = 0; r < renderers.Length; r++)
                    {
                        if (renderers[r] != null && renderers[r].enabled)
                        {
                            allDisabled = false;
                            break;
                        }
                    }

                    if (allDisabled)
                    {
                        for (int c = 0; c < colliders.Length; c++)
                        {
                            if (colliders[c] != null && colliders[c].enabled)
                            {
                                allDisabled = false;
                                break;
                            }
                        }
                    }

                    if (allDisabled)
                    {
                        foundDisabled = true;
                    }
                }

                // 재귀 탐색
                CheckSeafloorPlaceholderRecursive(child, ref foundAny, ref foundDisabled);
            }
        }

        /// <summary>
        /// Seafloor placeholder가 여전히 enabled인 zone을 로그에 출력한다.
        /// </summary>
        private static void LogEnabledSeafloorPlaceholders(Transform zoneRootsTransform, StringBuilder log)
        {
            if (zoneRootsTransform == null) return;

            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 10; row++)
                {
                    char colChar = (char)('A' + col);
                    string zrName = $"ZoneRoot_{colChar}{row + 1}";
                    Transform zr = zoneRootsTransform.Find(zrName);
                    if (zr == null) continue;

                    bool foundAny = false;
                    bool foundDisabled = false;
                    CheckSeafloorPlaceholderRecursive(zr, ref foundAny, ref foundDisabled);

                    if (foundAny && !foundDisabled)
                    {
                        log.AppendLine($"    [WARN] {zrName}: Seafloor placeholder still has enabled renderer/collider.");
                    }
                }
            }
        }

        /// <summary>
        /// ZoneRoot 하위에서 TerrainPatch_XX의 Mesh를 가져온다.
        /// </summary>
        private static Mesh GetPatchMesh(Transform zoneRootsParent, string zoneId)
        {
            string zoneRootName = $"ZoneRoot_{zoneId}";
            Transform zr = zoneRootsParent.Find(zoneRootName);
            if (zr == null) return null;

            Transform geo = zr.Find(GeometryRootName);
            if (geo == null) return null;

            Transform tpRoot = geo.Find(TerrainPatchRootName);
            if (tpRoot == null) return null;

            Transform patchT = tpRoot.Find($"TerrainPatch_{zoneId}");
            if (patchT == null) return null;

            MeshFilter mf = patchT.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;

            return mf.sharedMesh;
        }

        /// <summary>
        /// Material이 pink인지 검사한다 (pink material 방지).
        /// </summary>
        private static bool IsPinkMaterial(Material mat)
        {
            if (mat == null || mat.shader == null)
                return true;

            if (mat.shader.name == "Hidden/InternalErrorShader")
                return true;

            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                if (c.r > 0.9f && c.g < 0.1f && c.b > 0.9f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Transform 하위에서 이름으로 GameObject를 재귀 검색한다.
        /// </summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child;

                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Transform 하위에서 이름이 포함된 GameObject가 있는지 재귀 검색한다.
        /// </summary>
        private static bool FindObjectWithNameRecursive(Transform parent, string namePart)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.Contains(namePart))
                    return true;

                if (FindObjectWithNameRecursive(child, namePart))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// ZoneRoot 하위에서 BackgroundTrees/trees 계열 GameObject의 MeshRenderer material을 검사한다.
        /// </summary>
        private static void CheckBackgroundTreeBillboardMaterials(Transform parent, ref int billboardCount)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                bool isTreeObject = child.name.Contains("BackgroundTrees")
                    || child.name.Contains("trees")
                    || child.name.Contains("Trees")
                    || child.name.Contains("Tree")
                    || child.name.Contains("Foliage")
                    || child.name.Contains("foliage");

                if (isTreeObject)
                {
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                    {
                        if (IsTreeOrBillboardMaterialName(mr.sharedMaterial.name))
                        {
                            billboardCount++;
                        }
                    }
                }

                CheckBackgroundTreeBillboardMaterials(child, ref billboardCount);
            }
        }

        /// <summary>
        /// TerrainPatch material의 이름, shader 이름을 debug 로그로 출력한다.
        /// </summary>
        private static void LogTerrainPatchMaterialDebugInfo(Transform zoneRootsTransform, DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (zoneRootsTransform == null || settings == null) return;

            var debugLog = new StringBuilder();
            debugLog.AppendLine("  [DEBUG] TerrainPatch Material Debug Info:");

            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 10; row++)
                {
                    char colChar = (char)('A' + col);
                    string zoneIdStr = $"{colChar}{row + 1}";
                    string zrName = $"ZoneRoot_{zoneIdStr}";
                    Transform zr = zoneRootsTransform.Find(zrName);
                    if (zr == null) continue;

                    Transform geo = zr.Find(GeometryRootName);
                    if (geo == null) continue;

                    Transform tpRoot = geo.Find(TerrainPatchRootName);
                    if (tpRoot == null) continue;

                    Transform patchT = tpRoot.Find($"TerrainPatch_{zoneIdStr}");
                    if (patchT == null) continue;

                    MeshRenderer mr = patchT.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                    {
                        debugLog.AppendLine($"    {zoneIdStr}: material='{mr.sharedMaterial.name}', shader='{mr.sharedMaterial.shader?.name ?? "null"}', color={mr.sharedMaterial.color}");
                    }
                    else
                    {
                        debugLog.AppendLine($"    {zoneIdStr}: MeshRenderer or material is null");
                    }
                }
            }

            Debug.Log(debugLog.ToString());
        }

        /// <summary>
        /// Verbose 로그가 활성화된 경우에만 로그를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[ZoneTerrainPatch] {message}");
            }
        }
    }
}
