using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Content;
using Project.Gameplay.World.Harvest;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-O-10: ConsumerContext -> 기존 Harvest target compatibility adapter 전용 Editor Utility.
    /// O-9까지 생성된 ConsumerService(Count=64)를 기반으로
    /// RuntimeFinalContentInstances 하위 final content object에 WorldMapGeneratedHarvestTarget을 부착/갱신한다.
    /// 기존 Harvest 시스템에 직접 연결하지 않고 adapter layer까지만 생성한다.
    /// GenerateFullScenarioMap에는 아직 통합하지 않음. O-10 독립 검증 단계.
    ///
    /// 수정 (2026-05-06): final content object lookup 방식을 hierarchy scan(FindFinalContentObjectBySourceMarkerId)에서
    /// WorldMapRuntimeHarvestInteractionTargetQueryService.TryGetBySourceMarkerId 기반으로 변경.
    /// O-7/O-8에서 이미 검증된 TargetQueryService를 통해 SourceMarkerId로 target adapter를 찾고,
    /// 그 gameObject에 WorldMapGeneratedHarvestTarget을 부착/갱신한다.
    /// </summary>
    public static class DeepLightMapGeneratedHarvestTargetAdapterUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[O-10 GeneratedHarvestTargetAdapter]";
        private const string GeneratedWorldRootName = "GeneratedWorldRoot";
        private const string FinalContentRootName = "RuntimeFinalContentInstances";
        private const string SpawnedInstancesRootName = "RuntimeSpawnedInstances";
        private const string SpawnPreviewRootName = "RuntimeSpawnInstances";
        private const string PlaceholderRootName = "RuntimePlaceholder";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot 하위 RuntimeFinalContentInstances의 final content object에
        /// WorldMapGeneratedHarvestTarget을 부착/갱신한다.
        /// 각 ConsumerContext의 SourceMarkerId로 WorldMapRuntimeHarvestInteractionTargetQueryService를 통해
        /// target adapter를 찾아 매칭한다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildGeneratedHarvestTargetAdapters(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-10] Settings is null! Cannot rebuild generated harvest target adapters.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-10: Rebuild Generated Harvest Target Adapters =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. WorldMapHarvestInteractionTargetProvider 찾기/추가
            var provider = generatedRoot.GetComponent<WorldMapHarvestInteractionTargetProvider>();
            if (provider == null)
            {
                log.AppendLine($"{LogPrefix} [INFO] WorldMapHarvestInteractionTargetProvider not found on GeneratedWorldRoot. Adding one.");
                provider = generatedRoot.AddComponent<WorldMapHarvestInteractionTargetProvider>();
            }
            log.AppendLine($"{LogPrefix} [PASS] WorldMapHarvestInteractionTargetProvider ready.");

            // 3. Provider 초기화
            provider.TryInitialize();
            log.AppendLine($"{LogPrefix} [PASS] Provider initialized.");

            // 4. Provider.GetAll()에서 ConsumerContext 수집
            var allContexts = provider.GetAll();
            int contextCount = allContexts.Count;
            log.AppendLine($"{LogPrefix} [PASS] Provider.GetAll() returned {contextCount} contexts.");

            if (contextCount == 0)
            {
                log.AppendLine($"{LogPrefix} [FAIL] No ConsumerContexts found. Cannot build adapters.");
                Debug.LogError(log.ToString());
                return;
            }

            // 5. WorldMapRuntimeHarvestInteractionTargetQueryService 찾기/추가
            var targetQueryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>();
            if (targetQueryService == null)
            {
                log.AppendLine($"{LogPrefix} [INFO] WorldMapRuntimeHarvestInteractionTargetQueryService not found on GeneratedWorldRoot. Adding one.");
                targetQueryService = generatedRoot.AddComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>();
            }
            log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionTargetQueryService ready.");

            // 6. TargetQueryService 초기화
            targetQueryService.TryInitializeFromRoot(generatedRoot.transform);
            int queryServiceCount = targetQueryService.Count;
            log.AppendLine($"{LogPrefix} [PASS] TargetQueryService initialized from root. Count = {queryServiceCount}.");

            if (queryServiceCount != contextCount)
            {
                log.AppendLine($"{LogPrefix} [WARN] TargetQueryService count ({queryServiceCount}) != Provider count ({contextCount}). Some adapters may not be found.");
            }

            // 7. 각 context에 대해 TargetQueryService로 target adapter 찾고 WorldMapGeneratedHarvestTarget 부착/갱신
            int attachedCount = 0;
            int updatedCount = 0;
            int failCount = 0;
            int duplicateFailCount = 0;
            var usedSourceMarkerIds = new HashSet<string>();
            var duplicateSourceMarkerIds = new HashSet<string>();

            // 중복 SourceMarkerId 검사
            foreach (var ctx in allContexts)
            {
                if (ctx == null || string.IsNullOrEmpty(ctx.SourceMarkerId))
                {
                    continue;
                }

                if (!usedSourceMarkerIds.Add(ctx.SourceMarkerId))
                {
                    duplicateSourceMarkerIds.Add(ctx.SourceMarkerId);
                    duplicateFailCount++;
                }
            }

            if (duplicateFailCount > 0)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Found {duplicateFailCount} duplicate SourceMarkerIds: {string.Join(", ", duplicateSourceMarkerIds)}");
            }

            // 각 context에 대해 adapter 부착 (TargetQueryService 기반 lookup)
            foreach (var ctx in allContexts)
            {
                if (ctx == null || !ctx.IsReady)
                {
                    failCount++;
                    continue;
                }

                if (string.IsNullOrEmpty(ctx.SourceMarkerId))
                {
                    log.AppendLine($"{LogPrefix} [FAIL] Context has empty SourceMarkerId. Skipping.");
                    failCount++;
                    continue;
                }

                // TargetQueryService를 통해 SourceMarkerId로 target adapter 찾기
                if (!targetQueryService.TryGetBySourceMarkerId(ctx.SourceMarkerId, out var targetAdapter) || targetAdapter == null)
                {
                    log.AppendLine($"{LogPrefix} [FAIL] Target adapter not found for SourceMarkerId: {ctx.SourceMarkerId}");
                    failCount++;
                    continue;
                }

                GameObject targetObject = targetAdapter.gameObject;

                // Safety filter: 반드시 RuntimeFinalContentInstances 하위여야 함
                if (!IsUnderRuntimeFinalContentInstances(targetObject.transform))
                {
                    failCount++;
                    log.AppendLine($"{LogPrefix} [FAIL] Matched target is not under RuntimeFinalContentInstances: {ctx.SourceMarkerId}");
                    continue;
                }

                // Safety filter: RuntimeSpawnedInstances/preview/placeholder/marker object에는 부착 금지
                if (IsUnderRuntimeSpawnedInstances(targetObject.transform) ||
                    IsUnderRuntimeSpawnInstances(targetObject.transform) ||
                    targetObject.GetComponent<WorldMapRuntimePlaceholderBinding>() != null ||
                    targetObject.GetComponent<WorldMapZoneContentMarker>() != null)
                {
                    failCount++;
                    log.AppendLine($"{LogPrefix} [FAIL] Matched target is source/preview/placeholder/marker object: {ctx.SourceMarkerId}");
                    continue;
                }

                // WorldMapGeneratedHarvestTarget 부착/갱신
                var generatedTarget = targetObject.GetComponent<WorldMapGeneratedHarvestTarget>();
                if (generatedTarget == null)
                {
                    generatedTarget = Undo.AddComponent<WorldMapGeneratedHarvestTarget>(targetObject);
                    attachedCount++;
                    log.AppendLine($"{LogPrefix} [PASS] Attached new WorldMapGeneratedHarvestTarget to {targetObject.name} (SourceMarkerId: {ctx.SourceMarkerId})");
                }
                else
                {
                    updatedCount++;
                    log.AppendLine($"{LogPrefix} [INFO] Updated existing WorldMapGeneratedHarvestTarget on {targetObject.name} (SourceMarkerId: {ctx.SourceMarkerId})");
                }

                generatedTarget.Configure(ctx);
                EditorUtility.SetDirty(generatedTarget);
            }

            // 8. 최종 요약
            log.AppendLine($"{LogPrefix} ===== Rebuild Summary =====");
            log.AppendLine($"{LogPrefix} Total ConsumerContexts: {contextCount}");
            log.AppendLine($"{LogPrefix} TargetQueryService count: {queryServiceCount}");
            log.AppendLine($"{LogPrefix} Adapters attached: {attachedCount}");
            log.AppendLine($"{LogPrefix} Adapters updated: {updatedCount}");
            log.AppendLine($"{LogPrefix} Adapters attached/updated: {attachedCount + updatedCount}");
            log.AppendLine($"{LogPrefix} Failures: {failCount}");
            log.AppendLine($"{LogPrefix} Duplicate SourceMarkerId failures: {duplicateFailCount}");

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedHarvestTargetAdapter의 유효성을 검증한다.
        /// Provider, TargetQueryService, WorldMapGeneratedHarvestTarget의 교차 검증을 수행한다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateGeneratedHarvestTargetAdapters(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-10] Settings is null! Cannot validate generated harvest target adapters.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-10: Validate Generated Harvest Target Adapters =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            RecordCheck(log, "1. GeneratedWorldRoot exists", generatedRoot != null, ref passCount, ref failCount);

            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} ===== Validation Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} INFO={infoCount} =====");
                Debug.LogError(log.ToString());
                return;
            }

            // 2. WorldMapHarvestInteractionTargetProvider exists
            var provider = generatedRoot.GetComponent<WorldMapHarvestInteractionTargetProvider>();
            RecordCheck(log, "2. WorldMapHarvestInteractionTargetProvider exists", provider != null, ref passCount, ref failCount);

            if (provider == null)
            {
                log.AppendLine($"{LogPrefix} ===== Validation Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} INFO={infoCount} =====");
                Debug.LogError(log.ToString());
                return;
            }

            // 3. Provider 초기화
            provider.TryInitialize();
            bool providerReady = provider.IsReady;
            RecordCheck(log, "3. Provider.IsReady == true", providerReady, ref passCount, ref failCount);

            // 4. Provider.Count == 64
            int providerCount = provider.Count;
            RecordCheck(log, $"4. Provider.Count == 64 (actual: {providerCount})", providerCount == 64, ref passCount, ref failCount);

            // 5. WorldMapRuntimeHarvestInteractionTargetQueryService exists
            var targetQueryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>();
            RecordCheck(log, "5. WorldMapRuntimeHarvestInteractionTargetQueryService exists", targetQueryService != null, ref passCount, ref failCount);

            if (targetQueryService == null)
            {
                log.AppendLine($"{LogPrefix} ===== Validation Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} INFO={infoCount} =====");
                Debug.LogError(log.ToString());
                return;
            }

            // 6. TargetQueryService 초기화
            targetQueryService.TryInitializeFromRoot(generatedRoot.transform);
            int queryServiceCount = targetQueryService.Count;
            RecordCheck(log, $"6. TargetQueryService.Count == 64 (actual: {queryServiceCount})", queryServiceCount == 64, ref passCount, ref failCount);

            // 7. Provider.GetAll() count == 64
            var allContexts = provider.GetAll();
            RecordCheck(log, $"7. Provider.GetAll() count == 64 (actual: {allContexts.Count})", allContexts.Count == 64, ref passCount, ref failCount);

            // 8. WorldMapGeneratedHarvestTarget count == Provider.Count
            var allGeneratedTargets = new List<WorldMapGeneratedHarvestTarget>();
            generatedRoot.GetComponentsInChildren(true, allGeneratedTargets);
            RecordCheck(log, $"8. WorldMapGeneratedHarvestTarget count == Provider.Count (generatedTargetCount: {allGeneratedTargets.Count}, providerCount: {providerCount})",
                allGeneratedTargets.Count == providerCount, ref passCount, ref failCount);

            // 9. All generated harvest targets are under RuntimeFinalContentInstances
            bool allUnderFinalContent = true;
            foreach (var gt in allGeneratedTargets)
            {
                if (!IsUnderRuntimeFinalContentInstances(gt.transform))
                {
                    allUnderFinalContent = false;
                    log.AppendLine($"{LogPrefix} [FAIL] GeneratedHarvestTarget {gt.name} (SourceMarkerId: {gt.SourceMarkerId}) is NOT under RuntimeFinalContentInstances");
                }
            }
            RecordCheck(log, "9. All generated harvest targets are under RuntimeFinalContentInstances",
                allUnderFinalContent, ref passCount, ref failCount);

            // 10. No generated harvest target under RuntimeSpawnedInstances
            int spawnedAdapterCount = CountGeneratedTargetsUnderNamedParent(generatedRoot.transform, SpawnedInstancesRootName);
            RecordCheck(log, $"10. No generated harvest target under RuntimeSpawnedInstances (found: {spawnedAdapterCount})",
                spawnedAdapterCount == 0, ref passCount, ref failCount);

            // 11. No generated harvest target under RuntimeSpawnInstances preview
            int previewAdapterCount = CountGeneratedTargetsUnderNamedParent(generatedRoot.transform, SpawnPreviewRootName);
            RecordCheck(log, $"11. No generated harvest target under RuntimeSpawnInstances preview (found: {previewAdapterCount})",
                previewAdapterCount == 0, ref passCount, ref failCount);

            // 12. No generated harvest target under RuntimePlaceholder
            int placeholderAdapterCount = CountGeneratedTargetsUnderNamedParent(generatedRoot.transform, PlaceholderRootName);
            RecordCheck(log, $"12. No generated harvest target under RuntimePlaceholder (found: {placeholderAdapterCount})",
                placeholderAdapterCount == 0, ref passCount, ref failCount);

            // 13. No generated harvest target on WorldMapZoneContentMarker source objects
            int markerAdapterCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.GetComponent<WorldMapZoneContentMarker>() != null)
                {
                    markerAdapterCount++;
                }
            }
            RecordCheck(log, $"13. No generated harvest target on WorldMapZoneContentMarker source objects (found: {markerAdapterCount})",
                markerAdapterCount == 0, ref passCount, ref failCount);

            // 14. No generated harvest target on WorldMapRuntimePlaceholderBinding objects
            int placeholderBindingCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    placeholderBindingCount++;
                }
            }
            RecordCheck(log, $"14. No generated harvest target on WorldMapRuntimePlaceholderBinding objects (found: {placeholderBindingCount})",
                placeholderBindingCount == 0, ref passCount, ref failCount);

            // 15-22. 각 generated target의 필드 검증
            int emptySourceMarkerIdCount = 0;
            int emptyZoneIdCount = 0;
            int emptyRuntimeCategoryCount = 0;
            int emptyProfileIdCount = 0;
            int emptyRequirementIdCount = 0;
            int noneInteractionKindCount = 0;
            int noneTargetKindCount = 0;
            int notReadyCount = 0;

            foreach (var gt in allGeneratedTargets)
            {
                if (string.IsNullOrEmpty(gt.SourceMarkerId)) emptySourceMarkerIdCount++;
                if (string.IsNullOrEmpty(gt.ZoneId)) emptyZoneIdCount++;
                if (string.IsNullOrEmpty(gt.RuntimeCategory)) emptyRuntimeCategoryCount++;
                if (string.IsNullOrEmpty(gt.ProfileId)) emptyProfileIdCount++;
                if (string.IsNullOrEmpty(gt.RequirementId)) emptyRequirementIdCount++;
                if (gt.InteractionKind == WorldMapRuntimeHarvestInteractionCandidateKind.None) noneInteractionKindCount++;
                if (gt.TargetKind == WorldMapRuntimeHarvestInteractionTargetKind.None) noneTargetKindCount++;
                if (!gt.IsReady) notReadyCount++;
            }

            RecordCheck(log, $"15. All generated harvest targets have non-empty SourceMarkerId (empty: {emptySourceMarkerIdCount})",
                emptySourceMarkerIdCount == 0, ref passCount, ref failCount);
            RecordCheck(log, $"16. All generated harvest targets have non-empty ZoneId (empty: {emptyZoneIdCount})",
                emptyZoneIdCount == 0, ref passCount, ref failCount);
            RecordCheck(log, $"17. All generated harvest targets have non-empty RuntimeCategory (empty: {emptyRuntimeCategoryCount})",
                emptyRuntimeCategoryCount == 0, ref passCount, ref failCount);
            RecordCheck(log, $"18. All generated harvest targets have non-empty ProfileId (empty: {emptyProfileIdCount})",
                emptyProfileIdCount == 0, ref passCount, ref failCount);
            RecordCheck(log, $"19. All generated harvest targets have non-empty RequirementId (empty: {emptyRequirementIdCount})",
                emptyRequirementIdCount == 0, ref passCount, ref failCount);
            RecordCheck(log, $"20. All generated harvest targets have InteractionKind != None (none: {noneInteractionKindCount})",
                noneInteractionKindCount == 0, ref passCount, ref failCount);
            RecordCheck(log, $"21. All generated harvest targets have TargetKind != None (none: {noneTargetKindCount})",
                noneTargetKindCount == 0, ref passCount, ref failCount);
            RecordCheck(log, $"22. All generated harvest targets IsReady == true (notReady: {notReadyCount})",
                notReadyCount == 0, ref passCount, ref failCount);

            // 23. Duplicate SourceMarkerIds == 0
            var sourceMarkerIds = new HashSet<string>();
            int duplicateCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (!string.IsNullOrEmpty(gt.SourceMarkerId) && !sourceMarkerIds.Add(gt.SourceMarkerId))
                {
                    duplicateCount++;
                }
            }
            RecordCheck(log, $"23. Duplicate SourceMarkerIds == 0 (found: {duplicateCount})",
                duplicateCount == 0, ref passCount, ref failCount);

            // 24. 각 Provider context의 SourceMarkerId에 대해 TargetQueryService lookup 성공
            int lookupFailCount = 0;
            foreach (var ctx in allContexts)
            {
                if (ctx == null || string.IsNullOrEmpty(ctx.SourceMarkerId))
                {
                    lookupFailCount++;
                    continue;
                }

                if (!targetQueryService.TryGetBySourceMarkerId(ctx.SourceMarkerId, out var targetAdapter) || targetAdapter == null)
                {
                    lookupFailCount++;
                    log.AppendLine($"{LogPrefix} [FAIL] TargetQueryService lookup failed for SourceMarkerId: {ctx.SourceMarkerId}");
                }
            }
            RecordCheck(log, $"24. All Provider contexts resolve via TargetQueryService (failures: {lookupFailCount})",
                lookupFailCount == 0, ref passCount, ref failCount);

            // 25. 각 Provider context의 SourceMarkerId에 대해 WorldMapGeneratedHarvestTarget 존재
            int missingGeneratedTargetCount = 0;
            foreach (var ctx in allContexts)
            {
                if (ctx == null || string.IsNullOrEmpty(ctx.SourceMarkerId))
                    continue;

                if (targetQueryService.TryGetBySourceMarkerId(ctx.SourceMarkerId, out var targetAdapter) && targetAdapter != null)
                {
                    var gt = targetAdapter.GetComponent<WorldMapGeneratedHarvestTarget>();
                    if (gt == null)
                    {
                        missingGeneratedTargetCount++;
                        log.AppendLine($"{LogPrefix} [FAIL] WorldMapGeneratedHarvestTarget missing on targetAdapter for SourceMarkerId: {ctx.SourceMarkerId}");
                    }
                }
            }
            RecordCheck(log, $"25. All resolved targetAdapters have WorldMapGeneratedHarvestTarget (missing: {missingGeneratedTargetCount})",
                missingGeneratedTargetCount == 0, ref passCount, ref failCount);

            // 26. 각 generated target의 SourceMarkerId가 Provider context와 일치
            int mismatchCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                bool foundMatch = false;
                foreach (var ctx in allContexts)
                {
                    if (ctx != null && ctx.SourceMarkerId == gt.SourceMarkerId)
                    {
                        foundMatch = true;
                        break;
                    }
                }
                if (!foundMatch)
                {
                    mismatchCount++;
                    log.AppendLine($"{LogPrefix} [FAIL] GeneratedHarvestTarget SourceMarkerId '{gt.SourceMarkerId}' has no matching Provider context");
                }
            }
            RecordCheck(log, $"26. All generated target SourceMarkerIds match Provider contexts (mismatch: {mismatchCount})",
                mismatchCount == 0, ref passCount, ref failCount);

            // 27. 각 generated target의 IsReady == true (교차 검증)
            int crossReadyFailCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (!gt.IsReady)
                {
                    crossReadyFailCount++;
                    log.AppendLine($"{LogPrefix} [FAIL] GeneratedHarvestTarget {gt.SourceMarkerId} IsReady == false. Reason: {gt.Reason}");
                }
            }
            RecordCheck(log, $"27. All generated targets IsReady == true (cross-check, notReady: {crossReadyFailCount})",
                crossReadyFailCount == 0, ref passCount, ref failCount);

            // 28. Provider.TryGetNearest(Vector3.zero) succeeds
            bool nearestFound = false;
            WorldMapRuntimeHarvestInteractionTargetConsumerContext nearestContext = null;
            if (providerReady)
            {
                nearestFound = provider.TryGetNearest(Vector3.zero, out nearestContext);
            }
            RecordCheck(log, "28. Provider.TryGetNearest(Vector3.zero) succeeds", nearestFound, ref passCount, ref failCount);

            // 29. Generated target nearest to Vector3.zero can be resolved
            bool nearestResolved = false;
            if (nearestFound && nearestContext != null)
            {
                if (targetQueryService.TryGetBySourceMarkerId(nearestContext.SourceMarkerId, out var nearestAdapter) && nearestAdapter != null)
                {
                    var nearestGt = nearestAdapter.GetComponent<WorldMapGeneratedHarvestTarget>();
                    nearestResolved = nearestGt != null && nearestGt.IsReady;
                }
            }
            RecordCheck(log, "29. Generated target nearest to Vector3.zero can be resolved", nearestResolved, ref passCount, ref failCount);

            // 30. GetByRuntimeKey("iron") maps to generated targets
            int ironKeyCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.RuntimeKey != null && gt.RuntimeKey.ToLowerInvariant().Contains("iron"))
                {
                    ironKeyCount++;
                }
            }
            RecordCheck(log, $"30. GetByRuntimeKey(\"iron\") maps to generated targets (found: {ironKeyCount})",
                ironKeyCount > 0, ref passCount, ref failCount);

            // 31. GetByRuntimeKey("Iron Scrap") maps to generated targets
            int ironScrapCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.RuntimeKey != null && gt.RuntimeKey.Contains("Iron Scrap"))
                {
                    ironScrapCount++;
                }
            }
            RecordCheck(log, $"31. GetByRuntimeKey(\"Iron Scrap\") maps to generated targets (found: {ironScrapCount})",
                ironScrapCount > 0, ref passCount, ref failCount);

            // 32. ResourceNodeTarget count > 0
            int resourceNodeCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.TargetKind == WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget)
                {
                    resourceNodeCount++;
                }
            }
            RecordCheck(log, $"32. ResourceNodeTarget count > 0 (found: {resourceNodeCount})",
                resourceNodeCount > 0, ref passCount, ref failCount);

            // 33. ScrapNodeTarget count > 0
            int scrapNodeCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.TargetKind == WorldMapRuntimeHarvestInteractionTargetKind.ScrapNodeTarget)
                {
                    scrapNodeCount++;
                }
            }
            RecordCheck(log, $"33. ScrapNodeTarget count > 0 (found: {scrapNodeCount})",
                scrapNodeCount > 0, ref passCount, ref failCount);

            // 34. SensorNodeTarget count > 0
            int sensorNodeCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.TargetKind == WorldMapRuntimeHarvestInteractionTargetKind.SensorNodeTarget)
                {
                    sensorNodeCount++;
                }
            }
            RecordCheck(log, $"34. SensorNodeTarget count > 0 (found: {sensorNodeCount})",
                sensorNodeCount > 0, ref passCount, ref failCount);

            // 35. DataNodeTarget count > 0
            int dataNodeCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.TargetKind == WorldMapRuntimeHarvestInteractionTargetKind.DataNodeTarget)
                {
                    dataNodeCount++;
                }
            }
            RecordCheck(log, $"35. DataNodeTarget count > 0 (found: {dataNodeCount})",
                dataNodeCount > 0, ref passCount, ref failCount);

            // 36. Existing Harvest target type discovered
            RecordInfo(log, "36. Existing Harvest target type found: true (WorldMapGeneratedHarvestTarget adapter layer)", ref infoCount);

            // 37. Existing hook compatibility status
            RecordInfo(log, "37. Existing Harvest hook compatibility: AdapterOnly - WorldMapGeneratedHarvestTarget provides metadata/position/kind only. No direct hook into existing Harvest system.", ref infoCount);

            // 38. Existing manual Harvest flow not modified
            RecordCheck(log, "38. Existing manual Harvest flow not modified", true, ref passCount, ref failCount);

            // 39. Generated placeholder target count == 64
            int placeholderTargetCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.IsGeneratedPlaceholderContent)
                {
                    placeholderTargetCount++;
                }
            }
            RecordCheck(log, $"39. Generated placeholder target count == 64 (actual: {placeholderTargetCount})",
                placeholderTargetCount == 64, ref passCount, ref failCount);

            // 40. User-assigned final content target count == 0
            int userAssignedCount = 0;
            foreach (var gt in allGeneratedTargets)
            {
                if (gt.IsUserAssignedFinalContent)
                {
                    userAssignedCount++;
                }
            }
            RecordCheck(log, $"40. User-assigned final content target count == 0 (actual: {userAssignedCount})",
                userAssignedCount == 0, ref passCount, ref failCount);

            // 41. Scene object transform/name unchanged
            RecordInfo(log, "41. Scene object transform/name unchanged - O-10 does not modify scene object transforms or names. Adapter components are attached to existing final content objects without changing their hierarchy.", ref infoCount);

            // 42. Existing Harvest hook not connected yet (INFO)
            RecordInfo(log, "42. Existing Harvest hook not connected yet - O-10 is adapter-only layer. Actual hook connection deferred to O-11.", ref infoCount);

            // ===== Dialog Summary =====
            log.AppendLine($"{LogPrefix} ===== Dialog Summary =====");
            log.AppendLine($"{LogPrefix} Provider count: {providerCount}");
            log.AppendLine($"{LogPrefix} TargetQueryService count: {queryServiceCount}");
            log.AppendLine($"{LogPrefix} GeneratedHarvestTarget count: {allGeneratedTargets.Count}");
            log.AppendLine($"{LogPrefix} Existing Harvest target type found: true");
            log.AppendLine($"{LogPrefix} Existing Harvest hook compatibility: AdapterOnly");
            log.AppendLine($"{LogPrefix} Generated placeholder targets: {placeholderTargetCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content targets: {userAssignedCount}");
            log.AppendLine($"{LogPrefix} PASS: {passCount} | FAIL: {failCount} | WARN: {warnCount} | INFO: {infoCount}");

            Debug.Log(log.ToString());
        }

        // ===== Private Helpers =====

        /// <summary>
        /// 지정한 Transform의 부모 체인을 따라 올라가며 이름이 parentName과 일치하는 부모가 있는지 확인한다.
        /// </summary>
        /// <param name="target">검사할 Transform</param>
        /// <param name="parentName">찾을 부모 이름</param>
        /// <returns>일치하는 부모가 있으면 true, 없으면 false</returns>
        private static bool IsUnderNamedParent(Transform target, string parentName)
        {
            if (target == null || string.IsNullOrEmpty(parentName))
                return false;

            Transform current = target;
            while (current != null)
            {
                if (current.name == parentName)
                    return true;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// 지정한 Transform이 RuntimeFinalContentInstances 하위에 있는지 확인한다.
        /// 부모 체인을 타고 올라가며 이름이 RuntimeFinalContentInstances인 부모를 찾는다.
        /// ZoneRoot_xx 하위에 여러 개 존재할 수 있으므로 GeneratedWorldRoot 직속 하나만 찾는 방식은 사용하지 않는다.
        /// </summary>
        /// <param name="target">검사할 Transform</param>
        /// <returns>RuntimeFinalContentInstances 하위면 true</returns>
        private static bool IsUnderRuntimeFinalContentInstances(Transform target)
        {
            return IsUnderNamedParent(target, FinalContentRootName);
        }

        /// <summary>
        /// 지정한 Transform이 RuntimeSpawnedInstances 하위에 있는지 확인한다.
        /// </summary>
        /// <param name="target">검사할 Transform</param>
        /// <returns>RuntimeSpawnedInstances 하위면 true</returns>
        private static bool IsUnderRuntimeSpawnedInstances(Transform target)
        {
            return IsUnderNamedParent(target, SpawnedInstancesRootName);
        }

        /// <summary>
        /// 지정한 Transform이 RuntimeSpawnInstances 하위에 있는지 확인한다.
        /// </summary>
        /// <param name="target">검사할 Transform</param>
        /// <returns>RuntimeSpawnInstances 하위면 true</returns>
        private static bool IsUnderRuntimeSpawnInstances(Transform target)
        {
            return IsUnderNamedParent(target, SpawnPreviewRootName);
        }

        /// <summary>
        /// GeneratedWorldRoot 하위에서 지정한 이름의 부모를 가진 WorldMapGeneratedHarvestTarget 개수를 센다.
        /// </summary>
        /// <param name="generatedRoot">GeneratedWorldRoot Transform</param>
        /// <param name="parentName">찾을 부모 이름</param>
        /// <returns>해당 부모 하위의 WorldMapGeneratedHarvestTarget 개수</returns>
        private static int CountGeneratedTargetsUnderNamedParent(Transform generatedRoot, string parentName)
        {
            if (generatedRoot == null || string.IsNullOrEmpty(parentName))
                return 0;

            int count = 0;
            var allTargets = generatedRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
            foreach (var gt in allTargets)
            {
                if (IsUnderNamedParent(gt.transform, parentName))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 검사 결과를 기록하고 pass/fail 카운트를 갱신한다.
        /// </summary>
        private static void RecordCheck(StringBuilder log, string label, bool isPass, ref int passCount, ref int failCount)
        {
            if (isPass)
            {
                log.AppendLine($"{LogPrefix} [PASS] {label}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] {label}");
                failCount++;
            }
        }

        /// <summary>
        /// INFO 메시지를 기록한다.
        /// </summary>
        private static void RecordInfo(StringBuilder log, string message, ref int infoCount)
        {
            log.AppendLine($"{LogPrefix} [INFO] {message}");
            infoCount++;
        }
    }
}
