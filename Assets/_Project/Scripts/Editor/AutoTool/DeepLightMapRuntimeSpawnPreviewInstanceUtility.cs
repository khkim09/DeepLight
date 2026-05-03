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
    /// Phase 14.10-J-2: Runtime Spawn Preview Instance Utility.
    /// RuntimePlaceholder를 직접 교체하지 않고, Replacement Plan마다 대응되는 prefab instance를
    /// 별도 RuntimeSpawnInstances root 아래 생성/검증/삭제한다.
    /// 실제 RuntimePlaceholder 제거/대체는 Phase 14.10-J-3 이후로 넘긴다.
    /// </summary>
    public static class DeepLightMapRuntimeSpawnPreviewInstanceUtility
    {
        private const string LogPrefix = "[SpawnPreviewInstance]";
        private const string PlaceholderPrefix = "RuntimePlaceholder";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/World/Content/WorldMapRuntimeSpawnProfileDatabase.asset";
        private const string RuntimeSpawnInstancesRootName = "RuntimeSpawnInstances";
        private const string UnknownCategoryName = "Unknown";

        // ===== Public API =====

        /// <summary>
        /// 모든 RuntimePlaceholder에 대해 Replacement Plan을 빌드하고,
        /// 각 plan에 대응되는 preview prefab instance를 ZoneRoot 하위 RuntimeSpawnInstances root에 생성한다.
        /// RuntimePlaceholder 원본은 절대 수정/삭제하지 않는다.
        /// 기존 Preview Instance는 재실행 시 중복되지 않도록 제거 후 다시 생성한다.
        /// </summary>
        public static void RebuildRuntimeSpawnPreviewInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot rebuild runtime spawn preview instances.");
                return;
            }
            if (context == null)
            {
                Debug.LogError($"{LogPrefix} Context is null! Cannot rebuild runtime spawn preview instances.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-2: Rebuild Runtime Spawn Preview Instances =====");

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

            // 4. 기존 Preview Instance 제거 (중복 방지)
            int clearedCount = ClearPreviewInstancesInternal(generatedRoot, log);

            // 5. 모든 RuntimePlaceholder 수집 (중복 방지 HashSet 사용)
            HashSet<GameObject> placeholderSet = CollectAllRuntimePlaceholders(zoneRootsTransform);
            List<GameObject> allPlaceholders = new List<GameObject>(placeholderSet);
            log.AppendLine($"  [OK] Found {allPlaceholders.Count} unique RuntimePlaceholder GameObjects.");

            if (allPlaceholders.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No RuntimePlaceholder GameObjects found. Nothing to generate.");
                log.AppendLine("  [WARN] No RuntimePlaceholder GameObjects found.");
                Debug.Log(log.ToString());
                return;
            }

            // 6. 각 placeholder에 대해 plan build + preview instance 생성
            int totalSuccess = 0;
            int totalFail = 0;
            int totalFallback = 0;
            int totalEditorPlaceholder = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // 실패한 placeholder 목록
            List<string> failedPlaceholders = new List<string>();

            // ZoneRoot_XX별 RuntimeSpawnInstances root 캐시
            Dictionary<string, Transform> zoneInstanceRootCache = new Dictionary<string, Transform>();

            // RuntimeSpawnInstances/{category} root 캐시 (zone별)
            Dictionary<string, Dictionary<string, Transform>> categoryRootCache = new Dictionary<string, Dictionary<string, Transform>>();

            // 이름 충돌 방지를 위한 instance 이름 카운터
            Dictionary<string, int> instanceNameCounter = new Dictionary<string, int>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // plan build
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
                    string cat = !string.IsNullOrEmpty(plan.RuntimeCategory) ? plan.RuntimeCategory : UnknownCategoryName;
                    if (categoryCount.ContainsKey(cat))
                    {
                        categoryCount[cat]++;
                    }
                    else
                    {
                        categoryCount[cat] = 1;
                    }

                    // ZoneRoot_XX 찾기
                    Transform zoneRoot = FindZoneRootForPlaceholder(placeholder, zoneRootsTransform);
                    if (zoneRoot == null)
                    {
                        log.AppendLine($"  [WARN] Could not find ZoneRoot for placeholder '{placeholder.name}'. Skipping instance creation.");
                        continue;
                    }

                    // RuntimeSpawnInstances root 획득 (zone별 캐시)
                    Transform instanceRoot = GetOrCreateRuntimeSpawnInstancesRoot(zoneRoot, zoneInstanceRootCache);

                    // category root 획득 (zone별 캐시)
                    Transform categoryRoot = GetOrCreateCategoryRoot(instanceRoot, cat, zoneRoot.name, categoryRootCache);

                    // instance 이름 생성 (충돌 방지)
                    string baseName = SanitizeName($"SpawnInstance_{plan.ProfileId}_{plan.MarkerId}");
                    string instanceName = baseName;
                    if (instanceNameCounter.ContainsKey(instanceName))
                    {
                        instanceNameCounter[instanceName]++;
                        instanceName = $"{baseName}_{instanceNameCounter[instanceName]:D3}";
                    }
                    else
                    {
                        instanceNameCounter[instanceName] = 0;
                    }

                    // PrefabUtility.InstantiatePrefab 사용
                    GameObject instance = PrefabUtility.InstantiatePrefab(plan.Prefab, categoryRoot) as GameObject;
                    if (instance == null)
                    {
                        log.AppendLine($"  [WARN] Failed to instantiate prefab '{plan.PrefabName}' for placeholder '{placeholder.name}'.");
                        continue;
                    }

                    // instance 이름 설정
                    instance.name = instanceName;

                    // transform 설정
                    instance.transform.position = plan.WorldPosition;
                    instance.transform.rotation = plan.WorldRotation;
                    // prefab 내부 root scale 보존 (plan.WorldScale는 placeholder의 lossyScale이므로 사용하지 않음)
                    // localScale은 prefab 자체의 root scale을 유지하도록 Vector3.one으로 설정
                    instance.transform.localScale = Vector3.one;

                    // WorldMapRuntimeSpawnInstanceTag 부착
                    WorldMapRuntimeSpawnInstanceTag tag = instance.GetComponent<WorldMapRuntimeSpawnInstanceTag>();
                    if (tag == null)
                    {
                        tag = instance.AddComponent<WorldMapRuntimeSpawnInstanceTag>();
                    }
                    tag.Configure(plan, true);

                    // WorldMapRuntimePlaceholderBinding / WorldMapZoneContentMarker는 추가하지 않음
                }
                else
                {
                    totalFail++;
                    failedPlaceholders.Add($"{placeholder.name}: {plan.FailureReason}");
                }
            }

            // 7. 결과 로그
            log.AppendLine($"  Preview instances created: {totalSuccess}");
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

            log.AppendLine("");
            log.AppendLine("===== Phase 14.10-J-2: Rebuild Runtime Spawn Preview Instances Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Runtime Spawn Preview Instance의 유효성을 검사한다.
        /// 18개 항목을 검사하고 Console에 [PASS]/[FAIL]/[WARN] summary를 출력한다.
        /// RuntimePlaceholder 원본 count/transform/name 변경 없음을 검증한다.
        /// </summary>
        public static void ValidateRuntimeSpawnPreviewInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot validate runtime spawn preview instances.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-2: Validate Runtime Spawn Preview Instances =====");

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
                log.AppendLine("===== Phase 14.10-J-2: Validate Runtime Spawn Preview Instances Complete =====");
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
                log.AppendLine("===== Phase 14.10-J-2: Validate Runtime Spawn Preview Instances Complete =====");
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
                log.AppendLine("===== Phase 14.10-J-2: Validate Runtime Spawn Preview Instances Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4. 모든 RuntimePlaceholder 수집 (원본 보존 검증용)
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
                log.AppendLine("===== Phase 14.10-J-2: Validate Runtime Spawn Preview Instances Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 5. Replacement Plan build success count == RuntimePlaceholder count
            int totalPlansBuilt = 0;
            int totalPlanFailed = 0;
            int fallbackCount = 0;
            int editorPlaceholderCount = 0;

            // category별 count
            Dictionary<string, int> categoryCount = new Dictionary<string, int>();

            // placeholder snapshot (원본 보존 검증용)
            Dictionary<string, GameObject> placeholderByName = new Dictionary<string, GameObject>();
            Dictionary<GameObject, Vector3> positionSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, Quaternion> rotationSnapshot = new Dictionary<GameObject, Quaternion>();
            Dictionary<GameObject, Vector3> scaleSnapshot = new Dictionary<GameObject, Vector3>();
            Dictionary<GameObject, string> nameSnapshot = new Dictionary<GameObject, string>();

            foreach (GameObject placeholder in allPlaceholders)
            {
                if (placeholder == null) continue;

                // 이름 기반 dictionary 구축
                if (!placeholderByName.ContainsKey(placeholder.name))
                {
                    placeholderByName[placeholder.name] = placeholder;
                }

                // snapshot 저장
                positionSnapshot[placeholder] = placeholder.transform.position;
                rotationSnapshot[placeholder] = placeholder.transform.rotation;
                scaleSnapshot[placeholder] = placeholder.transform.localScale;
                nameSnapshot[placeholder] = placeholder.name;

                // plan build
                if (WorldMapRuntimeSpawnReplacementResolver.TryBuildPlan(placeholder, database, out WorldMapRuntimeSpawnReplacementPlan plan))
                {
                    totalPlansBuilt++;

                    if (plan.IsFallbackProfile)
                    {
                        fallbackCount++;
                    }

                    if (plan.IsEditorPlaceholderPrefab)
                    {
                        editorPlaceholderCount++;
                    }

                    string cat = !string.IsNullOrEmpty(plan.RuntimeCategory) ? plan.RuntimeCategory : UnknownCategoryName;
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
                }
            }

            // 5. plan build 성공률
            if (totalPlanFailed == 0)
            {
                log.AppendLine($"  [PASS] All {totalPlansBuilt} plans built successfully.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {totalPlanFailed} plan(s) failed to build.");
                failCount++;
            }

            // 6. Preview instance count == successful plan count
            List<WorldMapRuntimeSpawnInstanceTag> allPreviewTags = CollectAllPreviewInstanceTags(generatedRoot);
            int previewInstanceCount = allPreviewTags.Count;

            if (previewInstanceCount == totalPlansBuilt)
            {
                log.AppendLine($"  [PASS] Preview instance count ({previewInstanceCount}) matches successful plan count ({totalPlansBuilt}).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Preview instance count ({previewInstanceCount}) does not match successful plan count ({totalPlansBuilt}).");
                failCount++;
            }

            // 7. 모든 preview instance에 WorldMapRuntimeSpawnInstanceTag 존재
            int missingTag = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag == null)
                {
                    missingTag++;
                }
            }
            if (missingTag == 0)
            {
                log.AppendLine("  [PASS] All preview instances have WorldMapRuntimeSpawnInstanceTag.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {missingTag} preview instance(s) missing WorldMapRuntimeSpawnInstanceTag.");
                failCount++;
            }

            // 8. 모든 tag.IsPreviewInstance == true
            int notPreview = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag != null && !tag.IsPreviewInstance)
                {
                    notPreview++;
                }
            }
            if (notPreview == 0)
            {
                log.AppendLine("  [PASS] All tags have IsPreviewInstance == true.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {notPreview} tag(s) have IsPreviewInstance == false.");
                failCount++;
            }

            // 9. 모든 tag.ZoneId non-empty
            int emptyZoneId = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.ZoneId))
                {
                    emptyZoneId++;
                }
            }
            if (emptyZoneId == 0)
            {
                log.AppendLine("  [PASS] All tag ZoneId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyZoneId} tag(s) have empty ZoneId.");
                failCount++;
            }

            // 10. 모든 tag.MarkerId non-empty
            int emptyMarkerId = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.MarkerId))
                {
                    emptyMarkerId++;
                }
            }
            if (emptyMarkerId == 0)
            {
                log.AppendLine("  [PASS] All tag MarkerId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyMarkerId} tag(s) have empty MarkerId.");
                failCount++;
            }

            // 11. 모든 tag.ProfileId non-empty
            int emptyProfileId = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag != null && string.IsNullOrEmpty(tag.ProfileId))
                {
                    emptyProfileId++;
                }
            }
            if (emptyProfileId == 0)
            {
                log.AppendLine("  [PASS] All tag ProfileId values are non-empty.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {emptyProfileId} tag(s) have empty ProfileId.");
                failCount++;
            }

            // 12. 모든 preview instance에 WorldMapRuntimePlaceholderBinding 없음
            int hasBinding = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag != null)
                {
                    WorldMapRuntimePlaceholderBinding binding = tag.GetComponent<WorldMapRuntimePlaceholderBinding>();
                    if (binding != null)
                    {
                        hasBinding++;
                    }
                }
            }
            if (hasBinding == 0)
            {
                log.AppendLine("  [PASS] No preview instance has WorldMapRuntimePlaceholderBinding.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {hasBinding} preview instance(s) have WorldMapRuntimePlaceholderBinding.");
                failCount++;
            }

            // 13. 모든 preview instance에 WorldMapZoneContentMarker 없음
            int hasMarker = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag != null)
                {
                    WorldMapZoneContentMarker marker = tag.GetComponent<WorldMapZoneContentMarker>();
                    if (marker != null)
                    {
                        hasMarker++;
                    }
                }
            }
            if (hasMarker == 0)
            {
                log.AppendLine("  [PASS] No preview instance has WorldMapZoneContentMarker.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {hasMarker} preview instance(s) have WorldMapZoneContentMarker.");
                failCount++;
            }

            // 14. 모든 preview instance 위치가 source placeholder 위치와 0.1 unit 이내
            int positionMismatch = 0;
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allPreviewTags)
            {
                if (tag == null) continue;

                // SourcePlaceholderName으로 placeholder 찾기
                if (placeholderByName.TryGetValue(tag.SourcePlaceholderName, out GameObject sourcePlaceholder))
                {
                    if (sourcePlaceholder != null)
                    {
                        float distance = Vector3.Distance(tag.transform.position, sourcePlaceholder.transform.position);
                        if (distance > 0.1f)
                        {
                            positionMismatch++;
                        }
                    }
                }
            }
            if (positionMismatch == 0)
            {
                log.AppendLine("  [PASS] All preview instance positions match source placeholder positions (within 0.1 unit).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {positionMismatch} preview instance(s) have position mismatch > 0.1 unit.");
                failCount++;
            }

            // 15. fallback preview count (INFO)
            log.AppendLine($"  [INFO] Fallback preview instances: {fallbackCount}");

            // 16. editor placeholder prefab preview count (INFO)
            log.AppendLine($"  [INFO] Editor placeholder prefab preview instances: {editorPlaceholderCount}");

            // 17. category별 breakdown (INFO)
            log.AppendLine("  [INFO] Category breakdown:");
            foreach (KeyValuePair<string, int> kvp in categoryCount)
            {
                log.AppendLine($"         {kvp.Key}: {kvp.Value}");
            }

            // 18. RuntimePlaceholder 원본 count가 validate 전후 동일한지 확인
            HashSet<GameObject> placeholderSetAfter = CollectAllRuntimePlaceholders(zoneRootsTransform);
            int placeholderCountAfter = placeholderSetAfter.Count;
            if (placeholderCountAfter == allPlaceholders.Count)
            {
                log.AppendLine($"  [PASS] RuntimePlaceholder count unchanged: {placeholderCountAfter}.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] RuntimePlaceholder count changed! Before: {allPlaceholders.Count}, After: {placeholderCountAfter}.");
                failCount++;
            }

            // 19. RuntimePlaceholder 원본 transform/name 변경 없음 확인
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
            if (sceneObjectChanged == 0)
            {
                log.AppendLine("  [PASS] No RuntimePlaceholder transform/name was modified.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {sceneObjectChanged} RuntimePlaceholder(s) had transform/name changes detected.");
                failCount++;
            }

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine($"  Total placeholders: {allPlaceholders.Count}");
            log.AppendLine($"  Plans built: {totalPlansBuilt}");
            log.AppendLine($"  Plans failed: {totalPlanFailed}");
            log.AppendLine($"  Preview instances: {previewInstanceCount}");
            log.AppendLine($"  Fallback profiles: {fallbackCount}");
            log.AppendLine($"  Editor placeholder prefabs: {editorPlaceholderCount}");
            log.AppendLine("===== Phase 14.10-J-2: Validate Runtime Spawn Preview Instances Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedWorldRoot 하위의 모든 Preview Instance를 삭제한다.
        /// 삭제 대상: WorldMapRuntimeSpawnInstanceTag.IsPreviewInstance == true 인 GameObject.
        /// RuntimePlaceholder / Marker / DebugVisual은 절대 삭제하지 않는다.
        /// </summary>
        public static void ClearRuntimeSpawnPreviewInstances(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Settings is null! Cannot clear runtime spawn preview instances.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-J-2: Clear Runtime Spawn Preview Instances =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogWarning($"{LogPrefix} GeneratedWorldRoot not found. Nothing to clear.");
                log.AppendLine("  [WARN] GeneratedWorldRoot not found. Nothing to clear.");
                Debug.Log(log.ToString());
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. Preview Instance 제거
            int clearedCount = ClearPreviewInstancesInternal(generatedRoot, log);

            log.AppendLine($"  [OK] Cleared {clearedCount} preview instance(s).");
            log.AppendLine("  [OK] RuntimePlaceholder / Marker / DebugVisual preserved.");
            log.AppendLine("");
            log.AppendLine("===== Phase 14.10-J-2: Clear Runtime Spawn Preview Instances Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// GeneratedWorldRoot 하위의 모든 Preview Instance를 제거하고 제거된 개수를 반환한다.
        /// 삭제 대상: WorldMapRuntimeSpawnInstanceTag.IsPreviewInstance == true 인 GameObject.
        /// RuntimePlaceholder / Marker / DebugVisual은 절대 삭제하지 않는다.
        /// 빈 RuntimeSpawnInstances root는 하위가 비면 삭제한다.
        /// </summary>
        private static int ClearPreviewInstancesInternal(GameObject generatedRoot, StringBuilder log)
        {
            int clearedCount = 0;

            // GeneratedWorldRoot 하위의 모든 WorldMapRuntimeSpawnInstanceTag 수집
            List<WorldMapRuntimeSpawnInstanceTag> allTags = new List<WorldMapRuntimeSpawnInstanceTag>(
                generatedRoot.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true));

            // IsPreviewInstance == true 인 것만 삭제
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allTags)
            {
                if (tag != null && tag.IsPreviewInstance)
                {
                    toDestroy.Add(tag.gameObject);
                }
            }

            // 중복 제거 (같은 GameObject에 여러 tag가 있을 수 있음)
            HashSet<GameObject> uniqueToDestroy = new HashSet<GameObject>(toDestroy);

            foreach (GameObject obj in uniqueToDestroy)
            {
                if (obj != null)
                {
                    // 부모 Transform 기록 (빈 root 정리용)
                    Transform parentTransform = obj.transform.parent;

                    Undo.DestroyObjectImmediate(obj);
                    clearedCount++;

                    // 부모가 RuntimeSpawnInstances root이고, 하위가 비었으면 삭제
                    if (parentTransform != null && parentTransform.name == RuntimeSpawnInstancesRootName)
                    {
                        if (parentTransform.childCount == 0)
                        {
                            Transform grandParent = parentTransform.parent;
                            Undo.DestroyObjectImmediate(parentTransform.gameObject);
                            log.AppendLine($"  [INFO] Removed empty RuntimeSpawnInstances root under '{grandParent?.name ?? "null"}'.");
                        }
                    }
                }
            }

            log.AppendLine($"  [OK] Found {uniqueToDestroy.Count} preview instance(s) to clear.");
            return clearedCount;
        }

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

        /// <summary>
        /// GeneratedWorldRoot 하위의 모든 WorldMapRuntimeSpawnInstanceTag를 수집한다.
        /// </summary>
        private static List<WorldMapRuntimeSpawnInstanceTag> CollectAllPreviewInstanceTags(GameObject generatedRoot)
        {
            List<WorldMapRuntimeSpawnInstanceTag> result = new List<WorldMapRuntimeSpawnInstanceTag>();

            if (generatedRoot == null) return result;

            WorldMapRuntimeSpawnInstanceTag[] allTags = generatedRoot.GetComponentsInChildren<WorldMapRuntimeSpawnInstanceTag>(true);
            foreach (WorldMapRuntimeSpawnInstanceTag tag in allTags)
            {
                if (tag != null && tag.IsPreviewInstance)
                {
                    result.Add(tag);
                }
            }

            return result;
        }

        /// <summary>
        /// placeholder GameObject가 속한 ZoneRoot_XX Transform을 찾는다.
        /// 부모 체인을 따라 올라가면서 ZoneRoot_XX로 시작하는 Transform을 반환한다.
        /// </summary>
        private static Transform FindZoneRootForPlaceholder(GameObject placeholder, Transform zoneRootsTransform)
        {
            if (placeholder == null) return null;

            Transform current = placeholder.transform.parent;
            while (current != null)
            {
                if (current.name.StartsWith("ZoneRoot_"))
                {
                    return current;
                }
                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// ZoneRoot 하위에 RuntimeSpawnInstances root를 찾거나 생성한다.
        /// zoneInstanceRootCache를 사용하여 중복 검색을 방지한다.
        /// </summary>
        private static Transform GetOrCreateRuntimeSpawnInstancesRoot(
            Transform zoneRoot,
            Dictionary<string, Transform> zoneInstanceRootCache)
        {
            if (zoneRoot == null) return null;

            string zoneKey = zoneRoot.name;

            // 캐시 확인
            if (zoneInstanceRootCache.TryGetValue(zoneKey, out Transform cached))
            {
                if (cached != null) return cached;
            }

            // RuntimeSpawnInstances root 찾기
            Transform instanceRoot = zoneRoot.Find(RuntimeSpawnInstancesRootName);
            if (instanceRoot == null)
            {
                // 새로 생성
                GameObject newRoot = new GameObject(RuntimeSpawnInstancesRootName);
                newRoot.transform.SetParent(zoneRoot);
                newRoot.transform.localPosition = Vector3.zero;
                newRoot.transform.localRotation = Quaternion.identity;
                newRoot.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(newRoot, $"Create {RuntimeSpawnInstancesRootName}");
                instanceRoot = newRoot.transform;
            }

            zoneInstanceRootCache[zoneKey] = instanceRoot;
            return instanceRoot;
        }

        /// <summary>
        /// RuntimeSpawnInstances root 하위에 category root를 찾거나 생성한다.
        /// categoryRootCache를 사용하여 중복 검색을 방지한다.
        /// </summary>
        private static Transform GetOrCreateCategoryRoot(
            Transform instanceRoot,
            string category,
            string zoneName,
            Dictionary<string, Dictionary<string, Transform>> categoryRootCache)
        {
            if (instanceRoot == null) return null;

            string zoneKey = zoneName;

            // zone별 캐시 확인
            if (!categoryRootCache.ContainsKey(zoneKey))
            {
                categoryRootCache[zoneKey] = new Dictionary<string, Transform>();
            }

            Dictionary<string, Transform> zoneCache = categoryRootCache[zoneKey];

            // category 캐시 확인
            if (zoneCache.TryGetValue(category, out Transform cached))
            {
                if (cached != null) return cached;
            }

            // category root 찾기
            Transform categoryRoot = instanceRoot.Find(category);
            if (categoryRoot == null)
            {
                // 새로 생성
                GameObject newCategoryRoot = new GameObject(category);
                newCategoryRoot.transform.SetParent(instanceRoot);
                newCategoryRoot.transform.localPosition = Vector3.zero;
                newCategoryRoot.transform.localRotation = Quaternion.identity;
                newCategoryRoot.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(newCategoryRoot, $"Create {category}");
                categoryRoot = newCategoryRoot.transform;
            }

            zoneCache[category] = categoryRoot;
            return categoryRoot;
        }

        /// <summary>
        /// 문자열을 파일/오브젝트 이름에 안전하도록 sanitize 처리한다.
        /// 공백, 특수문자를 언더스코어로 치환한다.
        /// </summary>
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                // 유효하지 않은 파일 이름 문자를 언더스코어로 치환
                bool isInvalid = false;
                for (int j = 0; j < invalidChars.Length; j++)
                {
                    if (chars[i] == invalidChars[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }
                if (isInvalid || chars[i] == ' ')
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
