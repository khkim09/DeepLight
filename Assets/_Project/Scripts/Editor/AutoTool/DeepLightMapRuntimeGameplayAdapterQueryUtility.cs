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
    /// Phase 14.10-L-2: Runtime Gameplay Adapter Query 전용 Editor Utility.
    /// GeneratedWorldRoot에 WorldMapRuntimeGameplayAdapterRegistry와
    /// WorldMapRuntimeGameplayAdapterQueryService를 생성/검증한다.
    /// gameplay 시스템이 adapter를 안전하게 조회할 수 있는 기반을 제공한다.
    /// </summary>
    public static class DeepLightMapRuntimeGameplayAdapterQueryUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot를 찾고, WorldMapRuntimeGameplayAdapterRegistry와
        /// WorldMapRuntimeGameplayAdapterQueryService를 생성/초기화한다.
        /// registry.RebuildCacheFromRoot()와 queryService.TryInitializeFromRoot()를 호출한다.
        /// </summary>
        public static void RebuildRuntimeGameplayAdapterQuery(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-2] Settings is null! Cannot rebuild runtime gameplay adapter query.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-L-2: Rebuild Runtime Gameplay Adapter Query =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-2] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [L-2] Found GeneratedWorldRoot: '{generatedRoot.name}'");

            // 2. Registry 찾기 또는 추가
            WorldMapRuntimeGameplayAdapterRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterRegistry>();
            if (registry == null)
            {
                registry = generatedRoot.AddComponent<WorldMapRuntimeGameplayAdapterRegistry>();
                Debug.Log($"[MapAutoBuilder] [L-2] Added WorldMapRuntimeGameplayAdapterRegistry to '{generatedRoot.name}'.");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [L-2] Found existing WorldMapRuntimeGameplayAdapterRegistry on '{generatedRoot.name}'.");
            }

            // 3. QueryService 찾기 또는 추가
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null)
            {
                queryService = generatedRoot.AddComponent<WorldMapRuntimeGameplayAdapterQueryService>();
                Debug.Log($"[MapAutoBuilder] [L-2] Added WorldMapRuntimeGameplayAdapterQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] [L-2] Found existing WorldMapRuntimeGameplayAdapterQueryService on '{generatedRoot.name}'.");
            }

            // 4. Registry cache 재구축
            registry.RebuildCacheFromRoot(generatedRoot.transform);

            // 5. QueryService 초기화
            queryService.TryInitializeFromRoot(generatedRoot.transform);

            // 6. 결과 로그 출력
            LogRebuildResult(registry, queryService);
        }

        /// <summary>
        /// WorldMapRuntimeGameplayAdapterRegistry와 QueryService의 유효성을 검사한다.
        /// 25개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Validate는 cache rebuild/initialize는 허용하지만 GameObject 생성/삭제/이동/name 변경은 금지.
        /// </summary>
        public static void ValidateRuntimeGameplayAdapterQuery(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [L-2] Settings is null! Cannot validate runtime gameplay adapter query.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-L-2: Validate Runtime Gameplay Adapter Query =====");

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-L-2: Validate Runtime Gameplay Adapter Query =====");

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
                // 더 이상 진행 불가
                log.AppendLine("  [FAIL] Cannot proceed without GeneratedWorldRoot.");
                failCount++;
                Debug.Log(log.ToString());
                return;
            }

            // Validate 전후 snapshot으로 transform/name 변경 여부 확인
            var preValidateSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();
            CaptureTransformSnapshots(generatedRoot.transform, preValidateSnapshots);

            // 2. WorldMapRuntimeGameplayAdapterRegistry exists
            WorldMapRuntimeGameplayAdapterRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterRegistry>();
            if (registry == null)
            {
                log.AppendLine("  [FAIL] 2. WorldMapRuntimeGameplayAdapterRegistry exists: NOT FOUND");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 2. WorldMapRuntimeGameplayAdapterRegistry exists");
                passCount++;
            }

            // 3. WorldMapRuntimeGameplayAdapterQueryService exists
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null)
            {
                log.AppendLine("  [FAIL] 3. WorldMapRuntimeGameplayAdapterQueryService exists: NOT FOUND");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 3. WorldMapRuntimeGameplayAdapterQueryService exists");
                passCount++;
            }

            // Registry/QueryService가 없으면 더 이상 진행 불가
            if (registry == null || queryService == null)
            {
                log.AppendLine("  [FAIL] Cannot proceed without Registry and QueryService.");
                failCount++;
                Debug.Log(log.ToString());
                return;
            }

            // Registry cache 재구축 (Validate는 cache rebuild 허용)
            registry.RebuildCacheFromRoot(generatedRoot.transform);

            // QueryService 초기화
            queryService.TryInitializeFromRoot(generatedRoot.transform);

            // 4. Scene adapter count > 0
            int sceneAdapterCount = CountSceneAdapters(generatedRoot.transform);
            if (sceneAdapterCount <= 0)
            {
                log.AppendLine($"  [FAIL] 4. Scene adapter count > 0: {sceneAdapterCount}");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 4. Scene adapter count > 0: {sceneAdapterCount}");
                passCount++;
            }

            // 5. Registry count == scene adapter count
            int registryCount = registry.Count;
            if (registryCount != sceneAdapterCount)
            {
                log.AppendLine($"  [FAIL] 5. Registry count ({registryCount}) == scene adapter count ({sceneAdapterCount})");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 5. Registry count ({registryCount}) == scene adapter count ({sceneAdapterCount})");
                passCount++;
            }

            // 6. QueryService count == registry count
            int queryCount = queryService.Count;
            if (queryCount != registryCount)
            {
                log.AppendLine($"  [FAIL] 6. QueryService count ({queryCount}) == registry count ({registryCount})");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 6. QueryService count ({queryCount}) == registry count ({registryCount})");
                passCount++;
            }

            // 7. QueryService.HasRegistry == true
            if (!queryService.HasRegistry)
            {
                log.AppendLine("  [FAIL] 7. QueryService.HasRegistry == true");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 7. QueryService.HasRegistry == true");
                passCount++;
            }

            // 8. QueryService.GetAll count == QueryService.Count
            int getAllCount = queryService.GetAll().Count;
            if (getAllCount != queryCount)
            {
                log.AppendLine($"  [FAIL] 8. QueryService.GetAll count ({getAllCount}) == QueryService.Count ({queryCount})");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 8. QueryService.GetAll count ({getAllCount}) == QueryService.Count ({queryCount})");
                passCount++;
            }

            // 9. GetByZoneId("A1") 또는 첫 번째 zone id query 성공
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> zoneResult = queryService.GetByZoneId("A1");
            if (zoneResult == null || zoneResult.Count == 0)
            {
                // A1이 없으면 첫 번째 zone id로 시도
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                string firstZoneId = null;
                foreach (var adapter in allAdapters)
                {
                    if (adapter != null && !string.IsNullOrEmpty(adapter.ZoneId))
                    {
                        firstZoneId = adapter.ZoneId;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(firstZoneId))
                {
                    zoneResult = queryService.GetByZoneId(firstZoneId);
                    if (zoneResult != null && zoneResult.Count > 0)
                    {
                        log.AppendLine($"  [PASS] 9. GetByZoneId('{firstZoneId}') count: {zoneResult.Count}");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine($"  [FAIL] 9. GetByZoneId('{firstZoneId}') returned empty");
                        failCount++;
                    }
                }
                else
                {
                    log.AppendLine("  [WARN] 9. GetByZoneId: No zone id found in any adapter");
                    warnCount++;
                }
            }
            else
            {
                log.AppendLine($"  [PASS] 9. GetByZoneId('A1') count: {zoneResult.Count}");
                passCount++;
            }

            // 10. GetByRuntimeCategory("HarvestResource") count > 0
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> harvestResult = queryService.GetByRuntimeCategory("HarvestResource");
            if (harvestResult == null || harvestResult.Count == 0)
            {
                log.AppendLine("  [WARN] 10. GetByRuntimeCategory('HarvestResource') count > 0: 0");
                warnCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 10. GetByRuntimeCategory('HarvestResource') count: {harvestResult.Count}");
                passCount++;
            }

            // 11. GetByAdapterKind(HarvestResource) count > 0
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> kindResult = queryService.GetByAdapterKind(WorldMapRuntimeGameplayAdapterKind.HarvestResource);
            if (kindResult == null || kindResult.Count == 0)
            {
                log.AppendLine("  [WARN] 11. GetByAdapterKind(HarvestResource) count > 0: 0");
                warnCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 11. GetByAdapterKind(HarvestResource) count: {kindResult.Count}");
                passCount++;
            }

            // 12. TryGetByMarkerId(first marker id) 성공
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdaptersForMarker = queryService.GetAll();
            string firstMarkerId = null;
            foreach (var adapter in allAdaptersForMarker)
            {
                if (adapter != null && !string.IsNullOrEmpty(adapter.MarkerId))
                {
                    firstMarkerId = adapter.MarkerId;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(firstMarkerId))
            {
                if (queryService.TryGetByMarkerId(firstMarkerId, out var markerResult) && markerResult != null)
                {
                    log.AppendLine($"  [PASS] 12. TryGetByMarkerId('{firstMarkerId}') success");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] 12. TryGetByMarkerId('{firstMarkerId}') failed");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] 12. TryGetByMarkerId: No marker id found in any adapter");
                warnCount++;
            }

            // 13. TryGetNearest(Vector3.zero, None, out result) 성공
            if (queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeGameplayAdapterKind.None, out var nearestNone))
            {
                log.AppendLine($"  [PASS] 13. TryGetNearest(Vector3.zero, None) success: '{nearestNone.name}'");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] 13. TryGetNearest(Vector3.zero, None) failed (no adapters in scene)");
                warnCount++;
            }

            // 14. TryGetNearest(Vector3.zero, HarvestResource, out result) 성공
            if (queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeGameplayAdapterKind.HarvestResource, out var nearestHarvest))
            {
                log.AppendLine($"  [PASS] 14. TryGetNearest(Vector3.zero, HarvestResource) success: '{nearestHarvest.name}'");
                passCount++;
            }
            else
            {
                log.AppendLine("  [WARN] 14. TryGetNearest(Vector3.zero, HarvestResource) failed (no HarvestResource adapters)");
                warnCount++;
            }

            // 15. TryGetNearestInZone(first zone id, Vector3.zero, None, out result) 성공
            string firstZoneForNearest = null;
            foreach (var adapter in allAdaptersForMarker)
            {
                if (adapter != null && !string.IsNullOrEmpty(adapter.ZoneId))
                {
                    firstZoneForNearest = adapter.ZoneId;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(firstZoneForNearest))
            {
                if (queryService.TryGetNearestInZone(firstZoneForNearest, Vector3.zero, WorldMapRuntimeGameplayAdapterKind.None, out var nearestInZone))
                {
                    log.AppendLine($"  [PASS] 15. TryGetNearestInZone('{firstZoneForNearest}', Vector3.zero, None) success: '{nearestInZone.name}'");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"  [WARN] 15. TryGetNearestInZone('{firstZoneForNearest}', Vector3.zero, None) failed");
                    warnCount++;
                }
            }
            else
            {
                log.AppendLine("  [WARN] 15. TryGetNearestInZone: No zone id found");
                warnCount++;
            }

            // 16. Preview instance가 query 결과에 포함되지 않음
            bool previewFound = false;
            foreach (var adapter in queryService.GetAll())
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
                log.AppendLine("  [PASS] 16. Preview instance not included in query results");
                passCount++;
            }

            // 17. RuntimePlaceholder 원본이 query 결과에 포함되지 않음
            bool placeholderFound = false;
            foreach (var adapter in queryService.GetAll())
            {
                if (adapter == null) continue;
                if (adapter.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    placeholderFound = true;
                    log.AppendLine($"  [FAIL] 17. RuntimePlaceholder found in query results: '{adapter.name}'");
                    failCount++;
                    break;
                }
            }
            if (!placeholderFound)
            {
                log.AppendLine("  [PASS] 17. RuntimePlaceholder not included in query results");
                passCount++;
            }

            // 18. WorldMapZoneContentMarker가 붙은 marker object가 query 결과에 포함되지 않음
            bool markerFound = false;
            foreach (var adapter in queryService.GetAll())
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
                log.AppendLine("  [PASS] 18. WorldMapZoneContentMarker not included in query results");
                passCount++;
            }

            // 19. Duplicate MarkerIds count == 0
            if (registry.DuplicateMarkerIds.Count > 0)
            {
                log.AppendLine($"  [FAIL] 19. Duplicate MarkerIds count == 0: {registry.DuplicateMarkerIds.Count}");
                foreach (string dupId in registry.DuplicateMarkerIds)
                {
                    log.AppendLine($"         Duplicate: '{dupId}'");
                }
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 19. Duplicate MarkerIds count == 0");
                passCount++;
            }

            // 20. All adapters have non-empty ZoneId
            bool allZoneIdValid = true;
            foreach (var adapter in queryService.GetAll())
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.ZoneId))
                {
                    allZoneIdValid = false;
                    log.AppendLine($"  [FAIL] 20. Adapter '{adapter.name}' has empty ZoneId");
                    failCount++;
                    break;
                }
            }
            if (allZoneIdValid)
            {
                log.AppendLine("  [PASS] 20. All adapters have non-empty ZoneId");
                passCount++;
            }

            // 21. All adapters have non-empty MarkerId
            bool allMarkerIdValid = true;
            foreach (var adapter in queryService.GetAll())
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.MarkerId))
                {
                    allMarkerIdValid = false;
                    log.AppendLine($"  [FAIL] 21. Adapter '{adapter.name}' has empty MarkerId");
                    failCount++;
                    break;
                }
            }
            if (allMarkerIdValid)
            {
                log.AppendLine("  [PASS] 21. All adapters have non-empty MarkerId");
                passCount++;
            }

            // 22. All adapters have non-empty RuntimeCategory
            bool allCategoryValid = true;
            foreach (var adapter in queryService.GetAll())
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.RuntimeCategory))
                {
                    allCategoryValid = false;
                    log.AppendLine($"  [FAIL] 22. Adapter '{adapter.name}' has empty RuntimeCategory");
                    failCount++;
                    break;
                }
            }
            if (allCategoryValid)
            {
                log.AppendLine("  [PASS] 22. All adapters have non-empty RuntimeCategory");
                passCount++;
            }

            // 23. All adapters have non-empty ProfileId
            bool allProfileIdValid = true;
            foreach (var adapter in queryService.GetAll())
            {
                if (adapter == null) continue;
                if (string.IsNullOrEmpty(adapter.ProfileId))
                {
                    allProfileIdValid = false;
                    log.AppendLine($"  [FAIL] 23. Adapter '{adapter.name}' has empty ProfileId");
                    failCount++;
                    break;
                }
            }
            if (allProfileIdValid)
            {
                log.AppendLine("  [PASS] 23. All adapters have non-empty ProfileId");
                passCount++;
            }

            // 24. All adapters have AdapterKind != None
            bool allKindValid = true;
            foreach (var adapter in queryService.GetAll())
            {
                if (adapter == null) continue;
                if (adapter.AdapterKind == WorldMapRuntimeGameplayAdapterKind.None)
                {
                    allKindValid = false;
                    log.AppendLine($"  [FAIL] 24. Adapter '{adapter.name}' has AdapterKind=None (MarkerId: {adapter.MarkerId})");
                    failCount++;
                    break;
                }
            }
            if (allKindValid)
            {
                log.AppendLine("  [PASS] 24. All adapters have AdapterKind != None");
                passCount++;
            }

            // 25. Scene object transform/name unchanged
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
                        log.AppendLine($"  [FAIL] 25. Transform/name changed: '{kvp.Value.name}' -> '{postSnapshot.name}'");
                        failCount++;
                        break;
                    }
                }
            }

            if (!transformChanged)
            {
                log.AppendLine("  [PASS] 25. Scene object transform/name unchanged");
                passCount++;
            }

            // ===== Summary =====
            log.AppendLine($"===== Validate Summary: PASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount} =====");
            Debug.Log(log.ToString());

            // Console 로그 기반 요약 출력 (DisplayDialog 대체)
            LogValidationSummary(
                "Phase 14.10-L-2: Validate Runtime Gameplay Adapter Query",
                passCount, failCount, warnCount, infoCount,
                $"Scene Adapters: {sceneAdapterCount}\n" +
                $"Registry Count: {registryCount}\n" +
                $"QueryService Count: {queryCount}\n" +
                $"Duplicate MarkerIds: {registry.DuplicateMarkerIds.Count}");
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
        /// Rebuild 결과를 Console에 로그로 출력한다.
        /// </summary>
        private static void LogRebuildResult(
            WorldMapRuntimeGameplayAdapterRegistry registry,
            WorldMapRuntimeGameplayAdapterQueryService queryService)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== Phase 14.10-L-2: Rebuild Runtime Gameplay Adapter Query Result =====");
            sb.AppendLine($"Adapter Count: {registry.Count}");
            sb.AppendLine($"Registry Count: {registry.Count}");
            sb.AppendLine($"Query Service Count: {queryService.Count}");
            sb.AppendLine($"HasRegistry: {queryService.HasRegistry}");

            // Category breakdown
            sb.AppendLine("--- Category Breakdown ---");
            var categories = new HashSet<string>();
            foreach (var adapter in registry.AllAdapters)
            {
                if (adapter != null && !string.IsNullOrEmpty(adapter.RuntimeCategory))
                    categories.Add(adapter.RuntimeCategory);
            }
            foreach (string cat in categories)
            {
                sb.AppendLine($"  {cat}: {registry.CountByCategory(cat)}");
            }

            // AdapterKind breakdown
            sb.AppendLine("--- AdapterKind Breakdown ---");
            foreach (WorldMapRuntimeGameplayAdapterKind kind in System.Enum.GetValues(typeof(WorldMapRuntimeGameplayAdapterKind)))
            {
                int count = registry.CountByAdapterKind(kind);
                if (count > 0)
                    sb.AppendLine($"  {kind}: {count}");
            }

            // Zone count
            sb.AppendLine($"--- Zone Count: {registry.AllAdapters.Count} adapters across {new HashSet<string>(GetZoneIds(registry)).Count} zones ---");

            // Duplicate MarkerIds
            sb.AppendLine($"Duplicate MarkerIds: {registry.DuplicateMarkerIds.Count}");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Registry에서 모든 ZoneId를 수집한다.
        /// </summary>
        private static IEnumerable<string> GetZoneIds(WorldMapRuntimeGameplayAdapterRegistry registry)
        {
            var zoneIds = new HashSet<string>();
            foreach (var adapter in registry.AllAdapters)
            {
                if (adapter != null && !string.IsNullOrEmpty(adapter.ZoneId))
                    zoneIds.Add(adapter.ZoneId);
            }
            return zoneIds;
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
    }
}

