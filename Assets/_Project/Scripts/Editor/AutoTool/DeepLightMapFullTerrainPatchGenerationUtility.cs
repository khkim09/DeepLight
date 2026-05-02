using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Data.World.Design;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-A-2: A~J 전체 100개 ZoneRoot에 TerrainPatch scene artifact를 생성하기 위한 전용 유틸리티.
    /// 실제 grid mesh 생성, plan 기반 height 계산, material resolve, MeshCollider 설정을 포함한다.
    /// 기존 DeepLightMapZoneTerrainPatchUtility.cs(A1~C10 전용)와 독립적으로 동작한다.
    /// </summary>
    public static class DeepLightMapFullTerrainPatchGenerationUtility
    {
        // ===== Constants =====
        private const string GeometryRootName = "Geometry";
        private const string TerrainPatchRootName = "TerrainPatch";

        /// <summary>전체 Zone 열 개수 (A~J = 10열)</summary>
        private const int TotalColumns = 10;

        /// <summary>전체 Zone 행 개수 (1~10 = 10행)</summary>
        private const int TotalRows = 10;

        /// <summary>전체 Zone 개수 (10x10 = 100)</summary>
        private const int TotalZoneCount = 100;

        /// <summary>기본 zone 크기 (WorldMapConfig 없을 때 fallback)</summary>
        private const float DefaultZoneSize = 400f;

        /// <summary>기본 mesh 해상도 (33x33 vertices)</summary>
        private const int DefaultResolution = 32;

        /// <summary>edge falloff 폭 (vertex 단위, seam 보호용)</summary>
        private const int EdgeFalloffWidth = 3;

        // ===== Public API =====

        /// <summary>
        /// A~J 전체 100개 ZoneRoot에 TerrainPatch scene artifact를 재구축한다.
        /// 각 ZoneRoot_XX에 대해 Geometry/TerrainPatch/TerrainPatch_XX 구조를 보장하고,
        /// 실제 grid mesh를 생성하며 plan 기반 height, material, MeshCollider를 설정한다.
        /// </summary>
        public static void RebuildFullTerrainPatches(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[FullTerrainPatch] Settings is null! Cannot rebuild full terrain patches.");
                return;
            }

            if (settings.WorldMapConfig == null)
            {
                Debug.LogError("[FullTerrainPatch] WorldMapConfig is null! Cannot rebuild full terrain patches.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-A-2: Rebuild Full Terrain Patches (A1~J10) =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[FullTerrainPatch] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. ZoneRoots 부모 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[FullTerrainPatch] '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // 3. Config / Plan DB / Material resolve
            var config = settings.WorldMapConfig;
            float zoneSize = config.ZoneSize; // 400
            int resolution = Mathf.Max(2, settings.TerrainPatchResolution); // 32
            int vertexCountPerAxis = resolution + 1; // 33

            var planDb = settings.ZoneTerrainPlanDatabase;
            Material patchMaterial = ResolveTerrainPatchMaterial(settings);

            log.AppendLine($"  ZoneSize: {zoneSize}, Resolution: {resolution}x{resolution} ({vertexCountPerAxis}x{vertexCountPerAxis} vertices)");
            log.AppendLine($"  Material: {(patchMaterial != null ? patchMaterial.name : "NULL (will use fallback)")}");

            // 4. A~J 전체 100개 ZoneRoot 순회하며 mesh 생성
            int totalProcessed = 0;
            int totalSkipped = 0;
            int geometryCreated = 0;
            int terrainPatchRootCreated = 0;
            int patchesCreated = 0;

            ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
            {
                string zoneRootName = $"ZoneRoot_{zoneIdStr}";

                // ZoneRoot 찾기
                Transform zoneRootTransform = zoneRootsTransform.Find(zoneRootName);
                if (zoneRootTransform == null)
                {
                    LogIfVerbose(settings, $"[SKIP] {zoneRootName} not found.");
                    totalSkipped++;
                    return;
                }

                // Geometry root 찾기/생성
                Transform geometryTransform = FindOrCreateChildTransform(zoneRootTransform, GeometryRootName);
                if (geometryTransform == null)
                {
                    LogIfVerbose(settings, $"[SKIP] {zoneRootName}/Geometry could not be created.");
                    totalSkipped++;
                    return;
                }
                geometryCreated++;

                // TerrainPatch root 찾기/생성
                Transform terrainPatchParent = FindOrCreateChildTransform(geometryTransform, TerrainPatchRootName);
                if (terrainPatchParent == null)
                {
                    LogIfVerbose(settings, $"[SKIP] {zoneRootName}/Geometry/TerrainPatch could not be created.");
                    totalSkipped++;
                    return;
                }
                terrainPatchRootCreated++;

                // TerrainPatch_XX 생성 (기존 제거 후 재생성)
                string patchName = $"TerrainPatch_{zoneIdStr}";
                Transform existingPatch = terrainPatchParent.Find(patchName);
                if (existingPatch != null)
                {
                    Undo.DestroyObjectImmediate(existingPatch.gameObject);
                }

                // Plan 조회
                WorldMapZoneTerrainPlan plan = planDb?.GetPlan(zoneIdStr);

                // Zone 중심 좌표 계산
                ZoneCoordinate coord = new ZoneCoordinate(colIndex, rowIndex);
                Vector3 zoneCenter = coord.GetZoneCenterWorldPosition(config);

                // Mesh 생성 (plan 기반 height)
                Mesh patchMesh = GenerateFullTerrainPatchMesh(plan, zoneCenter, zoneSize, resolution, settings, zoneIdStr);

                // TerrainPatch GameObject 생성
                GameObject patchGo = new GameObject(patchName);
                patchGo.transform.SetParent(terrainPatchParent);
                patchGo.transform.localPosition = Vector3.zero;
                patchGo.transform.localRotation = Quaternion.identity;
                patchGo.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(patchGo, $"Create {patchName}");

                // MeshFilter
                MeshFilter mf = patchGo.AddComponent<MeshFilter>();
                mf.sharedMesh = patchMesh;

                // MeshRenderer + Material
                MeshRenderer mr = patchGo.AddComponent<MeshRenderer>();
                Material safeMat = patchMaterial;
                if (safeMat != null && IsForbiddenTerrainPatchMaterial(safeMat))
                {
                    Debug.LogWarning($"[FullTerrainPatch] {patchName}: Forbidden material '{safeMat.name}' detected. Re-resolving...");
                    safeMat = ResolveTerrainPatchMaterial(settings);
                }
                mr.sharedMaterial = safeMat;

                // MeshCollider
                MeshCollider mc = patchGo.AddComponent<MeshCollider>();
                mc.sharedMesh = patchMesh;
                mc.convex = false;
                mc.isTrigger = false;

                patchesCreated++;
                totalProcessed++;
                LogIfVerbose(settings, $"[OK] {patchName} created. Vertices: {patchMesh.vertexCount}, Triangles: {patchMesh.triangles.Length / 3}");
            });

            log.AppendLine($"  Total zones processed: {totalProcessed}");
            log.AppendLine($"  Total zones skipped: {totalSkipped}");
            log.AppendLine($"  Geometry roots ensured: {geometryCreated}");
            log.AppendLine($"  TerrainPatch roots ensured: {terrainPatchRootCreated}");
            log.AppendLine($"  TerrainPatch meshes created: {patchesCreated}");
            log.AppendLine("===== Phase 14.10-A-2: Rebuild Full Terrain Patches Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// A~J 전체 100개 ZoneRoot의 TerrainPatch scene artifact 유효성을 검사한다.
        /// 각 TerrainPatch_XX의 MeshFilter/MeshRenderer/MeshCollider 존재,
        /// mesh vertex/triangle count > 0, material null/forbidden 검사, 중복 검사를 수행한다.
        /// </summary>
        public static void ValidateFullTerrainPatches(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[FullTerrainPatch] Settings is null! Cannot validate.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-A-2: Validate Full Terrain Patches (A1~J10) =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot 존재
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

            // 2. ZoneRoots parent 존재
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

            // 3. A~J 100개 ZoneRoot 존재
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

            // 4. 100개 Geometry root 존재
            int geometryRootFound = 0;
            if (zoneRootsTransform != null)
            {
                ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
                {
                    string zrName = $"ZoneRoot_{zoneIdStr}";
                    Transform zr = zoneRootsTransform.Find(zrName);
                    if (zr != null && zr.Find(GeometryRootName) != null)
                    {
                        geometryRootFound++;
                    }
                });
            }

            if (geometryRootFound == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} Geometry roots exist under ZoneRoots.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Geometry roots found: {geometryRootFound}/{TotalZoneCount}");
                failCount++;
            }

            // 5. 100개 TerrainPatch root 존재
            int terrainPatchRootFound = 0;
            if (zoneRootsTransform != null)
            {
                ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
                {
                    string zrName = $"ZoneRoot_{zoneIdStr}";
                    Transform zr = zoneRootsTransform.Find(zrName);
                    if (zr != null)
                    {
                        Transform geo = zr.Find(GeometryRootName);
                        if (geo != null && geo.Find(TerrainPatchRootName) != null)
                        {
                            terrainPatchRootFound++;
                        }
                    }
                });
            }

            if (terrainPatchRootFound == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} TerrainPatch roots exist under Geometry.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] TerrainPatch roots found: {terrainPatchRootFound}/{TotalZoneCount}");
                failCount++;
            }

            // 6. 100개 TerrainPatch_XX 존재 + 상세 검증
            int patchFound = 0;
            int meshFilterValid = 0;
            int meshRendererValid = 0;
            int meshColliderValid = 0;
            int vertexCountValid = 0;
            int triangleCountValid = 0;
            int materialNonNull = 0;
            int materialAllowed = 0;
            int duplicateCount = 0;

            if (zoneRootsTransform != null)
            {
                ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
                {
                    string zrName = $"ZoneRoot_{zoneIdStr}";
                    Transform zr = zoneRootsTransform.Find(zrName);
                    if (zr == null) return;

                    Transform geo = zr.Find(GeometryRootName);
                    if (geo == null) return;

                    Transform tpRoot = geo.Find(TerrainPatchRootName);
                    if (tpRoot == null) return;

                    string patchName = $"TerrainPatch_{zoneIdStr}";

                    // 중복 검사: 같은 이름의 자식이 2개 이상인지 확인
                    int childCount = 0;
                    for (int i = 0; i < tpRoot.childCount; i++)
                    {
                        if (tpRoot.GetChild(i).name == patchName)
                        {
                            childCount++;
                        }
                    }
                    if (childCount > 1)
                    {
                        duplicateCount++;
                        log.AppendLine($"  [FAIL] {patchName}: Duplicate found ({childCount} instances).");
                        failCount++;
                        return;
                    }

                    Transform patchT = tpRoot.Find(patchName);
                    if (patchT == null) return;
                    patchFound++;

                    // MeshFilter 검증
                    MeshFilter mf = patchT.GetComponent<MeshFilter>();
                    if (mf != null)
                    {
                        meshFilterValid++;
                        if (mf.sharedMesh != null)
                        {
                            // vertex count > 0
                            if (mf.sharedMesh.vertexCount > 0)
                            {
                                vertexCountValid++;
                            }
                            // triangle count > 0
                            if (mf.sharedMesh.triangles.Length > 0)
                            {
                                triangleCountValid++;
                            }
                        }
                    }

                    // MeshRenderer 검증
                    MeshRenderer mr = patchT.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        meshRendererValid++;
                        if (mr.sharedMaterial != null)
                        {
                            materialNonNull++;
                            // forbidden material 검사
                            if (!IsForbiddenTerrainPatchMaterial(mr.sharedMaterial))
                            {
                                materialAllowed++;
                            }
                            else
                            {
                                log.AppendLine($"  [FAIL] {patchName}: Forbidden material '{mr.sharedMaterial.name}'.");
                                failCount++;
                            }
                        }
                    }

                    // MeshCollider 검증
                    MeshCollider mc = patchT.GetComponent<MeshCollider>();
                    if (mc != null)
                    {
                        meshColliderValid++;
                    }
                });
            }

            // 검증 결과 요약
            if (patchFound == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} TerrainPatch_XX exist.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] TerrainPatch_XX found: {patchFound}/{TotalZoneCount}");
                failCount++;
            }

            if (meshFilterValid == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} TerrainPatch_XX have MeshFilter.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] MeshFilter found: {meshFilterValid}/{TotalZoneCount}");
                failCount++;
            }

            if (meshRendererValid == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} TerrainPatch_XX have MeshRenderer.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] MeshRenderer found: {meshRendererValid}/{TotalZoneCount}");
                failCount++;
            }

            if (meshColliderValid == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} TerrainPatch_XX have MeshCollider.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] MeshCollider found: {meshColliderValid}/{TotalZoneCount}");
                failCount++;
            }

            if (vertexCountValid == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} meshes have vertexCount > 0.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Meshes with vertexCount > 0: {vertexCountValid}/{TotalZoneCount}");
                failCount++;
            }

            if (triangleCountValid == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} meshes have triangleCount > 0.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Meshes with triangleCount > 0: {triangleCountValid}/{TotalZoneCount}");
                failCount++;
            }

            if (materialNonNull == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} materials are non-null.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Non-null materials: {materialNonNull}/{TotalZoneCount}");
                failCount++;
            }

            if (materialAllowed == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} materials are allowed (no tree/billboard/vegetation).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Allowed materials: {materialAllowed}/{TotalZoneCount}");
                failCount++;
            }

            if (duplicateCount > 0)
            {
                log.AppendLine($"  [FAIL] Duplicate TerrainPatch_XX found: {duplicateCount} zones.");
            }

            // 최종 요약
            log.AppendLine($"  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-A-2: Validate Full Terrain Patches Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Mesh Generation =====

        /// <summary>
        /// A~J 전체 zone용 TerrainPatch grid mesh를 생성한다.
        /// plan이 있으면 targetCenterDepth/depthRange/slopeScale/roughnessScale을 반영하고,
        /// plan이 없으면 평탄 fallback을 사용한다.
        /// edge falloff를 약하게 적용하여 seam 보호를 준비한다.
        /// </summary>
        private static Mesh GenerateFullTerrainPatchMesh(
            WorldMapZoneTerrainPlan plan, Vector3 zoneCenter, float zoneSize,
            int resolution, DeepLightMapAutoBuilderSettingsSO settings, string zoneIdStr)
        {
            int vertexCountPerAxis = resolution + 1;
            int totalVertices = vertexCountPerAxis * vertexCountPerAxis;
            Vector3[] vertices = new Vector3[totalVertices];
            Vector3[] normals = new Vector3[totalVertices];
            Vector2[] uv = new Vector2[totalVertices];
            int[] triangles = new int[resolution * resolution * 6];

            float halfSize = zoneSize * 0.5f;
            float step = zoneSize / resolution;

            // Plan 기반 base height 계산
            float baseY = 0f;
            float depthRange = 0f;
            float slopeScale = 0f;
            float roughnessScale = 0.5f;
            float flatAreaWeight = 0f;

            if (plan != null)
            {
                // targetCenterDepth는 음수 (예: -165), floorHeight도 음수
                // baseY는 zone local y 기준으로 plan의 center depth 사용
                baseY = plan.targetCenterDepth;
                depthRange = plan.depthRange;
                slopeScale = plan.slopeScale;
                roughnessScale = plan.roughnessScale;
                flatAreaWeight = plan.flatAreaWeight01;
            }

            // zoneId 기반 deterministic seed offset (PerlinNoise용)
            float seedOffsetX = GetSeedOffsetX(zoneIdStr);
            float seedOffsetZ = GetSeedOffsetZ(zoneIdStr);

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

                    // Height calculation
                    float heightOffset = 0f;

                    if (plan != null)
                    {
                        // Slope contribution: plan의 slopeScale 기반 경사
                        heightOffset += (u - 0.5f) * slopeScale * 30f;

                        // PerlinNoise height (deterministic: zoneId seed + world position)
                        float noise = Mathf.PerlinNoise(
                            (zoneCenter.x + localX) * settings.TerrainPatchNoiseScale + seedOffsetX,
                            (zoneCenter.z + localZ) * settings.TerrainPatchNoiseScale + seedOffsetZ
                        );

                        // noise를 -1~1 범위로 변환 후 roughnessScale 적용
                        float effectiveNoise = (noise - 0.5f) * 2f * roughnessScale * settings.TerrainPatchNoiseStrength;
                        heightOffset += effectiveNoise;

                        // flatAreaWeight로 변형 감소
                        heightOffset *= (1f - flatAreaWeight * 0.8f);
                    }
                    else
                    {
                        // Plan 없음: 평탄 fallback + 미세 noise만 추가
                        float noise = Mathf.PerlinNoise(
                            (zoneCenter.x + localX) * 0.01f + seedOffsetX,
                            (zoneCenter.z + localZ) * 0.01f + seedOffsetZ
                        );
                        heightOffset = (noise - 0.5f) * 2f;
                    }

                    // Edge falloff: 경계 쪽 height 변화를 약하게 줄여 seam 보호
                    float edgeFalloff = CalculateEdgeFalloff(u, v, EdgeFalloffWidth, resolution);
                    heightOffset *= edgeFalloff;

                    // 최종 Y = baseY + heightOffset
                    float finalY = baseY + heightOffset;

                    vertices[idx] = new Vector3(localX, finalY, localZ);
                    uv[idx] = new Vector2(u, v);

                    // 임시 normal (upward, 이후 RecalculateNormals에서 재계산)
                    normals[idx] = Vector3.up;

                    // Triangles (x, z grid)
                    if (x < resolution && z < resolution)
                    {
                        int topLeft = idx;
                        int topRight = idx + 1;
                        int bottomLeft = idx + vertexCountPerAxis;
                        int bottomRight = idx + vertexCountPerAxis + 1;

                        triangles[triIndex++] = topLeft;
                        triangles[triIndex++] = bottomLeft;
                        triangles[triIndex++] = topRight;

                        triangles[triIndex++] = topRight;
                        triangles[triIndex++] = bottomLeft;
                        triangles[triIndex++] = bottomRight;
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = $"TerrainPatch_{zoneIdStr}_Mesh";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// edge falloff factor를 계산한다. 경계에서 0에 가깝고, 내부로 갈수록 1에 가까워진다.
        /// seam 보호를 위해 edge 쪽 height 변화를 줄인다.
        /// </summary>
        private static float CalculateEdgeFalloff(float u, float v, int falloffWidth, int resolution)
        {
            if (falloffWidth <= 0) return 1f;

            float falloffNormalized = (float)falloffWidth / resolution;
            if (falloffNormalized <= 0f) return 1f;

            // u 방향 edge 거리
            float distToEdgeU = Mathf.Min(u, 1f - u);
            // v 방향 edge 거리
            float distToEdgeV = Mathf.Min(v, 1f - v);
            // 가장 가까운 edge까지의 거리
            float minDist = Mathf.Min(distToEdgeU, distToEdgeV);

            if (minDist >= falloffNormalized) return 1f;

            // Smooth step falloff
            float t = minDist / falloffNormalized;
            return t * t * (3f - 2f * t); // smoothstep
        }

        // ===== Material Helpers =====

        /// <summary>
        /// TerrainPatch에 사용할 Material을 resolve한다.
        /// 1순위: settings.TerrainPatchMaterial (forbidden 검사 후)
        /// 2순위: "SW3_BackdropTerrain" 이름 정확 일치 검색
        /// 3순위: 이름에 "SW3_BackdropTerrain" 또는 "BackdropTerrain" 포함 + tree/billboard 제외
        /// 4순위: URP/Lit material
        /// 5순위: 새 URP Lit material 생성
        /// </summary>
        private static Material ResolveTerrainPatchMaterial(DeepLightMapAutoBuilderSettingsSO settings)
        {
            // 1순위: settings.TerrainPatchMaterial
            if (settings.TerrainPatchMaterial != null)
            {
                if (!IsForbiddenTerrainPatchMaterial(settings.TerrainPatchMaterial))
                {
                    return settings.TerrainPatchMaterial;
                }
                Debug.LogWarning($"[FullTerrainPatch] Settings.TerrainPatchMaterial '{settings.TerrainPatchMaterial.name}' is a tree/billboard material. Ignoring and searching for SW3_BackdropTerrain.");
            }

            // 2순위: 정확한 이름 일치
            Material exactMatch = FindMaterialByExactName("SW3_BackdropTerrain");
            if (exactMatch != null)
            {
                return exactMatch;
            }

            // 3순위: 이름 포함 검색 (tree/billboard 제외)
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

            // 4순위: URP/Lit material 검색
            Material urpLit = null;
            for (int i = 0; i < allMaterials.Length; i++)
            {
                if (allMaterials[i] == null || allMaterials[i].name == null) continue;
                if (allMaterials[i].name.Contains("URP") && allMaterials[i].name.Contains("Lit"))
                {
                    urpLit = allMaterials[i];
                    break;
                }
            }
            if (urpLit != null)
            {
                return urpLit;
            }

            // 5순위: 새 URP Lit material 생성 (pink material 방지)
            Material fallbackMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (fallbackMat != null)
            {
                fallbackMat.name = "FullTerrainPatch_Fallback_Lit";
                fallbackMat.color = new Color(0.5f, 0.5f, 0.5f, 1f); // 회색
                return fallbackMat;
            }

            Debug.LogError("[FullTerrainPatch] Failed to resolve any TerrainPatch material! Pink material may appear.");
            return null;
        }

        /// <summary>
        /// 지정된 이름과 정확히 일치하는 Material을 AssetDatabase에서 찾는다.
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

        // ===== Seed Helpers =====

        /// <summary>
        /// zoneId 기반 deterministic X offset을 생성한다.
        /// "A1" -> 0.0, "B1" -> 10.0, ..., "J10" -> 990.0
        /// </summary>
        private static float GetSeedOffsetX(string zoneIdStr)
        {
            if (string.IsNullOrEmpty(zoneIdStr) || zoneIdStr.Length < 2) return 0f;
            char colChar = zoneIdStr[0];
            int colIndex = colChar - 'A'; // A=0, B=1, ..., J=9
            return colIndex * 100f;
        }

        /// <summary>
        /// zoneId 기반 deterministic Z offset을 생성한다.
        /// "A1" -> 0.0, "A2" -> 10.0, ..., "J10" -> 990.0
        /// </summary>
        private static float GetSeedOffsetZ(string zoneIdStr)
        {
            if (string.IsNullOrEmpty(zoneIdStr) || zoneIdStr.Length < 2) return 0f;
            string rowPart = zoneIdStr.Substring(1);
            if (int.TryParse(rowPart, out int rowNumber))
            {
                return (rowNumber - 1) * 100f;
            }
            return 0f;
        }

        // ===== Private Helpers (기존 유지) =====

        /// <summary>
        /// A~J, 1~10 전체 100개 zoneId를 안정적으로 순회하는 private iterator.
        /// A1~A10, B1~B10 ... J1~J10 순서를 유지하며 각 zoneId에 대해 callback을 호출한다.
        /// </summary>
        private static void ForEachZoneId(System.Action<string, int, int> callback)
        {
            // A=0, B=1, ..., J=9 순서로 column 순회
            for (int colIndex = 0; colIndex < TotalColumns; colIndex++)
            {
                char columnChar = (char)('A' + colIndex);

                // 1~10 순서로 row 순회
                for (int rowIndex = 0; rowIndex < TotalRows; rowIndex++)
                {
                    int rowNumber = rowIndex + 1;
                    string zoneIdStr = $"{columnChar}{rowNumber}";
                    callback(zoneIdStr, colIndex, rowIndex);
                }
            }
        }

        /// <summary>
        /// 지정된 zoneId 문자열에 대해 ZoneRoot_XX Transform을 찾는다.
        /// zoneRootsTransform은 ZoneRoots 부모 Transform이며, zoneIdStr은 "A1", "B3" 등의 형식이다.
        /// </summary>
        private static Transform FindZoneRootTransform(Transform zoneRootsTransform, string zoneIdStr)
        {
            if (zoneRootsTransform == null || string.IsNullOrEmpty(zoneIdStr))
                return null;

            string zoneRootName = $"ZoneRoot_{zoneIdStr}";
            return zoneRootsTransform.Find(zoneRootName);
        }

        /// <summary>
        /// 지정된 ZoneRoot Transform 하위의 Geometry root Transform을 찾거나 생성한다.
        /// 없으면 새로 생성하여 반환한다.
        /// </summary>
        private static Transform FindOrCreateGeometryRoot(Transform zoneRootTransform)
        {
            if (zoneRootTransform == null)
                return null;

            return FindOrCreateChildTransform(zoneRootTransform, GeometryRootName);
        }

        /// <summary>
        /// 지정된 Geometry root Transform 하위의 TerrainPatch root Transform을 찾거나 생성한다.
        /// 없으면 새로 생성하여 반환한다.
        /// </summary>
        private static Transform FindOrCreateTerrainPatchRoot(Transform geometryTransform)
        {
            if (geometryTransform == null)
                return null;

            return FindOrCreateChildTransform(geometryTransform, TerrainPatchRootName);
        }

        /// <summary>
        /// 부모 Transform 하위에서 지정된 이름의 자식 Transform을 찾거나 생성한다.
        /// 생성 시 localPosition/Rotation/Scale은 identity로 설정한다.
        /// </summary>
        private static Transform FindOrCreateChildTransform(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
                return null;

            // 기존 자식 검색
            Transform existing = parent.Find(childName);
            if (existing != null)
                return existing;

            // 없으면 새로 생성
            GameObject childGo = new GameObject(childName);
            childGo.transform.SetParent(parent);
            childGo.transform.localPosition = Vector3.zero;
            childGo.transform.localRotation = Quaternion.identity;
            childGo.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(childGo, $"Create {childName} under {parent.name}");

            return childGo.transform;
        }

        /// <summary>
        /// settings.LogVerbose가 true일 때만 Debug.Log를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[FullTerrainPatch] {message}");
            }
        }
    }
}
