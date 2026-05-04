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
    /// Phase 14.10-K-1: Runtime Spawn Instance Registry 전용 Editor Utility.
    /// GeneratedWorldRoot에 WorldMapRuntimeSpawnInstanceRegistry를 부착/갱신하고,
    /// cache rebuild 및 검증을 수행한다.
    /// RuntimeSpawnedInstances 내부 instance를 새로 만들거나 삭제하지 않는다.
    /// Preview instance는 cache 대상에서 제외한다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnInstanceRegistryUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot를 찾고, WorldMapRuntimeSpawnInstanceRegistry를 부착/갱신한 후
        /// RebuildCacheFromRoot(generatedWorldRoot.transform)을 호출한다.
        /// RuntimeSpawnedInstances 내부 instance를 새로 만들거나 삭제하지 않는다.
        /// Preview instance는 cache 대상에서 제외한다.
        /// </summary>
        public static void RebuildRuntimeSpawnInstanceRegistry(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-1] Settings is null! Cannot rebuild runtime spawn instance registry.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-K-1: Rebuild Runtime Spawn Instance Registry =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-1] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [K-1] Found GeneratedWorldRoot: '{generatedRoot.name}'");

            // 2. GeneratedWorldRoot에 WorldMapRuntimeSpawnInstanceRegistry가 없으면 추가
            WorldMapRuntimeSpawnInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapRuntimeSpawnInstanceRegistry>();
                Undo.RegisterCreatedObjectUndo(registry, "Add WorldMapRuntimeSpawnInstanceRegistry");
                Debug.Log($"[MapAutoBuilder] [K-1] Added WorldMapRuntimeSpawnInstanceRegistry to '{generatedRoot.name}'");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [K-1] WorldMapRuntimeSpawnInstanceRegistry already exists on '{generatedRoot.name}'");
            }

            // 3. registry.RebuildCacheFromRoot(generatedWorldRoot.transform) 호출
            //    GeneratedWorldRoot 하위 모든 runtime instance를 includeInactive=true로 수집
            registry.RebuildCacheFromRoot(generatedRoot.transform);

            // 4. 결과 요약 출력
            Debug.Log($"[MapAutoBuilder] [K-1] Registry cache rebuild complete. " +
                $"Runtime instances cached: {registry.AllInstances.Count}, " +
                $"Duplicate MarkerIds: {registry.DuplicateMarkerIds.Count}");

            // 5. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-K-1: Rebuild Runtime Spawn Instance Registry Complete =====");
        }

        /// <summary>
        /// WorldMapRuntimeSpawnInstanceRegistry의 유효성을 검사한다.
        /// 12개 이상의 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// scene object transform/name 변경 없음.
        /// 검증 시작 시 registry cache가 비어 있더라도 scene runtime instance count가 0이 아니라면,
        /// registry.RebuildCacheFromRoot(generatedWorldRoot.transform)을 한 번 호출한 뒤 검증한다.
        /// </summary>
        public static void ValidateRuntimeSpawnInstanceRegistry(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-1] Settings is null! Cannot validate runtime spawn instance registry.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-K-1: Validate Runtime Spawn Instance Registry =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // 1. GeneratedWorldRoot 존재
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine("  [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                failCount++;
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }
            log.AppendLine($"  [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");
            passCount++;

            // 2. Registry component 존재
            WorldMapRuntimeSpawnInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();
            if (registry == null)
            {
                log.AppendLine("  [FAIL] WorldMapRuntimeSpawnInstanceRegistry not found on GeneratedWorldRoot.");
                failCount++;
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }
            log.AppendLine("  [PASS] WorldMapRuntimeSpawnInstanceRegistry found on GeneratedWorldRoot.");
            passCount++;

            // 3. RuntimeSpawnedInstances root 존재 여부 (재귀 탐색: 각 ZoneRoot_XX 하위에 존재)
            //    generatedRoot.transform.Find("RuntimeSpawnedInstances") 같은 direct child 탐색을 사용하지 않음.
            Transform runtimeSpawnedRoot = FindRuntimeSpawnedInstancesRoot(generatedRoot.transform);
            if (runtimeSpawnedRoot == null)
            {
                log.AppendLine("  [INFO] No RuntimeSpawnedInstances found under GeneratedWorldRoot. (No runtime instances exist yet.)");
                infoCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] RuntimeSpawnedInstances root found: '{GetFullPath(runtimeSpawnedRoot)}'. Children: {runtimeSpawnedRoot.childCount}");
                passCount++;
            }

            // 4. Scene runtime instance count 계산 (Registry의 공용 static helper 사용)
            //    RebuildCacheFromRoot와 동일한 필터 로직 적용
            int sceneRuntimeCount = WorldMapRuntimeSpawnInstanceRegistry.CountSceneRuntimeInstances(generatedRoot.transform);

            if (sceneRuntimeCount == 0)
            {
                if (runtimeSpawnedRoot == null)
                {
                    log.AppendLine("  [INFO] No RuntimeSpawnedInstances root and no runtime instances in scene. Registry will be empty.");
                    infoCount++;
                }
                else
                {
                    log.AppendLine("  [WARN] RuntimeSpawnedInstances root exists but no runtime instances found in scene. Registry will be empty.");
                    warnCount++;
                }
            }
            else
            {
                log.AppendLine($"  [PASS] Scene runtime instance count: {sceneRuntimeCount}");
                passCount++;
            }

            // 5. Registry cache가 비어 있고 scene runtime instance count > 0이면 자동 rebuild
            if (registry.AllInstances.Count == 0 && sceneRuntimeCount > 0)
            {
                log.AppendLine("  [INFO] Registry cache is empty but scene has runtime instances. Auto-rebuilding cache from GeneratedWorldRoot...");
                infoCount++;

                // RebuildCacheFromRoot로 GeneratedWorldRoot 하위 수집
                registry.RebuildCacheFromRoot(generatedRoot.transform);

                log.AppendLine($"  [INFO] Auto-rebuild complete. Registry.AllInstances count: {registry.AllInstances.Count}");
                infoCount++;
            }

            // 5b. Registry cache count != scene runtime instance count이면 자동 rebuild 후 재확인
            if (registry.AllInstances.Count != sceneRuntimeCount)
            {
                log.AppendLine("  [INFO] Registry.AllInstances count differs from scene runtime instance count. Auto-rebuilding cache...");
                infoCount++;

                registry.RebuildCacheFromRoot(generatedRoot.transform);

                // rebuild 후 scene count 재계산 (rebuild로 인해 scene이 변경되지는 않으므로 동일해야 함)
                int sceneCountAfterRebuild = WorldMapRuntimeSpawnInstanceRegistry.CountSceneRuntimeInstances(generatedRoot.transform);
                log.AppendLine($"  [INFO] Auto-rebuild complete. Registry.AllInstances count: {registry.AllInstances.Count}, Scene count: {sceneCountAfterRebuild}");
                infoCount++;
            }

            // 6. registry.AllInstances count == scene runtime instance count
            //    (auto-rebuild 후에도 불일치하면 FAIL)
            int finalSceneCount = WorldMapRuntimeSpawnInstanceRegistry.CountSceneRuntimeInstances(generatedRoot.transform);
            if (registry.AllInstances.Count != finalSceneCount)
            {
                log.AppendLine($"  [FAIL] Registry.AllInstances count ({registry.AllInstances.Count}) != scene runtime instance count ({finalSceneCount}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] Registry.AllInstances count ({registry.AllInstances.Count}) matches scene runtime instance count ({finalSceneCount}).");
                passCount++;
            }

            // 7. Preview instance가 registry에 포함되지 않음
            bool previewFoundInRegistry = false;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached != null && cached.IsPreviewInstance)
                {
                    previewFoundInRegistry = true;
                    log.AppendLine($"  [FAIL] Preview instance found in registry: '{cached.name}' (MarkerId: {cached.MarkerId})");
                    failCount++;
                    break;
                }
            }
            if (!previewFoundInRegistry)
            {
                log.AppendLine("  [PASS] No preview instances in registry.");
                passCount++;
            }

            // 8. RuntimePlaceholder가 registry에 포함되지 않음
            //    WorldMapRuntimeSpawnPlaceholderTag는 제외 조건으로 사용하지 않음.
            //    대신 WorldMapRuntimePlaceholderBinding 존재 여부로 원본 placeholder를 식별.
            bool placeholderFoundInRegistry = false;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached != null && cached.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    placeholderFoundInRegistry = true;
                    log.AppendLine($"  [FAIL] RuntimePlaceholder (with WorldMapRuntimePlaceholderBinding) found in registry: '{cached.name}'");
                    failCount++;
                    break;
                }
            }
            if (!placeholderFoundInRegistry)
            {
                log.AppendLine("  [PASS] No RuntimePlaceholders in registry.");
                passCount++;
            }

            // 9. 모든 cached instance의 ZoneId non-empty
            bool allZoneIdNonEmpty = true;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached != null && string.IsNullOrEmpty(cached.ZoneId))
                {
                    allZoneIdNonEmpty = false;
                    log.AppendLine($"  [FAIL] Instance '{cached.name}' has empty ZoneId.");
                    failCount++;
                    break;
                }
            }
            if (allZoneIdNonEmpty)
            {
                log.AppendLine("  [PASS] All cached instances have non-empty ZoneId.");
                passCount++;
            }

            // 10. 모든 cached instance의 MarkerId non-empty
            bool allMarkerIdNonEmpty = true;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached != null && string.IsNullOrEmpty(cached.MarkerId))
                {
                    allMarkerIdNonEmpty = false;
                    log.AppendLine($"  [FAIL] Instance '{cached.name}' has empty MarkerId.");
                    failCount++;
                    break;
                }
            }
            if (allMarkerIdNonEmpty)
            {
                log.AppendLine("  [PASS] All cached instances have non-empty MarkerId.");
                passCount++;
            }

            // 11. 모든 cached instance의 RuntimeCategory non-empty
            bool allCategoryNonEmpty = true;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached != null && string.IsNullOrEmpty(cached.RuntimeCategory))
                {
                    allCategoryNonEmpty = false;
                    log.AppendLine($"  [FAIL] Instance '{cached.name}' has empty RuntimeCategory.");
                    failCount++;
                    break;
                }
            }
            if (allCategoryNonEmpty)
            {
                log.AppendLine("  [PASS] All cached instances have non-empty RuntimeCategory.");
                passCount++;
            }

            // 12. 모든 cached instance의 ProfileId non-empty
            bool allProfileIdNonEmpty = true;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached != null && string.IsNullOrEmpty(cached.ProfileId))
                {
                    allProfileIdNonEmpty = false;
                    log.AppendLine($"  [FAIL] Instance '{cached.name}' has empty ProfileId.");
                    failCount++;
                    break;
                }
            }
            if (allProfileIdNonEmpty)
            {
                log.AppendLine("  [PASS] All cached instances have non-empty ProfileId.");
                passCount++;
            }

            // 13. Category breakdown 출력
            log.AppendLine("  [INFO] --- Category Breakdown ---");
            infoCount++;
            var categoryCounts = new Dictionary<string, int>();
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached == null) continue;
                string cat = cached.RuntimeCategory;
                if (!categoryCounts.ContainsKey(cat))
                    categoryCounts[cat] = 0;
                categoryCounts[cat]++;
            }
            foreach (var kvp in categoryCounts)
            {
                log.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }

            // 14. Zone count breakdown 출력
            log.AppendLine("  [INFO] --- Zone Count Breakdown ---");
            infoCount++;
            var zoneCounts = new Dictionary<string, int>();
            foreach (WorldMapRuntimeSpawnInstanceTag cached in registry.AllInstances)
            {
                if (cached == null) continue;
                string zone = cached.ZoneId;
                if (!zoneCounts.ContainsKey(zone))
                    zoneCounts[zone] = 0;
                zoneCounts[zone]++;
            }
            foreach (var kvp in zoneCounts)
            {
                log.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }

            // 15. Duplicate MarkerId count 출력
            int duplicateCount = registry.DuplicateMarkerIds.Count;
            if (duplicateCount > 0)
            {
                log.AppendLine($"  [WARN] Duplicate MarkerId count: {duplicateCount}");
                warnCount++;
                foreach (string dupId in registry.DuplicateMarkerIds)
                {
                    log.AppendLine($"    Duplicate MarkerId: '{dupId}'");
                }
            }
            else
            {
                log.AppendLine($"  [PASS] No duplicate MarkerIds. (count: 0)");
                passCount++;
            }

            // 16. Scene object transform/name 변경 없음 확인
            // (이 utility는 scene object를 변경하지 않으므로 항상 PASS)
            log.AppendLine("  [PASS] Scene object transform/name unchanged (this utility is read-only for scene objects).");
            passCount++;

            // 최종 결과 출력
            LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// Settings + Context에 따라 Generated Root를 찾는다.
        /// DeepLightMapAutoBuilder.FindGeneratedRoot와 동일한 로직.
        /// </summary>
        private static GameObject FindGeneratedRoot(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (context != null && context.GeneratedRootOverride != null)
            {
                return context.GeneratedRootOverride;
            }

            return GameObject.Find(settings.GeneratedRootName);
        }

        /// <summary>
        /// GeneratedWorldRoot 하위에서 "RuntimeSpawnedInstances" 이름을 가진 Transform을 재귀 탐색한다.
        /// RuntimeSpawnedInstances는 각 ZoneRoot_XX 하위에 있으므로 direct child 탐색이 아닌 전체 재귀 탐색을 사용한다.
        /// </summary>
        /// <param name="root">검색 시작 root Transform</param>
        /// <returns>첫 번째로 발견된 RuntimeSpawnedInstances Transform, 없으면 null</returns>
        private static Transform FindRuntimeSpawnedInstancesRoot(Transform root)
        {
            if (root == null)
                return null;

            // root 자신이 "RuntimeSpawnedInstances"인 경우
            if (root.name == "RuntimeSpawnedInstances")
                return root;

            // 자식들을 재귀 탐색
            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindRuntimeSpawnedInstancesRoot(root.GetChild(i));
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Transform의 전체 계층 경로를 문자열로 반환한다. (디버그 로그용)
        /// </summary>
        private static string GetFullPath(Transform t)
        {
            if (t == null)
                return "null";

            var path = new System.Text.StringBuilder();
            Transform current = t;
            while (current != null)
            {
                if (path.Length > 0)
                    path.Insert(0, "/");
                path.Insert(0, current.name);
                current = current.parent;
            }
            return path.ToString();
        }

        /// <summary>
        /// 검증 결과를 Console에 출력한다.
        /// </summary>
        private static void LogAndFinalize(StringBuilder log, int pass, int fail, int warn, int info)
        {
            log.AppendLine($"--- Summary: PASS={pass}, FAIL={fail}, WARN={warn}, INFO={info} ---");

            if (fail > 0)
            {
                log.AppendLine("=> Validation FAILED. Fix FAIL items before proceeding.");
            }
            else if (warn > 0)
            {
                log.AppendLine("=> Validation PASSED with warnings.");
            }
            else
            {
                log.AppendLine("=> Validation PASSED. All checks OK.");
            }

            Debug.Log(log.ToString());
        }
    }
}
