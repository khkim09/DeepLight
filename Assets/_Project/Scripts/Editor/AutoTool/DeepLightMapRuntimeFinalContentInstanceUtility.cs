using System;
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
    /// Phase 14.10-M-5: Runtime Final Content Instance 전용 Editor Utility.
    /// M-4 Resolution Plan을 기반으로 실제 final content instance를 scene에 생성/검증/삭제한다.
    /// 원본 RuntimeSpawnedInstances / RuntimePlaceholder / Marker / DebugVisual / Binding metadata는
    /// 절대 삭제하거나 이름/transform 변경하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeFinalContentInstanceUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[M-5 FinalContentInstance]";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeFinalContentRequirementDatabase.asset";
        private const string FinalContentRootName = "RuntimeFinalContentInstances";

        // ===== Public API =====

        /// <summary>
        /// M-5 Rebuild: M-4 Resolution Plan을 기반으로 RuntimeFinalContentInstances root 아래에
        /// final content instance를 생성한다. 기존 final instance root가 있으면 먼저 정리한 뒤 재생성한다.
        /// source RuntimeSpawnedInstances는 기본 보존하며, disableSourceRuntimeSpawnedInstances == true일 때만 비활성화한다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        /// <param name="disableSourceRuntimeSpawnedInstances">true이면 source RuntimeSpawnedInstances를 SetActive(false) 처리</param>
        public static void RebuildRuntimeFinalContentInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context,
            bool disableSourceRuntimeSpawnedInstances)
        {
            // StringBuilder로 모든 로그를 누적하여 마지막에 한 번만 출력
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-5: Rebuild Runtime Final Content Instances =====");
            logBuilder.AppendLine($"{LogPrefix} Disable source runtime spawned instances: {disableSourceRuntimeSpawnedInstances}");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(logBuilder.ToString());
                return;
            }
            logBuilder.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. QueryService 찾기
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null || queryService.Count == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] QueryService not found or empty. Run M-1 first.");
                Debug.LogError(logBuilder.ToString());
                return;
            }
            logBuilder.AppendLine($"{LogPrefix} [PASS] QueryService found. Adapter count: {queryService.Count}");

            // 3. Requirement Database asset 로드
            WorldMapRuntimeFinalContentRequirementDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeFinalContentRequirementDatabaseSO>(DatabaseAssetPath);
            if (database == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Requirement Database not found at: {DatabaseAssetPath}");
                Debug.LogError(logBuilder.ToString());
                return;
            }
            logBuilder.AppendLine($"{LogPrefix} [PASS] Requirement Database loaded. Entries: {database.Entries?.Count ?? 0}");

            // 4. 모든 adapter 수집
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
            int totalAdapters = allAdapters.Count;
            logBuilder.AppendLine($"{LogPrefix} [INFO] Total adapters collected: {totalAdapters}");

            // 5. 각 adapter에 대해 plan 생성
            List<WorldMapRuntimeFinalContentResolutionPlan> validPlans = new List<WorldMapRuntimeFinalContentResolutionPlan>();
            int failedPlans = 0;

            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;

                bool success = WorldMapRuntimeFinalContentResolver.TryBuildPlan(adapter, database, out WorldMapRuntimeFinalContentResolutionPlan plan);

                if (success && plan != null && plan.IsValidPlan)
                {
                    validPlans.Add(plan);
                }
                else
                {
                    failedPlans++;
                }
            }

            logBuilder.AppendLine($"{LogPrefix} [INFO] Valid plans: {validPlans.Count}, Failed plans: {failedPlans}");

            if (validPlans.Count == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] No valid plans to process. Aborting rebuild.");
                Debug.LogError(logBuilder.ToString());
                return;
            }

            // 6. 기존 final instance root가 있으면 먼저 Clear (reactivate=false로 generated final instance만 정리)
            ClearRuntimeFinalContentInstancesInternal(generatedRoot, logBuilder, false);

            // 7. ZoneRoots 부모 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] '{settings.ZoneRootParentName}' not found under '{generatedRoot.name}'.");
                Debug.LogError(logBuilder.ToString());
                return;
            }

            // 8. 각 plan에 대해 final content instance 생성
            int instancesCreated = 0;
            int instancesFailed = 0;

            // 통계
            var categoryBreakdown = new Dictionary<string, int>();
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            int generatedPlaceholderCount = 0;
            int userAssignedFinalCount = 0;

            foreach (WorldMapRuntimeFinalContentResolutionPlan plan in validPlans)
            {
                try
                {
                    // 8a. ZoneRoot 찾기
                    string zoneId = plan.ZoneId;
                    Transform zoneRootTransform = zoneRootsTransform.Find($"ZoneRoot_{zoneId}");
                    if (zoneRootTransform == null)
                    {
                        logBuilder.AppendLine($"{LogPrefix} [WARN] ZoneRoot_{zoneId} not found. Skipping plan for marker '{plan.SourceMarkerId}'.");
                        instancesFailed++;
                        continue;
                    }

                    // 8b. RuntimeFinalContentInstances root 찾기 또는 생성
                    Transform finalContentRootTransform = zoneRootTransform.Find(FinalContentRootName);
                    GameObject finalContentRoot;
                    if (finalContentRootTransform == null)
                    {
                        finalContentRoot = new GameObject(FinalContentRootName);
                        finalContentRoot.transform.SetParent(zoneRootTransform, false);
                        Undo.RegisterCreatedObjectUndo(finalContentRoot, "Create RuntimeFinalContentInstances Root");
                    }
                    else
                    {
                        finalContentRoot = finalContentRootTransform.gameObject;
                    }

                    // 8c. finalContentKind 기준 category root 생성
                    string categoryRootName = GetCategoryRootName(plan.FinalContentKind);
                    Transform categoryRootTransform = finalContentRoot.transform.Find(categoryRootName);
                    GameObject categoryRoot;
                    if (categoryRootTransform == null)
                    {
                        categoryRoot = new GameObject(categoryRootName);
                        categoryRoot.transform.SetParent(finalContentRoot.transform, false);
                        Undo.RegisterCreatedObjectUndo(categoryRoot, $"Create {categoryRootName} Category Root");
                    }
                    else
                    {
                        categoryRoot = categoryRootTransform.gameObject;
                    }

                    // 8d. instance 이름 생성 (파일/오브젝트 이름에 부적합한 문자 sanitize)
                    string instanceName = $"FinalContent_{plan.FinalContentKind}_{plan.SourceMarkerId}_{plan.RequirementId}";
                    instanceName = SanitizeObjectName(instanceName);

                    // 8e. finalPrefab으로 instance 생성
                    GameObject finalPrefab = plan.FinalPrefab;
                    if (finalPrefab == null)
                    {
                        logBuilder.AppendLine($"{LogPrefix} [WARN] FinalPrefab is null for plan '{plan.SourceMarkerId}'. Skipping.");
                        instancesFailed++;
                        continue;
                    }

                    GameObject instance = PrefabUtility.InstantiatePrefab(finalPrefab, categoryRoot.transform) as GameObject;
                    if (instance == null)
                    {
                        logBuilder.AppendLine($"{LogPrefix} [WARN] Failed to instantiate prefab '{finalPrefab.name}' for plan '{plan.SourceMarkerId}'. Skipping.");
                        instancesFailed++;
                        continue;
                    }

                    // 8f. instance 이름 설정
                    instance.name = instanceName;

                    // 8g. transform 설정
                    instance.transform.position = plan.WorldPosition;
                    instance.transform.rotation = plan.WorldRotation;
                    instance.transform.localScale = Vector3.one;

                    // 8h. WorldMapRuntimeFinalContentInstanceTag 부착 후 Configure
                    WorldMapRuntimeFinalContentInstanceTag tag = instance.GetComponent<WorldMapRuntimeFinalContentInstanceTag>();
                    if (tag == null)
                    {
                        tag = instance.AddComponent<WorldMapRuntimeFinalContentInstanceTag>();
                    }
                    tag.Configure(plan, true);

                    Undo.RegisterCreatedObjectUndo(instance, $"Create Final Content Instance '{instanceName}'");

                    instancesCreated++;

                    // 통계 수집
                    string category = plan.RuntimeCategory;
                    if (!string.IsNullOrEmpty(category))
                    {
                        if (!categoryBreakdown.ContainsKey(category))
                            categoryBreakdown[category] = 0;
                        categoryBreakdown[category]++;
                    }

                    WorldMapRuntimeFinalContentKind kind = plan.FinalContentKind;
                    if (!finalContentKindBreakdown.ContainsKey(kind))
                        finalContentKindBreakdown[kind] = 0;
                    finalContentKindBreakdown[kind]++;

                    if (plan.IsGeneratedFinalPlaceholder) generatedPlaceholderCount++;
                    if (plan.IsUserAssignedFinalAsset) userAssignedFinalCount++;
                }
                catch (Exception ex)
                {
                    logBuilder.AppendLine($"{LogPrefix} [ERROR] Exception while creating instance for marker '{plan.SourceMarkerId}': {ex.Message}");
                    instancesFailed++;
                }
            }

            // 9. disableSourceRuntimeSpawnedInstances 옵션 처리
            int disabledSourceCount = 0;
            if (disableSourceRuntimeSpawnedInstances)
            {
                foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
                {
                    if (adapter == null || adapter.gameObject == null) continue;
                    if (adapter.gameObject.activeSelf)
                    {
                        adapter.gameObject.SetActive(false);
                        Undo.RegisterFullObjectHierarchyUndo(adapter.gameObject, "Disable Runtime Spawned Instance");
                        disabledSourceCount++;
                    }
                }
                logBuilder.AppendLine($"{LogPrefix} [INFO] Disabled {disabledSourceCount} source runtime spawned instances.");
            }

            // 10. 결과 출력
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-5: Rebuild Results =====");
            logBuilder.AppendLine($"{LogPrefix} Total adapters: {totalAdapters}");
            logBuilder.AppendLine($"{LogPrefix} Valid plans: {validPlans.Count}");
            logBuilder.AppendLine($"{LogPrefix} Failed plans: {failedPlans}");
            logBuilder.AppendLine($"{LogPrefix} Instances created: {instancesCreated}");
            logBuilder.AppendLine($"{LogPrefix} Instances failed: {instancesFailed}");
            logBuilder.AppendLine($"{LogPrefix} Generated placeholder content count: {generatedPlaceholderCount}");
            logBuilder.AppendLine($"{LogPrefix} User-assigned final content count: {userAssignedFinalCount}");
            logBuilder.AppendLine($"{LogPrefix} Source instances disabled: {disabledSourceCount}");

            // Category breakdown
            logBuilder.AppendLine($"{LogPrefix} Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                logBuilder.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }

            // FinalContentKind breakdown
            logBuilder.AppendLine($"{LogPrefix} FinalContentKind breakdown:");
            foreach (WorldMapRuntimeFinalContentKind kind in Enum.GetValues(typeof(WorldMapRuntimeFinalContentKind)))
            {
                if (kind == WorldMapRuntimeFinalContentKind.None) continue;
                int count = finalContentKindBreakdown.ContainsKey(kind) ? finalContentKindBreakdown[kind] : 0;
                logBuilder.AppendLine($"{LogPrefix}   {kind}: {count}");
            }

            if (instancesFailed > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Rebuild completed with {instancesFailed} failed instance(s).");
                Debug.LogError(logBuilder.ToString());
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] All instances created successfully.");
                Debug.Log(logBuilder.ToString());
            }
        }

        /// <summary>
        /// M-5 Validate: RuntimeFinalContentInstances의 유효성을 검사한다.
        /// 30개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Read-only 검증이며 scene object를 수정하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeFinalContentInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // StringBuilder로 모든 로그를 누적하여 마지막에 한 번만 출력
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-5: Validate Runtime Final Content Instances =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            int totalChecks = 0;

            // 통계 변수
            int totalAdapters = 0;
            int validPlanCount = 0;
            int finalInstanceCount = 0;
            int generatedPlaceholderCount = 0;
            int userAssignedFinalCount = 0;
            int duplicateSourceMarkerIdCount = 0;
            int sourceRuntimeSpawnedCount = 0;
            int runtimePlaceholderCount = 0;
            int previewInstanceCount = 0;

            // Category breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            // FinalContentKind breakdown
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();

            // Snapshot 기반 transform/name unchanged 검증용
            var transformSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();

            // 1. GeneratedWorldRoot exists
            totalChecks++;
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 1: GeneratedWorldRoot not found.");
                failCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 1: GeneratedWorldRoot found: '{generatedRoot.name}'");
                passCount++;
            }

            // 2. RuntimeGameplayAdapterQueryService exists
            totalChecks++;
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot?.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 2: RuntimeGameplayAdapterQueryService not found.");
                failCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 2: RuntimeGameplayAdapterQueryService found. Count: {queryService.Count}");
                passCount++;
            }

            // 3. Requirement DB exists
            totalChecks++;
            WorldMapRuntimeFinalContentRequirementDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeFinalContentRequirementDatabaseSO>(DatabaseAssetPath);
            if (database == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 3: Requirement Database not found at: {DatabaseAssetPath}");
                failCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 3: Requirement Database loaded. Entries: {database.Entries?.Count ?? 0}");
                passCount++;
            }

            // 4. Adapter count > 0
            totalChecks++;
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                totalAdapters = allAdapters.Count;
                if (totalAdapters > 0)
                {
                    logBuilder.AppendLine($"{LogPrefix} [PASS] Check 4: Adapter count: {totalAdapters}");
                    passCount++;
                }
                else
                {
                    logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 4: Adapter count is 0.");
                    failCount++;
                }
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 4: Cannot check adapter count (queryService null).");
                failCount++;
            }

            // 5. Resolution plan count > 0
            totalChecks++;
            if (queryService != null && database != null)
            {
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                int validPlans = 0;
                foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
                {
                    if (adapter == null) continue;
                    bool success = WorldMapRuntimeFinalContentResolver.TryBuildPlan(adapter, database, out WorldMapRuntimeFinalContentResolutionPlan plan);
                    if (success && plan != null && plan.IsValidPlan)
                    {
                        validPlans++;
                    }
                }
                validPlanCount = validPlans;
                if (validPlans > 0)
                {
                    logBuilder.AppendLine($"{LogPrefix} [PASS] Check 5: Valid resolution plans: {validPlans}");
                    passCount++;
                }
                else
                {
                    logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 5: No valid resolution plans found.");
                    failCount++;
                }
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 5: Cannot check resolution plans (queryService or database null).");
                failCount++;
            }

            // 6. RuntimeFinalContentInstances root exists
            totalChecks++;
            bool finalContentRootExists = false;
            if (generatedRoot != null)
            {
                Transform zoneRootsTransform = generatedRoot.transform.Find(settings?.ZoneRootParentName ?? "ZoneRoots");
                if (zoneRootsTransform != null)
                {
                    for (int i = 0; i < zoneRootsTransform.childCount; i++)
                    {
                        Transform zoneRoot = zoneRootsTransform.GetChild(i);
                        if (zoneRoot.Find(FinalContentRootName) != null)
                        {
                            finalContentRootExists = true;
                            break;
                        }
                    }
                }
            }
            if (finalContentRootExists)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 6: RuntimeFinalContentInstances root exists in at least one zone.");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 6: RuntimeFinalContentInstances root not found in any zone.");
                failCount++;
            }

            // 7. Final content instance count == valid plan count
            totalChecks++;
            List<WorldMapRuntimeFinalContentInstanceTag> allFinalInstanceTags = new List<WorldMapRuntimeFinalContentInstanceTag>();
            if (generatedRoot != null)
            {
                allFinalInstanceTags.AddRange(generatedRoot.GetComponentsInChildren<WorldMapRuntimeFinalContentInstanceTag>(true));
            }
            finalInstanceCount = allFinalInstanceTags.Count;
            if (finalInstanceCount == validPlanCount)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 7: Final content instance count ({finalInstanceCount}) == valid plan count ({validPlanCount}).");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 7: Final content instance count ({finalInstanceCount}) != valid plan count ({validPlanCount}).");
                failCount++;
            }

            // 8. Every final instance has WorldMapRuntimeFinalContentInstanceTag
            totalChecks++;
            bool allHaveTag = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag == null)
                {
                    allHaveTag = false;
                    break;
                }
            }
            if (allHaveTag && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 8: All {finalInstanceCount} final instances have WorldMapRuntimeFinalContentInstanceTag.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 8: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 8: Some final instances missing WorldMapRuntimeFinalContentInstanceTag.");
                failCount++;
            }

            // 9. Every final instance tag IsGeneratedFinalInstance == true
            totalChecks++;
            bool allGenerated = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && !tag.IsGeneratedFinalInstance)
                {
                    allGenerated = false;
                    break;
                }
            }
            if (allGenerated && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 9: All {finalInstanceCount} final instance tags have IsGeneratedFinalInstance == true.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 9: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 9: Some final instance tags have IsGeneratedFinalInstance == false.");
                failCount++;
            }

            // 10. Every final instance tag ZoneId non-empty
            totalChecks++;
            bool allZoneIdValid = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.ZoneId))
                {
                    allZoneIdValid = false;
                    break;
                }
            }
            if (allZoneIdValid && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 10: All final instance tags have non-empty ZoneId.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 10: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 10: Some final instance tags have empty ZoneId.");
                failCount++;
            }

            // 11. Every final instance tag SourceMarkerId non-empty
            totalChecks++;
            bool allMarkerIdValid = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.SourceMarkerId))
                {
                    allMarkerIdValid = false;
                    break;
                }
            }
            if (allMarkerIdValid && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 11: All final instance tags have non-empty SourceMarkerId.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 11: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 11: Some final instance tags have empty SourceMarkerId.");
                failCount++;
            }

            // 12. Every final instance tag RequirementId non-empty
            totalChecks++;
            bool allRequirementIdValid = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.RequirementId))
                {
                    allRequirementIdValid = false;
                    break;
                }
            }
            if (allRequirementIdValid && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 12: All final instance tags have non-empty RequirementId.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 12: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 12: Some final instance tags have empty RequirementId.");
                failCount++;
            }

            // 13. Every final instance tag ProfileId non-empty
            totalChecks++;
            bool allProfileIdValid = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.ProfileId))
                {
                    allProfileIdValid = false;
                    break;
                }
            }
            if (allProfileIdValid && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 13: All final instance tags have non-empty ProfileId.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 13: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 13: Some final instance tags have empty ProfileId.");
                failCount++;
            }

            // 14. Every final instance tag FinalContentKind != None
            totalChecks++;
            bool allKindValid = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.FinalContentKind == WorldMapRuntimeFinalContentKind.None)
                {
                    allKindValid = false;
                    break;
                }
            }
            if (allKindValid && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 14: All final instance tags have FinalContentKind != None.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 14: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 14: Some final instance tags have FinalContentKind == None.");
                failCount++;
            }

            // 15. Every final instance has no WorldMapRuntimeSpawnInstanceTag
            totalChecks++;
            bool noSpawnInstanceTag = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.GetComponent<WorldMapRuntimeSpawnInstanceTag>() != null)
                {
                    noSpawnInstanceTag = false;
                    break;
                }
            }
            if (noSpawnInstanceTag && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 15: No final instance has WorldMapRuntimeSpawnInstanceTag.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 15: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 15: Some final instance has WorldMapRuntimeSpawnInstanceTag.");
                failCount++;
            }

            // 16. Every final instance has no WorldMapRuntimeSpawnGameplayAdapter
            totalChecks++;
            bool noSpawnAdapter = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.GetComponent<WorldMapRuntimeSpawnGameplayAdapter>() != null)
                {
                    noSpawnAdapter = false;
                    break;
                }
            }
            if (noSpawnAdapter && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 16: No final instance has WorldMapRuntimeSpawnGameplayAdapter.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 16: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 16: Some final instance has WorldMapRuntimeSpawnGameplayAdapter.");
                failCount++;
            }

            // 17. Every final instance has no WorldMapRuntimeFinalContentContract
            totalChecks++;
            bool noFinalContentContract = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.GetComponent<WorldMapRuntimeFinalContentContract>() != null)
                {
                    noFinalContentContract = false;
                    break;
                }
            }
            if (noFinalContentContract && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 17: No final instance has WorldMapRuntimeFinalContentContract.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 17: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 17: Some final instance has WorldMapRuntimeFinalContentContract.");
                failCount++;
            }

            // 18. Every final instance has no WorldMapRuntimePlaceholderBinding
            totalChecks++;
            bool noPlaceholderBinding = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                {
                    noPlaceholderBinding = false;
                    break;
                }
            }
            if (noPlaceholderBinding && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 18: No final instance has WorldMapRuntimePlaceholderBinding.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 18: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 18: Some final instance has WorldMapRuntimePlaceholderBinding.");
                failCount++;
            }

            // 19. Every final instance has no WorldMapZoneContentMarker
            totalChecks++;
            bool noZoneContentMarker = true;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.GetComponent<WorldMapZoneContentMarker>() != null)
                {
                    noZoneContentMarker = false;
                    break;
                }
            }
            if (noZoneContentMarker && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 19: No final instance has WorldMapZoneContentMarker.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 19: No final instances to check.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 19: Some final instance has WorldMapZoneContentMarker.");
                failCount++;
            }

            // 20. Every source RuntimeSpawnedInstances object still exists
            totalChecks++;
            bool sourceObjectsExist = true;
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
                {
                    if (adapter == null || adapter.gameObject == null)
                    {
                        sourceObjectsExist = false;
                        break;
                    }
                }
            }
            if (sourceObjectsExist && totalAdapters > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 20: All {totalAdapters} source RuntimeSpawnedInstances objects still exist.");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 20: Some source RuntimeSpawnedInstances objects are missing.");
                failCount++;
            }

            // 21. RuntimeSpawnedInstances source count unchanged
            totalChecks++;
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                sourceRuntimeSpawnedCount = allAdapters.Count;
                logBuilder.AppendLine($"{LogPrefix} [INFO] Check 21: RuntimeSpawnedInstances source count: {sourceRuntimeSpawnedCount}");
                infoCount++;
            }

            // 22. RuntimePlaceholder count unchanged (approximate check via RuntimePlaceholderBinding)
            totalChecks++;
            if (generatedRoot != null)
            {
                WorldMapRuntimePlaceholderBinding[] allPlaceholderBindings = generatedRoot.GetComponentsInChildren<WorldMapRuntimePlaceholderBinding>(true);
                runtimePlaceholderCount = allPlaceholderBindings.Length;
                logBuilder.AppendLine($"{LogPrefix} [INFO] Check 22: RuntimePlaceholder count (via binding): {runtimePlaceholderCount}");
                infoCount++;
            }

            // 23. Preview instance count unchanged
            totalChecks++;
            if (generatedRoot != null)
            {
                WorldMapRuntimeSpawnInstanceTag[] allSpawnTags = generatedRoot.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true);
                int previewCount = 0;
                foreach (WorldMapRuntimeSpawnInstanceTag tag in allSpawnTags)
                {
                    if (tag != null && tag.IsPreviewInstance)
                        previewCount++;
                }
                previewInstanceCount = previewCount;
                logBuilder.AppendLine($"{LogPrefix} [INFO] Check 23: Preview instance count: {previewInstanceCount}");
                infoCount++;
            }

            // 24. Final instance positions match source plan positions within 0.1 unit
            totalChecks++;
            bool positionsMatch = true;
            int positionMismatchCount = 0;
            if (queryService != null && database != null)
            {
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
                {
                    if (adapter == null) continue;
                    bool success = WorldMapRuntimeFinalContentResolver.TryBuildPlan(adapter, database, out WorldMapRuntimeFinalContentResolutionPlan plan);
                    if (!success || plan == null || !plan.IsValidPlan) continue;

                    // Find matching final instance
                    foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
                    {
                        if (tag == null) continue;
                        if (tag.SourceMarkerId == plan.SourceMarkerId && tag.RequirementId == plan.RequirementId)
                        {
                            float distance = Vector3.Distance(tag.transform.position, plan.WorldPosition);
                            if (distance > 0.1f)
                            {
                                positionsMatch = false;
                                positionMismatchCount++;
                            }
                            break;
                        }
                    }
                }
            }
            if (positionsMatch && finalInstanceCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 24: All final instance positions match source plan positions within 0.1 unit.");
                passCount++;
            }
            else if (finalInstanceCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Check 24: No final instances to check positions.");
                warnCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 24: {positionMismatchCount} final instance(s) have position mismatch > 0.1 unit.");
                failCount++;
            }

            // 25. Scene source object transform/name unchanged (snapshot-based check)
            totalChecks++;
            bool sourceTransformUnchanged = true;
            string sourceTransformDiff = string.Empty;
            if (queryService != null)
            {
                IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
                {
                    if (adapter == null || adapter.gameObject == null) continue;
                    CaptureTransformSnapshots(adapter.transform, transformSnapshots);
                }

                // Verify snapshots
                sourceTransformUnchanged = VerifyTransformSnapshots(transformSnapshots, out sourceTransformDiff);
            }
            if (sourceTransformUnchanged)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 25: Source object transform/name unchanged.");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 25: Source object transform/name changed:\n{sourceTransformDiff}");
                failCount++;
            }

            // 26. Duplicate final instance SourceMarkerId count == 0
            totalChecks++;
            var markerIdSet = new HashSet<string>();
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && !string.IsNullOrEmpty(tag.SourceMarkerId))
                {
                    if (!markerIdSet.Add(tag.SourceMarkerId))
                    {
                        duplicateSourceMarkerIdCount++;
                    }
                }
            }
            if (duplicateSourceMarkerIdCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 26: Duplicate SourceMarkerId count: 0.");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 26: Duplicate SourceMarkerId count: {duplicateSourceMarkerIdCount}.");
                failCount++;
            }

            // 27. Category breakdown
            totalChecks++;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag == null) continue;
                string category = tag.RuntimeCategory;
                if (!string.IsNullOrEmpty(category))
                {
                    if (!categoryBreakdown.ContainsKey(category))
                        categoryBreakdown[category] = 0;
                    categoryBreakdown[category]++;
                }
            }
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 27: Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                logBuilder.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 28. FinalContentKind breakdown
            totalChecks++;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag == null) continue;
                WorldMapRuntimeFinalContentKind kind = tag.FinalContentKind;
                if (!finalContentKindBreakdown.ContainsKey(kind))
                    finalContentKindBreakdown[kind] = 0;
                finalContentKindBreakdown[kind]++;
            }
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 28: FinalContentKind breakdown:");
            foreach (WorldMapRuntimeFinalContentKind kind in Enum.GetValues(typeof(WorldMapRuntimeFinalContentKind)))
            {
                if (kind == WorldMapRuntimeFinalContentKind.None) continue;
                int count = finalContentKindBreakdown.ContainsKey(kind) ? finalContentKindBreakdown[kind] : 0;
                logBuilder.AppendLine($"{LogPrefix}   {kind}: {count}");
            }
            infoCount++;

            // 29. Generated placeholder content count
            totalChecks++;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.IsGeneratedPlaceholderContent)
                    generatedPlaceholderCount++;
            }
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 29: Generated placeholder content count: {generatedPlaceholderCount}");
            infoCount++;

            // 30. User-assigned final content count
            totalChecks++;
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allFinalInstanceTags)
            {
                if (tag != null && tag.IsUserAssignedFinalContent)
                    userAssignedFinalCount++;
            }
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 30: User-assigned final content count: {userAssignedFinalCount}");
            infoCount++;

            // ===== Final Summary =====
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-5: Validate Summary =====");
            logBuilder.AppendLine($"{LogPrefix} Total checks: {totalChecks}");
            logBuilder.AppendLine($"{LogPrefix} [PASS] {passCount}");
            logBuilder.AppendLine($"{LogPrefix} [FAIL] {failCount}");
            logBuilder.AppendLine($"{LogPrefix} [WARN] {warnCount}");
            logBuilder.AppendLine($"{LogPrefix} [INFO] {infoCount}");
            logBuilder.AppendLine($"{LogPrefix} Final content instances: {finalInstanceCount}");
            logBuilder.AppendLine($"{LogPrefix} Source runtime spawned instances: {sourceRuntimeSpawnedCount}");
            logBuilder.AppendLine($"{LogPrefix} RuntimePlaceholder count: {runtimePlaceholderCount}");
            logBuilder.AppendLine($"{LogPrefix} Preview instances: {previewInstanceCount}");
            logBuilder.AppendLine($"{LogPrefix} Duplicate SourceMarkerId: {duplicateSourceMarkerIdCount}");
            logBuilder.AppendLine($"{LogPrefix} Generated placeholder content: {generatedPlaceholderCount}");
            logBuilder.AppendLine($"{LogPrefix} User-assigned final content: {userAssignedFinalCount}");

            // Console 출력 (DisplayDialog 대체 - Console 로그 기반 요약)
            string summaryMessage =
                $"Total checks: {totalChecks}\n" +
                $"[PASS] {passCount}\n" +
                $"[FAIL] {failCount}\n" +
                $"[WARN] {warnCount}\n" +
                $"[INFO] {infoCount}\n\n" +
                $"Final content instances: {finalInstanceCount}\n" +
                $"Source runtime spawned instances: {sourceRuntimeSpawnedCount}\n" +
                $"RuntimePlaceholder count: {runtimePlaceholderCount}\n" +
                $"Preview instances: {previewInstanceCount}\n" +
                $"Duplicate SourceMarkerId: {duplicateSourceMarkerIdCount}\n" +
                $"Generated placeholder content: {generatedPlaceholderCount}\n" +
                $"User-assigned final content: {userAssignedFinalCount}";

            if (failCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Validation completed with {failCount} failure(s).");
                Debug.LogError(logBuilder.ToString());
                Debug.LogError($"[Phase 14.10-M-5] Validation Summary\nPASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount}\n{summaryMessage}");
            }
            else if (warnCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [WARN] Validation completed with {warnCount} warning(s).");
                Debug.LogWarning(logBuilder.ToString());
                Debug.LogWarning($"[Phase 14.10-M-5] Validation Summary\nPASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount}\n{summaryMessage}");
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] All checks passed.");
                Debug.Log(logBuilder.ToString());
                Debug.Log($"[Phase 14.10-M-5] Validation Summary\nPASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount}\n{summaryMessage}");
            }
        }

        /// <summary>
        /// M-5 Clear: RuntimeFinalContentInstances 하위의 generated final content instance를 모두 삭제한다.
        /// 삭제 대상: WorldMapRuntimeFinalContentInstanceTag가 있고 IsGeneratedFinalInstance == true인 object만 삭제.
        /// RuntimeSpawnedInstances 원본 / RuntimePlaceholder / Marker / DebugVisual / Binding object는 절대 삭제하지 않는다.
        /// reactivateSourceRuntimeSpawnedInstances == true이면 비활성화된 source instance를 다시 활성화한다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        /// <param name="reactivateSourceRuntimeSpawnedInstances">true이면 비활성화된 source instance를 다시 SetActive(true) 처리</param>
        public static void ClearRuntimeFinalContentInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context,
            bool reactivateSourceRuntimeSpawnedInstances)
        {
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-5: Clear Runtime Final Content Instances =====");
            logBuilder.AppendLine($"{LogPrefix} Reactivate source runtime spawned instances: {reactivateSourceRuntimeSpawnedInstances}");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Nothing to clear.");
                Debug.LogError(logBuilder.ToString());
                return;
            }

            // 2. Clear internal (generated final instances only)
            ClearRuntimeFinalContentInstancesInternal(generatedRoot, logBuilder, reactivateSourceRuntimeSpawnedInstances);

            // 3. 결과 출력
            Debug.Log(logBuilder.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// RuntimeFinalContentInstances 하위의 generated final content instance를 정리하는 내부 메서드.
        /// Rebuild와 Clear 양쪽에서 호출된다.
        /// </summary>
        private static void ClearRuntimeFinalContentInstancesInternal(
            GameObject generatedRoot,
            StringBuilder logBuilder,
            bool reactivateSourceRuntimeSpawnedInstances)
        {
            if (generatedRoot == null) return;

            int deletedCount = 0;
            int emptyRootCleanedCount = 0;
            int reactivatedCount = 0;

            // 1. 모든 WorldMapRuntimeFinalContentInstanceTag 수집
            WorldMapRuntimeFinalContentInstanceTag[] allTags = generatedRoot.GetComponentsInChildren<WorldMapRuntimeFinalContentInstanceTag>(true);

            // 2. IsGeneratedFinalInstance == true인 object만 삭제
            List<GameObject> toDelete = new List<GameObject>();
            foreach (WorldMapRuntimeFinalContentInstanceTag tag in allTags)
            {
                if (tag != null && tag.IsGeneratedFinalInstance)
                {
                    toDelete.Add(tag.gameObject);
                }
            }

            foreach (GameObject obj in toDelete)
            {
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                    deletedCount++;
                }
            }

            logBuilder.AppendLine($"{LogPrefix} [INFO] Deleted {deletedCount} generated final content instance(s).");

            // 3. 비어있는 category root 정리
            if (generatedRoot != null)
            {
                Transform zoneRootsTransform = generatedRoot.transform.Find("ZoneRoots");
                if (zoneRootsTransform != null)
                {
                    for (int i = zoneRootsTransform.childCount - 1; i >= 0; i--)
                    {
                        Transform zoneRoot = zoneRootsTransform.GetChild(i);
                        Transform finalContentRoot = zoneRoot.Find(FinalContentRootName);
                        if (finalContentRoot == null) continue;

                        // category root 정리 (하위에 child가 없는 category root 삭제)
                        for (int j = finalContentRoot.childCount - 1; j >= 0; j--)
                        {
                            Transform categoryRoot = finalContentRoot.GetChild(j);
                            if (categoryRoot.childCount == 0)
                            {
                                Undo.DestroyObjectImmediate(categoryRoot.gameObject);
                                emptyRootCleanedCount++;
                            }
                        }

                        // RuntimeFinalContentInstances root 정리 (하위에 child가 없으면 삭제)
                        if (finalContentRoot.childCount == 0)
                        {
                            Undo.DestroyObjectImmediate(finalContentRoot.gameObject);
                            emptyRootCleanedCount++;
                        }
                    }
                }
            }

            logBuilder.AppendLine($"{LogPrefix} [INFO] Cleaned up {emptyRootCleanedCount} empty root(s).");

            // 4. reactivateSourceRuntimeSpawnedInstances 옵션 처리
            if (reactivateSourceRuntimeSpawnedInstances)
            {
                WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
                if (queryService != null)
                {
                    IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                    foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
                    {
                        if (adapter == null || adapter.gameObject == null) continue;
                        if (!adapter.gameObject.activeSelf)
                        {
                            adapter.gameObject.SetActive(true);
                            Undo.RegisterFullObjectHierarchyUndo(adapter.gameObject, "Reactivate Runtime Spawned Instance");
                            reactivatedCount++;
                        }
                    }
                }
                logBuilder.AppendLine($"{LogPrefix} [INFO] Reactivated {reactivatedCount} source runtime spawned instance(s).");
            }
        }

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
        /// FinalContentKind에 대응하는 category root 이름을 반환한다.
        /// </summary>
        private static string GetCategoryRootName(WorldMapRuntimeFinalContentKind kind)
        {
            switch (kind)
            {
                case WorldMapRuntimeFinalContentKind.HarvestResource:
                    return "HarvestResource";
                case WorldMapRuntimeFinalContentKind.HazardVolume:
                    return "HazardVolume";
                case WorldMapRuntimeFinalContentKind.LandmarkProp:
                    return "LandmarkProp";
                case WorldMapRuntimeFinalContentKind.NarrativeLog:
                    return "NarrativeLog";
                case WorldMapRuntimeFinalContentKind.RouteTrigger:
                    return "RouteTrigger";
                case WorldMapRuntimeFinalContentKind.DebugMarker:
                    return "DebugMarker";
                case WorldMapRuntimeFinalContentKind.Unknown:
                    return "Unknown";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// 오브젝트 이름에 부적합한 문자를 제거한다.
        /// </summary>
        private static string SanitizeObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c.ToString(), "_");
            }

            // 추가로 Unity 오브젝트 이름에 문제가 될 수 있는 문자 처리
            name = name.Replace("/", "_");
            name = name.Replace("\\", "_");
            name = name.Replace(":", "_");
            name = name.Replace("*", "_");
            name = name.Replace("?", "_");
            name = name.Replace("\"", "_");
            name = name.Replace("<", "_");
            name = name.Replace(">", "_");
            name = name.Replace("|", "_");

            return name;
        }

        /// <summary>
        /// Transform 계층 구조의 position/rotation/scale/name snapshot을 수집한다.
        /// Validate 전후 변경 감지용.
        /// </summary>
        private static void CaptureTransformSnapshots(Transform root, Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)> snapshots)
        {
            if (root == null)
                return;

            snapshots[root] = (root.position, root.rotation, root.localScale, root.name);

            for (int i = 0; i < root.childCount; i++)
            {
                CaptureTransformSnapshots(root.GetChild(i), snapshots);
            }
        }

        /// <summary>
        /// Snapshot과 현재 transform을 비교하여 변경 여부를 반환한다.
        /// </summary>
        private static bool VerifyTransformSnapshots(
            Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)> snapshots,
            out string diff)
        {
            diff = string.Empty;
            var diffs = new List<string>();

            foreach (var kvp in snapshots)
            {
                Transform t = kvp.Key;
                if (t == null) continue;

                var (pos, rot, scale, name) = kvp.Value;

                if (t.position != pos)
                    diffs.Add($"  Position changed: '{name}' ({t.position} != {pos})");
                if (t.rotation != rot)
                    diffs.Add($"  Rotation changed: '{name}' ({t.rotation.eulerAngles} != {rot.eulerAngles})");
                if (t.localScale != scale)
                    diffs.Add($"  Scale changed: '{name}' ({t.localScale} != {scale})");
                if (t.name != name)
                    diffs.Add($"  Name changed: '{t.name}' != '{name}'");
            }

            if (diffs.Count > 0)
            {
                diff = string.Join("\n", diffs);
                return false;
            }

            return true;
        }
    }
}
