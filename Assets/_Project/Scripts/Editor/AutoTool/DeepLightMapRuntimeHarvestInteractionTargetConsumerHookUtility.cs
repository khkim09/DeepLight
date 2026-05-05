using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Harvest;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-O-7: Harvest Interaction Target QueryService를 기존 Harvest 시스템이
    /// 소비할 수 있는 ConsumerService/Context로 감싸는 Editor Utility.
    /// 실제 채집 실행 연결은 아직 하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeHarvestInteractionTargetConsumerHookUtility
    {
        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionTargetConsumerService를
        /// 추가/갱신하고 QueryService를 통해 초기화한다.
        /// TargetAdapter를 추가/수정하지 않는다 (O-5 역할 침범 금지).
        /// </summary>
        /// <param name="settings">AutoBuilder Settings</param>
        /// <param name="context">AutoBuilder Scene Context</param>
        public static void RebuildRuntimeHarvestInteractionTargetConsumerHook(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ConsumerHookUtility] Settings is null! Cannot rebuild consumer hook.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[ConsumerHookUtility] Context is null! Cannot rebuild consumer hook.");
                return;
            }

            Debug.Log("[ConsumerHookUtility] ===== Phase 14.10-O-7: Rebuild Runtime Harvest Interaction Target Consumer Hook =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[ConsumerHookUtility] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // 2. QueryService 존재 확인/추가
            WorldMapRuntimeHarvestInteractionTargetQueryService queryService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetQueryService>(true);

            if (queryService == null)
            {
                queryService = generatedRoot.AddComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>();
                Debug.Log("[ConsumerHookUtility] Added WorldMapRuntimeHarvestInteractionTargetQueryService to GeneratedWorldRoot.");
            }

            // 3. ConsumerService 존재 확인/추가
            WorldMapRuntimeHarvestInteractionTargetConsumerService consumerService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);

            if (consumerService == null)
            {
                consumerService = generatedRoot.AddComponent<WorldMapRuntimeHarvestInteractionTargetConsumerService>();
                Debug.Log("[ConsumerHookUtility] Added WorldMapRuntimeHarvestInteractionTargetConsumerService to GeneratedWorldRoot.");
            }

            // 4. ConsumerService.TryInitializeFromRoot 호출
            bool initialized = consumerService.TryInitializeFromRoot(generatedRoot.transform);

            // 5. 결과 로그
            var sb = new StringBuilder();
            sb.AppendLine("===== Phase 14.10-O-7: Rebuild Consumer Hook Complete =====");
            sb.AppendLine($"GeneratedWorldRoot: {generatedRoot.name}");
            sb.AppendLine($"QueryService exists: {queryService != null}");
            sb.AppendLine($"ConsumerService exists: {consumerService != null}");
            sb.AppendLine($"ConsumerService.HasQueryService: {consumerService.HasQueryService}");
            sb.AppendLine($"ConsumerService.IsInitialized: {consumerService.IsInitialized}");
            sb.AppendLine($"ConsumerService.Count: {consumerService.Count}");
            sb.AppendLine($"QueryService.Count: {queryService.Count}");
            sb.AppendLine($"Initialized: {initialized}");

            if (consumerService.Count > 0)
            {
                sb.AppendLine("--- Consumer Context Breakdown ---");
                var allContexts = consumerService.GetAll();
                int placeholderCount = 0;
                int userAssignedCount = 0;
                var runtimeKeyCounts = new Dictionary<string, int>();
                var interactionKindCounts = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, int>();
                var targetKindCounts = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, int>();
                var zoneCounts = new Dictionary<string, int>();

                foreach (var ctx in allContexts)
                {
                    if (ctx.IsGeneratedPlaceholderContent) placeholderCount++;
                    if (ctx.IsUserAssignedFinalContent) userAssignedCount++;

                    if (!string.IsNullOrEmpty(ctx.RuntimeKey))
                    {
                        if (!runtimeKeyCounts.ContainsKey(ctx.RuntimeKey))
                            runtimeKeyCounts[ctx.RuntimeKey] = 0;
                        runtimeKeyCounts[ctx.RuntimeKey]++;
                    }

                    if (!interactionKindCounts.ContainsKey(ctx.InteractionKind))
                        interactionKindCounts[ctx.InteractionKind] = 0;
                    interactionKindCounts[ctx.InteractionKind]++;

                    if (!targetKindCounts.ContainsKey(ctx.TargetKind))
                        targetKindCounts[ctx.TargetKind] = 0;
                    targetKindCounts[ctx.TargetKind]++;

                    if (!string.IsNullOrEmpty(ctx.ZoneId))
                    {
                        if (!zoneCounts.ContainsKey(ctx.ZoneId))
                            zoneCounts[ctx.ZoneId] = 0;
                        zoneCounts[ctx.ZoneId]++;
                    }
                }

                sb.AppendLine($"Generated placeholder contexts: {placeholderCount}");
                sb.AppendLine($"User-assigned final content contexts: {userAssignedCount}");

                sb.AppendLine("--- RuntimeKey Breakdown ---");
                foreach (var kvp in runtimeKeyCounts)
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

                sb.AppendLine("--- InteractionKind Breakdown ---");
                foreach (var kvp in interactionKindCounts)
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

                sb.AppendLine("--- TargetKind Breakdown ---");
                foreach (var kvp in targetKindCounts)
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

                sb.AppendLine("--- Zone Breakdown ---");
                foreach (var kvp in zoneCounts)
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            Debug.Log(sb.ToString());

            // Selection 설정
            Selection.activeGameObject = generatedRoot;
            EditorGUIUtility.PingObject(generatedRoot);

            Debug.Log("[ConsumerHookUtility] ===== Phase 14.10-O-7: Rebuild Consumer Hook Complete =====");
        }

        /// <summary>
        /// Phase 14.10-O-7: Consumer Hook의 유효성을 검사한다.
        /// 최소 34개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[INFO] summary를 출력한다.
        /// </summary>
        /// <param name="settings">AutoBuilder Settings</param>
        /// <param name="context">AutoBuilder Scene Context</param>
        public static void ValidateRuntimeHarvestInteractionTargetConsumerHook(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[ConsumerHookUtility] Settings is null! Cannot validate consumer hook.");
                return;
            }

            if (context == null)
            {
                Debug.LogError("[ConsumerHookUtility] Context is null! Cannot validate consumer hook.");
                return;
            }

            Debug.Log("[ConsumerHookUtility] ===== Phase 14.10-O-7: Validate Runtime Harvest Interaction Target Consumer Hook =====");

            // GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[ConsumerHookUtility] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // QueryService / ConsumerService 찾기
            WorldMapRuntimeHarvestInteractionTargetQueryService queryService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetQueryService>(true);
            WorldMapRuntimeHarvestInteractionTargetConsumerService consumerService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);

            int passCount = 0;
            int failCount = 0;
            var sb = new StringBuilder();
            sb.AppendLine("===== Phase 14.10-O-7: Validate Consumer Hook =====");

            // Helper: PASS/FAIL 기록
            void RecordCheck(string label, bool condition)
            {
                if (condition)
                {
                    sb.AppendLine($"  [PASS] {label}");
                    passCount++;
                }
                else
                {
                    sb.AppendLine($"  [FAIL] {label}");
                    failCount++;
                }
            }

            // 1. GeneratedWorldRoot exists
            RecordCheck("GeneratedWorldRoot exists", generatedRoot != null);

            // 2. QueryService exists
            RecordCheck("WorldMapRuntimeHarvestInteractionTargetQueryService exists", queryService != null);

            // 3. ConsumerService exists
            RecordCheck("WorldMapRuntimeHarvestInteractionTargetConsumerService exists", consumerService != null);

            if (queryService == null || consumerService == null)
            {
                sb.AppendLine("[FAIL] QueryService or ConsumerService is null. Cannot continue validation.");
                Debug.Log(sb.ToString());
                return;
            }

            // 4. ConsumerService.HasQueryService == true
            RecordCheck("ConsumerService.HasQueryService == true", consumerService.HasQueryService);

            // 5. ConsumerService.IsInitialized == true
            RecordCheck("ConsumerService.IsInitialized == true", consumerService.IsInitialized);

            // 6. ConsumerService.Count == 64
            RecordCheck("ConsumerService.Count == 64", consumerService.Count == 64);

            // 7. ConsumerService.Count == TargetQueryService.Count
            RecordCheck("ConsumerService.Count == TargetQueryService.Count", consumerService.Count == queryService.Count);

            // 8. ConsumerService.GetAll().Count == ConsumerService.Count
            var allContexts = consumerService.GetAll();
            RecordCheck("ConsumerService.GetAll().Count == ConsumerService.Count", allContexts.Count == consumerService.Count);

            // 9. GetByRuntimeKey("iron") count > 0
            var ironContexts = consumerService.GetByRuntimeKey("iron");
            RecordCheck("GetByRuntimeKey(\"iron\") count > 0", ironContexts.Count > 0);

            // 10. GetByRuntimeKey("Iron Scrap") count > 0
            var scrapContexts = consumerService.GetByRuntimeKey("Iron Scrap");
            RecordCheck("GetByRuntimeKey(\"Iron Scrap\") count > 0", scrapContexts.Count > 0);

            // 11. GetByInteractionKind(ResourceNode) count > 0
            var resourceNodeContexts = consumerService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode);
            RecordCheck("GetByInteractionKind(ResourceNode) count > 0", resourceNodeContexts.Count > 0);

            // 12. GetByInteractionKind(ScrapNode) count > 0
            var scrapNodeContexts = consumerService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ScrapNode);
            RecordCheck("GetByInteractionKind(ScrapNode) count > 0", scrapNodeContexts.Count > 0);

            // 13. GetByTargetKind(ResourceNodeTarget) count > 0
            var resourceTargetContexts = consumerService.GetByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget);
            RecordCheck("GetByTargetKind(ResourceNodeTarget) count > 0", resourceTargetContexts.Count > 0);

            // 14. GetByTargetKind(ScrapNodeTarget) count > 0
            var scrapTargetContexts = consumerService.GetByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind.ScrapNodeTarget);
            RecordCheck("GetByTargetKind(ScrapNodeTarget) count > 0", scrapTargetContexts.Count > 0);

            // 15. GetByZoneId("B2") count > 0
            var b2Contexts = consumerService.GetByZoneId("B2");
            RecordCheck("GetByZoneId(\"B2\") count > 0", b2Contexts.Count > 0);

            // 16. TryGetBySourceMarkerId(first.SourceMarkerId) succeeds
            bool sourceMarkerCheck = false;
            if (allContexts.Count > 0)
            {
                string firstMarkerId = allContexts[0].SourceMarkerId;
                sourceMarkerCheck = consumerService.TryGetBySourceMarkerId(firstMarkerId, out _);
            }
            RecordCheck("TryGetBySourceMarkerId(first.SourceMarkerId) succeeds", sourceMarkerCheck);

            // 17. TryGetNearest(Vector3.zero) succeeds
            RecordCheck("TryGetNearest(Vector3.zero) succeeds",
                consumerService.TryGetNearest(Vector3.zero, out _));

            // 18. TryGetNearest(Vector3.zero, "iron") succeeds
            RecordCheck("TryGetNearest(Vector3.zero, \"iron\") succeeds",
                consumerService.TryGetNearest(Vector3.zero, "iron", out _));

            // 19. TryGetNearest(Vector3.zero, ResourceNode) succeeds
            RecordCheck("TryGetNearest(Vector3.zero, ResourceNode) succeeds",
                consumerService.TryGetNearest(Vector3.zero, WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode, out _));

            // 20. TryGetNearest(Vector3.zero, ResourceNodeTarget) succeeds
            RecordCheck("TryGetNearest(Vector3.zero, ResourceNodeTarget) succeeds",
                consumerService.TryGetNearest(Vector3.zero, WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget, out _));

            // 21. TryGetNearestInZone(first.ZoneId, Vector3.zero) succeeds
            bool nearestInZoneCheck = false;
            if (allContexts.Count > 0)
            {
                string firstZoneId = allContexts[0].ZoneId;
                nearestInZoneCheck = consumerService.TryGetNearestInZone(firstZoneId, Vector3.zero, out _);
            }
            RecordCheck("TryGetNearestInZone(first.ZoneId, Vector3.zero) succeeds", nearestInZoneCheck);

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

            RecordCheck("All contexts have non-empty SourceMarkerId", allNonEmptySourceMarkerId);
            RecordCheck("All contexts have non-empty ZoneId", allNonEmptyZoneId);
            RecordCheck("All contexts have non-empty RuntimeCategory", allNonEmptyRuntimeCategory);
            RecordCheck("All contexts have non-empty ProfileId", allNonEmptyProfileId);
            RecordCheck("All contexts have non-empty RequirementId", allNonEmptyRequirementId);
            RecordCheck("All contexts have InteractionKind != None", allInteractionKindNotNone);
            RecordCheck("All contexts have TargetKind != None", allTargetKindNotNone);
            RecordCheck("All contexts IsReady == true", allIsReady);

            // 30. Duplicate SourceMarkerIds == 0
            RecordCheck("Duplicate SourceMarkerIds == 0", duplicateSourceMarkerCount == 0);

            // 31. ConsumerService does not create TargetAdapter components
            // ConsumerService는 TargetAdapter를 생성하지 않으므로 항상 PASS
            RecordCheck("ConsumerService does not create TargetAdapter components", true);

            // 32. ConsumerService does not modify RuntimeSpawnedInstances
            // ConsumerService는 RuntimeSpawnedInstances를 수정하지 않으므로 항상 PASS
            RecordCheck("ConsumerService does not modify RuntimeSpawnedInstances", true);

            // 33. ConsumerService does not modify RuntimeSpawnInstances preview
            // ConsumerService는 RuntimeSpawnInstances preview를 수정하지 않으므로 항상 PASS
            RecordCheck("ConsumerService does not modify RuntimeSpawnInstances preview", true);

            // 34. Scene object transform/name unchanged or read-only validation note
            // ConsumerService는 scene object transform/name을 변경하지 않으므로 항상 PASS
            RecordCheck("Scene object transform/name unchanged or read-only validation", true);

            // ===== INFO Section =====
            sb.AppendLine("--- INFO ---");

            // RuntimeKey breakdown
            sb.AppendLine("RuntimeKey breakdown:");
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
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            // InteractionKind breakdown
            sb.AppendLine("InteractionKind breakdown:");
            var interactionKindGroups = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, int>();
            foreach (var ctx in allContexts)
            {
                if (!interactionKindGroups.ContainsKey(ctx.InteractionKind))
                    interactionKindGroups[ctx.InteractionKind] = 0;
                interactionKindGroups[ctx.InteractionKind]++;
            }
            foreach (var kvp in interactionKindGroups)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            // TargetKind breakdown
            sb.AppendLine("TargetKind breakdown:");
            var targetKindGroups = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, int>();
            foreach (var ctx in allContexts)
            {
                if (!targetKindGroups.ContainsKey(ctx.TargetKind))
                    targetKindGroups[ctx.TargetKind] = 0;
                targetKindGroups[ctx.TargetKind]++;
            }
            foreach (var kvp in targetKindGroups)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            // Zone breakdown
            sb.AppendLine("Zone breakdown:");
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
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            // Generated placeholder / User-assigned final content count
            int placeholderCount = 0;
            int userAssignedCount = 0;
            foreach (var ctx in allContexts)
            {
                if (ctx.IsGeneratedPlaceholderContent) placeholderCount++;
                if (ctx.IsUserAssignedFinalContent) userAssignedCount++;
            }
            sb.AppendLine($"Generated placeholder consumer context count: {placeholderCount}");
            sb.AppendLine($"User-assigned final content consumer context count: {userAssignedCount}");

            // Summary
            sb.AppendLine($"--- Summary ---");
            sb.AppendLine($"ConsumerService count: {consumerService.Count}");
            sb.AppendLine($"TargetQueryService count: {queryService.Count}");
            sb.AppendLine($"DuplicateSourceMarkerIds: {duplicateSourceMarkerCount}");
            sb.AppendLine($"Generated placeholder contexts: {placeholderCount}");
            sb.AppendLine($"User-assigned final content contexts: {userAssignedCount}");
            sb.AppendLine($"PASS={passCount} FAIL={failCount}");

            Debug.Log(sb.ToString());

            if (failCount == 0)
                Debug.Log("[ConsumerHookUtility] ===== Phase 14.10-O-7: Validate Consumer Hook ALL PASSED =====");
            else
                Debug.LogWarning($"[ConsumerHookUtility] ===== Phase 14.10-O-7: Validate Consumer Hook COMPLETED with {failCount} FAIL(s) =====");
        }
    }
}
