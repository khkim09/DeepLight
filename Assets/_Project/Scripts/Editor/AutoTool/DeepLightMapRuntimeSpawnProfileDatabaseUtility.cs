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
    /// Phase 14.10-H-2: Runtime Spawn Profile Database Utility.
    /// WorldMapRuntimeSpawnProfileDatabaseSO asset을 생성/갱신하고,
    /// Candidate 369개가 모두 어떤 profile entry에 매칭 가능한지 검증한다.
    /// 실제 prefab instantiate/replace는 하지 않으며, scene object를 변경하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnProfileDatabaseUtility
    {
        private const string LogPrefix = "[SpawnProfileDB]";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeSpawnProfileDatabase.asset";

        // ===== Public API =====

        /// <summary>
        /// 기본 Runtime Spawn Profile Database asset을 생성하거나 갱신한다.
        /// asset이 없으면 새로 생성하고, 있으면 재사용한다.
        /// 기본 fallback/profile entries를 보장하며, 기존 사용자가 수동 추가한 entry는 삭제하지 않는다.
        /// </summary>
        public static void CreateOrUpdateDefaultRuntimeSpawnProfileDatabase(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot create/update database.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-H-2: Create/Update Runtime Spawn Profile Database =====");

            // 1. Database asset 찾기 또는 생성
            WorldMapRuntimeSpawnProfileDatabaseSO database = FindOrCreateDatabaseAsset();
            if (database == null)
            {
                Debug.LogError($"{LogPrefix} Failed to find or create database asset.");
                return;
            }
            log.AppendLine($"  [OK] Database asset: {AssetDatabase.GetAssetPath(database)}");

            // 2. 기존 entries 보존하면서 기본 entries 추가
            List<WorldMapRuntimeSpawnProfileEntry> entries = database.GetEditableEntries();

            // 기존 ProfileId 목록 (중복 방지)
            HashSet<string> existingProfileIds = new HashSet<string>();
            foreach (WorldMapRuntimeSpawnProfileEntry existing in entries)
            {
                if (existing != null && !string.IsNullOrEmpty(existing.ProfileId))
                {
                    existingProfileIds.Add(existing.ProfileId);
                }
            }

            int addedCount = 0;
            int skippedCount = 0;

            // 3. 기본 fallback entries 생성
            WorldMapRuntimeSpawnProfileEntry[] defaultFallbackEntries = CreateDefaultFallbackEntries();
            foreach (WorldMapRuntimeSpawnProfileEntry fallbackEntry in defaultFallbackEntries)
            {
                if (existingProfileIds.Contains(fallbackEntry.ProfileId))
                {
                    skippedCount++;
                    continue;
                }

                entries.Add(fallbackEntry);
                existingProfileIds.Add(fallbackEntry.ProfileId);
                addedCount++;
            }

            // 4. 기본 exact/key entries 생성
            WorldMapRuntimeSpawnProfileEntry[] defaultExactEntries = CreateDefaultExactEntries();
            foreach (WorldMapRuntimeSpawnProfileEntry exactEntry in defaultExactEntries)
            {
                if (existingProfileIds.Contains(exactEntry.ProfileId))
                {
                    skippedCount++;
                    continue;
                }

                entries.Add(exactEntry);
                existingProfileIds.Add(exactEntry.ProfileId);
                addedCount++;
            }

            // 5. 변경 사항 저장
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            log.AppendLine($"  [OK] Total entries: {entries.Count}");
            log.AppendLine($"  [OK] Added: {addedCount}, Skipped (already exists): {skippedCount}");
            log.AppendLine($"  [OK] Fallback entries: {defaultFallbackEntries.Length}");
            log.AppendLine($"  [OK] Exact/key entries: {defaultExactEntries.Length}");
            log.AppendLine("===== Phase 14.10-H-2: Create/Update Runtime Spawn Profile Database Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Runtime Spawn Profile Database의 유효성을 검사한다.
        /// DB 존재, entries count, 중복 검사, fallback coverage, candidate 매칭 등을 검증한다.
        /// scene object를 변경하지 않는다.
        /// </summary>
        public static void ValidateRuntimeSpawnProfileDatabase(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot validate database.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-H-2: Validate Runtime Spawn Profile Database =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. DB asset 존재 확인
            WorldMapRuntimeSpawnProfileDatabaseSO database = LoadDatabaseAsset();
            if (database != null)
            {
                log.AppendLine("  [PASS] RuntimeSpawnProfileDatabase asset exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] RuntimeSpawnProfileDatabase asset not found! Run Create/Update first.");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-H-2: Validate Runtime Spawn Profile Database Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            IReadOnlyList<WorldMapRuntimeSpawnProfileEntry> entries = database.Entries;

            // 2. entries count > 0
            if (entries != null && entries.Count > 0)
            {
                log.AppendLine($"  [PASS] Entries count: {entries.Count} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] Entries count is 0!");
                failCount++;
            }

            // 3. duplicate ProfileId 없음
            if (!database.HasDuplicateProfileIds(out string duplicateId))
            {
                log.AppendLine("  [PASS] No duplicate ProfileId found.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Duplicate ProfileId found: '{duplicateId}'.");
                failCount++;
            }

            // 4. duplicate exact match key 없음
            if (!database.HasDuplicateExactMatchKeys(out string duplicateKey))
            {
                log.AppendLine("  [PASS] No duplicate exact match keys found.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Duplicate exact match key found: '{duplicateKey}'.");
                failCount++;
            }

            // 5. fallback entry가 RuntimeCategory별로 존재
            // 실제 candidate RuntimeCategory 기준으로 검사:
            //   HarvestResource, Hazard, Landmark, NarrativeLog, RouteTrigger, Debug, Unknown
            // WorldProp은 현재 candidate category가 아니므로 필수 검사에서 제외.
            // 단, 기존 Fallback_WorldProp entry가 있다면 보존 가능.
            string[] expectedCategories = { "HarvestResource", "Hazard", "Landmark", "NarrativeLog", "RouteTrigger", "Debug", "Unknown" };
            int missingFallbackCategories = 0;
            foreach (string category in expectedCategories)
            {
                if (!database.HasFallbackForCategory(category))
                {
                    log.AppendLine($"  [WARN] No fallback entry for RuntimeCategory '{category}'.");
                    warnCount++;
                    missingFallbackCategories++;
                }
            }
            if (missingFallbackCategories == 0)
            {
                log.AppendLine("  [PASS] All expected RuntimeCategories have fallback entries.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [INFO] {missingFallbackCategories} category/categories missing fallback entries (WARN only).");
                // passCount는 증가시키지 않음 (WARN 처리)
            }

            // 6. Prefab null entry는 WARN 처리
            int nullPrefabCount = 0;
            if (entries != null)
            {
                foreach (WorldMapRuntimeSpawnProfileEntry e in entries)
                {
                    if (e != null && e.Prefab == null)
                    {
                        nullPrefabCount++;
                    }
                }
            }
            if (nullPrefabCount > 0)
            {
                log.AppendLine($"  [WARN] {nullPrefabCount} entry/entries have null Prefab (expected at this phase).");
                warnCount++;
            }
            else
            {
                log.AppendLine("  [PASS] All entries have non-null Prefab.");
                passCount++;
            }

            // 7. Candidate 369개 매칭 검증
            log.AppendLine("  [INFO] Validating candidate matching against database...");
            ValidateCandidateMatching(settings, context, database, log, ref passCount, ref failCount, ref warnCount);

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-H-2: Validate Runtime Spawn Profile Database Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal: Candidate Matching Validation =====

        /// <summary>
        /// H-1 resolver로 candidate를 다시 생성하고 DB TryFindBestMatch를 실행하여
        /// 모든 candidate가 최소 하나의 profile entry에 매칭되는지 확인한다.
        /// category별 matched/fallback/noMatch count를 출력한다.
        /// </summary>
        private static void ValidateCandidateMatching(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context,
            WorldMapRuntimeSpawnProfileDatabaseSO database,
            StringBuilder log,
            ref int passCount,
            ref int failCount,
            ref int warnCount)
        {
            // GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine("  [FAIL] GeneratedWorldRoot not found. Cannot validate candidate matching.");
                failCount++;
                return;
            }

            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                log.AppendLine($"  [FAIL] '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                failCount++;
                return;
            }

            // 모든 RuntimePlaceholder 수집
            List<GameObject> allPlaceholders = CollectAllRuntimePlaceholders(zoneRootsTransform);
            log.AppendLine($"  [INFO] Found {allPlaceholders.Count} RuntimePlaceholder GameObjects for matching validation.");

            if (allPlaceholders.Count == 0)
            {
                log.AppendLine("  [FAIL] No RuntimePlaceholder GameObjects found. Cannot validate candidate matching.");
                failCount++;
                return;
            }

            // 매칭 통계
            int totalCandidates = 0;
            int totalMatched = 0;
            int totalFallbackMatched = 0;
            int totalNoMatch = 0;
            int totalNoBinding = 0;

            // category별 통계
            Dictionary<string, CategoryMatchStats> categoryStats = new Dictionary<string, CategoryMatchStats>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                WorldMapRuntimePlaceholderBinding binding = placeholder.GetComponent<WorldMapRuntimePlaceholderBinding>();
                if (binding == null)
                {
                    totalNoBinding++;
                    continue;
                }

                // H-1 resolver로 candidate 생성
                if (!WorldMapRuntimeSpawnProfileResolver.TryResolve(binding, out WorldMapRuntimeSpawnProfileCandidate candidate))
                {
                    totalNoMatch++;
                    continue;
                }

                totalCandidates++;

                // DB 매칭 시도
                bool matched = database.TryFindBestMatch(candidate, out WorldMapRuntimeSpawnProfileEntry matchedEntry);

                string category = !string.IsNullOrEmpty(candidate.RuntimeCategory) ? candidate.RuntimeCategory : "Unknown";

                if (!categoryStats.ContainsKey(category))
                {
                    categoryStats[category] = new CategoryMatchStats();
                }

                if (matched && matchedEntry != null)
                {
                    if (matchedEntry.IsFallbackProfile)
                    {
                        totalFallbackMatched++;
                        categoryStats[category].FallbackCount++;
                    }
                    else
                    {
                        totalMatched++;
                        categoryStats[category].MatchedCount++;
                    }
                }
                else
                {
                    totalNoMatch++;
                    categoryStats[category].NoMatchCount++;
                }
            }

            // 결과 출력
            log.AppendLine($"  [INFO] Total candidates: {totalCandidates}");
            log.AppendLine($"  [INFO] Exact matched: {totalMatched}");
            log.AppendLine($"  [INFO] Fallback matched: {totalFallbackMatched}");
            log.AppendLine($"  [INFO] No match: {totalNoMatch}");
            log.AppendLine($"  [INFO] No binding component: {totalNoBinding}");

            // category별 count 출력
            log.AppendLine("  [INFO] Category breakdown:");
            foreach (KeyValuePair<string, CategoryMatchStats> kvp in categoryStats)
            {
                log.AppendLine($"         {kvp.Key}: matched={kvp.Value.MatchedCount}, fallback={kvp.Value.FallbackCount}, noMatch={kvp.Value.NoMatchCount}");
            }

            // 모든 candidate가 매칭되었는지 확인
            if (totalNoMatch == 0 && totalCandidates > 0)
            {
                log.AppendLine($"  [PASS] All {totalCandidates} candidates matched to a profile entry (exact: {totalMatched}, fallback: {totalFallbackMatched}).");
                passCount++;
            }
            else if (totalNoMatch > 0)
            {
                log.AppendLine($"  [FAIL] {totalNoMatch} candidate(s) have no matching profile entry.");
                failCount++;
            }
            else
            {
                log.AppendLine("  [WARN] No candidates found to validate.");
                warnCount++;
            }
        }

        // ===== Internal: Database Asset Management =====

        /// <summary>
        /// Database asset을 찾거나 새로 생성한다.
        /// </summary>
        private static WorldMapRuntimeSpawnProfileDatabaseSO FindOrCreateDatabaseAsset()
        {
            // 기존 asset 검색
            WorldMapRuntimeSpawnProfileDatabaseSO existing = LoadDatabaseAsset();
            if (existing != null)
            {
                return existing;
            }

            // 폴더가 없으면 생성
            string folderPath = "Assets/_Project/ScriptableObjects/World/Content";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parent = "Assets/_Project/ScriptableObjects/World";
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "World");
                }
                AssetDatabase.CreateFolder(parent, "Content");
            }

            // 새 asset 생성
            WorldMapRuntimeSpawnProfileDatabaseSO database = ScriptableObject.CreateInstance<WorldMapRuntimeSpawnProfileDatabaseSO>();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            AssetDatabase.SaveAssets();

            return database;
        }

        /// <summary>
        /// Database asset을 로드한다. 없으면 null 반환.
        /// </summary>
        private static WorldMapRuntimeSpawnProfileDatabaseSO LoadDatabaseAsset()
        {
            return AssetDatabase.LoadAssetAtPath<WorldMapRuntimeSpawnProfileDatabaseSO>(DatabaseAssetPath);
        }

        // ===== Internal: Default Entry Creation =====

        /// <summary>
        /// 기본 fallback entry 배열을 생성한다.
        /// 각 RuntimeCategory별로 IsFallbackProfile=true인 entry를 만든다.
        /// Fallback_WorldProp은 기존 사용자 추가 entry 보존을 위해 유지하되,
        /// 실제 candidate category 기준으로 Fallback_Landmark를 추가한다.
        /// </summary>
        private static WorldMapRuntimeSpawnProfileEntry[] CreateDefaultFallbackEntries()
        {
            return new WorldMapRuntimeSpawnProfileEntry[]
            {
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_HarvestResource",
                    markerType: WorldMapZoneContentMarkerType.Resource,
                    runtimeCategory: "HarvestResource",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 10,
                    description: "Fallback for any HarvestResource candidate without exact key match"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_Hazard",
                    markerType: WorldMapZoneContentMarkerType.Hazard,
                    runtimeCategory: "Hazard",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 10,
                    description: "Fallback for any Hazard candidate without exact key match"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_WorldProp",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "WorldProp",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 10,
                    description: "Fallback for any WorldProp candidate without exact key match (legacy, preserved for user-added entries)"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_Landmark",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "Landmark",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 10,
                    description: "Fallback for any Landmark candidate without exact key match"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_NarrativeLog",
                    markerType: WorldMapZoneContentMarkerType.Narrative,
                    runtimeCategory: "NarrativeLog",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 10,
                    description: "Fallback for any NarrativeLog candidate without exact key match"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_RouteTrigger",
                    markerType: WorldMapZoneContentMarkerType.Route,
                    runtimeCategory: "RouteTrigger",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 10,
                    description: "Fallback for any RouteTrigger candidate without exact key match"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_Debug",
                    markerType: WorldMapZoneContentMarkerType.Debug,
                    runtimeCategory: "Debug",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 10,
                    description: "Fallback for any Debug candidate without exact key match"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Fallback_Unknown",
                    markerType: WorldMapZoneContentMarkerType.Unknown,
                    runtimeCategory: "Unknown",
                    runtimeKey: "",
                    matchTags: null,
                    prefab: null,
                    isFallbackProfile: true,
                    priority: 1,
                    description: "Ultimate fallback for any candidate that doesn't match other entries"
                )
            };
        }

        /// <summary>
        /// 기본 exact/key entry 배열을 생성한다.
        /// 특정 RuntimeCategory + RuntimeKey 조합에 대한 정확 매칭 entry를 만든다.
        /// </summary>
        private static WorldMapRuntimeSpawnProfileEntry[] CreateDefaultExactEntries()
        {
            return new WorldMapRuntimeSpawnProfileEntry[]
            {
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "HarvestResource_iron",
                    markerType: WorldMapZoneContentMarkerType.Resource,
                    runtimeCategory: "HarvestResource",
                    runtimeKey: "iron",
                    matchTags: new[] { "iron" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for HarvestResource/iron"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "HarvestResource_steel",
                    markerType: WorldMapZoneContentMarkerType.Resource,
                    runtimeCategory: "HarvestResource",
                    runtimeKey: "steel",
                    matchTags: new[] { "steel" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for HarvestResource/steel"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "HarvestResource_fuel",
                    markerType: WorldMapZoneContentMarkerType.Resource,
                    runtimeCategory: "HarvestResource",
                    runtimeKey: "fuel",
                    matchTags: new[] { "fuel" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for HarvestResource/fuel"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Hazard_current",
                    markerType: WorldMapZoneContentMarkerType.Hazard,
                    runtimeCategory: "Hazard",
                    runtimeKey: "current",
                    matchTags: new[] { "current" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for Hazard/current"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Hazard_pressure",
                    markerType: WorldMapZoneContentMarkerType.Hazard,
                    runtimeCategory: "Hazard",
                    runtimeKey: "pressure",
                    matchTags: new[] { "pressure" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for Hazard/pressure"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "NarrativeLog_log",
                    markerType: WorldMapZoneContentMarkerType.Narrative,
                    runtimeCategory: "NarrativeLog",
                    runtimeKey: "log",
                    matchTags: new[] { "log" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for NarrativeLog/log"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "RouteTrigger_route",
                    markerType: WorldMapZoneContentMarkerType.Route,
                    runtimeCategory: "RouteTrigger",
                    runtimeKey: "route",
                    matchTags: new[] { "route" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for RouteTrigger/route"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "WorldProp_hub",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "WorldProp",
                    runtimeKey: "hub",
                    matchTags: new[] { "hub" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for WorldProp/hub (legacy, preserved for user-added entries)"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "WorldProp_wreck",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "WorldProp",
                    runtimeKey: "wreck",
                    matchTags: new[] { "wreck" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for WorldProp/wreck (legacy, preserved for user-added entries)"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "WorldProp_harbor",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "WorldProp",
                    runtimeKey: "harbor",
                    matchTags: new[] { "harbor" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for WorldProp/harbor (legacy, preserved for user-added entries)"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Landmark_hub",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "Landmark",
                    runtimeKey: "hub",
                    matchTags: new[] { "hub" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for Landmark/hub"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Landmark_wreck",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "Landmark",
                    runtimeKey: "wreck",
                    matchTags: new[] { "wreck" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for Landmark/wreck"
                ),
                new WorldMapRuntimeSpawnProfileEntry(
                    profileId: "Landmark_harbor",
                    markerType: WorldMapZoneContentMarkerType.Landmark,
                    runtimeCategory: "Landmark",
                    runtimeKey: "harbor",
                    matchTags: new[] { "harbor" },
                    prefab: null,
                    isFallbackProfile: false,
                    priority: 100,
                    description: "Exact match for Landmark/harbor"
                )
            };
        }

        // ===== Internal: Placeholder Collection =====

        /// <summary>
        /// ZoneRoots parent Transform 하위의 모든 RuntimePlaceholder_* GameObject를 재귀적으로 수집한다.
        /// DeepLightMapRuntimePlaceholderBindingUtility의 동일 메서드와 동일한 로직.
        /// </summary>
        private static List<GameObject> CollectAllRuntimePlaceholders(Transform zoneRootsTransform)
        {
            List<GameObject> result = new List<GameObject>();

            for (int i = 0; i < zoneRootsTransform.childCount; i++)
            {
                Transform zoneRoot = zoneRootsTransform.GetChild(i);
                if (zoneRoot == null) continue;
                if (!zoneRoot.name.StartsWith("ZoneRoot_")) continue;

                for (int j = 0; j < zoneRoot.childCount; j++)
                {
                    Transform childRoot = zoneRoot.GetChild(j);
                    if (childRoot == null) continue;

                    CollectPlaceholdersRecursive(childRoot, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Transform 하위를 재귀적으로 탐색하여 RuntimePlaceholder_ prefix를 가진 GameObject를 수집한다.
        /// </summary>
        private static void CollectPlaceholdersRecursive(Transform parent, List<GameObject> result)
        {
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null) continue;

                if (child.name.StartsWith("RuntimePlaceholder_"))
                {
                    result.Add(child.gameObject);
                }

                CollectPlaceholdersRecursive(child, result);
            }
        }

        // ===== Internal: Category Match Stats =====

        /// <summary>
        /// category별 매칭 통계를 저장하는 내부 클래스.
        /// </summary>
        private class CategoryMatchStats
        {
            public int MatchedCount = 0;
            public int FallbackCount = 0;
            public int NoMatchCount = 0;
        }
    }
}
