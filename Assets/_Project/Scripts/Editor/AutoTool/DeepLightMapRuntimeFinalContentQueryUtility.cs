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
    /// Phase 14.10-M-6: Runtime Final Content Query 전용 Editor Utility.
    /// GeneratedWorldRoot에 WorldMapRuntimeFinalContentInstanceRegistry와
    /// WorldMapRuntimeFinalContentQueryService를 부착/갱신하고,
    /// cache rebuild 및 검증을 수행한다.
    /// RuntimeFinalContentInstances 하위 final content instance를 생성/삭제하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeFinalContentQueryUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[M-6 FinalContentQuery]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot를 찾고, WorldMapRuntimeFinalContentInstanceRegistry와
        /// WorldMapRuntimeFinalContentQueryService를 부착/갱신한 후
        /// registry.RebuildCacheFromRoot(generatedRoot.transform) 및
        /// queryService.TryInitializeFromRoot(generatedRoot.transform)을 호출한다.
        /// RuntimeFinalContentInstances 하위 final content instance를 생성/삭제하지 않는다.
        /// </summary>
        public static void RebuildRuntimeFinalContentQuery(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-6] Settings is null! Cannot rebuild runtime final content query.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-M-6: Rebuild Runtime Final Content Query =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. GeneratedWorldRoot에 WorldMapRuntimeFinalContentInstanceRegistry가 없으면 추가
            WorldMapRuntimeFinalContentInstanceRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeFinalContentInstanceRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapRuntimeFinalContentInstanceRegistry>();
                Undo.RegisterCreatedObjectUndo(registry, "Add WorldMapRuntimeFinalContentInstanceRegistry");
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeFinalContentInstanceRegistry to '{generatedRoot.name}'");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] WorldMapRuntimeFinalContentInstanceRegistry already exists on '{generatedRoot.name}'");
            }

            // 3. registry.RebuildCacheFromRoot(generatedRoot.transform) 호출
            registry.RebuildCacheFromRoot(generatedRoot.transform);

            // 4. GeneratedWorldRoot에 WorldMapRuntimeFinalContentQueryService가 없으면 추가
            WorldMapRuntimeFinalContentQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeFinalContentQueryService>();
            if (queryService == null)
            {
                queryService = generatedRoot.AddComponent<WorldMapRuntimeFinalContentQueryService>();
                Undo.RegisterCreatedObjectUndo(queryService, "Add WorldMapRuntimeFinalContentQueryService");
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeFinalContentQueryService to '{generatedRoot.name}'");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] WorldMapRuntimeFinalContentQueryService already exists on '{generatedRoot.name}'");
            }

            // 5. queryService.TryInitializeFromRoot(generatedRoot.transform) 호출
            bool initialized = queryService.TryInitializeFromRoot(generatedRoot.transform);

            // 6. 결과 요약 출력
            log.AppendLine($"{LogPrefix} [INFO] Final content instance count (scene): {WorldMapRuntimeFinalContentInstanceRegistry.CountSceneFinalContentInstances(generatedRoot.transform)}");
            log.AppendLine($"{LogPrefix} [INFO] Registry count: {registry.Count}");
            log.AppendLine($"{LogPrefix} [INFO] QueryService count: {queryService.Count}");
            log.AppendLine($"{LogPrefix} [INFO] QueryService.HasRegistry: {queryService.HasRegistry}");
            log.AppendLine($"{LogPrefix} [INFO] Duplicate SourceMarkerIds: {registry.DuplicateSourceMarkerIds.Count}");

            // Category breakdown
            log.AppendLine($"{LogPrefix} [INFO] Category breakdown:");
            IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> allInstances = registry.AllInstances;
            var categoryCounts = new Dictionary<string, int>();
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
            {
                if (tag == null) continue;
                string cat = tag.RuntimeCategory;
                if (!string.IsNullOrEmpty(cat))
                {
                    if (!categoryCounts.ContainsKey(cat))
                        categoryCounts[cat] = 0;
                    categoryCounts[cat]++;
                }
            }
            foreach (var kvp in categoryCounts)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            // FinalContentKind breakdown
            log.AppendLine($"{LogPrefix} [INFO] FinalContentKind breakdown:");
            var kindCounts = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
            {
                if (tag == null) continue;
                WorldMapRuntimeFinalContentKind kind = tag.FinalContentKind;
                if (!kindCounts.ContainsKey(kind))
                    kindCounts[kind] = 0;
                kindCounts[kind]++;
            }
            foreach (var kvp in kindCounts)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            // Zone count breakdown
            log.AppendLine($"{LogPrefix} [INFO] Zone count breakdown:");
            var zoneCounts = new Dictionary<string, int>();
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
            {
                if (tag == null) continue;
                string zoneId = tag.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!zoneCounts.ContainsKey(zoneId))
                        zoneCounts[zoneId] = 0;
                    zoneCounts[zoneId]++;
                }
            }
            foreach (var kvp in zoneCounts)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            // 7. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-M-6: Rebuild Runtime Final Content Query Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// WorldMapRuntimeFinalContentInstanceRegistry와 WorldMapRuntimeFinalContentQueryService의
        /// 유효성을 33개 항목으로 검사하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// scene object transform/name 변경 없음. Read-only 검증.
        /// 검증 시작 시 registry cache가 비어 있더라도 scene final content instance count가 0이 아니라면,
        /// registry.RebuildCacheFromRoot(generatedRoot.transform)을 한 번 호출한 뒤 검증한다.
        /// </summary>
        public static void ValidateRuntimeFinalContentQuery(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-6] Settings is null! Cannot validate runtime final content query.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-M-6: Validate Runtime Final Content Query =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            int totalChecks = 0;

            // 통계 변수
            int sceneFinalContentInstanceCount = 0;
            int registryCount = 0;
            int queryServiceCount = 0;
            int duplicateSourceMarkerIdCount = 0;
            int generatedPlaceholderCount = 0;
            int userAssignedFinalCount = 0;

            // Category / FinalContentKind / Zone breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var zoneCountBreakdown = new Dictionary<string, int>();

            // 1. GeneratedWorldRoot exists
            totalChecks++;
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 1: GeneratedWorldRoot not found.");
                failCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 1: GeneratedWorldRoot found: '{generatedRoot.name}'");
                passCount++;
            }

            // 2. WorldMapRuntimeFinalContentInstanceRegistry exists
            totalChecks++;
            WorldMapRuntimeFinalContentInstanceRegistry registry = generatedRoot?.GetComponent<WorldMapRuntimeFinalContentInstanceRegistry>();
            if (registry == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 2: WorldMapRuntimeFinalContentInstanceRegistry not found.");
                failCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 2: WorldMapRuntimeFinalContentInstanceRegistry found.");
                passCount++;
            }

            // 3. WorldMapRuntimeFinalContentQueryService exists
            totalChecks++;
            WorldMapRuntimeFinalContentQueryService queryService = generatedRoot?.GetComponent<WorldMapRuntimeFinalContentQueryService>();
            if (queryService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 3: WorldMapRuntimeFinalContentQueryService not found.");
                failCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 3: WorldMapRuntimeFinalContentQueryService found.");
                passCount++;
            }

            // Scene final content instance count 계산 (Registry의 static helper 사용)
            sceneFinalContentInstanceCount = generatedRoot != null
                ? WorldMapRuntimeFinalContentInstanceRegistry.CountSceneFinalContentInstances(generatedRoot.transform)
                : 0;

            // 4. Scene final content instance count > 0
            totalChecks++;
            if (sceneFinalContentInstanceCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 4: Scene final content instance count: {sceneFinalContentInstanceCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 4: Scene final content instance count is 0.");
                failCount++;
            }

            // Registry cache가 비어 있고 scene count > 0이면 RebuildCacheFromRoot 호출
            if (registry != null && registry.Count == 0 && sceneFinalContentInstanceCount > 0)
            {
                log.AppendLine($"{LogPrefix} [INFO] Registry cache is empty but scene has {sceneFinalContentInstanceCount} instances. Rebuilding cache.");
                registry.RebuildCacheFromRoot(generatedRoot.transform);
            }

            registryCount = registry != null ? registry.Count : 0;
            duplicateSourceMarkerIdCount = registry != null ? registry.DuplicateSourceMarkerIds.Count : 0;

            // 5. Registry.Count == scene final content instance count
            totalChecks++;
            if (registryCount == sceneFinalContentInstanceCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 5: Registry.Count ({registryCount}) == scene final content instance count ({sceneFinalContentInstanceCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 5: Registry.Count ({registryCount}) != scene final content instance count ({sceneFinalContentInstanceCount}).");
                failCount++;
            }

            queryServiceCount = queryService != null ? queryService.Count : 0;

            // 6. QueryService.Count == Registry.Count
            totalChecks++;
            if (queryServiceCount == registryCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 6: QueryService.Count ({queryServiceCount}) == Registry.Count ({registryCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 6: QueryService.Count ({queryServiceCount}) != Registry.Count ({registryCount}).");
                failCount++;
            }

            // 7. QueryService.HasRegistry == true
            totalChecks++;
            if (queryService != null && queryService.HasRegistry)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 7: QueryService.HasRegistry == true.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 7: QueryService.HasRegistry == false.");
                failCount++;
            }

            // 8. QueryService.GetAll count == QueryService.Count
            totalChecks++;
            int getAllCount = queryService != null ? queryService.GetAll().Count : 0;
            if (getAllCount == queryServiceCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 8: QueryService.GetAll count ({getAllCount}) == QueryService.Count ({queryServiceCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 8: QueryService.GetAll count ({getAllCount}) != QueryService.Count ({queryServiceCount}).");
                failCount++;
            }

            // 9. GetByZoneId(firstZoneId) count > 0
            totalChecks++;
            string firstZoneId = GetFirstZoneId(registry);
            int zoneQueryCount = queryService != null && !string.IsNullOrEmpty(firstZoneId)
                ? queryService.GetByZoneId(firstZoneId).Count
                : 0;
            if (zoneQueryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 9: GetByZoneId('{firstZoneId}') count: {zoneQueryCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 9: GetByZoneId('{firstZoneId}') count is 0.");
                failCount++;
            }

            // 10. GetByRuntimeCategory("HarvestResource") count > 0
            totalChecks++;
            int harvestCategoryCount = queryService != null
                ? queryService.GetByRuntimeCategory("HarvestResource").Count
                : 0;
            if (harvestCategoryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 10: GetByRuntimeCategory('HarvestResource') count: {harvestCategoryCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 10: GetByRuntimeCategory('HarvestResource') count is 0. (May be normal if no HarvestResource instances exist)");
                warnCount++;
            }

            // 11. GetByFinalContentKind(HarvestResource) count > 0
            totalChecks++;
            int harvestKindCount = queryService != null
                ? queryService.GetByFinalContentKind(WorldMapRuntimeFinalContentKind.HarvestResource).Count
                : 0;
            if (harvestKindCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 11: GetByFinalContentKind(HarvestResource) count: {harvestKindCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 11: GetByFinalContentKind(HarvestResource) count is 0. (May be normal if no HarvestResource instances exist)");
                warnCount++;
            }

            // 12. GetByAdapterKind(HarvestResource) count > 0
            totalChecks++;
            int harvestAdapterCount = queryService != null
                ? queryService.GetByAdapterKind(WorldMapRuntimeGameplayAdapterKind.HarvestResource).Count
                : 0;
            if (harvestAdapterCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 12: GetByAdapterKind(HarvestResource) count: {harvestAdapterCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 12: GetByAdapterKind(HarvestResource) count is 0. (May be normal if no HarvestResource instances exist)");
                warnCount++;
            }

            // 13. TryGetBySourceMarkerId(firstSourceMarkerId) succeeds
            totalChecks++;
            string firstSourceMarkerId = GetFirstSourceMarkerId(registry);
            bool sourceMarkerFound = queryService != null && !string.IsNullOrEmpty(firstSourceMarkerId)
                && queryService.TryGetBySourceMarkerId(firstSourceMarkerId, out _);
            if (sourceMarkerFound)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 13: TryGetBySourceMarkerId('{firstSourceMarkerId}') succeeded.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 13: TryGetBySourceMarkerId('{firstSourceMarkerId}') failed.");
                failCount++;
            }

            // 14. TryGetNearest(Vector3.zero, None) succeeds
            totalChecks++;
            bool nearestNone = queryService != null
                && queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeFinalContentKind.None, out _);
            if (nearestNone)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 14: TryGetNearest(Vector3.zero, None) succeeded.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 14: TryGetNearest(Vector3.zero, None) failed.");
                failCount++;
            }

            // 15. TryGetNearest(Vector3.zero, HarvestResource) succeeds
            totalChecks++;
            bool nearestHarvest = queryService != null
                && queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeFinalContentKind.HarvestResource, out _);
            if (nearestHarvest)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 15: TryGetNearest(Vector3.zero, HarvestResource) succeeded.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 15: TryGetNearest(Vector3.zero, HarvestResource) failed. (May be normal if no HarvestResource instances exist)");
                warnCount++;
            }

            // 16. TryGetNearestInZone(firstZoneId, Vector3.zero, None) succeeds
            totalChecks++;
            bool nearestInZone = queryService != null && !string.IsNullOrEmpty(firstZoneId)
                && queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, WorldMapRuntimeFinalContentKind.None, out _);
            if (nearestInZone)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 16: TryGetNearestInZone('{firstZoneId}', Vector3.zero, None) succeeded.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 16: TryGetNearestInZone('{firstZoneId}', Vector3.zero, None) failed.");
                failCount++;
            }

            // 17. Source RuntimeSpawnedInstances object not included in query results
            totalChecks++;
            bool noSpawnedInstances = true;
            IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> allInstances = registry != null ? registry.AllInstances : null;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && IsUnderRuntimeSpawnedInstances(tag.transform))
                    {
                        noSpawnedInstances = false;
                        break;
                    }
                }
            }
            if (noSpawnedInstances && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 17: No Source RuntimeSpawnedInstances object included in query results.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 17: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 17: Source RuntimeSpawnedInstances object found in query results.");
                failCount++;
            }

            // 18. Preview RuntimeSpawnInstances object not included in query results
            totalChecks++;
            bool noPreviewInstances = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && IsUnderRuntimeSpawnPreviewInstances(tag.transform))
                    {
                        noPreviewInstances = false;
                        break;
                    }
                }
            }
            if (noPreviewInstances && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 18: No Preview RuntimeSpawnInstances object included in query results.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 18: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 18: Preview RuntimeSpawnInstances object found in query results.");
                failCount++;
            }

            // 19. RuntimePlaceholder object not included in query results
            totalChecks++;
            bool noPlaceholder = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    {
                        noPlaceholder = false;
                        break;
                    }
                }
            }
            if (noPlaceholder && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 19: No RuntimePlaceholder object included in query results.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 19: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 19: RuntimePlaceholder object found in query results.");
                failCount++;
            }

            // 20. WorldMapZoneContentMarker object not included in query results
            totalChecks++;
            bool noZoneContentMarker = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && tag.GetComponent<WorldMapZoneContentMarker>() != null)
                    {
                        noZoneContentMarker = false;
                        break;
                    }
                }
            }
            if (noZoneContentMarker && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 20: No WorldMapZoneContentMarker object included in query results.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 20: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 20: WorldMapZoneContentMarker object found in query results.");
                failCount++;
            }

            // 21. WorldMapRuntimeFinalContentContract object not included in query results
            totalChecks++;
            bool noFinalContentContract = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && tag.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    {
                        noFinalContentContract = false;
                        break;
                    }
                }
            }
            if (noFinalContentContract && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 21: No WorldMapRuntimeFinalContentContract object included in query results.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 21: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 21: WorldMapRuntimeFinalContentContract object found in query results.");
                failCount++;
            }

            // 22. Duplicate SourceMarkerIds count == 0
            totalChecks++;
            if (duplicateSourceMarkerIdCount == 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 22: Duplicate SourceMarkerIds count: {duplicateSourceMarkerIdCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 22: Duplicate SourceMarkerIds count: {duplicateSourceMarkerIdCount} (expected 0).");
                failCount++;
            }

            // 23. All final content instances have non-empty ZoneId
            totalChecks++;
            bool allHaveZoneId = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null || string.IsNullOrEmpty(tag.ZoneId))
                    {
                        allHaveZoneId = false;
                        break;
                    }
                }
            }
            if (allHaveZoneId && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 23: All final content instances have non-empty ZoneId.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 23: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 23: Some final content instances have empty ZoneId.");
                failCount++;
            }

            // 24. All final content instances have non-empty SourceMarkerId
            totalChecks++;
            bool allHaveSourceMarkerId = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null || string.IsNullOrEmpty(tag.SourceMarkerId))
                    {
                        allHaveSourceMarkerId = false;
                        break;
                    }
                }
            }
            if (allHaveSourceMarkerId && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 24: All final content instances have non-empty SourceMarkerId.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 24: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 24: Some final content instances have empty SourceMarkerId.");
                failCount++;
            }

            // 25. All final content instances have non-empty RuntimeCategory
            totalChecks++;
            bool allHaveRuntimeCategory = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null || string.IsNullOrEmpty(tag.RuntimeCategory))
                    {
                        allHaveRuntimeCategory = false;
                        break;
                    }
                }
            }
            if (allHaveRuntimeCategory && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 25: All final content instances have non-empty RuntimeCategory.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 25: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 25: Some final content instances have empty RuntimeCategory.");
                failCount++;
            }

            // 26. All final content instances have non-empty RequirementId
            totalChecks++;
            bool allHaveRequirementId = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null || string.IsNullOrEmpty(tag.RequirementId))
                    {
                        allHaveRequirementId = false;
                        break;
                    }
                }
            }
            if (allHaveRequirementId && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 26: All final content instances have non-empty RequirementId.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 26: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 26: Some final content instances have empty RequirementId.");
                failCount++;
            }

            // 27. All final content instances have FinalContentKind != None
            totalChecks++;
            bool allHaveFinalContentKind = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null || tag.FinalContentKind == WorldMapRuntimeFinalContentKind.None)
                    {
                        allHaveFinalContentKind = false;
                        break;
                    }
                }
            }
            if (allHaveFinalContentKind && registryCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 27: All final content instances have FinalContentKind != None.");
                passCount++;
            }
            else if (registryCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 27: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 27: Some final content instances have FinalContentKind == None.");
                failCount++;
            }

            // 28. Scene object transform/name unchanged (snapshot-based)
            totalChecks++;
            log.AppendLine($"{LogPrefix} [INFO] Check 28: Scene object transform/name unchanged - skipped (read-only validation, no snapshot taken).");
            infoCount++;

            // 29. Category breakdown INFO
            totalChecks++;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null) continue;
                    string cat = tag.RuntimeCategory;
                    if (!string.IsNullOrEmpty(cat))
                    {
                        if (!categoryBreakdown.ContainsKey(cat))
                            categoryBreakdown[cat] = 0;
                        categoryBreakdown[cat]++;
                    }
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 29: Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 30. FinalContentKind breakdown INFO
            totalChecks++;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null) continue;
                    WorldMapRuntimeFinalContentKind kind = tag.FinalContentKind;
                    if (!finalContentKindBreakdown.ContainsKey(kind))
                        finalContentKindBreakdown[kind] = 0;
                    finalContentKindBreakdown[kind]++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 30: FinalContentKind breakdown:");
            foreach (var kvp in finalContentKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 31. Zone count breakdown INFO
            totalChecks++;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null) continue;
                    string zoneId = tag.ZoneId;
                    if (!string.IsNullOrEmpty(zoneId))
                    {
                        if (!zoneCountBreakdown.ContainsKey(zoneId))
                            zoneCountBreakdown[zoneId] = 0;
                        zoneCountBreakdown[zoneId]++;
                    }
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 31: Zone count breakdown:");
            foreach (var kvp in zoneCountBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 32. Generated placeholder final content count INFO
            totalChecks++;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && tag.IsGeneratedPlaceholderContent)
                        generatedPlaceholderCount++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 32: Generated placeholder final content count: {generatedPlaceholderCount}");
            infoCount++;

            // 33. User-assigned final content count INFO
            totalChecks++;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && tag.IsUserAssignedFinalContent)
                        userAssignedFinalCount++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 33: User-assigned final content count: {userAssignedFinalCount}");
            infoCount++;

            // ===== Summary =====
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} Total checks: {totalChecks}");
            log.AppendLine($"{LogPrefix} PASS: {passCount}");
            log.AppendLine($"{LogPrefix} FAIL: {failCount}");
            log.AppendLine($"{LogPrefix} WARN: {warnCount}");
            log.AppendLine($"{LogPrefix} INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Scene final content instances: {sceneFinalContentInstanceCount}");
            log.AppendLine($"{LogPrefix} Registry count: {registryCount}");
            log.AppendLine($"{LogPrefix} QueryService count: {queryServiceCount}");
            log.AppendLine($"{LogPrefix} Duplicate SourceMarkerIds: {duplicateSourceMarkerIdCount}");
            log.AppendLine($"{LogPrefix} Generated placeholder final content: {generatedPlaceholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content: {userAssignedFinalCount}");

            // DisplayDialog summary
            string dialogMessage = $"M-6 Validation Complete\n" +
                $"PASS: {passCount} / FAIL: {failCount} / WARN: {warnCount} / INFO: {infoCount}\n" +
                $"Scene final content instances: {sceneFinalContentInstanceCount}\n" +
                $"Registry count: {registryCount}\n" +
                $"QueryService count: {queryServiceCount}\n" +
                $"Duplicate SourceMarkerIds: {duplicateSourceMarkerIdCount}\n" +
                $"Generated placeholder final content: {generatedPlaceholderCount}\n" +
                $"User-assigned final content: {userAssignedFinalCount}";

            EditorUtility.DisplayDialog(
                "Phase 14.10-M-6: Validate Runtime Final Content Query",
                dialogMessage,
                "OK");

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-M-6: Validate Runtime Final Content Query Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Helper Methods =====

        /// <summary>
        /// GeneratedWorldRoot를 찾는다. context override가 있으면 우선 사용하고,
        /// 없으면 settings.GeneratedRootName으로 씬에서 찾는다.
        /// </summary>
        private static GameObject FindGeneratedRoot(DeepLightMapAutoBuilderSettingsSO settings, DeepLightMapAutoBuilderSceneContext context)
        {
            // context override 우선
            if (context != null && context.GeneratedRootOverride != null)
                return context.GeneratedRootOverride;

            if (settings == null) return null;

            string rootName = settings.GeneratedRootName;
            if (string.IsNullOrEmpty(rootName))
                rootName = "GeneratedWorldRoot";

            return GameObject.Find(rootName);
        }

        /// <summary>
        /// Registry의 첫 번째 instance에서 ZoneId를 추출한다.
        /// </summary>
        private static string GetFirstZoneId(WorldMapRuntimeFinalContentInstanceRegistry registry)
        {
            if (registry == null || registry.AllInstances.Count == 0)
                return null;

            foreach (WorldMapRuntimeFinalContentInstanceTag tag in registry.AllInstances)
            {
                if (tag != null && !string.IsNullOrEmpty(tag.ZoneId))
                    return tag.ZoneId;
            }

            return null;
        }

        /// <summary>
        /// Registry의 첫 번째 instance에서 SourceMarkerId를 추출한다.
        /// </summary>
        private static string GetFirstSourceMarkerId(WorldMapRuntimeFinalContentInstanceRegistry registry)
        {
            if (registry == null || registry.AllInstances.Count == 0)
                return null;

            foreach (WorldMapRuntimeFinalContentInstanceTag tag in registry.AllInstances)
            {
                if (tag != null && !string.IsNullOrEmpty(tag.SourceMarkerId))
                    return tag.SourceMarkerId;
            }

            return null;
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
        /// 지정한 Transform의 ancestor 중 "RuntimeSpawnInstances" 이름을 가진 object가 있는지 확인한다.
        /// </summary>
        private static bool IsUnderRuntimeSpawnPreviewInstances(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == "RuntimeSpawnInstances")
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
