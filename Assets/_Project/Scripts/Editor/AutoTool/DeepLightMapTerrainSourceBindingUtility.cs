using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Object = UnityEngine.Object;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 12: Terrain Source Binding Utility.
    /// Scene 내 기존 Terrain 오브젝트 또는 Project Asset을 안전하게 복제/배치한다.
    /// 원본 MapSettings/MicroVerse/ThirdParty 오브젝트는 절대 수정하지 않는다.
    /// </summary>
    public static class DeepLightMapTerrainSourceBindingUtility
    {
        // ===== Scene Source Root 검색 대상 이름 목록 =====
        private static readonly string[] SceneSourceRootNames = new string[]
        {
            "MicroVerse",
            "Terrain",
            "Underwater",
            "SW3_BackgroundTerrain"
        };

        // ===== Project Asset 검색 폴더 목록 =====
        private static readonly string[] ProjectAssetSearchFolders = new string[]
        {
            "Assets/ThirdParty/Stylized Water 3/_Demo/DemoAssets/Models",
            "Assets/ThirdParty/Stylized Water 3/_Demo/DemoAssets/Prefabs",
            "Assets/ThirdParty/Stylized Water 3/_Demo/Underwater/DemoAssets/Models",
            "Assets/ThirdParty/Stylized Water 3/_Demo/Underwater/DemoAssets/Prefabs"
        };

        // ===== Project Asset 우선 검색 키워드 =====
        private static readonly string[] AssetPriorityKeywords = new string[]
        {
            "underwater",
            "terrain",
            "canyon",
            "rock",
            "seabed",
            "background_terrain",
            "background terrain"
        };

        /// <summary>
        /// Phase 12: Terrain Source Binding을 재구축한다.
        /// GeneratedWorldRoot 하위에 TerrainSource/GeneratedTerrain 루트를 생성하고,
        /// Scene 또는 Project Asset에서 terrain source를 clone한다.
        /// </summary>
        public static void RebuildTerrainSourceBinding(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild terrain source binding.");
                return;
            }

            LogTerrainVerbose(settings, "===== Phase 12: Rebuild Terrain Source Binding =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // 2. TerrainSource 루트 생성 (GeneratedWorldRoot 하위)
            string terrainSourceRootName = settings.TerrainSourceRootName;
            GameObject terrainSourceRoot = GetOrCreateChild(generatedRoot, terrainSourceRootName);
            LogTerrainVerbose(settings, $"  [OK] TerrainSource root: {terrainSourceRootName}");

            // 3. GeneratedTerrain 루트 생성 (GeneratedWorldRoot 하위)
            string generatedTerrainRootName = settings.GeneratedTerrainRootName;
            GameObject generatedTerrainRoot = GetOrCreateChild(generatedRoot, generatedTerrainRootName);
            LogTerrainVerbose(settings, $"  [OK] GeneratedTerrain root: {generatedTerrainRootName}");

            // 4. 기존 TerrainSource/GeneratedTerrain 하위 clone 정리
            ClearTerrainClones(terrainSourceRoot, settings);
            ClearTerrainClones(generatedTerrainRoot, settings);

            // 5. Scene Source 검색 및 clone
            int sceneCloneCount = 0;
            if (settings.CloneTerrainSourceFromScene)
            {
                sceneCloneCount = CloneFromSceneSources(settings, generatedRoot, terrainSourceRoot);
            }
            else
            {
                LogTerrainVerbose(settings, "[SKIP] cloneTerrainSourceFromScene is false. Skipping scene source clone.");
            }

            // 6. Project Asset 검색 및 clone (Scene source가 없거나 부족할 때)
            int assetCloneCount = 0;
            if (settings.CloneTerrainSourceFromProjectAsset && sceneCloneCount == 0)
            {
                assetCloneCount = CloneFromProjectAssets(settings, generatedRoot, terrainSourceRoot);
            }
            else if (settings.CloneTerrainSourceFromProjectAsset && sceneCloneCount > 0)
            {
                LogTerrainVerbose(settings, "[SKIP] Scene source already found. Skipping project asset clone.");
            }
            else
            {
                LogTerrainVerbose(settings, "[SKIP] cloneTerrainSourceFromProjectAsset is false. Skipping project asset clone.");
            }

            // 7. Fallback warning
            int totalCloneCount = sceneCloneCount + assetCloneCount;
            if (totalCloneCount == 0)
            {
                Debug.LogWarning("[MapAutoBuilder] [WARN] No terrain source found from Scene or Project Asset. " +
                    "TerrainSource root is empty. Consider adding terrain assets or enabling fallback.");
            }

            // 8. Seafloor placeholder 숨김 처리
            if (settings.HideSeafloorPlaceholdersWhenTerrainSourceExists && totalCloneCount > 0)
            {
                HideSeafloorPlaceholders(settings, generatedRoot);
            }

            // 9. 완료 로그
            Debug.Log($"[MapAutoBuilder] Phase 12: Terrain Source Binding complete. " +
                $"Scene clones: {sceneCloneCount}, Asset clones: {assetCloneCount}, Total: {totalCloneCount}");

            // Selection 설정
            Selection.activeGameObject = terrainSourceRoot;
            EditorGUIUtility.PingObject(terrainSourceRoot);
        }

        /// <summary>
        /// Phase 12: Terrain Source Binding의 유효성을 검사한다.
        /// Non-destructive 검증만 수행하며, 실제 렌더링 상태를 변경하지 않는다.
        /// </summary>
        public static void ValidateTerrainSourceBinding(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate terrain source binding.");
                return;
            }

            var results = new List<ValidationResult>();
            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Terrain Source Binding (Phase 12) =====");

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            results.Add(new ValidationResult("GeneratedWorldRoot exists", generatedRoot != null));

            if (generatedRoot == null)
            {
                // Root가 없으면 이후 검증 불가
                log.AppendLine("[FAIL] GeneratedWorldRoot does not exist. Aborting validation.");
                log.AppendLine("Result: 0/1 passed, 1/1 failed.");
                log.AppendLine("===== Validate Terrain Source Binding Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. Terrain generated root exists
            string terrainSourceRootName = settings.TerrainSourceRootName;
            Transform terrainSourceTransform = generatedRoot.transform.Find(terrainSourceRootName);
            bool terrainSourceRootExists = terrainSourceTransform != null;
            results.Add(new ValidationResult("TerrainSource root exists", terrainSourceRootExists));

            // 3. At least one terrain source clone exists
            int cloneCount = 0;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        cloneCount++;
                    }
                }
            }
            bool hasAtLeastOneClone = cloneCount >= 1;
            results.Add(new ValidationResult("At least one terrain source clone exists", hasAtLeastOneClone));

            // 4. Clone is under GeneratedWorldRoot
            bool cloneUnderGenerated = true;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        if (!IsUnderGeneratedWorldRoot(child.gameObject, generatedRoot))
                        {
                            cloneUnderGenerated = false;
                            break;
                        }
                    }
                }
            }
            results.Add(new ValidationResult("Clone is under GeneratedWorldRoot", cloneUnderGenerated));

            // 5. Clone is NOT under MapSettings
            bool cloneNotUnderMapSettings = true;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        if (IsUnderProtectedRoot(child.gameObject, "MapSettings"))
                        {
                            cloneNotUnderMapSettings = false;
                            break;
                        }
                    }
                }
            }
            results.Add(new ValidationResult("Clone is NOT under MapSettings", cloneNotUnderMapSettings));

            // 6. Clone is NOT original ThirdParty asset object
            bool cloneNotOriginalAsset = true;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        // 원본 ThirdParty 경로에 있는지 확인
                        string assetPath = AssetDatabase.GetAssetPath(child.gameObject);
                        if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("ThirdParty"))
                        {
                            cloneNotOriginalAsset = false;
                            break;
                        }
                    }
                }
            }
            results.Add(new ValidationResult("Clone is NOT original ThirdParty asset object", cloneNotOriginalAsset));

            // 7. Renderer count >= 1
            int rendererCount = 0;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        rendererCount += child.GetComponentsInChildren<Renderer>(true).Length;
                    }
                }
            }
            results.Add(new ValidationResult("Renderer count >= 1", rendererCount >= 1));

            // 8. Missing material count == 0
            int missingMaterialCount = 0;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        missingMaterialCount += CountMissingMaterials(child.gameObject);
                    }
                }
            }
            results.Add(new ValidationResult("Missing material count == 0", missingMaterialCount == 0));

            // 9. Pink material risk count == 0
            int pinkMaterialRiskCount = 0;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        pinkMaterialRiskCount += CountPinkMaterialRisk(child.gameObject);
                    }
                }
            }
            results.Add(new ValidationResult("Pink material risk count == 0", pinkMaterialRiskCount == 0));

            // 10. Collider count >= 1 (warning pass 허용)
            int colliderCount = 0;
            if (terrainSourceTransform != null)
            {
                foreach (Transform child in terrainSourceTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        colliderCount += child.GetComponentsInChildren<Collider>(true).Length;
                    }
                }
            }
            bool hasCollider = colliderCount >= 1;
            results.Add(new ValidationResult("Collider count >= 1", hasCollider, isWarning: !hasCollider));

            // 11. Seafloor placeholders exist
            bool seafloorPlaceholdersExist = CheckSeafloorPlaceholdersExist(settings, generatedRoot);
            results.Add(new ValidationResult("Seafloor placeholders exist", seafloorPlaceholdersExist));

            // 12. Seafloor placeholders renderers disabled when terrain source exists
            bool seafloorRenderersDisabled = true;
            if (hasAtLeastOneClone && seafloorPlaceholdersExist)
            {
                seafloorRenderersDisabled = AreSeafloorRenderersDisabled(settings, generatedRoot);
            }
            results.Add(new ValidationResult("Seafloor placeholders renderers disabled when terrain source exists",
                seafloorRenderersDisabled));

            // 13. Seafloor placeholders colliders disabled when terrain source exists
            bool seafloorCollidersDisabled = true;
            if (hasAtLeastOneClone && seafloorPlaceholdersExist)
            {
                seafloorCollidersDisabled = AreSeafloorCollidersDisabled(settings, generatedRoot);
            }
            results.Add(new ValidationResult("Seafloor placeholders colliders disabled when terrain source exists",
                seafloorCollidersDisabled));

            // 14. MapSettings preserved
            GameObject mapSettings = GameObject.Find("MapSettings");
            results.Add(new ValidationResult("MapSettings preserved", mapSettings != null));

            // 15. _WorldMap_Manual preserved
            GameObject worldMapManual = GameObject.Find("_WorldMap_Manual");
            results.Add(new ValidationResult("_WorldMap_Manual preserved", worldMapManual != null));

            // 16. DeepLightMapAutoBuilderContext preserved
            GameObject builderContext = GameObject.Find("DeepLightMapAutoBuilderContext");
            results.Add(new ValidationResult("DeepLightMapAutoBuilderContext preserved", builderContext != null));

            // 결과 집계
            int passed = 0;
            int failed = 0;
            int warned = 0;
            foreach (var r in results)
            {
                if (r.IsWarning)
                {
                    warned++;
                    log.AppendLine($"[WARN] {r.Name}");
                }
                else if (r.Passed)
                {
                    passed++;
                    log.AppendLine($"[PASS] {r.Name}");
                }
                else
                {
                    failed++;
                    log.AppendLine($"[FAIL] {r.Name}");
                }
            }

            log.AppendLine($"Result: {passed}/{results.Count} passed, {failed}/{results.Count} failed, {warned}/{results.Count} warnings.");
            log.AppendLine("===== Validate Terrain Source Binding Complete =====");

            // Debug.LogWarning으로 출력 (찾기 쉽도록)
            Debug.LogWarning(log.ToString());

            // 실제 치명적 문제만 Debug.LogError
            if (failed > 0)
            {
                Debug.LogError($"[MapAutoBuilder] Phase 12 validation: {failed} failures detected. Check console for details.");
            }
        }

        // ======================================================================
        //  Internal: Scene Source Clone
        // ======================================================================

        /// <summary>
        /// Scene 내 MapSettings/MicroVerse 하위에서 terrain 후보를 찾아 clone한다.
        /// </summary>
        private static int CloneFromSceneSources(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject generatedRoot,
            GameObject terrainSourceRoot)
        {
            int cloneCount = 0;

            // MapSettings 검색
            GameObject mapSettings = GameObject.Find("MapSettings");
            if (mapSettings == null)
            {
                LogTerrainVerbose(settings, "[INFO] MapSettings not found in scene. Skipping scene source clone.");
                return 0;
            }

            // MicroVerse 하위 검색 (우선순위 1)
            Transform microVerseTransform = mapSettings.transform.Find("MicroVerse");
            if (microVerseTransform != null)
            {
                int microClones = CloneChildrenRecursive(settings, microVerseTransform.gameObject,
                    terrainSourceRoot, generatedRoot, "MicroVerse");
                cloneCount += microClones;
                if (microClones > 0)
                {
                    LogTerrainVerbose(settings, $"  [OK] Cloned {microClones} terrain objects from MapSettings/MicroVerse");
                }
            }

            // SceneSourceRootNames에 있는 다른 후보 검색
            foreach (string sourceName in SceneSourceRootNames)
            {
                if (sourceName == "MicroVerse") continue; // 이미 처리

                Transform sourceTransform = mapSettings.transform.Find(sourceName);
                if (sourceTransform == null)
                {
                    // Scene root에서도 검색
                    GameObject sceneRoot = GameObject.Find(sourceName);
                    if (sceneRoot != null)
                    {
                        sourceTransform = sceneRoot.transform;
                    }
                }

                if (sourceTransform != null)
                {
                    int subClones = CloneChildrenRecursive(settings, sourceTransform.gameObject,
                        terrainSourceRoot, generatedRoot, sourceName);
                    cloneCount += subClones;
                    if (subClones > 0)
                    {
                        LogTerrainVerbose(settings, $"  [OK] Cloned {subClones} terrain objects from {sourceName}");
                    }
                }
            }

            return cloneCount;
        }

        /// <summary>
        /// 부모 오브젝트의 자식들을 재귀적으로 검사하여 Renderer가 있는 오브젝트만 clone한다.
        /// </summary>
        private static int CloneChildrenRecursive(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject sourceParent,
            GameObject terrainSourceRoot,
            GameObject generatedRoot,
            string sourceLabel)
        {
            int count = 0;

            // sourceParent 자체에 Renderer가 있으면 clone
            Renderer parentRenderer = sourceParent.GetComponent<Renderer>();
            if (parentRenderer != null)
            {
                if (CloneSingleObject(settings, sourceParent, terrainSourceRoot, generatedRoot, sourceLabel))
                {
                    count++;
                }
                return count; // 부모 자체가 Renderer면 자식은 별도 clone하지 않음
            }

            // 자식들 검사
            foreach (Transform child in sourceParent.transform)
            {
                if (CloneSingleObject(settings, child.gameObject, terrainSourceRoot, generatedRoot, sourceLabel))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 단일 GameObject를 clone한다. Renderer가 있는 경우만 clone한다.
        /// </summary>
        private static bool CloneSingleObject(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject source,
            GameObject terrainSourceRoot,
            GameObject generatedRoot,
            string sourceLabel)
        {
            // Renderer가 없으면 skip
            Renderer renderer = source.GetComponent<Renderer>();
            if (renderer == null)
            {
                return false;
            }

            // 이미 GeneratedWorldRoot 하위에 있으면 skip (재귀 방지)
            if (IsUnderGeneratedWorldRoot(source, generatedRoot))
            {
                return false;
            }

            // 보호된 오브젝트면 skip
            if (DeepLightMapAutoBuilder.IsProtectedSceneObject(source))
            {
                return false;
            }

            // clone 실행
            string cloneName = $"{source.name}_{sourceLabel}_GeneratedClone";
            GameObject clone = Object.Instantiate(source);
            clone.name = cloneName;
            clone.transform.SetParent(terrainSourceRoot.transform);
            clone.transform.localPosition = source.transform.position;
            clone.transform.localRotation = source.transform.rotation;
            clone.transform.localScale = source.transform.lossyScale;

            // Fallback material 처리 (pink material 방지)
            FixMissingMaterials(clone, settings);

            // Collider 유지 (원본에 있으면 clone에도 유지)
            // MeshCollider/BoxCollider는 Instantiate 시 자동 복제되므로 추가 작업 불필요

            Undo.RegisterCreatedObjectUndo(clone, $"Clone Terrain: {cloneName}");

            LogTerrainVerbose(settings, $"    [CLONE] {cloneName} (from {sourceLabel}/{source.name})");
            return true;
        }

        // ======================================================================
        //  Internal: Project Asset Clone
        // ======================================================================

        /// <summary>
        /// Project Asset 폴더에서 terrain 관련 prefab/model을 검색하여 clone한다.
        /// </summary>
        private static int CloneFromProjectAssets(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject generatedRoot,
            GameObject terrainSourceRoot)
        {
            int cloneCount = 0;

            // 검색 폴더 순회
            foreach (string folder in ProjectAssetSearchFolders)
            {
                // 폴더 존재 확인
                string folderGuid = AssetDatabase.AssetPathToGUID(folder);
                if (string.IsNullOrEmpty(folderGuid))
                {
                    LogTerrainVerbose(settings, $"  [SKIP] Folder not found: {folder}");
                    continue;
                }

                // 폴더 내 모든 Prefab/Model 검색
                string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
                if (guids.Length == 0)
                {
                    LogTerrainVerbose(settings, $"  [INFO] No prefabs/models found in: {folder}");
                    continue;
                }

                // 우선순위 키워드 기반 정렬
                var candidates = new List<(string path, int priority)>();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string lowerName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                    int priority = int.MaxValue;
                    for (int i = 0; i < AssetPriorityKeywords.Length; i++)
                    {
                        if (lowerName.Contains(AssetPriorityKeywords[i].ToLowerInvariant()))
                        {
                            priority = i;
                            break;
                        }
                    }
                    candidates.Add((path, priority));
                }

                // 우선순위 정렬
                candidates.Sort((a, b) => a.priority.CompareTo(b.priority));

                // 상위 3개만 clone (너무 많으면 Hierarchy가 복잡해짐)
                int maxClonesPerFolder = 3;
                int folderCloneCount = 0;

                foreach (var candidate in candidates)
                {
                    if (folderCloneCount >= maxClonesPerFolder) break;

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(candidate.path);
                    if (prefab == null) continue;

                    // Renderer 확인
                    Renderer prefabRenderer = prefab.GetComponent<Renderer>();
                    if (prefabRenderer == null)
                    {
                        // 자식에 Renderer가 있는지 확인
                        bool hasChildRenderer = prefab.GetComponentInChildren<Renderer>() != null;
                        if (!hasChildRenderer) continue;
                    }

                    // Instantiate
                    string cloneName = $"{prefab.name}_Asset_GeneratedClone";
                    GameObject clone = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (clone == null)
                    {
                        clone = Object.Instantiate(prefab);
                    }

                    if (clone == null) continue;

                    clone.name = cloneName;
                    clone.transform.SetParent(terrainSourceRoot.transform);
                    clone.transform.localPosition = Vector3.zero;
                    clone.transform.localRotation = Quaternion.identity;
                    clone.transform.localScale = Vector3.one;

                    // Fallback material 처리
                    FixMissingMaterials(clone, settings);

                    Undo.RegisterCreatedObjectUndo(clone, $"Clone Terrain Asset: {cloneName}");
                    LogTerrainVerbose(settings, $"    [ASSET CLONE] {cloneName} (from {candidate.path})");

                    folderCloneCount++;
                    cloneCount++;
                }

                if (folderCloneCount > 0)
                {
                    LogTerrainVerbose(settings, $"  [OK] Cloned {folderCloneCount} assets from {folder}");
                }
            }

            return cloneCount;
        }

        // ======================================================================
        //  Internal: Seafloor Placeholder Hide
        // ======================================================================

        /// <summary>
        /// 모든 ZoneRoot/Environment/Seafloor 하위의 placeholder renderer/collider를 비활성화한다.
        /// </summary>
        private static void HideSeafloorPlaceholders(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject generatedRoot)
        {
            if (settings.WorldMapConfig == null) return;

            var config = settings.WorldMapConfig;
            int gridWidth = config.GridWidth;
            int gridHeight = config.GridHeight;

            // ZoneRoots 부모 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                LogTerrainVerbose(settings, "[SKIP] ZoneRoots not found. Cannot hide seafloor placeholders.");
                return;
            }

            int hiddenCount = 0;

            for (int row = 0; row < gridHeight; row++)
            {
                for (int col = 0; col < gridWidth; col++)
                {
                    string zoneId = $"{(char)('A' + row)}{col + 1}";
                    Transform zoneRoot = zoneRootsTransform.Find($"ZoneRoot_{zoneId}");
                    if (zoneRoot == null) continue;

                    // Environment/Seafloor 찾기
                    Transform envTransform = zoneRoot.Find("Environment");
                    if (envTransform == null) continue;

                    Transform seafloorRoot = envTransform.Find("Seafloor");
                    if (seafloorRoot == null) continue;

                    // Seafloor placeholder 찾기
                    string placeholderName = $"Seafloor_{zoneId}_PLACEHOLDER";
                    Transform placeholder = seafloorRoot.Find(placeholderName);
                    if (placeholder == null)
                    {
                        // 접미사 없는 경우도 확인
                        placeholder = seafloorRoot.Find($"Seafloor_{zoneId}");
                    }
                    if (placeholder == null) continue;

                    // Renderer 비활성화
                    MeshRenderer renderer = placeholder.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.enabled)
                    {
                        renderer.enabled = false;
                        hiddenCount++;
                    }

                    // Collider 비활성화
                    Collider collider = placeholder.GetComponent<Collider>();
                    if (collider != null && collider.enabled)
                    {
                        collider.enabled = false;
                    }
                }
            }

            LogTerrainVerbose(settings, $"  [OK] Hidden {hiddenCount} seafloor placeholder renderers.");
        }

        // ======================================================================
        //  Internal: Material Fix
        // ======================================================================

        /// <summary>
        /// clone된 오브젝트의 모든 Renderer에서 missing material을 fallback material로 교체한다.
        /// </summary>
        private static void FixMissingMaterials(GameObject target, DeepLightMapAutoBuilderSettingsSO settings)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Material fallback = settings.TerrainFallbackMaterial;

            foreach (Renderer r in renderers)
            {
                Material[] sharedMaterials = r.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    if (sharedMaterials[i] == null)
                    {
                        if (fallback != null)
                        {
                            sharedMaterials[i] = fallback;
                        }
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = sharedMaterials;
                }
            }
        }

        // ======================================================================
        //  Internal: Validation Helpers
        // ======================================================================

        /// <summary>
        /// 검증 결과 항목을 저장하는 구조체.
        /// </summary>
        private struct ValidationResult
        {
            public string Name;
            public bool Passed;
            public bool IsWarning;

            public ValidationResult(string name, bool passed, bool isWarning = false)
            {
                Name = name;
                Passed = passed;
                IsWarning = isWarning;
            }
        }

        /// <summary>
        /// 대상 GameObject가 GeneratedWorldRoot 하위에 있는지 확인한다.
        /// </summary>
        private static bool IsUnderGeneratedWorldRoot(GameObject target, GameObject generatedRoot)
        {
            if (target == null || generatedRoot == null) return false;

            Transform current = target.transform.parent;
            while (current != null)
            {
                if (current.gameObject == generatedRoot) return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// 대상 GameObject가 특정 이름의 보호된 루트 하위에 있는지 확인한다.
        /// </summary>
        private static bool IsUnderProtectedRoot(GameObject target, string rootName)
        {
            if (target == null) return false;

            Transform current = target.transform.parent;
            while (current != null)
            {
                if (current.name == rootName) return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// GameObject 내 모든 Renderer의 missing material 개수를 센다.
        /// </summary>
        private static int CountMissingMaterials(GameObject target)
        {
            int count = 0;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// GameObject 내 모든 Renderer의 pink material 위험도를 센다.
        /// sharedMaterial이 null이거나 missing shader인 경우 위험으로 간주한다.
        /// </summary>
        private static int CountPinkMaterialRisk(GameObject target)
        {
            int count = 0;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null)
                    {
                        count++;
                    }
                    else if (mat.shader == null)
                    {
                        count++;
                    }
                    else if (mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Seafloor placeholder가 하나라도 존재하는지 확인한다.
        /// </summary>
        private static bool CheckSeafloorPlaceholdersExist(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject generatedRoot)
        {
            if (settings.WorldMapConfig == null || generatedRoot == null) return false;

            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null) return false;

            foreach (Transform zoneRoot in zoneRootsTransform)
            {
                if (!zoneRoot.name.StartsWith("ZoneRoot_")) continue;

                Transform envTransform = zoneRoot.Find("Environment");
                if (envTransform == null) continue;

                Transform seafloorRoot = envTransform.Find("Seafloor");
                if (seafloorRoot == null) continue;

                foreach (Transform child in seafloorRoot)
                {
                    if (child.name.Contains("Seafloor") && child.name.Contains("PLACEHOLDER"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 모든 Seafloor placeholder의 Renderer가 비활성화되었는지 확인한다.
        /// </summary>
        private static bool AreSeafloorRenderersDisabled(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject generatedRoot)
        {
            if (settings.WorldMapConfig == null || generatedRoot == null) return true;

            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null) return true;

            foreach (Transform zoneRoot in zoneRootsTransform)
            {
                if (!zoneRoot.name.StartsWith("ZoneRoot_")) continue;

                Transform envTransform = zoneRoot.Find("Environment");
                if (envTransform == null) continue;

                Transform seafloorRoot = envTransform.Find("Seafloor");
                if (seafloorRoot == null) continue;

                foreach (Transform child in seafloorRoot)
                {
                    if (child.name.Contains("Seafloor") && child.name.Contains("PLACEHOLDER"))
                    {
                        MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                        if (renderer != null && renderer.enabled)
                        {
                            return false; // 하나라도 활성화되어 있으면 false
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 모든 Seafloor placeholder의 Collider가 비활성화되었는지 확인한다.
        /// </summary>
        private static bool AreSeafloorCollidersDisabled(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject generatedRoot)
        {
            if (settings.WorldMapConfig == null || generatedRoot == null) return true;

            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null) return true;

            foreach (Transform zoneRoot in zoneRootsTransform)
            {
                if (!zoneRoot.name.StartsWith("ZoneRoot_")) continue;

                Transform envTransform = zoneRoot.Find("Environment");
                if (envTransform == null) continue;

                Transform seafloorRoot = envTransform.Find("Seafloor");
                if (seafloorRoot == null) continue;

                foreach (Transform child in seafloorRoot)
                {
                    if (child.name.Contains("Seafloor") && child.name.Contains("PLACEHOLDER"))
                    {
                        Collider collider = child.GetComponent<Collider>();
                        if (collider != null && collider.enabled)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        // ======================================================================
        //  Internal: Utility
        // ======================================================================

        /// <summary>
        /// 부모 GameObject 아래에 자식 GameObject를 찾거나 생성한다.
        /// </summary>
        private static GameObject GetOrCreateChild(GameObject parent, string childName)
        {
            Transform existing = parent.transform.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            return child;
        }

        /// <summary>
        /// 지정된 루트 하위의 모든 _GeneratedClone 접미사 오브젝트를 정리한다.
        /// </summary>
        private static void ClearTerrainClones(GameObject root, DeepLightMapAutoBuilderSettingsSO settings)
        {
            var toDestroy = new List<GameObject>();
            foreach (Transform child in root.transform)
            {
                if (child.name.EndsWith("_GeneratedClone"))
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            foreach (GameObject obj in toDestroy)
            {
                Undo.DestroyObjectImmediate(obj);
            }

            if (toDestroy.Count > 0)
            {
                LogTerrainVerbose(settings, $"  [CLEAN] Removed {toDestroy.Count} existing terrain clones from {root.name}");
            }
        }

        /// <summary>
        /// settings.LogTerrainSourceVerbose가 true일 때만 로그를 출력한다.
        /// </summary>
        private static void LogTerrainVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings.LogTerrainSourceVerbose)
            {
                Debug.Log(message);
            }
        }
    }
}
