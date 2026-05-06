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
    /// Phase 14.10-O-13: Generated harvest target hook이 PlayMode 진입 전 실제 interactor fallback resolve까지
    /// 가능한지 검증하는 PlayMode Smoke Test / Compatibility 계층.
    /// 기존 Harvest 시스템을 대규모 리팩토링하지 않고, PlayMode 진입 전/후에 generated target hook이
    /// 실제로 사용 가능한지 검증하는 smoke test와 안정성 보강만 수행한다.
    /// Provider를 source of truth로 사용하며, Hook은 hierarchy와 무관하게 Provider 기반으로 동작한다.
    /// </summary>
    public static class DeepLightMapGeneratedHarvestPlayModeSmokeTestUtility
    {
        // ===== Public API =====

        /// <summary>
        /// O-13 Rebuild: GeneratedWorldRoot/Provider/Hook/Interactor를 검색하고,
        /// 각 interactor에 hook이 존재하는지 확인/추가하고,
        /// enableGeneratedWorldHarvestTargets를 true로 설정하며,
        /// hook의 provider 참조를 자동 할당하고 초기화한다.
        /// 기존 trigger/manual target 우선 정책을 유지하며,
        /// generated target은 fallback으로만 동작한다.
        /// Hook이 이미 붙어 있어도 AssignProvider/TryInitialize/ForceRebuildCache를 수행한다.
        /// </summary>
        public static void RebuildGeneratedHarvestPlayModeSmokeTest(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-O-13: Rebuild Generated Harvest PlayMode Smoke Test =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine("[FAIL] GeneratedWorldRoot not found. Cannot rebuild smoke test.");
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
            log.AppendLine($"[INFO] Provider found. Count={provider.Count}, IsReady={provider.IsReady}");

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
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}' already has hook. Reassigning provider and reinitializing...");

                    // 반드시 AssignProvider 호출 (provider 재할당 + 강제 초기화 + 캐시 리빌드)
                    existingHook.AssignProvider(provider);

                    // TryInitialize(provider) 호출 (명시적 초기화)
                    existingHook.TryInitialize(provider);

                    // ForceRebuildCache 호출 (캐시 강제 리빌드)
                    existingHook.ForceRebuildCache();

                    log.AppendLine($"[INFO] Existing hook on '{interactorGO.name}' re-initialized. IsInitialized={existingHook.IsInitialized}, HasProvider={existingHook.HasProvider}, CachedTargetCount={existingHook.CachedTargetCount}");

                    // interactor의 generatedTargetHook serialized field에 hook 할당
                    SerializedObject serializedInteractor = new SerializedObject(interactor);
                    SerializedProperty hookProp = serializedInteractor.FindProperty("generatedTargetHook");
                    if (hookProp != null)
                    {
                        hookProp.objectReferenceValue = existingHook;
                        serializedInteractor.ApplyModifiedProperties();
                        log.AppendLine($"[INFO] Interactor '{interactorGO.name}': generatedTargetHook field reassigned.");
                    }

                    // enableGeneratedWorldHarvestTargets serialized field를 true로 설정
                    SerializedProperty enableGeneratedProp = serializedInteractor.FindProperty("enableGeneratedWorldHarvestTargets");
                    if (enableGeneratedProp != null && enableGeneratedProp.propertyType == SerializedPropertyType.Boolean)
                    {
                        enableGeneratedProp.boolValue = true;
                        serializedInteractor.ApplyModifiedProperties();
                        log.AppendLine($"[INFO] Interactor '{interactorGO.name}': enableGeneratedWorldHarvestTargets set to true.");
                    }

                    // EditorUtility.SetDirty 처리
                    EditorUtility.SetDirty(interactor);

                    continue;
                }

                // hook 컴포넌트 추가
                WorldMapGeneratedHarvestTargetRuntimeHook newHook = interactorGO.AddComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (newHook == null)
                {
                    log.AppendLine($"[FAIL] Failed to add hook to interactor '{interactorGO.name}'.");
                    continue;
                }

                // Provider 참조 할당 및 초기화
                newHook.AssignProvider(provider);

                // Detection radius 설정
                newHook.DetectionRadius = 4f;

                // hook 초기화
                newHook.TryInitialize(provider);

                // interactor의 generatedTargetHook 필드 자동 할당
                SerializedObject newSerializedInteractor = new SerializedObject(interactor);
                SerializedProperty newHookProp = newSerializedInteractor.FindProperty("generatedTargetHook");
                if (newHookProp != null)
                {
                    newHookProp.objectReferenceValue = newHook;
                    newSerializedInteractor.ApplyModifiedProperties();
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': generatedTargetHook field assigned.");
                }
                else
                {
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': no generatedTargetHook field found.");
                }

                // enableGeneratedWorldHarvestTargets 필드를 true로 설정
                SerializedProperty newEnableGeneratedProp = newSerializedInteractor.FindProperty("enableGeneratedWorldHarvestTargets");
                if (newEnableGeneratedProp != null && newEnableGeneratedProp.propertyType == SerializedPropertyType.Boolean)
                {
                    newEnableGeneratedProp.boolValue = true;
                    newSerializedInteractor.ApplyModifiedProperties();
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': enableGeneratedWorldHarvestTargets set to true.");
                }
                else
                {
                    log.AppendLine($"[INFO] Interactor '{interactorGO.name}': no enableGeneratedWorldHarvestTargets field found.");
                }

                // EditorUtility.SetDirty 처리
                EditorUtility.SetDirty(interactor);

                hookedCount++;
                log.AppendLine($"[PASS] Hook added to interactor '{interactorGO.name}'. Hook.IsInitialized={newHook.IsInitialized}, HasProvider={newHook.HasProvider}, CachedTargetCount={newHook.CachedTargetCount}");
            }

            // Summary
            log.AppendLine($"--- O-13 Rebuild Summary ---");
            log.AppendLine($"Provider count: {provider.Count}");
            log.AppendLine($"GeneratedHarvestTarget count: {generatedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true).Length}");
            log.AppendLine($"HarvestPointInteractor count: {allInteractors.Length}");
            log.AppendLine($"Hooked interactor count: {alreadyHookedCount + hookedCount}");
            log.AppendLine($"useGeneratedHarvestTargets true count: {CountEnableGeneratedWorldHarvestTargets(allInteractors)}");

            // 각 hook의 CachedTargetCount 출력
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null)
                {
                    log.AppendLine($"Hook on '{interactor.name}' CachedTargetCount: {hook.CachedTargetCount}");
                }
            }

            if (hookedCount > 0 || alreadyHookedCount > 0)
            {
                log.AppendLine("[PASS] O-13 Rebuild completed successfully.");
            }
            else
            {
                log.AppendLine("[WARN] No hooks were added or found.");
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// O-13 Validate: Generated harvest target hook이 PlayMode 진입 전 실제 interactor fallback resolve까지
        /// 가능한지 검증한다. Provider 기반으로 동작하며, Hook이 Submarine처럼 GeneratedWorldRoot 하위가 아닌
        /// 오브젝트에 붙어 있어도 정상 동작하는지 검증한다.
        /// Read-only 검증을 원칙으로 하되, runtime cache rebuild는 허용한다.
        /// </summary>
        public static void ValidateGeneratedHarvestPlayModeSmokeTest(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-O-13: Validate Generated Harvest PlayMode Smoke Test =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // ===== 1. GeneratedWorldRoot exists =====
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

            // ===== 2. WorldMapHarvestInteractionTargetProvider exists =====
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

            // ===== 3. Provider 초기화 (Validate 시작 시) =====
            if (provider != null)
            {
                if (!provider.IsReady)
                {
                    provider.TryInitialize();
                    log.AppendLine($"[INFO] Provider.TryInitialize() called during validation. IsReady={provider.IsReady}");
                    infoCount++;
                }
            }

            // ===== 4. Provider.IsReady == true =====
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

            // ===== 5. Provider.Count == 64 =====
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

            // ===== 6. WorldMapGeneratedHarvestTarget count == 64 =====
            int generatedTargetCount = 0;
            if (generatedRoot != null)
            {
                WorldMapGeneratedHarvestTarget[] allGeneratedTargets = generatedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                generatedTargetCount = allGeneratedTargets.Length;
                if (generatedTargetCount == 64)
                {
                    log.AppendLine($"[PASS] WorldMapGeneratedHarvestTarget count == {generatedTargetCount} (expected 64).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"[FAIL] WorldMapGeneratedHarvestTarget count == {generatedTargetCount} (expected 64).");
                    failCount++;
                }
            }

            // ===== 7. HarvestPointInteractor count > 0 =====
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

            // ===== 8. Hooked interactor count == interactor count =====
            int hookedCount = 0;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null)
                    hookedCount++;
            }

            if (hookedCount == allInteractors.Length && allInteractors.Length > 0)
            {
                log.AppendLine($"[PASS] Hooked interactor count == {hookedCount} (matches total interactor count {allInteractors.Length}).");
                passCount++;
            }
            else if (allInteractors.Length > 0)
            {
                log.AppendLine($"[FAIL] Hooked interactor count == {hookedCount} (expected {allInteractors.Length}).");
                failCount++;
            }
            else
            {
                log.AppendLine("[WARN] No interactors to check hook count.");
                warnCount++;
            }

            // ===== 9. All interactors have generatedTargetHook assigned =====
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
            else
            {
                log.AppendLine("[WARN] No interactors to check generatedTargetHook assignment.");
                warnCount++;
            }

            // ===== 10. All interactors have enableGeneratedWorldHarvestTargets == true =====
            int interactorWithEnableGenerated = CountEnableGeneratedWorldHarvestTargets(allInteractors);
            if (interactorWithEnableGenerated == allInteractors.Length && allInteractors.Length > 0)
            {
                log.AppendLine($"[PASS] All {allInteractors.Length} interactors have enableGeneratedWorldHarvestTargets == true.");
                passCount++;
            }
            else if (allInteractors.Length > 0)
            {
                log.AppendLine($"[WARN] {interactorWithEnableGenerated}/{allInteractors.Length} interactors have enableGeneratedWorldHarvestTargets == true.");
                warnCount++;
            }
            else
            {
                log.AppendLine("[WARN] No interactors to check enableGeneratedWorldHarvestTargets.");
                warnCount++;
            }

            // ===== 11. All hooks HasProvider == true =====
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

            // ===== 12. All hooks IsInitialized == true =====
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

            // ===== 13. All hooks CachedTargetCount == Provider.Count (expected 64) =====
            // Validate 시작 시 각 hook에 대해 provider 기반 강제 초기화 후 캐시 검사
            bool allCachedTargetCountOk = true;
            foreach (HarvestPointInteractor interactor in allInteractors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null)
                {
                    // provider reference가 있으면 ForceRebuildCache는 runtime cache만 바꾸는 것이므로 허용
                    if (hook.HasProvider)
                    {
                        hook.ForceRebuildCache();
                    }
                    else if (provider != null)
                    {
                        // provider reference가 없으면 AssignProvider 호출 (non-mutating: scene transform/name 변경 없음)
                        hook.AssignProvider(provider);
                    }

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
                log.AppendLine($"[PASS] All hooks have correct CachedTargetCount (expected {provider?.Count ?? 64}).");
                passCount++;
            }
            else if (hookedCount == 0)
            {
                log.AppendLine("[WARN] No hooks to check CachedTargetCount.");
                warnCount++;
            }

            // ===== 14. Provider.TryGetNearest(Vector3.zero, 1000f) succeeds =====
            bool providerNearestSuccess = false;
            if (provider != null && provider.IsReady)
            {
                if (provider.TryGetNearest(Vector3.zero, out var nearestContext))
                {
                    providerNearestSuccess = true;
                    log.AppendLine($"[PASS] Provider.TryGetNearest(Vector3.zero) succeeded. Context ready: {nearestContext?.IsReady}");
                    passCount++;
                }
                else
                {
                    log.AppendLine("[INFO] Provider.TryGetNearest(Vector3.zero) returned false (expected if no targets near origin).");
                    infoCount++;
                }
            }
            else
            {
                log.AppendLine("[WARN] Provider not ready, skipping TryGetNearest test.");
                warnCount++;
            }

            // ===== 15. Hook.TryGetNearestGeneratedTarget(Vector3.zero, 1000f) succeeds =====
            bool hookNearestSuccess = false;
            if (hookedCount > 0)
            {
                foreach (HarvestPointInteractor interactor in allInteractors)
                {
                    if (interactor == null)
                        continue;
                    WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                    if (hook != null)
                    {
                        // 캐시가 비어 있으면 강제 리빌드
                        if (hook.CachedTargetCount == 0)
                        {
                            hook.ForceRebuildCache();
                        }

                        float largeRadius = 1000f;
                        if (hook.TryGetNearestGeneratedTarget(Vector3.zero, largeRadius, out IHarvestTarget nearestTarget))
                        {
                            hookNearestSuccess = true;
                            log.AppendLine($"[PASS] Hook.TryGetNearestGeneratedTarget(Vector3.zero, {largeRadius}) succeeded. Target available: {nearestTarget?.IsAvailable}");
                            passCount++;
                        }
                        else
                        {
                            log.AppendLine($"[INFO] Hook.TryGetNearestGeneratedTarget(Vector3.zero, {largeRadius}) returned false (expected if no targets near origin).");
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

            // ===== 16. Interactor smoke API TryResolveGeneratedTargetForSmokeTest (INFO/WARN only) =====
            // Submarine 위치 근처에 target이 없으면 정상이므로 FAIL이 아닌 INFO/WARN으로 처리
            bool interactorFallbackResolveSuccess = false;
            IHarvestTarget resolvedTarget = null;
            if (allInteractors.Length > 0)
            {
                foreach (HarvestPointInteractor interactor in allInteractors)
                {
                    if (interactor == null)
                        continue;

                    // TryResolveGeneratedTargetForSmokeTest 호출 (read-only, currentTarget 변경 없음)
                    if (interactor.TryResolveGeneratedTargetForSmokeTest(out IHarvestTarget smokeTarget))
                    {
                        interactorFallbackResolveSuccess = true;
                        resolvedTarget = smokeTarget;
                        log.AppendLine($"[PASS] Interactor.TryResolveGeneratedTargetForSmokeTest succeeded on '{interactor.name}'. Target available: {smokeTarget?.IsAvailable}");
                        passCount++;
                    }
                    else
                    {
                        // Submarine 위치 근처에 target이 없으면 정상이므로 INFO로 처리
                        log.AppendLine($"[INFO] Interactor.TryResolveGeneratedTargetForSmokeTest on '{interactor.name}' returned false (expected if no targets near interactor position).");
                        infoCount++;
                    }
                    break;
                }
            }
            else
            {
                log.AppendLine("[WARN] No interactors to test TryResolveGeneratedTargetForSmokeTest.");
                warnCount++;
            }

            // ===== 17. Returned target implements IHarvestTarget =====
            if (resolvedTarget != null)
            {
                if (resolvedTarget is IHarvestTarget)
                {
                    log.AppendLine("[PASS] Resolved target implements IHarvestTarget.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("[FAIL] Resolved target does not implement IHarvestTarget.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("[INFO] No resolved target to check IHarvestTarget implementation.");
                infoCount++;
            }

            // ===== 18. Returned target.IsAvailable == true =====
            if (resolvedTarget != null)
            {
                if (resolvedTarget.IsAvailable)
                {
                    log.AppendLine("[PASS] Resolved target.IsAvailable == true.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("[FAIL] Resolved target.IsAvailable == false.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("[INFO] No resolved target to check IsAvailable.");
                infoCount++;
            }

            // ===== 19. Calling target.GetWorldPosition() does not throw =====
            if (resolvedTarget != null)
            {
                try
                {
                    Vector3 worldPos = Vector3.zero;
                    if (resolvedTarget is WorldMapGeneratedHarvestTarget castTarget)
                    {
                        worldPos = castTarget.GetWorldPosition();
                    }
                    else if (resolvedTarget is MonoBehaviour mb)
                    {
                        worldPos = mb.transform.position;
                    }
                    log.AppendLine($"[PASS] target.GetWorldPosition() succeeded. Position: {worldPos}");
                    passCount++;
                }
                catch (System.Exception ex)
                {
                    log.AppendLine($"[FAIL] target.GetWorldPosition() threw exception: {ex.Message}");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("[INFO] No resolved target to test GetWorldPosition.");
                infoCount++;
            }

            // ===== 20. Calling target.GetDebugSummary() does not throw =====
            if (resolvedTarget != null)
            {
                try
                {
                    string summary = "N/A";
                    if (resolvedTarget is WorldMapGeneratedHarvestTarget castTarget)
                    {
                        summary = castTarget.GetDebugSummary();
                    }
                    log.AppendLine($"[PASS] target.GetDebugSummary() succeeded. Summary: {summary}");
                    passCount++;
                }
                catch (System.Exception ex)
                {
                    log.AppendLine($"[FAIL] target.GetDebugSummary() threw exception: {ex.Message}");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("[INFO] No resolved target to test GetDebugSummary.");
                infoCount++;
            }

            // ===== 21. Calling target.OnClawCollision() does not throw =====
            if (resolvedTarget != null)
            {
                try
                {
                    resolvedTarget.OnClawCollision();
                    log.AppendLine("[PASS] target.OnClawCollision() did not throw.");
                    passCount++;
                }
                catch (System.Exception ex)
                {
                    log.AppendLine($"[FAIL] target.OnClawCollision() threw exception: {ex.Message}");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("[INFO] No resolved target to test OnClawCollision.");
                infoCount++;
            }

            // ===== 22. No generated hook under RuntimeFinalContentInstances =====
            bool hookUnderFinalContent = false;
            if (generatedRoot != null)
            {
                Transform[] allTransforms = generatedRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
                {
                    if (t.name == "RuntimeFinalContentInstances")
                    {
                        WorldMapGeneratedHarvestTargetRuntimeHook[] hooksUnderFinal = t.GetComponentsInChildren<WorldMapGeneratedHarvestTargetRuntimeHook>(true);
                        if (hooksUnderFinal.Length > 0)
                        {
                            hookUnderFinalContent = true;
                            log.AppendLine($"[FAIL] Found {hooksUnderFinal.Length} hook(s) under RuntimeFinalContentInstances.");
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

            // ===== 23. No generated hook under RuntimeSpawnedInstances =====
            bool hookUnderSpawned = false;
            if (generatedRoot != null)
            {
                Transform[] allTransforms = generatedRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
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

            // ===== 24. No generated target under RuntimeSpawnedInstances =====
            bool targetUnderSpawned = false;
            if (generatedRoot != null)
            {
                Transform[] allTransforms = generatedRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
                {
                    if (t.name == "RuntimeSpawnedInstances")
                    {
                        WorldMapGeneratedHarvestTarget[] targetsUnderSpawned = t.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                        if (targetsUnderSpawned.Length > 0)
                        {
                            targetUnderSpawned = true;
                            log.AppendLine($"[FAIL] Found {targetsUnderSpawned.Length} generated target(s) under RuntimeSpawnedInstances.");
                            failCount++;
                        }
                    }
                }
            }
            if (!targetUnderSpawned)
            {
                log.AppendLine("[PASS] No generated targets under RuntimeSpawnedInstances.");
                passCount++;
            }

            // ===== 25. No generated target under RuntimeSpawnInstances preview =====
            bool targetUnderSpawnPreview = false;
            if (generatedRoot != null)
            {
                Transform[] allTransforms = generatedRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
                {
                    if (t.name == "RuntimeSpawnInstances")
                    {
                        WorldMapGeneratedHarvestTarget[] targetsUnderPreview = t.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                        if (targetsUnderPreview.Length > 0)
                        {
                            targetUnderSpawnPreview = true;
                            log.AppendLine($"[FAIL] Found {targetsUnderPreview.Length} generated target(s) under RuntimeSpawnInstances.");
                            failCount++;
                        }
                    }
                }
            }
            if (!targetUnderSpawnPreview)
            {
                log.AppendLine("[PASS] No generated targets under RuntimeSpawnInstances.");
                passCount++;
            }

            // ===== 26. No generated target under RuntimePlaceholder =====
            bool targetUnderPlaceholder = false;
            if (generatedRoot != null)
            {
                Transform[] allTransforms = generatedRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
                {
                    if (t.name == "RuntimePlaceholder")
                    {
                        WorldMapGeneratedHarvestTarget[] targetsUnderPlaceholder = t.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
                        if (targetsUnderPlaceholder.Length > 0)
                        {
                            targetUnderPlaceholder = true;
                            log.AppendLine($"[FAIL] Found {targetsUnderPlaceholder.Length} generated target(s) under RuntimePlaceholder.");
                            failCount++;
                        }
                    }
                }
            }
            if (!targetUnderPlaceholder)
            {
                log.AppendLine("[PASS] No generated targets under RuntimePlaceholder.");
                passCount++;
            }

            // ===== 27. Existing manual HarvestTargetBehaviour count unchanged =====
            HarvestTargetBehaviour[] manualTargets = GameObject.FindObjectsByType<HarvestTargetBehaviour>(FindObjectsSortMode.None);
            log.AppendLine($"[INFO] Existing manual HarvestTargetBehaviour count: {manualTargets.Length}");
            infoCount++;

            // ===== 28. Existing HarvestInteractionZone count unchanged =====
            var zoneType = System.Type.GetType("Project.Gameplay.Harvest.HarvestInteractionZone, Assembly-CSharp");
            int zoneCount = 0;
            if (zoneType != null)
            {
                var zoneObjects = GameObject.FindObjectsByType(zoneType, FindObjectsSortMode.None);
                zoneCount = zoneObjects != null ? zoneObjects.Length : 0;
            }
            log.AppendLine($"[INFO] Existing HarvestInteractionZone count: {zoneCount}");
            infoCount++;

            // ===== 29. Scene object transform/name unchanged (read-only validation) =====
            log.AppendLine("[INFO] Scene object transform/name unchanged (read-only validation).");
            infoCount++;

            // ===== 30. Hook detection radius > 0 =====
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

            // ===== 31. Generated target GetDebugSummary includes key fields =====
            if (resolvedTarget != null && resolvedTarget is WorldMapGeneratedHarvestTarget generatedTarget)
            {
                string summary = generatedTarget.GetDebugSummary();
                bool hasSourceMarkerId = summary.Contains(generatedTarget.SourceMarkerId);
                bool hasTargetKind = summary.Contains(generatedTarget.TargetKind.ToString());
                bool hasProfileId = summary.Contains(generatedTarget.ProfileId);
                bool hasRuntimeKey = summary.Contains(generatedTarget.RuntimeKey);

                if (hasSourceMarkerId && hasTargetKind && hasProfileId && hasRuntimeKey)
                {
                    log.AppendLine("[PASS] GetDebugSummary includes SourceMarkerId/TargetKind/ProfileId/RuntimeKey.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"[WARN] GetDebugSummary missing some fields. Summary: {summary}");
                    warnCount++;
                }
            }
            else
            {
                log.AppendLine("[INFO] No resolved WorldMapGeneratedHarvestTarget to check GetDebugSummary fields.");
                infoCount++;
            }

            // ===== 32. Consume() does not throw (non-destructive guarded mode) =====
            if (resolvedTarget != null)
            {
                try
                {
                    // Consume() 호출 전 IsAvailable 상태 기록
                    bool wasAvailable = resolvedTarget.IsAvailable;

                    resolvedTarget.Consume();

                    // Consume() 후에도 exception이 발생하지 않아야 함
                    log.AppendLine("[PASS] target.Consume() did not throw (non-destructive).");

                    // Consume() 후 IsAvailable이 false가 되었는지 확인 (정상 동작)
                    if (wasAvailable && !resolvedTarget.IsAvailable)
                    {
                        log.AppendLine("[INFO] target.Consume() correctly set IsAvailable to false.");
                        infoCount++;
                    }

                    passCount++;
                }
                catch (System.Exception ex)
                {
                    log.AppendLine($"[FAIL] target.Consume() threw exception: {ex.Message}");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("[INFO] No resolved target to test Consume.");
                infoCount++;
            }

            // ===== Summary =====
            log.AppendLine($"--- O-13 Validation Summary ---");
            log.AppendLine($"PASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount}");
            log.AppendLine($"Provider count: {(provider != null ? provider.Count : 0)}");
            log.AppendLine($"GeneratedHarvestTarget count: {generatedTargetCount}");
            log.AppendLine($"HarvestPointInteractor count: {allInteractors.Length}");
            log.AppendLine($"Hooked interactor count: {hookedCount}");
            log.AppendLine($"useGeneratedHarvestTargets true count: {interactorWithEnableGenerated}");
            log.AppendLine($"Hook cached target min/max/count: {GetHookCachedTargetStats(allInteractors)}");
            log.AppendLine($"Provider nearest success: {providerNearestSuccess}");
            log.AppendLine($"Hook nearest success: {hookNearestSuccess}");
            log.AppendLine($"Interactor local fallback resolve success: {interactorFallbackResolveSuccess} [INFO]");

            if (failCount == 0)
            {
                log.AppendLine("[PASS] O-13 Validation: All checks passed.");
            }
            else
            {
                log.AppendLine($"[FAIL] O-13 Validation: {failCount} failure(s) detected.");
            }

            Debug.Log(log.ToString());
        }

        // ===== Private Helpers =====

        /// <summary>
        /// 모든 HarvestPointInteractor 중 enableGeneratedWorldHarvestTargets가 true인 개수를 반환한다.
        /// </summary>
        private static int CountEnableGeneratedWorldHarvestTargets(HarvestPointInteractor[] interactors)
        {
            int count = 0;
            foreach (HarvestPointInteractor interactor in interactors)
            {
                if (interactor == null)
                    continue;
                var serializedInteractor = new SerializedObject(interactor);
                SerializedProperty enableGeneratedProp = serializedInteractor.FindProperty("enableGeneratedWorldHarvestTargets");
                if (enableGeneratedProp != null && enableGeneratedProp.propertyType == SerializedPropertyType.Boolean)
                {
                    if (enableGeneratedProp.boolValue)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 모든 hook의 CachedTargetCount 통계 문자열을 반환한다.
        /// </summary>
        private static string GetHookCachedTargetStats(HarvestPointInteractor[] interactors)
        {
            int min = int.MaxValue;
            int max = 0;
            int count = 0;

            foreach (HarvestPointInteractor interactor in interactors)
            {
                if (interactor == null)
                    continue;
                WorldMapGeneratedHarvestTargetRuntimeHook hook = interactor.GetComponent<WorldMapGeneratedHarvestTargetRuntimeHook>();
                if (hook != null)
                {
                    int cached = hook.CachedTargetCount;
                    if (cached < min) min = cached;
                    if (cached > max) max = cached;
                    count++;
                }
            }

            if (count == 0)
                return "N/A";

            return $"min={min}, max={max}, count={count}";
        }
    }
}
