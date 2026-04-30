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
    /// Phase 13: Terrain Source Layout Stabilization Utility.
    /// Phase 12에서 가져온 terrain source clone들을 실제 월드맵에서 사용할 수 있는
    /// 안정적인 지형 root로 정리한다.
    /// 원본 MapSettings/MicroVerse/ThirdParty 오브젝트는 절대 수정하지 않는다.
    /// </summary>
    public static class DeepLightMapTerrainSourceLayoutUtility
    {
        // ===== Base Terrain 후보 키워드 =====
        private static readonly string[] BaseTerrainKeywords = new string[]
        {
            "terrain",
            "backgroundterrain",
            "seabed",
            "oceanfloor",
            "ground",
            "island",
            "canyon",
            "underwater"
        };

        // ===== Decorative 후보 키워드 =====
        private static readonly string[] DecorativeKeywords = new string[]
        {
            "rock",
            "boulder",
            "coral",
            "kelp",
            "grass",
            "vegetation",
            "ripple",
            "splash",
            "particle",
            "waterfall"
        };

        // ===== Phase 13.1: Decorative child 분류 키워드 (base terrain 내부 child 탐색용) =====
        // tree/trees 계열 billboard/vegetation child를 base terrain에서 분리하기 위한 키워드
        // backgroundtrees는 별도로 명시하여 backgroundterrain보다 우선 매치되도록 함
        private static readonly string[] DecorativeChildKeywords = new string[]
        {
            "backgroundtrees",
            "tree",
            "trees",
            "vegetation",
            "grass",
            "kelp",
            "coral",
            "rock",
            "boulder",
            "bush",
            "plant",
            "ripple",
            "splash",
            "foam",
            "particle",
            "waterfall",
            "fish",
            "sphere",
            "random"
        };

        // ===== Base terrain에 남겨도 되는 child 키워드 =====
        private static readonly string[] BaseTerrainChildKeywords = new string[]
        {
            "terrain",
            "backgroundterrain",
            "ground",
            "seabed",
            "seafloor",
            "island",
            "canyon",
            "mesh",
            "border",
            "cliff",
            "lake",
            "riverbed"
        };

        /// <summary>
        /// Phase 13: Terrain Source Layout을 재구축한다.
        /// TerrainSource 하위 clone들을 Base Terrain / Decorative / Disabled로 분류하고,
        /// 최종 Terrain root에 배치한다.
        /// </summary>
        public static void RebuildTerrainSourceLayout(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild terrain source layout.");
                return;
            }

            LogLayoutVerbose(settings, "===== Phase 13: Rebuild Terrain Source Layout =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // 2. TerrainSource 루트 찾기 (Phase 12에서 생성)
            string sourceRootName = settings.TerrainSourceStagingRootName;
            Transform sourceRootTransform = generatedRoot.transform.Find(sourceRootName);
            if (sourceRootTransform == null)
            {
                // TerrainSourceRootName으로도 fallback 검색
                sourceRootTransform = generatedRoot.transform.Find(settings.TerrainSourceRootName);
            }

            if (sourceRootTransform == null)
            {
                Debug.LogWarning("[MapAutoBuilder] [WARN] TerrainSource root not found. " +
                    "Run Phase 12 first or ensure TerrainSource exists under GeneratedWorldRoot.");
                LogLayoutVerbose(settings, "[SKIP] No TerrainSource root found. Skipping Phase 13.");
                return;
            }

            GameObject terrainSourceRoot = sourceRootTransform.gameObject;

            // 3. 최종 Terrain root 생성 (GeneratedWorldRoot 하위)
            string terrainRootName = settings.TerrainLayoutRootName;
            GameObject terrainRoot = GetOrCreateChild(generatedRoot, terrainRootName);
            LogLayoutVerbose(settings, $"  [OK] Terrain root: {terrainRootName}");

            // 4. Decorative Candidates root 생성
            string decorativeRootName = settings.DecorativeCandidateRootName;
            GameObject decorativeRoot = GetOrCreateChild(generatedRoot, decorativeRootName);
            LogLayoutVerbose(settings, $"  [OK] Decorative root: {decorativeRootName}");

            // 5. Disabled Candidates root 생성
            string disabledRootName = settings.DisabledCandidateRootName;
            GameObject disabledRoot = GetOrCreateChild(generatedRoot, disabledRootName);
            LogLayoutVerbose(settings, $"  [OK] Disabled root: {disabledRootName}");

            // 6. 기존 Terrain/Decorative/Disabled 하위 정리 (재생성 전 clean)
            ClearLayoutChildren(terrainRoot, settings);
            ClearLayoutChildren(decorativeRoot, settings);
            ClearLayoutChildren(disabledRoot, settings);

            // 7. TerrainSource 하위 clone 수집
            var allClones = new List<GameObject>();
            foreach (Transform child in terrainSourceRoot.transform)
            {
                if (child.name.EndsWith("_GeneratedClone"))
                {
                    allClones.Add(child.gameObject);
                }
            }

            if (allClones.Count == 0)
            {
                Debug.LogWarning("[MapAutoBuilder] [WARN] No terrain source clones found in TerrainSource root. " +
                    "Phase 13 will create empty roots.");
                LogLayoutVerbose(settings, "[SKIP] No clones to process.");
                return;
            }

            LogLayoutVerbose(settings, $"  [INFO] Found {allClones.Count} terrain source clones to classify.");

            // 8. 분류: Base Terrain / Decorative / Disabled
            var baseCandidates = new List<GameObject>();
            var decorativeCandidates = new List<GameObject>();
            var disabledCandidates = new List<GameObject>();

            foreach (GameObject clone in allClones)
            {
                string lowerName = clone.name.ToLowerInvariant();

                if (MatchesAnyKeyword(lowerName, BaseTerrainKeywords))
                {
                    baseCandidates.Add(clone);
                }
                else if (MatchesAnyKeyword(lowerName, DecorativeKeywords))
                {
                    decorativeCandidates.Add(clone);
                }
                else
                {
                    disabledCandidates.Add(clone);
                }
            }

            LogLayoutVerbose(settings, $"  [CLASSIFY] Base: {baseCandidates.Count}, Decorative: {decorativeCandidates.Count}, Disabled: {disabledCandidates.Count}");

            // 9. Base Terrain 선택 규칙
            //    - Scene source clone 우선
            //    - Renderer bounds size가 가장 큰 것을 1순위로 선택
            //    - maxBaseTerrainCloneCount만큼만 Terrain root에 배치
            int maxBaseCount = Mathf.Max(1, settings.MaxBaseTerrainCloneCount);

            // Scene source clone과 Project Asset clone 분리
            var sceneBaseCandidates = new List<GameObject>();
            var assetBaseCandidates = new List<GameObject>();

            foreach (GameObject candidate in baseCandidates)
            {
                if (candidate.name.Contains("_Asset_"))
                {
                    assetBaseCandidates.Add(candidate);
                }
                else
                {
                    sceneBaseCandidates.Add(candidate);
                }
            }

            // 최종 선택 리스트
            var selectedForTerrain = new List<GameObject>();

            if (settings.PreferSceneTerrainSource && sceneBaseCandidates.Count > 0)
            {
                // Scene source를 bounds size로 정렬
                sceneBaseCandidates.Sort((a, b) => GetRendererBoundsSize(b).CompareTo(GetRendererBoundsSize(a)));
                for (int i = 0; i < Mathf.Min(maxBaseCount, sceneBaseCandidates.Count); i++)
                {
                    selectedForTerrain.Add(sceneBaseCandidates[i]);
                }
            }
            else if (assetBaseCandidates.Count > 0)
            {
                // Project Asset fallback을 bounds size로 정렬
                assetBaseCandidates.Sort((a, b) => GetRendererBoundsSize(b).CompareTo(GetRendererBoundsSize(a)));
                for (int i = 0; i < Mathf.Min(maxBaseCount, assetBaseCandidates.Count); i++)
                {
                    selectedForTerrain.Add(assetBaseCandidates[i]);
                }
            }
            else if (baseCandidates.Count > 0)
            {
                // 전체 base 후보에서 bounds size로 정렬
                baseCandidates.Sort((a, b) => GetRendererBoundsSize(b).CompareTo(GetRendererBoundsSize(a)));
                for (int i = 0; i < Mathf.Min(maxBaseCount, baseCandidates.Count); i++)
                {
                    selectedForTerrain.Add(baseCandidates[i]);
                }
            }

            // 10. 선택된 Base Terrain을 Terrain root로 이동
            foreach (GameObject selected in selectedForTerrain)
            {
                MoveCloneToRoot(selected, terrainRoot, settings);
                LogLayoutVerbose(settings, $"  [BASE] {selected.name} -> {terrainRoot.name}");

                // autoFitBaseTerrainToMapBounds 처리
                if (settings.AutoFitBaseTerrainToMapBounds && settings.WorldMapConfig != null)
                {
                    FitTerrainToMapBounds(selected, settings);
                }
            }

            // 11. 선택되지 않은 base 후보는 DisabledCandidates로 이동 (통합 비활성화 적용)
            foreach (GameObject candidate in baseCandidates)
            {
                if (!selectedForTerrain.Contains(candidate))
                {
                    MoveCloneToRoot(candidate, disabledRoot, settings);
                    DisableCandidateObject(candidate);
                    LogLayoutVerbose(settings, $"  [DISABLED-BASE] {candidate.name} -> {disabledRoot.name} (not selected)");
                }
            }

            // 12. Decorative 후보를 DecorativeCandidates root로 이동 (통합 비활성화 적용)
            foreach (GameObject decorative in decorativeCandidates)
            {
                MoveCloneToRoot(decorative, decorativeRoot, settings);
                DisableCandidateObject(decorative);
                LogLayoutVerbose(settings, $"  [DECORATIVE] {decorative.name} -> {decorativeRoot.name}");
            }

            // 13. Disabled 후보를 DisabledCandidates root로 이동 (통합 비활성화 적용)
            foreach (GameObject disabled in disabledCandidates)
            {
                MoveCloneToRoot(disabled, disabledRoot, settings);
                DisableCandidateObject(disabled);
                LogLayoutVerbose(settings, $"  [DISABLED] {disabled.name} -> {disabledRoot.name}");
            }

            // 14. TerrainSource 하위의 사용하지 않는 clone Renderer 비활성화
            if (settings.DisableUnusedTerrainSourceRenderers)
            {
                foreach (Transform child in terrainSourceRoot.transform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        DisableRenderer(child.gameObject, settings);
                    }
                }
            }

            // 15. Phase 13.1: Final Terrain root 하위 child 정리 (decorative child 분리)
            // 항상 로그 출력 (verbose 무관)
            Debug.Log("[MapAutoBuilder] === Phase 13.1: Cleaning up final terrain children ===");
            int extractedCount = CleanupFinalTerrainChildren(settings, generatedRoot, terrainRoot, decorativeRoot, disabledRoot);
            Debug.Log($"[MapAutoBuilder] Phase 13.1: Extracted {extractedCount} decorative children from final terrain.");

            // 16. Phase 13.2: SW3 terrain source material binding
            Debug.Log("[MapAutoBuilder] === Phase 13.2: Applying SW3 terrain source materials ===");
            int materialAssignedCount = ApplySw3TerrainSourceMaterials(generatedRoot);
            Debug.Log($"[MapAutoBuilder] Phase 13.2: Assigned materials to {materialAssignedCount} renderer(s).");

            // 17. Phase 13.2: Underwater collision policy
            Debug.Log("[MapAutoBuilder] === Phase 13.2: Applying terrain collision policy ===");
            ApplyTerrainCollisionPolicy(generatedRoot);

            // 18. 완료 로그
            Debug.Log($"[MapAutoBuilder] Phase 13: Terrain Source Layout complete. " +
                $"Base terrain: {selectedForTerrain.Count}, " +
                $"Decorative: {decorativeCandidates.Count}, " +
                $"Disabled: {disabledCandidates.Count + (baseCandidates.Count - selectedForTerrain.Count)}");


            // Selection 설정
            Selection.activeGameObject = terrainRoot;
            EditorGUIUtility.PingObject(terrainRoot);
        }

        /// <summary>
        /// Phase 13: Terrain Source Layout의 유효성을 검사한다.
        /// Non-destructive 검증만 수행하며, 실제 렌더링 상태를 변경하지 않는다.
        /// Phase 13.1 검증 항목 포함: Terrain root 내 decorative child 없음,
        /// DecorativeCandidates/DisabledCandidates 하위 Renderer/Collider disabled 확인.
        /// </summary>
        public static void ValidateTerrainSourceLayout(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate terrain source layout.");
                return;
            }

            var results = new List<ValidationResult>();
            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Terrain Source Layout (Phase 13) =====");

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            results.Add(new ValidationResult("GeneratedWorldRoot exists", generatedRoot != null));

            if (generatedRoot == null)
            {
                log.AppendLine("[FAIL] GeneratedWorldRoot does not exist. Aborting validation.");
                log.AppendLine("Result: 0/1 passed, 1/1 failed.");
                log.AppendLine("===== Validate Terrain Source Layout Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. TerrainSource root exists (비어 있어도 허용, 없으면 WARN)
            string sourceRootName = settings.TerrainSourceStagingRootName;
            Transform sourceRootTransform = generatedRoot.transform.Find(sourceRootName);
            if (sourceRootTransform == null)
            {
                sourceRootTransform = generatedRoot.transform.Find(settings.TerrainSourceRootName);
            }
            results.Add(new ValidationResult("TerrainSource root exists", sourceRootTransform != null, isWarning: sourceRootTransform == null));

            // 3. Terrain root exists
            string terrainRootName = settings.TerrainLayoutRootName;
            Transform terrainRootTransform = generatedRoot.transform.Find(terrainRootName);
            bool terrainRootExists = terrainRootTransform != null;
            results.Add(new ValidationResult("Terrain root exists", terrainRootExists));

            // 4. Terrain root has at least one final base terrain object
            int terrainChildCount = 0;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        terrainChildCount++;
                    }
                }
            }
            bool hasBaseTerrain = terrainChildCount >= 1;
            results.Add(new ValidationResult("Terrain root has at least one final base terrain object", hasBaseTerrain));

            // 5. Final base terrain is under GeneratedWorldRoot
            bool baseTerrainUnderGenerated = true;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        if (!IsUnderGameObject(child.gameObject, generatedRoot))
                        {
                            baseTerrainUnderGenerated = false;
                            break;
                        }
                    }
                }
            }
            results.Add(new ValidationResult("Final base terrain is under GeneratedWorldRoot", baseTerrainUnderGenerated));

            // 6. Final base terrain is NOT under MapSettings
            bool baseTerrainNotUnderMapSettings = true;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        if (IsUnderNamedRoot(child.gameObject, "MapSettings"))
                        {
                            baseTerrainNotUnderMapSettings = false;
                            break;
                        }
                    }
                }
            }
            results.Add(new ValidationResult("Final base terrain is NOT under MapSettings", baseTerrainNotUnderMapSettings));

            // 7. Final base terrain is NOT original ThirdParty asset
            bool baseTerrainNotOriginalAsset = true;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        string assetPath = AssetDatabase.GetAssetPath(child.gameObject);
                        if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("ThirdParty"))
                        {
                            baseTerrainNotOriginalAsset = false;
                            break;
                        }
                    }
                }
            }
            results.Add(new ValidationResult("Final base terrain is NOT original ThirdParty asset", baseTerrainNotOriginalAsset));

            // 8. Final base terrain Renderer count >= 1
            int baseRendererCount = 0;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        baseRendererCount += child.GetComponentsInChildren<Renderer>(true).Length;
                    }
                }
            }
            results.Add(new ValidationResult("Final base terrain Renderer count >= 1", baseRendererCount >= 1));

            // 9. Final base terrain Missing material count == 0
            int baseMissingMaterialCount = 0;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        baseMissingMaterialCount += CountMissingMaterials(child.gameObject);
                    }
                }
            }
            results.Add(new ValidationResult("Final base terrain Missing material count == 0", baseMissingMaterialCount == 0));

            // 10. Final base terrain Pink material risk count == 0
            int basePinkMaterialCount = 0;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        basePinkMaterialCount += CountPinkMaterialRisk(child.gameObject);
                    }
                }
            }
            results.Add(new ValidationResult("Final base terrain Pink material risk count == 0", basePinkMaterialCount == 0));

            // 11. Final base terrain bounds covers at least 70% of map X/Z bounds
            bool boundsCoverageOk = true;
            if (terrainRootTransform != null && settings.WorldMapConfig != null)
            {
                boundsCoverageOk = CheckBoundsCoverage(terrainRootTransform, settings);
            }
            results.Add(new ValidationResult("Final base terrain bounds covers at least 70% of map X/Z bounds",
                boundsCoverageOk, isWarning: !boundsCoverageOk));

            // 12. No more than maxBaseTerrainCloneCount active base terrain objects
            int activeBaseCount = 0;
            if (terrainRootTransform != null)
            {
                foreach (Transform child in terrainRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone") && child.gameObject.activeInHierarchy)
                    {
                        activeBaseCount++;
                    }
                }
            }
            bool noExcessiveActive = activeBaseCount <= Mathf.Max(1, settings.MaxBaseTerrainCloneCount);
            results.Add(new ValidationResult($"No more than {Mathf.Max(1, settings.MaxBaseTerrainCloneCount)} active base terrain objects",
                noExcessiveActive));

            // 13. No duplicated active terrain clones stacked at origin
            bool noDuplicatesAtOrigin = CheckNoDuplicatesAtOrigin(terrainRootTransform);
            results.Add(new ValidationResult("No duplicated active terrain clones stacked at origin", noDuplicatesAtOrigin));

            // 14. Decorative candidates are separated from final Terrain root
            string decorativeRootName = settings.DecorativeCandidateRootName;
            Transform decorativeRootTransform = generatedRoot.transform.Find(decorativeRootName);
            bool decorativeSeparated = decorativeRootTransform != null;
            results.Add(new ValidationResult("Decorative candidates are separated from final Terrain root", decorativeSeparated));

            // 15. Disabled candidates are inactive or renderer disabled
            string disabledRootName = settings.DisabledCandidateRootName;
            Transform disabledRootTransform = generatedRoot.transform.Find(disabledRootName);
            bool disabledInactive = true;
            if (disabledRootTransform != null)
            {
                foreach (Transform child in disabledRootTransform)
                {
                    if (child.name.EndsWith("_GeneratedClone"))
                    {
                        if (child.gameObject.activeSelf)
                        {
                            // activeSelf가 true여도 Renderer가 disabled면 허용
                            Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                            bool anyRendererEnabled = false;
                            foreach (Renderer r in renderers)
                            {
                                if (r.enabled)
                                {
                                    anyRendererEnabled = true;
                                    break;
                                }
                            }
                            if (anyRendererEnabled)
                            {
                                disabledInactive = false;
                                break;
                            }
                        }
                    }
                }
            }
            results.Add(new ValidationResult("Disabled candidates are inactive or renderer disabled", disabledInactive));

            // 16. Seafloor placeholder renderers disabled
            bool seafloorRenderersDisabled = CheckSeafloorRenderersDisabled(settings, generatedRoot);
            results.Add(new ValidationResult("Seafloor placeholder renderers disabled", seafloorRenderersDisabled));

            // 17. Seafloor placeholder colliders disabled
            bool seafloorCollidersDisabled = CheckSeafloorCollidersDisabled(settings, generatedRoot);
            results.Add(new ValidationResult("Seafloor placeholder colliders disabled", seafloorCollidersDisabled));

            // 18. MapSettings preserved
            GameObject mapSettings = GameObject.Find("MapSettings");
            results.Add(new ValidationResult("MapSettings preserved", mapSettings != null));

            // 19. _WorldMap_Manual preserved
            GameObject worldMapManual = GameObject.Find("_WorldMap_Manual");
            results.Add(new ValidationResult("_WorldMap_Manual preserved", worldMapManual != null));

            // 20. DeepLightMapAutoBuilderContext preserved
            GameObject builderContext = GameObject.Find("DeepLightMapAutoBuilderContext");
            results.Add(new ValidationResult("DeepLightMapAutoBuilderContext preserved", builderContext != null));

            // ===== Phase 13.1: Decorative child cleanup validation =====

            // 21. Terrain root 내부에 DecorativeChildKeywords 계열 active Renderer가 없어야 함
            bool noDecorativeActiveRendererInTerrain = true;
            if (terrainRootTransform != null)
            {
                noDecorativeActiveRendererInTerrain = CheckNoDecorativeActiveRendererInTerrain(terrainRootTransform);
            }
            results.Add(new ValidationResult("Terrain root has no decorative active Renderer (tree/vegetation/grass etc.)",
                noDecorativeActiveRendererInTerrain));

            // 22. TerrainDecorativeCandidates root exists
            Transform decorativeRootTransform2 = generatedRoot.transform.Find(decorativeRootName);
            bool decorativeRootExists = decorativeRootTransform2 != null;
            results.Add(new ValidationResult("TerrainDecorativeCandidates root exists", decorativeRootExists));

            // 23. TerrainDecorativeCandidates 하위 모든 Renderer disabled 또는 GameObject inactive
            bool decorativeAllInactive = true;
            if (decorativeRootTransform2 != null)
            {
                decorativeAllInactive = CheckAllRenderersDisabledOrInactive(decorativeRootTransform2);
            }
            results.Add(new ValidationResult("TerrainDecorativeCandidates all Renderer disabled or GameObject inactive",
                decorativeAllInactive));

            // 24. TerrainDecorativeCandidates 하위 모든 Collider disabled 또는 GameObject inactive
            bool decorativeAllCollidersDisabled = true;
            if (decorativeRootTransform2 != null)
            {
                decorativeAllCollidersDisabled = CheckAllCollidersDisabledOrInactive(decorativeRootTransform2);
            }
            results.Add(new ValidationResult("TerrainDecorativeCandidates all Collider disabled or GameObject inactive",
                decorativeAllCollidersDisabled));

            // 25. TerrainDisabledCandidates root exists
            Transform disabledRootTransform2 = generatedRoot.transform.Find(disabledRootName);
            bool disabledRootExists = disabledRootTransform2 != null;
            results.Add(new ValidationResult("TerrainDisabledCandidates root exists", disabledRootExists));

            // 26. TerrainDisabledCandidates 하위 모든 Renderer disabled 또는 GameObject inactive
            bool disabledAllInactive = true;
            if (disabledRootTransform2 != null)
            {
                disabledAllInactive = CheckAllRenderersDisabledOrInactive(disabledRootTransform2);
            }
            results.Add(new ValidationResult("TerrainDisabledCandidates all Renderer disabled or GameObject inactive",
                disabledAllInactive));

            // 27. TerrainDisabledCandidates 하위 모든 Collider disabled 또는 GameObject inactive
            bool disabledAllCollidersDisabled = true;
            if (disabledRootTransform2 != null)
            {
                disabledAllCollidersDisabled = CheckAllCollidersDisabledOrInactive(disabledRootTransform2);
            }
            results.Add(new ValidationResult("TerrainDisabledCandidates all Collider disabled or GameObject inactive",
                disabledAllCollidersDisabled));

            // ===== Phase 13.2: SW3 Material Binding + Collision Policy Validation =====

            // 28. Terrain root 하위 SW3_BackgroundTerrain 계열 Renderer가 SW3_BackdropTerrain material 사용
            bool terrainBaseMatOk = true;
            if (terrainRootTransform != null)
            {
                terrainBaseMatOk = CheckTerrainBaseMaterials(terrainRootTransform);
            }
            results.Add(new ValidationResult("Terrain base renderers use SW3_BackdropTerrain material",
                terrainBaseMatOk, isWarning: !terrainBaseMatOk));

            // 29. Terrain root 하위 BackgroundTrees/tree/billboard 계열 Renderer가 SW3_BackdropTerrain_FagaceaeBillboard material 사용
            bool treeBillboardMatOk = true;
            if (terrainRootTransform != null)
            {
                treeBillboardMatOk = CheckTreeBillboardMaterials(terrainRootTransform);
            }
            results.Add(new ValidationResult("Tree/billboard renderers use SW3_BackdropTerrain_FagaceaeBillboard material",
                treeBillboardMatOk, isWarning: !treeBillboardMatOk));

            // 30. BackgroundTrees/tree/billboard 계열에 enabled Collider가 없어야 함
            bool noEnabledColliderOnDecorative = true;
            if (terrainRootTransform != null)
            {
                noEnabledColliderOnDecorative = CheckNoEnabledColliderOnDecorative(terrainRootTransform);
            }
            results.Add(new ValidationResult("No enabled Collider on decorative objects (tree/billboard/vegetation/grass)",
                noEnabledColliderOnDecorative));

            // 31. Base terrain에 MeshCollider가 최소 1개 있어야 함
            bool hasMeshCollider = false;
            if (terrainRootTransform != null)
            {
                hasMeshCollider = CheckHasMeshColliderOnBaseTerrain(terrainRootTransform);
            }
            results.Add(new ValidationResult("Base terrain has at least 1 MeshCollider for underwater collision",
                hasMeshCollider));

            // 32. Terrain root 하위 MeshCollider 개수 검사 (1: PASS, 2~9: WARN, 10+: FAIL)
            int meshColliderCount = 0;
            if (terrainRootTransform != null)
            {
                meshColliderCount = CountMeshColliders(terrainRootTransform);
            }
            if (meshColliderCount == 0)
            {
                results.Add(new ValidationResult("Terrain root MeshCollider count (0 = FAIL)", false));
            }
            else if (meshColliderCount >= 10)
            {
                results.Add(new ValidationResult($"Terrain root MeshCollider count ({meshColliderCount} >= 10 = FAIL)", false));
            }
            else if (meshColliderCount >= 2)
            {
                results.Add(new ValidationResult($"Terrain root MeshCollider count ({meshColliderCount} = WARN)", true, isWarning: true));
            }
            else
            {
                results.Add(new ValidationResult($"Terrain root MeshCollider count ({meshColliderCount} = PASS)", true));
            }

            // 33. Terrain root 하위 Rigidbody가 없어야 함
            bool noRigidbody = true;
            if (terrainRootTransform != null)
            {
                noRigidbody = CheckNoRigidbodyInTerrain(terrainRootTransform);
            }
            results.Add(new ValidationResult("No Rigidbody in Terrain root", noRigidbody));

            // 34. Terrain root 하위 missing material count == 0
            int missingMatCount = 0;
            if (terrainRootTransform != null)
            {
                missingMatCount = CountMissingMaterialsInTerrain(terrainRootTransform);
            }
            results.Add(new ValidationResult("Terrain root missing material count == 0", missingMatCount == 0));

            // 35. Terrain root 하위 pink material risk count == 0
            int pinkMatCount = 0;
            if (terrainRootTransform != null)
            {
                pinkMatCount = CountPinkMaterialRiskInTerrain(terrainRootTransform);
            }
            results.Add(new ValidationResult("Terrain root pink material risk count == 0", pinkMatCount == 0));

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
            log.AppendLine("===== Validate Terrain Source Layout Complete =====");

            // Debug.LogWarning으로 출력 (찾기 쉽도록)
            Debug.LogWarning(log.ToString());

            // 실제 치명적 문제만 Debug.LogError
            if (failed > 0)
            {
                Debug.LogError($"[MapAutoBuilder] Phase 13 validation: {failed} failures detected. Check console for details.");
            }
        }

        // ======================================================================
        //  Internal: Classification Helpers
        // ======================================================================

        /// <summary>
        /// 이름이 키워드 목록 중 하나라도 포함하는지 확인한다.
        /// </summary>
        private static bool MatchesAnyKeyword(string lowerName, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (lowerName.Contains(keyword))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// GameObject의 모든 Renderer bounds를 합친 크기를 반환한다.
        /// Renderer가 없으면 0을 반환한다.
        /// </summary>
        private static float GetRendererBoundsSize(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }

            return combined.size.x * combined.size.z;
        }

        /// <summary>
        /// Clone을 대상 root로 이동한다.
        /// world transform 유지 옵션에 따라 SetParent 방식을 결정한다.
        /// </summary>
        private static void MoveCloneToRoot(GameObject clone, GameObject targetRoot, DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings.PreserveTerrainSourceWorldTransform)
            {
                // world position/rotation/scale 유지
                Vector3 worldPos = clone.transform.position;
                Quaternion worldRot = clone.transform.rotation;
                Vector3 worldScale = clone.transform.lossyScale;

                clone.transform.SetParent(targetRoot.transform);
                clone.transform.SetPositionAndRotation(worldPos, worldRot);

                // lossyScale은 직접 설정 불가능하므로 localScale 조정
                // 부모 스케일이 1,1,1이라고 가정
                clone.transform.localScale = worldScale;
            }
            else
            {
                clone.transform.SetParent(targetRoot.transform);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Base Terrain을 Map Bounds에 맞춘다.
        /// X/Z 중심으로 fit하며, Y scale은 과도하게 늘리지 않는다.
        /// </summary>
        private static void FitTerrainToMapBounds(GameObject terrain, DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings.WorldMapConfig == null) return;

            var config = settings.WorldMapConfig;
            float mapWidth = config.GridWidth * config.ZoneSize;
            float mapDepth = config.GridHeight * config.ZoneSize;
            float mapCenterX = (config.WorldMinX + config.WorldMaxX) / 2f;
            float mapCenterZ = (config.WorldMinZ + config.WorldMaxZ) / 2f;

            // Terrain의 현재 Renderer bounds 계산
            Renderer[] renderers = terrain.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds terrainBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                terrainBounds.Encapsulate(renderers[i].bounds);
            }

            // X/Z 스케일 계산
            float scaleX = mapWidth / Mathf.Max(terrainBounds.size.x, 0.01f);
            float scaleZ = mapDepth / Mathf.Max(terrainBounds.size.z, 0.01f);
            float uniformScale = Mathf.Max(scaleX, scaleZ);

            // Y 스케일은 X/Z 평균의 절반으로 제한 (과도한 Y 스케일링 방지)
            float avgXZScale = (scaleX + scaleZ) / 2f;
            float scaleY = Mathf.Min(avgXZScale, 1f);

            // Uniform scale 적용 (X/Z 기준)
            terrain.transform.localScale = new Vector3(uniformScale, scaleY, uniformScale);

            // Bounds center 기준으로 map center에 맞춤
            // SetPositionAndRotation 사용 (world transform 유지)
            Vector3 offset = terrainBounds.center - terrain.transform.position;
            Vector3 newPos = new Vector3(
                mapCenterX - offset.x * uniformScale,
                terrain.transform.position.y,
                mapCenterZ - offset.z * uniformScale
            );
            terrain.transform.position = newPos;
        }

        /// <summary>
        /// Clone을 inactive 상태로 설정한다.
        /// </summary>
        private static void SetCloneInactive(GameObject clone, DeepLightMapAutoBuilderSettingsSO settings)
        {
            clone.SetActive(false);
        }

        /// <summary>
        /// Clone의 모든 Renderer를 비활성화한다.
        /// </summary>
        private static void DisableRenderer(GameObject clone, DeepLightMapAutoBuilderSettingsSO settings)
        {
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = false;
            }
        }

        /// <summary>
        /// Phase 13.1: 후보 오브젝트에 대해 Renderer/Collider 비활성화 + GameObject inactive를 통합 적용한다.
        /// DecorativeCandidates와 DisabledCandidates 하위 후보들은 아직 배치 전이므로 Scene/Game View에 보이면 안 된다.
        /// </summary>
        private static void DisableCandidateObject(GameObject target)
        {
            if (target == null) return;

            // 모든 Renderer 비활성화
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = false;
            }

            // 모든 Collider 비활성화
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }

            // GameObject 자체 비활성화
            target.SetActive(false);
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
        /// 대상 GameObject가 특정 GameObject 하위에 있는지 확인한다.
        /// </summary>
        private static bool IsUnderGameObject(GameObject target, GameObject root)
        {
            if (target == null || root == null) return false;

            Transform current = target.transform.parent;
            while (current != null)
            {
                if (current.gameObject == root) return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// 대상 GameObject가 특정 이름의 루트 하위에 있는지 확인한다.
        /// </summary>
        private static bool IsUnderNamedRoot(GameObject target, string rootName)
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
        /// Base Terrain의 Renderer bounds가 Map X/Z Bounds의 70% 이상을 덮는지 확인한다.
        /// </summary>
        private static bool CheckBoundsCoverage(Transform terrainRoot, DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings.WorldMapConfig == null) return true;

            var config = settings.WorldMapConfig;
            float mapWidth = config.GridWidth * config.ZoneSize;
            float mapDepth = config.GridHeight * config.ZoneSize;

            // Terrain root 하위 모든 Renderer bounds 합산
            Bounds? combinedBounds = null;
            foreach (Transform child in terrainRoot)
            {
                if (!child.name.EndsWith("_GeneratedClone")) continue;

                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    if (!r.enabled) continue;
                    if (combinedBounds == null)
                    {
                        combinedBounds = r.bounds;
                    }
                    else
                    {
                        combinedBounds.Value.Encapsulate(r.bounds);
                    }
                }
            }

            if (combinedBounds == null) return false;

            float coverageX = combinedBounds.Value.size.x / Mathf.Max(mapWidth, 0.01f);
            float coverageZ = combinedBounds.Value.size.z / Mathf.Max(mapDepth, 0.01f);

            return coverageX >= 0.7f || coverageZ >= 0.7f;
        }

        /// <summary>
        /// Terrain root에 중복된 clone이 origin에 겹쳐 있는지 확인한다.
        /// </summary>
        private static bool CheckNoDuplicatesAtOrigin(Transform terrainRoot)
        {
            if (terrainRoot == null) return true;

            var positions = new HashSet<Vector3>();
            foreach (Transform child in terrainRoot)
            {
                if (!child.name.EndsWith("_GeneratedClone")) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                // 위치를 반올림하여 근사 비교
                Vector3 roundedPos = new Vector3(
                    Mathf.Round(child.position.x * 10f) / 10f,
                    Mathf.Round(child.position.y * 10f) / 10f,
                    Mathf.Round(child.position.z * 10f) / 10f
                );

                if (positions.Contains(roundedPos))
                {
                    return false; // 중복 발견
                }
                positions.Add(roundedPos);
            }

            return true;
        }

        /// <summary>
        /// 모든 Seafloor placeholder의 Renderer가 비활성화되었는지 확인한다.
        /// </summary>
        private static bool CheckSeafloorRenderersDisabled(
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
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 모든 Seafloor placeholder의 Collider가 비활성화되었는지 확인한다.
        /// </summary>
        private static bool CheckSeafloorCollidersDisabled(
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

        /// <summary>
        /// Phase 13.1: Terrain root 내부에 DecorativeChildKeywords 계열 active Renderer가 있는지 확인한다.
        /// </summary>
        private static bool CheckNoDecorativeActiveRendererInTerrain(Transform terrainRoot)
        {
            if (terrainRoot == null) return true;

            foreach (Transform baseTerrain in terrainRoot)
            {
                if (!baseTerrain.name.EndsWith("_GeneratedClone")) continue;

                // base terrain 내부의 모든 Renderer 검사
                Renderer[] renderers = baseTerrain.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;

                    string lowerName = r.gameObject.name.ToLowerInvariant();

                    // BaseTerrainChildKeywords에 포함되면 허용
                    if (MatchesAnyKeyword(lowerName, BaseTerrainChildKeywords))
                    {
                        continue;
                    }

                    // DecorativeChildKeywords에 포함되면 fail
                    if (MatchesAnyKeyword(lowerName, DecorativeChildKeywords))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Phase 13.1: Transform 하위의 모든 Renderer가 disabled이거나 GameObject가 inactive인지 확인한다.
        /// </summary>
        private static bool CheckAllRenderersDisabledOrInactive(Transform root)
        {
            if (root == null) return true;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Phase 13.1: Transform 하위의 모든 Collider가 disabled이거나 GameObject가 inactive인지 확인한다.
        /// </summary>
        private static bool CheckAllCollidersDisabledOrInactive(Transform root)
        {
            if (root == null) return true;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                if (c.enabled && c.gameObject.activeInHierarchy)
                {
                    return false;
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
        private static void ClearLayoutChildren(GameObject root, DeepLightMapAutoBuilderSettingsSO settings)
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
                LogLayoutVerbose(settings, $"  [CLEAN] Removed {toDestroy.Count} existing clones from {root.name}");
            }
        }

        // ======================================================================
        //  Phase 13.1: Final Terrain Child Cleanup
        // ======================================================================

        /// <summary>
        /// Phase 13.1: 최종 Terrain root 하위의 base terrain clone 내부에서
        /// decorative child(tree/vegetation/grass 등)를 재귀적으로 찾아 분리한다.
        /// 분리된 child는 TerrainDecorativeCandidates 또는 TerrainDisabledCandidates로 이동한다.
        /// 추출된 총 child 개수를 반환한다.
        /// </summary>
        private static int CleanupFinalTerrainChildren(
            DeepLightMapAutoBuilderSettingsSO settings,
            GameObject generatedRoot,
            GameObject terrainRoot,
            GameObject decorativeRoot,
            GameObject disabledRoot)
        {
            if (terrainRoot == null || decorativeRoot == null || disabledRoot == null)
            {
                Debug.LogWarning("[MapAutoBuilder] [WARN] Cannot cleanup final terrain children: one or more roots are null.");
                return 0;
            }

            int totalExtracted = 0;
            int decorativeCount = 0;
            int disabledCount = 0;

            // 검사한 base terrain clone 개수
            int baseTerrainCloneCount = 0;

            // Terrain root 하위의 모든 _GeneratedClone을 대상으로 재귀 탐색
            foreach (Transform baseTerrain in terrainRoot.transform)
            {
                if (!baseTerrain.name.EndsWith("_GeneratedClone")) continue;
                baseTerrainCloneCount++;

                // base terrain 자신은 분리하지 않고, 내부 child만 탐색
                var result = ExtractDecorativeChildrenFromFinalTerrain(
                    baseTerrain.gameObject, decorativeRoot, disabledRoot, settings);

                totalExtracted += result.decorativeCount + result.disabledCount;
                decorativeCount += result.decorativeCount;
                disabledCount += result.disabledCount;
            }

            // 항상 로그 출력 (verbose 무관)
            Debug.Log($"[MapAutoBuilder] Phase 13.1: Inspected {baseTerrainCloneCount} base terrain clone(s). " +
                $"Extracted {totalExtracted} decorative children (Decorative: {decorativeCount}, Disabled: {disabledCount}).");

            // 추출된 대표 오브젝트 경로 최대 10개 출력
            if (totalExtracted > 0)
            {
                var samplePaths = new List<string>();
                foreach (Transform baseTerrain in terrainRoot.transform)
                {
                    if (!baseTerrain.name.EndsWith("_GeneratedClone")) continue;
                    Transform[] allChildren = baseTerrain.GetComponentsInChildren<Transform>(true);
                    foreach (Transform child in allChildren)
                    {
                        if (child == baseTerrain) continue;
                        // decorativeRoot 또는 disabledRoot 하위로 이동되었는지 확인
                        if (child.parent == decorativeRoot.transform || child.parent == disabledRoot.transform)
                        {
                            samplePaths.Add($"{child.parent.name}/{child.name}");
                            if (samplePaths.Count >= 10) break;
                        }
                    }
                    if (samplePaths.Count >= 10) break;
                }

                if (samplePaths.Count > 0)
                {
                    Debug.Log("[MapAutoBuilder] Phase 13.1: Extracted objects (sample):");
                    foreach (string path in samplePaths)
                    {
                        Debug.Log($"  - {path}");
                    }
                }
            }

            // DecorativeCandidates/DisabledCandidates 하위 후보 개수 로그
            int decorativeCandidateCount = 0;
            foreach (Transform child in decorativeRoot.transform)
            {
                decorativeCandidateCount++;
            }
            int disabledCandidateCount = 0;
            foreach (Transform child in disabledRoot.transform)
            {
                disabledCandidateCount++;
            }
            Debug.Log($"[MapAutoBuilder] Phase 13.1: TerrainDecorativeCandidates has {decorativeCandidateCount} candidate(s). " +
                $"TerrainDisabledCandidates has {disabledCandidateCount} candidate(s).");

            return totalExtracted;
        }

        /// <summary>
        /// Phase 13.1: 단일 base terrain clone 내부의 child를 재귀적으로 탐색하여
        /// decorative 후보를 분리한다.
        /// root 자신은 분리하지 않는다.
        /// </summary>
        private static (int decorativeCount, int disabledCount) ExtractDecorativeChildrenFromFinalTerrain(
            GameObject baseTerrainClone,
            GameObject decorativeRoot,
            GameObject disabledRoot,
            DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (baseTerrainClone == null) return (0, 0);

            int decorativeCount = 0;
            int disabledCount = 0;

            // 재귀적으로 모든 child Transform 수집 (root 자신 제외)
            Transform[] allChildren = baseTerrainClone.GetComponentsInChildren<Transform>(true);

            // root 자신은 제외
            var childrenToProcess = new List<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child == baseTerrainClone.transform) continue; // root 자신 제외
                childrenToProcess.Add(child);
            }

            // 깊이 우선으로 처리하기 위해 depth 기준 내림차순 정렬
            // (깊은 child부터 처리하면 부모가 분리될 때 child가 중복 처리되지 않음)
            childrenToProcess.Sort((a, b) => GetDepth(b).CompareTo(GetDepth(a)));

            // 이미 decorative parent 하위에 포함된 Transform을 추적하여 중복 이동 방지
            var alreadyMovedParents = new HashSet<Transform>();

            foreach (Transform child in childrenToProcess)
            {
                // 이미 분리되었거나 부모가 baseTerrainClone 체인이 아니면 스킵
                if (child == null || child.parent == null) continue;
                if (!IsChildOfTransform(child, baseTerrainClone.transform)) continue;

                // 이미 decorative parent 하위에 포함된 child는 중복 이동하지 않음
                if (IsChildOfAnyMovedParent(child, alreadyMovedParents)) continue;

                string lowerName = child.name.ToLowerInvariant();

                // BaseTerrainChildKeywords에 포함되면 base terrain에 남김
                if (MatchesAnyKeyword(lowerName, BaseTerrainChildKeywords))
                {
                    continue;
                }

                // DecorativeChildKeywords에 포함되면 분리
                if (MatchesAnyKeyword(lowerName, DecorativeChildKeywords))
                {
                    // world transform 저장
                    Vector3 worldPos = child.position;
                    Quaternion worldRot = child.rotation;
                    Vector3 worldScale = child.lossyScale;

                    // DecorativeCandidates root로 이동
                    child.SetParent(decorativeRoot.transform);
                    child.SetPositionAndRotation(worldPos, worldRot);
                    child.localScale = worldScale;

                    // Renderer/Collider 비활성화 + GameObject inactive
                    DisableCandidateObject(child.gameObject);

                    // 이 parent가 이동되었음을 기록 (하위 child 중복 방지)
                    alreadyMovedParents.Add(child);

                    decorativeCount++;
                    LogLayoutVerbose(settings, $"  [EXTRACT-DECORATIVE] {child.name} -> {decorativeRoot.name}");
                }
                else
                {
                    // 분류되지 않은 child는 DisabledCandidates로 이동
                    Vector3 worldPos = child.position;
                    Quaternion worldRot = child.rotation;
                    Vector3 worldScale = child.lossyScale;

                    child.SetParent(disabledRoot.transform);
                    child.SetPositionAndRotation(worldPos, worldRot);
                    child.localScale = worldScale;

                    // Renderer/Collider 비활성화 + GameObject inactive
                    DisableCandidateObject(child.gameObject);

                    // 이 parent가 이동되었음을 기록 (하위 child 중복 방지)
                    alreadyMovedParents.Add(child);

                    disabledCount++;
                    LogLayoutVerbose(settings, $"  [EXTRACT-DISABLED] {child.name} -> {disabledRoot.name}");
                }
            }

            return (decorativeCount, disabledCount);
        }

        /// <summary>
        /// Phase 13.1: Transform이 이미 이동된 parent 하위에 포함되어 있는지 확인한다.
        /// </summary>
        private static bool IsChildOfAnyMovedParent(Transform target, HashSet<Transform> movedParents)
        {
            Transform current = target.parent;
            while (current != null)
            {
                if (movedParents.Contains(current))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Transform이 특정 Transform의 자식인지 재귀적으로 확인한다.
        /// </summary>
        private static bool IsChildOfTransform(Transform target, Transform potentialParent)
        {
            Transform current = target.parent;
            while (current != null)
            {
                if (current == potentialParent) return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Transform의 Hierarchy depth를 반환한다. (root가 0)
        /// </summary>
        private static int GetDepth(Transform t)
        {
            int depth = 0;
            Transform current = t;
            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        /// <summary>
        /// settings.LogTerrainLayoutVerbose가 true일 때만 로그를 출력한다.
        /// settings가 null이면 항상 출력한다.
        /// </summary>
        private static void LogLayoutVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings == null || settings.LogTerrainLayoutVerbose)
            {
                Debug.Log(message);
            }
        }


        // ======================================================================
        //  Phase 13.2: SW3 Terrain Source Material Binding + Collision Policy
        // ======================================================================

        /// <summary>
        /// Phase 13.2: 이름 기반으로 material을 AssetDatabase에서 검색한다.
        /// 정확히 일치하는 material이 없으면 null을 반환한다.
        /// </summary>
        private static Material FindMaterialByExactName(string materialName)
        {
            string[] guids = AssetDatabase.FindAssets($"{materialName} t:Material");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.name == materialName)
                {
                    Debug.Log($"[MapAutoBuilder] Phase 13.2: Found material '{materialName}' at: {path}");
                    return mat;
                }
            }

            Debug.LogWarning($"[MapAutoBuilder] [WARN] Phase 13.2: Material '{materialName}' not found in AssetDatabase.");
            return null;
        }

        /// <summary>
        /// Phase 13.2: GeneratedWorldRoot/Terrain 하위 모든 Renderer를 검사하여
        /// SW3_BackdropTerrain 또는 SW3_BackdropTerrain_FagaceaeBillboard material을 자동 할당한다.
        /// 원본 material asset의 property는 수정하지 않는다.
        /// 할당된 renderer 수를 반환한다.
        /// </summary>
        private static int ApplySw3TerrainSourceMaterials(GameObject generatedRoot)
        {
            if (generatedRoot == null) return 0;

            Transform terrainRoot = generatedRoot.transform.Find("Terrain");
            if (terrainRoot == null)
            {
                Debug.LogWarning("[MapAutoBuilder] [WARN] Phase 13.2: Terrain root not found under GeneratedWorldRoot.");
                return 0;
            }

            // Material 검색
            Material backdropTerrainMat = FindMaterialByExactName("SW3_BackdropTerrain");
            Material billboardMat = FindMaterialByExactName("SW3_BackdropTerrain_FagaceaeBillboard");

            int terrainMatCount = 0;
            int billboardMatCount = 0;

            // Terrain root 하위 모든 Renderer 검색
            Renderer[] allRenderers = terrainRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in allRenderers)
            {
                if (renderer == null) continue;

                string lowerName = renderer.gameObject.name.ToLowerInvariant();
                string lowerParentName = renderer.transform.parent != null
                    ? renderer.transform.parent.name.ToLowerInvariant()
                    : "";

                // tree/billboard 계열 우선 검사
                bool isTreeOrBillboard = lowerName.Contains("backgroundtrees") ||
                    lowerName.Contains("tree") ||
                    lowerName.Contains("trees") ||
                    lowerName.Contains("billboard") ||
                    lowerName.Contains("fagaceae") ||
                    lowerParentName.Contains("backgroundtrees") ||
                    lowerParentName.Contains("tree") ||
                    lowerParentName.Contains("trees") ||
                    lowerParentName.Contains("billboard") ||
                    lowerParentName.Contains("fagaceae");

                if (isTreeOrBillboard)
                {
                    if (billboardMat != null)
                    {
                        renderer.sharedMaterial = billboardMat;
                        billboardMatCount++;
                        LogLayoutVerbose(null, $"  [MAT-BILLBOARD] {renderer.gameObject.name} -> {billboardMat.name}");
                    }
                    continue;
                }

                // terrain 계열 검사 (tree/billboard가 아닌 경우)
                bool isTerrain = lowerName.Contains("sw3_backgroundterrain") ||
                    lowerName.Contains("backgroundterrain") ||
                    lowerName.Contains("backdropterrain") ||
                    lowerName.Contains("terrain") ||
                    lowerParentName.Contains("sw3_backgroundterrain") ||
                    lowerParentName.Contains("backgroundterrain") ||
                    lowerParentName.Contains("backdropterrain") ||
                    lowerParentName.Contains("terrain");

                if (isTerrain)
                {
                    if (backdropTerrainMat != null)
                    {
                        renderer.sharedMaterial = backdropTerrainMat;
                        terrainMatCount++;
                        LogLayoutVerbose(null, $"  [MAT-TERRAIN] {renderer.gameObject.name} -> {backdropTerrainMat.name}");
                    }
                    continue;
                }
            }

            Debug.Log($"[MapAutoBuilder] Phase 13.2: SW3 material binding complete. " +
                $"Terrain material assigned: {terrainMatCount}, " +
                $"Tree/Billboard material assigned: {billboardMatCount}.");

            return terrainMatCount + billboardMatCount;
        }

        /// <summary>
        /// Phase 13.2: Terrain root 하위의 Collider 정책을 적용한다.
        /// BackgroundTrees/tree/billboard/vegetation/grass 계열 Collider는 비활성화하고,
        /// base terrain mesh에는 잠수정 충돌용 MeshCollider 1개를 유지/생성한다.
        /// </summary>
        private static void ApplyTerrainCollisionPolicy(GameObject generatedRoot)
        {
            if (generatedRoot == null) return;

            Transform terrainRoot = generatedRoot.transform.Find("Terrain");
            if (terrainRoot == null) return;

            int disabledColliderCount = 0;
            int meshColliderCount = 0;
            GameObject baseTerrainMeshObject = null;

            // Terrain root 하위 모든 Collider 검사
            Collider[] allColliders = terrainRoot.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in allColliders)
            {
                if (collider == null) continue;

                string lowerName = collider.gameObject.name.ToLowerInvariant();
                string lowerParentName = collider.transform.parent != null
                    ? collider.transform.parent.name.ToLowerInvariant()
                    : "";

                // BackgroundTrees/tree/billboard/vegetation/grass 계열 Collider 비활성화
                bool isDecorative = lowerName.Contains("backgroundtrees") ||
                    lowerName.Contains("tree") ||
                    lowerName.Contains("trees") ||
                    lowerName.Contains("billboard") ||
                    lowerName.Contains("vegetation") ||
                    lowerName.Contains("grass") ||
                    lowerName.Contains("coral") ||
                    lowerName.Contains("kelp") ||
                    lowerName.Contains("particle") ||
                    lowerName.Contains("random") ||
                    lowerParentName.Contains("backgroundtrees") ||
                    lowerParentName.Contains("tree") ||
                    lowerParentName.Contains("trees") ||
                    lowerParentName.Contains("billboard") ||
                    lowerParentName.Contains("vegetation") ||
                    lowerParentName.Contains("grass") ||
                    lowerParentName.Contains("coral") ||
                    lowerParentName.Contains("kelp") ||
                    lowerParentName.Contains("particle") ||
                    lowerParentName.Contains("random");

                if (isDecorative)
                {
                    if (collider.enabled)
                    {
                        collider.enabled = false;
                        disabledColliderCount++;
                        LogLayoutVerbose(null, $"  [COLLIDER-DISABLE] {collider.gameObject.name}");
                    }
                    continue;
                }

                // base terrain mesh로 판단되는 MeshCollider 카운트
                bool isBaseTerrain = lowerName.Contains("sw3_backgroundterrain") ||
                    lowerName.Contains("backgroundterrain") ||
                    lowerName.Contains("backdropterrain") ||
                    lowerName.Contains("terrain") ||
                    lowerParentName.Contains("sw3_backgroundterrain") ||
                    lowerParentName.Contains("backgroundterrain") ||
                    lowerParentName.Contains("backdropterrain") ||
                    lowerParentName.Contains("terrain");

                if (isBaseTerrain && collider is MeshCollider meshCol)
                {
                    meshColliderCount++;
                    if (baseTerrainMeshObject == null)
                    {
                        baseTerrainMeshObject = collider.gameObject;
                    }

                    // MeshCollider 설정 보장
                    meshCol.convex = false;
                    meshCol.isTrigger = false;

                    // sharedMesh가 없으면 MeshFilter에서 가져오기
                    if (meshCol.sharedMesh == null)
                    {
                        MeshFilter mf = collider.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            meshCol.sharedMesh = mf.sharedMesh;
                        }
                    }
                }
            }

            // MeshCollider가 없으면 base terrain mesh object에 생성
            if (meshColliderCount == 0)
            {
                // Terrain root 하위에서 base terrain mesh object 찾기
                foreach (Transform child in terrainRoot)
                {
                    if (!child.name.EndsWith("_GeneratedClone")) continue;

                    MeshFilter[] meshFilters = child.GetComponentsInChildren<MeshFilter>(true);
                    foreach (MeshFilter mf in meshFilters)
                    {
                        if (mf.sharedMesh == null) continue;
                        string lowerName = mf.gameObject.name.ToLowerInvariant();
                        if (lowerName.Contains("terrain") || lowerName.Contains("backgroundterrain"))
                        {
                            MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
                            if (mc == null)
                            {
                                mc = mf.gameObject.AddComponent<MeshCollider>();
                                Undo.RegisterCreatedObjectUndo(mc, "Add MeshCollider");
                            }
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = false;
                            mc.isTrigger = false;
                            mc.enabled = true;
                            meshColliderCount++;
                            baseTerrainMeshObject = mf.gameObject;
                            Debug.Log($"[MapAutoBuilder] Phase 13.2: Created MeshCollider on {mf.gameObject.name} for underwater collision.");
                            break;
                        }
                    }
                    if (meshColliderCount > 0) break;
                }
            }

            // MeshCollider가 2개 이상이면 경고 출력하고 1개만 남기고 비활성화
            if (meshColliderCount >= 2)
            {
                Debug.LogWarning($"[MapAutoBuilder] [WARN] Phase 13.2: Found {meshColliderCount} MeshColliders under Terrain root. " +
                    $"Keeping 1 on '{baseTerrainMeshObject?.name ?? "unknown"}', disabling others.");

                int keptCount = 0;
                foreach (Collider collider in allColliders)
                {
                    if (collider == null) continue;
                    if (!(collider is MeshCollider)) continue;
                    if (collider.gameObject == baseTerrainMeshObject)
                    {
                        keptCount++;
                        continue;
                    }
                    if (keptCount >= 1)
                    {
                        collider.enabled = false;
                        disabledColliderCount++;
                    }
                    else
                    {
                        keptCount++;
                    }
                }
            }

            Debug.Log($"[MapAutoBuilder] Phase 13.2: Collision policy applied. " +
                $"Disabled colliders: {disabledColliderCount}, " +
                $"Final MeshCollider count: {Mathf.Max(meshColliderCount, 1)}.");
        }

        /// <summary>
        /// Phase 13.2: Terrain root 하위의 BackgroundTrees/tree/billboard 계열 Renderer가
        /// SW3_BackdropTerrain_FagaceaeBillboard material을 사용 중인지 확인한다.
        /// </summary>
        private static bool CheckTreeBillboardMaterials(Transform terrainRoot)
        {
            if (terrainRoot == null) return true;

            Material expectedMat = FindMaterialByExactName("SW3_BackdropTerrain_FagaceaeBillboard");
            if (expectedMat == null) return true; // material이 없으면 검사 불가 -> pass

            Renderer[] renderers = terrainRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                string lowerName = r.gameObject.name.ToLowerInvariant();
                if (!lowerName.Contains("backgroundtrees") &&
                    !lowerName.Contains("tree") &&
                    !lowerName.Contains("trees") &&
                    !lowerName.Contains("billboard") &&
                    !lowerName.Contains("fagaceae"))
                {
                    continue;
                }

                // tree/billboard 계열 renderer의 material 검사
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat != null && mat.name != expectedMat.name)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Phase 13.2: Terrain root 하위의 SW3_BackgroundTerrain 계열 Renderer가
        /// SW3_BackdropTerrain material을 사용 중인지 확인한다.
        /// </summary>
        private static bool CheckTerrainBaseMaterials(Transform terrainRoot)
        {
            if (terrainRoot == null) return true;

            Material expectedMat = FindMaterialByExactName("SW3_BackdropTerrain");
            if (expectedMat == null) return true;

            Renderer[] renderers = terrainRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                string lowerName = r.gameObject.name.ToLowerInvariant();
                if (!lowerName.Contains("sw3_backgroundterrain") &&
                    !lowerName.Contains("backgroundterrain") &&
                    !lowerName.Contains("backdropterrain"))
                {
                    continue;
                }

                // tree/billboard 계열은 제외
                if (lowerName.Contains("tree") || lowerName.Contains("billboard") || lowerName.Contains("fagaceae"))
                {
                    continue;
                }

                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat != null && mat.name != expectedMat.name)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Phase 13.2: BackgroundTrees/tree/billboard 계열에 enabled Collider가 없는지 확인한다.
        /// </summary>
        private static bool CheckNoEnabledColliderOnDecorative(Transform terrainRoot)
        {
            if (terrainRoot == null) return true;

            Collider[] colliders = terrainRoot.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                if (c == null || !c.enabled) continue;
                string lowerName = c.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("backgroundtrees") ||
                    lowerName.Contains("tree") ||
                    lowerName.Contains("trees") ||
                    lowerName.Contains("billboard") ||
                    lowerName.Contains("vegetation") ||
                    lowerName.Contains("grass"))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Phase 13.2: Base terrain에 MeshCollider가 최소 1개 있는지 확인한다.
        /// </summary>
        private static bool CheckHasMeshColliderOnBaseTerrain(Transform terrainRoot)
        {
            if (terrainRoot == null) return false;

            MeshCollider[] meshColliders = terrainRoot.GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider mc in meshColliders)
            {
                if (mc == null || !mc.enabled) continue;
                string lowerName = mc.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("terrain") || lowerName.Contains("backgroundterrain"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Phase 13.2: Terrain root 하위 MeshCollider 개수를 반환한다.
        /// </summary>
        private static int CountMeshColliders(Transform terrainRoot)
        {
            if (terrainRoot == null) return 0;
            MeshCollider[] meshColliders = terrainRoot.GetComponentsInChildren<MeshCollider>(true);
            int count = 0;
            foreach (MeshCollider mc in meshColliders)
            {
                if (mc != null && mc.enabled) count++;
            }
            return count;
        }

        /// <summary>
        /// Phase 13.2: Terrain root 하위에 Rigidbody가 있는지 확인한다.
        /// </summary>
        private static bool CheckNoRigidbodyInTerrain(Transform terrainRoot)
        {
            if (terrainRoot == null) return true;
            Rigidbody[] rigidbodies = terrainRoot.GetComponentsInChildren<Rigidbody>(true);
            return rigidbodies.Length == 0;
        }

        /// <summary>
        /// Phase 13.2: Terrain root 하위의 missing material 개수를 센다.
        /// </summary>
        private static int CountMissingMaterialsInTerrain(Transform terrainRoot)
        {
            if (terrainRoot == null) return 0;
            return CountMissingMaterials(terrainRoot.gameObject);
        }

        /// <summary>
        /// Phase 13.2: Terrain root 하위의 pink material risk 개수를 센다.
        /// </summary>
        private static int CountPinkMaterialRiskInTerrain(Transform terrainRoot)
        {
            if (terrainRoot == null) return 0;
            return CountPinkMaterialRisk(terrainRoot.gameObject);
        }
    }
}

