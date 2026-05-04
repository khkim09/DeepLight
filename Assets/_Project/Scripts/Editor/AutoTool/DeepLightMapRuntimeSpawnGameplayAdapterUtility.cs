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
    /// Phase 14.10-L-1: Runtime Spawn Gameplay Adapter 전용 Editor Utility.
    /// RuntimeSpawnedInstances 하위 runtime instance에 WorldMapRuntimeSpawnGameplayAdapter를
    /// 부착/갱신하고 검증한다. 실제 gameplay prefab 교체나 interaction 로직은 포함하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnGameplayAdapterUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot를 찾고, RuntimeSpawnedInstances 하위 모든 runtime instance에
        /// WorldMapRuntimeSpawnGameplayAdapter를 추가 또는 재사용하여 Configure()를 호출한다.
        /// Preview instance와 RuntimePlaceholder 원본은 대상에서 제외한다.
        /// </summary>
        public static void RebuildRuntimeSpawnGameplayAdapters(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-1] Settings is null! Cannot rebuild runtime spawn gameplay adapters.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-L-1: Rebuild Runtime Spawn Gameplay Adapters =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-1] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [L-1] Found GeneratedWorldRoot: '{generatedRoot.name}'");

            // 2. Runtime instance 목록 수집
            //    우선 QueryService.GetAll()을 사용
            WorldMapRuntimeSpawnQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeSpawnQueryService>();
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> runtimeInstances = null;

            if (queryService != null && queryService.Count > 0)
            {
                runtimeInstances = queryService.GetAll();
                Debug.Log($"[MapAutoBuilder] [L-1] Using QueryService.GetAll() - count: {runtimeInstances?.Count ?? 0}");
            }
            else
            {
                // QueryService가 없거나 Count==0이면 Registry를 통해 fallback
                WorldMapRuntimeSpawnInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();
                if (registry != null && registry.AllInstances.Count > 0)
                {
                    runtimeInstances = registry.AllInstances;
                    Debug.Log($"[MapAutoBuilder] [L-1] QueryService unavailable/empty. Falling back to Registry.AllInstances - count: {runtimeInstances?.Count ?? 0}");
                }
                else
                {
                    Debug.LogError("[MapAutoBuilder] [L-1] Both QueryService and Registry are unavailable or empty. Cannot rebuild adapters.");
                    return;
                }
            }

            if (runtimeInstances == null || runtimeInstances.Count == 0)
            {
                Debug.LogError("[MapAutoBuilder] [L-1] No runtime instances found. Cannot rebuild adapters.");
                return;
            }

            // 3. 각 runtime instance에 adapter 부착/갱신
            int totalTargetCount = 0;
            int addedCount = 0;
            int updatedCount = 0;
            int skippedPreviewCount = 0;
            int failedCount = 0;

            // category/adapterKind breakdown 수집
            var categoryCounts = new Dictionary<string, int>();
            var adapterKindCounts = new Dictionary<WorldMapRuntimeGameplayAdapterKind, int>();

            foreach (WorldMapRuntimeSpawnInstanceTag tag in runtimeInstances)
            {
                if (tag == null)
                {
                    failedCount++;
                    continue;
                }

                // Preview instance는 제외
                if (tag.IsPreviewInstance)
                {
                    skippedPreviewCount++;
                    continue;
                }

                // RuntimePlaceholder 원본은 제외 (WorldMapRuntimePlaceholderBinding이 붙어 있으면 원본)
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    skippedPreviewCount++;
                    continue;
                }

                totalTargetCount++;

                // adapter component 추가 또는 재사용
                WorldMapRuntimeSpawnGameplayAdapter adapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                if (adapter == null)
                {
                    // 새로 추가
                    adapter = tag.gameObject.AddComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                    Undo.RegisterCreatedObjectUndo(adapter, "Add WorldMapRuntimeSpawnGameplayAdapter");
                    addedCount++;
                }
                else
                {
                    updatedCount++;
                }

                // adapter.Configure(tag) 호출
                adapter.Configure(tag);

                // category/adapterKind breakdown 수집
                string cat = tag.RuntimeCategory;
                if (!string.IsNullOrEmpty(cat))
                {
                    if (!categoryCounts.ContainsKey(cat))
                        categoryCounts[cat] = 0;
                    categoryCounts[cat]++;
                }

                WorldMapRuntimeGameplayAdapterKind kind = adapter.AdapterKind;
                if (!adapterKindCounts.ContainsKey(kind))
                    adapterKindCounts[kind] = 0;
                adapterKindCounts[kind]++;

                // Scene 변경 사항 저장
                EditorUtility.SetDirty(tag.gameObject);
            }

            // 4. 결과 요약 출력
            Debug.Log($"[MapAutoBuilder] [L-1] Rebuild complete. " +
                $"Total target: {totalTargetCount}, " +
                $"Added: {addedCount}, " +
                $"Updated: {updatedCount}, " +
                $"Skipped preview: {skippedPreviewCount}, " +
                $"Failed: {failedCount}");

            // Category breakdown
            Debug.Log("[MapAutoBuilder] [L-1] --- Category Breakdown ---");
            foreach (var kvp in categoryCounts)
            {
                Debug.Log($"[MapAutoBuilder] [L-1]   {kvp.Key}: {kvp.Value}");
            }

            // AdapterKind breakdown
            Debug.Log("[MapAutoBuilder] [L-1] --- AdapterKind Breakdown ---");
            foreach (var kvp in adapterKindCounts)
            {
                Debug.Log($"[MapAutoBuilder] [L-1]   {kvp.Key}: {kvp.Value}");
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-L-1: Rebuild Runtime Spawn Gameplay Adapters Complete =====");
        }

        /// <summary>
        /// RuntimeSpawnedInstances 하위 runtime instance에 WorldMapRuntimeSpawnGameplayAdapter가
        /// 올바르게 부착되었는지 검증한다. 12개 이상의 항목을 검사하고
        /// Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// Validate는 읽기 전용이어야 하며, GameObject 생성/삭제/이동/이름 변경은 하지 않는다.
        /// 단, QueryService/Registry cache가 비어 있으면 cache rebuild는 허용한다.
        /// </summary>
        public static void ValidateRuntimeSpawnGameplayAdapters(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-1] Settings is null! Cannot validate runtime spawn gameplay adapters.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-L-1: Validate Runtime Spawn Gameplay Adapters =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // Validate 시작 전 GeneratedWorldRoot 하위 transform/name snapshot 저장
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

            // Snapshot: Validate 시작 전 transform/name 상태 저장
            var preValidateSnapshot = CaptureTransformSnapshot(generatedRoot.transform);

            // 2. QueryService 또는 Registry 존재 검사
            WorldMapRuntimeSpawnQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeSpawnQueryService>();
            WorldMapRuntimeSpawnInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();

            if (queryService == null && registry == null)
            {
                log.AppendLine("  [FAIL] Neither WorldMapRuntimeSpawnQueryService nor WorldMapRuntimeSpawnInstanceRegistry found on GeneratedWorldRoot.");
                failCount++;
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }

            if (queryService != null)
            {
                log.AppendLine("  [PASS] WorldMapRuntimeSpawnQueryService found on GeneratedWorldRoot.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [INFO] WorldMapRuntimeSpawnQueryService not found. Will use Registry as fallback.");
                infoCount++;
            }

            if (registry != null)
            {
                log.AppendLine("  [PASS] WorldMapRuntimeSpawnInstanceRegistry found on GeneratedWorldRoot.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [INFO] WorldMapRuntimeSpawnInstanceRegistry not found. Will use QueryService as fallback.");
                infoCount++;
            }

            // 3. Runtime instance 목록 수집
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> runtimeInstances = null;

            if (queryService != null && queryService.Count > 0)
            {
                runtimeInstances = queryService.GetAll();
            }
            else if (registry != null && registry.AllInstances.Count > 0)
            {
                runtimeInstances = registry.AllInstances;
            }
            else
            {
                // cache가 비어 있으면 scene에서 직접 수집 (cache rebuild 허용)
                log.AppendLine("  [INFO] QueryService/Registry cache is empty. Attempting cache rebuild from scene...");
                infoCount++;

                if (registry != null)
                {
                    registry.RebuildCacheFromRoot(generatedRoot.transform);
                    if (registry.AllInstances.Count > 0)
                    {
                        runtimeInstances = registry.AllInstances;
                        log.AppendLine($"  [INFO] Registry cache rebuild complete. Count: {registry.AllInstances.Count}");
                        infoCount++;
                    }
                }

                if ((runtimeInstances == null || runtimeInstances.Count == 0) && queryService != null)
                {
                    queryService.TryInitializeFromRoot(generatedRoot.transform);
                    if (queryService.Count > 0)
                    {
                        runtimeInstances = queryService.GetAll();
                        log.AppendLine($"  [INFO] QueryService initialize complete. Count: {queryService.Count}");
                        infoCount++;
                    }
                }
            }

            // 4. RuntimeSpawnedInstances 하위 runtime instance count > 0 검사
            int sceneRuntimeCount = WorldMapRuntimeSpawnInstanceRegistry.CountSceneRuntimeInstances(generatedRoot.transform);
            if (sceneRuntimeCount == 0)
            {
                log.AppendLine("  [FAIL] Scene runtime instance count is 0. Expected > 0 runtime instances under RuntimeSpawnedInstances.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] Scene runtime instance count: {sceneRuntimeCount} (> 0).");
                passCount++;
            }

            if (runtimeInstances == null || runtimeInstances.Count == 0)
            {
                log.AppendLine("  [FAIL] No runtime instances available for adapter validation.");
                failCount++;
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }

            log.AppendLine($"  [INFO] Runtime instances available for validation: {runtimeInstances.Count}");
            infoCount++;

            // 5. 모든 runtime instance에 WorldMapRuntimeSpawnGameplayAdapter가 있는지 검사
            int totalWithAdapter = 0;
            int totalWithoutAdapter = 0;
            int totalAdapterNotReady = 0;
            int totalPreviewWithAdapter = 0;
            int totalPlaceholderWithAdapter = 0;

            // category/adapterKind breakdown 수집
            var categoryCounts = new Dictionary<string, int>();
            var adapterKindCounts = new Dictionary<WorldMapRuntimeGameplayAdapterKind, int>();

            foreach (WorldMapRuntimeSpawnInstanceTag tag in runtimeInstances)
            {
                if (tag == null) continue;

                // Preview instance 검사
                if (tag.IsPreviewInstance)
                {
                    // preview instance에 adapter가 붙지 않았는지 검사
                    WorldMapRuntimeSpawnGameplayAdapter previewAdapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                    if (previewAdapter != null)
                    {
                        totalPreviewWithAdapter++;
                    }
                    continue;
                }

                // RuntimePlaceholder 원본 검사
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    WorldMapRuntimeSpawnGameplayAdapter placeholderAdapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                    if (placeholderAdapter != null)
                    {
                        totalPlaceholderWithAdapter++;
                    }
                    continue;
                }

                // Runtime instance adapter 검사
                WorldMapRuntimeSpawnGameplayAdapter adapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                if (adapter == null)
                {
                    totalWithoutAdapter++;
                }
                else
                {
                    totalWithAdapter++;

                    // IsRuntimeAdapterReady 검사
                    if (!adapter.IsRuntimeAdapterReady)
                    {
                        totalAdapterNotReady++;
                    }

                    // category/adapterKind breakdown 수집
                    string cat = adapter.RuntimeCategory;
                    if (!string.IsNullOrEmpty(cat))
                    {
                        if (!categoryCounts.ContainsKey(cat))
                            categoryCounts[cat] = 0;
                        categoryCounts[cat]++;
                    }

                    WorldMapRuntimeGameplayAdapterKind kind = adapter.AdapterKind;
                    if (!adapterKindCounts.ContainsKey(kind))
                        adapterKindCounts[kind] = 0;
                    adapterKindCounts[kind]++;
                }
            }

            // 6. 모든 runtime instance에 adapter가 있는지 검사
            if (totalWithoutAdapter > 0)
            {
                log.AppendLine($"  [FAIL] {totalWithoutAdapter} runtime instance(s) without WorldMapRuntimeSpawnGameplayAdapter.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] All runtime instances have WorldMapRuntimeSpawnGameplayAdapter. (count: {totalWithAdapter})");
                passCount++;
            }

            // 7. 모든 adapter의 IsRuntimeAdapterReady가 true인지 검사
            if (totalAdapterNotReady > 0)
            {
                log.AppendLine($"  [FAIL] {totalAdapterNotReady} adapter(s) have IsRuntimeAdapterReady = false.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] All adapters have IsRuntimeAdapterReady = true. (count: {totalWithAdapter})");
                passCount++;
            }

            // 8. adapter ZoneId/MarkerId/ProfileId/RuntimeCategory non-empty 검사
            bool allZoneIdNonEmpty = true;
            bool allMarkerIdNonEmpty = true;
            bool allProfileIdNonEmpty = true;
            bool allCategoryNonEmpty = true;

            foreach (WorldMapRuntimeSpawnInstanceTag tag in runtimeInstances)
            {
                if (tag == null || tag.IsPreviewInstance) continue;
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null) continue;

                WorldMapRuntimeSpawnGameplayAdapter adapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                if (adapter == null) continue;

                if (string.IsNullOrEmpty(adapter.ZoneId))
                {
                    allZoneIdNonEmpty = false;
                    log.AppendLine($"  [FAIL] Adapter on '{tag.name}' has empty ZoneId.");
                    failCount++;
                }
                if (string.IsNullOrEmpty(adapter.MarkerId))
                {
                    allMarkerIdNonEmpty = false;
                    log.AppendLine($"  [FAIL] Adapter on '{tag.name}' has empty MarkerId.");
                    failCount++;
                }
                if (string.IsNullOrEmpty(adapter.ProfileId))
                {
                    allProfileIdNonEmpty = false;
                    log.AppendLine($"  [FAIL] Adapter on '{tag.name}' has empty ProfileId.");
                    failCount++;
                }
                if (string.IsNullOrEmpty(adapter.RuntimeCategory))
                {
                    allCategoryNonEmpty = false;
                    log.AppendLine($"  [FAIL] Adapter on '{tag.name}' has empty RuntimeCategory.");
                    failCount++;
                }
            }

            if (allZoneIdNonEmpty)
            {
                log.AppendLine("  [PASS] All adapters have non-empty ZoneId.");
                passCount++;
            }
            if (allMarkerIdNonEmpty)
            {
                log.AppendLine("  [PASS] All adapters have non-empty MarkerId.");
                passCount++;
            }
            if (allProfileIdNonEmpty)
            {
                log.AppendLine("  [PASS] All adapters have non-empty ProfileId.");
                passCount++;
            }
            if (allCategoryNonEmpty)
            {
                log.AppendLine("  [PASS] All adapters have non-empty RuntimeCategory.");
                passCount++;
            }

            // 9. adapterKind가 None이 아닌지 검사
            bool allAdapterKindNotNone = true;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in runtimeInstances)
            {
                if (tag == null || tag.IsPreviewInstance) continue;
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null) continue;

                WorldMapRuntimeSpawnGameplayAdapter adapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                if (adapter == null) continue;

                if (adapter.AdapterKind == WorldMapRuntimeGameplayAdapterKind.None)
                {
                    allAdapterKindNotNone = false;
                    log.AppendLine($"  [FAIL] Adapter on '{tag.name}' has AdapterKind = None.");
                    failCount++;
                }
            }

            if (allAdapterKindNotNone)
            {
                log.AppendLine("  [PASS] All adapters have AdapterKind != None.");
                passCount++;
            }

            // 10. preview instance에 adapter가 붙지 않았는지 검사
            if (totalPreviewWithAdapter > 0)
            {
                log.AppendLine($"  [FAIL] {totalPreviewWithAdapter} preview instance(s) have WorldMapRuntimeSpawnGameplayAdapter. Preview instances should not have adapters.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] No preview instances have WorldMapRuntimeSpawnGameplayAdapter.");
                passCount++;
            }

            // 11. RuntimePlaceholder 원본에 adapter가 붙지 않았는지 검사
            if (totalPlaceholderWithAdapter > 0)
            {
                log.AppendLine($"  [FAIL] {totalPlaceholderWithAdapter} RuntimePlaceholder(s) have WorldMapRuntimeSpawnGameplayAdapter. Placeholders should not have adapters.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] No RuntimePlaceholders have WorldMapRuntimeSpawnGameplayAdapter.");
                passCount++;
            }

            // 12. WorldMapRuntimePlaceholderBinding / WorldMapZoneContentMarker를 가진 원본이 adapter 대상으로 잡히지 않았는지 검사
            bool originalMarkerFoundWithAdapter = false;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in runtimeInstances)
            {
                if (tag == null) continue;

                // WorldMapZoneContentMarker를 가진 object가 adapter를 가지고 있는지 검사
                if (tag.GetComponent<WorldMapZoneContentMarker>() != null)
                {
                    WorldMapRuntimeSpawnGameplayAdapter markerAdapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                    if (markerAdapter != null)
                    {
                        originalMarkerFoundWithAdapter = true;
                        log.AppendLine($"  [FAIL] Original marker (with WorldMapZoneContentMarker) has adapter: '{tag.name}'");
                        failCount++;
                    }
                }

                // WorldMapRuntimePlaceholderBinding을 가진 object가 adapter를 가지고 있는지 검사
                if (tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    WorldMapRuntimeSpawnGameplayAdapter bindingAdapter = tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>();
                    if (bindingAdapter != null)
                    {
                        originalMarkerFoundWithAdapter = true;
                        log.AppendLine($"  [FAIL] Original placeholder (with WorldMapRuntimePlaceholderBinding) has adapter: '{tag.name}'");
                        failCount++;
                    }
                }
            }

            if (!originalMarkerFoundWithAdapter)
            {
                log.AppendLine("  [PASS] No original markers/placeholders have WorldMapRuntimeSpawnGameplayAdapter.");
                passCount++;
            }

            // 13. Category breakdown 출력
            log.AppendLine("  [INFO] --- Category Breakdown ---");
            infoCount++;
            foreach (var kvp in categoryCounts)
            {
                log.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }

            // 14. AdapterKind breakdown 출력
            log.AppendLine("  [INFO] --- AdapterKind Breakdown ---");
            infoCount++;
            foreach (var kvp in adapterKindCounts)
            {
                log.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }

            // 15. Scene object transform/name 변경 없음 검사
            var postValidateSnapshot = CaptureTransformSnapshot(generatedRoot.transform);
            bool snapshotChanged = HasSnapshotChanged(preValidateSnapshot, postValidateSnapshot);
            if (snapshotChanged)
            {
                log.AppendLine("  [FAIL] Scene object transform/name changed during validation. Validate should be read-only.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] Scene object transform/name unchanged (validate is read-only).");
                passCount++;
            }

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
        /// Transform 하위의 모든 GameObject에 대해 name과 localPosition/localRotation/localScale을
        /// snapshot으로 저장한다. Validate 전/후 비교를 통해 scene object 변경 여부를 검증한다.
        /// </summary>
        private static List<TransformSnapshotEntry> CaptureTransformSnapshot(Transform root)
        {
            var snapshot = new List<TransformSnapshotEntry>();

            if (root == null)
                return snapshot;

            // root 자신 포함
            snapshot.Add(new TransformSnapshotEntry
            {
                path = GetFullPath(root),
                name = root.name,
                localPosition = root.localPosition,
                localRotation = root.localRotation,
                localScale = root.localScale
            });

            // 모든 자식을 재귀적으로 수집
            CaptureChildTransformsRecursive(root, snapshot);

            return snapshot;
        }

        /// <summary>
        /// 지정한 Transform의 모든 자식을 재귀적으로 snapshot에 추가한다.
        /// </summary>
        private static void CaptureChildTransformsRecursive(Transform parent, List<TransformSnapshotEntry> snapshot)
        {
            if (parent == null)
                return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                    continue;

                snapshot.Add(new TransformSnapshotEntry
                {
                    path = GetFullPath(child),
                    name = child.name,
                    localPosition = child.localPosition,
                    localRotation = child.localRotation,
                    localScale = child.localScale
                });

                // 재귀적으로 자식의 자식도 수집
                CaptureChildTransformsRecursive(child, snapshot);
            }
        }

        /// <summary>
        /// 두 snapshot을 비교하여 변경 사항이 있는지 확인한다.
        /// </summary>
        private static bool HasSnapshotChanged(List<TransformSnapshotEntry> before, List<TransformSnapshotEntry> after)
        {
            if (before.Count != after.Count)
                return true;

            for (int i = 0; i < before.Count; i++)
            {
                if (before[i].path != after[i].path)
                    return true;
                if (before[i].name != after[i].name)
                    return true;
                if (before[i].localPosition != after[i].localPosition)
                    return true;
                if (before[i].localRotation != after[i].localRotation)
                    return true;
                if (before[i].localScale != after[i].localScale)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Transform의 전체 계층 경로를 문자열로 반환한다. (디버그 로그용)
        /// </summary>
        private static string GetFullPath(Transform t)
        {
            if (t == null)
                return "null";

            var path = new StringBuilder();
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

        /// <summary>
        /// Transform snapshot entry 구조체.
        /// Validate 전/후 비교를 위해 name, path, transform 값을 저장한다.
        /// </summary>
        private struct TransformSnapshotEntry
        {
            public string path;
            public string name;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }
    }
}
