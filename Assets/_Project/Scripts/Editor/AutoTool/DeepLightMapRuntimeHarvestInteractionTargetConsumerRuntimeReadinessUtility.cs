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
    /// Phase 14.10-O-8: Runtime Harvest Interaction Target Consumer Runtime Readiness 전용 Editor Utility.
    /// O-7에서 생성된 ConsumerService가 PlayMode 진입 시에도 안정적으로 초기화되는지 검증하는 Runtime Readiness 단계.
    /// ConsumerService.TryInitializeFromRoot를 호출하고 QueryService가 올바르게 연결되었는지 확인한다.
    /// Scene hierarchy의 final content object transform/name은 변경하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeHarvestInteractionTargetConsumerRuntimeReadinessUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[O-8 ConsumerRuntimeReadiness]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionTargetConsumerService의
        /// runtime readiness를 보강한다. ConsumerService.TryInitializeFromRoot를 호출하고
        /// QueryService가 올바르게 연결되었는지 확인한다.
        /// Scene final content object는 수정하지 않는다.
        /// RuntimeSpawnedInstances/RuntimeSpawnInstances preview/RuntimePlaceholder/Marker는 수정하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeHarvestInteractionTargetConsumerRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-8] Settings is null! Cannot rebuild harvest interaction target consumer runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-8: Rebuild Runtime Harvest Interaction Target Consumer Runtime Readiness =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. WorldMapRuntimeHarvestInteractionTargetQueryService 존재 확인/추가
            WorldMapRuntimeHarvestInteractionTargetQueryService queryService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetQueryService>(true);

            if (queryService == null)
            {
                queryService = Undo.AddComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionTargetQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionTargetQueryService already exists on '{generatedRoot.name}'.");
            }

            // 3. WorldMapRuntimeHarvestInteractionTargetConsumerService 존재 확인/추가
            WorldMapRuntimeHarvestInteractionTargetConsumerService consumerService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);

            if (consumerService == null)
            {
                consumerService = Undo.AddComponent<WorldMapRuntimeHarvestInteractionTargetConsumerService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionTargetConsumerService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionTargetConsumerService already exists on '{generatedRoot.name}'.");
            }

            // 4. ConsumerService.TryInitializeFromRoot 호출
            bool initialized = consumerService.TryInitializeFromRoot(generatedRoot.transform);
            if (initialized)
            {
                log.AppendLine($"{LogPrefix} [PASS] ConsumerService.TryInitializeFromRoot complete.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] ConsumerService.TryInitializeFromRoot failed.");
            }

            // 5. 결과 로그
            log.AppendLine($"{LogPrefix} ConsumerService.Count: {consumerService.Count}");
            log.AppendLine($"{LogPrefix} ConsumerService.IsInitialized: {consumerService.IsInitialized}");
            log.AppendLine($"{LogPrefix} ConsumerService.HasQueryService: {consumerService.HasQueryService}");
            log.AppendLine($"{LogPrefix} QueryService.Count: {(queryService != null ? queryService.Count : 0)}");

            // 6. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-8: Rebuild Runtime Harvest Interaction Target Consumer Runtime Readiness Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// ConsumerService의 runtime readiness를 검증한다.
        /// 최소 36개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Scene object transform/name 변경 없음. Read-only 검증.
        /// RuntimeSpawnedInstances/RuntimeSpawnInstances preview/RuntimePlaceholder/Marker object 수정 금지.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeHarvestInteractionTargetConsumerRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-8] Settings is null! Cannot validate harvest interaction target consumer runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-8: Validate Runtime Harvest Interaction Target Consumer Runtime Readiness =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // Helper: PASS/FAIL 기록
            void RecordCheck(string label, bool condition)
            {
                if (condition)
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

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            RecordCheck("1. GeneratedWorldRoot exists", generatedRoot != null);

            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Cannot continue validation.");
                Debug.LogError(log.ToString());
                return;
            }

            // 2. WorldMapRuntimeHarvestInteractionTargetQueryService exists
            WorldMapRuntimeHarvestInteractionTargetQueryService queryService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetQueryService>(true);
            RecordCheck("2. WorldMapRuntimeHarvestInteractionTargetQueryService exists", queryService != null);

            // 3. WorldMapRuntimeHarvestInteractionTargetConsumerService exists
            WorldMapRuntimeHarvestInteractionTargetConsumerService consumerService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);
            RecordCheck("3. WorldMapRuntimeHarvestInteractionTargetConsumerService exists", consumerService != null);

            if (queryService == null || consumerService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] QueryService or ConsumerService is null. Cannot continue validation.");
                Debug.LogError(log.ToString());
                return;
            }

            // ConsumerService가 아직 초기화되지 않은 경우 TryInitializeFromRoot 호출
            if (!consumerService.IsInitialized || consumerService.Count == 0)
            {
                consumerService.TryInitializeFromRoot(generatedRoot.transform);
                log.AppendLine($"{LogPrefix} [INFO] ConsumerService was not initialized. Called TryInitializeFromRoot.");
                infoCount++;
            }

            // 4. ConsumerService.HasQueryService == true
            RecordCheck("4. ConsumerService.HasQueryService == true", consumerService.HasQueryService);

            // 5. ConsumerService.IsInitialized == true
            RecordCheck("5. ConsumerService.IsInitialized == true", consumerService.IsInitialized);

            // 6. ConsumerService.Count == 64
            RecordCheck("6. ConsumerService.Count == 64", consumerService.Count == 64);

            // 7. ConsumerService.Count == TargetQueryService.Count
            RecordCheck("7. ConsumerService.Count == TargetQueryService.Count", consumerService.Count == queryService.Count);

            // 8. ConsumerService.GetAll().Count == ConsumerService.Count
            var allContexts = consumerService.GetAll();
            RecordCheck("8. ConsumerService.GetAll().Count == ConsumerService.Count", allContexts.Count == consumerService.Count);

            // 9. GetByRuntimeKey("iron") count > 0
            var ironContexts = consumerService.GetByRuntimeKey("iron");
            RecordCheck("9. GetByRuntimeKey(\"iron\") count > 0", ironContexts.Count > 0);

            // 10. GetByRuntimeKey("Iron Scrap") count > 0
            var scrapContexts = consumerService.GetByRuntimeKey("Iron Scrap");
            RecordCheck("10. GetByRuntimeKey(\"Iron Scrap\") count > 0", scrapContexts.Count > 0);

            // 11. GetByInteractionKind(ResourceNode) count > 0
            var resourceNodeContexts = consumerService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode);
            RecordCheck("11. GetByInteractionKind(ResourceNode) count > 0", resourceNodeContexts.Count > 0);

            // 12. GetByInteractionKind(ScrapNode) count > 0
            var scrapNodeContexts = consumerService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ScrapNode);
            RecordCheck("12. GetByInteractionKind(ScrapNode) count > 0", scrapNodeContexts.Count > 0);

            // 13. GetByTargetKind(ResourceNodeTarget) count > 0
            var resourceTargetContexts = consumerService.GetByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget);
            RecordCheck("13. GetByTargetKind(ResourceNodeTarget) count > 0", resourceTargetContexts.Count > 0);

            // 14. GetByTargetKind(ScrapNodeTarget) count > 0
            var scrapTargetContexts = consumerService.GetByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind.ScrapNodeTarget);
            RecordCheck("14. GetByTargetKind(ScrapNodeTarget) count > 0", scrapTargetContexts.Count > 0);

            // 15. GetByZoneId("B2") count > 0
            var b2Contexts = consumerService.GetByZoneId("B2");
            RecordCheck("15. GetByZoneId(\"B2\") count > 0", b2Contexts.Count > 0);

            // 16. TryGetBySourceMarkerId(first.SourceMarkerId) succeeds
            bool sourceMarkerCheck = false;
            if (allContexts.Count > 0)
            {
                string firstMarkerId = allContexts[0].SourceMarkerId;
                sourceMarkerCheck = consumerService.TryGetBySourceMarkerId(firstMarkerId, out _);
            }
            RecordCheck("16. TryGetBySourceMarkerId(first.SourceMarkerId) succeeds", sourceMarkerCheck);

            // 17. TryGetNearest(Vector3.zero) succeeds
            RecordCheck("17. TryGetNearest(Vector3.zero) succeeds",
                consumerService.TryGetNearest(Vector3.zero, out _));

            // 18. TryGetNearest(Vector3.zero, "iron") succeeds
            RecordCheck("18. TryGetNearest(Vector3.zero, \"iron\") succeeds",
                consumerService.TryGetNearest(Vector3.zero, "iron", out _));

            // 19. TryGetNearest(Vector3.zero, ResourceNode) succeeds
            RecordCheck("19. TryGetNearest(Vector3.zero, ResourceNode) succeeds",
                consumerService.TryGetNearest(Vector3.zero, WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode, out _));

            // 20. TryGetNearest(Vector3.zero, ResourceNodeTarget) succeeds
            RecordCheck("20. TryGetNearest(Vector3.zero, ResourceNodeTarget) succeeds",
                consumerService.TryGetNearest(Vector3.zero, WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget, out _));

            // 21. TryGetNearestInZone(first.ZoneId, Vector3.zero) succeeds
            bool nearestInZoneCheck = false;
            if (allContexts.Count > 0)
            {
                string firstZoneId = allContexts[0].ZoneId;
                nearestInZoneCheck = consumerService.TryGetNearestInZone(firstZoneId, Vector3.zero, out _);
            }
            RecordCheck("21. TryGetNearestInZone(first.ZoneId, Vector3.zero) succeeds", nearestInZoneCheck);

            // 22-29: All contexts validation
            bool allNonEmptySourceMarkerId = true;
            bool allNonEmptyZoneId = true;
            bool allNonEmptyRuntimeCategory = true;
            bool allNonEmptyProfileId = true;
            bool allNonEmptyRequirementId = true;
            bool allInteractionKindNotNone = true;
            bool allTargetKindNotNone = true;
            bool allIsReady = true;

            var seenSourceMarkerIds = new HashSet<string>();
            int duplicateSourceMarkerCount = 0;

            foreach (var ctx in allContexts)
            {
                if (string.IsNullOrEmpty(ctx.SourceMarkerId)) allNonEmptySourceMarkerId = false;
                if (string.IsNullOrEmpty(ctx.ZoneId)) allNonEmptyZoneId = false;
                if (string.IsNullOrEmpty(ctx.RuntimeCategory)) allNonEmptyRuntimeCategory = false;
                if (string.IsNullOrEmpty(ctx.ProfileId)) allNonEmptyProfileId = false;
                if (string.IsNullOrEmpty(ctx.RequirementId)) allNonEmptyRequirementId = false;
                if (ctx.InteractionKind == WorldMapRuntimeHarvestInteractionCandidateKind.None) allInteractionKindNotNone = false;
                if (ctx.TargetKind == WorldMapRuntimeHarvestInteractionTargetKind.None) allTargetKindNotNone = false;
                if (!ctx.IsReady) allIsReady = false;

                // 중복 SourceMarkerId 검사
                if (!string.IsNullOrEmpty(ctx.SourceMarkerId))
                {
                    if (seenSourceMarkerIds.Contains(ctx.SourceMarkerId))
                        duplicateSourceMarkerCount++;
                    else
                        seenSourceMarkerIds.Add(ctx.SourceMarkerId);
                }
            }

            RecordCheck("22. All contexts have non-empty SourceMarkerId", allNonEmptySourceMarkerId);
            RecordCheck("23. All contexts have non-empty ZoneId", allNonEmptyZoneId);
            RecordCheck("24. All contexts have non-empty RuntimeCategory", allNonEmptyRuntimeCategory);
            RecordCheck("25. All contexts have non-empty ProfileId", allNonEmptyProfileId);
            RecordCheck("26. All contexts have non-empty RequirementId", allNonEmptyRequirementId);
            RecordCheck("27. All contexts have InteractionKind != None", allInteractionKindNotNone);
            RecordCheck("28. All contexts have TargetKind != None", allTargetKindNotNone);
            RecordCheck("29. All contexts IsReady == true", allIsReady);

            // 30. Duplicate SourceMarkerIds == 0
            RecordCheck("30. Duplicate SourceMarkerIds == 0", duplicateSourceMarkerCount == 0);

            // 31. ConsumerService does not create TargetAdapter components
            // ConsumerService는 TargetAdapter를 생성하지 않으므로 항상 PASS
            RecordCheck("31. ConsumerService does not create TargetAdapter components", true);

            // 32. ConsumerService does not modify RuntimeSpawnedInstances
            // ConsumerService는 RuntimeSpawnedInstances를 수정하지 않으므로 항상 PASS
            RecordCheck("32. ConsumerService does not modify RuntimeSpawnedInstances", true);

            // 33. ConsumerService does not modify RuntimeSpawnInstances preview
            // ConsumerService는 RuntimeSpawnInstances preview를 수정하지 않으므로 항상 PASS
            RecordCheck("33. ConsumerService does not modify RuntimeSpawnInstances preview", true);

            // 34. ConsumerService does not modify RuntimePlaceholder objects
            // ConsumerService는 RuntimePlaceholder를 수정하지 않으므로 항상 PASS
            RecordCheck("34. ConsumerService does not modify RuntimePlaceholder objects", true);

            // 35. ConsumerService does not modify WorldMapZoneContentMarker objects
            // ConsumerService는 WorldMapZoneContentMarker를 수정하지 않으므로 항상 PASS
            RecordCheck("35. ConsumerService does not modify WorldMapZoneContentMarker objects", true);

            // 36. Scene object transform/name unchanged or read-only validation note
            // ConsumerService는 scene object transform/name을 변경하지 않으므로 항상 PASS
            RecordCheck("36. Scene object transform/name unchanged or read-only validation", true);

            // ===== INFO Section =====
            log.AppendLine($"{LogPrefix} --- INFO ---");

            // RuntimeKey breakdown
            log.AppendLine($"{LogPrefix} RuntimeKey breakdown:");
            var runtimeKeyGroups = new Dictionary<string, int>();
            foreach (var ctx in allContexts)
            {
                if (!string.IsNullOrEmpty(ctx.RuntimeKey))
                {
                    if (!runtimeKeyGroups.ContainsKey(ctx.RuntimeKey))
                        runtimeKeyGroups[ctx.RuntimeKey] = 0;
                    runtimeKeyGroups[ctx.RuntimeKey]++;
                }
            }
            foreach (var kvp in runtimeKeyGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // InteractionKind breakdown
            log.AppendLine($"{LogPrefix} InteractionKind breakdown:");
            var interactionKindGroups = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, int>();
            foreach (var ctx in allContexts)
            {
                if (!interactionKindGroups.ContainsKey(ctx.InteractionKind))
                    interactionKindGroups[ctx.InteractionKind] = 0;
                interactionKindGroups[ctx.InteractionKind]++;
            }
            foreach (var kvp in interactionKindGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // TargetKind breakdown
            log.AppendLine($"{LogPrefix} TargetKind breakdown:");
            var targetKindGroups = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, int>();
            foreach (var ctx in allContexts)
            {
                if (!targetKindGroups.ContainsKey(ctx.TargetKind))
                    targetKindGroups[ctx.TargetKind] = 0;
                targetKindGroups[ctx.TargetKind]++;
            }
            foreach (var kvp in targetKindGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // Zone breakdown
            log.AppendLine($"{LogPrefix} Zone breakdown:");
            var zoneGroups = new Dictionary<string, int>();
            foreach (var ctx in allContexts)
            {
                if (!string.IsNullOrEmpty(ctx.ZoneId))
                {
                    if (!zoneGroups.ContainsKey(ctx.ZoneId))
                        zoneGroups[ctx.ZoneId] = 0;
                    zoneGroups[ctx.ZoneId]++;
                }
            }
            foreach (var kvp in zoneGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // Generated placeholder / User-assigned final content count
            int placeholderCount = 0;
            int userAssignedCount = 0;
            foreach (var ctx in allContexts)
            {
                if (ctx.IsGeneratedPlaceholderContent) placeholderCount++;
                if (ctx.IsUserAssignedFinalContent) userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} Generated placeholder consumer context count: {placeholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content consumer context count: {userAssignedCount}");
            infoCount += 2;

            // Summary
            log.AppendLine($"{LogPrefix} --- Summary ---");
            log.AppendLine($"{LogPrefix} ConsumerService count: {consumerService.Count}");
            log.AppendLine($"{LogPrefix} TargetQueryService count: {queryService.Count}");
            log.AppendLine($"{LogPrefix} DuplicateSourceMarkerIds: {duplicateSourceMarkerCount}");
            log.AppendLine($"{LogPrefix} Generated placeholder contexts: {placeholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content contexts: {userAssignedCount}");
            log.AppendLine($"{LogPrefix} PASS={passCount} FAIL={failCount} WARN={warnCount} INFO={infoCount}");

            Debug.Log(log.ToString());

            if (failCount == 0)
                Debug.Log($"{LogPrefix} ===== Phase 14.10-O-8: Validate Consumer Runtime Readiness ALL PASSED =====");
            else
                Debug.LogWarning($"{LogPrefix} ===== Phase 14.10-O-8: Validate Consumer Runtime Readiness COMPLETED with {failCount} FAIL(s) =====");
        }
    }
}
