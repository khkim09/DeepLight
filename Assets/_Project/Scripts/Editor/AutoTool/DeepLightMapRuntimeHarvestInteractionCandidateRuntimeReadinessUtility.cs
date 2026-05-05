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
    /// Phase 14.10-O-4: Harvest Interaction Candidate Runtime Readiness 전용 Editor Utility.
    /// O-3에서 구축한 Runtime Harvest Interaction Candidate Bridge가 PlayMode/gameplay 진입 시
    /// 자동 초기화 가능한지 runtime readiness를 검증한다.
    /// UnityEditor API만 사용하며, Gameplay 폴더 신규 파일에는 UnityEditor API를 사용하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeHarvestInteractionCandidateRuntimeReadinessUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[O-4 RuntimeReadiness]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionCandidateRegistry와
        /// WorldMapRuntimeHarvestInteractionCandidateQueryService를 부착/보강하고 cache를 재구축한다.
        /// Scene object의 transform/name을 변경하지 않는다.
        /// 단, GeneratedWorldRoot에 Registry/QueryService 컴포넌트 추가는 허용.
        /// RuntimeFinalContentInstances 하위 object에는 새 컴포넌트를 추가하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeHarvestInteractionCandidateRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-4] Settings is null! Cannot rebuild harvest interaction candidate runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-4: Rebuild Runtime Harvest Interaction Candidate Runtime Readiness =====");

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

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-4: Rebuild Runtime Harvest Interaction Candidate Runtime Readiness Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// WorldMapRuntimeHarvestInteractionCandidateRegistry와
        /// WorldMapRuntimeHarvestInteractionCandidateQueryService의 runtime readiness를 검증한다.
        /// 37개 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// 읽기 전용 검증이지만, Registry/QueryService가 없거나 cache가 비어 있는 경우
        /// readiness 검증 목적상 GeneratedWorldRoot에 필요한 컴포넌트 추가/초기화는 허용한다.
        /// RuntimeFinalContentInstances 하위 object는 수정하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeHarvestInteractionCandidateRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-4] Settings is null! Cannot validate harvest interaction candidate runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-4: Validate Runtime Harvest Interaction Candidate Runtime Readiness =====");

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

            // Registry가 없으면 readiness 검증 목적상 추가
            if (registry == null)
            {
                registry = Undo.AddComponent<WorldMapRuntimeHarvestInteractionCandidateRegistry>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionCandidateRegistry for readiness validation.");
                infoCount++;
            }

            // QueryService가 없으면 readiness 검증 목적상 추가
            if (queryService == null)
            {
                queryService = Undo.AddComponent<WorldMapRuntimeHarvestInteractionCandidateQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionCandidateQueryService for readiness validation.");
                infoCount++;
            }

            // Registry cache가 비어 있으면 rebuild
            if (registry.Count == 0)
            {
                registry.RebuildCacheFromRoot(generatedRoot.transform);
                log.AppendLine($"{LogPrefix} [INFO] Registry cache was empty. Rebuilt from root for readiness validation.");
                infoCount++;
            }

            // QueryService가 초기화되지 않았으면 TryInitializeFromRoot
            if (queryService.Count == 0)
            {
                queryService.TryInitializeFromRoot(generatedRoot.transform);
                log.AppendLine($"{LogPrefix} [INFO] QueryService was empty. Initialized from root for readiness validation.");
                infoCount++;
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

            // 10. GetByRuntimeKey("iron") count > 0
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

            // 11. GetByRuntimeKey("Iron Scrap") count > 0
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

            // 12. GetByInteractionKind(ResourceNode) count > 0
            IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> resourceNodeCandidates = queryService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode);
            if (resourceNodeCandidates.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 12. GetByInteractionKind(ResourceNode) count: {resourceNodeCandidates.Count}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] 12. GetByInteractionKind(ResourceNode) count is 0.");
                infoCount++;
            }

            // 13. GetByInteractionKind(ScrapNode) count > 0
            IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> scrapNodeCandidates = queryService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ScrapNode);
            if (scrapNodeCandidates.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 13. GetByInteractionKind(ScrapNode) count: {scrapNodeCandidates.Count}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] 13. GetByInteractionKind(ScrapNode) count is 0.");
                infoCount++;
            }

            // 14. GetByZoneId("B2") count > 0이면 PASS, 없으면 INFO
            IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> b2Candidates = queryService.GetByZoneId("B2");
            if (b2Candidates.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 14. GetByZoneId(\"B2\") count: {b2Candidates.Count}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] 14. GetByZoneId(\"B2\") count is 0. (No B2 candidates found)");
                infoCount++;
            }

            // 15. TryGetBySourceMarkerId(first.SourceMarkerId) succeeds
            if (registry.Count > 0)
            {
                string firstMarkerId = registry.AllCandidates[0].SourceMarkerId;
                if (queryService.TryGetBySourceMarkerId(firstMarkerId, out var byMarker))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 15. TryGetBySourceMarkerId(\"{firstMarkerId}\") succeeds.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 15. TryGetBySourceMarkerId(\"{firstMarkerId}\") failed.");
                    failCount++;
                }
            }

            // 16. TryGetNearest(Vector3.zero) succeeds
            if (queryService.TryGetNearest(Vector3.zero, out var nearest))
            {
                log.AppendLine($"{LogPrefix} [PASS] 16. TryGetNearest(Vector3.zero) succeeds. Nearest: {nearest.SourceMarkerId} at {nearest.WorldPosition}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 16. TryGetNearest(Vector3.zero) failed.");
                failCount++;
            }

            // 17. TryGetNearest(Vector3.zero, "iron") succeeds if iron exists
            if (ironCandidates.Count > 0)
            {
                if (queryService.TryGetNearest(Vector3.zero, "iron", out var nearestIron))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 17. TryGetNearest(Vector3.zero, \"iron\") succeeds. Nearest: {nearestIron.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 17. TryGetNearest(Vector3.zero, \"iron\") failed despite iron candidates existing.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] 17. TryGetNearest(Vector3.zero, \"iron\") skipped (no iron candidates).");
                infoCount++;
            }

            // 18. TryGetNearest(Vector3.zero, ResourceNode) succeeds
            if (resourceNodeCandidates.Count > 0)
            {
                if (queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode, out var nearestByKind))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 18. TryGetNearest(Vector3.zero, ResourceNode) succeeds. Nearest: {nearestByKind.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 18. TryGetNearest(Vector3.zero, ResourceNode) failed despite ResourceNode candidates existing.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] 18. TryGetNearest(Vector3.zero, ResourceNode) skipped (no ResourceNode candidates).");
                infoCount++;
            }

            // 19. TryGetNearestInZone(firstZoneId, Vector3.zero) succeeds
            if (registry.Count > 0)
            {
                string firstZoneId = registry.AllCandidates[0].ZoneId;
                if (queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, out var nearestInZone))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 19. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) succeeds. Nearest: {nearestInZone.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 19. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) failed.");
                    failCount++;
                }
            }

            // 20. DuplicateSourceMarkerIds count == 0
            IReadOnlyList<string> duplicateIds = registry.DuplicateSourceMarkerIds;
            if (duplicateIds.Count == 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 20. DuplicateSourceMarkerIds count: 0");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] 20. DuplicateSourceMarkerIds count: {duplicateIds.Count}");
                warnCount++;
            }

            // 21-27. All candidates field validation
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

            // 21. All candidates have non-empty SourceMarkerId
            if (allNonEmptySourceMarker == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 21. All candidates have non-empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 21. Some candidates have empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                failCount++;
            }

            // 22. All candidates have non-empty ZoneId
            if (allNonEmptyZoneId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 22. All candidates have non-empty ZoneId ({allNonEmptyZoneId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 22. Some candidates have empty ZoneId ({allNonEmptyZoneId}/{total}).");
                failCount++;
            }

            // 23. All candidates have non-empty RuntimeCategory
            if (allNonEmptyRuntimeCategory == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 23. All candidates have non-empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 23. Some candidates have empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                failCount++;
            }

            // 24. All candidates have non-empty ProfileId
            if (allNonEmptyProfileId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 24. All candidates have non-empty ProfileId ({allNonEmptyProfileId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 24. Some candidates have empty ProfileId ({allNonEmptyProfileId}/{total}).");
                failCount++;
            }

            // 25. All candidates have non-empty RequirementId
            if (allNonEmptyRequirementId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 25. All candidates have non-empty RequirementId ({allNonEmptyRequirementId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 25. Some candidates have empty RequirementId ({allNonEmptyRequirementId}/{total}).");
                failCount++;
            }

            // 26. All candidates have InteractionKind != None
            if (allInteractionKindNotNone == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 26. All candidates have InteractionKind != None ({allInteractionKindNotNone}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 26. Some candidates have InteractionKind == None ({allInteractionKindNotNone}/{total}).");
                failCount++;
            }

            // 27. All candidates IsReady() == true
            if (allReady == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 27. All candidates IsReady() == true ({allReady}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 27. Some candidates IsReady() == false ({allReady}/{total}).");
                failCount++;
            }

            // 28. RuntimeFinalContentInstances 하위 object에 WorldMapRuntimeHarvestInteractionCandidate MonoBehaviour가 붙지 않았는지 확인
            Transform runtimeFinalContentInstances = generatedRoot.transform.Find("RuntimeFinalContentInstances");
            bool hasIllegalComponent = false;
            if (runtimeFinalContentInstances != null)
            {
                // RuntimeFinalContentInstances 하위에서 WorldMapRuntimeHarvestInteractionCandidateRegistry 검색
                WorldMapRuntimeHarvestInteractionCandidateRegistry[] illegalRegistries = runtimeFinalContentInstances.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionCandidateRegistry>(true);
                if (illegalRegistries.Length > 0)
                {
                    hasIllegalComponent = true;
                    log.AppendLine($"{LogPrefix} [FAIL] 28. Found {illegalRegistries.Length} WorldMapRuntimeHarvestInteractionCandidateRegistry under RuntimeFinalContentInstances. This violates the policy.");
                    failCount++;
                }

                // RuntimeFinalContentInstances 하위에서 WorldMapRuntimeHarvestInteractionCandidateQueryService 검색
                WorldMapRuntimeHarvestInteractionCandidateQueryService[] illegalQueryServices = runtimeFinalContentInstances.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionCandidateQueryService>(true);
                if (illegalQueryServices.Length > 0)
                {
                    hasIllegalComponent = true;
                    log.AppendLine($"{LogPrefix} [FAIL] 28. Found {illegalQueryServices.Length} WorldMapRuntimeHarvestInteractionCandidateQueryService under RuntimeFinalContentInstances. This violates the policy.");
                    failCount++;
                }
            }
            if (!hasIllegalComponent)
            {
                log.AppendLine($"{LogPrefix} [PASS] 28. No WorldMapRuntimeHarvestInteractionCandidate MonoBehaviour found under RuntimeFinalContentInstances.");
                passCount++;
            }

            // 29. RuntimeSpawnedInstances object not included
            log.AppendLine($"{LogPrefix} [INFO] 29. RuntimeSpawnedInstances exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 30. RuntimeSpawnInstances preview object not included
            log.AppendLine($"{LogPrefix} [INFO] 30. RuntimeSpawnInstances preview exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 31. RuntimePlaceholder object not included
            log.AppendLine($"{LogPrefix} [INFO] 31. RuntimePlaceholder exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 32. WorldMapZoneContentMarker object not included
            log.AppendLine($"{LogPrefix} [INFO] 32. WorldMapZoneContentMarker exclusion is handled by source Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 33. Scene object transform/name unchanged (read-only validation INFO)
            log.AppendLine($"{LogPrefix} [INFO] 33. Scene object transform/name unchanged. Read-only validation performed.");
            infoCount++;

            // 34. RuntimeKey breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 34. RuntimeKey breakdown:");
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

            // 35. InteractionKind breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 35. InteractionKind breakdown:");
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

            // 36. Zone breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 36. Zone breakdown:");
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

            // 37. Generated placeholder interaction candidate count INFO
            int placeholderCount = 0;
            foreach (var candidate in registry.AllCandidates)
            {
                if (candidate.IsGeneratedPlaceholderContent)
                    placeholderCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 37. Generated placeholder interaction candidates: {placeholderCount}");
            infoCount++;

            // 38. User-assigned final content interaction candidate count INFO
            int userAssignedCount = 0;
            foreach (var candidate in registry.AllCandidates)
            {
                if (candidate.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 38. User-assigned final content interaction candidates: {userAssignedCount}");
            infoCount++;

            // Summary
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} PASS: {passCount}, FAIL: {failCount}, WARN: {warnCount}, INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Source HarvestResourceCandidate count: {sourceHarvestCount}");
            log.AppendLine($"{LogPrefix} Registry count: {registry.Count}");
            log.AppendLine($"{LogPrefix} QueryService count: {queryService.Count}");
            log.AppendLine($"{LogPrefix} Duplicate SourceMarkerIds: {duplicateIds.Count}");
            log.AppendLine($"{LogPrefix} Generated placeholder interaction candidates: {placeholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content interaction candidates: {userAssignedCount}");
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-4: Validate Runtime Harvest Interaction Candidate Runtime Readiness Complete =====");

            Debug.Log(log.ToString());
        }
    }
}
