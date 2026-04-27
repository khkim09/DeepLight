using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// DeepLight Map Auto Builder의 핵심 로직을 담은 정적 클래스.
    /// Scene 맵 자동 생성/검증/삭제를 수행하며, 모든 Hierarchy 조작은
    /// GeneratedWorldRoot 하위로 제한된다.
    ///
    /// Phase 3: 기본 루트 구조 생성 (GlobalWater, ZoneRoots, RuntimeEnvironment, Debug)
    /// Phase 4+: ZoneRoot_A1~J10, ZoneTrigger, UnderwaterArea per Zone 생성
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
        /// Phase 3: 기본 루트 구조만 생성 (GlobalWater, ZoneRoots, RuntimeEnvironment, Debug).
        /// ZoneRoot_A1~J10, ZoneTrigger, UnderwaterArea per Zone은 생성하지 않는다.
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

            LogIfVerbose(settings, "===== Map Auto Builder: Generate Full Scenario Map (Phase 3) =====");

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

            // 4. 생성 완료 후 Selection 설정
            Selection.activeGameObject = generatedRoot;
            EditorGUIUtility.PingObject(generatedRoot);

            LogIfVerbose(settings, $"===== Map Auto Builder: Generation complete. Root: {generatedRoot.name} =====");
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
        private static GameObject FindGeneratedRoot(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
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

        /// <summary>
        /// logVerbose가 true일 때만 로그를 출력한다.
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
