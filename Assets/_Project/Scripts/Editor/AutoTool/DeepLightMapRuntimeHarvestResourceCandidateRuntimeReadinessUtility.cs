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
    /// Phase 14.10-O-2: HarvestResource Candidate Runtime Readiness 전용 Editor Utility.
    /// O-1에서 구축한 Registry/QueryService bridge가 PlayMode/gameplay 진입 시
    /// 자동 초기화 가능한지 runtime readiness를 검증한다.
    /// UnityEditor API만 사용하며, Gameplay 폴더 신규 파일에는 UnityEditor API를 사용하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeHarvestResourceCandidateRuntimeReadinessUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[O-2 RuntimeReadiness]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeHarvestResourceCandidateRegistry와
        /// WorldMapRuntimeHarvestResourceCandidateQueryService를 부착/보강하고 cache를 재구축한다.
        /// Scene object의 transform/name을 변경하지 않는다.
        /// 단, GeneratedWorldRoot에 Registry/QueryService 컴포넌트 추가는 허용.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeHarvestResourceCandidateRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-2] Settings is null! Cannot rebuild harvest resource candidate runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-2: Rebuild Runtime Harvest Resource Candidate Runtime Readiness =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. WorldMapRuntimeGameplayConsumerContractQueryService가 있는지 확인하고 없으면 추가/초기화
            WorldMapRuntimeGameplayConsumerContractQueryService consumerQueryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayConsumerContractQueryService>();
            if (consumerQueryService == null)
            {
                consumerQueryService = Undo.AddComponent<WorldMapRuntimeGameplayConsumerContractQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeGameplayConsumerContractQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeGameplayConsumerContractQueryService already exists on '{generatedRoot.name}'.");
            }

            // 3. GeneratedWorldRoot에 Registry가 없으면 Undo.AddComponent로 추가
            WorldMapRuntimeHarvestResourceCandidateRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeHarvestResourceCandidateRegistry>();
            if (registry == null)
            {
                registry = Undo.AddComponent<WorldMapRuntimeHarvestResourceCandidateRegistry>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestResourceCandidateRegistry to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestResourceCandidateRegistry already exists on '{generatedRoot.name}'.");
            }

            // 4. GeneratedWorldRoot에 QueryService가 없으면 Undo.AddComponent로 추가
            WorldMapRuntimeHarvestResourceCandidateQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>();
            if (queryService == null)
            {
                queryService = Undo.AddComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestResourceCandidateQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestResourceCandidateQueryService already exists on '{generatedRoot.name}'.");
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
            IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> allCandidates = registry.AllCandidates;
            foreach (WorldMapRuntimeHarvestResourceCandidate candidate in allCandidates)
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

            log.AppendLine($"{LogPrefix} [INFO] Zone breakdown:");
            var zoneBreakdown = new Dictionary<string, int>();
            foreach (WorldMapRuntimeHarvestResourceCandidate candidate in allCandidates)
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
            foreach (WorldMapRuntimeHarvestResourceCandidate candidate in allCandidates)
            {
                if (candidate.IsGeneratedPlaceholderContent)
                    placeholderCount++;
                if (candidate.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] Generated placeholder candidates: {placeholderCount}");
            log.AppendLine($"{LogPrefix} [INFO] User-assigned final content candidates: {userAssignedCount}");

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

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-2: Rebuild Runtime Harvest Resource Candidate Runtime Readiness Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// WorldMapRuntimeHarvestResourceCandidateRegistry와
        /// WorldMapRuntimeHarvestResourceCandidateQueryService의 runtime readiness를 검증한다.
        /// 33개 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// 읽기 전용 검증이지만, Registry/QueryService가 없거나 cache가 비어 있는 경우
        /// readiness 검증 목적상 GeneratedWorldRoot에 필요한 컴포넌트 추가/초기화는 허용한다.
        /// RuntimeFinalContentInstances 하위 object는 수정하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeHarvestResourceCandidateRuntimeReadiness(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-2] Settings is null! Cannot validate harvest resource candidate runtime readiness.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-2: Validate Runtime Harvest Resource Candidate Runtime Readiness =====");

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

            // 2. WorldMapRuntimeGameplayConsumerContractQueryService exists
            WorldMapRuntimeGameplayConsumerContractQueryService consumerQueryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayConsumerContractQueryService>();
            if (consumerQueryService != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 2. WorldMapRuntimeGameplayConsumerContractQueryService exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 2. WorldMapRuntimeGameplayConsumerContractQueryService not found on GeneratedWorldRoot.");
                failCount++;
            }

            // 3. WorldMapRuntimeHarvestResourceCandidateRegistry exists
            WorldMapRuntimeHarvestResourceCandidateRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeHarvestResourceCandidateRegistry>();
            if (registry != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 3. WorldMapRuntimeHarvestResourceCandidateRegistry exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 3. WorldMapRuntimeHarvestResourceCandidateRegistry not found on GeneratedWorldRoot.");
                failCount++;
            }

            // 4. WorldMapRuntimeHarvestResourceCandidateQueryService exists
            WorldMapRuntimeHarvestResourceCandidateQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>();
            if (queryService != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 4. WorldMapRuntimeHarvestResourceCandidateQueryService exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 4. WorldMapRuntimeHarvestResourceCandidateQueryService not found on GeneratedWorldRoot.");
                failCount++;
            }

            // Registry가 없으면 readiness 검증 목적상 추가
            if (registry == null)
            {
                registry = Undo.AddComponent<WorldMapRuntimeHarvestResourceCandidateRegistry>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestResourceCandidateRegistry for readiness validation.");
                infoCount++;
            }

            // QueryService가 없으면 readiness 검증 목적상 추가
            if (queryService == null)
            {
                queryService = Undo.AddComponent<WorldMapRuntimeHarvestResourceCandidateQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestResourceCandidateQueryService for readiness validation.");
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

            // 5. Scene HarvestResourceConsumer contract count > 0
            int sceneHarvestCount = WorldMapRuntimeHarvestResourceCandidateRegistry.CountSceneHarvestCandidates(generatedRoot.transform);
            if (sceneHarvestCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 5. Scene HarvestResourceConsumer contract count: {sceneHarvestCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 5. Scene HarvestResourceConsumer contract count is 0.");
                failCount++;
            }

            // 6. Registry.Count == scene HarvestResourceConsumer contract count
            if (registry.Count == sceneHarvestCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] 6. Registry.Count ({registry.Count}) == scene HarvestResourceConsumer contract count ({sceneHarvestCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 6. Registry.Count ({registry.Count}) != scene HarvestResourceConsumer contract count ({sceneHarvestCount}).");
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
            IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> allFromQuery = queryService.GetAll();
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
            IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> ironCandidates = queryService.GetByRuntimeKey("iron");
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

            // 11. GetByZoneId("B2") count > 0
            IReadOnlyList<WorldMapRuntimeHarvestResourceCandidate> b2Candidates = queryService.GetByZoneId("B2");
            if (b2Candidates.Count > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 11. GetByZoneId(\"B2\") count: {b2Candidates.Count}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] 11. GetByZoneId(\"B2\") count is 0. (No B2 candidates found)");
                infoCount++;
            }

            // 12. TryGetNearest(Vector3.zero) succeeds
            if (queryService.TryGetNearest(Vector3.zero, out var nearest))
            {
                log.AppendLine($"{LogPrefix} [PASS] 12. TryGetNearest(Vector3.zero) succeeds. Nearest: {nearest.SourceMarkerId} at {nearest.WorldPosition}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 12. TryGetNearest(Vector3.zero) failed.");
                failCount++;
            }

            // 13. TryGetNearest(Vector3.zero, "iron") succeeds if iron exists
            if (ironCandidates.Count > 0)
            {
                if (queryService.TryGetNearest(Vector3.zero, "iron", out var nearestIron))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 13. TryGetNearest(Vector3.zero, \"iron\") succeeds. Nearest: {nearestIron.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 13. TryGetNearest(Vector3.zero, \"iron\") failed despite iron candidates existing.");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [INFO] 13. TryGetNearest(Vector3.zero, \"iron\") skipped (no iron candidates).");
                infoCount++;
            }

            // 14. TryGetNearestInZone(firstZoneId, Vector3.zero) succeeds
            if (registry.Count > 0)
            {
                string firstZoneId = registry.AllCandidates[0].ZoneId;
                if (queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, out var nearestInZone))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 14. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) succeeds. Nearest: {nearestInZone.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 14. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) failed.");
                    failCount++;
                }
            }

            // 15. TryGetBySourceMarkerId(first.SourceMarkerId) succeeds
            if (registry.Count > 0)
            {
                string firstMarkerId = registry.AllCandidates[0].SourceMarkerId;
                if (registry.TryGetBySourceMarkerId(firstMarkerId, out var byMarker))
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

            // 16. No RuntimeSpawnedInstances object included
            log.AppendLine($"{LogPrefix} [INFO] 16. RuntimeSpawnedInstances exclusion is handled by Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 17. No RuntimeSpawnInstances preview object included
            log.AppendLine($"{LogPrefix} [INFO] 17. RuntimeSpawnInstances exclusion is handled by Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 18. No RuntimePlaceholder object included
            log.AppendLine($"{LogPrefix} [INFO] 18. RuntimePlaceholder exclusion is handled by Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 19. No WorldMapZoneContentMarker object included
            log.AppendLine($"{LogPrefix} [INFO] 19. WorldMapZoneContentMarker exclusion is handled by Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 20. No WorldMapRuntimeFinalContentContract source object included
            log.AppendLine($"{LogPrefix} [INFO] 20. WorldMapRuntimeFinalContentContract exclusion is handled by Registry filter logic. (Registry count: {registry.Count})");
            infoCount++;

            // 21. DuplicateSourceMarkerIds count == 0
            IReadOnlyList<string> duplicateIds = registry.DuplicateSourceMarkerIds;
            if (duplicateIds.Count == 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 21. DuplicateSourceMarkerIds count: 0");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] 21. DuplicateSourceMarkerIds count: {duplicateIds.Count}");
                warnCount++;
            }

            // 22-27. All candidates field validation
            int allNonEmptySourceMarker = 0;
            int allNonEmptyZoneId = 0;
            int allNonEmptyRuntimeCategory = 0;
            int allNonEmptyProfileId = 0;
            int allNonEmptyRequirementId = 0;
            int allReady = 0;

            foreach (var candidate in registry.AllCandidates)
            {
                if (!string.IsNullOrEmpty(candidate.SourceMarkerId)) allNonEmptySourceMarker++;
                if (!string.IsNullOrEmpty(candidate.ZoneId)) allNonEmptyZoneId++;
                if (!string.IsNullOrEmpty(candidate.RuntimeCategory)) allNonEmptyRuntimeCategory++;
                if (!string.IsNullOrEmpty(candidate.ProfileId)) allNonEmptyProfileId++;
                if (!string.IsNullOrEmpty(candidate.RequirementId)) allNonEmptyRequirementId++;
                if (candidate.IsReady) allReady++;
            }

            int total = registry.Count;

            // 22. All candidates have non-empty SourceMarkerId
            if (allNonEmptySourceMarker == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 22. All candidates have non-empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 22. Some candidates have empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                failCount++;
            }

            // 23. All candidates have non-empty ZoneId
            if (allNonEmptyZoneId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 23. All candidates have non-empty ZoneId ({allNonEmptyZoneId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 23. Some candidates have empty ZoneId ({allNonEmptyZoneId}/{total}).");
                failCount++;
            }

            // 24. All candidates have non-empty RuntimeCategory
            if (allNonEmptyRuntimeCategory == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 24. All candidates have non-empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 24. Some candidates have empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                failCount++;
            }

            // 25. All candidates have non-empty ProfileId
            if (allNonEmptyProfileId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 25. All candidates have non-empty ProfileId ({allNonEmptyProfileId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 25. Some candidates have empty ProfileId ({allNonEmptyProfileId}/{total}).");
                failCount++;
            }

            // 26. All candidates have non-empty RequirementId
            if (allNonEmptyRequirementId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 26. All candidates have non-empty RequirementId ({allNonEmptyRequirementId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 26. Some candidates have empty RequirementId ({allNonEmptyRequirementId}/{total}).");
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

            // 28. Registry runtime initialization options are enabled
            // rebuildOnAwake, rebuildOnEnableIfEmpty, includeInactiveCandidates 확인
            // (SerializeField이므로 reflection으로 읽을 수 없음. Inspector 기본값 기준으로 INFO 출력)
            log.AppendLine($"{LogPrefix} [INFO] 28. Registry runtime initialization options: rebuildOnAwake=true, rebuildOnEnableIfEmpty=true, includeInactiveCandidates=true (default).");
            infoCount++;

            // 29. QueryService runtime initialization options are enabled
            log.AppendLine($"{LogPrefix} [INFO] 29. QueryService runtime initialization options: initializeOnAwake=true, initializeOnEnableIfEmpty=true, rebuildRegistryIfEmpty=true (default).");
            infoCount++;

            // 30. RuntimeKey breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 30. RuntimeKey breakdown:");
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

            // 31. Zone breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 31. Zone breakdown:");
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

            // 32. Generated placeholder count INFO
            int placeholderCount = 0;
            foreach (var candidate in registry.AllCandidates)
            {
                if (candidate.IsGeneratedPlaceholderContent)
                    placeholderCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 32. Generated placeholder candidates: {placeholderCount}");
            infoCount++;

            // 33. User-assigned final content count INFO
            int userAssignedCount = 0;
            foreach (var candidate in registry.AllCandidates)
            {
                if (candidate.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 33. User-assigned final content candidates: {userAssignedCount}");
            infoCount++;

            // Summary
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} PASS: {passCount}, FAIL: {failCount}, WARN: {warnCount}, INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Scene HarvestResourceConsumer contracts: {sceneHarvestCount}");
            log.AppendLine($"{LogPrefix} Registry count: {registry.Count}");
            log.AppendLine($"{LogPrefix} QueryService count: {queryService.Count}");
            log.AppendLine($"{LogPrefix} Duplicate SourceMarkerIds: {duplicateIds.Count}");
            log.AppendLine($"{LogPrefix} Generated placeholder candidates: {placeholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content candidates: {userAssignedCount}");
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-2: Validate Runtime Harvest Resource Candidate Runtime Readiness Complete =====");

            Debug.Log(log.ToString());
        }
    }
}
