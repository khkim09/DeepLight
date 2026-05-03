using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Content;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-I-2: Runtime Spawn Profile Database의 21개 entry에 연결할
    /// Editor/Test용 placeholder prefab을 생성/갱신하고 검증하는 Editor Utility.
    /// 실제 gameplay prefab을 만들지 않으며, scene object를 수정하지 않는다.
    /// 이후 Phase 14.10-J에서 RuntimePlaceholder를 실제 prefab instance로 치환하는 adapter를 만들 수 있도록 준비한다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnPlaceholderPrefabUtility
    {
        private const string LogPrefix = "[SpawnPlaceholderPrefab]";
        private const string PrefabRootFolder = "Assets/_Project/Generated/RuntimeSpawnPlaceholderPrefabs";
        private const string MaterialRootFolder = "Assets/_Project/Generated/RuntimeSpawnPlaceholderMaterials";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeSpawnProfileDatabase.asset";

        // ===== MarkerType → Primitive Type Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, PrimitiveType> MarkerPrimitiveMap =
            new Dictionary<WorldMapZoneContentMarkerType, PrimitiveType>
            {
                { WorldMapZoneContentMarkerType.Resource, PrimitiveType.Sphere },
                { WorldMapZoneContentMarkerType.Hazard, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Landmark, PrimitiveType.Cylinder },
                { WorldMapZoneContentMarkerType.Narrative, PrimitiveType.Capsule },
                { WorldMapZoneContentMarkerType.Route, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Debug, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Unknown, PrimitiveType.Cube },
            };

        // ===== MarkerType → Local Scale Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, Vector3> MarkerScaleMap =
            new Dictionary<WorldMapZoneContentMarkerType, Vector3>
            {
                { WorldMapZoneContentMarkerType.Resource, new Vector3(2f, 2f, 2f) },
                { WorldMapZoneContentMarkerType.Hazard, new Vector3(3f, 3f, 3f) },
                { WorldMapZoneContentMarkerType.Landmark, new Vector3(4f, 4f, 4f) },
                { WorldMapZoneContentMarkerType.Narrative, new Vector3(2f, 4f, 2f) },
                { WorldMapZoneContentMarkerType.Route, new Vector3(5f, 1f, 5f) },
                { WorldMapZoneContentMarkerType.Debug, new Vector3(2f, 2f, 2f) },
                { WorldMapZoneContentMarkerType.Unknown, new Vector3(2f, 2f, 2f) },
            };

        // ===== MarkerType → Material Color Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, Color> MarkerColorMap =
            new Dictionary<WorldMapZoneContentMarkerType, Color>
            {
                { WorldMapZoneContentMarkerType.Resource, new Color(0.2f, 0.8f, 0.2f, 1.0f) },   // Green
                { WorldMapZoneContentMarkerType.Hazard, new Color(0.9f, 0.2f, 0.2f, 1.0f) },     // Red
                { WorldMapZoneContentMarkerType.Landmark, new Color(1.0f, 0.6f, 0.1f, 1.0f) },   // Yellow/Orange
                { WorldMapZoneContentMarkerType.Narrative, new Color(0.2f, 0.7f, 1.0f, 1.0f) },  // Cyan/Blue
                { WorldMapZoneContentMarkerType.Route, new Color(0.8f, 0.8f, 0.8f, 1.0f) },      // White/Gray
                { WorldMapZoneContentMarkerType.Debug, new Color(0.8f, 0.2f, 0.8f, 1.0f) },      // Magenta/Purple
                { WorldMapZoneContentMarkerType.Unknown, new Color(0.5f, 0.5f, 0.5f, 1.0f) },    // Gray
            };

        // ===== Material Name Map =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, string> MaterialNameMap =
            new Dictionary<WorldMapZoneContentMarkerType, string>
            {
                { WorldMapZoneContentMarkerType.Resource, "RuntimeSpawnPlaceholder_Resource_Mat" },
                { WorldMapZoneContentMarkerType.Hazard, "RuntimeSpawnPlaceholder_Hazard_Mat" },
                { WorldMapZoneContentMarkerType.Landmark, "RuntimeSpawnPlaceholder_Landmark_Mat" },
                { WorldMapZoneContentMarkerType.Narrative, "RuntimeSpawnPlaceholder_Narrative_Mat" },
                { WorldMapZoneContentMarkerType.Route, "RuntimeSpawnPlaceholder_Route_Mat" },
                { WorldMapZoneContentMarkerType.Debug, "RuntimeSpawnPlaceholder_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Unknown, "RuntimeSpawnPlaceholder_Unknown_Mat" },
            };

        // ===== URP Shader Fallback List =====
        private static readonly string[] UrpShaderNames =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default",
            "Unlit/Color",
        };

        // ===== Public API =====

        /// <summary>
        /// RuntimeSpawnProfileDatabase의 모든 entry에 대해 Editor/Test용 placeholder prefab을 생성하거나 갱신한다.
        /// 각 entry의 ProfileId 기반으로 prefab asset을 생성하고, database의 Prefab slot에 연결한다.
        /// 기존 사용자 gameplay prefab이 연결된 entry는 절대 덮어쓰지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder Settings (null 체크용)</param>
        /// <param name="context">AutoBuilder Scene Context (null 체크용)</param>
        public static void CreateOrUpdateRuntimeSpawnPlaceholderPrefabs(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot create/update placeholder prefabs.");
                return;
            }

            if (context == null)
            {
                Debug.LogError($"{LogPrefix} Context is null! Cannot create/update placeholder prefabs.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-I-2: Create/Update Runtime Spawn Placeholder Prefabs =====");

            // 1. Database asset 로드
            WorldMapRuntimeSpawnProfileDatabaseSO database = LoadDatabaseAsset();
            if (database == null)
            {
                Debug.LogError($"{LogPrefix} RuntimeSpawnProfileDatabase not found at path: {DatabaseAssetPath}");
                return;
            }
            log.AppendLine($"  [OK] Database asset loaded: {AssetDatabase.GetAssetPath(database)}");

            // 2. Root folder 보장
            EnsureFolderExists(PrefabRootFolder);
            EnsureFolderExists(MaterialRootFolder);
            log.AppendLine($"  [OK] Prefab root folder: {PrefabRootFolder}");
            log.AppendLine($"  [OK] Material root folder: {MaterialRootFolder}");

            // 3. Database entries 순회
            IReadOnlyList<WorldMapRuntimeSpawnProfileEntry> entries = database.Entries;
            if (entries == null || entries.Count == 0)
            {
                Debug.LogError($"{LogPrefix} Database has no entries! Cannot create placeholder prefabs.");
                return;
            }
            log.AppendLine($"  [INFO] Total entries in database: {entries.Count}");

            // 통계
            int createdCount = 0;
            int updatedCount = 0;
            int linkedCount = 0;
            int preservedCount = 0;
            int skippedCount = 0;

            // 4. 각 entry별 placeholder prefab 생성/갱신
            List<WorldMapRuntimeSpawnProfileEntry> editableEntries = database.GetEditableEntries();
            for (int i = 0; i < editableEntries.Count; i++)
            {
                WorldMapRuntimeSpawnProfileEntry entry = editableEntries[i];
                if (entry == null)
                {
                    skippedCount++;
                    continue;
                }

                // ProfileId 기반 prefab path 생성
                string sanitizedProfileId = SanitizeProfileId(entry.ProfileId);
                string prefabPath = $"{PrefabRootFolder}/{sanitizedProfileId}.prefab";

                // 5. 기존 prefab이 있는지 확인
                GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                bool isExistingPlaceholder = false;

                if (existingPrefab != null)
                {
                    // 기존 prefab이 placeholder인지 확인
                    WorldMapRuntimeSpawnPlaceholderTag existingTag = existingPrefab.GetComponent<WorldMapRuntimeSpawnPlaceholderTag>();
                    isExistingPlaceholder = (existingTag != null && existingTag.IsEditorPlaceholder);
                }

                // 6. Prefab 생성 또는 갱신
                GameObject prefabInstance = null;
                bool loadedFromPrefabContents = false;

                if (existingPrefab != null && isExistingPlaceholder)
                {
                    // 기존 placeholder prefab 로드하여 갱신
                    prefabInstance = LoadPrefabContents(existingPrefab);
                    if (prefabInstance == null)
                    {
                        log.AppendLine($"  [WARN] Failed to load prefab contents for '{entry.ProfileId}'. Creating new.");
                        prefabInstance = CreatePlaceholderGameObject(entry);
                        // loadedFromPrefabContents는 false 유지
                    }
                    else
                    {
                        loadedFromPrefabContents = true;
                        // 기존 placeholder 갱신: mesh, material, tag 업데이트
                        RebuildPlaceholderContent(prefabInstance, entry);
                        updatedCount++;
                    }
                }
                else if (existingPrefab != null && !isExistingPlaceholder)
                {
                    // 사용자 gameplay prefab이 이미 연결되어 있음 → 보존
                    log.AppendLine($"  [INFO] Preserving user gameplay prefab for '{entry.ProfileId}': {AssetDatabase.GetAssetPath(existingPrefab)}");
                    preservedCount++;
                    continue;
                }
                else
                {
                    // 새 placeholder prefab 생성
                    prefabInstance = CreatePlaceholderGameObject(entry);
                    // loadedFromPrefabContents는 false 유지
                    createdCount++;
                }

                if (prefabInstance == null)
                {
                    log.AppendLine($"  [FAIL] Failed to create/update placeholder GameObject for '{entry.ProfileId}'.");
                    skippedCount++;
                    continue;
                }

                // 7. WorldMapRuntimeSpawnPlaceholderTag 설정
                WorldMapRuntimeSpawnPlaceholderTag tag = prefabInstance.GetComponent<WorldMapRuntimeSpawnPlaceholderTag>();
                if (tag == null)
                {
                    // Tag가 없으면 추가 (안전 장치)
                    tag = prefabInstance.AddComponent<WorldMapRuntimeSpawnPlaceholderTag>();
                }
                tag.Configure(entry.ProfileId, entry.MarkerType, entry.RuntimeCategory, entry.RuntimeKey, entry.IsFallbackProfile);

                // 8. Prefab 저장 및 임시 GameObject 정리 (try/finally로 정리 보장)
                GameObject savedPrefab = null;
                try
                {
                    if (existingPrefab != null && isExistingPlaceholder)
                    {
                        // 기존 prefab 갱신
                        savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                        if (savedPrefab != null)
                        {
                            log.AppendLine($"  [OK] Updated placeholder prefab: {prefabPath}");
                        }
                    }
                    else
                    {
                        // 새 prefab 저장
                        savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                        if (savedPrefab != null)
                        {
                            log.AppendLine($"  [OK] Created placeholder prefab: {prefabPath}");
                        }
                    }
                }
                finally
                {
                    // 임시 GameObject 정리
                    // LoadPrefabContents로 로드한 객체는 반드시 UnloadPrefabContents로 닫아야 함
                    // DestroyImmediate만 호출하면 Unity preview scene이 해제되지 않아 누적됨
                    if (loadedFromPrefabContents)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabInstance);
                    }
                    else
                    {
                        GameObject.DestroyImmediate(prefabInstance);
                    }
                }

                if (savedPrefab == null)
                {
                    log.AppendLine($"  [FAIL] Failed to save prefab for '{entry.ProfileId}'.");
                    skippedCount++;
                    continue;
                }

                // 9. Database entry의 Prefab 필드 연결
                if (entry.Prefab == null)
                {
                    // null prefab → placeholder 연결
                    entry.Prefab = savedPrefab;
                    linkedCount++;
                    log.AppendLine($"  [LINK] Linked placeholder prefab to entry '{entry.ProfileId}'.");
                }
                else if (isExistingPlaceholder)
                {
                    // 기존 placeholder prefab을 가리키는 경우 → 갱신된 prefab으로 재연결
                    entry.Prefab = savedPrefab;
                    linkedCount++;
                    log.AppendLine($"  [LINK] Re-linked updated placeholder prefab to entry '{entry.ProfileId}'.");
                }
                else
                {
                    // 사용자 gameplay prefab이 이미 연결되어 있음 → 보존 (위에서 continue 했으므로 여기 도달하지 않음)
                    preservedCount++;
                }
            }

            // 10. DB 변경 사항 저장
            bool databaseModified = (createdCount > 0 || updatedCount > 0 || linkedCount > 0);
            if (databaseModified)
            {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                log.AppendLine("  [OK] Database changes saved.");
            }
            else
            {
                log.AppendLine("  [INFO] No database changes needed.");
            }

            // 11. 결과 요약
            log.AppendLine($"  === Summary: Created={createdCount}, Updated={updatedCount}, Linked={linkedCount}, Preserved={preservedCount}, Skipped={skippedCount} ===");
            log.AppendLine("===== Phase 14.10-I-2: Create/Update Runtime Spawn Placeholder Prefabs Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// RuntimeSpawnProfileDatabase의 모든 entry에 대해 placeholder prefab의 유효성을 검사한다.
        /// prefab 존재, 컴포넌트 구성, metadata 일치, Collider 유무, shader 타입 등을 검증한다.
        /// placeholder prefab과 사용자 gameplay prefab을 구분하여 검사 기준을 다르게 적용한다.
        /// </summary>
        /// <param name="settings">AutoBuilder Settings (null 체크용)</param>
        /// <param name="context">AutoBuilder Scene Context (null 체크용)</param>
        public static void ValidateRuntimeSpawnPlaceholderPrefabs(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot validate placeholder prefabs.");
                return;
            }

            if (context == null)
            {
                Debug.LogError($"{LogPrefix} Context is null! Cannot validate placeholder prefabs.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-I-2: Validate Runtime Spawn Placeholder Prefabs =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. Database asset 존재 확인
            WorldMapRuntimeSpawnProfileDatabaseSO database = LoadDatabaseAsset();
            if (database != null)
            {
                log.AppendLine("  [PASS] RuntimeSpawnProfileDatabase asset exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] RuntimeSpawnProfileDatabase asset not found! Path: " + DatabaseAssetPath);
                failCount++;
                LogSummary(log, passCount, failCount, warnCount);
                Debug.LogWarning(log.ToString());
                return;
            }

            IReadOnlyList<WorldMapRuntimeSpawnProfileEntry> entries = database.Entries;

            // 2. Entry count > 0
            if (entries != null && entries.Count > 0)
            {
                log.AppendLine($"  [PASS] Entries count: {entries.Count} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] Entries count is 0! No entries to validate.");
                failCount++;
                LogSummary(log, passCount, failCount, warnCount);
                Debug.LogWarning(log.ToString());
                return;
            }

            // 통계
            int prefabNullCount = 0;
            int prefabSceneObjectCount = 0;
            int tagMissingCount = 0;
            int tagProfileIdMismatchCount = 0;
            int tagMarkerTypeMismatchCount = 0;
            int tagCategoryMismatchCount = 0;
            int tagKeyMismatchCount = 0;
            int meshFilterMissingCount = 0;
            int meshRendererMissingCount = 0;
            int colliderPresentCount = 0;
            int materialNullCount = 0;
            int standardShaderCount = 0;

            // category별 assigned count
            Dictionary<string, int> categoryAssignedCount = new Dictionary<string, int>();

            // MarkerType별 assigned count
            Dictionary<WorldMapZoneContentMarkerType, int> markerTypeAssignedCount = new Dictionary<WorldMapZoneContentMarkerType, int>();

            // 3. 모든 entry 검사
            foreach (WorldMapRuntimeSpawnProfileEntry entry in entries)
            {
                if (entry == null) continue;

                // Prefab null 검사
                if (entry.Prefab == null)
                {
                    log.AppendLine($"  [FAIL] Prefab null for entry '{entry.ProfileId}'.");
                    prefabNullCount++;
                    failCount++;
                    continue;
                }

                // Prefab이 asset prefab인지 확인
                bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(entry.Prefab)
                    || AssetDatabase.Contains(entry.Prefab);
                if (!isPrefabAsset)
                {
                    log.AppendLine($"  [FAIL] Prefab '{entry.Prefab.name}' (ProfileId: {entry.ProfileId}) is a scene object, not a prefab asset.");
                    prefabSceneObjectCount++;
                    failCount++;
                    continue;
                }

                // Prefab path로 placeholder 판정
                string prefabPath = AssetDatabase.GetAssetPath(entry.Prefab);
                bool isPlaceholderPrefab = prefabPath.StartsWith(PrefabRootFolder, StringComparison.OrdinalIgnoreCase);

                // Tag 검사
                WorldMapRuntimeSpawnPlaceholderTag tag = entry.Prefab.GetComponent<WorldMapRuntimeSpawnPlaceholderTag>();
                if (tag == null)
                {
                    if (isPlaceholderPrefab)
                    {
                        // Generated placeholder 폴더 안에 있는 prefab에 Tag가 없으면 FAIL
                        log.AppendLine($"  [FAIL] Placeholder prefab '{entry.Prefab.name}' (ProfileId: {entry.ProfileId}) missing WorldMapRuntimeSpawnPlaceholderTag.");
                        tagMissingCount++;
                        failCount++;
                    }
                    else
                    {
                        // 사용자 gameplay prefab이면 WARN
                        log.AppendLine($"  [WARN] User gameplay prefab '{entry.Prefab.name}' (ProfileId: {entry.ProfileId}) missing WorldMapRuntimeSpawnPlaceholderTag.");
                        tagMissingCount++;
                        warnCount++;
                    }
                }
                else
                {
                    // Tag.ProfileId == entry.ProfileId 확인
                    if (tag.ProfileId != entry.ProfileId)
                    {
                        log.AppendLine($"  [FAIL] Tag.ProfileId '{tag.ProfileId}' != entry.ProfileId '{entry.ProfileId}' for prefab '{entry.Prefab.name}'.");
                        tagProfileIdMismatchCount++;
                        failCount++;
                    }

                    // Tag.MarkerType == entry.MarkerType 확인
                    if (tag.MarkerType != entry.MarkerType)
                    {
                        log.AppendLine($"  [FAIL] Tag.MarkerType '{tag.MarkerType}' != entry.MarkerType '{entry.MarkerType}' for prefab '{entry.Prefab.name}'.");
                        tagMarkerTypeMismatchCount++;
                        failCount++;
                    }

                    // Tag.RuntimeCategory == entry.RuntimeCategory 확인
                    if (tag.RuntimeCategory != entry.RuntimeCategory)
                    {
                        log.AppendLine($"  [FAIL] Tag.RuntimeCategory '{tag.RuntimeCategory}' != entry.RuntimeCategory '{entry.RuntimeCategory}' for prefab '{entry.Prefab.name}'.");
                        tagCategoryMismatchCount++;
                        failCount++;
                    }

                    // Tag.RuntimeKey == entry.RuntimeKey 확인
                    if (tag.RuntimeKey != entry.RuntimeKey)
                    {
                        log.AppendLine($"  [FAIL] Tag.RuntimeKey '{tag.RuntimeKey}' != entry.RuntimeKey '{entry.RuntimeKey}' for prefab '{entry.Prefab.name}'.");
                        tagKeyMismatchCount++;
                        failCount++;
                    }
                }

                // MeshFilter 검사
                MeshFilter meshFilter = entry.Prefab.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    if (isPlaceholderPrefab)
                    {
                        log.AppendLine($"  [FAIL] Placeholder prefab '{entry.Prefab.name}' missing MeshFilter.");
                        meshFilterMissingCount++;
                        failCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [WARN] User gameplay prefab '{entry.Prefab.name}' missing MeshFilter.");
                        meshFilterMissingCount++;
                        warnCount++;
                    }
                }

                // MeshRenderer 검사
                MeshRenderer meshRenderer = entry.Prefab.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    if (isPlaceholderPrefab)
                    {
                        log.AppendLine($"  [FAIL] Placeholder prefab '{entry.Prefab.name}' missing MeshRenderer.");
                        meshRendererMissingCount++;
                        failCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [WARN] User gameplay prefab '{entry.Prefab.name}' missing MeshRenderer.");
                        meshRendererMissingCount++;
                        warnCount++;
                    }
                }

                // Collider 검사 (있으면 안 됨)
                Collider collider = entry.Prefab.GetComponent<Collider>();
                if (collider != null)
                {
                    if (isPlaceholderPrefab)
                    {
                        log.AppendLine($"  [FAIL] Placeholder prefab '{entry.Prefab.name}' has Collider (should not).");
                        colliderPresentCount++;
                        failCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [INFO] User gameplay prefab '{entry.Prefab.name}' has Collider (expected for gameplay).");
                        colliderPresentCount++;
                    }
                }

                // Material 검사
                if (meshRenderer != null)
                {
                    Material sharedMat = meshRenderer.sharedMaterial;
                    if (sharedMat == null)
                    {
                        log.AppendLine($"  [FAIL] Prefab '{entry.Prefab.name}' has null material.");
                        materialNullCount++;
                        failCount++;
                    }
                    else
                    {
                        // Standard shader 검사
                        if (sharedMat.shader != null && sharedMat.shader.name == "Standard")
                        {
                            if (isPlaceholderPrefab)
                            {
                                log.AppendLine($"  [FAIL] Placeholder prefab '{entry.Prefab.name}' uses Standard shader (should use URP).");
                                standardShaderCount++;
                                failCount++;
                            }
                            else
                            {
                                log.AppendLine($"  [WARN] User gameplay prefab '{entry.Prefab.name}' uses Standard shader.");
                                standardShaderCount++;
                                warnCount++;
                            }
                        }
                    }
                }

                // Category 통계
                string category = !string.IsNullOrEmpty(entry.RuntimeCategory) ? entry.RuntimeCategory : "Unknown";
                if (!categoryAssignedCount.ContainsKey(category))
                {
                    categoryAssignedCount[category] = 0;
                }
                categoryAssignedCount[category]++;

                // MarkerType 통계
                if (!markerTypeAssignedCount.ContainsKey(entry.MarkerType))
                {
                    markerTypeAssignedCount[entry.MarkerType] = 0;
                }
                markerTypeAssignedCount[entry.MarkerType]++;
            }

            // 4. Category별 assigned count 출력
            log.AppendLine("  [INFO] Category breakdown:");
            foreach (KeyValuePair<string, int> kvp in categoryAssignedCount)
            {
                log.AppendLine($"         Category '{kvp.Key}': {kvp.Value} entries");
            }

            // 5. MarkerType별 assigned count 출력
            log.AppendLine("  [INFO] MarkerType breakdown:");
            foreach (KeyValuePair<WorldMapZoneContentMarkerType, int> kvp in markerTypeAssignedCount)
            {
                log.AppendLine($"         MarkerType '{kvp.Key}': {kvp.Value} entries");
            }

            // 6. Summary
            int totalEntries = entries.Count;
            int validEntries = totalEntries - prefabNullCount - prefabSceneObjectCount;

            log.AppendLine($"  === Summary: PASS={passCount}, FAIL={failCount}, WARN={warnCount} ===");
            log.AppendLine($"  Total entries: {totalEntries}");
            log.AppendLine($"  Prefab null: {prefabNullCount}");
            log.AppendLine($"  Prefab scene object: {prefabSceneObjectCount}");
            log.AppendLine($"  Tag missing: {tagMissingCount}");
            log.AppendLine($"  Tag ProfileId mismatch: {tagProfileIdMismatchCount}");
            log.AppendLine($"  Tag MarkerType mismatch: {tagMarkerTypeMismatchCount}");
            log.AppendLine($"  Tag Category mismatch: {tagCategoryMismatchCount}");
            log.AppendLine($"  Tag Key mismatch: {tagKeyMismatchCount}");
            log.AppendLine($"  MeshFilter missing: {meshFilterMissingCount}");
            log.AppendLine($"  MeshRenderer missing: {meshRendererMissingCount}");
            log.AppendLine($"  Collider present: {colliderPresentCount}");
            log.AppendLine($"  Material null: {materialNullCount}");
            log.AppendLine($"  Standard shader: {standardShaderCount}");

            log.AppendLine("===== Phase 14.10-I-2: Validate Runtime Spawn Placeholder Prefabs Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// Database asset을 로드한다. 없으면 null 반환.
        /// </summary>
        private static WorldMapRuntimeSpawnProfileDatabaseSO LoadDatabaseAsset()
        {
            return AssetDatabase.LoadAssetAtPath<WorldMapRuntimeSpawnProfileDatabaseSO>(DatabaseAssetPath);
        }

        /// <summary>
        /// 지정된 경로의 폴더가 없으면 생성한다.
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            // 상위 폴더부터 순차적으로 생성
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string parentPath = currentPath;
                currentPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(currentPath))
                {
                    AssetDatabase.CreateFolder(parentPath, parts[i]);
                }
            }
        }

        /// <summary>
        /// ProfileId를 파일명으로 사용할 수 있도록 정화한다.
        /// 특수문자, 공백을 언더스코어로 치환한다.
        /// </summary>
        private static string SanitizeProfileId(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                return "UnnamedProfile";
            }

            // 파일명에 사용할 수 없는 문자를 언더스코어로 치환
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            string sanitized = profileId;
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // 공백도 언더스코어로 치환
            sanitized = sanitized.Replace(' ', '_');

            return sanitized;
        }

        /// <summary>
        /// entry의 metadata를 기반으로 placeholder GameObject를 생성한다.
        /// Transform, MeshFilter, MeshRenderer, WorldMapRuntimeSpawnPlaceholderTag를 보장한다.
        /// Collider는 추가하지 않는다.
        /// </summary>
        private static GameObject CreatePlaceholderGameObject(WorldMapRuntimeSpawnProfileEntry entry)
        {
            if (entry == null) return null;

            // 새 GameObject 생성
            GameObject go = new GameObject(entry.ProfileId);

            // Transform은 기본적으로 존재

            // MeshFilter + MeshRenderer 추가
            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();

            // Primitive mesh 설정
            PrimitiveType primitiveType = GetMarkerPrimitive(entry.MarkerType);
            GameObject primitiveGo = GameObject.CreatePrimitive(primitiveType);
            if (primitiveGo != null)
            {
                Mesh primitiveMesh = primitiveGo.GetComponent<MeshFilter>().sharedMesh;
                if (primitiveMesh != null)
                {
                    meshFilter.sharedMesh = primitiveMesh;
                }
                GameObject.DestroyImmediate(primitiveGo);
            }

            // Scale 설정
            Vector3 scale = GetMarkerScale(entry.MarkerType);
            go.transform.localScale = scale;

            // Material 설정
            Material material = GetOrCreateMarkerMaterial(entry.MarkerType);
            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            // WorldMapRuntimeSpawnPlaceholderTag 추가
            WorldMapRuntimeSpawnPlaceholderTag tag = go.AddComponent<WorldMapRuntimeSpawnPlaceholderTag>();
            tag.Configure(entry.ProfileId, entry.MarkerType, entry.RuntimeCategory, entry.RuntimeKey, entry.IsFallbackProfile);

            return go;
        }

        /// <summary>
        /// 기존 placeholder GameObject의 mesh, material, tag를 entry metadata 기준으로 갱신한다.
        /// </summary>
        private static void RebuildPlaceholderContent(GameObject prefabInstance, WorldMapRuntimeSpawnProfileEntry entry)
        {
            if (prefabInstance == null || entry == null) return;

            // MeshFilter 갱신
            MeshFilter meshFilter = prefabInstance.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = prefabInstance.AddComponent<MeshFilter>();
            }

            PrimitiveType primitiveType = GetMarkerPrimitive(entry.MarkerType);
            GameObject primitiveGo = GameObject.CreatePrimitive(primitiveType);
            if (primitiveGo != null)
            {
                Mesh primitiveMesh = primitiveGo.GetComponent<MeshFilter>().sharedMesh;
                if (primitiveMesh != null)
                {
                    meshFilter.sharedMesh = primitiveMesh;
                }
                GameObject.DestroyImmediate(primitiveGo);
            }

            // MeshRenderer 갱신
            MeshRenderer meshRenderer = prefabInstance.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = prefabInstance.AddComponent<MeshRenderer>();
            }

            Material material = GetOrCreateMarkerMaterial(entry.MarkerType);
            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            // Scale 갱신
            Vector3 scale = GetMarkerScale(entry.MarkerType);
            prefabInstance.transform.localScale = scale;

            // Tag 갱신
            WorldMapRuntimeSpawnPlaceholderTag tag = prefabInstance.GetComponent<WorldMapRuntimeSpawnPlaceholderTag>();
            if (tag == null)
            {
                tag = prefabInstance.AddComponent<WorldMapRuntimeSpawnPlaceholderTag>();
            }
            tag.Configure(entry.ProfileId, entry.MarkerType, entry.RuntimeCategory, entry.RuntimeKey, entry.IsFallbackProfile);

            // Collider 제거 (placeholder prefab에는 Collider가 없어야 함)
            Collider[] colliders = prefabInstance.GetComponents<Collider>();
            foreach (Collider c in colliders)
            {
                GameObject.DestroyImmediate(c);
            }
        }

        /// <summary>
        /// 기존 prefab asset의 내용을 로드하여 임시 GameObject를 반환한다.
        /// PrefabUtility.LoadPrefabContents를 사용한다.
        /// </summary>
        private static GameObject LoadPrefabContents(GameObject prefabAsset)
        {
            if (prefabAsset == null) return null;

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabPath)) return null;

            try
            {
                return PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Failed to load prefab contents: {prefabPath}. Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// MarkerType에 대응하는 PrimitiveType을 반환한다.
        /// </summary>
        private static PrimitiveType GetMarkerPrimitive(WorldMapZoneContentMarkerType markerType)
        {
            if (MarkerPrimitiveMap.TryGetValue(markerType, out PrimitiveType primitive))
            {
                return primitive;
            }
            return PrimitiveType.Cube;
        }

        /// <summary>
        /// MarkerType에 대응하는 local scale을 반환한다.
        /// </summary>
        private static Vector3 GetMarkerScale(WorldMapZoneContentMarkerType markerType)
        {
            if (MarkerScaleMap.TryGetValue(markerType, out Vector3 scale))
            {
                return scale;
            }
            return Vector3.one;
        }

        /// <summary>
        /// MarkerType에 대응하는 Material을 찾거나 생성한다.
        /// URP 호환 shader를 우선 사용하며, fallback으로 Unlit/Color를 사용한다.
        /// </summary>
        private static Material GetOrCreateMarkerMaterial(WorldMapZoneContentMarkerType markerType)
        {
            // Material 이름 결정
            if (!MaterialNameMap.TryGetValue(markerType, out string matName))
            {
                matName = $"RuntimeSpawnPlaceholder_{markerType}_Mat";
            }

            // Color 결정
            if (!MarkerColorMap.TryGetValue(markerType, out Color color))
            {
                color = Color.gray;
            }

            // Material 경로
            string matPath = $"{MaterialRootFolder}/{matName}.mat";

            // 기존 Material 확인
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
            {
                // 기존 material의 color를 갱신
                existingMat.color = color;
                EditorUtility.SetDirty(existingMat);
                return existingMat;
            }

            // 새 Material 생성: URP shader 우선 시도
            Shader selectedShader = null;
            foreach (string shaderName in UrpShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    selectedShader = shader;
                    break;
                }
            }

            // 최종 fallback: Unlit/Color
            if (selectedShader == null)
            {
                selectedShader = Shader.Find("Unlit/Color");
            }

            if (selectedShader == null)
            {
                Debug.LogWarning($"{LogPrefix} No suitable shader found for material '{matName}'. Using default material.");
                return null;
            }

            Material newMat = new Material(selectedShader);
            newMat.name = matName;
            newMat.color = color;

            AssetDatabase.CreateAsset(newMat, matPath);
            AssetDatabase.SaveAssets();

            return newMat;
        }

        /// <summary>
        /// 검증 실패 시 간략 Summary를 로그에 추가한다.
        /// </summary>
        private static void LogSummary(StringBuilder log, int passCount, int failCount, int warnCount)
        {
            log.AppendLine($"  === Summary: PASS={passCount}, FAIL={failCount}, WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-I-2: Validate Runtime Spawn Placeholder Prefabs Complete =====");
        }
    }
}
