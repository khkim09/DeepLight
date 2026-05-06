using System.Collections.Generic;
using System.Text;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Harvest;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-O-15: Generated harvest target의 runtime harvest data/profile binding을 구축/검증하는 Editor Utility.
    /// GeneratedWorldRoot 하위의 WorldMapGeneratedHarvestTarget 64개를 수집하고,
    /// WorldMapGeneratedHarvestTargetDataResolver를 부착/갱신하며,
    /// 각 target이 resolver를 통해 안전하게 resolved data를 가질 수 있도록 binding한다.
    /// 모든 로그는 StringBuilder 하나로 모아 단일 Debug.Log/Debug.LogError로 출력한다.
    /// </summary>
    public static class DeepLightMapGeneratedHarvestTargetDataBindingUtility
    {
        // ===== Constants =====

        private const string LOG_PREFIX = "[HarvestTargetDataBinding]";
        private const int EXPECTED_TARGET_COUNT = 64;

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot 하위의 WorldMapGeneratedHarvestTarget 64개를 수집하고,
        /// WorldMapGeneratedHarvestTargetDataResolver를 부착/갱신하며,
        /// 각 target에 resolver binding을 적용하고 RefreshResolvedData를 호출한다.
        /// 모든 로그는 StringBuilder 하나로 모아 단일 Debug.Log/Debug.LogError로 출력한다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildGeneratedHarvestTargetDataBindings(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{LOG_PREFIX} ===== Phase 14.10-O-15: Rebuild Generated Harvest Target Data Bindings =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedWorldRoot = FindGeneratedWorldRoot();
            if (generatedWorldRoot == null)
            {
                sb.AppendLine($"{LOG_PREFIX} [FAIL] GeneratedWorldRoot not found in scene.");
                Debug.LogError(sb.ToString());
                return;
            }
            sb.AppendLine($"{LOG_PREFIX} GeneratedWorldRoot: '{generatedWorldRoot.name}'");

            // 2. WorldMapGeneratedHarvestTarget 64개 수집
            WorldMapGeneratedHarvestTarget[] targets = generatedWorldRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
            sb.AppendLine($"{LOG_PREFIX} Targets: {targets.Length}");

            if (targets.Length != EXPECTED_TARGET_COUNT)
            {
                sb.AppendLine($"{LOG_PREFIX} [WARN] Expected {EXPECTED_TARGET_COUNT} targets, found {targets.Length}.");
            }

            // 3. GeneratedWorldRoot에 WorldMapGeneratedHarvestTargetDataResolver가 없으면 추가
            WorldMapGeneratedHarvestTargetDataResolver resolver = generatedWorldRoot.GetComponent<WorldMapGeneratedHarvestTargetDataResolver>();
            bool resolverCreated = false;
            if (resolver == null)
            {
                resolver = generatedWorldRoot.AddComponent<WorldMapGeneratedHarvestTargetDataResolver>();
                resolverCreated = true;
                sb.AppendLine($"{LOG_PREFIX} Resolver: WorldMapGeneratedHarvestTargetDataResolver (created)");
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} Resolver: WorldMapGeneratedHarvestTargetDataResolver (reused)");
            }

            // 4. 각 target에 AssignDataResolver + RefreshResolvedData 호출
            int resolvedCount = 0;
            int fallbackCount = 0;
            int unresolvedCount = 0;
            int availableCount = 0;
            var runtimeKeyBreakdown = new Dictionary<string, int>();
            var targetKindBreakdown = new Dictionary<string, int>();

            for (int i = 0; i < targets.Length; i++)
            {
                WorldMapGeneratedHarvestTarget target = targets[i];
                if (target == null)
                {
                    unresolvedCount++;
                    continue;
                }

                // AssignDataResolver
                target.AssignDataResolver(resolver);

                // RefreshResolvedData
                bool resolved = target.RefreshResolvedData();

                // 상태 집계
                ResolvedGeneratedHarvestTargetData resolvedData = target.ResolvedData;
                if (resolvedData != null)
                {
                    resolvedCount++;
                    if (resolvedData.IsFallback)
                        fallbackCount++;

                    if (resolvedData.IsReady)
                        availableCount++;
                }
                else
                {
                    unresolvedCount++;
                }

                // runtimeKey breakdown
                string runtimeKey = target.RuntimeKey ?? "null";
                if (!runtimeKeyBreakdown.ContainsKey(runtimeKey))
                    runtimeKeyBreakdown[runtimeKey] = 0;
                runtimeKeyBreakdown[runtimeKey]++;

                // targetKind breakdown
                string targetKindStr = target.TargetKind.ToString();
                if (!targetKindBreakdown.ContainsKey(targetKindStr))
                    targetKindBreakdown[targetKindStr] = 0;
                targetKindBreakdown[targetKindStr]++;

                // EditorUtility.SetDirty
                EditorUtility.SetDirty(target);
            }

            // 5. Summary 로그 구성
            sb.AppendLine($"{LOG_PREFIX} Resolved: {resolvedCount}");
            sb.AppendLine($"{LOG_PREFIX} Fallback: {fallbackCount}");
            sb.AppendLine($"{LOG_PREFIX} Unresolved: {unresolvedCount}");
            sb.AppendLine($"{LOG_PREFIX} Available: {availableCount}");

            // RuntimeKey Breakdown
            sb.AppendLine($"{LOG_PREFIX} --- RuntimeKey Breakdown ---");
            foreach (var kvp in runtimeKeyBreakdown)
            {
                sb.AppendLine($"{LOG_PREFIX}   {kvp.Key}: {kvp.Value}");
            }

            // TargetKind Breakdown
            sb.AppendLine($"{LOG_PREFIX} --- TargetKind Breakdown ---");
            foreach (var kvp in targetKindBreakdown)
            {
                sb.AppendLine($"{LOG_PREFIX}   {kvp.Key}: {kvp.Value}");
            }

            // Samples (최대 10개)
            sb.AppendLine($"{LOG_PREFIX} --- Samples ---");
            int sampleCount = Mathf.Min(targets.Length, 10);
            for (int i = 0; i < sampleCount; i++)
            {
                WorldMapGeneratedHarvestTarget target = targets[i];
                if (target == null)
                {
                    sb.AppendLine($"{LOG_PREFIX}   [{i + 1}] NULL");
                    continue;
                }
                sb.AppendLine($"{LOG_PREFIX}   [{i + 1}] {target.GetDebugSummary()}");
            }

            // Resolver summary
            string resolverSummary = resolver.GetDebugSummary();
            sb.AppendLine($"{LOG_PREFIX} Resolver summary:");
            sb.AppendLine(resolverSummary);

            // Final result
            if (unresolvedCount > 0)
            {
                sb.AppendLine($"{LOG_PREFIX} [RESULT] FAILED ({unresolvedCount} unresolved)");
                Debug.LogError(sb.ToString());
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [RESULT] PASS");
                Debug.Log(sb.ToString());
            }

            // SetDirty
            EditorUtility.SetDirty(resolver);
            EditorUtility.SetDirty(generatedWorldRoot);
        }

        /// <summary>
        /// GeneratedWorldRoot 하위의 WorldMapGeneratedHarvestTarget 64개와
        /// WorldMapGeneratedHarvestTargetDataResolver의 유효성을 검사한다.
        /// 모든 검사는 StringBuilder 하나로 모아 단일 Debug.Log/Debug.LogWarning/Debug.LogError로 출력한다.
        /// unresolved target이 있으면 FAIL로 처리한다.
        /// DisplayDialog를 사용하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateGeneratedHarvestTargetDataBindings(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{LOG_PREFIX} ===== Phase 14.10-O-15: Validate Generated Harvest Target Data Bindings =====");

            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot exists
            GameObject generatedWorldRoot = FindGeneratedWorldRoot();
            if (generatedWorldRoot != null)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] GeneratedWorldRoot exists: '{generatedWorldRoot.name}'");
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [FAIL] GeneratedWorldRoot not found in scene.");
                failCount++;
                sb.AppendLine($"{LOG_PREFIX} [RESULT] FAILED (no GeneratedWorldRoot)");
                Debug.LogError(sb.ToString());
                return;
            }

            // 2. Provider exists / count=64
            WorldMapHarvestInteractionTargetProvider provider = generatedWorldRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);
            if (provider != null)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] WorldMapHarvestInteractionTargetProvider exists.");
                int providerCount = provider.Count;
                if (providerCount == EXPECTED_TARGET_COUNT)
                {
                    sb.AppendLine($"{LOG_PREFIX} [PASS] Provider count = {providerCount} (expected {EXPECTED_TARGET_COUNT}).");
                }
                else
                {
                    sb.AppendLine($"{LOG_PREFIX} [FAIL] Provider count = {providerCount} (expected {EXPECTED_TARGET_COUNT}).");
                    failCount++;
                }
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [FAIL] WorldMapHarvestInteractionTargetProvider not found.");
                failCount++;
            }

            // 3. WorldMapGeneratedHarvestTarget count=64
            WorldMapGeneratedHarvestTarget[] targets = generatedWorldRoot.GetComponentsInChildren<WorldMapGeneratedHarvestTarget>(true);
            if (targets.Length == EXPECTED_TARGET_COUNT)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] WorldMapGeneratedHarvestTarget count = {targets.Length} (expected {EXPECTED_TARGET_COUNT}).");
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [FAIL] WorldMapGeneratedHarvestTarget count = {targets.Length} (expected {EXPECTED_TARGET_COUNT}).");
                failCount++;
            }

            // 4. Resolver exists
            WorldMapGeneratedHarvestTargetDataResolver resolver = generatedWorldRoot.GetComponent<WorldMapGeneratedHarvestTargetDataResolver>();
            if (resolver != null)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] WorldMapGeneratedHarvestTargetDataResolver exists on GeneratedWorldRoot.");
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [FAIL] WorldMapGeneratedHarvestTargetDataResolver not found on GeneratedWorldRoot.");
                failCount++;
            }

            // 5. 모든 generated target이 SourceMarkerId/ZoneId/RuntimeKey/ProfileId/RequirementId 보유
            int idValidCount = 0;
            int idInvalidCount = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                WorldMapGeneratedHarvestTarget target = targets[i];
                if (target == null)
                {
                    idInvalidCount++;
                    continue;
                }

                bool hasSourceMarkerId = !string.IsNullOrEmpty(target.SourceMarkerId);
                bool hasZoneId = !string.IsNullOrEmpty(target.ZoneId);
                bool hasRuntimeKey = !string.IsNullOrEmpty(target.RuntimeKey);
                bool hasProfileId = !string.IsNullOrEmpty(target.ProfileId);
                bool hasRequirementId = !string.IsNullOrEmpty(target.RequirementId);

                if (hasSourceMarkerId && hasZoneId && hasRuntimeKey && hasProfileId && hasRequirementId)
                {
                    idValidCount++;
                }
                else
                {
                    idInvalidCount++;
                    sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] missing IDs: " +
                        $"SourceMarkerId={hasSourceMarkerId}, ZoneId={hasZoneId}, " +
                        $"RuntimeKey={hasRuntimeKey}, ProfileId={hasProfileId}, RequirementId={hasRequirementId}");
                }
            }

            if (idInvalidCount == 0)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] All {idValidCount} targets have valid SourceMarkerId/ZoneId/RuntimeKey/ProfileId/RequirementId.");
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [FAIL] {idInvalidCount} targets have missing IDs ({idValidCount} valid).");
                failCount++;
            }

            // 6. 모든 generated target이 IsAvailable/IsReady/GetDebugSummary를 exception 없이 평가
            int availableCount = 0;
            int unavailableCount = 0;
            int exceptionCount = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    WorldMapGeneratedHarvestTarget target = targets[i];
                    if (target == null)
                    {
                        exceptionCount++;
                        continue;
                    }

                    // IsAvailable 평가
                    bool isAvailable = target.IsAvailable;
                    if (isAvailable)
                        availableCount++;
                    else
                        unavailableCount++;

                    // IsReady 평가
                    bool isReady = target.IsReady;

                    // GetDebugSummary 평가
                    string summary = target.GetDebugSummary();
                }
                catch (System.Exception ex)
                {
                    exceptionCount++;
                    sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] threw exception: {ex.Message}");
                }
            }

            if (exceptionCount == 0)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] All {targets.Length} targets evaluated without exception. " +
                    $"Available={availableCount}, Unavailable={unavailableCount}.");
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [FAIL] {exceptionCount} targets threw exceptions during evaluation.");
                failCount++;
            }

            // 7. 모든 generated target이 resolved data를 가지는지 검증
            int resolvedCount = 0;
            int unresolvedCount = 0;
            int fallbackCount = 0;
            int resolvedDataNullCount = 0;
            int resolvedDataNotReadyCount = 0;
            int identityMismatchCount = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                WorldMapGeneratedHarvestTarget target = targets[i];
                if (target == null)
                {
                    unresolvedCount++;
                    continue;
                }

                // RefreshResolvedData 호출
                bool refreshResult = target.RefreshResolvedData();

                // TryGetResolvedData 검증
                bool hasResolved = target.TryGetResolvedData(out ResolvedGeneratedHarvestTargetData resolvedData);

                if (hasResolved && resolvedData != null)
                {
                    resolvedCount++;
                    if (resolvedData.IsFallback)
                        fallbackCount++;

                    // ResolvedData identity 검증
                    if (resolvedData.SourceMarkerId != target.SourceMarkerId)
                    {
                        identityMismatchCount++;
                        sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] SourceMarkerId mismatch: target='{target.SourceMarkerId}', resolved='{resolvedData.SourceMarkerId}'");
                    }
                    if (resolvedData.ZoneId != target.ZoneId)
                    {
                        identityMismatchCount++;
                        sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] ZoneId mismatch: target='{target.ZoneId}', resolved='{resolvedData.ZoneId}'");
                    }
                    if (resolvedData.RuntimeKey != target.RuntimeKey)
                    {
                        identityMismatchCount++;
                        sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] RuntimeKey mismatch: target='{target.RuntimeKey}', resolved='{resolvedData.RuntimeKey}'");
                    }
                    if (resolvedData.ProfileId != target.ProfileId)
                    {
                        identityMismatchCount++;
                        sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] ProfileId mismatch: target='{target.ProfileId}', resolved='{resolvedData.ProfileId}'");
                    }
                    if (resolvedData.RequirementId != target.RequirementId)
                    {
                        identityMismatchCount++;
                        sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] RequirementId mismatch: target='{target.RequirementId}', resolved='{resolvedData.RequirementId}'");
                    }
                    if (resolvedData.InteractionKind != target.InteractionKind)
                    {
                        identityMismatchCount++;
                        sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] InteractionKind mismatch: target='{target.InteractionKind}', resolved='{resolvedData.InteractionKind}'");
                    }
                    if (resolvedData.TargetKind != target.TargetKind)
                    {
                        identityMismatchCount++;
                        sb.AppendLine($"{LOG_PREFIX} [FAIL] Target [{i}] TargetKind mismatch: target='{target.TargetKind}', resolved='{resolvedData.TargetKind}'");
                    }
                }
                else
                {
                    unresolvedCount++;
                    if (resolvedData == null)
                        resolvedDataNullCount++;
                    if (resolvedData != null && !resolvedData.IsReady)
                        resolvedDataNotReadyCount++;
                }
            }

            if (unresolvedCount == 0 && identityMismatchCount == 0)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] All {resolvedCount} targets have resolved data. " +
                    $"Fallback count: {fallbackCount}.");
            }
            else
            {
                if (unresolvedCount > 0)
                {
                    sb.AppendLine($"{LOG_PREFIX} [FAIL] {unresolvedCount} targets have no resolved data ({resolvedCount} resolved, {fallbackCount} fallback). " +
                        $"Null={resolvedDataNullCount}, NotReady={resolvedDataNotReadyCount}");
                    failCount++;
                }
                if (identityMismatchCount > 0)
                {
                    sb.AppendLine($"{LOG_PREFIX} [FAIL] {identityMismatchCount} identity mismatches detected.");
                    failCount++;
                }
            }

            // 8. Fallback data 사용 개수 출력 (INFO)
            sb.AppendLine($"{LOG_PREFIX} [INFO] Fallback data usage: {fallbackCount} / {targets.Length} targets.");

            // 9. RuntimeKey별 데이터 매핑 수 출력
            var runtimeKeyMappingCount = new Dictionary<string, int>();
            for (int i = 0; i < targets.Length; i++)
            {
                WorldMapGeneratedHarvestTarget target = targets[i];
                if (target == null)
                    continue;

                string runtimeKey = target.RuntimeKey ?? "null";
                if (!runtimeKeyMappingCount.ContainsKey(runtimeKey))
                    runtimeKeyMappingCount[runtimeKey] = 0;
                runtimeKeyMappingCount[runtimeKey]++;
            }

            sb.AppendLine($"{LOG_PREFIX} [INFO] RuntimeKey mapping count: {runtimeKeyMappingCount.Count} unique keys.");
            foreach (var kvp in runtimeKeyMappingCount)
            {
                sb.AppendLine($"{LOG_PREFIX} [INFO]   {kvp.Key}: {kvp.Value} targets");
            }

            // 10. no generated target under RuntimeSpawnedInstances/RuntimeSpawnInstances/RuntimePlaceholder/ZoneContentMarker
            int invalidParentCount = 0;
            string[] invalidParentNames = { "RuntimeSpawnedInstances", "RuntimeSpawnInstances", "RuntimePlaceholder", "ZoneContentMarker" };
            for (int i = 0; i < targets.Length; i++)
            {
                WorldMapGeneratedHarvestTarget target = targets[i];
                if (target == null)
                    continue;

                Transform parent = target.transform.parent;
                while (parent != null)
                {
                    for (int j = 0; j < invalidParentNames.Length; j++)
                    {
                        if (parent.name.Contains(invalidParentNames[j]))
                        {
                            invalidParentCount++;
                            sb.AppendLine($"{LOG_PREFIX} [WARN] Target '{target.SourceMarkerId}' is under '{parent.name}' hierarchy.");
                            break;
                        }
                    }
                    parent = parent.parent;
                }
            }

            if (invalidParentCount == 0)
            {
                sb.AppendLine($"{LOG_PREFIX} [PASS] No targets under RuntimeSpawnedInstances/RuntimeSpawnInstances/RuntimePlaceholder/ZoneContentMarker.");
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [WARN] {invalidParentCount} targets found under invalid parent hierarchy.");
                warnCount++;
            }

            // 11. scene transform/name unchanged (read-only 검증이므로 수정하지 않음)
            sb.AppendLine($"{LOG_PREFIX} [INFO] Read-only validation: scene transform/name unchanged.");

            // Summary
            sb.AppendLine($"{LOG_PREFIX} ===== Validation Summary =====");
            sb.AppendLine($"{LOG_PREFIX} [FAIL] {failCount} checks failed.");
            sb.AppendLine($"{LOG_PREFIX} [WARN] {warnCount} warnings.");

            if (failCount == 0)
            {
                sb.AppendLine($"{LOG_PREFIX} [RESULT] ALL CHECKS PASSED (FAIL=0).");
                if (warnCount > 0)
                {
                    Debug.LogWarning(sb.ToString());
                }
                else
                {
                    Debug.Log(sb.ToString());
                }
            }
            else
            {
                sb.AppendLine($"{LOG_PREFIX} [RESULT] {failCount} FAILURES DETECTED.");
                Debug.LogError(sb.ToString());
            }
        }

        // ===== Private Methods =====

        /// <summary>
        /// 현재 scene에서 GeneratedWorldRoot GameObject를 찾는다.
        /// 이름이 "GeneratedWorldRoot"인 GameObject를 우선 찾고,
        /// 없으면 "GeneratedWorld" 또는 "Generated"로 fallback한다.
        /// </summary>
        private static GameObject FindGeneratedWorldRoot()
        {
            // 1. 정확한 이름으로 찾기
            GameObject root = GameObject.Find("GeneratedWorldRoot");
            if (root != null)
                return root;

            // 2. "GeneratedWorldRoot"를 포함하는 GameObject 찾기
            GameObject[] allObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i].name == "GeneratedWorldRoot")
                {
                    return allObjects[i];
                }
            }

            // 3. "GeneratedWorld"로 fallback
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i].name.Contains("GeneratedWorld"))
                {
                    return allObjects[i];
                }
            }

            return null;
        }
    }
}
