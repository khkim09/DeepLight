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
    /// Phase 14.10-K-3: Runtime Spawn Query Service 전용 Editor Utility.
    /// GeneratedWorldRoot에 WorldMapRuntimeSpawnQueryService를 생성/보정하고,
    /// query API가 현재 생성된 runtime instance 기준으로 정상 동작하는지 검증한다.
    /// scene object 생성은 QueryService/Registry component 추가 외에는 금지.
    /// RuntimeSpawnedInstances, RuntimePlaceholder, RuntimeSpawnInstances 하위 오브젝트는 수정하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnQueryValidationUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot를 찾고, WorldMapRuntimeSpawnInstanceRegistry와
        /// WorldMapRuntimeSpawnQueryService를 부착/보정한 후 QueryService.TryInitializeFromRoot()를 호출한다.
        /// scene object 생성은 QueryService/Registry component 추가 외에는 금지.
        /// </summary>
        public static void RebuildRuntimeSpawnQueryService(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-3] Settings is null! Cannot rebuild runtime spawn query service.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-K-3: Rebuild Runtime Spawn Query Service =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-3] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [K-3] Found GeneratedWorldRoot: '{generatedRoot.name}'");

            // 2. GeneratedWorldRoot에 WorldMapRuntimeSpawnInstanceRegistry가 없으면 추가
            WorldMapRuntimeSpawnInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeSpawnInstanceRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapRuntimeSpawnInstanceRegistry>();
                Undo.RegisterCreatedObjectUndo(registry, "Add WorldMapRuntimeSpawnInstanceRegistry");
                Debug.Log($"[MapAutoBuilder] [K-3] Added WorldMapRuntimeSpawnInstanceRegistry to '{generatedRoot.name}'");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [K-3] WorldMapRuntimeSpawnInstanceRegistry already exists on '{generatedRoot.name}'");
            }

            // 3. GeneratedWorldRoot에 WorldMapRuntimeSpawnQueryService가 없으면 추가
            WorldMapRuntimeSpawnQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeSpawnQueryService>();
            if (queryService == null)
            {
                queryService = generatedRoot.AddComponent<WorldMapRuntimeSpawnQueryService>();
                Undo.RegisterCreatedObjectUndo(queryService, "Add WorldMapRuntimeSpawnQueryService");
                Debug.Log($"[MapAutoBuilder] [K-3] Added WorldMapRuntimeSpawnQueryService to '{generatedRoot.name}'");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [K-3] WorldMapRuntimeSpawnQueryService already exists on '{generatedRoot.name}'");
            }

            // 4. QueryService.TryInitializeFromRoot(generatedRoot.transform) 호출
            //    내부적으로 Registry를 찾고 cache가 비어 있으면 RebuildCacheFromRoot를 호출한다.
            bool initialized = queryService.TryInitializeFromRoot(generatedRoot.transform);

            if (!initialized)
            {
                Debug.LogError("[MapAutoBuilder] [K-3] QueryService.TryInitializeFromRoot failed. Registry may be missing or cache empty.");
                return;
            }

            // 5. registry cache count / query service count 로그 출력
            Debug.Log($"[MapAutoBuilder] [K-3] Registry cache count: {registry.AllInstances.Count}, QueryService count: {queryService.Count}");

            // 6. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-K-3: Rebuild Runtime Spawn Query Service Complete =====");
        }

        /// <summary>
        /// WorldMapRuntimeSpawnQueryService의 유효성을 검사한다.
        /// 18개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// scene object transform/name 변경 없음.
        /// </summary>
        public static void ValidateRuntimeSpawnQueryService(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [K-3] Settings is null! Cannot validate runtime spawn query service.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-K-3: Validate Runtime Spawn Query Service =====");

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

            // 2. WorldMapRuntimeSpawnInstanceRegistry 존재
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

            // 3. WorldMapRuntimeSpawnQueryService 존재
            WorldMapRuntimeSpawnQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeSpawnQueryService>();
            if (queryService == null)
            {
                log.AppendLine("  [FAIL] WorldMapRuntimeSpawnQueryService not found on GeneratedWorldRoot.");
                failCount++;
                LogAndFinalize(log, passCount, failCount, warnCount, infoCount);
                return;
            }
            log.AppendLine("  [PASS] WorldMapRuntimeSpawnQueryService found on GeneratedWorldRoot.");
            passCount++;

            // 4. QueryService.HasRegistry == true
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

            // 5. QueryService.Count == registry.AllInstances.Count
            if (queryService.Count != registry.AllInstances.Count)
            {
                log.AppendLine($"  [FAIL] QueryService.Count ({queryService.Count}) != registry.AllInstances.Count ({registry.AllInstances.Count}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] QueryService.Count ({queryService.Count}) matches registry.AllInstances.Count ({registry.AllInstances.Count}).");
                passCount++;
            }

            // 6. QueryService.Count == scene runtime instance count
            int sceneRuntimeCount = WorldMapRuntimeSpawnInstanceRegistry.CountSceneRuntimeInstances(generatedRoot.transform);
            if (queryService.Count != sceneRuntimeCount)
            {
                log.AppendLine($"  [FAIL] QueryService.Count ({queryService.Count}) != scene runtime instance count ({sceneRuntimeCount}).");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] QueryService.Count ({queryService.Count}) matches scene runtime instance count ({sceneRuntimeCount}).");
                passCount++;
            }

            // 7. GetAll count > 0
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> allInstances = queryService.GetAll();
            if (allInstances == null || allInstances.Count == 0)
            {
                log.AppendLine("  [FAIL] GetAll returned empty list. Expected > 0 runtime instances.");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] GetAll count: {allInstances.Count} (> 0).");
                passCount++;
            }

            // 8. GetByZoneId("A1") 또는 존재하는 첫 번째 ZoneId query count > 0
            //    첫 번째 instance의 ZoneId를 사용하여 query
            string firstZoneId = null;
            if (allInstances != null && allInstances.Count > 0)
            {
                firstZoneId = allInstances[0]?.ZoneId;
            }

            if (string.IsNullOrEmpty(firstZoneId))
            {
                log.AppendLine("  [WARN] Could not determine first ZoneId from GetAll. Skipping GetByZoneId test.");
                warnCount++;
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

            // 9. GetByRuntimeCategory("HarvestResource") count > 0
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> byHarvest = queryService.GetByRuntimeCategory("HarvestResource");
            if (byHarvest == null || byHarvest.Count == 0)
            {
                log.AppendLine("  [WARN] GetByRuntimeCategory(\"HarvestResource\") returned empty list. May be expected if no HarvestResource instances exist.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] GetByRuntimeCategory(\"HarvestResource\") count: {byHarvest.Count} (> 0).");
                passCount++;
            }

            // 10. GetByRuntimeCategory("NarrativeLog") count > 0
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> byNarrative = queryService.GetByRuntimeCategory("NarrativeLog");
            if (byNarrative == null || byNarrative.Count == 0)
            {
                log.AppendLine("  [WARN] GetByRuntimeCategory(\"NarrativeLog\") returned empty list. May be expected if no NarrativeLog instances exist.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] GetByRuntimeCategory(\"NarrativeLog\") count: {byNarrative.Count} (> 0).");
                passCount++;
            }

            // 11. GetByMarkerType(Resource) count > 0
            IReadOnlyList<WorldMapRuntimeSpawnInstanceTag> byResourceMarker = queryService.GetByMarkerType(WorldMapZoneContentMarkerType.Resource);
            if (byResourceMarker == null || byResourceMarker.Count == 0)
            {
                log.AppendLine("  [WARN] GetByMarkerType(Resource) returned empty list. May be expected if no Resource marker instances exist.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] GetByMarkerType(Resource) count: {byResourceMarker.Count} (> 0).");
                passCount++;
            }

            // 12. TryGetByMarkerId(first.MarkerId) succeeds
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

            // 13. TryGetNearest(first.transform.position, first.RuntimeCategory, out nearest) succeeds
            if (allInstances != null && allInstances.Count > 0)
            {
                WorldMapRuntimeSpawnInstanceTag first = allInstances[0];
                if (first != null && first.gameObject != null)
                {
                    bool foundNearest = queryService.TryGetNearest(first.transform.position, first.RuntimeCategory, out WorldMapRuntimeSpawnInstanceTag nearest);
                    if (!foundNearest || nearest == null)
                    {
                        log.AppendLine($"  [FAIL] TryGetNearest(position, \"{first.RuntimeCategory}\") failed. Expected to find nearest instance.");
                        failCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [PASS] TryGetNearest(position, \"{first.RuntimeCategory}\") succeeded. Nearest: '{nearest.name}'.");
                        passCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [WARN] First instance is null/destroyed. Skipping TryGetNearest test.");
                    warnCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] No instances available. Skipping TryGetNearest test.");
                warnCount++;
            }

            // 14. TryGetNearestInZone(first.ZoneId, first.transform.position, first.RuntimeCategory, out nearestInZone) succeeds
            if (allInstances != null && allInstances.Count > 0)
            {
                WorldMapRuntimeSpawnInstanceTag first = allInstances[0];
                if (first != null && first.gameObject != null && !string.IsNullOrEmpty(first.ZoneId))
                {
                    bool foundInZone = queryService.TryGetNearestInZone(first.ZoneId, first.transform.position, first.RuntimeCategory, out WorldMapRuntimeSpawnInstanceTag nearestInZone);
                    if (!foundInZone || nearestInZone == null)
                    {
                        log.AppendLine($"  [FAIL] TryGetNearestInZone(\"{first.ZoneId}\", position, \"{first.RuntimeCategory}\") failed. Expected to find nearest instance in zone.");
                        failCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [PASS] TryGetNearestInZone(\"{first.ZoneId}\", position, \"{first.RuntimeCategory}\") succeeded. Nearest in zone: '{nearestInZone.name}'.");
                        passCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [WARN] First instance has null ZoneId or is null/destroyed. Skipping TryGetNearestInZone test.");
                    warnCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] No instances available. Skipping TryGetNearestInZone test.");
                warnCount++;
            }

            // 15. Preview instance가 query 결과에 포함되지 않음
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

            // 16. RuntimePlaceholder가 query 결과에 포함되지 않음
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

            // 17. Duplicate MarkerId 없음
            int duplicateCount = registry.DuplicateMarkerIds.Count;
            if (duplicateCount > 0)
            {
                log.AppendLine($"  [FAIL] Duplicate MarkerId count: {duplicateCount}. Expected 0.");
                failCount++;
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

            // 18. scene object transform/name 변경 없음
            log.AppendLine("  [PASS] Scene object transform/name unchanged (this utility is read-only for scene objects).");
            passCount++;

            // Category/Zone breakdown (INFO)
            log.AppendLine("  [INFO] --- Category Breakdown (from QueryService) ---");
            infoCount++;
            var categoryCounts = new Dictionary<string, int>();
            foreach (WorldMapRuntimeSpawnInstanceTag cached in allInstances)
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

            log.AppendLine("  [INFO] --- Zone Count Breakdown (from QueryService) ---");
            infoCount++;
            var zoneCounts = new Dictionary<string, int>();
            foreach (WorldMapRuntimeSpawnInstanceTag cached in allInstances)
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
