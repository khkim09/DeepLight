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
    /// DeepLight Map Auto Builder의 핵심 로직을 담은 정적 클래스.
    /// Scene 맵 자동 생성/검증/삭제를 수행하며, 모든 Hierarchy 조작은
    /// GeneratedWorldRoot 하위로 제한된다.
    ///
    /// Phase 3: 기본 루트 구조 생성 (GlobalWater, ZoneRoots, RuntimeEnvironment, Debug)
    /// Phase 4: ZoneRoot_A1~J10, ZoneTrigger, Environment 루트 생성
    /// Phase 5: Zone별 UnderwaterArea 복제본, Seafloor placeholder 생성
    /// Phase 7: Runtime Binding (GeneratedWorldZoneNode + Registry + Tracker)
    /// </summary>
    public static class DeepLightMapAutoBuilder
    {
        // ===== 보호된 오브젝트 이름 목록 =====
        private static readonly HashSet<string> ProtectedRootNames = new HashSet<string>
        {
            "MapSettings",
            "_WorldMap_Manual",
            "WorldMapHUDCanvas",
            "WorldMapMiniGridCanvas",
            "UIRoot",
            "RuntimeControllers",
            "Submarine",
            "CameraSystem",
            "DeepLightMapAutoBuilderContext"
        };

        // ===== Public API (SettingsSO + SceneContext) =====

        /// <summary>
        /// Settings + SceneContext의 유효성을 검사한다.
        /// 심각한 오류(settings null, worldMapConfig null, scenarioPreset null, root name empty)면 false 반환.
        /// 경고만 있는 경우 true 반환.
        /// </summary>
        public static bool ValidateSettings(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate.");
                return false;
            }

            bool hasCriticalError = false;
            bool hasWarning = false;
            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Validate Settings + Context =====");

            // 1. WorldMapConfig null 검사
            if (settings.WorldMapConfig == null)
            {
                log.AppendLine("  [CRITICAL] WorldMapConfig is null! Assign in SettingsSO Inspector.");
                hasCriticalError = true;
            }
            else
            {
                log.AppendLine($"  [OK] WorldMapConfig: {settings.WorldMapConfig.name}");
                log.AppendLine($"       Grid: {settings.WorldMapConfig.GridWidth}x{settings.WorldMapConfig.GridHeight}, ZoneSize: {settings.WorldMapConfig.ZoneSize}");
            }

            // 2. ScenarioPreset null 검사
            if (settings.ScenarioPreset == null)
            {
                log.AppendLine("  [CRITICAL] ScenarioPreset is null! Assign in SettingsSO Inspector.");
                hasCriticalError = true;
            }
            else
            {
                log.AppendLine($"  [OK] ScenarioPreset: {settings.ScenarioPreset.name}");
                int ruleCount = settings.ScenarioPreset.ZoneRules != null ? settings.ScenarioPreset.ZoneRules.Count : 0;
                log.AppendLine($"       Zone Rules: {ruleCount}개");
                if (ruleCount < 7)
                {
                    log.AppendLine($"  [WARNING] Zone Rules count ({ruleCount}) is less than expected (7).");
                    hasWarning = true;
                }
            }

            // 3. Root name empty 검사
            if (string.IsNullOrEmpty(settings.GeneratedRootName))
            {
                log.AppendLine("  [CRITICAL] GeneratedRootName is empty!");
                hasCriticalError = true;
            }
            else
            {
                log.AppendLine($"  [OK] GeneratedRootName: {settings.GeneratedRootName}");
            }

            if (string.IsNullOrEmpty(settings.GlobalWaterRootName))
            {
                log.AppendLine("  [CRITICAL] GlobalWaterRootName is empty!");
                hasCriticalError = true;
            }
            else
            {
                log.AppendLine($"  [OK] GlobalWaterRootName: {settings.GlobalWaterRootName}");
            }

            if (string.IsNullOrEmpty(settings.ZoneRootParentName))
            {
                log.AppendLine("  [CRITICAL] ZoneRootParentName is empty!");
                hasCriticalError = true;
            }
            else
            {
                log.AppendLine($"  [OK] ZoneRootParentName: {settings.ZoneRootParentName}");
            }

            if (string.IsNullOrEmpty(settings.RuntimeEnvironmentRootName))
            {
                log.AppendLine("  [CRITICAL] RuntimeEnvironmentRootName is empty!");
                hasCriticalError = true;
            }
            else
            {
                log.AppendLine($"  [OK] RuntimeEnvironmentRootName: {settings.RuntimeEnvironmentRootName}");
            }

            // 4. Scene Context 검사
            if (context == null)
            {
                log.AppendLine("  [WARNING] SceneContext is null. Generation will still work but no template references.");
                hasWarning = true;
            }
            else
            {
                log.AppendLine($"  [OK] SceneContext: {context.name}");

                if (context.MapSettingsTemplateRoot == null)
                {
                    log.AppendLine("  [WARNING] MapSettingsTemplateRoot is null. No template reference.");
                    hasWarning = true;
                }
                else
                {
                    log.AppendLine($"  [OK] MapSettingsTemplateRoot: {context.MapSettingsTemplateRoot.name}");
                }

                if (context.GlobalWaterTemplate == null)
                {
                    if (settings.GlobalWaterPrefab != null)
                    {
                        log.AppendLine($"  [OK] GlobalWaterTemplate: (using SettingsSO Prefab fallback: {settings.GlobalWaterPrefab.name})");
                    }
                    else
                    {
                        log.AppendLine("  [WARNING] GlobalWaterTemplate is null (SceneContext + SettingsSO Prefab). Global water clone will be skipped.");
                        hasWarning = true;
                    }
                }
                else
                {
                    log.AppendLine($"  [OK] GlobalWaterTemplate: {context.GlobalWaterTemplate.name} (SceneContext)");
                }

                if (context.UnderwaterAreaTemplate == null)
                {
                    log.AppendLine("  [INFO] UnderwaterAreaTemplate is null. Will be used in Phase 4+.");
                }

                if (context.BubbleParticleTemplate == null)
                {
                    log.AppendLine("  [INFO] BubbleParticleTemplate is null. Will be used in Phase 4+.");
                }

                if (context.DynamicEffectTemplate == null)
                {
                    log.AppendLine("  [INFO] DynamicEffectTemplate is null. Will be used in Phase 4+.");
                }

                // GeneratedRootOverride 검사
                if (context.GeneratedRootOverride != null)
                {
                    if (IsProtectedSceneObject(context.GeneratedRootOverride))
                    {
                        log.AppendLine($"  [CRITICAL] GeneratedRootOverride '{context.GeneratedRootOverride.name}' is a protected object! Cannot use.");
                        hasCriticalError = true;
                    }
                    else
                    {
                        log.AppendLine($"  [OK] GeneratedRootOverride: {context.GeneratedRootOverride.name}");
                    }
                }
                else
                {
                    log.AppendLine("  [INFO] GeneratedRootOverride is null. Will use name-based lookup.");
                }
            }

            // 결과 출력
            if (hasCriticalError)
            {
                log.AppendLine($"\n=> Validation FAILED (critical errors). Fix critical errors before generating.");
            }
            else if (hasWarning)
            {
                log.AppendLine("\n=> Validation PASSED with warnings. Generation is possible.");
            }
            else
            {
                log.AppendLine("\n=> Validation PASSED. All checks OK.");
            }

            Debug.Log(log.ToString());
            return !hasCriticalError;
        }

        /// <summary>
        /// Dry Run Preview: 실제 Scene 오브젝트를 생성하지 않고 Console에 생성 예정 구조를 출력한다.
        /// </summary>
        public static void DryRunPreview(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot run dry run preview.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Map Auto Builder: Dry Run Preview =====");
            log.AppendLine("(No Scene objects are created during Dry Run)");
            log.AppendLine("");

            // Generated root name
            string rootName = settings.GeneratedRootName;
            log.AppendLine($"Generated Root Name: {rootName}");

            // WorldMapConfig 정보
            if (settings.WorldMapConfig != null)
            {
                var config = settings.WorldMapConfig;
                log.AppendLine($"WorldMapConfig: {config.name}");
                log.AppendLine($"  Grid Width: {config.GridWidth}");
                log.AppendLine($"  Grid Height: {config.GridHeight}");
                log.AppendLine($"  Zone Size: {config.ZoneSize}");
                log.AppendLine($"  World Min X: {config.WorldMinX}");
                log.AppendLine($"  World Min Z: {config.WorldMinZ}");
                log.AppendLine($"  World Max X: {config.WorldMaxX}");
                log.AppendLine($"  World Max Z: {config.WorldMaxZ}");
            }
            else
            {
                log.AppendLine("WorldMapConfig: [NULL]");
            }

            // ScenarioPreset 정보
            if (settings.ScenarioPreset != null)
            {
                int ruleCount = settings.ScenarioPreset.ZoneRules != null ? settings.ScenarioPreset.ZoneRules.Count : 0;
                log.AppendLine($"ScenarioPreset: {settings.ScenarioPreset.name} ({ruleCount} Zone Rules)");
            }
            else
            {
                log.AppendLine("ScenarioPreset: [NULL]");
            }

            // Scene Context 정보
            if (context != null)
            {
                log.AppendLine($"SceneContext: {context.name}");
                log.AppendLine($"  MapSettingsTemplate: {(context.MapSettingsTemplateRoot != null ? context.MapSettingsTemplateRoot.name : "NULL")}");
                log.AppendLine($"  GlobalWaterTemplate: {(context.GlobalWaterTemplate != null ? context.GlobalWaterTemplate.name : "NULL")}");
                log.AppendLine($"  Root Override: {(context.GeneratedRootOverride != null ? context.GeneratedRootOverride.name : "NULL (name-based)")}");
            }
            else
            {
                log.AppendLine("SceneContext: NULL (template clone will be skipped)");
            }

            // 생성 예정 기본 구조
            log.AppendLine("");
            log.AppendLine("=== 생성 예정 기본 구조 (Phase 3) ===");
            log.AppendLine($"{rootName}/");
            log.AppendLine($"  ├── {settings.GlobalWaterRootName}/");
            string waterTemplateName = (context != null && context.GlobalWaterTemplate != null) ? context.GlobalWaterTemplate.name : "SKIP - no template";
            log.AppendLine($"  │     └── (GlobalWater clone: {waterTemplateName})");
            log.AppendLine($"  ├── {settings.ZoneRootParentName}/");
            log.AppendLine($"  │     └── (Phase 4: ZoneRoot_A1~J10 will be created here)");
            log.AppendLine($"  ├── {settings.RuntimeEnvironmentRootName}/");
            log.AppendLine($"  │     ├── {settings.UnderwaterAreasRootName}/");
            log.AppendLine($"  │     ├── {settings.DynamicEffectsRootName}/");
            log.AppendLine($"  │     └── {settings.SharedParticlesRootName}/");
            log.AppendLine($"  └── {settings.DebugRootName}/");

            // Phase 3 제한 사항
            log.AppendLine("");
            log.AppendLine("=== Phase 3 제한 사항 ===");
            log.AppendLine("[INFO] 이번 Phase에서는 ZoneRoot_A1~J10을 생성하지 않습니다.");
            log.AppendLine("[INFO] ZoneRoot 생성은 Phase 4에서 구현 예정입니다.");
            log.AppendLine("[INFO] UnderwaterArea/Particle/DynamicEffect Zone별 복제는 Phase 4+에서 구현 예정입니다.");

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Full Scenario Map을 생성한다.
        /// Phase 3: 기본 루트 구조 생성 (GlobalWater, ZoneRoots, RuntimeEnvironment, Debug).
        /// Phase 4: settings.CreateZoneRoots가 true이면 ZoneRoot_A1~J10, ZoneTrigger, Environment 루트를 생성한다.
        /// </summary>
        public static void GenerateFullScenarioMap(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot generate.");
                return;
            }

            // Validate 먼저 실행
            if (!ValidateSettings(settings, context))
            {
                Debug.LogError("[MapAutoBuilder] Validation failed. Aborting generation.");
                return;
            }

            LogIfVerbose(settings, "===== Map Auto Builder: Generate Full Scenario Map (Phase 3 + Phase 4) =====");

            // 1. Generated Root 획득 또는 생성
            GameObject generatedRoot = GetOrCreateSceneRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] Failed to get or create GeneratedWorldRoot. Aborting.");
                return;
            }

            // 2. 기본 루트 구조 생성
            CreateBaseRootStructure(settings, generatedRoot);

            // 3. GlobalWater 복제 (SceneContext의 템플릿 사용)
            CloneGlobalWaterIfNeeded(settings, context, generatedRoot);

            // 4. Phase 4: ZoneRoot 생성 (settings.CreateZoneRoots가 true일 때만)
            if (settings.CreateZoneRoots)
            {
                RebuildZoneRootsOnly(settings, context);
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createZoneRoots is false. Skipping ZoneRoot generation.");
            }

            // 5. Phase 5 + 5.5: Zone Environment 생성 및 Underwater volume correction (settings.CreateZoneRoots가 true일 때만)
            if (settings.CreateZoneRoots)
            {
                // ZoneRoots 부모 찾기
                Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
                if (zoneRootsTransform != null)
                {
                    // Phase 5: UnderwaterArea + Seafloor 생성
                    DeepLightMapEnvironmentGenerationUtility.RebuildAllZoneEnvironments(settings, zoneRootsTransform.gameObject);
                    // Phase 5.5: Underwater volume correction은 RebuildAllZoneEnvironments 내부에서 자동 호출됨
                }
                else
                {
                    Debug.LogWarning($"[MapAutoBuilder] '{settings.ZoneRootParentName}' not found. Skipping environment generation.");
                }
            }

            // 6. 검증: Zone Setup + Environment Setup

            if (settings.CreateZoneRoots)
            {
                DeepLightMapZoneGenerationUtility.ValidateZoneSetup(settings);
                DeepLightMapEnvironmentGenerationUtility.ValidateEnvironmentSetup(settings);
            }

            // 7. Phase 7: Runtime Binding 생성 (settings.CreateRuntimeBindings가 true일 때만)
            if (settings.CreateRuntimeBindings)
            {
                RebuildRuntimeBindings(settings, context);
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createRuntimeBindings is false. Skipping runtime binding generation.");
            }

            // 8. Phase 6: ZoneResolver + DepthSampling 검증
            if (settings.CreateZoneRoots)
            {
                Debug.Log("[MapAutoBuilder] === Phase 6: Validating Zone Resolver ===");
                ValidateZoneResolver(settings, context);
                Debug.Log("[MapAutoBuilder] === Phase 6: Validating Depth Sampling ===");
                ValidateDepthSampling(settings, context);
            }

            // 9. Phase 7: Runtime Binding 검증
            if (settings.CreateRuntimeBindings)
            {
                Debug.Log("[MapAutoBuilder] === Phase 7: Validating Runtime Bindings ===");
                ValidateRuntimeBindings(settings, context);
            }

            // 10. Phase 8: Visual Profile Binding 생성 + 검증 (settings.CreateVisualRuntimeController가 true일 때만)
            if (settings.CreateVisualRuntimeController)
            {
                Debug.Log("[MapAutoBuilder] === Phase 8: Rebuilding Visual Runtime Binding ===");
                RebuildVisualRuntimeBinding(settings, context);
                Debug.Log("[MapAutoBuilder] === Phase 8: Validating Visual Profiles ===");
                ValidateVisualRuntimeBinding(settings, context);
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createVisualRuntimeController is false. Skipping visual runtime binding.");
            }

            // 11. Phase 9: Visual Adapter Binding 생성 + 검증 (settings.CreateVisualAdapterBinding이 true일 때만)
            if (settings.CreateVisualAdapterBinding)
            {
                Debug.Log("[MapAutoBuilder] === Phase 9: Rebuilding Visual Adapter Binding ===");
                RebuildVisualAdapterBinding(settings, context);
                Debug.Log("[MapAutoBuilder] === Phase 9: Validating Visual Adapter Binding ===");
                ValidateVisualAdapterBinding(settings, context);
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createVisualAdapterBinding is false. Skipping visual adapter binding.");
            }

            // 12. Phase 10: Visual Adapter Runtime Flow Validation (settings.CreateVisualAdapterBinding이 true일 때만)
            if (settings.CreateVisualAdapterBinding)
            {
                Debug.Log("[MapAutoBuilder] === Phase 10: Validating Visual Adapter Runtime Flow ===");
                ValidateVisualAdapterRuntimeFlow(settings, context);
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createVisualAdapterBinding is false. Skipping Phase 10 validation.");
            }

            // 13. Phase 11: Safe Runtime Rendering Finalization (settings.CreateSafeRuntimeRendering이 true일 때만)
            if (settings.CreateSafeRuntimeRendering)
            {
                Debug.Log("[MapAutoBuilder] === Phase 11: Enabling Safe Runtime Rendering ===");
                EnableSafeRuntimeRendering(settings, context);

                if (settings.ValidateRuntimeRenderingAfterGenerate)
                {
                    Debug.Log("[MapAutoBuilder] === Phase 11: Validating Runtime Rendering ===");
                    ValidateRuntimeRendering(settings, context);
                }
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createSafeRuntimeRendering is false. Skipping Phase 11.");
            }

            // 14. Phase 12: Terrain Source Binding (settings.CreateTerrainSourceBinding이 true일 때만)
            if (settings.CreateTerrainSourceBinding)
            {
                Debug.Log("[MapAutoBuilder] === Phase 12: Rebuilding Terrain Source Binding ===");
                RebuildTerrainSourceBinding(settings, context);

                Debug.Log("[MapAutoBuilder] === Phase 12: Validating Terrain Source Binding ===");
                ValidateTerrainSourceBinding(settings, context);
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createTerrainSourceBinding is false. Skipping Phase 12.");
            }

            // 15. Phase 13: Terrain Source Layout Stabilization (settings.CreateTerrainSourceLayout이 true일 때만)
            if (settings.CreateTerrainSourceLayout)
            {
                Debug.Log("[MapAutoBuilder] === Phase 13: Rebuilding Terrain Source Layout ===");
                RebuildTerrainSourceLayout(settings, context);

                Debug.Log("[MapAutoBuilder] === Phase 13: Validating Terrain Source Layout ===");
                ValidateTerrainSourceLayout(settings, context);
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createTerrainSourceLayout is false. Skipping Phase 13.");
            }

            // 16. Phase 14.1: Zone Design Database 생성 (settings.CreateZoneDesignDatabase이 true일 때만)
            if (settings.CreateZoneDesignDatabase)
            {
                Debug.Log("[MapAutoBuilder] === Phase 14.1: Rebuilding Zone Design Database ===");
                RebuildZoneDesignDatabase(settings, context);

                if (settings.ValidateZoneDesignDatabaseAfterGenerate)
                {
                    Debug.Log("[MapAutoBuilder] === Phase 14.1: Validating Zone Design Database ===");
                    ValidateZoneDesignDatabase(settings, context);
                }
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createZoneDesignDatabase is false. Skipping Phase 14.1.");
            }

            // 17. Phase 14.2: Zone Design Rule Interpreter (settings.CreateZoneDesignRules이 true일 때만)
            if (settings.CreateZoneDesignRules)
            {
                Debug.Log("[MapAutoBuilder] === Phase 14.2: Rebuilding Zone Design Rules ===");
                RebuildZoneDesignRules(settings, context);

                if (settings.ValidateZoneDesignRulesAfterGenerate)
                {
                    Debug.Log("[MapAutoBuilder] === Phase 14.2: Validating Zone Design Rules ===");
                    ValidateZoneDesignRules(settings, context);
                }
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createZoneDesignRules is false. Skipping Phase 14.2.");
            }

            // 18. Phase 14.3: Zone Terrain Plan Foundation (settings.CreateZoneTerrainPlans이 true일 때만)
            if (settings.CreateZoneTerrainPlans)
            {
                Debug.Log("[MapAutoBuilder] === Phase 14.3: Rebuilding Zone Terrain Plans ===");
                RebuildZoneTerrainPlans(settings, context);

                if (settings.ValidateZoneTerrainPlansAfterGenerate)
                {
                    Debug.Log("[MapAutoBuilder] === Phase 14.3: Validating Zone Terrain Plans ===");
                    ValidateZoneTerrainPlans(settings, context);
                }
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createZoneTerrainPlans is false. Skipping Phase 14.3.");
            }

            // 19. Phase 14.4 + 14.5: Zone Terrain Plan Mesh Patch Generation + Interior Detail
            // (settings.CreateZoneTerrainPatches이 true일 때만)
            // Phase 14.5 interior detail deformation은 RebuildZoneTerrainPatches 내부에서
            // base mesh 생성 직후, seam stabilization 이전에 자동 실행됨
            if (settings.CreateZoneTerrainPatches)
            {
                Debug.Log("[MapAutoBuilder] === Phase 14.4 + 14.5: Rebuilding Zone Terrain Patches with Interior Detail ===");
                RebuildZoneTerrainPatches(settings, context);

                if (settings.ValidateZoneTerrainPatchesAfterGenerate)
                {
                    Debug.Log("[MapAutoBuilder] === Phase 14.4 + 14.5: Validating Zone Terrain Patches ===");
                    ValidateZoneTerrainPatches(settings, context);
                }
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createZoneTerrainPatches is false. Skipping Phase 14.4/14.5.");
            }

            // 20. Phase 14.6 + 14.7: Zone Content Placeholder Foundation + Metadata Binding (settings.CreateZoneContentPlaceholders이 true일 때만)
            if (settings.CreateZoneContentPlaceholders)
            {
                Debug.Log("[MapAutoBuilder] === Phase 14.6 + 14.7: Rebuilding Zone Content Placeholders with Metadata Binding ===");
                RebuildZoneContentPlaceholders(settings, context);

                if (settings.ValidateZoneContentPlaceholdersAfterGenerate)
                {
                    Debug.Log("[MapAutoBuilder] === Phase 14.6 + 14.7: Validating Zone Content Placeholders + Metadata ===");
                    ValidateZoneContentPlaceholders(settings, context);
                }
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createZoneContentPlaceholders is false. Skipping Phase 14.6/14.7.");
            }

            // 21. Phase 14.8: Prototype Region Rebuild (settings.CreatePrototypeRegions이 true일 때만)
            if (settings.CreatePrototypeRegions)
            {
                Debug.Log("[MapAutoBuilder] === Phase 14.8: Rebuilding Prototype Regions ===");
                RebuildPrototypeRegions(settings, context);

                if (settings.ValidatePrototypeRegionsAfterGenerate)
                {
                    Debug.Log("[MapAutoBuilder] === Phase 14.8: Validating Prototype Regions ===");
                    ValidatePrototypeRegions(settings, context);
                }
            }
            else
            {
                LogIfVerbose(settings, "[SKIP] createPrototypeRegions is false. Skipping Phase 14.8.");
            }

            // 22. Phase 14.10-A: Full Terrain Patch Generation (A1~J10, 100개)
            // Phase 14.8 Prototype Region override 결과를 기준으로 실행되어야 하므로
            // Phase 14.8 이후, 최종 완료 로그 이전에 배치한다.
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-A: Rebuild Full Terrain Patches =====");
            RebuildFullTerrainPatches(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-A: Validate Full Terrain Patches =====");
            ValidateFullTerrainPatches(settings, context);

            // 23. Phase 14.10-B: Full Content Root Generation (A1~J10, 100개)
            // Phase 14.10-A Full Terrain Patch 생성/검증 이후 실행되어야 하므로
            // Phase 14.10-A 이후, Phase 14.6/14.7 Content Placeholder/Metadata 생성 이전에 배치한다.
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-B: Full Content Root Generation (A1~J10, 100 zones) =====");
            DeepLightMapFullContentRootGenerationUtility.RebuildFullContentRoots(settings, context);
            DeepLightMapFullContentRootGenerationUtility.ValidateFullContentRoots(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-B: Full Content Root Generation Complete =====");

            // 24. Phase 14.10-C: Full Content Marker Generation (non-prototype 83개 zone)
            // Phase 14.10-B Full Content Root 생성/검증 이후 실행되어야 하므로
            // Phase 14.10-B 이후, 최종 완료 로그 이전에 배치한다.
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-C: Full Content Marker Generation =====");
            DeepLightMapFullContentMarkerGenerationUtility.RebuildFullContentMarkers(settings, context);
            DeepLightMapFullContentMarkerGenerationUtility.ValidateFullContentMarkers(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-C: Full Content Marker Generation Complete =====");

            // 25. Phase 14.10-D-2: Content Marker Debug Visual Generation
            // Phase 14.10-C Full Content Marker 생성/검증 이후 실행되어야 하므로
            // Phase 14.10-C 이후, 최종 완료 로그 이전에 배치한다.
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-D-2: Content Marker Debug Visual Generation =====");
            DeepLightMapContentMarkerDebugVisualUtility.RebuildMarkerDebugVisuals(settings, context);
            DeepLightMapContentMarkerDebugVisualUtility.ValidateMarkerDebugVisuals(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-D-2: Content Marker Debug Visual Generation Complete =====");

            // 26. 생성 완료 후 Selection 설정
            Selection.activeGameObject = generatedRoot;
            EditorGUIUtility.PingObject(generatedRoot);

            // 27. 최종 데이터 카운트 검증 로그
            LogFinalDataCount(settings);

            Debug.Log("[MapAutoBuilder] ===== Generate Full Scenario Map: ALL PHASES (3~14.10-D) COMPLETE =====");

        }
        // ======================================================================
        //  Phase 14.8: Prototype Region Rebuild
        // ======================================================================

        /// <summary>
        /// Phase 14.8: Prototype Region 데이터를 재구축한다.
        /// Hub Basin / Harbor Debris Belt / Western Wreck Field 3개 권역의
        /// DesignEntry / DesignRule / TerrainPlan / TerrainPatch / ContentPlaceholder / Registry를
        /// 최신 기획 기준으로 override한다.
        /// DeepLightMapPrototypeRegionUtility에 위임한다.
        /// </summary>
        public static void RebuildPrototypeRegions(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild prototype regions.");
                return;
            }

            DeepLightMapPrototypeRegionUtility.RebuildPrototypeRegions(settings, context);
        }

        /// <summary>
        /// Phase 14.8: Prototype Region 유효성을 검사한다.
        /// 20개 이상의 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]을 출력한다.
        /// DeepLightMapPrototypeRegionUtility에 위임한다.
        /// </summary>
        public static void ValidatePrototypeRegions(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate prototype regions.");
                return;
            }

            DeepLightMapPrototypeRegionUtility.ValidatePrototypeRegions(settings, context);
        }


        // ======================================================================
        //  Phase 14.10-A: Full Terrain Patch Generation (A1~J10, 100개)
        // ======================================================================

        /// <summary>
        /// Phase 14.10-A: A~J 전체 100개 ZoneRoot에 TerrainPatch scene artifact를 재구축한다.
        /// DeepLightMapFullTerrainPatchGenerationUtility.RebuildFullTerrainPatches에 위임한다.
        /// </summary>
        public static void RebuildFullTerrainPatches(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild full terrain patches.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[MapAutoBuilder] Context is null! Cannot rebuild full terrain patches.");
                return;
            }

            Debug.Log("[MapAutoBuilder] === Phase 14.10-A: Rebuild Full Terrain Patches (A1~J10) ===");
            DeepLightMapFullTerrainPatchGenerationUtility.RebuildFullTerrainPatches(settings, context);
            Debug.Log("[MapAutoBuilder] === Phase 14.10-A: Rebuild Full Terrain Patches Complete ===");
        }

        /// <summary>
        /// Phase 14.10-A: A~J 전체 100개 ZoneRoot의 TerrainPatch scene artifact 유효성을 검사한다.
        /// DeepLightMapFullTerrainPatchGenerationUtility.ValidateFullTerrainPatches에 위임한다.
        /// </summary>
        public static void ValidateFullTerrainPatches(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate full terrain patches.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[MapAutoBuilder] Context is null! Cannot validate full terrain patches.");
                return;
            }

            Debug.Log("[MapAutoBuilder] === Phase 14.10-A: Validate Full Terrain Patches (A1~J10) ===");
            DeepLightMapFullTerrainPatchGenerationUtility.ValidateFullTerrainPatches(settings, context);
            Debug.Log("[MapAutoBuilder] === Phase 14.10-A: Validate Full Terrain Patches Complete ===");
        }

        // ======================================================================
        //  Phase 14.10-B: Full Content Root Generation (A1~J10, 100개)
        // ======================================================================

        /// <summary>
        /// Phase 14.10-B: A~J 전체 100개 ZoneRoot에 Content root 구조만 재구축한다.
        /// DeepLightMapFullContentRootGenerationUtility.RebuildFullContentRoots에 위임한다.
        /// </summary>
        public static void RebuildFullContentRoots(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [Phase 14.10-B] Settings is null! Cannot rebuild full content roots.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[MapAutoBuilder] [Phase 14.10-B] Context is null! Cannot rebuild full content roots.");
                return;
            }

            Debug.Log("[MapAutoBuilder] === Phase 14.10-B: Rebuild Full Content Roots (A1~J10) ===");
            DeepLightMapFullContentRootGenerationUtility.RebuildFullContentRoots(settings, context);
            Debug.Log("[MapAutoBuilder] === Phase 14.10-B: Rebuild Full Content Roots Complete ===");
        }

        /// <summary>
        /// Phase 14.10-B: A~J 전체 100개 ZoneRoot의 Content root 구조 유효성을 검사한다.
        /// DeepLightMapFullContentRootGenerationUtility.ValidateFullContentRoots에 위임한다.
        /// </summary>
        public static void ValidateFullContentRoots(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [Phase 14.10-B] Settings is null! Cannot validate full content roots.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[MapAutoBuilder] [Phase 14.10-B] Context is null! Cannot validate full content roots.");
                return;
            }

            Debug.Log("[MapAutoBuilder] === Phase 14.10-B: Validate Full Content Roots (A1~J10) ===");
            DeepLightMapFullContentRootGenerationUtility.ValidateFullContentRoots(settings, context);
            Debug.Log("[MapAutoBuilder] === Phase 14.10-B: Validate Full Content Roots Complete ===");
        }

        // ======================================================================
        //  Phase 14.10-C: Full Content Marker Generation (A1~J10, 100개)
        // ======================================================================

        /// <summary>
        /// Phase 14.10-C: A~J 전체 100개 ZoneRoot의 Content 하위에 marker를 재구축한다.
        /// prototype 17개 zone은 스킵하고, non-prototype 83개 zone에 대해서만 생성한다.
        /// DeepLightMapFullContentMarkerGenerationUtility.RebuildFullContentMarkers에 위임한다.
        /// </summary>
        public static void RebuildFullContentMarkers(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild full content markers.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[MapAutoBuilder] Context is null! Cannot rebuild full content markers.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-C: Rebuild Full Content Markers =====");
            DeepLightMapFullContentMarkerGenerationUtility.RebuildFullContentMarkers(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-C: Rebuild Full Content Markers Complete =====");
        }

        /// <summary>
        /// Phase 14.10-C: A~J 전체 100개 ZoneRoot의 Content marker 유효성을 검사한다.
        /// prototype 17개 zone은 validation 대상에서 제외한다.
        /// DeepLightMapFullContentMarkerGenerationUtility.ValidateFullContentMarkers에 위임한다.
        /// </summary>
        public static void ValidateFullContentMarkers(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate full content markers.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[MapAutoBuilder] Context is null! Cannot validate full content markers.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-C: Validate Full Content Markers =====");
            DeepLightMapFullContentMarkerGenerationUtility.ValidateFullContentMarkers(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-C: Validate Full Content Markers Complete =====");
        }


        /// <summary>
        /// ZoneRoot_A1~J10만 다시 생성한다.
        /// 기존 ZoneRoot가 있으면 재사용하고 위치/이름을 보정한다.
        /// GeneratedWorldRoot/ZoneRoots 하위에만 생성하며, 그 외 Hierarchy는 절대 수정하지 않는다.
        /// </summary>
        public static void RebuildZoneRootsOnly(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild zone roots.");
                return;
            }

            if (settings.WorldMapConfig == null || settings.ScenarioPreset == null)
            {
                Debug.LogError("[MapAutoBuilder] WorldMapConfig or ScenarioPreset is null. Cannot rebuild zone roots.");
                return;
            }

            // Generated Root 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // ZoneRoots 부모 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[MapAutoBuilder] '{settings.ZoneRootParentName}' not found under '{generatedRoot.name}'. Run Generate Full Scenario Map first.");
                return;
            }

            GameObject zoneRootsParent = zoneRootsTransform.gameObject;

            // DeepLightMapZoneGenerationUtility에 위임
            DeepLightMapZoneGenerationUtility.RebuildAllZoneRoots(settings, zoneRootsParent);

            // Selection 설정
            Selection.activeGameObject = zoneRootsParent;
            EditorGUIUtility.PingObject(zoneRootsParent);
        }

        /// <summary>
        /// ZoneRoot 설정의 유효성을 검사한다.
        /// 16개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneSetup(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate zone setup.");
                return;
            }

            // DeepLightMapZoneGenerationUtility에 위임
            DeepLightMapZoneGenerationUtility.ValidateZoneSetup(settings);
        }

        /// <summary>
        /// ZoneResolver의 좌표 기반 Zone 판정을 검증한다.
        /// Phase 6: DeepLightMapZoneResolverValidationUtility에 위임한다.
        /// </summary>
        public static void ValidateZoneResolver(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate ZoneResolver.");
                return;
            }

            DeepLightMapZoneResolverValidationUtility.ValidateZoneResolver(settings);
        }

        /// <summary>
        /// Depth Sampling 검증을 수행한다.
        /// Phase 6: DeepLightMapZoneResolverValidationUtility에 위임한다.
        /// </summary>
        public static void ValidateDepthSampling(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate depth sampling.");
                return;
            }

            DeepLightMapZoneResolverValidationUtility.ValidateDepthSampling(settings);
        }

        /// <summary>
        /// 생성된 맵을 정리한다.
        /// 오직 GeneratedWorldRoot만 삭제 대상이며, 보호된 오브젝트는 절대 삭제하지 않는다.
        /// 삭제 후 context.GeneratedRootOverride가 삭제된 오브젝트를 가리키면 null로 초기화한다.
        /// </summary>
        public static void ClearGeneratedMap(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot clear.");
                return;
            }

            LogIfVerbose(settings, "===== Map Auto Builder: Clear Generated Map =====");

            // 삭제 대상 찾기
            GameObject target = FindGeneratedRoot(settings, context);

            if (target == null)
            {
                Debug.Log("[MapAutoBuilder] 삭제할 GeneratedWorldRoot가 없습니다. Nothing to clear.");
                return;
            }

            // 보호된 오브젝트인지 확인 (null 체크 후)
            if (IsProtectedSceneObject(target))
            {
                Debug.LogError($"[MapAutoBuilder] 대상 '{target.name}'은(는) 보호된 오브젝트입니다. 삭제할 수 없습니다.");
                return;
            }

            // 이름이 정확히 일치하는지 추가 확인 (generatedRootOverride가 아닌 경우)
            bool hasOverride = context != null && context.GeneratedRootOverride != null;
            if (!hasOverride && target.name != settings.GeneratedRootName)
            {
                Debug.LogError($"[MapAutoBuilder] 대상 이름 '{target.name}'이(가) 설정된 GeneratedRootName '{settings.GeneratedRootName}'과(와) 일치하지 않습니다. 삭제를 중단합니다.");
                return;
            }

            // 삭제 전에 context.GeneratedRootOverride가 target과 같은지 기록
            bool isOverrideTarget = (context != null && context.GeneratedRootOverride == target);

            // Undo 지원과 함께 삭제
            Undo.DestroyObjectImmediate(target);

            // 삭제된 오브젝트가 context.GeneratedRootOverride와 같으면 null 처리
            if (isOverrideTarget && context != null)
            {
                context.ClearGeneratedRootOverride();
                EditorUtility.SetDirty(context);
                Debug.Log("[MapAutoBuilder] SceneContext.GeneratedRootOverride cleared (was the deleted object).");
            }

            // Selection이 삭제된 오브젝트 또는 그 자식이면 null 처리
            if (Selection.activeGameObject == null || Selection.activeGameObject == target)
            {
                Selection.activeObject = null;
            }

            Debug.Log("[MapAutoBuilder] GeneratedWorldRoot cleared.");
            Debug.Log("[MapAutoBuilder] SceneContext preserved.");
        }

        // ======================================================================
        //  Internal Helpers
        // ======================================================================

        /// <summary>
        /// Settings + Context에 따라 Generated Root GameObject를 획득하거나 새로 생성한다.
        /// 우선순위: context.GeneratedRootOverride > Scene 내 이름 검색 > 새 생성
        /// </summary>
        private static GameObject GetOrCreateSceneRoot(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            // 1. Override가 있으면 사용
            if (context != null && context.GeneratedRootOverride != null)
            {
                if (IsProtectedSceneObject(context.GeneratedRootOverride))
                {
                    Debug.LogError($"[MapAutoBuilder] GeneratedRootOverride '{context.GeneratedRootOverride.name}' is protected! Cannot use.");
                    return null;
                }
                LogIfVerbose(settings, $"Using GeneratedRootOverride: {context.GeneratedRootOverride.name}");
                return context.GeneratedRootOverride;
            }

            // 2. Scene root에서 이름으로 검색
            string rootName = settings.GeneratedRootName;
            GameObject existing = GameObject.Find(rootName);
            if (existing != null)
            {
                if (IsProtectedSceneObject(existing))
                {
                    Debug.LogError($"[MapAutoBuilder] Found '{rootName}' but it is a protected object! Cannot use.");
                    return null;
                }
                LogIfVerbose(settings, $"Found existing root: {rootName}");
                return existing;
            }

            // 3. 새로 생성
            GameObject newRoot = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(newRoot, $"Create {rootName}");
            LogIfVerbose(settings, $"Created new root: {rootName}");
            return newRoot;
        }

        /// <summary>
        /// 부모 GameObject 아래에 자식 GameObject를 찾거나 생성한다.
        /// </summary>
        private static GameObject GetOrCreateChild(GameObject parent, string childName)
        {
            // 부모 Transform에서 자식 검색
            Transform existing = parent.transform.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            // 없으면 새로 생성
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            return child;
        }

        /// <summary>
        /// Settings + Context에 따라 Generated Root를 찾는다.
        /// 우선순위: context.GeneratedRootOverride > Scene 내 이름 검색
        /// </summary>
        public static GameObject FindGeneratedRoot(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (context != null && context.GeneratedRootOverride != null)
            {
                return context.GeneratedRootOverride;
            }

            return GameObject.Find(settings.GeneratedRootName);
        }

        /// <summary>
        /// Phase 3 기본 루트 구조를 생성한다.
        /// GeneratedWorldRoot/
        ///   ├── GlobalWater/
        ///   ├── ZoneRoots/
        ///   ├── RuntimeEnvironment/
        ///   │     ├── UnderwaterAreas/
        ///   │     ├── DynamicEffects/
        ///   │     └── SharedParticles/
        ///   └── Debug/
        /// </summary>
        private static void CreateBaseRootStructure(DeepLightMapAutoBuilderSettingsSO settings, GameObject generatedRoot)
        {
            LogIfVerbose(settings, "Creating base root structure...");

            // GlobalWater
            GameObject globalWater = GetOrCreateChild(generatedRoot, settings.GlobalWaterRootName);
            LogIfVerbose(settings, $"  [OK] {settings.GlobalWaterRootName}");

            // ZoneRoots (Phase 4에서 ZoneRoot_A1~J10이 생성될 부모)
            GameObject zoneRoots = GetOrCreateChild(generatedRoot, settings.ZoneRootParentName);
            LogIfVerbose(settings, $"  [OK] {settings.ZoneRootParentName} (Phase 4: ZoneRoots will be created here)");

            // RuntimeEnvironment
            GameObject runtimeEnv = GetOrCreateChild(generatedRoot, settings.RuntimeEnvironmentRootName);
            LogIfVerbose(settings, $"  [OK] {settings.RuntimeEnvironmentRootName}");

            // RuntimeEnvironment 하위 구조
            if (settings.CreateEnvironmentRoots)
            {
                GetOrCreateChild(runtimeEnv, settings.UnderwaterAreasRootName);
                LogIfVerbose(settings, $"  [OK] {settings.RuntimeEnvironmentRootName}/{settings.UnderwaterAreasRootName}");

                GetOrCreateChild(runtimeEnv, settings.DynamicEffectsRootName);
                LogIfVerbose(settings, $"  [OK] {settings.RuntimeEnvironmentRootName}/{settings.DynamicEffectsRootName}");

                GetOrCreateChild(runtimeEnv, settings.SharedParticlesRootName);
                LogIfVerbose(settings, $"  [OK] {settings.RuntimeEnvironmentRootName}/{settings.SharedParticlesRootName}");
            }

            // Debug
            if (settings.CreateDebugVisuals)
            {
                GetOrCreateChild(generatedRoot, settings.DebugRootName);
                LogIfVerbose(settings, $"  [OK] {settings.DebugRootName}");
            }
        }

        /// <summary>
        /// GlobalWater 템플릿을 복제하여 GeneratedWorldRoot/GlobalWater 하위에 배치한다.
        /// 원본은 절대 수정하지 않는다. SceneContext의 GlobalWaterTemplate을 사용한다.
        /// </summary>
        private static void CloneGlobalWaterIfNeeded(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context, GameObject generatedRoot)
        {
            if (!settings.CreateGlobalWaterClone)
            {
                LogIfVerbose(settings, "[SKIP] createGlobalWaterClone is false. Skipping global water clone.");
                return;
            }

            // SceneContext가 없거나 템플릿이 없으면 스킵
            if (context == null || context.GlobalWaterTemplate == null)
            {
                LogIfVerbose(settings, "[SKIP] GlobalWaterTemplate is null (SceneContext missing or no template). Skipping global water clone.");
                return;
            }

            // GlobalWater 부모 찾기
            GameObject globalWaterParent = GetOrCreateChild(generatedRoot, settings.GlobalWaterRootName);

            // 복제본 이름 결정
            string cloneName = $"{context.GlobalWaterTemplate.name}_Generated";

            // 기존 복제본이 있으면 삭제 후 재생성
            Transform existingClone = globalWaterParent.transform.Find(cloneName);
            if (existingClone != null)
            {
                Undo.DestroyObjectImmediate(existingClone.gameObject);
                LogIfVerbose(settings, $"Removed existing clone: {cloneName}");
            }

            // 복제 실행
            GameObject clone = null;

            // PrefabUtility.InstantiatePrefab 시도 (Prefab asset인 경우)
            if (PrefabUtility.GetPrefabAssetType(context.GlobalWaterTemplate) != PrefabAssetType.NotAPrefab)
            {
                clone = PrefabUtility.InstantiatePrefab(context.GlobalWaterTemplate) as GameObject;
            }

            // 실패하거나 Prefab이 아니면 Object.Instantiate fallback
            if (clone == null)
            {
                clone = Object.Instantiate(context.GlobalWaterTemplate);
            }

            if (clone == null)
            {
                Debug.LogError("[MapAutoBuilder] Failed to clone GlobalWaterTemplate!");
                return;
            }

            // 복제본 설정
            clone.name = cloneName;
            clone.transform.SetParent(globalWaterParent.transform);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            // 원본 스케일 유지 (원본이 이미 올바른 스케일을 가지고 있다고 가정)

            Undo.RegisterCreatedObjectUndo(clone, $"Clone GlobalWater: {cloneName}");

            LogIfVerbose(settings, $"GlobalWater clone created: {cloneName} (parent: {globalWaterParent.name})");
        }

        /// <summary>
        /// 대상 GameObject가 보호된 오브젝트인지 검사한다.
        /// 보호 대상: MapSettings, _WorldMap_Manual, WorldMapHUDCanvas, WorldMapMiniGridCanvas,
        /// UIRoot, RuntimeControllers, Submarine, CameraSystem, DeepLightMapAutoBuilderContext 및 그 자식들,
        /// GeneratedWorldRoot 밖에 있는 ZoneRoot_* 오브젝트.
        /// </summary>
        public static bool IsProtectedSceneObject(GameObject target)
        {
            if (target == null)
                return false;

            // 정확한 이름 매치
            if (ProtectedRootNames.Contains(target.name))
                return true;

            // ZoneRoot_* 이면서 GeneratedWorldRoot 밖에 있으면 보호
            if (target.name.StartsWith("ZoneRoot_"))
            {
                // 부모 체인에 GeneratedWorldRoot가 없으면 보호
                Transform parent = target.transform.parent;
                bool isUnderGenerated = false;
                while (parent != null)
                {
                    if (parent.name == "GeneratedWorldRoot")
                    {
                        isUnderGenerated = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (!isUnderGenerated)
                    return true;
            }

            // MapSettings 또는 _WorldMap_Manual의 자식이면 보호
            Transform current = target.transform.parent;
            while (current != null)
            {
                if (current.name == "MapSettings" || current.name == "_WorldMap_Manual")
                    return true;
                current = current.parent;
            }

            return false;
        }

        // ======================================================================
        //  Phase 7: Runtime Binding
        // ======================================================================

        /// <summary>
        /// Runtime Binding을 재구축한다.
        /// Phase 7: DeepLightMapRuntimeBindingUtility에 위임한다.
        /// </summary>
        public static void RebuildRuntimeBindings(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild runtime bindings.");
                return;
            }

            DeepLightMapRuntimeBindingUtility.RebuildRuntimeBindings(settings, context);
        }

        /// <summary>
        /// Runtime Binding의 유효성을 검사한다.
        /// Phase 7: DeepLightMapRuntimeBindingUtility에 위임한다.
        /// </summary>
        public static void ValidateRuntimeBindings(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate runtime bindings.");
                return;
            }

            DeepLightMapRuntimeBindingUtility.ValidateRuntimeBindings(settings, context);
        }

        // ======================================================================
        //  Phase 8: Visual Runtime Binding
        // ======================================================================

        /// <summary>
        /// Visual Runtime Binding을 재구축한다.
        /// Phase 8: DeepLightMapVisualBindingUtility에 위임한다.
        /// </summary>
        public static void RebuildVisualRuntimeBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild visual runtime binding.");
                return;
            }

            DeepLightMapVisualBindingUtility.RebuildVisualRuntimeBinding(settings, context);
        }

        /// <summary>
        /// Visual Runtime Binding의 유효성을 검사한다.
        /// Phase 8: DeepLightMapVisualBindingUtility에 위임한다.
        /// </summary>
        public static void ValidateVisualRuntimeBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate visual runtime binding.");
                return;
            }

            DeepLightMapVisualBindingUtility.ValidateVisualRuntimeBinding(settings, context);
        }

        /// <summary>
        /// Phase 9: Visual Adapter Binding을 재구축한다.
        /// </summary>
        public static void RebuildVisualAdapterBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild visual adapter binding.");
                return;
            }

            DeepLightMapVisualBindingUtility.RebuildVisualAdapterBinding(settings, context);
        }

        /// <summary>
        /// Phase 9: Visual Adapter Binding의 유효성을 검사한다.
        /// </summary>
        public static void ValidateVisualAdapterBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate visual adapter binding.");
                return;
            }

            DeepLightMapVisualBindingUtility.ValidateVisualAdapterBinding(settings, context);
        }

        /// <summary>
        /// Phase 10: Visual Adapter Runtime Flow의 유효성을 검사한다.
        /// DeepLightMapVisualAdapterRuntimeValidationUtility에 위임한다.
        /// </summary>
        public static void ValidateVisualAdapterRuntimeFlow(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate visual adapter runtime flow.");
                return;
            }

            DeepLightMapVisualAdapterRuntimeValidationUtility.ValidateVisualAdapterRuntimeFlow(settings, context);
        }

        // ======================================================================
        //  Phase 11: Safe Runtime Rendering Finalization
        // ======================================================================

        /// <summary>
        /// Phase 11: Safe Runtime Rendering을 활성화한다.
        /// DeepLightMapSafeRuntimeRenderingUtility에 위임한다.
        /// </summary>
        public static void EnableSafeRuntimeRendering(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot enable safe runtime rendering.");
                return;
            }

            DeepLightMapSafeRuntimeRenderingUtility.EnableSafeRuntimeRendering(settings, context);
        }

        /// <summary>
        /// Phase 11: Runtime Rendering의 유효성을 검사한다.
        /// DeepLightMapSafeRuntimeRenderingUtility에 위임한다.
        /// </summary>
        public static void ValidateRuntimeRendering(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate runtime rendering.");
                return;
            }

            DeepLightMapSafeRuntimeRenderingUtility.ValidateRuntimeRendering(settings, context);
        }

        // ======================================================================
        //  Phase 12: Terrain Source Binding
        // ======================================================================

        /// <summary>
        /// Phase 12: Terrain Source Binding을 재구축한다.
        /// DeepLightMapTerrainSourceBindingUtility에 위임한다.
        /// </summary>
        public static void RebuildTerrainSourceBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild terrain source binding.");
                return;
            }

            DeepLightMapTerrainSourceBindingUtility.RebuildTerrainSourceBinding(settings, context);
        }

        /// <summary>
        /// Phase 12: Terrain Source Binding의 유효성을 검사한다.
        /// DeepLightMapTerrainSourceBindingUtility에 위임한다.
        /// </summary>
        public static void ValidateTerrainSourceBinding(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate terrain source binding.");
                return;
            }

            DeepLightMapTerrainSourceBindingUtility.ValidateTerrainSourceBinding(settings, context);
        }

        // ======================================================================
        //  Phase 13: Terrain Source Layout Stabilization
        // ======================================================================

        /// <summary>
        /// Phase 13: Terrain Source Layout을 재구축한다.
        /// DeepLightMapTerrainSourceLayoutUtility에 위임한다.
        /// </summary>
        public static void RebuildTerrainSourceLayout(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild terrain source layout.");
                return;
            }

            DeepLightMapTerrainSourceLayoutUtility.RebuildTerrainSourceLayout(settings, context);
        }

        /// <summary>
        /// Phase 13: Terrain Source Layout의 유효성을 검사한다.
        /// DeepLightMapTerrainSourceLayoutUtility에 위임한다.
        /// </summary>
        public static void ValidateTerrainSourceLayout(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate terrain source layout.");
                return;
            }

            DeepLightMapTerrainSourceLayoutUtility.ValidateTerrainSourceLayout(settings, context);
        }

        // ======================================================================
        //  Phase 14.1: Zone Design Database
        // ======================================================================

        /// <summary>
        /// Phase 14.1: Zone Design Database를 재구축한다.
        /// A1~J10 전체 100개 entry를 생성한다 (Phase 14.9 통합).
        /// DeepLightMapZoneDesignDatabaseUtility에 위임한다.
        /// </summary>
        public static void RebuildZoneDesignDatabase(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild zone design database.");
                return;
            }

            // 1. Database asset 찾기 또는 생성
            WorldMapZoneDesignDatabaseSO database = DeepLightMapZoneDesignDatabaseUtility.FindOrCreateDatabaseAsset();
            if (database == null)
            {
                Debug.LogError("[MapAutoBuilder] Failed to find or create ZoneDesignDatabase asset.");
                return;
            }

            // 2. A1~J10 전체 100개 entries 채우기 (Phase 14.9 통합: 구버전 PopulateA1ToC10Entries 30개 대신 100개 생성)
            DeepLightMapZoneDesignDatabaseUtility.RebuildFullZoneDesignDatabase(settings, context);

            // 3. SettingsSO에 참조 연결
            if (settings.ZoneDesignDatabase != database)
            {
                SerializedObject serializedSettings = new SerializedObject(settings);
                SerializedProperty dbProp = serializedSettings.FindProperty("zoneDesignDatabase");
                if (dbProp != null)
                {
                    dbProp.objectReferenceValue = database;
                    serializedSettings.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    Debug.Log("[MapAutoBuilder] ZoneDesignDatabase reference linked to SettingsSO.");
                }
            }

            Debug.Log("[MapAutoBuilder] Phase 14.1: Zone Design Database rebuild complete (A1~J10, 100 entries).");
        }

        /// <summary>
        /// Phase 14.1: Zone Design Database의 유효성을 검사한다.
        /// 20개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneDesignDatabase(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate zone design database.");
                return;
            }

            DeepLightMapZoneDesignDatabaseUtility.ValidateZoneDesignDatabase(settings);
        }

        // ======================================================================
        //  Phase 14.2: Zone Design Rule Interpreter
        // ======================================================================

        /// <summary>
        /// Phase 14.2: Zone Design Rule Database를 재구축한다.
        /// DeepLightMapZoneDesignRuleUtility에 위임한다.
        /// </summary>
        public static void RebuildZoneDesignRules(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild zone design rules.");
                return;
            }

            DeepLightMapZoneDesignRuleUtility.RebuildZoneDesignRules(settings, context);
        }

        /// <summary>
        /// Phase 14.2: Zone Design Rule Database의 유효성을 검사한다.
        /// 17개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneDesignRules(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate zone design rules.");
                return;
            }

            DeepLightMapZoneDesignRuleUtility.ValidateZoneDesignRules(settings, context);
        }

        // ======================================================================
        //  Phase 14.3: Zone Terrain Plan Foundation
        // ======================================================================

        /// <summary>
        /// Phase 14.3: Zone Terrain Plan Database를 재구축한다.
        /// DeepLightMapZoneTerrainPlanUtility에 위임한다.
        /// </summary>
        public static void RebuildZoneTerrainPlans(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild zone terrain plans.");
                return;
            }

            DeepLightMapZoneTerrainPlanUtility.RebuildZoneTerrainPlans(settings, context);
        }

        /// <summary>
        /// Phase 14.3: Zone Terrain Plan Database의 유효성을 검사한다.
        /// 21개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneTerrainPlans(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate zone terrain plans.");
                return;
            }

            DeepLightMapZoneTerrainPlanUtility.ValidateZoneTerrainPlans(settings, context);
        }

        // ======================================================================
        //  Phase 14.4: Zone Terrain Plan Mesh Patch Generation
        // ======================================================================

        /// <summary>
        /// Phase 14.4: Zone Terrain Patches를 재구축한다.
        /// A1~C10 각 ZoneRoot 하위에 seafloor mesh patch와 collision mesh를 생성한다.
        /// DeepLightMapZoneTerrainPatchUtility에 위임한다.
        /// </summary>
        public static void RebuildZoneTerrainPatches(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild zone terrain patches.");
                return;
            }

            DeepLightMapZoneTerrainPatchUtility.RebuildZoneTerrainPatches(settings, context);
        }

        /// <summary>
        /// Phase 14.4: Zone Terrain Patches의 유효성을 검사한다.
        /// 23개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneTerrainPatches(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate zone terrain patches.");
                return;
            }

            DeepLightMapZoneTerrainPatchUtility.ValidateZoneTerrainPatches(settings, context);
        }

        // ======================================================================
        //  Phase 14.6 + 14.7: Zone Content Placeholder Foundation + Metadata Binding
        // ======================================================================

        /// <summary>
        /// Phase 14.6 + 14.7: Zone Content Placeholders를 재구축하고 metadata를 부착한다.
        /// A1~C10 각 ZoneRoot 하위에 Content/ResourceSpawns/HazardSpawns/LandmarkSpawns/NarrativeSpawns/RouteMarkers/DebugMarkers 구조를 생성한다.
        /// Phase 14.7: WorldMapZoneContentMarker metadata component를 각 marker root에 부착하고,
        /// WorldMapZoneContentRegistry를 GeneratedWorldRoot에 추가한다.
        /// DeepLightMapZoneContentPlaceholderUtility에 위임한다.
        /// </summary>
        public static void RebuildZoneContentPlaceholders(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot rebuild zone content placeholders.");
                return;
            }

            DeepLightMapZoneContentPlaceholderUtility.RebuildZoneContentPlaceholders(settings, context);
        }

        /// <summary>
        /// Phase 14.6 + 14.7: Zone Content Placeholders + Metadata의 유효성을 검사한다.
        /// 기존 22개 항목 + Phase 14.7 17개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]을 출력한다.
        /// DeepLightMapZoneContentPlaceholderUtility에 위임한다.
        /// </summary>
        public static void ValidateZoneContentPlaceholders(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate zone content placeholders.");
                return;
            }

            DeepLightMapZoneContentPlaceholderUtility.ValidateZoneContentPlaceholders(settings, context);
        }

        /// <summary>
        /// Generate Full Scenario Map 완료 후 최종 데이터 카운트를 검증하고 로그를 출력한다.
        /// DesignEntry / DesignRule / TerrainPlan 각각 100개인지 확인한다.
        /// </summary>
        private static void LogFinalDataCount(DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings == null) return;

            int entries = settings.ZoneDesignDatabase?.Entries?.Count ?? 0;
            int rules = settings.ZoneDesignRuleDatabase?.Rules?.Count ?? 0;
            int plans = settings.ZoneTerrainPlanDatabase?.Plans?.Count ?? 0;

            Debug.Log($"[MapAutoBuilder] Final data count check: entries={entries}, rules={rules}, plans={plans}");

            bool allPass = true;

            if (entries != 100)
            {
                Debug.LogError($"[MapAutoBuilder] [FAIL] Final ZoneDesignDatabase entries={entries} (expected 100)");
                allPass = false;
            }

            if (rules != 100)
            {
                Debug.LogError($"[MapAutoBuilder] [FAIL] Final ZoneDesignRuleDatabase rules={rules} (expected 100)");
                allPass = false;
            }

            if (plans != 100)
            {
                Debug.LogError($"[MapAutoBuilder] [FAIL] Final ZoneTerrainPlanDatabase plans={plans} (expected 100)");
                allPass = false;
            }

            if (allPass)
            {
                Debug.Log("[MapAutoBuilder] [PASS] Final A~J data count preserved after GenerateFullScenarioMap.");
            }
        }

        /// <summary>
        /// logVerbose가 true일 때만 로그를 출력한다
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[MapAutoBuilder] {message}");
            }
        }
    }
}

