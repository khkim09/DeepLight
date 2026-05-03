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
    /// Phase 14.10-I-1: Runtime Spawn Profile Database의 prefab slot 상태를 검사하고,
    /// 추후 연결해야 할 missing prefab 목록을 로그로 출력하는 Editor Utility.
    /// scene object를 절대 수정하지 않으며, database asset도 수정하지 않는다.
    /// 읽기 전용 검증만 수행한다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnProfilePrefabValidationUtility
    {
        private const string LogPrefix = "[SpawnProfilePrefabValidation]";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeSpawnProfileDatabase.asset";

        // ===== Public API =====

        /// <summary>
        /// Runtime Spawn Profile Database의 모든 entry에 대해 prefab slot 상태를 검사한다.
        /// 검사 항목:
        /// 1. Database asset exists.
        /// 2. Entry count > 0.
        /// 3. 모든 entry ProfileId non-empty.
        /// 4. 모든 entry MarkerType이 Unknown만으로 몰려있지 않은지 검사.
        /// 5. 모든 non-fallback exact/key entry 중 prefab null count 출력.
        /// 6. fallback entry의 prefab null은 WARN으로만 처리.
        /// 7. exact/key entry의 prefab null은 WARN으로 처리 (FAIL 아님).
        /// 8. prefab이 연결되어 있는 entry가 있다면 추가 검사 (INFO/WARN만 출력).
        /// 9. duplicate prefab reference가 있으면 WARN.
        /// 10. category별 prefab 연결 상태 출력.
        /// 11. MarkerType별 prefab 연결 상태 출력.
        /// 12. 최종 Summary.
        /// </summary>
        /// <param name="settings">AutoBuilder Settings (null 체크용)</param>
        /// <param name="context">AutoBuilder Scene Context (null 체크용)</param>
        public static void ValidateRuntimeSpawnProfilePrefabSlots(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot validate prefab slots.");
                return;
            }

            if (context == null)
            {
                Debug.LogError($"{LogPrefix} Context is null! Cannot validate prefab slots.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-I-1: Validate Runtime Spawn Profile Prefab Slots =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. Database asset 존재 확인
            WorldMapRuntimeSpawnProfileDatabaseSO database = LoadDatabaseAsset();
            if (database != null)
            {
                log.AppendLine("[PASS] RuntimeSpawnProfileDatabase asset exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("[FAIL] RuntimeSpawnProfileDatabase asset not found! Path: " + DatabaseAssetPath);
                failCount++;
                LogSummary(log, passCount, failCount, warnCount);
                Debug.LogWarning(log.ToString());
                return;
            }

            IReadOnlyList<WorldMapRuntimeSpawnProfileEntry> entries = database.Entries;

            // 2. Entry count > 0
            if (entries != null && entries.Count > 0)
            {
                log.AppendLine($"[PASS] Entries count: {entries.Count} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("[FAIL] Entries count is 0! No entries to validate.");
                failCount++;
                LogSummary(log, passCount, failCount, warnCount);
                Debug.LogWarning(log.ToString());
                return;
            }

            // 3. 모든 entry ProfileId non-empty
            bool allProfileIdsValid = true;
            List<string> emptyProfileIdEntries = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                WorldMapRuntimeSpawnProfileEntry e = entries[i];
                if (e == null)
                {
                    emptyProfileIdEntries.Add($"index={i} (null entry)");
                    allProfileIdsValid = false;
                    continue;
                }
                if (string.IsNullOrEmpty(e.ProfileId))
                {
                    emptyProfileIdEntries.Add($"index={i}, Category={e.RuntimeCategory}, Key={e.RuntimeKey}");
                    allProfileIdsValid = false;
                }
            }
            if (allProfileIdsValid)
            {
                log.AppendLine("[PASS] All entries have non-empty ProfileId.");
                passCount++;
            }
            else
            {
                log.AppendLine($"[FAIL] {emptyProfileIdEntries.Count} entry/entries have empty ProfileId:");
                foreach (string info in emptyProfileIdEntries)
                {
                    log.AppendLine($"       - {info}");
                }
                failCount++;
            }

            // 4. 모든 entry MarkerType이 Unknown만으로 몰려있지 않은지 검사
            int unknownCount = 0;
            int nonUnknownCount = 0;
            foreach (WorldMapRuntimeSpawnProfileEntry e in entries)
            {
                if (e == null) continue;
                if (e.MarkerType == WorldMapZoneContentMarkerType.Unknown)
                {
                    unknownCount++;
                }
                else
                {
                    nonUnknownCount++;
                }
            }
            if (unknownCount > 0 && nonUnknownCount == 0)
            {
                log.AppendLine($"[WARN] All {unknownCount} entries have MarkerType=Unknown. Expected at least some entries with specific MarkerType.");
                warnCount++;
            }
            else if (unknownCount > 0 && nonUnknownCount > 0)
            {
                log.AppendLine($"[INFO] MarkerType distribution: Unknown={unknownCount}, Specific={nonUnknownCount} (mixed, OK).");
                passCount++;
            }
            else
            {
                log.AppendLine("[PASS] No entries with MarkerType=Unknown.");
                passCount++;
            }

            // 5-7. Prefab null 검사 (fallback vs exact/key 구분)
            int fallbackNullCount = 0;
            int exactNullCount = 0;
            int fallbackAssignedCount = 0;
            int exactAssignedCount = 0;

            // 8. prefab이 연결된 entry 추가 검사
            int prefabAssignedCount = 0;
            int prefabSceneObjectCount = 0;
            int prefabWithColliderCount = 0;
            int prefabWithoutMonoBehaviourCount = 0;

            // 9. duplicate prefab reference 검사
            Dictionary<string, int> prefabPathCount = new Dictionary<string, int>();

            // 10. category별 통계
            Dictionary<string, CategoryPrefabStats> categoryStats = new Dictionary<string, CategoryPrefabStats>();

            // 11. MarkerType별 통계
            Dictionary<WorldMapZoneContentMarkerType, MarkerTypePrefabStats> markerTypeStats = new Dictionary<WorldMapZoneContentMarkerType, MarkerTypePrefabStats>();

            foreach (WorldMapRuntimeSpawnProfileEntry e in entries)
            {
                if (e == null) continue;

                // Category 통계 수집
                string category = !string.IsNullOrEmpty(e.RuntimeCategory) ? e.RuntimeCategory : "Unknown";
                if (!categoryStats.ContainsKey(category))
                {
                    categoryStats[category] = new CategoryPrefabStats();
                }
                categoryStats[category].TotalCount++;

                // MarkerType 통계 수집
                WorldMapZoneContentMarkerType markerType = e.MarkerType;
                if (!markerTypeStats.ContainsKey(markerType))
                {
                    markerTypeStats[markerType] = new MarkerTypePrefabStats();
                }
                markerTypeStats[markerType].TotalCount++;

                if (e.Prefab == null)
                {
                    // Prefab null
                    if (e.IsFallbackProfile)
                    {
                        fallbackNullCount++;
                        log.AppendLine($"[WARN] Prefab missing (fallback): {e.ProfileId}");
                        warnCount++;
                    }
                    else
                    {
                        exactNullCount++;
                        log.AppendLine($"[WARN] Prefab missing (exact/key): {e.ProfileId}");
                        warnCount++;
                    }
                }
                else
                {
                    // Prefab assigned
                    if (e.IsFallbackProfile)
                    {
                        fallbackAssignedCount++;
                    }
                    else
                    {
                        exactAssignedCount++;
                    }

                    prefabAssignedCount++;
                    categoryStats[category].AssignedCount++;
                    markerTypeStats[markerType].AssignedCount++;

                    // 8a. prefab asset이 null이 아닌지 (이미 위에서 체크됨)

                    // 8b. prefab이 scene object가 아닌 asset prefab인지
                    bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(e.Prefab)
                        || AssetDatabase.Contains(e.Prefab);
                    if (!isPrefabAsset)
                    {
                        log.AppendLine($"[WARN] Prefab '{e.Prefab.name}' (ProfileId: {e.ProfileId}) is a scene object, not a prefab asset.");
                        prefabSceneObjectCount++;
                        warnCount++;
                    }

                    // 8c. prefab에 Collider가 있어도 이번 단계에서는 FAIL 처리하지 말고 INFO/WARN만 출력
                    Collider collider = e.Prefab.GetComponent<Collider>();
                    if (collider != null)
                    {
                        log.AppendLine($"[INFO] Prefab '{e.Prefab.name}' (ProfileId: {e.ProfileId}) has Collider component (expected for gameplay prefabs).");
                        prefabWithColliderCount++;
                    }

                    // 8d. prefab에 MonoBehaviour가 없어도 이번 단계에서는 FAIL 처리하지 말고 INFO만 출력
                    MonoBehaviour[] monoBehaviours = e.Prefab.GetComponents<MonoBehaviour>();
                    if (monoBehaviours == null || monoBehaviours.Length == 0)
                    {
                        log.AppendLine($"[INFO] Prefab '{e.Prefab.name}' (ProfileId: {e.ProfileId}) has no MonoBehaviour components (may be a pure visual prefab).");
                        prefabWithoutMonoBehaviourCount++;
                    }

                    // 9. duplicate prefab reference 검사
                    string prefabPath = AssetDatabase.GetAssetPath(e.Prefab);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        if (prefabPathCount.ContainsKey(prefabPath))
                        {
                            prefabPathCount[prefabPath]++;
                        }
                        else
                        {
                            prefabPathCount[prefabPath] = 1;
                        }
                    }
                }
            }

            // 5-7. Prefab null 요약
            int totalNullCount = fallbackNullCount + exactNullCount;
            if (totalNullCount > 0)
            {
                log.AppendLine($"[INFO] Prefab null summary: fallback={fallbackNullCount}, exact/key={exactNullCount}, total={totalNullCount}");
            }
            else
            {
                log.AppendLine("[PASS] All entries have non-null Prefab.");
                passCount++;
            }

            // 8. Prefab assigned entry 요약
            if (prefabAssignedCount > 0)
            {
                log.AppendLine($"[INFO] Prefab assigned entries: {prefabAssignedCount}");
                if (prefabSceneObjectCount > 0)
                {
                    log.AppendLine($"[WARN] {prefabSceneObjectCount} assigned prefab(s) are scene objects, not prefab assets.");
                }
                if (prefabWithColliderCount > 0)
                {
                    log.AppendLine($"[INFO] {prefabWithColliderCount} assigned prefab(s) have Collider component.");
                }
                if (prefabWithoutMonoBehaviourCount > 0)
                {
                    log.AppendLine($"[INFO] {prefabWithoutMonoBehaviourCount} assigned prefab(s) have no MonoBehaviour.");
                }
            }
            else
            {
                log.AppendLine("[INFO] No prefab assigned yet (expected at this phase).");
            }

            // 9. Duplicate prefab reference 검사
            int duplicatePrefabCount = 0;
            foreach (KeyValuePair<string, int> kvp in prefabPathCount)
            {
                if (kvp.Value > 1)
                {
                    log.AppendLine($"[WARN] Duplicate prefab reference: '{kvp.Key}' used by {kvp.Value} entries.");
                    duplicatePrefabCount++;
                    warnCount++;
                }
            }
            if (duplicatePrefabCount == 0 && prefabAssignedCount > 0)
            {
                log.AppendLine("[PASS] No duplicate prefab references found.");
                passCount++;
            }

            // 10. Category별 prefab 연결 상태 출력
            log.AppendLine("[INFO] Category breakdown:");
            foreach (KeyValuePair<string, CategoryPrefabStats> kvp in categoryStats)
            {
                int missingCount = kvp.Value.TotalCount - kvp.Value.AssignedCount;
                log.AppendLine($"       Category {kvp.Key}: total={kvp.Value.TotalCount}, assigned={kvp.Value.AssignedCount}, missing={missingCount}");
            }

            // 11. MarkerType별 prefab 연결 상태 출력
            log.AppendLine("[INFO] MarkerType breakdown:");
            foreach (KeyValuePair<WorldMapZoneContentMarkerType, MarkerTypePrefabStats> kvp in markerTypeStats)
            {
                int missingCount = kvp.Value.TotalCount - kvp.Value.AssignedCount;
                log.AppendLine($"       MarkerType {kvp.Key}: total={kvp.Value.TotalCount}, assigned={kvp.Value.AssignedCount}, missing={missingCount}");
            }

            // 12. 최종 Summary
            int totalEntries = entries.Count;
            int fallbackEntries = fallbackNullCount + fallbackAssignedCount;
            int exactKeyEntries = exactNullCount + exactAssignedCount;
            int assignedEntries = prefabAssignedCount;
            int missingEntries = totalNullCount;

            log.AppendLine($"=== Summary: PASS={passCount}, FAIL={failCount}, WARN={warnCount} ===");
            log.AppendLine($"  Total entries: {totalEntries}");
            log.AppendLine($"  Fallback entries: {fallbackEntries}");
            log.AppendLine($"  Exact/key entries: {exactKeyEntries}");
            log.AppendLine($"  Assigned prefab entries: {assignedEntries}");
            log.AppendLine($"  Missing prefab entries: {missingEntries}");

            log.AppendLine("===== Phase 14.10-I-1: Validate Runtime Spawn Profile Prefab Slots Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// Database asset을 로드한다. 없으면 null 반환.
        /// </summary>
        private static WorldMapRuntimeSpawnProfileDatabaseSO LoadDatabaseAsset()
        {
            return AssetDatabase.LoadAssetAtPath<WorldMapRuntimeSpawnProfileDatabaseSO>(DatabaseAssetPath);
        }

        /// <summary>
        /// 검증 실패 시 간략 Summary를 로그에 추가한다.
        /// </summary>
        private static void LogSummary(StringBuilder log, int passCount, int failCount, int warnCount)
        {
            log.AppendLine($"=== Summary: PASS={passCount}, FAIL={failCount}, WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-I-1: Validate Runtime Spawn Profile Prefab Slots Complete =====");
        }

        // ===== Internal Stats Classes =====

        /// <summary>
        /// Category별 prefab 연결 통계.
        /// </summary>
        private class CategoryPrefabStats
        {
            public int TotalCount = 0;
            public int AssignedCount = 0;
        }

        /// <summary>
        /// MarkerType별 prefab 연결 통계.
        /// </summary>
        private class MarkerTypePrefabStats
        {
            public int TotalCount = 0;
            public int AssignedCount = 0;
        }
    }
}
