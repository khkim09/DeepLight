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
    /// Phase 14.10-O-3: HarvestResource Interaction Candidate Bridge 전용 Editor Utility.
    /// O-1/O-2에서 구축한 Runtime Harvest Resource Candidate Bridge를 기존 Harvest 시스템이
    /// 사용할 수 있는 "Harvest Interaction Candidate Adapter" 계층으로 변환한다.
    /// 실제 채집 실행/보상 지급/UI prompt/실패 패널티 적용을 직접 연결하지 않는다.
    /// RuntimeFinalContentInstances 하위 object에는 새 MonoBehaviour를 붙이지 않는다.
    /// UnityEditor API만 사용하며, Gameplay 폴더 신규 파일에는 UnityEditor API를 사용하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeHarvestInteractionCandidateBridgeUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[O-3 InteractionBridge]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionCandidateRegistry와
        /// WorldMapRuntimeHarvestInteractionCandidateQueryService를 추가/갱신하고
        /// cache를 재구축한다. Scene object의 transform/name을 변경하지 않는다.
        /// 단, GeneratedWorldRoot에 Registry/QueryService 컴포넌트 추가는 허용.
        /// RuntimeFinalContentInstances 하위 object에는 새 컴포넌트를 추가하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeHarvestInteractionCandidateBridge(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-3] Settings is null! Cannot rebuild harvest interaction candidate bridge.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-3: Rebuild Runtime Harvest Interaction Candidate Bridge =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. WorldMapRuntimeHarvestResourceCandidateQueryService 존재 확인/추가/초기화
            WorldMapRuntimeHarvestResourceCandidateQueryService sourceQueryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>();
            if (sourceQueryService == null)
            {
                sourceQueryService = Undo.AddComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestResourceCandidateQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestResourceCandidateQueryService already exists on '{generatedRoot.name}'.");
            }

            // 3. GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionCandidateRegistry가 없으면 추가
            WorldMapRuntimeHarvestInteractionCandidateRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionCandidateRegistry>();
            if (registry == null)
            {
                registry = Undo.AddComponent<WorldMapRuntimeHarvestInteractionCandidateRegistry>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionCandidateRegistry to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionCandidateRegistry already exists on '{generatedRoot.name}'.");
            }

            // 4. GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionCandidateQueryService가 없으면 추가
            WorldMapRuntimeHarvestInteractionCandidateQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionCandidateQueryService>();
            if (queryService == null)
            {
                queryService = Undo.AddComponent<WorldMapRuntimeHarvestInteractionCandidateQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionCandidateQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionCandidateQueryService already exists on '{generatedRoot.name}'.");
            }

            // 5. Registry.RebuildCacheFromRoot(generatedRoot.transform) 호출
            registry.RebuildCacheFromRoot(generatedRoot.transform);
            log.AppendLine($"{LogPrefix} [INFO] Registry.RebuildCacheFromRoot complete. Registry count: {registry.Count}");

            // 6. QueryService.TryInitializeFromRoot(generatedRoot.transform) 호출
            bool initialized = queryService.TryInitializeFromRoot(generatedRoot.transform);
            if (initialized)
            {
                log.AppendLine($"{LogPrefix} [PASS] QueryService.TryInitializeFromRoot complete. QueryService count: {queryService.Count}");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] QueryService.TryInitializeFromRoot failed.");
            }

            // 7. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            // 8. Breakdown 출력
            log.AppendLine($"{LogPrefix} [INFO] RuntimeKey breakdown:");
            var runtimeKeyBreakdown = new Dictionary<string, int>();
            IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> allCandidates = registry.AllCandidates;
            foreach (WorldMapRuntimeHarvestInteractionCandidate candidate in allCandidates)
            {
                string key = candidate.RuntimeKey;
                if (!string.IsNullOrEmpty(key))
                {
                    if (!runtimeKeyBreakdown.ContainsKey(key))
                        runtimeKeyBreakdown[key] = 0;
                    runtimeKeyBreakdown[key]++;
                }
            }
            foreach (var kvp in runtimeKeyBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] InteractionKind breakdown:");
            var kindBreakdown = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, int>();
            foreach (WorldMapRuntimeHarvestInteractionCandidate candidate in allCandidates)
            {
                WorldMapRuntimeHarvestInteractionCandidateKind kind = candidate.InteractionKind;
                if (!kindBreakdown.ContainsKey(kind))
                    kindBreakdown[kind] = 0;
                kindBreakdown[kind]++;
            }
            foreach (var kvp in kindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] Zone breakdown:");
            var zoneBreakdown = new Dictionary<string, int>();
            foreach (WorldMapRuntimeHarvestInteractionCandidate candidate in allCandidates)
            {
                string zoneId = candidate.ZoneId;
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

            // Generated placeholder / User-assigned final content count
            int placeholderCount = 0;
            int userAssignedCount = 0;
            foreach (WorldMapRuntimeHarvestInteractionCandidate candidate in allCandidates)
            {
                if (candidate.IsGeneratedPlaceholderContent)
                    placeholderCount++;
                if (candidate.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] Generated placeholder interaction candidates: {placeholderCount}");
            log.AppendLine($"{LogPrefix} [INFO] User-assigned final content interaction candidates: {userAssignedCount}");

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

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-3: Rebuild Runtime Harvest Interaction Candidate Bridge Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// WorldMapRuntimeHarvestInteractionCandidateRegistry와
        /// WorldMapRuntimeHarvestInteractionCandidateQueryService의 유효성을 검사한다.
        /// 36개 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Scene object transform/name 변경 없음. Read-only 검증.
        /// RuntimeFinalContentInstances 하위 object에 새 MonoBehaviour가 붙지 않았는지 확인한다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeHarvestInteractionCandidateBridge(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-3] Settings is null! Cannot validate harvest interaction candidate bridge.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-3: Validate Runtime Harvest Interaction Candidate Bridge =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // 1. GeneratedWorldRoot exists
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 1. GeneratedWorldRoot exists: '{generatedRoot.name}'");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 1. GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                failCount++;
                Debug.LogError(log.ToString());
                return;
            }

            // 2. WorldMapRuntimeHarvestResourceCandidateQueryService exists
            WorldMapRuntimeHarvestResourceCandidateQueryService sourceQueryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>();
            if (sourceQueryService != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 2. WorldMapRuntimeHarvestResourceCandidateQueryService exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 2. WorldMapRuntimeHarvestResourceCandidateQueryService not found on GeneratedWorldRoot.");
                failCount++;
            }

            // 3. WorldMapRuntimeHarvestInteractionCandidateRegistry exists
            WorldMapRuntimeHarvestInteractionCandidateRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionCandidateRegistry>();
            if (registry != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 3. WorldMapRuntimeHarvestInteractionCandidateRegistry exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 3. WorldMapRuntimeHarvestInteractionCandidateRegistry not found on GeneratedWorldRoot.");
                failCount++;
            }

            // 4. WorldMapRuntimeHarvestInteractionCandidateQueryService exists
            WorldMapRuntimeHarvestInteractionCandidateQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionCandidateQueryService>();
            if (queryService != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 4. WorldMapRuntimeHarvestInteractionCandidateQueryService exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 4. WorldMapRuntimeHarvestInteractionCandidateQueryService not found on GeneratedWorldRoot.");
                failCount++;
            }

            // Registry가 없으면 여기서 중단
            if (registry == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Registry is null. Cannot continue validation.");
                failCount++;
                Debug.LogError(log.ToString());
                return;
            }

            // Registry cache가 비어 있으면 rebuild
            if (registry.Count == 0)
            {
                registry.RebuildCacheFromRoot(generatedRoot.transform);
                log.AppendLine($"{LogPrefix} [INFO] Registry cache was empty. Rebuilt from root.");
                infoCount++;
            }

            // QueryService가 없거나 count가 0이면 TryInitializeFromRoot
            if (queryService == null || queryService.Count == 0)
            {
                if (queryService != null)
                {
                    queryService.TryInitializeFromRoot(generatedRoot.transform);
                    log.AppendLine($"{LogPrefix} [INFO] QueryService was empty. Initialized from root.");
                    infoCount++;
                }
            }

            // 5. Source HarvestResourceCandidate count > 0
            int sourceHarvestCount = 0;
            if (sourceQueryService != null)
            {
                sourceQueryService.TryInitializeFromRoot(generatedRoot.transform);
                sourceHarvestCount = sourceQueryService.Count;
            }
            if (sourceHarvestCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 5. Source HarvestResourceCandidate count: {sourceHarvestCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 5. Source HarvestResourceCandidate count is 0.");
                failCount++;
            }

            // 6. Registry.Count == source HarvestResourceCandidate count
            if (registry.Count == sourceHarvestCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] 6. Registry.Count ({registry.Count}) == source HarvestResourceCandidate count ({sourceHarvestCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 6. Registry.Count ({registry.Count}) != source HarvestResourceCandidate count ({sourceHarvestCount}).");
                failCount++;
            }

            // 7. QueryService.Count == Registry.Count
            if (queryService != null)
            {
                if (queryService.Count == registry.Count)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 7. QueryService.Count ({queryService.Count}) == Registry.Count ({registry.Count}).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 7. QueryService.Count ({queryService.Count}) != Registry.Count ({registry.Count}).");
                    failCount++;
                }

                // 8. QueryService.HasRegistry == true
                if (queryService.HasRegistry)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 8. QueryService.HasRegistry == true.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 8. QueryService.HasRegistry == false.");
                    failCount++;
                }

                // 9. QueryService.GetAll count == QueryService.Count
                IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> allFromQuery = queryService.GetAll();
                if (allFromQuery.Count == queryService.Count)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 9. QueryService.GetAll count ({allFromQuery.Count}) == QueryService.Count ({queryService.Count}).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 9. QueryService.GetAll count ({allFromQuery.Count}) != QueryService.Count ({queryService.Count}).");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 7. QueryService is null. Skipping checks 7-9.");
                failCount++;
                log.AppendLine($"{LogPrefix} [FAIL] 8. QueryService is null.");
                failCount++;
                log.AppendLine($"{LogPrefix} [FAIL] 9. QueryService is null.");
                failCount++;
            }

            // 10. GetByRuntimeKey("iron") count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> ironCandidates = queryService.GetByRuntimeKey("iron");
                if (ironCandidates.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 10. GetByRuntimeKey(\"iron\") count: {ironCandidates.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 10. GetByRuntimeKey(\"iron\") count is 0. (No iron candidates found)");
                    infoCount++;
                }
            }

            // 11. GetByRuntimeKey("Iron Scrap") count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> scrapCandidates = queryService.GetByRuntimeKey("Iron Scrap");
                if (scrapCandidates.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 11. GetByRuntimeKey(\"Iron Scrap\") count: {scrapCandidates.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 11. GetByRuntimeKey(\"Iron Scrap\") count is 0. (No Iron Scrap candidates found)");
                    infoCount++;
                }
            }

            // 12. GetByInteractionKind(ResourceNode or ScrapNode) count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> resourceCandidates = queryService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode);
                IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> scrapCandidates = queryService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ScrapNode);
                int combinedCount = resourceCandidates.Count + scrapCandidates.Count;
                if (combinedCount > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 12. GetByInteractionKind(ResourceNode|ScrapNode) count: {combinedCount} (ResourceNode: {resourceCandidates.Count}, ScrapNode: {scrapCandidates.Count})");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 12. GetByInteractionKind(ResourceNode|ScrapNode) count is 0.");
                    infoCount++;
                }
            }

            // 13. GetByZoneId("B2") count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> b2Candidates = queryService.GetByZoneId("B2");
                if (b2Candidates.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 13. GetByZoneId(\"B2\") count: {b2Candidates.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 13. GetByZoneId(\"B2\") count is 0. (No B2 candidates found)");
                    infoCount++;
                }
            }

            // 14. TryGetBySourceMarkerId(first.SourceMarkerId) succeeds
            if (registry.Count > 0)
            {
                string firstMarkerId = registry.AllCandidates[0].SourceMarkerId;
                if (registry.TryGetBySourceMarkerId(firstMarkerId, out var byMarker))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 14. TryGetBySourceMarkerId(\"{firstMarkerId}\") succeeds.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 14. TryGetBySourceMarkerId(\"{firstMarkerId}\") failed.");
                    failCount++;
                }
            }

            // 15. TryGetNearest(Vector3.zero) succeeds
            if (queryService != null)
            {
                if (queryService.TryGetNearest(Vector3.zero, out var nearest))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 15. TryGetNearest(Vector3.zero) succeeds. Nearest: {nearest.SourceMarkerId} at {nearest.WorldPosition}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 15. TryGetNearest(Vector3.zero) failed.");
                    failCount++;
                }
            }

            // 16. TryGetNearest(Vector3.zero, "iron") succeeds if iron exists
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> ironCandidates = queryService.GetByRuntimeKey("iron");
                if (ironCandidates.Count > 0)
                {
                    if (queryService.TryGetNearest(Vector3.zero, "iron", out var nearestIron))
                    {
                        log.AppendLine($"{LogPrefix} [PASS] 16. TryGetNearest(Vector3.zero, \"iron\") succeeds. Nearest: {nearestIron.SourceMarkerId}");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine($"{LogPrefix} [FAIL] 16. TryGetNearest(Vector3.zero, \"iron\") failed despite iron candidates existing.");
                        failCount++;
                    }
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 16. TryGetNearest(Vector3.zero, \"iron\") skipped (no iron candidates).");
                    infoCount++;
                }
            }

            // 17. TryGetNearest(Vector3.zero, interactionKind) succeeds
            if (queryService != null && registry.Count > 0)
            {
                WorldMapRuntimeHarvestInteractionCandidateKind firstKind = registry.AllCandidates[0].InteractionKind;
                if (queryService.TryGetNearest(Vector3.zero, firstKind, out var nearestByKind))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 17. TryGetNearest(Vector3.zero, {firstKind}) succeeds. Nearest: {nearestByKind.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 17. TryGetNearest(Vector3.zero, {firstKind}) failed.");
                    failCount++;
                }
            }

            // 18. TryGetNearestInZone(firstZoneId, Vector3.zero) succeeds
            if (queryService != null && registry.Count > 0)
            {
                string firstZoneId = registry.AllCandidates[0].ZoneId;
                if (queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, out var nearestInZone))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 18. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) succeeds. Nearest: {nearestInZone.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 18. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) failed.");
                    failCount++;
                }
            }

            // 19. DuplicateSourceMarkerIds count == 0
            IReadOnlyList<string> duplicateIds = registry.DuplicateSourceMarkerIds;
            if (duplicateIds.Count == 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 19. DuplicateSourceMarkerIds count: 0");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] 19. DuplicateSourceMarkerIds count: {duplicateIds.Count}");
                warnCount++;
            }

            // 20-26. All candidates field validation
            int allNonEmptySourceMarker = 0;
            int allNonEmptyZoneId = 0;
            int allNonEmptyRuntimeCategory = 0;
            int allNonEmptyProfileId = 0;
            int allNonEmptyRequirementId = 0;
            int allInteractionKindNotNone = 0;
            int allReady = 0;

            foreach (var candidate in registry.AllCandidates)
            {
                if (!string.IsNullOrEmpty(candidate.SourceMarkerId)) allNonEmptySourceMarker++;
                if (!string.IsNullOrEmpty(candidate.ZoneId)) allNonEmptyZoneId++;
                if (!string.IsNullOrEmpty(candidate.RuntimeCategory)) allNonEmptyRuntimeCategory++;
                if (!string.IsNullOrEmpty(candidate.ProfileId)) allNonEmptyProfileId++;
                if (!string.IsNullOrEmpty(candidate.RequirementId)) allNonEmptyRequirementId++;
                if (candidate.InteractionKind != WorldMapRuntimeHarvestInteractionCandidateKind.None) allInteractionKindNotNone++;
                if (candidate.IsReady) allReady++;
            }

            int total = registry.Count;

            // 20. All candidates have non-empty SourceMarkerId
            if (allNonEmptySourceMarker == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 20. All candidates have non-empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 20. Some candidates have empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                failCount++;
            }

            // 21. All candidates have non-empty ZoneId
            if (allNonEmptyZoneId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 21. All candidates have non-empty ZoneId ({allNonEmptyZoneId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 21. Some candidates have empty ZoneId ({allNonEmptyZoneId}/{total}).");
                failCount++;
            }

            // 22. All candidates have non-empty RuntimeCategory
            if (allNonEmptyRuntimeCategory == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 22. All candidates have non-empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 22. Some candidates have empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                failCount++;
            }

            // 23. All candidates have non-empty ProfileId
            if (allNonEmptyProfileId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 23. All candidates have non-empty ProfileId ({allNonEmptyProfileId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 23. Some candidates have empty ProfileId ({allNonEmptyProfileId}/{total}).");
                failCount++;
            }

            // 24. All candidates have non-empty RequirementId
            if (allNonEmptyRequirementId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 24. All candidates have non-empty RequirementId ({allNonEmptyRequirementId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 24. Some candidates have empty RequirementId ({allNonEmptyRequirementId}/{total}).");
                failCount++;
            }

            // 25. All candidates have InteractionKind != None
            if (allInteractionKindNotNone == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 25. All candidates have InteractionKind != None ({allInteractionKindNotNone}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 25. Some candidates have InteractionKind == None ({allInteractionKindNotNone}/{total}).");
                failCount++;
            }

            // 26. All candidates IsReady() == true
            if (allReady == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 26. All candidates IsReady() == true ({allReady}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 26. Some candidates IsReady() == false ({allReady}/{total}).");
                failCount++;
            }

            // 27. RuntimeFinalContentInstances 하위 object에 WorldMapRuntimeHarvestInteractionCandidate 관련 MonoBehaviour가 붙지 않았는지 확인
            Transform runtimeFinalContentInstances = generatedRoot.transform.Find("RuntimeFinalContentInstances");
            bool hasIllegalComponent = false;
            if (runtimeFinalContentInstances != null)
            {
                // RuntimeFinalContentInstances 하위에서 WorldMapRuntimeHarvestInteractionCandidateRegistry 검색
                WorldMapRuntimeHarvestInteractionCandidateRegistry[] illegalRegistries = runtimeFinalContentInstances.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionCandidateRegistry>(true);
                if (illegalRegistries.Length > 0)
                {
                    hasIllegalComponent = true;
                    log.AppendLine($"{LogPrefix} [FAIL] 27. Found {illegalRegistries.Length} WorldMapRuntimeHarvestInteractionCandidateRegistry under RuntimeFinalContentInstances. This violates the policy.");
                    failCount++;
                }

                // RuntimeFinalContentInstances 하위에서 WorldMapRuntimeHarvestInteractionCandidateQueryService 검색
                WorldMapRuntimeHarvestInteractionCandidateQueryService[] illegalQueryServices = runtimeFinalContentInstances.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionCandidateQueryService>(true);
                if (illegalQueryServices.Length > 0)
                {
                    hasIllegalComponent = true;
                    log.AppendLine($"{LogPrefix} [FAIL] 27. Found {illegalQueryServices.Length} WorldMapRuntimeHarvestInteractionCandidateQueryService under RuntimeFinalContentInstances. This violates the policy.");
                    failCount++;
                }
            }
            if (!hasIllegalComponent)
            {
                log.AppendLine($"{LogPrefix} [PASS] 27. No WorldMapRuntimeHarvestInteractionCandidate MonoBehaviour found under RuntimeFinalContentInstances.");
                passCount++;
            }

            // 28. RuntimeSpawnedInstances object not included
            log.AppendLine($"{LogPrefix} [INFO] 28. RuntimeSpawnedInstances exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 29. RuntimeSpawnInstances preview object not included
            log.AppendLine($"{LogPrefix} [INFO] 29. RuntimeSpawnInstances preview exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 30. RuntimePlaceholder object not included
            log.AppendLine($"{LogPrefix} [INFO] 30. RuntimePlaceholder exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 31. WorldMapZoneContentMarker object not included
            log.AppendLine($"{LogPrefix} [INFO] 31. WorldMapZoneContentMarker exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 32. RuntimeKey breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 32. RuntimeKey breakdown:");
            var runtimeKeyBreakdown = new Dictionary<string, int>();
            foreach (var candidate in registry.AllCandidates)
            {
                string key = candidate.RuntimeKey;
                if (!string.IsNullOrEmpty(key))
                {
                    if (!runtimeKeyBreakdown.ContainsKey(key))
                        runtimeKeyBreakdown[key] = 0;
                    runtimeKeyBreakdown[key]++;
                }
            }
            foreach (var kvp in runtimeKeyBreakdown)
            {
                log.AppendLine($"{LogPrefix}       {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 33. InteractionKind breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 33. InteractionKind breakdown:");
            var kindBreakdown = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, int>();
            foreach (var candidate in registry.AllCandidates)
            {
                WorldMapRuntimeHarvestInteractionCandidateKind kind = candidate.InteractionKind;
                if (!kindBreakdown.ContainsKey(kind))
                    kindBreakdown[kind] = 0;
                kindBreakdown[kind]++;
            }
            foreach (var kvp in kindBreakdown)
            {
                log.AppendLine($"{LogPrefix}       {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 34. Zone breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 34. Zone breakdown:");
            var zoneBreakdown = new Dictionary<string, int>();
            foreach (var candidate in registry.AllCandidates)
            {
                string zoneId = candidate.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!zoneBreakdown.ContainsKey(zoneId))
                        zoneBreakdown[zoneId] = 0;
                    zoneBreakdown[zoneId]++;
                }
            }
            foreach (var kvp in zoneBreakdown)
            {
                log.AppendLine($"{LogPrefix}       {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 35. Generated placeholder candidate count INFO
            int placeholderCount = 0;
            foreach (var candidate in registry.AllCandidates)
            {
                if (candidate.IsGeneratedPlaceholderContent)
                    placeholderCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 35. Generated placeholder interaction candidates: {placeholderCount}");
            infoCount++;

            // 36. User-assigned final content candidate count INFO
            int userAssignedCount = 0;
            foreach (var candidate in registry.AllCandidates)
            {
                if (candidate.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 36. User-assigned final content interaction candidates: {userAssignedCount}");
            infoCount++;

            // Summary
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} PASS: {passCount}, FAIL: {failCount}, WARN: {warnCount}, INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Source HarvestResourceCandidate count: {sourceHarvestCount}");
            log.AppendLine($"{LogPrefix} Registry count: {registry.Count}");
            log.AppendLine($"{LogPrefix} QueryService count: {(queryService != null ? queryService.Count : 0)}");
            log.AppendLine($"{LogPrefix} Duplicate SourceMarkerIds: {duplicateIds.Count}");
            log.AppendLine($"{LogPrefix} Generated placeholder interaction candidates: {placeholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content interaction candidates: {userAssignedCount}");
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-3: Validate Runtime Harvest Interaction Candidate Bridge Complete =====");

            Debug.Log(log.ToString());
        }
    }
}
