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
    /// Phase 14.10-K-4: Runtime Spawn Runtime Readiness 전용 Editor Utility.
    /// PlayMode/gameplay 진입 시 Registry/QueryService가 자동 초기화 가능한지 검증하는 단계.
    /// GeneratedWorldRoot에 Registry/QueryService component를 보강하고,
    /// runtime 초기화 옵션(rebuildOnAwake, initializeOnAwake 등)이 올바르게 설정되었는지 검증한다.
    /// GenerateFullScenarioMap에 통합 완료. 필요 시 독립 호출 가능.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnRuntimeReadinessUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeSpawnInstanceRegistry와
        /// WorldMapRuntimeSpawnQueryService를 부착/보강하고 cache를 재구축한다.
        /// GenerateFullScenarioMap에 통합 완료. 필요 시 독립 호출 가능.
        /// </summary>

        public static void RebuildRuntimeSpawnRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-4] Settings is null! Cannot rebuild runtime spawn runtime readiness.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-K-4: Rebuild Runtime Spawn Runtime Readiness =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-4] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [K-4] Found GeneratedWorldRoot: '{generatedRoot.name}'");

            // 2. GeneratedWorldRoot에 WorldMapRuntimeSpawnInstanceRegistry가 없으면 추가
            WorldMapRuntimeSpawnInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapRuntimeSpawnInstanceRegistry>();
                Undo.RegisterCreatedObjectUndo(registry, "Add WorldMapRuntimeSpawnInstanceRegistry");
                Debug.Log($"[MapAutoBuilder] [K-4] Added WorldMapRuntimeSpawnInstanceRegistry to '{generatedRoot.name}'");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [K-4] WorldMapRuntimeSpawnInstanceRegistry already exists on '{generatedRoot.name}'");
            }

            // 3. GeneratedWorldRoot에 WorldMapRuntimeSpawnQueryService가 없으면 추가
            WorldMapRuntimeSpawnQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeSpawnQueryService>();
            if (queryService == null)
            {
                queryService = generatedRoot.AddComponent<WorldMapRuntimeSpawnQueryService>();
                Undo.RegisterCreatedObjectUndo(queryService, "Add WorldMapRuntimeSpawnQueryService");
                Debug.Log($"[MapAutoBuilder] [K-4] Added WorldMapRuntimeSpawnQueryService to '{generatedRoot.name}'");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [K-4] WorldMapRuntimeSpawnQueryService already exists on '{generatedRoot.name}'");
            }

            // 4. registry.RebuildCacheFromRoot(generatedRoot.transform) 호출
            registry.RebuildCacheFromRoot(generatedRoot.transform);

            // 5. queryService.TryInitializeFromRoot(generatedRoot.transform) 호출
            bool initialized = queryService.TryInitializeFromRoot(generatedRoot.transform);

            // 6. 결과 요약 출력
            Debug.Log($"[MapAutoBuilder] [K-4] Rebuild complete. " +
                $"Registry count: {registry.AllInstances.Count}, " +
                $"QueryService count: {queryService.Count}, " +
                $"QueryService initialized: {initialized}");

            // 7. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-K-4: Rebuild Runtime Spawn Runtime Readiness Complete =====");
        }

        /// <summary>
        /// WorldMapRuntimeSpawnInstanceRegistry와 WorldMapRuntimeSpawnQueryService의
        /// runtime readiness를 검증한다. 18개 항목을 검사하고
        /// Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// Validate는 읽기 전용이어야 하며, GameObject 생성/삭제/이동/이름 변경은 하지 않는다.
        /// 단, registry/query cache가 비어 있으면 cache rebuild/initialize는 허용한다.
        /// GenerateFullScenarioMap에 통합 완료. 필요 시 독립 호출 가능.
        /// </summary>

        public static void ValidateRuntimeSpawnRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-4] Settings is null! Cannot validate runtime spawn runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-K-4: Validate Runtime Spawn Runtime Readiness =====");

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

            // 2. WorldMapRuntimeSpawnInstanceRegistry 존재
            WorldMapRuntimeSpawnInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();
            if (registry == null)
            {
                log.AppendLine("  [FAIL] WorldMapRuntimeSpawnInstanceRegistry not found on GeneratedWorldRoot.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] WorldMapRuntimeSpawnInstanceRegistry found on GeneratedWorldRoot.");
                passCount++;
            }

            // 3. WorldMapRuntimeSpawnQueryService 존재
            WorldMapRuntimeSpawnQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeSpawnQueryService>();
            if (queryService == null)
            {
                log.AppendLine("  [FAIL] WorldMapRuntimeSpawnQueryService not found on GeneratedWorldRoot.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] WorldMapRuntimeSpawnQueryService found on GeneratedWorldRoot.");
                passCount++;
            }

            // Registry나 QueryService가 없으면 더 이상 진행 불가
            if (registry == null || queryService == null)
            {
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }

            // 4. RuntimeSpawnedInstances 하위 scene runtime instance count > 0
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

            // 5. Registry cache가 비어 있으면 자동 rebuild (읽기 전용 예외)
            if (registry.AllInstances.Count == 0 && sceneRuntimeCount > 0)
            {
                log.AppendLine("  [INFO] Registry cache is empty but scene has runtime instances. Auto-rebuilding cache...");
                infoCount++;
                registry.RebuildCacheFromRoot(generatedRoot.transform);
            }

            // 6. Registry.AllInstances count == scene runtime instance count
            if (registry.AllInstances.Count != sceneRuntimeCount)
            {
                log.AppendLine($"  [FAIL] Registry.AllInstances count ({registry.AllInstances.Count}) != scene runtime instance count ({sceneRuntimeCount}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] Registry.AllInstances count ({registry.AllInstances.Count}) matches scene runtime instance count ({sceneRuntimeCount}).");
                passCount++;
            }

            // 7. QueryService.Count == Registry.AllInstances count
            if (queryService.Count != registry.AllInstances.Count)
            {
                log.AppendLine($"  [FAIL] QueryService.Count ({queryService.Count}) != Registry.AllInstances count ({registry.AllInstances.Count}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] QueryService.Count ({queryService.Count}) matches Registry.AllInstances count ({registry.AllInstances.Count}).");
                passCount++;
            }

            // 8. QueryService.HasRegistry == true
            if (!queryService.HasRegistry)
            {
                log.AppendLine("  [FAIL] QueryService.HasRegistry is false. Registry reference may be missing.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] QueryService.HasRegistry is true.");
                passCount++;
            }

            // 9. GetAll count == QueryService.Count
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> allInstances = queryService.GetAll();
            int getAllCount = (allInstances != null) ? allInstances.Count : 0;
            if (getAllCount != queryService.Count)
            {
                log.AppendLine($"  [FAIL] GetAll count ({getAllCount}) != QueryService.Count ({queryService.Count}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] GetAll count ({getAllCount}) matches QueryService.Count ({queryService.Count}).");
                passCount++;
            }

            // 10. GetByZoneId(firstZoneId) count > 0
            string firstZoneId = null;
            if (allInstances != null && allInstances.Count > 0)
            {
                firstZoneId = allInstances[0]?.ZoneId;
            }

            if (string.IsNullOrEmpty(firstZoneId))
            {
                log.AppendLine("  [INFO] Could not determine first ZoneId from GetAll. Skipping GetByZoneId test.");
                infoCount++;
            }
            else
            {
                IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> byZone = queryService.GetByZoneId(firstZoneId);
                if (byZone == null || byZone.Count == 0)
                {
                    log.AppendLine($"  [FAIL] GetByZoneId(\"{firstZoneId}\") returned empty list. Expected > 0 instances.");
                    failCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] GetByZoneId(\"{firstZoneId}\") count: {byZone.Count} (> 0).");
                    passCount++;
                }
            }

            // 11. GetByRuntimeCategory("HarvestResource") 또는 존재하는 첫 번째 category count > 0
            string firstCategory = null;
            if (allInstances != null && allInstances.Count > 0)
            {
                firstCategory = allInstances[0]?.RuntimeCategory;
            }

            if (string.IsNullOrEmpty(firstCategory))
            {
                log.AppendLine("  [INFO] Could not determine first RuntimeCategory from GetAll. Skipping GetByRuntimeCategory test.");
                infoCount++;
            }
            else
            {
                IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> byCategory = queryService.GetByRuntimeCategory(firstCategory);
                if (byCategory == null || byCategory.Count == 0)
                {
                    log.AppendLine($"  [FAIL] GetByRuntimeCategory(\"{firstCategory}\") returned empty list. Expected > 0 instances.");
                    failCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] GetByRuntimeCategory(\"{firstCategory}\") count: {byCategory.Count} (> 0).");
                    passCount++;
                }
            }

            // 12. TryGetByMarkerId(firstMarkerId) 성공
            if (allInstances != null && allInstances.Count > 0)
            {
                WorldMapRuntimeSpawnInstanceTag first = allInstances[0];
                if (first != null && !string.IsNullOrEmpty(first.MarkerId))
                {
                    bool found = queryService.TryGetByMarkerId(first.MarkerId, out WorldMapRuntimeSpawnInstanceTag byMarkerId);
                    if (!found || byMarkerId == null)
                    {
                        log.AppendLine($"  [FAIL] TryGetByMarkerId(\"{first.MarkerId}\") failed. Expected to find instance.");
                        failCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [PASS] TryGetByMarkerId(\"{first.MarkerId}\") succeeded. Found: '{byMarkerId.name}'.");
                        passCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [WARN] First instance has null/empty MarkerId. Skipping TryGetByMarkerId test.");
                    warnCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] No instances available. Skipping TryGetByMarkerId test.");
                warnCount++;
            }

            // 13. TryGetNearest(Vector3.zero, null, out result) 성공
            if (allInstances != null && allInstances.Count > 0)
            {
                bool foundNearest = queryService.TryGetNearest(Vector3.zero, null, out WorldMapRuntimeSpawnInstanceTag nearest);
                if (!foundNearest || nearest == null)
                {
                    log.AppendLine("  [FAIL] TryGetNearest(Vector3.zero, null) failed. Expected to find nearest instance.");
                    failCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] TryGetNearest(Vector3.zero, null) succeeded. Nearest: '{nearest.name}'.");
                    passCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] No instances available. Skipping TryGetNearest test.");
                warnCount++;
            }

            // 14. TryGetNearestInZone(firstZoneId, Vector3.zero, null, out result) 성공
            if (!string.IsNullOrEmpty(firstZoneId))
            {
                bool foundInZone = queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, null, out WorldMapRuntimeSpawnInstanceTag nearestInZone);
                if (!foundInZone || nearestInZone == null)
                {
                    log.AppendLine($"  [INFO] TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero, null) returned no results. May be expected if zone has no instances near origin.");
                    infoCount++;
                }
                else
                {
                    log.AppendLine($"  [PASS] TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero, null) succeeded. Nearest in zone: '{nearestInZone.name}'.");
                    passCount++;
                }
            }
            else
            {
                log.AppendLine("  [INFO] No firstZoneId available. Skipping TryGetNearestInZone test.");
                infoCount++;
            }

            // 15. Preview instance가 QueryService 결과에 포함되지 않음
            bool previewFound = false;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in allInstances)
            {
                if (cached != null && cached.IsPreviewInstance)
                {
                    previewFound = true;
                    log.AppendLine($"  [FAIL] Preview instance found in query results: '{cached.name}' (MarkerId: {cached.MarkerId})");
                    failCount++;
                    break;
                }
            }
            if (!previewFound)
            {
                log.AppendLine("  [PASS] No preview instances in query results.");
                passCount++;
            }

            // 16. RuntimePlaceholder 원본이 QueryService 결과에 포함되지 않음
            bool placeholderFound = false;
            foreach (WorldMapRuntimeSpawnInstanceTag cached in allInstances)
            {
                if (cached != null && cached.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    placeholderFound = true;
                    log.AppendLine($"  [FAIL] RuntimePlaceholder (with WorldMapRuntimePlaceholderBinding) found in query results: '{cached.name}'");
                    failCount++;
                    break;
                }
            }
            if (!placeholderFound)
            {
                log.AppendLine("  [PASS] No RuntimePlaceholders in query results.");
                passCount++;
            }

            // 17. Duplicate MarkerIds count == 0
            int duplicateCount = registry.DuplicateMarkerIds.Count;
            if (duplicateCount > 0)
            {
                log.AppendLine($"  [WARN] Duplicate MarkerId count: {duplicateCount}. Expected 0.");
                warnCount++;
                foreach (string dupId in registry.DuplicateMarkerIds)
                {
                    log.AppendLine($"    Duplicate MarkerId: '{dupId}'");
                }
            }
            else
            {
                log.AppendLine("  [PASS] No duplicate MarkerIds. (count: 0)");
                passCount++;
            }

            // 18. Scene object transform/name 변경 없음
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
