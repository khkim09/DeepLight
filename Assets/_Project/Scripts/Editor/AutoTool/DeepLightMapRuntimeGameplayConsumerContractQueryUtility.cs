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
    /// Phase 14.10-N-4: Consumer Contract 전용 Registry + QueryService 구축/검증 Editor Utility.
    /// RuntimeFinalContentInstances 하위의 WorldMapRuntimeGameplayConsumerContract를
    /// Registry/QueryService가 안전하게 조회할 수 있도록 cache를 구축하고 검증한다.
    /// 실제 Harvest/Hazard/Narrative/Landmark/RouteTrigger gameplay 시스템에 직접 연결하지 않는다.
    /// UnityEditor API만 사용하며, Gameplay 폴더 신규 파일에는 UnityEditor API를 사용하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeGameplayConsumerContractQueryUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[N-4 ConsumerContractQuery]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeGameplayConsumerContractRegistry와
        /// WorldMapRuntimeGameplayConsumerContractQueryService를 추가/갱신하고
        /// cache를 재구축한다. Scene object의 transform/name을 변경하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeGameplayConsumerContractQuery(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [N-4] Settings is null! Cannot rebuild consumer contract query.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-4: Rebuild Runtime Gameplay Consumer Contract Query =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. GeneratedWorldRoot에 Registry가 없으면 Undo.AddComponent로 추가
            WorldMapRuntimeGameplayConsumerContractRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeGameplayConsumerContractRegistry>();
            if (registry == null)
            {
                registry = Undo.AddComponent<WorldMapRuntimeGameplayConsumerContractRegistry>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeGameplayConsumerContractRegistry to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeGameplayConsumerContractRegistry already exists on '{generatedRoot.name}'.");
            }

            // 3. GeneratedWorldRoot에 QueryService가 없으면 Undo.AddComponent로 추가
            WorldMapRuntimeGameplayConsumerContractQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayConsumerContractQueryService>();
            if (queryService == null)
            {
                queryService = Undo.AddComponent<WorldMapRuntimeGameplayConsumerContractQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeGameplayConsumerContractQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeGameplayConsumerContractQueryService already exists on '{generatedRoot.name}'.");
            }

            // 4. Registry.RebuildCacheFromRoot(generatedRoot.transform) 호출
            registry.RebuildCacheFromRoot(generatedRoot.transform);
            log.AppendLine($"{LogPrefix} [INFO] Registry.RebuildCacheFromRoot complete. Registry count: {registry.Count}");

            // 5. QueryService.TryInitializeFromRoot(generatedRoot.transform) 호출
            bool initialized = queryService.TryInitializeFromRoot(generatedRoot.transform);
            if (initialized)
            {
                log.AppendLine($"{LogPrefix} [PASS] QueryService.TryInitializeFromRoot complete. QueryService count: {queryService.Count}");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] QueryService.TryInitializeFromRoot failed.");
            }

            // 6. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            // 7. Breakdown 출력
            log.AppendLine($"{LogPrefix} [INFO] Category breakdown:");
            var categoryBreakdown = new Dictionary<string, int>();
            IReadOnlyList<WorldMapRuntimeGameplayConsumerContract> allContracts = registry.AllContracts;
            foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
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
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] FinalContentKind breakdown:");
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
            {
                WorldMapRuntimeFinalContentKind kind = contract.FinalContentKind;
                if (!finalContentKindBreakdown.ContainsKey(kind))
                    finalContentKindBreakdown[kind] = 0;
                finalContentKindBreakdown[kind]++;
            }
            foreach (var kvp in finalContentKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] BindingKind breakdown:");
            var bindingKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, int>();
            foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
            {
                WorldMapRuntimeFinalContentGameplayBindingKind kind = contract.BindingKind;
                if (!bindingKindBreakdown.ContainsKey(kind))
                    bindingKindBreakdown[kind] = 0;
                bindingKindBreakdown[kind]++;
            }
            foreach (var kvp in bindingKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] ConsumerKind breakdown:");
            var consumerKindBreakdown = new Dictionary<WorldMapRuntimeGameplayConsumerKind, int>();
            foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
            {
                WorldMapRuntimeGameplayConsumerKind kind = contract.ConsumerKind;
                if (!consumerKindBreakdown.ContainsKey(kind))
                    consumerKindBreakdown[kind] = 0;
                consumerKindBreakdown[kind]++;
            }
            foreach (var kvp in consumerKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] Zone breakdown:");
            var zoneBreakdown = new Dictionary<string, int>();
            foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
            {
                string zoneId = contract.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!zoneBreakdown.ContainsKey(zoneId))
                        zoneBreakdown[zoneId] = 0;
                    zoneBreakdown[zoneId]++;
                }
            }
            foreach (var kvp in zoneBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            // Duplicate SourceMarkerIds 출력
            IReadOnlyList<string> duplicateIds = registry.DuplicateSourceMarkerIds;
            if (duplicateIds != null && duplicateIds.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Duplicate SourceMarkerIds ({duplicateIds.Count}):");
                foreach (string dupId in duplicateIds)
                {
                    log.AppendLine($"{LogPrefix}   {dupId}");
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Duplicate SourceMarkerIds: 0");
            }

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-4: Rebuild Runtime Gameplay Consumer Contract Query Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// WorldMapRuntimeGameplayConsumerContractRegistry와
        /// WorldMapRuntimeGameplayConsumerContractQueryService의 유효성을 검사한다.
        /// 42개 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// Scene object transform/name 변경 없음. Read-only 검증.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeGameplayConsumerContractQuery(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [N-4] Settings is null! Cannot validate consumer contract query.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-4: Validate Runtime Gameplay Consumer Contract Query =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            int totalChecks = 0;

            // 통계 변수
            int sceneContractCount = 0;
            int registryCount = 0;
            int queryServiceCount = 0;
            int duplicateSourceMarkerIdCount = 0;
            int generatedPlaceholderCount = 0;
            int userAssignedFinalCount = 0;

            // Breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var bindingKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, int>();
            var consumerKindBreakdown = new Dictionary<WorldMapRuntimeGameplayConsumerKind, int>();
            var zoneBreakdown = new Dictionary<string, int>();

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

            // 2. WorldMapRuntimeGameplayConsumerContractRegistry exists
            totalChecks++;
            WorldMapRuntimeGameplayConsumerContractRegistry registry = generatedRoot?.GetComponent<WorldMapRuntimeGameplayConsumerContractRegistry>();
            if (registry == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 2: WorldMapRuntimeGameplayConsumerContractRegistry not found.");
                failCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 2: WorldMapRuntimeGameplayConsumerContractRegistry found.");
                passCount++;
            }

            // 3. WorldMapRuntimeGameplayConsumerContractQueryService exists
            totalChecks++;
            WorldMapRuntimeGameplayConsumerContractQueryService queryService = generatedRoot?.GetComponent<WorldMapRuntimeGameplayConsumerContractQueryService>();
            if (queryService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 3: WorldMapRuntimeGameplayConsumerContractQueryService not found.");
                failCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 3: WorldMapRuntimeGameplayConsumerContractQueryService found.");
                passCount++;
            }

            // 4. Scene consumer contract count > 0 (Registry.CountSceneConsumerContracts 사용)
            totalChecks++;
            sceneContractCount = generatedRoot != null
                ? WorldMapRuntimeGameplayConsumerContractRegistry.CountSceneConsumerContracts(generatedRoot.transform)
                : 0;
            if (sceneContractCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 4: Scene consumer contracts: {sceneContractCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 4: Scene consumer contracts count is 0.");
                failCount++;
            }

            // 5. Registry.Count == scene consumer contract count
            totalChecks++;
            registryCount = registry != null ? registry.Count : 0;
            if (registryCount == sceneContractCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 5: Registry.Count ({registryCount}) == scene contract count ({sceneContractCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 5: Registry.Count ({registryCount}) != scene contract count ({sceneContractCount}).");
                failCount++;
            }

            // 6. QueryService.Count == Registry.Count
            totalChecks++;
            queryServiceCount = queryService != null ? queryService.Count : 0;
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
            IReadOnlyList<WorldMapRuntimeGameplayConsumerContract> allContracts = queryService?.GetAll();
            int getAllCount = allContracts?.Count ?? 0;
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

            // 9. GetByZoneId(firstZoneId) count > 0 (INFO)
            totalChecks++;
            string firstZoneId = null;
            if (allContracts != null && allContracts.Count > 0)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract != null && !string.IsNullOrEmpty(contract.ZoneId))
                    {
                        firstZoneId = contract.ZoneId;
                        break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(firstZoneId))
            {
                IReadOnlyList<WorldMapRuntimeGameplayConsumerContract> zoneContracts = queryService.GetByZoneId(firstZoneId);
                if (zoneContracts != null && zoneContracts.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [INFO] Check 9: GetByZoneId('{firstZoneId}') count: {zoneContracts.Count}");
                    infoCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] Check 9: GetByZoneId('{firstZoneId}') returned 0.");
                    infoCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 9: No ZoneId found to test GetByZoneId.");
                infoCount++;
            }

            // 10. GetByRuntimeCategory("HarvestResource") count > 0 (INFO)
            totalChecks++;
            IReadOnlyList<WorldMapRuntimeGameplayConsumerContract> harvestContracts = queryService?.GetByRuntimeCategory("HarvestResource");
            if (harvestContracts != null && harvestContracts.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 10: GetByRuntimeCategory('HarvestResource') count: {harvestContracts.Count}");
                infoCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 10: GetByRuntimeCategory('HarvestResource') returned 0 (may be expected if no HarvestResource category exists).");
                infoCount++;
            }

            // 11. GetByFinalContentKind(HarvestResource) count > 0 (INFO)
            totalChecks++;
            IReadOnlyList<WorldMapRuntimeGameplayConsumerContract> harvestKindContracts = queryService?.GetByFinalContentKind(WorldMapRuntimeFinalContentKind.HarvestResource);
            if (harvestKindContracts != null && harvestKindContracts.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 11: GetByFinalContentKind(HarvestResource) count: {harvestKindContracts.Count}");
                infoCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 11: GetByFinalContentKind(HarvestResource) returned 0 (may be expected).");
                infoCount++;
            }

            // 12. GetByBindingKind(HarvestResourceCandidate) count > 0 (INFO)
            totalChecks++;
            IReadOnlyList<WorldMapRuntimeGameplayConsumerContract> candidateContracts = queryService?.GetByBindingKind(WorldMapRuntimeFinalContentGameplayBindingKind.HarvestResourceCandidate);
            if (candidateContracts != null && candidateContracts.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 12: GetByBindingKind(HarvestResourceCandidate) count: {candidateContracts.Count}");
                infoCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 12: GetByBindingKind(HarvestResourceCandidate) returned 0 (may be expected).");
                infoCount++;
            }

            // 13. GetByConsumerKind(HarvestResourceConsumer) count > 0 (INFO)
            totalChecks++;
            IReadOnlyList<WorldMapRuntimeGameplayConsumerContract> consumerKindContracts = queryService?.GetByConsumerKind(WorldMapRuntimeGameplayConsumerKind.HarvestResourceConsumer);
            if (consumerKindContracts != null && consumerKindContracts.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 13: GetByConsumerKind(HarvestResourceConsumer) count: {consumerKindContracts.Count}");
                infoCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 13: GetByConsumerKind(HarvestResourceConsumer) returned 0 (may be expected).");
                infoCount++;
            }

            // 14. TryGetBySourceMarkerId(firstSourceMarkerId) success (FAIL)
            totalChecks++;
            string firstSourceMarkerId = null;
            if (allContracts != null && allContracts.Count > 0)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract != null && !string.IsNullOrEmpty(contract.SourceMarkerId))
                    {
                        firstSourceMarkerId = contract.SourceMarkerId;
                        break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(firstSourceMarkerId))
            {
                bool found = queryService.TryGetBySourceMarkerId(firstSourceMarkerId, out var foundContract);
                if (found && foundContract != null)
                {
                    log.AppendLine($"{LogPrefix} [PASS] Check 14: TryGetBySourceMarkerId('{firstSourceMarkerId}') success.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] Check 14: TryGetBySourceMarkerId('{firstSourceMarkerId}') failed.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 14: No SourceMarkerId found to test TryGetBySourceMarkerId.");
                failCount++;
            }

            // 15. TryGetNearest(Vector3.zero, None consumerKind) success (FAIL)
            totalChecks++;
            bool nearestNone = queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeGameplayConsumerKind.None, out var nearestNoneResult);
            if (nearestNone && nearestNoneResult != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 15: TryGetNearest(Vector3.zero, None) success. Nearest: '{nearestNoneResult.name}'");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 15: TryGetNearest(Vector3.zero, None) failed.");
                failCount++;
            }

            // 16. TryGetNearest(Vector3.zero, HarvestResourceConsumer) success (INFO)
            totalChecks++;
            bool nearestConsumer = queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeGameplayConsumerKind.HarvestResourceConsumer, out var nearestConsumerResult);
            if (nearestConsumer && nearestConsumerResult != null)
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 16: TryGetNearest(Vector3.zero, HarvestResourceConsumer) success. Nearest: '{nearestConsumerResult.name}'");
                infoCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 16: TryGetNearest(Vector3.zero, HarvestResourceConsumer) returned false (may be expected if no HarvestResourceConsumer contracts exist).");
                infoCount++;
            }

            // 17. TryGetNearest(Vector3.zero, HarvestResource finalContentKind) success (INFO)
            totalChecks++;
            bool nearestHarvestKind = queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeFinalContentKind.HarvestResource, out var nearestHarvestKindResult);
            if (nearestHarvestKind && nearestHarvestKindResult != null)
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 17: TryGetNearest(Vector3.zero, HarvestResource finalContentKind) success. Nearest: '{nearestHarvestKindResult.name}'");
                infoCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 17: TryGetNearest(Vector3.zero, HarvestResource finalContentKind) returned false (may be expected).");
                infoCount++;
            }

            // 18. TryGetNearest(Vector3.zero, "HarvestResource") success (INFO)
            totalChecks++;
            bool nearestHarvestCat = queryService.TryGetNearest(Vector3.zero, "HarvestResource", out var nearestHarvestCatResult);
            if (nearestHarvestCat && nearestHarvestCatResult != null)
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 18: TryGetNearest(Vector3.zero, 'HarvestResource') success. Nearest: '{nearestHarvestCatResult.name}'");
                infoCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 18: TryGetNearest(Vector3.zero, 'HarvestResource') returned false (may be expected).");
                infoCount++;
            }

            // 19. TryGetNearestInZone(firstZoneId, Vector3.zero, None) success (INFO)
            totalChecks++;
            if (!string.IsNullOrEmpty(firstZoneId))
            {
                bool nearestInZone = queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, WorldMapRuntimeGameplayConsumerKind.None, out var nearestInZoneResult);
                if (nearestInZone && nearestInZoneResult != null)
                {
                    log.AppendLine($"{LogPrefix} [INFO] Check 19: TryGetNearestInZone('{firstZoneId}', Vector3.zero, None) success. Nearest: '{nearestInZoneResult.name}'");
                    infoCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] Check 19: TryGetNearestInZone('{firstZoneId}', Vector3.zero, None) returned false.");
                    infoCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] Check 19: No ZoneId found to test TryGetNearestInZone.");
                infoCount++;
            }

            // 20. RuntimeSpawnedInstances object가 query result에 포함되지 않음 (FAIL)
            totalChecks++;
            bool noSpawnedInQuery = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract != null && IsUnderRuntimeSpawnedInstances(contract.transform))
                    {
                        noSpawnedInQuery = false;
                        break;
                    }
                }
            }
            if (noSpawnedInQuery)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 20: RuntimeSpawnedInstances object not included in query result.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 20: RuntimeSpawnedInstances object found in query result.");
                failCount++;
            }

            // 21. RuntimeSpawnInstances preview object가 query result에 포함되지 않음 (FAIL)
            totalChecks++;
            bool noPreviewInQuery = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract != null && IsUnderRuntimeSpawnPreviewInstances(contract.transform))
                    {
                        noPreviewInQuery = false;
                        break;
                    }
                }
            }
            if (noPreviewInQuery)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 21: RuntimeSpawnInstances preview object not included in query result.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 21: RuntimeSpawnInstances preview object found in query result.");
                failCount++;
            }

            // 22. RuntimePlaceholder 원본 object가 query result에 포함되지 않음 (FAIL)
            totalChecks++;
            bool noPlaceholderInQuery = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract != null && contract.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    {
                        noPlaceholderInQuery = false;
                        break;
                    }
                }
            }
            if (noPlaceholderInQuery)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 22: RuntimePlaceholder object not included in query result.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 22: RuntimePlaceholder object found in query result.");
                failCount++;
            }

            // 23. WorldMapZoneContentMarker object가 query result에 포함되지 않음 (FAIL)
            totalChecks++;
            bool noMarkerInQuery = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract != null && contract.GetComponent<WorldMapZoneContentMarker>() != null)
                    {
                        noMarkerInQuery = false;
                        break;
                    }
                }
            }
            if (noMarkerInQuery)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 23: WorldMapZoneContentMarker object not included in query result.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 23: WorldMapZoneContentMarker object found in query result.");
                failCount++;
            }

            // 24. WorldMapRuntimeFinalContentContract source object가 query result에 포함되지 않음 (FAIL)
            totalChecks++;
            bool noContractInQuery = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract != null && contract.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                    {
                        noContractInQuery = false;
                        break;
                    }
                }
            }
            if (noContractInQuery)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 24: WorldMapRuntimeFinalContentContract object not included in query result.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 24: WorldMapRuntimeFinalContentContract object found in query result.");
                failCount++;
            }

            // 25. Duplicate SourceMarkerIds count == 0 (WARN)
            totalChecks++;
            duplicateSourceMarkerIdCount = registry != null ? registry.DuplicateSourceMarkerIds.Count : 0;
            if (duplicateSourceMarkerIdCount == 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 25: Duplicate SourceMarkerIds count: {duplicateSourceMarkerIdCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 25: Duplicate SourceMarkerIds count: {duplicateSourceMarkerIdCount} (expected 0).");
                warnCount++;
            }

            // 26. 모든 contract sourceMarkerId non-empty (FAIL)
            totalChecks++;
            bool allSourceMarkerIdNonEmpty = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || string.IsNullOrEmpty(contract.SourceMarkerId))
                    {
                        allSourceMarkerIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allSourceMarkerIdNonEmpty && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 26: All contracts have non-empty SourceMarkerId.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 26: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 26: Some contracts have empty SourceMarkerId.");
                failCount++;
            }

            // 27. 모든 contract zoneId non-empty (FAIL)
            totalChecks++;
            bool allZoneIdNonEmpty = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || string.IsNullOrEmpty(contract.ZoneId))
                    {
                        allZoneIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allZoneIdNonEmpty && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 27: All contracts have non-empty ZoneId.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 27: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 27: Some contracts have empty ZoneId.");
                failCount++;
            }

            // 28. 모든 contract runtimeCategory non-empty (FAIL)
            totalChecks++;
            bool allCategoryNonEmpty = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || string.IsNullOrEmpty(contract.RuntimeCategory))
                    {
                        allCategoryNonEmpty = false;
                        break;
                    }
                }
            }
            if (allCategoryNonEmpty && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 28: All contracts have non-empty RuntimeCategory.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 28: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 28: Some contracts have empty RuntimeCategory.");
                failCount++;
            }

            // 29. 모든 contract profileId non-empty (FAIL)
            totalChecks++;
            bool allProfileIdNonEmpty = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || string.IsNullOrEmpty(contract.ProfileId))
                    {
                        allProfileIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allProfileIdNonEmpty && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 29: All contracts have non-empty ProfileId.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 29: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 29: Some contracts have empty ProfileId.");
                failCount++;
            }

            // 30. 모든 contract requirementId non-empty (FAIL)
            totalChecks++;
            bool allRequirementIdNonEmpty = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || string.IsNullOrEmpty(contract.RequirementId))
                    {
                        allRequirementIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allRequirementIdNonEmpty && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 30: All contracts have non-empty RequirementId.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 30: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 30: Some contracts have empty RequirementId.");
                failCount++;
            }

            // 31. 모든 contract FinalContentKind != None (FAIL)
            totalChecks++;
            bool allFinalContentKindValid = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || contract.FinalContentKind == WorldMapRuntimeFinalContentKind.None)
                    {
                        allFinalContentKindValid = false;
                        break;
                    }
                }
            }
            if (allFinalContentKindValid && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 31: All contracts have FinalContentKind != None.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 31: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 31: Some contracts have FinalContentKind == None.");
                failCount++;
            }

            // 32. 모든 contract BindingKind != None (FAIL)
            totalChecks++;
            bool allBindingKindValid = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || contract.BindingKind == WorldMapRuntimeFinalContentGameplayBindingKind.None)
                    {
                        allBindingKindValid = false;
                        break;
                    }
                }
            }
            if (allBindingKindValid && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 32: All contracts have BindingKind != None.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 32: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 32: Some contracts have BindingKind == None.");
                failCount++;
            }

            // 33. 모든 contract ConsumerKind != None (FAIL)
            totalChecks++;
            bool allConsumerKindValid = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || contract.ConsumerKind == WorldMapRuntimeGameplayConsumerKind.None)
                    {
                        allConsumerKindValid = false;
                        break;
                    }
                }
            }
            if (allConsumerKindValid && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 33: All contracts have ConsumerKind != None.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 33: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 33: Some contracts have ConsumerKind == None.");
                failCount++;
            }

            // 34. 모든 contract IsReady == true (FAIL)
            totalChecks++;
            bool allIsReady = true;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null || !contract.IsReady())
                    {
                        allIsReady = false;
                        break;
                    }
                }
            }
            if (allIsReady && (allContracts?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 34: All contracts IsReady == true.");
                passCount++;
            }
            else if ((allContracts?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 34: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 34: Some contracts IsReady == false.");
                failCount++;
            }

            // 35. category breakdown INFO
            totalChecks++;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null) continue;
                    string cat = contract.RuntimeCategory;
                    if (!string.IsNullOrEmpty(cat))
                    {
                        if (!categoryBreakdown.ContainsKey(cat))
                            categoryBreakdown[cat] = 0;
                        categoryBreakdown[cat]++;
                    }
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 35: Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 36. finalContentKind breakdown INFO
            totalChecks++;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null) continue;
                    WorldMapRuntimeFinalContentKind kind = contract.FinalContentKind;
                    if (!finalContentKindBreakdown.ContainsKey(kind))
                        finalContentKindBreakdown[kind] = 0;
                    finalContentKindBreakdown[kind]++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 36: FinalContentKind breakdown:");
            foreach (var kvp in finalContentKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 37. bindingKind breakdown INFO
            totalChecks++;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null) continue;
                    WorldMapRuntimeFinalContentGameplayBindingKind kind = contract.BindingKind;
                    if (!bindingKindBreakdown.ContainsKey(kind))
                        bindingKindBreakdown[kind] = 0;
                    bindingKindBreakdown[kind]++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 37: BindingKind breakdown:");
            foreach (var kvp in bindingKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 38. consumerKind breakdown INFO
            totalChecks++;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null) continue;
                    WorldMapRuntimeGameplayConsumerKind kind = contract.ConsumerKind;
                    if (!consumerKindBreakdown.ContainsKey(kind))
                        consumerKindBreakdown[kind] = 0;
                    consumerKindBreakdown[kind]++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 38: ConsumerKind breakdown:");
            foreach (var kvp in consumerKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 39. zone breakdown INFO
            totalChecks++;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null) continue;
                    string zoneId = contract.ZoneId;
                    if (!string.IsNullOrEmpty(zoneId))
                    {
                        if (!zoneBreakdown.ContainsKey(zoneId))
                            zoneBreakdown[zoneId] = 0;
                        zoneBreakdown[zoneId]++;
                    }
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 39: Zone breakdown:");
            foreach (var kvp in zoneBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 40. generated placeholder content count INFO
            totalChecks++;
            if (allContracts != null)
            {
                foreach (WorldMapRuntimeGameplayConsumerContract contract in allContracts)
                {
                    if (contract == null) continue;
                    if (contract.IsGeneratedPlaceholderContent)
                        generatedPlaceholderCount++;
                    else
                        userAssignedFinalCount++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 40: Generated placeholder content count: {generatedPlaceholderCount}");
            infoCount++;

            // 41. user-assigned final content count INFO
            totalChecks++;
            log.AppendLine($"{LogPrefix} [INFO] Check 41: User-assigned final content count: {userAssignedFinalCount}");
            infoCount++;

            // 42. scene object transform/name unchanged (snapshot-based)
            totalChecks++;
            log.AppendLine($"{LogPrefix} [INFO] Check 42: Scene object transform/name unchanged - skipped (read-only validation, no snapshot taken).");
            infoCount++;

            // ===== Summary =====
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} Total checks: {totalChecks}");
            log.AppendLine($"{LogPrefix} PASS: {passCount}");
            log.AppendLine($"{LogPrefix} FAIL: {failCount}");
            log.AppendLine($"{LogPrefix} WARN: {warnCount}");
            log.AppendLine($"{LogPrefix} INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Scene consumer contracts: {sceneContractCount}");
            log.AppendLine($"{LogPrefix} Registry count: {registryCount}");
            log.AppendLine($"{LogPrefix} QueryService count: {queryServiceCount}");
            log.AppendLine($"{LogPrefix} Duplicate SourceMarkerIds: {duplicateSourceMarkerIdCount}");
            log.AppendLine($"{LogPrefix} Generated placeholder content: {generatedPlaceholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content: {userAssignedFinalCount}");

            // Console 출력
            if (failCount > 0)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Validation completed with {failCount} failure(s).");
                Debug.LogError(log.ToString());
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] All checks passed.");
                Debug.Log(log.ToString());
            }

            // Dialog summary
            EditorUtility.DisplayDialog(
                "Phase 14.10-N-4: Validate Runtime Gameplay Consumer Contract Query",
                $"Total checks: {totalChecks}\n" +
                $"[PASS] {passCount}\n" +
                $"[FAIL] {failCount}\n" +
                $"[WARN] {warnCount}\n" +
                $"[INFO] {infoCount}\n\n" +
                $"Scene consumer contracts: {sceneContractCount}\n" +
                $"Registry count: {registryCount}\n" +
                $"QueryService count: {queryServiceCount}\n" +
                $"Duplicate SourceMarkerIds: {duplicateSourceMarkerIdCount}\n" +
                $"Generated placeholder content: {generatedPlaceholderCount}\n" +
                $"User-assigned final content: {userAssignedFinalCount}",
                "OK");
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
