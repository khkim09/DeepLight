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
    /// Phase 14.10-J-1: Runtime Spawn Replacement Plan Utility.
    /// RuntimePlaceholder를 실제 prefab으로 바로 교체하지 않고,
    /// 어떤 prefab으로 치환될 예정인지 plan만 생성/검증한다.
    /// scene/database는 수정하지 않는다.
    /// 실제 Instantiate/Replace는 Phase 14.10-J-2 이후에 진행한다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnReplacementPlanUtility
    {
        private const string LogPrefix = "[SpawnReplacementPlan]";
        private const string PlaceholderPrefix = "RuntimePlaceholder";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeSpawnProfileDatabase.asset";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 RuntimePlaceholder를 순회하며
        /// WorldMapRuntimeSpawnReplacementResolver.TryBuildPlan을 호출하고 결과를 집계한다.
        /// "Rebuild"라는 이름이지만 scene/database를 수정하지 않으며, 계획 재생성/로그 출력 의미로 사용한다.
        /// </summary>
        public static void RebuildRuntimeSpawnReplacementPlans(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot rebuild replacement plans.");
                return;
            }
            if (context == null)
            {
                Debug.LogError($"{LogPrefix} Context is null! Cannot rebuild replacement plans.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-1: Rebuild Runtime Spawn Replacement Plans =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError($"{LogPrefix} GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. RuntimeSpawnProfileDatabase 로드
            WorldMapRuntimeSpawnProfileDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeSpawnProfileDatabaseSO>(DatabaseAssetPath);
            if (database == null)
            {
                Debug.LogError($"{LogPrefix} RuntimeSpawnProfileDatabase not found at '{DatabaseAssetPath}'. Run Phase 14.10-H-2 first.");
                return;
            }
            log.AppendLine($"  [OK] RuntimeSpawnProfileDatabase loaded: {database.name} (entries: {database.Entries.Count})");

            // 3. ZoneRoots parent 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"{LogPrefix} '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // 4. 모든 RuntimePlaceholder 수집 (중복 방지 HashSet 사용)
            HashSet<GameObject> placeholderSet = CollectAllRuntimePlaceholders(zoneRootsTransform);
            List<GameObject> allPlaceholders = new List<GameObject>(placeholderSet);
            log.AppendLine($"  [OK] Found {allPlaceholders.Count} unique RuntimePlaceholder GameObjects.");

            if (allPlaceholders.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No RuntimePlaceholder GameObjects found. Nothing to plan.");
                log.AppendLine("  [WARN] No RuntimePlaceholder GameObjects found.");
                Debug.Log(log.ToString());
                return;
            }

            // 5. 각 placeholder에 대해 plan build 실행
            int totalSuccess = 0;
            int totalFail = 0;
            int totalFallback = 0;
            int totalEditorPlaceholder = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // 샘플 plan 저장 (verbose 전용)
            List<string> samplePlans = new List<string>();
            const int maxSamples = 5;

            // 실패한 placeholder 목록
            List<string> failedPlaceholders = new List<string>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                if (WorldMapRuntimeSpawnReplacementResolver.TryBuildPlan(placeholder, database, out WorldMapRuntimeSpawnReplacementPlan plan))
                {
                    totalSuccess++;

                    if (plan.IsFallbackProfile)
                    {
                        totalFallback++;
                    }

                    if (plan.IsEditorPlaceholderPrefab)
                    {
                        totalEditorPlaceholder++;
                    }

                    // category count
                    string cat = !string.IsNullOrEmpty(plan.RuntimeCategory) ? plan.RuntimeCategory : "Unknown";
                    if (categoryCount.ContainsKey(cat))
                    {
                        categoryCount[cat]++;
                    }
                    else
                    {
                        categoryCount[cat] = 1;
                    }

                    // verbose 샘플 수집
                    if (settings.LogVerbose && samplePlans.Count < maxSamples)
                    {
                        samplePlans.Add(plan.GetSummary());
                    }
                }
                else
                {
                    totalFail++;
                    failedPlaceholders.Add($"{placeholder.name}: {plan.FailureReason}");
                }
            }

            // 6. 결과 로그
            log.AppendLine($"  Plans built: {totalSuccess}");
            log.AppendLine($"    - Normal: {totalSuccess - totalFallback}");
            log.AppendLine($"    - Fallback profile: {totalFallback}");
            log.AppendLine($"    - Editor placeholder prefab: {totalEditorPlaceholder}");
            log.AppendLine($"  Failed: {totalFail}");

            if (failedPlaceholders.Count > 0)
            {
                log.AppendLine("  Failed placeholders:");
                foreach (string failInfo in failedPlaceholders)
                {
                    log.AppendLine($"    [FAIL] {failInfo}");
                }
            }

            log.AppendLine("");

            // category별 count 출력
            log.AppendLine("  === Category Counts ===");
            foreach (KeyValuePair<string, int> kvp in categoryCount)
            {
                log.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }

            // verbose 샘플 출력
            if (settings.LogVerbose && samplePlans.Count > 0)
            {
                log.AppendLine("");
                log.AppendLine($"  === Sample Plans (first {maxSamples}) ===");
                foreach (string sample in samplePlans)
                {
                    log.AppendLine($"    {sample}");
                }
            }

            log.AppendLine("");
            log.AppendLine("===== Phase 14.10-J-1: Rebuild Runtime Spawn Replacement Plans Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 RuntimePlaceholder에 대해
        /// Replacement Plan의 유효성을 검사한다.
        /// 14개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN] summary를 출력한다.
        /// scene object transform/name 변경 없음을 검증한다.
        /// </summary>
        public static void ValidateRuntimeSpawnReplacementPlans(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot validate replacement plans.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-1: Validate Runtime Spawn Replacement Plans =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot 존재 확인
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null)
            {
                log.AppendLine("  [PASS] GeneratedWorldRoot exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] GeneratedWorldRoot not found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-1: Validate Runtime Spawn Replacement Plans Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. RuntimeSpawnProfileDatabase asset 존재 확인
            WorldMapRuntimeSpawnProfileDatabaseSO database = AssetDatabase.LoadAssetAtPath<WorldMapRuntimeSpawnProfileDatabaseSO>(DatabaseAssetPath);
            if (database != null)
            {
                log.AppendLine($"  [PASS] RuntimeSpawnProfileDatabase exists (entries: {database.Entries.Count}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] RuntimeSpawnProfileDatabase not found at '{DatabaseAssetPath}'!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-1: Validate Runtime Spawn Replacement Plans Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 3. ZoneRoots parent 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform != null)
            {
                log.AppendLine("  [PASS] ZoneRoots parent exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] ZoneRoots parent not found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-1: Validate Runtime Spawn Replacement Plans Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4. 모든 RuntimePlaceholder 수집
            HashSet<GameObject> placeholderSet = CollectAllRuntimePlaceholders(zoneRootsTransform);
            List<GameObject> allPlaceholders = new List<GameObject>(placeholderSet);

            if (allPlaceholders.Count > 0)
            {
                log.AppendLine($"  [PASS] RuntimePlaceholder count: {allPlaceholders.Count} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] No RuntimePlaceholder GameObjects found!");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-J-1: Validate Runtime Spawn Replacement Plans Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 5-14. 상세 검사
            int totalPlansBuilt = 0;
            int totalPlanFailed = 0;
            int emptyZoneId = 0;
            int emptyMarkerId = 0;
            int emptyProfileId = 0;
            int nullPrefab = 0;
            int emptyPrefabName = 0;
            int emptyPlaceholderName = 0;
            int fallbackCount = 0;
            int editorPlaceholderCount = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // 실패한 placeholder 목록 (최대 20개)
            List<string> failedPlaceholders = new List<string>();
            const int maxFailedDisplay = 20;

            // scene 변경 감지용 snapshot
            Dictionary<GameObject, Vector3> positionSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, Quaternion> rotationSnapshot = new Dictionary<GameObject, Quaternion>();
            Dictionary<GameObject, Vector3> scaleSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, string> nameSnapshot = new Dictionary<GameObject, string>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // snapshot 저장 (resolver 실행 전)
                positionSnapshot[placeholder] = placeholder.transform.position;
                rotationSnapshot[placeholder] = placeholder.transform.rotation;
                scaleSnapshot[placeholder] = placeholder.transform.localScale;
                nameSnapshot[placeholder] = placeholder.name;

                // plan build
                if (WorldMapRuntimeSpawnReplacementResolver.TryBuildPlan(placeholder, database, out WorldMapRuntimeSpawnReplacementPlan plan))
                {
                    totalPlansBuilt++;

                    // 5. 모든 plan ZoneId non-empty
                    if (string.IsNullOrEmpty(plan.ZoneId))
                    {
                        emptyZoneId++;
                    }

                    // 6. 모든 plan MarkerId non-empty
                    if (string.IsNullOrEmpty(plan.MarkerId))
                    {
                        emptyMarkerId++;
                    }

                    // 7. 모든 plan ProfileId non-empty
                    if (string.IsNullOrEmpty(plan.ProfileId))
                    {
                        emptyProfileId++;
                    }

                    // 8. 모든 plan Prefab non-null
                    if (plan.Prefab == null)
                    {
                        nullPrefab++;
                    }

                    // 9. 모든 plan PrefabName non-empty
                    if (string.IsNullOrEmpty(plan.PrefabName))
                    {
                        emptyPrefabName++;
                    }

                    // 10. 모든 plan PlaceholderName non-empty
                    if (string.IsNullOrEmpty(plan.PlaceholderName))
                    {
                        emptyPlaceholderName++;
                    }

                    // 11. fallback plan count
                    if (plan.IsFallbackProfile)
                    {
                        fallbackCount++;
                    }

                    // 12. editor placeholder prefab plan count
                    if (plan.IsEditorPlaceholderPrefab)
                    {
                        editorPlaceholderCount++;
                    }

                    // 13. category별 breakdown
                    string cat = !string.IsNullOrEmpty(plan.RuntimeCategory) ? plan.RuntimeCategory : "Unknown";
                    if (categoryCount.ContainsKey(cat))
                    {
                        categoryCount[cat]++;
                    }
                    else
                    {
                        categoryCount[cat] = 1;
                    }
                }
                else
                {
                    totalPlanFailed++;
                    if (failedPlaceholders.Count < maxFailedDisplay)
                    {
                        failedPlaceholders.Add($"{placeholder.name}: {plan.FailureReason}");
                    }
                }
            }

            // 14. scene object transform/name 변경 없음 확인
            int sceneObjectChanged = 0;
            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                if (positionSnapshot.TryGetValue(placeholder, out Vector3 pos) &&
                    Vector3.Distance(pos, placeholder.transform.position) > 0.001f)
                {
                    sceneObjectChanged++;
                }
                else if (rotationSnapshot.TryGetValue(placeholder, out Quaternion rot) &&
                         Quaternion.Angle(rot, placeholder.transform.rotation) > 0.01f)
                {
                    sceneObjectChanged++;
                }
                else if (scaleSnapshot.TryGetValue(placeholder, out Vector3 scl) &&
                         Vector3.Distance(scl, placeholder.transform.localScale) > 0.001f)
                {
                    sceneObjectChanged++;
                }
                else if (nameSnapshot.TryGetValue(placeholder, out string name) &&
                         placeholder.name != name)
                {
                    sceneObjectChanged++;
                }
            }

            // ===== 검증 결과 출력 =====

            // 5. 모든 plan ZoneId non-empty
            if (emptyZoneId == 0)
            {
                log.AppendLine("  [PASS] All plan ZoneId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyZoneId} plan(s) have empty ZoneId.");
                failCount++;
            }

            // 6. 모든 plan MarkerId non-empty
            if (emptyMarkerId == 0)
            {
                log.AppendLine("  [PASS] All plan MarkerId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyMarkerId} plan(s) have empty MarkerId.");
                failCount++;
            }

            // 7. 모든 plan ProfileId non-empty
            if (emptyProfileId == 0)
            {
                log.AppendLine("  [PASS] All plan ProfileId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyProfileId} plan(s) have empty ProfileId.");
                failCount++;
            }

            // 8. 모든 plan Prefab non-null
            if (nullPrefab == 0)
            {
                log.AppendLine("  [PASS] All plan Prefab references are non-null.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {nullPrefab} plan(s) have null Prefab.");
                failCount++;
            }

            // 9. 모든 plan PrefabName non-empty
            if (emptyPrefabName == 0)
            {
                log.AppendLine("  [PASS] All plan PrefabName values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyPrefabName} plan(s) have empty PrefabName.");
                failCount++;
            }

            // 10. 모든 plan PlaceholderName non-empty
            if (emptyPlaceholderName == 0)
            {
                log.AppendLine("  [PASS] All plan PlaceholderName values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyPlaceholderName} plan(s) have empty PlaceholderName.");
                failCount++;
            }

            // 11. plan build 성공률
            if (totalPlanFailed == 0)
            {
                log.AppendLine($"  [PASS] All {totalPlansBuilt} plans built successfully.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {totalPlanFailed} plan(s) failed to build.");
                failCount++;
                if (failedPlaceholders.Count > 0)
                {
                    log.AppendLine("  Failed placeholders (first 20):");
                    foreach (string failInfo in failedPlaceholders)
                    {
                        log.AppendLine($"    [FAIL] {failInfo}");
                    }
                }
            }

            // 12. fallback plan count (INFO)
            log.AppendLine($"  [INFO] Fallback profile plans: {fallbackCount}");

            // 13. editor placeholder prefab plan count (INFO)
            log.AppendLine($"  [INFO] Editor placeholder prefab plans: {editorPlaceholderCount}");

            // 14. scene object transform/name 변경 없음
            if (sceneObjectChanged == 0)
            {
                log.AppendLine("  [PASS] No scene object transform/name was modified by resolver.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {sceneObjectChanged} placeholder(s) had transform/name changes detected.");
                failCount++;
            }

            // category별 breakdown
            log.AppendLine("  [INFO] Category breakdown:");
            foreach (KeyValuePair<string, int> kvp in categoryCount)
            {
                log.AppendLine($"         {kvp.Key}: {kvp.Value}");
            }

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine($"  Total placeholders: {allPlaceholders.Count}");
            log.AppendLine($"  Plans built: {totalPlansBuilt}");
            log.AppendLine($"  Plans failed: {totalPlanFailed}");
            log.AppendLine($"  Fallback profiles: {fallbackCount}");
            log.AppendLine($"  Editor placeholder prefabs: {editorPlaceholderCount}");
            log.AppendLine("===== Phase 14.10-J-1: Validate Runtime Spawn Replacement Plans Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// ZoneRoots parent Transform 하위의 모든 RuntimePlaceholder GameObject를 재귀적으로 수집한다.
        /// 기준: WorldMapRuntimePlaceholderBinding 컴포넌트를 가진 GameObject 또는 이름이 "RuntimePlaceholder_"로 시작하는 GameObject.
        /// 중복 카운트가 생기지 않도록 HashSet을 사용한다.
        /// </summary>
        private static HashSet<GameObject> CollectAllRuntimePlaceholders(Transform zoneRootsTransform)
        {
            HashSet<GameObject> result = new HashSet<GameObject>();

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
        /// Transform 하위를 재귀적으로 탐색하여 RuntimePlaceholder GameObject를 수집한다.
        /// 기준: WorldMapRuntimePlaceholderBinding 컴포넌트를 가졌거나 이름이 "RuntimePlaceholder_"로 시작하는 GameObject.
        /// </summary>
        private static void CollectPlaceholdersRecursive(Transform parent, HashSet<GameObject> result)
        {
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null) continue;

                // 기준 1: WorldMapRuntimePlaceholderBinding 컴포넌트를 가진 GameObject
                bool hasBinding = child.GetComponent<WorldMapRuntimePlaceholderBinding>() != null;

                // 기준 2: 이름이 "RuntimePlaceholder_"로 시작하는 GameObject
                bool nameMatch = child.name.StartsWith(PlaceholderPrefix + "_");

                if (hasBinding || nameMatch)
                {
                    result.Add(child.gameObject);
                }

                CollectPlaceholdersRecursive(child, result);
            }
        }
    }
}
