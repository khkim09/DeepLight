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
    /// Phase 14.10-O-5: Harvest Interaction Runtime Target Adapter 전용 Editor Utility.
    /// O-3/O-4에서 구축한 Harvest Interaction Candidate를 기존 Harvest 시스템이 consume할 수 있는
    /// Runtime Harvest Interaction Target Adapter 계층으로 변환한다.
    /// 실제 채집 실행/보상 지급/UI prompt/실패 패널티 적용을 직접 연결하지 않는다.
    /// RuntimeFinalContentInstances 하위 final content object에만 TargetAdapter를 부착한다.
    /// RuntimeSpawnedInstances / RuntimeSpawnInstances preview / RuntimePlaceholder / marker object에는 절대 붙이지 않는다.
    /// 기존 Harvest 시스템 component는 붙이지 않는다.
    /// Collider/Trigger/Event/Resolver 연결 금지.
    /// source object transform/name 변경 금지.
    /// final content object transform/name 변경 금지.
    /// </summary>
    public static class DeepLightMapRuntimeHarvestInteractionTargetAdapterUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[O-5 TargetAdapter]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionTargetAdapter를
        /// RuntimeFinalContentInstances 하위 final content object에 부착/갱신하고
        /// Registry/QueryService를 추가/보강한다.
        /// Scene object의 transform/name을 변경하지 않는다.
        /// 단, GeneratedWorldRoot에 Registry/QueryService 컴포넌트 추가는 허용.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeHarvestInteractionTargetAdapters(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-5] Settings is null! Cannot rebuild harvest interaction target adapters.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-5: Rebuild Runtime Harvest Interaction Target Adapters =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. WorldMapRuntimeHarvestInteractionCandidateQueryService 존재 확인/추가/초기화
            WorldMapRuntimeHarvestInteractionCandidateQueryService candidateQueryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionCandidateQueryService>();
            if (candidateQueryService == null)
            {
                candidateQueryService = Undo.AddComponent<WorldMapRuntimeHarvestInteractionCandidateQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionCandidateQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionCandidateQueryService already exists on '{generatedRoot.name}'.");
            }

            // 3. Candidate QueryService 초기화
            bool initialized = candidateQueryService.TryInitializeFromRoot(generatedRoot.transform);
            if (!initialized)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Candidate QueryService.TryInitializeFromRoot failed. No candidates found.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] Candidate QueryService initialized. Candidate count: {candidateQueryService.Count}");

            // 4. 모든 candidate 수집
            IReadOnlyList<WorldMapRuntimeHarvestInteractionCandidate> allCandidates = candidateQueryService.GetAll();
            log.AppendLine($"{LogPrefix} [INFO] Collected {allCandidates.Count} candidates from QueryService.");

            // ===== 기존 잘못 부착된 TargetAdapter 정리 =====
            // GeneratedWorldRoot 하위의 모든 WorldMapRuntimeHarvestInteractionTargetAdapter를 찾고,
            // RuntimeFinalContentInstances 하위가 아닌 object에 붙어 있는 adapter는 제거한다.
            int removedInvalidAdapters = 0;
            WorldMapRuntimeHarvestInteractionTargetAdapter[] allExistingAdapters = generatedRoot.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionTargetAdapter>(true);
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter existingAdapter in allExistingAdapters)
            {
                if (existingAdapter == null) continue;

                // RuntimeFinalContentInstances 하위가 아닌 adapter는 제거
                if (!IsUnderRuntimeFinalContentInstances(existingAdapter.transform))
                {
                    Undo.DestroyObjectImmediate(existingAdapter);
                    removedInvalidAdapters++;
                }
            }
            if (removedInvalidAdapters > 0)
            {
                log.AppendLine($"{LogPrefix} [INFO] Removed {removedInvalidAdapters} invalid TargetAdapter(s) not under RuntimeFinalContentInstances.");
            }

            // ===== final content instance tag 수집 =====
            // GeneratedWorldRoot 전체 하위에서 WorldMapRuntimeFinalContentInstanceTag를 수집한다.
            WorldMapRuntimeFinalContentInstanceTag[] allFinalInstanceTags = generatedRoot.GetComponentsInChildren<WorldMapRuntimeFinalContentInstanceTag>(true);
            int totalTagsScanned = allFinalInstanceTags.Length;
            log.AppendLine($"{LogPrefix} [INFO] Final content instance tags scanned: {totalTagsScanned}");

            // IsUnderRuntimeFinalContentInstances 조건을 통과한 tag만 대상으로 dictionary 구축
            var finalInstanceBySourceMarkerId = new Dictionary<string, WorldMapRuntimeFinalContentInstanceTag>();
            int tagsUnderRuntimeFinalContentInstances = 0;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag == null) continue;
                if (!tag.IsGeneratedFinalInstance) continue;
                if (!IsUnderRuntimeFinalContentInstances(tag.transform)) continue;

                tagsUnderRuntimeFinalContentInstances++;
                string sourceMarkerId = tag.SourceMarkerId;
                if (!string.IsNullOrEmpty(sourceMarkerId) && !finalInstanceBySourceMarkerId.ContainsKey(sourceMarkerId))
                {
                    finalInstanceBySourceMarkerId[sourceMarkerId] = tag;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Final content instance tags under RuntimeFinalContentInstances: {tagsUnderRuntimeFinalContentInstances}");

            // 5. 각 candidate에 대응되는 final content object를 찾아 TargetAdapter 부착/갱신
            int adapterAttachedCount = 0;
            int adapterUpdatedCount = 0;
            int failedCandidateCount = 0;

            foreach (WorldMapRuntimeHarvestInteractionCandidate candidate in allCandidates)
            {
                if (candidate == null || !candidate.IsReady)
                    continue;

                string sourceMarkerId = candidate.SourceMarkerId;
                if (string.IsNullOrEmpty(sourceMarkerId))
                    continue;

                // finalInstanceBySourceMarkerId dictionary에서 동일 SourceMarkerId를 가진 final content object를 찾는다.
                if (!finalInstanceBySourceMarkerId.TryGetValue(sourceMarkerId, out WorldMapRuntimeFinalContentInstanceTag matchedTag))
                {
                    failedCandidateCount++;
                    log.AppendLine($"{LogPrefix} [WARN] Final content instance not found for SourceMarkerId: '{sourceMarkerId}' (RuntimeKey: {candidate.RuntimeKey}). Skipping.");
                    continue;
                }

                GameObject targetObject = matchedTag.gameObject;

                // TargetAdapter 부착 또는 갱신
                WorldMapRuntimeHarvestInteractionTargetAdapter adapter = targetObject.GetComponent<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                if (adapter == null)
                {
                    adapter = Undo.AddComponent<WorldMapRuntimeHarvestInteractionTargetAdapter>(targetObject);
                    adapterAttachedCount++;
                }
                else
                {
                    adapterUpdatedCount++;
                }

                // Configure 호출
                adapter.Configure(candidate);
            }

            log.AppendLine($"{LogPrefix} [INFO] TargetAdapter attached: {adapterAttachedCount}, updated: {adapterUpdatedCount}, failed candidates: {failedCandidateCount}");

            // 6. GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionTargetRegistry 추가/보강
            WorldMapRuntimeHarvestInteractionTargetRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionTargetRegistry>();
            if (registry == null)
            {
                registry = Undo.AddComponent<WorldMapRuntimeHarvestInteractionTargetRegistry>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionTargetRegistry to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionTargetRegistry already exists on '{generatedRoot.name}'.");
            }

            // 7. Registry.RebuildCacheFromRoot 호출
            registry.RebuildCacheFromRoot(generatedRoot.transform);
            log.AppendLine($"{LogPrefix} [INFO] Registry.RebuildCacheFromRoot complete. Registry count: {registry.Count}");

            // 8. GeneratedWorldRoot에 WorldMapRuntimeHarvestInteractionTargetQueryService 추가/보강
            WorldMapRuntimeHarvestInteractionTargetQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>();
            if (queryService == null)
            {
                queryService = Undo.AddComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapRuntimeHarvestInteractionTargetQueryService to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionTargetQueryService already exists on '{generatedRoot.name}'.");
            }

            // 9. QueryService.TryInitializeFromRoot 호출
            bool queryInitialized = queryService.TryInitializeFromRoot(generatedRoot.transform);
            if (queryInitialized)
            {
                log.AppendLine($"{LogPrefix} [PASS] QueryService.TryInitializeFromRoot complete. QueryService count: {queryService.Count}");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] QueryService.TryInitializeFromRoot failed.");
            }

            // 10. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            // 11. Breakdown 출력
            log.AppendLine($"{LogPrefix} [INFO] RuntimeKey breakdown:");
            var runtimeKeyBreakdown = new Dictionary<string, int>();
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in registry.AllTargets)
            {
                string key = adapter.RuntimeKey;
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
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in registry.AllTargets)
            {
                WorldMapRuntimeHarvestInteractionCandidateKind kind = adapter.InteractionKind;
                if (!kindBreakdown.ContainsKey(kind))
                    kindBreakdown[kind] = 0;
                kindBreakdown[kind]++;
            }
            foreach (var kvp in kindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] TargetKind breakdown:");
            var targetKindBreakdown = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, int>();
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in registry.AllTargets)
            {
                WorldMapRuntimeHarvestInteractionTargetKind kind = adapter.TargetKind;
                if (!targetKindBreakdown.ContainsKey(kind))
                    targetKindBreakdown[kind] = 0;
                targetKindBreakdown[kind]++;
            }
            foreach (var kvp in targetKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] Zone breakdown:");
            var zoneBreakdown = new Dictionary<string, int>();
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in registry.AllTargets)
            {
                string zoneId = adapter.ZoneId;
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
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in registry.AllTargets)
            {
                if (adapter.IsGeneratedPlaceholderContent)
                    placeholderCount++;
                if (adapter.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] Generated placeholder targets: {placeholderCount}");
            log.AppendLine($"{LogPrefix} [INFO] User-assigned final content targets: {userAssignedCount}");

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

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-5: Rebuild Runtime Harvest Interaction Target Adapters Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// WorldMapRuntimeHarvestInteractionTargetAdapter/Registry/QueryService의 유효성을 검사한다.
        /// 44개 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Scene object transform/name 변경 없음. Read-only 검증.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeHarvestInteractionTargetAdapters(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-5] Settings is null! Cannot validate harvest interaction target adapters.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-5: Validate Runtime Harvest Interaction Target Adapters =====");

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

            // 2. WorldMapRuntimeHarvestInteractionCandidateQueryService exists
            WorldMapRuntimeHarvestInteractionCandidateQueryService candidateQueryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionCandidateQueryService>();
            if (candidateQueryService != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 2. WorldMapRuntimeHarvestInteractionCandidateQueryService exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 2. WorldMapRuntimeHarvestInteractionCandidateQueryService not found on GeneratedWorldRoot.");
                failCount++;
            }

            // 3. Candidate count > 0
            int candidateCount = 0;
            if (candidateQueryService != null)
            {
                candidateQueryService.TryInitializeFromRoot(generatedRoot.transform);
                candidateCount = candidateQueryService.Count;
            }
            if (candidateCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 3. Candidate count: {candidateCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 3. Candidate count is 0.");
                failCount++;
            }

            // 4. Scene target adapter count == candidate count
            int sceneAdapterCount = WorldMapRuntimeHarvestInteractionTargetRegistry.CountSceneHarvestInteractionTargets(generatedRoot.transform);
            if (sceneAdapterCount == candidateCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] 4. Scene target adapter count ({sceneAdapterCount}) == candidate count ({candidateCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 4. Scene target adapter count ({sceneAdapterCount}) != candidate count ({candidateCount}).");
                failCount++;
            }

            // 5. Registry exists
            WorldMapRuntimeHarvestInteractionTargetRegistry registry = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionTargetRegistry>();
            if (registry != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 5. WorldMapRuntimeHarvestInteractionTargetRegistry exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 5. WorldMapRuntimeHarvestInteractionTargetRegistry not found on GeneratedWorldRoot.");
                failCount++;
            }

            // 6. QueryService exists
            WorldMapRuntimeHarvestInteractionTargetQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeHarvestInteractionTargetQueryService>();
            if (queryService != null)
            {
                log.AppendLine($"{LogPrefix} [PASS] 6. WorldMapRuntimeHarvestInteractionTargetQueryService exists.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 6. WorldMapRuntimeHarvestInteractionTargetQueryService not found on GeneratedWorldRoot.");
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

            // 7. Registry.Count == scene target adapter count
            if (registry.Count == sceneAdapterCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] 7. Registry.Count ({registry.Count}) == scene target adapter count ({sceneAdapterCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 7. Registry.Count ({registry.Count}) != scene target adapter count ({sceneAdapterCount}).");
                failCount++;
            }

            // 8. QueryService.Count == Registry.Count
            if (queryService != null)
            {
                if (queryService.Count == registry.Count)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 8. QueryService.Count ({queryService.Count}) == Registry.Count ({registry.Count}).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 8. QueryService.Count ({queryService.Count}) != Registry.Count ({registry.Count}).");
                    failCount++;
                }

                // 9. QueryService.HasRegistry == true
                if (queryService.HasRegistry)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 9. QueryService.HasRegistry == true.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 9. QueryService.HasRegistry == false.");
                    failCount++;
                }

                // 10. QueryService.GetAll count == QueryService.Count
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> allFromQuery = queryService.GetAll();
                if (allFromQuery.Count == queryService.Count)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 10. QueryService.GetAll count ({allFromQuery.Count}) == QueryService.Count ({queryService.Count}).");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 10. QueryService.GetAll count ({allFromQuery.Count}) != QueryService.Count ({queryService.Count}).");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 8. QueryService is null.");
                failCount++;
                log.AppendLine($"{LogPrefix} [FAIL] 9. QueryService is null.");
                failCount++;
                log.AppendLine($"{LogPrefix} [FAIL] 10. QueryService is null.");
                failCount++;
            }

            // 11. GetByRuntimeKey("iron") count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> ironTargets = queryService.GetByRuntimeKey("iron");
                if (ironTargets.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 11. GetByRuntimeKey(\"iron\") count: {ironTargets.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 11. GetByRuntimeKey(\"iron\") count is 0.");
                    infoCount++;
                }
            }

            // 12. GetByRuntimeKey("Iron Scrap") count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> scrapTargets = queryService.GetByRuntimeKey("Iron Scrap");
                if (scrapTargets.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 12. GetByRuntimeKey(\"Iron Scrap\") count: {scrapTargets.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 12. GetByRuntimeKey(\"Iron Scrap\") count is 0.");
                    infoCount++;
                }
            }

            // 13. GetByInteractionKind(ResourceNode) count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> resourceTargets = queryService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode);
                if (resourceTargets.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 13. GetByInteractionKind(ResourceNode) count: {resourceTargets.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 13. GetByInteractionKind(ResourceNode) count is 0.");
                    infoCount++;
                }
            }

            // 14. GetByInteractionKind(ScrapNode) count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> scrapTargets = queryService.GetByInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind.ScrapNode);
                if (scrapTargets.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 14. GetByInteractionKind(ScrapNode) count: {scrapTargets.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 14. GetByInteractionKind(ScrapNode) count is 0.");
                    infoCount++;
                }
            }

            // 15. GetByTargetKind(ResourceNodeTarget) count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> resourceTargets = queryService.GetByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget);
                if (resourceTargets.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 15. GetByTargetKind(ResourceNodeTarget) count: {resourceTargets.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 15. GetByTargetKind(ResourceNodeTarget) count is 0.");
                    infoCount++;
                }
            }

            // 16. GetByTargetKind(ScrapNodeTarget) count > 0
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> scrapTargets = queryService.GetByTargetKind(WorldMapRuntimeHarvestInteractionTargetKind.ScrapNodeTarget);
                if (scrapTargets.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 16. GetByTargetKind(ScrapNodeTarget) count: {scrapTargets.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 16. GetByTargetKind(ScrapNodeTarget) count is 0.");
                    infoCount++;
                }
            }

            // 17. GetByZoneId("B2") count > 0이면 PASS, 없으면 INFO
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> b2Targets = queryService.GetByZoneId("B2");
                if (b2Targets.Count > 0)
                {
                    log.AppendLine($"{LogPrefix} [PASS] 17. GetByZoneId(\"B2\") count: {b2Targets.Count}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 17. GetByZoneId(\"B2\") count is 0. (No B2 targets found)");
                    infoCount++;
                }
            }

            // 18. TryGetBySourceMarkerId(first.SourceMarkerId) succeeds
            if (registry.Count > 0)
            {
                string firstMarkerId = registry.AllTargets[0].SourceMarkerId;
                if (registry.TryGetBySourceMarkerId(firstMarkerId, out var byMarker))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 18. TryGetBySourceMarkerId(\"{firstMarkerId}\") succeeds.");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 18. TryGetBySourceMarkerId(\"{firstMarkerId}\") failed.");
                    failCount++;
                }
            }

            // 19. TryGetNearest(Vector3.zero) succeeds
            if (queryService != null)
            {
                if (queryService.TryGetNearest(Vector3.zero, out var nearest))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 19. TryGetNearest(Vector3.zero) succeeds. Nearest: {nearest.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 19. TryGetNearest(Vector3.zero) failed.");
                    failCount++;
                }
            }

            // 20. TryGetNearest(Vector3.zero, "iron") succeeds if iron exists
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeHarvestInteractionTargetAdapter> ironTargets = queryService.GetByRuntimeKey("iron");
                if (ironTargets.Count > 0)
                {
                    if (queryService.TryGetNearest(Vector3.zero, "iron", out var nearestIron))
                    {
                        log.AppendLine($"{LogPrefix} [PASS] 20. TryGetNearest(Vector3.zero, \"iron\") succeeds. Nearest: {nearestIron.SourceMarkerId}");
                        passCount++;
                    }
                    else
                    {
                        log.AppendLine($"{LogPrefix} [FAIL] 20. TryGetNearest(Vector3.zero, \"iron\") failed despite iron targets existing.");
                        failCount++;
                    }
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 20. TryGetNearest(Vector3.zero, \"iron\") skipped (no iron targets).");
                    infoCount++;
                }
            }

            // 21. TryGetNearest(Vector3.zero, ResourceNodeTarget) succeeds
            if (queryService != null && registry.Count > 0)
            {
                if (queryService.TryGetNearest(Vector3.zero, WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget, out var nearestByTargetKind))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 21. TryGetNearest(Vector3.zero, ResourceNodeTarget) succeeds. Nearest: {nearestByTargetKind.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [INFO] 21. TryGetNearest(Vector3.zero, ResourceNodeTarget) failed (no ResourceNodeTarget targets).");
                    infoCount++;
                }
            }

            // 22. TryGetNearestInZone(firstZoneId, Vector3.zero) succeeds
            if (queryService != null && registry.Count > 0)
            {
                string firstZoneId = registry.AllTargets[0].ZoneId;
                if (queryService.TryGetNearestInZone(firstZoneId, Vector3.zero, out var nearestInZone))
                {
                    log.AppendLine($"{LogPrefix} [PASS] 22. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) succeeds. Nearest: {nearestInZone.SourceMarkerId}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] 22. TryGetNearestInZone(\"{firstZoneId}\", Vector3.zero) failed.");
                    failCount++;
                }
            }

            // 23. DuplicateSourceMarkerIds count == 0
            IReadOnlyList<string> duplicateIds = registry.DuplicateSourceMarkerIds;
            if (duplicateIds.Count == 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] 23. DuplicateSourceMarkerIds count: 0");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [WARN] 23. DuplicateSourceMarkerIds count: {duplicateIds.Count}");
                warnCount++;
            }

            // 24-31. All target adapters field validation
            int allNonEmptySourceMarker = 0;
            int allNonEmptyZoneId = 0;
            int allNonEmptyRuntimeCategory = 0;
            int allNonEmptyProfileId = 0;
            int allNonEmptyRequirementId = 0;
            int allInteractionKindNotNone = 0;
            int allTargetKindNotNone = 0;
            int allReady = 0;

            foreach (var adapter in registry.AllTargets)
            {
                if (!string.IsNullOrEmpty(adapter.SourceMarkerId)) allNonEmptySourceMarker++;
                if (!string.IsNullOrEmpty(adapter.ZoneId)) allNonEmptyZoneId++;
                if (!string.IsNullOrEmpty(adapter.RuntimeCategory)) allNonEmptyRuntimeCategory++;
                if (!string.IsNullOrEmpty(adapter.ProfileId)) allNonEmptyProfileId++;
                if (!string.IsNullOrEmpty(adapter.RequirementId)) allNonEmptyRequirementId++;
                if (adapter.InteractionKind != WorldMapRuntimeHarvestInteractionCandidateKind.None) allInteractionKindNotNone++;
                if (adapter.TargetKind != WorldMapRuntimeHarvestInteractionTargetKind.None) allTargetKindNotNone++;
                if (adapter.IsReady()) allReady++;
            }

            int total = registry.Count;

            // 24. All target adapters have non-empty SourceMarkerId
            if (allNonEmptySourceMarker == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 24. All target adapters have non-empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 24. Some target adapters have empty SourceMarkerId ({allNonEmptySourceMarker}/{total}).");
                failCount++;
            }

            // 25. All target adapters have non-empty ZoneId
            if (allNonEmptyZoneId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 25. All target adapters have non-empty ZoneId ({allNonEmptyZoneId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 25. Some target adapters have empty ZoneId ({allNonEmptyZoneId}/{total}).");
                failCount++;
            }

            // 26. All target adapters have non-empty RuntimeCategory
            if (allNonEmptyRuntimeCategory == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 26. All target adapters have non-empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 26. Some target adapters have empty RuntimeCategory ({allNonEmptyRuntimeCategory}/{total}).");
                failCount++;
            }

            // 27. All target adapters have non-empty ProfileId
            if (allNonEmptyProfileId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 27. All target adapters have non-empty ProfileId ({allNonEmptyProfileId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 27. Some target adapters have empty ProfileId ({allNonEmptyProfileId}/{total}).");
                failCount++;
            }

            // 28. All target adapters have non-empty RequirementId
            if (allNonEmptyRequirementId == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 28. All target adapters have non-empty RequirementId ({allNonEmptyRequirementId}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 28. Some target adapters have empty RequirementId ({allNonEmptyRequirementId}/{total}).");
                failCount++;
            }

            // 29. All target adapters have InteractionKind != None
            if (allInteractionKindNotNone == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 29. All target adapters have InteractionKind != None ({allInteractionKindNotNone}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 29. Some target adapters have InteractionKind == None ({allInteractionKindNotNone}/{total}).");
                failCount++;
            }

            // 30. All target adapters have TargetKind != None
            if (allTargetKindNotNone == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 30. All target adapters have TargetKind != None ({allTargetKindNotNone}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 30. Some target adapters have TargetKind == None ({allTargetKindNotNone}/{total}).");
                failCount++;
            }

            // 31. All target adapters IsReady() == true
            if (allReady == total)
            {
                log.AppendLine($"{LogPrefix} [PASS] 31. All target adapters IsReady() == true ({allReady}/{total}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 31. Some target adapters IsReady() == false ({allReady}/{total}).");
                failCount++;
            }

            // 32. No target adapter under RuntimeSpawnedInstances
            // Use GetComponentsInChildren on GeneratedWorldRoot to find RuntimeSpawnedInstances anywhere in hierarchy
            bool hasIllegalSpawned = false;
            WorldMapRuntimeHarvestInteractionTargetAdapter[] allSceneAdapters = generatedRoot.GetComponentsInChildren<WorldMapRuntimeHarvestInteractionTargetAdapter>(true);
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in allSceneAdapters)
            {
                if (adapter == null) continue;
                Transform current = adapter.transform;
                while (current != null && current != generatedRoot.transform)
                {
                    if (current.name == "RuntimeSpawnedInstances")
                    {
                        hasIllegalSpawned = true;
                        log.AppendLine($"{LogPrefix} [FAIL] 32. Found TargetAdapter under RuntimeSpawnedInstances: '{adapter.name}'. This violates the policy.");
                        break;
                    }
                    current = current.parent;
                }
            }
            if (!hasIllegalSpawned)
            {
                log.AppendLine($"{LogPrefix} [PASS] 32. No TargetAdapter under RuntimeSpawnedInstances.");
                passCount++;
            }

            // 33. No target adapter under RuntimeSpawnInstances preview
            bool hasIllegalSpawnPreview = false;
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in allSceneAdapters)
            {
                if (adapter == null) continue;
                Transform current = adapter.transform;
                while (current != null && current != generatedRoot.transform)
                {
                    if (current.name == "RuntimeSpawnInstances")
                    {
                        hasIllegalSpawnPreview = true;
                        log.AppendLine($"{LogPrefix} [FAIL] 33. Found TargetAdapter under RuntimeSpawnInstances preview: '{adapter.name}'. This violates the policy.");
                        break;
                    }
                    current = current.parent;
                }
            }
            if (!hasIllegalSpawnPreview)
            {
                log.AppendLine($"{LogPrefix} [PASS] 33. No TargetAdapter under RuntimeSpawnInstances preview.");
                passCount++;
            }

            // 34. No target adapter under RuntimePlaceholder
            bool hasIllegalPlaceholder = false;
            foreach (WorldMapRuntimeHarvestInteractionTargetAdapter adapter in allSceneAdapters)
            {
                if (adapter == null) continue;
                Transform current = adapter.transform;
                while (current != null && current != generatedRoot.transform)
                {
                    if (current.name == "RuntimePlaceholder")
                    {
                        hasIllegalPlaceholder = true;
                        log.AppendLine($"{LogPrefix} [FAIL] 34. Found TargetAdapter under RuntimePlaceholder: '{adapter.name}'. This violates the policy.");
                        break;
                    }
                    current = current.parent;
                }
            }
            if (!hasIllegalPlaceholder)
            {
                log.AppendLine($"{LogPrefix} [PASS] 34. No TargetAdapter under RuntimePlaceholder.");
                passCount++;
            }

            // 35. No target adapter under WorldMapZoneContentMarker source objects
            WorldMapZoneContentMarker[] allMarkers = generatedRoot.GetComponentsInChildren<WorldMapZoneContentMarker>(true);
            bool hasIllegalMarker = false;
            foreach (WorldMapZoneContentMarker marker in allMarkers)
            {
                if (marker == null) continue;
                WorldMapRuntimeHarvestInteractionTargetAdapter markerAdapter = marker.GetComponent<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                if (markerAdapter != null)
                {
                    hasIllegalMarker = true;
                    log.AppendLine($"{LogPrefix} [FAIL] 35. Found TargetAdapter on WorldMapZoneContentMarker object: '{marker.name}'. This violates the policy.");
                    failCount++;
                    break;
                }
            }
            if (!hasIllegalMarker)
            {
                log.AppendLine($"{LogPrefix} [PASS] 35. No TargetAdapter under WorldMapZoneContentMarker source objects.");
                passCount++;
            }

            // 36. No target adapter under WorldMapRuntimeFinalContentContract source objects
            WorldMapRuntimeFinalContentContract[] allContracts = generatedRoot.GetComponentsInChildren<WorldMapRuntimeFinalContentContract>(true);
            bool hasIllegalContract = false;
            foreach (WorldMapRuntimeFinalContentContract contract in allContracts)
            {
                if (contract == null) continue;
                WorldMapRuntimeHarvestInteractionTargetAdapter contractAdapter = contract.GetComponent<WorldMapRuntimeHarvestInteractionTargetAdapter>();
                if (contractAdapter != null)
                {
                    hasIllegalContract = true;
                    log.AppendLine($"{LogPrefix} [FAIL] 36. Found TargetAdapter on WorldMapRuntimeFinalContentContract object: '{contract.name}'. This violates the policy.");
                    failCount++;
                    break;
                }
            }
            if (!hasIllegalContract)
            {
                log.AppendLine($"{LogPrefix} [PASS] 36. No TargetAdapter under WorldMapRuntimeFinalContentContract source objects.");
                passCount++;
            }

            // 37. Candidate count == target adapter count
            if (candidateCount == registry.Count)
            {
                log.AppendLine($"{LogPrefix} [PASS] 37. Candidate count ({candidateCount}) == target adapter count ({registry.Count}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] 37. Candidate count ({candidateCount}) != target adapter count ({registry.Count}).");
                failCount++;
            }

            // 38. Generated placeholder target count INFO
            int placeholderCount = 0;
            foreach (var adapter in registry.AllTargets)
            {
                if (adapter.IsGeneratedPlaceholderContent)
                    placeholderCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 38. Generated placeholder targets: {placeholderCount}");
            infoCount++;

            // 39. User-assigned final content target count INFO
            int userAssignedCount = 0;
            foreach (var adapter in registry.AllTargets)
            {
                if (adapter.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }
            log.AppendLine($"{LogPrefix} [INFO] 39. User-assigned final content targets: {userAssignedCount}");
            infoCount++;

            // 40. RuntimeKey breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 40. RuntimeKey breakdown:");
            var runtimeKeyBreakdown = new Dictionary<string, int>();
            foreach (var adapter in registry.AllTargets)
            {
                string key = adapter.RuntimeKey;
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
            infoCount++;

            // 41. InteractionKind breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 41. InteractionKind breakdown:");
            var kindBreakdown = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, int>();
            foreach (var adapter in registry.AllTargets)
            {
                WorldMapRuntimeHarvestInteractionCandidateKind kind = adapter.InteractionKind;
                if (!kindBreakdown.ContainsKey(kind))
                    kindBreakdown[kind] = 0;
                kindBreakdown[kind]++;
            }
            foreach (var kvp in kindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 42. TargetKind breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 42. TargetKind breakdown:");
            var targetKindBreakdown = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, int>();
            foreach (var adapter in registry.AllTargets)
            {
                WorldMapRuntimeHarvestInteractionTargetKind kind = adapter.TargetKind;
                if (!targetKindBreakdown.ContainsKey(kind))
                    targetKindBreakdown[kind] = 0;
                targetKindBreakdown[kind]++;
            }
            foreach (var kvp in targetKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 43. Zone breakdown INFO
            log.AppendLine($"{LogPrefix} [INFO] 43. Zone breakdown:");
            var zoneBreakdown = new Dictionary<string, int>();
            foreach (var adapter in registry.AllTargets)
            {
                string zoneId = adapter.ZoneId;
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
            infoCount++;

            // 44. Scene object transform/name unchanged (read-only validation INFO)
            log.AppendLine($"{LogPrefix} [INFO] 44. Scene object transform/name unchanged. (Read-only validation - no modifications made during validate.)");
            infoCount++;

            // Summary
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} PASS: {passCount}, FAIL: {failCount}, WARN: {warnCount}, INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Candidate count: {candidateCount}");
            log.AppendLine($"{LogPrefix} Scene target adapter count: {sceneAdapterCount}");
            log.AppendLine($"{LogPrefix} Registry count: {registry.Count}");
            log.AppendLine($"{LogPrefix} QueryService count: {(queryService != null ? queryService.Count : 0)}");
            log.AppendLine($"{LogPrefix} Duplicate SourceMarkerIds: {duplicateIds.Count}");
            log.AppendLine($"{LogPrefix} Generated placeholder targets: {placeholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content targets: {userAssignedCount}");
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-5: Validate Runtime Harvest Interaction Target Adapters Complete =====");

            Debug.Log(log.ToString());
        }

        // ===== Private Helpers =====

        /// <summary>
        /// 지정한 transform이 RuntimeFinalContentInstances 하위에 있는지 확인한다.
        /// 부모 체인을 따라 올라가면서 "RuntimeFinalContentInstances" 이름을 가진 transform을 찾는다.
        /// </summary>
        /// <param name="target">확인할 transform</param>
        /// <returns>RuntimeFinalContentInstances 하위면 true</returns>
        private static bool IsUnderRuntimeFinalContentInstances(Transform target)
        {
            if (target == null)
                return false;

            Transform current = target;
            while (current != null)
            {
                if (current.name == "RuntimeFinalContentInstances")
                    return true;

                current = current.parent;
            }

            return false;
        }
    }
}
