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
    /// Phase 14.10-M-4: Runtime Final Content Resolution Plan 전용 Editor Utility.
    /// M-1 Contract + M-2 Requirement DB + M-3 finalPrefab/finalProfile binding을 사용해서,
    /// 각 RuntimeSpawnedInstance가 어떤 final content prefab/profile로 교체될 수 있는지
    /// "최종 해석 계획"을 생성하고 검증한다.
    /// 이 단계에서는 scene instance를 실제로 교체하지 않으며, read-only plan build + validation만 수행한다.
    /// </summary>
    public static class DeepLightMapRuntimeFinalContentResolutionPlanUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[M-4 ResolutionPlan]";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeFinalContentRequirementDatabase.asset";

        // ===== Public API =====

        /// <summary>
        /// M-4 Rebuild: 모든 adapter에 대해 resolution plan을 생성하고 summary를 출력한다.
        /// 실제 plan asset 저장은 하지 않으며, read-only로 369개 plan을 생성 가능한지 검사한다.
        /// Scene object를 생성/삭제/이동/name 변경하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeFinalContentResolutionPlans(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // StringBuilder로 모든 로그를 누적하여 마지막에 한 번만 출력
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-4: Rebuild Runtime Final Content Resolution Plans =====");

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
            int plansBuilt = 0;
            int failedPlans = 0;
            int generatedFinalPlaceholderCount = 0;
            int userFinalAssetCount = 0;
            int fallbackProfileCount = 0;
            int fallbackRequirementCount = 0;

            // Category breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            // FinalContentKind breakdown
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            // 실패 plan 목록 (sample 출력용)
            var failedPlanSummaries = new List<string>();
            // 성공 plan 목록 (sample 출력용)
            var successPlanSummaries = new List<string>();

            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;

                bool success = WorldMapRuntimeFinalContentResolver.TryBuildPlan(adapter, database, out WorldMapRuntimeFinalContentResolutionPlan plan);

                if (success && plan != null && plan.IsValidPlan)
                {
                    plansBuilt++;

                    // 통계 수집
                    if (plan.IsGeneratedFinalPlaceholder) generatedFinalPlaceholderCount++;
                    if (plan.IsUserAssignedFinalAsset) userFinalAssetCount++;
                    if (plan.IsFallbackProfile) fallbackProfileCount++;
                    if (plan.IsFallbackRequirement) fallbackRequirementCount++;

                    // Category breakdown
                    string category = plan.RuntimeCategory;
                    if (!string.IsNullOrEmpty(category))
                    {
                        if (!categoryBreakdown.ContainsKey(category))
                            categoryBreakdown[category] = 0;
                        categoryBreakdown[category]++;
                    }

                    // FinalContentKind breakdown
                    WorldMapRuntimeFinalContentKind kind = plan.FinalContentKind;
                    if (!finalContentKindBreakdown.ContainsKey(kind))
                        finalContentKindBreakdown[kind] = 0;
                    finalContentKindBreakdown[kind]++;

                    // 성공 plan sample 저장 (최대 5개)
                    if (successPlanSummaries.Count < 5)
                    {
                        successPlanSummaries.Add(plan.GetSummary());
                    }
                }
                else
                {
                    failedPlans++;
                    if (plan != null)
                    {
                        string summary = plan.GetSummary();
                        if (failedPlanSummaries.Count < 5)
                        {
                            failedPlanSummaries.Add(summary);
                        }
                    }
                }
            }

            // 6. 결과 출력
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-4: Rebuild Results =====");
            logBuilder.AppendLine($"{LogPrefix} Total adapters: {totalAdapters}");
            logBuilder.AppendLine($"{LogPrefix} Plans built successfully: {plansBuilt}");
            logBuilder.AppendLine($"{LogPrefix} Failed plans: {failedPlans}");
            logBuilder.AppendLine($"{LogPrefix} Generated final placeholder count: {generatedFinalPlaceholderCount}");
            logBuilder.AppendLine($"{LogPrefix} User-assigned final asset count: {userFinalAssetCount}");
            logBuilder.AppendLine($"{LogPrefix} Fallback profile count: {fallbackProfileCount}");
            logBuilder.AppendLine($"{LogPrefix} Fallback requirement count: {fallbackRequirementCount}");

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

            // Sample plans
            logBuilder.AppendLine($"{LogPrefix} ===== Sample Successful Plans (up to 5) =====");
            foreach (string sample in successPlanSummaries)
            {
                logBuilder.AppendLine($"{LogPrefix}   {sample}");
            }

            if (failedPlanSummaries.Count > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} ===== Sample Failed Plans (up to 5) =====");
                foreach (string sample in failedPlanSummaries)
                {
                    logBuilder.AppendLine($"{LogPrefix}   {sample}");
                }
            }

            // 최종 요약
            if (failedPlans > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Rebuild completed with {failedPlans} failed plan(s).");
                Debug.LogError(logBuilder.ToString());
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] All plans built successfully.");
                Debug.Log(logBuilder.ToString());
            }
        }

        /// <summary>
        /// M-4 Validate: 생성된 resolution plan의 유효성을 검사한다.
        /// 최소 25개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Scene object를 생성/삭제/이동/name 변경하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeFinalContentResolutionPlans(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // StringBuilder로 모든 로그를 누적하여 마지막에 한 번만 출력
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-4: Validate Runtime Final Content Resolution Plans =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            int totalChecks = 0;

            // 통계 변수
            int totalAdapters = 0;
            int plansBuilt = 0;
            int failedPlans = 0;
            int generatedFinalPlaceholderCount = 0;
            int userFinalAssetCount = 0;
            int fallbackProfileCount = 0;
            int fallbackRequirementCount = 0;
            int contractsMissingCount = 0;
            int requirementMatchFailedCount = 0;
            int finalPrefabNullCount = 0;
            int finalProfileNullCount = 0;
            int finalContentKindNoneCount = 0;
            int emptySourceInstanceNameCount = 0;
            int emptySourceMarkerIdCount = 0;
            int emptyZoneIdCount = 0;
            int emptyProfileIdCount = 0;
            int emptyRequirementIdCount = 0;

            // Category breakdown
            var categoryBreakdown = new Dictionary<string, int>();
            // FinalContentKind breakdown
            var finalContentKindBreakdown = new Dictionary<WorldMapRuntimeFinalContentKind, int>();

            // Snapshot 기반 transform/name unchanged 검증용
            var transformSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();

            // 1. GeneratedWorldRoot exists
            totalChecks++;
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot != null)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 1: GeneratedWorldRoot exists: '{generatedRoot.name}'");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 1: GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                failCount++;
                // Root가 없으면 더 이상 검사 불가
                LogFinalSummary(logBuilder, passCount, failCount, warnCount, infoCount, totalChecks);
                return;
            }

            // 2. QueryService exists
            totalChecks++;
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService != null)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 2: QueryService exists on GeneratedWorldRoot");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 2: QueryService not found on GeneratedWorldRoot");
                failCount++;
                LogFinalSummary(logBuilder, passCount, failCount, warnCount, infoCount, totalChecks);
                return;
            }

            // 3. Requirement Database asset exists
            totalChecks++;
            WorldMapRuntimeFinalContentRequirementDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeFinalContentRequirementDatabaseSO>(DatabaseAssetPath);
            if (database != null)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 3: Requirement Database asset exists at {DatabaseAssetPath}");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 3: Requirement Database asset NOT found at {DatabaseAssetPath}");
                failCount++;
                LogFinalSummary(logBuilder, passCount, failCount, warnCount, infoCount, totalChecks);
                return;
            }

            // 4. Adapter count > 0
            totalChecks++;
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
            totalAdapters = allAdapters.Count;
            if (totalAdapters > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 4: Adapter count = {totalAdapters} (> 0)");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 4: Adapter count = 0 (expected > 0)");
                failCount++;
                LogFinalSummary(logBuilder, passCount, failCount, warnCount, infoCount, totalChecks);
                return;
            }

            // Snapshot: 검사 전 transform/name 기록
            CaptureTransformSnapshots(generatedRoot.transform, transformSnapshots);

            // 5-25: 각 adapter별 plan 생성 및 검사
            var successPlanSummaries = new List<string>();
            var failedPlanSummaries = new List<string>();

            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null) continue;

                // 5. All adapters have WorldMapRuntimeFinalContentContract
                WorldMapRuntimeFinalContentContract contract = adapter.GetComponent<WorldMapRuntimeFinalContentContract>();
                if (contract == null)
                {
                    contractsMissingCount++;
                }

                // Plan 생성
                bool success = WorldMapRuntimeFinalContentResolver.TryBuildPlan(adapter, database, out WorldMapRuntimeFinalContentResolutionPlan plan);

                if (success && plan != null && plan.IsValidPlan)
                {
                    plansBuilt++;

                    // 통계 수집
                    if (plan.IsGeneratedFinalPlaceholder) generatedFinalPlaceholderCount++;
                    if (plan.IsUserAssignedFinalAsset) userFinalAssetCount++;
                    if (plan.IsFallbackProfile) fallbackProfileCount++;
                    if (plan.IsFallbackRequirement) fallbackRequirementCount++;

                    // Category breakdown
                    string category = plan.RuntimeCategory;
                    if (!string.IsNullOrEmpty(category))
                    {
                        if (!categoryBreakdown.ContainsKey(category))
                            categoryBreakdown[category] = 0;
                        categoryBreakdown[category]++;
                    }

                    // FinalContentKind breakdown
                    WorldMapRuntimeFinalContentKind kind = plan.FinalContentKind;
                    if (!finalContentKindBreakdown.ContainsKey(kind))
                        finalContentKindBreakdown[kind] = 0;
                    finalContentKindBreakdown[kind]++;

                    // 개별 plan 검사
                    if (string.IsNullOrEmpty(plan.SourceInstanceName)) emptySourceInstanceNameCount++;
                    if (string.IsNullOrEmpty(plan.SourceMarkerId)) emptySourceMarkerIdCount++;
                    if (string.IsNullOrEmpty(plan.ZoneId)) emptyZoneIdCount++;
                    if (string.IsNullOrEmpty(plan.ProfileId)) emptyProfileIdCount++;
                    if (string.IsNullOrEmpty(plan.RequirementId)) emptyRequirementIdCount++;
                    if (plan.FinalPrefab == null) finalPrefabNullCount++;
                    if (plan.FinalProfile == null) finalProfileNullCount++;
                    if (plan.FinalContentKind == WorldMapRuntimeFinalContentKind.None) finalContentKindNoneCount++;

                    // 성공 plan sample 저장 (최대 5개)
                    if (successPlanSummaries.Count < 5)
                    {
                        successPlanSummaries.Add(plan.GetSummary());
                    }
                }
                else
                {
                    failedPlans++;
                    requirementMatchFailedCount++;
                    if (plan != null)
                    {
                        if (plan.FinalPrefab == null) finalPrefabNullCount++;
                        if (plan.FinalProfile == null) finalProfileNullCount++;
                        if (failedPlanSummaries.Count < 5)
                        {
                            failedPlanSummaries.Add(plan.GetSummary());
                        }
                    }
                }
            }

            // Snapshot 검증: transform/name unchanged
            bool transformUnchanged = VerifyTransformSnapshots(transformSnapshots, out string transformDiff);

            // ===== Check Results =====

            // Check 5: All adapters have WorldMapRuntimeFinalContentContract
            totalChecks++;
            if (contractsMissingCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 5: All {totalAdapters} adapters have WorldMapRuntimeFinalContentContract");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 5: {contractsMissingCount} adapter(s) missing WorldMapRuntimeFinalContentContract");
                failCount++;
            }

            // Check 6: All adapters matched to requirement
            totalChecks++;
            if (requirementMatchFailedCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 6: All {totalAdapters} adapters matched to requirement");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 6: {requirementMatchFailedCount} adapter(s) failed to match requirement");
                failCount++;
            }

            // Check 7: All plans build successfully
            totalChecks++;
            if (failedPlans == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 7: All {totalAdapters} plans built successfully");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 7: {failedPlans} plan(s) failed to build");
                failCount++;
            }

            // Check 8: All plans have non-empty sourceInstanceName
            totalChecks++;
            if (emptySourceInstanceNameCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 8: All plans have non-empty sourceInstanceName");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 8: {emptySourceInstanceNameCount} plan(s) have empty sourceInstanceName");
                failCount++;
            }

            // Check 9: All plans have non-empty sourceMarkerId
            totalChecks++;
            if (emptySourceMarkerIdCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 9: All plans have non-empty sourceMarkerId");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 9: {emptySourceMarkerIdCount} plan(s) have empty sourceMarkerId");
                failCount++;
            }

            // Check 10: All plans have non-empty zoneId
            totalChecks++;
            if (emptyZoneIdCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 10: All plans have non-empty zoneId");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 10: {emptyZoneIdCount} plan(s) have empty zoneId");
                failCount++;
            }

            // Check 11: All plans have non-empty profileId
            totalChecks++;
            if (emptyProfileIdCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 11: All plans have non-empty profileId");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 11: {emptyProfileIdCount} plan(s) have empty profileId");
                failCount++;
            }

            // Check 12: All plans have non-empty requirementId
            totalChecks++;
            if (emptyRequirementIdCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 12: All plans have non-empty requirementId");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 12: {emptyRequirementIdCount} plan(s) have empty requirementId");
                failCount++;
            }

            // Check 13: All plans have finalPrefab non-null
            totalChecks++;
            if (finalPrefabNullCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 13: All plans have non-null finalPrefab");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 13: {finalPrefabNullCount} plan(s) have null finalPrefab");
                failCount++;
            }

            // Check 14: All plans have finalProfile non-null
            totalChecks++;
            if (finalProfileNullCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 14: All plans have non-null finalProfile");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 14: {finalProfileNullCount} plan(s) have null finalProfile");
                failCount++;
            }

            // Check 15: All plans have finalContentKind != None
            totalChecks++;
            if (finalContentKindNoneCount == 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 15: All plans have finalContentKind != None");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 15: {finalContentKindNoneCount} plan(s) have finalContentKind == None");
                failCount++;
            }

            // Check 16: Generated final placeholder count (INFO)
            totalChecks++;
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 16: Generated final placeholder count: {generatedFinalPlaceholderCount}");
            infoCount++;

            // Check 17: User-assigned final asset count (INFO)
            totalChecks++;
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 17: User-assigned final asset count: {userFinalAssetCount}");
            infoCount++;

            // Check 18: Fallback profile count (INFO)
            totalChecks++;
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 18: Fallback profile count: {fallbackProfileCount}");
            infoCount++;

            // Check 19: Fallback requirement count (INFO)
            totalChecks++;
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 19: Fallback requirement count: {fallbackRequirementCount}");
            infoCount++;

            // Check 20: Category breakdown (INFO)
            totalChecks++;
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 20: Category breakdown:");
            foreach (var kvp in categoryBreakdown)
            {
                logBuilder.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // Check 21: FinalContentKind breakdown (INFO)
            totalChecks++;
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 21: FinalContentKind breakdown:");
            foreach (WorldMapRuntimeFinalContentKind kind in Enum.GetValues(typeof(WorldMapRuntimeFinalContentKind)))
            {
                if (kind == WorldMapRuntimeFinalContentKind.None) continue;
                int count = finalContentKindBreakdown.ContainsKey(kind) ? finalContentKindBreakdown[kind] : 0;
                logBuilder.AppendLine($"{LogPrefix}   {kind}: {count}");
            }
            infoCount++;

            // Check 22: Scene object transform/name unchanged
            totalChecks++;
            if (transformUnchanged)
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] Check 22: Scene object transform/name unchanged (snapshot verified)");
                passCount++;
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Check 22: Scene object transform/name changed! Details:");
                logBuilder.AppendLine($"{LogPrefix}   {transformDiff}");
                failCount++;
            }

            // Check 23: Preview instances are not included (INFO)
            totalChecks++;
            int previewCount = 0;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter != null && adapter.gameObject != null)
                {
                    // Preview instance는 이름에 "Preview" 또는 "preview"가 포함되어 있음
                    if (adapter.gameObject.name.Contains("Preview", StringComparison.OrdinalIgnoreCase))
                    {
                        previewCount++;
                    }
                }
            }
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 23: Preview instances included in adapters: {previewCount} (expected 0 for final content resolution)");
            infoCount++;

            // Check 24: RuntimePlaceholder originals are not included (INFO)
            totalChecks++;
            int runtimePlaceholderCount = 0;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter != null && adapter.gameObject != null)
                {
                    // RuntimePlaceholder original은 SourcePlaceholderName이 비어있거나 특정 패턴
                    if (string.IsNullOrEmpty(adapter.SourcePlaceholderName))
                    {
                        runtimePlaceholderCount++;
                    }
                }
            }
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 24: Adapters with empty SourcePlaceholderName: {runtimePlaceholderCount} (expected 0 for spawned instances)");
            infoCount++;

            // Check 25: WorldMapZoneContentMarker objects are not included (INFO)
            totalChecks++;
            int markerObjectCount = 0;
            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter != null && adapter.gameObject != null)
                {
                    // Marker object는 이름에 "Marker" 또는 "marker"가 포함되어 있음
                    if (adapter.gameObject.name.Contains("Marker", StringComparison.OrdinalIgnoreCase))
                    {
                        markerObjectCount++;
                    }
                }
            }
            logBuilder.AppendLine($"{LogPrefix} [INFO] Check 25: Adapters with 'Marker' in name: {markerObjectCount} (expected 0 for spawned instances)");
            infoCount++;

            // Sample plans 출력
            logBuilder.AppendLine($"{LogPrefix} ===== Sample Successful Plans (up to 5) =====");
            foreach (string sample in successPlanSummaries)
            {
                logBuilder.AppendLine($"{LogPrefix}   {sample}");
            }

            if (failedPlanSummaries.Count > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} ===== Sample Failed Plans (up to 5) =====");
                foreach (string sample in failedPlanSummaries)
                {
                    logBuilder.AppendLine($"{LogPrefix}   {sample}");
                }
            }

            // 최종 요약
            LogFinalSummary(logBuilder, passCount, failCount, warnCount, infoCount, totalChecks);
        }

        // ===== Private Helpers =====

        /// <summary>
        /// 최종 요약 로그를 출력한다.
        /// </summary>
        private static void LogFinalSummary(StringBuilder logBuilder, int passCount, int failCount, int warnCount, int infoCount, int totalChecks)
        {
            logBuilder.AppendLine($"{LogPrefix} ===== Phase 14.10-M-4: Validation Summary =====");
            logBuilder.AppendLine($"{LogPrefix} Total checks: {totalChecks} | [PASS] {passCount} | [FAIL] {failCount} | [WARN] {warnCount} | [INFO] {infoCount}");

            if (failCount > 0)
            {
                logBuilder.AppendLine($"{LogPrefix} [FAIL] Validation completed with {failCount} failure(s). Review logs above.");
                Debug.LogError(logBuilder.ToString());
            }
            else
            {
                logBuilder.AppendLine($"{LogPrefix} [PASS] All checks OK. Resolution plans are valid.");
                Debug.Log(logBuilder.ToString());
            }
        }

        /// <summary>
        /// GeneratedWorldRoot를 찾는다. settings와 context를 기반으로 탐색한다.
        /// </summary>
        private static GameObject FindGeneratedRoot(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (context != null && context.GeneratedRootOverride != null)
                return context.GeneratedRootOverride;

            string rootName = settings.GeneratedRootName;
            if (string.IsNullOrEmpty(rootName))
                rootName = "GeneratedWorldRoot";

            return GameObject.Find(rootName);
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
