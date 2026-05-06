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
    /// Phase 14.10-M-1: Runtime Final Content Contract 전용 Editor Utility.
    /// RuntimeSpawnedInstances 하위 runtime instance에 WorldMapRuntimeFinalContentContract를
    /// 부착/갱신하고, category별 coverage와 contract readiness를 검증한다.
    /// 실제 gameplay 기능은 실행하지 않으며, 오직 최종 content prefab/profile로 교체될 준비가
    /// 되었는지 audit하는 단계이다.
    /// </summary>
    public static class DeepLightMapRuntimeFinalContentContractUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot를 찾고, RuntimeSpawnedInstances 하위 모든 adapter에
        /// WorldMapRuntimeFinalContentContract를 부착/갱신한다.
        /// QueryService가 없거나 Count가 0이면 Registry/QueryService를 rebuild/initialize한다.
        /// RuntimeSpawnInstances preview instance, RuntimePlaceholder 원본,
        /// WorldMapZoneContentMarker가 붙은 object는 제외한다.
        /// </summary>
        public static void RebuildRuntimeFinalContentContracts(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-1] Settings is null! Cannot rebuild runtime final content contracts.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-M-1: Rebuild Runtime Final Content Contracts =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-1] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [M-1] Found GeneratedWorldRoot: '{generatedRoot.name}'");

            // 2. QueryService 찾기 또는 초기화
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null || queryService.Count == 0)
            {
                Debug.Log("[MapAutoBuilder] [M-1] QueryService not found or empty. Rebuilding Registry/QueryService...");

                // Registry 찾기 또는 추가
                WorldMapRuntimeGameplayAdapterRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterRegistry>();
                if (registry == null)
                {
                    registry = generatedRoot.AddComponent<WorldMapRuntimeGameplayAdapterRegistry>();
                    Debug.Log($"[MapAutoBuilder] [M-1] Added WorldMapRuntimeGameplayAdapterRegistry to '{generatedRoot.name}'.");
                }

                // Registry cache 재구축
                registry.RebuildCacheFromRoot(generatedRoot.transform);

                // QueryService 찾기 또는 추가
                queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
                if (queryService == null)
                {
                    queryService = generatedRoot.AddComponent<WorldMapRuntimeGameplayAdapterQueryService>();
                    Debug.Log($"[MapAutoBuilder] [M-1] Added WorldMapRuntimeGameplayAdapterQueryService to '{generatedRoot.name}'.");
                }

                // QueryService 초기화
                queryService.TryInitializeFromRoot(generatedRoot.transform);
            }

            // 3. QueryService.GetAll()로 adapter 목록 획득
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
            if (allAdapters == null || allAdapters.Count == 0)
            {
                Debug.LogError("[MapAutoBuilder] [M-1] No adapters found in QueryService. Cannot rebuild contracts.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [M-1] Found {allAdapters.Count} adapters in QueryService.");

            // 4. 각 adapter GameObject에 contract 부착/갱신
            int addedCount = 0;
            int updatedCount = 0;
            int readyCount = 0;
            int notReadyCount = 0;
            int requiresReplacementCount = 0;
            int fallbackCount = 0;
            int editorPlaceholderCount = 0;

            // category별 breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            // finalContentKind별 breakdown
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();

            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null || adapter.gameObject == null)
                    continue;

                // RuntimeSpawnedInstances 하위만 대상
                if (!IsUnderRuntimeSpawnedInstances(adapter.transform))
                    continue;

                // Preview instance 제외
                WorldMapRuntimeSpawnInstanceTag tag = adapter.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                if (tag != null && tag.IsPreviewInstance)
                    continue;

                // RuntimePlaceholderBinding이 있는 원본 placeholder 제외
                if (adapter.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // WorldMapZoneContentMarker가 붙은 marker/debug object 제외
                if (adapter.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // Contract 부착/갱신
                WorldMapRuntimeFinalContentContract contract = adapter.GetComponent<WorldMapRuntimeFinalContentContract>();
                if (contract == null)
                {
                    contract = adapter.gameObject.AddComponent<WorldMapRuntimeFinalContentContract>();
                    addedCount++;
                }
                else
                {
                    updatedCount++;
                }

                // Configure 호출
                contract.Configure(adapter);

                // 통계 수집
                if (contract.IsFinalContentReady)
                    readyCount++;
                else
                    notReadyCount++;

                if (contract.RequiresRealPrefabReplacement)
                    requiresReplacementCount++;

                if (contract.IsFallbackProfile)
                    fallbackCount++;

                if (contract.IsEditorPlaceholderPrefab)
                    editorPlaceholderCount++;

                // Category breakdown
                string cat = contract.RuntimeCategory;
                if (!string.IsNullOrEmpty(cat))
                {
                    if (!categoryBreakdown.ContainsKey(cat))
                        categoryBreakdown[cat] = 0;
                    categoryBreakdown[cat]++;
                }

                // FinalContentKind breakdown
                WorldMapRuntimeFinalContentKind kind = contract.FinalContentKind;
                if (!finalContentKindBreakdown.ContainsKey(kind))
                    finalContentKindBreakdown[kind] = 0;
                finalContentKindBreakdown[kind]++;
            }

            // 5. 결과 로그 출력
            LogRebuildResult(
                allAdapters.Count,
                addedCount,
                updatedCount,
                readyCount,
                notReadyCount,
                requiresReplacementCount,
                fallbackCount,
                editorPlaceholderCount,
                categoryBreakdown,
                finalContentKindBreakdown);
        }

        /// <summary>
        /// RuntimeSpawnedInstances 하위 runtime instance에 부착된 WorldMapRuntimeFinalContentContract의
        /// 유효성을 검사한다. 20개 이상의 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Validate는 GameObject 생성/삭제/이동/name 변경을 금지한다.
        /// </summary>
        public static void ValidateRuntimeFinalContentContracts(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-1] Settings is null! Cannot validate runtime final content contracts.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-M-1: Validate Runtime Final Content Contracts =====");

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-M-1: Validate Runtime Final Content Contracts =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine("  [FAIL] 1. GeneratedWorldRoot exists: NOT FOUND");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 1. GeneratedWorldRoot exists: '{generatedRoot.name}'");
                passCount++;
            }

            if (generatedRoot == null)
            {
                log.AppendLine("  [FAIL] Cannot proceed without GeneratedWorldRoot.");
                failCount++;
                Debug.Log(log.ToString());
                return;
            }

            // Validate 전후 snapshot으로 transform/name 변경 여부 확인
            var preValidateSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();
            CaptureTransformSnapshots(generatedRoot.transform, preValidateSnapshots);

            // 2. GameplayAdapterQueryService exists
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null)
            {
                log.AppendLine("  [FAIL] 2. WorldMapRuntimeGameplayAdapterQueryService exists: NOT FOUND");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 2. WorldMapRuntimeGameplayAdapterQueryService exists");
                passCount++;
            }

            if (queryService == null)
            {
                log.AppendLine("  [FAIL] Cannot proceed without QueryService.");
                failCount++;
                Debug.Log(log.ToString());
                return;
            }

            // 3. Adapter count > 0
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
            int adapterCount = allAdapters?.Count ?? 0;
            if (adapterCount <= 0)
            {
                log.AppendLine($"  [FAIL] 3. Adapter count > 0: {adapterCount}");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 3. Adapter count > 0: {adapterCount}");
                passCount++;
            }

            if (adapterCount <= 0)
            {
                log.AppendLine("  [FAIL] Cannot proceed without adapters.");
                failCount++;
                Debug.Log(log.ToString());
                return;
            }

            // Contract 수집 (RuntimeSpawnedInstances 하위만)
            var validContracts = new List<WorldMapRuntimeFinalContentContract>();
            var previewContracts = new List<WorldMapRuntimeFinalContentContract>();
            var placeholderContracts = new List<WorldMapRuntimeFinalContentContract>();
            var markerContracts = new List<WorldMapRuntimeFinalContentContract>();

            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null || adapter.gameObject == null)
                    continue;

                WorldMapRuntimeFinalContentContract contract = adapter.GetComponent<WorldMapRuntimeFinalContentContract>();
                if (contract == null)
                    continue;

                // RuntimeSpawnedInstances 하위인지 확인
                if (!IsUnderRuntimeSpawnedInstances(adapter.transform))
                    continue;

                // Preview instance 체크
                WorldMapRuntimeSpawnInstanceTag tag = adapter.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                if (tag != null && tag.IsPreviewInstance)
                {
                    previewContracts.Add(contract);
                    continue;
                }

                // RuntimePlaceholderBinding 체크
                if (adapter.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    placeholderContracts.Add(contract);
                    continue;
                }

                // WorldMapZoneContentMarker 체크
                if (adapter.GetComponent<WorldMapZoneContentMarker>() != null)
                {
                    markerContracts.Add(contract);
                    continue;
                }

                validContracts.Add(contract);
            }

            // 4. Contract count == Adapter count (valid contracts만)
            int contractCount = validContracts.Count;
            if (contractCount != adapterCount)
            {
                log.AppendLine($"  [FAIL] 4. Contract count ({contractCount}) == Adapter count ({adapterCount})");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 4. Contract count ({contractCount}) == Adapter count ({adapterCount})");
                passCount++;
            }

            // 5. Preview instance에 contract가 붙지 않았는지 확인
            if (previewContracts.Count > 0)
            {
                log.AppendLine($"  [FAIL] 5. Preview instance에 contract가 붙지 않음: {previewContracts.Count} contracts found on preview instances");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 5. Preview instance에 contract가 붙지 않음");
                passCount++;
            }

            // 6. RuntimePlaceholder 원본에 contract가 붙지 않았는지 확인
            if (placeholderContracts.Count > 0)
            {
                log.AppendLine($"  [FAIL] 6. RuntimePlaceholder 원본에 contract가 붙지 않음: {placeholderContracts.Count} contracts found on placeholders");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 6. RuntimePlaceholder 원본에 contract가 붙지 않음");
                passCount++;
            }

            // 7. WorldMapZoneContentMarker object에 contract가 붙지 않았는지 확인
            if (markerContracts.Count > 0)
            {
                log.AppendLine($"  [FAIL] 7. WorldMapZoneContentMarker object에 contract가 붙지 않음: {markerContracts.Count} contracts found on markers");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 7. WorldMapZoneContentMarker object에 contract가 붙지 않음");
                passCount++;
            }

            // 8. 모든 contract ZoneId non-empty
            bool allZoneIdValid = true;
            foreach (var contract in validContracts)
            {
                if (string.IsNullOrEmpty(contract.ZoneId))
                {
                    allZoneIdValid = false;
                    log.AppendLine($"  [FAIL] 8. Contract '{contract.name}' has empty ZoneId");
                    failCount++;
                    break;
                }
            }
            if (allZoneIdValid)
            {
                log.AppendLine("  [PASS] 8. All contracts have non-empty ZoneId");
                passCount++;
            }

            // 9. 모든 contract sourceMarkerId non-empty
            bool allMarkerIdValid = true;
            foreach (var contract in validContracts)
            {
                if (string.IsNullOrEmpty(contract.SourceMarkerId))
                {
                    allMarkerIdValid = false;
                    log.AppendLine($"  [FAIL] 9. Contract '{contract.name}' has empty SourceMarkerId");
                    failCount++;
                    break;
                }
            }
            if (allMarkerIdValid)
            {
                log.AppendLine("  [PASS] 9. All contracts have non-empty SourceMarkerId");
                passCount++;
            }

            // 10. 모든 contract runtimeCategory non-empty
            bool allCategoryValid = true;
            foreach (var contract in validContracts)
            {
                if (string.IsNullOrEmpty(contract.RuntimeCategory))
                {
                    allCategoryValid = false;
                    log.AppendLine($"  [FAIL] 10. Contract '{contract.name}' has empty RuntimeCategory");
                    failCount++;
                    break;
                }
            }
            if (allCategoryValid)
            {
                log.AppendLine("  [PASS] 10. All contracts have non-empty RuntimeCategory");
                passCount++;
            }

            // 11. 모든 contract profileId non-empty
            bool allProfileIdValid = true;
            foreach (var contract in validContracts)
            {
                if (string.IsNullOrEmpty(contract.ProfileId))
                {
                    allProfileIdValid = false;
                    log.AppendLine($"  [FAIL] 11. Contract '{contract.name}' has empty ProfileId");
                    failCount++;
                    break;
                }
            }
            if (allProfileIdValid)
            {
                log.AppendLine("  [PASS] 11. All contracts have non-empty ProfileId");
                passCount++;
            }

            // 12. 모든 contract finalContentKind != None (None이면 WARN)
            int noneKindCount = 0;
            foreach (var contract in validContracts)
            {
                if (contract.FinalContentKind == WorldMapRuntimeFinalContentKind.None)
                {
                    noneKindCount++;
                }
            }
            if (noneKindCount > 0)
            {
                log.AppendLine($"  [WARN] 12. Contracts with FinalContentKind=None: {noneKindCount} (expected 0 in final state)");
                warnCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 12. All contracts have FinalContentKind != None");
                passCount++;
            }

            // 13. Unknown finalContentKind count 출력
            int unknownKindCount = 0;
            foreach (var contract in validContracts)
            {
                if (contract.FinalContentKind == WorldMapRuntimeFinalContentKind.Unknown)
                {
                    unknownKindCount++;
                }
            }
            log.AppendLine($"  [INFO] 13. Contracts with FinalContentKind=Unknown: {unknownKindCount}");
            infoCount++;

            // 14. requiresRealPrefabReplacement count 출력
            int requiresReplacementCount = 0;
            foreach (var contract in validContracts)
            {
                if (contract.RequiresRealPrefabReplacement)
                    requiresReplacementCount++;
            }
            log.AppendLine($"  [INFO] 14. Contracts requiring real prefab replacement: {requiresReplacementCount}");
            infoCount++;

            // 15. editor placeholder prefab 기반 count 출력
            int editorPlaceholderCount = 0;
            foreach (var contract in validContracts)
            {
                if (contract.IsEditorPlaceholderPrefab)
                    editorPlaceholderCount++;
            }
            log.AppendLine($"  [INFO] 15. Editor placeholder prefab based contracts: {editorPlaceholderCount}");
            infoCount++;

            // 16. fallback profile 기반 count 출력
            int fallbackCount = 0;
            foreach (var contract in validContracts)
            {
                if (contract.IsFallbackProfile)
                    fallbackCount++;
            }
            log.AppendLine($"  [INFO] 16. Fallback profile based contracts: {fallbackCount}");
            infoCount++;

            // 17. ready count 출력
            int readyCount = 0;
            foreach (var contract in validContracts)
            {
                if (contract.IsFinalContentReady)
                    readyCount++;
            }
            log.AppendLine($"  [INFO] 17. Final content ready count: {readyCount}");
            infoCount++;

            // 18. not ready count 출력
            int notReadyCount = validContracts.Count - readyCount;
            log.AppendLine($"  [INFO] 18. Final content not ready count: {notReadyCount}");
            infoCount++;

            // 19. category별 breakdown 출력
            log.AppendLine("  [INFO] 19. Category breakdown:");
            var categoryBreakdown = new Dictionary<string, int>();
            foreach (var contract in validContracts)
            {
                string cat = contract.RuntimeCategory;
                if (!string.IsNullOrEmpty(cat))
                {
                    if (!categoryBreakdown.ContainsKey(cat))
                        categoryBreakdown[cat] = 0;
                    categoryBreakdown[cat]++;
                }
            }
            foreach (var kvp in categoryBreakdown)
            {
                log.AppendLine($"         {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 20. finalContentKind별 breakdown 출력
            log.AppendLine("  [INFO] 20. FinalContentKind breakdown:");
            var kindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            foreach (var contract in validContracts)
            {
                WorldMapRuntimeFinalContentKind kind = contract.FinalContentKind;
                if (!kindBreakdown.ContainsKey(kind))
                    kindBreakdown[kind] = 0;
                kindBreakdown[kind]++;
            }
            foreach (var kvp in kindBreakdown)
            {
                log.AppendLine($"         {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 21. Scene object transform/name unchanged
            bool transformChanged = false;
            var postValidateSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();
            CaptureTransformSnapshots(generatedRoot.transform, postValidateSnapshots);

            foreach (var kvp in preValidateSnapshots)
            {
                Transform t = kvp.Key;
                if (t == null) continue;

                if (postValidateSnapshots.TryGetValue(t, out var postSnapshot))
                {
                    if (kvp.Value.pos != postSnapshot.pos ||
                        kvp.Value.rot != postSnapshot.rot ||
                        kvp.Value.scale != postSnapshot.scale ||
                        kvp.Value.name != postSnapshot.name)
                    {
                        transformChanged = true;
                        log.AppendLine($"  [FAIL] 21. Transform/name changed: '{kvp.Value.name}' -> '{postSnapshot.name}'");
                        failCount++;
                        break;
                    }
                }
            }

            if (!transformChanged)
            {
                log.AppendLine("  [PASS] 21. Scene object transform/name unchanged");
                passCount++;
            }

            // ===== Summary =====
            log.AppendLine($"===== Validate Summary: PASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount} =====");
            Debug.Log(log.ToString());

            // Console 로그 기반 요약 출력 (DisplayDialog 대체)
            LogValidationSummary(
                "Phase 14.10-M-1: Validate Runtime Final Content Contracts",
                passCount, failCount, warnCount, infoCount,
                $"Total Adapters: {adapterCount}\n" +
                $"Valid Contracts: {contractCount}\n" +
                $"Ready: {readyCount}\n" +
                $"Not Ready: {notReadyCount}\n" +
                $"Requires Replacement: {requiresReplacementCount}\n" +
                $"Fallback: {fallbackCount}\n" +
                $"Editor Placeholder: {editorPlaceholderCount}\n" +
                $"Unknown Kind: {unknownKindCount}");
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// GeneratedWorldRoot를 찾는다. settings와 context를 기반으로 탐색한다.
        /// </summary>
        private static GameObject FindGeneratedRoot(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // Context에 GeneratedRootOverride가 있으면 우선 사용
            if (context != null && context.GeneratedRootOverride != null)
                return context.GeneratedRootOverride;

            // 이름 기반 탐색
            string rootName = settings.GeneratedRootName;
            if (string.IsNullOrEmpty(rootName))
                rootName = "GeneratedWorldRoot";

            GameObject root = GameObject.Find(rootName);
            return root;
        }

        /// <summary>
        /// 지정한 Transform의 ancestor 중 "RuntimeSpawnedInstances" 이름을 가진 object가 있는지 확인한다.
        /// </summary>
        private static bool IsUnderRuntimeSpawnedInstances(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == "RuntimeSpawnedInstances")
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Transform 계층 구조의 position/rotation/scale/name snapshot을 수집한다.
        /// Validate 전후 변경 감지용.
        /// </summary>
        private static void CaptureTransformSnapshots(Transform root, Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)> snapshots)
        {
            if (root == null)
                return;

            snapshots[root] = (root.position, root.rotation, root.localScale, root.name);

            for (int i = 0; i < root.childCount; i++)
            {
                CaptureTransformSnapshots(root.GetChild(i), snapshots);
            }
        }

        /// <summary>
        /// Validation 결과 요약을 Console 로그로 출력한다.
        /// FAIL이 1개 이상이면 Debug.LogError, WARN이 1개 이상이면 Debug.LogWarning, 그 외는 Debug.Log.
        /// </summary>
        private static void LogValidationSummary(
            string phaseName,
            int passCount,
            int failCount,
            int warnCount,
            int infoCount,
            string summary)
        {
            string message =
                $"[{phaseName}] Validation Summary\n" +
                $"PASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount}\n" +
                summary;

            if (failCount > 0)
            {
                Debug.LogError(message);
                return;
            }

            if (warnCount > 0)
            {
                Debug.LogWarning(message);
                return;
            }

            Debug.Log(message);
        }

        /// <summary>
        /// Rebuild 결과를 Console에 로그로 출력한다.
        /// </summary>
        private static void LogRebuildResult(
            int totalAdapters,
            int addedCount,
            int updatedCount,
            int readyCount,
            int notReadyCount,
            int requiresReplacementCount,
            int fallbackCount,
            int editorPlaceholderCount,
            Dictionary<string, int> categoryBreakdown,
            Dictionary<WorldMapRuntimeFinalContentKind, int> finalContentKindBreakdown)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== Phase 14.10-M-1: Rebuild Runtime Final Content Contracts Result =====");
            sb.AppendLine($"Total adapters processed: {totalAdapters}");
            sb.AppendLine($"Added contracts: {addedCount}");
            sb.AppendLine($"Updated contracts: {updatedCount}");
            sb.AppendLine($"Ready count: {readyCount}");
            sb.AppendLine($"Not ready count: {notReadyCount}");
            sb.AppendLine($"Requires real prefab replacement: {requiresReplacementCount}");
            sb.AppendLine($"Fallback count: {fallbackCount}");
            sb.AppendLine($"Editor placeholder prefab count: {editorPlaceholderCount}");

            // Category breakdown
            sb.AppendLine("--- Category Breakdown ---");
            foreach (var kvp in categoryBreakdown)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            // FinalContentKind breakdown
            sb.AppendLine("--- FinalContentKind Breakdown ---");
            foreach (var kvp in finalContentKindBreakdown)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
