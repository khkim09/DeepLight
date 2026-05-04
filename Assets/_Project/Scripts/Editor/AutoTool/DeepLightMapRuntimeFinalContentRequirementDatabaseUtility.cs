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
    /// Phase 14.10-M-2: Runtime Final Content Requirement Database 전용 Editor Utility.
    /// M-1 contract 기준으로 실제 final content prefab/profile 연결이 필요한 requirement slot을
    /// SO asset으로 생성/검증한다. finalPrefab/finalProfile null은 현재 단계에서 FAIL이 아니라 WARN이다.
    /// </summary>
    public static class DeepLightMapRuntimeFinalContentRequirementDatabaseUtility
    {
        // ===== Constants =====

        /// <summary>Database asset 기본 경로</summary>
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeFinalContentRequirementDatabase.asset";

        /// <summary>exact requirement 기본 priority</summary>
        private const int ExactPriority = 100;

        /// <summary>fallback requirement 기본 priority</summary>
        private const int FallbackPriority = 10;

        // ===== Public API =====

        /// <summary>
        /// M-1 contract 369개를 scene에서 수집하고, unique requirement 후보를 기반으로
        /// Runtime Final Content Requirement Database asset을 생성/갱신한다.
        /// 기존 사용자 entry는 requirementId 기준으로 보존한다.
        /// </summary>
        public static void CreateOrUpdateDefaultRuntimeFinalContentRequirementDatabase(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-2] Settings is null! Cannot create/update requirement database.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-M-2: Create/Update Runtime Final Content Requirement Database =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-2] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }

            // 2. QueryService 찾기
            WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
            if (queryService == null || queryService.Count == 0)
            {
                Debug.LogError("[MapAutoBuilder] [M-2] QueryService not found or empty. Run M-1 first.");
                return;
            }

            // 3. M-1 contract 수집 (RuntimeSpawnedInstances 하위만, preview 제외)
            IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
            List<WorldMapRuntimeFinalContentContract> validContracts = CollectValidContracts(allAdapters);

            if (validContracts.Count == 0)
            {
                Debug.LogError("[MapAutoBuilder] [M-2] No valid contracts found in RuntimeSpawnedInstances. Run M-1 first.");
                return;
            }

            Debug.Log($"[MapAutoBuilder] [M-2] Collected {validContracts.Count} valid contracts from RuntimeSpawnedInstances.");

            // 4. Database asset 찾기 또는 생성
            WorldMapRuntimeFinalContentRequirementDatabaseSO database = FindOrCreateDatabaseAsset();
            if (database == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-2] Failed to find or create database asset.");
                return;
            }

            // 5. 기존 entry를 requirementId 기준으로 보존
            Dictionary<string, WorldMapRuntimeFinalContentRequirementEntry> existingById = new Dictionary<string, WorldMapRuntimeFinalContentRequirementEntry>();
            foreach (WorldMapRuntimeFinalContentRequirementEntry existing in database.Entries)
            {
                if (existing != null && !string.IsNullOrEmpty(existing.RequirementId))
                {
                    if (!existingById.ContainsKey(existing.RequirementId))
                    {
                        existingById[existing.RequirementId] = existing;
                    }
                }
            }

            Debug.Log($"[MapAutoBuilder] [M-2] Preserved {existingById.Count} existing entries by requirementId.");

            // 6. Contract 기준으로 unique requirement 후보 생성
            var uniqueCandidates = new Dictionary<string, (WorldMapRuntimeFinalContentContract contract, int count)>();
            foreach (WorldMapRuntimeFinalContentContract contract in validContracts)
            {
                string key = BuildCandidateKey(contract);
                if (!uniqueCandidates.ContainsKey(key))
                {
                    uniqueCandidates[key] = (contract, 0);
                }
                // count 증가
                var current = uniqueCandidates[key];
                uniqueCandidates[key] = (current.contract, current.count + 1);
            }

            Debug.Log($"[MapAutoBuilder] [M-2] Generated {uniqueCandidates.Count} unique requirement candidates from {validContracts.Count} contracts.");

            // 7. 새 entry 목록 생성 (기존 보존 + 신규 추가)
            List<WorldMapRuntimeFinalContentRequirementEntry> newEntries = new List<WorldMapRuntimeFinalContentRequirementEntry>();

            // 7a. 기존 보존 entry 추가
            foreach (WorldMapRuntimeFinalContentRequirementEntry existing in existingById.Values)
            {
                newEntries.Add(existing);
            }

            // 7b. 신규 unique candidate entry 추가 (기존에 없는 것만)
            int addedCount = 0;
            foreach (var kvp in uniqueCandidates)
            {
                string candidateKey = kvp.Key;
                WorldMapRuntimeFinalContentContract contract = kvp.Value.contract;
                int contractCount = kvp.Value.count;

                // 이미 존재하는 requirementId인지 확인
                string requirementId = BuildRequirementId(contract);
                if (existingById.ContainsKey(requirementId))
                {
                    continue; // 기존 entry 보존
                }

                // 신규 entry 생성
                WorldMapRuntimeFinalContentRequirementEntry newEntry = CreateEntryFromContract(contract, requirementId, contractCount);
                newEntries.Add(newEntry);
                addedCount++;
            }

            // 7c. 기본 fallback requirement 보장 (finalContentKind별)
            EnsureFallbackRequirements(newEntries, existingById);

            // 8. Database에 새 목록 할당
            List<WorldMapRuntimeFinalContentRequirementEntry> editableEntries = database.GetEditableEntries();
            editableEntries.Clear();
            editableEntries.AddRange(newEntries);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MapAutoBuilder] [M-2] Database updated. Total entries: {newEntries.Count} (preserved: {existingById.Count}, added: {addedCount}).");
            Debug.Log($"[MapAutoBuilder] ===== Phase 14.10-M-2: Create/Update Runtime Final Content Requirement Database Complete =====");
        }

        /// <summary>
        /// Runtime Final Content Requirement Database의 유효성을 검사한다.
        /// 15개 이상의 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Validate는 GameObject 생성/삭제/이동/name 변경을 금지한다.
        /// </summary>
        public static void ValidateRuntimeFinalContentRequirementDatabase(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [M-2] Settings is null! Cannot validate requirement database.");
                return;
            }

            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-M-2: Validate Runtime Final Content Requirement Database =====");

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-M-2: Validate Runtime Final Content Requirement Database =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // 1. Database asset exists
            WorldMapRuntimeFinalContentRequirementDatabaseSO database = LoadDatabaseAsset();
            if (database == null)
            {
                log.AppendLine("  [FAIL] 1. Database asset exists: NOT FOUND");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 1. Database asset exists: '{AssetDatabase.GetAssetPath(database)}'");
                passCount++;
            }

            if (database == null)
            {
                log.AppendLine("  [FAIL] Cannot proceed without database asset.");
                failCount++;
                Debug.Log(log.ToString());
                return;
            }

            // 2. Entries count > 0
            int entryCount = database.Entries?.Count ?? 0;
            if (entryCount <= 0)
            {
                log.AppendLine($"  [FAIL] 2. Entries count > 0: {entryCount}");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 2. Entries count > 0: {entryCount}");
                passCount++;
            }

            if (entryCount <= 0)
            {
                log.AppendLine("  [FAIL] Cannot proceed without entries.");
                failCount++;
                Debug.Log(log.ToString());
                return;
            }

            // 3. No duplicate requirementId
            string duplicateId;
            if (database.HasDuplicateRequirementIds(out duplicateId))
            {
                log.AppendLine($"  [FAIL] 3. No duplicate requirementId: Found duplicate '{duplicateId}'");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 3. No duplicate requirementId");
                passCount++;
            }

            // 4. No duplicate exact match keys
            string duplicateKey;
            if (database.HasDuplicateExactMatchKeys(out duplicateKey))
            {
                log.AppendLine($"  [FAIL] 4. No duplicate exact match keys: Found duplicate '{duplicateKey}'");
                failCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 4. No duplicate exact match keys");
                passCount++;
            }

            // 5. Required fallback finalContentKind coverage
            bool allFallbackCovered = true;
            WorldMapRuntimeFinalContentKind[] requiredKinds = (WorldMapRuntimeFinalContentKind[])Enum.GetValues(typeof(WorldMapRuntimeFinalContentKind));
            foreach (WorldMapRuntimeFinalContentKind kind in requiredKinds)
            {
                if (kind == WorldMapRuntimeFinalContentKind.None || kind == WorldMapRuntimeFinalContentKind.Unknown)
                    continue;

                if (!database.HasFallbackForFinalContentKind(kind))
                {
                    log.AppendLine($"  [FAIL] 5. Fallback coverage for {kind}: MISSING");
                    allFallbackCovered = false;
                    failCount++;
                }
            }
            if (allFallbackCovered)
            {
                log.AppendLine("  [PASS] 5. All required fallback finalContentKind coverage present");
                passCount++;
            }

            // 6. M-1 contract count > 0 (scene에서 수집)
            GameObject generatedRoot = FindGeneratedRoot(settings, context);
            int contractCount = 0;
            if (generatedRoot != null)
            {
                WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
                if (queryService != null && queryService.Count > 0)
                {
                    IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                    List<WorldMapRuntimeFinalContentContract> validContracts = CollectValidContracts(allAdapters);
                    contractCount = validContracts.Count;
                }
            }

            if (contractCount <= 0)
            {
                log.AppendLine($"  [FAIL] 6. M-1 contract count > 0: {contractCount}");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 6. M-1 contract count > 0: {contractCount}");
                passCount++;
            }

            // 7. All contracts matched to requirement entry
            int matchedCount = 0;
            int unmatchedCount = 0;
            int matchedButUnconfiguredCount = 0;
            int configuredCount = 0;
            int missingPrefabProfileCount = 0;

            // Fallback/Exact usage tracking
            int fallbackUsageCount = 0;
            int exactUsageCount = 0;
            int unknownKindCount = 0;

            // finalContentKind별 breakdown
            var kindTotal = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var kindMatched = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var kindConfigured = new Dictionary<WorldMapRuntimeFinalContentKind, int>();
            var kindMissing = new Dictionary<WorldMapRuntimeFinalContentKind, int>();

            // runtimeCategory별 breakdown
            var categoryTotal = new Dictionary<string, int>();

            if (generatedRoot != null && contractCount > 0)
            {
                WorldMapRuntimeGameplayAdapterQueryService queryService = generatedRoot.GetComponent<WorldMapRuntimeGameplayAdapterQueryService>();
                if (queryService != null)
                {
                    IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters = queryService.GetAll();
                    List<WorldMapRuntimeFinalContentContract> validContracts = CollectValidContracts(allAdapters);

                    foreach (WorldMapRuntimeFinalContentContract contract in validContracts)
                    {
                        // Category breakdown
                        string cat = contract.RuntimeCategory;
                        if (!string.IsNullOrEmpty(cat))
                        {
                            if (!categoryTotal.ContainsKey(cat))
                                categoryTotal[cat] = 0;
                            categoryTotal[cat]++;
                        }

                        // Kind breakdown
                        WorldMapRuntimeFinalContentKind kind = contract.FinalContentKind;
                        if (!kindTotal.ContainsKey(kind))
                            kindTotal[kind] = 0;
                        kindTotal[kind]++;

                        if (kind == WorldMapRuntimeFinalContentKind.Unknown)
                            unknownKindCount++;

                        // Try match
                        WorldMapRuntimeFinalContentRequirementEntry matchedEntry;
                        if (database.TryFindBestRequirement(contract, out matchedEntry))
                        {
                            matchedCount++;

                            if (!kindMatched.ContainsKey(kind))
                                kindMatched[kind] = 0;
                            kindMatched[kind]++;

                            if (matchedEntry.IsFallbackRequirement)
                                fallbackUsageCount++;
                            else
                                exactUsageCount++;

                            if (matchedEntry.IsConfigured())
                            {
                                configuredCount++;
                                if (!kindConfigured.ContainsKey(kind))
                                    kindConfigured[kind] = 0;
                                kindConfigured[kind]++;
                            }
                            else
                            {
                                matchedButUnconfiguredCount++;
                                if (!kindMissing.ContainsKey(kind))
                                    kindMissing[kind] = 0;
                                kindMissing[kind]++;
                            }
                        }
                        else
                        {
                            unmatchedCount++;
                        }
                    }
                }
            }

            if (unmatchedCount > 0)
            {
                log.AppendLine($"  [FAIL] 7. All contracts matched to requirement entry: {matchedCount} matched, {unmatchedCount} unmatched");
                failCount++;
            }
            else
            {
                log.AppendLine($"  [PASS] 7. All contracts matched to requirement entry: {matchedCount}/{contractCount}");
                passCount++;
            }

            // 8. Matched but unconfigured count (INFO/WARN)
            log.AppendLine($"  [INFO] 8. Matched but unconfigured entries: {matchedButUnconfiguredCount} (expected in current phase)");
            infoCount++;

            // 9. Configured count (INFO)
            log.AppendLine($"  [INFO] 9. Configured entries: {configuredCount}");
            infoCount++;

            // 10. Missing finalPrefab/finalProfile count (WARN, not FAIL)
            missingPrefabProfileCount = database.CountMissing();
            if (missingPrefabProfileCount > 0)
            {
                log.AppendLine($"  [WARN] 10. Missing finalPrefab/finalProfile entries: {missingPrefabProfileCount} (expected in current phase, not FAIL)");
                warnCount++;
            }
            else
            {
                log.AppendLine("  [PASS] 10. All entries have finalPrefab or finalProfile assigned");
                passCount++;
            }

            // 11. finalContentKind별 breakdown
            log.AppendLine("  [INFO] 11. FinalContentKind breakdown (total/matched/configured/missing):");
            foreach (WorldMapRuntimeFinalContentKind kind in kindTotal.Keys)
            {
                int total = kindTotal[kind];
                int matched = kindMatched.ContainsKey(kind) ? kindMatched[kind] : 0;
                int configured = kindConfigured.ContainsKey(kind) ? kindConfigured[kind] : 0;
                int missing = kindMissing.ContainsKey(kind) ? kindMissing[kind] : 0;
                log.AppendLine($"         {kind}: total={total}, matched={matched}, configured={configured}, missing={missing}");
            }
            infoCount++;

            // 12. runtimeCategory별 breakdown
            log.AppendLine("  [INFO] 12. RuntimeCategory breakdown:");
            foreach (var kvp in categoryTotal)
            {
                log.AppendLine($"         {kvp.Key}: {kvp.Value}");
            }
            infoCount++;

            // 13. Fallback requirement 사용 count
            log.AppendLine($"  [INFO] 13. Fallback requirement usage count: {fallbackUsageCount}");
            infoCount++;

            // 14. Exact requirement 사용 count
            log.AppendLine($"  [INFO] 14. Exact requirement usage count: {exactUsageCount}");
            infoCount++;

            // 15. Unknown kind count
            log.AppendLine($"  [INFO] 15. Unknown kind count: {unknownKindCount}");
            infoCount++;

            // 16. Scene object transform/name unchanged (Validate 전후 snapshot 비교)
            bool transformChanged = false;
            if (generatedRoot != null)
            {
                var preValidateSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();
                CaptureTransformSnapshots(generatedRoot.transform, preValidateSnapshots);

                var postValidateSnapshots = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale, string name)>();
                CaptureTransformSnapshots(generatedRoot.transform, postValidateSnapshots);

                foreach (var kvp in preValidateSnapshots)
                {
                    Transform t = kvp.Key;
                    if (t == null) continue;

                    if (postValidateSnapshots.TryGetValue(t, out var postSnapshot))
                    {
                        if (kvp.Value.pos != postSnapshot.pos ||
                            kvp.Value.rot != postSnapshot.rot ||
                            kvp.Value.scale != postSnapshot.scale ||
                            kvp.Value.name != postSnapshot.name)
                        {
                            transformChanged = true;
                            log.AppendLine($"  [FAIL] 16. Transform/name changed: '{kvp.Value.name}' -> '{postSnapshot.name}'");
                            failCount++;
                            break;
                        }
                    }
                }
            }

            if (!transformChanged)
            {
                log.AppendLine("  [PASS] 16. Scene object transform/name unchanged");
                passCount++;
            }

            // ===== Summary =====
            log.AppendLine($"===== Validate Summary: PASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount} =====");
            Debug.Log(log.ToString());

            // EditorUtility.DisplayDialog로 요약 표시
            EditorUtility.DisplayDialog(
                "Phase 14.10-M-2: Validate Runtime Final Content Requirement Database",
                $"PASS={passCount}, FAIL={failCount}, WARN={warnCount}, INFO={infoCount}\n\n" +
                $"Database Entries: {entryCount}\n" +
                $"M-1 Contracts: {contractCount}\n" +
                $"Matched: {matchedCount}\n" +
                $"Unmatched: {unmatchedCount}\n" +
                $"Configured: {configuredCount}\n" +
                $"Unconfigured: {matchedButUnconfiguredCount}\n" +
                $"Missing Prefab/Profile: {missingPrefabProfileCount}\n" +
                $"Fallback Usage: {fallbackUsageCount}\n" +
                $"Exact Usage: {exactUsageCount}\n" +
                $"Unknown Kind: {unknownKindCount}\n\n" +
                $"자세한 내용은 Console 창을 확인하세요.",
                "OK");
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// Database asset을 로드한다. 없으면 null 반환.
        /// </summary>
        private static WorldMapRuntimeFinalContentRequirementDatabaseSO LoadDatabaseAsset()
        {
            return AssetDatabase.LoadAssetAtPath<WorldMapRuntimeFinalContentRequirementDatabaseSO>(DatabaseAssetPath);
        }

        /// <summary>
        /// Database asset을 찾거나 생성한다.
        /// </summary>
        private static WorldMapRuntimeFinalContentRequirementDatabaseSO FindOrCreateDatabaseAsset()
        {
            // 기존 asset 로드 시도
            WorldMapRuntimeFinalContentRequirementDatabaseSO existing = LoadDatabaseAsset();
            if (existing != null)
            {
                Debug.Log($"[MapAutoBuilder] [M-2] Found existing database asset at '{DatabaseAssetPath}'.");
                return existing;
            }

            // 디렉토리 확인 및 생성
            string directory = System.IO.Path.GetDirectoryName(DatabaseAssetPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
                Debug.Log($"[MapAutoBuilder] [M-2] Created directory: '{directory}'.");
            }

            // 새 asset 생성
            WorldMapRuntimeFinalContentRequirementDatabaseSO newDb = ScriptableObject.CreateInstance<WorldMapRuntimeFinalContentRequirementDatabaseSO>();
            AssetDatabase.CreateAsset(newDb, DatabaseAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MapAutoBuilder] [M-2] Created new database asset at '{DatabaseAssetPath}'.");
            return newDb;
        }

        /// <summary>
        /// RuntimeSpawnedInstances 하위 runtime instance에서 valid contract만 수집한다.
        /// Preview instance, RuntimePlaceholder 원본, WorldMapZoneContentMarker object는 제외한다.
        /// </summary>
        private static List<WorldMapRuntimeFinalContentContract> CollectValidContracts(IReadOnlyList<WorldMapRuntimeSpawnGameplayAdapter> allAdapters)
        {
            var validContracts = new List<WorldMapRuntimeFinalContentContract>();

            if (allAdapters == null)
                return validContracts;

            foreach (WorldMapRuntimeSpawnGameplayAdapter adapter in allAdapters)
            {
                if (adapter == null || adapter.gameObject == null)
                    continue;

                // RuntimeSpawnedInstances 하위만 대상
                if (!IsUnderRuntimeSpawnedInstances(adapter.transform))
                    continue;

                // Preview instance 제외
                WorldMapRuntimeSpawnInstanceTag tag = adapter.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                if (tag != null && tag.IsPreviewInstance)
                    continue;

                // RuntimePlaceholderBinding이 있는 원본 placeholder 제외
                if (adapter.GetComponent<WorldMapRuntimePlaceholderBinding>() != null)
                    continue;

                // WorldMapZoneContentMarker가 붙은 marker/debug object 제외
                if (adapter.GetComponent<WorldMapZoneContentMarker>() != null)
                    continue;

                // Contract 획득
                WorldMapRuntimeFinalContentContract contract = adapter.GetComponent<WorldMapRuntimeFinalContentContract>();
                if (contract == null)
                    continue;

                validContracts.Add(contract);
            }

            return validContracts;
        }

        /// <summary>
        /// Contract 기반 unique candidate key를 생성한다.
        /// finalContentKind + runtimeCategory + runtimeKey + profileId 조합.
        /// </summary>
        private static string BuildCandidateKey(WorldMapRuntimeFinalContentContract contract)
        {
            string kindStr = contract.FinalContentKind.ToString();
            string catStr = string.IsNullOrEmpty(contract.RuntimeCategory) ? "None" : contract.RuntimeCategory;
            string keyStr = string.IsNullOrEmpty(contract.RuntimeKey) ? "None" : contract.RuntimeKey;
            string profStr = string.IsNullOrEmpty(contract.ProfileId) ? "None" : contract.ProfileId;
            return $"{kindStr}|{catStr}|{keyStr}|{profStr}";
        }

        /// <summary>
        /// Contract 기반 requirementId를 생성한다.
        /// AssetDatabase-safe하게 sanitize한다.
        /// </summary>
        private static string BuildRequirementId(WorldMapRuntimeFinalContentContract contract)
        {
            string kindStr = contract.FinalContentKind.ToString();
            string catStr = string.IsNullOrEmpty(contract.RuntimeCategory) ? "None" : SanitizeForAssetName(contract.RuntimeCategory);
            string keyStr = string.IsNullOrEmpty(contract.RuntimeKey) ? "None" : SanitizeForAssetName(contract.RuntimeKey);
            string profStr = string.IsNullOrEmpty(contract.ProfileId) ? "None" : SanitizeForAssetName(contract.ProfileId);
            return $"Final_{kindStr}_{catStr}_{keyStr}_{profStr}";
        }

        /// <summary>
        /// Contract 기반 requirement entry를 생성한다.
        /// finalPrefab/finalProfile은 null로 둔다.
        /// </summary>
        private static WorldMapRuntimeFinalContentRequirementEntry CreateEntryFromContract(
            WorldMapRuntimeFinalContentContract contract,
            string requirementId,
            int contractCount)
        {
            WorldMapRuntimeFinalContentRequirementEntry entry = new WorldMapRuntimeFinalContentRequirementEntry();

            entry.RequirementId = requirementId;
            entry.FinalContentKind = contract.FinalContentKind;
            entry.AdapterKind = contract.AdapterKind;
            entry.RuntimeCategory = contract.RuntimeCategory;
            entry.RuntimeKey = contract.RuntimeKey;
            entry.ProfileId = contract.ProfileId;
            entry.MatchKey = BuildCandidateKey(contract);
            entry.DisplayName = $"{contract.FinalContentKind} - {contract.RuntimeCategory}/{contract.RuntimeKey} ({contract.ProfileId})";
            entry.Description = $"Auto-generated requirement for {contract.FinalContentKind} | " +
                $"Category={contract.RuntimeCategory} Key={contract.RuntimeKey} ProfileId={contract.ProfileId} | " +
                $"Matched {contractCount} contract(s)";
            entry.FinalPrefab = null; // 이번 단계에서는 null
            entry.FinalProfile = null; // 이번 단계에서는 null
            entry.IsFallbackRequirement = false;
            entry.AllowFallbackUse = true;
            entry.Priority = ExactPriority;
            entry.MatchTags = new string[] { contract.RuntimeCategory, contract.RuntimeKey };

            return entry;
        }

        /// <summary>
        /// 기본 fallback requirement를 보장한다.
        /// finalContentKind별로 IsFallbackRequirement=true인 entry가 없으면 추가한다.
        /// </summary>
        private static void EnsureFallbackRequirements(
            List<WorldMapRuntimeFinalContentRequirementEntry> entries,
            Dictionary<string, WorldMapRuntimeFinalContentRequirementEntry> existingById)
        {
            // 필수 fallback finalContentKind 목록
            var fallbackKinds = new (WorldMapRuntimeFinalContentKind kind, string displayName, string description)[]
            {
                (WorldMapRuntimeFinalContentKind.HarvestResource, "Harvest Resource Fallback", "Fallback requirement for HarvestResource final content"),
                (WorldMapRuntimeFinalContentKind.HazardVolume, "Hazard Volume Fallback", "Fallback requirement for HazardVolume final content"),
                (WorldMapRuntimeFinalContentKind.LandmarkProp, "Landmark Prop Fallback", "Fallback requirement for LandmarkProp final content"),
                (WorldMapRuntimeFinalContentKind.NarrativeLog, "Narrative Log Fallback", "Fallback requirement for NarrativeLog final content"),
                (WorldMapRuntimeFinalContentKind.RouteTrigger, "Route Trigger Fallback", "Fallback requirement for RouteTrigger final content"),
                (WorldMapRuntimeFinalContentKind.DebugMarker, "Debug Marker Fallback", "Fallback requirement for DebugMarker final content"),
                (WorldMapRuntimeFinalContentKind.Unknown, "Unknown Kind Fallback", "Fallback requirement for Unknown final content"),
            };

            foreach (var (kind, displayName, description) in fallbackKinds)
            {
                string fallbackId = $"Fallback_Final_{kind}";

                // 이미 존재하는지 확인
                if (existingById.ContainsKey(fallbackId))
                    continue;

                // 이미 entries에 있는지 확인
                bool alreadyExists = false;
                foreach (WorldMapRuntimeFinalContentRequirementEntry e in entries)
                {
                    if (e != null && e.RequirementId == fallbackId)
                    {
                        alreadyExists = true;
                        break;
                    }
                }
                if (alreadyExists)
                    continue;

                // 새 fallback entry 생성
                WorldMapRuntimeFinalContentRequirementEntry fallbackEntry = new WorldMapRuntimeFinalContentRequirementEntry();
                fallbackEntry.RequirementId = fallbackId;
                fallbackEntry.FinalContentKind = kind;
                fallbackEntry.AdapterKind = WorldMapRuntimeGameplayAdapterKind.None;
                fallbackEntry.RuntimeCategory = null; // 모든 category 허용
                fallbackEntry.RuntimeKey = null; // 모든 key 허용
                fallbackEntry.ProfileId = null; // 모든 profileId 허용
                fallbackEntry.MatchKey = $"Fallback_{kind}";
                fallbackEntry.DisplayName = displayName;
                fallbackEntry.Description = description;
                fallbackEntry.FinalPrefab = null;
                fallbackEntry.FinalProfile = null;
                fallbackEntry.IsFallbackRequirement = true;
                fallbackEntry.AllowFallbackUse = true;
                fallbackEntry.Priority = FallbackPriority;
                fallbackEntry.MatchTags = null;

                entries.Add(fallbackEntry);
                Debug.Log($"[MapAutoBuilder] [M-2] Added fallback requirement: '{fallbackId}' for {kind}.");
            }
        }

        /// <summary>
        /// AssetDatabase-safe한 이름으로 sanitize한다.
        /// 특수문자를 언더스코어로 치환한다.
        /// </summary>
        private static string SanitizeForAssetName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "None";

            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
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
    }
}
