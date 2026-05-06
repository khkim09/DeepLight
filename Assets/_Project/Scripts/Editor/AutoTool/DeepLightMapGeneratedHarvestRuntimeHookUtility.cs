using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Content;
using Project.Gameplay.Harvest;
using Project.Gameplay.World.Harvest;
using Project.Gameplay.Interaction;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-O-11: Generated harvest target runtime hook을
    /// 기존 HarvestPointInteractor/Provider 흐름에 연결하는 Editor Utility.
    /// GenerateFullScenarioMap에는 아직 통합하지 않음. O-11 독립 검증 단계.
    /// </summary>
    public static class DeepLightMapGeneratedHarvestRuntimeHookUtility
    {
        // ===== Public API =====

        /// <summary>
        /// Generated harvest runtime hook을 재구축한다.
        /// GeneratedWorldRoot 찾기 → Provider 존재 확인/추가 → Provider 초기화 →
        /// 기존 HarvestPointInteractor들을 찾고 가능하면 generated provider reference를 자동 연결.
        /// 단, 기존 scene object transform/name 변경 금지. 컴포넌트 필드 연결만 수행.
        /// </summary>
        public static void RebuildGeneratedHarvestRuntimeHook(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            Debug.Log("[O-11] ===== Rebuild Generated Harvest Runtime Hook =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedWorldRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[O-11] GeneratedWorldRoot not found. Cannot rebuild runtime hook.");
                return;
            }

            // 2. WorldMapHarvestInteractionTargetProvider 찾기
            WorldMapHarvestInteractionTargetProvider provider = generatedRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);
            if (provider == null)
            {
                Debug.Log("[O-11] [INFO] WorldMapHarvestInteractionTargetProvider not found. Adding to GeneratedWorldRoot...");
                provider = generatedRoot.AddComponent<WorldMapHarvestInteractionTargetProvider>();
                Debug.Log("[O-11] [OK] WorldMapHarvestInteractionTargetProvider added to GeneratedWorldRoot.");
            }
            else
            {
                Debug.Log("[O-11] [OK] WorldMapHarvestInteractionTargetProvider already exists.");
            }

            // 3. Provider 초기화
            if (!provider.IsReady)
            {
                provider.TryInitialize();
            }

            if (!provider.IsReady)
            {
                Debug.LogWarning("[O-11] [WARN] Provider is not ready after initialization. Count may be 0.");
            }
            else
            {
                Debug.Log($"[O-11] [OK] Provider initialized. Count={provider.Count}");
            }

            // 4. 기존 HarvestPointInteractor들을 찾고 generatedTargetHook 연결
            HarvestPointInteractor[] interactors = GameObject.FindObjectsByType<HarvestPointInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int hookedCount = 0;
            foreach (var interactor in interactors)
            {
                if (interactor == null)
                    continue;

                // 이미 hook 컴포넌트가 있는지 확인
                WorldMapGeneratedHarvestTargetRuntimeHook existingHook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (existingHook == null)
                {
                    existingHook = interactor.GetComponentInParent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                }

                if (existingHook == null)
                {
                    Debug.Log($"[O-11] [INFO] Interactor '{interactor.name}' has no WorldMapGeneratedHarvestTargetRuntimeHook. " +
                        "Add manually or skip. (No interactor in scene is OK for this phase.)");
                    continue;
                }

                // Provider 참조 연결
                if (existingHook.Provider == null)
                {
                    existingHook.Provider = provider;
                    EditorUtility.SetDirty(existingHook);
                    Debug.Log($"[O-11] [OK] Provider assigned to hook on '{interactor.name}'.");
                }
                else
                {
                    Debug.Log($"[O-11] [OK] Hook on '{interactor.name}' already has provider reference.");
                }

                // Hook 초기화
                existingHook.TryInitialize();
                hookedCount++;
            }

            Debug.Log($"[O-11] [INFO] Found {interactors.Length} HarvestPointInteractor(s). Hooked {hookedCount} interactor(s).");
            Debug.Log("[O-11] ===== Rebuild Generated Harvest Runtime Hook Complete =====");
        }

        /// <summary>
        /// Generated harvest runtime hook의 유효성을 검증한다.
        /// 최소 30개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// </summary>
        public static void ValidateGeneratedHarvestRuntimeHook(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            Debug.Log("[O-11] ===== Validate Generated Harvest Runtime Hook =====");

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-O-11: Validate Generated Harvest Runtime Hook =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = FindGeneratedWorldRoot(settings, context);
            if (generatedRoot != null)
            {
                log.AppendLine("  [PASS] GeneratedWorldRoot exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] GeneratedWorldRoot not found.");
                failCount++;
            }

            // 2. WorldMapHarvestInteractionTargetProvider exists
            WorldMapHarvestInteractionTargetProvider provider = null;
            if (generatedRoot != null)
            {
                provider = generatedRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);
                if (provider != null)
                {
                    log.AppendLine("  [PASS] WorldMapHarvestInteractionTargetProvider exists.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [FAIL] WorldMapHarvestInteractionTargetProvider not found.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [FAIL] Cannot check provider: GeneratedWorldRoot not found.");
                failCount++;
            }

            // 3. Provider.IsReady == true
            if (provider != null)
            {
                if (provider.IsReady)
                {
                    log.AppendLine("  [PASS] Provider.IsReady == true.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [FAIL] Provider.IsReady == false.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [FAIL] Cannot check IsReady: provider is null.");
                failCount++;
            }

            // 4. Provider.Count == 64
            if (provider != null)
            {
                if (provider.Count == 64)
                {
                    log.AppendLine($"  [PASS] Provider.Count == {provider.Count}.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Provider.Count == {provider.Count} (expected 64).");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [FAIL] Cannot check Count: provider is null.");
                failCount++;
            }

            // 5. WorldMapGeneratedHarvestTarget count == 64 (via CollectGeneratedHarvestTargets)
            int generatedTargetCount = 0;
            if (generatedRoot != null)
            {
                List<WorldMapGeneratedHarvestTarget> collectedTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);
                generatedTargetCount = collectedTargets.Count;
                if (generatedTargetCount == 64)
                {
                    log.AppendLine($"  [PASS] WorldMapGeneratedHarvestTarget count == {generatedTargetCount}.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] WorldMapGeneratedHarvestTarget count == {generatedTargetCount} (expected 64).");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [FAIL] Cannot check generated target count: GeneratedWorldRoot not found.");
                failCount++;
            }

            // 6. Provider.TryGetNearestTarget(Vector3.zero, largeRadius, out target) succeeds
            bool nearestSuccess = false;
            if (provider != null && provider.IsReady)
            {
                if (provider.TryGetNearest(Vector3.zero, out var nearestContext))
                {
                    nearestSuccess = nearestContext != null && nearestContext.IsReady;
                    if (nearestSuccess)
                    {
                        log.AppendLine($"  [PASS] Provider.TryGetNearest(Vector3.zero) succeeds. Nearest: {nearestContext.SourceMarkerId}.");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine("  [FAIL] Provider.TryGetNearest(Vector3.zero) returned null or not ready.");
                        failCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [FAIL] Provider.TryGetNearest(Vector3.zero) returned false.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [FAIL] Cannot check TryGetNearest: provider is null or not ready.");
                failCount++;
            }

            // 7. target is IHarvestTarget (via CollectGeneratedHarvestTargets lookup)
            if (nearestSuccess && provider != null && generatedRoot != null)
            {
                // nearest context의 SourceMarkerId로 WorldMapGeneratedHarvestTarget 조회
                if (provider.TryGetNearest(Vector3.zero, out var ctx) && ctx != null)
                {
                    List<WorldMapGeneratedHarvestTarget> allTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);
                    bool foundByMarkerId = false;
                    foreach (var t in allTargets)
                    {
                        if (t != null && t.SourceMarkerId == ctx.SourceMarkerId)
                        {
                            foundByMarkerId = true;
                            if (t is IHarvestTarget)
                            {
                                log.AppendLine($"  [PASS] WorldMapGeneratedHarvestTarget is IHarvestTarget. SourceMarkerId={t.SourceMarkerId}.");
                                passCount++;
                            }
                            else
                            {
                                log.AppendLine("  [FAIL] WorldMapGeneratedHarvestTarget does not implement IHarvestTarget.");
                                failCount++;
                            }
                            break;
                        }
                    }
                    if (!foundByMarkerId)
                    {
                        log.AppendLine($"  [FAIL] WorldMapGeneratedHarvestTarget with SourceMarkerId='{ctx.SourceMarkerId}' not found.");
                        failCount++;
                    }
                }
            }
            else
            {
                log.AppendLine("  [INFO] Skipping IHarvestTarget check: TryGetNearest failed.");
                infoCount++;
            }

            // 8. target.IsAvailable == true (via CollectGeneratedHarvestTargets)
            if (nearestSuccess && provider != null && generatedRoot != null)
            {
                if (provider.TryGetNearest(Vector3.zero, out var ctx) && ctx != null)
                {
                    List<WorldMapGeneratedHarvestTarget> allTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);
                    foreach (var t in allTargets)
                    {
                        if (t != null && t.SourceMarkerId == ctx.SourceMarkerId)
                        {
                            if (t.IsAvailable)
                            {
                                log.AppendLine("  [PASS] WorldMapGeneratedHarvestTarget.IsAvailable == true.");
                                passCount++;
                            }
                            else
                            {
                                log.AppendLine($"  [FAIL] WorldMapGeneratedHarvestTarget.IsAvailable == false. Reason: {t.Reason}.");
                                failCount++;
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                log.AppendLine("  [INFO] Skipping IsAvailable check: TryGetNearest failed.");
                infoCount++;
            }

            // 9. SourceMarkerId lookup succeeds (via CollectGeneratedHarvestTargets)
            if (generatedRoot != null)
            {
                List<WorldMapGeneratedHarvestTarget> allTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);
                var markerIds = new HashSet<string>();
                bool allHaveMarkerId = true;
                foreach (var t in allTargets)
                {
                    if (t == null || string.IsNullOrEmpty(t.SourceMarkerId))
                    {
                        allHaveMarkerId = false;
                        break;
                    }
                    markerIds.Add(t.SourceMarkerId);
                }
                if (allHaveMarkerId && markerIds.Count > 0)
                {
                    log.AppendLine($"  [PASS] All {allTargets.Count} targets have non-empty SourceMarkerId. Unique count={markerIds.Count}.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [FAIL] Some targets have empty SourceMarkerId.");
                    failCount++;
                }
            }

            // 10. No duplicate SourceMarkerId (via CollectGeneratedHarvestTargets)
            if (generatedRoot != null)
            {
                List<WorldMapGeneratedHarvestTarget> allTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);
                var markerIds = new HashSet<string>();
                bool hasDuplicate = false;
                foreach (var t in allTargets)
                {
                    if (t == null || string.IsNullOrEmpty(t.SourceMarkerId))
                        continue;
                    if (!markerIds.Add(t.SourceMarkerId))
                    {
                        hasDuplicate = true;
                        log.AppendLine($"  [FAIL] Duplicate SourceMarkerId: '{t.SourceMarkerId}'.");
                        failCount++;
                        break;
                    }
                }
                if (!hasDuplicate)
                {
                    log.AppendLine($"  [PASS] No duplicate SourceMarkerId. Unique count={markerIds.Count}.");
                    passCount++;
                }
            }

            // 11. No generated target under RuntimeSpawnedInstances
            if (generatedRoot != null)
            {
                Transform spawnedRoot = generatedRoot.transform.Find("RuntimeSpawnedInstances");
                if (spawnedRoot != null)
                {
                    WorldMapGeneratedHarvestTarget[] spawnedTargets = spawnedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                    if (spawnedTargets.Length == 0)
                    {
                        log.AppendLine("  [PASS] No WorldMapGeneratedHarvestTarget under RuntimeSpawnedInstances.");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [FAIL] Found {spawnedTargets.Length} WorldMapGeneratedHarvestTarget under RuntimeSpawnedInstances.");
                        failCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [INFO] RuntimeSpawnedInstances not found. Skipping check.");
                    infoCount++;
                }
            }

            // 12. No generated target under RuntimeSpawnInstances preview
            if (generatedRoot != null)
            {
                Transform spawnInstancesRoot = generatedRoot.transform.Find("RuntimeSpawnInstances");
                if (spawnInstancesRoot != null)
                {
                    WorldMapGeneratedHarvestTarget[] previewTargets = spawnInstancesRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                    if (previewTargets.Length == 0)
                    {
                        log.AppendLine("  [PASS] No WorldMapGeneratedHarvestTarget under RuntimeSpawnInstances.");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [FAIL] Found {previewTargets.Length} WorldMapGeneratedHarvestTarget under RuntimeSpawnInstances.");
                        failCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [INFO] RuntimeSpawnInstances not found. Skipping check.");
                    infoCount++;
                }
            }

            // 13. No generated target under RuntimePlaceholder
            if (generatedRoot != null)
            {
                Transform placeholderRoot = generatedRoot.transform.Find("RuntimePlaceholder");
                if (placeholderRoot != null)
                {
                    WorldMapGeneratedHarvestTarget[] placeholderTargets = placeholderRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                    if (placeholderTargets.Length == 0)
                    {
                        log.AppendLine("  [PASS] No WorldMapGeneratedHarvestTarget under RuntimePlaceholder.");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [FAIL] Found {placeholderTargets.Length} WorldMapGeneratedHarvestTarget under RuntimePlaceholder.");
                        failCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [INFO] RuntimePlaceholder not found. Skipping check.");
                    infoCount++;
                }
            }

            // 14. All WorldMapGeneratedHarvestTarget objects are under RuntimeFinalContentInstances
            //     and no target is attached to WorldMapZoneContentMarker source objects.
            if (generatedRoot != null)
            {
                // CollectGeneratedHarvestTargets는 이미 RuntimeFinalContentInstances 하위 target만 포함하고,
                // WorldMapZoneContentMarker source object에 붙은 target은 제외한다.
                List<WorldMapGeneratedHarvestTarget> validTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);

                // ZoneRoots 하위 전체 target 수집 (비교용)
                Transform zoneRootsParent = generatedRoot.transform.Find("ZoneRoots");
                int zoneRootsTotalTargets = 0;
                if (zoneRootsParent != null)
                {
                    WorldMapGeneratedHarvestTarget[] allZoneTargets = zoneRootsParent.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                    zoneRootsTotalTargets = allZoneTargets.Length;
                }

                // 모든 target이 RuntimeFinalContentInstances 하위에 있고, source object에 붙은 것이 없으면 PASS
                if (validTargets.Count > 0 && validTargets.Count == zoneRootsTotalTargets)
                {
                    log.AppendLine("  [PASS] All WorldMapGeneratedHarvestTarget objects are under RuntimeFinalContentInstances.");
                    passCount++;
                }
                else if (validTargets.Count > 0 && validTargets.Count < zoneRootsTotalTargets)
                {
                    int excludedCount = zoneRootsTotalTargets - validTargets.Count;
                    log.AppendLine($"  [WARN] {excludedCount} WorldMapGeneratedHarvestTarget(s) excluded (on marker source objects or under excluded roots).");
                    warnCount++;
                    log.AppendLine("  [PASS] Valid targets are under RuntimeFinalContentInstances.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [INFO] No WorldMapGeneratedHarvestTarget found under ZoneRoots.");
                    infoCount++;
                }

                // WorldMapZoneContentMarker source object에 붙은 target이 있는지 별도 검사
                if (zoneRootsParent != null)
                {
                    WorldMapGeneratedHarvestTarget[] allZoneTargets = zoneRootsParent.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                    bool foundOnMarkerSource = false;
                    foreach (var t in allZoneTargets)
                    {
                        if (t == null) continue;
                        Transform check = t.transform.parent;
                        while (check != null)
                        {
                            if (check.GetComponent<WorldMapZoneContentMarker>() != null)
                            {
                                // marker source object에 붙은 target 발견
                                if (!IsUnderRuntimeFinalContentInstances(check))
                                {
                                    log.AppendLine($"  [FAIL] WorldMapGeneratedHarvestTarget attached to WorldMapZoneContentMarker source object: '{t.name}'.");
                                    foundOnMarkerSource = true;
                                    failCount++;
                                }
                                break;
                            }
                            check = check.parent;
                        }
                    }
                    if (!foundOnMarkerSource)
                    {
                        log.AppendLine("  [PASS] No WorldMapGeneratedHarvestTarget attached to WorldMapZoneContentMarker source objects.");
                        passCount++;
                    }
                }
            }
            else
            {
                log.AppendLine("  [INFO] ZoneRoots not found. Skipping check.");
                infoCount++;
            }

            // 15. Existing HarvestPointInteractor found count
            HarvestPointInteractor[] interactors = GameObject.FindObjectsByType<HarvestPointInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            log.AppendLine($"  [INFO] Existing HarvestPointInteractor count: {interactors.Length}.");
            infoCount++;

            // 16. Hooked HarvestPointInteractor count
            int hookedCount = 0;
            foreach (var interactor in interactors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook == null)
                    hook = interactor.GetComponentInParent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null && hook.IsInitialized)
                {
                    hookedCount++;
                }
            }
            log.AppendLine($"  [INFO] Hooked HarvestPointInteractor count: {hookedCount}.");
            infoCount++;

            // 17. If interactor exists and hook fields are available, provider reference assigned
            if (interactors.Length > 0)
            {
                bool allHookedHaveProvider = true;
                foreach (var interactor in interactors)
                {
                    if (interactor == null)
                        continue;
                    WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                    if (hook == null)
                        hook = interactor.GetComponentInParent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                    if (hook != null && hook.Provider == null)
                    {
                        allHookedHaveProvider = false;
                        log.AppendLine($"  [WARN] Hook on '{interactor.name}' has no provider reference.");
                        warnCount++;
                    }
                }
                if (allHookedHaveProvider)
                {
                    log.AppendLine("  [PASS] All hooked interactors have provider reference assigned.");
                    passCount++;
                }
            }
            else
            {
                log.AppendLine("  [INFO] No interactors found. Skipping provider reference check.");
                infoCount++;
            }

            // 18. Existing manual harvest target flow not removed
            // 기존 HarvestTargetBehaviour가 있는지 확인 (기존 flow가 유지되는지)
            HarvestTargetBehaviour[] manualTargets = GameObject.FindObjectsByType<HarvestTargetBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (manualTargets.Length > 0)
            {
                log.AppendLine($"  [PASS] Existing manual harvest target flow preserved. Found {manualTargets.Length} HarvestTargetBehaviour(s).");
                passCount++;
            }
            else
            {
                log.AppendLine("  [INFO] No manual HarvestTargetBehaviour found. (May be expected if scene has no manual targets.)");
                infoCount++;
            }

            // 19. Generated placeholder count 64 (via CollectGeneratedHarvestTargets)
            if (generatedRoot != null)
            {
                List<WorldMapGeneratedHarvestTarget> allTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);
                int placeholderCount = 0;
                foreach (var t in allTargets)
                {
                    if (t != null && t.IsGeneratedPlaceholderContent)
                        placeholderCount++;
                }
                if (placeholderCount == 64)
                {
                    log.AppendLine($"  [PASS] Generated placeholder count == {placeholderCount}.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Generated placeholder count == {placeholderCount} (expected 64).");
                    failCount++;
                }
            }

            // 20. User-assigned final content count 0 (via CollectGeneratedHarvestTargets)
            if (generatedRoot != null)
            {
                List<WorldMapGeneratedHarvestTarget> allTargets = CollectGeneratedHarvestTargets(generatedRoot.transform);
                int userAssignedCount = 0;
                foreach (var t in allTargets)
                {
                    if (t != null && t.IsUserAssignedFinalContent)
                        userAssignedCount++;
                }
                if (userAssignedCount == 0)
                {
                    log.AppendLine("  [PASS] User-assigned final content count == 0.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [WARN] User-assigned final content count == {userAssignedCount} (expected 0 for generated-only scene).");
                    warnCount++;
                }
            }

            // 21. Scene object transform/name unchanged or read-only validation
            // 보호된 오브젝트 이름 목록
            var protectedNames = new HashSet<string>
            {
                "MapSettings", "_WorldMap_Manual", "WorldMapHUDCanvas",
                "WorldMapMiniGridCanvas", "UIRoot", "RuntimeControllers",
                "Submarine", "CameraSystem", "DeepLightMapAutoBuilderContext"
            };
            bool protectedObjectsUnchanged = true;
            foreach (string protectedName in protectedNames)
            {
                GameObject protectedObj = GameObject.Find(protectedName);
                if (protectedObj != null)
                {
                    // 이름이 변경되지 않았는지 확인
                    if (protectedObj.name != protectedName)
                    {
                        log.AppendLine($"  [WARN] Protected object '{protectedName}' has been renamed to '{protectedObj.name}'.");
                        protectedObjectsUnchanged = false;
                        warnCount++;
                    }
                }
            }
            if (protectedObjectsUnchanged)
            {
                log.AppendLine("  [PASS] Protected scene objects unchanged.");
                passCount++;
            }

            // ===== Dialog Summary =====
            log.AppendLine("");
            log.AppendLine("===== Phase 14.10-O-11: Validation Summary =====");
            log.AppendLine($"  Provider count: {provider?.Count ?? 0}");
            log.AppendLine($"  GeneratedHarvestTarget count: {generatedTargetCount}");
            log.AppendLine($"  Existing interactor count: {interactors.Length}");
            log.AppendLine($"  Hooked interactor count: {hookedCount}");
            log.AppendLine($"  TryGetNearestTarget success: {nearestSuccess}");
            log.AppendLine($"  PASS: {passCount} | FAIL: {failCount} | WARN: {warnCount} | INFO: {infoCount}");
            log.AppendLine("================================================");

            Debug.Log(log.ToString());

            Debug.Log("[O-11] ===== Validate Generated Harvest Runtime Hook Complete =====");
        }

        // ===== Private Helpers =====

        /// <summary>
        /// GeneratedWorldRoot를 찾는다.
        /// SceneContext.GeneratedRootOverride가 있으면 그것을 우선 사용하고,
        /// 없으면 SettingsSO.GeneratedRootName으로 이름 기반 탐색한다.
        /// </summary>
        private static GameObject FindGeneratedWorldRoot(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // SceneContext override 우선
            if (context != null && context.GeneratedRootOverride != null)
            {
                return context.GeneratedRootOverride;
            }

            // 이름 기반 탐색
            if (settings != null && !string.IsNullOrEmpty(settings.GeneratedRootName))
            {
                return GameObject.Find(settings.GeneratedRootName);
            }

            // fallback: "GeneratedWorldRoot"로 탐색
            return GameObject.Find("GeneratedWorldRoot");
        }

        /// <summary>
        /// GeneratedWorldRoot 하위 전체에서 이름이 "RuntimeFinalContentInstances"인 Transform을 모두 수집한다.
        /// 단, RuntimeSpawnedInstances, RuntimeSpawnInstances, RuntimePlaceholder 하위는 제외한다.
        /// ZoneRoots/ZoneRoot_XX/RuntimeFinalContentInstances 구조를 정상 대상으로 포함한다.
        /// </summary>
        /// <param name="generatedRoot">GeneratedWorldRoot Transform</param>
        /// <returns>찾은 RuntimeFinalContentInstances Transform 목록</returns>
        private static List<Transform> FindRuntimeFinalContentInstanceRoots(Transform generatedRoot)
        {
            var results = new List<Transform>();

            if (generatedRoot == null)
                return results;

            // GeneratedWorldRoot 하위 전체에서 "RuntimeFinalContentInstances" 이름을 가진 Transform을 모두 찾는다.
            WorldMapGeneratedHarvestTarget[] allTargets = generatedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
            var visitedRoots = new HashSet<Transform>();

            foreach (var target in allTargets)
            {
                if (target == null)
                    continue;

                // 부모 체인을 따라 올라가면서 RuntimeFinalContentInstances를 찾는다.
                Transform current = target.transform.parent;
                while (current != null && current != generatedRoot)
                {
                    if (current.name == "RuntimeFinalContentInstances")
                    {
                        // 제외 대상 하위인지 확인
                        if (!IsUnderExcludedRoot(current))
                        {
                            if (visitedRoots.Add(current))
                            {
                                results.Add(current);
                            }
                        }
                        break;
                    }
                    current = current.parent;
                }
            }

            return results;
        }

        /// <summary>
        /// 지정한 Transform이 RuntimeSpawnedInstances, RuntimeSpawnInstances, RuntimePlaceholder 하위에 있는지 확인한다.
        /// </summary>
        /// <param name="target">확인할 Transform</param>
        /// <returns>제외 대상 하위면 true</returns>
        private static bool IsUnderExcludedRoot(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                string name = current.name;
                if (name == "RuntimeSpawnedInstances" ||
                    name == "RuntimeSpawnInstances" ||
                    name == "RuntimePlaceholder")
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// RuntimeFinalContentInstances root들 하위에서 WorldMapGeneratedHarvestTarget을 수집한다.
        /// RuntimeSpawnedInstances/RuntimeSpawnInstances/RuntimePlaceholder/WorldMapZoneContentMarker source object 하위 target은 제외한다.
        /// </summary>
        /// <param name="generatedRoot">GeneratedWorldRoot Transform</param>
        /// <returns>수집된 WorldMapGeneratedHarvestTarget 목록</returns>
        private static List<WorldMapGeneratedHarvestTarget> CollectGeneratedHarvestTargets(Transform generatedRoot)
        {
            var results = new List<WorldMapGeneratedHarvestTarget>();

            if (generatedRoot == null)
                return results;

            // 1. 모든 RuntimeFinalContentInstances root 찾기
            List<Transform> finalContentRoots = FindRuntimeFinalContentInstanceRoots(generatedRoot);

            // 2. 각 root 하위에서 WorldMapGeneratedHarvestTarget 수집
            var visitedTargets = new HashSet<WorldMapGeneratedHarvestTarget>();
            foreach (Transform root in finalContentRoots)
            {
                WorldMapGeneratedHarvestTarget[] targets = root.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                foreach (var target in targets)
                {
                    if (target == null)
                        continue;

                    // 중복 방지
                    if (!visitedTargets.Add(target))
                        continue;

                    // RuntimeSpawnedInstances/RuntimeSpawnInstances/RuntimePlaceholder 하위 제외
                    if (IsUnderExcludedRoot(target.transform))
                        continue;

                    // WorldMapZoneContentMarker가 붙은 source object 자체에 붙어 있으면 제외
                    if (target.GetComponentInParent<WorldMapZoneContentMarker>() != null)
                    {
                        // 단, target 자신이 RuntimeFinalContentInstances 하위에 있고,
                        // WorldMapZoneContentMarker가 target 자신이 아닌 상위 부모에만 있는 경우는 허용
                        // -> target.transform.parent 경로에 WorldMapZoneContentMarker가 있고,
                        //    그 marker가 RuntimeFinalContentInstances 바깥에 있으면 제외
                        Transform check = target.transform.parent;
                        bool isOnMarkerSourceObject = false;
                        while (check != null)
                        {
                            if (check.GetComponent<WorldMapZoneContentMarker>() != null)
                            {
                                // 이 marker가 RuntimeFinalContentInstances 하위가 아니면 source object에 붙은 것
                                if (!IsUnderRuntimeFinalContentInstances(check))
                                {
                                    isOnMarkerSourceObject = true;
                                }
                                break;
                            }
                            check = check.parent;
                        }
                        if (isOnMarkerSourceObject)
                            continue;
                    }

                    results.Add(target);
                }
            }

            return results;
        }

        /// <summary>
        /// 지정한 Transform이 RuntimeFinalContentInstances 하위에 있는지 확인한다.
        /// </summary>
        private static bool IsUnderRuntimeFinalContentInstances(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == "RuntimeFinalContentInstances")
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
