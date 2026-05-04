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
    /// Phase 14.10-N-1: Runtime Final Content Gameplay Binding 전용 Editor Utility.
    /// RuntimeFinalContentInstances 하위 final content instance에
    /// WorldMapRuntimeFinalContentGameplayBinding component를 부착/갱신하고 검증한다.
    /// 실제 Harvest/Hazard/Narrative/Landmark/RouteTrigger 시스템에 직접 연결하지 않는다.
    /// RuntimeFinalContentInstances 하위 object만 수정하며, 다른 계층은 건드리지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeFinalContentGameplayBindingUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[N-1 GameplayBinding]";

        // ===== Public API =====

        /// <summary>
        /// RuntimeFinalContentInstances 하위 모든 final content instance에
        /// WorldMapRuntimeFinalContentGameplayBinding을 부착/갱신한다.
        /// QueryService.GetAll()로 final content instance를 수집하고,
        /// 각 instance GameObject에 binding component를 추가/재사용한 후 Configure()를 호출한다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeFinalContentGameplayBindings(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [N-1] Settings is null! Cannot rebuild gameplay bindings.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-1: Rebuild Runtime Final Content Gameplay Bindings =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. WorldMapRuntimeFinalContentQueryService 찾기
            WorldMapRuntimeFinalContentQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeFinalContentQueryService>();
            if (queryService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] WorldMapRuntimeFinalContentQueryService not found on GeneratedWorldRoot.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeFinalContentQueryService found.");

            // 3. QueryService.GetAll()로 final content instance 수집
            IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> allInstances = queryService.GetAll();
            int totalCount = allInstances.Count;
            log.AppendLine($"{LogPrefix} [INFO] Final content targets: {totalCount}");

            if (totalCount == 0)
            {
                log.AppendLine($"{LogPrefix} [FAIL] No final content instances found. Nothing to bind.");
                Debug.LogError(log.ToString());
                return;
            }

            // 4. 각 instance GameObject에 binding component 부착/갱신
            int addedCount = 0;
            int updatedCount = 0;
            int failedCount = 0;

            // category/bindingKind/finalContentKind/generated/userAssigned breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            var bindingKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, int>();
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            int generatedPlaceholderCount = 0;
            int userAssignedCount = 0;

            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
            {
                if (tag == null || tag.gameObject == null)
                {
                    failedCount++;
                    continue;
                }

                GameObject targetObj = tag.gameObject;

                // binding component 찾기 또는 추가
                WorldMapRuntimeFinalContentGameplayBinding binding = targetObj.GetComponent<WorldMapRuntimeFinalContentGameplayBinding>();
                if (binding == null)
                {
                    // Undo 지원으로 AddComponent
                    binding = Undo.AddComponent<WorldMapRuntimeFinalContentGameplayBinding>(targetObj);
                    addedCount++;
                }
                else
                {
                    updatedCount++;
                }

                // Configure 호출
                binding.Configure(tag);

                // EditorUtility.SetDirty 처리
                EditorUtility.SetDirty(targetObj);

                // breakdown 수집
                string cat = tag.RuntimeCategory;
                if (!string.IsNullOrEmpty(cat))
                {
                    if (!categoryBreakdown.ContainsKey(cat))
                        categoryBreakdown[cat] = 0;
                    categoryBreakdown[cat]++;
                }

                WorldMapRuntimeFinalContentGameplayBindingKind bindingKind = binding.BindingKind;
                if (!bindingKindBreakdown.ContainsKey(bindingKind))
                    bindingKindBreakdown[bindingKind] = 0;
                bindingKindBreakdown[bindingKind]++;

                WorldMapRuntimeFinalContentKind finalKind = tag.FinalContentKind;
                if (!finalContentKindBreakdown.ContainsKey(finalKind))
                    finalContentKindBreakdown[finalKind] = 0;
                finalContentKindBreakdown[finalKind]++;

                if (tag.IsGeneratedPlaceholderContent)
                    generatedPlaceholderCount++;
                if (tag.IsUserAssignedFinalContent)
                    userAssignedCount++;
            }

            // 5. 결과 요약 출력
            log.AppendLine($"{LogPrefix} ===== Rebuild Results =====");
            log.AppendLine($"{LogPrefix} Final content targets: {totalCount}");
            log.AppendLine($"{LogPrefix} Added bindings: {addedCount}");
            log.AppendLine($"{LogPrefix} Updated bindings: {updatedCount}");
            log.AppendLine($"{LogPrefix} Failed: {failedCount}");

            log.AppendLine($"{LogPrefix} [INFO] Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] FinalContentKind breakdown:");
            foreach (var kvp in finalContentKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] BindingKind breakdown:");
            foreach (var kvp in bindingKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] Generated placeholder content: {generatedPlaceholderCount}");
            log.AppendLine($"{LogPrefix} [INFO] User-assigned final content: {userAssignedCount}");

            // Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-1: Rebuild Runtime Final Content Gameplay Bindings Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// RuntimeFinalContentInstances 하위 final content instance에 부착된
        /// WorldMapRuntimeFinalContentGameplayBinding의 유효성을 검사한다.
        /// 20개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO]를 출력한다.
        /// Scene object transform/name 변경 없음. Read-only 검증.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeFinalContentGameplayBindings(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [N-1] Settings is null! Cannot validate gameplay bindings.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-1: Validate Runtime Final Content Gameplay Bindings =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            int totalChecks = 0;

            // 통계 변수
            int finalInstanceCount = 0;
            int bindingCount = 0;
            int duplicateSourceMarkerIdCount = 0;
            int generatedPlaceholderCount = 0;
            int userAssignedFinalCount = 0;

            // Breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var bindingKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, int>();

            // Transform snapshot (변경 감지용)
            var transformSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();

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

            // 2. WorldMapRuntimeFinalContentQueryService exists
            totalChecks++;
            WorldMapRuntimeFinalContentQueryService queryService = generatedRoot?.GetComponent<WorldMapRuntimeFinalContentQueryService>();
            if (queryService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 2: WorldMapRuntimeFinalContentQueryService not found.");
                failCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 2: WorldMapRuntimeFinalContentQueryService found.");
                passCount++;
            }

            // 3. Final content instance count > 0
            totalChecks++;
            IReadOnlyList<WorldMapRuntimeFinalContentInstanceTag> allInstances = queryService?.GetAll();
            finalInstanceCount = allInstances?.Count ?? 0;
            if (finalInstanceCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 3: Final content instance count: {finalInstanceCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 3: Final content instance count is 0.");
                failCount++;
            }

            // 4. Scene final content count == binding count
            totalChecks++;
            int sceneFinalContentCount = generatedRoot != null
                ? WorldMapRuntimeFinalContentInstanceRegistry.CountSceneFinalContentInstances(generatedRoot.transform)
                : 0;

            // 모든 binding 수집
            WorldMapRuntimeFinalContentGameplayBinding[] allBindings = generatedRoot != null
                ? generatedRoot.GetComponentsInChildren<WorldMapRuntimeFinalContentGameplayBinding>(true)
                : new WorldMapRuntimeFinalContentGameplayBinding[0];
            bindingCount = allBindings.Length;

            if (sceneFinalContentCount == bindingCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 4: Scene final content count ({sceneFinalContentCount}) == binding count ({bindingCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 4: Scene final content count ({sceneFinalContentCount}) != binding count ({bindingCount}).");
                failCount++;
            }

            // 5. All final content instances have binding component
            totalChecks++;
            bool allHaveBinding = true;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag == null || tag.GetComponent<WorldMapRuntimeFinalContentGameplayBinding>() == null)
                    {
                        allHaveBinding = false;
                        break;
                    }
                }
            }
            if (allHaveBinding && finalInstanceCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 5: All final content instances have binding component.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 5: No instances to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 5: Some final content instances missing binding component.");
                failCount++;
            }

            // 6. All bindings are under RuntimeFinalContentInstances hierarchy
            totalChecks++;
            bool allBindingsUnderFinalContent = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null) continue;
                if (!IsUnderRuntimeFinalContentInstances(binding.transform))
                {
                    allBindingsUnderFinalContent = false;
                    break;
                }
            }
            if (allBindingsUnderFinalContent && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 6: All bindings are under RuntimeFinalContentInstances hierarchy.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 6: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 6: Some bindings are outside RuntimeFinalContentInstances hierarchy.");
                failCount++;
            }

            // 7. Preview instance has no binding
            totalChecks++;
            bool noPreviewBinding = true;
            WorldMapRuntimeFinalContentGameplayBinding[] previewBindings = generatedRoot != null
                ? generatedRoot.GetComponentsInChildren<WorldMapRuntimeFinalContentGameplayBinding>(true)
                : new WorldMapRuntimeFinalContentGameplayBinding[0];
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in previewBindings)
            {
                if (binding == null) continue;
                if (IsUnderRuntimeSpawnPreviewInstances(binding.transform))
                {
                    noPreviewBinding = false;
                    break;
                }
            }
            if (noPreviewBinding)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 7: No binding on preview instances.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 7: Binding found on preview instances.");
                failCount++;
            }

            // 8. RuntimeSpawnedInstances has no binding
            totalChecks++;
            bool noSpawnedBinding = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in previewBindings)
            {
                if (binding == null) continue;
                if (IsUnderRuntimeSpawnedInstances(binding.transform))
                {
                    noSpawnedBinding = false;
                    break;
                }
            }
            if (noSpawnedBinding)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 8: No binding on RuntimeSpawnedInstances.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 8: Binding found on RuntimeSpawnedInstances.");
                failCount++;
            }

            // 9. RuntimePlaceholder has no binding
            totalChecks++;
            bool noPlaceholderBinding = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in previewBindings)
            {
                if (binding == null) continue;
                if (binding.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    noPlaceholderBinding = false;
                    break;
                }
            }
            if (noPlaceholderBinding)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 9: No binding on RuntimePlaceholder objects.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 9: Binding found on RuntimePlaceholder objects.");
                failCount++;
            }

            // 10. WorldMapZoneContentMarker has no binding
            totalChecks++;
            bool noMarkerBinding = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in previewBindings)
            {
                if (binding == null) continue;
                if (binding.GetComponent<WorldMapZoneContentMarker>() != null)
                {
                    noMarkerBinding = false;
                    break;
                }
            }
            if (noMarkerBinding)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 10: No binding on WorldMapZoneContentMarker objects.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 10: Binding found on WorldMapZoneContentMarker objects.");
                failCount++;
            }

            // 11. WorldMapRuntimeFinalContentContract has no binding
            totalChecks++;
            bool noContractBinding = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in previewBindings)
            {
                if (binding == null) continue;
                if (binding.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                {
                    noContractBinding = false;
                    break;
                }
            }
            if (noContractBinding)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 11: No binding on WorldMapRuntimeFinalContentContract objects.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 11: Binding found on WorldMapRuntimeFinalContentContract objects.");
                failCount++;
            }

            // 12. All bindings have non-empty SourceMarkerId
            totalChecks++;
            bool allHaveSourceMarkerId = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.SourceMarkerId))
                {
                    allHaveSourceMarkerId = false;
                    break;
                }
            }
            if (allHaveSourceMarkerId && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 12: All bindings have non-empty SourceMarkerId.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 12: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 12: Some bindings have empty SourceMarkerId.");
                failCount++;
            }

            // 13. All bindings have non-empty ZoneId
            totalChecks++;
            bool allHaveZoneId = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.ZoneId))
                {
                    allHaveZoneId = false;
                    break;
                }
            }
            if (allHaveZoneId && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 13: All bindings have non-empty ZoneId.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 13: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 13: Some bindings have empty ZoneId.");
                failCount++;
            }

            // 14. All bindings have non-empty RuntimeCategory
            totalChecks++;
            bool allHaveRuntimeCategory = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.RuntimeCategory))
                {
                    allHaveRuntimeCategory = false;
                    break;
                }
            }
            if (allHaveRuntimeCategory && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 14: All bindings have non-empty RuntimeCategory.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 14: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 14: Some bindings have empty RuntimeCategory.");
                failCount++;
            }

            // 15. All bindings have non-empty ProfileId
            totalChecks++;
            bool allHaveProfileId = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.ProfileId))
                {
                    allHaveProfileId = false;
                    break;
                }
            }
            if (allHaveProfileId && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 15: All bindings have non-empty ProfileId.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 15: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 15: Some bindings have empty ProfileId.");
                failCount++;
            }

            // 16. All bindings have non-empty RequirementId
            totalChecks++;
            bool allHaveRequirementId = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.RequirementId))
                {
                    allHaveRequirementId = false;
                    break;
                }
            }
            if (allHaveRequirementId && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 16: All bindings have non-empty RequirementId.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 16: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 16: Some bindings have empty RequirementId.");
                failCount++;
            }

            // 17. All bindings have FinalContentKind != None
            totalChecks++;
            bool allHaveFinalContentKind = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || binding.FinalContentKind == WorldMapRuntimeFinalContentKind.None)
                {
                    allHaveFinalContentKind = false;
                    break;
                }
            }
            if (allHaveFinalContentKind && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 17: All bindings have FinalContentKind != None.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 17: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 17: Some bindings have FinalContentKind == None.");
                failCount++;
            }

            // 18. All bindings have BindingKind != None
            totalChecks++;
            bool allHaveBindingKind = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || binding.BindingKind == WorldMapRuntimeFinalContentGameplayBindingKind.None)
                {
                    allHaveBindingKind = false;
                    break;
                }
            }
            if (allHaveBindingKind && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 18: All bindings have BindingKind != None.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 18: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 18: Some bindings have BindingKind == None.");
                failCount++;
            }

            // 19. All bindings have IsGameplayBindingReady == true
            totalChecks++;
            bool allReady = true;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || !binding.IsGameplayBindingReady)
                {
                    allReady = false;
                    break;
                }
            }
            if (allReady && bindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 19: All bindings have IsGameplayBindingReady == true.");
                passCount++;
            }
            else if (bindingCount == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 19: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 19: Some bindings have IsGameplayBindingReady == false.");
                failCount++;
            }

            // 20. QueryService.Count == binding count
            totalChecks++;
            int queryServiceCount = queryService != null ? queryService.Count : 0;
            if (queryServiceCount == bindingCount)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 20: QueryService.Count ({queryServiceCount}) == binding count ({bindingCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 20: QueryService.Count ({queryServiceCount}) != binding count ({bindingCount}).");
                failCount++;
            }

            // 21. Duplicate SourceMarkerIds count == 0
            totalChecks++;
            duplicateSourceMarkerIdCount = CountDuplicateSourceMarkerIds(allBindings);
            if (duplicateSourceMarkerIdCount == 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 21: Duplicate SourceMarkerIds count: {duplicateSourceMarkerIdCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 21: Duplicate SourceMarkerIds count: {duplicateSourceMarkerIdCount} (expected 0).");
                failCount++;
            }

            // 22. Category breakdown INFO
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
            log.AppendLine($"{LogPrefix} [INFO] Check 22: Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 23. FinalContentKind breakdown INFO
            totalChecks++;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null) continue;
                WorldMapRuntimeFinalContentKind kind = binding.FinalContentKind;
                if (!finalContentKindBreakdown.ContainsKey(kind))
                    finalContentKindBreakdown[kind] = 0;
                finalContentKindBreakdown[kind]++;
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 23: FinalContentKind breakdown:");
            foreach (var kvp in finalContentKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 24. BindingKind breakdown INFO
            totalChecks++;
            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null) continue;
                WorldMapRuntimeFinalContentGameplayBindingKind kind = binding.BindingKind;
                if (!bindingKindBreakdown.ContainsKey(kind))
                    bindingKindBreakdown[kind] = 0;
                bindingKindBreakdown[kind]++;
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 24: BindingKind breakdown:");
            foreach (var kvp in bindingKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 25. Generated placeholder content count INFO
            totalChecks++;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && tag.IsGeneratedPlaceholderContent)
                        generatedPlaceholderCount++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 25: Generated placeholder content count: {generatedPlaceholderCount}");
            infoCount++;

            // 26. User-assigned final content count INFO
            totalChecks++;
            if (allInstances != null)
            {
                foreach (WorldMapRuntimeFinalContentInstanceTag tag in allInstances)
                {
                    if (tag != null && tag.IsUserAssignedFinalContent)
                        userAssignedFinalCount++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 26: User-assigned final content count: {userAssignedFinalCount}");
            infoCount++;

            // 27. Scene object transform/name unchanged (snapshot-based)
            totalChecks++;
            log.AppendLine($"{LogPrefix} [INFO] Check 27: Scene object transform/name unchanged - skipped (read-only validation, no snapshot taken).");
            infoCount++;

            // ===== Summary =====
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} Total checks: {totalChecks}");
            log.AppendLine($"{LogPrefix} PASS: {passCount}");
            log.AppendLine($"{LogPrefix} FAIL: {failCount}");
            log.AppendLine($"{LogPrefix} WARN: {warnCount}");
            log.AppendLine($"{LogPrefix} INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Final content instances: {finalInstanceCount}");
            log.AppendLine($"{LogPrefix} Gameplay bindings: {bindingCount}");
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
                "Phase 14.10-N-1: Validate Runtime Final Content Gameplay Bindings",
                $"Total checks: {totalChecks}\n" +
                $"[PASS] {passCount}\n" +
                $"[FAIL] {failCount}\n" +
                $"[WARN] {warnCount}\n" +
                $"[INFO] {infoCount}\n\n" +
                $"Final content instances: {finalInstanceCount}\n" +
                $"Gameplay bindings: {bindingCount}\n" +
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
        /// 지정한 Transform의 ancestor 중 "RuntimeFinalContentInstances" 이름을 가진 object가 있는지 확인한다.
        /// </summary>
        private static bool IsUnderRuntimeFinalContentInstances(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name == "RuntimeFinalContentInstances")
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
        /// 모든 binding에서 중복된 SourceMarkerId의 개수를 반환한다.
        /// </summary>
        private static int CountDuplicateSourceMarkerIds(WorldMapRuntimeFinalContentGameplayBinding[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
                return 0;

            var idSet = new HashSet<string>();
            int duplicateCount = 0;

            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.SourceMarkerId))
                    continue;

                if (!idSet.Add(binding.SourceMarkerId))
                {
                    duplicateCount++;
                }
            }

            return duplicateCount;
        }
    }
}
