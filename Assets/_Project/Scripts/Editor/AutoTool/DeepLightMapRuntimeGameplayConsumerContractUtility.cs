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
    /// Phase 14.10-N-3: Runtime Gameplay Consumer Contract 전용 Editor Utility.
    /// RuntimeFinalContentInstances 하위 final content object에 WorldMapRuntimeGameplayConsumerContract를
    /// 부착/갱신하고 검증한다. 실제 Harvest/Hazard/Narrative/Landmark/RouteTrigger gameplay 시스템에
    /// 직접 연결하지 않는다. UnityEditor API만 사용한다.
    /// </summary>
    public static class DeepLightMapRuntimeGameplayConsumerContractUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[N-3 ConsumerContract]";

        // ===== Public API =====

        /// <summary>
        /// RuntimeFinalContentInstances 하위 모든 final content object에
        /// WorldMapRuntimeGameplayConsumerContract를 부착/갱신한다.
        /// WorldMapRuntimeFinalContentGameplayBindingQueryService.GetAll()로 binding을 수집하고,
        /// 각 binding GameObject에 contract를 추가/재사용하여 Configure(binding)을 호출한다.
        /// Scene object의 transform/name을 변경하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeGameplayConsumerContracts(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [N-3] Settings is null! Cannot rebuild consumer contracts.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-3: Rebuild Runtime Gameplay Consumer Contracts =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. WorldMapRuntimeFinalContentGameplayBindingQueryService 찾기
            WorldMapRuntimeFinalContentGameplayBindingQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeFinalContentGameplayBindingQueryService>();
            if (queryService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] WorldMapRuntimeFinalContentGameplayBindingQueryService not found on '{generatedRoot.name}'. Run N-2 Rebuild first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeFinalContentGameplayBindingQueryService found.");

            // 3. QueryService.GetAll()로 binding 수집
            IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> allBindings = queryService.GetAll();
            if (allBindings == null || allBindings.Count == 0)
            {
                log.AppendLine($"{LogPrefix} [FAIL] QueryService.GetAll() returned 0 bindings. Run N-2 Rebuild first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [INFO] QueryService.GetAll() returned {allBindings.Count} bindings.");

            // 4. 각 binding GameObject에 contract 부착/갱신
            int addedCount = 0;
            int updatedCount = 0;
            int failedCount = 0;

            // Breakdown 통계
            var categoryBreakdown = new Dictionary<string, int>();
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var bindingKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, int>();
            var consumerKindBreakdown = new Dictionary<WorldMapRuntimeGameplayConsumerKind, int>();
            int generatedPlaceholderCount = 0;
            int userAssignedFinalCount = 0;

            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
            {
                if (binding == null || binding.gameObject == null)
                {
                    failedCount++;
                    continue;
                }

                GameObject bindingGO = binding.gameObject;

                // RuntimeFinalContentInstances 하위에 있는 binding만 contract 부착 대상
                if (!IsUnderRuntimeFinalContentInstances(bindingGO.transform))
                {
                    log.AppendLine($"{LogPrefix} [WARN] Binding '{bindingGO.name}' is not under RuntimeFinalContentInstances. Skipping.");
                    continue;
                }

                // contract component 확인/추가
                WorldMapRuntimeGameplayConsumerContract contract = bindingGO.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                if (contract == null)
                {
                    // Undo.AddComponent로 추가
                    contract = Undo.AddComponent<WorldMapRuntimeGameplayConsumerContract>(bindingGO);
                    addedCount++;
                }
                else
                {
                    updatedCount++;
                }

                // Configure 호출
                contract.Configure(binding);

                // EditorUtility.SetDirty 처리
                EditorUtility.SetDirty(bindingGO);

                // Breakdown 통계 수집
                string cat = contract.RuntimeCategory;
                if (!string.IsNullOrEmpty(cat))
                {
                    if (!categoryBreakdown.ContainsKey(cat))
                        categoryBreakdown[cat] = 0;
                    categoryBreakdown[cat]++;
                }

                WorldMapRuntimeFinalContentKind fKind = contract.FinalContentKind;
                if (!finalContentKindBreakdown.ContainsKey(fKind))
                    finalContentKindBreakdown[fKind] = 0;
                finalContentKindBreakdown[fKind]++;

                WorldMapRuntimeFinalContentGameplayBindingKind bKind = contract.BindingKind;
                if (!bindingKindBreakdown.ContainsKey(bKind))
                    bindingKindBreakdown[bKind] = 0;
                bindingKindBreakdown[bKind]++;

                WorldMapRuntimeGameplayConsumerKind cKind = contract.ConsumerKind;
                if (!consumerKindBreakdown.ContainsKey(cKind))
                    consumerKindBreakdown[cKind] = 0;
                consumerKindBreakdown[cKind]++;

                if (contract.IsGeneratedPlaceholderContent)
                    generatedPlaceholderCount++;

                if (contract.IsUserAssignedFinalContent)
                    userAssignedFinalCount++;
            }

            // 5. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            // 6. Breakdown 출력
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

            log.AppendLine($"{LogPrefix} [INFO] ConsumerKind breakdown:");
            foreach (var kvp in consumerKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            log.AppendLine($"{LogPrefix} [INFO] Generated placeholder content: {generatedPlaceholderCount}");
            log.AppendLine($"{LogPrefix} [INFO] User-assigned final content: {userAssignedFinalCount}");
            log.AppendLine($"{LogPrefix} [INFO] Contracts added: {addedCount}, updated: {updatedCount}, failed: {failedCount}");

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-3: Rebuild Runtime Gameplay Consumer Contracts Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// RuntimeFinalContentInstances 하위 final content object에 부착된
        /// WorldMapRuntimeGameplayConsumerContract의 유효성을 검사한다.
        /// 28개 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Scene object transform/name 변경 없음. Read-only 검증.
        /// QueryService cache가 비어 있는데 scene binding이 있으면 TryInitializeFromRoot 정도는 허용.
        /// GameObject 생성/삭제/이동/name 변경은 금지.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeGameplayConsumerContracts(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [N-3] Settings is null! Cannot validate consumer contracts.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-N-3: Validate Runtime Gameplay Consumer Contracts =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            int totalChecks = 0;

            // 통계 변수
            int sceneBindingCount = 0;
            int sceneContractCount = 0;
            int duplicateSourceMarkerIdCount = 0;
            int generatedPlaceholderCount = 0;
            int userAssignedFinalCount = 0;

            // Breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var bindingKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentGameplayBindingKind, int>();
            var consumerKindBreakdown = new Dictionary<WorldMapRuntimeGameplayConsumerKind, int>();

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

            // 2. WorldMapRuntimeFinalContentGameplayBindingQueryService exists
            totalChecks++;
            WorldMapRuntimeFinalContentGameplayBindingQueryService queryService = generatedRoot?.GetComponent<WorldMapRuntimeFinalContentGameplayBindingQueryService>();
            if (queryService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 2: WorldMapRuntimeFinalContentGameplayBindingQueryService not found.");
                failCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 2: WorldMapRuntimeFinalContentGameplayBindingQueryService found.");
                passCount++;
            }

            // QueryService cache가 비어 있는데 scene binding이 있으면 TryInitializeFromRoot 허용
            if (queryService != null && queryService.Count == 0 && generatedRoot != null)
            {
                // scene binding count 확인
                int sceneBindings = WorldMapRuntimeFinalContentGameplayBindingRegistry.CountSceneGameplayBindings(generatedRoot.transform);
                if (sceneBindings > 0)
                {
                    log.AppendLine($"{LogPrefix} [INFO] QueryService cache is empty but scene has {sceneBindings} bindings. Attempting TryInitializeFromRoot.");
                    queryService.TryInitializeFromRoot(generatedRoot.transform);
                }
            }

            // 3. Scene gameplay binding count > 0
            totalChecks++;
            sceneBindingCount = generatedRoot != null
                ? WorldMapRuntimeFinalContentGameplayBindingRegistry.CountSceneGameplayBindings(generatedRoot.transform)
                : 0;
            if (sceneBindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 3: Scene gameplay bindings: {sceneBindingCount}");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 3: Scene gameplay bindings count is 0.");
                failCount++;
            }

            // 4. Scene consumer contract count == scene gameplay binding count
            totalChecks++;
            IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> allBindings = queryService?.GetAll();
            sceneContractCount = 0;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() != null)
                    {
                        sceneContractCount++;
                    }
                }
            }
            if (sceneContractCount == sceneBindingCount && sceneBindingCount > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 4: Scene consumer contract count ({sceneContractCount}) == scene gameplay binding count ({sceneBindingCount}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 4: Scene consumer contract count ({sceneContractCount}) != scene gameplay binding count ({sceneBindingCount}).");
                failCount++;
            }

            // 5. 모든 gameplay binding에 consumer contract 존재
            totalChecks++;
            bool allBindingsHaveContract = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() == null)
                    {
                        allBindingsHaveContract = false;
                        break;
                    }
                }
            }
            if (allBindingsHaveContract && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 5: All gameplay bindings have consumer contract.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 5: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 5: Some gameplay bindings missing consumer contract.");
                failCount++;
            }

            // 6. 모든 consumer contract가 RuntimeFinalContentInstances 하위에 있음
            totalChecks++;
            bool allContractsUnderFinalContent = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() != null)
                    {
                        if (!IsUnderRuntimeFinalContentInstances(binding.transform))
                        {
                            allContractsUnderFinalContent = false;
                            break;
                        }
                    }
                }
            }
            if (allContractsUnderFinalContent && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 6: All consumer contracts are under RuntimeFinalContentInstances.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 6: No bindings to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 6: Some consumer contracts are not under RuntimeFinalContentInstances.");
                failCount++;
            }

            // 7. RuntimeSpawnedInstances object에 consumer contract 없음
            totalChecks++;
            bool noSpawnedContract = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() != null)
                    {
                        if (IsUnderRuntimeSpawnedInstances(binding.transform))
                        {
                            noSpawnedContract = false;
                            break;
                        }
                    }
                }
            }
            if (noSpawnedContract)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 7: RuntimeSpawnedInstances object has no consumer contract.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 7: RuntimeSpawnedInstances object has consumer contract.");
                failCount++;
            }

            // 8. RuntimeSpawnInstances preview object에 consumer contract 없음
            totalChecks++;
            bool noPreviewContract = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() != null)
                    {
                        if (IsUnderRuntimeSpawnPreviewInstances(binding.transform))
                        {
                            noPreviewContract = false;
                            break;
                        }
                    }
                }
            }
            if (noPreviewContract)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 8: RuntimeSpawnInstances preview object has no consumer contract.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 8: RuntimeSpawnInstances preview object has consumer contract.");
                failCount++;
            }

            // 9. RuntimePlaceholder 원본 object에 consumer contract 없음
            totalChecks++;
            bool noPlaceholderContract = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() != null)
                    {
                        if (binding.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                        {
                            noPlaceholderContract = false;
                            break;
                        }
                    }
                }
            }
            if (noPlaceholderContract)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 9: RuntimePlaceholder object has no consumer contract.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 9: RuntimePlaceholder object has consumer contract.");
                failCount++;
            }

            // 10. WorldMapZoneContentMarker object에 consumer contract 없음
            totalChecks++;
            bool noMarkerContract = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() != null)
                    {
                        if (binding.GetComponent<WorldMapZoneContentMarker>() != null)
                        {
                            noMarkerContract = false;
                            break;
                        }
                    }
                }
            }
            if (noMarkerContract)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 10: WorldMapZoneContentMarker object has no consumer contract.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 10: WorldMapZoneContentMarker object has consumer contract.");
                failCount++;
            }

            // 11. WorldMapRuntimeFinalContentContract source object에 consumer contract 없음
            totalChecks++;
            bool noSourceContract = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    if (binding != null && binding.GetComponent<WorldMapRuntimeGameplayConsumerContract>() != null)
                    {
                        if (binding.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                        {
                            noSourceContract = false;
                            break;
                        }
                    }
                }
            }
            if (noSourceContract)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 11: WorldMapRuntimeFinalContentContract source object has no consumer contract.");
                passCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 11: WorldMapRuntimeFinalContentContract source object has consumer contract.");
                failCount++;
            }

            // 12. 모든 contract sourceMarkerId non-empty
            totalChecks++;
            bool allSourceMarkerIdNonEmpty = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && string.IsNullOrEmpty(contract.SourceMarkerId))
                    {
                        allSourceMarkerIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allSourceMarkerIdNonEmpty && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 12: All contracts have non-empty SourceMarkerId.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 12: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 12: Some contracts have empty SourceMarkerId.");
                failCount++;
            }

            // 13. 모든 contract zoneId non-empty
            totalChecks++;
            bool allZoneIdNonEmpty = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && string.IsNullOrEmpty(contract.ZoneId))
                    {
                        allZoneIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allZoneIdNonEmpty && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 13: All contracts have non-empty ZoneId.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 13: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 13: Some contracts have empty ZoneId.");
                failCount++;
            }

            // 14. 모든 contract runtimeCategory non-empty
            totalChecks++;
            bool allCategoryNonEmpty = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && string.IsNullOrEmpty(contract.RuntimeCategory))
                    {
                        allCategoryNonEmpty = false;
                        break;
                    }
                }
            }
            if (allCategoryNonEmpty && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 14: All contracts have non-empty RuntimeCategory.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 14: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 14: Some contracts have empty RuntimeCategory.");
                failCount++;
            }

            // 15. 모든 contract profileId non-empty
            totalChecks++;
            bool allProfileIdNonEmpty = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && string.IsNullOrEmpty(contract.ProfileId))
                    {
                        allProfileIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allProfileIdNonEmpty && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 15: All contracts have non-empty ProfileId.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 15: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 15: Some contracts have empty ProfileId.");
                failCount++;
            }

            // 16. 모든 contract requirementId non-empty
            totalChecks++;
            bool allRequirementIdNonEmpty = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && string.IsNullOrEmpty(contract.RequirementId))
                    {
                        allRequirementIdNonEmpty = false;
                        break;
                    }
                }
            }
            if (allRequirementIdNonEmpty && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 16: All contracts have non-empty RequirementId.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 16: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 16: Some contracts have empty RequirementId.");
                failCount++;
            }

            // 17. 모든 contract finalContentKind != None
            totalChecks++;
            bool allFinalContentKindValid = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && contract.FinalContentKind == WorldMapRuntimeFinalContentKind.None)
                    {
                        allFinalContentKindValid = false;
                        break;
                    }
                }
            }
            if (allFinalContentKindValid && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 17: All contracts have FinalContentKind != None.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 17: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 17: Some contracts have FinalContentKind == None.");
                failCount++;
            }

            // 18. 모든 contract bindingKind != None
            totalChecks++;
            bool allBindingKindValid = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && contract.BindingKind == WorldMapRuntimeFinalContentGameplayBindingKind.None)
                    {
                        allBindingKindValid = false;
                        break;
                    }
                }
            }
            if (allBindingKindValid && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 18: All contracts have BindingKind != None.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 18: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 18: Some contracts have BindingKind == None.");
                failCount++;
            }

            // 19. 모든 contract consumerKind != None
            totalChecks++;
            bool allConsumerKindValid = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && contract.ConsumerKind == WorldMapRuntimeGameplayConsumerKind.None)
                    {
                        allConsumerKindValid = false;
                        break;
                    }
                }
            }
            if (allConsumerKindValid && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 19: All contracts have ConsumerKind != None.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 19: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 19: Some contracts have ConsumerKind == None.");
                failCount++;
            }

            // 20. 모든 contract IsReady == true
            totalChecks++;
            bool allReady = true;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract != null && !contract.IsReady())
                    {
                        allReady = false;
                        break;
                    }
                }
            }
            if (allReady && (allBindings?.Count ?? 0) > 0)
            {
                log.AppendLine($"{LogPrefix} [PASS] Check 20: All contracts have IsReady == true.");
                passCount++;
            }
            else if ((allBindings?.Count ?? 0) == 0)
            {
                log.AppendLine($"{LogPrefix} [WARN] Check 20: No contracts to check.");
                warnCount++;
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Check 20: Some contracts have IsReady == false.");
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
                log.AppendLine($"{LogPrefix} [WARN] Check 21: Duplicate SourceMarkerIds count: {duplicateSourceMarkerIdCount} (expected 0).");
                warnCount++;
            }

            // 22. Category breakdown INFO
            totalChecks++;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
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
            log.AppendLine($"{LogPrefix} [INFO] Check 22: Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 23. FinalContentKind breakdown INFO
            totalChecks++;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract == null) continue;
                    WorldMapRuntimeFinalContentKind kind = contract.FinalContentKind;
                    if (!finalContentKindBreakdown.ContainsKey(kind))
                        finalContentKindBreakdown[kind] = 0;
                    finalContentKindBreakdown[kind]++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 23: FinalContentKind breakdown:");
            foreach (var kvp in finalContentKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 24. BindingKind breakdown INFO
            totalChecks++;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract == null) continue;
                    WorldMapRuntimeFinalContentGameplayBindingKind kind = contract.BindingKind;
                    if (!bindingKindBreakdown.ContainsKey(kind))
                        bindingKindBreakdown[kind] = 0;
                    bindingKindBreakdown[kind]++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 24: BindingKind breakdown:");
            foreach (var kvp in bindingKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 25. ConsumerKind breakdown INFO
            totalChecks++;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract == null) continue;
                    WorldMapRuntimeGameplayConsumerKind kind = contract.ConsumerKind;
                    if (!consumerKindBreakdown.ContainsKey(kind))
                        consumerKindBreakdown[kind] = 0;
                    consumerKindBreakdown[kind]++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 25: ConsumerKind breakdown:");
            foreach (var kvp in consumerKindBreakdown)
            {
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 26. Generated placeholder content count INFO
            totalChecks++;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract == null) continue;
                    if (contract.IsGeneratedPlaceholderContent)
                        generatedPlaceholderCount++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 26: Generated placeholder content count: {generatedPlaceholderCount}");
            infoCount++;

            // 27. User-assigned final content count INFO
            totalChecks++;
            if (allBindings != null)
            {
                foreach (WorldMapRuntimeFinalContentGameplayBinding binding in allBindings)
                {
                    WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                    if (contract == null) continue;
                    if (contract.IsUserAssignedFinalContent)
                        userAssignedFinalCount++;
                }
            }
            log.AppendLine($"{LogPrefix} [INFO] Check 27: User-assigned final content count: {userAssignedFinalCount}");
            infoCount++;

            // 28. Scene object transform/name unchanged (read-only)
            totalChecks++;
            log.AppendLine($"{LogPrefix} [INFO] Check 28: Scene object transform/name unchanged - skipped (read-only validation).");
            infoCount++;

            // ===== Summary =====
            log.AppendLine($"{LogPrefix} ===== Validation Summary =====");
            log.AppendLine($"{LogPrefix} Total checks: {totalChecks}");
            log.AppendLine($"{LogPrefix} PASS: {passCount}");
            log.AppendLine($"{LogPrefix} FAIL: {failCount}");
            log.AppendLine($"{LogPrefix} WARN: {warnCount}");
            log.AppendLine($"{LogPrefix} INFO: {infoCount}");
            log.AppendLine($"{LogPrefix} Scene gameplay bindings: {sceneBindingCount}");
            log.AppendLine($"{LogPrefix} Consumer contracts: {sceneContractCount}");
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
                "Phase 14.10-N-3: Validate Runtime Gameplay Consumer Contracts",
                $"Total checks: {totalChecks}\n" +
                $"[PASS] {passCount}\n" +
                $"[FAIL] {failCount}\n" +
                $"[WARN] {warnCount}\n" +
                $"[INFO] {infoCount}\n\n" +
                $"Scene gameplay bindings: {sceneBindingCount}\n" +
                $"Consumer contracts: {sceneContractCount}\n" +
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

        /// <summary>
        /// 주어진 binding 목록에서 중복된 SourceMarkerId의 개수를 반환한다.
        /// </summary>
        private static int CountDuplicateSourceMarkerIds(IReadOnlyList<WorldMapRuntimeFinalContentGameplayBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0)
                return 0;

            var seen = new HashSet<string>();
            int duplicateCount = 0;

            foreach (WorldMapRuntimeFinalContentGameplayBinding binding in bindings)
            {
                WorldMapRuntimeGameplayConsumerContract contract = binding?.GetComponent<WorldMapRuntimeGameplayConsumerContract>();
                if (contract == null) continue;

                string markerId = contract.SourceMarkerId;
                if (string.IsNullOrEmpty(markerId))
                    continue;

                if (!seen.Add(markerId))
                {
                    duplicateCount++;
                }
            }

            return duplicateCount;
        }
    }
}
