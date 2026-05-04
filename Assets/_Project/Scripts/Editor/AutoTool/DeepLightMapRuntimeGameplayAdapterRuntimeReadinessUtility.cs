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
    /// Phase 14.10-L-3: Runtime Gameplay Adapter Runtime Readiness 전용 Editor Utility.
    /// PlayMode/gameplay 진입 시 WorldMapRuntimeGameplayAdapterRegistry와
    /// WorldMapRuntimeGameplayAdapterQueryService가 자동 초기화 가능한지 검증하는 단계.
    /// GeneratedWorldRoot에 Registry/QueryService component를 보강하고,
    /// runtime 초기화 옵션이 올바르게 설정되었는지 검증한다.
    /// 아직 GenerateFullScenarioMap에 통합되지 않음. 독립 실행 전용.
    /// </summary>
    public static class DeepLightMapRuntimeGameplayAdapterRuntimeReadinessUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeGameplayAdapterRegistry와
        /// WorldMapRuntimeGameplayAdapterQueryService를 부착/보강하고 cache를 재구축한다.
        /// 아직 GenerateFullScenarioMap에 통합되지 않음. 독립 실행 전용.
        /// </summary>
        public static void RebuildRuntimeGameplayAdapterRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-3] Settings is null! Cannot rebuild runtime gameplay adapter runtime readiness.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-L-3: Rebuild Runtime Gameplay Adapter Runtime Readiness =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-3] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [L-3] Found GeneratedWorldRoot: '{generatedRoot.name}'");

            // 2. GeneratedWorldRoot에 WorldMapRuntimeGameplayAdapterRegistry가 없으면 추가
            WorldMapRuntimeGameplayAdapterRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapRuntimeGameplayAdapterRegistry>();
                Undo.RegisterCreatedObjectUndo(registry, "Add WorldMapRuntimeGameplayAdapterRegistry");
                Debug.Log($"[MapAutoBuilder] [L-3] Added WorldMapRuntimeGameplayAdapterRegistry to '{generatedRoot.name}'");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [L-3] WorldMapRuntimeGameplayAdapterRegistry already exists on '{generatedRoot.name}'");
            }

            // 3. GeneratedWorldRoot에 WorldMapRuntimeGameplayAdapterQueryService가 없으면 추가
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null)
            {
                queryService = generatedRoot.AddComponent<WorldMapRuntimeGameplayAdapterQueryService>();
                Undo.RegisterCreatedObjectUndo(queryService, "Add WorldMapRuntimeGameplayAdapterQueryService");
                Debug.Log($"[MapAutoBuilder] [L-3] Added WorldMapRuntimeGameplayAdapterQueryService to '{generatedRoot.name}'");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [L-3] WorldMapRuntimeGameplayAdapterQueryService already exists on '{generatedRoot.name}'");
            }

            // 4. Registry.RebuildCacheFromRoot(generatedRoot.transform) 호출
            registry.RebuildCacheFromRoot(generatedRoot.transform);

            // 5. QueryService.TryInitializeFromRoot(generatedRoot.transform) 호출
            bool initialized = queryService.TryInitializeFromRoot(generatedRoot.transform);

            // 6. 결과 요약 출력
            Debug.Log($"[MapAutoBuilder] [L-3] Rebuild complete. " +
                $"Registry count: {registry.Count}, " +
                $"QueryService count: {queryService.Count}, " +
                $"QueryService initialized: {initialized}");

            // 7. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-L-3: Rebuild Runtime Gameplay Adapter Runtime Readiness Complete =====");
        }

        /// <summary>
        /// WorldMapRuntimeGameplayAdapterRegistry와 WorldMapRuntimeGameplayAdapterQueryService의
        /// runtime readiness를 검증한다. 25개 항목을 검사하고
        /// Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// Validate는 읽기 전용이어야 하며, GameObject 생성/삭제/이동/이름 변경은 하지 않는다.
        /// 단, registry/query cache가 비어 있으면 cache rebuild/initialize는 허용한다.
        /// 아직 GenerateFullScenarioMap에 통합되지 않음. 독립 실행 전용.
        /// </summary>
        public static void ValidateRuntimeGameplayAdapterRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-3] Settings is null! Cannot validate runtime gameplay adapter runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-L-3: Validate Runtime Gameplay Adapter Runtime Readiness =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // Validate 시작 전 GeneratedWorldRoot 하위 transform/name snapshot 저장
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine("  [FAIL] 1. GeneratedWorldRoot exists: NOT FOUND. Run Generate Full Scenario Map first.");
                failCount++;
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }
            log.AppendLine($"  [PASS] 1. GeneratedWorldRoot exists: '{generatedRoot.name}'");
            passCount++;

            // Snapshot: Validate 시작 전 transform/name 상태 저장
            var preValidateSnapshot = CaptureTransformSnapshot(generatedRoot.transform);

            // 2. WorldMapRuntimeGameplayAdapterRegistry 존재
            WorldMapRuntimeGameplayAdapterRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterRegistry>();
            if (registry == null)
            {
                log.AppendLine("  [FAIL] 2. WorldMapRuntimeGameplayAdapterRegistry exists: NOT FOUND on GeneratedWorldRoot.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 2. WorldMapRuntimeGameplayAdapterRegistry exists on GeneratedWorldRoot.");
                passCount++;
            }

            // 3. WorldMapRuntimeGameplayAdapterQueryService 존재
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null)
            {
                log.AppendLine("  [FAIL] 3. WorldMapRuntimeGameplayAdapterQueryService exists: NOT FOUND on GeneratedWorldRoot.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 3. WorldMapRuntimeGameplayAdapterQueryService exists on GeneratedWorldRoot.");
                passCount++;
            }

            // Registry나 QueryService가 없으면 더 이상 진행 불가
            if (registry == null || queryService == null)
            {
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }

            // 4. RuntimeSpawnedInstances 하위 scene adapter count > 0
            int sceneAdapterCount = CountSceneAdapters(generatedRoot.transform);
            if (sceneAdapterCount == 0)
            {
                log.AppendLine("  [FAIL] 4. Scene adapter count > 0: 0. Expected > 0 adapters under RuntimeSpawnedInstances.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 4. Scene adapter count > 0: {sceneAdapterCount}");
                passCount++;
            }

            // 5. Registry cache가 비어 있으면 자동 rebuild (읽기 전용 예외)
            if (registry.Count == 0 && sceneAdapterCount > 0)
            {
                log.AppendLine("  [INFO] Registry cache is empty but scene has adapters. Auto-rebuilding cache...");
                infoCount++;
                registry.RebuildCacheFromRoot(generatedRoot.transform);
            }

            // QueryService cache가 비어 있으면 자동 initialize (읽기 전용 예외)
            if (queryService.Count == 0 && sceneAdapterCount > 0)
            {
                log.AppendLine("  [INFO] QueryService cache is empty but scene has adapters. Auto-initializing...");
                infoCount++;
                queryService.TryInitializeFromRoot(generatedRoot.transform);
            }

            // 5. Registry.AllAdapters count == scene adapter count
            int registryCount = registry.Count;
            if (registryCount != sceneAdapterCount)
            {
                log.AppendLine($"  [FAIL] 5. Registry.AllAdapters count ({registryCount}) != scene adapter count ({sceneAdapterCount}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 5. Registry.AllAdapters count ({registryCount}) == scene adapter count ({sceneAdapterCount}).");
                passCount++;
            }

            // 6. QueryService.Count == Registry.AllAdapters count
            int queryCount = queryService.Count;
            if (queryCount != registryCount)
            {
                log.AppendLine($"  [FAIL] 6. QueryService.Count ({queryCount}) != Registry.AllAdapters count ({registryCount}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 6. QueryService.Count ({queryCount}) == Registry.AllAdapters count ({registryCount}).");
                passCount++;
            }

            // 7. QueryService.HasRegistry == true
            if (!queryService.HasRegistry)
            {
                log.AppendLine("  [FAIL] 7. QueryService.HasRegistry == true: false. Registry reference may be missing.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 7. QueryService.HasRegistry == true.");
                passCount++;
            }

            // 8. QueryService.GetAll count == QueryService.Count
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
            int getAllCount = (allAdapters != null) ? allAdapters.Count : 0;
            if (getAllCount != queryCount)
            {
                log.AppendLine($"  [FAIL] 8. QueryService.GetAll count ({getAllCount}) != QueryService.Count ({queryCount}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 8. QueryService.GetAll count ({getAllCount}) == QueryService.Count ({queryCount}).");
                passCount++;
            }

            // 9. GetByZoneId("A1") 또는 존재 zone 기준 count > 0
            string firstZoneId = null;
            if (allAdapters != null && allAdapters.Count > 0)
            {
                firstZoneId = allAdapters[0]?.ZoneId;
            }

            if (string.IsNullOrEmpty(firstZoneId))
            {
                log.AppendLine("  [INFO] 9. Could not determine first ZoneId from GetAll. Skipping GetByZoneId test.");
                infoCount++;
            }
            else
            {
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> byZone = queryService.GetByZoneId(firstZoneId);
                if (byZone == null || byZone.Count == 0)
                {
                    log.AppendLine($"  [FAIL] 9. GetByZoneId(\"{firstZoneId}\") returned empty list. Expected > 0 adapters.");
                    failCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] 9. GetByZoneId(\"{firstZoneId}\") count: {byZone.Count} (> 0).");
                    passCount++;
                }
            }

            // 10. GetByRuntimeCategory("HarvestResource") count > 0
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> harvestResult = queryService.GetByRuntimeCategory("HarvestResource");
            if (harvestResult == null || harvestResult.Count == 0)
            {
                log.AppendLine("  [WARN] 10. GetByRuntimeCategory('HarvestResource') count > 0: 0. No HarvestResource adapters found.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 10. GetByRuntimeCategory('HarvestResource') count: {harvestResult.Count} (> 0).");
                passCount++;
            }

            // 11. GetByAdapterKind(HarvestResource) count > 0
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> kindResult = queryService.GetByAdapterKind(WorldMapRuntimeGameplayAdapterKind.HarvestResource);
            if (kindResult == null || kindResult.Count == 0)
            {
                log.AppendLine("  [WARN] 11. GetByAdapterKind(HarvestResource) count > 0: 0. No HarvestResource adapters found.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 11. GetByAdapterKind(HarvestResource) count: {kindResult.Count} (> 0).");
                passCount++;
            }

            // 12. TryGetByMarkerId(firstMarkerId) 성공
            string firstMarkerId = null;
            if (allAdapters != null && allAdapters.Count > 0)
            {
                firstMarkerId = allAdapters[0]?.MarkerId;
            }

            if (!string.IsNullOrEmpty(firstMarkerId))
            {
                bool found = queryService.TryGetByMarkerId(firstMarkerId, out WorldMapRuntimeSpawnGameplayAdapter byMarkerId);
                if (!found || byMarkerId == null)
                {
                    log.AppendLine($"  [FAIL] 12. TryGetByMarkerId(\"{firstMarkerId}\") failed. Expected to find adapter.");
                    failCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] 12. TryGetByMarkerId(\"{firstMarkerId}\") succeeded. Found: '{byMarkerId.name}'.");
                    passCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] 12. First adapter has null/empty MarkerId. Skipping TryGetByMarkerId test.");
                warnCount++;
            }

            // 13. TryGetNearest(Vector3.zero, None) 성공
            if (allAdapters != null && allAdapters.Count > 0)
            {
                bool foundNearest = queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeGameplayAdapterKind.None, out WorldMapRuntimeSpawnGameplayAdapter nearestNone);
                if (!foundNearest || nearestNone == null)
                {
                    log.AppendLine("  [FAIL] 13. TryGetNearest(Vector3.zero, None) failed. Expected to find nearest adapter.");
                    failCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] 13. TryGetNearest(Vector3.zero, None) succeeded. Nearest: '{nearestNone.name}'.");
                    passCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] 13. No adapters available. Skipping TryGetNearest(None) test.");
                warnCount++;
            }

            // 14. TryGetNearest(Vector3.zero, HarvestResource) 성공
            if (harvestResult != null && harvestResult.Count > 0)
            {
                bool foundNearestHarvest = queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeGameplayAdapterKind.HarvestResource, out WorldMapRuntimeSpawnGameplayAdapter nearestHarvest);
                if (!foundNearestHarvest || nearestHarvest == null)
                {
                    log.AppendLine("  [FAIL] 14. TryGetNearest(Vector3.zero, HarvestResource) failed. Expected to find nearest HarvestResource adapter.");
                    failCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] 14. TryGetNearest(Vector3.zero, HarvestResource) succeeded. Nearest: '{nearestHarvest.name}'.");
                    passCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] 14. No HarvestResource adapters available. Skipping TryGetNearest(HarvestResource) test.");
                warnCount++;
            }

            // 15. TryGetNearestInZone(firstZoneId, Vector3.zero, None) 성공
            if (!string.IsNullOrEmpty(firstZoneId))
            {
                bool foundInZone = queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, WorldMapRuntimeGameplayAdapterKind.None, out WorldMapRuntimeSpawnGameplayAdapter nearestInZone);
                if (!foundInZone || nearestInZone == null)
                {
                    log.AppendLine($"  [INFO] 15. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero, None) returned no results. May be expected if zone has no adapters near origin.");
                    infoCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] 15. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero, None) succeeded. Nearest in zone: '{nearestInZone.name}'.");
                    passCount++;
                }
            }
            else
            {
                log.AppendLine("  [INFO] 15. No firstZoneId available. Skipping TryGetNearestInZone test.");
                infoCount++;
            }

            // 16. Preview instance가 QueryService 결과에 포함되지 않음
            bool previewFound = false;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                WorldMapRuntimeSpawnInstanceTag tag = adapter.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                if (tag != null && tag.IsPreviewInstance)
                {
                    previewFound = true;
                    log.AppendLine($"  [FAIL] 16. Preview instance found in query results: '{adapter.name}' (MarkerId: {adapter.MarkerId})");
                    failCount++;
                    break;
                }
            }
            if (!previewFound)
            {
                log.AppendLine("  [PASS] 16. Preview instance not included in query results.");
                passCount++;
            }

            // 17. RuntimePlaceholder 원본이 QueryService 결과에 포함되지 않음
            bool placeholderFound = false;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                if (adapter.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    placeholderFound = true;
                    log.AppendLine($"  [FAIL] 17. RuntimePlaceholder (with WorldMapRuntimePlaceholderBinding) found in query results: '{adapter.name}'");
                    failCount++;
                    break;
                }
            }
            if (!placeholderFound)
            {
                log.AppendLine("  [PASS] 17. RuntimePlaceholder not included in query results.");
                passCount++;
            }

            // 18. WorldMapZoneContentMarker object가 QueryService 결과에 포함되지 않음
            bool markerFound = false;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                if (adapter.GetComponent<WorldMapZoneContentMarker>() != null)
                {
                    markerFound = true;
                    log.AppendLine($"  [FAIL] 18. WorldMapZoneContentMarker found in query results: '{adapter.name}'");
                    failCount++;
                    break;
                }
            }
            if (!markerFound)
            {
                log.AppendLine("  [PASS] 18. WorldMapZoneContentMarker not included in query results.");
                passCount++;
            }

            // 19. Duplicate MarkerIds count == 0
            int duplicateCount = registry.DuplicateMarkerIds.Count;
            if (duplicateCount > 0)
            {
                log.AppendLine($"  [FAIL] 19. Duplicate MarkerIds count == 0: {duplicateCount}. Expected 0.");
                failCount++;
                foreach (string dupId in registry.DuplicateMarkerIds)
                {
                    log.AppendLine($"         Duplicate MarkerId: '{dupId}'");
                }
            }
            else
            {
                log.AppendLine("  [PASS] 19. Duplicate MarkerIds count == 0.");
                passCount++;
            }

            // 20. 모든 adapter의 ZoneId non-empty
            bool allZoneIdValid = true;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.ZoneId))
                {
                    allZoneIdValid = false;
                    log.AppendLine($"  [FAIL] 20. Adapter '{adapter.name}' has empty ZoneId.");
                    failCount++;
                    break;
                }
            }
            if (allZoneIdValid)
            {
                log.AppendLine("  [PASS] 20. All adapters have non-empty ZoneId.");
                passCount++;
            }

            // 21. 모든 adapter의 MarkerId non-empty
            bool allMarkerIdValid = true;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.MarkerId))
                {
                    allMarkerIdValid = false;
                    log.AppendLine($"  [FAIL] 21. Adapter '{adapter.name}' has empty MarkerId.");
                    failCount++;
                    break;
                }
            }
            if (allMarkerIdValid)
            {
                log.AppendLine("  [PASS] 21. All adapters have non-empty MarkerId.");
                passCount++;
            }

            // 22. 모든 adapter의 RuntimeCategory non-empty
            bool allCategoryValid = true;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.RuntimeCategory))
                {
                    allCategoryValid = false;
                    log.AppendLine($"  [FAIL] 22. Adapter '{adapter.name}' has empty RuntimeCategory.");
                    failCount++;
                    break;
                }
            }
            if (allCategoryValid)
            {
                log.AppendLine("  [PASS] 22. All adapters have non-empty RuntimeCategory.");
                passCount++;
            }

            // 23. 모든 adapter의 ProfileId non-empty
            bool allProfileIdValid = true;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.ProfileId))
                {
                    allProfileIdValid = false;
                    log.AppendLine($"  [FAIL] 23. Adapter '{adapter.name}' has empty ProfileId.");
                    failCount++;
                    break;
                }
            }
            if (allProfileIdValid)
            {
                log.AppendLine("  [PASS] 23. All adapters have non-empty ProfileId.");
                passCount++;
            }

            // 24. 모든 adapter의 AdapterKind != None
            bool allKindValid = true;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;
                if (adapter.AdapterKind == WorldMapRuntimeGameplayAdapterKind.None)
                {
                    allKindValid = false;
                    log.AppendLine($"  [FAIL] 24. Adapter '{adapter.name}' has AdapterKind=None (MarkerId: {adapter.MarkerId}).");
                    failCount++;
                    break;
                }
            }
            if (allKindValid)
            {
                log.AppendLine("  [PASS] 24. All adapters have AdapterKind != None.");
                passCount++;
            }

            // 25. Scene object transform/name unchanged
            var postValidateSnapshot = CaptureTransformSnapshot(generatedRoot.transform);
            bool snapshotChanged = HasSnapshotChanged(preValidateSnapshot, postValidateSnapshot);
            if (snapshotChanged)
            {
                log.AppendLine("  [FAIL] 25. Scene object transform/name changed during validation. Validate should be read-only.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 25. Scene object transform/name unchanged (validate is read-only).");
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
        /// RuntimeSpawnedInstances 하위의 adapter 개수를 scene scan으로 계산한다.
        /// Registry의 필터 로직과 동일한 조건을 사용한다.
        /// </summary>
        private static int CountSceneAdapters(Transform root)
        {
            if (root == null)
                return 0;

            WorldMapRuntimeSpawnGameplayAdapter[] allAdapters = root.GetComponentsInChildren<WorldMapRuntimeSpawnGameplayAdapter>(true);
            int count = 0;

            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null || adapter.gameObject == null)
                    continue;

                // RuntimeSpawnedInstances 하위인지 확인
                if (!IsUnderRuntimeSpawnedInstances(adapter.transform))
                    continue;

                // Preview instance 제외
                WorldMapRuntimeSpawnInstanceTag tag = adapter.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                if (tag != null && tag.IsPreviewInstance)
                    continue;

                // RuntimePlaceholderBinding 제외
                if (adapter.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // WorldMapZoneContentMarker 제외
                if (adapter.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                count++;
            }

            return count;
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
