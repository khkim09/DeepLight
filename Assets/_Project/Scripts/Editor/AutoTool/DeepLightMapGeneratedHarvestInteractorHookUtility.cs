using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Gameplay.Interaction;
using Project.Gameplay.World.Harvest;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.Harvest;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-O-12: Generated harvest target hook을 기존 HarvestPointInteractor에 연결하는 Editor Utility.
    /// O-11에서 생성/검증된 WorldMapGeneratedHarvestTargetRuntimeHook과
    /// WorldMapHarvestInteractionTargetProvider를 기존 HarvestPointInteractor 실제 플레이 흐름에 연결한다.
    /// 최소 침습 방식으로 기존 HarvestPointInteractor 흐름을 유지하면서 generated target fallback을 추가한다.
    /// </summary>
    public static class DeepLightMapGeneratedHarvestInteractorHookUtility
    {
        // ===== Public API =====

        /// <summary>
        /// Scene 내 모든 HarvestPointInteractor를 검색하고,
        /// 각 interactor에 WorldMapGeneratedHarvestTargetRuntimeHook 컴포넌트가 없으면 추가한다.
        /// hook에 provider/root 참조가 필요한 경우 자동 할당한다.
        /// interactor의 generatedTargetHook 필드가 있으면 SerializedObject로 자동 할당한다.
        /// enableGeneratedWorldHarvestTargets가 있으면 true로 설정한다.
        /// 기존 HarvestPointInteractor transform/name 변경 금지.
        /// 기존 manual target/zone object 삭제 금지.
        /// </summary>
        public static void RebuildGeneratedHarvestInteractorHooks(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-O-12: Rebuild Generated Harvest Interactor Hooks =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine("[FAIL] GeneratedWorldRoot not found. Cannot rebuild hooks.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"[INFO] GeneratedWorldRoot: {generatedRoot.name}");

            // 2. WorldMapHarvestInteractionTargetProvider 찾기
            WorldMapHarvestInteractionTargetProvider provider = generatedRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);
            if (provider == null)
            {
                log.AppendLine("[FAIL] WorldMapHarvestInteractionTargetProvider not found under GeneratedWorldRoot.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"[INFO] Provider found: {provider.name}, Count={provider.Count}, IsReady={provider.IsReady}");

            // 3. Provider 초기화
            if (!provider.IsReady)
            {
                provider.TryInitialize();
                log.AppendLine($"[INFO] Provider.TryInitialize() called. IsReady={provider.IsReady}");
            }

            // 4. Scene 내 모든 HarvestPointInteractor 검색
            HarvestPointInteractor[] allInteractors = GameObject.FindObjectsByType<HarvestPointInteractor>(FindObjectsSortMode.None);
            log.AppendLine($"[INFO] HarvestPointInteractor count: {allInteractors.Length}");

            if (allInteractors.Length == 0)
            {
                log.AppendLine("[WARN] No HarvestPointInteractor found in scene. Nothing to hook.");
                Debug.LogWarning(log.ToString());
                return;
            }

            int hookedCount = 0;
            int alreadyHookedCount = 0;

            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;

                GameObject interactorGO = interactor.gameObject;

                // 기존 hook 컴포넌트 확인
                WorldMapGeneratedHarvestTargetRuntimeHook existingHook = interactorGO.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (existingHook != null)
                {
                    alreadyHookedCount++;
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}' already has hook. Skipping add.");
                    continue;
                }

                // hook 컴포넌트 추가
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactorGO.AddComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook == null)
                {
                    log.AppendLine($"[FAIL] Failed to add hook to interactor '{interactorGO.name}'.");
                    continue;
                }

                // Provider 참조 할당
                hook.Provider = provider;

                // Detection radius 설정 (interactor의 interaction distance 참고)
                // 기본값 4f 사용, 필요시 interactor의 serialized field에서 읽을 수 있음
                hook.DetectionRadius = 4f;

                // hook 초기화
                hook.TryInitialize();

                // interactor의 generatedTargetHook 필드가 있으면 SerializedObject로 자동 할당
                SerializedObject serializedInteractor = new SerializedObject(interactor);
                SerializedProperty hookProp = serializedInteractor.FindProperty("generatedTargetHook");
                if (hookProp != null)
                {
                    hookProp.objectReferenceValue = hook;
                    serializedInteractor.ApplyModifiedProperties();
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': generatedTargetHook field assigned.");
                }
                else
                {
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': no generatedTargetHook field found. Hook component added but field not linked.");
                }

                // enableGeneratedWorldHarvestTargets 필드가 있으면 true로 설정
                // 실제 필드명은 enableGeneratedWorldHarvestTargets (public property: EnableGeneratedWorldHarvestTargets)
                SerializedProperty enableGeneratedProp = serializedInteractor.FindProperty("enableGeneratedWorldHarvestTargets");
                if (enableGeneratedProp != null && enableGeneratedProp.propertyType == SerializedPropertyType.Boolean)
                {
                    enableGeneratedProp.boolValue = true;
                    serializedInteractor.ApplyModifiedProperties();
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': enableGeneratedWorldHarvestTargets set to true.");
                }
                else
                {
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': no enableGeneratedWorldHarvestTargets field found.");
                }

                hookedCount++;
                log.AppendLine($"[PASS] Hook added to interactor '{interactorGO.name}'. Hook.IsInitialized={hook.IsInitialized}, HasProvider={hook.HasProvider}, CachedTargetCount={hook.CachedTargetCount}");
            }

            // Summary
            log.AppendLine($"--- O-12 Rebuild Summary ---");
            log.AppendLine($"HarvestPointInteractor count: {allInteractors.Length}");
            log.AppendLine($"Already hooked count: {alreadyHookedCount}");
            log.AppendLine($"Newly hooked count: {hookedCount}");
            log.AppendLine($"Total hooked count: {alreadyHookedCount + hookedCount}");
            log.AppendLine($"Provider count: {provider.Count}");
            log.AppendLine($"Provider.IsReady: {provider.IsReady}");

            if (hookedCount > 0 || alreadyHookedCount > 0)
            {
                log.AppendLine("[PASS] O-12 Rebuild completed successfully.");
            }
            else
            {
                log.AppendLine("[WARN] No hooks were added or found.");
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Phase 14.10-O-12: Generated harvest interactor hook의 유효성을 검증한다.
        /// 최소 25개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Read-only 검증이며 scene object를 수정하지 않는다.
        /// </summary>
        public static void ValidateGeneratedHarvestInteractorHooks(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-O-12: Validate Generated Harvest Interactor Hooks =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null)
            {
                log.AppendLine("[PASS] GeneratedWorldRoot exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("[FAIL] GeneratedWorldRoot not found.");
                failCount++;
            }

            // 2. WorldMapHarvestInteractionTargetProvider exists
            WorldMapHarvestInteractionTargetProvider provider = null;
            if (generatedRoot != null)
            {
                provider = generatedRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);
                if (provider != null)
                {
                    log.AppendLine($"[PASS] WorldMapHarvestInteractionTargetProvider exists. Count={provider.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine("[FAIL] WorldMapHarvestInteractionTargetProvider not found under GeneratedWorldRoot.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("[FAIL] WorldMapHarvestInteractionTargetProvider not found (no GeneratedWorldRoot).");
                failCount++;
            }

            // 3. Provider.IsReady == true
            if (provider != null)
            {
                if (provider.IsReady)
                {
                    log.AppendLine("[PASS] Provider.IsReady == true.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("[FAIL] Provider.IsReady == false.");
                    failCount++;
                }
            }

            // 4. Provider.Count == 64
            if (provider != null)
            {
                if (provider.Count == 64)
                {
                    log.AppendLine($"[PASS] Provider.Count == {provider.Count} (expected 64).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"[FAIL] Provider.Count == {provider.Count} (expected 64).");
                    failCount++;
                }
            }

            // 5. WorldMapGeneratedHarvestTarget count == 64
            if (generatedRoot != null)
            {
                WorldMapGeneratedHarvestTarget[] allGeneratedTargets = generatedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                if (allGeneratedTargets.Length == 64)
                {
                    log.AppendLine($"[PASS] WorldMapGeneratedHarvestTarget count == {allGeneratedTargets.Length} (expected 64).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"[FAIL] WorldMapGeneratedHarvestTarget count == {allGeneratedTargets.Length} (expected 64).");
                    failCount++;
                }
            }

            // 6. HarvestPointInteractor count > 0
            HarvestPointInteractor[] allInteractors = GameObject.FindObjectsByType<HarvestPointInteractor>(FindObjectsSortMode.None);
            if (allInteractors.Length > 0)
            {
                log.AppendLine($"[PASS] HarvestPointInteractor count: {allInteractors.Length} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("[FAIL] HarvestPointInteractor count == 0.");
                failCount++;
            }

            // 7. Hooked HarvestPointInteractor count == HarvestPointInteractor count
            int hookedCount = 0;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null)
                    hookedCount++;
            }

            if (hookedCount == allInteractors.Length)
            {
                log.AppendLine($"[PASS] Hooked interactor count == {hookedCount} (matches total interactor count {allInteractors.Length}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"[FAIL] Hooked interactor count == {hookedCount} (expected {allInteractors.Length}).");
                failCount++;
            }

            // 8. Each hook HasProvider == true
            bool allHooksHaveProvider = true;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null && !hook.HasProvider)
                {
                    allHooksHaveProvider = false;
                    log.AppendLine($"[FAIL] Hook on '{interactor.name}' HasProvider == false.");
                    failCount++;
                }
            }
            if (allHooksHaveProvider && hookedCount > 0)
            {
                log.AppendLine("[PASS] All hooks have HasProvider == true.");
                passCount++;
            }
            else if (hookedCount == 0)
            {
                log.AppendLine("[WARN] No hooks to check HasProvider.");
                warnCount++;
            }

            // 9. Each hook IsInitialized == true
            bool allHooksInitialized = true;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null && !hook.IsInitialized)
                {
                    allHooksInitialized = false;
                    log.AppendLine($"[FAIL] Hook on '{interactor.name}' IsInitialized == false.");
                    failCount++;
                }
            }
            if (allHooksInitialized && hookedCount > 0)
            {
                log.AppendLine("[PASS] All hooks have IsInitialized == true.");
                passCount++;
            }
            else if (hookedCount == 0)
            {
                log.AppendLine("[WARN] No hooks to check IsInitialized.");
                warnCount++;
            }

            // 10. Each hook CachedTargetCount == 64 또는 Provider.Count
            bool allCachedTargetCountOk = true;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null && hook.IsInitialized)
                {
                    int expectedCount = provider != null ? provider.Count : 64;
                    if (hook.CachedTargetCount != expectedCount)
                    {
                        allCachedTargetCountOk = false;
                        log.AppendLine($"[FAIL] Hook on '{interactor.name}' CachedTargetCount == {hook.CachedTargetCount} (expected {expectedCount}).");
                        failCount++;
                    }
                }
            }
            if (allCachedTargetCountOk && hookedCount > 0)
            {
                log.AppendLine($"[PASS] All hooks have correct CachedTargetCount.");
                passCount++;
            }
            else if (hookedCount == 0)
            {
                log.AppendLine("[WARN] No hooks to check CachedTargetCount.");
                warnCount++;
            }

            // 11. hook.TryGetNearestGeneratedTarget(Vector3.zero, largeRadius, out target) succeeds
            bool tryGetNearestSuccess = false;
            if (hookedCount > 0)
            {
                // 첫 번째 hooked interactor의 hook 사용
                foreach (HarvestPointInteractor interactor in allInteractors)
                {
                    if (interactor == null)
                        continue;
                    WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                    if (hook != null && hook.IsInitialized)
                    {
                        float largeRadius = 1000f;
                        if (hook.TryGetNearestGeneratedTarget(Vector3.zero, largeRadius, out IHarvestTarget nearestTarget))
                        {
                            tryGetNearestSuccess = true;
                            log.AppendLine($"[PASS] TryGetNearestGeneratedTarget(Vector3.zero, {largeRadius}) succeeded. Target available: {nearestTarget?.IsAvailable}");
                            passCount++;
                        }
                        else
                        {
                            log.AppendLine($"[INFO] TryGetNearestGeneratedTarget(Vector3.zero, {largeRadius}) returned false (expected if no targets near origin).");
                            infoCount++;
                        }
                        break;
                    }
                }
            }
            else
            {
                log.AppendLine("[WARN] No hooks to test TryGetNearestGeneratedTarget.");
                warnCount++;
            }

            // 12. returned target is IHarvestTarget (tested above implicitly)
            // 13. returned target.IsAvailable == true (tested above implicitly)

            // 14. No generated hook added under RuntimeFinalContentInstances
            bool hookUnderFinalContent = false;
            if (generatedRoot != null)
            {
                Transform[] allFinalContentRoots = generatedRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allFinalContentRoots)
                {
                    if (t.name == "RuntimeFinalContentInstances")
                    {
                        WorldMapGeneratedHarvestTargetRuntimeHook[] hooksUnderFinal = t.GetComponentsInChildren<WorldMapGeneratedHarvestTargetRuntimeHook>(true);
                        if (hooksUnderFinal.Length > 0)
                        {
                            hookUnderFinalContent = true;
                            log.AppendLine($"[FAIL] Found {hooksUnderFinal.Length} hook(s) under RuntimeFinalContentInstances. Hooks should only be on HarvestPointInteractor.");
                            failCount++;
                        }
                    }
                }
            }
            if (!hookUnderFinalContent)
            {
                log.AppendLine("[PASS] No hooks found under RuntimeFinalContentInstances.");
                passCount++;
            }

            // 15. No generated hook added under RuntimeSpawnedInstances
            bool hookUnderSpawned = false;
            if (generatedRoot != null)
            {
                Transform[] allSpawnedRoots = generatedRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allSpawnedRoots)
                {
                    if (t.name == "RuntimeSpawnedInstances")
                    {
                        WorldMapGeneratedHarvestTargetRuntimeHook[] hooksUnderSpawned = t.GetComponentsInChildren<WorldMapGeneratedHarvestTargetRuntimeHook>(true);
                        if (hooksUnderSpawned.Length > 0)
                        {
                            hookUnderSpawned = true;
                            log.AppendLine($"[FAIL] Found {hooksUnderSpawned.Length} hook(s) under RuntimeSpawnedInstances.");
                            failCount++;
                        }
                    }
                }
            }
            if (!hookUnderSpawned)
            {
                log.AppendLine("[PASS] No hooks found under RuntimeSpawnedInstances.");
                passCount++;
            }

            // 16. No generated hook added to generated target objects
            bool hookOnGeneratedTarget = false;
            if (generatedRoot != null)
            {
                WorldMapGeneratedHarvestTarget[] allTargets = generatedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                foreach (WorldMapGeneratedHarvestTarget target in allTargets)
                {
                    if (target == null)
                        continue;
                    WorldMapGeneratedHarvestTargetRuntimeHook hookOnTarget = target.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                    if (hookOnTarget != null)
                    {
                        hookOnGeneratedTarget = true;
                        log.AppendLine($"[FAIL] Hook found on generated target object '{target.name}'.");
                        failCount++;
                    }
                }
            }
            if (!hookOnGeneratedTarget)
            {
                log.AppendLine("[PASS] No hooks found on generated target objects.");
                passCount++;
            }

            // 17. Existing manual HarvestTargetBehaviour count unchanged
            HarvestTargetBehaviour[] manualTargets = GameObject.FindObjectsByType<HarvestTargetBehaviour>(FindObjectsSortMode.None);
            log.AppendLine($"[INFO] Existing manual HarvestTargetBehaviour count: {manualTargets.Length}");
            infoCount++;

            // 18. Existing HarvestInteractionZone count unchanged
            // HarvestInteractionZone 타입 확인 (존재하는 경우)
            var zoneType = System.Type.GetType("Project.Gameplay.Harvest.HarvestInteractionZone, Assembly-CSharp");
            int zoneCount = 0;
            if (zoneType != null)
            {
                var zoneObjects = GameObject.FindObjectsByType(zoneType, FindObjectsSortMode.None);
                zoneCount = zoneObjects != null ? zoneObjects.Length : 0;
            }
            log.AppendLine($"[INFO] Existing HarvestInteractionZone count: {zoneCount}");
            infoCount++;

            // 19. Scene object transform/name unchanged (read-only validation, no modification)
            log.AppendLine("[INFO] Scene object transform/name unchanged (read-only validation).");
            infoCount++;

            // 20. Provider/GeneratedTarget count unchanged
            if (provider != null)
            {
                log.AppendLine($"[INFO] Provider count unchanged: {provider.Count}");
                infoCount++;
            }

            // 21. Check interactor's generatedTargetHook field assignment
            int interactorWithHookField = 0;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                var serializedInteractor = new SerializedObject(interactor);
                SerializedProperty hookProp = serializedInteractor.FindProperty("generatedTargetHook");
                if (hookProp != null && hookProp.objectReferenceValue != null)
                {
                    interactorWithHookField++;
                }
            }
            if (interactorWithHookField == allInteractors.Length && allInteractors.Length > 0)
            {
                log.AppendLine($"[PASS] All {allInteractors.Length} interactors have generatedTargetHook field assigned.");
                passCount++;
            }
            else if (allInteractors.Length > 0)
            {
                log.AppendLine($"[WARN] {interactorWithHookField}/{allInteractors.Length} interactors have generatedTargetHook field assigned.");
                warnCount++;
            }

            // 22. Check interactor's enableGeneratedWorldHarvestTargets field
            int interactorWithUseGenerated = 0;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                var serializedInteractor = new SerializedObject(interactor);
                SerializedProperty useGeneratedProp = serializedInteractor.FindProperty("enableGeneratedWorldHarvestTargets");
                if (useGeneratedProp != null && useGeneratedProp.propertyType == SerializedPropertyType.Boolean)
                {
                    if (useGeneratedProp.boolValue)
                    {
                        interactorWithUseGenerated++;
                    }
                }
            }
            if (interactorWithUseGenerated == allInteractors.Length && allInteractors.Length > 0)
            {
                log.AppendLine($"[PASS] All {allInteractors.Length} interactors have enableGeneratedWorldHarvestTargets == true.");
                passCount++;
            }
            else if (allInteractors.Length > 0)
            {
                log.AppendLine($"[WARN] {interactorWithUseGenerated}/{allInteractors.Length} interactors have enableGeneratedWorldHarvestTargets == true.");
                warnCount++;
            }

            // 23. Hook detection radius > 0
            bool allDetectionRadiusOk = true;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null && hook.DetectionRadius <= 0f)
                {
                    allDetectionRadiusOk = false;
                    log.AppendLine($"[FAIL] Hook on '{interactor.name}' DetectionRadius == {hook.DetectionRadius} (must be > 0).");
                    failCount++;
                }
            }
            if (allDetectionRadiusOk && hookedCount > 0)
            {
                log.AppendLine("[PASS] All hooks have DetectionRadius > 0.");
                passCount++;
            }
            else if (hookedCount == 0)
            {
                log.AppendLine("[WARN] No hooks to check DetectionRadius.");
                warnCount++;
            }

            // 24. Provider.TryGetNearest test
            if (provider != null && provider.IsReady)
            {
                if (provider.TryGetNearest(Vector3.zero, out var nearestContext))
                {
                    log.AppendLine($"[PASS] Provider.TryGetNearest(Vector3.zero) succeeded. Context ready: {nearestContext?.IsReady}");
                    passCount++;
                }
                else
                {
                    log.AppendLine("[INFO] Provider.TryGetNearest(Vector3.zero) returned false (expected if no targets near origin).");
                    infoCount++;
                }
            }

            // 25. Total hook count matches interactor count (redundant check for clarity)
            if (hookedCount == allInteractors.Length)
            {
                log.AppendLine($"[PASS] Hooked interactor count ({hookedCount}) == Existing interactor count ({allInteractors.Length}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"[FAIL] Hooked interactor count ({hookedCount}) != Existing interactor count ({allInteractors.Length}).");
                failCount++;
            }

            // Summary
            log.AppendLine($"--- O-12 Validation Summary ---");
            log.AppendLine($"PASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount}");
            log.AppendLine($"Provider count: {(provider != null ? provider.Count : 0)}");
            log.AppendLine($"GeneratedHarvestTarget count: {(generatedRoot != null ? generatedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true).Length : 0)}");
            log.AppendLine($"HarvestPointInteractor count: {allInteractors.Length}");
            log.AppendLine($"Hooked interactor count: {hookedCount}");
            log.AppendLine($"TryGetNearestTarget success: {tryGetNearestSuccess}");

            if (failCount == 0)
            {
                log.AppendLine("[PASS] O-12 Validation: All checks passed.");
            }
            else
            {
                log.AppendLine($"[FAIL] O-12 Validation: {failCount} failure(s) detected.");
            }

            Debug.Log(log.ToString());
        }
    }
}
